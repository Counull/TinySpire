using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

public sealed class CardIllustrationConfigurationTests
{
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";

    /// <summary>确认生成表只保存文件短键，并能在专用牌面目录中解析为单 Sprite 主资源。</summary>
    [Test]
    public void GeneratedCardTable_UsesFilenameStemIllustrationKeys()
    {
        var expectedKeys = new Dictionary<int, string>
        {
            [3001] = "card_art_strength",
            [3002] = "card_art_strike",
            [3003] = "card_art_defend",
            [3004] = "card_art_bash"
        };
        JObject cards = JObject.Parse(File.ReadAllText(CardTableJsonPath));
        foreach (KeyValuePair<int, string> expected in expectedKeys)
        {
            JToken card = cards[expected.Key.ToString()];
            Assert.That(card, Is.Not.Null, $"Missing card template {expected.Key}.");

            string key = (string)card["illustration_key"];
            Assert.That(key, Is.EqualTo(expected.Value));
            Assert.That(key, Does.Not.Contain("/"));
            Assert.That(Path.HasExtension(key), Is.False);

            string assetPath = $"Assets/Arts/Runtime/Card/Illustrations/{key}.png";
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(assetPath), Is.Not.Null);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.mipmapEnabled, Is.False);
        }
    }

    /// <summary>确认短键只生成逻辑地址，并拒绝重新把目录或扩展名写回配置。</summary>
    [Test]
    public void CardIllustrationAddress_UsesLogicalPrefixAndRejectsPaths()
    {
        Assert.That(
            CardIllustrationAddress.FromKey("card_art_strike"),
            Is.EqualTo("card-art/card_art_strike"));
        Assert.Throws<System.ArgumentException>(() => CardIllustrationAddress.FromKey(string.Empty));
        Assert.Throws<System.ArgumentException>(() => CardIllustrationAddress.FromKey("folder/card_art_strike"));
        Assert.Throws<System.ArgumentException>(() => CardIllustrationAddress.FromKey("card_art_strike.png"));
    }

    /// <summary>确认实际 Card Art 资源组只暴露逻辑地址，且没有残留完整 Assets 路径。</summary>
    [Test]
    public void CardArtAddressableGroup_UsesLogicalAddresses()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        Assert.That(settings, Is.Not.Null);

        AddressableAssetGroup group = settings.FindGroup("TinySpire Card Art");
        Assert.That(group, Is.Not.Null);
        Assert.That(group.entries.Count, Is.EqualTo(4));

        foreach (AddressableAssetEntry entry in group.entries)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
            string key = Path.GetFileNameWithoutExtension(assetPath);
            Assert.That(assetPath, Does.StartWith("Assets/Arts/Runtime/Card/Illustrations/"));
            Assert.That(entry.address, Is.EqualTo(CardIllustrationAddress.FromKey(key)));
        }
    }

    /// <summary>确认运行时 Addressables API 能用逻辑地址取得四张牌面 Sprite。</summary>
    [UnityTest]
    public IEnumerator CardArtLogicalAddresses_LoadSprites()
    {
        string[] keys =
        {
            "card_art_strength",
            "card_art_strike",
            "card_art_defend",
            "card_art_bash"
        };

        foreach (string key in keys)
        {
            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(
                CardIllustrationAddress.FromKey(key));
            yield return handle;
            try
            {
                Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                Assert.That(handle.Result, Is.Not.Null);
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }
    }
}
