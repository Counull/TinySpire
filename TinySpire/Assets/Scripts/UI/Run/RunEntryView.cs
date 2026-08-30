using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TinySpire.Run;
using TinySpire.Run.History.Presentation;
using TinySpire.Settings;
using TinySpire.Settings.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TinySpire.UI.Run
{
    /// <summary>以可替换几何控件渲染入口、明牌地图与冻结奖励，不持有任何 Run 业务事实。</summary>
    [DisallowMultipleComponent]
    public sealed class RunEntryView : MonoBehaviour, IRunEntryView, IAppSettingsView, IRunStatisticsView, ICancelHandler
    {
        private static readonly string[] CjkFontCandidates =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "Noto Sans SC",
            "Arial Unicode MS",
            "Arial",
        };

        internal const string RequiredEntryGlyphs =
            "开始游戏继续设置图鉴统计返回取消开发中布局占位选择角色确认并未来队伍槽地图节点遭遇已清除后续内容未接入生命战斗奖励一张牌跳过费用失败离开战士机枪兵放弃当前存档前会删除不可用有效无法版本未知迁移检测写入读取引用配置缺失保存重试退出不回退上一份成功检查点若尚无将恢复永久撤销金币遗物药水力量护符治疗获得休息升级至宝箱领取带满商店购买事件支付最多点？。“”，；：—";

        private static readonly Color32 BackgroundColor = new Color32(18, 24, 36, 255);
        private static readonly Color32 SurfaceColor = new Color32(28, 38, 55, 248);
        private static readonly Color32 ButtonColor = new Color32(47, 62, 86, 255);
        private static readonly Color32 SelectedButtonColor = new Color32(75, 145, 205, 255);
        private static readonly Color32 DisabledButtonColor = new Color32(55, 61, 72, 255);
        private static readonly Color32 PrimaryTextColor = new Color32(235, 242, 250, 255);
        private static readonly Color32 SecondaryTextColor = new Color32(166, 181, 202, 255);
        private static readonly Color32 MapSelectableColor = new Color32(204, 146, 62, 255);
        private static readonly Color32 MapCurrentColor = new Color32(61, 126, 178, 255);
        private static readonly Color32 MapCompletedColor = new Color32(62, 126, 115, 255);
        private static readonly Color32 MapBossColor = new Color32(154, 72, 86, 255);
        private static readonly Color32 MapRouteColor = new Color32(77, 177, 207, 255);
        private static readonly Color32 MapEdgeColor = new Color32(91, 105, 125, 210);
        private static readonly Color32 MapCompletedEdgeColor = new Color32(75, 145, 205, 255);
        private static readonly Color32 MapDimmedColor = new Color32(39, 45, 56, 120);
        private static readonly Color32 SettingsHighContrastBackdropColor =
            new Color32(4, 6, 10, 248);
        private static readonly Color32 SettingsHighContrastButtonColor =
            new Color32(12, 16, 22, 255);
        private static readonly Color32 SettingsHighContrastTextColor =
            new Color32(255, 246, 140, 255);

        [Header("Run Entry Visuals")]
        [SerializeField]
        private Sprite _entryBackground;

        [SerializeField]
        private Texture2D _entryPaperTexture;

        private readonly Dictionary<RunEntryPage, GameObject> _pages =
            new Dictionary<RunEntryPage, GameObject>();
        private readonly Dictionary<string, Button> _buttons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly List<Button> _boundButtons = new List<Button>();
        private readonly Dictionary<string, Button> _mapNodeButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, TMP_Text> _mapNodeLabels =
            new Dictionary<string, TMP_Text>(StringComparer.Ordinal);
        private readonly Dictionary<string, TMP_Text> _mapNodeIdentityIds =
            new Dictionary<string, TMP_Text>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Image>> _mapNodeAnchorImages =
            new Dictionary<string, IReadOnlyList<Image>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Image> _mapEdgeImages =
            new Dictionary<string, Image>(StringComparer.Ordinal);

        private TMP_FontAsset _fontAsset;
        private bool _ownsFontAsset;
        private bool _built;
        private EventSystem _eventSystem;
        private RunEntryPage? _renderedPage;
        private EntryPaperStackView _entryPaperStack;
        private GameObject _secondarySurface;

        private TMP_Text _mainTitle;
        private TMP_Text _continueGameText;
        private TMP_Text _startGameText;
        private TMP_Text _settingsMenuText;
        private TMP_Text _compendiumMenuText;
        private TMP_Text _statisticsMenuText;
        private TMP_Text _saveIssueTitleText;
        private TMP_Text _saveIssueText;
        private Button _continueGameButton;

        private TMP_Text _heroTitle;
        private TMP_Text _warriorText;
        private TMP_Text _machineGunnerText;
        private TMP_Text _futureSlotText;
        private TMP_Text _confirmHeroText;
        private TMP_Text _heroBackText;
        private Button _warriorButton;
        private Button _machineGunnerButton;
        private Button _confirmHeroButton;

        private readonly Dictionary<AppSettingsTextSlot, TMP_Text> _appSettingsTexts =
            new Dictionary<AppSettingsTextSlot, TMP_Text>();
        private readonly List<TMP_Text> _appSettingsStyledTexts = new List<TMP_Text>();
        private readonly List<Button> _appSettingsButtons = new List<Button>();
        private readonly Dictionary<TMP_Text, float> _appSettingsBaseFontSizes =
            new Dictionary<TMP_Text, float>();
        private readonly Dictionary<TMP_Text, float> _appSettingsBaseFontSizeMins =
            new Dictionary<TMP_Text, float>();
        private readonly Dictionary<TMP_Text, float> _appSettingsBaseFontSizeMaxes =
            new Dictionary<TMP_Text, float>();
        private readonly Dictionary<TMP_Text, Color> _appSettingsBaseTextColors =
            new Dictionary<TMP_Text, Color>();
        private readonly Dictionary<Button, Color> _appSettingsBaseButtonColors =
            new Dictionary<Button, Color>();
        private readonly Dictionary<Button, Selectable.Transition> _appSettingsBaseTransitions =
            new Dictionary<Button, Selectable.Transition>();
        private RectTransform _settingsPage;
        private Image _settingsAccessibilityBackdrop;
        private TMP_Text _settingsFailureText;
        private TMP_Text _settingsBackText;
        private Action<AppSettingsAction> _appSettingsActionRequested;
        private AppSettingsSnapshot _lastAppSettings;

        private TMP_Text _compendiumTitle;
        private TMP_Text _compendiumPlaceholder;
        private TMP_Text _compendiumBackText;

        private TMP_Text _statisticsTitle;
        private TMP_Text _statisticsTotalRuns;
        private TMP_Text _statisticsVictory;
        private TMP_Text _statisticsDefeat;
        private TMP_Text _statisticsAbandoned;
        private TMP_Text _statisticsVictoryRate;
        private TMP_Text _statisticsState;
        private RectTransform _statisticsHeroRowsRoot;
        private readonly Dictionary<int, TMP_Text> _statisticsHeroRows =
            new Dictionary<int, TMP_Text>();
        private TMP_Text _statisticsBackText;

        private TMP_Text _mapTitle;
        private TMP_Text _mapHealth;
        private TMP_Text _mapAbandonText;
        private Button _mapAbandonButton;
        private RectTransform _mapGraphHost;
        private RectTransform _mapGraphRoot;
        private RunMapViewModel _renderedMap;
        private string _renderedMapFingerprint;

        private TMP_Text _cardRewardTitle;
        private readonly Button[] _cardRewardCandidateButtons = new Button[3];
        private readonly TMP_Text[] _cardRewardCandidateTexts = new TMP_Text[3];
        private readonly RunEntryAction?[] _cardRewardCandidateActions = new RunEntryAction?[3];
        private TMP_Text _skipCardRewardText;
        private Button _skipCardRewardButton;
        private RunEntryAction? _skipCardRewardAction;

        private RectTransform _restPage;
        private TMP_Text _restTitle;
        private TMP_Text _restHealText;
        private Button _restHealButton;
        private RunEntryAction? _restHealAction;
        private readonly List<Button> _restUpgradeButtons = new List<Button>();
        private readonly List<TMP_Text> _restUpgradeTexts = new List<TMP_Text>();
        private readonly List<RunEntryAction?> _restUpgradeActions =
            new List<RunEntryAction?>();

        private TMP_Text _chestTitle;
        private TMP_Text _chestPotionText;
        private TMP_Text _chestCapacityText;
        private TMP_Text _chestClaimText;
        private TMP_Text _chestSkipText;
        private Button _chestClaimButton;
        private Button _chestSkipButton;
        private RunEntryAction? _chestClaimAction;
        private RunEntryAction? _chestSkipAction;

        private TMP_Text _shopTitle;
        private readonly Button[] _shopStockButtons = new Button[3];
        private readonly TMP_Text[] _shopStockTexts = new TMP_Text[3];
        private readonly RunEntryAction?[] _shopStockActions = new RunEntryAction?[3];
        private TMP_Text _shopLeaveText;
        private Button _shopLeaveButton;
        private RunEntryAction? _shopLeaveAction;

        private TMP_Text _eventTitle;
        private TMP_Text _eventGainGoldText;
        private TMP_Text _eventPaidHealText;
        private Button _eventGainGoldButton;
        private Button _eventPaidHealButton;
        private RunEntryAction? _eventGainGoldAction;
        private RunEntryAction? _eventPaidHealAction;

        private RectTransform _holdingsPanel;
        private TMP_Text _holdingsGoldText;
        private TMP_Text _holdingsRelicsText;
        private TMP_Text _holdingsPotionsText;

        private TMP_Text _failureTitle;
        private TMP_Text _failureHealth;
        private TMP_Text _leaveTerminalRunText;

        private TMP_Text _confirmationTitle;
        private TMP_Text _confirmationMessage;
        private TMP_Text _confirmationConfirmText;
        private TMP_Text _confirmationCancelText;

        private TMP_Text _saveFailureMessage;
        private TMP_Text _saveFailureHealth;
        private TMP_Text _retrySaveText;
        private TMP_Text _saveFailureExitText;
        private Button _saveFailureExitButton;

        private TMP_Text _rollbackTitle;
        private TMP_Text _rollbackMessage;
        private TMP_Text _rollbackConfirmText;
        private TMP_Text _rollbackCancelText;

        /// <summary>所有按钮被归一化后发布的唯一 UI 动作流。</summary>
        public event Action<RunEntryAction> ActionRequested;

        /// <summary>把标准 Cancel/Escape 归一化为既有 Back 意图，页面门禁继续由 Presenter 决定。</summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (!isActiveAndEnabled)
                return;

            ActionRequested?.Invoke(new RunEntryAction(RunEntryActionKind.Back));
            eventData?.Use();
        }

        /// <summary>显式公开独立设置动作流，避免与 RunEntry 动作使用同名 public event。</summary>
        event Action<AppSettingsAction> IAppSettingsView.ActionRequested
        {
            add => _appSettingsActionRequested += value;
            remove => _appSettingsActionRequested -= value;
        }

        /// <summary>由独立设置 Presenter 渲染设置页，不参与 RunEntry 页面状态投影。</summary>
        void IAppSettingsView.Render(AppSettingsViewModel model)
        {
            RenderAppSettings(model);
        }

        /// <summary>由只读历史 Presenter 渲染 Statistics 页面，不改变 RunEntry 当前页面。</summary>
        void IRunStatisticsView.Render(StatisticsViewModel model)
        {
            RenderStatistics(model);
        }

        /// <summary>场景对象唤醒时建立一次功能性几何 UI。</summary>
        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>释放场景级动态字体与按钮监听，不留下旧入口回调。</summary>
        private void OnDestroy()
        {
            foreach (Button button in _boundButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }

            if (_ownsFontAsset && _fontAsset != null)
                DestroyOwnedObject(_fontAsset);
        }

        /// <summary>根据完整不可变 ViewModel 切页、赋文案并设置交互状态。</summary>
        public void Render(RunEntryViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            EnsureBuilt();
            bool pageChanged = !_renderedPage.HasValue || _renderedPage.Value != model.Page;
            foreach (KeyValuePair<RunEntryPage, GameObject> page in _pages)
                page.Value.SetActive(page.Key == model.Page);
            if (_secondarySurface != null)
                _secondarySurface.SetActive(model.Page != RunEntryPage.MainMenu);
            if (_entryPaperStack != null)
            {
                if (model.Page == RunEntryPage.MainMenu)
                    _entryPaperStack.TryPlayEntrance();
                else
                    _entryPaperStack.ResolveWithoutEntrance();
            }

            _mainTitle.text = model.GetText(RunEntryTextSlot.MainTitle);
            _continueGameText.text = model.GetText(RunEntryTextSlot.ContinueGame);
            _continueGameButton.interactable = model.ContinueEnabled;
            _startGameText.text = model.GetText(RunEntryTextSlot.StartGame);
            _settingsMenuText.text = model.GetText(RunEntryTextSlot.Settings);
            _compendiumMenuText.text = model.GetText(RunEntryTextSlot.Compendium);
            _statisticsMenuText.text = model.GetText(RunEntryTextSlot.Statistics);
            _saveIssueTitleText.text = model.GetText(RunEntryTextSlot.SaveIssueTitle);
            _saveIssueText.text = model.GetText(RunEntryTextSlot.SaveIssue);

            _heroTitle.text = model.GetText(RunEntryTextSlot.HeroTitle);
            _warriorText.text = model.GetText(RunEntryTextSlot.Hero1001Name);
            _machineGunnerText.text = model.GetText(RunEntryTextSlot.Hero1002Name);
            _futureSlotText.text = model.GetText(RunEntryTextSlot.FutureSlot);
            _confirmHeroText.text = model.GetText(RunEntryTextSlot.ConfirmHero);
            _heroBackText.text = model.GetText(RunEntryTextSlot.Back);
            _confirmHeroButton.interactable = model.ConfirmEnabled;
            SetButtonColor(
                _warriorButton,
                model.SelectedHeroTemplateId == 1001 ? SelectedButtonColor : ButtonColor);
            SetButtonColor(
                _machineGunnerButton,
                model.SelectedHeroTemplateId == 1002 ? SelectedButtonColor : ButtonColor);

            _settingsBackText.text = model.GetText(RunEntryTextSlot.Back);

            _compendiumTitle.text = model.GetText(RunEntryTextSlot.Compendium);
            _compendiumPlaceholder.text = model.GetText(RunEntryTextSlot.ComingSoon);
            _compendiumBackText.text = model.GetText(RunEntryTextSlot.Back);

            _statisticsBackText.text = model.GetText(RunEntryTextSlot.Back);

            _mapTitle.text = model.GetText(RunEntryTextSlot.MapTitle);
            _mapHealth.text = model.GetText(RunEntryTextSlot.Health);
            _mapAbandonText.text = model.GetText(RunEntryTextSlot.ConfirmationConfirm);
            _mapAbandonButton.interactable = model.CanAbandonActiveRun;
            RenderMap(model.Map);

            _cardRewardTitle.text = model.GetText(RunEntryTextSlot.CardRewardTitle);
            _skipCardRewardText.text = model.GetText(RunEntryTextSlot.SkipCardReward);
            RenderCardReward(model.CardReward);
            _restTitle.text = model.GetText(RunEntryTextSlot.RestTitle);
            RenderRest(model.Rest);
            _chestTitle.text = model.GetText(RunEntryTextSlot.ChestTitle);
            RenderChest(model.Chest);
            _shopTitle.text = model.GetText(RunEntryTextSlot.ShopTitle);
            RenderShop(model.Shop);
            _eventTitle.text = model.GetText(RunEntryTextSlot.EventTitle);
            RenderEvent(model.Event);
            RenderHoldings(model.Holdings);

            _failureTitle.text = model.GetText(RunEntryTextSlot.FailureTitle);
            string failureIssue = model.GetText(RunEntryTextSlot.SaveIssue);
            _failureHealth.text = failureIssue.Length == 0
                ? model.GetText(RunEntryTextSlot.Health)
                : $"{model.GetText(RunEntryTextSlot.Health)}\n{failureIssue}";
            _leaveTerminalRunText.text = model.GetText(RunEntryTextSlot.LeaveRun);

            _confirmationTitle.text = model.GetText(RunEntryTextSlot.ConfirmationTitle);
            _confirmationMessage.text = model.GetText(RunEntryTextSlot.ConfirmationMessage);
            _confirmationConfirmText.text = model.GetText(RunEntryTextSlot.ConfirmationConfirm);
            _confirmationCancelText.text = model.GetText(RunEntryTextSlot.Cancel);

            _saveFailureMessage.text = model.GetText(RunEntryTextSlot.SaveFailureMessage);
            _saveFailureHealth.text = model.GetText(RunEntryTextSlot.Health);
            _retrySaveText.text = model.GetText(RunEntryTextSlot.RetrySave);
            _saveFailureExitText.text = model.GetText(RunEntryTextSlot.Exit);
            _saveFailureExitButton.interactable = model.CanRollbackFailedSave;
            _saveFailureExitButton.gameObject.SetActive(model.CanRollbackFailedSave);

            _rollbackTitle.text = model.GetText(RunEntryTextSlot.RollbackTitle);
            _rollbackMessage.text = model.GetText(RunEntryTextSlot.RollbackMessage);
            _rollbackConfirmText.text = model.GetText(RunEntryTextSlot.RollbackConfirm);
            _rollbackCancelText.text = model.GetText(RunEntryTextSlot.Cancel);
            RefreshAccessibilityTargets();

            _renderedPage = model.Page;
            EnsurePageFocus(model.Page, pageChanged);
        }

        /// <summary>页面切换时设置首焦点；同页只在当前焦点失效或离开该页时修复，不抢玩家选择。</summary>
        private void EnsurePageFocus(RunEntryPage page, bool pageChanged)
        {
            GameObject selected = _eventSystem.currentSelectedGameObject;
            bool validSelection = selected != null &&
                                  selected.activeInHierarchy &&
                                  selected.transform.IsChildOf(_pages[page].transform);
            if (pageChanged || !validSelection)
                FocusFirstButton(page);
        }

        /// <summary>页面切换完成后选择层级中第一个可见可交互按钮。</summary>
        private void FocusFirstButton(RunEntryPage page)
        {
            Button first = _pages[page]
                .GetComponentsInChildren<Button>(includeInactive: false)
                .FirstOrDefault(button => button.IsActive() && button.IsInteractable());
            _eventSystem.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        /// <summary>只更新设置 Presenter 所有的文本与可访问性表现，不改变 RunEntry 当前页面。</summary>
        private void RenderAppSettings(AppSettingsViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            EnsureBuilt();
            foreach (KeyValuePair<AppSettingsTextSlot, TMP_Text> item in _appSettingsTexts)
                item.Value.text = model.GetText(item.Key);
            _settingsFailureText.text = model.FailureText;
            _lastAppSettings = model.Settings;
            _entryPaperStack?.SetReducedMotion(model.Settings.ReducedMotion);
            RefreshAccessibilityTargets();
        }

        /// <summary>完整替换 Statistics 文案、全局数值、状态和稳定 Hero 行。</summary>
        private void RenderStatistics(StatisticsViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            EnsureBuilt();
            _statisticsTitle.text = model.GetText(RunStatisticsTextSlot.Title);
            HideStatisticsHeroRows();
            if (model.Status == RunStatisticsViewStatus.Unavailable)
            {
                ClearStatisticsTotals();
                _statisticsState.text = model.FailureText;
                RefreshAccessibilityTargets();
                return;
            }

            _statisticsTotalRuns.text = FormatStatistic(
                model.GetText(RunStatisticsTextSlot.TotalRunsLabel),
                model.TotalRuns.Value);
            _statisticsVictory.text = FormatStatistic(
                model.GetText(RunStatisticsTextSlot.VictoryLabel),
                model.VictoryCount.Value);
            _statisticsDefeat.text = FormatStatistic(
                model.GetText(RunStatisticsTextSlot.DefeatLabel),
                model.DefeatCount.Value);
            _statisticsAbandoned.text = FormatStatistic(
                model.GetText(RunStatisticsTextSlot.AbandonedLabel),
                model.AbandonedCount.Value);
            _statisticsVictoryRate.text =
                $"{model.GetText(RunStatisticsTextSlot.VictoryRateLabel)}: {model.VictoryRateText}";
            _statisticsState.text = model.IsEmpty
                ? model.GetText(RunStatisticsTextSlot.EmptyHistory)
                : string.Empty;
            RenderStatisticsHeroRows(model);
            RefreshAccessibilityTargets();
        }

        /// <summary>把一项统计标签与整数值组合成稳定可读文本。</summary>
        private static string FormatStatistic(string label, int value)
        {
            return $"{label}: {value}";
        }

        /// <summary>不可用状态清空全部数值，避免把加载失败伪装成零历史。</summary>
        private void ClearStatisticsTotals()
        {
            _statisticsTotalRuns.text = string.Empty;
            _statisticsVictory.text = string.Empty;
            _statisticsDefeat.text = string.Empty;
            _statisticsAbandoned.text = string.Empty;
            _statisticsVictoryRate.text = string.Empty;
        }

        /// <summary>先隐藏已建立 Hero 行，后续模型只重新启用当前集合。</summary>
        private void HideStatisticsHeroRows()
        {
            foreach (TMP_Text row in _statisticsHeroRows.Values)
                row.gameObject.SetActive(false);
        }

        /// <summary>按 ViewModel 冻结顺序复用或建立 Hero 行并更新完整统计。</summary>
        private void RenderStatisticsHeroRows(StatisticsViewModel model)
        {
            for (int index = 0; index < model.HeroRows.Count; index++)
            {
                StatisticsHeroRowViewModel row = model.HeroRows[index];
                if (!_statisticsHeroRows.TryGetValue(row.HeroTemplateId, out TMP_Text text))
                {
                    text = CreateText(
                        $"StatisticsHero_{row.HeroTemplateId}",
                        _statisticsHeroRowsRoot,
                        19f,
                        FontStyles.Normal,
                        PrimaryTextColor,
                        0f,
                        800f,
                        46f);
                    _statisticsHeroRows.Add(row.HeroTemplateId, text);
                }

                SetCenteredRect(
                    text.rectTransform,
                    new Vector2(0f, 70f - index * 52f),
                    new Vector2(800f, 46f));
                text.text = FormatStatisticsHeroRow(model, row);
                text.gameObject.SetActive(true);
            }
        }

        /// <summary>用全局同一组标签格式化一行 Hero V/D/A 与胜率。</summary>
        private static string FormatStatisticsHeroRow(
            StatisticsViewModel model,
            StatisticsHeroRowViewModel row)
        {
            return
                $"{row.HeroText}  " +
                $"{model.GetText(RunStatisticsTextSlot.TotalRunsLabel)}: {row.TotalRuns}  " +
                $"{model.GetText(RunStatisticsTextSlot.VictoryLabel)}: {row.VictoryCount}  " +
                $"{model.GetText(RunStatisticsTextSlot.DefeatLabel)}: {row.DefeatCount}  " +
                $"{model.GetText(RunStatisticsTextSlot.AbandonedLabel)}: {row.AbandonedCount}  " +
                $"{model.GetText(RunStatisticsTextSlot.VictoryRateLabel)}: {row.VictoryRateText}";
        }

        /// <summary>仅供 EditMode 测试显式建立与 Awake 相同的 UI。</summary>
        internal void BuildForTesting()
        {
            EnsureBuilt();
        }

        /// <summary>仅供 EditMode 测试在 Awake 前注入临时视觉资产，生产引用仍来自 Scene 序列化。</summary>
        internal void ConfigureVisualAssetsForTesting(Sprite background, Texture2D paperTexture)
        {
            if (_built)
                throw new InvalidOperationException("Visual assets must be configured before RunEntryView is built.");

            _entryBackground = background != null
                ? background
                : throw new ArgumentNullException(nameof(background));
            _entryPaperTexture = paperTexture != null
                ? paperTexture
                : throw new ArgumentNullException(nameof(paperTexture));
        }

        /// <summary>仅供 EditMode 视觉测试读取动态建立的纸叠组件。</summary>
        internal EntryPaperStackView GetPaperStackForTesting()
        {
            EnsureBuilt();
            return _entryPaperStack;
        }

        /// <summary>仅供 EditMode 合约测试读取指定页面对象。</summary>
        internal GameObject GetPageForTesting(RunEntryPage page)
        {
            EnsureBuilt();
            return _pages[page];
        }

        /// <summary>仅供 EditMode 合约测试按稳定对象名读取按钮。</summary>
        internal Button GetButtonForTesting(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Button object name cannot be empty.", nameof(objectName));

            EnsureBuilt();
            return _buttons.TryGetValue(objectName, out Button button)
                ? button
                : throw new InvalidOperationException($"Run entry button '{objectName}' does not exist.");
        }

        /// <summary>建立 Canvas、事件系统及全部互斥页面；重复调用不再绑定监听。</summary>
        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            _fontAsset = CreateRuntimeFontAsset();

            CreateEventSystem();
            RectTransform canvas = CreateCanvas();
            _entryPaperStack = CreateEntryPaperStack(canvas);
            if (_entryPaperStack == null)
            {
                CreatePanel(
                    "Background",
                    canvas,
                    BackgroundColor,
                    Vector2.zero,
                    Vector2.zero,
                    stretch: true);
            }

            RectTransform pagesRoot = CreateContainer("PagesRoot", canvas, stretch: true);
            RectTransform surface = CreatePanel(
                "ContentSurface",
                pagesRoot,
                SurfaceColor,
                Vector2.zero,
                new Vector2(940f, 760f),
                stretch: false);

            BuildMainMenuPage(_entryPaperStack != null ? pagesRoot : surface);
            BuildHeroSelectionPage(surface);
            BuildSettingsPage(surface);
            BuildComingSoonPage(surface, RunEntryPage.Compendium);
            BuildStatisticsPage(surface);
            BuildMapPage(surface);
            BuildCardRewardPage(surface);
            BuildRestPage(surface);
            BuildChestPage(surface);
            BuildShopPage(surface);
            BuildEventPage(surface);
            BuildFailurePage(surface);
            BuildAbandonConfirmationPage(surface);
            BuildSaveFailurePage(surface);
            BuildRollbackConfirmationPage(surface);
            BuildHoldingsPanel(surface);

            foreach (GameObject page in _pages.Values)
                page.SetActive(false);
            _pages[RunEntryPage.MainMenu].SetActive(true);
            if (_entryPaperStack != null)
            {
                _secondarySurface = surface.gameObject;
                _secondarySurface.SetActive(false);
            }
            CaptureAccessibilityBaselines();
        }

        /// <summary>资产成对存在时在动态 Canvas 上建立纯视觉纸叠；测试缺省仍保留功能性旧背景。</summary>
        private EntryPaperStackView CreateEntryPaperStack(RectTransform canvas)
        {
            if (_entryBackground == null && _entryPaperTexture == null)
                return null;
            if (_entryBackground == null || _entryPaperTexture == null)
            {
                Debug.LogError(
                    "RunEntry visual assets are only partially configured; using the functional fallback UI.");
                return null;
            }

            var paperStack = canvas.gameObject.AddComponent<EntryPaperStackView>();
            paperStack.Compose(_entryBackground, _entryPaperTexture);
            return paperStack;
        }

        /// <summary>创建只消费 Input System 默认 UI Actions 的场景级 EventSystem。</summary>
        private void CreateEventSystem()
        {
            var eventObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(transform, worldPositionStays: false);
            _eventSystem = eventObject.GetComponent<EventSystem>();
            // InputSystemUIInputModule.OnEnable 会自动分配默认 UI Actions；重复分配会破坏跨 Play/Test 复用的包级静态状态。
        }

        /// <summary>创建按 1920×1080 等比缩放的 Overlay Canvas。</summary>
        private RectTransform CreateCanvas()
        {
            var canvasObject = new GameObject(
                "RunEntryCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, worldPositionStays: false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject.GetComponent<RectTransform>();
        }

        /// <summary>建立包含继续、开始与既有辅助入口的主菜单页，并按资产是否配置选择视觉或功能性布局。</summary>
        private void BuildMainMenuPage(RectTransform parent)
        {
            if (_entryPaperStack == null)
                BuildFallbackMainMenuPage(parent);
            else
                BuildVisualMainMenuPage(parent);
        }

        /// <summary>为无生产资产的 EditMode seam 保留原有功能性主菜单几何与配色。</summary>
        private void BuildFallbackMainMenuPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.MainMenu, parent);
            _mainTitle = CreateText("MainTitle", page, 52f, FontStyles.Bold, PrimaryTextColor, 285f, 700f, 80f);
            (_continueGameButton, _continueGameText) = CreateButton(
                "ContinueGameButton",
                page,
                new RunEntryAction(RunEntryActionKind.ContinueGame),
                150f);
            _startGameText = CreateButton(
                "StartGameButton",
                page,
                new RunEntryAction(RunEntryActionKind.StartGame),
                60f).label;
            _settingsMenuText = CreateButton(
                "SettingsButton",
                page,
                new RunEntryAction(RunEntryActionKind.OpenSettings),
                -30f).label;
            _compendiumMenuText = CreateButton(
                "CompendiumButton",
                page,
                new RunEntryAction(RunEntryActionKind.OpenCompendium),
                -120f).label;
            _statisticsMenuText = CreateButton(
                "StatisticsButton",
                page,
                new RunEntryAction(RunEntryActionKind.OpenStatistics),
                -210f).label;
            _saveIssueText = CreateText(
                "SaveIssue",
                page,
                18f,
                FontStyles.Normal,
                SecondaryTextColor,
                -325f,
                820f,
                68f);
            _saveIssueTitleText = CreateText(
                "SaveIssueTitle",
                page,
                19f,
                FontStyles.Bold,
                PrimaryTextColor,
                -275f,
                820f,
                34f);
        }

        /// <summary>按 ENTRY-OVERALL-V06 的左侧安全区建立五个既有菜单控件，不新增文案或动作。</summary>
        private void BuildVisualMainMenuPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.MainMenu, parent);
            RectTransform content = CreateContainer(
                "MainMenuContent",
                page,
                stretch: false,
                size: new Vector2(620f, 900f));
            CanvasGroup canvasGroup = content.gameObject.AddComponent<CanvasGroup>();
            _entryPaperStack.BindMainMenuContent(content, canvasGroup);
            Color primary = _entryPaperStack.MenuPrimaryTextColor;
            Color secondary = _entryPaperStack.MenuSecondaryTextColor;

            _mainTitle = CreateText(
                "MainTitle",
                content,
                58f,
                FontStyles.Bold,
                primary,
                305f,
                520f,
                90f);
            (_continueGameButton, _continueGameText) = CreateVisualMainMenuButton(
                "ContinueGameButton",
                content,
                new RunEntryAction(RunEntryActionKind.ContinueGame),
                104f);
            _startGameText = CreateVisualMainMenuButton(
                "StartGameButton",
                content,
                new RunEntryAction(RunEntryActionKind.StartGame),
                -8f).label;
            _settingsMenuText = CreateVisualMainMenuButton(
                "SettingsButton",
                content,
                new RunEntryAction(RunEntryActionKind.OpenSettings),
                -120f).label;
            _compendiumMenuText = CreateVisualMainMenuButton(
                "CompendiumButton",
                content,
                new RunEntryAction(RunEntryActionKind.OpenCompendium),
                -232f).label;
            _statisticsMenuText = CreateVisualMainMenuButton(
                "StatisticsButton",
                content,
                new RunEntryAction(RunEntryActionKind.OpenStatistics),
                -344f).label;
            _saveIssueTitleText = CreateText(
                "SaveIssueTitle",
                content,
                19f,
                FontStyles.Bold,
                primary,
                -435f,
                520f,
                34f);
            _saveIssueText = CreateText(
                "SaveIssue",
                content,
                18f,
                FontStyles.Normal,
                secondary,
                -485f,
                540f,
                68f);
        }

        /// <summary>建立两名可选 Hero、一个禁用未来槽位与确认/返回的角色选择页。</summary>
        private void BuildHeroSelectionPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.HeroSelection, parent);
            _heroTitle = CreateText("HeroTitle", page, 42f, FontStyles.Bold, PrimaryTextColor, 280f, 700f, 64f);

            (_warriorButton, _warriorText) = CreateButton(
                "Hero1001Button",
                page,
                new RunEntryAction(RunEntryActionKind.SelectHero, 1001),
                95f,
                width: 360f,
                height: 150f,
                x: -205f);
            (_machineGunnerButton, _machineGunnerText) = CreateButton(
                "Hero1002Button",
                page,
                new RunEntryAction(RunEntryActionKind.SelectHero, 1002),
                95f,
                width: 360f,
                height: 150f,
                x: 205f);

            var futureSlot = CreatePassivePanel("FutureTeamSlot", page, -70f, 420f, 68f);
            _futureSlotText = futureSlot.label;
            futureSlot.image.color = DisabledButtonColor;

            (_confirmHeroButton, _confirmHeroText) = CreateButton(
                "ConfirmHeroButton",
                page,
                new RunEntryAction(RunEntryActionKind.ConfirmHero),
                -185f);
            _heroBackText = CreateButton(
                "HeroBackButton",
                page,
                new RunEntryAction(RunEntryActionKind.Back),
                -275f,
                width: 260f).label;
        }

        /// <summary>建立七组实际设置控件，并保留由 RunEntry seam 所有的返回按钮。</summary>
        private void BuildSettingsPage(RectTransform parent)
        {
            _settingsPage = CreatePage(RunEntryPage.Settings, parent);
            _settingsAccessibilityBackdrop = CreatePanel(
                "SettingsAccessibilityBackdrop",
                _settingsPage,
                Color.clear,
                Vector2.zero,
                new Vector2(880f, 690f),
                stretch: false).GetComponent<Image>();
            _settingsAccessibilityBackdrop.raycastTarget = false;

            RegisterAppSettingsText(
                AppSettingsTextSlot.Title,
                CreateSettingsText(
                    "SettingsTitle",
                    42f,
                    FontStyles.Bold,
                    PrimaryTextColor,
                    300f,
                    760f,
                    58f,
                    0f));
            BuildSettingsSingleButtonRow(
                "Language",
                AppSettingsTextSlot.LanguageLabel,
                AppSettingsTextSlot.LanguageValue,
                "LanguageButton",
                AppSettingsActionKind.CycleLocale,
                200f);
            BuildSettingsVolumeRow(142f);
            BuildSettingsSingleButtonRow(
                "DisplayMode",
                AppSettingsTextSlot.DisplayModeLabel,
                AppSettingsTextSlot.DisplayModeValue,
                "DisplayModeButton",
                AppSettingsActionKind.ToggleDisplayMode,
                84f);
            BuildSettingsResolutionRow(26f);
            BuildSettingsSingleButtonRow(
                "TextScale",
                AppSettingsTextSlot.TextScaleLabel,
                AppSettingsTextSlot.TextScaleValue,
                "TextScaleButton",
                AppSettingsActionKind.CycleTextScale,
                -32f);
            BuildSettingsSingleButtonRow(
                "HighContrast",
                AppSettingsTextSlot.HighContrastLabel,
                AppSettingsTextSlot.HighContrastValue,
                "HighContrastButton",
                AppSettingsActionKind.ToggleHighContrast,
                -90f);
            BuildSettingsSingleButtonRow(
                "ReducedMotion",
                AppSettingsTextSlot.ReducedMotionLabel,
                AppSettingsTextSlot.ReducedMotionValue,
                "ReducedMotionButton",
                AppSettingsActionKind.ToggleReducedMotion,
                -148f);

            _settingsFailureText = CreateSettingsText(
                "SettingsFailureText",
                20f,
                FontStyles.Bold,
                MapBossColor,
                -207f,
                760f,
                38f,
                0f);
            (Button settingsBackButton, TMP_Text settingsBackText) = CreateButton(
                "SettingsBackButton",
                _settingsPage,
                new RunEntryAction(RunEntryActionKind.Back),
                -292f,
                width: 260f,
                height: 54f);
            _settingsBackText = settingsBackText;
            TrackAppSettingsText(_settingsBackText);
            TrackAppSettingsButton(settingsBackButton);
        }

        /// <summary>建立标签与单一循环按钮组成的一行设置。</summary>
        private void BuildSettingsSingleButtonRow(
            string prefix,
            AppSettingsTextSlot labelSlot,
            AppSettingsTextSlot valueSlot,
            string buttonName,
            AppSettingsActionKind actionKind,
            float y)
        {
            RegisterAppSettingsText(
                labelSlot,
                CreateSettingsText(
                    $"{prefix}Label",
                    22f,
                    FontStyles.Normal,
                    SecondaryTextColor,
                    y,
                    270f,
                    42f,
                    -245f));
            (Button _, TMP_Text label) = CreateAppSettingsButton(
                buttonName,
                actionKind,
                y,
                330f,
                46f,
                180f);
            RegisterAppSettingsText(valueSlot, label);
        }

        /// <summary>建立减号、当前值与加号组成的主音量设置行。</summary>
        private void BuildSettingsVolumeRow(float y)
        {
            RegisterAppSettingsText(
                AppSettingsTextSlot.MasterVolumeLabel,
                CreateSettingsText(
                    "MasterVolumeLabel",
                    22f,
                    FontStyles.Normal,
                    SecondaryTextColor,
                    y,
                    270f,
                    42f,
                    -245f));
            (Button _, TMP_Text decreaseLabel) = CreateAppSettingsButton(
                "MasterVolumeDecreaseButton",
                AppSettingsActionKind.DecreaseMasterVolume,
                y,
                140f,
                46f,
                45f);
            RegisterAppSettingsText(AppSettingsTextSlot.DecreaseAction, decreaseLabel);
            RegisterAppSettingsText(
                AppSettingsTextSlot.MasterVolumeValue,
                CreateSettingsText(
                    "MasterVolumeValue",
                    23f,
                    FontStyles.Bold,
                    PrimaryTextColor,
                    y,
                    150f,
                    42f,
                    180f));
            (Button _, TMP_Text increaseLabel) = CreateAppSettingsButton(
                "MasterVolumeIncreaseButton",
                AppSettingsActionKind.IncreaseMasterVolume,
                y,
                140f,
                46f,
                345f);
            RegisterAppSettingsText(AppSettingsTextSlot.IncreaseAction, increaseLabel);
        }

        /// <summary>建立前一项、当前值与后一项组成的分辨率设置行。</summary>
        private void BuildSettingsResolutionRow(float y)
        {
            RegisterAppSettingsText(
                AppSettingsTextSlot.ResolutionLabel,
                CreateSettingsText(
                    "ResolutionLabel",
                    22f,
                    FontStyles.Normal,
                    SecondaryTextColor,
                    y,
                    270f,
                    42f,
                    -245f));
            (Button _, TMP_Text previousLabel) = CreateAppSettingsButton(
                "ResolutionPreviousButton",
                AppSettingsActionKind.PreviousResolution,
                y,
                140f,
                46f,
                25f);
            RegisterAppSettingsText(AppSettingsTextSlot.PreviousAction, previousLabel);
            RegisterAppSettingsText(
                AppSettingsTextSlot.ResolutionValue,
                CreateSettingsText(
                    "ResolutionValue",
                    21f,
                    FontStyles.Bold,
                    PrimaryTextColor,
                    y,
                    180f,
                    42f,
                    190f));
            (Button _, TMP_Text nextLabel) = CreateAppSettingsButton(
                "ResolutionNextButton",
                AppSettingsActionKind.NextResolution,
                y,
                140f,
                46f,
                355f);
            RegisterAppSettingsText(AppSettingsTextSlot.NextAction, nextLabel);
        }

        /// <summary>创建设置页专用定位文本并纳入可访问性样式恢复集合。</summary>
        private TMP_Text CreateSettingsText(
            string objectName,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            float y,
            float width,
            float height,
            float x)
        {
            TMP_Text text = CreateText(
                objectName,
                _settingsPage,
                fontSize,
                fontStyle,
                color,
                y,
                width,
                height);
            SetCenteredRect(
                text.rectTransform,
                new Vector2(x, y),
                new Vector2(width, height));
            TrackAppSettingsText(text);
            return text;
        }

        /// <summary>创建尚未接入 RunEntry 动作流的设置页专用按钮。</summary>
        private (Button button, TMP_Text label) CreateAppSettingsButton(
            string objectName,
            AppSettingsActionKind actionKind,
            float y,
            float width,
            float height,
            float x)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(_settingsPage, worldPositionStays: false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetCenteredRect(rect, new Vector2(x, y), new Vector2(width, height));
            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = CreateText(
                $"{objectName}Label",
                rect,
                22f,
                FontStyles.Bold,
                PrimaryTextColor,
                0f,
                width - 16f,
                height - 8f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 22f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            TrackAppSettingsText(label);
            BindAppSettingsButton(button, new AppSettingsAction(actionKind));
            TrackAppSettingsButton(button);
            _buttons.Add(objectName, button);
            return (button, label);
        }

        /// <summary>登记 ViewModel 文本槽位并拒绝重复所有权。</summary>
        private void RegisterAppSettingsText(AppSettingsTextSlot slot, TMP_Text text)
        {
            if (!_appSettingsTexts.TryAdd(slot, text))
                throw new InvalidOperationException($"App settings text slot '{slot}' is already registered.");
        }

        /// <summary>把设置页文字加入去重后的可访问性样式集合。</summary>
        private void TrackAppSettingsText(TMP_Text text)
        {
            if (text == null || _appSettingsStyledTexts.Contains(text))
                return;

            _appSettingsStyledTexts.Add(text);
            _appSettingsBaseFontSizes.Add(text, text.fontSize);
            _appSettingsBaseFontSizeMins.Add(text, text.fontSizeMin);
            _appSettingsBaseFontSizeMaxes.Add(text, text.fontSizeMax);
            _appSettingsBaseTextColors.Add(text, text.color);
        }

        /// <summary>把设置按钮加入去重后的颜色与过渡恢复集合。</summary>
        private void TrackAppSettingsButton(Button button)
        {
            if (button == null || button.targetGraphic == null || _appSettingsButtons.Contains(button))
                return;

            _appSettingsButtons.Add(button);
            _appSettingsBaseButtonColors.Add(button, button.targetGraphic.color);
            _appSettingsBaseTransitions.Add(button, button.transition);
        }

        /// <summary>从可访问性基线中移除即将销毁的动态子树控件。</summary>
        private void UntrackAccessibilityTargets(Transform root)
        {
            if (root == null)
                return;

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                _appSettingsStyledTexts.Remove(text);
                _appSettingsBaseFontSizes.Remove(text);
                _appSettingsBaseFontSizeMins.Remove(text);
                _appSettingsBaseFontSizeMaxes.Remove(text);
                _appSettingsBaseTextColors.Remove(text);
            }

            foreach (Button button in root.GetComponentsInChildren<Button>(includeInactive: true))
            {
                _appSettingsButtons.Remove(button);
                _appSettingsBaseButtonColors.Remove(button);
                _appSettingsBaseTransitions.Remove(button);
            }
        }

        /// <summary>扫描整个 RunEntry 子树，只为首次出现的动态文字和按钮冻结未缩放基线。</summary>
        private void CaptureAccessibilityBaselines()
        {
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(includeInactive: true))
                TrackAppSettingsText(text);
            foreach (Button button in GetComponentsInChildren<Button>(includeInactive: true))
                TrackAppSettingsButton(button);
        }

        /// <summary>发现后续 Render 新建控件，并把最近设置幂等应用到完整 RunEntry 子树。</summary>
        private void RefreshAccessibilityTargets()
        {
            CaptureAccessibilityBaselines();
            if (_lastAppSettings != null)
                ApplyAppSettingsAccessibility(_lastAppSettings);
        }

        /// <summary>从冻结基线应用文字缩放、高对比和减少动态，重复渲染不会累积。</summary>
        private void ApplyAppSettingsAccessibility(AppSettingsSnapshot settings)
        {
            float textScale = (int)settings.TextScale / 100f;
            foreach (TMP_Text text in _appSettingsStyledTexts)
            {
                text.fontSize = _appSettingsBaseFontSizes[text] * textScale;
                if (text.enableAutoSizing)
                {
                    text.fontSizeMin = _appSettingsBaseFontSizeMins[text] * textScale;
                    text.fontSizeMax = _appSettingsBaseFontSizeMaxes[text] * textScale;
                }
                text.color = settings.HighContrast
                    ? SettingsHighContrastTextColor
                    : _appSettingsBaseTextColors[text];
            }

            _settingsAccessibilityBackdrop.color = settings.HighContrast
                ? SettingsHighContrastBackdropColor
                : Color.clear;
            foreach (Button button in _appSettingsButtons)
            {
                button.targetGraphic.color = settings.HighContrast
                    ? SettingsHighContrastButtonColor
                    : _appSettingsBaseButtonColors[button];
                button.transition = settings.ReducedMotion
                    ? Selectable.Transition.None
                    : _appSettingsBaseTransitions[button];
            }
        }

        /// <summary>只在设置按钮可交互且可见时发布独立 AppSettings 动作。</summary>
        private void BindAppSettingsButton(Button button, AppSettingsAction action)
        {
            button.onClick.AddListener(() =>
            {
                if (button == null || !button.IsActive() || !button.IsInteractable())
                    return;

                _appSettingsActionRequested?.Invoke(action);
            });
            _boundButtons.Add(button);
        }

        /// <summary>建立图鉴开发中占位页，并保存文本引用。</summary>
        private void BuildComingSoonPage(RectTransform parent, RunEntryPage pageKind)
        {
            if (pageKind != RunEntryPage.Compendium)
                throw new ArgumentOutOfRangeException(nameof(pageKind));

            RectTransform page = CreatePage(pageKind, parent);
            string prefix = pageKind.ToString();
            TMP_Text title = CreateText(
                $"{prefix}Title",
                page,
                42f,
                FontStyles.Bold,
                PrimaryTextColor,
                245f,
                700f,
                64f);
            TMP_Text placeholder = CreateText(
                $"{prefix}Placeholder",
                page,
                32f,
                FontStyles.Normal,
                SecondaryTextColor,
                35f,
                700f,
                100f);
            TMP_Text back = CreateButton(
                $"{prefix}BackButton",
                page,
                new RunEntryAction(RunEntryActionKind.Back),
                -235f,
                width: 260f).label;

            _compendiumTitle = title;
            _compendiumPlaceholder = placeholder;
            _compendiumBackText = back;
        }

        /// <summary>建立全局统计、稳定 Hero 行、状态文案与返回按钮组成的真实 Statistics 页面。</summary>
        private void BuildStatisticsPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Statistics, parent);
            _statisticsTitle = CreateStatisticsText(
                "StatisticsTitle",
                page,
                42f,
                FontStyles.Bold,
                PrimaryTextColor,
                292f,
                760f,
                58f,
                0f);
            _statisticsTotalRuns = CreateStatisticsText(
                "StatisticsTotalRuns",
                page,
                27f,
                FontStyles.Bold,
                PrimaryTextColor,
                205f,
                350f,
                46f,
                0f);
            _statisticsVictory = CreateStatisticsText(
                "StatisticsVictory",
                page,
                23f,
                FontStyles.Normal,
                SecondaryTextColor,
                150f,
                240f,
                42f,
                -250f);
            _statisticsDefeat = CreateStatisticsText(
                "StatisticsDefeat",
                page,
                23f,
                FontStyles.Normal,
                SecondaryTextColor,
                150f,
                240f,
                42f,
                0f);
            _statisticsAbandoned = CreateStatisticsText(
                "StatisticsAbandoned",
                page,
                23f,
                FontStyles.Normal,
                SecondaryTextColor,
                150f,
                240f,
                42f,
                250f);
            _statisticsVictoryRate = CreateStatisticsText(
                "StatisticsVictoryRate",
                page,
                25f,
                FontStyles.Bold,
                PrimaryTextColor,
                95f,
                360f,
                42f,
                0f);
            _statisticsHeroRowsRoot = CreateContainer(
                "StatisticsHeroRows",
                page,
                stretch: false,
                size: new Vector2(820f, 210f));
            SetCenteredRect(
                _statisticsHeroRowsRoot,
                new Vector2(0f, -38f),
                new Vector2(820f, 210f));
            _statisticsState = CreateStatisticsText(
                "StatisticsState",
                page,
                24f,
                FontStyles.Normal,
                SecondaryTextColor,
                -55f,
                760f,
                100f,
                0f);
            _statisticsBackText = CreateButton(
                "StatisticsBackButton",
                page,
                new RunEntryAction(RunEntryActionKind.Back),
                -292f,
                width: 260f,
                height: 54f).label;
        }

        /// <summary>创建 Statistics 页面专用定位文本。</summary>
        private TMP_Text CreateStatisticsText(
            string objectName,
            RectTransform parent,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            float y,
            float width,
            float height,
            float x)
        {
            TMP_Text text = CreateText(
                objectName,
                parent,
                fontSize,
                fontStyle,
                color,
                y,
                width,
                height);
            SetCenteredRect(
                text.rectTransform,
                new Vector2(x, y),
                new Vector2(width, height));
            return text;
        }

        /// <summary>建立地图标题、生命和供整张冻结 DAG 使用的绘制区域。</summary>
        private void BuildMapPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Map, parent);
            _mapTitle = CreateText("MapTitle", page, 42f, FontStyles.Bold, PrimaryTextColor, 260f, 700f, 64f);
            _mapHealth = CreateText("MapHealth", page, 26f, FontStyles.Normal, SecondaryTextColor, 180f, 500f, 50f);
            _mapGraphHost = CreateContainer(
                "MapGraphHost",
                page,
                stretch: false,
                size: new Vector2(820f, 480f));
            SetCenteredRect(_mapGraphHost, new Vector2(0f, -75f), new Vector2(820f, 480f));
            (_mapAbandonButton, _mapAbandonText) = CreateButton(
                "MapAbandonRunButton",
                page,
                new RunEntryAction(RunEntryActionKind.RequestAbandon),
                -380f,
                width: 260f);
        }

        /// <summary>建立固定三候选与单一跳过按钮的普通战斗奖励页。</summary>
        private void BuildCardRewardPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.CardReward, parent);
            _cardRewardTitle = CreateText(
                "CardRewardTitle",
                page,
                42f,
                FontStyles.Bold,
                PrimaryTextColor,
                300f,
                700f,
                64f);
            for (int index = 0; index < RunCardRewardGenerator.CandidateCount; index++)
            {
                int candidateIndex = index;
                float x = (index - 1) * 280f;
                (_cardRewardCandidateButtons[index], _cardRewardCandidateTexts[index]) = CreateButton(
                    $"CardRewardCandidate{index}Button",
                    page,
                    () => _cardRewardCandidateActions[candidateIndex],
                    20f,
                    width: 250f,
                    height: 360f,
                    x: x);
            }

            (_skipCardRewardButton, _skipCardRewardText) = CreateButton(
                "SkipCardRewardButton",
                page,
                () => _skipCardRewardAction,
                -260f,
                width: 260f);
        }

        /// <summary>建立只有治疗与冻结卡牌升级选择的 Rest 页，不创建返回或跳过入口。</summary>
        private void BuildRestPage(RectTransform parent)
        {
            _restPage = CreatePage(RunEntryPage.Rest, parent);
            _restTitle = CreateText(
                "RestTitle",
                _restPage,
                42f,
                FontStyles.Bold,
                PrimaryTextColor,
                300f,
                700f,
                64f);
            (_restHealButton, _restHealText) = CreateButton(
                "RestHealButton",
                _restPage,
                () => _restHealAction,
                205f,
                width: 620f,
                height: 72f);
        }

        /// <summary>建立只有领取与跳过两个动作的 Chest 页，不创建返回入口。</summary>
        private void BuildChestPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Chest, parent);
            _chestTitle = CreateText(
                "ChestTitle",
                page,
                42f,
                FontStyles.Bold,
                PrimaryTextColor,
                300f,
                700f,
                64f);
            _chestPotionText = CreateText(
                "ChestPotionText",
                page,
                28f,
                FontStyles.Normal,
                PrimaryTextColor,
                145f,
                620f,
                150f);
            _chestCapacityText = CreateText(
                "ChestCapacityText",
                page,
                22f,
                FontStyles.Normal,
                SecondaryTextColor,
                25f,
                620f,
                54f);
            (_chestClaimButton, _chestClaimText) = CreateButton(
                "ChestClaimButton",
                page,
                () => _chestClaimAction,
                -90f,
                width: 420f);
            (_chestSkipButton, _chestSkipText) = CreateButton(
                "ChestSkipButton",
                page,
                () => _chestSkipAction,
                -185f,
                width: 300f);
        }

        /// <summary>建立固定三项购买与一个 Leave 的 Shop 页，不创建返回、出售或刷新入口。</summary>
        private void BuildShopPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Shop, parent);
            _shopTitle = CreateText(
                "ShopTitle",
                page,
                42f,
                FontStyles.Bold,
                PrimaryTextColor,
                300f,
                700f,
                64f);
            for (int index = 0; index < _shopStockButtons.Length; index++)
            {
                int stockIndex = index;
                (_shopStockButtons[index], _shopStockTexts[index]) = CreateButton(
                    $"ShopStock{index + 1}Button",
                    page,
                    () => _shopStockActions[stockIndex],
                    165f - index * 92f,
                    width: 620f,
                    height: 72f);
            }

            (_shopLeaveButton, _shopLeaveText) = CreateButton(
                "ShopLeaveButton",
                page,
                () => _shopLeaveAction,
                -185f,
                width: 300f);
        }

        /// <summary>建立只有免费金币与付费治疗两个类型化选择的 Event 页，不创建返回或通用执行入口。</summary>
        private void BuildEventPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Event, parent);
            _eventTitle = CreateText(
                "EventTitle",
                page,
                42f,
                FontStyles.Bold,
                PrimaryTextColor,
                300f,
                700f,
                64f);
            (_eventGainGoldButton, _eventGainGoldText) = CreateButton(
                "EventGainGoldButton",
                page,
                () => _eventGainGoldAction,
                95f,
                width: 620f,
                height: 82f);
            (_eventPaidHealButton, _eventPaidHealText) = CreateButton(
                "EventPaidHealButton",
                page,
                () => _eventPaidHealAction,
                -35f,
                width: 620f,
                height: 82f);
        }

        /// <summary>在所有 Run 页面之上建立一次无射线与无按钮的持有物 TMP 面板。</summary>
        private void BuildHoldingsPanel(RectTransform parent)
        {
            _holdingsPanel = CreateContainer("RunHoldingsPanel", parent, stretch: true);
            _holdingsGoldText = CreateText(
                "RunHoldingsGoldText",
                _holdingsPanel,
                22f,
                FontStyles.Bold,
                PrimaryTextColor,
                300f,
                360f,
                54f);
            _holdingsRelicsText = CreateText(
                "RunHoldingsRelicsText",
                _holdingsPanel,
                17f,
                FontStyles.Normal,
                SecondaryTextColor,
                150f,
                360f,
                210f);
            _holdingsPotionsText = CreateText(
                "RunHoldingsPotionsText",
                _holdingsPanel,
                17f,
                FontStyles.Normal,
                SecondaryTextColor,
                -100f,
                360f,
                230f);
            SetCenteredRect(
                _holdingsGoldText.rectTransform,
                new Vector2(660f, 300f),
                new Vector2(360f, 54f));
            SetCenteredRect(
                _holdingsRelicsText.rectTransform,
                new Vector2(660f, 150f),
                new Vector2(360f, 210f));
            SetCenteredRect(
                _holdingsPotionsText.rectTransform,
                new Vector2(660f, -100f),
                new Vector2(360f, 230f));
            _holdingsGoldText.alignment = TextAlignmentOptions.Left;
            _holdingsRelicsText.alignment = TextAlignmentOptions.TopLeft;
            _holdingsPotionsText.alignment = TextAlignmentOptions.TopLeft;
            _holdingsPanel.gameObject.SetActive(false);
        }

        /// <summary>以最新不可变投影替换持有物文本；尚未创建 Run 时隐藏整个被动面板。</summary>
        private void RenderHoldings(RunHoldingsViewModel model)
        {
            bool visible = model != null;
            _holdingsPanel.gameObject.SetActive(visible);
            if (!visible)
            {
                _holdingsGoldText.text = string.Empty;
                _holdingsRelicsText.text = string.Empty;
                _holdingsPotionsText.text = string.Empty;
                return;
            }

            _holdingsGoldText.text = model.GoldText;
            _holdingsRelicsText.text = BuildHoldingSection(
                model.RelicsTitle,
                model.Relics,
                model.EmptyText);
            _holdingsPotionsText.text = BuildHoldingSection(
                model.PotionsTitle,
                model.Potions,
                model.EmptyText);
        }

        /// <summary>按 ViewModel 顺序将一个只读持有物分区格式化为单个 TMP 文本。</summary>
        private static string BuildHoldingSection(
            string title,
            IReadOnlyList<RunHoldingItemViewModel> items,
            string emptyText)
        {
            if (items.Count == 0)
                return $"{title}\n{emptyText}";

            return $"{title}\n{string.Join("\n", items.Select(item => $"{item.Name} — {item.Description}"))}";
        }

        /// <summary>以最新投影替换三个动态动作与文本，不在重复 Render 时新增监听。</summary>
        private void RenderCardReward(RunCardRewardViewModel model)
        {
            for (int index = 0; index < RunCardRewardGenerator.CandidateCount; index++)
            {
                if (model == null)
                {
                    _cardRewardCandidateActions[index] = null;
                    _cardRewardCandidateTexts[index].text = string.Empty;
                    _cardRewardCandidateButtons[index].interactable = false;
                    continue;
                }

                RunCardRewardCandidateViewModel candidate = model.Candidates[index];
                _cardRewardCandidateActions[index] = new RunEntryAction(
                    RunEntryActionKind.SelectCardReward,
                    cardRewardId: model.RewardId,
                    cardTemplateId: candidate.TemplateId);
                _cardRewardCandidateTexts[index].text =
                    $"{candidate.Name}\n{candidate.Description}\n{candidate.CostText}";
                _cardRewardCandidateButtons[index].interactable = model.ActionsEnabled;
            }

            _skipCardRewardAction = model == null
                ? default
                : new RunEntryAction(
                    RunEntryActionKind.SkipCardReward,
                    cardRewardId: model.RewardId);
            _skipCardRewardButton.interactable = model != null && model.ActionsEnabled;
        }

        /// <summary>以最新 Rest 投影替换治疗与全部冻结实例动作，重复渲染不新增旧监听。</summary>
        private void RenderRest(RunRestViewModel model)
        {
            _restHealAction = model == null
                ? default
                : new RunEntryAction(
                    RunEntryActionKind.HealAtRest,
                    nodeVisitId: model.VisitId);
            _restHealText.text = model?.HealText ?? string.Empty;
            _restHealButton.interactable = model != null && model.HealEnabled;

            int candidateCount = model?.UpgradeCandidates.Count ?? 0;
            for (int index = _restUpgradeButtons.Count; index < candidateCount; index++)
                CreateRestUpgradeButton(index);

            for (int index = 0; index < _restUpgradeButtons.Count; index++)
            {
                bool visible = index < candidateCount;
                Button button = _restUpgradeButtons[index];
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    _restUpgradeActions[index] = null;
                    _restUpgradeTexts[index].text = string.Empty;
                    button.interactable = false;
                    continue;
                }

                RunRestUpgradeCandidateViewModel candidate = model.UpgradeCandidates[index];
                _restUpgradeActions[index] = new RunEntryAction(
                    RunEntryActionKind.UpgradeCardAtRest,
                    nodeVisitId: model.VisitId,
                    cardInstanceId: candidate.CardInstanceId);
                _restUpgradeTexts[index].text = candidate.Text;
                button.interactable = candidate.Enabled;
                LayoutRestUpgradeButton(
                    button,
                    _restUpgradeTexts[index],
                    index,
                    candidateCount);
            }
        }

        /// <summary>以最新 Chest 投影替换两条访问动作与文本，重复渲染不新增旧身份监听。</summary>
        private void RenderChest(RunChestViewModel model)
        {
            _chestClaimAction = model == null
                ? default
                : new RunEntryAction(
                    RunEntryActionKind.ClaimChest,
                    nodeVisitId: model.VisitId);
            _chestSkipAction = model == null
                ? default
                : new RunEntryAction(
                    RunEntryActionKind.SkipChest,
                    nodeVisitId: model.VisitId);
            _chestPotionText.text = model == null
                ? string.Empty
                : $"{model.Potion.Name}\n{model.Potion.Description}";
            _chestCapacityText.text = model != null && model.IsCapacityFull
                ? model.CapacityFullText
                : string.Empty;
            _chestClaimText.text = model?.ClaimText ?? string.Empty;
            _chestSkipText.text = model?.SkipText ?? string.Empty;
            _chestClaimButton.interactable = model != null && model.ClaimEnabled;
            _chestSkipButton.interactable = model != null && model.SkipEnabled;
        }

        /// <summary>以最新 Shop 投影替换固定三项库存与 Leave 动作，重复渲染不叠加旧身份监听。</summary>
        private void RenderShop(RunShopViewModel model)
        {
            for (int index = 0; index < _shopStockButtons.Length; index++)
            {
                RunShopStockEntryViewModel entry = model?.Entries[index];
                _shopStockActions[index] = entry == null
                    ? default
                    : new RunEntryAction(
                        RunEntryActionKind.PurchaseShopStock,
                        nodeVisitId: model.VisitId,
                        shopStockEntryId: entry.EntryId);
                _shopStockTexts[index].text = entry?.Text ?? string.Empty;
                _shopStockButtons[index].interactable = entry != null && entry.PurchaseEnabled;
            }

            _shopLeaveAction = model == null
                ? default
                : new RunEntryAction(
                    RunEntryActionKind.LeaveShop,
                    nodeVisitId: model.VisitId);
            _shopLeaveText.text = model?.LeaveText ?? string.Empty;
            _shopLeaveButton.interactable = model != null && model.LeaveEnabled;
        }

        /// <summary>以最新 Event 投影替换固定双选择动作，重复渲染不叠加旧访问身份监听。</summary>
        private void RenderEvent(RunEventViewModel model)
        {
            _eventGainGoldAction = model == null
                ? default
                : new RunEntryAction(
                    RunEntryActionKind.ChooseEvent,
                    nodeVisitId: model.VisitId,
                    eventChoice: RunEventChoiceKind.GainGold);
            _eventPaidHealAction = model == null
                ? default
                : new RunEntryAction(
                    RunEntryActionKind.ChooseEvent,
                    nodeVisitId: model.VisitId,
                    eventChoice: RunEventChoiceKind.PaidHeal);
            _eventGainGoldText.text = model?.GainGoldText ?? string.Empty;
            _eventPaidHealText.text = model?.PaidHealText ?? string.Empty;
            _eventGainGoldButton.interactable = model != null && model.GainGoldEnabled;
            _eventPaidHealButton.interactable = model != null && model.PaidHealEnabled;
        }

        /// <summary>按候选索引创建一次可复用按钮，并永久绑定到该索引的最新动作槽。</summary>
        private void CreateRestUpgradeButton(int index)
        {
            int candidateIndex = index;
            (Button button, TMP_Text text) = CreateButton(
                $"RestUpgradeCandidate{index}Button",
                _restPage,
                () => _restUpgradeActions[candidateIndex],
                0f,
                width: 620f,
                height: 64f);
            button.gameObject.SetActive(false);
            _restUpgradeButtons.Add(button);
            _restUpgradeTexts.Add(text);
            _restUpgradeActions.Add(null);
        }

        /// <summary>在内容面内以至多两列紧凑排列当前全部冻结升级候选。</summary>
        private static void LayoutRestUpgradeButton(
            Button button,
            TMP_Text text,
            int index,
            int candidateCount)
        {
            int columnCount = candidateCount > 5 ? 2 : 1;
            int rowCount = (candidateCount + columnCount - 1) / columnCount;
            int row = index / columnCount;
            int column = index % columnCount;
            const float gap = 10f;
            float height = Mathf.Min(68f, (400f - (rowCount - 1) * gap) / rowCount);
            float width = columnCount == 1 ? 620f : 300f;
            float x = columnCount == 1 ? 0f : (column == 0 ? -160f : 160f);
            float y = 115f - height * 0.5f - row * (height + gap);
            SetCenteredRect(
                button.GetComponent<RectTransform>(),
                new Vector2(x, y),
                new Vector2(width, height));
            SetCenteredRect(
                text.rectTransform,
                Vector2.zero,
                new Vector2(width - 30f, height - 12f));
        }

        /// <summary>按地图指纹复用拓扑几何，并在每次投影时刷新节点/边的功能状态。</summary>
        private void RenderMap(RunMapViewModel model)
        {
            _renderedMap = model;
            if (model == null)
                return;

            if (_mapGraphRoot == null ||
                !string.Equals(_renderedMapFingerprint, model.Fingerprint, StringComparison.Ordinal))
            {
                BuildMapGraph(model);
            }

            foreach (RunMapNodeViewModel node in model.Nodes)
            {
                if (!_mapNodeButtons.TryGetValue(node.NodeId, out Button button) ||
                    !_mapNodeLabels.TryGetValue(node.NodeId, out TMP_Text label) ||
                    !_mapNodeIdentityIds.TryGetValue(node.NodeId, out TMP_Text identityId) ||
                    !_mapNodeAnchorImages.ContainsKey(node.NodeId))
                {
                    throw new InvalidOperationException($"Map node view '{node.NodeId}' is missing.");
                }

                label.text = node.DisplayName;
                identityId.text = node.ContentId > 0
                    ? $"#{node.ContentId}"
                    : node.NodeId;
                button.interactable = node.State == RunMapNodePresentationState.Selectable ||
                                      node.State == RunMapNodePresentationState.BossGateReached;
            }

            RestoreMapVisuals();
        }

        /// <summary>由 Layer/Slot 建立一次冻结 DAG 的全部边和节点，重建时先停用旧根。</summary>
        private void BuildMapGraph(RunMapViewModel model)
        {
            ClearMapGraph();
            _renderedMapFingerprint = model.Fingerprint;
            _mapGraphRoot = CreateContainer("FrozenActMap", _mapGraphHost, stretch: true);

            IReadOnlyList<RunMapNodeLayout> layouts = RunMapLayout.Build(model.Nodes);
            Dictionary<string, RunMapNodeLayout> layoutByNodeId = layouts.ToDictionary(
                layout => layout.NodeId,
                StringComparer.Ordinal);

            foreach (RunMapEdgeViewModel edge in model.Edges)
            {
                if (!layoutByNodeId.TryGetValue(edge.FromNodeId, out RunMapNodeLayout fromLayout) ||
                    !layoutByNodeId.TryGetValue(edge.ToNodeId, out RunMapNodeLayout toLayout))
                {
                    throw new InvalidOperationException($"Map edge '{edge.Key}' references a missing view node.");
                }

                var from = new Vector2(fromLayout.CenterX, fromLayout.CenterY);
                var to = new Vector2(toLayout.CenterX, toLayout.CenterY);
                CreateMapEdge(edge, from, to);
            }

            foreach (RunMapNodeViewModel node in model.Nodes)
                CreateMapNode(node, layoutByNodeId[node.NodeId]);
        }

        /// <summary>建立一条位于节点之后的纯表现连线。</summary>
        private void CreateMapEdge(RunMapEdgeViewModel edge, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            RectTransform rect = CreatePanel(
                $"MapEdge_{edge.FromNodeId}_{edge.ToNodeId}",
                _mapGraphRoot,
                MapEdgeColor,
                (from + to) * 0.5f,
                new Vector2(delta.magnitude, 6f),
                stretch: false);
            rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            _mapEdgeImages.Add(edge.Key, rect.GetComponent<Image>());
        }

        /// <summary>建立一个按 NodeId 提交命令、仅由投影控制可交互性的地图节点。</summary>
        private void CreateMapNode(RunMapNodeViewModel node, RunMapNodeLayout layout)
        {
            string objectName = GetMapNodeObjectName(node.NodeId);
            float width = layout.Width;
            float height = layout.Height;
            bool compact = height < 60f;
            (Button button, TMP_Text label) = CreateButton(
                objectName,
                _mapGraphRoot,
                new RunEntryAction(
                    RunEntryActionKind.EnterMapNode,
                    mapNodeId: new TinySpire.Run.Map.MapNodeId(node.NodeId)),
                layout.CenterY,
                width,
                height,
                x: layout.CenterX);
            SetCenteredRect(
                (RectTransform)label.transform,
                compact ? new Vector2(18f, 7f) : new Vector2(20f, 12f),
                compact ? new Vector2(width - 58f, 22f) : new Vector2(width - 68f, 42f));
            float labelFontSize = compact
                ? 14f
                : node.Kind == TinySpire.Run.Map.MapNodeKind.Boss ? 18f : 20f;
            ConfigureMapNodeText(
                label,
                compact ? 9f : 13f,
                labelFontSize);

            TMP_Text identityId = CreateText(
                $"MapNode_{node.NodeId}_IdentityId",
                (RectTransform)button.transform,
                compact ? 10f : 14f,
                FontStyles.Normal,
                SecondaryTextColor,
                compact ? -12f : -22f,
                compact ? width - 58f : width - 68f,
                compact ? 14f : 20f);
            SetCenteredRect(
                (RectTransform)identityId.transform,
                compact ? new Vector2(18f, -13f) : new Vector2(20f, -23f),
                compact ? new Vector2(width - 58f, 14f) : new Vector2(width - 68f, 20f));
            ConfigureMapNodeText(
                identityId,
                compact ? 7.5f : 10f,
                compact ? 10f : 14f);
            IReadOnlyList<Image> anchorImages = CreateMapVisualAnchor(
                node,
                (RectTransform)button.transform,
                width);

            _mapNodeButtons.Add(node.NodeId, button);
            _mapNodeLabels.Add(node.NodeId, label);
            _mapNodeIdentityIds.Add(node.NodeId, identityId);
            _mapNodeAnchorImages.Add(node.NodeId, anchorImages);

            var trigger = button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ApplyMapHover(node.NodeId));
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => RestoreMapVisuals());
            trigger.triggers.Add(exit);
        }

        /// <summary>让地图节点文字在固定语义分区内单行自适应，并保留全局文字缩放可扩大的字号区间。</summary>
        private static void ConfigureMapNodeText(
            TMP_Text text,
            float minimumFontSize,
            float preferredFontSize)
        {
            text.fontSize = preferredFontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = minimumFontSize;
            text.fontSizeMax = preferredFontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>按 ViewModel 指定种类建立轻量程序化徽记，不读取或猜测业务内容 ID。</summary>
        private static IReadOnlyList<Image> CreateMapVisualAnchor(
            RunMapNodeViewModel node,
            RectTransform parent,
            float buttonWidth)
        {
            RectTransform anchorRoot = CreateContainer(
                $"MapNode_{node.NodeId}_Anchor_{node.VisualAnchorKind}",
                parent,
                stretch: false,
                size: new Vector2(40f, 40f));
            SetCenteredRect(
                anchorRoot,
                new Vector2((-buttonWidth * 0.5f) + 25f, 0f),
                new Vector2(40f, 40f));

            Color color = ResolveMapVisualAnchorColor(node.VisualAnchorKind);
            var images = new List<Image>();
            switch (node.VisualAnchorKind)
            {
                case RunMapVisualAnchorKind.StartFlag:
                    AddMapAnchorShape(images, anchorRoot, "FlagPole", new Vector2(-7f, -1f), new Vector2(4f, 30f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "FlagPennant", new Vector2(3f, 8f), new Vector2(18f, 11f), -12f, color);
                    break;
                case RunMapVisualAnchorKind.EncounterSlimeSilhouette:
                    AddMapAnchorShape(images, anchorRoot, "SlimeBody", new Vector2(0f, -5f), new Vector2(30f, 18f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "SlimeCrest", new Vector2(0f, 5f), new Vector2(18f, 18f), 45f, color);
                    break;
                case RunMapVisualAnchorKind.EncounterSentrySilhouette:
                    AddMapAnchorShape(images, anchorRoot, "SentryBody", new Vector2(0f, -5f), new Vector2(23f, 21f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "SentryHead", new Vector2(0f, 8f), new Vector2(16f, 10f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "SentryAntenna", new Vector2(0f, 16f), new Vector2(3f, 10f), 0f, color);
                    break;
                case RunMapVisualAnchorKind.RestCampfire:
                    AddMapAnchorShape(images, anchorRoot, "FireLogLeft", new Vector2(-6f, -10f), new Vector2(5f, 24f), 55f, color);
                    AddMapAnchorShape(images, anchorRoot, "FireLogRight", new Vector2(6f, -10f), new Vector2(5f, 24f), -55f, color);
                    AddMapAnchorShape(images, anchorRoot, "FireFlame", new Vector2(0f, 5f), new Vector2(17f, 24f), 45f, color);
                    break;
                case RunMapVisualAnchorKind.ChestCache:
                    AddMapAnchorShape(images, anchorRoot, "ChestBody", new Vector2(0f, -6f), new Vector2(30f, 18f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "ChestLid", new Vector2(0f, 7f), new Vector2(30f, 8f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "ChestLatch", new Vector2(0f, -1f), new Vector2(6f, 12f), 0f, color);
                    break;
                case RunMapVisualAnchorKind.ShopBag:
                    AddMapAnchorShape(images, anchorRoot, "BagBody", new Vector2(0f, -5f), new Vector2(27f, 25f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "BagTie", new Vector2(0f, 10f), new Vector2(16f, 5f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "BagCoin", new Vector2(0f, -4f), new Vector2(7f, 7f), 45f, color);
                    break;
                case RunMapVisualAnchorKind.EventQuestionMark:
                    AddMapAnchorShape(images, anchorRoot, "QuestionTop", new Vector2(2f, 9f), new Vector2(18f, 5f), -15f, color);
                    AddMapAnchorShape(images, anchorRoot, "QuestionStem", new Vector2(0f, 0f), new Vector2(5f, 18f), 12f, color);
                    AddMapAnchorShape(images, anchorRoot, "QuestionDot", new Vector2(0f, -14f), new Vector2(6f, 6f), 0f, color);
                    break;
                case RunMapVisualAnchorKind.BossAlphaCrown:
                    AddMapAnchorShape(images, anchorRoot, "CrownBase", new Vector2(0f, -10f), new Vector2(30f, 6f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "CrownLeft", new Vector2(-10f, 1f), new Vector2(7f, 21f), -18f, color);
                    AddMapAnchorShape(images, anchorRoot, "CrownCenter", new Vector2(0f, 3f), new Vector2(7f, 25f), 0f, color);
                    AddMapAnchorShape(images, anchorRoot, "CrownRight", new Vector2(10f, 1f), new Vector2(7f, 21f), 18f, color);
                    break;
                case RunMapVisualAnchorKind.BossBetaHorns:
                    AddMapAnchorShape(images, anchorRoot, "HornCore", new Vector2(0f, -2f), new Vector2(14f, 14f), 45f, color);
                    AddMapAnchorShape(images, anchorRoot, "HornLeft", new Vector2(-11f, 6f), new Vector2(6f, 24f), -35f, color);
                    AddMapAnchorShape(images, anchorRoot, "HornRight", new Vector2(11f, 6f), new Vector2(6f, 24f), 35f, color);
                    break;
                case RunMapVisualAnchorKind.BossGammaEye:
                    AddMapAnchorShape(images, anchorRoot, "EyeUpper", new Vector2(0f, 6f), new Vector2(29f, 5f), -12f, color);
                    AddMapAnchorShape(images, anchorRoot, "EyeLower", new Vector2(0f, -6f), new Vector2(29f, 5f), 12f, color);
                    AddMapAnchorShape(images, anchorRoot, "EyePupil", Vector2.zero, new Vector2(6f, 18f), 0f, color);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(node.VisualAnchorKind));
            }

            return images.AsReadOnly();
        }

        /// <summary>向程序化徽记加入一个不参与射线检测的矩形轮廓片段。</summary>
        private static void AddMapAnchorShape(
            ICollection<Image> images,
            RectTransform parent,
            string shapeName,
            Vector2 position,
            Vector2 size,
            float rotationDegrees,
            Color color)
        {
            RectTransform shape = CreatePanel(
                shapeName,
                parent,
                color,
                position,
                size,
                stretch: false);
            shape.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            Image image = shape.GetComponent<Image>();
            image.raycastTarget = false;
            images.Add(image);
        }

        /// <summary>为每类开局明牌身份提供稳定基础色，轮廓仍是主要区分信息。</summary>
        private static Color ResolveMapVisualAnchorColor(RunMapVisualAnchorKind kind)
        {
            switch (kind)
            {
                case RunMapVisualAnchorKind.StartFlag:
                    return new Color32(133, 202, 229, 255);
                case RunMapVisualAnchorKind.EncounterSlimeSilhouette:
                    return new Color32(244, 195, 105, 255);
                case RunMapVisualAnchorKind.EncounterSentrySilhouette:
                    return new Color32(135, 193, 224, 255);
                case RunMapVisualAnchorKind.RestCampfire:
                    return new Color32(239, 144, 75, 255);
                case RunMapVisualAnchorKind.ChestCache:
                    return new Color32(224, 184, 84, 255);
                case RunMapVisualAnchorKind.ShopBag:
                    return new Color32(111, 207, 159, 255);
                case RunMapVisualAnchorKind.EventQuestionMark:
                    return new Color32(189, 142, 230, 255);
                case RunMapVisualAnchorKind.BossAlphaCrown:
                    return new Color32(243, 115, 128, 255);
                case RunMapVisualAnchorKind.BossBetaHorns:
                    return new Color32(185, 135, 230, 255);
                case RunMapVisualAnchorKind.BossGammaEye:
                    return new Color32(105, 220, 203, 255);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>悬停当前可选节点时高亮完整后半程，并弱化会被放弃的路线与 Boss。</summary>
        private void ApplyMapHover(string candidateNodeId)
        {
            RunMapNodeViewModel candidate = _renderedMap?.Nodes.FirstOrDefault(node =>
                string.Equals(node.NodeId, candidateNodeId, StringComparison.Ordinal));
            if (candidate == null || candidate.State != RunMapNodePresentationState.Selectable)
                return;

            var routeNodeIds = new HashSet<string>(
                candidate.DownstreamNodeIds,
                StringComparer.Ordinal);
            var routeEdgeKeys = new HashSet<string>(
                candidate.DownstreamEdgeKeys,
                StringComparer.Ordinal);
            foreach (RunMapNodeViewModel node in _renderedMap.Nodes)
            {
                bool completedPrefix = node.State == RunMapNodePresentationState.Completed ||
                                       node.State == RunMapNodePresentationState.Current;
                Color color = routeNodeIds.Contains(node.NodeId)
                    ? MapRouteColor
                    : completedPrefix
                        ? ResolveMapNodeColor(node)
                        : MapDimmedColor;
                SetButtonColor(_mapNodeButtons[node.NodeId], color);
                bool emphasized = routeNodeIds.Contains(node.NodeId) || completedPrefix;
                Color textColor = emphasized
                    ? PrimaryTextColor
                    : new Color32(115, 124, 139, 150);
                SetTextColor(_mapNodeLabels[node.NodeId], textColor);
                SetTextColor(_mapNodeIdentityIds[node.NodeId], textColor);
                SetMapAnchorColor(
                    node.NodeId,
                    emphasized
                        ? ResolveMapVisualAnchorColor(node.VisualAnchorKind)
                        : new Color32(115, 124, 139, 120));
            }

            foreach (RunMapEdgeViewModel edge in _renderedMap.Edges)
            {
                _mapEdgeImages[edge.Key].color = routeEdgeKeys.Contains(edge.Key)
                    ? MapRouteColor
                    : edge.IsCompletedPath
                        ? MapCompletedEdgeColor
                        : MapDimmedColor;
            }
        }

        /// <summary>移除悬停派生表现，恢复当前不可变投影的基础颜色。</summary>
        private void RestoreMapVisuals()
        {
            if (_renderedMap == null)
                return;

            foreach (RunMapNodeViewModel node in _renderedMap.Nodes)
            {
                if (!_mapNodeButtons.TryGetValue(node.NodeId, out Button button) ||
                    !_mapNodeLabels.TryGetValue(node.NodeId, out TMP_Text label) ||
                    !_mapNodeIdentityIds.TryGetValue(node.NodeId, out TMP_Text identityId))
                {
                    continue;
                }

                SetButtonColor(button, ResolveMapNodeColor(node));
                Color textColor = node.State == RunMapNodePresentationState.Locked
                    ? SecondaryTextColor
                    : PrimaryTextColor;
                SetTextColor(label, textColor);
                SetTextColor(identityId, textColor);
                SetMapAnchorColor(
                    node.NodeId,
                    ResolveMapVisualAnchorColor(node.VisualAnchorKind));
            }

            foreach (RunMapEdgeViewModel edge in _renderedMap.Edges)
            {
                if (_mapEdgeImages.TryGetValue(edge.Key, out Image image))
                {
                    image.color = edge.IsCompletedPath
                        ? MapCompletedEdgeColor
                        : MapEdgeColor;
                }
            }
        }

        /// <summary>按节点种类与进度状态选择基础色；Boss 身份在锁定时仍保持可辨认。</summary>
        private static Color ResolveMapNodeColor(RunMapNodeViewModel node)
        {
            switch (node.State)
            {
                case RunMapNodePresentationState.Selectable:
                    return node.Kind == TinySpire.Run.Map.MapNodeKind.Boss
                        ? MapBossColor
                        : MapSelectableColor;
                case RunMapNodePresentationState.Current:
                    return MapCurrentColor;
                case RunMapNodePresentationState.Completed:
                    return MapCompletedColor;
                case RunMapNodePresentationState.BossGateReached:
                    return MapBossColor;
                case RunMapNodePresentationState.Locked:
                    return node.Kind == TinySpire.Run.Map.MapNodeKind.Boss
                        ? new Color32(91, 52, 62, 220)
                        : DisabledButtonColor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(node.State));
            }
        }

        /// <summary>为一个节点的全部程序化轮廓片段同步设置表现色。</summary>
        private void SetMapAnchorColor(string nodeId, Color color)
        {
            if (!_mapNodeAnchorImages.TryGetValue(nodeId, out IReadOnlyList<Image> images))
                return;

            foreach (Image image in images)
            {
                if (image != null)
                    image.color = color;
            }
        }

        /// <summary>清理旧地图几何、可访问性基线与测试按钮索引，不影响其他入口页面。</summary>
        private void ClearMapGraph()
        {
            foreach (KeyValuePair<string, Button> entry in _mapNodeButtons)
            {
                _buttons.Remove(GetMapNodeObjectName(entry.Key));
                _boundButtons.Remove(entry.Value);
                if (entry.Value != null)
                    entry.Value.onClick.RemoveAllListeners();
            }
            _mapNodeButtons.Clear();
            _mapNodeLabels.Clear();
            _mapNodeIdentityIds.Clear();
            _mapNodeAnchorImages.Clear();
            _mapEdgeImages.Clear();

            if (_mapGraphRoot == null)
                return;

            UntrackAccessibilityTargets(_mapGraphRoot);
            _mapGraphRoot.SetParent(null, worldPositionStays: false);
            _mapGraphRoot.gameObject.SetActive(false);
            DestroyOwnedObject(_mapGraphRoot.gameObject);
            _mapGraphRoot = null;
        }

        /// <summary>把稳定 NodeId 转为可由 EditMode 测试读取的稳定按钮对象名。</summary>
        private static string GetMapNodeObjectName(string nodeId)
        {
            return $"MapNode_{nodeId}_Button";
        }

        /// <summary>建立失败说明、零生命投影与确认离开终局的唯一动作。</summary>
        private void BuildFailurePage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Failure, parent);
            _failureTitle = CreateText("FailureTitle", page, 46f, FontStyles.Bold, PrimaryTextColor, 185f, 700f, 70f);
            _failureHealth = CreateText("FailureHealth", page, 26f, FontStyles.Normal, SecondaryTextColor, 95f, 700f, 100f);
            _leaveTerminalRunText = CreateButton(
                "LeaveTerminalRunButton",
                page,
                new RunEntryAction(RunEntryActionKind.LeaveTerminalRun),
                -35f).label;
        }

        /// <summary>建立删除现有可用或不可用单槽前的明确确认页。</summary>
        private void BuildAbandonConfirmationPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.AbandonConfirmation, parent);
            _confirmationTitle = CreateText(
                "ConfirmationTitle",
                page,
                44f,
                FontStyles.Bold,
                PrimaryTextColor,
                205f,
                760f,
                70f);
            _confirmationMessage = CreateText(
                "ConfirmationMessage",
                page,
                26f,
                FontStyles.Normal,
                SecondaryTextColor,
                75f,
                780f,
                130f);
            _confirmationConfirmText = CreateButton(
                "ConfirmAbandonButton",
                page,
                new RunEntryAction(RunEntryActionKind.ConfirmAbandon),
                -80f).label;
            _confirmationCancelText = CreateButton(
                "AbandonCancelButton",
                page,
                new RunEntryAction(RunEntryActionKind.Back),
                -175f,
                width: 300f).label;
        }

        /// <summary>建立 checkpoint commit 失败后的重试与请求退出页。</summary>
        private void BuildSaveFailurePage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.SaveFailure, parent);
            _saveFailureMessage = CreateText(
                "SaveFailureMessage",
                page,
                46f,
                FontStyles.Bold,
                PrimaryTextColor,
                205f,
                760f,
                74f);
            _saveFailureHealth = CreateText(
                "SaveFailureHealth",
                page,
                26f,
                FontStyles.Normal,
                SecondaryTextColor,
                105f,
                520f,
                50f);
            _retrySaveText = CreateButton(
                "RetrySaveButton",
                page,
                new RunEntryAction(RunEntryActionKind.RetrySave),
                -35f).label;
            (_saveFailureExitButton, _saveFailureExitText) = CreateButton(
                "SaveFailureExitButton",
                page,
                new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure),
                -130f,
                width: 300f);
        }

        /// <summary>建立退出未保存 Run 前的回退检查点警告确认页。</summary>
        private void BuildRollbackConfirmationPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.RollbackConfirmation, parent);
            _rollbackTitle = CreateText(
                "RollbackTitle",
                page,
                44f,
                FontStyles.Bold,
                PrimaryTextColor,
                220f,
                760f,
                70f);
            _rollbackMessage = CreateText(
                "RollbackMessage",
                page,
                25f,
                FontStyles.Normal,
                SecondaryTextColor,
                70f,
                800f,
                190f);
            _rollbackConfirmText = CreateButton(
                "ConfirmRollbackButton",
                page,
                new RunEntryAction(RunEntryActionKind.ConfirmRollback),
                -105f).label;
            _rollbackCancelText = CreateButton(
                "RollbackCancelButton",
                page,
                new RunEntryAction(RunEntryActionKind.Back),
                -200f,
                width: 300f).label;
        }

        /// <summary>创建一个铺满内容面的互斥页面。</summary>
        private RectTransform CreatePage(RunEntryPage page, RectTransform parent)
        {
            var pageObject = new GameObject($"{page}Page", typeof(RectTransform));
            pageObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = pageObject.GetComponent<RectTransform>();
            Stretch(rect);
            _pages.Add(page, pageObject);
            return rect;
        }

        /// <summary>创建带 Image 的几何矩形面板。</summary>
        private static RectTransform CreatePanel(
            string objectName,
            RectTransform parent,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size,
            bool stretch)
        {
            var panelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panelObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            if (stretch)
                Stretch(rect);
            else
                SetCenteredRect(rect, anchoredPosition, size);
            Image image = panelObject.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        /// <summary>创建不带 Graphic 的布局节点，可选全拉伸或中心固定尺寸。</summary>
        private static RectTransform CreateContainer(
            string objectName,
            RectTransform parent,
            bool stretch,
            Vector2 size = default)
        {
            var containerObject = new GameObject(objectName, typeof(RectTransform));
            containerObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = containerObject.GetComponent<RectTransform>();
            if (stretch)
                Stretch(rect);
            else
                SetCenteredRect(rect, Vector2.zero, size);
            return rect;
        }

        /// <summary>创建一个只展示禁用占位文本的几何面板。</summary>
        private (Image image, TMP_Text label) CreatePassivePanel(
            string objectName,
            RectTransform parent,
            float y,
            float width,
            float height)
        {
            RectTransform rect = CreatePanel(
                objectName,
                parent,
                DisabledButtonColor,
                new Vector2(0f, y),
                new Vector2(width, height),
                stretch: false);
            TMP_Text label = CreateText(
                $"{objectName}Label",
                rect,
                24f,
                FontStyles.Normal,
                SecondaryTextColor,
                0f,
                width - 24f,
                height - 12f);
            return (rect.GetComponent<Image>(), label);
        }

        /// <summary>创建按钮、TMP 标签并只绑定一次动作发布。</summary>
        private (Button button, TMP_Text label) CreateButton(
            string objectName,
            RectTransform parent,
            RunEntryAction action,
            float y,
            float width = 420f,
            float height = 66f,
            float x = 0f)
        {
            return CreateButton(
                objectName,
                parent,
                () => action,
                y,
                width,
                height,
                x);
        }

        /// <summary>创建从最新投影延迟读取动作的按钮，供跨 Render 身份变化的页面复用。</summary>
        private (Button button, TMP_Text label) CreateButton(
            string objectName,
            RectTransform parent,
            Func<RunEntryAction?> actionProvider,
            float y,
            float width = 420f,
            float height = 66f,
            float x = 0f)
        {
            if (actionProvider == null)
                throw new ArgumentNullException(nameof(actionProvider));

            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetCenteredRect(rect, new Vector2(x, y), new Vector2(width, height));
            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(226, 238, 250, 255);
            colors.pressedColor = new Color32(180, 205, 230, 255);
            colors.disabledColor = new Color32(118, 126, 138, 180);
            button.colors = colors;

            TMP_Text label = CreateText(
                $"{objectName}Label",
                rect,
                height >= 100f ? 30f : 25f,
                FontStyles.Bold,
                PrimaryTextColor,
                0f,
                width - 30f,
                height - 12f);
            BindButton(button, actionProvider);
            _buttons.Add(objectName, button);
            return (button, label);
        }

        /// <summary>创建主菜单专用透明纸面八边形，并继续复用原有动作发布与 TMP 标签。</summary>
        private (Button button, TMP_Text label) CreateVisualMainMenuButton(
            string objectName,
            RectTransform parent,
            RunEntryAction action,
            float y)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(EntryOctagonGraphic),
                typeof(Button));
            buttonObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetCenteredRect(
                rect,
                new Vector2(0f, y),
                new Vector2(
                    EntryPaperStackView.MainMenuButtonWidth,
                    EntryPaperStackView.MainMenuButtonHeight));

            EntryOctagonGraphic graphic = buttonObject.GetComponent<EntryOctagonGraphic>();
            _entryPaperStack.ConfigureMainMenuButton(graphic);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(248, 244, 235, 255);
            colors.pressedColor = new Color32(205, 196, 181, 255);
            colors.disabledColor = new Color32(132, 126, 116, 170);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text label = CreateText(
                $"{objectName}Label",
                rect,
                25f,
                FontStyles.Bold,
                _entryPaperStack.MenuPrimaryTextColor,
                0f,
                EntryPaperStackView.MainMenuButtonWidth - 42f,
                72f);
            BindButton(button, action);
            _buttons.Add(objectName, button);
            return (button, label);
        }

        /// <summary>创建居中的 TMP 文本矩形并应用场景级动态字体。</summary>
        private TMP_Text CreateText(
            string objectName,
            RectTransform parent,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            float y,
            float width,
            float height)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            SetCenteredRect(rect, new Vector2(0f, y), new Vector2(width, height));

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = _fontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        /// <summary>只在按钮实际可交互且可见时发布对应入口动作。</summary>
        private void BindButton(Button button, RunEntryAction action)
        {
            BindButton(button, () => action);
        }

        /// <summary>只绑定一次动态动作读取器，使重复投影不会叠加旧奖励监听。</summary>
        private void BindButton(Button button, Func<RunEntryAction?> actionProvider)
        {
            button.onClick.AddListener(() =>
            {
                if (button == null || !button.IsActive() || !button.IsInteractable())
                    return;

                RunEntryAction? action = actionProvider();
                if (action.HasValue)
                    ActionRequested?.Invoke(action.Value);
            });
            _boundButtons.Add(button);
        }

        /// <summary>写入按钮最新语义底色，高对比启用时仅以可逆显示色覆盖。</summary>
        private void SetButtonColor(Button button, Color color)
        {
            if (button?.targetGraphic == null)
                return;

            TrackAppSettingsButton(button);
            _appSettingsBaseButtonColors[button] = color;
            button.targetGraphic.color = _lastAppSettings?.HighContrast == true
                ? SettingsHighContrastButtonColor
                : color;
        }

        /// <summary>写入文字最新语义底色，高对比启用时保留底色并显示统一强调色。</summary>
        private void SetTextColor(TMP_Text text, Color color)
        {
            if (text == null)
                return;

            TrackAppSettingsText(text);
            _appSettingsBaseTextColors[text] = color;
            text.color = _lastAppSettings?.HighContrast == true
                ? SettingsHighContrastTextColor
                : color;
        }

        /// <summary>将 RectTransform 设为父级全拉伸。</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>将 RectTransform 设为中心锚点与固定尺寸。</summary>
        private static void SetCenteredRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        /// <summary>优先从本机许可字体建立 TMP 动态字库，缺失时回退项目默认字库并记录风险。</summary>
        private TMP_FontAsset CreateRuntimeFontAsset()
        {
            foreach (string familyName in CjkFontCandidates)
            {
                TMP_FontAsset dynamicAsset = TMP_FontAsset.CreateFontAsset(
                    familyName,
                    "Regular",
                    32);
                if (dynamicAsset == null)
                    continue;

                dynamicAsset.name = $"RunEntry Runtime Font ({familyName})";
                dynamicAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                dynamicAsset.isMultiAtlasTexturesEnabled = true;
                if (!dynamicAsset.TryAddCharacters(
                        RequiredEntryGlyphs,
                        out _,
                        includeFontFeatures: false))
                {
                    DestroyOwnedObject(dynamicAsset);
                    continue;
                }

                _ownsFontAsset = true;
                return dynamicAsset;
            }

            TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
            if (fallback == null)
                throw new InvalidOperationException("TMP default font asset is not configured.");

            Debug.LogError(
                "RunEntry could not create a CJK-capable operating-system TMP font. " +
                "Chinese glyphs may be unavailable on this machine.");
            return fallback;
        }

        /// <summary>按 EditMode/PlayMode 选择安全的 Unity Object 销毁 API。</summary>
        private static void DestroyOwnedObject(UnityEngine.Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
