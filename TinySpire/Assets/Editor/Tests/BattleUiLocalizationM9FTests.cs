using NUnit.Framework;
using UnityEditor;
using UnityEngine.Localization.Tables;

public sealed class BattleUiLocalizationM9FTests
{
    private const string EnglishTablePath = "Assets/Localization/Battle Cards_en.asset";
    private const string ChineseTablePath = "Assets/Localization/Battle Cards_zh-CN.asset";

    private static readonly object[] LocalizedEntries =
    {
        new object[] { "battle.ui.battle.start", "Battle Start", "战斗开始" },
        new object[] { "battle.ui.turn.player", "Player Turn", "玩家回合" },
        new object[] { "battle.ui.turn.enemy", "Enemy Turn", "敌人回合" },
        new object[] { "battle.ui.result.victory", "Victory", "胜利" },
        new object[] { "battle.ui.result.defeat", "Defeat", "失败" },
        new object[] { "battle.ui.action.restart", "Restart", "重新开始" },
        new object[] { "battle.ui.action.exit", "Exit", "退出" },
    };

    /// <summary>确认 M9F 七条正式战斗流程文案已同步到两种语言，并保持为普通字符串。</summary>
    [TestCaseSource(nameof(LocalizedEntries))]
    public void BattleFlowEntry_MatchesFormalLocalization(
        string key,
        string expectedEnglish,
        string expectedChinese)
    {
        StringTable englishTable = AssetDatabase.LoadAssetAtPath<StringTable>(EnglishTablePath);
        StringTable chineseTable = AssetDatabase.LoadAssetAtPath<StringTable>(ChineseTablePath);
        Assert.That(englishTable, Is.Not.Null);
        Assert.That(chineseTable, Is.Not.Null);

        StringTableEntry englishEntry = englishTable.GetEntry(key);
        StringTableEntry chineseEntry = chineseTable.GetEntry(key);
        Assert.That(englishEntry, Is.Not.Null, $"English entry is missing: {key}");
        Assert.That(chineseEntry, Is.Not.Null, $"Chinese entry is missing: {key}");
        Assert.That(englishEntry.Value, Is.EqualTo(expectedEnglish));
        Assert.That(chineseEntry.Value, Is.EqualTo(expectedChinese));
        Assert.That(englishEntry.IsSmart, Is.False);
        Assert.That(chineseEntry.IsSmart, Is.False);
    }
}
