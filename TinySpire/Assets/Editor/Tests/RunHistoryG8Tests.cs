using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Infrastructure.Persistence;
using TinySpire.Run;
using TinySpire.Run.History;
using TinySpire.Run.Map;

public sealed class RunHistoryG8Tests
{
    private static readonly DateTimeOffset FrozenUtc =
        new DateTimeOffset(2026, 8, 29, 12, 34, 56, TimeSpan.Zero);

    private string _testDirectory;

    /// <summary>为物理历史 adapter 测试建立独立临时目录。</summary>
    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TinySpire.RunHistoryG8Tests",
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

    /// <summary>三种 Terminal outcome 都必须冻结完整身份、计数与 Battle 绑定。</summary>
    [TestCase(RunOutcomeKind.Victory, 37, 1, true)]
    [TestCase(RunOutcomeKind.Defeat, 0, 1, true)]
    [TestCase(RunOutcomeKind.Abandoned, 73, 0, false)]
    public void SummaryFactory_TerminalOutcomesFreezeCompleteFacts(
        RunOutcomeKind outcomeKind,
        int expectedHealth,
        int expectedAttempts,
        bool expectsBattle)
    {
        RunState terminal = CreateTerminalState(
            outcomeKind,
            CreateRunGuid(outcomeKind),
            heroTemplateId: 1001);

        RunSummary summary = RunSummaryFactory.Create(terminal, FrozenUtc);

        Assert.That(summary.RunId, Is.EqualTo(terminal.RunId));
        Assert.That(summary.CompletedAtUtc, Is.EqualTo(FrozenUtc));
        Assert.That(summary.HeroTemplateId, Is.EqualTo(1001));
        Assert.That(summary.OutcomeKind, Is.EqualTo(outcomeKind));
        Assert.That(summary.FinalHealth, Is.EqualTo(expectedHealth));
        Assert.That(summary.MaxHealth, Is.EqualTo(80));
        Assert.That(summary.RandomRootSeed, Is.EqualTo(424242u));
        Assert.That(summary.BattleAttemptCount, Is.EqualTo(expectedAttempts));
        Assert.That(summary.OutcomeBattleNodeId != null, Is.EqualTo(expectsBattle));
        Assert.That(summary.OutcomeBattleAttemptSequence.HasValue, Is.EqualTo(expectsBattle));
        Assert.That(summary.Deck.Select(card => card.InstanceSequence), Is.EqualTo(new[] { 3, 8 }));
        Assert.That(summary.Deck.Select(card => card.UpgradeLevel), Is.EqualTo(new[] { 0, 2 }));
        Assert.That(summary.Holdings.Gold, Is.EqualTo(147));
        Assert.That(summary.Holdings.Relics.Single().TemplateId, Is.EqualTo(8001));
        Assert.That(summary.Holdings.Potions.Single().TemplateId, Is.EqualTo(9001));
    }

    /// <summary>历史更新契约必须携带完整统计结果，禁止发布无载荷失效信号。</summary>
    [Test]
    public void HistoryUpdateContract_CarriesCompleteStatisticsSnapshot()
    {
        System.Reflection.EventInfo statisticsChanged =
            typeof(RunHistoryService).GetEvent("StatisticsChanged");

        Assert.That(statisticsChanged, Is.Not.Null);
        Assert.That(
            statisticsChanged.EventHandlerType,
            Is.EqualTo(typeof(Action<RunHistoryStatisticsLoadResult>)));
        Assert.That(typeof(RunHistoryService).GetEvent("HistoryChanged"), Is.Null);
    }

