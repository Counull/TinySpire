using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class BattleTurnHudPresentationTests
{
    /// <summary>确认首轮玩家行动的能量、轮次和阶段文本直接来自权威事实。</summary>
    [Test]
    public void FirstPlayerAction_FormatsRoundPhaseAndEnergy()
    {
        Assert.That(BattleTurnHudPresentation.FormatEnergy(3, 3), Is.EqualTo("3 / 3"));
        Assert.That(BattleTurnHudPresentation.FormatRound(1), Is.EqualTo("Round 1"));
        Assert.That(
            BattleTurnHudPresentation.FormatPhase(BattleTurnPhase.PlayerAction),
            Is.EqualTo("PlayerAction"));
    }

    /// <summary>确认结束行动只有在玩家行动阶段且没有待定命令时可提交。</summary>
    [Test]
    public void EndActionAvailability_RejectsEndedLockedAndPendingStates()
    {
        Assert.That(
            BattleTurnHudPresentation.CanSubmitEndAction(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                hasPendingEndAction: false),
            Is.True);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitEndAction(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: true,
                hasPendingEndAction: false),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitEndAction(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                hasPendingEndAction: true),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitEndAction(
                BattleTurnPhase.EnemyAction,
                hasEndedAction: false,
                hasPendingEndAction: false),
            Is.False);
    }
}
