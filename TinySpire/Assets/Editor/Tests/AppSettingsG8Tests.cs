using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Settings;

public sealed class AppSettingsG8Tests
{
    /// <summary>schema v1 必须无损往返全部已冻结应用设置事实。</summary>
    [Test]
    public void DocumentCodec_RoundTripsSchemaV1()
    {
        var expected = new AppSettingsSnapshot(
            localeCode: "zh-CN",
            masterVolumePercent: 65,
            displayMode: AppDisplayMode.Windowed,
            resolution: new AppResolution(width: 1920, height: 1080),
            textScale: AppTextScale.Percent125,
            highContrast: true,
            reducedMotion: true);

        string json = AppSettingsDocumentCodec.Write(expected);
        AppSettingsDocumentReadResult read = AppSettingsDocumentCodec.Read(json);

        Assert.That(read.Status, Is.EqualTo(AppSettingsDocumentReadStatus.Success));
        Assert.That(read.Settings, Is.EqualTo(expected));
    }

    /// <summary>schema v1 的每个字段必须拒绝 Newtonsoft 能隐式转换的错误 token 类型。</summary>
    [TestCase("schemaVersion", "\"1\"")]
    [TestCase("localeCode", "true")]
    [TestCase("masterVolumePercent", "\"65\"")]
    [TestCase("displayMode", "0")]
    [TestCase("resolutionWidth", "\"1920\"")]
    [TestCase("resolutionHeight", "\"1080\"")]
    [TestCase("textScalePercent", "\"125\"")]
    [TestCase("highContrast", "\"true\"")]
    [TestCase("reducedMotion", "\"true\"")]
    public void DocumentCodec_RejectsImplicitTokenTypeConversions(
        string propertyName,
        string invalidTokenJson)
    {
        JObject document = JObject.Parse(AppSettingsDocumentCodec.Write(CreateSettings()));
        document[propertyName] = JToken.Parse(invalidTokenJson);

        AppSettingsDocumentReadResult read =
            AppSettingsDocumentCodec.Read(document.ToString());

        Assert.That(read.Status, Is.EqualTo(AppSettingsDocumentReadStatus.InvalidDocument));
        StringAssert.Contains(propertyName, read.Detail);
        StringAssert.Contains("invalid type", read.Detail);
    }

    /// <summary>schema v1 必须拒绝未声明的额外属性，避免静默接受拼写漂移或未来字段。</summary>
    [Test]
    public void DocumentCodec_RejectsUnknownAdditionalProperty()
    {
        JObject document = JObject.Parse(AppSettingsDocumentCodec.Write(CreateSettings()));
        document["futureSetting"] = true;

        AppSettingsDocumentReadResult read =
            AppSettingsDocumentCodec.Read(document.ToString());

        Assert.That(read.Status, Is.EqualTo(AppSettingsDocumentReadStatus.InvalidDocument));
        StringAssert.Contains("futureSetting", read.Detail);
    }

    /// <summary>首次启动没有设置文件时必须采用平台安全默认值并实际应用一次。</summary>
    [Test]
    public void Service_MissingDocumentUsesAndAppliesPlatformDefaults()
    {
        AppSettingsSnapshot defaults = CreateDefaults();
        var repository = new RecordingRepository(AppSettingsRepositoryLoadResult.NotFound());
        var platform = new RecordingPlatform(defaults);
        var service = new AppSettingsService(repository, platform);

        AppSettingsInitializationStatus status = service.Initialize();

        Assert.That(status, Is.EqualTo(AppSettingsInitializationStatus.DefaultedMissing));
        Assert.That(service.Current, Is.EqualTo(defaults));
        Assert.That(platform.Applied, Is.EqualTo(new[] { defaults }));
        Assert.That(repository.CommitAttempts, Is.Empty);
    }

