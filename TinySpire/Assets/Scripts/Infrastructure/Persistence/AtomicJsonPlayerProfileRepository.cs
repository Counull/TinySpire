using System;
using System.IO;
using System.Security;
using System.Text;
using TinySpire.Profile;

namespace TinySpire.Infrastructure.Persistence
{
    /// <summary>为 player-profile.json 隔离可故障注入的文件系统边界。</summary>
    internal interface IPlayerProfileFileSystem
    {
        /// <summary>判断指定文件是否存在。</summary>
        bool FileExists(string path);

        /// <summary>确保目标目录存在。</summary>
        void CreateDirectory(string path);

        /// <summary>以严格 UTF-8 读取完整文本。</summary>
        string ReadAllText(string path);

        /// <summary>把完整文本持久刷新到指定临时文件。</summary>
        void WriteAllTextDurably(string path, string content);

        /// <summary>在正式文件不存在时以同卷移动完成首次提交。</summary>
        void MoveFile(string sourcePath, string destinationPath);

        /// <summary>在正式文件存在时以平台原子替换完成提交。</summary>
        void ReplaceFile(string sourcePath, string destinationPath);

        /// <summary>删除指定临时文件。</summary>
        void DeleteFile(string path);
    }

    /// <summary>使用 System.IO 实现当前 Editor/Windows Standalone 的 Profile 存储边界。</summary>
    internal sealed class PhysicalAtomicPlayerProfileFileSystem : IPlayerProfileFileSystem
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

        /// <summary>用系统原子替换语义覆盖已有正式文件。</summary>
        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null);
        }

        /// <summary>删除真实临时文件。</summary>
        public void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    /// <summary>以临时文件回读校验加原子替换实现独立 Player Profile repository。</summary>
    public sealed class AtomicJsonPlayerProfileRepository : IPlayerProfileRepository
    {
        internal const string LiveFileName = "player-profile.json";
        internal const string TemporaryFileName = "player-profile.json.tmp";

        private readonly string _directoryPath;
        private readonly string _liveFilePath;
        private readonly string _temporaryFilePath;
        private readonly IPlayerProfileFileSystem _fileSystem;

        /// <summary>在指定目录建立真实文件系统 Profile adapter。</summary>
        public AtomicJsonPlayerProfileRepository(string directoryPath)
            : this(directoryPath, new PhysicalAtomicPlayerProfileFileSystem())
        {
        }

        /// <summary>以可控文件系统边界建立 Profile adapter，供故障路径验证。</summary>
        internal AtomicJsonPlayerProfileRepository(
            string directoryPath,
            IPlayerProfileFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Player Profile directory is required.", nameof(directoryPath));

            _directoryPath = Path.GetFullPath(directoryPath);
            _liveFilePath = Path.Combine(_directoryPath, LiveFileName);
            _temporaryFilePath = Path.Combine(_directoryPath, TemporaryFileName);
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>只读取最近原子提交的正式文件，绝不把未提交临时文件提升为 Profile。</summary>
        public PlayerProfileRepositoryLoadResult Load()
        {
            try
            {
                bool hasLiveFile = _fileSystem.FileExists(_liveFilePath);
                bool hasTemporaryFile = _fileSystem.FileExists(_temporaryFilePath);
                if (!hasLiveFile)
                {
                    return hasTemporaryFile
                        ? PlayerProfileRepositoryLoadResult.InterruptedCommit(
                            "Player Profile has an uncommitted temporary file but no stable document.")
                        : PlayerProfileRepositoryLoadResult.NotFound();
                }

                string json = _fileSystem.ReadAllText(_liveFilePath);
                PlayerProfileDocumentReadResult read = PlayerProfileDocumentCodec.Read(json);
                if (read.Status != PlayerProfileDocumentReadStatus.Success || read.Profile == null)
                {
                    return PlayerProfileRepositoryLoadResult.InvalidData(
                        read.Detail.Length > 0
                            ? read.Detail
                            : $"Player Profile document status was {read.Status}.");
                }

                return PlayerProfileRepositoryLoadResult.Succeeded(read.Profile);
            }
            catch (DecoderFallbackException exception)
            {
                return PlayerProfileRepositoryLoadResult.InvalidData(exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return PlayerProfileRepositoryLoadResult.IoFailure(exception.Message);
            }
        }

        /// <summary>先持久写入并回读完整临时文件，再以同卷移动或原子替换提交。</summary>
        public PlayerProfileRepositoryCommitResult Commit(PlayerProfileSnapshot profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            try
            {
                _fileSystem.CreateDirectory(_directoryPath);
                if (_fileSystem.FileExists(_temporaryFilePath))
                    _fileSystem.DeleteFile(_temporaryFilePath);

                string json = PlayerProfileDocumentCodec.Write(profile);
                _fileSystem.WriteAllTextDurably(_temporaryFilePath, json);

                PlayerProfileDocumentReadResult verification =
                    PlayerProfileDocumentCodec.Read(_fileSystem.ReadAllText(_temporaryFilePath));
                if (verification.Status != PlayerProfileDocumentReadStatus.Success ||
                    verification.Profile == null ||
                    !verification.Profile.Equals(profile))
                {
                    BestEffortDeleteTemporary();
                    return PlayerProfileRepositoryCommitResult.VerificationFailure(
                        verification.Detail.Length > 0
                            ? verification.Detail
                            : "Player Profile temporary verification did not match the candidate.");
                }

                if (_fileSystem.FileExists(_liveFilePath))
                    _fileSystem.ReplaceFile(_temporaryFilePath, _liveFilePath);
                else
                    _fileSystem.MoveFile(_temporaryFilePath, _liveFilePath);

                return PlayerProfileRepositoryCommitResult.Succeeded();
            }
            catch (DecoderFallbackException exception)
            {
                BestEffortDeleteTemporary();
                return PlayerProfileRepositoryCommitResult.VerificationFailure(exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                BestEffortDeleteTemporary();
                return PlayerProfileRepositoryCommitResult.IoFailure(exception.Message);
            }
        }

        /// <summary>失败路径尽力清理未提交临时文件，清理失败不覆盖首个失败原因。</summary>
        private void BestEffortDeleteTemporary()
        {
            try
            {
                if (_fileSystem.FileExists(_temporaryFilePath))
                    _fileSystem.DeleteFile(_temporaryFilePath);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                // 首个提交失败仍是调用方需要处理的权威结果。
            }
        }

        /// <summary>只把可预期的本地存储异常转换为 typed failure。</summary>
        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is NotSupportedException;
        }
    }
}
