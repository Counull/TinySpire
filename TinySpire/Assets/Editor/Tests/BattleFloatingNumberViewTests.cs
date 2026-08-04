using DG.Tweening;
using NUnit.Framework;
using TinySpire.UI.Battle;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleFloatingNumberViewTests
{
    /// <summary>确认三类飘字只使用正负数字字符、样式可区分且创建阶段不可见也不阻断指针。</summary>
    [Test]
    public void CreateTween_NumberKindsUsePureCharactersDistinctStylesAndStayHiddenBeforePlayback()
    {
        var viewObject = new GameObject(
            "BattleFloatingNumberViewTests_View",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(CanvasGroup),
            typeof(BattleFloatingNumberView));
        Text text = viewObject.GetComponent<Text>();
        CanvasGroup canvasGroup = viewObject.GetComponent<CanvasGroup>();
        BattleFloatingNumberView view = viewObject.GetComponent<BattleFloatingNumberView>();
        try
        {
            Tween blockAbsorbed = view.CreateTween(
                BattleCommandPresentationStepKind.BlockAbsorbedNumber,
                amount: 5);
            AssertStyle(
                text,
                canvasGroup,
                "-5",
                new Color32(110, 205, 255, 255));
            blockAbsorbed.Complete(withCallbacks: false);

            Tween healthLoss = view.CreateTween(
                BattleCommandPresentationStepKind.HealthLossNumber,
                amount: 7);
            AssertStyle(
                text,
                canvasGroup,
                "-7",
                new Color32(255, 100, 100, 255));
            healthLoss.Complete(withCallbacks: false);

            Tween blockGained = view.CreateTween(
                BattleCommandPresentationStepKind.BlockGainedNumber,
                amount: 4);
            AssertStyle(
                text,
                canvasGroup,
                "+4",
                new Color32(105, 235, 185, 255));
            blockGained.Complete(withCallbacks: false);
        }
        finally
        {
            Object.DestroyImmediate(viewObject);
        }
    }

    /// <summary>确认当前纯字符样式及预构建隐藏、非交互约束。</summary>
    private static void AssertStyle(
        Text text,
        CanvasGroup canvasGroup,
        string expectedText,
        Color32 expectedColor)
    {
        Assert.That(text.text, Is.EqualTo(expectedText));
        Assert.That(text.color, Is.EqualTo((Color)expectedColor));
        Assert.That(text.raycastTarget, Is.False);
        Assert.That(canvasGroup.alpha, Is.Zero);
        Assert.That(canvasGroup.interactable, Is.False);
        Assert.That(canvasGroup.blocksRaycasts, Is.False);
    }
}
