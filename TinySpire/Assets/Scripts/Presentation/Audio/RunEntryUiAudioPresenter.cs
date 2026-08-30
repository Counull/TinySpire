using System;
using TinySpire.Settings.Presentation;
using TinySpire.UI.Run;
using VContainer.Unity;

namespace TinySpire.Presentation.Audio
{
    /// <summary>在 RunEntry 场景生命周期内把入口与设置动作映射为类型化 UI cue。</summary>
    public sealed class RunEntryUiAudioPresenter : IInitializable, IDisposable
    {
        private readonly IRunEntryView _runEntryView;
        private readonly IAppSettingsView _settingsView;
        private readonly IUiAudioPlayer _player;

        private bool _initialized;
        private bool _disposed;

        /// <summary>接收两个同场景 View seam 与唯一音频播放端口。</summary>
        public RunEntryUiAudioPresenter(
            IRunEntryView runEntryView,
            IAppSettingsView settingsView,
            IUiAudioPlayer player)
        {
            _runEntryView = runEntryView ?? throw new ArgumentNullException(nameof(runEntryView));
            _settingsView = settingsView ?? throw new ArgumentNullException(nameof(settingsView));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>一次性订阅 RunEntry 与设置动作流。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;

            _initialized = true;
            _runEntryView.ActionRequested += HandleRunEntryAction;
            _settingsView.ActionRequested += HandleSettingsAction;
        }

        /// <summary>解除场景级动作订阅，避免旧 RunEntry 场景继续发声。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (!_initialized)
                return;

            _runEntryView.ActionRequested -= HandleRunEntryAction;
            _settingsView.ActionRequested -= HandleSettingsAction;
        }

        /// <summary>把入口页面导航、选择和返回动作映射为轻量 Click。</summary>
        private void HandleRunEntryAction(RunEntryAction action)
        {
            _player.Play(ResolveRunEntryCue(action.Kind));
        }

        /// <summary>设置页全部离散调整都播放轻量 Click。</summary>
        private void HandleSettingsAction(AppSettingsAction action)
        {
            _player.Play(UiAudioCue.Click);
        }

        /// <summary>为当前已覆盖的 RunEntry 导航动作返回稳定 cue。</summary>
        private static UiAudioCue ResolveRunEntryCue(RunEntryActionKind kind)
        {
            switch (kind)
            {
                case RunEntryActionKind.StartGame:
                case RunEntryActionKind.OpenSettings:
                case RunEntryActionKind.OpenCompendium:
                case RunEntryActionKind.OpenStatistics:
                case RunEntryActionKind.Back:
                case RunEntryActionKind.SelectHero:
                case RunEntryActionKind.LeaveTerminalRun:
                case RunEntryActionKind.RequestAbandon:
                case RunEntryActionKind.RequestExitAfterSaveFailure:
                    return UiAudioCue.Click;
                case RunEntryActionKind.ConfirmHero:
                case RunEntryActionKind.EnterMapNode:
                case RunEntryActionKind.ContinueGame:
                case RunEntryActionKind.ConfirmAbandon:
                case RunEntryActionKind.RetrySave:
                case RunEntryActionKind.ConfirmRollback:
                case RunEntryActionKind.SelectCardReward:
                case RunEntryActionKind.SkipCardReward:
                case RunEntryActionKind.HealAtRest:
                case RunEntryActionKind.UpgradeCardAtRest:
                case RunEntryActionKind.ClaimChest:
                case RunEntryActionKind.SkipChest:
                case RunEntryActionKind.PurchaseShopStock:
                case RunEntryActionKind.LeaveShop:
                case RunEntryActionKind.ChooseEvent:
                    return UiAudioCue.Confirm;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>释放后的 Presenter 拒绝重新订阅场景事件。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RunEntryUiAudioPresenter));
        }
    }
}
