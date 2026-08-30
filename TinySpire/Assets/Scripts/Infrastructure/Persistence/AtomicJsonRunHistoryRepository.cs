using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using TinySpire.Run;
using TinySpire.Run.History;

namespace TinySpire.Infrastructure.Persistence
{
    /// <summary>区分历史目录缺失、可用与被非目录对象占用。</summary>
    internal enum RunHistoryDirectoryProbeStatus
    {
        Missing,
        Directory,
        NotDirectory,
    }

    /// <summary>为逐 RunId 不可变历史隔离可故障注入的文件系统边界。</summary>
    internal interface IRunHistoryFileSystem
    {
        /// <summary>探测历史目录路径，且不得把探测故障伪装成缺失。</summary>
        RunHistoryDirectoryProbeStatus ProbeDirectory(string path);

        /// <summary>判断指定文件是否存在。</summary>
        bool FileExists(string path);

        /// <summary>确保目标目录存在。</summary>
        void CreateDirectory(string path);

        /// <summary>按搜索模式枚举目标目录中的文件。</summary>
        string[] GetFiles(string path, string searchPattern);

        /// <summary>以严格 UTF-8 读取完整文本。</summary>
        string ReadAllText(string path);

        /// <summary>把完整文本持久刷新到唯一临时文件。</summary>
        void WriteAllTextDurably(string path, string content);

        /// <summary>只在目标不存在时以同卷移动完成首次提交。</summary>
        void MoveFile(string sourcePath, string destinationPath);

        /// <summary>删除当前提交拥有的临时文件。</summary>
        void DeleteFile(string path);
    }

