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

    private CanvasGroup _dragFeedbackCanvasGroup;
    private Vector3 _baseScale;
    private HandCardPose _basePose;
    private Tween _activeTween;
    private Tween _feedbackTween;
    private bool _hasPlayFeedback;

    public CardInstanceId CardId { get; private set; }
    public float CurrentAnchoredY => _cardContent.anchoredPosition.y;
    public RectTransform CardContent => _cardContent;

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
        _canvas.overrideSorting = true;
    }

    /// <summary>
    /// 用当前格式化文本和费用刷新卡牌展示；不保存战斗事实。
    /// </summary>
    public void Bind(CardPresentationText presentation, int cost)
    {
        EnsureReferences();
        _titleText.text = presentation.Name;
        _costText.text = cost.ToString();
        _typeText.text = string.Empty;
        _descriptionText.text = presentation.Description;
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
        if (_dragFeedbackCanvasGroup == null || _hasPlayFeedback == isPastPlayLine)
            return;

        _hasPlayFeedback = isPastPlayLine;
        KillFeedbackTween();

        // TODO(DEP-003): Final drag-over-play-line visual style requires design/art confirmation.
        float targetAlpha = isPastPlayLine ? 0.82f : 1f;
        _feedbackTween = _dragFeedbackCanvasGroup.DOFade(targetAlpha, 0.1f).SetEase(Ease.OutQuad);
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
            || _descriptionText == null)
        {
            throw new InvalidOperationException(
                "HandCardVisual is missing one or more serialized CardView references.");
        }
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
