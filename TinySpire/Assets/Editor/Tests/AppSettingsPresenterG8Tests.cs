using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Settings;
using TinySpire.Settings.Presentation;

public sealed class AppSettingsPresenterG8Tests
{
    /// <summary>设置页初始化时必须一次性渲染 owner 当前的完整设置。</summary>
    [Test]
    public void Initialize_RendersCurrentSettings()
    {
        var harness = new PresenterHarness(CreateInitialSettings());

        harness.Presenter.Initialize();

        Assert.That(harness.View.Models, Has.Count.EqualTo(1));
        Assert.That(harness.View.Models[0].Settings, Is.EqualTo(CreateInitialSettings()));
        Assert.That(harness.View.Models[0].Status, Is.EqualTo(AppSettingsViewStatus.Ready));
        Assert.That(
            harness.View.Models[0].GetText(AppSettingsTextSlot.Title),
            Is.EqualTo("localized:app.settings.title"));
        Assert.That(
            harness.View.Models[0].GetText(AppSettingsTextSlot.MasterVolumeLabel),
            Is.EqualTo("localized:app.settings.master_volume"));
        Assert.That(
            harness.View.Models[0].GetText(AppSettingsTextSlot.LanguageValue),
            Is.EqualTo("localized:app.settings.language.en"));
    }

    /// <summary>语言动作必须只在持久化成功后从 en 切换并发布 zh-CN。</summary>
    [Test]
    public void CycleLocale_SavesAndRendersSimplifiedChinese()
    {
        var harness = new PresenterHarness(CreateInitialSettings());
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.CycleLocale);

