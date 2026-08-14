using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class HandCardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private HandCardContainer _container;
    private HandCardVisual _card;

    /// <summary>绑定唯一的手牌容器与当前卡牌 View。</summary>
    public void Initialize(HandCardContainer container, HandCardVisual card)
    {
        _container = container;
        _card = card;
    }

    /// <summary>把指针进入事件转交给手牌容器。</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _container?.HandlePointerEnter(_card);
    }

    /// <summary>把指针离开事件转交给手牌容器。</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _container?.HandlePointerExit(_card);
    }

    /// <summary>把手牌点击事件原样转交给容器统一解析活动选牌会话。</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        _container?.HandlePointerClick(_card, eventData);
    }

    /// <summary>把开始拖拽事件转交给手牌容器。</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        _container?.HandleBeginDrag(_card);
    }

    /// <summary>把拖拽增量与屏幕位置转交给手牌容器。</summary>
    public void OnDrag(PointerEventData eventData)
    {
        _container?.HandleDrag(_card, eventData);
    }

    /// <summary>把松手时的最终屏幕位置交给容器完成目标命中。</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        _container?.HandleEndDrag(_card, eventData);
    }
}
