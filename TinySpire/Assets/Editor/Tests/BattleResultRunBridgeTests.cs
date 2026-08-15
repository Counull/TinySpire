using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Run;
using VContainer;

public sealed class BattleResultRunBridgeTests
{
    /// <summary>bridge 忽略初始空值，并只把首个稳定结果转发给当前 Run attempt。</summary>
    [Test]
    public void Initialize_FirstStableResult_IsForwardedExactlyOnce()
    {
        using var store = CreateActiveRun(out _);
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes);
        BattleSetupOptions setup = flow.CreateBattleSetupOptions();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(flow).AsSelf();
        using IObjectResolver resolver = builder.Build();
        using var results = new ReactiveProperty<BattleResult>(null);
        using ReadOnlyReactiveProperty<BattleResult> resultView = results.ToReadOnlyReactiveProperty();
        using var bridge = new BattleResultRunBridge(resultView, setup, resolver);

        bridge.Initialize();
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.InBattle));

        results.Value = CreateBattleResult(BattleResultKind.Victory, health: 31);
        results.Value = CreateBattleResult(BattleResultKind.Victory, health: 22);

        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(31));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.RunEntry }));
    }

    /// <summary>BattleScope 释放 bridge 后，旧结果源不得再调用跨场景 Run 编排。</summary>
    [Test]
    public void Dispose_BeforeResult_PreventsOldBattleCallback()
    {
        using var store = CreateActiveRun(out RunBattleInput activeInput);
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes);
        using var results = new ReactiveProperty<BattleResult>(null);
        using ReadOnlyReactiveProperty<BattleResult> resultView = results.ToReadOnlyReactiveProperty();
        var bridge = new BattleResultRunBridge(resultView, flow, flow.CreateBattleSetupOptions());
        bridge.Initialize();

        bridge.Dispose();
        results.Value = CreateBattleResult(BattleResultKind.Defeat, health: 0);

        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.InBattle));
        Assert.That(store.Current.ActiveBattle, Is.SameAs(activeInput));
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>没有 RunFlow 的 legacy/debug Battle 将 bridge 初始化为无订阅空操作。</summary>
    [Test]
    public void Initialize_WithoutRunFlow_IsNoOpForLegacyBattle()
    {
        var builder = new ContainerBuilder();
        using IObjectResolver resolver = builder.Build();
        using var results = new ReactiveProperty<BattleResult>(null);
        using ReadOnlyReactiveProperty<BattleResult> resultView = results.ToReadOnlyReactiveProperty();
        using var bridge = new BattleResultRunBridge(
            resultView,
            new BattleSetupOptions(1001, 5001, randomSeed: 5),
            resolver);

        Assert.DoesNotThrow(bridge.Initialize);
        Assert.DoesNotThrow(() =>
            results.Value = CreateBattleResult(BattleResultKind.Defeat, health: 0));
    }

    /// <summary>Bootstrap 已注册但尚无 active Run 时，bridge 仍不绑定、不订阅也不抛错。</summary>
    [Test]
    public void Initialize_WithIdleRunFlow_IsNoOpForLegacyBattle()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(flow).AsSelf();
        using IObjectResolver resolver = builder.Build();
        using var results = new ReactiveProperty<BattleResult>(null);
        using ReadOnlyReactiveProperty<BattleResult> resultView = results.ToReadOnlyReactiveProperty();

        Assert.DoesNotThrow(() =>
        {
            using var bridge = new BattleResultRunBridge(
                resultView,
                new BattleSetupOptions(1001, 5001, randomSeed: 5),
                resolver);
            bridge.Initialize();
            results.Value = CreateBattleResult(BattleResultKind.Defeat, health: 0);
        });
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>建立已冻结 snapshot 与 active attempt 的最小 RunStateStore。</summary>
    private static RunStateStore CreateActiveRun(out RunBattleInput input)
    {
        var store = new RunStateStore();
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("aaaa1111-bbbb-2222-cccc-3333dddd4444")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 13579u));
        input = store.BeginBattle();
        return store;
    }

    /// <summary>以不会读取配置或随机源的依赖建立结果编排 Flow。</summary>
    private static RunFlowService CreateFlow(RunStateStore store, RecordingSceneFlow scenes)
    {
        return new RunFlowService(
            store,
            () => null,
            scenes,
            new UnusedRunEntropySource());
    }

    /// <summary>创建与测试 Run Hero 对应的单玩家稳定战斗结果。</summary>
    private static BattleResult CreateBattleResult(BattleResultKind kind, int health)
    {
        return new BattleResult(
            kind,
            authoritySequence: 1,
            roundNumber: 1,
            new[]
            {
                new BattleResultPlayerSnapshot(
                    new CombatantId(1),
                    templateId: 1001,
                    health,
                    maxHealth: 80),
            });
    }

    /// <summary>记录 bridge 完成结算后请求的场景地址。</summary>
    private sealed class RecordingSceneFlow : ISceneFlowService
    {
        /// <summary>按调用顺序保存场景目标。</summary>
        public List<string> LoadedAddresses { get; } = new List<string>();

        /// <summary>同步记录并完成测试场景请求。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            LoadedAddresses.Add(targetSceneAddress);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>结果 bridge 测试不会创建新 Run，因此该随机源只负责阻止误调用。</summary>
    private sealed class UnusedRunEntropySource : IRunEntropySource
    {
        /// <summary>若结果路径错误请求新 Run，则立即使测试失败。</summary>
        public RunEntropy Next()
        {
            throw new InvalidOperationException("Bridge result handling must not create a new run.");
        }
    }
}
