using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>
    /// 隐藏 Effect 表查找、全量预校验、有序状态写入与结算记录生成的具体 module。
    /// </summary>
    public sealed class BattleEffectExecutor
    {
        private readonly cfg.Tables _tables;
        private readonly BattleCombatantsData _combatants;
        private readonly BattleCombatantEffectOperations _stateOperations;

        /// <summary>绑定静态 Tables 与本场唯一参与者事实入口。</summary>
        public BattleEffectExecutor(cfg.Tables tables, BattleCombatantsData combatants)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _stateOperations = new BattleCombatantEffectOperations(combatants);
        }

        /// <summary>完整预构建后按 Effect 标识顺序执行，失败时保证零写入和空记录。</summary>
        public BattleEffectExecutionResult Execute(BattleEffectExecutionRequest request)
        {
            BattleEffectPreparationResult preparation = Prepare(request);
            if (!preparation.Succeeded)
            {
                return new BattleEffectExecutionResult(
                    preparation.FailureReason,
                    Array.Empty<BattleSettlementRecord>());
            }

            ValidatePreparedExecution(preparation.Plan, startingOrder: 0);
            return CommitPrepared(preparation.Plan);
        }

        /// <summary>在首次写入前解析全部 Effect 标识并模拟顺序结果。</summary>
        internal BattleEffectPreparationResult Prepare(BattleEffectExecutionRequest request)
        {
            return PrepareCore(request, projectedSource: null, projectedTarget: null);
        }

        /// <summary>在联合事务的投影事实上预构建 Effect，同时保留真实初始快照供唯一校验。</summary>
        internal BattleEffectPreparationResult PrepareProjected(
            BattleEffectExecutionRequest request,
            BattleEffectTargetSnapshot projectedSource,
            BattleEffectTargetSnapshot projectedTarget)
        {
            return PrepareCore(request, projectedSource, projectedTarget);
        }

        /// <summary>以实际事实或调用方提供的联合投影执行同一套全量解析与顺序模拟。</summary>
        private BattleEffectPreparationResult PrepareCore(
            BattleEffectExecutionRequest request,
            BattleEffectTargetSnapshot? projectedSource,
            BattleEffectTargetSnapshot? projectedTarget)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!_combatants.TryGet(request.SourceId, out CombatantData source))
            {
                return Failed(BattleCommandExecutionFailureReason.EffectSourceNotFound);
            }

            if (!source.IsAlive)
            {
                return Failed(BattleCommandExecutionFailureReason.EffectSourceNotAlive);
            }

            if (!_combatants.TryGet(request.TargetId, out CombatantData target))
            {
                return Failed(BattleCommandExecutionFailureReason.TargetNotFound);
            }

            if (!target.IsAlive)
            {
                return Failed(BattleCommandExecutionFailureReason.TargetNotAlive);
            }

            BattleEffectTargetSnapshot sourceFacts = projectedSource ??
                new BattleEffectTargetSnapshot(
                    source.CurrentHealth,
                    source.CurrentBlock,
                    source.CurrentVulnerable);
            BattleEffectTargetSnapshot targetFacts = projectedTarget ??
                new BattleEffectTargetSnapshot(
                    target.CurrentHealth,
                    target.CurrentBlock,
                    target.CurrentVulnerable);
            int simulatedSourceStrength = source.CurrentStrength;
            int simulatedTargetStrength = target.CurrentStrength;
            int simulatedTargetHealth = targetFacts.Health;
            int simulatedTargetBlock = targetFacts.Block;
            int simulatedTargetVulnerable = targetFacts.Vulnerable;
            var operations = new List<BattlePreparedEffectOperation>(
                request.EffectIds.Count);
            foreach (BattleEffectId effectId in request.EffectIds)
            {
                if (effectId.Value <= 0)
                {
                    return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);
                }

                cfg.battle.CardEffect effect = _tables.TbCardEffect.GetOrDefault(effectId.Value);
                if (effect == null)
                {
                    return Failed(BattleCommandExecutionFailureReason.EffectTemplateNotFound);
                }

                BattleEffectOperationType operationType;
                BattleAttributeType? attribute = null;
                switch (effect.EffectType)
                {
                    case cfg.battle.EffectType.ModifyAttribute:
                        if (effect.Attribute != cfg.battle.Attribute.Strength)
                        {
                            return Failed(
                                BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                        }

                        operationType = BattleEffectOperationType.ModifyAttribute;
                        attribute = BattleAttributeType.Strength;
                        break;
                    case cfg.battle.EffectType.DealDamage:
                        if (effect.Attribute != cfg.battle.Attribute.None)
                        {
                            return Failed(
                                BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                        }

                        operationType = BattleEffectOperationType.DealDamage;
                        break;
                    case cfg.battle.EffectType.GainBlock:
                        if (effect.Attribute != cfg.battle.Attribute.None)
                        {
                            return Failed(
                                BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                        }

                        operationType = BattleEffectOperationType.GainBlock;
                        break;
                    case cfg.battle.EffectType.ApplyVulnerable:
                        if (effect.Attribute != cfg.battle.Attribute.None)
                        {
                            return Failed(
                                BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                        }

                        operationType = BattleEffectOperationType.ApplyVulnerable;
                        break;
                    default:
                        return Failed(BattleCommandExecutionFailureReason.UnsupportedEffectType);
                }

                bool shouldSkipTargetNotAlive = simulatedTargetHealth <= 0;
                try
                {
                    if (!shouldSkipTargetNotAlive)
                    {
                        BattleEffectFormulaResult formula;
                        switch (operationType)
                        {
                            case BattleEffectOperationType.ModifyAttribute:
                                formula = BattleEffectFormula.Calculate(
                                    new BattleEffectFormulaContext(
                                        operationType,
                                        effect.Value,
                                        sourceStrength: 0,
                                        target: null));
                                simulatedTargetStrength = checked(
                                    simulatedTargetStrength + formula.Value);
                                if (request.SourceId == request.TargetId)
                                {
                                    simulatedSourceStrength = simulatedTargetStrength;
                                }

                                break;
                            case BattleEffectOperationType.DealDamage:
                                formula = BattleEffectFormula.Calculate(
                                    new BattleEffectFormulaContext(
                                        operationType,
                                        effect.Value,
                                        simulatedSourceStrength,
                                        new BattleEffectTargetSnapshot(
                                            simulatedTargetHealth,
                                            simulatedTargetBlock,
                                            simulatedTargetVulnerable)));
                                BattleDamageFormulaOutcome damageOutcome =
                                    formula.DamageOutcome.Value;
                                simulatedTargetBlock = damageOutcome.BlockAfter;
                                simulatedTargetHealth = damageOutcome.HealthAfter;
                                break;
                            case BattleEffectOperationType.GainBlock:
                                formula = BattleEffectFormula.Calculate(
                                    new BattleEffectFormulaContext(
                                        operationType,
                                        effect.Value,
                                        sourceStrength: 0,
                                        target: null));
                                simulatedTargetBlock = checked(
                                    simulatedTargetBlock + formula.Value);
                                break;
                            case BattleEffectOperationType.ApplyVulnerable:
                                formula = BattleEffectFormula.Calculate(
                                    new BattleEffectFormulaContext(
                                        operationType,
                                        effect.Value,
                                        sourceStrength: 0,
                                        target: null));
                                simulatedTargetVulnerable = checked(
                                    simulatedTargetVulnerable + formula.Value);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(operationType));
                        }
                    }
                }
                catch (OverflowException)
                {
                    return Failed(BattleCommandExecutionFailureReason.EffectValueOverflow);
                }

                operations.Add(new BattlePreparedEffectOperation(
                    new BattleEffectId(effect.Id),
                    operationType,
                    effect.Value,
                    attribute,
                    shouldSkipTargetNotAlive));
            }

            var projectedTargetAfterEffect = new BattleEffectTargetSnapshot(
                simulatedTargetHealth,
                simulatedTargetBlock,
                simulatedTargetVulnerable);
            BattleEffectTargetSnapshot projectedSourceAfterEffect =
                request.SourceId == request.TargetId
                    ? projectedTargetAfterEffect
                    : sourceFacts;
            return new BattleEffectPreparationResult(
                BattleCommandExecutionFailureReason.None,
                new BattlePreparedEffectPlan(
                    this,
                    source,
                    target,
                    operations,
                    projectedSourceAfterEffect,
                    projectedTargetAfterEffect));
        }

        /// <summary>提交已经验证的冻结计划；本阶段不复验中间事实，也不返回普通失败。</summary>
        internal BattleEffectExecutionResult CommitPrepared(BattlePreparedEffectPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("Effect 计划不能由另一个 executor 执行。");

            plan.MarkConsumed();
            int startingOrder = plan.StartingOrder;

            var settlements = new List<BattleSettlementRecord>(plan.Operations.Count);
            foreach (BattlePreparedEffectOperation operation in plan.Operations)
            {
                if (operation.ShouldSkipTargetNotAlive)
                {
                    settlements.Add(new BattleOperationSkippedSettlement(
                        startingOrder + settlements.Count,
                        operation.EffectId,
                        plan.SourceId,
                        plan.TargetId,
                        BattleOperationSkipReason.TargetNotAlive));
                    continue;
                }


                BattleCombatantEffectOperationResult stateResult;
                switch (operation.OperationType)
                {
                    case BattleEffectOperationType.ModifyAttribute:
                        stateResult = _stateOperations.ModifyStrength(
                            plan.TargetId,
                            operation.ConfiguredValue);
                        break;
                    case BattleEffectOperationType.DealDamage:
                        stateResult = _stateOperations.ApplyDamage(
                            plan.SourceId,
                            plan.TargetId,
                            operation.ConfiguredValue);
                        break;
                    case BattleEffectOperationType.GainBlock:
                        stateResult = _stateOperations.GainBlock(
                            plan.TargetId,
                            operation.ConfiguredValue);
                        break;
                    case BattleEffectOperationType.ApplyVulnerable:
                        stateResult = _stateOperations.ApplyVulnerable(
                            plan.TargetId,
                            operation.ConfiguredValue);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation.OperationType));
                }

                if (stateResult.Status != BattleCombatantEffectOperationStatus.Applied)
                {
                    throw new InvalidOperationException(
                        $"预构建后的 Effect 状态操作意外失败：{stateResult.Status}。");
                }

                int order = startingOrder + settlements.Count;
                switch (operation.OperationType)
                {
                    case BattleEffectOperationType.ModifyAttribute:
                        settlements.Add(new BattleAttributeModifiedSettlement(
                            order,
                            operation.EffectId,
                            plan.SourceId,
                            plan.TargetId,
                            operation.Attribute.Value,
                            stateResult.ValueBefore,
                            stateResult.ValueAfter));
                        break;
                    case BattleEffectOperationType.DealDamage:
                        BattleDamageFormulaOutcome damage = stateResult.DamageOutcome.Value;
                        settlements.Add(new BattleDamageAppliedSettlement(
                            order,
                            operation.EffectId,
                            plan.SourceId,
                            plan.TargetId,
                            damage.AttackValue,
                            damage.BlockBefore,
                            damage.BlockAfter,
                            damage.HealthBefore,
                            damage.HealthAfter));
                        break;
                    case BattleEffectOperationType.GainBlock:
                        settlements.Add(new BattleBlockGainedSettlement(
                            order,
                            operation.EffectId,
                            plan.SourceId,
                            plan.TargetId,
                            stateResult.ValueBefore,
                            stateResult.ValueAfter));
                        break;
                    case BattleEffectOperationType.ApplyVulnerable:
                        settlements.Add(new BattleStatusAppliedSettlement(
                            order,
                            operation.EffectId,
                            plan.SourceId,
                            plan.TargetId,
                            BattleStatusType.Vulnerable,
                            stateResult.ValueBefore,
                            stateResult.ValueAfter));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation.OperationType));
                }
            }

            return new BattleEffectExecutionResult(
                BattleCommandExecutionFailureReason.None,
                settlements);
        }

        /// <summary>在任何调用方写入前确认 prepared plan 仍可从指定记录序号执行。</summary>
        internal void ValidatePreparedExecution(
            BattlePreparedEffectPlan plan,
            int startingOrder)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (startingOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            }

            if (plan.Operations.Count > 0 &&
                (long)startingOrder + plan.Operations.Count - 1 > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingOrder),
                    "Effect 结算记录顺序超出 Int32 范围。");
            }

            if (!ReferenceEquals(plan.Owner, this))
            {
                throw new InvalidOperationException("Effect 计划不能由另一个 executor 执行。");
            }

            if (plan.IsValidated || plan.IsConsumed)
                throw new InvalidOperationException("Effect 计划已经校验或提交。");

            if (!_combatants.TryGet(plan.SourceId, out CombatantData source) ||
                !plan.SourceSnapshot.Matches(source) ||
                !_combatants.TryGet(plan.TargetId, out CombatantData target) ||
                !plan.TargetSnapshot.Matches(target))
            {
                throw new InvalidOperationException("Effect 计划预构建后参与者事实发生了漂移。");
            }

            plan.MarkValidated(startingOrder);
        }

        /// <summary>创建零计划、零记录的内部预构建失败结果。</summary>
        private static BattleEffectPreparationResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleEffectPreparationResult(failureReason, null);
        }
    }
}
