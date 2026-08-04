using NUnit.Framework;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleTargetingPrefabContractTests
{
    private const string ArrowPrefabPath =
        "Assets/Prefabs/UI/Battle/Targeting/BattleTargetingArrow.prefab";
    private const string HandPrefabPath =
        "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab";
    private const string HudPrefabPath =
        "Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab";
    private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
    private const string ArrowBodySpritePath =
        "Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_arrow_body.png";
    private const string ArrowHeadSpritePath =
        "Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_arrow_head.png";
    private const string LegalHighlightSpritePath =
        "Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_legal_highlight.png";
    private const string HoverHighlightSpritePath =
        "Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_hover_highlight.png";

    /// <summary>验证瞄准箭头 Prefab 默认隐藏，且所有图形均不截获手牌拖拽事件。</summary>
    [Test]
    public void TargetingArrowPrefab_IsHiddenAndNonRaycastByDefault()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        BattleTargetingArrowView arrow = prefab.GetComponent<BattleTargetingArrowView>();
        Assert.That(arrow, Is.Not.Null);

        var serializedArrow = new SerializedObject(arrow);
        var visualRoot =
            serializedArrow.FindProperty("_visualRoot").objectReferenceValue as GameObject;
        Assert.That(visualRoot, Is.Not.Null);
        Assert.That(visualRoot.activeSelf, Is.False);

        Image[] graphics = prefab.GetComponentsInChildren<Image>(includeInactive: true);
        Assert.That(graphics, Has.Length.EqualTo(2));
        Assert.That(graphics, Has.All.Property(nameof(Graphic.raycastTarget)).False);
    }

    /// <summary>验证箭身与箭头 Image 精确引用 Runtime/Targeting 的正式 Sprite 子资源。</summary>
    [Test]
    public void TargetingArrowPrefab_UsesOfficialBodyAndHeadSprites()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        BattleTargetingArrowView arrow = prefab.GetComponent<BattleTargetingArrowView>();
        var serializedArrow = new SerializedObject(arrow);
        Image body = serializedArrow.FindProperty("_lineImage").objectReferenceValue as Image;
        Image head = serializedArrow.FindProperty("_headImage").objectReferenceValue as Image;

        Assert.That(body, Is.Not.Null);
        Assert.That(head, Is.Not.Null);
        Assert.That(body.sprite, Is.Not.Null);
        Assert.That(head.sprite, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(body.sprite),
            Is.EqualTo("Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_arrow_body.png"));
        Assert.That(body.sprite.name, Is.EqualTo("ui_battle_target_arrow_body_0"));
        Assert.That(
            AssetDatabase.GetAssetPath(head.sprite),
            Is.EqualTo("Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_arrow_head.png"));
        Assert.That(head.sprite.name, Is.EqualTo("ui_battle_target_arrow_head_0"));
        Assert.That(body.color, Is.EqualTo(Color.white));
        Assert.That(head.color, Is.EqualTo(Color.white));
        Assert.That(body.preserveAspect, Is.False);
        Assert.That(head.preserveAspect, Is.True);
        Assert.That(body.raycastTarget, Is.False);
        Assert.That(head.raycastTarget, Is.False);

        string[] dependencies = AssetDatabase.GetDependencies(ArrowPrefabPath, recursive: true);
        Assert.That(
            dependencies,
            Does.Contain("Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_arrow_body.png"));
        Assert.That(
            dependencies,
            Does.Contain("Assets/Arts/Runtime/UI/Battle/Targeting/ui_battle_target_arrow_head.png"));
    }

    /// <summary>验证 BattleHandUI 通过序列化 Prefab 实例持有唯一瞄准箭头，不依赖运行时查找。</summary>
    [Test]
    public void BattleHandPrefab_HoldsSerializedTargetingArrowInstance()
    {
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
        Assert.That(handPrefab, Is.Not.Null);
        Assert.That(
            handPrefab.transform.localScale,
            Is.EqualTo(Vector3.one),
            "嵌套箭头必须继承可见的非零根缩放。");
        HandCardContainer container = handPrefab.GetComponent<HandCardContainer>();
        Assert.That(container, Is.Not.Null);

        var serializedContainer = new SerializedObject(container);
        var arrow =
            serializedContainer.FindProperty("_targetingArrow").objectReferenceValue
                as BattleTargetingArrowView;
        Assert.That(arrow, Is.Not.Null);
        Assert.That(arrow.transform.IsChildOf(handPrefab.transform), Is.True);

        BattleTargetingArrowView source = PrefabUtility.GetCorrespondingObjectFromSource(arrow);
        Assert.That(source, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(ArrowPrefabPath));
    }

    /// <summary>验证手牌 Prefab 静态持有唯一 Enemy 聚焦锚点与轻量补间参数。</summary>
    [Test]
    public void BattleHandPrefab_HasSerializedEnemyTargetFocusAnchorAndSettings()
    {
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
        Assert.That(handPrefab, Is.Not.Null);
        HandCardContainer container = handPrefab.GetComponent<HandCardContainer>();
        Assert.That(container, Is.Not.Null);
        var serializedContainer = new SerializedObject(container);

        SerializedProperty anchorProperty = serializedContainer.FindProperty("_targetFocusAnchor");
        SerializedProperty durationProperty = serializedContainer.FindProperty("_targetFocusDuration");
        SerializedProperty easeProperty = serializedContainer.FindProperty("_targetFocusEase");
        SerializedProperty scaleProperty = serializedContainer.FindProperty("_targetFocusScale");
        SerializedProperty breathScaleProperty = serializedContainer.FindProperty("_targetFocusBreathScale");
        SerializedProperty breathDurationProperty = serializedContainer.FindProperty("_targetFocusBreathDuration");

        Assert.That(anchorProperty, Is.Not.Null);
        Assert.That(durationProperty, Is.Not.Null);
        Assert.That(easeProperty, Is.Not.Null);
        Assert.That(scaleProperty, Is.Not.Null);
        Assert.That(breathScaleProperty, Is.Not.Null);
        Assert.That(breathDurationProperty, Is.Not.Null);

        var anchor = anchorProperty.objectReferenceValue as RectTransform;
        Assert.That(anchor, Is.Not.Null);
        Assert.That(anchor.name, Is.EqualTo("TargetFocusAnchor"));
        Assert.That(anchor.parent, Is.EqualTo(handPrefab.transform));
        Assert.That(anchor.sizeDelta, Is.EqualTo(Vector2.zero));
        Assert.That(anchor.GetComponentsInChildren<Graphic>(includeInactive: true), Is.Empty);
        Assert.That(durationProperty.floatValue, Is.GreaterThan(0f));
        Assert.That(scaleProperty.floatValue, Is.InRange(1.01f, 1.2f));
        Assert.That(breathScaleProperty.floatValue, Is.InRange(1.001f, 1.08f));
        Assert.That(breathDurationProperty.floatValue, Is.GreaterThan(0f));
    }

    /// <summary>验证既有 BattleScene 依赖链静态包含手牌、箭头与普通 HUD，无需新增独立资源地址。</summary>
    [Test]
    public void BattleSceneDependencies_IncludeHandArrowHudAndFourOfficialTargetingSprites()
    {
        string[] dependencies = AssetDatabase.GetDependencies(BattleScenePath, recursive: true);

        Assert.That(dependencies, Does.Contain(HandPrefabPath));
        Assert.That(dependencies, Does.Contain(ArrowPrefabPath));
        Assert.That(dependencies, Does.Contain(HudPrefabPath));
        Assert.That(dependencies, Does.Contain(ArrowBodySpritePath));
        Assert.That(dependencies, Does.Contain(ArrowHeadSpritePath));
        Assert.That(dependencies, Does.Contain(LegalHighlightSpritePath));
        Assert.That(dependencies, Does.Contain(HoverHighlightSpritePath));
        Assert.That(
            dependencies,
            Has.None.Contains("/Candidates/"),
            "BattleScene 依赖不得接入受保护的 Candidates 资源。");
    }
}
