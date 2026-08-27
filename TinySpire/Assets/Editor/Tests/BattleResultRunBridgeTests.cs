using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.Run.Map;
using VContainer;

public sealed class BattleResultRunBridgeTests
{
    /// <summary>BattleResult 对消费身份排序并防御复制，调用方随后改动原列表不得篡改结果。</summary>
    [Test]
    public void BattleResult_ConsumedPotionIds_AreSortedAndDefensivelyFrozen()
    {
        var consumedIds = new List<RunPotionInstanceId>
        {
            new RunPotionInstanceId(9),
            new RunPotionInstanceId(2),
        };
        var result = new BattleResult(
            BattleResultKind.Victory,
            authoritySequence: 7,
            roundNumber: 2,
            new[]
            {
                new BattleResultPlayerSnapshot(
                    new CombatantId(1),
                    templateId: 1001,
                    health: 31,
                    maxHealth: 80),
            },
            consumedIds);

        consumedIds.Clear();

        Assert.That(
            result.ConsumedPotionInstanceIds.Select(id => id.Sequence),
            Is.EqualTo(new[] { 2, 9 }));
        Assert.Throws<ArgumentException>(() => new BattleResult(
            BattleResultKind.Victory,
            authoritySequence: 8,
            roundNumber: 2,
            result.Players,
            new[] { new RunPotionInstanceId(2), new RunPotionInstanceId(2) }));
    }

