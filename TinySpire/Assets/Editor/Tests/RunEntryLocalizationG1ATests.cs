using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

public sealed class RunEntryLocalizationG1ATests
{
    private const string EnglishTablePath = "Assets/Localization/Battle Cards_en.asset";
    private const string ChineseTablePath = "Assets/Localization/Battle Cards_zh-CN.asset";
    private const string HeroDataPath = "Assets/GameData/battle_tbhero.json";

    private static readonly string[] ExpectedRunEntryKeys =
    {
        "run.entry.title",
        "run.entry.menu.start",
        "run.entry.menu.settings",
        "run.entry.menu.compendium",
        "run.entry.menu.statistics",
        "run.entry.common.back",
        "run.entry.common.coming_soon",
        "run.entry.settings.title",
        "run.entry.settings.placeholder",
        "run.entry.hero.title",
        "run.entry.hero.confirm",
        "run.entry.hero.future_slot",
        "run.entry.map.title",
        "run.entry.map.battle_node",
        "run.entry.map.cleared",
        "run.entry.map.health",
        "run.entry.failure.title",
        "run.entry.failure.restart",
    };

    private static readonly object[] LocalizedEntries =
    {
        new object[] { "run.entry.title", "TinySpire", "TinySpire", false },
        new object[] { "run.entry.menu.start", "Start Game", "开始游戏", false },
        new object[] { "run.entry.menu.settings", "Settings", "设置", false },
        new object[] { "run.entry.menu.compendium", "Compendium", "图鉴", false },
        new object[] { "run.entry.menu.statistics", "Statistics", "统计", false },
        new object[] { "run.entry.common.back", "Back", "返回", false },
        new object[] { "run.entry.common.coming_soon", "In Development", "开发中", false },
        new object[] { "run.entry.settings.title", "Settings", "设置", false },
        new object[] { "run.entry.settings.placeholder", "Settings layout placeholder", "设置布局占位", false },
        new object[] { "run.entry.hero.title", "Choose a Hero", "选择角色", false },
        new object[] { "run.entry.hero.confirm", "Start Run", "确认并开始", false },
        new object[] { "run.entry.hero.future_slot", "Future Team Slot", "未来队伍槽位", false },
        new object[] { "run.entry.map.title", "Temporary Map", "临时地图", false },
        new object[] { "run.entry.map.battle_node", "First Battle", "首战", false },
        new object[] { "run.entry.map.cleared", "Cleared", "已完成", false },
        new object[] { "run.entry.map.health", "HP {current}/{max}", "生命 {current}/{max}", true },
        new object[] { "run.entry.failure.title", "Battle Failed", "战斗失败", false },
        new object[] { "run.entry.failure.restart", "Restart Battle", "重开本关", false },
        new object[] { "battle.hero.test_warrior.name", "Warrior", "战士", false },
    };

    /// <summary>构建门禁必须显式冻结入口运行时会读取的全部 18 个键。</summary>
    [Test]
    public void LocalizationBuildGate_RequiresAllRuntimeRunEntryKeys()
    {
        FieldInfo field = typeof(LocalizationBuildTools).GetField(
            "RequiredRunEntryKeys",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(field, Is.Not.Null);
        var actual = (string[])field.GetValue(null);
        Assert.That(actual, Has.Length.EqualTo(18));
        Assert.That(actual, Is.EquivalentTo(ExpectedRunEntryKeys));
    }

    /// <summary>生成的中英文 StringTable 必须逐项匹配冻结文案与 Smart 标记。</summary>
    [TestCaseSource(nameof(LocalizedEntries))]
    public void GeneratedEntry_MatchesG1AContract(
        string key,
        string expectedEnglish,
        string expectedChinese,
        bool expectedSmart)
    {
        StringTable english = AssetDatabase.LoadAssetAtPath<StringTable>(EnglishTablePath);
        StringTable chinese = AssetDatabase.LoadAssetAtPath<StringTable>(ChineseTablePath);
        Assert.That(english, Is.Not.Null);
        Assert.That(chinese, Is.Not.Null);

        StringTableEntry englishEntry = english.GetEntry(key);
        StringTableEntry chineseEntry = chinese.GetEntry(key);
        Assert.That(englishEntry, Is.Not.Null, $"English entry is missing: {key}");
        Assert.That(chineseEntry, Is.Not.Null, $"Chinese entry is missing: {key}");
        Assert.That(englishEntry.Value, Is.EqualTo(expectedEnglish));
        Assert.That(chineseEntry.Value, Is.EqualTo(expectedChinese));
        Assert.That(englishEntry.IsSmart, Is.EqualTo(expectedSmart));
        Assert.That(chineseEntry.IsSmart, Is.EqualTo(expectedSmart));
    }

    /// <summary>生成 Hero 1001 必须继续引用正式 Warrior 翻译键，入口不得写死角色名。</summary>
    [Test]
    public void Hero1001_UsesFormalWarriorLocalizationKey()
    {
        TextAsset heroAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(HeroDataPath);
        Assert.That(heroAsset, Is.Not.Null);
        JObject heroes = JObject.Parse(heroAsset.text);
        JObject hero = heroes["1001"] as JObject;

        Assert.That(hero, Is.Not.Null);
        Assert.That(hero.Value<int>("id"), Is.EqualTo(1001));
        Assert.That(
            hero.Value<string>("name_i18n_key"),
            Is.EqualTo("battle.hero.test_warrior.name"));
    }
}
