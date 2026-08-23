using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cfg;
using Cysharp.Threading.Tasks;
using R3;
using TinySpire.Run;
using TinySpire.Run.Map;
using UnityEngine.Localization;
using VContainer;
using VContainer.Unity;

namespace TinySpire.UI.Run
{
    /// <summary>RunEntryScene 内可见的互斥页面。</summary>
    public enum RunEntryPage
    {
        MainMenu,
        HeroSelection,
        Settings,
        Compendium,
        Statistics,
        Map,
        Failure,
        AbandonConfirmation,
        SaveFailure,
        RollbackConfirmation,
    }

    /// <summary>入口 View 可提交给 Presenter 的有限动作集合。</summary>
    public enum RunEntryActionKind
    {
        StartGame,
        OpenSettings,
        OpenCompendium,
        OpenStatistics,
        Back,
        SelectHero,
        ConfirmHero,
        EnterMapNode,
        LeaveTerminalRun,
        ContinueGame,
        ConfirmAbandon,
        RetrySave,
        RequestExitAfterSaveFailure,
        ConfirmRollback,
    }

    /// <summary>入口投影中每个 TMP 文本的稳定槽位。</summary>
    public enum RunEntryTextSlot
    {
        MainTitle,
        StartGame,
        Settings,
        Compendium,
        Statistics,
        Back,
        ComingSoon,
        SettingsTitle,
        SettingsPlaceholder,
        HeroTitle,
        Hero1001Name,
        Hero1002Name,
        ConfirmHero,
        FutureSlot,
        MapTitle,
        BattleNode,
        Cleared,
        Health,
        FailureTitle,
        LeaveRun,
        ContinueGame,
        Cancel,
        ConfirmationTitle,
        ConfirmationMessage,
        ConfirmationConfirm,
        SaveIssueTitle,
        SaveIssue,
        SaveFailureMessage,
        RetrySave,
        Exit,
        RollbackTitle,
        RollbackMessage,
        RollbackConfirm,
    }

    /// <summary>View 发出的单个不可变入口动作；选择类动作只携带对应领域身份。</summary>
    public readonly struct RunEntryAction
    {
        /// <summary>动作类型。</summary>
        public RunEntryActionKind Kind { get; }

        /// <summary>选择动作携带的 Hero 模板标识，其余动作为空。</summary>
        public int? HeroTemplateId { get; }

        /// <summary>地图节点动作携带的稳定节点身份，其余动作为空。</summary>
        public MapNodeId? MapNodeId { get; }

        /// <summary>创建并验证一个入口 UI 意图。</summary>
        public RunEntryAction(
            RunEntryActionKind kind,
            int? heroTemplateId = null,
            MapNodeId? mapNodeId = null)
        {
            if (kind == RunEntryActionKind.SelectHero)
            {
                if (!heroTemplateId.HasValue || heroTemplateId.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            }
            else if (heroTemplateId.HasValue)
            {
                throw new ArgumentException(
                    "Only SelectHero actions may carry a hero template id.",
                    nameof(heroTemplateId));
            }


            if (kind == RunEntryActionKind.EnterMapNode)
            {
                if (mapNodeId == null || string.IsNullOrEmpty(mapNodeId.Value.Value))
                    throw new ArgumentException("EnterMapNode requires a stable node id.", nameof(mapNodeId));
            }
            else if (mapNodeId.HasValue)
            {
                throw new ArgumentException(
                    "Only EnterMapNode actions may carry a map node id.",
                    nameof(mapNodeId));
            }

            Kind = kind;
            HeroTemplateId = heroTemplateId;
            MapNodeId = mapNodeId;
        }
    }

    /// <summary>地图节点在当前 Run 投影中的互斥功能状态。</summary>
    public enum RunMapNodePresentationState
    {
        Locked,
        Selectable,
        Completed,
        Current,
        BossGateReached,
    }

    /// <summary>地图节点使用的轻量程序化视觉锚点；Boss 候选以不同轮廓保持开局可区分。</summary>
    public enum RunMapVisualAnchorKind
    {
        StartFlag,
        EncounterSlimeSilhouette,
        EncounterSentrySilhouette,
        BossAlphaCrown,
        BossBetaHorns,
        BossGammaEye,
    }

    /// <summary>由静态内容身份解析出的只读显示描述，不进入 MapDefinition 或 Run 存档。</summary>
    public sealed class RunMapIdentityDescriptor
    {
        public string DisplayName { get; }
        public RunMapVisualAnchorKind VisualAnchorKind { get; }

        /// <summary>冻结玩家可见名称与程序化视觉锚点种类。</summary>
        public RunMapIdentityDescriptor(
            string displayName,
            RunMapVisualAnchorKind visualAnchorKind)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Map identity display name cannot be empty.", nameof(displayName));
            if (!Enum.IsDefined(typeof(RunMapVisualAnchorKind), visualAnchorKind))
                throw new ArgumentOutOfRangeException(nameof(visualAnchorKind));

            DisplayName = displayName;
            VisualAnchorKind = visualAnchorKind;
        }
    }

    /// <summary>把冻结 EncounterId/BossId 解析为当前语言只读展示身份的单一 seam。</summary>
    public interface IRunMapIdentityCatalog
    {
        /// <summary>读取指定节点内容身份的名称与视觉锚点，不写入任何 Run 事实。</summary>
        RunMapIdentityDescriptor Resolve(MapNodeKind kind, int contentId);
    }

