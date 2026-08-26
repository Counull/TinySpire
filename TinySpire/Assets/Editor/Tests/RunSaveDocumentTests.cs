using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class RunSaveDocumentTests
{
    /// <summary>RewardPending 必须连同稳定身份、候选顺序、生命与未完成节点完成冷恢复。</summary>
    [Test]
    public void RewardPending_JsonRoundTrip_PreservesFrozenRewardAndAttempt()
    {
        using RunStateStore sourceStore = CreateStore(
            "01010101-0202-0303-0404-050505050505",
            mapSeed: 135791357u);
        MapNodeId nodeId = FirstSelectableNodeId(sourceStore.Current);
        sourceStore.CommitNode(nodeId);
        RunBattleInput battle = sourceStore.BeginCommittedBattle();
        RunState source = sourceStore.RecordVictoryAndFreezeReward(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth: 43,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));

        RunSaveDocument document = RunSaveDocumentMapper.Create(source);
        string json = RunSaveDocumentCodec.Serialize(document);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            read.Document,
            new ExistingConfigurationCatalog());
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);
        JObject raw = JObject.Parse(json);

        Assert.That(raw.Value<int>("schemaVersion"), Is.EqualTo(4));
        Assert.That(raw.Value<string>("progressPhase"), Is.EqualTo("RewardPending"));
        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(restored.CurrentHealth, Is.EqualTo(43));
        Assert.That(restored.PathNodeIds, Is.EqualTo(source.PathNodeIds));
        Assert.That(restored.CommittedNodeId, Is.EqualTo(nodeId));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(restored.PendingCardReward.Id, Is.EqualTo(source.PendingCardReward.Id));
        Assert.That(restored.PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(new[] { 3105, 3123, 3157 }));
    }

    /// <summary>冷恢复必须拒绝不属于当前 Hero 明确奖励池的伪造候选，即使该 Card 模板存在。</summary>
    [Test]
    public void CreateRestore_RewardPendingWithForeignCandidate_ReturnsInvalidDocument()
    {
        using RunStateStore store = CreateStore(
            "02020202-0303-0404-0505-060606060606",
            mapSeed: 24682468u);
        MapNodeId nodeId = FirstSelectableNodeId(store.Current);
        store.CommitNode(nodeId);
        RunBattleInput battle = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth: 55,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));
        RunSaveDocument source = RunSaveDocumentMapper.Create(pending);
        RunSaveDocument tampered = CopyDocument(
            source,
            pendingCardReward: new RunSavePendingCardRewardDocument(
                source.PendingCardReward.RewardId,
                new[] { 3002, 3123, 3157 }));

        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            tampered,
            new ExistingConfigurationCatalog());

        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(restore.Options, Is.Null);
        Assert.That(restore.Detail, Does.Contain("reward pool").IgnoreCase);
    }

    /// <summary>地图稳定态只保存重建配方与路径，并由配方恢复出相同指纹的完整冻结地图。</summary>
    [Test]
    public void MapReady_RecipeRoundTrip_RebuildsSameFingerprintAndPath()
    {
        using RunStateStore sourceStore = CreateStore(
            "11111111-2222-3333-4444-555555555555",
            mapSeed: 123456789u);
        CompleteFirstSelectableCombat(sourceStore, settledHealth: 51);
        RunState source = CompleteFirstSelectableCombat(sourceStore, settledHealth: 37);

        RunSaveDocument sourceDocument = RunSaveDocumentMapper.Create(source);
        string json = RunSaveDocumentCodec.Serialize(sourceDocument);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            read.Document,
            new ExistingConfigurationCatalog());
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restored.MapDefinition, Is.Not.SameAs(source.MapDefinition));
        Assert.That(restored.MapDefinition.Fingerprint, Is.EqualTo(source.MapDefinition.Fingerprint));
        Assert.That(restored.MapDefinition.Nodes.Count, Is.EqualTo(source.MapDefinition.Nodes.Count));
        Assert.That(restored.MapDefinition.Edges.Count, Is.EqualTo(source.MapDefinition.Edges.Count));
        Assert.That(
            restored.PathNodeIds.Select(nodeId => nodeId.Value),
            Is.EqualTo(source.PathNodeIds.Select(nodeId => nodeId.Value)));
        Assert.That(restored.CurrentNodeId, Is.EqualTo(source.CurrentNodeId));
        Assert.That(restored.CurrentHealth, Is.EqualTo(37));
        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(2));
        Assert.That(restored.ActiveBattle, Is.Null);
    }

    /// <summary>当前 schema 只保存有序 RunCard 与稳定地图事实，不再把初始牌组模板作为牌组权威。</summary>
    [Test]
    public void MapReady_JsonRoundTrip_ContainsCanonicalRunDeckAndNoLegacyTemplate()
    {
        using RunStateStore store = CreateStore(
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            mapSeed: 246813579u);
        RunState completed = CompleteFirstSelectableCombat(store, settledHealth: 17);

        RunSaveDocument expected = RunSaveDocumentMapper.Create(completed);
        string json = RunSaveDocumentCodec.Serialize(expected);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        JObject raw = JObject.Parse(json);

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(raw.Value<int>("schemaVersion"), Is.EqualTo(RunSaveDocument.CurrentSchemaVersion));
        Assert.That(read.Document.MapFingerprint, Is.EqualTo(expected.MapFingerprint));
        Assert.That(read.Document.PathNodeIds, Is.EqualTo(expected.PathNodeIds));
        Assert.That(
            raw.Properties().Select(property => property.Name),
            Is.EquivalentTo(new[]
            {
                "schemaVersion",
                "runId",
                "heroTemplateId",
                "currentHealth",
                "maxHealth",
                "runCards",
                "randomRootSeed",
                "mapProfileId",
                "mapGeneratorVersion",
                "mapSeed",
                "mapFingerprint",
                "pathNodeIds",
                "progressPhase",
                "committedNodeId",
                "terminalReason",
                "pendingCardReward",
            }));
        Assert.That(
            raw["runCards"]?.Select(card => card.Value<int>("instanceId")),
            Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(
            raw["runCards"]?.Select(card => card.Value<int>("templateId")),
            Is.EqualTo(new[] { 3002, 3002, 3003 }));
        Assert.That(
            raw["runCards"]?.Select(card => card.Value<int>("upgradeLevel")),
            Is.EqualTo(new[] { 0, 0, 0 }));
        Assert.That(raw["pendingCardReward"]?.Type, Is.EqualTo(JTokenType.Null));
        Assert.That(raw["deckTemplateId"], Is.Null);
        Assert.That(json, Does.Not.Contain("\"nodes\"").IgnoreCase);
        Assert.That(json, Does.Not.Contain("\"edges\"").IgnoreCase);
        Assert.That(json, Does.Not.Contain("MapDefinition").IgnoreCase);
        Assert.That(json, Does.Not.Contain("selectableNodeIds").IgnoreCase);
        Assert.That(json, Does.Not.Contain("reachableBossIds").IgnoreCase);
        Assert.That(json, Does.Not.Contain("downstream").IgnoreCase);
        Assert.That(json, Does.Not.Contain("ActiveBattle").IgnoreCase);
        Assert.That(json, Does.Not.Contain("battleAttemptSequence").IgnoreCase);
        Assert.That(json, Does.Not.Contain("uiState").IgnoreCase);
    }

    /// <summary>实例级升级只改变指定副本，并在 JSON 冷恢复后保留身份、顺序与等级。</summary>
    [Test]
    public void MapReady_UpgradedSpecificInstanceRoundTrip_PreservesIdentityOrderAndLevel()
    {
        using RunStateStore sourceStore = CreateStore(
            "abababab-cdcd-efef-0101-232323232323",
            mapSeed: 975319753u);
        var catalog = new ExistingConfigurationCatalog();

        RunState upgraded = sourceStore.CommitCardUpgrade(
            new RunCardInstanceId(2),
            catalog);
        RunSaveDocument document = RunSaveDocumentMapper.Create(upgraded);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
            RunSaveDocumentCodec.Serialize(document));
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(read.Document, catalog);
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);

        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.TemplateId),
            Is.EqualTo(new[] { 3002, 3002, 3003 }));
        Assert.That(
            restored.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 0, 1, 0 }));
    }

    /// <summary>有限轨道升满后的下一等级与伪造存档都被同一配置合法性拒绝。</summary>
    [Test]
    public void FiniteUpgradeBeyondConfiguredLevel_IsRejectedWithoutPublishingOrRestore()
    {
        using RunStateStore store = CreateStore(
            "bcbcbcbc-dede-f0f0-1212-343434343434",
            mapSeed: 864208642u);
        var catalog = new ExistingConfigurationCatalog();
        store.CommitCardUpgrade(new RunCardInstanceId(1), catalog);
        RunState beforeRejectedCommand = store.Current;

        Assert.Throws<InvalidOperationException>(() => store.CommitCardUpgrade(
            new RunCardInstanceId(1),
            catalog));
        Assert.That(store.Current, Is.SameAs(beforeRejectedCommand));

        RunSaveDocument source = RunSaveDocumentMapper.Create(store.Current);
        RunSaveCardDocument[] forgedCards = source.RunCards
            .Select(card => card.InstanceId == 1
                ? new RunSaveCardDocument(card.InstanceId, card.TemplateId, upgradeLevel: 2)
                : card)
            .ToArray();
        RunSaveDocument forged = CopyDocument(source, runCards: forgedCards);

        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(forged, catalog);

        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(restore.Options, Is.Null);
        Assert.That(restore.Detail, Does.Contain("upgrade level").IgnoreCase);
    }

    /// <summary>canonical 保存与恢复必须保留稳定实例身份、同模板副本顺序及合法有限/无限等级。</summary>
    [Test]
    public void CanonicalRunDeck_RoundTrip_PreservesInstanceOrderAndUpgradeFacts()
    {
        MapDefinition map = ActMapGenerator.Generate(TinySpireActMapProfiles.Current, 424242u);
        var deck = new RunDeck(new[]
        {
            new RunCard(new RunCardInstanceId(17), templateId: 3002, upgradeLevel: 1),
            new RunCard(new RunCardInstanceId(29), templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(61), templateId: 3123, upgradeLevel: 2),
        });
        using var sourceStore = new RunStateStore();
        sourceStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("17171717-2929-6161-7171-292961617171")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: deck,
            randomRootSeed: 424242u,
            map));

        string json = RunSaveDocumentCodec.Serialize(
            RunSaveDocumentMapper.Create(sourceStore.Current));
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            read.Document,
            new ExistingConfigurationCatalog());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(
            restore.Options.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 17, 29, 61 }));
        Assert.That(
            restore.Options.RunDeck.Cards.Select(card => card.TemplateId),
            Is.EqualTo(new[] { 3002, 3002, 3123 }));
        Assert.That(
            restore.Options.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 1, 0, 2 }));
    }

    /// <summary>无限轨道必须经两次实例命令升至二级，只改目标副本并完整穿过保存恢复。</summary>
    [Test]
    public void InfiniteUpgrade_TwoCommandsOnlyChangeTargetAndRoundTrip()
    {
        MapDefinition map = ActMapGenerator.Generate(TinySpireActMapProfiles.Current, 434343u);
        var deck = new RunDeck(new[]
        {
            new RunCard(new RunCardInstanceId(17), templateId: 3123, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(29), templateId: 3123, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(61), templateId: 3002, upgradeLevel: 0),
        });
        var catalog = new ExistingConfigurationCatalog();
        using var sourceStore = new RunStateStore();
        sourceStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("18181818-2929-6161-7171-292961617171")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: deck,
            randomRootSeed: 434343u,
            map));

        RunState levelOne = sourceStore.CommitCardUpgrade(
            new RunCardInstanceId(17),
            catalog);
        RunState levelTwo = sourceStore.CommitCardUpgrade(
            new RunCardInstanceId(17),
            catalog);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
            RunSaveDocumentCodec.Serialize(RunSaveDocumentMapper.Create(levelTwo)));
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(read.Document, catalog);
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);

        Assert.That(levelOne.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 1, 0, 0 }));
        Assert.That(levelTwo.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 2, 0, 0 }));
        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restored.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 17, 29, 61 }));
        Assert.That(restored.RunDeck.Cards.Select(card => card.TemplateId),
            Is.EqualTo(new[] { 3123, 3123, 3002 }));
        Assert.That(restored.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 2, 0, 0 }));
    }

    /// <summary>无重试后 attempt 是路径派生值，当前 schema 必须拒绝外部夹带第二份事实。</summary>
    [Test]
    public void Read_WhenDocumentContainsDerivedAttemptSequence_ReturnsInvalidDocument()
    {
        RunSaveDocument document = CreateInitialDocument(
            "acacacac-bdbd-cece-dfdf-e0e0e0e0e0e0");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(document));
        raw["battleAttemptSequence"] = 99;

        RunSaveDocumentReadResult result = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(result.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
    }

    /// <summary>外部档案夹带整图或 UI 派生字段时必须被严格白名单拒绝，而不是静默忽略。</summary>
    [Test]
    public void Read_WhenDocumentContainsWholeMapOrDerivedField_ReturnsInvalidDocument()
    {
        RunSaveDocument document = CreateInitialDocument(
            "abababab-cdcd-efef-0101-232323232323");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(document));
        raw["nodes"] = new JArray();
        raw["selectableNodeIds"] = new JArray();

        RunSaveDocumentReadResult result = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(result.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
    }

    /// <summary>抵达 Boss 门是可保存稳定态，冷恢复后保留完整路径且不会伪造真实 Boss 战。</summary>
    [Test]
    public void BossGateReached_RoundTrip_RestoresStableGateWithoutBattle()
    {
        using RunStateStore sourceStore = CreateStore(
            "22222222-3333-4444-5555-666666666666",
            mapSeed: 987654321u);
        RunState bossGate = ReachFirstBossGate(sourceStore);

        RunSaveDocument document = RunSaveDocumentMapper.Create(bossGate);
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            document,
            new ExistingConfigurationCatalog());
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);

        Assert.That(document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.BossGateReached));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.BossGateReached));
        Assert.That(restored.MapDefinition.GetNode(restored.CurrentNodeId).Kind, Is.EqualTo(MapNodeKind.Boss));
        Assert.That(restored.PathNodeIds.Select(value => value.Value),
            Is.EqualTo(bossGate.PathNodeIds.Select(value => value.Value)));
        Assert.That(restored.CommittedNodeId, Is.Null);
        Assert.That(restored.ActiveBattle, Is.Null);
        Assert.That(restored.TerminalReason, Is.Null);
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(2));
    }

    /// <summary>普通战斗失败作为 Terminal(Defeat) 原子持久化，冷恢复仍停留失败终局且不能重试。</summary>
    [Test]
    public void TerminalDefeat_RoundTrip_RestoresFailedNodeAndTerminalReason()
    {
        using RunStateStore sourceStore = CreateStore(
            "33333333-4444-5555-6666-777777777777",
            mapSeed: 135792468u);
        MapNodeId failedNodeId = FirstSelectableNodeId(sourceStore.Current);
        sourceStore.CommitNode(failedNodeId);
        RunBattleInput battle = sourceStore.BeginCommittedBattle();
        RunState defeated = sourceStore.RecordDefeat(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80);

        RunSaveDocument document = RunSaveDocumentMapper.Create(defeated);
        string json = RunSaveDocumentCodec.Serialize(document);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            read.Document,
            new ExistingConfigurationCatalog());
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);

        Assert.That(document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(document.TerminalReason, Is.EqualTo(RunSaveTerminalReason.Defeat));
        Assert.That(document.CommittedNodeId, Is.EqualTo(failedNodeId.Value));
        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(restored.TerminalReason, Is.EqualTo(RunTerminalReason.Defeat));
        Assert.That(restored.CurrentHealth, Is.Zero);
        Assert.That(restored.CommittedNodeId, Is.EqualTo(failedNodeId));
        Assert.That(restored.PathNodeIds.Count, Is.EqualTo(1));
        Assert.That(restored.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(restored.ActiveBattle, Is.Null);
        Assert.Throws<InvalidOperationException>(() => restoredStore.BeginCommittedBattle());
    }

    /// <summary>指纹漂移必须拒绝读档，禁止用当前生成结果静默替换原地图。</summary>
    [Test]
    public void CreateRestore_FingerprintDrift_ReturnsInvalidDocument()
    {
        RunSaveDocument source = CreateInitialDocument("44444444-5555-6666-7777-888888888888");
        RunSaveDocument drifted = CopyDocument(
            source,
            mapFingerprint: new string('0', 64));

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            drifted,
            new ExistingConfigurationCatalog());

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("fingerprint").IgnoreCase);
    }

    /// <summary>存档路径跳过相邻层或并非冻结边时必须 fail-fast，禁止恢复出伪造进度。</summary>
    [Test]
    public void CreateRestore_PathDrift_ReturnsInvalidDocument()
    {
        RunSaveDocument source = CreateInitialDocument(
            "45454545-5656-6767-7878-898989898989");
        string startNodeId = MapNodeId.FromPosition(layer: 0, slot: 0).Value;
        string skippedLayerNodeId = MapNodeId.FromPosition(layer: 2, slot: 0).Value;
        RunSaveDocument drifted = CopyDocument(
            source,
            pathNodeIds: new[] { startNodeId, skippedLayerNodeId });

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            drifted,
            new ExistingConfigurationCatalog());

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("progress").IgnoreCase);
    }

    /// <summary>生成器版本漂移必须类型化失败，禁止跨算法版本猜测重建。</summary>
    [Test]
    public void CreateRestore_GeneratorVersionDrift_ReturnsInvalidDocument()
    {
        RunSaveDocument source = CreateInitialDocument("55555555-6666-7777-8888-999999999999");
        RunSaveDocument drifted = CopyDocument(
            source,
            mapGeneratorVersion: ActMapGenerator.CurrentVersion + 1);

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            drifted,
            new ExistingConfigurationCatalog());

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("generator version").IgnoreCase);
    }

    /// <summary>存档引用的 profile ID 不存在时返回专用失败类别，不回退到当前默认 profile。</summary>
    [Test]
    public void CreateRestore_MissingProfile_ReturnsMissingMapProfile()
    {
        RunSaveDocument source = CreateInitialDocument("66666666-7777-8888-9999-aaaaaaaaaaaa");
        RunSaveDocument drifted = CopyDocument(source, mapProfileId: "missing.act.profile");

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            drifted,
            new ExistingConfigurationCatalog());

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.MissingMapProfile));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("profile").IgnoreCase);
    }

    /// <summary>同 ID 的当前 profile 内容发生漂移时也会由指纹契约拒绝，不能静默重掷。</summary>
    [Test]
    public void CreateRestore_ProfileContentDrift_ReturnsInvalidDocument()
    {
        RunSaveDocument source = CreateInitialDocument("77777777-8888-9999-aaaa-bbbbbbbbbbbb");
        var changedProfile = new ActMapProfile(
            TinySpireActMapProfiles.CurrentProfileId,
            normalLayerSlotCounts: new[] { 1, 2 },
            encounterIds: new[] { 5001 },
            enabledBossIds: new[] { 9001, 9002, 9003 },
            bossCandidateCount: 2,
            bossEndpointCount: 3);

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            source,
            new ExistingConfigurationCatalog(profile: changedProfile));

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("fingerprint").IgnoreCase);
    }

    /// <summary>Hero、Deck、Encounter 任一当前配置引用消失时，恢复必须返回对应类型化失败。</summary>
    [TestCase(MissingConfiguration.Hero, RunSaveRestoreStatus.MissingHeroTemplate)]
    [TestCase(MissingConfiguration.Deck, RunSaveRestoreStatus.MissingDeckTemplate)]
    [TestCase(MissingConfiguration.Encounter, RunSaveRestoreStatus.MissingEncounterTemplate)]
    public void CreateRestore_MissingConfiguration_ReturnsTypedFailure(
        MissingConfiguration missing,
        RunSaveRestoreStatus expectedStatus)
    {
        RunSaveDocument document = CreateInitialDocument(
            "88888888-9999-aaaa-bbbb-cccccccccccc");
        if (missing == MissingConfiguration.Deck)
            document = CopyAsLegacyFallback(document, deckTemplateId: 1001);

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            document,
            new SelectiveConfigurationCatalog(missing));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
    }

    /// <summary>旧 Deck 仍引用已删除 Card 时必须在冷读档阶段拒绝，不能把坏实例投影延迟到 Battle。</summary>
    [Test]
    public void CreateRestore_LegacyDeckWithMissingCardTemplate_ReturnsInvalidDocument()
    {
        RunSaveDocument document = CopyAsLegacyFallback(
            CreateInitialDocument("89898989-9a9a-abab-bcbc-cdcdcdcdcdcd"),
            deckTemplateId: 1001);

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            document,
            new ExistingConfigurationCatalog(missingCardTemplateId: 3003));

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("3003"));
    }

    /// <summary>Hero 最大生命配置漂移时必须在冷读档阶段拒绝，不能延迟到 Battle。</summary>
    [Test]
    public void CreateRestore_HeroMaxHealthDrift_ReturnsInvalidDocument()
    {
        RunSaveDocument document = CreateInitialDocument(
            "99999999-aaaa-bbbb-cccc-dddddddddddd");

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            document,
            new ExistingConfigurationCatalog(heroMaxHealth: 81));

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("max health").IgnoreCase);
    }

    /// <summary>schema v1 缺少地图配方与路径，无法无歧义迁移时必须 fail-fast。</summary>
    [Test]
    public void Read_SchemaV1WithoutMapRecipe_ReturnsUnsupportedSchema()
    {
        const string legacyV1Json = @"{
  ""schemaVersion"": 1,
  ""runId"": ""12345678-1234-1234-1234-123456789abc"",
  ""heroTemplateId"": 1001,
  ""currentHealth"": 80,
  ""maxHealth"": 80,
  ""deckTemplateId"": 1001,
  ""encounterTemplateId"": 5001,
  ""randomRootSeed"": 777,
  ""nodeStatus"": ""Available"",
  ""battleAttemptSequence"": 0
}";

        RunSaveDocumentReadResult result = RunSaveDocumentCodec.Read(legacyV1Json);

        Assert.That(result.Status, Is.EqualTo(RunSaveDocumentReadStatus.UnsupportedSchema));
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
    }

    /// <summary>schema v2 的初始牌组模板必须无歧义展开为当前有序实例牌组。</summary>
    [Test]
    public void Read_SchemaV2DeckTemplate_MigratesThroughLegacyFallbackAndRestoresOrderedInstances()
    {
        RunSaveDocument current = CreateInitialDocument(
            "12345678-aaaa-bbbb-cccc-123456789abc");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 2;
        raw.Remove("runCards");
        raw.Remove("pendingCardReward");
        raw["deckTemplateId"] = 1001;

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());
        RunSaveRestoreResult restore = read.Document == null
            ? null
            : RunSaveDocumentMapper.CreateRestore(
                read.Document,
                new ExistingConfigurationCatalog());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.SchemaVersion, Is.EqualTo(RunSaveDocument.CurrentSchemaVersion));
        Assert.That(read.Document.RunCards, Is.Null);
        Assert.That(read.Document.LegacyDeckTemplateId, Is.EqualTo(1001));
        Assert.That(read.Document.PendingCardReward, Is.Null);
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(
            restore.Options.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(
            restore.Options.RunDeck.Cards.Select(card => card.TemplateId),
            Is.EqualTo(new[] { 3002, 3002, 3003 }));

        using var store = new RunStateStore();
        store.RestoreRun(restore.Options);
        RunSaveDocument resaved = RunSaveDocumentMapper.Create(store.Current);
        Assert.That(resaved.RunCards, Is.Not.Null);
        Assert.That(resaved.RunCards.Select(card => card.InstanceId), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(resaved.LegacyDeckTemplateId, Is.Null);
    }

    /// <summary>schema v2 不得夹带新 schema 的实例牌组或 legacy 字段并让迁移静默覆盖。</summary>
    [TestCase("runCards")]
    [TestCase("legacyDeckTemplateId")]
    public void Read_SchemaV2WithNewerDeckField_ReturnsInvalidDocument(string newerField)
    {
        RunSaveDocument current = CreateInitialDocument(
            "13131313-aaaa-bbbb-cccc-131313131313");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 2;
        raw.Remove("pendingCardReward");
        raw["deckTemplateId"] = 1001;
        if (newerField == "legacyDeckTemplateId")
        {
            raw.Remove("runCards");
            raw["legacyDeckTemplateId"] = 1001;
        }

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Does.Contain(newerField));
    }

    /// <summary>schema v3 的 canonical RunDeck 必须补入空奖励字段并无歧义迁移到 v4。</summary>
    [Test]
    public void Read_SchemaV3CanonicalDeck_MigratesWithNullPendingReward()
    {
        RunSaveDocument current = CreateInitialDocument(
            "23232323-aaaa-bbbb-cccc-232323232323");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 3;
        raw.Remove("pendingCardReward");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.SchemaVersion, Is.EqualTo(RunSaveDocument.CurrentSchemaVersion));
        Assert.That(read.Document.RunCards, Is.Not.Null);
        Assert.That(read.Document.PendingCardReward, Is.Null);
    }

    /// <summary>schema v3 当时不存在奖励字段，夹带 v4 Pending 事实必须拒绝而非静默擦除。</summary>
    [Test]
    public void Read_SchemaV3WithPendingRewardField_ReturnsInvalidDocument()
    {
        RunSaveDocument current = CreateInitialDocument(
            "24242424-aaaa-bbbb-cccc-242424242424");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 3;

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Does.Contain("pendingCardReward"));
    }

    /// <summary>legacy deck fallback 只用于稳定旧档展开，不能与可结算 Pending 奖励组成死档。</summary>
    [Test]
    public void Read_RewardPendingWithLegacyDeckFallback_ReturnsInvalidDocument()
    {
        using RunStateStore store = CreateStore(
            "25252525-aaaa-bbbb-cccc-252525252525",
            mapSeed: 987612345u);
        MapNodeId nodeId = FirstSelectableNodeId(store.Current);
        store.CommitNode(nodeId);
        RunBattleInput battle = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth: 61,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(
            RunSaveDocumentMapper.Create(pending)));
        raw["runCards"] = JValue.CreateNull();
        raw["legacyDeckTemplateId"] = 1001;

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Does.Contain("legacy").IgnoreCase);
    }

    /// <summary>任一关键 JSON 属性重复时必须拒绝整份输入，不能采用 first/last-wins。</summary>
    [TestCase("schemaVersion", "4")]
    [TestCase("runCards", "[]")]
    [TestCase("pendingCardReward", "null")]
    public void Read_DuplicateCriticalProperty_ReturnsInvalidJson(
        string propertyName,
        string duplicateValue)
    {
        string json = RunSaveDocumentCodec.Serialize(CreateInitialDocument(
            "26262626-aaaa-bbbb-cccc-262626262626"));
        int objectStart = json.IndexOf('{');
        string duplicated = json.Insert(
            objectStart + 1,
            $"\"{propertyName}\":{duplicateValue},");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(duplicated);

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidJson));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Is.Not.Empty);
    }

    /// <summary>progressPhase 必须使用可审阅的精确字符串，数字枚举不能绕过当前 schema。</summary>
    [Test]
    public void Read_NumericProgressPhase_ReturnsInvalidDocument()
    {
        RunSaveDocument document = CreateInitialDocument(
            "abcdefab-cdef-abcd-efab-cdefabcdefab");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(document));
        raw["progressPhase"] = 0;

        RunSaveDocumentReadResult result = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(result.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Detail, Does.Contain("progressPhase"));
    }

    /// <summary>坏 JSON 与未知 schema 保持不同错误分类，且都不产生可继续文档。</summary>
    [TestCase("{", RunSaveDocumentReadStatus.InvalidJson)]
    [TestCase("{\"schemaVersion\":999}", RunSaveDocumentReadStatus.UnsupportedSchema)]
    public void Read_BrokenOrUnsupportedJson_ReturnsExplicitFailure(
        string json,
        RunSaveDocumentReadStatus expectedStatus)
    {
        RunSaveDocumentReadResult result = RunSaveDocumentCodec.Read(json);

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
    }

    /// <summary>EncounterCommitted 与 InBattle 都是瞬态，映射器不得把未结算战斗写入稳定存档。</summary>
    [Test]
    public void Create_EncounterCommittedOrInBattle_ThrowsWithoutDocument()
    {
        using RunStateStore store = CreateStore(
            "fedcbafe-dcba-fedc-bafe-dcbafedcbafe",
            mapSeed: 271828182u);
        store.CommitNode(FirstSelectableNodeId(store.Current));

        Assert.Throws<InvalidOperationException>(() =>
            RunSaveDocumentMapper.Create(store.Current));

        store.BeginCommittedBattle();

        Assert.Throws<InvalidOperationException>(() =>
            RunSaveDocumentMapper.Create(store.Current));
    }

    /// <summary>内存 fake 继续通过公共 port 表达单槽 commit/load/delete，不泄漏文件系统语义。</summary>
    [Test]
    public void InMemorySaveStore_CommitLoadDelete_UsesTypedSingleSlotContract()
    {
        RunSaveDocument document = CreateInitialDocument(
            "01234567-89ab-cdef-0123-456789abcdef");
        IRunSaveStore saves = new InMemoryRunSaveStore();

        RunSaveCommitResult commit = saves.Commit(document);
        RunSaveLoadResult load = saves.Load();
        RunSaveDeleteResult delete = saves.Delete();
        RunSaveLoadResult afterDelete = saves.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.MapFingerprint, Is.EqualTo(document.MapFingerprint));
        Assert.That(load.Document.PathNodeIds, Is.EqualTo(document.PathNodeIds));
        Assert.That(delete.Status, Is.EqualTo(RunSaveDeleteStatus.Success));
        Assert.That(afterDelete.Status, Is.EqualTo(RunSaveLoadStatus.NotFound));
    }

    public enum MissingConfiguration
    {
        Hero,
        Deck,
        Encounter,
    }

    /// <summary>建立使用当前固定 profile 与指定 map seed 的新 Run Store。</summary>
    private static RunStateStore CreateStore(string runId, uint mapSeed)
    {
        MapDefinition map = ActMapGenerator.Generate(TinySpireActMapProfiles.Current, mapSeed);
        var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.ParseExact(runId, "D")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002, 3002, 3003 }),
            randomRootSeed: 987654321u,
            map: map));
        return store;
    }

    /// <summary>创建一份当前 schema 的初始地图稳定文档。</summary>
    private static RunSaveDocument CreateInitialDocument(string runId)
    {
        using RunStateStore store = CreateStore(runId, mapSeed: 314159265u);
        return RunSaveDocumentMapper.Create(store.Current);
    }

    /// <summary>选择并胜利结算当前路径的首个普通可达 Combat 节点。</summary>
    private static RunState CompleteFirstSelectableCombat(
        RunStateStore store,
        int settledHealth)
    {
        MapNodeId nodeId = FirstSelectableNodeId(store.Current);
        Assert.That(store.Current.MapDefinition.GetNode(nodeId).Kind, Is.EqualTo(MapNodeKind.Combat));
        store.CommitNode(nodeId);
        RunBattleInput battle = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));
        return store.CommitCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: null);
    }

    /// <summary>胜利穿过全部普通层后选择首个普通可达 Boss 门。</summary>
    private static RunState ReachFirstBossGate(RunStateStore store)
    {
        for (int layer = 0; layer < TinySpireActMapProfiles.Current.NormalLayerSlotCounts.Count; layer++)
            CompleteFirstSelectableCombat(store, settledHealth: 60 - layer);

        MapNodeId bossNodeId = FirstSelectableNodeId(store.Current);
        Assert.That(store.Current.MapDefinition.GetNode(bossNodeId).Kind, Is.EqualTo(MapNodeKind.Boss));
        return store.CommitNode(bossNodeId);
    }

    /// <summary>按普通移动规则读取当前路径的首个可选节点。</summary>
    private static MapNodeId FirstSelectableNodeId(RunState state)
    {
        return MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary)
            .First();
    }

    /// <summary>复制文档并只替换漂移测试指定的地图配方字段。</summary>
    private static RunSaveDocument CopyDocument(
        RunSaveDocument source,
        string mapProfileId = null,
        int? mapGeneratorVersion = null,
        string mapFingerprint = null,
        string[] pathNodeIds = null,
        RunSavePendingCardRewardDocument pendingCardReward = null,
        IReadOnlyList<RunSaveCardDocument> runCards = null)
    {
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            runCards ?? source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            mapProfileId ?? source.MapProfileId,
            mapGeneratorVersion ?? source.MapGeneratorVersion,
            source.MapSeed,
            mapFingerprint ?? source.MapFingerprint,
            pathNodeIds ?? source.PathNodeIds,
            source.ProgressPhase,
            source.CommittedNodeId,
            source.TerminalReason,
            pendingCardReward ?? source.PendingCardReward);
    }

    /// <summary>把 canonical 测试文档改写为只携带旧 Deck 模板的一次性恢复输入。</summary>
    private static RunSaveDocument CopyAsLegacyFallback(
        RunSaveDocument source,
        int deckTemplateId)
    {
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            runCards: null,
            legacyDeckTemplateId: deckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            source.PathNodeIds,
            source.ProgressPhase,
            source.CommittedNodeId,
            source.TerminalReason,
            source.PendingCardReward);
    }

    private sealed class ExistingConfigurationCatalog :
        IRunSaveConfigurationCatalog,
        IRunCardUpgradeConfigurationCatalog
    {
        private readonly int _heroMaxHealth;
        private readonly ActMapProfile _profile;
        private readonly int? _missingCardTemplateId;

        /// <summary>建立测试配置目录，并允许替换 Hero 上限、同 ID profile 或指定缺失 Card。</summary>
        public ExistingConfigurationCatalog(
            int heroMaxHealth = 80,
            ActMapProfile profile = null,
            int? missingCardTemplateId = null)
        {
            _heroMaxHealth = heroMaxHealth;
            _profile = profile ?? TinySpireActMapProfiles.Current;
            _missingCardTemplateId = missingCardTemplateId;
        }

        /// <summary>完整目录中的 Hero 均视为存在。</summary>
        public bool HeroExists(int templateId)
        {
            return true;
        }

        /// <summary>返回测试指定的当前 Hero 生命上限。</summary>
        public int GetHeroMaxHealth(int templateId)
        {
            return _heroMaxHealth;
        }

        /// <summary>完整目录中的 Deck 均视为存在。</summary>
        public bool DeckExists(int templateId)
        {
            return true;
        }

        /// <summary>按固定顺序返回含同模板副本的测试初始牌组。</summary>
        public IReadOnlyList<int> GetDeckCardTemplateIds(int templateId)
        {
            return new[] { 3002, 3002, 3003 };
        }

        /// <summary>除测试指定缺失模板外，其余 Card 均视为存在。</summary>
        public bool CardExists(int templateId)
        {
            return templateId != _missingCardTemplateId;
        }

        /// <summary>测试有限卡 3002 仅允许一级、无限卡 3123 允许任意可表达非负等级。</summary>
        public bool IsCardUpgradeLevelValid(int templateId, int upgradeLevel)
        {
            if (upgradeLevel < 0)
                return false;

            if (templateId == 3002)
                return upgradeLevel <= 1;
            return templateId == 3123 || upgradeLevel == 0;
        }

        /// <summary>测试 Hero 1001 只接受本轮冻结的三个显式奖励候选。</summary>
        public bool IsRewardCardForHero(int heroTemplateId, int cardTemplateId)
        {
            return heroTemplateId == 1001 &&
                   cardTemplateId != _missingCardTemplateId &&
                   (cardTemplateId == 3105 ||
                    cardTemplateId == 3123 ||
                    cardTemplateId == 3157);
        }

        /// <summary>完整目录中的 Encounter 均视为存在。</summary>
        public bool EncounterExists(int templateId)
        {
            return true;
        }

        /// <summary>只按稳定 ID 返回测试目录当前采用的 Act profile。</summary>
        public ActMapProfile GetActMapProfile(string profileId)
        {
            return string.Equals(profileId, _profile.ProfileId, StringComparison.Ordinal)
                ? _profile
                : null;
        }
    }

    private sealed class SelectiveConfigurationCatalog : IRunSaveConfigurationCatalog
    {
        private readonly MissingConfiguration _missing;

        /// <summary>建立只缺少一种指定静态配置的测试目录。</summary>
        public SelectiveConfigurationCatalog(MissingConfiguration missing)
        {
            _missing = missing;
        }

        /// <summary>仅在测试指定 Hero 缺失时返回 false。</summary>
        public bool HeroExists(int templateId)
        {
            return _missing != MissingConfiguration.Hero;
        }

        /// <summary>返回与测试存档一致的 Hero 生命上限。</summary>
        public int GetHeroMaxHealth(int templateId)
        {
            return 80;
        }

        /// <summary>仅在测试指定 Deck 缺失时返回 false。</summary>
        public bool DeckExists(int templateId)
        {
            return _missing != MissingConfiguration.Deck;
        }

        /// <summary>为非缺失 Deck 的恢复分支提供稳定测试牌序。</summary>
        public IReadOnlyList<int> GetDeckCardTemplateIds(int templateId)
        {
            return new[] { 3002, 3003 };
        }

        /// <summary>当前旧测试目录中的 Card 均视为存在。</summary>
        public bool CardExists(int templateId)
        {
            return true;
        }

        /// <summary>选择性缺配置测试不包含 Pending reward，若被调用则保持 Card 可奖励。</summary>
        public bool IsRewardCardForHero(int heroTemplateId, int cardTemplateId)
        {
            return true;
        }

        /// <summary>仅在测试指定 Encounter 缺失时返回 false。</summary>
        public bool EncounterExists(int templateId)
        {
            return _missing != MissingConfiguration.Encounter;
        }

        /// <summary>始终返回当前支持的固定 Act profile。</summary>
        public ActMapProfile GetActMapProfile(string profileId)
        {
            return TinySpireActMapProfiles.GetById(profileId);
        }
    }
}
