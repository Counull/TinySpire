using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleEffectValueCalculatorTests
{
    /// <summary>验证伤害展示值由 Effect 基础值与当前来源力量共同派生。</summary>
    [Test]
    public void Calculate_DealDamage_DerivesFromBaseValueAndCurrentSourceStrength()
    {
        cfg.battle.CardEffect effect = CreateEffect(cfg.battle.EffectType.DealDamage, value: 6);
        var combatants = new BattleCombatantsData();
        PlayerCombatantData weakSource = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: 1);
        PlayerCombatantData strongSource = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: 4);

        Assert.That(BattleEffectValueCalculator.Calculate(effect, weakSource), Is.EqualTo(7));
        Assert.That(BattleEffectValueCalculator.Calculate(effect, strongSource), Is.EqualTo(10));
        combatants.Dispose();
    }

    /// <summary>验证非伤害效果不叠加来源力量，只使用配置数值。</summary>
    [TestCase(cfg.battle.EffectType.ModifyAttribute, 3)]
    [TestCase(cfg.battle.EffectType.GainBlock, 5)]
    [TestCase(cfg.battle.EffectType.ApplyVulnerable, 2)]
    public void Calculate_NonDamageEffect_UsesConfiguredValue(cfg.battle.EffectType effectType, int value)
    {
        cfg.battle.CardEffect effect = CreateEffect(effectType, value);
        var combatants = new BattleCombatantsData();
        PlayerCombatantData source = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: 99);

        Assert.That(BattleEffectValueCalculator.Calculate(effect, source), Is.EqualTo(value));
        combatants.Dispose();
    }

    /// <summary>验证负力量不会让派生伤害低于零。</summary>
    [Test]
    public void Calculate_DealDamage_ClampsNegativeDerivedDamageToZero()
    {
        cfg.battle.CardEffect effect = CreateEffect(cfg.battle.EffectType.DealDamage, value: 2);
        var combatants = new BattleCombatantsData();
        PlayerCombatantData source = combatants.AddPlayer(templateId: 1001, maxHealth: 80, strength: -5);

        Assert.That(BattleEffectValueCalculator.Calculate(effect, source), Is.Zero);
        combatants.Dispose();
    }

    /// <summary>验证没有来源参与者时伤害展示使用 Effect 基础值。</summary>
    [Test]
    public void Calculate_DealDamage_WithoutSource_UsesConfiguredBaseValue()
    {
        cfg.battle.CardEffect effect = CreateEffect(cfg.battle.EffectType.DealDamage, value: 6);

        Assert.That(BattleEffectValueCalculator.Calculate(effect, source: null), Is.EqualTo(6));
    }

    /// <summary>创建指定类型和基础值的最小静态 Effect。</summary>
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
