using System;
using System.Collections.Generic;

namespace TinySpire.Settings
{
    /// <summary>应用设置 repository 读取的封闭状态。</summary>
    public enum AppSettingsRepositoryLoadStatus
    {
        Success,
        NotFound,
        InvalidData,
        IoFailure,
    }

    /// <summary>repository 返回的完整设置或类型化失败。</summary>
    public sealed class AppSettingsRepositoryLoadResult
    {
        /// <summary>本次读取状态。</summary>
        public AppSettingsRepositoryLoadStatus Status { get; }

        /// <summary>成功时读取到的稳定设置。</summary>
        public AppSettingsSnapshot Settings { get; }

        /// <summary>失败或降级时的诊断详情。</summary>
        public string Detail { get; }

        /// <summary>冻结一项 repository 读取结果。</summary>
        private AppSettingsRepositoryLoadResult(
            AppSettingsRepositoryLoadStatus status,
            AppSettingsSnapshot settings,
            string detail)
        {
            Status = status;
            Settings = settings;
            Detail = detail ?? string.Empty;
        }

        /// <summary>创建成功读取结果。</summary>
        public static AppSettingsRepositoryLoadResult Succeeded(AppSettingsSnapshot settings)
        {
            return new AppSettingsRepositoryLoadResult(
                AppSettingsRepositoryLoadStatus.Success,
                settings ?? throw new ArgumentNullException(nameof(settings)),
                string.Empty);
        }

        /// <summary>创建文件尚不存在的安全结果。</summary>
        public static AppSettingsRepositoryLoadResult NotFound()
        {
            return new AppSettingsRepositoryLoadResult(
                AppSettingsRepositoryLoadStatus.NotFound,
                null,
                string.Empty);
        }

        /// <summary>创建坏数据读取结果。</summary>
        public static AppSettingsRepositoryLoadResult InvalidData(string detail)
        {
            return new AppSettingsRepositoryLoadResult(
                AppSettingsRepositoryLoadStatus.InvalidData,
                null,
                detail);
        }

        /// <summary>创建 I/O 失败读取结果。</summary>
        public static AppSettingsRepositoryLoadResult IoFailure(string detail)
        {
            return new AppSettingsRepositoryLoadResult(
                AppSettingsRepositoryLoadStatus.IoFailure,
                null,
                detail);
        }
    }

    /// <summary>应用设置 repository 提交的封闭状态。</summary>
    public enum AppSettingsRepositoryCommitStatus
    {
        Success,
        IoFailure,
    }

    /// <summary>repository 提交的类型化结果。</summary>
    public sealed class AppSettingsRepositoryCommitResult
    {
        /// <summary>本次提交状态。</summary>
        public AppSettingsRepositoryCommitStatus Status { get; }

        /// <summary>失败时的诊断详情。</summary>
        public string Detail { get; }

        /// <summary>冻结一项 repository 提交结果。</summary>
        private AppSettingsRepositoryCommitResult(
            AppSettingsRepositoryCommitStatus status,
            string detail)
        {
            Status = status;
            Detail = detail ?? string.Empty;
        }

        /// <summary>创建成功提交结果。</summary>
        public static AppSettingsRepositoryCommitResult Succeeded()
        {
            return new AppSettingsRepositoryCommitResult(
                AppSettingsRepositoryCommitStatus.Success,
                string.Empty);
        }

        /// <summary>创建 I/O 失败提交结果。</summary>
        public static AppSettingsRepositoryCommitResult IoFailure(string detail)
        {
            return new AppSettingsRepositoryCommitResult(
                AppSettingsRepositoryCommitStatus.IoFailure,
                detail);
        }
    }

    /// <summary>versioned 应用设置持久化的唯一端口。</summary>
    public interface IAppSettingsRepository
    {
        /// <summary>读取最近成功提交的完整设置。</summary>
        AppSettingsRepositoryLoadResult Load();

        /// <summary>原子提交一份完整设置快照。</summary>
        AppSettingsRepositoryCommitResult Commit(AppSettingsSnapshot settings);
    }

    /// <summary>Unity/系统语言、音量和显示调用的唯一外部边界。</summary>
    public interface IAppSettingsPlatform
    {
        /// <summary>首发设置页可选择的冻结分辨率集合。</summary>
        IReadOnlyList<AppResolution> SupportedResolutions { get; }

        /// <summary>从当前平台状态创建安全默认设置。</summary>
        AppSettingsSnapshot CreateDefaults();

        /// <summary>确认当前平台声明支持指定分辨率。</summary>
        bool SupportsResolution(AppResolution resolution);

        /// <summary>把一份已耐久设置实际应用到平台。</summary>
        void Apply(AppSettingsSnapshot settings);
    }

    /// <summary>应用设置启动加载后的公开状态。</summary>
    public enum AppSettingsInitializationStatus
    {
        Loaded,
        DefaultedMissing,
        DefaultedInvalid,
        DefaultedIoFailure,
    }

