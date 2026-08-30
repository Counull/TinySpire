using System;
using System.Collections.Generic;
using R3;

namespace TinySpire.Run.History
{
    /// <summary>为终局首次观察提供可替换的 UTC 时钟边界。</summary>
    public interface IRunHistoryClock
    {
        /// <summary>返回当前 UTC 时间。</summary>
        DateTimeOffset UtcNow { get; }
    }

    /// <summary>使用系统 UTC 的运行时时钟。</summary>
    public sealed class SystemRunHistoryClock : IRunHistoryClock
    {
        /// <summary>读取当前系统 UTC 时间。</summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>终局历史服务一次处理的封闭结果分类。</summary>
    public enum RunHistoryServiceRecordStatus
    {
        IgnoredNonTerminal,
        Recorded,
        AlreadyRecorded,
        Conflict,
        Unavailable,
    }

    /// <summary>冻结一次终局处理结果、摘要与诊断。</summary>
    public sealed class RunHistoryServiceRecordResult
    {
        /// <summary>本次处理的封闭状态。</summary>
        public RunHistoryServiceRecordStatus Status { get; }

        /// <summary>终局处理时冻结或读取到的摘要。</summary>
        public RunSummary Summary { get; }

        /// <summary>失败或冲突时可用于日志的本地诊断。</summary>
        public string Detail { get; }

        /// <summary>冻结一个终局服务处理结果。</summary>
        private RunHistoryServiceRecordResult(
            RunHistoryServiceRecordStatus status,
            RunSummary summary,
            string detail)
        {
            Status = status;
            Summary = summary;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回非终局零写入结果。</summary>
        public static RunHistoryServiceRecordResult Ignored()
        {
            return new RunHistoryServiceRecordResult(
                RunHistoryServiceRecordStatus.IgnoredNonTerminal,
                null,
                string.Empty);
        }

        /// <summary>返回携带终局摘要的成功或幂等结果。</summary>
        public static RunHistoryServiceRecordResult Succeeded(
            RunHistoryServiceRecordStatus status,
            RunSummary summary)
        {
            if (status != RunHistoryServiceRecordStatus.Recorded &&
                status != RunHistoryServiceRecordStatus.AlreadyRecorded)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new RunHistoryServiceRecordResult(
                status,
                summary ?? throw new ArgumentNullException(nameof(summary)),
                string.Empty);
        }

        /// <summary>返回保留诊断与可选冻结摘要的冲突或不可用结果。</summary>
        public static RunHistoryServiceRecordResult Failed(
            RunHistoryServiceRecordStatus status,
            RunSummary summary,
            string detail)
        {
            if (status != RunHistoryServiceRecordStatus.Conflict &&
                status != RunHistoryServiceRecordStatus.Unavailable)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new RunHistoryServiceRecordResult(status, summary, detail);
        }
    }

    /// <summary>读取完整历史并派生统计的封闭结果分类。</summary>
    public enum RunHistoryStatisticsLoadStatus
    {
        Success,
        Unavailable,
    }

    /// <summary>冻结统计投影或明确的历史读取故障。</summary>
    public sealed class RunHistoryStatisticsLoadResult
    {
        /// <summary>本次统计读取状态。</summary>
        public RunHistoryStatisticsLoadStatus Status { get; }

        /// <summary>成功时从逐局历史纯派生的统计。</summary>
        public RunHistoryStatistics Statistics { get; }

        /// <summary>失败时可用于日志的本地诊断。</summary>
        public string Detail { get; }

        /// <summary>冻结一个统计读取结果。</summary>
        private RunHistoryStatisticsLoadResult(
            RunHistoryStatisticsLoadStatus status,
            RunHistoryStatistics statistics,
            string detail)
        {
            Status = status;
            Statistics = statistics;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回携带只读统计投影的成功结果。</summary>
        public static RunHistoryStatisticsLoadResult Succeeded(
            RunHistoryStatistics statistics)
        {
            return new RunHistoryStatisticsLoadResult(
                RunHistoryStatisticsLoadStatus.Success,
                statistics ?? throw new ArgumentNullException(nameof(statistics)),
                string.Empty);
        }

        /// <summary>返回不发布不完整统计的历史不可用结果。</summary>
        public static RunHistoryStatisticsLoadResult Failed(string detail)
        {
            return new RunHistoryStatisticsLoadResult(
                RunHistoryStatisticsLoadStatus.Unavailable,
                null,
                detail);
        }
    }

