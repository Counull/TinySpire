using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景切换流程：先显示 LoadingScene，再准备并切换到目标场景。
/// Addressables 句柄只存在于本模块内，不向战斗层泄漏。
/// </summary>
public sealed class SceneFlowService
{
    private static readonly TimeSpan MinimumLoadingSceneDuration = TimeSpan.FromSeconds(1);

    private readonly GameStartupOptions _options;
    private readonly AddressableAssetService _assets;

    public SceneFlowService(GameStartupOptions options, AddressableAssetService assets)
    {
        _options = options;
        _assets = assets;
    }

    public async UniTask LoadInitialSceneAsync()
    {
        await _assets.InitializeAsync();
        await LoadSceneWithLoadingAsync(_options.InitialSceneAddress);
    }

    public async UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
    {
        if (string.IsNullOrWhiteSpace(targetSceneAddress))
            throw new ArgumentException("Target scene address cannot be empty.", nameof(targetSceneAddress));

        await LoadSceneAsync(_options.LoadingSceneAddress);

        Stopwatch loadingSceneTimer = Stopwatch.StartNew();
        await UniTask.NextFrame();

        TimeSpan remainingLoadingTime = MinimumLoadingSceneDuration - loadingSceneTimer.Elapsed;
        if (remainingLoadingTime > TimeSpan.Zero)
            await UniTask.Delay(remainingLoadingTime, ignoreTimeScale: true);

        await LoadSceneAsync(targetSceneAddress);
    }

    private static async UniTask LoadSceneAsync(string address)
    {
        AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(
            address,
            LoadSceneMode.Single,
            SceneReleaseMode.ReleaseSceneWhenSceneUnloaded,
            activateOnLoad: true,
            priority: 100);
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Exception failure = handle.OperationException;
            Addressables.Release(handle);
            throw new InvalidOperationException($"Unable to load scene '{address}'.", failure);
        }
    }
}
