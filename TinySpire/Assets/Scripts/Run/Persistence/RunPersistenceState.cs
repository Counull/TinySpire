using System;

namespace TinySpire.Run
{
    /// <summary>RunFlow 对 UI 公开的单槽发现、校验与提交状态。</summary>
    public enum RunPersistenceStatus
    {
        Unchecked,
        NotFound,
        ContinueAvailable,
        TerminalOutcome,
        TerminalDefeat = TerminalOutcome,
        InvalidJson,
        InvalidDocument,
        UnsupportedSchema,
        MissingHeroTemplate,
        MissingDeckTemplate,
        MissingEncounterTemplate,
        MissingMapProfile,
        InterruptedCommit,
        IoFailure,
        DeleteFailed,
        CommitPending,
        CommitFailed,
    }

    /// <summary>不暴露 IO 或 SDK 的不可变存档可用性投影。</summary>
    public sealed class RunPersistenceState
    {
        /// <summary>当前存档状态分类。</summary>
        public RunPersistenceStatus Status { get; }

        /// <summary>是否可从最近成功的地图检查点继续。</summary>
        public bool CanContinue { get; }

        /// <summary>槽位中是否仍有必须经玩家确认才能删除的数据。</summary>
        public bool HasStoredData { get; }

        /// <summary>是否存在保留作诊断的未发布临时文件。</summary>
        public bool HasPendingTemporaryFile { get; }

        /// <summary>供错误页和诊断记录使用的明确原因。</summary>
        public string Detail { get; }

        /// <summary>配置缺失时供本地化说明使用的稳定类别。</summary>
        public string MissingConfigurationKind { get; }

        /// <summary>配置缺失时供本地化说明使用的稳定 ID。</summary>
        public int? MissingConfigurationId { get; }

        /// <summary>终局槽位恢复出的唯一 outcome 分类，非终局为空。</summary>
        public RunOutcomeKind? OutcomeKind { get; }

        /// <summary>建立一份内部已验证的存档状态投影。</summary>
        private RunPersistenceState(
            RunPersistenceStatus status,
            bool canContinue,
            bool hasStoredData,
            bool hasPendingTemporaryFile,
            string detail,
            string missingConfigurationKind,
            int? missingConfigurationId,
            RunOutcomeKind? outcomeKind)
        {
            if ((status == RunPersistenceStatus.TerminalOutcome) != outcomeKind.HasValue)
            {
                throw new ArgumentException(
                    "Only TerminalOutcome persistence state can carry an outcome kind.",
                    nameof(outcomeKind));
            }

            Status = status;
            CanContinue = canContinue;
            HasStoredData = hasStoredData;
            HasPendingTemporaryFile = hasPendingTemporaryFile;
            Detail = detail ?? string.Empty;
            MissingConfigurationKind = missingConfigurationKind ?? string.Empty;
            MissingConfigurationId = missingConfigurationId;
            OutcomeKind = outcomeKind;
        }

        /// <summary>建立尚未检查当前单槽的初始状态。</summary>
        internal static RunPersistenceState Unchecked()
        {
            return new RunPersistenceState(
                RunPersistenceStatus.Unchecked,
                canContinue: false,
                hasStoredData: false,
                hasPendingTemporaryFile: false,
                detail: string.Empty,
                missingConfigurationKind: null,
                missingConfigurationId: null,
                outcomeKind: null);
        }

        /// <summary>建立确认没有任何单槽数据的状态。</summary>
        internal static RunPersistenceState NotFound()
        {
            return new RunPersistenceState(
                RunPersistenceStatus.NotFound,
                canContinue: false,
                hasStoredData: false,
                hasPendingTemporaryFile: false,
                detail: string.Empty,
                missingConfigurationKind: null,
                missingConfigurationId: null,
                outcomeKind: null);
        }

