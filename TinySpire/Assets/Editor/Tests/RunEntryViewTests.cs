using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TinySpire.UI.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class RunEntryViewTests
{
    /// <summary>运行时几何入口必须只使用 TMP 文本，并创建可工作的 Input System UI 事件链。</summary>
    [Test]
    public void Build_CreatesTmpOnlyUiAndInputSystemEventModule()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();

            Assert.That(root.GetComponentInChildren<Canvas>(true), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<GraphicRaycaster>(true), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<EventSystem>(true), Is.Not.Null);
            InputSystemUIInputModule inputModule =
                root.GetComponentInChildren<InputSystemUIInputModule>(true);
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.actionsAsset, Is.Not.Null);
            Assert.That(inputModule.point?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.leftClick?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.move?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.submit?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.cancel?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(root.GetComponentsInChildren<TMP_Text>(true), Is.Not.Empty);
            Assert.That(root.GetComponentsInChildren<Text>(true), Is.Empty);
            Assert.That(
                root.GetComponentsInChildren<TMP_Text>(true).All(text => text.font != null),
                Is.True);
            TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)[0].font;
            Assert.That(
                font.HasCharacters(
                    RunEntryView.RequiredEntryGlyphs,
                    out List<char> missingCharacters),
                Is.True,
                $"Missing RunEntry glyphs: {string.Concat(missingCharacters ?? new List<char>())}");
            Assert.That(
                root.GetComponentsInChildren<Button>(true)
                    .All(button => button.onClick.GetPersistentEventCount() == 0),
                Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>完整 ViewModel 只激活目标页，并把选择按钮归一化为单个带 Hero id 的动作。</summary>
    [Test]
    public void RenderHeroSelection_ActivatesOnlyHeroPageAndEmitsSelectedHeroAction()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.HeroSelection,
                selectedHeroTemplateId: 1002,
                confirmEnabled: true,
                nodeInteractable: false,
                nodeCompleted: false));

            foreach (RunEntryPage page in Enum.GetValues(typeof(RunEntryPage)))
            {
                Assert.That(
                    view.GetPageForTesting(page).activeSelf,
                    Is.EqualTo(page == RunEntryPage.HeroSelection),
                    page.ToString());
            }
            Assert.That(view.GetButtonForTesting("ConfirmHeroButton").interactable, Is.True);
            Assert.That(
                view.GetButtonForTesting("Hero1002Button").targetGraphic.color,
                Is.EqualTo((Color)new Color32(75, 145, 205, 255)));

            view.GetButtonForTesting("Hero1002Button").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.SelectHero));
            Assert.That(actions[0].HeroTemplateId, Is.EqualTo(1002));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>完成地图节点必须显示已完成文案且不可点击；失败页只暴露重开动作。</summary>
    [Test]
    public void RenderCompletedMapAndFailure_AppliesFrozenInteractionState()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                nodeInteractable: false,
                nodeCompleted: true));

            Button node = view.GetButtonForTesting("BattleNodeButton");
            Assert.That(node.interactable, Is.False);
            Assert.That(
                node.GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("Cleared"));
            node.onClick.Invoke();
            Assert.That(actions, Is.Empty);

            view.Render(CreateModel(
                RunEntryPage.Failure,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                nodeInteractable: false,
                nodeCompleted: false));
            view.GetButtonForTesting("RestartBattleButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.RestartBattle));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>创建覆盖全部文本槽位的冻结测试投影。</summary>
    private static RunEntryViewModel CreateModel(
        RunEntryPage page,
        int? selectedHeroTemplateId,
        bool confirmEnabled,
        bool nodeInteractable,
        bool nodeCompleted)
    {
        var texts = new Dictionary<RunEntryTextSlot, string>();
        foreach (RunEntryTextSlot slot in Enum.GetValues(typeof(RunEntryTextSlot)))
            texts.Add(slot, slot == RunEntryTextSlot.Cleared ? "Cleared" : slot.ToString());

        return new RunEntryViewModel(
            page,
            texts,
            selectedHeroTemplateId,
            confirmEnabled,
            nodeInteractable,
            nodeCompleted);
    }
}
