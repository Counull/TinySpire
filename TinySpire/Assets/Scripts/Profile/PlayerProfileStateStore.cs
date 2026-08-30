using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Profile
{
    /// <summary>Player Profile repository 读取的封闭状态。</summary>
    public enum PlayerProfileRepositoryLoadStatus
    {
        Success,
        NotFound,
        InvalidData,
        InterruptedCommit,
        IoFailure,
    }

    /// <summary>repository 返回的完整教程 Profile 或类型化失败。</summary>
    public sealed class PlayerProfileRepositoryLoadResult
    {
        /// <summary>本次读取状态。</summary>
        public PlayerProfileRepositoryLoadStatus Status { get; }

        /// <summary>成功时读取到的稳定 Profile。</summary>
        public PlayerProfileSnapshot Profile { get; }

        /// <summary>失败或降级时的诊断详情。</summary>
        public string Detail { get; }

        /// <summary>冻结一项 repository 读取结果。</summary>
        private PlayerProfileRepositoryLoadResult(
            PlayerProfileRepositoryLoadStatus status,
            PlayerProfileSnapshot profile,
            string detail)
        {
            Status = status;
            Profile = profile;
            Detail = detail ?? string.Empty;
        }

        /// <summary>创建成功读取结果。</summary>
        public static PlayerProfileRepositoryLoadResult Succeeded(
            PlayerProfileSnapshot profile)
        {
            return new PlayerProfileRepositoryLoadResult(
                PlayerProfileRepositoryLoadStatus.Success,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                string.Empty);
        }

        /// <summary>创建文件尚不存在的安全结果。</summary>
        public static PlayerProfileRepositoryLoadResult NotFound()
        {
            return new PlayerProfileRepositoryLoadResult(
                PlayerProfileRepositoryLoadStatus.NotFound,
                null,
                string.Empty);
        }

        /// <summary>创建坏数据读取结果。</summary>
        public static PlayerProfileRepositoryLoadResult InvalidData(string detail)
        {
            return new PlayerProfileRepositoryLoadResult(
                PlayerProfileRepositoryLoadStatus.InvalidData,
                null,
                detail);
        }

        /// <summary>创建首次提交被临时文件中断的读取结果。</summary>
        public static PlayerProfileRepositoryLoadResult InterruptedCommit(string detail)
        {
            return new PlayerProfileRepositoryLoadResult(
                PlayerProfileRepositoryLoadStatus.InterruptedCommit,
                null,
                detail);
        }

        /// <summary>创建 I/O 失败读取结果。</summary>
        public static PlayerProfileRepositoryLoadResult IoFailure(string detail)
        {
            return new PlayerProfileRepositoryLoadResult(
                PlayerProfileRepositoryLoadStatus.IoFailure,
                null,
                detail);
        }
    }

    /// <summary>Player Profile repository 提交的封闭状态。</summary>
    public enum PlayerProfileRepositoryCommitStatus
    {
        Success,
        VerificationFailure,
        IoFailure,
    }

    /// <summary>repository 提交的类型化结果。</summary>
    public sealed class PlayerProfileRepositoryCommitResult
    {
        /// <summary>本次提交状态。</summary>
        public PlayerProfileRepositoryCommitStatus Status { get; }

        /// <summary>失败时的诊断详情。</summary>
        public string Detail { get; }

        /// <summary>冻结一项 repository 提交结果。</summary>
        private PlayerProfileRepositoryCommitResult(
            PlayerProfileRepositoryCommitStatus status,
            string detail)
        {
            Status = status;
            Detail = detail ?? string.Empty;
        }

        /// <summary>创建成功提交结果。</summary>
        public static PlayerProfileRepositoryCommitResult Succeeded()
        {
            return new PlayerProfileRepositoryCommitResult(
                PlayerProfileRepositoryCommitStatus.Success,
                string.Empty);
        }

        /// <summary>创建写后回读不一致结果。</summary>
        public static PlayerProfileRepositoryCommitResult VerificationFailure(string detail)
        {
            return new PlayerProfileRepositoryCommitResult(
                PlayerProfileRepositoryCommitStatus.VerificationFailure,
                detail);
        }

        /// <summary>创建 I/O 失败提交结果。</summary>
        public static PlayerProfileRepositoryCommitResult IoFailure(string detail)
        {
            return new PlayerProfileRepositoryCommitResult(
                PlayerProfileRepositoryCommitStatus.IoFailure,
                detail);
        }
    }

    /// <summary>versioned player-profile.json 的唯一持久化端口。</summary>
    public interface IPlayerProfileRepository
    {
        /// <summary>读取最近成功提交的完整教程 Profile。</summary>
        PlayerProfileRepositoryLoadResult Load();

        /// <summary>原子提交一份只含教程事实的完整 Profile。</summary>
        PlayerProfileRepositoryCommitResult Commit(PlayerProfileSnapshot profile);
    }

    /// <summary>Player Profile 启动加载后的公开状态。</summary>
    public enum PlayerProfileInitializationStatus
    {
        Loaded,
        NewProfile,
        SuppressedInvalidData,
        SuppressedInterruptedCommit,
        SuppressedIoFailure,
    }

    /// <summary>教程确认、跳过与重置的封闭结果。</summary>
    public enum TutorialProfileActionStatus
    {
        Success,
        AlreadyAcknowledged,
        NotCurrentPrompt,
        TutorialInactive,
        Suppressed,
        Unchanged,
        SaveFailed,
    }

    /// <summary>教程 Profile 唯一 owner；先耐久提交，再发布新快照。</summary>
    public sealed class PlayerProfileStateStore
    {
        private static readonly ReadOnlyCollection<TutorialPromptDefinition> EmptyPrompts =
            Array.AsReadOnly(Array.Empty<TutorialPromptDefinition>());

        private readonly IPlayerProfileRepository _repository;
        private bool _initialized;

        /// <summary>当前最近成功读取或提交的稳定 Profile。</summary>
        public PlayerProfileSnapshot Current { get; private set; }

        /// <summary>坏档或 I/O 失败后是否在本次会话 fail-open 抑制教程。</summary>
        public bool IsTutorialSuppressed { get; private set; }

        /// <summary>仅在耐久提交成功后发布完整新 Profile。</summary>
        public event Action<PlayerProfileSnapshot> Changed;

        /// <summary>以独立 Profile 持久化端口创建教程状态 owner。</summary>
        public PlayerProfileStateStore(IPlayerProfileRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>读取稳定 Profile；坏档、首次中断与 I/O 失败均以不阻挡游戏降级。</summary>
        public PlayerProfileInitializationStatus Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("Player Profile store is already initialized.");

            _initialized = true;
            PlayerProfileRepositoryLoadResult load = _repository.Load()
                ?? throw new InvalidOperationException("Player Profile repository returned null.");

            if (load.Status == PlayerProfileRepositoryLoadStatus.Success &&
                load.Profile != null)
            {
                Current = load.Profile;
                IsTutorialSuppressed = false;
                return PlayerProfileInitializationStatus.Loaded;
            }

            Current = PlayerProfileSnapshot.CreateNew();
            switch (load.Status)
            {
                case PlayerProfileRepositoryLoadStatus.NotFound:
                    IsTutorialSuppressed = false;
                    return PlayerProfileInitializationStatus.NewProfile;
                case PlayerProfileRepositoryLoadStatus.Success:
                case PlayerProfileRepositoryLoadStatus.InvalidData:
                    IsTutorialSuppressed = true;
                    return PlayerProfileInitializationStatus.SuppressedInvalidData;
                case PlayerProfileRepositoryLoadStatus.InterruptedCommit:
                    IsTutorialSuppressed = true;
                    return PlayerProfileInitializationStatus.SuppressedInterruptedCommit;
                case PlayerProfileRepositoryLoadStatus.IoFailure:
                    IsTutorialSuppressed = true;
                    return PlayerProfileInitializationStatus.SuppressedIoFailure;
                default:
                    throw new ArgumentOutOfRangeException(nameof(load.Status), load.Status, null);
            }
        }

        /// <summary>收集当前上下文唯一可见的未确认步骤；跳过、完成或降级时为空。</summary>
        public IReadOnlyList<TutorialPromptDefinition> GetPendingPrompts(
            TutorialContext context)
        {
            EnsureInitialized();
            if (!Enum.IsDefined(typeof(TutorialContext), context))
                throw new ArgumentOutOfRangeException(nameof(context));
            if (IsTutorialSuppressed || Current.TutorialSkipped || Current.TutorialCompleted)
                return EmptyPrompts;

            TutorialPromptDefinition current = Current.CurrentPrompt;
            if (current == null || current.Context != context)
                return EmptyPrompts;

            return Array.AsReadOnly(new[] { current });
        }

        /// <summary>幂等确认当前步骤；重复或乱序确认绝不写盘。</summary>
        public TutorialProfileActionStatus Acknowledge(TutorialPromptId promptId)
        {
            EnsureInitialized();
            if (Current.HasAcknowledged(promptId))
                return TutorialProfileActionStatus.AlreadyAcknowledged;
            if (IsTutorialSuppressed)
                return TutorialProfileActionStatus.Suppressed;
            if (Current.TutorialSkipped || Current.TutorialCompleted)
                return TutorialProfileActionStatus.TutorialInactive;
            if (Current.CurrentPrompt == null || Current.CurrentPrompt.Id != promptId)
                return TutorialProfileActionStatus.NotCurrentPrompt;

            PlayerProfileSnapshot candidate = Current.AcknowledgeCurrent(promptId);
            return CommitAndPublish(candidate);
        }

        /// <summary>显式跳过余下教程；已跳过或全部完成时不重复写盘。</summary>
        public TutorialProfileActionStatus SkipTutorial()
        {
            EnsureInitialized();
            if (Current.TutorialSkipped || Current.TutorialCompleted)
                return TutorialProfileActionStatus.Unchanged;

            PlayerProfileSnapshot candidate = Current.SkipTutorial();
            return CommitAndPublish(candidate);
        }

        /// <summary>把教程重置为首步骤；降级会话也可用本操作尝试修复 Profile。</summary>
        public TutorialProfileActionStatus ResetTutorial()
        {
            EnsureInitialized();
            PlayerProfileSnapshot candidate = PlayerProfileSnapshot.CreateNew();
            if (!IsTutorialSuppressed && Current.Equals(candidate))
                return TutorialProfileActionStatus.Unchanged;

            return CommitAndPublish(candidate);
        }

        /// <summary>先提交完整候选，成功后才替换稳定快照并发布；失败则进入会话级 fail-open。</summary>
        private TutorialProfileActionStatus CommitAndPublish(PlayerProfileSnapshot candidate)
        {
            PlayerProfileRepositoryCommitResult commit = _repository.Commit(candidate)
                ?? throw new InvalidOperationException("Player Profile repository returned null.");
            if (commit.Status != PlayerProfileRepositoryCommitStatus.Success)
            {
                IsTutorialSuppressed = true;
                return TutorialProfileActionStatus.SaveFailed;
            }

            Current = candidate;
            IsTutorialSuppressed = false;
            Changed?.Invoke(candidate);
            return TutorialProfileActionStatus.Success;
        }

        /// <summary>拒绝初始化前读取或变更教程事实。</summary>
        private void EnsureInitialized()
        {
            if (!_initialized || Current == null)
                throw new InvalidOperationException("Player Profile store must be initialized first.");
        }
    }
}
