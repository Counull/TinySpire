using System;
using System.Collections.Generic;
using R3;
using TinySpire.Battle;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 命令在当前 BattleScene 展示层中的三个可观察阶段。
    /// </summary>
    public enum BattleCommandFeedbackStage
    {
        Queued,
        ExecutionFailed,
        ExecutionCompleted
    }

    /// <summary>
    /// 只描述一次命令展示反馈，不复制能量、卡区或回合事实。
    /// </summary>
    public sealed class BattleCommandFeedback
    {
        /// <summary>该反馈对应的权威命令序号。</summary>
        public long AuthoritySequence { get; }

        /// <summary>该反馈对应的命令类型。</summary>
        public BattleCommandType CommandType { get; }

        /// <summary>该反馈对应的命令提交者。</summary>
        public CombatantId? SubmitterId { get; }

        /// <summary>当前展示反馈阶段。</summary>
        public BattleCommandFeedbackStage Stage { get; }

        /// <summary>执行失败原因；非失败反馈时为 None。</summary>
        public BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>权威执行结果携带的冻结结算记录；排队阶段为空。</summary>
        public IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>创建一条不可变的命令展示反馈。</summary>
        private BattleCommandFeedback(
            long authoritySequence,
            BattleCommandType commandType,
            CombatantId? submitterId,
            BattleCommandFeedbackStage stage,
            BattleCommandExecutionFailureReason failureReason,
            IReadOnlyList<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            AuthoritySequence = authoritySequence;
            CommandType = commandType;
            SubmitterId = submitterId;
            Stage = stage;
            FailureReason = failureReason;
            Settlements = settlements;
        }

        /// <summary>从已经接受的提交创建“已排队”反馈。</summary>
        internal static BattleCommandFeedback FromQueued(
            BattleCommand command,
            long authoritySequence)
        {
            return new BattleCommandFeedback(
                authoritySequence,
                command.Type,
                command.SubmitterId,
                BattleCommandFeedbackStage.Queued,
                BattleCommandExecutionFailureReason.None,
                Array.Empty<BattleSettlementRecord>());
        }

        /// <summary>从权威执行结果创建“执行失败”或“执行完成”反馈。</summary>
        internal static BattleCommandFeedback FromExecution(BattleCommandExecutionResult result)
        {
            return new BattleCommandFeedback(
                result.AuthoritySequence,
                result.CommandType,
                result.SubmitterId,
                result.Succeeded
                    ? BattleCommandFeedbackStage.ExecutionCompleted
                    : BattleCommandFeedbackStage.ExecutionFailed,
                result.FailureReason,
                result.Settlements);
        }
    }

    /// <summary>
    /// 将权威执行结果转换为 BattleScene 可订阅反馈，并在最短展示时间后确认队列继续。
    /// </summary>
    public sealed class BattleCommandPresentationAdapter : IBattleCommandPresentation, ITickable, IDisposable
    {
        private const float DefaultPresentationDurationSeconds = 0.35f;

        private readonly float _presentationDurationSeconds;
        private readonly Func<float> _unscaledDeltaTimeProvider;
        private readonly Subject<BattleCommandFeedback> _feedback = new Subject<BattleCommandFeedback>();
        private BattleCommandExecutionResult _currentResult;
        private Action _currentCompletion;
        private float _remainingDurationSeconds;
        private bool _hasPublishedCurrentResult;

        /// <summary>按发生顺序发布排队、执行失败和执行完成反馈。</summary>
        public Observable<BattleCommandFeedback> Feedback => _feedback;

        /// <summary>以 BattleScene 默认展示时长和不受暂停影响的帧时间创建生产 adapter。</summary>
        [Inject]
        public BattleCommandPresentationAdapter()
            : this(DefaultPresentationDurationSeconds, () => Time.unscaledDeltaTime)
        {
        }

        /// <summary>以可控展示时长和帧时间创建 adapter，供定向测试复用同一行为。</summary>
        public BattleCommandPresentationAdapter(
            float presentationDurationSeconds,
            Func<float> unscaledDeltaTimeProvider)
        {
            if (presentationDurationSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(presentationDurationSeconds));

            _presentationDurationSeconds = presentationDurationSeconds;
            _unscaledDeltaTimeProvider = unscaledDeltaTimeProvider
                ?? throw new ArgumentNullException(nameof(unscaledDeltaTimeProvider));
        }

        /// <summary>在 UI 收到接受结果后发布“已排队”，但不把接受误当成执行成功。</summary>
        public void PublishQueued(
            BattleCommand command,
            BattleCommandSubmissionResult submission)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (!submission.Accepted || !submission.AuthoritySequence.HasValue)
            {
                throw new ArgumentException(
                    "Only an accepted command submission can publish queued feedback.",
                    nameof(submission));
            }

            _feedback.OnNext(BattleCommandFeedback.FromQueued(
                command,
                submission.AuthoritySequence.Value));
        }

        /// <summary>保存当前权威执行结果，并把完成回调延后到展示时间结束。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));
            if (_currentResult != null)
            {
                throw new InvalidOperationException(
                    "Battle command presentation already owns an unfinished result.");
            }

            _currentResult = result;
            _currentCompletion = onCompleted;
            _remainingDurationSeconds = _presentationDurationSeconds;
            _hasPublishedCurrentResult = false;
        }

        /// <summary>先发布权威执行结果，再于最短展示时间结束时允许队列推进。</summary>
        public void Tick()
        {
            if (_currentResult == null)
                return;

            if (!_hasPublishedCurrentResult)
            {
                _feedback.OnNext(BattleCommandFeedback.FromExecution(_currentResult));
                _hasPublishedCurrentResult = true;
            }

            _remainingDurationSeconds -= Math.Max(0f, _unscaledDeltaTimeProvider.Invoke());
            if (_remainingDurationSeconds > 0f)
                return;

            Action completion = _currentCompletion;
            _currentResult = null;
            _currentCompletion = null;
            _remainingDurationSeconds = 0f;
            _hasPublishedCurrentResult = false;
            completion.Invoke();
        }

        /// <summary>场景销毁时停止尚未完成的展示并释放反馈流。</summary>
        public void Dispose()
        {
            _currentResult = null;
            _currentCompletion = null;
            _feedback.Dispose();
        }
    }
}