        /// <summary>建立可继续最近成功检查点的状态。</summary>
        internal static RunPersistenceState Available(
            bool hasPendingTemporaryFile,
            string detail = "")
        {
            return new RunPersistenceState(
                RunPersistenceStatus.ContinueAvailable,
                canContinue: true,
                hasStoredData: true,
                hasPendingTemporaryFile: hasPendingTemporaryFile,
                detail: detail,
                missingConfigurationKind: null,
                missingConfigurationId: null,
                outcomeKind: null);
        }

        /// <summary>建立已恢复唯一 Run outcome 且永久禁止 Continue 的单槽状态。</summary>
        internal static RunPersistenceState TerminalOutcome(
            RunOutcomeKind outcomeKind,
            bool hasPendingTemporaryFile,
            string detail = "")
        {
            return new RunPersistenceState(
                RunPersistenceStatus.TerminalOutcome,
                canContinue: false,
                hasStoredData: true,
                hasPendingTemporaryFile,
                detail,
                missingConfigurationKind: null,
                missingConfigurationId: null,
                outcomeKind);
        }

        /// <summary>把旧失败终局投影到通用 TerminalOutcome，供迁移期调用方继续编译。</summary>
        internal static RunPersistenceState TerminalDefeat(
            bool hasPendingTemporaryFile,
            string detail = "")
        {
            return TerminalOutcome(
                RunOutcomeKind.Defeat,
                hasPendingTemporaryFile,
                detail);
        }

        /// <summary>建立禁止继续但必须保留原始槽位的读取故障状态。</summary>
        internal static RunPersistenceState Unavailable(
            RunPersistenceStatus status,
            string detail,
            bool hasStoredData,
            bool hasPendingTemporaryFile = false,
            string missingConfigurationKind = null,
            int? missingConfigurationId = null)
        {
            if (status == RunPersistenceStatus.Unchecked ||
                 status == RunPersistenceStatus.NotFound ||
                 status == RunPersistenceStatus.ContinueAvailable ||
                 status == RunPersistenceStatus.TerminalOutcome ||
                status == RunPersistenceStatus.DeleteFailed ||
                status == RunPersistenceStatus.CommitPending ||
                status == RunPersistenceStatus.CommitFailed)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new RunPersistenceState(
                status,
                canContinue: false,
                hasStoredData: hasStoredData,
                hasPendingTemporaryFile: hasPendingTemporaryFile,
                detail: detail,
                missingConfigurationKind: missingConfigurationKind,
                missingConfigurationId: missingConfigurationId,
                outcomeKind: null);
        }

        /// <summary>建立删除失败状态，同时保留删除前已验证文档的 Continue 能力。</summary>
        internal static RunPersistenceState DeleteFailed(
            string detail,
            bool canContinue,
            bool hasPendingTemporaryFile)
        {
            return new RunPersistenceState(
                RunPersistenceStatus.DeleteFailed,
                canContinue,
                hasStoredData: true,
                hasPendingTemporaryFile,
                detail,
                missingConfigurationKind: null,
                missingConfigurationId: null,
                outcomeKind: null);
        }

        /// <summary>建立当前稳定态正在提交、不可继续推进的状态。</summary>
        internal static RunPersistenceState CommitPending(bool hasStoredData)
        {
            return new RunPersistenceState(
                RunPersistenceStatus.CommitPending,
                canContinue: false,
                hasStoredData: hasStoredData,
                hasPendingTemporaryFile: false,
                detail: string.Empty,
                missingConfigurationKind: null,
                missingConfigurationId: null,
                outcomeKind: null);
        }

        /// <summary>建立保留内存结算、等待重试或确认回退的提交失败状态。</summary>
        internal static RunPersistenceState CommitFailed(
            string detail,
            bool hasStoredData,
            bool hasPendingTemporaryFile)
        {
            return new RunPersistenceState(
                RunPersistenceStatus.CommitFailed,
                canContinue: false,
                hasStoredData: hasStoredData,
                hasPendingTemporaryFile: hasPendingTemporaryFile,
                detail: detail,
                missingConfigurationKind: null,
                missingConfigurationId: null,
                outcomeKind: null);
        }
    }
}
