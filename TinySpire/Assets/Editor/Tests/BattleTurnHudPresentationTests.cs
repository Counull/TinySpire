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

    /// <summary>确认参与者表现映射尚未就绪时仅关闭系统指针的结束行动入口。</summary>
    [Test]
    public void EndActionAvailability_WhenParticipantPresentationIsNotReady_RejectsSystemPointer()
    {
        Assert.That(
            BattleTurnHudPresentation.CanSubmitEndAction(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                hasPendingEndAction: false,
                queueFaulted: false,
                participantPresentationReady: false),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitEndAction(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                hasPendingEndAction: false,
                queueFaulted: false,
                participantPresentationReady: true),
            Is.True);
    }

    /// <summary>药水投影只显示最多三个稳定槽位，并以冻结治疗量生成可读按钮文本。</summary>
    [Test]
    public void PotionProjection_FormatsHealingAndCapsVisibleSlotsAtThree()
    {
        Assert.That(BattleTurnHudPresentation.FormatPotion(13), Is.EqualTo("Potion +13 HP"));
        Assert.That(BattleTurnHudPresentation.GetVisiblePotionSlotCount(0), Is.Zero);
        Assert.That(BattleTurnHudPresentation.GetVisiblePotionSlotCount(2), Is.EqualTo(2));
        Assert.That(BattleTurnHudPresentation.GetVisiblePotionSlotCount(4), Is.EqualTo(3));
    }

    /// <summary>药水入口仅在玩家行动、玩家存活受伤、未消费且没有同类待定命令时开放。</summary>
    [Test]
    public void PotionAvailability_RejectsInvalidPhaseHealthConsumedPendingAndReadiness()
    {
        Assert.That(
            BattleTurnHudPresentation.CanSubmitPotion(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                currentHealth: 10,
                maxHealth: 30,
                isConsumed: false,
                hasPendingPotion: false),
            Is.True);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitPotion(
                BattleTurnPhase.EnemyAction,
                hasEndedAction: false,
                currentHealth: 10,
                maxHealth: 30,
                isConsumed: false,
                hasPendingPotion: false),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitPotion(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                currentHealth: 0,
                maxHealth: 30,
                isConsumed: false,
                hasPendingPotion: false),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitPotion(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                currentHealth: 30,
                maxHealth: 30,
                isConsumed: false,
                hasPendingPotion: false),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitPotion(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: true,
                currentHealth: 10,
                maxHealth: 30,
                isConsumed: false,
                hasPendingPotion: false),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitPotion(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                currentHealth: 10,
                maxHealth: 30,
                isConsumed: true,
                hasPendingPotion: false),
            Is.False);
        Assert.That(
            BattleTurnHudPresentation.CanSubmitPotion(
                BattleTurnPhase.PlayerAction,
                hasEndedAction: false,
                currentHealth: 10,
                maxHealth: 30,
                isConsumed: false,
                hasPendingPotion: true,
                queueFaulted: false,
                participantPresentationReady: false),
            Is.False);
    }
}