    /// <summary>订阅唯一 RunState 并把 Terminal 快照逐 RunId 恰好记录一次。</summary>
    public sealed class RunHistoryService : IDisposable
    {
        private readonly IRunHistoryRepository _repository;
        private readonly IRunHistoryClock _clock;
        private readonly Dictionary<RunId, RunSummary> _pendingSummaries =
            new Dictionary<RunId, RunSummary>();
        private RunStateStore _subscribedStore;
        private IDisposable _stateSubscription;
        private bool _disposed;

        /// <summary>最近一次非空 RunState 处理结果。</summary>
        public RunHistoryServiceRecordResult LastRecordResult { get; private set; }

        /// <summary>最近一次统计快照通知中的观察者异常；通知成功或尚未通知时为空。</summary>
        public Exception LastStatisticsNotificationException { get; private set; }

        /// <summary>首次成功新增耐久历史后发布完整统计读取结果。</summary>
        public event Action<RunHistoryStatisticsLoadResult> StatisticsChanged;

        /// <summary>使用系统 UTC 建立运行时历史服务。</summary>
        public RunHistoryService(IRunHistoryRepository repository)
            : this(repository, new SystemRunHistoryClock())
        {
        }

        /// <summary>以可控时钟建立历史服务，供失败重试与冷恢复验证。</summary>
        public RunHistoryService(
            IRunHistoryRepository repository,
            IRunHistoryClock clock)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>幂等订阅一个 RunStateStore；当前值会同时覆盖冷恢复终局。</summary>
        public void Initialize(RunStateStore store)
        {
            ThrowIfDisposed();
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (_subscribedStore != null)
            {
                if (ReferenceEquals(_subscribedStore, store))
                    return;

                throw new InvalidOperationException("Run history service already observes another RunStateStore.");
            }

            _subscribedStore = store;
            _stateSubscription = store.State.Subscribe(state =>
            {
                if (state != null)
                    EnsureRecorded(state);
            });
        }

        /// <summary>非终局零写入；终局首次冻结，失败时始终重试同一摘要对象。</summary>
        public RunHistoryServiceRecordResult EnsureRecorded(RunState state)
        {
            ThrowIfDisposed();
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.ProgressPhase != RunProgressPhase.Terminal || state.Outcome == null)
                return PublishResult(RunHistoryServiceRecordResult.Ignored());

            RunSummary pending = null;
            if (_pendingSummaries.TryGetValue(state.RunId, out RunSummary frozenPending))
            {
                RunSummary observedPending = RunSummaryFactory.Create(
                    state,
                    frozenPending.CompletedAtUtc);
                if (!frozenPending.Equals(observedPending))
                {
                    return PublishResult(RunHistoryServiceRecordResult.Failed(
                        RunHistoryServiceRecordStatus.Conflict,
                        observedPending,
                        "Terminal RunState changed while its history commit was pending."));
                }

                pending = frozenPending;
            }

