using System;
using System.Collections.Generic;
using DG.Tweening;

namespace TinySpire.UI.Battle
{
    /// <summary>串行播放一个不可变表现计划，并精确拥有该命令的 completion 与 Tween。</summary>
    internal sealed class BattleCommandPresentationRunner : IDisposable
    {
        private readonly Func<BattleCommandPrelude, BattleCommandPresentationTween> _preludeTweenFactory;
        private readonly Func<BattleCommandPresentationStep, BattleCommandPresentationTween> _stepTweenFactory;
        private PendingPlayback _pending;
        private Playback _current;
        private bool _isDisposed;
        private float _speedMultiplier = 1f;

        /// <summary>以单一前奏与 settlement Tween 工厂创建命令级 runner。</summary>
        internal BattleCommandPresentationRunner(
            Func<BattleCommandPrelude, Tween> preludeTweenFactory,
            Func<BattleCommandPresentationStep, Tween> stepTweenFactory)
            : this(
                prelude => new BattleCommandPresentationTween(
                    preludeTweenFactory.Invoke(prelude),
                    cleanup: null),
                step => new BattleCommandPresentationTween(
                    stepTweenFactory.Invoke(step),
                    cleanup: null))
        {
            if (preludeTweenFactory == null)
                throw new ArgumentNullException(nameof(preludeTweenFactory));
            if (stepTweenFactory == null)
                throw new ArgumentNullException(nameof(stepTweenFactory));
        }

        /// <summary>以携带幂等清理动作的 concrete Tween lease 工厂创建 runner。</summary>
        internal BattleCommandPresentationRunner(
            Func<BattleCommandPrelude, BattleCommandPresentationTween> preludeTweenFactory,
            Func<BattleCommandPresentationStep, BattleCommandPresentationTween> stepTweenFactory)
        {
            _preludeTweenFactory = preludeTweenFactory
                ?? throw new ArgumentNullException(nameof(preludeTweenFactory));
            _stepTweenFactory = stepTweenFactory
                ?? throw new ArgumentNullException(nameof(stepTweenFactory));
        }

        /// <summary>播放一个已冻结计划；没有可见 cue 时同步完成且不创建 Tween。</summary>
        internal void Play(BattleCommandPresentationPlan plan, Action onCompleted)
        {
            Play(plan, onCompleted, canStart: null);
        }

