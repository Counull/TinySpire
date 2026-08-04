using System;
using System.Globalization;
using cfg;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>由单个参与者当前事实即时派生、不会被 View 保存的状态 HUD 投影。</summary>
    internal readonly struct ParticipantStatusPresentationData
    {
        /// <summary>当前权威格挡值。</summary>
        public int Block { get; }

        /// <summary>当前权威力量值。</summary>
        public int Strength { get; }

        /// <summary>当前权威易伤层数。</summary>
        public int Vulnerable { get; }

        /// <summary>至少一个存活状态需要显示时，状态行可见。</summary>
        public bool IsVisible => IsBlockVisible || IsStrengthVisible || IsVulnerableVisible;

        /// <summary>只有存活且格挡为正时显示格挡。</summary>
        public bool IsBlockVisible { get; }

        /// <summary>只有存活且力量非零时显示力量。</summary>
        public bool IsStrengthVisible { get; }

        /// <summary>只有存活且易伤为正时显示易伤。</summary>
        public bool IsVulnerableVisible { get; }

        /// <summary>冻结一次只读状态投影，不持有参与者或响应式属性。</summary>
        internal ParticipantStatusPresentationData(
            int block,
            int strength,
            int vulnerable,
            bool isAlive)
        {
            Block = block;
            Strength = strength;
            Vulnerable = vulnerable;
            IsBlockVisible = isAlive && block > 0;
            IsStrengthVisible = isAlive && strength != 0;
            IsVulnerableVisible = isAlive && vulnerable > 0;
        }
    }

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

        /// <summary>从参与者当前 Health、Block、Strength 与 Vulnerable 即时派生状态行。</summary>
        internal static ParticipantStatusPresentationData DeriveStatus(CombatantData combatant)
        {
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));

            return new ParticipantStatusPresentationData(
                combatant.CurrentBlock,
                combatant.CurrentStrength,
                combatant.CurrentVulnerable,
                combatant.IsAlive);
        }

        /// <summary>以不受当前区域文化影响的十进制文本显示状态数值。</summary>
        internal static string FormatStatusValue(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
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
