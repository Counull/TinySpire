using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using R3;
using VContainer;
using VContainer.Unity;

namespace TinySpire.Settings.Presentation
{
    /// <summary>设置 View 可以提交给 Presenter 的封闭动作集合。</summary>
    public enum AppSettingsActionKind
    {
        CycleLocale,
        DecreaseMasterVolume,
        IncreaseMasterVolume,
        ToggleDisplayMode,
        PreviousResolution,
        NextResolution,
        CycleTextScale,
        ToggleHighContrast,
        ToggleReducedMotion,
    }

    /// <summary>设置 View 发布的一项无业务数据动作。</summary>
    public readonly struct AppSettingsAction
    {
        /// <summary>本次玩家意图的封闭分类。</summary>
        public AppSettingsActionKind Kind { get; }

        /// <summary>创建并验证一项设置动作。</summary>
        public AppSettingsAction(AppSettingsActionKind kind)
        {
            if (!Enum.IsDefined(typeof(AppSettingsActionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));

            Kind = kind;
        }
    }

    /// <summary>设置页面当前渲染状态的封闭分类。</summary>
    public enum AppSettingsViewStatus
    {
        Ready,
        SaveFailed,
        ApplyFailed,
        RecoveryRequired,
        UnsupportedResolution,
    }

    /// <summary>设置 View 可以按稳定槽位读取的完整文本集合。</summary>
    public enum AppSettingsTextSlot
    {
        Title,
        LanguageLabel,
        LanguageValue,
        MasterVolumeLabel,
        MasterVolumeValue,
        DisplayModeLabel,
        DisplayModeValue,
        ResolutionLabel,
        ResolutionValue,
        TextScaleLabel,
        TextScaleValue,
        HighContrastLabel,
        HighContrastValue,
        ReducedMotionLabel,
        ReducedMotionValue,
        DecreaseAction,
        IncreaseAction,
        PreviousAction,
        NextAction,
    }

    /// <summary>设置页一次渲染使用的完整不可变投影。</summary>
    public sealed class AppSettingsViewModel
    {
        private readonly IReadOnlyDictionary<AppSettingsTextSlot, string> _texts;

        /// <summary>本次渲染对应的唯一稳定设置快照。</summary>
        public AppSettingsSnapshot Settings { get; }

        /// <summary>设置页当前的类型化交互状态。</summary>
        public AppSettingsViewStatus Status { get; }

        /// <summary>失败状态对应的已本地化文案；Ready 时为空。</summary>
        public string FailureText { get; }

        /// <summary>冻结设置、状态与已解析文本。</summary>
        public AppSettingsViewModel(
            AppSettingsSnapshot settings,
            AppSettingsViewStatus status,
            IReadOnlyDictionary<AppSettingsTextSlot, string> texts,
            string failureText = "")
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!Enum.IsDefined(typeof(AppSettingsViewStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));

            Status = status;
            FailureText = failureText ?? throw new ArgumentNullException(nameof(failureText));
            _texts = new ReadOnlyDictionary<AppSettingsTextSlot, string>(
                new Dictionary<AppSettingsTextSlot, string>(texts));
        }

        /// <summary>按稳定槽位读取已解析文本，并拒绝不完整模型。</summary>
        public string GetText(AppSettingsTextSlot slot)
        {
            if (!_texts.TryGetValue(slot, out string value))
                throw new InvalidOperationException($"App settings text slot '{slot}' is missing.");

            return value;
        }
    }

    /// <summary>设置 Presenter 与 Unity View 之间唯一、无业务状态的渲染 seam。</summary>
    public interface IAppSettingsView
    {
        /// <summary>按钮输入被归一化后发布的唯一动作事件。</summary>
        event Action<AppSettingsAction> ActionRequested;

        /// <summary>用完整不可变投影替换当前设置页面。</summary>
        void Render(AppSettingsViewModel model);
    }

