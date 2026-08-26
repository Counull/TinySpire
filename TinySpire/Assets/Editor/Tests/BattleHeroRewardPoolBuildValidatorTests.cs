using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>验证 G4-B Hero 普通战斗奖励池的构建期内容门禁。</summary>
public sealed class BattleHeroRewardPoolBuildValidatorTests
{
    /// <summary>当前两名生产 Hero 必须各自拥有合法且彼此独立的显式奖励池。</summary>
    [Test]
    public void ValidateCurrentProject_ProductionRewardPoolsPass()
    {
        Assert.DoesNotThrow(BattleHeroRewardPoolBuildValidator.ValidateCurrentProject);
    }

    /// <summary>当前两名可选 Hero 任一缺失都必须视为构建期内容错误。</summary>
    [Test]
    public void Validate_MissingSelectableHero_Throws()
    {
        CreateValidTables(out JObject heroes, out JObject decks, out JObject cards);
        heroes.Remove("1002");

        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));
    }

    /// <summary>重复模板必须在生成奖励前被构建门禁拒绝。</summary>
    [Test]
    public void Validate_DuplicateRewardTemplate_Throws()
    {
        CreateValidTables(out JObject heroes, out JObject decks, out JObject cards);
        heroes["1001"]["reward_card_template_ids"] = new JArray(3101, 3101, 3103);

        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));
    }

    /// <summary>CatalogOnly、Basic 与 Ancient 都不得进入普通战斗奖励池。</summary>
    [TestCase(cfg.battle.CardImplementationStatus.CatalogOnly, cfg.battle.CardRarity.Common)]
    [TestCase(cfg.battle.CardImplementationStatus.Implemented, cfg.battle.CardRarity.Basic)]
    [TestCase(cfg.battle.CardImplementationStatus.Implemented, cfg.battle.CardRarity.Ancient)]
    public void Validate_NonRewardableCard_Throws(
        cfg.battle.CardImplementationStatus implementationStatus,
        cfg.battle.CardRarity rarity)
    {
        CreateValidTables(out JObject heroes, out JObject decks, out JObject cards);
        cards["3102"]["implementation_status"] = (int)implementationStatus;
        cards["3102"]["rarity"] = (int)rarity;

        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));
    }

    /// <summary>合法非 Basic 初始牌组卡仍可奖励，但战斗内动态临时卡必须被排除。</summary>
    [Test]
    public void Validate_RewardableInitialDeckCardPassesButDynamicTemporaryCardThrows()
    {
        CreateValidTables(out JObject heroes, out JObject decks, out JObject cards);
        decks["1001"]["card_template_ids"] = new JArray(3001, 3101);
        Assert.DoesNotThrow(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));

        CreateValidTables(out heroes, out decks, out cards);
        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, new[] { 3102 }));
    }

    /// <summary>缺卡、少于三张或正权重覆盖不足三张都不得成为生产池。</summary>
    [Test]
    public void Validate_IncompleteRewardPool_Throws()
    {
        CreateValidTables(out JObject heroes, out JObject decks, out JObject cards);
        cards.Remove("3102");
        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));

        CreateValidTables(out heroes, out decks, out cards);
        heroes["1001"]["reward_card_template_ids"] = new JArray(3101, 3102);
        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));

        CreateValidTables(out heroes, out decks, out cards);
        heroes["1001"]["reward_rare_weight"] = 0;
        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));
    }

    /// <summary>总权重为零与两个 Hero 共享同模板都必须失败。</summary>
    [Test]
    public void Validate_ZeroWeightsOrCrossHeroOwnership_Throws()
    {
        CreateValidTables(out JObject heroes, out JObject decks, out JObject cards);
        heroes["1001"]["reward_common_weight"] = 0;
        heroes["1001"]["reward_uncommon_weight"] = 0;
        heroes["1001"]["reward_rare_weight"] = 0;
        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));

        CreateValidTables(out heroes, out decks, out cards);
        heroes["1002"] = new JObject
        {
            ["id"] = 1002,
            ["initial_deck_id"] = 1002,
            ["reward_card_template_ids"] = new JArray(3101, 3202, 3203),
            ["reward_common_weight"] = 60,
            ["reward_uncommon_weight"] = 37,
            ["reward_rare_weight"] = 3,
        };
        decks["1002"] = new JObject
        {
            ["id"] = 1002,
            ["card_template_ids"] = new JArray(3002),
        };
        cards["3202"] = CreateCard(3202, cfg.battle.CardRarity.Uncommon);
        cards["3203"] = CreateCard(3203, cfg.battle.CardRarity.Rare);
        Assert.Throws<InvalidOperationException>(() =>
            BattleHeroRewardPoolBuildValidator.Validate(heroes, decks, cards, Array.Empty<int>()));
    }

    /// <summary>建立两名可选 Hero、各三个合法奖励模板与不相交初始牌组的最小夹具。</summary>
    private static void CreateValidTables(
        out JObject heroes,
        out JObject decks,
        out JObject cards)
    {
        heroes = new JObject
        {
            ["1001"] = new JObject
            {
                ["id"] = 1001,
                ["initial_deck_id"] = 1001,
                ["reward_card_template_ids"] = new JArray(3101, 3102, 3103),
                ["reward_common_weight"] = 60,
                ["reward_uncommon_weight"] = 37,
                ["reward_rare_weight"] = 3,
            },
            ["1002"] = new JObject
            {
                ["id"] = 1002,
                ["initial_deck_id"] = 1002,
                ["reward_card_template_ids"] = new JArray(3201, 3202, 3203),
                ["reward_common_weight"] = 60,
                ["reward_uncommon_weight"] = 37,
                ["reward_rare_weight"] = 3,
            },
        };
        decks = new JObject
        {
            ["1001"] = new JObject
            {
                ["id"] = 1001,
                ["card_template_ids"] = new JArray(3001),
            },
            ["1002"] = new JObject
            {
                ["id"] = 1002,
                ["card_template_ids"] = new JArray(3002),
            },
        };
        cards = new JObject
        {
            ["3101"] = CreateCard(3101, cfg.battle.CardRarity.Common),
            ["3102"] = CreateCard(3102, cfg.battle.CardRarity.Uncommon),
            ["3103"] = CreateCard(3103, cfg.battle.CardRarity.Rare),
            ["3201"] = CreateCard(3201, cfg.battle.CardRarity.Common),
            ["3202"] = CreateCard(3202, cfg.battle.CardRarity.Uncommon),
            ["3203"] = CreateCard(3203, cfg.battle.CardRarity.Rare),
        };
    }

    /// <summary>建立一个已实现且具有指定奖励稀有度的卡牌 JSON。</summary>
    private static JObject CreateCard(int cardTemplateId, cfg.battle.CardRarity rarity)
    {
        return new JObject
        {
            ["id"] = cardTemplateId,
            ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
            ["rarity"] = (int)rarity,
        };
    }
}
