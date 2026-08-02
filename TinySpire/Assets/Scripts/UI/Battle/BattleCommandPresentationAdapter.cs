using System;
using TinySpire.Battle;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 只为携带可见结算的权威结果提供最短展示屏障，不再复制命令生命周期。
    /// </summary>
    public sealed class BattleCommandPresentationAdapter : IBattleCommandPresentation, ITickable, IDisposable
    {
        private const float DefaultPresentationDurationSeconds = 0.35f;

        private readonly float _presentationDurationSeconds;
        private readonly Func<float> _unscaledDeltaTimeProvider;
        private BattleCommandExecutionResult _currentResult;
        private Action _currentCompletion;
        private float _remainingDurationSeconds;

        /// <summary>以 BattleScene 默认时长和不受暂停影响的帧时间创建表现屏障。</summary>
        [Inject]
        public BattleCommandPresentationAdapter()
            : this(DefaultPresentationDurationSeconds, () => Time.unscaledDeltaTime)
        {
        }

        /// <summary>以可控展示时长和帧时间创建 adapter，供定向测试复用。</summary>
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

        /// <summary>保存唯一可见结果，并把 completion 延后到最短展示时间结束。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));
            if (result.Settlements.Count == 0)
            {
                throw new ArgumentException(
                    "零可见结算不得进入表现屏障。",
                    nameof(result));
            }
            if (_currentResult != null)
            {
                throw new InvalidOperationException(
                    "Battle command presentation already owns an unfinished result.");
            }

            _currentResult = result;
            _currentCompletion = onCompleted;
            _remainingDurationSeconds = _presentationDurationSeconds;
        }

        /// <summary>展示时间满足后只回报一次精确 completion，由 Queue 决定后续推进。</summary>
        public void Tick()
        {
            if (_currentResult == null)
                return;

            _remainingDurationSeconds -= Math.Max(0f, _unscaledDeltaTimeProvider.Invoke());
            if (_remainingDurationSeconds > 0f)
                return;

            Action completion = _currentCompletion;
            _currentResult = null;
            _currentCompletion = null;
            _remainingDurationSeconds = 0f;
            completion.Invoke();
        }

        /// <summary>场景销毁时停止尚未完成的展示，不再持有 Queue completion。</summary>
        public void Dispose()
        {
            _currentResult = null;
            _currentCompletion = null;
            _remainingDurationSeconds = 0f;
        }
    }
}
