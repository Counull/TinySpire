using cfg.demo;
using UnityEngine;
using VContainer.Unity;

public sealed class GameLauncher : IStartable
{
    private readonly LubanConfigService _configs;
    private readonly SceneFlowService _sceneFlow;

    public GameLauncher(LubanConfigService configs, SceneFlowService sceneFlow)
    {
        _configs = configs;
        _sceneFlow = sceneFlow;
    }

    public void Start()
    {
        _sceneFlow.LoadInitialScene();
    }
}