    /// <summary>从 Luban Encounter 首敌解析名称，并提供 G3 明确 Boss 测试身份的目录适配器。</summary>
    public sealed class RunMapIdentityCatalog : IRunMapIdentityCatalog
    {
        private readonly Func<Tables> _tablesProvider;
        private readonly Func<string, IReadOnlyDictionary<string, object>, string> _localize;

        /// <summary>以生产配置与本地化服务创建只读地图身份目录。</summary>
        [Inject]
        public RunMapIdentityCatalog(
            ConfigService configs,
            LocalizationService localization)
            : this(
                CreateTablesProvider(configs),
                CreateLocalizer(localization))
        {
        }

        /// <summary>以可替换表与本地化 seam 创建可直接 EditMode 验证的身份目录。</summary>
        internal RunMapIdentityCatalog(
            Func<Tables> tablesProvider,
            Func<string, IReadOnlyDictionary<string, object>, string> localize)
        {
            _tablesProvider = tablesProvider ?? throw new ArgumentNullException(nameof(tablesProvider));
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
        }

        /// <summary>按节点种类解析开局明牌身份，并拒绝未定义的内容 ID。</summary>
        public RunMapIdentityDescriptor Resolve(MapNodeKind kind, int contentId)
        {
            switch (kind)
            {
                case MapNodeKind.Start when contentId == 0:
                    return new RunMapIdentityDescriptor(
                        "START",
                        RunMapVisualAnchorKind.StartFlag);
                case MapNodeKind.Start:
                    throw new InvalidOperationException("Start map identity must use content id 0.");
                case MapNodeKind.Combat:
                    return ResolveEncounter(contentId);
                case MapNodeKind.Boss:
                    return ResolveG3BossTestIdentity(contentId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>从 Encounter 的首个 TbEnemy 读取 NameI18nKey 并解析当前语言名称。</summary>
        private RunMapIdentityDescriptor ResolveEncounter(int encounterId)
        {
            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException(
                    "ConfigService must be initialized before resolving map identities.");
            cfg.battle.Encounter encounter = tables.TbEncounter.GetOrDefault(encounterId)
                ?? throw new InvalidOperationException($"Encounter template {encounterId} does not exist.");
            if (encounter.EnemyTemplateIds == null || encounter.EnemyTemplateIds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Encounter template {encounterId} has no enemy identity to present.");
            }

            int enemyId = encounter.EnemyTemplateIds[0];
            cfg.battle.Enemy enemy = tables.TbEnemy.GetOrDefault(enemyId)
                ?? throw new InvalidOperationException($"Enemy template {enemyId} does not exist.");
            if (string.IsNullOrWhiteSpace(enemy.NameI18nKey))
                throw new InvalidOperationException($"Enemy template {enemyId} has no name localization key.");

            string mainEnemyName = _localize(enemy.NameI18nKey, null);
            if (string.IsNullOrWhiteSpace(mainEnemyName))
            {
                throw new InvalidOperationException(
                    $"Enemy template {enemyId} resolved an empty display name.");
            }

            return ResolveG3EncounterTestIdentity(encounterId, enemyId, mainEnemyName);
        }

        /// <summary>解析当前 G3 profile 的 5001 与仅供目录判别测试的 5002，并把首敌身份绑定到稳定剪影。</summary>
        private static RunMapIdentityDescriptor ResolveG3EncounterTestIdentity(
            int encounterId,
            int mainEnemyId,
            string mainEnemyName)
        {
            switch (encounterId)
            {
                case 5001 when mainEnemyId == 2001:
                    return new RunMapIdentityDescriptor(
                        $"SLIME PATROL\n{mainEnemyName}",
                        RunMapVisualAnchorKind.EncounterSlimeSilhouette);
                case 5002 when mainEnemyId == 2101:
                    return new RunMapIdentityDescriptor(
                        $"SENTRY LINE\n{mainEnemyName}",
                        RunMapVisualAnchorKind.EncounterSentrySilhouette);
                default:
                    throw new InvalidOperationException(
                        $"G3 test Encounter identity {encounterId} with main enemy {mainEnemyId} is not defined.");
            }
        }

        /// <summary>解析仅供 G3 地图闭环使用的三名测试 Boss 身份，不冒充真实 Boss 配置。</summary>
        private static RunMapIdentityDescriptor ResolveG3BossTestIdentity(int bossId)
        {
            switch (bossId)
            {
                case 9001:
                    return new RunMapIdentityDescriptor(
                        "BOSS ALPHA",
                        RunMapVisualAnchorKind.BossAlphaCrown);
                case 9002:
                    return new RunMapIdentityDescriptor(
                        "BOSS BETA",
                        RunMapVisualAnchorKind.BossBetaHorns);
                case 9003:
                    return new RunMapIdentityDescriptor(
                        "BOSS GAMMA",
                        RunMapVisualAnchorKind.BossGammaEye);
                default:
                    throw new InvalidOperationException(
                        $"G3 test Boss identity {bossId} is not defined.");
            }
        }

        /// <summary>从生产 ConfigService 延迟读取初始化完成后的 Luban 表。</summary>
        private static Func<Tables> CreateTablesProvider(ConfigService configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            return () => configs.Tables;
        }

        /// <summary>把生产 LocalizationService 适配为身份目录的只读文本函数。</summary>
        private static Func<string, IReadOnlyDictionary<string, object>, string> CreateLocalizer(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return localization.GetString;
        }
    }

    /// <summary>一个明牌地图节点及其悬停后半程的不可变 View 投影。</summary>
    public sealed class RunMapNodeViewModel
    {
        private readonly ReadOnlyCollection<string> _downstreamNodeIds;
        private readonly ReadOnlyCollection<string> _downstreamEdgeKeys;

        public string NodeId { get; }
        public int Layer { get; }
        public int Slot { get; }
        public MapNodeKind Kind { get; }
        public int ContentId { get; }
        public string DisplayName { get; }
        public RunMapVisualAnchorKind VisualAnchorKind { get; }
        public RunMapNodePresentationState State { get; }
        public IReadOnlyList<string> DownstreamNodeIds => _downstreamNodeIds;
        public IReadOnlyList<string> DownstreamEdgeKeys => _downstreamEdgeKeys;

        /// <summary>冻结一个节点的布局、明牌身份、交互状态与纯派生后半程。</summary>
        public RunMapNodeViewModel(
            string nodeId,
            int layer,
            int slot,
            MapNodeKind kind,
            int contentId,
            string displayName,
            RunMapVisualAnchorKind visualAnchorKind,
            RunMapNodePresentationState state,
            IReadOnlyList<string> downstreamNodeIds,
            IReadOnlyList<string> downstreamEdgeKeys)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Map node id cannot be empty.", nameof(nodeId));
            if (layer < 0)
                throw new ArgumentOutOfRangeException(nameof(layer));
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Map node display name cannot be empty.", nameof(displayName));
            ValidateVisualAnchor(kind, visualAnchorKind);

            NodeId = nodeId;
            Layer = layer;
            Slot = slot;
            Kind = kind;
            ContentId = contentId;
            DisplayName = displayName;
            VisualAnchorKind = visualAnchorKind;
            State = state;
            _downstreamNodeIds = Array.AsReadOnly(
                (downstreamNodeIds ?? throw new ArgumentNullException(nameof(downstreamNodeIds))).ToArray());
            _downstreamEdgeKeys = Array.AsReadOnly(
                (downstreamEdgeKeys ?? throw new ArgumentNullException(nameof(downstreamEdgeKeys))).ToArray());
        }

        /// <summary>约束节点种类与视觉锚点种类一致，避免 View 猜测内容身份。</summary>
        private static void ValidateVisualAnchor(
            MapNodeKind nodeKind,
            RunMapVisualAnchorKind visualAnchorKind)
        {
            bool isValid = nodeKind == MapNodeKind.Start
                ? visualAnchorKind == RunMapVisualAnchorKind.StartFlag
                : nodeKind == MapNodeKind.Combat
                    ? visualAnchorKind == RunMapVisualAnchorKind.EncounterSlimeSilhouette ||
                      visualAnchorKind == RunMapVisualAnchorKind.EncounterSentrySilhouette
                    : nodeKind == MapNodeKind.Boss &&
                      (visualAnchorKind == RunMapVisualAnchorKind.BossAlphaCrown ||
                       visualAnchorKind == RunMapVisualAnchorKind.BossBetaHorns ||
                       visualAnchorKind == RunMapVisualAnchorKind.BossGammaEye);
            if (!isValid)
            {
                throw new ArgumentException(
                    $"Visual anchor '{visualAnchorKind}' is invalid for map node kind '{nodeKind}'.",
                    nameof(visualAnchorKind));
            }
        }
    }

    /// <summary>一条冻结地图边的稳定 View 投影。</summary>
    public sealed class RunMapEdgeViewModel
    {
        public string Key { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public bool IsCompletedPath { get; }

        /// <summary>冻结一条边的端点与已走路径表现。</summary>
        public RunMapEdgeViewModel(
            string fromNodeId,
            string toNodeId,
            bool isCompletedPath)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId))
                throw new ArgumentException("From node id cannot be empty.", nameof(fromNodeId));
            if (string.IsNullOrWhiteSpace(toNodeId))
                throw new ArgumentException("To node id cannot be empty.", nameof(toNodeId));

            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Key = $"{fromNodeId}>{toNodeId}";
            IsCompletedPath = isCompletedPath;
        }
    }

