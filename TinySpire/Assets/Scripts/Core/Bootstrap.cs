using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class Bootstrap : LifetimeScope
{
    [SerializeField] private string initialSceneName = "BattleScene";
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [SerializeField] private string packageName = "Main";

    protected override void Awake()
    {
        DontDestroyOnLoad(gameObject);
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(new GameStartupOptions(packageName, initialSceneName, loadingSceneName));
        builder.Register<YooAssetPackageService>(Lifetime.Singleton);
        builder.Register<LubanConfigService>(Lifetime.Singleton);
        builder.Register<SceneFlowService>(Lifetime.Singleton);
        builder.RegisterEntryPoint<GameLauncher>();
    }
}

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
        if (sceneName.StartsWith("Assets/"))
            return sceneName.EndsWith(".unity") ? sceneName[..^6] : sceneName;

        return $"Assets/Scenes/{sceneName}";
    }
}