    /// <summary>把应用设置 owner 投影到独立设置 View。</summary>
    public sealed class AppSettingsPresenter : IInitializable, IDisposable
    {
        private const string TitleKey = "app.settings.title";
        private const string LanguageLabelKey = "app.settings.language";
        private const string EnglishValueKey = "app.settings.language.en";
        private const string SimplifiedChineseValueKey = "app.settings.language.zh_cn";
        private const string MasterVolumeLabelKey = "app.settings.master_volume";
        private const string DisplayModeLabelKey = "app.settings.display_mode";
        private const string WindowedValueKey = "app.settings.display_mode.windowed";
        private const string BorderlessValueKey = "app.settings.display_mode.borderless";
        private const string ResolutionLabelKey = "app.settings.resolution";
        private const string TextScaleLabelKey = "app.settings.text_scale";
        private const string HighContrastLabelKey = "app.settings.high_contrast";
        private const string ReducedMotionLabelKey = "app.settings.reduced_motion";
        private const string EnabledValueKey = "app.settings.state.enabled";
        private const string DisabledValueKey = "app.settings.state.disabled";
        private const string DecreaseActionKey = "app.settings.action.decrease";
        private const string IncreaseActionKey = "app.settings.action.increase";
        private const string PreviousActionKey = "app.settings.action.previous";
        private const string NextActionKey = "app.settings.action.next";
        private const string SaveFailureKey = "app.settings.failure.save";
        private const string UnsupportedResolutionFailureKey =
            "app.settings.failure.unsupported_resolution";

        private readonly IAppSettingsView _view;
        private readonly AppSettingsService _settings;
        private readonly IReadOnlyList<AppResolution> _resolutions;
        private readonly Func<string, string> _localize;
        private readonly Func<Action, IDisposable> _subscribeLocaleChanged;

        private bool _initialized;
        private bool _disposed;
        private IDisposable _localeSubscription;
        private AppSettingsViewStatus _status = AppSettingsViewStatus.Ready;

        /// <summary>以生产本地化服务创建可由 VContainer 驱动的 Presenter。</summary>
        [Inject]
        public AppSettingsPresenter(
            IAppSettingsView view,
            AppSettingsService settings,
            IAppSettingsPlatform platform,
            LocalizationService localization)
            : this(
                view,
                settings,
                platform,
                CreateLocalizer(localization),
                CreateLocaleSubscription(localization))
        {
        }

        /// <summary>以可替换本地化 seam 创建可直接 EditMode 验证的 Presenter。</summary>
        public AppSettingsPresenter(
            IAppSettingsView view,
            AppSettingsService settings,
            IAppSettingsPlatform platform,
            Func<string, string> localize,
            Func<Action, IDisposable> subscribeLocaleChanged)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (platform == null)
                throw new ArgumentNullException(nameof(platform));
            _resolutions = CopyResolutions(platform.SupportedResolutions);
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
            _subscribeLocaleChanged = subscribeLocaleChanged ??
                throw new ArgumentNullException(nameof(subscribeLocaleChanged));
        }

        /// <summary>一次性订阅动作与语言变化，并立即渲染 owner 当前快照。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;
            if (_settings.Current == null)
                throw new InvalidOperationException("App settings service must be initialized first.");

