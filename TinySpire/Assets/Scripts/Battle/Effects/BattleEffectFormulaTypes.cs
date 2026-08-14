using System;

namespace TinySpire.Battle
{
    /// <summary>共享公式 module 支持的 M7 领域操作。</summary>
    public enum BattleEffectOperationType
    {
        ModifyAttribute,
        DealDamage,
        GainBlock,
        ApplyVulnerable,
        Heal,
        RetainBlock,
        RegisterBlockGainRandomEnemyDamage,
    }

    /// <summary>声明一条 Effect 在命令起点应从哪里冻结其公式基础值。</summary>
    internal enum BattleEffectMagnitudeSource
    {
        ConfiguredValue,
        SourceBlock,
    }

    /// <summary>集中解析静态配置值与来源格挡值，避免执行器和卡面各自解释动态数值。</summary>
    internal static class BattleEffectMagnitudeResolver
    {
        /// <summary>从同一份来源快照解析本条 Effect 的公式基础值，不读取或写入权威状态。</summary>
        internal static int Resolve(
            BattleEffectMagnitudeSource magnitudeSource,
            int configuredValue,
            int sourceBlock)
        {
            if (sourceBlock < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceBlock));

            switch (magnitudeSource)
            {
                case BattleEffectMagnitudeSource.ConfiguredValue:
                    return configuredValue;
                case BattleEffectMagnitudeSource.SourceBlock:
                    return sourceBlock;
                default:
                    throw new ArgumentOutOfRangeException(nameof(magnitudeSource));
            }
        }
    }

    /// <summary>公式读取的不可变目标标量快照，不持有任何权威状态对象。</summary>
    public readonly struct BattleEffectTargetSnapshot
    {
        /// <summary>目标当前生命。</summary>
        public int Health { get; }

        /// <summary>目标当前格挡。</summary>
        public int Block { get; }

        /// <summary>目标当前易伤值。</summary>
        public int Vulnerable { get; }

        /// <summary>创建一次仅用于纯计算的目标标量快照。</summary>
        public BattleEffectTargetSnapshot(int health, int block, int vulnerable)
        {
            if (health < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(health));
            }

            if (block < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(block));
            }

            if (vulnerable < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vulnerable));
            }

            Health = health;
            Block = block;
            Vulnerable = vulnerable;
        }
    }

    /// <summary>一次 Effect 公式计算所需的全部不可变输入。</summary>
    public readonly struct BattleEffectFormulaContext
    {
        /// <summary>要计算的领域操作。</summary>
        public BattleEffectOperationType OperationType { get; }

        /// <summary>配置提供的原始数值。</summary>
        public int ConfiguredValue { get; }

        /// <summary>来源参与者当前力量。</summary>
        public int SourceStrength { get; }

        /// <summary>可选目标标量；无目标时表示展示投影。</summary>
        public BattleEffectTargetSnapshot? Target { get; }

        /// <summary>治疗推演使用的目标生命上限；纯展示或非治疗公式可为空。</summary>
        public int? TargetMaxHealth { get; }

        /// <summary>创建一次纯公式计算上下文。</summary>
        public BattleEffectFormulaContext(
            BattleEffectOperationType operationType,
            int configuredValue,
            int sourceStrength,
            BattleEffectTargetSnapshot? target,
            int? targetMaxHealth = null)
        {
            OperationType = operationType;
            ConfiguredValue = configuredValue;
            SourceStrength = sourceStrength;
            Target = target;
            TargetMaxHealth = targetMaxHealth;
        }
    }

    /// <summary>伤害公式对目标格挡与生命的不可变推演结果。</summary>
    public readonly struct BattleDamageFormulaOutcome
    {
        /// <summary>公式计算后的攻击值。</summary>
        public int AttackValue { get; }

        /// <summary>伤害前格挡。</summary>
        public int BlockBefore { get; }

        /// <summary>伤害后格挡。</summary>
        public int BlockAfter { get; }

        /// <summary>实际吸收量。</summary>
        public int BlockAbsorbed { get; }

        /// <summary>伤害前生命。</summary>
        public int HealthBefore { get; }

        /// <summary>伤害后生命。</summary>
        public int HealthAfter { get; }

        /// <summary>实际生命损失。</summary>
        public int HealthLoss { get; }

        /// <summary>此次伤害是否令原本存活的目标死亡。</summary>
        public bool WasFatal { get; }

        /// <summary>冻结一次完整的伤害公式推演。</summary>
        internal BattleDamageFormulaOutcome(
            int attackValue,
            int blockBefore,
            int blockAfter,
            int blockAbsorbed,
            int healthBefore,
            int healthAfter,
            int healthLoss,
            bool wasFatal)
        {
            AttackValue = attackValue;
            BlockBefore = blockBefore;
            BlockAfter = blockAfter;
            BlockAbsorbed = blockAbsorbed;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
            HealthLoss = healthLoss;
            WasFatal = wasFatal;
        }
    }

    /// <summary>一次受生命上限约束的治疗推演结果。</summary>
    public readonly struct BattleHealthRestorationOutcome
    {
        /// <summary>把配置值归一化后请求恢复的非负生命量。</summary>
        public int RequestedAmount { get; }

        /// <summary>治疗前生命。</summary>
        public int HealthBefore { get; }

        /// <summary>治疗后生命。</summary>
        public int HealthAfter { get; }

        /// <summary>受生命上限约束后实际恢复的生命量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次受生命上限约束的治疗推演。</summary>
        internal BattleHealthRestorationOutcome(
            int requestedAmount,
            int healthBefore,
            int healthAfter)
        {
            RequestedAmount = requestedAmount;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
            Amount = healthAfter - healthBefore;
        }
    }

    /// <summary>集中解析治疗配置，不读取或写入任何参与者权威状态。</summary>
    internal static class BattleHealthRestorationOutcomeResolver
    {
        /// <summary>按当前生命与上限冻结治疗结果，并用剩余生命空间避免加法溢出。</summary>
        internal static BattleHealthRestorationOutcome Resolve(
            int configuredAmount,
            int currentHealth,
            int maxHealth)
        {
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (currentHealth < 0 || currentHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));

            int requestedAmount = Math.Max(0, configuredAmount);
            int remainingHealth = maxHealth - currentHealth;
            int amount = Math.Min(requestedAmount, remainingHealth);
            return new BattleHealthRestorationOutcome(
                requestedAmount,
                currentHealth,
                currentHealth + amount);
        }
    }

    /// <summary>一次纯公式计算的不可变结果。</summary>
    public readonly struct BattleEffectFormulaResult
    {
        /// <summary>展示或写入操作使用的有效值。</summary>
        public int Value { get; }

        /// <summary>有目标伤害操作才具有的格挡与生命结果。</summary>
        public BattleDamageFormulaOutcome? DamageOutcome { get; }

        /// <summary>指示当前结果是否包含目标伤害推演。</summary>
        public bool HasDamageOutcome => DamageOutcome.HasValue;

        /// <summary>有目标治疗操作才具有的生命恢复结果。</summary>
        public BattleHealthRestorationOutcome? HealthRestorationOutcome { get; }

        /// <summary>指示当前结果是否包含目标治疗推演。</summary>
        public bool HasHealthRestorationOutcome => HealthRestorationOutcome.HasValue;

        /// <summary>冻结一次公式结果。</summary>
        internal BattleEffectFormulaResult(
            int value,
            BattleDamageFormulaOutcome? damageOutcome,
            BattleHealthRestorationOutcome? healthRestorationOutcome = null)
        {
            if (damageOutcome.HasValue && healthRestorationOutcome.HasValue)
            {
                throw new ArgumentException("单次 Effect 公式结果不能同时包含伤害与治疗推演。");
            }

            Value = value;
            DamageOutcome = damageOutcome;
            HealthRestorationOutcome = healthRestorationOutcome;
        }
    }
}
