using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

/// <summary>
/// VContainer 的启动入口。
/// 这里只编排启动顺序，具体资源加载和场景句柄管理交给对应服务。
/// </summary>
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
		// IStartable 不能直接等待 UniTask，因此将异步流程转为后台任务启动。
		StartAsync().Forget();
	}

	private async UniTaskVoid StartAsync()
	{
		// 资源包初始化和配置初始化必须先于场景加载。
		await _assets.InitializeAsync();
		_configs.Initialize(_assets.Package);
		await _sceneFlow.LoadInitialSceneAsync();
	}
}
