using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using YooAsset;
using YooSceneHandle = YooAsset.SceneHandle;

public sealed class SceneFlowService : IDisposable
{
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
        await _assets.InitializeAsync();
        await LoadSceneWithLoadingAsync(_options.InitialSceneLocation);
    }

    public async UniTask LoadSceneWithLoadingAsync(string targetSceneLocation)
    {
        if (string.IsNullOrWhiteSpace(targetSceneLocation))
            throw new ArgumentException("Target scene location cannot be empty.", nameof(targetSceneLocation));

        await LoadSceneAsync(_options.LoadingSceneLocation);
        await UniTask.NextFrame();

        if (!_gameContentReady)
        {
            await _assets.EnsureAllContentAvailableAsync();
            _gameContentReady = true;
        }

        await LoadSceneAsync(targetSceneLocation);
    }

    private async UniTask LoadSceneAsync(string location)
    {
        YooSceneHandle nextHandle = _assets.Package.LoadSceneAsync(location, LoadSceneMode.Single);
        await nextHandle.Task;

        if (nextHandle.Status != EOperationStatus.Succeed)
        {
            nextHandle.Release();
            throw new InvalidOperationException($"Unable to load scene '{location}': {nextHandle.LastError}");
        }

        YooSceneHandle previousHandle = _activeSceneHandle;
        _activeSceneHandle = nextHandle;
        previousHandle?.Release();
    }

    public void Dispose()
    {
        _activeSceneHandle?.Release();
    }
}
