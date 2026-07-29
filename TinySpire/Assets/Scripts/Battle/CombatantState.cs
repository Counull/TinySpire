using System;

namespace TinySpire.Battle
{
    public readonly struct CombatantId : IEquatable<CombatantId>
    {
        public int Value { get; }

        internal CombatantId(int value)
        {
            Value = value;
        }

        public bool Equals(CombatantId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatantId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(CombatantId left, CombatantId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CombatantId left, CombatantId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 玩家与敌人共享的、可被卡牌效果和目标系统读取的运行时战斗状态。
    /// </summary>
    public abstract class CombatantState
    {
        public CombatantId Id { get; }
        public int TemplateId { get; }
        public int MaxHealth { get; }
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;

        protected CombatantState(CombatantId id, int templateId, int maxHealth)
        {
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));

            Id = id;
            TemplateId = templateId;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }

        internal bool ApplyDamage(int damage)
        {
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));

            if (!IsAlive)
                return false;

            CurrentHealth = Math.Max(0, CurrentHealth - damage);
            return true;
        }
    }

    public sealed class PlayerCombatantState : CombatantState
    {
        internal PlayerCombatantState(CombatantId id, int templateId, int maxHealth)
            : base(id, templateId, maxHealth)
        {
        }
    }

    public sealed class EnemyCombatantState : CombatantState
    {
        internal EnemyCombatantState(CombatantId id, int templateId, int maxHealth)
            : base(id, templateId, maxHealth)
        {
        }
    }
}
