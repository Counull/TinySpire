using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;
using Cysharp.Threading.Tasks;
using R3;
using TinySpire.Run;
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
        EnterBattle,
        RestartBattle,
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
        RestartBattle,
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

    /// <summary>View 发出的单个不可变入口动作；只有选择动作携带 Hero 模板标识。</summary>
    public readonly struct RunEntryAction
    {
        /// <summary>动作类型。</summary>
        public RunEntryActionKind Kind { get; }

        /// <summary>选择动作携带的 Hero 模板标识，其余动作为空。</summary>
        public int? HeroTemplateId { get; }

        /// <summary>创建并验证一个入口 UI 意图。</summary>
        public RunEntryAction(RunEntryActionKind kind, int? heroTemplateId = null)
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

            Kind = kind;
            HeroTemplateId = heroTemplateId;
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

        /// <summary>唯一战斗节点是否可点击。</summary>
        public bool BattleNodeInteractable { get; }

        /// <summary>唯一战斗节点是否已经完成。</summary>
        public bool BattleNodeCompleted { get; }

        /// <summary>主菜单继续游戏按钮是否可用。</summary>
        public bool ContinueEnabled { get; }

        /// <summary>冻结当前页面、交互状态与全部本地化文本。</summary>
        public RunEntryViewModel(
            RunEntryPage page,
            IReadOnlyDictionary<RunEntryTextSlot, string> texts,
            int? selectedHeroTemplateId,
            bool confirmEnabled,
            bool battleNodeInteractable,
            bool battleNodeCompleted,
            bool continueEnabled = false)
        {
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));

            Page = page;
            SelectedHeroTemplateId = selectedHeroTemplateId;
            ConfirmEnabled = confirmEnabled;
            BattleNodeInteractable = battleNodeInteractable;
            BattleNodeCompleted = battleNodeCompleted;
            ContinueEnabled = continueEnabled;
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
        private const string RestartBattleKey = "run.entry.failure.restart";
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
            LocalizationService localization)
            : this(
                view,
                store,
                flow,
                CreateTablesProvider(configs),
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
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _tablesProvider = tablesProvider ?? throw new ArgumentNullException(nameof(tablesProvider));
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

        /// <summary>只响应由当前 Run 节点状态允许的入战或失败重开动作。</summary>
        private void HandleRunAction(RunEntryAction action)
        {
            RunState state = _store.Current;
            if (action.Kind == RunEntryActionKind.EnterBattle &&
                state.NodeStatus == RunNodeStatus.Available)
            {
                _flow.EnterBattleNodeAsync().Forget();
            }
            else if (action.Kind == RunEntryActionKind.RestartBattle &&
                     state.NodeStatus == RunNodeStatus.Failed)
            {
                _flow.RestartFailedBattleAsync().Forget();
            }
            else if (action.Kind == RunEntryActionKind.RetrySave &&
                     _flow.Persistence.Status == RunPersistenceStatus.CommitFailed)
            {
                _flow.RetryPendingCommit();
            }
            else if (action.Kind == RunEntryActionKind.RequestExitAfterSaveFailure &&
                     _flow.Persistence.Status == RunPersistenceStatus.CommitFailed)
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
                     _flow.Persistence.Status == RunPersistenceStatus.CommitFailed)
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
            bool nodeCompleted = state?.NodeStatus == RunNodeStatus.Completed;
            bool nodeInteractable = state?.NodeStatus == RunNodeStatus.Available &&
                                    _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                    _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            int? selectedHero = state == null ? _selectedHeroTemplateId : state.HeroTemplateId;
            var texts = BuildTexts(state);

            _view.Render(new RunEntryViewModel(
                page,
                texts,
                selectedHero,
                confirmEnabled: state == null &&
                                page == RunEntryPage.HeroSelection &&
                                _selectedHeroTemplateId.HasValue,
                battleNodeInteractable: nodeInteractable,
                battleNodeCompleted: nodeCompleted,
                continueEnabled: state == null &&
                                 page == RunEntryPage.MainMenu &&
                                 _flow.Persistence.CanContinue));
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

            return state.NodeStatus == RunNodeStatus.Failed
                ? RunEntryPage.Failure
                : RunEntryPage.Map;
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
                [RunEntryTextSlot.RestartBattle] = Localize(RestartBattleKey),
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
