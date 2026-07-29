using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HandCardVisual : MonoBehaviour
{
    private RectTransform _cardContent;
    private Canvas _canvas;
    private Vector3 _baseScale;
    private HandCardPose _basePose;
    private Tween _activeTween;

    public void Initialize(Canvas canvas, RectTransform cardContent, Vector3 baseScale)
    {
        _canvas = canvas;
        _cardContent = cardContent;
        _baseScale = baseScale;
        _canvas.overrideSorting = true;
    }

    public void SetBasePoseImmediately(HandCardPose pose)
    {
        KillActiveTween();
        _basePose = pose;
        ApplyBasePose();
    }

    public void SetBasePose(HandCardPose pose)
    {
        _basePose = pose;
    }

    public void PlayBasePose(float duration, Ease ease)
    {
        KillActiveTween();
        _canvas.sortingOrder = _basePose.SortingOrder;
        _activeTween = DOTween.Sequence()
            .Join(_cardContent.DOAnchorPos(_basePose.AnchoredPosition, duration).SetEase(ease))
            .Join(_cardContent.DOLocalRotate(new Vector3(0f, 0f, _basePose.RotationDegrees), duration).SetEase(ease))
            .Join(_cardContent.DOScale(_baseScale, duration).SetEase(ease));
    }

    public void PlayHover(float lift, float scaleMultiplier, int elevatedSortingOrder, float duration)
    {
        KillActiveTween();
        _canvas.sortingOrder = elevatedSortingOrder;
        _activeTween = DOTween.Sequence()
            .Join(_cardContent.DOAnchorPos(_basePose.AnchoredPosition + Vector2.up * lift, duration).SetEase(Ease.OutBack))
            .Join(_cardContent.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.OutBack))
            .Join(_cardContent.DOScale(_baseScale * scaleMultiplier, duration).SetEase(Ease.OutBack));
    }

    public void BeginDrag(int elevatedSortingOrder)
    {
        KillActiveTween();
        _canvas.sortingOrder = elevatedSortingOrder;
        _cardContent.localEulerAngles = Vector3.zero;
        _cardContent.localScale = _baseScale;
    }

    public void FollowPointer(Vector2 anchoredPosition)
    {
        _cardContent.anchoredPosition = anchoredPosition;
    }

    private void ApplyBasePose()
    {
        _canvas.sortingOrder = _basePose.SortingOrder;
        _cardContent.anchoredPosition = _basePose.AnchoredPosition;
        _cardContent.localEulerAngles = new Vector3(0f, 0f, _basePose.RotationDegrees);
        _cardContent.localScale = _baseScale;
    }

    private void KillActiveTween()
    {
        if (_activeTween != null && _activeTween.IsActive())
            _activeTween.Kill();

        _activeTween = null;
    }

    private void OnDestroy()
    {
        KillActiveTween();
    }
}
