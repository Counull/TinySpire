using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TinySpire.Battle;

/// <summary>
/// 在本地化与 Addressables 写入前校验战斗卡牌目录的发布约束。
/// </summary>
internal static class BattleCardCatalogBuildValidator
{
    private const string DeckTableJsonPath = "Assets/GameData/battle_tbdeck.json";
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string EffectTableJsonPath = "Assets/GameData/battle_tbcardeffect.json";
    private const string IroncladSnapshotKey = "sts2-v0.107.1-23811903-59260271";
    private const string MarineSnapshotKey = "marine-game-v1-20260807-cards";
    private const string MarineV2ExtensionSnapshotKey = "marine-game-v2-20260812-cards";
    private const int FirstMarineCardId = 3201;
    private const int FirstMarineV2ExtensionCardId = 3265;
    private const int FirstMarineV2ExtensionProgramId = 65;
    private const int OpeningHandLimit = 10;
    internal const string CatalogPlaceholderIllustrationKey = "art_placeholder";

    private static readonly HashSet<string> ExpectedIroncladExternalKeys =
        new HashSet<string>(StringComparer.Ordinal)
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

    private static readonly HashSet<string> ExpectedIroncladImplementedExternalKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "BARRICADE",
            "BASH",
            "BLUDGEON",
            "BODY_SLAM",
            "BURNING_PACT",
            "DEFEND_IRONCLAD",
            "HAVOC",
            "JUGGERNAUT",
            "NOT_YET",
            "POMMEL_STRIKE",
            "SHRUG_IT_OFF",
            "STRIKE_IRONCLAD",
            "SWORD_BOOMERANG",
            "TREMBLE",
            "TWIN_STRIKE"
        };

    private static readonly HashSet<string> ExpectedMarineImplementedExternalKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "MARINE_SHOOT",
            "MARINE_ELBOW",
            "MARINE_BLOCK",
            "MARINE_RELOAD",
            "MARINE_STIM",
            "MARINE_CORE_EXPANSION",
            "MARINE_OUTPUT_ADJUST",
            "MARINE_BLAST_SHIELD",
            "MARINE_MAG_EXPANSION",
            "MARINE_INCENDIARY_AMMO",
            "MARINE_SMOKE_PERSIST",
            "MARINE_KUNGFU_MECH",
            "MARINE_OVERLOAD",
            "MARINE_TUMBLE_RELOAD",
            "MARINE_RETREAT",
            "MARINE_GAS_PUMP",
            "MARINE_NAPALM",
            "MARINE_MOLOTOV",
            "MARINE_STUN_GRENADE",
            "MARINE_HOLD_LINE",
            "MARINE_SMOKE_BOMB",
            "MARINE_INCOMPLETE_COMBUSTION",
            "MARINE_KNOCKBACK_SHOT",
            "MARINE_SPRAY",
            "MARINE_BAYONET_PARRY",
            "MARINE_WILD_RAMPAGE",
            "MARINE_QUICK_ELBOW",
            "MARINE_KIDNEY_SHOT",
            "MARINE_PAINFUL_ELBOW",
            "MARINE_HEAVY_ELBOW",
            "MARINE_FIELD_SURGERY",
            "MARINE_GARRISON",
            "MARINE_HURRICANE_ELBOW",
            "MARINE_PRECISION_SHOT",
            "MARINE_TACTICAL_ADVANCE",
            "MARINE_QUICK_ROLL",
            "MARINE_ELECTRO_BOOST",
            "MARINE_GUIDED_NUKE",
            "MARINE_BANSHEE_STRIKE",
            "MARINE_FIRE_SUPPORT",
            "MARINE_FIRE_BOMBARDMENT",
            "MARINE_FIVE_HUNDRED_POUNDER",
            "MARINE_COMBO_ELBOW",
            "MARINE_OPPORTUNISTIC_STRIKE",
            "MARINE_VENT_HEAT",
            "MARINE_POWER_OVERCLOCK",
            "MARINE_SNIPER_SHOT",
            "MARINE_SPIKE_SHOT",
            "MARINE_OPTICAL_CAMO",
            "MARINE_UNSTOPPABLE",
            "MARINE_GUERRILLA_TACTICS",
            "MARINE_EXPLOSIVE_ELBOW",
            "MARINE_AGED_OIL",
            "MARINE_BURNING_OIL",
            "MARINE_FLAME_ELBOW",
            "MARINE_SIX_HITS",
            "MARINE_TWELVE_HITS",
            "MARINE_QUICK_MANEUVER",
            "MARINE_HOLO_DECOY",
            "MARINE_LIMIT_OVERLOAD",
            "MARINE_MACHINEGUN",
            "MARINE_DEFENSE_TARGET",
            "MARINE_MACHINEGUN_BURST",
            "MARINE_TRIPLE_STRIKE"
        };

    private static readonly string[] ExpectedMarineExternalKeysInIdOrder =
    {
        "MARINE_SHOOT",
        "MARINE_ELBOW",
        "MARINE_BLOCK",
        "MARINE_RELOAD",
        "MARINE_STIM",
        "MARINE_CORE_EXPANSION",
        "MARINE_OUTPUT_ADJUST",
        "MARINE_BLAST_SHIELD",
        "MARINE_MAG_EXPANSION",
        "MARINE_INCENDIARY_AMMO",
        "MARINE_SMOKE_PERSIST",
        "MARINE_KUNGFU_MECH",
        "MARINE_OVERLOAD",
        "MARINE_TUMBLE_RELOAD",
        "MARINE_STUN_GRENADE",
        "MARINE_RETREAT",
        "MARINE_GAS_PUMP",
        "MARINE_NAPALM",
        "MARINE_MOLOTOV",
        "MARINE_HOLD_LINE",
        "MARINE_SMOKE_BOMB",
        "MARINE_INCOMPLETE_COMBUSTION",
        "MARINE_KNOCKBACK_SHOT",
        "MARINE_SPRAY",
        "MARINE_BAYONET_PARRY",
        "MARINE_WILD_RAMPAGE",
        "MARINE_QUICK_ELBOW",
        "MARINE_KIDNEY_SHOT",
        "MARINE_PAINFUL_ELBOW",
        "MARINE_HEAVY_ELBOW",
        "MARINE_FIELD_SURGERY",
        "MARINE_HURRICANE_ELBOW",
        "MARINE_PRECISION_SHOT",
        "MARINE_TACTICAL_ADVANCE",
        "MARINE_QUICK_ROLL",
        "MARINE_ELECTRO_BOOST",
        "MARINE_GUIDED_NUKE",
        "MARINE_BANSHEE_STRIKE",
        "MARINE_FIRE_SUPPORT",
        "MARINE_FIRE_BOMBARDMENT",
        "MARINE_FIVE_HUNDRED_POUNDER",
        "MARINE_COMBO_ELBOW",
        "MARINE_OPPORTUNISTIC_STRIKE",
        "MARINE_VENT_HEAT",
        "MARINE_POWER_OVERCLOCK",
        "MARINE_GARRISON",
        "MARINE_SNIPER_SHOT",
        "MARINE_SPIKE_SHOT",
        "MARINE_OPTICAL_CAMO",
        "MARINE_UNSTOPPABLE",
        "MARINE_GUERRILLA_TACTICS",
        "MARINE_EXPLOSIVE_ELBOW",
        "MARINE_AGED_OIL",
        "MARINE_BURNING_OIL",
        "MARINE_FLAME_ELBOW",
        "MARINE_SIX_HITS",
        "MARINE_TWELVE_HITS",
        "MARINE_QUICK_MANEUVER",
        "MARINE_HOLO_DECOY",
        "MARINE_LIMIT_OVERLOAD",
        "MARINE_MACHINEGUN",
        "MARINE_DEFENSE_TARGET",
        "MARINE_MACHINEGUN_BURST",
        "MARINE_TRIPLE_STRIKE",
    };

    private static readonly string[] ExpectedMarineV2ExtensionExternalKeysInIdOrder =
    {
        "MARINE_BOMBARD",
        "MARINE_SKY_WRATH",
        "MARINE_PORTABLE_HELPER",
        "MARINE_PRIVATE_MOD",
        "MARINE_CHAIN_SMOKE",
        "MARINE_SECONDHAND_SMOKE",
        "MARINE_DEFENSIVE_STANCE",
        "MARINE_EMERGENCY_COOLING",
        "MARINE_THERMITE_BOMB",
        "MARINE_NEEDLE_STORM",
        "MARINE_STEALTH_ACTION",
        "MARINE_FOEHN_WIND",
        "MARINE_PREEMPTIVE_STRIKE",
        "MARINE_BULLY",
        "MARINE_PRISMATIC_SHOT",
        "MARINE_MARK",
        "MARINE_CRUSH",
        "MARINE_CHARGED_BURST",
    };

    private static readonly HashSet<string> ExpectedMarineV2ExtensionImplementedExternalKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "MARINE_BOMBARD",
            "MARINE_SKY_WRATH",
            "MARINE_PORTABLE_HELPER",
            "MARINE_PRIVATE_MOD",
            "MARINE_CHAIN_SMOKE",
            "MARINE_SECONDHAND_SMOKE",
            "MARINE_DEFENSIVE_STANCE",
            "MARINE_EMERGENCY_COOLING",
            "MARINE_THERMITE_BOMB",
            "MARINE_NEEDLE_STORM",
            "MARINE_STEALTH_ACTION",
            "MARINE_FOEHN_WIND",
            "MARINE_PREEMPTIVE_STRIKE",
            "MARINE_BULLY",
            "MARINE_PRISMATIC_SHOT",
            "MARINE_MARK",
            "MARINE_CRUSH",
            "MARINE_CHARGED_BURST",
        };

    private static readonly HashSet<string> ExpectedMarineV2ExtensionInnateExternalKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "MARINE_STEALTH_ACTION",
        };

    /// <summary>读取当前生成表并在任何本地化或 Addressables 写入前完成目录校验。</summary>
    internal static void ValidateCurrentProject()
    {
        JObject decks = ReadRequiredTable(DeckTableJsonPath);
        JObject cards = ReadRequiredTable(CardTableJsonPath);
        JObject effects = ReadRequiredTable(EffectTableJsonPath);
        Validate(decks, cards, effects);
        ValidateIroncladV01071Snapshot(cards);
        ValidateMarineGameV1CardSnapshot(cards);
        ValidateMarineGameV2ExtensionSnapshot(cards);
        AddressablesBuildTools.ValidateCardIllustrations(cards);
    }

    /// <summary>校验 Marine Game v1 的 64 张机枪兵目录身份、稳定 ID、程序绑定与分阶段开放门禁。</summary>
    internal static void ValidateMarineGameV1CardSnapshot(JObject cards)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));

        var expectedExternalKeys = new HashSet<string>(
            ExpectedMarineExternalKeysInIdOrder,
            StringComparer.Ordinal);
        var snapshotCards = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (JProperty property in cards.Properties())
        {
            JObject card = property.Value as JObject
                ?? throw new InvalidOperationException($"Card record '{property.Name}' must be an object.");
            string snapshotKey = ReadRequiredString(card, "catalog_snapshot_key", property.Name);
            if (!string.Equals(snapshotKey, MarineSnapshotKey, StringComparison.Ordinal))
                continue;

            string externalKey = ReadRequiredString(card, "external_key", property.Name);
            if (!expectedExternalKeys.Contains(externalKey))
                throw new InvalidOperationException($"Unexpected Marine external_key '{externalKey}'.");
            if (snapshotCards.ContainsKey(externalKey))
                throw new InvalidOperationException($"Duplicate Marine external_key '{externalKey}'.");
            snapshotCards.Add(externalKey, card);
        }

        var actualExternalKeys = new HashSet<string>(snapshotCards.Keys, StringComparer.Ordinal);
        if (!actualExternalKeys.SetEquals(expectedExternalKeys))
        {
            string missing = string.Join(
                ", ",
                expectedExternalKeys.Except(snapshotCards.Keys).OrderBy(key => key, StringComparer.Ordinal));
            string unexpected = string.Join(
                ", ",
                snapshotCards.Keys.Except(expectedExternalKeys).OrderBy(key => key, StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"Marine snapshot identities drifted: missing [{missing}]; unexpected [{unexpected}].");
        }

        for (int index = 0; index < ExpectedMarineExternalKeysInIdOrder.Length; index++)
        {
            string externalKey = ExpectedMarineExternalKeysInIdOrder[index];
            JObject card = snapshotCards[externalKey];
            int expectedId = FirstMarineCardId + index;
            int actualId = ReadRequiredInt(card, "id", externalKey);
            if (actualId != expectedId)
            {
                throw new InvalidOperationException(
                    $"Marine card '{externalKey}' must keep id {expectedId}, but found {actualId}.");
            }

            int expectedProgramId = index + 1;
            int actualProgramId = ReadRequiredInt(card, "program_id", externalKey);
            if (actualProgramId != expectedProgramId)
            {
                throw new InvalidOperationException(
                    $"Marine card '{externalKey}' must bind program {expectedProgramId}, but found {actualProgramId}.");
            }

            if (ReadRequiredBool(card, "is_innate", externalKey))
            {
                throw new InvalidOperationException(
                    $"Marine card '{externalKey}' must keep is_innate=false.");
            }

            bool isImplemented = ExpectedMarineImplementedExternalKeys.Contains(externalKey);
            int expectedImplementationStatus = isImplemented
                ? (int)cfg.battle.CardImplementationStatus.Implemented
                : (int)cfg.battle.CardImplementationStatus.CatalogOnly;
            if (ReadRequiredInt(card, "implementation_status", externalKey) != expectedImplementationStatus)
            {
                string expectedStatus = isImplemented
                    ? "Implemented"
                    : "CatalogOnly";
                throw new InvalidOperationException(
                    $"Marine card '{externalKey}' must remain {expectedStatus} in the current runtime slice.");
            }
            if (!string.Equals(
                    ReadRequiredString(card, "illustration_key", externalKey),
                    CatalogPlaceholderIllustrationKey,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Marine card '{externalKey}' must use placeholder illustration_key " +
                    $"'{CatalogPlaceholderIllustrationKey}'.");
            }

            if (!(card["effect_bindings"] is JArray bindings) || bindings.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Marine card '{externalKey}' must have empty effect_bindings during catalog intake.");
            }

            bool expectedHasUpgrade = !string.Equals(
                externalKey,
                "MARINE_MACHINEGUN_BURST",
                StringComparison.Ordinal);
            if (card["has_upgrade"]?.Type != JTokenType.Boolean ||
                card.Value<bool>("has_upgrade") != expectedHasUpgrade)
            {
                throw new InvalidOperationException(
                    $"Marine card '{externalKey}' has unexpected has_upgrade value.");
            }
        }
    }

    /// <summary>校验 V2 新增的 18 张机枪兵目录卡保持独立快照、连续身份与精确实现状态门禁，不改写既有 V1 的 64 张冻结身份。</summary>
    internal static void ValidateMarineGameV2ExtensionSnapshot(JObject cards)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));

        var expectedExternalKeys = new HashSet<string>(
            ExpectedMarineV2ExtensionExternalKeysInIdOrder,
            StringComparer.Ordinal);
        var snapshotCards = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (JProperty property in cards.Properties())
        {
            JObject card = property.Value as JObject
                ?? throw new InvalidOperationException($"Card record '{property.Name}' must be an object.");
            string snapshotKey = ReadRequiredString(card, "catalog_snapshot_key", property.Name);
            if (!string.Equals(snapshotKey, MarineV2ExtensionSnapshotKey, StringComparison.Ordinal))
                continue;

            string externalKey = ReadRequiredString(card, "external_key", property.Name);
            if (!expectedExternalKeys.Contains(externalKey))
            {
                throw new InvalidOperationException(
                    $"Unexpected Marine Game V2 extension external_key '{externalKey}'.");
            }
            if (snapshotCards.ContainsKey(externalKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate Marine Game V2 extension external_key '{externalKey}'.");
            }
            snapshotCards.Add(externalKey, card);
        }

        var actualExternalKeys = new HashSet<string>(snapshotCards.Keys, StringComparer.Ordinal);
        if (!actualExternalKeys.SetEquals(expectedExternalKeys))
        {
            string missing = string.Join(
                ", ",
                expectedExternalKeys.Except(snapshotCards.Keys).OrderBy(key => key, StringComparer.Ordinal));
            string unexpected = string.Join(
                ", ",
                snapshotCards.Keys.Except(expectedExternalKeys).OrderBy(key => key, StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"Marine Game V2 extension snapshot identities drifted: missing [{missing}]; unexpected [{unexpected}].");
        }

        for (int index = 0; index < ExpectedMarineV2ExtensionExternalKeysInIdOrder.Length; index++)
        {
            string externalKey = ExpectedMarineV2ExtensionExternalKeysInIdOrder[index];
            JObject card = snapshotCards[externalKey];
            int expectedId = FirstMarineV2ExtensionCardId + index;
            int actualId = ReadRequiredInt(card, "id", externalKey);
            if (actualId != expectedId)
            {
                throw new InvalidOperationException(
                    $"Marine Game V2 extension card '{externalKey}' must keep id {expectedId}, but found {actualId}.");
            }

            int expectedProgramId = FirstMarineV2ExtensionProgramId + index;
            int actualProgramId = ReadRequiredInt(card, "program_id", externalKey);
            if (actualProgramId != expectedProgramId)
            {
                throw new InvalidOperationException(
                    $"Marine Game V2 extension card '{externalKey}' must bind program {expectedProgramId}, but found {actualProgramId}.");
            }

            bool isImplemented = ExpectedMarineV2ExtensionImplementedExternalKeys.Contains(externalKey);
            int expectedImplementationStatus = isImplemented
                ? (int)cfg.battle.CardImplementationStatus.Implemented
                : (int)cfg.battle.CardImplementationStatus.CatalogOnly;
            if (ReadRequiredInt(card, "implementation_status", externalKey) != expectedImplementationStatus)
            {
                string expectedStatus = isImplemented ? "Implemented" : "CatalogOnly";
                throw new InvalidOperationException(
                    $"Marine Game V2 extension card '{externalKey}' must remain {expectedStatus} in the current runtime slice.");
            }
            bool expectedInnate =
                ExpectedMarineV2ExtensionInnateExternalKeys.Contains(externalKey);
            if (ReadRequiredBool(card, "is_innate", externalKey) != expectedInnate)
            {
                throw new InvalidOperationException(
                    $"Marine Game V2 extension card '{externalKey}' must keep " +
                    $"is_innate={expectedInnate.ToString().ToLowerInvariant()}.");
            }
            if (!string.Equals(
                    ReadRequiredString(card, "illustration_key", externalKey),
                    CatalogPlaceholderIllustrationKey,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Marine Game V2 extension card '{externalKey}' must use placeholder illustration_key " +
                    $"'{CatalogPlaceholderIllustrationKey}'.");
            }
            if (!(card["effect_bindings"] is JArray bindings) || bindings.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Marine Game V2 extension card '{externalKey}' must have empty effect_bindings during catalog intake.");
            }
            if (card["has_upgrade"]?.Type != JTokenType.Boolean || !card.Value<bool>("has_upgrade"))
            {
                throw new InvalidOperationException(
                    $"Marine Game V2 extension card '{externalKey}' must preserve has_upgrade=true metadata.");
            }
        }
    }

    /// <summary>校验冻结的 STS2 v0.107.1 战士单人目录身份、元数据聚合与不可玩隔离。</summary>
    internal static void ValidateIroncladV01071Snapshot(JObject cards)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));

        var allExternalKeys = new HashSet<string>(StringComparer.Ordinal);
        var snapshotExternalKeys = new HashSet<string>(StringComparer.Ordinal);
        var implementedExternalKeys = new HashSet<string>(StringComparer.Ordinal);
        var xCostExternalKeys = new HashSet<string>(StringComparer.Ordinal);
        int[] typeCounts = new int[3];
        int[] rarityCounts = new int[5];
        int[] targetCounts = new int[4];
        int[] destinationCounts = new int[3];
        int implementedCount = 0;
        int catalogOnlyCount = 0;

        foreach (JProperty property in cards.Properties())
        {
            JObject card = property.Value as JObject
                ?? throw new InvalidOperationException($"Card record '{property.Name}' must be an object.");
            string externalKey = ReadRequiredString(card, "external_key", property.Name);
            if (!allExternalKeys.Add(externalKey))
                throw new InvalidOperationException($"Duplicate external_key '{externalKey}'.");
            if (string.Equals(externalKey, "DEMONIC_SHIELD", StringComparison.Ordinal)
                || string.Equals(externalKey, "TANK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Multiplayer-only external_key '{externalKey}' must not enter the solo snapshot.");
            }

            string snapshotKey = ReadRequiredString(card, "catalog_snapshot_key", property.Name);
            if (!string.Equals(snapshotKey, IroncladSnapshotKey, StringComparison.Ordinal))
                continue;

            if (!ExpectedIroncladExternalKeys.Contains(externalKey))
                throw new InvalidOperationException($"Unexpected Ironclad external_key '{externalKey}'.");
            snapshotExternalKeys.Add(externalKey);

            ReadRequiredString(card, "name_i18n_key", property.Name);
            ReadRequiredString(card, "description_i18n_key", property.Name);
            ReadRequiredString(card, "upgraded_description_i18n_key", property.Name);
            IncrementKnownEnum(typeCounts, ReadRequiredInt(card, "card_type", property.Name), "card_type", externalKey);
            IncrementKnownEnum(rarityCounts, ReadRequiredInt(card, "rarity", property.Name), "rarity", externalKey);
            IncrementKnownEnum(targetCounts, ReadRequiredInt(card, "target_rule", property.Name), "target_rule", externalKey);
            IncrementKnownEnum(
                destinationCounts,
                ReadRequiredInt(card, "play_destination", property.Name),
                "play_destination",
                externalKey);
            int upgradedDestination =
                ReadRequiredInt(card, "upgraded_play_destination", property.Name);
            if (upgradedDestination < 0 || upgradedDestination >= 3)
            {
                throw new InvalidOperationException(
                    $"Ironclad card '{externalKey}' has unknown upgraded_play_destination " +
                    $"{upgradedDestination}.");
            }

            int cost = ReadRequiredInt(card, "cost", property.Name);
            ReadRequiredInt(card, "upgraded_cost", property.Name);
            int costKind = ReadRequiredInt(card, "cost_kind", property.Name);
            if (costKind == (int)cfg.battle.CardCostKind.Fixed)
            {
                if (cost < 0)
                    throw new InvalidOperationException($"Fixed-cost card '{externalKey}' has negative cost {cost}.");
            }
            else if (costKind == (int)cfg.battle.CardCostKind.X)
            {
                xCostExternalKeys.Add(externalKey);
            }
            else
            {
                throw new InvalidOperationException($"Card '{externalKey}' has unknown cost_kind {costKind}.");
            }

            JToken hasUpgradeToken = card["has_upgrade"];
            if (hasUpgradeToken == null
                || hasUpgradeToken.Type != JTokenType.Boolean
                || !(bool)hasUpgradeToken)
            {
                throw new InvalidOperationException($"Ironclad card '{externalKey}' must declare has_upgrade=true.");
            }

            int implementationStatus = ReadRequiredInt(card, "implementation_status", property.Name);
            if (implementationStatus == (int)cfg.battle.CardImplementationStatus.Implemented)
            {
                implementedCount++;
                implementedExternalKeys.Add(externalKey);
            }
            else if (implementationStatus == (int)cfg.battle.CardImplementationStatus.CatalogOnly)
                catalogOnlyCount++;
            else
                throw new InvalidOperationException(
                    $"Ironclad card '{externalKey}' has unknown implementation_status {implementationStatus}.");
        }

        if (!snapshotExternalKeys.SetEquals(ExpectedIroncladExternalKeys))
        {
            string missing = string.Join(", ", ExpectedIroncladExternalKeys
                .Except(snapshotExternalKeys)
                .OrderBy(key => key, StringComparer.Ordinal));
            throw new InvalidOperationException($"Ironclad snapshot missing external keys: {missing}");
        }

        RequireDistribution(typeCounts, new[] { 37, 29, 19 }, "card_type");
        RequireDistribution(rarityCounts, new[] { 3, 20, 35, 25, 2 }, "rarity");
        RequireDistribution(targetCounts, new[] { 45, 32, 7, 1 }, "target_rule");
        RequireDistribution(destinationCounts, new[] { 56, 10, 19 }, "play_destination");
        if (!xCostExternalKeys.SetEquals(new[] { "CASCADE", "WHIRLWIND" }))
        {
            throw new InvalidOperationException(
                $"Ironclad X-cost external keys drifted: {string.Join(", ", xCostExternalKeys.OrderBy(key => key, StringComparer.Ordinal))}");
        }
        if (implementedCount != 15 || catalogOnlyCount != 70)
        {
            throw new InvalidOperationException(
                $"Ironclad implementation split must be 15 Implemented / 70 CatalogOnly, " +
                $"but was {implementedCount} / {catalogOnlyCount}.");
        }
        if (!implementedExternalKeys.SetEquals(ExpectedIroncladImplementedExternalKeys))
        {
            string missing = string.Join(
                ", ",
                ExpectedIroncladImplementedExternalKeys
                    .Except(implementedExternalKeys)
                    .OrderBy(key => key, StringComparer.Ordinal));
            string unexpected = string.Join(
                ", ",
                implementedExternalKeys
                    .Except(ExpectedIroncladImplementedExternalKeys)
                    .OrderBy(key => key, StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"Ironclad Implemented external keys drifted: " +
                $"missing [{missing}]; unexpected [{unexpected}].");
        }
    }

    /// <summary>校验牌组只引用存在且已经实现的卡牌模板。</summary>
    internal static void Validate(JObject decks, JObject cards, JObject effects)
    {
        if (decks == null)
            throw new ArgumentNullException(nameof(decks));
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));
        if (effects == null)
            throw new ArgumentNullException(nameof(effects));

        ValidateRecordKeys(decks, "battle_tbdeck");
        ValidateRecordKeys(cards, "battle_tbcard");
        ValidateRecordKeys(effects, "battle_tbcardeffect");

        foreach (JProperty card in cards.Properties())
        {
            int cardId = (int)card.Value["id"];
            ReadRequiredBool((JObject)card.Value, "is_innate", cardId.ToString());
            string illustrationKey = (string)card.Value["illustration_key"];
            try
            {
                CardIllustrationAddress.FromKey(illustrationKey);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"Card {cardId} has invalid illustration_key '{illustrationKey}'.",
                    exception);
            }

            int implementationStatus = (int)card.Value["implementation_status"];
            if (implementationStatus != (int)cfg.battle.CardImplementationStatus.Implemented
                && implementationStatus != (int)cfg.battle.CardImplementationStatus.CatalogOnly)
            {
                throw new InvalidOperationException(
                    $"Card {cardId} has unknown implementation_status {implementationStatus}.");
            }
            if (implementationStatus == (int)cfg.battle.CardImplementationStatus.CatalogOnly)
            {
                if (!string.Equals(
                        illustrationKey,
                        CatalogPlaceholderIllustrationKey,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CatalogOnly card {cardId} must use placeholder illustration_key "
                        + $"'{CatalogPlaceholderIllustrationKey}', but found '{illustrationKey}'.");
                }

                continue;
            }

            int programId = card.Value["program_id"]?.Type == JTokenType.Integer
                ? (int)card.Value["program_id"]
                : 0;
            var bindings = card.Value["effect_bindings"] as JArray;
            if (programId != 0)
            {
                if (bindings == null || bindings.Count != 0)
                {
                    throw new InvalidOperationException(
                        $"Implemented program card {cardId} must have empty effect_bindings.");
                }

                continue;
            }
            if (bindings == null || bindings.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Implemented card {cardId} has no effect_bindings.");
            }

            var argumentKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken binding in bindings)
            {
                string argumentKey = (string)binding["argument_key"];
                if (string.IsNullOrWhiteSpace(argumentKey))
                {
                    throw new InvalidOperationException(
                        $"Implemented card {cardId} has an empty effect argument key.");
                }
                if (!argumentKeys.Add(argumentKey))
                {
                    throw new InvalidOperationException(
                        $"Implemented card {cardId} contains duplicate effect argument '{argumentKey}'.");
                }

                int effectId = (int)binding["effect_id"];
                if (effects[effectId.ToString()] == null)
                {
                    throw new InvalidOperationException(
                        $"Implemented card {cardId} binding '{argumentKey}' references missing effect {effectId}.");
                }
            }
        }

        foreach (JProperty deck in decks.Properties())
        {
            int deckId = (int)deck.Value["id"];
            int innateCardCount = 0;
            foreach (JToken cardIdToken in (JArray)deck.Value["card_template_ids"])
            {
                int cardId = (int)cardIdToken;
                JObject card = cards[cardId.ToString()] as JObject;
                if (card == null)
                    throw new InvalidOperationException($"Deck {deckId} references missing card {cardId}.");

                int implementationStatus = (int)card["implementation_status"];
                if (implementationStatus == (int)cfg.battle.CardImplementationStatus.CatalogOnly)
                {
                    throw new InvalidOperationException(
                        $"Deck {deckId} references CatalogOnly card {cardId}.");
                }
                if (ReadRequiredBool(card, "is_innate", cardId.ToString()))
                    innateCardCount++;
            }

            if (innateCardCount > OpeningHandLimit)
            {
                throw new InvalidOperationException(
                    $"Deck {deckId} contains {innateCardCount} innate cards, " +
                    $"exceeding opening hand limit {OpeningHandLimit}.");
            }
        }
    }

    /// <summary>读取目录记录的必填非空字符串字段。</summary>
    private static string ReadRequiredString(JObject record, string fieldName, string recordName)
    {
        JToken token = record[fieldName];
        string value = token?.Type == JTokenType.String ? (string)token : null;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Card {recordName} has no {fieldName}.");

        return value;
    }

    /// <summary>读取目录记录的必填整数值字段。</summary>
    private static int ReadRequiredInt(JObject record, string fieldName, string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.Integer)
            throw new InvalidOperationException($"Card {recordName} has no integer {fieldName}.");

        return (int)token;
    }

    /// <summary>读取目录记录的必填布尔字段，拒绝缺失、数字或字符串代替。</summary>
    private static bool ReadRequiredBool(JObject record, string fieldName, string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.Boolean)
            throw new InvalidOperationException($"Card {recordName} has no boolean {fieldName}.");

        return (bool)token;
    }

    /// <summary>累加已知枚举值，并在越界时报告卡牌身份。</summary>
    private static void IncrementKnownEnum(int[] counts, int value, string fieldName, string externalKey)
    {
        if (value < 0 || value >= counts.Length)
        {
            throw new InvalidOperationException(
                $"Ironclad card '{externalKey}' has unknown {fieldName} {value}.");
        }

        counts[value]++;
    }

    /// <summary>确认冻结目录的枚举聚合没有漂移。</summary>
    private static void RequireDistribution(int[] actual, int[] expected, string fieldName)
    {
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                $"Ironclad {fieldName} distribution drifted: {string.Join(", ", actual)}.");
        }
    }

    /// <summary>确认生成 JSON 的顶层记录键与记录 ID 完全一致。</summary>
    private static void ValidateRecordKeys(JObject table, string tableName)
    {
        foreach (JProperty property in table.Properties())
        {
            if (property.Value is not JObject record)
            {
                throw new InvalidOperationException(
                    $"{tableName} record '{property.Name}' must be an object.");
            }

            JToken idToken = record["id"];
            if (idToken == null
                || idToken.Type != JTokenType.Integer
                || !int.TryParse(
                    idToken.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int recordId))
            {
                throw new InvalidOperationException(
                    $"{tableName} record key '{property.Name}' has no integer id.");
            }

            string expectedKey = recordId.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(property.Name, expectedKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{tableName} record key '{property.Name}' does not match id {recordId}.");
            }
        }
    }

    /// <summary>读取一份必需的生成表，并拒绝文件缺失或空表。</summary>
    private static JObject ReadRequiredTable(string tableJsonPath)
    {
        if (!File.Exists(tableJsonPath))
            throw new InvalidOperationException($"Generated table does not exist: {tableJsonPath}");

        JObject table = JObject.Parse(File.ReadAllText(tableJsonPath));
        if (table.Count == 0)
            throw new InvalidOperationException($"Generated table has no records: {tableJsonPath}");

        return table;
    }
}
