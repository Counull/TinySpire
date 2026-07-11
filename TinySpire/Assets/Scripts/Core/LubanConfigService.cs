using System;
using System.IO;
using cfg;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class LubanConfigService
{
    public Tables Tables { get; }

    public LubanConfigService()
    {
        Tables = new Tables(LoadTable);
    }

    private static JArray LoadTable(string tableName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "GameData", tableName + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"找不到 Luban 配置表 '{tableName}'，请重新生成配置。", path);
        }

        JToken token = JToken.Parse(File.ReadAllText(path));
        if (token is JArray array)
        {
            return array;
        }

        if (token is JObject map)
        {
            var values = new JArray();
            foreach (JProperty property in map.Properties())
            {
                values.Add(property.Value);
            }

            return values;
        }

        throw new InvalidOperationException($"Luban 配置表 '{tableName}' 的 JSON 格式无效。");
    }
}
