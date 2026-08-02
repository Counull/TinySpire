using System;

namespace TinySpire.Battle
{
    /// <summary>
    /// 运行时 Effect 配置的强类型标识，只允许配置适配层从裸整数创建。
    /// </summary>
    public readonly struct BattleEffectId : IEquatable<BattleEffectId>
    {
        /// <summary>标识的整数值。</summary>
        public int Value { get; }

        /// <summary>仅允许战斗运行时配置适配代码创建 Effect 标识。</summary>
        internal BattleEffectId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        /// <summary>比较两个 Effect 标识是否相同。</summary>
        public bool Equals(BattleEffectId other)
        {
            return Value == other.Value;
        }

        /// <summary>比较此标识与另一个对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is BattleEffectId other && Equals(other);
        }

        /// <summary>返回可用于字典键的稳定哈希值。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>返回标识的文本形式。</summary>
        public override string ToString()
        {
            return Value.ToString();
        }

        /// <summary>判断两个 Effect 标识是否相同。</summary>
        public static bool operator ==(BattleEffectId left, BattleEffectId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个 Effect 标识是否不同。</summary>
        public static bool operator !=(BattleEffectId left, BattleEffectId right)
        {
            return !left.Equals(right);
        }
    }
}
