using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Profile;
using TinySpire.Profile.Presentation;
using TinySpire.Settings;

public sealed class TutorialGuidePresenterG8Tests
{
    /// <summary>Presenter 初始化必须投影当前设置，并在 owner 发布成功变更后立即重投影。</summary>
    [Test]
    public void SettingsProjection_AppliesCurrentAndChangedAccessibility()
    {
        var harness = new PresenterHarness(
            settings: CreateSettings(AppTextScale.Percent125, true, true));

        Assert.That(harness.View.AccessibilityModels, Has.Count.EqualTo(1));
        TutorialGuideAccessibilityViewModel initial = harness.View.AccessibilityModels[0];
        Assert.That(initial.TextScale, Is.EqualTo(AppTextScale.Percent125));
        Assert.That(initial.HighContrast, Is.True);
        Assert.That(initial.ReducedMotion, Is.True);

        AppSettingsChangeStatus status = harness.Settings.TryChange(
            CreateSettings(AppTextScale.Percent100, false, false));

        Assert.That(status, Is.EqualTo(AppSettingsChangeStatus.Success));
        Assert.That(harness.View.AccessibilityModels, Has.Count.EqualTo(2));
        TutorialGuideAccessibilityViewModel changed = harness.View.AccessibilityModels[1];
        Assert.That(changed.TextScale, Is.EqualTo(AppTextScale.Percent100));
        Assert.That(changed.HighContrast, Is.False);
        Assert.That(changed.ReducedMotion, Is.False);
    }

    /// <summary>Presenter 释放后必须解除设置订阅，避免已失效全局层继续接收投影。</summary>
    [Test]
    public void Dispose_UnsubscribesSettingsAccessibilityProjection()
    {
        var harness = new PresenterHarness();
        int rendersBeforeDispose = harness.View.AccessibilityModels.Count;

        harness.Presenter.Dispose();
        AppSettingsChangeStatus status = harness.Settings.TryChange(
            CreateSettings(AppTextScale.Percent125, true, true));

        Assert.That(status, Is.EqualTo(AppSettingsChangeStatus.Success));
        Assert.That(harness.View.AccessibilityModels, Has.Count.EqualTo(rendersBeforeDispose));
    }

    /// <summary>首次观察主菜单上下文必须显示欢迎提示并使用稳定文本键。</summary>
    [Test]
    public void ObserveContext_FirstProfileShowsWelcomePrompt()
    {
        var harness = new PresenterHarness();

        harness.Presenter.ObserveContext(TutorialContext.MainMenu);

        TutorialGuideViewModel model = harness.View.Latest;
        Assert.That(model.IsVisible, Is.True);
        Assert.That(model.BlocksInput, Is.True);
        Assert.That(model.PromptId, Is.EqualTo(TutorialPromptId.MainMenuWelcome));
        Assert.That(model.PromptTextKey, Is.EqualTo("tutorial.guide.main_menu_welcome"));
        Assert.That(model.ConfirmTextKey, Is.EqualTo("tutorial.guide.confirm"));
        Assert.That(model.SkipTextKey, Is.EqualTo("tutorial.guide.skip"));
        Assert.That(model.ResetTextKey, Is.EqualTo("tutorial.guide.reset"));
    }

    /// <summary>上下文与当前有序步骤不匹配时必须投影不阻挡的隐藏模型。</summary>
    [Test]
    public void ObserveContext_MismatchHidesGuideWithoutProfileWrite()
    {
        var harness = new PresenterHarness();

        harness.Presenter.ObserveContext(TutorialContext.Battle);

        Assert.That(harness.View.Latest.IsVisible, Is.False);
        Assert.That(harness.View.Latest.BlocksInput, Is.False);
        Assert.That(harness.View.Latest.PromptId, Is.Null);
        Assert.That(harness.Repository.CommitAttempts, Is.Empty);
    }

