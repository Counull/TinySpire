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

        /// <summary>创建一次纯公式计算上下文。</summary>
        public BattleEffectFormulaContext(
            BattleEffectOperationType operationType,
            int configuredValue,
            int sourceStrength,
            BattleEffectTargetSnapshot? target)
        {
            OperationType = operationType;
            ConfiguredValue = configuredValue;
            SourceStrength = sourceStrength;
            Target = target;
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

    /// <summary>一次纯公式计算的不可变结果。</summary>
    public readonly struct BattleEffectFormulaResult
    {
        /// <summary>展示或写入操作使用的有效值。</summary>
        public int Value { get; }

        /// <summary>有目标伤害操作才具有的格挡与生命结果。</summary>
        public BattleDamageFormulaOutcome? DamageOutcome { get; }

        /// <summary>指示当前结果是否包含目标伤害推演。</summary>
        public bool HasDamageOutcome => DamageOutcome.HasValue;

        /// <summary>冻结一次公式结果。</summary>
        internal BattleEffectFormulaResult(
            int value,
            BattleDamageFormulaOutcome? damageOutcome)
        {
            Value = value;
            DamageOutcome = damageOutcome;
        }
    }
}
