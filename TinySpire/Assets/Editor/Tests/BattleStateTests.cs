using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleStateTests
{
    [Test]
    public void AddPlayerAndEnemy_AssignsDistinctCombatantIdsAndExposesTheSourceDictionary()
    {
        var battle = new BattleState();

        PlayerCombatantState player = battle.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantState enemy = battle.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);

        Assert.That(player.Id, Is.Not.EqualTo(enemy.Id));
        Assert.That(battle.Combatants.Count, Is.EqualTo(2));
        Assert.That(battle.Combatants[player.Id], Is.SameAs(player));
        Assert.That(battle.Combatants[enemy.Id], Is.SameAs(enemy));
    }

    [Test]
    public void TryGetCombatant_ReturnsTheCombatantWithTheRequestedId()
    {
        var battle = new BattleState();
        PlayerCombatantState player = battle.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        battle.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);

        bool found = battle.TryGetCombatant(player.Id, out CombatantState combatant);

        Assert.That(found, Is.True);
        Assert.That(combatant, Is.SameAs(player));
    }

    [Test]
    public void ApplyDamage_ChangesOnlyTheTargetCombatantHealth()
    {
        var battle = new BattleState();
        PlayerCombatantState player = battle.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantState enemy = battle.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);

        bool damaged = battle.ApplyDamage(enemy.Id, 20);

        Assert.That(damaged, Is.True);
        Assert.That(enemy.IsAlive, Is.False);
        Assert.That(player.CurrentHealth, Is.EqualTo(30));
        Assert.That(battle.Combatants.Count, Is.EqualTo(2));
        Assert.That(battle.Combatants[player.Id], Is.SameAs(player));
        Assert.That(battle.Combatants[enemy.Id], Is.SameAs(enemy));
    }
}
