using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

/// <summary>在本地化与 Addressables 写入前校验 G4-D 卡牌升级配置。</summary>
internal static class BattleCardUpgradeBuildValidator
{
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string HeroTableJsonPath = "Assets/GameData/battle_tbhero.json";
    private const string DeckTableJsonPath = "Assets/GameData/battle_tbdeck.json";
    private const string UpgradeLevelTableJsonPath =
        "Assets/GameData/battle_tbcardupgradelevel.json";

    /// <summary>读取当前生成卡表与逐级升级表，执行一般合同和冻结生产内容门禁。</summary>
    internal static void ValidateCurrentProject()
    {
        JObject cards = ReadRequiredObjectTable(CardTableJsonPath);
        ValidateProductionTracks(cards, ReadRequiredArrayTable(UpgradeLevelTableJsonPath));
        ValidateProductionRoles(
            cards,
            ReadRequiredObjectTable(HeroTableJsonPath),
            ReadRequiredObjectTable(DeckTableJsonPath));
    }

    /// <summary>校验卡牌升级轨道与逐级配置之间不存在冲突。</summary>
    internal static void Validate(JObject cards, JArray upgradeLevels)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));
        if (upgradeLevels == null)
            throw new ArgumentNullException(nameof(upgradeLevels));

        Dictionary<int, CardTrackFacts> factsByCardId = ReadCardTrackFacts(cards);
        ReadAndValidateLevelRows(upgradeLevels, factsByCardId);
        foreach (CardTrackFacts facts in factsByCardId.Values)
            ValidateTrackTopology(facts);
    }

    /// <summary>先执行一般合同，再锁定 G4-D 选定的四张生产升级卡。</summary>
    internal static void ValidateProductionTracks(JObject cards, JArray upgradeLevels)
    {
        Validate(cards, upgradeLevels);
        RequireProductionTrack(
            cards,
            cardId: 3002,
            cfg.battle.CardUpgradeTrackKind.Finite,
            cfg.battle.CardUpgradeRuleKind.None,
            expectedDelta: 0);
        RequireProductionTrack(
            cards,
            cardId: 3123,
            cfg.battle.CardUpgradeTrackKind.Infinite,
            cfg.battle.CardUpgradeRuleKind.DamageValue,
            expectedDelta: 10);
        RequireProductionTrack(
            cards,
            cardId: 3201,
            cfg.battle.CardUpgradeTrackKind.Infinite,
            cfg.battle.CardUpgradeRuleKind.DamageValue,
            expectedDelta: 3);
        RequireProductionTrack(
            cards,
            cardId: 3207,
            cfg.battle.CardUpgradeTrackKind.Finite,
            cfg.battle.CardUpgradeRuleKind.None,
            expectedDelta: 0);
    }

    /// <summary>锁定两名 Hero 各一张 Basic 初始牌与一张 Implemented 非 Basic 奖励牌。</summary>
    internal static void ValidateProductionRoles(
        JObject cards,
        JObject heroes,
        JObject decks)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));
        if (heroes == null)
            throw new ArgumentNullException(nameof(heroes));
        if (decks == null)
            throw new ArgumentNullException(nameof(decks));

        RequireProductionHeroRoles(
            cards,
            heroes,
            decks,
            heroId: 1001,
            basicCardId: 3002,
            rewardCardId: 3123);
        RequireProductionHeroRoles(
            cards,
            heroes,
            decks,
            heroId: 1002,
            basicCardId: 3201,
            rewardCardId: 3207);
    }

    /// <summary>校验一名生产 Hero 的初始牌组与奖励池精确承担冻结升级卡职责。</summary>
    private static void RequireProductionHeroRoles(
        JObject cards,
        JObject heroes,
        JObject decks,
        int heroId,
        int basicCardId,
        int rewardCardId)
    {
        JObject hero = ReadRequiredRecord(heroes, heroId, "battle_tbhero");
        int deckId = ReadRequiredInt(hero, "initial_deck_id", heroId.ToString(CultureInfo.InvariantCulture));
        JObject deck = ReadRequiredRecord(decks, deckId, "battle_tbdeck");
        IReadOnlyCollection<int> initialCards = ReadRequiredIntCollection(
            deck,
            "card_template_ids",
            deckId.ToString(CultureInfo.InvariantCulture));
        IReadOnlyCollection<int> rewardCards = ReadRequiredIntCollection(
            hero,
            "reward_card_template_ids",
            heroId.ToString(CultureInfo.InvariantCulture));
        JObject basicCard = ReadRequiredRecord(cards, basicCardId, "battle_tbcard");
        JObject rewardCard = ReadRequiredRecord(cards, rewardCardId, "battle_tbcard");

        RequireImplementedCard(basicCard, basicCardId);
        RequireImplementedCard(rewardCard, rewardCardId);
        cfg.battle.CardRarity basicRarity = ReadCardRarity(basicCard, basicCardId);
        cfg.battle.CardRarity rewardRarity = ReadCardRarity(rewardCard, rewardCardId);
        bool rewardRarityAllowed = rewardRarity == cfg.battle.CardRarity.Common ||
                                   rewardRarity == cfg.battle.CardRarity.Uncommon ||
                                   rewardRarity == cfg.battle.CardRarity.Rare;
        if (basicRarity != cfg.battle.CardRarity.Basic ||
            !initialCards.Contains(basicCardId) ||
            rewardCards.Contains(basicCardId))
        {
            throw new InvalidOperationException(
                $"Production card {basicCardId} must be Hero {heroId}'s Basic initial-only card.");
        }
        if (!rewardRarityAllowed || !rewardCards.Contains(rewardCardId))
        {
            throw new InvalidOperationException(
                $"Production card {rewardCardId} must be Hero {heroId}'s non-Basic reward card.");
        }
    }

    /// <summary>读取带稳定顶层键与一致 ID 的必需对象记录。</summary>
    private static JObject ReadRequiredRecord(JObject table, int recordId, string tableName)
    {
        string key = recordId.ToString(CultureInfo.InvariantCulture);
        JObject record = table[key] as JObject
            ?? throw new InvalidOperationException($"{tableName} required record {recordId} is missing.");
        int actualId = ReadRequiredInt(record, "id", key);
        ValidateRecordKey(key, actualId, tableName);
        return record;
    }

    /// <summary>读取必需的整数数组，并拒绝非整数成员。</summary>
    private static IReadOnlyCollection<int> ReadRequiredIntCollection(
        JObject record,
        string fieldName,
        string recordName)
    {
        JArray values = record[fieldName] as JArray
            ?? throw new InvalidOperationException($"Record {recordName} has no array {fieldName}.");
        var result = new List<int>(values.Count);
        foreach (JToken value in values)
        {
            if (value.Type != JTokenType.Integer)
            {
                throw new InvalidOperationException(
                    $"Record {recordName} {fieldName} contains a non-integer value.");
            }

            result.Add((int)value);
        }

        return result;
    }

    /// <summary>要求冻结生产卡仍是可真实执行的 Implemented 内容。</summary>
    private static void RequireImplementedCard(JObject card, int cardId)
    {
        int status = ReadRequiredInt(
            card,
            "implementation_status",
            cardId.ToString(CultureInfo.InvariantCulture));
        if (status != (int)cfg.battle.CardImplementationStatus.Implemented)
            throw new InvalidOperationException($"Production card {cardId} must remain Implemented.");
    }

    /// <summary>读取并验证冻结生产卡的稀有度枚举。</summary>
    private static cfg.battle.CardRarity ReadCardRarity(JObject card, int cardId)
    {
        int value = ReadRequiredInt(
            card,
            "rarity",
            cardId.ToString(CultureInfo.InvariantCulture));
        if (!Enum.IsDefined(typeof(cfg.battle.CardRarity), value))
            throw new InvalidOperationException($"Production card {cardId} has invalid rarity {value}.");

        return (cfg.battle.CardRarity)value;
    }

    /// <summary>读取所有卡牌的轨道、无限规则与每级增量事实。</summary>
    private static Dictionary<int, CardTrackFacts> ReadCardTrackFacts(JObject cards)
    {
        var factsByCardId = new Dictionary<int, CardTrackFacts>();
        foreach (JProperty cardProperty in cards.Properties())
        {
            JObject card = cardProperty.Value as JObject
                ?? throw new InvalidOperationException(
                    $"Card record '{cardProperty.Name}' must be an object.");
            int cardId = ReadRequiredInt(card, "id", cardProperty.Name);
            ValidateRecordKey(cardProperty.Name, cardId, "battle_tbcard");
            if (cardId <= 0 || factsByCardId.ContainsKey(cardId))
                throw new InvalidOperationException($"Card upgrade catalog contains duplicate card {cardId}.");

            cfg.battle.CardUpgradeTrackKind trackKind = ReadTrackKind(card, cardId);
            cfg.battle.CardUpgradeRuleKind infiniteRuleKind = ReadRuleKind(
                card,
                "infinite_upgrade_rule_kind",
                cardId.ToString(CultureInfo.InvariantCulture));
            int infiniteDelta = ReadRequiredInt(
                card,
                "infinite_upgrade_value_per_level",
                cardId.ToString(CultureInfo.InvariantCulture));
            factsByCardId.Add(
                cardId,
                new CardTrackFacts(cardId, trackKind, infiniteRuleKind, infiniteDelta));
        }

        return factsByCardId;
    }

    /// <summary>校验每条有限级字段、孤儿引用与卡牌加等级组合唯一性。</summary>
    private static void ReadAndValidateLevelRows(
        JArray upgradeLevels,
        IReadOnlyDictionary<int, CardTrackFacts> factsByCardId)
    {
        var pairs = new HashSet<(int CardId, int NextLevel)>();
        foreach (JToken levelToken in upgradeLevels)
        {
            JObject level = levelToken as JObject
                ?? throw new InvalidOperationException("Card upgrade level record must be an object.");
            int cardId = ReadRequiredInt(level, "card_id", "card upgrade level");
            int nextLevel = ReadRequiredInt(
                level,
                "next_upgrade_level",
                $"card upgrade level {cardId}");
            if (!factsByCardId.TryGetValue(cardId, out CardTrackFacts facts))
                throw new InvalidOperationException($"Card upgrade level references missing card {cardId}.");
            if (nextLevel <= 0)
            {
                throw new InvalidOperationException(
                    $"Card {cardId} has invalid next upgrade level {nextLevel}.");
            }
            if (!pairs.Add((cardId, nextLevel)))
            {
                throw new InvalidOperationException(
                    $"Card {cardId} contains duplicate next upgrade level {nextLevel}.");
            }

            ReadRequiredNonEmptyString(
                level,
                "description_i18n_key",
                $"card {cardId} upgrade level {nextLevel}");
            int cost = ReadRequiredInt(
                level,
                "cost",
                $"card {cardId} upgrade level {nextLevel}");
            if (cost < 0)
                throw new InvalidOperationException($"Card {cardId} upgrade level {nextLevel} has negative cost.");

            int destination = ReadRequiredInt(
                level,
                "play_destination",
                $"card {cardId} upgrade level {nextLevel}");
            if (!Enum.IsDefined(typeof(cfg.battle.CardPlayDestination), destination))
            {
                throw new InvalidOperationException(
                    $"Card {cardId} upgrade level {nextLevel} has invalid play destination {destination}.");
            }

            cfg.battle.CardUpgradeRuleKind ruleKind = ReadRuleKind(
                level,
                "rule_kind",
                $"card {cardId} upgrade level {nextLevel}");
            int ruleValue = ReadRequiredInt(
                level,
                "rule_value",
                $"card {cardId} upgrade level {nextLevel}");
            ValidateLevelRuleValue(cardId, nextLevel, ruleKind, ruleValue);
            facts.Levels.Add(nextLevel);
        }
    }

    /// <summary>按 None、Finite、Infinite 三种轨道验证互斥事实与连续级数。</summary>
    private static void ValidateTrackTopology(CardTrackFacts facts)
    {
        switch (facts.TrackKind)
        {
            case cfg.battle.CardUpgradeTrackKind.None:
                if (facts.Levels.Count > 0)
                    throw new InvalidOperationException($"None-track card {facts.CardId} contains finite levels.");
                RequireNoInfiniteTail(facts);
                return;

            case cfg.battle.CardUpgradeTrackKind.Finite:
                if (facts.Levels.Count == 0)
                    throw new InvalidOperationException($"Finite-track card {facts.CardId} has no levels.");
                RequireNoInfiniteTail(facts);
                facts.Levels.Sort();
                for (int index = 0; index < facts.Levels.Count; index++)
                {
                    int expectedLevel = index + 1;
                    if (facts.Levels[index] != expectedLevel)
                    {
                        throw new InvalidOperationException(
                            $"Finite-track card {facts.CardId} is missing upgrade level {expectedLevel}.");
                    }
                }
                return;

            case cfg.battle.CardUpgradeTrackKind.Infinite:
                if (facts.Levels.Count > 0)
                    throw new InvalidOperationException($"Infinite-track card {facts.CardId} contains finite levels.");
                if (facts.InfiniteRuleKind != cfg.battle.CardUpgradeRuleKind.DamageValue ||
                    facts.InfiniteDelta <= 0)
                {
                    throw new InvalidOperationException(
                        $"Infinite-track card {facts.CardId} requires a positive DamageValue delta.");
                }
                return;

            default:
                throw new InvalidOperationException(
                    $"Card {facts.CardId} has unknown upgrade track {(int)facts.TrackKind}.");
        }
    }

    /// <summary>验证有限级的类型化规则和值组合合法。</summary>
    private static void ValidateLevelRuleValue(
        int cardId,
        int nextLevel,
        cfg.battle.CardUpgradeRuleKind ruleKind,
        int ruleValue)
    {
        if (ruleKind == cfg.battle.CardUpgradeRuleKind.None && ruleValue == 0)
            return;
        if (ruleKind == cfg.battle.CardUpgradeRuleKind.DamageValue && ruleValue > 0)
            return;

        throw new InvalidOperationException(
            $"Card {cardId} upgrade level {nextLevel} has invalid rule value {ruleValue} for {ruleKind}.");
    }

    /// <summary>有限或无升级轨道不得携带无限规则及增量。</summary>
    private static void RequireNoInfiniteTail(CardTrackFacts facts)
    {
        if (facts.InfiniteRuleKind == cfg.battle.CardUpgradeRuleKind.None && facts.InfiniteDelta == 0)
            return;

        throw new InvalidOperationException(
            $"Card {facts.CardId} must use no infinite rule and zero delta for {facts.TrackKind} track.");
    }

    /// <summary>校验一张冻结生产卡的轨道、类型化规则与增量没有漂移。</summary>
    private static void RequireProductionTrack(
        JObject cards,
        int cardId,
        cfg.battle.CardUpgradeTrackKind expectedTrackKind,
        cfg.battle.CardUpgradeRuleKind expectedRuleKind,
        int expectedDelta)
    {
        JObject card = cards[cardId.ToString(CultureInfo.InvariantCulture)] as JObject
            ?? throw new InvalidOperationException($"Required production upgrade card {cardId} is missing.");
        cfg.battle.CardUpgradeTrackKind actualTrackKind = ReadTrackKind(card, cardId);
        cfg.battle.CardUpgradeRuleKind actualRuleKind = ReadRuleKind(
            card,
            "infinite_upgrade_rule_kind",
            cardId.ToString(CultureInfo.InvariantCulture));
        int actualDelta = ReadRequiredInt(
            card,
            "infinite_upgrade_value_per_level",
            cardId.ToString(CultureInfo.InvariantCulture));
        if (actualTrackKind == expectedTrackKind &&
            actualRuleKind == expectedRuleKind &&
            actualDelta == expectedDelta)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Production card {cardId} upgrade track drifted: expected " +
            $"{expectedTrackKind}/{expectedRuleKind}/{expectedDelta}, actual " +
            $"{actualTrackKind}/{actualRuleKind}/{actualDelta}.");
    }

    /// <summary>读取并验证已知的卡牌升级轨道枚举。</summary>
    private static cfg.battle.CardUpgradeTrackKind ReadTrackKind(JObject card, int cardId)
    {
        int value = ReadRequiredInt(
            card,
            "upgrade_track_kind",
            cardId.ToString(CultureInfo.InvariantCulture));
        if (!Enum.IsDefined(typeof(cfg.battle.CardUpgradeTrackKind), value))
            throw new InvalidOperationException($"Card {cardId} has unknown upgrade track {value}.");

        return (cfg.battle.CardUpgradeTrackKind)value;
    }

    /// <summary>读取并验证当前允许的类型化升级规则枚举。</summary>
    private static cfg.battle.CardUpgradeRuleKind ReadRuleKind(
        JObject record,
        string fieldName,
        string recordName)
    {
        int value = ReadRequiredInt(record, fieldName, recordName);
        if (!Enum.IsDefined(typeof(cfg.battle.CardUpgradeRuleKind), value))
        {
            throw new InvalidOperationException(
                $"Record {recordName} has unknown {fieldName} {value}.");
        }

        return (cfg.battle.CardUpgradeRuleKind)value;
    }

    /// <summary>读取必填整数，不接受缺失、字符串或小数替代。</summary>
    private static int ReadRequiredInt(JObject record, string fieldName, string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.Integer)
            throw new InvalidOperationException($"Record {recordName} has no integer {fieldName}.");

        return (int)token;
    }

    /// <summary>读取必填非空字符串，并拒绝只含空白的本地化键。</summary>
    private static void ReadRequiredNonEmptyString(
        JObject record,
        string fieldName,
        string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.String ||
            string.IsNullOrWhiteSpace((string)token))
        {
            throw new InvalidOperationException($"Record {recordName} has no non-empty {fieldName}.");
        }
    }

    /// <summary>确认 JSON 顶层键与卡牌记录 ID 完全一致。</summary>
    private static void ValidateRecordKey(string propertyName, int recordId, string tableName)
    {
        string expected = recordId.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(propertyName, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{tableName} record key '{propertyName}' does not match id {recordId}.");
        }
    }

    /// <summary>读取必需的对象根生成表，并拒绝缺失、坏 JSON 或空表。</summary>
    private static JObject ReadRequiredObjectTable(string tableJsonPath)
    {
        if (!File.Exists(tableJsonPath))
            throw new InvalidOperationException($"Generated table does not exist: {tableJsonPath}");

        JToken token = JToken.Parse(File.ReadAllText(tableJsonPath));
        if (token is not JObject table || table.Count == 0)
            throw new InvalidOperationException($"Generated table has no object records: {tableJsonPath}");

        return table;
    }

    /// <summary>读取必需的数组根生成表，并拒绝缺失或根节点类型错误。</summary>
    private static JArray ReadRequiredArrayTable(string tableJsonPath)
    {
        if (!File.Exists(tableJsonPath))
            throw new InvalidOperationException($"Generated table does not exist: {tableJsonPath}");

        JToken token = JToken.Parse(File.ReadAllText(tableJsonPath));
        if (token is not JArray table)
            throw new InvalidOperationException($"Generated table must be an array: {tableJsonPath}");

        return table;
    }

    /// <summary>保存单张卡牌的升级轨道事实与显式级数。</summary>
    private sealed class CardTrackFacts
    {
        internal int CardId { get; }
        internal cfg.battle.CardUpgradeTrackKind TrackKind { get; }
        internal cfg.battle.CardUpgradeRuleKind InfiniteRuleKind { get; }
        internal int InfiniteDelta { get; }
        internal List<int> Levels { get; } = new List<int>();

        /// <summary>创建一份只包含构建期合法性所需字段的卡牌轨道事实。</summary>
        internal CardTrackFacts(
            int cardId,
            cfg.battle.CardUpgradeTrackKind trackKind,
            cfg.battle.CardUpgradeRuleKind infiniteRuleKind,
            int infiniteDelta)
        {
            CardId = cardId;
            TrackKind = trackKind;
            InfiniteRuleKind = infiniteRuleKind;
            InfiniteDelta = infiniteDelta;
        }
    }
}
