using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

public sealed class BattleGoldenBaselineM10BTests
{
    private const string GameConfigPath = "Assets/GameData/game-config.json";
    private const string DeckPath = "Assets/GameData/battle_tbdeck.json";
    private const string CardEffectPath = "Assets/GameData/battle_tbcardeffect.json";
    private const string HeroPath = "Assets/GameData/battle_tbhero.json";
    private const string EnemyPath = "Assets/GameData/battle_tbenemy.json";
    private const string EnglishTablePath = "Assets/Localization/Battle Cards_en.asset";
    private const string ChineseTablePath = "Assets/Localization/Battle Cards_zh-CN.asset";

    private static readonly string[] RequiredBattleFlowKeys =
    {
        "battle.ui.battle.start",
        "battle.ui.turn.player",
        "battle.ui.turn.enemy",
        "battle.ui.result.victory",
        "battle.ui.result.defeat",
        "battle.ui.action.restart",
        "battle.ui.action.exit"
    };

    /// <summary>确认本地化构建门禁将当前运行时会读取的战斗流程键纳入必需清单。</summary>
    [Test]
    public void LocalizationBuildGate_RequiresAllRuntimeBattleFlowKeys()
    {
        FieldInfo field = typeof(LocalizationBuildTools).GetField(
            "RequiredBattleFlowKeys",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(field, Is.Not.Null, "LocalizationBuildTools must declare required battle-flow keys.");
        Assert.That((string[])field.GetValue(null), Is.EquivalentTo(RequiredBattleFlowKeys));
    }

    /// <summary>确认作者 Excel、生成 GameData 与 Unity String Table 共同锁定 M10 的默认战斗黄金基线。</summary>
    [Test]
    public void GoldenBaseline_AgreesAcrossAuthoringGeneratedAndLocalizedSources()
    {
        Assert.That(ReadAuthoringCell("battle.deck.xlsx", "C5"),
            Is.EqualTo("3002,3002,3002,3002,3002,3003,3003,3003,3003,3004"));
        Assert.That(ReadAuthoringCell("battle.card_effect.xlsx", "E6"), Is.EqualTo("6"));
        Assert.That(ReadAuthoringCell("battle.card_effect.xlsx", "E7"), Is.EqualTo("5"));
        Assert.That(ReadAuthoringCell("battle.card_effect.xlsx", "E8"), Is.EqualTo("8"));
        Assert.That(ReadAuthoringCell("battle.card_effect.xlsx", "E9"), Is.EqualTo("2"));
        Assert.That(ReadAuthoringCell("battle.hero.xlsx", "D5"), Is.EqualTo("30"));
        Assert.That(ReadAuthoringCell("battle.enemy.xlsx", "D5"), Is.EqualTo("20"));

        JObject gameConfig = LoadGeneratedObject(GameConfigPath);
        JObject deck = LoadGeneratedObject(DeckPath);
        JObject effects = LoadGeneratedObject(CardEffectPath);
        JObject heroes = LoadGeneratedObject(HeroPath);
        JObject enemies = LoadGeneratedObject(EnemyPath);
        Assert.That(gameConfig.Value<int>("initialHandCount"), Is.EqualTo(5));
        Assert.That(gameConfig.Value<int>("energyPerRound"), Is.EqualTo(3));
        CollectionAssert.AreEqual(
            new[] { 3002, 3002, 3002, 3002, 3002, 3003, 3003, 3003, 3003, 3004 },
            deck["1001"].Value<JArray>("card_template_ids").Values<int>());
        Assert.That(effects["4002"].Value<int>("value"), Is.EqualTo(6));
        Assert.That(effects["4003"].Value<int>("value"), Is.EqualTo(5));
        Assert.That(effects["4004"].Value<int>("value"), Is.EqualTo(8));
        Assert.That(effects["4005"].Value<int>("value"), Is.EqualTo(2));
        Assert.That(heroes["1001"].Value<int>("max_health"), Is.EqualTo(30));
        Assert.That(enemies["2001"].Value<int>("max_health"), Is.EqualTo(20));

        IReadOnlyList<I18nExcelEntry> sourceEntries = I18nExcelReader.Read(
            Path.Combine(GetWorkspaceDirectory(), "DataTables", "Datas", "i18n.xlsx"),
            "i18n",
            new[] { "en", "zh-CN" });
        Dictionary<string, I18nExcelEntry> entriesByKey = sourceEntries.ToDictionary(entry => entry.Key);
        StringTable english = AssetDatabase.LoadAssetAtPath<StringTable>(EnglishTablePath);
        StringTable chinese = AssetDatabase.LoadAssetAtPath<StringTable>(ChineseTablePath);
        Assert.That(english, Is.Not.Null);
        Assert.That(chinese, Is.Not.Null);

        AssertLocalizedEntry(entriesByKey, english, chinese,
            "battle.card.strike.name", "Strike", "打击", false);
        AssertLocalizedEntry(entriesByKey, english, chinese,
            "battle.card.strike.description", "Deal {damage} damage.", "造成 {damage} 点伤害。", true);
        AssertLocalizedEntry(entriesByKey, english, chinese,
            "battle.card.defend.name", "Defend", "防御", false);
        AssertLocalizedEntry(entriesByKey, english, chinese,
            "battle.card.defend.description", "Gain {block} Block.", "获得 {block} 点格挡。", true);
        AssertLocalizedEntry(entriesByKey, english, chinese,
            "battle.card.bash.name", "Bash", "重击", false);
        AssertLocalizedEntry(entriesByKey, english, chinese,
            "battle.card.bash.description",
            "Deal {damage} damage. Apply {vulnerable} {keywordVulnerable}.",
            "造成 {damage} 点伤害。施加 {vulnerable} 层{keywordVulnerable}。",
            true);
    }

    /// <summary>读取生成 JSON 对象，保证黄金数值从运行时实际加载的内容断言。</summary>
    private static JObject LoadGeneratedObject(string assetPath)
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        Assert.That(asset, Is.Not.Null, $"Generated asset is missing: {assetPath}");
        return JObject.Parse(asset.text);
    }

