using System;
using TinySpire.Profile.Presentation;
using VContainer.Unity;

namespace TinySpire.Presentation.Audio
{
    /// <summary>在全局教程生命周期内把提示动作映射为类型化 UI cue。</summary>
    public sealed class TutorialUiAudioPresenter : IInitializable, IDisposable
    {
        private readonly ITutorialGuideView _view;
        private readonly IUiAudioPlayer _player;

        private bool _initialized;
        private bool _disposed;

        /// <summary>接收全局教程 View 与唯一音频播放端口。</summary>
        public TutorialUiAudioPresenter(
            ITutorialGuideView view,
            IUiAudioPlayer player)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>一次性订阅教程确认、跳过与重置动作。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;

            _initialized = true;
            _view.ConfirmRequested += HandleConfirm;
            _view.SkipRequested += HandleSkip;
            _view.ResetRequested += HandleReset;
        }

        /// <summary>解除全局教程动作订阅。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (!_initialized)
                return;

            _view.ConfirmRequested -= HandleConfirm;
            _view.SkipRequested -= HandleSkip;
            _view.ResetRequested -= HandleReset;
        }

        /// <summary>教程确认播放强调 Confirm。</summary>
        private void HandleConfirm()
        {
            _player.Play(UiAudioCue.Confirm);
        }

        /// <summary>教程跳过播放轻量 Click。</summary>
        private void HandleSkip()
        {
            _player.Play(UiAudioCue.Click);
        }

        /// <summary>教程重置播放轻量 Click。</summary>
        private void HandleReset()
        {
            _player.Play(UiAudioCue.Click);
        }

        /// <summary>释放后的 Presenter 拒绝重新订阅教程事件。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TutorialUiAudioPresenter));
        }
    }
}
