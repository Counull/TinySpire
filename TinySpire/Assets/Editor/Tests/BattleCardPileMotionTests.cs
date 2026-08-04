using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleCardPileMotionTests
{
    /// <summary>验证牌堆表现锚点每次都读取当前 Text 中心，不缓存旧屏幕坐标。</summary>
    [Test]
    public void PileAnchors_ReadCurrentTextCentersWithoutCaching()
    {
        GameObject canvasObject = new GameObject(
            "BattleCardPileMotionCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView view = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(view, "_drawPileText", drawText);
            SetPrivateField(view, "_discardPileText", discardText);
            SetPrivateField(view, "_exhaustPileText", exhaustText);
            Canvas.ForceUpdateCanvases();

            Assert.That(
                view.TryGetPileScreenAnchor(BattleCardZone.DrawPile, out Vector2 firstDraw),
                Is.True);
            Assert.That(
                view.TryGetPileScreenAnchor(BattleCardZone.DiscardPile, out Vector2 firstDiscard),
                Is.True);

            Vector2 expectedDraw = RectTransformUtility.WorldToScreenPoint(
                null,
                drawText.rectTransform.TransformPoint(drawText.rectTransform.rect.center));
            Vector2 expectedDiscard = RectTransformUtility.WorldToScreenPoint(
                null,
                discardText.rectTransform.TransformPoint(discardText.rectTransform.rect.center));
            Assert.That(firstDraw.x, Is.EqualTo(expectedDraw.x).Within(0.01f));
            Assert.That(firstDraw.y, Is.EqualTo(expectedDraw.y).Within(0.01f));
            Assert.That(firstDiscard.x, Is.EqualTo(expectedDiscard.x).Within(0.01f));
            Assert.That(firstDiscard.y, Is.EqualTo(expectedDiscard.y).Within(0.01f));

            drawText.rectTransform.anchoredPosition += new Vector2(120f, 35f);
            Canvas.ForceUpdateCanvases();

            Assert.That(
                view.TryGetPileScreenAnchor(BattleCardZone.DrawPile, out Vector2 movedDraw),
                Is.True);
            Assert.That(movedDraw, Is.Not.EqualTo(firstDraw));
            expectedDraw = RectTransformUtility.WorldToScreenPoint(
                null,
                drawText.rectTransform.TransformPoint(drawText.rectTransform.rect.center));
            Assert.That(movedDraw.x, Is.EqualTo(expectedDraw.x).Within(0.01f));
            Assert.That(movedDraw.y, Is.EqualTo(expectedDraw.y).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>验证一条重洗记录只懒创建一个非交互字符，并在完成与重复清理时精确释放一次。</summary>
    [Test]
    public void CardsReshuffled_CreatesLazyNonInteractiveTransientAndReleasesOnce()
    {
        GameObject canvasObject = new GameObject(
            "BattleCardReshuffleCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var parentTweenId = new object();
        BattleCommandPresentationTween lease = null;
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView view = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(view, "_drawPileText", drawText);
            SetPrivateField(view, "_discardPileText", discardText);
            SetPrivateField(view, "_exhaustPileText", exhaustText);
            int destroyCount = 0;
            view.ConfigureReshuffleTransientDestroyForTesting(transient =>
            {
                destroyCount++;
                Object.DestroyImmediate(transient);
            });
            Canvas.ForceUpdateCanvases();
            var newDrawPileOrder = new[]
            {
                new CardInstanceId(11),
                new CardInstanceId(12),
                new CardInstanceId(13),
            };
            var cue = new BattleCardMotionCue(
                BattleCardMotionCueKind.CardsReshuffled,
                cardId: null,
                targetId: null,
                settlementOrder: 6,
                newDrawPileOrder);

            lease = view.CreateReshuffleMotionTween(cue, duration: 0.4f, Ease.Linear);

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Null);
            Sequence parent = DOTween.Sequence()
                .SetId(parentTweenId)
                .SetUpdate(UpdateType.Manual)
                .Pause()
                .Append(lease.Tween);
            parent.Play();
            parent.ManualUpdate(0.1f, 0.1f);

            Transform transientTransform = canvasRect.Find("CardReshuffleTransient");
            Assert.That(transientTransform, Is.Not.Null);
            Text transientText = transientTransform.GetComponent<Text>();
            CanvasGroup transientGroup = transientTransform.GetComponent<CanvasGroup>();
            Assert.That(transientText, Is.Not.Null);
            Assert.That(transientText.text, Is.EqualTo("↻"));
            Assert.That(transientText.raycastTarget, Is.False);
            Assert.That(transientGroup, Is.Not.Null);
            Assert.That(transientGroup.interactable, Is.False);
            Assert.That(transientGroup.blocksRaycasts, Is.False);
            Assert.That(cue.NewDrawPileOrder, Is.EqualTo(newDrawPileOrder));
            Assert.That(destroyCount, Is.Zero);

            parent.Complete(withCallbacks: true);
            lease.Cleanup();
            lease.Cleanup();

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Null);
            Assert.That(destroyCount, Is.EqualTo(1));
        }
        finally
        {
            DOTween.Kill(parentTweenId, complete: false);
            lease?.Cleanup();
            Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>验证重洗字符播放中取消会立即清理资源，并丢弃旧命令 completion。</summary>
    [Test]
    public void CardsReshuffled_RunnerDisposeMidFlight_CleansVisualWithoutLateCompletion()
    {
        GameObject canvasObject = new GameObject(
            "BattleCardReshuffleCancelCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        BattleCommandPresentationRunner runner = null;
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            Text drawText = CreatePileText(canvasRect, "DrawPile", new Vector2(-360f, -210f));
            Text discardText = CreatePileText(canvasRect, "DiscardPile", new Vector2(360f, -210f));
            Text exhaustText = CreatePileText(canvasRect, "ExhaustPile", new Vector2(0f, -210f));
            BattleCardPileHudView view = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(view, "_drawPileText", drawText);
            SetPrivateField(view, "_discardPileText", discardText);
            SetPrivateField(view, "_exhaustPileText", exhaustText);
            int destroyCount = 0;
            view.ConfigureReshuffleTransientDestroyForTesting(transient =>
            {
                destroyCount++;
                Object.DestroyImmediate(transient);
            });
            Canvas.ForceUpdateCanvases();
            var cue = new BattleCardMotionCue(
                BattleCardMotionCueKind.CardsReshuffled,
                cardId: null,
                targetId: null,
                settlementOrder: 0,
                new[] { new CardInstanceId(11), new CardInstanceId(12) });
            int activeTweenCountBefore = DOTween.TotalActiveTweens();
            BattleCommandPresentationTween lease = view.CreateReshuffleMotionTween(
                cue,
                duration: 0.4f,
                Ease.Linear);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 1,
                BattleCommandType.EndPlayerAction,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleCardsReshuffledSettlement(0, cue.NewDrawPileOrder),
                });
            int completionCount = 0;
            runner = new BattleCommandPresentationRunner(
                _ => CreateZeroDurationTween(),
                _ => lease);

            runner.Play(BattleCommandPresentationPlan.Create(result), () => completionCount++);
            runner.Tick(0.1f);

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Not.Null);
            Assert.That(destroyCount, Is.Zero);
            Assert.That(completionCount, Is.Zero);

            runner.Dispose();
            runner.Dispose();
            runner.Tick(1f);
            runner.CompleteImmediately();

            Assert.That(canvasRect.Find("CardReshuffleTransient"), Is.Null);
            Assert.That(destroyCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.Zero);
            Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
        }
        finally
        {
            runner?.Dispose();
            Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>创建一个只用于牌堆锚点验证的非交互 Text 节点。</summary>
    private static Text CreatePileText(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition)
    {
        var textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, worldPositionStays: false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 60f);
        rect.anchoredPosition = anchoredPosition;
        Text text = textObject.GetComponent<Text>();
        text.raycastTarget = false;
        return text;
    }

    /// <summary>创建一个由 runner 统一拥有的零时长占位 cue。</summary>
    private static BattleCommandPresentationTween CreateZeroDurationTween()
    {
        return new BattleCommandPresentationTween(
            DOTween.Sequence().AppendCallback(() => { }),
            cleanup: null);
    }

    /// <summary>为纯 View 测试设置现有序列化引用，不新增运行时依赖入口。</summary>
    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }
}