            RunHistoryLoadResult existingLoad = _repository.Load(state.RunId);
            switch (existingLoad.Status)
            {
                case RunHistoryLoadStatus.Success:
                    _pendingSummaries.Remove(state.RunId);
                    RunSummary expected = pending ?? RunSummaryFactory.Create(
                        state,
                        existingLoad.Summary.CompletedAtUtc);
                    return existingLoad.Summary.Equals(expected)
                        ? PublishResult(RunHistoryServiceRecordResult.Succeeded(
                            RunHistoryServiceRecordStatus.AlreadyRecorded,
                            existingLoad.Summary))
                        : PublishResult(RunHistoryServiceRecordResult.Failed(
                            RunHistoryServiceRecordStatus.Conflict,
                            expected,
                            "A different Terminal snapshot already exists for this Run id."));
                case RunHistoryLoadStatus.NotFound:
                    break;
                case RunHistoryLoadStatus.InvalidData:
                case RunHistoryLoadStatus.UnsupportedSchema:
                case RunHistoryLoadStatus.IoFailure:
                    if (pending == null)
                    {
                        pending = RunSummaryFactory.Create(state, _clock.UtcNow);
                        _pendingSummaries.Add(state.RunId, pending);
                    }

                    return PublishResult(RunHistoryServiceRecordResult.Failed(
                        RunHistoryServiceRecordStatus.Unavailable,
                        pending,
                        existingLoad.Detail));
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (pending == null)
            {
                pending = RunSummaryFactory.Create(state, _clock.UtcNow);
                _pendingSummaries.Add(state.RunId, pending);
            }

            RunHistoryRecordResult record = _repository.Record(pending);
            switch (record.Status)
            {
                case RunHistoryRecordStatus.Recorded:
                    _pendingSummaries.Remove(state.RunId);
                    return PublishResult(RunHistoryServiceRecordResult.Succeeded(
                        RunHistoryServiceRecordStatus.Recorded,
                        pending));
                case RunHistoryRecordStatus.AlreadyRecorded:
                    _pendingSummaries.Remove(state.RunId);
                    return PublishResult(RunHistoryServiceRecordResult.Succeeded(
                        RunHistoryServiceRecordStatus.AlreadyRecorded,
                        pending));
                case RunHistoryRecordStatus.Conflict:
                    _pendingSummaries.Remove(state.RunId);
                    return PublishResult(RunHistoryServiceRecordResult.Failed(
                        RunHistoryServiceRecordStatus.Conflict,
                        pending,
                        record.Detail));
                case RunHistoryRecordStatus.InvalidData:
                case RunHistoryRecordStatus.IoFailure:
                    return PublishResult(RunHistoryServiceRecordResult.Failed(
                        RunHistoryServiceRecordStatus.Unavailable,
                        pending,
                        record.Detail));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>读取全部逐局历史并即时派生统计，不保存第二份计数。</summary>
        public RunHistoryStatisticsLoadResult LoadStatistics()
        {
            ThrowIfDisposed();
            RunHistoryReadAllResult read = _repository.ReadAll();
            if (read.Status != RunHistoryReadAllStatus.Success)
                return RunHistoryStatisticsLoadResult.Failed(read.Detail);

            try
            {
                return RunHistoryStatisticsLoadResult.Succeeded(
                    RunHistoryStatisticsProjector.Project(read.Summaries));
            }
            catch (InvalidOperationException exception)
            {
                return RunHistoryStatisticsLoadResult.Failed(exception.Message);
            }
        }

        /// <summary>解除 RunState 订阅并停止后续历史写入。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            _subscribedStore = null;
        }

        /// <summary>保存最近结果，并且只在首次成功新增历史时发布完整统计快照。</summary>
        private RunHistoryServiceRecordResult PublishResult(
            RunHistoryServiceRecordResult result)
        {
            LastRecordResult = result ?? throw new ArgumentNullException(nameof(result));
            if (result.Status == RunHistoryServiceRecordStatus.Recorded)
                NotifyStatisticsChanged();

            return result;
        }

        /// <summary>向每个统计观察者独立发布完整快照，观察者故障不得遮蔽已经耐久的终局成功。</summary>
        private void NotifyStatisticsChanged()
        {
            Action<RunHistoryStatisticsLoadResult> handlers = StatisticsChanged;
            if (handlers == null)
            {
                LastStatisticsNotificationException = null;
                return;
            }

            RunHistoryStatisticsLoadResult statistics = LoadStatistics();
            var failures = new List<Exception>();
            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<RunHistoryStatisticsLoadResult>)subscriber)(statistics);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (failures.Count == 0)
                LastStatisticsNotificationException = null;
            else if (failures.Count == 1)
                LastStatisticsNotificationException = failures[0];
            else
                LastStatisticsNotificationException = new AggregateException(failures);
        }

        /// <summary>服务释放后拒绝重新订阅、写入或读取统计。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RunHistoryService));
        }
    }
}
