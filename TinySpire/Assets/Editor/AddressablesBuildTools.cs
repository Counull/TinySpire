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
    private const string HeroTableJsonPath = "Assets/GameData/battle_tbhero.json";
    private const string EnemyTableJsonPath = "Assets/GameData/battle_tbenemy.json";
    private const string CharacterPrefabRoot = "Assets/Arts/Runtime/Character/Prefabs";
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string CardIllustrationRoot = "Assets/Arts/Runtime/Card/Illustrations";
    private const string CatalogPlaceholderIllustrationPath =
        "Assets/Arts/Runtime/Card/Texture/art_placeholder.png";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/LoadingScene.unity",
        "Assets/Scenes/BattleScene.unity"
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
        SyncEntries(settings, characters, ReadCharacterViewEntries(), label: null);

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

    /// <summary>从英雄与敌人生成表收集角色短键，并解析为资源路径与逻辑地址。</summary>
    private static IReadOnlyDictionary<string, string> ReadCharacterViewEntries()
    {
        IReadOnlyDictionary<string, string> pathsByKey = IndexCharacterPrefabPaths();
        JObject heroes = ReadRequiredCharacterTable(HeroTableJsonPath);
        JObject enemies = ReadRequiredCharacterTable(EnemyTableJsonPath);
        return ResolveCharacterViewEntries(pathsByKey, heroes, enemies);
    }

    /// <summary>读取一份必需的角色生成表，并在文件缺失或没有记录时立即失败。</summary>
    private static JObject ReadRequiredCharacterTable(string tableJsonPath)
    {
        if (!File.Exists(tableJsonPath))
            throw new InvalidOperationException($"Generated character table does not exist: {tableJsonPath}");

        JObject table = JObject.Parse(File.ReadAllText(tableJsonPath));
        if (table.Count == 0)
            throw new InvalidOperationException($"Generated character table has no records: {tableJsonPath}");

        return table;
    }

    /// <summary>索引角色 Prefab 专用目录，并拒绝忽略大小写后重名的文件短键。</summary>
    private static IReadOnlyDictionary<string, string> IndexCharacterPrefabPaths()
    {
        var assetPaths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { CharacterPrefabRoot }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetExtension(assetPath).Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                assetPaths.Add(assetPath);
        }

        return IndexCharacterPrefabPaths(assetPaths);
    }

    /// <summary>从给定路径构建角色 Prefab 短键索引，供生产扫描与漂移校验共用。</summary>
    internal static IReadOnlyDictionary<string, string> IndexCharacterPrefabPaths(
        IEnumerable<string> assetPaths)
    {
        if (assetPaths == null)
            throw new ArgumentNullException(nameof(assetPaths));

        var pathsByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string assetPath in assetPaths)
        {
            string key = Path.GetFileNameWithoutExtension(assetPath);
            CharacterViewAddress.FromKey(key);
            if (pathsByKey.TryGetValue(key, out string existingPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate character view key '{key}': {existingPath}, {assetPath}");
            }

            pathsByKey.Add(key, assetPath);
        }

        if (pathsByKey.Count == 0)
            throw new InvalidOperationException($"Character prefab folder is empty: {CharacterPrefabRoot}");

        return pathsByKey;
    }

    /// <summary>把角色表短键解析成精确 Addressables 清单，并校验缺失、大小写与 Prefab 契约。</summary>
    internal static IReadOnlyDictionary<string, string> ResolveCharacterViewEntries(
        IReadOnlyDictionary<string, string> pathsByKey,
        params JObject[] tables)
    {
        if (pathsByKey == null)
            throw new ArgumentNullException(nameof(pathsByKey));
        if (tables == null)
            throw new ArgumentNullException(nameof(tables));

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JObject table in tables)
        {
            if (table == null || table.Count == 0)
                throw new InvalidOperationException("Generated character table has no records.");

            foreach (JProperty record in table.Properties())
            {
                string key = (string)record.Value["view_prefab_key"];
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException($"Character template {record.Name} has no view_prefab_key.");

                CharacterViewAddress.FromKey(key);
                if (!pathsByKey.TryGetValue(key, out string assetPath))
                {
                    throw new InvalidOperationException(
                        $"Character template {record.Name} view prefab key does not exist: {key}");
                }

                string assetKey = Path.GetFileNameWithoutExtension(assetPath);
                if (!string.Equals(key, assetKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Character template {record.Name} view prefab key casing must match the asset filename: {key} != {assetKey}");
                }

                ValidateCharacterViewPrefab(assetPath, key);
                entries[assetPath] = CharacterViewAddress.FromKey(key);
            }
        }

        if (entries.Count == 0)
            throw new InvalidOperationException("Generated character tables do not contain any view prefab keys.");

        return entries;
    }

    /// <summary>确认角色短键指向可实例化且包含 SpriteRenderer 的 Prefab。</summary>
    private static void ValidateCharacterViewPrefab(string assetPath, string key)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        ValidateCharacterViewPrefabContract(prefab, key, assetPath);
    }

    /// <summary>确认角色 Prefab 具有运行时可发现的启用 SpriteRenderer，供构建与测试共用。</summary>
    internal static void ValidateCharacterViewPrefabContract(
        GameObject prefab,
        string key,
        string assetPath)
    {
        if (prefab == null)
            throw new InvalidOperationException($"Character view key '{key}' is not an importable Prefab: {assetPath}");
        if (prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: false) == null)
        {
            throw new InvalidOperationException(
                $"Character view key '{key}' must reference a Prefab containing an active SpriteRenderer: {assetPath}");
        }
    }

    /// <summary>从 Luban 生成的牌表读取短键，并解析为资源路径与逻辑地址。</summary>
    private static IReadOnlyDictionary<string, string> ReadCardIllustrationEntries()
    {
        if (!File.Exists(CardTableJsonPath))
            throw new InvalidOperationException($"Generated card table does not exist: {CardTableJsonPath}");

        IReadOnlyDictionary<string, string> pathsByKey = IndexCardIllustrationPaths();
        JObject cards = JObject.Parse(File.ReadAllText(CardTableJsonPath));
        return ResolveCardIllustrationEntries(pathsByKey, cards);
    }

    /// <summary>用当前专用素材目录校验卡表中的全部牌面短键与 Sprite 导入契约。</summary>
    internal static void ValidateCardIllustrations(JObject cards)
    {
        ResolveCardIllustrationEntries(IndexCardIllustrationPaths(), cards);
    }

    /// <summary>把卡牌短键解析成精确 Addressables 清单，并校验缺失、大小写与 Sprite 契约。</summary>
    internal static IReadOnlyDictionary<string, string> ResolveCardIllustrationEntries(
        IReadOnlyDictionary<string, string> pathsByKey,
        JObject cards)
    {
        if (pathsByKey == null)
            throw new ArgumentNullException(nameof(pathsByKey));
        if (cards == null || cards.Count == 0)
            throw new InvalidOperationException("Generated card table has no records.");

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
            IndexCardIllustrationPath(pathsByKey, assetPath);
        }
        IndexCardIllustrationPath(pathsByKey, CatalogPlaceholderIllustrationPath);

        if (pathsByKey.Count == 0)
            throw new InvalidOperationException($"Card illustration folder is empty: {CardIllustrationRoot}");

        return pathsByKey;
    }

    /// <summary>把一个明确允许的牌面资源路径加入短键索引，并拒绝缺失或重名。</summary>
    private static void IndexCardIllustrationPath(
        IDictionary<string, string> pathsByKey,
        string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) == null)
            throw new InvalidOperationException($"Card illustration asset does not exist: {assetPath}");

        string key = Path.GetFileNameWithoutExtension(assetPath);
        CardIllustrationAddress.FromKey(key);
        if (pathsByKey.TryGetValue(key, out string existingPath))
        {
            throw new InvalidOperationException(
                $"Duplicate card illustration key '{key}': {existingPath}, {assetPath}");
        }

        pathsByKey.Add(key, assetPath);
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
