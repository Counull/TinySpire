using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TinySpire.Battle;
using TinySpire.Presentation.Audio;
using TinySpire.Run;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>纯构建 helper 使用的一项 UI 音频资源导入描述。</summary>
internal sealed class UiAudioAssetDescriptor
{
    /// <summary>Unity 工程内的稳定 Assets 路径。</summary>
    public string AssetPath { get; }

    /// <summary>AssetDatabase 声明的主资源类型。</summary>
    public Type MainAssetType { get; }

    /// <summary>该路径是否能以 AudioClip 泛型加载。</summary>
    public bool LoadsAsAudioClip { get; }

    /// <summary>AudioImporter 是否保证资源加载时同步预载音频数据。</summary>
    public bool PreloadAudioData { get; }

    /// <summary>冻结一项供纯解析与生产扫描共用的导入事实。</summary>
    public UiAudioAssetDescriptor(
        string assetPath,
        Type mainAssetType,
        bool loadsAsAudioClip,
        bool preloadAudioData)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            throw new ArgumentException("UI audio asset path is required.", nameof(assetPath));

        AssetPath = assetPath;
        MainAssetType = mainAssetType;
        LoadsAsAudioClip = loadsAsAudioClip;
        PreloadAudioData = preloadAudioData;
    }
}

public static class AddressablesBuildTools
{
    private const string ScenesGroupName = "TinySpire Scenes";
    private const string GameDataGroupName = "TinySpire GameData";
    private const string CharactersGroupName = "TinySpire Characters";
    private const string CardArtGroupName = "TinySpire Card Art";
    internal const string UiAudioGroupName = "TinySpire UI Audio";
    internal const string UiAudioAssetRoot = "Assets/Arts/Runtime/Audio/UI";
    private const string GameDataLabel = "GameData";
    private const string HeroTableJsonPath = "Assets/GameData/battle_tbhero.json";
    private const string EnemyTableJsonPath = "Assets/GameData/battle_tbenemy.json";
    private const string CharacterPrefabRoot = "Assets/Arts/Runtime/Character/Prefabs";
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";
    private const string CardIllustrationRoot = "Assets/Arts/Runtime/Card/Illustrations";
    private const string CatalogPlaceholderIllustrationPath =
        "Assets/Arts/Runtime/Card/Texture/art_placeholder.png";

