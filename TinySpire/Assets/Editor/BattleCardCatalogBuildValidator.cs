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
            "BASH",
            "DEFEND_IRONCLAD",
            "STRIKE_IRONCLAD",
            "TREMBLE"
        };

    /// <summary>读取当前生成表并在任何本地化或 Addressables 写入前完成目录校验。</summary>
    internal static void ValidateCurrentProject()
    {
        JObject decks = ReadRequiredTable(DeckTableJsonPath);
        JObject cards = ReadRequiredTable(CardTableJsonPath);
        JObject effects = ReadRequiredTable(EffectTableJsonPath);
        Validate(decks, cards, effects);
        ValidateIroncladV01071Snapshot(cards);
        AddressablesBuildTools.ValidateCardIllustrations(cards);
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
        if (implementedCount != 4 || catalogOnlyCount != 81)
        {
            throw new InvalidOperationException(
                $"Ironclad implementation split must be 4 Implemented / 81 CatalogOnly, " +
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

            var bindings = card.Value["effect_bindings"] as JArray;
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
            foreach (JToken cardIdToken in (JArray)deck.Value["card_template_ids"])
            {
                int cardId = (int)cardIdToken;
                JToken card = cards[cardId.ToString()];
                if (card == null)
                    throw new InvalidOperationException($"Deck {deckId} references missing card {cardId}.");

                int implementationStatus = (int)card["implementation_status"];
                if (implementationStatus == (int)cfg.battle.CardImplementationStatus.CatalogOnly)
                {
                    throw new InvalidOperationException(
                        $"Deck {deckId} references CatalogOnly card {cardId}.");
                }
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
