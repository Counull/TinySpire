using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleCommandPresentationAdapterTests
{
    /// <summary>每个用例结束后释放共享工厂代建的敌人意图响应式资源。</summary>
    [TearDown]
    public void TearDown()
    {
        BattleCommandQueueTestFactory.DisposeOwnedEnemyIntents();
    }

    /// <summary>确认 Queue 以同一不透明句柄发布生命周期，而 Adapter 只负责解除可见结果屏障。</summary>
    [Test]
    public void VisibleExecution_PublishesLifecycleBeforeAdapterReleasesBarrier()
    {
        using var adapter = new BattleCommandPresentationAdapter(0f, () => 0f);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using var combatants = new BattleCombatantsData();
        using var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1u);
        PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
        var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [player.Id] = zones,
        };
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            adapter,
            playerZones,
            initialHandCount: 0,
            coordinator: coordinator);
        var command = new StartBattleCommand();
        BattleCommandHandle handle = coordinator.PreRegister(command);

        BattleCommandSubmissionResult submission = queue.Submit(command);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(lifecycles, Has.Count.EqualTo(2));
        Assert.That(lifecycles.Select(item => item.Stage), Is.EqualTo(new[]
        {
            BattleCommandLifecycleStage.Queued,
            BattleCommandLifecycleStage.ExecutionCompleted,
        }));
        Assert.That(lifecycles.All(item => ReferenceEquals(item.Handle, handle)), Is.True);
        Assert.That(lifecycles.All(item => item.AuthoritySequence == submission.AuthoritySequence), Is.True);
        Assert.That(lifecycles[0].Settlements, Is.Empty);
        Assert.That(lifecycles[1].Settlements, Has.Count.EqualTo(2));
        Assert.That(lifecycles[1].Settlements[0], Is.TypeOf<BattleEnergyRefilledSettlement>());
        Assert.That(lifecycles[1].Settlements[1], Is.TypeOf<BattlePhaseChangedSettlement>());
        Assert.That(coordinator.IsPending(handle), Is.False);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

        adapter.Tick();

        Assert.That(lifecycles, Has.Count.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
    }

    /// <summary>确认展示等待期间第二张牌只进入生命周期队列，不会提前扣能量或移动卡区。</summary>
    [Test]
    public void PresentationWait_AllowsSecondPlayWithoutPrematureMutation()
    {
        float deltaTime = 0f;
        using var adapter = new BattleCommandPresentationAdapter(1f, () => deltaTime);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using var combatants = new BattleCombatantsData();
        using var zones = new BattleCardZonesData(new[] { 1001, 1002 }, shuffleSeed: 1u);
        PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
        EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
        var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [player.Id] = zones,
        };
        var costs = new Dictionary<int, int>
        {
            [1001] = 1,
            [1002] = 1,
        };
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            adapter,
            playerZones,
            costs,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            energyPerRound: 3,
            initialHandCount: 2,
            coordinator: coordinator);
        queue.SubmitRegistered(new StartBattleCommand());
        deltaTime = 10f;
        adapter.Tick();
        lifecycles.Clear();

        deltaTime = 0f;
        CardInstanceId firstCardId = zones.Hand[0];
        CardInstanceId secondCardId = zones.Hand[1];
        var firstCommand = new PlayCardCommand(player.Id, firstCardId, player.Id);
        BattleCommandHandle firstHandle = coordinator.PreRegister(firstCommand);
        BattleCommandSubmissionResult firstSubmission = queue.Submit(firstCommand);
        var secondCommand = new PlayCardCommand(player.Id, secondCardId, player.Id);
        BattleCommandHandle secondHandle = coordinator.PreRegister(secondCommand);
        BattleCommandSubmissionResult secondSubmission = queue.Submit(secondCommand);

        Assert.That(firstSubmission.Accepted, Is.True);
        Assert.That(secondSubmission.Accepted, Is.True);
        Assert.That(
            lifecycles.Select(item => (item.Handle, item.Stage)),
            Is.EqualTo(new[]
            {
                (firstHandle, BattleCommandLifecycleStage.Queued),
                (firstHandle, BattleCommandLifecycleStage.ExecutionCompleted),
                (secondHandle, BattleCommandLifecycleStage.Queued),
            }));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(2));
        Assert.That(zones.Hand.Contains(secondCardId), Is.True);
        Assert.That(zones.DiscardPile.Contains(secondCardId), Is.False);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));

        deltaTime = 10f;
        adapter.Tick();

        Assert.That(
            lifecycles.Select(item => (item.Handle, item.Stage)),
            Is.EqualTo(new[]
            {
                (firstHandle, BattleCommandLifecycleStage.Queued),
                (firstHandle, BattleCommandLifecycleStage.ExecutionCompleted),
                (secondHandle, BattleCommandLifecycleStage.Queued),
                (secondHandle, BattleCommandLifecycleStage.ExecutionCompleted),
            }));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(1));
        Assert.That(zones.DiscardPile, Does.Contain(secondCardId));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
    }

    /// <summary>确认普通执行失败只经 coordinator 发布空结算终态，不会进入 Adapter 表现屏障。</summary>
    [Test]
    public void ExecutionFailure_PublishesLifecycleWithoutPresentationBarrierOrMutation()
    {
        using var adapter = new BattleCommandPresentationAdapter(0f, () => 0f);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using var combatants = new BattleCombatantsData();
        using var zones = new BattleCardZonesData(new[] { 1001 }, shuffleSeed: 1u);
        PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
        EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
        var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [player.Id] = zones,
        };
        var costs = new Dictionary<int, int>
        {
            [1001] = 1,
        };
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            adapter,
            playerZones,
            costs,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            energyPerRound: 0,
            initialHandCount: 1,
            coordinator: coordinator);
        queue.SubmitRegistered(new StartBattleCommand());
        adapter.Tick();
        lifecycles.Clear();

        CardInstanceId cardId = zones.Hand[0];
        var command = new PlayCardCommand(player.Id, cardId, player.Id);
        BattleCommandHandle handle = coordinator.PreRegister(command);
        BattleCommandSubmissionResult submission = queue.Submit(command);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(
            lifecycles.Select(item => (item.Handle, item.Stage)),
            Is.EqualTo(new[]
            {
                (handle, BattleCommandLifecycleStage.Queued),
                (handle, BattleCommandLifecycleStage.ExecutionFailed),
            }));
        Assert.That(
            lifecycles[^1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(lifecycles[^1].Settlements, Is.Empty);
        Assert.That(coordinator.IsPending(handle), Is.False);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.Zero);
        Assert.That(zones.Hand, Does.Contain(cardId));
        Assert.That(zones.DiscardPile.Contains(cardId), Is.False);

        adapter.Tick();

        Assert.That(lifecycles, Has.Count.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
    }

    /// <summary>确认成功但没有可见结算的命令直接完成，不会被纯 Adapter 人为增加等待。</summary>
    [Test]
    public void ZeroSettlementSuccess_BypassesPresentationAdapter()
    {
        using var adapter = new BattleCommandPresentationAdapter(0f, () => 0f);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using var combatants = new BattleCombatantsData();
        using var firstZones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1u);
        using var secondZones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 2u);
        PlayerCombatantData firstPlayer = combatants.AddPlayer(1001, 30, 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(1002, 28, 0);
        EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
        var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [firstPlayer.Id] = firstZones,
            [secondPlayer.Id] = secondZones,
        };
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            adapter,
            playerZones,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            initialHandCount: 0,
            coordinator: coordinator);
        queue.SubmitRegistered(new StartBattleCommand());
        adapter.Tick();
        lifecycles.Clear();

        var command = new EndPlayerActionCommand(firstPlayer.Id);
        BattleCommandHandle handle = coordinator.PreRegister(command);
        BattleCommandSubmissionResult submission = queue.Submit(command);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(
            lifecycles.Select(item => (item.Handle, item.Stage)),
            Is.EqualTo(new[]
            {
                (handle, BattleCommandLifecycleStage.Queued),
                (handle, BattleCommandLifecycleStage.ExecutionCompleted),
            }));
        Assert.That(lifecycles[^1].Settlements, Is.Empty);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.True);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);

        adapter.Tick();

        Assert.That(lifecycles, Has.Count.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
    }

    /// <summary>确认只有跳过记录的成功结果没有可见步骤，不再被固定时长占用表现屏障。</summary>
    [Test]
    public void NonVisibleSettlementResult_CompletesSynchronouslyWithoutFixedDelay()
    {
        using var adapter = new BattleCommandPresentationAdapter(1f, () => 0f);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 1,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleEnemyActionSkippedSettlement(
                    order: 0,
                    new CombatantId(2001),
                    BattleEnemyActionSkipReason.SourceNotAlive),
            });
        var completionCount = 0;

        ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);

        Assert.That(completionCount, Is.EqualTo(1));

        adapter.Tick();

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认单个 Effect 跳过记录同样不伪造反馈，也不会占用固定表现时长。</summary>
    [Test]
    public void NonVisibleOperationSkippedResult_CompletesSynchronouslyWithoutFixedDelay()
    {
        using var adapter = new BattleCommandPresentationAdapter(1f, () => 0f);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 1,
            BattleCommandType.PlayCard,
            new CombatantId(1001),
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleOperationSkippedSettlement(
                    order: 0,
                    new BattleEffectId(4001),
                    new CombatantId(1001),
                    new CombatantId(2001),
                    BattleOperationSkipReason.TargetNotAlive),
            });
        var completionCount = 0;

        ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);

        Assert.That(completionCount, Is.EqualTo(1));

        adapter.Tick();

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认 adapter 的零可见性只来自完整 plan，而不是继续扩展 settlement 类型特判。</summary>
    [Test]
    public void NonVisibleMixedSettlementResult_CompletesSynchronouslyFromPlan()
    {
        var playerId = new CombatantId(1001);
        using var adapter = new BattleCommandPresentationAdapter(1f, () => 0f);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 2,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleEnergySpentSettlement(0, playerId, 3, 2),
                new BattleEnergyRefilledSettlement(1, playerId, 2, 3),
            });
        var completionCount = 0;

        ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);

        Assert.That(completionCount, Is.EqualTo(1));

        adapter.Tick();

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认 concrete adapter 的立即完成沿 runner 同一门闩释放一次 completion。</summary>
    [Test]
    public void CompleteImmediately_VisibleResult_CompletesExactlyOnce()
    {
        using var adapter = new BattleCommandPresentationAdapter(1f, () => 0f);
        var phaseChanged = new BattlePhaseChangedSettlement(
            0,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            0,
            1,
            null,
            null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 3,
            BattleCommandType.StartBattle,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { phaseChanged });
        var completionCount = 0;

        ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);
        adapter.CompleteImmediately();
        adapter.CompleteImmediately();
        adapter.Tick();

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认 concrete adapter 把加速倍率转发给同一 runner，且不改变一次 completion 门闩。</summary>
    [Test]
    public void SetPresentationSpeed_VisibleResult_AcceleratesAndCompletesExactlyOnce()
    {
        using var adapter = new BattleCommandPresentationAdapter(1f, () => 1f);
        var phaseChanged = new BattlePhaseChangedSettlement(
            0,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            0,
            1,
            null,
            null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 31,
            BattleCommandType.StartBattle,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { phaseChanged });
        var completionCount = 0;

        adapter.SetPresentationSpeed(2f);
        ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);

        Assert.That(completionCount, Is.Zero);

        adapter.Tick();
        adapter.Tick();
        adapter.CompleteImmediately();

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认 concrete adapter 销毁时丢弃旧 completion，后续 Tick 或立即完成都不会迟到释放。</summary>
    [Test]
    public void Dispose_VisibleResult_DropsCompletionAndIgnoresLaterControlCalls()
    {
        var adapter = new BattleCommandPresentationAdapter(1f, () => 10f);
        var phaseChanged = new BattlePhaseChangedSettlement(
            0,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            0,
            1,
            null,
            null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 4,
            BattleCommandType.StartBattle,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { phaseChanged });
        var completionCount = 0;

        ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);
        adapter.Dispose();
        adapter.Dispose();
        adapter.Tick();
        adapter.CompleteImmediately();

        Assert.That(completionCount, Is.Zero);
    }

    /// <summary>确认散热的两张离手牌按被选消耗、源牌弃置的结算顺序飞向各自锚点并各清理一次。</summary>
    [Test]
    public void Present_VentHeatDualHandDepartures_RoutesSelectedToExhaustThenSourceToDiscardAndCleansBothInOrder()
    {
        GameObject canvasObject = new GameObject(
            "AdapterVentHeatCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var participantObject = new GameObject("AdapterVentHeatParticipant");
        var handObject = new GameObject("AdapterVentHeatHand");
        var cardObjects = new List<GameObject>();
        BattleCommandPresentationAdapter adapter = null;
        using var zones = new BattleCardZonesData(new[] { 3244, 3203 }, shuffleSeed: 3244);
        try
        {
            zones.Draw(2);
            CardInstanceId sourceCardId = zones.Hand.Single(cardId =>
                zones.Cards[cardId].TemplateId == 3244);
            CardInstanceId selectedCardId = zones.Hand.Single(cardId =>
                zones.Cards[cardId].TemplateId == 3203);
            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
            var visualsById = new Dictionary<CardInstanceId, HandCardVisual>();
            var initialScreenPositions = new Dictionary<CardInstanceId, Vector2>();
            CardInstanceId[] visualOrder = { sourceCardId, selectedCardId };
            for (int index = 0; index < visualOrder.Length; index++)
            {
                GameObject cardObject = UnityEngine.Object.Instantiate(cardPrefab);
                cardObjects.Add(cardObject);
                HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup group = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, visualOrder[index], group);
                visual.SetBasePoseImmediately(new HandCardPose(
                    new Vector2(-120f + index * 240f, -280f),
                    -4f + index * 8f,
                    index));
                visualsById.Add(visualOrder[index], visual);
                initialScreenPositions.Add(visualOrder[index], visual.GetScreenCenter());
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView pileView = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(pileView, "_drawPileText", drawText);
            SetPrivateField(pileView, "_discardPileText", discardText);
            SetPrivateField(pileView, "_exhaustPileText", exhaustText);
            Canvas.ForceUpdateCanvases();
            Assert.That(
                pileView.TryGetPileScreenAnchor(
                    BattleCardZone.ExhaustPile,
                    out Vector2 exhaustScreenPosition),
                Is.True);
            Assert.That(
                pileView.TryGetPileScreenAnchor(
                    BattleCardZone.DiscardPile,
                    out Vector2 discardScreenPosition),
                Is.True);

            BattleParticipantPresenter participantPresenter =
                participantObject.AddComponent<BattleParticipantPresenter>();
            HandCardContainer hand = handObject.AddComponent<HandCardContainer>();
            var transientDestroyOrder = new List<CardInstanceId>();
            hand.ConfigureTransientCardDestroyForTesting(transient =>
                transientDestroyOrder.Add(transient.GetComponent<HandCardVisual>().CardId));
            SetPrivateField(hand, "_cardZones", zones);
            GetPrivateField<List<HandCardVisual>>(hand, "_cards").AddRange(
                visualOrder.Select(cardId => visualsById[cardId]));

            int layoutPublicationCount = 0;
            using IDisposable layoutSubscription = zones.Layout
                .Skip(1)
                .Subscribe(_ => layoutPublicationCount++);
            BattlePreparedHandCardSelectionResolution zonePlan =
                zones.PrepareHandCardSelectionResolution(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    sourceCardId,
                    BattleCardZone.DiscardPile,
                    selectedStartingOrder: 1,
                    playedCardStartingOrder: 3);

            Assert.That(zonePlan.SelectedCardMovement.Order, Is.EqualTo(1));
            Assert.That(zonePlan.PlayedCardDeparture.Order, Is.EqualTo(3));
            Assert.That(layoutPublicationCount, Is.Zero);

            BattleCardZoneOperationResult zoneResult =
                zones.CommitPreparedHandCardSelectionResolution(zonePlan);
            CardZoneLayoutData committedLayout = zones.Layout.CurrentValue;

            Assert.That(zoneResult.Succeeded, Is.True);
            Assert.That(layoutPublicationCount, Is.EqualTo(1));
            Assert.That(committedLayout, Is.SameAs(zonePlan.NextLayout));
            Assert.That(zoneResult.Settlements.Select(settlement => settlement.Order),
                Is.EqualTo(new[] { 1, 3 }));
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(hand, new object[] { false });

            var playerId = new CombatantId(1001);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 3244,
                BattleCommandType.PlayCard,
                playerId,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleEnergySpentSettlement(0, playerId, 2, 2),
                    zoneResult.Settlements[0],
                    new BattleEnergyGainedSettlement(2, playerId, 2, 3),
                    zoneResult.Settlements[1],
                });
            BattleCommandPresentationPlan presentationPlan =
                BattleCommandPresentationPlan.Create(result);
            Assert.That(presentationPlan.Prelude, Is.Null);
            Assert.That(
                presentationPlan.SettlementSteps.Select(step =>
                    (step.SettlementOrder, step.Kind)),
                Is.EqualTo(new[]
                {
                    (1, BattleCommandPresentationStepKind.CardMoved),
                    (3, BattleCommandPresentationStepKind.CardMoved),
                }));
            adapter = new BattleCommandPresentationAdapter(
                participantPresenter,
                hand,
                pileView,
                () => 0.23f);
            int completionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);
            adapter.Tick();

            Assert.That(
                visualsById[selectedCardId].GetScreenCenter().x,
                Is.EqualTo(exhaustScreenPosition.x).Within(0.1f));
            Assert.That(
                visualsById[selectedCardId].GetScreenCenter().y,
                Is.EqualTo(exhaustScreenPosition.y).Within(0.1f));
            Assert.That(
                visualsById[sourceCardId].GetScreenCenter().x,
                Is.EqualTo(initialScreenPositions[sourceCardId].x).Within(0.1f));
            Assert.That(
                visualsById[sourceCardId].GetScreenCenter().y,
                Is.EqualTo(initialScreenPositions[sourceCardId].y).Within(0.1f));
            Assert.That(transientDestroyOrder, Is.Empty);
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            Assert.That(
                visualsById[sourceCardId].GetScreenCenter().x,
                Is.EqualTo(discardScreenPosition.x).Within(0.1f));
            Assert.That(
                visualsById[sourceCardId].GetScreenCenter().y,
                Is.EqualTo(discardScreenPosition.y).Within(0.1f));
            Assert.That(
                transientDestroyOrder,
                Is.EqualTo(new[] { selectedCardId, sourceCardId }));
            Assert.That(completionCount, Is.EqualTo(1));

            adapter.Tick();
            adapter.CompleteImmediately();
            adapter.CompleteImmediately();

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(
                transientDestroyOrder,
                Is.EqualTo(new[] { selectedCardId, sourceCardId }));
            Assert.That(zones.Layout.CurrentValue, Is.SameAs(committedLayout));
            Assert.That(layoutPublicationCount, Is.EqualTo(1));
            Assert.That(zones.Hand, Is.Empty);
            Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
            Assert.That(zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
        }
        finally
        {
            adapter?.Dispose();
            foreach (GameObject cardObject in cardObjects)
            {
                if (cardObject != null)
                    UnityEngine.Object.DestroyImmediate(cardObject);
            }
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(participantObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>确认 Burning Pact 的选择消耗、两次抽牌入场与来源弃置按真实冻结顺序串行播放，且不会再次写卡区。</summary>
    [Test]
    public void Present_BurningPactAtomicCardFlow_RoutesDeparturesAndIncomingDrawsInSettlementOrder()
    {
        GameObject canvasObject = new GameObject(
            "AdapterBurningPactCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var participantObject = new GameObject("AdapterBurningPactParticipant");
        var handObject = new GameObject("AdapterBurningPactHand");
        var cardObjects = new List<GameObject>();
        BattleCommandPresentationAdapter adapter = null;
        using var zones = new BattleCardZonesData(
            new[] { 3125, 3001, 3002, 3003 },
            shuffleSeed: 3125);
        try
        {
            zones.Draw(2);
            CardInstanceId sourceCardId = zones.Hand[0];
            CardInstanceId selectedCardId = zones.Hand[1];
            int layoutPublicationCount = 0;
            using IDisposable layoutSubscription = zones.Layout
                .Skip(1)
                .Subscribe(_ => layoutPublicationCount++);
            BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture zonePlan =
                zones.PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    drawCount: 2,
                    BattleCardZonesData.BattleCardHandLimit,
                    sourceCardId,
                    BattleCardZone.DiscardPile,
                    startingOrder: 1);
            CardInstanceId[] drawnCardIds = zonePlan.Settlements
                .OfType<BattleCardMovedSettlement>()
                .Where(moved =>
                    moved.FromZone == BattleCardZone.DrawPile &&
                    moved.ToZone == BattleCardZone.Hand)
                .Select(moved => moved.CardId)
                .ToArray();

            Assert.That(drawnCardIds, Has.Length.EqualTo(2));
            Assert.That(layoutPublicationCount, Is.Zero);
            Assert.That(
                zonePlan.Settlements.Select(settlement => settlement.Order),
                Is.EqualTo(new[] { 1, 2, 3, 4 }));

            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
            var visualsById = new Dictionary<CardInstanceId, HandCardVisual>();
            CardInstanceId[] visualOrder = new[] { sourceCardId, selectedCardId }
                .Concat(drawnCardIds)
                .ToArray();
            var initialScreenPositions = new Dictionary<CardInstanceId, Vector2>();
            for (int index = 0; index < visualOrder.Length; index++)
            {
                GameObject cardObject = UnityEngine.Object.Instantiate(cardPrefab);
                cardObjects.Add(cardObject);
                HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup group = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, visualOrder[index], group);
                visual.SetBasePoseImmediately(new HandCardPose(
                    new Vector2(-270f + index * 180f, -280f),
                    -6f + index * 4f,
                    index));
                visualsById.Add(visualOrder[index], visual);
                initialScreenPositions.Add(visualOrder[index], visual.GetScreenCenter());
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView pileView = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(pileView, "_drawPileText", drawText);
            SetPrivateField(pileView, "_discardPileText", discardText);
            SetPrivateField(pileView, "_exhaustPileText", exhaustText);
            Canvas.ForceUpdateCanvases();
            Assert.That(
                pileView.TryGetPileScreenAnchor(
                    BattleCardZone.ExhaustPile,
                    out Vector2 exhaustScreenPosition),
                Is.True);
            Assert.That(
                pileView.TryGetPileScreenAnchor(
                    BattleCardZone.DiscardPile,
                    out Vector2 discardScreenPosition),
                Is.True);

            BattleParticipantPresenter participantPresenter =
                participantObject.AddComponent<BattleParticipantPresenter>();
            HandCardContainer hand = handObject.AddComponent<HandCardContainer>();
            var transientDestroyOrder = new List<CardInstanceId>();
            hand.ConfigureTransientCardDestroyForTesting(transient =>
                transientDestroyOrder.Add(transient.GetComponent<HandCardVisual>().CardId));
            SetPrivateField(hand, "_cardZones", zones);
            GetPrivateField<List<HandCardVisual>>(hand, "_cards").AddRange(
                visualOrder.Select(cardId => visualsById[cardId]));

            BattleCardZoneOperationResult zoneResult =
                zones.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(zonePlan);
            CardZoneLayoutData committedLayout = zones.Layout.CurrentValue;
            Assert.That(zoneResult.Succeeded, Is.True);
            Assert.That(layoutPublicationCount, Is.EqualTo(1));
            Assert.That(committedLayout, Is.SameAs(zonePlan.NextLayout));
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(hand, new object[] { false });

            var playerId = new CombatantId(1001);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 3125,
                BattleCommandType.PlayCard,
                playerId,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleEnergySpentSettlement(0, playerId, 3, 2),
                    zoneResult.Settlements[0],
                    zoneResult.Settlements[1],
                    zoneResult.Settlements[2],
                    zoneResult.Settlements[3],
                });
            Assert.That(result.Settlements, Has.Count.EqualTo(5));
            Assert.That(result.Settlements[0], Is.TypeOf<BattleEnergySpentSettlement>());
            Assert.That(
                result.Settlements.Skip(1).Select(settlement => settlement.RecordType),
                Is.All.EqualTo(BattleSettlementRecordType.CardMoved));
            Assert.That(
                result.Settlements.Skip(1).Cast<BattleCardMovedSettlement>().Select(moved =>
                    (moved.CardId, moved.FromZone, moved.ToZone)),
                Is.EqualTo(new[]
                {
                    (selectedCardId, BattleCardZone.Hand, BattleCardZone.ExhaustPile),
                    (drawnCardIds[0], BattleCardZone.DrawPile, BattleCardZone.Hand),
                    (drawnCardIds[1], BattleCardZone.DrawPile, BattleCardZone.Hand),
                    (sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile),
                }));
            BattleCommandPresentationPlan presentationPlan =
                BattleCommandPresentationPlan.Create(result);
            Assert.That(presentationPlan.Prelude, Is.Null);
            Assert.That(
                presentationPlan.SettlementSteps.Select(step =>
                    (step.SettlementOrder, step.Kind)),
                Is.EqualTo(new[]
                {
                    (1, BattleCommandPresentationStepKind.CardMoved),
                    (2, BattleCommandPresentationStepKind.CardMoved),
                    (3, BattleCommandPresentationStepKind.CardMoved),
                    (4, BattleCommandPresentationStepKind.CardMoved),
                }));

            float frameDelta = 0.23f;
            adapter = new BattleCommandPresentationAdapter(
                participantPresenter,
                hand,
                pileView,
                () => frameDelta);
            int completionCount = 0;
            ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);

            adapter.Tick();

            Assert.That(
                visualsById[selectedCardId].GetScreenCenter().x,
                Is.EqualTo(exhaustScreenPosition.x).Within(0.1f));
            Assert.That(
                visualsById[selectedCardId].GetScreenCenter().y,
                Is.EqualTo(exhaustScreenPosition.y).Within(0.1f));
            Assert.That(visualsById[drawnCardIds[0]].IsIncomingCardMotionActive, Is.True);
            Assert.That(visualsById[drawnCardIds[1]].IsIncomingCardMotionActive, Is.False);
            Assert.That(
                visualsById[sourceCardId].GetScreenCenter().x,
                Is.EqualTo(initialScreenPositions[sourceCardId].x).Within(0.1f));
            Assert.That(transientDestroyOrder, Is.Empty);
            Assert.That(completionCount, Is.Zero);

            Assert.That(
                visualsById[drawnCardIds[0]].TryFastForwardIncomingCardMotion(),
                Is.True);
            Assert.That(visualsById[drawnCardIds[0]].IsIncomingCardMotionActive, Is.False);
            frameDelta = 0.01f;
            adapter.Tick();

            Assert.That(visualsById[drawnCardIds[1]].IsIncomingCardMotionActive, Is.True);
            Assert.That(
                visualsById[drawnCardIds[1]].TryFastForwardIncomingCardMotion(),
                Is.True);
            Assert.That(visualsById[drawnCardIds[1]].IsIncomingCardMotionActive, Is.False);
            Assert.That(completionCount, Is.Zero);

            frameDelta = 0.23f;
            adapter.Tick();

            Assert.That(
                visualsById[sourceCardId].GetScreenCenter().x,
                Is.EqualTo(discardScreenPosition.x).Within(0.1f));
            Assert.That(
                visualsById[sourceCardId].GetScreenCenter().y,
                Is.EqualTo(discardScreenPosition.y).Within(0.1f));
            Assert.That(
                transientDestroyOrder,
                Is.EqualTo(new[] { selectedCardId, sourceCardId }));
            Assert.That(drawnCardIds, Has.None.Matches<CardInstanceId>(
                cardId => transientDestroyOrder.Contains(cardId)));
            Assert.That(drawnCardIds.All(cardId => visualsById[cardId] != null), Is.True);
            Assert.That(completionCount, Is.EqualTo(1));

            adapter.Tick();
            adapter.CompleteImmediately();
            adapter.CompleteImmediately();

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(
                transientDestroyOrder,
                Is.EqualTo(new[] { selectedCardId, sourceCardId }));
            Assert.That(zones.Layout.CurrentValue, Is.SameAs(committedLayout));
            Assert.That(layoutPublicationCount, Is.EqualTo(1));
            Assert.That(zones.Hand, Is.EqualTo(drawnCardIds));
            Assert.That(zones.DrawPile, Is.Empty);
            Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
            Assert.That(zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
        }
        finally
        {
            adapter?.Dispose();
            foreach (GameObject cardObject in cardObjects)
            {
                if (cardObject != null)
                    UnityEngine.Object.DestroyImmediate(cardObject);
            }
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(participantObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>确认 concrete adapter 把重洗步骤路由到现有 Pile View，并仍由同一 runner 释放一次 completion。</summary>
    [Test]
    public void Present_CardsReshuffled_UsesConcretePileCueInSharedRunner()
    {
        GameObject canvasObject = new GameObject(
            "AdapterCardMotionCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var participantObject = new GameObject("AdapterCardMotionParticipant");
        var handObject = new GameObject("AdapterCardMotionHand");
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView pileView = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(pileView, "_drawPileText", drawText);
            SetPrivateField(pileView, "_discardPileText", discardText);
            SetPrivateField(pileView, "_exhaustPileText", exhaustText);
            int transientDestroyCount = 0;
            pileView.ConfigureReshuffleTransientDestroyForTesting(transient =>
            {
                transientDestroyCount++;
                UnityEngine.Object.DestroyImmediate(transient);
            });
            Canvas.ForceUpdateCanvases();

            BattleParticipantPresenter participantPresenter =
                participantObject.AddComponent<BattleParticipantPresenter>();
            HandCardContainer hand = handObject.AddComponent<HandCardContainer>();
            adapter = new BattleCommandPresentationAdapter(
                participantPresenter,
                hand,
                pileView,
                () => 0.1f);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 51,
                BattleCommandType.EndPlayerAction,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleCardsReshuffledSettlement(
                        order: 0,
                        new[] { new CardInstanceId(11), new CardInstanceId(12) }),
                });
            int completionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Null);
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Not.Null);
            Assert.That(completionCount, Is.Zero);

            adapter.CompleteImmediately();
            adapter.CompleteImmediately();

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Null);
            Assert.That(transientDestroyCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            adapter?.Dispose();
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(participantObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>确认同一 concrete adapter 依次播放 Draw→Hand 与 Hand→Discard，并只快进当前入场 cue。</summary>
    [Test]
    public void Present_DrawThenDiscard_UsesAuthoritativeHandAndTransientInSharedRunner()
    {
        GameObject canvasObject = new GameObject(
            "AdapterDrawDiscardCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var participantObject = new GameObject("AdapterDrawDiscardParticipant");
        var handObject = new GameObject("AdapterDrawDiscardHand");
        GameObject cardObject = null;
        BattleCommandPresentationAdapter adapter = null;
        using var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 7331);
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
            cardObject = UnityEngine.Object.Instantiate(cardPrefab);
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup cardCanvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, cardCanvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(70f, -280f), 5f, 0));
            Vector2 authoritativeBaseScreenPosition = visual.GetScreenCenter();

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView pileView = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(pileView, "_drawPileText", drawText);
            SetPrivateField(pileView, "_discardPileText", discardText);
            SetPrivateField(pileView, "_exhaustPileText", exhaustText);
            Canvas.ForceUpdateCanvases();

            BattleParticipantPresenter participantPresenter =
                participantObject.AddComponent<BattleParticipantPresenter>();
            HandCardContainer hand = handObject.AddComponent<HandCardContainer>();
            int transientDestroyCount = 0;
            hand.ConfigureTransientCardDestroyForTesting(_ => transientDestroyCount++);
            SetPrivateField(hand, "_cardZones", zones);
            GetPrivateField<List<HandCardVisual>>(hand, "_cards").Add(visual);
            int handResolveCount = 0;
            adapter = new BattleCommandPresentationAdapter(
                participantPresenter,
                () =>
                {
                    handResolveCount++;
                    return hand;
                },
                pileView,
                () => 0.1f);
            Assert.That(handResolveCount, Is.Zero, "adapter 构造不得提前解析依赖 Queue 的 Hand。");
            var drawResult = new BattleCommandExecutionResult(
                authoritySequence: 61,
                BattleCommandType.EndPlayerAction,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleCardMovedSettlement(
                        order: 0,
                        cardId,
                        BattleCardZone.DrawPile,
                        BattleCardZone.Hand),
                });
            int drawCompletionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(
                drawResult,
                () => drawCompletionCount++);
            adapter.Tick();

            Assert.That(handResolveCount, Is.EqualTo(1));
            Assert.That(visual.IsIncomingCardMotionActive, Is.True);
            Assert.That(visual.GetScreenCenter(), Is.Not.EqualTo(authoritativeBaseScreenPosition));
            Assert.That(visual.TryFastForwardIncomingCardMotion(), Is.True);
            Assert.That(visual.IsIncomingCardMotionActive, Is.False);
            Assert.That(drawCompletionCount, Is.EqualTo(1));
            Assert.That(visual.GetScreenCenter().x, Is.EqualTo(authoritativeBaseScreenPosition.x).Within(0.01f));
            Assert.That(visual.GetScreenCenter().y, Is.EqualTo(authoritativeBaseScreenPosition.y).Within(0.01f));
            Assert.That(zones.Hand, Is.EqualTo(new[] { cardId }));

            zones.DiscardFromHand(cardId);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(hand, new object[] { false });
            var discardResult = new BattleCommandExecutionResult(
                authoritySequence: 62,
                BattleCommandType.EndPlayerAction,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleCardMovedSettlement(
                        order: 0,
                        cardId,
                        BattleCardZone.Hand,
                        BattleCardZone.DiscardPile),
                });
            int discardCompletionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(
                discardResult,
                () => discardCompletionCount++);
            adapter.Tick();
            adapter.CompleteImmediately();
            adapter.CompleteImmediately();

            Assert.That(handResolveCount, Is.EqualTo(2));
            Assert.That(drawCompletionCount, Is.EqualTo(1));
            Assert.That(discardCompletionCount, Is.EqualTo(1));
            Assert.That(transientDestroyCount, Is.EqualTo(1));
            Assert.That(zones.Hand, Is.Empty);
            Assert.That(zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
        finally
        {
            adapter?.Dispose();
            if (cardObject != null)
                UnityEngine.Object.DestroyImmediate(cardObject);
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(participantObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>确认 EndAction 多张离手牌严格按 settlement Order 依次到达弃牌锚点并顺序清理。</summary>
    [Test]
    public void Present_EndActionMultipleCards_PreservesSettlementMotionAndCleanupOrder()
    {
        GameObject canvasObject = new GameObject(
            "AdapterMultiDiscardCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var participantObject = new GameObject("AdapterMultiDiscardParticipant");
        var handObject = new GameObject("AdapterMultiDiscardHand");
        var cardObjects = new List<GameObject>();
        BattleCommandPresentationAdapter adapter = null;
        using var zones = new BattleCardZonesData(new[] { 3001, 3001, 3001 }, shuffleSeed: 9881);
        try
        {
            zones.Draw(3);
            CardInstanceId[] orderedCardIds = zones.Hand.ToArray();
            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
            var visuals = new List<HandCardVisual>();
            var initialScreenPositions = new List<Vector2>();
            for (int index = 0; index < orderedCardIds.Length; index++)
            {
                GameObject cardObject = UnityEngine.Object.Instantiate(cardPrefab);
                cardObjects.Add(cardObject);
                HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup group = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, orderedCardIds[index], group);
                visual.SetBasePoseImmediately(new HandCardPose(
                    new Vector2(-160f + index * 160f, -280f),
                    -6f + index * 6f,
                    index));
                visuals.Add(visual);
                initialScreenPositions.Add(visual.GetScreenCenter());
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView pileView = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(pileView, "_drawPileText", drawText);
            SetPrivateField(pileView, "_discardPileText", discardText);
            SetPrivateField(pileView, "_exhaustPileText", exhaustText);
            Canvas.ForceUpdateCanvases();
            Assert.That(
                pileView.TryGetPileScreenAnchor(
                    BattleCardZone.DiscardPile,
                    out Vector2 discardScreenPosition),
                Is.True);

            BattleParticipantPresenter participantPresenter =
                participantObject.AddComponent<BattleParticipantPresenter>();
            HandCardContainer hand = handObject.AddComponent<HandCardContainer>();
            var transientDestroyOrder = new List<CardInstanceId>();
            hand.ConfigureTransientCardDestroyForTesting(transient =>
            {
                transientDestroyOrder.Add(transient.GetComponent<HandCardVisual>().CardId);
            });
            SetPrivateField(hand, "_cardZones", zones);
            GetPrivateField<List<HandCardVisual>>(hand, "_cards").AddRange(visuals);
            foreach (CardInstanceId cardId in orderedCardIds)
                zones.DiscardFromHand(cardId);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(hand, new object[] { false });

            adapter = new BattleCommandPresentationAdapter(
                participantPresenter,
                hand,
                pileView,
                () => 0.23f);
            var settlements = new BattleSettlementRecord[orderedCardIds.Length];
            for (int index = 0; index < orderedCardIds.Length; index++)
            {
                settlements[index] = new BattleCardMovedSettlement(
                    order: index,
                    orderedCardIds[index],
                    BattleCardZone.Hand,
                    BattleCardZone.DiscardPile);
            }
            var result = new BattleCommandExecutionResult(
                authoritySequence: 63,
                BattleCommandType.EndPlayerAction,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                settlements);
            int completionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);
            adapter.Tick();

            Assert.That(visuals[0].GetScreenCenter().x, Is.EqualTo(discardScreenPosition.x).Within(0.01f));
            Assert.That(visuals[0].GetScreenCenter().y, Is.EqualTo(discardScreenPosition.y).Within(0.01f));
            Assert.That(visuals[2].GetScreenCenter().x, Is.EqualTo(initialScreenPositions[2].x).Within(0.01f));
            Assert.That(visuals[2].GetScreenCenter().y, Is.EqualTo(initialScreenPositions[2].y).Within(0.01f));
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            Assert.That(visuals[1].GetScreenCenter().x, Is.EqualTo(discardScreenPosition.x).Within(0.01f));
            Assert.That(visuals[1].GetScreenCenter().y, Is.EqualTo(discardScreenPosition.y).Within(0.01f));
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(transientDestroyOrder, Is.EqualTo(orderedCardIds));
            Assert.That(zones.Hand, Is.Empty);
            Assert.That(zones.DiscardPile, Is.EqualTo(orderedCardIds));
        }
        finally
        {
            adapter?.Dispose();
            foreach (GameObject cardObject in cardObjects)
            {
                if (cardObject != null)
                    UnityEngine.Object.DestroyImmediate(cardObject);
            }
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(participantObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>确认真实重洗记录先播放单个过渡，再按原 Order 逐张执行 Draw→Hand 入场。</summary>
    [Test]
    public void Present_ReshuffleThenDraw_PreservesFrozenSettlementOrderAndCurrentHand()
    {
        GameObject canvasObject = new GameObject(
            "AdapterReshuffleDrawCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var participantObject = new GameObject("AdapterReshuffleDrawParticipant");
        var handObject = new GameObject("AdapterReshuffleDrawHand");
        var cardObjects = new List<GameObject>();
        BattleCommandPresentationAdapter adapter = null;
        using var zones = new BattleCardZonesData(new[] { 3001, 3001 }, shuffleSeed: 4419);
        try
        {
            zones.Draw(2);
            zones.DiscardHand();
            BattleCardZoneOperationResult drawOperation = zones.Draw(2);
            CardInstanceId[] authoritativeHand = zones.Hand.ToArray();
            Assert.That(authoritativeHand, Has.Length.EqualTo(2));

            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
            var visualsById = new Dictionary<CardInstanceId, HandCardVisual>();
            for (int index = 0; index < authoritativeHand.Length; index++)
            {
                GameObject cardObject = UnityEngine.Object.Instantiate(cardPrefab);
                cardObjects.Add(cardObject);
                HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup group = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, authoritativeHand[index], group);
                visual.SetBasePoseImmediately(new HandCardPose(
                    new Vector2(-90f + index * 180f, -280f),
                    -4f + index * 8f,
                    index));
                visualsById.Add(authoritativeHand[index], visual);
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView pileView = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(pileView, "_drawPileText", drawText);
            SetPrivateField(pileView, "_discardPileText", discardText);
            SetPrivateField(pileView, "_exhaustPileText", exhaustText);
            int reshuffleDestroyCount = 0;
            pileView.ConfigureReshuffleTransientDestroyForTesting(transient =>
            {
                reshuffleDestroyCount++;
                UnityEngine.Object.DestroyImmediate(transient);
            });
            Canvas.ForceUpdateCanvases();

            BattleParticipantPresenter participantPresenter =
                participantObject.AddComponent<BattleParticipantPresenter>();
            HandCardContainer hand = handObject.AddComponent<HandCardContainer>();
            SetPrivateField(hand, "_cardZones", zones);
            GetPrivateField<List<HandCardVisual>>(hand, "_cards")
                .AddRange(authoritativeHand.Select(cardId => visualsById[cardId]));
            adapter = new BattleCommandPresentationAdapter(
                participantPresenter,
                hand,
                pileView,
                () => 0.1f);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 64,
                BattleCommandType.CompleteEnemyAction,
                submitterId: null,
                BattleCommandExecutionFailureReason.None,
                drawOperation.Settlements);
            CardInstanceId[] drawCueOrder = drawOperation.Settlements
                .OfType<BattleCardMovedSettlement>()
                .Where(moved =>
                    moved.FromZone == BattleCardZone.DrawPile &&
                    moved.ToZone == BattleCardZone.Hand)
                .Select(moved => moved.CardId)
                .ToArray();
            Assert.That(drawCueOrder, Is.EqualTo(authoritativeHand));
            int completionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);
            adapter.Tick();

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Not.Null);
            Assert.That(visualsById[drawCueOrder[0]].IsIncomingCardMotionActive, Is.False);
            Assert.That(visualsById[drawCueOrder[1]].IsIncomingCardMotionActive, Is.False);
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();
            adapter.Tick();
            adapter.Tick();

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Null);
            Assert.That(reshuffleDestroyCount, Is.EqualTo(1));
            Assert.That(visualsById[drawCueOrder[0]].IsIncomingCardMotionActive, Is.True);
            Assert.That(visualsById[drawCueOrder[1]].IsIncomingCardMotionActive, Is.False);

            Assert.That(visualsById[drawCueOrder[0]].TryFastForwardIncomingCardMotion(), Is.True);
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            Assert.That(visualsById[drawCueOrder[1]].IsIncomingCardMotionActive, Is.True);
            Assert.That(visualsById[drawCueOrder[1]].TryFastForwardIncomingCardMotion(), Is.True);
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(zones.Hand, Is.EqualTo(authoritativeHand));
            Assert.That(zones.DrawPile, Is.Empty);
            Assert.That(zones.DiscardPile, Is.Empty);
        }
        finally
        {
            adapter?.Dispose();
            foreach (GameObject cardObject in cardObjects)
            {
                if (cardObject != null)
                    UnityEngine.Object.DestroyImmediate(cardObject);
            }
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(participantObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>确认无效 settlement Order 在 Present 内同步抛出，且不会占用或调用 completion。</summary>
    [Test]
    public void Present_InvalidSettlementOrder_ThrowsBeforeCompletionOrOwnership()
    {
        using var adapter = new BattleCommandPresentationAdapter(1f, () => 0f);
        var invalidResult = new BattleCommandExecutionResult(
            authoritySequence: 5,
            BattleCommandType.EndPlayerAction,
            new CombatantId(1001),
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleCardMovedSettlement(
                    order: 1,
                    new CardInstanceId(41),
                    BattleCardZone.Hand,
                    BattleCardZone.DiscardPile),
            });
        var completionCount = 0;

        Assert.Throws<ArgumentException>(
            () => ((IBattleCommandPresentation)adapter).Present(
                invalidResult,
                () => completionCount++));

        Assert.That(completionCount, Is.Zero);
    }

    /// <summary>确认终局步骤消费执行结果携带的 typed 事实，并在胜负反馈后释放唯一 completion。</summary>
    [TestCase(BattleResultKind.Victory, "battle.ui.result.victory")]
    [TestCase(BattleResultKind.Defeat, "battle.ui.result.defeat")]
    public void BattleEnded_MapsTypedResultBeforeCompletion(
        BattleResultKind resultKind,
        string expectedLocalizationKey)
    {
        BattleFlowFeedbackCue capturedCue = null;
        var callbackOrder = new List<string>();
        using var adapter = new BattleCommandPresentationAdapter(
            cue =>
            {
                capturedCue = cue;
                Sequence sequence = DOTween.Sequence()
                    .AppendCallback(() => callbackOrder.Add("BattleOutcome"));
                return new BattleCommandPresentationTween(sequence, cleanup: null);
            },
            () => 0f);

        var battleEnded = new BattlePhaseChangedSettlement(
            order: 0,
            BattleTurnPhase.EnemyAction,
            BattleTurnPhase.BattleEnded,
            roundNumberBefore: 3,
            roundNumberAfter: 3,
            currentActingEnemyIdBefore: new CombatantId(2001),
            currentActingEnemyIdAfter: null);
        var battleResult = new BattleResult(
            resultKind,
            authoritySequence: 65,
            roundNumber: 3,
            players: new[]
            {
                new BattleResultPlayerSnapshot(new CombatantId(1), 1001, 30, 30),
            });
        var result = new BattleCommandExecutionResult(
            authoritySequence: 65,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { battleEnded },
            battleResult);

        ((IBattleCommandPresentation)adapter).Present(
            result,
            () => callbackOrder.Add("Completion"));
        adapter.CompleteImmediately();

        Assert.That(capturedCue, Is.Not.Null);
        Assert.That(capturedCue.Kind, Is.EqualTo(BattleFlowFeedbackCueKind.BattleOutcome));
        Assert.That(capturedCue.LocalizationKey, Is.EqualTo(expectedLocalizationKey));
        Assert.That(capturedCue.BlocksSystemPointer, Is.True);
        Assert.That(callbackOrder, Is.EqualTo(new[] { "BattleOutcome", "Completion" }));
    }

    /// <summary>确认缺少 typed 结果的 BattleEnded 同步 fault，且不伪造终局 cue 或 completion。</summary>
    [Test]
    public void BattleEnded_WithoutTypedResult_ThrowsWithoutCueOrCompletion()
    {
        int cueCount = 0;
        using var adapter = new BattleCommandPresentationAdapter(
            cue =>
            {
                cueCount++;
                return new BattleCommandPresentationTween(
                    DOTween.Sequence().AppendCallback(() => { }),
                    cleanup: null);
            },
            () => 0f);
        var battleEnded = new BattlePhaseChangedSettlement(
            order: 0,
            BattleTurnPhase.EnemyAction,
            BattleTurnPhase.BattleEnded,
            roundNumberBefore: 3,
            roundNumberAfter: 3,
            currentActingEnemyIdBefore: new CombatantId(2001),
            currentActingEnemyIdAfter: null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 67,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { battleEnded });
        int completionCount = 0;

        Assert.Throws<ArgumentException>(
            () => ((IBattleCommandPresentation)adapter).Present(
                result,
                () => completionCount++));

        Assert.That(cueCount, Is.Zero);
        Assert.That(completionCount, Is.Zero);
    }

    /// <summary>确认旧句柄的失败反馈不会清除同一视图后来绑定的新待定句柄。</summary>
    [Test]
    public void CardVisual_OlderFailureDoesNotClearNewerPendingHandle()
    {
        using var coordinator = new BattleCommandSubmissionCoordinator();
        BattleCommandHandle olderHandle = coordinator.PreRegister(new StartBattleCommand());
        BattleCommandHandle newerHandle = coordinator.PreRegister(new StartBattleCommand());
        var cardObject = new GameObject("PendingHandleTestCard");
        HandCardVisual visual = cardObject.AddComponent<HandCardVisual>();
        try
        {
            visual.SetCommandPending(olderHandle);
            visual.SetCommandPending(newerHandle);

            visual.PlayCommandFailureFeedback(olderHandle);

            Assert.That(visual.IsCommandPending, Is.True);

            visual.PlayCommandFailureFeedback(newerHandle);

            Assert.That(visual.IsCommandPending, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cardObject);
        }
    }

    /// <summary>创建 adapter 牌堆路由测试使用的当前 UI 锚点。</summary>
    private static Text CreatePileText(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition)
    {
        var textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, worldPositionStays: false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 60f);
        rect.anchoredPosition = anchoredPosition;
        Text text = textObject.GetComponent<Text>();
        text.raycastTarget = false;
        return text;
    }

    /// <summary>为 adapter 纯 View 测试设置现有序列化引用。</summary>
    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }

    /// <summary>读取 adapter concrete View 测试所需的现有私有集合。</summary>
    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        return (T)field.GetValue(target);
    }
}
