using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleCommandQueueM8DTests
{
    /// <summary>验证玩家回合开始严格按清 Block、恢复能量、抽牌、发布阶段的真实写入顺序结算。</summary>
    [Test]
    public void StartBattle_PlayerRoundStart_ClearsBlockBeforeEnergyAndDraw()
    {
        using (var scenario = new M8DQueueScenario(
                   enemyCount: 1,
                   deckTemplateIds: new[]
                   {
                       M8DQueueScenario.LethalCardTemplateId,
                       M8DQueueScenario.LethalCardTemplateId,
                   },
                   initialHandCount: 2))
        {
            scenario.ApplyBlock(scenario.Player.Id, scenario.Player.Id, amount: 4);

            BattleCommandSubmissionResult submission =
                scenario.Queue.SubmitRegistered(new StartBattleCommand());

            Assert.That(submission.Accepted, Is.True);
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(1));
            BattleCommandExecutionResult result = scenario.Presentation.Results[0];
            Assert.That(result.Succeeded, Is.True);
            AssertSettlementTypes(
                result,
                BattleSettlementRecordType.BlockCleared,
                BattleSettlementRecordType.EnergyRefilled,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.BattlePhaseChanged);
            AssertContinuousOrders(result.Settlements);

            var cleared = result.Settlements[0] as BattleBlockClearedSettlement;
            var refilled = result.Settlements[1] as BattleEnergyRefilledSettlement;
            Assert.That(cleared, Is.Not.Null);
            Assert.That(cleared.TargetId, Is.EqualTo(scenario.Player.Id));
            Assert.That(cleared.BlockBefore, Is.EqualTo(4));
            Assert.That(cleared.BlockAfter, Is.Zero);
            Assert.That(refilled, Is.Not.Null);
            Assert.That(refilled.SourceId, Is.EqualTo(scenario.Player.Id));
            Assert.That(refilled.EnergyBefore, Is.Zero);
            Assert.That(refilled.EnergyAfter, Is.EqualTo(3));
            Assert.That(scenario.Player.CurrentBlock, Is.Zero);
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(2));
        }
    }

    /// <summary>验证结束玩家行动先按手牌原顺序弃牌，再让该玩家 Vulnerable 衰减一次。</summary>
    [Test]
    public void EndPlayerAction_DiscardsHandBeforeReducingVulnerable()
    {
        using (var scenario = new M8DQueueScenario(
                   enemyCount: 1,
                   deckTemplateIds: new[]
                   {
                       M8DQueueScenario.LethalCardTemplateId,
                       M8DQueueScenario.LethalCardTemplateId,
                   },
                   initialHandCount: 2))
        {
            scenario.StartAndCompleteFeedback();
            scenario.ApplyVulnerable(scenario.FirstEnemy.Id, scenario.Player.Id, amount: 2);
            CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
            int enemyHealthBefore = scenario.FirstEnemy.CurrentHealth;
            EnemyIntentLayoutData intentBefore = scenario.Intents.Layout.CurrentValue;

            scenario.Queue.SubmitRegistered(new EndPlayerActionCommand(scenario.Player.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            AssertSettlementTypes(
                result,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.StatusReduced,
                BattleSettlementRecordType.BattlePhaseChanged);
            AssertContinuousOrders(result.Settlements);
            Assert.That(((BattleCardMovedSettlement)result.Settlements[0]).CardId, Is.EqualTo(handBefore[0]));
            Assert.That(((BattleCardMovedSettlement)result.Settlements[1]).CardId, Is.EqualTo(handBefore[1]));
            var reduced = result.Settlements[2] as BattleStatusReducedSettlement;
            Assert.That(reduced, Is.Not.Null);
            Assert.That(reduced.Status, Is.EqualTo(BattleStatusType.Vulnerable));
            Assert.That(reduced.TargetId, Is.EqualTo(scenario.Player.Id));
            Assert.That(reduced.ValueBefore, Is.EqualTo(2));
            Assert.That(reduced.ValueAfter, Is.EqualTo(1));
            Assert.That(scenario.Player.CurrentVulnerable, Is.EqualTo(1));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(handBefore));
            Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(enemyHealthBefore));
            Assert.That(scenario.Intents.Layout.CurrentValue, Is.SameAs(intentBefore));
            Assert.That(scenario.Queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        }
    }

    /// <summary>验证单敌 Queue 事务按清 Block、Effect、Vulnerable、Intent 的顺序提交，且反馈期间不执行后续命令。</summary>
    [Test]
    public void EnemyAction_SingleEnemy_CommitsBlockEffectVulnerableIntentInOrder()
    {
        using (var scenario = new M8DQueueScenario(enemyCount: 1))
        {
            scenario.StartAndCompleteFeedback();
            scenario.ApplyBlock(scenario.Player.Id, scenario.FirstEnemy.Id, amount: 8);
            scenario.ApplyVulnerable(scenario.Player.Id, scenario.FirstEnemy.Id, amount: 2);
            scenario.ApplyBlock(scenario.FirstEnemy.Id, scenario.Player.Id, amount: 2);

            scenario.Queue.SubmitRegistered(new EndPlayerActionCommand(scenario.Player.Id));
            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(3));
            BattleCommandExecutionResult result = scenario.Presentation.Results[2];
            Assert.That(result.Succeeded, Is.True);
            AssertSettlementPrefix(
                result,
                BattleSettlementRecordType.BlockCleared,
                BattleSettlementRecordType.DamageApplied,
                BattleSettlementRecordType.StatusReduced,
                BattleSettlementRecordType.EnemyIntentAdvanced);
            AssertContinuousOrders(result.Settlements);

            var damage = result.Settlements[1] as BattleDamageAppliedSettlement;
            var reduced = result.Settlements[2] as BattleStatusReducedSettlement;
            var intent = result.Settlements[3] as BattleEnemyIntentAdvancedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
            Assert.That(damage.TargetId, Is.EqualTo(scenario.Player.Id));
            Assert.That(damage.AttackValue, Is.EqualTo(6));
            Assert.That(damage.BlockBefore, Is.EqualTo(2));
            Assert.That(damage.BlockAfter, Is.Zero);
            Assert.That(damage.HealthBefore, Is.EqualTo(30));
            Assert.That(damage.HealthAfter, Is.EqualTo(26));
            Assert.That(reduced, Is.Not.Null);
            Assert.That(reduced.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
            Assert.That(reduced.ValueBefore, Is.EqualTo(2));
            Assert.That(reduced.ValueAfter, Is.EqualTo(1));
            Assert.That(intent, Is.Not.Null);
            Assert.That(intent.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
            Assert.That(intent.CompletedBehaviorId, Is.EqualTo(M8DQueueScenario.FirstAttackBehaviorId));
            Assert.That(intent.NextBehaviorId, Is.EqualTo(M8DQueueScenario.FirstAttackBehaviorId));
            Assert.That(scenario.FirstEnemy.CurrentBlock, Is.Zero);
            Assert.That(scenario.FirstEnemy.CurrentVulnerable, Is.EqualTo(1));
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(26));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
            Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
            Assert.That(scenario.Queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        }
    }

    /// <summary>验证单敌结算把真实伤害、意图、下一轮重洗抽牌与最终阶段变化冻结为同一连续记录链。</summary>
    [Test]
    public void EnemyAction_SingleEnemy_ReshufflesDiscardedOpeningHandBeforeOrderedRoundDraw()
    {
        using (var scenario = new M8DQueueScenario(
                   enemyCount: 1,
                   deckTemplateIds: new[]
                   {
                       M8DQueueScenario.LethalCardTemplateId,
                       M8DQueueScenario.LethalCardTemplateId,
                   },
                   initialHandCount: 2))
        {
            scenario.StartAndCompleteFeedback();
            CardInstanceId[] openingHand = scenario.Zones.Hand.ToArray();
            Assert.That(openingHand, Has.Length.EqualTo(2));
            Assert.That(scenario.Zones.DrawPile, Is.Empty);

            scenario.Queue.SubmitRegistered(new EndPlayerActionCommand(scenario.Player.Id));

            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(openingHand));
            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(3));
            BattleCommandExecutionResult result = scenario.Presentation.Results[2];
            Assert.That(result.CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
            Assert.That(result.Succeeded, Is.True);
            AssertSettlementTypes(
                result,
                BattleSettlementRecordType.DamageApplied,
                BattleSettlementRecordType.EnemyIntentAdvanced,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.CardsReshuffled,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.BattlePhaseChanged);
            AssertContinuousOrders(result.Settlements);

            var damage = result.Settlements[0] as BattleDamageAppliedSettlement;
            var intent = result.Settlements[1] as BattleEnemyIntentAdvancedSettlement;
            var firstRecycled = result.Settlements[2] as BattleCardMovedSettlement;
            var secondRecycled = result.Settlements[3] as BattleCardMovedSettlement;
            var reshuffled = result.Settlements[4] as BattleCardsReshuffledSettlement;
            var firstDrawn = result.Settlements[5] as BattleCardMovedSettlement;
            var secondDrawn = result.Settlements[6] as BattleCardMovedSettlement;
            var phaseChanged = result.Settlements[7] as BattlePhaseChangedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
            Assert.That(damage.TargetId, Is.EqualTo(scenario.Player.Id));
            Assert.That(damage.AttackValue, Is.EqualTo(6));
            Assert.That(damage.HealthBefore, Is.EqualTo(30));
            Assert.That(damage.HealthAfter, Is.EqualTo(24));
            Assert.That(intent, Is.Not.Null);
            Assert.That(intent.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
            Assert.That(intent.CompletedBehaviorId, Is.EqualTo(M8DQueueScenario.FirstAttackBehaviorId));
            Assert.That(intent.NextBehaviorId, Is.EqualTo(M8DQueueScenario.FirstAttackBehaviorId));
            Assert.That(firstRecycled, Is.Not.Null);
            Assert.That(firstRecycled.CardId, Is.EqualTo(openingHand[0]));
            Assert.That(firstRecycled.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(firstRecycled.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(secondRecycled, Is.Not.Null);
            Assert.That(secondRecycled.CardId, Is.EqualTo(openingHand[1]));
            Assert.That(secondRecycled.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(secondRecycled.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(reshuffled, Is.Not.Null);
            Assert.That(reshuffled.NewDrawPileOrder, Is.EquivalentTo(openingHand));
            Assert.That(firstDrawn, Is.Not.Null);
            Assert.That(
                firstDrawn.CardId,
                Is.EqualTo(reshuffled.NewDrawPileOrder[reshuffled.NewDrawPileOrder.Count - 1]));
            Assert.That(firstDrawn.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(firstDrawn.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(secondDrawn, Is.Not.Null);
            Assert.That(secondDrawn.CardId, Is.EqualTo(reshuffled.NewDrawPileOrder[0]));
            Assert.That(secondDrawn.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(secondDrawn.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(phaseChanged, Is.Not.Null);
            Assert.That(phaseChanged.PhaseBefore, Is.EqualTo(BattleTurnPhase.EnemyAction));
            Assert.That(phaseChanged.PhaseAfter, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(phaseChanged.RoundNumberBefore, Is.EqualTo(1));
            Assert.That(phaseChanged.RoundNumberAfter, Is.EqualTo(2));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[]
            {
                firstDrawn.CardId,
                secondDrawn.CardId,
            }));
            Assert.That(scenario.Zones.DrawPile, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(24));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        }
    }

    /// <summary>验证双敌严格按 Encounter 顺序行动，且反馈完成前不会执行下一敌人或下一轮命令。</summary>
    [Test]
    public void EnemyActions_TwoEnemies_RespectEncounterOrderAndFeedbackBarrier()
    {
        using (var scenario = new M8DQueueScenario(enemyCount: 2))
        {
            using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();
            scenario.StartAndCompleteFeedback();
            int secondBehaviorBefore = scenario.GetBehaviorId(scenario.SecondEnemy.Id);

            scenario.Queue.SubmitRegistered(new EndPlayerActionCommand(scenario.Player.Id));
            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(3));
            BattleCommandExecutionResult firstResult = scenario.Presentation.Results[2];
            Assert.That(firstResult.CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
            Assert.That(firstResult.Succeeded, Is.True);
            Assert.That(firstResult.Settlements[0], Is.TypeOf<BattleDamageAppliedSettlement>());
            Assert.That(
                firstResult.Settlements.Any(record =>
                    record.RecordType == BattleSettlementRecordType.BlockCleared ||
                    record.RecordType == BattleSettlementRecordType.StatusReduced),
                Is.False);
            AssertContinuousOrders(firstResult.Settlements);
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(24));
            Assert.That(scenario.SecondEnemy.CurrentBlock, Is.Zero);
            Assert.That(scenario.GetBehaviorId(scenario.SecondEnemy.Id), Is.EqualTo(secondBehaviorBefore));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.EnemyAction));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.CurrentActingEnemyId,
                Is.EqualTo(scenario.SecondEnemy.Id));

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(4));
            BattleCommandExecutionResult secondResult = scenario.Presentation.Results[3];
            Assert.That(secondResult.CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
            Assert.That(secondResult.Succeeded, Is.True);
            Assert.That(secondResult.Settlements[0], Is.TypeOf<BattleBlockGainedSettlement>());
            Assert.That(
                secondResult.Settlements.Any(record =>
                    record.RecordType == BattleSettlementRecordType.BlockCleared ||
                    record.RecordType == BattleSettlementRecordType.StatusReduced),
                Is.False);
            AssertContinuousOrders(secondResult.Settlements);
            Assert.That(scenario.SecondEnemy.CurrentBlock, Is.EqualTo(5));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
            Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));

            BattleCommandSubmissionResult blockedPlayerCommand = scenario.Queue.SubmitRegistered(
                new PlayCardCommand(
                    scenario.Player.Id,
                    new CardInstanceId(9999),
                    scenario.Player.Id));
            Assert.That(blockedPlayerCommand.Accepted, Is.True);
            Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(4));
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(24));
            Assert.That(scenario.SecondEnemy.CurrentBlock, Is.EqualTo(5));

            scenario.Presentation.CompleteNext();

            BattleCommandLifecycleEvent blockedTerminal = lifecycle.RequireTerminal(blockedPlayerCommand);
            Assert.That(blockedTerminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(blockedTerminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.CardNotInHand));
            Assert.That(blockedTerminal.Settlements, Is.Empty);
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(4));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
            Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        }
    }

    /// <summary>验证活敌人在两名玩家均存活时于首次写入前触发 Queue fault，并冻结全部待处理事实。</summary>
    [Test]
    public void EnemyAction_MultipleLivingPlayers_FaultsBeforeFirstWriteAndFreezesQueue()
    {
        using (var scenario = new M8DQueueScenario(
                   enemyCount: 1,
                   includeSecondLivingPlayer: true))
        using (BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle())
        {
            scenario.StartAndCompleteFeedback();
            scenario.Queue.SubmitRegistered(new EndPlayerActionCommand(scenario.Player.Id));
            BattleCommandSubmissionResult secondEnd = scenario.Queue.SubmitRegistered(
                new EndPlayerActionCommand(scenario.SecondPlayer.Id));
            BattleCommandSubmissionResult acceptedTail = scenario.Queue.SubmitRegistered(
                new StartBattleCommand());

            Assert.That(secondEnd.Accepted, Is.True);
            Assert.That(acceptedTail.Accepted, Is.True);
            Assert.That(scenario.Player.IsAlive, Is.True);
            Assert.That(scenario.SecondPlayer.IsAlive, Is.True);
            Assert.That(scenario.FirstEnemy.IsAlive, Is.True);
            Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.EqualTo(2));

            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            EnemyIntentLayoutData intentLayoutBefore = scenario.Intents.Layout.CurrentValue;
            BattleEnemyIntentAuthoritySnapshot intentBefore =
                scenario.Intents.CaptureAuthoritySnapshot(scenario.FirstEnemy.Id);
            uint intentRandomBefore = scenario.Intents.RandomState;
            CardZoneLayoutData firstZonesBefore = scenario.Zones.Layout.CurrentValue;
            CardZoneLayoutData secondZonesBefore = scenario.SecondZones.Layout.CurrentValue;
            uint firstShuffleRandomBefore = scenario.Zones.ShuffleRandomState;
            uint secondShuffleRandomBefore = scenario.SecondZones.ShuffleRandomState;
            var combatantFactsBefore = scenario.Combatants.All.Values
                .OrderBy(combatant => combatant.Id.Value)
                .Select(combatant => (
                    combatant.Id,
                    combatant.CurrentHealth,
                    combatant.CurrentStrength,
                    combatant.CurrentBlock,
                    combatant.CurrentVulnerable))
                .ToArray();
            int presentationCountBeforeFault = scenario.Presentation.Results.Count;

            scenario.Presentation.CompleteNext();

            BattleCommandQueueData queueFacts = scenario.Queue.Queue.CurrentValue;
            Assert.That(queueFacts.IsFaulted, Is.True);
            Assert.That(queueFacts.Fault, Is.Not.Null);
            Assert.That(
                queueFacts.Fault.Reason,
                Is.EqualTo(BattleCommandQueueFaultReason.MultipleLivingPlayers));
            Assert.That(queueFacts.Fault.MayHavePartialWrites, Is.False);
            Assert.That(queueFacts.CurrentCommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
            Assert.That(queueFacts.CurrentAuthoritySequence, Is.EqualTo(queueFacts.Fault.AuthoritySequence));
            Assert.That(queueFacts.PendingCount, Is.EqualTo(1));
            Assert.That(queueFacts.IsWaitingForPresentation, Is.False);
            Assert.That(scenario.Presentation.Results.Count, Is.EqualTo(presentationCountBeforeFault));

            BattleCommandLifecycleEvent faulted = lifecycle.Events.Single(item =>
                item.Stage == BattleCommandLifecycleStage.Faulted);
            Assert.That(faulted.AuthoritySequence, Is.EqualTo(queueFacts.Fault.AuthoritySequence));
            Assert.That(faulted.CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
            Assert.That(faulted.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(faulted.Settlements, Is.Empty);
            Assert.That(faulted.Fault, Is.SameAs(queueFacts.Fault));
            Assert.That(
                lifecycle.Events.Any(item =>
                    item.AuthoritySequence == acceptedTail.AuthoritySequence.Value &&
                    item.Stage != BattleCommandLifecycleStage.Queued),
                Is.False);

            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Intents.Layout.CurrentValue, Is.SameAs(intentLayoutBefore));
            Assert.That(intentBefore.Matches(scenario.Intents), Is.True);
            Assert.That(scenario.Intents.RandomState, Is.EqualTo(intentRandomBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(firstZonesBefore));
            Assert.That(scenario.SecondZones.Layout.CurrentValue, Is.SameAs(secondZonesBefore));
            Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(firstShuffleRandomBefore));
            Assert.That(scenario.SecondZones.ShuffleRandomState, Is.EqualTo(secondShuffleRandomBefore));
            Assert.That(
                scenario.Combatants.All.Values
                    .OrderBy(combatant => combatant.Id.Value)
                    .Select(combatant => (
                        combatant.Id,
                        combatant.CurrentHealth,
                        combatant.CurrentStrength,
                        combatant.CurrentBlock,
                        combatant.CurrentVulnerable)),
                Is.EqualTo(combatantFactsBefore));

            BattleCommandSubmissionResult rejectedAfterFault = scenario.Queue.SubmitRegistered(
                new EndPlayerActionCommand(scenario.Player.Id));
            Assert.That(rejectedAfterFault.Accepted, Is.False);
            Assert.That(rejectedAfterFault.AuthoritySequence, Is.Null);
            Assert.That(
                rejectedAfterFault.FailureReason,
                Is.EqualTo(BattleCommandSubmissionFailureReason.QueueFaulted));
            Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
            Assert.That(scenario.Queue.Queue.CurrentValue.Fault, Is.SameAs(queueFacts.Fault));
        }
    }

    /// <summary>验证相同种子的双敌完整循环可重放，且连续两轮每名敌人都恰好行动一次。</summary>
    [Test]
    public void AutomaticContinuation_SameSeedTwoRounds_ReplaysEffectsAndIntentOncePerEnemy()
    {
        using (var first = new M8DQueueScenario(enemyCount: 2, autoCompletePresentation: true, battleSeed: 2468))
        using (var second = new M8DQueueScenario(enemyCount: 2, autoCompletePresentation: true, battleSeed: 2468))
        {
            first.Queue.SubmitRegistered(new StartBattleCommand());
            second.Queue.SubmitRegistered(new StartBattleCommand());

            for (int round = 0; round < 2; round++)
            {
                first.Queue.SubmitRegistered(new EndPlayerActionCommand(first.Player.Id));
                second.Queue.SubmitRegistered(new EndPlayerActionCommand(second.Player.Id));
            }

            AssertEquivalentResults(first.Presentation.Results, second.Presentation.Results);
            Assert.That(CountRecordType(
                first.Presentation.Results,
                BattleSettlementRecordType.EnemyIntentAdvanced), Is.EqualTo(4));
            Assert.That(CountRecordType(
                second.Presentation.Results,
                BattleSettlementRecordType.EnemyIntentAdvanced), Is.EqualTo(4));
            Assert.That(first.Player.CurrentHealth, Is.EqualTo(18));
            Assert.That(second.Player.CurrentHealth, Is.EqualTo(18));
            Assert.That(first.FirstEnemy.CurrentBlock, Is.Zero);
            Assert.That(first.SecondEnemy.CurrentBlock, Is.EqualTo(5));
            Assert.That(second.SecondEnemy.CurrentBlock, Is.EqualTo(5));
            Assert.That(first.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(3));
            Assert.That(second.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(3));
            Assert.That(first.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(second.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(first.Intents.RandomState, Is.EqualTo(second.Intents.RandomState));
            Assert.That(
                first.GetBehaviorId(first.FirstEnemy.Id),
                Is.EqualTo(second.GetBehaviorId(second.FirstEnemy.Id)));
            Assert.That(
                first.GetBehaviorId(first.SecondEnemy.Id),
                Is.EqualTo(second.GetBehaviorId(second.SecondEnemy.Id)));
        }
    }

    /// <summary>验证排队后死亡的敌人只产生 source-only skip，并在该反馈完成后才继续下一名存活敌人。</summary>
    [Test]
    public void EnemyAction_SourceDiesAfterQueued_SkipsSourceOnlyThenContinues()
    {
        using (var scenario = new M8DQueueScenario(enemyCount: 2))
        {
            scenario.StartAndCompleteFeedback();
            scenario.ApplyVulnerable(scenario.Player.Id, scenario.FirstEnemy.Id, amount: 2);
            scenario.Queue.SubmitRegistered(new EndPlayerActionCommand(scenario.Player.Id));
            BattleEffectStateTestDriver.Kill(
                scenario.Combatants,
                scenario.Player.Id,
                scenario.FirstEnemy.Id);
            int vulnerableAfterDeath = scenario.FirstEnemy.CurrentVulnerable;
            BattleEnemyIntentAuthoritySnapshot deadIntentBefore =
                scenario.Intents.CaptureAuthoritySnapshot(scenario.FirstEnemy.Id);
            int secondBehaviorBefore = scenario.GetBehaviorId(scenario.SecondEnemy.Id);

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(3));
            BattleCommandExecutionResult skippedResult = scenario.Presentation.Results[2];
            Assert.That(skippedResult.Succeeded, Is.True);
            AssertSettlementTypes(
                skippedResult,
                BattleSettlementRecordType.EnemyActionSkipped,
                BattleSettlementRecordType.BattlePhaseChanged);
            AssertContinuousOrders(skippedResult.Settlements);
            var skipped = skippedResult.Settlements[0] as BattleEnemyActionSkippedSettlement;
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.Reason, Is.EqualTo(BattleEnemyActionSkipReason.SourceNotAlive));
            Assert.That(skipped.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
            Assert.That(skipped.TargetId, Is.Null);
            Assert.That(skipped.EffectId, Is.Null);
            Assert.That(scenario.FirstEnemy.CurrentVulnerable, Is.EqualTo(vulnerableAfterDeath));
            Assert.That(deadIntentBefore.Matches(scenario.Intents), Is.True);
            Assert.That(scenario.SecondEnemy.CurrentBlock, Is.Zero);
            Assert.That(scenario.GetBehaviorId(scenario.SecondEnemy.Id), Is.EqualTo(secondBehaviorBefore));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.CurrentActingEnemyId,
                Is.EqualTo(scenario.SecondEnemy.Id));

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(4));
            Assert.That(scenario.Presentation.Results[3].Settlements[0], Is.TypeOf<BattleBlockGainedSettlement>());
            Assert.That(scenario.SecondEnemy.CurrentBlock, Is.EqualTo(5));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.Null);
            Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        }
    }

    /// <summary>验证致死玩家的当前敌人仍提交一次下一意图，随后进入失败终局并中止剩余敌人。</summary>
    [Test]
    public void EnemyAction_PlayerKilled_CommitsCurrentIntentThenEndsWithoutRemainingEnemy()
    {
        using (var scenario = new M8DQueueScenario(
                   enemyCount: 2,
                   playerHealth: 5,
                   firstEnemyAttackValue: 6))
        {
            scenario.StartAndCompleteFeedback();
            BattleEnemyIntentAuthoritySnapshot secondIntentBefore =
                scenario.Intents.CaptureAuthoritySnapshot(scenario.SecondEnemy.Id);
            scenario.Queue.SubmitRegistered(new EndPlayerActionCommand(scenario.Player.Id));

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(3));
            BattleCommandExecutionResult result = scenario.Presentation.Results[2];
            Assert.That(result.Succeeded, Is.True);
            AssertSettlementPrefix(
                result,
                BattleSettlementRecordType.DamageApplied,
                BattleSettlementRecordType.EnemyIntentAdvanced);
            AssertContinuousOrders(result.Settlements);
            var damage = result.Settlements[0] as BattleDamageAppliedSettlement;
            var intent = result.Settlements[1] as BattleEnemyIntentAdvancedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.WasFatal, Is.True);
            Assert.That(damage.HealthBefore, Is.EqualTo(5));
            Assert.That(damage.HealthAfter, Is.Zero);
            Assert.That(intent, Is.Not.Null);
            Assert.That(intent.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
            Assert.That(
                result.Settlements[result.Settlements.Count - 1],
                Is.TypeOf<BattlePhaseChangedSettlement>());
            var phase = result.Settlements[result.Settlements.Count - 1] as BattlePhaseChangedSettlement;
            Assert.That(phase.PhaseAfter, Is.EqualTo(BattleTurnPhase.BattleEnded));
            Assert.That(scenario.Player.IsAlive, Is.False);
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
            Assert.That(new BattleTerminalRules(scenario.Combatants).Evaluate(), Is.EqualTo(BattleTerminalOutcome.Defeat));
            AssertSingleCandidateEnemyIntentFactsUnchanged(secondIntentBefore, scenario.Intents);
            Assert.That(scenario.SecondEnemy.CurrentBlock, Is.Zero);

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(3));
            AssertSingleCandidateEnemyIntentFactsUnchanged(secondIntentBefore, scenario.Intents);
            Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.Zero);
        }
    }

    /// <summary>验证玩家卡牌击杀最后敌人后立刻进入胜利终局，不再要求结束行动或排入敌人 continuation。</summary>
    [Test]
    public void PlayCard_LastEnemyKilled_EndsBattleWithoutEndAction()
    {
        using (var scenario = new M8DQueueScenario(
                   enemyCount: 1,
                   deckTemplateIds: new[] { M8DQueueScenario.LethalCardTemplateId },
                   initialHandCount: 1))
        {
            scenario.StartAndCompleteFeedback();
            CardInstanceId lethalCard = scenario.Zones.Hand[0];

            scenario.Queue.SubmitRegistered(new PlayCardCommand(
                scenario.Player.Id,
                lethalCard,
                scenario.FirstEnemy.Id));

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(2));
            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            AssertSettlementTypes(
                result,
                BattleSettlementRecordType.EnergySpent,
                BattleSettlementRecordType.DamageApplied,
                BattleSettlementRecordType.CardMoved,
                BattleSettlementRecordType.BattlePhaseChanged);
            AssertContinuousOrders(result.Settlements);
            Assert.That(((BattleDamageAppliedSettlement)result.Settlements[1]).WasFatal, Is.True);
            Assert.That(
                ((BattlePhaseChangedSettlement)result.Settlements[3]).PhaseAfter,
                Is.EqualTo(BattleTurnPhase.BattleEnded));
            Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
            Assert.That(new BattleTerminalRules(scenario.Combatants).Evaluate(), Is.EqualTo(BattleTerminalOutcome.Victory));
            Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.Zero);

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(2));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
        }
    }

    /// <summary>验证终局前已接受的尾命令到队首时稳定失败，终局后的新提交则不分配权威序号。</summary>
    [Test]
    public void BattleEnded_FailsAcceptedTailAndRejectsNewSubmissionStably()
    {
        using (var scenario = new M8DQueueScenario(
                   enemyCount: 1,
                   deckTemplateIds: new[] { M8DQueueScenario.LethalCardTemplateId },
                   initialHandCount: 1))
        using (BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle())
        {
            scenario.Queue.SubmitRegistered(new StartBattleCommand());
            CardInstanceId lethalCard = scenario.Zones.Hand[0];
            BattleCommandSubmissionResult lethalSubmission = scenario.Queue.SubmitRegistered(
                new PlayCardCommand(scenario.Player.Id, lethalCard, scenario.FirstEnemy.Id));
            BattleCommandSubmissionResult acceptedTail = scenario.Queue.SubmitRegistered(
                new EndPlayerActionCommand(scenario.Player.Id));

            Assert.That(lethalSubmission.Accepted, Is.True);
            Assert.That(acceptedTail.Accepted, Is.True);
            Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.EqualTo(2));

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(2));
            BattleTurnData terminalTurn = scenario.Queue.Turn.CurrentValue;
            EnemyIntentLayoutData terminalIntent = scenario.Intents.Layout.CurrentValue;
            uint terminalRandom = scenario.Intents.RandomState;
            int terminalPlayerHealth = scenario.Player.CurrentHealth;
            int terminalEnemyHealth = scenario.FirstEnemy.CurrentHealth;
            int terminalEnergy = terminalTurn.Players[scenario.Player.Id].Energy;
            CardInstanceId[] terminalHand = scenario.Zones.Hand.ToArray();
            CardInstanceId[] terminalDiscard = scenario.Zones.DiscardPile.ToArray();

            scenario.Presentation.CompleteNext();

            BattleCommandLifecycleEvent failedTail = lifecycle.RequireTerminal(acceptedTail);
            Assert.That(failedTail.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                failedTail.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.BattleAlreadyEnded));
            Assert.That(failedTail.Settlements, Is.Empty);
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(2));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(terminalTurn));
            Assert.That(scenario.Intents.Layout.CurrentValue, Is.SameAs(terminalIntent));
            Assert.That(scenario.Intents.RandomState, Is.EqualTo(terminalRandom));
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(terminalPlayerHealth));
            Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(terminalEnemyHealth));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(terminalEnergy));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(terminalHand));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(terminalDiscard));

            BattleCommandSubmissionResult rejectedAfterTerminal = scenario.Queue.SubmitRegistered(
                new EndPlayerActionCommand(scenario.Player.Id));

            Assert.That(rejectedAfterTerminal.Accepted, Is.False);
            Assert.That(rejectedAfterTerminal.AuthoritySequence, Is.Null);
            Assert.That(
                rejectedAfterTerminal.FailureReason,
                Is.EqualTo(BattleCommandSubmissionFailureReason.BattleAlreadyEnded));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(terminalTurn));
            Assert.That(scenario.Intents.Layout.CurrentValue, Is.SameAs(terminalIntent));
            Assert.That(scenario.Intents.RandomState, Is.EqualTo(terminalRandom));
        }
    }

    /// <summary>断言一条命令的全部结算类型与预期严格相等。</summary>
    private static void AssertSettlementTypes(
        BattleCommandExecutionResult result,
        params BattleSettlementRecordType[] expectedTypes)
    {
        Assert.That(result.Settlements.Select(record => record.RecordType), Is.EqualTo(expectedTypes));
    }

    /// <summary>断言一条命令的前缀结算类型与领域事务顺序严格相等。</summary>
    private static void AssertSettlementPrefix(
        BattleCommandExecutionResult result,
        params BattleSettlementRecordType[] expectedTypes)
    {
        Assert.That(result.Settlements.Count, Is.GreaterThanOrEqualTo(expectedTypes.Length));
        Assert.That(
            result.Settlements.Take(expectedTypes.Length).Select(record => record.RecordType),
            Is.EqualTo(expectedTypes));
    }

    /// <summary>断言单次命令的结算序号从零开始连续且没有缺口。</summary>
    private static void AssertContinuousOrders(IReadOnlyList<BattleSettlementRecord> settlements)
    {
        for (int index = 0; index < settlements.Count; index++)
            Assert.That(settlements[index].Order, Is.EqualTo(index));
    }

    /// <summary>断言单候选夹具中未行动敌人的行为、历史与未推进随机流保持不变；允许当前行动者发布新的聚合 Layout。</summary>
    private static void AssertSingleCandidateEnemyIntentFactsUnchanged(
        BattleEnemyIntentAuthoritySnapshot before,
        BattleEnemyIntentsData intents)
    {
        BattleEnemyIntentAuthoritySnapshot after = intents.CaptureAuthoritySnapshot(before.EnemyId);
        Assert.That(after.CurrentBehaviorId, Is.EqualTo(before.CurrentBehaviorId));
        Assert.That(after.History.LastCompletedBehaviorId, Is.EqualTo(before.History.LastCompletedBehaviorId));
        Assert.That(after.History.ConsecutiveCompletedCount, Is.EqualTo(before.History.ConsecutiveCompletedCount));
        Assert.That(after.History.CooldownsByBehaviorId, Is.EqualTo(before.History.CooldownsByBehaviorId));
        Assert.That(after.RandomState, Is.EqualTo(before.RandomState));
    }

    /// <summary>统计一组真实 presentation 结果中指定结算类型的出现次数。</summary>
    private static int CountRecordType(
        IEnumerable<BattleCommandExecutionResult> results,
        BattleSettlementRecordType recordType)
    {
        return results.Sum(result => result.Settlements.Count(record => record.RecordType == recordType));
    }

    /// <summary>逐命令比较两场同种子战斗的权威结果和结算签名。</summary>
    private static void AssertEquivalentResults(
        IReadOnlyList<BattleCommandExecutionResult> first,
        IReadOnlyList<BattleCommandExecutionResult> second)
    {
        Assert.That(second, Has.Count.EqualTo(first.Count));
        for (int resultIndex = 0; resultIndex < first.Count; resultIndex++)
        {
            Assert.That(second[resultIndex].AuthoritySequence, Is.EqualTo(first[resultIndex].AuthoritySequence));
            Assert.That(second[resultIndex].CommandType, Is.EqualTo(first[resultIndex].CommandType));
            Assert.That(second[resultIndex].SubmitterId, Is.EqualTo(first[resultIndex].SubmitterId));
            Assert.That(second[resultIndex].FailureReason, Is.EqualTo(first[resultIndex].FailureReason));
            Assert.That(
                second[resultIndex].Settlements.Select(CreateSettlementSignature),
                Is.EqualTo(first[resultIndex].Settlements.Select(CreateSettlementSignature)));
            AssertContinuousOrders(first[resultIndex].Settlements);
            AssertContinuousOrders(second[resultIndex].Settlements);
        }
    }

    /// <summary>把结算的公共关联和关键字面结果压成稳定字符串，供同种子回放逐项比较。</summary>
    private static string CreateSettlementSignature(BattleSettlementRecord settlement)
    {
        string specific = string.Empty;
        if (settlement is BattleDamageAppliedSettlement damage)
        {
            specific = $":{damage.AttackValue}:{damage.BlockBefore}:{damage.BlockAfter}:{damage.HealthBefore}:{damage.HealthAfter}";
        }
        else if (settlement is BattleBlockGainedSettlement gained)
        {
            specific = $":{gained.BlockBefore}:{gained.BlockAfter}";
        }
        else if (settlement is BattleBlockClearedSettlement cleared)
        {
            specific = $":{cleared.BlockBefore}:{cleared.BlockAfter}";
        }
        else if (settlement is BattleStatusReducedSettlement reduced)
        {
            specific = $":{reduced.Status}:{reduced.ValueBefore}:{reduced.ValueAfter}";
        }
        else if (settlement is BattleEnergyRefilledSettlement refilled)
        {
            specific = $":{refilled.EnergyBefore}:{refilled.EnergyAfter}";
        }
        else if (settlement is BattleEnemyIntentAdvancedSettlement advanced)
        {
            specific = $":{advanced.CompletedBehaviorId}:{advanced.NextBehaviorId}";
        }
        else if (settlement is BattlePhaseChangedSettlement phase)
        {
            specific = $":{phase.PhaseBefore}:{phase.PhaseAfter}:{phase.RoundNumberBefore}:{phase.RoundNumberAfter}";
        }

        return $"{settlement.Order}:{settlement.RecordType}:{settlement.EffectId}:{settlement.SourceId}:{settlement.TargetId}{specific}";
    }

    /// <summary>封装 M8D 公开 Queue 用例所需的玩家、稳定 Encounter、卡区、意图、表与可控表现。</summary>
    private sealed class M8DQueueScenario : IDisposable
    {
        internal const int LethalCardTemplateId = 3101;
        internal const int FirstAttackBehaviorId = 7001;
        internal const int SecondDefendBehaviorId = 7002;

        private const int FirstAttackEffectId = 4101;
        private const int SecondDefendEffectId = 4102;
        private const int LethalCardEffectId = 4103;

        internal BattleCombatantsData Combatants { get; }
        internal PlayerCombatantData Player { get; }
        internal PlayerCombatantData SecondPlayer { get; }
        internal EnemyCombatantData FirstEnemy { get; }
        internal EnemyCombatantData SecondEnemy { get; }
        internal BattleCardZonesData Zones { get; }
        internal BattleCardZonesData SecondZones { get; }
        internal BattleEnemyIntentsData Intents { get; }
        internal M8DRecordingPresentation Presentation { get; }
        internal BattleCommandSubmissionCoordinator Coordinator { get; }
        internal BattleCommandQueue Queue { get; }

        private readonly Tables _tables;

        /// <summary>创建固定行为、可选手牌及可选第二存活玩家的最小战斗，不擅自完成任何表现屏障。</summary>
        internal M8DQueueScenario(
            int enemyCount,
            int playerHealth = 30,
            int firstEnemyAttackValue = 6,
            IEnumerable<int> deckTemplateIds = null,
            int initialHandCount = 0,
            bool autoCompletePresentation = false,
            uint battleSeed = 2468,
            bool includeSecondLivingPlayer = false)
        {
            if (enemyCount < 1 || enemyCount > 2)
                throw new ArgumentOutOfRangeException(nameof(enemyCount));

            Combatants = new BattleCombatantsData();
            Player = Combatants.AddPlayer(templateId: 101, maxHealth: playerHealth, strength: 0);
            if (includeSecondLivingPlayer)
                SecondPlayer = Combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
            FirstEnemy = Combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            if (enemyCount == 2)
                SecondEnemy = Combatants.AddEnemy(templateId: 202, maxHealth: 20, strength: 0);

            var enemyIds = new List<CombatantId> { FirstEnemy.Id };
            if (SecondEnemy != null)
                enemyIds.Add(SecondEnemy.Id);

            Zones = new BattleCardZonesData(
                deckTemplateIds ?? Array.Empty<int>(),
                shuffleSeed: 1357);
            if (SecondPlayer != null)
            {
                SecondZones = new BattleCardZonesData(
                    Array.Empty<int>(),
                    shuffleSeed: 9753);
            }
            _tables = CreateTables(enemyCount, firstEnemyAttackValue);
            Intents = new BattleEnemyIntentsData(
                Combatants,
                enemyIds,
                _tables,
                battleSeed);
            Presentation = new M8DRecordingPresentation(autoCompletePresentation);
            Coordinator = new BattleCommandSubmissionCoordinator();
            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [Player.Id] = Zones,
            };
            if (SecondPlayer != null)
                playerZones.Add(SecondPlayer.Id, SecondZones);
            Queue = BattleCommandQueueTestFactory.Create(
                Combatants,
                Presentation,
                playerZones,
                enemyCombatantIdsInEncounterOrder: enemyIds,
                energyPerRound: 3,
                initialHandCount: initialHandCount,
                enemyIntents: Intents,
                tables: _tables,
                battleSeed: battleSeed,
                coordinator: Coordinator);
        }

        /// <summary>提交战斗开始并完成它唯一的可见反馈，使测试稳定进入第一轮玩家行动。</summary>
        internal void StartAndCompleteFeedback()
        {
            BattleCommandSubmissionResult start = Queue.SubmitRegistered(new StartBattleCommand());
            Assert.That(start.Accepted, Is.True);
            Presentation.CompleteNext();
            Assert.That(Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        }

        /// <summary>通过共享 Effect executor 为指定目标建立非零 Block 前置事实。</summary>
        internal void ApplyBlock(CombatantId sourceId, CombatantId targetId, int amount)
        {
            ApplyFixtureEffect(
                sourceId,
                targetId,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                amount);
        }

        /// <summary>通过共享 Effect executor 为指定目标建立非零 Vulnerable 前置事实。</summary>
        internal void ApplyVulnerable(CombatantId sourceId, CombatantId targetId, int amount)
        {
            ApplyFixtureEffect(
                sourceId,
                targetId,
                cfg.battle.EffectType.ApplyVulnerable,
                cfg.battle.Attribute.None,
                amount);
        }

        /// <summary>读取指定敌人的当前权威行为标识，缺失时立即暴露测试夹具错误。</summary>
        internal int GetBehaviorId(CombatantId enemyId)
        {
            if (!Intents.Layout.CurrentValue.TryGetBehaviorId(enemyId, out int behaviorId))
                throw new InvalidOperationException($"测试敌人 {enemyId.Value} 缺少当前意图。");

            return behaviorId;
        }

        /// <summary>释放 Queue、coordinator、意图、卡区和参与者持有的响应式资源。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            Coordinator.Dispose();
            Intents.Dispose();
            SecondZones?.Dispose();
            Zones.Dispose();
            Combatants.Dispose();
        }

        /// <summary>执行只用于建立前置标量的单 Effect，并把失败视为夹具错误。</summary>
        private void ApplyFixtureEffect(
            CombatantId sourceId,
            CombatantId targetId,
            cfg.battle.EffectType effectType,
            cfg.battle.Attribute attribute,
            int amount)
        {
            BattleEffectExecutionResult result = BattleEffectStateTestDriver.Execute(
                Combatants,
                sourceId,
                targetId,
                effectType,
                attribute,
                amount);
            if (!result.Succeeded)
                throw new InvalidOperationException($"M8D 测试前置 Effect 失败：{result.FailureReason}。");
        }

        /// <summary>创建单敌 attack 或双敌 attack/defend 以及一张致死测试卡所需的最小 Luban 表。</summary>
        private static Tables CreateTables(int enemyCount, int firstEnemyAttackValue)
        {
            var enemies = new JArray
            {
                CreateEnemy(templateId: 201, behaviorGroupId: 6001),
            };
            var groups = new JArray
            {
                CreateBehaviorGroup(groupId: 6001, FirstAttackBehaviorId),
            };
            var behaviors = new JArray
            {
                CreateBehavior(
                    FirstAttackBehaviorId,
                    cfg.battle.EnemyIntentType.Attack,
                    cfg.battle.TargetRule.Enemy,
                    FirstAttackEffectId),
            };
            if (enemyCount == 2)
            {
                enemies.Add(CreateEnemy(templateId: 202, behaviorGroupId: 6002));
                groups.Add(CreateBehaviorGroup(groupId: 6002, SecondDefendBehaviorId));
                behaviors.Add(CreateBehavior(
                    SecondDefendBehaviorId,
                    cfg.battle.EnemyIntentType.Defend,
                    cfg.battle.TargetRule.Self,
                    SecondDefendEffectId));
            }

            var data = new Dictionary<string, JArray>
            {
                ["battle_tbhero"] = new JArray(),
                ["battle_tbenemy"] = enemies,
                ["battle_tbdeck"] = new JArray(),
                ["battle_tbcard"] = new JArray(CreateCard()),
                ["battle_tbcardeffect"] = new JArray(
                    CreateEffect(
                        FirstAttackEffectId,
                        cfg.battle.EffectType.DealDamage,
                        firstEnemyAttackValue),
                    CreateEffect(
                        SecondDefendEffectId,
                        cfg.battle.EffectType.GainBlock,
                        value: 5),
                    CreateEffect(
                        LethalCardEffectId,
                        cfg.battle.EffectType.DealDamage,
                        value: 50)),
                ["battle_tbencounter"] = new JArray(),
                ["battle_tbenemybehaviorgroup"] = groups,
                ["battle_tbenemybehavior"] = behaviors,
            };
            return new Tables(tableName => data[tableName]);
        }

        /// <summary>创建一名固定引用指定行为组的最小敌人表行。</summary>
        private static JObject CreateEnemy(int templateId, int behaviorGroupId)
        {
            return new JObject
            {
                ["id"] = templateId,
                ["name_i18n_key"] = $"battle.enemy.test_{templateId}.name",
                ["max_health"] = 20,
                ["base_strength"] = 0,
                ["view_prefab_key"] = string.Empty,
                ["behavior_group_id"] = behaviorGroupId,
            };
        }

        /// <summary>创建只含一个确定性行为的最小敌人行为组。</summary>
        private static JObject CreateBehaviorGroup(int groupId, int behaviorId)
        {
            return new JObject
            {
                ["id"] = groupId,
                ["behavior_ids"] = new JArray(behaviorId),
            };
        }

        /// <summary>创建显式 Self 或 Enemy 目标且只引用一个 Effect 的最小敌人行为。</summary>
        private static JObject CreateBehavior(
            int behaviorId,
            cfg.battle.EnemyIntentType intentType,
            cfg.battle.TargetRule targetRule,
            int effectId)
        {
            return new JObject
            {
                ["id"] = behaviorId,
                ["intent_type"] = (int)intentType,
                ["target_rule"] = (int)targetRule,
                ["effect_id"] = effectId,
                ["weight"] = 1,
                ["cooldown_selections"] = 0,
                ["max_consecutive"] = 0,
            };
        }

        /// <summary>创建一张零费、显式敌方目标并绑定致死 Damage Effect 的测试卡。</summary>
        private static JObject CreateCard()
        {
            return new JObject
            {
                ["id"] = LethalCardTemplateId,
                ["name_i18n_key"] = "battle.card.test_lethal.name",
                ["description_i18n_key"] = "battle.card.test_lethal.description",
                ["cost"] = 0,
                ["target_rule"] = (int)cfg.battle.TargetRule.Enemy,
                ["effect_bindings"] = new JArray(new JObject
                {
                    ["argument_key"] = string.Empty,
                    ["effect_id"] = LethalCardEffectId,
                }),
                ["illustration_key"] = string.Empty,
            };
        }

        /// <summary>创建一个 Attribute.None 的最小 Damage 或 Block Effect 表行。</summary>
        private static JObject CreateEffect(
            int effectId,
            cfg.battle.EffectType effectType,
            int value)
        {
            return new JObject
            {
                ["id"] = effectId,
                ["effect_type"] = (int)effectType,
                ["attribute"] = (int)cfg.battle.Attribute.None,
                ["value"] = value,
            };
        }
    }

    /// <summary>记录真实 Queue 结果，并按用例选择立即完成或显式保留 presentation barrier。</summary>
    private sealed class M8DRecordingPresentation : IBattleCommandPresentation
    {
        private readonly bool _autoComplete;
        private readonly Queue<Action> _completions = new Queue<Action>();

        internal List<BattleCommandExecutionResult> Results { get; } =
            new List<BattleCommandExecutionResult>();

        /// <summary>保存是否在收到结果时同步完成反馈。</summary>
        internal M8DRecordingPresentation(bool autoComplete)
        {
            _autoComplete = autoComplete;
        }

        /// <summary>按收到顺序记录结果；自动模式立即回报，否则保留最早 completion。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            Results.Add(result);
            if (_autoComplete)
            {
                onCompleted.Invoke();
                return;
            }

            _completions.Enqueue(onCompleted);
        }

        /// <summary>完成最早收到且尚未完成的一次可见反馈。</summary>
        internal void CompleteNext()
        {
            if (_completions.Count == 0)
                throw new InvalidOperationException("当前没有待完成的 M8D 表现反馈。");

            _completions.Dequeue().Invoke();
        }
    }
}
