using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TinySpire.Battle;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class AddressablesBuildTools
{
    private const string ScenesGroupName = "TinySpire Scenes";
    private const string GameDataGroupName = "TinySpire GameData";
    private const string CharactersGroupName = "TinySpire Characters";
    private const string CardArtGroupName = "TinySpire Card Art";
    private const string GameDataLabel = "GameData";
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string CardIllustrationRoot = "Assets/Arts/Runtime/Card/Illustrations";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/LoadingScene.unity",
        "Assets/Scenes/BattleScene.unity"
    };

    private static readonly string[] CharacterPrefabPaths =
    {
        "Assets/Arts/Runtime/Character/Prefabs/pfb_char_player.prefab",
        "Assets/Arts/Runtime/Character/Prefabs/pfb_char_enemy.prefab"
    };

    /// <summary>按项目稳定地址配置场景、配置、角色与牌面本地资源组。</summary>
    [MenuItem("TinySpire/Addressables/Configure Local Content")]
    public static void ConfigureLocalContent()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(create: true);
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
        settings.BuildRemoteCatalog = false;
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

        AddressableAssetGroup scenes = EnsureLocalGroup(
            settings,
            ScenesGroupName,
            BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
        foreach (string scenePath in ScenePaths)
            AddEntry(settings, scenes, scenePath, label: null);

        AddressableAssetGroup gameData = EnsureLocalGroup(
            settings,
            GameDataGroupName,
            BundledAssetGroupSchema.BundlePackingMode.PackTogether);
        settings.AddLabel(GameDataLabel);
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/GameData" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                AddEntry(settings, gameData, path, GameDataLabel);
        }

        AddressableAssetGroup characters = EnsureLocalGroup(
            settings,
            CharactersGroupName,
            BundledAssetGroupSchema.BundlePackingMode.PackTogether);
        foreach (string prefabPath in CharacterPrefabPaths)
            AddEntry(settings, characters, prefabPath, label: null);

        AddressableAssetGroup cardArt = EnsureLocalGroup(
            settings,
            CardArtGroupName,
            BundledAssetGroupSchema.BundlePackingMode.PackTogether);
        SyncEntries(settings, cardArt, ReadCardIllustrationEntries(), label: null);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("TinySpire local Addressables content configured.");
    }

    /// <summary>同步本地资源组并构建供启动链路加载的 Addressables 内容。</summary>
    [MenuItem("TinySpire/Addressables/Build Local Content")]
    public static void BuildLocalContent()
    {
        ConfigureLocalContent();
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
            throw new InvalidOperationException($"Addressables build failed: {result.Error}");

        Debug.Log($"TinySpire Addressables content built: {result.OutputPath}");
    }

    /// <summary>取得或创建一个使用本地构建与加载路径的 Addressables 资源组。</summary>
    private static AddressableAssetGroup EnsureLocalGroup(
        AddressableAssetSettings settings,
        string groupName,
        BundledAssetGroupSchema.BundlePackingMode bundleMode)
    {
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            group = settings.CreateGroup(
                groupName,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
        schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
        schema.BundleMode = bundleMode;
        schema.IncludeInBuild = true;
        EditorUtility.SetDirty(schema);
        return group;
    }

    /// <summary>从 Luban 生成的牌表读取短键，并解析为资源路径与逻辑地址。</summary>
    private static IReadOnlyDictionary<string, string> ReadCardIllustrationEntries()
    {
        if (!File.Exists(CardTableJsonPath))
            throw new InvalidOperationException($"Generated card table does not exist: {CardTableJsonPath}");

        IReadOnlyDictionary<string, string> pathsByKey = IndexCardIllustrationPaths();
        JObject cards = JObject.Parse(File.ReadAllText(CardTableJsonPath));
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JProperty card in cards.Properties())
        {
            string key = (string)card.Value["illustration_key"];
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException($"Card template {card.Name} has no illustration_key.");

            if (!pathsByKey.TryGetValue(key, out string assetPath))
                throw new InvalidOperationException($"Card template {card.Name} illustration key does not exist: {key}");

            string assetKey = Path.GetFileNameWithoutExtension(assetPath);
            if (!string.Equals(key, assetKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Card template {card.Name} illustration key casing must match the asset filename: {key} != {assetKey}");
            }

            ValidateSingleSprite(assetPath, key);
            entries[assetPath] = CardIllustrationAddress.FromKey(key);
        }

        if (entries.Count == 0)
            throw new InvalidOperationException("Generated card table does not contain any illustration keys.");

        return entries;
    }

    /// <summary>递归索引专用目录内的图片短名，并用不区分大小写的规则阻止重名。</summary>
    private static IReadOnlyDictionary<string, string> IndexCardIllustrationPaths()
    {
        var pathsByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CardIllustrationRoot }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string key = Path.GetFileNameWithoutExtension(assetPath);
            CardIllustrationAddress.FromKey(key);
            if (pathsByKey.TryGetValue(key, out string existingPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate card illustration key '{key}': {existingPath}, {assetPath}");
            }

            pathsByKey.Add(key, assetPath);
        }

        if (pathsByKey.Count == 0)
            throw new InvalidOperationException($"Card illustration folder is empty: {CardIllustrationRoot}");

        return pathsByKey;
    }

    /// <summary>确保短键指向单 Sprite 且关闭 mipmap，避免运行时加载到错误的主资源。</summary>
    private static void ValidateSingleSprite(string assetPath, string key)
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
            throw new InvalidOperationException($"Card illustration key '{key}' is not an importable Sprite: {assetPath}");

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null
            || importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.mipmapEnabled)
        {
            throw new InvalidOperationException(
                $"Card illustration key '{key}' must use Sprite/Single with mipmaps disabled: {assetPath}");
        }
    }

    /// <summary>让专用资源组与当前配置地址集合完全一致，并移除已经失效的旧条目。</summary>
    private static void SyncEntries(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        IReadOnlyDictionary<string, string> addressesByAssetPath,
        string label)
    {
        var expectedGuids = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> addressByAssetPath in addressesByAssetPath)
        {
            string assetPath = addressByAssetPath.Key;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException($"Addressable asset does not exist: {assetPath}");

            expectedGuids.Add(guid);
            AddEntry(settings, group, assetPath, label, addressByAssetPath.Value);
        }

        var staleEntries = new List<AddressableAssetEntry>();
        foreach (AddressableAssetEntry entry in group.entries)
        {
            if (!expectedGuids.Contains(entry.guid))
                staleEntries.Add(entry);
        }
        foreach (AddressableAssetEntry staleEntry in staleEntries)
            group.RemoveAssetEntry(staleEntry, postEvent: false);
    }

    /// <summary>将资源加入目标组；未指定逻辑地址时继续使用完整 Assets 路径。</summary>
    private static void AddEntry(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        string assetPath,
        string label,
        string address = null)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException($"Addressable asset does not exist: {assetPath}");

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
        entry.address = string.IsNullOrEmpty(address) ? assetPath : address;
        if (!string.IsNullOrEmpty(label))
            entry.SetLabel(label, enable: true, force: true, postEvent: false);
    }
}
