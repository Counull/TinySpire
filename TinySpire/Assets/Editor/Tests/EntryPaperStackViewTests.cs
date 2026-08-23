using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using NUnit.Framework;
using TinySpire.UI.Run;
using UnityEngine;
using UnityEngine.UI;

public sealed class EntryPaperStackViewTests
{
    private sealed class VisualFixture : IDisposable
    {
        /// <summary>保存一次视觉测试建立的根对象与临时纹理。</summary>
        public VisualFixture(
            GameObject root,
            RunEntryView view,
            Sprite backgroundSprite,
            Texture2D backgroundTexture,
            Texture2D paperTexture)
        {
            Root = root;
            View = view;
            BackgroundSprite = backgroundSprite;
            BackgroundTexture = backgroundTexture;
            PaperTexture = paperTexture;
        }

        public GameObject Root { get; }
        public RunEntryView View { get; }
        public Sprite BackgroundSprite { get; }
        public Texture2D BackgroundTexture { get; }
        public Texture2D PaperTexture { get; }

        /// <summary>按依赖顺序销毁动态 UI、Sprite 与临时纹理，并让组件回收自己的 Tween。</summary>
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Root);
            UnityEngine.Object.DestroyImmediate(BackgroundSprite);
            UnityEngine.Object.DestroyImmediate(BackgroundTexture);
            UnityEngine.Object.DestroyImmediate(PaperTexture);
        }
    }

    /// <summary>生产视觉层必须使用同一纸纹、禁止三纸 raycast，并让内容脱离旋转根。</summary>
    [Test]
    public void BuildVisualHierarchy_UsesSharedNonBlockingPaperAndStableContent()
    {
        using VisualFixture fixture = CreateVisualFixture();
        EntryPaperStackView stack = fixture.View.GetPaperStackForTesting();

        RawImage[] papers = fixture.Root.GetComponentsInChildren<RawImage>(includeInactive: true);
        Assert.That(papers, Has.Length.EqualTo(3));
        Assert.That(papers.All(paper => paper.texture == fixture.PaperTexture), Is.True);
        Assert.That(papers.All(paper => !paper.raycastTarget), Is.True);

        Image background = FindTransform(fixture.Root, "EntryTowerBackgroundView")
            .GetComponent<Image>();
        Assert.That(background.sprite, Is.SameAs(fixture.BackgroundSprite));
        Assert.That(background.raycastTarget, Is.False);

        Transform paperRoot = FindTransform(fixture.Root, "PaperStackRoot");
        Transform content = FindTransform(fixture.Root, "MainMenuContent");
        Assert.That(content.IsChildOf(paperRoot), Is.False);
        Assert.That(content.GetComponent<CanvasGroup>().alpha, Is.Zero);

        foreach (string buttonName in MainMenuButtonNames())
        {
            Button button = fixture.View.GetButtonForTesting(buttonName);
            Assert.That(button.targetGraphic, Is.TypeOf<EntryOctagonGraphic>());
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
            Assert.That(
                button.GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(new Vector2(
                    EntryPaperStackView.MainMenuButtonWidth,
                    EntryPaperStackView.MainMenuButtonHeight)));
        }

        CanvasScaler scaler = fixture.Root.GetComponentInChildren<CanvasScaler>(includeInactive: true);
        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
        Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));
        Assert.That(stack, Is.Not.Null);
    }

    /// <summary>1920×1080 最终三条右边界必须复现 V06，而完整纸的开始态必须完全位于左侧画外。</summary>
    [Test]
    public void CalculateLayout_BaselineMatchesV06EdgesAndOffscreenStarts()
    {
        var canvas = new Vector2(1920f, 1080f);
        EntryPaperStackLayout layout = EntryPaperStackView.CalculateLayout(canvas);

        Assert.That(
            EdgeFromLeft(layout.IvoryFinalPosition, layout.SheetSize, canvas, 540f),
            Is.EqualTo(576.0f).Within(0.8f));
        Assert.That(
            EdgeFromLeft(layout.IvoryFinalPosition, layout.SheetSize, canvas, 0f),
            Is.EqualTo(746.5f).Within(0.8f));
        Assert.That(
            EdgeFromLeft(layout.IvoryFinalPosition, layout.SheetSize, canvas, -540f),
            Is.EqualTo(916.7f).Within(0.8f));
        Assert.That(
            EdgeFromLeft(layout.CharcoalFinalPosition, layout.SheetSize, canvas, 0f),
            Is.EqualTo(812.5f).Within(0.8f));
        Assert.That(
            EdgeFromLeft(layout.RedFinalPosition, layout.SheetSize, canvas, 0f),
            Is.EqualTo(863.5f).Within(0.8f));

        float expectedMinimumHeight = canvas.magnitude + 2f * 0.06f * canvas.x;
        Assert.That(layout.SheetSize.y, Is.GreaterThanOrEqualTo(expectedMinimumHeight));
        AssertFullyOffscreen(layout.RedStartPosition, layout.SheetSize, canvas);
        AssertFullyOffscreen(layout.CharcoalStartPosition, layout.SheetSize, canvas);
        AssertFullyOffscreen(layout.IvoryStartPosition, layout.SheetSize, canvas);
    }

    /// <summary>超宽只扩展背景，窄窗按可用宽度收缩构图，均不得退化为 60% 米白大面。</summary>
    [Test]
    public void CalculateLayout_ResponsiveWidthsPreserveCompositionContract()
    {
        var ultrawide = new Vector2(2560f, 1080f);
        EntryPaperStackLayout wideLayout = EntryPaperStackView.CalculateLayout(ultrawide);
        float wideRedCoverage = EdgeFromLeft(
            wideLayout.RedFinalPosition,
            wideLayout.SheetSize,
            ultrawide,
            0f) / ultrawide.x;
        Assert.That(wideLayout.CompositionWidth, Is.EqualTo(1920f).Within(0.1f));
        Assert.That(wideRedCoverage, Is.LessThan(0.36f));

        var narrow = new Vector2(1280f, 960f);
        EntryPaperStackLayout narrowLayout = EntryPaperStackView.CalculateLayout(narrow);
        float narrowIvoryCoverage = EdgeFromLeft(
            narrowLayout.IvoryFinalPosition,
            narrowLayout.SheetSize,
            narrow,
            0f) / narrow.x;
        Assert.That(narrowLayout.CompositionWidth, Is.EqualTo(1280f).Within(0.1f));
        Assert.That(narrowLayout.ContentScale, Is.EqualTo(2f / 3f).Within(0.001f));
        Assert.That(narrowIvoryCoverage, Is.EqualTo(0.3888f).Within(0.001f));
        Assert.That(
            narrowLayout.MenuCenterX -
            0.5f * EntryPaperStackView.MainMenuButtonWidth * narrowLayout.ContentScale,
            Is.GreaterThanOrEqualTo(-0.5f * narrow.x + 31f));
    }

    /// <summary>纸张与内容按冻结时间点播放一次，重复 Render 不重播，禁用时只清理本组件 Tween。</summary>
    [Test]
    public void RenderMainMenu_PlaysFrozenTimelineOnceAndOwnsCleanup()
    {
        using VisualFixture fixture = CreateVisualFixture();
        fixture.View.Render(CreateModel(RunEntryPage.MainMenu));
        EntryPaperStackView stack = fixture.View.GetPaperStackForTesting();
        Sequence sequence = stack.ActiveSequenceForTesting;

        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.Duration(includeLoops: false),
            Is.EqualTo(EntryPaperStackView.ContentFadeStartTime +
                       EntryPaperStackView.ContentFadeDuration).Within(0.001f));
        Assert.That(stack.PlayCountForTesting, Is.EqualTo(1));

        fixture.View.Render(CreateModel(RunEntryPage.MainMenu));
        Assert.That(stack.ActiveSequenceForTesting, Is.SameAs(sequence));
        Assert.That(stack.PlayCountForTesting, Is.EqualTo(1));

        CanvasGroup content = FindTransform(fixture.Root, "MainMenuContent")
            .GetComponent<CanvasGroup>();
        RectTransform ivory = (RectTransform)FindTransform(fixture.Root, "WarmIvoryFullSheet");
        sequence.ManualUpdate(
            EntryPaperStackView.IvorySettledTime,
            EntryPaperStackView.IvorySettledTime);
        Assert.That(ivory.anchoredPosition.x,
            Is.EqualTo(stack.LayoutForTesting.IvoryFinalPosition.x).Within(0.1f));
        Assert.That(ivory.localEulerAngles.z,
            Is.EqualTo(EntryPaperStackView.FinalRotationDegrees).Within(0.1f));
        Assert.That(content.alpha, Is.Zero.Within(0.001f));
        Assert.That(content.interactable, Is.False, "Content must remain disabled before 1.10s.");

        float fadeProgress =
            EntryPaperStackView.ContentFadeStartTime + 0.11f -
            EntryPaperStackView.IvorySettledTime;
        sequence.ManualUpdate(fadeProgress, fadeProgress);
        Assert.That(content.alpha, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(content.interactable, Is.True, "Content must enable when its 1.10s fade begins.");
        Assert.That(content.blocksRaycasts, Is.True, "Content raycasts must enable with the fade.");

        object tweenId = stack.TweenIdForTesting;
        stack.ResolveWithoutEntrance();
        Assert.That(stack.ActiveSequenceForTesting, Is.Null, "Resolving the visual must clear its sequence handle.");
        Assert.That(
            DOTween.IsTweening(tweenId),
            Is.False,
            "Disabling the owner must kill its private tween id.");
    }

    /// <summary>建立带 16:9 临时背景与独立纸纹的生产视觉测试对象。</summary>
    private static VisualFixture CreateVisualFixture()
    {
        var root = new GameObject("RunEntryVisualTestRoot");
        root.SetActive(false);
        var backgroundTexture = new Texture2D(16, 9, TextureFormat.RGBA32, mipChain: false);
        var paperTexture = new Texture2D(8, 8, TextureFormat.RGBA32, mipChain: false);
        Sprite backgroundSprite = Sprite.Create(
            backgroundTexture,
            new Rect(0f, 0f, 16f, 9f),
            new Vector2(0.5f, 0.5f));
        var view = root.AddComponent<RunEntryView>();
        view.ConfigureVisualAssetsForTesting(backgroundSprite, paperTexture);
        root.SetActive(true);
        view.BuildForTesting();
        return new VisualFixture(root, view, backgroundSprite, backgroundTexture, paperTexture);
    }

    /// <summary>创建覆盖全部文字槽位的最小视觉投影，不携带 Run 业务状态。</summary>
    private static RunEntryViewModel CreateModel(RunEntryPage page)
    {
        var texts = new Dictionary<RunEntryTextSlot, string>();
        foreach (RunEntryTextSlot slot in Enum.GetValues(typeof(RunEntryTextSlot)))
            texts.Add(slot, slot.ToString());
        return new RunEntryViewModel(
            page,
            texts,
            selectedHeroTemplateId: null,
            confirmEnabled: false,
            map: null,
            continueEnabled: false);
    }

    /// <summary>把 Canvas 本地右边界换算为从左缘起算的参考图坐标。</summary>
    private static float EdgeFromLeft(
        Vector2 center,
        Vector2 sheetSize,
        Vector2 canvasSize,
        float canvasLocalY)
    {
        return EntryPaperStackView.GetRightEdgeX(
                   center,
                   sheetSize,
                   EntryPaperStackView.FinalRotationDegrees,
                   canvasLocalY) +
               0.5f * canvasSize.x;
    }

    /// <summary>断言一张 -8 度完整纸的最右端仍在 Canvas 左缘 bleed 之外。</summary>
    private static void AssertFullyOffscreen(
        Vector2 center,
        Vector2 sheetSize,
        Vector2 canvasSize)
    {
        float radians = Mathf.Abs(EntryPaperStackView.StartRotationDegrees) * Mathf.Deg2Rad;
        float halfExtent =
            0.5f * sheetSize.x * Mathf.Cos(radians) +
            0.5f * sheetSize.y * Mathf.Sin(radians);
        Assert.That(center.x + halfExtent, Is.LessThan(-0.5f * canvasSize.x));
    }

    /// <summary>返回既有五个主菜单按钮的稳定对象名。</summary>
    private static IEnumerable<string> MainMenuButtonNames()
    {
        yield return "ContinueGameButton";
        yield return "StartGameButton";
        yield return "SettingsButton";
        yield return "CompendiumButton";
        yield return "StatisticsButton";
    }

    /// <summary>按稳定对象名查找测试层级节点，并在缺失时给出明确失败。</summary>
    private static Transform FindTransform(GameObject root, string objectName)
    {
        return root.GetComponentsInChildren<Transform>(includeInactive: true)
            .Single(transform => transform.name == objectName);
    }
}
