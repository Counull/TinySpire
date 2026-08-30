using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Infrastructure.Persistence;
using TinySpire.Settings;

public sealed class AtomicJsonAppSettingsRepositoryG8Tests
{
    private string _testDirectory;

    /// <summary>坏设置文档的三个独立来源。</summary>
    public enum InvalidDocumentKind
    {
        MalformedJson,
        UnsupportedSchema,
        InvalidField,
    }

    /// <summary>为每个测试建立独立的系统临时目录，避免触碰玩家真实设置。</summary>
    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TinySpire.AtomicJsonAppSettingsRepositoryG8Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>只清理当前测试明确创建的临时目录。</summary>
    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(_testDirectory) && Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    /// <summary>真实目录中的首次 move 与后续 replace 都可恢复最近设置且不改 Run save。</summary>
    [Test]
    public void CommitLoad_RealTemporaryDirectory_RoundTripsDedicatedSettingsFile()
    {
        string runSavePath = Path.Combine(_testDirectory, "run-save.json");
        File.WriteAllText(runSavePath, "run-save-sentinel");
        var repository = new AtomicJsonAppSettingsRepository(_testDirectory);
        AppSettingsSnapshot first = CreateSettings(masterVolumePercent: 80);
        AppSettingsSnapshot second = CreateSettings(masterVolumePercent: 35);

        AppSettingsRepositoryCommitResult firstCommit = repository.Commit(first);
        AppSettingsRepositoryCommitResult secondCommit = repository.Commit(second);
        AppSettingsRepositoryLoadResult load = repository.Load();

        Assert.That(firstCommit.Status, Is.EqualTo(AppSettingsRepositoryCommitStatus.Success));
        Assert.That(secondCommit.Status, Is.EqualTo(AppSettingsRepositoryCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(AppSettingsRepositoryLoadStatus.Success));
        Assert.That(load.Settings, Is.EqualTo(second));
        Assert.That(
            File.Exists(Path.Combine(
                _testDirectory,
                AtomicJsonAppSettingsRepository.LiveFileName)),
            Is.True);
        Assert.That(
            File.Exists(Path.Combine(
                _testDirectory,
                AtomicJsonAppSettingsRepository.TemporaryFileName)),
            Is.False);
        Assert.That(File.ReadAllText(runSavePath), Is.EqualTo("run-save-sentinel"));
    }

    /// <summary>没有正式设置文件时必须返回 NotFound，残留临时文件不能被当成已提交设置。</summary>
    [Test]
    public void Load_MissingLiveWithResidualTemporaryFile_ReturnsNotFound()
    {
        File.WriteAllText(
            Path.Combine(
                _testDirectory,
                AtomicJsonAppSettingsRepository.TemporaryFileName),
            AppSettingsDocumentCodec.Write(CreateSettings()));
        var repository = new AtomicJsonAppSettingsRepository(_testDirectory);

        AppSettingsRepositoryLoadResult load = repository.Load();

        Assert.That(load.Status, Is.EqualTo(AppSettingsRepositoryLoadStatus.NotFound));
        Assert.That(load.Settings, Is.Null);
    }

    /// <summary>畸形 JSON、未知 schema 与非法字段都必须成为坏数据而非半合法设置。</summary>
    [TestCase(InvalidDocumentKind.MalformedJson)]
    [TestCase(InvalidDocumentKind.UnsupportedSchema)]
    [TestCase(InvalidDocumentKind.InvalidField)]
    public void Load_InvalidDocument_ReturnsTypedInvalidData(InvalidDocumentKind kind)
    {
        string livePath = Path.Combine(
            _testDirectory,
            AtomicJsonAppSettingsRepository.LiveFileName);
        File.WriteAllText(livePath, CreateInvalidDocument(kind));
        var repository = new AtomicJsonAppSettingsRepository(_testDirectory);

        AppSettingsRepositoryLoadResult load = repository.Load();

        Assert.That(load.Status, Is.EqualTo(AppSettingsRepositoryLoadStatus.InvalidData));
        Assert.That(load.Settings, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
    }

    /// <summary>非法 UTF-8 字节必须归类为坏数据，而不是泄漏解码异常。</summary>
    [Test]
    public void Load_InvalidUtf8_ReturnsTypedInvalidData()
    {
        string livePath = Path.Combine(
            _testDirectory,
            AtomicJsonAppSettingsRepository.LiveFileName);
        File.WriteAllBytes(livePath, new byte[] { 0x7B, 0xFF, 0x7D });
        var repository = new AtomicJsonAppSettingsRepository(_testDirectory);

        AppSettingsRepositoryLoadResult load = repository.Load();

        Assert.That(load.Status, Is.EqualTo(AppSettingsRepositoryLoadStatus.InvalidData));
        Assert.That(load.Settings, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
    }

    /// <summary>正式文件读取 I/O 故障必须返回类型化失败。</summary>
    [Test]
    public void Load_ReadIoFailure_ReturnsTypedIoFailure()
    {
        var fileSystem = new ScriptedAppSettingsFileSystem(_testDirectory);
        fileSystem.SeedLive(AppSettingsDocumentCodec.Write(CreateSettings()));
        fileSystem.ThrowOnRead = true;
        var repository = new AtomicJsonAppSettingsRepository(_testDirectory, fileSystem);

        AppSettingsRepositoryLoadResult load = repository.Load();

        Assert.That(load.Status, Is.EqualTo(AppSettingsRepositoryLoadStatus.IoFailure));
        Assert.That(load.Settings, Is.Null);
        Assert.That(load.Detail, Does.Contain("Injected read failure"));
    }

    /// <summary>临时文件回读不一致时不得调用 move 或 replace，也不得发布候选设置。</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Commit_CorruptedTemporaryReadBack_NeverPublishes(bool hasExistingLive)
    {
        AppSettingsSnapshot oldSettings = CreateSettings(masterVolumePercent: 80);
        AppSettingsSnapshot candidate = CreateSettings(masterVolumePercent: 25);
        var fileSystem = new ScriptedAppSettingsFileSystem(_testDirectory)
        {
            CorruptTemporaryReads = true,
        };
        if (hasExistingLive)
            fileSystem.SeedLive(AppSettingsDocumentCodec.Write(oldSettings));
        var repository = new AtomicJsonAppSettingsRepository(_testDirectory, fileSystem);

        AppSettingsRepositoryCommitResult commit = repository.Commit(candidate);

        Assert.That(commit.Status, Is.EqualTo(AppSettingsRepositoryCommitStatus.IoFailure));
        Assert.That(fileSystem.MoveCalls, Is.Zero);
        Assert.That(fileSystem.ReplaceCalls, Is.Zero);
        Assert.That(fileSystem.HasLive, Is.EqualTo(hasExistingLive));
        if (hasExistingLive)
        {
            Assert.That(
                AppSettingsDocumentCodec.Read(fileSystem.LiveContent).Settings,
                Is.EqualTo(oldSettings));
        }
    }

    /// <summary>首次 move 与后续 replace 的 I/O 失败都必须保持正式设置未发布。</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Commit_PublishIoFailure_ReturnsTypedFailureAndPreservesLive(bool hasExistingLive)
    {
        AppSettingsSnapshot oldSettings = CreateSettings(masterVolumePercent: 80);
        AppSettingsSnapshot candidate = CreateSettings(masterVolumePercent: 10);
        var fileSystem = new ScriptedAppSettingsFileSystem(_testDirectory)
        {
            ThrowOnMove = !hasExistingLive,
            ThrowOnReplace = hasExistingLive,
        };
        if (hasExistingLive)
            fileSystem.SeedLive(AppSettingsDocumentCodec.Write(oldSettings));
        var repository = new AtomicJsonAppSettingsRepository(_testDirectory, fileSystem);

        AppSettingsRepositoryCommitResult commit = repository.Commit(candidate);

        Assert.That(commit.Status, Is.EqualTo(AppSettingsRepositoryCommitStatus.IoFailure));
        Assert.That(fileSystem.HasLive, Is.EqualTo(hasExistingLive));
        if (hasExistingLive)
        {
            Assert.That(
                AppSettingsDocumentCodec.Read(fileSystem.LiveContent).Settings,
                Is.EqualTo(oldSettings));
        }
    }

    /// <summary>创建一份合法、可区分音量的完整设置快照。</summary>
    private static AppSettingsSnapshot CreateSettings(int masterVolumePercent = 65)
    {
        return new AppSettingsSnapshot(
            localeCode: AppSettingsSnapshot.SimplifiedChineseLocaleCode,
            masterVolumePercent: masterVolumePercent,
            displayMode: AppDisplayMode.Windowed,
            resolution: new AppResolution(width: 1920, height: 1080),
            textScale: AppTextScale.Percent125,
            highContrast: true,
            reducedMotion: true);
    }

    /// <summary>按分类创建 codec 必须完整拒绝的坏文档。</summary>
    private static string CreateInvalidDocument(InvalidDocumentKind kind)
    {
        if (kind == InvalidDocumentKind.MalformedJson)
            return "{";

        JObject document = JObject.Parse(AppSettingsDocumentCodec.Write(CreateSettings()));
        switch (kind)
        {
            case InvalidDocumentKind.UnsupportedSchema:
                document["schemaVersion"] = AppSettingsDocumentCodec.CurrentSchemaVersion + 1;
                break;
            case InvalidDocumentKind.InvalidField:
                document["masterVolumePercent"] = 101;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return document.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>以内存文件和故障开关稳定验证原子发布顺序。</summary>
    private sealed class ScriptedAppSettingsFileSystem : IAppSettingsFileSystem
    {
        private readonly Dictionary<string, string> _files =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly string _livePath;
        private readonly string _temporaryPath;

        /// <summary>是否让任意读取抛出 I/O 异常。</summary>
        public bool ThrowOnRead { get; set; }

        /// <summary>是否把临时文件回读篡改为坏 JSON。</summary>
        public bool CorruptTemporaryReads { get; set; }

        /// <summary>是否让首次正式移动失败。</summary>
        public bool ThrowOnMove { get; set; }

        /// <summary>是否让正式替换失败。</summary>
        public bool ThrowOnReplace { get; set; }

        /// <summary>记录正式移动调用次数。</summary>
        public int MoveCalls { get; private set; }

        /// <summary>记录正式替换调用次数。</summary>
        public int ReplaceCalls { get; private set; }

        /// <summary>指出正式设置是否已经存在。</summary>
        public bool HasLive => _files.ContainsKey(_livePath);

        /// <summary>返回当前正式文件文本。</summary>
        public string LiveContent => _files[_livePath];

        /// <summary>绑定 repository 将使用的完整文件路径。</summary>
        public ScriptedAppSettingsFileSystem(string directoryPath)
        {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            _livePath = Path.Combine(
                fullDirectoryPath,
                AtomicJsonAppSettingsRepository.LiveFileName);
            _temporaryPath = Path.Combine(
                fullDirectoryPath,
                AtomicJsonAppSettingsRepository.TemporaryFileName);
        }

        /// <summary>预置一份已发布正式文件。</summary>
        public void SeedLive(string content)
        {
            _files[_livePath] = content;
        }

        /// <summary>查询内存文件是否存在。</summary>
        public bool FileExists(string path)
        {
            return _files.ContainsKey(path);
        }

        /// <summary>内存边界无需创建真实目录。</summary>
        public void CreateDirectory(string path)
        {
        }

        /// <summary>读取内存文件，并按测试开关注入读取故障或临时损坏。</summary>
        public string ReadAllText(string path)
        {
            if (ThrowOnRead)
                throw new IOException("Injected read failure.");
            if (CorruptTemporaryReads && string.Equals(path, _temporaryPath, StringComparison.Ordinal))
                return "{";

            return _files[path];
        }

        /// <summary>把完整文本写入内存临时文件。</summary>
        public void WriteAllTextDurably(string path, string content)
        {
            _files[path] = content;
        }

        /// <summary>模拟首次移动，并在开关启用时稳定抛出 I/O 异常。</summary>
        public void MoveFile(string sourcePath, string destinationPath)
        {
            MoveCalls++;
            if (ThrowOnMove)
                throw new IOException("Injected move failure.");

            _files[destinationPath] = _files[sourcePath];
            _files.Remove(sourcePath);
        }

        /// <summary>模拟已有文件替换，并在开关启用时稳定抛出 I/O 异常。</summary>
        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            ReplaceCalls++;
            if (ThrowOnReplace)
                throw new IOException("Injected replace failure.");

            _files[destinationPath] = _files[sourcePath];
            _files.Remove(sourcePath);
        }
    }
}
