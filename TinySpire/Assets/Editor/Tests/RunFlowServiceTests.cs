using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cfg;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Run;

public sealed class RunFlowServiceTests
{
    /// <summary>S0 提交失败时保留 Available 内存态、阻止进战，并以同一文档重试且不重取 entropy。</summary>
    [Test]
    public async Task CreateNewRun_WhenInitialCommitFails_BlocksEntryAndRetriesSameDocument()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "initial commit failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var entropy = new CountingRunEntropySource(
            new RunEntropy(
                new RunId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
                randomRootSeed: 123456u));
        var flow = new RunFlowService(store, CreateTables, scenes, entropy, saves);

        RunState created = flow.CreateNewRun(heroTemplateId: 1001);

        Assert.That(created.NodeStatus, Is.EqualTo(RunNodeStatus.Available));
        Assert.That(created.ActiveBattle, Is.Null);
        Assert.That(created.BattleSnapshot, Is.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(
            async () => await flow.EnterBattleNodeAsync(),
            Throws.TypeOf<InvalidOperationException>());

        RunSaveCommitResult retry = flow.RetryPendingCommit();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(saves.CommitAttempts[0]));
        Assert.That(entropy.NextCount, Is.EqualTo(1));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.ContinueAvailable));
        await flow.EnterBattleNodeAsync();
    }

    /// <summary>显式发现有效存档只缓存文档；玩家选择 Continue 后才恢复，并从已存 attempt 序号确定性派生下一 seed。</summary>
    [Test]
    public async Task RefreshValidSave_DoesNotHydrateUntilContinueAndPreservesNextAttemptSeed()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId: 1002,
            deckTemplateId: 1002,
            encounterTemplateId: 5001,
            randomRootSeed: 246810u,
            nodeStatus: RunSaveNodeStatus.Available,
            battleAttemptSequence: 4));
        var entropy = new CountingRunEntropySource(new RunEntropy(
            new RunId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            randomRootSeed: 999u));
        var flow = new RunFlowService(
            store,
            CreateTables,
            new RecordingSceneFlow(),
            entropy,
            saves);

        RunPersistenceState availability = flow.RefreshSaveAvailability();

        Assert.That(availability.CanContinue, Is.True);
        Assert.That(store.Current, Is.Null);

        RunState restored = flow.ContinueSavedRun();
        RunBattleInput nextAttempt = await flow.EnterBattleNodeAsync();

        Assert.That(restored.NodeStatus, Is.EqualTo(RunNodeStatus.Available));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(4));
        Assert.That(nextAttempt.BattleId.AttemptSequence, Is.EqualTo(5));
        Assert.That(
            nextAttempt.RandomSeed,
            Is.EqualTo(RunStateStore.DeriveBattleSeed(246810u, attemptSequence: 5)));
        Assert.That(entropy.NextCount, Is.Zero);
        Assert.That(saves.CommitAttempts, Is.Empty);
    }

    /// <summary>Completed S1 只在 Continue 后恢复为无战斗暂存的已清除地图态，并拒绝再次进入唯一节点。</summary>
    [Test]
    public void RefreshCompletedSave_RestoresStableCompletedRunAndRejectsBattleEntry()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId: 1002,
            deckTemplateId: 1002,
            encounterTemplateId: 5001,
            randomRootSeed: 357913u,
            nodeStatus: RunSaveNodeStatus.Completed,
            battleAttemptSequence: 3));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 97531u);

        RunPersistenceState availability = flow.RefreshSaveAvailability();

        Assert.That(availability.CanContinue, Is.True);
        Assert.That(store.Current, Is.Null);

        RunState restored = flow.ContinueSavedRun();

        Assert.That(restored.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));
        Assert.That(restored.ActiveBattle, Is.Null);
        Assert.That(restored.BattleSnapshot, Is.Null);
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(3));
        Assert.That(
            async () => await flow.EnterBattleNodeAsync(),
            Throws.TypeOf<InvalidOperationException>());
    }

    /// <summary>胜利先形成无 transient 的 Completed 稳定态，只有 RunEntry 场景 await 完成后才精确提交一次 S1。</summary>
    [Test]
    public async Task Victory_WaitsForRunEntryBeforeCommittingSingleStableS1()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 34567u);
        flow.CreateNewRun(heroTemplateId: 1002);
        await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        scenes.HoldNextRunEntryLoad();

        Task handling = flow.HandleBattleResultAsync(
                battleId,
                CreateBattleResult(BattleResultKind.Victory, 1002, health: 41, maxHealth: 90))
            .AsTask();

        Assert.That(handling.IsCompleted, Is.False);
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(store.Current.BattleSnapshot, Is.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitPending));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1), "RunEntry 未完成前只能存在 S0");

        scenes.ReleaseRunEntryLoad();
        await handling;

        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(saves.CommitAttempts[1].NodeStatus, Is.EqualTo(RunSaveNodeStatus.Completed));
        Assert.That(saves.SuccessfulDocument, Is.SameAs(saves.CommitAttempts[1]));
    }

    /// <summary>S1 提交失败时旧 S0 仍是唯一成功档、内存保留 Completed，重试必须提交同一 S1 实例。</summary>
    [Test]
    public async Task VictoryCommitFailure_PreservesS0AndRetriesSameS1Document()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 45678u);
        flow.CreateNewRun(heroTemplateId: 1001);
        RunSaveDocument stableS0 = saves.SuccessfulDocument;
        await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1001, health: 29, maxHealth: 80));

        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(store.Current.BattleSnapshot, Is.Null);
        Assert.That(saves.SuccessfulDocument, Is.SameAs(stableS0));
        Assert.That(stableS0.NodeStatus, Is.EqualTo(RunSaveNodeStatus.Available));
        RunSaveDocument failedS1 = saves.CommitAttempts[1];

        flow.RetryPendingCommit();

        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(3));
        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedS1));
        Assert.That(saves.SuccessfulDocument, Is.SameAs(failedS1));
        Assert.That(saves.SuccessfulDocument.NodeStatus, Is.EqualTo(RunSaveNodeStatus.Completed));
    }

    /// <summary>坏 JSON、未知 schema、中断提交与加载 IO 都必须禁用 Continue，保留数据且不得隐式删除。</summary>
    [TestCase(RunSaveLoadStatus.InvalidJson, RunPersistenceStatus.InvalidJson)]
    [TestCase(RunSaveLoadStatus.InvalidDocument, RunPersistenceStatus.InvalidDocument)]
    [TestCase(RunSaveLoadStatus.UnsupportedSchema, RunPersistenceStatus.UnsupportedSchema)]
    [TestCase(RunSaveLoadStatus.InterruptedCommit, RunPersistenceStatus.InterruptedCommit)]
    [TestCase(RunSaveLoadStatus.IoFailure, RunPersistenceStatus.IoFailure)]
    public void RefreshUnavailableSave_ClassifiesDisablesAndNeverDeletes(
        RunSaveLoadStatus loadStatus,
        RunPersistenceStatus expectedStatus)
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueLoadResult(RunSaveLoadResult.Failed(
            loadStatus,
            "load detail",
            hasStoredData: true,
            hasPendingTemporaryFile: loadStatus == RunSaveLoadStatus.InterruptedCommit));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 56789u);

        RunPersistenceState result = flow.RefreshSaveAvailability();

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.CanContinue, Is.False);
        Assert.That(result.HasStoredData, Is.True);
        Assert.That(result.Detail, Is.EqualTo("load detail"));
        Assert.That(store.Current, Is.Null);
        Assert.That(saves.DeleteCount, Is.Zero);
        Assert.That(() => flow.ContinueSavedRun(), Throws.TypeOf<InvalidOperationException>());
    }

    /// <summary>每一种缺失配置引用都必须独立分类、禁用 Continue，并保留原始存档等待玩家确认。</summary>
    [TestCase(9991, 1001, 5001, RunPersistenceStatus.MissingHeroTemplate)]
    [TestCase(1001, 9992, 5001, RunPersistenceStatus.MissingDeckTemplate)]
    [TestCase(1001, 1001, 9993, RunPersistenceStatus.MissingEncounterTemplate)]
    public void RefreshSaveWithMissingConfiguration_ClassifiesAndNeverDeletes(
        int heroTemplateId,
        int deckTemplateId,
        int encounterTemplateId,
        RunPersistenceStatus expectedStatus)
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId,
            deckTemplateId,
            encounterTemplateId));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 67890u);

        RunPersistenceState result = flow.RefreshSaveAvailability();

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.CanContinue, Is.False);
        Assert.That(result.HasStoredData, Is.True);
        Assert.That(store.Current, Is.Null);
        Assert.That(saves.DeleteCount, Is.Zero);
    }

    /// <summary>有存档时新开局必须先显式放弃；确认删除成功后才允许创建新 Run。</summary>
    [Test]
    public void ExistingSave_RequiresSuccessfulExplicitAbandonBeforeCreatingNewRun()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument());
        var entropy = new CountingRunEntropySource(
            new RunEntropy(
                new RunId(Guid.Parse("12345678-1234-5678-90ab-1234567890ab")),
                randomRootSeed: 13579u));
        var flow = new RunFlowService(store, CreateTables, new RecordingSceneFlow(), entropy, saves);
        flow.RefreshSaveAvailability();

        Assert.That(() => flow.CreateNewRun(1001), Throws.TypeOf<InvalidOperationException>());
        Assert.That(entropy.NextCount, Is.Zero);

        RunSaveDeleteResult deletion = flow.AbandonSavedRun();
        RunState created = flow.CreateNewRun(1001);

        Assert.That(deletion.Status, Is.EqualTo(RunSaveDeleteStatus.Success));
        Assert.That(saves.DeleteCount, Is.EqualTo(1));
        Assert.That(created.NodeStatus, Is.EqualTo(RunNodeStatus.Available));
        Assert.That(entropy.NextCount, Is.EqualTo(1));
    }

    /// <summary>删除失败必须保留槽位并继续阻止新开局，不能把确认动作伪装成成功。</summary>
    [Test]
    public void AbandonFailure_PreservesStoredDataAndStillBlocksNewRun()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument());
        saves.EnqueueDeleteResult(RunSaveDeleteResult.Failed("delete failed"));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 78901u);
        flow.RefreshSaveAvailability();

        RunSaveDeleteResult deletion = flow.AbandonSavedRun();

        Assert.That(deletion.Status, Is.EqualTo(RunSaveDeleteStatus.IoFailure));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.DeleteFailed));
        Assert.That(flow.Persistence.HasStoredData, Is.True);
        Assert.That(flow.Persistence.CanContinue, Is.True);
        Assert.That(saves.SuccessfulDocument, Is.Not.Null);
        Assert.That(() => flow.CreateNewRun(1001), Throws.TypeOf<InvalidOperationException>());
        RunState continued = flow.ContinueSavedRun();
        Assert.That(continued.RunId.ToString(), Is.EqualTo(saves.SuccessfulDocument.RunId));
    }

    /// <summary>确认退出提交失败页时只丢弃未保存内存进度，并重新发现、继续上一份成功 S0。</summary>
    [Test]
    public async Task ExitAfterFailedS1_RefreshesAndRestoresPreviousSuccessfulCheckpoint()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "S1 failed"));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 89012u);
        flow.CreateNewRun(heroTemplateId: 1002);
        RunSaveDocument stableS0 = saves.SuccessfulDocument;
        await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1002, health: 33, maxHealth: 90));

        flow.ExitPendingRunToMenu();

        Assert.That(store.Current, Is.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.ContinueAvailable));
        Assert.That(flow.Persistence.CanContinue, Is.True);
        Assert.That(saves.SuccessfulDocument, Is.SameAs(stableS0));
        RunState restored = flow.ContinueSavedRun();
        Assert.That(restored.NodeStatus, Is.EqualTo(RunNodeStatus.Available));
        Assert.That(restored.CurrentHealth, Is.EqualTo(stableS0.CurrentHealth));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(stableS0.BattleAttemptSequence));
    }

    /// <summary>S0 只在 Hero 确认创建稳定 Run 后提交；进战、失败与重开全程不得写盘。</summary>
    [Test]
    public async Task CreateRunThenDefeatAndRestart_CommitsOnlyInitialStableCheckpoint()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = new RunFlowService(
            store,
            CreateTables,
            scenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef")),
                randomRootSeed: 654321u)),
            saves);

        flow.CreateNewRun(heroTemplateId: 1001);
        Assert.That(saves.CommittedDocuments, Has.Count.EqualTo(1));
        Assert.That(saves.CommittedDocuments[0].NodeStatus, Is.EqualTo(RunSaveNodeStatus.Available));

        await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Defeat, heroTemplateId: 1001, health: 0, maxHealth: 80));
        await flow.RestartFailedBattleAsync();

        Assert.That(saves.CommittedDocuments, Has.Count.EqualTo(1));
        Assert.That(saves.DeleteCount, Is.Zero);
    }

    /// <summary>新 Run 与入战编排只从配置和 RunState 生成既有 Battle setup seam 的完整输入。</summary>
    [Test]
    public async Task CreateRunAndEnterBattle_MapsRunFactsToBattleSetupAndSceneFlow()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var entropy = new FixedRunEntropySource(
            new RunEntropy(
                new RunId(Guid.Parse("11112222-3333-4444-5555-666677778888")),
                randomRootSeed: 987654321u));
        var flow = new RunFlowService(
            store,
            CreateTables,
            scenes,
            entropy,
            new InMemoryRunSaveStore());

        RunState created = flow.CreateNewRun(heroTemplateId: 1002);
        RunBattleInput input = await flow.EnterBattleNodeAsync();
        BattleSetupOptions setup = flow.CreateBattleSetupOptions();

        Assert.That(created.HeroTemplateId, Is.EqualTo(1002));
        Assert.That(created.CurrentHealth, Is.EqualTo(90));
        Assert.That(created.DeckTemplateId, Is.EqualTo(1002));
        Assert.That(input.BattleId.RunId, Is.EqualTo(created.RunId));
        Assert.That(input.RandomSeed, Is.LessThanOrEqualTo(int.MaxValue));
        Assert.That(setup.HeroTemplateId, Is.EqualTo(input.HeroTemplateId));
        Assert.That(setup.EncounterTemplateId, Is.EqualTo(input.EncounterTemplateId));
        Assert.That(setup.PlayerInitialHealth, Is.EqualTo(input.InitialHealth));
        Assert.That(setup.DeckTemplateId, Is.EqualTo(input.DeckTemplateId));
        Assert.That(setup.RandomSeed, Is.EqualTo(input.RandomSeed));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
    }

    /// <summary>稳定胜利结果经唯一 bridge seam 写回结算生命、完成节点并返回入口地图。</summary>
    [Test]
    public async Task HandleBattleResult_WithVictory_SettlesRunBeforeReturningToMap()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 12345u);
        flow.CreateNewRun(heroTemplateId: 1002);
        await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, heroTemplateId: 1002, health: 37, maxHealth: 90));

        Assert.That(store.Current.CurrentHealth, Is.EqualTo(37));
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(store.Current.BattleSnapshot, Is.Null);
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle, RunSceneAddresses.RunEntry }));
    }

    /// <summary>失败结果保留 snapshot；重开恢复战前生命、签发新 seed 并再次进入 BattleScene。</summary>
    [Test]
    public async Task HandleBattleResult_WithDefeat_RestartsSnapshotWithNewBattleSeed()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 424242u);
        flow.CreateNewRun(heroTemplateId: 1001);
        RunBattleInput failedAttempt = await flow.EnterBattleNodeAsync();
        RunBattleId failedBattleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            failedBattleId,
            CreateBattleResult(BattleResultKind.Defeat, heroTemplateId: 1001, health: 0, maxHealth: 80));
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.Failed));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(80));
        Assert.That(store.Current.BattleSnapshot.CurrentHealth, Is.EqualTo(80));

        RunBattleInput retry = await flow.RestartFailedBattleAsync();

        Assert.That(retry.InitialHealth, Is.EqualTo(80));
        Assert.That(retry.RandomSeed, Is.Not.EqualTo(failedAttempt.RandomSeed));
        Assert.That(retry.BattleId.AttemptSequence, Is.EqualTo(2));
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[]
            {
                RunSceneAddresses.Battle,
                RunSceneAddresses.RunEntry,
                RunSceneAddresses.Battle,
            }));
    }

    /// <summary>创建带确定身份、可变根 seed 与既有配置的 Flow 测试装配。</summary>
    private static RunFlowService CreateFlow(
        RunStateStore store,
        RecordingSceneFlow scenes,
        uint randomRootSeed)
    {
        return new RunFlowService(
            store,
            CreateTables,
            scenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("99990000-aaaa-bbbb-cccc-ddddeeeeffff")),
                randomRootSeed)),
            new InMemoryRunSaveStore());
    }

    /// <summary>创建可观测存档调用的 Flow 测试装配。</summary>
    private static RunFlowService CreateFlow(
        RunStateStore store,
        RecordingSceneFlow scenes,
        RecordingRunSaveStore saves,
        uint randomRootSeed)
    {
        return new RunFlowService(
            store,
            CreateTables,
            scenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("99990000-aaaa-bbbb-cccc-ddddeeeeffff")),
                randomRootSeed)),
            saves);
    }

    /// <summary>冻结一个单玩家 BattleResult，模拟命令队列表现屏障后的唯一公开结果。</summary>
    private static BattleResult CreateBattleResult(
        BattleResultKind kind,
        int heroTemplateId,
        int health,
        int maxHealth)
    {
        return new BattleResult(
            kind,
            authoritySequence: 1,
            roundNumber: 1,
            new[]
            {
                new BattleResultPlayerSnapshot(
                    new CombatantId(1),
                    heroTemplateId,
                    health,
                    maxHealth),
            });
    }

    /// <summary>创建一份可按测试覆盖配置引用与稳定状态的 v1 文档。</summary>
    private static RunSaveDocument CreateDocument(
        int heroTemplateId = 1001,
        int deckTemplateId = 1001,
        int encounterTemplateId = 5001,
        uint randomRootSeed = 112233u,
        RunSaveNodeStatus nodeStatus = RunSaveNodeStatus.Available,
        int battleAttemptSequence = 0)
    {
        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            "0f0e0d0c-0b0a-0908-0706-050403020100",
            heroTemplateId,
            currentHealth: heroTemplateId == 1002 ? 90 : 80,
            maxHealth: heroTemplateId == 1002 ? 90 : 80,
            deckTemplateId,
            encounterTemplateId,
            randomRootSeed,
            nodeStatus,
            battleAttemptSequence);
    }

    /// <summary>创建仅含两名可选 Hero 与固定临时遭遇的最小 Run 配置表。</summary>
    private static Tables CreateTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.machine_gunner.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":3,\"max_ammo\":6,\"ammo_gain_per_round\":1,\"runtime_profile\":1}]") ,
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002]},{\"id\":1002,\"card_template_ids\":[3003]}]"),
            ["battle_tbencounter"] = JArray.Parse(
                "[{\"id\":5001,\"enemy_template_ids\":[2001]}]"),
        };

        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>记录 Run 编排请求的稳定场景地址，替代真实 Addressables 切换。</summary>
    private sealed class RecordingSceneFlow : ISceneFlowService
    {
        private TaskCompletionSource<bool> _runEntryGate;

        /// <summary>按调用顺序保存全部目标场景地址。</summary>
        public List<string> LoadedAddresses { get; } = new List<string>();

        /// <summary>令下一次 RunEntry 加载保持未完成，以观察检查点提交的 await 边界。</summary>
        public void HoldNextRunEntryLoad()
        {
            if (_runEntryGate != null)
                throw new InvalidOperationException("A RunEntry load is already held.");

            _runEntryGate = new TaskCompletionSource<bool>();
        }

        /// <summary>完成当前被保持的 RunEntry 加载。</summary>
        public void ReleaseRunEntryLoad()
        {
            TaskCompletionSource<bool> gate = _runEntryGate
                ?? throw new InvalidOperationException("No RunEntry load is held.");
            gate.SetResult(true);
        }

        /// <summary>记录目标地址，并在测试要求时等待 RunEntry 完成信号。</summary>
        public async UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            LoadedAddresses.Add(targetSceneAddress);
            if (targetSceneAddress == RunSceneAddresses.RunEntry && _runEntryGate != null)
            {
                TaskCompletionSource<bool> gate = _runEntryGate;
                await gate.Task;
                _runEntryGate = null;
            }
        }
    }

    /// <summary>为测试提供一次确定的 Run 身份与根随机输入。</summary>
    private sealed class FixedRunEntropySource : IRunEntropySource
    {
        private readonly RunEntropy _entropy;

        /// <summary>保存下一局应使用的确定输入。</summary>
        public FixedRunEntropySource(RunEntropy entropy)
        {
            _entropy = entropy;
        }

        /// <summary>返回固定输入，便于断言派生后的完整 Run 事实。</summary>
        public RunEntropy Next()
        {
            return _entropy;
        }
    }

    /// <summary>记录取用次数的确定 entropy source，用于证明重试不会生成新身份或根 seed。</summary>
    private sealed class CountingRunEntropySource : IRunEntropySource
    {
        private readonly RunEntropy _entropy;

        /// <summary>当前测试累计取用 entropy 的次数。</summary>
        public int NextCount { get; private set; }

        /// <summary>保存每次请求都返回的确定输入。</summary>
        public CountingRunEntropySource(RunEntropy entropy)
        {
            _entropy = entropy;
        }

        /// <summary>记录一次取用并返回固定 Run 输入。</summary>
        public RunEntropy Next()
        {
            NextCount++;
            return _entropy;
        }
    }

    /// <summary>记录完整文档写入次数，并以最后一份成功文档模拟单槽。</summary>
    private sealed class RecordingRunSaveStore : IRunSaveStore
    {
        private readonly Queue<RunSaveLoadResult> _loadResults =
            new Queue<RunSaveLoadResult>();
        private readonly Queue<RunSaveCommitResult> _commitResults =
            new Queue<RunSaveCommitResult>();
        private readonly Queue<RunSaveDeleteResult> _deleteResults =
            new Queue<RunSaveDeleteResult>();

        /// <summary>按调用顺序保存全部提交文档。</summary>
        public List<RunSaveDocument> CommitAttempts { get; } =
            new List<RunSaveDocument>();

        /// <summary>兼容既有断言的全部提交尝试视图。</summary>
        public List<RunSaveDocument> CommittedDocuments => CommitAttempts;

        /// <summary>最后一份真正提交成功、代表正式单槽的文档。</summary>
        public RunSaveDocument SuccessfulDocument { get; private set; }

        /// <summary>记录删除请求次数。</summary>
        public int DeleteCount { get; private set; }

        /// <summary>建立初始为空的可编排单槽 fake。</summary>
        public RecordingRunSaveStore()
        {
        }

        /// <summary>建立已含一份成功检查点的可编排单槽 fake。</summary>
        public RecordingRunSaveStore(RunSaveDocument successfulDocument)
        {
            SuccessfulDocument = successfulDocument
                ?? throw new ArgumentNullException(nameof(successfulDocument));
        }

        /// <summary>编排下一次 Load 的明确结果。</summary>
        public void EnqueueLoadResult(RunSaveLoadResult result)
        {
            _loadResults.Enqueue(result ?? throw new ArgumentNullException(nameof(result)));
        }

        /// <summary>编排下一次 Commit 的明确结果。</summary>
        public void EnqueueCommitResult(RunSaveCommitResult result)
        {
            _commitResults.Enqueue(result ?? throw new ArgumentNullException(nameof(result)));
        }

        /// <summary>编排下一次 Delete 的明确结果。</summary>
        public void EnqueueDeleteResult(RunSaveDeleteResult result)
        {
            _deleteResults.Enqueue(result ?? throw new ArgumentNullException(nameof(result)));
        }

        /// <summary>读取最后一份成功提交的文档。</summary>
        public RunSaveLoadResult Load()
        {
            if (_loadResults.Count > 0)
                return _loadResults.Dequeue();

            return SuccessfulDocument == null
                ? RunSaveLoadResult.NotFound()
                : RunSaveLoadResult.Succeeded(SuccessfulDocument);
        }

        /// <summary>记录完整 checkpoint 尝试，并只在编排结果成功时替换正式单槽。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            CommitAttempts.Add(document);
            RunSaveCommitResult result = _commitResults.Count > 0
                ? _commitResults.Dequeue()
                : RunSaveCommitResult.Succeeded();
            if (result.Status == RunSaveCommitStatus.Success)
                SuccessfulDocument = document;
            return result;
        }

        /// <summary>记录删除尝试，并只在编排结果成功时清空正式单槽。</summary>
        public RunSaveDeleteResult Delete()
        {
            DeleteCount++;
            RunSaveDeleteResult result = _deleteResults.Count > 0
                ? _deleteResults.Dequeue()
                : RunSaveDeleteResult.Succeeded();
            if (result.Status == RunSaveDeleteStatus.Success)
                SuccessfulDocument = null;
            return result;
        }
    }
}
