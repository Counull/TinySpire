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

    /// <summary>验证功能性箭头按屏幕端点显示和隐藏，且全部 Graphic 始终不接收射线。</summary>
    [Test]
    public void Arrow_ShowUpdateHide_UsesScreenEndpointsAndRemainsNonRaycast()
    {
        var canvasObject = new GameObject("TargetingCanvas", typeof(RectTransform), typeof(Canvas));
        var arrowObject = new GameObject("TargetingArrow", typeof(RectTransform));
        var visualObject = new GameObject("VisualRoot", typeof(RectTransform));
        var lineObject = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var headObject = new GameObject("Head", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            arrowObject.transform.SetParent(canvasObject.transform, false);
            visualObject.transform.SetParent(arrowObject.transform, false);
            lineObject.transform.SetParent(visualObject.transform, false);
            headObject.transform.SetParent(visualObject.transform, false);
            var coordinateSpace = (RectTransform)arrowObject.transform;
            coordinateSpace.sizeDelta = new Vector2(1920f, 1080f);
            var lineRect = (RectTransform)lineObject.transform;
            lineRect.pivot = new Vector2(0f, 0.5f);
            var headRect = (RectTransform)headObject.transform;
            Image lineImage = lineObject.GetComponent<Image>();
            Image headImage = headObject.GetComponent<Image>();
            BattleTargetingArrowView view = arrowObject.AddComponent<BattleTargetingArrowView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_coordinateSpace").objectReferenceValue = coordinateSpace;
            serializedView.FindProperty("_visualRoot").objectReferenceValue = visualObject;
            serializedView.FindProperty("_lineRect").objectReferenceValue = lineRect;
            serializedView.FindProperty("_headRect").objectReferenceValue = headRect;
            serializedView.FindProperty("_lineImage").objectReferenceValue = lineImage;
            serializedView.FindProperty("_headImage").objectReferenceValue = headImage;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            view.Show(new Vector2(100f, 100f), new Vector2(300f, 100f));

            Assert.That(view.IsVisible, Is.True);
            Assert.That(lineRect.sizeDelta.x, Is.EqualTo(200f).Within(0.01f));
            Assert.That(lineImage.raycastTarget, Is.False);
            Assert.That(headImage.raycastTarget, Is.False);

            view.Hide();

            Assert.That(view.IsVisible, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }
}
