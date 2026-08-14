using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;
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

    /// <summary>验证同一交互模式投影能区分禁用灰化、费用不足可拖与正常可用三种表现。</summary>
    [Test]
    public void InteractionPresentation_DisabledVisualOnlyPlayable_UsesDistinctStyleAndPointerContract()
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
                Color originalCostColor = costText.color;
                CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one, zones.Hand[0], canvasGroup);
                var insufficientColor = new Color(0.95f, 0.2f, 0.2f, 1f);

                visual.SetInteractionPresentation(
                    HandCardInteractionMode.Disabled,
                    insufficientColor);

                Image disabledOverlay = visual.CardContent.Find("DisabledOverlay")?.GetComponent<Image>();
                Assert.That(disabledOverlay, Is.Not.Null);
                Assert.That(disabledOverlay.gameObject.activeSelf, Is.True);
                Assert.That(disabledOverlay.color.r, Is.EqualTo(disabledOverlay.color.g).Within(0.001f));
                Assert.That(disabledOverlay.color.g, Is.EqualTo(disabledOverlay.color.b).Within(0.001f));
                Assert.That(disabledOverlay.color.a, Is.GreaterThan(0f));
                Assert.That(disabledOverlay.raycastTarget, Is.False);
                Assert.That(canvasGroup.interactable, Is.False);
                Assert.That(canvasGroup.blocksRaycasts, Is.False);
                Assert.That(costText.color, Is.EqualTo(originalCostColor));

                visual.SetInteractionPresentation(
                    HandCardInteractionMode.VisualOnly,
                    insufficientColor);

                Assert.That(disabledOverlay.gameObject.activeSelf, Is.False);
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
                Assert.That(costText.color, Is.EqualTo(insufficientColor));

                visual.SetInteractionPresentation(
                    HandCardInteractionMode.Playable,
                    insufficientColor);

                Assert.That(disabledOverlay.gameObject.activeSelf, Is.False);
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
                Assert.That(costText.color, Is.EqualTo(originalCostColor));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    /// <summary>验证选牌态在底层禁用时区分候选与非候选点击契约，并始终让待定状态优先关闭射线。</summary>
    [Test]
    public void SelectionPresentation_DisabledCandidateAndNonCandidate_PreservesClickRaycastWithPendingPriority()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        using var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        try
        {
            zones.Draw(1);
            HandCardVisual visual = instance.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one, zones.Hand[0], canvasGroup);
            var insufficientColor = new Color(0.95f, 0.2f, 0.2f, 1f);

            visual.SetInteractionPresentation(
                HandCardInteractionMode.Disabled,
                insufficientColor,
                HandCardSelectionPresentationRole.Candidate);

            Image disabledOverlay = visual.CardContent.Find("DisabledOverlay")?.GetComponent<Image>();
            Assert.That(visual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.Candidate));
            Assert.That(disabledOverlay, Is.Not.Null);
            Assert.That(disabledOverlay.gameObject.activeSelf, Is.False);
            Assert.That(canvasGroup.interactable, Is.True);
            Assert.That(canvasGroup.blocksRaycasts, Is.True);

            visual.SetInteractionPresentation(
                HandCardInteractionMode.Disabled,
                insufficientColor,
                HandCardSelectionPresentationRole.NonCandidate);

            Assert.That(visual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.NonCandidate));
            Assert.That(disabledOverlay.gameObject.activeSelf, Is.True);
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.True);

            BattleCommandHandle pendingHandle = coordinator.PreRegister(new StartBattleCommand());
            visual.SetCommandPending(pendingHandle);
            visual.SetInteractionPresentation(
                HandCardInteractionMode.Disabled,
                insufficientColor,
                HandCardSelectionPresentationRole.Candidate);

            Assert.That(visual.IsCommandPending, Is.True);
            Assert.That(visual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.Candidate));
            Assert.That(disabledOverlay.gameObject.activeSelf, Is.False);
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
