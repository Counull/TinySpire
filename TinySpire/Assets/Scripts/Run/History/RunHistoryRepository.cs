using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TinySpire.Run.History
{
    /// <summary>读取一个逐局历史文件的封闭结果分类。</summary>
    public enum RunHistoryLoadStatus
    {
        Success,
        NotFound,
        InvalidData,
        UnsupportedSchema,
        IoFailure,
    }

    /// <summary>冻结单个 Run 历史读取结果与诊断信息。</summary>
    public sealed class RunHistoryLoadResult
    {
        /// <summary>本次读取的封闭状态。</summary>
        public RunHistoryLoadStatus Status { get; }

        /// <summary>成功时读取到的不可变摘要。</summary>
        public RunSummary Summary { get; }

        /// <summary>失败时可用于日志的本地诊断。</summary>
        public string Detail { get; }

        /// <summary>冻结一个单局读取结果。</summary>
        private RunHistoryLoadResult(
            RunHistoryLoadStatus status,
            RunSummary summary,
            string detail)
        {
            Status = status;
            Summary = summary;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回携带完整摘要的成功结果。</summary>
        public static RunHistoryLoadResult Succeeded(RunSummary summary)
        {
            return new RunHistoryLoadResult(
                RunHistoryLoadStatus.Success,
                summary ?? throw new ArgumentNullException(nameof(summary)),
                string.Empty);
        }

        /// <summary>返回目标 RunId 尚无历史文件的结果。</summary>
        public static RunHistoryLoadResult NotFound()
        {
            return new RunHistoryLoadResult(
                RunHistoryLoadStatus.NotFound,
                null,
                string.Empty);
        }

        /// <summary>返回不发布半合法摘要的明确读取失败。</summary>
        public static RunHistoryLoadResult Failed(
            RunHistoryLoadStatus status,
            string detail)
        {
            if (status == RunHistoryLoadStatus.Success ||
                status == RunHistoryLoadStatus.NotFound)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new RunHistoryLoadResult(status, null, detail);
        }
    }

    /// <summary>首次记录一局不可变历史的封闭结果分类。</summary>
    public enum RunHistoryRecordStatus
    {
        Recorded,
        AlreadyRecorded,
        Conflict,
        InvalidData,
        IoFailure,
    }

    /// <summary>冻结逐 RunId 记录结果与诊断信息。</summary>
    public sealed class RunHistoryRecordResult
    {
        /// <summary>本次记录的封闭状态。</summary>
        public RunHistoryRecordStatus Status { get; }

        /// <summary>成功、幂等或冲突所针对的 RunId。</summary>
        public RunId RunId { get; }

        /// <summary>失败或冲突时可用于日志的本地诊断。</summary>
        public string Detail { get; }

        /// <summary>冻结一个逐局记录结果。</summary>
        private RunHistoryRecordResult(
            RunHistoryRecordStatus status,
            RunId runId,
            string detail)
        {
            Status = status;
            RunId = runId;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回首次成功创建历史文件的结果。</summary>
        public static RunHistoryRecordResult Recorded(RunId runId)
        {
            return new RunHistoryRecordResult(
                RunHistoryRecordStatus.Recorded,
                runId,
                string.Empty);
        }

        /// <summary>返回同 RunId 同内容已经存在的幂等结果。</summary>
        public static RunHistoryRecordResult AlreadyRecorded(RunId runId)
        {
            return new RunHistoryRecordResult(
                RunHistoryRecordStatus.AlreadyRecorded,
                runId,
                string.Empty);
        }

        /// <summary>返回同 RunId 已存在不同内容且拒绝覆盖的冲突。</summary>
        public static RunHistoryRecordResult Conflict(RunId runId, string detail)
        {
            return new RunHistoryRecordResult(
                RunHistoryRecordStatus.Conflict,
                runId,
                detail);
        }

        /// <summary>返回未创建或覆盖任何历史文件的明确失败。</summary>
        public static RunHistoryRecordResult Failed(
            RunHistoryRecordStatus status,
            RunId runId,
            string detail)
        {
            if (status == RunHistoryRecordStatus.Recorded ||
                status == RunHistoryRecordStatus.AlreadyRecorded ||
                status == RunHistoryRecordStatus.Conflict)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new RunHistoryRecordResult(status, runId, detail);
        }
    }

    /// <summary>读取全部逐局历史文件的封闭结果分类。</summary>
    public enum RunHistoryReadAllStatus
    {
        Success,
        InvalidData,
        UnsupportedSchema,
        IoFailure,
    }

    /// <summary>冻结完整历史目录读取结果，失败时不发布不完整统计输入。</summary>
    public sealed class RunHistoryReadAllResult
    {
        private readonly ReadOnlyCollection<RunSummary> _summaries;

        /// <summary>本次目录读取的封闭状态。</summary>
        public RunHistoryReadAllStatus Status { get; }

        /// <summary>成功时按完成时间与 RunId 排序的全部摘要。</summary>
        public IReadOnlyList<RunSummary> Summaries => _summaries;

        /// <summary>失败时可用于日志的本地诊断。</summary>
        public string Detail { get; }

        /// <summary>冻结一个完整历史目录读取结果。</summary>
        private RunHistoryReadAllResult(
            RunHistoryReadAllStatus status,
            IEnumerable<RunSummary> summaries,
            string detail)
        {
            Status = status;
            _summaries = Array.AsReadOnly((summaries ?? Array.Empty<RunSummary>()).ToArray());
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回包含零到多局完整摘要的成功结果。</summary>
        public static RunHistoryReadAllResult Succeeded(IEnumerable<RunSummary> summaries)
        {
            if (summaries == null)
                throw new ArgumentNullException(nameof(summaries));

            RunSummary[] frozen = summaries.ToArray();
            if (frozen.Any(summary => summary == null))
                throw new ArgumentException("Run history cannot contain null summaries.", nameof(summaries));

            return new RunHistoryReadAllResult(
                RunHistoryReadAllStatus.Success,
                frozen.OrderBy(summary => summary.CompletedAtUtc)
                    .ThenBy(summary => summary.RunId.Value),
                string.Empty);
        }

        /// <summary>返回不发布部分摘要集合的明确目录读取失败。</summary>
        public static RunHistoryReadAllResult Failed(
            RunHistoryReadAllStatus status,
            string detail)
        {
            if (status == RunHistoryReadAllStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new RunHistoryReadAllResult(status, Array.Empty<RunSummary>(), detail);
        }
    }

    /// <summary>逐 RunId 不可变历史的领域端口。</summary>
    public interface IRunHistoryRepository
    {
        /// <summary>按 RunId 读取一局摘要，绝不读取或修改 Run save。</summary>
        RunHistoryLoadResult Load(RunId runId);

        /// <summary>只允许首次创建；已存在时返回幂等或冲突而不覆盖。</summary>
        RunHistoryRecordResult Record(RunSummary summary);

        /// <summary>读取全部逐局摘要，供只读统计投影派生。</summary>
        RunHistoryReadAllResult ReadAll();
    }

    /// <summary>遵守同一不可变契约的内存历史库，供纯领域测试与工具使用。</summary>
    public sealed class InMemoryRunHistoryRepository : IRunHistoryRepository
    {
        private readonly Dictionary<RunId, RunSummary> _summaries =
            new Dictionary<RunId, RunSummary>();

        /// <summary>按 RunId 读取当前内存中的不可变摘要。</summary>
        public RunHistoryLoadResult Load(RunId runId)
        {
            return _summaries.TryGetValue(runId, out RunSummary summary)
                ? RunHistoryLoadResult.Succeeded(summary)
                : RunHistoryLoadResult.NotFound();
        }

        /// <summary>首次加入摘要；同内容幂等，不同内容冲突且拒绝替换。</summary>
        public RunHistoryRecordResult Record(RunSummary summary)
        {
            if (summary == null)
            {
                return RunHistoryRecordResult.Failed(
                    RunHistoryRecordStatus.InvalidData,
                    default,
                    "Run summary is missing.");
            }

            if (_summaries.TryGetValue(summary.RunId, out RunSummary existing))
            {
                return existing.Equals(summary)
                    ? RunHistoryRecordResult.AlreadyRecorded(summary.RunId)
                    : RunHistoryRecordResult.Conflict(
                        summary.RunId,
                        "A different summary already exists for this Run id.");
            }

            _summaries.Add(summary.RunId, summary);
            return RunHistoryRecordResult.Recorded(summary.RunId);
        }

        /// <summary>返回当前全部不可变摘要的防御性有序视图。</summary>
        public RunHistoryReadAllResult ReadAll()
        {
            return RunHistoryReadAllResult.Succeeded(_summaries.Values);
        }
    }
}
