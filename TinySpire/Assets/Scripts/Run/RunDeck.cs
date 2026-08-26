using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TinySpire.Run
{
    /// <summary>一张 RunCard 在所属 Run 内的稳定序号身份。</summary>
    public readonly struct RunCardInstanceId : IEquatable<RunCardInstanceId>
    {
        /// <summary>所属 Run 内从一开始递增的正整数序号。</summary>
        public int Sequence { get; }

        /// <summary>创建一个经过正数约束的 Run 卡牌实例身份。</summary>
        public RunCardInstanceId(int sequence)
        {
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));

            Sequence = sequence;
        }

        /// <summary>比较两个 Run 卡牌实例身份是否相同。</summary>
        public bool Equals(RunCardInstanceId other)
        {
            return Sequence == other.Sequence;
        }

        /// <summary>比较当前身份与任意对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is RunCardInstanceId other && Equals(other);
        }

        /// <summary>返回稳定序号的哈希值。</summary>
        public override int GetHashCode()
        {
            return Sequence;
        }

        /// <summary>返回便于日志与测试诊断的序号文本。</summary>
        public override string ToString()
        {
            return Sequence.ToString();
        }

        /// <summary>判断两个 Run 卡牌实例身份是否相同。</summary>
        public static bool operator ==(RunCardInstanceId left, RunCardInstanceId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个 Run 卡牌实例身份是否不同。</summary>
        public static bool operator !=(RunCardInstanceId left, RunCardInstanceId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>RunDeck 中一张不可变的实例级卡牌事实。</summary>
    public sealed class RunCard
    {
        /// <summary>所属 Run 内的稳定实例身份。</summary>
        public RunCardInstanceId InstanceId { get; }

        /// <summary>该实例引用的静态卡牌模板。</summary>
        public int TemplateId { get; }

        /// <summary>该实例当前的非负升级等级。</summary>
        public int UpgradeLevel { get; }

        /// <summary>创建仅保存身份、模板与升级等级的最小 RunCard 事实。</summary>
        public RunCard(RunCardInstanceId instanceId, int templateId, int upgradeLevel)
        {
            if (instanceId.Sequence <= 0)
                throw new ArgumentException("Run card instance id cannot be empty.", nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (upgradeLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));

            InstanceId = instanceId;
            TemplateId = templateId;
            UpgradeLevel = upgradeLevel;
        }
    }

    /// <summary>跨战斗保存顺序、实例身份与升级事实的不可变 Run 牌组。</summary>
    public sealed class RunDeck
    {
        private readonly ReadOnlyCollection<RunCard> _cards;

        /// <summary>按 Run 业务顺序冻结的全部卡牌实例。</summary>
        public IReadOnlyList<RunCard> Cards => _cards;

        /// <summary>防御性复制并验证有序 RunCard 集合。</summary>
        public RunDeck(IEnumerable<RunCard> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));

            RunCard[] frozenCards = cards.ToArray();
            var instanceIds = new HashSet<RunCardInstanceId>();
            foreach (RunCard card in frozenCards)
            {
                if (card == null)
                    throw new ArgumentException("RunDeck cannot contain null cards.", nameof(cards));
                if (!instanceIds.Add(card.InstanceId))
                    throw new ArgumentException("RunDeck cannot contain duplicate instance ids.", nameof(cards));
            }

            _cards = Array.AsReadOnly(frozenCards);
        }

        /// <summary>按初始牌组模板顺序一次展开等级零实例，并从一分配稳定序号。</summary>
        public static RunDeck CreateInitial(IEnumerable<int> cardTemplateIds)
        {
            if (cardTemplateIds == null)
                throw new ArgumentNullException(nameof(cardTemplateIds));

            var cards = new List<RunCard>();
            foreach (int templateId in cardTemplateIds)
            {
                int sequence = checked(cards.Count + 1);
                cards.Add(new RunCard(
                    new RunCardInstanceId(sequence),
                    templateId,
                    upgradeLevel: 0));
            }

            return new RunDeck(cards);
        }

        /// <summary>在牌组末尾追加一个等级零的新实例，并以现有最大序号加一分配稳定身份。</summary>
        public RunDeck AppendNewInstance(int cardTemplateId)
        {
            if (cardTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardTemplateId));

            int nextSequence = _cards.Count == 0
                ? 1
                : checked(_cards.Max(card => card.InstanceId.Sequence) + 1);
            var cards = new List<RunCard>(_cards)
            {
                new RunCard(
                    new RunCardInstanceId(nextSequence),
                    cardTemplateId,
                    upgradeLevel: 0),
            };
            return new RunDeck(cards);
        }

        /// <summary>把指定实例恰好提升一级，并由调用方的配置规则确认该下一等级合法。</summary>
        public RunDeck UpgradeInstanceOneLevel(
            RunCardInstanceId instanceId,
            Func<int, int, bool> isUpgradeLevelValid)
        {
            if (instanceId.Sequence <= 0)
                throw new ArgumentException("Run card instance id cannot be empty.", nameof(instanceId));
            if (isUpgradeLevelValid == null)
                throw new ArgumentNullException(nameof(isUpgradeLevelValid));

            int cardIndex = -1;
            for (int index = 0; index < _cards.Count; index++)
            {
                if (_cards[index].InstanceId != instanceId)
                    continue;

                cardIndex = index;
                break;
            }
            if (cardIndex < 0)
                throw new InvalidOperationException("The requested Run card instance does not exist.");

            RunCard current = _cards[cardIndex];
            int nextLevel = checked(current.UpgradeLevel + 1);
            if (!isUpgradeLevelValid(current.TemplateId, nextLevel))
            {
                throw new InvalidOperationException(
                    "The requested Run card instance has no legal next upgrade level.");
            }

            var cards = new List<RunCard>(_cards);
            cards[cardIndex] = new RunCard(current.InstanceId, current.TemplateId, nextLevel);
            return new RunDeck(cards);
        }
    }
}
