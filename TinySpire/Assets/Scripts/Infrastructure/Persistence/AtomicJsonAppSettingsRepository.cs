using System;
using System.IO;
using System.Security;
using System.Text;
using TinySpire.Settings;

namespace TinySpire.Infrastructure.Persistence
{
    /// <summary>为应用设置原子提交隔离可故障注入的文件系统边界。</summary>
    internal interface IAppSettingsFileSystem
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
    }

    /// <summary>使用 System.IO 实现 Editor 与 Windows Standalone 的应用设置存储边界。</summary>
    internal sealed class PhysicalAtomicAppSettingsFileSystem : IAppSettingsFileSystem
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>查询真实文件是否存在，并只把明确缺失视为 false。</summary>
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

        /// <summary>以拒绝非法字节的 UTF-8 编码读取真实文件。</summary>
        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, StrictUtf8);
        }

        /// <summary>独占写入临时文件并请求把完整内容刷新到持久介质。</summary>
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

        /// <summary>把首次提交的同目录临时文件移动为正式文件。</summary>
        public void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>用系统原子替换语义覆盖已有正式文件，且不提供非原子降级。</summary>
        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null);
        }
    }

    /// <summary>在独立 app-settings.json 中原子持久化完整应用设置。</summary>
    public sealed class AtomicJsonAppSettingsRepository : IAppSettingsRepository
    {
        internal const string LiveFileName = "app-settings.json";
        internal const string TemporaryFileName = "app-settings.json.tmp";

        private readonly string _directoryPath;
        private readonly string _settingsFilePath;
        private readonly string _temporaryFilePath;
        private readonly IAppSettingsFileSystem _fileSystem;

        /// <summary>在指定目录建立真实文件系统应用设置 Adapter。</summary>
        public AtomicJsonAppSettingsRepository(string directoryPath)
            : this(directoryPath, new PhysicalAtomicAppSettingsFileSystem())
        {
        }

        /// <summary>以可控文件系统边界建立 Adapter，供原子失败路径验证。</summary>
        internal AtomicJsonAppSettingsRepository(
            string directoryPath,
            IAppSettingsFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException(
                    "App settings directory is required.",
                    nameof(directoryPath));
            }

            _directoryPath = Path.GetFullPath(directoryPath);
            _settingsFilePath = Path.Combine(_directoryPath, LiveFileName);
            _temporaryFilePath = Path.Combine(_directoryPath, TemporaryFileName);
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>读取最近成功发布的完整设置，并区分缺失、坏数据与存储故障。</summary>
        public AppSettingsRepositoryLoadResult Load()
        {
            try
            {
                if (!_fileSystem.FileExists(_settingsFilePath))
                    return AppSettingsRepositoryLoadResult.NotFound();

                AppSettingsDocumentReadResult read = AppSettingsDocumentCodec.Read(
                    _fileSystem.ReadAllText(_settingsFilePath));
                if (read.Status == AppSettingsDocumentReadStatus.Success)
                    return AppSettingsRepositoryLoadResult.Succeeded(read.Settings);

                return AppSettingsRepositoryLoadResult.InvalidData(
                    read.Detail.Length > 0
                        ? read.Detail
                        : $"App settings document failed with status {read.Status}.");
            }
            catch (DecoderFallbackException exception)
            {
                return AppSettingsRepositoryLoadResult.InvalidData(exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return AppSettingsRepositoryLoadResult.IoFailure(exception.Message);
            }
        }

        /// <summary>先严格回读同目录临时文件，再以 move 或 replace 原子发布完整设置。</summary>
        public AppSettingsRepositoryCommitResult Commit(AppSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string serialized = AppSettingsDocumentCodec.Write(settings);
            AppSettingsDocumentReadResult sourceRead = AppSettingsDocumentCodec.Read(serialized);
            if (sourceRead.Status != AppSettingsDocumentReadStatus.Success ||
                !settings.Equals(sourceRead.Settings))
            {
                return AppSettingsRepositoryCommitResult.IoFailure(
                    sourceRead.Detail.Length > 0
                        ? sourceRead.Detail
                        : "App settings failed pre-write validation.");
            }

            try
            {
                _fileSystem.CreateDirectory(_directoryPath);
                _fileSystem.WriteAllTextDurably(_temporaryFilePath, serialized);

                AppSettingsDocumentReadResult temporaryRead = AppSettingsDocumentCodec.Read(
                    _fileSystem.ReadAllText(_temporaryFilePath));
                if (temporaryRead.Status != AppSettingsDocumentReadStatus.Success ||
                    !settings.Equals(temporaryRead.Settings))
                {
                    return AppSettingsRepositoryCommitResult.IoFailure(
                        temporaryRead.Detail.Length > 0
                            ? temporaryRead.Detail
                            : "The temporary app settings did not match the requested snapshot.");
                }

                if (_fileSystem.FileExists(_settingsFilePath))
                    _fileSystem.ReplaceFile(_temporaryFilePath, _settingsFilePath);
                else
                    _fileSystem.MoveFile(_temporaryFilePath, _settingsFilePath);

                return AppSettingsRepositoryCommitResult.Succeeded();
            }
            catch (DecoderFallbackException exception)
            {
                return AppSettingsRepositoryCommitResult.IoFailure(exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return AppSettingsRepositoryCommitResult.IoFailure(exception.Message);
            }
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
