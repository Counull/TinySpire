using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CardIllustrationConfigurationTests
{
    private const string CardTableJsonPath = "Assets/GameData/battle_tbcard.json";

    /// <summary>确认生成表中的四张牌面使用完整稳定地址，并能作为单 Sprite 主资源加载。</summary>
    [Test]
    public void GeneratedCardTable_UsesStableSingleSpriteIllustrationAddresses()
    {
        var expectedAddresses = new Dictionary<int, string>
        {
            [3001] = "Assets/Arts/Runtime/Card/card_art_strength.png",
            [3002] = "Assets/Arts/Runtime/Card/card_art_strike.png",
            [3003] = "Assets/Arts/Runtime/Card/card_art_defend.png",
            [3004] = "Assets/Arts/Runtime/Card/card_art_bash.png"
        };
        JObject cards = JObject.Parse(File.ReadAllText(CardTableJsonPath));

        foreach (KeyValuePair<int, string> expected in expectedAddresses)
        {
            JToken card = cards[expected.Key.ToString()];
            Assert.That(card, Is.Not.Null, $"Missing card template {expected.Key}.");

            string address = (string)card["illustration_address"];
            Assert.That(address, Is.EqualTo(expected.Value));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(address), Is.Not.Null);

            var importer = AssetImporter.GetAtPath(address) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.mipmapEnabled, Is.False);
        }
    }
}
