using Cysharp.Threading.Tasks;
using VContainer.Unity;

/// <summary>
/// VContainer 的启动入口。
/// 这里只编排启动顺序，具体资源加载和场景句柄管理交给对应服务。
/// </summary>
public sealed class GameLauncher : IStartable
{
	private readonly ConfigService _configs;
	private readonly AddressableAssetService _assets;
	private readonly LocalizationService _localization;
	private readonly SceneFlowService _sceneFlow;

	public GameLauncher(
		ConfigService configs,
		AddressableAssetService assets,
		LocalizationService localization,
		SceneFlowService sceneFlow)
	{
		_configs = configs;
		_assets = assets;
		_localization = localization;
		_sceneFlow = sceneFlow;
	}

	public void Start()
	{
		// IStartable 不能直接等待 UniTask，因此将异步流程转为后台任务启动。
		StartAsync().Forget();
	}

	private async UniTaskVoid StartAsync()
	{
		await _assets.InitializeAsync();
		await _configs.InitializeAsync(_assets);
		await _localization.InitializeAsync();
		await _sceneFlow.LoadInitialSceneAsync();
	}
}
