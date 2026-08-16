using System;

namespace TinySpire.Run
{
    /// <summary>单槽 Run 文档加载结果，不向调用方泄漏具体存储 API。</summary>
    public enum RunSaveLoadStatus
    {
        Success,
        NotFound,
        InvalidJson,
        InvalidDocument,
        UnsupportedSchema,
        InterruptedCommit,
        IoFailure,
    }

    /// <summary>冻结 load 的文档、故障分类与本地诊断上下文。</summary>
    public sealed class RunSaveLoadResult
    {
        public RunSaveLoadStatus Status { get; }
        public RunSaveDocument Document { get; }
        public string Detail { get; }
        public bool HasStoredData { get; }
        public bool HasPendingTemporaryFile { get; }

        /// <summary>建立不可变 load 结果。</summary>
        private RunSaveLoadResult(
            RunSaveLoadStatus status,
            RunSaveDocument document,
            string detail,
            bool hasStoredData,
            bool hasPendingTemporaryFile)
        {
            Status = status;
            Document = document;
            Detail = detail ?? string.Empty;
            HasStoredData = hasStoredData;
            HasPendingTemporaryFile = hasPendingTemporaryFile;
        }

        /// <summary>返回携带最近成功文档的 load 成功。</summary>
        public static RunSaveLoadResult Succeeded(
            RunSaveDocument document,
            bool hasPendingTemporaryFile = false,
            string detail = "")
        {
            return new RunSaveLoadResult(
                RunSaveLoadStatus.Success,
                document ?? throw new ArgumentNullException(nameof(document)),
                detail,
                hasStoredData: true,
                hasPendingTemporaryFile);
        }

        /// <summary>返回完全没有单槽记录的结果。</summary>
        public static RunSaveLoadResult NotFound()
        {
            return new RunSaveLoadResult(
                RunSaveLoadStatus.NotFound,
                null,
                string.Empty,
                hasStoredData: false,
                hasPendingTemporaryFile: false);
        }

        /// <summary>返回保留原始存储物但不提供可恢复文档的显式失败。</summary>
        public static RunSaveLoadResult Failed(
            RunSaveLoadStatus status,
            string detail,
            bool hasStoredData,
            bool hasPendingTemporaryFile = false)
        {
            if (status == RunSaveLoadStatus.Success || status == RunSaveLoadStatus.NotFound)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new RunSaveLoadResult(
                status,
                null,
                detail,
                hasStoredData,
                hasPendingTemporaryFile);
        }
    }

    /// <summary>单槽 commit 的明确成功或失败分类。</summary>
    public enum RunSaveCommitStatus
    {
        Success,
        InvalidDocument,
        IoFailure,
    }

    /// <summary>冻结 commit 状态与可供 UI/日志说明的诊断信息。</summary>
    public sealed class RunSaveCommitResult
    {
        public RunSaveCommitStatus Status { get; }
        public string Detail { get; }

        /// <summary>建立不可变 commit 结果。</summary>
        private RunSaveCommitResult(RunSaveCommitStatus status, string detail)
        {
            Status = status;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回成功提交结果。</summary>
        public static RunSaveCommitResult Succeeded()
        {
            return new RunSaveCommitResult(RunSaveCommitStatus.Success, string.Empty);
        }

        /// <summary>返回不覆盖旧档的显式提交失败。</summary>
        public static RunSaveCommitResult Failed(RunSaveCommitStatus status, string detail)
        {
            if (status == RunSaveCommitStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new RunSaveCommitResult(status, detail);
        }
    }

    /// <summary>单槽 delete 的明确成功或 IO 失败分类。</summary>
    public enum RunSaveDeleteStatus
    {
        Success,
        IoFailure,
    }

    /// <summary>冻结 delete 状态与诊断信息。</summary>
    public sealed class RunSaveDeleteResult
    {
        public RunSaveDeleteStatus Status { get; }
        public string Detail { get; }

        /// <summary>建立不可变 delete 结果。</summary>
        private RunSaveDeleteResult(RunSaveDeleteStatus status, string detail)
        {
            Status = status;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回幂等删除成功。</summary>
        public static RunSaveDeleteResult Succeeded()
        {
            return new RunSaveDeleteResult(RunSaveDeleteStatus.Success, string.Empty);
        }

        /// <summary>返回保留原始存储物的删除失败。</summary>
        public static RunSaveDeleteResult Failed(string detail)
        {
            return new RunSaveDeleteResult(RunSaveDeleteStatus.IoFailure, detail);
        }
    }

    /// <summary>游戏自有的完整 Run Document 单槽 load/commit/delete port。</summary>
    public interface IRunSaveStore
    {
        /// <summary>读取最近一份成功提交的地图稳定态或明确故障。</summary>
        RunSaveLoadResult Load();

        /// <summary>提交完整地图稳定态；失败时不得覆盖旧有效档。</summary>
        RunSaveCommitResult Commit(RunSaveDocument document);

        /// <summary>仅在玩家确认后删除当前单槽及其诊断临时物。</summary>
        RunSaveDeleteResult Delete();
    }

    /// <summary>以同一公共 port 保存一个内存文档，供领域与流程测试使用。</summary>
    public sealed class InMemoryRunSaveStore : IRunSaveStore
    {
        private RunSaveDocument _document;

        /// <summary>读取当前内存槽，不存在时返回 NotFound。</summary>
        public RunSaveLoadResult Load()
        {
            return _document == null
                ? RunSaveLoadResult.NotFound()
                : RunSaveLoadResult.Succeeded(_document);
        }

        /// <summary>原子替换内存中的完整不可变文档。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            if (document == null)
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.InvalidDocument,
                    "Run save document is missing.");
            }

            _document = document;
            return RunSaveCommitResult.Succeeded();
        }

        /// <summary>幂等清空当前内存槽。</summary>
        public RunSaveDeleteResult Delete()
        {
            _document = null;
            return RunSaveDeleteResult.Succeeded();
        }
    }
}
