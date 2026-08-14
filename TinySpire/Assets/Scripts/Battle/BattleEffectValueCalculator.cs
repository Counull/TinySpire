using System;

namespace TinySpire.Battle
{
    /// <summary>
    /// 从静态效果数据和当前参与者事实推导展示数值。
    /// 它不执行效果，也不持久化推导结果。
    /// </summary>
    public static class BattleEffectValueCalculator
    {
        /// <summary>
        /// 计算一条效果在当前来源参与者下的展示数值。
        /// 此方法供卡牌文本与敌人意图 HUD 共享，不代表执行任何效果。
        /// </summary>
        public static int Calculate(cfg.battle.CardEffect effect, CombatantData source)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            int sourceStrength = source?.Strength.CurrentValue ?? 0;
            int sourceBlock = source?.Block.CurrentValue ?? 0;
            BattleEffectMagnitudeSource magnitudeSource =
                BattleCardEffectTypeMapping.IsDealDamageFromSourceBlock(effect.EffectType)
                    ? BattleEffectMagnitudeSource.SourceBlock
                    : BattleEffectMagnitudeSource.ConfiguredValue;
            int resolvedValue = BattleEffectMagnitudeResolver.Resolve(
                magnitudeSource,
                effect.Value,
                sourceBlock);
            var context = new BattleEffectFormulaContext(
                ToOperationType(effect.EffectType),
                resolvedValue,
                sourceStrength,
                target: null);
            return BattleEffectFormula.Calculate(context).Value;
        }

        /// <summary>把 Luban Effect 类型在展示适配边界映射为领域公式操作。</summary>
        private static BattleEffectOperationType ToOperationType(
            cfg.battle.EffectType effectType)
        {
            if (BattleCardEffectTypeMapping.IsHeal(effectType))
                return BattleEffectOperationType.Heal;
            if (BattleCardEffectTypeMapping.IsDealDamageFromSourceBlock(effectType))
                return BattleEffectOperationType.DealDamage;

            switch (effectType)
            {
                case cfg.battle.EffectType.ModifyAttribute:
                    return BattleEffectOperationType.ModifyAttribute;
                case cfg.battle.EffectType.DealDamage:
                    return BattleEffectOperationType.DealDamage;
                case cfg.battle.EffectType.GainBlock:
                    return BattleEffectOperationType.GainBlock;
                case cfg.battle.EffectType.ApplyVulnerable:
                    return BattleEffectOperationType.ApplyVulnerable;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effectType));
            }
        }
    }
}
