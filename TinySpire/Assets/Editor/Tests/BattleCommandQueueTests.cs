using System;
using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;

public sealed class BattleCommandQueueTests
{
    /// <summary>验证战斗开始前的玩家命令会在提交 seam 被拒绝，且不会占用权威序号。</summary>
    [Test]
    public void Submit_PlayerCommandBeforeBattleStart_RejectsWithoutQueueing()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new RejectUnexpectedPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);

        BattleCommandSubmissionResult result = queue.Submit(new EndPlayerActionCommand(player.Id));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.AuthoritySequence, Is.Null);
        Assert.That(result.FailureReason, Is.EqualTo(BattleCommandSubmissionFailureReason.BattleNotStarted));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.NotStarted));

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证开始命令取得首个权威序号，并在权威写入后等待表现完成。</summary>
    [Test]
    public void Submit_StartBattle_AssignsFirstSequenceAndWaitsForPresentation()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);

        BattleCommandSubmissionResult result = queue.Submit(new StartBattleCommand());

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.AuthoritySequence, Is.EqualTo(1));
        Assert.That(result.FailureReason, Is.EqualTo(BattleCommandSubmissionFailureReason.None));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.CurrentCommandType, Is.EqualTo(BattleCommandType.StartBattle));
        Assert.That(queue.Queue.CurrentValue.CurrentSubmitterId, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));
        Assert.That(presentation.Results.Count, Is.EqualTo(1));
        Assert.That(presentation.Results[0].AuthoritySequence, Is.EqualTo(1));
        Assert.That(presentation.Results[0].Succeeded, Is.True);

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证当前命令等待表现时，不同玩家仍可提交且不会提前修改共享事实。</summary>
    [Test]
    public void Submit_WhilePresentationIsPending_AcceptsPlayersWithoutExecutingThem()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        queue.Submit(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        BattleCommandSubmissionResult firstSubmission =
            queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));
        BattleCommandSubmissionResult secondSubmission =
            queue.Submit(new EndPlayerActionCommand(secondPlayer.Id));

        Assert.That(firstSubmission.Accepted, Is.True);
        Assert.That(firstSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(secondSubmission.Accepted, Is.True);
        Assert.That(secondSubmission.AuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.False);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);
        Assert.That(presentation.Results.Count, Is.EqualTo(1));

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证表现完成信号只推进一条命令，且执行结果严格遵循权威序号。</summary>
    [Test]
    public void PresentationCompletion_AdvancesExactlyOneCommandInAuthorityOrder()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());

        presentation.CompleteNext();

        Assert.That(presentation.Results.Count, Is.EqualTo(2));
        Assert.That(presentation.Results[0].AuthoritySequence, Is.EqualTo(1));
        Assert.That(presentation.Results[0].Succeeded, Is.True);
        Assert.That(presentation.Results[1].AuthoritySequence, Is.EqualTo(2));
        Assert.That(presentation.Results[1].Succeeded, Is.False);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

        presentation.CompleteNext();

        Assert.That(presentation.Results.Count, Is.EqualTo(3));
        Assert.That(presentation.Results[2].AuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证玩家阶段收到敌人完成命令时明确失败，且不会修改回合事实。</summary>
    [Test]
    public void CompleteEnemyAction_DuringPlayerAction_FailsWithoutChangingSharedFacts()
    {
        var combatants = new BattleCombatantsData();
        combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        queue.Submit(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        queue.Submit(new CompleteEnemyActionCommand(enemy.Id));

        presentation.CompleteNext();
        Assert.That(presentation.Results[1].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(presentation.Results[1].SubmitterId, Is.Null);
        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InvalidTurnPhase));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证同一玩家重复结束只弃牌一次，第二条命令执行失败且不推进阶段。</summary>
    [Test]
    public void EndPlayerAction_WhenRepeated_FailsWithoutDiscardingOrAdvancingAgain()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var firstZones = new BattleCardZonesData(new[] { 3001, 3001 }, shuffleSeed: 1);
        var secondZones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 2);
        firstZones.Draw(2);
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [firstPlayer.Id] = firstZones,
            [secondPlayer.Id] = secondZones
        };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();

        queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));
        queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));

        Assert.That(presentation.Results[1].Succeeded, Is.True);
        Assert.That(firstZones.Hand, Is.Empty);
        Assert.That(firstZones.DiscardPile.Count, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.False);
        Assert.That(
            presentation.Results[2].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded));
        Assert.That(firstZones.DiscardPile.Count, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);

        queue.Dispose();
        firstZones.Dispose();
        secondZones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证一名玩家结束行动并弃牌后，另一名玩家仍可在同一玩家阶段按队列出牌。</summary>
    [Test]
    public void EndPlayerAction_WhileAnotherPlayerRemains_AllowsTheirQueuedCard()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var firstZones = new BattleCardZonesData(new[] { 3001, 3001 }, shuffleSeed: 1);
        var secondZones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 2);
        firstZones.Draw(2);
        secondZones.Draw(1);
        CardInstanceId secondPlayerCardId = secondZones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [firstPlayer.Id] = firstZones,
            [secondPlayer.Id] = secondZones
        };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            new Dictionary<int, int> { [3001] = 1 });
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();

        queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));
        BattleCommandSubmissionResult secondSubmission =
            queue.Submit(new PlayCardCommand(secondPlayer.Id, secondPlayerCardId));

        Assert.That(presentation.Results[1].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.True);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);
        Assert.That(firstZones.Hand, Is.Empty);
        Assert.That(firstZones.DiscardPile.Count, Is.EqualTo(2));
        Assert.That(secondSubmission.Accepted, Is.True);
        Assert.That(secondZones.Hand, Is.EqualTo(new[] { secondPlayerCardId }));

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.True);
        Assert.That(secondZones.Hand, Is.Empty);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].Energy, Is.EqualTo(2));

        queue.Dispose();
        firstZones.Dispose();
        secondZones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证全体玩家结束后按 Encounter 顺序进入敌人阶段，且已结束玩家的旧出牌不会再执行。</summary>
    [Test]
    public void EndAllPlayers_EntersFirstEnemyInEncounterOrderAndRejectsLatePlay()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        EnemyCombatantData firstEnemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        EnemyCombatantData secondEnemy = combatants.AddEnemy(templateId: 202, maxHealth: 22, strength: 0);
        var firstZones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1);
        var secondZones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 2);
        firstZones.Draw(1);
        secondZones.Draw(1);
        CardInstanceId lateCardId = firstZones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [firstPlayer.Id] = firstZones,
            [secondPlayer.Id] = secondZones
        };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            new Dictionary<int, int> { [3001] = 1 },
            enemyCombatantIdsInEncounterOrder: new[] { secondEnemy.Id, firstEnemy.Id });
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();

        queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));
        queue.Submit(new EndPlayerActionCommand(secondPlayer.Id));
        queue.Submit(new PlayCardCommand(firstPlayer.Id, lateCardId));

        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.True);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondEnemy.Id));
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.True);

        presentation.CompleteNext();

        Assert.That(presentation.Results[3].Succeeded, Is.False);
        Assert.That(
            presentation.Results[3].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InvalidTurnPhase));
        Assert.That(firstZones.DiscardPile, Is.EqualTo(new[] { lateCardId }));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondEnemy.Id));

        queue.Dispose();
        firstZones.Dispose();
        secondZones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证死亡敌人会跳过，错误或重复完成命令不会越过当前行动敌人。</summary>
    [Test]
    public void CompleteEnemyAction_SkipsDeadAndRejectsWrongOrRepeatedEnemy()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData deadEnemy = combatants.AddEnemy(templateId: 201, maxHealth: 1, strength: 0);
        EnemyCombatantData firstLivingEnemy = combatants.AddEnemy(templateId: 202, maxHealth: 20, strength: 0);
        EnemyCombatantData secondLivingEnemy = combatants.AddEnemy(templateId: 203, maxHealth: 22, strength: 0);
        var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[]
            {
                deadEnemy.Id,
                firstLivingEnemy.Id,
                secondLivingEnemy.Id
            });
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        combatants.ApplyDamage(deadEnemy.Id, 1);
        queue.Submit(new EndPlayerActionCommand(player.Id));
        queue.Submit(new CompleteEnemyActionCommand(secondLivingEnemy.Id));
        queue.Submit(new CompleteEnemyActionCommand(firstLivingEnemy.Id));
        queue.Submit(new CompleteEnemyActionCommand(firstLivingEnemy.Id));

        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(firstLivingEnemy.Id));

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.False);
        Assert.That(
            presentation.Results[2].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.EnemyNotCurrentActor));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(firstLivingEnemy.Id));

        presentation.CompleteNext();

        Assert.That(presentation.Results[3].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondLivingEnemy.Id));

        presentation.CompleteNext();

        Assert.That(presentation.Results[4].Succeeded, Is.False);
        Assert.That(
            presentation.Results[4].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.EnemyNotCurrentActor));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondLivingEnemy.Id));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证最后敌人完成后进入下一轮，并重置能量、结束标记及目标手牌。</summary>
    [Test]
    public void CompleteLastEnemy_StartsNextRoundWithResetEnergyAndTargetHand()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var zones = new BattleCardZonesData(
            new[] { 3001, 3001, 3001, 3001, 3001, 3001 },
            shuffleSeed: 1);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            new Dictionary<int, int> { [3001] = 1 },
            new[] { enemy.Id },
            initialHandCount: 2);

        queue.Submit(new StartBattleCommand());

        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(zones.Hand.Count, Is.EqualTo(2));

        presentation.CompleteNext();
        CardInstanceId playedCardId = zones.Hand[0];
        queue.Submit(new PlayCardCommand(player.Id, playedCardId));

        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(2));
        Assert.That(zones.Hand.Count, Is.EqualTo(1));

        presentation.CompleteNext();
        queue.Submit(new EndPlayerActionCommand(player.Id));

        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
        Assert.That(zones.Hand, Is.Empty);

        presentation.CompleteNext();
        queue.Submit(new CompleteEnemyActionCommand(enemy.Id));

        Assert.That(presentation.Results[3].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].HasEndedAction, Is.False);
        Assert.That(zones.Hand.Count, Is.EqualTo(2));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证生产逐帧入口每帧只完成一名敌人，不会在同帧连续越过 Encounter 顺序。</summary>
    [Test]
    public void RuntimeDriver_Tick_CompletesAtMostOneEnemyPerFrame()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData firstEnemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        EnemyCombatantData secondEnemy = combatants.AddEnemy(templateId: 202, maxHealth: 22, strength: 0);
        var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            new ImmediateBattleCommandPresentation(),
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[] { firstEnemy.Id, secondEnemy.Id });
        var driver = new BattleCommandRuntimeDriver(queue);
        driver.Start();
        queue.Submit(new EndPlayerActionCommand(player.Id));

        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(firstEnemy.Id));

        driver.Tick();

        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondEnemy.Id));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));

        driver.Tick();

        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));

        driver.Tick();

        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证费用一与二的卡牌按权威顺序执行后，能量归零且指定实例依次进入弃牌堆。</summary>
    [Test]
    public void PlayCards_InAuthorityOrder_SpendsThreeEnergyAndDiscardsInstances()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001, 3002 }, shuffleSeed: 1234);
        zones.Draw(2);
        CardInstanceId oneCostCardId = FindCardByTemplate(zones, 3001);
        CardInstanceId twoCostCardId = FindCardByTemplate(zones, 3002);
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var cardCosts = new Dictionary<int, int> { [3001] = 1, [3002] = 2 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();

        BattleCommandSubmissionResult firstSubmission =
            queue.Submit(new PlayCardCommand(player.Id, oneCostCardId));
        BattleCommandSubmissionResult secondSubmission =
            queue.Submit(new PlayCardCommand(player.Id, twoCostCardId));

        Assert.That(firstSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(secondSubmission.AuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(2));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { oneCostCardId }));
        Assert.That(zones.Hand, Does.Contain(twoCostCardId));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.Zero);
        Assert.That(zones.Hand, Is.Empty);
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { oneCostCardId, twoCostCardId }));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证首张牌等待展示时另一玩家仍可提交，但其能量和卡区不会提前变化。</summary>
    [Test]
    public void Submit_WhilePlayPresentationIsPending_AcceptsAnotherPlayerWithoutEarlyExecution()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var firstZones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1);
        var secondZones = new BattleCardZonesData(new[] { 3002 }, shuffleSeed: 2);
        firstZones.Draw(1);
        secondZones.Draw(1);
        CardInstanceId firstCardId = firstZones.Hand[0];
        CardInstanceId secondCardId = secondZones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData>
        {
            [firstPlayer.Id] = firstZones,
            [secondPlayer.Id] = secondZones
        };
        var cardCosts = new Dictionary<int, int> { [3001] = 1, [3002] = 1 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        queue.Submit(new PlayCardCommand(firstPlayer.Id, firstCardId));

        BattleCommandSubmissionResult secondSubmission =
            queue.Submit(new PlayCardCommand(secondPlayer.Id, secondCardId));

        Assert.That(secondSubmission.Accepted, Is.True);
        Assert.That(secondSubmission.AuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].Energy, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].Energy, Is.EqualTo(3));
        Assert.That(firstZones.Hand, Is.Empty);
        Assert.That(secondZones.Hand, Is.EqualTo(new[] { secondCardId }));
        Assert.That(presentation.Results.Count, Is.EqualTo(2));

        presentation.CompleteNext();

        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].Energy, Is.EqualTo(2));
        Assert.That(secondZones.Hand, Is.Empty);
        Assert.That(secondZones.DiscardPile, Is.EqualTo(new[] { secondCardId }));
        Assert.That(presentation.Results[2].Succeeded, Is.True);

        queue.Dispose();
        firstZones.Dispose();
        secondZones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证同一玩家基于旧能量排队的后续命令会重新校验，失败时不再移动卡牌。</summary>
    [Test]
    public void QueuedPlayCard_RevalidatesEnergyAndRejectsOverspendWithoutMutation()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3002, 3002 }, shuffleSeed: 1234);
        zones.Draw(2);
        CardInstanceId firstCardId = zones.Hand[0];
        CardInstanceId secondCardId = zones.Hand[1];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var cardCosts = new Dictionary<int, int> { [3002] = 2 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        queue.Submit(new PlayCardCommand(player.Id, firstCardId));
        queue.Submit(new PlayCardCommand(player.Id, secondCardId));
        BattleTurnData turnAfterFirstCard = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutAfterFirstCard = zones.Layout.CurrentValue;

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.False);
        Assert.That(
            presentation.Results[2].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterFirstCard));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(1));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutAfterFirstCard));
        Assert.That(zones.Hand, Is.EqualTo(new[] { secondCardId }));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证卡牌在排队期间离开手牌后，执行失败且不扣能量或再次改变卡区。</summary>
    [Test]
    public void QueuedPlayCard_WhenCardLeavesHandBeforeExecution_FailsWithoutMutation()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var cardCosts = new Dictionary<int, int> { [3001] = 1 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts);
        queue.Submit(new StartBattleCommand());
        queue.Submit(new PlayCardCommand(player.Id, cardId));
        zones.DiscardFromHand(cardId);
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        presentation.CompleteNext();

        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.CardNotInHand));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeExecution));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeExecution));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证敌人标识不能冒充玩家出牌，失败时玩家能量与卡区保持不变。</summary>
    [Test]
    public void PlayCard_WithEnemyActor_FailsWithoutChangingPlayerFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var cardCosts = new Dictionary<int, int> { [3001] = 1 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        queue.Submit(new PlayCardCommand(enemy.Id, cardId));

        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InvalidPlayer));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeExecution));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeExecution));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证死亡玩家的排队出牌在执行期失败，且不扣能量或移动卡牌。</summary>
    [Test]
    public void PlayCard_WithDeadPlayer_FailsWithoutChangingSharedFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 1, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var cardCosts = new Dictionary<int, int> { [3001] = 1 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        combatants.ApplyDamage(player.Id, 1);
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        queue.Submit(new PlayCardCommand(player.Id, cardId));

        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerNotAlive));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeExecution));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeExecution));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证缺少该玩家卡区映射时返回明确失败，不会把其他卡区当作其权威事实。</summary>
    [Test]
    public void PlayCard_WithoutActorCardZones_FailsWithoutChangingTurn()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardCosts = new Dictionary<int, int> { [3001] = 1 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            playerCardZones: null,
            cardCosts: cardCosts);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;

        queue.Submit(new PlayCardCommand(player.Id, cardId));

        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerCardZonesNotFound));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeExecution));
        Assert.That(zones.Hand, Is.EqualTo(new[] { cardId }));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证运行时实例引用的静态卡牌模板缺失时失败，不扣能量也不移动实例。</summary>
    [Test]
    public void PlayCard_WithMissingTemplate_FailsWithoutChangingSharedFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3999 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        queue.Submit(new PlayCardCommand(player.Id, cardId));

        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.CardTemplateNotFound));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeExecution));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeExecution));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证当前命令执行期间可以重入提交，且执行与等待表现事实不会混淆。</summary>
    [Test]
    public void Submit_DuringCurrentExecution_QueuesWithoutWaitingOrChangingPlayerFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        BattleCommandSubmissionResult firstSubmission = default;
        BattleCommandSubmissionResult secondSubmission = default;
        BattleCommandQueueData queueDuringExecution = null;

        using (queue.Turn.Skip(1).Subscribe(turn =>
               {
                   // BattleStart 的同步发布发生在队首命令执行尚未结束时。
                   if (turn.Phase != BattleTurnPhase.BattleStart)
                       return;

                   firstSubmission = queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));
                   secondSubmission = queue.Submit(new EndPlayerActionCommand(secondPlayer.Id));
                   queueDuringExecution = queue.Queue.CurrentValue;
               }))
        {
            queue.Submit(new StartBattleCommand());
        }

        Assert.That(firstSubmission.Accepted, Is.True);
        Assert.That(firstSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(secondSubmission.Accepted, Is.True);
        Assert.That(secondSubmission.AuthoritySequence, Is.EqualTo(3));
        Assert.That(queueDuringExecution.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queueDuringExecution.PendingCount, Is.EqualTo(2));
        Assert.That(queueDuringExecution.IsWaitingForPresentation, Is.False);
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.False);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(presentation.Results.Count, Is.EqualTo(1));

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证过期的重复表现回调不会越过正在等待的当前命令。</summary>
    [Test]
    public void PresentationCompletion_WhenRepeated_DoesNotSkipCurrentCommand()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());

        presentation.CompleteNext();
        presentation.CompleteLastAgain();

        Assert.That(presentation.Results.Count, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>按静态模板标识返回当前手牌中的运行时实例，测试数据不完整时立即失败。</summary>
    private static CardInstanceId FindCardByTemplate(BattleCardZonesData zones, int templateId)
    {
        foreach (CardInstanceId cardId in zones.Hand)
        {
            if (zones.Cards[cardId].TemplateId == templateId)
                return cardId;
        }

        Assert.Fail($"Template {templateId} was not found in the test hand.");
        return default;
    }

    private sealed class RejectUnexpectedPresentation : IBattleCommandPresentation
    {
        /// <summary>任何展示请求都表示被拒绝的命令错误地进入了执行流程。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            Assert.Fail($"Rejected command unexpectedly reached presentation: {result.AuthoritySequence}.");
        }
    }

}

