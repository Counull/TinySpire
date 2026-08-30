using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Infrastructure.Persistence;
using TinySpire.Profile;

public sealed class TutorialProfileG8Tests
{
    private string _testDirectory;

    /// <summary>为每个物理 repository 测试建立独立临时目录。</summary>
    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TinySpire.TutorialProfileG8Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>清理当前测试独占的临时目录。</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    /// <summary>首轮教程目录必须严格覆盖完整 Run 的七个上下文并保持冻结顺序。</summary>
    [Test]
    public void Catalog_UsesFrozenCompleteRunOrder()
    {
        Assert.That(
            TutorialPromptCatalog.Ordered.Select(definition => definition.Id),
            Is.EqualTo(new[]
            {
                TutorialPromptId.MainMenuWelcome,
                TutorialPromptId.HeroSelection,
                TutorialPromptId.MapRoute,
                TutorialPromptId.BattleBasics,
                TutorialPromptId.CardReward,
                TutorialPromptId.NonCombatNode,
                TutorialPromptId.RunOutcome,
            }));
        Assert.That(
            TutorialPromptCatalog.Ordered.Select(definition => definition.Context),
            Is.EqualTo(new[]
            {
                TutorialContext.MainMenu,
                TutorialContext.HeroSelection,
                TutorialContext.ActMap,
                TutorialContext.Battle,
                TutorialContext.CardReward,
                TutorialContext.NonCombatNode,
                TutorialContext.RunOutcome,
            }));
    }

    /// <summary>schema v1 必须只往返教程事实，不得混入设置、Run 或历史字段。</summary>
    [Test]
    public void DocumentCodec_RoundTripsTutorialOnlySchemaV1()
    {
        PlayerProfileSnapshot expected = CreateProfile(acknowledgedCount: 3);

        string json = PlayerProfileDocumentCodec.Write(expected);
        JObject document = JObject.Parse(json);
        PlayerProfileDocumentReadResult read = PlayerProfileDocumentCodec.Read(json);

        Assert.That(document.Properties().Select(property => property.Name),
            Is.EqualTo(new[] { "schemaVersion", "tutorial" }));
        Assert.That(document["settings"], Is.Null);
        Assert.That(document["runHistory"], Is.Null);
        Assert.That(document["runState"], Is.Null);
        Assert.That(read.Status, Is.EqualTo(PlayerProfileDocumentReadStatus.Success));
        Assert.That(read.Profile, Is.EqualTo(expected));
    }

    /// <summary>未知 schema、越权字段与乱序确认前缀都必须整档拒绝。</summary>
    [Test]
    public void DocumentCodec_RejectsUnknownSchemaExtraFieldsAndOutOfOrderProgress()
    {
        string unknownSchema =
            "{\"schemaVersion\":2,\"tutorial\":{\"skipped\":false," +
            "\"acknowledgedPromptIds\":[]}}";
        string extraSettings =
            "{\"schemaVersion\":1,\"tutorial\":{\"skipped\":false," +
            "\"acknowledgedPromptIds\":[]},\"settings\":{}}";
        string outOfOrder =
            "{\"schemaVersion\":1,\"tutorial\":{\"skipped\":false," +
            "\"acknowledgedPromptIds\":[\"hero-selection\"]}}";

        Assert.That(PlayerProfileDocumentCodec.Read(unknownSchema).Status,
            Is.EqualTo(PlayerProfileDocumentReadStatus.UnsupportedSchema));
        Assert.That(PlayerProfileDocumentCodec.Read(extraSettings).Status,
            Is.EqualTo(PlayerProfileDocumentReadStatus.InvalidDocument));
        Assert.That(PlayerProfileDocumentCodec.Read(outOfOrder).Status,
            Is.EqualTo(PlayerProfileDocumentReadStatus.InvalidDocument));
        Assert.That(PlayerProfileDocumentCodec.Read("{broken").Status,
            Is.EqualTo(PlayerProfileDocumentReadStatus.InvalidJson));
    }