    /// <summary>确认当前提示成功后才推进 Profile，并在新上下文显示下一步骤。</summary>
    [Test]
    public void Confirm_AdvancesOnlyDurablePromptAndRendersNextContext()
    {
        var harness = new PresenterHarness();
        harness.Presenter.ObserveContext(TutorialContext.MainMenu);

        harness.View.RequestConfirm();

        Assert.That(harness.Repository.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(
            harness.Store.Current.HasAcknowledged(TutorialPromptId.MainMenuWelcome),
            Is.True);
        Assert.That(harness.View.Latest.IsVisible, Is.False);

        harness.Presenter.ObserveContext(TutorialContext.HeroSelection);

        Assert.That(harness.View.Latest.IsVisible, Is.True);
        Assert.That(harness.View.Latest.PromptId,
            Is.EqualTo(TutorialPromptId.HeroSelection));
        Assert.That(harness.View.Latest.PromptTextKey,
            Is.EqualTo("tutorial.guide.hero_selection"));
    }

    /// <summary>跳过成功后当前及后续上下文都不得再阻断正常输入。</summary>
    [Test]
    public void Skip_HidesGuideForRemainingSessionAndContexts()
    {
        var harness = new PresenterHarness();
        harness.Presenter.ObserveContext(TutorialContext.MainMenu);

        harness.View.RequestSkip();
        harness.Presenter.ObserveContext(TutorialContext.HeroSelection);
        harness.Presenter.ObserveContext(TutorialContext.Battle);

        Assert.That(harness.Repository.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(harness.Store.Current.TutorialSkipped, Is.True);
        Assert.That(harness.View.Latest.IsVisible, Is.False);
        Assert.That(harness.View.Latest.BlocksInput, Is.False);
    }

    /// <summary>已跳过 Profile 重置成功后必须从主菜单首步骤重新显示。</summary>
    [Test]
    public void Reset_RestoresFirstPromptInMainMenuContext()
    {
        var skipped = new PlayerProfileSnapshot(
            tutorialSkipped: true,
            acknowledgedPromptIds: Array.Empty<TutorialPromptId>());
        var harness = new PresenterHarness(skipped);
        harness.Presenter.ObserveContext(TutorialContext.MainMenu);
        Assert.That(harness.View.Latest.IsVisible, Is.False);

        harness.View.RequestReset();

        Assert.That(harness.Repository.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(harness.Store.Current,
            Is.EqualTo(PlayerProfileSnapshot.CreateNew()));
        Assert.That(harness.View.Latest.IsVisible, Is.True);
        Assert.That(harness.View.Latest.PromptId,
            Is.EqualTo(TutorialPromptId.MainMenuWelcome));
    }

    /// <summary>清除非玩法页上下文后重置不得重放旧提示，重新观察玩法页才恢复首步骤。</summary>
    [Test]
    public void ClearContext_ResetStaysHiddenUntilGameplayContextIsObservedAgain()
    {
        var skipped = new PlayerProfileSnapshot(
            tutorialSkipped: true,
            acknowledgedPromptIds: Array.Empty<TutorialPromptId>());
        var harness = new PresenterHarness(skipped);
        harness.Presenter.ObserveContext(TutorialContext.MainMenu);

        harness.Presenter.ClearContext();
        harness.View.RequestReset();

        Assert.That(harness.Repository.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(harness.Store.Current,
            Is.EqualTo(PlayerProfileSnapshot.CreateNew()));
        Assert.That(harness.View.Latest.IsVisible, Is.False);
        Assert.That(harness.View.Latest.BlocksInput, Is.False);

        harness.Presenter.ObserveContext(TutorialContext.MainMenu);

        Assert.That(harness.View.Latest.IsVisible, Is.True);
        Assert.That(harness.View.Latest.PromptId,
            Is.EqualTo(TutorialPromptId.MainMenuWelcome));
    }

    /// <summary>确认写盘失败必须保留旧 Profile，并立即隐藏阻挡层实现 fail-open。</summary>
    [Test]
    public void Confirm_SaveFailureKeepsProfileAndFailsOpen()
    {
        var harness = new PresenterHarness();
        PlayerProfileSnapshot stable = harness.Store.Current;
        harness.Repository.EnqueueCommitResult(
            PlayerProfileRepositoryCommitResult.IoFailure("injected"));
        harness.Presenter.ObserveContext(TutorialContext.MainMenu);

        harness.View.RequestConfirm();

        Assert.That(harness.Repository.CommitAttempts, Has.Count.EqualTo(1));
        Assert.That(harness.Store.Current, Is.SameAs(stable));
        Assert.That(harness.Store.IsTutorialSuppressed, Is.True);
        Assert.That(harness.View.Latest.IsVisible, Is.False);
        Assert.That(harness.View.Latest.BlocksInput, Is.False);
    }

    /// <summary>组装真实 Profile owner 与纯 View 替身供 Presenter seam 验证。</summary>
    private sealed class PresenterHarness
    {
        /// <summary>记录全部 Profile 提交尝试。</summary>
        public RecordingRepository Repository { get; }

        /// <summary>作为 Presenter 唯一事实来源的真实 Profile owner。</summary>
        public PlayerProfileStateStore Store { get; }

        /// <summary>提供当前应用设置并发布成功变更的唯一 owner。</summary>
        public AppSettingsService Settings { get; }

        /// <summary>只记录模型并发布三类允许事件的 View。</summary>
        public RecordingView View { get; }

        /// <summary>本测试直接驱动的教程 Presenter。</summary>
        public TutorialGuidePresenter Presenter { get; }

        /// <summary>以新 Profile 或指定稳定 Profile 建立已初始化环境。</summary>
        public PresenterHarness(
            PlayerProfileSnapshot initial = null,
            AppSettingsSnapshot settings = null)
        {
            Repository = new RecordingRepository(
                initial == null
                    ? PlayerProfileRepositoryLoadResult.NotFound()
                    : PlayerProfileRepositoryLoadResult.Succeeded(initial));
            Store = new PlayerProfileStateStore(Repository);
            Store.Initialize();
            AppSettingsSnapshot initialSettings = settings ??
                CreateSettings(AppTextScale.Percent100, false, false);
            Settings = new AppSettingsService(
                new AppSettingsRepository(initialSettings),
                new AppSettingsPlatform(initialSettings));
            Settings.Initialize();
            View = new RecordingView();
            Presenter = new TutorialGuidePresenter(View, Store, Settings);
            Presenter.Initialize();
        }
    }

    /// <summary>只实现 Confirm、Skip、Reset 三项输入并保存不可变模型。</summary>
    private sealed class RecordingView : ITutorialGuideView
    {
        /// <summary>按顺序保存 Presenter 的全部投影。</summary>
        public List<TutorialGuideViewModel> Models { get; } =
            new List<TutorialGuideViewModel>();

        /// <summary>按顺序保存 Presenter 的全部可访问性投影。</summary>
        public List<TutorialGuideAccessibilityViewModel> AccessibilityModels { get; } =
            new List<TutorialGuideAccessibilityViewModel>();

        /// <summary>返回最近一次完整投影。</summary>
        public TutorialGuideViewModel Latest => Models[Models.Count - 1];

        /// <summary>模拟玩家确认当前提示。</summary>
        public event Action ConfirmRequested;

        /// <summary>模拟玩家跳过余下教程。</summary>
        public event Action SkipRequested;

        /// <summary>模拟玩家重置教程。</summary>
        public event Action ResetRequested;

        /// <summary>记录一份完整教程投影。</summary>
        public void Render(TutorialGuideViewModel model)
        {
            Models.Add(model);
        }

        /// <summary>记录一份不携带玩法事实的教程可访问性投影。</summary>
        public void ApplyAccessibility(TutorialGuideAccessibilityViewModel model)
        {
            AccessibilityModels.Add(model);
        }

        /// <summary>向 Presenter 发布确认事件。</summary>
        public void RequestConfirm()
        {
            ConfirmRequested?.Invoke();
        }

        /// <summary>向 Presenter 发布跳过事件。</summary>
        public void RequestSkip()
        {
            SkipRequested?.Invoke();
        }

        /// <summary>向 Presenter 发布重置事件。</summary>
        public void RequestReset()
        {
            ResetRequested?.Invoke();
        }
    }

    /// <summary>创建覆盖三个教程可访问性字段的稳定设置快照。</summary>
    private static AppSettingsSnapshot CreateSettings(
        AppTextScale textScale,
        bool highContrast,
        bool reducedMotion)
    {
        return new AppSettingsSnapshot(
            AppSettingsSnapshot.EnglishLocaleCode,
            masterVolumePercent: 80,
            AppDisplayMode.Windowed,
            new AppResolution(1920, 1080),
            textScale,
            highContrast,
            reducedMotion);
    }

    /// <summary>为 Presenter 测试提供可提交的内存设置持久化端口。</summary>
    private sealed class AppSettingsRepository : IAppSettingsRepository
    {
        private readonly AppSettingsSnapshot _initial;

        /// <summary>冻结初始化时应被 owner 读取的设置。</summary>
        public AppSettingsRepository(AppSettingsSnapshot initial)
        {
            _initial = initial;
        }

        /// <summary>返回冻结的成功读取结果。</summary>
        public AppSettingsRepositoryLoadResult Load()
        {
            return AppSettingsRepositoryLoadResult.Succeeded(_initial);
        }

        /// <summary>允许 owner 发布测试候选以触发 Changed。</summary>
        public AppSettingsRepositoryCommitResult Commit(AppSettingsSnapshot settings)
        {
            return AppSettingsRepositoryCommitResult.Succeeded();
        }
    }

    /// <summary>为 Presenter 测试提供无副作用的平台设置边界。</summary>
    private sealed class AppSettingsPlatform : IAppSettingsPlatform
    {
        private readonly AppSettingsSnapshot _defaults;

        /// <summary>测试只声明目标 1920x1080 分辨率。</summary>
        public IReadOnlyList<AppResolution> SupportedResolutions { get; } =
            new[] { new AppResolution(1920, 1080) };

        /// <summary>冻结安全默认设置。</summary>
        public AppSettingsPlatform(AppSettingsSnapshot defaults)
        {
            _defaults = defaults;
        }

        /// <summary>返回冻结的默认设置。</summary>
        public AppSettingsSnapshot CreateDefaults()
        {
            return _defaults;
        }

        /// <summary>只接受测试声明的唯一分辨率。</summary>
        public bool SupportsResolution(AppResolution resolution)
        {
            return resolution == SupportedResolutions[0];
        }

        /// <summary>Presenter 测试不需要观察平台副作用。</summary>
        public void Apply(AppSettingsSnapshot settings)
        {
        }
    }

    /// <summary>返回脚本化结果并记录全部完整 Profile 候选。</summary>
    private sealed class RecordingRepository : IPlayerProfileRepository
    {
        private readonly PlayerProfileRepositoryLoadResult _loadResult;
        private readonly Queue<PlayerProfileRepositoryCommitResult> _commitResults =
            new Queue<PlayerProfileRepositoryCommitResult>();

        /// <summary>按调用顺序保存全部提交候选。</summary>
        public List<PlayerProfileSnapshot> CommitAttempts { get; } =
            new List<PlayerProfileSnapshot>();

        /// <summary>冻结启动读取结果。</summary>
        public RecordingRepository(PlayerProfileRepositoryLoadResult loadResult)
        {
            _loadResult = loadResult;
        }

        /// <summary>把下一次提交结果加入脚本队列。</summary>
        public void EnqueueCommitResult(PlayerProfileRepositoryCommitResult result)
        {
            _commitResults.Enqueue(result);
        }

        /// <summary>返回冻结的启动读取结果。</summary>
        public PlayerProfileRepositoryLoadResult Load()
        {
            return _loadResult;
        }

        /// <summary>记录候选并返回脚本化结果。</summary>
        public PlayerProfileRepositoryCommitResult Commit(PlayerProfileSnapshot profile)
        {
            CommitAttempts.Add(profile);
            return _commitResults.Count > 0
                ? _commitResults.Dequeue()
                : PlayerProfileRepositoryCommitResult.Succeeded();
        }
    }
}
