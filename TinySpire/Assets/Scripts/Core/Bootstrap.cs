using System;
using System.IO;
using UnityEngine;
using TinySpire.Battle;
using TinySpire.Infrastructure.Persistence;
using TinySpire.Presentation.Audio;
using TinySpire.Profile;
using TinySpire.Profile.Presentation;
using TinySpire.Run;
using TinySpire.Run.History;
using TinySpire.Settings;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 游戏启动入口，同时负责注册整个启动流程需要的单例服务。
/// 该对象会跨场景保留，避免切换场景后根服务被销毁。
/// </summary>
public sealed class Bootstrap : LifetimeScope
{
    [SerializeField] private string initialSceneName = "RunEntryScene";
    [SerializeField] private string loadingSceneName = "LoadingScene";

    private BootstrapFailureView _failureView;
    private TutorialGuideOverlayView _tutorialGuideView;
    private AudioSource _uiAudioSource;

    /// <summary>动态确保启动对象持有唯一全局教程层，再建立跨场景根容器。</summary>
    protected override void Awake()
    {
        _failureView = GetComponent<BootstrapFailureView>()
            ?? gameObject.AddComponent<BootstrapFailureView>();
        _tutorialGuideView = EnsureTutorialOverlay(gameObject);
        _uiAudioSource = EnsureUiAudioSource(gameObject);
        DontDestroyOnLoad(gameObject);
        base.Awake();
    }

    /// <summary>注册全局服务、真实教程 View 与单例 Presenter。</summary>
    protected override void Configure(IContainerBuilder builder)
    {
        string windowsLocalApplicationDataPath = ResolveWindowsLocalApplicationDataPath(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("LOCALAPPDATA"),
            Environment.GetEnvironmentVariable("USERPROFILE"));
        string persistenceDirectory = ResolvePersistenceDirectory(
            Application.persistentDataPath,
            windowsLocalApplicationDataPath,
            Application.companyName,
            Application.productName);

        builder.RegisterInstance(new GameStartupOptions(initialSceneName, loadingSceneName));
        builder.Register<AddressableAssetService>(Lifetime.Singleton);
        builder.Register<AddressablesUiAudioClipLoader>(Lifetime.Singleton)
            .As<IUiAudioClipLoader>();
        builder.RegisterInstance(_uiAudioSource);
        builder.Register<UiAudioService>(Lifetime.Singleton)
            .AsSelf()
            .As<IUiAudioPlayer>();
        builder.Register<ConfigService>(Lifetime.Singleton);
        builder.Register<LocalizationService>(Lifetime.Singleton);
        builder.RegisterInstance<IAppSettingsRepository>(
            new AtomicJsonAppSettingsRepository(persistenceDirectory));
        builder.Register<UnityAppSettingsPlatform>(Lifetime.Singleton)
            .AsSelf()
            .As<IAppSettingsPlatform>();
        builder.Register<AppSettingsService>(Lifetime.Singleton);
        builder.RegisterInstance<IPlayerProfileRepository>(
            new AtomicJsonPlayerProfileRepository(persistenceDirectory));
        builder.Register<PlayerProfileStateStore>(Lifetime.Singleton);
        builder.RegisterInstance(_tutorialGuideView)
            .AsSelf()
            .As<ITutorialGuideView>();
        builder.Register<TutorialGuidePresenter>(Lifetime.Singleton);
        builder.Register<SceneFlowService>(Lifetime.Singleton)
            .AsSelf()
            .As<ISceneFlowService>();
        builder.Register<RunStateStore>(Lifetime.Singleton);
        builder.RegisterInstance<IRunHistoryRepository>(
            new AtomicJsonRunHistoryRepository(persistenceDirectory));
        builder.Register<SystemRunHistoryClock>(Lifetime.Singleton)
            .As<IRunHistoryClock>();
        builder.Register<RunHistoryService>(Lifetime.Singleton);
        builder.RegisterInstance<IRunSaveStore>(
            new AtomicJsonRunSaveStore(persistenceDirectory));
        builder.Register<SystemRunEntropySource>(Lifetime.Singleton)
            .As<IRunEntropySource>();
        builder.Register<RunFlowService>(Lifetime.Singleton)
            .AsSelf()
            .As<IBattleSetupOptionsSource>();
        builder.RegisterEntryPoint<TutorialUiAudioPresenter>();
        builder.RegisterEntryPoint<RunOutcomeUiAudioPresenter>();
        builder.RegisterInstance<IBootstrapFailurePresenter>(_failureView);
        builder.RegisterEntryPoint<GameLauncher>();
    }

