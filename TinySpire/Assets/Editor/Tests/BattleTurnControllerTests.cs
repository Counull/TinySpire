using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleTurnControllerTests
{
    /// <summary>验证玩家回合事实只按 CombatantId 映射保存，且不存在单玩家全局字段。</summary>
    [Test]
    public void Turn_WithTwoPlayers_ExposesIndependentCombatantIdFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        var queue = new BattleCommandQueue(new[] { firstPlayer.Id, secondPlayer.Id }, presentation);

        queue.Submit(new StartBattleCommand());

        BattleTurnData turn = queue.Turn.CurrentValue;
        Assert.That(turn.Players.Count, Is.EqualTo(2));
        Assert.That(turn.Players.ContainsKey(firstPlayer.Id), Is.True);
        Assert.That(turn.Players.ContainsKey(secondPlayer.Id), Is.True);
        Assert.That(turn.Players[firstPlayer.Id], Is.Not.SameAs(turn.Players[secondPlayer.Id]));
        Assert.That(turn.Players[firstPlayer.Id].Energy, Is.Zero);
        Assert.That(turn.Players[secondPlayer.Id].Energy, Is.Zero);
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
        var queue = new BattleCommandQueue(new[] { player.Id }, presentation);
        queue.Submit(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        BattleCommandSubmissionResult duplicateSubmission = queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();

        Assert.That(duplicateSubmission.Accepted, Is.True);
        Assert.That(duplicateSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(presentation.Results[1].Succeeded, Is.False);
        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.BattleAlreadyStarted));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));

        queue.Dispose();
        combatants.Dispose();
    }
}
