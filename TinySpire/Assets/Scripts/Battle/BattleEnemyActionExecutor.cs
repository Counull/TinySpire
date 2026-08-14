using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>敌人行动联合预构建的互斥结果类别。</summary>
    internal enum BattleEnemyActionPreparationKind
    {
        Prepared,
        Succeeded,
        BattleEnded,
        Failed,
        Faulted,
    }

    /// <summary>敌人行动联合执行的互斥结果类别。</summary>
    internal enum BattleEnemyActionResultKind
    {
        Succeeded,
        BattleEnded,
        Failed,
        Faulted,
    }

    /// <summary>一次敌人行动联合预构建的冻结结果。</summary>
    internal sealed class BattleEnemyActionPreparationResult
    {
        /// <summary>预构建、即时成功、终局、普通失败或 fault。</summary>
        internal BattleEnemyActionPreparationKind Kind { get; }

        /// <summary>普通失败时的稳定原因。</summary>
        internal BattleCommandExecutionFailureReason? FailureReason { get; }

        /// <summary>首次写入前 fault 的稳定原因。</summary>
        internal BattleCommandQueueFaultReason? FaultReason { get; }

        /// <summary>成功预构建时的联合计划。</summary>
        internal BattlePreparedEnemyActionPlan Plan { get; }

        /// <summary>死亡 source 即时成功时的专用记录；其他非提交结果为空。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>调用方预定并由本 module 复制冻结的 continuation 候选。</summary>
        internal BattleEnemyActionContinuationSnapshot Continuation { get; }

        /// <summary>冻结一次互斥的联合预构建结果。</summary>
        private BattleEnemyActionPreparationResult(
            BattleEnemyActionPreparationKind kind,
            BattleCommandExecutionFailureReason? failureReason,
            BattleCommandQueueFaultReason? faultReason,
            BattlePreparedEnemyActionPlan plan,
            IEnumerable<BattleSettlementRecord> settlements,
            BattleEnemyActionContinuationSnapshot continuation)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            Kind = kind;
            FailureReason = failureReason;
            FaultReason = faultReason;
            Plan = plan;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
            Continuation = continuation ?? new BattleEnemyActionContinuationSnapshot(null);
        }

        /// <summary>创建完整、尚未写入的联合计划结果。</summary>
        internal static BattleEnemyActionPreparationResult Prepared(
            BattlePreparedEnemyActionPlan plan)
        {
            return new BattleEnemyActionPreparationResult(
                BattleEnemyActionPreparationKind.Prepared,
                failureReason: null,
                faultReason: null,
                plan ?? throw new ArgumentNullException(nameof(plan)),
                Array.Empty<BattleSettlementRecord>(),
                plan.Continuation);
        }

        /// <summary>创建死亡 source 的 source-only 即时成功结果。</summary>
        internal static BattleEnemyActionPreparationResult SourceSkipped(
            BattleEnemyActionSkippedSettlement settlement,
            BattleEnemyActionContinuationSnapshot continuation)
        {
            return new BattleEnemyActionPreparationResult(
                BattleEnemyActionPreparationKind.Succeeded,
                failureReason: null,
                faultReason: null,
                plan: null,
                new BattleSettlementRecord[]
                {
                    settlement ?? throw new ArgumentNullException(nameof(settlement)),
                },
                continuation);
        }

        /// <summary>创建当前没有存活玩家的零写入终局结果。</summary>
        internal static BattleEnemyActionPreparationResult Ended()
        {
            return new BattleEnemyActionPreparationResult(
                BattleEnemyActionPreparationKind.BattleEnded,
                failureReason: null,
                faultReason: null,
                plan: null,
                Array.Empty<BattleSettlementRecord>(),
                continuation: null);
        }

        /// <summary>创建空结算、零写入的普通执行失败。</summary>
        internal static BattleEnemyActionPreparationResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleEnemyActionPreparationResult(
                BattleEnemyActionPreparationKind.Failed,
                failureReason,
                faultReason: null,
                plan: null,
                Array.Empty<BattleSettlementRecord>(),
                continuation: null);
        }

        /// <summary>创建空结算、首次写入前的结构化 fault。</summary>
        internal static BattleEnemyActionPreparationResult Faulted(
            BattleCommandQueueFaultReason faultReason)
        {
            return new BattleEnemyActionPreparationResult(
                BattleEnemyActionPreparationKind.Faulted,
                failureReason: null,
                faultReason,
                plan: null,
                Array.Empty<BattleSettlementRecord>(),
                continuation: null);
        }
    }

    /// <summary>一次敌人行动联合提交或零写入终态的冻结结果。</summary>
    internal sealed class BattleEnemyActionExecutionResult
    {
        /// <summary>成功、终局、普通失败或 fault。</summary>
        internal BattleEnemyActionResultKind Kind { get; }

        /// <summary>普通失败时的稳定原因。</summary>
        internal BattleCommandExecutionFailureReason? FailureReason { get; }

        /// <summary>首次写入前 fault 的稳定原因。</summary>
        internal BattleCommandQueueFaultReason? FaultReason { get; }

        /// <summary>成功时按真实提交顺序冻结的结算；其他结果为空。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>复制冻结的可选 continuation 候选。</summary>
        internal BattleEnemyActionContinuationSnapshot Continuation { get; }

        /// <summary>冻结一次互斥的敌人行动结果。</summary>
        private BattleEnemyActionExecutionResult(
            BattleEnemyActionResultKind kind,
            BattleCommandExecutionFailureReason? failureReason,
            BattleCommandQueueFaultReason? faultReason,
            IEnumerable<BattleSettlementRecord> settlements,
            BattleEnemyActionContinuationSnapshot continuation)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            Kind = kind;
            FailureReason = failureReason;
            FaultReason = faultReason;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
            Continuation = continuation ?? new BattleEnemyActionContinuationSnapshot(null);
        }

        /// <summary>创建完整提交或死亡 source 跳过的成功结果。</summary>
        internal static BattleEnemyActionExecutionResult Succeeded(
            IEnumerable<BattleSettlementRecord> settlements,
            BattleEnemyActionContinuationSnapshot continuation)
        {
            return new BattleEnemyActionExecutionResult(
                BattleEnemyActionResultKind.Succeeded,
                failureReason: null,
                faultReason: null,
                settlements,
                continuation);
        }

        /// <summary>创建当前没有存活玩家的零写入终局结果。</summary>
        internal static BattleEnemyActionExecutionResult Ended()
        {
            return new BattleEnemyActionExecutionResult(
                BattleEnemyActionResultKind.BattleEnded,
                failureReason: null,
                faultReason: null,
                Array.Empty<BattleSettlementRecord>(),
                continuation: null);
        }

        /// <summary>创建空结算、零写入的普通失败结果。</summary>
        internal static BattleEnemyActionExecutionResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleEnemyActionExecutionResult(
                BattleEnemyActionResultKind.Failed,
                failureReason,
                faultReason: null,
                Array.Empty<BattleSettlementRecord>(),
                continuation: null);
        }

        /// <summary>创建空结算、首次写入前的结构化 fault 结果。</summary>
        internal static BattleEnemyActionExecutionResult Faulted(
            BattleCommandQueueFaultReason faultReason)
        {
            return new BattleEnemyActionExecutionResult(
                BattleEnemyActionResultKind.Faulted,
                failureReason: null,
                faultReason,
                Array.Empty<BattleSettlementRecord>(),
                continuation: null);
        }
    }

    /// <summary>联合冻结中毒触发、可选行为组件与 continuation 的一次性敌人行动计划。</summary>
    internal sealed class BattlePreparedEnemyActionPlan
    {
        /// <summary>创建计划的唯一联合 executor。</summary>
        internal BattleEnemyActionExecutor Owner { get; }

        /// <summary>联合初始快照与一次性 guard。</summary>
        internal BattleEnemyActionJointInitialSnapshot InitialSnapshot { get; }
        internal BattleEnemyActionJointCommitGuard Guard { get; }

        /// <summary>行动最开始执行的通用中毒触发计划。</summary>
        internal BattlePreparedPoisonTick PoisonTickPlan { get; }

        /// <summary>中毒触发记录在完整联合事务中的冻结起始序号。</summary>
        internal int PoisonTickStartingOrder { get; }

        /// <summary>指示中毒是否会令 source 死亡并切换到 source-only 跳过分支。</summary>
        internal bool IsPoisonFatal => PoisonTickPlan.WasFatal;

        /// <summary>中毒致死后 SourceNotAlive 跳过记录的冻结序号；非致死分支为空。</summary>
        internal int? FatalSkipOrder { get; }

        /// <summary>行动前 Block 时机计划。</summary>
        internal BattlePreparedStatusTimingPlan StartStatusPlan { get; }

        /// <summary>以 Block=0 投影预构建的 Effect 计划。</summary>
        internal BattlePreparedEffectPlan EffectPlan { get; }

        /// <summary>Effect 记录在完整联合事务中的冻结起始序号。</summary>
        internal int EffectStartingOrder { get; }

        /// <summary>以 Effect 后 source 投影预构建的 Vulnerable 时机计划。</summary>
        internal BattlePreparedStatusTimingPlan CompletionStatusPlan { get; }

        /// <summary>敌人行动结束时清除职业私有临时状态的冻结计划；本场无职业运行时时为空。</summary>
        internal MachineGunnerActorActionEndPlan ActorActionEndPlan { get; }

        /// <summary>职业行动结束记录在联合事务中的冻结起始序号。</summary>
        internal int ActorActionEndStartingOrder { get; }

        /// <summary>同一初始 Intent 快照预构建的下一意图计划。</summary>
        internal BattlePreparedEnemyIntentCompletion IntentPlan { get; }

        /// <summary>复制冻结的可选 continuation 候选。</summary>
        internal BattleEnemyActionContinuationSnapshot Continuation =>
            InitialSnapshot.Continuation;

        /// <summary>冻结一次非致死或致死中毒分支，并拒绝互相矛盾的 component 组合。</summary>
        internal BattlePreparedEnemyActionPlan(
            BattleEnemyActionExecutor owner,
            BattleEnemyActionJointInitialSnapshot initialSnapshot,
            BattlePreparedPoisonTick poisonTickPlan,
            int poisonTickStartingOrder,
            int? fatalSkipOrder,
            BattlePreparedStatusTimingPlan startStatusPlan,
            BattlePreparedEffectPlan effectPlan,
            int effectStartingOrder,
            BattlePreparedStatusTimingPlan completionStatusPlan,
            MachineGunnerActorActionEndPlan actorActionEndPlan,
            int actorActionEndStartingOrder,
            BattlePreparedEnemyIntentCompletion intentPlan)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialSnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            PoisonTickPlan = poisonTickPlan ?? throw new ArgumentNullException(nameof(poisonTickPlan));
            if (poisonTickStartingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(poisonTickStartingOrder));
            if (poisonTickPlan.TargetId != initialSnapshot.Source.Id)
                throw new ArgumentException("中毒触发目标必须是本次行动的敌人 source。", nameof(poisonTickPlan));

            bool hasNormalComponents = startStatusPlan != null &&
                effectPlan != null &&
                completionStatusPlan != null &&
                intentPlan != null;
            bool hasAnyNormalComponent = startStatusPlan != null ||
                effectPlan != null ||
                completionStatusPlan != null ||
                actorActionEndPlan != null ||
                intentPlan != null;
            if (poisonTickPlan.WasFatal)
            {
                long expectedFatalSkipOrder = (long)poisonTickStartingOrder +
                    (poisonTickPlan.HasWrite ? 1 : 0);
                if (expectedFatalSkipOrder > int.MaxValue ||
                    initialSnapshot.HasTarget ||
                    hasAnyNormalComponent ||
                    !fatalSkipOrder.HasValue ||
                    fatalSkipOrder.Value != expectedFatalSkipOrder)
                {
                    throw new ArgumentException("中毒致死计划只能包含 source-only 快照、触发与跳过记录。", nameof(initialSnapshot));
                }
            }
            else if (!initialSnapshot.HasTarget || !hasNormalComponents || fatalSkipOrder.HasValue)
            {
                throw new ArgumentException("非致死敌人行动计划必须包含完整行为组件且不能包含致死跳过记录。", nameof(initialSnapshot));
            }

            PoisonTickStartingOrder = poisonTickStartingOrder;
            FatalSkipOrder = fatalSkipOrder;
            StartStatusPlan = startStatusPlan;
            EffectPlan = effectPlan;
            EffectStartingOrder = effectStartingOrder;
            CompletionStatusPlan = completionStatusPlan;
            ActorActionEndPlan = actorActionEndPlan;
            ActorActionEndStartingOrder = actorActionEndStartingOrder;
            IntentPlan = intentPlan;
            Guard = new BattleEnemyActionJointCommitGuard(initialSnapshot);
        }
    }

    /// <summary>
    /// 以同一初始权威快照联合预构建并提交敌人 Block、Effect、Vulnerable 与下一意图。
    /// M8C 只提供纯 module，不签发 Queue token、不入队或修改 Turn。
    /// </summary>
    internal sealed class BattleEnemyActionExecutor
    {
        private readonly cfg.Tables _tables;
        private readonly BattleCombatantsData _combatants;
        private readonly BattleEnemyIntentsData _intents;
        private readonly BattleEnemyActionTargetResolver _targetResolver;
        private readonly BattleStatusTiming _statusTiming;
        private readonly BattleEffectExecutor _effectExecutor;
        private readonly BattlePoisonApplication _poisonApplication;
        private readonly MachineGunnerBattleRuntime _machineGunnerRuntime;

        /// <summary>绑定本场唯一参与者、意图和同一静态配置。</summary>
        internal BattleEnemyActionExecutor(
            cfg.Tables tables,
            BattleCombatantsData combatants,
            BattleEnemyIntentsData intents,
            MachineGunnerBattleRuntime machineGunnerRuntime = null)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
            _targetResolver = new BattleEnemyActionTargetResolver(combatants);
            _statusTiming = new BattleStatusTiming(combatants);
            _poisonApplication = new BattlePoisonApplication(combatants);
            _machineGunnerRuntime = machineGunnerRuntime;
            _effectExecutor = new BattleEffectExecutor(
                tables,
                combatants,
                _machineGunnerRuntime?.DamageFormulaOverride);
        }

        /// <summary>完成联合 prepare、唯一 validate 与无普通失败 commit 的深入口。</summary>
        internal BattleEnemyActionExecutionResult Execute(
            CombatantId enemyId,
            BattleTurnData currentTurn,
            CompleteEnemyActionCommand plannedContinuation,
            int startingOrder)
        {
            BattleEnemyActionPreparationResult preparation = Prepare(
                enemyId,
                currentTurn,
                plannedContinuation,
                startingOrder);
            switch (preparation.Kind)
            {
                case BattleEnemyActionPreparationKind.Prepared:
                    if (!ValidatePrepared(preparation.Plan, currentTurn))
                    {
                        return BattleEnemyActionExecutionResult.Faulted(
                            BattleCommandQueueFaultReason.PreparedInvariantViolation);
                    }

                    return CommitPrepared(preparation.Plan);
                case BattleEnemyActionPreparationKind.Succeeded:
                    return BattleEnemyActionExecutionResult.Succeeded(
                        preparation.Settlements,
                        preparation.Continuation);
                case BattleEnemyActionPreparationKind.BattleEnded:
                    return BattleEnemyActionExecutionResult.Ended();
                case BattleEnemyActionPreparationKind.Failed:
                    return BattleEnemyActionExecutionResult.Failed(
                        preparation.FailureReason.Value);
                case BattleEnemyActionPreparationKind.Faulted:
                    return BattleEnemyActionExecutionResult.Faulted(
                        preparation.FaultReason.Value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(preparation.Kind));
            }
        }

        /// <summary>首次写入前从同一权威事实联合预构建完整敌人行动。</summary>
        internal BattleEnemyActionPreparationResult Prepare(
            CombatantId enemyId,
            BattleTurnData currentTurn,
            CompleteEnemyActionCommand plannedContinuation,
            int startingOrder)
        {
            if (currentTurn == null)
                throw new ArgumentNullException(nameof(currentTurn));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (currentTurn.Phase == BattleTurnPhase.BattleEnded)
            {
                return BattleEnemyActionPreparationResult.Failed(
                    BattleCommandExecutionFailureReason.BattleAlreadyEnded);
            }
            if (currentTurn.Phase != BattleTurnPhase.EnemyAction)
            {
                return BattleEnemyActionPreparationResult.Failed(
                    BattleCommandExecutionFailureReason.InvalidTurnPhase);
            }
            if (!_combatants.TryGet(enemyId, out CombatantData sourceData) ||
                sourceData is not EnemyCombatantData source)
            {
                return BattleEnemyActionPreparationResult.Failed(
                    BattleCommandExecutionFailureReason.InvalidEnemy);
            }
            if (!currentTurn.CurrentActingEnemyId.HasValue ||
                currentTurn.CurrentActingEnemyId.Value != enemyId)
            {
                return BattleEnemyActionPreparationResult.Failed(
                    BattleCommandExecutionFailureReason.EnemyNotCurrentActor);
            }
            var continuation = new BattleEnemyActionContinuationSnapshot(plannedContinuation);
            if (!source.IsAlive)
            {
                return BattleEnemyActionPreparationResult.SourceSkipped(
                    new BattleEnemyActionSkippedSettlement(
                        startingOrder,
                        source.Id,
                        BattleEnemyActionSkipReason.SourceNotAlive),
                    continuation);
            }

            int poisonTickStartingOrder = startingOrder;
            BattlePoisonTickPreparationResult poisonTick =
                _poisonApplication.PrepareTick(source.Id);
            if (!poisonTick.Succeeded)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            long orderAfterPoisonTick = (long)poisonTickStartingOrder +
                (poisonTick.Plan.HasWrite ? 1 : 0);
            if (orderAfterPoisonTick > int.MaxValue)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            BattleEnemyIntentAuthoritySnapshot intentSnapshot;
            try
            {
                intentSnapshot = _intents.CaptureAuthoritySnapshot(source.Id);
            }
            catch (InvalidOperationException)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.MissingEnemyBehavior);
            }

            int nextOrder = (int)orderAfterPoisonTick;
            if (poisonTick.Plan.WasFatal)
            {
                long orderAfterFatalSkip = (long)nextOrder + 1;
                if (orderAfterFatalSkip > int.MaxValue)
                {
                    return BattleEnemyActionPreparationResult.Faulted(
                        BattleCommandQueueFaultReason.PreparedInvariantViolation);
                }

                var sourceOnlySnapshot = new BattleEnemyActionJointInitialSnapshot(
                    source,
                    currentTurn,
                    intentSnapshot,
                    plannedContinuation);
                return BattleEnemyActionPreparationResult.Prepared(
                    new BattlePreparedEnemyActionPlan(
                        this,
                        sourceOnlySnapshot,
                        poisonTick.Plan,
                        poisonTickStartingOrder,
                        fatalSkipOrder: nextOrder,
                        startStatusPlan: null,
                        effectPlan: null,
                        effectStartingOrder: 0,
                        completionStatusPlan: null,
                        actorActionEndPlan: null,
                        actorActionEndStartingOrder: 0,
                        intentPlan: null));
            }

            BattleEnemyActionTargetEvaluation playerPreflight = _targetResolver.Resolve(
                source.Id,
                cfg.battle.TargetRule.Self,
                nextOrder);
            if (playerPreflight.Kind == BattleEnemyActionTargetResolutionKind.BattleEnded)
                return BattleEnemyActionPreparationResult.Ended();
            if (playerPreflight.Kind == BattleEnemyActionTargetResolutionKind.Faulted)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    playerPreflight.FaultReason.Value);
            }

            cfg.battle.EnemyBehavior behavior =
                _tables.TbEnemyBehavior.GetOrDefault(intentSnapshot.CurrentBehaviorId);
            if (behavior == null)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.MissingEnemyBehavior);
            }
            if (!Enum.IsDefined(typeof(cfg.battle.EnemyIntentType), behavior.IntentType))
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.UnsupportedConfiguration);
            }
            if (behavior.EffectId <= 0)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.MissingEffect);
            }

            BattleEnemyActionTargetEvaluation targetEvaluation = _targetResolver.Resolve(
                source.Id,
                behavior.TargetRule,
                startingOrder);
            if (targetEvaluation.Kind == BattleEnemyActionTargetResolutionKind.BattleEnded)
                return BattleEnemyActionPreparationResult.Ended();
            if (targetEvaluation.Kind == BattleEnemyActionTargetResolutionKind.Faulted)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    targetEvaluation.FaultReason.Value);
            }
            if (targetEvaluation.Kind != BattleEnemyActionTargetResolutionKind.Resolved ||
                !targetEvaluation.TargetId.HasValue ||
                !_combatants.TryGet(targetEvaluation.TargetId.Value, out CombatantData target))
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            var effectId = new BattleEffectId(behavior.EffectId);
            var effectIds = new[] { effectId };
            var sourceSnapshot = new BattleCombatantScalarSnapshot(source);
            var targetSnapshot = new BattleCombatantScalarSnapshot(target);
            BattleEffectTargetSnapshot sourceAfterPoisonTick =
                new BattleEffectTargetSnapshot(
                    poisonTick.Plan.HealthAfter,
                    poisonTick.Plan.BlockAfter,
                    sourceSnapshot.Vulnerable);
            BattleEffectTargetSnapshot sourceBeforeEffect = BattleStatusTiming.Project(
                BattleStatusTimingPoint.EnemyActionStarted,
                sourceAfterPoisonTick);
            BattleStatusTimingPreparationResult startStatus = _statusTiming.PrepareProjected(
                BattleStatusTimingPoint.EnemyActionStarted,
                source.Id,
                sourceAfterPoisonTick,
                nextOrder);
            if (!startStatus.Succeeded)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            long orderAfterStart = (long)nextOrder + startStatus.Plan.Settlements.Count;
            if (orderAfterStart > int.MaxValue)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            nextOrder = (int)orderAfterStart;
            BattleEffectTargetSnapshot targetBeforeEffect =
                target.Id == source.Id
                    ? sourceBeforeEffect
                    : new BattleEffectTargetSnapshot(
                        targetSnapshot.Health,
                        targetSnapshot.Block,
                        targetSnapshot.Vulnerable);
            int effectStartingOrder = nextOrder;
            BattleEffectPreparationResult effect = _effectExecutor.PrepareProjected(
                new BattleEffectExecutionRequest(
                    source.Id,
                    target.Id,
                    effectIds),
                sourceBeforeEffect,
                targetBeforeEffect);
            if (!effect.Succeeded)
                return BattleEnemyActionPreparationResult.Faulted(MapEffectFault(effect.FailureReason));

            CompleteEnemyActionCommand resolvedContinuation =
                target is PlayerCombatantData &&
                effect.Plan.ProjectedTargetAfterEffect.Health <= 0
                    ? null
                    : plannedContinuation;
            var initialSnapshot = new BattleEnemyActionJointInitialSnapshot(
                source,
                target,
                currentTurn,
                intentSnapshot,
                effectIds,
                resolvedContinuation);

            long orderAfterEffect = (long)nextOrder + effect.Plan.PlannedSettlementCount;
            if (orderAfterEffect > int.MaxValue)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            nextOrder = (int)orderAfterEffect;
            BattleStatusTimingPreparationResult completionStatus =
                _statusTiming.PrepareProjected(
                    BattleStatusTimingPoint.EnemyActionCompleted,
                    source.Id,
                    effect.Plan.ProjectedSourceAfterEffect,
                    nextOrder);
            if (!completionStatus.Succeeded)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            long orderAfterCompletion = (long)nextOrder + completionStatus.Plan.Settlements.Count;
            if (orderAfterCompletion > int.MaxValue)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            nextOrder = (int)orderAfterCompletion;
            MachineGunnerActorActionEndPlan actorActionEndPlan =
                _machineGunnerRuntime?.PrepareActorActionEnd(source.Id);
            int actorActionEndStartingOrder = nextOrder;
            long orderAfterActorActionEnd = (long)nextOrder +
                (actorActionEndPlan?.SettlementCount ?? 0);
            if (orderAfterActorActionEnd > int.MaxValue)
            {
                return BattleEnemyActionPreparationResult.Faulted(
                    BattleCommandQueueFaultReason.PreparedInvariantViolation);
            }

            nextOrder = (int)orderAfterActorActionEnd;
            BattleEnemyIntentCompletionPreparationResult intent =
                _intents.PrepareCompletion(initialSnapshot.Intent, nextOrder);
            if (!intent.Succeeded)
                return BattleEnemyActionPreparationResult.Faulted(intent.FaultReason.Value);

            return BattleEnemyActionPreparationResult.Prepared(
                new BattlePreparedEnemyActionPlan(
                    this,
                    initialSnapshot,
                    poisonTick.Plan,
                    poisonTickStartingOrder,
                    null,
                    startStatus.Plan,
                    effect.Plan,
                    effectStartingOrder,
                    completionStatus.Plan,
                    actorActionEndPlan,
                    actorActionEndStartingOrder,
                    intent.Plan));
        }

        /// <summary>首次写入前只执行一次联合 source/可选 target/Turn/Intent/component 校验。</summary>
        internal bool ValidatePrepared(
            BattlePreparedEnemyActionPlan plan,
            BattleTurnData currentTurn)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (currentTurn == null)
                throw new ArgumentNullException(nameof(currentTurn));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能校验其他敌人行动 executor 创建的计划。");
            _combatants.TryGet(
                plan.InitialSnapshot.Source.Id,
                out CombatantData source);
            CombatantData target = null;
            if (plan.InitialSnapshot.HasTarget)
            {
                _combatants.TryGet(
                    plan.InitialSnapshot.Target.Id,
                    out target);
            }
            return plan.Guard.ValidateInitial(
                source,
                target,
                currentTurn,
                () =>
                {
                    if (!_poisonApplication.ValidatePreparedTick(plan.PoisonTickPlan))
                        return false;
                    if (plan.IsPoisonFatal)
                        return plan.InitialSnapshot.Intent.Matches(_intents);
                    if (!_intents.ValidatePreparedCompletion(plan.IntentPlan) ||
                        !_statusTiming.ValidatePrepared(plan.StartStatusPlan))
                    {
                        return false;
                    }

                    _effectExecutor.ValidatePreparedExecution(
                        plan.EffectPlan,
                        plan.EffectStartingOrder);
                    if (!_statusTiming.ValidatePrepared(plan.CompletionStatusPlan))
                        return false;
                    return plan.ActorActionEndPlan == null ||
                        (_machineGunnerRuntime != null &&
                         _machineGunnerRuntime.ValidatePreparedActorActionEnd(
                             plan.ActorActionEndPlan));
                });
        }

        /// <summary>先提交中毒；非致死再提交行为组件，致死则只追加 source 跳过，期间不复验。</summary>
        internal BattleEnemyActionExecutionResult CommitPrepared(
            BattlePreparedEnemyActionPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他敌人行动 executor 创建的计划。");

            var settlements = new List<BattleSettlementRecord>();
            plan.Guard.Commit(() =>
            {
                settlements.AddRange(
                    _poisonApplication.CommitPreparedTick(
                        plan.PoisonTickPlan,
                        plan.PoisonTickStartingOrder));
                if (plan.IsPoisonFatal)
                {
                    settlements.Add(
                        new BattleEnemyActionSkippedSettlement(
                            plan.FatalSkipOrder.Value,
                            plan.InitialSnapshot.Source.Id,
                            BattleEnemyActionSkipReason.SourceNotAlive));
                    return;
                }

                BattleStatusTimingResult start =
                    _statusTiming.CommitPrepared(plan.StartStatusPlan);
                settlements.AddRange(start.Settlements);
                BattleEffectExecutionResult effect =
                    _effectExecutor.CommitPrepared(plan.EffectPlan);
                settlements.AddRange(effect.Settlements);
                BattleStatusTimingResult completion =
                    _statusTiming.CommitPrepared(plan.CompletionStatusPlan);
                settlements.AddRange(completion.Settlements);
                if (plan.ActorActionEndPlan != null)
                {
                    if (_machineGunnerRuntime == null)
                        throw new InvalidOperationException("敌人行动计划缺少机枪兵运行时。");
                    settlements.AddRange(
                        _machineGunnerRuntime.CommitPreparedActorActionEnd(
                            plan.ActorActionEndPlan,
                            plan.ActorActionEndStartingOrder));
                }
                BattleEnemyIntentCompletionResult intent =
                    _intents.CommitPreparedCompletion(plan.IntentPlan);
                settlements.AddRange(intent.Settlements);
            });

            return BattleEnemyActionExecutionResult.Succeeded(
                settlements,
                plan.Continuation);
        }

        /// <summary>把通用 Effect 预构建失败稳定映射为敌人配置或 prepared fault。</summary>
        private static BattleCommandQueueFaultReason MapEffectFault(
            BattleCommandExecutionFailureReason failureReason)
        {
            switch (failureReason)
            {
                case BattleCommandExecutionFailureReason.EffectTemplateNotFound:
                    return BattleCommandQueueFaultReason.MissingEffect;
                case BattleCommandExecutionFailureReason.InvalidEffectBinding:
                case BattleCommandExecutionFailureReason.UnsupportedEffectType:
                case BattleCommandExecutionFailureReason.UnsupportedEffectAttribute:
                case BattleCommandExecutionFailureReason.EffectValueOverflow:
                    return BattleCommandQueueFaultReason.UnsupportedConfiguration;
                default:
                    return BattleCommandQueueFaultReason.PreparedInvariantViolation;
            }
        }
    }
}
