using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;
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

    [Header("Targeting")]
    [SerializeField] private BattleTargetingArrowView _targetingArrow;
    [SerializeField] private Color _insufficientCostColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Header("Animation")]
    [SerializeField, Min(0f)] private float hoverLift = 100f;
    [SerializeField, Range(1.1f, 1.2f)] private float hoverScale = 1.15f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.15f;
    [SerializeField, Min(0f)] private float reflowDuration = 0.22f;

    private BattleSession _session;
    private ConfigService _configs;
    private CardTextFormatter _cardTextFormatter;
    private LocalizationService _localization;
    private BattleCommandQueue _commandQueue;
    private BattleCommandPresentationAdapter _commandPresentation;
    private BattleParticipantPresenter _participantPresenter;
    private BattleCardPlayRules _cardPlayRules;

    private readonly List<HandCardVisual> _cards = new();
    private readonly Dictionary<int, AsyncOperationHandle<Sprite>> _illustrationHandles = new();
    private readonly Dictionary<long, CardInstanceId> _pendingPlayCards = new();
    private HandCardVisual _draggingCard;
    private BattleCardZonesData _cardZones;
    private PlayerCombatantData _player;
    private bool _isDestroyed;
    private HandCardDragPhase _dragPhase;
    private Vector2 _lastPointerScreenPosition;
    private bool _hasPointerScreenPosition;

    private int CurrentHandCount => _cardZones.Hand.Count;

    /// <summary>
    /// 接收已初始化的战斗会话及文本依赖，并在 Start 中订阅所需的运行时事实。
    /// </summary>
    [Inject]
    public void Construct(
        BattleSession session,
        ConfigService configs,
        CardTextFormatter cardTextFormatter,
        LocalizationService localization,
        BattleCommandQueue commandQueue,
        BattleCommandPresentationAdapter commandPresentation,
        BattleParticipantPresenter participantPresenter)
    {
        _session = session;
        _configs = configs;
        _cardTextFormatter = cardTextFormatter;
        _localization = localization;
        _commandQueue = commandQueue;
        _commandPresentation = commandPresentation;
        _participantPresenter = participantPresenter;
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
            || _localization == null
            || _commandQueue == null
            || _commandPresentation == null
            || _participantPresenter == null
            || _targetingArrow == null)
        {
            Debug.LogError("HandCardContainer did not receive the initialized battle session.", this);
            enabled = false;
            return;
        }

        _cardZones = _session.CardZones;
        _player = ResolvePlayer();
        var playerCardZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [_player.Id] = _cardZones
        };
        _cardPlayRules = new BattleCardPlayRules(
            _session.Combatants,
            playerCardZones,
            _session.EnemyCombatantIdsInEncounterOrder,
            _configs.Tables);
        _targetingArrow.PrepareAsScreenOverlay();
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
        _commandQueue.Turn.Subscribe(HandleTurnChanged).AddTo(this);
        _commandPresentation.Feedback.Subscribe(HandleCommandFeedback).AddTo(this);
        foreach (CombatantData combatant in _session.Combatants.All.Values)
        {
            combatant.Health
                .Skip(1)
                .Subscribe(HandleCombatantHealthChanged)
                .AddTo(this);
        }

        RebuildCards(immediate: true);
    }

    /// <summary>
    /// 处理手牌悬停进入，播放抬升反馈。
    /// </summary>
    public void HandlePointerEnter(HandCardVisual card)
    {
        if (!CanInteractWithCard(card) || _draggingCard != null)
            return;

        card.PlayHover(hoverLift, hoverScale, CurrentHandCount, hoverDuration);
    }

    /// <summary>
    /// 处理手牌悬停离开，恢复该牌的基础姿态。
    /// </summary>
    public void HandlePointerExit(HandCardVisual card)
    {
        if (!CanInteractWithCard(card) || card == _draggingCard)
            return;

        card.PlayBasePose(hoverDuration, Ease.OutBack);
    }

    /// <summary>
    /// 开始拖拽一张手牌，并使其脱离当前手牌排布。
    /// </summary>
    public void HandleBeginDrag(HandCardVisual card)
    {
        if (!CanInteractWithCard(card) || _draggingCard != null)
            return;

        _draggingCard = card;
        _dragPhase = HandCardDragPhase.Dragging;
        _hasPointerScreenPosition = false;
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

        _lastPointerScreenPosition = eventData.position;
        _hasPointerScreenPosition = true;

        if (_dragPhase == HandCardDragPhase.EnemyTargeting)
        {
            UpdateEnemyTargeting(card, eventData.position);
            return;
        }

        // 独立根 Canvas 的 RectTransform 尺寸为零，绝对屏幕点会让卡牌跳到原点；继续只累加指针增量。
        card.FollowPointerDelta(eventData.delta);
        BattleCardPlayEvaluation evaluation = EvaluateCard(card, targetId: null);
        bool isPastPlayLine = IsPastPlayLine(card);
        card.SetDragPlayFeedback(isPastPlayLine && evaluation.CanStartInteraction);
        if (isPastPlayLine
            && evaluation.CanStartInteraction
            && evaluation.TargetRule == cfg.battle.TargetRule.Enemy)
        {
            EnterEnemyTargeting(card, evaluation, eventData.position);
        }
    }

    /// <summary>
    /// 结束拖拽：越过打出线时只提交出牌命令，否则恢复手牌排布。
    /// </summary>
    public void HandleEndDrag(HandCardVisual card, PointerEventData eventData)
    {
        if (card == null || card != _draggingCard)
            return;

        BattleCardPlayEvaluation evaluation = EvaluateCard(card, targetId: null);
        CombatantId? hoveredTargetId = null;
        if (evaluation.TargetRule == cfg.battle.TargetRule.Enemy
            && IsPastPlayLine(card)
            && evaluation.CanStartInteraction
            && eventData != null)
        {
            if (_dragPhase != HandCardDragPhase.EnemyTargeting)
                EnterEnemyTargeting(card, evaluation, eventData.position);

            hoveredTargetId = _participantPresenter.UpdateTargetSelection(eventData.position);
        }

        CombatantId? targetId = HandCardReleaseTargetResolver.Resolve(
            IsPastPlayLine(card),
            evaluation.TargetRule,
            evaluation.CanStartInteraction,
            _player.Id,
            hoveredTargetId,
            evaluation.LegalTargetIds);

        // Submit 可能同步发布 Turn 与 CardZones；必须先清空拖拽/瞄准瞬时表现，再进入权威队列。
        _draggingCard = null;
        _dragPhase = HandCardDragPhase.Idle;
        _hasPointerScreenPosition = false;
        card.SetDragPlayFeedback(false);
        ClearTargetingPresentation();

        if (targetId.HasValue)
        {
            BattleCardPlayEvaluation finalEvaluation = EvaluateCard(card, targetId.Value);
            if (finalEvaluation.Succeeded)
            {
                SubmitPlayCard(card, targetId.Value);
                return;
            }
        }

        LayoutCards(immediate: false);
        RefreshCardPlayPresentation();
    }

    /// <summary>把拖拽意图与显式目标提交为 PlayCardCommand，并只记录该序号的待定展示关系。</summary>
    private void SubmitPlayCard(HandCardVisual card, CombatantId targetId)
    {
        CardInstanceId cardId = card.CardId;
        var command = new PlayCardCommand(_player.Id, cardId, targetId);
        BattleCardPlayEvaluation evaluation = _cardPlayRules.Evaluate(
            _commandQueue.Turn.CurrentValue,
            command);
        if (!evaluation.Succeeded)
        {
            LayoutCards(immediate: false);
            RefreshCardPlayPresentation();
            return;
        }

        BattleCommandSubmissionResult submission = _commandQueue.Submit(command);
        if (!submission.Accepted || !submission.AuthoritySequence.HasValue)
        {
            if (card != null)
                card.SetCommandPending(null);
            LayoutCards(immediate: false);
            RefreshCardPlayPresentation();
            return;
        }

        long authoritySequence = submission.AuthoritySequence.Value;
        _pendingPlayCards.Add(authoritySequence, cardId);
        card.SetCommandPending(authoritySequence);
        _commandPresentation.PublishQueued(command, submission);
        LayoutCards(immediate: false);
    }

    /// <summary>执行失败时恢复对应卡牌交互；成功时只清除展示追踪，卡区变化负责移除 View。</summary>
    private void HandleCommandFeedback(BattleCommandFeedback feedback)
    {
        if (feedback.Stage == BattleCommandFeedbackStage.Queued ||
            !_pendingPlayCards.TryGetValue(feedback.AuthoritySequence, out CardInstanceId cardId))
        {
            return;
        }

        _pendingPlayCards.Remove(feedback.AuthoritySequence);
        if (feedback.Stage != BattleCommandFeedbackStage.ExecutionFailed)
            return;

        HandCardVisual card = FindCardById(cardId);
        if (card == null)
            return;

        card.PlayCommandFailureFeedback(feedback.AuthoritySequence);
        LayoutCards(immediate: false);
        RefreshCardPlayPresentation();
    }

    /// <summary>阶段或当前玩家结束事实变化时，立即派生全部手牌的输入可用性。</summary>
    private void HandleTurnChanged(BattleTurnData _)
    {
        RefreshActiveDragFromCurrentFacts();
        RefreshCardPlayPresentation();
    }

    /// <summary>根据当前手牌布局增删并排序 CardView。</summary>
    private void RebuildCards(bool immediate)
    {
        IReadOnlyList<CardInstanceId> hand = _cardZones.Hand;
        HandCardDragTransition? dragTransition = RefreshActiveDragFromCurrentFacts(
            reflowIfCancelled: false);
        HandCardVisual excludedDraggingCard = dragTransition.HasValue
            && dragTransition.Value.ExcludeActiveCardFromLayout
                ? _draggingCard
                : null;

        for (int index = _cards.Count - 1; index >= 0; index--)
        {
            HandCardVisual card = _cards[index];
            if (ContainsCardId(hand, card.CardId))
                continue;

            // TODO(DEP-004): Future card-effect types need distinct pre-destruction actions before this visual is destroyed.
            card.SetPlayerInputEnabled(false);
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
            {
                card.SetCommandPending(
                    TryGetLatestPendingPlaySequence(cardId, out long authoritySequence)
                        ? authoritySequence
                        : (long?)null);
                orderedCards.Add(card);
            }
        }

        _cards.Clear();
        _cards.AddRange(orderedCards);
        LayoutCards(immediate, excludedDraggingCard);
        RefreshCardPlayPresentation();
    }

    /// <summary>从当前 Turn 与显式可空目标即时取得同一规则 module 的派生结果。</summary>
    private BattleCardPlayEvaluation EvaluateCard(
        HandCardVisual card,
        CombatantId? targetId)
    {
        var command = new PlayCardCommand(_player.Id, card.CardId, targetId);
        return _cardPlayRules.Evaluate(_commandQueue.Turn.CurrentValue, command);
    }

    /// <summary>按当前事实刷新全部卡牌输入与费用颜色，只有明确能量不足才显示不可支付色。</summary>
    private void RefreshCardPlayPresentation()
    {
        if (_cardPlayRules == null)
            return;

        foreach (HandCardVisual card in _cards)
        {
            BattleCardPlayEvaluation evaluation = EvaluateCard(card, targetId: null);
            bool hasInsufficientEnergy =
                evaluation.FailureReason == BattleCommandExecutionFailureReason.InsufficientEnergy;
            card.SetCostPaymentFeedback(!hasInsufficientEnergy, _insufficientCostColor);
            card.SetPlayerInputEnabled(HandCardInteractionAvailability.CanBeginDrag(
                evaluation.CanStartInteraction,
                evaluation.FailureReason));
        }
    }

    /// <summary>进入 Enemy 临时瞄准态，冻结卡牌并按当前合法候选显示箭头与普通高亮。</summary>
    private void EnterEnemyTargeting(
        HandCardVisual card,
        BattleCardPlayEvaluation evaluation,
        Vector2 pointerScreenPosition)
    {
        _dragPhase = HandCardDragPhase.EnemyTargeting;
        _participantPresenter.BeginTargetSelection(evaluation.LegalTargetIds);
        _targetingArrow.Show(card.GetScreenCenter(), pointerScreenPosition);
        _participantPresenter.UpdateTargetSelection(pointerScreenPosition);
    }

    /// <summary>保持卡牌冻结，只更新箭头屏幕端点与当前命中强化高亮。</summary>
    private void UpdateEnemyTargeting(HandCardVisual card, Vector2 pointerScreenPosition)
    {
        _targetingArrow.UpdateArrow(card.GetScreenCenter(), pointerScreenPosition);
        _participantPresenter.UpdateTargetSelection(pointerScreenPosition);
    }

    /// <summary>Turn、卡区或生命变化时重派生当前拖拽，并返回实际应用的纯转换决策。</summary>
    private HandCardDragTransition? RefreshActiveDragFromCurrentFacts(bool reflowIfCancelled = true)
    {
        if (_draggingCard == null)
            return null;

        BattleCardPlayEvaluation evaluation = EvaluateCard(_draggingCard, targetId: null);
        HandCardInteractionMode interactionMode = HandCardInteractionAvailability.ResolveMode(
            evaluation.CanStartInteraction,
            evaluation.FailureReason);
        HandCardDragTransition transition = HandCardDragTransitionPolicy.Resolve(
            _dragPhase,
            ContainsCardId(_cardZones.Hand, _draggingCard.CardId),
            interactionMode,
            evaluation.TargetRule);
        if (!transition.PreserveActiveCard)
        {
            CancelActiveDrag(reflowIfCancelled);
            return transition;
        }

        _dragPhase = transition.NextPhase;
        if (transition.ClearPlayFeedback)
            _draggingCard.SetDragPlayFeedback(false);
        if (transition.ClearTargetingPresentation)
            ClearTargetingPresentation();
        if (!transition.RebuildEnemyTargeting)
            return transition;

        _participantPresenter.BeginTargetSelection(evaluation.LegalTargetIds);
        if (_hasPointerScreenPosition)
        {
            _targetingArrow.Show(_draggingCard.GetScreenCenter(), _lastPointerScreenPosition);
            _participantPresenter.UpdateTargetSelection(_lastPointerScreenPosition);
        }

        return transition;
    }

    /// <summary>参与者生命改变时立即重派生卡牌可用性与当前瞄准候选。</summary>
    private void HandleCombatantHealthChanged(int _)
    {
        RefreshActiveDragFromCurrentFacts();
        RefreshCardPlayPresentation();
    }

    /// <summary>取消当前拖拽并清理箭头/高亮，可选让仍在手中的卡牌回到扇形排布。</summary>
    private void CancelActiveDrag(bool reflow)
    {
        if (_draggingCard != null)
            _draggingCard.SetDragPlayFeedback(false);

        _draggingCard = null;
        _dragPhase = HandCardDragPhase.Idle;
        _hasPointerScreenPosition = false;
        ClearTargetingPresentation();
        if (reflow && _cardZones != null)
            LayoutCards(immediate: false);
    }

    /// <summary>清除目标选择的全部瞬时表现，不修改手牌、能量、参与者或回合事实。</summary>
    private void ClearTargetingPresentation()
    {
        _participantPresenter?.EndTargetSelection();
        _targetingArrow?.Hide();
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

    /// <summary>返回指定卡牌实例当前记录的最新待定权威序号。</summary>
    private bool TryGetLatestPendingPlaySequence(
        CardInstanceId cardId,
        out long authoritySequence)
    {
        authoritySequence = 0;
        bool found = false;
        foreach (KeyValuePair<long, CardInstanceId> entry in _pendingPlayCards)
        {
            if (entry.Value != cardId || (found && entry.Key <= authoritySequence))
                continue;

            authoritySequence = entry.Key;
            found = true;
        }

        return found;
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

    /// <summary>检查卡牌有效、未待定，并允许费用不足卡只做不会提交的视觉拖动。</summary>
    private bool CanInteractWithCard(HandCardVisual card)
    {
        if (card == null || card.IsCommandPending || _cardPlayRules == null)
            return false;

        BattleCardPlayEvaluation evaluation = EvaluateCard(card, targetId: null);
        return HandCardInteractionAvailability.CanBeginDrag(
            evaluation.CanStartInteraction,
            evaluation.FailureReason);
    }

    /// <summary>组件被禁用时立即清理拖拽、箭头和高亮，避免切换阶段或场景后残留。</summary>
    private void OnDisable()
    {
        CancelActiveDrag(reflow: false);
    }

    /// <summary>容器销毁时停止异步落地，并释放其持有的牌面资源。</summary>
    private void OnDestroy()
    {
        _isDestroyed = true;
        CancelActiveDrag(reflow: false);
        ReleaseCardIllustrations();
        if (_targetingArrow != null && _targetingArrow.transform.parent == null)
            Destroy(_targetingArrow.gameObject);
    }
}
