using System;
using DG.Tweening;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HandCardVisual : MonoBehaviour
{
    private const string DisabledOverlayName = "DisabledOverlay";
    private static readonly Color DisabledOverlayColor = new Color(0.42f, 0.42f, 0.42f, 0.58f);

    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _cardContent;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _costText;
    [SerializeField] private Text _typeText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Image _illustrationImage;

    private CanvasGroup _dragFeedbackCanvasGroup;
    private Image _disabledOverlayImage;
    private Vector3 _baseScale;
    private HandCardPose _basePose;
    private Tween _activeTween;
    private Tween _feedbackTween;
    private Tween _targetFocusTransitionTween;
    private Tween _targetFocusBreathTween;
    private readonly object _targetFocusTransitionTweenId = new object();
    private readonly object _targetFocusBreathTweenId = new object();
    private bool _hasPlayFeedback;
    private bool _isPlayerInputEnabled = true;
    private BattleCommandHandle _pendingCommandHandle;
    private Color _normalCostColor;
    private bool _hasNormalCostColor;
    private Action _requestIncomingCardFastForward;
    private bool _isIncomingCardMotionActive;

    public CardInstanceId CardId { get; private set; }
    public float CurrentAnchoredY => _cardContent.anchoredPosition.y;
    public RectTransform CardContent => _cardContent;
    public bool IsCommandPending => _pendingCommandHandle != null;
    internal bool IsIncomingCardMotionActive => _isIncomingCardMotionActive;

    /// <summary>
    /// 初始化该 View 对应的卡牌实例及拖拽反馈依赖。
    /// </summary>
    public void Initialize(
        Vector3 baseScale,
        CardInstanceId cardId,
        CanvasGroup dragFeedbackCanvasGroup)
    {
        EnsureReferences();
        _baseScale = baseScale;
        CardId = cardId;
        _dragFeedbackCanvasGroup = dragFeedbackCanvasGroup;
        _normalCostColor = _costText.color;
        _hasNormalCostColor = true;
        _canvas.overrideSorting = true;
        RefreshInteractionState();
    }

    /// <summary>
    /// 用当前格式化文本、费用和等比覆盖牌面刷新卡牌展示；不保存战斗事实。
    /// </summary>
    public void Bind(CardPresentationText presentation, int cost, Sprite illustration)
    {
        EnsureReferences();
        _titleText.text = presentation.Name;
        _costText.text = cost.ToString();
        _typeText.text = string.Empty;
        _descriptionText.text = presentation.Description;
        ApplyIllustration(illustration);
    }

    /// <summary>
    /// 立刻应用手牌排布计算出的基础姿态。
    /// </summary>
    public void SetBasePoseImmediately(HandCardPose pose)
    {
        CancelTargetFocus();
        KillActiveTween();
        _basePose = pose;
        ApplyBasePose();
    }

    /// <summary>
    /// 记录下次恢复或补间所使用的基础姿态。
    /// </summary>
    public void SetBasePose(HandCardPose pose)
    {
        _basePose = pose;
    }

    /// <summary>
    /// 以补间动画回到当前基础姿态。
    /// </summary>
    public void PlayBasePose(float duration, Ease ease)
    {
        CancelTargetFocus();
        KillActiveTween();
        _canvas.sortingOrder = _basePose.SortingOrder;
        _activeTween = DOTween.Sequence()
            .Join(_cardContent.DOAnchorPos(_basePose.AnchoredPosition, duration).SetEase(ease))
            .Join(_cardContent.DOLocalRotate(new Vector3(0f, 0f, _basePose.RotationDegrees), duration).SetEase(ease))
            .Join(_cardContent.DOScale(_baseScale, duration).SetEase(ease));
    }

    /// <summary>
    /// 播放悬停抬升、放大与层级提升效果。
    /// </summary>
    public void PlayHover(float lift, float scaleMultiplier, int elevatedSortingOrder, float duration)
    {
        CancelTargetFocus();
        KillActiveTween();
        _canvas.sortingOrder = elevatedSortingOrder;
        _activeTween = DOTween.Sequence()
            .Join(_cardContent.DOAnchorPos(_basePose.AnchoredPosition + Vector2.up * lift, duration).SetEase(Ease.OutBack))
            .Join(_cardContent.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.OutBack))
            .Join(_cardContent.DOScale(_baseScale * scaleMultiplier, duration).SetEase(Ease.OutBack));
    }

    /// <summary>
    /// 切换到拖拽展示姿态，并清除上一段补间。
    /// </summary>
    public void BeginDrag(int elevatedSortingOrder)
    {
        CancelTargetFocus();
        KillActiveTween();
        SetDragPlayFeedback(false);
        _canvas.sortingOrder = elevatedSortingOrder;
        _cardContent.localEulerAngles = Vector3.zero;
        _cardContent.localScale = _baseScale;
    }

    /// <summary>把 Enemy 瞄准卡从当前屏幕位置补间到 focus anchor，并在到达后开始轻微呼吸。</summary>
    public void PlayTargetFocus(
        Vector2 focusScreenPosition,
        float focusScale,
        float duration,
        Ease ease,
        float breathScale,
        float breathDuration)
    {
        EnsureReferences();
        if (focusScale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(focusScale));
        if (duration < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (breathScale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(breathScale));
        if (breathDuration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(breathDuration));

        CancelTargetFocus();
        KillActiveTween();
        float scaleFactor = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        Vector2 screenDelta = focusScreenPosition - GetScreenCenter();
        Vector2 focusAnchoredPosition = _cardContent.anchoredPosition + screenDelta / scaleFactor;
        Vector3 focusLocalScale = _baseScale * focusScale;
        _targetFocusTransitionTween = DOTween.Sequence()
            .SetId(_targetFocusTransitionTweenId)
            .Join(_cardContent.DOAnchorPos(focusAnchoredPosition, duration).SetEase(ease))
            .Join(_cardContent.DOLocalRotate(Vector3.zero, duration).SetEase(ease))
            .Join(_cardContent.DOScale(focusLocalScale, duration).SetEase(ease))
            .OnComplete(() => StartTargetFocusBreath(
                focusLocalScale,
                breathScale,
                breathDuration));
    }

    /// <summary>精确终止该卡拥有的聚焦与呼吸 Tween，不写回基础姿态或任何战斗事实。</summary>
    public void CancelTargetFocus()
    {
        DOTween.Kill(_targetFocusTransitionTweenId, complete: false);
        DOTween.Kill(_targetFocusBreathTweenId, complete: false);
        _targetFocusTransitionTween = null;
        _targetFocusBreathTween = null;
    }

    /// <summary>按当前玩家阶段启用或锁定卡牌输入，不改变任何卡区事实。</summary>
    public void SetPlayerInputEnabled(bool isEnabled)
    {
        _isPlayerInputEnabled = isEnabled;
        RefreshInteractionState();
    }

    /// <summary>把权威离手卡收口为无 pending、无目标反馈、无 raycast 的非交互 transient View。</summary>
    public void PrepareAsTransient()
    {
        EnsureReferences();
        CancelTargetFocus();
        KillActiveTween();
        KillFeedbackTween();
        _pendingCommandHandle = null;
        _hasPlayFeedback = false;
        _requestIncomingCardFastForward = null;
        _isIncomingCardMotionActive = false;
        _isPlayerInputEnabled = false;
        if (_dragFeedbackCanvasGroup != null)
            _dragFeedbackCanvasGroup.alpha = 1f;
        if (_disabledOverlayImage != null)
            _disabledOverlayImage.gameObject.SetActive(false);
        if (_hasNormalCostColor)
            _costText.color = _normalCostColor;
        RefreshInteractionState();
    }

    /// <summary>在 cue 实际开始时从当前屏幕位置计算目标，供同一 transient 串联目标与弃牌轨迹。</summary>
    internal Tween CreateTransientScreenMotionTween(
        Vector2 targetScreenPosition,
        float duration,
        Ease ease)
    {
        EnsureReferences();
        if (duration < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));

        Vector2 startAnchoredPosition = default;
        Vector2 targetAnchoredPosition = default;
        return DOTween.Sequence()
            .AppendCallback(() =>
            {
                if (this == null || _cardContent == null)
                    return;

                CancelTargetFocus();
                KillActiveTween();
                KillFeedbackTween();
                float scaleFactor = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                startAnchoredPosition = _cardContent.anchoredPosition;
                Vector2 screenDelta = targetScreenPosition - GetScreenCenter();
                targetAnchoredPosition = startAnchoredPosition + screenDelta / scaleFactor;
                _canvas.sortingOrder = 1000;
                _cardContent.localEulerAngles = Vector3.zero;
            })
            .Append(DOVirtual.Float(
                    0f,
                    1f,
                    duration,
                    progress =>
                    {
                        if (this != null && _cardContent != null)
                        {
                            _cardContent.anchoredPosition = Vector2.LerpUnclamped(
                                startAnchoredPosition,
                                targetAnchoredPosition,
                                progress);
                        }
                    })
                .SetEase(ease))
            .AppendCallback(() =>
            {
                if (this != null && _cardContent != null)
                    _cardContent.anchoredPosition = targetAnchoredPosition;
            });
    }

    /// <summary>在 cue 开始时从抽牌堆屏幕锚点进入当前权威 base pose，并保持真实手牌可交互。</summary>
    internal Tween CreateIncomingScreenMotionTween(
        Vector2 drawScreenPosition,
        float duration,
        Ease ease,
        Action requestFastForward)
    {
        EnsureReferences();
        if (duration < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (requestFastForward == null)
            throw new ArgumentNullException(nameof(requestFastForward));

        Vector2 startAnchoredPosition = default;
        Vector3 startLocalScale = default;
        return DOTween.Sequence()
            .AppendCallback(() =>
            {
                if (this == null || _cardContent == null)
                    return;

                CancelTargetFocus();
                KillActiveTween();
                KillFeedbackTween();
                ApplyBasePose();
                float scaleFactor = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                Vector2 screenDelta = drawScreenPosition - GetScreenCenter();
                startAnchoredPosition = _basePose.AnchoredPosition + screenDelta / scaleFactor;
                startLocalScale = _baseScale * 0.82f;
                _cardContent.anchoredPosition = startAnchoredPosition;
                _cardContent.localEulerAngles = Vector3.zero;
                _cardContent.localScale = startLocalScale;
                _requestIncomingCardFastForward = requestFastForward;
                _isIncomingCardMotionActive = true;
            })
            .Append(DOVirtual.Float(
                    0f,
                    1f,
                    duration,
                    progress =>
                    {
                        if (this == null || _cardContent == null)
                            return;

                        _cardContent.anchoredPosition = Vector2.LerpUnclamped(
                            startAnchoredPosition,
                            _basePose.AnchoredPosition,
                            progress);
                        float rotation = Mathf.LerpAngle(
                            0f,
                            _basePose.RotationDegrees,
                            progress);
                        _cardContent.localEulerAngles = new Vector3(0f, 0f, rotation);
                        _cardContent.localScale = Vector3.LerpUnclamped(
                            startLocalScale,
                            _baseScale,
                            progress);
                    })
                .SetEase(ease))
            .AppendCallback(FinishIncomingCardMotion);
    }

    /// <summary>结束或取消入场 cue 时清除定向快进入口，并恢复最新权威布局对应的 base pose。</summary>
    internal void FinishIncomingCardMotion()
    {
        _requestIncomingCardFastForward = null;
        _isIncomingCardMotionActive = false;
        if (this != null && _cardContent != null)
            ApplyBasePose();
    }

    /// <summary>只取出一次当前入场 cue 的快进请求；没有活动 cue 时保持无操作。</summary>
    internal bool TryFastForwardIncomingCardMotion()
    {
        if (!_isIncomingCardMotionActive || _requestIncomingCardFastForward == null)
            return false;

        Action request = _requestIncomingCardFastForward;
        _requestIncomingCardFastForward = null;
        request.Invoke();
        return true;
    }

    /// <summary>把既有交互模式投影为独立的禁用灰化、费用提示和系统指针开关。</summary>
    public void SetInteractionPresentation(
        HandCardInteractionMode mode,
        Color insufficientCostColor)
    {
        EnsureReferences();
        EnsureDisabledOverlay();
        switch (mode)
        {
            case HandCardInteractionMode.Disabled:
                _disabledOverlayImage.gameObject.SetActive(true);
                _isPlayerInputEnabled = false;
                SetCostPaymentFeedback(canPayCost: true, insufficientCostColor);
                break;
            case HandCardInteractionMode.VisualOnly:
                _disabledOverlayImage.gameObject.SetActive(false);
                _isPlayerInputEnabled = true;
                SetCostPaymentFeedback(canPayCost: false, insufficientCostColor);
                break;
            case HandCardInteractionMode.Playable:
                _disabledOverlayImage.gameObject.SetActive(false);
                _isPlayerInputEnabled = true;
                SetCostPaymentFeedback(canPayCost: true, insufficientCostColor);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported hand interaction mode.");
        }

        RefreshInteractionState();
    }

    /// <summary>把待定视觉绑定到 Submit 前取得的不透明句柄。</summary>
    public void SetCommandPending(BattleCommandHandle handle)
    {
        if (handle == null)
            throw new ArgumentNullException(nameof(handle));
        if (ReferenceEquals(_pendingCommandHandle, handle))
            return;

        _pendingCommandHandle = handle;
        _hasPlayFeedback = false;
        KillFeedbackTween();
        if (_dragFeedbackCanvasGroup != null)
        {
            _feedbackTween = _dragFeedbackCanvasGroup
                .DOFade(0.58f, 0.1f)
                .SetEase(Ease.OutQuad);
        }

        RefreshInteractionState();
    }

    /// <summary>只在精确句柄仍绑定当前待定视觉时安静清除状态。</summary>
    public void ClearCommandPending(BattleCommandHandle handle)
    {
        if (!ReferenceEquals(_pendingCommandHandle, handle))
            return;

        _pendingCommandHandle = null;
        _hasPlayFeedback = false;
        RefreshInteractionState();
        KillFeedbackTween();
        if (_dragFeedbackCanvasGroup != null)
        {
            _feedbackTween = _dragFeedbackCanvasGroup
                .DOFade(1f, 0.1f)
                .SetEase(Ease.OutQuad);
        }
    }

    /// <summary>只在失败句柄仍绑定当前待定视觉时清除状态并播放反馈。</summary>
    public void PlayCommandFailureFeedback(BattleCommandHandle handle)
    {
        if (!ReferenceEquals(_pendingCommandHandle, handle))
            return;

        _pendingCommandHandle = null;
        _hasPlayFeedback = false;
        RefreshInteractionState();
        KillFeedbackTween();
        if (_dragFeedbackCanvasGroup == null)
            return;

        _feedbackTween = DOTween.Sequence()
            .Append(_dragFeedbackCanvasGroup.DOFade(0.28f, 0.08f).SetEase(Ease.OutQuad))
            .Append(_dragFeedbackCanvasGroup.DOFade(1f, 0.16f).SetEase(Ease.OutQuad));
    }

    /// <summary>
    /// 使用屏幕坐标增量移动卡牌，保持抓取时的相对偏移。
    /// </summary>
    public void FollowPointerDelta(Vector2 screenDelta)
    {
        float scaleFactor = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        _cardContent.anchoredPosition += screenDelta / scaleFactor;
    }

    /// <summary>
    /// 根据是否越过打出线切换拖拽反馈透明度。
    /// </summary>
    public void SetDragPlayFeedback(bool isPastPlayLine)
    {
        if (_dragFeedbackCanvasGroup == null ||
            IsCommandPending ||
            _hasPlayFeedback == isPastPlayLine)
            return;

        _hasPlayFeedback = isPastPlayLine;
        KillFeedbackTween();

        // Self 与 VisualOnly 继续使用独立透明度提示；Enemy 越线后由 focus Tween 接管姿态。
        float targetAlpha = isPastPlayLine ? 0.82f : 1f;
        _feedbackTween = _dragFeedbackCanvasGroup.DOFade(targetAlpha, 0.1f).SetEase(Ease.OutQuad);
    }

    /// <summary>只在明确费用不足时切换费用颜色，可支付或其他失败均恢复 Prefab 原色。</summary>
    public void SetCostPaymentFeedback(bool canPayCost, Color insufficientCostColor)
    {
        EnsureReferences();
        if (!_hasNormalCostColor)
        {
            _normalCostColor = _costText.color;
            _hasNormalCostColor = true;
        }

        _costText.color = canPayCost ? _normalCostColor : insufficientCostColor;
    }

    /// <summary>把当前卡牌中心投影为屏幕坐标，供目标箭头与其他 Canvas 交换位置。</summary>
    public Vector2 GetScreenCenter()
    {
        EnsureReferences();
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;
        Vector3 worldCenter = _cardContent.TransformPoint(_cardContent.rect.center);
        return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
    }

    /// <summary>由玩家阶段与该牌待定状态共同派生实际射线和交互开关。</summary>
    private void RefreshInteractionState()
    {
        if (_dragFeedbackCanvasGroup == null)
            return;

        bool canInteract = _isPlayerInputEnabled && !IsCommandPending;
        _dragFeedbackCanvasGroup.interactable = canInteract;
        _dragFeedbackCanvasGroup.blocksRaycasts = canInteract;
    }

    /// <summary>懒创建独立灰色覆盖层，避免与越线、待定和失败反馈共用透明度通道。</summary>
    private void EnsureDisabledOverlay()
    {
        if (_disabledOverlayImage != null)
            return;

        Transform existing = _cardContent.Find(DisabledOverlayName);
        if (existing != null)
            _disabledOverlayImage = existing.GetComponent<Image>();
        if (_disabledOverlayImage == null)
        {
            var overlayObject = new GameObject(
                DisabledOverlayName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.transform.SetParent(_cardContent, worldPositionStays: false);
            _disabledOverlayImage = overlayObject.GetComponent<Image>();
        }

        RectTransform overlayRect = _disabledOverlayImage.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();
        _disabledOverlayImage.color = DisabledOverlayColor;
        _disabledOverlayImage.raycastTarget = false;
        _disabledOverlayImage.gameObject.SetActive(false);
    }

    /// <summary>在聚焦位姿到达后启动只改变本地缩放的循环呼吸，并归属到该卡唯一 Tween ID。</summary>
    private void StartTargetFocusBreath(
        Vector3 focusLocalScale,
        float breathScale,
        float breathDuration)
    {
        if (this == null || _cardContent == null)
            return;

        DOTween.Kill(_targetFocusBreathTweenId, complete: false);
        _cardContent.localScale = focusLocalScale;
        _targetFocusBreathTween = _cardContent
            .DOScale(focusLocalScale * breathScale, breathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetId(_targetFocusBreathTweenId);
    }

    /// <summary>立即将 RectTransform 写入当前基础姿态。</summary>
    private void ApplyBasePose()
    {
        _canvas.sortingOrder = _basePose.SortingOrder;
        _cardContent.anchoredPosition = _basePose.AnchoredPosition;
        _cardContent.localEulerAngles = new Vector3(0f, 0f, _basePose.RotationDegrees);
        _cardContent.localScale = _baseScale;
    }

    /// <summary>确认预制体已经配置所有必需的展示引用。</summary>
    private void EnsureReferences()
    {
        if (_canvas == null
            || _cardContent == null
            || _titleText == null
            || _costText == null
            || _typeText == null
            || _descriptionText == null
            || _illustrationImage == null)
        {
            throw new InvalidOperationException(
                "HandCardVisual is missing one or more serialized CardView references.");
        }
    }

    /// <summary>让横向牌面保持原始比例覆盖插图区，并由父级 Stencil Mask 裁切溢出部分。</summary>
    private void ApplyIllustration(Sprite illustration)
    {
        if (illustration == null)
            throw new ArgumentNullException(nameof(illustration));

        RectTransform illustrationRect = _illustrationImage.rectTransform;
        RectTransform maskRect = illustrationRect.parent as RectTransform;
        if (maskRect == null)
            throw new InvalidOperationException("Card illustration Image must be a child of a RectTransform mask.");

        Vector2 maskSize = maskRect.rect.size;
        Vector2 spriteSize = illustration.rect.size;
        if (maskSize.x <= 0f || maskSize.y <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
            throw new InvalidOperationException("Card illustration and mask must have non-zero dimensions.");

        float maskAspect = maskSize.x / maskSize.y;
        float spriteAspect = spriteSize.x / spriteSize.y;
        Vector2 coverSize = spriteAspect >= maskAspect
            ? new Vector2(maskSize.y * spriteAspect, maskSize.y)
            : new Vector2(maskSize.x, maskSize.x / spriteAspect);

        _illustrationImage.sprite = illustration;
        _illustrationImage.preserveAspect = true;
        illustrationRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, coverSize.x);
        illustrationRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, coverSize.y);
        illustrationRect.anchoredPosition = Vector2.zero;
    }

    /// <summary>停止并清理当前姿态补间。</summary>
    private void KillActiveTween()
    {
        if (_activeTween != null && _activeTween.IsActive())
            _activeTween.Complete(withCallbacks: false);

        _activeTween = null;
    }

    /// <summary>停止并清理当前拖拽反馈补间。</summary>
    private void KillFeedbackTween()
    {
        if (_feedbackTween != null && _feedbackTween.IsActive())
            _feedbackTween.Complete(withCallbacks: false);

        _feedbackTween = null;
    }

    /// <summary>销毁 View 时回收所有 DOTween 补间。</summary>
    private void OnDestroy()
    {
        CancelTargetFocus();
        KillActiveTween();
        KillFeedbackTween();
    }
}
