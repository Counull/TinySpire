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

    /// <summary>有效当前 schema 地图档只启用 Continue，玩家确认后才重建同一冻结地图。</summary>
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

    /// <summary>稳定地图页主动放弃先经过确认，再投影唯一 Abandoned 结果并保留终局档到玩家返回主菜单。</summary>
    [Test]
    public void ActiveMapRun_RequestAbandonProjectsTypedOutcomeResult()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            randomRootSeed: 303u,
            saves);
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

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.CanAbandonActiveRun, Is.True);

        view.Emit(new RunEntryAction(RunEntryActionKind.RequestAbandon));

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.AbandonConfirmation));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));

        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmAbandon));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(store.Current.Outcome.Kind, Is.EqualTo(RunOutcomeKind.Abandoned));
        Assert.That(saves.Load().Document.OutcomeKind, Is.EqualTo(RunSaveOutcomeKind.Abandoned));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Failure));
        Assert.That(
            view.LastModel.GetText(RunEntryTextSlot.FailureTitle),
            Is.EqualTo("run.entry.outcome.abandoned"));
        Assert.That(
            view.LastModel.GetText(RunEntryTextSlot.LeaveRun),
            Is.EqualTo("run.entry.outcome.return_to_menu"));
        Assert.That(view.LastModel.ContinueEnabled, Is.False);
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
        RunMapNodeViewModel elite = projectedMap.Nodes
            .Single(node => node.Kind == MapNodeKind.Elite);
        RunMapNodeViewModel[] nonCombat = projectedMap.Nodes
            .Where(node => node.Kind == MapNodeKind.Rest ||
                           node.Kind == MapNodeKind.Chest ||
                           node.Kind == MapNodeKind.Shop ||
                           node.Kind == MapNodeKind.Event)
            .ToArray();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(frozenMap.ProfileId, Is.EqualTo(TinySpireActMapProfiles.NewRunG7V1ProfileId));
        Assert.That(frozenMap.GeneratorVersion, Is.EqualTo(ActMapGenerator.NewRunG6Version));
        Assert.That(
            frozenMap.Nodes.OrderBy(node => node.Layer).ThenBy(node => node.Slot)
                .Select(node => node.Kind),
            Is.EqualTo(new[]
            {
                MapNodeKind.Start,
                MapNodeKind.Combat,
                MapNodeKind.Rest,
                MapNodeKind.Chest,
                MapNodeKind.Shop,
                MapNodeKind.Event,
                MapNodeKind.Combat,
                MapNodeKind.Elite,
                MapNodeKind.Boss,
                MapNodeKind.Boss,
                MapNodeKind.Boss,
            }));
        Assert.That(projectedMap.Fingerprint, Is.EqualTo(frozenMap.Fingerprint));
        Assert.That(projectedMap.Nodes.Count, Is.EqualTo(frozenMap.Nodes.Count));
        Assert.That(projectedMap.Edges.Count, Is.EqualTo(frozenMap.Edges.Count));
        Assert.That(combats, Is.Not.Empty);
        Assert.That(combats.All(node => node.ContentId == 5001), Is.True);
        Assert.That(combats.All(node => node.DisplayName == "SLIME PATROL\nTest Slime"), Is.True);
        Assert.That(
            combats.All(node => node.VisualAnchorKind == RunMapVisualAnchorKind.EncounterSlimeSilhouette),
            Is.True);
        Assert.That(elite.ContentId, Is.EqualTo(5101));
        Assert.That(elite.DisplayName, Is.EqualTo("ELITE GUARDIAN\nTest Sentry"));
        Assert.That(
            elite.VisualAnchorKind,
            Is.EqualTo(RunMapVisualAnchorKind.EncounterSentrySilhouette));
        Assert.That(nonCombat.Select(node => node.DisplayName),
            Is.EqualTo(new[] { "REST", "CHEST", "SHOP", "EVENT" }));
        Assert.That(nonCombat.Select(node => node.VisualAnchorKind),
            Is.EqualTo(new[]
            {
                RunMapVisualAnchorKind.RestCampfire,
                RunMapVisualAnchorKind.ChestCache,
                RunMapVisualAnchorKind.ShopBag,
                RunMapVisualAnchorKind.EventQuestionMark,
            }));
        Assert.That(bosses.Length, Is.EqualTo(TinySpireActMapProfiles.NewRunG7V1.BossEndpointCount));
        Assert.That(
            bosses.Select(node => node.ContentId).Distinct().Count(),
            Is.EqualTo(TinySpireActMapProfiles.NewRunG7V1.BossCandidateCount));
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
            Is.EqualTo(TinySpireActMapProfiles.NewRunG7V1.BossCandidateCount));

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

    /// <summary>G6 四类非战斗程序化内容必须各自解析唯一名称与锚点，并拒绝同 kind 的伪造内容 ID。</summary>
    [TestCase(MapNodeKind.Rest, 7101, "REST", RunMapVisualAnchorKind.RestCampfire)]
    [TestCase(MapNodeKind.Chest, 7201, "CHEST", RunMapVisualAnchorKind.ChestCache)]
    [TestCase(MapNodeKind.Shop, 7301, "SHOP", RunMapVisualAnchorKind.ShopBag)]
    [TestCase(MapNodeKind.Event, 7401, "EVENT", RunMapVisualAnchorKind.EventQuestionMark)]
    public void IdentityCatalog_G6NonCombatAnchorsResolveExactProgrammaticIdentity(
        MapNodeKind kind,
        int contentId,
        string expectedName,
        RunMapVisualAnchorKind expectedAnchor)
    {
        var catalog = new RunMapIdentityCatalog(CreateTables, Localize);

        RunMapIdentityDescriptor descriptor = catalog.Resolve(kind, contentId);

        Assert.That(descriptor.DisplayName, Is.EqualTo(expectedName));
        Assert.That(descriptor.VisualAnchorKind, Is.EqualTo(expectedAnchor));
        Assert.Throws<InvalidOperationException>(() => catalog.Resolve(kind, contentId + 1));
    }

    /// <summary>八层 mixed 单路线与三 BossGate 必须全部留在宿主内，任意节点矩形不得重叠。</summary>
    [Test]
    public void MapLayout_G6MixedRouteFitsHostWithoutNodeOverlap()
    {
        RunMapNodeViewModel[] nodes = CreateMixedMapNodeViewModels();

        IReadOnlyList<RunMapNodeLayout> layouts = RunMapLayout.Build(nodes);

        Assert.That(layouts, Has.Count.EqualTo(nodes.Length));
        foreach (RunMapNodeLayout layout in layouts)
        {
            Assert.That(layout.Left, Is.GreaterThanOrEqualTo(-410f), layout.NodeId);
            Assert.That(layout.Right, Is.LessThanOrEqualTo(410f), layout.NodeId);
            Assert.That(layout.Bottom, Is.GreaterThanOrEqualTo(-240f), layout.NodeId);
            Assert.That(layout.Top, Is.LessThanOrEqualTo(240f), layout.NodeId);
        }

        for (int leftIndex = 0; leftIndex < layouts.Count; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < layouts.Count; rightIndex++)
            {
                RunMapNodeLayout left = layouts[leftIndex];
                RunMapNodeLayout right = layouts[rightIndex];
                bool separated = left.Right <= right.Left ||
                                 right.Right <= left.Left ||
                                 left.Top <= right.Bottom ||
                                 right.Top <= left.Bottom;
                Assert.That(separated, Is.True, $"{left.NodeId} overlaps {right.NodeId}");
            }
        }
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

    /// <summary>战斗胜利跳过冻结奖励后推进当前位置，并只投影新当前位置的普通直接出边。</summary>
    [Test]
    public async Task VictoryThenSkip_ProjectsNewCurrentNodeAndNextSelectableLayer()
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
        SkipPendingReward(view, store);

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

    /// <summary>普通胜利投影同一冻结奖励；语言刷新不换候选，选择动作精确结算对应模板。</summary>
    [Test]
    public async Task RewardPending_ProjectsFrozenCandidatesAndRoutesExactSelection()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var localizer = new MutableCardLocalizer();
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 313u);
        flow.CreateNewRun(heroTemplateId: 1001);
        int deckCountBeforeReward = store.Current.RunDeck.Cards.Count;
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            localizer.Translate,
            localeChanges);
        presenter.Initialize();
        MapNodeId combatNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Combat);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: combatNodeId));

        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Victory, settledHealth: 67);

        PendingCardReward pending = store.Current.PendingCardReward;
        RunCardRewardViewModel firstProjection = view.LastModel.CardReward;
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.CardReward));
        Assert.That(firstProjection.RewardId, Is.EqualTo(pending.Id));
        Assert.That(
            firstProjection.Candidates.Select(candidate => candidate.TemplateId),
            Is.EqualTo(pending.CandidateTemplateIds));
        Assert.That(firstProjection.Candidates.All(candidate => candidate.Name.StartsWith("EN:")), Is.True);

        localizer.Language = "ZH";
        localeChanges.OnNext(null);

        Assert.That(view.LastModel.CardReward.RewardId, Is.EqualTo(pending.Id));
        Assert.That(
            view.LastModel.CardReward.Candidates.Select(candidate => candidate.TemplateId),
            Is.EqualTo(pending.CandidateTemplateIds));
        Assert.That(
            view.LastModel.CardReward.Candidates.All(candidate => candidate.Name.StartsWith("ZH:")),
            Is.True);

        int selectedTemplateId = pending.CandidateTemplateIds[1];
        view.Emit(new RunEntryAction(
            RunEntryActionKind.SelectCardReward,
            cardRewardId: pending.Id,
            cardTemplateId: selectedTemplateId));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.RunDeck.Cards, Has.Count.EqualTo(deckCountBeforeReward + 1));
        Assert.That(store.Current.RunDeck.Cards.Last().TemplateId, Is.EqualTo(selectedTemplateId));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
    }

    /// <summary>当前 Run 持有物必须按领域顺序投影配置名称、带精确参数的描述与当前语言金币文本。</summary>
    [Test]
    public void CurrentRun_ProjectsLocalizedHoldingsInStableDomainOrder()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var localizer = new MutableHoldingsLocalizer();
        var holdings = new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(7), 8002),
                new RunRelic(new RunRelicInstanceId(3), 8001),
            },
            new[]
            {
                new RunPotion(new RunPotionInstanceId(9), 9002),
                new RunPotion(new RunPotionInstanceId(4), 9001),
                new RunPotion(new RunPotionInstanceId(12), 9002),
            },
            gold: 321);
        CreateDirectRun(store, randomRootSeed: 414141u, holdings: holdings);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 414141u),
            CreateTables,
            localizer.Translate,
            localeChanges);

        presenter.Initialize();

        Assert.That(view.LastModel.Holdings.GoldText, Is.EqualTo("EN:GOLD:321"));
        Assert.That(view.LastModel.Holdings.RelicsTitle, Is.EqualTo("EN:RELICS"));
        Assert.That(view.LastModel.Holdings.PotionsTitle, Is.EqualTo("EN:POTIONS"));
        Assert.That(view.LastModel.Holdings.EmptyText, Is.EqualTo("EN:EMPTY"));
        Assert.That(
            view.LastModel.Holdings.Relics.Select(item => item.TemplateId),
            Is.EqualTo(new[] { 8002, 8001 }));
        Assert.That(
            view.LastModel.Holdings.Relics.Select(item => item.Name),
            Is.EqualTo(new[] { "EN:Relic Two", "EN:Relic One" }));
        Assert.That(
            view.LastModel.Holdings.Relics.Select(item => item.Description),
            Is.EqualTo(new[] { "EN:Strength 3", "EN:Strength 1" }));
        Assert.That(
            view.LastModel.Holdings.Potions.Select(item => item.TemplateId),
            Is.EqualTo(new[] { 9002, 9001, 9002 }));
        Assert.That(
            view.LastModel.Holdings.Potions.Select(item => item.Description),
            Is.EqualTo(new[] { "EN:Heal 25", "EN:Heal 10", "EN:Heal 25" }));

        localizer.Language = "ZH";
        localeChanges.OnNext(null);

        Assert.That(view.LastModel.Holdings.GoldText, Is.EqualTo("ZH:GOLD:321"));
        Assert.That(
            view.LastModel.Holdings.Relics.Select(item => item.TemplateId),
            Is.EqualTo(new[] { 8002, 8001 }));
        Assert.That(
            view.LastModel.Holdings.Potions.Select(item => item.TemplateId),
            Is.EqualTo(new[] { 9002, 9001, 9002 }));
        Assert.That(
            view.LastModel.Holdings.Potions.Select(item => item.Name),
            Is.EqualTo(new[] { "ZH:Potion Two", "ZH:Potion One", "ZH:Potion Two" }));
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
        CreateDirectRun(store, randomRootSeed: 404u);
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
            SkipPendingReward(view, store);
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

    /// <summary>G7 BossGateReached 的当前 Boss 节点可再次点击，并只启动清单登记的真实 Boss Battle。</summary>
    [Test]
    public async Task G7BossGate_CurrentBossActionStartsManifestBattle()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var saves = new ScriptedRunSaveStore();
        RunState bossGate = CreateG7BossGateRun(store, randomRootSeed: 414u);
        saves.Commit(RunSaveDocumentMapper.Create(bossGate));
        var flow = CreateFlow(store, scenes, randomRootSeed: 414u, saves);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();

        RunMapNodeViewModel boss = view.LastModel.Map.Nodes.Single(node =>
            node.State == RunMapNodePresentationState.BossGateReached);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: new MapNodeId(boss.NodeId)));
        await UniTask.Yield();

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(store.Current.ActiveBattle.NodeKind, Is.EqualTo(MapNodeKind.Boss));
        Assert.That(store.Current.ActiveBattle.EncounterTemplateId, Is.EqualTo(5201));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
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

    /// <summary>冻结奖励提交失败保持 InBattle 来源，只能重试同一文档并在成功后发布 RewardPending。</summary>
    [Test]
    public async Task RewardPendingCommitFailure_DisablesRollbackAndPreservesFrozenReward()
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
        RunState failedSource = store.Current;
        RunSaveDocument failedDocument = saves.CommitAttempts.Last();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(store.Current.PendingCardReward, Is.Null);
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));

        view.Emit(new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmRollback));

        Assert.That(store.Current, Is.SameAs(failedSource));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(saves.CommitAttempts.Last(), Is.SameAs(failedDocument));
        Assert.That(store.Current, Is.Not.SameAs(failedSource));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.CardReward));
        Assert.That(
            view.LastModel.CardReward.RewardId.ToString(),
            Is.EqualTo(failedDocument.PendingCardReward.RewardId));
        Assert.That(
            view.LastModel.CardReward.Candidates.Select(candidate => candidate.TemplateId),
            Is.EqualTo(failedDocument.PendingCardReward.CandidateTemplateIds));
        Assert.That(saves.Load().Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
    }

    /// <summary>非战斗进入存档失败必须隐藏 rollback，并保留同一节点进入文档直到 retry 成功。</summary>
    [Test]
    public async Task NodeVisitEntryCommitFailure_DisablesRollbackAndRetriesExactPending()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            randomRootSeed: 818u,
            saves);
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
        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Victory, settledHealth: 61);
        SkipPendingReward(view, store);
        MapNodeId restNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Rest);
        RunState beforeEntry = store.Current;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected node-entry checkpoint failure."));

        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: restNodeId));
        RunSaveDocument failedDocument = saves.CommitAttempts.Last();

        Assert.That(store.Current, Is.SameAs(beforeEntry));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(failedDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        view.Emit(new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmRollback));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(store.Current, Is.SameAs(beforeEntry));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(saves.CommitAttempts.Last(), Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(store.Current.PendingNodeVisit.NodeId, Is.EqualTo(restNodeId));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Rest));
    }

    /// <summary>Rest Pending 投影冻结治疗与有序升级动作，治疗成功后只响应式返回地图。</summary>
    [Test]
    public async Task RestPending_ProjectsTypedChoicesAndHealActionCompletesWithoutNavigation()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(store, scenes, randomRootSeed: 828u, saves);
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
        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Victory, settledHealth: 61);
        SkipPendingReward(view, store);
        MapNodeId restNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Rest);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: restNodeId));
        PendingRunNodeVisit pending = store.Current.PendingNodeVisit;
        int sceneCountBeforeRestChoice = scenes.LoadedAddresses.Count;

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Rest));
        Assert.That(view.LastModel.Rest, Is.Not.Null);
        Assert.That(view.LastModel.Rest.VisitId, Is.EqualTo(pending.Id));
        Assert.That(view.LastModel.Rest.HealAmount, Is.EqualTo(24));
        Assert.That(view.LastModel.Rest.HealEnabled, Is.True);
        Assert.That(view.LastModel.Rest.HealText, Is.EqualTo("Heal 24 HP"));
        Assert.That(
            view.LastModel.Rest.UpgradeCandidates.Select(candidate => candidate.CardInstanceId),
            Is.EqualTo(pending.RestPayload.UpgradeCandidateInstanceIds));
        Assert.That(view.LastModel.Rest.UpgradeCandidates.Single().Text,
            Is.EqualTo("Upgrade Test Strike to +1"));
        Assert.That(view.LastModel.Rest.UpgradeCandidates.Single().Enabled, Is.True);

        view.Emit(new RunEntryAction(
            RunEntryActionKind.HealAtRest,
            nodeVisitId: pending.Id));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(80));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(restNodeId));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.Rest, Is.Null);
        Assert.That(scenes.LoadedAddresses.Count, Is.EqualTo(sceneCountBeforeRestChoice));
    }

    /// <summary>Rest 升级保存失败时页面锁定同一 Pending，Retry 复用文档后才升级并返回地图。</summary>
    [Test]
    public async Task RestUpgradeCommitFailure_DisablesChoicesAndRetriesExactDocumentBeforePublishing()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(store, scenes, randomRootSeed: 838u, saves);
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
        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Victory, settledHealth: 61);
        SkipPendingReward(view, store);
        MapNodeId restNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Rest);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: restNodeId));
        RunState pendingState = store.Current;
        RunRestUpgradeCandidateViewModel candidate = view.LastModel.Rest.UpgradeCandidates.Single();
        int sceneCountBeforeChoice = scenes.LoadedAddresses.Count;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected Rest settlement checkpoint failure."));

        view.Emit(new RunEntryAction(
            RunEntryActionKind.UpgradeCardAtRest,
            nodeVisitId: pendingState.PendingNodeVisit.Id,
            cardInstanceId: candidate.CardInstanceId));
        RunSaveDocument failedDocument = saves.CommitAttempts.Last();
        int commitCountAfterFailure = saves.CommitAttempts.Count;

        Assert.That(store.Current, Is.SameAs(pendingState));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(view.LastModel.Rest.HealEnabled, Is.False);
        Assert.That(view.LastModel.Rest.UpgradeCandidates.All(value => !value.Enabled), Is.True);
        Assert.That(
            failedDocument.RunCards.Single(card => card.InstanceId == candidate.CardInstanceId.Sequence)
                .UpgradeLevel,
            Is.EqualTo(1));
        view.Emit(new RunEntryAction(
            RunEntryActionKind.HealAtRest,
            nodeVisitId: pendingState.PendingNodeVisit.Id));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(commitCountAfterFailure));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(saves.CommitAttempts.Last(), Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(61));
        Assert.That(
            store.Current.RunDeck.Cards.Single(card => card.InstanceId == candidate.CardInstanceId)
                .UpgradeLevel,
            Is.EqualTo(1));
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(restNodeId));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(scenes.LoadedAddresses.Count, Is.EqualTo(sceneCountBeforeChoice));
    }

    /// <summary>Chest Pending 投影冻结药水与双动作，领取保存失败锁定选择并以同一文档重试后返回地图。</summary>
    [Test]
    public async Task ChestClaimCommitFailure_ProjectsFrozenPotionAndRetriesExactDocumentBeforePublishing()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(store, scenes, randomRootSeed: 848u, saves);
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
        await CompleteActiveBattleAsync(flow, store, BattleResultKind.Victory, settledHealth: 61);
        SkipPendingReward(view, store);
        MapNodeId restNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Rest);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: restNodeId));
        view.Emit(new RunEntryAction(
            RunEntryActionKind.HealAtRest,
            nodeVisitId: store.Current.PendingNodeVisit.Id));
        MapNodeId chestNodeId = GetFirstProjectedNodeId(
            view.LastModel,
            RunMapNodePresentationState.Selectable,
            MapNodeKind.Chest);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.EnterMapNode,
            mapNodeId: chestNodeId));
        RunState pendingState = store.Current;
        int potionCountBeforeChestClaim = pendingState.Holdings.Potions.Count;
        int sceneCountBeforeChoice = scenes.LoadedAddresses.Count;

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Chest));
        Assert.That(view.LastModel.Chest, Is.Not.Null);
        Assert.That(view.LastModel.Chest.VisitId, Is.EqualTo(pendingState.PendingNodeVisit.Id));
        Assert.That(view.LastModel.Chest.Potion.TemplateId,
            Is.EqualTo(RunNodeVisitIdentityCatalog.SamplePotionTemplateId));
        Assert.That(view.LastModel.Chest.Potion.Name, Is.EqualTo("Healing Potion"));
        Assert.That(view.LastModel.Chest.Potion.Description, Is.EqualTo("Restore 10 HP"));
        Assert.That(view.LastModel.Chest.ClaimText, Is.EqualTo("Claim"));
        Assert.That(view.LastModel.Chest.SkipText, Is.EqualTo("Skip"));
        Assert.That(view.LastModel.Chest.ClaimEnabled, Is.True);
        Assert.That(view.LastModel.Chest.SkipEnabled, Is.True);
        Assert.That(view.LastModel.Chest.IsCapacityFull, Is.False);
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected Chest settlement checkpoint failure."));

        view.Emit(new RunEntryAction(
            RunEntryActionKind.ClaimChest,
            nodeVisitId: pendingState.PendingNodeVisit.Id));
        RunSaveDocument failedDocument = saves.CommitAttempts.Last();
        int commitCountAfterFailure = saves.CommitAttempts.Count;

        Assert.That(store.Current, Is.SameAs(pendingState));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(view.LastModel.Chest.ClaimEnabled, Is.False);
        Assert.That(view.LastModel.Chest.SkipEnabled, Is.False);
        Assert.That(failedDocument.Potions.Last().TemplateId,
            Is.EqualTo(RunNodeVisitIdentityCatalog.SamplePotionTemplateId));
        view.Emit(new RunEntryAction(
            RunEntryActionKind.SkipChest,
            nodeVisitId: pendingState.PendingNodeVisit.Id));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(commitCountAfterFailure));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(saves.CommitAttempts.Last(), Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.Holdings.Potions, Has.Count.EqualTo(potionCountBeforeChestClaim + 1));
        Assert.That(store.Current.Holdings.Potions.Last().TemplateId,
            Is.EqualTo(RunNodeVisitIdentityCatalog.SamplePotionTemplateId));
        Assert.That(store.Current.PathNodeIds.Last(), Is.EqualTo(chestNodeId));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.Chest, Is.Null);
        Assert.That(scenes.LoadedAddresses.Count, Is.EqualTo(sceneCountBeforeChoice));
    }

    /// <summary>药水三槽已满时 Chest 只禁用领取并显示提示，跳过仍可完成节点且没有返回动作。</summary>
    [Test]
    public void ChestAtCapacity_DisablesClaimButKeepsSkipAvailable()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Chest,
            RunNodeVisitIdentityCatalog.ChestContentId);
        var holdings = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 9002),
                new RunPotion(new RunPotionInstanceId(2), templateId: 9002),
                new RunPotion(new RunPotionInstanceId(3), templateId: 9002),
            },
            gold: 100);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("30303030-5050-7272-9494-161616161616")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: holdings));
        store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new TablesRunSaveConfigurationCatalog(CreateTables())));
        using var localeChanges = new Subject<Locale>();
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            randomRootSeed: 858u,
            saveStore: new InMemoryRunSaveStore());
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Chest));
        Assert.That(view.LastModel.Chest.IsCapacityFull, Is.True);
        Assert.That(view.LastModel.Chest.CapacityFullText, Is.EqualTo("Potion belt is full"));
        Assert.That(view.LastModel.Chest.ClaimEnabled, Is.False);
        Assert.That(view.LastModel.Chest.SkipEnabled, Is.True);
        Assert.Throws<InvalidOperationException>(() => view.Emit(new RunEntryAction(
            RunEntryActionKind.ClaimChest,
            nodeVisitId: store.Current.PendingNodeVisit.Id)));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));

        view.Emit(new RunEntryAction(
            RunEntryActionKind.SkipChest,
            nodeVisitId: store.Current.PendingNodeVisit.Id));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.Holdings.Potions, Has.Count.EqualTo(3));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
    }

    /// <summary>Shop 投影按冻结顺序展示三类库存，已持有遗物禁用，购买后留在 Shop 且 Leave 才完成路径。</summary>
    [Test]
    public void ShopPending_ProjectsOwnedRelicPurchasesPotionThenLeavesWithoutNavigation()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Shop,
            RunNodeVisitIdentityCatalog.ShopContentId);
        var holdings = new RunHoldings(
            new[]
            {
                new RunRelic(
                    new RunRelicInstanceId(1),
                    RunNodeVisitIdentityCatalog.SampleRelicTemplateId),
            },
            Array.Empty<RunPotion>(),
            gold: 100);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("39393939-6161-7d7d-afaf-272727272727")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: holdings));
        Tables tables = CreateTables();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new TablesRunSaveConfigurationCatalog(tables)));
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(
            store,
            scenes,
            randomRootSeed: 868u,
            saveStore: new InMemoryRunSaveStore());
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            () => tables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Shop));
        Assert.That(view.LastModel.Shop.VisitId, Is.EqualTo(pending.PendingNodeVisit.Id));
        Assert.That(view.LastModel.Shop.Entries.Select(entry => entry.Kind), Is.EqualTo(new[]
        {
            RunShopStockKind.Relic,
            RunShopStockKind.Potion,
            RunShopStockKind.Card,
        }));
        Assert.That(view.LastModel.Shop.Entries[0].ItemName, Is.EqualTo("Relic One"));
        Assert.That(view.LastModel.Shop.Entries[0].PurchaseEnabled, Is.False);
        Assert.That(view.LastModel.Shop.Entries[1].ItemName, Is.EqualTo("Healing Potion"));
        Assert.That(view.LastModel.Shop.Entries[1].Text,
            Is.EqualTo("Buy Healing Potion — 25 Gold"));
        Assert.That(view.LastModel.Shop.Entries[1].PurchaseEnabled, Is.True);
        Assert.That(view.LastModel.Shop.Entries[2].PurchaseEnabled, Is.True);
        Assert.That(view.LastModel.Shop.LeaveText, Is.EqualTo("Leave"));
        Assert.That(view.LastModel.Shop.LeaveEnabled, Is.True);
        int sceneCountBeforeChoice = scenes.LoadedAddresses.Count;

        view.Emit(new RunEntryAction(
            RunEntryActionKind.PurchaseShopStock,
            nodeVisitId: pending.PendingNodeVisit.Id,
            shopStockEntryId: 2));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(75));
        Assert.That(store.Current.Holdings.Potions.Single().TemplateId,
            Is.EqualTo(RunNodeVisitIdentityCatalog.SamplePotionTemplateId));
        Assert.That(store.Current.PathNodeIds, Is.EqualTo(pending.PathNodeIds));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Shop));
        Assert.That(view.LastModel.Shop.Entries[1].Purchased, Is.True);
        Assert.That(view.LastModel.Shop.Entries[1].PurchaseEnabled, Is.False);
        Assert.That(view.LastModel.Shop.Entries[1].Text,
            Is.EqualTo("Healing Potion — Purchased"));

        view.Emit(new RunEntryAction(
            RunEntryActionKind.LeaveShop,
            nodeVisitId: store.Current.PendingNodeVisit.Id));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.PathNodeIds.Last(),
            Is.EqualTo(MapNodeId.FromPosition(layer: 1, slot: 0)));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.Shop, Is.Null);
        Assert.That(scenes.LoadedAddresses.Count, Is.EqualTo(sceneCountBeforeChoice));
    }

    /// <summary>Shop 购买保存失败时保留原 Pending 并锁死购买、Leave 与回退，Retry 后才发布同一已购后继。</summary>
    [Test]
    public void ShopPurchaseCommitFailure_DisablesAllActionsAndRetriesExactDocument()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Shop,
            RunNodeVisitIdentityCatalog.ShopContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("40404040-6262-7e7e-b0b0-282828282828")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 100)));
        Tables tables = CreateTables();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new TablesRunSaveConfigurationCatalog(tables)));
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(store, scenes, randomRootSeed: 878u, saves);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            () => tables,
            Localize,
            localeChanges);
        presenter.Initialize();
        int originalDeckCount = pending.RunDeck.Cards.Count;
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected Shop settlement checkpoint failure."));

        view.Emit(new RunEntryAction(
            RunEntryActionKind.PurchaseShopStock,
            nodeVisitId: pending.PendingNodeVisit.Id,
            shopStockEntryId: 3));
        RunSaveDocument failedDocument = saves.CommitAttempts.Last();
        int commitCountAfterFailure = saves.CommitAttempts.Count;

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(view.LastModel.Shop.Entries.All(entry => !entry.PurchaseEnabled), Is.True);
        Assert.That(view.LastModel.Shop.LeaveEnabled, Is.False);
        Assert.That(failedDocument.RunCards, Has.Count.EqualTo(originalDeckCount + 1));
        Assert.That(failedDocument.PendingNodeVisit.ShopPayload.Entries[2].Purchased, Is.True);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.PurchaseShopStock,
            nodeVisitId: pending.PendingNodeVisit.Id,
            shopStockEntryId: 2));
        view.Emit(new RunEntryAction(
            RunEntryActionKind.LeaveShop,
            nodeVisitId: pending.PendingNodeVisit.Id));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(commitCountAfterFailure));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(saves.CommitAttempts.Last(), Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(store.Current.RunDeck.Cards, Has.Count.EqualTo(originalDeckCount + 1));
        Assert.That(store.Current.PendingNodeVisit.ShopPayload.Entries[2].Purchased, Is.True);
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Shop));
        Assert.That(view.LastModel.Shop.Entries[2].PurchaseEnabled, Is.False);
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>冻结卡仍全局存在但移出 Hero 池或配置缺失时，Shop 只禁用卡项并保留身份占位。</summary>
    [Test]
    public void ShopCardConfigurationDrift_DisablesOnlyCardWithoutBreakingPage()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Shop,
            RunNodeVisitIdentityCatalog.ShopContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("41414141-6363-7f7f-b1b1-292929292929")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 100)));
        Tables currentTables = CreateTables();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new TablesRunSaveConfigurationCatalog(currentTables)));
        int frozenCardTemplateId = pending.PendingNodeVisit.ShopPayload.Entries[2].TemplateId;
        using var localeChanges = new Subject<Locale>();
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            randomRootSeed: 888u,
            saveStore: new InMemoryRunSaveStore());
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            () => currentTables,
            Localize,
            localeChanges);
        presenter.Initialize();
        Assert.That(view.LastModel.Shop.Entries[2].PurchaseEnabled, Is.True);

        currentTables = CreateTables(includeHero1001ShopCards: false);
        localeChanges.OnNext(null);

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Shop));
        Assert.That(view.LastModel.Shop.Entries[1].PurchaseEnabled, Is.True);
        Assert.That(view.LastModel.Shop.Entries[2].TemplateId, Is.EqualTo(frozenCardTemplateId));
        Assert.That(view.LastModel.Shop.Entries[2].PurchaseEnabled, Is.False);

        currentTables = CreateTables(
            includeHero1001ShopCards: true,
            missingCardTemplateId: frozenCardTemplateId);
        localeChanges.OnNext(null);

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Shop));
        Assert.That(view.LastModel.Shop.Entries[2].ItemName,
            Is.EqualTo($"#{frozenCardTemplateId}"));
        Assert.That(view.LastModel.Shop.Entries[2].PurchaseEnabled, Is.False);
    }

    /// <summary>Event 页投影冻结双选择，并只通过类型化 choice 完成路径而不触发场景导航。</summary>
    [Test]
    public void EventPending_ProjectsFrozenChoicesAndSettlesPaidHealWithoutNavigation()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Event,
            RunNodeVisitIdentityCatalog.EventContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("44444444-6666-8282-b4b4-323232323232")),
            heroTemplateId: 1001,
            initialHealth: 76,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 25)));
        Tables tables = CreateTables();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new TablesRunSaveConfigurationCatalog(tables)));
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(
            store,
            scenes,
            randomRootSeed: 898u,
            saveStore: new InMemoryRunSaveStore());
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            () => tables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Event));
        Assert.That(view.LastModel.Event.VisitId, Is.EqualTo(pending.PendingNodeVisit.Id));
        Assert.That(view.LastModel.Event.GainGoldAmount, Is.EqualTo(50));
        Assert.That(view.LastModel.Event.PaidHealCost, Is.EqualTo(25));
        Assert.That(view.LastModel.Event.PaidHealAmount, Is.EqualTo(15));
        Assert.That(view.LastModel.Event.GainGoldText, Is.EqualTo("Gain 50 Gold"));
        Assert.That(view.LastModel.Event.PaidHealText,
            Is.EqualTo("Pay 25 Gold to heal up to 15 HP"));
        Assert.That(view.LastModel.Event.GainGoldEnabled, Is.True);
        Assert.That(view.LastModel.Event.PaidHealEnabled, Is.True);
        int sceneCountBeforeChoice = scenes.LoadedAddresses.Count;

        view.Emit(new RunEntryAction(
            RunEntryActionKind.ChooseEvent,
            nodeVisitId: pending.PendingNodeVisit.Id,
            eventChoice: RunEventChoiceKind.PaidHeal));

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.PendingNodeVisit, Is.Null);
        Assert.That(store.Current.Holdings.Gold, Is.Zero);
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(80));
        Assert.That(store.Current.PathNodeIds.Last(),
            Is.EqualTo(MapNodeId.FromPosition(layer: 1, slot: 0)));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.Event, Is.Null);
        Assert.That(scenes.LoadedAddresses.Count, Is.EqualTo(sceneCountBeforeChoice));
    }

    /// <summary>Event 表现按 checked 金币上界、余额与满血事实独立门禁两项选择。</summary>
    [TestCase(24, 65, true, false)]
    [TestCase(25, 80, true, false)]
    [TestCase(int.MaxValue, 65, false, true)]
    public void EventPending_ProjectsOverflowBalanceAndFullHealthGates(
        int gold,
        int currentHealth,
        bool expectedGainEnabled,
        bool expectedPaidHealEnabled)
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Event,
            RunNodeVisitIdentityCatalog.EventContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("45454545-6767-8383-b5b5-333333333333")),
            heroTemplateId: 1001,
            initialHealth: currentHealth,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: gold)));
        Tables tables = CreateTables();
        store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new TablesRunSaveConfigurationCatalog(tables)));
        using var localeChanges = new Subject<Locale>();
        var flow = CreateFlow(
            store,
            new RecordingSceneFlow(),
            randomRootSeed: 899u,
            saveStore: new InMemoryRunSaveStore());
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            () => tables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Event));
        Assert.That(view.LastModel.Event.GainGoldEnabled, Is.EqualTo(expectedGainEnabled));
        Assert.That(view.LastModel.Event.PaidHealEnabled,
            Is.EqualTo(expectedPaidHealEnabled));
    }

    /// <summary>Event 保存失败时双选择与回退全部锁定，Retry 必须发布同一冻结完成文档。</summary>
    [Test]
    public void EventChoiceCommitFailure_DisablesBothChoicesAndRetriesExactDocument()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Event,
            RunNodeVisitIdentityCatalog.EventContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("46464646-6868-8484-b6b6-343434343434")),
            heroTemplateId: 1001,
            initialHealth: 60,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 100)));
        Tables tables = CreateTables();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new TablesRunSaveConfigurationCatalog(tables)));
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var saves = new ScriptedRunSaveStore();
        var flow = CreateFlow(store, scenes, randomRootSeed: 900u, saves);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            () => tables,
            Localize,
            localeChanges);
        presenter.Initialize();
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected Event settlement checkpoint failure."));

        view.Emit(new RunEntryAction(
            RunEntryActionKind.ChooseEvent,
            nodeVisitId: pending.PendingNodeVisit.Id,
            eventChoice: RunEventChoiceKind.GainGold));
        RunSaveDocument failedDocument = saves.CommitAttempts.Last();
        int commitCountAfterFailure = saves.CommitAttempts.Count;

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(view.LastModel.Event.GainGoldEnabled, Is.False);
        Assert.That(view.LastModel.Event.PaidHealEnabled, Is.False);
        Assert.That(failedDocument.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(failedDocument.Gold, Is.EqualTo(150));
        Assert.That(failedDocument.PendingNodeVisit, Is.Null);
        view.Emit(new RunEntryAction(
            RunEntryActionKind.ChooseEvent,
            nodeVisitId: pending.PendingNodeVisit.Id,
            eventChoice: RunEventChoiceKind.PaidHeal));
        Assert.That(saves.CommitAttempts, Has.Count.EqualTo(commitCountAfterFailure));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(saves.CommitAttempts.Last(), Is.SameAs(failedDocument));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(150));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.Event, Is.Null);
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>Terminal 提交失败保持 InBattle 来源，退出动作无效且 retry 成功后才发布终局。</summary>
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
        RunState failedSource = store.Current;
        RunSaveDocument failedDocument = saves.CommitAttempts.Last();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(failedSource.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));

        view.Emit(new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmRollback));

        Assert.That(store.Current, Is.SameAs(failedSource));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.CanRollbackFailedSave, Is.False);
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(saves.CommitAttempts.Last(), Is.SameAs(failedDocument));
        Assert.That(store.Current, Is.Not.SameAs(failedSource));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Failure));
        Assert.That(saves.Load().Document, Is.SameAs(failedDocument));
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
        return CreateTables(includeHero1001ShopCards: true, missingCardTemplateId: null);
    }

    /// <summary>创建可模拟 Hero 奖励池漂移或指定卡配置缺失的 Presenter 测试表。</summary>
    private static Tables CreateTables(
        bool includeHero1001ShopCards,
        int? missingCardTemplateId = null)
    {
        string warriorRewardCards = includeHero1001ShopCards
            ? "[3105,3123,3157]"
            : "[3206,3227,3264]";
        JObject[] cardRows = new[]
            {
                CreateTestCardRow(3002, rarity: 0),
                CreateTestCardRow(3003, rarity: 0),
                CreateTestCardRow(3105, rarity: 1),
                CreateTestCardRow(3123, rarity: 2),
                CreateTestCardRow(3157, rarity: 3),
                CreateTestCardRow(3206, rarity: 1),
                CreateTestCardRow(3227, rarity: 2),
                CreateTestCardRow(3264, rarity: 3),
            }
            .Where(row => !missingCardTemplateId.HasValue ||
                          row.Value<int>("id") != missingCardTemplateId.Value)
            .ToArray();
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                $"[{{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0,\"reward_card_template_ids\":{warriorRewardCards},\"reward_common_weight\":60,\"reward_uncommon_weight\":37,\"reward_rare_weight\":3}}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.machine_gunner.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":3,\"max_ammo\":6,\"ammo_gain_per_round\":1,\"runtime_profile\":1,\"reward_card_template_ids\":[3206,3227,3264],\"reward_common_weight\":60,\"reward_uncommon_weight\":37,\"reward_rare_weight\":3}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002]},{\"id\":1002,\"card_template_ids\":[3003]}]"),
            ["battle_tbcard"] = new JArray(cardRows),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6}]"),
            ["battle_tbcardupgradelevel"] = JArray.Parse(
                "[{\"card_id\":3002,\"next_upgrade_level\":1," +
                "\"description_i18n_key\":\"battle.card.3002.upgrade_description\"," +
                "\"cost\":1,\"play_destination\":0,\"rule_kind\":1,\"rule_value\":9}]"),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"max_health\":20,\"base_strength\":0,\"view_prefab_key\":\"pfb_char_enemy\",\"behavior_group_id\":6001}," +
                "{\"id\":2101,\"name_i18n_key\":\"battle.enemy.test_sentry.name\",\"max_health\":45,\"base_strength\":0,\"view_prefab_key\":\"pfb_char_enemy\",\"behavior_group_id\":6101}," +
                "{\"id\":2201,\"name_i18n_key\":\"battle.enemy.chrono_warden.name\",\"max_health\":60,\"base_strength\":0,\"view_prefab_key\":\"pfb_char_enemy\",\"behavior_group_id\":6201}]"),
            ["battle_tbencounter"] = JArray.Parse(
                "[{\"id\":5001,\"enemy_template_ids\":[2001],\"phase_two_behavior_group_id\":0}," +
                "{\"id\":5002,\"enemy_template_ids\":[2101],\"phase_two_behavior_group_id\":0}," +
                "{\"id\":5101,\"enemy_template_ids\":[2101],\"phase_two_behavior_group_id\":0}," +
                "{\"id\":5201,\"enemy_template_ids\":[2201],\"phase_two_behavior_group_id\":6202}]"),
            ["run_tbrelic"] = JArray.Parse(
                "[{\"id\":8001,\"name_i18n_key\":\"run.relic.one.name\",\"description_i18n_key\":\"run.relic.one.description\",\"battle_start_strength\":1}," +
                "{\"id\":8002,\"name_i18n_key\":\"run.relic.two.name\",\"description_i18n_key\":\"run.relic.two.description\",\"battle_start_strength\":3}]"),
            ["run_tbpotion"] = JArray.Parse(
                "[{\"id\":9001,\"name_i18n_key\":\"run.potion.one.name\",\"description_i18n_key\":\"run.potion.one.description\",\"heal_amount\":10}," +
                "{\"id\":9002,\"name_i18n_key\":\"run.potion.two.name\",\"description_i18n_key\":\"run.potion.two.description\",\"heal_amount\":25}]"),
        };

        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>创建 Presenter 奖励投影所需的最小 Implemented 卡牌配置行。</summary>
    private static JObject CreateTestCardRow(int templateId, int rarity)
    {
        bool isXCost = templateId == 3157 || templateId == 3264;
        return new JObject
        {
            ["id"] = templateId,
            ["external_key"] = $"TEST_RUN_ENTRY_{templateId}",
            ["catalog_snapshot_key"] = "test-fixture",
            ["name_i18n_key"] = $"battle.card.{templateId}.name",
            ["description_i18n_key"] = $"battle.card.{templateId}.description",
            ["upgraded_description_i18n_key"] = $"battle.card.{templateId}.description",
            ["card_type"] = 0,
            ["rarity"] = rarity,
            ["cost"] = isXCost ? 0 : 1,
            ["cost_kind"] = isXCost ? 1 : 0,
            ["upgraded_cost"] = 1,
            ["target_rule"] = 1,
            ["play_destination"] = 0,
            ["upgraded_play_destination"] = 0,
            ["has_upgrade"] = templateId == 3002,
            ["implementation_status"] = 0,
            ["effect_bindings"] = templateId == 3002
                ? new JArray(new JObject
                {
                    ["argument_key"] = "damage",
                    ["effect_id"] = 4002,
                })
                : new JArray(),
            ["illustration_key"] = string.Empty,
            ["program_id"] = 0,
            ["is_innate"] = false,
            ["upgrade_track_kind"] = templateId == 3002 ? 1 : 0,
            ["infinite_upgrade_rule_kind"] = 0,
            ["infinite_upgrade_value_per_level"] = 0,
        };
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
            case "run.entry.rest.heal":
                return $"Heal {arguments["amount"]} HP";
            case "run.entry.rest.upgrade":
                return $"Upgrade {arguments["card"]} to +{arguments["level"]}";
            case "run.entry.chest.title":
                return "Chest";
            case "run.entry.chest.claim":
                return "Claim";
            case "run.entry.chest.skip":
                return "Skip";
            case "run.entry.chest.full":
                return "Potion belt is full";
            case "run.entry.shop.title":
                return "Shop";
            case "run.entry.shop.purchase":
                return $"Buy {arguments["item"]} — {arguments["price"]} Gold";
            case "run.entry.shop.purchased":
                return $"{arguments["item"]} — Purchased";
            case "run.entry.shop.leave":
                return "Leave";
            case "run.entry.event.title":
                return "Event";
            case "run.entry.event.gain_gold":
                return $"Gain {arguments["gold"]} Gold";
            case "run.entry.event.paid_heal":
                return $"Pay {arguments["cost"]} Gold to heal up to {arguments["heal"]} HP";
            case "run.relic.one.name":
                return "Relic One";
            case "run.potion.one.name":
                return "Healing Potion";
            case "run.potion.one.description":
                return $"Restore {arguments["heal"]} HP";
            case "battle.card.3002.name":
                return "Test Strike";
            case "run.entry.save.issue.missing_configuration":
                return $"Missing {arguments["kind"]} {arguments["id"]}";
            default:
                return key;
        }
    }

    /// <summary>由真实生成器和初始路径创建一份可继续的当前 schema 文档。</summary>
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
    private static RunState CreateDirectRun(
        RunStateStore store,
        uint randomRootSeed,
        RunHoldings holdings = null)
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.Current,
            RunRandomDomains.DeriveMapSeed(randomRootSeed));
        return store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("13572468-2468-1357-2468-135724681357")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002, 3003 }),
            randomRootSeed,
            map: map,
            holdings: holdings));
    }

    /// <summary>沿 G7 冻结普通边恢复一个已完成精英且刚抵达 Boss 的稳定门夹具。</summary>
    private static RunState CreateG7BossGateRun(
        RunStateStore store,
        uint randomRootSeed)
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.NewRunG7V1,
            RunRandomDomains.DeriveMapSeed(randomRootSeed));
        var path = new List<MapNodeId>
        {
            MapNodeId.FromPosition(layer: 0, slot: 0),
        };
        MapNodeId cursor = path[0];
        for (int index = 0; index < map.Nodes.Count; index++)
        {
            MapNodeId next = MapReachability.GetSelectableNodeIds(
                    map,
                    cursor,
                    MapTraversalMode.Ordinary)
                .First();
            path.Add(next);
            cursor = next;
            if (map.GetNode(next).Kind == MapNodeKind.Boss)
                break;
        }

        Assert.That(map.GetNode(path[path.Count - 1]).Kind, Is.EqualTo(MapNodeKind.Boss));
        return store.RestoreRun(new RunRestoreOptions(
            new RunId(Guid.Parse("24682468-1357-2468-1357-246813572468")),
            heroTemplateId: 1001,
            currentHealth: 53,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002, 3003 }),
            randomRootSeed,
            map,
            path,
            RunProgressPhase.BossGateReached,
            committedNodeId: null,
            outcomeKind: null,
            holdings: RunHoldings.Empty(initialGold: 100)));
    }

    /// <summary>建立只含 Start 与一个直接可达非战斗节点的最小 Presenter 测试地图。</summary>
    private static MapDefinition CreateSingleNonCombatMap(
        MapNodeKind kind,
        int contentId)
    {
        MapNodeId startNodeId = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId destinationNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        return new MapDefinition(
            profileId: "tinyspire.test.presenter.noncombat.v1",
            generatorVersion: 1,
            mapSeed: 42420003u,
            nodes: new[]
            {
                new MapNode(startNodeId, layer: 0, slot: 0, MapNodeKind.Start, contentId: 0),
                new MapNode(destinationNodeId, layer: 1, slot: 0, kind, contentId),
            },
            edges: new[]
            {
                new MapEdge(startNodeId, destinationNodeId),
            });
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

    /// <summary>从生产 mixed profile 建立只含布局必要字段的十个地图节点投影。</summary>
    private static RunMapNodeViewModel[] CreateMixedMapNodeViewModels()
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.NewRunG6V1,
            mapSeed: 123456u);
        var catalog = new RunMapIdentityCatalog(CreateTables, Localize);
        return map.Nodes
            .OrderBy(node => node.Layer)
            .ThenBy(node => node.Slot)
            .Select(node =>
            {
                RunMapIdentityDescriptor identity = catalog.Resolve(node.Kind, node.ContentId);
                return new RunMapNodeViewModel(
                    node.Id.Value,
                    node.Layer,
                    node.Slot,
                    node.Kind,
                    node.ContentId,
                    identity.DisplayName,
                    identity.VisualAnchorKind,
                    RunMapNodePresentationState.Locked,
                    Array.Empty<string>(),
                    Array.Empty<string>());
            })
            .ToArray();
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

    /// <summary>通过 Presenter 的严格奖励身份动作跳过当前唯一冻结奖励。</summary>
    private static void SkipPendingReward(
        RecordingRunEntryView view,
        RunStateStore store)
    {
        PendingCardReward pending = store.Current.PendingCardReward
            ?? throw new InvalidOperationException("The test Run does not have a pending reward.");
        view.Emit(new RunEntryAction(
            RunEntryActionKind.SkipCardReward,
            cardRewardId: pending.Id));
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

    /// <summary>以可切换语言前缀证明刷新只重投影文本而不改变奖励身份。</summary>
    private sealed class MutableCardLocalizer
    {
        /// <summary>当前测试语言前缀。</summary>
        public string Language { get; set; } = "EN";

        /// <summary>本地化卡牌名称与描述，其余入口键复用稳定测试映射。</summary>
        public string Translate(
            string key,
            IReadOnlyDictionary<string, object> arguments)
        {
            return key.StartsWith("battle.card.", StringComparison.Ordinal)
                ? $"{Language}:{key}"
                : RunEntryPresenterTests.Localize(key, arguments);
        }
    }

    /// <summary>以可切换语言与显式 Smart 参数结果验证持有物本地化投影。</summary>
    private sealed class MutableHoldingsLocalizer
    {
        /// <summary>当前测试语言前缀。</summary>
        public string Language { get; set; } = "EN";

        /// <summary>将固定入口键及 cfg.run 名称、描述投影为可独立断言的文本。</summary>
        public string Translate(
            string key,
            IReadOnlyDictionary<string, object> arguments)
        {
            switch (key)
            {
                case "run.entry.holdings.gold":
                    return $"{Language}:GOLD:{arguments["gold"]}";
                case "run.entry.holdings.relics":
                    return $"{Language}:RELICS";
                case "run.entry.holdings.potions":
                    return $"{Language}:POTIONS";
                case "run.entry.holdings.empty":
                    return $"{Language}:EMPTY";
                case "run.relic.one.name":
                    return $"{Language}:Relic One";
                case "run.relic.two.name":
                    return $"{Language}:Relic Two";
                case "run.relic.one.description":
                case "run.relic.two.description":
                    return $"{Language}:Strength {arguments["strength"]}";
                case "run.potion.one.name":
                    return $"{Language}:Potion One";
                case "run.potion.two.name":
                    return $"{Language}:Potion Two";
                case "run.potion.one.description":
                case "run.potion.two.description":
                    return $"{Language}:Heal {arguments["heal"]}";
                default:
                    return RunEntryPresenterTests.Localize(key, arguments);
            }
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
        private readonly List<RunSaveDocument> _commitAttempts =
            new List<RunSaveDocument>();
        private RunSaveDocument _document;

        /// <summary>累计玩家确认后的删除次数。</summary>
        public int DeleteCount { get; private set; }

        /// <summary>按调用顺序公开全部冻结提交对象，供 exact retry 身份断言。</summary>
        public IReadOnlyList<RunSaveDocument> CommitAttempts => _commitAttempts;

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
            _commitAttempts.Add(document ?? throw new ArgumentNullException(nameof(document)));
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
