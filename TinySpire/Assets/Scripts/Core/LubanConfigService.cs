using System;
using cfg;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YooAsset;

public sealed class LubanConfigService
{
    private ResourcePackage _package;

    public Tables Tables { get; private set; }

    public void Initialize(ResourcePackage package)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));
        Tables = new Tables(LoadTable);
    }

    private JArray LoadTable(string tableName)
    {
        if (_package == null)
            throw new InvalidOperationException("LubanConfigService has not been initialized.");

        var handle = _package.LoadAssetSync<TextAsset>($"Assets/GameData/{tableName}.json");
        try
        {
            if (handle.Status != EOperationStatus.Succeed)
                throw new InvalidOperationException($"Unable to load Luban table '{tableName}': {handle.LastError}");

            TextAsset textAsset = handle.GetAssetObject<TextAsset>();
            if (textAsset == null)
                throw new InvalidOperationException($"Luban table '{tableName}' did not load as a TextAsset.");

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

            throw new InvalidOperationException($"Luban table '{tableName}' has an unsupported JSON shape.");
        }
        finally
        {
            handle.Release();
        }
    }
}
