using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>Queue 为一个内部 continuation 签发且只允许消费一次的能力。</summary>
    internal sealed class BattleSystemCommandToken
    {
        private readonly BattleCommandSchedulingCore _owner;
        private bool _isConsumed;

        /// <summary>该签发能力是否已经由所属 Queue 调度核心消费。</summary>
        internal bool IsConsumed => _isConsumed;

        /// <summary>把 token 绑定到签发它的唯一调度核心。</summary>
        internal BattleSystemCommandToken(BattleCommandSchedulingCore owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>仅允许签发者成功消费一次。</summary>
        internal bool TryConsume(BattleCommandSchedulingCore owner)
        {
            if (!ReferenceEquals(_owner, owner) || _isConsumed)
                return false;

            _isConsumed = true;
            return true;
        }
    }

    /// <summary>一次预注册命令进入纯调度核心后的不可变接受结果。</summary>
    internal sealed class BattleCommandSchedulingAcceptance
    {
        /// <summary>对外保持既有 Queue Submit 接受语义。</summary>
        public BattleCommandSubmissionResult Submission { get; }

        /// <summary>接受时与序号同时形成的唯一 Queued 生命周期；拒绝时为空。</summary>
        public BattleCommandLifecycleEvent QueuedLifecycle { get; }

        /// <summary>冻结一次接受或拒绝结果。</summary>
        internal BattleCommandSchedulingAcceptance(
            BattleCommandSubmissionResult submission,
            BattleCommandLifecycleEvent queuedLifecycle)
        {
            Submission = submission;
            QueuedLifecycle = queuedLifecycle;
        }
    }

    /// <summary>一次已接受命令在纯调度核心中的不可变公开信封。</summary>
    internal sealed class BattleCommandSchedulingEntry
    {
        private readonly BattleCommandSchedulingCore _owner;
        private readonly BattleSystemCommandToken _systemToken;

        /// <summary>Queue 接受此命令时分配的权威序号。</summary>
        public long AuthoritySequence { get; }

        /// <summary>此命令在提交前取得的生命周期对账句柄。</summary>
        public BattleCommandHandle Handle { get; }

        /// <summary>尚未执行的稳定语义命令。</summary>
        public BattleCommand Command { get; }

        /// <summary>玩家命令接受时的轮次栅栏；系统命令仍保留快照但不冒用它校验。</summary>
        public int SubmittedRoundNumber { get; }

        /// <summary>该项是否必须消费 Queue 签发的一次性系统 token。</summary>
        public bool RequiresSystemToken => _systemToken != null;

        /// <summary>内部 continuation 的一次性 Queue 能力是否已在出队时消费。</summary>
        internal bool IsSystemTokenConsumed => _systemToken?.IsConsumed == true;

        /// <summary>冻结一次已接受的外部命令或内部 continuation。</summary>
        internal BattleCommandSchedulingEntry(
            BattleCommandSchedulingCore owner,
            long authoritySequence,
            BattleCommandHandle handle,
            BattleCommand command,
            int submittedRoundNumber,
            BattleSystemCommandToken systemToken)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            AuthoritySequence = authoritySequence;
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            SubmittedRoundNumber = submittedRoundNumber;
            _systemToken = systemToken;
        }

        /// <summary>仅允许所属调度核心把内部 token 消费一次。</summary>
        internal bool TryConsumeSystemToken(BattleCommandSchedulingCore owner)
        {
            if (_systemToken == null ||
                !ReferenceEquals(_owner, owner))
            {
                return false;
            }

            return _systemToken.TryConsume(owner);
        }
    }

    /// <summary>一次当前命令执行返回后冻结的调度结果。</summary>
    internal sealed class BattleCommandSchedulingCompletion
    {
        /// <summary>当前命令等待发布的普通完成或失败生命周期。</summary>
        public BattleCommandLifecycleEvent CurrentLifecycle { get; }

        /// <summary>在当前命令进入表现前已经分配并形成的 continuation Queued；没有后继时为空。</summary>
        public BattleCommandLifecycleEvent ContinuationQueuedLifecycle { get; }

        /// <summary>冻结一次执行返回后的生命周期与 continuation 结果。</summary>
        internal BattleCommandSchedulingCompletion(
            BattleCommandLifecycleEvent currentLifecycle,
            BattleCommandLifecycleEvent continuationQueuedLifecycle)
        {
            CurrentLifecycle = currentLifecycle ?? throw new ArgumentNullException(nameof(currentLifecycle));
            ContinuationQueuedLifecycle = continuationQueuedLifecycle;
        }
    }

    /// <summary>
    /// 由生产 BattleCommandQueue 唯一持有的纯 C# 调度核心；M8A 只锁定协议，不接现有生产写链。
    /// </summary>
    internal sealed class BattleCommandSchedulingCore
    {
        private readonly BattleCommandSubmissionCoordinator _coordinator;
        private readonly Queue<BattleCommandSchedulingEntry> _pending =
            new Queue<BattleCommandSchedulingEntry>();
        private long _nextAuthoritySequence = 1;
        private bool _drainEntered;
        private bool _waitingForPresentation;
        private BattleCommandSchedulingEntry _current;
        private BattleCommandQueueFaultData _fault;

        /// <summary>尚未成为当前项的 accepted 命令数量。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>当前执行或等待表现的权威序号；空闲时为空。</summary>
        public long? CurrentAuthoritySequence => _current?.AuthoritySequence;

        /// <summary>冻结 drain 的唯一 fault 事实；正常时为空。</summary>
        public BattleCommandQueueFaultData Fault => _fault;

        /// <summary>从内部 current、pending、barrier 与 fault 一次派生只读 Queue 快照。</summary>
        public BattleCommandQueueData CreateQueueSnapshot()
        {
            return new BattleCommandQueueData(
                _current?.AuthoritySequence,
                _current?.Command.Type,
                _current?.Command.SubmitterId,
                _pending.Count,
                _waitingForPresentation,
                _fault);
        }

        /// <summary>绑定唯一提交协调器，后续接受必须消费其预注册句柄。</summary>
        public BattleCommandSchedulingCore(BattleCommandSubmissionCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        /// <summary>接受已预注册的命令、分配序号并同时形成 Queued 生命周期。</summary>
        public BattleCommandSchedulingAcceptance AcceptPreRegistered(
            BattleCommandHandle handle,
            BattleCommand command,
            int submittedRoundNumber)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (submittedRoundNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(submittedRoundNumber));
            if (_fault != null)
            {
                _coordinator.Cancel(handle);
                return Rejected(BattleCommandSubmissionFailureReason.QueueFaulted);
            }
            if (!_coordinator.Matches(handle, command))
            {
                _coordinator.Cancel(handle);
                return Rejected(BattleCommandSubmissionFailureReason.InvalidSubmissionHandle);
            }
            if (command.Type == BattleCommandType.CompleteEnemyAction)
            {
                _coordinator.Cancel(handle);
                return Rejected(BattleCommandSubmissionFailureReason.SystemCommandNotAuthorized);
            }

            return AcceptRegistered(
                handle,
                command,
                submittedRoundNumber,
                systemToken: null);
        }

        /// <summary>撤销尚未接受的预注册句柄，并返回不分配序号的拒绝结果。</summary>
        public BattleCommandSubmissionResult RejectPreRegistered(
            BattleCommandHandle handle,
            BattleCommandSubmissionFailureReason reason)
        {
            if (reason == BattleCommandSubmissionFailureReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));

            if (!_coordinator.Cancel(handle))
                return BattleCommandSubmissionResult.Rejected(
                    BattleCommandSubmissionFailureReason.InvalidSubmissionHandle);

            return BattleCommandSubmissionResult.Rejected(reason);
        }

        /// <summary>尝试进入唯一迭代 drain；执行、回调、表现或 fault 期间的重入均失败。</summary>
        public bool TryEnterDrain()
        {
            if (_drainEntered || _fault != null)
                return false;

            _drainEntered = true;
            return true;
        }

        /// <summary>在外层 drain 内取得下一项；表现屏障、当前执行或 fault 会阻止推进。</summary>
        public bool TryBeginNext(out BattleCommandSchedulingEntry entry)
        {
            entry = null;
            if (!_drainEntered ||
                _fault != null ||
                _waitingForPresentation ||
                _current != null ||
                _pending.Count == 0)
            {
                return false;
            }

            BattleCommandSchedulingEntry next = _pending.Peek();
            if (next.RequiresSystemToken && !next.TryConsumeSystemToken(this))
                throw new InvalidOperationException("Queue 签发的系统命令 token 无效或已被消费。");

            _current = _pending.Dequeue();
            entry = _current;
            return true;
        }

        /// <summary>
        /// 在 Execute 返回后先为 continuation 分配序号并追加到 FIFO，再建立当前终态与可选表现屏障。
        /// </summary>
        public BattleCommandSchedulingCompletion CompleteCurrent(
            BattleCommandSchedulingEntry entry,
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements,
            BattleCommand continuationCommand,
            int continuationSubmittedRoundNumber)
        {
            ValidateCurrent(entry);
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (continuationSubmittedRoundNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(continuationSubmittedRoundNumber));
            if (failureReason != BattleCommandExecutionFailureReason.None && continuationCommand != null)
            {
                throw new ArgumentException("普通失败不能生成 continuation。", nameof(continuationCommand));
            }
            if (continuationCommand != null &&
                continuationCommand.Type != BattleCommandType.CompleteEnemyAction)
            {
                throw new ArgumentException(
                    "只有 Queue 生成的 CompleteEnemyAction 可以作为系统 continuation。",
                    nameof(continuationCommand));
            }
            if (entry.RequiresSystemToken && !entry.IsSystemTokenConsumed)
                throw new InvalidOperationException("系统 continuation 尚未消费 Queue 签发的 token。");

            BattleCommandLifecycleEvent currentLifecycle =
                BattleCommandLifecycleEvent.FromExecution(
                    entry.Handle,
                    entry.AuthoritySequence,
                    entry.Command,
                    failureReason,
                    settlements);
            BattleCommandLifecycleEvent continuationQueued = null;
            if (continuationCommand != null)
            {
                BattleCommandHandle continuationHandle = _coordinator.PreRegister(continuationCommand);
                var systemToken = new BattleSystemCommandToken(this);
                BattleCommandSchedulingAcceptance acceptance = AcceptRegistered(
                    continuationHandle,
                    continuationCommand,
                    continuationSubmittedRoundNumber,
                    systemToken);
                if (!acceptance.Submission.Accepted || acceptance.QueuedLifecycle == null)
                {
                    throw new InvalidOperationException(
                        "Queue 内部 continuation 必须通过唯一注册路径成功进入权威顺序。");
                }

                continuationQueued = acceptance.QueuedLifecycle;
            }

            if (currentLifecycle.Settlements.Count > 0)
            {
                _waitingForPresentation = true;
            }
            else
            {
                _current = null;
            }

            return new BattleCommandSchedulingCompletion(currentLifecycle, continuationQueued);
        }

        /// <summary>只允许当前精确序号的一次 completion 解除表现屏障。</summary>
        public bool CompletePresentation(long authoritySequence)
        {
            if (_fault != null ||
                !_waitingForPresentation ||
                _current == null ||
                _current.AuthoritySequence != authoritySequence)
            {
                return false;
            }

            _waitingForPresentation = false;
            _current = null;
            return true;
        }

        /// <summary>冻结当前 drain，并保留 current、pending 与独立 fault 生命周期供诊断。</summary>
        public BattleCommandLifecycleEvent FaultCurrent(
            BattleCommandSchedulingEntry entry,
            BattleCommandQueueFaultReason reason,
            bool mayHavePartialWrites)
        {
            ValidateFaultableCurrent(entry);
            if (reason != BattleCommandQueueFaultReason.UnexpectedException && mayHavePartialWrites)
            {
                throw new ArgumentException(
                    "确定性 Queue fault 必须发生在首次权威写入前。",
                    nameof(mayHavePartialWrites));
            }
            if (_waitingForPresentation &&
                (reason != BattleCommandQueueFaultReason.UnexpectedException || !mayHavePartialWrites))
            {
                throw new ArgumentException(
                    "权威提交后的表现阶段只能进入明确标记部分写入的不可预期 fault。",
                    nameof(reason));
            }

            _fault = new BattleCommandQueueFaultData(
                entry.AuthoritySequence,
                entry.Command.Type,
                reason,
                mayHavePartialWrites);
            return BattleCommandLifecycleEvent.Faulted(
                entry.Handle,
                entry.Command,
                _fault);
        }

        /// <summary>退出外层迭代 drain；fault 或等待表现时允许保留当前项。</summary>
        public void ExitDrain()
        {
            if (!_drainEntered)
                throw new InvalidOperationException("当前没有进入 drain。");
            if (_current != null && !_waitingForPresentation && _fault == null)
                throw new InvalidOperationException("当前命令仍在执行，不能退出 drain。");

            _drainEntered = false;
        }

        /// <summary>接受已由本核心确认的外部命令或内部 continuation。</summary>
        private BattleCommandSchedulingAcceptance AcceptRegistered(
            BattleCommandHandle handle,
            BattleCommand command,
            int submittedRoundNumber,
            BattleSystemCommandToken systemToken)
        {
            long authoritySequence = _nextAuthoritySequence;
            if (!_coordinator.BindQueued(handle, command, authoritySequence))
                return Rejected(BattleCommandSubmissionFailureReason.InvalidSubmissionHandle);

            _nextAuthoritySequence++;
            BattleCommandLifecycleEvent queued = BattleCommandLifecycleEvent.Queued(
                handle,
                authoritySequence,
                command);
            _pending.Enqueue(new BattleCommandSchedulingEntry(
                this,
                authoritySequence,
                handle,
                command,
                submittedRoundNumber,
                systemToken));
            return new BattleCommandSchedulingAcceptance(
                BattleCommandSubmissionResult.AcceptedWith(authoritySequence),
                queued);
        }

        /// <summary>创建不携带 Queued 生命周期的拒绝调度结果。</summary>
        private static BattleCommandSchedulingAcceptance Rejected(
            BattleCommandSubmissionFailureReason reason)
        {
            return new BattleCommandSchedulingAcceptance(
                BattleCommandSubmissionResult.Rejected(reason),
                queuedLifecycle: null);
        }

        /// <summary>校验调用方正在结束或 fault 当前精确信封。</summary>
        private void ValidateCurrent(BattleCommandSchedulingEntry entry)
        {
            if (!_drainEntered ||
                entry == null ||
                !ReferenceEquals(_current, entry) ||
                _waitingForPresentation ||
                _fault != null)
            {
                throw new InvalidOperationException("只有外层 drain 正在执行的当前命令可以结束。");
            }
        }

        /// <summary>校验执行期或提交后表现期仍由当前 drain 持有的精确信封。</summary>
        private void ValidateFaultableCurrent(BattleCommandSchedulingEntry entry)
        {
            if (!_drainEntered ||
                entry == null ||
                !ReferenceEquals(_current, entry) ||
                _fault != null)
            {
                throw new InvalidOperationException("只有当前执行或等待表现的命令可以令 Queue fault。");
            }
        }
    }
}
