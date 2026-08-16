using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

/// <summary>
/// 校验已提交的 Unity Localization 表资源与卡牌配置是否一致。
/// 不创建或改写翻译文本，翻译资源是唯一内容来源。
/// </summary>
public static class LocalizationBuildTools
{
    private const string CardDataPath = "Assets/GameData/battle_tbcard.json";
    private const string CardEffectDataPath = "Assets/GameData/battle_tbcardeffect.json";
    private const string HeroDataPath = "Assets/GameData/battle_tbhero.json";
    private const string EnemyDataPath = "Assets/GameData/battle_tbenemy.json";
    private const string I18nWorkbookRelativePath = "DataTables/Datas/i18n.xlsx";
    private const string I18nSheetName = "i18n";

    private static readonly Regex ArgumentPattern =
        new Regex(@"\{([A-Za-z][A-Za-z0-9]*)\}", RegexOptions.Compiled);

    private static readonly HashSet<string> SharedArguments = new HashSet<string>(StringComparer.Ordinal)
    {
        "keywordStrength",
        "keywordVulnerable"
    };

    private static readonly string[] RequiredLocaleCodes =
    {
        "en",
        "zh-CN"
    };

    private static readonly string[] RequiredKeywordKeys =
    {
        "battle.keyword.strength.name",
        "battle.keyword.vulnerable.name"
    };

    private static readonly string[] RequiredBattleHudKeys =
    {
        "battle.card_pile.draw.name",
        "battle.card_pile.discard.name",
        "battle.card_pile.exhaust.name"
    };

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

