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
    /// 生命、力量、格挡、易伤与中毒是唯一可变事实，对外仅以只读 R3 属性暴露。
    /// </summary>
    public abstract class CombatantData : IDisposable
    {
        private readonly ReactiveProperty<int> _health;
        private readonly ReactiveProperty<int> _strength;
        private readonly ReactiveProperty<int> _block;
        private readonly ReactiveProperty<int> _vulnerable;
        private readonly ReactiveProperty<int> _poison;

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

        /// <summary>当前格挡这一事实的只读响应式视图。</summary>
        public ReadOnlyReactiveProperty<int> Block { get; }

        /// <summary>当前易伤这一事实的只读响应式视图。</summary>
        public ReadOnlyReactiveProperty<int> Vulnerable { get; }

        /// <summary>当前中毒层数这一事实的只读响应式视图。</summary>
        public ReadOnlyReactiveProperty<int> Poison { get; }

        /// <summary>当前生命值的同步读取入口。</summary>
        public int CurrentHealth => Health.CurrentValue;

        /// <summary>当前力量值的同步读取入口。</summary>
        public int CurrentStrength => Strength.CurrentValue;

        /// <summary>当前格挡值的同步读取入口。</summary>
        public int CurrentBlock => Block.CurrentValue;

        /// <summary>当前易伤值的同步读取入口。</summary>
        public int CurrentVulnerable => Vulnerable.CurrentValue;

        /// <summary>当前中毒层数的同步读取入口。</summary>
        public int CurrentPoison => Poison.CurrentValue;

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
            _block = new ReactiveProperty<int>(0);
            _vulnerable = new ReactiveProperty<int>(0);
            _poison = new ReactiveProperty<int>(0);
            Health = _health.ToReadOnlyReactiveProperty();
            Strength = _strength.ToReadOnlyReactiveProperty();
            Block = _block.ToReadOnlyReactiveProperty();
            Vulnerable = _vulnerable.ToReadOnlyReactiveProperty();
            Poison = _poison.ToReadOnlyReactiveProperty();
        }

        /// <summary>仅由内部 Effect 状态入口一次写入已计算的格挡与生命结果。</summary>
        internal void ApplyDamageOutcome(BattleDamageFormulaOutcome outcome)
        {
            if (outcome.BlockBefore != CurrentBlock || outcome.HealthBefore != CurrentHealth)
            {
                throw new InvalidOperationException("伤害推演与当前参与者事实不一致。");
            }

            _block.Value = outcome.BlockAfter;
            _health.Value = outcome.HealthAfter;
        }

        /// <summary>仅由内部 Effect 状态入口一次写入已经冻结且受上限约束的治疗结果。</summary>
        internal void ApplyHealthRestorationOutcome(BattleHealthRestorationOutcome outcome)
        {
            if (outcome.HealthBefore != CurrentHealth ||
                outcome.HealthAfter < outcome.HealthBefore ||
                outcome.HealthAfter > MaxHealth)
            {
                throw new InvalidOperationException("治疗推演与当前参与者事实或生命上限不一致。");
            }

            _health.Value = outcome.HealthAfter;
        }

        /// <summary>仅由内部 Effect 状态入口累加非负格挡值。</summary>
        internal void ApplyBlockGain(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            _block.Value = checked(CurrentBlock + amount);
        }

        /// <summary>仅由内部 Effect 状态入口应用有符号力量变化。</summary>
        internal void ApplyStrengthChange(int amount)
        {
            _strength.Value = checked(CurrentStrength + amount);
        }

        /// <summary>仅由内部 Effect 状态入口累加非负易伤值。</summary>
        internal void ApplyVulnerableGain(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            _vulnerable.Value = checked(CurrentVulnerable + amount);
        }

        /// <summary>仅由通用中毒模块把已冻结的非负层数写回唯一权威事实。</summary>
        internal void ApplyPoisonValue(int expectedBefore, int valueAfter)
        {
            if (expectedBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedBefore));
            if (valueAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(valueAfter));
            if (CurrentPoison != expectedBefore)
                throw new InvalidOperationException("中毒推演与当前参与者事实不一致。");

            if (valueAfter != expectedBefore)
                _poison.Value = valueAfter;
        }

        /// <summary>仅由通用中毒模块在一次权威入口内校验并写入回合开始的生命与中毒终局。</summary>
        internal void ApplyPoisonTickOutcome(BattlePoisonTickOutcome outcome)
        {
            if (outcome.HealthBefore != CurrentHealth ||
                outcome.BlockBefore != CurrentBlock ||
                outcome.BlockAfter != CurrentBlock ||
                outcome.PoisonBefore != CurrentPoison)
            {
                throw new InvalidOperationException("中毒触发推演与当前参与者事实不一致。");
            }

            if (!outcome.HasWrite)
                return;

            _poison.Value = outcome.PoisonAfter;
            _health.Value = outcome.HealthAfter;
        }

        /// <summary>仅由内部状态时机 module 提交已经完整预构建的 Block 与 Vulnerable 结果。</summary>
        internal void ApplyStatusTimingValues(int blockAfter, int vulnerableAfter)
        {
            if (!IsAlive)
                throw new InvalidOperationException("死亡参与者不能提交状态时机写入。");
            if (blockAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(blockAfter));
            if (vulnerableAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(vulnerableAfter));

            if (blockAfter != CurrentBlock)
                _block.Value = blockAfter;
            if (vulnerableAfter != CurrentVulnerable)
                _vulnerable.Value = vulnerableAfter;
        }

        /// <summary>
        /// 释放此参与者持有的响应式资源；由所属战斗聚合统一调用。
        /// </summary>
        public void Dispose()
        {
            Health.Dispose();
            Strength.Dispose();
            Block.Dispose();
            Vulnerable.Dispose();
            Poison.Dispose();
            _health.Dispose();
            _strength.Dispose();
            _block.Dispose();
            _vulnerable.Dispose();
            _poison.Dispose();
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
