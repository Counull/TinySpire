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

            if (effect.EffectType != cfg.battle.EffectType.DealDamage)
                return effect.Value;

            int sourceStrength = source?.Strength.CurrentValue ?? 0;
            return Math.Max(0, effect.Value + sourceStrength);
        }
    }
}
