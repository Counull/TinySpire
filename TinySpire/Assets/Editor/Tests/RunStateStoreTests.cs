using System;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using TinySpire.Run;

public sealed class RunStateStoreTests
{
    /// <summary>创建新 Run 时冻结唯一身份、单英雄基础事实与唯一可进入节点。</summary>
    [Test]
    public void CreateNewRun_FreezesIdentityHeroAndSingleAvailableNode()
    {
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 123456u);

        using var store = new RunStateStore();
        RunState state = store.CreateNewRun(options);

        Assert.That(state.RunId, Is.EqualTo(options.RunId));
        Assert.That(state.HeroTemplateId, Is.EqualTo(1001));
        Assert.That(state.CurrentHealth, Is.EqualTo(80));
        Assert.That(state.MaxHealth, Is.EqualTo(80));
        Assert.That(state.DeckTemplateId, Is.EqualTo(1001));
        Assert.That(state.EncounterTemplateId, Is.EqualTo(5001));
        Assert.That(state.RandomRootSeed, Is.EqualTo(123456u));
        Assert.That(state.NodeStatus, Is.EqualTo(RunNodeStatus.Available));
        Assert.That(state.BattleAttemptSequence, Is.Zero);
        Assert.That(state.ActiveBattle, Is.Null);
        Assert.That(state.BattleSnapshot, Is.Null);
        Assert.That(store.Current, Is.SameAs(state));
    }

    /// <summary>创建 Run 时拒绝由值类型默认值绕过构造器产生的空身份。</summary>
    [Test]
    public void RunCreationOptions_WithDefaultRunId_IsRejected()
    {
        using var store = new RunStateStore();

        Assert.Throws<ArgumentException>(() => new RunCreationOptions(
            default,
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 123456u));
        Assert.That(store.Current, Is.Null);
    }

    /// <summary>进入唯一节点时冻结进战前事实，并签发本次战斗唯一输入。</summary>
    [Test]
    public void BeginBattle_FreezesPreBattleSnapshotAndPublishesActiveInput()
    {
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            heroTemplateId: 1002,
            initialHealth: 57,
            maxHealth: 70,
            deckTemplateId: 1002,
            encounterTemplateId: 5001,
            randomRootSeed: 987654321u);
        using var store = new RunStateStore();
        RunState beforeBattle = store.CreateNewRun(options);

        RunBattleInput input = store.BeginBattle();
        RunState inBattle = store.Current;

        Assert.That(inBattle, Is.Not.SameAs(beforeBattle));
        Assert.That(inBattle.NodeStatus, Is.EqualTo(RunNodeStatus.InBattle));
        Assert.That(inBattle.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(inBattle.ActiveBattle, Is.SameAs(input));
        Assert.That(input.BattleId.RunId, Is.EqualTo(options.RunId));
        Assert.That(input.BattleId.AttemptSequence, Is.EqualTo(1));
        Assert.That(input.HeroTemplateId, Is.EqualTo(1002));
        Assert.That(input.InitialHealth, Is.EqualTo(57));
        Assert.That(input.MaxHealth, Is.EqualTo(70));
        Assert.That(input.DeckTemplateId, Is.EqualTo(1002));
        Assert.That(input.EncounterTemplateId, Is.EqualTo(5001));
        Assert.That(input.RandomSeed, Is.Not.Zero);
        Assert.That(inBattle.BattleSnapshot, Is.Not.Null);
        Assert.That(inBattle.BattleSnapshot.RunId, Is.EqualTo(options.RunId));
        Assert.That(inBattle.BattleSnapshot.CurrentHealth, Is.EqualTo(57));
        Assert.That(inBattle.BattleSnapshot.DeckTemplateId, Is.EqualTo(1002));
        Assert.That(inBattle.BattleSnapshot.NodeStatus, Is.EqualTo(RunNodeStatus.Available));
    }

    /// <summary>当前本战胜利时原子写回结算生命、完成节点并清除战斗暂存事实。</summary>
    [Test]
    public void ApplyVictory_WritesSettledHealthCompletesNodeAndClearsBattleFacts()
    {
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("10000000-2000-3000-4000-500000000000")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 31337u);
        using var store = new RunStateStore();
        store.CreateNewRun(options);
        RunBattleInput input = store.BeginBattle();

        RunState completed = store.ApplyVictory(
            input.BattleId,
            heroTemplateId: 1001,
            settledHealth: 34,
            maxHealth: 80);

        Assert.That(completed.CurrentHealth, Is.EqualTo(34));
        Assert.That(completed.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));
        Assert.That(completed.BattleAttemptSequence, Is.EqualTo(1));
        Assert.That(completed.ActiveBattle, Is.Null);
        Assert.That(completed.BattleSnapshot, Is.Null);
        Assert.That(store.Current, Is.SameAs(completed));
    }

    /// <summary>失败不污染进战前事实，重开恢复 snapshot 并签发不同本战 seed。</summary>
    [Test]
    public void RestartAfterDefeat_RestoresSnapshotAndUsesNewBattleSeed()
    {
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("abcdefab-cdef-abcd-efab-cdefabcdefab")),
            heroTemplateId: 1002,
            initialHealth: 41,
            maxHealth: 70,
            deckTemplateId: 1002,
            encounterTemplateId: 5001,
            randomRootSeed: 424242u);
        using var store = new RunStateStore();
        store.CreateNewRun(options);
        RunBattleInput failedAttempt = store.BeginBattle();

        RunState failed = store.RecordDefeat(
            failedAttempt.BattleId,
            heroTemplateId: 1002,
            settledHealth: 0,
            maxHealth: 70);

        Assert.That(failed.NodeStatus, Is.EqualTo(RunNodeStatus.Failed));
        Assert.That(failed.CurrentHealth, Is.EqualTo(41));
        Assert.That(failed.ActiveBattle, Is.Null);
        Assert.That(failed.BattleSnapshot, Is.Not.Null);
        Assert.That(failed.BattleSnapshot.CurrentHealth, Is.EqualTo(41));

        RunBattleInput retry = store.RestartBattle();

        Assert.That(retry.BattleId.AttemptSequence, Is.EqualTo(2));
        Assert.That(retry.InitialHealth, Is.EqualTo(41));
        Assert.That(retry.DeckTemplateId, Is.EqualTo(1002));
        Assert.That(retry.RandomSeed, Is.Not.EqualTo(failedAttempt.RandomSeed));
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.InBattle));
        Assert.That(store.Current.BattleAttemptSequence, Is.EqualTo(2));
        Assert.That(store.Current.BattleSnapshot.CurrentHealth, Is.EqualTo(41));
    }

    /// <summary>本战 seed 派生在完整正整数 attempt 空间内不得复现旧压缩算法的碰撞。</summary>
    [Test]
    public void BattleSeedDerivation_PreviouslyCollidingAttempts_AreDifferent()
    {
        uint first = RunStateStore.DeriveBattleSeed(
            randomRootSeed: 123456789u,
            attemptSequence: 50549);
        uint second = RunStateStore.DeriveBattleSeed(
            randomRootSeed: 123456789u,
            attemptSequence: 63342);

        Assert.That(first, Is.InRange(1u, (uint)int.MaxValue));
        Assert.That(second, Is.InRange(1u, (uint)int.MaxValue));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    /// <summary>Store 以只读事实流依次发布创建与入战后的完整不可变状态。</summary>
    [Test]
    public void StateStream_PublishesEachImmutableRunState()
    {
        var observed = new List<RunState>();
        using var store = new RunStateStore();
        using IDisposable subscription = store.State.Subscribe(observed.Add);
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef")),
            heroTemplateId: 1001,
            initialHealth: 30,
            maxHealth: 30,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 77u);

        RunState created = store.CreateNewRun(options);
        store.BeginBattle();

        Assert.That(observed.Count, Is.EqualTo(3));
        Assert.That(observed[0], Is.Null);
        Assert.That(observed[1], Is.SameAs(created));
        Assert.That(observed[2], Is.SameAs(store.Current));
        Assert.That(observed[2].NodeStatus, Is.EqualTo(RunNodeStatus.InBattle));
    }

    /// <summary>非法或过期迁移均被拒绝，且不会替换最后一份有效 Run 事实。</summary>
    [Test]
    public void IllegalAndStaleTransitions_AreRejectedWithoutMutatingState()
    {
        using var store = new RunStateStore();
        Assert.Throws<InvalidOperationException>(() => store.BeginBattle());
        var options = new RunCreationOptions(
            new RunId(Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 999u);
        store.CreateNewRun(options);
        RunBattleInput firstAttempt = store.BeginBattle();
        RunState firstInBattle = store.Current;

        Assert.Throws<InvalidOperationException>(() => store.BeginBattle());
        Assert.Throws<InvalidOperationException>(() => store.ApplyVictory(
            new RunBattleId(options.RunId, 2),
            heroTemplateId: 1001,
            settledHealth: 70,
            maxHealth: 80));
        Assert.That(store.Current, Is.SameAs(firstInBattle));

        store.RecordDefeat(firstAttempt.BattleId, 1001, 0, 80);
        Assert.Throws<InvalidOperationException>(() => store.RecordDefeat(
            firstAttempt.BattleId,
            heroTemplateId: 1001,
            settledHealth: 0,
            maxHealth: 80));
        RunBattleInput retry = store.RestartBattle();
        Assert.Throws<InvalidOperationException>(() => store.ApplyVictory(
            firstAttempt.BattleId,
            heroTemplateId: 1001,
            settledHealth: 70,
            maxHealth: 80));
        Assert.That(store.Current.ActiveBattle, Is.SameAs(retry));
    }
}
