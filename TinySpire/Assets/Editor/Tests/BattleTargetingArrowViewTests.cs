using NUnit.Framework;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleTargetingArrowViewTests
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/Battle/Targeting/BattleTargetingArrow.prefab";

    /// <summary>验证箭头可从缩放父级提升为独立 Overlay 根，并保持默认隐藏。</summary>
    [Test]
    public void PrepareAsScreenOverlay_DetachesFromScaledParentAndStaysHidden()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var parent = new GameObject("ScaledHandCanvas", typeof(RectTransform));
        GameObject instance = null;
        try
        {
            parent.transform.localScale = Vector3.one * 0.01f;
            instance = Object.Instantiate(prefab, parent.transform);
            BattleTargetingArrowView arrow = instance.GetComponent<BattleTargetingArrowView>();

            arrow.PrepareAsScreenOverlay();

            Assert.That(instance.transform.parent, Is.Null);
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(instance.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(arrow.IsVisible, Is.False);
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
            Object.DestroyImmediate(parent);
        }
    }

    /// <summary>验证功能性箭头按弧线生成多段箭身，片段与箭头各自使用局部切线且全程不接收射线。</summary>
    [Test]
    public void Arrow_ShowUpdateHide_UsesCurvedTangentFragmentsAndRemainsNonRaycast()
    {
        var canvasObject = new GameObject("TargetingCanvas", typeof(RectTransform), typeof(Canvas));
        var arrowObject = new GameObject("TargetingArrow", typeof(RectTransform));
        var visualObject = new GameObject("VisualRoot", typeof(RectTransform));
        var fragmentTemplateObject = new GameObject(
            "FragmentTemplate",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        var headObject = new GameObject("Head", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            arrowObject.transform.SetParent(canvasObject.transform, false);
            visualObject.transform.SetParent(arrowObject.transform, false);
            fragmentTemplateObject.transform.SetParent(visualObject.transform, false);
            headObject.transform.SetParent(visualObject.transform, false);
            var coordinateSpace = (RectTransform)arrowObject.transform;
            coordinateSpace.sizeDelta = new Vector2(1920f, 1080f);
            var fragmentTemplateRect = (RectTransform)fragmentTemplateObject.transform;
            fragmentTemplateRect.pivot = new Vector2(0.5f, 0.5f);
            var headRect = (RectTransform)headObject.transform;
            Image fragmentTemplate = fragmentTemplateObject.GetComponent<Image>();
            Image headImage = headObject.GetComponent<Image>();
            BattleTargetingArrowView view = arrowObject.AddComponent<BattleTargetingArrowView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_coordinateSpace").objectReferenceValue = coordinateSpace;
            serializedView.FindProperty("_visualRoot").objectReferenceValue = visualObject;
            serializedView.FindProperty("_fragmentTemplate").objectReferenceValue = fragmentTemplate;
            serializedView.FindProperty("_headRect").objectReferenceValue = headRect;
            serializedView.FindProperty("_headImage").objectReferenceValue = headImage;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            view.Show(new Vector2(100f, 100f), new Vector2(900f, 100f));

            Assert.That(view.IsVisible, Is.True);
            Image[] activeFragments = visualObject.GetComponentsInChildren<Image>(includeInactive: true);
            activeFragments = System.Array.FindAll(
                activeFragments,
                image => image != headImage && image.gameObject.activeSelf);
            Assert.That(activeFragments, Has.Length.GreaterThan(1));
            Assert.That(fragmentTemplate.gameObject.activeSelf, Is.False);
            Assert.That(activeFragments, Has.All.Property(nameof(Graphic.raycastTarget)).False);
            Assert.That(headImage.raycastTarget, Is.False);
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(headRect.localEulerAngles.z, 0f)),
                Is.GreaterThan(0.1f),
                "水平端点的箭头仍应按曲线终点切线旋转。");
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(
                    activeFragments[0].rectTransform.localEulerAngles.z,
                    activeFragments[activeFragments.Length - 1].rectTransform.localEulerAngles.z)),
                Is.GreaterThan(0.1f),
                "箭身片段应分别跟随所在位置的局部切线。");

            Vector2 originBeforeUpdate = activeFragments[0].rectTransform.anchoredPosition;
            Vector2 endpointBeforeUpdate = headRect.anchoredPosition;
            view.UpdateArrow(new Vector2(200f, 200f), new Vector2(200f, 500f));

            Image updatedFragment = System.Array.Find(
                visualObject.GetComponentsInChildren<Image>(includeInactive: true),
                image => image != headImage && image.gameObject.activeSelf);
            Assert.That(updatedFragment, Is.Not.Null);
            Assert.That(updatedFragment.rectTransform.anchoredPosition, Is.Not.EqualTo(originBeforeUpdate));
            Assert.That(headRect.anchoredPosition, Is.Not.EqualTo(endpointBeforeUpdate));

            view.Hide();

            Assert.That(view.IsVisible, Is.False);
            Assert.That(
                System.Array.Exists(
                    visualObject.GetComponentsInChildren<Image>(includeInactive: true),
                    image => image != headImage && image.gameObject.activeSelf),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }
}
