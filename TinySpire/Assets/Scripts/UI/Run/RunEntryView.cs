using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TinySpire.UI.Run
{
    /// <summary>以可替换几何控件渲染 G1-A 全部入口页面，不持有任何 Run 业务事实。</summary>
    [DisallowMultipleComponent]
    public sealed class RunEntryView : MonoBehaviour, IRunEntryView
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
            "开始游戏继续设置图鉴统计返回取消开发中布局占位选择角色确认并未来队伍槽临时地图首战节点已清除后续内容未接入生命战斗失败重开本关战士机枪兵放弃当前存档前会删除不可用有效无法版本未知迁移检测写入读取引用配置缺失保存重试退出不回退上一份成功检查点若尚无将恢复永久撤销？。“”，；";

        private static readonly Color32 BackgroundColor = new Color32(18, 24, 36, 255);
        private static readonly Color32 SurfaceColor = new Color32(28, 38, 55, 248);
        private static readonly Color32 ButtonColor = new Color32(47, 62, 86, 255);
        private static readonly Color32 SelectedButtonColor = new Color32(75, 145, 205, 255);
        private static readonly Color32 DisabledButtonColor = new Color32(55, 61, 72, 255);
        private static readonly Color32 PrimaryTextColor = new Color32(235, 242, 250, 255);
        private static readonly Color32 SecondaryTextColor = new Color32(166, 181, 202, 255);

        private readonly Dictionary<RunEntryPage, GameObject> _pages =
            new Dictionary<RunEntryPage, GameObject>();
        private readonly Dictionary<string, Button> _buttons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly List<Button> _boundButtons = new List<Button>();

        private TMP_FontAsset _fontAsset;
        private bool _ownsFontAsset;
        private bool _built;

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

        private TMP_Text _settingsTitle;
        private TMP_Text _settingsPlaceholder;
        private TMP_Text _settingsBackText;

        private TMP_Text _compendiumTitle;
        private TMP_Text _compendiumPlaceholder;
        private TMP_Text _compendiumBackText;

        private TMP_Text _statisticsTitle;
        private TMP_Text _statisticsPlaceholder;
        private TMP_Text _statisticsBackText;

        private TMP_Text _mapTitle;
        private TMP_Text _mapHealth;
        private TMP_Text _battleNodeText;
        private Button _battleNodeButton;

        private TMP_Text _failureTitle;
        private TMP_Text _failureHealth;
        private TMP_Text _restartBattleText;

        private TMP_Text _confirmationTitle;
        private TMP_Text _confirmationMessage;
        private TMP_Text _confirmationConfirmText;
        private TMP_Text _confirmationCancelText;

        private TMP_Text _saveFailureMessage;
        private TMP_Text _saveFailureHealth;
        private TMP_Text _retrySaveText;
        private TMP_Text _saveFailureExitText;

        private TMP_Text _rollbackTitle;
        private TMP_Text _rollbackMessage;
        private TMP_Text _rollbackConfirmText;
        private TMP_Text _rollbackCancelText;

        /// <summary>所有按钮被归一化后发布的唯一 UI 动作流。</summary>
        public event Action<RunEntryAction> ActionRequested;

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
            foreach (KeyValuePair<RunEntryPage, GameObject> page in _pages)
                page.Value.SetActive(page.Key == model.Page);

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

            _settingsTitle.text = model.GetText(RunEntryTextSlot.SettingsTitle);
            _settingsPlaceholder.text = model.GetText(RunEntryTextSlot.SettingsPlaceholder);
            _settingsBackText.text = model.GetText(RunEntryTextSlot.Back);

            _compendiumTitle.text = model.GetText(RunEntryTextSlot.Compendium);
            _compendiumPlaceholder.text = model.GetText(RunEntryTextSlot.ComingSoon);
            _compendiumBackText.text = model.GetText(RunEntryTextSlot.Back);

            _statisticsTitle.text = model.GetText(RunEntryTextSlot.Statistics);
            _statisticsPlaceholder.text = model.GetText(RunEntryTextSlot.ComingSoon);
            _statisticsBackText.text = model.GetText(RunEntryTextSlot.Back);

            _mapTitle.text = model.GetText(RunEntryTextSlot.MapTitle);
            _mapHealth.text = model.GetText(RunEntryTextSlot.Health);
            _battleNodeText.text = model.GetText(
                model.BattleNodeCompleted
                    ? RunEntryTextSlot.Cleared
                    : RunEntryTextSlot.BattleNode);
            _battleNodeButton.interactable = model.BattleNodeInteractable;
            SetButtonColor(
                _battleNodeButton,
                model.BattleNodeCompleted ? DisabledButtonColor : ButtonColor);

            _failureTitle.text = model.GetText(RunEntryTextSlot.FailureTitle);
            _failureHealth.text = model.GetText(RunEntryTextSlot.Health);
            _restartBattleText.text = model.GetText(RunEntryTextSlot.RestartBattle);

            _confirmationTitle.text = model.GetText(RunEntryTextSlot.ConfirmationTitle);
            _confirmationMessage.text = model.GetText(RunEntryTextSlot.ConfirmationMessage);
            _confirmationConfirmText.text = model.GetText(RunEntryTextSlot.ConfirmationConfirm);
            _confirmationCancelText.text = model.GetText(RunEntryTextSlot.Cancel);

            _saveFailureMessage.text = model.GetText(RunEntryTextSlot.SaveFailureMessage);
            _saveFailureHealth.text = model.GetText(RunEntryTextSlot.Health);
            _retrySaveText.text = model.GetText(RunEntryTextSlot.RetrySave);
            _saveFailureExitText.text = model.GetText(RunEntryTextSlot.Exit);

            _rollbackTitle.text = model.GetText(RunEntryTextSlot.RollbackTitle);
            _rollbackMessage.text = model.GetText(RunEntryTextSlot.RollbackMessage);
            _rollbackConfirmText.text = model.GetText(RunEntryTextSlot.RollbackConfirm);
            _rollbackCancelText.text = model.GetText(RunEntryTextSlot.Cancel);
        }

        /// <summary>仅供 EditMode 测试显式建立与 Awake 相同的 UI。</summary>
        internal void BuildForTesting()
        {
            EnsureBuilt();
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

        /// <summary>建立 Canvas、事件系统及十个互斥页面；重复调用不再绑定监听。</summary>
        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            _fontAsset = CreateRuntimeFontAsset();

            CreateEventSystem();
            RectTransform canvas = CreateCanvas();
            RectTransform background = CreatePanel(
                "Background",
                canvas,
                BackgroundColor,
                Vector2.zero,
                Vector2.zero,
                stretch: true);
            RectTransform surface = CreatePanel(
                "ContentSurface",
                background,
                SurfaceColor,
                Vector2.zero,
                new Vector2(940f, 760f),
                stretch: false);

            BuildMainMenuPage(surface);
            BuildHeroSelectionPage(surface);
            BuildSettingsPage(surface);
            BuildComingSoonPage(surface, RunEntryPage.Compendium);
            BuildComingSoonPage(surface, RunEntryPage.Statistics);
            BuildMapPage(surface);
            BuildFailurePage(surface);
            BuildAbandonConfirmationPage(surface);
            BuildSaveFailurePage(surface);
            BuildRollbackConfirmationPage(surface);

            foreach (GameObject page in _pages.Values)
                page.SetActive(false);
            _pages[RunEntryPage.MainMenu].SetActive(true);
        }

        /// <summary>创建只消费 Input System 默认 UI Actions 的场景级 EventSystem。</summary>
        private void CreateEventSystem()
        {
            var eventObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(transform, worldPositionStays: false);
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

        /// <summary>建立包含继续、开始与既有辅助入口的主菜单页。</summary>
        private void BuildMainMenuPage(RectTransform parent)
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

        /// <summary>建立只展示布局占位并可返回的设置页。</summary>
        private void BuildSettingsPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Settings, parent);
            _settingsTitle = CreateText("SettingsTitle", page, 42f, FontStyles.Bold, PrimaryTextColor, 245f, 700f, 64f);
            _settingsPlaceholder = CreateText(
                "SettingsPlaceholder",
                page,
                28f,
                FontStyles.Normal,
                SecondaryTextColor,
                45f,
                720f,
                180f);
            _settingsBackText = CreateButton(
                "SettingsBackButton",
                page,
                new RunEntryAction(RunEntryActionKind.Back),
                -235f,
                width: 260f).label;
        }

        /// <summary>建立图鉴或统计的开发中占位页，并保存各自文本引用。</summary>
        private void BuildComingSoonPage(RectTransform parent, RunEntryPage pageKind)
        {
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

            if (pageKind == RunEntryPage.Compendium)
            {
                _compendiumTitle = title;
                _compendiumPlaceholder = placeholder;
                _compendiumBackText = back;
            }
            else if (pageKind == RunEntryPage.Statistics)
            {
                _statisticsTitle = title;
                _statisticsPlaceholder = placeholder;
                _statisticsBackText = back;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(pageKind));
            }
        }

        /// <summary>建立只含当前生命与唯一可点击战斗节点的临时地图。</summary>
        private void BuildMapPage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Map, parent);
            _mapTitle = CreateText("MapTitle", page, 42f, FontStyles.Bold, PrimaryTextColor, 260f, 700f, 64f);
            _mapHealth = CreateText("MapHealth", page, 26f, FontStyles.Normal, SecondaryTextColor, 180f, 500f, 50f);
            (_battleNodeButton, _battleNodeText) = CreateButton(
                "BattleNodeButton",
                page,
                new RunEntryAction(RunEntryActionKind.EnterBattle),
                5f,
                width: 320f,
                height: 130f);
        }

        /// <summary>建立失败说明、恢复生命投影与唯一重开本关动作。</summary>
        private void BuildFailurePage(RectTransform parent)
        {
            RectTransform page = CreatePage(RunEntryPage.Failure, parent);
            _failureTitle = CreateText("FailureTitle", page, 46f, FontStyles.Bold, PrimaryTextColor, 185f, 700f, 70f);
            _failureHealth = CreateText("FailureHealth", page, 26f, FontStyles.Normal, SecondaryTextColor, 95f, 500f, 50f);
            _restartBattleText = CreateButton(
                "RestartBattleButton",
                page,
                new RunEntryAction(RunEntryActionKind.RestartBattle),
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
            _saveFailureExitText = CreateButton(
                "SaveFailureExitButton",
                page,
                new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure),
                -130f,
                width: 300f).label;
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
            panelObject.GetComponent<Image>().color = color;
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
            button.onClick.AddListener(() =>
            {
                if (button != null && button.IsActive() && button.IsInteractable())
                    ActionRequested?.Invoke(action);
            });
            _boundButtons.Add(button);
        }

        /// <summary>设置按钮底图颜色，保持选择与完成状态为纯表现事实。</summary>
        private static void SetButtonColor(Button button, Color color)
        {
            if (button?.targetGraphic != null)
                button.targetGraphic.color = color;
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
