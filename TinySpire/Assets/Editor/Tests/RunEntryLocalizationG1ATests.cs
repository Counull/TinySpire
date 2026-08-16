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
        "run.entry.menu.continue",
        "run.entry.common.cancel",
        "run.entry.abandon.title",
        "run.entry.abandon.message",
        "run.entry.abandon.confirm",
        "run.entry.save.issue.title",
        "run.entry.save.issue.invalid_json",
        "run.entry.save.issue.invalid_document",
        "run.entry.save.issue.unsupported_schema",
        "run.entry.save.issue.interrupted_commit",
        "run.entry.save.issue.io_failure",
        "run.entry.save.issue.missing_configuration",
        "run.entry.save.delete.title",
        "run.entry.save.delete.message",
        "run.entry.save.delete.confirm",
        "run.entry.save.delete.failed",
        "run.entry.save.commit_failed",
        "run.entry.save.retry",
        "run.entry.save.exit",
        "run.entry.save.rollback.title",
        "run.entry.save.rollback.message",
        "run.entry.save.rollback.confirm",
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
        new object[] { "run.entry.map.cleared", "Node cleared; later content is not connected yet", "节点已清除、后续内容未接入", false },
        new object[] { "run.entry.map.health", "HP {current}/{max}", "生命 {current}/{max}", true },
        new object[] { "run.entry.failure.title", "Battle Failed", "战斗失败", false },
        new object[] { "run.entry.failure.restart", "Restart Battle", "重开本关", false },
        new object[] { "run.entry.menu.continue", "Continue", "继续游戏", false },
        new object[] { "run.entry.common.cancel", "Cancel", "取消", false },
        new object[] { "run.entry.abandon.title", "Abandon current Run?", "放弃当前 Run？", false },
        new object[] { "run.entry.abandon.message", "The current save will be deleted before starting a new Run.", "开始新 Run 前会删除当前存档。", false },
        new object[] { "run.entry.abandon.confirm", "Abandon Run", "放弃 Run", false },
        new object[] { "run.entry.save.issue.title", "Save Unavailable", "存档不可用", false },
        new object[] { "run.entry.save.issue.invalid_json", "The save file contains invalid JSON. Continue is unavailable.", "存档不是有效的 JSON，无法继续游戏。", false },
        new object[] { "run.entry.save.issue.invalid_document", "The save data is incomplete or invalid. Continue is unavailable.", "存档内容不完整或无效，无法继续游戏。", false },
        new object[] { "run.entry.save.issue.unsupported_schema", "This save version is unknown or cannot be migrated. Continue is unavailable.", "存档版本未知或无法迁移，无法继续游戏。", false },
        new object[] { "run.entry.save.issue.interrupted_commit", "An interrupted save was detected. Continue is unavailable.", "检测到未完成的存档写入，无法继续游戏。", false },
        new object[] { "run.entry.save.issue.io_failure", "The save could not be read. Continue is unavailable.", "无法读取存档，无法继续游戏。", false },
        new object[] { "run.entry.save.issue.missing_configuration", "The save references missing {kind} configuration ID {id}. Continue is unavailable.", "存档引用的 {kind} 配置 ID {id} 缺失，无法继续游戏。", true },
        new object[] { "run.entry.save.delete.title", "Delete unusable save?", "删除不可用的存档？", false },
        new object[] { "run.entry.save.delete.message", "This save will be permanently deleted. This cannot be undone.", "该存档将被永久删除，且无法撤销。", false },
        new object[] { "run.entry.save.delete.confirm", "Delete Save", "删除存档", false },
        new object[] { "run.entry.save.delete.failed", "The save could not be deleted. Retry or cancel.", "删除存档失败，请重试或取消。", false },
        new object[] { "run.entry.save.commit_failed", "Save failed. Retry.", "保存失败，请重试。", false },
        new object[] { "run.entry.save.retry", "Retry Save", "重试保存", false },
        new object[] { "run.entry.save.exit", "Exit", "退出", false },
        new object[] { "run.entry.save.rollback.title", "Exit without saving?", "不保存并退出？", false },
        new object[] { "run.entry.save.rollback.message", "If you exit now, Continue will return to the last successfully saved map checkpoint. If no checkpoint was saved, this Run cannot be recovered.", "现在退出后，“继续游戏”会回退到上一份成功保存的地图检查点；若尚无检查点，本 Run 将无法恢复。", false },
        new object[] { "run.entry.save.rollback.confirm", "Exit and Roll Back", "退出并回退", false },
        new object[] { "battle.hero.test_warrior.name", "Warrior", "战士", false },
    };

    /// <summary>构建门禁必须显式冻结 G2-A 入口运行时会读取的全部 40 个键。</summary>
    [Test]
    public void LocalizationBuildGate_RequiresAllRuntimeRunEntryKeys()
    {
        FieldInfo field = typeof(LocalizationBuildTools).GetField(
            "RequiredRunEntryKeys",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(field, Is.Not.Null);
        var actual = (string[])field.GetValue(null);
        Assert.That(actual, Has.Length.EqualTo(40));
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
