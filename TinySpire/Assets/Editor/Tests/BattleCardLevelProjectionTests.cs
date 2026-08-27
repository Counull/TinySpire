using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

/// <summary>验证 G4-D 卡牌等级配置投影的有限、无限与拒绝语义。</summary>
public sealed class BattleCardLevelProjectionTests
{
    /// <summary>有限伤害轨道必须按明确等级行替换展示与效果值，并在轨道末端拒绝继续升级。</summary>
    [Test]
    public void Create_ProductionFiniteDamageCard_UsesExactConfiguredLevel()
    {
        Tables tables = LoadProductionTables();

        BattleCardLevelProjection levelZero = BattleCardLevelProjection.Create(tables, 3002, 0);
        BattleCardLevelProjection levelOne = BattleCardLevelProjection.Create(tables, 3002, 1);

        Assert.That(levelZero.Template.Id, Is.EqualTo(3002));
        Assert.That(levelZero.DescriptionI18nKey, Is.EqualTo("battle.card.strike.description"));
        Assert.That(levelZero.Cost, Is.EqualTo(1));
        Assert.That(levelZero.PlayDestination, Is.EqualTo(cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(levelZero.EffectDamageValue, Is.EqualTo(6));
        Assert.That(levelZero.ProgramDamageValue, Is.Null);
        Assert.That(levelZero.CanUpgradeOneLevel, Is.True);

        Assert.That(levelOne.DescriptionI18nKey, Is.EqualTo("battle.card.strike.upgrade_description"));
        Assert.That(levelOne.Cost, Is.EqualTo(1));
        Assert.That(levelOne.PlayDestination, Is.EqualTo(cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(levelOne.EffectDamageValue, Is.EqualTo(9));
        Assert.That(levelOne.ProgramDamageValue, Is.Null);
        Assert.That(levelOne.CanUpgradeOneLevel, Is.False);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, 3002, 0), Is.True);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, 3002, 1), Is.True);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, 3002, 2), Is.False);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleCardLevelProjection.Create(tables, 3002, 2));
    }

    /// <summary>有限无数值规则轨道仍必须从明确等级行投影费用与去向，且不得伪造伤害值。</summary>
    [Test]
    public void Create_ProductionFiniteRuleNoneCard_ProjectsCostAndDestinationOnly()
    {
        Tables tables = LoadProductionTables();

        BattleCardLevelProjection projection = BattleCardLevelProjection.Create(tables, 3207, 1);

        Assert.That(projection.Template.Id, Is.EqualTo(3207));
        Assert.That(
            projection.DescriptionI18nKey,
            Is.EqualTo("battle.card.marine.output_adjust.upgrade_description"));
        Assert.That(projection.Cost, Is.Zero);
        Assert.That(projection.PlayDestination, Is.EqualTo(cfg.battle.CardPlayDestination.Power));
        Assert.That(projection.EffectDamageValue, Is.Null);
        Assert.That(projection.ProgramDamageValue, Is.Null);
        Assert.That(projection.CanUpgradeOneLevel, Is.False);
    }

    /// <summary>无限轨道必须把类型化每级增量分别累加到效果伤害与程序伤害的生产基值。</summary>
    [Test]
    public void Create_ProductionInfiniteCards_AccumulatesTypedDamagePerLevel()
    {
        Tables tables = LoadProductionTables();

        BattleCardLevelProjection effectCard = BattleCardLevelProjection.Create(tables, 3123, 3);
        BattleCardLevelProjection programCard = BattleCardLevelProjection.Create(tables, 3201, 4);

        Assert.That(effectCard.Template.Id, Is.EqualTo(3123));
        Assert.That(effectCard.DescriptionI18nKey, Is.EqualTo(effectCard.Template.UpgradedDescriptionI18nKey));
        Assert.That(effectCard.Cost, Is.EqualTo(effectCard.Template.UpgradedCost));
        Assert.That(effectCard.PlayDestination, Is.EqualTo(effectCard.Template.UpgradedPlayDestination));
        Assert.That(effectCard.EffectDamageValue, Is.EqualTo(62));
        Assert.That(effectCard.ProgramDamageValue, Is.Null);
        Assert.That(effectCard.CanUpgradeOneLevel, Is.True);

        Assert.That(programCard.Template.Id, Is.EqualTo(3201));
        Assert.That(programCard.EffectDamageValue, Is.Null);
        Assert.That(programCard.ProgramDamageValue, Is.EqualTo(18));
        Assert.That(programCard.CanUpgradeOneLevel, Is.True);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, 3201, 4), Is.True);
    }

    /// <summary>多级有限轨道必须从一级开始连续，并按目标等级的明确绝对值投影。</summary>
    [Test]
    public void Create_MultiLevelFiniteTrack_UsesContinuousExplicitSteps()
    {
        const int cardId = 9101;
        Tables tables = CreateTables(
            new[]
            {
                CreateCard(cardId, cfg.battle.CardUpgradeTrackKind.Finite, effectId: 9201),
            },
            new[]
            {
                CreateEffect(9201, value: 5),
            },
            new[]
            {
                CreateUpgrade(cardId, nextLevel: 1, ruleValue: 7),
                CreateUpgrade(cardId, nextLevel: 2, ruleValue: 11),
            });

        BattleCardLevelProjection projection = BattleCardLevelProjection.Create(tables, cardId, 2);

        Assert.That(projection.UpgradeLevel, Is.EqualTo(2));
        Assert.That(projection.DescriptionI18nKey, Is.EqualTo("test.card.9101.level.2"));
        Assert.That(projection.EffectDamageValue, Is.EqualTo(11));
        Assert.That(projection.CanUpgradeOneLevel, Is.False);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, cardId, 2), Is.True);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, cardId, 3), Is.False);
    }

    /// <summary>None 轨道只允许零级，负等级与任何正等级都必须被明确拒绝。</summary>
    [Test]
    public void Create_NoneTrack_AllowsOnlyLevelZero()
    {
        const int cardId = 9102;
        Tables tables = CreateTables(new[]
        {
            CreateCard(cardId, cfg.battle.CardUpgradeTrackKind.None),
        });

        BattleCardLevelProjection projection = BattleCardLevelProjection.Create(tables, cardId, 0);

        Assert.That(projection.UpgradeLevel, Is.Zero);
        Assert.That(projection.CanUpgradeOneLevel, Is.False);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, cardId, -1), Is.False);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, cardId, 1), Is.False);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleCardLevelProjection.Create(tables, cardId, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleCardLevelProjection.Create(tables, cardId, 1));
    }

    /// <summary>None 轨道允许既有多段直伤卡继续沿各 Effect 原值执行，不伪造单一伤害投影。</summary>
    [Test]
    public void Create_NoneTrackWithMultipleDamageBindings_LeavesDamageUnprojected()
    {
        const int cardId = 9106;
        JObject card = CreateCard(cardId, cfg.battle.CardUpgradeTrackKind.None);
        card["effect_bindings"] = new JArray(
            new JObject
            {
                ["argument_key"] = "first_damage",
                ["effect_id"] = 9206,
            },
            new JObject
            {
                ["argument_key"] = "second_damage",
                ["effect_id"] = 9207,
            });
        Tables tables = CreateTables(
            new[] { card },
            new[]
            {
                CreateEffect(9206, value: 3),
                CreateEffect(9207, value: 5),
            });

        BattleCardLevelProjection projection = BattleCardLevelProjection.Create(
            tables,
            cardId,
            upgradeLevel: 0);

        Assert.That(projection.EffectDamageValue, Is.Null);
        Assert.That(projection.ProgramDamageValue, Is.Null);
        Assert.That(projection.CanUpgradeOneLevel, Is.False);
    }

    /// <summary>有限轨道若缺少中间等级，读取任何等级时都必须暴露配置错误。</summary>
    [Test]
    public void Create_FiniteTrackWithGap_RejectsConfiguration()
    {
        const int cardId = 9103;
        Tables tables = CreateTables(
            new[]
            {
                CreateCard(cardId, cfg.battle.CardUpgradeTrackKind.Finite, effectId: 9203),
            },
            new[]
            {
                CreateEffect(9203, value: 5),
            },
            new[]
            {
                CreateUpgrade(cardId, nextLevel: 2, ruleValue: 9),
            });

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            BattleCardLevelProjection.Create(tables, cardId, 0));

        StringAssert.Contains("continuous", failure.Message);
    }

    /// <summary>无限轨道不得同时挂载有限等级行，避免有限轨道结束后暗接无限尾巴。</summary>
    [Test]
    public void Create_InfiniteTrackWithFiniteRow_RejectsMixedTrack()
    {
        const int cardId = 9104;
        Tables tables = CreateTables(
            new[]
            {
                CreateCard(
                    cardId,
                    cfg.battle.CardUpgradeTrackKind.Infinite,
                    cfg.battle.CardUpgradeRuleKind.DamageValue,
                    infiniteValuePerLevel: 2,
                    effectId: 9204),
            },
            new[]
            {
                CreateEffect(9204, value: 5),
            },
            new[]
            {
                CreateUpgrade(cardId, nextLevel: 1, ruleValue: 7),
            });

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            BattleCardLevelProjection.IsUpgradeLevelValid(tables, cardId, 1));

        StringAssert.Contains("finite", failure.Message.ToLowerInvariant());
    }

    /// <summary>无限轨道的乘加若超出 Int32，合法性必须返回 false，实际投影必须抛出溢出。</summary>
    [Test]
    public void Create_InfiniteDamageOverflow_IsExplicitlyRejected()
    {
        const int cardId = 9105;
        Tables tables = CreateTables(
            new[]
            {
                CreateCard(
                    cardId,
                    cfg.battle.CardUpgradeTrackKind.Infinite,
                    cfg.battle.CardUpgradeRuleKind.DamageValue,
                    infiniteValuePerLevel: 2,
                    effectId: 9205),
            },
            new[]
            {
                CreateEffect(9205, int.MaxValue - 1),
            });

        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, cardId, 0), Is.True);
        Assert.That(BattleCardLevelProjection.IsUpgradeLevelValid(tables, cardId, 1), Is.False);
        Assert.Throws<OverflowException>(() =>
            BattleCardLevelProjection.Create(tables, cardId, 1));
    }

    /// <summary>从当前生成 GameData 装载生产卡、效果与升级等级，其余表保持真实结构但不参与投影。</summary>
    private static Tables LoadProductionTables()
    {
        return new Tables(LoadProductionRows);
    }

    /// <summary>兼容生成表的对象索引根与有限升级表的数组根，并统一返回 Luban 行数组。</summary>
    private static JArray LoadProductionRows(string tableName)
    {
        string path = Path.Combine("Assets", "GameData", tableName + ".json");
        JToken table = JToken.Parse(File.ReadAllText(path));
        if (table is JArray rows)
            return rows;
        if (table is JObject indexedRows)
            return new JArray(indexedRows.Properties().Select(property => property.Value));

        throw new InvalidOperationException(
            $"Generated table {tableName} must use an object or array root.");
    }

    /// <summary>建立只含本用例卡牌、效果和升级行的最小 Luban 表集合。</summary>
    private static Tables CreateTables(
        IEnumerable<JObject> cards,
        IEnumerable<JObject> effects = null,
        IEnumerable<JObject> upgrades = null)
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = new JArray(),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(cards),
            ["battle_tbcardeffect"] = new JArray(effects ?? Array.Empty<JObject>()),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = new JArray(),
            ["battle_tbenemybehavior"] = new JArray(),
            ["battle_tbcardupgradelevel"] = new JArray(upgrades ?? Array.Empty<JObject>()),
        };
        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>创建包含 G4 升级字段的最小测试卡牌行。</summary>
    private static JObject CreateCard(
        int id,
        cfg.battle.CardUpgradeTrackKind trackKind,
        cfg.battle.CardUpgradeRuleKind infiniteRuleKind = cfg.battle.CardUpgradeRuleKind.None,
        int infiniteValuePerLevel = 0,
        int? effectId = null)
    {
        return new JObject
        {
            ["id"] = id,
            ["external_key"] = "TEST_" + id,
            ["catalog_snapshot_key"] = "g4-d-test",
            ["name_i18n_key"] = $"test.card.{id}.name",
            ["description_i18n_key"] = $"test.card.{id}.description",
            ["upgraded_description_i18n_key"] = $"test.card.{id}.upgraded_description",
            ["card_type"] = (int)cfg.battle.CardType.Attack,
            ["rarity"] = (int)cfg.battle.CardRarity.Common,
            ["cost"] = 1,
            ["cost_kind"] = (int)cfg.battle.CardCostKind.Fixed,
            ["upgraded_cost"] = 1,
            ["target_rule"] = (int)cfg.battle.TargetRule.Enemy,
            ["play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
            ["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
            ["has_upgrade"] = trackKind != cfg.battle.CardUpgradeTrackKind.None,
            ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
            ["effect_bindings"] = effectId.HasValue
                ? new JArray(new JObject
                {
                    ["argument_key"] = "damage",
                    ["effect_id"] = effectId.Value,
                })
                : new JArray(),
            ["illustration_key"] = string.Empty,
            ["program_id"] = (int)cfg.battle.MachineGunnerProgramId.None,
            ["is_innate"] = false,
            ["upgrade_track_kind"] = (int)trackKind,
            ["infinite_upgrade_rule_kind"] = (int)infiniteRuleKind,
            ["infinite_upgrade_value_per_level"] = infiniteValuePerLevel,
        };
    }

    /// <summary>创建一条基础伤害效果配置。</summary>
    private static JObject CreateEffect(int id, int value)
    {
        return new JObject
        {
            ["id"] = id,
            ["effect_type"] = (int)cfg.battle.EffectType.DealDamage,
            ["attribute"] = (int)cfg.battle.Attribute.None,
            ["value"] = value,
        };
    }

    /// <summary>创建一条达到指定有限等级的绝对伤害配置。</summary>
    private static JObject CreateUpgrade(int cardId, int nextLevel, int ruleValue)
    {
        return new JObject
        {
            ["card_id"] = cardId,
            ["next_upgrade_level"] = nextLevel,
            ["description_i18n_key"] = $"test.card.{cardId}.level.{nextLevel}",
            ["cost"] = 1,
            ["play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
            ["rule_kind"] = (int)cfg.battle.CardUpgradeRuleKind.DamageValue,
            ["rule_value"] = ruleValue,
        };
    }
}
