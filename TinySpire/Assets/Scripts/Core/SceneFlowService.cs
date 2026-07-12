using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using YooAsset;
using YooSceneHandle = YooAsset.SceneHandle;

/// <summary>
/// 场景切换流程：先显示 LoadingScene，再准备并切换到目标场景。
/// 该服务持有当前场景的 YooAsset 句柄，保证场景 Bundle 在使用期间不会被释放。
/// </summary>
public sealed class SceneFlowService : IDisposable
{
    // LoadingScene 从进入到目标场景激活，至少保留 1 秒可见时间。
    private static readonly TimeSpan MinimumLoadingSceneDuration = TimeSpan.FromSeconds(1);

    private readonly GameStartupOptions _options;
    private readonly YooAssetPackageService _assets;
    private YooSceneHandle _activeSceneHandle;
    private bool _gameContentReady;

    public SceneFlowService(GameStartupOptions options, YooAssetPackageService assets)
    {
        _options = options;
        _assets = assets;
    }

    public async UniTask LoadInitialSceneAsync()
    {
        // 资源初始化通常已经由 GameLauncher 完成；这里再次调用是幂等保护。
        await _assets.InitializeAsync();
        await LoadSceneWithLoadingAsync(_options.InitialSceneLocation);
    }

    public async UniTask LoadSceneWithLoadingAsync(string targetSceneLocation)
    {
        if (string.IsNullOrWhiteSpace(targetSceneLocation))
            throw new ArgumentException("Target scene location cannot be empty.", nameof(targetSceneLocation));

        // 先切入 LoadingScene，后续资源准备工作在该场景显示期间执行。
        await LoadSceneAsync(_options.LoadingSceneLocation);

        // Stopwatch 不受 Time.timeScale 影响，适合计算真实展示时长。
        Stopwatch loadingSceneTimer = Stopwatch.StartNew();
        await UniTask.NextFrame();

        if (!_gameContentReady)
        {
            // 首次进入游戏时准备内容；后续场景切换不重复下载整个 Package。
            await _assets.EnsureAllContentAvailableAsync();
            _gameContentReady = true;
        }

        // 资源准备很快时补足剩余时间，资源准备较慢时不再额外等待 1 秒。
        TimeSpan remainingLoadingTime = MinimumLoadingSceneDuration - loadingSceneTimer.Elapsed;
        if (remainingLoadingTime > TimeSpan.Zero)
            await UniTask.Delay(remainingLoadingTime, ignoreTimeScale: true);

        // LoadingScene 的最短展示时间满足后，才加载并激活目标场景。
        await LoadSceneAsync(targetSceneLocation);
    }

    private async UniTask LoadSceneAsync(string location)
    {
        // YooAsset 内部会先加载场景 Bundle，再通过 Unity 的异步场景 API 激活场景。
        YooSceneHandle nextHandle = _assets.Package.LoadSceneAsync(location, LoadSceneMode.Single);
        await nextHandle.Task;

        // 失败时释放当前句柄，避免错误路径泄漏资源引用。
        if (nextHandle.Status != EOperationStatus.Succeed)
        {
            nextHandle.Release();
            throw new InvalidOperationException($"Unable to load scene '{location}': {nextHandle.LastError}");
        }

        // 只有新场景加载成功后才释放旧场景句柄。
        YooSceneHandle previousHandle = _activeSceneHandle;
        _activeSceneHandle = nextHandle;
        previousHandle?.Release();
    }

    public void Dispose()
    {
        // 容器销毁时释放最后一个场景句柄及其 Bundle 引用。
        _activeSceneHandle?.Release();
    }
}
