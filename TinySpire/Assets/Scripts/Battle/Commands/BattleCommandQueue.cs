using System;
using System.Collections.Generic;
using cfg;
using R3;

namespace TinySpire.Battle
{
    /// <summary>
    /// 为全部战斗命令建立统一权威顺序的调度根。
    /// </summary>
    public sealed class BattleCommandQueue : IDisposable
    {
        private readonly IBattleCommandPresentation _presentation;
        private readonly BattleTurnController _turnController;
        private readonly ReactiveProperty<BattleCommandQueueData> _queue;
        private readonly Queue<QueuedBattleCommand> _pendingCommands = new Queue<QueuedBattleCommand>();
        private long _nextAuthoritySequence = 1;
        private QueuedBattleCommand _currentCommand;
        private bool _isWaitingForPresentation;

        /// <summary>权威命令顺序的只读响应式事实。</summary>
        public ReadOnlyReactiveProperty<BattleCommandQueueData> Queue { get; }

        /// <summary>权威回合状态的只读响应式事实。</summary>
        public ReadOnlyReactiveProperty<BattleTurnData> Turn => _turnController.Turn;

        /// <summary>以权威战斗事实、静态卡牌配置和表现 adapter 创建统一命令队列。</summary>
        public BattleCommandQueue(
            BattleCombatantsData combatants,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            Tables tables,
            int energyPerRound,
            int initialHandCount,
            IBattleCommandPresentation presentation)
        {
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _turnController = new BattleTurnController(
                combatants,
                playerCardZones,
                enemyCombatantIdsInEncounterOrder,
                tables,
                energyPerRound,
                initialHandCount);
            _queue = new ReactiveProperty<BattleCommandQueueData>(BattleCommandQueueData.Empty());
            Queue = _queue.ToReadOnlyReactiveProperty();
        }

        /// <summary>通过统一 seam 提交命令；未开始时玩家命令不会进入权威顺序。</summary>
        public BattleCommandSubmissionResult Submit(BattleCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (Turn.CurrentValue.Phase == BattleTurnPhase.NotStarted &&
                command.Type != BattleCommandType.StartBattle)
            {
                return BattleCommandSubmissionResult.Rejected(
                    BattleCommandSubmissionFailureReason.BattleNotStarted);
            }

            long authoritySequence = _nextAuthoritySequence++;
            int submittedRoundNumber = Turn.CurrentValue.RoundNumber;
            _pendingCommands.Enqueue(new QueuedBattleCommand(
                authoritySequence,
                submittedRoundNumber,
                command));
            if (_currentCommand == null)
                BeginNextCommand();
            else
                PublishQueueSnapshot();

            return BattleCommandSubmissionResult.AcceptedWith(authoritySequence);
        }

        /// <summary>收到表现完成信号后只推进下一条队首命令，队列耗尽时恢复空闲。</summary>
        private void CompleteCurrentPresentation(long authoritySequence)
        {
            if (_currentCommand == null ||
                !_isWaitingForPresentation ||
                _currentCommand.AuthoritySequence != authoritySequence)
            {
                return;
            }

            _isWaitingForPresentation = false;
            _currentCommand = null;
            if (_pendingCommands.Count > 0)
                BeginNextCommand();
            else
                _queue.Value = BattleCommandQueueData.Empty();
        }

        /// <summary>从权威顺序取出队首命令，执行后交给表现 adapter。</summary>
        private void BeginNextCommand()
        {
            _currentCommand = _pendingCommands.Dequeue();
            _isWaitingForPresentation = false;
            PublishQueueSnapshot();
            BattleCommandExecutionResult executionResult = Execute(_currentCommand);
            _isWaitingForPresentation = true;
            PublishQueueSnapshot();
            var completion = new PresentationCompletion(this, _currentCommand.AuthoritySequence);
            _presentation.Present(executionResult, completion.Complete);
        }

        /// <summary>仅由队首命令调用回合模块并形成执行结果。</summary>
        private BattleCommandExecutionResult Execute(QueuedBattleCommand queuedCommand)
        {
            BattleCommandExecutionFailureReason failureReason;
            if (queuedCommand.Command is StartBattleCommand)
            {
                failureReason = _turnController.TryStartBattle()
                    ? BattleCommandExecutionFailureReason.None
                    : BattleCommandExecutionFailureReason.BattleAlreadyStarted;
            }
            else if ((queuedCommand.Command is PlayCardCommand ||
                      queuedCommand.Command is EndPlayerActionCommand) &&
                     queuedCommand.SubmittedRoundNumber != Turn.CurrentValue.RoundNumber)
            {
                failureReason = BattleCommandExecutionFailureReason.PlayerActionWindowExpired;
            }
            else if (queuedCommand.Command is PlayCardCommand playCardCommand)
            {
                failureReason = _turnController.TryPlayCard(playCardCommand);
            }
            else if (queuedCommand.Command is EndPlayerActionCommand endPlayerActionCommand)
            {
                failureReason = _turnController.TryEndPlayerAction(endPlayerActionCommand);
            }
            else if (queuedCommand.Command is CompleteEnemyActionCommand completeEnemyActionCommand)
            {
                failureReason = _turnController.TryCompleteEnemyAction(completeEnemyActionCommand);
            }
            else
            {
                failureReason = BattleCommandExecutionFailureReason.UnsupportedCommand;
            }

            return new BattleCommandExecutionResult(
                queuedCommand.AuthoritySequence,
                queuedCommand.Command.Type,
                queuedCommand.Command.SubmitterId,
                failureReason);
        }

        /// <summary>根据当前命令和待执行数量发布完整队列事实。</summary>
        private void PublishQueueSnapshot()
        {
            _queue.Value = new BattleCommandQueueData(
                _currentCommand.AuthoritySequence,
                _currentCommand.Command.Type,
                _currentCommand.Command.SubmitterId,
                _pendingCommands.Count,
                _isWaitingForPresentation);
        }

        /// <summary>队列内部保存权威序号与原始命令的不可见信封。</summary>
        private sealed class QueuedBattleCommand
        {
            /// <summary>本地权威调度层分配的单调序号。</summary>
            internal long AuthoritySequence { get; }

            /// <summary>该命令提交时观察到的权威轮次。</summary>
            internal int SubmittedRoundNumber { get; }

            /// <summary>等待执行的原始战斗意图。</summary>
            internal BattleCommand Command { get; }

            /// <summary>把权威序号、提交轮次与命令绑定为一个内部排队条目。</summary>
            internal QueuedBattleCommand(
                long authoritySequence,
                int submittedRoundNumber,
                BattleCommand command)
            {
                AuthoritySequence = authoritySequence;
                SubmittedRoundNumber = submittedRoundNumber;
                Command = command;
            }
        }

        /// <summary>把表现完成回调绑定到创建它的权威序号，防止过期回调推进新队首。</summary>
        private sealed class PresentationCompletion
        {
            private readonly BattleCommandQueue _owner;
            private readonly long _authoritySequence;

            /// <summary>记录队列与当前权威序号，供 adapter 回调时核对身份。</summary>
            internal PresentationCompletion(BattleCommandQueue owner, long authoritySequence)
            {
                _owner = owner;
                _authoritySequence = authoritySequence;
            }

            /// <summary>仅当绑定序号仍是当前等待项时请求队列推进。</summary>
            internal void Complete()
            {
                _owner.CompleteCurrentPresentation(_authoritySequence);
            }
        }

        /// <summary>释放队列与回合事实持有的响应式资源。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            _queue.Dispose();
            _turnController.Dispose();
        }
    }
}
