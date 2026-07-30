namespace TinySpire.UI.Battle
{
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
    }
}
