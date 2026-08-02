using System;
using System.Collections.Generic;
using cfg;
using R3;

namespace TinySpire.Battle
{
    /// <summary>
    /// 为全部战斗命令建立唯一权威顺序，并独占 drain、continuation、表现屏障与 fault。
    /// </summary>
    public sealed class BattleCommandQueue : IDisposable
    {
        private readonly IBattleCommandPresentation _presentation;
        private readonly BattleCommandSubmissionCoordinator _coordinator;
        private readonly BattleCommandSchedulingCore _scheduling;
        private readonly BattleTurnController _turnController;
        private readonly BattleEnemyActionExecutor _enemyActionExecutor;
        private readonly BattleTerminalRules _terminalRules;
        private readonly ReactiveProperty<BattleCommandQueueData> _queue;

        /// <summary>权威命令顺序的只读响应式事实。</summary>
        public ReadOnlyReactiveProperty<BattleCommandQueueData> Queue { get; }

        /// <summary>权威回合状态的只读响应式事实。</summary>
        public ReadOnlyReactiveProperty<BattleTurnData> Turn => _turnController.Turn;

        /// <summary>以战斗事实、静态配置、表现适配器与唯一提交协调器创建命令队列。</summary>
        public BattleCommandQueue(
            BattleCombatantsData combatants,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            BattleEnemyIntentsData enemyIntents,
            Tables tables,
            int energyPerRound,
            int initialHandCount,
            IBattleCommandPresentation presentation,
            BattleCommandSubmissionCoordinator coordinator)
        {
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _scheduling = new BattleCommandSchedulingCore(_coordinator);
            if (combatants == null)
                throw new ArgumentNullException(nameof(combatants));
            if (enemyIntents == null)
                throw new ArgumentNullException(nameof(enemyIntents));

            _enemyActionExecutor = new BattleEnemyActionExecutor(
                tables,
                combatants,
                enemyIntents);
            _terminalRules = new BattleTerminalRules(combatants);
            _turnController = new BattleTurnController(
                combatants,
                playerCardZones,
                enemyCombatantIdsInEncounterOrder,
                tables,
                energyPerRound,
                initialHandCount);
            _queue = new ReactiveProperty<BattleCommandQueueData>(_scheduling.CreateQueueSnapshot());
            Queue = _queue.ToReadOnlyReactiveProperty();
        }

        /// <summary>消费同一命令引用的预注册句柄，并通过唯一迭代 drain 接受或执行命令。</summary>
        public BattleCommandSubmissionResult Submit(BattleCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (!_coordinator.TryResolvePreRegistered(command, out BattleCommandHandle handle))
            {
                return BattleCommandSubmissionResult.Rejected(
                    BattleCommandSubmissionFailureReason.InvalidSubmissionHandle);
            }

            BattleCommandSchedulingAcceptance acceptance;
            BattleTurnPhase phase = Turn.CurrentValue.Phase;
            if (_scheduling.Fault != null)
            {
                acceptance = _scheduling.AcceptPreRegistered(
                    handle,
                    command,
                    Turn.CurrentValue.RoundNumber);
            }
            else if (phase == BattleTurnPhase.NotStarted && command.Type != BattleCommandType.StartBattle)
            {
                return _scheduling.RejectPreRegistered(
                    handle,
                    BattleCommandSubmissionFailureReason.BattleNotStarted);
            }
            else if (phase == BattleTurnPhase.BattleEnded)
            {
                return _scheduling.RejectPreRegistered(
                    handle,
                    BattleCommandSubmissionFailureReason.BattleAlreadyEnded);
            }
            else
            {
                acceptance = _scheduling.AcceptPreRegistered(
                    handle,
                    command,
                    Turn.CurrentValue.RoundNumber);
            }

            if (!acceptance.Submission.Accepted)
            {
                PublishQueueSnapshot();
                return acceptance.Submission;
            }

            bool ownsDrain = _scheduling.TryEnterDrain();
            if (!ownsDrain)
            {
                PublishLifecycle(acceptance.QueuedLifecycle);
                PublishQueueSnapshot();
                return acceptance.Submission;
            }

            try
            {
                PublishLifecycle(acceptance.QueuedLifecycle);
                PublishQueueSnapshot();
                DrainOwnedCommands();
            }
            finally
            {
                _scheduling.ExitDrain();
            }

            return acceptance.Submission;
        }

