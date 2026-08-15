using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cfg;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Run;

public sealed class RunFlowServiceTests
{
    /// <summary>新 Run 与入战编排只从配置和 RunState 生成既有 Battle setup seam 的完整输入。</summary>
    [Test]
    public async Task CreateRunAndEnterBattle_MapsRunFactsToBattleSetupAndSceneFlow()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var entropy = new FixedRunEntropySource(
            new RunEntropy(
                new RunId(Guid.Parse("11112222-3333-4444-5555-666677778888")),
                randomRootSeed: 987654321u));
        var flow = new RunFlowService(store, CreateTables, scenes, entropy);

        RunState created = flow.CreateNewRun(heroTemplateId: 1002);
        RunBattleInput input = await flow.EnterBattleNodeAsync();
        BattleSetupOptions setup = flow.CreateBattleSetupOptions();

        Assert.That(created.HeroTemplateId, Is.EqualTo(1002));
        Assert.That(created.CurrentHealth, Is.EqualTo(90));
        Assert.That(created.DeckTemplateId, Is.EqualTo(1002));
        Assert.That(input.BattleId.RunId, Is.EqualTo(created.RunId));
        Assert.That(input.RandomSeed, Is.LessThanOrEqualTo(int.MaxValue));
        Assert.That(setup.HeroTemplateId, Is.EqualTo(input.HeroTemplateId));
        Assert.That(setup.EncounterTemplateId, Is.EqualTo(input.EncounterTemplateId));
        Assert.That(setup.PlayerInitialHealth, Is.EqualTo(input.InitialHealth));
        Assert.That(setup.DeckTemplateId, Is.EqualTo(input.DeckTemplateId));
        Assert.That(setup.RandomSeed, Is.EqualTo(input.RandomSeed));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
    }

    /// <summary>稳定胜利结果经唯一 bridge seam 写回结算生命、完成节点并返回入口地图。</summary>
    [Test]
    public async Task HandleBattleResult_WithVictory_SettlesRunBeforeReturningToMap()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 12345u);
        flow.CreateNewRun(heroTemplateId: 1002);
        await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, heroTemplateId: 1002, health: 37, maxHealth: 90));

        Assert.That(store.Current.CurrentHealth, Is.EqualTo(37));
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));
        Assert.That(store.Current.ActiveBattle, Is.Null);
        Assert.That(store.Current.BattleSnapshot, Is.Null);
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[] { RunSceneAddresses.Battle, RunSceneAddresses.RunEntry }));
    }

    /// <summary>失败结果保留 snapshot；重开恢复战前生命、签发新 seed 并再次进入 BattleScene。</summary>
    [Test]
    public async Task HandleBattleResult_WithDefeat_RestartsSnapshotWithNewBattleSeed()
    {
        using var store = new RunStateStore();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 424242u);
        flow.CreateNewRun(heroTemplateId: 1001);
        RunBattleInput failedAttempt = await flow.EnterBattleNodeAsync();
        RunBattleId failedBattleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());

        await flow.HandleBattleResultAsync(
            failedBattleId,
            CreateBattleResult(BattleResultKind.Defeat, heroTemplateId: 1001, health: 0, maxHealth: 80));
        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.Failed));
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(80));
        Assert.That(store.Current.BattleSnapshot.CurrentHealth, Is.EqualTo(80));

        RunBattleInput retry = await flow.RestartFailedBattleAsync();

        Assert.That(retry.InitialHealth, Is.EqualTo(80));
        Assert.That(retry.RandomSeed, Is.Not.EqualTo(failedAttempt.RandomSeed));
        Assert.That(retry.BattleId.AttemptSequence, Is.EqualTo(2));
        Assert.That(
            scenes.LoadedAddresses,
            Is.EqualTo(new[]
            {
                RunSceneAddresses.Battle,
                RunSceneAddresses.RunEntry,
                RunSceneAddresses.Battle,
            }));
    }

    /// <summary>创建带确定身份、可变根 seed 与既有配置的 Flow 测试装配。</summary>
    private static RunFlowService CreateFlow(
        RunStateStore store,
        RecordingSceneFlow scenes,
        uint randomRootSeed)
    {
        return new RunFlowService(
            store,
            CreateTables,
            scenes,
            new FixedRunEntropySource(new RunEntropy(
                new RunId(Guid.Parse("99990000-aaaa-bbbb-cccc-ddddeeeeffff")),
                randomRootSeed)));
    }

    /// <summary>冻结一个单玩家 BattleResult，模拟命令队列表现屏障后的唯一公开结果。</summary>
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

    /// <summary>创建仅含两名可选 Hero 与固定临时遭遇的最小 Run 配置表。</summary>
    private static Tables CreateTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.machine_gunner.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":3,\"max_ammo\":6,\"ammo_gain_per_round\":1,\"runtime_profile\":1}]") ,
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002]},{\"id\":1002,\"card_template_ids\":[3003]}]"),
            ["battle_tbencounter"] = JArray.Parse(
                "[{\"id\":5001,\"enemy_template_ids\":[2001]}]"),
        };

        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>记录 Run 编排请求的稳定场景地址，替代真实 Addressables 切换。</summary>
    private sealed class RecordingSceneFlow : ISceneFlowService
    {
        /// <summary>按调用顺序保存全部目标场景地址。</summary>
        public List<string> LoadedAddresses { get; } = new List<string>();

        /// <summary>记录目标地址并同步完成测试期场景切换。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            LoadedAddresses.Add(targetSceneAddress);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>为测试提供一次确定的 Run 身份与根随机输入。</summary>
    private sealed class FixedRunEntropySource : IRunEntropySource
    {
        private readonly RunEntropy _entropy;

        /// <summary>保存下一局应使用的确定输入。</summary>
        public FixedRunEntropySource(RunEntropy entropy)
        {
            _entropy = entropy;
        }

        /// <summary>返回固定输入，便于断言派生后的完整 Run 事实。</summary>
        public RunEntropy Next()
        {
            return _entropy;
        }
    }
}
