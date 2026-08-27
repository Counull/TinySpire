using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class RunStateStoreTests
{
    /// <summary>创建新 Run 时冻结唯一身份、角色事实与完整地图，并停在 Start。</summary>
    [Test]
    public void CreateNewRun_FreezesIdentityHeroAndWholeActMap()
    {
        RunCreationOptions options = CreateOptions(
            "11111111-2222-3333-4444-555555555555",
            mapSeed: 123456u);

        using var store = new RunStateStore();
        RunState state = store.CreateNewRun(options);

        Assert.That(state.RunId, Is.EqualTo(options.RunId));
        Assert.That(state.HeroTemplateId, Is.EqualTo(1001));
        Assert.That(state.CurrentHealth, Is.EqualTo(80));
        Assert.That(state.MaxHealth, Is.EqualTo(80));
        Assert.That(state.RunDeck.Cards.Select(card => card.TemplateId), Is.EqualTo(new[] { 3002 }));
        Assert.That(state.RandomRootSeed, Is.EqualTo(123456u));
        Assert.That(state.MapDefinition, Is.SameAs(options.Map));
        Assert.That(state.CurrentNodeId, Is.EqualTo(MapNodeId.FromPosition(0, 0)));
        Assert.That(state.PathNodeIds, Is.EqualTo(new[] { MapNodeId.FromPosition(0, 0) }));
        Assert.That(state.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(state.BattleAttemptSequence, Is.Zero);
        Assert.That(state.ActiveBattle, Is.Null);
        Assert.That(store.Current, Is.SameAs(state));
    }

    /// <summary>创建 Run 时由 Store 冻结显式 RunDeck，并把同一不可变实例投影签发给 Battle。</summary>
    [Test]
    public void CreateThenBeginBattle_PreservesExplicitRunDeckProjection()
    {
        var deck = new RunDeck(new[]
        {
            new RunCard(new RunCardInstanceId(1), templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(2), templateId: 3002, upgradeLevel: 1),
            new RunCard(new RunCardInstanceId(3), templateId: 3003, upgradeLevel: 0),
        });
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("10101010-2020-3030-4040-505050505050")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: deck,
            randomRootSeed: 2468u,
            map: CreateMap(2468u));
        using var store = new RunStateStore();

        RunState created = store.CreateNewRun(options);
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput battleInput = store.BeginCommittedBattle();

        Assert.That(created.RunDeck, Is.SameAs(deck));
        Assert.That(battleInput.RunCards.Select(card => card.InstanceId), Is.EqualTo(
            new[]
            {
                new RunCardInstanceId(1),
                new RunCardInstanceId(2),
                new RunCardInstanceId(3),
            }));
        Assert.That(battleInput.RunCards.Select(card => card.UpgradeLevel), Is.EqualTo(
            new[] { 0, 1, 0 }));
    }

    /// <summary>创建 Run 与签发 Battle 输入时保持同一份不可变持有物事实。</summary>
    [Test]
    public void CreateThenBeginBattle_PreservesExplicitRunHoldingsProjection()
    {
        RunHoldings holdings = RunHoldings.Empty(initialGold: 100)
            .AddRelic(templateId: 5001)
            .AddPotion(templateId: 6001);
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("11112222-3333-4444-5555-666677778888")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: 97531u,
            map: CreateMap(97531u),
            holdings: holdings);
        using var store = new RunStateStore();

        RunState created = store.CreateNewRun(options);
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput battleInput = store.BeginCommittedBattle();

        Assert.That(created.Holdings, Is.SameAs(holdings));
        Assert.That(created.Holdings.Gold, Is.EqualTo(100));
        Assert.That(created.Holdings.Relics.Single().TemplateId, Is.EqualTo(5001));
        Assert.That(created.Holdings.Potions.Single().TemplateId, Is.EqualTo(6001));
        Assert.That(battleInput.Holdings, Is.SameAs(holdings));
    }

    /// <summary>非战斗访问只能按节点身份预演并进入权威冻结的 Pending，且 G6-A 不推进路径。</summary>
    [Test]
    public void NodeVisitEntry_PreviewAndCommitFreezeAuthorityWithoutAdvancingPath()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Rest,
            contentId: 7101);
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("12121212-3434-5656-7878-909090909090")),
            heroTemplateId: 1001,
            initialHealth: 70,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 100));
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(options);
        MapNodeId restNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        var catalog = new NodeVisitEntryCatalogStub();

        RunNodeVisitEntrySettlement preview = store.PreviewNodeVisitEntry(restNodeId, catalog);

        Assert.That(store.Current, Is.SameAs(created));
        Assert.That(preview.Source, Is.SameAs(created));
        Assert.That(preview.Successor.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(preview.Successor.PendingNodeVisit.Id,
            Is.EqualTo(new RunNodeVisitId(created.RunId, restNodeId)));
        Assert.That(preview.Successor.PendingNodeVisit.ContentId, Is.EqualTo(7101));
        Assert.That(preview.Successor.PendingNodeVisit.RestPayload.HealAmount, Is.EqualTo(24));
        Assert.That(preview.Successor.PendingNodeVisit.RestPayload.UpgradeCandidateInstanceIds,
            Is.EqualTo(new[] { new RunCardInstanceId(1) }));
        Assert.That(preview.Successor.PathNodeIds, Is.EqualTo(created.PathNodeIds));

        catalog.AllowUpgrade = false;
        RunState entered = store.CommitNodeVisitEntry(preview);

        Assert.That(store.Current, Is.SameAs(entered));
        Assert.That(entered.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(entered.PendingNodeVisit.Id,
            Is.EqualTo(preview.Successor.PendingNodeVisit.Id));
        Assert.That(entered.PendingNodeVisit.RestPayload.UpgradeCandidateInstanceIds,
            Is.EqualTo(new[] { new RunCardInstanceId(1) }));
        Assert.That(entered.PathNodeIds, Is.EqualTo(created.PathNodeIds));
        Assert.Throws<InvalidOperationException>(() =>
            store.CommitNodeVisitEntry(preview));
        Assert.That(store.Current, Is.SameAs(entered));
    }

    /// <summary>不可达节点、错误程序化内容与重复进入都必须在发布前零写入。</summary>
    [Test]
    public void NodeVisitEntry_InvalidNodeOrAnchorAndRetry_AreRejectedWithoutPublishing()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Rest,
            contentId: 7101);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("13131313-3535-5757-7979-919191919191")),
            heroTemplateId: 1001,
            initialHealth: 70,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));
        MapNodeId restNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        var catalog = new NodeVisitEntryCatalogStub();

        Assert.Throws<InvalidOperationException>(() => store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 9, slot: 0),
            catalog));
        Assert.That(store.Current, Is.SameAs(created));

        RunNodeVisitEntrySettlement settlement = store.PreviewNodeVisitEntry(
            restNodeId,
            catalog);
        RunState entered = store.CommitNodeVisitEntry(settlement);

        Assert.Throws<InvalidOperationException>(() =>
            store.CommitNodeVisitEntry(settlement));
        Assert.That(store.Current, Is.SameAs(entered));

        using var wrongAnchorStore = new RunStateStore();
        RunState wrongAnchor = wrongAnchorStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("14141414-3636-5858-8080-929292929292")),
            heroTemplateId: 1001,
            initialHealth: 70,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: 42420002u,
            map: CreateSingleNonCombatMap(MapNodeKind.Rest, contentId: 7199)));
        Assert.Throws<InvalidOperationException>(() =>
            wrongAnchorStore.PreviewNodeVisitEntry(restNodeId, catalog));
        Assert.That(wrongAnchorStore.Current, Is.SameAs(wrongAnchor));
    }

    /// <summary>休息点治疗先纯计算冻结后继，CAS 发布后才恢复生命并把节点追加一次。</summary>
    [Test]
    public void RestHealSettlement_PreviewsThenCommitsFrozenHealingAndCompletesNode()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Rest,
            contentId: 7101);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("15151515-3737-5959-8181-939393939393")),
            heroTemplateId: 1001,
            initialHealth: 40,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));
        var catalog = new NodeVisitEntryCatalogStub();
        RunNodeVisitEntrySettlement entry = store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog);
        RunState pending = store.CommitNodeVisitEntry(entry);

        RunRestSettlement settlement = store.PreviewRestHealSettlement(
            pending.PendingNodeVisit.Id);

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(settlement.Source, Is.SameAs(pending));
        Assert.That(settlement.Successor.CurrentHealth, Is.EqualTo(64));
        Assert.That(settlement.Successor.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(settlement.Successor.PendingNodeVisit, Is.Null);
        Assert.That(settlement.Successor.PathNodeIds, Is.EqualTo(new[]
        {
            MapNodeId.FromPosition(layer: 0, slot: 0),
            MapNodeId.FromPosition(layer: 1, slot: 0),
        }));

        RunState settled = store.CommitRestSettlement(settlement);

        Assert.That(settled, Is.SameAs(settlement.Successor));
        Assert.Throws<InvalidOperationException>(() => store.CommitRestSettlement(settlement));
        Assert.That(store.Current, Is.SameAs(settled));
    }

    /// <summary>Rest 升级只接受冻结候选并在预览时以当前配置终审下一等级。</summary>
    [Test]
    public void RestUpgradeSettlement_RequiresFrozenCandidateAndCurrentConfiguration()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Rest,
            contentId: 7101);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("16161616-3838-6060-8282-949494949494")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));
        var catalog = new NodeVisitEntryCatalogStub();
        RunNodeVisitEntrySettlement entry = store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog);
        RunState pending = store.CommitNodeVisitEntry(entry);

        Assert.Throws<InvalidOperationException>(() =>
            store.PreviewRestUpgradeSettlement(
                pending.PendingNodeVisit.Id,
                new RunCardInstanceId(2),
                catalog));
        Assert.That(store.Current, Is.SameAs(pending));

        catalog.AllowUpgrade = false;
        Assert.Throws<InvalidOperationException>(() =>
            store.PreviewRestUpgradeSettlement(
                pending.PendingNodeVisit.Id,
                new RunCardInstanceId(1),
                catalog));
        Assert.That(store.Current, Is.SameAs(pending));

        catalog.AllowUpgrade = true;
        RunRestSettlement settlement = store.PreviewRestUpgradeSettlement(
            pending.PendingNodeVisit.Id,
            new RunCardInstanceId(1),
            catalog);
        catalog.AllowUpgrade = false;

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(settlement.Successor.RunDeck.Cards.Single().UpgradeLevel, Is.EqualTo(1));
        Assert.That(settlement.Successor.CurrentHealth, Is.EqualTo(80));
        Assert.That(settlement.Successor.PathNodeIds.Last(),
            Is.EqualTo(MapNodeId.FromPosition(layer: 1, slot: 0)));
        Assert.That(store.CommitRestSettlement(settlement), Is.SameAs(settlement.Successor));
    }

    /// <summary>满血治疗、伪造访问身份、跨 Store 与重复提交均保持当前 Pending 零写入。</summary>
    [Test]
    public void RestSettlement_FullHealthForgedStaleAndDuplicateCommandsAreZeroWrite()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Rest,
            contentId: 7101);
        var catalog = new NodeVisitEntryCatalogStub();
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("17171717-3939-6161-8383-959595959595")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));

        Assert.Throws<InvalidOperationException>(() =>
            store.PreviewRestHealSettlement(pending.PendingNodeVisit.Id));
        Assert.Throws<InvalidOperationException>(() =>
            store.PreviewRestUpgradeSettlement(
                new RunNodeVisitId(
                    new RunId(Guid.Parse("18181818-4040-6262-8484-969696969696")),
                    pending.PendingNodeVisit.NodeId),
                new RunCardInstanceId(1),
                catalog));
        Assert.That(store.Current, Is.SameAs(pending));

        RunRestSettlement settlement = store.PreviewRestUpgradeSettlement(
            pending.PendingNodeVisit.Id,
            new RunCardInstanceId(1),
            catalog);
        using var otherStore = new RunStateStore();
        otherStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("19191919-4141-6363-8585-979797979797")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map));

        Assert.Throws<InvalidOperationException>(() =>
            otherStore.CommitRestSettlement(settlement));
        Assert.That(otherStore.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));

        RunState settled = store.CommitRestSettlement(settlement);
        Assert.Throws<InvalidOperationException>(() => store.CommitRestSettlement(settlement));
        Assert.That(store.Current, Is.SameAs(settled));
        Assert.That(created.PathNodeIds.Count, Is.EqualTo(1));
    }

    /// <summary>宝箱领取只消费冻结药水模板，以现有最大实例序号加一并在 CAS 发布时完成路径一次。</summary>
    [Test]
    public void ChestClaimSettlement_AppendsFrozenPotionAfterSaveBoundaryAndCompletesPathOnce()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Chest,
            RunNodeVisitIdentityCatalog.ChestContentId);
        var holdings = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(4), templateId: 6001),
                new RunPotion(new RunPotionInstanceId(9), templateId: 6002),
            },
            gold: 100);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("20202020-4242-6464-8686-989898989898")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: holdings));
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new NodeVisitEntryCatalogStub()));

        RunChestSettlement settlement = store.PreviewChestClaimSettlement(
            pending.PendingNodeVisit.Id);

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(settlement.Source, Is.SameAs(pending));
        Assert.That(settlement.Successor.Holdings.Potions.Select(potion => potion.InstanceId.Sequence),
            Is.EqualTo(new[] { 4, 9, 10 }));
        Assert.That(settlement.Successor.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6001, 6002, RunNodeVisitIdentityCatalog.SamplePotionTemplateId }));
        Assert.That(settlement.Successor.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(settlement.Successor.PendingNodeVisit, Is.Null);
        Assert.That(settlement.Successor.PathNodeIds, Is.EqualTo(new[]
        {
            MapNodeId.FromPosition(layer: 0, slot: 0),
            MapNodeId.FromPosition(layer: 1, slot: 0),
        }));

        RunState settled = store.CommitChestSettlement(settlement);

        Assert.That(settled, Is.SameAs(settlement.Successor));
        Assert.Throws<InvalidOperationException>(() => store.CommitChestSettlement(settlement));
        Assert.That(created.PathNodeIds.Count, Is.EqualTo(1));
    }

    /// <summary>空药水带领取宝箱奖励时从实例序号一开始，且预览阶段不提前发布。</summary>
    [Test]
    public void ChestClaimSettlement_EmptyPotionBeltStartsInstanceSequenceAtOne()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Chest,
            RunNodeVisitIdentityCatalog.ChestContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("25252525-4747-6969-9191-131313131313")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 0)));
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new NodeVisitEntryCatalogStub()));

        RunChestSettlement settlement = store.PreviewChestClaimSettlement(
            pending.PendingNodeVisit.Id);

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(settlement.Successor.Holdings.Potions, Has.Count.EqualTo(1));
        Assert.That(settlement.Successor.Holdings.Potions[0].InstanceId.Sequence, Is.EqualTo(1));
        Assert.That(settlement.Successor.Holdings.Potions[0].TemplateId,
            Is.EqualTo(RunNodeVisitIdentityCatalog.SamplePotionTemplateId));
    }

    /// <summary>宝箱跳过保持同一持有物快照，但仍清除 Pending 并把该节点追加一次。</summary>
    [Test]
    public void ChestSkipSettlement_PreservesHoldingsAndCompletesPathOnce()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Chest,
            RunNodeVisitIdentityCatalog.ChestContentId);
        RunHoldings holdings = RunHoldings.Empty(initialGold: 77)
            .AddPotion(templateId: 6001);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("21212121-4343-6565-8787-999999999999")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: holdings));
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new NodeVisitEntryCatalogStub()));

        RunChestSettlement settlement = store.PreviewChestSkipSettlement(
            pending.PendingNodeVisit.Id);

        Assert.That(settlement.Successor.Holdings, Is.SameAs(holdings));
        Assert.That(settlement.Successor.PathNodeIds.Last(),
            Is.EqualTo(pending.PendingNodeVisit.NodeId));
        Assert.That(store.CommitChestSettlement(settlement), Is.SameAs(settlement.Successor));
    }

    /// <summary>伪造身份、满槽与实例序号溢出都必须在宝箱预览阶段拒绝且不写 Store。</summary>
    [Test]
    public void ChestClaimSettlement_ForgedFullAndOverflowCommandsAreZeroWrite()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Chest,
            RunNodeVisitIdentityCatalog.ChestContentId);
        var fullHoldings = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 6001),
                new RunPotion(new RunPotionInstanceId(2), templateId: 6002),
                new RunPotion(new RunPotionInstanceId(3), templateId: 6003),
            },
            gold: 100);
        using var fullStore = new RunStateStore();
        fullStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("22222222-4444-6666-8888-101010101010")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: fullHoldings));
        RunState fullPending = fullStore.CommitNodeVisitEntry(fullStore.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            new NodeVisitEntryCatalogStub()));

        Assert.Throws<InvalidOperationException>(() =>
            fullStore.PreviewChestClaimSettlement(new RunNodeVisitId(
                new RunId(Guid.Parse("23232323-4545-6767-8989-111111111111")),
                fullPending.PendingNodeVisit.NodeId)));
        Assert.Throws<InvalidOperationException>(() =>
            fullStore.PreviewChestClaimSettlement(fullPending.PendingNodeVisit.Id));
        Assert.That(fullStore.Current, Is.SameAs(fullPending));

        var overflowHoldings = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(int.MaxValue), templateId: 6001),
            },
            gold: 100);
        using var overflowStore = new RunStateStore();
        overflowStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("24242424-4646-6868-9090-121212121212")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: overflowHoldings));
        RunState overflowPending = overflowStore.CommitNodeVisitEntry(
            overflowStore.PreviewNodeVisitEntry(
                MapNodeId.FromPosition(layer: 1, slot: 0),
                new NodeVisitEntryCatalogStub()));

        Assert.Throws<OverflowException>(() =>
            overflowStore.PreviewChestClaimSettlement(overflowPending.PendingNodeVisit.Id));
        Assert.That(overflowStore.Current, Is.SameAs(overflowPending));
    }

    /// <summary>商店购买必须把扣款、内容获得与目标库存已购标记冻结在同一 Pending 后继中。</summary>
    [Test]
    public void ShopPurchaseSettlement_RelicPurchaseIsAtomicAndKeepsPendingPath()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Shop,
            RunNodeVisitIdentityCatalog.ShopContentId);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("26262626-4848-7070-9292-141414141414")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 75)));
        var catalog = new NodeVisitEntryCatalogStub();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));

        RunShopSettlement settlement = store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId: 1,
            catalog);

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(settlement.Source, Is.SameAs(pending));
        Assert.That(settlement.Successor.ProgressPhase,
            Is.EqualTo(RunProgressPhase.NodeVisitPending));
        Assert.That(settlement.Successor.PendingNodeVisit.Id,
            Is.EqualTo(pending.PendingNodeVisit.Id));
        Assert.That(settlement.Successor.PathNodeIds, Is.EqualTo(created.PathNodeIds));
        Assert.That(settlement.Successor.Holdings.Gold, Is.Zero);
        Assert.That(settlement.Successor.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { RunNodeVisitIdentityCatalog.SampleRelicTemplateId }));
        Assert.That(settlement.Successor.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { true, false, false }));

        RunState settled = store.CommitShopSettlement(settlement);

        Assert.That(settled, Is.SameAs(settlement.Successor));
        Assert.Throws<InvalidOperationException>(() => store.CommitShopSettlement(settlement));
        Assert.That(created.Holdings.Gold, Is.EqualTo(75));
        Assert.That(created.Holdings.Relics, Is.Empty);
    }

    /// <summary>药水与卡牌可按同一冻结商店连续购买，实例追加与两次已购事实均不提前完成路径。</summary>
    [Test]
    public void ShopPurchaseSettlement_PotionThenCardPreservesEarlierPurchaseAndStableInstances()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Shop,
            RunNodeVisitIdentityCatalog.ShopContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("27272727-4949-7171-9393-151515151515")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 100)));
        var catalog = new NodeVisitEntryCatalogStub();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));
        int frozenCardTemplateId = pending.PendingNodeVisit.ShopPayload.Entries[2].TemplateId;
        RunShopSettlement staleCardSettlement = store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId: 3,
            catalog);

        RunShopSettlement potionSettlement = store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId: 2,
            catalog);
        RunState afterPotion = store.CommitShopSettlement(potionSettlement);
        Assert.Throws<InvalidOperationException>(() =>
            store.CommitShopSettlement(staleCardSettlement));
        Assert.Throws<InvalidOperationException>(() => store.PreviewShopPurchaseSettlement(
            afterPotion.PendingNodeVisit.Id,
            stockEntryId: 2,
            catalog));
        Assert.That(store.Current, Is.SameAs(afterPotion));
        RunShopSettlement cardSettlement = store.PreviewShopPurchaseSettlement(
            afterPotion.PendingNodeVisit.Id,
            stockEntryId: 3,
            catalog);

        Assert.That(cardSettlement.Successor.Holdings.Gold, Is.EqualTo(25));
        Assert.That(cardSettlement.Successor.Holdings.Potions.Single().InstanceId.Sequence,
            Is.EqualTo(1));
        Assert.That(cardSettlement.Successor.Holdings.Potions.Single().TemplateId,
            Is.EqualTo(RunNodeVisitIdentityCatalog.SamplePotionTemplateId));
        Assert.That(cardSettlement.Successor.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 1, 2 }));
        Assert.That(cardSettlement.Successor.RunDeck.Cards.Last().TemplateId,
            Is.EqualTo(frozenCardTemplateId));
        Assert.That(cardSettlement.Successor.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, true }));
        Assert.That(cardSettlement.Successor.PathNodeIds, Is.EqualTo(pending.PathNodeIds));

        RunState afterCard = store.CommitShopSettlement(cardSettlement);
        RunShopSettlement leaveSettlement = store.PreviewShopLeaveSettlement(
            afterCard.PendingNodeVisit.Id);

        Assert.That(leaveSettlement.Successor.ProgressPhase,
            Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(leaveSettlement.Successor.PendingNodeVisit, Is.Null);
        Assert.That(leaveSettlement.Successor.Holdings, Is.SameAs(afterCard.Holdings));
        Assert.That(leaveSettlement.Successor.RunDeck, Is.SameAs(afterCard.RunDeck));
        Assert.That(leaveSettlement.Successor.PathNodeIds, Is.EqualTo(new[]
        {
            MapNodeId.FromPosition(layer: 0, slot: 0),
            MapNodeId.FromPosition(layer: 1, slot: 0),
        }));
        Assert.That(store.CommitShopSettlement(leaveSettlement),
            Is.SameAs(leaveSettlement.Successor));
    }

    /// <summary>错误访问、未知或已购库存、余额/容量/重复遗物与配置漂移均在 Shop 预览阶段零写入。</summary>
    [Test]
    public void ShopPurchaseSettlement_InvalidAuthorityAndCapacityFailuresAreZeroWrite()
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
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 6001),
                new RunPotion(new RunPotionInstanceId(2), templateId: 6002),
                new RunPotion(new RunPotionInstanceId(3), templateId: 6003),
            },
            gold: 200);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("28282828-5050-7272-9494-161616161616")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: holdings));
        var catalog = new NodeVisitEntryCatalogStub();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));

        Assert.Throws<InvalidOperationException>(() => store.PreviewShopPurchaseSettlement(
            new RunNodeVisitId(
                new RunId(Guid.Parse("29292929-5151-7373-9595-171717171717")),
                pending.PendingNodeVisit.NodeId),
            stockEntryId: 1,
            catalog));
        Assert.Throws<InvalidOperationException>(() => store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId: 99,
            catalog));
        Assert.Throws<InvalidOperationException>(() => store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId: 1,
            catalog));
        Assert.Throws<InvalidOperationException>(() => store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId: 2,
            catalog));
        catalog.ShopCardInHeroPool = false;
        Assert.Throws<InvalidOperationException>(() => store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId: 3,
            catalog));
        Assert.That(store.Current, Is.SameAs(pending));

        using var poorStore = new RunStateStore();
        poorStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("30303030-5252-7474-9696-181818181818")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 49)));
        catalog.ShopCardInHeroPool = true;
        RunState poorPending = poorStore.CommitNodeVisitEntry(poorStore.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));
        Assert.Throws<InvalidOperationException>(() => poorStore.PreviewShopPurchaseSettlement(
            poorPending.PendingNodeVisit.Id,
            stockEntryId: 3,
            catalog));
        Assert.That(poorStore.Current, Is.SameAs(poorPending));
    }

    /// <summary>三类库存的下一实例序号溢出都必须在预览阶段拒绝且不扣款、不翻转 Purchased。</summary>
    [Test]
    public void ShopPurchaseSettlement_InstanceSequenceOverflowIsZeroWrite()
    {
        AssertShopPurchaseSequenceOverflowIsZeroWrite(RunShopStockKind.Relic, stockEntryId: 1);
        AssertShopPurchaseSequenceOverflowIsZeroWrite(RunShopStockKind.Potion, stockEntryId: 2);
        AssertShopPurchaseSequenceOverflowIsZeroWrite(RunShopStockKind.Card, stockEntryId: 3);
    }

    /// <summary>为指定 Shop 库存种类构造最大实例序号，并验证预览失败时仍保持同一 Source。</summary>
    private static void AssertShopPurchaseSequenceOverflowIsZeroWrite(
        RunShopStockKind stockKind,
        int stockEntryId)
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Shop,
            RunNodeVisitIdentityCatalog.ShopContentId);
        RunHoldings holdings = stockKind switch
        {
            RunShopStockKind.Relic => new RunHoldings(
                new[] { new RunRelic(new RunRelicInstanceId(int.MaxValue), templateId: 8999) },
                Array.Empty<RunPotion>(),
                gold: 100),
            RunShopStockKind.Potion => new RunHoldings(
                Array.Empty<RunRelic>(),
                new[] { new RunPotion(new RunPotionInstanceId(int.MaxValue), templateId: 6999) },
                gold: 100),
            RunShopStockKind.Card => RunHoldings.Empty(initialGold: 100),
            _ => throw new ArgumentOutOfRangeException(nameof(stockKind)),
        };
        RunDeck deck = stockKind == RunShopStockKind.Card
            ? new RunDeck(new[]
            {
                new RunCard(
                    new RunCardInstanceId(int.MaxValue),
                    templateId: 3002,
                    upgradeLevel: 0),
            })
            : RunDeck.CreateInitial(new[] { 3002 });
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("37373737-5959-7b7b-adad-252525252525")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: deck,
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: holdings));
        var catalog = new NodeVisitEntryCatalogStub();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));

        Assert.Throws<OverflowException>(() => store.PreviewShopPurchaseSettlement(
            pending.PendingNodeVisit.Id,
            stockEntryId,
            catalog));
        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(store.Current.Holdings.Gold, Is.EqualTo(100));
        Assert.That(store.Current.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, false, false }));
    }

    /// <summary>事件免费金币选择必须使用冻结值一次完成节点，并以来源引用拒绝重复或过期发布。</summary>
    [Test]
    public void EventChoiceSettlement_GainGoldCompletesExactlyOnceAndRejectsStaleSettlement()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Event,
            RunNodeVisitIdentityCatalog.EventContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("38383838-6060-7c7c-aeae-262626262626")),
            heroTemplateId: 1001,
            initialHealth: 65,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 100)));
        var catalog = new NodeVisitEntryCatalogStub();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));

        RunEventChoiceSettlement gain = store.PreviewEventChoiceSettlement(
            pending.PendingNodeVisit.Id,
            RunEventChoiceKind.GainGold);
        RunEventChoiceSettlement stalePaidHeal = store.PreviewEventChoiceSettlement(
            pending.PendingNodeVisit.Id,
            RunEventChoiceKind.PaidHeal);

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(gain.Source, Is.SameAs(pending));
        Assert.That(gain.Successor.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(gain.Successor.PendingNodeVisit, Is.Null);
        Assert.That(gain.Successor.Holdings.Gold, Is.EqualTo(150));
        Assert.That(gain.Successor.CurrentHealth, Is.EqualTo(65));
        Assert.That(gain.Successor.PathNodeIds, Is.EqualTo(new[]
        {
            MapNodeId.FromPosition(layer: 0, slot: 0),
            MapNodeId.FromPosition(layer: 1, slot: 0),
        }));

        RunState settled = store.CommitEventChoiceSettlement(gain);

        Assert.That(settled, Is.SameAs(gain.Successor));
        Assert.Throws<InvalidOperationException>(() => store.CommitEventChoiceSettlement(gain));
        Assert.Throws<InvalidOperationException>(() =>
            store.CommitEventChoiceSettlement(stalePaidHeal));
        Assert.That(store.Current, Is.SameAs(settled));
    }

    /// <summary>付费治疗在余额恰好等于冻结价格时扣至零，并把不足十五点的缺失生命夹到上限。</summary>
    [Test]
    public void EventChoiceSettlement_PaidHealAtExactGoldClampsToMissingHealth()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Event,
            RunNodeVisitIdentityCatalog.EventContentId);
        using var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("39393939-6161-7d7d-afaf-272727272727")),
            heroTemplateId: 1001,
            initialHealth: 76,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 25)));
        var catalog = new NodeVisitEntryCatalogStub();
        RunState pending = store.CommitNodeVisitEntry(store.PreviewNodeVisitEntry(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            catalog));

        RunEventChoiceSettlement settlement = store.PreviewEventChoiceSettlement(
            pending.PendingNodeVisit.Id,
            RunEventChoiceKind.PaidHeal);

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(settlement.Successor.Holdings.Gold, Is.Zero);
        Assert.That(settlement.Successor.CurrentHealth, Is.EqualTo(80));
        Assert.That(settlement.Successor.Holdings.Relics, Is.EqualTo(pending.Holdings.Relics));
        Assert.That(settlement.Successor.Holdings.Potions, Is.EqualTo(pending.Holdings.Potions));
        Assert.That(store.CommitEventChoiceSettlement(settlement),
            Is.SameAs(settlement.Successor));
    }

    /// <summary>伪造身份、非法选择、金币不足、满血与金币溢出都必须在事件预览阶段零写入。</summary>
    [Test]
    public void EventChoiceSettlement_InvalidAuthorityAndChoiceFailuresAreZeroWrite()
    {
        MapDefinition map = CreateSingleNonCombatMap(
            MapNodeKind.Event,
            RunNodeVisitIdentityCatalog.EventContentId);
        var catalog = new NodeVisitEntryCatalogStub();
        using var poorStore = new RunStateStore();
        poorStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("40404040-6262-7e7e-b0b0-282828282828")),
            heroTemplateId: 1001,
            initialHealth: 65,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 24)));
        RunState poorPending = poorStore.CommitNodeVisitEntry(
            poorStore.PreviewNodeVisitEntry(
                MapNodeId.FromPosition(layer: 1, slot: 0),
                catalog));

        Assert.Throws<InvalidOperationException>(() =>
            poorStore.PreviewEventChoiceSettlement(
                new RunNodeVisitId(
                    new RunId(Guid.Parse("41414141-6363-7f7f-b1b1-292929292929")),
                    poorPending.PendingNodeVisit.NodeId),
                RunEventChoiceKind.GainGold));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            poorStore.PreviewEventChoiceSettlement(
                poorPending.PendingNodeVisit.Id,
                (RunEventChoiceKind)999));
        Assert.Throws<InvalidOperationException>(() =>
            poorStore.PreviewEventChoiceSettlement(
                poorPending.PendingNodeVisit.Id,
                RunEventChoiceKind.PaidHeal));
        Assert.That(poorStore.Current, Is.SameAs(poorPending));
        Assert.That(poorStore.Current.Holdings.Gold, Is.EqualTo(24));

        using var fullStore = new RunStateStore();
        fullStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("42424242-6464-8080-b2b2-303030303030")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: 25)));
        RunState fullPending = fullStore.CommitNodeVisitEntry(
            fullStore.PreviewNodeVisitEntry(
                MapNodeId.FromPosition(layer: 1, slot: 0),
                catalog));
        Assert.Throws<InvalidOperationException>(() =>
            fullStore.PreviewEventChoiceSettlement(
                fullPending.PendingNodeVisit.Id,
                RunEventChoiceKind.PaidHeal));
        Assert.That(fullStore.Current, Is.SameAs(fullPending));

        using var overflowStore = new RunStateStore();
        overflowStore.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("43434343-6565-8181-b3b3-313131313131")),
            heroTemplateId: 1001,
            initialHealth: 65,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            holdings: RunHoldings.Empty(initialGold: int.MaxValue)));
        RunState overflowPending = overflowStore.CommitNodeVisitEntry(
            overflowStore.PreviewNodeVisitEntry(
                MapNodeId.FromPosition(layer: 1, slot: 0),
                catalog));
        Assert.Throws<OverflowException>(() =>
            overflowStore.PreviewEventChoiceSettlement(
                overflowPending.PendingNodeVisit.Id,
                RunEventChoiceKind.GainGold));
        Assert.That(overflowStore.Current, Is.SameAs(overflowPending));
        Assert.That(overflowStore.Current.Holdings.Gold, Is.EqualTo(int.MaxValue));
    }

    /// <summary>创建 Run 时拒绝由值类型默认值绕过构造器产生的空身份。</summary>
    [Test]
    public void RunCreationOptions_WithDefaultRunId_IsRejected()
    {
        MapDefinition map = CreateMap(mapSeed: 7u);

        Assert.Throws<ArgumentException>(() => new RunCreationOptions(
            default,
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: 123456u,
            map: map));
    }

    /// <summary>提交普通直接出边后才签发绑定 NodeId 与冻结 EncounterId 的本战输入。</summary>
    [Test]
    public void CommitThenBeginBattle_FreezesSelectedNodeAndEncounterInput()
    {
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            mapSeed: 91u,
            heroTemplateId: 1002,
            initialHealth: 57,
            maxHealth: 70,
            deckTemplateId: 1002));
        MapNodeId selectedNodeId = GetFirstSelectable(created);

        RunState committed = store.CommitNode(selectedNodeId);
        RunBattleInput input = store.BeginCommittedBattle();
        RunState inBattle = store.Current;

        Assert.That(committed.ProgressPhase, Is.EqualTo(RunProgressPhase.EncounterCommitted));
        Assert.That(committed.CommittedNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(inBattle.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
        Assert.That(inBattle.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(inBattle.ActiveBattle, Is.SameAs(input));
        Assert.That(input.BattleId.RunId, Is.EqualTo(created.RunId));
        Assert.That(input.BattleId.NodeId, Is.EqualTo(selectedNodeId));
        Assert.That(input.BattleId.AttemptSequence, Is.EqualTo(1));
        Assert.That(input.EncounterTemplateId, Is.EqualTo(
            created.MapDefinition.GetNode(selectedNodeId).ContentId));
        Assert.That(input.InitialHealth, Is.EqualTo(57));
        Assert.That(input.RandomSeed, Is.Not.Zero);
        Assert.That(inBattle.PathNodeIds, Is.EqualTo(created.PathNodeIds));
    }

    /// <summary>当前本战胜利时只冻结奖励与生命，不得提前完成节点或重复生成。</summary>
    [Test]
    public void RecordVictory_FreezesRewardWithoutCompletingNode_ExactlyOnce()
    {
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "10000000-2000-3000-4000-500000000000",
            mapSeed: 31337u));
        MapNodeId selectedNodeId = GetFirstSelectable(created);
        store.CommitNode(selectedNodeId);
        RunBattleInput input = store.BeginCommittedBattle();
        int generationCount = 0;

        RunState pending = store.RecordVictoryAndFreezeReward(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80,
            battleInput =>
            {
                generationCount++;
                return new PendingCardReward(
                    new RunCardRewardId(battleInput.BattleId),
                    new[] { 3105, 3123, 3157 });
            });

        Assert.That(pending.CurrentHealth, Is.EqualTo(34));
        Assert.That(pending.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(pending.CurrentNodeId, Is.EqualTo(created.CurrentNodeId));
        Assert.That(pending.PathNodeIds, Is.EqualTo(created.PathNodeIds));
        Assert.That(pending.CommittedNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(pending.ActiveBattle, Is.Null);
        Assert.That(pending.PendingCardReward.Id.BattleId, Is.EqualTo(input.BattleId));
        Assert.That(pending.PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(new[] { 3105, 3123, 3157 }));
        Assert.That(generationCount, Is.EqualTo(1));

        Assert.Throws<InvalidOperationException>(() => store.RecordVictoryAndFreezeReward(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80,
            battleInput =>
            {
                generationCount++;
                return pending.PendingCardReward;
            }));
        Assert.That(generationCount, Is.EqualTo(1));
        Assert.That(store.Current, Is.SameAs(pending));
    }

    /// <summary>战斗结果先预览移除本战已消费药水但不发布，显式提交后才一次发布同一后继。</summary>
    [Test]
    public void BattleResultSettlement_PreviewsThenCommitsConsumedPotionRemovalExactlyOnce()
    {
        RunHoldings holdings = RunHoldings.Empty(initialGold: 73)
            .AddPotion(templateId: 6001)
            .AddPotion(templateId: 6002)
            .AddPotion(templateId: 6003);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "11000000-2000-3000-4000-500000000000",
            mapSeed: 31338u,
            holdings: holdings));
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput input = store.BeginCommittedBattle();
        RunState inBattle = store.Current;

        RunBattleResultSettlement preview = store.PreviewBattleResultSettlement(
            input.BattleId,
            BattleResultKind.Victory,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80,
            new[] { holdings.Potions[1].InstanceId },
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));

        Assert.That(store.Current, Is.SameAs(inBattle));
        Assert.That(
            store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6001, 6002, 6003 }));
        Assert.That(preview.Successor.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(
            preview.Successor.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6001, 6003 }));
        Assert.That(preview.Successor.Holdings.Gold, Is.EqualTo(73));

        RunState committed = store.CommitBattleResultSettlement(preview);

        Assert.That(committed, Is.SameAs(preview.Successor));
        Assert.That(store.Current, Is.SameAs(preview.Successor));
        Assert.Throws<InvalidOperationException>(() =>
            store.CommitBattleResultSettlement(preview));
        Assert.That(
            store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6001, 6003 }));
    }

    /// <summary>首场普通战斗胜利必须先移除本战消费药水，再据此一次冻结样本遗物与药水。</summary>
    [Test]
    public void FirstOrdinaryCombatVictory_FreezesAttachedLootAgainstPostConsumptionHoldings()
    {
        RunHoldings holdings = RunHoldings.Empty(initialGold: 73)
            .AddPotion(templateId: 6001)
            .AddPotion(templateId: 6002)
            .AddPotion(templateId: 6003);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "11500000-2000-3000-4000-500000000000",
            mapSeed: 313381u,
            holdings: holdings));
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput input = store.BeginCommittedBattle();
        RunState inBattle = store.Current;

        RunBattleResultSettlement preview = store.PreviewBattleResultSettlement(
            input.BattleId,
            BattleResultKind.Victory,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80,
            new[] { holdings.Potions[1].InstanceId },
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));

        Assert.That(store.Current, Is.SameAs(inBattle));
        Assert.That(
            preview.Successor.PendingCardReward.AttachedLoot.RelicTemplateId,
            Is.EqualTo(RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic));
        Assert.That(
            preview.Successor.PendingCardReward.AttachedLoot.PotionTemplateId,
            Is.EqualTo(RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattlePotion));
        Assert.That(
            preview.Successor.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6001, 6003 }));
    }

    /// <summary>首战若已持有样本遗物且药水满槽，冻结事实必须分别为空且不得安排补发。</summary>
    [Test]
    public void FirstOrdinaryCombatVictory_WithOwnedRelicAndFullPotions_FreezesEmptyLoot()
    {
        RunHoldings holdings = RunHoldings.Empty(initialGold: 73)
            .AddRelic(RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic)
            .AddPotion(templateId: 6001)
            .AddPotion(templateId: 6002)
            .AddPotion(templateId: 6003);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "11600000-2000-3000-4000-500000000000",
            mapSeed: 313382u,
            holdings: holdings));
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput input = store.BeginCommittedBattle();

        RunBattleResultSettlement preview = store.PreviewBattleResultSettlement(
            input.BattleId,
            BattleResultKind.Victory,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80,
            Array.Empty<RunPotionInstanceId>(),
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));

        Assert.That(preview.Successor.PendingCardReward.AttachedLoot.RelicTemplateId, Is.Null);
        Assert.That(preview.Successor.PendingCardReward.AttachedLoot.PotionTemplateId, Is.Null);
    }

    /// <summary>奖励工厂不得越过 Store 权威边界注入附着掉落，失败时保持 InBattle 零写入。</summary>
    [Test]
    public void VictoryRewardFactory_WithAttachedLoot_IsRejectedWithoutWriting()
    {
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "11700000-2000-3000-4000-500000000000",
            mapSeed: 313383u));
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput input = store.BeginCommittedBattle();
        RunState inBattle = store.Current;

        Assert.Throws<InvalidOperationException>(() => store.PreviewBattleResultSettlement(
            input.BattleId,
            BattleResultKind.Victory,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80,
            Array.Empty<RunPotionInstanceId>(),
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 },
                new RunCardRewardAttachedLoot(relicTemplateId: 8001, potionTemplateId: null))));
        Assert.That(store.Current, Is.SameAs(inBattle));
    }

    /// <summary>失败结果也必须先预览药水移除，只有显式提交后才一次进入 Terminal。</summary>
    [Test]
    public void DefeatBattleResultSettlement_RemovesConsumedPotionOnlyWhenCommitted()
    {
        RunHoldings holdings = RunHoldings.Empty(initialGold: 73)
            .AddPotion(templateId: 6001)
            .AddPotion(templateId: 6002);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "12000000-2000-3000-4000-500000000000",
            mapSeed: 31339u,
            holdings: holdings));
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput input = store.BeginCommittedBattle();
        RunState inBattle = store.Current;

        RunBattleResultSettlement preview = store.PreviewBattleResultSettlement(
            input.BattleId,
            BattleResultKind.Defeat,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80,
            new[] { holdings.Potions[0].InstanceId });

        Assert.That(store.Current, Is.SameAs(inBattle));
        Assert.That(
            store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6001, 6002 }));
        Assert.That(preview.Successor.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(
            preview.Successor.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6002 }));

        RunState committed = store.CommitBattleResultSettlement(preview);

        Assert.That(committed.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(
            committed.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6002 }));
    }

    /// <summary>重复或不属于本 attempt 的药水身份必须在预览边界失败，并保持 InBattle 完全零写入。</summary>
    [Test]
    public void BattleResultSettlement_WithDuplicateOrUnknownPotionIds_IsRejectedWithoutWriting()
    {
        RunHoldings holdings = RunHoldings.Empty(initialGold: 73)
            .AddPotion(templateId: 6001)
            .AddPotion(templateId: 6002);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "13000000-2000-3000-4000-500000000000",
            mapSeed: 31340u,
            holdings: holdings));
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput input = store.BeginCommittedBattle();
        RunState inBattle = store.Current;
        RunPotionInstanceId existingId = holdings.Potions[0].InstanceId;

        Assert.Throws<InvalidOperationException>(() => store.PreviewBattleResultSettlement(
            input.BattleId,
            BattleResultKind.Defeat,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80,
            new[] { existingId, existingId }));
        Assert.That(store.Current, Is.SameAs(inBattle));

        Assert.Throws<InvalidOperationException>(() => store.PreviewBattleResultSettlement(
            input.BattleId,
            BattleResultKind.Defeat,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80,
            new[] { new RunPotionInstanceId(999) }));
        Assert.That(store.Current, Is.SameAs(inBattle));
        Assert.That(
            store.Current.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 6001, 6002 }));
    }

    /// <summary>选择冻结候选时预览不得发布；正式结算同时只追加一次卡牌与冻结持有物。</summary>
    [Test]
    public void CardRewardSelection_PreviewsThenCommitsOneNewIndependentInstance()
    {
        var deck = new RunDeck(new[]
        {
            new RunCard(new RunCardInstanceId(2), templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(7), templateId: 3002, upgradeLevel: 1),
        });
        var holdings = new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(7), templateId: 8002),
            },
            new[]
            {
                new RunPotion(new RunPotionInstanceId(3), templateId: 6001),
            },
            gold: 73);
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("20000000-3000-4000-5000-600000000000")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: deck,
            randomRootSeed: 421337u,
            map: CreateMap(421337u),
            holdings: holdings));
        MapNodeId selectedNodeId = GetFirstSelectable(created);
        store.CommitNode(selectedNodeId);
        RunBattleInput input = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 47,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));

        RunState preview = store.PreviewCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: 3123);

        Assert.That(store.Current, Is.SameAs(pending));
        Assert.That(preview.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(preview.PathNodeIds, Is.EqualTo(
            pending.PathNodeIds.Concat(new[] { selectedNodeId })));
        Assert.That(preview.CommittedNodeId, Is.Null);
        Assert.That(preview.PendingCardReward, Is.Null);
        Assert.That(preview.CurrentHealth, Is.EqualTo(47));
        Assert.That(preview.RunDeck.Cards.Select(card => card.InstanceId.Sequence),
            Is.EqualTo(new[] { 2, 7, 8 }));
        Assert.That(preview.RunDeck.Cards.Select(card => card.TemplateId),
            Is.EqualTo(new[] { 3002, 3002, 3123 }));
        Assert.That(preview.RunDeck.Cards.Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 0, 1, 0 }));
        Assert.That(preview.Holdings.Relics.Select(relic =>
                (relic.InstanceId.Sequence, relic.TemplateId)),
            Is.EqualTo(new[] { (7, 8002), (8, 8001) }));
        Assert.That(preview.Holdings.Potions.Select(potion =>
                (potion.InstanceId.Sequence, potion.TemplateId)),
            Is.EqualTo(new[] { (3, 6001), (4, 9001) }));
        Assert.That(preview.Holdings.Gold, Is.EqualTo(73));

        RunState settled = store.CommitCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: 3123);

        Assert.That(store.Current, Is.SameAs(settled));
        Assert.That(settled.RunDeck.Cards.Last().InstanceId.Sequence, Is.EqualTo(8));
        Assert.That(settled.RunDeck.Cards.Last(), Is.Not.SameAs(deck.Cards[0]));
        Assert.That(settled.Holdings.Relics.Count, Is.EqualTo(2));
        Assert.That(settled.Holdings.Potions.Count, Is.EqualTo(2));
    }

    /// <summary>跨两场战斗可重复选卡，但持有物只随首战附着一次且第二战不再补发。</summary>
    [Test]
    public void CardRewardSelection_AcrossBattlesSameTemplateCreatesIndependentInstances()
    {
        using var store = new RunStateStore();
        RunState state = store.CreateNewRun(CreateOptions(
            "21000000-3000-4000-5000-610000000000",
            mapSeed: 531337u));
        var acquiredIds = new List<RunCardInstanceId>();
        var attachedRelicTemplateIds = new List<int?>();
        var attachedPotionTemplateIds = new List<int?>();

        for (int battleIndex = 0; battleIndex < 2; battleIndex++)
        {
            MapNodeId selectedNodeId = GetFirstSelectable(state);
            store.CommitNode(selectedNodeId);
            RunBattleInput input = store.BeginCommittedBattle();
            RunState pending = store.RecordVictoryAndFreezeReward(
                input.BattleId,
                heroTemplateId: 1001,
                settledHealth: 70 - battleIndex,
                maxHealth: 80,
                battleInput => new PendingCardReward(
                    new RunCardRewardId(battleInput.BattleId),
                    new[] { 3105, 3123, 3157 }));
            attachedRelicTemplateIds.Add(pending.PendingCardReward.AttachedLoot.RelicTemplateId);
            attachedPotionTemplateIds.Add(pending.PendingCardReward.AttachedLoot.PotionTemplateId);
            state = store.CommitCardRewardSettlement(
                pending.PendingCardReward.Id,
                selectedCardTemplateId: 3123);
            acquiredIds.Add(state.RunDeck.Cards.Last().InstanceId);
        }

        Assert.That(acquiredIds, Has.Count.EqualTo(2));
        Assert.That(acquiredIds[0], Is.Not.EqualTo(acquiredIds[1]));
        Assert.That(attachedRelicTemplateIds,
            Is.EqualTo(new int?[] { 8001, null }));
        Assert.That(attachedPotionTemplateIds,
            Is.EqualTo(new int?[] { 9001, null }));
        Assert.That(state.Holdings.Relics.Select(relic => relic.TemplateId),
            Is.EqualTo(new[] { 8001 }));
        Assert.That(state.Holdings.Potions.Select(potion => potion.TemplateId),
            Is.EqualTo(new[] { 9001 }));
        Assert.That(
            state.RunDeck.Cards.Where(card => card.TemplateId == 3123)
                .Select(card => card.InstanceId),
            Is.EqualTo(acquiredIds));
        Assert.That(
            state.RunDeck.Cards.Where(card => card.TemplateId == 3123)
                .Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 0, 0 }));
    }

    /// <summary>跳过卡牌仍完成原节点并发放相同冻结持有物，同时保持原 RunDeck 对象。</summary>
    [Test]
    public void CardRewardSkip_CompletesNodeWithoutChangingRunDeck()
    {
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "30000000-4000-5000-6000-700000000000",
            mapSeed: 741852u));
        MapNodeId selectedNodeId = GetFirstSelectable(created);
        store.CommitNode(selectedNodeId);
        RunBattleInput input = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 56,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));

        RunState settled = store.CommitCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: null);

        Assert.That(settled.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(settled.CurrentNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(settled.RunDeck, Is.SameAs(pending.RunDeck));
        Assert.That(settled.PendingCardReward, Is.Null);
        Assert.That(settled.Holdings.Relics.Single().InstanceId.Sequence, Is.EqualTo(1));
        Assert.That(settled.Holdings.Relics.Single().TemplateId, Is.EqualTo(8001));
        Assert.That(settled.Holdings.Potions.Single().InstanceId.Sequence, Is.EqualTo(1));
        Assert.That(settled.Holdings.Potions.Single().TemplateId, Is.EqualTo(9001));
    }

    /// <summary>伪造模板、过期身份与重复提交都必须在发布前拒绝并保持同一快照。</summary>
    [Test]
    public void CardRewardSettlement_ForgedStaleAndDuplicateCommandsAreZeroWrite()
    {
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "40000000-5000-6000-7000-800000000000",
            mapSeed: 963258u));
        store.CommitNode(GetFirstSelectable(created));
        RunBattleInput input = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 62,
            maxHealth: 80,
            battleInput => new PendingCardReward(
                new RunCardRewardId(battleInput.BattleId),
                new[] { 3105, 3123, 3157 }));
        var staleId = new RunCardRewardId(new RunBattleId(
            pending.RunId,
            pending.BattleAttemptSequence + 1,
            pending.CommittedNodeId.Value));

        Assert.Throws<InvalidOperationException>(() =>
            store.PreviewCardRewardSettlement(staleId, selectedCardTemplateId: 3105));
        Assert.Throws<InvalidOperationException>(() =>
            store.PreviewCardRewardSettlement(
                pending.PendingCardReward.Id,
                selectedCardTemplateId: 3999));
        Assert.That(store.Current, Is.SameAs(pending));

        RunState settled = store.CommitCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: 3105);

        Assert.Throws<InvalidOperationException>(() =>
            store.CommitCardRewardSettlement(
                pending.PendingCardReward.Id,
                selectedCardTemplateId: 3105));
        Assert.That(store.Current, Is.SameAs(settled));
        Assert.That(settled.RunDeck.Cards.Count(card => card.TemplateId == 3105), Is.EqualTo(1));
    }

    /// <summary>普通战斗失败立即进入零生命终局，不完成失败节点且任何继续迁移都被拒绝。</summary>
    [Test]
    public void RecordDefeat_EntersTerminalWithoutCompletingOrRetryingNode()
    {
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "abcdefab-cdef-abcd-efab-cdefabcdefab",
            mapSeed: 424242u));
        MapNodeId failedNodeId = GetFirstSelectable(created);
        store.CommitNode(failedNodeId);
        RunBattleInput failedAttempt = store.BeginCommittedBattle();

        RunState terminal = store.RecordDefeat(
            failedAttempt.BattleId,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80);

        Assert.That(terminal.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(terminal.TerminalReason, Is.EqualTo(RunTerminalReason.Defeat));
        Assert.That(terminal.CurrentHealth, Is.Zero);
        Assert.That(terminal.PathNodeIds, Is.EqualTo(created.PathNodeIds));
        Assert.That(terminal.CommittedNodeId, Is.EqualTo(failedNodeId));
        Assert.That(terminal.ActiveBattle, Is.Null);
        Assert.Throws<InvalidOperationException>(() => store.CommitNode(failedNodeId));
        Assert.Throws<InvalidOperationException>(() => store.BeginCommittedBattle());
    }

    /// <summary>本战 seed 派生在完整正整数 attempt 空间内不得复现旧压缩算法的碰撞。</summary>
    [Test]
    public void BattleSeedDerivation_PreviouslyCollidingAttempts_AreDifferent()
    {
        uint first = RunStateStore.DeriveBattleSeed(123456789u, 50549);
        uint second = RunStateStore.DeriveBattleSeed(123456789u, 63342);

        Assert.That(first, Is.InRange(1u, (uint)int.MaxValue));
        Assert.That(second, Is.InRange(1u, (uint)int.MaxValue));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    /// <summary>Store 以只读事实流依次发布创建、承诺与入战的完整不可变状态。</summary>
    [Test]
    public void StateStream_PublishesEachImmutableRunState()
    {
        var observed = new List<RunState>();
        using var store = new RunStateStore();
        using IDisposable subscription = store.State.Subscribe(observed.Add);
        RunState created = store.CreateNewRun(CreateOptions(
            "12345678-90ab-cdef-1234-567890abcdef",
            mapSeed: 77u));
        store.CommitNode(GetFirstSelectable(created));
        store.BeginCommittedBattle();

        Assert.That(observed.Count, Is.EqualTo(4));
        Assert.That(observed[0], Is.Null);
        Assert.That(observed[1], Is.SameAs(created));
        Assert.That(observed[2].ProgressPhase, Is.EqualTo(RunProgressPhase.EncounterCommitted));
        Assert.That(observed[3].ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
    }

    /// <summary>建立带确定性 G3 地图的有效 Run 创建输入。</summary>
    private static RunCreationOptions CreateOptions(
        string runId,
        uint mapSeed,
        int heroTemplateId = 1001,
        int initialHealth = 80,
        int maxHealth = 80,
        int deckTemplateId = 1001,
        RunHoldings holdings = null)
    {
        return new RunCreationOptions(
            new RunId(Guid.Parse(runId)),
            heroTemplateId,
            initialHealth,
            maxHealth,
            RunDeck.CreateInitial(new[]
            {
                deckTemplateId == 1002 ? 3201 : 3002,
            }),
            randomRootSeed: mapSeed,
            map: CreateMap(mapSeed),
            holdings: holdings);
    }

    /// <summary>从当前固定 profile 生成一张通过生产 validator 的地图。</summary>
    private static MapDefinition CreateMap(uint mapSeed)
    {
        MapDefinition map = ActMapGenerator.Generate(TinySpireActMapProfiles.Current, mapSeed);
        Assert.That(
            ActMapValidator.Validate(map, TinySpireActMapProfiles.Current).IsValid,
            Is.True);
        return map;
    }

    /// <summary>建立只含 Start 与一个直接可达非战斗节点的最小 Store 测试地图。</summary>
    private static MapDefinition CreateSingleNonCombatMap(
        MapNodeKind kind,
        int contentId)
    {
        MapNodeId startNodeId = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId destinationNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        return new MapDefinition(
            profileId: "tinyspire.test.noncombat.v1",
            generatorVersion: 1,
            mapSeed: 42420001u,
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

    /// <summary>读取当前位置按普通规则可选的第一个节点。</summary>
    private static MapNodeId GetFirstSelectable(RunState state)
    {
        return MapReachability.GetSelectableNodeIds(
            state.MapDefinition,
            state.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];
    }

    /// <summary>为 Store 进入测试提供固定且完整的非战斗权威目录。</summary>
    private sealed class NodeVisitEntryCatalogStub : IRunNodeVisitEntryCatalog
    {
        /// <summary>测试可在 preview 后切换配置，证明 commit 不重建后继。</summary>
        internal bool AllowUpgrade { get; set; } = true;

        /// <summary>测试可模拟全局卡仍存在但在进入后被移出当前 Hero 奖励池。</summary>
        internal bool ShopCardInHeroPool { get; set; } = true;

        /// <summary>只允许测试卡 3002 从零级升级到一级。</summary>
        public bool IsCardUpgradeLevelValid(int templateId, int upgradeLevel)
        {
            return AllowUpgrade && templateId == 3002 && upgradeLevel == 1;
        }

        /// <summary>仅登记程序化遗物 anchor。</summary>
        public bool RelicExists(int templateId)
        {
            return templateId == RunNodeVisitIdentityCatalog.SampleRelicTemplateId;
        }

        /// <summary>仅登记程序化药水 anchor。</summary>
        public bool PotionExists(int templateId)
        {
            return templateId == RunNodeVisitIdentityCatalog.SamplePotionTemplateId;
        }

        /// <summary>返回一个稳定的 Hero 商店卡牌候选。</summary>
        public HeroCardRewardPool CreateHeroCardRewardPool(int heroTemplateId)
        {
            return new HeroCardRewardPool(
                heroTemplateId,
                new CardRewardRarityWeights(60, 37, 3),
                new[]
                {
                    new CardRewardCandidate(
                        ShopCardInHeroPool ? 3002 : 4002,
                        cfg.battle.CardRarity.Common),
                    new CardRewardCandidate(
                        ShopCardInHeroPool ? 3003 : 4003,
                        cfg.battle.CardRarity.Uncommon),
                    new CardRewardCandidate(
                        ShopCardInHeroPool ? 3004 : 4004,
                        cfg.battle.CardRarity.Rare),
                });
        }
    }
}
