using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.UI.Battle;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class BattleTurnHudFlowFeedbackTests
{
    /// <summary>确认战斗开始覆盖层只在自身 cue 期间阻断系统指针，并在完成与重复清理后无残留。</summary>
    [Test]
    public void BattleStartOverlay_BlocksPointerOnlyDuringCueAndCleansIdempotently()
    {
        GameObject root = null;
        Sequence timeline = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            root = new GameObject(
                "BattleTurnHudFlowFeedbackTest",
                typeof(RectTransform),
                typeof(BattleTurnHudView));
            BattleTurnHudView view = root.GetComponent<BattleTurnHudView>();
            CanvasGroup startOverlay = CreatePanel(
                root.transform,
                "BattleStartOverlay",
                raycastTarget: true);
            Text startText = CreateText(startOverlay.transform, "BattleStartText");
            CanvasGroup turnBanner = CreatePanel(
                root.transform,
                "TurnBanner",
                raycastTarget: false);
            startOverlay.gameObject.SetActive(false);
            turnBanner.gameObject.SetActive(false);
            SetPrivateField(view, "_battleStartOverlay", startOverlay);
            SetPrivateField(view, "_battleStartText", startText);
            SetPrivateField(view, "_turnBannerGroup", turnBanner);
            int localizationReadCount = 0;
            view.ConfigureFlowFeedback(
                key =>
                {
                    localizationReadCount++;
                    return $"localized:{key}";
                },
                () => UniTask.CompletedTask,
                () => { });

            lease = view.CreateFlowFeedbackTween(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.BattleStartOverlay,
                "battle.ui.battle.start",
                blocksSystemPointer: true));
            timeline = DOTween.Sequence()
                .SetAutoKill(false)
                .SetUpdate(UpdateType.Manual)
                .Pause()
                .Append(lease.Tween);

            Assert.That(startOverlay.gameObject.activeSelf, Is.False);
            Assert.That(turnBanner.gameObject.activeSelf, Is.False);
            Assert.That(localizationReadCount, Is.Zero);

            timeline.Play();
            timeline.ManualUpdate(0.01f, 0.01f);

            Assert.That(startOverlay.gameObject.activeSelf, Is.True);
            Assert.That(startOverlay.blocksRaycasts, Is.True);
            Assert.That(startText.text, Is.EqualTo("localized:battle.ui.battle.start"));
            Assert.That(turnBanner.gameObject.activeSelf, Is.False);
            Assert.That(localizationReadCount, Is.EqualTo(1));

            timeline.Complete(withCallbacks: true);
            lease.Cleanup();
            lease.Cleanup();

            Assert.That(startOverlay.gameObject.activeSelf, Is.False);
            Assert.That(startOverlay.blocksRaycasts, Is.False);
            Assert.That(turnBanner.gameObject.activeSelf, Is.False);
        }
        finally
        {
            timeline?.Kill(complete: false);
            lease?.Cleanup();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>确认玩家与敌人横幅复用同一 Image、使用各自 tint，且全程不阻断系统指针。</summary>
    [Test]
    public void TurnBanners_ReuseImageWithLocalizedTintAndNeverBlockPointer()
    {
        GameObject root = null;
        Sequence timeline = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            root = new GameObject(
                "BattleTurnHudTurnBannerTest",
                typeof(RectTransform),
                typeof(BattleTurnHudView));
            BattleTurnHudView view = root.GetComponent<BattleTurnHudView>();
            CanvasGroup startOverlay = CreatePanel(
                root.transform,
                "BattleStartOverlay",
                raycastTarget: true);
            Text startText = CreateText(startOverlay.transform, "BattleStartText");
            CanvasGroup turnBanner = CreatePanel(
                root.transform,
                "TurnBanner",
                raycastTarget: false);
            Image sharedBannerImage = turnBanner.GetComponent<Image>();
            Text turnBannerText = CreateText(turnBanner.transform, "TurnBannerText");
            var playerTint = new Color32(255, 255, 255, 255);
            var enemyTint = new Color32(210, 88, 88, 255);
            SetPrivateField(view, "_battleStartOverlay", startOverlay);
            SetPrivateField(view, "_battleStartText", startText);
            SetPrivateField(view, "_turnBannerGroup", turnBanner);
            SetPrivateField(view, "_playerTurnBanner", sharedBannerImage);
            SetPrivateField(view, "_turnBannerText", turnBannerText);
            SetPrivateField(view, "_playerTurnBannerColor", (Color)playerTint);
            SetPrivateField(view, "_enemyTurnBannerColor", (Color)enemyTint);
            view.ConfigureFlowFeedback(
                key => $"localized:{key}",
                () => UniTask.CompletedTask,
                () => { });

            lease = view.CreateFlowFeedbackTween(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.PlayerTurnBanner,
                "battle.ui.turn.player",
                blocksSystemPointer: false));
            timeline = CreateAndStartTimeline(lease);
            timeline.ManualUpdate(0.01f, 0.01f);

            Assert.That(turnBanner.gameObject.activeSelf, Is.True);
            Assert.That(turnBanner.blocksRaycasts, Is.False);
            Assert.That(sharedBannerImage.raycastTarget, Is.False);
            Assert.That(turnBannerText.raycastTarget, Is.False);
            Assert.That(turnBannerText.text, Is.EqualTo("localized:battle.ui.turn.player"));
            Assert.That(sharedBannerImage.color, Is.EqualTo((Color)playerTint));

            timeline.Complete(withCallbacks: true);
            lease.Cleanup();
            timeline.Kill(complete: false);
            timeline = null;
            lease = null;

            lease = view.CreateFlowFeedbackTween(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.EnemyTurnBanner,
                "battle.ui.turn.enemy",
                blocksSystemPointer: false));
            timeline = CreateAndStartTimeline(lease);
            timeline.ManualUpdate(0.01f, 0.01f);

            Assert.That(turnBanner.gameObject.activeSelf, Is.True);
            Assert.That(turnBanner.blocksRaycasts, Is.False);
            Assert.That(turnBannerText.text, Is.EqualTo("localized:battle.ui.turn.enemy"));
            Assert.That(sharedBannerImage.color, Is.EqualTo((Color)enemyTint));

            timeline.Complete(withCallbacks: true);
            lease.Cleanup();
            Assert.That(turnBanner.gameObject.activeSelf, Is.False);
            Assert.That(turnBanner.blocksRaycasts, Is.False);
        }
        finally
        {
            timeline?.Kill(complete: false);
            lease?.Cleanup();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>确认终局面板在稳定末尾前取消时释放局部指针锁，且按钮无法触发场景动作。</summary>
    [Test]
    public void BattleOutcome_CancelBeforeStableEnd_HidesPanelAndReleasesPointerLock()
    {
        GameObject root = null;
        Sequence timeline = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            root = new GameObject(
                "BattleTurnHudOutcomeCancelTest",
                typeof(RectTransform),
                typeof(BattleTurnHudView));
            BattleTurnHudView view = root.GetComponent<BattleTurnHudView>();
            CanvasGroup outcomePanel = CreatePanel(
                root.transform,
                "BattleOutcomePanel",
                raycastTarget: true);
            Text outcomeText = CreateText(outcomePanel.transform, "BattleOutcomeText");
            Button restartButton = CreateButton(outcomePanel.transform, "RestartButton");
            Text restartText = CreateText(restartButton.transform, "RestartText");
            Button exitButton = CreateButton(outcomePanel.transform, "ExitButton");
            Text exitText = CreateText(exitButton.transform, "ExitText");
            outcomePanel.gameObject.SetActive(false);
            SetPrivateField(view, "_battleOutcomePanel", outcomePanel);
            SetPrivateField(view, "_battleOutcomeText", outcomeText);
            SetPrivateField(view, "_restartButton", restartButton);
            SetPrivateField(view, "_restartButtonText", restartText);
            SetPrivateField(view, "_exitButton", exitButton);
            SetPrivateField(view, "_exitButtonText", exitText);
            int restartCount = 0;
            int quitCount = 0;
            view.ConfigureFlowFeedback(
                key => $"localized:{key}",
                () =>
                {
                    restartCount++;
                    return UniTask.CompletedTask;
                },
                () => quitCount++);

            lease = view.CreateFlowFeedbackTween(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.BattleOutcome,
                "battle.ui.result.defeat",
                blocksSystemPointer: true));
            timeline = CreateAndStartTimeline(lease);
            timeline.ManualUpdate(0.01f, 0.01f);

            Assert.That(outcomePanel.gameObject.activeSelf, Is.True);
            Assert.That(outcomePanel.blocksRaycasts, Is.True);
            Assert.That(restartButton.interactable, Is.False);
            Assert.That(exitButton.interactable, Is.False);

            timeline.Kill(complete: false);
            timeline = null;
            lease.Cleanup();
            lease.Cleanup();

            Assert.That(outcomePanel.gameObject.activeSelf, Is.False);
            Assert.That(outcomePanel.alpha, Is.Zero);
            Assert.That(outcomePanel.interactable, Is.False);
            Assert.That(outcomePanel.blocksRaycasts, Is.False);
            Assert.That(restartButton.interactable, Is.False);
            Assert.That(exitButton.interactable, Is.False);

            restartButton.onClick.Invoke();
            exitButton.onClick.Invoke();

            Assert.That(restartCount, Is.Zero);
            Assert.That(quitCount, Is.Zero);
        }
        finally
        {
            timeline?.Kill(complete: false);
            lease?.Cleanup();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>确认终局面板完成后稳定保留，并由任一首个场景按钮占用共享一次性 guard。</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void BattleOutcome_StaysVisibleAndEitherTerminalButtonWinsOneShotGuard(
        bool restartFirst)
    {
        GameObject root = null;
        Sequence timeline = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            root = new GameObject(
                "BattleTurnHudOutcomeTest",
                typeof(RectTransform),
                typeof(BattleTurnHudView));
            BattleTurnHudView view = root.GetComponent<BattleTurnHudView>();
            CanvasGroup outcomePanel = CreatePanel(
                root.transform,
                "BattleOutcomePanel",
                raycastTarget: true);
            Text outcomeText = CreateText(outcomePanel.transform, "BattleOutcomeText");
            Button restartButton = CreateButton(outcomePanel.transform, "RestartButton");
            Text restartText = CreateText(restartButton.transform, "RestartText");
            Button exitButton = CreateButton(outcomePanel.transform, "ExitButton");
            Text exitText = CreateText(exitButton.transform, "ExitText");
            outcomePanel.gameObject.SetActive(false);
            SetPrivateField(view, "_battleOutcomePanel", outcomePanel);
            SetPrivateField(view, "_battleOutcomeText", outcomeText);
            SetPrivateField(view, "_restartButton", restartButton);
            SetPrivateField(view, "_restartButtonText", restartText);
            SetPrivateField(view, "_exitButton", exitButton);
            SetPrivateField(view, "_exitButtonText", exitText);
            int restartCount = 0;
            int quitCount = 0;
            view.ConfigureFlowFeedback(
                key => $"localized:{key}",
                () =>
                {
                    restartCount++;
                    return UniTask.CompletedTask;
                },
                () => quitCount++);

            lease = view.CreateFlowFeedbackTween(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.BattleOutcome,
                "battle.ui.result.victory",
                blocksSystemPointer: true));
            timeline = CreateAndStartTimeline(lease);
            timeline.ManualUpdate(0.01f, 0.01f);

            Assert.That(outcomePanel.gameObject.activeSelf, Is.True);
            Assert.That(outcomePanel.blocksRaycasts, Is.True);
            Assert.That(restartButton.interactable, Is.False);
            Assert.That(exitButton.interactable, Is.False);
            Assert.That(outcomeText.text, Is.EqualTo("localized:battle.ui.result.victory"));
            Assert.That(restartText.text, Is.EqualTo("localized:battle.ui.action.restart"));
            Assert.That(exitText.text, Is.EqualTo("localized:battle.ui.action.exit"));

            timeline.Complete(withCallbacks: true);
            lease.Cleanup();
            lease.Cleanup();

            Assert.That(outcomePanel.gameObject.activeSelf, Is.True);
            Assert.That(outcomePanel.alpha, Is.EqualTo(1f));
            Assert.That(outcomePanel.blocksRaycasts, Is.True);
            Assert.That(restartButton.interactable, Is.True);
            Assert.That(exitButton.interactable, Is.True);

            if (restartFirst)
            {
                restartButton.onClick.Invoke();
                restartButton.onClick.Invoke();
                exitButton.onClick.Invoke();
            }
            else
            {
                exitButton.onClick.Invoke();
                exitButton.onClick.Invoke();
                restartButton.onClick.Invoke();
            }

            Assert.That(restartCount, Is.EqualTo(restartFirst ? 1 : 0));
            Assert.That(quitCount, Is.EqualTo(restartFirst ? 0 : 1));
            Assert.That(restartButton.interactable, Is.False);
            Assert.That(exitButton.interactable, Is.False);
            Assert.That(outcomePanel.blocksRaycasts, Is.True);
        }
        finally
        {
            timeline?.Kill(complete: false);
            lease?.Cleanup();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>Run 托管终局仍完成稳定面板表现，但隐藏本地 Restart/Exit 且不能抢占场景流。</summary>
    [Test]
    public void BattleOutcome_RunManaged_HidesLegacyActionsAndKeepsPointerBlock()
    {
        GameObject root = null;
        Sequence timeline = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            root = new GameObject(
                "BattleTurnHudRunOutcomeTest",
                typeof(RectTransform),
                typeof(BattleTurnHudView));
            BattleTurnHudView view = root.GetComponent<BattleTurnHudView>();
            CanvasGroup outcomePanel = CreatePanel(
                root.transform,
                "BattleOutcomePanel",
                raycastTarget: true);
            Text outcomeText = CreateText(outcomePanel.transform, "BattleOutcomeText");
            Button restartButton = CreateButton(outcomePanel.transform, "RestartButton");
            Text restartText = CreateText(restartButton.transform, "RestartText");
            Button exitButton = CreateButton(outcomePanel.transform, "ExitButton");
            Text exitText = CreateText(exitButton.transform, "ExitText");
            SetPrivateField(view, "_battleOutcomePanel", outcomePanel);
            SetPrivateField(view, "_battleOutcomeText", outcomeText);
            SetPrivateField(view, "_restartButton", restartButton);
            SetPrivateField(view, "_restartButtonText", restartText);
            SetPrivateField(view, "_exitButton", exitButton);
            SetPrivateField(view, "_exitButtonText", exitText);
            int restartCount = 0;
            int quitCount = 0;
            view.ConfigureFlowFeedback(
                key => $"localized:{key}",
                () =>
                {
                    restartCount++;
                    return UniTask.CompletedTask;
                },
                () => quitCount++,
                showLegacyTerminalActions: false);

            lease = view.CreateFlowFeedbackTween(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.BattleOutcome,
                "battle.ui.result.victory",
                blocksSystemPointer: true));
            timeline = CreateAndStartTimeline(lease);
            timeline.ManualUpdate(0.01f, 0.01f);

            Assert.That(outcomePanel.gameObject.activeSelf, Is.True);
            Assert.That(outcomePanel.blocksRaycasts, Is.True);
            Assert.That(restartButton.gameObject.activeSelf, Is.False);
            Assert.That(exitButton.gameObject.activeSelf, Is.False);
            Assert.That(restartButton.interactable, Is.False);
            Assert.That(exitButton.interactable, Is.False);

            timeline.Complete(withCallbacks: true);
            lease.Cleanup();
            restartButton.onClick.Invoke();
            exitButton.onClick.Invoke();

            Assert.That(outcomePanel.gameObject.activeSelf, Is.True);
            Assert.That(outcomePanel.alpha, Is.EqualTo(1f));
            Assert.That(outcomePanel.interactable, Is.False);
            Assert.That(outcomePanel.blocksRaycasts, Is.True);
            Assert.That(restartCount, Is.Zero);
            Assert.That(quitCount, Is.Zero);
        }
        finally
        {
            timeline?.Kill(complete: false);
            lease?.Cleanup();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>legacy/debug 的 Restart 必须固定重载 BattleScene，不再复用 Bootstrap 初始入口。</summary>
    [Test]
    public void ConfigureProductionFlowFeedback_LegacyRestartAlwaysLoadsBattleScene()
    {
        GameObject root = null;
        Sequence timeline = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            root = new GameObject(
                "BattleTurnHudLegacyRestartTest",
                typeof(RectTransform),
                typeof(BattleTurnHudView));
            BattleTurnHudView view = root.GetComponent<BattleTurnHudView>();
            CanvasGroup outcomePanel = CreatePanel(
                root.transform,
                "BattleOutcomePanel",
                raycastTarget: true);
            Text outcomeText = CreateText(outcomePanel.transform, "BattleOutcomeText");
            Button restartButton = CreateButton(outcomePanel.transform, "RestartButton");
            Text restartText = CreateText(restartButton.transform, "RestartText");
            Button exitButton = CreateButton(outcomePanel.transform, "ExitButton");
            Text exitText = CreateText(exitButton.transform, "ExitText");
            SetPrivateField(view, "_battleOutcomePanel", outcomePanel);
            SetPrivateField(view, "_battleOutcomeText", outcomeText);
            SetPrivateField(view, "_restartButton", restartButton);
            SetPrivateField(view, "_restartButtonText", restartText);
            SetPrivateField(view, "_exitButton", exitButton);
            SetPrivateField(view, "_exitButtonText", exitText);
            var scenes = new RecordingSceneFlow();
            BattleCommandPresentationAdapter.ConfigureFlowFeedbackView(
                view,
                key => key,
                scenes,
                runManaged: false,
                () => { });

            lease = view.CreateFlowFeedbackTween(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.BattleOutcome,
                "battle.ui.result.defeat",
                blocksSystemPointer: true));
            timeline = CreateAndStartTimeline(lease);
            timeline.Complete(withCallbacks: true);
            restartButton.onClick.Invoke();

            Assert.That(
                scenes.LoadedAddresses,
                Is.EqualTo(new[] { RunSceneAddresses.Battle }));
        }
        finally
        {
            timeline?.Kill(complete: false);
            lease?.Cleanup();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>生产 Adapter 只有在 RunFlow 确实持有 active attempt 时才判定为 Run 托管。</summary>
    [Test]
    public void RunManagementDetection_RequiresActiveRunBattle()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = new RunFlowService(
            store,
            new ConfigService(),
            scenes,
            new UnusedRunEntropySource(),
            new InMemoryRunSaveStore());
        var builder = new ContainerBuilder();
        builder.RegisterInstance(flow).AsSelf();
        using IObjectResolver resolver = builder.Build();

        Assert.That(
            BattleCommandPresentationAdapter.IsRunManaged(resolver),
            Is.False);

        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 2468u));
        store.BeginBattle();

        Assert.That(
            BattleCommandPresentationAdapter.IsRunManaged(resolver),
            Is.True);
    }

    /// <summary>确认 Adapter 虽预构造全部 cue，仍严格先播放战斗开始覆盖层、再玩家横幅、最后一次 completion。</summary>
    [Test]
    public void Adapter_StartBattle_PlaysOverlayThenPlayerBannerBeforeSingleCompletion()
    {
        GameObject root = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            root = new GameObject(
                "BattleTurnHudAdapterStartTest",
                typeof(RectTransform),
                typeof(BattleTurnHudView));
            BattleTurnHudView view = root.GetComponent<BattleTurnHudView>();
            CanvasGroup startOverlay = CreatePanel(
                root.transform,
                "BattleStartOverlay",
                raycastTarget: true);
            Text startText = CreateText(startOverlay.transform, "BattleStartText");
            CanvasGroup turnBanner = CreatePanel(
                root.transform,
                "TurnBanner",
                raycastTarget: false);
            Image bannerImage = turnBanner.GetComponent<Image>();
            Text bannerText = CreateText(turnBanner.transform, "TurnBannerText");
            SetPrivateField(view, "_battleStartOverlay", startOverlay);
            SetPrivateField(view, "_battleStartText", startText);
            SetPrivateField(view, "_turnBannerGroup", turnBanner);
            SetPrivateField(view, "_playerTurnBanner", bannerImage);
            SetPrivateField(view, "_turnBannerText", bannerText);
            var playbackOrder = new System.Collections.Generic.List<string>();
            view.ConfigureFlowFeedback(
                key =>
                {
                    playbackOrder.Add(key);
                    return key;
                },
                () => UniTask.CompletedTask,
                () => { });

            float deltaTime = 0.01f;
            adapter = new BattleCommandPresentationAdapter(
                view.CreateFlowFeedbackTween,
                () => deltaTime);
            var phaseChanged = new BattlePhaseChangedSettlement(
                order: 0,
                BattleTurnPhase.BattleStart,
                BattleTurnPhase.PlayerAction,
                roundNumberBefore: 0,
                roundNumberAfter: 1,
                currentActingEnemyIdBefore: null,
                currentActingEnemyIdAfter: null);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 66,
                BattleCommandType.StartBattle,
                submitterId: null,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[] { phaseChanged });
            int completionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(
                result,
                () =>
                {
                    completionCount++;
                    playbackOrder.Add("Completion");
                });

            Assert.That(startOverlay.gameObject.activeSelf, Is.False);
            Assert.That(turnBanner.gameObject.activeSelf, Is.False);
            Assert.That(playbackOrder, Is.Empty);

            adapter.Tick();

            Assert.That(startOverlay.gameObject.activeSelf, Is.True);
            Assert.That(startOverlay.blocksRaycasts, Is.True);
            Assert.That(turnBanner.gameObject.activeSelf, Is.False);
            Assert.That(playbackOrder, Is.EqualTo(new[] { "battle.ui.battle.start" }));
            Assert.That(completionCount, Is.Zero);

            deltaTime = 0.61f;
            adapter.Tick();

            Assert.That(startOverlay.gameObject.activeSelf, Is.False);
            Assert.That(turnBanner.gameObject.activeSelf, Is.True);
            Assert.That(turnBanner.blocksRaycasts, Is.False);
            Assert.That(
                playbackOrder,
                Is.EqualTo(new[]
                {
                    "battle.ui.battle.start",
                    "battle.ui.turn.player",
                }));
            Assert.That(completionCount, Is.Zero);

            deltaTime = 0.51f;
            adapter.Tick();
            adapter.CompleteImmediately();
            adapter.Tick();

            Assert.That(turnBanner.gameObject.activeSelf, Is.False);
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(
                playbackOrder,
                Is.EqualTo(new[]
                {
                    "battle.ui.battle.start",
                    "battle.ui.turn.player",
                    "Completion",
                }));
        }
        finally
        {
            adapter?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>把一个 View cue 嵌入可控手动父时间线并开始播放。</summary>
    private static Sequence CreateAndStartTimeline(BattleCommandPresentationTween lease)
    {
        Sequence timeline = DOTween.Sequence()
            .SetAutoKill(false)
            .SetUpdate(UpdateType.Manual)
            .Pause()
            .Append(lease.Tween);
        timeline.Play();
        return timeline;
    }

    /// <summary>创建带可选系统指针命中面的最小全屏面板夹具。</summary>
    private static CanvasGroup CreatePanel(
        Transform parent,
        string name,
        bool raycastTarget)
    {
        var panelObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        panelObject.transform.SetParent(parent, worldPositionStays: false);
        panelObject.GetComponent<Image>().raycastTarget = raycastTarget;
        return panelObject.GetComponent<CanvasGroup>();
    }

    /// <summary>创建不参与系统指针命中的最小文字夹具。</summary>
    private static Text CreateText(Transform parent, string name)
    {
        var textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, worldPositionStays: false);
        Text text = textObject.GetComponent<Text>();
        text.raycastTarget = false;
        return text;
    }

    /// <summary>创建由自身 Image 覆盖完整矩形并接收系统指针的最小按钮夹具。</summary>
    private static Button CreateButton(Transform parent, string name)
    {
        var buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);
        Image image = buttonObject.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    /// <summary>为纯 View 测试配置计划内的序列化引用。</summary>
    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }

    /// <summary>记录 legacy 重开请求的稳定 BattleScene 地址。</summary>
    private sealed class RecordingSceneFlow : ISceneFlowService
    {
        /// <summary>按调用顺序保存场景目标。</summary>
        public System.Collections.Generic.List<string> LoadedAddresses { get; } =
            new System.Collections.Generic.List<string>();

        /// <summary>记录目标并同步完成测试期场景请求。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            LoadedAddresses.Add(targetSceneAddress);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>HUD 托管识别只读取已有 Run，不应签发新的 Run 随机输入。</summary>
    private sealed class UnusedRunEntropySource : IRunEntropySource
    {
        /// <summary>误创建 Run 时立即使测试失败。</summary>
        public RunEntropy Next()
        {
            throw new InvalidOperationException("Run management detection must not create a Run.");
        }
    }
}
