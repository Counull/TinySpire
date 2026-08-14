using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// 校验 Marine Game v1 的机枪兵卡牌目录、程序绑定和初始牌运行时切片保持冻结。
/// </summary>
public sealed class MachineGunnerCatalogSnapshotMG2ATests
{
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string SnapshotKey = "marine-game-v1-20260807-cards";

    /// <summary>确认生成表中的 64 张机枪兵卡通过完整快照，且全部运行时程序集合精确受门禁约束。</summary>
    [Test]
    public void GeneratedCatalog_MarineGameV1SnapshotPassesStarterRuntimeValidation()
    {
        JObject cards = LoadCards();

        Assert.DoesNotThrow(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV1CardSnapshot(cards));

        JToken[] snapshotCards = cards.Properties()
            .Select(property => property.Value)
            .Where(card => string.Equals(
                card.Value<string>("catalog_snapshot_key"),
                SnapshotKey,
                StringComparison.Ordinal))
            .ToArray();
        Assert.That(snapshotCards, Has.Length.EqualTo(64));
        Assert.That(snapshotCards.Select(card => card.Value<int>("id")).OrderBy(id => id),
            Is.EqualTo(Enumerable.Range(3201, 64)));
        int[] expectedImplementedIds =
        {
            3201, 3202, 3203, 3204, 3205,
            3206, 3207, 3208, 3209, 3210, 3211, 3212, 3213,
            3214, 3215, 3216, 3217, 3218, 3219, 3220, 3221, 3222, 3223, 3224, 3225, 3226, 3227,
            3228, 3229, 3230, 3231, 3232, 3233, 3234, 3235, 3236, 3237, 3238, 3239, 3240, 3241,
            3242, 3243, 3244, 3245, 3246, 3247, 3248, 3249, 3250, 3251, 3252, 3253, 3254, 3255, 3256, 3257, 3258, 3259, 3260, 3261, 3262, 3263, 3264
        };
        Assert.That(
            snapshotCards
                .Where(card => card.Value<int>("implementation_status")
                    == (int)cfg.battle.CardImplementationStatus.Implemented)
                .Select(card => card.Value<int>("id"))
                .OrderBy(id => id),
            Is.EqualTo(expectedImplementedIds));
        Assert.That(
            snapshotCards
                .Where(card => card.Value<int>("implementation_status")
                    == (int)cfg.battle.CardImplementationStatus.CatalogOnly)
                .Select(card => card.Value<int>("id"))
                .OrderBy(id => id),
            Is.EqualTo(Enumerable.Range(3201, 64).Except(expectedImplementedIds)));
        Assert.That(snapshotCards.Select(card => card.Value<int>("program_id")).OrderBy(id => id),
            Is.EqualTo(Enumerable.Range(1, 64)));
        Assert.That(snapshotCards.All(card =>
            card["is_innate"]?.Type == JTokenType.Boolean &&
            !card.Value<bool>("is_innate")), Is.True);
        Assert.That(snapshotCards.All(card =>
            string.Equals(card.Value<string>("illustration_key"), "art_placeholder", StringComparison.Ordinal)),
            Is.True);
        Assert.That(snapshotCards.All(card => ((JArray)card["effect_bindings"]).Count == 0), Is.True);
    }