    /// <summary>断言单个翻译键在 Excel 来源和两个 Unity String Table 中拥有同一文本与 Smart 标记。</summary>
    private static void AssertLocalizedEntry(
        IReadOnlyDictionary<string, I18nExcelEntry> sourceEntries,
        StringTable english,
        StringTable chinese,
        string key,
        string expectedEnglish,
        string expectedChinese,
        bool expectedSmart)
    {
        Assert.That(sourceEntries.TryGetValue(key, out I18nExcelEntry source), Is.True, $"Excel key is missing: {key}");
        Assert.That(source.Translations["en"], Is.EqualTo(expectedEnglish));
        Assert.That(source.Translations["zh-CN"], Is.EqualTo(expectedChinese));
        Assert.That(source.IsSmart, Is.EqualTo(expectedSmart));

        StringTableEntry englishEntry = english.GetEntry(key);
        StringTableEntry chineseEntry = chinese.GetEntry(key);
        Assert.That(englishEntry, Is.Not.Null, $"English key is missing: {key}");
        Assert.That(chineseEntry, Is.Not.Null, $"Chinese key is missing: {key}");
        Assert.That(englishEntry.Value, Is.EqualTo(expectedEnglish));
        Assert.That(chineseEntry.Value, Is.EqualTo(expectedChinese));
        Assert.That(englishEntry.IsSmart, Is.EqualTo(expectedSmart));
        Assert.That(chineseEntry.IsSmart, Is.EqualTo(expectedSmart));
    }

    /// <summary>从项目相对 DataTables/Datas 作者表读取指定的 Sheet1 单元格，不把生成 JSON 误当成内容来源。</summary>
    private static string ReadAuthoringCell(string workbookName, string cellReference)
    {
        string workbookPath = Path.Combine(
            GetWorkspaceDirectory(),
            "DataTables",
            "Datas",
            workbookName);
        using var stream = File.OpenRead(workbookPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        IReadOnlyList<string> sharedStrings = ReadSharedStrings(archive);
        ZipArchiveEntry sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidOperationException($"Workbook has no Sheet1 data: {workbookName}");
        using Stream sheetStream = sheetEntry.Open();
        XDocument worksheet = XDocument.Load(sheetStream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XElement cell = worksheet.Descendants(spreadsheet + "c")
            .SingleOrDefault(candidate => (string)candidate.Attribute("r") == cellReference);
        if (cell == null)
            throw new InvalidOperationException($"Workbook cell is missing: {workbookName}!{cellReference}");

        string value = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
        if ((string)cell.Attribute("t") != "s")
            return value;
        if (!int.TryParse(value, out int index) || index < 0 || index >= sharedStrings.Count)
            throw new InvalidOperationException($"Workbook shared-string index is invalid: {workbookName}!{cellReference}");

        return sharedStrings[index];
    }

    /// <summary>读取 XLSX 的共享字符串表，使测试能同时解析文本与数值作者表。</summary>
    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return Array.Empty<string>();

        using Stream stream = entry.Open();
        XDocument document = XDocument.Load(stream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document
            .Descendants(spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
            .ToArray();
    }

    /// <summary>从 Unity 项目目录向上定位仓库根目录，所有作者表路径保持项目相对语义。</summary>
    private static string GetWorkspaceDirectory()
    {
        DirectoryInfo unityProject = Directory.GetParent(Application.dataPath)
            ?? throw new InvalidOperationException("Unable to determine the Unity project directory.");
        return unityProject.Parent?.FullName
            ?? throw new InvalidOperationException("Unable to determine the workspace directory.");
    }
}