internal static class BattleCommandQueueTestFactory
{
    /// <summary>用最小静态表和可选玩家卡区创建只供纯模型测试使用的命令队列。</summary>
    internal static BattleCommandQueue Create(
        BattleCombatantsData combatants,
        IBattleCommandPresentation presentation,
        IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones = null,
        IReadOnlyDictionary<int, int> cardCosts = null,
        IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder = null,
        int energyPerRound = 3,
        int initialHandCount = 0)
    {
        IReadOnlyDictionary<CombatantId, BattleCardZonesData> resolvedCardZones =
            playerCardZones ?? new Dictionary<CombatantId, BattleCardZonesData>();
        return new BattleCommandQueue(
            combatants,
            resolvedCardZones,
            enemyCombatantIdsInEncounterOrder ?? Array.Empty<CombatantId>(),
            CreateTables(cardCosts),
            energyPerRound,
            initialHandCount,
            presentation);
    }

    /// <summary>创建仅填充测试所需卡牌费用、其余表为空的 Luban 静态表集合。</summary>
    private static Tables CreateTables(IReadOnlyDictionary<int, int> cardCosts)
    {
        var cards = new JArray();
        if (cardCosts != null)
        {
            foreach (KeyValuePair<int, int> entry in cardCosts)
            {
                cards.Add(new JObject
                {
                    ["id"] = entry.Key,
                    ["name_i18n_key"] = $"battle.card.test_{entry.Key}.name",
                    ["description_i18n_key"] = $"battle.card.test_{entry.Key}.description",
                    ["cost"] = entry.Value,
                    ["target_rule"] = 0,
                    ["effect_bindings"] = new JArray(),
                    ["illustration_key"] = string.Empty
                });
            }
        }

        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = new JArray(),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = cards,
            ["battle_tbcardeffect"] = new JArray(),
            ["battle_tbencounter"] = new JArray()
        };
        return new Tables(tableName => data[tableName]);
    }
}

internal sealed class ControllableBattleCommandPresentation : IBattleCommandPresentation
{
    private readonly Queue<Action> _completions = new Queue<Action>();
    private Action _lastCompleted;

    /// <summary>按收到顺序保存的权威执行结果。</summary>
    internal List<BattleCommandExecutionResult> Results { get; } =
        new List<BattleCommandExecutionResult>();

    /// <summary>记录当前展示结果，并故意保持未完成以观察等待事实。</summary>
    public void Present(BattleCommandExecutionResult result, Action onCompleted)
    {
        Results.Add(result);
        _completions.Enqueue(onCompleted);
    }

    /// <summary>完成最早收到的展示请求，模拟表现层按顺序回报完成。</summary>
    internal void CompleteNext()
    {
        _lastCompleted = _completions.Dequeue();
        _lastCompleted.Invoke();
    }

    /// <summary>再次触发最近完成的回调，用于验证过期信号不会重复推进。</summary>
    internal void CompleteLastAgain()
    {
        _lastCompleted.Invoke();
    }
}
