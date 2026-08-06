using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// 校验 STS2 v0.107.1 战士单人卡牌目录的冻结身份集合。
/// </summary>
public sealed class IroncladCatalogSnapshotI3Tests
{
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string CardEffectTableJsonPath = "Assets/GameData/battle_tbcardeffect.json";
    private const string SnapshotKey = "sts2-v0.107.1-23811903-59260271";

    private static readonly string[] ExpectedExternalKeys =
    {
        "AGGRESSION",
        "ANGER",
        "ARMAMENTS",
        "ASHEN_STRIKE",
        "BARRICADE",
        "BASH",
        "BATTLE_TRANCE",
        "BLOOD_WALL",
        "BLOODLETTING",
        "BLUDGEON",
        "BODY_SLAM",
        "BRAND",
        "BREAK",
        "BREAKTHROUGH",
        "BULLY",
        "BURNING_PACT",
        "CASCADE",
        "CINDER",
        "COLOSSUS",
        "CONFLAGRATION",
        "CORRUPTION",
        "CRIMSON_MANTLE",
        "CRUELTY",
        "DARK_EMBRACE",
        "DEFEND_IRONCLAD",
        "DEMON_FORM",
        "DISMANTLE",
        "DOMINATE",
        "DRUM_OF_BATTLE",
        "EVIL_EYE",
        "EXPECT_A_FIGHT",
        "FEED",
        "FEEL_NO_PAIN",
        "FIEND_FIRE",
        "FIGHT_ME",
        "FLAME_BARRIER",
        "FORGOTTEN_RITUAL",
        "HAVOC",
        "HEADBUTT",
        "HELLRAISER",
        "HEMOKINESIS",
        "HOWL_FROM_BEYOND",
        "IMPERVIOUS",
        "INFERNAL_BLADE",
        "INFERNO",
        "INFLAME",
        "IRON_WAVE",
        "JUGGERNAUT",
        "JUGGLING",
        "MANGLE",
        "MOLTEN_FIST",
        "NOT_YET",
        "OFFERING",
        "ONE_TWO_PUNCH",
        "PACTS_END",
        "PERFECTED_STRIKE",
        "PILLAGE",
        "POMMEL_STRIKE",
        "PRIMAL_FORCE",
        "PYRE",
        "RAGE",
        "RAMPAGE",
        "RUPTURE",
        "SECOND_WIND",
        "SETUP_STRIKE",
        "SHRUG_IT_OFF",
        "SPITE",
        "STAMPEDE",
        "STOKE",
        "STOMP",
        "STONE_ARMOR",
        "STRIKE_IRONCLAD",
        "SWORD_BOOMERANG",
        "TAUNT",
        "TEAR_ASUNDER",
        "THRASH",
        "THUNDERCLAP",
        "TREMBLE",
        "TRUE_GRIT",
        "TWIN_STRIKE",
        "UNMOVABLE",
        "UNRELENTING",
        "UPPERCUT",
        "VICIOUS",
        "WHIRLWIND"
    };

