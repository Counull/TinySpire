using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Settings;
using TinySpire.UI.Battle;

public sealed class BattleAccessibilityG8Tests
{
    /// <summary>Battle 进入时必须立即消费当前文字、高对比和减少动态设置。</summary>
    [Test]
    public void Initialize_AppliesCurrentAccessibilitySettings()
    {
        var harness = new AccessibilityHarness(CreateSettings(
            AppTextScale.Percent125,
            highContrast: true,
            reducedMotion: true));

        harness.Presenter.Initialize();

        Assert.That(harness.View.Models, Has.Count.EqualTo(1));
        BattleAccessibilityViewModel model = harness.View.Models[0];
        Assert.That(model.TextScaleMultiplier, Is.EqualTo(1.25f));
        Assert.That(model.HighContrast, Is.True);
        Assert.That(model.MotionSpeedMultiplier, Is.EqualTo(4f));
    }

    /// <summary>设置 owner 发布后必须重投影，不能要求重载 Battle 场景。</summary>
    [Test]
    public void SettingsChanged_ReappliesBattleProjection()
    {
        var harness = new AccessibilityHarness(CreateSettings(
            AppTextScale.Percent100,
            highContrast: false,
            reducedMotion: false));
        harness.Presenter.Initialize();

        AppSettingsChangeStatus result = harness.Settings.TryChange(CreateSettings(
            AppTextScale.Percent125,
            highContrast: true,
            reducedMotion: true));

        Assert.That(result, Is.EqualTo(AppSettingsChangeStatus.Success));
        Assert.That(harness.View.Models, Has.Count.EqualTo(2));
        Assert.That(harness.View.Models[1].TextScaleMultiplier, Is.EqualTo(1.25f));
        Assert.That(harness.View.Models[1].HighContrast, Is.True);
        Assert.That(harness.View.Models[1].MotionSpeedMultiplier, Is.EqualTo(4f));
    }

    /// <summary>Battle Scope 释放后必须停止响应父级设置变化。</summary>
    [Test]
    public void Dispose_StopsSettingsProjection()
    {
        var harness = new AccessibilityHarness(CreateSettings(
            AppTextScale.Percent100,
            highContrast: false,
            reducedMotion: false));
        harness.Presenter.Initialize();
        harness.Presenter.Dispose();

        harness.Settings.TryChange(CreateSettings(
            AppTextScale.Percent125,
            highContrast: true,
            reducedMotion: true));

        Assert.That(harness.View.Models, Has.Count.EqualTo(1));
    }

    /// <summary>建立只改变可访问性字段的有效首发设置。</summary>
    private static AppSettingsSnapshot CreateSettings(
        AppTextScale textScale,
        bool highContrast,
        bool reducedMotion)
    {
        return new AppSettingsSnapshot(
            AppSettingsSnapshot.EnglishLocaleCode,
            80,
            AppDisplayMode.Windowed,
            new AppResolution(1920, 1080),
            textScale,
            highContrast,
            reducedMotion);
    }

    /// <summary>组装真实 settings owner 与可观察 Battle View seam。</summary>
    private sealed class AccessibilityHarness
    {
        /// <summary>供测试提交后续设置的唯一 owner。</summary>
        public AppSettingsService Settings { get; }

        /// <summary>记录每次完整 Battle 可访问性投影。</summary>
        public RecordingView View { get; }

        /// <summary>本测试直接驱动的 Presenter。</summary>
        public BattleAccessibilityPresenter Presenter { get; }

        /// <summary>以指定启动设置建立已初始化环境。</summary>
        public AccessibilityHarness(AppSettingsSnapshot initial)
        {
            var repository = new RecordingRepository(initial);
            Settings = new AppSettingsService(repository, new RecordingPlatform(initial));
            Settings.Initialize();
            View = new RecordingView();
            Presenter = new BattleAccessibilityPresenter(View, Settings);
        }
    }

    /// <summary>只记录完整 ViewModel 的 Battle 表现边界。</summary>
    private sealed class RecordingView : IBattleAccessibilityView
    {
        /// <summary>按应用顺序保存全部不可变模型。</summary>
        public List<BattleAccessibilityViewModel> Models { get; } =
            new List<BattleAccessibilityViewModel>();

        /// <summary>保存 Presenter 交付的一次完整投影。</summary>
        public void Apply(BattleAccessibilityViewModel model)
        {
            Models.Add(model);
        }
    }

    /// <summary>提供稳定初值并接受全部提交的内存 repository。</summary>
    private sealed class RecordingRepository : IAppSettingsRepository
    {
        private readonly AppSettingsSnapshot _initial;

        /// <summary>冻结 owner 首次读取的设置。</summary>
        public RecordingRepository(AppSettingsSnapshot initial)
        {
            _initial = initial;
        }

        /// <summary>返回稳定初值。</summary>
        public AppSettingsRepositoryLoadResult Load()
        {
            return AppSettingsRepositoryLoadResult.Succeeded(_initial);
        }

        /// <summary>本测试所有合法设置提交都成功。</summary>
        public AppSettingsRepositoryCommitResult Commit(AppSettingsSnapshot settings)
        {
            return AppSettingsRepositoryCommitResult.Succeeded();
        }
    }

    /// <summary>只记录 owner 应用并冻结 1920x1080 支持的测试平台。</summary>
    private sealed class RecordingPlatform : IAppSettingsPlatform
    {
        private readonly AppSettingsSnapshot _defaults;

        /// <summary>测试声明的唯一分辨率。</summary>
        public IReadOnlyList<AppResolution> SupportedResolutions { get; } =
            new[] { new AppResolution(1920, 1080) };

        /// <summary>保存默认设置。</summary>
        public RecordingPlatform(AppSettingsSnapshot defaults)
        {
            _defaults = defaults;
        }

        /// <summary>返回测试默认设置。</summary>
        public AppSettingsSnapshot CreateDefaults()
        {
            return _defaults;
        }

        /// <summary>只接受测试冻结分辨率。</summary>
        public bool SupportsResolution(AppResolution resolution)
        {
            return resolution == SupportedResolutions[0];
        }

        /// <summary>测试不需要观察系统副作用。</summary>
        public void Apply(AppSettingsSnapshot settings)
        {
        }
    }
}
