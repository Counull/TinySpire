using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>
    /// 已格式化、仅供界面显示的卡牌文本，不属于战斗事实。
    /// </summary>
    public readonly struct CardPresentationText
    {
        /// <summary>本地化后的卡牌名称。</summary>
        public string Name { get; }

        /// <summary>代入当前动态参数后的卡牌说明。</summary>
        public string Description { get; }

        /// <summary>
        /// 创建一份卡牌展示文本。
        /// </summary>
        public CardPresentationText(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// 按需根据本地化资源与当前参与者事实格式化卡牌文本。
    /// 格式化后的文字和动态数值不是战斗事实，不能被持久化为状态。
    /// </summary>
    public sealed class CardTextFormatter
    {
        private const string StrengthKeywordKey = "battle.keyword.strength.name";
        private const string VulnerableKeywordKey = "battle.keyword.vulnerable.name";

        private readonly ConfigService _configs;
        private readonly LocalizationService _localization;

        /// <summary>
        /// 创建卡牌文本格式化器；配置服务与本地化服务须先完成初始化。
        /// </summary>
        public CardTextFormatter(ConfigService configs, LocalizationService localization)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>
        /// 从卡牌实例、静态模板和来源参与者的当前事实生成展示文本。
        /// </summary>
        public CardPresentationText Format(CardInstanceData card, CombatantData source)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
            if (_configs.Tables == null)
                throw new InvalidOperationException("ConfigService must be initialized before formatting card text.");

            cfg.battle.Card template = _configs.Tables.TbCard.GetOrDefault(card.TemplateId)
                ?? throw new InvalidOperationException($"Card template {card.TemplateId} does not exist.");

            Dictionary<string, object> arguments = BuildArguments(template, source);
            arguments.Add("keywordStrength", _localization.GetString(StrengthKeywordKey));
            arguments.Add("keywordVulnerable", _localization.GetString(VulnerableKeywordKey));

            return new CardPresentationText(
                _localization.GetString(template.NameI18nKey),
                _localization.GetString(template.DescriptionI18nKey, arguments));
        }

        /// <summary>从静态效果绑定和当前来源事实派生本地化模板参数。</summary>
        private Dictionary<string, object> BuildArguments(cfg.battle.Card card, CombatantData source)
        {
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (cfg.battle.CardEffectBinding binding in card.EffectBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.ArgumentKey))
                    throw new InvalidOperationException($"Card {card.Id} contains an empty effect argument key.");
                if (arguments.ContainsKey(binding.ArgumentKey))
                    throw new InvalidOperationException(
                        $"Card {card.Id} contains duplicate effect argument '{binding.ArgumentKey}'.");

                cfg.battle.CardEffect effect = _configs.Tables.TbCardEffect.GetOrDefault(binding.EffectId)
                    ?? throw new InvalidOperationException(
                        $"Card {card.Id} references missing effect {binding.EffectId}.");
                int displayValue = BattleCardEffectTypeMapping.UsesLiteralDisplayValue(
                    effect.EffectType)
                    ? effect.Value
                    : BattleEffectValueCalculator.Calculate(effect, source);
                arguments.Add(binding.ArgumentKey, displayValue);
            }

            return arguments;
        }
    }
}
