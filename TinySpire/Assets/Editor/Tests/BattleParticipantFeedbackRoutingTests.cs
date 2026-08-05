using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BattleParticipantFeedbackRoutingTests
{
    private const string HudPrefabPath = "Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab";
    private const string TurnHudPrefabPath = "Assets/Prefabs/UI/Battle/BattleTurnHud.prefab";
    private const string HandPrefabPath = "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab";

    /// <summary>确认 Presenter 未就绪只关闭系统指针入口，直接 Queue 提交仍保留原序号与生命周期。</summary>
    [Test]
    public void DirectQueueSubmit_WhenPresentationIsNotReady_PreservesAuthoritySemantics()
    {
        cfg.Tables tables = CreateReadinessSessionTables();
        using BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        var presenterObject = new GameObject("BattleParticipantDirectQueuePresenter");
        var coordinator = new BattleCommandSubmissionCoordinator();
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        try
        {
            PlayerCombatantData player = null;
            foreach (CombatantData combatant in session.Combatants.All.Values)
            {
                if (combatant is PlayerCombatantData candidate)
                    player = candidate;
            }

            Assert.That(player, Is.Not.Null);
            presenter.Construct(session, CreateConfigService(tables), localization);
            Assert.That(presenter.IsPresentationReady, Is.False);
            using IDisposable subscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
            using var queue = new BattleCommandQueue(
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
                new ImmediateBattleCommandPresentation(),
                coordinator);
            var command = new StartBattleCommand();
            BattleCommandHandle handle = coordinator.PreRegister(command);

            BattleCommandSubmissionResult result = queue.Submit(command);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.AuthoritySequence, Is.EqualTo(1));
            Assert.That(lifecycles, Has.Count.EqualTo(2));
            Assert.That(lifecycles[0].Handle, Is.SameAs(handle));
            Assert.That(lifecycles[0].Stage, Is.EqualTo(BattleCommandLifecycleStage.Queued));
            Assert.That(lifecycles[1].Handle, Is.SameAs(handle));
            Assert.That(
                lifecycles[1].Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
            Assert.That(presenter.IsPresentationReady, Is.False);
        }
        finally
        {
            coordinator.Dispose();
            Object.DestroyImmediate(presenterObject);
            localization.Dispose();
        }
    }

    /// <summary>确认失败或空结果的参与者实例句柄由 Presenter 精确释放，重复清理不会二次释放。</summary>
    [Test]
    public void ReleaseFailedViewHandle_ValidCompletedHandle_ReleasesExactlyOnce()
    {
        AsyncOperationHandle<GameObject> handle =
            Addressables.ResourceManager.CreateCompletedOperation<GameObject>(
                result: null,
                errorMsg: string.Empty);

        Assert.That(handle.IsValid(), Is.True);

        BattleParticipantPresenter.ReleaseFailedViewHandle(handle);

        Assert.That(handle.IsValid(), Is.False);
        Assert.DoesNotThrow(() => BattleParticipantPresenter.ReleaseFailedViewHandle(handle));
    }

    /// <summary>确认默认 Addressables 等待边界在空结果时先释放句柄，再把加载失败向活 Scope 抛出。</summary>
    [UnityTest]
    public IEnumerator AwaitAddressableViewAsync_NullResult_ReleasesBeforeRethrow()
    {
        AsyncOperationHandle<GameObject> handle =
            Addressables.ResourceManager.CreateCompletedOperation<GameObject>(
                result: null,
                errorMsg: string.Empty);
        Exception observedException = null;

        yield return BattleParticipantPresenter.AwaitAddressableViewAsync(handle)
            .ToCoroutine(exceptionHandler: exception => observedException = exception);

        Assert.That(observedException, Is.TypeOf<InvalidOperationException>());
        Assert.That(handle.IsValid(), Is.False);
    }

    /// <summary>确认旧 Presenter 已销毁时不会开始任何后续参与者 Addressables 加载。</summary>
    [UnityTest]
    public IEnumerator CreateViewsAsync_WhenPresenterDisposedBeforeLoad_ReturnsBeforeAnyLoad()
    {
        BattleSession session = BattleSession.FromConfig(
            CreateReadinessSessionTables(),
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        var presenterObject = new GameObject("BattleParticipantDestroyedBeforeLoadPresenter");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        try
        {
            presenter.Construct(session, new ConfigService(), localization);
            presenter.DisposePresentationForTesting();

            yield return presenter.CreateViewsAsync().ToCoroutine();

            Assert.That(presenter.IsPresentationReady, Is.False);
        }
        finally
        {
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            localization.Dispose();
            session.Dispose();
        }
    }

    /// <summary>确认首个加载挂起时销毁旧 Presenter，只收口该迟到实例且不再启动敌人加载。</summary>
    [UnityTest]
    public IEnumerator CreateViewsAsync_DestroyedDuringPlayerLoad_ReleasesLateViewAndStopsSequence()
    {
        cfg.Tables tables = CreateReadinessSessionTables();
        BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        var presenterObject = new GameObject("BattleParticipantDestroyedDuringLoadPresenter");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        var pendingView = new UniTaskCompletionSource<GameObject>();
        var instantiateCount = 0;
        var releaseCount = 0;
        GameObject lateView = null;
        try
        {
            presenter.Construct(session, CreateConfigService(tables), localization);
            presenter.ConfigureViewResourceBoundaryForTesting(
                (address, anchor) =>
                {
                    instantiateCount++;
                    return pendingView.Task;
                },
                view =>
                {
                    releaseCount++;
                    Object.DestroyImmediate(view);
                },
                hudObject => Object.DestroyImmediate(hudObject));

            UniTask creation = presenter.CreateViewsAsync();

            Assert.That(instantiateCount, Is.EqualTo(1));
            Assert.That(presenter.IsPresentationReady, Is.False);
            Assert.That(releaseCount, Is.Zero);

            yield return null;

            Assert.That(instantiateCount, Is.EqualTo(1));
            Assert.That(presenter.IsPresentationReady, Is.False);
            Assert.That(releaseCount, Is.Zero);

            yield return null;

            Assert.That(instantiateCount, Is.EqualTo(1));
            Assert.That(presenter.IsPresentationReady, Is.False);
            Assert.That(releaseCount, Is.Zero);

            presenter.DisposePresentationForTesting();
            Assert.That(presenter.IsPresentationReady, Is.False);
            lateView = new GameObject(
                "BattleParticipantLatePlayerView",
                typeof(SpriteRenderer));
            pendingView.TrySetResult(lateView);

            yield return creation.ToCoroutine();

            Assert.That(instantiateCount, Is.EqualTo(1));
            Assert.That(releaseCount, Is.EqualTo(1));
            Assert.That(lateView == null, Is.True);
            Assert.That(presenter.IsPresentationReady, Is.False);
        }
        finally
        {
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (lateView != null)
                Object.DestroyImmediate(lateView);
            localization.Dispose();
            session.Dispose();
        }
    }

    /// <summary>确认完整成功加载只在全部世界 View/HUD 就绪后解锁，并在销毁时把每个表现对象精确清理一次。</summary>
    [UnityTest]
    public IEnumerator CreateViewsAsync_SucceedsThenDisposes_TransitionsReadinessAndCleansExactlyOnce()
    {
        cfg.Tables tables = CreateReadinessSessionTables();
        BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);
        var presenterObject = new GameObject("BattleParticipantSuccessfulLoadPresenter");
        var canvasObject = new GameObject(
            "BattleParticipantSuccessfulLoadCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var playerAnchorObject = new GameObject("BattleParticipantSuccessfulLoadPlayerAnchor");
        var enemyAnchorObject = new GameObject("BattleParticipantSuccessfulLoadEnemyAnchor");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        var createdViews = new List<GameObject>();
        var requestedAddresses = new List<string>();
        int expectedParticipantCount = session.Combatants.All.Count;
        var instantiateCount = 0;
        var releaseCount = 0;
        var destroyHudCount = 0;
        try
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(hudPrefab, Is.Not.Null);
            SetPrivateField(presenter, "_playerAnchor", playerAnchorObject.transform);
            SetPrivateField(presenter, "_enemyAnchor", enemyAnchorObject.transform);
            SetPrivateField(presenter, "_hudCanvas", canvasObject.GetComponent<Canvas>());
            SetPrivateField(
                presenter,
                "_hudViewPrefab",
                hudPrefab.GetComponent<ParticipantHudView>());
            presenter.Construct(session, CreateConfigService(tables), localization);
            presenter.ConfigureViewResourceBoundaryForTesting(
                (address, anchor) =>
                {
                    requestedAddresses.Add(address);
                    instantiateCount++;
                    var view = new GameObject(
                        $"BattleParticipantSuccessfulView_{instantiateCount}",
                        typeof(SpriteRenderer));
                    view.transform.SetParent(anchor, worldPositionStays: false);
                    createdViews.Add(view);
                    return UniTask.FromResult(view);
                },
                view =>
                {
                    releaseCount++;
                    Object.DestroyImmediate(view);
                },
                hudObject =>
                {
                    destroyHudCount++;
                    Object.DestroyImmediate(hudObject);
                });

            Assert.That(presenter.IsPresentationReady, Is.False);

            yield return presenter.CreateViewsAsync().ToCoroutine();

            Assert.That(instantiateCount, Is.EqualTo(expectedParticipantCount));
            Assert.That(
                requestedAddresses,
                Is.EqualTo(new[]
                {
                    "character-view/pfb_char_player",
                    "character-view/pfb_char_enemy"
                }));
            Assert.That(createdViews, Has.Count.EqualTo(expectedParticipantCount));
            Assert.That(canvasObject.transform.childCount, Is.EqualTo(expectedParticipantCount));
            Assert.That(presenter.IsPresentationReady, Is.True);

            presenter.DisposePresentationForTesting();

            Assert.That(presenter.IsPresentationReady, Is.False);
            Assert.That(releaseCount, Is.EqualTo(expectedParticipantCount));
            Assert.That(destroyHudCount, Is.EqualTo(expectedParticipantCount));
            Assert.That(canvasObject.transform.childCount, Is.Zero);
            foreach (GameObject view in createdViews)
                Assert.That(view == null, Is.True);

            presenter.DisposePresentationForTesting();

            Assert.That(releaseCount, Is.EqualTo(expectedParticipantCount));
            Assert.That(destroyHudCount, Is.EqualTo(expectedParticipantCount));
        }
        finally
        {
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            foreach (GameObject view in createdViews)
            {
                if (view != null)
                    Object.DestroyImmediate(view);
            }
            if (playerAnchorObject != null)
                Object.DestroyImmediate(playerAnchorObject);
            if (enemyAnchorObject != null)
                Object.DestroyImmediate(enemyAnchorObject);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            localization.Dispose();
            session.Dispose();
        }
    }

    /// <summary>确认活 Scope 的敌人加载失败会清理已创建玩家 View/HUD，并继续向上报告失败。</summary>
    [UnityTest]
    public IEnumerator CreateViewsAsync_EnemyLoadFails_CleansPartialPresentationAndRethrows()
    {
        cfg.Tables tables = CreateReadinessSessionTables();
        BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);
        var presenterObject = new GameObject("BattleParticipantPartialFailurePresenter");
        var canvasObject = new GameObject(
            "BattleParticipantPartialFailureCanvas",
            typeof(RectTransform),
            typeof(Canvas));
        var playerAnchorObject = new GameObject("BattleParticipantPartialFailurePlayerAnchor");
        var enemyAnchorObject = new GameObject("BattleParticipantPartialFailureEnemyAnchor");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        var instantiateCount = 0;
        var releaseCount = 0;
        GameObject playerView = null;
        Exception observedException = null;
        try
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            SetPrivateField(presenter, "_playerAnchor", playerAnchorObject.transform);
            SetPrivateField(presenter, "_enemyAnchor", enemyAnchorObject.transform);
            SetPrivateField(presenter, "_hudCanvas", canvasObject.GetComponent<Canvas>());
            SetPrivateField(
                presenter,
                "_hudViewPrefab",
                hudPrefab.GetComponent<ParticipantHudView>());
            presenter.Construct(session, CreateConfigService(tables), localization);
            presenter.ConfigureViewResourceBoundaryForTesting(
                (address, anchor) =>
                {
                    instantiateCount++;
                    if (instantiateCount == 1)
                    {
                        playerView = new GameObject(
                            "BattleParticipantPartialFailurePlayerView",
                            typeof(SpriteRenderer));
                        playerView.transform.SetParent(anchor, worldPositionStays: false);
                        return UniTask.FromResult(playerView);
                    }

                    return UniTask.FromException<GameObject>(
                        new InvalidOperationException("Expected enemy view load failure."));
                },
                view =>
                {
                    releaseCount++;
                    Object.DestroyImmediate(view);
                },
                hudObject => Object.DestroyImmediate(hudObject));

            yield return presenter.CreateViewsAsync()
                .ToCoroutine(exception => observedException = exception);
            yield return null;

            Assert.That(observedException, Is.TypeOf<InvalidOperationException>());
            Assert.That(instantiateCount, Is.EqualTo(2));
            Assert.That(releaseCount, Is.EqualTo(1));
            Assert.That(playerView == null, Is.True);
            Assert.That(canvasObject.transform.childCount, Is.Zero);
            Assert.That(presenter.IsPresentationReady, Is.False);
        }
        finally
        {
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (playerView != null)
                Object.DestroyImmediate(playerView);
            if (playerAnchorObject != null)
                Object.DestroyImmediate(playerAnchorObject);
            if (enemyAnchorObject != null)
                Object.DestroyImmediate(enemyAnchorObject);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            localization.Dispose();
            session.Dispose();
        }
    }

    /// <summary>确认只在当前 Session 的每个参与者世界 View 与 HUD 都完成唯一映射且仍存活时报告表现就绪。</summary>
    [Test]
    public void IsPresentationReady_RequiresEveryCurrentParticipantViewAndHudMapping()
    {
        BattleSession session = BattleSession.FromConfig(
            CreateReadinessSessionTables(),
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        var presenterObject = new GameObject("BattleParticipantReadinessPresenter");
        var firstWorldObject = new GameObject("BattleParticipantReadinessFirstWorld");
        var secondWorldObject = new GameObject("BattleParticipantReadinessSecondWorld");
        var firstHudObject = new GameObject("BattleParticipantReadinessFirstHud");
        var secondHudObject = new GameObject("BattleParticipantReadinessSecondHud");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        ParticipantHudView firstHud = firstHudObject.AddComponent<ParticipantHudView>();
        ParticipantHudView secondHud = secondHudObject.AddComponent<ParticipantHudView>();
        try
        {
            presenter.Construct(session, new ConfigService(), localization);
            var participantIds = new List<CombatantId>(session.Combatants.All.Keys);

            Assert.That(participantIds.Count, Is.EqualTo(2));
            Assert.That(presenter.IsPresentationReady, Is.False);

            presenter.RegisterParticipantView(participantIds[0], firstWorldObject);
            presenter.RegisterParticipantHud(participantIds[0], firstHud);

            Assert.That(presenter.IsPresentationReady, Is.False);

            presenter.RegisterParticipantHud(participantIds[1], secondHud);

            Assert.That(presenter.IsPresentationReady, Is.False);

            presenter.RegisterParticipantView(participantIds[1], secondWorldObject);

            Assert.That(presenter.IsPresentationReady, Is.True);

            Object.DestroyImmediate(secondWorldObject);
            secondWorldObject = null;

            Assert.That(presenter.IsPresentationReady, Is.False);
        }
        finally
        {
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (firstWorldObject != null)
                Object.DestroyImmediate(firstWorldObject);
            if (secondWorldObject != null)
                Object.DestroyImmediate(secondWorldObject);
            if (firstHudObject != null)
                Object.DestroyImmediate(firstHudObject);
            if (secondHudObject != null)
                Object.DestroyImmediate(secondHudObject);
            localization.Dispose();
            session.Dispose();
        }
    }

    /// <summary>确认世界 View 已全部映射时仍必须等待最后一个 HUD，且已就绪 HUD 销毁后立即重新关闭。</summary>
    [Test]
    public void IsPresentationReady_AllWorldViewsWithoutEveryHud_RemainsFalseAndDestroyedHudRelocks()
    {
        BattleSession session = BattleSession.FromConfig(
            CreateReadinessSessionTables(),
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        var presenterObject = new GameObject("BattleParticipantHudReadinessPresenter");
        var firstWorldObject = new GameObject("BattleParticipantHudReadinessFirstWorld");
        var secondWorldObject = new GameObject("BattleParticipantHudReadinessSecondWorld");
        var firstHudObject = new GameObject("BattleParticipantHudReadinessFirstHud");
        var secondHudObject = new GameObject("BattleParticipantHudReadinessSecondHud");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        ParticipantHudView firstHud = firstHudObject.AddComponent<ParticipantHudView>();
        ParticipantHudView secondHud = secondHudObject.AddComponent<ParticipantHudView>();
        try
        {
            presenter.Construct(session, new ConfigService(), localization);
            var participantIds = new List<CombatantId>(session.Combatants.All.Keys);

            Assert.That(participantIds.Count, Is.EqualTo(2));
            presenter.RegisterParticipantView(participantIds[0], firstWorldObject);
            presenter.RegisterParticipantView(participantIds[1], secondWorldObject);
            presenter.RegisterParticipantHud(participantIds[0], firstHud);

            Assert.That(presenter.IsPresentationReady, Is.False);

            presenter.RegisterParticipantHud(participantIds[1], secondHud);

            Assert.That(presenter.IsPresentationReady, Is.True);

            Object.DestroyImmediate(secondHudObject);
            secondHudObject = null;

            Assert.That(presenter.IsPresentationReady, Is.False);
        }
        finally
        {
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (firstWorldObject != null)
                Object.DestroyImmediate(firstWorldObject);
            if (secondWorldObject != null)
                Object.DestroyImmediate(secondWorldObject);
            if (firstHudObject != null)
                Object.DestroyImmediate(firstHudObject);
            if (secondHudObject != null)
                Object.DestroyImmediate(secondHudObject);
            localization.Dispose();
            session.Dispose();
        }
    }

    /// <summary>确认实际结束行动按钮随 Presenter 完整映射 false→true→false，门禁变化不写入 Queue 或 Turn。</summary>
    [Test]
    public void TurnHudView_PresentationReadiness_RefreshesConcreteEndActionButtonWithoutQueueWrite()
    {
        cfg.Tables tables = CreateReadinessSessionTables();
        using BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presenterObject = new GameObject("BattleParticipantTurnHudReadinessPresenter");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        GameObject turnHudObject = null;
        var presentationObjects = new List<GameObject>();
        try
        {
            ConfigService configs = CreateConfigService(tables);
            presenter.Construct(session, configs, localization);
            using BattleCommandQueue queue = CreateStartedReadinessQueue(
                session,
                tables,
                coordinator,
                out PlayerCombatantData player);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TurnHudPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            turnHudObject = Object.Instantiate(prefab);
            BattleTurnHudView turnHud = turnHudObject.GetComponent<BattleTurnHudView>();
            turnHud.Construct(session, configs, queue, coordinator, presenter);
            InvokePrivate(turnHud, "Start");
            Button endActionButton = GetPrivateField<Button>(turnHud, "_endActionButton");
            Image phaseBanner = GetPrivateField<Image>(turnHud, "_playerTurnBanner");

            Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(
                phaseBanner.gameObject.activeSelf,
                Is.False,
                "权威 Phase 刷新不得抢先显示只属于冻结表现步骤的横幅。");
            Assert.That(endActionButton.interactable, Is.False);
            endActionButton.onClick.Invoke();
            Assert.That(queue.Turn.CurrentValue.Players[player.Id].HasEndedAction, Is.False);
            Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);

            RegisterReadyPresentation(session, presenter, presentationObjects);
            InvokePrivate(turnHud, "Update");

            Assert.That(presenter.IsPresentationReady, Is.True);
            Assert.That(endActionButton.interactable, Is.True);
            Assert.That(queue.Turn.CurrentValue.Players[player.Id].HasEndedAction, Is.False);

            Object.DestroyImmediate(presentationObjects[presentationObjects.Count - 1]);
            presentationObjects.RemoveAt(presentationObjects.Count - 1);
            InvokePrivate(turnHud, "Update");

            Assert.That(presenter.IsPresentationReady, Is.False);
            Assert.That(endActionButton.interactable, Is.False);
            endActionButton.onClick.Invoke();
            Assert.That(queue.Turn.CurrentValue.Players[player.Id].HasEndedAction, Is.False);
            Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        }
        finally
        {
            if (turnHudObject != null)
                Object.DestroyImmediate(turnHudObject);
            DestroyPresentationObjects(presentationObjects);
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            coordinator.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>确认实际手牌射线与 BeginDrag 随 Presenter readiness 开关，映射失效立即取消拖拽且不写卡区。</summary>
    [Test]
    public void HandCardContainer_PresentationReadiness_GatesConcreteRaycastAndBeginDragWithoutZoneWrite()
    {
        cfg.Tables tables = CreateReadinessSessionTables();
        using BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        var localization = new LocalizationService();
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presenterObject = new GameObject("BattleParticipantHandReadinessPresenter");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        GameObject handObject = null;
        HandCardVisual card = null;
        var presentationObjects = new List<GameObject>();
        int activeTweenCountBefore = DG.Tweening.DOTween.TotalActiveTweens();
        try
        {
            ConfigService configs = CreateConfigService(tables);
            presenter.Construct(session, configs, localization);
            using BattleCommandQueue queue = CreateStartedReadinessQueue(
                session,
                tables,
                coordinator,
                out PlayerCombatantData player);
            GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            Assert.That(handPrefab, Is.Not.Null);
            handObject = Object.Instantiate(handPrefab);
            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            GameObject cardPrefab = GetPrivateField<GameObject>(container, "cardViewPrefab");
            Assert.That(cardPrefab, Is.Not.Null);
            card = Object.Instantiate(cardPrefab).GetComponent<HandCardVisual>();
            Assert.That(card, Is.Not.Null);
            CanvasGroup canvasGroup = card.CardContent.gameObject.AddComponent<CanvasGroup>();
            CardInstanceId cardId = session.CardZones.Hand[0];
            card.Initialize(Vector3.one, cardId, canvasGroup);
            card.SetBasePoseImmediately(new HandCardPose(Vector2.zero, 0f, 0));

            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = session.CardZones,
            };
            SetPrivateField(container, "_cardZones", session.CardZones);
            SetPrivateField(container, "_player", player);
            SetPrivateField(container, "_commandQueue", queue);
            SetPrivateField(container, "_participantPresenter", presenter);
            SetPrivateField(
                container,
                "_cardPlayRules",
                new BattleCardPlayRules(
                    session.Combatants,
                    playerZones,
                    session.EnemyCombatantIdsInEncounterOrder,
                    tables));
            GetPrivateField<List<HandCardVisual>>(container, "_cards").Add(card);

            InvokePrivate(container, "RefreshCardPlayPresentation");

            Image disabledOverlay = card.CardContent.Find("DisabledOverlay")?.GetComponent<Image>();
            Assert.That(disabledOverlay, Is.Not.Null);
            Assert.That(disabledOverlay.gameObject.activeSelf, Is.True);
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            container.HandleBeginDrag(card);
            Assert.That(GetPrivateField<HandCardVisual>(container, "_draggingCard"), Is.Null);
            Assert.That(session.CardZones.Hand, Is.EqualTo(new[] { cardId }));

            RegisterReadyPresentation(session, presenter, presentationObjects);
            InvokePrivate(container, "RefreshCardPlayPresentation");

            Assert.That(canvasGroup.interactable, Is.True);
            Assert.That(canvasGroup.blocksRaycasts, Is.True);
            Assert.That(disabledOverlay.gameObject.activeSelf, Is.False);
            container.HandleBeginDrag(card);
            Assert.That(GetPrivateField<HandCardVisual>(container, "_draggingCard"), Is.SameAs(card));
            Assert.That(
                GetPrivateField<HandCardDragPhase>(container, "_dragPhase"),
                Is.EqualTo(HandCardDragPhase.Dragging));

            Object.DestroyImmediate(presentationObjects[presentationObjects.Count - 1]);
            presentationObjects.RemoveAt(presentationObjects.Count - 1);
            container.HandleDrag(card, new PointerEventData(eventSystem: null));

            Assert.That(presenter.IsPresentationReady, Is.False);
            Assert.That(GetPrivateField<HandCardVisual>(container, "_draggingCard"), Is.Null);
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            Assert.That(disabledOverlay.gameObject.activeSelf, Is.True);
            Assert.That(session.CardZones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
            card.SetBasePoseImmediately(new HandCardPose(Vector2.zero, 0f, 0));
            Assert.That(
                DG.Tweening.DOTween.TotalActiveTweens(),
                Is.EqualTo(activeTweenCountBefore),
                "同帧取消并收口后不得留下等待下一次 PlayerLoop 的卡牌补间。");
        }
        finally
        {
            if (card != null)
            {
                DG.Tweening.DOTween.Kill(card.CardContent, complete: false);
                InvokePrivate(card, "OnDestroy");
                Object.DestroyImmediate(card.gameObject);
            }
            if (handObject != null)
                Object.DestroyImmediate(handObject);
            DestroyPresentationObjects(presentationObjects);
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            coordinator.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>确认 BeginDrag 只快进命中的入场牌一次，其他合法牌在表现期间仍可正常拖拽。</summary>
    [Test]
    public void HandCardContainer_BeginDrag_FastForwardsOnlyHitIncomingCardOnce()
    {
        cfg.Tables tables = CreateReadinessSessionTables();
        using BattleSession session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001));
        using var zones = new BattleCardZonesData(new[] { 3001, 3001 }, shuffleSeed: 8642);
        var localization = new LocalizationService();
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presenterObject = new GameObject("BattleParticipantIncomingCardPresenter");
        BattleParticipantPresenter presenter =
            presenterObject.AddComponent<BattleParticipantPresenter>();
        GameObject handObject = null;
        HandCardVisual incomingCard = null;
        HandCardVisual otherCard = null;
        BattleCommandPresentationTween incomingLease = null;
        var presentationObjects = new List<GameObject>();
        var parentTweenId = new object();
        try
        {
            PlayerCombatantData player = null;
            foreach (CombatantData combatant in session.Combatants.All.Values)
            {
                if (combatant is PlayerCombatantData candidate)
                    player = candidate;
            }

            Assert.That(player, Is.Not.Null);
            presenter.Construct(session, CreateConfigService(tables), localization);
            RegisterReadyPresentation(session, presenter, presentationObjects);
            using var queue = new BattleCommandQueue(
                session.Combatants,
                new Dictionary<CombatantId, BattleCardZonesData>
                {
                    [player.Id] = zones,
                },
                session.EnemyCombatantIdsInEncounterOrder,
                session.EnemyIntents,
                tables,
                energyPerRound: 3,
                initialHandCount: 2,
                new ImmediateBattleCommandPresentation(),
                coordinator);
            var startCommand = new StartBattleCommand();
            coordinator.PreRegister(startCommand);
            Assert.That(queue.Submit(startCommand).Accepted, Is.True);
            Assert.That(zones.Hand, Has.Count.EqualTo(2));

            GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            Assert.That(handPrefab, Is.Not.Null);
            handObject = Object.Instantiate(handPrefab);
            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            GameObject cardPrefab = GetPrivateField<GameObject>(container, "cardViewPrefab");
            incomingCard = Object.Instantiate(cardPrefab).GetComponent<HandCardVisual>();
            otherCard = Object.Instantiate(cardPrefab).GetComponent<HandCardVisual>();
            var incomingCanvasGroup = incomingCard.CardContent.gameObject.AddComponent<CanvasGroup>();
            var otherCanvasGroup = otherCard.CardContent.gameObject.AddComponent<CanvasGroup>();
            incomingCard.Initialize(Vector3.one, zones.Hand[0], incomingCanvasGroup);
            otherCard.Initialize(Vector3.one, zones.Hand[1], otherCanvasGroup);
            incomingCard.SetBasePoseImmediately(new HandCardPose(new Vector2(-80f, -260f), -3f, 0));
            otherCard.SetBasePoseImmediately(new HandCardPose(new Vector2(80f, -260f), 3f, 1));

            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones,
            };
            SetPrivateField(container, "_cardZones", zones);
            SetPrivateField(container, "_player", player);
            SetPrivateField(container, "_commandQueue", queue);
            SetPrivateField(container, "_participantPresenter", presenter);
            SetPrivateField(
                container,
                "_cardPlayRules",
                new BattleCardPlayRules(
                    session.Combatants,
                    playerZones,
                    session.EnemyCombatantIdsInEncounterOrder,
                    tables));
            List<HandCardVisual> cards = GetPrivateField<List<HandCardVisual>>(container, "_cards");
            cards.Add(incomingCard);
            cards.Add(otherCard);
            InvokePrivate(container, "RefreshCardPlayPresentation");

            int fastForwardRequestCount = 0;
            Sequence parent = null;
            var cue = new BattleCardMotionCue(
                BattleCardMotionCueKind.DrawToHand,
                incomingCard.CardId,
                settlementOrder: 4);
            incomingLease = container.CreateIncomingCardMotionTween(
                cue,
                incomingCard.GetScreenCenter() + new Vector2(-360f, -120f),
                duration: 0.4f,
                ease: Ease.Linear,
                requestFastForward: () =>
                {
                    fastForwardRequestCount++;
                    parent.Complete(withCallbacks: true);
                });
            parent = DOTween.Sequence()
                .SetId(parentTweenId)
                .SetUpdate(UpdateType.Manual)
                .Pause()
                .Append(incomingLease.Tween);
            parent.Play();
            parent.ManualUpdate(0.1f, 0.1f);

            Assert.That(incomingCard.IsIncomingCardMotionActive, Is.True);
            Assert.That(otherCard.IsIncomingCardMotionActive, Is.False);
            Assert.That(incomingCanvasGroup.interactable, Is.True);
            Assert.That(otherCanvasGroup.interactable, Is.True);

            container.HandleBeginDrag(otherCard);

            Assert.That(GetPrivateField<HandCardVisual>(container, "_draggingCard"), Is.SameAs(otherCard));
            Assert.That(fastForwardRequestCount, Is.Zero);
            Assert.That(incomingCard.IsIncomingCardMotionActive, Is.True);
            MethodInfo cancelActiveDrag = typeof(HandCardContainer).GetMethod(
                "CancelActiveDrag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(cancelActiveDrag, Is.Not.Null);
            cancelActiveDrag.Invoke(container, new object[] { false });

            container.HandleBeginDrag(incomingCard);

            Assert.That(fastForwardRequestCount, Is.EqualTo(1));
            Assert.That(incomingCard.IsIncomingCardMotionActive, Is.False);
            Assert.That(GetPrivateField<HandCardVisual>(container, "_draggingCard"), Is.SameAs(incomingCard));
            Assert.That(otherCanvasGroup.interactable, Is.True);
            Assert.That(zones.Hand, Has.Count.EqualTo(2));
            Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);

            container.HandleBeginDrag(incomingCard);

            Assert.That(fastForwardRequestCount, Is.EqualTo(1));
        }
        finally
        {
            DOTween.Kill(parentTweenId, complete: false);
            incomingLease?.Cleanup();
            if (incomingCard != null)
                Object.DestroyImmediate(incomingCard.gameObject);
            if (otherCard != null)
                Object.DestroyImmediate(otherCard.gameObject);
            if (handObject != null)
                Object.DestroyImmediate(handObject);
            DestroyPresentationObjects(presentationObjects);
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            coordinator.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>确认 PlayCard 前奏先飞向目标，随后才播放 Effect，并最终按原 Order 飞向弃牌堆。</summary>
    [UnityTest]
    public IEnumerator PlayCardPresentation_UsesPreludeThenEffectThenOriginalCardMovedOrder()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        using var combatants = new BattleCombatantsData();
        using var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 5721);
        PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
        GameObject canvasObject = null;
        GameObject cameraObject = null;
        GameObject worldView = null;
        GameObject presenterObject = null;
        GameObject handObject = null;
        HandCardVisual card = null;
        Texture2D targetTexture = null;
        Sprite targetSprite = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            canvasObject = new GameObject(
                "PlayCardPresentationCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);
            cameraObject = new GameObject("PlayCardPresentationCamera", typeof(Camera));
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
            worldView = new GameObject("PlayCardPresentationWorld", typeof(SpriteRenderer));
            worldView.GetComponent<SpriteRenderer>().sprite = targetSprite;
            worldView.transform.position = new Vector3(1.25f, 0.75f, 0f);

            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            ParticipantHudView hud = Object.Instantiate(
                hudPrefab,
                canvas.transform).GetComponent<ParticipantHudView>();
            cfg.Tables emptyTables = new cfg.Tables(_ => new JArray());
            hud.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                emptyTables,
                enemyIntents: null);
            presenterObject = new GameObject("PlayCardPresentationPresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantView(player.Id, worldView);
            presenter.RegisterParticipantHud(player.Id, hud);

            Text drawText = CreateTestPileText(
                canvasRect,
                "DrawPile",
                new Vector2(-360f, -210f));
            Text discardText = CreateTestPileText(
                canvasRect,
                "DiscardPile",
                new Vector2(360f, -210f));
            Text exhaustText = CreateTestPileText(
                canvasRect,
                "ExhaustPile",
                new Vector2(0f, -210f));
            BattleCardPileHudView pileView = canvasObject.AddComponent<BattleCardPileHudView>();
            SetPrivateField(pileView, "_drawPileText", drawText);
            SetPrivateField(pileView, "_discardPileText", discardText);
            SetPrivateField(pileView, "_exhaustPileText", exhaustText);
            Canvas.ForceUpdateCanvases();

            GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            handObject = Object.Instantiate(handPrefab);
            HandCardContainer hand = handObject.GetComponent<HandCardContainer>();
            GameObject cardPrefab = GetPrivateField<GameObject>(hand, "cardViewPrefab");
            card = Object.Instantiate(cardPrefab).GetComponent<HandCardVisual>();
            CanvasGroup cardCanvasGroup = card.CardContent.gameObject.AddComponent<CanvasGroup>();
            card.Initialize(Vector3.one * 0.36f, cardId, cardCanvasGroup);
            card.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -300f), 0f, 0));
            Vector2 initialCardScreenPosition = card.GetScreenCenter();
            int transientDestroyCount = 0;
            hand.ConfigureTransientCardDestroyForTesting(_ => transientDestroyCount++);
            SetPrivateField(hand, "_cardZones", zones);
            GetPrivateField<List<HandCardVisual>>(hand, "_cards").Add(card);
            zones.DiscardFromHand(cardId);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(hand, new object[] { false });

            adapter = new BattleCommandPresentationAdapter(
                presenter,
                hand,
                pileView,
                () => 0.1f);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 72,
                BattleCommandType.PlayCard,
                player.Id,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleEnergySpentSettlement(0, player.Id, 3, 2),
                    new BattleBlockGainedSettlement(
                        order: 1,
                        new BattleEffectId(4301),
                        player.Id,
                        player.Id,
                        blockBefore: 0,
                        blockAfter: 5),
                    new BattleCardMovedSettlement(
                        order: 2,
                        cardId,
                        BattleCardZone.Hand,
                        BattleCardZone.DiscardPile),
                });
            int completionCount = 0;
            Transform feedbackAnchor = hud.transform.Find("FeedbackAnchor");
            Assert.That(
                presenter.TryGetPresentationScreenAnchor(player.Id, out Vector2 targetScreenPosition),
                Is.True);

            ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);

            Assert.That(feedbackAnchor.childCount, Is.EqualTo(1));
            Assert.That(feedbackAnchor.GetChild(0).gameObject.activeSelf, Is.False);
            Assert.That(card.GetScreenCenter().x, Is.EqualTo(initialCardScreenPosition.x).Within(0.01f));
            Assert.That(card.GetScreenCenter().y, Is.EqualTo(initialCardScreenPosition.y).Within(0.01f));

            adapter.Tick();

            Assert.That(card.GetScreenCenter(), Is.Not.EqualTo(initialCardScreenPosition));
            Assert.That(feedbackAnchor.GetChild(0).gameObject.activeSelf, Is.False);
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            Assert.That(card.GetScreenCenter().x, Is.EqualTo(targetScreenPosition.x).Within(0.01f));
            Assert.That(card.GetScreenCenter().y, Is.EqualTo(targetScreenPosition.y).Within(0.01f));
            Assert.That(feedbackAnchor.GetChild(0).gameObject.activeSelf, Is.True);
            Assert.That(completionCount, Is.Zero);

            adapter.CompleteImmediately();
            adapter.CompleteImmediately();

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(transientDestroyCount, Is.EqualTo(1));
            Assert.That(feedbackAnchor.childCount, Is.Zero);
            Assert.That(zones.Hand, Is.Empty);
            Assert.That(zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
        finally
        {
            adapter?.Dispose();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (card != null)
                Object.DestroyImmediate(card.gameObject);
            if (handObject != null)
                Object.DestroyImmediate(handObject);
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (cameraObject != null)
                Object.DestroyImmediate(cameraObject);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            if (targetSprite != null)
                Object.DestroyImmediate(targetSprite);
            if (targetTexture != null)
                Object.DestroyImmediate(targetTexture);
            localization.Dispose();
        }
    }

    /// <summary>确认 concrete factory 只经 Presenter 唯一映射命中精确 HUD，缺失目标同步 fault。</summary>
    [UnityTest]
    public IEnumerator CreateCombatFeedbackTween_TwoParticipantsUseExactExistingHudWithoutFallback()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject firstWorld = null;
        GameObject secondWorld = null;
        GameObject presenterObject = null;
        ParticipantHudView firstHud = null;
        ParticipantHudView secondHud = null;
        BattleCommandPresentationTween tween = null;
        try
        {
            PlayerCombatantData first = combatants.AddPlayer(1001, 30, 0);
            PlayerCombatantData second = combatants.AddPlayer(1002, 28, 0);
            canvasObject = new GameObject(
                "BattleParticipantFeedbackRoutingCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            firstWorld = new GameObject("FirstParticipantWorld", typeof(SpriteRenderer));
            secondWorld = new GameObject("SecondParticipantWorld", typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            firstHud = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            secondHud = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            cfg.Tables tables = new cfg.Tables(_ => new JArray());
            firstHud.Bind(
                first,
                "battle.keyword.strength.name",
                firstWorld.transform,
                canvas,
                localization,
                tables,
                enemyIntents: null);
            secondHud.Bind(
                second,
                "battle.keyword.strength.name",
                secondWorld.transform,
                canvas,
                localization,
                tables,
                enemyIntents: null);
            presenterObject = new GameObject("BattleParticipantFeedbackRoutingPresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantHud(first.Id, firstHud);
            presenter.RegisterParticipantHud(second.Id, secondHud);
            var factory = new BattleCombatFeedbackTweenFactory(
                presenter.CreateCombatFeedbackTween);
            BattleCommandPresentationStep secondStep = CreateBlockGainedStep(second.Id);

            Assert.That(factory.TryCreate(secondStep, out tween), Is.True);

            Assert.That(firstHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(secondHud.transform.Find("FeedbackAnchor").childCount, Is.EqualTo(1));

            BattleCommandPresentationStep missingStep =
                CreateBlockGainedStep(new CombatantId(9999));
            Assert.Throws<InvalidOperationException>(
                () => factory.TryCreate(missingStep, out _));
            Assert.That(firstHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(secondHud.transform.Find("FeedbackAnchor").childCount, Is.EqualTo(1));

            ReleasePresentationTween(ref tween);
            using var adapter = new BattleCommandPresentationAdapter(
                presenter,
                () => 0.1f);
            var completionCount = 0;
            ((IBattleCommandPresentation)adapter).Present(
                CreateBlockGainedResult(second.Id),
                () => completionCount++);

            Assert.That(secondHud.transform.Find("FeedbackAnchor").childCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            Assert.That(completionCount, Is.Zero);
            Assert.That(
                secondHud.transform.Find("FeedbackAnchor").GetChild(0).gameObject.activeSelf,
                Is.True);

            adapter.CompleteImmediately();

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(secondHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);

            var cancelledCompletionCount = 0;
            var cancelledAdapter = new BattleCommandPresentationAdapter(
                presenter,
                () => 0.1f);
            ((IBattleCommandPresentation)cancelledAdapter).Present(
                CreateBlockGainedResult(first.Id),
                () => cancelledCompletionCount++);
            cancelledAdapter.Tick();
            Assert.That(firstHud.transform.Find("FeedbackAnchor").childCount, Is.EqualTo(1));

            cancelledAdapter.Dispose();
            cancelledAdapter.Dispose();
            cancelledAdapter.Tick();
            cancelledAdapter.CompleteImmediately();

            Assert.That(firstHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(cancelledCompletionCount, Is.Zero);

            using var faultAdapter = new BattleCommandPresentationAdapter(
                presenter,
                () => 0.1f);
            var faultCompletionCount = 0;

            Assert.Throws<InvalidOperationException>(() =>
                ((IBattleCommandPresentation)faultAdapter).Present(
                    CreateBuildFaultResult(first.Id, new CombatantId(9998)),
                    () => faultCompletionCount++));

            Assert.That(firstHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(secondHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(faultCompletionCount, Is.Zero);
        }
        finally
        {
            ReleasePresentationTween(ref tween);
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (firstHud != null)
                Object.DestroyImmediate(firstHud.gameObject);
            if (secondHud != null)
                Object.DestroyImmediate(secondHud.gameObject);
            if (firstWorld != null)
                Object.DestroyImmediate(firstWorld);
            if (secondWorld != null)
                Object.DestroyImmediate(secondWorld);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>确认 fatal 活动时间线被销毁时清理数字、丢弃 completion 并收口当前死亡事实。</summary>
    [UnityTest]
    public IEnumerator Dispose_FatalFeedbackCleansAndConvergesWithoutCompletion()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        GameObject presenterObject = null;
        ParticipantHudView hudView = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
            canvasObject = new GameObject(
                "BattleParticipantFatalDisposeCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "BattleParticipantFatalDisposeWorld",
                typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            presenterObject = new GameObject("BattleParticipantFatalDisposePresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantHud(player.Id, hudView);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);
            var fatal = new BattleDamageAppliedSettlement(
                order: 0,
                new BattleEffectId(4501),
                enemy.Id,
                player.Id,
                attackValue: 30,
                blockBefore: 0,
                blockAfter: 0,
                healthBefore: 30,
                healthAfter: 0);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 88,
                BattleCommandType.CompleteEnemyAction,
                submitterId: null,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[] { fatal });
            var completionCount = 0;
            adapter = new BattleCommandPresentationAdapter(presenter, () => 0.1f);

            ((IBattleCommandPresentation)adapter).Present(result, () => completionCount++);
            adapter.Tick();

            Assert.That(hudView.transform.Find("FeedbackAnchor").childCount, Is.EqualTo(1));
            Assert.That(worldView.activeSelf, Is.True);
            Assert.That(hudView.gameObject.activeSelf, Is.True);
            Assert.That(completionCount, Is.Zero);

            adapter.Dispose();
            adapter.Dispose();
            adapter.Tick();
            adapter.CompleteImmediately();

            Assert.That(hudView.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(worldView.activeSelf, Is.False);
            Assert.That(hudView.gameObject.activeSelf, Is.False);
            Assert.That(combatants.All[player.Id], Is.SameAs(player));
            Assert.That(completionCount, Is.Zero);
        }
        finally
        {
            adapter?.Dispose();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>验证 HUD 先销毁时，活动状态脉冲的 runner 清理无异常、无迟到 completion。</summary>
    [UnityTest]
    public IEnumerator Dispose_AfterPulseHudDestroyed_DropsCompletionWithoutLateCallback()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        GameObject presenterObject = null;
        ParticipantHudView hudView = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            canvasObject = new GameObject(
                "BattleParticipantDestroyedPulseCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "BattleParticipantDestroyedPulseWorld",
                typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            presenterObject = new GameObject("BattleParticipantDestroyedPulsePresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantHud(player.Id, hudView);
            var completionCount = 0;
            adapter = new BattleCommandPresentationAdapter(presenter, () => 0.1f);

            ((IBattleCommandPresentation)adapter).Present(
                CreateStrengthResult(player.Id),
                () => completionCount++);
            adapter.Tick();
            Assert.That(
                hudView.transform.Find("VitalsAnchor/StatusRow").gameObject.activeSelf,
                Is.True);

            Object.DestroyImmediate(hudView.gameObject);
            hudView = null;
            Object.DestroyImmediate(worldView);
            worldView = null;

            Assert.DoesNotThrow(() => adapter.Dispose());
            Assert.DoesNotThrow(() => adapter.Dispose());
            Assert.DoesNotThrow(() => adapter.Tick());
            Assert.DoesNotThrow(() => adapter.CompleteImmediately());
            Assert.That(completionCount, Is.Zero);
            Assert.That(player.CurrentStrength, Is.Zero);
        }
        finally
        {
            adapter?.Dispose();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>验证 HUD 与世界 View 先销毁时，fatal runner 清理无异常、无迟到 completion。</summary>
    [UnityTest]
    public IEnumerator Dispose_AfterFatalHudAndWorldDestroyed_DropsCompletionWithoutLateCallback()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        GameObject presenterObject = null;
        ParticipantHudView hudView = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
            canvasObject = new GameObject(
                "BattleParticipantDestroyedFatalCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "BattleParticipantDestroyedFatalWorld",
                typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            presenterObject = new GameObject("BattleParticipantDestroyedFatalPresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantHud(player.Id, hudView);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);
            var completionCount = 0;
            adapter = new BattleCommandPresentationAdapter(presenter, () => 0.1f);

            ((IBattleCommandPresentation)adapter).Present(
                CreateFatalResult(player.Id, enemy.Id),
                () => completionCount++);
            adapter.Tick();

            Object.DestroyImmediate(hudView.gameObject);
            hudView = null;
            Object.DestroyImmediate(worldView);
            worldView = null;

            Assert.DoesNotThrow(() => adapter.Dispose());
            Assert.DoesNotThrow(() => adapter.Dispose());
            Assert.DoesNotThrow(() => adapter.Tick());
            Assert.DoesNotThrow(() => adapter.CompleteImmediately());
            Assert.That(completionCount, Is.Zero);
            Assert.That(player.CurrentHealth, Is.Zero);
            Assert.That(combatants.All[player.Id], Is.SameAs(player));
        }
        finally
        {
            adapter?.Dispose();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>验证真实 fatal 时间线自然串行播放数字、抖动与死亡过渡，且 M9C 不消费 outcome。</summary>
    [UnityTest]
    public IEnumerator Present_FatalTimeline_NaturalPlayback_KeepsWorldHudVisibleThroughNumberAndShakeThenHides()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        GameObject presenterObject = null;
        ParticipantHudView hudView = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
            canvasObject = new GameObject(
                "BattleParticipantFatalNaturalCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "BattleParticipantFatalNaturalWorld",
                typeof(SpriteRenderer));
            worldView.transform.localPosition = new Vector3(2f, 3f, 0f);
            Vector3 basePosition = worldView.transform.localPosition;
            Vector3 baseWorldScale = worldView.transform.localScale;
            SpriteRenderer spriteRenderer = worldView.GetComponent<SpriteRenderer>();
            Color baseWorldColor = spriteRenderer.color;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            Vector3 baseHudScale = hudView.transform.localScale;
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            presenterObject = new GameObject("BattleParticipantFatalNaturalPresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantHud(player.Id, hudView);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);
            var completionCount = 0;
            adapter = new BattleCommandPresentationAdapter(presenter, () => 0.1f);

            ((IBattleCommandPresentation)adapter).Present(
                CreateFatalResultWithOutcome(player.Id, enemy.Id),
                () => completionCount++);

            Transform feedbackAnchor = hudView.transform.Find("FeedbackAnchor");
            Assert.That(feedbackAnchor.childCount, Is.EqualTo(1));
            Assert.That(feedbackAnchor.GetChild(0).gameObject.activeSelf, Is.False);
            Assert.That(worldView.activeSelf, Is.True);
            Assert.That(hudView.gameObject.activeSelf, Is.True);
            Assert.That(completionCount, Is.Zero);

            adapter.Tick();

            GameObject floatingNumber = feedbackAnchor.GetChild(0).gameObject;
            Assert.That(floatingNumber.activeSelf, Is.True);
            Assert.That(floatingNumber.GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("-30"));
            Assert.That(worldView.activeSelf, Is.True);
            Assert.That(hudView.gameObject.activeSelf, Is.True);
            Assert.That(player.CurrentHealth, Is.Zero);

            bool observedShake = false;
            for (int index = 0; index < 7; index++)
            {
                adapter.Tick();
                if (worldView.transform.localPosition != basePosition)
                    observedShake = true;
            }

            Assert.That(observedShake, Is.True);
            Assert.That(worldView.activeSelf, Is.True);
            Assert.That(hudView.gameObject.activeSelf, Is.True);
            Assert.That(spriteRenderer.color.a, Is.LessThan(baseWorldColor.a));
            Assert.That(completionCount, Is.Zero);

            for (int index = 0; index < 6 && completionCount == 0; index++)
                adapter.Tick();

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(feedbackAnchor.childCount, Is.Zero);
            Assert.That(worldView.activeSelf, Is.False);
            Assert.That(hudView.gameObject.activeSelf, Is.False);
            Assert.That(worldView.transform.localPosition, Is.EqualTo(basePosition));
            Assert.That(worldView.transform.localScale, Is.EqualTo(baseWorldScale));
            Assert.That(hudView.transform.localScale, Is.EqualTo(baseHudScale));
            Assert.That(spriteRenderer.color, Is.EqualTo(baseWorldColor));
            Assert.That(canvas.transform.childCount, Is.EqualTo(1));
            Assert.That(combatants.All[player.Id], Is.SameAs(player));

            adapter.Tick();
            adapter.CompleteImmediately();
            Assert.That(completionCount, Is.EqualTo(1));
        }
        finally
        {
            adapter?.Dispose();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>验证真实 fatal 时间线立即完成时只释放一次 completion，并清理全部瞬态而不创建 outcome 镜像。</summary>
    [UnityTest]
    public IEnumerator CompleteImmediately_FatalTimeline_CleansAndHidesExactlyOnceWithoutOutcome()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        GameObject presenterObject = null;
        ParticipantHudView hudView = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
            canvasObject = new GameObject(
                "BattleParticipantFatalImmediateCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "BattleParticipantFatalImmediateWorld",
                typeof(SpriteRenderer));
            worldView.transform.localPosition = new Vector3(-2f, 1f, 0f);
            Vector3 basePosition = worldView.transform.localPosition;
            Vector3 baseWorldScale = worldView.transform.localScale;
            SpriteRenderer spriteRenderer = worldView.GetComponent<SpriteRenderer>();
            Color baseWorldColor = spriteRenderer.color;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            Vector3 baseHudScale = hudView.transform.localScale;
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            presenterObject = new GameObject("BattleParticipantFatalImmediatePresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantHud(player.Id, hudView);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);
            var completionCount = 0;
            adapter = new BattleCommandPresentationAdapter(presenter, () => 0.1f);

            ((IBattleCommandPresentation)adapter).Present(
                CreateFatalResultWithOutcome(player.Id, enemy.Id),
                () => completionCount++);
            Transform feedbackAnchor = hudView.transform.Find("FeedbackAnchor");
            Assert.That(feedbackAnchor.childCount, Is.EqualTo(1));

            adapter.CompleteImmediately();

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(feedbackAnchor.childCount, Is.Zero);
            Assert.That(worldView.activeSelf, Is.False);
            Assert.That(hudView.gameObject.activeSelf, Is.False);
            Assert.That(worldView.transform.localPosition, Is.EqualTo(basePosition));
            Assert.That(worldView.transform.localScale, Is.EqualTo(baseWorldScale));
            Assert.That(hudView.transform.localScale, Is.EqualTo(baseHudScale));
            Assert.That(spriteRenderer.color, Is.EqualTo(baseWorldColor));
            Assert.That(canvas.transform.childCount, Is.EqualTo(1));
            Assert.That(player.CurrentHealth, Is.Zero);
            Assert.That(combatants.All[player.Id], Is.SameAs(player));

            adapter.Tick();
            adapter.CompleteImmediately();
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(feedbackAnchor.childCount, Is.Zero);
        }
        finally
        {
            adapter?.Dispose();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>验证首个敌人死亡后仍按唯一映射把后续反馈送到第二个敌人，不回退到已死亡 HUD。</summary>
    [UnityTest]
    public IEnumerator Complete_FirstEnemyFatalThenPresentSecondEnemyFeedback_UsesSecondHudAndKeepsFirstAuthority()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        BattleEnemyIntentsData enemyIntents = null;
        GameObject canvasObject = null;
        GameObject firstWorld = null;
        GameObject secondWorld = null;
        GameObject presenterObject = null;
        ParticipantHudView firstHud = null;
        ParticipantHudView secondHud = null;
        BattleCommandPresentationAdapter adapter = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            EnemyCombatantData firstEnemy = combatants.AddEnemy(2001, 20, 0);
            EnemyCombatantData secondEnemy = combatants.AddEnemy(2002, 24, 0);
            cfg.Tables tables = CreateTwoEnemyIntentTables();
            enemyIntents = new BattleEnemyIntentsData(
                combatants,
                new[] { firstEnemy.Id, secondEnemy.Id },
                tables,
                battleSeed: 97531);
            canvasObject = new GameObject(
                "BattleParticipantTwoEnemyCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            firstWorld = new GameObject("BattleParticipantFirstEnemyWorld", typeof(SpriteRenderer));
            secondWorld = new GameObject("BattleParticipantSecondEnemyWorld", typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            firstHud = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            secondHud = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            firstHud.Bind(
                firstEnemy,
                "battle.enemy.test_slime.name",
                firstWorld.transform,
                canvas,
                localization,
                tables,
                enemyIntents);
            secondHud.Bind(
                secondEnemy,
                "battle.enemy.test_slime.name",
                secondWorld.transform,
                canvas,
                localization,
                tables,
                enemyIntents);
            presenterObject = new GameObject("BattleParticipantTwoEnemyPresenter");
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            presenter.RegisterParticipantHud(firstEnemy.Id, firstHud);
            presenter.RegisterParticipantHud(secondEnemy.Id, secondHud);
            BattleEffectStateTestDriver.Kill(combatants, player.Id, firstEnemy.Id);
            adapter = new BattleCommandPresentationAdapter(presenter, () => 0.1f);
            var firstCompletionCount = 0;

            ((IBattleCommandPresentation)adapter).Present(
                CreateFatalResult(firstEnemy.Id, player.Id, healthBefore: 20),
                () => firstCompletionCount++);
            adapter.CompleteImmediately();

            Assert.That(firstCompletionCount, Is.EqualTo(1));
            Assert.That(firstWorld.activeSelf, Is.False);
            Assert.That(firstHud.gameObject.activeSelf, Is.False);
            Assert.That(firstHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(combatants.All[firstEnemy.Id], Is.SameAs(firstEnemy));
            Assert.That(firstEnemy.IsAlive, Is.False);

            var secondCompletionCount = 0;
            ((IBattleCommandPresentation)adapter).Present(
                CreateBlockGainedResult(secondEnemy.Id),
                () => secondCompletionCount++);

            Assert.That(firstHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(secondHud.transform.Find("FeedbackAnchor").childCount, Is.EqualTo(1));
            Assert.That(secondHud.transform.Find("FeedbackAnchor").GetChild(0).gameObject.activeSelf, Is.False);
            Assert.That(secondWorld.activeSelf, Is.True);
            Assert.That(secondHud.gameObject.activeSelf, Is.True);
            Assert.That(secondCompletionCount, Is.Zero);

            adapter.CompleteImmediately();

            Assert.That(secondCompletionCount, Is.EqualTo(1));
            Assert.That(secondHud.transform.Find("FeedbackAnchor").childCount, Is.Zero);
            Assert.That(firstWorld.activeSelf, Is.False);
            Assert.That(firstHud.gameObject.activeSelf, Is.False);
            Assert.That(combatants.All[firstEnemy.Id], Is.SameAs(firstEnemy));
            Assert.That(combatants.All[secondEnemy.Id], Is.SameAs(secondEnemy));
        }
        finally
        {
            adapter?.Dispose();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (presenterObject != null)
                Object.DestroyImmediate(presenterObject);
            if (firstHud != null)
                Object.DestroyImmediate(firstHud.gameObject);
            if (secondHud != null)
                Object.DestroyImmediate(secondHud.gameObject);
            if (firstWorld != null)
                Object.DestroyImmediate(firstWorld);
            if (secondWorld != null)
                Object.DestroyImmediate(secondWorld);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            enemyIntents?.Dispose();
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>创建只有一个 Defend 数字步骤的冻结表现计划。</summary>
    private static BattleCommandPresentationStep CreateBlockGainedStep(CombatantId targetId)
    {
        return BattleCommandPresentationPlan.Create(
            CreateBlockGainedResult(targetId)).SettlementSteps[0];
    }

    /// <summary>创建只有一个 Defend 数字记录的冻结命令结果。</summary>
    private static BattleCommandExecutionResult CreateBlockGainedResult(CombatantId targetId)
    {
        var settlement = new BattleBlockGainedSettlement(
            order: 0,
            new BattleEffectId(4301),
            targetId,
            targetId,
            blockBefore: 0,
            blockAfter: 5);
        var result = new BattleCommandExecutionResult(
            authoritySequence: targetId.Value,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { settlement });
        return result;
    }

    /// <summary>创建只包含一条力量变化的冻结结果。</summary>
    private static BattleCommandExecutionResult CreateStrengthResult(CombatantId targetId)
    {
        return new BattleCommandExecutionResult(
            authoritySequence: 87,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleAttributeModifiedSettlement(
                    order: 0,
                    new BattleEffectId(4500),
                    targetId,
                    targetId,
                    BattleAttributeType.Strength,
                    valueBefore: 0,
                    valueAfter: 2),
            });
    }

    /// <summary>创建只包含一条致命伤害的冻结结果。</summary>
    private static BattleCommandExecutionResult CreateFatalResult(
        CombatantId targetId,
        CombatantId sourceId,
        int healthBefore = 30)
    {
        return new BattleCommandExecutionResult(
            authoritySequence: 89,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleDamageAppliedSettlement(
                    order: 0,
                    new BattleEffectId(4502),
                    sourceId,
                    targetId,
                    attackValue: healthBefore,
                    blockBefore: 0,
                    blockAfter: 0,
                    healthBefore,
                    healthAfter: 0),
            });
    }

    /// <summary>创建致命伤害后紧接 BattleEnded 的冻结结果，供 M9C 证明不消费 outcome。</summary>
    private static BattleCommandExecutionResult CreateFatalResultWithOutcome(
        CombatantId targetId,
        CombatantId sourceId)
    {
        return new BattleCommandExecutionResult(
            authoritySequence: 90,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleDamageAppliedSettlement(
                    order: 0,
                    new BattleEffectId(4503),
                    sourceId,
                    targetId,
                    attackValue: 30,
                    blockBefore: 0,
                    blockAfter: 0,
                    healthBefore: 30,
                    healthAfter: 0),
                new BattlePhaseChangedSettlement(
                    order: 1,
                    BattleTurnPhase.EnemyAction,
                    BattleTurnPhase.BattleEnded,
                    roundNumberBefore: 1,
                    roundNumberAfter: 1,
                    currentActingEnemyIdBefore: sourceId,
                    currentActingEnemyIdAfter: null),
            });
    }

    /// <summary>创建双敌人 HUD 所需的最小确定性意图表集合。</summary>
    private static cfg.Tables CreateTwoEnemyIntentTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\"," +
                "\"max_health\":20,\"base_strength\":0,\"view_prefab_key\":\"\"," +
                "\"behavior_group_id\":6001}," +
                "{\"id\":2002,\"name_i18n_key\":\"battle.enemy.test_slime.name\"," +
                "\"max_health\":24,\"base_strength\":0,\"view_prefab_key\":\"\"," +
                "\"behavior_group_id\":6001}]"),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6}]"),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1," +
                "\"effect_id\":4002,\"weight\":1,\"cooldown_selections\":0," +
                "\"max_consecutive\":0}]"),
        };
        return new cfg.Tables(tableName => data[tableName]);
    }

    /// <summary>创建一个玩家和一个敌人的 Presenter readiness 最小 Session 配置。</summary>
    private static cfg.Tables CreateReadinessSessionTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\"," +
                "\"view_prefab_key\":\"pfb_char_player\",\"max_health\":30," +
                "\"base_strength\":0,\"initial_deck_id\":1001}]"),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\"," +
                "\"view_prefab_key\":\"pfb_char_enemy\",\"max_health\":20," +
                "\"base_strength\":0,\"behavior_group_id\":6001}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3001]}]"),
            ["battle_tbcard"] = JArray.Parse(
                "[{\"id\":3001,\"name_i18n_key\":\"battle.card.test.name\"," +
                "\"description_i18n_key\":\"battle.card.test.description\",\"cost\":0," +
                "\"target_rule\":0,\"effect_bindings\":[{\"argument_key\":\"block\"," +
                "\"effect_id\":4001}]}]"),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4001,\"effect_type\":2,\"attribute\":0,\"value\":1}]"),
            ["battle_tbencounter"] = JArray.Parse(
                "[{\"id\":5001,\"enemy_template_ids\":[2001]}]"),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":1,\"target_rule\":0," +
                "\"effect_id\":4001,\"weight\":1,\"cooldown_selections\":0," +
                "\"max_consecutive\":0}]"),
        };
        return new cfg.Tables(tableName => data[tableName]);
    }

    /// <summary>为 Presenter 资源边界测试创建只带显式 Luban 表的配置服务。</summary>
    private static ConfigService CreateConfigService(cfg.Tables tables)
    {
        var configs = new ConfigService();
        PropertyInfo property = typeof(ConfigService).GetProperty(
            nameof(ConfigService.Tables),
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        property.SetValue(configs, tables);
        PropertyInfo gameConfigProperty = typeof(ConfigService).GetProperty(
            nameof(ConfigService.GameConfig),
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(gameConfigProperty, Is.Not.Null);
        gameConfigProperty.SetValue(configs, new GameConfig());
        return configs;
    }

    /// <summary>创建并启动只含一个玩家和一个敌人的真实 Queue，供 concrete readiness 输入测试使用。</summary>
    private static BattleCommandQueue CreateStartedReadinessQueue(
        BattleSession session,
        cfg.Tables tables,
        BattleCommandSubmissionCoordinator coordinator,
        out PlayerCombatantData player)
    {
        player = null;
        foreach (CombatantData combatant in session.Combatants.All.Values)
        {
            if (combatant is PlayerCombatantData candidate)
                player = candidate;
        }

        Assert.That(player, Is.Not.Null);
        var queue = new BattleCommandQueue(
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
            new ImmediateBattleCommandPresentation(),
            coordinator);
        var command = new StartBattleCommand();
        coordinator.PreRegister(command);
        BattleCommandSubmissionResult result = queue.Submit(command);
        Assert.That(result.Accepted, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(session.CardZones.Hand, Has.Count.EqualTo(1));
        return queue;
    }

    /// <summary>创建 PlayCard concrete 路由测试使用的当前牌堆 Text 锚点。</summary>
    private static Text CreateTestPileText(
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

    /// <summary>为当前 Session 的每名参与者注册存活的世界 View 与 HUD，并保留清理句柄。</summary>
    private static void RegisterReadyPresentation(
        BattleSession session,
        BattleParticipantPresenter presenter,
        List<GameObject> presentationObjects)
    {
        foreach (CombatantId participantId in session.Combatants.All.Keys)
        {
            var worldObject = new GameObject($"BattleParticipantReadyWorld_{participantId.Value}");
            var hudObject = new GameObject($"BattleParticipantReadyHud_{participantId.Value}");
            ParticipantHudView hud = hudObject.AddComponent<ParticipantHudView>();
            presentationObjects.Add(worldObject);
            presentationObjects.Add(hudObject);
            presenter.RegisterParticipantView(participantId, worldObject);
            presenter.RegisterParticipantHud(participantId, hud);
        }
    }

    /// <summary>销毁 concrete readiness 测试创建的所有非权威表现对象。</summary>
    private static void DestroyPresentationObjects(List<GameObject> presentationObjects)
    {
        foreach (GameObject presentationObject in presentationObjects)
        {
            if (presentationObject != null)
                Object.DestroyImmediate(presentationObject);
        }

        presentationObjects.Clear();
    }

    /// <summary>立即回收未交给 runner 的 concrete Tween lease，再执行其一次性 transient 清理。</summary>
    private static void ReleasePresentationTween(ref BattleCommandPresentationTween tween)
    {
        if (tween == null)
            return;

        tween.Tween.Complete(withCallbacks: false);
        tween.Cleanup();
        tween = null;
    }

    /// <summary>读取 concrete View 的私有状态，测试只观察表现接线而不新增生产 seam。</summary>
    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        return (T)field.GetValue(target);
    }

    /// <summary>调用 concrete View 的 Unity 生命周期或刷新函数，验证现有生产接线。</summary>
    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        method.Invoke(target, parameters: null);
    }

    /// <summary>为具体 Presenter 生命周期测试设置序列化依赖，不修改 Prefab 或场景。</summary>
    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }

    /// <summary>创建先命中现有 HUD、再因缺失精确目标而同步失败的双记录结果。</summary>
    private static BattleCommandExecutionResult CreateBuildFaultResult(
        CombatantId existingTargetId,
        CombatantId missingTargetId)
    {
        return new BattleCommandExecutionResult(
            authoritySequence: 77,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleBlockGainedSettlement(
                    order: 0,
                    new BattleEffectId(4302),
                    existingTargetId,
                    existingTargetId,
                    blockBefore: 0,
                    blockAfter: 3),
                new BattleBlockGainedSettlement(
                    order: 1,
                    new BattleEffectId(4303),
                    missingTargetId,
                    missingTargetId,
                    blockBefore: 0,
                    blockAfter: 4),
            });
    }
}
