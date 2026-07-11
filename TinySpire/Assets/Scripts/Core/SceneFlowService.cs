using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public sealed class SceneFlowService : IDisposable
{
    private readonly GameStartupOptions _options;
    private string _nextSceneName;
    private bool _isLoadingTarget;

    public SceneFlowService(GameStartupOptions options)
    {
        _options = options;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void LoadInitialScene() => LoadScene(_options.InitialSceneName);

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            throw new ArgumentException("目标场景名不能为空。", nameof(sceneName));
        }

        _nextSceneName = sceneName;
        SceneManager.LoadSceneAsync(_options.LoadingSceneName, LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == _options.LoadingSceneName && !_isLoadingTarget && !string.IsNullOrEmpty(_nextSceneName))
        {
            LoadTargetSceneAsync().Forget();
        }
    }

    private async UniTask LoadTargetSceneAsync()
    {
        _isLoadingTarget = true;
        string targetSceneName = _nextSceneName;
        await UniTask.Yield();

        var operation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        while (!operation.isDone)
        {
            await UniTask.Yield();
        }

        _nextSceneName = null;
        _isLoadingTarget = false;
    }

    public void Dispose()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
