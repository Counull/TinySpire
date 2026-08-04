using System;
using System.Linq;
using NUnit.Framework;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ParticipantHudPrefabContractTests
{
    private const string PrefabPath = "Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab";
    private const string FloatingNumberPrefabPath =
        "Assets/Prefabs/UI/Battle/BattleFloatingNumberView.prefab";

    /// <summary>验证唯一飘字 Prefab 是默认隐藏、无美术依赖且完全非交互的纯 UGUI Text。</summary>
    [Test]
    public void BattleFloatingNumberPrefab_IsSinglePureTextNonInteractiveAndHasNoArtDependency()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloatingNumberPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.activeSelf, Is.False);
        Assert.That(prefab.GetComponent<BattleFloatingNumberView>(), Is.Not.Null);
        RectTransform rectTransform = prefab.GetComponent<RectTransform>();
        Text text = prefab.GetComponent<Text>();
        CanvasGroup canvasGroup = prefab.GetComponent<CanvasGroup>();

        Assert.That(rectTransform, Is.Not.Null);
        Assert.That(rectTransform.sizeDelta, Is.EqualTo(new Vector2(160f, 64f)));
        Assert.That(text, Is.Not.Null);
        Assert.That(text.text, Is.Empty);
        Assert.That(text.font, Is.Not.Null);
        Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Bold));
        Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(text.resizeTextForBestFit, Is.True);
        Assert.That(text.resizeTextMinSize, Is.LessThanOrEqualTo(18));
        Assert.That(text.resizeTextMaxSize, Is.GreaterThanOrEqualTo(40));
        Assert.That(text.raycastTarget, Is.False);
        Assert.That(canvasGroup, Is.Not.Null);
        Assert.That(canvasGroup.alpha, Is.Zero);
        Assert.That(canvasGroup.interactable, Is.False);
        Assert.That(canvasGroup.blocksRaycasts, Is.False);
        Assert.That(prefab.GetComponentsInChildren<Graphic>(includeInactive: true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentInChildren<Image>(includeInactive: true), Is.Null);
        Assert.That(prefab.GetComponentInChildren<RawImage>(includeInactive: true), Is.Null);
        Assert.That(prefab.GetComponentInChildren<Selectable>(includeInactive: true), Is.Null);
        Assert.That(prefab.GetComponentInChildren<EventTrigger>(includeInactive: true), Is.Null);
        Assert.That(prefab.GetComponentInChildren<GraphicRaycaster>(includeInactive: true), Is.Null);
        Assert.That(prefab.GetComponentInChildren<Canvas>(includeInactive: true), Is.Null);
        Assert.That(prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: true), Is.Null);
        Assert.That(prefab.GetComponentInChildren<Animator>(includeInactive: true), Is.Null);

        string[] dependencies = AssetDatabase.GetDependencies(FloatingNumberPrefabPath, recursive: true);
        Assert.That(
            dependencies.Any(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)),
            Is.False);
        Assert.That(
            dependencies.Any(path => path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)),
            Is.False);
    }

    /// <summary>验证 Participant HUD 只持有一个反馈锚点和一个纯字符飘字 Prefab 引用。</summary>
    [Test]
    public void ParticipantHudPrefab_ReferencesSingleFloatingNumberPrefabAndFeedbackAnchor()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        ParticipantHudView view = prefab.GetComponent<ParticipantHudView>();
        var serializedView = new SerializedObject(view);
        SerializedProperty feedbackAnchorProperty = serializedView.FindProperty("_feedbackAnchor");
        SerializedProperty floatingPrefabProperty = serializedView.FindProperty("_floatingNumberPrefab");

        Assert.That(feedbackAnchorProperty, Is.Not.Null);
        Assert.That(floatingPrefabProperty, Is.Not.Null);
        var feedbackAnchor = feedbackAnchorProperty.objectReferenceValue as RectTransform;
        var floatingPrefab = floatingPrefabProperty.objectReferenceValue as BattleFloatingNumberView;
        Assert.That(feedbackAnchor, Is.Not.Null);
        Assert.That(feedbackAnchor.name, Is.EqualTo("FeedbackAnchor"));
        Assert.That(feedbackAnchor.parent, Is.EqualTo(prefab.transform));
        Assert.That(feedbackAnchor.GetSiblingIndex(), Is.EqualTo(prefab.transform.childCount - 1));
        Assert.That(feedbackAnchor.sizeDelta, Is.EqualTo(Vector2.zero));
        Assert.That(feedbackAnchor.GetComponentsInChildren<Graphic>(includeInactive: true), Is.Empty);
        Assert.That(floatingPrefab, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(floatingPrefab),
            Is.EqualTo(FloatingNumberPrefabPath));
        Assert.That(
            AssetDatabase.GetDependencies(PrefabPath, recursive: true),
            Does.Contain(FloatingNumberPrefabPath));
    }

    /// <summary>验证既有 BattleScene 经 Participant HUD 递归包含唯一飘字 Prefab，无需 Scene 接线。</summary>
    [Test]
    public void BattleSceneDependencies_IncludeParticipantHudFloatingNumberPrefab()
    {
        string[] dependencies = AssetDatabase.GetDependencies(
            "Assets/Scenes/BattleScene.unity",
            recursive: true);

        Assert.That(dependencies, Does.Contain(PrefabPath));
        Assert.That(dependencies, Does.Contain(FloatingNumberPrefabPath));
    }

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

    /// <summary>验证目标候选高亮使用四个独立角件围住角色，合法与悬停互斥切换且全部非交互。</summary>
    [Test]
    public void ParticipantHudPrefab_TargetHighlightUsesOfficialFourCornersAndSwitchesWithoutRaycast()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        ParticipantHudView view = prefab.GetComponent<ParticipantHudView>();
        var serializedView = new SerializedObject(view);

        var highlightAnchor =
            serializedView.FindProperty("_targetHighlightAnchor").objectReferenceValue as RectTransform;
        SerializedProperty legalRootProperty = serializedView.FindProperty("_legalTargetHighlightRoot");
        SerializedProperty legalCornersProperty =
            serializedView.FindProperty("_legalTargetHighlightCornerImages");
        SerializedProperty hoveredRootProperty = serializedView.FindProperty("_hoveredTargetHighlightRoot");
        SerializedProperty hoveredCornersProperty =
            serializedView.FindProperty("_hoveredTargetHighlightCornerImages");

        Assert.That(highlightAnchor, Is.Not.Null);
        Assert.That(legalRootProperty, Is.Not.Null);
        Assert.That(legalCornersProperty, Is.Not.Null);
        Assert.That(hoveredRootProperty, Is.Not.Null);
        Assert.That(hoveredCornersProperty, Is.Not.Null);

        var legalRoot = legalRootProperty.objectReferenceValue as GameObject;
        var hoveredRoot = hoveredRootProperty.objectReferenceValue as GameObject;
        Image[] legalCorners = GetCornerImages(legalCornersProperty);
        Image[] hoveredCorners = GetCornerImages(hoveredCornersProperty);
        Assert.That(highlightAnchor.name, Is.EqualTo("TargetHighlightAnchor"));
        Assert.That(legalRoot, Is.Not.Null);
        Assert.That(hoveredRoot, Is.Not.Null);
        Assert.That(legalRoot.transform.parent, Is.EqualTo(highlightAnchor));
        Assert.That(hoveredRoot.transform.parent, Is.EqualTo(highlightAnchor));
        Assert.That(legalRoot.activeSelf, Is.False);
        Assert.That(hoveredRoot.activeSelf, Is.False);
        Assert.That(highlightAnchor.GetComponentsInChildren<Image>(includeInactive: true), Has.Length.EqualTo(8));
        Assert.That(highlightAnchor.Find("TargetHighlightVisual"), Is.Null);
        Assert.That(legalRoot.layer, Is.EqualTo(highlightAnchor.gameObject.layer));
        Assert.That(hoveredRoot.layer, Is.EqualTo(highlightAnchor.gameObject.layer));
        AssertTargetHighlightCorners(
            legalCorners,
            highlightAnchor.gameObject.layer,
            "Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_legal_highlight.png",
            "ui_battle_target_legal_highlight");
        AssertTargetHighlightCorners(
            hoveredCorners,
            highlightAnchor.gameObject.layer,
            "Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_hover_highlight.png",
            "ui_battle_target_hover_highlight");

        view.SetTargetHighlight(isLegalCandidate: true, isHovered: false);
        Assert.That(legalRoot.activeSelf, Is.True);
        Assert.That(hoveredRoot.activeSelf, Is.False);
        view.SetTargetHighlight(isLegalCandidate: true, isHovered: true);
        Assert.That(legalRoot.activeSelf, Is.False);
        Assert.That(hoveredRoot.activeSelf, Is.True);
        view.SetTargetHighlight(isLegalCandidate: false, isHovered: false);
        Assert.That(legalRoot.activeSelf, Is.False);
        Assert.That(hoveredRoot.activeSelf, Is.False);

        string[] dependencies = AssetDatabase.GetDependencies(PrefabPath, recursive: true);
        Assert.That(
            dependencies,
            Does.Contain("Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_legal_highlight.png"));
        Assert.That(
            dependencies,
            Does.Contain("Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_hover_highlight.png"));
        Assert.That(dependencies.Any(path => path.Contains("/Candidates/")), Is.False);
    }

    /// <summary>验证 Block、Strength、Vulnerable 状态行使用正式图标、默认隐藏且不截获指针。</summary>
    [Test]
    public void ParticipantHudPrefab_HasStaticStatusHierarchyAndOfficialSprites()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        ParticipantHudView view = prefab.GetComponent<ParticipantHudView>();
        Assert.That(view, Is.Not.Null);
        var serializedView = new SerializedObject(view);
        RectTransform vitalsAnchor = prefab.transform.Find("VitalsAnchor") as RectTransform;
        RectTransform healthBar = vitalsAnchor.Find("HealthBar") as RectTransform;
        RectTransform healthText = vitalsAnchor.Find("HealthText") as RectTransform;
        RectTransform statusRow = vitalsAnchor.Find("StatusRow") as RectTransform;

        Assert.That(vitalsAnchor, Is.Not.Null);
        Assert.That(healthBar, Is.Not.Null);
        Assert.That(healthText, Is.Not.Null);
        Assert.That(statusRow, Is.Not.Null);
        Assert.That(statusRow.gameObject.activeSelf, Is.False);
        Assert.That(
            serializedView.FindProperty("_statusRoot").objectReferenceValue,
            Is.EqualTo(statusRow.gameObject));
        Assert.That(healthBar.parent, Is.EqualTo(vitalsAnchor));
        Assert.That(healthText.parent, Is.EqualTo(vitalsAnchor));
        Assert.That(statusRow.parent, Is.EqualTo(vitalsAnchor));
        Assert.That(statusRow.Find("Weak"), Is.Null);
        Assert.That(statusRow.Find("Poison"), Is.Null);

        AssertStatusItem(
            serializedView,
            statusRow,
            "Block",
            "_blockRoot",
            "_blockText",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_icon_block.png");
        AssertStatusItem(
            serializedView,
            statusRow,
            "Strength",
            "_strengthRoot",
            "_strengthText",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_icon_strength.png");
        AssertStatusItem(
            serializedView,
            statusRow,
            "Vulnerable",
            "_vulnerableRoot",
            "_vulnerableText",
            "Assets/Arts/Runtime/UI/Battle/ui_battle_icon_vulnerable.png");

        float healthBottom = healthBar.anchoredPosition.y - healthBar.sizeDelta.y * 0.5f;
        float statusTop = statusRow.anchoredPosition.y + statusRow.sizeDelta.y * 0.5f;
        Assert.That(statusTop, Is.LessThan(healthBottom));
    }

    /// <summary>验证 BattleScene 通过 Participant HUD 递归依赖三张正式状态图标，无需新增地址或 Scene 接线。</summary>
    [Test]
    public void BattleSceneDependencies_IncludeParticipantHudStatusSprites()
    {
        string[] dependencies = AssetDatabase.GetDependencies(
            "Assets/Scenes/BattleScene.unity",
            recursive: true);

        Assert.That(dependencies, Does.Contain(PrefabPath));
        Assert.That(
            dependencies,
            Does.Contain("Assets/Arts/Runtime/UI/Battle/ui_battle_icon_block.png"));
        Assert.That(
            dependencies,
            Does.Contain("Assets/Arts/Runtime/UI/Battle/ui_battle_icon_strength.png"));
        Assert.That(
            dependencies,
            Does.Contain("Assets/Arts/Runtime/UI/Battle/ui_battle_icon_vulnerable.png"));
    }

    /// <summary>验证单个状态项的序列化引用、正式 Sprite 与非交互默认状态。</summary>
    private static void AssertStatusItem(
        SerializedObject serializedView,
        RectTransform statusRow,
        string itemName,
        string rootPropertyName,
        string textPropertyName,
        string expectedSpritePath)
    {
        RectTransform root = statusRow.Find(itemName) as RectTransform;
        Assert.That(root, Is.Not.Null, itemName);
        Assert.That(root.gameObject.activeSelf, Is.False, itemName);
        Assert.That(
            serializedView.FindProperty(rootPropertyName).objectReferenceValue,
            Is.EqualTo(root.gameObject),
            rootPropertyName);

        Image icon = root.Find($"{itemName}Icon")?.GetComponent<Image>();
        Text text = root.Find($"{itemName}Text")?.GetComponent<Text>();
        Assert.That(icon, Is.Not.Null, $"{itemName}Icon");
        Assert.That(text, Is.Not.Null, $"{itemName}Text");
        Assert.That(
            serializedView.FindProperty(textPropertyName).objectReferenceValue,
            Is.EqualTo(text),
            textPropertyName);
        Assert.That(icon.raycastTarget, Is.False);
        Assert.That(icon.preserveAspect, Is.True);
        Assert.That(text.raycastTarget, Is.False);
        Assert.That(text.rectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(31f));
        Assert.That(text.resizeTextForBestFit, Is.True);
        Assert.That(text.resizeTextMinSize, Is.LessThanOrEqualTo(12));
        Assert.That(text.resizeTextMaxSize, Is.GreaterThanOrEqualTo(18));
        AssertOfficialImageSprite(icon, expectedSpritePath);
    }

    /// <summary>验证状态 Image 的正式 Sprite 路径及单子图导入契约。</summary>
    private static void AssertOfficialImageSprite(Image image, string expectedPath)
    {
        Assert.That(image.sprite, Is.Not.Null);
        string actualPath = AssetDatabase.GetAssetPath(image.sprite);
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

    /// <summary>从序列化数组读取四个锁定框角件，避免测试依赖运行时查找。</summary>
    private static Image[] GetCornerImages(SerializedProperty cornerImagesProperty)
    {
        Assert.That(cornerImagesProperty.isArray, Is.True);
        Assert.That(cornerImagesProperty.arraySize, Is.EqualTo(4));
        var images = new Image[cornerImagesProperty.arraySize];
        for (int index = 0; index < images.Length; index++)
        {
            images[index] = cornerImagesProperty.GetArrayElementAtIndex(index)
                .objectReferenceValue as Image;
        }

        return images;
    }

    /// <summary>验证四角分别锚定于外框四角，并以裁切后的正式左右半图构成独立角件。</summary>
    private static void AssertTargetHighlightCorners(
        Image[] cornerImages,
        int expectedLayer,
        string expectedPath,
        string expectedSpritePrefix)
    {
        Assert.That(cornerImages, Has.Length.EqualTo(4));
        for (int index = 0; index < cornerImages.Length; index++)
        {
            Image image = cornerImages[index];
            Assert.That(image, Is.Not.Null, $"corner {index}");
            Assert.That(image.gameObject.layer, Is.EqualTo(expectedLayer));
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(expectedPath));
            Assert.That(image.sprite.name, Is.EqualTo($"{expectedSpritePrefix}_{index % 2}"));
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(image.fillMethod, Is.EqualTo(Image.FillMethod.Vertical));
            Assert.That(image.fillAmount, Is.EqualTo(0.5f).Within(0.01f));
        }

        Assert.That(cornerImages[0].rectTransform.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(cornerImages[1].rectTransform.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
        Assert.That(cornerImages[2].rectTransform.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
        Assert.That(cornerImages[3].rectTransform.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
        Assert.That(cornerImages[0].rectTransform.pivot, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(cornerImages[1].rectTransform.pivot, Is.EqualTo(new Vector2(1f, 1f)));
        Assert.That(cornerImages[2].rectTransform.pivot, Is.EqualTo(new Vector2(0f, 0f)));
        Assert.That(cornerImages[3].rectTransform.pivot, Is.EqualTo(new Vector2(1f, 0f)));
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
