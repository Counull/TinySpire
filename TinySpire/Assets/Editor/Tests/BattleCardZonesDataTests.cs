using System.Collections.Generic;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Core;

public sealed class BattleCardZonesDataTests
{
    [Test]
    public void CreatingDeck_CreatesDistinctInstancesInTheShuffledDrawPile()
    {
        var zones = new BattleCardZonesData(
            new[] { 3002, 3002, 3003, 3004 },
            shuffleSeed: 1234);

        Assert.That(zones.Cards.Count, Is.EqualTo(4));
        Assert.That(zones.DrawPile.Count, Is.EqualTo(4));
        Assert.That(zones.Hand, Is.Empty);
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.ExhaustPile, Is.Empty);

        var distinctIds = new HashSet<CardInstanceId>(zones.DrawPile);
        Assert.That(distinctIds.Count, Is.EqualTo(4));
    }

    [Test]
    public void Drawing_WithTheSameSeed_ProducesTheSameTemplateOrder()
    {
        int[] templates = { 3001, 3002, 3003, 3004, 3005, 3006 };
        var firstZones = new BattleCardZonesData(templates, shuffleSeed: 2468);
        var secondZones = new BattleCardZonesData(templates, shuffleSeed: 2468);

        firstZones.Draw(templates.Length);
        secondZones.Draw(templates.Length);

        Assert.That(GetTemplateOrder(secondZones), Is.EqualTo(GetTemplateOrder(firstZones)));
    }

    [Test]
    public void Draw_WhenDrawPileIsEmpty_ReshufflesDiscardPileWithoutLosingCards()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 1357);

        BattleCardZoneOperationResult initialDraw = zones.Draw(4);
        BattleCardZoneOperationResult discard = zones.DiscardHand();
        Assert.That(initialDraw.MovedCardCount, Is.EqualTo(4));
        Assert.That(discard.MovedCardCount, Is.EqualTo(4));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile.Count, Is.EqualTo(4));
        CardInstanceId[] discardOrder = new List<CardInstanceId>(zones.DiscardPile).ToArray();

        BattleCardZoneOperationResult redrawn = zones.Draw(4);

        Assert.That(zones.Hand.Count, Is.EqualTo(4));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.ExhaustPile, Is.Empty);
        Assert.That(redrawn.Succeeded, Is.True);
        Assert.That(redrawn.Settlements.Count, Is.EqualTo(9));
        for (int index = 0; index < discardOrder.Length; index++)
        {
            var moved = redrawn.Settlements[index] as BattleCardMovedSettlement;
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(index));
            Assert.That(moved.CardId, Is.EqualTo(discardOrder[index]));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
        }

        var reshuffled = redrawn.Settlements[4] as BattleCardsReshuffledSettlement;
        Assert.That(reshuffled, Is.Not.Null);
        Assert.That(reshuffled.Order, Is.EqualTo(4));
        Assert.That(reshuffled.NewDrawPileOrder, Is.EquivalentTo(discardOrder));
        for (int index = 0; index < reshuffled.NewDrawPileOrder.Count; index++)
        {
            var moved = redrawn.Settlements[index + 5] as BattleCardMovedSettlement;
            CardInstanceId expectedDrawnId =
                reshuffled.NewDrawPileOrder[reshuffled.NewDrawPileOrder.Count - 1 - index];
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(index + 5));
            Assert.That(moved.CardId, Is.EqualTo(expectedDrawnId));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.Hand));
        }

        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证 Draw 先抽完残余抽牌堆，再按弃牌顺序重洗，并从指定命令序号连续记录。</summary>
    [Test]
    public void Draw_WithResidualDrawPile_PreservesMixedOperationOrderAndOffset()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 8642);
        zones.Draw(3);
        zones.DiscardHand();
        CardInstanceId residualDrawCard = zones.DrawPile[0];
        CardInstanceId[] discardOrder = new List<CardInstanceId>(zones.DiscardPile).ToArray();

        int layoutPublicationCount = 0;
        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = zones.Draw(3, startingOrder: 7);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(result.MovedCardCount, Is.EqualTo(6));
        Assert.That(result.Settlements.Count, Is.EqualTo(7));
        var residualMove = result.Settlements[0] as BattleCardMovedSettlement;
        Assert.That(residualMove, Is.Not.Null);
        Assert.That(residualMove.Order, Is.EqualTo(7));
        Assert.That(residualMove.CardId, Is.EqualTo(residualDrawCard));
        Assert.That(residualMove.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
        Assert.That(residualMove.ToZone, Is.EqualTo(BattleCardZone.Hand));
        for (int index = 0; index < discardOrder.Length; index++)
        {
            var reshuffleMove = result.Settlements[index + 1] as BattleCardMovedSettlement;
            Assert.That(reshuffleMove, Is.Not.Null);
            Assert.That(reshuffleMove.Order, Is.EqualTo(index + 8));
            Assert.That(reshuffleMove.CardId, Is.EqualTo(discardOrder[index]));
            Assert.That(reshuffleMove.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(reshuffleMove.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
        }

        var reshuffled = result.Settlements[4] as BattleCardsReshuffledSettlement;
        Assert.That(reshuffled, Is.Not.Null);
        Assert.That(reshuffled.Order, Is.EqualTo(11));
        Assert.That(result.Settlements[5].Order, Is.EqualTo(12));
        Assert.That(result.Settlements[6].Order, Is.EqualTo(13));
        Assert.That(zones.Hand.Count, Is.EqualTo(3));
        Assert.That(zones.DrawPile.Count, Is.EqualTo(1));
        Assert.That(zones.DiscardPile, Is.Empty);
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证同一种子重洗会冻结完全相同的新抽牌堆顺序，而非仅保证卡牌集合相同。</summary>
    [Test]
    public void Draw_WithSameSeed_RecordsExactSameReshuffledOrder()
    {
        int[] templates = { 3001, 3002, 3003, 3004 };
        var firstZones = new BattleCardZonesData(templates, shuffleSeed: 7531);
        var secondZones = new BattleCardZonesData(templates, shuffleSeed: 7531);
        firstZones.Draw(templates.Length);
        secondZones.Draw(templates.Length);
        firstZones.DiscardHand();
        secondZones.DiscardHand();

        BattleCardZoneOperationResult firstResult = firstZones.Draw(2);
        BattleCardZoneOperationResult secondResult = secondZones.Draw(2);

        var firstReshuffle = firstResult.Settlements[4] as BattleCardsReshuffledSettlement;
        var secondReshuffle = secondResult.Settlements[4] as BattleCardsReshuffledSettlement;
        Assert.That(firstReshuffle, Is.Not.Null);
        Assert.That(secondReshuffle, Is.Not.Null);
        Assert.That(
            secondReshuffle.NewDrawPileOrder,
            Is.EqualTo(firstReshuffle.NewDrawPileOrder));
        Assert.That(secondZones.Hand, Is.EqualTo(firstZones.Hand));
        Assert.That(secondZones.DrawPile, Is.EqualTo(firstZones.DrawPile));
        Assert.That(secondZones.ShuffleRandomState, Is.EqualTo(firstZones.ShuffleRandomState));
    }

    [Test]
    public void DiscardAndExhaust_MoveOnlyTheRequestedHandInstances()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003 },
            shuffleSeed: 9753);
        zones.Draw(3);
        CardInstanceId discardedId = zones.Hand[0];
        CardInstanceId exhaustedId = zones.Hand[1];

        BattleCardZoneOperationResult discarded = zones.DiscardFromHand(discardedId);
        BattleCardZoneOperationResult exhausted = zones.ExhaustFromHand(exhaustedId);

        Assert.That(discarded.Succeeded, Is.True);
        Assert.That(discarded.Settlements.Count, Is.EqualTo(1));
        Assert.That(discarded.Settlements[0], Is.TypeOf<BattleCardMovedSettlement>());
        Assert.That(exhausted.Succeeded, Is.True);
        Assert.That(exhausted.Settlements.Count, Is.EqualTo(1));
        Assert.That(exhausted.Settlements[0], Is.TypeOf<BattleCardMovedSettlement>());
        Assert.That(zones.Hand.Count, Is.EqualTo(1));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { discardedId }));
        Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { exhaustedId }));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证指定牌不在手牌中时返回空失败结果，且不发布布局或推进随机流。</summary>
    [Test]
    public void DiscardFromHand_WhenCardIsMissing_ReturnsEmptyWithoutWrites()
    {
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 2468);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        zones.DiscardFromHand(cardId);
        CardZoneLayoutData layoutBefore = zones.Layout.CurrentValue;
        uint randomBefore = zones.ShuffleRandomState;
        int layoutPublicationCount = 0;

        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = zones.DiscardFromHand(cardId, startingOrder: 7);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.MovedCardCount, Is.Zero);
        Assert.That(result.Settlements, Is.Empty);
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(randomBefore));
        Assert.That(layoutPublicationCount, Is.Zero);
    }

    [Test]
    public void Layout_PublishesTheNewHandAfterMovingACard()
    {
        var zones = new BattleCardZonesData(new[] { 3001, 3002 }, shuffleSeed: 9753);
        IReadOnlyList<CardInstanceId> observedHand = null;

        using (zones.Layout.Skip(1).Subscribe(layout => observedHand = layout.Hand))
        {
            Assert.That(zones.Draw(1).MovedCardCount, Is.EqualTo(1));
            CardInstanceId cardId = zones.Hand[0];
            Assert.That(zones.DiscardFromHand(cardId).Succeeded, Is.True);
        }

        Assert.That(zones.Hand, Is.Empty);
        Assert.That(observedHand, Is.Empty);
    }

    private static IReadOnlyList<int> GetTemplateOrder(BattleCardZonesData zones)
    {
        var templateIds = new List<int>(zones.Hand.Count);
        foreach (CardInstanceId cardId in zones.Hand)
            templateIds.Add(zones.Cards[cardId].TemplateId);

        return templateIds;
    }

    private static void AssertEveryCardHasExactlyOneZone(BattleCardZonesData zones)
    {
        var occurrences = new Dictionary<CardInstanceId, int>();
        foreach (CardInstanceId cardId in zones.Cards.Keys)
            occurrences.Add(cardId, 0);

        CountZone(zones.DrawPile, occurrences);
        CountZone(zones.Hand, occurrences);
        CountZone(zones.DiscardPile, occurrences);
        CountZone(zones.ExhaustPile, occurrences);

        foreach (int count in occurrences.Values)
            Assert.That(count, Is.EqualTo(1));
    }

    private static void CountZone(
        IReadOnlyList<CardInstanceId> zone,
        IDictionary<CardInstanceId, int> occurrences)
    {
        foreach (CardInstanceId cardId in zone)
            occurrences[cardId]++;
    }
}
