using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Reflection;

/// <summary>验证 G4-D 卡牌升级轨道与逐级配置的构建期内容门禁。</summary>
public sealed class BattleCardUpgradeBuildValidatorTests
{
    /// <summary>无升级轨道在没有有限级配置且无限规则为空时必须通过。</summary>
    [Test]
    public void Validate_NoneTrackWithoutLevelRows_Passes()
    {
        JObject cards = CreateCards(
            CreateCard(
                9001,
                cfg.battle.CardUpgradeTrackKind.None,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0));

        Assert.DoesNotThrow(() =>
            BattleCardUpgradeBuildValidator.Validate(cards, new JArray()));
    }

    /// <summary>无升级轨道不得携带有限级配置或无限规则事实。</summary>
    [Test]
    public void Validate_NoneTrackWithLevelRowsOrInfiniteRule_Throws()
    {
        JObject cards = CreateCards(
            CreateCard(
                9001,
                cfg.battle.CardUpgradeTrackKind.None,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0));

        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(
                cards,
                new JArray(CreateLevel(9001, nextLevel: 1))));

        cards = CreateCards(
            CreateCard(
                9001,
                cfg.battle.CardUpgradeTrackKind.None,
                cfg.battle.CardUpgradeRuleKind.DamageValue,
                infiniteDelta: 1));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(cards, new JArray()));
    }

    /// <summary>有限连续级与无逐级行的类型化无限增量可以同时存在于同一目录。</summary>
    [Test]
    public void Validate_FiniteAndInfiniteTracksWithValidFacts_Pass()
    {
        JObject cards = CreateCards(
            CreateCard(
                9001,
                cfg.battle.CardUpgradeTrackKind.None,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0),
            CreateCard(
                9002,
                cfg.battle.CardUpgradeTrackKind.Finite,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0),
            CreateCard(
                9003,
                cfg.battle.CardUpgradeTrackKind.Infinite,
                cfg.battle.CardUpgradeRuleKind.DamageValue,
                infiniteDelta: 4));
        var levels = new JArray(
            CreateLevel(9002, nextLevel: 1, ruleValue: 9),
            CreateLevel(
                9002,
                nextLevel: 2,
                ruleKind: cfg.battle.CardUpgradeRuleKind.None,
                ruleValue: 0));

        Assert.DoesNotThrow(() =>
            BattleCardUpgradeBuildValidator.Validate(cards, levels));
    }

    /// <summary>有限轨道必须至少有一级，且不得混入无限规则尾巴。</summary>
    [Test]
    public void Validate_FiniteTrackWithoutLevelOrWithInfiniteTail_Throws()
    {
        JObject cards = CreateCards(
            CreateCard(
                9002,
                cfg.battle.CardUpgradeTrackKind.Finite,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(cards, new JArray()));

        cards = CreateCards(
            CreateCard(
                9002,
                cfg.battle.CardUpgradeTrackKind.Finite,
                cfg.battle.CardUpgradeRuleKind.DamageValue,
                infiniteDelta: 2));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(
                cards,
                new JArray(CreateLevel(9002, nextLevel: 1))));
    }

    /// <summary>无限轨道只允许 DamageValue 正增量，且不得同时定义有限级。</summary>
    [Test]
    public void Validate_InfiniteTrackWithWrongRuleDeltaOrFiniteRows_Throws()
    {
        JObject cards = CreateCards(
            CreateCard(
                9003,
                cfg.battle.CardUpgradeTrackKind.Infinite,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 3));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(cards, new JArray()));

        cards = CreateCards(
            CreateCard(
                9003,
                cfg.battle.CardUpgradeTrackKind.Infinite,
                cfg.battle.CardUpgradeRuleKind.DamageValue,
                infiniteDelta: 0));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(cards, new JArray()));

        cards = CreateCards(
            CreateCard(
                9003,
                cfg.battle.CardUpgradeTrackKind.Infinite,
                cfg.battle.CardUpgradeRuleKind.DamageValue,
                infiniteDelta: 3));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(
                cards,
                new JArray(CreateLevel(9003, nextLevel: 1))));
    }

    /// <summary>有限级的描述、费用、去向与类型化数值必须分别满足发布约束。</summary>
    [Test]
    public void Validate_FiniteLevelWithInvalidFields_Throws()
    {
        JObject cards = CreateCards(
            CreateCard(
                9002,
                cfg.battle.CardUpgradeTrackKind.Finite,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0));

        AssertInvalidLevel(cards, CreateLevel(9002, 1, descriptionKey: " "));
        AssertInvalidLevel(cards, CreateLevel(9002, 1, cost: -1));
        AssertInvalidLevel(cards, CreateLevel(9002, 1, destinationValue: 99));
        AssertInvalidLevel(
            cards,
            CreateLevel(
                9002,
                1,
                ruleKind: cfg.battle.CardUpgradeRuleKind.None,
                ruleValue: 1));
        AssertInvalidLevel(cards, CreateLevel(9002, 1, ruleValue: 0));
    }

    /// <summary>孤儿行、重复卡牌级组合与从一级开始的连续性缺口都必须失败。</summary>
    [Test]
    public void Validate_OrphanDuplicateOrGappedFiniteLevels_Throws()
    {
        JObject cards = CreateCards(
            CreateCard(
                9002,
                cfg.battle.CardUpgradeTrackKind.Finite,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0));

        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(
                cards,
                new JArray(CreateLevel(9999, nextLevel: 1))));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(
                cards,
                new JArray(
                    CreateLevel(9002, nextLevel: 1),
                    CreateLevel(9002, nextLevel: 1))));
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(
                cards,
                new JArray(CreateLevel(9002, nextLevel: 2))));
    }

    /// <summary>当前生成内容必须保留两张有限卡和两张固定增量无限卡。</summary>
    [Test]
    public void ValidateCurrentProject_RequiredProductionTracksPass()
    {
        Assert.DoesNotThrow(BattleCardUpgradeBuildValidator.ValidateCurrentProject);
    }

    /// <summary>四张冻结生产卡任一轨道或增量漂移都必须被专属门禁拒绝。</summary>
    [TestCase(3002)]
    [TestCase(3123)]
    [TestCase(3201)]
    [TestCase(3207)]
    public void ValidateProductionTracks_WhenRequiredFactChanges_Throws(int changedCardId)
    {
        CreateProductionTables(out JObject cards, out JArray levels);
        JObject changedCard = (JObject)cards[changedCardId.ToString()];
        if (changedCardId == 3123 || changedCardId == 3201)
        {
            changedCard["infinite_upgrade_value_per_level"] =
                changedCard.Value<int>("infinite_upgrade_value_per_level") + 1;
        }
        else
        {
            changedCard["upgrade_track_kind"] = (int)cfg.battle.CardUpgradeTrackKind.None;
            for (int index = levels.Count - 1; index >= 0; index--)
            {
                if (levels[index].Value<int>("card_id") == changedCardId)
                    levels.RemoveAt(index);
            }
        }

        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.ValidateProductionTracks(cards, levels));
    }

    /// <summary>冻结生产轨道还必须锁定双 Hero 的 Basic 初始牌与 Implemented 非 Basic 奖励牌职责。</summary>
    [Test]
    public void ValidateProductionRoles_RequiredHeroCardRolesPassAndDriftFails()
    {
        CreateProductionRoleTables(
            out JObject cards,
            out JObject heroes,
            out JObject decks);
        MethodInfo validator = typeof(BattleCardUpgradeBuildValidator).GetMethod(
            "ValidateProductionRoles",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(validator, Is.Not.Null);
        Assert.DoesNotThrow(() => validator.Invoke(null, new object[] { cards, heroes, decks }));

        ((JObject)cards["3123"])["implementation_status"] =
            (int)cfg.battle.CardImplementationStatus.CatalogOnly;
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
            validator.Invoke(null, new object[] { cards, heroes, decks }));
        Assert.That(error.InnerException, Is.TypeOf<System.InvalidOperationException>());
    }

    /// <summary>建立以卡牌 ID 为顶层键的最小生成卡表。</summary>
    private static JObject CreateCards(params JObject[] cards)
    {
        var table = new JObject();
        foreach (JObject card in cards)
            table.Add(card.Value<int>("id").ToString(), card);

        return table;
    }

    /// <summary>断言单条有限级坏配置会被公共校验入口拒绝。</summary>
    private static void AssertInvalidLevel(JObject cards, JObject level)
    {
        Assert.Throws<System.InvalidOperationException>(() =>
            BattleCardUpgradeBuildValidator.Validate(cards, new JArray(level)));
    }

    /// <summary>建立只包含升级轨道门禁字段的最小卡牌记录。</summary>
    private static JObject CreateCard(
        int cardId,
        cfg.battle.CardUpgradeTrackKind trackKind,
        cfg.battle.CardUpgradeRuleKind infiniteRuleKind,
        int infiniteDelta)
    {
        return new JObject
        {
            ["id"] = cardId,
            ["upgrade_track_kind"] = (int)trackKind,
            ["infinite_upgrade_rule_kind"] = (int)infiniteRuleKind,
            ["infinite_upgrade_value_per_level"] = infiniteDelta,
        };
    }

    /// <summary>建立包含全部有限级字段的最小升级配置行。</summary>
    private static JObject CreateLevel(
        int cardId,
        int nextLevel,
        string descriptionKey = "battle.card.test.upgrade_description",
        int cost = 1,
        int destinationValue = (int)cfg.battle.CardPlayDestination.DiscardPile,
        cfg.battle.CardUpgradeRuleKind ruleKind = cfg.battle.CardUpgradeRuleKind.DamageValue,
        int ruleValue = 9)
    {
        return new JObject
        {
            ["card_id"] = cardId,
            ["next_upgrade_level"] = nextLevel,
            ["description_i18n_key"] = descriptionKey,
            ["cost"] = cost,
            ["play_destination"] = destinationValue,
            ["rule_kind"] = (int)ruleKind,
            ["rule_value"] = ruleValue,
        };
    }

    /// <summary>建立四张冻结生产卡及两条一级有限升级配置。</summary>
    private static void CreateProductionTables(out JObject cards, out JArray levels)
    {
        cards = CreateCards(
            CreateCard(
                3002,
                cfg.battle.CardUpgradeTrackKind.Finite,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0),
            CreateCard(
                3123,
                cfg.battle.CardUpgradeTrackKind.Infinite,
                cfg.battle.CardUpgradeRuleKind.DamageValue,
                infiniteDelta: 10),
            CreateCard(
                3201,
                cfg.battle.CardUpgradeTrackKind.Infinite,
                cfg.battle.CardUpgradeRuleKind.DamageValue,
                infiniteDelta: 3),
            CreateCard(
                3207,
                cfg.battle.CardUpgradeTrackKind.Finite,
                cfg.battle.CardUpgradeRuleKind.None,
                infiniteDelta: 0));
        levels = new JArray(
            CreateLevel(3002, 1, ruleValue: 9),
            CreateLevel(
                3207,
                1,
                cost: 0,
                destinationValue: (int)cfg.battle.CardPlayDestination.Power,
                ruleKind: cfg.battle.CardUpgradeRuleKind.None,
                ruleValue: 0));
    }

    /// <summary>建立双 Hero 四张冻结升级卡的最小初始牌组与奖励职责事实。</summary>
    private static void CreateProductionRoleTables(
        out JObject cards,
        out JObject heroes,
        out JObject decks)
    {
        CreateProductionTables(out cards, out _);
        foreach (string cardId in new[] { "3002", "3123", "3201", "3207" })
        {
            ((JObject)cards[cardId])["implementation_status"] =
                (int)cfg.battle.CardImplementationStatus.Implemented;
        }
        ((JObject)cards["3002"])["rarity"] = (int)cfg.battle.CardRarity.Basic;
        ((JObject)cards["3123"])["rarity"] = (int)cfg.battle.CardRarity.Uncommon;
        ((JObject)cards["3201"])["rarity"] = (int)cfg.battle.CardRarity.Basic;
        ((JObject)cards["3207"])["rarity"] = (int)cfg.battle.CardRarity.Uncommon;

        heroes = new JObject
        {
            ["1001"] = new JObject
            {
                ["id"] = 1001,
                ["initial_deck_id"] = 1001,
                ["reward_card_template_ids"] = new JArray(3123),
            },
            ["1002"] = new JObject
            {
                ["id"] = 1002,
                ["initial_deck_id"] = 1002,
                ["reward_card_template_ids"] = new JArray(3207),
            },
        };
        decks = new JObject
        {
            ["1001"] = new JObject
            {
                ["id"] = 1001,
                ["card_template_ids"] = new JArray(3002),
            },
            ["1002"] = new JObject
            {
                ["id"] = 1002,
                ["card_template_ids"] = new JArray(3201),
            },
        };
    }
}
