using System;
using System.IO;
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
    private const string GameDataLabel = "GameData";

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

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("TinySpire local Addressables content configured.");
    }

    [MenuItem("TinySpire/Addressables/Build Local Content")]
    public static void BuildLocalContent()
    {
        ConfigureLocalContent();
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
            throw new InvalidOperationException($"Addressables build failed: {result.Error}");

        Debug.Log($"TinySpire Addressables content built: {result.OutputPath}");
    }

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
