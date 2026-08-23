using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cfg;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class RunFlowServiceTests
{
    /// <summary>S0 recipe 提交失败时阻止地图推进，并只重试同一文档而不重取 entropy。</summary>
    [Test]
    public async Task CreateNewRun_WhenInitialCommitFails_BlocksMapEntryAndRetriesSameRecipe()
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
        MapNodeId nextNodeId = GetFirstSelectableNodeId(created);

        Assert.That(created.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(created.PathNodeIds, Has.Count.EqualTo(1));
        Assert.That(created.ActiveBattle, Is.Null);
        Assert.That(created.MapDefinition.Fingerprint, Has.Length.EqualTo(64));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(
            async () => await flow.EnterMapNodeAsync(nextNodeId),
            Throws.TypeOf<InvalidOperationException>());

        RunSaveCommitResult retry = flow.RetryPendingCommit();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(saves.CommitAttempts[0]));
        Assert.That(saves.CommitAttempts[1].MapFingerprint, Is.EqualTo(created.MapDefinition.Fingerprint));
        Assert.That(entropy.NextCount, Is.EqualTo(1));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.ContinueAvailable));

        await flow.EnterMapNodeAsync(nextNodeId);
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
    }

    /// <summary>有效 MapReady 文档只在 Continue 后恢复，并从已完成路径推导下一次节点战斗 seed。</summary>
    [Test]
    public async Task RefreshValidMapReadySave_DoesNotHydrateUntilContinueAndPreservesNextAttemptSeed()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId: 1002,
            deckTemplateId: 1002,
            randomRootSeed: 246810u,
            completedCombatCount: 1));
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
        MapNodeId nextNodeId = GetFirstSelectableNodeId(restored);
        await flow.EnterMapNodeAsync(nextNodeId);
        RunBattleInput nextAttempt = store.Current.ActiveBattle;

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(nextAttempt.BattleId.AttemptSequence, Is.EqualTo(2));
        Assert.That(nextAttempt.BattleId.NodeId, Is.EqualTo(nextNodeId));
        Assert.That(
            nextAttempt.RandomSeed,
            Is.EqualTo(RunStateStore.DeriveBattleSeed(246810u, attemptSequence: 2)));
        Assert.That(entropy.NextCount, Is.Zero);
        Assert.That(saves.CommitAttempts, Is.Empty);
    }

    /// <summary>BossGateReached 文档只在 Continue 后恢复为稳定门，并拒绝继续选择或启动 Boss 战。</summary>
    [Test]
    public void RefreshBossGateSave_RestoresStableGateAndRejectsFurtherEntry()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId: 1002,
            deckTemplateId: 1002,
            randomRootSeed: 357913u,
            progressPhase: RunSaveProgressPhase.BossGateReached));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 97531u);

        RunPersistenceState availability = flow.RefreshSaveAvailability();

        Assert.That(availability.CanContinue, Is.True);
        Assert.That(store.Current, Is.Null);

        RunState restored = flow.ContinueSavedRun();

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.BossGateReached));
        Assert.That(restored.MapDefinition.GetNode(restored.CurrentNodeId).Kind, Is.EqualTo(MapNodeKind.Boss));
        Assert.That(restored.ActiveBattle, Is.Null);
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(2));
        Assert.That(
            async () => await flow.EnterMapNodeAsync(restored.CurrentNodeId),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(() => flow.CreateBattleSetupOptions(), Throws.TypeOf<InvalidOperationException>());
    }

    /// <summary>普通战斗胜利先形成 MapReady，只有 RunEntry 加载完成后才提交同一稳定检查点。</summary>
    [Test]
    public async Task Victory_WaitsForRunEntryBeforeCommittingMapReadyCheckpoint()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 34567u);
        flow.CreateNewRun(heroTemplateId: 1002);
        MapNodeId selectedNodeId = GetFirstSelectableNodeId(store.Current);
        await flow.EnterMapNodeAsync(selectedNodeId);
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        scenes.HoldNextRunEntryLoad();

        Task handling = flow.HandleBattleResultAsync(
                battleId,
                CreateBattleResult(BattleResultKind.Victory, 1002, health: 41, maxHealth: 90))
            .AsTask();

        Assert.That(handling.IsCompleted, Is.False);
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.CurrentNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(store.Current.CommittedNodeId, Is.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitPending));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1), "RunEntry 未完成前只能存在开局检查点");

        scenes.ReleaseRunEntryLoad();
        await handling;

        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(saves.CommitAttempts[1].ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(saves.CommitAttempts[1].PathNodeIds, Is.EqualTo(new[]
        {
            MapNodeId.FromPosition(0, 0).Value,
            selectedNodeId.Value,
        }));
        Assert.That(saves.SuccessfulDocument, Is.SameAs(saves.CommitAttempts[1]));
    }

    /// <summary>胜利检查点提交失败时保留上一正式档与内存路径，并只重试同一新文档。</summary>
    [Test]
    public async Task VictoryCommitFailure_PreservesPreviousCheckpointAndRetriesSameDocument()
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
        RunSaveDocument openingCheckpoint = saves.SuccessfulDocument;
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1001, health: 29, maxHealth: 80));

        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PathNodeIds, Has.Count.EqualTo(2));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(saves.SuccessfulDocument, Is.SameAs(openingCheckpoint));
        Assert.That(openingCheckpoint.PathNodeIds, Has.Count.EqualTo(1));
        RunSaveDocument failedCheckpoint = saves.CommitAttempts[1];

        flow.RetryPendingCommit();

        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(3));
        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedCheckpoint));
        Assert.That(saves.SuccessfulDocument, Is.SameAs(failedCheckpoint));
        Assert.That(saves.SuccessfulDocument.PathNodeIds, Has.Count.EqualTo(2));
    }

    /// <summary>坏 JSON、未知 schema、中断提交与加载 IO 都禁用 Continue、保留数据且不隐式删除。</summary>
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

    /// <summary>缺失 Hero、Deck、Encounter 或地图 profile 时分别禁用 Continue 且保留原始文档。</summary>
    [TestCase(9991, 1001, true, null, RunPersistenceStatus.MissingHeroTemplate)]
    [TestCase(1001, 9992, true, null, RunPersistenceStatus.MissingDeckTemplate)]
    [TestCase(1001, 1001, false, null, RunPersistenceStatus.MissingEncounterTemplate)]
    [TestCase(1001, 1001, true, "missing.act.profile", RunPersistenceStatus.MissingMapProfile)]
    public void RefreshSaveWithMissingConfiguration_ClassifiesAndNeverDeletes(
        int heroTemplateId,
        int deckTemplateId,
        bool includeEncounter,
        string mapProfileId,
        RunPersistenceStatus expectedStatus)
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId,
            deckTemplateId,
            mapProfileId: mapProfileId));
        var flow = new RunFlowService(
            store,
            () => CreateTables(includeEncounter),
            new RecordingSceneFlow(),
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("99990000-aaaa-bbbb-cccc-ddddeeeeffff")),
                67890u)),
            saves);

        RunPersistenceState result = flow.RefreshSaveAvailability();

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.CanContinue, Is.False);
        Assert.That(result.HasStoredData, Is.True);
        Assert.That(store.Current, Is.Null);
        Assert.That(saves.DeleteCount, Is.Zero);
    }

    /// <summary>存在地图检查点时必须先显式删除；删除成功后才允许生成新 Run 与 S0 recipe。</summary>
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
        Assert.That(created.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(saves.SuccessfulDocument.MapFingerprint, Is.EqualTo(created.MapDefinition.Fingerprint));
        Assert.That(entropy.NextCount, Is.EqualTo(1));
    }

    /// <summary>删除失败保留槽位并阻止新开局，仍允许恢复删除前已验证的地图检查点。</summary>
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

    /// <summary>普通胜利存档失败可显式回退到上一成功路径，不把未保存节点伪装成已恢复。</summary>
    [Test]
    public async Task ExitAfterFailedVictoryCheckpoint_RestoresPreviousSuccessfulPath()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "checkpoint failed"));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 89012u);
        flow.CreateNewRun(heroTemplateId: 1002);
        RunSaveDocument openingCheckpoint = saves.SuccessfulDocument;
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1002, health: 33, maxHealth: 90));

        flow.ExitPendingRunToMenu();

        Assert.That(store.Current, Is.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.ContinueAvailable));
        Assert.That(saves.SuccessfulDocument, Is.SameAs(openingCheckpoint));
        RunState restored = flow.ContinueSavedRun();
        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(restored.PathNodeIds, Has.Count.EqualTo(1));
        Assert.That(restored.CurrentHealth, Is.EqualTo(openingCheckpoint.CurrentHealth));
        Assert.That(restored.BattleAttemptSequence, Is.Zero);
    }

    /// <summary>选择普通直接出边后，Battle setup 精确使用该冻结节点的 Encounter 与节点身份。</summary>
    [Test]
    public async Task CreateRunAndEnterCombat_MapsFrozenNodeFactsToBattleSetupAndSceneFlow()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var entropy = new FixedRunEntropySource(
            new RunEntropy(
                new RunId(Guid.Parse("11112222-3333-4444-5555-666677778888")),
                randomRootSeed: 987654321u));
        var flow = new RunFlowService(store, CreateTables, scenes, entropy, saves);

        RunState created = flow.CreateNewRun(heroTemplateId: 1002);
        MapNodeId selectedNodeId = GetFirstSelectableNodeId(created);
        MapNode selectedNode = created.MapDefinition.GetNode(selectedNodeId);
        await flow.EnterMapNodeAsync(selectedNodeId);
        RunBattleInput input = store.Current.ActiveBattle;
        BattleSetupOptions setup = flow.CreateBattleSetupOptions();

        Assert.That(created.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(created.MapDefinition.ProfileId, Is.EqualTo(TinySpireActMapProfiles.CurrentProfileId));
        Assert.That(created.MapDefinition.Nodes.Any(node => node.Kind == MapNodeKind.Boss), Is.True);
        Assert.That(saves.SuccessfulDocument.MapSeed, Is.EqualTo(created.MapDefinition.MapSeed));
        Assert.That(saves.SuccessfulDocument.MapFingerprint, Is.EqualTo(created.MapDefinition.Fingerprint));
        Assert.That(input.BattleId.RunId, Is.EqualTo(created.RunId));
        Assert.That(input.BattleId.NodeId, Is.EqualTo(selectedNodeId));
        Assert.That(input.EncounterTemplateId, Is.EqualTo(selectedNode.ContentId));
        Assert.That(setup.HeroTemplateId, Is.EqualTo(input.HeroTemplateId));
        Assert.That(setup.EncounterTemplateId, Is.EqualTo(input.EncounterTemplateId));
        Assert.That(setup.PlayerInitialHealth, Is.EqualTo(input.InitialHealth));
        Assert.That(setup.DeckTemplateId, Is.EqualTo(input.DeckTemplateId));
        Assert.That(setup.RandomSeed, Is.EqualTo(input.RandomSeed));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
    }

    /// <summary>沿普通路线完成多个 Combat 后可保存抵达 Boss 门，且不会启动真实 Boss Battle。</summary>
    [Test]
    public async Task MultipleVictories_ReachBossGateAndSaveWithoutStartingBossBattle()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 12345u);
        RunState created = flow.CreateNewRun(heroTemplateId: 1002);

        int victories = await AdvanceFirstOrdinaryRouteToBossAsync(flow, store);

        Assert.That(victories, Is.EqualTo(2));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.BossGateReached));
        Assert.That(store.Current.PathNodeIds, Has.Count.EqualTo(4));
        Assert.That(store.Current.MapDefinition.GetNode(store.Current.CurrentNodeId).Kind, Is.EqualTo(MapNodeKind.Boss));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(4));
        Assert.That(saves.SuccessfulDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.BossGateReached));
        Assert.That(saves.SuccessfulDocument.PathNodeIds.Last(), Is.EqualTo(store.Current.CurrentNodeId.Value));
        Assert.That(saves.SuccessfulDocument.MapFingerprint, Is.EqualTo(created.MapDefinition.Fingerprint));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[]
        {
            RunSceneAddresses.Battle,
            RunSceneAddresses.RunEntry,
            RunSceneAddresses.Battle,
            RunSceneAddresses.RunEntry,
        }));
        Assert.That(() => flow.CreateBattleSetupOptions(), Throws.TypeOf<InvalidOperationException>());
    }

    /// <summary>普通失败立即提交 Terminal(Defeat)；冷启动恢复失败态、禁止 Continue，确认删除后才清档。</summary>
    [Test]
    public async Task Defeat_SavesTerminalAndColdStartHydratesFailureWithoutContinueUntilDelete()
    {
        using var liveStore = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(liveStore, scenes, saves, randomRootSeed: 424242u);
        flow.CreateNewRun(heroTemplateId: 1001);
        MapNodeId failedNodeId = GetFirstSelectableNodeId(liveStore.Current);
        await flow.EnterMapNodeAsync(failedNodeId);
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Defeat, 1001, health: 0, maxHealth: 80));

        Assert.That(liveStore.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(liveStore.Current.TerminalReason, Is.EqualTo(RunTerminalReason.Defeat));
        Assert.That(liveStore.Current.CurrentHealth, Is.Zero);
        Assert.That(liveStore.Current.PathNodeIds, Has.Count.EqualTo(1));
        Assert.That(liveStore.Current.CommittedNodeId, Is.EqualTo(failedNodeId));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.TerminalDefeat));
        Assert.That(flow.Persistence.CanContinue, Is.False);
        Assert.That(saves.SuccessfulDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(saves.SuccessfulDocument.TerminalReason, Is.EqualTo(RunSaveTerminalReason.Defeat));
        Assert.That(saves.SuccessfulDocument.CommittedNodeId, Is.EqualTo(failedNodeId.Value));

        using var coldStore = new RunStateStore();
        var coldFlow = CreateFlow(coldStore, new RecordingSceneFlow(), saves, randomRootSeed: 777u);
        RunPersistenceState coldAvailability = coldFlow.RefreshSaveAvailability();

        Assert.That(coldAvailability.Status, Is.EqualTo(RunPersistenceStatus.TerminalDefeat));
        Assert.That(coldAvailability.CanContinue, Is.False);
        Assert.That(coldStore.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(coldStore.Current.TerminalReason, Is.EqualTo(RunTerminalReason.Defeat));
        Assert.That(() => coldFlow.ContinueSavedRun(), Throws.TypeOf<InvalidOperationException>());

        RunSaveDeleteResult deletion = coldFlow.AbandonSavedRun();

        Assert.That(deletion.Status, Is.EqualTo(RunSaveDeleteStatus.Success));
        Assert.That(coldStore.Current, Is.Null);
        Assert.That(coldFlow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.NotFound));
        Assert.That(saves.DeleteCount, Is.EqualTo(1));
    }

    /// <summary>Terminal 提交失败只能重试同一终局文档，不能回退到旧可继续检查点或重新入战。</summary>
    [Test]
    public async Task DefeatCommitFailure_RejectsRollbackAndRetriesSameTerminalDocument()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "terminal replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 654321u);
        flow.CreateNewRun(heroTemplateId: 1001);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Defeat, 1001, health: 0, maxHealth: 80));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        RunSaveDocument failedTerminal = saves.CommitAttempts[1];
        Assert.That(failedTerminal.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(() => flow.ExitPendingRunToMenu(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));

        RunSaveCommitResult retry = flow.RetryPendingCommit();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedTerminal));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.TerminalDefeat));
        Assert.That(flow.Persistence.CanContinue, Is.False);
        Assert.That(() => flow.CreateBattleSetupOptions(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[]
        {
            RunSceneAddresses.Battle,
            RunSceneAddresses.RunEntry,
        }));
    }

    /// <summary>非当前 attempt 的旧 BattleResult 必须零写入拒绝，保持当前节点战斗与 S0 不变。</summary>
    [Test]
    public async Task StaleBattleResult_IsRejectedWithoutChangingRunOrSave()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 101010u);
        flow.CreateNewRun(heroTemplateId: 1002);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleInput active = store.Current.ActiveBattle;
        var staleBattleId = new RunBattleId(
            active.BattleId.RunId,
            active.BattleId.AttemptSequence + 1,
            active.BattleId.NodeId);

        Assert.That(
            async () => await flow.HandleBattleResultAsync(
                staleBattleId,
                CreateBattleResult(BattleResultKind.Victory, 1002, health: 50, maxHealth: 90)),
            Throws.TypeOf<InvalidOperationException>());

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(store.Current.ActiveBattle, Is.SameAs(active));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
    }

    /// <summary>创建带确定身份、根 seed 与既有配置的 Flow 测试装配。</summary>
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

    /// <summary>返回当前节点按普通规则排序后的第一个可选冻结节点。</summary>
    private static MapNodeId GetFirstSelectableNodeId(RunState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        return MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary)
            .First();
    }

    /// <summary>沿第一条普通路线逐战胜利，最后只提交并保存 Boss 门。</summary>
    private static async UniTask<int> AdvanceFirstOrdinaryRouteToBossAsync(
        RunFlowService flow,
        RunStateStore store)
    {
        int victories = 0;
        int safetyLimit = store.Current.MapDefinition.Nodes.Count;
        while (victories < safetyLimit)
        {
            MapNodeId nextNodeId = GetFirstSelectableNodeId(store.Current);
            MapNode nextNode = store.Current.MapDefinition.GetNode(nextNodeId);
            await flow.EnterMapNodeAsync(nextNodeId);
            if (nextNode.Kind == MapNodeKind.Boss)
                return victories;

            RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
            int settledHealth = Math.Max(1, store.Current.MaxHealth - victories - 1);
            await flow.HandleBattleResultAsync(
                battleId,
                CreateBattleResult(
                    BattleResultKind.Victory,
                    store.Current.HeroTemplateId,
                    settledHealth,
                    store.Current.MaxHealth));
            victories++;
        }

        throw new InvalidOperationException("The deterministic test route did not reach a Boss gate.");
    }

    /// <summary>冻结一个单玩家 BattleResult，模拟表现屏障后的唯一公开结果。</summary>
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

    /// <summary>创建一份可覆盖配置引用与稳定地图阶段的 v2 recipe 文档。</summary>
    private static RunSaveDocument CreateDocument(
        int heroTemplateId = 1001,
        int deckTemplateId = 1001,
        uint randomRootSeed = 112233u,
        RunSaveProgressPhase progressPhase = RunSaveProgressPhase.MapReady,
        int completedCombatCount = 0,
        string mapProfileId = null)
    {
        ActMapProfile profile = TinySpireActMapProfiles.Current;
        uint mapSeed = RunRandomDomains.DeriveMapSeed(randomRootSeed);
        MapDefinition map = ActMapGenerator.Generate(profile, mapSeed);
        string[] pathNodeIds = progressPhase == RunSaveProgressPhase.BossGateReached
            ? BuildFirstPathToBoss(map).Select(nodeId => nodeId.Value).ToArray()
            : BuildFirstCombatPath(map, completedCombatCount)
                .Select(nodeId => nodeId.Value)
                .ToArray();
        bool terminal = progressPhase == RunSaveProgressPhase.Terminal;
        MapNodeId currentNodeId = new MapNodeId(pathNodeIds[pathNodeIds.Length - 1]);
        string committedNodeId = terminal
            ? MapReachability.GetSelectableNodeIds(
                    map,
                    currentNodeId,
                    MapTraversalMode.Ordinary)
                .First()
                .Value
            : null;
        int maxHealth = heroTemplateId == 1002 ? 90 : 80;

        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            "0f0e0d0c-0b0a-0908-0706-050403020100",
            heroTemplateId,
            currentHealth: terminal ? 0 : maxHealth,
            maxHealth,
            deckTemplateId,
            randomRootSeed,
            mapProfileId ?? map.ProfileId,
            map.GeneratorVersion,
            map.MapSeed,
            map.Fingerprint,
            pathNodeIds,
            progressPhase,
            committedNodeId,
            terminal ? RunSaveTerminalReason.Defeat : (RunSaveTerminalReason?)null);
    }

    /// <summary>沿普通直接边构造含指定数量已完成 Combat 的合法稳定路径。</summary>
    private static IReadOnlyList<MapNodeId> BuildFirstCombatPath(
        MapDefinition map,
        int completedCombatCount)
    {
        if (completedCombatCount < 0)
            throw new ArgumentOutOfRangeException(nameof(completedCombatCount));

        var path = new List<MapNodeId> { MapNodeId.FromPosition(0, 0) };
        for (int completed = 0; completed < completedCombatCount; completed++)
        {
            MapNodeId next = MapReachability.GetSelectableNodeIds(
                    map,
                    path[path.Count - 1],
                    MapTraversalMode.Ordinary)
                .First();
            if (map.GetNode(next).Kind != MapNodeKind.Combat)
                throw new ArgumentOutOfRangeException(nameof(completedCombatCount));
            path.Add(next);
        }

        return path;
    }

    /// <summary>沿每层第一条普通出边构造一条从 Start 到 Boss 门的合法冻结路径。</summary>
    private static IReadOnlyList<MapNodeId> BuildFirstPathToBoss(MapDefinition map)
    {
        var path = new List<MapNodeId> { MapNodeId.FromPosition(0, 0) };
        while (map.GetNode(path[path.Count - 1]).Kind != MapNodeKind.Boss)
        {
            MapNodeId next = MapReachability.GetSelectableNodeIds(
                    map,
                    path[path.Count - 1],
                    MapTraversalMode.Ordinary)
                .First();
            path.Add(next);
        }

        return path;
    }

    /// <summary>创建包含当前 G3 Encounter 的最小 Run 配置表。</summary>
    private static Tables CreateTables()
    {
        return CreateTables(includeEncounter: true);
    }

    /// <summary>按测试需要创建可显式缺失 Encounter 的最小配置表。</summary>
    private static Tables CreateTables(bool includeEncounter)
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.machine_gunner.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":3,\"max_ammo\":6,\"ammo_gain_per_round\":1,\"runtime_profile\":1}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002]},{\"id\":1002,\"card_template_ids\":[3003]}]"),
            ["battle_tbencounter"] = includeEncounter
                ? JArray.Parse("[{\"id\":5001,\"enemy_template_ids\":[2001]}]")
                : new JArray(),
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
        private readonly Queue<RunSaveLoadResult> _loadResults = new Queue<RunSaveLoadResult>();
        private readonly Queue<RunSaveCommitResult> _commitResults = new Queue<RunSaveCommitResult>();
        private readonly Queue<RunSaveDeleteResult> _deleteResults = new Queue<RunSaveDeleteResult>();

        /// <summary>按调用顺序保存全部提交文档。</summary>
        public List<RunSaveDocument> CommitAttempts { get; } = new List<RunSaveDocument>();

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

        /// <summary>记录检查点尝试，并只在编排结果成功时替换正式单槽。</summary>
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