        /// <summary>在当前外层 drain 内迭代执行，任何同步回调提交都只能追加 FIFO。</summary>
        private void DrainOwnedCommands()
        {
            while (_scheduling.TryBeginNext(out BattleCommandSchedulingEntry entry))
            {
                PublishQueueSnapshot();
                BattleQueueExecutionOutcome execution;
                try
                {
                    execution = Execute(entry);
                }
                catch (Exception)
                {
                    FaultCurrent(
                        entry,
                        BattleCommandQueueFaultReason.UnexpectedException,
                        mayHavePartialWrites: true);
                    return;
                }

                if (execution.FaultReason.HasValue)
                {
                    FaultCurrent(
                        entry,
                        execution.FaultReason.Value,
                        mayHavePartialWrites: false);
                    return;
                }

                BattleCommandExecutionResult executionResult = execution.Result;
                BattleCommandSchedulingCompletion completion;
                try
                {
                    completion = _scheduling.CompleteCurrent(
                        entry,
                        executionResult.FailureReason,
                        executionResult.Settlements,
                        execution.Continuation,
                        Turn.CurrentValue.RoundNumber);
                }
                catch (Exception)
                {
                    FaultCurrent(
                        entry,
                        BattleCommandQueueFaultReason.UnexpectedException,
                        mayHavePartialWrites: true);
                    return;
                }

                if (completion.ContinuationQueuedLifecycle != null)
                    PublishLifecycle(completion.ContinuationQueuedLifecycle);
                PublishQueueSnapshot();

                if (executionResult.Settlements.Count == 0)
                {
                    PublishLifecycle(completion.CurrentLifecycle);
                    continue;
                }

                var presentationCompletion = new PresentationCompletion(
                    this,
                    entry.AuthoritySequence);
                try
                {
                    _presentation.Present(executionResult, presentationCompletion.Complete);
                }
                catch (Exception)
                {
                    presentationCompletion.Cancel();
                    FaultCurrent(
                        entry,
                        BattleCommandQueueFaultReason.UnexpectedException,
                        mayHavePartialWrites: true);
                    return;
                }

                PublishLifecycle(completion.CurrentLifecycle);
                presentationCompletion.Arm();
            }
        }

