using NUnit.Framework;
using R3;
using TinySpire.Battle;

public sealed class BattleCombatantsDataTests
{
    [Test]
    public void AddPlayerAndEnemy_AssignsDistinctCombatantIdsAndExposesTheSourceDictionary()
    {
        var combatants = new BattleCombatantsData();

        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);

        Assert.That(player.Id, Is.Not.EqualTo(enemy.Id));
        Assert.That(combatants.All.Count, Is.EqualTo(2));
        Assert.That(combatants.All[player.Id], Is.SameAs(player));
        Assert.That(combatants.All[enemy.Id], Is.SameAs(enemy));
    }

    [Test]
    public void TryGet_ReturnsTheCombatantWithTheRequestedId()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);

        bool found = combatants.TryGet(player.Id, out CombatantData combatant);

        Assert.That(found, Is.True);
        Assert.That(combatant, Is.SameAs(player));
    }

    [Test]
    public void ApplyDamage_ChangesOnlyTheTargetCombatantHealth()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);

        bool damaged = combatants.ApplyDamage(enemy.Id, 20);

        Assert.That(damaged, Is.True);
        Assert.That(enemy.IsAlive, Is.False);
        Assert.That(player.CurrentHealth, Is.EqualTo(30));
        Assert.That(combatants.All.Count, Is.EqualTo(2));
        Assert.That(combatants.All[player.Id], Is.SameAs(player));
        Assert.That(combatants.All[enemy.Id], Is.SameAs(enemy));
    }

    [Test]
    public void Health_PublishesTheNewAuthoritativeValueAfterDamage()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        int observedHealth = 0;

        using (player.Health.Skip(1).Subscribe(value => observedHealth = value))
        {
            Assert.That(combatants.ApplyDamage(player.Id, 5), Is.True);
        }

        Assert.That(player.Health.CurrentValue, Is.EqualTo(25));
        Assert.That(observedHealth, Is.EqualTo(25));
    }
}
