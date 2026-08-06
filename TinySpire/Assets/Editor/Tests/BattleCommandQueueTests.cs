using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;

public sealed class BattleCommandQueueTests
{
    /// <summary>每个用例结束后释放测试工厂代建的敌人意图响应式资源。</summary>
    [TearDown]
    public void TearDown()
    {
        BattleCommandQueueTestFactory.DisposeOwnedEnemyIntents();
    }

    /// <summary>验证战斗开始前的玩家命令会在提交 seam 被拒绝，且不会占用权威序号。</summary>
    [Test]
    public void Submit_PlayerCommandBeforeBattleStart_RejectsWithoutQueueing()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new RejectUnexpectedPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);

        BattleCommandSubmissionResult result = queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));

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

        BattleCommandSubmissionResult result = queue.SubmitRegistered(new StartBattleCommand());

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
        queue.SubmitRegistered(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        BattleCommandSubmissionResult firstSubmission =
            queue.SubmitRegistered(new EndPlayerActionCommand(firstPlayer.Id));
        BattleCommandSubmissionResult secondSubmission =
            queue.SubmitRegistered(new EndPlayerActionCommand(secondPlayer.Id));

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

    /// <summary>验证表现完成后普通失败按权威序号直通，且不会伪造新的展示请求。</summary>
    [Test]
    public void PresentationCompletion_DrainsQueuedFailuresWithoutFakePresentations()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        BattleCommandSubmissionResult second = queue.SubmitRegistered(new StartBattleCommand());
        BattleCommandSubmissionResult third = queue.SubmitRegistered(new StartBattleCommand());

        presentation.CompleteNext();

        BattleCommandLifecycleEvent secondTerminal = lifecycle.RequireTerminal(second);
        BattleCommandLifecycleEvent thirdTerminal = lifecycle.RequireTerminal(third);
        Assert.That(presentation.Results.Count, Is.EqualTo(1));
        Assert.That(presentation.Results[0].AuthoritySequence, Is.EqualTo(1));
        Assert.That(presentation.Results[0].Succeeded, Is.True);
        Assert.That(secondTerminal.AuthoritySequence, Is.EqualTo(2));
        Assert.That(secondTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(secondTerminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.BattleAlreadyStarted));
        Assert.That(secondTerminal.Settlements, Is.Empty);
        Assert.That(thirdTerminal.AuthoritySequence, Is.EqualTo(3));
        Assert.That(thirdTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(thirdTerminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.BattleAlreadyStarted));
        Assert.That(thirdTerminal.Settlements, Is.Empty);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证外部不能提交敌人系统命令，拒绝不分配序号也不修改回合事实。</summary>
    [Test]
    public void Submit_ExternalCompleteEnemyAction_IsRejectedWithoutSequenceOrMutation()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        queue.SubmitRegistered(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        BattleCommandSubmissionResult rejected =
            queue.SubmitRegistered(new CompleteEnemyActionCommand(enemy.Id));
        BattleCommandSubmissionResult nextPlayerCommand =
            queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));

        Assert.That(rejected.Accepted, Is.False);
        Assert.That(rejected.AuthoritySequence, Is.Null);
        Assert.That(
            rejected.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.SystemCommandNotAuthorized));
        Assert.That(nextPlayerCommand.AuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(presentation.Results, Has.Count.EqualTo(1));

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
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
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
            cardZones,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();

        queue.SubmitRegistered(new EndPlayerActionCommand(firstPlayer.Id));
        BattleCommandSubmissionResult repeated =
            queue.SubmitRegistered(new EndPlayerActionCommand(firstPlayer.Id));

        Assert.That(presentation.Results[1].Succeeded, Is.True);
        Assert.That(firstZones.Hand, Is.Empty);
        Assert.That(firstZones.DiscardPile.Count, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));

        presentation.CompleteNext();

        BattleCommandLifecycleEvent repeatedTerminal = lifecycle.RequireTerminal(repeated);
        Assert.That(presentation.Results, Has.Count.EqualTo(2));
        Assert.That(repeatedTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(
            repeatedTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded));
        Assert.That(repeatedTerminal.Settlements, Is.Empty);
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
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
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
            new Dictionary<int, int> { [3001] = 1 },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();

        queue.SubmitRegistered(new EndPlayerActionCommand(firstPlayer.Id));
        BattleCommandSubmissionResult secondSubmission =
            queue.SubmitRegistered(new PlayCardCommand(secondPlayer.Id, secondPlayerCardId, secondPlayer.Id));

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
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();

        queue.SubmitRegistered(new EndPlayerActionCommand(firstPlayer.Id));
        queue.SubmitRegistered(new EndPlayerActionCommand(secondPlayer.Id));
        BattleCommandSubmissionResult latePlay =
            queue.SubmitRegistered(new PlayCardCommand(firstPlayer.Id, lateCardId, firstPlayer.Id));
        BattleEffectStateTestDriver.Kill(combatants, secondEnemy.Id, firstPlayer.Id);

        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.True);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondEnemy.Id));
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.True);

        presentation.CompleteNext();

        BattleCommandLifecycleEvent lateTerminal = lifecycle.RequireTerminal(latePlay);
        Assert.That(lateTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(
            lateTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InvalidTurnPhase));
        Assert.That(lateTerminal.Settlements, Is.Empty);
        Assert.That(presentation.Results, Has.Count.EqualTo(4));
        Assert.That(presentation.Results[3].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(firstZones.DiscardPile, Is.EqualTo(new[] { lateCardId }));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(firstEnemy.Id));

        queue.Dispose();
        firstZones.Dispose();
        secondZones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证排在敌人 continuation 后的同轮重复结束命令，不会跨轮结束下一轮。</summary>
    [Test]
    public void EndPlayerAction_QueuedDuplicateBehindEnemyContinuation_ExpiresAtNextRound()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 1, strength: 0);
        var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();

        queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));
        BattleCommandSubmissionResult duplicate =
            queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));

        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(2));

        presentation.CompleteNext();

        Assert.That(presentation.Results[2].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].HasEndedAction, Is.False);

        presentation.CompleteNext();

        BattleCommandLifecycleEvent duplicateTerminal = lifecycle.RequireTerminal(duplicate);
        Assert.That(presentation.Results, Has.Count.EqualTo(3));
        Assert.That(duplicateTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(
            duplicateTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerActionWindowExpired));
        Assert.That(duplicateTerminal.Settlements, Is.Empty);
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].HasEndedAction, Is.False);

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证旧出牌命令即使同一卡牌在下一轮重抽，也不会跨轮消耗能量或移动卡牌。</summary>
    [Test]
    public void PlayCard_WhenCardIsRedrawnNextRound_QueuedOldCommandExpires()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 1, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            new Dictionary<int, int> { [3001] = 1 },
            new[] { enemy.Id },
            initialHandCount: 1);
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        CardInstanceId queuedCardId = zones.Hand[0];

        queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));
        BattleCommandSubmissionResult stalePlay =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, queuedCardId, player.Id));

        presentation.CompleteNext();

        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(zones.Hand, Is.EqualTo(new[] { queuedCardId }));

        presentation.CompleteNext();

        BattleCommandLifecycleEvent staleTerminal = lifecycle.RequireTerminal(stalePlay);
        Assert.That(presentation.Results, Has.Count.EqualTo(3));
        Assert.That(staleTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(
            staleTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerActionWindowExpired));
        Assert.That(staleTerminal.Settlements, Is.Empty);
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(zones.Hand, Is.EqualTo(new[] { queuedCardId }));
        Assert.That(zones.DiscardPile, Is.Empty);

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证 Queue 自动 continuation 跳过死亡敌人，并按 Encounter 顺序推进存活敌人。</summary>
    [Test]
    public void AutomaticEnemyContinuation_SkipsDeadAndPreservesEncounterOrder()
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
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        BattleEffectStateTestDriver.Kill(combatants, player.Id, deadEnemy.Id);
        queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));

        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(firstLivingEnemy.Id));
        Assert.That(presentation.Results, Has.Count.EqualTo(2));

        presentation.CompleteNext();

        Assert.That(presentation.Results, Has.Count.EqualTo(3));
        Assert.That(presentation.Results[2].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(presentation.Results[2].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondLivingEnemy.Id));

        presentation.CompleteNext();

        Assert.That(presentation.Results, Has.Count.EqualTo(4));
        Assert.That(presentation.Results[3].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(presentation.Results[3].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));

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

        queue.SubmitRegistered(new StartBattleCommand());

        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(zones.Hand.Count, Is.EqualTo(2));

        presentation.CompleteNext();
        CardInstanceId playedCardId = zones.Hand[0];
        queue.SubmitRegistered(new PlayCardCommand(player.Id, playedCardId, player.Id));

        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(2));
        Assert.That(zones.Hand.Count, Is.EqualTo(1));

        presentation.CompleteNext();
        queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));

        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
        Assert.That(zones.Hand, Is.Empty);

        presentation.CompleteNext();

        Assert.That(presentation.Results[3].Succeeded, Is.True);
        Assert.That(presentation.Results[3].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
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

    /// <summary>验证生产入口只负责启动，后续敌人由 Queue continuation 按表现屏障依次推进。</summary>
    [Test]
    public void RuntimeDriver_StartOnly_AutomaticContinuationsRespectPresentationBarrier()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData firstEnemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        EnemyCombatantData secondEnemy = combatants.AddEnemy(templateId: 202, maxHealth: 22, strength: 0);
        var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        var presentation = new ControllableBattleCommandPresentation();
        var coordinator = new BattleCommandSubmissionCoordinator();
        var lifecycle = new List<BattleCommandLifecycleEvent>();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[] { firstEnemy.Id, secondEnemy.Id },
            coordinator: coordinator);
        using (coordinator.Lifecycle.Subscribe(lifecycle.Add))
        {
            var driver = new BattleCommandRuntimeDriver(queue, coordinator);
            driver.Start();

            Assert.That(presentation.Results, Has.Count.EqualTo(1));
            Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

            presentation.CompleteNext();
            queue.SubmitRegistered(new EndPlayerActionCommand(player.Id));

            Assert.That(presentation.Results, Has.Count.EqualTo(2));
            Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
            Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(firstEnemy.Id));
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
            Assert.That(lifecycle, Has.Some.Matches<BattleCommandLifecycleEvent>(item =>
                item.Stage == BattleCommandLifecycleStage.Queued &&
                item.CommandType == BattleCommandType.CompleteEnemyAction &&
                item.AuthoritySequence == 3));

            presentation.CompleteNext();

            Assert.That(presentation.Results, Has.Count.EqualTo(3));
            Assert.That(presentation.Results[2].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
            Assert.That(presentation.Results[2].AuthoritySequence, Is.EqualTo(3));
            Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
            Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondEnemy.Id));
            Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

            presentation.CompleteNext();

            Assert.That(presentation.Results, Has.Count.EqualTo(4));
            Assert.That(presentation.Results[3].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
            Assert.That(presentation.Results[3].AuthoritySequence, Is.EqualTo(4));
            Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
            Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

            presentation.CompleteNext();

            Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
            Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
            Assert.That(
                lifecycle.FindAll(item =>
                    item.Stage == BattleCommandLifecycleStage.Queued &&
                    item.CommandType == BattleCommandType.CompleteEnemyAction),
                Has.Count.EqualTo(2));
        }

        queue.Dispose();
        coordinator.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证费用一与二的卡牌按权威顺序执行后，能量归零且指定实例依次进入弃牌堆。</summary>
    [Test]
    public void PlayCards_InAuthorityOrder_SpendsThreeEnergyAndDiscardsInstances()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
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
            cardCosts,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();

        BattleCommandSubmissionResult firstSubmission =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, oneCostCardId, player.Id));
        BattleCommandSubmissionResult secondSubmission =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, twoCostCardId, player.Id));

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
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
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
            cardCosts,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        queue.SubmitRegistered(new PlayCardCommand(firstPlayer.Id, firstCardId, firstPlayer.Id));

        BattleCommandSubmissionResult secondSubmission =
            queue.SubmitRegistered(new PlayCardCommand(secondPlayer.Id, secondCardId, secondPlayer.Id));

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
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
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
            cardCosts,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        queue.SubmitRegistered(new PlayCardCommand(player.Id, firstCardId, player.Id));
        BattleCommandSubmissionResult overspend =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, secondCardId, player.Id));
        BattleTurnData turnAfterFirstCard = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutAfterFirstCard = zones.Layout.CurrentValue;

        presentation.CompleteNext();

        BattleCommandLifecycleEvent overspendTerminal = lifecycle.RequireTerminal(overspend);
        Assert.That(presentation.Results, Has.Count.EqualTo(2));
        Assert.That(overspendTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(
            overspendTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(overspendTerminal.Settlements, Is.Empty);
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
            cardCosts,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        BattleCommandSubmissionResult stalePlay =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, cardId, player.Id));
        zones.DiscardFromHand(cardId);
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        presentation.CompleteNext();

        BattleCommandLifecycleEvent staleTerminal = lifecycle.RequireTerminal(stalePlay);
        Assert.That(
            staleTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.CardNotInHand));
        Assert.That(staleTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(staleTerminal.Settlements, Is.Empty);
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeExecution));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeExecution));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证 Enemy 目标在排队期间死亡后，队首重校验失败且所有权威事实保持目标死亡后的原值。</summary>
    [Test]
    public void QueuedPlayCard_WhenEnemyTargetDiesBeforeHead_FailsWithoutMutation()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 1, strength: 0);
        EnemyCombatantData survivingEnemy = combatants.AddEnemy(templateId: 202, maxHealth: 5, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var cardCosts = new Dictionary<int, int> { [3001] = 1 };
        var targetRules = new Dictionary<int, cfg.battle.TargetRule>
        {
            [3001] = cfg.battle.TargetRule.Enemy
        };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id, survivingEnemy.Id },
            cardTargetRules: targetRules);
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        BattleCommandSubmissionResult playSubmission =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, cardId, enemy.Id));
        BattleEffectStateTestDriver.Kill(combatants, player.Id, enemy.Id);
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;
        var healthFactBeforeExecution = enemy.Health;
        int healthValueBeforeExecution = enemy.CurrentHealth;

        presentation.CompleteNext();

        BattleCommandLifecycleEvent playTerminal = lifecycle.RequireTerminal(playSubmission);
        Assert.That(
            playTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotAlive));
        Assert.That(playTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(playTerminal.Settlements, Is.Empty);
        Assert.That(playTerminal.AuthoritySequence, Is.EqualTo(playSubmission.AuthoritySequence));
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeExecution));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeExecution));
        Assert.That(zones.Hand, Is.EqualTo(new[] { cardId }));
        Assert.That(enemy.Health, Is.SameAs(healthFactBeforeExecution));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(healthValueBeforeExecution));

        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证合法 Enemy 出牌只扣一次能量、只移动一次指定实例，且不会提前执行目标效果。</summary>
    [Test]
    public void PlayCard_WithLegalEnemyTarget_SpendsAndMovesOnceWithoutChangingTargetHealth()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var cardCosts = new Dictionary<int, int> { [3001] = 1 };
        var targetRules = new Dictionary<int, cfg.battle.TargetRule>
        {
            [3001] = cfg.battle.TargetRule.Enemy
        };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            cardCosts,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            cardTargetRules: targetRules);
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        var healthBeforeExecution = enemy.Health;
        int healthValueBeforeExecution = enemy.CurrentHealth;

        queue.SubmitRegistered(new PlayCardCommand(player.Id, cardId, enemy.Id));

        Assert.That(presentation.Results[1].Succeeded, Is.True);
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(2));
        Assert.That(zones.Hand, Is.Empty);
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        Assert.That(enemy.Health, Is.SameAs(healthBeforeExecution));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(healthValueBeforeExecution));

        presentation.CompleteNext();

        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(2));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        Assert.That(enemy.Health, Is.SameAs(healthBeforeExecution));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(healthValueBeforeExecution));

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
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        BattleCommandSubmissionResult invalidActor =
            queue.SubmitRegistered(new PlayCardCommand(enemy.Id, cardId, enemy.Id));

        BattleCommandLifecycleEvent invalidActorTerminal = lifecycle.RequireTerminal(invalidActor);
        Assert.That(
            invalidActorTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InvalidPlayer));
        Assert.That(invalidActorTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(invalidActorTerminal.Settlements, Is.Empty);
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
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
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        BattleEffectStateTestDriver.Kill(combatants, player.Id, player.Id);
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        BattleCommandSubmissionResult deadPlayerPlay =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, cardId, player.Id));

        BattleCommandLifecycleEvent deadPlayerTerminal = lifecycle.RequireTerminal(deadPlayerPlay);
        Assert.That(
            deadPlayerTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerNotAlive));
        Assert.That(deadPlayerTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(deadPlayerTerminal.Settlements, Is.Empty);
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
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
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardCosts = new Dictionary<int, int> { [3001] = 1 };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            playerCardZones: null,
            cardCosts: cardCosts,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;

        BattleCommandSubmissionResult missingZonesPlay =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, cardId, player.Id));

        BattleCommandLifecycleEvent missingZonesTerminal = lifecycle.RequireTerminal(missingZonesPlay);
        Assert.That(
            missingZonesTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.PlayerCardZonesNotFound));
        Assert.That(missingZonesTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(missingZonesTerminal.Settlements, Is.Empty);
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
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
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3999 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id });
        using BattleCommandLifecycleExecutionRecorder lifecycle = queue.RecordExecutionLifecycle();
        queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        BattleTurnData turnBeforeExecution = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeExecution = zones.Layout.CurrentValue;

        BattleCommandSubmissionResult missingTemplatePlay =
            queue.SubmitRegistered(new PlayCardCommand(player.Id, cardId, player.Id));

        BattleCommandLifecycleEvent missingTemplateTerminal = lifecycle.RequireTerminal(missingTemplatePlay);
        Assert.That(
            missingTemplateTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.CardTemplateNotFound));
        Assert.That(missingTemplateTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(missingTemplateTerminal.Settlements, Is.Empty);
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
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

                   firstSubmission = queue.SubmitRegistered(new EndPlayerActionCommand(firstPlayer.Id));
                   secondSubmission = queue.SubmitRegistered(new EndPlayerActionCommand(secondPlayer.Id));
                   queueDuringExecution = queue.Queue.CurrentValue;
               }))
        {
            queue.SubmitRegistered(new StartBattleCommand());
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
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var firstZones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1);
        var secondZones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 2);
        firstZones.Draw(1);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData>
            {
                [firstPlayer.Id] = firstZones,
                [secondPlayer.Id] = secondZones,
            });
        queue.SubmitRegistered(new StartBattleCommand());
        queue.SubmitRegistered(new EndPlayerActionCommand(firstPlayer.Id));
        queue.SubmitRegistered(new EndPlayerActionCommand(secondPlayer.Id));

        presentation.CompleteNext();
        presentation.CompleteLastAgain();

        Assert.That(presentation.Results.Count, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

        queue.Dispose();
        firstZones.Dispose();
        secondZones.Dispose();
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
    private static readonly List<BattleEnemyIntentsData> OwnedEnemyIntents =
        new List<BattleEnemyIntentsData>();
    private static readonly ConditionalWeakTable<BattleCommandQueue, BattleCommandSubmissionCoordinator>
        Coordinators = new ConditionalWeakTable<BattleCommandQueue, BattleCommandSubmissionCoordinator>();

    /// <summary>用最小静态表和可选玩家卡区创建只供纯模型测试使用的命令队列。</summary>
    internal static BattleCommandQueue Create(
        BattleCombatantsData combatants,
        IBattleCommandPresentation presentation,
        IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones = null,
        IReadOnlyDictionary<int, int> cardCosts = null,
        IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder = null,
        int energyPerRound = 3,
        int initialHandCount = 0,
        BattleEnemyIntentsData enemyIntents = null,
        Tables tables = null,
        uint battleSeed = 1,
        IReadOnlyDictionary<int, cfg.battle.TargetRule> cardTargetRules = null,
        BattleCommandSubmissionCoordinator coordinator = null)
    {
        IReadOnlyDictionary<CombatantId, BattleCardZonesData> resolvedCardZones =
            playerCardZones ?? new Dictionary<CombatantId, BattleCardZonesData>();
        IReadOnlyList<CombatantId> resolvedEnemyIds =
            enemyCombatantIdsInEncounterOrder ?? Array.Empty<CombatantId>();
        Tables resolvedTables = tables ?? CreateTables(
            cardCosts,
            cardTargetRules,
            combatants,
            resolvedEnemyIds);
        BattleEnemyIntentsData resolvedEnemyIntents = enemyIntents;
        if (resolvedEnemyIntents == null)
        {
            resolvedEnemyIntents = new BattleEnemyIntentsData(
                combatants,
                resolvedEnemyIds,
                resolvedTables,
                battleSeed);
            OwnedEnemyIntents.Add(resolvedEnemyIntents);
        }

        BattleCommandSubmissionCoordinator resolvedCoordinator =
            coordinator ?? new BattleCommandSubmissionCoordinator();
        var queue = new BattleCommandQueue(
            combatants,
            resolvedCardZones,
            resolvedEnemyIds,
            resolvedEnemyIntents,
            resolvedTables,
            energyPerRound,
            initialHandCount,
            presentation,
            resolvedCoordinator);
        TrackCoordinator(queue, resolvedCoordinator);
        return queue;
    }

    /// <summary>把自建 Queue 与其唯一 coordinator 登记给共享预注册提交扩展。</summary>
    internal static void TrackCoordinator(
        BattleCommandQueue queue,
        BattleCommandSubmissionCoordinator coordinator)
    {
        if (queue == null)
            throw new ArgumentNullException(nameof(queue));
        if (coordinator == null)
            throw new ArgumentNullException(nameof(coordinator));

        Coordinators.Add(queue, coordinator);
    }

    /// <summary>读取测试工厂与指定 Queue 共同持有的唯一提交协调器。</summary>
    internal static BattleCommandSubmissionCoordinator GetCoordinator(BattleCommandQueue queue)
    {
        if (queue == null)
            throw new ArgumentNullException(nameof(queue));
        if (!Coordinators.TryGetValue(queue, out BattleCommandSubmissionCoordinator coordinator))
            throw new InvalidOperationException("Battle command queue was not created by the shared test factory.");

        return coordinator;
    }

    /// <summary>释放当前用例中由工厂创建、但不归命令队列所有的敌人意图聚合。</summary>
    internal static void DisposeOwnedEnemyIntents()
    {
        foreach (BattleEnemyIntentsData enemyIntents in OwnedEnemyIntents)
            enemyIntents.Dispose();

        OwnedEnemyIntents.Clear();
    }

    /// <summary>创建测试所需卡牌费用、目标规则与固定敌人行为的最小 Luban 静态表集合。</summary>
    private static Tables CreateTables(
        IReadOnlyDictionary<int, int> cardCosts,
        IReadOnlyDictionary<int, cfg.battle.TargetRule> cardTargetRules,
        BattleCombatantsData combatants,
        IReadOnlyList<CombatantId> enemyIds)
    {
        var cards = new JArray();
        if (cardCosts != null)
        {
            foreach (KeyValuePair<int, int> entry in cardCosts)
            {
                cfg.battle.TargetRule cardTargetRule = cardTargetRules != null &&
                                                       cardTargetRules.TryGetValue(
                                                           entry.Key,
                                                           out cfg.battle.TargetRule configuredTargetRule)
                    ? configuredTargetRule
                    : cfg.battle.TargetRule.Self;
                cards.Add(new JObject
                {
                    ["id"] = entry.Key,
                    ["external_key"] = $"TEST_COMMAND_QUEUE_CARD_{entry.Key}",
                    ["catalog_snapshot_key"] = "test-fixture",
                    ["name_i18n_key"] = $"battle.card.test_{entry.Key}.name",
                    ["description_i18n_key"] = $"battle.card.test_{entry.Key}.description",
                    ["upgraded_description_i18n_key"] = $"battle.card.test_{entry.Key}.description",
                    ["card_type"] = (int)(cardTargetRule == cfg.battle.TargetRule.Enemy
                        ? cfg.battle.CardType.Attack
                        : cfg.battle.CardType.Skill),
                    ["rarity"] = (int)cfg.battle.CardRarity.Basic,
                    ["cost"] = entry.Value,
                    ["cost_kind"] = (int)cfg.battle.CardCostKind.Fixed,
                    ["upgraded_cost"] = entry.Value,
                    ["target_rule"] = (int)cardTargetRule,
                    ["play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
                    ["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
                    ["has_upgrade"] = false,
                    ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                    ["effect_bindings"] = new JArray(),
                    ["illustration_key"] = string.Empty
                });
            }
        }

        var enemies = new JArray();
        var enemyTemplateIds = new HashSet<int>();
        foreach (CombatantId enemyId in enemyIds)
        {
            if (!combatants.TryGet(enemyId, out CombatantData combatant) ||
                !(combatant is EnemyCombatantData) ||
                !enemyTemplateIds.Add(combatant.TemplateId))
            {
                continue;
            }

            enemies.Add(new JObject
            {
                ["id"] = combatant.TemplateId,
                ["name_i18n_key"] = $"battle.enemy.test_{combatant.TemplateId}.name",
                ["max_health"] = combatant.MaxHealth,
                ["base_strength"] = combatant.Strength.CurrentValue,
                ["view_prefab_key"] = string.Empty,
                ["behavior_group_id"] = 6001
            });
        }

        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = enemies,
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = cards,
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4999,\"effect_type\":1,\"attribute\":0,\"value\":1}]"),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":0}]")
        };
        return new Tables(tableName => data[tableName]);
    }
}

