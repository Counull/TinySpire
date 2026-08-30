using System;
using System.Collections.Generic;
using TinySpire.Settings;

namespace TinySpire.Profile.Presentation
{
    /// <summary>只把 PlayerProfileStateStore 的当前有序提示投影给独立教程 View。</summary>
    public sealed class TutorialGuidePresenter : IDisposable
    {
        private readonly ITutorialGuideView _view;
        private readonly PlayerProfileStateStore _profile;
        private readonly AppSettingsService _settings;

        private bool _initialized;
        private bool _disposed;
        private TutorialContext? _observedContext;

        /// <summary>以教程 View 与唯一 Profile owner 创建纯 Presenter。</summary>
        public TutorialGuidePresenter(
            ITutorialGuideView view,
            PlayerProfileStateStore profile,
            AppSettingsService settings)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>一次性订阅三类 View 事件与耐久 Profile 发布，并先渲染隐藏模型。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;
            if (_profile.Current == null)
                throw new InvalidOperationException("Player Profile store must be initialized first.");
            if (_settings.Current == null)
                throw new InvalidOperationException("App settings service must be initialized first.");

            _initialized = true;
            _view.ConfirmRequested += HandleConfirm;
            _view.SkipRequested += HandleSkip;
            _view.ResetRequested += HandleReset;
            _profile.Changed += HandleProfileChanged;
            _settings.Changed += HandleSettingsChanged;
            ApplyAccessibility(_settings.Current);
            Render();
        }

        /// <summary>观察当前产品上下文，并只投影 owner 返回的当前有序提示。</summary>
        public void ObserveContext(TutorialContext context)
        {
            ThrowIfDisposed();
            EnsureInitialized();
            if (!Enum.IsDefined(typeof(TutorialContext), context))
                throw new ArgumentOutOfRangeException(nameof(context));

            _observedContext = context;
            Render();
        }

        /// <summary>清除离开的玩法上下文并立即投影不阻挡输入的隐藏模型。</summary>
        public void ClearContext()
        {
            ThrowIfDisposed();
            EnsureInitialized();
            _observedContext = null;
            Render();
        }

        /// <summary>解除全部 View 与 Profile 订阅，避免旧场景继续处理教程输入。</summary>
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
            _profile.Changed -= HandleProfileChanged;
            _settings.Changed -= HandleSettingsChanged;
        }

        /// <summary>从当前上下文重新读取可见提示并提交幂等确认。</summary>
        private void HandleConfirm()
        {
            if (_disposed || !_initialized)
                return;

            IReadOnlyList<TutorialPromptDefinition> prompts = GetCurrentPrompts();
            if (prompts.Count == 0)
            {
                Render();
                return;
            }

            TutorialProfileActionStatus result = _profile.Acknowledge(prompts[0].Id);
            if (result != TutorialProfileActionStatus.Success)
                Render();
        }

        /// <summary>把跳过意图交给 Profile owner，失败时立即重投影 fail-open 状态。</summary>
        private void HandleSkip()
        {
            if (_disposed || !_initialized)
                return;

            TutorialProfileActionStatus result = _profile.SkipTutorial();
            if (result != TutorialProfileActionStatus.Success)
                Render();
        }

        /// <summary>把重置意图交给 Profile owner，并按当前真实上下文重新投影首步骤。</summary>
        private void HandleReset()
        {
            if (_disposed || !_initialized)
                return;

            TutorialProfileActionStatus result = _profile.ResetTutorial();
            if (result != TutorialProfileActionStatus.Success)
                Render();
        }

        /// <summary>耐久 Profile 成功发布后重新读取投影，不自行保存第二份进度。</summary>
        private void HandleProfileChanged(PlayerProfileSnapshot profile)
        {
            if (!_disposed)
                Render();
        }

        /// <summary>应用设置成功发布后立即把新快照收窄为教程只读可访问性投影。</summary>
        private void HandleSettingsChanged(AppSettingsSnapshot settings)
        {
            if (!_disposed)
                ApplyAccessibility(settings);
        }

        /// <summary>仅复制文字缩放、高对比和减少动态，不向设置 owner 写入任何事实。</summary>
        private void ApplyAccessibility(AppSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            _view.ApplyAccessibility(new TutorialGuideAccessibilityViewModel(
                settings.TextScale,
                settings.HighContrast,
                settings.ReducedMotion));
        }

        /// <summary>把当前上下文的零或一个有序提示转换为完整 ViewModel。</summary>
        private void Render()
        {
            IReadOnlyList<TutorialPromptDefinition> prompts = GetCurrentPrompts();
            _view.Render(
                prompts.Count == 0
                    ? TutorialGuideViewModel.Hidden
                    : TutorialGuideViewModel.Visible(prompts[0]));
        }

        /// <summary>只经 Profile owner 收集当前上下文提示；尚未观察上下文时返回空集合。</summary>
        private IReadOnlyList<TutorialPromptDefinition> GetCurrentPrompts()
        {
            return _observedContext.HasValue
                ? _profile.GetPendingPrompts(_observedContext.Value)
                : Array.Empty<TutorialPromptDefinition>();
        }

        /// <summary>拒绝初始化前观察上下文。</summary>
        private void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("Tutorial guide presenter must be initialized first.");
        }

        /// <summary>拒绝在场景生命周期结束后重新使用 Presenter。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TutorialGuidePresenter));
        }
    }
}
