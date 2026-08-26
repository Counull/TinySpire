using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using TinySpire.Run;

namespace TinySpire.Infrastructure.Persistence
{
    /// <summary>为原子单槽存档隔离可故障注入的文件系统边界。</summary>
    internal interface IRunSaveFileSystem
    {
        /// <summary>判断指定文件是否存在。</summary>
        bool FileExists(string path);

        /// <summary>确保目标目录存在。</summary>
        void CreateDirectory(string path);

        /// <summary>以严格 UTF-8 读取完整文本。</summary>
        string ReadAllText(string path);

        /// <summary>把完整文本持久刷新到指定临时文件。</summary>
        void WriteAllTextDurably(string path, string content);

        /// <summary>在正式档不存在时以同卷移动完成首次提交。</summary>
        void MoveFile(string sourcePath, string destinationPath);

        /// <summary>在正式档存在时以平台原子替换完成提交。</summary>
        void ReplaceFile(string sourcePath, string destinationPath);

        /// <summary>删除指定文件。</summary>
        void DeleteFile(string path);
    }

    /// <summary>使用 System.IO 实现当前 Editor/Standalone 的真实存储边界。</summary>
    internal sealed class PhysicalAtomicRunSaveFileSystem : IRunSaveFileSystem
    {
        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>查询真实文件是否存在。</summary>
        public bool FileExists(string path)
        {
            try
            {
                File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        /// <summary>创建尚不存在的真实目录。</summary>
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>严格读取真实 UTF-8 文件。</summary>
        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, StrictUtf8);
        }

        /// <summary>独占写入临时文件并请求把内容刷新到持久介质。</summary>
        public void WriteAllTextDurably(string path, string content)
        {
            byte[] payload = StrictUtf8.GetBytes(content);
            using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            stream.Write(payload, 0, payload.Length);
            stream.Flush(flushToDisk: true);
        }