    /// <summary>整张冻结 Act 地图的功能性、无业务写入 View 投影。</summary>
    public sealed class RunMapViewModel
    {
        private readonly ReadOnlyCollection<RunMapNodeViewModel> _nodes;
        private readonly ReadOnlyCollection<RunMapEdgeViewModel> _edges;

        public string Fingerprint { get; }
        public IReadOnlyList<RunMapNodeViewModel> Nodes => _nodes;
        public IReadOnlyList<RunMapEdgeViewModel> Edges => _edges;

        /// <summary>冻结地图指纹、全部节点和全部边。</summary>
        public RunMapViewModel(
            string fingerprint,
            IReadOnlyList<RunMapNodeViewModel> nodes,
            IReadOnlyList<RunMapEdgeViewModel> edges)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException("Map fingerprint cannot be empty.", nameof(fingerprint));

            Fingerprint = fingerprint;
            _nodes = Array.AsReadOnly(
                (nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray());
            _edges = Array.AsReadOnly(
                (edges ?? throw new ArgumentNullException(nameof(edges))).ToArray());
        }
    }

    /// <summary>由 Presenter 一次冻结、供 View 无业务判断渲染的完整页面投影。</summary>
    public sealed class RunEntryViewModel
    {
        private readonly IReadOnlyDictionary<RunEntryTextSlot, string> _texts;

