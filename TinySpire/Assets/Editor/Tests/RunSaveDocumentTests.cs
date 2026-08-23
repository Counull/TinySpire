using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class RunSaveDocumentTests
{
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

    /// <summary>schema v2 JSON 严格限制为地图配方与稳定进度，不落整图、UI 或可达性派生结果。</summary>
    [Test]
    public void MapReady_JsonRoundTrip_ContainsOnlyRecipeAndStableProgressFacts()
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
                "deckTemplateId",
                "randomRootSeed",
                "mapProfileId",
                "mapGeneratorVersion",
                "mapSeed",
                "mapFingerprint",
                "pathNodeIds",
                "progressPhase",
                "committedNodeId",
                "terminalReason",
            }));
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

    /// <summary>无重试后 attempt 是路径派生值，schema v2 必须拒绝外部夹带第二份事实。</summary>
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

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            document,
            new SelectiveConfigurationCatalog(missing));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
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

    /// <summary>progressPhase 必须使用可审阅的精确字符串，数字枚举不能绕过 schema v2。</summary>
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
            deckTemplateId: 1001,
            randomRootSeed: 987654321u,
            map));
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
        return store.ApplyVictory(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth,
            maxHealth: 80);
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
        string[] pathNodeIds = null)
    {
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            source.DeckTemplateId,
            source.RandomRootSeed,
            mapProfileId ?? source.MapProfileId,
            mapGeneratorVersion ?? source.MapGeneratorVersion,
            source.MapSeed,
            mapFingerprint ?? source.MapFingerprint,
            pathNodeIds ?? source.PathNodeIds,
            source.ProgressPhase,
            source.CommittedNodeId,
            source.TerminalReason);
    }

    private sealed class ExistingConfigurationCatalog : IRunSaveConfigurationCatalog
    {
        private readonly int _heroMaxHealth;
        private readonly ActMapProfile _profile;

        /// <summary>建立完整测试配置目录，并允许替换当前 Hero 上限或同 ID profile。</summary>
        public ExistingConfigurationCatalog(
            int heroMaxHealth = 80,
            ActMapProfile profile = null)
        {
            _heroMaxHealth = heroMaxHealth;
            _profile = profile ?? TinySpireActMapProfiles.Current;
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
