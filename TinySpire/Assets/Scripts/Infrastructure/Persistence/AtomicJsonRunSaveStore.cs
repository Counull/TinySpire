using System;
using System.IO;
using System.Security;
using System.Text;
using TinySpire.Run;

namespace TinySpire.Infrastructure.Persistence
{
    /// <summary>为原子单槽存档隔离可故障注入的文件系统边界。</summary>
    internal interface IRunSaveFileSystem
    {
        /// <summary>判断指定文件是否存在。</summary>
        bool FileExists(string path);

        /// <summary>确保目标目录存在。</summary>
        void CreateDirectory(string path);

        /// <summary>以严格 UTF-8 读取完整文本。</summary>
        string ReadAllText(string path);

        /// <summary>把完整文本持久刷新到指定临时文件。</summary>
        void WriteAllTextDurably(string path, string content);

        /// <summary>在正式档不存在时以同卷移动完成首次提交。</summary>
        void MoveFile(string sourcePath, string destinationPath);

        /// <summary>在正式档存在时以平台原子替换完成提交。</summary>
        void ReplaceFile(string sourcePath, string destinationPath);

        /// <summary>删除指定文件。</summary>
        void DeleteFile(string path);
    }

    /// <summary>使用 System.IO 实现当前 Editor/Standalone 的真实存储边界。</summary>
    internal sealed class PhysicalAtomicRunSaveFileSystem : IRunSaveFileSystem
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>查询真实文件是否存在。</summary>
        public bool FileExists(string path)
        {
            try
            {
                File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        /// <summary>创建尚不存在的真实目录。</summary>
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>严格读取真实 UTF-8 文件。</summary>
        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, StrictUtf8);
        }

        /// <summary>独占写入临时文件并请求把内容刷新到持久介质。</summary>
        public void WriteAllTextDurably(string path, string content)
        {
            byte[] payload = StrictUtf8.GetBytes(content);
            using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            stream.Write(payload, 0, payload.Length);
            stream.Flush(flushToDisk: true);
        }