    /// <summary>按 SpecialFolder、LOCALAPPDATA、USERPROFILE 顺序解析 Windows Local 目录。</summary>
    internal static string ResolveWindowsLocalApplicationDataPath(
        string specialFolderPath,
        string environmentLocalApplicationDataPath,
        string userProfilePath)
    {
        if (!string.IsNullOrWhiteSpace(specialFolderPath))
            return Path.GetFullPath(specialFolderPath);
        if (!string.IsNullOrWhiteSpace(environmentLocalApplicationDataPath))
            return Path.GetFullPath(environmentLocalApplicationDataPath);
        if (!string.IsNullOrWhiteSpace(userProfilePath))
        {
            return Path.GetFullPath(Path.Combine(
                userProfilePath,
                "AppData",
                "Local"));
        }

        throw new InvalidOperationException(
            "Windows local application data directory could not be resolved.");
    }

    /// <summary>优先采用 Unity 路径；Player 启动早期为空时回退到同身份的 Windows LocalLow 目录。</summary>
    internal static string ResolvePersistenceDirectory(
        string unityPersistentDataPath,
        string localApplicationDataPath,
        string companyName,
        string productName)
    {
        if (!string.IsNullOrWhiteSpace(unityPersistentDataPath))
            return Path.GetFullPath(unityPersistentDataPath);

        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
            throw new ArgumentException(
                "Windows local application data directory is required.",
                nameof(localApplicationDataPath));
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));

        var localDirectory = new DirectoryInfo(Path.GetFullPath(localApplicationDataPath));
        if (localDirectory.Parent == null ||
            !string.Equals(localDirectory.Name, "Local", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Windows local application data directory must end with the Local segment.");
        }

        return Path.GetFullPath(Path.Combine(
            localDirectory.Parent.FullName,
            "LocalLow",
            companyName,
            productName));
    }

    /// <summary>复用 Bootstrap 层级中已有 overlay，否则动态挂载唯一实例。</summary>
    internal static TutorialGuideOverlayView EnsureTutorialOverlay(GameObject owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        TutorialGuideOverlayView[] existing =
            owner.GetComponentsInChildren<TutorialGuideOverlayView>(includeInactive: true);
        if (existing.Length > 1)
            throw new InvalidOperationException("Bootstrap must own exactly one tutorial overlay.");

        TutorialGuideOverlayView view = existing.Length == 1
            ? existing[0]
            : owner.AddComponent<TutorialGuideOverlayView>();
        view.EnsureBuilt();
        return view;
    }

    /// <summary>复用 Bootstrap 上唯一的 2D UI AudioSource，否则创建并冻结安全默认值。</summary>
    internal static AudioSource EnsureUiAudioSource(GameObject owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        AudioSource[] existing = owner.GetComponents<AudioSource>();
        if (existing.Length > 1)
            throw new InvalidOperationException("Bootstrap must own at most one UI audio source.");

        AudioSource source = existing.Length == 1
            ? existing[0]
            : owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }
}

/// <summary>
/// 启动流程所需的只读配置，并将场景名转换为稳定的 Addressables 地址。
/// </summary>
public sealed class GameStartupOptions
{
    public string InitialSceneName { get; }
    public string LoadingSceneName { get; }

    public string InitialSceneAddress => ToSceneAddress(InitialSceneName);
    public string LoadingSceneAddress => ToSceneAddress(LoadingSceneName);

    /// <summary>冻结初始场景与 Loading 场景名称。</summary>
    public GameStartupOptions(string initialSceneName, string loadingSceneName)
    {
        InitialSceneName = initialSceneName;
        LoadingSceneName = loadingSceneName;
    }

    /// <summary>把场景名或 Assets 路径规范化为稳定 Addressables 场景地址。</summary>
    private static string ToSceneAddress(string sceneName)
    {
        string path = sceneName.StartsWith("Assets/")
            ? sceneName
            : $"Assets/Scenes/{sceneName}";

        return path.EndsWith(".unity") ? path : $"{path}.unity";
    }
}
