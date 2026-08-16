using System;
using System.IO;
using NUnit.Framework;
using TinySpire.Infrastructure.Persistence;
using TinySpire.Run;

public sealed class AtomicJsonRunSaveStoreTests
{
    private string _testDirectory;

    /// <summary>为每个测试建立独立的系统临时目录，避免与玩家真实存档交叉。</summary>
    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TinySpire.AtomicJsonRunSaveStoreTests",
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

    /// <summary>真实临时目录中的单槽存档可完成提交、读取与幂等删除闭环。</summary>
    [Test]
    public void CommitLoadDelete_RealTemporaryDirectory_RoundTripsSingleSlot()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument document = CreateDocument(
            "11111111-aaaa-bbbb-cccc-222222222222",
            RunSaveNodeStatus.Available,
            currentHealth: 73,
            battleAttemptSequence: 0);

        RunSaveCommitResult commit = store.Commit(document);
        RunSaveLoadResult load = store.Load();
        RunSaveDeleteResult delete = store.Delete();
        RunSaveLoadResult afterDelete = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.RunId, Is.EqualTo(document.RunId));
        Assert.That(load.Document.CurrentHealth, Is.EqualTo(73));
        Assert.That(delete.Status, Is.EqualTo(RunSaveDeleteStatus.Success));
        Assert.That(afterDelete.Status, Is.EqualTo(RunSaveLoadStatus.NotFound));
    }

    /// <summary>真实文件系统的第二次提交必须通过 replace 发布 S1，并移除提交临时文件。</summary>
    [Test]
    public void Commit_SecondCheckpoint_RealTemporaryDirectory_ReplacesLiveFile()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument initial = CreateDocument(
            "10101010-aaaa-bbbb-cccc-202020202020",
            RunSaveNodeStatus.Available,
            currentHealth: 80,
            battleAttemptSequence: 0);
        RunSaveDocument completed = CreateDocument(
            "10101010-aaaa-bbbb-cccc-202020202020",
            RunSaveNodeStatus.Completed,
            currentHealth: 23,
            battleAttemptSequence: 1);

        Assert.That(store.Commit(initial).Status, Is.EqualTo(RunSaveCommitStatus.Success));

        RunSaveCommitResult replacement = store.Commit(completed);
        RunSaveLoadResult load = store.Load();

        Assert.That(replacement.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.NodeStatus, Is.EqualTo(RunSaveNodeStatus.Completed));
        Assert.That(load.Document.CurrentHealth, Is.EqualTo(23));
        Assert.That(load.Document.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(File.Exists(GetTemporaryPath()), Is.False);
    }

    /// <summary>首次 move 发布失败时必须保留完整临时档且不能产生正式档。</summary>
    [Test]
    public void Commit_FirstMoveFails_LeavesValidatedTemporaryFileWithoutLiveFile()
    {
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new MoveFailingFileSystem());
        RunSaveDocument document = CreateDocument(
            "30303030-aaaa-bbbb-cccc-404040404040",
            RunSaveNodeStatus.Available,
            currentHealth: 80,
            battleAttemptSequence: 0);

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetTemporaryPath())).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>已确认删除遇到 IO 故障时必须返回 typed failure 并保留正式档。</summary>
    [Test]
    public void Delete_FileSystemFails_ReturnsIoFailureAndPreservesLiveFile()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(
            initialStore.Commit(CreateDocument(
                "50505050-aaaa-bbbb-cccc-606060606060",
                RunSaveNodeStatus.Available,
                currentHealth: 80,
                battleAttemptSequence: 0)).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DeleteFailingFileSystem());

        RunSaveDeleteResult delete = failingStore.Delete();

        Assert.That(delete.Status, Is.EqualTo(RunSaveDeleteStatus.IoFailure));
        Assert.That(delete.Detail, Is.Not.Empty);
        Assert.That(File.Exists(GetLivePath()), Is.True);
    }

    /// <summary>已有有效正式档时替换失败必须报告 IO 故障，并逐字节保留旧档。</summary>
    [Test]
    public void Commit_ReplacementFails_PreservesByteIdenticalOldLiveFile()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldDocument = CreateDocument(
            "33333333-aaaa-bbbb-cccc-444444444444",
            RunSaveNodeStatus.Available,
            currentHealth: 80,
            battleAttemptSequence: 0);
        Assert.That(
            initialStore.Commit(oldDocument).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        string livePath = GetLivePath();
        byte[] oldBytes = File.ReadAllBytes(livePath);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());
        RunSaveDocument newDocument = CreateDocument(
            "55555555-aaaa-bbbb-cccc-666666666666",
            RunSaveNodeStatus.Completed,
            currentHealth: 29,
            battleAttemptSequence: 1);

        RunSaveCommitResult commit = failingStore.Commit(newDocument);
        byte[] currentBytes = File.ReadAllBytes(livePath);
        RunSaveLoadResult load = initialStore.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(currentBytes, Is.EqualTo(oldBytes));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.RunId, Is.EqualTo(oldDocument.RunId));
        Assert.That(load.HasPendingTemporaryFile, Is.True);
    }

    /// <summary>临时文件若在写入边界损坏，提交必须重读识别并在替换前拒绝它。</summary>
    [Test]
    public void Commit_TemporaryJsonIsInvalid_ValidatesBeforeReplacingLiveFile()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldDocument = CreateDocument(
            "77777777-aaaa-bbbb-cccc-888888888888",
            RunSaveNodeStatus.Available,
            currentHealth: 61,
            battleAttemptSequence: 0);
        Assert.That(
            initialStore.Commit(oldDocument).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        string livePath = GetLivePath();
        byte[] oldBytes = File.ReadAllBytes(livePath);
        var corruptingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new CorruptingTemporaryWriteFileSystem());
        RunSaveDocument newDocument = CreateDocument(
            "99999999-aaaa-bbbb-cccc-aaaaaaaaaaaa",
            RunSaveNodeStatus.Completed,
            currentHealth: 11,
            battleAttemptSequence: 1);

        RunSaveCommitResult commit = corruptingStore.Commit(newDocument);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.ReadAllBytes(livePath), Is.EqualTo(oldBytes));
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(File.ReadAllText(GetTemporaryPath()), Is.EqualTo("{"));
    }

    /// <summary>坏 JSON 与未知 schema 必须保持各自分类，且不得静默删除正式档。</summary>
    [TestCase("{", RunSaveLoadStatus.InvalidJson)]
    [TestCase("{\"schemaVersion\":999}", RunSaveLoadStatus.UnsupportedSchema)]
    public void Load_UnusableLiveFile_ClassifiesExplicitlyAndLeavesFileOnDisk(
        string json,
        RunSaveLoadStatus expectedStatus)
    {
        string livePath = GetLivePath();
        File.WriteAllText(livePath, json);
        var store = new AtomicJsonRunSaveStore(_testDirectory);

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(expectedStatus));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(File.Exists(livePath), Is.True);
        Assert.That(File.ReadAllText(livePath), Is.EqualTo(json));
    }

    /// <summary>非法 UTF-8 也是不可解析的 JSON，必须类型化失败并原样保留正式档。</summary>
    [Test]
    public void Load_InvalidUtf8LiveFile_ReturnsInvalidJsonAndLeavesBytesOnDisk()
    {
        string livePath = GetLivePath();
        byte[] invalidUtf8 = { 0xc3, 0x28 };
        File.WriteAllBytes(livePath, invalidUtf8);
        var store = new AtomicJsonRunSaveStore(_testDirectory);

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InvalidJson));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(File.ReadAllBytes(livePath), Is.EqualTo(invalidUtf8));
    }

    /// <summary>正式档读取失败时仍必须保留已经发现的残留临时档诊断事实。</summary>
    [Test]
    public void Load_LiveReadFailsWithTemporaryFile_PreservesTemporaryDiagnosticFlag()
    {
        File.WriteAllText(GetLivePath(), "{}");
        File.WriteAllText(GetTemporaryPath(), "{}");
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new LiveReadFailingFileSystem(GetLivePath()));

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.IoFailure));
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(File.Exists(GetLivePath()), Is.True);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
    }

    /// <summary>只有残留临时文件时必须显式报告中断提交，不能擅自晋升或删除。</summary>
    [Test]
    public void Load_TemporaryFileOnly_ReturnsInterruptedCommitWithoutMutation()
    {
        string temporaryPath = GetTemporaryPath();
        string json = RunSaveDocumentCodec.Serialize(CreateDocument(
            "bbbbbbbb-aaaa-bbbb-cccc-cccccccccccc",
            RunSaveNodeStatus.Available,
            currentHealth: 80,
            battleAttemptSequence: 0));
        File.WriteAllText(temporaryPath, json);
        var store = new AtomicJsonRunSaveStore(_testDirectory);

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InterruptedCommit));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.ReadAllText(temporaryPath), Is.EqualTo(json));
    }

    /// <summary>返回 Adapter 契约冻结的版本化正式文件路径。</summary>
    private string GetLivePath()
    {
        return Path.Combine(_testDirectory, AtomicJsonRunSaveStore.LiveFileName);
    }

    /// <summary>返回与正式文件同目录的提交临时文件路径。</summary>
    private string GetTemporaryPath()
    {
        return Path.Combine(_testDirectory, AtomicJsonRunSaveStore.TemporaryFileName);
    }

    /// <summary>建立字段稳定且互不共享的测试存档文档。</summary>
    private static RunSaveDocument CreateDocument(
        string runId,
        RunSaveNodeStatus nodeStatus,
        int currentHealth,
        int battleAttemptSequence)
    {
        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            runId,
            heroTemplateId: 1001,
            currentHealth,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 123456789u,
            nodeStatus,
            battleAttemptSequence);
    }

    private class TestRunSaveFileSystem : IRunSaveFileSystem
    {
        /// <summary>在测试目录中创建持久化目录。</summary>
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>查询指定测试文件是否存在。</summary>
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>按完整文本读取指定测试文件。</summary>
        public virtual string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        /// <summary>在测试边界写入完整文本；耐久刷新由生产实现的真实目录测试覆盖。</summary>
        public virtual void WriteAllTextDurably(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        /// <summary>以仅允许新目标的移动模拟首次提交。</summary>
        public virtual void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>以平台原子替换模拟已有正式档提交。</summary>
        public virtual void ReplaceFile(string sourcePath, string destinationPath)
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null);
        }

        /// <summary>删除测试边界内的指定文件。</summary>
        public virtual void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    private sealed class ReplaceFailingFileSystem : TestRunSaveFileSystem
    {
        /// <summary>稳定注入正式文件替换失败，证明旧档不会被覆盖。</summary>
        public override void ReplaceFile(string sourcePath, string destinationPath)
        {
            throw new IOException("Injected replace failure.");
        }
    }

    private sealed class MoveFailingFileSystem : TestRunSaveFileSystem
    {
        /// <summary>稳定注入首次正式发布失败。</summary>
        public override void MoveFile(string sourcePath, string destinationPath)
        {
            throw new IOException("Injected move failure.");
        }
    }

    private sealed class DeleteFailingFileSystem : TestRunSaveFileSystem
    {
        /// <summary>稳定注入已确认删除失败，并保留目标文件。</summary>
        public override void DeleteFile(string path)
        {
            throw new IOException("Injected delete failure.");
        }
    }

    private sealed class CorruptingTemporaryWriteFileSystem : TestRunSaveFileSystem
    {
        /// <summary>忽略待写正文并落下坏 JSON，迫使 Adapter 重读临时文件。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            File.WriteAllText(path, "{");
        }
    }

    private sealed class LiveReadFailingFileSystem : TestRunSaveFileSystem
    {
        private readonly string _livePath;

        /// <summary>建立只在读取指定正式档时故障的文件系统 fake。</summary>
        public LiveReadFailingFileSystem(string livePath)
        {
            _livePath = livePath;
        }

        /// <summary>正式档读取抛出 IO 故障，临时档仍保持可发现。</summary>
        public override string ReadAllText(string path)
        {
            if (path == _livePath)
                throw new IOException("Injected live read failure.");

            return base.ReadAllText(path);
        }
    }
}
