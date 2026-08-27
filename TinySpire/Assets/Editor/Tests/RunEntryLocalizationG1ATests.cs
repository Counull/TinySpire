using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const string CardDataPath = "Assets/GameData/battle_tbcard.json";
    private const string HeroDataPath = "Assets/GameData/battle_tbhero.json";
    private const string RelicDataPath = "Assets/GameData/run_tbrelic.json";
    private const string PotionDataPath = "Assets/GameData/run_tbpotion.json";

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
        "run.entry.reward.title",
        "run.entry.reward.skip",
        "run.entry.rest.title",
        "run.entry.rest.heal",
        "run.entry.rest.upgrade",
        "run.entry.chest.title",
        "run.entry.chest.claim",
        "run.entry.chest.skip",
        "run.entry.chest.full",
        "run.entry.shop.title",
        "run.entry.shop.purchase",
        "run.entry.shop.purchased",
        "run.entry.shop.leave",
        "run.entry.event.title",
        "run.entry.event.gain_gold",
        "run.entry.event.paid_heal",
        "run.entry.holdings.gold",
        "run.entry.holdings.relics",
        "run.entry.holdings.potions",
        "run.entry.holdings.empty",
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
        new object[] { "run.entry.map.title", "Act Map", "Act 地图", false },
        new object[] { "run.entry.map.battle_node", "Encounter", "遭遇", false },
        new object[] { "run.entry.map.cleared", "Node cleared; later content is not connected yet", "节点已清除、后续内容未接入", false },
        new object[] { "run.entry.map.health", "HP {current}/{max}", "生命 {current}/{max}", true },
        new object[] { "run.entry.reward.title", "Card Reward", "卡牌奖励", false },
        new object[] { "run.entry.reward.skip", "Skip", "跳过", false },
        new object[] { "run.entry.rest.title", "Rest", "休息点", false },
        new object[] { "run.entry.rest.heal", "Heal {amount} HP", "恢复 {amount} 点生命", true },
        new object[] { "run.entry.rest.upgrade", "Upgrade {card} to +{level}", "升级 {card} 至 +{level}", true },
        new object[] { "run.entry.chest.title", "Chest", "宝箱", false },
        new object[] { "run.entry.chest.claim", "Claim", "领取", false },
        new object[] { "run.entry.chest.skip", "Skip", "跳过", false },
        new object[] { "run.entry.chest.full", "Potion belt is full", "药水带已满", false },
        new object[] { "run.entry.shop.title", "Shop", "商店", false },
        new object[] { "run.entry.shop.purchase", "Buy {item} — {price} Gold", "购买 {item} — {price} 金币", true },
        new object[] { "run.entry.shop.purchased", "{item} — Purchased", "{item} — 已购买", true },
        new object[] { "run.entry.shop.leave", "Leave", "离开", false },
        new object[] { "run.entry.event.title", "Event", "事件", false },
        new object[] { "run.entry.event.gain_gold", "Gain {gold} Gold", "获得 {gold} 金币", true },
        new object[] { "run.entry.event.paid_heal", "Pay {cost} Gold to heal up to {heal} HP", "支付 {cost} 金币，最多恢复 {heal} 点生命", true },
        new object[] { "run.entry.holdings.gold", "Gold: {gold}", "金币：{gold}", true },
        new object[] { "run.entry.holdings.relics", "Relics", "遗物", false },
        new object[] { "run.entry.holdings.potions", "Potions", "药水", false },
        new object[] { "run.entry.holdings.empty", "None", "无", false },
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

    /// <summary>构建门禁必须显式冻结入口运行时会读取的全部 60 个键。</summary>
    [Test]
    public void LocalizationBuildGate_RequiresAllRuntimeRunEntryKeys()
    {
        FieldInfo field = typeof(LocalizationBuildTools).GetField(
            "RequiredRunEntryKeys",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(field, Is.Not.Null);
        var actual = (string[])field.GetValue(null);
        Assert.That(actual, Has.Length.EqualTo(60));
        Assert.That(actual, Is.EquivalentTo(ExpectedRunEntryKeys));
    }

    /// <summary>G5 两张 Run 持有物表必须由生成数值推导唯一 Smart 参数，并由 i18n 作者源声明对应模板。</summary>
    [TestCase(
        RelicDataPath,
        "run_tbrelic",
        "8001",
        "battle_start_strength",
        1,
        "strength",
        "run.relic.strength_charm.name",
        "run.relic.strength_charm.description")]
    [TestCase(
        PotionDataPath,
        "run_tbpotion",
        "9001",
        "heal_amount",
        10,
        "heal",
        "run.potion.healing.name",
        "run.potion.healing.description")]
    public void LocalizationBuildGate_RunItemUsesExactConfiguredSmartArgument(
        string dataPath,
        string tableName,
        string itemId,
        string amountField,
        int expectedAmount,
        string expectedArgument,
        string expectedNameKey,
        string expectedDescriptionKey)
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(dataPath);
        Assert.That(asset, Is.Not.Null, $"Generated Run item table is missing: {dataPath}");
        JObject item = JObject.Parse(asset.text)[itemId] as JObject;
        Assert.That(item, Is.Not.Null, $"Generated Run item {itemId} is missing from {tableName}.");
        Assert.That(item.Value<int>(amountField), Is.EqualTo(expectedAmount));
        Assert.That(item.Value<string>("name_i18n_key"), Is.EqualTo(expectedNameKey));
        Assert.That(item.Value<string>("description_i18n_key"), Is.EqualTo(expectedDescriptionKey));
        Assert.That(
            LocalizationBuildTools.ResolveExpectedRunItemArguments(tableName, itemId, item),
            Is.EquivalentTo(new[] { expectedArgument }));

        IReadOnlyList<I18nExcelEntry> sourceEntries = I18nExcelReader.Read(
            GetI18nWorkbookPath(),
            "i18n",
            new[] { "en", "zh-CN" });
        I18nExcelEntry name = sourceEntries.Single(entry => entry.Key == expectedNameKey);
        I18nExcelEntry description = sourceEntries.Single(
            entry => entry.Key == expectedDescriptionKey);
        Assert.That(name.IsSmart, Is.False);
        Assert.That(description.IsSmart, Is.True);
        Assert.That(description.Translations["en"], Does.Contain($"{{{expectedArgument}}}"));
        Assert.That(description.Translations["zh-CN"], Does.Contain($"{{{expectedArgument}}}"));
    }

    /// <summary>Shoot 的生产程序伤害投影必须让本地化门禁接受无 Effect 绑定的 damage 参数。</summary>
    [Test]
    public void LocalizationBuildGate_ShootProgramOwnsDamageArgumentWithoutEffectBinding()
    {
        TextAsset cardAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(CardDataPath);
        Assert.That(cardAsset, Is.Not.Null);
        JObject card = JObject.Parse(cardAsset.text)["3201"] as JObject;
        Assert.That(card, Is.Not.Null);
        Assert.That(
            card.Value<int>("program_id"),
            Is.EqualTo((int)cfg.battle.MachineGunnerProgramId.Shoot));
        Assert.That(card.Value<JArray>("effect_bindings"), Is.Empty);

        HashSet<string> arguments = LocalizationBuildTools.ResolveExpectedCardArguments(
            "3201",
            card,
            new JObject());

        Assert.That(arguments, Is.EquivalentTo(new[] { "damage" }));
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

    /// <summary>从 Unity 项目目录稳定定位工作区内的 i18n 作者源。</summary>
    private static string GetI18nWorkbookPath()
    {
        string unityProjectDirectory = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new DirectoryNotFoundException("Unable to determine Unity project directory.");
        string workspaceDirectory = Directory.GetParent(unityProjectDirectory)?.FullName
            ?? throw new DirectoryNotFoundException("Unable to determine TinySpire workspace directory.");
        return Path.Combine(workspaceDirectory, "DataTables", "Datas", "i18n.xlsx");
    }
}
