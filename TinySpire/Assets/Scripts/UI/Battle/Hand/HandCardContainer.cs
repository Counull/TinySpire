using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
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
    private CardTextFormatter _cardTextFormatter;
    private LocalizationService _localization;

    private readonly List<HandCardVisual> _cards = new();
    private readonly Dictionary<int, AsyncOperationHandle<Sprite>> _illustrationHandles = new();
    private HandCardVisual _draggingCard;
    private BattleCardZonesData _cardZones;
    private PlayerCombatantData _player;
    private bool _isDestroyed;

    private int CurrentHandCount => _cardZones.Hand.Count;

    /// <summary>
    /// 接收已初始化的战斗会话及文本依赖，并在 Start 中订阅所需的运行时事实。
    /// </summary>
    [Inject]
    public void Construct(
        BattleSession session,
        ConfigService configs,
        CardTextFormatter cardTextFormatter,
        LocalizationService localization)
    {
        _session = session;
        _configs = configs;
        _cardTextFormatter = cardTextFormatter;
        _localization = localization;
    }

    /// <summary>校验依赖、建立事实订阅并绘制初始手牌。</summary>
    private async void Start()
    {
        if (cardViewPrefab == null)
        {
            Debug.LogError("HandCardContainer is missing its CardView prefab.", this);
            enabled = false;
            return;
        }

        if (_session == null
            || _configs?.Tables == null
            || _cardTextFormatter == null
            || _localization == null)
        {
            Debug.LogError("HandCardContainer did not receive the initialized battle session.", this);
            enabled = false;
            return;
        }

        _cardZones = _session.CardZones;
        _player = ResolvePlayer();
        try
        {
            await LoadCardIllustrationsAsync();
        }
        catch (Exception exception)
        {
            ReleaseCardIllustrations();
            if (!_isDestroyed)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }

            return;
        }

        if (_isDestroyed)
            return;

        _cardZones.Layout
            .Select(layout => layout.Hand)
            .Skip(1)
            .Subscribe(_ => RebuildCards(immediate: false))
            .AddTo(this);
        _player.Strength
            .Skip(1)
            .Subscribe(_ => RefreshCardTexts())
            .AddTo(this);
        _localization.LocaleChanged.Subscribe(_ => RefreshCardTexts()).AddTo(this);
        RebuildCards(immediate: true);
    }

    /// <summary>
    /// 处理手牌悬停进入，播放抬升反馈。
    /// </summary>
    public void HandlePointerEnter(HandCardVisual card)
    {
        if (card == null || _draggingCard != null)
            return;

        card.PlayHover(hoverLift, hoverScale, CurrentHandCount, hoverDuration);
    }

    /// <summary>
    /// 处理手牌悬停离开，恢复该牌的基础姿态。
    /// </summary>
    public void HandlePointerExit(HandCardVisual card)
    {
        if (card == null || card == _draggingCard)
            return;

        card.PlayBasePose(hoverDuration, Ease.OutBack);
    }

    /// <summary>
    /// 开始拖拽一张手牌，并使其脱离当前手牌排布。
    /// </summary>
    public void HandleBeginDrag(HandCardVisual card)
    {
        if (card == null || _draggingCard != null)
            return;

        _draggingCard = card;
        card.BeginDrag(CurrentHandCount + 1);
        LayoutCards(immediate: false, excludedCard: card);
    }

    /// <summary>
    /// 根据指针增量移动正在拖拽的卡牌，并刷新越过打出线的视觉反馈。
    /// </summary>
    public void HandleDrag(HandCardVisual card, PointerEventData eventData)
    {
        if (card == null || card != _draggingCard || eventData == null)
            return;

        // Independent root Canvases serialize with a zero-sized RectTransform, so converting absolute screen points can snap to (0, 0).
        // Incremental pointer movement preserves the grab offset and makes the card follow the cursor continuously.
        card.FollowPointerDelta(eventData.delta);
        card.SetDragPlayFeedback(IsPastPlayLine(card));
    }

    /// <summary>
    /// 结束拖拽：越过打出线则请求卡区弃置该实例，否则恢复手牌排布。
    /// </summary>
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

    /// <summary>根据当前手牌布局增删并排序 CardView。</summary>
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
            CardInstanceData cardState = _cardZones.Cards[cardId];
            HandCardVisual card = FindCardById(cardState.Id);
            if (card == null)
                card = CreateCard(cardState);
            else
                BindCardPresentation(card, cardState);

            if (card != null)
                orderedCards.Add(card);
        }

        _cards.Clear();
        _cards.AddRange(orderedCards);
        LayoutCards(immediate);
    }

    /// <summary>从预制体创建一个与卡牌实例绑定的视觉对象。</summary>
    private HandCardVisual CreateCard(CardInstanceData cardState)
    {
        Vector3 baseScale = Vector3.one * cardDisplayScale;

        // CardView owns a root Canvas. Keep each runtime card as an independent root Canvas so its serialized root scale and sorting order remain valid.
        GameObject cardObject = Instantiate(cardViewPrefab);
        cardObject.name = $"HandCard_{cardState.Id.Value:00}_Template_{cardState.TemplateId}";

        HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
        if (visual == null || visual.CardContent == null)
        {
            Debug.LogError("CardView prefab does not contain a configured HandCardVisual.", cardObject);
            Destroy(cardObject);
            return null;
        }

        RectTransform cardContent = visual.CardContent;
        CanvasGroup feedbackCanvasGroup = cardContent.gameObject.AddComponent<CanvasGroup>();
        feedbackCanvasGroup.interactable = true;
        feedbackCanvasGroup.blocksRaycasts = true;
        visual.Initialize(
            baseScale,
            cardState.Id,
            feedbackCanvasGroup);
        BindCardPresentation(visual, cardState);

        Image hitTarget = cardContent.gameObject.AddComponent<Image>();
        hitTarget.color = Color.clear;
        hitTarget.raycastTarget = true;

        HandCardInteraction interaction = cardContent.gameObject.AddComponent<HandCardInteraction>();
        interaction.Initialize(this, visual);
        return visual;
    }

    /// <summary>按扇形布局计算并应用当前全部手牌的基础姿态。</summary>
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

    /// <summary>以当前语言和玩家事实重刷已有卡牌的展示文本。</summary>
    private void RefreshCardTexts()
    {
        foreach (HandCardVisual card in _cards)
        {
            if (_cardZones.TryGetCard(card.CardId, out CardInstanceData cardState))
                BindCardPresentation(card, cardState);
        }
    }

    /// <summary>将一个卡牌实例的当前展示文本与费用写入其视觉对象。</summary>
    private void BindCardPresentation(
        HandCardVisual visual,
        CardInstanceData cardState)
    {
        cfg.battle.Card cardTemplate = _configs.Tables.TbCard.Get(cardState.TemplateId);
        if (!_illustrationHandles.TryGetValue(cardState.TemplateId, out AsyncOperationHandle<Sprite> handle)
            || !handle.IsValid()
            || handle.Status != AsyncOperationStatus.Succeeded
            || handle.Result == null)
        {
            throw new InvalidOperationException(
                $"Card template {cardState.TemplateId} illustration is not loaded.");
        }

        CardPresentationText text = _cardTextFormatter.Format(cardState, _player);
        visual.Bind(text, cardTemplate.Cost, handle.Result);
    }

    /// <summary>按当前牌组中的唯一模板预加载牌面 Sprite，并持有其 Addressables 句柄。</summary>
    private async UniTask LoadCardIllustrationsAsync()
    {
        foreach (CardInstanceData cardState in _cardZones.Cards.Values)
        {
            int templateId = cardState.TemplateId;
            if (_illustrationHandles.ContainsKey(templateId))
                continue;

            cfg.battle.Card cardTemplate = _configs.Tables.TbCard.GetOrDefault(templateId)
                ?? throw new InvalidOperationException($"Card template {templateId} does not exist.");
            string illustrationKey = cardTemplate.IllustrationKey;
            if (string.IsNullOrWhiteSpace(illustrationKey))
                throw new InvalidOperationException($"Card template {templateId} has no illustration_key.");

            string address = CardIllustrationAddress.FromKey(illustrationKey);

            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(address);
            try
            {
                await handle.Task;
            }
            catch (Exception exception)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                throw new InvalidOperationException(
                    $"Failed to load card template {templateId} illustration '{address}'.",
                    exception);
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Exception operationException = handle.OperationException;
                if (handle.IsValid())
                    Addressables.Release(handle);

                throw new InvalidOperationException(
                    $"Failed to load card template {templateId} illustration '{address}'.",
                    operationException);
            }
            if (_isDestroyed)
            {
                Addressables.Release(handle);
                return;
            }

            _illustrationHandles.Add(templateId, handle);
        }
    }

    /// <summary>释放本容器持有的全部牌面 Addressables 句柄。</summary>
    private void ReleaseCardIllustrations()
    {
        foreach (AsyncOperationHandle<Sprite> handle in _illustrationHandles.Values)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        _illustrationHandles.Clear();
    }

    /// <summary>从战斗参与者唯一事实映射中取得本场玩家。</summary>
    private PlayerCombatantData ResolvePlayer()
    {
        foreach (CombatantData combatant in _session.Combatants.All.Values)
        {
            if (combatant is PlayerCombatantData player)
                return player;
        }

        throw new System.InvalidOperationException("Battle session does not contain a player combatant.");
    }

    /// <summary>在已创建的视觉对象中查找对应卡牌实例。</summary>
    private HandCardVisual FindCardById(CardInstanceId cardId)
    {
        foreach (HandCardVisual card in _cards)
        {
            if (card.CardId == cardId)
                return card;
        }

        return null;
    }

    /// <summary>判断一个卡牌实例是否仍属于指定卡区快照。</summary>
    private static bool ContainsCardId(IReadOnlyList<CardInstanceId> cards, CardInstanceId cardId)
    {
        foreach (CardInstanceId candidate in cards)
        {
            if (candidate == cardId)
                return true;
        }

        return false;
    }

    /// <summary>获取一个手牌视觉对象在当前显示顺序中的位置。</summary>
    private int IndexOf(HandCardVisual card)
    {
        return _cards.IndexOf(card);
    }

    /// <summary>判断拖拽卡牌是否已经越过配置的打出线。</summary>
    private bool IsPastPlayLine(HandCardVisual card)
    {
        return card.CurrentAnchoredY > playLineY;
    }

    /// <summary>容器销毁时停止异步落地，并释放其持有的牌面资源。</summary>
    private void OnDestroy()
    {
        _isDestroyed = true;
        ReleaseCardIllustrations();
    }
}
