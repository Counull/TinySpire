using NUnit.Framework;
using TinySpire.Battle;

public sealed class CardPlayTransitionTests
{
    [Test]
    public void DiscardFromHand_MovesOnlyTheRequestedRuntimeInstance()
    {
        var zones = new BattleCardZonesData(new[] { 3002, 3002 }, shuffleSeed: 1234);
        zones.Draw(2);
        CardInstanceId discardedId = zones.Hand[0];
        CardInstanceId remainingId = zones.Hand[1];

        bool discarded = zones.DiscardFromHand(discardedId).Succeeded;

        Assert.That(discarded, Is.True);
        Assert.That(zones.Hand, Is.EqualTo(new[] { remainingId }));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { discardedId }));
    }
}
