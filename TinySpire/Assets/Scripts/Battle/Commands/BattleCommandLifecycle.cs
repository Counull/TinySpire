using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using R3;

namespace TinySpire.Battle
{
    /// <summary>Queue 在接受判断前签发的引用身份；它不暴露或替代权威序号。</summary>
    public sealed class BattleCommandHandle
    {
        /// <summary>只允许统一提交协调器签发不透明句柄。</summary>
        internal BattleCommandHandle()
        {
        }
    }

    /// <summary>一条已接受命令对展示与 pending 对账公开的稳定生命周期阶段。</summary>
    public enum BattleCommandLifecycleStage
    {
        Queued,
        ExecutionFailed,
        ExecutionCompleted,
        Faulted,
    }

    /// <summary>Queue 生命周期中一次不可变反馈；fault 元数据不会混入战斗结算。</summary>
    public sealed class BattleCommandLifecycleEvent
    {
        /// <summary>Queue 为该命令签发、用于生命周期对账的同一不透明句柄。</summary>
        public BattleCommandHandle Handle { get; }

        /// <summary>Queue 接受命令时分配的权威序号。</summary>
        public long AuthoritySequence { get; }

        /// <summary>反馈对应的原始不可变命令；调用方可据此在 Queued 阶段建立精确 pending。</summary>
        public BattleCommand Command { get; }

        /// <summary>从原始命令派生反馈对应的稳定命令类型。</summary>
        public BattleCommandType CommandType => Command.Type;

        /// <summary>从原始命令派生提交者；系统命令为空。</summary>
        public CombatantId? SubmitterId => Command.SubmitterId;

        /// <summary>本条反馈的稳定生命周期阶段。</summary>
        public BattleCommandLifecycleStage Stage { get; }

        /// <summary>普通执行失败原因；非普通失败阶段为 None。</summary>
        public BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>成功执行携带的冻结战斗结算；Queued、普通失败与 fault 均为空。</summary>
        public IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>Queue fault 诊断事实；只有 Faulted 阶段非空。</summary>
        public BattleCommandQueueFaultData Fault { get; }