            _initialized = true;
            _status = _settings.RequiresRecovery
                ? AppSettingsViewStatus.RecoveryRequired
                : AppSettingsViewStatus.Ready;
            _view.ActionRequested += HandleAction;
            _settings.Changed += HandleSettingsChanged;
            _localeSubscription = _subscribeLocaleChanged(Render) ??
                throw new InvalidOperationException("Locale subscription cannot be null.");
            Render();
        }

        /// <summary>解除设置页生命周期内的全部订阅。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_initialized)
            {
                _view.ActionRequested -= HandleAction;
                _settings.Changed -= HandleSettingsChanged;
            }

            _localeSubscription?.Dispose();
        }

        /// <summary>把封闭 View 动作转换为完整候选快照。</summary>
        private void HandleAction(AppSettingsAction action)
        {
            if (_disposed)
                return;

            AppSettingsSnapshot candidate;
            switch (action.Kind)
            {
                case AppSettingsActionKind.CycleLocale:
                    candidate = WithLocale(
                        _settings.Current.LocaleCode == AppSettingsSnapshot.EnglishLocaleCode
                            ? AppSettingsSnapshot.SimplifiedChineseLocaleCode
                            : AppSettingsSnapshot.EnglishLocaleCode);
                    break;
                case AppSettingsActionKind.DecreaseMasterVolume:
                    candidate = WithMasterVolume(
                        Math.Max(0, _settings.Current.MasterVolumePercent - 10));
                    break;
                case AppSettingsActionKind.IncreaseMasterVolume:
                    candidate = WithMasterVolume(
                        Math.Min(100, _settings.Current.MasterVolumePercent + 10));
                    break;
                case AppSettingsActionKind.ToggleDisplayMode:
                    candidate = WithDisplayMode(
                        _settings.Current.DisplayMode == AppDisplayMode.Windowed
                            ? AppDisplayMode.BorderlessFullscreen
                            : AppDisplayMode.Windowed);
                    break;
                case AppSettingsActionKind.PreviousResolution:
                    candidate = WithResolution(MoveResolution(-1));
                    break;
                case AppSettingsActionKind.NextResolution:
                    candidate = WithResolution(MoveResolution(1));
                    break;
                case AppSettingsActionKind.CycleTextScale:
                    candidate = WithTextScale(
                        _settings.Current.TextScale == AppTextScale.Percent100
                            ? AppTextScale.Percent125
                            : AppTextScale.Percent100);
                    break;
                case AppSettingsActionKind.ToggleHighContrast:
                    candidate = WithHighContrast(!_settings.Current.HighContrast);
                    break;
                case AppSettingsActionKind.ToggleReducedMotion:
                    candidate = WithReducedMotion(!_settings.Current.ReducedMotion);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, null);
            }

            _status = _settings.RequiresRecovery
                ? AppSettingsViewStatus.RecoveryRequired
                : AppSettingsViewStatus.Ready;
            AppSettingsChangeStatus result = _settings.TryChange(candidate);
            HandleChangeResult(result);
        }

        /// <summary>把 owner 的类型化结果映射为当前页面状态并补齐无发布场景渲染。</summary>
        private void HandleChangeResult(AppSettingsChangeStatus result)
        {
            switch (result)
            {
                case AppSettingsChangeStatus.Success:
                    return;
                case AppSettingsChangeStatus.Unchanged:
                    Render();
                    return;
                case AppSettingsChangeStatus.UnsupportedResolution:
                    _status = AppSettingsViewStatus.UnsupportedResolution;
                    Render();
                    return;
                case AppSettingsChangeStatus.SaveFailed:
                    _status = AppSettingsViewStatus.SaveFailed;
                    Render();
                    return;
                case AppSettingsChangeStatus.ApplyFailed:
                    _status = AppSettingsViewStatus.ApplyFailed;
                    Render();
                    return;
                case AppSettingsChangeStatus.RecoveryFailed:
                case AppSettingsChangeStatus.RecoveryRequired:
                    _status = AppSettingsViewStatus.RecoveryRequired;
                    Render();
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        /// <summary>owner 成功发布新快照时重新投影设置页面。</summary>
        private void HandleSettingsChanged(AppSettingsSnapshot settings)
        {
            Render();
        }

        /// <summary>把当前稳定设置和当前语言解析为完整 ViewModel。</summary>
        private void Render()
        {
            AppSettingsSnapshot settings = _settings.Current;
            var texts = new Dictionary<AppSettingsTextSlot, string>
            {
                [AppSettingsTextSlot.Title] = _localize(TitleKey),
                [AppSettingsTextSlot.LanguageLabel] = _localize(LanguageLabelKey),
                [AppSettingsTextSlot.LanguageValue] = _localize(
                    settings.LocaleCode == AppSettingsSnapshot.EnglishLocaleCode
                        ? EnglishValueKey
                        : SimplifiedChineseValueKey),
                [AppSettingsTextSlot.MasterVolumeLabel] = _localize(MasterVolumeLabelKey),
                [AppSettingsTextSlot.MasterVolumeValue] =
                    settings.MasterVolumePercent.ToString(CultureInfo.InvariantCulture) + "%",
                [AppSettingsTextSlot.DisplayModeLabel] = _localize(DisplayModeLabelKey),
                [AppSettingsTextSlot.DisplayModeValue] = _localize(
                    settings.DisplayMode == AppDisplayMode.Windowed
                        ? WindowedValueKey
                        : BorderlessValueKey),
                [AppSettingsTextSlot.ResolutionLabel] = _localize(ResolutionLabelKey),
                [AppSettingsTextSlot.ResolutionValue] = settings.Resolution.ToString(),
                [AppSettingsTextSlot.TextScaleLabel] = _localize(TextScaleLabelKey),
                [AppSettingsTextSlot.TextScaleValue] =
                    ((int)settings.TextScale).ToString(CultureInfo.InvariantCulture) + "%",
                [AppSettingsTextSlot.HighContrastLabel] = _localize(HighContrastLabelKey),
                [AppSettingsTextSlot.HighContrastValue] = _localize(
                    settings.HighContrast ? EnabledValueKey : DisabledValueKey),
                [AppSettingsTextSlot.ReducedMotionLabel] = _localize(ReducedMotionLabelKey),
                [AppSettingsTextSlot.ReducedMotionValue] = _localize(
                    settings.ReducedMotion ? EnabledValueKey : DisabledValueKey),
                [AppSettingsTextSlot.DecreaseAction] = _localize(DecreaseActionKey),
                [AppSettingsTextSlot.IncreaseAction] = _localize(IncreaseActionKey),
                [AppSettingsTextSlot.PreviousAction] = _localize(PreviousActionKey),
                [AppSettingsTextSlot.NextAction] = _localize(NextActionKey),
            };
            _view.Render(new AppSettingsViewModel(
                settings,
                _status,
                texts,
                ResolveFailureText()));
        }

        /// <summary>把类型化页面状态解析为当前语言下的故障文案。</summary>
        private string ResolveFailureText()
        {
            switch (_status)
            {
                case AppSettingsViewStatus.Ready:
                    return string.Empty;
                case AppSettingsViewStatus.SaveFailed:
                case AppSettingsViewStatus.ApplyFailed:
                case AppSettingsViewStatus.RecoveryRequired:
                    return _localize(SaveFailureKey);
                case AppSettingsViewStatus.UnsupportedResolution:
                    return _localize(UnsupportedResolutionFailureKey);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>把生产本地化服务收窄为按 key 读取文本的函数 seam。</summary>
        private static Func<string, string> CreateLocalizer(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return key => localization.GetString(key);
        }

        /// <summary>把生产 locale 流收窄为无载荷重绘订阅 seam。</summary>
        private static Func<Action, IDisposable> CreateLocaleSubscription(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return handler => localization.LocaleChanged.Subscribe(_ => handler());
        }

        /// <summary>只替换语言字段，同时保留其余全部已耐久设置事实。</summary>
        private AppSettingsSnapshot WithLocale(string localeCode)
        {
            AppSettingsSnapshot current = _settings.Current;
            return new AppSettingsSnapshot(
                localeCode,
                current.MasterVolumePercent,
                current.DisplayMode,
                current.Resolution,
                current.TextScale,
                current.HighContrast,
                current.ReducedMotion);
        }

        /// <summary>只替换主音量字段，同时保留其余全部已耐久设置事实。</summary>
        private AppSettingsSnapshot WithMasterVolume(int masterVolumePercent)
        {
            AppSettingsSnapshot current = _settings.Current;
            return new AppSettingsSnapshot(
                current.LocaleCode,
                masterVolumePercent,
                current.DisplayMode,
                current.Resolution,
                current.TextScale,
                current.HighContrast,
                current.ReducedMotion);
        }

        /// <summary>只替换显示模式字段，同时保留其余全部已耐久设置事实。</summary>
        private AppSettingsSnapshot WithDisplayMode(AppDisplayMode displayMode)
        {
            AppSettingsSnapshot current = _settings.Current;
            return new AppSettingsSnapshot(
                current.LocaleCode,
                current.MasterVolumePercent,
                displayMode,
                current.Resolution,
                current.TextScale,
                current.HighContrast,
                current.ReducedMotion);
        }

        /// <summary>按方向在冻结分辨率矩阵中循环并返回下一项。</summary>
        private AppResolution MoveResolution(int direction)
        {
            int currentIndex = -1;
            for (int index = 0; index < _resolutions.Count; index++)
            {
                if (_resolutions[index] == _settings.Current.Resolution)
                {
                    currentIndex = index;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Current resolution '{_settings.Current.Resolution}' is outside the launch matrix.");
            }

            int nextIndex = (currentIndex + direction + _resolutions.Count) % _resolutions.Count;
            return _resolutions[nextIndex];
        }

        /// <summary>只替换分辨率字段，同时保留其余全部已耐久设置事实。</summary>
        private AppSettingsSnapshot WithResolution(AppResolution resolution)
        {
            AppSettingsSnapshot current = _settings.Current;
            return new AppSettingsSnapshot(
                current.LocaleCode,
                current.MasterVolumePercent,
                current.DisplayMode,
                resolution,
                current.TextScale,
                current.HighContrast,
                current.ReducedMotion);
        }

        /// <summary>只替换文字缩放字段，同时保留其余全部已耐久设置事实。</summary>
        private AppSettingsSnapshot WithTextScale(AppTextScale textScale)
        {
            AppSettingsSnapshot current = _settings.Current;
            return new AppSettingsSnapshot(
                current.LocaleCode,
                current.MasterVolumePercent,
                current.DisplayMode,
                current.Resolution,
                textScale,
                current.HighContrast,
                current.ReducedMotion);
        }

        /// <summary>只替换高对比字段，同时保留其余全部已耐久设置事实。</summary>
        private AppSettingsSnapshot WithHighContrast(bool highContrast)
        {
            AppSettingsSnapshot current = _settings.Current;
            return new AppSettingsSnapshot(
                current.LocaleCode,
                current.MasterVolumePercent,
                current.DisplayMode,
                current.Resolution,
                current.TextScale,
                highContrast,
                current.ReducedMotion);
        }

        /// <summary>只替换减少动态字段，同时保留其余全部已耐久设置事实。</summary>
        private AppSettingsSnapshot WithReducedMotion(bool reducedMotion)
        {
            AppSettingsSnapshot current = _settings.Current;
            return new AppSettingsSnapshot(
                current.LocaleCode,
                current.MasterVolumePercent,
                current.DisplayMode,
                current.Resolution,
                current.TextScale,
                current.HighContrast,
                reducedMotion);
        }

        /// <summary>复制平台集合，使设置页生命周期内的循环矩阵保持稳定。</summary>
        private static IReadOnlyList<AppResolution> CopyResolutions(
            IReadOnlyList<AppResolution> source)
        {
            if (source == null)
                throw new InvalidOperationException("Supported resolutions cannot be null.");
            if (source.Count == 0)
                throw new InvalidOperationException("Supported resolutions cannot be empty.");

            var copy = new AppResolution[source.Count];
            for (int index = 0; index < source.Count; index++)
                copy[index] = source[index];

            return Array.AsReadOnly(copy);
        }

        /// <summary>拒绝在场景生命周期结束后重新使用 Presenter。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AppSettingsPresenter));
        }
    }
}
