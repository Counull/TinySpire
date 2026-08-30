using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinySpire.Settings
{
    /// <summary>把耐久应用设置映射到 Unity Localization、音量与窗口系统。</summary>
    public sealed class UnityAppSettingsPlatform : IAppSettingsPlatform
    {
        private static readonly IReadOnlyList<AppResolution> FrozenLaunchResolutions =
            Array.AsReadOnly(new[]
            {
                new AppResolution(1280, 720),
                new AppResolution(1920, 1080),
                new AppResolution(2560, 1440),
                new AppResolution(1920, 1200),
                new AppResolution(2560, 1080),
            });

        private readonly LocalizationService _localization;

        /// <summary>首发设置页明确支持的分辨率矩阵。</summary>
        public static IReadOnlyList<AppResolution> LaunchResolutions => FrozenLaunchResolutions;

        /// <summary>向设置 owner 公开同一份不可变首发矩阵。</summary>
        public IReadOnlyList<AppResolution> SupportedResolutions => FrozenLaunchResolutions;

        /// <summary>绑定已在启动链初始化完成的本地化服务。</summary>
        public UnityAppSettingsPlatform(LocalizationService localization)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>以当前 Unity 状态建立首启安全默认值，并把未知窗口尺寸回落到 1080p。</summary>
        public AppSettingsSnapshot CreateDefaults()
        {
            string localeCode = AppSettingsSnapshot.IsSupportedLocale(_localization.CurrentLocaleCode)
                ? _localization.CurrentLocaleCode
                : AppSettingsSnapshot.EnglishLocaleCode;
            var currentResolution = new AppResolution(
                Math.Max(1, Screen.width),
                Math.Max(1, Screen.height));
            AppResolution resolution = SupportsResolution(currentResolution)
                ? currentResolution
                : FrozenLaunchResolutions[1];

            return new AppSettingsSnapshot(
                localeCode,
                Mathf.RoundToInt(Mathf.Clamp01(AudioListener.volume) * 100f),
                Screen.fullScreenMode == FullScreenMode.Windowed
                    ? AppDisplayMode.Windowed
                    : AppDisplayMode.BorderlessFullscreen,
                resolution,
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false);
        }

        /// <summary>只接受产品首发矩阵内的精确宽高组合。</summary>
        public bool SupportsResolution(AppResolution resolution)
        {
            for (int index = 0; index < FrozenLaunchResolutions.Count; index++)
            {
                if (FrozenLaunchResolutions[index] == resolution)
                    return true;
            }

            return false;
        }

        /// <summary>同步应用语言、主音量与窗口；表现层设置继续由订阅者读取同一快照。</summary>
        public void Apply(AppSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (!SupportsResolution(settings.Resolution))
                throw new ArgumentOutOfRangeException(nameof(settings), "Unsupported app resolution.");
            if (!_localization.SetLocale(settings.LocaleCode))
                throw new InvalidOperationException($"Locale '{settings.LocaleCode}' is not installed.");

            AudioListener.volume = settings.MasterVolumePercent / 100f;
            FullScreenMode mode = settings.DisplayMode == AppDisplayMode.Windowed
                ? FullScreenMode.Windowed
                : FullScreenMode.FullScreenWindow;
            Screen.SetResolution(settings.Resolution.Width, settings.Resolution.Height, mode);
        }
    }
}
