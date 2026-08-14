using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// 校验 Marine Game V2B 新增的 18 张机枪兵身份已进入独立目录快照，并由当前运行时切片精确限制开放状态。
/// </summary>
public sealed class MachineGunnerCatalogSnapshotV2BTests
{
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string CardEffectTableJsonPath = "Assets/GameData/battle_tbcardeffect.json";
    private const string V1SnapshotKey = "marine-game-v1-20260807-cards";
    private const string V2ExtensionSnapshotKey = "marine-game-v2-20260812-cards";

    private static readonly ExpectedCatalogCard[] ExpectedCards =
    {
        new ExpectedCatalogCard(3265, "MARINE_BOMBARD", cfg.battle.CardType.Power, cfg.battle.CardRarity.Uncommon, 1, 1, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.Power, cfg.battle.MachineGunnerProgramId.Bombard, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3266, "MARINE_SKY_WRATH", cfg.battle.CardType.Power, cfg.battle.CardRarity.Rare, 1, 0, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.Power, cfg.battle.MachineGunnerProgramId.SkyWrath, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3267, "MARINE_PORTABLE_HELPER", cfg.battle.CardType.Power, cfg.battle.CardRarity.Uncommon, 1, 0, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.Power, cfg.battle.MachineGunnerProgramId.PortableHelper, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3268, "MARINE_PRIVATE_MOD", cfg.battle.CardType.Power, cfg.battle.CardRarity.Uncommon, 1, 1, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.Power, cfg.battle.MachineGunnerProgramId.PrivateMod, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3269, "MARINE_CHAIN_SMOKE", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Common, 1, 1, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.ChainSmoke, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3270, "MARINE_SECONDHAND_SMOKE", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Uncommon, 0, 0, cfg.battle.TargetRule.Enemy, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.SecondhandSmoke, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3271, "MARINE_DEFENSIVE_STANCE", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Uncommon, 1, 1, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.DefensiveStance, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3272, "MARINE_EMERGENCY_COOLING", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Uncommon, 1, 1, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.EmergencyCooling, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3273, "MARINE_THERMITE_BOMB", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Uncommon, 1, 1, cfg.battle.TargetRule.AllEnemies, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.ThermiteBomb, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3274, "MARINE_NEEDLE_STORM", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Common, 1, 1, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.NeedleStorm, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3275, "MARINE_STEALTH_ACTION", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Uncommon, 1, 1, cfg.battle.TargetRule.Self, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.StealthAction, cfg.battle.CardImplementationStatus.Implemented, isInnate: true),
        new ExpectedCatalogCard(3276, "MARINE_FOEHN_WIND", cfg.battle.CardType.Skill, cfg.battle.CardRarity.Uncommon, 2, 2, cfg.battle.TargetRule.Enemy, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.FoehnWind, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3277, "MARINE_PREEMPTIVE_STRIKE", cfg.battle.CardType.Attack, cfg.battle.CardRarity.Uncommon, 0, 0, cfg.battle.TargetRule.Enemy, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.PreemptiveStrike, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3278, "MARINE_BULLY", cfg.battle.CardType.Attack, cfg.battle.CardRarity.Uncommon, 0, 0, cfg.battle.TargetRule.Enemy, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.Bully, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3279, "MARINE_PRISMATIC_SHOT", cfg.battle.CardType.Attack, cfg.battle.CardRarity.Rare, 0, 0, cfg.battle.TargetRule.Enemy, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.PrismaticShot, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3280, "MARINE_MARK", cfg.battle.CardType.Attack, cfg.battle.CardRarity.Common, 0, 0, cfg.battle.TargetRule.Enemy, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.Mark, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3281, "MARINE_CRUSH", cfg.battle.CardType.Attack, cfg.battle.CardRarity.Uncommon, 1, 1, cfg.battle.TargetRule.Enemy, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.Crush, cfg.battle.CardImplementationStatus.Implemented),
        new ExpectedCatalogCard(3282, "MARINE_CHARGED_BURST", cfg.battle.CardType.Attack, cfg.battle.CardRarity.Uncommon, 2, 2, cfg.battle.TargetRule.AllEnemies, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.MachineGunnerProgramId.ChargedBurst, cfg.battle.CardImplementationStatus.Implemented),
    };

