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
        var expectedCards = new[]
        {
            new RunSaveCardDocument(instanceId: 17, templateId: 3002, upgradeLevel: 1),
            new RunSaveCardDocument(instanceId: 29, templateId: 3002, upgradeLevel: 0),
            new RunSaveCardDocument(instanceId: 61, templateId: 3123, upgradeLevel: 2),
        };
        var expectedRelics = new[]
        {
            new RunSaveRelicDocument(instanceId: 8, templateId: 8002),
            new RunSaveRelicDocument(instanceId: 9, templateId: 8001),
        };
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId: 1002,
            deckTemplateId: 1002,
            randomRootSeed: 246810u,
            completedCombatCount: 1,
            runCards: expectedCards,
            relics: expectedRelics));
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
        BattleSetupOptions setup = flow.CreateBattleSetupOptions();
        RunBattleId boundBattleId = flow.BindBattleAttempt(setup);

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 17, 29, 61 }));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.TemplateId),
            Is.EqualTo(new[] { 3002, 3002, 3123 }));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 1, 0, 2 }));
        Assert.That(nextAttempt.BattleId.AttemptSequence, Is.EqualTo(2));
        Assert.That(nextAttempt.BattleId.NodeId, Is.EqualTo(nextNodeId));
        Assert.That(boundBattleId, Is.EqualTo(nextAttempt.BattleId));
        Assert.That(
            setup.RunCards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 17, 29, 61 }));
        Assert.That(
            setup.RunCards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 1, 0, 2 }));
        Assert.That(setup.Holdings, Is.Not.SameAs(nextAttempt.Holdings));
        Assert.That(
            setup.Holdings.Relics.Select(relic => relic.InstanceId.Sequence),
            Is.EqualTo(new[] { 8, 9 }));
        Assert.That(
            setup.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8002, 8001 }));
        var reorderedHoldings = new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(9), templateId: 8001),
                new RunRelic(new RunRelicInstanceId(8), templateId: 8002),
            },
            setup.Holdings.Potions,
            setup.Holdings.Gold);
        var driftedSetup = new BattleSetupOptions(
            setup.HeroTemplateId,
            setup.EncounterTemplateId,
            checked((int)setup.RandomSeed),
            setup.PlayerInitialHealth,
            deckTemplateId: null,
            runCards: setup.RunCards,
            holdings: reorderedHoldings);
        Assert.That(
            () => flow.BindBattleAttempt(driftedSetup),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(
            nextAttempt.RandomSeed,
            Is.EqualTo(RunStateStore.DeriveBattleSeed(246810u, attemptSequence: 2)));
        Assert.That(entropy.NextCount, Is.Zero);
        Assert.That(saves.CommitAttempts, Is.Empty);
    }

    /// <summary>Continue 必须先把 legacy deck fallback 原子改写为 canonical RunCards，再发布 active Run。</summary>
    [Test]
    public void ContinueLegacyDeckCheckpoint_CommitsCanonicalBeforePublishingRun()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId: 1001,
            deckTemplateId: 1001,
            randomRootSeed: 246812u));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 97532u);
        flow.RefreshSaveAvailability();

        RunState restored = flow.ContinueSavedRun();

        Assert.That(store.Current, Is.SameAs(restored));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(saves.CommitAttempts[0].LegacyDeckTemplateId, Is.Null);
        Assert.That(saves.CommitAttempts[0].RunCards, Is.Not.Null.And.Not.Empty);
        Assert.That(
            saves.CommitAttempts[0].RunCards.Select(card => card.TemplateId),
            Is.EqualTo(restored.RunDeck.Cards.Select(card => card.TemplateId)));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.ContinueAvailable));
    }

    /// <summary>legacy canonical 改写失败时不得发布 Run；重试同一文档成功后才允许再次 Continue。</summary>
    [Test]
    public void ContinueLegacyDeckCheckpoint_WhenCanonicalCommitFails_KeepsStoreEmptyAndRetriesExactDocument()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(CreateDocument(
            heroTemplateId: 1001,
            deckTemplateId: 1001,
            randomRootSeed: 246813u));
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "canonical migration failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 97533u);
        flow.RefreshSaveAvailability();

        Assert.That(() => flow.ContinueSavedRun(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(store.Current, Is.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        RunSaveDocument canonicalDocument = saves.CommitAttempts[0];
        Assert.That(canonicalDocument.LegacyDeckTemplateId, Is.Null);
        Assert.That(canonicalDocument.RunCards, Is.Not.Null.And.Not.Empty);

        RunSaveCommitResult retry = flow.RetryPendingCommit();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Current, Is.Null);
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(canonicalDocument));

        RunState restored = flow.ContinueSavedRun();

        Assert.That(store.Current, Is.SameAs(restored));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.TemplateId),
            Is.EqualTo(canonicalDocument.RunCards.Select(card => card.TemplateId)));
    }

    /// <summary>已有 canonical RunCards 的 v4 旧档也必须先耐久改写 v5，而不能只依赖 legacy deck 信号。</summary>
    [Test]
    public void ContinueMigratedV4Checkpoint_CommitsCanonicalHoldingsBeforePublishingRun()
    {
        RunSaveDocument current = CreateDocument(
            heroTemplateId: 1001,
            deckTemplateId: 1001,
            randomRootSeed: 246814u,
            runCards: new[]
            {
                new RunSaveCardDocument(instanceId: 1, templateId: 3002, upgradeLevel: 0),
                new RunSaveCardDocument(instanceId: 2, templateId: 3003, upgradeLevel: 0),
            });
        JObject legacyJson = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        legacyJson["schemaVersion"] = 4;
        legacyJson.Remove("relics");
        legacyJson.Remove("potions");
        legacyJson.Remove("gold");
        legacyJson.Remove("pendingNodeVisit");
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(legacyJson.ToString());
        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.RequiresCanonicalRewrite, Is.True);

        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(read.Document);
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 97534u);
        flow.RefreshSaveAvailability();

        RunState restored = flow.ContinueSavedRun();

        Assert.That(store.Current, Is.SameAs(restored));
        Assert.That(restored.Holdings.Gold, Is.EqualTo(100));
        Assert.That(restored.Holdings.Relics, Is.Empty);
        Assert.That(restored.Holdings.Potions, Is.Empty);
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(saves.CommitAttempts[0].RequiresCanonicalRewrite, Is.False);
        Assert.That(saves.CommitAttempts[0].SchemaVersion, Is.EqualTo(5));
    }

    /// <summary>有效 RewardPending 文档只在 Continue 后恢复，且不重新生成奖励或漂移牌组实例。</summary>
    [Test]
    public void RefreshRewardPendingSave_DoesNotHydrateUntilContinueAndRestoresFrozenReward()
    {
        using var store = new RunStateStore();
        var expectedCards = new[]
        {
            new RunSaveCardDocument(instanceId: 17, templateId: 3002, upgradeLevel: 1),
            new RunSaveCardDocument(instanceId: 29, templateId: 3002, upgradeLevel: 0),
        };
        RunSaveDocument document = CreateDocument(
            heroTemplateId: 1001,
            deckTemplateId: 1001,
            randomRootSeed: 246811u,
            progressPhase: RunSaveProgressPhase.RewardPending,
            completedCombatCount: 1,
            runCards: expectedCards);
        var saves = new RecordingRunSaveStore(document);
        var entropy = new CountingRunEntropySource(new RunEntropy(
            new RunId(Guid.Parse("11111111-2222-3333-4444-666666666666")),
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

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(restored.CurrentHealth, Is.EqualTo(80));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(2));
        Assert.That(restored.CommittedNodeId?.Value, Is.EqualTo(document.CommittedNodeId));
        Assert.That(restored.PendingCardReward.Id.ToString(),
            Is.EqualTo(document.PendingCardReward.RewardId));
        Assert.That(restored.PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(new[] { 3105, 3123, 3157 }));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 17, 29 }));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 1, 0 }));
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

    /// <summary>四类 NodeVisitPending 必须经 Flow 冷恢复同一冻结事实，且重复进入在触达 save port 前失败。</summary>
    [TestCase(MapNodeKind.Rest)]
    [TestCase(MapNodeKind.Chest)]
    [TestCase(MapNodeKind.Shop)]
    [TestCase(MapNodeKind.Event)]
    public async Task RefreshNodeVisitPending_RestoresExactPayloadAndRejectsDuplicateEntry(
        MapNodeKind kind)
    {
        RunSaveDocument document = CreateAuthoritativeNodeVisitPendingDocument(kind);
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore(document);
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            saves,
            randomRootSeed: 975310u);

        RunPersistenceState availability = flow.RefreshSaveAvailability();

        Assert.That(availability.CanContinue, Is.True);
        Assert.That(store.Current, Is.Null);

        RunState restored = flow.ContinueSavedRun();
        string restoredJson = RunSaveDocumentCodec.Serialize(
            RunSaveDocumentMapper.Create(restored));

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(restored.PendingNodeVisit.Kind, Is.EqualTo(kind));
        Assert.That(restored.PendingNodeVisit.Id.ToString(),
            Is.EqualTo(document.PendingNodeVisit.VisitId));
        Assert.That(restoredJson, Is.EqualTo(RunSaveDocumentCodec.Serialize(document)));
        Assert.That(saves.CommitAttempts, Is.Empty);
        Assert.That(
            async () => await flow.EnterMapNodeAsync(restored.PendingNodeVisit.NodeId),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(saves.CommitAttempts, Is.Empty);
    }

    /// <summary>两名 Hero 胜利后都先提交卡牌与首战附着掉落冻结事实，再等待 RunEntry。</summary>
    [TestCase(1001, 80, 41)]
    [TestCase(1002, 90, 51)]
    public async Task Victory_CommitsHeroOwnedFrozenRewardBeforeLoadingRunEntry(
        int heroTemplateId,
        int maxHealth,
        int settledHealth)
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 34567u);
        flow.CreateNewRun(heroTemplateId);
        MapNodeId selectedNodeId = GetFirstSelectableNodeId(store.Current);
        await flow.EnterMapNodeAsync(selectedNodeId);
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        scenes.HoldNextRunEntryLoad();

        Task handling = flow.HandleBattleResultAsync(
                battleId,
                CreateBattleResult(
                    BattleResultKind.Victory,
                    heroTemplateId,
                    settledHealth,
                    maxHealth))
            .AsTask();

        Assert.That(handling.IsCompleted, Is.False);
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(store.Current.CurrentNodeId, Is.EqualTo(MapNodeId.FromPosition(0, 0)));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(settledHealth));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(store.Current.CommittedNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(store.Current.PendingCardReward, Is.Not.Null);
        Assert.That(store.Current.PendingCardReward.CandidateTemplateIds, Has.Count.EqualTo(3));
        Assert.That(
            store.Current.PendingCardReward.CandidateTemplateIds.Distinct().Count(),
            Is.EqualTo(3));
        int[] legalPool = heroTemplateId == 1001
            ? new[] { 3105, 3123, 3157 }
            : new[] { 3206, 3227, 3264 };
        Assert.That(store.Current.PendingCardReward.CandidateTemplateIds,
            Is.SubsetOf(legalPool));
        Assert.That(store.Current.PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(8001));
        Assert.That(store.Current.PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(9001));
        Assert.That(store.Current.Holdings.Relics, Is.Empty);
        Assert.That(store.Current.Holdings.Potions, Is.Empty);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.ContinueAvailable));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(saves.CommitAttempts[1].ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(saves.CommitAttempts[1].PendingCardReward.RewardId,
            Is.EqualTo(store.Current.PendingCardReward.Id.ToString()));
        Assert.That(saves.CommitAttempts[1].PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(store.Current.PendingCardReward.CandidateTemplateIds));
        Assert.That(saves.CommitAttempts[1].PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(8001));
        Assert.That(saves.CommitAttempts[1].PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(9001));
        Assert.That(saves.CommitAttempts[1].PathNodeIds,
            Is.EqualTo(new[] { MapNodeId.FromPosition(0, 0).Value }));
        Assert.That(saves.CommitAttempts[1].CommittedNodeId, Is.EqualTo(selectedNodeId.Value));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[]
        {
            RunSceneAddresses.Battle,
            RunSceneAddresses.RunEntry,
        }));

        scenes.ReleaseRunEntryLoad();
        await handling;

        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(saves.SuccessfulDocument, Is.SameAs(saves.CommitAttempts[1]));
    }

    /// <summary>胜利保存失败时 Store 保持 InBattle，重试同一文档成功后才发布同一冻结奖励。</summary>
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
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 45678u);
        flow.CreateNewRun(heroTemplateId: 1001);
        RunSaveDocument openingCheckpoint = saves.SuccessfulDocument;
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1001, health: 29, maxHealth: 80));

        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(store.Current.PathNodeIds, Has.Count.EqualTo(1));
        Assert.That(store.Current.CommittedNodeId, Is.EqualTo(battleId.NodeId));
        Assert.That(store.Current.PendingCardReward, Is.Null);
        Assert.That(store.Current.ActiveBattle, Is.Not.Null);
        Assert.That(saves.SuccessfulDocument, Is.SameAs(openingCheckpoint));
        Assert.That(openingCheckpoint.PathNodeIds, Has.Count.EqualTo(1));
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle }));
        RunSaveDocument failedCheckpoint = saves.CommitAttempts[1];
        string frozenRewardId = failedCheckpoint.PendingCardReward.RewardId;
        Assert.That(failedCheckpoint.PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(8001));
        Assert.That(failedCheckpoint.PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(9001));

        flow.RetryPendingCommit();

        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(3));
        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedCheckpoint));
        Assert.That(saves.SuccessfulDocument, Is.SameAs(failedCheckpoint));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(saves.SuccessfulDocument.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(saves.SuccessfulDocument.PathNodeIds, Has.Count.EqualTo(1));
        Assert.That(saves.SuccessfulDocument.PendingCardReward.RewardId,
            Is.EqualTo(frozenRewardId));
        Assert.That(store.Current.PendingCardReward.Id.ToString(), Is.EqualTo(frozenRewardId));
        Assert.That(store.Current.PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(8001));
        Assert.That(store.Current.PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(9001));
        Assert.That(store.Current.Holdings.Relics, Is.Empty);
        Assert.That(store.Current.Holdings.Potions, Is.Empty);
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle, RunSceneAddresses.RunEntry }));
    }

    /// <summary>失败重试复用同一冻结文档，消费药水先移除但附着掉落尚不提前发放。</summary>
    [Test]
    public async Task VictoryWithConsumedPotion_SaveFailureKeepsBattleHoldingsAndRetryCommitsSameSuccessor()
    {
        RunHoldings holdings = RunHoldings.Empty(initialGold: 73)
            .AddPotion(templateId: 9001)
            .AddPotion(templateId: 9002);
        RunPotionInstanceId consumedId = holdings.Potions[0].InstanceId;
        uint randomRootSeed = 45679u;
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.Current,
            RunRandomDomains.DeriveMapSeed(randomRootSeed));
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("45678901-aaaa-bbbb-cccc-123456789012")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed,
            map,
            holdings));
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(created));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(
                BattleResultKind.Victory,
                1001,
                health: 29,
                maxHealth: 80,
                consumedPotionInstanceIds: new[] { consumedId }));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(
            store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001, 9002 }));
        RunSaveDocument failedDocument = saves.CommitAttempts[0];
        Assert.That(
            failedDocument.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9002 }));
        string frozenRewardId = failedDocument.PendingCardReward.RewardId;
        Assert.That(failedDocument.PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(8001));
        Assert.That(failedDocument.PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(9001));
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle }));

        RunSaveCommitResult retry = flow.RetryPendingCommit();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(
            store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9002 }));
        Assert.That(store.Current.Holdings.Relics, Is.Empty);
        Assert.That(store.Current.PendingCardReward.Id.ToString(), Is.EqualTo(frozenRewardId));
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle, RunSceneAddresses.RunEntry }));
    }

    /// <summary>选择冻结候选必须先提交卡牌与附着掉落完整后继，再让下一战收到相同事实。</summary>
    [TestCase(1001, 80, 37)]
    [TestCase(1002, 90, 47)]
    public async Task SelectCardReward_CommitsBeforePublishingAndNextBattleReceivesSelectedInstance(
        int heroTemplateId,
        int maxHealth,
        int settledHealth)
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 56789u);
        flow.CreateNewRun(heroTemplateId);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(
                BattleResultKind.Victory,
                heroTemplateId,
                settledHealth,
                maxHealth));
        RunState pending = store.Current;
        int selectedTemplateId = pending.PendingCardReward.CandidateTemplateIds[1];

        RunSaveCommitResult settlement = flow.SettleCardReward(
            pending.PendingCardReward.Id,
            selectedTemplateId);

        Assert.That(settlement.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.CurrentNodeId, Is.EqualTo(battleId.NodeId));
        Assert.That(store.Current.PathNodeIds, Has.Count.EqualTo(2));
        Assert.That(store.Current.PendingCardReward, Is.Null);
        Assert.That(store.Current.RunDeck.Cards, Has.Count.EqualTo(2));
        RunCard rewardedCard = store.Current.RunDeck.Cards[1];
        Assert.That(rewardedCard.InstanceId.Sequence, Is.EqualTo(2));
        Assert.That(rewardedCard.TemplateId, Is.EqualTo(selectedTemplateId));
        Assert.That(rewardedCard.UpgradeLevel, Is.Zero);
        Assert.That(store.Current.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(3));
        Assert.That(saves.CommitAttempts[2].ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(saves.CommitAttempts[2].PendingCardReward, Is.Null);
        Assert.That(saves.CommitAttempts[2].RunCards.Last().InstanceId, Is.EqualTo(2));
        Assert.That(saves.CommitAttempts[2].Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(saves.CommitAttempts[2].Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));

        using RunStateStore nextBattleStore = RestoreBeforeNextCombatAfterMixedRoute(store.Current);
        var nextBattleFlow = CreateFlow(
            nextBattleStore,
            scenes,
            saves,
            randomRootSeed: 56789u);
        MapNodeId nextCombatNodeId = GetFirstSelectableCombatNodeId(nextBattleStore.Current);
        await nextBattleFlow.EnterMapNodeAsync(nextCombatNodeId);
        BattleSetupOptions nextBattle = nextBattleFlow.CreateBattleSetupOptions();

        Assert.That(nextBattle.RunCards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 1, 2 }));
        Assert.That(nextBattle.RunCards.Last().TemplateId, Is.EqualTo(selectedTemplateId));
        Assert.That(nextBattle.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(nextBattle.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));

        using BattleSession nextSession = BattleSession.FromConfig(CreateTables(), nextBattle);
        nextSession.CardZones.Draw(nextSession.CardZones.Cards.Count);
        CardInstanceData[] drawnRewardInstances = nextSession.CardZones.Hand
            .Select(cardId =>
            {
                Assert.That(nextSession.CardZones.TryGetCard(cardId, out CardInstanceData card),
                    Is.True);
                return card;
            })
            .Where(card => card.OriginRunCardInstanceId == rewardedCard.InstanceId)
            .ToArray();
        Assert.That(drawnRewardInstances, Has.Length.EqualTo(1));
        Assert.That(drawnRewardInstances[0].TemplateId, Is.EqualTo(selectedTemplateId));
        Assert.That(drawnRewardInstances[0].UpgradeLevel, Is.Zero);
    }

    /// <summary>跳过卡牌仍须原子发放同一附着掉落，但不能改变任何 RunCard。</summary>
    [Test]
    public async Task SkipCardReward_CommitsCompletedPathWithoutChangingDeck()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            saves,
            randomRootSeed: 67890u);
        flow.CreateNewRun(heroTemplateId: 1002);
        RunCard[] openingCards = store.Current.RunDeck.Cards.ToArray();
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1002, health: 48, maxHealth: 90));
        RunCardRewardId rewardId = store.Current.PendingCardReward.Id;

        RunSaveCommitResult settlement = flow.SettleCardReward(
            rewardId,
            selectedCardTemplateId: null);

        Assert.That(settlement.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.RunDeck.Cards, Is.EqualTo(openingCards));
        Assert.That(store.Current.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));
        Assert.That(saves.SuccessfulDocument.RunCards.Select(card => card.InstanceId),
            Is.EqualTo(openingCards.Select(card => card.InstanceId.Sequence)));
        Assert.That(saves.SuccessfulDocument.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(saves.SuccessfulDocument.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));
    }

    /// <summary>结算落盘失败保持 Pending，重试同一文档后才一次发布卡牌与附着掉落。</summary>
    [Test]
    public async Task CardRewardCommitFailure_RetriesSameDocumentBeforePublishingExactlyOnce()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "settlement replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            saves,
            randomRootSeed: 78901u);
        flow.CreateNewRun(heroTemplateId: 1001);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1001, health: 44, maxHealth: 80));
        RunState pending = store.Current;
        int selectedTemplateId = pending.PendingCardReward.CandidateTemplateIds[0];

        RunSaveCommitResult failed = flow.SettleCardReward(
            pending.PendingCardReward.Id,
            selectedTemplateId);
        RunSaveDocument failedDocument = saves.CommitAttempts[2];

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(store.Current.RunDeck.Cards,
            Has.Count.EqualTo(pending.RunDeck.Cards.Count));
        Assert.That(failedDocument.RunCards.Last().InstanceId, Is.EqualTo(2));
        Assert.That(store.Current.Holdings.Relics, Is.Empty);
        Assert.That(store.Current.Holdings.Potions, Is.Empty);
        Assert.That(failedDocument.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(failedDocument.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));

        RunSaveCommitResult retry = flow.RetryPendingCommit();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[3], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.RunDeck.Cards.Count(card =>
            card.TemplateId == selectedTemplateId), Is.EqualTo(1));
        Assert.That(store.Current.RunDeck.Cards.Last().InstanceId.Sequence, Is.EqualTo(2));
        Assert.That(store.Current.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));
    }

    /// <summary>伪造、过期和重复奖励命令必须在 save port 之前拒绝，保持提交计数不变。</summary>
    [Test]
    public async Task CardRewardForgedStaleAndDuplicateInputs_AreRejectedBeforeSaveWrite()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            saves,
            randomRootSeed: 89013u);
        flow.CreateNewRun(heroTemplateId: 1001);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1001, health: 52, maxHealth: 80));
        RunState pending = store.Current;
        int writesBeforeInvalidCommands = saves.CommitAttempts.Count;
        var staleRewardId = new RunCardRewardId(new RunBattleId(
            pending.RunId,
            pending.BattleAttemptSequence + 1,
            pending.CommittedNodeId.Value));

        Assert.Throws<InvalidOperationException>(() =>
            flow.SettleCardReward(staleRewardId, pending.PendingCardReward.CandidateTemplateIds[0]));
        Assert.Throws<InvalidOperationException>(() =>
            flow.SettleCardReward(pending.PendingCardReward.Id, selectedCardTemplateId: 3999));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(writesBeforeInvalidCommands));

        RunCardRewardId validRewardId = pending.PendingCardReward.Id;
        int validTemplateId = pending.PendingCardReward.CandidateTemplateIds[0];
        flow.SettleCardReward(validRewardId, validTemplateId);
        int writesAfterSettlement = saves.CommitAttempts.Count;

        Assert.Throws<InvalidOperationException>(() =>
            flow.SettleCardReward(validRewardId, validTemplateId));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(writesAfterSettlement));
    }

    /// <summary>节点进入必须先保存同一 Pending，失败时零发布且 exact retry 不重建文档。</summary>
    [Test]
    public async Task NodeVisitEntryCommitFailure_RetriesSameDocumentBeforePublishing()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Rest,
            contentId: 7101);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("15151515-3737-5959-8181-939393939393")),
            heroTemplateId: 1001,
            initialHealth: 70,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));
        var saves = new RecordingRunSaveStore(RunSaveDocumentMapper.Create(created));
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "node enter failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            saves,
            randomRootSeed: 151515u);
        MapNodeId restNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);

        await flow.EnterMapNodeAsync(restNodeId);
        RunSaveDocument enterDocument = saves.CommitAttempts[0];

        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(store.Current, Is.SameAs(created));
        Assert.That(enterDocument.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(enterDocument.PendingNodeVisit.NodeId, Is.EqualTo(restNodeId.Value));
        Assert.That(enterDocument.PendingNodeVisit.RestPayload.HealAmount, Is.EqualTo(24));
        Assert.That(flow.CanRollbackFailedCheckpoint, Is.False);
        Assert.Throws<InvalidOperationException>(() => flow.ExitPendingRunToMenu());
        Assert.That(store.Current, Is.SameAs(created));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));

        RunSaveCommitResult enterRetry = flow.RetryPendingCommit();

        Assert.That(enterRetry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(enterDocument));
        RunState entered = store.Current;
        Assert.That(entered.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(entered.PendingNodeVisit.Id,
            Is.EqualTo(new RunNodeVisitId(created.RunId, restNodeId)));
        Assert.That(entered.PathNodeIds, Is.EqualTo(created.PathNodeIds));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
    }

    /// <summary>Rest 治疗必须先保存完整后继，失败保持原 Pending 并以同一文档 hard retry。</summary>
    [Test]
    public async Task RestHealCommitFailure_PreservesPendingAndRetriesSameDocumentBeforePublishing()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Rest,
            contentId: 7101);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("20202020-4242-6464-8686-989898989898")),
            heroTemplateId: 1001,
            initialHealth: 40,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));
        var saves = new RecordingRunSaveStore(RunSaveDocumentMapper.Create(created));
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 202020u);
        await flow.EnterMapNodeAsync(MapNodeId.FromPosition(layer: 1, slot: 0));
        RunState pending = store.Current;
        RunNodeVisitId visitId = pending.PendingNodeVisit.Id;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "rest heal replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());

        RunSaveCommitResult failed = flow.SettleRestHeal(visitId);
        RunSaveDocument failedDocument = saves.CommitAttempts[1];

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(40));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(failedDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(failedDocument.CurrentHealth, Is.EqualTo(64));
        Assert.That(failedDocument.PendingNodeVisit, Is.Null);
        Assert.That(failedDocument.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId.Value));
        Assert.That(flow.CanRollbackFailedCheckpoint, Is.False);
        Assert.Throws<InvalidOperationException>(() => flow.SettleRestHeal(visitId));
        Assert.Throws<InvalidOperationException>(() => flow.SettleRestUpgrade(
            visitId,
            new RunCardInstanceId(1)));
        Assert.Throws<InvalidOperationException>(() => flow.ExitPendingRunToMenu());
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(scenes.LoadedAddresses, Is.Empty);

        RunSaveCommitResult retried = flow.RetryPendingCommit();

        Assert.That(retried.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(64));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId));
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>冷恢复的 Rest 只能升级冻结候选，成功存档后再冷启动保持等级与一次路径完成。</summary>
    [Test]
    public void RestUpgrade_FromColdPendingPersistsExactSuccessorAndRejectsDuplicate()
    {
        RunSaveDocument pendingDocument = CreateAuthoritativeNodeVisitPendingDocument(MapNodeKind.Rest);
        var saves = new RecordingRunSaveStore(pendingDocument);
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 212121u);
        flow.RefreshSaveAvailability();
        RunState pending = flow.ContinueSavedRun();
        RunNodeVisitId visitId = pending.PendingNodeVisit.Id;
        RunCardInstanceId selected = pending.PendingNodeVisit.RestPayload
            .UpgradeCandidateInstanceIds[0];

        RunSaveCommitResult result = flow.SettleRestUpgrade(visitId, selected);

        Assert.That(result.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId));
        Assert.That(store.Current.RunDeck.Cards.Single(card => card.InstanceId == selected).UpgradeLevel,
            Is.EqualTo(1));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(saves.CommitAttempts[0].PendingNodeVisit, Is.Null);
        Assert.That(scenes.LoadedAddresses, Is.Empty);
        Assert.Throws<InvalidOperationException>(() => flow.SettleRestUpgrade(visitId, selected));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));

        using var coldStore = new RunStateStore();
        var coldScenes = new RecordingSceneFlow();
        var coldFlow = CreateFlow(coldStore, coldScenes, saves, randomRootSeed: 222222u);
        coldFlow.RefreshSaveAvailability();
        RunState restored = coldFlow.ContinueSavedRun();

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(restored.PendingNodeVisit, Is.Null);
        Assert.That(restored.PathNodeIds, Is.EqualTo(store.Current.PathNodeIds));
        Assert.That(restored.RunDeck.Cards.Single(card => card.InstanceId == selected).UpgradeLevel,
            Is.EqualTo(1));
        Assert.That(coldScenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>Rest 进入后配置若失去下一等级，升级命令必须在首次 save write 前终审拒绝。</summary>
    [Test]
    public void RestUpgrade_WhenConfigurationDrifts_IsRejectedBeforeSaveWrite()
    {
        RunSaveDocument pendingDocument = CreateAuthoritativeNodeVisitPendingDocument(MapNodeKind.Rest);
        var saves = new RecordingRunSaveStore(pendingDocument);
        using var store = new RunStateStore();
        Tables currentTables = CreateTables();
        var flow = new RunFlowService(
            store,
            () => currentTables,
            new RecordingSceneFlow(),
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("23232323-4545-6767-8989-010101010101")),
                randomRootSeed: 232323u)),
            saves);
        flow.RefreshSaveAvailability();
        RunState pending = flow.ContinueSavedRun();
        RunCardInstanceId selected = pending.PendingNodeVisit.RestPayload
            .UpgradeCandidateInstanceIds[0];
        currentTables = CreateTables(includeEncounter: true, includeUpgrade: false);

        Assert.Throws<InvalidOperationException>(() => flow.SettleRestUpgrade(
            pending.PendingNodeVisit.Id,
            selected));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(saves.CommitAttempts, Is.Empty);
    }

    /// <summary>Rest 升级后继一旦冻结，保存失败期间配置漂移也只能重试同一文档而不得重新终审。</summary>
    [Test]
    public void RestUpgradeCommitFailure_ConfigurationDriftStillRetriesFrozenSuccessor()
    {
        RunSaveDocument pendingDocument = CreateAuthoritativeNodeVisitPendingDocument(MapNodeKind.Rest);
        var saves = new RecordingRunSaveStore(pendingDocument);
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "rest upgrade replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        using var store = new RunStateStore();
        Tables currentTables = CreateTables();
        var scenes = new RecordingSceneFlow();
        var flow = new RunFlowService(
            store,
            () => currentTables,
            scenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("24242424-4646-6868-9090-020202020202")),
                randomRootSeed: 242424u)),
            saves);
        flow.RefreshSaveAvailability();
        RunState pending = flow.ContinueSavedRun();
        RunCardInstanceId selected = pending.PendingNodeVisit.RestPayload
            .UpgradeCandidateInstanceIds[0];

        RunSaveCommitResult failed = flow.SettleRestUpgrade(
            pending.PendingNodeVisit.Id,
            selected);
        RunSaveDocument failedDocument = saves.CommitAttempts[0];
        currentTables = CreateTables(includeEncounter: true, includeUpgrade: false);

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.RunDeck.Cards.Single(card => card.InstanceId == selected).UpgradeLevel,
            Is.Zero);

        RunSaveCommitResult retried = flow.RetryPendingCommit();

        Assert.That(retried.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.RunDeck.Cards.Single(card => card.InstanceId == selected).UpgradeLevel,
            Is.EqualTo(1));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>宝箱领取必须先保存冻结后继，失败保留原 Pending，并以同一文档重试后才发布持有物与路径。</summary>
    [Test]
    public async Task ChestClaimCommitFailure_PreservesPendingAndRetriesExactSuccessorWithoutNavigation()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Chest,
            RunNodeVisitIdentityCatalog.ChestContentId);
        var holdings = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(4), templateId: 9002),
                new RunPotion(new RunPotionInstanceId(9), templateId: 9002),
            },
            gold: 100);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("25252525-4747-6969-9191-131313131313")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: holdings));
        var saves = new RecordingRunSaveStore(RunSaveDocumentMapper.Create(created));
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 252525u);
        await flow.EnterMapNodeAsync(MapNodeId.FromPosition(layer: 1, slot: 0));
        RunState pending = store.Current;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "chest claim replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());

        RunSaveCommitResult failed = flow.SettleChestClaim(pending.PendingNodeVisit.Id);
        RunSaveDocument failedDocument = saves.CommitAttempts[1];

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.Holdings.Potions.Select(potion => potion.InstanceId.Sequence),
            Is.EqualTo(new[] { 4, 9 }));
        Assert.That(failedDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(failedDocument.PendingNodeVisit, Is.Null);
        Assert.That(failedDocument.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(new[] { 4, 9, 10 }));
        Assert.That(failedDocument.Potions.Last().TemplateId,
            Is.EqualTo(RunNodeVisitIdentityCatalog.SamplePotionTemplateId));
        Assert.That(flow.CanRollbackFailedCheckpoint, Is.False);
        Assert.Throws<InvalidOperationException>(() =>
            flow.SettleChestSkip(pending.PendingNodeVisit.Id));
        Assert.Throws<InvalidOperationException>(() => flow.ExitPendingRunToMenu());
        Assert.That(scenes.LoadedAddresses, Is.Empty);

        RunSaveCommitResult retried = flow.RetryPendingCommit();

        Assert.That(retried.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.Holdings.Potions.Select(potion => potion.InstanceId.Sequence),
            Is.EqualTo(new[] { 4, 9, 10 }));
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(pending.PendingNodeVisit.NodeId));
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>冷启动恢复的宝箱可跳过且保持持有物，成功后路径只完成一次并拒绝重复结算。</summary>
    [Test]
    public void ChestSkip_FromColdPendingPersistsExactSuccessorAndRejectsDuplicate()
    {
        RunSaveDocument pendingDocument = CreateAuthoritativeNodeVisitPendingDocument(MapNodeKind.Chest);
        var saves = new RecordingRunSaveStore(pendingDocument);
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 262626u);
        flow.RefreshSaveAvailability();
        RunState pending = flow.ContinueSavedRun();
        RunNodeVisitId visitId = pending.PendingNodeVisit.Id;

        RunSaveCommitResult result = flow.SettleChestSkip(visitId);

        Assert.That(result.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.Holdings, Is.SameAs(pending.Holdings));
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(saves.CommitAttempts[0].PendingNodeVisit, Is.Null);
        Assert.Throws<InvalidOperationException>(() => flow.SettleChestSkip(visitId));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(scenes.LoadedAddresses, Is.Empty);

        using var coldStore = new RunStateStore();
        var coldFlow = CreateFlow(
            coldStore,
            new RecordingSceneFlow(),
            saves,
            randomRootSeed: 272727u);
        coldFlow.RefreshSaveAvailability();
        RunState restored = coldFlow.ContinueSavedRun();

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(restored.PendingNodeVisit, Is.Null);
        Assert.That(restored.PathNodeIds, Is.EqualTo(store.Current.PathNodeIds));
        Assert.That(restored.Holdings.Potions, Is.Empty);
    }

    /// <summary>宝箱伪造身份与满槽领取必须在首次结算写盘前拒绝，而满槽仍允许显式跳过。</summary>
    [Test]
    public async Task ChestClaim_ForgedIdentityAndFullCapacityAreRejectedBeforeSaveWhileSkipRemainsAvailable()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Chest,
            RunNodeVisitIdentityCatalog.ChestContentId);
        var fullHoldings = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 9002),
                new RunPotion(new RunPotionInstanceId(2), templateId: 9002),
                new RunPotion(new RunPotionInstanceId(3), templateId: 9002),
            },
            gold: 100);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("28282828-4848-7070-9292-141414141414")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: fullHoldings));
        var saves = new RecordingRunSaveStore(RunSaveDocumentMapper.Create(created));
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 282828u);
        await flow.EnterMapNodeAsync(MapNodeId.FromPosition(layer: 1, slot: 0));
        RunState pending = store.Current;
        int writesAfterEntry = saves.CommitAttempts.Count;

        Assert.Throws<InvalidOperationException>(() => flow.SettleChestClaim(
            new RunNodeVisitId(
                new RunId(Guid.Parse("29292929-4949-7171-9393-151515151515")),
                pending.PendingNodeVisit.NodeId)));
        Assert.Throws<InvalidOperationException>(() =>
            flow.SettleChestClaim(pending.PendingNodeVisit.Id));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(writesAfterEntry));

        RunSaveCommitResult skipped = flow.SettleChestSkip(pending.PendingNodeVisit.Id);

        Assert.That(skipped.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Current.Holdings.Potions, Has.Count.EqualTo(3));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>Shop 购买失败必须保留原 Pending 并 exact retry 同一后继；冷恢复后可连买且只在 Leave 完成路径。</summary>
    [Test]
    public void ShopPurchaseCommitFailure_RetriesExactPendingThenColdRestoresAndLeavesWithoutNavigation()
    {
        RunSaveDocument pendingDocument = CreateAuthoritativeNodeVisitPendingDocument(
            MapNodeKind.Shop);
        var saves = new RecordingRunSaveStore(pendingDocument);
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        Tables currentTables = CreateTables();
        var flow = new RunFlowService(
            store,
            () => currentTables,
            scenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("33333333-5555-7777-9999-212121212121")),
                randomRootSeed: 323232u)),
            saves);
        flow.RefreshSaveAvailability();
        RunState pending = flow.ContinueSavedRun();
        RunNodeVisitId visitId = pending.PendingNodeVisit.Id;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "shop card replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());

        RunSaveCommitResult failed = flow.SettleShopPurchase(visitId, stockEntryId: 3);
        RunSaveDocument failedDocument = saves.CommitAttempts[0];

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(100));
        Assert.That(store.Current.RunDeck.Cards,
            Has.Count.EqualTo(pending.RunDeck.Cards.Count));
        Assert.That(failedDocument.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(failedDocument.Gold, Is.EqualTo(50));
        Assert.That(failedDocument.RunCards,
            Has.Count.EqualTo(pending.RunDeck.Cards.Count + 1));
        Assert.That(failedDocument.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, false, true }));
        Assert.That(failedDocument.PathNodeIds, Is.EqualTo(pending.PathNodeIds
            .Select(nodeId => nodeId.Value)));
        Assert.That(flow.CanRollbackFailedCheckpoint, Is.False);
        Assert.Throws<InvalidOperationException>(() => flow.SettleShopPurchase(visitId, 2));
        Assert.Throws<InvalidOperationException>(() => flow.SettleShopLeave(visitId));
        Assert.Throws<InvalidOperationException>(() => flow.ExitPendingRunToMenu());
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(scenes.LoadedAddresses, Is.Empty);

        currentTables = CreateTables(
            includeEncounter: true,
            includeUpgrade: true,
            includeHero1001ShopCards: false);
        RunSaveCommitResult retried = flow.RetryPendingCommit();

        Assert.That(retried.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase,
            Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(store.Current.PendingNodeVisit.ShopPayload.Entries[2].Purchased, Is.True);
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(50));
        Assert.That(store.Current.PathNodeIds, Is.EqualTo(pending.PathNodeIds));
        Assert.That(scenes.LoadedAddresses, Is.Empty);

        currentTables = CreateTables();
        using var coldStore = new RunStateStore();
        var coldScenes = new RecordingSceneFlow();
        var coldFlow = new RunFlowService(
            coldStore,
            () => currentTables,
            coldScenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("34343434-5656-7878-aaaa-222222222222")),
                randomRootSeed: 343434u)),
            saves);
        coldFlow.RefreshSaveAvailability();
        RunState restored = coldFlow.ContinueSavedRun();

        Assert.That(restored.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, false, true }));
        Assert.That(restored.Holdings.Gold, Is.EqualTo(50));
        Assert.That(restored.RunDeck.Cards,
            Has.Count.EqualTo(pending.RunDeck.Cards.Count + 1));
        Assert.That(coldFlow.SettleShopPurchase(restored.PendingNodeVisit.Id, 2).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunState afterPotion = coldStore.Current;
        Assert.That(afterPotion.ProgressPhase,
            Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(afterPotion.Holdings.Gold, Is.EqualTo(25));
        Assert.That(afterPotion.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, true }));

        using var afterPotionColdStore = new RunStateStore();
        var afterPotionColdScenes = new RecordingSceneFlow();
        var afterPotionColdFlow = new RunFlowService(
            afterPotionColdStore,
            () => currentTables,
            afterPotionColdScenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("38383838-6060-7c7c-aeae-262626262626")),
                randomRootSeed: 363636u)),
            saves);
        afterPotionColdFlow.RefreshSaveAvailability();
        RunState restoredAfterPotion = afterPotionColdFlow.ContinueSavedRun();
        Assert.That(restoredAfterPotion.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, true }));
        Assert.That(restoredAfterPotion.Holdings.Gold, Is.EqualTo(25));
        Assert.That(restoredAfterPotion.Holdings.Potions.Single().InstanceId.Sequence,
            Is.EqualTo(1));
        Assert.That(restoredAfterPotion.RunDeck.Cards,
            Has.Count.EqualTo(pending.RunDeck.Cards.Count + 1));

        Assert.That(afterPotionColdFlow.SettleShopLeave(
                restoredAfterPotion.PendingNodeVisit.Id).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(afterPotionColdStore.Current.ProgressPhase,
            Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(afterPotionColdStore.Current.PendingNodeVisit, Is.Null);
        Assert.That(afterPotionColdStore.Current.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId));
        Assert.That(afterPotionColdStore.Current.Holdings.Gold, Is.EqualTo(25));
        Assert.That(coldScenes.LoadedAddresses, Is.Empty);
        Assert.That(afterPotionColdScenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>冻结卡仍全局存在但已移出当前 Hero 奖励池时，Shop Card 购买必须在首次 save write 前拒绝。</summary>
    [Test]
    public async Task ShopCardPurchase_HeroPoolDriftIsRejectedBeforeSaveWrite()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Shop,
            RunNodeVisitIdentityCatalog.ShopContentId);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("35353535-5757-7979-abab-232323232323")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 100)));
        var saves = new RecordingRunSaveStore(RunSaveDocumentMapper.Create(created));
        Tables currentTables = CreateTables();
        var flow = new RunFlowService(
            store,
            () => currentTables,
            new RecordingSceneFlow(),
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("36363636-5858-7a7a-acac-242424242424")),
                randomRootSeed: 353535u)),
            saves);
        await flow.EnterMapNodeAsync(MapNodeId.FromPosition(layer: 1, slot: 0));
        RunState pending = store.Current;
        int writesAfterEntry = saves.CommitAttempts.Count;
        currentTables = CreateTables(
            includeEncounter: true,
            includeUpgrade: true,
            includeHero1001ShopCards: false);

        Assert.Throws<InvalidOperationException>(() => flow.SettleShopPurchase(
            pending.PendingNodeVisit.Id,
            stockEntryId: 3));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(writesAfterEntry));
    }

    /// <summary>零购买 Leave 替换失败保持原 Shop，重试同文档只完成一次路径，完成后生产 Flow 拒绝再入。</summary>
    [Test]
    public async Task ShopLeaveWithoutPurchaseCommitFailure_RetriesExactDocumentAndRejectsReentry()
    {
        RunSaveDocument pendingDocument = CreateAuthoritativeNodeVisitPendingDocument(
            MapNodeKind.Shop);
        var saves = new RecordingRunSaveStore(pendingDocument);
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 363637u);
        flow.RefreshSaveAvailability();
        RunState pending = flow.ContinueSavedRun();
        RunNodeVisitId visitId = pending.PendingNodeVisit.Id;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "shop leave replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());

        RunSaveCommitResult failed = flow.SettleShopLeave(visitId);
        RunSaveDocument failedDocument = saves.CommitAttempts[0];

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, false, false }));
        Assert.That(failedDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(failedDocument.PendingNodeVisit, Is.Null);
        Assert.That(failedDocument.Gold, Is.EqualTo(pending.Holdings.Gold));
        Assert.That(failedDocument.PathNodeIds,
            Is.EqualTo(pending.PathNodeIds.Select(nodeId => nodeId.Value)
                .Concat(new[] { visitId.NodeId.Value })));
        Assert.That(flow.CanRollbackFailedCheckpoint, Is.False);

        RunSaveCommitResult retried = flow.RetryPendingCommit();

        Assert.That(retried.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(pending.Holdings.Gold));
        Assert.That(store.Current.PathNodeIds.Count, Is.EqualTo(pending.PathNodeIds.Count + 1));
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId));
        Assert.That(
            async () => await flow.EnterMapNodeAsync(visitId.NodeId),
            Throws.TypeOf<InvalidOperationException>());
        Assert.Throws<InvalidOperationException>(() => flow.SettleShopLeave(visitId));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>事件选择保存失败必须保留原 Pending、锁死两项与回退，并以同一文档重试后支持冷恢复。</summary>
    [Test]
    public void EventChoiceCommitFailure_RetriesExactDocumentThenColdRestoresWithoutNavigation()
    {
        RunSaveDocument pendingDocument = CreateAuthoritativeNodeVisitPendingDocument(
            MapNodeKind.Event);
        var saves = new RecordingRunSaveStore(pendingDocument);
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 373737u);
        flow.RefreshSaveAvailability();
        RunState pending = flow.ContinueSavedRun();
        RunNodeVisitId visitId = pending.PendingNodeVisit.Id;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "event choice replace failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());

        RunSaveCommitResult failed = flow.SettleEventChoice(
            visitId,
            RunEventChoiceKind.PaidHeal);
        RunSaveDocument failedDocument = saves.CommitAttempts[0];

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(61));
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(100));
        Assert.That(failedDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(failedDocument.PendingNodeVisit, Is.Null);
        Assert.That(failedDocument.CurrentHealth, Is.EqualTo(76));
        Assert.That(failedDocument.Gold, Is.EqualTo(75));
        Assert.That(failedDocument.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId.Value));
        Assert.That(flow.CanRollbackFailedCheckpoint, Is.False);
        Assert.Throws<InvalidOperationException>(() => flow.SettleEventChoice(
            visitId,
            RunEventChoiceKind.GainGold));
        Assert.Throws<InvalidOperationException>(() => flow.SettleEventChoice(
            visitId,
            RunEventChoiceKind.PaidHeal));
        Assert.Throws<InvalidOperationException>(() => flow.ExitPendingRunToMenu());
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(scenes.LoadedAddresses, Is.Empty);

        RunSaveCommitResult retried = flow.RetryPendingCommit();

        Assert.That(retried.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[1], Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(76));
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(75));
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId));
        Assert.Throws<InvalidOperationException>(() => flow.SettleEventChoice(
            visitId,
            RunEventChoiceKind.PaidHeal));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(2));
        Assert.That(scenes.LoadedAddresses, Is.Empty);

        using var coldStore = new RunStateStore();
        var coldScenes = new RecordingSceneFlow();
        var coldFlow = CreateFlow(coldStore, coldScenes, saves, randomRootSeed: 383838u);
        coldFlow.RefreshSaveAvailability();
        RunState restored = coldFlow.ContinueSavedRun();

        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(restored.PendingNodeVisit, Is.Null);
        Assert.That(restored.CurrentHealth, Is.EqualTo(76));
        Assert.That(restored.Holdings.Gold, Is.EqualTo(75));
        Assert.That(restored.PathNodeIds.Last(), Is.EqualTo(visitId.NodeId));
        Assert.That(coldScenes.LoadedAddresses, Is.Empty);
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

    /// <summary>冻结奖励存档失败保持 InBattle 来源，retry 同一文档成功后才发布 RewardPending 后继。</summary>
    [Test]
    public async Task ExitAfterFailedVictoryCheckpoint_IsRejectedAndPreservesRetryDocument()
    {
        using var store = new RunStateStore();
        var saves = new RecordingRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "checkpoint failed"));
        var flow = CreateFlow(store, new RecordingSceneFlow(), saves, randomRootSeed: 89012u);
        flow.CreateNewRun(heroTemplateId: 1002);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, 1002, health: 33, maxHealth: 90));

        RunState failedSource = store.Current;
        RunSaveDocument failedCheckpoint = saves.CommitAttempts[1];

        Assert.That(() => flow.ExitPendingRunToMenu(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(store.Current, Is.SameAs(failedSource));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));

        flow.RetryPendingCommit();

        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedCheckpoint));
        Assert.That(store.Current, Is.Not.SameAs(failedSource));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(store.Current.PendingCardReward, Is.Not.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.ContinueAvailable));
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
        Assert.That(created.MapDefinition.ProfileId,
            Is.EqualTo(TinySpireActMapProfiles.NewRunG6V1ProfileId));
        Assert.That(created.MapDefinition.GeneratorVersion,
            Is.EqualTo(ActMapGenerator.NewRunG6Version));
        Assert.That(
            created.MapDefinition.Nodes
                .Where(node => node.Kind != MapNodeKind.Start && node.Kind != MapNodeKind.Boss)
                .OrderBy(node => node.Layer)
                .Select(node => node.Kind),
            Is.EqualTo(new[]
            {
                MapNodeKind.Combat,
                MapNodeKind.Rest,
                MapNodeKind.Chest,
                MapNodeKind.Shop,
                MapNodeKind.Event,
                MapNodeKind.Combat,
            }));
        Assert.That(created.MapDefinition.Nodes.Any(node => node.Kind == MapNodeKind.Boss), Is.True);
        Assert.That(saves.SuccessfulDocument.MapSeed, Is.EqualTo(created.MapDefinition.MapSeed));
        Assert.That(saves.SuccessfulDocument.MapFingerprint, Is.EqualTo(created.MapDefinition.Fingerprint));
        Assert.That(input.BattleId.RunId, Is.EqualTo(created.RunId));
        Assert.That(input.BattleId.NodeId, Is.EqualTo(selectedNodeId));
        Assert.That(input.EncounterTemplateId, Is.EqualTo(selectedNode.ContentId));
        Assert.That(setup.HeroTemplateId, Is.EqualTo(input.HeroTemplateId));
        Assert.That(setup.EncounterTemplateId, Is.EqualTo(input.EncounterTemplateId));
        Assert.That(setup.PlayerInitialHealth, Is.EqualTo(input.InitialHealth));
        Assert.That(setup.DeckTemplateId, Is.Null);
        Assert.That(setup.RunCards, Is.EqualTo(input.RunCards));
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
        RunState created = CreateLegacyBossGateRun(store, randomRootSeed: 12345u);
        saves.Commit(RunSaveDocumentMapper.Create(created));

        int victories = await AdvanceFirstOrdinaryRouteToBossAsync(flow, store);

        Assert.That(victories, Is.EqualTo(2));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.BossGateReached));
        Assert.That(store.Current.PathNodeIds, Has.Count.EqualTo(4));
        Assert.That(store.Current.MapDefinition.GetNode(store.Current.CurrentNodeId).Kind, Is.EqualTo(MapNodeKind.Boss));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(6));
        Assert.That(
            saves.CommitAttempts.Select(document => document.ProgressPhase),
            Is.EqualTo(new[]
            {
                RunSaveProgressPhase.MapReady,
                RunSaveProgressPhase.RewardPending,
                RunSaveProgressPhase.MapReady,
                RunSaveProgressPhase.RewardPending,
                RunSaveProgressPhase.MapReady,
                RunSaveProgressPhase.BossGateReached,
            }));
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
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "terminal retry failed"));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var flow = CreateFlow(store, scenes, saves, randomRootSeed: 654321u);
        flow.CreateNewRun(heroTemplateId: 1001);
        await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Defeat, 1001, health: 0, maxHealth: 80));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(store.Current.ActiveBattle, Is.Not.Null);
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        RunSaveDocument failedTerminal = saves.CommitAttempts[1];
        Assert.That(failedTerminal.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(() => flow.ExitPendingRunToMenu(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle }));

        RunSaveCommitResult failedRetry = flow.RetryPendingCommit();

        Assert.That(failedRetry.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(saves.CommitAttempts[2], Is.SameAs(failedTerminal));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle }));

        RunSaveCommitResult successfulRetry = flow.RetryPendingCommit();

        Assert.That(successfulRetry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(saves.CommitAttempts[3], Is.SameAs(failedTerminal));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.TerminalDefeat));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
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

    /// <summary>沿正式 G6 mixed 普通路径补齐相邻非战斗节点，并恢复到下一 Combat 的稳定前驱。</summary>
    private static RunStateStore RestoreBeforeNextCombatAfterMixedRoute(RunState source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.ProgressPhase != RunProgressPhase.MapReady)
            throw new InvalidOperationException("A stable MapReady source is required.");

        var path = source.PathNodeIds.ToList();
        var traversedKinds = new List<MapNodeKind>();
        MapNodeId cursor = source.CurrentNodeId;
        int safetyLimit = source.MapDefinition.Nodes.Count;
        while (traversedKinds.Count < safetyLimit)
        {
            MapNodeId next = MapReachability.GetSelectableNodeIds(
                    source.MapDefinition,
                    cursor,
                    MapTraversalMode.Ordinary)
                .First();
            MapNode node = source.MapDefinition.GetNode(next);
            if (node.Kind == MapNodeKind.Combat)
                break;
            if (node.Kind == MapNodeKind.Boss)
                throw new InvalidOperationException("The mixed route reached Boss before another Combat.");

            path.Add(next);
            traversedKinds.Add(node.Kind);
            cursor = next;
        }

        Assert.That(traversedKinds, Is.EqualTo(new[]
        {
            MapNodeKind.Rest,
            MapNodeKind.Chest,
            MapNodeKind.Shop,
            MapNodeKind.Event,
        }));
        int restHealAmount = (int)Math.Ceiling(source.MaxHealth * 0.3m);
        int restoredHealth = (int)Math.Min(
            source.MaxHealth,
            (long)source.CurrentHealth + restHealAmount);
        var restoredStore = new RunStateStore();
        restoredStore.RestoreRun(new RunRestoreOptions(
            source.RunId,
            source.HeroTemplateId,
            restoredHealth,
            source.MaxHealth,
            source.RunDeck,
            source.RandomRootSeed,
            source.MapDefinition,
            path,
            RunProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            source.Holdings));
        return restoredStore;
    }

    /// <summary>只从当前普通直接出边中选择 Combat，避免把 Rest 等 Pending 当作战斗。</summary>
    private static MapNodeId GetFirstSelectableCombatNodeId(RunState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        return MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary)
            .First(nodeId => state.MapDefinition.GetNode(nodeId).Kind == MapNodeKind.Combat);
    }

    /// <summary>建立只含 Start 与一个直接可达非战斗节点的最小 Flow 测试地图。</summary>
    private static MapDefinition CreateSingleNonCombatMap(
        MapNodeKind kind,
        int contentId)
    {
        MapNodeId startNodeId = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId destinationNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        return new MapDefinition(
            profileId: "tinyspire.test.flow.noncombat.v1",
            generatorVersion: 1,
            mapSeed: 42420002u,
            nodes: new[]
            {
                new MapNode(startNodeId, layer: 0, slot: 0, MapNodeKind.Start, contentId: 0),
                new MapNode(destinationNodeId, layer: 1, slot: 0, kind, contentId),
            },
            edges: new[]
            {
                new MapEdge(startNodeId, destinationNodeId),
            });
    }

    /// <summary>以 legacy G3 v1 直接建立仍可独立验证假 BossGate 的旧路线夹具。</summary>
    private static RunState CreateLegacyBossGateRun(
        RunStateStore store,
        uint randomRootSeed)
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.LegacyG3V1,
            RunRandomDomains.DeriveMapSeed(randomRootSeed));
        return store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("21212121-3434-5656-7878-909090909090")),
            heroTemplateId: 1002,
            initialHealth: 90,
            maxHealth: 90,
            runDeck: RunDeck.CreateInitial(new[] { 3201 }),
            randomRootSeed,
            map,
            RunHoldings.Empty(initialGold: 100)));
    }

    /// <summary>沿 G6 mixed 路径恢复到目标前一层，并由生产 entry 工厂建立冷启动测试文档。</summary>
    private static RunSaveDocument CreateAuthoritativeNodeVisitPendingDocument(
        MapNodeKind kind)
    {
        ActMapProfile profile = TinySpireActMapProfiles.NewRunG6V1;
        MapDefinition map = ActMapGenerator.Generate(profile, mapSeed: 24681357u);
        MapNode target = map.Nodes.Single(node => node.Kind == kind);
        MapNodeId[] path = map.Nodes
            .Where(node => node.Slot == 0 && node.Layer < target.Layer)
            .OrderBy(node => node.Layer)
            .Select(node => node.Id)
            .ToArray();
        string runId = kind switch
        {
            MapNodeKind.Rest => "31313131-4242-5353-6464-757575757571",
            MapNodeKind.Chest => "31313131-4242-5353-6464-757575757572",
            MapNodeKind.Shop => "31313131-4242-5353-6464-757575757573",
            MapNodeKind.Event => "31313131-4242-5353-6464-757575757574",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        using var store = new RunStateStore();
        store.RestoreRun(new RunRestoreOptions(
            new RunId(Guid.Parse(runId)),
            heroTemplateId: 1001,
            currentHealth: 61,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002, 3123 }),
            randomRootSeed: 123456u,
            map,
            path,
            RunProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            holdings: RunHoldings.Empty(initialGold: 100)));
        RunNodeVisitEntrySettlement settlement = store.PreviewNodeVisitEntry(
            target.Id,
            new TablesRunSaveConfigurationCatalog(CreateTables()));
        return RunSaveDocumentMapper.Create(settlement.Successor);
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
            flow.SettleCardReward(
                store.Current.PendingCardReward.Id,
                selectedCardTemplateId: null);
            victories++;
        }

        throw new InvalidOperationException("The deterministic test route did not reach a Boss gate.");
    }

    /// <summary>冻结一个单玩家 BattleResult，模拟表现屏障后的唯一公开结果。</summary>
    private static BattleResult CreateBattleResult(
        BattleResultKind kind,
        int heroTemplateId,
        int health,
        int maxHealth,
        IEnumerable<RunPotionInstanceId> consumedPotionInstanceIds = null)
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
            },
            consumedPotionInstanceIds);
    }

    /// <summary>创建一份可覆盖配置引用、RunCards、冻结奖励与稳定阶段的 schema v4 文档。</summary>
    private static RunSaveDocument CreateDocument(
        int heroTemplateId = 1001,
        int deckTemplateId = 1001,
        uint randomRootSeed = 112233u,
        RunSaveProgressPhase progressPhase = RunSaveProgressPhase.MapReady,
        int completedCombatCount = 0,
        string mapProfileId = null,
        IReadOnlyList<RunSaveCardDocument> runCards = null,
        IReadOnlyList<RunSaveRelicDocument> relics = null)
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
        bool rewardPending = progressPhase == RunSaveProgressPhase.RewardPending;
        MapNodeId currentNodeId = new MapNodeId(pathNodeIds[pathNodeIds.Length - 1]);
        string committedNodeId = terminal || rewardPending
            ? MapReachability.GetSelectableNodeIds(
                    map,
                    currentNodeId,
                    MapTraversalMode.Ordinary)
                .First()
                .Value
            : null;
        int maxHealth = heroTemplateId == 1002 ? 90 : 80;
        int[] rewardCandidates = heroTemplateId == 1002
            ? new[] { 3206, 3227, 3264 }
            : new[] { 3105, 3123, 3157 };
        const string runId = "0f0e0d0c-0b0a-0908-0706-050403020100";
        RunSavePendingCardRewardDocument pendingCardReward = rewardPending
            ? new RunSavePendingCardRewardDocument(
                $"{Guid.ParseExact(runId, "D"):N}:{completedCombatCount + 1}:{committedNodeId}",
                rewardCandidates)
            : null;

        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            runId,
            heroTemplateId,
            currentHealth: terminal ? 0 : maxHealth,
            maxHealth,
            runCards,
            legacyDeckTemplateId: runCards == null ? deckTemplateId : (int?)null,
            randomRootSeed,
            mapProfileId ?? map.ProfileId,
            map.GeneratorVersion,
            map.MapSeed,
            map.Fingerprint,
            pathNodeIds,
            progressPhase,
            committedNodeId,
            terminal ? RunSaveTerminalReason.Defeat : (RunSaveTerminalReason?)null,
            pendingCardReward,
            relics: relics ?? Array.Empty<RunSaveRelicDocument>(),
            potions: Array.Empty<RunSavePotionDocument>(),
            gold: 100,
            pendingNodeVisit: null);
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
    private static Tables CreateTables(
        bool includeEncounter,
        bool includeUpgrade = true,
        bool includeHero1001ShopCards = true)
    {
        string hero1001RewardCards = includeHero1001ShopCards
            ? "[3105,3123,3157]"
            : "[3206,3227,3264]";
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0,\"reward_card_template_ids\":" + hero1001RewardCards + ",\"reward_common_weight\":60,\"reward_uncommon_weight\":37,\"reward_rare_weight\":3}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.machine_gunner.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":3,\"max_ammo\":6,\"ammo_gain_per_round\":1,\"runtime_profile\":1,\"reward_card_template_ids\":[3206,3227,3264],\"reward_common_weight\":60,\"reward_uncommon_weight\":37,\"reward_rare_weight\":3}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002]},{\"id\":1002,\"card_template_ids\":[3003]}]"),
            ["battle_tbcard"] = new JArray(
                CreateTestCardRow(3002, rarity: 0),
                CreateTestCardRow(3003, rarity: 0),
                CreateTestCardRow(3105, rarity: 1),
                CreateTestCardRow(3123, rarity: 2),
                CreateTestCardRow(3157, rarity: 3),
                CreateTestCardRow(3206, rarity: 1),
                CreateTestCardRow(3227, rarity: 2),
                CreateTestCardRow(3263, rarity: 1),
                CreateTestCardRow(3264, rarity: 3)),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6}]"),
            ["battle_tbcardupgradelevel"] = includeUpgrade
                ? JArray.Parse(
                    "[{\"card_id\":3002,\"next_upgrade_level\":1," +
                    "\"description_i18n_key\":\"battle.card.3002.upgrade_description\"," +
                    "\"cost\":1,\"play_destination\":0,\"rule_kind\":1,\"rule_value\":9}]")
                : new JArray(),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test.name\",\"view_prefab_key\":\"pfb_char_enemy\",\"max_health\":20,\"base_strength\":0,\"behavior_group_id\":6001}]"),
            ["battle_tbencounter"] = includeEncounter
                ? JArray.Parse("[{\"id\":5001,\"enemy_template_ids\":[2001]}]")
                : new JArray(),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4002,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":0}]"),
            ["run_tbrelic"] = JArray.Parse(
                "[{\"id\":8001,\"name_i18n_key\":\"run.relic.test_8001.name\",\"description_i18n_key\":\"run.relic.test_8001.description\",\"battle_start_strength\":1}," +
                "{\"id\":8002,\"name_i18n_key\":\"run.relic.test_8002.name\",\"description_i18n_key\":\"run.relic.test_8002.description\",\"battle_start_strength\":2}]"),
            ["run_tbpotion"] = JArray.Parse(
                "[{\"id\":9001,\"name_i18n_key\":\"run.potion.test_9001.name\",\"description_i18n_key\":\"run.potion.test_9001.description\",\"heal_amount\":10}," +
                "{\"id\":9002,\"name_i18n_key\":\"run.potion.test_9002.name\",\"description_i18n_key\":\"run.potion.test_9002.description\",\"heal_amount\":25}]"),
        };

        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>创建 Flow 测试所需的最小 Implemented 卡牌配置行。</summary>
    private static JObject CreateTestCardRow(int templateId, int rarity)
    {
        bool finiteDamage = templateId == 3002;
        bool infiniteDamage = templateId == 3123;
        bool machinegunBurst = templateId == 3263;
        return new JObject
        {
            ["id"] = templateId,
            ["external_key"] = $"TEST_RUN_FLOW_{templateId}",
            ["catalog_snapshot_key"] = "test-fixture",
            ["name_i18n_key"] = $"battle.card.{templateId}.name",
            ["description_i18n_key"] = $"battle.card.{templateId}.description",
            ["upgraded_description_i18n_key"] = $"battle.card.{templateId}.description",
            ["card_type"] = 0,
            ["rarity"] = rarity,
            ["cost"] = 1,
            ["cost_kind"] = 0,
            ["upgraded_cost"] = 1,
            ["target_rule"] = machinegunBurst ? 3 : 1,
            ["play_destination"] = machinegunBurst ? 1 : 0,
            ["upgraded_play_destination"] = machinegunBurst ? 1 : 0,
            ["has_upgrade"] = false,
            ["implementation_status"] = 0,
            ["effect_bindings"] = finiteDamage || infiniteDamage
                ? new JArray(new JObject
                {
                    ["argument_key"] = "damage",
                    ["effect_id"] = 4002,
                })
                : new JArray(),
            ["illustration_key"] = string.Empty,
            ["program_id"] = machinegunBurst ? 63 : 0,
            ["is_innate"] = false,
            ["upgrade_track_kind"] = finiteDamage ? 1 : infiniteDamage ? 2 : 0,
            ["infinite_upgrade_rule_kind"] = infiniteDamage ? 1 : 0,
            ["infinite_upgrade_value_per_level"] = infiniteDamage ? 10 : 0,
        };
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
