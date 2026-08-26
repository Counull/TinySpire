using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using TinySpire.Battle;
using TinySpire.Run;

/// <summary>在构建写入前校验每名 Hero 的普通战斗卡牌奖励池。</summary>
internal static class BattleHeroRewardPoolBuildValidator
{
    private static readonly int[] RequiredSelectableHeroTemplateIds = { 1001, 1002 };
    private const string HeroTableJsonPath = "Assets/GameData/battle_tbhero.json";
    private const string DeckTableJsonPath = "Assets/GameData/battle_tbdeck.json";
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";

    /// <summary>读取当前生成表与运行时临时卡注册表，执行生产内容门禁。</summary>
    internal static void ValidateCurrentProject()
    {
        Validate(
            ReadRequiredTable(HeroTableJsonPath),
            ReadRequiredTable(DeckTableJsonPath),
            ReadRequiredTable(CardTableJsonPath),
            MachineGunnerCardProgramRegistry.PotentiallyCreatedCardTemplateIds);
    }

    /// <summary>校验 Hero-owned 池的归属、模板、稀有度、实现状态与权重。</summary>
    internal static void Validate(
        JObject heroes,
        JObject decks,
        JObject cards,
        IReadOnlyCollection<int> dynamicTemporaryCardTemplateIds)
    {
        if (heroes == null)
            throw new ArgumentNullException(nameof(heroes));
        if (decks == null)
            throw new ArgumentNullException(nameof(decks));
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));
        if (dynamicTemporaryCardTemplateIds == null)
            throw new ArgumentNullException(nameof(dynamicTemporaryCardTemplateIds));

        var globallyOwnedTemplateIds = new Dictionary<int, int>();
        var dynamicTemplates = new HashSet<int>(dynamicTemporaryCardTemplateIds);
        foreach (int heroTemplateId in RequiredSelectableHeroTemplateIds)
        {
            if (heroes[heroTemplateId.ToString(CultureInfo.InvariantCulture)] is JObject)
                continue;

            throw new InvalidOperationException(
                $"Selectable Hero {heroTemplateId} is missing from battle_tbhero.");
        }

        foreach (JProperty heroProperty in heroes.Properties())
        {
            JObject hero = heroProperty.Value as JObject
                ?? throw new InvalidOperationException(
                    $"Hero record '{heroProperty.Name}' must be an object.");
            int heroTemplateId = ReadRequiredInt(hero, "id", heroProperty.Name);
            ValidateRecordKey(heroProperty.Name, heroTemplateId, "battle_tbhero");

            int initialDeckId = ReadRequiredInt(
                hero,
                "initial_deck_id",
                heroTemplateId.ToString(CultureInfo.InvariantCulture));
            if (decks[initialDeckId.ToString(CultureInfo.InvariantCulture)] is not JObject)
            {
                throw new InvalidOperationException(
                    $"Hero {heroTemplateId} references missing initial deck {initialDeckId}.");
            }

            CardRewardRarityWeights weights = ReadWeights(hero, heroTemplateId);
            JArray rewardTokens = hero["reward_card_template_ids"] as JArray
                ?? throw new InvalidOperationException(
                    $"Hero {heroTemplateId} has no reward_card_template_ids array.");
            if (rewardTokens.Count < RunCardRewardGenerator.CandidateCount)
            {
                throw new InvalidOperationException(
                    $"Hero {heroTemplateId} reward pool requires at least three templates.");
            }

            var candidates = new List<CardRewardCandidate>(rewardTokens.Count);
            var localTemplateIds = new HashSet<int>();
            foreach (JToken rewardToken in rewardTokens)
            {
                if (rewardToken.Type != JTokenType.Integer)
                {
                    throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward pool contains a non-integer template id.");
                }

                int cardTemplateId = (int)rewardToken;
                if (cardTemplateId <= 0 || !localTemplateIds.Add(cardTemplateId))
                {
                    throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward pool contains invalid or duplicate card {cardTemplateId}.");
                }
                if (globallyOwnedTemplateIds.TryGetValue(cardTemplateId, out int ownerHeroTemplateId))
                {
                    throw new InvalidOperationException(
                        $"Reward card {cardTemplateId} is owned by both Hero " +
                        $"{ownerHeroTemplateId} and Hero {heroTemplateId}.");
                }
                if (dynamicTemplates.Contains(cardTemplateId))
                {
                    throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward pool contains dynamic temporary card {cardTemplateId}.");
                }

                JObject card = cards[cardTemplateId.ToString(CultureInfo.InvariantCulture)] as JObject
                    ?? throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward pool references missing card {cardTemplateId}.");
                int implementationStatus = ReadRequiredInt(
                    card,
                    "implementation_status",
                    cardTemplateId.ToString(CultureInfo.InvariantCulture));
                if (implementationStatus != (int)cfg.battle.CardImplementationStatus.Implemented)
                {
                    throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward pool contains non-Implemented card {cardTemplateId}.");
                }

                int rarityValue = ReadRequiredInt(
                    card,
                    "rarity",
                    cardTemplateId.ToString(CultureInfo.InvariantCulture));
                var rarity = (cfg.battle.CardRarity)rarityValue;
                if (!CardRewardCandidate.IsRewardableRarity(rarity))
                {
                    throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward pool contains non-rewardable rarity " +
                        $"{rarityValue} card {cardTemplateId}.");
                }

                globallyOwnedTemplateIds.Add(cardTemplateId, heroTemplateId);
                candidates.Add(new CardRewardCandidate(cardTemplateId, rarity));
            }

            try
            {
                _ = new HeroCardRewardPool(heroTemplateId, weights, candidates);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"Hero {heroTemplateId} reward pool is invalid: {exception.Message}",
                    exception);
            }
        }
    }

    /// <summary>读取并验证一名 Hero 的三档非负整数权重。</summary>
    private static CardRewardRarityWeights ReadWeights(JObject hero, int heroTemplateId)
    {
        int commonWeight = ReadRequiredInt(
            hero,
            "reward_common_weight",
            heroTemplateId.ToString(CultureInfo.InvariantCulture));
        int uncommonWeight = ReadRequiredInt(
            hero,
            "reward_uncommon_weight",
            heroTemplateId.ToString(CultureInfo.InvariantCulture));
        int rareWeight = ReadRequiredInt(
            hero,
            "reward_rare_weight",
            heroTemplateId.ToString(CultureInfo.InvariantCulture));

        try
        {
            return new CardRewardRarityWeights(commonWeight, uncommonWeight, rareWeight);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Hero {heroTemplateId} reward weights are invalid: {exception.Message}",
                exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                $"Hero {heroTemplateId} reward weights overflow Int32.",
                exception);
        }
    }

    /// <summary>读取必填整数，不接受缺失、字符串或小数替代。</summary>
    private static int ReadRequiredInt(JObject record, string fieldName, string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.Integer)
            throw new InvalidOperationException($"Record {recordName} has no integer {fieldName}.");

        return (int)token;
    }

    /// <summary>确认 JSON 顶层键与记录 ID 完全一致。</summary>
    private static void ValidateRecordKey(string propertyName, int recordId, string tableName)
    {
        string expected = recordId.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(propertyName, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{tableName} record key '{propertyName}' does not match id {recordId}.");
        }
    }

    /// <summary>读取一份必需生成表，并拒绝缺失、坏 JSON 或空表。</summary>
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
