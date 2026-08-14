using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// 战斗卡牌目录构建隔离的契约测试。
/// </summary>
public sealed class BattleCardCatalogBuildValidatorTests
{
    /// <summary>验证当前生成表、程序引用与真实牌面素材满足完整构建目录契约。</summary>
    [Test]
    public void ValidateCurrentProject_ProductionCatalogPasses()
    {
        Assert.DoesNotThrow(BattleCardCatalogBuildValidator.ValidateCurrentProject);
    }

    /// <summary>验证可玩牌组引用目录占位卡时会在构建期报告精确的牌组与卡牌身份。</summary>
    [Test]
    public void Validate_CatalogOnlyCardReferencedByDeck_Throws()
    {
        const int deckId = 7101;
        const int cardId = 9101;
        const int effectId = 9901;
        var decks = new JObject
        {
            [deckId.ToString()] = new JObject
            {
                ["id"] = deckId,
                ["card_template_ids"] = new JArray(cardId)
            }
        };
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.CatalogOnly,
                ["effect_bindings"] = new JArray(
                    new JObject
                    {
                        ["argument_key"] = "damage",
                        ["effect_id"] = effectId
                    }),
                ["illustration_key"] = "art_placeholder",
                ["is_innate"] = false
            }
        };
        var effects = new JObject
        {
            [effectId.ToString()] = new JObject
            {
                ["id"] = effectId,
                ["effect_type"] = 1,
                ["attribute"] = 0,
                ["value"] = 6
            }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(decks, cards, effects));

        Assert.That(
            failure.Message,
            Is.EqualTo($"Deck {deckId} references CatalogOnly card {cardId}."));
    }

    /// <summary>验证标记为已实现的卡牌缺少效果程序时会在构建期失败。</summary>
    [Test]
    public void Validate_ImplementedCardWithoutEffectBindings_Throws()
    {
        const int cardId = 9102;
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                ["effect_bindings"] = new JArray(),
                ["illustration_key"] = "card_art_strike",
                ["is_innate"] = false
            }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                new JObject()));

        Assert.That(
            failure.Message,
            Is.EqualTo($"Implemented card {cardId} has no effect_bindings."));
    }

    /// <summary>验证已实现卡牌的程序引用缺失 Effect 时会报告卡牌、参数与 Effect 身份。</summary>
    [Test]
    public void Validate_ImplementedCardWithMissingEffectReference_Throws()
    {
        const int cardId = 9103;
        const int effectId = 9999;
        const string argumentKey = "damage";
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                ["effect_bindings"] = new JArray(
                    new JObject
                    {
                        ["argument_key"] = argumentKey,
                        ["effect_id"] = effectId
                    }),
                ["illustration_key"] = "card_art_strike",
                ["is_innate"] = false
            }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                new JObject()));

        Assert.That(
            failure.Message,
            Is.EqualTo(
                $"Implemented card {cardId} binding '{argumentKey}' references missing effect {effectId}."));
    }

    /// <summary>验证已实现卡牌的效果参数键为空时不会留到运行时文本格式化才失败。</summary>
    [Test]
    public void Validate_ImplementedCardWithEmptyArgumentKey_Throws()
    {
        const int cardId = 9104;
        const int effectId = 9904;
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                ["effect_bindings"] = new JArray(
                    new JObject
                    {
                        ["argument_key"] = string.Empty,
                        ["effect_id"] = effectId
                    }),
                ["illustration_key"] = "card_art_strike",
                ["is_innate"] = false
            }
        };
        var effects = new JObject
        {
            [effectId.ToString()] = new JObject { ["id"] = effectId }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                effects));

        Assert.That(
            failure.Message,
            Is.EqualTo($"Implemented card {cardId} has an empty effect argument key."));
    }

    /// <summary>验证同一卡牌的效果参数键重复时会在构建期拒绝歧义程序。</summary>
    [Test]
    public void Validate_ImplementedCardWithDuplicateArgumentKey_Throws()
    {
        const int cardId = 9105;
        const int firstEffectId = 9905;
        const int secondEffectId = 9906;
        const string argumentKey = "damage";
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                ["effect_bindings"] = new JArray(
                    new JObject
                    {
                        ["argument_key"] = argumentKey,
                        ["effect_id"] = firstEffectId
                    },
                    new JObject
                    {
                        ["argument_key"] = argumentKey,
                        ["effect_id"] = secondEffectId
                    }),
                ["illustration_key"] = "card_art_strike",
                ["is_innate"] = false
            }
        };
        var effects = new JObject
        {
            [firstEffectId.ToString()] = new JObject { ["id"] = firstEffectId },
            [secondEffectId.ToString()] = new JObject { ["id"] = secondEffectId }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                effects));

        Assert.That(
            failure.Message,
            Is.EqualTo(
                $"Implemented card {cardId} contains duplicate effect argument '{argumentKey}'."));
    }

    /// <summary>验证未知实现状态不会被静默当成不可玩目录卡放过。</summary>
    [Test]
    public void Validate_CardWithUnknownImplementationStatus_Throws()
    {
        const int cardId = 9106;
        const int unknownStatus = 99;
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = unknownStatus,
                ["effect_bindings"] = new JArray(),
                ["illustration_key"] = "card_art_strike",
                ["is_innate"] = false
            }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                new JObject()));

        Assert.That(
            failure.Message,
            Is.EqualTo($"Card {cardId} has unknown implementation_status {unknownStatus}."));
    }

    /// <summary>验证已实现卡牌不能把完整工程路径伪装成牌面短键。</summary>
    [Test]
    public void Validate_ImplementedCardWithInvalidIllustrationKey_Throws()
    {
        const int cardId = 9107;
        const int effectId = 9907;
        const string invalidKey = "Assets/Arts/Runtime/Card/Illustrations/card_art_strike.png";
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                ["effect_bindings"] = new JArray(
                    new JObject
                    {
                        ["argument_key"] = "damage",
                        ["effect_id"] = effectId
                    }),
                ["illustration_key"] = invalidKey,
                ["is_innate"] = false
            }
        };
        var effects = new JObject
        {
            [effectId.ToString()] = new JObject { ["id"] = effectId }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                effects));

        Assert.That(
            failure.Message,
            Is.EqualTo($"Card {cardId} has invalid illustration_key '{invalidKey}'."));
    }

    /// <summary>验证目录占位卡只能声明专用占位牌面短键，不能借用正式牌面伪装完成。</summary>
    [Test]
    public void Validate_CatalogOnlyCardWithoutPlaceholderIllustration_Throws()
    {
        const int cardId = 9108;
        const string actualKey = "card_art_strike";
        const string placeholderKey = "art_placeholder";
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.CatalogOnly,
                ["effect_bindings"] = new JArray(),
                ["illustration_key"] = actualKey,
                ["is_innate"] = false
            }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                new JObject()));

        Assert.That(
            failure.Message,
            Is.EqualTo(
                $"CatalogOnly card {cardId} must use placeholder illustration_key '{placeholderKey}', but found '{actualKey}'."));
    }

    /// <summary>验证 Effect JSON 顶层键与记录 ID 漂移时会在引用解析前报告精确表记录。</summary>
    [Test]
    public void Validate_EffectRecordKeyDoesNotMatchId_Throws()
    {
        const int cardId = 9109;
        const int effectId = 9909;
        const int mismatchedPropertyKey = 9910;
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                ["effect_bindings"] = new JArray(
                    new JObject
                    {
                        ["argument_key"] = "damage",
                        ["effect_id"] = effectId
                    }),
                ["illustration_key"] = "card_art_strike",
                ["is_innate"] = false
            }
        };
        var effects = new JObject
        {
            [mismatchedPropertyKey.ToString()] = new JObject
            {
                ["id"] = effectId
            }
        };

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                effects));

        Assert.That(
            failure.Message,
            Is.EqualTo(
                $"battle_tbcardeffect record key '{mismatchedPropertyKey}' does not match id {effectId}."));
    }

    /// <summary>验证每张目录卡都必须显式声明布尔类型的固有标记，不允许缺失或数字代替。</summary>
    [Test]
    public void Validate_CardWithoutBooleanInnateFlag_Throws()
    {
        const int cardId = 9110;
        var card = new JObject
        {
            ["id"] = cardId,
            ["implementation_status"] =
                (int)cfg.battle.CardImplementationStatus.Implemented,
            ["program_id"] = 1,
            ["effect_bindings"] = new JArray(),
            ["illustration_key"] = "card_art_strike",
        };
        var cards = new JObject { [cardId.ToString()] = card };

        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                new JObject()));
        Assert.That(
            missing.Message,
            Is.EqualTo($"Card {cardId} has no boolean is_innate."));

        card["is_innate"] = 1;
        InvalidOperationException wrongType = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                new JObject(),
                cards,
                new JObject()));
        Assert.That(
            wrongType.Message,
            Is.EqualTo($"Card {cardId} has no boolean is_innate."));
    }

    /// <summary>验证牌组固有牌按实例数量计数：重复模板十张可通过，第十一张在构建期被拒绝。</summary>
    [Test]
    public void Validate_DeckCountsRepeatedInnateTemplatesAgainstOpeningHandLimit()
    {
        const int deckId = 7110;
        const int cardId = 9111;
        var deckCards = new JArray();
        for (int index = 0; index < 10; index++)
            deckCards.Add(cardId);
        var decks = new JObject
        {
            [deckId.ToString()] = new JObject
            {
                ["id"] = deckId,
                ["card_template_ids"] = deckCards,
            },
        };
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["implementation_status"] =
                    (int)cfg.battle.CardImplementationStatus.Implemented,
                ["program_id"] = 1,
                ["effect_bindings"] = new JArray(),
                ["illustration_key"] = "card_art_strike",
                ["is_innate"] = true,
            },
        };

        Assert.DoesNotThrow(() => BattleCardCatalogBuildValidator.Validate(
            decks,
            cards,
            new JObject()));

        deckCards.Add(cardId);
        InvalidOperationException overflow = Assert.Throws<InvalidOperationException>(
            () => BattleCardCatalogBuildValidator.Validate(
                decks,
                cards,
                new JObject()));
        Assert.That(
            overflow.Message,
            Is.EqualTo(
                $"Deck {deckId} contains 11 innate cards, exceeding opening hand limit 10."));
    }
}
