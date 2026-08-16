using System;
using cfg;
using Cysharp.Threading.Tasks;
using TinySpire.Battle;

namespace TinySpire.Run
{
    /// <summary>G1-A 使用的三个稳定 Addressables 场景目标。</summary>
    public static class RunSceneAddresses
    {
        public const string RunEntry = "Assets/Scenes/RunEntryScene.unity";
        public const string Battle = "Assets/Scenes/BattleScene.unity";
    }

    /// <summary>只编排 Run 状态迁移、Battle setup 与场景请求，不复制业务事实。</summary>
    public sealed class RunFlowService : IBattleSetupOptionsSource
    {
        private const int EncounterTemplateId = 5001;

        private readonly RunStateStore _store;
        private readonly Func<Tables> _tablesProvider;
        private readonly ISceneFlowService _scenes;
        private readonly IRunEntropySource _entropy;
        private readonly IRunSaveStore _saveStore;

        private RunSaveDocument _continuableDocument;
        private RunSaveDocument _pendingCommitDocument;

        /// <summary>存档发现、校验与提交状态变化时通知当前入口 Presenter。</summary>
        public event Action PersistenceChanged;

        /// <summary>当前单槽对 UI 可见的不可变状态。</summary>
        public RunPersistenceState Persistence { get; private set; }

        /// <summary>当前是否确有一份可由 Battle child Scope 冻结的 Run attempt。</summary>
        internal bool HasActiveBattleInput
        {
            get
            {
                RunState state = _store.Current;
                return state != null &&
                       state.NodeStatus == RunNodeStatus.InBattle &&
                       state.ActiveBattle != null &&
                       state.BattleSnapshot != null;
            }
        }

        /// <summary>以生产配置服务、场景接口与随机输入源建立跨场景 Run 编排。</summary>
        public RunFlowService(
            RunStateStore store,
            ConfigService configs,
            ISceneFlowService scenes,
            IRunEntropySource entropy,
            IRunSaveStore saveStore)
            : this(store, CreateTablesProvider(configs), scenes, entropy, saveStore)
        {
        }

        /// <summary>以显式配置与存档 port 建立可验证完整检查点语义的 Run 编排。</summary>
        internal RunFlowService(
            RunStateStore store,
            Func<Tables> tablesProvider,
            ISceneFlowService scenes,
            IRunEntropySource entropy,
            IRunSaveStore saveStore)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _tablesProvider = tablesProvider ?? throw new ArgumentNullException(nameof(tablesProvider));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            _entropy = entropy ?? throw new ArgumentNullException(nameof(entropy));
            _saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            Persistence = RunPersistenceState.Unchecked();
        }

