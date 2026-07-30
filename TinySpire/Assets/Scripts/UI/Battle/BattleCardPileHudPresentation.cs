namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 无状态地格式化卡牌堆 HUD 的派生展示文本。
    /// </summary>
    public static class BattleCardPileHudPresentation
    {
        /// <summary>
        /// 将已本地化的牌堆名称和当前数量组合为两行 HUD 文本。
        /// </summary>
        public static string Format(string localizedPileName, int count)
        {
            return $"{localizedPileName}\n{count}";
        }
    }
}