        /// <summary>由同一 runner 在只读 readiness 满足后再构造 cue，等待期间仍唯一持有 completion。</summary>
        internal void Play(
            BattleCommandPresentationPlan plan,
            Action onCompleted,
            Func<bool> canStart)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BattleCommandPresentationRunner));
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));
            if (_current != null || _pending != null)
            {
                throw new InvalidOperationException(
                    "Battle command presentation runner already owns an unfinished plan.");
            }

            bool hasVisibleCue = plan.Prelude != null || plan.SettlementSteps.Count > 0;
            if (!hasVisibleCue)
            {
                onCompleted.Invoke();
                return;
            }

            if (canStart != null && !canStart.Invoke())
            {
                _pending = new PendingPlayback(plan, onCompleted, canStart);
                return;
            }

            StartPlayback(plan, onCompleted);
        }

        /// <summary>在 readiness 已满足后同步构造并启动唯一父时间线。</summary>
        private void StartPlayback(BattleCommandPresentationPlan plan, Action onCompleted)
        {
            var sequenceId = new object();
            Sequence sequence = DOTween.Sequence()
                .SetId(sequenceId)
                .SetAutoKill(false)
                .SetUpdate(UpdateType.Manual)
                .Pause();
            var cues = new List<BattleCommandPresentationTween>();
            var cuePositions = new List<CuePosition>();
            float timelinePosition = 0f;
            Playback playback = null;
            try
            {
                if (plan.Prelude != null)
                {
                    BattleCommandPresentationTween cue = CreatePreludeTween(plan.Prelude);
                    cues.Add(cue);
                    float endPosition = timelinePosition + cue.Tween.Duration(includeLoops: true);
                    sequence.Append(cue.Tween);
                    cuePositions.Add(new CuePosition(
                        cue,
                        timelinePosition,
                        endPosition));
                    timelinePosition = endPosition;
                }
                foreach (BattleCommandPresentationStep step in plan.SettlementSteps)
                {
                    BattleCommandPresentationTween cue = CreateStepTween(step);
                    cues.Add(cue);
                    float endPosition = timelinePosition + cue.Tween.Duration(includeLoops: true);
                    sequence.Append(cue.Tween);
                    cuePositions.Add(new CuePosition(
                        cue,
                        timelinePosition,
                        endPosition));
                    timelinePosition = endPosition;
                }

                playback = new Playback(
                    sequenceId,
                    sequence,
                    cues,
                    cuePositions,
                    onCompleted);
                _current = playback;
                sequence.OnComplete(() => Finish(playback));
                sequence.Play();
            }
            catch
            {
                if (ReferenceEquals(_current, playback))
                    _current = null;
                playback?.Cancel();
                KillOwnedSequence(sequenceId);
                CleanupCues(cues);
                throw;
            }
        }

        /// <summary>以不受 Time.timeScale 影响的帧时长推进当前 runner。</summary>
        internal void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));

            PendingPlayback pending = _pending;
            if (pending != null)
            {
                if (!pending.CanStart())
                    return;

                _pending = null;
                bool completeImmediately = pending.CompleteImmediatelyRequested;
                StartPlayback(pending.Plan, pending.TakeCompletion());
                if (completeImmediately)
                {
                    CompleteImmediately();
                    return;
                }
            }

            Playback current = _current;
            if (current == null || unscaledDeltaTime == 0f)
                return;

            float scaledDeltaTime = unscaledDeltaTime * _speedMultiplier;
            current.Sequence.ManualUpdate(scaledDeltaTime, scaledDeltaTime);
        }

        /// <summary>只调整当前及后续计划的手动时间倍率，不改变任何 cue 顺序。</summary>
        internal void SetSpeed(float speedMultiplier)
        {
            if (speedMultiplier <= 0f ||
                float.IsNaN(speedMultiplier) ||
                float.IsInfinity(speedMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
            }

            _speedMultiplier = speedMultiplier;
        }

        /// <summary>只把当前正在播放且引用精确匹配的 cue 推进到末端，旧 cue、未来 cue 与重复请求均保持无效。</summary>
        internal bool TryCompleteCue(BattleCommandPresentationTween cue)
        {
            if (cue == null)
                return false;

            Playback current = _current;
            if (current == null || !current.TryTakeActiveCueEnd(cue, out float endPosition))
                return false;

            current.Sequence.GotoWithCallbacks(endPosition, andPlay: true);
            return true;
        }

        /// <summary>立即收口当前计划；没有活动计划时保持幂等。</summary>
        internal void CompleteImmediately()
        {
            if (_pending != null)
            {
                _pending.RequestCompleteImmediately();
                return;
            }

            Playback current = _current;
            if (current == null)
                return;

            current.Sequence.Complete(withCallbacks: true);
            Finish(current);
        }

        /// <summary>丢弃 owner 仍持有的表现资源与 completion，且保持幂等。</summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            PendingPlayback pending = _pending;
            _pending = null;
            pending?.Cancel();
            Playback current = _current;
            _current = null;
            if (current == null)
                return;

            current.Cancel();
            KillOwnedSequence(current.SequenceId);
            current.CleanupCues();
        }

        /// <summary>同步构建并校验一个命令前奏 Tween。</summary>
        private BattleCommandPresentationTween CreatePreludeTween(BattleCommandPrelude prelude)
        {
            return _preludeTweenFactory.Invoke(prelude)
                ?? throw new InvalidOperationException("命令前奏 Tween lease 工厂不得返回空值。");
        }

        /// <summary>同步构建并校验一个 settlement 子步骤 Tween。</summary>
        private BattleCommandPresentationTween CreateStepTween(BattleCommandPresentationStep step)
        {
            return _stepTweenFactory.Invoke(step)
                ?? throw new InvalidOperationException("settlement Tween lease 工厂不得返回空值。");
        }

        /// <summary>先释放旧计划所有权，再精确取出并调用一次 completion。</summary>
        private void Finish(Playback playback)
        {
            if (!ReferenceEquals(_current, playback) || !playback.TryFinish())
                return;

            _current = null;
            Action completion = playback.TakeCompletion();
            KillOwnedSequence(playback.SequenceId);
            playback.CleanupCues();
            completion?.Invoke();
        }

        /// <summary>通过 runner 私有标识同步注销父时间线，兼容当前 DOTween 对未启动 Sequence 的回收语义。</summary>
        private static void KillOwnedSequence(object sequenceId)
        {
            DOTween.Kill(sequenceId, complete: false);
        }

        /// <summary>在部分构建失败时幂等清理已经创建的全部 concrete cue lease。</summary>
        private static void CleanupCues(IEnumerable<BattleCommandPresentationTween> cues)
        {
            foreach (BattleCommandPresentationTween cue in cues)
                cue.Cleanup();
        }

        /// <summary>在启动 View 就绪前由 runner 唯一持有的冻结计划、completion 与立即完成请求。</summary>
        private sealed class PendingPlayback
        {
            private Action _completion;
            private readonly Func<bool> _canStart;

            /// <summary>等待 readiness 的不可变表现计划。</summary>
            public BattleCommandPresentationPlan Plan { get; }

            /// <summary>是否已领取一次“就绪后立即完成”请求。</summary>
            public bool CompleteImmediatelyRequested { get; private set; }

            /// <summary>冻结等待启动的计划与唯一 completion，不创建 Tween 或第二排序根。</summary>
            public PendingPlayback(
                BattleCommandPresentationPlan plan,
                Action completion,
                Func<bool> canStart)
            {
                Plan = plan ?? throw new ArgumentNullException(nameof(plan));
                _completion = completion ?? throw new ArgumentNullException(nameof(completion));
                _canStart = canStart ?? throw new ArgumentNullException(nameof(canStart));
            }

            /// <summary>即时读取当前 View readiness，不缓存战斗事实。</summary>
            public bool CanStart()
            {
                return _canStart.Invoke();
            }

            /// <summary>记录一次幂等请求，待 readiness 满足后由活动父时间线执行全部 cue。</summary>
            public void RequestCompleteImmediately()
            {
                CompleteImmediatelyRequested = true;
            }

            /// <summary>把唯一 completion 移交给即将启动的活动时间线。</summary>
            public Action TakeCompletion()
            {
                Action completion = _completion;
                _completion = null;
                return completion;
            }

            /// <summary>owner 销毁时丢弃尚未移交的 completion。</summary>
            public void Cancel()
            {
                _completion = null;
            }
        }

        /// <summary>单条计划的 Tween、completion 与幂等完成门闩。</summary>
        private sealed class CuePosition
        {
            /// <summary>父时间线中该 cue 的精确 lease 引用。</summary>
            public BattleCommandPresentationTween Cue { get; }

            /// <summary>父时间线中该 cue 的起始位置。</summary>
            public float Start { get; }

            /// <summary>父时间线中该 cue 的结束位置。</summary>
            public float End { get; }

            /// <summary>冻结一个 cue 在父时间线中的半开区间。</summary>
            public CuePosition(BattleCommandPresentationTween cue, float start, float end)
            {
                Cue = cue ?? throw new ArgumentNullException(nameof(cue));
                Start = start;
                End = end;
            }
        }

        /// <summary>单条计划的 Tween、completion 与幂等完成门闩。</summary>
        private sealed class Playback
        {
            private Action _completion;
            private bool _isFinished;
            private readonly IReadOnlyList<BattleCommandPresentationTween> _cues;
            private readonly IReadOnlyList<CuePosition> _cuePositions;
            private readonly HashSet<BattleCommandPresentationTween> _fastForwardedCues = new();

            /// <summary>只用于精确注销本次命令父时间线的私有 DOTween 标识。</summary>
            public object SequenceId { get; }

            /// <summary>该计划唯一拥有的父时间线。</summary>
            public Sequence Sequence { get; }

            /// <summary>冻结一条活动计划的父时间线与 Queue completion。</summary>
            public Playback(
                object sequenceId,
                Sequence sequence,
                IReadOnlyList<BattleCommandPresentationTween> cues,
                IReadOnlyList<CuePosition> cuePositions,
                Action completion)
            {
                SequenceId = sequenceId ?? throw new ArgumentNullException(nameof(sequenceId));
                Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
                _cues = cues ?? throw new ArgumentNullException(nameof(cues));
                _cuePositions = cuePositions ?? throw new ArgumentNullException(nameof(cuePositions));
                _completion = completion ?? throw new ArgumentNullException(nameof(completion));
            }

            /// <summary>在当前父时间位置精确领取一次指定 cue 的结束位置。</summary>
            public bool TryTakeActiveCueEnd(
                BattleCommandPresentationTween cue,
                out float endPosition)
            {
                endPosition = 0f;
                if (_isFinished || _fastForwardedCues.Contains(cue))
                    return false;

                float currentPosition = Sequence.Elapsed(includeLoops: false);
                foreach (CuePosition position in _cuePositions)
                {
                    if (!ReferenceEquals(position.Cue, cue))
                        continue;
                    if (position.End <= position.Start ||
                        currentPosition <= position.Start ||
                        currentPosition >= position.End)
                    {
                        return false;
                    }

                    _fastForwardedCues.Add(cue);
                    endPosition = position.End;
                    return true;
                }

                return false;
            }

            /// <summary>只允许自然完成、立即完成或重复回调中的首个调用取得完成权。</summary>
            public bool TryFinish()
            {
                if (_isFinished)
                    return false;

                _isFinished = true;
                return true;
            }

            /// <summary>精确取出并清空 completion，避免旧 Scope 继续持有 Queue。</summary>
            public Action TakeCompletion()
            {
                Action completion = _completion;
                _completion = null;
                return completion;
            }

            /// <summary>取消活动计划并丢弃 completion，不把 owner 销毁伪装成正常完成。</summary>
            public void Cancel()
            {
                _isFinished = true;
                _completion = null;
            }

            /// <summary>幂等收口该计划创建的所有 transient cue 资源。</summary>
            public void CleanupCues()
            {
                BattleCommandPresentationRunner.CleanupCues(_cues);
            }
        }
    }

    /// <summary>把一个 cue 的 Tween 与取消/完成都必须执行的幂等清理绑定为 concrete lease。</summary>
    internal sealed class BattleCommandPresentationTween
    {
        private Action _cleanup;

        /// <summary>由命令级父 Sequence 串行拥有的子 Tween。</summary>
        public Tween Tween { get; }

        /// <summary>冻结一个子 Tween 与其可丢弃资源的幂等清理动作。</summary>
        internal BattleCommandPresentationTween(Tween tween, Action cleanup)
        {
            Tween = tween ?? throw new ArgumentNullException(nameof(tween));
            _cleanup = cleanup;
        }

        /// <summary>精确取出并执行一次 cleanup；没有资源时保持幂等。</summary>
        internal void Cleanup()
        {
            Action cleanup = _cleanup;
            _cleanup = null;
            cleanup?.Invoke();
        }
    }
}
