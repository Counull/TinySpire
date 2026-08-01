using System;
using DG.Tweening;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HandCardVisual : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _cardContent;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _costText;
    [SerializeField] private Text _typeText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Image _illustrationImage;

    private CanvasGroup _dragFeedbackCanvasGroup;
    private Vector3 _baseScale;
    private HandCardPose _basePose;
    private Tween _activeTween;
    private Tween _feedbackTween;
    private bool _hasPlayFeedback;
    private bool _isPlayerInputEnabled = true;
    private long? _pendingCommandSequence;
    private Color _normalCostColor;
    private bool _hasNormalCostColor;

    public CardInstanceId CardId { get; private set; }
    public float CurrentAnchoredY => _cardContent.anchoredPosition.y;
    public RectTransform CardContent => _cardContent;
    public bool IsCommandPending => _pendingCommandSequence.HasValue;

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
        KillActiveTween();
        SetDragPlayFeedback(false);
        _canvas.sortingOrder = elevatedSortingOrder;
        _cardContent.localEulerAngles = Vector3.zero;
        _cardContent.localScale = _baseScale;
    }

    /// <summary>按当前玩家阶段启用或锁定卡牌输入，不改变任何卡区事实。</summary>
    public void SetPlayerInputEnabled(bool isEnabled)
    {
        _isPlayerInputEnabled = isEnabled;
        RefreshInteractionState();
    }

    /// <summary>把待定视觉绑定到权威序号；空值表示当前没有待定出牌。</summary>
    public void SetCommandPending(long? authoritySequence)
    {
        if (_pendingCommandSequence == authoritySequence)
            return;

        _pendingCommandSequence = authoritySequence;
        _hasPlayFeedback = false;
        KillFeedbackTween();
        if (_dragFeedbackCanvasGroup != null)
        {
            _feedbackTween = _dragFeedbackCanvasGroup
                .DOFade(authoritySequence.HasValue ? 0.58f : 1f, 0.1f)
                .SetEase(Ease.OutQuad);
        }

        RefreshInteractionState();
    }

    /// <summary>只在失败序号仍绑定当前待定视觉时清除状态并播放反馈。</summary>
    public void PlayCommandFailureFeedback(long authoritySequence)
    {
        if (_pendingCommandSequence != authoritySequence)
            return;

        _pendingCommandSequence = null;
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

        // TODO(DEP-003): Final drag-over-play-line visual style requires design/art confirmation.
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
            _activeTween.Kill();

        _activeTween = null;
    }

    /// <summary>停止并清理当前拖拽反馈补间。</summary>
    private void KillFeedbackTween()
    {
        if (_feedbackTween != null && _feedbackTween.IsActive())
            _feedbackTween.Kill();

        _feedbackTween = null;
    }

    /// <summary>销毁 View 时回收所有 DOTween 补间。</summary>
    private void OnDestroy()
    {
        KillActiveTween();
        KillFeedbackTween();
    }
}
