using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class RunSaveDocumentTests
{
    /// <summary>NodeVisitPending Shop 必须保存可核对身份与三项库存的全部冻结字段。</summary>
    [Test]
    public void NodeVisitPending_ShopJsonRoundTrip_PreservesIdentityAndStock()
    {
        RunSaveDocument source = CreateInitialDocument(
            "00110011-2233-4455-6677-8899aabbccdd");
        const string nodeId = "L01-S00";
        var pendingNodeVisit = new RunSavePendingNodeVisitDocument(
            visitId: $"{source.RunId}/{nodeId}",
            nodeId,
            contentId: 6201,
            MapNodeKind.Shop,
            restPayload: null,
            chestPayload: null,
            shopPayload: new RunSaveShopNodeVisitPayloadDocument(new[]
            {
                new RunSaveShopStockEntryDocument(
                    entryId: 1,
                    RunShopStockKind.Relic,
                    templateId: 4101,
                    price: 150,
                    purchased: false),
                new RunSaveShopStockEntryDocument(
                    entryId: 2,
                    RunShopStockKind.Potion,
                    templateId: 5101,
                    price: 60,
                    purchased: true),
                new RunSaveShopStockEntryDocument(
                    entryId: 3,
                    RunShopStockKind.Card,
                    templateId: 3105,
                    price: 75,
                    purchased: false),
            }),
            eventPayload: null);
        var document = new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            source.PathNodeIds,
            RunSaveProgressPhase.NodeVisitPending,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            source.Relics,
            source.Potions,
            source.Gold,
            pendingNodeVisit);

        string json = RunSaveDocumentCodec.Serialize(document);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        JObject raw = JObject.Parse(json);

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(raw.Value<string>("progressPhase"), Is.EqualTo("NodeVisitPending"));
        Assert.That(read.Document.PendingNodeVisit.VisitId,
            Is.EqualTo($"{source.RunId}/{nodeId}"));
        Assert.That(read.Document.PendingNodeVisit.NodeId, Is.EqualTo(nodeId));
        Assert.That(read.Document.PendingNodeVisit.ContentId, Is.EqualTo(6201));
        Assert.That(read.Document.PendingNodeVisit.Kind, Is.EqualTo(MapNodeKind.Shop));
        Assert.That(
            read.Document.PendingNodeVisit.ShopPayload.Entries.Select(entry => entry.EntryId),
            Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(
            read.Document.PendingNodeVisit.ShopPayload.Entries.Select(entry => entry.Kind),
            Is.EqualTo(new[]
            {
                RunShopStockKind.Relic,
                RunShopStockKind.Potion,
                RunShopStockKind.Card,
            }));
        Assert.That(
            read.Document.PendingNodeVisit.ShopPayload.Entries.Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, false }));
    }

    /// <summary>四类 NodeVisit payload 都必须以唯一匹配字段完成严格 JSON 往返。</summary>
    [TestCase(MapNodeKind.Rest)]
    [TestCase(MapNodeKind.Chest)]
    [TestCase(MapNodeKind.Shop)]
    [TestCase(MapNodeKind.Event)]
    public void NodeVisitPending_AllPayloadsJsonRoundTrip_PreserveFrozenFacts(
        MapNodeKind kind)
    {
        RunSaveDocument source = CreateNodeVisitDocument(
            "01100110-2233-4455-6677-8899aabbccdd",
            kind);

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
            RunSaveDocumentCodec.Serialize(source));

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.PendingNodeVisit.Kind, Is.EqualTo(kind));
        switch (kind)
        {
            case MapNodeKind.Rest:
                Assert.That(read.Document.PendingNodeVisit.RestPayload.HealAmount, Is.EqualTo(24));
                Assert.That(
                    read.Document.PendingNodeVisit.RestPayload.UpgradeCandidateInstanceIds,
                    Is.EqualTo(new[] { 1, 2 }));
                break;
            case MapNodeKind.Chest:
                Assert.That(
                    read.Document.PendingNodeVisit.ChestPayload.PotionTemplateId,
                    Is.EqualTo(5101));
                break;
            case MapNodeKind.Shop:
                Assert.That(
                    read.Document.PendingNodeVisit.ShopPayload.Entries.Select(entry => entry.Price),
                    Is.EqualTo(new[] { 150, 60, 75 }));
                break;
            case MapNodeKind.Event:
                Assert.That(read.Document.PendingNodeVisit.EventPayload.GainGoldAmount,
                    Is.EqualTo(45));
                Assert.That(read.Document.PendingNodeVisit.EventPayload.PaidHealCost,
                    Is.EqualTo(30));
                Assert.That(read.Document.PendingNodeVisit.EventPayload.PaidHealAmount,
                    Is.EqualTo(18));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>当前 v5 必须拒绝缺字段、错 payload、伪造身份与重复库存等 NodeVisit 形状。</summary>
    [TestCase(NodeVisitShapeViolation.MissingTopLevelField)]
    [TestCase(NodeVisitShapeViolation.MissingNullablePayloadField)]
    [TestCase(NodeVisitShapeViolation.MismatchedKind)]
    [TestCase(NodeVisitShapeViolation.MultiplePayloads)]
    [TestCase(NodeVisitShapeViolation.ForgedVisitId)]
    [TestCase(NodeVisitShapeViolation.DuplicateShopEntryId)]
    [TestCase(NodeVisitShapeViolation.NumericNodeKind)]
    [TestCase(NodeVisitShapeViolation.NumericStockKind)]
    public void Read_InvalidNodeVisitShape_ReturnsInvalidDocument(
        NodeVisitShapeViolation violation)
    {
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(
            CreateNodeVisitDocument(
                "02110211-2233-4455-6677-8899aabbccdd",
                MapNodeKind.Shop)));
        var pending = (JObject)raw["pendingNodeVisit"];
        var entries = (JArray)pending["shopPayload"]["entries"];
        switch (violation)
        {
            case NodeVisitShapeViolation.MissingTopLevelField:
                raw.Remove("pendingNodeVisit");
                break;
            case NodeVisitShapeViolation.MissingNullablePayloadField:
                pending.Remove("eventPayload");
                break;
            case NodeVisitShapeViolation.MismatchedKind:
                pending["kind"] = nameof(MapNodeKind.Rest);
                break;
            case NodeVisitShapeViolation.MultiplePayloads:
                pending["restPayload"] = new JObject
                {
                    ["healAmount"] = 24,
                    ["upgradeCandidateInstanceIds"] = new JArray(1, 2),
                };
                break;
            case NodeVisitShapeViolation.ForgedVisitId:
                pending["visitId"] =
                    "ffffffff-ffff-ffff-ffff-ffffffffffff/L01-S00";
                break;
            case NodeVisitShapeViolation.DuplicateShopEntryId:
                entries[1]["entryId"] = entries[0]["entryId"].Value<int>();
                break;
            case NodeVisitShapeViolation.NumericNodeKind:
                pending["kind"] = (int)MapNodeKind.Shop;
                break;
            case NodeVisitShapeViolation.NumericStockKind:
                entries[0]["kind"] = (int)RunShopStockKind.Relic;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(violation));
        }

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Is.Not.Empty);
    }

    /// <summary>Mapper 必须从领域 NodeVisitPending 投影四类 payload 与可核对访问身份。</summary>
    [TestCase(MapNodeKind.Rest)]
    [TestCase(MapNodeKind.Chest)]
    [TestCase(MapNodeKind.Shop)]
    [TestCase(MapNodeKind.Event)]
    public void MapperCreate_NodeVisitPending_PreservesEnvelopeAndPayload(
        MapNodeKind kind)
    {
        const int contentId = 7201;
        var runId = new RunId(Guid.Parse("03120312-2233-4455-6677-8899aabbccdd"));
        MapDefinition map = CreateSingleNonCombatMap(kind, contentId);
        MapNodeId nodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        PendingRunNodeVisit pending = CreatePendingRunNodeVisit(
            kind,
            new RunNodeVisitId(runId, nodeId),
            contentId);
        var options = new RunRestoreOptions(
            runId,
            heroTemplateId: 1001,
            currentHealth: 70,
            maxHealth: 80,
            RunDeck.CreateInitial(new[] { 3002, 3003 }),
            randomRootSeed: 42420002u,
            map,
            pathNodeIds: new[] { MapNodeId.FromPosition(layer: 0, slot: 0) },
            RunProgressPhase.NodeVisitPending,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            holdings: RunHoldings.Empty(),
            pendingNodeVisit: pending);

        RunSaveDocument document = RunSaveDocumentMapper.Create(options);

        Assert.That(document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(document.PendingNodeVisit.VisitId, Is.EqualTo(pending.Id.ToString()));
        Assert.That(document.PendingNodeVisit.NodeId, Is.EqualTo(nodeId.Value));
        Assert.That(document.PendingNodeVisit.ContentId, Is.EqualTo(contentId));
        Assert.That(document.PendingNodeVisit.Kind, Is.EqualTo(kind));
        Assert.That(RunSaveDocumentCodec.Read(RunSaveDocumentCodec.Serialize(document)).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>schema v5 必须逐项保存遗物、药水实例身份、模板顺序与金币。</summary>
    [Test]
    public void Holdings_JsonRoundTrip_PreservesRelicsPotionsAndGold()
    {
        RunSaveDocument source = CreateInitialDocument(
            "00112233-4455-6677-8899-aabbccddeeff");
        var document = new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            source.PathNodeIds,
            source.ProgressPhase,
            source.CommittedNodeId,
            source.TerminalReason,
            source.PendingCardReward,
            relics: new[]
            {
                new RunSaveRelicDocument(instanceId: 1, templateId: 4101),
                new RunSaveRelicDocument(instanceId: 2, templateId: 4102),
            },
            potions: new[]
            {
                new RunSavePotionDocument(instanceId: 1, templateId: 5101),
                new RunSavePotionDocument(instanceId: 2, templateId: 5101),
            },
            gold: 137,
            pendingNodeVisit: null);

        string json = RunSaveDocumentCodec.Serialize(document);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        JObject raw = JObject.Parse(json);

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(raw.Value<int>("schemaVersion"), Is.EqualTo(5));
        Assert.That(
            read.Document.Relics.Select(relic => relic.InstanceId),
            Is.EqualTo(new[] { 1, 2 }));
        Assert.That(
            read.Document.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 4101, 4102 }));
        Assert.That(
            read.Document.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(new[] { 1, 2 }));
        Assert.That(
            read.Document.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 5101, 5101 }));
        Assert.That(read.Document.Gold, Is.EqualTo(137));
        Assert.That(read.Document.RequiresCanonicalRewrite, Is.False);
    }

    /// <summary>Mapper 必须让非默认持有物从稳定 RunState 经 JSON 冷恢复后逐项等值。</summary>
    [Test]
    public void Holdings_MapperRoundTrip_PreservesDomainFacts()
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.Current,
            mapSeed: 42424242u);
        var holdings = new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(1), templateId: 4101),
                new RunRelic(new RunRelicInstanceId(2), templateId: 4102),
            },
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 5101),
                new RunPotion(new RunPotionInstanceId(2), templateId: 5102),
            },
            gold: 248);
        using var sourceStore = new RunStateStore();
        sourceStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.ParseExact(
                "10213243-5465-7687-98a9-bacbdcedfe0f",
                "D")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002, 3002, 3003 }),
            randomRootSeed: 987654321u,
            map,
            holdings));

        RunSaveDocument sourceDocument = RunSaveDocumentMapper.Create(sourceStore.Current);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
            RunSaveDocumentCodec.Serialize(sourceDocument));
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            read.Document,
            new ExistingConfigurationCatalog());
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);

        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(
            restored.Holdings.Relics.Select(relic => relic.InstanceId.Sequence),
            Is.EqualTo(new[] { 1, 2 }));
        Assert.That(
            restored.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 4101, 4102 }));
        Assert.That(
            restored.Holdings.Potions.Select(potion => potion.InstanceId.Sequence),
            Is.EqualTo(new[] { 1, 2 }));
        Assert.That(
            restored.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 5101, 5102 }));
        Assert.That(restored.Holdings.Gold, Is.EqualTo(248));
    }

    /// <summary>恢复输入必须显式携带完整 holdings，禁止把缺失事实静默改写成 100 Gold。</summary>
    [Test]
    public void RunRestoreOptions_NullHoldings_ThrowsInsteadOfDefaulting()
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.Current,
            mapSeed: 43434343u);

        Assert.Throws<ArgumentNullException>(() => new RunRestoreOptions(
            new RunId(Guid.Parse("20314253-6475-8697-a8b9-cadbecfd0e1f")),
            heroTemplateId: 1001,
            currentHealth: 80,
            maxHealth: 80,
            RunDeck.CreateInitial(new[] { 3002, 3002, 3003 }),
            randomRootSeed: 987654321u,
            map,
            pathNodeIds: new[] { MapNodeId.FromPosition(layer: 0, slot: 0) },
            RunProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            holdings: null));
    }

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

        Assert.That(
            raw.Value<int>("schemaVersion"),
            Is.EqualTo(RunSaveDocument.CurrentSchemaVersion));
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
        Assert.That(
            raw["pendingCardReward"]["attachedLoot"].Value<int?>("relicTemplateId"),
            Is.EqualTo(8001));
        Assert.That(
            raw["pendingCardReward"]["attachedLoot"].Value<int?>("potionTemplateId"),
            Is.EqualTo(9001));
        Assert.That(restored.PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(8001));
        Assert.That(restored.PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(9001));
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
                "relics",
                "potions",
                "gold",
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
                "pendingNodeVisit",
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
            mapGeneratorVersion: TinySpireActMapProfiles.LegacyG3V1.GeneratorVersion + 1);

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            drifted,
            new ExistingConfigurationCatalog());

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("generator version").IgnoreCase);
    }

    /// <summary>旧 G3 profile v1 与新 G6 profile v1 必须各自按绑定的生成器版本完成恢复。</summary>
    [Test]
    public void CreateRestore_ProfileOwnedGeneratorVersions_RestoreLegacyAndMixedRecipes()
    {
        RunSaveDocument legacy = CreateInitialDocument(
            "56565656-6767-7878-8989-aaaaaaaaaaaa");
        RunSaveRestoreResult legacyRestore = RunSaveDocumentMapper.CreateRestore(
            legacy,
            new ExistingConfigurationCatalog());
        RunSaveDocument mixed = CreateAuthoritativeMixedPendingDocument(
            "57575757-6868-7979-8a8a-bbbbbbbbbbbb",
            MapNodeKind.Rest);
        RunSaveRestoreResult mixedRestore = RunSaveDocumentMapper.CreateRestore(
            mixed,
            new ExistingConfigurationCatalog(profile: TinySpireActMapProfiles.NewRunG6V1));

        Assert.That(legacy.MapGeneratorVersion,
            Is.EqualTo(TinySpireActMapProfiles.LegacyG3V1.GeneratorVersion));
        Assert.That(legacyRestore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(mixed.MapProfileId,
            Is.EqualTo(TinySpireActMapProfiles.NewRunG6V1ProfileId));
        Assert.That(mixed.MapGeneratorVersion, Is.EqualTo(ActMapGenerator.NewRunG6Version));
        Assert.That(mixedRestore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
    }

    /// <summary>四类 Pending 的存档 payload 必须逐字段等于从 Run/地图/配置重建的权威初始事实。</summary>
    [TestCase(MapNodeKind.Rest)]
    [TestCase(MapNodeKind.Chest)]
    [TestCase(MapNodeKind.Shop)]
    [TestCase(MapNodeKind.Event)]
    public void CreateRestore_ForgedNodeVisitPayload_ReturnsInvalidDocument(
        MapNodeKind kind)
    {
        string runId = kind switch
        {
            MapNodeKind.Rest => "58585858-6969-7a7a-8b8b-c1c1c1c1c1c1",
            MapNodeKind.Chest => "59595959-6a6a-7b7b-8c8c-c2c2c2c2c2c2",
            MapNodeKind.Shop => "5a5a5a5a-6b6b-7c7c-8d8d-c3c3c3c3c3c3",
            MapNodeKind.Event => "5b5b5b5b-6c6c-7d7d-8e8e-c4c4c4c4c4c4",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        RunSaveDocument source = CreateAuthoritativeMixedPendingDocument(runId, kind);
        RunSavePendingNodeVisitDocument forgedPending = ForgePendingNodeVisitPayload(
            source.PendingNodeVisit);
        RunSaveDocument forged = CopyDocument(
            source,
            pendingNodeVisit: forgedPending);
        var catalog = new ExistingConfigurationCatalog(
            profile: TinySpireActMapProfiles.NewRunG6V1);

        RunSaveRestoreResult authenticRestore = RunSaveDocumentMapper.CreateRestore(
            source,
            catalog);
        RunSaveRestoreResult forgedRestore = RunSaveDocumentMapper.CreateRestore(
            forged,
            catalog);

        Assert.That(authenticRestore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(forgedRestore.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(forgedRestore.Options, Is.Null);
        Assert.That(forgedRestore.Detail, Does.Contain("node visit").IgnoreCase);
    }

    /// <summary>Shop 冷恢复允许任意已购子集，但仍由工厂终审库存顺序、身份、种类、模板与价格。</summary>
    [Test]
    public void CreateRestore_ShopPurchasedSubsetRestoresWithoutWeakeningStaticInventoryAuthority()
    {
        RunSaveDocument source = CreateAuthoritativeMixedPendingDocument(
            "5c5c5c5c-6d6d-7e7e-8f8f-c5c5c5c5c5c5",
            MapNodeKind.Shop);
        RunSaveShopStockEntryDocument[] entries = source.PendingNodeVisit.ShopPayload.Entries
            .Select((entry, index) => new RunSaveShopStockEntryDocument(
                entry.EntryId,
                entry.Kind,
                entry.TemplateId,
                entry.Price,
                purchased: index != 1))
            .ToArray();
        var purchasedSubset = new RunSavePendingNodeVisitDocument(
            source.PendingNodeVisit.VisitId,
            source.PendingNodeVisit.NodeId,
            source.PendingNodeVisit.ContentId,
            source.PendingNodeVisit.Kind,
            restPayload: null,
            chestPayload: null,
            new RunSaveShopNodeVisitPayloadDocument(entries),
            eventPayload: null);
        RunSaveDocument document = CopyDocument(source, pendingNodeVisit: purchasedSubset);

        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            document,
            new ExistingConfigurationCatalog(profile: TinySpireActMapProfiles.NewRunG6V1));

        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restore.Options.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { true, false, true }));
    }

    /// <summary>生产 Store 连续购买形成的 Shop 文档必须原样恢复余额、实例身份与 Purchased 子集。</summary>
    [Test]
    public void CreateRestore_AfterShopPurchasesPreservesPurchasedGoldAndAcquiredContent()
    {
        var catalog = new ExistingConfigurationCatalog(
            profile: TinySpireActMapProfiles.NewRunG6V1);
        RunSaveDocument source = CreateAuthoritativeMixedPendingDocument(
            "5d5d5d5d-6e6e-7f7f-9090-c6c6c6c6c6c6",
            MapNodeKind.Shop);
        RunSaveRestoreResult initialRestore = RunSaveDocumentMapper.CreateRestore(
            source,
            catalog);
        using var store = new RunStateStore();
        RunState pending = store.RestoreRun(initialRestore.Options);
        int frozenPotionTemplateId = pending.PendingNodeVisit.ShopPayload.Entries[1].TemplateId;
        int frozenCardTemplateId = pending.PendingNodeVisit.ShopPayload.Entries[2].TemplateId;
        RunState afterPotion = store.CommitShopSettlement(
            store.PreviewShopPurchaseSettlement(
                pending.PendingNodeVisit.Id,
                stockEntryId: 2,
                catalog));
        RunState afterCard = store.CommitShopSettlement(
            store.PreviewShopPurchaseSettlement(
                afterPotion.PendingNodeVisit.Id,
                stockEntryId: 3,
                catalog));
        RunSaveDocument purchasedDocument = RunSaveDocumentMapper.Create(afterCard);

        RunSaveRestoreResult purchasedRestore = RunSaveDocumentMapper.CreateRestore(
            purchasedDocument,
            catalog);
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(purchasedRestore.Options);

        Assert.That(initialRestore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(purchasedRestore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restored.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(restored.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, true }));
        Assert.That(restored.Holdings.Gold, Is.EqualTo(25));
        Assert.That(restored.Holdings.Potions.Single().InstanceId.Sequence, Is.EqualTo(1));
        Assert.That(restored.Holdings.Potions.Single().TemplateId,
            Is.EqualTo(frozenPotionTemplateId));
        Assert.That(restored.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(restored.RunDeck.Cards.Last().TemplateId,
            Is.EqualTo(frozenCardTemplateId));
        Assert.That(restored.PathNodeIds, Is.EqualTo(pending.PathNodeIds));
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
            TinySpireActMapProfiles.LegacyG3V1ProfileId,
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

    /// <summary>持有物或冻结奖励引用不存在的 G5 模板时，冷读档必须 fail-closed 且不发布恢复输入。</summary>
    [TestCase(MissingRunItemReference.HeldRelic, 8001)]
    [TestCase(MissingRunItemReference.HeldPotion, 9001)]
    [TestCase(MissingRunItemReference.AttachedRelic, 8001)]
    [TestCase(MissingRunItemReference.AttachedPotion, 9001)]
    public void CreateRestore_MissingRunItemReference_ReturnsInvalidDocument(
        MissingRunItemReference missing,
        int missingTemplateId)
    {
        RunSaveDocument document;
        int? missingRelicTemplateId = null;
        int? missingPotionTemplateId = null;
        switch (missing)
        {
            case MissingRunItemReference.HeldRelic:
                document = CopyDocument(
                    CreateInitialDocument("81818181-aaaa-bbbb-cccc-818181818181"),
                    relics: new[] { new RunSaveRelicDocument(1, 8001) });
                missingRelicTemplateId = 8001;
                break;
            case MissingRunItemReference.HeldPotion:
                document = CopyDocument(
                    CreateInitialDocument("82828282-aaaa-bbbb-cccc-828282828282"),
                    potions: new[] { new RunSavePotionDocument(1, 9001) });
                missingPotionTemplateId = 9001;
                break;
            case MissingRunItemReference.AttachedRelic:
            case MissingRunItemReference.AttachedPotion:
                string runId = missing == MissingRunItemReference.AttachedRelic
                    ? "83838383-aaaa-bbbb-cccc-838383838383"
                    : "84848484-aaaa-bbbb-cccc-848484848484";
                RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
                    CreateCanonicalPendingRewardJson(runId, attemptSequence: 1).ToString());
                Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
                document = read.Document;
                missingRelicTemplateId = missing == MissingRunItemReference.AttachedRelic
                    ? 8001
                    : null;
                missingPotionTemplateId = missing == MissingRunItemReference.AttachedPotion
                    ? 9001
                    : null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(missing), missing, null);
        }

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            document,
            new ExistingConfigurationCatalog(
                missingRelicTemplateId: missingRelicTemplateId,
                missingPotionTemplateId: missingPotionTemplateId));

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain(missingTemplateId.ToString()));
    }

    /// <summary>第二场普通战斗 Pending 不得伪造合法附着模板并在冷恢复后重复获利。</summary>
    [Test]
    public void CreateRestore_LaterRewardWithForgedAttachedPotion_ReturnsInvalidDocument()
    {
        using RunStateStore store = CreateStore(
            "85858585-aaaa-bbbb-cccc-858585858585",
            mapSeed: 85858585u);
        CompleteFirstSelectableCombat(store, settledHealth: 70);
        MapNodeId secondNodeId = FirstSelectableNodeId(store.Current);
        store.CommitNode(secondNodeId);
        RunBattleInput battle = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth: 60,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(
            RunSaveDocumentMapper.Create(pending)));
        ((JObject)raw["pendingCardReward"]["attachedLoot"])["potionTemplateId"] =
            RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattlePotion;

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            read.Document,
            new ExistingConfigurationCatalog());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(restore.Options, Is.Null);
        Assert.That(restore.Detail, Does.Contain("attached loot").IgnoreCase);
    }

    /// <summary>首场普通战斗 Pending 也不得删除当时应冻结的附着药水事实。</summary>
    [Test]
    public void CreateRestore_FirstRewardWithMissingAttachedPotion_ReturnsInvalidDocument()
    {
        JObject raw = CreateCanonicalPendingRewardJson(
            "86868686-aaaa-bbbb-cccc-868686868686",
            attemptSequence: 1);
        ((JObject)raw["pendingCardReward"]["attachedLoot"])["potionTemplateId"] =
            JValue.CreateNull();

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            read.Document,
            new ExistingConfigurationCatalog());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(restore.Options, Is.Null);
        Assert.That(restore.Detail, Does.Contain("attached loot").IgnoreCase);
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
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");
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
        Assert.That(read.Document.Relics, Is.Empty);
        Assert.That(read.Document.Potions, Is.Empty);
        Assert.That(read.Document.Gold, Is.EqualTo(100));
        Assert.That(read.Document.PendingNodeVisit, Is.Null);
        Assert.That(read.Document.RequiresCanonicalRewrite, Is.True);
        Assert.That(
            JObject.Parse(RunSaveDocumentCodec.Serialize(read.Document))
                .Property("requiresCanonicalRewrite"),
            Is.Null);
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
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");
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

    /// <summary>schema v3 的 canonical RunDeck 必须经 v4 补奖励后串行迁移到 v5。</summary>
    [Test]
    public void Read_SchemaV3CanonicalDeck_MigratesWithNullPendingReward()
    {
        RunSaveDocument current = CreateInitialDocument(
            "23232323-aaaa-bbbb-cccc-232323232323");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 3;
        raw.Remove("pendingCardReward");
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.SchemaVersion, Is.EqualTo(RunSaveDocument.CurrentSchemaVersion));
        Assert.That(read.Document.RunCards, Is.Not.Null);
        Assert.That(read.Document.PendingCardReward, Is.Null);
        Assert.That(read.Document.Relics, Is.Empty);
        Assert.That(read.Document.Potions, Is.Empty);
        Assert.That(read.Document.Gold, Is.EqualTo(100));
    }

    /// <summary>schema v3 当时不存在奖励字段，夹带 v4 Pending 事实必须拒绝而非静默擦除。</summary>
    [Test]
    public void Read_SchemaV3WithPendingRewardField_ReturnsInvalidDocument()
    {
        RunSaveDocument current = CreateInitialDocument(
            "24242424-aaaa-bbbb-cccc-242424242424");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 3;
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Does.Contain("pendingCardReward"));
    }

    /// <summary>schema v4 当时没有持有物字段，迁移必须显式补入空集合与 100 Gold。</summary>
    [Test]
    public void Read_SchemaV4WithoutHoldings_MigratesWithExplicitDefaults()
    {
        RunSaveDocument current = CreateInitialDocument(
            "34343434-aaaa-bbbb-cccc-343434343434");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 4;
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.SchemaVersion, Is.EqualTo(5));
        Assert.That(read.Document.Relics, Is.Empty);
        Assert.That(read.Document.Potions, Is.Empty);
        Assert.That(read.Document.Gold, Is.EqualTo(100));
        Assert.That(read.Document.PendingNodeVisit, Is.Null);
        Assert.That(read.Document.RequiresCanonicalRewrite, Is.True);
    }

    /// <summary>v4 首场普通战斗的 Pending 奖励必须迁移固定遗物与药水附着掉落。</summary>
    [Test]
    public void Read_SchemaV4FirstRewardPending_MigratesFixedAttachedLoot()
    {
        using RunStateStore store = CreateStore(
            "45454545-aaaa-bbbb-cccc-454545454545",
            mapSeed: 45454545u);
        MapNodeId nodeId = FirstSelectableNodeId(store.Current);
        store.CommitNode(nodeId);
        RunBattleInput battle = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth: 52,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(
            RunSaveDocumentMapper.Create(pending)));
        raw["schemaVersion"] = 4;
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");
        ((JObject)raw["pendingCardReward"]).Remove("attachedLoot");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(8001));
        Assert.That(read.Document.PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(9001));
        Assert.That(read.Document.RequiresCanonicalRewrite, Is.True);
    }

    /// <summary>v4 非首次 Pending 奖励必须迁移显式 Empty，不能重复发放首战附着掉落。</summary>
    [Test]
    public void Read_SchemaV4LaterRewardPending_MigratesEmptyAttachedLoot()
    {
        JObject raw = CreateCanonicalPendingRewardJson(
            "46464646-aaaa-bbbb-cccc-464646464646",
            attemptSequence: 2);
        raw["schemaVersion"] = 4;
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");
        ((JObject)raw["pendingCardReward"]).Remove("attachedLoot");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.PendingCardReward.AttachedLoot.RelicTemplateId, Is.Null);
        Assert.That(read.Document.PendingCardReward.AttachedLoot.PotionTemplateId, Is.Null);
        Assert.That(read.Document.RequiresCanonicalRewrite, Is.True);
    }

    /// <summary>v4 不得夹带当时不存在的 attachedLoot 字段让迁移静默采信。</summary>
    [Test]
    public void Read_SchemaV4WithAttachedLoot_ReturnsInvalidDocument()
    {
        JObject raw = CreateCanonicalPendingRewardJson(
            "47474747-aaaa-bbbb-cccc-474747474747",
            attemptSequence: 1);
        raw["schemaVersion"] = 4;
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Detail, Does.Contain("attachedLoot"));
    }

    /// <summary>canonical v5 的 Pending 奖励必须携带字段齐全、值为 null 或正数的 attachedLoot。</summary>
    [TestCase(AttachedLootShapeViolation.MissingObject)]
    [TestCase(AttachedLootShapeViolation.NullObject)]
    [TestCase(AttachedLootShapeViolation.MissingRelicField)]
    [TestCase(AttachedLootShapeViolation.MissingPotionField)]
    [TestCase(AttachedLootShapeViolation.NonPositiveRelic)]
    [TestCase(AttachedLootShapeViolation.NonPositivePotion)]
    public void Read_InvalidAttachedLootShape_ReturnsInvalidDocument(
        AttachedLootShapeViolation violation)
    {
        JObject raw = CreateCanonicalPendingRewardJson(
            "48484848-aaaa-bbbb-cccc-484848484848",
            attemptSequence: 1);
        var pending = (JObject)raw["pendingCardReward"];
        var attached = (JObject)pending["attachedLoot"];
        switch (violation)
        {
            case AttachedLootShapeViolation.MissingObject:
                pending.Remove("attachedLoot");
                break;
            case AttachedLootShapeViolation.NullObject:
                pending["attachedLoot"] = JValue.CreateNull();
                break;
            case AttachedLootShapeViolation.MissingRelicField:
                attached.Remove("relicTemplateId");
                break;
            case AttachedLootShapeViolation.MissingPotionField:
                attached.Remove("potionTemplateId");
                break;
            case AttachedLootShapeViolation.NonPositiveRelic:
                attached["relicTemplateId"] = 0;
                break;
            case AttachedLootShapeViolation.NonPositivePotion:
                attached["potionTemplateId"] = -1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(violation));
        }

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Is.Not.Empty);
    }

    /// <summary>v4 不得夹带任何 v5 持有物字段并让迁移静默采信或覆盖。</summary>
    [TestCase("relics")]
    [TestCase("potions")]
    [TestCase("gold")]
    [TestCase("pendingNodeVisit")]
    public void Read_SchemaV4WithNewerHoldingsField_ReturnsInvalidDocument(
        string newerField)
    {
        RunSaveDocument current = CreateInitialDocument(
            "35353535-aaaa-bbbb-cccc-353535353535");
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(current));
        raw["schemaVersion"] = 4;
        raw.Remove("relics");
        raw.Remove("potions");
        raw.Remove("gold");
        raw.Remove("pendingNodeVisit");
        raw[newerField] = newerField == "gold"
            ? new JValue(100)
            : newerField == "pendingNodeVisit"
                ? JValue.CreateNull()
                : new JArray();

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Does.Contain(newerField));
    }

    /// <summary>当前 v5 必须严格拒绝缺字段、负 Gold、非法或重复实例与越界容量。</summary>
    [TestCase(HoldingsShapeViolation.MissingRelics)]
    [TestCase(HoldingsShapeViolation.NullPotions)]
    [TestCase(HoldingsShapeViolation.NegativeGold)]
    [TestCase(HoldingsShapeViolation.NonPositiveRelicInstance)]
    [TestCase(HoldingsShapeViolation.DuplicateRelicInstance)]
    [TestCase(HoldingsShapeViolation.DuplicateRelicTemplate)]
    [TestCase(HoldingsShapeViolation.NonPositivePotionInstance)]
    [TestCase(HoldingsShapeViolation.DuplicatePotionInstance)]
    [TestCase(HoldingsShapeViolation.TooManyPotions)]
    public void Read_InvalidHoldingsShape_ReturnsInvalidDocument(
        HoldingsShapeViolation violation)
    {
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(
            CreateInitialDocument("36363636-aaaa-bbbb-cccc-363636363636")));
        var relicA = new JObject
        {
            ["instanceId"] = 1,
            ["templateId"] = 4101,
        };
        var relicB = new JObject
        {
            ["instanceId"] = 2,
            ["templateId"] = 4102,
        };
        var potionA = new JObject
        {
            ["instanceId"] = 1,
            ["templateId"] = 5101,
        };
        var potionB = new JObject
        {
            ["instanceId"] = 2,
            ["templateId"] = 5102,
        };
        switch (violation)
        {
            case HoldingsShapeViolation.MissingRelics:
                raw.Remove("relics");
                break;
            case HoldingsShapeViolation.NullPotions:
                raw["potions"] = JValue.CreateNull();
                break;
            case HoldingsShapeViolation.NegativeGold:
                raw["gold"] = -1;
                break;
            case HoldingsShapeViolation.NonPositiveRelicInstance:
                relicA["instanceId"] = 0;
                raw["relics"] = new JArray(relicA);
                break;
            case HoldingsShapeViolation.DuplicateRelicInstance:
                relicB["instanceId"] = 1;
                raw["relics"] = new JArray(relicA, relicB);
                break;
            case HoldingsShapeViolation.DuplicateRelicTemplate:
                relicB["templateId"] = 4101;
                raw["relics"] = new JArray(relicA, relicB);
                break;
            case HoldingsShapeViolation.NonPositivePotionInstance:
                potionA["instanceId"] = 0;
                raw["potions"] = new JArray(potionA);
                break;
            case HoldingsShapeViolation.DuplicatePotionInstance:
                potionB["instanceId"] = 1;
                raw["potions"] = new JArray(potionA, potionB);
                break;
            case HoldingsShapeViolation.TooManyPotions:
                raw["potions"] = new JArray(
                    potionA,
                    potionB,
                    new JObject { ["instanceId"] = 3, ["templateId"] = 5103 },
                    new JObject { ["instanceId"] = 4, ["templateId"] = 5104 });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(violation), violation, null);
        }

        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(read.Document, Is.Null);
        Assert.That(read.Detail, Is.Not.Empty);
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

    public enum MissingRunItemReference
    {
        HeldRelic,
        HeldPotion,
        AttachedRelic,
        AttachedPotion,
    }

    public enum HoldingsShapeViolation
    {
        MissingRelics,
        NullPotions,
        NegativeGold,
        NonPositiveRelicInstance,
        DuplicateRelicInstance,
        DuplicateRelicTemplate,
        NonPositivePotionInstance,
        DuplicatePotionInstance,
        TooManyPotions,
    }

    public enum NodeVisitShapeViolation
    {
        MissingTopLevelField,
        MissingNullablePayloadField,
        MismatchedKind,
        MultiplePayloads,
        ForgedVisitId,
        DuplicateShopEntryId,
        NumericNodeKind,
        NumericStockKind,
    }

    public enum AttachedLootShapeViolation
    {
        MissingObject,
        NullObject,
        MissingRelicField,
        MissingPotionField,
        NonPositiveRelic,
        NonPositivePotion,
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

    /// <summary>从当前稳定文档建立一个字段齐全、可单独改写的 canonical Pending reward JSON。</summary>
    private static JObject CreateCanonicalPendingRewardJson(
        string runId,
        int attemptSequence)
    {
        if (attemptSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptSequence));

        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(
            CreateInitialDocument(runId)));
        const string nodeId = "L01-S00";
        raw["progressPhase"] = nameof(RunSaveProgressPhase.RewardPending);
        raw["committedNodeId"] = nodeId;
        raw["pendingCardReward"] = new JObject
        {
            ["rewardId"] =
                $"{Guid.ParseExact(runId, "D"):N}:{attemptSequence}:{nodeId}",
            ["candidateTemplateIds"] = new JArray(3105, 3123, 3157),
            ["attachedLoot"] = new JObject
            {
                ["relicTemplateId"] = 8001,
                ["potionTemplateId"] = 9001,
            },
        };
        return raw;
    }

    /// <summary>建立携带指定四类 payload 之一的完整 NodeVisitPending 测试文档。</summary>
    private static RunSaveDocument CreateNodeVisitDocument(
        string runId,
        MapNodeKind kind)
    {
        RunSaveDocument source = CreateInitialDocument(runId);
        const string nodeId = "L01-S00";
        RunSaveRestNodeVisitPayloadDocument restPayload = null;
        RunSaveChestNodeVisitPayloadDocument chestPayload = null;
        RunSaveShopNodeVisitPayloadDocument shopPayload = null;
        RunSaveEventNodeVisitPayloadDocument eventPayload = null;
        switch (kind)
        {
            case MapNodeKind.Rest:
                restPayload = new RunSaveRestNodeVisitPayloadDocument(
                    healAmount: 24,
                    upgradeCandidateInstanceIds: new[] { 1, 2 });
                break;
            case MapNodeKind.Chest:
                chestPayload = new RunSaveChestNodeVisitPayloadDocument(
                    potionTemplateId: 5101);
                break;
            case MapNodeKind.Shop:
                shopPayload = new RunSaveShopNodeVisitPayloadDocument(new[]
                {
                    new RunSaveShopStockEntryDocument(
                        entryId: 1,
                        RunShopStockKind.Relic,
                        templateId: 4101,
                        price: 150,
                        purchased: false),
                    new RunSaveShopStockEntryDocument(
                        entryId: 2,
                        RunShopStockKind.Potion,
                        templateId: 5101,
                        price: 60,
                        purchased: true),
                    new RunSaveShopStockEntryDocument(
                        entryId: 3,
                        RunShopStockKind.Card,
                        templateId: 3105,
                        price: 75,
                        purchased: false),
                });
                break;
            case MapNodeKind.Event:
                eventPayload = new RunSaveEventNodeVisitPayloadDocument(
                    gainGoldAmount: 45,
                    paidHealCost: 30,
                    paidHealAmount: 18);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var pendingNodeVisit = new RunSavePendingNodeVisitDocument(
            visitId: $"{source.RunId}/{nodeId}",
            nodeId,
            contentId: 7201,
            kind,
            restPayload,
            chestPayload,
            shopPayload,
            eventPayload);
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            source.PathNodeIds,
            RunSaveProgressPhase.NodeVisitPending,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            source.Relics,
            source.Potions,
            source.Gold,
            pendingNodeVisit);
    }

    /// <summary>建立与指定非战斗 kind 匹配的领域 Pending 访问。</summary>
    private static PendingRunNodeVisit CreatePendingRunNodeVisit(
        MapNodeKind kind,
        RunNodeVisitId visitId,
        int contentId)
    {
        switch (kind)
        {
            case MapNodeKind.Rest:
                return PendingRunNodeVisit.CreateRest(
                    visitId,
                    contentId,
                    healAmount: 24,
                    upgradeCandidateInstanceIds: new[]
                    {
                        new RunCardInstanceId(1),
                        new RunCardInstanceId(2),
                    });
            case MapNodeKind.Chest:
                return PendingRunNodeVisit.CreateChest(
                    visitId,
                    contentId,
                    potionTemplateId: 5101);
            case MapNodeKind.Shop:
                return PendingRunNodeVisit.CreateShop(
                    visitId,
                    contentId,
                    new[]
                    {
                        new RunShopStockEntry(1, RunShopStockKind.Relic, 4101, 150, false),
                        new RunShopStockEntry(2, RunShopStockKind.Potion, 5101, 60, true),
                        new RunShopStockEntry(3, RunShopStockKind.Card, 3105, 75, false),
                    });
            case MapNodeKind.Event:
                return PendingRunNodeVisit.CreateEvent(
                    visitId,
                    contentId,
                    gainGoldAmount: 45,
                    paidHealCost: 30,
                    paidHealAmount: 18);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>建立只含 Start 与一个直接可达非战斗节点的最小 Mapper 测试地图。</summary>
    private static MapDefinition CreateSingleNonCombatMap(
        MapNodeKind kind,
        int contentId)
    {
        MapNodeId startNodeId = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId destinationNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        return new MapDefinition(
            profileId: "tinyspire.test.persistence.noncombat.v1",
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
        IReadOnlyList<RunSaveCardDocument> runCards = null,
        IReadOnlyList<RunSaveRelicDocument> relics = null,
        IReadOnlyList<RunSavePotionDocument> potions = null,
        RunSavePendingNodeVisitDocument pendingNodeVisit = null)
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
            pendingCardReward ?? source.PendingCardReward,
            relics ?? source.Relics,
            potions ?? source.Potions,
            source.Gold,
            pendingNodeVisit ?? source.PendingNodeVisit);
    }

    /// <summary>沿 mixed 单路径恢复到目标节点前一层，再通过生产工厂冻结真实 Pending 文档。</summary>
    private static RunSaveDocument CreateAuthoritativeMixedPendingDocument(
        string runId,
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
        var catalog = new ExistingConfigurationCatalog(profile: profile);
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

        RunNodeVisitEntrySettlement pending = store.PreviewNodeVisitEntry(
            target.Id,
            catalog);
        return RunSaveDocumentMapper.Create(pending.Successor);
    }

    /// <summary>只改写一种 payload 权威字段，同时保持 DTO 形状与访问身份均合法。</summary>
    private static RunSavePendingNodeVisitDocument ForgePendingNodeVisitPayload(
        RunSavePendingNodeVisitDocument source)
    {
        RunSaveRestNodeVisitPayloadDocument rest = source.RestPayload;
        RunSaveChestNodeVisitPayloadDocument chest = source.ChestPayload;
        RunSaveShopNodeVisitPayloadDocument shop = source.ShopPayload;
        RunSaveEventNodeVisitPayloadDocument eventPayload = source.EventPayload;
        switch (source.Kind)
        {
            case MapNodeKind.Rest:
                rest = new RunSaveRestNodeVisitPayloadDocument(
                    source.RestPayload.HealAmount + 1,
                    source.RestPayload.UpgradeCandidateInstanceIds);
                break;
            case MapNodeKind.Chest:
                chest = new RunSaveChestNodeVisitPayloadDocument(
                    source.ChestPayload.PotionTemplateId + 1);
                break;
            case MapNodeKind.Shop:
                RunSaveShopStockEntryDocument[] entries = source.ShopPayload.Entries
                    .Select((entry, index) => new RunSaveShopStockEntryDocument(
                        entry.EntryId,
                        entry.Kind,
                        entry.TemplateId,
                        index == 0 ? entry.Price + 1 : entry.Price,
                        entry.Purchased))
                    .ToArray();
                shop = new RunSaveShopNodeVisitPayloadDocument(entries);
                break;
            case MapNodeKind.Event:
                eventPayload = new RunSaveEventNodeVisitPayloadDocument(
                    source.EventPayload.GainGoldAmount + 1,
                    source.EventPayload.PaidHealCost,
                    source.EventPayload.PaidHealAmount);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source.Kind), source.Kind, null);
        }

        return new RunSavePendingNodeVisitDocument(
            source.VisitId,
            source.NodeId,
            source.ContentId,
            source.Kind,
            rest,
            chest,
            shop,
            eventPayload);
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
            source.PendingCardReward,
            source.Relics,
            source.Potions,
            source.Gold,
            source.PendingNodeVisit);
    }

    private sealed class ExistingConfigurationCatalog :
        IRunSaveConfigurationCatalog,
        IRunCardUpgradeConfigurationCatalog,
        IRunNodeVisitEntryCatalog
    {
        private readonly int _heroMaxHealth;
        private readonly ActMapProfile _profile;
        private readonly int? _missingCardTemplateId;
        private readonly int? _missingRelicTemplateId;
        private readonly int? _missingPotionTemplateId;

        /// <summary>建立测试配置目录，并允许替换 Hero 上限、同 ID profile 或指定缺失 Card。</summary>
        public ExistingConfigurationCatalog(
            int heroMaxHealth = 80,
            ActMapProfile profile = null,
            int? missingCardTemplateId = null,
            int? missingRelicTemplateId = null,
            int? missingPotionTemplateId = null)
        {
            _heroMaxHealth = heroMaxHealth;
            _profile = profile ?? TinySpireActMapProfiles.Current;
            _missingCardTemplateId = missingCardTemplateId;
            _missingRelicTemplateId = missingRelicTemplateId;
            _missingPotionTemplateId = missingPotionTemplateId;
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

        /// <summary>除测试指定缺失模板外，其余 Relic 均视为存在。</summary>
        public bool RelicExists(int templateId)
        {
            return templateId != _missingRelicTemplateId;
        }

        /// <summary>除测试指定缺失模板外，其余 Potion 均视为存在。</summary>
        public bool PotionExists(int templateId)
        {
            return templateId != _missingPotionTemplateId;
        }

        /// <summary>按稳定顺序返回与测试奖励合法性一致的 Hero 商店卡牌候选。</summary>
        public HeroCardRewardPool CreateHeroCardRewardPool(int heroTemplateId)
        {
            return new HeroCardRewardPool(
                heroTemplateId,
                new CardRewardRarityWeights(60, 37, 3),
                new[]
                {
                    new CardRewardCandidate(3105, cfg.battle.CardRarity.Common),
                    new CardRewardCandidate(3123, cfg.battle.CardRarity.Uncommon),
                    new CardRewardCandidate(3157, cfg.battle.CardRarity.Rare),
                });
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

        /// <summary>旧选择性配置测试不缺少 Relic 模板。</summary>
        public bool RelicExists(int templateId)
        {
            return true;
        }

        /// <summary>旧选择性配置测试不缺少 Potion 模板。</summary>
        public bool PotionExists(int templateId)
        {
            return true;
        }

        /// <summary>始终返回当前支持的固定 Act profile。</summary>
        public ActMapProfile GetActMapProfile(string profileId)
        {
            return TinySpireActMapProfiles.GetById(profileId);
        }
    }
}