    /// <summary>观察者异常不得遮蔽已耐久 Recorded，也不得阻止后续观察者收到完整快照。</summary>
    [Test]
    public void StatisticsChanged_ObserverFailureIsIsolatedFromDurableRecord()
    {
        RunState terminal = CreateTerminalState(
            RunOutcomeKind.Defeat,
            Guid.Parse("26262626-2222-3333-4444-555555555555"),
            heroTemplateId: 1001);
        var repository = new RecordingRunHistoryRepository();
        using var service = new RunHistoryService(repository, new FrozenClock(FrozenUtc));
        RunHistoryStatisticsLoadResult observed = null;
        service.StatisticsChanged += _ =>
            throw new InvalidOperationException("scripted observer failure");
        service.StatisticsChanged += snapshot => observed = snapshot;

        RunHistoryServiceRecordResult result = service.EnsureRecorded(terminal);

        Assert.That(result.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Recorded));
        Assert.That(service.LastRecordResult, Is.SameAs(result));
        Assert.That(observed, Is.Not.Null);
        Assert.That(observed.Status, Is.EqualTo(RunHistoryStatisticsLoadStatus.Success));
        Assert.That(observed.Statistics.TotalRuns, Is.EqualTo(1));
        Assert.That(service.LastStatisticsNotificationException, Is.Not.Null);
        Assert.That(service.LastStatisticsNotificationException.Message,
            Does.Contain("scripted observer failure"));
    }

    /// <summary>非终局 RunState 既不能制造 summary，也不能触发历史写入。</summary>
    [Test]
    public void NonTerminalState_IsRejectedByFactoryAndIgnoredByService()
    {
        RunState nonTerminal = CreateNonTerminalState();
        var repository = new RecordingRunHistoryRepository();
        using var service = new RunHistoryService(repository, new FrozenClock(FrozenUtc));

        Assert.Throws<InvalidOperationException>(() =>
            RunSummaryFactory.Create(nonTerminal, FrozenUtc));
        RunHistoryServiceRecordResult result = service.EnsureRecorded(nonTerminal);

        Assert.That(result.Status, Is.EqualTo(RunHistoryServiceRecordStatus.IgnoredNonTerminal));
        Assert.That(repository.RecordAttempts, Is.Empty);
    }

    /// <summary>summary 构造与 codec 往返都必须保持路径、牌组和持有物深冻结。</summary>
    [Test]
    public void SummaryAndCodec_DeepFreezePathDeckAndHoldings()
    {
        var path = new[]
        {
            new RunSummaryPathNode("L00-S00", MapNodeKind.Start, contentId: 0),
            new RunSummaryPathNode("L01-S00", MapNodeKind.Boss, contentId: 7001),
        };
        var deck = new[]
        {
            new RunSummaryCard(3, 3002, 0),
            new RunSummaryCard(8, 3123, 2),
        };
        var relics = new[] { new RunSummaryRelic(4, 8001) };
        var potions = new[] { new RunSummaryPotion(6, 9001) };
        var summary = new RunSummary(
            new RunId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            FrozenUtc,
            heroTemplateId: 1001,
            outcomeKind: RunOutcomeKind.Victory,
            outcomeBattleNodeId: "L01-S00",
            outcomeBattleAttemptSequence: 1,
            randomRootSeed: 424242u,
            finalHealth: 37,
            maxHealth: 80,
            battleAttemptCount: 1,
            path,
            deck,
            new RunSummaryHoldings(147, relics, potions));

        path[1] = new RunSummaryPathNode("L09-S09", MapNodeKind.Event, contentId: 7999);
        deck[0] = new RunSummaryCard(99, 3999, 9);
        relics[0] = new RunSummaryRelic(99, 8999);
        potions[0] = new RunSummaryPotion(99, 9999);
        string json = RunHistoryDocumentCodec.Write(summary);
        RunHistoryDocumentReadResult read = RunHistoryDocumentCodec.Read(json);

        Assert.That(summary.Path.Select(node => node.NodeId),
            Is.EqualTo(new[] { "L00-S00", "L01-S00" }));
        Assert.That(summary.Deck.Select(card => card.InstanceSequence), Is.EqualTo(new[] { 3, 8 }));
        Assert.That(summary.Holdings.Relics.Single().InstanceSequence, Is.EqualTo(4));
        Assert.That(summary.Holdings.Potions.Single().InstanceSequence, Is.EqualTo(6));
        Assert.That(read.Status, Is.EqualTo(RunHistoryDocumentReadStatus.Success));
        Assert.That(read.Summary, Is.EqualTo(summary));
        Assert.That(read.Summary.Path, Is.Not.SameAs(summary.Path));
        Assert.That(read.Summary.Deck, Is.Not.SameAs(summary.Deck));
        Assert.That(read.Summary.Holdings, Is.Not.SameAs(summary.Holdings));
    }

    /// <summary>坏 JSON 与未知 schema 必须类型化失败且不发布半合法摘要。</summary>
    [Test]
    public void DocumentCodec_InvalidJsonAndUnknownSchema_AreRejected()
    {
        RunSummary summary = CreateSummary(
            RunOutcomeKind.Victory,
            Guid.Parse("12121212-2222-3333-4444-555555555555"),
            heroTemplateId: 1001,
            FrozenUtc);
        var root = JObject.Parse(RunHistoryDocumentCodec.Write(summary));
        root["schemaVersion"] = 99;

        RunHistoryDocumentReadResult invalidJson = RunHistoryDocumentCodec.Read("{");
        RunHistoryDocumentReadResult unsupported = RunHistoryDocumentCodec.Read(root.ToString());

        Assert.That(invalidJson.Status, Is.EqualTo(RunHistoryDocumentReadStatus.InvalidJson));
        Assert.That(invalidJson.Summary, Is.Null);
        Assert.That(unsupported.Status, Is.EqualTo(RunHistoryDocumentReadStatus.UnsupportedSchema));
        Assert.That(unsupported.Summary, Is.Null);
    }

    /// <summary>物理 adapter 只创建 run-history/{RunId}.json，同内容幂等且不同内容拒绝覆盖。</summary>
    [Test]
    public void AtomicRepository_PerRunIdFileIsImmutableAndConflictSafe()
    {
        RunSummary summary = CreateSummary(
            RunOutcomeKind.Victory,
            Guid.Parse("13131313-2222-3333-4444-555555555555"),
            heroTemplateId: 1001,
            FrozenUtc);
        var repository = new AtomicJsonRunHistoryRepository(_testDirectory);

        RunHistoryRecordResult first = repository.Record(summary);
        string expectedPath = Path.Combine(
            _testDirectory,
            AtomicJsonRunHistoryRepository.HistoryDirectoryName,
            $"{summary.RunId}.json");
        string originalBytes = File.ReadAllText(expectedPath);
        RunHistoryRecordResult duplicate = repository.Record(summary);
        RunSummary conflicting = CloneWithCompletion(summary, FrozenUtc.AddMinutes(1));
        RunHistoryRecordResult conflict = repository.Record(conflicting);

        Assert.That(first.Status, Is.EqualTo(RunHistoryRecordStatus.Recorded));
        Assert.That(File.Exists(expectedPath), Is.True);
        Assert.That(duplicate.Status, Is.EqualTo(RunHistoryRecordStatus.AlreadyRecorded));
        Assert.That(conflict.Status, Is.EqualTo(RunHistoryRecordStatus.Conflict));
        Assert.That(File.ReadAllText(expectedPath), Is.EqualTo(originalBytes));
        Assert.That(repository.Load(summary.RunId).Summary, Is.EqualTo(summary));
        Assert.That(repository.ReadAll().Summaries, Is.EqualTo(new[] { summary }));
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(expectedPath), "*.tmp"), Is.Empty);
    }

    /// <summary>历史路径被普通文件占用时必须报告 I/O 故障，不能伪装成空历史。</summary>
    [Test]
    public void AtomicRepository_ReadAllRejectsHistoryPathOccupiedByRegularFile()
    {
        string historyPath = Path.Combine(
            _testDirectory,
            AtomicJsonRunHistoryRepository.HistoryDirectoryName);
        File.WriteAllText(historyPath, "occupied");
        var repository = new AtomicJsonRunHistoryRepository(_testDirectory);

        RunHistoryReadAllResult result = repository.ReadAll();

        Assert.That(result.Status, Is.EqualTo(RunHistoryReadAllStatus.IoFailure));
        Assert.That(result.Summaries, Is.Empty);
        Assert.That(result.Detail, Is.Not.Empty);
    }

    /// <summary>历史目录尚未创建时必须返回成功的空历史。</summary>
    [Test]
    public void AtomicRepository_ReadAllTreatsMissingHistoryDirectoryAsEmpty()
    {
        var repository = new AtomicJsonRunHistoryRepository(_testDirectory);

        RunHistoryReadAllResult result = repository.ReadAll();

        Assert.That(result.Status, Is.EqualTo(RunHistoryReadAllStatus.Success));
        Assert.That(result.Summaries, Is.Empty);
        Assert.That(result.Detail, Is.Empty);
    }

    /// <summary>目录探测本身失败时必须返回 I/O 故障且不发布空历史。</summary>
    [Test]
    public void AtomicRepository_ReadAllReturnsIoFailureWhenDirectoryProbeFails()
    {
        var repository = new AtomicJsonRunHistoryRepository(
            _testDirectory,
            new FailingDirectoryProbeRunHistoryFileSystem());

        RunHistoryReadAllResult result = repository.ReadAll();

        Assert.That(result.Status, Is.EqualTo(RunHistoryReadAllStatus.IoFailure));
        Assert.That(result.Summaries, Is.Empty);
        Assert.That(result.Detail, Does.Contain("scripted probe failure"));
    }

    /// <summary>commit 失败后服务必须冻结同一 summary 对象和完成时间再重试。</summary>
    [Test]
    public void Service_CommitFailureRetriesSameFrozenSummary()
    {
        RunState terminal = CreateTerminalState(
            RunOutcomeKind.Defeat,
            Guid.Parse("14141414-2222-3333-4444-555555555555"),
            heroTemplateId: 1001);
        var repository = new RecordingRunHistoryRepository();
        repository.EnqueueRecordStatus(RunHistoryRecordStatus.IoFailure);
        repository.EnqueueRecordStatus(RunHistoryRecordStatus.Recorded);
        var clock = new FrozenClock(FrozenUtc);
        using var service = new RunHistoryService(repository, clock);

        RunHistoryServiceRecordResult failed = service.EnsureRecorded(terminal);
        RunHistoryServiceRecordResult retried = service.EnsureRecorded(terminal);

        Assert.That(failed.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Unavailable));
        Assert.That(retried.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Recorded));
        Assert.That(repository.RecordAttempts, Has.Count.EqualTo(2));
        Assert.That(repository.RecordAttempts[1], Is.SameAs(repository.RecordAttempts[0]));
        Assert.That(repository.RecordAttempts[1].CompletedAtUtc, Is.EqualTo(FrozenUtc));
        Assert.That(clock.ReadCount, Is.EqualTo(1));
    }

    /// <summary>首次历史读取失败也必须立即冻结完成时间，重试不得读取更晚时钟。</summary>
    [Test]
    public void Service_LoadFailureRetriesSameFrozenCompletionTime()
    {
        RunState terminal = CreateTerminalState(
            RunOutcomeKind.Defeat,
            Guid.Parse("24242424-2222-3333-4444-555555555555"),
            heroTemplateId: 1001);
        var repository = new RecordingRunHistoryRepository();
        repository.EnqueueLoadStatus(RunHistoryLoadStatus.IoFailure);
        var clock = new MutableClock(FrozenUtc);
        using var service = new RunHistoryService(repository, clock);

        RunHistoryServiceRecordResult failed = service.EnsureRecorded(terminal);
        clock.SetUtcNow(FrozenUtc.AddMinutes(5));
        RunHistoryServiceRecordResult retried = service.EnsureRecorded(terminal);

        Assert.That(failed.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Unavailable));
        Assert.That(retried.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Recorded));
        Assert.That(repository.RecordAttempts, Has.Count.EqualTo(1));
        Assert.That(repository.RecordAttempts[0].CompletedAtUtc, Is.EqualTo(FrozenUtc));
        Assert.That(clock.ReadCount, Is.EqualTo(1));
    }

    /// <summary>读取失败后恢复出的既有历史也不能掩盖首次冻结终局期间的事实漂移。</summary>
    [Test]
    public void Service_LoadFailureRejectsTerminalDriftBeforeRecoveredHistory()
    {
        Guid runGuid = Guid.Parse("25252525-2222-3333-4444-555555555555");
        RunState firstTerminal = CreateTerminalState(
            RunOutcomeKind.Defeat,
            runGuid,
            heroTemplateId: 1001);
        RunState driftedTerminal = CreateTerminalState(
            RunOutcomeKind.Defeat,
            runGuid,
            heroTemplateId: 1002);
        var repository = new RecordingRunHistoryRepository();
        repository.EnqueueLoadStatus(RunHistoryLoadStatus.IoFailure);
        var clock = new FrozenClock(FrozenUtc);
        using var service = new RunHistoryService(repository, clock);

        RunHistoryServiceRecordResult failed = service.EnsureRecorded(firstTerminal);
        repository.Seed(RunSummaryFactory.Create(driftedTerminal, FrozenUtc));
        RunHistoryServiceRecordResult retried = service.EnsureRecorded(driftedTerminal);

        Assert.That(failed.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Unavailable));
        Assert.That(retried.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Conflict));
        Assert.That(repository.RecordAttempts, Is.Empty);
        Assert.That(clock.ReadCount, Is.EqualTo(1));
    }

    /// <summary>读取失败后的重试必须逐字段比较冻结摘要，不能接受另一完成时间的并发记录。</summary>
    [Test]
    public void Service_LoadFailureRejectsRecoveredHistoryWithDifferentCompletionTime()
    {
        RunState terminal = CreateTerminalState(
            RunOutcomeKind.Defeat,
            Guid.Parse("26262626-2222-3333-4444-555555555555"),
            heroTemplateId: 1001);
        var repository = new RecordingRunHistoryRepository();
        repository.EnqueueLoadStatus(RunHistoryLoadStatus.IoFailure);
        var clock = new MutableClock(FrozenUtc);
        using var service = new RunHistoryService(repository, clock);

        RunHistoryServiceRecordResult failed = service.EnsureRecorded(terminal);
        repository.Seed(RunSummaryFactory.Create(terminal, FrozenUtc.AddMinutes(1)));
        clock.SetUtcNow(FrozenUtc.AddMinutes(5));
        RunHistoryServiceRecordResult retried = service.EnsureRecorded(terminal);

        Assert.That(failed.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Unavailable));
        Assert.That(retried.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Conflict));
        Assert.That(retried.Summary.CompletedAtUtc, Is.EqualTo(FrozenUtc));
        Assert.That(repository.RecordAttempts, Is.Empty);
        Assert.That(clock.ReadCount, Is.EqualTo(1));
    }

    /// <summary>同一 Store 重复初始化、重复 Terminal 发布与冷恢复都只能产生一个物理记录尝试。</summary>
    [Test]
    public void Service_SubscriptionAndColdRecovery_RecordTerminalOnlyOnce()
    {
        Guid runGuid = Guid.Parse("15151515-2222-3333-4444-555555555555");
        RunState terminal = CreateTerminalState(
            RunOutcomeKind.Abandoned,
            runGuid,
            heroTemplateId: 1002);
        using var store = CreateStoreWithTerminal(terminal);
        var repository = new RecordingRunHistoryRepository();

        using (var firstService = new RunHistoryService(repository, new FrozenClock(FrozenUtc)))
        {
            firstService.Initialize(store);
            Assert.That(firstService.LastRecordResult.Status,
                Is.EqualTo(RunHistoryServiceRecordStatus.Recorded));

            RunHistoryServiceRecordResult duplicate = firstService.EnsureRecorded(store.Current);
            firstService.Initialize(store);

            Assert.That(duplicate.Status,
                Is.EqualTo(RunHistoryServiceRecordStatus.AlreadyRecorded));
        }

        using (var coldService = new RunHistoryService(
                   repository,
                   new FrozenClock(FrozenUtc.AddHours(5))))
        {
            coldService.Initialize(store);
            Assert.That(coldService.LastRecordResult.Status,
                Is.EqualTo(RunHistoryServiceRecordStatus.AlreadyRecorded));
            Assert.That(coldService.LastRecordResult.Summary.CompletedAtUtc,
                Is.EqualTo(FrozenUtc));
        }

        Assert.That(repository.RecordAttempts, Has.Count.EqualTo(1));
    }

    /// <summary>统计只从逐局历史派生 V/D/A、胜率与按 Hero 分组，重复同内容 RunId 只计一次。</summary>
    [Test]
    public void StatisticsProjection_DerivesOutcomeCountsVictoryRateAndHeroGroups()
    {
        RunSummary victory = CreateSummary(
            RunOutcomeKind.Victory,
            Guid.Parse("16161616-2222-3333-4444-555555555555"),
            heroTemplateId: 1001,
            FrozenUtc);
        RunSummary defeat = CreateSummary(
            RunOutcomeKind.Defeat,
            Guid.Parse("17171717-2222-3333-4444-555555555555"),
            heroTemplateId: 1001,
            FrozenUtc.AddMinutes(1));
        RunSummary abandoned = CreateSummary(
            RunOutcomeKind.Abandoned,
            Guid.Parse("18181818-2222-3333-4444-555555555555"),
            heroTemplateId: 1002,
            FrozenUtc.AddMinutes(2));

        RunHistoryStatistics statistics = RunHistoryStatisticsProjector.Project(new[]
        {
            victory,
            defeat,
            abandoned,
            victory,
        });

        Assert.That(statistics.TotalRuns, Is.EqualTo(3));
        Assert.That(statistics.VictoryCount, Is.EqualTo(1));
        Assert.That(statistics.DefeatCount, Is.EqualTo(1));
        Assert.That(statistics.AbandonedCount, Is.EqualTo(1));
        Assert.That(statistics.VictoryRate, Is.EqualTo(1d / 3d).Within(0.000001d));
        Assert.That(statistics.Heroes.Select(hero => hero.HeroTemplateId),
            Is.EqualTo(new[] { 1001, 1002 }));
        Assert.That(statistics.FindHero(1001).TotalRuns, Is.EqualTo(2));
        Assert.That(statistics.FindHero(1001).VictoryRate, Is.EqualTo(0.5d));
        Assert.That(statistics.FindHero(1002).AbandonedCount, Is.EqualTo(1));
    }

    /// <summary>同 RunId 不同内容不能被统计投影静默双计。</summary>
    [Test]
    public void StatisticsProjection_ConflictingRunIdIsRejected()
    {
        RunSummary first = CreateSummary(
            RunOutcomeKind.Victory,
            Guid.Parse("19191919-2222-3333-4444-555555555555"),
            heroTemplateId: 1001,
            FrozenUtc);
        RunSummary conflict = CloneWithCompletion(first, FrozenUtc.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() =>
            RunHistoryStatisticsProjector.Project(new[] { first, conflict }));
    }

    /// <summary>按 outcome 建立一份经过 RunState 自身校验的 Terminal 快照。</summary>
    private static RunState CreateTerminalState(
        RunOutcomeKind outcomeKind,
        Guid runGuid,
        int heroTemplateId)
    {
        MapNodeId start = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId destination = MapNodeId.FromPosition(layer: 1, slot: 0);
        bool victory = outcomeKind == RunOutcomeKind.Victory;
        MapNodeKind destinationKind = victory ? MapNodeKind.Boss : MapNodeKind.Combat;
        var map = new MapDefinition(
            profileId: "tinyspire.test.run-history.v1",
            generatorVersion: 1,
            mapSeed: 424242u,
            nodes: new[]
            {
                new MapNode(start, layer: 0, slot: 0, MapNodeKind.Start, contentId: 0),
                new MapNode(destination, layer: 1, slot: 0, destinationKind, contentId: 7001),
            },
            edges: new[] { new MapEdge(start, destination) });
        IReadOnlyList<MapNodeId> path = victory
            ? new[] { start, destination }
            : new[] { start };
        MapNodeId? committed = outcomeKind == RunOutcomeKind.Abandoned
            ? (MapNodeId?)null
            : destination;
        int currentHealth = outcomeKind == RunOutcomeKind.Defeat
            ? 0
            : outcomeKind == RunOutcomeKind.Victory
                ? 37
                : 73;
        var options = new RunRestoreOptions(
            new RunId(runGuid),
            heroTemplateId,
            currentHealth,
            maxHealth: 80,
            runDeck: new RunDeck(new[]
            {
                new RunCard(new RunCardInstanceId(3), templateId: 3002, upgradeLevel: 0),
                new RunCard(new RunCardInstanceId(8), templateId: 3123, upgradeLevel: 2),
            }),
            randomRootSeed: 424242u,
            map: map,
            pathNodeIds: path,
            progressPhase: RunProgressPhase.Terminal,
            committedNodeId: committed,
            outcomeKind: outcomeKind,
            holdings: new RunHoldings(
                new[] { new RunRelic(new RunRelicInstanceId(4), templateId: 8001) },
                new[] { new RunPotion(new RunPotionInstanceId(6), templateId: 9001) },
                gold: 147));
        using var store = new RunStateStore();
        return store.RestoreRun(options);
    }

    /// <summary>建立一份停在 Start 的非终局 RunState。</summary>
    private static RunState CreateNonTerminalState()
    {
        MapNodeId start = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId combat = MapNodeId.FromPosition(layer: 1, slot: 0);
        var map = new MapDefinition(
            profileId: "tinyspire.test.run-history.nonterminal.v1",
            generatorVersion: 1,
            mapSeed: 515151u,
            nodes: new[]
            {
                new MapNode(start, layer: 0, slot: 0, MapNodeKind.Start, contentId: 0),
                new MapNode(combat, layer: 1, slot: 0, MapNodeKind.Combat, contentId: 7001),
            },
            edges: new[] { new MapEdge(start, combat) });
        using var store = new RunStateStore();
        return store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("20202020-2222-3333-4444-555555555555")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));
    }

    /// <summary>从有效 Terminal RunState 建立一份测试摘要。</summary>
    private static RunSummary CreateSummary(
        RunOutcomeKind outcomeKind,
        Guid runGuid,
        int heroTemplateId,
        DateTimeOffset completedAtUtc)
    {
        return RunSummaryFactory.Create(
            CreateTerminalState(outcomeKind, runGuid, heroTemplateId),
            completedAtUtc);
    }

    /// <summary>只改变完成时间以制造同 RunId 不同内容的合法冲突摘要。</summary>
    private static RunSummary CloneWithCompletion(
        RunSummary source,
        DateTimeOffset completedAtUtc)
    {
        return new RunSummary(
            source.RunId,
            completedAtUtc,
            source.HeroTemplateId,
            source.OutcomeKind,
            source.OutcomeBattleNodeId,
            source.OutcomeBattleAttemptSequence,
            source.RandomRootSeed,
            source.FinalHealth,
            source.MaxHealth,
            source.BattleAttemptCount,
            source.Path,
            source.Deck,
            source.Holdings);
    }

    /// <summary>把已有 Terminal 快照恢复到一个可被服务冷订阅的 Store。</summary>
    private static RunStateStore CreateStoreWithTerminal(RunState terminal)
    {
        var store = new RunStateStore();
        store.RestoreRun(new RunRestoreOptions(
            terminal.RunId,
            terminal.HeroTemplateId,
            terminal.CurrentHealth,
            terminal.MaxHealth,
            terminal.RunDeck,
            terminal.RandomRootSeed,
            terminal.MapDefinition,
            terminal.PathNodeIds,
            terminal.ProgressPhase,
            terminal.CommittedNodeId,
            terminal.Outcome.Kind,
            terminal.Holdings));
        return store;
    }

    /// <summary>为每种 outcome 提供稳定且互不相同的测试 RunId。</summary>
    private static Guid CreateRunGuid(RunOutcomeKind outcomeKind)
    {
        switch (outcomeKind)
        {
            case RunOutcomeKind.Victory:
                return Guid.Parse("21212121-2222-3333-4444-555555555555");
            case RunOutcomeKind.Defeat:
                return Guid.Parse("22222222-2222-3333-4444-555555555555");
            case RunOutcomeKind.Abandoned:
                return Guid.Parse("23232323-2222-3333-4444-555555555555");
            default:
                throw new ArgumentOutOfRangeException(nameof(outcomeKind));
        }
    }

    /// <summary>按调用次数返回固定 UTC 的测试时钟。</summary>
    private sealed class FrozenClock : IRunHistoryClock
    {
        private readonly DateTimeOffset _utcNow;

        /// <summary>时钟被读取的次数。</summary>
        public int ReadCount { get; private set; }

        /// <summary>冻结该时钟始终返回的 UTC。</summary>
        public FrozenClock(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        /// <summary>记录读取并返回固定 UTC。</summary>
        public DateTimeOffset UtcNow
        {
            get
            {
                ReadCount++;
                return _utcNow;
            }
        }
    }

    /// <summary>允许测试在两次服务调用之间推进当前 UTC 的可控时钟。</summary>
    private sealed class MutableClock : IRunHistoryClock
    {
        private DateTimeOffset _utcNow;

        /// <summary>时钟被读取的次数。</summary>
        public int ReadCount { get; private set; }

        /// <summary>以初始 UTC 建立可推进时钟。</summary>
        public MutableClock(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        /// <summary>记录读取并返回当前 UTC。</summary>
        public DateTimeOffset UtcNow
        {
            get
            {
                ReadCount++;
                return _utcNow;
            }
        }

        /// <summary>把后续读取推进到指定 UTC。</summary>
        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }

    /// <summary>记录服务调用并支持脚本化 commit 结果的测试 repository。</summary>
    private sealed class RecordingRunHistoryRepository : IRunHistoryRepository
    {
        private readonly Dictionary<RunId, RunSummary> _stored =
            new Dictionary<RunId, RunSummary>();
        private readonly Queue<RunHistoryRecordStatus> _recordStatuses =
            new Queue<RunHistoryRecordStatus>();
        private readonly Queue<RunHistoryLoadStatus> _loadStatuses =
            new Queue<RunHistoryLoadStatus>();

        /// <summary>按调用顺序保存全部 record 候选对象。</summary>
        public List<RunSummary> RecordAttempts { get; } = new List<RunSummary>();

        /// <summary>追加下一次 Record 应返回的状态。</summary>
        public void EnqueueRecordStatus(RunHistoryRecordStatus status)
        {
            _recordStatuses.Enqueue(status);
        }

        /// <summary>追加下一次 Load 应返回的失败或未找到状态。</summary>
        public void EnqueueLoadStatus(RunHistoryLoadStatus status)
        {
            if (status == RunHistoryLoadStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            _loadStatuses.Enqueue(status);
        }

        /// <summary>模拟读取故障恢复后磁盘已经存在的一份不可变摘要。</summary>
        public void Seed(RunSummary summary)
        {
            _stored.Add(summary.RunId, summary);
        }

        /// <summary>按 RunId 读取已成功保存的测试摘要。</summary>
        public RunHistoryLoadResult Load(RunId runId)
        {
            if (_loadStatuses.Count > 0)
            {
                RunHistoryLoadStatus status = _loadStatuses.Dequeue();
                return status == RunHistoryLoadStatus.NotFound
                    ? RunHistoryLoadResult.NotFound()
                    : RunHistoryLoadResult.Failed(status, "scripted load failure");
            }

            return _stored.TryGetValue(runId, out RunSummary summary)
                ? RunHistoryLoadResult.Succeeded(summary)
                : RunHistoryLoadResult.NotFound();
        }

        /// <summary>记录候选并按脚本状态模拟不可变提交。</summary>
        public RunHistoryRecordResult Record(RunSummary summary)
        {
            RecordAttempts.Add(summary);
            RunHistoryRecordStatus status = _recordStatuses.Count > 0
                ? _recordStatuses.Dequeue()
                : RunHistoryRecordStatus.Recorded;
            switch (status)
            {
                case RunHistoryRecordStatus.Recorded:
                    _stored.Add(summary.RunId, summary);
                    return RunHistoryRecordResult.Recorded(summary.RunId);
                case RunHistoryRecordStatus.AlreadyRecorded:
                    return RunHistoryRecordResult.AlreadyRecorded(summary.RunId);
                case RunHistoryRecordStatus.Conflict:
                    return RunHistoryRecordResult.Conflict(summary.RunId, "scripted conflict");
                case RunHistoryRecordStatus.InvalidData:
                case RunHistoryRecordStatus.IoFailure:
                    return RunHistoryRecordResult.Failed(status, summary.RunId, "scripted failure");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>返回测试中已经成功保存的全部摘要。</summary>
        public RunHistoryReadAllResult ReadAll()
        {
            return RunHistoryReadAllResult.Succeeded(_stored.Values);
        }
    }

    /// <summary>只在目录探测处注入可控权限故障的文件系统边界。</summary>
    private sealed class FailingDirectoryProbeRunHistoryFileSystem : IRunHistoryFileSystem
    {
        /// <summary>模拟底层无法判断历史目录是否存在。</summary>
        public RunHistoryDirectoryProbeStatus ProbeDirectory(string path)
        {
            throw new UnauthorizedAccessException("scripted probe failure");
        }

        /// <summary>探测失败后不得继续查询正式文件。</summary>
        public bool FileExists(string path)
        {
            throw UnexpectedCall();
        }

        /// <summary>探测失败后不得创建目录。</summary>
        public void CreateDirectory(string path)
        {
            throw UnexpectedCall();
        }

        /// <summary>探测失败后不得枚举历史文件。</summary>
        public string[] GetFiles(string path, string searchPattern)
        {
            throw UnexpectedCall();
        }

        /// <summary>探测失败后不得读取历史文件。</summary>
        public string ReadAllText(string path)
        {
            throw UnexpectedCall();
        }

        /// <summary>探测失败后不得写入临时文件。</summary>
        public void WriteAllTextDurably(string path, string content)
        {
            throw UnexpectedCall();
        }

        /// <summary>探测失败后不得提交临时文件。</summary>
        public void MoveFile(string sourcePath, string destinationPath)
        {
            throw UnexpectedCall();
        }

        /// <summary>探测失败后不得清理临时文件。</summary>
        public void DeleteFile(string path)
        {
            throw UnexpectedCall();
        }

        /// <summary>为越过探测边界的意外调用提供一致诊断。</summary>
        private static InvalidOperationException UnexpectedCall()
        {
            return new InvalidOperationException(
                "Directory probe failure must stop history enumeration.");
        }
    }
}
