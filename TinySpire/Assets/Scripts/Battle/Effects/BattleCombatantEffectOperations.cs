using System;

namespace TinySpire.Battle
{
    /// <summary>内部参与者 Effect 操作的明确执行状态。</summary>
    internal enum BattleCombatantEffectOperationStatus
    {
        Applied,
        SourceNotFound,
        SourceNotAlive,
        TargetNotFound,
        TargetNotAlive,
    }

    /// <summary>内部参与者 Effect 操作返回的不可变状态变化。</summary>
    internal readonly struct BattleCombatantEffectOperationResult
    {
        /// <summary>操作是否写入或明确跳过。</summary>
        internal BattleCombatantEffectOperationStatus Status { get; }

        /// <summary>标量操作前的值。</summary>
        internal int ValueBefore { get; }

        /// <summary>标量操作后的值。</summary>
        internal int ValueAfter { get; }

        /// <summary>标量操作的实际变化量。</summary>
        internal int Amount { get; }

        /// <summary>伤害操作才具有的公式推演结果。</summary>
        internal BattleDamageFormulaOutcome? DamageOutcome { get; }

        /// <summary>冻结一次内部参与者 Effect 操作结果。</summary>
        internal BattleCombatantEffectOperationResult(
            BattleCombatantEffectOperationStatus status,
            int valueBefore,
            int valueAfter,
            BattleDamageFormulaOutcome? damageOutcome)
        {
            Status = status;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
            Amount = valueAfter - valueBefore;
            DamageOutcome = damageOutcome;
        }
    }

    /// <summary>
    /// 由后续 Effect executor 独占的参与者状态操作入口，不暴露为生产公共 seam。
    /// </summary>
    internal sealed class BattleCombatantEffectOperations
    {
        private readonly BattleCombatantsData _combatants;

        /// <summary>绑定本场唯一参与者映射。</summary>
        internal BattleCombatantEffectOperations(BattleCombatantsData combatants)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        }

        /// <summary>按共享公式累加目标格挡并返回前后值。</summary>
        internal BattleCombatantEffectOperationResult GainBlock(
            CombatantId targetId,
            int configuredValue)
        {
            if (!_combatants.TryGet(targetId, out CombatantData target))
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotFound,
                    0,
                    0,
                    null);
            }

            if (!target.IsAlive)
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotAlive,
                    target.CurrentBlock,
                    target.CurrentBlock,
                    null);
            }

            BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
                new BattleEffectFormulaContext(
                    BattleEffectOperationType.GainBlock,
                    configuredValue,
                    sourceStrength: 0,
                    target: null));
            int valueBefore = target.CurrentBlock;
            target.ApplyBlockGain(formula.Value);
            return new BattleCombatantEffectOperationResult(
                BattleCombatantEffectOperationStatus.Applied,
                valueBefore,
                target.CurrentBlock,
                null);
        }

        /// <summary>按共享公式修改目标力量并返回有符号前后值。</summary>
        internal BattleCombatantEffectOperationResult ModifyStrength(
            CombatantId targetId,
            int configuredValue)
        {
            if (!_combatants.TryGet(targetId, out CombatantData target))
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotFound,
                    0,
                    0,
                    null);
            }

            if (!target.IsAlive)
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotAlive,
                    target.CurrentStrength,
                    target.CurrentStrength,
                    null);
            }

            BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
                new BattleEffectFormulaContext(
                    BattleEffectOperationType.ModifyAttribute,
                    configuredValue,
                    sourceStrength: 0,
                    target: null));
            int valueBefore = target.CurrentStrength;
            target.ApplyStrengthChange(formula.Value);
            return new BattleCombatantEffectOperationResult(
                BattleCombatantEffectOperationStatus.Applied,
                valueBefore,
                target.CurrentStrength,
                null);
        }

        /// <summary>按共享公式累加目标易伤并返回前后值。</summary>
        internal BattleCombatantEffectOperationResult ApplyVulnerable(
            CombatantId targetId,
            int configuredValue)
        {
            if (!_combatants.TryGet(targetId, out CombatantData target))
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotFound,
                    0,
                    0,
                    null);
            }

            if (!target.IsAlive)
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotAlive,
                    target.CurrentVulnerable,
                    target.CurrentVulnerable,
                    null);
            }

            BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
                new BattleEffectFormulaContext(
                    BattleEffectOperationType.ApplyVulnerable,
                    configuredValue,
                    sourceStrength: 0,
                    target: null));
            int valueBefore = target.CurrentVulnerable;
            target.ApplyVulnerableGain(formula.Value);
            return new BattleCombatantEffectOperationResult(
                BattleCombatantEffectOperationStatus.Applied,
                valueBefore,
                target.CurrentVulnerable,
                null);
        }

        /// <summary>一次计算并写入格挡与生命伤害结果。</summary>
        internal BattleCombatantEffectOperationResult ApplyDamage(
            CombatantId sourceId,
            CombatantId targetId,
            int configuredValue)
        {
            if (!_combatants.TryGet(sourceId, out CombatantData source))
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.SourceNotFound,
                    0,
                    0,
                    null);
            }

            if (!source.IsAlive)
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.SourceNotAlive,
                    0,
                    0,
                    null);
            }

            if (!_combatants.TryGet(targetId, out CombatantData target))
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotFound,
                    0,
                    0,
                    null);
            }

            if (!target.IsAlive)
            {
                return new BattleCombatantEffectOperationResult(
                    BattleCombatantEffectOperationStatus.TargetNotAlive,
                    0,
                    0,
                    null);
            }

            BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
                new BattleEffectFormulaContext(
                    BattleEffectOperationType.DealDamage,
                    configuredValue,
                    source.CurrentStrength,
                    new BattleEffectTargetSnapshot(
                        target.CurrentHealth,
                        target.CurrentBlock,
                        target.CurrentVulnerable)));
            if (!formula.DamageOutcome.HasValue)
            {
                throw new InvalidOperationException("目标伤害公式未返回伤害推演结果。");
            }

            BattleDamageFormulaOutcome damageOutcome = formula.DamageOutcome.Value;
            target.ApplyDamageOutcome(damageOutcome);
            return new BattleCombatantEffectOperationResult(
                BattleCombatantEffectOperationStatus.Applied,
                0,
                0,
                damageOutcome);
        }
    }
}