    /// <summary>确认 starter 的基础元数据来自新版设计且不被旧卡牌数值覆盖。</summary>
    [Test]
    public void GeneratedCatalog_StarterCardsKeepUpdatedMetadata()
    {
        JObject cards = LoadCards();
        JObject shoot = FindCard(cards, "MARINE_SHOOT");
        JObject block = FindCard(cards, "MARINE_BLOCK");
        JObject reload = FindCard(cards, "MARINE_RELOAD");

        Assert.That(shoot.Value<int>("id"), Is.EqualTo(3201));
        Assert.That(shoot.Value<int>("cost"), Is.Zero);
        Assert.That(shoot.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(shoot.Value<int>("target_rule"), Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(block.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(block.Value<bool>("has_upgrade"), Is.True);
        Assert.That(reload.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(reload.Value<int>("upgraded_cost"), Is.Zero);
    }

    /// <summary>确认过载供能保留零费、自目标、弃牌归宿和唯一程序绑定，并只开放基础态运行时。</summary>
    [Test]
    public void GeneratedCatalog_OverloadKeepsAuthoredMetadata()
    {
        JObject overload = FindCard(LoadCards(), "MARINE_OVERLOAD");

        Assert.That(overload.Value<int>("id"), Is.EqualTo(3213));
        Assert.That(overload.Value<int>("card_type"), Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(overload.Value<int>("rarity"), Is.EqualTo((int)cfg.battle.CardRarity.Common));
        Assert.That(overload.Value<int>("cost"), Is.Zero);
        Assert.That(overload.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(overload.Value<int>("target_rule"), Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(overload.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(overload.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(overload.Value<bool>("has_upgrade"), Is.True);
        Assert.That(overload.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(overload.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.Overload));
    }

    /// <summary>确认撤退保留作者表的费用、自目标、弃牌归宿与职业程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_RetreatKeepsAuthoredMetadata()
    {
        JObject retreat = FindCard(LoadCards(), "MARINE_RETREAT");

        Assert.That(retreat.Value<int>("id"), Is.EqualTo(3216));
        Assert.That(retreat.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(retreat.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(retreat.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(retreat.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(retreat.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(retreat.Value<bool>("has_upgrade"), Is.True);
        Assert.That(retreat.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(retreat.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.Retreat));
    }

    /// <summary>确认战地手术保留一费、自目标、消耗归宿和唯一程序绑定，并以空 Effect 绑定进入可玩目录。</summary>
    [Test]
    public void GeneratedCatalog_FieldSurgeryKeepsExactAuthoredMetadata()
    {
        JObject fieldSurgery = FindCard(LoadCards(), "MARINE_FIELD_SURGERY");

        Assert.That(fieldSurgery.Value<int>("id"), Is.EqualTo(3231));
        Assert.That(fieldSurgery.Value<string>("external_key"), Is.EqualTo("MARINE_FIELD_SURGERY"));
        Assert.That(fieldSurgery.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(fieldSurgery.Value<int>("card_type"), Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(fieldSurgery.Value<int>("rarity"), Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(fieldSurgery.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(
            fieldSurgery.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(fieldSurgery.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(
            fieldSurgery.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            fieldSurgery.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(
            fieldSurgery.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(fieldSurgery.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            fieldSurgery.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            fieldSurgery.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.FieldSurgery));
        Assert.That(fieldSurgery.Value<string>("illustration_key"), Is.EqualTo("art_placeholder"));
        Assert.That((JArray)fieldSurgery["effect_bindings"], Is.Empty);
        Assert.That(fieldSurgery.Value<bool>("is_innate"), Is.False);
    }

    /// <summary>确认驻守以二费技能、弃牌归宿、Program46 和空 Effect 绑定进入可玩目录。</summary>
    [Test]
    public void GeneratedCatalog_GarrisonKeepsExactAuthoredMetadataAndImplementedProgram()
    {
        JObject garrison = FindCard(LoadCards(), "MARINE_GARRISON");

        Assert.That(garrison.Value<int>("id"), Is.EqualTo(3246));
        Assert.That(garrison.Value<string>("external_key"), Is.EqualTo("MARINE_GARRISON"));
        Assert.That(garrison.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            garrison.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(
            garrison.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Uncommon));
        Assert.That(garrison.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(
            garrison.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(garrison.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(
            garrison.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            garrison.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            garrison.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(garrison.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            garrison.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            garrison.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.Garrison));
        Assert.That(garrison.Value<bool>("is_innate"), Is.False);
        Assert.That(garrison["effect_bindings"], Is.TypeOf<JArray>());
        Assert.That((JArray)garrison["effect_bindings"], Is.Empty);
    }

    /// <summary>确认趁势追击以一费攻击、单敌目标、Program43 与空 Effect 绑定进入可玩目录。</summary>
    [Test]
    public void GeneratedCatalog_OpportunisticStrikeKeepsExactAuthoredMetadataAndImplementedProgram()
    {
        JObject opportunisticStrike = FindCard(LoadCards(), "MARINE_OPPORTUNISTIC_STRIKE");

        Assert.That(opportunisticStrike.Value<int>("id"), Is.EqualTo(3243));
        Assert.That(
            opportunisticStrike.Value<string>("catalog_snapshot_key"),
            Is.EqualTo(SnapshotKey));
        Assert.That(
            opportunisticStrike.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Attack));
        Assert.That(
            opportunisticStrike.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Uncommon));
        Assert.That(opportunisticStrike.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(
            opportunisticStrike.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(opportunisticStrike.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(
            opportunisticStrike.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(
            opportunisticStrike.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            opportunisticStrike.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(opportunisticStrike.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            opportunisticStrike.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            opportunisticStrike.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.OpportunisticStrike));
        Assert.That(opportunisticStrike.Value<bool>("is_innate"), Is.False);
        Assert.That(opportunisticStrike["effect_bindings"], Is.TypeOf<JArray>());
        Assert.That((JArray)opportunisticStrike["effect_bindings"], Is.Empty);
    }

    /// <summary>确认战术突进保留作者表的固定费用、卡牌分类、自目标、弃牌归宿与职业程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_TacticalAdvanceKeepsAuthoredMetadata()
    {
        JObject tacticalAdvance = FindCard(LoadCards(), "MARINE_TACTICAL_ADVANCE");

        Assert.That(tacticalAdvance.Value<int>("id"), Is.EqualTo(3234));
        Assert.That(tacticalAdvance.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(tacticalAdvance.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Uncommon));
        Assert.That(tacticalAdvance.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(tacticalAdvance.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(tacticalAdvance.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(tacticalAdvance.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(tacticalAdvance.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(tacticalAdvance.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(tacticalAdvance.Value<bool>("has_upgrade"), Is.True);
        Assert.That(tacticalAdvance.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(tacticalAdvance.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.TacticalAdvance));
        Assert.That((JArray)tacticalAdvance["effect_bindings"], Is.Empty);
        Assert.That(tacticalAdvance.Value<bool>("is_innate"), Is.False);
    }

    /// <summary>确认快速翻滚保留作者表的费用、自目标、弃牌归宿与职业程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_QuickRollKeepsAuthoredMetadata()
    {
        JObject quickRoll = FindCard(LoadCards(), "MARINE_QUICK_ROLL");

        Assert.That(quickRoll.Value<int>("id"), Is.EqualTo(3235));
        Assert.That(quickRoll.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(quickRoll.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(quickRoll.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(quickRoll.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(quickRoll.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(quickRoll.Value<bool>("has_upgrade"), Is.True);
        Assert.That(quickRoll.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(quickRoll.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.QuickRoll));
    }

    /// <summary>确认不充分爆燃保留作者表的费用、Self、Exhaust 与升级元数据，且已绑定唯一职业程序。</summary>
    [Test]
    public void GeneratedCatalog_IncompleteCombustionKeepsAuthoredMetadata()
    {
        JObject incompleteCombustion = FindCard(LoadCards(), "MARINE_INCOMPLETE_COMBUSTION");

        Assert.That(incompleteCombustion.Value<int>("id"), Is.EqualTo(3222));
        Assert.That(incompleteCombustion.Value<int>("cost"), Is.EqualTo(3));
        Assert.That(incompleteCombustion.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(incompleteCombustion.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(incompleteCombustion.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(incompleteCombustion.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(incompleteCombustion.Value<bool>("has_upgrade"), Is.True);
        Assert.That(incompleteCombustion.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(incompleteCombustion.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.IncompleteCombustion));
    }

    /// <summary>确认击退射击保留作者表的零费、敌方目录目标、弃牌归宿与唯一职业程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_KnockbackShotKeepsAuthoredMetadata()
    {
        JObject knockbackShot = FindCard(LoadCards(), "MARINE_KNOCKBACK_SHOT");

        Assert.That(knockbackShot.Value<int>("id"), Is.EqualTo(3223));
        Assert.That(knockbackShot.Value<int>("cost"), Is.Zero);
        Assert.That(knockbackShot.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(knockbackShot.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(knockbackShot.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(knockbackShot.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(knockbackShot.Value<bool>("has_upgrade"), Is.True);
        Assert.That(knockbackShot.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(knockbackShot.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.KnockbackShot));
    }

    /// <summary>确认爆炸肘保留作者表的费用、目录目标和弃牌归宿，且已绑定唯一职业程序。</summary>
    [Test]
    public void GeneratedCatalog_ExplosiveElbowKeepsAuthoredMetadata()
    {
        JObject explosiveElbow = FindCard(LoadCards(), "MARINE_EXPLOSIVE_ELBOW");

        Assert.That(explosiveElbow.Value<int>("id"), Is.EqualTo(3252));
        Assert.That(explosiveElbow.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(explosiveElbow.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(explosiveElbow.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(explosiveElbow.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(explosiveElbow.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(explosiveElbow.Value<bool>("has_upgrade"), Is.True);
        Assert.That(explosiveElbow.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(explosiveElbow.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.ExplosiveElbow));
    }

    /// <summary>确认光学迷彩保留作者表的费用、自目标、弃牌归宿与升级元数据，并只绑定唯一职业程序。</summary>
    [Test]
    public void GeneratedCatalog_OpticalCamoKeepsAuthoredMetadata()
    {
        JObject opticalCamo = FindCard(LoadCards(), "MARINE_OPTICAL_CAMO");

        Assert.That(opticalCamo.Value<int>("id"), Is.EqualTo(3249));
        Assert.That(opticalCamo.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(opticalCamo.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(opticalCamo.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(opticalCamo.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(opticalCamo.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(opticalCamo.Value<bool>("has_upgrade"), Is.True);
        Assert.That(opticalCamo.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(opticalCamo.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.OpticalCamo));
    }

    /// <summary>确认游击战术保留作者表的固定费用、自目标、Power 归宿与程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_GuerrillaTacticsKeepsAuthoredMetadata()
    {
        JObject guerrillaTactics = FindCard(LoadCards(), "MARINE_GUERRILLA_TACTICS");

        Assert.That(guerrillaTactics.Value<int>("id"), Is.EqualTo(3251));
        Assert.That(guerrillaTactics.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(guerrillaTactics.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(guerrillaTactics.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(guerrillaTactics.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(guerrillaTactics.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(guerrillaTactics.Value<bool>("has_upgrade"), Is.True);
        Assert.That(guerrillaTactics.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(guerrillaTactics.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.GuerrillaTactics));
    }

    /// <summary>确认全息诱饵保留作者表的固定费用、自目标、基础消耗归宿和当前升级归宿元数据，并绑定唯一职业程序。</summary>
    [Test]
    public void GeneratedCatalog_HoloDecoyKeepsAuthoredMetadata()
    {
        JObject holoDecoy = FindCard(LoadCards(), "MARINE_HOLO_DECOY");

        Assert.That(holoDecoy.Value<int>("id"), Is.EqualTo(3259));
        Assert.That(holoDecoy.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(holoDecoy.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(holoDecoy.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(holoDecoy.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(holoDecoy.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(holoDecoy.Value<bool>("has_upgrade"), Is.True);
        Assert.That(holoDecoy.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(holoDecoy.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.HoloDecoy));
    }

    /// <summary>确认新版电磁增压已迁为罕见能力牌，基础和升级均进入 PowerPile 并保留唯一程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_ElectroBoostKeepsUpdatedPowerMetadata()
    {
        JObject electroBoost = FindCard(LoadCards(), "MARINE_ELECTRO_BOOST");

        Assert.That(electroBoost.Value<int>("id"), Is.EqualTo(3236));
        Assert.That(electroBoost.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Power));
        Assert.That(electroBoost.Value<int>("rarity"),
                Is.EqualTo((int)cfg.battle.CardRarity.Uncommon));
        Assert.That(electroBoost.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(electroBoost.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(electroBoost.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(electroBoost.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(electroBoost.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(electroBoost.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(electroBoost.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.ElectroBoost));
    }

    /// <summary>确认新版防御靶机已翻为可运行卡，保留作者表的消耗归宿、费用与唯一程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_DefenseTargetKeepsUpdatedExhaustMetadata()
    {
        JObject defenseTarget = FindCard(LoadCards(), "MARINE_DEFENSE_TARGET");

        Assert.That(defenseTarget.Value<int>("id"), Is.EqualTo(3262));
        Assert.That(defenseTarget.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(defenseTarget.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(defenseTarget.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(defenseTarget.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(defenseTarget.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(defenseTarget.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(defenseTarget.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(defenseTarget.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(defenseTarget.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.DefenseTarget));
    }

    /// <summary>确认 V2E 六张 V1 延迟卡只开放运行时状态，费用、目标、归宿与程序身份仍精确来自作者表。</summary>
    [Test]
    public void GeneratedCatalog_DelayedRuntimeCardsKeepAuthoredMetadata()
    {
        JObject cards = LoadCards();

        AssertCardMetadata(cards, "MARINE_GUIDED_NUKE", 3237, 5, 5,
            cfg.battle.CardType.Skill, cfg.battle.TargetRule.Self,
            cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.GuidedNuke);
        AssertCardMetadata(cards, "MARINE_BANSHEE_STRIKE", 3238, 2, 2,
            cfg.battle.CardType.Skill, cfg.battle.TargetRule.Self,
            cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.BansheeStrike);
        AssertCardMetadata(cards, "MARINE_FIRE_SUPPORT", 3239, 1, 1,
            cfg.battle.CardType.Skill, cfg.battle.TargetRule.Self,
            cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.FireSupport);
        AssertCardMetadata(cards, "MARINE_FIRE_BOMBARDMENT", 3240, 2, 2,
            cfg.battle.CardType.Skill, cfg.battle.TargetRule.Self,
            cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.FireBombardment);
        AssertCardMetadata(cards, "MARINE_FIVE_HUNDRED_POUNDER", 3241, 3, 2,
            cfg.battle.CardType.Skill, cfg.battle.TargetRule.Self,
            cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.FiveHundredPounder);
        AssertCardMetadata(cards, "MARINE_TRIPLE_STRIKE", 3264, 4, 4,
            cfg.battle.CardType.Attack, cfg.battle.TargetRule.Enemy,
            cfg.battle.CardPlayDestination.ExhaustPile, cfg.battle.MachineGunnerProgramId.TripleStrike);
    }

    /// <summary>确认机枪爆射保持零费随机敌人攻击、消耗归宿、无升级与已实现程序身份。</summary>
    [Test]
    public void GeneratedCatalog_MachinegunBurstKeepsAuthoredMetadataAndImplementedProgram()
    {
        JObject burst = FindCard(LoadCards(), "MARINE_MACHINEGUN_BURST");

        Assert.That(burst.Value<int>("id"), Is.EqualTo(3263));
        Assert.That(burst.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Attack));
        Assert.That(burst.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Common));
        Assert.That(burst.Value<int>("cost"), Is.Zero);
        Assert.That(burst.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(burst.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.RandomEnemy));
        Assert.That(burst.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(burst.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(burst.Value<bool>("has_upgrade"), Is.False);
        Assert.That(burst.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(burst.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.MachinegunBurst));
        Assert.That(burst.Value<bool>("is_innate"), Is.False);
    }

    /// <summary>确认固定机枪保持二费、自目标、罕见技能、消耗归宿、可升级和已实现程序身份。</summary>
    [Test]
    public void GeneratedCatalog_MachinegunKeepsAuthoredMetadataAndImplementedProgram()
    {
        JObject machinegun = FindCard(LoadCards(), "MARINE_MACHINEGUN");

        Assert.That(machinegun.Value<int>("id"), Is.EqualTo(3261));
        Assert.That(machinegun.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(machinegun.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(machinegun.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(machinegun.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(machinegun.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(machinegun.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(machinegun.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(machinegun.Value<bool>("has_upgrade"), Is.True);
        Assert.That(machinegun.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(machinegun.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.Machinegun));
        Assert.That(machinegun.Value<bool>("is_innate"), Is.False);
    }

    /// <summary>确认极限过载保持零费、自目标、弃牌归宿与升级元数据，并只开放基础态运行时。</summary>
    [Test]
    public void GeneratedCatalog_LimitOverloadKeepsAuthoredMetadata()
    {
        JObject limitOverload = FindCard(LoadCards(), "MARINE_LIMIT_OVERLOAD");

        Assert.That(limitOverload.Value<int>("id"), Is.EqualTo(3260));
        Assert.That(limitOverload.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(limitOverload.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(limitOverload.Value<int>("cost"), Is.Zero);
        Assert.That(limitOverload.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(limitOverload.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(limitOverload.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(limitOverload.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(limitOverload.Value<bool>("has_upgrade"), Is.True);
        Assert.That(limitOverload.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(limitOverload.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.LimitOverload));
    }

    /// <summary>确认不解释12连保留作者表中的固定费用、敌人目标元数据、弃牌归宿与已开放程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_TwelveHitsKeepsAuthoredMetadataAndImplementedProgram()
    {
        JObject twelveHits = FindCard(LoadCards(), "MARINE_TWELVE_HITS");

        Assert.That(twelveHits.Value<int>("id"), Is.EqualTo(3257));
        Assert.That(twelveHits.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Attack));
        Assert.That(twelveHits.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(twelveHits.Value<int>("cost"), Is.EqualTo(3));
        Assert.That(twelveHits.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(twelveHits.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(twelveHits.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(twelveHits.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(twelveHits.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(twelveHits.Value<bool>("has_upgrade"), Is.True);
        Assert.That(twelveHits.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(twelveHits.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.TwelveHits));
        Assert.That((JArray)twelveHits["effect_bindings"], Is.Empty);
        Assert.That(twelveHits.Value<bool>("is_innate"), Is.False);
    }

    /// <summary>确认排气散热保留作者表中的零费技能、自目标、弃牌归宿与已开放程序绑定。</summary>
    [Test]
    public void GeneratedCatalog_VentHeatKeepsAuthoredMetadataAndImplementedProgram()
    {
        JObject ventHeat = FindCard(LoadCards(), "MARINE_VENT_HEAT");

        Assert.That(ventHeat.Value<int>("id"), Is.EqualTo(3244));
        Assert.That(ventHeat.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(ventHeat.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Uncommon));
        Assert.That(ventHeat.Value<int>("cost"), Is.Zero);
        Assert.That(ventHeat.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(ventHeat.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(ventHeat.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(ventHeat.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(ventHeat.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(ventHeat.Value<bool>("has_upgrade"), Is.True);
        Assert.That(ventHeat.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(ventHeat.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.VentHeat));
        Assert.That((JArray)ventHeat["effect_bindings"], Is.Empty);
        Assert.That(ventHeat.Value<bool>("is_innate"), Is.False);
    }

    /// <summary>确认势不可挡以一费稀有能力牌、Power 归宿和 Program50 进入可玩目录。</summary>
    [Test]
    public void GeneratedCatalog_UnstoppableKeepsAuthoredMetadataAndImplementedProgram()
    {
        JObject unstoppable = FindCard(LoadCards(), "MARINE_UNSTOPPABLE");

        Assert.That(unstoppable.Value<int>("id"), Is.EqualTo(3250));
        Assert.That(unstoppable.Value<string>("external_key"), Is.EqualTo("MARINE_UNSTOPPABLE"));
        Assert.That(unstoppable.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(unstoppable.Value<int>("card_type"), Is.EqualTo((int)cfg.battle.CardType.Power));
        Assert.That(unstoppable.Value<int>("rarity"), Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(unstoppable.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(unstoppable.Value<int>("cost_kind"), Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(unstoppable.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(unstoppable.Value<int>("target_rule"), Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            unstoppable.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(
            unstoppable.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(unstoppable.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            unstoppable.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            unstoppable.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.Unstoppable));
        Assert.That((JArray)unstoppable["effect_bindings"], Is.Empty);
        Assert.That(unstoppable.Value<bool>("is_innate"), Is.False);
    }

    /// <summary>确认势不可挡被回退为目录占位时会给出稳定失败原因。</summary>
    [Test]
    public void SnapshotValidator_ImplementedUnstoppableDemotion_Throws()
    {
        JObject cards = LoadCards();
        FindCard(cards, "MARINE_UNSTOPPABLE")["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.CatalogOnly;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV1CardSnapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Marine card 'MARINE_UNSTOPPABLE' must remain Implemented in the current runtime slice."));
    }

    /// <summary>确认 V1 目录中的任意卡牌漂移为固有牌时都会被精确快照门禁拒绝。</summary>
    [Test]
    public void SnapshotValidator_V1InnateIdentityDrift_Throws()
    {
        JObject cards = LoadCards();
        FindCard(cards, "MARINE_SHOOT")["is_innate"] = true;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV1CardSnapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Marine card 'MARINE_SHOOT' must keep is_innate=false."));
    }

    /// <summary>读取生成 JSON，确保断言针对 Luban 真实输出而不是作者表副本。</summary>
    private static JObject LoadCards()
    {
        return JObject.Parse(File.ReadAllText(CardTableJsonPath));
    }

    /// <summary>按唯一外部身份定位机枪兵卡牌，防止 ID 顺序调整掩盖内容漂移。</summary>
    private static JObject FindCard(JObject cards, string externalKey)
    {
        return (JObject)cards.Properties()
            .Single(property => string.Equals(
                property.Value.Value<string>("external_key"),
                externalKey,
                StringComparison.Ordinal))
            .Value;
    }

    /// <summary>核对一张已开放延迟卡的作者元数据与精确运行时程序绑定。</summary>
    private static void AssertCardMetadata(
        JObject cards,
        string externalKey,
        int id,
        int cost,
        int upgradedCost,
        cfg.battle.CardType cardType,
        cfg.battle.TargetRule targetRule,
        cfg.battle.CardPlayDestination destination,
        cfg.battle.MachineGunnerProgramId programId)
    {
        JObject card = FindCard(cards, externalKey);
        Assert.That(card.Value<int>("id"), Is.EqualTo(id));
        Assert.That(card.Value<int>("cost"), Is.EqualTo(cost));
        Assert.That(card.Value<int>("upgraded_cost"), Is.EqualTo(upgradedCost));
        Assert.That(card.Value<int>("card_type"), Is.EqualTo((int)cardType));
        Assert.That(card.Value<int>("target_rule"), Is.EqualTo((int)targetRule));
        Assert.That(card.Value<int>("play_destination"), Is.EqualTo((int)destination));
        Assert.That(card.Value<int>("upgraded_play_destination"), Is.EqualTo((int)destination));
        Assert.That(card.Value<bool>("has_upgrade"), Is.True);
        Assert.That(card.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(card.Value<int>("program_id"), Is.EqualTo((int)programId));
    }
}
