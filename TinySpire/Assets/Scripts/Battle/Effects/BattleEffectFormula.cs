using System;

namespace TinySpire.Battle
{
    /// <summary>集中计算 M7 Effect 数值且不读取或写入任何权威状态。</summary>
    public static class BattleEffectFormula
    {
        /// <summary>根据不可变上下文计算展示值或目标推演结果。</summary>
        public static BattleEffectFormulaResult Calculate(BattleEffectFormulaContext context)
        {
            switch (context.OperationType)
            {
                case BattleEffectOperationType.DealDamage:
                    int attackValue = Math.Max(
                        0,
                        checked(context.ConfiguredValue + context.SourceStrength));
                    if (!context.Target.HasValue)
                    {
                        return new BattleEffectFormulaResult(attackValue, null);
                    }

                    BattleEffectTargetSnapshot target = context.Target.Value;
                    if (target.Vulnerable > 0)
                    {
                        attackValue = checked(attackValue * 3) / 2;
                    }

                    int blockAbsorbed = Math.Min(target.Block, attackValue);
                    int blockAfter = target.Block - blockAbsorbed;
                    int healthLoss = Math.Min(
                        target.Health,
                        attackValue - blockAbsorbed);
                    int healthAfter = target.Health - healthLoss;
                    var damageOutcome = new BattleDamageFormulaOutcome(
                        attackValue,
                        target.Block,
                        blockAfter,
                        blockAbsorbed,
                        target.Health,
                        healthAfter,
                        healthLoss,
                        target.Health > 0 && healthAfter == 0);
                    return new BattleEffectFormulaResult(attackValue, damageOutcome);
                case BattleEffectOperationType.ModifyAttribute:
                    return new BattleEffectFormulaResult(context.ConfiguredValue, null);
                case BattleEffectOperationType.GainBlock:
                case BattleEffectOperationType.ApplyVulnerable:
                    return new BattleEffectFormulaResult(
                        Math.Max(0, context.ConfiguredValue),
                        null);
                case BattleEffectOperationType.Heal:
                    int requestedAmount = Math.Max(0, context.ConfiguredValue);
                    if (!context.Target.HasValue)
                        return new BattleEffectFormulaResult(requestedAmount, null);
                    if (!context.TargetMaxHealth.HasValue)
                    {
                        throw new InvalidOperationException(
                            "目标治疗公式必须提供冻结的生命上限。");
                    }

                    BattleHealthRestorationOutcome restorationOutcome =
                        BattleHealthRestorationOutcomeResolver.Resolve(
                            context.ConfiguredValue,
                            context.Target.Value.Health,
                            context.TargetMaxHealth.Value);
                    return new BattleEffectFormulaResult(
                        restorationOutcome.RequestedAmount,
                        damageOutcome: null,
                        healthRestorationOutcome: restorationOutcome);
                default:
                    throw new ArgumentOutOfRangeException(nameof(context.OperationType));
            }
        }
    }
}
