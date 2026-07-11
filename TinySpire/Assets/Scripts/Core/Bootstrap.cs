using UnityEngine;
using VContainer;
using VContainer.Unity;

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
        builder.Register<LubanConfigService>(Lifetime.Singleton);
        builder.RegisterInstance(new GameStartupOptions(initialSceneName, loadingSceneName));
        builder.Register<SceneFlowService>(Lifetime.Singleton);
        builder.RegisterEntryPoint<GameLauncher>();
    }
}

public sealed class GameStartupOptions
{
    public string InitialSceneName { get; }
    public string LoadingSceneName { get; }

    public GameStartupOptions(string initialSceneName, string loadingSceneName)
    {
        InitialSceneName = initialSceneName;
        LoadingSceneName = loadingSceneName;
    }
}