    /// <summary>平台应用候选失败时必须补偿写回并恢复旧设置，且不得发布半应用快照。</summary>
    [Test]
    public void Service_PlatformApplyFailureRestoresPreviousStableSnapshot()
    {
        AppSettingsSnapshot previous = CreateDefaults();
        AppSettingsSnapshot candidate = CreateChangedVolumeSettings(previous, 90);
        var repository = new RecordingRepository(
            AppSettingsRepositoryLoadResult.Succeeded(previous));
        var platform = new RecordingPlatform(previous);
        var service = new AppSettingsService(repository, platform);
        var published = new List<AppSettingsSnapshot>();
        service.Changed += published.Add;
        service.Initialize();
        platform.ApplyOutcomes.Enqueue(
            ScriptedPlatformApplyOutcome.FailAfterEffectiveChange);

        AppSettingsChangeStatus status = AppSettingsChangeStatus.Success;
        Assert.DoesNotThrow(() => status = service.TryChange(candidate));

        Assert.That(status, Is.EqualTo(AppSettingsChangeStatus.ApplyFailed));
        Assert.That(service.Current, Is.EqualTo(previous));
        Assert.That(repository.Live, Is.EqualTo(previous));
        Assert.That(platform.Effective, Is.EqualTo(previous));
        CollectionAssert.AreEqual(
            new[] { candidate, previous },
            repository.CommitAttempts);
        CollectionAssert.AreEqual(
            new[] { previous, candidate, previous },
            platform.Applied);
        Assert.That(published, Is.Empty);
    }

    /// <summary>补偿写回失败时必须返回恢复故障，仍保留旧内存快照且继续尝试恢复平台。</summary>
    [Test]
    public void Service_PlatformApplyAndCompensationFailureReturnsRecoveryFailure()
    {
        AppSettingsSnapshot previous = CreateDefaults();
        AppSettingsSnapshot candidate = CreateChangedVolumeSettings(previous, 90);
        var repository = new RecordingRepository(
            AppSettingsRepositoryLoadResult.Succeeded(previous));
        repository.CommitResults.Enqueue(AppSettingsRepositoryCommitResult.Succeeded());
        repository.CommitResults.Enqueue(
            AppSettingsRepositoryCommitResult.IoFailure("scripted compensation failure"));
        var platform = new RecordingPlatform(previous);
        var service = new AppSettingsService(repository, platform);
        var published = new List<AppSettingsSnapshot>();
        service.Changed += published.Add;
        service.Initialize();
        platform.ApplyOutcomes.Enqueue(
            ScriptedPlatformApplyOutcome.FailAfterEffectiveChange);

        AppSettingsChangeStatus status = AppSettingsChangeStatus.Success;
        Assert.DoesNotThrow(() => status = service.TryChange(candidate));

        Assert.That(status, Is.EqualTo(AppSettingsChangeStatus.RecoveryFailed));
        Assert.That(service.Current, Is.EqualTo(previous));
        Assert.That(repository.Live, Is.EqualTo(candidate));
        Assert.That(platform.Effective, Is.EqualTo(previous));
        CollectionAssert.AreEqual(
            new[] { candidate, previous },
            repository.CommitAttempts);
        CollectionAssert.AreEqual(
            new[] { previous, candidate, previous },
            platform.Applied);
        Assert.That(published, Is.Empty);
    }

    /// <summary>平台补偿再次失败时必须进入 fail-closed，后续变更不得继续碰磁盘或平台。</summary>
    [Test]
    public void Service_PlatformCompensationFailureRequiresOwnerRecovery()
    {
        AppSettingsSnapshot previous = CreateDefaults();
        AppSettingsSnapshot candidate = CreateChangedVolumeSettings(previous, 90);
        AppSettingsSnapshot laterCandidate = CreateChangedVolumeSettings(previous, 70);
        var repository = new RecordingRepository(
            AppSettingsRepositoryLoadResult.Succeeded(previous));
        var platform = new RecordingPlatform(previous);
        var service = new AppSettingsService(repository, platform);
        var published = new List<AppSettingsSnapshot>();
        service.Changed += published.Add;
        service.Initialize();
        platform.ApplyOutcomes.Enqueue(
            ScriptedPlatformApplyOutcome.FailAfterEffectiveChange);
        platform.ApplyOutcomes.Enqueue(
            ScriptedPlatformApplyOutcome.FailBeforeEffectiveChange);

        AppSettingsChangeStatus failure = AppSettingsChangeStatus.Success;
        Assert.DoesNotThrow(() => failure = service.TryChange(candidate));
        int commitsAfterFailure = repository.CommitAttempts.Count;
        int appliesAfterFailure = platform.Applied.Count;
        AppSettingsChangeStatus blocked = service.TryChange(laterCandidate);

        Assert.That(failure, Is.EqualTo(AppSettingsChangeStatus.RecoveryFailed));
        Assert.That(blocked, Is.EqualTo(AppSettingsChangeStatus.RecoveryRequired));
        Assert.That(service.RequiresRecovery, Is.True);
        Assert.That(service.Current, Is.EqualTo(previous));
        Assert.That(repository.Live, Is.EqualTo(previous));
        Assert.That(platform.Effective, Is.EqualTo(candidate));
        Assert.That(repository.CommitAttempts, Has.Count.EqualTo(commitsAfterFailure));
        Assert.That(platform.Applied, Has.Count.EqualTo(appliesAfterFailure));
        CollectionAssert.AreEqual(
            new[] { candidate, previous },
            repository.CommitAttempts);
        CollectionAssert.AreEqual(
            new[] { previous, candidate, previous },
            platform.Applied);
        Assert.That(published, Is.Empty);
    }

