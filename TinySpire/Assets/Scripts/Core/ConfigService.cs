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

    public async UniTask InitializeAsync(AddressableAssetService assets)
    {
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));
        if (Tables != null && GameConfig != null)
            return;

        var tableData = new Dictionary<string, JArray>(TableNames.Length);
        foreach (string tableName in TableNames)
        {
            string json = await assets.LoadTextAsync(ToGameDataAddress($"{tableName}.json"));
            tableData.Add(tableName, ParseTable(tableName, json));
        }

        Tables = new Tables(tableName =>
            tableData.TryGetValue(tableName, out JArray data)
                ? data
                : throw new InvalidOperationException($"Table '{tableName}' was not preloaded."));
        GameConfig = await LoadGameConfigAsync(assets);
    }

    private static JArray ParseTable(string tableName, string json)
    {
        JToken token = JToken.Parse(json);
        if (token is JArray array)
            return array;

        if (token is JObject map)
        {
            var values = new JArray();
            foreach (JProperty property in map.Properties())
                values.Add(property.Value);
            return values;
        }

        throw new InvalidOperationException($"Table '{tableName}' has an unsupported JSON shape.");
    }

    private static async UniTask<GameConfig> LoadGameConfigAsync(AddressableAssetService assets)
    {
        try
        {
            string json = await assets.LoadTextAsync(ToGameDataAddress("game-config.json"));
            var config = new GameConfig();
            JsonConvert.PopulateObject(json, config);
            Debug.Log("game-config.json 已加载。");
            return config;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"game-config.json 加载失败（{exception.Message}），将使用默认配置。");
            return new GameConfig();
        }
    }

    private static string ToGameDataAddress(string fileName)
    {
        return $"Assets/GameData/{fileName}";
    }
}
