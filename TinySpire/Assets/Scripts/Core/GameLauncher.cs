using System;
using Cysharp.Threading.Tasks;
using TinySpire.Presentation.Audio;
using TinySpire.Profile;
using TinySpire.Profile.Presentation;
using TinySpire.Run;
using TinySpire.Run.History;
using TinySpire.Settings;
using VContainer.Unity;

/// <summary>
/// VContainer 的启动入口。
/// 这里只编排启动顺序，具体资源加载和场景句柄管理交给对应服务。
/// </summary>
public sealed class GameLauncher : IStartable
{
	private readonly ConfigService _configs;
	private readonly AddressableAssetService _assets;
	private readonly UiAudioService _uiAudio;
	private readonly LocalizationService _localization;
	private readonly AppSettingsService _appSettings;
	private readonly PlayerProfileStateStore _playerProfile;
	private readonly TutorialGuideOverlayView _tutorialGuideView;
	private readonly TutorialGuidePresenter _tutorialGuidePresenter;
	private readonly RunHistoryService _runHistory;
	private readonly RunStateStore _runState;
	private readonly SceneFlowService _sceneFlow;
	private readonly IBootstrapFailurePresenter _failurePresenter;

	/// <summary>注入启动期唯一服务与全局教程表现 seam。</summary>
	public GameLauncher(
		ConfigService configs,
		AddressableAssetService assets,
		UiAudioService uiAudio,
		LocalizationService localization,
		AppSettingsService appSettings,
		PlayerProfileStateStore playerProfile,
		TutorialGuideOverlayView tutorialGuideView,
		TutorialGuidePresenter tutorialGuidePresenter,
		RunHistoryService runHistory,
		RunStateStore runState,
		SceneFlowService sceneFlow,
		IBootstrapFailurePresenter failurePresenter)
	{
		_configs = configs;
		_assets = assets;
		_uiAudio = uiAudio;
		_localization = localization;
		_appSettings = appSettings;
		_playerProfile = playerProfile;
		_tutorialGuideView = tutorialGuideView;
		_tutorialGuidePresenter = tutorialGuidePresenter;
		_runHistory = runHistory;
		_runState = runState;
		_sceneFlow = sceneFlow;
		_failurePresenter = failurePresenter;
	}

	/// <summary>从同步 VContainer 入口启动唯一异步编排。</summary>
	public void Start()
	{
		// IStartable 不能直接等待 UniTask，因此将异步流程转为后台任务启动。
		StartAsync().Forget();
	}

	/// <summary>严格按依赖顺序初始化根服务，并把配置失败路由到启动失败视图。</summary>
	private async UniTaskVoid StartAsync()
	{
		await RunStartupAsync(
			_assets.InitializeAsync,
			_uiAudio.InitializeAsync,
			() => _configs.InitializeAsync(_assets),
			_localization.InitializeAsync,
			() => _appSettings.Initialize(),
			() => _playerProfile.Initialize(),
			() => _tutorialGuideView.Initialize(_localization),
			_tutorialGuidePresenter.Initialize,
			() => _runHistory.Initialize(_runState),
			_sceneFlow.LoadInitialSceneAsync,
			_failurePresenter.ShowConfigurationFailure);
	}

	/// <summary>
	/// 串行编排 Bootstrap 启动；只截获 ConfigService 的 typed failure，其他异常保持原样上抛。
	/// </summary>
	internal static async UniTask RunStartupAsync(
		Func<UniTask> initializeAssets,
		Func<UniTask> initializeUiAudio,
		Func<UniTask> initializeConfiguration,
		Func<UniTask> initializeLocalization,
		Action initializeAppSettings,
		Action initializePlayerProfile,
		Action initializeTutorialView,
		Action initializeTutorialPresenter,
		Action initializeRunHistory,
		Func<UniTask> loadInitialScene,
		Action<ConfigInitializationException> showConfigurationFailure)
	{
		if (initializeAssets == null)
			throw new ArgumentNullException(nameof(initializeAssets));
		if (initializeUiAudio == null)
			throw new ArgumentNullException(nameof(initializeUiAudio));
		if (initializeConfiguration == null)
			throw new ArgumentNullException(nameof(initializeConfiguration));
		if (initializeLocalization == null)
			throw new ArgumentNullException(nameof(initializeLocalization));
		if (initializeAppSettings == null)
			throw new ArgumentNullException(nameof(initializeAppSettings));
		if (initializePlayerProfile == null)
			throw new ArgumentNullException(nameof(initializePlayerProfile));
		if (initializeTutorialView == null)
			throw new ArgumentNullException(nameof(initializeTutorialView));
		if (initializeTutorialPresenter == null)
			throw new ArgumentNullException(nameof(initializeTutorialPresenter));
		if (initializeRunHistory == null)
			throw new ArgumentNullException(nameof(initializeRunHistory));
		if (loadInitialScene == null)
			throw new ArgumentNullException(nameof(loadInitialScene));
		if (showConfigurationFailure == null)
			throw new ArgumentNullException(nameof(showConfigurationFailure));

		await initializeAssets();
		await initializeUiAudio();
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
		initializeAppSettings();
		initializePlayerProfile();
		initializeTutorialView();
		initializeTutorialPresenter();
		initializeRunHistory();
		await loadInitialScene();
	}
}
