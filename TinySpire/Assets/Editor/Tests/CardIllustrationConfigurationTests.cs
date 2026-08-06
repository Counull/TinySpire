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

    /// <summary>确认生成表只保存文件短键，并能解析正式牌面与明确占位图的单 Sprite 主资源。</summary>
    [Test]
    public void GeneratedCardTable_UsesFilenameStemIllustrationKeys()
    {
        var expectedKeys = new Dictionary<int, string>
        {
            [3001] = "card_art_strength",
            [3002] = "card_art_strike",
            [3003] = "card_art_defend",
            [3004] = "card_art_bash",
            [3101] = "art_placeholder"
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

            string assetPath = key == "art_placeholder"
                ? "Assets/Arts/Runtime/Card/Texture/art_placeholder.png"
                : $"Assets/Arts/Runtime/Card/Illustrations/{key}.png";
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
        var expectedPaths = new HashSet<string>
        {
            "Assets/Arts/Runtime/Card/Illustrations/card_art_strength.png",
            "Assets/Arts/Runtime/Card/Illustrations/card_art_strike.png",
            "Assets/Arts/Runtime/Card/Illustrations/card_art_defend.png",
            "Assets/Arts/Runtime/Card/Illustrations/card_art_bash.png",
            "Assets/Arts/Runtime/Card/Texture/art_placeholder.png"
        };
        Assert.That(group.entries.Count, Is.EqualTo(expectedPaths.Count));

        foreach (AddressableAssetEntry entry in group.entries)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
            string key = Path.GetFileNameWithoutExtension(assetPath);
            Assert.That(expectedPaths.Remove(assetPath), Is.True, $"Unexpected card art entry: {assetPath}");
            Assert.That(entry.address, Is.EqualTo(CardIllustrationAddress.FromKey(key)));
        }
        Assert.That(expectedPaths, Is.Empty);
    }

    /// <summary>确认生成卡表引用不存在的牌面短键时会报告精确的卡牌身份与短键。</summary>
    [Test]
    public void CardIllustrationEntries_RejectMissingAssetKey()
    {
        const int cardId = 9109;
        const string missingKey = "missing_card_art";
        var pathsByKey = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["card_art_strike"] = "Assets/Arts/Runtime/Card/Illustrations/card_art_strike.png"
        };
        var cards = new JObject
        {
            [cardId.ToString()] = new JObject
            {
                ["id"] = cardId,
                ["illustration_key"] = missingKey
            }
        };

        System.InvalidOperationException failure = Assert.Throws<System.InvalidOperationException>(
            () => AddressablesBuildTools.ResolveCardIllustrationEntries(pathsByKey, cards));

        Assert.That(
            failure.Message,
            Is.EqualTo($"Card template {cardId} illustration key does not exist: {missingKey}"));
    }

    /// <summary>确认运行时 Addressables API 能用逻辑地址取得四张正式牌面与一张占位 Sprite。</summary>
    [UnityTest]
    public IEnumerator CardArtLogicalAddresses_LoadSprites()
    {
        string[] keys =
        {
            "card_art_strength",
            "card_art_strike",
            "card_art_defend",
            "card_art_bash",
            "art_placeholder"
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
