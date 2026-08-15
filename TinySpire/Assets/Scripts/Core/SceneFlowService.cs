using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

/// <summary>供 Run 编排替换真实 Addressables 场景切换的最小接口。</summary>
public interface ISceneFlowService
{
    /// <summary>经 LoadingScene 切换到指定稳定场景地址。</summary>
    UniTask LoadSceneWithLoadingAsync(string targetSceneAddress);
}

/// <summary>
/// 场景切换流程：先显示 LoadingScene，再准备并切换到目标场景。
/// Addressables 句柄只存在于本模块内，不向战斗层泄漏。
/// </summary>
public sealed class SceneFlowService : ISceneFlowService
{
    private static readonly TimeSpan MinimumLoadingSceneDuration = TimeSpan.FromSeconds(1);

    private readonly GameStartupOptions _options;
    private readonly AddressableAssetService _assets;

    /// <summary>保存启动地址与 Addressables 资源访问模块。</summary>
    public SceneFlowService(GameStartupOptions options, AddressableAssetService assets)
    {
        _options = options;
        _assets = assets;
    }

    /// <summary>初始化 Addressables 后按启动配置进入首个功能场景。</summary>
    public async UniTask LoadInitialSceneAsync()
    {
        await _assets.InitializeAsync();
        await LoadSceneWithLoadingAsync(_options.InitialSceneAddress);
    }

    /// <summary>先展示最短时长 LoadingScene，再切换到目标场景。</summary>
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

    /// <summary>通过 Addressables Single 模式加载并激活一个场景。</summary>
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