        /// <summary>当前唯一可见页面。</summary>
        public RunEntryPage Page { get; }

        /// <summary>尚未创建 Run 时临时选择的单个 Hero。</summary>
        public int? SelectedHeroTemplateId { get; }

        /// <summary>角色确认按钮是否可用。</summary>
        public bool ConfirmEnabled { get; }

        /// <summary>当前 Run 的完整地图投影；尚未创建 Run 时为空。</summary>
        public RunMapViewModel Map { get; }

        /// <summary>主菜单继续游戏按钮是否可用。</summary>
        public bool ContinueEnabled { get; }

        /// <summary>普通检查点提交失败时是否允许显式回退；Terminal 永远不允许回退。</summary>
        public bool CanRollbackFailedSave { get; }

        /// <summary>冻结当前页面、交互状态与全部本地化文本。</summary>
        public RunEntryViewModel(
            RunEntryPage page,
            IReadOnlyDictionary<RunEntryTextSlot, string> texts,
            int? selectedHeroTemplateId,
            bool confirmEnabled,
            RunMapViewModel map,
            bool continueEnabled = false,
            bool canRollbackFailedSave = false)
        {
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));

            Page = page;
            SelectedHeroTemplateId = selectedHeroTemplateId;
            ConfirmEnabled = confirmEnabled;
            Map = map;
            ContinueEnabled = continueEnabled;
            CanRollbackFailedSave = canRollbackFailedSave;
            _texts = new ReadOnlyDictionary<RunEntryTextSlot, string>(
                new Dictionary<RunEntryTextSlot, string>(texts));
        }

        /// <summary>读取指定 TMP 槽位的已本地化文本，并拒绝不完整投影。</summary>
        public string GetText(RunEntryTextSlot slot)
        {
            if (!_texts.TryGetValue(slot, out string value))
                throw new InvalidOperationException($"Run entry text slot '{slot}' is missing.");

            return value;
        }
    }

    /// <summary>RunEntry Presenter 与 Unity View 之间唯一、无业务状态的渲染 seam。</summary>
    public interface IRunEntryView
    {
        /// <summary>按钮点击被归一化后发布的唯一动作事件。</summary>
        event Action<RunEntryAction> ActionRequested;

        /// <summary>用完整不可变投影替换当前可见页面。</summary>
        void Render(RunEntryViewModel model);
    }

    /// <summary>把入口导航与 RunState 投影到 View；跨场景业务事实只读取 RunStateStore。</summary>
    public sealed class RunEntryPresenter : IInitializable, IDisposable
    {
        private const int WarriorHeroTemplateId = 1001;
        private const int MachineGunnerHeroTemplateId = 1002;

        private const string MainTitleKey = "run.entry.title";
        private const string StartGameKey = "run.entry.menu.start";
        private const string ContinueGameKey = "run.entry.menu.continue";
        private const string SettingsKey = "run.entry.menu.settings";
        private const string CompendiumKey = "run.entry.menu.compendium";
        private const string StatisticsKey = "run.entry.menu.statistics";
        private const string BackKey = "run.entry.common.back";
        private const string ComingSoonKey = "run.entry.common.coming_soon";
        private const string SettingsTitleKey = "run.entry.settings.title";
        private const string SettingsPlaceholderKey = "run.entry.settings.placeholder";
        private const string HeroTitleKey = "run.entry.hero.title";
        private const string HeroConfirmKey = "run.entry.hero.confirm";
        private const string FutureSlotKey = "run.entry.hero.future_slot";
        private const string MapTitleKey = "run.entry.map.title";
        private const string BattleNodeKey = "run.entry.map.battle_node";
        private const string ClearedKey = "run.entry.map.cleared";
        private const string HealthKey = "run.entry.map.health";
        private const string FailureTitleKey = "run.entry.failure.title";
        private const string CancelKey = "run.entry.common.cancel";
        private const string AbandonTitleKey = "run.entry.abandon.title";
        private const string AbandonMessageKey = "run.entry.abandon.message";
        private const string AbandonConfirmKey = "run.entry.abandon.confirm";
        private const string DeleteTitleKey = "run.entry.save.delete.title";
        private const string DeleteMessageKey = "run.entry.save.delete.message";
        private const string DeleteConfirmKey = "run.entry.save.delete.confirm";
        private const string SaveIssueTitleKey = "run.entry.save.issue.title";
        private const string InvalidJsonKey = "run.entry.save.issue.invalid_json";
        private const string InvalidDocumentKey = "run.entry.save.issue.invalid_document";
        private const string UnsupportedSchemaKey = "run.entry.save.issue.unsupported_schema";
        private const string InterruptedCommitKey = "run.entry.save.issue.interrupted_commit";
        private const string IoFailureKey = "run.entry.save.issue.io_failure";
        private const string MissingConfigurationKey =
            "run.entry.save.issue.missing_configuration";
        private const string DeleteFailedKey = "run.entry.save.delete.failed";
        private const string CommitFailedKey = "run.entry.save.commit_failed";
        private const string RetrySaveKey = "run.entry.save.retry";
        private const string ExitKey = "run.entry.save.exit";
        private const string RollbackTitleKey = "run.entry.save.rollback.title";
        private const string RollbackMessageKey = "run.entry.save.rollback.message";
        private const string RollbackConfirmKey = "run.entry.save.rollback.confirm";

        private readonly IRunEntryView _view;
        private readonly RunStateStore _store;
        private readonly RunFlowService _flow;
        private readonly Func<Tables> _tablesProvider;
        private readonly IRunMapIdentityCatalog _mapIdentities;
        private readonly Func<string, IReadOnlyDictionary<string, object>, string> _localize;
        private readonly Observable<Locale> _localeChanges;

        private IDisposable _stateSubscription;
        private IDisposable _localeSubscription;
        private RunEntryPage _localPage = RunEntryPage.MainMenu;
        private int? _selectedHeroTemplateId;
        private bool _initialized;
        private bool _disposed;

        /// <summary>以生产配置、本地化服务和跨场景 Run 服务创建入口 Presenter。</summary>
        [Inject]
        public RunEntryPresenter(
            IRunEntryView view,
            RunStateStore store,
            RunFlowService flow,
            ConfigService configs,
            LocalizationService localization,
            IRunMapIdentityCatalog mapIdentities)
            : this(
                view,
                store,
                flow,
                CreateTablesProvider(configs),
                mapIdentities,
                CreateLocalizer(localization),
                RequireLocaleChanges(localization))
        {
        }

        /// <summary>以可替换配置与本地化 seam 创建可直接 EditMode 验证的 Presenter。</summary>
        internal RunEntryPresenter(
            IRunEntryView view,
            RunStateStore store,
            RunFlowService flow,
            Func<Tables> tablesProvider,
            Func<string, IReadOnlyDictionary<string, object>, string> localize,
            Observable<Locale> localeChanges)
            : this(
                view,
                store,
                flow,
                tablesProvider,
                new RunMapIdentityCatalog(tablesProvider, localize),
                localize,
                localeChanges)
        {
        }

        /// <summary>以显式地图身份目录创建可直接验证身份投影的 Presenter。</summary>
        internal RunEntryPresenter(
            IRunEntryView view,
            RunStateStore store,
            RunFlowService flow,
            Func<Tables> tablesProvider,
            IRunMapIdentityCatalog mapIdentities,
            Func<string, IReadOnlyDictionary<string, object>, string> localize,
            Observable<Locale> localeChanges)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _tablesProvider = tablesProvider ?? throw new ArgumentNullException(nameof(tablesProvider));
            _mapIdentities = mapIdentities ?? throw new ArgumentNullException(nameof(mapIdentities));
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
            _localeChanges = localeChanges ?? throw new ArgumentNullException(nameof(localeChanges));
        }

        /// <summary>一次性订阅 View、RunState 与语言变化，并立即渲染当前页面。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;

            _initialized = true;
            _view.ActionRequested += HandleAction;
            _flow.PersistenceChanged += HandlePersistenceChanged;
            _stateSubscription = _store.State.Subscribe(_ => Render());
            _localeSubscription = _localeChanges.Subscribe(_ => Render());
            _flow.RefreshSaveAvailability();
            Render();
        }

        /// <summary>解除全部场景级订阅，使旧 RunEntryScene 不留下回调。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_initialized)
            {
                _view.ActionRequested -= HandleAction;
                _flow.PersistenceChanged -= HandlePersistenceChanged;
            }
            _stateSubscription?.Dispose();
            _localeSubscription?.Dispose();
        }

        /// <summary>只把 View 意图路由到本地导航或 RunFlow，不让 View 直接写 Run。</summary>
        private void HandleAction(RunEntryAction action)
        {
            if (_disposed)
                return;

            if (_store.Current != null)
            {
                HandleRunAction(action);
                return;
            }

            HandlePreRunAction(action);
        }

        /// <summary>处理创建 Run 之前的菜单、返回与单 Hero 选择。</summary>
        private void HandlePreRunAction(RunEntryAction action)
        {
            switch (action.Kind)
            {
                case RunEntryActionKind.StartGame when _localPage == RunEntryPage.MainMenu:
                    _localPage = _flow.Persistence.HasStoredData
                        ? RunEntryPage.AbandonConfirmation
                        : RunEntryPage.HeroSelection;
                    _selectedHeroTemplateId = null;
                    Render();
                    break;
                case RunEntryActionKind.ContinueGame
                    when _localPage == RunEntryPage.MainMenu &&
                         _flow.Persistence.CanContinue:
                    _flow.ContinueSavedRun();
                    break;
                case RunEntryActionKind.OpenSettings when _localPage == RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.Settings;
                    Render();
                    break;
                case RunEntryActionKind.OpenCompendium when _localPage == RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.Compendium;
                    Render();
                    break;
                case RunEntryActionKind.OpenStatistics when _localPage == RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.Statistics;
                    Render();
                    break;
                case RunEntryActionKind.Back when _localPage != RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.MainMenu;
                    _selectedHeroTemplateId = null;
                    Render();
                    break;
                case RunEntryActionKind.SelectHero when _localPage == RunEntryPage.HeroSelection:
                    SelectHero(action.HeroTemplateId.Value);
                    break;
                case RunEntryActionKind.ConfirmHero
                    when _localPage == RunEntryPage.HeroSelection && _selectedHeroTemplateId.HasValue:
                    _flow.CreateNewRun(_selectedHeroTemplateId.Value);
                    break;
                case RunEntryActionKind.ConfirmAbandon
                    when _localPage == RunEntryPage.AbandonConfirmation:
                    RunSaveDeleteResult delete = _flow.AbandonSavedRun();
                    _localPage = delete.Status == RunSaveDeleteStatus.Success
                        ? RunEntryPage.HeroSelection
                        : RunEntryPage.MainMenu;
                    _selectedHeroTemplateId = null;
                    Render();
                    break;
            }
        }

        /// <summary>只响应由当前 Run 阶段允许的地图选择、终局离开或存档恢复动作。</summary>
        private void HandleRunAction(RunEntryAction action)
        {
            RunState state = _store.Current;
            if (action.Kind == RunEntryActionKind.EnterMapNode &&
                action.MapNodeId.HasValue &&
                state.ProgressPhase == RunProgressPhase.MapReady)
            {
                _flow.EnterMapNodeAsync(action.MapNodeId.Value).Forget();
            }
            else if (action.Kind == RunEntryActionKind.LeaveTerminalRun &&
                     state.ProgressPhase == RunProgressPhase.Terminal)
            {
                RunSaveDeleteResult delete = _flow.AbandonSavedRun();
                if (delete.Status == RunSaveDeleteStatus.Success)
                {
                    _localPage = RunEntryPage.MainMenu;
                    _selectedHeroTemplateId = null;
                }

                Render();
            }
            else if (action.Kind == RunEntryActionKind.RetrySave &&
                     _flow.Persistence.Status == RunPersistenceStatus.CommitFailed)
            {
                _flow.RetryPendingCommit();
            }
            else if (action.Kind == RunEntryActionKind.RequestExitAfterSaveFailure &&
                     _flow.Persistence.Status == RunPersistenceStatus.CommitFailed &&
                     state.ProgressPhase != RunProgressPhase.Terminal)
            {
                _localPage = RunEntryPage.RollbackConfirmation;
                Render();
            }
            else if (action.Kind == RunEntryActionKind.Back &&
                     _localPage == RunEntryPage.RollbackConfirmation)
            {
                _localPage = RunEntryPage.SaveFailure;
                Render();
            }
            else if (action.Kind == RunEntryActionKind.ConfirmRollback &&
                     _flow.Persistence.Status == RunPersistenceStatus.CommitFailed &&
                     state.ProgressPhase != RunProgressPhase.Terminal)
            {
                _localPage = RunEntryPage.MainMenu;
                _selectedHeroTemplateId = null;
                _flow.ExitPendingRunToMenu();
                Render();
            }
        }

        /// <summary>验证冻结候选并更新创建 Run 前唯一允许的临时选择。</summary>
        private void SelectHero(int heroTemplateId)
        {
            if (heroTemplateId != WarriorHeroTemplateId &&
                heroTemplateId != MachineGunnerHeroTemplateId)
            {
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            }

            _selectedHeroTemplateId = heroTemplateId;
            Render();
        }

        /// <summary>从当前 RunState 或本地预 Run 导航重建完整不可变页面投影。</summary>
        private void Render()
        {
            RunState state = _store.Current;
            RunEntryPage page = ResolvePage(state);
            int? selectedHero = state == null ? _selectedHeroTemplateId : state.HeroTemplateId;
            var texts = BuildTexts(state);
            RunMapViewModel map = state == null ? null : BuildMapViewModel(state);

            _view.Render(new RunEntryViewModel(
                page,
                texts,
                selectedHero,
                confirmEnabled: state == null &&
                                page == RunEntryPage.HeroSelection &&
                                _selectedHeroTemplateId.HasValue,
                map,
                continueEnabled: state == null &&
                                 page == RunEntryPage.MainMenu &&
                                 _flow.Persistence.CanContinue,
                canRollbackFailedSave: state != null &&
                                       state.ProgressPhase != RunProgressPhase.Terminal &&
                                       _flow.Persistence.Status == RunPersistenceStatus.CommitFailed));
        }

        /// <summary>让 RunState 决定地图或失败页；尚未创建 Run 时才使用场景内导航。</summary>
        private RunEntryPage ResolvePage(RunState state)
        {
            if (state == null)
                return _localPage;

            if (_flow.Persistence.Status == RunPersistenceStatus.CommitFailed)
            {
                return _localPage == RunEntryPage.RollbackConfirmation
                    ? RunEntryPage.RollbackConfirmation
                    : RunEntryPage.SaveFailure;
            }

            return state.ProgressPhase == RunProgressPhase.Terminal
                ? RunEntryPage.Failure
                : RunEntryPage.Map;
        }

        /// <summary>把冻结地图和当前唯一进度投影为 View 可直接绘制的完整明牌图。</summary>
        private RunMapViewModel BuildMapViewModel(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            MapDefinition map = state.MapDefinition;
            bool selectionEnabled = state.ProgressPhase == RunProgressPhase.MapReady &&
                                    _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                    _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            var selectable = selectionEnabled
                ? new HashSet<MapNodeId>(MapReachability.GetSelectableNodeIds(
                    map,
                    state.CurrentNodeId,
                    MapTraversalMode.Ordinary))
                : new HashSet<MapNodeId>();
            var completed = new HashSet<MapNodeId>(state.PathNodeIds);
            var completedEdgeKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 1; index < state.PathNodeIds.Count; index++)
            {
                completedEdgeKeys.Add(BuildEdgeKey(
                    state.PathNodeIds[index - 1],
                    state.PathNodeIds[index]));
            }

            RunMapNodeViewModel[] nodes = map.Nodes
                .OrderBy(node => node.Layer)
                .ThenBy(node => node.Slot)
                .Select(node => BuildMapNodeViewModel(
                    map,
                    node,
                    state,
                    selectable.Contains(node.Id),
                    completed.Contains(node.Id)))
                .ToArray();
            RunMapEdgeViewModel[] edges = map.Edges
                .Select(edge => new RunMapEdgeViewModel(
                    edge.FromNodeId.Value,
                    edge.ToNodeId.Value,
                    completedEdgeKeys.Contains(BuildEdgeKey(edge.FromNodeId, edge.ToNodeId))))
                .ToArray();

            return new RunMapViewModel(map.Fingerprint, nodes, edges);
        }

        /// <summary>投影一个节点的功能状态，并只为当前可选节点计算完整后半程。</summary>
        private RunMapNodeViewModel BuildMapNodeViewModel(
            MapDefinition map,
            MapNode node,
            RunState state,
            bool isSelectable,
            bool isCompleted)
        {
            MapDownstreamRoute route = isSelectable
                ? MapReachability.GetDownstreamRoute(map, node.Id)
                : null;
            string[] downstreamNodeIds = route == null
                ? Array.Empty<string>()
                : route.NodeIds.Select(nodeId => nodeId.Value).ToArray();
            string[] downstreamEdgeKeys = route == null
                ? Array.Empty<string>()
                : route.Edges.Select(edge => BuildEdgeKey(
                    edge.FromNodeId,
                    edge.ToNodeId)).ToArray();
            RunMapIdentityDescriptor identity = _mapIdentities.Resolve(
                node.Kind,
                node.ContentId);

            return new RunMapNodeViewModel(
                node.Id.Value,
                node.Layer,
                node.Slot,
                node.Kind,
                node.ContentId,
                identity.DisplayName,
                identity.VisualAnchorKind,
                ResolveMapNodePresentationState(node, state, isSelectable, isCompleted),
                downstreamNodeIds,
                downstreamEdgeKeys);
        }

        /// <summary>把节点当前事实归一化为互斥的 View 表现状态。</summary>
        private static RunMapNodePresentationState ResolveMapNodePresentationState(
            MapNode node,
            RunState state,
            bool isSelectable,
            bool isCompleted)
        {
            if (state.ProgressPhase == RunProgressPhase.BossGateReached &&
                node.Id == state.CurrentNodeId)
            {
                return RunMapNodePresentationState.BossGateReached;
            }
            if (node.Id == state.CurrentNodeId)
                return RunMapNodePresentationState.Current;
            if (isCompleted)
                return RunMapNodePresentationState.Completed;
            if (isSelectable)
                return RunMapNodePresentationState.Selectable;
            return RunMapNodePresentationState.Locked;
        }

        /// <summary>为地图边生成与 View 一致的稳定无歧义键。</summary>
        private static string BuildEdgeKey(MapNodeId fromNodeId, MapNodeId toNodeId)
        {
            return $"{fromNodeId.Value}>{toNodeId.Value}";
        }

        /// <summary>从 Luban Hero 键、当前语言与当前 Run 事实构建全部 TMP 文本。</summary>
        private IReadOnlyDictionary<RunEntryTextSlot, string> BuildTexts(RunState state)
        {
            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException("ConfigService must be initialized before rendering RunEntry.");
            cfg.battle.Hero warrior = tables.TbHero.GetOrDefault(WarriorHeroTemplateId)
                ?? throw new InvalidOperationException("Hero template 1001 does not exist.");
            cfg.battle.Hero machineGunner = tables.TbHero.GetOrDefault(MachineGunnerHeroTemplateId)
                ?? throw new InvalidOperationException("Hero template 1002 does not exist.");
            var healthArguments = new Dictionary<string, object>
            {
                ["current"] = state?.CurrentHealth ?? 0,
                ["max"] = state?.MaxHealth ?? 0,
            };
            bool deletingUnusableSave = _flow.Persistence.HasStoredData &&
                                        !_flow.Persistence.CanContinue;
            string saveIssue = BuildSaveIssueText();

            return new Dictionary<RunEntryTextSlot, string>
            {
                [RunEntryTextSlot.MainTitle] = Localize(MainTitleKey),
                [RunEntryTextSlot.StartGame] = Localize(StartGameKey),
                [RunEntryTextSlot.ContinueGame] = Localize(ContinueGameKey),
                [RunEntryTextSlot.Settings] = Localize(SettingsKey),
                [RunEntryTextSlot.Compendium] = Localize(CompendiumKey),
                [RunEntryTextSlot.Statistics] = Localize(StatisticsKey),
                [RunEntryTextSlot.Back] = Localize(BackKey),
                [RunEntryTextSlot.ComingSoon] = Localize(ComingSoonKey),
                [RunEntryTextSlot.SettingsTitle] = Localize(SettingsTitleKey),
                [RunEntryTextSlot.SettingsPlaceholder] = Localize(SettingsPlaceholderKey),
                [RunEntryTextSlot.HeroTitle] = Localize(HeroTitleKey),
                [RunEntryTextSlot.Hero1001Name] = Localize(warrior.NameI18nKey),
                [RunEntryTextSlot.Hero1002Name] = Localize(machineGunner.NameI18nKey),
                [RunEntryTextSlot.ConfirmHero] = Localize(HeroConfirmKey),
                [RunEntryTextSlot.FutureSlot] = Localize(FutureSlotKey),
                [RunEntryTextSlot.MapTitle] = Localize(MapTitleKey),
                [RunEntryTextSlot.BattleNode] = Localize(BattleNodeKey),
                [RunEntryTextSlot.Cleared] = Localize(ClearedKey),
                [RunEntryTextSlot.Health] = _localize(HealthKey, healthArguments),
                [RunEntryTextSlot.FailureTitle] = Localize(FailureTitleKey),
                [RunEntryTextSlot.LeaveRun] = Localize(ExitKey),
                [RunEntryTextSlot.Cancel] = Localize(CancelKey),
                [RunEntryTextSlot.ConfirmationTitle] = Localize(
                    deletingUnusableSave ? DeleteTitleKey : AbandonTitleKey),
                [RunEntryTextSlot.ConfirmationMessage] = Localize(
                    deletingUnusableSave ? DeleteMessageKey : AbandonMessageKey),
                [RunEntryTextSlot.ConfirmationConfirm] = Localize(
                    deletingUnusableSave ? DeleteConfirmKey : AbandonConfirmKey),
                [RunEntryTextSlot.SaveIssueTitle] = saveIssue.Length == 0
                    ? string.Empty
                    : Localize(SaveIssueTitleKey),
                [RunEntryTextSlot.SaveIssue] = saveIssue,
                [RunEntryTextSlot.SaveFailureMessage] = Localize(CommitFailedKey),
                [RunEntryTextSlot.RetrySave] = Localize(RetrySaveKey),
                [RunEntryTextSlot.Exit] = Localize(ExitKey),
                [RunEntryTextSlot.RollbackTitle] = Localize(RollbackTitleKey),
                [RunEntryTextSlot.RollbackMessage] = Localize(RollbackMessageKey),
                [RunEntryTextSlot.RollbackConfirm] = Localize(RollbackConfirmKey),
            };
        }

        /// <summary>把类型化存档故障转换为玩家可见的当前语言说明。</summary>
        private string BuildSaveIssueText()
        {
            switch (_flow.Persistence.Status)
            {
                case RunPersistenceStatus.InvalidJson:
                    return Localize(InvalidJsonKey);
                case RunPersistenceStatus.InvalidDocument:
                    return Localize(InvalidDocumentKey);
                case RunPersistenceStatus.UnsupportedSchema:
                    return Localize(UnsupportedSchemaKey);
                case RunPersistenceStatus.InterruptedCommit:
                    return Localize(InterruptedCommitKey);
                case RunPersistenceStatus.IoFailure:
                    return Localize(IoFailureKey);
                case RunPersistenceStatus.DeleteFailed:
                    return Localize(DeleteFailedKey);
                case RunPersistenceStatus.MissingHeroTemplate:
                case RunPersistenceStatus.MissingDeckTemplate:
                case RunPersistenceStatus.MissingEncounterTemplate:
                    return _localize(
                        MissingConfigurationKey,
                        new Dictionary<string, object>
                        {
                            ["kind"] = _flow.Persistence.MissingConfigurationKind,
                            ["id"] = _flow.Persistence.MissingConfigurationId ?? 0,
                        });
                case RunPersistenceStatus.MissingMapProfile:
                    return Localize(InvalidDocumentKey);
                default:
                    return string.Empty;
            }
        }

        /// <summary>存档状态变化时重建当前入口页面投影。</summary>
        private void HandlePersistenceChanged()
        {
            if (!_disposed)
                Render();
        }

        /// <summary>读取无 Smart 参数的当前语言文本。</summary>
        private string Localize(string key)
        {
            return _localize(key, null);
        }

        /// <summary>从生产 ConfigService 延迟读取初始化完成后的 Luban 表。</summary>
        private static Func<Tables> CreateTablesProvider(ConfigService configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            return () => configs.Tables;
        }

        /// <summary>把生产 LocalizationService 适配为无 Unity 静态依赖的文本函数。</summary>
        private static Func<string, IReadOnlyDictionary<string, object>, string> CreateLocalizer(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return localization.GetString;
        }

        /// <summary>验证生产本地化服务并公开其语言变化流。</summary>
        private static Observable<Locale> RequireLocaleChanges(LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return localization.LocaleChanged;
        }

        /// <summary>拒绝在场景级 Presenter 已释放后重新初始化。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RunEntryPresenter));
        }
    }
}
