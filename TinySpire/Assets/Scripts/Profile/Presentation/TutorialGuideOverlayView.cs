using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TinySpire.Profile.Presentation
{
    /// <summary>跨场景保留的全局教程层；只渲染稳定文本键与三类既定输入事件。</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialGuideOverlayView : MonoBehaviour, ITutorialGuideView
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

        private const string RequiredTutorialGlyphs =
            "欢迎开始一局游戏选择英雄然后沿连通路线完成这一幕确定初始牌组和本局身份请可到达的节点已会保留只有相连会解锁将卡牌拖到有效目标留意能量敌人意图准备好后结束回合战斗可一张奖励或跳过立即保存休息宝箱商店事件只结算一次在确认前检查已经查看返回菜单摘要统计记录中知道了重置教程，。";

        private const float PromptFontSize = 31f;
        private const float ButtonFontSize = 22f;
        private const float HiddenResetFontSize = 19f;

        private static readonly Color32 BackdropColor = new Color32(5, 8, 13, 206);
        private static readonly Color32 PaperColor = new Color32(27, 29, 34, 252);
        private static readonly Color32 PaperEdgeColor = new Color32(137, 105, 67, 230);
        private static readonly Color32 PrimaryTextColor = new Color32(236, 226, 204, 255);
        private static readonly Color32 SecondaryTextColor = new Color32(190, 171, 137, 255);
        private static readonly Color32 ButtonColor = new Color32(52, 49, 49, 255);
        private static readonly Color32 HiddenResetButtonColor = new Color32(34, 34, 38, 225);

        private Canvas _canvas;
        private GameObject _overlayRoot;
        private CanvasGroup _inputGate;
        private Image _backdrop;
        private Image _paper;
        private Outline _paperOutline;
        private TMP_Text _promptText;
        private TMP_Text _confirmText;
        private TMP_Text _skipText;
        private TMP_Text _resetText;
        private TMP_Text _hiddenResetText;
        private Button _confirmButton;
        private Button _skipButton;
        private Button _resetButton;
        private Button _hiddenResetButton;
        private TMP_FontAsset _fontAsset;
        private GameObject _selectionBeforeOverlay;
        private Func<string, string> _localize;
        private IDisposable _localeSubscription;
        private TutorialGuideViewModel _currentModel = TutorialGuideViewModel.Hidden;
        private bool _initialized;
        private bool _destroyed;
        private bool _ownsFontAsset;

        /// <summary>玩家确认当前提示时发布无 payload 事件。</summary>
        public event Action ConfirmRequested;

        /// <summary>玩家跳过余下教程时发布无 payload 事件。</summary>
        public event Action SkipRequested;

        /// <summary>玩家重置教程时发布无 payload 事件。</summary>
        public event Action ResetRequested;

        /// <summary>组件挂载时立即建立唯一 ScreenSpaceOverlay 层，初始保持隐藏且不拦截输入。</summary>
        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>绑定生产 LocalizationService，并把 locale 流收窄为无载荷重绘订阅。</summary>
        public void Initialize(LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            Initialize(
                key => localization.GetString(key),
                handler => localization.LocaleChanged.Subscribe(_ => handler()));
        }

        /// <summary>用可替换本地化 seam 初始化真实 View，供 EditMode 验证 key 与重绘契约。</summary>
        internal void Initialize(
            Func<string, string> localize,
            Func<Action, IDisposable> subscribeLocaleChanged)
        {
            if (_destroyed)
                throw new ObjectDisposedException(nameof(TutorialGuideOverlayView));
            if (_initialized)
                return;
            EnsureBuilt();
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
            if (subscribeLocaleChanged == null)
                throw new ArgumentNullException(nameof(subscribeLocaleChanged));

            _localeSubscription = subscribeLocaleChanged(RedrawCurrentModel) ??
                                  throw new InvalidOperationException(
                                      "Tutorial locale subscription cannot be null.");
            _initialized = true;
            RedrawCurrentModel();
        }

        /// <summary>幂等建立动态 UI；兼容运行时 Awake 与 EditMode AddComponent 两种生命周期。</summary>
        internal void EnsureBuilt()
        {
            if (_canvas != null)
                return;

            BuildUi();
            ApplyVisibility(TutorialGuideViewModel.Hidden);
        }

        /// <summary>用完整不可变投影替换当前教程层，隐藏投影立即释放底层输入。</summary>
        public void Render(TutorialGuideViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (_destroyed)
                throw new ObjectDisposedException(nameof(TutorialGuideOverlayView));
            if (!_initialized)
                throw new InvalidOperationException(
                    "Tutorial overlay view must be initialized before rendering.");

            _currentModel = model;
            RedrawCurrentModel();
        }

        /// <summary>释放 locale 订阅和按钮监听，避免销毁后的全局层继续接收回调。</summary>
        private void OnDestroy()
        {
            if (_destroyed)
                return;

            _destroyed = true;
            _localeSubscription?.Dispose();
            _localeSubscription = null;
            _confirmButton?.onClick.RemoveListener(HandleConfirmClick);
            _skipButton?.onClick.RemoveListener(HandleSkipClick);
            _resetButton?.onClick.RemoveListener(HandleResetClick);
            _hiddenResetButton?.onClick.RemoveListener(HandleResetClick);
            if (_ownsFontAsset && _fontAsset != null)
                DestroyOwnedObject(_fontAsset);
            ConfirmRequested = null;
            SkipRequested = null;
            ResetRequested = null;
        }

        /// <summary>动态创建自有深色纸张风格 Canvas、正文与三枚操作按钮。</summary>
        private void BuildUi()
        {
            _fontAsset = CreateRuntimeFontAsset();
            var canvasObject = new GameObject(
                "Tutorial Guide Overlay Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, worldPositionStays: false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 32000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _overlayRoot = new GameObject(
                "Tutorial Guide Input Gate",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            _overlayRoot.transform.SetParent(canvasObject.transform, worldPositionStays: false);
            Stretch(_overlayRoot.GetComponent<RectTransform>());
            _backdrop = _overlayRoot.GetComponent<Image>();
            _backdrop.color = BackdropColor;
            _backdrop.raycastTarget = false;
            _inputGate = _overlayRoot.GetComponent<CanvasGroup>();

            RectTransform paper = CreatePaperPanel(_overlayRoot.transform);
            _paper = paper.GetComponent<Image>();
            _paperOutline = paper.GetComponent<Outline>();
            _promptText = CreateText(
                "Tutorial Prompt",
                paper,
                new Vector2(0f, 62f),
                new Vector2(680f, 215f),
                fontSize: PromptFontSize,
                FontStyles.Normal,
                PrimaryTextColor,
                _fontAsset);
            (_confirmButton, _confirmText) = CreateButton(
                "Tutorial Confirm",
                paper,
                new Vector2(-214f, -142f),
                _fontAsset);
            (_skipButton, _skipText) = CreateButton(
                "Tutorial Skip",
                paper,
                new Vector2(0f, -142f),
                _fontAsset);
            (_resetButton, _resetText) = CreateButton(
                "Tutorial Reset",
                paper,
                new Vector2(214f, -142f),
                _fontAsset);
            _confirmButton.onClick.AddListener(HandleConfirmClick);
            _skipButton.onClick.AddListener(HandleSkipClick);
            _resetButton.onClick.AddListener(HandleResetClick);
            (_hiddenResetButton, _hiddenResetText) = CreateHiddenResetButton(
                canvasObject.GetComponent<RectTransform>(),
                _fontAsset);
            _hiddenResetButton.onClick.AddListener(HandleResetClick);
        }

        /// <summary>创建带暖色描边与阴影的独立深色纸面，不借用场景或 RunEntry 视觉资源。</summary>
        private static RectTransform CreatePaperPanel(Transform parent)
        {
            var paperObject = new GameObject(
                "Tutorial Dark Paper",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            paperObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform paper = paperObject.GetComponent<RectTransform>();
            SetCenteredRect(paper, Vector2.zero, new Vector2(780f, 440f));
            Image image = paperObject.GetComponent<Image>();
            image.color = PaperColor;
            image.raycastTarget = false;
            Outline outline = paperObject.GetComponent<Outline>();
            outline.effectColor = PaperEdgeColor;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = true;
            return paper;
        }

        /// <summary>创建一个居中 TMP 文本区域，并保持文字本身不参与 raycast。</summary>
        private static TMP_Text CreateText(
            string objectName,
            RectTransform parent,
            Vector2 position,
            Vector2 size,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TMP_FontAsset fontAsset)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            SetCenteredRect(rect, position, size);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = fontAsset != null
                ? fontAsset
                : throw new ArgumentNullException(nameof(fontAsset));
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        /// <summary>创建统一深色纸面按钮与 TMP 标签，具体语义仅由绑定事件区分。</summary>
        private static (Button button, TMP_Text label) CreateButton(
            string objectName,
            RectTransform parent,
            Vector2 position,
            TMP_FontAsset fontAsset)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetCenteredRect(rect, position, new Vector2(188f, 62f));
            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(225, 201, 157, 255);
            colors.pressedColor = new Color32(173, 143, 99, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            TMP_Text label = CreateText(
                objectName + " Label",
                rect,
                Vector2.zero,
                new Vector2(166f, 52f),
                fontSize: ButtonFontSize,
                FontStyles.Bold,
                SecondaryTextColor,
                fontAsset);
            return (button, label);
        }

        /// <summary>创建右上角局部 Reset 入口，教程隐藏时不启用任何全屏 raycast 门禁。</summary>
        private static (Button button, TMP_Text label) CreateHiddenResetButton(
            RectTransform parent,
            TMP_FontAsset fontAsset)
        {
            var buttonObject = new GameObject(
                "Tutorial Hidden Reset",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(154f, 50f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = HiddenResetButtonColor;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(225, 201, 157, 255);
            colors.pressedColor = new Color32(173, 143, 99, 255);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            TMP_Text label = CreateText(
                "Tutorial Hidden Reset Label",
                rect,
                Vector2.zero,
                new Vector2(138f, 42f),
                fontSize: HiddenResetFontSize,
                FontStyles.Bold,
                SecondaryTextColor,
                fontAsset);
            buttonObject.SetActive(false);
            return (button, label);
        }

        /// <summary>按当前语言重新解析 ViewModel 的四个稳定 key，并同步输入门禁。</summary>
        private void RedrawCurrentModel()
        {
            ApplyVisibility(_currentModel);
            if (!_currentModel.IsVisible)
            {
                ClearTexts();
                _hiddenResetText.text = Localize(_currentModel.ResetTextKey);
                return;
            }

            _hiddenResetText.text = string.Empty;
            _promptText.text = Localize(_currentModel.PromptTextKey);
            _confirmText.text = Localize(_currentModel.ConfirmTextKey);
            _skipText.text = Localize(_currentModel.SkipTextKey);
            _resetText.text = Localize(_currentModel.ResetTextKey);
        }

        /// <summary>应用只读可访问性投影；文字缩放不改变教程进度或玩法事实。</summary>
        public void ApplyAccessibility(TutorialGuideAccessibilityViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (_destroyed)
                throw new ObjectDisposedException(nameof(TutorialGuideOverlayView));

            EnsureBuilt();
            float scale = (int)model.TextScale / 100f;
            _promptText.fontSize = PromptFontSize * scale;
            _confirmText.fontSize = ButtonFontSize * scale;
            _skipText.fontSize = ButtonFontSize * scale;
            _resetText.fontSize = ButtonFontSize * scale;
            _hiddenResetText.fontSize = HiddenResetFontSize * scale;
            ApplyHighContrast(model.HighContrast);
            ApplyReducedMotion(model.ReducedMotion);
        }

        /// <summary>在高对比黑白调色板与原始纸张调色板之间完整切换。</summary>
        private void ApplyHighContrast(bool isEnabled)
        {
            _backdrop.color = isEnabled ? Color.black : BackdropColor;
            _paper.color = isEnabled ? Color.black : PaperColor;
            _paperOutline.effectColor = isEnabled ? Color.white : PaperEdgeColor;
            _promptText.color = isEnabled ? Color.white : PrimaryTextColor;
            ApplyButtonPalette(_confirmButton, _confirmText, isEnabled, ButtonColor);
            ApplyButtonPalette(_skipButton, _skipText, isEnabled, ButtonColor);
            ApplyButtonPalette(_resetButton, _resetText, isEnabled, ButtonColor);
            ApplyButtonPalette(
                _hiddenResetButton,
                _hiddenResetText,
                isEnabled,
                HiddenResetButtonColor);
        }

        /// <summary>统一应用一枚教程按钮的高对比或默认前景与背景颜色。</summary>
        private static void ApplyButtonPalette(
            Button button,
            TMP_Text label,
            bool highContrast,
            Color defaultBackground)
        {
            Graphic target = button.targetGraphic;
            target.color = highContrast ? Color.white : defaultBackground;
            label.color = highContrast ? Color.black : SecondaryTextColor;
        }

        /// <summary>按减少动态设置关闭或恢复四枚教程按钮的颜色过渡。</summary>
        private void ApplyReducedMotion(bool isEnabled)
        {
            Selectable.Transition transition = isEnabled
                ? Selectable.Transition.None
                : Selectable.Transition.ColorTint;
            _confirmButton.transition = transition;
            _skipButton.transition = transition;
            _resetButton.transition = transition;
            _hiddenResetButton.transition = transition;
        }

        /// <summary>切换 overlay 可见性，并让 BlocksInput 成为拦截下层 raycast 的唯一开关。</summary>
        private void ApplyVisibility(TutorialGuideViewModel model)
        {
            bool visible = model.IsVisible;
            bool wasVisible = _overlayRoot.activeSelf;
            EventSystem eventSystem = ResolveEventSystem();
            if (visible && !wasVisible && eventSystem != null)
                _selectionBeforeOverlay = eventSystem.currentSelectedGameObject;
            if (visible && !_overlayRoot.activeSelf)
                _overlayRoot.SetActive(true);

            _inputGate.alpha = visible ? 1f : 0f;
            _inputGate.interactable = visible;
            _inputGate.blocksRaycasts = visible && model.BlocksInput;
            _backdrop.raycastTarget = visible && model.BlocksInput;
            _hiddenResetButton.gameObject.SetActive(_initialized && !visible);

            if (visible && eventSystem != null &&
                (eventSystem.currentSelectedGameObject == null ||
                 !eventSystem.currentSelectedGameObject.transform.IsChildOf(transform)))
            {
                eventSystem.SetSelectedGameObject(_confirmButton.gameObject);
            }

            if (!visible && _overlayRoot.activeSelf)
            {
                _overlayRoot.SetActive(false);
                if (eventSystem != null &&
                    _selectionBeforeOverlay != null &&
                    _selectionBeforeOverlay.activeInHierarchy)
                {
                    eventSystem.SetSelectedGameObject(_selectionBeforeOverlay);
                }

                _selectionBeforeOverlay = null;
            }
        }

        /// <summary>优先使用运行时当前 EventSystem；EditMode 或域重载窗口中回退到任一激活实例。</summary>
        private static EventSystem ResolveEventSystem()
        {
            return EventSystem.current != null
                ? EventSystem.current
                : UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        }

        /// <summary>严格经初始化后的本地化函数解析一个稳定 key，并拒绝空返回。</summary>
        private string Localize(string key)
        {
            string value = _localize(key);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"Tutorial localization returned no text for key '{key}'.");
            }

            return value;
        }

        /// <summary>隐藏时清除上一语言文本，防止非激活层保留陈旧视觉内容。</summary>
        private void ClearTexts()
        {
            _promptText.text = string.Empty;
            _confirmText.text = string.Empty;
            _skipText.text = string.Empty;
            _resetText.text = string.Empty;
        }

        /// <summary>仅在当前 View 可见且可交互时发布确认事件。</summary>
        private void HandleConfirmClick()
        {
            if (_currentModel.IsVisible && _inputGate.interactable)
                ConfirmRequested?.Invoke();
        }

        /// <summary>仅在当前 View 可见且可交互时发布跳过事件。</summary>
        private void HandleSkipClick()
        {
            if (_currentModel.IsVisible && _inputGate.interactable)
                SkipRequested?.Invoke();
        }

        /// <summary>仅在当前 View 可见且可交互时发布重置事件。</summary>
        private void HandleResetClick()
        {
            if (_initialized && !_destroyed)
                ResetRequested?.Invoke();
        }

        /// <summary>把 RectTransform 拉伸至父级完整区域。</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>把 RectTransform 设为中心锚点的固定尺寸区域。</summary>
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

        /// <summary>为 Windows 产品基线创建可动态扩展的 CJK TMP 字库，失败时显式回退默认字库。</summary>
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

                dynamicAsset.name = $"Tutorial Runtime Font ({familyName})";
                dynamicAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                dynamicAsset.isMultiAtlasTexturesEnabled = true;
                if (!dynamicAsset.TryAddCharacters(
                        RequiredTutorialGlyphs,
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
                "Tutorial overlay could not create a CJK-capable operating-system TMP font. " +
                "Chinese glyphs may be unavailable on this machine.");
            return fallback;
        }

        /// <summary>按 EditMode/PlayMode 使用安全的 Unity Object 销毁入口。</summary>
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
