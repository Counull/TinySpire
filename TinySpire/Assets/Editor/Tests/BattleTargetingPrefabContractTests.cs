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

    /// <summary>验证既有 BattleScene 依赖链静态包含手牌、箭头与普通 HUD，无需新增独立资源地址。</summary>
    [Test]
    public void BattleSceneDependencies_IncludeHandArrowAndParticipantHud()
    {
        string[] dependencies = AssetDatabase.GetDependencies(BattleScenePath, recursive: true);

        Assert.That(dependencies, Does.Contain(HandPrefabPath));
        Assert.That(dependencies, Does.Contain(ArrowPrefabPath));
        Assert.That(dependencies, Does.Contain(HudPrefabPath));
    }
}
