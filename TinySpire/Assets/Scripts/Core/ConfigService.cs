using System;
using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 统一配置服务：加载 Luban 表格（Tables）和手写 JSON 配置（GameConfig）。
/// </summary>
public sealed class ConfigService
{
    private static readonly string[] TableNames =
    {
        "battle_tbhero",
        "battle_tbenemy",
        "battle_tbdeck",
        "battle_tbcard",
        "battle_tbcardeffect",
        "battle_tbencounter",
        "battle_tbenemybehaviorgroup",
        "battle_tbenemybehavior"
    };

    public Tables Tables { get; private set; }
    public GameConfig GameConfig { get; private set; }

    /// <summary>返回运行时预加载的稳定表名清单，供 Editor 构建期校验读取。</summary>
    internal static IReadOnlyList<string> RequiredTableNames => Array.AsReadOnly(TableNames);

    /// <summary>通过生产 Addressables 服务初始化全部配置。</summary>
    public UniTask InitializeAsync(AddressableAssetService assets)
    {
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));

        return InitializeAsync((IConfigTextLoader)assets);
    }

    /// <summary>通过可替换的文本加载边界原子初始化配置，供 Editor 契约测试使用。</summary>
    internal async UniTask InitializeAsync(IConfigTextLoader assets)
    {
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));
        if (Tables != null && GameConfig != null)
            return;

        var tableData = new Dictionary<string, JArray>(TableNames.Length);
        foreach (string tableName in TableNames)
        {
            string address = ToGameDataAddress($"{tableName}.json");
            string json = await LoadTextAsync(assets, address, tableName);
            tableData.Add(tableName, ParseTable(tableName, address, json));
        }

        Tables tables = CreateTables(tableData);
        GameConfig gameConfig = await LoadGameConfigAsync(assets);
        Tables = tables;
        GameConfig = gameConfig;
    }

    /// <summary>读取表格文本并将底层加载异常统一转换为带表名的 typed failure。</summary>
    private static async UniTask<string> LoadTextAsync(
        IConfigTextLoader assets,
        string address,
        string tableName)
    {
        try
        {
            return await assets.LoadTextAsync(address);
        }
        catch (Exception exception)
        {
            throw new ConfigInitializationException(
                address,
                tableName,
                ConfigInitializationFailureReason.AssetLoadFailed,
                exception);
        }
    }

    /// <summary>在所有表数据已就绪后构造 Luban Tables，避免构造期失败发布半成品。</summary>
    private static Tables CreateTables(IReadOnlyDictionary<string, JArray> tableData)
    {
        try
        {
            return new Tables(tableName =>
                tableData.TryGetValue(tableName, out JArray data)
                    ? data
                    : throw new InvalidOperationException($"Table '{tableName}' was not preloaded."));
        }
        catch (Exception exception)
        {
            throw new ConfigInitializationException(
                "Assets/GameData",
                null,
                ConfigInitializationFailureReason.TableConstructionFailed,
                exception);
        }
    }

    /// <summary>解析单张 Luban 表并拒绝无法构造成行集合的根节点。</summary>
    private static JArray ParseTable(string tableName, string address, string json)
    {
        JToken token;
        try
        {
            token = JToken.Parse(json);
        }
        catch (Exception exception)
        {
            throw new ConfigInitializationException(
                address,
                tableName,
                ConfigInitializationFailureReason.InvalidJson,
                exception);
        }

        JArray rows;
        if (token is JObject map)
        {
            rows = new JArray();
            foreach (JProperty property in map.Properties())
                rows.Add(property.Value);
        }
        else if (token is JArray array)
        {
            rows = array;
        }
        else
        {
            throw new ConfigInitializationException(
                address,
                tableName,
                ConfigInitializationFailureReason.UnsupportedTableShape);
        }

        ValidateTableRows(tableName, address, rows);
        return rows;
    }

    /// <summary>验证 Luban 表的每个行条目都是对象，避免不带表名的构造期失败。</summary>
    private static void ValidateTableRows(string tableName, string address, JArray rows)
    {
        foreach (JToken row in rows)
        {
            if (row is JObject)
                continue;

            throw new ConfigInitializationException(
                address,
                tableName,
                ConfigInitializationFailureReason.InvalidTableRowShape);
        }
    }

    /// <summary>通过可替换加载边界加载 game-config，并在全部字段有效后返回配置。</summary>
    private static async UniTask<GameConfig> LoadGameConfigAsync(IConfigTextLoader assets)
    {
        string address = ToGameDataAddress("game-config.json");
        string json = await LoadTextAsync(assets, address, null);
        JObject configObject = ParseGameConfigObject(address, json);
        RequireIntegerProperty(configObject, address, "initialHandCount");
        RequireIntegerProperty(configObject, address, "energyPerRound");

        try
        {
            var config = new GameConfig();
            JsonConvert.PopulateObject(json, config);
            Debug.Log("game-config.json 已加载。");
            return config;
        }
        catch (Exception exception)
        {
            throw new ConfigInitializationException(
                address,
                null,
                ConfigInitializationFailureReason.InvalidJson,
                exception);
        }
    }

    /// <summary>将 game-config 文本解析为对象根节点，拒绝数组和标量配置。</summary>
    private static JObject ParseGameConfigObject(string address, string json)
    {
        try
        {
            JToken token = JToken.Parse(json);
            if (token is JObject configObject)
                return configObject;
        }
        catch (Exception exception)
        {
            throw new ConfigInitializationException(
                address,
                null,
                ConfigInitializationFailureReason.InvalidJson,
                exception);
        }

        throw new ConfigInitializationException(
            address,
            null,
            ConfigInitializationFailureReason.InvalidGameConfigShape);
    }

    /// <summary>验证 game-config 中的必需整数属性存在且类型正确。</summary>
    private static void RequireIntegerProperty(JObject configObject, string address, string propertyName)
    {
        if (configObject.TryGetValue(propertyName, out JToken value) && value.Type == JTokenType.Integer)
            return;

        throw new ConfigInitializationException(
            address,
            null,
            ConfigInitializationFailureReason.MissingRequiredGameConfigField);
    }

    private static string ToGameDataAddress(string fileName)
    {
        return $"Assets/GameData/{fileName}";
    }
}
