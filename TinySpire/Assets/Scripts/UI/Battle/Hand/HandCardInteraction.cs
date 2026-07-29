using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class HandCardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private HandCardContainer _container;
    private HandCardVisual _card;

    public void Initialize(HandCardContainer container, HandCardVisual card)
    {
        _container = container;
        _card = card;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _container?.HandlePointerEnter(_card);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _container?.HandlePointerExit(_card);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _container?.HandleBeginDrag(_card);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _container?.HandleDrag(_card, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _container?.HandleEndDrag(_card);
    }
}
