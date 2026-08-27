using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cfg.battle;
using TinySpire.Core;

namespace TinySpire.Run
{
    /// <summary>一张已通过内容门禁的普通战斗奖励模板候选。</summary>
    public readonly struct CardRewardCandidate
    {
        /// <summary>奖励卡牌模板标识。</summary>
        public int TemplateId { get; }

        /// <summary>参与配表权重抽取的合法稀有度。</summary>
        public CardRarity Rarity { get; }

        /// <summary>冻结一个正数模板与 Common/Uncommon/Rare 稀有度。</summary>
        public CardRewardCandidate(int templateId, CardRarity rarity)
        {
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (!IsRewardableRarity(rarity))
                throw new ArgumentOutOfRangeException(nameof(rarity));

            TemplateId = templateId;
            Rarity = rarity;
        }

        /// <summary>判断稀有度是否属于普通战斗奖励的三档白名单。</summary>
        internal static bool IsRewardableRarity(CardRarity rarity)
        {
            return rarity == CardRarity.Common ||
                   rarity == CardRarity.Uncommon ||
                   rarity == CardRarity.Rare;
        }
    }

    /// <summary>普通战斗奖励三档稀有度的无状态整数权重。</summary>
    public readonly struct CardRewardRarityWeights
    {
        /// <summary>Common 档权重。</summary>
        public int CommonWeight { get; }

        /// <summary>Uncommon 档权重。</summary>
        public int UncommonWeight { get; }

        /// <summary>Rare 档权重。</summary>
        public int RareWeight { get; }

        /// <summary>冻结非负权重，并拒绝总权重为零。</summary>
        public CardRewardRarityWeights(int commonWeight, int uncommonWeight, int rareWeight)
        {
            if (commonWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(commonWeight));
            if (uncommonWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(uncommonWeight));
            if (rareWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(rareWeight));
            if (checked(commonWeight + uncommonWeight + rareWeight) == 0)
                throw new ArgumentException("Card reward rarity weights cannot all be zero.");

            CommonWeight = commonWeight;
            UncommonWeight = uncommonWeight;
            RareWeight = rareWeight;
        }

        /// <summary>读取合法奖励稀有度对应的配表权重。</summary>
        public int GetWeight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:
                    return CommonWeight;
                case CardRarity.Uncommon:
                    return UncommonWeight;
                case CardRarity.Rare:
                    return RareWeight;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }
    }

    /// <summary>一名 Hero 独占的普通战斗奖励模板池与稀有度权重。</summary>
    public sealed class HeroCardRewardPool
    {
        private readonly ReadOnlyCollection<CardRewardCandidate> _candidates;

        /// <summary>该卡池唯一所属的 Hero 模板。</summary>
        public int HeroTemplateId { get; }

        /// <summary>本 Hero 独立配置的稀有度权重。</summary>
        public CardRewardRarityWeights RarityWeights { get; }

        /// <summary>按配置顺序冻结的合法奖励模板。</summary>
        public IReadOnlyList<CardRewardCandidate> Candidates => _candidates;

        /// <summary>冻结并验证至少三个不同模板的单 Hero 奖励池。</summary>
        public HeroCardRewardPool(
            int heroTemplateId,
            CardRewardRarityWeights rarityWeights,
            IReadOnlyList<CardRewardCandidate> candidates)
        {
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count < RunCardRewardGenerator.CandidateCount)
            {
                throw new ArgumentException(
                    "A Hero card reward pool requires at least three templates.",
                    nameof(candidates));
            }

            var seenTemplateIds = new HashSet<int>();
            var frozenCandidates = new CardRewardCandidate[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                CardRewardCandidate candidate = candidates[index];
                if (!seenTemplateIds.Add(candidate.TemplateId))
                {
                    throw new ArgumentException(
                        "A Hero card reward pool cannot contain duplicate templates.",
                        nameof(candidates));
                }

                frozenCandidates[index] = candidate;
            }
            if (frozenCandidates.Count(candidate => rarityWeights.GetWeight(candidate.Rarity) > 0) <
                RunCardRewardGenerator.CandidateCount)
            {
                throw new ArgumentException(
                    "A Hero card reward pool requires three templates in positive-weight rarities.",
                    nameof(candidates));
            }

            HeroTemplateId = heroTemplateId;
            RarityWeights = rarityWeights;
            _candidates = Array.AsReadOnly(frozenCandidates);
        }
    }

    /// <summary>由某一 Run battle attempt 唯一决定的稳定奖励身份。</summary>
    public readonly struct RunCardRewardId : IEquatable<RunCardRewardId>
    {
        /// <summary>产生该奖励的唯一战斗身份。</summary>
        public RunBattleId BattleId { get; }

        /// <summary>把一个有效战斗身份冻结为奖励身份。</summary>
        public RunCardRewardId(RunBattleId battleId)
        {
            if (battleId.RunId.Value == Guid.Empty ||
                battleId.AttemptSequence <= 0 ||
                string.IsNullOrEmpty(battleId.NodeId.Value))
            {
                throw new ArgumentException("Card reward battle id cannot be empty.", nameof(battleId));
            }

            BattleId = battleId;
        }

        /// <summary>比较两个奖励身份是否指向同一战斗 attempt。</summary>
        public bool Equals(RunCardRewardId other)
        {
            return BattleId == other.BattleId;
        }

        /// <summary>比较此奖励身份与另一个对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is RunCardRewardId other && Equals(other);
        }

        /// <summary>返回稳定哈希值。</summary>
        public override int GetHashCode()
        {
            return BattleId.GetHashCode();
        }

        /// <summary>输出可持久化并核对的稳定奖励身份文本。</summary>
        public override string ToString()
        {
            return $"{BattleId.RunId.Value:N}:{BattleId.AttemptSequence}:{BattleId.NodeId.Value}";
        }

        /// <summary>判断两个奖励身份是否相同。</summary>
        public static bool operator ==(RunCardRewardId left, RunCardRewardId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个奖励身份是否不同。</summary>
        public static bool operator !=(RunCardRewardId left, RunCardRewardId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>S0 首次普通战斗奖励使用的固定附着掉落模板身份。</summary>
    public static class RunCardRewardAttachedLootTemplateIds
    {
        public const int FirstOrdinaryBattleRelic = 8001;
        public const int FirstOrdinaryBattlePotion = 9001;
    }

    /// <summary>普通战斗奖励可附着的零或一个遗物、零或一个药水模板事实。</summary>
    public sealed class RunCardRewardAttachedLoot
    {
        /// <summary>显式表示没有任何附着掉落的不可变值。</summary>
        public static RunCardRewardAttachedLoot Empty { get; } =
            new RunCardRewardAttachedLoot(relicTemplateId: null, potionTemplateId: null);

        /// <summary>可空遗物模板；非空时必须为正数。</summary>
        public int? RelicTemplateId { get; }

        /// <summary>可空药水模板；非空时必须为正数。</summary>
        public int? PotionTemplateId { get; }

        /// <summary>冻结两个可空且非空时为正数的附着模板。</summary>
        public RunCardRewardAttachedLoot(
            int? relicTemplateId,
            int? potionTemplateId)
        {
            if (relicTemplateId.HasValue && relicTemplateId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(relicTemplateId));
            if (potionTemplateId.HasValue && potionTemplateId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(potionTemplateId));

            RelicTemplateId = relicTemplateId;
            PotionTemplateId = potionTemplateId;
        }
    }

    /// <summary>跨刷新、场景重建与冷启动保持不变的普通战斗奖励事实。</summary>
    public sealed class PendingCardReward
    {
        private readonly ReadOnlyCollection<int> _candidateTemplateIds;

        /// <summary>产生该奖励的稳定身份。</summary>
        public RunCardRewardId Id { get; }

        /// <summary>按奖励页展示顺序冻结的三个不同模板。</summary>
        public IReadOnlyList<int> CandidateTemplateIds => _candidateTemplateIds;

        /// <summary>永不为空的不可变附着遗物与药水模板事实。</summary>
        public RunCardRewardAttachedLoot AttachedLoot { get; }

        /// <summary>冻结三个候选与永不为空的附着掉落；省略时显式采用 Empty。</summary>
        public PendingCardReward(
            RunCardRewardId id,
            IReadOnlyList<int> candidateTemplateIds,
            RunCardRewardAttachedLoot attachedLoot = null)
        {
            if (id.BattleId.RunId.Value == Guid.Empty)
                throw new ArgumentException("Pending card reward id cannot be empty.", nameof(id));
            if (candidateTemplateIds == null)
                throw new ArgumentNullException(nameof(candidateTemplateIds));
            if (candidateTemplateIds.Count != RunCardRewardGenerator.CandidateCount)
            {
                throw new ArgumentException(
                    "A pending card reward must contain exactly three templates.",
                    nameof(candidateTemplateIds));
            }
            if (candidateTemplateIds.Any(templateId => templateId <= 0) ||
                candidateTemplateIds.Distinct().Count() != candidateTemplateIds.Count)
            {
                throw new ArgumentException(
                    "Pending card reward templates must be positive and distinct.",
                    nameof(candidateTemplateIds));
            }

            Id = id;
            _candidateTemplateIds = Array.AsReadOnly(candidateTemplateIds.ToArray());
            RunCardRewardAttachedLoot source = attachedLoot ?? RunCardRewardAttachedLoot.Empty;
            AttachedLoot = new RunCardRewardAttachedLoot(
                source.RelicTemplateId,
                source.PotionTemplateId);
        }
    }

    /// <summary>以独立 seed 无状态生成三张不同模板的普通战斗奖励。</summary>
    public static class RunCardRewardGenerator
    {
        public const int CandidateCount = 3;

        internal static readonly IReadOnlyList<CardRarity> OrderedRarities = Array.AsReadOnly(new[]
        {
            CardRarity.Common,
            CardRarity.Uncommon,
            CardRarity.Rare,
        });

        /// <summary>只消费本次 Reward domain 随机流并返回不可变 Pending。</summary>
        public static PendingCardReward Generate(
            RunCardRewardId rewardId,
            HeroCardRewardPool pool,
            uint seed)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (seed == 0)
                throw new ArgumentOutOfRangeException(nameof(seed));

            var random = new GameRandom(seed);
            var remaining = pool.Candidates.ToList();
            var selectedTemplateIds = new int[CandidateCount];
            for (int slot = 0; slot < CandidateCount; slot++)
            {
                CardRarity rarity = SelectRarity(pool.RarityWeights, remaining, random);
                List<CardRewardCandidate> bucket = remaining
                    .Where(candidate => candidate.Rarity == rarity)
                    .ToList();
                CardRewardCandidate selected = bucket[random.NextInt(bucket.Count)];
                selectedTemplateIds[slot] = selected.TemplateId;
                remaining.RemoveAll(candidate => candidate.TemplateId == selected.TemplateId);
            }

            return new PendingCardReward(rewardId, selectedTemplateIds);
        }

        /// <summary>按仍有候选的稀有度档重新归一化本槽整数权重。</summary>
        private static CardRarity SelectRarity(
            CardRewardRarityWeights weights,
            IReadOnlyList<CardRewardCandidate> remaining,
            GameRandom random)
        {
            int totalWeight = 0;
            foreach (CardRarity rarity in OrderedRarities)
            {
                if (remaining.Any(candidate => candidate.Rarity == rarity))
                    totalWeight = checked(totalWeight + weights.GetWeight(rarity));
            }
            if (totalWeight <= 0)
                throw new InvalidOperationException("No weighted card reward candidate remains.");

            int roll = random.NextInt(totalWeight);
            foreach (CardRarity rarity in OrderedRarities)
            {
                if (!remaining.Any(candidate => candidate.Rarity == rarity))
                    continue;

                int weight = weights.GetWeight(rarity);
                if (roll < weight)
                    return rarity;
                roll -= weight;
            }

            throw new InvalidOperationException("Card reward rarity selection exceeded total weight.");
        }
    }
}
