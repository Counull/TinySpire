using System;
using System.Collections.Generic;
using cfg;

namespace TinySpire.Run
{
    /// <summary>把 Luban Hero-owned 奖励配置严格映射为纯领域卡池。</summary>
    internal static class TablesHeroCardRewardPoolCatalog
    {
        /// <summary>按 Hero 模板读取显式数组，不过滤、不排序也不推断卡牌归属。</summary>
        internal static HeroCardRewardPool Create(Tables tables, int heroTemplateId)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));

            cfg.battle.Hero hero = tables.TbHero.GetOrDefault(heroTemplateId)
                ?? throw new InvalidOperationException($"Hero template {heroTemplateId} does not exist.");
            var candidates = new List<CardRewardCandidate>(hero.RewardCardTemplateIds.Length);
            foreach (int cardTemplateId in hero.RewardCardTemplateIds)
            {
                cfg.battle.Card card = tables.TbCard.GetOrDefault(cardTemplateId)
                    ?? throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward card {cardTemplateId} does not exist.");
                if (card.ImplementationStatus != cfg.battle.CardImplementationStatus.Implemented)
                {
                    throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward card {cardTemplateId} is not Implemented.");
                }
                if (!CardRewardCandidate.IsRewardableRarity(card.Rarity))
                {
                    throw new InvalidOperationException(
                        $"Hero {heroTemplateId} reward card {cardTemplateId} has rarity {card.Rarity}.");
                }

                candidates.Add(new CardRewardCandidate(cardTemplateId, card.Rarity));
            }

            var weights = new CardRewardRarityWeights(
                hero.RewardCommonWeight,
                hero.RewardUncommonWeight,
                hero.RewardRareWeight);
            return new HeroCardRewardPool(heroTemplateId, weights, candidates);
        }
    }
}