        /// <summary>冻结一条已经校验阶段组合的生命周期反馈。</summary>
        private BattleCommandLifecycleEvent(
            BattleCommandHandle handle,
            long authoritySequence,
            BattleCommand command,
            BattleCommandLifecycleStage stage,
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements,
            BattleCommandQueueFaultData fault)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));
            if (authoritySequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(authoritySequence));
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            Handle = handle;
            AuthoritySequence = authoritySequence;
            Command = command;
            Stage = stage;
            FailureReason = failureReason;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
            Fault = fault;
        }

        /// <summary>为刚由 Queue 接受的命令创建唯一 Queued 反馈。</summary>
        internal static BattleCommandLifecycleEvent Queued(
            BattleCommandHandle handle,
            long authoritySequence,
            BattleCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            return new BattleCommandLifecycleEvent(
                handle,
                authoritySequence,
                command,
                BattleCommandLifecycleStage.Queued,
                BattleCommandExecutionFailureReason.None,
                Array.Empty<BattleSettlementRecord>(),
                fault: null);
        }

        /// <summary>从一次普通执行结果创建完成或失败生命周期。</summary>
        internal static BattleCommandLifecycleEvent FromExecution(
            BattleCommandHandle handle,
            long authoritySequence,
            BattleCommand command,
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            var copiedSettlements = new List<BattleSettlementRecord>(settlements);
            if (failureReason != BattleCommandExecutionFailureReason.None && copiedSettlements.Count != 0)
            {
                throw new ArgumentException(
                    "普通失败的 battle settlement 必须为空。",
                    nameof(settlements));
            }

            return new BattleCommandLifecycleEvent(
                handle,
                authoritySequence,
                command,
                failureReason == BattleCommandExecutionFailureReason.None
                    ? BattleCommandLifecycleStage.ExecutionCompleted
                    : BattleCommandLifecycleStage.ExecutionFailed,
                failureReason,
                copiedSettlements,
                fault: null);
        }

        /// <summary>从独立 Queue fault 事实创建不携带 battle settlement 的 Faulted 生命周期。</summary>
        internal static BattleCommandLifecycleEvent Faulted(
            BattleCommandHandle handle,
            BattleCommand command,
            BattleCommandQueueFaultData fault)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (fault == null)
                throw new ArgumentNullException(nameof(fault));

            return new BattleCommandLifecycleEvent(
                handle,
                fault.AuthoritySequence,
                command,
                BattleCommandLifecycleStage.Faulted,
                BattleCommandExecutionFailureReason.None,
                Array.Empty<BattleSettlementRecord>(),
                fault);
        }
    }

    /// <summary>只管理 Queue 内部接受前的句柄注册与后续生命周期对账，不分配权威序号。</summary>
    public sealed class BattleCommandSubmissionCoordinator : IDisposable
    {
        private readonly Dictionary<BattleCommandHandle, Registration> _pending =
            new Dictionary<BattleCommandHandle, Registration>();
        private readonly Dictionary<BattleCommand, BattleCommandHandle> _unboundHandlesByCommand =
            new Dictionary<BattleCommand, BattleCommandHandle>(BattleCommandReferenceComparer.Instance);
        private readonly Subject<BattleCommandLifecycleEvent> _lifecycle =
            new Subject<BattleCommandLifecycleEvent>();

        /// <summary>只读发布由 Queue 形成并完成对账的唯一命令生命周期。</summary>
        internal Observable<BattleCommandLifecycleEvent> Lifecycle => _lifecycle;

        /// <summary>只允许 Queue implementation 在接受判断前为具体命令注册唯一不透明句柄。</summary>
        internal BattleCommandHandle PreRegister(BattleCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (_unboundHandlesByCommand.ContainsKey(command))
            {
                throw new InvalidOperationException(
                    "同一命令引用只能存在一个尚未提交的预注册句柄。");
            }

            var handle = new BattleCommandHandle();
            _pending.Add(handle, new Registration(command));
            _unboundHandlesByCommand.Add(command, handle);
            return handle;
        }

        /// <summary>判断指定句柄是否仍等待拒绝、完成、失败或 fault 对账。</summary>
        internal bool IsPending(BattleCommandHandle handle)
        {
            return handle != null && _pending.ContainsKey(handle);
        }

        /// <summary>校验句柄确由本协调器为同一命令预注册。</summary>
        internal bool Matches(BattleCommandHandle handle, BattleCommand command)
        {
            return handle != null &&
                   command != null &&
                   _pending.TryGetValue(handle, out Registration registration) &&
                   ReferenceEquals(registration.Command, command) &&
                   !registration.AuthoritySequence.HasValue;
        }

        /// <summary>在 Queue 分配序号后把预注册句柄绑定到唯一 accepted 命令。</summary>
        internal bool BindQueued(
            BattleCommandHandle handle,
            BattleCommand command,
            long authoritySequence)
        {
            if (!Matches(handle, command) || authoritySequence <= 0)
                return false;

            _pending[handle].AuthoritySequence = authoritySequence;
            _unboundHandlesByCommand.Remove(command);
            return true;
        }

        /// <summary>在命令未被 Queue 接受时撤销其预注册句柄。</summary>
        internal bool Cancel(BattleCommandHandle handle)
        {
            if (handle == null ||
                !_pending.TryGetValue(handle, out Registration registration) ||
                registration.AuthoritySequence.HasValue)
            {
                return false;
            }

            _unboundHandlesByCommand.Remove(registration.Command);
            return _pending.Remove(handle);
        }

        /// <summary>按 handle 与序号对账生命周期；旧终态不能影响其他新句柄。</summary>
        internal bool Reconcile(BattleCommandLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent == null)
                throw new ArgumentNullException(nameof(lifecycleEvent));
            if (!_pending.TryGetValue(lifecycleEvent.Handle, out Registration registration) ||
                registration.AuthoritySequence != lifecycleEvent.AuthoritySequence ||
                !ReferenceEquals(registration.Command, lifecycleEvent.Command))
            {
                return false;
            }

            if (lifecycleEvent.Stage == BattleCommandLifecycleStage.Queued)
                return true;

            return _pending.Remove(lifecycleEvent.Handle);
        }

        /// <summary>仅允许 Queue 在句柄、序号与命令语义对账成功后发布生命周期。</summary>
        internal void PublishFromQueue(BattleCommandLifecycleEvent lifecycleEvent)
        {
            if (!Reconcile(lifecycleEvent))
            {
                throw new InvalidOperationException(
                    "Queue 尝试发布无法与预注册句柄对账的命令生命周期。");
            }

            _lifecycle.OnNext(lifecycleEvent);
        }

        /// <summary>释放本场战斗的生命周期流与尚未完成的句柄登记。</summary>
        public void Dispose()
        {
            _pending.Clear();
            _unboundHandlesByCommand.Clear();
            _lifecycle.Dispose();
        }

        /// <summary>协调器内部保存的命令引用与已绑定序号。</summary>
        private sealed class Registration
        {
            /// <summary>保存一次尚待终态对账的提交注册。</summary>
            internal Registration(BattleCommand command)
            {
                Command = command;
            }

            internal BattleCommand Command { get; }
            internal long? AuthoritySequence { get; set; }
        }

        /// <summary>让命令反向索引严格按对象引用而不是潜在值相等语义比较。</summary>
        private sealed class BattleCommandReferenceComparer : IEqualityComparer<BattleCommand>
        {
            internal static BattleCommandReferenceComparer Instance { get; } =
                new BattleCommandReferenceComparer();

            /// <summary>比较两个命令是否为同一对象引用。</summary>
            public bool Equals(BattleCommand left, BattleCommand right)
            {
                return ReferenceEquals(left, right);
            }

            /// <summary>返回不受值相等重载影响的对象引用哈希。</summary>
            public int GetHashCode(BattleCommand command)
            {
                if (command == null)
                    throw new ArgumentNullException(nameof(command));

                return RuntimeHelpers.GetHashCode(command);
            }
        }
    }
}
