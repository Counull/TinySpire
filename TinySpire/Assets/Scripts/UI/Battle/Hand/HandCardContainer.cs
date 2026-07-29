using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HandCardContainer : MonoBehaviour
{
    [SerializeField] private GameObject cardViewPrefab;

    // Temporary placeholder: Luban-driven card data will replace only this value source; layout, hover, and drag logic must remain unchanged.
    [SerializeField, Min(0)] private int handCount = 5;

    [Header("Fan Layout")]
    [SerializeField, Min(0f)] private float baseSpacing = 260f;
    [SerializeField, Min(0f)] private float maxFanAngle = 15f;
    [SerializeField, Min(0f)] private float verticalDrop = 72f;
    [SerializeField, Min(0f)] private float maxHandWidth = 1300f;
    [SerializeField] private float handCenterYOffset = -240f;
    [SerializeField, Min(0.01f)] private float cardDisplayScale = 0.36f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float hoverLift = 100f;
    [SerializeField, Range(1.1f, 1.2f)] private float hoverScale = 1.15f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.15f;
    [SerializeField, Min(0f)] private float reflowDuration = 0.22f;

    private readonly List<HandCardVisual> _cards = new();
    private Canvas _handCanvas;
    private RectTransform _handArea;
    private HandCardVisual _draggingCard;

    private void Awake()
    {
        _handCanvas = GetComponentInParent<Canvas>();
        _handArea = transform as RectTransform;

        if (cardViewPrefab == null || _handCanvas == null || _handArea == null)
        {
            Debug.LogError("HandCardContainer is missing its CardView prefab or Canvas setup.", this);
            enabled = false;
            return;
        }

        CreateCards();
        LayoutCards(immediate: true);
    }

    public void HandlePointerEnter(HandCardVisual card)
    {
        if (card == null || _draggingCard != null)
            return;

        card.PlayHover(hoverLift, hoverScale, _cards.Count, hoverDuration);
    }

    public void HandlePointerExit(HandCardVisual card)
    {
        if (card == null || card == _draggingCard)
            return;

        card.PlayBasePose(hoverDuration, Ease.OutBack);
    }

    public void HandleBeginDrag(HandCardVisual card)
    {
        if (card == null || _draggingCard != null)
            return;

        _draggingCard = card;
        card.BeginDrag(_cards.Count + 1);
        LayoutCards(immediate: false, excludedCard: card);
    }

    public void HandleDrag(HandCardVisual card, PointerEventData eventData)
    {
        if (card == null || card != _draggingCard || eventData == null)
            return;

        Camera eventCamera = _handCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _handCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_handArea, eventData.position, eventCamera, out Vector2 localPosition))
            card.FollowPointer(localPosition - _handArea.rect.center);
    }

    public void HandleEndDrag(HandCardVisual card)
    {
        if (card == null || card != _draggingCard)
            return;

        _draggingCard = null;
        LayoutCards(immediate: false);
    }

    private void CreateCards()
    {
        Vector3 baseScale = Vector3.one * cardDisplayScale;
        for (int index = 0; index < handCount; index++)
        {
            // CardView owns a root Canvas. Keep each runtime card as an independent root Canvas so its serialized root scale and sorting order remain valid.
            GameObject cardObject = Instantiate(cardViewPrefab);
            cardObject.name = $"HandCard_{index + 1:00}";

            Canvas cardCanvas = cardObject.GetComponent<Canvas>();
            RectTransform cardContent = cardObject.transform.Find("CardContent") as RectTransform;
            if (cardCanvas == null || cardContent == null)
            {
                Debug.LogError("CardView prefab does not contain the expected Canvas and CardContent objects.", cardObject);
                Destroy(cardObject);
                continue;
            }

            HandCardVisual visual = cardObject.AddComponent<HandCardVisual>();
            visual.Initialize(cardCanvas, cardContent, baseScale);

            Image hitTarget = cardContent.gameObject.AddComponent<Image>();
            hitTarget.color = Color.clear;
            hitTarget.raycastTarget = true;

            HandCardInteraction interaction = cardContent.gameObject.AddComponent<HandCardInteraction>();
            interaction.Initialize(this, visual);
            _cards.Add(visual);
        }
    }

    private void LayoutCards(bool immediate, HandCardVisual excludedCard = null)
    {
        int layoutCount = _cards.Count - (excludedCard == null ? 0 : 1);
        HandCardPose[] poses = HandCardLayout.Calculate(
            layoutCount,
            new HandCardLayoutSettings(baseSpacing, maxFanAngle, verticalDrop, maxHandWidth));

        int layoutIndex = 0;
        foreach (HandCardVisual card in _cards)
        {
            if (card == excludedCard)
                continue;

            HandCardPose fanPose = poses[layoutIndex];
            var pose = new HandCardPose(
                fanPose.AnchoredPosition + Vector2.up * handCenterYOffset,
                fanPose.RotationDegrees,
                card == excludedCard ? fanPose.SortingOrder : IndexOf(card));

            if (immediate)
                card.SetBasePoseImmediately(pose);
            else
            {
                card.SetBasePose(pose);
                card.PlayBasePose(reflowDuration, Ease.OutCubic);
            }

            layoutIndex++;
        }
    }

    private int IndexOf(HandCardVisual card)
    {
        return _cards.IndexOf(card);
    }
}