    /// <summary>确认生成目录恰好包含冻结版本的 85 个单人外部身份。</summary>
    [Test]
    public void GeneratedCatalog_ContainsExactV01071SoloIroncladExternalKeys()
    {
        JObject cards = JObject.Parse(File.ReadAllText(CardTableJsonPath));
        string[] actual = cards.Properties()
            .Where(card =>
                string.Equals(
                    card.Value.Value<string>("catalog_snapshot_key"),
                    SnapshotKey,
                    StringComparison.Ordinal))
            .Select(card => card.Value.Value<string>("external_key"))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            actual.Length,
            Is.EqualTo(ExpectedExternalKeys.Length),
            $"冻结快照应有 {ExpectedExternalKeys.Length} 张卡，实际为 {actual.Length} 张。");
        Assert.That(
            actual,
            Is.EqualTo(ExpectedExternalKeys.OrderBy(
                key => key,
                StringComparer.Ordinal)));
    }

    /// <summary>确认 Tremble 以三层易伤程序和消耗归宿进入可玩目录。</summary>
    [Test]
    public void GeneratedTremble_IsImplementedWithVulnerableThreeAndExhaustDestination()
    {
        JObject cards = LoadCards();
        JToken tremble = FindCardByExternalKey(cards, "TREMBLE").Value;

        Assert.That(
            tremble.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(tremble.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(
            tremble.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(
            tremble.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(
            tremble.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(tremble.Value<string>("illustration_key"), Is.EqualTo("art_placeholder"));

        JArray bindings = (JArray)tremble["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(1));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("vulnerable"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4006));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        JToken vulnerable = effects["4006"];
        Assert.That(vulnerable, Is.Not.Null);
        Assert.That(
            vulnerable.Value<int>("effect_type"),
            Is.EqualTo((int)cfg.battle.EffectType.ApplyVulnerable));
        Assert.That(
            vulnerable.Value<int>("attribute"),
            Is.EqualTo((int)cfg.battle.Attribute.None));
        Assert.That(vulnerable.Value<int>("value"), Is.EqualTo(3));
    }

    /// <summary>确认构建门禁会精确报告冻结快照缺失的外部身份。</summary>
    [Test]
    public void SnapshotValidator_MissingExternalIdentity_Throws()
    {
        JObject cards = LoadCards();
        JProperty anger = FindCardByExternalKey(cards, "ANGER");
        anger.Remove();

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateIroncladV01071Snapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Ironclad snapshot missing external keys: ANGER"));
    }

    /// <summary>确认全表外部身份重复时不会被快照计数掩盖。</summary>
    [Test]
    public void SnapshotValidator_DuplicateExternalIdentity_Throws()
    {
        JObject cards = LoadCards();
        FindCardByExternalKey(cards, "TINY_SPIRE_STRENGTH").Value["external_key"] = "BASH";

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateIroncladV01071Snapshot(cards));

        Assert.That(failure.Message, Is.EqualTo("Duplicate external_key 'BASH'."));
    }

    /// <summary>确认 X 费身份集合漂移时构建门禁会失败。</summary>
    [Test]
    public void SnapshotValidator_XCostIdentityDrifts_Throws()
    {
        JObject cards = LoadCards();
        FindCardByExternalKey(cards, "WHIRLWIND").Value["cost_kind"] =
            (int)cfg.battle.CardCostKind.Fixed;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateIroncladV01071Snapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Ironclad X-cost external keys drifted: CASCADE"));
    }

    /// <summary>确认 4/81 总数不变时，可玩身份互换仍会被构建门禁拒绝。</summary>
    [Test]
    public void SnapshotValidator_ImplementedIdentitySwappedWithoutCountDrift_Throws()
    {
        JObject cards = LoadCards();
        FindCardByExternalKey(cards, "TREMBLE").Value["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.CatalogOnly;
        FindCardByExternalKey(cards, "ANGER").Value["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.Implemented;

        JToken[] snapshotCards = cards.Properties()
            .Select(property => property.Value)
            .Where(card => string.Equals(
                card.Value<string>("catalog_snapshot_key"),
                SnapshotKey,
                StringComparison.Ordinal))
            .ToArray();
        Assert.That(
            snapshotCards.Count(card =>
                card.Value<int>("implementation_status")
                == (int)cfg.battle.CardImplementationStatus.Implemented),
            Is.EqualTo(4));
        Assert.That(
            snapshotCards.Count(card =>
                card.Value<int>("implementation_status")
                == (int)cfg.battle.CardImplementationStatus.CatalogOnly),
            Is.EqualTo(81));

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateIroncladV01071Snapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo(
                "Ironclad Implemented external keys drifted: " +
                "missing [TREMBLE]; unexpected [ANGER]."));
    }

    /// <summary>确认全部目录名称、基础说明与升级说明都已同步为 en/zh-CN Smart String。</summary>
    [Test]
    public void GeneratedCatalog_LocalizationSourceAndAssetsPassFullValidation()
    {
        Assert.DoesNotThrow(LocalizationBuildTools.ValidateBattleCardText);
    }

    /// <summary>读取并深拷贝当前生成牌表，供漂移测试独立修改。</summary>
    private static JObject LoadCards()
    {
        return JObject.Parse(File.ReadAllText(CardTableJsonPath));
    }

    /// <summary>按外部身份定位唯一的卡牌记录。</summary>
    private static JProperty FindCardByExternalKey(JObject cards, string externalKey)
    {
        return cards.Properties().Single(property =>
            string.Equals(
                property.Value.Value<string>("external_key"),
                externalKey,
                StringComparison.Ordinal));
    }
}
