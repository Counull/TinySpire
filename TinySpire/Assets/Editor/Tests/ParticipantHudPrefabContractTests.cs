using System;
using System.Linq;
using NUnit.Framework;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ParticipantHudPrefabContractTests
{
    private const string PrefabPath = "Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab";

    /// <summary>验证 Prefab 静态持有意图层级、展示组件与五张正式 Sprite 子资源。</summary>
    [Test]
    public void ParticipantHudPrefab_HasStaticIntentHierarchyAndOfficialSprites()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        ParticipantHudView view = prefab.GetComponent<ParticipantHudView>();
        Assert.That(view, Is.Not.Null);
        var serializedView = new SerializedObject(view);

        var intentRoot = serializedView.FindProperty("_intentRoot").objectReferenceValue as GameObject;
        var intentIcon = serializedView.FindProperty("_intentIcon").objectReferenceValue as Image;
        var intentValueText = serializedView.FindProperty("_intentValueText").objectReferenceValue as Text;
        Assert.That(intentRoot, Is.Not.Null);
        Assert.That(intentIcon, Is.Not.Null);
        Assert.That(intentValueText, Is.Not.Null);
        Assert.That(intentRoot.name, Is.EqualTo("IntentRoot"));
        Assert.That(intentRoot.transform.parent.name, Is.EqualTo("NameAnchor"));
        Assert.That(intentIcon.name, Is.EqualTo("IntentIcon"));
        Assert.That(intentValueText.name, Is.EqualTo("IntentValueText"));
        Assert.That(intentRoot.activeSelf, Is.False);
        Assert.That(intentIcon.preserveAspect, Is.True);

        AssertOfficialSprite(
            serializedView,
            "_attackIntentSprite",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_intent_attack.png");
        AssertOfficialSprite(
            serializedView,
            "_defendIntentSprite",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_intent_defend.png");
        AssertOfficialSprite(
            serializedView,
            "_buffIntentSprite",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_intent_buff.png");
        AssertOfficialSprite(
            serializedView,
            "_debuffIntentSprite",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_intent_debuff.png");
        AssertOfficialSprite(
            serializedView,
            "_specialIntentSprite",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_intent_special.png");
    }

    /// <summary>验证意图行位于名称上方，两个静态矩形之间保留明确间距。</summary>
    [Test]
    public void ParticipantHudPrefab_IntentRowDoesNotOverlapNameText()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        RectTransform nameAnchor = prefab.transform.Find("NameAnchor") as RectTransform;
        RectTransform nameText = nameAnchor.Find("NameText") as RectTransform;
        RectTransform intentRoot = nameAnchor.Find("IntentRoot") as RectTransform;

        Assert.That(nameText, Is.Not.Null);
        Assert.That(intentRoot, Is.Not.Null);
        float nameTop = nameText.anchoredPosition.y + nameText.sizeDelta.y * 0.5f;
        float intentBottom = intentRoot.anchoredPosition.y - intentRoot.sizeDelta.y * 0.5f;
        Assert.That(intentBottom, Is.GreaterThan(nameTop));
    }

    /// <summary>验证序列化 Sprite 来自精确正式路径，并保持单子图、无 mipmap 的当前导入契约。</summary>
    private static void AssertOfficialSprite(
        SerializedObject serializedView,
        string propertyName,
        string expectedPath)
    {
        var sprite = serializedView.FindProperty(propertyName).objectReferenceValue as Sprite;
        Assert.That(sprite, Is.Not.Null, propertyName);
        string actualPath = AssetDatabase.GetAssetPath(sprite);
        Assert.That(actualPath, Is.EqualTo(expectedPath));
        Assert.That(actualPath, Does.Not.Contain("_ref_"));

        var importer = AssetImporter.GetAtPath(actualPath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        Assert.That(
            AssetDatabase.LoadAllAssetsAtPath(actualPath).OfType<Sprite>().Count(),
            Is.EqualTo(1));
    }
}