    /// <summary>UI 音频专用 group 的冻结打包模式。</summary>
    internal static BundledAssetGroupSchema.BundlePackingMode UiAudioBundleMode =>
        BundledAssetGroupSchema.BundlePackingMode.PackTogether;

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/LoadingScene.unity",
        RunSceneAddresses.RunEntry,
        RunSceneAddresses.Battle
    };

    /// <summary>按项目稳定地址配置场景、配置、角色、牌面与 UI 音频本地资源组。</summary>
    [MenuItem("TinySpire/Addressables/Configure Local Content")]
    public static void ConfigureLocalContent()
    {
        IReadOnlyDictionary<string, string> uiAudioEntries = ReadUiAudioEntries();
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

        AddressableAssetGroup uiAudio = EnsureLocalGroup(
            settings,
            UiAudioGroupName,
            UiAudioBundleMode);
        ConfigureUiAudioCatalogKeys(uiAudio.GetSchema<BundledAssetGroupSchema>());
        SyncEntries(settings, uiAudio, uiAudioEntries, label: null);

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

    /// <summary>冻结 UI 音频 catalog 只暴露逻辑地址，拒绝 GUID 与 Label 形成第二组公钥。</summary>
    internal static void ConfigureUiAudioCatalogKeys(BundledAssetGroupSchema schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        schema.IncludeAddressInCatalog = true;
        schema.IncludeGUIDInCatalog = false;
        schema.IncludeLabelsInCatalog = false;
        EditorUtility.SetDirty(schema);
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

    /// <summary>扫描 UI 音频专用根并收集主类型与 AudioClip 泛型加载事实。</summary>
    private static IReadOnlyDictionary<string, string> ReadUiAudioEntries()
    {
        if (!AssetDatabase.IsValidFolder(UiAudioAssetRoot))
            throw new InvalidOperationException($"UI audio asset root does not exist: {UiAudioAssetRoot}");

        var descriptors = new List<UiAudioAssetDescriptor>();
        foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { UiAudioAssetRoot }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(assetPath))
                continue;

            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;

            descriptors.Add(new UiAudioAssetDescriptor(
                assetPath,
                AssetDatabase.GetMainAssetTypeAtPath(assetPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath) != null,
                UsesPreloadedAudioData(importer)));
        }

        return ResolveUiAudioEntries(descriptors);
    }

    /// <summary>按 Standalone override 或默认采样设置判断音频数据是否随资源完整预载。</summary>
    private static bool UsesPreloadedAudioData(AudioImporter importer)
    {
        if (importer == null)
            return false;

        AudioImporterSampleSettings settings =
            importer.ContainsSampleSettingsOverride(BuildTargetGroup.Standalone)
                ? importer.GetOverrideSampleSettings(BuildTargetGroup.Standalone)
                : importer.defaultSampleSettings;
        return settings.preloadAudioData;
    }

    /// <summary>把专用根导入事实解析为目录四项精确清单，并拒绝全部资源契约漂移。</summary>
    internal static IReadOnlyDictionary<string, string> ResolveUiAudioEntries(
        IEnumerable<UiAudioAssetDescriptor> descriptors)
    {
        if (descriptors == null)
            throw new ArgumentNullException(nameof(descriptors));

        var assetsByKey = new Dictionary<string, UiAudioAssetDescriptor>(
            StringComparer.OrdinalIgnoreCase);
        foreach (UiAudioAssetDescriptor descriptor in descriptors)
        {
            if (descriptor == null)
                throw new InvalidOperationException("UI audio asset descriptor cannot be null.");

            string normalizedPath = descriptor.AssetPath.Replace('\\', '/');
            if (!string.Equals(normalizedPath, descriptor.AssetPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"UI audio asset path must use Unity separators: {descriptor.AssetPath}");
            }

            string directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            if (!string.Equals(directory, UiAudioAssetRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"UI audio assets must be direct children of {UiAudioAssetRoot}: {descriptor.AssetPath}");
            }
            if (!string.Equals(Path.GetExtension(normalizedPath), ".wav", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"UI audio asset must use the exact lowercase .wav extension: {descriptor.AssetPath}");
            }

            string key = Path.GetFileNameWithoutExtension(normalizedPath);
            if (assetsByKey.TryGetValue(key, out UiAudioAssetDescriptor duplicate))
            {
                throw new InvalidOperationException(
                    $"Duplicate UI audio key '{key}': {duplicate.AssetPath}, {descriptor.AssetPath}");
            }

            try
            {
                UiAudioAddress.FromKey(key);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"UI audio asset filename is not a canonical short key: {descriptor.AssetPath}",
                    exception);
            }

            if (descriptor.MainAssetType != typeof(AudioClip))
            {
                throw new InvalidOperationException(
                    $"UI audio asset must import with AudioClip as its main type: {descriptor.AssetPath}");
            }
            if (!descriptor.LoadsAsAudioClip)
            {
                throw new InvalidOperationException(
                    $"UI audio asset cannot be loaded as AudioClip: {descriptor.AssetPath}");
            }
            if (!descriptor.PreloadAudioData)
            {
                throw new InvalidOperationException(
                    $"UI audio asset must preload audio data: {descriptor.AssetPath}");
            }

            assetsByKey.Add(key, descriptor);
        }

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < UiAudioCatalog.Ordered.Count; index++)
        {
            UiAudioCueDefinition definition = UiAudioCatalog.Ordered[index];
            if (!assetsByKey.TryGetValue(definition.Key, out UiAudioAssetDescriptor descriptor))
            {
                throw new InvalidOperationException(
                    $"Declared UI audio cue is missing from the dedicated root: {definition.Key}");
            }

            string assetKey = Path.GetFileNameWithoutExtension(descriptor.AssetPath);
            if (!string.Equals(assetKey, definition.Key, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"UI audio key casing must match the catalog: {assetKey} != {definition.Key}");
            }

            entries.Add(descriptor.AssetPath, definition.Address);
        }

        return entries;
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