        /// <summary>把首次提交的临时文件移动为正式文件。</summary>
        public void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>用系统原子替换语义覆盖已有正式文件，且不提供非原子降级。</summary>
        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null);
        }

        /// <summary>删除真实文件。</summary>
        public void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    /// <summary>在一个目录内以临时文件校验加原子替换实现 versioned JSON 单槽存档。</summary>
    public sealed class AtomicJsonRunSaveStore : IRunSaveStore
    {
        internal const string LiveFileName = "run-save.json";
        internal const string TemporaryFileName = "run-save.json.tmp";

        private readonly string _directoryPath;
        private readonly string _saveFilePath;
        private readonly string _temporaryFilePath;
        private readonly IRunSaveFileSystem _fileSystem;

        /// <summary>在指定目录建立真实文件系统单槽 Adapter。</summary>
        public AtomicJsonRunSaveStore(string directoryPath)
            : this(directoryPath, new PhysicalAtomicRunSaveFileSystem())
        {
        }

        /// <summary>以可控文件系统边界建立 Adapter，供故障路径验证。</summary>
        internal AtomicJsonRunSaveStore(
            string directoryPath,
            IRunSaveFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Run save directory is required.", nameof(directoryPath));

            _directoryPath = Path.GetFullPath(directoryPath);
            _saveFilePath = Path.Combine(_directoryPath, LiveFileName);
            _temporaryFilePath = Path.Combine(_directoryPath, TemporaryFileName);
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>读取最近成功的正式档；临时物与坏档只作为显式诊断，不自动修复或删除。</summary>
        public RunSaveLoadResult Load()
        {
            bool hasTemporaryFile = false;
            try
            {
                hasTemporaryFile = _fileSystem.FileExists(_temporaryFilePath);
                if (!_fileSystem.FileExists(_saveFilePath))
                {
                    return hasTemporaryFile
                        ? RunSaveLoadResult.Failed(
                            RunSaveLoadStatus.InterruptedCommit,
                            "A temporary Run save exists without a successful checkpoint.",
                            hasStoredData: true,
                            hasPendingTemporaryFile: true)
                        : RunSaveLoadResult.NotFound();
                }

                RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(_saveFilePath));
                if (read.Status == RunSaveDocumentReadStatus.Success)
                {
                    return RunSaveLoadResult.Succeeded(
                        read.Document,
                        hasTemporaryFile,
                        hasTemporaryFile
                            ? "A failed or interrupted newer commit remains for diagnosis."
                            : string.Empty);
                }

                return RunSaveLoadResult.Failed(
                    ToLoadStatus(read.Status),
                    read.Detail,
                    hasStoredData: true,
                    hasPendingTemporaryFile: hasTemporaryFile);
            }
            catch (DecoderFallbackException exception)
            {
                return RunSaveLoadResult.Failed(
                    RunSaveLoadStatus.InvalidJson,
                    exception.Message,
                    hasStoredData: true,
                    hasPendingTemporaryFile: hasTemporaryFile);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunSaveLoadResult.Failed(
                    RunSaveLoadStatus.IoFailure,
                    exception.Message,
                    hasStoredData: true,
                    hasPendingTemporaryFile: hasTemporaryFile);
            }
        }

        /// <summary>完整写入并回读校验临时档，之后只用同卷 move 或原子 replace 发布正式档。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            if (document == null)
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.InvalidDocument,
                    "Run save document is missing.");
            }

            string serialized = RunSaveDocumentCodec.Serialize(document);
            RunSaveDocumentReadResult sourceRead = RunSaveDocumentCodec.Read(serialized);
            if (sourceRead.Status != RunSaveDocumentReadStatus.Success ||
                !DocumentsEqual(document, sourceRead.Document))
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.InvalidDocument,
                    sourceRead.Detail.Length > 0
                        ? sourceRead.Detail
                        : "Run save document failed its pre-write validation.");
            }

            try
            {
                _fileSystem.CreateDirectory(_directoryPath);
                _fileSystem.WriteAllTextDurably(_temporaryFilePath, serialized);

                RunSaveDocumentReadResult temporaryRead = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(_temporaryFilePath));
                if (temporaryRead.Status != RunSaveDocumentReadStatus.Success ||
                    !DocumentsEqual(document, temporaryRead.Document))
                {
                    return RunSaveCommitResult.Failed(
                        RunSaveCommitStatus.InvalidDocument,
                        temporaryRead.Detail.Length > 0
                            ? temporaryRead.Detail
                            : "The temporary Run save did not match the requested checkpoint.");
                }

                if (_fileSystem.FileExists(_saveFilePath))
                    _fileSystem.ReplaceFile(_temporaryFilePath, _saveFilePath);
                else
                    _fileSystem.MoveFile(_temporaryFilePath, _saveFilePath);

                return RunSaveCommitResult.Succeeded();
            }
            catch (DecoderFallbackException exception)
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.InvalidDocument,
                    exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.IoFailure,
                    exception.Message);
            }
        }

        /// <summary>仅由已确认的调用方幂等删除诊断临时物与正式单槽。</summary>
        public RunSaveDeleteResult Delete()
        {
            try
            {
                if (_fileSystem.FileExists(_temporaryFilePath))
                    _fileSystem.DeleteFile(_temporaryFilePath);
                if (_fileSystem.FileExists(_saveFilePath))
                    _fileSystem.DeleteFile(_saveFilePath);
                return RunSaveDeleteResult.Succeeded();
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunSaveDeleteResult.Failed(exception.Message);
            }
        }

        /// <summary>把文档读取分类映射到 port 的存储读取分类。</summary>
        private static RunSaveLoadStatus ToLoadStatus(RunSaveDocumentReadStatus status)
        {
            switch (status)
            {
                case RunSaveDocumentReadStatus.InvalidJson:
                    return RunSaveLoadStatus.InvalidJson;
                case RunSaveDocumentReadStatus.InvalidDocument:
                    return RunSaveLoadStatus.InvalidDocument;
                case RunSaveDocumentReadStatus.UnsupportedSchema:
                    return RunSaveLoadStatus.UnsupportedSchema;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        /// <summary>逐字段确认回读临时档就是本次完整 checkpoint，而非仅可解析的其他文档。</summary>
        private static bool DocumentsEqual(RunSaveDocument left, RunSaveDocument right)
        {
            return left != null &&
                   right != null &&
                   left.SchemaVersion == right.SchemaVersion &&
                   left.RunId == right.RunId &&
                   left.HeroTemplateId == right.HeroTemplateId &&
                   left.CurrentHealth == right.CurrentHealth &&
                   left.MaxHealth == right.MaxHealth &&
                   left.DeckTemplateId == right.DeckTemplateId &&
                   left.EncounterTemplateId == right.EncounterTemplateId &&
                   left.RandomRootSeed == right.RandomRootSeed &&
                   left.NodeStatus == right.NodeStatus &&
                   left.BattleAttemptSequence == right.BattleAttemptSequence;
        }

        /// <summary>只把可预期的本地存储异常转换为 typed failure，编程错误继续 fail-fast。</summary>
        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is NotSupportedException;
        }
    }
}