        Assert.That(
            harness.Service.Current.LocaleCode,
            Is.EqualTo(AppSettingsSnapshot.SimplifiedChineseLocaleCode));
        Assert.That(harness.Repository.Commits, Has.Count.EqualTo(1));
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1]
                .GetText(AppSettingsTextSlot.LanguageValue),
            Is.EqualTo("localized:app.settings.language.zh_cn"));
    }

    /// <summary>音量增减必须以 10% 为单位并在 0～100 内封顶。</summary>
    [Test]
    public void MasterVolumeActions_StepByTenAndClamp()
    {
        var harness = new PresenterHarness(CreateInitialSettings());
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.IncreaseMasterVolume);
        harness.View.Request(AppSettingsActionKind.IncreaseMasterVolume);
        harness.View.Request(AppSettingsActionKind.IncreaseMasterVolume);
        harness.View.Request(AppSettingsActionKind.DecreaseMasterVolume);

        CollectionAssert.AreEqual(
            new[] { 90, 100, 90 },
            harness.Repository.Commits.ConvertAll(settings => settings.MasterVolumePercent));
        Assert.That(harness.Service.Current.MasterVolumePercent, Is.EqualTo(90));
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1]
                .GetText(AppSettingsTextSlot.MasterVolumeValue),
            Is.EqualTo("90%"));
    }

    /// <summary>显示模式动作必须只在 windowed 与 borderless 之间循环。</summary>
    [Test]
    public void ToggleDisplayMode_CyclesLaunchModes()
    {
        var harness = new PresenterHarness(CreateInitialSettings());
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.ToggleDisplayMode);
        Assert.That(
            harness.Service.Current.DisplayMode,
            Is.EqualTo(AppDisplayMode.BorderlessFullscreen));

        harness.View.Request(AppSettingsActionKind.ToggleDisplayMode);

        Assert.That(harness.Service.Current.DisplayMode, Is.EqualTo(AppDisplayMode.Windowed));
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1]
                .GetText(AppSettingsTextSlot.DisplayModeValue),
            Is.EqualTo("localized:app.settings.display_mode.windowed"));
    }

    /// <summary>分辨率前后动作必须在首发冻结矩阵两端安全循环。</summary>
    [Test]
    public void ResolutionActions_WrapFrozenLaunchMatrix()
    {
        var harness = new PresenterHarness(
            CreateInitialSettings(new AppResolution(1280, 720)));
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.PreviousResolution);
        Assert.That(
            harness.Service.Current.Resolution,
            Is.EqualTo(new AppResolution(2560, 1080)));

        harness.View.Request(AppSettingsActionKind.NextResolution);

        Assert.That(
            harness.Service.Current.Resolution,
            Is.EqualTo(new AppResolution(1280, 720)));
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1]
                .GetText(AppSettingsTextSlot.ResolutionValue),
            Is.EqualTo("1280x720"));
    }

    /// <summary>文字缩放动作必须只在首发 100% 与 125% 档位间循环。</summary>
    [Test]
    public void CycleTextScale_CyclesLaunchLevels()
    {
        var harness = new PresenterHarness(CreateInitialSettings());
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.CycleTextScale);
        Assert.That(harness.Service.Current.TextScale, Is.EqualTo(AppTextScale.Percent125));
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1]
                .GetText(AppSettingsTextSlot.TextScaleValue),
            Is.EqualTo("125%"));

        harness.View.Request(AppSettingsActionKind.CycleTextScale);

        Assert.That(harness.Service.Current.TextScale, Is.EqualTo(AppTextScale.Percent100));
    }

    /// <summary>高对比动作必须耐久并投影新的可访问性状态。</summary>
    [Test]
    public void ToggleHighContrast_SavesAndRendersEnabled()
    {
        var harness = new PresenterHarness(CreateInitialSettings());
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.ToggleHighContrast);

        Assert.That(harness.Service.Current.HighContrast, Is.True);
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1]
                .GetText(AppSettingsTextSlot.HighContrastValue),
            Is.EqualTo("localized:app.settings.state.enabled"));
    }

    /// <summary>减少动态动作必须耐久并投影新的可访问性状态。</summary>
    [Test]
    public void ToggleReducedMotion_SavesAndRendersEnabled()
    {
        var harness = new PresenterHarness(CreateInitialSettings());
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.ToggleReducedMotion);

        Assert.That(harness.Service.Current.ReducedMotion, Is.True);
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1]
                .GetText(AppSettingsTextSlot.ReducedMotionValue),
            Is.EqualTo("localized:app.settings.state.enabled"));
    }

    /// <summary>保存失败时必须继续展示旧快照并给 View 类型化故障文案。</summary>
    [Test]
    public void SaveFailure_KeepsOldSnapshotAndRendersTypedFailure()
    {
        AppSettingsSnapshot initial = CreateInitialSettings();
        var harness = new PresenterHarness(initial);
        harness.Repository.CommitResult =
            AppSettingsRepositoryCommitResult.IoFailure("disk full");
        harness.Presenter.Initialize();

        harness.View.Request(AppSettingsActionKind.IncreaseMasterVolume);

        Assert.That(harness.Service.Current, Is.EqualTo(initial));
        Assert.That(harness.Platform.Applied, Is.EqualTo(new[] { initial }));
        AppSettingsViewModel failure = harness.View.Models[harness.View.Models.Count - 1];
        Assert.That(failure.Settings, Is.EqualTo(initial));
        Assert.That(failure.Status, Is.EqualTo(AppSettingsViewStatus.SaveFailed));
        Assert.That(failure.FailureText, Is.EqualTo("localized:app.settings.failure.save"));
    }

    /// <summary>平台应用失败但完整补偿后必须展示可重试的 ApplyFailed，而不是冒充保存失败。</summary>
    [Test]
    public void ApplyFailure_AfterCompleteCompensationRendersTypedFailure()
    {
        AppSettingsSnapshot initial = CreateInitialSettings();
        var harness = new PresenterHarness(initial);
        harness.Presenter.Initialize();
        harness.Platform.FailuresRemaining = 1;

        harness.View.Request(AppSettingsActionKind.IncreaseMasterVolume);

        Assert.That(harness.Service.Current, Is.EqualTo(initial));
        Assert.That(harness.Service.RequiresRecovery, Is.False);
        AppSettingsViewModel failure = harness.View.Models[harness.View.Models.Count - 1];
        Assert.That(failure.Settings, Is.EqualTo(initial));
        Assert.That(failure.Status, Is.EqualTo(AppSettingsViewStatus.ApplyFailed));
        Assert.That(failure.FailureText, Is.EqualTo("localized:app.settings.failure.save"));
    }

    /// <summary>平台补偿失败后当前与重建 Presenter 都必须展示 RecoveryRequired。</summary>
    [Test]
    public void RecoveryFailure_RemainsVisibleAfterPresenterRebuild()
    {
        AppSettingsSnapshot initial = CreateInitialSettings();
        var harness = new PresenterHarness(initial);
        harness.Presenter.Initialize();
        harness.Platform.FailuresRemaining = 2;

        harness.View.Request(AppSettingsActionKind.IncreaseMasterVolume);

        Assert.That(harness.Service.Current, Is.EqualTo(initial));
        Assert.That(harness.Service.RequiresRecovery, Is.True);
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1].Status,
            Is.EqualTo(AppSettingsViewStatus.RecoveryRequired));

        harness.Presenter.Dispose();
        var rebuiltView = new RecordingView();
        var rebuilt = new AppSettingsPresenter(
            rebuiltView,
            harness.Service,
            harness.Platform,
            key => $"localized:{key}",
            harness.LocaleChanges.Subscribe);
        rebuilt.Initialize();

        Assert.That(rebuiltView.Models[0].Status,
            Is.EqualTo(AppSettingsViewStatus.RecoveryRequired));
        rebuilt.Dispose();
    }

    /// <summary>外部 locale 变化必须重绘当前快照，而不写入第二份设置状态。</summary>
    [Test]
    public void LocaleChange_RerendersCurrentSnapshot()
    {
        var harness = new PresenterHarness(CreateInitialSettings());
        harness.Presenter.Initialize();
        int rendersBefore = harness.View.Models.Count;

        harness.LocaleChanges.Raise();

        Assert.That(harness.View.Models, Has.Count.EqualTo(rendersBefore + 1));
        Assert.That(
            harness.View.Models[harness.View.Models.Count - 1].Settings,
            Is.EqualTo(harness.Service.Current));
    }

    /// <summary>建立一份覆盖首发全部设置域的稳定初值。</summary>
    private static AppSettingsSnapshot CreateInitialSettings(AppResolution? resolution = null)
    {
        return new AppSettingsSnapshot(
            localeCode: AppSettingsSnapshot.EnglishLocaleCode,
            masterVolumePercent: 80,
            displayMode: AppDisplayMode.Windowed,
            resolution: resolution ?? new AppResolution(1920, 1080),
            textScale: AppTextScale.Percent100,
            highContrast: false,
            reducedMotion: false);
    }

    /// <summary>从真实设置 owner 与边界替身组装 Presenter public seam。</summary>
    private sealed class PresenterHarness
    {
        /// <summary>可由测试提交动作并观察渲染的 View。</summary>
        public RecordingView View { get; }

        /// <summary>记录本测试全部耐久尝试的 repository。</summary>
        public RecordingRepository Repository { get; }

        /// <summary>供断言读取唯一当前快照的设置 owner。</summary>
        public AppSettingsService Service { get; }

        /// <summary>记录 owner 实际应用次数的平台边界。</summary>
        public RecordingPlatform Platform { get; }

        /// <summary>模拟 Unity Localization 的语言变化通知。</summary>
        public RecordingLocaleChanges LocaleChanges { get; }

        /// <summary>本测试直接驱动的设置 Presenter。</summary>
        public AppSettingsPresenter Presenter { get; }

        /// <summary>建立已完成 owner 初始化的 Presenter 环境。</summary>
        public PresenterHarness(AppSettingsSnapshot initial)
        {
            View = new RecordingView();
            Repository = new RecordingRepository(initial);
            Platform = new RecordingPlatform();
            LocaleChanges = new RecordingLocaleChanges();
            Service = new AppSettingsService(Repository, Platform);
            Service.Initialize();
            Presenter = new AppSettingsPresenter(
                View,
                Service,
                Platform,
                key => $"localized:{key}",
                LocaleChanges.Subscribe);
        }
    }

    /// <summary>通过 View seam 记录不可变模型并发布封闭动作。</summary>
    private sealed class RecordingView : IAppSettingsView
    {
        /// <summary>按顺序保存全部渲染模型。</summary>
        public List<AppSettingsViewModel> Models { get; } = new List<AppSettingsViewModel>();

        /// <summary>测试通过此事件模拟设置页输入。</summary>
        public event Action<AppSettingsAction> ActionRequested;

        /// <summary>保存 Presenter 交付的完整模型。</summary>
        public void Render(AppSettingsViewModel model)
        {
            Models.Add(model);
        }

        /// <summary>向 Presenter 发布一个玩家动作。</summary>
        public void Request(AppSettingsActionKind kind)
        {
            ActionRequested?.Invoke(new AppSettingsAction(kind));
        }
    }

    /// <summary>以稳定初值模拟应用设置持久化边界。</summary>
    private sealed class RecordingRepository : IAppSettingsRepository
    {
        private readonly AppSettingsSnapshot _initial;

        /// <summary>按顺序记录全部完整候选设置。</summary>
        public List<AppSettingsSnapshot> Commits { get; } = new List<AppSettingsSnapshot>();

        /// <summary>后续提交要返回的类型化结果。</summary>
        public AppSettingsRepositoryCommitResult CommitResult { get; set; } =
            AppSettingsRepositoryCommitResult.Succeeded();

        /// <summary>冻结 owner 启动时读取的设置。</summary>
        public RecordingRepository(AppSettingsSnapshot initial)
        {
            _initial = initial;
        }

        /// <summary>返回稳定初值。</summary>
        public AppSettingsRepositoryLoadResult Load()
        {
            return AppSettingsRepositoryLoadResult.Succeeded(_initial);
        }

        /// <summary>当前 tracer bullet 不触发提交。</summary>
        public AppSettingsRepositoryCommitResult Commit(AppSettingsSnapshot settings)
        {
            Commits.Add(settings);
            return CommitResult;
        }
    }

    /// <summary>提供首发分辨率矩阵并记录 owner 的平台应用。</summary>
    private sealed class RecordingPlatform : IAppSettingsPlatform
    {
        /// <summary>首发设置页循环使用的稳定矩阵。</summary>
        public IReadOnlyList<AppResolution> SupportedResolutions { get; } =
            new[]
            {
                new AppResolution(1280, 720),
                new AppResolution(1920, 1080),
                new AppResolution(2560, 1440),
                new AppResolution(1920, 1200),
                new AppResolution(2560, 1080),
            };

        /// <summary>按顺序记录 owner 实际应用的完整设置。</summary>
        public List<AppSettingsSnapshot> Applied { get; } = new List<AppSettingsSnapshot>();

        /// <summary>后续 Apply 在记录调用后还需抛出的脚本化故障次数。</summary>
        public int FailuresRemaining { get; set; }

        /// <summary>返回测试用安全默认设置。</summary>
        public AppSettingsSnapshot CreateDefaults()
        {
            return CreateInitialSettings();
        }

        /// <summary>只接受首发矩阵中的分辨率。</summary>
        public bool SupportsResolution(AppResolution resolution)
        {
            foreach (AppResolution candidate in SupportedResolutions)
            {
                if (candidate == resolution)
                    return true;
            }

            return false;
        }

        /// <summary>初始化 tracer bullet 不需要额外记录平台副作用。</summary>
        public void Apply(AppSettingsSnapshot settings)
        {
            Applied.Add(settings);
            if (FailuresRemaining <= 0)
                return;

            FailuresRemaining--;
            throw new InvalidOperationException("scripted presenter platform failure");
        }
    }

    /// <summary>以明确订阅 seam 模拟 Unity Localization 变化。</summary>
    private sealed class RecordingLocaleChanges
    {
        private Action _handler;

        /// <summary>保存唯一 Presenter 回调并返回可释放订阅。</summary>
        public IDisposable Subscribe(Action handler)
        {
            _handler += handler ?? throw new ArgumentNullException(nameof(handler));
            return new DelegateDisposable(() => _handler -= handler);
        }

        /// <summary>向所有当前订阅者发布一次语言变化。</summary>
        public void Raise()
        {
            _handler?.Invoke();
        }
    }

    /// <summary>以一次性回调释放测试订阅。</summary>
    private sealed class DelegateDisposable : IDisposable
    {
        private Action _dispose;

        /// <summary>冻结本订阅的释放回调。</summary>
        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        /// <summary>只执行一次释放回调。</summary>
        public void Dispose()
        {
            Action dispose = _dispose;
            _dispose = null;
            dispose?.Invoke();
        }
    }
}
