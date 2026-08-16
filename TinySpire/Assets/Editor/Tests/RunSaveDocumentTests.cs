using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Run;

public sealed class RunSaveDocumentTests
{
    /// <summary>初始地图稳定态 S0 经显式文档映射后可恢复全部 Run 事实，且不产生战斗中间态。</summary>
    [Test]
    public void StableS0_RoundTrip_RestoresEquivalentRunWithoutBattleFacts()
    {
        using var sourceStore = new RunStateStore();
        RunState source = sourceStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            heroTemplateId: 1001,
            initialHealth: 63,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 987654321u));

        RunSaveDocument document = RunSaveDocumentMapper.Create(source);
        RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
            document,
            new ExistingConfigurationCatalog());
        using var restoredStore = new RunStateStore();
        RunState restored = restoredStore.RestoreRun(restore.Options);

        Assert.That(document.SchemaVersion, Is.EqualTo(RunSaveDocument.CurrentSchemaVersion));
        Assert.That(restore.Status, Is.EqualTo(RunSaveRestoreStatus.Success));
        Assert.That(restored.RunId, Is.EqualTo(source.RunId));
        Assert.That(restored.HeroTemplateId, Is.EqualTo(source.HeroTemplateId));
        Assert.That(restored.CurrentHealth, Is.EqualTo(source.CurrentHealth));
        Assert.That(restored.MaxHealth, Is.EqualTo(source.MaxHealth));
        Assert.That(restored.DeckTemplateId, Is.EqualTo(source.DeckTemplateId));
        Assert.That(restored.EncounterTemplateId, Is.EqualTo(source.EncounterTemplateId));
        Assert.That(restored.RandomRootSeed, Is.EqualTo(source.RandomRootSeed));
        Assert.That(restored.NodeStatus, Is.EqualTo(RunNodeStatus.Available));
        Assert.That(restored.BattleAttemptSequence, Is.Zero);
        Assert.That(restored.ActiveBattle, Is.Null);
        Assert.That(restored.BattleSnapshot, Is.Null);
    }

    /// <summary>节点完成稳定态 S1 的 v1 JSON 只公开白名单事实，并可经 codec 无损读回。</summary>
    [Test]
    public void StableS1_JsonRoundTrip_ContainsOnlyExplicitStableFacts()
    {
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            heroTemplateId: 1002,
            initialHealth: 70,
            maxHealth: 70,
            deckTemplateId: 1002,
            encounterTemplateId: 5001,
            randomRootSeed: 246813579u));
        RunBattleInput battle = store.BeginBattle();
        RunState completed = store.ApplyVictory(
            battle.BattleId,
            heroTemplateId: 1002,
            settledHealth: 17,
            maxHealth: 70);

        RunSaveDocument expected = RunSaveDocumentMapper.Create(completed);
        string json = RunSaveDocumentCodec.Serialize(expected);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(json);
        JObject raw = JObject.Parse(json);

        Assert.That(read.Status, Is.EqualTo(RunSaveDocumentReadStatus.Success));
        Assert.That(read.Document.RunId, Is.EqualTo(expected.RunId));
        Assert.That(read.Document.CurrentHealth, Is.EqualTo(17));
        Assert.That(read.Document.NodeStatus, Is.EqualTo(RunSaveNodeStatus.Completed));
        Assert.That(read.Document.BattleAttemptSequence, Is.EqualTo(1));
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
                "encounterTemplateId",
                "randomRootSeed",
                "nodeStatus",
                "battleAttemptSequence",
            }));
        Assert.That(json, Does.Not.Contain("ActiveBattle").IgnoreCase);
        Assert.That(json, Does.Not.Contain("BattleSnapshot").IgnoreCase);
        Assert.That(json, Does.Not.Contain("BattleSession").IgnoreCase);
    }

    /// <summary>内存 fake 通过同一公共 port 表达单槽 commit/load/delete，不泄漏文件系统语义。</summary>
    [Test]
    public void InMemorySaveStore_CommitLoadDelete_UsesTypedSingleSlotContract()
    {
        using var sourceStore = new RunStateStore();
        RunSaveDocument document = RunSaveDocumentMapper.Create(
            sourceStore.CreateNewRun(new RunCreationOptions(
                new RunId(Guid.Parse("99999999-8888-7777-6666-555555555555")),
                heroTemplateId: 1001,
                initialHealth: 80,
                maxHealth: 80,
                deckTemplateId: 1001,
                encounterTemplateId: 5001,
                randomRootSeed: 12345u)));
        IRunSaveStore saves = new InMemoryRunSaveStore();

        RunSaveCommitResult commit = saves.Commit(document);
        RunSaveLoadResult load = saves.Load();
        RunSaveDeleteResult delete = saves.Delete();
        RunSaveLoadResult afterDelete = saves.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.RunId, Is.EqualTo(document.RunId));
        Assert.That(delete.Status, Is.EqualTo(RunSaveDeleteStatus.Success));
        Assert.That(afterDelete.Status, Is.EqualTo(RunSaveLoadStatus.NotFound));
    }

    /// <summary>v1 节点状态必须使用可审阅的字符串，数字枚举不能绕过 schema 约束。</summary>
    [Test]
    public void Read_NumericNodeStatus_ReturnsInvalidDocument()
    {
        using var sourceStore = new RunStateStore();
        RunSaveDocument document = RunSaveDocumentMapper.Create(
            sourceStore.CreateNewRun(new RunCreationOptions(
                new RunId(Guid.Parse("12345678-1234-1234-1234-123456789abc")),
                heroTemplateId: 1001,
                initialHealth: 80,
                maxHealth: 80,
                deckTemplateId: 1001,
                encounterTemplateId: 5001,
                randomRootSeed: 777u)));
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(document));
        raw["nodeStatus"] = 0;

        RunSaveDocumentReadResult result = RunSaveDocumentCodec.Read(raw.ToString());

        Assert.That(result.Status, Is.EqualTo(RunSaveDocumentReadStatus.InvalidDocument));
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Detail, Does.Contain("nodeStatus"));
    }

    /// <summary>坏 JSON 与未知 schema 必须保持不同错误分类，且都不得产生可继续文档。</summary>
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

    /// <summary>任一当前配置 ID 缺失时，恢复必须说明具体类别并拒绝默认值回退。</summary>
    [TestCase(MissingConfiguration.Hero, RunSaveRestoreStatus.MissingHeroTemplate)]
    [TestCase(MissingConfiguration.Deck, RunSaveRestoreStatus.MissingDeckTemplate)]
    [TestCase(MissingConfiguration.Encounter, RunSaveRestoreStatus.MissingEncounterTemplate)]
    public void CreateRestore_MissingConfigurationId_ReturnsExplicitFailure(
        MissingConfiguration missing,
        RunSaveRestoreStatus expectedStatus)
    {
        using var sourceStore = new RunStateStore();
        RunSaveDocument document = RunSaveDocumentMapper.Create(
            sourceStore.CreateNewRun(new RunCreationOptions(
                new RunId(Guid.Parse("abcdefab-cdef-abcd-efab-cdefabcdefab")),
                heroTemplateId: 1001,
                initialHealth: 80,
                maxHealth: 80,
                deckTemplateId: 1001,
                encounterTemplateId: 5001,
                randomRootSeed: 314159u)));

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            document,
            new SelectiveConfigurationCatalog(missing));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
    }

    /// <summary>存档生命上限与当前 Hero 配置漂移时必须在冷读档阶段拒绝，不能延迟到 Battle 卡死。</summary>
    [Test]
    public void CreateRestore_HeroMaxHealthDiffersFromCurrentConfiguration_ReturnsInvalidDocument()
    {
        var document = new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            "01234567-89ab-cdef-0123-456789abcdef",
            heroTemplateId: 1001,
            currentHealth: 30,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 42u,
            RunSaveNodeStatus.Available,
            battleAttemptSequence: 0);

        RunSaveRestoreResult result = RunSaveDocumentMapper.CreateRestore(
            document,
            new ExistingConfigurationCatalog(heroMaxHealth: 30));

        Assert.That(result.Status, Is.EqualTo(RunSaveRestoreStatus.InvalidDocument));
        Assert.That(result.Options, Is.Null);
        Assert.That(result.Detail, Does.Contain("max health").IgnoreCase);
    }

    /// <summary>InBattle 与 Failed 都不是地图稳定态，映射器必须拒绝持久化。</summary>
    [Test]
    public void Create_InBattleOrFailedState_ThrowsWithoutCreatingDocument()
    {
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("fedcbafe-dcba-fedc-bafe-dcbafedcbafe")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 271828u));
        RunBattleInput battle = store.BeginBattle();

        Assert.Throws<InvalidOperationException>(() =>
            RunSaveDocumentMapper.Create(store.Current));

        store.RecordDefeat(
            battle.BattleId,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80);

        Assert.Throws<InvalidOperationException>(() =>
            RunSaveDocumentMapper.Create(store.Current));
    }

    public enum MissingConfiguration
    {
        Hero,
        Deck,
        Encounter,
    }

    private sealed class ExistingConfigurationCatalog : IRunSaveConfigurationCatalog
    {
        private readonly int _heroMaxHealth;

        /// <summary>建立具有指定当前 Hero 生命上限的完整测试配置目录。</summary>
        public ExistingConfigurationCatalog(int heroMaxHealth = 80)
        {
            _heroMaxHealth = heroMaxHealth;
        }

        /// <summary>首个 tracer bullet 的 Hero 配置全部视为存在。</summary>
        public bool HeroExists(int templateId)
        {
            return true;
        }

        /// <summary>返回测试指定的当前 Hero 生命上限。</summary>
        public int GetHeroMaxHealth(int templateId)
        {
            return _heroMaxHealth;
        }

        /// <summary>首个 tracer bullet 的 Deck 配置全部视为存在。</summary>
        public bool DeckExists(int templateId)
        {
            return true;
        }

        /// <summary>首个 tracer bullet 的 Encounter 配置全部视为存在。</summary>
        public bool EncounterExists(int templateId)
        {
            return true;
        }
    }

    private sealed class SelectiveConfigurationCatalog : IRunSaveConfigurationCatalog
    {
        private readonly MissingConfiguration _missing;

        /// <summary>建立只缺少一种指定配置的测试目录。</summary>
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
    }
}
