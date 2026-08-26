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

    /// <summary>选择冻结候选时预览不得发布；正式结算只追加一个全新实例并完成原节点。</summary>
    [Test]
    public void CardRewardSelection_PreviewsThenCommitsOneNewIndependentInstance()
    {
        var deck = new RunDeck(new[]
        {
            new RunCard(new RunCardInstanceId(2), templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(7), templateId: 3002, upgradeLevel: 1),
        });
        using var store = new RunStateStore();
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("20000000-3000-4000-5000-600000000000")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: deck,
            randomRootSeed: 421337u,
            map: CreateMap(421337u)));
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

        RunState settled = store.CommitCardRewardSettlement(
            pending.PendingCardReward.Id,
            selectedCardTemplateId: 3123);

        Assert.That(store.Current, Is.SameAs(settled));
        Assert.That(settled.RunDeck.Cards.Last().InstanceId.Sequence, Is.EqualTo(8));
        Assert.That(settled.RunDeck.Cards.Last(), Is.Not.SameAs(deck.Cards[0]));
    }

    /// <summary>跨两场战斗可重复选择同模板，但每次都必须追加不同的稳定实例身份。</summary>
    [Test]
    public void CardRewardSelection_AcrossBattlesSameTemplateCreatesIndependentInstances()
    {
        using var store = new RunStateStore();
        RunState state = store.CreateNewRun(CreateOptions(
            "21000000-3000-4000-5000-610000000000",
            mapSeed: 531337u));
        var acquiredIds = new List<RunCardInstanceId>();

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
            state = store.CommitCardRewardSettlement(
                pending.PendingCardReward.Id,
                selectedCardTemplateId: 3123);
            acquiredIds.Add(state.RunDeck.Cards.Last().InstanceId);
        }

        Assert.That(acquiredIds, Has.Count.EqualTo(2));
        Assert.That(acquiredIds[0], Is.Not.EqualTo(acquiredIds[1]));
        Assert.That(
            state.RunDeck.Cards.Where(card => card.TemplateId == 3123)
                .Select(card => card.InstanceId),
            Is.EqualTo(acquiredIds));
        Assert.That(
            state.RunDeck.Cards.Where(card => card.TemplateId == 3123)
                .Select(card => card.UpgradeLevel),
            Is.EqualTo(new[] { 0, 0 }));
    }

    /// <summary>跳过奖励只完成原节点，保持原有 RunDeck 对象与全部实例事实不变。</summary>
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
        int deckTemplateId = 1001)
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
