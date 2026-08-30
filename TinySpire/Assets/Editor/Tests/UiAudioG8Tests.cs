using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TinySpire.Presentation.Audio;
using UnityEngine;

public sealed class UiAudioG8Tests
{
    /// <summary>稳定短键必须精确转换为 ui-audio 地址，并拒绝路径、扩展名、空白与大小写漂移。</summary>
    [Test]
    public void Address_FromKeyUsesStrictLogicalAddressContract()
    {
        Assert.That(UiAudioAddress.FromKey(UiAudioCueKeys.Hover),
            Is.EqualTo("ui-audio/hover"));
        Assert.That(UiAudioAddress.FromKey(UiAudioCueKeys.Click),
            Is.EqualTo("ui-audio/click"));
        Assert.That(UiAudioAddress.FromKey(UiAudioCueKeys.Confirm),
            Is.EqualTo("ui-audio/confirm"));
        Assert.That(UiAudioAddress.FromKey(UiAudioCueKeys.Error),
            Is.EqualTo("ui-audio/error"));

        string[] invalidKeys =
        {
            string.Empty,
            " ",
            " click",
            "click ",
            "cli ck",
            "Click",
            "folder/click",
            "folder\\click",
            "click.wav",
            "click?",
        };
        foreach (string invalidKey in invalidKeys)
            Assert.Throws<ArgumentException>(() => UiAudioAddress.FromKey(invalidKey));

        Assert.Throws<ArgumentNullException>(() => UiAudioAddress.FromKey(null));
        Assert.Throws<ArgumentException>(() =>
            UiAudioAddress.ValidateAddress("UI-AUDIO/click"));
        Assert.Throws<ArgumentException>(() =>
            UiAudioAddress.ValidateAddress("ui-audio/click.wav"));
    }

    /// <summary>初始化必须按声明顺序加载完整 cue 集合，不能按首次播放懒加载。</summary>
    [Test]
    public async Task InitializeAsync_LoadsCompleteDeclaredCueSet()
    {
        var loader = new RecordingClipLoader();
        var output = new RecordingOutput();
        var service = new UiAudioService(loader, output);
        try
        {
            await service.InitializeAsync();

            Assert.That(loader.RequestedAddresses,
                Is.EqualTo(UiAudioCatalog.Ordered
                    .Select(definition => definition.Address)
                    .ToArray()));
            Assert.That(loader.Leases, Has.Count.EqualTo(UiAudioCatalog.Ordered.Count));
            Assert.That(output.Played, Is.Empty);
        }
        finally
        {
            service.Dispose();
        }
    }

    /// <summary>播放必须把精确 cue 对应的已加载 clip 送入唯一输出，不得串键或重新加载。</summary>
    [Test]
    public async Task Play_UsesExactPreloadedCueOnSingleOutput()
    {
        var loader = new RecordingClipLoader();
        var output = new RecordingOutput();
        var service = new UiAudioService(loader, output);
        try
        {
            await service.InitializeAsync();
            AudioClip expected = loader.Leases["ui-audio/error"].Clip;

            service.Play(UiAudioCue.Error);

            Assert.That(output.Played, Is.EqualTo(new[] { expected }));
            Assert.That(loader.RequestedAddresses, Has.Count.EqualTo(4));
        }
        finally
        {
            service.Dispose();
        }
    }

    /// <summary>任一声明 cue 缺失必须令完整初始化失败并释放此前全部成功 lease。</summary>
    [Test]
    public void InitializeAsync_MissingClipFailsFastAndReleasesPartialSet()
    {
        var loader = new RecordingClipLoader(missingAddress: "ui-audio/confirm");
        var service = new UiAudioService(loader, new RecordingOutput());

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.InitializeAsync());

        Assert.That(loader.RequestedAddresses,
            Is.EqualTo(new[]
            {
                "ui-audio/hover",
                "ui-audio/click",
                "ui-audio/confirm",
            }));
        Assert.That(loader.Leases.Values.All(lease => lease.DisposeCount == 1), Is.True);
        Assert.Throws<InvalidOperationException>(() => service.Play(UiAudioCue.Hover));
        service.Dispose();
    }

    /// <summary>释放 Service 必须逐一释放全部句柄且保持幂等，释放后禁止继续播放。</summary>
    [Test]
    public async Task Dispose_ReleasesEveryLeaseExactlyOnceAndStopsUse()
    {
        var loader = new RecordingClipLoader();
        var service = new UiAudioService(loader, new RecordingOutput());
        await service.InitializeAsync();

        service.Dispose();
        service.Dispose();

        Assert.That(loader.Leases, Has.Count.EqualTo(4));
        Assert.That(loader.Leases.Values.All(lease => lease.DisposeCount == 1), Is.True);
        Assert.Throws<ObjectDisposedException>(() => service.Play(UiAudioCue.Click));
    }

    /// <summary>按地址生成测试 AudioClip，并记录所有加载与释放事实。</summary>
    private sealed class RecordingClipLoader : IUiAudioClipLoader
    {
        private readonly string _missingAddress;

        /// <summary>按调用顺序记录完整逻辑地址。</summary>
        public List<string> RequestedAddresses { get; } = new List<string>();

        /// <summary>按地址保存本测试返回的每个独立 lease。</summary>
        public Dictionary<string, RecordingClipLease> Leases { get; } =
            new Dictionary<string, RecordingClipLease>(StringComparer.Ordinal);

        /// <summary>可选指定一个返回 null clip 的缺失地址。</summary>
        public RecordingClipLoader(string missingAddress = null)
        {
            _missingAddress = missingAddress;
        }

        /// <summary>返回一个可观察释放次数的异步 clip lease。</summary>
        public UniTask<IUiAudioClipLease> LoadAsync(string address)
        {
            RequestedAddresses.Add(address);
            AudioClip clip = string.Equals(address, _missingAddress, StringComparison.Ordinal)
                ? null
                : AudioClip.Create(address, lengthSamples: 1, channels: 1, frequency: 44100, stream: false);
            var lease = new RecordingClipLease(address, clip);
            Leases.Add(address, lease);
            return UniTask.FromResult<IUiAudioClipLease>(lease);
        }
    }

    /// <summary>记录一个 clip 的独立释放次数，并清理测试创建的临时 Unity 对象。</summary>
    private sealed class RecordingClipLease : IUiAudioClipLease
    {
        /// <summary>本 lease 对应的精确逻辑地址。</summary>
        public string Address { get; }

        /// <summary>加载成功时的测试 clip；缺失注入时为空。</summary>
        public AudioClip Clip { get; private set; }

        /// <summary>本 lease 的实际释放次数。</summary>
        public int DisposeCount { get; private set; }

        /// <summary>冻结地址与可空测试 clip。</summary>
        public RecordingClipLease(string address, AudioClip clip)
        {
            Address = address;
            Clip = clip;
        }

        /// <summary>首次释放销毁测试 clip，重复释放保持幂等。</summary>
        public void Dispose()
        {
            if (DisposeCount > 0)
                return;

            DisposeCount++;
            if (Clip != null)
                UnityEngine.Object.DestroyImmediate(Clip);
            Clip = null;
        }
    }

    /// <summary>记录单一音频输出收到的精确 clip 顺序。</summary>
    private sealed class RecordingOutput : IUiAudioOutput
    {
        /// <summary>按播放顺序保存全部 clip。</summary>
        public List<AudioClip> Played { get; } = new List<AudioClip>();

        /// <summary>记录一次单输出播放。</summary>
        public void PlayOneShot(AudioClip clip)
        {
            Played.Add(clip);
        }
    }
}
