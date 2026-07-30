using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 游戏启动入口，同时负责注册整个启动流程需要的单例服务。
/// 该对象会跨场景保留，避免切换场景后根服务被销毁。
/// </summary>
public sealed class Bootstrap : LifetimeScope
{
    [SerializeField] private string initialSceneName = "BattleScene";
    [SerializeField] private string loadingSceneName = "LoadingScene";

    protected override void Awake()
    {
        DontDestroyOnLoad(gameObject);
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(new GameStartupOptions(initialSceneName, loadingSceneName));
        builder.Register<AddressableAssetService>(Lifetime.Singleton);
        builder.Register<ConfigService>(Lifetime.Singleton);
        builder.Register<LocalizationService>(Lifetime.Singleton);
        builder.Register<SceneFlowService>(Lifetime.Singleton);
        builder.RegisterEntryPoint<GameLauncher>();
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

    public GameStartupOptions(string initialSceneName, string loadingSceneName)
    {
        InitialSceneName = initialSceneName;
        LoadingSceneName = loadingSceneName;
    }

    private static string ToSceneAddress(string sceneName)
    {
        string path = sceneName.StartsWith("Assets/")
            ? sceneName
            : $"Assets/Scenes/{sceneName}";

        return path.EndsWith(".unity") ? path : $"{path}.unity";
    }
}
