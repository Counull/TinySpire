using NUnit.Framework;
using TinySpire.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class HandCardVisualPlayabilityTests
{
    private const string CardPrefabPath = "Assets/Arts/Runtime/Card/Prefab/CardView.prefab";

    /// <summary>验证费用不足只切换费用颜色，重新可支付时恢复 Prefab 原始颜色。</summary>
    [Test]
    public void CostPaymentFeedback_InsufficientThenPayable_RestoresOriginalColor()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        using (var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1))
        {
            try
            {
                zones.Draw(1);
                HandCardVisual visual = instance.GetComponent<HandCardVisual>();
                var serializedVisual = new SerializedObject(visual);
                Text costText = serializedVisual.FindProperty("_costText").objectReferenceValue as Text;
                Color originalColor = costText.color;
                CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one, zones.Hand[0], canvasGroup);
                var insufficientColor = new Color(0.95f, 0.2f, 0.2f, 1f);

                visual.SetCostPaymentFeedback(canPayCost: false, insufficientColor);
                visual.SetPlayerInputEnabled(true);
                Assert.That(costText.color, Is.EqualTo(insufficientColor));
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);

                visual.SetCostPaymentFeedback(canPayCost: true, insufficientColor);
                Assert.That(costText.color, Is.EqualTo(originalColor));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
