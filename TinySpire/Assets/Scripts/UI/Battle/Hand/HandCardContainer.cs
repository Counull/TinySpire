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
    [SerializeField] private RectTransform _targetFocusAnchor;
    [SerializeField] private Color _insufficientCostColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    [SerializeField, Min(0.01f)] private float _targetFocusDuration = 0.2f;
    [SerializeField] private Ease _targetFocusEase = Ease.OutCubic;
    [SerializeField, Range(1.01f, 1.2f)] private float _targetFocusScale = 1.08f;
    [SerializeField, Range(1.001f, 1.08f)] private float _targetFocusBreathScale = 1.025f;
    [SerializeField, Min(0.05f)] private float _targetFocusBreathDuration = 0.55f;

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
    private BattleParticipantPresenter _participantPresenter;
    private BattleCardPlayRules _cardPlayRules;

    private readonly List<HandCardVisual> _cards = new();
    private readonly Dictionary<CardInstanceId, HandCardVisual> _transientCards = new();
    private readonly HashSet<CardInstanceId> _visibleHandCards = new();
    private readonly Dictionary<int, AsyncOperationHandle<Sprite>> _illustrationHandles = new();
    private readonly Dictionary<BattleCommandHandle, CardInstanceId> _pendingPlayCards = new();
    private HandCardVisual _draggingCard;
    private HandCardSelectionSession _activeHandCardSelection;
    private BattleCardZonesData _cardZones;
    private PlayerCombatantData _player;
    private bool _isDestroyed;
    private bool _lastParticipantPresentationReady;
    private HandCardDragPhase _dragPhase;
    private Vector2 _lastPointerScreenPosition;
    private bool _hasPointerScreenPosition;
    private Action<GameObject> _destroyTransientCardForTesting;

    private int CurrentHandCount => _cardZones.Hand.Count;

    /// <summary>只读公开当前等待补齐选牌意图的不可变 UI 会话；没有活动会话时为空。</summary>
    internal HandCardSelectionSession ActiveHandCardSelection => _activeHandCardSelection;

    /// <summary>只读确认真实 Hand View 已按当前权威顺序建好，可安全构造 Draw→Hand cue。</summary>
    internal bool IsCardMotionReady
    {
        get
        {
            if (_isDestroyed || !isActiveAndEnabled || _cardZones == null ||
                _cards.Count != _cardZones.Hand.Count)
            {
                return false;
            }

            for (int index = 0; index < _cards.Count; index++)
            {
                HandCardVisual card = _cards[index];
                if (card == null || card.CardId != _cardZones.Hand[index])
                    return false;
            }

            return true;
        }
    }

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
        BattleParticipantPresenter participantPresenter)
    {
        _session = session;
        _configs = configs;
        _cardTextFormatter = cardTextFormatter;
        _localization = localization;
        _commandQueue = commandQueue;
        _participantPresenter = participantPresenter;
    }

    /// <summary>仅供程序集内 Editor 测试替换 transient 销毁边界，不参与运行时 DI。</summary>
    internal void ConfigureTransientCardDestroyForTesting(Action<GameObject> destroyTransientCard)
    {
        if (destroyTransientCard == null)
            throw new ArgumentNullException(nameof(destroyTransientCard));
        if (_transientCards.Count > 0)
        {
            throw new InvalidOperationException(
                "Transient card destroy boundary cannot change after detaching a card.");
        }

        _destroyTransientCardForTesting = destroyTransientCard;
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
             || _participantPresenter == null
             || _targetingArrow == null
             || _targetFocusAnchor == null)
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
            _configs.Tables,
            _session.MachineGunnerRuntime);
        _lastParticipantPresentationReady = IsParticipantPresentationReady();
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
            .Subscribe(HandleCardZoneLayoutChanged)
            .AddTo(this);
        _player.Strength
            .Skip(1)
            .Subscribe(_ => RefreshCardTexts())
            .AddTo(this);
        _localization.LocaleChanged.Subscribe(_ => RefreshCardTexts()).AddTo(this);
        _commandQueue.Turn.Subscribe(HandleTurnChanged).AddTo(this);
        _commandQueue.Queue.Subscribe(HandleQueueChanged).AddTo(this);
        _commandQueue.Lifecycle.Subscribe(HandleCommandLifecycle).AddTo(this);
        foreach (CombatantData combatant in _session.Combatants.All.Values)
        {
            combatant.Health
                .Skip(1)
                .Subscribe(HandleCombatantHealthChanged)
                .AddTo(this);
        }

        RebuildCards(immediate: true);
    }

    /// <summary>只在 Presenter readiness 变化时刷新卡牌系统指针可用性，并清理失效的瞬时拖拽。</summary>
    private void Update()
    {
        if (_cardPlayRules == null)
            return;

        bool presentationReady = IsParticipantPresentationReady();
        if (presentationReady == _lastParticipantPresentationReady)
            return;

        _lastParticipantPresentationReady = presentationReady;
        if (!presentationReady)
            CancelActiveDrag(reflow: true);
        RefreshCardPlayPresentation();
    }

    /// <summary>在 Enemy 聚焦补间期间逐帧用最后真实指针刷新箭头，避免静止指针时起点落后卡牌。</summary>
    private void LateUpdate()
    {
        if (_dragPhase != HandCardDragPhase.EnemyTargeting
            || _draggingCard == null
            || !_hasPointerScreenPosition)
        {
            return;
        }

        UpdateEnemyTargeting(_draggingCard, _lastPointerScreenPosition);
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

    /// <summary>用右键取消活动选牌，并只让左键在重验三份权威快照后确认候选。</summary>
    public void HandlePointerClick(HandCardVisual card, PointerEventData eventData)
    {
        if (card == null ||
            eventData == null ||
            _activeHandCardSelection == null)
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            CancelHandCardSelection(reflow: true);
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        HandCardSelectionSession selection = _activeHandCardSelection;
        if (!selection.MatchesSnapshots(
                _cardZones.Layout.CurrentValue,
                _commandQueue.Turn.CurrentValue,
                _commandQueue.Queue.CurrentValue))
        {
            CancelHandCardSelection(reflow: true);
            return;
        }

        HandCardSelectionClickResolution resolution = selection.ResolveClick(card.CardId);
        switch (resolution.Action)
        {
            case HandCardSelectionClickAction.Ignore:
                return;
            case HandCardSelectionClickAction.Cancel:
                CancelHandCardSelection(reflow: true);
                return;
            case HandCardSelectionClickAction.Continue:
                RefreshCardPlayPresentation();
                return;
            case HandCardSelectionClickAction.Confirm:
                if (resolution.SelectedCardIds.Count != selection.RequiredCount)
                {
                    throw new InvalidOperationException(
                        "A confirmed hand-card selection must carry the required target count.");
                }

                _activeHandCardSelection = null;
                HandCardVisual sourceCard = FindCardById(selection.SourceCardId);
                if (sourceCard == null)
                {
                    LayoutCards(immediate: false);
                    RefreshCardPlayPresentation();
                    return;
                }

                SubmitPlayCard(
                    sourceCard,
                    selection.PlayTargetId,
                    resolution.SelectedCardIds);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(resolution),
                    resolution.Action,
                    "Unsupported hand-card selection click action.");
        }
    }

    /// <summary>
    /// 开始拖拽一张手牌，并使其脱离当前手牌排布。
    /// </summary>
    public void HandleBeginDrag(HandCardVisual card)
    {
        if (!CanInteractWithCard(card) || _draggingCard != null)
            return;

        // 只收口命中的入场牌；completion 可能继续 drain，随后必须以最新权威事实重验合法性。
        card.TryFastForwardIncomingCardMotion();
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
        if (!IsParticipantPresentationReady())
        {
            CancelActiveDrag(reflow: true);
            RefreshCardPlayPresentation();
            return;
        }

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
            && evaluation.RequiresExplicitTargetInput)
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
        if (!IsParticipantPresentationReady())
        {
            CancelActiveDrag(reflow: true);
            RefreshCardPlayPresentation();
            return;
        }

        BattleCardPlayEvaluation evaluation = EvaluateCard(card, targetId: null);
        CombatantId? hoveredTargetId = null;
        if (evaluation.RequiresExplicitTargetInput
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
        card.CancelTargetFocus();
        _draggingCard = null;
        _dragPhase = HandCardDragPhase.Idle;
        _hasPointerScreenPosition = false;
        card.SetDragPlayFeedback(false);
        ClearTargetingPresentation();

        bool canSubmitWithoutExplicitTarget =
            IsPastPlayLine(card) &&
            evaluation.CanStartInteraction &&
            !evaluation.RequiresExplicitTargetInput;
        if (targetId.HasValue || canSubmitWithoutExplicitTarget)
        {
            BattleCardPlayEvaluation finalEvaluation = EvaluateCard(card, targetId);
            if (finalEvaluation.Succeeded)
            {
                SubmitPlayCard(card, targetId);
                return;
            }

            if (finalEvaluation.FailureReason ==
                    BattleCommandExecutionFailureReason.CardSelectionRequired &&
                finalEvaluation.HandCardSelectionRequest != null)
            {
                BeginHandCardSelection(
                    card,
                    targetId,
                    finalEvaluation.HandCardSelectionRequest);
                return;
            }
        }

        LayoutCards(immediate: false);
        RefreshCardPlayPresentation();
    }

    /// <summary>冻结精确数量的手牌选择请求与当前三份权威快照，并让源牌回到普通手牌布局。</summary>
    private void BeginHandCardSelection(
        HandCardVisual sourceCard,
        CombatantId? playTargetId,
        BattleHandCardSelectionRequest request)
    {
        if (sourceCard == null)
            throw new ArgumentNullException(nameof(sourceCard));
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        _activeHandCardSelection = HandCardSelectionSession.Begin(
            sourceCard.CardId,
            playTargetId,
            request.LegalCardIds,
            request.RequiredCount,
            _cardZones.Layout.CurrentValue,
            _commandQueue.Turn.CurrentValue,
            _commandQueue.Queue.CurrentValue);
        LayoutCards(immediate: false);
        RefreshCardPlayPresentation();
    }

    /// <summary>以空选牌集合兼容普通出牌，并委托统一的选牌命令提交路径。</summary>
    private void SubmitPlayCard(HandCardVisual card, CombatantId? targetId)
    {
        SubmitPlayCard(card, targetId, Array.Empty<CardInstanceId>());
    }

    /// <summary>冻结显式选牌实例并先重验规则，再通过唯一 Queue seam 提交出牌意图。</summary>
    private void SubmitPlayCard(
        HandCardVisual card,
        CombatantId? targetId,
        IReadOnlyList<CardInstanceId> selectedCardIds)
    {
        if (selectedCardIds == null)
            throw new ArgumentNullException(nameof(selectedCardIds));

        CardInstanceId cardId = card.CardId;
        var command = new PlayCardCommand(
            _player.Id,
            cardId,
            targetId,
            selectedCardIds);
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
            LayoutCards(immediate: false);
            RefreshCardPlayPresentation();
            return;
        }

        LayoutCards(immediate: false);
    }

    /// <summary>按精确句柄处理出牌终态；旧生命周期不能清除新的待定视觉。</summary>
    private void HandleCommandLifecycle(BattleCommandLifecycleEvent lifecycle)
    {
        if (lifecycle.Stage == BattleCommandLifecycleStage.Queued)
        {
            TrackQueuedPlayCard(lifecycle);
            return;
        }

        if (lifecycle.Stage == BattleCommandLifecycleStage.ExecutionFailed
            || lifecycle.Stage == BattleCommandLifecycleStage.Faulted)
        {
            // 队首失败会使当前瞬时目标预览失去稳定前提，先回到既有拖拽安全恢复路径。
            CancelActiveDrag(reflow: true);
        }

        if (!_pendingPlayCards.TryGetValue(lifecycle.Handle, out CardInstanceId cardId))
        {
            return;
        }

        _pendingPlayCards.Remove(lifecycle.Handle);
        HandCardVisual card = FindCardById(cardId);
        if (card == null)
            return;

        if (lifecycle.Stage == BattleCommandLifecycleStage.ExecutionFailed)
            card.PlayCommandFailureFeedback(lifecycle.Handle);
        else
            card.ClearCommandPending(lifecycle.Handle);

        LayoutCards(immediate: false);
        RefreshCardPlayPresentation();
    }

    /// <summary>从 Queue 的 Queued 事实识别当前玩家可见出牌，并在任何同步终态前建立精确 pending。</summary>
    private void TrackQueuedPlayCard(BattleCommandLifecycleEvent lifecycle)
    {
        if (!(lifecycle.Command is PlayCardCommand command) ||
            _player == null ||
            command.ActorId != _player.Id ||
            _pendingPlayCards.ContainsKey(lifecycle.Handle))
        {
            return;
        }

        HandCardVisual card = FindCardById(command.CardId);
        if (card == null)
            return;

        _pendingPlayCards.Add(lifecycle.Handle, command.CardId);
        card.SetCommandPending(lifecycle.Handle);
    }

    /// <summary>阶段或当前玩家结束事实变化时，立即派生全部手牌的输入可用性。</summary>
    private void HandleTurnChanged(BattleTurnData _)
    {
        ClearStaleHandCardSelection();
        RefreshActiveDragFromCurrentFacts();
        RefreshCardPlayPresentation();
    }

    /// <summary>手牌布局发布时先清除已经失配的本地选牌会话，再按新布局重建牌面。</summary>
    private void HandleCardZoneLayoutChanged(IReadOnlyList<CardInstanceId> _)
    {
        ClearStaleHandCardSelection();
        RebuildCards(immediate: false);
    }

    /// <summary>队列权威快照发布时清除已经失配的本地选牌会话，并刷新手牌输入表现。</summary>
    private void HandleQueueChanged(BattleCommandQueueData _)
    {
        ClearStaleHandCardSelection();
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

            _visibleHandCards.Remove(card.CardId);
            DetachTransientCard(card);
            _cards.RemoveAt(index);
        }

        var orderedCards = new List<HandCardVisual>(hand.Count);
        foreach (CardInstanceId cardId in hand)
        {
            CardInstanceData cardState = _cardZones.Cards[cardId];
            HandCardVisual card = FindCardById(cardState.Id);
            if (card == null)
                card = CreateCard(cardState);

            if (card != null)
            {
                if (!_visibleHandCards.Contains(cardId))
                    card.PrepareForIncomingCardMotion();
                if (TryGetPendingPlayHandle(cardId, out BattleCommandHandle pendingHandle))
                    card.SetCommandPending(pendingHandle);
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

        bool queueAcceptsInput = !_commandQueue.Queue.CurrentValue.IsFaulted;
        bool presentationReady = IsParticipantPresentationReady();
        HandCardSelectionSession selection = _activeHandCardSelection;
        bool selectionFactsMatch = selection != null &&
            queueAcceptsInput &&
            presentationReady &&
            selection.MatchesSnapshots(
                _cardZones.Layout.CurrentValue,
                _commandQueue.Turn.CurrentValue,
                _commandQueue.Queue.CurrentValue);
        foreach (HandCardVisual card in _cards)
        {
            BattleCardPlayEvaluation evaluation = EvaluateCard(card, targetId: null);
            HandCardInteractionMode interactionMode =
                HandCardInteractionAvailability.ResolveMode(
                    evaluation.CanStartInteraction,
                    evaluation.FailureReason);
            if (!queueAcceptsInput || !presentationReady)
                interactionMode = HandCardInteractionMode.Disabled;

            HandCardSelectionPresentationRole selectionRole = selectionFactsMatch
                ? ContainsCardId(selection.LegalTargetCardIds, card.CardId)
                    ? HandCardSelectionPresentationRole.Candidate
                    : HandCardSelectionPresentationRole.NonCandidate
                : HandCardSelectionPresentationRole.None;
            card.SetInteractionPresentation(
                interactionMode,
                _insufficientCostColor,
                selectionRole);
        }
    }

    /// <summary>进入 Enemy 临时瞄准态，冻结卡牌并按当前合法候选显示箭头与普通高亮。</summary>
    private void EnterEnemyTargeting(
        HandCardVisual card,
        BattleCardPlayEvaluation evaluation,
        Vector2 pointerScreenPosition)
    {
        _dragPhase = HandCardDragPhase.EnemyTargeting;
        card.PlayTargetFocus(
            GetTargetFocusScreenPosition(),
            _targetFocusScale,
            _targetFocusDuration,
            _targetFocusEase,
            _targetFocusBreathScale,
            _targetFocusBreathDuration);
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

    /// <summary>把手牌 Prefab 的序列化 focus anchor 中心投影为当前 Canvas 对应的屏幕坐标。</summary>
    private Vector2 GetTargetFocusScreenPosition()
    {
        Canvas canvas = _targetFocusAnchor.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector3 worldCenter = _targetFocusAnchor.TransformPoint(_targetFocusAnchor.rect.center);
        return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
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
        {
            // 降级为普通拖拽时保留卡牌与指针意图，只退出聚焦并恢复基础缩放/旋转。
            _draggingCard.BeginDrag(CurrentHandCount + 1);
            ClearTargetingPresentation();
        }
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
        {
            _draggingCard.SetDragPlayFeedback(false);
            _draggingCard.CancelTargetFocus();
        }

        _draggingCard = null;
        _dragPhase = HandCardDragPhase.Idle;
        _hasPointerScreenPosition = false;
        ClearTargetingPresentation();
        if (reflow && _cardZones != null)
            LayoutCards(immediate: false);
    }

    /// <summary>清除当前选牌会话，并按需让仍在 Hand 中的卡牌回流后刷新输入表现。</summary>
    private void CancelHandCardSelection(bool reflow)
    {
        if (!ClearHandCardSelection())
            return;

        if (reflow && _cardZones != null)
            LayoutCards(immediate: false);
        RefreshCardPlayPresentation();
    }

    /// <summary>按当前三份权威快照重验活动选牌会话，失配时零写清理并按需回流牌面。</summary>
    internal void RefreshHandCardSelectionFromCurrentFacts(bool reflow)
    {
        if (!ClearStaleHandCardSelection())
            return;

        if (reflow && _cardZones != null)
            LayoutCards(immediate: false);
        RefreshCardPlayPresentation();
    }

    /// <summary>仅在活动选牌会话的布局、回合或队列引用已经漂移时清除本地会话。</summary>
    private bool ClearStaleHandCardSelection()
    {
        HandCardSelectionSession selection = _activeHandCardSelection;
        if (selection == null ||
            selection.MatchesSnapshots(
                _cardZones.Layout.CurrentValue,
                _commandQueue.Turn.CurrentValue,
                _commandQueue.Queue.CurrentValue))
        {
            return false;
        }

        return ClearHandCardSelection();
    }

    /// <summary>只清除本地选牌会话，不发布布局、回合、队列或其他权威战斗事实。</summary>
    private bool ClearHandCardSelection()
    {
        if (_activeHandCardSelection == null)
            return false;

        _activeHandCardSelection = null;
        return true;
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

            if (card.IsIncomingCardMotionPending || card.IsIncomingCardMotionActive)
                card.SetBasePose(pose);
            else if (immediate)
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

    /// <summary>按本局初始牌组及职业程序可能创建的唯一模板预加载牌面 Sprite，并持有其 Addressables 句柄。</summary>
    private async UniTask LoadCardIllustrationsAsync()
    {
        foreach (int templateId in _session.AvailableCardTemplateIds)
        {
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

    /// <summary>让离手 View 立即退出交互集合，并按运行时实例身份保留为短生命周期 transient。</summary>
    private void DetachTransientCard(HandCardVisual card)
    {
        if (card == null)
            return;

        CardInstanceId cardId = card.CardId;
        if (_transientCards.TryGetValue(cardId, out HandCardVisual existing)
            && existing != null
            && existing != card)
        {
            DestroyTransientCardObject(existing.gameObject);
        }

        card.PrepareAsTransient();
        _transientCards[cardId] = card;
    }

    /// <summary>创建无位移的 PlayCard 前奏 lease，使离手 transient 在完整时间线结束前保持可清理。</summary>
    internal BattleCommandPresentationTween CreateTransientCardHoldTween(BattleCardMotionCue cue)
    {
        if (cue == null)
            throw new ArgumentNullException(nameof(cue));
        if (cue.Kind != BattleCardMotionCueKind.PlayCardTransientHold || !cue.CardId.HasValue)
        {
            throw new InvalidOperationException(
                "Transient hold requires a frozen PlayCard CardId.");
        }

        CardInstanceId cardId = cue.CardId.Value;
        if (!_transientCards.TryGetValue(cardId, out HandCardVisual transientCard)
            || transientCard == null)
        {
            throw new InvalidOperationException(
                $"Card hold cannot find detached transient {cardId}.");
        }

        Tween tween = DOTween.Sequence().AppendCallback(() => { });
        Action cleanup = () => ReleaseTransientCard(cardId, transientCard);
        return new BattleCommandPresentationTween(tween, cleanup);
    }

    /// <summary>为同一离手 transient 创建原 Order 的弃牌或消耗轨迹，并让任一已建 lease 都能幂等收口。</summary>
    internal BattleCommandPresentationTween CreateTransientCardMotionTween(
        BattleCardMotionCue cue,
        Vector2 discardScreenPosition,
        float duration,
        Ease ease)
    {
        if (cue == null)
            throw new ArgumentNullException(nameof(cue));
        if (!cue.CardId.HasValue)
            throw new InvalidOperationException("Transient card motion requires a frozen CardId.");
        if (duration < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (cue.Kind != BattleCardMotionCueKind.HandToDiscard &&
            cue.Kind != BattleCardMotionCueKind.HandToExhaust)
        {
            throw new InvalidOperationException(
                $"Transient card motion cannot consume {cue.Kind}.");
        }

        CardInstanceId cardId = cue.CardId.Value;
        if (!_transientCards.TryGetValue(cardId, out HandCardVisual transientCard)
            || transientCard == null)
        {
            throw new InvalidOperationException(
                $"Card motion cannot find detached transient {cardId}.");
        }

        Tween tween = transientCard.CreateTransientScreenMotionTween(
            discardScreenPosition,
            duration,
            ease);
        Action cleanup = () => ReleaseTransientCard(cardId, transientCard);
        return new BattleCommandPresentationTween(tween, cleanup);
    }

    /// <summary>为当前权威 Hand 中抽取或临时创建的新牌建立统一入场 cue，取消时恢复最新 base pose。</summary>
    internal BattleCommandPresentationTween CreateIncomingCardMotionTween(
        BattleCardMotionCue cue,
        Vector2 incomingSourceScreenPosition,
        float duration,
        Ease ease,
        Action requestFastForward)
    {
        if (cue == null)
            throw new ArgumentNullException(nameof(cue));
        bool isSupportedIncomingCue = cue.Kind == BattleCardMotionCueKind.DrawToHand ||
            cue.Kind == BattleCardMotionCueKind.CreatedToHand;
        if (!isSupportedIncomingCue || !cue.CardId.HasValue)
        {
            throw new InvalidOperationException(
                "Incoming card motion requires a frozen DrawToHand or CreatedToHand CardId.");
        }
        if (duration < 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (requestFastForward == null)
            throw new ArgumentNullException(nameof(requestFastForward));

        HandCardVisual card = FindCardById(cue.CardId.Value);
        if (card == null)
        {
            throw new InvalidOperationException(
                $"Draw motion cannot find authoritative Hand View {cue.CardId.Value}.");
        }

        _visibleHandCards.Add(cue.CardId.Value);
        Tween tween = card.CreateIncomingScreenMotionTween(
            incomingSourceScreenPosition,
            duration,
            ease,
            requestFastForward);
        return new BattleCommandPresentationTween(
            tween,
            card.FinishIncomingCardMotion);
    }

    /// <summary>只在映射仍指向同一 View 时移除并销毁 transient，允许重复 cleanup 保持无操作。</summary>
    private void ReleaseTransientCard(CardInstanceId cardId, HandCardVisual transientCard)
    {
        if (!_transientCards.TryGetValue(cardId, out HandCardVisual current)
            || current != transientCard)
        {
            return;
        }

        _transientCards.Remove(cardId);
        if (transientCard != null)
            DestroyTransientCardObject(transientCard.gameObject);
    }

    /// <summary>经测试可替换边界销毁一个 transient 根 Canvas；生产路径继续使用场景生命周期 Destroy。</summary>
    private void DestroyTransientCardObject(GameObject transientObject)
    {
        if (transientObject == null)
            return;
        if (_destroyTransientCardForTesting != null)
        {
            _destroyTransientCardForTesting.Invoke(transientObject);
            return;
        }

        Destroy(transientObject);
    }

    /// <summary>销毁所有尚未由表现 lease 收口的离手 transient，避免旧 Scene 留下根 Canvas。</summary>
    private void DestroyTransientCards()
    {
        foreach (HandCardVisual transientCard in _transientCards.Values)
        {
            if (transientCard != null)
                DestroyTransientCardObject(transientCard.gameObject);
        }

        _transientCards.Clear();
    }

    /// <summary>返回指定卡牌实例当前唯一待定的 Queue 生命周期句柄。</summary>
    private bool TryGetPendingPlayHandle(
        CardInstanceId cardId,
        out BattleCommandHandle handle)
    {
        handle = null;
        foreach (KeyValuePair<BattleCommandHandle, CardInstanceId> entry in _pendingPlayCards)
        {
            if (entry.Value != cardId)
                continue;

            handle = entry.Key;
            return true;
        }

        return false;
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
        if (card == null ||
            card.IsCommandPending ||
            _activeHandCardSelection != null ||
            _cardPlayRules == null ||
            _commandQueue.Queue.CurrentValue.IsFaulted)
            return false;

        BattleCardPlayEvaluation evaluation = EvaluateCard(card, targetId: null);
        return HandCardInteractionAvailability.CanBeginDrag(
            evaluation.CanStartInteraction,
            evaluation.FailureReason,
            IsParticipantPresentationReady());
    }

    /// <summary>安全读取同场 Presenter 的非权威映射 readiness；对象销毁后保持关闭。</summary>
    private bool IsParticipantPresentationReady()
    {
        return _participantPresenter != null && _participantPresenter.IsPresentationReady;
    }

    /// <summary>组件被禁用时立即清理拖拽、箭头和高亮，避免切换阶段或场景后残留。</summary>
    private void OnDisable()
    {
        CancelActiveDrag(reflow: false);
        CancelHandCardSelection(reflow: false);
    }

    /// <summary>容器销毁时停止异步落地，并释放其持有的牌面资源。</summary>
    private void OnDestroy()
    {
        _isDestroyed = true;
        ClearHandCardSelection();
        CancelActiveDrag(reflow: false);
        DestroyTransientCards();
        ReleaseCardIllustrations();
        if (_targetingArrow != null && _targetingArrow.transform.parent == null)
            Destroy(_targetingArrow.gameObject);
    }
}