    /// <summary>确认 V2B 目录扩展保持 18 个连续身份、作者元数据和精确开放门禁，并且不影响 V1 运行时快照。</summary>
    [Test]
    public void GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate()
    {
        JObject cards = LoadCards();

        Assert.DoesNotThrow(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV2ExtensionSnapshot(cards));

        JObject[] snapshotCards = cards.Properties()
            .Select(property => property.Value as JObject)
            .Where(card => card != null && string.Equals(
                card.Value<string>("catalog_snapshot_key"),
                V2ExtensionSnapshotKey,
                StringComparison.Ordinal))
            .ToArray();
        Assert.That(snapshotCards, Has.Length.EqualTo(ExpectedCards.Length));
        Assert.That(snapshotCards.Select(card => card.Value<int>("id")).OrderBy(id => id),
            Is.EqualTo(Enumerable.Range(3265, ExpectedCards.Length)));
        Assert.That(snapshotCards.Select(card => card.Value<int>("program_id")).OrderBy(programId => programId),
            Is.EqualTo(Enumerable.Range(65, ExpectedCards.Length)));
        Assert.That(snapshotCards.Select(card => card.Value<string>("external_key")).OrderBy(key => key, StringComparer.Ordinal),
            Is.EqualTo(ExpectedCards.Select(card => card.ExternalKey).OrderBy(key => key, StringComparer.Ordinal)));
        Assert.That(
            snapshotCards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.Implemented),
            Is.EqualTo(18));
        Assert.That(
            snapshotCards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.CatalogOnly),
            Is.Zero);

        JObject[] v1Cards = cards.Properties()
            .Select(property => property.Value as JObject)
            .Where(card => card != null && string.Equals(
                card.Value<string>("catalog_snapshot_key"),
                V1SnapshotKey,
                StringComparison.Ordinal))
            .ToArray();
        Assert.That(
            v1Cards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.Implemented),
            Is.EqualTo(64));
        Assert.That(
            v1Cards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.CatalogOnly),
            Is.Zero);

        JObject[] allMachineGunnerCards = cards.Properties()
            .Select(property => property.Value as JObject)
            .Where(card => card != null &&
                (string.Equals(card.Value<string>("catalog_snapshot_key"), V1SnapshotKey, StringComparison.Ordinal) ||
                    string.Equals(card.Value<string>("catalog_snapshot_key"), V2ExtensionSnapshotKey, StringComparison.Ordinal)))
            .ToArray();
        Assert.That(
            allMachineGunnerCards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.Implemented),
            Is.EqualTo(82));
        Assert.That(
            allMachineGunnerCards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.CatalogOnly),
            Is.Zero);

        JObject[] allCards = cards.Properties()
            .Select(property => property.Value as JObject)
            .Where(card => card != null)
            .ToArray();
        Assert.That(allCards, Has.Length.EqualTo(168));
        Assert.That(
            allCards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.Implemented),
            Is.EqualTo(98));
        Assert.That(
            allCards.Count(card => card.Value<int>("implementation_status") ==
                (int)cfg.battle.CardImplementationStatus.CatalogOnly),
            Is.EqualTo(70));
        JObject effects = JObject.Parse(File.ReadAllText(CardEffectTableJsonPath));
        Assert.That(effects.Properties().Count(), Is.EqualTo(19));

        foreach (ExpectedCatalogCard expected in ExpectedCards)
        {
            JObject card = FindCard(cards, expected.ExternalKey);
            Assert.That(card.Value<int>("id"), Is.EqualTo(expected.Id));
            Assert.That(card.Value<int>("card_type"), Is.EqualTo((int)expected.CardType));
            Assert.That(card.Value<int>("rarity"), Is.EqualTo((int)expected.Rarity));
            Assert.That(card.Value<int>("cost"), Is.EqualTo(expected.Cost));
            Assert.That(card.Value<int>("upgraded_cost"), Is.EqualTo(expected.UpgradedCost));
            Assert.That(card.Value<int>("target_rule"), Is.EqualTo((int)expected.TargetRule));
            Assert.That(card.Value<int>("play_destination"), Is.EqualTo((int)expected.Destination));
            Assert.That(card.Value<int>("upgraded_play_destination"), Is.EqualTo((int)expected.Destination));
            Assert.That(card.Value<bool>("has_upgrade"), Is.True);
            Assert.That(card["is_innate"], Is.Not.Null);
            Assert.That(card.Value<bool>("is_innate"), Is.EqualTo(expected.IsInnate));
            Assert.That(card.Value<int>("implementation_status"),
                Is.EqualTo((int)expected.ImplementationStatus));
            Assert.That(card.Value<int>("program_id"), Is.EqualTo((int)expected.ProgramId));
            Assert.That(card.Value<string>("illustration_key"),
                Is.EqualTo(BattleCardCatalogBuildValidator.CatalogPlaceholderIllustrationKey));
            Assert.That(card["effect_bindings"], Is.TypeOf<JArray>());
            Assert.That(((JArray)card["effect_bindings"]).Count, Is.Zero);
        }
    }

    /// <summary>确认天空之怒被回退为目录占位时，目录门禁会给出稳定的阻止原因。</summary>
    /// <summary>确认 Prismatic Shot 以固定零费、显式敌人目标、Program79 与空 Effect 绑定进入可玩目录。</summary>
    [Test]
    public void GeneratedPrismaticShot_IsImplementedWithExactMetadataAndProgramBinding()
    {
        JObject prismaticShot = FindCard(LoadCards(), "MARINE_PRISMATIC_SHOT");

        Assert.That(prismaticShot.Value<int>("id"), Is.EqualTo(3279));
        Assert.That(
            prismaticShot.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Attack));
        Assert.That(
            prismaticShot.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Rare));
        Assert.That(prismaticShot.Value<int>("cost"), Is.Zero);
        Assert.That(
            prismaticShot.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(prismaticShot.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(
            prismaticShot.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(
            prismaticShot.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            prismaticShot.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            prismaticShot.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            prismaticShot.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.PrismaticShot));
        Assert.That(prismaticShot.Value<bool>("is_innate"), Is.False);
        Assert.That(prismaticShot["effect_bindings"], Is.TypeOf<JArray>());
        Assert.That((JArray)prismaticShot["effect_bindings"], Is.Empty);
    }

    /// <summary>确认 Secondhand Smoke 以固定零费、敌方目标、Program70 与空 Effect 绑定进入可玩目录。</summary>
    [Test]
    public void GeneratedSecondhandSmoke_IsImplementedWithExactMetadataAndProgramBinding()
    {
        JObject secondhandSmoke = FindCard(LoadCards(), "MARINE_SECONDHAND_SMOKE");

        Assert.That(secondhandSmoke.Value<int>("id"), Is.EqualTo(3270));
        Assert.That(
            secondhandSmoke.Value<string>("external_key"),
            Is.EqualTo("MARINE_SECONDHAND_SMOKE"));
        Assert.That(
            secondhandSmoke.Value<string>("catalog_snapshot_key"),
            Is.EqualTo(V2ExtensionSnapshotKey));
        Assert.That(
            secondhandSmoke.Value<int>("implementation_status"),
            Is.EqualTo((int)cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(
            secondhandSmoke.Value<int>("card_type"),
            Is.EqualTo((int)cfg.battle.CardType.Skill));
        Assert.That(
            secondhandSmoke.Value<int>("rarity"),
            Is.EqualTo((int)cfg.battle.CardRarity.Uncommon));
        Assert.That(secondhandSmoke.Value<int>("cost"), Is.Zero);
        Assert.That(
            secondhandSmoke.Value<int>("cost_kind"),
            Is.EqualTo((int)cfg.battle.CardCostKind.Fixed));
        Assert.That(secondhandSmoke.Value<int>("upgraded_cost"), Is.Zero);
        Assert.That(
            secondhandSmoke.Value<int>("target_rule"),
            Is.EqualTo((int)cfg.battle.TargetRule.Enemy));
        Assert.That(
            secondhandSmoke.Value<int>("play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(
            secondhandSmoke.Value<int>("upgraded_play_destination"),
            Is.EqualTo((int)cfg.battle.CardPlayDestination.DiscardPile));
        Assert.That(secondhandSmoke.Value<bool>("has_upgrade"), Is.True);
        Assert.That(
            secondhandSmoke.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.SecondhandSmoke));
        Assert.That(secondhandSmoke.Value<bool>("is_innate"), Is.False);
        Assert.That(secondhandSmoke["effect_bindings"], Is.TypeOf<JArray>());
        Assert.That((JArray)secondhandSmoke["effect_bindings"], Is.Empty);
    }

    /// <summary>确认 Secondhand Smoke 被回退为目录占位时，V2 实现身份门禁会给出稳定原因。</summary>
    [Test]
    public void SnapshotValidator_ImplementedSecondhandSmokeDemotion_Throws()
    {
        JObject cards = LoadCards();
        FindCard(cards, "MARINE_SECONDHAND_SMOKE")["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.CatalogOnly;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV2ExtensionSnapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Marine Game V2 extension card 'MARINE_SECONDHAND_SMOKE' must remain Implemented in the current runtime slice."));
    }

    [Test]
    public void SnapshotValidator_ImplementedSkyWrathDemotion_Throws()
    {
        JObject cards = LoadCards();
        FindCard(cards, "MARINE_SKY_WRATH")["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.CatalogOnly;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV2ExtensionSnapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Marine Game V2 extension card 'MARINE_SKY_WRATH' must remain Implemented in the current runtime slice."));
    }

    /// <summary>确认欺凌被回退为目录占位时，V2 实现门禁会在构建前给出稳定阻止原因。</summary>
    [Test]
    public void SnapshotValidator_ImplementedBullyDemotion_Throws()
    {
        JObject cards = LoadCards();
        FindCard(cards, "MARINE_BULLY")["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.CatalogOnly;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV2ExtensionSnapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Marine Game V2 extension card 'MARINE_BULLY' must remain Implemented in the current runtime slice."));
    }

    /// <summary>确认 Prismatic Shot 被回退为目录占位时，V2 实现门禁会在构建前精确拒绝。</summary>
    [Test]
    public void SnapshotValidator_ImplementedPrismaticShotDemotion_Throws()
    {
        JObject cards = LoadCards();
        FindCard(cards, "MARINE_PRISMATIC_SHOT")["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.CatalogOnly;

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV2ExtensionSnapshot(cards));

        Assert.That(
            failure.Message,
            Is.EqualTo("Marine Game V2 extension card 'MARINE_PRISMATIC_SHOT' must remain Implemented in the current runtime slice."));
    }

    /// <summary>确认隐秘行动的固有标记或其他 V2 卡的非固有标记漂移时，目录门禁会给出稳定失败原因。</summary>
    [Test]
    public void SnapshotValidator_V2InnateIdentityDrift_Throws()
    {
        JObject missingInnate = LoadCards();
        FindCard(missingInnate, "MARINE_STEALTH_ACTION")["is_innate"] = false;

        InvalidOperationException missingFailure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV2ExtensionSnapshot(
                missingInnate));
        Assert.That(
            missingFailure.Message,
            Is.EqualTo("Marine Game V2 extension card 'MARINE_STEALTH_ACTION' must keep is_innate=true."));

        JObject unexpectedInnate = LoadCards();
        FindCard(unexpectedInnate, "MARINE_NEEDLE_STORM")["is_innate"] = true;

        InvalidOperationException unexpectedFailure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.ValidateMarineGameV2ExtensionSnapshot(
                unexpectedInnate));
        Assert.That(
            unexpectedFailure.Message,
            Is.EqualTo("Marine Game V2 extension card 'MARINE_NEEDLE_STORM' must keep is_innate=false."));
    }

    /// <summary>读取 Luban 生成的目录 JSON，避免测试误读作者工作簿或手工副本。</summary>
    private static JObject LoadCards()
    {
        return JObject.Parse(File.ReadAllText(CardTableJsonPath));
    }

    /// <summary>按稳定外部键定位唯一机枪兵目录卡，避免依赖 JSON 属性顺序。</summary>
    private static JObject FindCard(JObject cards, string externalKey)
    {
        return (JObject)cards.Properties()
            .Single(property => string.Equals(
                property.Value.Value<string>("external_key"),
                externalKey,
                StringComparison.Ordinal))
            .Value;
    }

    /// <summary>封装一张 V2B 目录卡的冻结作者元数据，供生成目录回归逐项比对。</summary>
    private sealed class ExpectedCatalogCard
    {
        /// <summary>初始化冻结的目录元数据预期。</summary>
        internal ExpectedCatalogCard(
            int id,
            string externalKey,
            cfg.battle.CardType cardType,
            cfg.battle.CardRarity rarity,
            int cost,
            int upgradedCost,
            cfg.battle.TargetRule targetRule,
            cfg.battle.CardPlayDestination destination,
            cfg.battle.MachineGunnerProgramId programId,
            cfg.battle.CardImplementationStatus implementationStatus =
                cfg.battle.CardImplementationStatus.CatalogOnly,
            bool isInnate = false)
        {
            Id = id;
            ExternalKey = externalKey;
            CardType = cardType;
            Rarity = rarity;
            Cost = cost;
            UpgradedCost = upgradedCost;
            TargetRule = targetRule;
            Destination = destination;
            ProgramId = programId;
            ImplementationStatus = implementationStatus;
            IsInnate = isInnate;
        }

        internal int Id { get; }

        internal string ExternalKey { get; }

        internal cfg.battle.CardType CardType { get; }

        internal cfg.battle.CardRarity Rarity { get; }

        internal int Cost { get; }

        internal int UpgradedCost { get; }

        internal cfg.battle.TargetRule TargetRule { get; }

        internal cfg.battle.CardPlayDestination Destination { get; }

        internal cfg.battle.MachineGunnerProgramId ProgramId { get; }

        internal cfg.battle.CardImplementationStatus ImplementationStatus { get; }

        internal bool IsInnate { get; }
    }
}