    /// <summary>新 Profile 只在当前上下文给出首步骤，乱序确认零写入。</summary>
    [Test]
    public void StateStore_NewProfileCollectsCurrentContextAndRejectsOutOfOrderAck()
    {
        var repository = new RecordingRepository(PlayerProfileRepositoryLoadResult.NotFound());
        var store = new PlayerProfileStateStore(repository);

        PlayerProfileInitializationStatus initialization = store.Initialize();
        TutorialProfileActionStatus outOfOrder =
            store.Acknowledge(TutorialPromptId.BattleBasics);

        Assert.That(initialization, Is.EqualTo(PlayerProfileInitializationStatus.NewProfile));
        Assert.That(store.GetPendingPrompts(TutorialContext.MainMenu)
            .Select(prompt => prompt.Id),
            Is.EqualTo(new[] { TutorialPromptId.MainMenuWelcome }));
        Assert.That(store.GetPendingPrompts(TutorialContext.Battle), Is.Empty);
        Assert.That(outOfOrder, Is.EqualTo(TutorialProfileActionStatus.NotCurrentPrompt));
        Assert.That(repository.CommitAttempts, Is.Empty);
    }

    /// <summary>确认必须先提交再发布，重复确认返回幂等结果且不再次写盘。</summary>
    [Test]
    public void StateStore_AcknowledgeCommitsBeforePublishAndIsIdempotent()
    {
        var repository = new RecordingRepository(PlayerProfileRepositoryLoadResult.NotFound());
        var store = new PlayerProfileStateStore(repository);
        var published = new List<PlayerProfileSnapshot>();
        store.Changed += published.Add;
        store.Initialize();

        TutorialProfileActionStatus first =
            store.Acknowledge(TutorialPromptId.MainMenuWelcome);
        TutorialProfileActionStatus duplicate =
            store.Acknowledge(TutorialPromptId.MainMenuWelcome);

        Assert.That(first, Is.EqualTo(TutorialProfileActionStatus.Success));
        Assert.That(duplicate,
            Is.EqualTo(TutorialProfileActionStatus.AlreadyAcknowledged));
        Assert.That(repository.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(published, Has.Count.EqualTo(1));
        Assert.That(store.Current, Is.SameAs(repository.CommitAttempts[0]));
        Assert.That(store.GetPendingPrompts(TutorialContext.HeroSelection)
            .Select(prompt => prompt.Id),
            Is.EqualTo(new[] { TutorialPromptId.HeroSelection }));
    }

    /// <summary>冷启动必须从最近耐久确认之后的上下文继续，而不是重放已确认步骤。</summary>
    [Test]
    public void StateStore_ColdLoadContinuesAtNextDurableContext()
    {
        PlayerProfileSnapshot durable = CreateProfile(acknowledgedCount: 3);
        var store = new PlayerProfileStateStore(
            new RecordingRepository(PlayerProfileRepositoryLoadResult.Succeeded(durable)));

        PlayerProfileInitializationStatus status = store.Initialize();

        Assert.That(status, Is.EqualTo(PlayerProfileInitializationStatus.Loaded));
        Assert.That(store.GetPendingPrompts(TutorialContext.MainMenu), Is.Empty);
        Assert.That(store.GetPendingPrompts(TutorialContext.Battle)
            .Select(prompt => prompt.Id),
            Is.EqualTo(new[] { TutorialPromptId.BattleBasics }));
    }

    /// <summary>提交失败必须保持旧进度、零发布，并在本次会话抑制阻挡层。</summary>
    [Test]
    public void StateStore_CommitFailurePreservesStableStateAndFailsOpen()
    {
        PlayerProfileSnapshot durable = CreateProfile(acknowledgedCount: 1);
        var repository = new RecordingRepository(
            PlayerProfileRepositoryLoadResult.Succeeded(durable));
        repository.EnqueueCommitResult(
            PlayerProfileRepositoryCommitResult.IoFailure("injected"));
        var store = new PlayerProfileStateStore(repository);
        var published = new List<PlayerProfileSnapshot>();
        store.Changed += published.Add;
        store.Initialize();

        TutorialProfileActionStatus result =
            store.Acknowledge(TutorialPromptId.HeroSelection);

        Assert.That(result, Is.EqualTo(TutorialProfileActionStatus.SaveFailed));
        Assert.That(store.Current, Is.SameAs(durable));
        Assert.That(store.IsTutorialSuppressed, Is.True);
        Assert.That(store.GetPendingPrompts(TutorialContext.HeroSelection), Is.Empty);
        Assert.That(published, Is.Empty);
    }

    /// <summary>跳过与重置都必须耐久；重复动作不得产生第二次相同提交。</summary>
    [Test]
    public void StateStore_SkipAndResetAreDurableAndIdempotent()
    {
        var repository = new RecordingRepository(PlayerProfileRepositoryLoadResult.NotFound());
        var store = new PlayerProfileStateStore(repository);
        store.Initialize();

        TutorialProfileActionStatus skip = store.SkipTutorial();
        TutorialProfileActionStatus duplicateSkip = store.SkipTutorial();
        TutorialProfileActionStatus reset = store.ResetTutorial();
        TutorialProfileActionStatus duplicateReset = store.ResetTutorial();

        Assert.That(skip, Is.EqualTo(TutorialProfileActionStatus.Success));
        Assert.That(duplicateSkip, Is.EqualTo(TutorialProfileActionStatus.Unchanged));
        Assert.That(reset, Is.EqualTo(TutorialProfileActionStatus.Success));
        Assert.That(duplicateReset, Is.EqualTo(TutorialProfileActionStatus.Unchanged));
        Assert.That(repository.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(repository.CommitAttempts[0].TutorialSkipped, Is.True);
        Assert.That(repository.CommitAttempts[1], Is.EqualTo(PlayerProfileSnapshot.CreateNew()));
        Assert.That(store.GetPendingPrompts(TutorialContext.MainMenu), Has.Count.EqualTo(1));
    }

    /// <summary>坏 Profile 必须 fail-open；显式重置成功后才能重新发布首步骤。</summary>
    [Test]
    public void StateStore_InvalidLoadFailsOpenAndResetRepairsProfile()
    {
        var repository = new RecordingRepository(
            PlayerProfileRepositoryLoadResult.InvalidData("broken"));
        var store = new PlayerProfileStateStore(repository);

        PlayerProfileInitializationStatus initialization = store.Initialize();
        IReadOnlyList<TutorialPromptDefinition> suppressed =
            store.GetPendingPrompts(TutorialContext.MainMenu);
        TutorialProfileActionStatus reset = store.ResetTutorial();

        Assert.That(initialization,
            Is.EqualTo(PlayerProfileInitializationStatus.SuppressedInvalidData));
        Assert.That(suppressed, Is.Empty);
        Assert.That(reset, Is.EqualTo(TutorialProfileActionStatus.Success));
        Assert.That(store.IsTutorialSuppressed, Is.False);
        Assert.That(store.GetPendingPrompts(TutorialContext.MainMenu), Has.Count.EqualTo(1));
        Assert.That(repository.CommitAttempts, Has.Count.EqualTo(1));
    }

    /// <summary>物理 adapter 只能生成 versioned player-profile.json 并可冷回读。</summary>
    [Test]
    public void Repository_CommitsOnlyPlayerProfileFileAndRoundTrips()
    {
        var repository = new AtomicJsonPlayerProfileRepository(_testDirectory);
        PlayerProfileSnapshot expected = CreateProfile(acknowledgedCount: 4);

        PlayerProfileRepositoryCommitResult commit = repository.Commit(expected);
        PlayerProfileRepositoryLoadResult load =
            new AtomicJsonPlayerProfileRepository(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(PlayerProfileRepositoryCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(PlayerProfileRepositoryLoadStatus.Success));
        Assert.That(load.Profile, Is.EqualTo(expected));
        Assert.That(
            Directory.GetFiles(_testDirectory).Select(Path.GetFileName),
            Is.EqualTo(new[] { "player-profile.json" }));
        Assert.That(File.Exists(Path.Combine(_testDirectory, "run-save.json")), Is.False);
        Assert.That(File.Exists(Path.Combine(_testDirectory, "run-history.json")), Is.False);
    }

    /// <summary>首次提交中断只留下 tmp 时不得把未提交候选提升为稳定 Profile。</summary>
    [Test]
    public void Repository_TemporaryOnlyFileIsInterruptedAndNeverPromoted()
    {
        File.WriteAllText(
            Path.Combine(_testDirectory, "player-profile.json.tmp"),
            PlayerProfileDocumentCodec.Write(CreateProfile(acknowledgedCount: 2)),
            new UTF8Encoding(false));
        var repository = new AtomicJsonPlayerProfileRepository(_testDirectory);

        PlayerProfileRepositoryLoadResult load = repository.Load();

        Assert.That(load.Status,
            Is.EqualTo(PlayerProfileRepositoryLoadStatus.InterruptedCommit));
        Assert.That(load.Profile, Is.Null);
        Assert.That(File.Exists(Path.Combine(_testDirectory, "player-profile.json")), Is.False);
    }

    /// <summary>原子替换失败必须保持旧正式 Profile 字节不变并返回类型化失败。</summary>
    [Test]
    public void Repository_ReplaceFailurePreservesStableBytes()
    {
        var initialRepository = new AtomicJsonPlayerProfileRepository(_testDirectory);
        Assert.That(
            initialRepository.Commit(CreateProfile(acknowledgedCount: 1)).Status,
            Is.EqualTo(PlayerProfileRepositoryCommitStatus.Success));
        string livePath = Path.Combine(_testDirectory, "player-profile.json");
        byte[] stableBytes = File.ReadAllBytes(livePath);
        var failingRepository = new AtomicJsonPlayerProfileRepository(
            _testDirectory,
            new ReplaceFailingPlayerProfileFileSystem());

        PlayerProfileRepositoryCommitResult commit =
            failingRepository.Commit(CreateProfile(acknowledgedCount: 2));

        Assert.That(commit.Status, Is.EqualTo(PlayerProfileRepositoryCommitStatus.IoFailure));
        Assert.That(File.ReadAllBytes(livePath), Is.EqualTo(stableBytes));
        Assert.That(
            new AtomicJsonPlayerProfileRepository(_testDirectory).Load().Profile,
            Is.EqualTo(CreateProfile(acknowledgedCount: 1)));
    }

    /// <summary>创建确认目录前 N 步的合法 Profile。</summary>
    private static PlayerProfileSnapshot CreateProfile(int acknowledgedCount)
    {
        return new PlayerProfileSnapshot(
            tutorialSkipped: false,
            acknowledgedPromptIds: TutorialPromptCatalog.Ordered
                .Take(acknowledgedCount)
                .Select(definition => definition.Id));
    }

    /// <summary>记录所有候选并按队列返回脚本化提交结果。</summary>
    private sealed class RecordingRepository : IPlayerProfileRepository
    {
        private readonly PlayerProfileRepositoryLoadResult _loadResult;
        private readonly Queue<PlayerProfileRepositoryCommitResult> _commitResults =
            new Queue<PlayerProfileRepositoryCommitResult>();

        /// <summary>按尝试顺序保存完整不可变候选。</summary>
        public List<PlayerProfileSnapshot> CommitAttempts { get; } =
            new List<PlayerProfileSnapshot>();

        /// <summary>冻结本测试每次 Load 返回的结果。</summary>
        public RecordingRepository(PlayerProfileRepositoryLoadResult loadResult)
        {
            _loadResult = loadResult;
        }

        /// <summary>把下一次提交结果加入脚本队列。</summary>
        public void EnqueueCommitResult(PlayerProfileRepositoryCommitResult result)
        {
            _commitResults.Enqueue(result);
        }

        /// <summary>返回脚本化读取结果。</summary>
        public PlayerProfileRepositoryLoadResult Load()
        {
            return _loadResult;
        }

        /// <summary>记录完整候选并返回下一项脚本化结果。</summary>
        public PlayerProfileRepositoryCommitResult Commit(PlayerProfileSnapshot profile)
        {
            CommitAttempts.Add(profile);
            return _commitResults.Count > 0
                ? _commitResults.Dequeue()
                : PlayerProfileRepositoryCommitResult.Succeeded();
        }
    }

    /// <summary>委托真实文件操作，仅在替换正式文件时注入 I/O 失败。</summary>
    private sealed class ReplaceFailingPlayerProfileFileSystem : IPlayerProfileFileSystem
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>查询真实文件是否存在。</summary>
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>创建真实目录。</summary>
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>严格读取真实 UTF-8 文本。</summary>
        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, StrictUtf8);
        }

        /// <summary>写入并持久刷新真实临时文件。</summary>
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

        /// <summary>执行真实首次移动。</summary>
        public void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>稳定注入替换失败。</summary>
        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            throw new IOException("Injected Player Profile replace failure.");
        }

        /// <summary>删除真实临时文件。</summary>
        public void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }
}
