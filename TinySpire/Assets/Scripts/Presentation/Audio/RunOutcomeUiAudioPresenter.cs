using System;
using R3;
using TinySpire.Run;
using VContainer.Unity;

namespace TinySpire.Presentation.Audio
{
    /// <summary>全局观察唯一 Run 与存档状态边沿，为终局和保存失败提供音频反馈。</summary>
    public sealed class RunOutcomeUiAudioPresenter : IInitializable, IDisposable
    {
        private readonly RunStateStore _store;
        private readonly RunFlowService _flow;
        private readonly IUiAudioPlayer _player;

        private IDisposable _stateSubscription;
        private bool _wasTerminal;
        private bool _wasCommitFailed;
        private bool _initialized;
        private bool _disposed;

        /// <summary>接收全局 Run owners 与唯一音频播放端口。</summary>
        public RunOutcomeUiAudioPresenter(
            RunStateStore store,
            RunFlowService flow,
            IUiAudioPlayer player)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>一次性记录当前基线并订阅 RunState 与 Persistence 变化。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;

            _initialized = true;
            _wasTerminal = IsTerminal(_store.Current);
            _wasCommitFailed = IsCommitFailed(_flow.Persistence.Status);
            _flow.PersistenceChanged += HandlePersistenceChanged;
            _stateSubscription = _store.State.Subscribe(HandleRunStateChanged);
        }

        /// <summary>解除全局状态订阅，避免释放后重复播放。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_initialized)
                _flow.PersistenceChanged -= HandlePersistenceChanged;
            _stateSubscription?.Dispose();
            _stateSubscription = null;
        }

        /// <summary>只在非 Terminal 到 Terminal 的状态边沿播放一次 Confirm。</summary>
        private void HandleRunStateChanged(RunState state)
        {
            bool isTerminal = IsTerminal(state);
            if (!_wasTerminal && isTerminal)
                _player.Play(UiAudioCue.Confirm);

            _wasTerminal = isTerminal;
        }

        /// <summary>只在 Persistence 首次进入 CommitFailed 的边沿播放 Error。</summary>
        private void HandlePersistenceChanged()
        {
            bool isCommitFailed = IsCommitFailed(_flow.Persistence.Status);
            if (!_wasCommitFailed && isCommitFailed)
                _player.Play(UiAudioCue.Error);

            _wasCommitFailed = isCommitFailed;
        }

        /// <summary>判断当前 Run 快照是否为唯一终局。</summary>
        private static bool IsTerminal(RunState state)
        {
            return state != null && state.ProgressPhase == RunProgressPhase.Terminal;
        }

        /// <summary>判断当前存档状态是否为检查点提交失败。</summary>
        private static bool IsCommitFailed(RunPersistenceStatus status)
        {
            return status == RunPersistenceStatus.CommitFailed;
        }

        /// <summary>释放后的 Presenter 拒绝重新订阅全局状态。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RunOutcomeUiAudioPresenter));
        }
    }
}
