using System;
using System.Collections.Generic;
using TinySpire.Core;

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
        private readonly IBattleDamageFormulaOverride _damageFormulaOverride;
        private readonly BattleBlockRetention _blockRetention;
        private readonly BattleSettlementTriggerEngine _settlementTriggerEngine;

        /// <summary>绑定静态 Tables 与本场唯一参与者事实入口，使用默认伤害公式。</summary>
        public BattleEffectExecutor(
            cfg.Tables tables,
            BattleCombatantsData combatants)
            : this(tables, combatants, damageFormulaOverride: null)
        {
        }

        /// <summary>仅供同程序集会话注入私有伤害公式；不会将职业实现暴露为公共 Effect API。</summary>
        internal BattleEffectExecutor(
            cfg.Tables tables,
            BattleCombatantsData combatants,
            IBattleDamageFormulaOverride damageFormulaOverride = null,
            BattleBlockRetention blockRetention = null,
            BattleSettlementTriggerEngine settlementTriggerEngine = null)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _stateOperations = new BattleCombatantEffectOperations(combatants);
            _damageFormulaOverride = damageFormulaOverride;
            _blockRetention = blockRetention ?? new BattleBlockRetention();
            _settlementTriggerEngine = settlementTriggerEngine ??
                new BattleSettlementTriggerEngine(
                    combatants,
                    Array.Empty<CombatantId>(),
                    new GameRandom(1u));
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
            return PrepareCore(
                request,
                projectedSource: null,
                projectedTarget: null,
                projectedSourceStrength: null,
                projectedTargetStrength: null);
        }

        /// <summary>在联合事务的投影事实上预构建 Effect，同时保留真实初始快照供唯一校验。</summary>
        internal BattleEffectPreparationResult PrepareProjected(
            BattleEffectExecutionRequest request,
            BattleEffectTargetSnapshot projectedSource,
            BattleEffectTargetSnapshot projectedTarget)
        {
            return PrepareCore(
                request,
                projectedSource,
                projectedTarget,
                projectedSourceStrength: null,
                projectedTargetStrength: null);
        }

        /// <summary>在联合事务的完整标量投影上预构建后续 Effect，保持跨抽牌分段的伤害公式一致。</summary>
        internal BattleEffectPreparationResult PrepareProjected(
            BattleEffectExecutionRequest request,
            BattleEffectTargetSnapshot projectedSource,
            BattleEffectTargetSnapshot projectedTarget,
            int projectedSourceStrength,
            int projectedTargetStrength)
        {
            return PrepareCore(
                request,
                projectedSource,
                projectedTarget,
                projectedSourceStrength,
                projectedTargetStrength);
        }

        /// <summary>以实际事实或调用方提供的联合投影执行同一套全量解析与顺序模拟。</summary>
        private BattleEffectPreparationResult PrepareCore(
            BattleEffectExecutionRequest request,
            BattleEffectTargetSnapshot? projectedSource,
            BattleEffectTargetSnapshot? projectedTarget,
            int? projectedSourceStrength,
            int? projectedTargetStrength)
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
            int simulatedSourceStrength = projectedSourceStrength ?? source.CurrentStrength;
            int simulatedTargetStrength = projectedTargetStrength ?? target.CurrentStrength;
            int simulatedTargetHealth = targetFacts.Health;
            int simulatedTargetBlock = targetFacts.Block;
            int simulatedTargetVulnerable = targetFacts.Vulnerable;
            var operations = new List<BattlePreparedEffectOperation>(
                request.EffectIds.Count);
            IBattleDamageFormulaOverrideSequence damageFormulaSequence =
                _damageFormulaOverride?.CreateSequence();
            if (_damageFormulaOverride != null && damageFormulaSequence == null)
                throw new InvalidOperationException("职业伤害覆盖必须为每条 Effect 链创建局部预演序列。");
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
                BattleEffectMagnitudeSource magnitudeSource =
                    BattleEffectMagnitudeSource.ConfiguredValue;
                BattleAttributeType? attribute = null;
                if (BattleCardEffectTypeMapping.IsRegisterBlockGainRandomEnemyDamage(
                        effect.EffectType))
                {
                    if (effect.Attribute != cfg.battle.Attribute.None || effect.Value <= 0)
                    {
                        return Failed(
                            BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                    }

                    operationType =
                        BattleEffectOperationType.RegisterBlockGainRandomEnemyDamage;
                }
                else if (BattleCardEffectTypeMapping.IsRetainBlock(effect.EffectType))
                {
                    if (effect.Attribute != cfg.battle.Attribute.None || effect.Value != 0)
                    {
                        return Failed(
                            BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                    }

                    operationType = BattleEffectOperationType.RetainBlock;
                }
                else if (BattleCardEffectTypeMapping.IsDealDamageFromSourceBlock(
                        effect.EffectType))
                {
                    if (effect.Attribute != cfg.battle.Attribute.None)
                    {
                        return Failed(
                            BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                    }

                    operationType = BattleEffectOperationType.DealDamage;
                    magnitudeSource = BattleEffectMagnitudeSource.SourceBlock;
                }
                else if (BattleCardEffectTypeMapping.IsHeal(effect.EffectType))
                {
                    if (effect.Attribute != cfg.battle.Attribute.None)
                    {
                        return Failed(
                            BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                    }

                    operationType = BattleEffectOperationType.Heal;
                }
                else
                {
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
                }

                int configuredEffectValue = effect.Value;
                if (operationType == BattleEffectOperationType.DealDamage &&
                    magnitudeSource == BattleEffectMagnitudeSource.ConfiguredValue &&
                    request.CardLevelProjection?.EffectDamageValue is int projectedDamageValue)
                {
                    configuredEffectValue = projectedDamageValue;
                }
                int resolvedEffectValue = BattleEffectMagnitudeResolver.Resolve(
                    magnitudeSource,
                    configuredEffectValue,
                    sourceFacts.Block);
                bool shouldSkipTargetNotAlive = simulatedTargetHealth <= 0;
                BattleDamageFormulaOutcome? preparedDamageOutcome = null;
                BattleHealthRestorationOutcome? preparedHealthRestorationOutcome = null;
                BattlePreparedBlockRetentionPlan preparedBlockRetention = null;
                BattlePreparedSettlementTriggerRegistration
                    preparedSettlementTriggerRegistration = null;
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
                                        resolvedEffectValue,
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
                                BattleEffectTargetSnapshot simulatedTarget =
                                    new BattleEffectTargetSnapshot(
                                        simulatedTargetHealth,
                                        simulatedTargetBlock,
                                        simulatedTargetVulnerable);
                                BattleDamageFormulaOutcome damageOutcome;
                                if (damageFormulaSequence != null)
                                {
                                    damageOutcome = damageFormulaSequence.Calculate(
                                        source,
                                        simulatedSourceStrength,
                                        request.TargetId,
                                        resolvedEffectValue,
                                        simulatedTarget);
                                }
                                else
                                {
                                    formula = BattleEffectFormula.Calculate(
                                        new BattleEffectFormulaContext(
                                            operationType,
                                            resolvedEffectValue,
                                            simulatedSourceStrength,
                                            simulatedTarget));
                                    damageOutcome = formula.DamageOutcome.Value;
                                }

                                preparedDamageOutcome = damageOutcome;
                                simulatedTargetBlock = damageOutcome.BlockAfter;
                                simulatedTargetHealth = damageOutcome.HealthAfter;
                                break;
                            case BattleEffectOperationType.GainBlock:
                                formula = BattleEffectFormula.Calculate(
                                    new BattleEffectFormulaContext(
                                        operationType,
                                        resolvedEffectValue,
                                        sourceStrength: 0,
                                        target: null));
                                simulatedTargetBlock = checked(
                                    simulatedTargetBlock + formula.Value);
                                break;
                            case BattleEffectOperationType.ApplyVulnerable:
                                formula = BattleEffectFormula.Calculate(
                                    new BattleEffectFormulaContext(
                                        operationType,
                                        resolvedEffectValue,
                                        sourceStrength: 0,
                                        target: null));
                                simulatedTargetVulnerable = checked(
                                    simulatedTargetVulnerable + formula.Value);
                                break;
                            case BattleEffectOperationType.Heal:
                                formula = BattleEffectFormula.Calculate(
                                    new BattleEffectFormulaContext(
                                        operationType,
                                        resolvedEffectValue,
                                        sourceStrength: 0,
                                        target: new BattleEffectTargetSnapshot(
                                            simulatedTargetHealth,
                                            simulatedTargetBlock,
                                            simulatedTargetVulnerable),
                                        targetMaxHealth: target.MaxHealth));
                                if (!formula.HealthRestorationOutcome.HasValue)
                                {
                                    throw new InvalidOperationException(
                                        "目标治疗公式未返回治疗推演结果。");
                                }

                                preparedHealthRestorationOutcome =
                                    formula.HealthRestorationOutcome.Value;
                                simulatedTargetHealth =
                                    preparedHealthRestorationOutcome.Value.HealthAfter;
                                break;
                            case BattleEffectOperationType.RetainBlock:
                                preparedBlockRetention =
                                    _blockRetention.PreparePermanent(request.TargetId);
                                break;
                            case BattleEffectOperationType.RegisterBlockGainRandomEnemyDamage:
                                preparedSettlementTriggerRegistration =
                                    _settlementTriggerEngine
                                        .PrepareBlockGainRandomEnemyDamage(
                                            request.TargetId,
                                            resolvedEffectValue);
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
                    resolvedEffectValue,
                    attribute,
                    shouldSkipTargetNotAlive,
                    preparedDamageOutcome,
                    preparedHealthRestorationOutcome,
                    preparedBlockRetention,
                    preparedSettlementTriggerRegistration));
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
                    damageFormulaSequence,
                    projectedSourceAfterEffect,
                    projectedTargetAfterEffect,
                    simulatedSourceStrength,
                    simulatedTargetStrength));
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

            var settlements = new List<BattleSettlementRecord>(
                plan.PlannedSettlementCount);
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

                if (operation.OperationType == BattleEffectOperationType.RetainBlock)
                {
                    BattleBlockRetentionSnapshot after =
                        _blockRetention.CommitPrepared(operation.PreparedBlockRetention);
                    settlements.Add(new BattleStatusAppliedSettlement(
                        startingOrder + settlements.Count,
                        operation.EffectId,
                        plan.SourceId,
                        plan.TargetId,
                        BattleStatusType.BlockRetention,
                        operation.PreparedBlockRetention.Before.IsPermanent ? 1 : 0,
                        after.IsPermanent ? 1 : 0));
                    continue;
                }

                if (operation.OperationType ==
                    BattleEffectOperationType.RegisterBlockGainRandomEnemyDamage)
                {
                    BattleSettlementTriggerRegistrationOutcome after =
                        _settlementTriggerEngine.CommitPrepared(
                            operation.PreparedSettlementTriggerRegistration);
                    settlements.Add(new BattleStatusAppliedSettlement(
                        startingOrder + settlements.Count,
                        operation.EffectId,
                        plan.SourceId,
                        plan.TargetId,
                        BattleStatusType.BlockGainDamageTrigger,
                        after.ValueBefore,
                        after.ValueAfter));
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
                        stateResult = _stateOperations.ApplyPreparedDamage(
                            plan.SourceId,
                            plan.TargetId,
                            operation.PreparedDamageOutcome.Value);
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
                    case BattleEffectOperationType.Heal:
                        stateResult = _stateOperations.ApplyPreparedHealthRestoration(
                            plan.TargetId,
                            operation.PreparedHealthRestorationOutcome.Value);
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
                    case BattleEffectOperationType.Heal:
                        settlements.Add(new BattleHealthRestoredSettlement(
                            order,
                            operation.EffectId,
                            plan.SourceId,
                            plan.TargetId,
                            stateResult.HealthRestorationOutcome.Value));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation.OperationType));
                }

                if (operation.OperationType == BattleEffectOperationType.DealDamage)
                {
                    AppendDamageAftermath(
                        plan.DamageFormulaSequence,
                        plan.SourceId,
                        plan.TargetId,
                        operation.PreparedDamageOutcome.Value,
                        startingOrder,
                        settlements);
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

            int totalSettlementCount = plan.PlannedSettlementCount;
            if (totalSettlementCount > 0 &&
                (long)startingOrder + totalSettlementCount - 1 > int.MaxValue)
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

            foreach (BattlePreparedEffectOperation operation in plan.Operations)
            {
                if (operation.PreparedBlockRetention != null &&
                    !_blockRetention.ValidatePrepared(operation.PreparedBlockRetention))
                {
                    throw new InvalidOperationException(
                        "Effect 计划预构建后格挡保留事实发生了漂移。");
                }
                if (operation.PreparedSettlementTriggerRegistration != null &&
                    !_settlementTriggerEngine.ValidatePrepared(
                        operation.PreparedSettlementTriggerRegistration))
                {
                    throw new InvalidOperationException(
                        "Effect 计划预构建后结算触发注册事实发生了漂移。");
                }
            }

            plan.MarkValidated(startingOrder);
        }

        /// <summary>在伤害主记录后追加职业伤害序列冻结的后效记录，并验证其顺序不会越过当前权威 Effect 结算链。</summary>
        private static void AppendDamageAftermath(
            IBattleDamageFormulaOverrideSequence damageFormulaSequence,
            CombatantId sourceId,
            CombatantId targetId,
            BattleDamageFormulaOutcome damageOutcome,
            int startingOrder,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (damageFormulaSequence == null)
                return;

            IReadOnlyList<BattleSettlementRecord> aftermath =
                damageFormulaSequence.CommitDamageAftermath(
                    sourceId,
                    targetId,
                    damageOutcome,
                    checked(startingOrder + settlements.Count));
            if (aftermath == null)
                throw new InvalidOperationException("职业伤害序列不能返回空引用后效集合。");

            foreach (BattleSettlementRecord settlement in aftermath)
            {
                if (settlement == null || settlement.Order != startingOrder + settlements.Count)
                {
                    throw new InvalidOperationException(
                        "职业伤害后效 settlement 必须按当前 Effect 链连续排序。");
                }

                settlements.Add(settlement);
            }
        }

        /// <summary>创建零计划、零记录的内部预构建失败结果。</summary>
        private static BattleEffectPreparationResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleEffectPreparationResult(failureReason, null);
        }
    }

    /// <summary>集中维护通用卡牌 Effect 类型判断，避免执行器散落枚举比较。</summary>
    internal static class BattleCardEffectTypeMapping
    {
        /// <summary>判断一条 Luban Effect 是否声明普通抽牌。</summary>
        internal static bool IsDrawCards(cfg.battle.EffectType effectType)
        {
            return effectType == cfg.battle.EffectType.DrawCards;
        }

        /// <summary>判断一条 Luban Effect 是否声明消耗显式选择的手牌。</summary>
        internal static bool IsExhaustSelectedHandCard(cfg.battle.EffectType effectType)
        {
            return effectType == cfg.battle.EffectType.ExhaustSelectedHandCard;
        }

        /// <summary>判断一条 Luban Effect 是否声明受生命上限约束的治疗。</summary>
        internal static bool IsHeal(cfg.battle.EffectType effectType)
        {
            return effectType == cfg.battle.EffectType.Heal;
        }

        /// <summary>判断一条 Luban Effect 是否把命令起点的来源格挡冻结为伤害基础值。</summary>
        internal static bool IsDealDamageFromSourceBlock(cfg.battle.EffectType effectType)
        {
            return effectType == cfg.battle.EffectType.DealDamageFromSourceBlock;
        }

        /// <summary>判断一条 Luban Effect 是否声明永久保留格挡。</summary>
        internal static bool IsRetainBlock(cfg.battle.EffectType effectType)
        {
            return effectType == cfg.battle.EffectType.RetainBlock;
        }

        /// <summary>判断 Effect 是否声明免费打出抽牌堆顶并强制消耗。</summary>
        internal static bool IsPlayTopDrawCardAndExhaust(cfg.battle.EffectType effectType)
        {
            return effectType == cfg.battle.EffectType.PlayTopDrawCardAndExhaust;
        }

        /// <summary>判断 Effect 是否声明获得格挡后对随机敌人造成固定伤害的持久触发。</summary>
        internal static bool IsRegisterBlockGainRandomEnemyDamage(
            cfg.battle.EffectType effectType)
        {
            return effectType == cfg.battle.EffectType.RegisterBlockGainRandomEnemyDamage;
        }

        /// <summary>判断卡牌文本参数是否应直接展示配置 Value，而不进入战斗数值公式。</summary>
        internal static bool UsesLiteralDisplayValue(cfg.battle.EffectType effectType)
        {
            return IsDrawCards(effectType) ||
                   IsExhaustSelectedHandCard(effectType) ||
                   IsHeal(effectType) ||
                   IsRegisterBlockGainRandomEnemyDamage(effectType);
        }
    }

    /// <summary>免费触发出牌在父命令首次写入前冻结的卡区事实与内部请求。</summary>
    internal sealed class BattlePreparedTriggeredCardPlay
    {
        internal BattleTriggeredCardPlayExecution Owner { get; }
        internal BattleCardZonesData CardZones { get; }
        internal CardZoneLayoutData InitialLayout { get; }
        internal BattleTriggeredCardPlayRequest Request { get; }
        internal bool IsValidated { get; private set; }
        internal bool IsConsumed { get; private set; }

        /// <summary>冻结计划归属、卡区布局与可空的顶牌触发请求。</summary>
        internal BattlePreparedTriggeredCardPlay(
            BattleTriggeredCardPlayExecution owner,
            BattleCardZonesData cardZones,
            CardZoneLayoutData initialLayout,
            BattleTriggeredCardPlayRequest request)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            CardZones = cardZones ?? throw new ArgumentNullException(nameof(cardZones));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            Request = request;
        }

        /// <summary>记录首次写入前的唯一校验结果。</summary>
        internal void MarkValidated()
        {
            if (IsValidated || IsConsumed)
                throw new InvalidOperationException("触发出牌计划已经校验或消费。");
            IsValidated = true;
        }

        /// <summary>消费已经校验的触发出牌计划一次。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated || IsConsumed)
                throw new InvalidOperationException("触发出牌计划尚未校验或已经消费。");
            IsConsumed = true;
        }
    }

    /// <summary>隐藏顶牌选择、目标冻结和 Queue 内部免费出牌请求构造的深模块。</summary>
    internal sealed class BattleTriggeredCardPlayExecution
    {
        private readonly cfg.Tables _tables;

        /// <summary>绑定只读卡牌配置；权威卡区由每次请求显式提供。</summary>
        internal BattleTriggeredCardPlayExecution(cfg.Tables tables)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        }

        /// <summary>冻结抽牌堆顶卡；空抽牌堆形成成功的无请求计划，敌方目标卡留待后续目标策略切片。</summary>
        internal BattlePreparedTriggeredCardPlay PrepareTopDrawCardAndExhaust(
            CombatantId actorId,
            BattleCardZonesData cardZones,
            int parentDepth,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (cardZones == null)
                throw new ArgumentNullException(nameof(cardZones));
            if (parentDepth < 0 || parentDepth >= BattleTriggeredCardPlayRequest.MaximumDepth)
            {
                failureReason = BattleCommandExecutionFailureReason.InvalidEffectBinding;
                return null;
            }

            CardZoneLayoutData initialLayout = cardZones.Layout.CurrentValue;
            if (initialLayout.DrawPile.Count == 0)
            {
                failureReason = BattleCommandExecutionFailureReason.None;
                return new BattlePreparedTriggeredCardPlay(
                    this,
                    cardZones,
                    initialLayout,
                    request: null);
            }

            CardInstanceId cardId = initialLayout.DrawPile[initialLayout.DrawPile.Count - 1];
            if (!cardZones.TryGetCard(cardId, out CardInstanceData card))
            {
                failureReason = BattleCommandExecutionFailureReason.CardTemplateNotFound;
                return null;
            }

            cfg.battle.Card template = _tables.TbCard.GetOrDefault(card.TemplateId);
            if (template == null)
            {
                failureReason = BattleCommandExecutionFailureReason.CardTemplateNotFound;
                return null;
            }
            if (template.ImplementationStatus != cfg.battle.CardImplementationStatus.Implemented)
            {
                failureReason = BattleCommandExecutionFailureReason.CardNotImplemented;
                return null;
            }

            CombatantId? targetId;
            switch (template.TargetRule)
            {
                case cfg.battle.TargetRule.Self:
                    targetId = actorId;
                    break;
                case cfg.battle.TargetRule.RandomEnemy:
                    targetId = null;
                    break;
                case cfg.battle.TargetRule.Enemy:
                    failureReason = BattleCommandExecutionFailureReason.UnsupportedTargetRule;
                    return null;
                default:
                    failureReason = BattleCommandExecutionFailureReason.UnsupportedTargetRule;
                    return null;
            }

            var request = new BattleTriggeredCardPlayRequest(
                actorId,
                cardId,
                targetId,
                BattleCardZone.DrawPile,
                BattleCardPaymentMode.Waived,
                BattleCardZone.ExhaustPile,
                parentDepth + 1);
            failureReason = BattleCommandExecutionFailureReason.None;
            return new BattlePreparedTriggeredCardPlay(
                this,
                cardZones,
                initialLayout,
                request);
        }

        /// <summary>首次写入前确认顶牌、布局、归属和一次性阶段没有漂移。</summary>
        /// <summary>冻结一张仍在手牌中的触发牌；职业适配器负责先决定候选与目标，本模块只封装免支付请求和布局快照。</summary>
        internal BattlePreparedTriggeredCardPlay PrepareHandCard(
            CombatantId actorId,
            BattleCardZonesData cardZones,
            CardInstanceId cardId,
            CombatantId? targetId,
            BattleCardZone destination,
            int parentDepth,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (cardZones == null)
                throw new ArgumentNullException(nameof(cardZones));
            if (parentDepth < 0 || parentDepth >= BattleTriggeredCardPlayRequest.MaximumDepth)
            {
                failureReason = BattleCommandExecutionFailureReason.InvalidEffectBinding;
                return null;
            }

            CardZoneLayoutData initialLayout = cardZones.Layout.CurrentValue;
            if (!ContainsCard(initialLayout.Hand, cardId))
            {
                failureReason = BattleCommandExecutionFailureReason.CardNotInHand;
                return null;
            }
            if (!cardZones.TryGetCard(cardId, out CardInstanceData card))
            {
                failureReason = BattleCommandExecutionFailureReason.CardTemplateNotFound;
                return null;
            }

            cfg.battle.Card template = _tables.TbCard.GetOrDefault(card.TemplateId);
            if (template == null)
            {
                failureReason = BattleCommandExecutionFailureReason.CardTemplateNotFound;
                return null;
            }
            if (template.ImplementationStatus != cfg.battle.CardImplementationStatus.Implemented)
            {
                failureReason = BattleCommandExecutionFailureReason.CardNotImplemented;
                return null;
            }
            if (template.TargetRule == cfg.battle.TargetRule.Self &&
                (!targetId.HasValue || targetId.Value != actorId))
            {
                failureReason = BattleCommandExecutionFailureReason.TargetRuleMismatch;
                return null;
            }
            if ((template.TargetRule == cfg.battle.TargetRule.RandomEnemy ||
                 template.TargetRule == cfg.battle.TargetRule.AllEnemies) && targetId.HasValue)
            {
                failureReason = BattleCommandExecutionFailureReason.TargetRuleMismatch;
                return null;
            }

            var request = new BattleTriggeredCardPlayRequest(
                actorId,
                cardId,
                targetId,
                BattleCardZone.Hand,
                BattleCardPaymentMode.Waived,
                destination,
                parentDepth + 1);
            failureReason = BattleCommandExecutionFailureReason.None;
            return new BattlePreparedTriggeredCardPlay(
                this,
                cardZones,
                initialLayout,
                request);
        }

        /// <summary>提交前确认触发出牌计划仍属于当前执行器且冻结卡区事实未漂移。</summary>
        internal void ValidatePrepared(BattlePreparedTriggeredCardPlay plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this) || plan.IsValidated || plan.IsConsumed)
                throw new InvalidOperationException("触发出牌计划归属或一次性阶段无效。");
            if (!ReferenceEquals(plan.CardZones.Layout.CurrentValue, plan.InitialLayout))
                throw new InvalidOperationException("触发出牌计划的卡区布局已经漂移。");
            if (plan.Request != null && !ContainsFrozenSource(plan))
            {
                throw new InvalidOperationException("触发出牌计划冻结的顶牌已经漂移。");
            }

            plan.MarkValidated();
        }

        /// <summary>消费已校验计划并返回冻结请求；提交本身不写卡区或参与者事实。</summary>
        internal BattleTriggeredCardPlayRequest CommitPrepared(
            BattlePreparedTriggeredCardPlay plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this) || !plan.IsValidated || plan.IsConsumed)
                throw new InvalidOperationException("触发出牌计划尚未校验或已经消费。");

            plan.MarkConsumed();
            return plan.Request;
        }

        /// <summary>按冻结来源区确认触发牌仍处于预构建时的位置。</summary>
        private static bool ContainsFrozenSource(BattlePreparedTriggeredCardPlay plan)
        {
            switch (plan.Request.SourceZone)
            {
                case BattleCardZone.DrawPile:
                    return plan.InitialLayout.DrawPile.Count > 0 &&
                           plan.InitialLayout.DrawPile[plan.InitialLayout.DrawPile.Count - 1] ==
                           plan.Request.CardId;
                case BattleCardZone.Hand:
                    return ContainsCard(plan.InitialLayout.Hand, plan.Request.CardId);
                default:
                    return false;
            }
        }

        /// <summary>在不依赖 LINQ 的情况下确认只读卡牌序列包含指定实例。</summary>
        private static bool ContainsCard(
            IReadOnlyList<CardInstanceId> cardIds,
            CardInstanceId cardId)
        {
            foreach (CardInstanceId candidateId in cardIds)
            {
                if (candidateId == cardId)
                    return true;
            }

            return false;
        }
    }

    /// <summary>一张普通卡全部绑定的冻结组合计划，按绑定原序组合战斗 Effect 与至多一次普通抽牌。</summary>
    internal sealed class BattlePreparedCardEffectSequence
    {
        internal BattleCardEffectSequenceExecutor Owner { get; }
        internal BattleCardZonesData CardZones { get; }
        internal BattlePreparedEffectPlan BeforeDrawEffects { get; }
        internal BattlePreparedDraw Draw { get; }
        internal BattlePreparedEffectPlan AfterDrawEffects { get; }
        internal BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture
            SelectedHandCardDrawAndPlayedCardDeparture { get; }
        internal BattlePreparedTriggeredCardPlay TriggeredCardPlay { get; }
        internal int StartingOrder { get; }
        internal bool IsValidated { get; private set; }
        internal bool IsConsumed { get; private set; }

        internal bool CommitsPlayedCardDeparture =>
            SelectedHandCardDrawAndPlayedCardDeparture != null;

        internal BattleTriggeredCardPlayRequest TriggeredCardPlayRequest =>
            TriggeredCardPlay?.Request;

        internal int PlannedSettlementCount =>
            SelectedHandCardDrawAndPlayedCardDeparture?.Settlements.Count ?? checked(
                (BeforeDrawEffects?.PlannedSettlementCount ?? 0) +
                (Draw?.Settlements.Count ?? 0) +
                (AfterDrawEffects?.PlannedSettlementCount ?? 0));

        /// <summary>冻结组合计划的归属、卡区、子计划和结算起始序号。</summary>
        internal BattlePreparedCardEffectSequence(
            BattleCardEffectSequenceExecutor owner,
            BattleCardZonesData cardZones,
            BattlePreparedEffectPlan beforeDrawEffects,
            BattlePreparedDraw draw,
            BattlePreparedEffectPlan afterDrawEffects,
            BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture
                selectedHandCardDrawAndPlayedCardDeparture,
            BattlePreparedTriggeredCardPlay triggeredCardPlay,
            int startingOrder)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            CardZones = cardZones ?? throw new ArgumentNullException(nameof(cardZones));
            if (selectedHandCardDrawAndPlayedCardDeparture != null &&
                (beforeDrawEffects != null || draw != null || afterDrawEffects != null ||
                 triggeredCardPlay != null))
            {
                throw new ArgumentException(
                    "选择抽牌归宿计划不能与普通 Effect 子计划同时存在。",
                    nameof(selectedHandCardDrawAndPlayedCardDeparture));
            }

            BeforeDrawEffects = beforeDrawEffects;
            Draw = draw;
            AfterDrawEffects = afterDrawEffects;
            SelectedHandCardDrawAndPlayedCardDeparture =
                selectedHandCardDrawAndPlayedCardDeparture;
            TriggeredCardPlay = triggeredCardPlay;
            StartingOrder = startingOrder >= 0
                ? startingOrder
                : throw new ArgumentOutOfRangeException(nameof(startingOrder));
        }

        /// <summary>把组合计划标记为已完成首次写入前校验。</summary>
        internal void MarkValidated()
        {
            if (IsValidated || IsConsumed)
                throw new InvalidOperationException("卡牌 Effect 组合计划已经校验或提交。");
            IsValidated = true;
        }

        /// <summary>把已经校验的组合计划标记为一次性提交。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated || IsConsumed)
                throw new InvalidOperationException("卡牌 Effect 组合计划尚未校验或已经提交。");
            IsConsumed = true;
        }
    }

    /// <summary>一次通用卡牌 Effect 组合预构建的成功计划或稳定失败原因。</summary>
    internal readonly struct BattleCardEffectSequencePreparationResult
    {
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;
        internal BattleCommandExecutionFailureReason FailureReason { get; }
        internal BattlePreparedCardEffectSequence Plan { get; }

        /// <summary>冻结组合预构建结果；失败结果不得携带计划。</summary>
        internal BattleCardEffectSequencePreparationResult(
            BattleCommandExecutionFailureReason failureReason,
            BattlePreparedCardEffectSequence plan)
        {
            if (failureReason == BattleCommandExecutionFailureReason.None && plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (failureReason != BattleCommandExecutionFailureReason.None && plan != null)
                throw new ArgumentException("失败的卡牌 Effect 组合不能携带计划。", nameof(plan));

            FailureReason = failureReason;
            Plan = plan;
        }
    }

    /// <summary>隐藏普通卡绑定解析、战斗 Effect 投影和抽牌联合事务的深模块。</summary>
    internal sealed class BattleCardEffectSequenceExecutor
    {
        private readonly cfg.Tables _tables;
        private readonly BattleEffectExecutor _effectExecutor;
        private readonly BattleTriggeredCardPlayExecution _triggeredCardPlayExecution;

        /// <summary>绑定静态表与既有战斗 Effect 执行器。</summary>
        internal BattleCardEffectSequenceExecutor(
            cfg.Tables tables,
            BattleEffectExecutor effectExecutor,
            BattleTriggeredCardPlayExecution triggeredCardPlayExecution = null)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _effectExecutor = effectExecutor ?? throw new ArgumentNullException(nameof(effectExecutor));
            _triggeredCardPlayExecution = triggeredCardPlayExecution ??
                new BattleTriggeredCardPlayExecution(_tables);
        }

        /// <summary>在首次写入前按绑定原序预构建全部战斗 Effect 与至多一次普通抽牌。</summary>
        internal BattleCardEffectSequencePreparationResult Prepare(
            IEnumerable<cfg.battle.CardEffectBinding> bindings,
            CombatantId sourceId,
            CombatantId targetId,
            BattleCardZonesData cardZones,
            CardInstanceId playedCardId,
            BattleCardZone playedCardDestination,
            IReadOnlyList<CardInstanceId> selectedCardIds,
            int startingOrder,
            int triggeredPlayDepth = 0,
            BattleCardLevelProjection cardLevelProjection = null)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));
            if (cardZones == null)
                throw new ArgumentNullException(nameof(cardZones));
            if (selectedCardIds == null)
                throw new ArgumentNullException(nameof(selectedCardIds));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));

            var beforeDrawEffectIds = new List<BattleEffectId>();
            var afterDrawEffectIds = new List<BattleEffectId>();
            bool foundSelectedHandCardExhaust = false;
            bool foundDraw = false;
            bool foundTriggeredCardPlay = false;
            int drawCount = 0;
            foreach (cfg.battle.CardEffectBinding binding in bindings)
            {
                if (binding == null || binding.EffectId <= 0)
                    return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

                cfg.battle.CardEffect effect = _tables.TbCardEffect.GetOrDefault(binding.EffectId);
                if (effect == null)
                    return Failed(BattleCommandExecutionFailureReason.EffectTemplateNotFound);
                if (BattleCardEffectTypeMapping.IsPlayTopDrawCardAndExhaust(effect.EffectType))
                {
                    if (foundTriggeredCardPlay || foundSelectedHandCardExhaust || foundDraw ||
                        beforeDrawEffectIds.Count > 0 || afterDrawEffectIds.Count > 0)
                    {
                        return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);
                    }
                    if (effect.Attribute != cfg.battle.Attribute.None)
                        return Failed(BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                    if (effect.Value != 0)
                        return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

                    foundTriggeredCardPlay = true;
                    continue;
                }
                if (foundTriggeredCardPlay)
                    return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);
                if (BattleCardEffectTypeMapping.IsExhaustSelectedHandCard(effect.EffectType))
                {
                    if (foundSelectedHandCardExhaust || foundDraw || beforeDrawEffectIds.Count > 0)
                        return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);
                    if (effect.Attribute != cfg.battle.Attribute.None)
                        return Failed(BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                    if (effect.Value != 1)
                        return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

                    foundSelectedHandCardExhaust = true;
                    continue;
                }
                if (BattleCardEffectTypeMapping.IsDrawCards(effect.EffectType))
                {
                    if (foundDraw)
                        return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);
                    if (effect.Attribute != cfg.battle.Attribute.None)
                        return Failed(BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);
                    if (effect.Value < 0)
                        return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

                    foundDraw = true;
                    drawCount = effect.Value;
                    continue;
                }

                if (foundSelectedHandCardExhaust)
                    return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

                (foundDraw ? afterDrawEffectIds : beforeDrawEffectIds).Add(
                    new BattleEffectId(binding.EffectId));
            }

            if (foundTriggeredCardPlay)
            {
                if (selectedCardIds.Count > 0)
                    return Failed(BattleCommandExecutionFailureReason.InvalidCardSelectionCount);

                BattlePreparedTriggeredCardPlay triggeredCardPlay =
                    _triggeredCardPlayExecution.PrepareTopDrawCardAndExhaust(
                        sourceId,
                        cardZones,
                        triggeredPlayDepth,
                        out BattleCommandExecutionFailureReason triggerFailureReason);
                if (triggerFailureReason != BattleCommandExecutionFailureReason.None)
                    return Failed(triggerFailureReason);

                var triggeredPlan = new BattlePreparedCardEffectSequence(
                    this,
                    cardZones,
                    beforeDrawEffects: null,
                    draw: null,
                    afterDrawEffects: null,
                    selectedHandCardDrawAndPlayedCardDeparture: null,
                    triggeredCardPlay: triggeredCardPlay,
                    startingOrder: startingOrder);
                return new BattleCardEffectSequencePreparationResult(
                    BattleCommandExecutionFailureReason.None,
                    triggeredPlan);
            }

            if (foundSelectedHandCardExhaust)
            {
                if (!foundDraw || afterDrawEffectIds.Count > 0)
                    return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);
                if (selectedCardIds.Count > 1)
                    return Failed(BattleCommandExecutionFailureReason.InvalidCardSelectionCount);

                CardInstanceId? selectedCardId = selectedCardIds.Count == 1
                    ? selectedCardIds[0]
                    : (CardInstanceId?)null;
                BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture cardZonePlan;
                try
                {
                    cardZonePlan = cardZones.PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                        selectedCardId,
                        BattleCardZone.ExhaustPile,
                        drawCount,
                        BattleCardZonesData.BattleCardHandLimit,
                        playedCardId,
                        playedCardDestination,
                        startingOrder);
                }
                catch (InvalidOperationException)
                {
                    return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);
                }

                var selectedHandCardPlan = new BattlePreparedCardEffectSequence(
                    this,
                    cardZones,
                    beforeDrawEffects: null,
                    draw: null,
                    afterDrawEffects: null,
                    selectedHandCardDrawAndPlayedCardDeparture: cardZonePlan,
                    triggeredCardPlay: null,
                    startingOrder: startingOrder);
                _ = checked(startingOrder + selectedHandCardPlan.PlannedSettlementCount);
                return new BattleCardEffectSequencePreparationResult(
                    BattleCommandExecutionFailureReason.None,
                    selectedHandCardPlan);
            }
            if (selectedCardIds.Count > 0)
                return Failed(BattleCommandExecutionFailureReason.InvalidCardSelectionCount);

            BattlePreparedEffectPlan beforeDrawPlan = null;
            if (beforeDrawEffectIds.Count > 0)
            {
                BattleEffectPreparationResult preparation = _effectExecutor.Prepare(
                    new BattleEffectExecutionRequest(
                        sourceId,
                        targetId,
                        beforeDrawEffectIds,
                        cardLevelProjection));
                if (!preparation.Succeeded)
                    return Failed(preparation.FailureReason);
                beforeDrawPlan = preparation.Plan;
            }

            BattlePreparedEffectPlan afterDrawPlan = null;
            if (afterDrawEffectIds.Count > 0)
            {
                var request = new BattleEffectExecutionRequest(
                    sourceId,
                    targetId,
                    afterDrawEffectIds,
                    cardLevelProjection);
                BattleEffectPreparationResult preparation = beforeDrawPlan == null
                    ? _effectExecutor.Prepare(request)
                    : _effectExecutor.PrepareProjected(
                        request,
                        beforeDrawPlan.ProjectedSourceAfterEffect,
                        beforeDrawPlan.ProjectedTargetAfterEffect,
                        beforeDrawPlan.ProjectedSourceStrengthAfterEffect,
                        beforeDrawPlan.ProjectedTargetStrengthAfterEffect);
                if (!preparation.Succeeded)
                    return Failed(preparation.FailureReason);
                afterDrawPlan = preparation.Plan;
            }

            int drawStartingOrder = checked(
                startingOrder + (beforeDrawPlan?.PlannedSettlementCount ?? 0));
            BattlePreparedDraw drawPlan = foundDraw
                ? cardZones.PrepareDraw(
                    drawCount,
                    drawStartingOrder,
                    BattleCardZonesData.BattleCardHandLimit)
                : null;
            var plan = new BattlePreparedCardEffectSequence(
                this,
                cardZones,
                beforeDrawPlan,
                drawPlan,
                afterDrawPlan,
                selectedHandCardDrawAndPlayedCardDeparture: null,
                triggeredCardPlay: null,
                startingOrder: startingOrder);
            _ = checked(startingOrder + plan.PlannedSettlementCount);
            return new BattleCardEffectSequencePreparationResult(
                BattleCommandExecutionFailureReason.None,
                plan);
        }

        /// <summary>首次写入前联合校验战斗快照、卡区布局、洗牌随机和一次性状态。</summary>
        internal void ValidatePrepared(BattlePreparedCardEffectSequence plan)
        {
            ValidateOwnerAndState(plan, requireValidated: false);
            if (plan.SelectedHandCardDrawAndPlayedCardDeparture != null)
            {
                if (!plan.CardZones.ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(
                        plan.SelectedHandCardDrawAndPlayedCardDeparture))
                {
                    throw new InvalidOperationException(
                        "卡牌 Effect 组合中的选择抽牌归宿计划发生快照漂移。");
                }

                plan.MarkValidated();
                return;
            }
            if (plan.Draw != null && !plan.CardZones.ValidatePreparedDraw(plan.Draw))
                throw new InvalidOperationException("卡牌 Effect 组合中的抽牌计划发生快照漂移。");
            if (plan.TriggeredCardPlay != null)
                _triggeredCardPlayExecution.ValidatePrepared(plan.TriggeredCardPlay);

            int order = plan.StartingOrder;
            if (plan.BeforeDrawEffects != null)
            {
                _effectExecutor.ValidatePreparedExecution(plan.BeforeDrawEffects, order);
                order = checked(order + plan.BeforeDrawEffects.PlannedSettlementCount);
            }
            if (plan.Draw != null)
                order = checked(order + plan.Draw.Settlements.Count);
            if (plan.AfterDrawEffects != null)
                _effectExecutor.ValidatePreparedExecution(plan.AfterDrawEffects, order);

            plan.MarkValidated();
        }

        /// <summary>按绑定原序一次性提交已经联合校验的战斗 Effect 与普通抽牌。</summary>
        internal IReadOnlyList<BattleSettlementRecord> CommitPrepared(
            BattlePreparedCardEffectSequence plan)
        {
            ValidateOwnerAndState(plan, requireValidated: true);
            plan.MarkConsumed();
            var settlements = new List<BattleSettlementRecord>(plan.PlannedSettlementCount);
            if (plan.TriggeredCardPlay != null)
                _triggeredCardPlayExecution.CommitPrepared(plan.TriggeredCardPlay);
            if (plan.SelectedHandCardDrawAndPlayedCardDeparture != null)
            {
                AppendSettlements(
                    plan.CardZones.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(
                        plan.SelectedHandCardDrawAndPlayedCardDeparture).Settlements,
                    settlements,
                    plan.StartingOrder);
                return settlements.AsReadOnly();
            }
            if (plan.BeforeDrawEffects != null)
            {
                AppendSettlements(
                    _effectExecutor.CommitPrepared(plan.BeforeDrawEffects).Settlements,
                    settlements,
                    plan.StartingOrder);
            }
            if (plan.Draw != null)
            {
                AppendSettlements(
                    plan.CardZones.CommitPreparedDraw(plan.Draw).Settlements,
                    settlements,
                    plan.StartingOrder);
            }
            if (plan.AfterDrawEffects != null)
            {
                AppendSettlements(
                    _effectExecutor.CommitPrepared(plan.AfterDrawEffects).Settlements,
                    settlements,
                    plan.StartingOrder);
            }

            return settlements.AsReadOnly();
        }

        /// <summary>校验组合计划归属和一次性阶段。</summary>
        private void ValidateOwnerAndState(
            BattlePreparedCardEffectSequence plan,
            bool requireValidated)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("卡牌 Effect 组合不能由另一个 executor 执行。");
            if (plan.IsConsumed || plan.IsValidated != requireValidated)
                throw new InvalidOperationException("卡牌 Effect 组合计划处于无效的一次性阶段。");
        }

        /// <summary>校验并追加一段连续排序的冻结结算记录。</summary>
        private static void AppendSettlements(
            IReadOnlyList<BattleSettlementRecord> source,
            List<BattleSettlementRecord> destination,
            int startingOrder)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            foreach (BattleSettlementRecord settlement in source)
            {
                if (settlement == null || settlement.Order != startingOrder + destination.Count)
                    throw new InvalidOperationException("卡牌 Effect 组合 settlement 必须连续排序。");
                destination.Add(settlement);
            }
        }

        /// <summary>创建零计划的稳定预构建失败结果。</summary>
        private static BattleCardEffectSequencePreparationResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleCardEffectSequencePreparationResult(failureReason, null);
        }
    }
}
