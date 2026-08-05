using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

public sealed class CharacterViewConfigurationTests
{
    private const string HeroTableJsonPath = "Assets/GameData/battle_tbhero.json";
    private const string EnemyTableJsonPath = "Assets/GameData/battle_tbenemy.json";
    private const string CharacterPrefabRoot = "Assets/Arts/Runtime/Character/Prefabs";

    /// <summary>确认角色视图短键只生成逻辑地址，并拒绝目录、扩展名和首尾空白。</summary>
    [Test]
    public void CharacterViewAddress_UsesLogicalPrefixAndRejectsPaths()
    {
        Assert.That(
            CharacterViewAddress.FromKey("pfb_char_player"),
            Is.EqualTo("character-view/pfb_char_player"));
        Assert.Throws<System.ArgumentException>(() => CharacterViewAddress.FromKey(string.Empty));
        Assert.Throws<System.ArgumentException>(() => CharacterViewAddress.FromKey("Prefabs/pfb_char_player"));
        Assert.Throws<System.ArgumentException>(() => CharacterViewAddress.FromKey("pfb_char_player.prefab"));
        Assert.Throws<System.ArgumentException>(() => CharacterViewAddress.FromKey(" pfb_char_player"));
    }

    /// <summary>确认生成的英雄与敌人表只保存文件短键，并能精确解析到角色 Prefab。</summary>
    [Test]
    public void GeneratedCharacterTables_UseFilenameStemViewPrefabKeys()
    {
        JObject heroes = JObject.Parse(File.ReadAllText(HeroTableJsonPath));
        JObject enemies = JObject.Parse(File.ReadAllText(EnemyTableJsonPath));
        var expectedKeys = new Dictionary<JToken, string>
        {
            [heroes["1001"]] = "pfb_char_player",
            [enemies["2001"]] = "pfb_char_enemy",
            [enemies["2002"]] = "pfb_char_enemy"
        };

        foreach (KeyValuePair<JToken, string> expected in expectedKeys)
        {
            Assert.That(expected.Key, Is.Not.Null);
            Assert.That(expected.Key["view_prefab_address"], Is.Null);

            string key = (string)expected.Key["view_prefab_key"];
            Assert.That(key, Is.EqualTo(expected.Value));
            Assert.That(key, Does.Not.Contain("/"));
            Assert.That(key, Does.Not.Contain("\\"));
            Assert.That(Path.HasExtension(key), Is.False);

            string assetPath = $"{CharacterPrefabRoot}/{key}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: false), Is.Not.Null);
        }
    }

    /// <summary>确认角色资源组只暴露逻辑地址，并维持本地整组打包契约。</summary>
    [Test]
    public void CharacterAddressableGroup_UsesLogicalAddresses()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        Assert.That(settings, Is.Not.Null);