    /// <summary>使用 System.IO 实现 Editor 与 Windows Standalone 的真实历史边界。</summary>
    internal sealed class PhysicalAtomicRunHistoryFileSystem : IRunHistoryFileSystem
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>以路径属性区分目录缺失、可用目录与普通文件。</summary>
        public RunHistoryDirectoryProbeStatus ProbeDirectory(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.Directory) != 0
                    ? RunHistoryDirectoryProbeStatus.Directory
                    : RunHistoryDirectoryProbeStatus.NotDirectory;
            }
            catch (FileNotFoundException)
            {
                return RunHistoryDirectoryProbeStatus.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return RunHistoryDirectoryProbeStatus.Missing;
            }
        }

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

        /// <summary>创建尚不存在的历史目录。</summary>
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>按模式枚举真实目录中的直接子文件。</summary>
        public string[] GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        /// <summary>严格读取真实 UTF-8 历史文件。</summary>
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
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            stream.Write(payload, 0, payload.Length);
            stream.Flush(flushToDisk: true);
        }

        /// <summary>把唯一临时文件移动为首次正式历史文件。</summary>
        public void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>删除当前提交拥有的真实临时文件。</summary>
        public void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    /// <summary>把每局摘要原子首次创建为 run-history/{RunId}.json 且永不覆盖。</summary>
    public sealed class AtomicJsonRunHistoryRepository : IRunHistoryRepository
    {
        /// <summary>相对于应用持久目录的独立历史目录名。</summary>
        public const string HistoryDirectoryName = "run-history";

        private readonly string _historyDirectoryPath;
        private readonly IRunHistoryFileSystem _fileSystem;

        /// <summary>在应用持久目录下建立真实逐局历史 adapter。</summary>
        public AtomicJsonRunHistoryRepository(string applicationDataDirectoryPath)
            : this(applicationDataDirectoryPath, new PhysicalAtomicRunHistoryFileSystem())
        {
        }

        /// <summary>以可控文件系统建立逐局历史 adapter，供故障路径验证。</summary>
        internal AtomicJsonRunHistoryRepository(
            string applicationDataDirectoryPath,
            IRunHistoryFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(applicationDataDirectoryPath))
            {
                throw new ArgumentException(
                    "Application data directory is required.",
                    nameof(applicationDataDirectoryPath));
            }

            string root = Path.GetFullPath(applicationDataDirectoryPath);
            _historyDirectoryPath = Path.Combine(root, HistoryDirectoryName);
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>按规范 RunId 文件名读取一局完整摘要。</summary>
        public RunHistoryLoadResult Load(RunId runId)
        {
            string summaryPath = GetSummaryPath(runId);
            try
            {
                if (!_fileSystem.FileExists(summaryPath))
                    return RunHistoryLoadResult.NotFound();

                return ReadSummaryFile(summaryPath, runId);
            }
            catch (DecoderFallbackException exception)
            {
                return RunHistoryLoadResult.Failed(
                    RunHistoryLoadStatus.InvalidData,
                    exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunHistoryLoadResult.Failed(
                    RunHistoryLoadStatus.IoFailure,
                    exception.Message);
            }
        }

        /// <summary>原子首次创建一局历史；存在时只比较并返回幂等或冲突。</summary>
        public RunHistoryRecordResult Record(RunSummary summary)
        {
            if (summary == null)
            {
                return RunHistoryRecordResult.Failed(
                    RunHistoryRecordStatus.InvalidData,
                    default,
                    "Run summary is missing.");
            }

            string summaryPath = GetSummaryPath(summary.RunId);
            string temporaryPath = string.Concat(
                summaryPath,
                ".",
                Guid.NewGuid().ToString("N"),
                ".tmp");
            try
            {
                _fileSystem.CreateDirectory(_historyDirectoryPath);
                if (_fileSystem.FileExists(summaryPath))
                    return CompareExisting(summaryPath, summary);

                string serialized = RunHistoryDocumentCodec.Write(summary);
                _fileSystem.WriteAllTextDurably(temporaryPath, serialized);
                RunHistoryDocumentReadResult temporaryRead = RunHistoryDocumentCodec.Read(
                    _fileSystem.ReadAllText(temporaryPath));
                if (temporaryRead.Status != RunHistoryDocumentReadStatus.Success ||
                    !summary.Equals(temporaryRead.Summary))
                {
                    return RunHistoryRecordResult.Failed(
                        RunHistoryRecordStatus.InvalidData,
                        summary.RunId,
                        temporaryRead.Detail.Length > 0
                            ? temporaryRead.Detail
                            : "Temporary Run history did not match the frozen summary.");
                }

                try
                {
                    _fileSystem.MoveFile(temporaryPath, summaryPath);
                }
                catch (IOException) when (_fileSystem.FileExists(summaryPath))
                {
                    return CompareExisting(summaryPath, summary);
                }

                return RunHistoryRecordResult.Recorded(summary.RunId);
            }
            catch (DecoderFallbackException exception)
            {
                return RunHistoryRecordResult.Failed(
                    RunHistoryRecordStatus.InvalidData,
                    summary.RunId,
                    exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunHistoryRecordResult.Failed(
                    RunHistoryRecordStatus.IoFailure,
                    summary.RunId,
                    exception.Message);
            }
            finally
            {
                TryDeleteTemporary(temporaryPath);
            }
        }

        /// <summary>读取并验证全部规范 JSON 文件，任一坏文件都会阻止部分统计发布。</summary>
        public RunHistoryReadAllResult ReadAll()
        {
            try
            {
                RunHistoryDirectoryProbeStatus probe =
                    _fileSystem.ProbeDirectory(_historyDirectoryPath);
                if (probe == RunHistoryDirectoryProbeStatus.Missing)
                    return RunHistoryReadAllResult.Succeeded(Array.Empty<RunSummary>());
                if (probe == RunHistoryDirectoryProbeStatus.NotDirectory)
                {
                    return RunHistoryReadAllResult.Failed(
                        RunHistoryReadAllStatus.IoFailure,
                        "Run history path is occupied by a non-directory object.");
                }

                string[] paths = _fileSystem.GetFiles(_historyDirectoryPath, "*.json")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                var summaries = new List<RunSummary>(paths.Length);
                var runIds = new HashSet<RunId>();
                foreach (string path in paths)
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    if (!Guid.TryParseExact(fileName, "D", out Guid runGuid) || runGuid == Guid.Empty)
                    {
                        return RunHistoryReadAllResult.Failed(
                            RunHistoryReadAllStatus.InvalidData,
                            $"Run history file '{Path.GetFileName(path)}' has a non-canonical name.");
                    }

                    var runId = new RunId(runGuid);
                    if (!string.Equals(fileName, runId.ToString(), StringComparison.Ordinal))
                    {
                        return RunHistoryReadAllResult.Failed(
                            RunHistoryReadAllStatus.InvalidData,
                            $"Run history file '{Path.GetFileName(path)}' is not canonical lowercase RunId text.");
                    }

                    RunHistoryLoadResult read = ReadSummaryFile(path, runId);
                    if (read.Status != RunHistoryLoadStatus.Success)
                        return MapReadAllFailure(read);
                    if (!runIds.Add(runId))
                    {
                        return RunHistoryReadAllResult.Failed(
                            RunHistoryReadAllStatus.InvalidData,
                            $"Run history contains duplicate Run id '{runId}'.");
                    }

                    summaries.Add(read.Summary);
                }

                return RunHistoryReadAllResult.Succeeded(summaries);
            }
            catch (DecoderFallbackException exception)
            {
                return RunHistoryReadAllResult.Failed(
                    RunHistoryReadAllStatus.InvalidData,
                    exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunHistoryReadAllResult.Failed(
                    RunHistoryReadAllStatus.IoFailure,
                    exception.Message);
            }
        }

        /// <summary>返回指定 RunId 的规范正式历史路径，供 adapter 测试核对。</summary>
        internal string GetSummaryPath(RunId runId)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run history id cannot be empty.", nameof(runId));

            return Path.Combine(_historyDirectoryPath, $"{runId}.json");
        }

        /// <summary>读取现存文件并只按完整内容返回幂等或冲突。</summary>
        private RunHistoryRecordResult CompareExisting(
            string summaryPath,
            RunSummary candidate)
        {
            RunHistoryLoadResult existing = ReadSummaryFile(summaryPath, candidate.RunId);
            if (existing.Status == RunHistoryLoadStatus.Success)
            {
                return existing.Summary.Equals(candidate)
                    ? RunHistoryRecordResult.AlreadyRecorded(candidate.RunId)
                    : RunHistoryRecordResult.Conflict(
                        candidate.RunId,
                        "A different immutable Run summary already exists.");
            }

            return RunHistoryRecordResult.Failed(
                existing.Status == RunHistoryLoadStatus.IoFailure
                    ? RunHistoryRecordStatus.IoFailure
                    : RunHistoryRecordStatus.InvalidData,
                candidate.RunId,
                existing.Detail);
        }

        /// <summary>严格解析一个文件并验证文件 RunId 与文档 RunId 一致。</summary>
        private RunHistoryLoadResult ReadSummaryFile(string path, RunId expectedRunId)
        {
            RunHistoryDocumentReadResult read = RunHistoryDocumentCodec.Read(
                _fileSystem.ReadAllText(path));
            switch (read.Status)
            {
                case RunHistoryDocumentReadStatus.Success:
                    return read.Summary.RunId == expectedRunId
                        ? RunHistoryLoadResult.Succeeded(read.Summary)
                        : RunHistoryLoadResult.Failed(
                            RunHistoryLoadStatus.InvalidData,
                            "Run history file name and document RunId differ.");
                case RunHistoryDocumentReadStatus.UnsupportedSchema:
                    return RunHistoryLoadResult.Failed(
                        RunHistoryLoadStatus.UnsupportedSchema,
                        read.Detail);
                case RunHistoryDocumentReadStatus.InvalidJson:
                case RunHistoryDocumentReadStatus.InvalidDocument:
                    return RunHistoryLoadResult.Failed(
                        RunHistoryLoadStatus.InvalidData,
                        read.Detail);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>把单文件读取故障映射为不发布部分集合的目录故障。</summary>
        private static RunHistoryReadAllResult MapReadAllFailure(RunHistoryLoadResult read)
        {
            switch (read.Status)
            {
                case RunHistoryLoadStatus.UnsupportedSchema:
                    return RunHistoryReadAllResult.Failed(
                        RunHistoryReadAllStatus.UnsupportedSchema,
                        read.Detail);
                case RunHistoryLoadStatus.IoFailure:
                    return RunHistoryReadAllResult.Failed(
                        RunHistoryReadAllStatus.IoFailure,
                        read.Detail);
                case RunHistoryLoadStatus.InvalidData:
                case RunHistoryLoadStatus.NotFound:
                    return RunHistoryReadAllResult.Failed(
                        RunHistoryReadAllStatus.InvalidData,
                        read.Detail);
                default:
                    throw new ArgumentOutOfRangeException(nameof(read));
            }
        }

        /// <summary>只清理当前调用生成的唯一临时文件，且不掩盖主结果。</summary>
        private void TryDeleteTemporary(string temporaryPath)
        {
            try
            {
                if (_fileSystem.FileExists(temporaryPath))
                    _fileSystem.DeleteFile(temporaryPath);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                // 临时文件不具业务权威性，清理失败留给后续诊断，不覆盖主提交结果。
            }
        }

        /// <summary>统一识别允许转换为类型化 I/O 失败的存储异常。</summary>
        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is NotSupportedException;
        }
    }
}
