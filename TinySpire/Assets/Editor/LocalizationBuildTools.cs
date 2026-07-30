using System;
using System.Collections.Generic;
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

    /// <summary>
    /// 校验 Battle Cards 表的语言、条目、Smart String 参数和效果引用。
    /// </summary>
    [MenuItem("TinySpire/Localization/Validate Battle Card Text")]
    public static void ValidateBattleCardText()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(LocalizationService.BattleCardTableName)
            ?? throw new InvalidOperationException(
                $"String table collection '{LocalizationService.BattleCardTableName}' does not exist.");

        ValidateCardLocalization(collection);
        Debug.Log("TinySpire battle card localization validation passed.");
    }

    /// <summary>按每种要求语言校验卡牌 key、参数模板和效果绑定。</summary>
    private static void ValidateCardLocalization(StringTableCollection collection)
    {
        ValidateRequiredLocales();

        TextAsset cardData = AssetDatabase.LoadAssetAtPath<TextAsset>(CardDataPath)
            ?? throw new InvalidOperationException($"Generated card data does not exist: {CardDataPath}");
        TextAsset effectData = AssetDatabase.LoadAssetAtPath<TextAsset>(CardEffectDataPath)
            ?? throw new InvalidOperationException($"Generated effect data does not exist: {CardEffectDataPath}");
        JObject cards = JObject.Parse(cardData.text);
        JObject effects = JObject.Parse(effectData.text);

        foreach (string localeCode in RequiredLocaleCodes)
        {
            StringTable table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable
                ?? throw new InvalidOperationException(
                    $"String table collection '{LocalizationService.BattleCardTableName}' " +
                    $"does not contain required locale '{localeCode}'.");

            foreach (string keywordKey in RequiredKeywordKeys)
                RequireEntry(table, keywordKey);

            foreach (JProperty cardProperty in cards.Properties())
            {
                JObject card = (JObject)cardProperty.Value;
                string nameKey = card.Value<string>("name_i18n_key");
                string descriptionKey = card.Value<string>("description_i18n_key");
                RequireEntry(table, nameKey);
                StringTableEntry description = RequireEntry(table, descriptionKey);
                if (!description.IsSmart)
                {
                    throw new InvalidOperationException(
                        $"Card {cardProperty.Name} description for locale " +
                        $"'{table.LocaleIdentifier.Code}' must be a Smart String.");
                }

                var expectedArguments = new HashSet<string>(StringComparer.Ordinal);
                foreach (JObject binding in card.Value<JArray>("effect_bindings"))
                {
                    string argumentKey = binding.Value<string>("argument_key");
                    if (!expectedArguments.Add(argumentKey))
                        throw new InvalidOperationException(
                            $"Card {cardProperty.Name} has duplicate effect argument '{argumentKey}'.");

                    int effectId = binding.Value<int>("effect_id");
                    if (effects.Property(effectId.ToString()) == null)
                    {
                        throw new InvalidOperationException(
                            $"Card {cardProperty.Name} references missing effect {effectId}.");
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
        }
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
