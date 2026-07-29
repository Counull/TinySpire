using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

[DisallowMultipleComponent]
public sealed class HandCardContainer : MonoBehaviour
{
    [SerializeField] private GameObject cardViewPrefab;

    [Header("Fan Layout")]
    [SerializeField, Min(0f)] private float baseSpacing = 260f;
    [SerializeField, Min(0f)] private float maxFanAngle = 15f;
    [SerializeField, Min(0f)] private float verticalDrop = 72f;
    [SerializeField, Min(0f)] private float maxHandWidth = 1300f;
    [SerializeField] private float handCenterYOffset = -380;
    [SerializeField, Min(0.01f)] private float cardDisplayScale = 0.36f;

    [Header("Play")]
    [Tooltip("CardContent local Y must be greater than this hand-area value to play the card.")]
    [SerializeField] private float playLineY = -100f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float hoverLift = 100f;
    [SerializeField, Range(1.1f, 1.2f)] private float hoverScale = 1.15f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.15f;
    [SerializeField, Min(0f)] private float reflowDuration = 0.22f;

    private GameConfig _config;

    private readonly List<HandCardVisual> _cards = new();
    private HandCardVisual _draggingCard;
    private HandState _handState;

    private int CurrentHandCount => _handState.CardIds.Count;

    private void Awake()
    {
        if (cardViewPrefab == null)
        {
            Debug.LogError("HandCardContainer is missing its CardView prefab.", this);
            enabled = false;
            return;
        }

        // 从 Bootstrap 根 LifetimeScope 解析运行时配置。
        if (FindFirstObjectByType<Bootstrap>() is { } scope)
            _config = scope.Container.Resolve<ConfigService>().GameConfig;

        int handCount = _config?.InitialHandCount ?? 5;
        _handState = new HandState(handCount);
        _handState.Changed += HandleHandStateChanged;
        RebuildCards(immediate: true);
    }

    private void OnDestroy()
    {
        if (_handState != null)
            _handState.Changed -= HandleHandStateChanged;
    }

    public void HandlePointerEnter(HandCardVisual card)
    {
        if (card == null || _draggingCard != null)
            return;

        card.PlayHover(hoverLift, hoverScale, CurrentHandCount, hoverDuration);
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
        card.BeginDrag(CurrentHandCount + 1);
        LayoutCards(immediate: false, excludedCard: card);
    }

    public void HandleDrag(HandCardVisual card, PointerEventData eventData)
    {
        if (card == null || card != _draggingCard || eventData == null)
            return;

        // Independent root Canvases serialize with a zero-sized RectTransform, so converting absolute screen points can snap to (0, 0).
        // Incremental pointer movement preserves the grab offset and makes the card follow the cursor continuously.
        card.FollowPointerDelta(eventData.delta);
        card.SetDragPlayFeedback(IsPastPlayLine(card));
    }

    public void HandleEndDrag(HandCardVisual card)
    {
        if (card == null || card != _draggingCard)
            return;

        bool shouldPlayCard = IsPastPlayLine(card);
        _draggingCard = null;
        card.SetDragPlayFeedback(false);

        if (shouldPlayCard && _handState.PlayCard(card.CardId))
            return;

        LayoutCards(immediate: false);
    }

    private void HandleHandStateChanged()
    {
        RebuildCards(immediate: false);
    }

    private void RebuildCards(bool immediate)
    {
        IReadOnlyList<int> cardIds = _handState.CardIds;
        for (int index = _cards.Count - 1; index >= 0; index--)
        {
            HandCardVisual card = _cards[index];
            if (ContainsCardId(cardIds, card.CardId))
                continue;

            // TODO(DEP-004): Future card-effect types need distinct pre-destruction actions before this visual is destroyed.
            Destroy(card.gameObject);
            _cards.RemoveAt(index);
        }

        var orderedCards = new List<HandCardVisual>(cardIds.Count);
        foreach (int cardId in cardIds)
        {
            HandCardVisual card = FindCardById(cardId);
            if (card == null)
                card = CreateCard(cardId);

            if (card != null)
                orderedCards.Add(card);
        }

        _cards.Clear();
        _cards.AddRange(orderedCards);
        LayoutCards(immediate);
    }

    private HandCardVisual CreateCard(int cardId)
    {
        Vector3 baseScale = Vector3.one * cardDisplayScale;

        // CardView owns a root Canvas. Keep each runtime card as an independent root Canvas so its serialized root scale and sorting order remain valid.
        GameObject cardObject = Instantiate(cardViewPrefab);
        cardObject.name = $"HandCard_{cardId + 1:00}";

        Canvas cardCanvas = cardObject.GetComponent<Canvas>();
        RectTransform cardContent = cardObject.transform.Find("CardContent") as RectTransform;
        if (cardCanvas == null || cardContent == null)
        {
            Debug.LogError("CardView prefab does not contain the expected Canvas and CardContent objects.", cardObject);
            Destroy(cardObject);
            return null;
        }

        HandCardVisual visual = cardObject.AddComponent<HandCardVisual>();
        CanvasGroup feedbackCanvasGroup = cardContent.gameObject.AddComponent<CanvasGroup>();
        feedbackCanvasGroup.interactable = true;
        feedbackCanvasGroup.blocksRaycasts = true;
        visual.Initialize(cardCanvas, cardContent, baseScale, cardId, feedbackCanvasGroup);

        Image hitTarget = cardContent.gameObject.AddComponent<Image>();
        hitTarget.color = Color.clear;
        hitTarget.raycastTarget = true;

        HandCardInteraction interaction = cardContent.gameObject.AddComponent<HandCardInteraction>();
        interaction.Initialize(this, visual);
        return visual;
    }

    private void LayoutCards(bool immediate, HandCardVisual excludedCard = null)
    {
        int layoutCount = CurrentHandCount - (excludedCard == null ? 0 : 1);
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
                IndexOf(card));

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

    private HandCardVisual FindCardById(int cardId)
    {
        foreach (HandCardVisual card in _cards)
        {
            if (card.CardId == cardId)
                return card;
        }

        return null;
    }

    private static bool ContainsCardId(IReadOnlyList<int> cardIds, int cardId)
    {
        foreach (int candidateCardId in cardIds)
        {
            if (candidateCardId == cardId)
                return true;
        }

        return false;
    }

    private int IndexOf(HandCardVisual card)
    {
        return _cards.IndexOf(card);
    }

    private bool IsPastPlayLine(HandCardVisual card)
    {
        return card.CurrentAnchoredY > playLineY;
    }
}
