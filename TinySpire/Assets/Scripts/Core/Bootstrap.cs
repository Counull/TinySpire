using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 游戏启动入口，同时负责注册整个启动流程需要的单例服务。
/// 该对象会跨场景保留，避免切换场景后资源服务和场景句柄被销毁。
/// </summary>
public sealed class Bootstrap : LifetimeScope
{
    // 初始目标场景和过渡用 LoadingScene 的逻辑名称。
    [SerializeField] private string initialSceneName = "BattleScene";
    [SerializeField] private string loadingSceneName = "LoadingScene";

    // YooAsset 资源包名称，必须与构建出来的 Package 名称一致。
    [SerializeField] private string packageName = "Main";

    protected override void Awake()
    {
        // Bootstrap 是全局服务宿主，场景切换时不能销毁。
        DontDestroyOnLoad(gameObject);
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        // 先注册启动参数，再注册依赖这些参数的资源、配置和场景服务。
        builder.RegisterInstance(new GameStartupOptions(packageName, initialSceneName, loadingSceneName));
        builder.Register<YooAssetPackageService>(Lifetime.Singleton);
        builder.Register<LubanConfigService>(Lifetime.Singleton);
        builder.Register<SceneFlowService>(Lifetime.Singleton);
        builder.RegisterEntryPoint<GameLauncher>();
    }
}

/// <summary>
/// 启动流程所需的只读配置，并将场景名转换为 YooAsset 的 location。
/// </summary>
public sealed class GameStartupOptions
{
    public string PackageName { get; }
    public string InitialSceneName { get; }
    public string LoadingSceneName { get; }

    public string InitialSceneLocation => ToSceneLocation(InitialSceneName);
    public string LoadingSceneLocation => ToSceneLocation(LoadingSceneName);

    public GameStartupOptions(string packageName, string initialSceneName, string loadingSceneName)
    {
        PackageName = packageName;
        InitialSceneName = initialSceneName;
        LoadingSceneName = loadingSceneName;
    }

    private static string ToSceneLocation(string sceneName)
    {
        // YooAsset 的场景 location 不带 .unity 扩展名。
        if (sceneName.StartsWith("Assets/"))
            return sceneName.EndsWith(".unity") ? sceneName[..^6] : sceneName;

        return $"Assets/Scenes/{sceneName}";
    }
}
