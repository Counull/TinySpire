using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TinySpire.Run.Map;
using TinySpire.UI.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class RunEntryViewTests
{
    /// <summary>主菜单继续按钮按 ViewModel 启用，并只发布一次 ContinueGame 意图。</summary>
    [Test]
    public void RenderMainMenu_EnabledContinueButton_EmitsContinueAction()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.MainMenu,
                selectedHeroTemplateId: null,
                confirmEnabled: false,
                continueEnabled: true));

            Button continueButton = view.GetButtonForTesting("ContinueGameButton");
            Assert.That(continueButton.interactable, Is.True);
            continueButton.onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.ContinueGame));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>主菜单没有可恢复检查点时继续按钮必须禁用，直接调用也不得发布动作。</summary>
    [Test]
    public void RenderMainMenu_DisabledContinueButton_DoesNotEmitAction()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.MainMenu,
                selectedHeroTemplateId: null,
                confirmEnabled: false));

            Button continueButton = view.GetButtonForTesting("ContinueGameButton");
            Assert.That(continueButton.interactable, Is.False);
            continueButton.onClick.Invoke();

            Assert.That(actions, Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

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
                confirmEnabled: true));

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

    /// <summary>确认与存档故障页保持互斥，关键按钮每次点击只发布一个确定动作。</summary>
    [TestCase(
        RunEntryPage.AbandonConfirmation,
        "ConfirmAbandonButton",
        RunEntryActionKind.ConfirmAbandon)]
    [TestCase(
        RunEntryPage.SaveFailure,
        "RetrySaveButton",
        RunEntryActionKind.RetrySave)]
    [TestCase(
        RunEntryPage.SaveFailure,
        "SaveFailureExitButton",
        RunEntryActionKind.RequestExitAfterSaveFailure)]
    [TestCase(
        RunEntryPage.RollbackConfirmation,
        "ConfirmRollbackButton",
        RunEntryActionKind.ConfirmRollback)]
    public void RenderPersistencePage_ActivatesOnlyTargetAndEmitsActionOnce(
        RunEntryPage targetPage,
        string buttonName,
        RunEntryActionKind expectedAction)
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                targetPage,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                canRollbackFailedSave: true));

            foreach (RunEntryPage page in Enum.GetValues(typeof(RunEntryPage)))
            {
                Assert.That(
                    view.GetPageForTesting(page).activeSelf,
                    Is.EqualTo(page == targetPage),
                    page.ToString());
            }

            view.GetButtonForTesting(buttonName).onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(expectedAction));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>整图名称、稳定内容 ID 与视觉锚点均可见，只有可选节点提交稳定 NodeId。</summary>
    [Test]
    public void RenderMap_DrawsWholeFrozenGraphWithNamesIdsAndVisualAnchors()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            RunMapViewModel map = CreateMapModel();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: map));

            foreach (RunMapNodeViewModel node in map.Nodes)
            {
                Button button = view.GetButtonForTesting($"MapNode_{node.NodeId}_Button");
                Assert.That(
                    button.interactable,
                    Is.EqualTo(node.State == RunMapNodePresentationState.Selectable),
                    node.NodeId);
                Assert.That(
                    button.transform.Find($"MapNode_{node.NodeId}_ButtonLabel")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo(node.DisplayName));
                Assert.That(
                    button.transform.Find($"MapNode_{node.NodeId}_IdentityId")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo(node.ContentId > 0 ? $"#{node.ContentId}" : node.NodeId));
                Assert.That(
                    button.transform.Find($"MapNode_{node.NodeId}_Anchor_{node.VisualAnchorKind}"),
                    Is.Not.Null,
                    node.NodeId);
            }

            Button selected = view.GetButtonForTesting("MapNode_L01-S00_Button");
            selected.onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.EnterMapNode));
            Assert.That(actions[0].MapNodeId, Is.EqualTo(new MapNodeId("L01-S00")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>不同 Boss 身份绘制不同程序化锚点，同一 Boss 的重复终点绘制同一种锚点。</summary>
    [Test]
    public void RenderMap_BossIdentityAnchorsAreDistinctAndRepeatable()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            RunMapViewModel map = CreateMapModel();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: map));

            Transform alpha = FindAnchor(view, "L03-S00", RunMapVisualAnchorKind.BossAlphaCrown);
            Transform beta = FindAnchor(view, "L03-S01", RunMapVisualAnchorKind.BossBetaHorns);
            Transform repeatedAlpha = FindAnchor(view, "L03-S02", RunMapVisualAnchorKind.BossAlphaCrown);

            Assert.That(GetShapeNames(alpha), Is.EqualTo(GetShapeNames(repeatedAlpha)));
            Assert.That(GetShapeNames(alpha), Is.Not.EqualTo(GetShapeNames(beta)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>不同首敌的遭遇节点必须建立不同程序化剪影，而不是共享通用战斗图标。</summary>
    [Test]
    public void RenderMap_EncounterPrimaryEnemySilhouettesAreDistinct()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel()));

            Transform slime = FindAnchor(
                view,
                "L01-S00",
                RunMapVisualAnchorKind.EncounterSlimeSilhouette);
            Transform sentry = FindAnchor(
                view,
                "L01-S01",
                RunMapVisualAnchorKind.EncounterSentrySilhouette);

            Assert.That(GetShapeNames(slime), Is.Not.EqualTo(GetShapeNames(sentry)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>悬停可选节点时完整后半程保持高亮，另一条路线和不可达 Boss 被弱化。</summary>
    [Test]
    public void HoverSelectableNode_HighlightsDownstreamAndDimsForfeitedBoss()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel()));

            Button candidate = view.GetButtonForTesting("MapNode_L01-S00_Button");
            Button reachableBoss = view.GetButtonForTesting("MapNode_L03-S00_Button");
            Button forfeitedBoss = view.GetButtonForTesting("MapNode_L03-S01_Button");
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(
                candidate.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);

            Assert.That(reachableBoss.targetGraphic.color, Is.EqualTo(candidate.targetGraphic.color));
            Assert.That(
                forfeitedBoss.targetGraphic.color.a,
                Is.LessThan(reachableBoss.targetGraphic.color.a));

            ExecuteEvents.Execute(
                candidate.gameObject,
                pointer,
                ExecuteEvents.pointerExitHandler);
            Assert.That(
                forfeitedBoss.targetGraphic.color.a,
                Is.GreaterThan(0.7f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>失败页只提交确认离开终局，Terminal 保存失败页不会暴露旧检查点回退按钮。</summary>
    [Test]
    public void RenderFailureAndTerminalSaveFailure_RemoveRetrySemanticsAndRollback()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.Failure,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel()));
            view.GetButtonForTesting("LeaveTerminalRunButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.LeaveTerminalRun));

            view.Render(CreateModel(
                RunEntryPage.SaveFailure,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel(),
                canRollbackFailedSave: false));
            Assert.That(
                view.GetButtonForTesting("SaveFailureExitButton").gameObject.activeSelf,
                Is.False);
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
        RunMapViewModel map = null,
        bool continueEnabled = false,
        bool canRollbackFailedSave = false)
    {
        var texts = new Dictionary<RunEntryTextSlot, string>();
        foreach (RunEntryTextSlot slot in Enum.GetValues(typeof(RunEntryTextSlot)))
            texts.Add(slot, slot.ToString());

        return new RunEntryViewModel(
            page,
            texts,
            selectedHeroTemplateId,
            confirmEnabled,
            map,
            continueEnabled,
            canRollbackFailedSave);
    }

    /// <summary>创建含两条分支与两个不同 Boss 的完整地图 View 投影。</summary>
    private static RunMapViewModel CreateMapModel()
    {
        RunMapNodeViewModel[] nodes =
        {
            Node("L00-S00", 0, 0, MapNodeKind.Start, 0, "START",
                RunMapVisualAnchorKind.StartFlag,
                RunMapNodePresentationState.Current),
            Node("L01-S00", 1, 0, MapNodeKind.Combat, 5001, "SLIME PATROL\nTest Slime",
                RunMapVisualAnchorKind.EncounterSlimeSilhouette,
                RunMapNodePresentationState.Selectable,
                new[] { "L01-S00", "L02-S00", "L03-S00" },
                new[] { "L01-S00>L02-S00", "L02-S00>L03-S00" }),
            Node("L01-S01", 1, 1, MapNodeKind.Combat, 5002, "SENTRY LINE\nTest Sentry",
                RunMapVisualAnchorKind.EncounterSentrySilhouette,
                RunMapNodePresentationState.Selectable,
                new[] { "L01-S01", "L02-S01", "L03-S01" },
                new[] { "L01-S01>L02-S01", "L02-S01>L03-S01" }),
            Node("L02-S00", 2, 0, MapNodeKind.Combat, 5001, "SLIME PATROL\nTest Slime",
                RunMapVisualAnchorKind.EncounterSlimeSilhouette,
                RunMapNodePresentationState.Locked),
            Node("L02-S01", 2, 1, MapNodeKind.Combat, 5002, "SENTRY LINE\nTest Sentry",
                RunMapVisualAnchorKind.EncounterSentrySilhouette,
                RunMapNodePresentationState.Locked),
            Node("L03-S00", 3, 0, MapNodeKind.Boss, 9001, "BOSS ALPHA",
                RunMapVisualAnchorKind.BossAlphaCrown,
                RunMapNodePresentationState.Locked),
            Node("L03-S01", 3, 1, MapNodeKind.Boss, 9002, "BOSS BETA",
                RunMapVisualAnchorKind.BossBetaHorns,
                RunMapNodePresentationState.Locked),
            Node("L03-S02", 3, 2, MapNodeKind.Boss, 9001, "BOSS ALPHA",
                RunMapVisualAnchorKind.BossAlphaCrown,
                RunMapNodePresentationState.Locked),
        };
        RunMapEdgeViewModel[] edges =
        {
            new RunMapEdgeViewModel("L00-S00", "L01-S00", false),
            new RunMapEdgeViewModel("L00-S00", "L01-S01", false),
            new RunMapEdgeViewModel("L01-S00", "L02-S00", false),
            new RunMapEdgeViewModel("L01-S01", "L02-S01", false),
            new RunMapEdgeViewModel("L02-S00", "L03-S00", false),
            new RunMapEdgeViewModel("L02-S01", "L03-S01", false),
            new RunMapEdgeViewModel("L02-S00", "L03-S02", false),
        };
        return new RunMapViewModel("test-map-fingerprint", nodes, edges);
    }

    /// <summary>创建一个地图节点投影并为非悬停节点补空后半程。</summary>
    private static RunMapNodeViewModel Node(
        string nodeId,
        int layer,
        int slot,
        MapNodeKind kind,
        int contentId,
        string displayName,
        RunMapVisualAnchorKind visualAnchorKind,
        RunMapNodePresentationState state,
        IReadOnlyList<string> downstreamNodeIds = null,
        IReadOnlyList<string> downstreamEdgeKeys = null)
    {
        return new RunMapNodeViewModel(
            nodeId,
            layer,
            slot,
            kind,
            contentId,
            displayName,
            visualAnchorKind,
            state,
            downstreamNodeIds ?? Array.Empty<string>(),
            downstreamEdgeKeys ?? Array.Empty<string>());
    }

    /// <summary>按稳定节点身份读取实际建立的视觉锚点根。</summary>
    private static Transform FindAnchor(
        RunEntryView view,
        string nodeId,
        RunMapVisualAnchorKind kind)
    {
        Button button = view.GetButtonForTesting($"MapNode_{nodeId}_Button");
        return button.transform.Find($"MapNode_{nodeId}_Anchor_{kind}");
    }

    /// <summary>按层级顺序读取程序化锚点的稳定形状名。</summary>
    private static string[] GetShapeNames(Transform anchor)
    {
        Assert.That(anchor, Is.Not.Null);
        return Enumerable.Range(0, anchor.childCount)
            .Select(index => anchor.GetChild(index).name)
            .ToArray();
    }
}
