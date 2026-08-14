using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class HandCardTargetFocusTests
{
    private const string CardPrefabPath = "Assets/Arts/Runtime/Card/Prefab/CardView.prefab";

    /// <summary>释放测试队列工厂为本类临时创建的敌人意图资源。</summary>
    [TearDown]
    public void TearDown()
    {
        BattleCommandQueueTestFactory.DisposeOwnedEnemyIntents();
    }

    /// <summary>验证首次 Enemy 聚焦按屏幕锚点移动卡牌，并在到达时旋转归零、轻微放大。</summary>
    [Test]
    public void TargetFocus_EnemyFirstCrossing_MovesToScreenAnchorWithZeroRotationAndFocusScale()
    {
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        using (var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1))
        {
            HandCardVisual visual = null;
            try
            {
                zones.Draw(1);
                visual = instance.GetComponent<HandCardVisual>();
                CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                Vector3 baseScale = Vector3.one * 0.36f;
                visual.Initialize(baseScale, zones.Hand[0], canvasGroup);
                var basePose = new HandCardPose(new Vector2(-80f, -320f), 12f, 0);
                visual.SetBasePoseImmediately(basePose);
                Vector2 screenDelta = new Vector2(140f, 180f);
                Vector2 focusScreenPosition = visual.GetScreenCenter() + screenDelta;

                visual.PlayTargetFocus(
                    focusScreenPosition,
                    focusScale: 1.08f,
                    duration: 0.2f,
                    Ease.OutCubic,
                    breathScale: 1.025f,
                    breathDuration: 0.55f);

                FieldInfo transitionField = typeof(HandCardVisual).GetField(
                    "_targetFocusTransitionTween",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(transitionField, Is.Not.Null);
                Tween transition = transitionField.GetValue(visual) as Tween;
                Assert.That(transition, Is.Not.Null);
                transition.Complete(withCallbacks: true);

                Vector2 finalScreenPosition = visual.GetScreenCenter();
                Assert.That(finalScreenPosition.x, Is.EqualTo(focusScreenPosition.x).Within(0.01f));
                Assert.That(finalScreenPosition.y, Is.EqualTo(focusScreenPosition.y).Within(0.01f));
                Assert.That(
                    Mathf.DeltaAngle(visual.CardContent.localEulerAngles.z, 0f),
                    Is.Zero.Within(0.01f));
                Assert.That(visual.CardContent.localScale.x, Is.EqualTo(baseScale.x * 1.08f).Within(0.001f));
                Assert.That(visual.CardContent.localScale.y, Is.EqualTo(baseScale.y * 1.08f).Within(0.001f));
            }
            finally
            {
                if (visual != null)
                    visual.CancelTargetFocus();
                Object.DestroyImmediate(instance);
                Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
            }
        }
    }

    /// <summary>验证 Container 只在首次进入 EnemyTargeting 时启动聚焦，后续拖拽帧不会重启 Tween。</summary>
    [Test]
    public void TargetFocus_EnemyTargetingUpdates_DoNotRestartTransition()
    {
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject cardObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("TargetFocusPresenter");
        using (var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1))
        using (var combatants = new BattleCombatantsData())
        {
            HandCardVisual visual = null;
            try
            {
                zones.Draw(1);
                EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
                HandCardContainer container = handObject.GetComponent<HandCardContainer>();
                BattleParticipantPresenter presenter =
                    presenterObject.AddComponent<BattleParticipantPresenter>();
                visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, zones.Hand[0], canvasGroup);
                visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 8f, 0));

                typeof(HandCardContainer).GetField(
                    "_participantPresenter",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
                var evaluation = new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetRequired,
                    cfg.battle.TargetRule.Enemy,
                    canStartInteraction: true,
                    canPayCost: true,
                    new[] { enemy.Id });
                MethodInfo enterMethod = typeof(HandCardContainer).GetMethod(
                    "EnterEnemyTargeting",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo updateMethod = typeof(HandCardContainer).GetMethod(
                    "UpdateEnemyTargeting",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(enterMethod, Is.Not.Null);
                Assert.That(updateMethod, Is.Not.Null);

                enterMethod.Invoke(container, new object[] { visual, evaluation, new Vector2(800f, 520f) });
                Tween firstTransition = typeof(HandCardVisual).GetField(
                    "_targetFocusTransitionTween",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(visual) as Tween;
                Assert.That(firstTransition, Is.Not.Null);

                updateMethod.Invoke(container, new object[] { visual, new Vector2(860f, 540f) });
                Tween transitionAfterUpdate = typeof(HandCardVisual).GetField(
                    "_targetFocusTransitionTween",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(visual) as Tween;
                Assert.That(transitionAfterUpdate, Is.SameAs(firstTransition));
            }
            finally
            {
                if (visual != null)
                    visual.CancelTargetFocus();
                Object.DestroyImmediate(cardObject);
                Object.DestroyImmediate(handObject);
                Object.DestroyImmediate(presenterObject);
                Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
            }
        }
    }

    /// <summary>验证指针静止时，LateUpdate 仍让箭头起点跟随聚焦中的卡牌而终点保持不动。</summary>
    [Test]
    public void TargetFocus_LateUpdate_TracksMovingCardWhilePointerStaysStill()
    {
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject cardObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("TargetFocusLateUpdatePresenter");
        using (var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1))
        {
            HandCardVisual visual = null;
            try
            {
                zones.Draw(1);
                HandCardContainer container = handObject.GetComponent<HandCardContainer>();
                BattleParticipantPresenter presenter =
                    presenterObject.AddComponent<BattleParticipantPresenter>();
                visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, zones.Hand[0], canvasGroup);
                visual.SetBasePoseImmediately(new HandCardPose(new Vector2(-100f, -320f), 8f, 0));
                Vector2 pointerScreenPosition = new Vector2(900f, 560f);
                Vector2 focusScreenPosition = visual.GetScreenCenter() + new Vector2(240f, 180f);
                visual.PlayTargetFocus(
                    focusScreenPosition,
                    focusScale: 1.08f,
                    duration: 0.2f,
                    Ease.OutCubic,
                    breathScale: 1.025f,
                    breathDuration: 0.55f);

                BattleTargetingArrowView arrow = handObject
                    .GetComponentInChildren<BattleTargetingArrowView>(includeInactive: true);
                Assert.That(arrow, Is.Not.Null);
                arrow.Show(visual.GetScreenCenter(), pointerScreenPosition);
                Image[] activeGraphics = arrow.GetComponentsInChildren<Image>(includeInactive: false);
                Image originFragment = System.Array.Find(activeGraphics, image => !image.preserveAspect);
                Image head = System.Array.Find(activeGraphics, image => image.preserveAspect);
                Assert.That(arrow.IsVisible, Is.True);
                Assert.That(originFragment, Is.Not.Null);
                Assert.That(head, Is.Not.Null);
                Vector2 originBefore = originFragment.rectTransform.anchoredPosition;
                Vector2 endpointBefore = head.rectTransform.anchoredPosition;

                typeof(HandCardContainer).GetField(
                    "_participantPresenter",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
                typeof(HandCardContainer).GetField(
                    "_draggingCard",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, visual);
                typeof(HandCardContainer).GetField(
                    "_dragPhase",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                    container,
                    HandCardDragPhase.EnemyTargeting);
                typeof(HandCardContainer).GetField(
                    "_lastPointerScreenPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                    container,
                    pointerScreenPosition);
                typeof(HandCardContainer).GetField(
                    "_hasPointerScreenPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, true);

                Tween transition = typeof(HandCardVisual).GetField(
                    "_targetFocusTransitionTween",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(visual) as Tween;
                transition.Goto(0.1f, andPlay: false);
                MethodInfo lateUpdate = typeof(HandCardContainer).GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lateUpdate, Is.Not.Null);
                lateUpdate.Invoke(container, null);

                Image updatedFragment = System.Array.Find(
                    arrow.GetComponentsInChildren<Image>(includeInactive: false),
                    image => !image.preserveAspect);
                Image updatedHead = System.Array.Find(
                    arrow.GetComponentsInChildren<Image>(includeInactive: false),
                    image => image.preserveAspect);
                Assert.That(arrow.IsVisible, Is.True);
                Assert.That(updatedFragment, Is.Not.Null);
                Assert.That(updatedHead, Is.Not.Null);
                Assert.That(updatedFragment.rectTransform.anchoredPosition, Is.Not.EqualTo(originBefore));
                Assert.That(updatedHead.rectTransform.anchoredPosition.x, Is.EqualTo(endpointBefore.x).Within(0.01f));
                Assert.That(updatedHead.rectTransform.anchoredPosition.y, Is.EqualTo(endpointBefore.y).Within(0.01f));
            }
            finally
            {
                if (visual != null)
                    visual.CancelTargetFocus();
                Object.DestroyImmediate(cardObject);
                Object.DestroyImmediate(handObject);
                Object.DestroyImmediate(presenterObject);
                Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
            }
        }
    }

    /// <summary>验证无关队首命令普通失败时也会结束当前聚焦拖拽，并精确清理 Tween、箭头与高亮入口。</summary>
    [Test]
    public void TargetFocus_UnrelatedHeadExecutionFailure_CancelsActivePresentation()
    {
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject cardObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("TargetFocusHeadFailurePresenter");
        using (var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1))
        {
            HandCardVisual visual = null;
            try
            {
                zones.Draw(1);
                HandCardContainer container = handObject.GetComponent<HandCardContainer>();
                BattleParticipantPresenter presenter =
                    presenterObject.AddComponent<BattleParticipantPresenter>();
                visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, zones.Hand[0], canvasGroup);
                visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 8f, 0));
                visual.PlayTargetFocus(
                    visual.GetScreenCenter() + new Vector2(180f, 160f),
                    focusScale: 1.08f,
                    duration: 0.2f,
                    Ease.OutCubic,
                    breathScale: 1.025f,
                    breathDuration: 0.55f);
                Tween transition = typeof(HandCardVisual).GetField(
                    "_targetFocusTransitionTween",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(visual) as Tween;
                Assert.That(transition, Is.Not.Null);
                Assert.That(transition.IsActive(), Is.True);

                var serializedContainer = new SerializedObject(container);
                BattleTargetingArrowView arrow = serializedContainer
                    .FindProperty("_targetingArrow")
                    .objectReferenceValue as BattleTargetingArrowView;
                arrow.Show(visual.GetScreenCenter(), new Vector2(900f, 560f));
                typeof(HandCardContainer).GetField(
                    "_participantPresenter",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
                typeof(HandCardContainer).GetField(
                    "_draggingCard",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, visual);
                typeof(HandCardContainer).GetField(
                    "_dragPhase",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                    container,
                    HandCardDragPhase.EnemyTargeting);

                MethodInfo lifecycleMethod = typeof(HandCardContainer).GetMethod(
                    "HandleCommandLifecycle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lifecycleMethod, Is.Not.Null);
                lifecycleMethod.Invoke(container, new object[] { CreateExecutionFailedLifecycle() });

                Assert.That(transition.IsActive(), Is.False);
                Assert.That(arrow.IsVisible, Is.False);
                Assert.That(
                    typeof(HandCardContainer).GetField(
                        "_draggingCard",
                        BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container),
                    Is.Null);
                Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
            }
            finally
            {
                if (visual != null)
                    visual.CancelTargetFocus();
                Object.DestroyImmediate(cardObject);
                Object.DestroyImmediate(handObject);
                Object.DestroyImmediate(presenterObject);
            }
        }
    }


    /// <summary>验证费用变化把 EnemyTargeting 降级为 VisualOnly 时保留拖拽，但退出聚焦并恢复普通拖拽姿态。</summary>
    [Test]
    public void TargetFocus_WhenInteractionDowngradesToVisualOnly_CancelsFocusAndPreservesDrag()
    {
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject cardObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("TargetFocusVisualOnlyPresenter");
        var combatants = new BattleCombatantsData();
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1);
        BattleCommandQueue queue = null;
        HandCardVisual visual = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
            cfg.Tables tables = CreateTargetFocusTables(cardCost: 4);
            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones,
            };
            queue = BattleCommandQueueTestFactory.Create(
                combatants,
                new ImmediateBattleCommandPresentation(),
                playerZones,
                enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
                initialHandCount: 1,
                tables: tables);
            queue.SubmitRegistered(new StartBattleCommand());
            BattleTurnData turnBefore = queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = zones.Layout.CurrentValue;

            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            Vector3 baseScale = Vector3.one * 0.36f;
            visual.Initialize(baseScale, zones.Hand[0], canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 8f, 0));
            visual.PlayTargetFocus(
                visual.GetScreenCenter() + new Vector2(180f, 160f),
                focusScale: 1.08f,
                duration: 0.2f,
                Ease.OutCubic,
                breathScale: 1.025f,
                breathDuration: 0.55f);
            Tween transition = typeof(HandCardVisual).GetField(
                "_targetFocusTransitionTween",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(visual) as Tween;
            Assert.That(transition, Is.Not.Null);
            transition.Goto(0.1f, andPlay: false);

            var serializedContainer = new SerializedObject(container);
            BattleTargetingArrowView arrow = serializedContainer
                .FindProperty("_targetingArrow")
                .objectReferenceValue as BattleTargetingArrowView;
            arrow.Show(visual.GetScreenCenter(), new Vector2(900f, 560f));
            typeof(HandCardContainer).GetField(
                "_participantPresenter",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
            typeof(HandCardContainer).GetField(
                "_player",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, player);
            typeof(HandCardContainer).GetField(
                "_cardZones",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, zones);
            typeof(HandCardContainer).GetField(
                "_commandQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, queue);
            typeof(HandCardContainer).GetField(
                "_cardPlayRules",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                new BattleCardPlayRules(
                    combatants,
                    playerZones,
                    new[] { enemy.Id },
                    tables));
            typeof(HandCardContainer).GetField(
                "_draggingCard",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, visual);
            typeof(HandCardContainer).GetField(
                "_dragPhase",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                HandCardDragPhase.EnemyTargeting);

            MethodInfo refreshMethod = typeof(HandCardContainer).GetMethod(
                "RefreshActiveDragFromCurrentFacts",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshMethod, Is.Not.Null);
            refreshMethod.Invoke(container, new object[] { false });

            Assert.That(transition.IsActive(), Is.False);
            Assert.That(arrow.IsVisible, Is.False);
            Assert.That(
                typeof(HandCardContainer).GetField(
                    "_draggingCard",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container),
                Is.SameAs(visual));
            Assert.That(
                typeof(HandCardContainer).GetField(
                    "_dragPhase",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container),
                Is.EqualTo(HandCardDragPhase.Dragging));
            Assert.That(
                Mathf.DeltaAngle(visual.CardContent.localEulerAngles.z, 0f),
                Is.Zero.Within(0.01f));
            Assert.That(visual.CardContent.localScale.x, Is.EqualTo(baseScale.x).Within(0.001f));
            Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
        }
        finally
        {
            if (visual != null)
                visual.CancelTargetFocus();
            Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(handObject);
            Object.DestroyImmediate(presenterObject);
            queue?.Dispose();
            zones.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证有效 Enemy 释放在权威卡区同步移除牌面前，先终止聚焦 Tween，再进入唯一 Queue 提交入口。</summary>
    [Test]
    public void TargetFocus_ValidEnemyRelease_CancelsFocusBeforeAuthoritativeCardRemoval()
    {
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject cardObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("TargetFocusValidReleasePresenter");
        var cameraObject = new GameObject("TargetFocusValidReleaseCamera", typeof(Camera));
        var coordinator = new BattleCommandSubmissionCoordinator();
        var commandPresentation = new ControllableBattleCommandPresentation();
        BattleSession session = null;
        BattleCommandQueue queue = null;
        Sprite targetSprite = null;
        Texture2D targetTexture = null;
        HandCardVisual visual = null;
        var presentationObjects = new List<GameObject>();
        try
        {
            cfg.Tables tables = CreateTargetFocusTables(cardCost: 0);
            session = BattleSession.FromConfig(
                tables,
                new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
            PlayerCombatantData player = null;
            foreach (CombatantData combatant in session.Combatants.All.Values)
            {
                if (combatant is PlayerCombatantData candidate)
                    player = candidate;
            }

            Assert.That(player, Is.Not.Null);
            queue = new BattleCommandQueue(
                session.Combatants,
                new Dictionary<CombatantId, BattleCardZonesData>
                {
                    [player.Id] = session.CardZones,
                },
                session.EnemyCombatantIdsInEncounterOrder,
                session.EnemyIntents,
                tables,
                energyPerRound: 3,
                initialHandCount: 1,
                commandPresentation,
                coordinator);
            var startCommand = new StartBattleCommand();
            coordinator.PreRegister(startCommand);
            Assert.That(queue.Submit(startCommand).Accepted, Is.True);
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
            CardInstanceId cardId = session.CardZones.Hand[0];

            Camera camera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            targetTexture = new Texture2D(2, 2);
            targetTexture.Apply();
            targetSprite = Sprite.Create(
                targetTexture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            typeof(BattleParticipantPresenter).GetField(
                "_session",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, session);
            GameObject enemyWorld = null;
            foreach (KeyValuePair<CombatantId, CombatantData> entry in session.Combatants.All)
            {
                var world = new GameObject($"TargetFocusWorld_{entry.Key.Value}", typeof(SpriteRenderer));
                var hudObject = new GameObject($"TargetFocusHud_{entry.Key.Value}");
                ParticipantHudView hud = hudObject.AddComponent<ParticipantHudView>();
                presentationObjects.Add(world);
                presentationObjects.Add(hudObject);
                world.GetComponent<SpriteRenderer>().sprite = targetSprite;
                world.transform.position = entry.Value is EnemyCombatantData
                    ? Vector3.zero
                    : new Vector3(-4f, 0f, 0f);
                if (entry.Value is EnemyCombatantData)
                    enemyWorld = world;
                presenter.RegisterParticipantView(entry.Key, world);
                presenter.RegisterParticipantHud(entry.Key, hud);
            }

            Assert.That(enemyWorld, Is.Not.Null);
            Assert.That(presenter.IsPresentationReady, Is.True);
            CombatantId enemyId = session.EnemyCombatantIdsInEncounterOrder[0];
            presenter.BeginTargetSelection(new[] { enemyId });
            Vector2 enemyScreenPosition = camera.WorldToScreenPoint(enemyWorld.transform.position);
            Assert.That(presenter.UpdateTargetSelection(enemyScreenPosition), Is.EqualTo(enemyId));

            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 8f, 0));
            visual.PlayTargetFocus(
                visual.GetScreenCenter() + new Vector2(180f, 160f),
                focusScale: 1.08f,
                duration: 0.2f,
                Ease.OutCubic,
                breathScale: 1.025f,
                breathDuration: 0.55f);
            Tween transition = typeof(HandCardVisual).GetField(
                "_targetFocusTransitionTween",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(visual) as Tween;
            Assert.That(transition, Is.Not.Null);
            Assert.That(transition.IsActive(), Is.True);

            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = session.CardZones,
            };
            typeof(HandCardContainer).GetField(
                "_player",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, player);
            typeof(HandCardContainer).GetField(
                "_cardZones",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, session.CardZones);
            typeof(HandCardContainer).GetField(
                "_commandQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, queue);
            typeof(HandCardContainer).GetField(
                "_commandCoordinator",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, coordinator);
            typeof(HandCardContainer).GetField(
                "_participantPresenter",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
            typeof(HandCardContainer).GetField(
                "_cardPlayRules",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                new BattleCardPlayRules(
                    session.Combatants,
                    playerZones,
                    session.EnemyCombatantIdsInEncounterOrder,
                    tables));
            typeof(HandCardContainer).GetField(
                "playLineY",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, -10000f);
            var cards = typeof(HandCardContainer).GetField(
                "_cards",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container)
                as List<HandCardVisual>;
            Assert.That(cards, Is.Not.Null);
            cards.Add(visual);
            MethodInfo refreshPresentation = typeof(HandCardContainer).GetMethod(
                "RefreshCardPlayPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshPresentation, Is.Not.Null);
            refreshPresentation.Invoke(container, null);
            Assert.That(canvasGroup.interactable, Is.True);
            Assert.That(canvasGroup.blocksRaycasts, Is.True);
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
            cards.Remove(visual);
            commandPresentation.CompleteNext();
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
            typeof(HandCardContainer).GetField(
                "_draggingCard",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, visual);
            typeof(HandCardContainer).GetField(
                "_dragPhase",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                HandCardDragPhase.EnemyTargeting);

            var eventData = new UnityEngine.EventSystems.PointerEventData(eventSystem: null)
            {
                position = enemyScreenPosition,
            };
            container.HandleEndDrag(visual, eventData);

            Assert.That(transition.IsActive(), Is.False);
            Assert.That(session.CardZones.Hand, Is.Empty);
            Assert.That(session.CardZones.DiscardPile, Has.Count.EqualTo(1));
            Assert.That(session.CardZones.DiscardPile[0], Is.EqualTo(cardId));
            Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
            commandPresentation.CompleteNext();
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
        }
        finally
        {
            if (visual != null)
                visual.CancelTargetFocus();
            Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(handObject);
            Object.DestroyImmediate(presenterObject);
            Object.DestroyImmediate(cameraObject);
            foreach (GameObject presentationObject in presentationObjects)
            {
                if (presentationObject != null)
                    Object.DestroyImmediate(presentationObject);
            }
            if (targetSprite != null)
                Object.DestroyImmediate(targetSprite);
            if (targetTexture != null)
                Object.DestroyImmediate(targetTexture);
            queue?.Dispose();
            session?.Dispose();
            coordinator.Dispose();
        }
    }

    /// <summary>验证活动拖拽被阶段或对象生命周期取消时，精确清理聚焦 Tween、箭头与本地拖拽态。</summary>
    [Test]
    public void TargetFocus_CancelActiveDrag_KillsOwnedTweenAndClearsPresentation()
    {
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject cardObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("TargetFocusCancelPresenter");
        using (var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1))
        {
            HandCardVisual visual = null;
            try
            {
                zones.Draw(1);
                HandCardContainer container = handObject.GetComponent<HandCardContainer>();
                BattleParticipantPresenter presenter =
                    presenterObject.AddComponent<BattleParticipantPresenter>();
                visual = cardObject.GetComponent<HandCardVisual>();
                CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
                visual.Initialize(Vector3.one * 0.36f, zones.Hand[0], canvasGroup);
                visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 8f, 0));
                visual.PlayTargetFocus(
                    visual.GetScreenCenter() + new Vector2(180f, 160f),
                    focusScale: 1.08f,
                    duration: 0.2f,
                    Ease.OutCubic,
                    breathScale: 1.025f,
                    breathDuration: 0.55f);
                Tween transition = typeof(HandCardVisual).GetField(
                    "_targetFocusTransitionTween",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(visual) as Tween;
                Assert.That(transition, Is.Not.Null);
                Assert.That(transition.IsActive(), Is.True);

                var serializedContainer = new SerializedObject(container);
                BattleTargetingArrowView arrow = serializedContainer
                    .FindProperty("_targetingArrow")
                    .objectReferenceValue as BattleTargetingArrowView;
                arrow.Show(visual.GetScreenCenter(), new Vector2(900f, 560f));
                typeof(HandCardContainer).GetField(
                    "_participantPresenter",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
                typeof(HandCardContainer).GetField(
                    "_draggingCard",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, visual);
                typeof(HandCardContainer).GetField(
                    "_dragPhase",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                    container,
                    HandCardDragPhase.EnemyTargeting);

                MethodInfo cancelMethod = typeof(HandCardContainer).GetMethod(
                    "CancelActiveDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(cancelMethod, Is.Not.Null);
                cancelMethod.Invoke(container, new object[] { false });

                Assert.That(transition.IsActive(), Is.False);
                Assert.That(arrow.IsVisible, Is.False);
                Assert.That(
                    typeof(HandCardContainer).GetField(
                        "_draggingCard",
                        BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container),
                    Is.Null);
                Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
            }
            finally
            {
                if (visual != null)
                    visual.CancelTargetFocus();
                Object.DestroyImmediate(cardObject);
                Object.DestroyImmediate(handObject);
                Object.DestroyImmediate(presenterObject);
            }
        }
    }

    /// <summary>验证 fatal 结算后仍从尚未播放死亡过渡的世界 View 解析 PlayCard 前奏目标锚点。</summary>
    [Test]
    public void PresentationAnchor_FatalTargetStillResolvesBeforeDeathTransition()
    {
        cfg.Tables tables = CreateTargetFocusTables(cardCost: 0);
        using BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var presenterObject = new GameObject("FatalPresentationAnchorPresenter");
        var cameraObject = new GameObject("FatalPresentationAnchorCamera", typeof(Camera));
        var enemyWorld = new GameObject("FatalPresentationAnchorEnemy", typeof(SpriteRenderer));
        Texture2D texture = null;
        Sprite sprite = null;
        try
        {
            PlayerCombatantData player = null;
            EnemyCombatantData enemy = null;
            foreach (CombatantData combatant in session.Combatants.All.Values)
            {
                if (combatant is PlayerCombatantData playerCandidate)
                    player = playerCandidate;
                else if (combatant is EnemyCombatantData enemyCandidate)
                    enemy = enemyCandidate;
            }

            Assert.That(player, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);
            BattleEffectStateTestDriver.ApplyDamage(
                session.Combatants,
                player.Id,
                enemy.Id,
                configuredValue: enemy.CurrentHealth);
            Assert.That(enemy.CurrentHealth, Is.Zero);

            Camera camera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            texture = new Texture2D(2, 2);
            texture.Apply();
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);
            SpriteRenderer renderer = enemyWorld.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            enemyWorld.transform.position = new Vector3(1.5f, 0.75f, 0f);

            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            typeof(BattleParticipantPresenter).GetField(
                "_session",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, session);
            presenter.RegisterParticipantView(enemy.Id, enemyWorld);
            Vector2 expected = camera.WorldToScreenPoint(renderer.bounds.center);

            bool resolved = presenter.TryGetPresentationScreenAnchor(enemy.Id, out Vector2 actual);

            Assert.That(resolved, Is.True);
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(enemyWorld.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(enemyWorld);
            Object.DestroyImmediate(presenterObject);
            Object.DestroyImmediate(cameraObject);
            if (sprite != null)
                Object.DestroyImmediate(sprite);
            if (texture != null)
                Object.DestroyImmediate(texture);
        }
    }

    /// <summary>通过既有内部工厂构造一条冻结的普通失败反馈，不公开新的生产测试 seam。</summary>
    private static BattleCommandLifecycleEvent CreateExecutionFailedLifecycle()
    {
        var handle = (BattleCommandHandle)System.Activator.CreateInstance(
            typeof(BattleCommandHandle),
            nonPublic: true);
        var command = new StartBattleCommand();
        MethodInfo factory = typeof(BattleCommandLifecycleEvent).GetMethod(
            "FromExecution",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(factory, Is.Not.Null);
        return (BattleCommandLifecycleEvent)factory.Invoke(
            null,
            new object[]
            {
                handle,
                1L,
                command,
                BattleCommandExecutionFailureReason.BattleAlreadyStarted,
                System.Array.Empty<BattleSettlementRecord>(),
            });
    }

    /// <summary>创建一张费用高于当前能量的 Enemy 牌所需最小静态表，不复制任何生产公式。</summary>
    private static cfg.Tables CreateTargetFocusTables(int cardCost)
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test.name\"," +
                "\"view_prefab_key\":\"\",\"max_health\":30," +
                "\"base_strength\":0,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0}]"),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test.name\"," +
                "\"max_health\":20,\"base_strength\":0,\"view_prefab_key\":\"\"," +
                "\"behavior_group_id\":6001}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3001]}]"),
            ["battle_tbcard"] = new JArray
            {
                new JObject
                {
                    ["id"] = 3001,
                    ["external_key"] = "TEST_HAND_CARD_TARGET_FOCUS_3001",
                    ["catalog_snapshot_key"] = "test-fixture",
                    ["name_i18n_key"] = "battle.card.test.name",
                    ["description_i18n_key"] = "battle.card.test.description",
                    ["upgraded_description_i18n_key"] = "battle.card.test.description",
                    ["card_type"] = (int)cfg.battle.CardType.Attack,
                    ["rarity"] = (int)cfg.battle.CardRarity.Basic,
                    ["cost"] = cardCost,
                    ["cost_kind"] = (int)cfg.battle.CardCostKind.Fixed,
                    ["upgraded_cost"] = cardCost,
                    ["target_rule"] = (int)cfg.battle.TargetRule.Enemy,
                    ["play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
                    ["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
                    ["has_upgrade"] = false,
                    ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                    ["program_id"] = (int)cfg.battle.MachineGunnerProgramId.None,
                    ["is_innate"] = false,
                    ["effect_bindings"] = new JArray(),
                    ["illustration_key"] = string.Empty,
                },
            },
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4999,\"effect_type\":1,\"attribute\":0,\"value\":1}]"),
            ["battle_tbencounter"] = JArray.Parse(
                "[{\"id\":5001,\"enemy_template_ids\":[2001]}]"),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1," +
                "\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0," +
                "\"max_consecutive\":0}]"),
        };
        return new cfg.Tables(tableName => data[tableName]);
    }
}
