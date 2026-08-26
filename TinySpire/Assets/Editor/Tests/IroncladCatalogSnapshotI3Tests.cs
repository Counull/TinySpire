using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;

/// <summary>
/// 校验 STS2 v0.107.1 战士单人卡牌目录的冻结身份集合。
/// </summary>
public sealed class IroncladCatalogSnapshotI3Tests
{
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string CardEffectTableJsonPath = "Assets/GameData/battle_tbcardeffect.json";
    private const string SnapshotKey = "sts2-v0.107.1-23811903-59260271";
    private const int HealEffectTypeValue = (int)cfg.battle.EffectType.Heal;
    private const int RetainBlockEffectTypeValue = (int)cfg.battle.EffectType.RetainBlock;
    private const int PlayTopDrawCardAndExhaustEffectTypeValue =
        (int)cfg.battle.EffectType.PlayTopDrawCardAndExhaust;
    private const int TriggerRandomEnemyDamageOnBlockGainedEffectTypeValue = 10;

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

    /// <summary>确认首批四张 Ironclad 运行卡具有冻结的基础数值、目标、归宿和有序 Effect 绑定。</summary>
    [Test]
    public void GeneratedFirstIroncladRuntimeSlice_HasExactBaseEffectsAndBindings()
    {
        JObject cards = LoadCards();
        AssertImplementedCard(
            FindCardByExternalKey(cards, "BLUDGEON").Value,
            id: 3123,
            cfg.battle.CardType.Attack,
            cost: 3,
            cfg.battle.TargetRule.Enemy,
            4007);
        AssertImplementedCard(
            FindCardByExternalKey(cards, "TWIN_STRIKE").Value,
            id: 3120,
            cfg.battle.CardType.Attack,
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4008,
            4008);
        AssertImplementedCard(
            FindCardByExternalKey(cards, "POMMEL_STRIKE").Value,
            id: 3113,
            cfg.battle.CardType.Attack,
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4009,
            4010);
        AssertImplementedCard(
            FindCardByExternalKey(cards, "SHRUG_IT_OFF").Value,
            id: 3115,
            cfg.battle.CardType.Skill,
            cost: 1,
            cfg.battle.TargetRule.Self,
            4011,
            4010);

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        AssertEffect(effects, 4007, cfg.battle.EffectType.DealDamage, value: 32);
        AssertEffect(effects, 4008, cfg.battle.EffectType.DealDamage, value: 5);
        AssertEffect(effects, 4009, cfg.battle.EffectType.DealDamage, value: 9);
        AssertEffect(effects, 4010, cfg.battle.EffectType.DrawCards, value: 1);
        AssertEffect(effects, 4011, cfg.battle.EffectType.GainBlock, value: 8);
    }