    /// <summary>bridge 忽略初始空值，并只把首个稳定结果转发给当前 Run attempt。</summary>
    [Test]
    public void Initialize_FirstStableResult_IsForwardedExactlyOnce()
    {
        using var store = CreateActiveRun(out _);
        var scenes = new RecordingSceneFlow();
        var saves = new RecordingRunSaveStore();
        var flow = CreateFlow(store, scenes, saves);
        BattleSetupOptions setup = flow.CreateBattleSetupOptions();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(flow).AsSelf();
        using IObjectResolver resolver = builder.Build();
        using var results = new ReactiveProperty<BattleResult>(null);
        using ReadOnlyReactiveProperty<BattleResult> resultView = results.ToReadOnlyReactiveProperty();
        using var bridge = new BattleResultRunBridge(resultView, setup, resolver);

        bridge.Initialize();
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));

        results.Value = CreateBattleResult(BattleResultKind.Victory, health: 31);
        PendingCardReward frozenReward = store.Current.PendingCardReward;
        int[] frozenCandidates = frozenReward.CandidateTemplateIds.ToArray();
        results.Value = CreateBattleResult(BattleResultKind.Victory, health: 22);

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(31));
        Assert.That(store.Current.PendingCardReward, Is.SameAs(frozenReward));
        Assert.That(store.Current.PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(frozenCandidates));
        Assert.That(frozenCandidates, Is.EquivalentTo(new[] { 3105, 3123, 3157 }));
        Assert.That(store.Current.RunDeck.Cards, Has.Count.EqualTo(3));
        Assert.That(store.Current.RunDeck.Cards[0].InstanceId, Is.EqualTo(new RunCardInstanceId(51)));
        Assert.That(store.Current.RunDeck.Cards[0].TemplateId, Is.EqualTo(3002));
        Assert.That(store.Current.RunDeck.Cards[0].UpgradeLevel, Is.EqualTo(1));
        Assert.That(store.Current.RunDeck.Cards[1].InstanceId, Is.EqualTo(new RunCardInstanceId(77)));
        Assert.That(store.Current.RunDeck.Cards[1].TemplateId, Is.EqualTo(3002));
        Assert.That(store.Current.RunDeck.Cards[1].UpgradeLevel, Is.Zero);
        Assert.That(store.Current.RunDeck.Cards[2].InstanceId, Is.EqualTo(new RunCardInstanceId(81)));
        Assert.That(store.Current.RunDeck.Cards[2].TemplateId, Is.EqualTo(3123));
        Assert.That(store.Current.RunDeck.Cards[2].UpgradeLevel, Is.EqualTo(2));
        Assert.That(saves.Documents, Has.Count.EqualTo(1));
        Assert.That(saves.Documents[0].ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(saves.Documents[0].PendingCardReward.RewardId,
            Is.EqualTo(frozenReward.Id.ToString()));
        Assert.That(saves.Documents[0].PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(frozenCandidates));
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

        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.InBattle));
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

    /// <summary>建立已提交地图节点与 active attempt 的最小 RunStateStore。</summary>
    private static RunStateStore CreateActiveRun(out RunBattleInput input)
    {
        var store = new RunStateStore();
        MapDefinition map = ActMapGenerator.Generate(TinySpireActMapProfiles.Current, 13579u);
        RunState created = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("aaaa1111-bbbb-2222-cccc-3333dddd4444")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            runDeck: new RunDeck(new[]
            {
                new RunCard(new RunCardInstanceId(51), templateId: 3002, upgradeLevel: 1),
                new RunCard(new RunCardInstanceId(77), templateId: 3002, upgradeLevel: 0),
                new RunCard(new RunCardInstanceId(81), templateId: 3123, upgradeLevel: 2),
            }),
            randomRootSeed: 13579u,
            map: map));
        MapNodeId selectedNodeId = MapReachability.GetSelectableNodeIds(
            map,
            created.CurrentNodeId,
            MapTraversalMode.Ordinary)[0];
        store.CommitNode(selectedNodeId);
        input = store.BeginCommittedBattle();
        return store;
    }

    /// <summary>以最小奖励配置和不会取用的新 Run 随机源建立结果编排 Flow。</summary>
    private static RunFlowService CreateFlow(
        RunStateStore store,
        RecordingSceneFlow scenes,
        RecordingRunSaveStore saves = null)
    {
        return new RunFlowService(
            store,
            CreateTables,
            scenes,
            new UnusedRunEntropySource(),
            saves ?? new RecordingRunSaveStore());
    }

    /// <summary>创建 bridge 胜利结算所需的一名 Hero 与三个合法奖励模板。</summary>
    private static cfg.Tables CreateTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0,\"reward_card_template_ids\":[3105,3123,3157],\"reward_common_weight\":60,\"reward_uncommon_weight\":37,\"reward_rare_weight\":3}]"),
            ["battle_tbcard"] = new JArray(
                CreateCardRow(3105, rarity: 1),
                CreateCardRow(3123, rarity: 2),
                CreateCardRow(3157, rarity: 3)),
        };
        return new cfg.Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>建立 bridge 奖励池使用的最小 Implemented 卡牌行。</summary>
    private static JObject CreateCardRow(int templateId, int rarity)
    {
        return new JObject
        {
            ["id"] = templateId,
            ["external_key"] = $"TEST_BRIDGE_{templateId}",
            ["catalog_snapshot_key"] = "test-fixture",
            ["name_i18n_key"] = $"battle.card.{templateId}.name",
            ["description_i18n_key"] = $"battle.card.{templateId}.description",
            ["upgraded_description_i18n_key"] = $"battle.card.{templateId}.description",
            ["card_type"] = 0,
            ["rarity"] = rarity,
            ["cost"] = 1,
            ["cost_kind"] = 0,
            ["upgraded_cost"] = 1,
            ["target_rule"] = 1,
            ["play_destination"] = 0,
            ["upgraded_play_destination"] = 0,
            ["has_upgrade"] = false,
            ["implementation_status"] = 0,
            ["effect_bindings"] = new JArray(),
            ["illustration_key"] = string.Empty,
            ["program_id"] = 0,
            ["is_innate"] = false,
            ["upgrade_track_kind"] = 0,
            ["infinite_upgrade_rule_kind"] = 0,
            ["infinite_upgrade_value_per_level"] = 0,
        };
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

    /// <summary>记录 bridge 触发的稳定检查点，证明重复结果不会再次写入。</summary>
    private sealed class RecordingRunSaveStore : IRunSaveStore
    {
        /// <summary>按提交顺序保留全部文档引用。</summary>
        public List<RunSaveDocument> Documents { get; } = new List<RunSaveDocument>();

        /// <summary>bridge 测试没有预存档。</summary>
        public RunSaveLoadResult Load()
        {
            return RunSaveLoadResult.NotFound();
        }

        /// <summary>记录一次成功提交。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            Documents.Add(document ?? throw new ArgumentNullException(nameof(document)));
            return RunSaveCommitResult.Succeeded();
        }

        /// <summary>bridge 测试不会请求删除，保持幂等成功。</summary>
        public RunSaveDeleteResult Delete()
        {
            return RunSaveDeleteResult.Succeeded();
        }
    }
}
