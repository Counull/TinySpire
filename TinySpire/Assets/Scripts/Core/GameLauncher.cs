using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

public sealed class GameLauncher : IStartable
{
	private readonly LubanConfigService _configs;
	private readonly YooAssetPackageService _assets;
	private readonly SceneFlowService _sceneFlow;

	public GameLauncher(LubanConfigService configs, YooAssetPackageService assets, SceneFlowService sceneFlow)
	{
		_configs = configs;
		_assets = assets;
		_sceneFlow = sceneFlow;
	}

	public void Start()
	{
		StartAsync().Forget();
	}

	private async UniTaskVoid StartAsync()
	{
		await _assets.InitializeAsync();
		_configs.Initialize(_assets.Package);
		await _sceneFlow.LoadInitialSceneAsync();
	}
}