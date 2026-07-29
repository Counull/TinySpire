using cfg;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YooAsset;

/// <summary>
/// 统一配置服务：加载 Luban 表格（Tables）和手写 JSON 配置（GameConfig）。
/// </summary>
public sealed class ConfigService
{
    private ResourcePackage _package;

    public Tables Tables { get; private set; }
    public GameConfig GameConfig { get; private set; }

    public void Initialize(ResourcePackage package)
    {
        _package = package ?? throw new System.ArgumentNullException(nameof(package));
        Tables = new Tables(LoadTable);
        GameConfig = LoadGameConfig();
    }

    private JArray LoadTable(string tableName)
    {
        if (_package == null)
            throw new System.InvalidOperationException("ConfigService has not been initialized.");

        var handle = _package.LoadAssetSync<TextAsset>($"Assets/GameData/{tableName}.json");
        try
        {
            if (handle.Status != EOperationStatus.Succeed)
                throw new System.InvalidOperationException($"Unable to load table '{tableName}': {handle.LastError}");

            TextAsset textAsset = handle.GetAssetObject<TextAsset>();
            if (textAsset == null)
                throw new System.InvalidOperationException($"Table '{tableName}' did not load as a TextAsset.");

            JToken token = JToken.Parse(textAsset.text);
            if (token is JArray array)
                return array;

            if (token is JObject map)
            {
                var values = new JArray();
                foreach (JProperty property in map.Properties())
                    values.Add(property.Value);
                return values;
            }

            throw new System.InvalidOperationException($"Table '{tableName}' has an unsupported JSON shape.");
        }
        finally
        {
            handle.Release();
        }
    }

    private GameConfig LoadGameConfig()
    {
        var handle = _package.LoadAssetSync<TextAsset>("Assets/GameData/game-config.json");
        try
        {
            if (handle.Status != EOperationStatus.Succeed)
            {
                Debug.LogWarning($"game-config.json 加载失败（{handle.LastError}），将使用默认配置。");
                return new GameConfig();
            }

            TextAsset textAsset = handle.GetAssetObject<TextAsset>();
            if (textAsset == null)
            {
                Debug.LogWarning("game-config.json 加载后为 null，将使用默认配置。");
                return new GameConfig();
            }

            var config = new GameConfig();
            JsonConvert.PopulateObject(textAsset.text, config);
            Debug.Log("game-config.json 已加载。");
            return config;
        }
        finally
        {
            handle.Release();
        }
    }
}
