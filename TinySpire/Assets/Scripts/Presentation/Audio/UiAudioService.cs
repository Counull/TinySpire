using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TinySpire.Presentation.Audio
{
    /// <summary>Presenter 唯一依赖的类型化 UI 音频播放端口。</summary>
    public interface IUiAudioPlayer
    {
        /// <summary>播放一个稳定 cue，不向调用方暴露 AudioClip 或 AudioSource。</summary>
        void Play(UiAudioCue cue);
    }

    /// <summary>隔离单一 AudioSource 的可测试 UI 音频输出。</summary>
    internal interface IUiAudioOutput
    {
        /// <summary>在唯一输出上播放一个已加载 clip。</summary>
        void PlayOneShot(AudioClip clip);
    }

    /// <summary>复用一个既有 AudioSource 播放全部 UI cue，不拥有音量事实。</summary>
    internal sealed class AudioSourceUiAudioOutput : IUiAudioOutput
    {
        private readonly AudioSource _audioSource;

        /// <summary>接管一个由外层生命周期持有的单一 AudioSource 引用。</summary>
        public AudioSourceUiAudioOutput(AudioSource audioSource)
        {
            _audioSource = audioSource != null
                ? audioSource
                : throw new ArgumentNullException(nameof(audioSource));
        }

        /// <summary>经 AudioSource.PlayOneShot 播放，不改 source 或 AudioListener 音量。</summary>
        public void PlayOneShot(AudioClip clip)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            _audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>完整预加载声明集合并在单一 AudioSource 上路由 UI cue 的可释放深模块。</summary>
    public sealed class UiAudioService : IUiAudioPlayer, IDisposable
    {
        private readonly IUiAudioClipLoader _loader;
        private readonly IUiAudioOutput _output;
        private readonly Dictionary<UiAudioCue, IUiAudioClipLease> _leases =
            new Dictionary<UiAudioCue, IUiAudioClipLease>();

        private bool _initialized;
        private bool _initializing;
        private bool _disposed;

        /// <summary>以可注入 loader 和外层唯一 AudioSource 创建生产 Service。</summary>
        public UiAudioService(IUiAudioClipLoader loader, AudioSource audioSource)
            : this(loader, new AudioSourceUiAudioOutput(audioSource))
        {
        }

        /// <summary>以可控 loader 与单输出 seam 创建 EditMode 可验证 Service。</summary>
        internal UiAudioService(IUiAudioClipLoader loader, IUiAudioOutput output)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>按目录顺序完整加载全部声明 cue；任一失败释放部分结果并 fail-fast。</summary>
        public async UniTask InitializeAsync()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;
            if (_initializing)
                throw new InvalidOperationException("UI audio service initialization is already in progress.");

            _initializing = true;
            try
            {
                for (int index = 0; index < UiAudioCatalog.Ordered.Count; index++)
                {
                    UiAudioCueDefinition definition = UiAudioCatalog.Ordered[index];
                    IUiAudioClipLease lease;
                    try
                    {
                        lease = await _loader.LoadAsync(definition.Address);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException(
                            $"Unable to initialize UI audio cue '{definition.Key}'.",
                            exception);
                    }

                    if (_disposed)
                    {
                        lease?.Dispose();
                        throw new ObjectDisposedException(nameof(UiAudioService));
                    }
                    if (lease == null)
                    {
                        throw new InvalidOperationException(
                            $"UI audio loader returned no lease for '{definition.Address}'.");
                    }
                    if (!string.Equals(lease.Address, definition.Address, StringComparison.Ordinal))
                    {
                        lease.Dispose();
                        throw new InvalidOperationException(
                            $"UI audio loader returned mismatched address '{lease.Address}'.");
                    }
                    if (lease.Clip == null)
                    {
                        lease.Dispose();
                        throw new InvalidOperationException(
                            $"UI audio loader returned no clip for '{definition.Address}'.");
                    }

                    _leases.Add(definition.Cue, lease);
                }

                _initialized = true;
            }
            catch
            {
                ReleaseAllLeases();
                throw;
            }
            finally
            {
                _initializing = false;
            }
        }

        /// <summary>把精确 cue 的已加载 clip 送入唯一输出；未初始化或缺失立即失败。</summary>
        public void Play(UiAudioCue cue)
        {
            ThrowIfDisposed();
            if (!_initialized)
                throw new InvalidOperationException("UI audio service must be initialized before playback.");
            if (!UiAudioCatalog.TryGet(cue, out _))
                throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            if (!_leases.TryGetValue(cue, out IUiAudioClipLease lease) || lease.Clip == null)
                throw new InvalidOperationException($"UI audio cue '{cue}' is not loaded.");

            _output.PlayOneShot(lease.Clip);
        }

        /// <summary>释放全部 clip lease；不销毁外层 AudioSource，也不改任何音量 owner。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _initialized = false;
            ReleaseAllLeases();
        }

        /// <summary>逐一释放当前完整或部分加载结果并清空索引。</summary>
        private void ReleaseAllLeases()
        {
            foreach (IUiAudioClipLease lease in _leases.Values)
                lease.Dispose();

            _leases.Clear();
        }

        /// <summary>拒绝释放后的初始化与播放调用。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UiAudioService));
        }
    }
}