    private static readonly string[] RequiredRunEntryKeys =
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
        "run.entry.save.rollback.confirm"
    };

    /// <summary>
    /// 将 Excel 翻译源表同步到 Battle Cards String Table，并立即校验结果。
    /// </summary>
    [MenuItem("TinySpire/Localization/Import Battle Card Text from Excel")]
    public static void ImportBattleCardTextFromExcel()
    {
        IReadOnlyList<I18nExcelEntry> entries = ReadExcelEntries();
        StringTableCollection collection = GetBattleCardCollection();
        ImportEntries(collection, entries);
        ValidateCardLocalization(collection, entries);
        Debug.Log("TinySpire battle card localization imported from Excel and validated.");
    }

    /// <summary>
    /// 校验 Excel 翻译源表与 Battle Cards 表、卡牌配置和效果引用是否一致。
    /// </summary>
    [MenuItem("TinySpire/Localization/Validate Battle Card Text")]
    public static void ValidateBattleCardText()
    {
        IReadOnlyList<I18nExcelEntry> entries = ReadExcelEntries();
        StringTableCollection collection = GetBattleCardCollection();
        ValidateCardLocalization(collection, entries);
        Debug.Log("TinySpire battle card localization validation passed.");
    }

    /// <summary>按每种要求语言校验卡牌 key、参数模板和效果绑定。</summary>
    private static void ValidateCardLocalization(
        StringTableCollection collection,
        IReadOnlyList<I18nExcelEntry> entries)
    {
        ValidateRequiredLocales();

        TextAsset cardData = AssetDatabase.LoadAssetAtPath<TextAsset>(CardDataPath)
            ?? throw new InvalidOperationException($"Generated card data does not exist: {CardDataPath}");
        TextAsset effectData = AssetDatabase.LoadAssetAtPath<TextAsset>(CardEffectDataPath)
            ?? throw new InvalidOperationException($"Generated effect data does not exist: {CardEffectDataPath}");
        TextAsset heroData = AssetDatabase.LoadAssetAtPath<TextAsset>(HeroDataPath)
            ?? throw new InvalidOperationException($"Generated hero data does not exist: {HeroDataPath}");
        TextAsset enemyData = AssetDatabase.LoadAssetAtPath<TextAsset>(EnemyDataPath)
            ?? throw new InvalidOperationException($"Generated enemy data does not exist: {EnemyDataPath}");
        JObject cards = JObject.Parse(cardData.text);
        JObject effects = JObject.Parse(effectData.text);
        JObject heroes = JObject.Parse(heroData.text);
        JObject enemies = JObject.Parse(enemyData.text);
        Dictionary<string, I18nExcelEntry> entriesByKey = IndexEntries(entries);
        var requiredKeys = new HashSet<string>(RequiredKeywordKeys, StringComparer.Ordinal);
        requiredKeys.UnionWith(RequiredBattleHudKeys);
        requiredKeys.UnionWith(RequiredBattleFlowKeys);
        requiredKeys.UnionWith(RequiredRunEntryKeys);
        foreach (JProperty cardProperty in cards.Properties())
        {
            JObject card = (JObject)cardProperty.Value;
            requiredKeys.Add(card.Value<string>("name_i18n_key"));
            requiredKeys.Add(card.Value<string>("description_i18n_key"));
            requiredKeys.Add(card.Value<string>("upgraded_description_i18n_key"));
        }
        AddParticipantNameKeys(requiredKeys, heroes, "Hero");
        AddParticipantNameKeys(requiredKeys, enemies, "Enemy");
        ValidateExcelCoverage(entriesByKey, requiredKeys);

        foreach (string localeCode in RequiredLocaleCodes)
        {
            StringTable table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable
                ?? throw new InvalidOperationException(
                    $"String table collection '{LocalizationService.BattleCardTableName}' " +
                    $"does not contain required locale '{localeCode}'.");

            foreach (string keywordKey in RequiredKeywordKeys)
                RequireEntry(table, keywordKey);
            foreach (string battleHudKey in RequiredBattleHudKeys)
                RequireEntry(table, battleHudKey);
            foreach (string battleFlowKey in RequiredBattleFlowKeys)
                RequireEntry(table, battleFlowKey);
            foreach (string runEntryKey in RequiredRunEntryKeys)
                RequireEntry(table, runEntryKey);
            ValidateParticipantNames(table, heroes, "Hero");
            ValidateParticipantNames(table, enemies, "Enemy");

            foreach (JProperty cardProperty in cards.Properties())
            {
                JObject card = (JObject)cardProperty.Value;
                string nameKey = card.Value<string>("name_i18n_key");
                string descriptionKey = card.Value<string>("description_i18n_key");
                string upgradedDescriptionKey =
                    card.Value<string>("upgraded_description_i18n_key");
                RequireEntry(table, nameKey);
                StringTableEntry description = RequireEntry(table, descriptionKey);
                StringTableEntry upgradedDescription = RequireEntry(table, upgradedDescriptionKey);
                if (!description.IsSmart)
                {
                    throw new InvalidOperationException(
                        $"Card {cardProperty.Name} description for locale " +
                        $"'{table.LocaleIdentifier.Code}' must be a Smart String.");
                }
                if (!upgradedDescription.IsSmart)
                {
                    throw new InvalidOperationException(
                        $"Card {cardProperty.Name} upgraded description for locale " +
                        $"'{table.LocaleIdentifier.Code}' must be a Smart String.");
                }

                var bindingArguments = new HashSet<string>(StringComparer.Ordinal);
                var expectedArguments = new HashSet<string>(StringComparer.Ordinal);
                foreach (JObject binding in card.Value<JArray>("effect_bindings"))
                {
                    string argumentKey = binding.Value<string>("argument_key");
                    if (!bindingArguments.Add(argumentKey))
                        throw new InvalidOperationException(
                            $"Card {cardProperty.Name} has duplicate effect argument '{argumentKey}'.");

                    int effectId = binding.Value<int>("effect_id");
                    JObject effect = effects[effectId.ToString()] as JObject;
                    if (effect == null)
                    {
                        throw new InvalidOperationException(
                            $"Card {cardProperty.Name} references missing effect {effectId}.");
                    }

                    // 非展示规则声明不应为了满足绑定校验而在正文显示无意义的“0”。
                    int effectType = effect.Value<int>("effect_type");
                    if (effectType != (int)cfg.battle.EffectType.RetainBlock &&
                        effectType != (int)cfg.battle.EffectType.PlayTopDrawCardAndExhaust)
                    {
                        expectedArguments.Add(argumentKey);
                    }
                }

                var actualArguments = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match match in ArgumentPattern.Matches(description.LocalizedValue))
                {
                    string argument = match.Groups[1].Value;
                    if (!SharedArguments.Contains(argument))
                        actualArguments.Add(argument);
                }

                if (!expectedArguments.SetEquals(actualArguments))
                {
                    throw new InvalidOperationException(
                        $"Card {cardProperty.Name} template arguments for locale " +
                        $"'{table.LocaleIdentifier.Code}' do not match its effect bindings.");
                }
            }

            ValidateImportedTableMatchesExcel(table, entries, localeCode);
        }
    }

    /// <summary>读取项目内 i18n.xlsx 的全部翻译行。</summary>
    private static IReadOnlyList<I18nExcelEntry> ReadExcelEntries()
    {
        return I18nExcelReader.Read(
            GetI18nWorkbookPath(),
            I18nSheetName,
            RequiredLocaleCodes);
    }

    /// <summary>取得已创建的 Battle Cards String Table Collection。</summary>
    private static StringTableCollection GetBattleCardCollection()
    {
        return LocalizationEditorSettings.GetStringTableCollection(LocalizationService.BattleCardTableName)
            ?? throw new InvalidOperationException(
                $"String table collection '{LocalizationService.BattleCardTableName}' does not exist.");
    }

    /// <summary>将 Excel 行写入每种要求语言的 String Table，不删除其他条目。</summary>
    private static void ImportEntries(
        StringTableCollection collection,
        IReadOnlyList<I18nExcelEntry> entries)
    {
        foreach (string localeCode in RequiredLocaleCodes)
        {
            StringTable table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable
                ?? throw new InvalidOperationException(
                    $"String table collection '{LocalizationService.BattleCardTableName}' " +
                    $"does not contain required locale '{localeCode}'.");

            foreach (I18nExcelEntry entry in entries)
            {
                StringTableEntry tableEntry = table.GetEntry(entry.Key)
                    ?? table.AddEntry(entry.Key, entry.Translations[localeCode]);
                tableEntry.Value = entry.Translations[localeCode];
                tableEntry.IsSmart = entry.IsSmart;
            }

            EditorUtility.SetDirty(table);
        }

        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();
    }

    /// <summary>以 key 建立 Excel 行索引，供覆盖范围和一致性校验使用。</summary>
    private static Dictionary<string, I18nExcelEntry> IndexEntries(IReadOnlyList<I18nExcelEntry> entries)
    {
        var entriesByKey = new Dictionary<string, I18nExcelEntry>(StringComparer.Ordinal);
        foreach (I18nExcelEntry entry in entries)
            entriesByKey.Add(entry.Key, entry);
        return entriesByKey;
    }

    /// <summary>将 Hero 或 Enemy 配置引用的名称 key 加入本地化覆盖范围。</summary>
    private static void AddParticipantNameKeys(
        ISet<string> requiredKeys,
        JObject participants,
        string participantType)
    {
        foreach (JProperty participantProperty in participants.Properties())
        {
            string key = participantProperty.Value.Value<string>("name_i18n_key");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    $"{participantType} {participantProperty.Name} has no name_i18n_key.");
            }

            requiredKeys.Add(key);
        }
    }

    /// <summary>确认静态参与者名称存在且不被声明为 Smart String。</summary>
    private static void ValidateParticipantNames(StringTable table, JObject participants, string participantType)
    {
        foreach (JProperty participantProperty in participants.Properties())
        {
            string key = participantProperty.Value.Value<string>("name_i18n_key");
            StringTableEntry entry = RequireEntry(table, key);
            if (entry.IsSmart)
            {
                throw new InvalidOperationException(
                    $"{participantType} {participantProperty.Name} name '{key}' for locale " +
                    $"'{table.LocaleIdentifier.Code}' must not be a Smart String.");
            }
        }
    }

    /// <summary>确认 Excel 至少维护了当前运行时会读取的全部 key。</summary>
    private static void ValidateExcelCoverage(
        IReadOnlyDictionary<string, I18nExcelEntry> entriesByKey,
        IEnumerable<string> requiredKeys)
    {
        foreach (string key in requiredKeys)
        {
            if (!entriesByKey.ContainsKey(key))
                throw new InvalidOperationException($"i18n workbook is missing required key '{key}'.");
        }
    }

    /// <summary>确认运行时 String Table 仍与 Excel 的文本和 Smart String 标记完全一致。</summary>
    private static void ValidateImportedTableMatchesExcel(
        StringTable table,
        IReadOnlyList<I18nExcelEntry> entries,
        string localeCode)
    {
        foreach (I18nExcelEntry entry in entries)
        {
            StringTableEntry tableEntry = RequireEntry(table, entry.Key);
            if (tableEntry.Value != entry.Translations[localeCode]
                || tableEntry.IsSmart != entry.IsSmart)
            {
                throw new InvalidOperationException(
                    $"String table '{table.LocaleIdentifier.Code}' does not match i18n workbook key '{entry.Key}'. " +
                    "Run 'TinySpire/Localization/Import Battle Card Text from Excel'.");
            }
        }
    }

    /// <summary>从 Unity 项目根目录拼出受版本控制的 Excel 源表路径。</summary>
    private static string GetI18nWorkbookPath()
    {
        string projectDirectory = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to determine Unity project directory.");
        return Path.Combine(projectDirectory, "..", I18nWorkbookRelativePath);
    }

    /// <summary>确认 en/zh-CN 已配置，且中文显式回退到英文。</summary>
    private static void ValidateRequiredLocales()
    {
        Locale english = LocalizationEditorSettings.GetLocale("en")
            ?? throw new InvalidOperationException("Required locale 'en' does not exist.");
        Locale chinese = LocalizationEditorSettings.GetLocale("zh-CN")
            ?? throw new InvalidOperationException("Required locale 'zh-CN' does not exist.");
        FallbackLocale fallback = chinese.Metadata.GetMetadata<FallbackLocale>();
        if (fallback?.Locale != english)
        {
            throw new InvalidOperationException(
                "Locale 'zh-CN' must use project locale 'en' as its explicit fallback.");
        }
    }

    /// <summary>获取一个非空翻译条目；缺失时抛出包含语言与 key 的错误。</summary>
    private static StringTableEntry RequireEntry(StringTable table, string key)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null || string.IsNullOrWhiteSpace(entry.LocalizedValue))
        {
            throw new InvalidOperationException(
                $"Missing localization key '{key}' for locale '{table.LocaleIdentifier.Code}'.");
        }

        return entry;
    }

}
