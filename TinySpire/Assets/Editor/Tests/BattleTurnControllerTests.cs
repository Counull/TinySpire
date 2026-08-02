using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleTurnControllerTests
{
    /// <summary>验证手写运行时规则在 JSON 缺省时仍提供每轮三点基础能量。</summary>
    [Test]
    public void GameConfig_DefaultEnergyPerRound_IsThree()
    {
        Assert.That(new GameConfig().EnergyPerRound, Is.EqualTo(3));
    }

    /// <summary>验证玩家回合事实只按 CombatantId 映射保存，且不存在单玩家全局字段。</summary>
    [Test]
    public void Turn_WithTwoPlayers_ExposesIndependentCombatantIdFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);

        queue.SubmitRegistered(new StartBattleCommand());

        BattleTurnData turn = queue.Turn.CurrentValue;
        Assert.That(turn.Players.Count, Is.EqualTo(2));
        Assert.That(turn.Players.ContainsKey(firstPlayer.Id), Is.True);
        Assert.That(turn.Players.ContainsKey(secondPlayer.Id), Is.True);
        Assert.That(turn.Players[firstPlayer.Id], Is.Not.SameAs(turn.Players[secondPlayer.Id]));
        Assert.That(turn.Players[firstPlayer.Id].Energy, Is.EqualTo(3));
        Assert.That(turn.Players[secondPlayer.Id].Energy, Is.EqualTo(3));
        Assert.That(typeof(BattleTurnData).GetProperty("CurrentPlayer"), Is.Null);
        Assert.That(typeof(BattleTurnData).GetProperty("CurrentEnergy"), Is.Null);

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证重复开始在真正出队时失败，且不会再次初始化回合事实。</summary>
    [Test]
    public void DuplicateStartBattle_WhenExecuted_FailsWithoutReinitializingTurn()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(combatants, presentation);
        queue.SubmitRegistered(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        using BattleCommandLifecycleExecutionRecorder recorder =
            queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult duplicateSubmission = queue.SubmitRegistered(new StartBattleCommand());
        presentation.CompleteNext();
        BattleCommandLifecycleEvent duplicateResult = recorder.RequireTerminal(duplicateSubmission);

        Assert.That(duplicateSubmission.Accepted, Is.True);
        Assert.That(duplicateSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(duplicateResult.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(
            duplicateResult.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.BattleAlreadyStarted));
        Assert.That(duplicateResult.Settlements, Is.Empty);
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));

        queue.Dispose();
        combatants.Dispose();
    }
}
