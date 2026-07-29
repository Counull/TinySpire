using System.Collections.Generic;
using DG.Tweening;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

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

    private BattleSession _session;
    private ConfigService _configs;

    private readonly List<HandCardVisual> _cards = new();
    private HandCardVisual _draggingCard;
    private CardZoneState _cardZones;

    private int CurrentHandCount => _cardZones.Hand.Count;

    [Inject]
    public void Construct(BattleSession session, ConfigService configs)
    {
        _session = session;
        _configs = configs;
    }

    private void Start()
    {
        if (cardViewPrefab == null)
        {
            Debug.LogError("HandCardContainer is missing its CardView prefab.", this);
            enabled = false;
            return;
        }

        if (_session == null || _configs?.Tables == null)
        {
            Debug.LogError("HandCardContainer did not receive the initialized battle session.", this);
            enabled = false;
            return;
        }

        _cardZones = _session.CardZones;
        _cardZones.Changed += HandleCardZonesChanged;
        RebuildCards(immediate: true);
    }

    private void OnDestroy()
    {
        if (_cardZones != null)
            _cardZones.Changed -= HandleCardZonesChanged;
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

        // TODO(DEP-001): Resolve a legal target before moving an aimed card out of the hand.
        // TODO(DEP-002): Validate and spend card energy before committing the zone move.
        if (shouldPlayCard && _cardZones.DiscardFromHand(card.CardId))
            return;

        LayoutCards(immediate: false);
    }

    private void HandleCardZonesChanged()
    {
        RebuildCards(immediate: false);
    }

    private void RebuildCards(bool immediate)
    {
        IReadOnlyList<CardInstanceId> hand = _cardZones.Hand;
        for (int index = _cards.Count - 1; index >= 0; index--)
        {
            HandCardVisual card = _cards[index];
            if (ContainsCardId(hand, card.CardId))
                continue;

            // TODO(DEP-004): Future card-effect types need distinct pre-destruction actions before this visual is destroyed.
            Destroy(card.gameObject);
            _cards.RemoveAt(index);
        }

        var orderedCards = new List<HandCardVisual>(hand.Count);
        foreach (CardInstanceId cardId in hand)
        {
            CardInstanceState cardState = _cardZones.Cards[cardId];
            HandCardVisual card = FindCardById(cardState.Id);
            if (card == null)
                card = CreateCard(cardState);

            if (card != null)
                orderedCards.Add(card);
        }

        _cards.Clear();
        _cards.AddRange(orderedCards);
        LayoutCards(immediate);
    }

    private HandCardVisual CreateCard(CardInstanceState cardState)
    {
        Vector3 baseScale = Vector3.one * cardDisplayScale;

        // CardView owns a root Canvas. Keep each runtime card as an independent root Canvas so its serialized root scale and sorting order remain valid.
        GameObject cardObject = Instantiate(cardViewPrefab);
        cardObject.name = $"HandCard_{cardState.Id.Value:00}_Template_{cardState.TemplateId}";

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
        visual.Initialize(
            cardCanvas,
            cardContent,
            baseScale,
            cardState.Id,
            feedbackCanvasGroup);
        BindCardTemplate(cardObject, cardState.TemplateId);

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

    private void BindCardTemplate(GameObject cardObject, int cardTemplateId)
    {
        cfg.battle.Card cardTemplate = _configs.Tables.TbCard.Get(cardTemplateId);
        foreach (Text label in cardObject.GetComponentsInChildren<Text>(includeInactive: true))
        {
            switch (label.name)
            {
                case "TitleText":
                    label.text = cardTemplate.Name;
                    break;
                case "CostText":
                    label.text = cardTemplate.Cost.ToString();
                    break;
                case "TypeText":
                case "DescriptionText":
                    // Effect display text belongs to the later card-presentation/effect-description slice.
                    label.text = string.Empty;
                    break;
            }
        }
    }

    private HandCardVisual FindCardById(CardInstanceId cardId)
    {
        foreach (HandCardVisual card in _cards)
        {
            if (card.CardId == cardId)
                return card;
        }

        return null;
    }

    private static bool ContainsCardId(IReadOnlyList<CardInstanceId> cards, CardInstanceId cardId)
    {
        foreach (CardInstanceId candidate in cards)
        {
            if (candidate == cardId)
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
