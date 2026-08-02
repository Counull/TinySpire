using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEngine;

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
        deltaTime = 1f;
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

        deltaTime = 1f;
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
}
