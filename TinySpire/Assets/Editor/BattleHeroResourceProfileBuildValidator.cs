using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>在本地化和 Addressables 构建前拒绝无效 Hero 资源档案的构建期门禁。</summary>
internal static class BattleHeroResourceProfileBuildValidator
{
    private const string HeroTableJsonRelativePath = "Assets/GameData/battle_tbhero.json";

    /// <summary>读取当前生成的 Hero 表并验证每个资源档案都可安全装配到战斗运行时。</summary>
    internal static void ValidateCurrentProject()
    {
        string heroTableJsonPath = Path.Combine(Application.dataPath, "GameData", "battle_tbhero.json");
        if (!File.Exists(heroTableJsonPath))
        {
            throw new InvalidOperationException(
                $"Generated Hero table does not exist: {HeroTableJsonRelativePath}");
        }

        Validate(JObject.Parse(File.ReadAllText(heroTableJsonPath)));
    }

    /// <summary>验证给定的生成 Hero 表；测试可直接传入最小 JSON 而无需依赖 Unity 资源数据库。</summary>
    internal static void Validate(JObject heroes)
    {
        if (heroes == null)
            throw new ArgumentNullException(nameof(heroes));
        if (heroes.Count == 0)
            throw new InvalidOperationException("Generated Hero table has no records.");

        foreach (JProperty property in heroes.Properties())
        {
            JObject hero = property.Value as JObject
                ?? throw new InvalidOperationException(
                    $"Hero record '{property.Name}' must be an object.");
            int heroId = ReadRequiredInt(hero, "id", property.Name);
            if (!string.Equals(
                    property.Name,
                    heroId.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Hero record key '{property.Name}' does not match id {heroId}.");
            }

            ValidateEnergy(hero, property.Name);
            ValidateAmmo(hero, property.Name);
        }
    }

    /// <summary>验证 Hero 始终拥有正的能量上限，且初始和补充数值不会越界。</summary>
    private static void ValidateEnergy(JObject hero, string recordName)
    {
        int initial = ReadRequiredInt(hero, "initial_energy", recordName);
        int maximum = ReadRequiredInt(hero, "max_energy", recordName);
        int gain = ReadRequiredInt(hero, "energy_gain_per_round", recordName);
        if (initial < 0 || maximum <= 0 || gain < 0 || initial > maximum)
        {
            throw new InvalidOperationException(
                $"Hero {recordName} has invalid Energy profile " +
                $"(initial={initial}, max={maximum}, gain={gain}).");
        }
    }

    /// <summary>验证弹药可整体禁用，但禁用时不允许留下初始值或回合补充。</summary>
    private static void ValidateAmmo(JObject hero, string recordName)
    {
        int initial = ReadRequiredInt(hero, "initial_ammo", recordName);
        int maximum = ReadRequiredInt(hero, "max_ammo", recordName);
        int gain = ReadRequiredInt(hero, "ammo_gain_per_round", recordName);
        if (initial < 0 || maximum < 0 || gain < 0 || initial > maximum ||
            (maximum == 0 && (initial != 0 || gain != 0)))
        {
            throw new InvalidOperationException(
                $"Hero {recordName} has invalid Ammo profile " +
                $"(initial={initial}, max={maximum}, gain={gain}).");
        }
    }

    /// <summary>读取一个必需整数资源字段，并在缺失或类型不匹配时保留 Hero 身份诊断。</summary>
    private static int ReadRequiredInt(JObject hero, string fieldName, string recordName)
    {
        JToken token = hero[fieldName];
        if (token == null || token.Type != JTokenType.Integer)
        {
            throw new InvalidOperationException(
                $"Hero {recordName} has no integer {fieldName}.");
        }

        return (int)token;
    }
}
