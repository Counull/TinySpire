using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class BattleCardMotionTweenFactoryTests
{
    private readonly object _testTweenId = new object();

    /// <summary>精确注销定向测试直接创建但没有交给 runner 的 Tween。</summary>
    [TearDown]
    public void TearDown()
    {
        DOTween.Kill(_testTweenId, complete: false);
    }

    /// <summary>验证出牌轨迹与后续卡区轨迹共用同一 runner，并保持 Prelude 先于原 settlement Order。</summary>
    [Test]
    public void Play_PlayCardPreludeRunsBeforeOrderZeroAndCardMovedAtItsOwnOrder()
    {
        var playerId = new CombatantId(1001);
        var enemyId = new CombatantId(2001);
        var cardId = new CardInstanceId(11);
        var energySpent = new BattleEnergySpentSettlement(
            order: 0,
            playerId,
            energyBefore: 3,
            energyAfter: 2);
        var damage = new BattleDamageAppliedSettlement(
            order: 1,
            new BattleEffectId(4001),
            playerId,
            enemyId,
            attackValue: 6,
            blockBefore: 0,
            blockAfter: 0,
            healthBefore: 20,
            healthAfter: 14);
        var moved = new BattleCardMovedSettlement(
            order: 2,
            cardId,
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 1,
            BattleCommandType.PlayCard,
            playerId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { energySpent, damage, moved });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var playbackOrder = new List<string>();
        var capturedCues = new List<BattleCardMotionCue>();
        var factory = new BattleCardMotionTweenFactory(cue =>
        {
            capturedCues.Add(cue);
            return CreateCallbackTween(() => playbackOrder.Add(
                $"{cue.Kind}:{cue.SettlementOrder?.ToString() ?? "Prelude"}"));
        });
        using var runner = new BattleCommandPresentationRunner(
            prelude => factory.TryCreate(prelude, out BattleCommandPresentationTween tween)
                ? tween
                : CreateCallbackTween(() => playbackOrder.Add($"UnhandledPrelude:{prelude.Kind}")),
            step => factory.TryCreate(step, out BattleCommandPresentationTween tween)
                ? tween
                : CreateCallbackTween(() => playbackOrder.Add(
                    $"{step.Kind}:{step.SettlementOrder}")));
        int completionCount = 0;

        runner.Play(plan, () =>
        {
            completionCount++;
            playbackOrder.Add("Completion");
        });
        runner.CompleteImmediately();

        Assert.That(
            playbackOrder,
            Is.EqualTo(new[]
            {
                "PlayCardToTarget:Prelude",
                "HealthLossNumber:1",
                "HitShake:1",
                "HandToDiscard:2",
                "Completion",
            }));
        Assert.That(capturedCues, Has.Count.EqualTo(2));
        Assert.That(capturedCues[0].Kind, Is.EqualTo(BattleCardMotionCueKind.PlayCardToTarget));
        Assert.That(capturedCues[0].CardId, Is.EqualTo(cardId));
        Assert.That(capturedCues[0].TargetId, Is.EqualTo(enemyId));
        Assert.That(capturedCues[0].SettlementOrder, Is.Null);
        Assert.That(capturedCues[1].Kind, Is.EqualTo(BattleCardMotionCueKind.HandToDiscard));
        Assert.That(capturedCues[1].CardId, Is.EqualTo(cardId));
        Assert.That(capturedCues[1].TargetId, Is.Null);
        Assert.That(capturedCues[1].SettlementOrder, Is.EqualTo(2));
        Assert.That(plan.SettlementEntries[0].Settlement, Is.SameAs(energySpent));
        Assert.That(plan.SettlementEntries[1].Settlement, Is.SameAs(damage));
        Assert.That(plan.SettlementEntries[2].Settlement, Is.SameAs(moved));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认工厂只按冻结路线区分抽牌入手与离手弃牌，不为重洗内部搬运或 Exhaust 制造 cue。</summary>
    [Test]
    public void TryCreate_CardMoved_RoutesOnlyDrawToHandAndHandToDiscard()
    {
        var drawnCardId = new CardInstanceId(21);
        var discardedCardId = new CardInstanceId(22);
        var internalMoveCardId = new CardInstanceId(23);
        var exhaustedCardId = new CardInstanceId(24);
        var drawToHand = new BattleCardMovedSettlement(
            order: 0,
            drawnCardId,
            BattleCardZone.DrawPile,
            BattleCardZone.Hand);
        var handToDiscard = new BattleCardMovedSettlement(
            order: 1,
            discardedCardId,
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var discardToDraw = new BattleCardMovedSettlement(
            order: 2,
            internalMoveCardId,
            BattleCardZone.DiscardPile,
            BattleCardZone.DrawPile);
        var handToExhaust = new BattleCardMovedSettlement(
            order: 3,
            exhaustedCardId,
            BattleCardZone.Hand,
            BattleCardZone.ExhaustPile);
        var captured = new List<BattleCardMotionCue>();
        var factory = new BattleCardMotionTweenFactory(cue =>
        {
            captured.Add(cue);
            return CreateCallbackTween(() => { });
        });

        Assert.That(
            factory.TryCreate(
                new BattleCommandPresentationStep(
                    BattleCommandPresentationStepKind.CardMoved,
                    drawToHand,
                    substepIndex: 0),
                out _),
            Is.True);
        Assert.That(
            factory.TryCreate(
                new BattleCommandPresentationStep(
                    BattleCommandPresentationStepKind.CardMoved,
                    handToDiscard,
                    substepIndex: 0),
                out _),
            Is.True);
        Assert.That(
            factory.TryCreate(
                new BattleCommandPresentationStep(
                    BattleCommandPresentationStepKind.CardMoved,
                    discardToDraw,
                    substepIndex: 0),
                out _),
            Is.False);
        Assert.That(
            factory.TryCreate(
                new BattleCommandPresentationStep(
                    BattleCommandPresentationStepKind.CardMoved,
                    handToExhaust,
                    substepIndex: 0),
                out _),
            Is.False);

        Assert.That(captured, Has.Count.EqualTo(2));
        Assert.That(captured[0].Kind, Is.EqualTo(BattleCardMotionCueKind.DrawToHand));
        Assert.That(captured[0].CardId, Is.EqualTo(drawnCardId));
        Assert.That(captured[0].SettlementOrder, Is.EqualTo(0));
        Assert.That(captured[1].Kind, Is.EqualTo(BattleCardMotionCueKind.HandToDiscard));
        Assert.That(captured[1].CardId, Is.EqualTo(discardedCardId));
        Assert.That(captured[1].SettlementOrder, Is.EqualTo(1));
    }

    /// <summary>确认重洗只形成一条消费冻结新抽牌堆顺序的提示，不展开逐卡 Discard→Draw 动画。</summary>
    [Test]
    public void TryCreate_CardsReshuffled_UsesOneFrozenOrderCue()
    {
        var firstCardId = new CardInstanceId(31);
        var secondCardId = new CardInstanceId(32);
        var reshuffled = new BattleCardsReshuffledSettlement(
            order: 4,
            new[] { secondCardId, firstCardId });
        BattleCardMotionCue captured = null;
        var factory = new BattleCardMotionTweenFactory(cue =>
        {
            captured = cue;
            return CreateCallbackTween(() => { });
        });

        bool created = factory.TryCreate(
            new BattleCommandPresentationStep(
                BattleCommandPresentationStepKind.CardsReshuffled,
                reshuffled,
                substepIndex: 0),
            out BattleCommandPresentationTween tween);

        Assert.That(created, Is.True);
        Assert.That(tween, Is.Not.Null);
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured.Kind, Is.EqualTo(BattleCardMotionCueKind.CardsReshuffled));
        Assert.That(captured.CardId, Is.Null);
        Assert.That(captured.TargetId, Is.Null);
        Assert.That(captured.SettlementOrder, Is.EqualTo(4));
        Assert.That(captured.NewDrawPileOrder, Is.EqualTo(new[] { secondCardId, firstCardId }));
    }

    /// <summary>创建由 runner 统一拥有、只在播放时记录顺序的零时长测试 cue。</summary>
    private BattleCommandPresentationTween CreateCallbackTween(System.Action callback)
    {
        return new BattleCommandPresentationTween(
            DOTween.Sequence().SetId(_testTweenId).AppendCallback(callback.Invoke),
            cleanup: null);
    }
}