internal static class BattleCommandQueueTestSubmissionExtensions
{
    /// <summary>通过共享 coordinator 预注册同一命令引用，再调用生产唯一 Submit seam。</summary>
    internal static BattleCommandSubmissionResult SubmitRegistered(
        this BattleCommandQueue queue,
        BattleCommand command)
    {
        if (queue == null)
            throw new ArgumentNullException(nameof(queue));
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        BattleCommandSubmissionCoordinator coordinator =
            BattleCommandQueueTestFactory.GetCoordinator(queue);
        coordinator.PreRegister(command);
        return queue.Submit(command);
    }

    /// <summary>订阅指定 Queue 的真实 lifecycle，供普通失败零表现测试读取终态。</summary>
    internal static BattleCommandLifecycleExecutionRecorder RecordExecutionLifecycle(
        this BattleCommandQueue queue)
    {
        return new BattleCommandLifecycleExecutionRecorder(
            BattleCommandQueueTestFactory.GetCoordinator(queue));
    }
}

/// <summary>只记录 coordinator 发布的真实执行终态，不伪造 presentation result。</summary>
internal sealed class BattleCommandLifecycleExecutionRecorder : IDisposable
{
    private readonly IDisposable _subscription;
    private readonly List<BattleCommandLifecycleEvent> _events =
        new List<BattleCommandLifecycleEvent>();

