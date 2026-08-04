using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class ParticipantHudPresentationTests
{
    [Test]
    public void FormatHealth_UsesAuthoritativeCurrentAndMaximumValues()
    {
        Assert.That(ParticipantHudPresentation.FormatHealth(19, 80), Is.EqualTo("19 / 80"));
    }

    [Test]
    public void StrengthPresentation_HidesZeroAndKeepsTheLocalizedLabelForNonZeroValues()
    {
        Assert.That(ParticipantHudPresentation.ShouldShowStrength(0), Is.False);
        Assert.That(ParticipantHudPresentation.ShouldShowStrength(-2), Is.True);
        Assert.That(ParticipantHudPresentation.FormatStrength("力量", 2), Is.EqualTo("力量 +2"));
        Assert.That(ParticipantHudPresentation.FormatStrength("Strength", -2), Is.EqualTo("Strength -2"));
    }

    /// <summary>确认状态 HUD 只投影存活参与者的当前非零事实，死亡后不隐藏生命事实却隐藏全部状态。</summary>
    [Test]
    public void DeriveStatus_UsesCurrentFactsAndHidesAllStatusesAfterDeath()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(
            templateId: 1001,
            maxHealth: 30,
            strength: 2);
        EnemyCombatantData enemy = combatants.AddEnemy(
            templateId: 2001,
            maxHealth: 20,
            strength: 0);
        BattleEffectStateTestDriver.Execute(
            combatants,
            player.Id,
            player.Id,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            configuredValue: 5);
        BattleEffectStateTestDriver.Execute(
            combatants,
            player.Id,
            player.Id,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            configuredValue: 2);

        ParticipantStatusPresentationData living =
            ParticipantHudPresentation.DeriveStatus(player);

        Assert.That(living.Block, Is.EqualTo(5));
        Assert.That(living.Strength, Is.EqualTo(2));
        Assert.That(living.Vulnerable, Is.EqualTo(2));
        Assert.That(living.IsVisible, Is.True);
        Assert.That(living.IsBlockVisible, Is.True);
        Assert.That(living.IsStrengthVisible, Is.True);
        Assert.That(living.IsVulnerableVisible, Is.True);
        Assert.That(ParticipantHudPresentation.FormatStatusValue(living.Block), Is.EqualTo("5"));
        Assert.That(ParticipantHudPresentation.FormatStatusValue(-2), Is.EqualTo("-2"));

        BattleEffectStateTestDriver.ApplyDamage(
            combatants,
            enemy.Id,
            player.Id,
            configuredValue: player.CurrentHealth + player.CurrentBlock);

        ParticipantStatusPresentationData dead =
            ParticipantHudPresentation.DeriveStatus(player);

        Assert.That(player.CurrentHealth, Is.Zero);
        Assert.That(dead.Strength, Is.EqualTo(2));
        Assert.That(dead.Vulnerable, Is.EqualTo(2));
        Assert.That(dead.IsVisible, Is.False);
        Assert.That(dead.IsBlockVisible, Is.False);
        Assert.That(dead.IsStrengthVisible, Is.False);
        Assert.That(dead.IsVulnerableVisible, Is.False);
    }

    /// <summary>确认全零状态行隐藏，且三名敌人的当前状态投影按 CombatantId 事实彼此隔离。</summary>
    [Test]
    public void DeriveStatus_ZeroFactsAndThreeEnemiesStayIndependent()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
        EnemyCombatantData blockEnemy = combatants.AddEnemy(2001, 20, 0);
        EnemyCombatantData vulnerableEnemy = combatants.AddEnemy(2002, 20, 0);
        EnemyCombatantData strengthEnemy = combatants.AddEnemy(2003, 20, -1);
        BattleEffectStateTestDriver.Execute(
            combatants,
            blockEnemy.Id,
            blockEnemy.Id,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            configuredValue: 4);
        BattleEffectStateTestDriver.Execute(
            combatants,
            vulnerableEnemy.Id,
            vulnerableEnemy.Id,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            configuredValue: 2);

        ParticipantStatusPresentationData playerStatus =
            ParticipantHudPresentation.DeriveStatus(player);
        ParticipantStatusPresentationData blockStatus =
            ParticipantHudPresentation.DeriveStatus(blockEnemy);
        ParticipantStatusPresentationData vulnerableStatus =
            ParticipantHudPresentation.DeriveStatus(vulnerableEnemy);
        ParticipantStatusPresentationData strengthStatus =
            ParticipantHudPresentation.DeriveStatus(strengthEnemy);

        Assert.That(playerStatus.IsVisible, Is.False);
        Assert.That(blockStatus.IsBlockVisible, Is.True);
        Assert.That(blockStatus.IsStrengthVisible, Is.False);
        Assert.That(blockStatus.IsVulnerableVisible, Is.False);
        Assert.That(vulnerableStatus.IsBlockVisible, Is.False);
        Assert.That(vulnerableStatus.IsStrengthVisible, Is.False);
        Assert.That(vulnerableStatus.IsVulnerableVisible, Is.True);
        Assert.That(strengthStatus.IsBlockVisible, Is.False);
        Assert.That(strengthStatus.IsStrengthVisible, Is.True);
        Assert.That(strengthStatus.IsVulnerableVisible, Is.False);
    }
}