    /// <summary>首发 Player 只公开计划冻结的五组 16:9、16:10 与 21:9 分辨率。</summary>
    [Test]
    public void UnityPlatform_ExposesFrozenLaunchResolutionMatrix()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                new AppResolution(1280, 720),
                new AppResolution(1920, 1080),
                new AppResolution(2560, 1440),
                new AppResolution(1920, 1200),
                new AppResolution(2560, 1080),
            },
            UnityAppSettingsPlatform.LaunchResolutions);
    }

    /// <summary>Player 启动早期拿不到 Unity 路径时必须回退到同身份的 Windows LocalLow 目录。</summary>
    [Test]
    public void BootstrapPersistenceDirectory_EmptyUnityPathUsesWindowsLocalLow()
    {
        string localApplicationData = @"C:\Users\Tester\AppData\Local";

        string resolved = Bootstrap.ResolvePersistenceDirectory(
            unityPersistentDataPath: string.Empty,
            localApplicationDataPath: localApplicationData,
            companyName: "DefaultCompany",
            productName: "TinySpire");

        Assert.That(
            resolved,
            Is.EqualTo(Path.GetFullPath(
                @"C:\Users\Tester\AppData\LocalLow\DefaultCompany\TinySpire")));
    }

    /// <summary>IL2CPP 启动早期 SpecialFolder 为空时必须采用同进程 LOCALAPPDATA。</summary>
    [Test]
    public void BootstrapLocalApplicationData_EmptySpecialFolderUsesEnvironmentVariable()
    {
        string resolved = Bootstrap.ResolveWindowsLocalApplicationDataPath(
            specialFolderPath: string.Empty,
            environmentLocalApplicationDataPath: @"C:\Users\Tester\AppData\Local",
            userProfilePath: @"C:\Users\Tester");

        Assert.That(
            resolved,
            Is.EqualTo(Path.GetFullPath(@"C:\Users\Tester\AppData\Local")));
    }

    /// <summary>建立首发矩阵内的一份稳定平台默认设置。</summary>
    private static AppSettingsSnapshot CreateDefaults()
    {
        return new AppSettingsSnapshot(
            localeCode: "en",
            masterVolumePercent: 80,
            displayMode: AppDisplayMode.BorderlessFullscreen,
            resolution: new AppResolution(width: 1920, height: 1080),
            textScale: AppTextScale.Percent100,
            highContrast: false,
            reducedMotion: false);
    }

    /// <summary>建立用于 codec 契约验证的非默认完整设置。</summary>
    private static AppSettingsSnapshot CreateSettings()
    {
        return new AppSettingsSnapshot(
            localeCode: "zh-CN",
            masterVolumePercent: 65,
            displayMode: AppDisplayMode.Windowed,
            resolution: new AppResolution(width: 1920, height: 1080),
            textScale: AppTextScale.Percent125,
            highContrast: true,
            reducedMotion: true);
    }

    /// <summary>只替换主音量，建立同一稳定设置上的变更候选。</summary>
    private static AppSettingsSnapshot CreateChangedVolumeSettings(
        AppSettingsSnapshot previous,
        int masterVolumePercent)
    {
        return new AppSettingsSnapshot(
            previous.LocaleCode,
            masterVolumePercent,
            previous.DisplayMode,
            previous.Resolution,
            previous.TextScale,
            previous.HighContrast,
            previous.ReducedMotion);
    }

    /// <summary>以脚本化读取结果记录所有设置提交尝试。</summary>
    private sealed class RecordingRepository : IAppSettingsRepository
    {
        private readonly AppSettingsRepositoryLoadResult _loadResult;

        /// <summary>按调用顺序保存完整候选快照。</summary>
        public List<AppSettingsSnapshot> CommitAttempts { get; } =
            new List<AppSettingsSnapshot>();

        /// <summary>需要覆盖默认成功时按顺序提供脚本化提交结果。</summary>
        public Queue<AppSettingsRepositoryCommitResult> CommitResults { get; } =
            new Queue<AppSettingsRepositoryCommitResult>();

        /// <summary>模拟磁盘最近一次成功原子替换后的真实完整快照。</summary>
        public AppSettingsSnapshot Live { get; private set; }

        /// <summary>冻结本测试每次 Load 返回的结果。</summary>
        public RecordingRepository(AppSettingsRepositoryLoadResult loadResult)
        {
            _loadResult = loadResult;
            Live = loadResult.Status == AppSettingsRepositoryLoadStatus.Success
                ? loadResult.Settings
                : null;
        }

        /// <summary>返回脚本化读取结果。</summary>
        public AppSettingsRepositoryLoadResult Load()
        {
            return _loadResult;
        }

        /// <summary>记录候选并模拟成功提交。</summary>
        public AppSettingsRepositoryCommitResult Commit(AppSettingsSnapshot settings)
        {
            CommitAttempts.Add(settings);
            AppSettingsRepositoryCommitResult result = CommitResults.Count > 0
                ? CommitResults.Dequeue()
                : AppSettingsRepositoryCommitResult.Succeeded();
            if (result.Status == AppSettingsRepositoryCommitStatus.Success)
                Live = settings;

            return result;
        }
    }

    /// <summary>记录应用设置的系统边界，并提供一份稳定默认值。</summary>
    private sealed class RecordingPlatform : IAppSettingsPlatform
    {
        private readonly AppSettingsSnapshot _defaults;

        /// <summary>按顺序记录每次实际应用的完整快照。</summary>
        public List<AppSettingsSnapshot> Applied { get; } =
            new List<AppSettingsSnapshot>();

        /// <summary>按顺序脚本化每次平台应用的成功或故障时点。</summary>
        public Queue<ScriptedPlatformApplyOutcome> ApplyOutcomes { get; } =
            new Queue<ScriptedPlatformApplyOutcome>();

        /// <summary>模拟平台当前真正生效的完整设置。</summary>
        public AppSettingsSnapshot Effective { get; private set; }

        /// <summary>本测试平台只公开默认分辨率。</summary>
        public IReadOnlyList<AppResolution> SupportedResolutions =>
            new[] { _defaults.Resolution };

        /// <summary>冻结平台默认快照。</summary>
        public RecordingPlatform(AppSettingsSnapshot defaults)
        {
            _defaults = defaults;
        }

        /// <summary>返回平台当前安全默认值。</summary>
        public AppSettingsSnapshot CreateDefaults()
        {
            return _defaults;
        }

        /// <summary>本测试平台只声明默认分辨率受支持。</summary>
        public bool SupportsResolution(AppResolution resolution)
        {
            return resolution == _defaults.Resolution;
        }

        /// <summary>记录实际应用的完整快照。</summary>
        public void Apply(AppSettingsSnapshot settings)
        {
            Applied.Add(settings);
            ScriptedPlatformApplyOutcome outcome = ApplyOutcomes.Count > 0
                ? ApplyOutcomes.Dequeue()
                : ScriptedPlatformApplyOutcome.Success;
            if (outcome == ScriptedPlatformApplyOutcome.FailBeforeEffectiveChange)
                throw new InvalidOperationException("scripted platform apply failure before change");

            Effective = settings;
            if (outcome == ScriptedPlatformApplyOutcome.FailAfterEffectiveChange)
                throw new InvalidOperationException("scripted platform apply failure after change");
        }
    }

    /// <summary>测试平台可在生效前或生效后注入一次明确故障。</summary>
    private enum ScriptedPlatformApplyOutcome
    {
        Success,
        FailBeforeEffectiveChange,
        FailAfterEffectiveChange,
    }
}
