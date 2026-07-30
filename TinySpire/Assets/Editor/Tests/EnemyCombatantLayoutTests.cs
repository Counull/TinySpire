using NUnit.Framework;
using TinySpire.Battle;
using UnityEngine;

public sealed class EnemyCombatantLayoutTests
{
    [Test]
    public void CalculateLocalPositions_TwoEnemies_KeepsEncounterOrderFromRightToLeft()
    {
        var positions = EnemyCombatantLayout.CalculateLocalPositions(enemyCount: 2, spacing: 2f);

        Assert.That(positions, Is.EqualTo(new[]
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f)
        }));
    }

    [Test]
    public void CalculateLocalPositions_ThreeEnemies_UsesCenteredEqualSpacing()
    {
        var positions = EnemyCombatantLayout.CalculateLocalPositions(enemyCount: 3, spacing: 1.5f);

        Assert.That(positions, Is.EqualTo(new[]
        {
            new Vector3(1.5f, 0f, 0f),
            Vector3.zero,
            new Vector3(-1.5f, 0f, 0f)
        }));
    }

    [TestCase(0)]
    [TestCase(4)]
    public void CalculateLocalPositions_OutsideM3ACapacity_Throws(int enemyCount)
    {
        Assert.That(
            () => EnemyCombatantLayout.CalculateLocalPositions(enemyCount, spacing: 1f),
            Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }

    [Test]
    public void CalculateLocalPositions_NonPositiveSpacing_Throws()
    {
        Assert.That(
            () => EnemyCombatantLayout.CalculateLocalPositions(enemyCount: 1, spacing: 0f),
            Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }
}
