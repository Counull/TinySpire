using System;
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
	private readonly IBootstrapFailurePresenter _failurePresenter;

	public GameLauncher(
		ConfigService configs,
		AddressableAssetService assets,
		LocalizationService localization,
		SceneFlowService sceneFlow,
		IBootstrapFailurePresenter failurePresenter)
	{
		_configs = configs;
		_assets = assets;
		_localization = localization;
		_sceneFlow = sceneFlow;
		_failurePresenter = failurePresenter;
	}

	public void Start()
	{
		// IStartable 不能直接等待 UniTask，因此将异步流程转为后台任务启动。
		StartAsync().Forget();
	}

	private async UniTaskVoid StartAsync()
	{
		await RunStartupAsync(
			_assets.InitializeAsync,
			() => _configs.InitializeAsync(_assets),
			_localization.InitializeAsync,
			_sceneFlow.LoadInitialSceneAsync,
			_failurePresenter.ShowConfigurationFailure);
	}

	/// <summary>
	/// 串行编排 Bootstrap 启动；只截获 ConfigService 的 typed failure，其他异常保持原样上抛。
	/// </summary>
	internal static async UniTask RunStartupAsync(
		Func<UniTask> initializeAssets,
		Func<UniTask> initializeConfiguration,
		Func<UniTask> initializeLocalization,
		Func<UniTask> loadInitialScene,
		Action<ConfigInitializationException> showConfigurationFailure)
	{
		if (initializeAssets == null)
			throw new ArgumentNullException(nameof(initializeAssets));
		if (initializeConfiguration == null)
			throw new ArgumentNullException(nameof(initializeConfiguration));
		if (initializeLocalization == null)
			throw new ArgumentNullException(nameof(initializeLocalization));
		if (loadInitialScene == null)
			throw new ArgumentNullException(nameof(loadInitialScene));
		if (showConfigurationFailure == null)
			throw new ArgumentNullException(nameof(showConfigurationFailure));

		await initializeAssets();
		try
		{
			await initializeConfiguration();
		}
		catch (ConfigInitializationException failure)
		{
			showConfigurationFailure(failure);
			return;
		}

		await initializeLocalization();
		await loadInitialScene();
	}
}
