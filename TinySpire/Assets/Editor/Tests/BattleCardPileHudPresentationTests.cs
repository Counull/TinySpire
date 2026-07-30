using NUnit.Framework;
using TinySpire.UI.Battle;

public sealed class BattleCardPileHudPresentationTests
{
    /// <summary>
    /// 确认已本地化的标签与卡区派生数量以稳定的两行格式显示。
    /// </summary>
    [Test]
    public void Format_CombinesLocalizedPileNameAndCount()
    {
        Assert.That(
            BattleCardPileHudPresentation.Format("Draw Pile", 5),
            Is.EqualTo("Draw Pile\n5"));
        Assert.That(
            BattleCardPileHudPresentation.Format("弃牌堆", 0),
            Is.EqualTo("弃牌堆\n0"));
        Assert.That(
            BattleCardPileHudPresentation.Format("Exhaust Pile", 2),
            Is.EqualTo("Exhaust Pile\n2"));
    }
}