        AddressableAssetGroup group = settings.FindGroup("TinySpire Characters");
        Assert.That(group, Is.Not.Null);
        Assert.That(group.entries.Count, Is.EqualTo(2));

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        Assert.That(schema, Is.Not.Null);
        Assert.That(schema.BundleMode, Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackTogether));
        Assert.That(schema.IncludeInBuild, Is.True);

        var actualEntries = new List<string>();
        foreach (AddressableAssetEntry entry in group.entries)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
            string key = Path.GetFileNameWithoutExtension(assetPath);
            Assert.That(assetPath, Does.StartWith(CharacterPrefabRoot + "/"));
            Assert.That(entry.address, Is.EqualTo(CharacterViewAddress.FromKey(key)));
            Assert.That(entry.address, Does.Not.StartWith("Assets/"));
            actualEntries.Add($"{assetPath}|{entry.address}");
        }
        Assert.That(
            actualEntries,
            Is.EquivalentTo(new[]
            {
                $"{CharacterPrefabRoot}/pfb_char_player.prefab|character-view/pfb_char_player",
                $"{CharacterPrefabRoot}/pfb_char_enemy.prefab|character-view/pfb_char_enemy"
            }));
    }

    /// <summary>确认运行时 Addressables API 能以角色逻辑地址实例化并释放两个 Prefab。</summary>
    [UnityTest]
    public IEnumerator CharacterLogicalAddresses_InstantiatePrefabs()
    {
        string[] keys = { "pfb_char_player", "pfb_char_enemy" };
        foreach (string key in keys)
        {
            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(
                CharacterViewAddress.FromKey(key));
            yield return handle;
            try
            {
                Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                Assert.That(handle.Result, Is.Not.Null);
                Assert.That(
                    handle.Result.GetComponentInChildren<SpriteRenderer>(includeInactive: false),
                    Is.Not.Null);
            }
            finally
            {
                if (handle.IsValid() && handle.Result != null)
                    Addressables.ReleaseInstance(handle.Result);
                else if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }
    }

    /// <summary>确认角色 Prefab 索引会拒绝仅大小写不同的重复短键。</summary>
    [Test]
    public void CharacterPrefabIndex_RejectsCaseInsensitiveDuplicateKeys()
    {
        Assert.Throws<System.InvalidOperationException>(() =>
            AddressablesBuildTools.IndexCharacterPrefabPaths(new[]
            {
                $"{CharacterPrefabRoot}/pfb_char_player.prefab",
                $"{CharacterPrefabRoot}/PFB_CHAR_PLAYER.prefab"
            }));
    }

    /// <summary>确认角色表解析会拒绝不存在的 Prefab 短键。</summary>
    [Test]
    public void CharacterViewEntries_RejectMissingPrefabKey()
    {
        var pathsByKey = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["pfb_char_player"] = $"{CharacterPrefabRoot}/pfb_char_player.prefab"
        };
        var table = new JObject
        {
            ["1001"] = new JObject { ["view_prefab_key"] = "missing_character" }
        };

        Assert.Throws<System.InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveCharacterViewEntries(pathsByKey, table));
    }

    /// <summary>确认角色表短键大小写必须与真实 Prefab 文件名精确一致。</summary>
    [Test]
    public void CharacterViewEntries_RejectFilenameCaseMismatch()
    {
        var pathsByKey = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["pfb_char_player"] = $"{CharacterPrefabRoot}/pfb_char_player.prefab"
        };
        var table = new JObject
        {
            ["1001"] = new JObject { ["view_prefab_key"] = "PFB_CHAR_PLAYER" }
        };

        Assert.Throws<System.InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveCharacterViewEntries(pathsByKey, table));
    }

    /// <summary>确认角色短键不能指向缺少 SpriteRenderer 的普通 Prefab。</summary>
    [Test]
    public void CharacterViewEntries_RejectPrefabWithoutSpriteRenderer()
    {
        const string key = "CardView";
        var pathsByKey = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            [key] = "Assets/Arts/Runtime/Card/Prefab/CardView.prefab"
        };
        var table = new JObject
        {
            ["1001"] = new JObject { ["view_prefab_key"] = key }
        };

        Assert.Throws<System.InvalidOperationException>(() =>
            AddressablesBuildTools.ResolveCharacterViewEntries(pathsByKey, table));
    }

    /// <summary>确认只有禁用 SpriteRenderer 的 Prefab 不能越过构建期角色可见性契约。</summary>
    [Test]
    public void CharacterViewPrefabContract_RejectsOnlyInactiveSpriteRenderer()
    {
        var prefab = new GameObject("CharacterRoot");
        var inactiveChild = new GameObject("InactiveRenderer");
        inactiveChild.transform.SetParent(prefab.transform, worldPositionStays: false);
        inactiveChild.AddComponent<SpriteRenderer>();
        inactiveChild.SetActive(false);
        try
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                AddressablesBuildTools.ValidateCharacterViewPrefabContract(
                    prefab,
                    "inactive_character",
                    "in-memory-character.prefab"));
        }
        finally
        {
            Object.DestroyImmediate(prefab);
        }
    }
}
