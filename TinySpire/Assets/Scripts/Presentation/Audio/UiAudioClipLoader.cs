using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TinySpire.Presentation.Audio
{
    /// <summary>一次成功 UI 音频加载的 clip 与独立资源生命周期。</summary>
    public interface IUiAudioClipLease : IDisposable
    {
        /// <summary>本 lease 对应的精确逻辑地址。</summary>
        string Address { get; }

        /// <summary>尚未释放时的已加载 clip。</summary>
        AudioClip Clip { get; }
    }

    /// <summary>UiAudioService 唯一可注入的异步 clip 加载边界。</summary>
    public interface IUiAudioClipLoader
    {
        /// <summary>按严格逻辑地址加载一个独立可释放 clip lease。</summary>
        UniTask<IUiAudioClipLease> LoadAsync(string address);
    }

    /// <summary>用 Addressables.LoadAssetAsync 加载并严格持有 AudioClip 句柄。</summary>
    public sealed class AddressablesUiAudioClipLoader : IUiAudioClipLoader
    {
        /// <summary>加载精确 UI 音频地址；失败或空结果立即释放句柄。</summary>
        public async UniTask<IUiAudioClipLease> LoadAsync(string address)
        {
            UiAudioAddress.ValidateAddress(address);
            AsyncOperationHandle<AudioClip> handle =
                Addressables.LoadAssetAsync<AudioClip>(address);
            try
            {
                await handle.Task;
            }
            catch (Exception exception)
            {
                ReleaseFailedHandle(handle);
                throw new InvalidOperationException(
                    $"Failed to load UI audio clip '{address}'.",
                    exception);
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Exception operationException = handle.OperationException;
                ReleaseFailedHandle(handle);
                throw new InvalidOperationException(
                    $"UI audio clip '{address}' did not load successfully.",
                    operationException);
            }

            return new AddressablesUiAudioClipLease(address, handle);
        }

        /// <summary>失败或空结果只在句柄仍有效时释放一次。</summary>
        private static void ReleaseFailedHandle(AsyncOperationHandle<AudioClip> handle)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
    }

    /// <summary>持有一个成功 Addressables AudioClip 句柄直至 Service 释放。</summary>
    internal sealed class AddressablesUiAudioClipLease : IUiAudioClipLease
    {
        private AsyncOperationHandle<AudioClip> _handle;
        private bool _disposed;

        /// <summary>本句柄对应的精确逻辑地址。</summary>
        public string Address { get; }

        /// <summary>尚未释放时返回句柄结果，释放后为空。</summary>
        public AudioClip Clip => _disposed ? null : _handle.Result;

        /// <summary>接管一个已成功且非空的 Addressables 句柄。</summary>
        public AddressablesUiAudioClipLease(
            string address,
            AsyncOperationHandle<AudioClip> handle)
        {
            UiAudioAddress.ValidateAddress(address);
            if (!handle.IsValid() ||
                handle.Status != AsyncOperationStatus.Succeeded ||
                handle.Result == null)
            {
                throw new ArgumentException("UI audio lease requires a successful handle.", nameof(handle));
            }

            Address = address;
            _handle = handle;
        }

        /// <summary>精确释放所持 Addressables 句柄，重复调用保持幂等。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_handle.IsValid())
                Addressables.Release(_handle);
        }
    }
}
