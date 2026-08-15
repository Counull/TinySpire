using System;
using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.UI.Run;
using UnityEngine.Localization;

public sealed class RunEntryPresenterTests
{
    /// <summary>主菜单进入角色选择后，两名配置 Hero 都可分别确认并创建对应的单角色 Run。</summary>
    [TestCase(1001)]
    [TestCase(1002)]
    public void MainMenu_SelectHeroAndConfirm_CreatesRunAndShowsMap(int heroTemplateId)
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 101u);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));

        view.Emit(new RunEntryAction(RunEntryActionKind.StartGame));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.HeroSelection));
        Assert.That(view.LastModel.GetText(RunEntryTextSlot.Hero1001Name), Is.EqualTo("Warrior"));
        Assert.That(view.LastModel.GetText(RunEntryTextSlot.Hero1002Name), Is.EqualTo("Machine Gunner"));
        Assert.That(view.LastModel.ConfirmEnabled, Is.False);

        view.Emit(new RunEntryAction(RunEntryActionKind.SelectHero, heroTemplateId));
        Assert.That(view.LastModel.SelectedHeroTemplateId, Is.EqualTo(heroTemplateId));
        Assert.That(view.LastModel.ConfirmEnabled, Is.True);

        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmHero));

        Assert.That(store.Current, Is.Not.Null);
        Assert.That(store.Current.HeroTemplateId, Is.EqualTo(heroTemplateId));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.BattleNodeInteractable, Is.True);
        Assert.That(view.LastModel.BattleNodeCompleted, Is.False);
        Assert.That(scenes.LoadedAddresses, Is.Empty);
    }

    /// <summary>设置、图鉴与统计都是只在入口内切换的可返回页面，不创建 Run。</summary>
    [TestCase(RunEntryActionKind.OpenSettings, RunEntryPage.Settings)]
    [TestCase(RunEntryActionKind.OpenCompendium, RunEntryPage.Compendium)]
    [TestCase(RunEntryActionKind.OpenStatistics, RunEntryPage.Statistics)]
    public void MainMenu_OpenAuxiliaryPageAndBack_DoesNotCreateRun(
        RunEntryActionKind actionKind,
        RunEntryPage expectedPage)
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 202u),
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();
        view.Emit(new RunEntryAction(actionKind));
        Assert.That(view.LastModel.Page, Is.EqualTo(expectedPage));

        view.Emit(new RunEntryAction(RunEntryActionKind.Back));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(store.Current, Is.Null);
    }

    /// <summary>地图节点动作只调用 RunFlow；生成的 setup 与进入 BattleScene 的目标仍来自 Run。</summary>
    [Test]
    public void Map_EnterBattleNode_UsesRunFlowAndDisablesNodeImmediately()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 303u);
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
        view.Emit(new RunEntryAction(RunEntryActionKind.EnterBattle));

        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.InBattle));
        Assert.That(store.Current.ActiveBattle.HeroTemplateId, Is.EqualTo(1001));
        Assert.That(view.LastModel.BattleNodeInteractable, Is.False);
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
    }

    /// <summary>失败页重开只委托 RunFlow，恢复 snapshot 并以不同 seed 进入全新 BattleScene。</summary>
    [Test]
    public async System.Threading.Tasks.Task Failure_RestartBattle_RestoresSnapshotAndUsesNewSeed()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 404u);
        flow.CreateNewRun(heroTemplateId: 1002);
        RunBattleInput failedInput = await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Defeat, heroTemplateId: 1002, health: 0, maxHealth: 90));
        scenes.LoadedAddresses.Clear();
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Failure));

        view.Emit(new RunEntryAction(RunEntryActionKind.RestartBattle));

        Assert.That(store.Current.NodeStatus, Is.EqualTo(RunNodeStatus.InBattle));
        Assert.That(store.Current.ActiveBattle.InitialHealth, Is.EqualTo(90));
        Assert.That(store.Current.ActiveBattle.RandomSeed, Is.Not.EqualTo(failedInput.RandomSeed));
        Assert.That(scenes.LoadedAddresses, Is.EqualTo(new[] { RunSceneAddresses.Battle }));
    }

    /// <summary>完成节点永远不可再次进入；语言变化仅重新投影当前 Run，不产生第二份业务状态。</summary>
    [Test]
    public async System.Threading.Tasks.Task CompletedMap_LocaleChanged_ReprojectsWithoutReopeningNode()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 505u);
        flow.CreateNewRun(heroTemplateId: 1001);
        await flow.EnterBattleNodeAsync();
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, heroTemplateId: 1001, health: 31, maxHealth: 80));
        scenes.LoadedAddresses.Clear();
        var view = new RecordingRunEntryView();
        int localizationVersion = 0;
        string VersionedLocalize(string key, IReadOnlyDictionary<string, object> arguments)
        {
            // 以版本前缀模拟语言切换后的重新投影。
            return $"v{localizationVersion}:{Localize(key, arguments)}";
        }

        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            VersionedLocalize,
            localeChanges);
        presenter.Initialize();
        RunState completed = store.Current;
        int initialRenderCount = view.RenderCount;
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.BattleNodeCompleted, Is.True);
        Assert.That(view.LastModel.BattleNodeInteractable, Is.False);

        view.Emit(new RunEntryAction(RunEntryActionKind.EnterBattle));
        localizationVersion = 1;
        localeChanges.OnNext(null);

        Assert.That(store.Current, Is.SameAs(completed));
        Assert.That(scenes.LoadedAddresses, Is.Empty);
        Assert.That(view.RenderCount, Is.EqualTo(initialRenderCount + 1));
        Assert.That(view.LastModel.GetText(RunEntryTextSlot.MapTitle), Does.StartWith("v1:"));
    }

    /// <summary>场景释放 Presenter 后，旧 View、RunState 与语言事件都不得再触发渲染。</summary>
    [Test]
    public void Dispose_UnsubscribesViewStateAndLocaleCallbacks()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var view = new RecordingRunEntryView();
        var flow = CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 606u);
        var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);
        presenter.Initialize();
        int renderedBeforeDispose = view.RenderCount;

        presenter.Dispose();
        view.Emit(new RunEntryAction(RunEntryActionKind.StartGame));
        store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 606u));
        localeChanges.OnNext(null);

        Assert.That(view.RenderCount, Is.EqualTo(renderedBeforeDispose));
    }

    /// <summary>创建带确定 Run 身份、配置与随机输入的测试编排。</summary>
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
                new RunId(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef")),
                randomRootSeed)));
    }

    /// <summary>创建两名候选 Hero、两副起始牌组与唯一遭遇的最小 Luban 表。</summary>
    private static Tables CreateTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.machine_gunner.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":3,\"max_ammo\":6,\"ammo_gain_per_round\":1,\"runtime_profile\":1}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002]},{\"id\":1002,\"card_template_ids\":[3003]}]"),
            ["battle_tbencounter"] = JArray.Parse(
                "[{\"id\":5001,\"enemy_template_ids\":[2001]}]"),
        };

        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>以稳定键映射模拟当前语言，并保留 Smart 参数用于生命文本断言。</summary>
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
            case "run.entry.map.health":
                return $"HP {arguments["current"]}/{arguments["max"]}";
            default:
                return key;
        }
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

    /// <summary>记录 View 唯一动作流和最后一次不可变投影。</summary>
    private sealed class RecordingRunEntryView : IRunEntryView
    {
        /// <summary>Presenter 订阅的唯一入口动作事件。</summary>
        public event Action<RunEntryAction> ActionRequested;

        /// <summary>最后一次收到的不可变页面投影。</summary>
        public RunEntryViewModel LastModel { get; private set; }

        /// <summary>累计渲染次数，用于验证语言变化只重投影。</summary>
        public int RenderCount { get; private set; }

        /// <summary>保存 Presenter 提交的完整投影。</summary>
        public void Render(RunEntryViewModel model)
        {
            LastModel = model;
            RenderCount++;
        }

        /// <summary>模拟单个 UI 控件发出业务意图，不直接写 Run。</summary>
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

        /// <summary>记录场景目标并同步完成测试期请求。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            LoadedAddresses.Add(targetSceneAddress);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>为测试返回一次确定的 Run 身份与根随机输入。</summary>
    private sealed class FixedRunEntropySource : IRunEntropySource
    {
        private readonly RunEntropy _entropy;

        /// <summary>保存应返回的确定输入。</summary>
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
}