    /// <summary>玩家设置变更的封闭结果。</summary>
    public enum AppSettingsChangeStatus
    {
        Success,
        Unchanged,
        UnsupportedResolution,
        SaveFailed,
        ApplyFailed,
        RecoveryFailed,
        RecoveryRequired,
    }

    /// <summary>应用设置唯一所有者；只在完整耐久成功后发布和应用新快照。</summary>
    public sealed class AppSettingsService
    {
        private readonly IAppSettingsRepository _repository;
        private readonly IAppSettingsPlatform _platform;
        private bool _initialized;
        private bool _recoveryRequired;

        /// <summary>当前唯一稳定设置；初始化前为空。</summary>
        public AppSettingsSnapshot Current { get; private set; }

        /// <summary>补偿不完整后保持 true；同一 owner 必须 fail-closed，等待进程重建后重载耐久状态。</summary>
        public bool RequiresRecovery => _recoveryRequired;

        /// <summary>成功替换稳定设置后发布完整新快照。</summary>
        public event Action<AppSettingsSnapshot> Changed;

        /// <summary>以持久化端口和平台边界创建设置 owner。</summary>
        public AppSettingsService(
            IAppSettingsRepository repository,
            IAppSettingsPlatform platform)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>读取设置或安全默认值，并在首场景前应用一次。</summary>
        public AppSettingsInitializationStatus Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("App settings service is already initialized.");

            _initialized = true;
            AppSettingsRepositoryLoadResult load = _repository.Load()
                ?? throw new InvalidOperationException("App settings repository returned null.");
            AppSettingsInitializationStatus status;
            if (load.Status == AppSettingsRepositoryLoadStatus.Success &&
                load.Settings != null &&
                _platform.SupportsResolution(load.Settings.Resolution))
            {
                Current = load.Settings;
                status = AppSettingsInitializationStatus.Loaded;
            }
            else
            {
                Current = _platform.CreateDefaults()
                    ?? throw new InvalidOperationException("App settings platform returned null defaults.");
                status = ToDefaultedStatus(load.Status);
            }

            _platform.Apply(Current);
            return status;
        }

        /// <summary>先耐久候选设置，成功后才替换唯一快照并应用平台效果。</summary>
        public AppSettingsChangeStatus TryChange(AppSettingsSnapshot candidate)
        {
            if (!_initialized || Current == null)
                throw new InvalidOperationException("App settings service must be initialized first.");
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (_recoveryRequired)
                return AppSettingsChangeStatus.RecoveryRequired;
            if (!_platform.SupportsResolution(candidate.Resolution))
                return AppSettingsChangeStatus.UnsupportedResolution;
            if (Current.Equals(candidate))
                return AppSettingsChangeStatus.Unchanged;

            AppSettingsRepositoryCommitResult commit = _repository.Commit(candidate)
                ?? throw new InvalidOperationException("App settings repository returned null.");
            if (commit.Status != AppSettingsRepositoryCommitStatus.Success)
                return AppSettingsChangeStatus.SaveFailed;

            AppSettingsSnapshot previous = Current;
            try
            {
                _platform.Apply(candidate);
            }
            catch (Exception)
            {
                if (TryRestorePreviousSettings(previous))
                    return AppSettingsChangeStatus.ApplyFailed;

                _recoveryRequired = true;
                return AppSettingsChangeStatus.RecoveryFailed;
            }

            Current = candidate;
            Changed?.Invoke(candidate);
            return AppSettingsChangeStatus.Success;
        }

        /// <summary>候选应用失败后独立补偿持久化与平台状态，只有两侧都恢复才视为完整回滚。</summary>
        private bool TryRestorePreviousSettings(AppSettingsSnapshot previous)
        {
            bool repositoryRestored;
            try
            {
                AppSettingsRepositoryCommitResult rollback = _repository.Commit(previous);
                repositoryRestored =
                    rollback != null &&
                    rollback.Status == AppSettingsRepositoryCommitStatus.Success;
            }
            catch (Exception)
            {
                repositoryRestored = false;
            }

            bool platformRestored;
            try
            {
                _platform.Apply(previous);
                platformRestored = true;
            }
            catch (Exception)
            {
                platformRestored = false;
            }

            return repositoryRestored && platformRestored;
        }

        /// <summary>把 repository 失败分类映射为明确启动降级状态。</summary>
        private static AppSettingsInitializationStatus ToDefaultedStatus(
            AppSettingsRepositoryLoadStatus status)
        {
            switch (status)
            {
                case AppSettingsRepositoryLoadStatus.NotFound:
                    return AppSettingsInitializationStatus.DefaultedMissing;
                case AppSettingsRepositoryLoadStatus.Success:
                case AppSettingsRepositoryLoadStatus.InvalidData:
                    return AppSettingsInitializationStatus.DefaultedInvalid;
                case AppSettingsRepositoryLoadStatus.IoFailure:
                    return AppSettingsInitializationStatus.DefaultedIoFailure;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}
