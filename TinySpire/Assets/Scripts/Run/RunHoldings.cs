using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TinySpire.Run
{
    /// <summary>一个遗物在所属 Run 内的稳定正整数身份。</summary>
    public readonly struct RunRelicInstanceId : IEquatable<RunRelicInstanceId>
    {
        /// <summary>所属 Run 内的正整数序号。</summary>
        public int Sequence { get; }

        /// <summary>创建一个经过正数约束的遗物实例身份。</summary>
        public RunRelicInstanceId(int sequence)
        {
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));

            Sequence = sequence;
        }

        /// <summary>比较两个遗物实例身份是否相同。</summary>
        public bool Equals(RunRelicInstanceId other)
        {
            return Sequence == other.Sequence;
        }

        /// <summary>比较当前身份与任意对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is RunRelicInstanceId other && Equals(other);
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

        /// <summary>判断两个遗物实例身份是否相同。</summary>
        public static bool operator ==(RunRelicInstanceId left, RunRelicInstanceId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个遗物实例身份是否不同。</summary>
        public static bool operator !=(RunRelicInstanceId left, RunRelicInstanceId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>一瓶药水在所属 Run 内的稳定正整数身份。</summary>
    public readonly struct RunPotionInstanceId : IEquatable<RunPotionInstanceId>
    {
        /// <summary>所属 Run 内的正整数序号。</summary>
        public int Sequence { get; }

        /// <summary>创建一个经过正数约束的药水实例身份。</summary>
        public RunPotionInstanceId(int sequence)
        {
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));

            Sequence = sequence;
        }

        /// <summary>比较两个药水实例身份是否相同。</summary>
        public bool Equals(RunPotionInstanceId other)
        {
            return Sequence == other.Sequence;
        }

        /// <summary>比较当前身份与任意对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is RunPotionInstanceId other && Equals(other);
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

        /// <summary>判断两个药水实例身份是否相同。</summary>
        public static bool operator ==(RunPotionInstanceId left, RunPotionInstanceId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个药水实例身份是否不同。</summary>
        public static bool operator !=(RunPotionInstanceId left, RunPotionInstanceId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Run 持有物中的一个不可变遗物实例事实。</summary>
    public sealed class RunRelic
    {
        /// <summary>所属 Run 内的稳定遗物实例身份。</summary>
        public RunRelicInstanceId InstanceId { get; }

        /// <summary>该实例引用的正整数静态遗物模板。</summary>
        public int TemplateId { get; }

        /// <summary>创建仅保存稳定身份与模板引用的遗物事实。</summary>
        public RunRelic(RunRelicInstanceId instanceId, int templateId)
        {
            if (instanceId.Sequence <= 0)
                throw new ArgumentException("Run relic instance id cannot be empty.", nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));

            InstanceId = instanceId;
            TemplateId = templateId;
        }
    }

    /// <summary>Run 持有物中的一个不可变药水实例事实。</summary>
    public sealed class RunPotion
    {
        /// <summary>所属 Run 内的稳定药水实例身份。</summary>
        public RunPotionInstanceId InstanceId { get; }

        /// <summary>该实例引用的正整数静态药水模板。</summary>
        public int TemplateId { get; }

        /// <summary>创建仅保存稳定身份与模板引用的药水事实。</summary>
        public RunPotion(RunPotionInstanceId instanceId, int templateId)
        {
            if (instanceId.Sequence <= 0)
                throw new ArgumentException("Run potion instance id cannot be empty.", nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));

            InstanceId = instanceId;
            TemplateId = templateId;
        }
    }

    /// <summary>跨战斗保存遗物、药水与金币的不可变 Run 持有物快照。</summary>
    public sealed class RunHoldings
    {
        private readonly ReadOnlyCollection<RunRelic> _relics;
        private readonly ReadOnlyCollection<RunPotion> _potions;

        /// <summary>按获得顺序冻结的全部遗物实例。</summary>
        public IReadOnlyList<RunRelic> Relics => _relics;

        /// <summary>按槽位顺序冻结的全部药水实例。</summary>
        public IReadOnlyList<RunPotion> Potions => _potions;

        /// <summary>当前非负金币数量。</summary>
        public int Gold { get; }

        /// <summary>防御性复制并冻结完整的 Run 持有物事实。</summary>
        public RunHoldings(
            IEnumerable<RunRelic> relics,
            IEnumerable<RunPotion> potions,
            int gold)
        {
            if (relics == null)
                throw new ArgumentNullException(nameof(relics));
            if (potions == null)
                throw new ArgumentNullException(nameof(potions));
            if (gold < 0)
                throw new ArgumentOutOfRangeException(nameof(gold));

            RunRelic[] frozenRelics = relics.ToArray();
            RunPotion[] frozenPotions = potions.ToArray();
            if (frozenRelics.Any(relic => relic == null))
                throw new ArgumentException("RunHoldings cannot contain null relics.", nameof(relics));
            if (frozenPotions.Any(potion => potion == null))
                throw new ArgumentException("RunHoldings cannot contain null potions.", nameof(potions));
            if (frozenPotions.Length > 3)
                throw new ArgumentException("RunHoldings cannot contain more than three potions.", nameof(potions));

            var relicInstanceIds = new HashSet<RunRelicInstanceId>();
            var relicTemplateIds = new HashSet<int>();
            foreach (RunRelic relic in frozenRelics)
            {
                if (!relicInstanceIds.Add(relic.InstanceId))
                    throw new ArgumentException("RunHoldings cannot contain duplicate relic instance ids.", nameof(relics));
                if (!relicTemplateIds.Add(relic.TemplateId))
                    throw new ArgumentException("RunHoldings cannot contain duplicate relic templates.", nameof(relics));
            }

            var potionInstanceIds = new HashSet<RunPotionInstanceId>();
            foreach (RunPotion potion in frozenPotions)
            {
                if (!potionInstanceIds.Add(potion.InstanceId))
                    throw new ArgumentException("RunHoldings cannot contain duplicate potion instance ids.", nameof(potions));
            }

            _relics = Array.AsReadOnly(frozenRelics);
            _potions = Array.AsReadOnly(frozenPotions);
            Gold = gold;
        }

        /// <summary>创建一个没有遗物与药水的新 Run 持有物快照。</summary>
        public static RunHoldings Empty(int initialGold = 100)
        {
            return new RunHoldings(
                Array.Empty<RunRelic>(),
                Array.Empty<RunPotion>(),
                initialGold);
        }

        /// <summary>在末尾增加一个模板唯一的遗物，并按现有最大序号加一分配身份。</summary>
        public RunHoldings AddRelic(int templateId)
        {
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (_relics.Any(relic => relic.TemplateId == templateId))
                throw new InvalidOperationException("The requested relic template is already owned.");

            int nextSequence = _relics.Count == 0
                ? 1
                : checked(_relics.Max(relic => relic.InstanceId.Sequence) + 1);
            var relics = new List<RunRelic>(_relics)
            {
                new RunRelic(new RunRelicInstanceId(nextSequence), templateId),
            };
            return new RunHoldings(relics, _potions, Gold);
        }

        /// <summary>在三槽容量内追加药水，并按现有最大序号加一分配独立身份。</summary>
        public RunHoldings AddPotion(int templateId)
        {
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (_potions.Count >= 3)
                throw new InvalidOperationException("All Run potion slots are occupied.");

            int nextSequence = _potions.Count == 0
                ? 1
                : checked(_potions.Max(potion => potion.InstanceId.Sequence) + 1);
            var potions = new List<RunPotion>(_potions)
            {
                new RunPotion(new RunPotionInstanceId(nextSequence), templateId),
            };
            return new RunHoldings(_relics, potions, Gold);
        }

        /// <summary>按稳定身份移除一个已持有药水，并保持其余槽位顺序不变。</summary>
        public RunHoldings RemovePotion(RunPotionInstanceId instanceId)
        {
            if (instanceId.Sequence <= 0)
                throw new ArgumentException("Run potion instance id cannot be empty.", nameof(instanceId));

            int potionIndex = -1;
            for (int index = 0; index < _potions.Count; index++)
            {
                if (_potions[index].InstanceId != instanceId)
                    continue;

                potionIndex = index;
                break;
            }
            if (potionIndex < 0)
                throw new InvalidOperationException("The requested Run potion instance does not exist.");

            var potions = new List<RunPotion>(_potions);
            potions.RemoveAt(potionIndex);
            return new RunHoldings(_relics, potions, Gold);
        }

        /// <summary>以 checked 加法获得正数金币，并返回完整的新持有物快照。</summary>
        public RunHoldings GainGold(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            int nextGold = checked(Gold + amount);
            return new RunHoldings(_relics, _potions, nextGold);
        }

        /// <summary>在余额足够时以 checked 减法花费正数金币，并返回新快照。</summary>
        public RunHoldings SpendGold(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Gold)
                throw new InvalidOperationException("The requested gold spend exceeds the current balance.");

            int nextGold = checked(Gold - amount);
            return new RunHoldings(_relics, _potions, nextGold);
        }
    }
}