        /// <summary>把首次提交的临时文件移动为正式文件。</summary>
        public void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>用系统原子替换语义覆盖已有正式文件，且不提供非原子降级。</summary>
        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null);
        }

        /// <summary>删除真实文件。</summary>
        public void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    /// <summary>在一个目录内以临时文件校验加原子替换实现 versioned JSON 单槽存档。</summary>
    public sealed class AtomicJsonRunSaveStore : IRunSaveStore
    {
        internal const string LiveFileName = "run-save.json";
        internal const string TemporaryFileName = "run-save.json.tmp";
        internal const string TerminalIntentFileName = "run-save.terminal-intent.json";
        internal const string RewardIntentFileName = "run-save.reward-intent.json";

        private readonly string _directoryPath;
        private readonly string _saveFilePath;
        private readonly string _temporaryFilePath;
        private readonly string _terminalIntentFilePath;
        private readonly string _rewardIntentFilePath;
        private readonly IRunSaveFileSystem _fileSystem;

        /// <summary>在指定目录建立真实文件系统单槽 Adapter。</summary>
        public AtomicJsonRunSaveStore(string directoryPath)
            : this(directoryPath, new PhysicalAtomicRunSaveFileSystem())
        {
        }

        /// <summary>以可控文件系统边界建立 Adapter，供故障路径验证。</summary>
        internal AtomicJsonRunSaveStore(
            string directoryPath,
            IRunSaveFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Run save directory is required.", nameof(directoryPath));

            _directoryPath = Path.GetFullPath(directoryPath);
            _saveFilePath = Path.Combine(_directoryPath, LiveFileName);
            _temporaryFilePath = Path.Combine(_directoryPath, TemporaryFileName);
            _terminalIntentFilePath = Path.Combine(_directoryPath, TerminalIntentFileName);
            _rewardIntentFilePath = Path.Combine(_directoryPath, RewardIntentFileName);
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>读取最近安全的稳定事实；终局与奖励事务意图优先，旧版终局临时档继续兼容。</summary>
        public RunSaveLoadResult Load()
        {
            bool hasTemporaryFile = false;
            bool hasTerminalIntent = false;
            bool hasRewardIntent = false;
            try
            {
                hasTerminalIntent = _fileSystem.FileExists(_terminalIntentFilePath);
                hasRewardIntent = _fileSystem.FileExists(_rewardIntentFilePath);
                hasTemporaryFile = _fileSystem.FileExists(_temporaryFilePath);
                if (hasTerminalIntent)
                {
                    if (TryReadTerminalCheckpoint(
                            _terminalIntentFilePath,
                            "terminal intent",
                            out RunSaveDocument intentDocument,
                            out string intentDetail))
                    {
                        return RunSaveLoadResult.Succeeded(
                            intentDocument,
                            hasPendingTemporaryFile: true,
                            "Recovered a validated Terminal(Defeat) checkpoint from the terminal intent journal.");
                    }

                    return RunSaveLoadResult.Failed(
                        RunSaveLoadStatus.InterruptedCommit,
                        intentDetail.Length > 0
                            ? intentDetail
                            : "The terminal intent journal could not be validated.",
                        hasStoredData: true,
                        hasPendingTemporaryFile: true);
                }

                if (hasRewardIntent)
                {
                    if (TryResolveRewardCheckpoint(
                            out RunSaveDocument rewardDocument,
                            out string rewardDetail))
                    {
                        return RunSaveLoadResult.Succeeded(
                            rewardDocument,
                            hasPendingTemporaryFile: true,
                            rewardDetail);
                    }

                    return RunSaveLoadResult.Failed(
                        RunSaveLoadStatus.InterruptedCommit,
                        rewardDetail.Length > 0
                            ? rewardDetail
                            : "The reward intent journal could not be validated.",
                        hasStoredData: true,
                        hasPendingTemporaryFile: true);
                }

                string temporaryDetail = string.Empty;
                if (hasTemporaryFile &&
                    TryReadTerminalCheckpoint(
                        _temporaryFilePath,
                        "pending temporary Run save",
                        out RunSaveDocument terminalDocument,
                        out temporaryDetail))
                {
                    return RunSaveLoadResult.Succeeded(
                        terminalDocument,
                        hasPendingTemporaryFile: true,
                        "Recovered a validated Terminal(Defeat) checkpoint from an interrupted commit.");
                }

                if (!_fileSystem.FileExists(_saveFilePath))
                {
                    return hasTemporaryFile
                        ? RunSaveLoadResult.Failed(
                            RunSaveLoadStatus.InterruptedCommit,
                            temporaryDetail.Length > 0
                                ? temporaryDetail
                                : "A temporary Run save exists without a successful checkpoint.",
                            hasStoredData: true,
                            hasPendingTemporaryFile: true)
                        : RunSaveLoadResult.NotFound();
                }

                RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(_saveFilePath));
                if (read.Status == RunSaveDocumentReadStatus.Success)
                {
                    return RunSaveLoadResult.Succeeded(
                        read.Document,
                        hasTemporaryFile,
                        hasTemporaryFile
                            ? temporaryDetail.Length > 0
                                ? temporaryDetail
                                : "A failed or interrupted newer commit remains for diagnosis."
                            : string.Empty);
                }

                return RunSaveLoadResult.Failed(
                    ToLoadStatus(read.Status),
                    read.Detail,
                    hasStoredData: true,
                    hasPendingTemporaryFile: hasTemporaryFile);
            }
            catch (DecoderFallbackException exception)
            {
                return RunSaveLoadResult.Failed(
                    RunSaveLoadStatus.InvalidJson,
                    exception.Message,
                    hasStoredData: true,
                    hasPendingTemporaryFile:
                        hasTemporaryFile || hasTerminalIntent || hasRewardIntent);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunSaveLoadResult.Failed(
                    RunSaveLoadStatus.IoFailure,
                    exception.Message,
                    hasStoredData: true,
                    hasPendingTemporaryFile:
                        hasTemporaryFile || hasTerminalIntent || hasRewardIntent);
            }
        }

        /// <summary>只把完整可解析的 Terminal(Defeat) 文档作为更安全事实返回，并为拒绝原因保留诊断。</summary>
        private bool TryReadTerminalCheckpoint(
            string path,
            string artifactName,
            out RunSaveDocument terminalDocument,
            out string detail)
        {
            terminalDocument = null;
            detail = string.Empty;
            try
            {
                RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(path));
                if (read.Status == RunSaveDocumentReadStatus.Success &&
                    read.Document.ProgressPhase == RunSaveProgressPhase.Terminal &&
                    read.Document.TerminalReason == RunSaveTerminalReason.Defeat)
                {
                    terminalDocument = read.Document;
                    return true;
                }

                detail = read.Status == RunSaveDocumentReadStatus.Success
                    ? $"The {artifactName} is not a Terminal(Defeat) document."
                    : $"The {artifactName} is unusable: {read.Detail}";
                return false;
            }
            catch (DecoderFallbackException exception)
            {
                detail = $"The {artifactName} is not valid UTF-8: {exception.Message}";
                return false;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                detail = $"The {artifactName} could not be read: {exception.Message}";
                return false;
            }
        }

        /// <summary>用源 RewardPending intent 与正式档唯一判定冻结奖励或已发布结算后继。</summary>
        private bool TryResolveRewardCheckpoint(
            out RunSaveDocument rewardDocument,
            out string detail)
        {
            rewardDocument = null;
            detail = string.Empty;
            RunSaveDocumentReadResult intentRead;
            try
            {
                intentRead = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(_rewardIntentFilePath));
            }
            catch (DecoderFallbackException exception)
            {
                return TryRecoverLivePendingAfterBrokenRewardIntent(
                    $"The reward intent journal is not valid UTF-8: {exception.Message}",
                    out rewardDocument,
                    out detail);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                detail = $"The reward intent journal could not be read: {exception.Message}";
                return false;
            }

            if (intentRead.Status != RunSaveDocumentReadStatus.Success ||
                intentRead.Document.ProgressPhase != RunSaveProgressPhase.RewardPending)
            {
                string failure = intentRead.Status == RunSaveDocumentReadStatus.Success
                    ? "The reward intent journal is not a RewardPending document."
                    : $"The reward intent journal is unusable: {intentRead.Detail}";
                return TryRecoverLivePendingAfterBrokenRewardIntent(
                    failure,
                    out rewardDocument,
                    out detail);
            }

            RunSaveDocument source = intentRead.Document;
            if (!_fileSystem.FileExists(_saveFilePath))
            {
                detail =
                    "The reward intent journal has no live predecessor and cannot be promoted alone.";
                return false;
            }

            RunSaveDocumentReadResult liveRead = RunSaveDocumentCodec.Read(
                _fileSystem.ReadAllText(_saveFilePath));
            if (liveRead.Status != RunSaveDocumentReadStatus.Success)
            {
                detail = $"The live Run save cannot disambiguate its reward intent: {liveRead.Detail}";
                return false;
            }

            RunSaveDocument live = liveRead.Document;
            if (DocumentsEqual(source, live) || IsRewardPendingSuccessor(live, source))
            {
                rewardDocument = source;
                detail = "Recovered the frozen RewardPending checkpoint from its durable intent.";
                return true;
            }
            if (IsRewardSettlementSuccessor(source, live))
            {
                rewardDocument = live;
                detail = "Recovered the published card reward settlement with a residual reward intent.";
                return true;
            }

            detail = "The reward intent journal conflicts with the live Run save.";
            return false;
        }

        /// <summary>intent 损坏时只信任已正式发布的 RewardPending；战前或结算后 MapReady 都保持 fail-closed。</summary>
        private bool TryRecoverLivePendingAfterBrokenRewardIntent(
            string failureDetail,
            out RunSaveDocument rewardDocument,
            out string detail)
        {
            rewardDocument = null;
            detail = failureDetail;
            if (!_fileSystem.FileExists(_saveFilePath))
                return false;

            try
            {
                RunSaveDocumentReadResult liveRead = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(_saveFilePath));
                if (liveRead.Status != RunSaveDocumentReadStatus.Success ||
                    liveRead.Document.ProgressPhase != RunSaveProgressPhase.RewardPending)
                {
                    return false;
                }

                rewardDocument = liveRead.Document;
                detail =
                    "Recovered the live RewardPending checkpoint despite an unusable reward intent.";
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                detail = $"{failureDetail} The live Run save also could not be read: {exception.Message}";
                return false;
            }
        }

        /// <summary>终局先耐久冻结意图，再完整校验通用临时档并用同卷 move 或原子 replace 发布正式档。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            if (document == null)
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.InvalidDocument,
                    "Run save document is missing.");
            }

            string serialized = RunSaveDocumentCodec.Serialize(document);
            RunSaveDocumentReadResult sourceRead = RunSaveDocumentCodec.Read(serialized);
            if (sourceRead.Status != RunSaveDocumentReadStatus.Success ||
                !DocumentsEqual(document, sourceRead.Document))
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.InvalidDocument,
                    sourceRead.Detail.Length > 0
                        ? sourceRead.Detail
                        : "Run save document failed its pre-write validation.");
            }

            try
            {
                _fileSystem.CreateDirectory(_directoryPath);
                FinalizePublishedRewardIntent();
                if (document.ProgressPhase == RunSaveProgressPhase.Terminal)
                {
                    bool requiresIntentWrite = true;
                    if (_fileSystem.FileExists(_terminalIntentFilePath))
                    {
                        RunSaveDocumentReadResult existingIntentRead = RunSaveDocumentCodec.Read(
                            _fileSystem.ReadAllText(_terminalIntentFilePath));
                        if (existingIntentRead.Status == RunSaveDocumentReadStatus.Success &&
                            existingIntentRead.Document.ProgressPhase == RunSaveProgressPhase.Terminal &&
                            existingIntentRead.Document.TerminalReason == RunSaveTerminalReason.Defeat)
                        {
                            if (!DocumentsEqual(document, existingIntentRead.Document))
                            {
                                return RunSaveCommitResult.Failed(
                                    RunSaveCommitStatus.InvalidDocument,
                                    "A different validated terminal intent already owns this save slot.");
                            }

                            requiresIntentWrite = false;
                        }
                    }

                    if (requiresIntentWrite)
                    {
                        _fileSystem.WriteAllTextDurably(_terminalIntentFilePath, serialized);
                        RunSaveDocumentReadResult intentRead = RunSaveDocumentCodec.Read(
                            _fileSystem.ReadAllText(_terminalIntentFilePath));
                        if (intentRead.Status != RunSaveDocumentReadStatus.Success ||
                            intentRead.Document.ProgressPhase != RunSaveProgressPhase.Terminal ||
                            intentRead.Document.TerminalReason != RunSaveTerminalReason.Defeat ||
                            !DocumentsEqual(document, intentRead.Document))
                        {
                            return RunSaveCommitResult.Failed(
                                RunSaveCommitStatus.InvalidDocument,
                                intentRead.Detail.Length > 0
                                    ? intentRead.Detail
                                    : "The terminal intent journal did not match the requested checkpoint.");
                        }
                    }
                }

                bool isRewardSettlement = false;
                if (document.ProgressPhase != RunSaveProgressPhase.Terminal &&
                    !TryPrepareRewardIntent(
                        document,
                        serialized,
                        out isRewardSettlement,
                        out string rewardIntentDetail))
                {
                    return RunSaveCommitResult.Failed(
                        RunSaveCommitStatus.InvalidDocument,
                        rewardIntentDetail);
                }

                _fileSystem.WriteAllTextDurably(_temporaryFilePath, serialized);

                RunSaveDocumentReadResult temporaryRead = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(_temporaryFilePath));
                if (temporaryRead.Status != RunSaveDocumentReadStatus.Success ||
                    !DocumentsEqual(document, temporaryRead.Document))
                {
                    return RunSaveCommitResult.Failed(
                        RunSaveCommitStatus.InvalidDocument,
                        temporaryRead.Detail.Length > 0
                            ? temporaryRead.Detail
                            : "The temporary Run save did not match the requested checkpoint.");
                }

                if (_fileSystem.FileExists(_saveFilePath))
                    _fileSystem.ReplaceFile(_temporaryFilePath, _saveFilePath);
                else
                    _fileSystem.MoveFile(_temporaryFilePath, _saveFilePath);

                if (isRewardSettlement && _fileSystem.FileExists(_rewardIntentFilePath))
                    _fileSystem.DeleteFile(_rewardIntentFilePath);

                return RunSaveCommitResult.Succeeded();
            }
            catch (DecoderFallbackException exception)
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.InvalidDocument,
                    exception.Message);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.IoFailure,
                    exception.Message);
            }
        }

        /// <summary>若正式档已是旧 Pending 的严格结算后继，则在任何新提交前安全清除残留 intent。</summary>
        private void FinalizePublishedRewardIntent()
        {
            if (!_fileSystem.FileExists(_rewardIntentFilePath) ||
                !_fileSystem.FileExists(_saveFilePath))
            {
                return;
            }

            RunSaveDocumentReadResult intentRead = RunSaveDocumentCodec.Read(
                _fileSystem.ReadAllText(_rewardIntentFilePath));
            if (intentRead.Status != RunSaveDocumentReadStatus.Success ||
                intentRead.Document.ProgressPhase != RunSaveProgressPhase.RewardPending)
            {
                return;
            }

            RunSaveDocumentReadResult liveRead = RunSaveDocumentCodec.Read(
                _fileSystem.ReadAllText(_saveFilePath));
            if (liveRead.Status != RunSaveDocumentReadStatus.Success ||
                !IsRewardSettlementSuccessor(intentRead.Document, liveRead.Document))
            {
                return;
            }

            _fileSystem.DeleteFile(_rewardIntentFilePath);
        }

        /// <summary>为新冻结奖励或其合法结算后继准备并回读完整源 Pending intent。</summary>
        private bool TryPrepareRewardIntent(
            RunSaveDocument document,
            string serialized,
            out bool isRewardSettlement,
            out string detail)
        {
            isRewardSettlement = false;
            detail = string.Empty;
            bool hasIntent = _fileSystem.FileExists(_rewardIntentFilePath);
            RunSaveDocument source = null;
            if (hasIntent)
            {
                RunSaveDocumentReadResult intentRead = RunSaveDocumentCodec.Read(
                    _fileSystem.ReadAllText(_rewardIntentFilePath));
                if (intentRead.Status == RunSaveDocumentReadStatus.Success &&
                    intentRead.Document.ProgressPhase == RunSaveProgressPhase.RewardPending)
                {
                    source = intentRead.Document;
                }
                else if (!TryRewriteBrokenRewardIntentFromCurrentFacts(
                             document,
                             serialized,
                             out source,
                             out detail))
                {
                    return false;
                }
            }

            if (document.ProgressPhase == RunSaveProgressPhase.RewardPending)
            {
                if (source != null && !DocumentsEqual(source, document))
                {
                    detail = "A different frozen reward intent already owns this save slot.";
                    return false;
                }

                if (source == null)
                {
                    return WriteAndValidateRewardIntent(
                        document,
                        serialized,
                        out detail);
                }

                return true;
            }

            if (source == null && TryReadLiveRewardPending(out RunSaveDocument livePending))
            {
                if (!IsRewardSettlementSuccessor(livePending, document))
                {
                    detail = "The requested checkpoint is not the legal successor of the live reward.";
                    return false;
                }

                string sourceJson = RunSaveDocumentCodec.Serialize(livePending);
                if (!WriteAndValidateRewardIntent(livePending, sourceJson, out detail))
                    return false;
                source = livePending;
            }

            if (source == null)
                return true;
            if (!IsRewardSettlementSuccessor(source, document))
            {
                detail = "The requested checkpoint is not the legal successor of the frozen reward intent.";
                return false;
            }

            isRewardSettlement = true;
            return true;
        }

        /// <summary>intent 损坏时只用本次完整 Pending 或正式 Pending 重建同一源事实。</summary>
        private bool TryRewriteBrokenRewardIntentFromCurrentFacts(
            RunSaveDocument document,
            string serialized,
            out RunSaveDocument source,
            out string detail)
        {
            source = null;
            if (document.ProgressPhase == RunSaveProgressPhase.RewardPending)
            {
                if (!WriteAndValidateRewardIntent(document, serialized, out detail))
                    return false;
                source = document;
                return true;
            }

            if (TryReadLiveRewardPending(out RunSaveDocument livePending) &&
                IsRewardSettlementSuccessor(livePending, document))
            {
                string sourceJson = RunSaveDocumentCodec.Serialize(livePending);
                if (!WriteAndValidateRewardIntent(livePending, sourceJson, out detail))
                    return false;
                source = livePending;
                return true;
            }

            detail = "The existing reward intent is unusable and cannot be safely reconstructed.";
            return false;
        }

        /// <summary>耐久写入完整源 Pending，并逐字段回读确认未漂移。</summary>
        private bool WriteAndValidateRewardIntent(
            RunSaveDocument source,
            string serialized,
            out string detail)
        {
            _fileSystem.WriteAllTextDurably(_rewardIntentFilePath, serialized);
            RunSaveDocumentReadResult intentRead = RunSaveDocumentCodec.Read(
                _fileSystem.ReadAllText(_rewardIntentFilePath));
            if (intentRead.Status == RunSaveDocumentReadStatus.Success &&
                intentRead.Document.ProgressPhase == RunSaveProgressPhase.RewardPending &&
                DocumentsEqual(source, intentRead.Document))
            {
                detail = string.Empty;
                return true;
            }

            detail = intentRead.Detail.Length > 0
                ? intentRead.Detail
                : "The reward intent journal did not match the frozen RewardPending checkpoint.";
            return false;
        }

        /// <summary>读取正式档中可作为奖励事务源的完整 RewardPending 文档。</summary>
        private bool TryReadLiveRewardPending(out RunSaveDocument pending)
        {
            pending = null;
            if (!_fileSystem.FileExists(_saveFilePath))
                return false;

            RunSaveDocumentReadResult liveRead = RunSaveDocumentCodec.Read(
                _fileSystem.ReadAllText(_saveFilePath));
            if (liveRead.Status != RunSaveDocumentReadStatus.Success ||
                liveRead.Document.ProgressPhase != RunSaveProgressPhase.RewardPending)
            {
                return false;
            }

            pending = liveRead.Document;
            return true;
        }

        /// <summary>先删正式档；只有正式档已消失后才清终局意图与临时物，避免删除失败后复活 Continue。</summary>
        public RunSaveDeleteResult Delete()
        {
            try
            {
                if (_fileSystem.FileExists(_saveFilePath))
                    _fileSystem.DeleteFile(_saveFilePath);
                if (_fileSystem.FileExists(_terminalIntentFilePath))
                    _fileSystem.DeleteFile(_terminalIntentFilePath);
                if (_fileSystem.FileExists(_rewardIntentFilePath))
                    _fileSystem.DeleteFile(_rewardIntentFilePath);
                if (_fileSystem.FileExists(_temporaryFilePath))
                    _fileSystem.DeleteFile(_temporaryFilePath);
                return RunSaveDeleteResult.Succeeded();
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return RunSaveDeleteResult.Failed(exception.Message);
            }
        }

        /// <summary>把文档读取分类映射到 port 的存储读取分类。</summary>
        private static RunSaveLoadStatus ToLoadStatus(RunSaveDocumentReadStatus status)
        {
            switch (status)
            {
                case RunSaveDocumentReadStatus.InvalidJson:
                    return RunSaveLoadStatus.InvalidJson;
                case RunSaveDocumentReadStatus.InvalidDocument:
                    return RunSaveLoadStatus.InvalidDocument;
                case RunSaveDocumentReadStatus.UnsupportedSchema:
                    return RunSaveLoadStatus.UnsupportedSchema;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        /// <summary>判断源 MapReady 到冻结 RewardPending 是否只包含一次战斗胜利允许的事实变化。</summary>
        private static bool IsRewardPendingSuccessor(
            RunSaveDocument live,
            RunSaveDocument pending)
        {
            return live != null &&
                   pending != null &&
                   live.ProgressPhase == RunSaveProgressPhase.MapReady &&
                   pending.ProgressPhase == RunSaveProgressPhase.RewardPending &&
                   HasSameStableRunIdentity(live, pending) &&
                   pending.CurrentHealth > 0 &&
                   pending.CurrentHealth <= pending.MaxHealth &&
                   live.PathNodeIds.SequenceEqual(pending.PathNodeIds) &&
                   live.CommittedNodeId == null &&
                   live.PendingCardReward == null &&
                   pending.CommittedNodeId != null &&
                   pending.PendingCardReward != null &&
                   live.LegacyDeckTemplateId == pending.LegacyDeckTemplateId &&
                   RunCardsEqual(live.RunCards, pending.RunCards);
        }

        /// <summary>严格判断目标是否为源 Pending 的一次选择或跳过结算后继。</summary>
        private static bool IsRewardSettlementSuccessor(
            RunSaveDocument source,
            RunSaveDocument target)
        {
            if (source == null ||
                target == null ||
                source.ProgressPhase != RunSaveProgressPhase.RewardPending ||
                target.ProgressPhase != RunSaveProgressPhase.MapReady ||
                !HasSameStableRunIdentity(source, target) ||
                source.CurrentHealth != target.CurrentHealth ||
                source.CommittedNodeId == null ||
                source.PendingCardReward == null ||
                target.CommittedNodeId != null ||
                target.PendingCardReward != null ||
                source.LegacyDeckTemplateId != null ||
                target.LegacyDeckTemplateId != null ||
                target.PathNodeIds.Count != source.PathNodeIds.Count + 1)
            {
                return false;
            }

            for (int index = 0; index < source.PathNodeIds.Count; index++)
            {
                if (source.PathNodeIds[index] != target.PathNodeIds[index])
                    return false;
            }
            if (target.PathNodeIds[target.PathNodeIds.Count - 1] != source.CommittedNodeId)
                return false;
            if (RunCardsEqual(source.RunCards, target.RunCards))
                return true;
            if (source.RunCards == null ||
                target.RunCards == null ||
                target.RunCards.Count != source.RunCards.Count + 1)
            {
                return false;
            }

            for (int index = 0; index < source.RunCards.Count; index++)
            {
                RunSaveCardDocument sourceCard = source.RunCards[index];
                RunSaveCardDocument targetCard = target.RunCards[index];
                if (sourceCard.InstanceId != targetCard.InstanceId ||
                    sourceCard.TemplateId != targetCard.TemplateId ||
                    sourceCard.UpgradeLevel != targetCard.UpgradeLevel)
                {
                    return false;
                }
            }

            int maximumInstanceId = 0;
            foreach (RunSaveCardDocument sourceCard in source.RunCards)
                maximumInstanceId = Math.Max(maximumInstanceId, sourceCard.InstanceId);
            int expectedInstanceId;
            try
            {
                expectedInstanceId = checked(maximumInstanceId + 1);
            }
            catch (OverflowException)
            {
                return false;
            }

            RunSaveCardDocument appended = target.RunCards[target.RunCards.Count - 1];
            return appended.InstanceId == expectedInstanceId &&
                   appended.UpgradeLevel == 0 &&
                   source.PendingCardReward.CandidateTemplateIds.Contains(
                       appended.TemplateId);
        }

        /// <summary>比较奖励事务前后绝不允许变化的 Run 身份、Hero 与地图配方事实。</summary>
        private static bool HasSameStableRunIdentity(
            RunSaveDocument left,
            RunSaveDocument right)
        {
            return left.SchemaVersion == right.SchemaVersion &&
                   left.RunId == right.RunId &&
                   left.HeroTemplateId == right.HeroTemplateId &&
                   left.MaxHealth == right.MaxHealth &&
                   left.RandomRootSeed == right.RandomRootSeed &&
                   left.MapProfileId == right.MapProfileId &&
                   left.MapGeneratorVersion == right.MapGeneratorVersion &&
                   left.MapSeed == right.MapSeed &&
                   left.MapFingerprint == right.MapFingerprint;
        }

        /// <summary>逐字段确认回读临时档就是本次完整 checkpoint，而非仅可解析的其他文档。</summary>
        private static bool DocumentsEqual(RunSaveDocument left, RunSaveDocument right)
        {
            return left != null &&
                   right != null &&
                   left.SchemaVersion == right.SchemaVersion &&
                   left.RunId == right.RunId &&
                   left.HeroTemplateId == right.HeroTemplateId &&
                   left.CurrentHealth == right.CurrentHealth &&
                   left.MaxHealth == right.MaxHealth &&
                   left.LegacyDeckTemplateId == right.LegacyDeckTemplateId &&
                   RunCardsEqual(left.RunCards, right.RunCards) &&
                   left.RandomRootSeed == right.RandomRootSeed &&
                   left.MapProfileId == right.MapProfileId &&
                   left.MapGeneratorVersion == right.MapGeneratorVersion &&
                   left.MapSeed == right.MapSeed &&
                   left.MapFingerprint == right.MapFingerprint &&
                   left.PathNodeIds.SequenceEqual(right.PathNodeIds) &&
                   left.ProgressPhase == right.ProgressPhase &&
                   left.CommittedNodeId == right.CommittedNodeId &&
                   left.TerminalReason == right.TerminalReason &&
                   PendingCardRewardsEqual(left.PendingCardReward, right.PendingCardReward);
        }

        /// <summary>逐字段比较冻结奖励身份与三个有序候选模板。</summary>
        private static bool PendingCardRewardsEqual(
            RunSavePendingCardRewardDocument left,
            RunSavePendingCardRewardDocument right)
        {
            if (ReferenceEquals(left, right))
                return true;
            return left != null &&
                   right != null &&
                   left.RewardId == right.RewardId &&
                   left.CandidateTemplateIds.SequenceEqual(right.CandidateTemplateIds);
        }

        /// <summary>逐卡比较 canonical RunDeck 的稳定顺序、实例身份、模板与升级事实。</summary>
        private static bool RunCardsEqual(
            IReadOnlyList<RunSaveCardDocument> left,
            IReadOnlyList<RunSaveCardDocument> right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int index = 0; index < left.Count; index++)
            {
                RunSaveCardDocument leftCard = left[index];
                RunSaveCardDocument rightCard = right[index];
                if (leftCard.InstanceId != rightCard.InstanceId ||
                    leftCard.TemplateId != rightCard.TemplateId ||
                    leftCard.UpgradeLevel != rightCard.UpgradeLevel)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>只把可预期的本地存储异常转换为 typed failure，编程错误继续 fail-fast。</summary>
        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is NotSupportedException;
        }
    }
}
