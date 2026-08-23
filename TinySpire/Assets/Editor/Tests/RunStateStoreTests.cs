using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
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
        Assert.That(state.DeckTemplateId, Is.EqualTo(1001));
        Assert.That(state.RandomRootSeed, Is.EqualTo(123456u));
        Assert.That(state.MapDefinition, Is.SameAs(options.Map));
        Assert.That(state.CurrentNodeId, Is.EqualTo(MapNodeId.FromPosition(0, 0)));
        Assert.That(state.PathNodeIds, Is.EqualTo(new[] { MapNodeId.FromPosition(0, 0) }));
        Assert.That(state.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(state.BattleAttemptSequence, Is.Zero);
        Assert.That(state.ActiveBattle, Is.Null);
        Assert.That(store.Current, Is.SameAs(state));
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
            deckTemplateId: 1001,
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

    /// <summary>当前本战胜利时原子写回生命、追加完成路径并回到地图。</summary>
    [Test]
    public void ApplyVictory_AppendsCompletedNodeAndReturnsToMap()
    {
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(CreateOptions(
            "10000000-2000-3000-4000-500000000000",
            mapSeed: 31337u));
        MapNodeId selectedNodeId = GetFirstSelectable(created);
        store.CommitNode(selectedNodeId);
        RunBattleInput input = store.BeginCommittedBattle();

        RunState completed = store.ApplyVictory(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80);

        Assert.That(completed.CurrentHealth, Is.EqualTo(34));
        Assert.That(completed.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(completed.CurrentNodeId, Is.EqualTo(selectedNodeId));
        Assert.That(completed.PathNodeIds.Last(), Is.EqualTo(selectedNodeId));
        Assert.That(completed.CommittedNodeId, Is.Null);
        Assert.That(completed.ActiveBattle, Is.Null);
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
        int deckTemplateId = 1001)
    {
        return new RunCreationOptions(
            new RunId(Guid.Parse(runId)),
            heroTemplateId,
            initialHealth,
            maxHealth,
            deckTemplateId,
            randomRootSeed: mapSeed,
            map: CreateMap(mapSeed));
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

    /// <summary>读取当前位置按普通规则可选的第一个节点。</summary>
    private static MapNodeId GetFirstSelectable(RunState state)
    {
        return MapReachability.GetSelectableNodeIds(
            state.MapDefinition,
            state.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];
    }
}
