using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class CardValueCalculatorTests
{
    [Test]
    public void Calculate_DealDamage_DerivesFromBaseValueAndCurrentSourceStrength()
    {
        cfg.battle.CardEffect effect = CreateEffect(cfg.battle.EffectType.DealDamage, value: 6);
        var combatants = new BattleCombatantsData();
        PlayerCombatantData weakSource = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: 1);
        PlayerCombatantData strongSource = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: 4);

        Assert.That(CardValueCalculator.Calculate(effect, weakSource), Is.EqualTo(7));
        Assert.That(CardValueCalculator.Calculate(effect, strongSource), Is.EqualTo(10));
    }

    [TestCase(cfg.battle.EffectType.ModifyAttribute, 3)]
    [TestCase(cfg.battle.EffectType.GainBlock, 5)]
    [TestCase(cfg.battle.EffectType.ApplyVulnerable, 2)]
    public void Calculate_NonDamageEffect_UsesConfiguredValue(cfg.battle.EffectType effectType, int value)
    {
        cfg.battle.CardEffect effect = CreateEffect(effectType, value);
        var combatants = new BattleCombatantsData();
        PlayerCombatantData source = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: 99);

        Assert.That(CardValueCalculator.Calculate(effect, source), Is.EqualTo(value));
    }

    [Test]
    public void Calculate_DealDamage_ClampsNegativeDerivedDamageToZero()
    {
        cfg.battle.CardEffect effect = CreateEffect(cfg.battle.EffectType.DealDamage, value: 2);
        var combatants = new BattleCombatantsData();
        PlayerCombatantData source = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: -5);

        Assert.That(CardValueCalculator.Calculate(effect, source), Is.Zero);
    }

    [Test]
    public void Calculate_DealDamage_WithoutSource_UsesConfiguredBaseValue()
    {
        cfg.battle.CardEffect effect = CreateEffect(cfg.battle.EffectType.DealDamage, value: 6);

        Assert.That(CardValueCalculator.Calculate(effect, source: null), Is.EqualTo(6));
    }

    private static cfg.battle.CardEffect CreateEffect(cfg.battle.EffectType effectType, int value)
    {
        return new cfg.battle.CardEffect(JObject.FromObject(new
        {
            id = 1,
            effect_type = (int)effectType,
            attribute = 0,
            value
        }));
    }
}