        /// <summary>执行一条队首命令，并把整次同步阶段变化压缩为至多一条可见记录。</summary>
        private BattleQueueExecutionOutcome Execute(BattleCommandSchedulingEntry entry)
        {
            BattleTurnData turnBefore = Turn.CurrentValue;
            BattleTurnOperationResult operationResult;
            BattleCommand frozenContinuation = null;
            bool hasFrozenContinuation = false;
            if (turnBefore.Phase == BattleTurnPhase.BattleEnded)
            {
                operationResult = BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.BattleAlreadyEnded);
            }
            else if (entry.Command is StartBattleCommand)
            {
                operationResult = _turnController.TryStartBattle();
            }
            else if ((entry.Command is PlayCardCommand ||
                      entry.Command is EndPlayerActionCommand) &&
                     entry.SubmittedRoundNumber != turnBefore.RoundNumber)
            {
                operationResult = BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerActionWindowExpired);
            }
            else if (entry.Command is PlayCardCommand playCardCommand)
            {
                operationResult = _turnController.TryPlayCard(playCardCommand);
            }
            else if (entry.Command is EndPlayerActionCommand endPlayerActionCommand)
            {
                operationResult = _turnController.TryEndPlayerAction(endPlayerActionCommand);
            }
            else if (entry.Command is CompleteEnemyActionCommand completeEnemyActionCommand)
            {
                BattleCommandExecutionFailureReason failureReason =
                    _turnController.ValidateCompleteEnemyAction(completeEnemyActionCommand);
                if (failureReason == BattleCommandExecutionFailureReason.None)
                {
                    CompleteEnemyActionCommand plannedContinuation =
                        _turnController.CreateNextEnemyContinuation();
                    BattleEnemyActionExecutionResult enemyResult =
                        _enemyActionExecutor.Execute(
                            completeEnemyActionCommand.EnemyId,
                            turnBefore,
                            plannedContinuation,
                            startingOrder: 0);
                    if (enemyResult.Kind == BattleEnemyActionResultKind.Faulted)
                    {
                        return BattleQueueExecutionOutcome.Faulted(
                            enemyResult.FaultReason.Value);
                    }
                    if (enemyResult.Kind == BattleEnemyActionResultKind.Failed)
                    {
                        operationResult = BattleTurnOperationResult.Failed(
                            enemyResult.FailureReason.Value);
                    }
                    else
                    {
                        BattleTerminalOutcome terminalOutcome = _terminalRules.Evaluate();
                        BattleTurnOperationResult turnAdvance =
                            _turnController.AdvanceAfterValidatedEnemyAction(
                                terminalOutcome,
                                enemyResult.Settlements.Count);
                        if (turnAdvance.FailureReason != BattleCommandExecutionFailureReason.None)
                        {
                            throw new InvalidOperationException(
                                "敌人事务提交后的回合推进不应再返回普通失败。");
                        }

                        var settlements = new List<BattleSettlementRecord>(
                            enemyResult.Settlements.Count + turnAdvance.Settlements.Count);
                        settlements.AddRange(enemyResult.Settlements);
                        settlements.AddRange(turnAdvance.Settlements);
                        operationResult = new BattleTurnOperationResult(
                            BattleCommandExecutionFailureReason.None,
                            settlements);
                        hasFrozenContinuation = true;
                        frozenContinuation = terminalOutcome == BattleTerminalOutcome.Ongoing
                            ? enemyResult.Continuation.Command
                            : null;
                    }
                }
                else
                {
                    operationResult = BattleTurnOperationResult.Failed(failureReason);
                }
            }
            else
            {
                operationResult = BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.UnsupportedCommand);
            }

            BattleTurnOperationResult visibleResult = AppendPhaseSettlement(
                operationResult,
                turnBefore,
                Turn.CurrentValue);
            var executionResult = new BattleCommandExecutionResult(
                entry.AuthoritySequence,
                entry.Command.Type,
                entry.Command.SubmitterId,
                visibleResult.FailureReason,
                visibleResult.Settlements);
            BattleCommand continuation = hasFrozenContinuation
                ? frozenContinuation
                : CreateTurnContinuation(executionResult);
            return BattleQueueExecutionOutcome.Completed(
                executionResult,
                continuation);
        }

        /// <summary>非敌人命令成功写入后若进入敌人行动，则冻结恰好一条 Queue 内部 continuation。</summary>
        private BattleCommand CreateTurnContinuation(BattleCommandExecutionResult executionResult)
        {
            if (!executionResult.Succeeded)
                return null;

            BattleTurnData turn = Turn.CurrentValue;
            if (turn.Phase != BattleTurnPhase.EnemyAction ||
                !turn.CurrentActingEnemyId.HasValue)
            {
                return null;
            }

            return new CompleteEnemyActionCommand(turn.CurrentActingEnemyId.Value);
        }

        /// <summary>把命令前后的稳定回合事实差异追加为唯一阶段记录。</summary>
        private static BattleTurnOperationResult AppendPhaseSettlement(
            BattleTurnOperationResult operationResult,
            BattleTurnData turnBefore,
            BattleTurnData turnAfter)
        {
            if (operationResult == null)
                throw new ArgumentNullException(nameof(operationResult));
            if (turnBefore == null)
                throw new ArgumentNullException(nameof(turnBefore));
            if (turnAfter == null)
                throw new ArgumentNullException(nameof(turnAfter));
            if (operationResult.FailureReason != BattleCommandExecutionFailureReason.None ||
                (turnBefore.Phase == turnAfter.Phase &&
                 turnBefore.RoundNumber == turnAfter.RoundNumber &&
                 turnBefore.CurrentActingEnemyId == turnAfter.CurrentActingEnemyId))
            {
                return operationResult;
            }

            var settlements = new List<BattleSettlementRecord>(operationResult.Settlements)
            {
                new BattlePhaseChangedSettlement(
                    operationResult.Settlements.Count,
                    turnBefore.Phase,
                    turnAfter.Phase,
                    turnBefore.RoundNumber,
                    turnAfter.RoundNumber,
                    turnBefore.CurrentActingEnemyId,
                    turnAfter.CurrentActingEnemyId),
            };
            return new BattleTurnOperationResult(
                BattleCommandExecutionFailureReason.None,
                settlements);
        }

        /// <summary>精确 completion 只解除所属屏障，随后尝试由新的外层 drain 继续。</summary>
        private void CompletePresentation(long authoritySequence)
        {
            if (!_scheduling.CompletePresentation(authoritySequence))
                return;

            PublishQueueSnapshot();
            DrainIfAvailable();
        }

        /// <summary>仅在当前没有外层 drain 时取得所有权并继续迭代。</summary>
        private void DrainIfAvailable()
        {
            if (!_scheduling.TryEnterDrain())
                return;

            try
            {
                DrainOwnedCommands();
            }
            finally
            {
                _scheduling.ExitDrain();
            }
        }

        /// <summary>冻结当前 Queue fault 并发布不携带 battle settlement 的诊断生命周期。</summary>
        private void FaultCurrent(
            BattleCommandSchedulingEntry entry,
            BattleCommandQueueFaultReason reason,
            bool mayHavePartialWrites)
        {
            BattleCommandLifecycleEvent faulted = _scheduling.FaultCurrent(
                entry,
                reason,
                mayHavePartialWrites);
            PublishQueueSnapshot();
            PublishLifecycle(faulted);
        }

        /// <summary>由 Queue 唯一触发生命周期发布，并让 coordinator 集中完成句柄对账。</summary>
        private void PublishLifecycle(BattleCommandLifecycleEvent lifecycleEvent)
        {
            _coordinator.PublishFromQueue(lifecycleEvent);
        }

        /// <summary>从唯一调度核心发布完整只读 Queue 快照。</summary>
        private void PublishQueueSnapshot()
        {
            _queue.Value = _scheduling.CreateQueueSnapshot();
        }

        /// <summary>释放 Queue 与内部回合事实，不释放由场景容器独立拥有的 coordinator。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            _queue.Dispose();
            _turnController.Dispose();
        }

        /// <summary>把一次表现 completion 永久绑定到创建它的权威序号。</summary>
        private sealed class PresentationCompletion
        {
            private readonly BattleCommandQueue _owner;
            private readonly long _authoritySequence;
            private bool _isArmed;
            private bool _isCompletionRequested;
            private bool _isCanceled;
            private bool _isCompleted;

            /// <summary>保存 Queue 与精确序号，防止迟到回调跨过新屏障。</summary>
            internal PresentationCompletion(BattleCommandQueue owner, long authoritySequence)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _authoritySequence = authoritySequence;
            }

            /// <summary>在 Present 成功返回前只记录同步 completion，避免先完成后抛错逃逸 fault。</summary>
            internal void Complete()
            {
                if (_isCanceled || _isCompleted)
                    return;
                if (!_isArmed)
                {
                    _isCompletionRequested = true;
                    return;
                }

                _isCompleted = true;
                _owner.CompletePresentation(_authoritySequence);
            }

            /// <summary>在生命周期终态发布后启用 completion，并兑现此前的同步请求。</summary>
            internal void Arm()
            {
                if (_isCanceled || _isCompleted)
                    return;

                _isArmed = true;
                if (!_isCompletionRequested)
                    return;

                _isCompleted = true;
                _owner.CompletePresentation(_authoritySequence);
            }

            /// <summary>表现入口抛错后永久作废已给出的 completion，保留 fault 诊断现场。</summary>
            internal void Cancel()
            {
                _isCanceled = true;
                _isCompletionRequested = false;
            }
        }

        /// <summary>Queue 内部一次执行的公开结果、冻结 continuation 或首次写入前 fault。</summary>
        private sealed class BattleQueueExecutionOutcome
        {
            /// <summary>普通成功或失败时交给生命周期与表现层的不可变结果。</summary>
            internal BattleCommandExecutionResult Result { get; }

            /// <summary>成功执行后由 Queue 唯一签发的可选系统后继。</summary>
            internal BattleCommand Continuation { get; }

            /// <summary>首次写入前需要冻结 Queue 的结构化原因。</summary>
            internal BattleCommandQueueFaultReason? FaultReason { get; }

            /// <summary>冻结一次互斥的执行结果或 fault。</summary>
            private BattleQueueExecutionOutcome(
                BattleCommandExecutionResult result,
                BattleCommand continuation,
                BattleCommandQueueFaultReason? faultReason)
            {
                Result = result;
                Continuation = continuation;
                FaultReason = faultReason;
            }

            /// <summary>创建可进入现有完成、表现与 continuation 链的普通结果。</summary>
            internal static BattleQueueExecutionOutcome Completed(
                BattleCommandExecutionResult result,
                BattleCommand continuation)
            {
                return new BattleQueueExecutionOutcome(
                    result ?? throw new ArgumentNullException(nameof(result)),
                    continuation,
                    faultReason: null);
            }

            /// <summary>创建不携带 battle settlement 的首次写入前 Queue fault。</summary>
            internal static BattleQueueExecutionOutcome Faulted(
                BattleCommandQueueFaultReason reason)
            {
                return new BattleQueueExecutionOutcome(
                    result: null,
                    continuation: null,
                    faultReason: reason);
            }
        }
    }
}
