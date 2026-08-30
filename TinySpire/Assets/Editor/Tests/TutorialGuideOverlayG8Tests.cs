using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TinySpire.Profile;
using TinySpire.Profile.Presentation;
using TinySpire.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TutorialGuideOverlayG8Tests
{
    /// <summary>125% 设置投影必须同步放大全局教程正文与所有操作标签。</summary>
    [Test]
    public void Accessibility_Percent125ScalesAllTutorialText()
    {
        var owner = new GameObject("Tutorial Overlay Text Scale Test");
        try
        {
            TutorialGuideOverlayView view = owner.AddComponent<TutorialGuideOverlayView>();
            view.Initialize(
                key => key,
                _ => new CallbackDisposable(() => { }));

            view.ApplyAccessibility(new TutorialGuideAccessibilityViewModel(
                AppTextScale.Percent125,
                highContrast: false,
                reducedMotion: false));

            Assert.That(FindText(owner, "Tutorial Prompt").fontSize, Is.EqualTo(38.75f));
            Assert.That(FindText(owner, "Tutorial Confirm Label").fontSize, Is.EqualTo(27.5f));
            Assert.That(FindText(owner, "Tutorial Skip Label").fontSize, Is.EqualTo(27.5f));
            Assert.That(FindText(owner, "Tutorial Reset Label").fontSize, Is.EqualTo(27.5f));
            Assert.That(FindText(owner, "Tutorial Hidden Reset Label").fontSize, Is.EqualTo(23.75f));

            view.ApplyAccessibility(new TutorialGuideAccessibilityViewModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));

            Assert.That(FindText(owner, "Tutorial Prompt").fontSize, Is.EqualTo(31f));
            Assert.That(FindText(owner, "Tutorial Confirm Label").fontSize, Is.EqualTo(22f));
            Assert.That(FindText(owner, "Tutorial Skip Label").fontSize, Is.EqualTo(22f));
            Assert.That(FindText(owner, "Tutorial Reset Label").fontSize, Is.EqualTo(22f));
            Assert.That(FindText(owner, "Tutorial Hidden Reset Label").fontSize, Is.EqualTo(19f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    /// <summary>高对比投影必须采用黑底白正文与白底黑按钮，并可完整恢复默认配色。</summary>
    [Test]
    public void Accessibility_HighContrastAppliesAndRestoresPalette()
    {
        var owner = new GameObject("Tutorial Overlay High Contrast Test");
        try
        {
            TutorialGuideOverlayView view = owner.AddComponent<TutorialGuideOverlayView>();
            view.Initialize(
                key => key,
                _ => new CallbackDisposable(() => { }));
            Image backdrop = FindImage(owner, "Tutorial Guide Input Gate");
            Image paper = FindImage(owner, "Tutorial Dark Paper");
            Image confirmButton = FindImage(owner, "Tutorial Confirm");
            TMP_Text prompt = FindText(owner, "Tutorial Prompt");
            TMP_Text confirm = FindText(owner, "Tutorial Confirm Label");
            Button[] buttons = owner.GetComponentsInChildren<Button>(includeInactive: true);
            Color defaultBackdrop = backdrop.color;
            Color defaultPaper = paper.color;
            Color defaultButton = confirmButton.color;
            Color defaultPrompt = prompt.color;
            Color defaultConfirm = confirm.color;

            view.ApplyAccessibility(new TutorialGuideAccessibilityViewModel(
                AppTextScale.Percent100,
                highContrast: true,
                reducedMotion: false));

            Assert.That(backdrop.color, Is.EqualTo(Color.black));
            Assert.That(paper.color, Is.EqualTo(Color.black));
            Assert.That(prompt.color, Is.EqualTo(Color.white));
            Assert.That(confirmButton.color, Is.EqualTo(Color.white));
            Assert.That(confirm.color, Is.EqualTo(Color.black));
            Assert.That(buttons.All(button => button.targetGraphic.color == Color.white), Is.True);
            Assert.That(buttons.All(button =>
                    button.GetComponentInChildren<TMP_Text>(includeInactive: true).color == Color.black),
                Is.True);
            Assert.That(
                FindOutline(owner, "Tutorial Dark Paper").effectColor,
                Is.EqualTo(Color.white));

            view.ApplyAccessibility(new TutorialGuideAccessibilityViewModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));

            Assert.That(backdrop.color, Is.EqualTo(defaultBackdrop));
            Assert.That(paper.color, Is.EqualTo(defaultPaper));
            Assert.That(confirmButton.color, Is.EqualTo(defaultButton));
            Assert.That(prompt.color, Is.EqualTo(defaultPrompt));
            Assert.That(confirm.color, Is.EqualTo(defaultConfirm));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    /// <summary>减少动态必须移除全部教程按钮过渡，关闭后恢复既定 ColorTint。</summary>
    [Test]
    public void Accessibility_ReducedMotionDisablesAndRestoresButtonTransitions()
    {
        var owner = new GameObject("Tutorial Overlay Reduced Motion Test");
        try
        {
            TutorialGuideOverlayView view = owner.AddComponent<TutorialGuideOverlayView>();
            view.Initialize(
                key => key,
                _ => new CallbackDisposable(() => { }));
            Button[] buttons = owner.GetComponentsInChildren<Button>(includeInactive: true);
            Assert.That(buttons, Has.Length.EqualTo(4));
            Assert.That(buttons.All(button => button.transition == Selectable.Transition.ColorTint),
                Is.True);

            view.ApplyAccessibility(new TutorialGuideAccessibilityViewModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: true));

            Assert.That(buttons.All(button => button.transition == Selectable.Transition.None),
                Is.True);

            view.ApplyAccessibility(new TutorialGuideAccessibilityViewModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));

            Assert.That(buttons.All(button => button.transition == Selectable.Transition.ColorTint),
                Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    /// <summary>Bootstrap 重复确保 overlay 时必须复用同一组件与唯一 ScreenSpaceOverlay Canvas。</summary>
    [Test]
    public void Bootstrap_EnsuresOnePersistentScreenSpaceOverlay()
    {
        var owner = new GameObject("Tutorial Overlay Bootstrap Test");
        try
        {
            TutorialGuideOverlayView first = Bootstrap.EnsureTutorialOverlay(owner);
            TutorialGuideOverlayView second = Bootstrap.EnsureTutorialOverlay(owner);
            Canvas[] canvases = owner.GetComponentsInChildren<Canvas>(includeInactive: true);

            Assert.That(second, Is.SameAs(first));
            Assert.That(owner.GetComponents<TutorialGuideOverlayView>(), Has.Length.EqualTo(1));
            Assert.That(canvases, Has.Length.EqualTo(1));
            Assert.That(canvases[0].renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    /// <summary>可见模型必须只解析稳定 key、阻挡下层 raycast，并在 locale 变化后重绘全部 TMP 文本。</summary>
    [Test]
    public void Render_UsesLocalizedKeysBlocksInputAndRedrawsOnLocaleChange()
    {
        var owner = new GameObject("Tutorial Overlay Render Test");
        try
        {
            TutorialGuideOverlayView view = owner.AddComponent<TutorialGuideOverlayView>();
            string localePrefix = "zh:";
            Action localeChanged = null;
            view.Initialize(
                key => localePrefix + key,
                handler =>
                {
                    localeChanged = handler;
                    return new CallbackDisposable(() => localeChanged = null);
                });
            TutorialGuideViewModel model = TutorialGuideViewModel.Visible(
                TutorialPromptCatalog.Ordered[0]);

            view.Render(model);

            CanvasGroup inputGate = owner.GetComponentInChildren<CanvasGroup>(includeInactive: true);
            Assert.That(inputGate, Is.Not.Null);
            Assert.That(inputGate.blocksRaycasts, Is.True);
            Assert.That(VisibleTexts(owner), Is.EquivalentTo(new[]
            {
                "zh:tutorial.guide.main_menu_welcome",
                "zh:tutorial.guide.confirm",
                "zh:tutorial.guide.skip",
                "zh:tutorial.guide.reset",
            }));

            localePrefix = "en:";
            localeChanged?.Invoke();

            Assert.That(VisibleTexts(owner), Is.EquivalentTo(new[]
            {
                "en:tutorial.guide.main_menu_welcome",
                "en:tutorial.guide.confirm",
                "en:tutorial.guide.skip",
                "en:tutorial.guide.reset",
            }));

            view.Render(TutorialGuideViewModel.Hidden);

            Assert.That(inputGate.blocksRaycasts, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    /// <summary>真实三按钮只能发布 Confirm、Skip、Reset 三类既定 View 事件。</summary>
    [Test]
    public void Buttons_PublishOnlyTutorialViewEvents()
    {
        var owner = new GameObject("Tutorial Overlay Button Test");
        try
        {
            TutorialGuideOverlayView view = owner.AddComponent<TutorialGuideOverlayView>();
            view.Initialize(
                key => key,
                _ => new CallbackDisposable(() => { }));
            view.Render(TutorialGuideViewModel.Visible(TutorialPromptCatalog.Ordered[0]));
            int confirmCount = 0;
            int skipCount = 0;
            int resetCount = 0;
            view.ConfirmRequested += () => confirmCount++;
            view.SkipRequested += () => skipCount++;
            view.ResetRequested += () => resetCount++;

            Button[] buttons = owner.GetComponentsInChildren<Button>(includeInactive: false);
            Assert.That(buttons, Has.Length.EqualTo(3));
            FindButton(buttons, TutorialGuideTextKeys.Confirm).onClick.Invoke();
            FindButton(buttons, TutorialGuideTextKeys.Skip).onClick.Invoke();
            FindButton(buttons, TutorialGuideTextKeys.Reset).onClick.Invoke();

            Assert.That(confirmCount, Is.EqualTo(1));
            Assert.That(skipCount, Is.EqualTo(1));
            Assert.That(resetCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    /// <summary>教程隐藏或 Skip 后必须只保留局部 Reset 入口，且全屏门禁继续释放下层输入。</summary>
    [Test]
    public void HiddenModel_KeepsResetReachableWithoutBlockingUnderlyingUi()
    {
        var owner = new GameObject("Tutorial Hidden Reset Test");
        try
        {
            TutorialGuideOverlayView view = owner.AddComponent<TutorialGuideOverlayView>();
            view.Initialize(
                key => "localized:" + key,
                _ => new CallbackDisposable(() => { }));
            int resetCount = 0;
            view.ResetRequested += () => resetCount++;

            view.Render(TutorialGuideViewModel.Hidden);

            CanvasGroup inputGate = owner.GetComponentInChildren<CanvasGroup>(includeInactive: true);
            Button[] activeButtons = owner.GetComponentsInChildren<Button>(includeInactive: false);
            Assert.That(inputGate.blocksRaycasts, Is.False);
            Assert.That(activeButtons, Has.Length.EqualTo(1));
            Assert.That(
                activeButtons[0].GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo("localized:tutorial.guide.reset"));

            activeButtons[0].onClick.Invoke();

            Assert.That(resetCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    /// <summary>模态教程显示时必须接管键盘焦点，隐藏后再把焦点归还给原控件。</summary>
    [Test]
    public void VisibilityChange_TakesAndRestoresKeyboardFocus()
    {
        var owner = new GameObject("Tutorial Overlay Focus Test");
        var underlying = new GameObject(
            "Underlying Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image),
            typeof(Button));
        GameObject eventSystemOwner = null;
        EventSystem eventSystem = EventSystem.current;
        try
        {
            if (eventSystem == null)
            {
                eventSystemOwner = new GameObject("Tutorial Focus EventSystem");
                eventSystem = eventSystemOwner.AddComponent<EventSystem>();
            }

            TutorialGuideOverlayView view = owner.AddComponent<TutorialGuideOverlayView>();
            view.Initialize(
                key => key,
                _ => new CallbackDisposable(() => { }));
            eventSystem.SetSelectedGameObject(underlying);

            view.Render(TutorialGuideViewModel.Visible(TutorialPromptCatalog.Ordered[0]));

            GameObject selectedInOverlay = eventSystem.currentSelectedGameObject;
            Assert.That(selectedInOverlay, Is.Not.Null);
            Assert.That(
                selectedInOverlay.transform.IsChildOf(owner.transform),
                Is.True,
                $"Expected overlay focus, actual selection: {selectedInOverlay.name}");

            view.Render(TutorialGuideViewModel.Hidden);

            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(underlying));
        }
        finally
        {
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(underlying);
            if (eventSystemOwner != null)
                UnityEngine.Object.DestroyImmediate(eventSystemOwner);
        }
    }

    /// <summary>读取当前激活 overlay 内所有非空 TMP 显示文本。</summary>
    private static IReadOnlyList<string> VisibleTexts(GameObject owner)
    {
        return owner.GetComponentsInChildren<TMP_Text>(includeInactive: false)
            .Select(text => text.text)
            .Where(text => !string.IsNullOrEmpty(text))
            .ToArray();
    }

    /// <summary>按稳定对象名读取包括隐藏节点在内的唯一教程 TMP 文本。</summary>
    private static TMP_Text FindText(GameObject owner, string objectName)
    {
        return owner.GetComponentsInChildren<TMP_Text>(includeInactive: true)
            .Single(text => text.gameObject.name == objectName);
    }

    /// <summary>按稳定对象名读取包括隐藏节点在内的唯一教程 Image。</summary>
    private static Image FindImage(GameObject owner, string objectName)
    {
        return owner.GetComponentsInChildren<Image>(includeInactive: true)
            .Single(image => image.gameObject.name == objectName);
    }

    /// <summary>按稳定对象名读取包括隐藏节点在内的唯一教程 Outline。</summary>
    private static Outline FindOutline(GameObject owner, string objectName)
    {
        return owner.GetComponentsInChildren<Outline>(includeInactive: true)
            .Single(outline => outline.gameObject.name == objectName);
    }

    /// <summary>按当前本地化标签寻找一个真实按钮。</summary>
    private static Button FindButton(IEnumerable<Button> buttons, string label)
    {
        return buttons.Single(button =>
            button.GetComponentInChildren<TMP_Text>(includeInactive: true).text == label);
    }

    /// <summary>测试 locale 订阅使用的最小幂等释放句柄。</summary>
    private sealed class CallbackDisposable : IDisposable
    {
        private Action _dispose;

        /// <summary>冻结一次释放回调。</summary>
        public CallbackDisposable(Action dispose)
        {
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        /// <summary>至多执行一次释放回调。</summary>
        public void Dispose()
        {
            Action dispose = _dispose;
            _dispose = null;
            dispose?.Invoke();
        }
    }
}
