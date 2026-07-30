using System;
using R3;

namespace TinySpire.Battle
{
    /// <summary>
    /// 单场战斗内参与者的唯一标识。
    /// </summary>
    public readonly struct CombatantId : IEquatable<CombatantId>
    {
        /// <summary>标识的整数值。</summary>
        public int Value { get; }

        internal CombatantId(int value)
        {
            Value = value;
        }

        /// <summary>比较两个参与者标识是否相同。</summary>
        public bool Equals(CombatantId other)
        {
            return Value == other.Value;
        }

        /// <summary>比较此标识与另一个对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is CombatantId other && Equals(other);
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

        /// <summary>判断两个参与者标识是否相同。</summary>
        public static bool operator ==(CombatantId left, CombatantId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个参与者标识是否不同。</summary>
        public static bool operator !=(CombatantId left, CombatantId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 单场战斗中实例化的参与者运行时数据。
    /// 生命与力量是唯一可变事实，对外仅以只读 R3 属性暴露。
    /// </summary>
    public abstract class CombatantData : IDisposable
    {
        private readonly ReactiveProperty<int> _health;
        private readonly ReactiveProperty<int> _strength;

        /// <summary>本场战斗内的参与者标识。</summary>
        public CombatantId Id { get; }

        /// <summary>对应静态 Hero 或 Enemy 配置表的模板标识。</summary>
        public int TemplateId { get; }

        /// <summary>入场时确定且战斗期间不变的生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>当前生命这一事实的只读响应式视图。</summary>
        public ReadOnlyReactiveProperty<int> Health { get; }

        /// <summary>当前力量这一事实的只读响应式视图。</summary>
        public ReadOnlyReactiveProperty<int> Strength { get; }

        /// <summary>当前生命值的同步读取入口。</summary>
        public int CurrentHealth => Health.CurrentValue;

        /// <summary>根据当前生命值派生的存活结果，不单独保存状态。</summary>
        public bool IsAlive => CurrentHealth > 0;

        /// <summary>建立参与者的初始事实与对外只读响应式视图。</summary>
        protected CombatantData(CombatantId id, int templateId, int maxHealth, int strength)
        {
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));

            Id = id;
            TemplateId = templateId;
            MaxHealth = maxHealth;
            _health = new ReactiveProperty<int>(maxHealth);
            _strength = new ReactiveProperty<int>(strength);
            Health = _health.ToReadOnlyReactiveProperty();
            Strength = _strength.ToReadOnlyReactiveProperty();
        }

        /// <summary>在参与者仍存活时写入扣减后的生命事实。</summary>
        internal bool ApplyDamage(int damage)
        {
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (!IsAlive)
                return false;

            _health.Value = Math.Max(0, CurrentHealth - damage);
            return true;
        }

        /// <summary>
        /// 释放此参与者持有的响应式资源；由所属战斗聚合统一调用。
        /// </summary>
        public void Dispose()
        {
            Health.Dispose();
            Strength.Dispose();
            _health.Dispose();
            _strength.Dispose();
        }
    }

    /// <summary>
    /// 玩家在一场战斗中的参与者数据。
    /// </summary>
    public sealed class PlayerCombatantData : CombatantData
    {
        /// <summary>由参与者聚合创建玩家实例。</summary>
        internal PlayerCombatantData(CombatantId id, int templateId, int maxHealth, int strength)
            : base(id, templateId, maxHealth, strength)
        {
        }
    }

    /// <summary>
    /// 敌人在一场战斗中的参与者数据。
    /// </summary>
    public sealed class EnemyCombatantData : CombatantData
    {
        /// <summary>由参与者聚合创建敌人实例。</summary>
        internal EnemyCombatantData(CombatantId id, int templateId, int maxHealth, int strength)
            : base(id, templateId, maxHealth, strength)
        {
        }
    }
}
