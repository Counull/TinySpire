using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Core;

public sealed class CardZoneStateTests
{
    [Test]
    public void CreatingDeck_CreatesDistinctInstancesInTheShuffledDrawPile()
    {
        var zones = new CardZoneState(
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
        var firstZones = new CardZoneState(templates, shuffleSeed: 2468);
        var secondZones = new CardZoneState(templates, shuffleSeed: 2468);

        firstZones.Draw(templates.Length);
        secondZones.Draw(templates.Length);

        Assert.That(GetTemplateOrder(secondZones), Is.EqualTo(GetTemplateOrder(firstZones)));
    }

    [Test]
    public void Draw_WhenDrawPileIsEmpty_ReshufflesDiscardPileWithoutLosingCards()
    {
        var zones = new CardZoneState(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 1357);

        Assert.That(zones.Draw(4), Is.EqualTo(4));
        Assert.That(zones.DiscardHand(), Is.EqualTo(4));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile.Count, Is.EqualTo(4));

        Assert.That(zones.Draw(4), Is.EqualTo(4));

        Assert.That(zones.Hand.Count, Is.EqualTo(4));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.ExhaustPile, Is.Empty);
        AssertEveryCardHasExactlyOneZone(zones);
    }

    [Test]
    public void DiscardAndExhaust_MoveOnlyTheRequestedHandInstances()
    {
        var zones = new CardZoneState(
            new[] { 3001, 3002, 3003 },
            shuffleSeed: 9753);
        zones.Draw(3);
        CardInstanceId discardedId = zones.Hand[0];
        CardInstanceId exhaustedId = zones.Hand[1];

        Assert.That(zones.DiscardFromHand(discardedId), Is.True);
        Assert.That(zones.ExhaustFromHand(exhaustedId), Is.True);

        Assert.That(zones.Hand.Count, Is.EqualTo(1));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { discardedId }));
        Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { exhaustedId }));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    private static IReadOnlyList<int> GetTemplateOrder(CardZoneState zones)
    {
        var templateIds = new List<int>(zones.Hand.Count);
        foreach (CardInstanceId cardId in zones.Hand)
            templateIds.Add(zones.Cards[cardId].TemplateId);

        return templateIds;
    }

    private static void AssertEveryCardHasExactlyOneZone(CardZoneState zones)
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
