using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cfg;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.Run.Map;
using TinySpire.UI.Run;
using UnityEngine.Localization;

public sealed class RunEntryPresenterTests
{
    /// <summary>空槽冷启动只投影主菜单，不创建 Run、地图或可继续入口。</summary>
    [Test]
    public void ColdStartWithoutSave_ProjectsEmptyMainMenu()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 1u),
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(store.Current, Is.Null);
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(view.LastModel.ContinueEnabled, Is.False);
        Assert.That(view.LastModel.Map, Is.Null);
    }

    /// <summary>有效 schema v2 地图档只启用 Continue，玩家确认后才重建同一冻结地图。</summary>
    [Test]
    public void ColdStartMapSave_EnablesContinueAndHydratesFrozenMapOnlyAfterAction()
    {
        RunSaveDocument document = CreateMapReadyDocument();
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore(document);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 2u, saves),
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(store.Current, Is.Null);
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(view.LastModel.ContinueEnabled, Is.True);
        Assert.That(view.LastModel.Map, Is.Null);

        view.Emit(new RunEntryAction(RunEntryActionKind.ContinueGame));

        Assert.That(store.Current, Is.Not.Null);
        Assert.That(store.Current.MapDefinition.Fingerprint, Is.EqualTo(document.MapFingerprint));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.ContinueEnabled, Is.False);
        Assert.That(view.LastModel.Map.Fingerprint, Is.EqualTo(document.MapFingerprint));
    }

    /// <summary>新 Run 一次投影整张冻结图，并明牌位置、内容 ID、明确名称与可区分视觉锚点。</summary>
    [Test]
    public void CreateRun_ProjectsWholeFrozenMapWithStablePositionsNamesAndVisualAnchors()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 101u);
        flow.CreateNewRun(heroTemplateId: 1001);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();

        MapDefinition frozenMap = store.Current.MapDefinition;
        RunMapViewModel projectedMap = view.LastModel.Map;
        RunMapNodeViewModel[] combats = projectedMap.Nodes
            .Where(node => node.Kind == MapNodeKind.Combat)
            .ToArray();
        RunMapNodeViewModel[] bosses = projectedMap.Nodes
            .Where(node => node.Kind == MapNodeKind.Boss)
            .ToArray();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(projectedMap.Fingerprint, Is.EqualTo(frozenMap.Fingerprint));
        Assert.That(projectedMap.Nodes.Count, Is.EqualTo(frozenMap.Nodes.Count));
        Assert.That(projectedMap.Edges.Count, Is.EqualTo(frozenMap.Edges.Count));
        Assert.That(combats, Is.Not.Empty);
        Assert.That(combats.All(node => node.ContentId == 5001), Is.True);
        Assert.That(combats.All(node => node.DisplayName == "SLIME PATROL\nTest Slime"), Is.True);
        Assert.That(
            combats.All(node => node.VisualAnchorKind == RunMapVisualAnchorKind.EncounterSlimeSilhouette),
            Is.True);
        Assert.That(bosses.Length, Is.EqualTo(TinySpireActMapProfiles.Current.BossEndpointCount));
        Assert.That(
            bosses.Select(node => node.ContentId).Distinct().Count(),
            Is.EqualTo(TinySpireActMapProfiles.Current.BossCandidateCount));
        Assert.That(
            bosses.GroupBy(node => node.ContentId).Any(group => group.Count() > 1),
            Is.True);
        foreach (IGrouping<int, RunMapNodeViewModel> bossGroup in bosses.GroupBy(node => node.ContentId))
        {
            Assert.That(bossGroup.Select(node => node.DisplayName).Distinct().Count(), Is.EqualTo(1));
            Assert.That(bossGroup.Select(node => node.VisualAnchorKind).Distinct().Count(), Is.EqualTo(1));
        }
        Assert.That(
            bosses.Select(node => node.VisualAnchorKind).Distinct().Count(),
            Is.EqualTo(TinySpireActMapProfiles.Current.BossCandidateCount));

        foreach (RunMapNodeViewModel node in projectedMap.Nodes)
        {
            Assert.That(node.NodeId, Is.EqualTo(MapNodeId.FromPosition(node.Layer, node.Slot).Value));
            Assert.That(
                frozenMap.GetNode(new MapNodeId(node.NodeId)).ContentId,
                Is.EqualTo(node.ContentId));
        }
    }

    /// <summary>显式 G3 遭遇展示数据必须同时区分可读遭遇名称与首敌程序化剪影。</summary>
    [Test]
    public void IdentityCatalog_G3EncounterTestData_DistinguishesNamesAndPrimaryEnemySilhouettes()
    {
        var catalog = new RunMapIdentityCatalog(CreateTables, Localize);

        RunMapIdentityDescriptor slimePatrol = catalog.Resolve(MapNodeKind.Combat, 5001);
        RunMapIdentityDescriptor sentryLine = catalog.Resolve(MapNodeKind.Combat, 5002);

        Assert.That(slimePatrol.DisplayName, Is.EqualTo("SLIME PATROL\nTest Slime"));
        Assert.That(sentryLine.DisplayName, Is.EqualTo("SENTRY LINE\nTest Sentry"));
        Assert.That(slimePatrol.VisualAnchorKind, Is.EqualTo(RunMapVisualAnchorKind.EncounterSlimeSilhouette));
        Assert.That(sentryLine.VisualAnchorKind, Is.EqualTo(RunMapVisualAnchorKind.EncounterSentrySilhouette));
        Assert.That(slimePatrol.VisualAnchorKind, Is.Not.EqualTo(sentryLine.VisualAnchorKind));
    }

    /// <summary>G3 测试 Boss 目录必须为三个冻结身份提供明确名称与互不相同的程序化锚点。</summary>
    [TestCase(9001, "BOSS ALPHA", RunMapVisualAnchorKind.BossAlphaCrown)]
    [TestCase(9002, "BOSS BETA", RunMapVisualAnchorKind.BossBetaHorns)]
    [TestCase(9003, "BOSS GAMMA", RunMapVisualAnchorKind.BossGammaEye)]
    public void IdentityCatalog_G3BossTestData_ResolvesExplicitDistinctIdentity(
        int bossId,
        string expectedName,
        RunMapVisualAnchorKind expectedAnchor)
    {
        var catalog = new RunMapIdentityCatalog(CreateTables, Localize);

        RunMapIdentityDescriptor descriptor = catalog.Resolve(MapNodeKind.Boss, bossId);

        Assert.That(descriptor.DisplayName, Is.EqualTo(expectedName));
        Assert.That(descriptor.VisualAnchorKind, Is.EqualTo(expectedAnchor));
    }

    /// <summary>可选 Combat 动作只进入一次 Battle；Store 迁到 InBattle 后重复动作立即失效。</summary>
    [Test]
    public void SelectableCombatAction_EntersBattleAndImmediatelyBlocksDuplicateAction()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 202u);
        flow.CreateNewRun(heroTemplateId: 1001);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();
        MapNodeId selectedNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Combat);
        var action = new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: selectedNodeId);

        view.Emit(action);
        view.Emit(action);

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(store.Current.CommittedNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(store.Current.ActiveBattle.BattleId.NodeId, Is.EqualTo(selectedNodeId));
        Assert.That(store.Current.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(
            view.LastModel.Map.Nodes.Any(node =>
                node.State == RunMapNodePresentationState.Selectable),
            Is.False);
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
    }

    /// <summary>战斗胜利后当前位置推进到已选节点，并只投影新当前位置的普通直接出边。</summary>
    [Test]
    public async Task Victory_ProjectsNewCurrentNodeAndNextSelectableLayer()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 303u);
        flow.CreateNewRun(heroTemplateId: 1001);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();
        MapNodeId selectedNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Combat);

        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: selectedNodeId));
        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Victory, settledHealth: 67);

        string[] expectedSelectable = MapReachability.GetSelectableNodeIds(
                store.Current.MapDefinition,
                selectedNodeId,
                MapTraversalMode.Ordinary)
            .Select(nodeId => nodeId.Value)
            .ToArray();
        string[] actualSelectable = view.LastModel.Map.Nodes
            .Where(node => node.State == RunMapNodePresentationState.Selectable)
            .Select(node => node.NodeId)
            .ToArray();
        RunMapNodeViewModel current = view.LastModel.Map.Nodes.Single(node =>
            node.NodeId == selectedNodeId.Value);

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.CurrentNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(store.Current.PathNodeIds.Count, Is.EqualTo(2));
        Assert.That(current.State, Is.EqualTo(RunMapNodePresentationState.Current));
        Assert.That(actualSelectable, Is.EqualTo(expectedSelectable));
        Assert.That(actualSelectable.All(nodeId =>
            store.Current.MapDefinition.GetNode(new MapNodeId(nodeId)).Layer == current.Layer + 1), Is.True);
    }

    /// <summary>走完普通层后抵达 Boss 终点只保存 BossGateReached，并继续停留地图页。</summary>
    [Test]
    public async Task BossSelection_ReachesSavedBossGateWithoutStartingBossBattle()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(store, scenes, randomRootSeed: 404u, saves);
        flow.CreateNewRun(heroTemplateId: 1001);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();

        for (int layer = 1; layer <= TinySpireActMapProfiles.Current.NormalLayerSlotCounts.Count; layer++)
        {
            MapNodeId combatNodeId = GetFirstProjectedNodeId(
                view.LastModel,
                RunMapNodePresentationState.Selectable,
                MapNodeKind.Combat);
            view.Emit(new RunEntryAction(
                RunEntryActionKind.EnterMapNode,
                mapNodeId: combatNodeId));
            await CompleteActiveBattleAsync(
                flow,
                store,
                BattleResultKind.Victory,
                settledHealth: 80 - layer);
        }

        MapNodeId bossNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Boss);
        int sceneRequestCountBeforeBoss = scenes.LoadedAddresses.Count;
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: bossNodeId));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.BossGateReached));
        Assert.That(store.Current.CurrentNodeId, Is.EqualTo(bossNodeId));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(
            view.LastModel.Map.Nodes.Single(node => node.NodeId == bossNodeId.Value).State,
            Is.EqualTo(RunMapNodePresentationState.BossGateReached));
        Assert.That(scenes.LoadedAddresses.Count, Is.EqualTo(sceneRequestCountBeforeBoss));
        Assert.That(saves.Load().Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.BossGateReached));
    }

    /// <summary>普通战斗失败投影 Failure；玩家确认离开后才删除档与唯一终局 Run。</summary>
    [Test]
    public async Task Defeat_ProjectsFailureAndLeaveTerminalRunDeletesSave()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 505u, saves);
        flow.CreateNewRun(heroTemplateId: 1001);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();
        MapNodeId combatNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Combat);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: combatNodeId));

        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Defeat, settledHealth: 0);

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(store.Current.TerminalReason, Is.EqualTo(RunTerminalReason.Defeat));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Failure));
        Assert.That(view.LastModel.ContinueEnabled, Is.False);
        Assert.That(saves.DeleteCount, Is.Zero);

        view.Emit(new RunEntryAction(RunEntryActionKind.LeaveTerminalRun));

        Assert.That(saves.DeleteCount, Is.EqualTo(1));
        Assert.That(saves.Load().Status, Is.EqualTo(RunSaveLoadStatus.NotFound));
        Assert.That(store.Current, Is.Null);
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(view.LastModel.ContinueEnabled, Is.False);
    }

    /// <summary>冷启动读到 Terminal(Defeat) 必须直接恢复失败页，永不暴露 Continue。</summary>
    [Test]
    public void ColdStartTerminalSave_RestoresFailurePageWithoutContinue()
    {
        RunSaveDocument terminalDocument = CreateTerminalDocument();
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore(terminalDocument);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 606u, saves),
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(store.Current, Is.Not.Null);
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(store.Current.TerminalReason, Is.EqualTo(RunTerminalReason.Defeat));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Failure));
        Assert.That(view.LastModel.ContinueEnabled, Is.False);
        Assert.That(saves.DeleteCount, Is.Zero);
    }

    /// <summary>只有当前可选节点携带悬停后半程，且数据精确覆盖纯规则的全部后继边与 Boss。</summary>
    [Test]
    public void MapProjection_HoverRouteExistsOnlyForSelectableNodesAndIncludesCompleteBossRoutes()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 707u);
        flow.CreateNewRun(heroTemplateId: 1001);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();

        MapDefinition map = store.Current.MapDefinition;
        RunMapNodeViewModel[] selectable = view.LastModel.Map.Nodes
            .Where(node => node.State == RunMapNodePresentationState.Selectable)
            .ToArray();
        RunMapNodeViewModel[] nonSelectable = view.LastModel.Map.Nodes
            .Where(node => node.State != RunMapNodePresentationState.Selectable)
            .ToArray();

        Assert.That(selectable, Is.Not.Empty);
        foreach (RunMapNodeViewModel node in selectable)
        {
            MapDownstreamRoute expected = MapReachability.GetDownstreamRoute(
                map,
                new MapNodeId(node.NodeId));
            string[] expectedNodeIds = expected.NodeIds.Select(nodeId => nodeId.Value).ToArray();
            string[] expectedEdgeKeys = expected.Edges.Select(edge =>
                $"{edge.FromNodeId.Value}>{edge.ToNodeId.Value}").ToArray();

            Assert.That(node.DownstreamNodeIds, Is.EqualTo(expectedNodeIds));
            Assert.That(node.DownstreamEdgeKeys, Is.EqualTo(expectedEdgeKeys));
            Assert.That(node.DownstreamNodeIds, Does.Contain(node.NodeId));
            Assert.That(node.DownstreamNodeIds.Any(nodeId =>
                map.GetNode(new MapNodeId(nodeId)).Kind == MapNodeKind.Boss), Is.True);
        }

        foreach (RunMapNodeViewModel node in nonSelectable)
        {
            Assert.That(node.DownstreamNodeIds, Is.Empty);
            Assert.That(node.DownstreamEdgeKeys, Is.Empty);
        }
    }

    /// <summary>普通胜利检查点提交失败仍允许显式回退到上一成功档。</summary>
    [Test]
    public async Task OrdinaryCommitFailure_AllowsConfirmedRollbackToPreviousCheckpoint()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected ordinary checkpoint failure."));
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 808u, saves);
        flow.CreateNewRun(heroTemplateId: 1001);
        RunSaveDocument previousCheckpoint = saves.Load().Document;
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();
        MapNodeId combatNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Combat);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: combatNodeId));

        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Victory, settledHealth: 61);

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.True);
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));

        view.Emit(new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.RollbackConfirmation));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmRollback));

        Assert.That(store.Current, Is.Null);
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(view.LastModel.ContinueEnabled, Is.True);
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));
    }

    /// <summary>Terminal 提交失败只能重试保存，退出与确认回退动作都不得复活旧检查点。</summary>
    [Test]
    public async Task TerminalCommitFailure_DisablesRollbackAndPreservesTerminalRun()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected terminal checkpoint failure."));
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 909u, saves);
        flow.CreateNewRun(heroTemplateId: 1001);
        RunSaveDocument previousCheckpoint = saves.Load().Document;
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();
        MapNodeId combatNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Combat);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: combatNodeId));

        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Defeat, settledHealth: 0);
        RunState terminal = store.Current;

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(terminal.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));

        view.Emit(new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmRollback));

        Assert.That(store.Current, Is.SameAs(terminal));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));
    }

    /// <summary>创建带确定 Run 身份、配置、地图 seed 与可替换存档 port 的测试编排。</summary>
    private static RunFlowService CreateFlow(
        RunStateStore store,
        RecordingSceneFlow scenes,
        uint randomRootSeed,
        IRunSaveStore saveStore = null)
    {
        return new RunFlowService(
            store,
            CreateTables,
            scenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef")),
                randomRootSeed)),
            saveStore ?? new InMemoryRunSaveStore());
    }

    /// <summary>创建两名候选 Hero、两副起始牌组与 G3 profile 遭遇的最小 Luban 表。</summary>
    private static Tables CreateTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.machine_gunner.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":3,\"max_ammo\":6,\"ammo_gain_per_round\":1,\"runtime_profile\":1}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002]},{\"id\":1002,\"card_template_ids\":[3003]}]"),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"max_health\":20,\"base_strength\":0,\"view_prefab_key\":\"pfb_char_enemy\",\"behavior_group_id\":6001}," +
                "{\"id\":2101,\"name_i18n_key\":\"battle.enemy.test_sentry.name\",\"max_health\":30,\"base_strength\":0,\"view_prefab_key\":\"pfb_char_enemy\",\"behavior_group_id\":6101}]"),
            ["battle_tbencounter"] = JArray.Parse(
                "[{\"id\":5001,\"enemy_template_ids\":[2001]}," +
                "{\"id\":5002,\"enemy_template_ids\":[2101]}]"),
        };

        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>以稳定键映射模拟当前语言，并保留生命与缺失配置参数。</summary>
    private static string Localize(
        string key,
        IReadOnlyDictionary<string, object> arguments)
    {
        switch (key)
        {
            case "battle.hero.test_warrior.name":
                return "Warrior";
            case "battle.hero.machine_gunner.name":
                return "Machine Gunner";
            case "battle.enemy.test_slime.name":
                return "Test Slime";
            case "battle.enemy.test_sentry.name":
                return "Test Sentry";
            case "run.entry.map.health":
                return $"HP {arguments["current"]}/{arguments["max"]}";
            case "run.entry.save.issue.missing_configuration":
                return $"Missing {arguments["kind"]} {arguments["id"]}";
            default:
                return key;
        }
    }

    /// <summary>由真实生成器和初始路径创建一份可继续的 schema v2 文档。</summary>
    private static RunSaveDocument CreateMapReadyDocument()
    {
        using var store = new RunStateStore();
        return RunSaveDocumentMapper.Create(CreateDirectRun(store, randomRootSeed: 919191u));
    }

    /// <summary>由真实生成器、已承诺 Combat 与 Defeat 创建冷启动终局文档。</summary>
    private static RunSaveDocument CreateTerminalDocument()
    {
        using var store = new RunStateStore();
        RunState state = CreateDirectRun(store, randomRootSeed: 828282u);
        MapNodeId combatNodeId = MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary)
            .First();
        store.CommitNode(combatNodeId);
        RunBattleInput input = store.BeginCommittedBattle();
        RunState terminal = store.RecordDefeat(
            input.BattleId,
            state.HeroTemplateId,
            settledHealth: 0,
            state.MaxHealth);
        return RunSaveDocumentMapper.Create(terminal);
    }

    /// <summary>绕过场景编排，仅以当前 G3 profile 创建合法初始 Run 事实供文档测试使用。</summary>
    private static RunState CreateDirectRun(RunStateStore store, uint randomRootSeed)
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.Current,
            RunRandomDomains.DeriveMapSeed(randomRootSeed));
        return store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("13572468-2468-1357-2468-135724681357")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            randomRootSeed,
            map));
    }

    /// <summary>从完整地图投影中读取第一个指定状态与种类的节点身份。</summary>
    private static MapNodeId GetFirstProjectedNodeId(
        RunEntryViewModel model,
        RunMapNodePresentationState state,
        MapNodeKind kind)
    {
        RunMapNodeViewModel node = model.Map.Nodes.First(value =>
            value.State == state && value.Kind == kind);
        return new MapNodeId(node.NodeId);
    }

    /// <summary>消费当前唯一 Battle attempt，并发布指定胜负与结算生命。</summary>
    private static async Task CompleteActiveBattleAsync(
        RunFlowService flow,
        RunStateStore store,
        BattleResultKind kind,
        int settledHealth)
    {
        RunBattleInput input = store.Current.ActiveBattle;
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(
                kind,
                input.HeroTemplateId,
                settledHealth,
                input.MaxHealth));
    }

    /// <summary>冻结一个单玩家稳定 BattleResult，模拟队列完成表现后的唯一发布。</summary>
    private static BattleResult CreateBattleResult(
        BattleResultKind kind,
        int heroTemplateId,
        int health,
        int maxHealth)
    {
        return new BattleResult(
            kind,
            authoritySequence: 1,
            roundNumber: 1,
            new[]
            {
                new BattleResultPlayerSnapshot(
                    new CombatantId(1),
                    heroTemplateId,
                    health,
                    maxHealth),
            });
    }

    /// <summary>记录 View 唯一动作流与最后一次不可变投影。</summary>
    private sealed class RecordingRunEntryView : IRunEntryView
    {
        /// <summary>Presenter 订阅的唯一入口动作事件。</summary>
        public event Action<RunEntryAction> ActionRequested;

        /// <summary>最后一次收到的完整页面投影。</summary>
        public RunEntryViewModel LastModel { get; private set; }

        /// <summary>保存 Presenter 提交的完整投影。</summary>
        public void Render(RunEntryViewModel model)
        {
            LastModel = model;
        }

        /// <summary>模拟 UI 控件发布命令，不直接写 Run。</summary>
        public void Emit(RunEntryAction action)
        {
            ActionRequested?.Invoke(action);
        }
    }

    /// <summary>记录场景编排请求而不触发 Addressables。</summary>
    private sealed class RecordingSceneFlow : ISceneFlowService
    {
        /// <summary>按调用顺序保存目标场景地址。</summary>
        public List<string> LoadedAddresses { get; } = new List<string>();

        /// <summary>记录目标并同步完成测试期请求。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            LoadedAddresses.Add(targetSceneAddress);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>每次返回同一确定 Run 身份与根随机输入。</summary>
    private sealed class FixedRunEntropySource : IRunEntropySource
    {
        private readonly RunEntropy _entropy;

        /// <summary>冻结测试应返回的确定输入。</summary>
        public FixedRunEntropySource(RunEntropy entropy)
        {
            _entropy = entropy;
        }

        /// <summary>返回确定输入。</summary>
        public RunEntropy Next()
        {
            return _entropy;
        }
    }

    /// <summary>以脚本化提交结果保存最近成功文档，供失败与删除边界测试。</summary>
    private sealed class ScriptedRunSaveStore : IRunSaveStore
    {
        private readonly Queue<RunSaveCommitResult> _commitResults =
            new Queue<RunSaveCommitResult>();
        private RunSaveDocument _document;

        /// <summary>累计玩家确认后的删除次数。</summary>
        public int DeleteCount { get; private set; }

        /// <summary>创建空单槽。</summary>
        public ScriptedRunSaveStore()
        {
        }

        /// <summary>创建含最近成功检查点的单槽。</summary>
        public ScriptedRunSaveStore(RunSaveDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>安排下一次 commit 的确定结果。</summary>
        public void EnqueueCommitResult(RunSaveCommitResult result)
        {
            _commitResults.Enqueue(result ?? throw new ArgumentNullException(nameof(result)));
        }

        /// <summary>返回当前最近成功检查点或空槽。</summary>
        public RunSaveLoadResult Load()
        {
            return _document == null
                ? RunSaveLoadResult.NotFound()
                : RunSaveLoadResult.Succeeded(_document);
        }

        /// <summary>按队列返回结果，并只在成功时替换最近检查点。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            RunSaveCommitResult result = _commitResults.Count > 0
                ? _commitResults.Dequeue()
                : RunSaveCommitResult.Succeeded();
            if (result.Status == RunSaveCommitStatus.Success)
                _document = document;
            return result;
        }

        /// <summary>模拟玩家确认后的幂等删除。</summary>
        public RunSaveDeleteResult Delete()
        {
            DeleteCount++;
            _document = null;
            return RunSaveDeleteResult.Succeeded();
        }
    }
}
