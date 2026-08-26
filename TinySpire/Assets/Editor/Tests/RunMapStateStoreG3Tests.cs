using System;
using System.Linq;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class RunMapStateStoreG3Tests
{
    /// <summary>普通节点先承诺但不完成路径，再签发冻结遭遇的 BattleInput，胜利后回到地图稳定态。</summary>
    [Test]
    public void CommitEncounterThenVictory_AppendsPathOnceAndReturnsToMapReady()
    {
        MapDefinition map = CreateMap();
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002, 3003 }),
            randomRootSeed: 123456u,
            map: map));
        MapNodeId selectedNodeId = MapReachability.GetSelectableNodeIds(
            map,
            created.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];

        RunState committed = store.CommitNode(selectedNodeId);
        RunBattleInput input = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 37,
            maxHealth: 80,
            battleInput => CreatePendingReward(battleInput));
        RunState settled = store.CommitCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: null);

        Assert.That(created.MapDefinition, Is.SameAs(map));
        Assert.That(created.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(created.PathNodeIds.Select(id => id.Value), Is.EqualTo(new[] { "L00-S00" }));
        Assert.That(committed.ProgressPhase, Is.EqualTo(RunProgressPhase.EncounterCommitted));
        Assert.That(committed.ActiveBattle, Is.Null);
        Assert.That(committed.CommittedNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(input.BattleId.NodeId, Is.EqualTo(selectedNodeId));
        Assert.That(input.EncounterTemplateId, Is.EqualTo(map.GetNode(selectedNodeId).ContentId));
        Assert.That(settled.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(settled.CurrentHealth, Is.EqualTo(37));
        Assert.That(settled.CurrentNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(settled.PathNodeIds, Has.Count.EqualTo(2));
        Assert.That(settled.ActiveBattle, Is.Null);
    }

    /// <summary>Store 必须自行拒绝非直接出边目标，且失败命令不得发布半份进度。</summary>
    [Test]
    public void CommitNode_WhenTargetIsNotOrdinarilySelectable_LeavesStateUntouched()
    {
        MapDefinition map = CreateMap();
        using var store = CreateStore(map);
        RunState before = store.Current;
        MapNodeId lockedNodeId = MapNodeId.FromPosition(layer: 2, slot: 0);

        Assert.Throws<InvalidOperationException>(() => store.CommitNode(lockedNodeId));

        Assert.That(store.Current, Is.SameAs(before));
        Assert.That(store.Current.PathNodeIds.Select(id => id.Value), Is.EqualTo(new[] { "L00-S00" }));
    }

    /// <summary>最后普通节点之后选择 Boss 终点只抵达并保存门，不签发真实 Boss 战。</summary>
    [Test]
    public void CommitNode_WhenReachableTargetIsBoss_ReachesStableBossGate()
    {
        MapDefinition map = CreateMap();
        using var store = CreateStore(map);
        CompleteFirstSelectableCombat(store);
        CompleteFirstSelectableCombat(store);
        MapNodeId bossNodeId = MapReachability.GetSelectableNodeIds(
            map,
            store.Current.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];

        RunState reached = store.CommitNode(bossNodeId);

        Assert.That(map.GetNode(bossNodeId).Kind, Is.EqualTo(MapNodeKind.Boss));
        Assert.That(reached.ProgressPhase, Is.EqualTo(RunProgressPhase.BossGateReached));
        Assert.That(reached.CurrentNodeId, Is.EqualTo(bossNodeId));
        Assert.That(reached.CommittedNodeId, Is.Null);
        Assert.That(reached.ActiveBattle, Is.Null);
        Assert.That(reached.PathNodeIds.Last(), Is.EqualTo(bossNodeId));
        Assert.Throws<InvalidOperationException>(() => store.BeginCommittedBattle());
    }

    /// <summary>普通战斗失败必须原子进入 Terminal(Defeat)，不得恢复 snapshot 或继续地图。</summary>
    [Test]
    public void RecordDefeat_TerminatesRunWithoutCompletingCommittedNode()
    {
        MapDefinition map = CreateMap();
        using var store = CreateStore(map);
        MapNodeId selectedNodeId = MapReachability.GetSelectableNodeIds(
            map,
            store.Current.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];
        store.CommitNode(selectedNodeId);
        RunBattleInput input = store.BeginCommittedBattle();

        RunState terminal = store.RecordDefeat(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80);

        Assert.That(terminal.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(terminal.TerminalReason, Is.EqualTo(RunTerminalReason.Defeat));
        Assert.That(terminal.CurrentHealth, Is.EqualTo(0));
        Assert.That(terminal.CurrentNodeId, Is.EqualTo(MapNodeId.FromPosition(0, 0)));
        Assert.That(terminal.CommittedNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(terminal.PathNodeIds.Select(id => id.Value), Is.EqualTo(new[] { "L00-S00" }));
        Assert.That(terminal.ActiveBattle, Is.Null);
        Assert.Throws<InvalidOperationException>(() => store.BeginCommittedBattle());
        Assert.Throws<InvalidOperationException>(() => store.CommitNode(selectedNodeId));
    }

    /// <summary>旧 attempt 不能结算随后已承诺的新节点，拒绝时当前状态必须保持同一快照。</summary>
    [Test]
    public void RecordVictory_WithStaleBattleId_LeavesNewAttemptUntouched()
    {
        MapDefinition map = CreateMap();
        using var store = CreateStore(map);
        MapNodeId firstNodeId = MapReachability.GetSelectableNodeIds(
            map,
            store.Current.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];
        store.CommitNode(firstNodeId);
        RunBattleInput oldInput = store.BeginCommittedBattle();
        RunState firstPending = store.RecordVictoryAndFreezeReward(
            oldInput.BattleId,
            heroTemplateId: 1001,
            settledHealth: 70,
            maxHealth: 80,
            CreatePendingReward);
        store.CommitCardRewardSettlement(
            firstPending.PendingCardReward.Id,
            selectedCardTemplateId: null);
        MapNodeId nextNodeId = MapReachability.GetSelectableNodeIds(
            map,
            store.Current.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];
        store.CommitNode(nextNodeId);
        store.BeginCommittedBattle();
        RunState before = store.Current;

        Assert.Throws<InvalidOperationException>(() =>
            store.RecordVictoryAndFreezeReward(
                oldInput.BattleId,
                heroTemplateId: 1001,
                settledHealth: 60,
                maxHealth: 80,
                CreatePendingReward));

        Assert.That(store.Current, Is.SameAs(before));
    }

    /// <summary>建立一局包含冻结测试地图的 Store。</summary>
    private static RunStateStore CreateStore(MapDefinition map)
    {
        var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: RunDeck.CreateInitial(new[] { 3002, 3003 }),
            randomRootSeed: 123456u,
            map: map));
        return store;
    }

    /// <summary>选择、进入并胜利结算当前第一项普通节点。</summary>
    private static void CompleteFirstSelectableCombat(RunStateStore store)
    {
        MapNodeId nodeId = MapReachability.GetSelectableNodeIds(
            store.Current.MapDefinition,
            store.Current.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];
        store.CommitNode(nodeId);
        RunBattleInput input = store.BeginCommittedBattle();
        RunState pending = store.RecordVictoryAndFreezeReward(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: store.Current.CurrentHealth,
            maxHealth: 80,
            CreatePendingReward);
        store.CommitCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: null);
    }

    /// <summary>为旧地图行为测试冻结一组固定不同模板的合法普通奖励。</summary>
    private static PendingCardReward CreatePendingReward(RunBattleInput battleInput)
    {
        return new PendingCardReward(
            new RunCardRewardId(battleInput.BattleId),
            new[] { 3105, 3123, 3157 });
    }

    /// <summary>创建包含两个普通层与多个 Boss 候选的确定性测试地图。</summary>
    private static MapDefinition CreateMap()
    {
        var profile = new ActMapProfile(
            "test.store.g3.v1",
            new[] { 2, 2 },
            new[] { 101, 102 },
            new[] { 901, 902, 903 },
            bossCandidateCount: 2,
            bossEndpointCount: 3);
        return ActMapGenerator.Generate(profile, mapSeed: 98765u);
    }
}
