using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TinySpire.UI.Run
{
    /// <summary>冻结一次入口纸叠布局计算的结果，供运行时应用与 EditMode 验证共用。</summary>
    internal readonly struct EntryPaperStackLayout
    {
        /// <summary>保存当前 Canvas、纸张、最终位置、画外位置与菜单安全区几何。</summary>
        public EntryPaperStackLayout(
            Vector2 canvasSize,
            float compositionWidth,
            Vector2 sheetSize,
            Vector2 redFinalPosition,
            Vector2 charcoalFinalPosition,
            Vector2 ivoryFinalPosition,
            Vector2 redStartPosition,
            Vector2 charcoalStartPosition,
            Vector2 ivoryStartPosition,
            float menuCenterX,
            float contentScale)
        {
            CanvasSize = canvasSize;
            CompositionWidth = compositionWidth;
            SheetSize = sheetSize;
            RedFinalPosition = redFinalPosition;
            CharcoalFinalPosition = charcoalFinalPosition;
            IvoryFinalPosition = ivoryFinalPosition;
            RedStartPosition = redStartPosition;
            CharcoalStartPosition = charcoalStartPosition;
            IvoryStartPosition = ivoryStartPosition;
            MenuCenterX = menuCenterX;
            ContentScale = contentScale;
        }

        public Vector2 CanvasSize { get; }
        public float CompositionWidth { get; }
        public Vector2 SheetSize { get; }
        public Vector2 RedFinalPosition { get; }
        public Vector2 CharcoalFinalPosition { get; }
        public Vector2 IvoryFinalPosition { get; }
        public Vector2 RedStartPosition { get; }
        public Vector2 CharcoalStartPosition { get; }
        public Vector2 IvoryStartPosition { get; }
        public float MenuCenterX { get; }
        public float ContentScale { get; }
    }

    /// <summary>只管理入口背景、三张完整纸、响应式几何与一次性 DOTween，不接触 Run 事实或导航。</summary>
    [DisallowMultipleComponent]
    public sealed class EntryPaperStackView : MonoBehaviour
    {
        internal const float StartRotationDegrees = -8f;
        internal const float FinalRotationDegrees = 17.52f;
        internal const float RedStartTime = 0f;
        internal const float CharcoalStartTime = 0.12f;
        internal const float IvoryStartTime = 0.24f;
        internal const float SheetDuration = 0.76f;
        internal const float IvorySettledTime = IvoryStartTime + SheetDuration;
        internal const float ContentFadeStartTime = 1.10f;
        internal const float ContentFadeDuration = 0.22f;
        internal const float IvoryMidCoverage = 0.3888f;
        internal const float CharcoalRevealRatio = 0.0328f;
        internal const float RedRevealRatio = 0.0253f;
        internal const float BaselineAspect = 16f / 9f;
        internal const float MainMenuButtonWidth = 459f;
        internal const float MainMenuButtonHeight = 99f;

        private static readonly Color32 IvoryTint = new Color32(220, 203, 179, 255);
        private static readonly Color32 CharcoalTint = new Color32(43, 46, 45, 255);
        private static readonly Color32 BrickRedTint = new Color32(131, 54, 44, 255);
        private static readonly Color32 MenuPrimaryText = new Color32(42, 38, 31, 255);
        private static readonly Color32 MenuSecondaryText = new Color32(91, 81, 68, 255);
        private static readonly Color32 ButtonBorder = new Color32(42, 38, 31, 255);
        private static readonly Color32 ButtonTopEdge = new Color32(74, 67, 57, 96);

        private readonly object _tweenId = new object();

        private RectTransform _canvasRect;
        private Image _backgroundImage;
        private RectTransform _backgroundRect;
        private RectTransform _paperStackRoot;
        private RectTransform _redSheet;
        private RectTransform _charcoalSheet;
        private RectTransform _ivorySheet;
        private RectTransform _mainMenuContent;
        private CanvasGroup _mainMenuCanvasGroup;
        private Sprite _backgroundSprite;
        private Sequence _sequence;
        private EntryPaperStackLayout _layout;
        private Vector2 _lastCanvasSize;
        private bool _composed;
        private bool _hasResolvedEntrance;
        private bool _isEntrancePlaying;
        private int _playCount;

        internal Color MenuPrimaryTextColor => MenuPrimaryText;
        internal Color MenuSecondaryTextColor => MenuSecondaryText;
        internal Sequence ActiveSequenceForTesting => _sequence;
        internal object TweenIdForTesting => _tweenId;
        internal int PlayCountForTesting => _playCount;
        internal EntryPaperStackLayout LayoutForTesting => _layout;

        /// <summary>在动态 Canvas 下建立背景与三张完整纸；重复调用会被拒绝。</summary>
        internal void Compose(Sprite backgroundSprite, Texture2D paperTexture)
        {
            if (_composed)
                throw new InvalidOperationException("Entry paper stack is already composed.");
            if (backgroundSprite == null)
                throw new ArgumentNullException(nameof(backgroundSprite));
            if (paperTexture == null)
                throw new ArgumentNullException(nameof(paperTexture));

            _canvasRect = transform as RectTransform
                ?? throw new InvalidOperationException("EntryPaperStackView requires a RectTransform.");
            _backgroundSprite = backgroundSprite;
            _backgroundImage = CreateBackground(_canvasRect, backgroundSprite);
            _backgroundRect = _backgroundImage.rectTransform;
            _paperStackRoot = CreateStretchedRect("PaperStackRoot", _canvasRect);
            _redSheet = CreateSheet("BrickRedFullSheet", _paperStackRoot, BrickRedTint, paperTexture);
            _charcoalSheet = CreateSheet("CharcoalFullSheet", _paperStackRoot, CharcoalTint, paperTexture);
            _ivorySheet = CreateSheet("WarmIvoryFullSheet", _paperStackRoot, IvoryTint, paperTexture);
            _composed = true;
            RebuildGeometry(force: true);
            ApplyFinalVisualState(showContent: false);
        }

        /// <summary>绑定独立于旋转纸叠的主菜单布局与淡入组，并立即按当前 Canvas 定位。</summary>
        internal void BindMainMenuContent(RectTransform content, CanvasGroup canvasGroup)
        {
            _mainMenuContent = content != null
                ? content
                : throw new ArgumentNullException(nameof(content));
            _mainMenuCanvasGroup = canvasGroup != null
                ? canvasGroup
                : throw new ArgumentNullException(nameof(canvasGroup));
            RebuildGeometry(force: true);
            if (!_hasResolvedEntrance)
            {
                _mainMenuCanvasGroup.alpha = 0f;
                SetContentInteraction(enabled: false);
            }
        }

        /// <summary>为主菜单按钮应用无厚侧壁、透明纸面内芯和克制上下缘的八边形样式。</summary>
        internal void ConfigureMainMenuButton(EntryOctagonGraphic graphic)
        {
            if (graphic == null)
                throw new ArgumentNullException(nameof(graphic));

            graphic.Configure(
                ButtonBorder,
                ButtonTopEdge,
                cornerCut: 27f,
                outlineWidth: 2f,
                bottomSeparation: 2f);
        }

        /// <summary>首次主菜单展示时按 0/.12/.24 秒错拍播放一次入口，后续 Render 不重复创建 Tween。</summary>
        internal bool TryPlayEntrance()
        {
            if (!_composed || _mainMenuCanvasGroup == null || _hasResolvedEntrance || !isActiveAndEnabled)
                return false;

            _hasResolvedEntrance = true;
            _playCount++;
            KillOwnedTweens();
            Canvas.ForceUpdateCanvases();
            RebuildGeometry(force: true);
            _isEntrancePlaying = true;
            ApplyStartVisualState();

            _sequence = DOTween.Sequence()
                .SetId(_tweenId)
                .SetUpdate(isIndependentUpdate: true);
            InsertSheetTween(_sequence, _redSheet, _layout.RedFinalPosition, RedStartTime);
            InsertSheetTween(
                _sequence,
                _charcoalSheet,
                _layout.CharcoalFinalPosition,
                CharcoalStartTime);
            InsertSheetTween(_sequence, _ivorySheet, _layout.IvoryFinalPosition, IvoryStartTime);
            _sequence.InsertCallback(ContentFadeStartTime, EnableContentForFade);
            _sequence.Insert(
                ContentFadeStartTime,
                _mainMenuCanvasGroup
                    .DOFade(1f, ContentFadeDuration)
                    .SetEase(Ease.OutQuad));
            _sequence.OnComplete(HandleEntranceCompleted);
            return true;
        }

        /// <summary>非主菜单首次渲染时直接稳定到最终构图，避免战后回场景播放主菜单入场。</summary>
        internal void ResolveWithoutEntrance()
        {
            if (!_composed)
                return;
            if (_hasResolvedEntrance && !_isEntrancePlaying)
            {
                ApplyFinalVisualState(showContent: true);
                return;
            }

            _hasResolvedEntrance = true;
            _isEntrancePlaying = false;
            KillOwnedTweens();
            RebuildGeometry(force: true);
            ApplyFinalVisualState(showContent: true);
        }

        /// <summary>依据当前 Canvas 计算完整纸尺寸、参考图边界、画外起点和菜单安全区。</summary>
        internal static EntryPaperStackLayout CalculateLayout(Vector2 canvasSize)
        {
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(canvasSize));

            float compositionWidth = Mathf.Min(canvasSize.x, canvasSize.y * BaselineAspect);
            float bleed = 0.06f * Mathf.Max(canvasSize.x, canvasSize.y);
            float sheetHeight = Mathf.Sqrt(
                canvasSize.x * canvasSize.x + canvasSize.y * canvasSize.y) + 2f * bleed;
            float sheetWidth = 0.72f * compositionWidth + 2f * bleed;
            Vector2 sheetSize = new Vector2(sheetWidth, sheetHeight);
            float radians = FinalRotationDegrees * Mathf.Deg2Rad;
            Vector2 localXAxis = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            float ivoryRightEdgeAtMid = -0.5f * canvasSize.x + IvoryMidCoverage * compositionWidth;
            Vector2 ivoryFinal = new Vector2(
                ivoryRightEdgeAtMid - 0.5f * sheetWidth / localXAxis.x,
                0f);
            Vector2 charcoalFinal =
                ivoryFinal + localXAxis * (CharcoalRevealRatio * compositionWidth);
            Vector2 redFinal =
                charcoalFinal + localXAxis * (RedRevealRatio * compositionWidth);

            float startRadians = Mathf.Abs(StartRotationDegrees) * Mathf.Deg2Rad;
            float startHalfExtent =
                0.5f * sheetWidth * Mathf.Cos(startRadians) +
                0.5f * sheetHeight * Mathf.Sin(startRadians);
            float startX = -0.5f * canvasSize.x - startHalfExtent - bleed;
            Vector2 redStart = new Vector2(startX, redFinal.y);
            Vector2 charcoalStart = new Vector2(startX, charcoalFinal.y);
            Vector2 ivoryStart = new Vector2(startX, ivoryFinal.y);

            float contentScale = Mathf.Min(1f, compositionWidth / 1920f);
            float menuCenterFromLeft = Mathf.Max(
                0.175f * compositionWidth,
                0.5f * MainMenuButtonWidth * contentScale + 32f);
            float menuCenterX = -0.5f * canvasSize.x + menuCenterFromLeft;
            return new EntryPaperStackLayout(
                canvasSize,
                compositionWidth,
                sheetSize,
                redFinal,
                charcoalFinal,
                ivoryFinal,
                redStart,
                charcoalStart,
                ivoryStart,
                menuCenterX,
                contentScale);
        }

        /// <summary>读取一张旋转纸右边在指定 Canvas 本地 Y 上的 X，供响应式验收复用。</summary>
        internal static float GetRightEdgeX(
            Vector2 sheetCenter,
            Vector2 sheetSize,
            float rotationDegrees,
            float canvasLocalY)
        {
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return sheetCenter.x +
                   (0.5f * sheetSize.x - sine * (canvasLocalY - sheetCenter.y)) / cosine;
        }

        /// <summary>Canvas 尺寸真实变化时只重算一次；若正处于入场则收口到稳定最终态。</summary>
        private void OnRectTransformDimensionsChange()
        {
            if (!_composed || _canvasRect == null)
                return;

            Vector2 currentSize = _canvasRect.rect.size;
            if (Approximately(currentSize, _lastCanvasSize))
                return;

            bool interruptedEntrance = _isEntrancePlaying;
            KillOwnedTweens();
            RebuildGeometry(force: true);
            if (interruptedEntrance)
            {
                _hasResolvedEntrance = true;
                _isEntrancePlaying = false;
                ApplyFinalVisualState(showContent: true);
            }
        }

        /// <summary>组件重新启用时不重播；已开始过的入口直接恢复最终稳定构图。</summary>
        private void OnEnable()
        {
            if (_composed && _hasResolvedEntrance)
            {
                RebuildGeometry(force: true);
                ApplyFinalVisualState(showContent: true);
            }
        }

        /// <summary>禁用时精确终止本组件拥有的补间，不影响其他 UI Tween。</summary>
        private void OnDisable()
        {
            KillOwnedTweens();
        }

        /// <summary>销毁时再次幂等回收本组件拥有的补间。</summary>
        private void OnDestroy()
        {
            KillOwnedTweens();
        }

        /// <summary>按当前 Canvas 尺寸重建背景 cover、三纸几何和菜单位置，不创建纹理或 Sprite。</summary>
        private void RebuildGeometry(bool force)
        {
            if (!_composed || _canvasRect == null)
                return;

            Vector2 size = _canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;
            if (!force && Approximately(size, _lastCanvasSize))
                return;

            _lastCanvasSize = size;
            _layout = CalculateLayout(size);
            ApplyBackgroundCover(size);
            ApplySheetGeometry(_redSheet, _layout.RedFinalPosition);
            ApplySheetGeometry(_charcoalSheet, _layout.CharcoalFinalPosition);
            ApplySheetGeometry(_ivorySheet, _layout.IvoryFinalPosition);
            if (_mainMenuContent != null)
            {
                _mainMenuContent.anchorMin = new Vector2(0.5f, 0.5f);
                _mainMenuContent.anchorMax = new Vector2(0.5f, 0.5f);
                _mainMenuContent.pivot = new Vector2(0.5f, 0.5f);
                _mainMenuContent.anchoredPosition = new Vector2(_layout.MenuCenterX, 0f);
                _mainMenuContent.localScale = Vector3.one * _layout.ContentScale;
            }
        }

        /// <summary>保持 16:9 背景不变形；超宽裁上下，窄窗裁左并以右缘对齐保护塔主体。</summary>
        private void ApplyBackgroundCover(Vector2 canvasSize)
        {
            float sourceAspect = _backgroundSprite.rect.width / _backgroundSprite.rect.height;
            float canvasAspect = canvasSize.x / canvasSize.y;
            Vector2 backgroundSize = canvasAspect >= sourceAspect
                ? new Vector2(canvasSize.x, canvasSize.x / sourceAspect)
                : new Vector2(canvasSize.y * sourceAspect, canvasSize.y);
            float horizontalOverflow = Mathf.Max(0f, backgroundSize.x - canvasSize.x);
            _backgroundRect.sizeDelta = backgroundSize;
            _backgroundRect.anchoredPosition = new Vector2(-0.5f * horizontalOverflow, 0f);
        }

        /// <summary>给纸张应用共享尺寸、最终中心与最终构成主义角度。</summary>
        private void ApplySheetGeometry(RectTransform sheet, Vector2 finalPosition)
        {
            sheet.sizeDelta = _layout.SheetSize;
            sheet.anchoredPosition = finalPosition;
            sheet.localRotation = Quaternion.Euler(0f, 0f, FinalRotationDegrees);
        }

        /// <summary>把三张纸放在按当前 Canvas 推导的左侧画外，并隐藏主菜单内容。</summary>
        private void ApplyStartVisualState()
        {
            SetSheetState(_redSheet, _layout.RedStartPosition, StartRotationDegrees);
            SetSheetState(_charcoalSheet, _layout.CharcoalStartPosition, StartRotationDegrees);
            SetSheetState(_ivorySheet, _layout.IvoryStartPosition, StartRotationDegrees);
            _mainMenuCanvasGroup.alpha = 0f;
            SetContentInteraction(enabled: false);
        }

        /// <summary>立即应用三纸最终位置，并按调用方决定是否显示主菜单内容。</summary>
        private void ApplyFinalVisualState(bool showContent)
        {
            if (!_composed)
                return;

            SetSheetState(_redSheet, _layout.RedFinalPosition, FinalRotationDegrees);
            SetSheetState(_charcoalSheet, _layout.CharcoalFinalPosition, FinalRotationDegrees);
            SetSheetState(_ivorySheet, _layout.IvoryFinalPosition, FinalRotationDegrees);
            if (_mainMenuCanvasGroup != null)
            {
                _mainMenuCanvasGroup.alpha = showContent ? 1f : 0f;
                SetContentInteraction(enabled: showContent);
            }
        }

        /// <summary>写入单张纸的位置与 Z 旋转，不触碰层级或共享纹理。</summary>
        private static void SetSheetState(
            RectTransform sheet,
            Vector2 position,
            float rotationDegrees)
        {
            sheet.anchoredPosition = position;
            sheet.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        }

        /// <summary>在父时间线中插入一张纸的平移与旋转，使用无弹跳的 OutCubic。</summary>
        private static void InsertSheetTween(
            Sequence sequence,
            RectTransform sheet,
            Vector2 finalPosition,
            float startTime)
        {
            sequence.Insert(
                startTime,
                sheet.DOAnchorPos(finalPosition, SheetDuration).SetEase(Ease.OutCubic));
            sequence.Insert(
                startTime,
                sheet
                    .DOLocalRotate(new Vector3(0f, 0f, FinalRotationDegrees), SheetDuration)
                    .SetEase(Ease.OutCubic));
        }

        /// <summary>菜单淡入开始时恢复其交互与 raycast；此前透明控件不会截获输入。</summary>
        private void EnableContentForFade()
        {
            if (this != null && _mainMenuCanvasGroup != null)
                SetContentInteraction(enabled: true);
        }

        /// <summary>入口时间线结束后清除句柄并钉住最终可读、可点状态。</summary>
        private void HandleEntranceCompleted()
        {
            _sequence = null;
            _isEntrancePlaying = false;
            ApplyFinalVisualState(showContent: true);
        }

        /// <summary>切换主菜单内容的交互和 raycast，不修改任何按钮业务状态。</summary>
        private void SetContentInteraction(bool enabled)
        {
            if (_mainMenuCanvasGroup == null)
                return;

            _mainMenuCanvasGroup.interactable = enabled;
            _mainMenuCanvasGroup.blocksRaycasts = enabled;
        }

        /// <summary>按私有 ID 和本地引用幂等终止入口补间。</summary>
        private void KillOwnedTweens()
        {
            DOTween.Kill(_tweenId, complete: false);
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill(complete: false);
            _sequence = null;
            _isEntrancePlaying = false;
        }

        /// <summary>创建不接收 raycast 的等比背景 Image。</summary>
        private static Image CreateBackground(RectTransform parent, Sprite sprite)
        {
            var backgroundObject = new GameObject(
                "EntryTowerBackgroundView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backgroundObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = backgroundObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = backgroundObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>创建只作几何父级、铺满 Canvas 且不包含 Graphic 的纸叠根。</summary>
        private static RectTransform CreateStretchedRect(string objectName, RectTransform parent)
        {
            var rectObject = new GameObject(objectName, typeof(RectTransform));
            rectObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>创建共用同一中性纹理、仅以 tint 区分且不接收 raycast 的完整纸张。</summary>
        private static RectTransform CreateSheet(
            string objectName,
            RectTransform parent,
            Color tint,
            Texture2D paperTexture)
        {
            var sheetObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            sheetObject.transform.SetParent(parent, worldPositionStays: false);
            RectTransform rect = sheetObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            RawImage image = sheetObject.GetComponent<RawImage>();
            image.texture = paperTexture;
            image.color = tint;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>以小容差比较 Canvas 尺寸，避免同一次布局通知重复重建。</summary>
        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.01f &&
                   Mathf.Abs(left.y - right.y) < 0.01f;
        }
    }
}
