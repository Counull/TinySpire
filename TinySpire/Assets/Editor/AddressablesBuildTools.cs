using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
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
        SyncEntries(settings, cardArt, ReadCardIllustrationAddresses(), label: null);

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

    /// <summary>从 Luban 生成的卡牌表读取并校验唯一牌面 Sprite 稳定地址。</summary>
    private static IReadOnlyList<string> ReadCardIllustrationAddresses()
    {
        if (!File.Exists(CardTableJsonPath))
            throw new InvalidOperationException($"Generated card table does not exist: {CardTableJsonPath}");

        JObject cards = JObject.Parse(File.ReadAllText(CardTableJsonPath));
        var addresses = new List<string>();
        var uniqueAddresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (JProperty card in cards.Properties())
        {
            string address = (string)card.Value["illustration_address"];
            if (string.IsNullOrWhiteSpace(address))
                throw new InvalidOperationException($"Card template {card.Name} has no illustration_address.");
            if (!address.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Card template {card.Name} illustration address must start with 'Assets/': {address}");
            }
            if (AssetDatabase.LoadAssetAtPath<Sprite>(address) == null)
            {
                throw new InvalidOperationException(
                    $"Card template {card.Name} illustration is not an importable Sprite: {address}");
            }
            if (uniqueAddresses.Add(address))
                addresses.Add(address);
        }

        if (addresses.Count == 0)
            throw new InvalidOperationException("Generated card table does not contain any illustration addresses.");

        return addresses;
    }

    /// <summary>让专用资源组与当前配置地址集合完全一致，并移除已经失效的旧条目。</summary>
    private static void SyncEntries(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        IReadOnlyList<string> assetPaths,
        string label)
    {
        var expectedGuids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string assetPath in assetPaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException($"Addressable asset does not exist: {assetPath}");

            expectedGuids.Add(guid);
            AddEntry(settings, group, assetPath, label);
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

    /// <summary>将指定资源以完整 Assets 路径作为稳定地址加入目标资源组。</summary>
    private static void AddEntry(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        string assetPath,
        string label)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException($"Addressable asset does not exist: {assetPath}");

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
        entry.address = assetPath;
        if (!string.IsNullOrEmpty(label))
            entry.SetLabel(label, enable: true, force: true, postEvent: false);
    }
}
