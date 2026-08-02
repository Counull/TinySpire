using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleEffectFormulaTests
{
    /// <summary>验证无目标伤害投影只应用基础值与来源力量，并保持非负。</summary>
    [Test]
    public void Calculate_DealDamageWithoutTarget_ReturnsStrengthAdjustedProjection()
    {
        var context = new BattleEffectFormulaContext(
            BattleEffectOperationType.DealDamage,
            configuredValue: 6,
            sourceStrength: 3,
            target: null);

        BattleEffectFormulaResult result = BattleEffectFormula.Calculate(context);

        Assert.That(result.Value, Is.EqualTo(9));
        Assert.That(result.HasDamageOutcome, Is.False);
    }

    /// <summary>验证伤害先合并配置值与力量，再把最终负结果钳制到零。</summary>
    [TestCase(-2, 3, 1)]
    [TestCase(2, -5, 0)]
    [TestCase(0, 0, 0)]
    public void Calculate_DealDamage_ClampsAfterAddingStrength(
        int configuredValue,
        int sourceStrength,
        int expectedValue)
    {
        var context = new BattleEffectFormulaContext(
            BattleEffectOperationType.DealDamage,
            configuredValue,
            sourceStrength,
            target: null);

        BattleEffectFormulaResult result = BattleEffectFormula.Calculate(context);

        Assert.That(result.Value, Is.EqualTo(expectedValue));
        Assert.That(result.HasDamageOutcome, Is.False);
    }

    /// <summary>验证易伤把奇数攻击乘以 3/2 并使用整数向下取整。</summary>
    [Test]
    public void Calculate_DealDamageAgainstVulnerableTarget_MultipliesAndFloors()
    {
        var context = new BattleEffectFormulaContext(
            BattleEffectOperationType.DealDamage,
            configuredValue: 6,
            sourceStrength: 1,
            new BattleEffectTargetSnapshot(health: 20, block: 0, vulnerable: 1));

        BattleEffectFormulaResult result = BattleEffectFormula.Calculate(context);

        Assert.That(result.Value, Is.EqualTo(10));
        Assert.That(result.HasDamageOutcome, Is.True);
        Assert.That(result.DamageOutcome.Value.AttackValue, Is.EqualTo(10));
    }

    /// <summary>验证非伤害操作分别保留有符号属性变化并钳制非负累加值。</summary>
    [TestCase(BattleEffectOperationType.ModifyAttribute, -3, -3)]
    [TestCase(BattleEffectOperationType.GainBlock, -3, 0)]
    [TestCase(BattleEffectOperationType.ApplyVulnerable, -3, 0)]
    [TestCase(BattleEffectOperationType.GainBlock, 5, 5)]
    [TestCase(BattleEffectOperationType.ApplyVulnerable, 2, 2)]
    public void Calculate_NonDamageOperation_UsesItsOwnSignedOrNonNegativeRule(
        BattleEffectOperationType operationType,
        int configuredValue,
        int expectedValue)
    {
        var context = new BattleEffectFormulaContext(
            operationType,
            configuredValue,
            sourceStrength: 99,
            target: null);

        BattleEffectFormulaResult result = BattleEffectFormula.Calculate(context);

        Assert.That(result.Value, Is.EqualTo(expectedValue));
        Assert.That(result.HasDamageOutcome, Is.False);
    }

    /// <summary>验证目标格挡足够时吸收全部攻击且生命保持不变。</summary>
    [Test]
    public void Calculate_DealDamageWithEnoughBlock_ProducesFullAbsorptionOutcome()
    {
        BattleEffectFormulaResult result = BattleEffectFormula.Calculate(
            new BattleEffectFormulaContext(
                BattleEffectOperationType.DealDamage,
                configuredValue: 6,
                sourceStrength: 1,
                new BattleEffectTargetSnapshot(health: 20, block: 10, vulnerable: 0)));

        BattleDamageFormulaOutcome outcome = result.DamageOutcome.Value;
        Assert.That(outcome.AttackValue, Is.EqualTo(7));
        Assert.That(outcome.BlockBefore, Is.EqualTo(10));
        Assert.That(outcome.BlockAfter, Is.EqualTo(3));
        Assert.That(outcome.BlockAbsorbed, Is.EqualTo(7));
        Assert.That(outcome.HealthBefore, Is.EqualTo(20));
        Assert.That(outcome.HealthAfter, Is.EqualTo(20));
        Assert.That(outcome.HealthLoss, Is.Zero);
        Assert.That(outcome.WasFatal, Is.False);
    }

    /// <summary>验证格挡不足时仅剩余攻击扣血并报告真实生命损失。</summary>
    [Test]
    public void Calculate_DealDamageWithPartialBlock_ProducesRemainderHealthLoss()
    {
        BattleEffectFormulaResult result = BattleEffectFormula.Calculate(
            new BattleEffectFormulaContext(
                BattleEffectOperationType.DealDamage,
                configuredValue: 6,
                sourceStrength: 1,
                new BattleEffectTargetSnapshot(health: 20, block: 2, vulnerable: 0)));

        BattleDamageFormulaOutcome outcome = result.DamageOutcome.Value;
        Assert.That(outcome.BlockAbsorbed, Is.EqualTo(2));
        Assert.That(outcome.BlockAfter, Is.Zero);
        Assert.That(outcome.HealthLoss, Is.EqualTo(5));
        Assert.That(outcome.HealthAfter, Is.EqualTo(15));
        Assert.That(outcome.WasFatal, Is.False);
    }

    /// <summary>验证过量伤害把生命钳制到零并只对原本存活目标报告致死。</summary>
    [Test]
    public void Calculate_DealDamageBeyondHealth_ClampsAndMarksFatal()
    {
        BattleEffectFormulaResult result = BattleEffectFormula.Calculate(
            new BattleEffectFormulaContext(
                BattleEffectOperationType.DealDamage,
                configuredValue: 8,
                sourceStrength: 0,
                new BattleEffectTargetSnapshot(health: 5, block: 0, vulnerable: 0)));

        BattleDamageFormulaOutcome outcome = result.DamageOutcome.Value;
        Assert.That(outcome.HealthBefore, Is.EqualTo(5));
        Assert.That(outcome.HealthAfter, Is.Zero);
        Assert.That(outcome.HealthLoss, Is.EqualTo(5));
        Assert.That(outcome.WasFatal, Is.True);
    }
}