        /// <summary>从两名冻结候选 Hero 的配置创建唯一新 Run，不触发额外场景切换。</summary>
        public RunState CreateNewRun(int heroTemplateId)
        {
            if (Persistence.Status == RunPersistenceStatus.Unchecked)
                RefreshSaveAvailability();
            if (Persistence.HasStoredData)
            {
                throw new InvalidOperationException(
                    "The stored Run must be explicitly abandoned before creating a new Run.");
            }

            if (heroTemplateId != 1001 && heroTemplateId != 1002)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));

            Tables tables = RequireTables();
            cfg.battle.Hero hero = tables.TbHero.GetOrDefault(heroTemplateId)
                ?? throw new InvalidOperationException($"Hero template {heroTemplateId} does not exist.");
            if (tables.TbDeck.GetOrDefault(hero.InitialDeckId) == null)
                throw new InvalidOperationException($"Deck template {hero.InitialDeckId} does not exist.");
            if (tables.TbEncounter.GetOrDefault(EncounterTemplateId) == null)
                throw new InvalidOperationException($"Encounter template {EncounterTemplateId} does not exist.");

            RunEntropy entropy = _entropy.Next();
            RunState created = _store.CreateNewRun(new RunCreationOptions(
                entropy.RunId,
                hero.Id,
                hero.MaxHealth,
                hero.MaxHealth,
                hero.InitialDeckId,
                EncounterTemplateId,
                entropy.RandomRootSeed));
            BeginCheckpointCommit(RunSaveDocumentMapper.Create(created));
            CommitPendingCheckpoint();
            return created;
        }

        /// <summary>冻结进战 snapshot 与本战输入后，请求进入既有 BattleScene。</summary>
        public async UniTask<RunBattleInput> EnterBattleNodeAsync()
        {
            if (Persistence.Status == RunPersistenceStatus.CommitPending ||
                Persistence.Status == RunPersistenceStatus.CommitFailed)
            {
                throw new InvalidOperationException(
                    "The current stable checkpoint must be saved before entering battle.");
            }

            RunBattleInput input = _store.BeginBattle();
            await _scenes.LoadSceneWithLoadingAsync(RunSceneAddresses.Battle);
            return input;
        }

        /// <summary>从失败 snapshot 恢复并签发新 attempt 后，再次请求进入 BattleScene。</summary>
        public async UniTask<RunBattleInput> RestartFailedBattleAsync()
        {
            RunBattleInput input = _store.RestartBattle();
            await _scenes.LoadSceneWithLoadingAsync(RunSceneAddresses.Battle);
            return input;
        }

        /// <summary>把当前有效本战输入映射为 Battle child Scope 唯一读取的装配参数。</summary>
        public BattleSetupOptions CreateBattleSetupOptions()
        {
            RunBattleInput input = RequireActiveBattle();
            return new BattleSetupOptions(
                input.HeroTemplateId,
                input.EncounterTemplateId,
                checked((int)input.RandomSeed),
                input.InitialHealth,
                input.DeckTemplateId);
        }

        /// <summary>确认 BattleScope 冻结参数仍精确对应当前 attempt，并返回关联身份。</summary>
        internal RunBattleId BindBattleAttempt(BattleSetupOptions setup)
        {
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            RunBattleInput input = RequireActiveBattle();
            if (setup.HeroTemplateId != input.HeroTemplateId ||
                setup.EncounterTemplateId != input.EncounterTemplateId ||
                setup.RandomSeed != input.RandomSeed ||
                setup.PlayerInitialHealth != input.InitialHealth ||
                setup.DeckTemplateId != input.DeckTemplateId)
            {
                throw new InvalidOperationException("Battle setup does not match the active Run attempt.");
            }

            return input.BattleId;
        }

        /// <summary>消费当前 attempt 的单玩家稳定结果，先写回 Run 再返回入口场景。</summary>
        internal async UniTask HandleBattleResultAsync(
            RunBattleId battleId,
            BattleResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.Players.Count != 1)
                throw new InvalidOperationException("G1-A requires exactly one player BattleResult snapshot.");

            BattleResultPlayerSnapshot player = result.Players[0];
            switch (result.Kind)
            {
                case BattleResultKind.Victory:
                    RunState completed = _store.ApplyVictory(
                        battleId,
                        player.TemplateId,
                        player.Health,
                        player.MaxHealth);
                    BeginCheckpointCommit(RunSaveDocumentMapper.Create(completed));
                    await _scenes.LoadSceneWithLoadingAsync(RunSceneAddresses.RunEntry);
                    CommitPendingCheckpoint();
                    return;
                case BattleResultKind.Defeat:
                    _store.RecordDefeat(
                        battleId,
                        player.TemplateId,
                        player.Health,
                        player.MaxHealth);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result));
            }

            await _scenes.LoadSceneWithLoadingAsync(RunSceneAddresses.RunEntry);
        }

        /// <summary>在没有 active Run 时发现并配置校验最近成功的地图检查点，但不自动 hydrate。</summary>
        public RunPersistenceState RefreshSaveAvailability()
        {
            if (_store.Current != null ||
                Persistence.Status == RunPersistenceStatus.CommitPending ||
                Persistence.Status == RunPersistenceStatus.CommitFailed)
            {
                return Persistence;
            }

            RunSaveLoadResult load = _saveStore.Load();
            _continuableDocument = null;
            switch (load.Status)
            {
                case RunSaveLoadStatus.NotFound:
                    SetPersistence(RunPersistenceState.NotFound());
                    break;
                case RunSaveLoadStatus.Success:
                    ApplyLoadedDocument(load);
                    break;
                case RunSaveLoadStatus.InvalidJson:
                    SetPersistence(RunPersistenceState.Unavailable(
                        RunPersistenceStatus.InvalidJson,
                        load.Detail,
                        load.HasStoredData,
                        load.HasPendingTemporaryFile));
                    break;
                case RunSaveLoadStatus.InvalidDocument:
                    SetPersistence(RunPersistenceState.Unavailable(
                        RunPersistenceStatus.InvalidDocument,
                        load.Detail,
                        load.HasStoredData,
                        load.HasPendingTemporaryFile));
                    break;
                case RunSaveLoadStatus.UnsupportedSchema:
                    SetPersistence(RunPersistenceState.Unavailable(
                        RunPersistenceStatus.UnsupportedSchema,
                        load.Detail,
                        load.HasStoredData,
                        load.HasPendingTemporaryFile));
                    break;
                case RunSaveLoadStatus.InterruptedCommit:
                    SetPersistence(RunPersistenceState.Unavailable(
                        RunPersistenceStatus.InterruptedCommit,
                        load.Detail,
                        load.HasStoredData,
                        load.HasPendingTemporaryFile));
                    break;
                case RunSaveLoadStatus.IoFailure:
                    SetPersistence(RunPersistenceState.Unavailable(
                        RunPersistenceStatus.IoFailure,
                        load.Detail,
                        load.HasStoredData,
                        load.HasPendingTemporaryFile));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(load.Status), load.Status, null);
            }

            return Persistence;
        }

        /// <summary>只在玩家明确选择 Continue 后把已验证文档恢复到唯一 RunStateStore。</summary>
        public RunState ContinueSavedRun()
        {
            if (!Persistence.CanContinue || _continuableDocument == null)
                throw new InvalidOperationException("No validated Run checkpoint is available to continue.");
            if (_store.Current != null)
                throw new InvalidOperationException("An active Run already exists.");

            RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
                _continuableDocument,
                new TablesRunSaveConfigurationCatalog(RequireTables()));
            if (restore.Status != RunSaveRestoreStatus.Success)
            {
                SetRestoreFailure(restore, _continuableDocument);
                throw new InvalidOperationException(restore.Detail);
            }

            return _store.RestoreRun(restore.Options);
        }

        /// <summary>仅在 UI 已取得玩家确认后删除单槽；失败时保留原数据与当前状态。</summary>
        public RunSaveDeleteResult AbandonSavedRun()
        {
            if (_store.Current != null)
                throw new InvalidOperationException("An active Run cannot be deleted from the cold-start menu.");

            bool canContinueAfterFailure = Persistence.CanContinue &&
                                           _continuableDocument != null;
            bool hadPendingTemporaryFile = Persistence.HasPendingTemporaryFile;
            RunSaveDeleteResult result = _saveStore.Delete();
            if (result.Status == RunSaveDeleteStatus.Success)
            {
                _continuableDocument = null;
                SetPersistence(RunPersistenceState.NotFound());
            }
            else
            {
                SetPersistence(RunPersistenceState.DeleteFailed(
                    result.Detail,
                    canContinueAfterFailure,
                    hadPendingTemporaryFile));
            }

            return result;
        }

        /// <summary>重试已缓存的同一 S0/S1 文档，不重放结果、不重取 entropy。</summary>
        public RunSaveCommitResult RetryPendingCommit()
        {
            if (Persistence.Status != RunPersistenceStatus.CommitFailed ||
                _pendingCommitDocument == null)
            {
                throw new InvalidOperationException("No failed Run checkpoint is available to retry.");
            }

            SetPersistence(RunPersistenceState.CommitPending(Persistence.HasStoredData));
            return CommitPendingCheckpoint();
        }

        /// <summary>仅在玩家确认回退警告后丢弃内存未保存进度，并重新发现上一成功档。</summary>
        public void ExitPendingRunToMenu()
        {
            if (Persistence.Status != RunPersistenceStatus.CommitFailed)
                throw new InvalidOperationException("The current Run does not have a failed checkpoint commit.");

            _pendingCommitDocument = null;
            _continuableDocument = null;
            _store.ClearStableRun();
            SetPersistence(RunPersistenceState.Unchecked());
            RefreshSaveAvailability();
        }

        /// <summary>配置校验成功后缓存可继续文档；失败时只发布类型化原因。</summary>
        private void ApplyLoadedDocument(RunSaveLoadResult load)
        {
            RunSaveRestoreResult restore = RunSaveDocumentMapper.CreateRestore(
                load.Document,
                new TablesRunSaveConfigurationCatalog(RequireTables()));
            if (restore.Status != RunSaveRestoreStatus.Success)
            {
                SetRestoreFailure(
                    restore,
                    load.Document,
                    load.HasPendingTemporaryFile);
                return;
            }

            _continuableDocument = load.Document;
            SetPersistence(RunPersistenceState.Available(
                load.HasPendingTemporaryFile,
                load.Detail));
        }

        /// <summary>把配置引用失败映射为 UI 可区分且禁止 Continue 的状态。</summary>
        private void SetRestoreFailure(
            RunSaveRestoreResult restore,
            RunSaveDocument document,
            bool hasPendingTemporaryFile = false)
        {
            RunPersistenceStatus status;
            string missingKind = null;
            int? missingId = null;
            switch (restore.Status)
            {
                case RunSaveRestoreStatus.InvalidDocument:
                    status = RunPersistenceStatus.InvalidDocument;
                    break;
                case RunSaveRestoreStatus.MissingHeroTemplate:
                    status = RunPersistenceStatus.MissingHeroTemplate;
                    missingKind = "Hero";
                    missingId = document?.HeroTemplateId;
                    break;
                case RunSaveRestoreStatus.MissingDeckTemplate:
                    status = RunPersistenceStatus.MissingDeckTemplate;
                    missingKind = "Deck";
                    missingId = document?.DeckTemplateId;
                    break;
                case RunSaveRestoreStatus.MissingEncounterTemplate:
                    status = RunPersistenceStatus.MissingEncounterTemplate;
                    missingKind = "Encounter";
                    missingId = document?.EncounterTemplateId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(restore.Status), restore.Status, null);
            }

            _continuableDocument = null;
            SetPersistence(RunPersistenceState.Unavailable(
                status,
                restore.Detail,
                hasStoredData: true,
                hasPendingTemporaryFile: hasPendingTemporaryFile,
                missingConfigurationKind: missingKind,
                missingConfigurationId: missingId));
        }

        /// <summary>缓存本次完整稳定文档并先发布阻断推进的 Pending 状态。</summary>
        private void BeginCheckpointCommit(RunSaveDocument document)
        {
            _pendingCommitDocument = document
                ?? throw new ArgumentNullException(nameof(document));
            SetPersistence(RunPersistenceState.CommitPending(Persistence.HasStoredData));
        }

        /// <summary>提交缓存的同一稳定文档，并保留失败文档供明确重试。</summary>
        private RunSaveCommitResult CommitPendingCheckpoint()
        {
            if (_pendingCommitDocument == null)
                throw new InvalidOperationException("No pending Run checkpoint exists.");

            RunSaveDocument document = _pendingCommitDocument;
            RunSaveCommitResult result = _saveStore.Commit(document);
            if (result.Status == RunSaveCommitStatus.Success)
            {
                _pendingCommitDocument = null;
                _continuableDocument = document;
                SetPersistence(RunPersistenceState.Available(
                    hasPendingTemporaryFile: false));
                return result;
            }

            RunSaveLoadResult fallback = _saveStore.Load();
            SetPersistence(RunPersistenceState.CommitFailed(
                result.Detail,
                fallback.HasStoredData,
                fallback.HasPendingTemporaryFile));
            return result;
        }

        /// <summary>替换存档状态并同步通知当前场景 Presenter。</summary>
        private void SetPersistence(RunPersistenceState state)
        {
            Persistence = state ?? throw new ArgumentNullException(nameof(state));
            PersistenceChanged?.Invoke();
        }

        /// <summary>从已初始化配置服务创建延迟读取表格的生产提供器。</summary>
        private static Func<Tables> CreateTablesProvider(ConfigService configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            return () => configs.Tables;
        }

        /// <summary>读取已初始化的 Luban 表，并拒绝在启动配置完成前创建 Run。</summary>
        private Tables RequireTables()
        {
            return _tablesProvider()
                ?? throw new InvalidOperationException("ConfigService must be initialized before creating a run.");
        }

        /// <summary>读取唯一处于 InBattle 的本战输入。</summary>
        private RunBattleInput RequireActiveBattle()
        {
            RunState state = _store.Current;
            if (state == null ||
                state.NodeStatus != RunNodeStatus.InBattle ||
                state.ActiveBattle == null)
            {
                throw new InvalidOperationException("No active Run battle input exists.");
            }

            return state.ActiveBattle;
        }
    }

    /// <summary>以当前 Luban 表实现读档所需的稳定配置 ID 存在性目录。</summary>
    internal sealed class TablesRunSaveConfigurationCatalog : IRunSaveConfigurationCatalog
    {
        private readonly Tables _tables;

        /// <summary>冻结一次已初始化的配置表引用。</summary>
        public TablesRunSaveConfigurationCatalog(Tables tables)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        }

        /// <summary>判断 Hero 模板是否仍存在。</summary>
        public bool HeroExists(int templateId)
        {
            return _tables.TbHero.GetOrDefault(templateId) != null;
        }

        /// <summary>读取已确认存在的 Hero 当前生命上限。</summary>
        public int GetHeroMaxHealth(int templateId)
        {
            cfg.battle.Hero hero = _tables.TbHero.GetOrDefault(templateId)
                ?? throw new InvalidOperationException($"Hero template {templateId} does not exist.");
            return hero.MaxHealth;
        }

        /// <summary>判断 Deck 模板是否仍存在。</summary>
        public bool DeckExists(int templateId)
        {
            return _tables.TbDeck.GetOrDefault(templateId) != null;
        }

        /// <summary>判断 Encounter 模板是否仍存在。</summary>
        public bool EncounterExists(int templateId)
        {
            return _tables.TbEncounter.GetOrDefault(templateId) != null;
        }
    }
}