    /// <summary>确认 Burning Pact 以精确目录元数据、有序选牌消耗与抽牌 Effect 进入可玩目录。</summary>
    [Test]
    public void GeneratedBurningPact_IsImplementedWithExactMetadataAndOrderedEffects()
    {
        JObject cards = LoadCards();
        JToken burningPact = FindCardByExternalKey(cards, "BURNING_PACT").Value;

        Assert.That(burningPact.Value<int>("id"), Is.EqualTo(3125));
        Assert.That(burningPact.Value<string>("external_key"), Is.EqualTo("BURNING_PACT"));
        Assert.That(burningPact.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            burningPact.Value<string>("name_i18n_key"),
            Is.EqualTo("battle.card.sts2.burning_pact.name"));
        Assert.That(
            burningPact.Value<string>("description_i18n_key"),
            Is.EqualTo("battle.card.sts2.burning_pact.description"));
        Assert.That(
            burningPact.Value<string>("upgraded_description_i18n_key"),
            Is.EqualTo("battle.card.sts2.burning_pact.upgrade_description"));
        Assert.That(
            burningPact.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            burningPact.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(
            burningPact.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Uncommon));
        Assert.That(burningPact.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(
            burningPact.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(burningPact.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(
            burningPact.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            burningPact.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            burningPact.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(burningPact.Value<bool>("has_upgrade"), Is.True);
        Assert.That(burningPact.Value<string>("illustration_key"), Is.EqualTo("art_placeholder"));
        Assert.That(
            burningPact.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(burningPact.Value<bool>("is_innate"), Is.False);

        JArray bindings = (JArray)burningPact["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(2));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("exhaustCards"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4012));
        Assert.That(bindings[1].Value<string>("argument_key"), Is.EqualTo("cards"));
        Assert.That(bindings[1].Value<int>("effect_id"), Is.EqualTo(4013));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        AssertEffect(effects, 4012, cfg.battle.EffectType.ExhaustSelectedHandCard, value: 1);
        AssertEffect(effects, 4013, cfg.battle.EffectType.DrawCards, value: 2);
    }

    /// <summary>确认 Not Yet 以精确目录元数据、治疗绑定和消耗归宿进入可玩目录。</summary>
    [Test]
    public void GeneratedNotYet_IsImplementedWithExactMetadataAndHealEffect()
    {
        JObject cards = LoadCards();
        JToken notYet = FindCardByExternalKey(cards, "NOT_YET").Value;

        Assert.That(notYet.Value<int>("id"), Is.EqualTo(3171));
        Assert.That(notYet.Value<string>("external_key"), Is.EqualTo("NOT_YET"));
        Assert.That(notYet.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            notYet.Value<string>("name_i18n_key"),
            Is.EqualTo("battle.card.sts2.not_yet.name"));
        Assert.That(
            notYet.Value<string>("description_i18n_key"),
            Is.EqualTo("battle.card.sts2.not_yet.description"));
        Assert.That(
            notYet.Value<string>("upgraded_description_i18n_key"),
            Is.EqualTo("battle.card.sts2.not_yet.upgrade_description"));
        Assert.That(
            notYet.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(notYet.Value<int>("card_type"), Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(notYet.Value<int>("rarity"), Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(notYet.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(
            notYet.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(notYet.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(notYet.Value<int>("target_rule"), Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            notYet.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(
            notYet.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.ExhaustPile));
        Assert.That(notYet.Value<bool>("has_upgrade"), Is.True);
        Assert.That(notYet.Value<string>("illustration_key"), Is.EqualTo("art_placeholder"));
        Assert.That(
            notYet.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(notYet.Value<bool>("is_innate"), Is.False);

        JArray bindings = (JArray)notYet["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(1));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("heal"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4014));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        JToken heal = effects["4014"];
        Assert.That(heal, Is.Not.Null, "缺少 Not Yet 治疗 Effect 4014。");
        Assert.That(heal.Value<int>("effect_type"), Is.EqualTo(HealEffectTypeValue));
        Assert.That(
            heal.Value<int>("attribute"),
            Is.EqualTo((int)cfg.battle.Attribute.None));
        Assert.That(heal.Value<int>("value"), Is.EqualTo(10));
    }

    /// <summary>确认 Sword Boomerang 以随机敌人规则、三段有序伤害绑定和固定基础伤害进入可玩目录。</summary>
    [Test]
    public void GeneratedSwordBoomerang_IsImplementedWithExactMetadataAndRepeatedDamageBindings()
    {
        JObject cards = LoadCards();
        JToken swordBoomerang = FindCardByExternalKey(cards, "SWORD_BOOMERANG").Value;

        Assert.That(swordBoomerang.Value<int>("id"), Is.EqualTo(3116));
        Assert.That(swordBoomerang.Value<string>("external_key"), Is.EqualTo("SWORD_BOOMERANG"));
        Assert.That(swordBoomerang.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            swordBoomerang.Value<string>("name_i18n_key"),
            Is.EqualTo("battle.card.sts2.sword_boomerang.name"));
        Assert.That(
            swordBoomerang.Value<string>("description_i18n_key"),
            Is.EqualTo("battle.card.sts2.sword_boomerang.description"));
        Assert.That(
            swordBoomerang.Value<string>("upgraded_description_i18n_key"),
            Is.EqualTo("battle.card.sts2.sword_boomerang.upgrade_description"));
        Assert.That(
            swordBoomerang.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            swordBoomerang.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Attack));
        Assert.That(
            swordBoomerang.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Common));
        Assert.That(swordBoomerang.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(
            swordBoomerang.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(swordBoomerang.Value<int>("upgraded_cost"), Is.EqualTo(1));
        Assert.That(
            swordBoomerang.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.RandomEnemy));
        Assert.That(
            swordBoomerang.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            swordBoomerang.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(swordBoomerang.Value<bool>("has_upgrade"), Is.True);
        Assert.That(swordBoomerang.Value<string>("illustration_key"), Is.EqualTo("art_placeholder"));
        Assert.That(
            swordBoomerang.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(swordBoomerang.Value<bool>("is_innate"), Is.False);

        JArray bindings = (JArray)swordBoomerang["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(3));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("damage"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4015));
        Assert.That(bindings[1].Value<string>("argument_key"), Is.EqualTo("damageRepeat1"));
        Assert.That(bindings[1].Value<int>("effect_id"), Is.EqualTo(4015));
        Assert.That(bindings[2].Value<string>("argument_key"), Is.EqualTo("damageRepeat2"));
        Assert.That(bindings[2].Value<int>("effect_id"), Is.EqualTo(4015));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        AssertEffect(effects, 4015, cfg.battle.EffectType.DealDamage, value: 3);
    }

    /// <summary>确认 Body Slam 以当前格挡伤害 Effect、敌方目标和降费升级元数据进入可玩目录。</summary>
    [Test]
    public void GeneratedBodySlam_IsImplementedWithExactMetadataAndSourceBlockDamageEffect()
    {
        JObject cards = LoadCards();
        JToken bodySlam = FindCardByExternalKey(cards, "BODY_SLAM").Value;

        Assert.That(bodySlam.Value<int>("id"), Is.EqualTo(3105));
        Assert.That(bodySlam.Value<string>("external_key"), Is.EqualTo("BODY_SLAM"));
        Assert.That(bodySlam.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            bodySlam.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            bodySlam.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Attack));
        Assert.That(
            bodySlam.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Common));
        Assert.That(bodySlam.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(
            bodySlam.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(bodySlam.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(
            bodySlam.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(
            bodySlam.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            bodySlam.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(bodySlam.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            bodySlam.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(bodySlam.Value<bool>("is_innate"), Is.False);

        JArray bindings = (JArray)bodySlam["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(1));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("damage"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4016));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        JToken sourceBlockDamage = effects["4016"];
        Assert.That(sourceBlockDamage, Is.Not.Null, "缺少 Body Slam 当前格挡伤害 Effect 4016。");
        Assert.That(
            sourceBlockDamage.Value<int>("effect_type"),
            Is.EqualTo((int)cfg.battle.EffectType.DealDamageFromSourceBlock));
        Assert.That(
            sourceBlockDamage.Value<int>("attribute"),
            Is.EqualTo((int)cfg.battle.Attribute.None));
        Assert.That(sourceBlockDamage.Value<int>("value"), Is.Zero);
    }

    /// <summary>确认 Barricade 以能力牌归宿、降费升级和永久保留格挡 Effect 进入可玩目录。</summary>
    [Test]
    public void GeneratedBarricade_IsImplementedWithExactMetadataAndRetainBlockEffect()
    {
        JObject cards = LoadCards();
        JToken barricade = FindCardByExternalKey(cards, "BARRICADE").Value;

        Assert.That(barricade.Value<int>("id"), Is.EqualTo(3157));
        Assert.That(barricade.Value<string>("external_key"), Is.EqualTo("BARRICADE"));
        Assert.That(barricade.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            barricade.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            barricade.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Power));
        Assert.That(
            barricade.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(barricade.Value<int>("cost"), Is.EqualTo(3));
        Assert.That(
            barricade.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(barricade.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(
            barricade.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            barricade.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(
            barricade.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(barricade.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            barricade.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(barricade.Value<bool>("is_innate"), Is.False);

        JArray bindings = (JArray)barricade["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(1));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("retention"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4017));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        JToken retainBlock = effects["4017"];
        Assert.That(retainBlock, Is.Not.Null, "缺少 Barricade 永久保留格挡 Effect 4017。");
        Assert.That(retainBlock.Value<int>("effect_type"), Is.EqualTo(RetainBlockEffectTypeValue));
        Assert.That(
            retainBlock.Value<int>("attribute"),
            Is.EqualTo((int)cfg.battle.Attribute.None));
        Assert.That(retainBlock.Value<int>("value"), Is.Zero);
    }

    /// <summary>确认 Havoc 以抽牌堆顶免费出牌并强制消耗的通用 Effect 进入可玩目录。</summary>
    [Test]
    public void GeneratedHavoc_IsImplementedWithExactMetadataAndTriggeredPlayEffect()
    {
        JObject cards = LoadCards();
        JToken havoc = FindCardByExternalKey(cards, "HAVOC").Value;

        Assert.That(havoc.Value<int>("id"), Is.EqualTo(3108));
        Assert.That(havoc.Value<string>("external_key"), Is.EqualTo("HAVOC"));
        Assert.That(havoc.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            havoc.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(havoc.Value<int>("card_type"), Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(havoc.Value<int>("rarity"), Is.EqualTo((int)cfg.battle.CardRarity.Common));
        Assert.That(havoc.Value<int>("cost"), Is.EqualTo(1));
        Assert.That(havoc.Value<int>("cost_kind"), Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(havoc.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(havoc.Value<int>("target_rule"), Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            havoc.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            havoc.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(havoc.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            havoc.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(havoc.Value<bool>("is_innate"), Is.False);

        JArray bindings = (JArray)havoc["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(1));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("triggeredPlay"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4018));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        JToken triggeredPlay = effects["4018"];
        Assert.That(triggeredPlay, Is.Not.Null, "缺少 Havoc 抽牌堆顶触发出牌 Effect 4018。");
        Assert.That(
            triggeredPlay.Value<int>("effect_type"),
            Is.EqualTo(PlayTopDrawCardAndExhaustEffectTypeValue));
        Assert.That(
            triggeredPlay.Value<int>("attribute"),
            Is.EqualTo((int)cfg.battle.Attribute.None));
        Assert.That(triggeredPlay.Value<int>("value"), Is.Zero);
    }

    /// <summary>确认 Juggernaut 以能力牌归宿和获得格挡后随机伤害触发进入可玩目录。</summary>
    [Test]
    public void GeneratedJuggernaut_IsImplementedWithExactMetadataAndBlockTriggerDamageEffect()
    {
        JObject cards = LoadCards();
        JToken juggernaut = FindCardByExternalKey(cards, "JUGGERNAUT").Value;

        Assert.That(juggernaut.Value<int>("id"), Is.EqualTo(3169));
        Assert.That(juggernaut.Value<string>("external_key"), Is.EqualTo("JUGGERNAUT"));
        Assert.That(juggernaut.Value<string>("catalog_snapshot_key"), Is.EqualTo(SnapshotKey));
        Assert.That(
            juggernaut.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(juggernaut.Value<int>("card_type"), Is.EqualTo((int)cfg.battle.CardType.Power));
        Assert.That(juggernaut.Value<int>("rarity"), Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(juggernaut.Value<int>("cost"), Is.EqualTo(2));
        Assert.That(
            juggernaut.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(juggernaut.Value<int>("upgraded_cost"), Is.EqualTo(2));
        Assert.That(juggernaut.Value<int>("target_rule"), Is.EqualTo((int)cfg.battle.TargetRule.Self));
        Assert.That(
            juggernaut.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(
            juggernaut.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.Power));
        Assert.That(juggernaut.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            juggernaut.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(juggernaut.Value<bool>("is_innate"), Is.False);

        JArray bindings = (JArray)juggernaut["effect_bindings"];
        Assert.That(bindings, Has.Count.EqualTo(1));
        Assert.That(bindings[0].Value<string>("argument_key"), Is.EqualTo("triggerDamage"));
        Assert.That(bindings[0].Value<int>("effect_id"), Is.EqualTo(4019));

        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        JToken triggerDamage = effects["4019"];
        Assert.That(triggerDamage, Is.Not.Null, "缺少 Juggernaut 获得格挡后随机伤害 Effect 4019。");
        Assert.That(
            triggerDamage.Value<int>("effect_type"),
            Is.EqualTo(TriggerRandomEnemyDamageOnBlockGainedEffectTypeValue));
        Assert.That(
            triggerDamage.Value<int>("attribute"),
            Is.EqualTo((int)cfg.battle.Attribute.None));
        Assert.That(triggerDamage.Value<int>("value"), Is.EqualTo(6));
    }

    /// <summary>确认 Burning Pact 会从生成配置和真实英文 String Table 投影出固定的选牌与抽牌数值。</summary>
    [UnityTest]
    public IEnumerator GeneratedBurningPact_CardTextFormatterUsesLiteralSelectionAndDrawValues()
    {
        var configs = new ConfigService();
        var localization = new LocalizationService();
        var combatants = new BattleCombatantsData();
        Locale previousLocale = null;
        bool localeCaptured = false;
        try
        {
            yield return configs.InitializeAsync(new GeneratedGameDataTextLoader()).ToCoroutine();
            yield return localization.InitializeAsync().ToCoroutine();
            previousLocale = LocalizationSettings.SelectedLocale;
            localeCaptured = true;
            Assert.That(localization.SetLocale("en"), Is.True);

            PlayerCombatantData source = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 80,
                strength: 7);
            var formatter = new CardTextFormatter(configs, localization);
            var card = new CardInstanceData(new CardInstanceId(1), templateId: 3125);

            CardPresentationText text = formatter.Format(card, source);

            Assert.That(text.Name, Is.EqualTo("Burning Pact"));
            Assert.That(
                text.Description,
                Is.EqualTo("Exhaust 1 card(s). Draw 2 card(s)."));
        }
        finally
        {
            if (localeCaptured)
                LocalizationSettings.SelectedLocale = previousLocale;
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>确认四张 G4 生产卡按各自实例等级投影描述键与伤害参数，且同模板基础实例不被升级实例污染。</summary>
    [UnityTest]
    public IEnumerator GeneratedG4UpgradeCards_CardTextFormatterUsesInstanceLevelProjection()
    {
        var configs = new ConfigService();
        var localization = new LocalizationService();
        var combatants = new BattleCombatantsData();
        Locale previousLocale = null;
        bool localeCaptured = false;
        try
        {
            yield return configs.InitializeAsync(new GeneratedGameDataTextLoader()).ToCoroutine();
            yield return localization.InitializeAsync().ToCoroutine();
            previousLocale = LocalizationSettings.SelectedLocale;
            localeCaptured = true;
            Assert.That(localization.SetLocale("en"), Is.True);

            PlayerCombatantData source = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 80,
                strength: 0);
            var formatter = new CardTextFormatter(configs, localization);
            var baseStrike = new CardInstanceData(
                new CardInstanceId(1),
                templateId: 3002,
                originRunCardInstanceId: null,
                upgradeLevel: 0);
            var upgradedStrike = new CardInstanceData(
                new CardInstanceId(2),
                templateId: 3002,
                originRunCardInstanceId: null,
                upgradeLevel: 1);
            var upgradedBludgeon = new CardInstanceData(
                new CardInstanceId(3),
                templateId: 3123,
                originRunCardInstanceId: null,
                upgradeLevel: 2);
            var upgradedShoot = new CardInstanceData(
                new CardInstanceId(4),
                templateId: 3201,
                originRunCardInstanceId: null,
                upgradeLevel: 2);
            var upgradedOutputAdjust = new CardInstanceData(
                new CardInstanceId(5),
                templateId: 3207,
                originRunCardInstanceId: null,
                upgradeLevel: 1);

            CardPresentationText baseStrikeText = formatter.Format(baseStrike, source);
            CardPresentationText upgradedStrikeText = formatter.Format(upgradedStrike, source);
            CardPresentationText upgradedBludgeonText = formatter.Format(upgradedBludgeon, source);
            CardPresentationText upgradedShootText = formatter.Format(upgradedShoot, source);
            CardPresentationText upgradedOutputAdjustText = formatter.Format(
                upgradedOutputAdjust,
                source);

            Assert.That(
                baseStrikeText.Description,
                Is.EqualTo(localization.GetString(
                    "battle.card.strike.description",
                    DamageArguments(6))));
            Assert.That(
                upgradedStrikeText.Description,
                Is.EqualTo(localization.GetString(
                    "battle.card.strike.upgrade_description",
                    DamageArguments(9))));
            Assert.That(
                upgradedBludgeonText.Description,
                Is.EqualTo(localization.GetString(
                    "battle.card.sts2.bludgeon.upgrade_description",
                    DamageArguments(52))));
            Assert.That(
                upgradedShootText.Description,
                Is.EqualTo(localization.GetString(
                    "battle.card.marine.shoot.upgrade_description",
                    DamageArguments(12))));
            Assert.That(
                upgradedOutputAdjustText.Description,
                Is.EqualTo(localization.GetString(
                    "battle.card.marine.output_adjust.upgrade_description")));
            StringAssert.Contains("6", baseStrikeText.Description);
            StringAssert.Contains("9", upgradedStrikeText.Description);
            StringAssert.Contains("52", upgradedBludgeonText.Description);
            StringAssert.Contains("12", upgradedShootText.Description);
        }
        finally
        {
            if (localeCaptured)
                LocalizationSettings.SelectedLocale = previousLocale;
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>创建只含投影伤害参数的稳定本地化参数表。</summary>
    private static IReadOnlyDictionary<string, object> DamageArguments(int damage)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["damage"] = damage,
        };
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

    /// <summary>确认 15/70 总数不变时，Havoc 的可玩身份互换仍会被构建门禁拒绝。</summary>
    [Test]
    public void SnapshotValidator_ImplementedIdentitySwappedWithoutCountDrift_Throws()
    {
        JObject cards = LoadCards();
        FindCardByExternalKey(cards, "HAVOC").Value["implementation_status"] =
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
            Is.EqualTo(15));
        Assert.That(
            snapshotCards.Count(card =>
                card.Value<int>("implementation_status")
                == (int)cfg.battle.CardImplementationStatus.CatalogOnly),
            Is.EqualTo(70));

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateIroncladV01071Snapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo(
                "Ironclad Implemented external keys drifted: " +
                "missing [HAVOC]; unexpected [ANGER]."));
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

    /// <summary>断言一张首批运行卡的冻结身份、基础玩法事实和有序 Effect 标识。</summary>
    private static void AssertImplementedCard(
        JToken card,
        int id,
        cfg.battle.CardType cardType,
        int cost,
        cfg.battle.TargetRule targetRule,
        params int[] effectIds)
    {
        Assert.That(card.Value<int>("id"), Is.EqualTo(id));
        Assert.That(
            card.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(card.Value<int>("card_type"), Is.EqualTo((int)cardType));
        Assert.That(card.Value<int>("cost"), Is.EqualTo(cost));
        Assert.That(
            card.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(card.Value<int>("target_rule"), Is.EqualTo((int)targetRule));
        Assert.That(
            card.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            card.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            card.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.None));
        Assert.That(
            ((JArray)card["effect_bindings"])
                .Select(binding => binding.Value<int>("effect_id"))
                .ToArray(),
            Is.EqualTo(effectIds));
    }

    /// <summary>断言一条首批运行 Effect 使用冻结的类型、空属性和基础数值。</summary>
    private static void AssertEffect(
        JObject effects,
        int id,
        cfg.battle.EffectType effectType,
        int value)
    {
        JToken effect = effects[id.ToString()];
        Assert.That(effect, Is.Not.Null, $"缺少 Ironclad Effect {id}。");
        Assert.That(effect.Value<int>("effect_type"), Is.EqualTo((int)effectType));
        Assert.That(
            effect.Value<int>("attribute"),
            Is.EqualTo((int)cfg.battle.Attribute.None));
        Assert.That(effect.Value<int>("value"), Is.EqualTo(value));
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

    /// <summary>从已生成的 Assets/GameData 路径读取生产配置文本，供格式化集成测试初始化 ConfigService。</summary>
    private sealed class GeneratedGameDataTextLoader : IConfigTextLoader
    {
        /// <summary>异步读取 ConfigService 请求的生成配置文件。</summary>
        public async UniTask<string> LoadTextAsync(string address)
        {
            await UniTask.Yield();
            return File.ReadAllText(address);
        }
    }
}
