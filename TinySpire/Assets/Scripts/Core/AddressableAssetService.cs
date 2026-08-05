using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressables 的最小运行时边界。
/// 上层只取得已经复制出的文本，不持有 Addressables 句柄或资源生命周期状态。
/// </summary>
public sealed class AddressableAssetService : IConfigTextLoader
{
    private bool _initialized;

    public async UniTask InitializeAsync()
    {
        if (_initialized)
            return;

        AsyncOperationHandle handle = Addressables.InitializeAsync(autoReleaseHandle: false);
        try
        {
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException("Unable to initialize Addressables.", handle.OperationException);

            _initialized = true;
        }
        finally
        {
            Addressables.Release(handle);
        }
    }

    public async UniTask<string> LoadTextAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address cannot be empty.", nameof(address));

        await InitializeAsync();

        AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(address);
        try
        {
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"Unable to load text asset '{address}'.", handle.OperationException);
            if (handle.Result == null)
                throw new InvalidOperationException($"Text asset '{address}' loaded as null.");

            return handle.Result.text;
        }
        finally
        {
            Addressables.Release(handle);
        }
    }
}
