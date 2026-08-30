using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TinySpire.Presentation.Audio;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public sealed class UiAudioAddressablesG8Tests
{
    /// <summary>UI 音频必须使用固定专用根、专用组与 PackTogether 打包模式。</summary>
    [Test]
    public void Contract_UsesDedicatedRootGroupAndPackTogether()
    {
        Assert.That(AddressablesBuildTools.UiAudioGroupName,
            Is.EqualTo("TinySpire UI Audio"));
        Assert.That(AddressablesBuildTools.UiAudioAssetRoot,
            Is.EqualTo("Assets/Arts/Runtime/Audio/UI"));
        Assert.That(AddressablesBuildTools.UiAudioBundleMode,
            Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackTogether));
    }

    /// <summary>专用 UI 音频组只能把逻辑地址写入 catalog，禁止额外 GUID 或 Label 公钥。</summary>
    [Test]
    public void ConfigureUiAudioCatalogKeys_ExposesAddressOnly()
    {
        var schema = ScriptableObject.CreateInstance<BundledAssetGroupSchema>();
        try
        {
            schema.IncludeAddressInCatalog = false;
            schema.IncludeGUIDInCatalog = true;
            schema.IncludeLabelsInCatalog = true;

            AddressablesBuildTools.ConfigureUiAudioCatalogKeys(schema);

            Assert.That(schema.IncludeAddressInCatalog, Is.True);
            Assert.That(schema.IncludeGUIDInCatalog, Is.False);
            Assert.That(schema.IncludeLabelsInCatalog, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(schema);
        }
    }

    /// <summary>四个有效 WAV 必须解析为目录声明的精确资源路径与逻辑地址。</summary>
    [Test]
    public void ResolveUiAudioEntries_ReturnsExactCatalogSet()
    {
        List<UiAudioAssetDescriptor> descriptors = CreateValidDescriptors();
        descriptors.Add(CreateDescriptor("legacy.wav"));

        IReadOnlyDictionary<string, string> entries =
            AddressablesBuildTools.ResolveUiAudioEntries(descriptors);

        Assert.That(entries, Has.Count.EqualTo(4));
        Assert.That(entries.Keys, Is.EquivalentTo(new[]
        {
            RootPath("hover.wav"),
            RootPath("click.wav"),
            RootPath("confirm.wav"),
            RootPath("error.wav"),
        }));
        Assert.That(entries.Values, Is.EquivalentTo(new[]
        {
            "ui-audio/hover",
            "ui-audio/click",
            "ui-audio/confirm",
            "ui-audio/error",
        }));
        Assert.That(entries.ContainsKey(RootPath("legacy.wav")), Is.False);
    }

    /// <summary>目录缺少任一声明 cue 时必须在修改 Addressables group 前失败。</summary>
    [Test]
    public void ResolveUiAudioEntries_RejectsMissingCatalogCue()
    {
        List<UiAudioAssetDescriptor> descriptors = CreateValidDescriptors();
        descriptors.RemoveAll(descriptor =>
            descriptor.AssetPath.EndsWith("/confirm.wav", StringComparison.Ordinal));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveUiAudioEntries(descriptors));

        StringAssert.Contains("confirm", exception.Message);
    }

    /// <summary>嵌套目录、非 WAV、空白和单文件大小写漂移都必须被专用根门禁拒绝。</summary>
    [TestCase("Nested/hover.wav")]
    [TestCase("hover.mp3")]
    [TestCase("ho ver.wav")]
    [TestCase("Hover.wav")]
    public void ResolveUiAudioEntries_RejectsInvalidPathContract(string relativePath)
    {
        List<UiAudioAssetDescriptor> descriptors = CreateValidDescriptors();
        descriptors[0] = CreateDescriptor(relativePath);

        Assert.Throws<InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveUiAudioEntries(descriptors));
    }

    /// <summary>忽略大小写后的短键重名必须先于地址同步被稳定拒绝。</summary>
    [Test]
    public void ResolveUiAudioEntries_RejectsCaseInsensitiveDuplicate()
    {
        List<UiAudioAssetDescriptor> descriptors = CreateValidDescriptors();
        descriptors.Add(CreateDescriptor("CLICK.wav"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveUiAudioEntries(descriptors));

        StringAssert.Contains("Duplicate", exception.Message);
    }

    /// <summary>非 AudioClip 主资源与无法按 AudioClip 加载两种漂移都必须拒绝。</summary>
    [Test]
    public void ResolveUiAudioEntries_RejectsWrongMainOrLoadType()
    {
        List<UiAudioAssetDescriptor> wrongMainType = CreateValidDescriptors();
        wrongMainType[0] = new UiAudioAssetDescriptor(
            RootPath("hover.wav"),
            typeof(TextAsset),
            loadsAsAudioClip: false,
            preloadAudioData: true);
        List<UiAudioAssetDescriptor> wrongLoadType = CreateValidDescriptors();
        wrongLoadType[0] = new UiAudioAssetDescriptor(
            RootPath("hover.wav"),
            typeof(AudioClip),
            loadsAsAudioClip: false,
            preloadAudioData: true);

        Assert.Throws<InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveUiAudioEntries(wrongMainType));
        Assert.Throws<InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveUiAudioEntries(wrongLoadType));
    }

    /// <summary>关闭 AudioImporter 预加载会把首次播放退化为动态加载，必须在组装前拒绝。</summary>
    [Test]
    public void ResolveUiAudioEntries_RejectsDisabledAudioPreload()
    {
        List<UiAudioAssetDescriptor> descriptors = CreateValidDescriptors();
        descriptors[0] = new UiAudioAssetDescriptor(
            RootPath("hover.wav"),
            typeof(AudioClip),
            loadsAsAudioClip: true,
            preloadAudioData: false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveUiAudioEntries(descriptors));

        StringAssert.Contains("preload", exception.Message.ToLowerInvariant());
    }

    /// <summary>四个原创 cue 的 WAV 字节必须确定、互异并符合 48 kHz 单声道 16-bit PCM 契约。</summary>
    [Test]
    public void Generator_BuildWaveBytesIsDeterministicLayeredPcm()
    {
        var payloads = new Dictionary<UiAudioCue, byte[]>();
        foreach (UiAudioCueDefinition definition in UiAudioCatalog.Ordered)
        {
            byte[] first = UiAudioAssetGenerator.BuildWaveBytes(definition.Cue);
            byte[] second = UiAudioAssetGenerator.BuildWaveBytes(definition.Cue);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(Encoding.ASCII.GetString(first, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(first, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(BitConverter.ToInt16(first, 22), Is.EqualTo(1));
            Assert.That(BitConverter.ToInt32(first, 24), Is.EqualTo(48000));
            Assert.That(BitConverter.ToInt16(first, 34), Is.EqualTo(16));
            Assert.That(BitConverter.ToInt32(first, 40), Is.EqualTo(first.Length - 44));
            payloads.Add(definition.Cue, first);
        }

        Assert.That(payloads.Values
            .Select(payload => Convert.ToBase64String(payload))
            .Distinct(StringComparer.Ordinal)
            .Count(), Is.EqualTo(4));
    }

    /// <summary>创建覆盖目录四项的正确 AudioClip 描述符。</summary>
    private static List<UiAudioAssetDescriptor> CreateValidDescriptors()
    {
        return new List<UiAudioAssetDescriptor>
        {
            CreateDescriptor("hover.wav"),
            CreateDescriptor("click.wav"),
            CreateDescriptor("confirm.wav"),
            CreateDescriptor("error.wav"),
        };
    }

    /// <summary>在专用根下创建一项可按 AudioClip 加载的 WAV 描述符。</summary>
    private static UiAudioAssetDescriptor CreateDescriptor(string relativePath)
    {
        return new UiAudioAssetDescriptor(
            RootPath(relativePath),
            typeof(AudioClip),
            loadsAsAudioClip: true,
            preloadAudioData: true);
    }

    /// <summary>把测试相对路径拼成 Unity 风格专用根路径。</summary>
    private static string RootPath(string relativePath)
    {
        return AddressablesBuildTools.UiAudioAssetRoot + "/" + relativePath;
    }
}