    /// <summary>按 Queue 发布顺序暴露测试期生命周期事件。</summary>
    internal IReadOnlyList<BattleCommandLifecycleEvent> Events => _events;

    /// <summary>订阅本场唯一 coordinator 的生命周期流。</summary>
    internal BattleCommandLifecycleExecutionRecorder(
        BattleCommandSubmissionCoordinator coordinator)
    {
        if (coordinator == null)
            throw new ArgumentNullException(nameof(coordinator));

        _subscription = coordinator.Lifecycle.Subscribe(_events.Add);
    }

    /// <summary>按已接受序号取得唯一非 Queued 终态；尚未终结或重复终态时立即失败。</summary>
    internal BattleCommandLifecycleEvent RequireTerminal(
        BattleCommandSubmissionResult submission)
    {
        if (!submission.Accepted || !submission.AuthoritySequence.HasValue)
            throw new ArgumentException("只有已接受命令才能读取执行终态。", nameof(submission));

        BattleCommandLifecycleEvent resolved = null;
        foreach (BattleCommandLifecycleEvent lifecycleEvent in _events)
        {
            if (lifecycleEvent.AuthoritySequence != submission.AuthoritySequence.Value ||
                lifecycleEvent.Stage == BattleCommandLifecycleStage.Queued)
            {
                continue;
            }

            if (resolved != null)
            {
                throw new InvalidOperationException(
                    $"Authority sequence {submission.AuthoritySequence.Value} published multiple terminal events.");
            }

            resolved = lifecycleEvent;
        }

        return resolved ?? throw new InvalidOperationException(
            $"Authority sequence {submission.AuthoritySequence.Value} has not published a terminal event.");
    }

    /// <summary>停止记录 lifecycle，避免测试之间保留订阅。</summary>
    public void Dispose()
    {
        _subscription.Dispose();
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
