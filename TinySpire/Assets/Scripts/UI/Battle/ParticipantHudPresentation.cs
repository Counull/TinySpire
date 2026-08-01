using System;
using System.Globalization;
using cfg;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>由权威当前行为、静态 Effect 和参与者事实即时派生的敌人意图展示。</summary>
    public readonly struct EnemyIntentPresentationData
    {
        /// <summary>决定 HUD 正式图标的意图类型。</summary>
        public cfg.battle.EnemyIntentType IntentType { get; }

        /// <summary>由共享效果数值入口即时计算的展示值。</summary>
        public int Value { get; }

        /// <summary>敌人存活时显示，死亡时隐藏。</summary>
        public bool IsVisible { get; }

        /// <summary>组合一次无状态敌人意图展示投影。</summary>
        internal EnemyIntentPresentationData(
            cfg.battle.EnemyIntentType intentType,
            int value,
            bool isVisible)
        {
            IntentType = intentType;
            Value = value;
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// 无状态地格式化参与者 HUD 的派生展示文本。
    /// </summary>
    public static class ParticipantHudPresentation
    {
        /// <summary>将当前生命与生命上限格式化为 HUD 数值。</summary>
        public static string FormatHealth(int currentHealth, int maxHealth)
        {
            return $"{currentHealth} / {maxHealth}";
        }

        /// <summary>力量为零时不展示，避免为不存在的效果保留 UI 状态。</summary>
        public static bool ShouldShowStrength(int strength)
        {
            return strength != 0;
        }

        /// <summary>将已本地化的力量名称与当前力量事实组合为 HUD 文本。</summary>
        public static string FormatStrength(string localizedStrengthName, int strength)
        {
            return $"{localizedStrengthName} {strength:+#;-#;0}";
        }

        /// <summary>从同一当前意图快照、静态行为与敌人当前事实派生 HUD 投影。</summary>
        public static EnemyIntentPresentationData DeriveEnemyIntent(
            EnemyIntentLayoutData layout,
            Tables tables,
            EnemyCombatantData enemy)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (enemy == null)
                throw new ArgumentNullException(nameof(enemy));
            if (!layout.TryGetBehaviorId(enemy.Id, out int behaviorId))
                throw new InvalidOperationException($"Enemy {enemy.Id} does not have a current intent.");

            cfg.battle.EnemyBehavior behavior = tables.TbEnemyBehavior.GetOrDefault(behaviorId)
                ?? throw new InvalidOperationException($"Enemy behavior {behaviorId} does not exist.");
            cfg.battle.CardEffect effect = tables.TbCardEffect.GetOrDefault(behavior.EffectId)
                ?? throw new InvalidOperationException(
                    $"Enemy behavior {behavior.Id} references missing effect {behavior.EffectId}.");
            return new EnemyIntentPresentationData(
                behavior.IntentType,
                BattleEffectValueCalculator.Calculate(effect, enemy),
                ShouldShowIntent(enemy));
        }

        /// <summary>只有仍存活的敌人才展示意图；玩家与死亡参与者都隐藏。</summary>
        public static bool ShouldShowIntent(CombatantData combatant)
        {
            return combatant is EnemyCombatantData && combatant.IsAlive;
        }

        /// <summary>以不受当前区域文化影响的十进制文本显示意图数值。</summary>
        public static string FormatIntentValue(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
