using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Core;

public sealed class GameRandomTests
{
    [Test]
    public void Shuffle_WithTheSameSeed_ProducesTheSameOrder()
    {
        var firstValues = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
        var secondValues = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
        var firstRandom = new GameRandom(1234);
        var secondRandom = new GameRandom(1234);

        firstRandom.Shuffle(firstValues);
        secondRandom.Shuffle(secondValues);

        Assert.That(secondValues, Is.EqualTo(firstValues));
    }

    [Test]
    public void RestoringState_RepeatsTheFollowingSequence()
    {
        var random = new GameRandom(5678);
        random.NextInt(1000);
        uint stateBeforeSequence = random.State;

        int first = random.NextInt(1000);
        int second = random.NextInt(1000);

        random.State = stateBeforeSequence;

        Assert.That(random.NextInt(1000), Is.EqualTo(first));
        Assert.That(random.NextInt(1000), Is.EqualTo(second));
    }

    [Test]
    public void AdvancingOneInstance_DoesNotAdvanceAnotherInstance()
    {
        var advancedRandom = new GameRandom(9012);
        var untouchedRandom = new GameRandom(9012);
        var referenceRandom = new GameRandom(9012);

        advancedRandom.NextInt(1000);

        Assert.That(untouchedRandom.NextInt(1000), Is.EqualTo(referenceRandom.NextInt(1000)));
    }
}
