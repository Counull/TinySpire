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
    /// <summary>冷启动发现有效档时仍停留主菜单，只有玩家点继续才 hydrate 并进入地图。</summary>
    [Test]
    public void ColdStartValidSave_EnablesContinueAndHydratesOnlyAfterAction()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new InMemoryRunSaveStore();
        saves.Commit(new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            "13572468-2468-1357-2468-135724681357",
            heroTemplateId: 1001,
            currentHealth: 46,
            maxHealth: 80,
            deckTemplateId: 1001,
            encounterTemplateId: 5001,
            randomRootSeed: 919191u,
            RunSaveNodeStatus.Available,
            battleAttemptSequence: 0));
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 1u, saves),
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(store.Current, Is.Null);
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(view.LastModel.ContinueEnabled, Is.True);

        view.Emit(new RunEntryAction(RunEntryActionKind.ContinueGame));

        Assert.That(store.Current, Is.Not.Null);
        Assert.That(store.Current.CurrentHealth, Is.EqualTo(46));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
    }

    /// <summary>有效单槽存在时新开局必须先确认放弃；取消不删档，确认后才进入角色选择。</summary>
    [Test]
    public void ValidSave_StartGame_RequiresConfirmedAbandonBeforeHeroSelection()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore(CreateSaveDocument());
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 11u, saves),
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();
        view.Emit(new RunEntryAction(RunEntryActionKind.StartGame));

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.AbandonConfirmation));
        Assert.That(saves.DeleteCount, Is.Zero);

        view.Emit(new RunEntryAction(RunEntryActionKind.Back));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(saves.DeleteCount, Is.Zero);
        Assert.That(saves.Load().Status, Is.EqualTo(RunSaveLoadStatus.Success));

        view.Emit(new RunEntryAction(RunEntryActionKind.StartGame));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmAbandon));

        Assert.That(saves.DeleteCount, Is.EqualTo(1));
        Assert.That(saves.Load().Status, Is.EqualTo(RunSaveLoadStatus.NotFound));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.HeroSelection));
        Assert.That(store.Current, Is.Null);
    }

    /// <summary>不可恢复单槽必须禁用继续并显示类型化说明；同样只有玩家确认后才删除。</summary>
    [TestCase("invalid_json", "run.entry.save.issue.invalid_json")]
    [TestCase("unsupported_schema", "run.entry.save.issue.unsupported_schema")]
    [TestCase("missing_config", "Missing Hero 9999")]
    public void UnusableSave_DisablesContinueExplainsIssueAndDeletesOnlyAfterConfirmation(
        string issueKind,
        string expectedIssue)
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        ScriptedRunSaveStore saves;
        switch (issueKind)
        {
            case "invalid_json":
                saves = new ScriptedRunSaveStore(RunSaveLoadResult.Failed(
                    RunSaveLoadStatus.InvalidJson,
                    "Malformed JSON.",
                    hasStoredData: true));
                break;
            case "unsupported_schema":
                saves = new ScriptedRunSaveStore(RunSaveLoadResult.Failed(
                    RunSaveLoadStatus.UnsupportedSchema,
                    "Schema 99 cannot be migrated.",
                    hasStoredData: true));
                break;
            case "missing_config":
                saves = new ScriptedRunSaveStore(CreateSaveDocument(heroTemplateId: 9999));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(issueKind));
        }

        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            CreateFlow(store, new RecordingSceneFlow(), randomRootSeed: 12u, saves),
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(view.LastModel.ContinueEnabled, Is.False);
        Assert.That(view.LastModel.GetText(RunEntryTextSlot.SaveIssue), Is.EqualTo(expectedIssue));
        Assert.That(saves.DeleteCount, Is.Zero);

        view.Emit(new RunEntryAction(RunEntryActionKind.StartGame));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.AbandonConfirmation));
        Assert.That(saves.DeleteCount, Is.Zero);

        view.Emit(new RunEntryAction(RunEntryActionKind.Back));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(saves.DeleteCount, Is.Zero);

        view.Emit(new RunEntryAction(RunEntryActionKind.StartGame));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmAbandon));

        Assert.That(saves.DeleteCount, Is.EqualTo(1));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.HeroSelection));
    }

    /// <summary>S0 提交失败必须阻止推进；重试提交同一 Run 后返回地图且不重取身份或随机输入。</summary>
    [Test]
    public void InitialCommitFailure_RetrySave_ReturnsSameRunToMap()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        var saves = new ScriptedRunSaveStore();
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected commit failure."));
        saves.EnqueueCommitResult(RunSaveCommitResult.Succeeded());
        var entropy = new CountingRunEntropySource(new RunEntropy(
            new RunId(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef")),
            13u));
        var flow = new RunFlowService(
            store,
            CreateTables,
            new RecordingSceneFlow(),
            entropy,
            saves);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();
        view.Emit(new RunEntryAction(RunEntryActionKind.StartGame));
        view.Emit(new RunEntryAction(RunEntryActionKind.SelectHero, 1001));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmHero));
        RunState failedRun = store.Current;

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(view.LastModel.BattleNodeInteractable, Is.False);
        Assert.That(saves.CommitCount, Is.EqualTo(1));
        Assert.That(entropy.NextCount, Is.EqualTo(1));

        view.Emit(new RunEntryAction(RunEntryActionKind.RetrySave));

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.Map));
        Assert.That(view.LastModel.BattleNodeInteractable, Is.True);
        Assert.That(store.Current, Is.SameAs(failedRun));
        Assert.That(saves.CommitCount, Is.EqualTo(2));
        Assert.That(saves.CommittedDocuments[1], Is.SameAs(saves.CommittedDocuments[0]));
        Assert.That(entropy.NextCount, Is.EqualTo(1));
    }

    /// <summary>S1 提交失败退出前必须二次确认；取消保留未保存 Run，确认才回到上一成功检查点。</summary>
    [Test]
    public async System.Threading.Tasks.Task CompletedCommitFailure_ExitRequiresConfirmationAndReturnsPreviousCheckpoint()
    {
        using var store = new RunStateStore();
        using var localeChanges = new Subject<Locale>();
        RunSaveDocument previousCheckpoint = CreateSaveDocument();
        var saves = new ScriptedRunSaveStore(previousCheckpoint);
        saves.EnqueueCommitResult(RunSaveCommitResult.Failed(
            RunSaveCommitStatus.IoFailure,
            "Injected S1 failure."));
        var scenes = new RecordingSceneFlow();
        var flow = CreateFlow(store, scenes, randomRootSeed: 14u, saves);
        var view = new RecordingRunEntryView();
        using var presenter = new RunEntryPresenter(
            view,
            store,
            flow,
            CreateTables,
            Localize,
            localeChanges);

        presenter.Initialize();
        view.Emit(new RunEntryAction(RunEntryActionKind.ContinueGame));
        view.Emit(new RunEntryAction(RunEntryActionKind.EnterBattle));
        RunBattleId battleId = flow.BindBattleAttempt(flow.CreateBattleSetupOptions());
        await flow.HandleBattleResultAsync(
            battleId,
            CreateBattleResult(BattleResultKind.Victory, heroTemplateId: 1001, health: 30, maxHealth: 80));
        RunState unsavedCompletedRun = store.Current;

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(unsavedCompletedRun.NodeStatus, Is.EqualTo(RunNodeStatus.Completed));

        view.Emit(new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.RollbackConfirmation));

        view.Emit(new RunEntryAction(RunEntryActionKind.Back));
        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.SaveFailure));
        Assert.That(store.Current, Is.SameAs(unsavedCompletedRun));
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));

        view.Emit(new RunEntryAction(RunEntryActionKind.RequestExitAfterSaveFailure));
        view.Emit(new RunEntryAction(RunEntryActionKind.ConfirmRollback));

        Assert.That(view.LastModel.Page, Is.EqualTo(RunEntryPage.MainMenu));
        Assert.That(view.LastModel.ContinueEnabled, Is.True);
        Assert.That(store.Current, Is.Null);
        Assert.That(saves.Load().Document, Is.SameAs(previousCheckpoint));
    }

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
        Assert.That(
            view.LastModel.GetText(RunEntryTextSlot.Cleared),
            Is.EqualTo("v0:节点已清除、后续内容未接入"));

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
            case "run.entry.map.cleared":
                return "节点已清除、后续内容未接入";
            case "run.entry.save.issue.missing_configuration":
                return $"Missing {arguments["kind"]} {arguments["id"]}";
            default:
                return key;
        }
    }

    /// <summary>创建一份可恢复的稳定 S0 文档，允许单独替换引用 ID 制造失配。</summary>
    private static RunSaveDocument CreateSaveDocument(
        int heroTemplateId = 1001,
        int deckTemplateId = 1001,
        int encounterTemplateId = 5001)
    {
        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            "13572468-2468-1357-2468-135724681357",
            heroTemplateId,
            currentHealth: 46,
            maxHealth: 80,
            deckTemplateId,
            encounterTemplateId,
            randomRootSeed: 919191u,
            RunSaveNodeStatus.Available,
            battleAttemptSequence: 0);
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

    /// <summary>记录 entropy 请求次数，证明存档重试不会偷偷创建第二个 Run。</summary>
    private sealed class CountingRunEntropySource : IRunEntropySource
    {
        private readonly RunEntropy _entropy;

        /// <summary>累计业务请求随机根输入的次数。</summary>
        public int NextCount { get; private set; }

        /// <summary>保存每次应返回的同一确定输入。</summary>
        public CountingRunEntropySource(RunEntropy entropy)
        {
            _entropy = entropy;
        }

        /// <summary>记录请求并返回确定输入。</summary>
        public RunEntropy Next()
        {
            NextCount++;
            return _entropy;
        }
    }

    /// <summary>以脚本化 load/commit/delete 结果验证 Presenter 的系统边界行为。</summary>
    private sealed class ScriptedRunSaveStore : IRunSaveStore
    {
        private readonly Queue<RunSaveCommitResult> _commitResults =
            new Queue<RunSaveCommitResult>();
        private RunSaveDocument _document;
        private RunSaveLoadResult _forcedLoadResult;

        /// <summary>累计 commit 调用次数。</summary>
        public int CommitCount { get; private set; }

        /// <summary>累计玩家确认后的 delete 调用次数。</summary>
        public int DeleteCount { get; private set; }

        /// <summary>记录每次提交的文档引用，供重试身份断言。</summary>
        public List<RunSaveDocument> CommittedDocuments { get; } =
            new List<RunSaveDocument>();

        /// <summary>创建空单槽。</summary>
        public ScriptedRunSaveStore()
        {
        }

        /// <summary>创建含一份最近成功检查点的单槽。</summary>
        public ScriptedRunSaveStore(RunSaveDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>创建每次 load 都先返回指定故障的单槽。</summary>
        public ScriptedRunSaveStore(RunSaveLoadResult forcedLoadResult)
        {
            _forcedLoadResult = forcedLoadResult
                ?? throw new ArgumentNullException(nameof(forcedLoadResult));
        }

        /// <summary>安排下一次 commit 的确定结果。</summary>
        public void EnqueueCommitResult(RunSaveCommitResult result)
        {
            _commitResults.Enqueue(result ?? throw new ArgumentNullException(nameof(result)));
        }

        /// <summary>返回脚本化故障、当前成功检查点或空槽。</summary>
        public RunSaveLoadResult Load()
        {
            if (_forcedLoadResult != null)
                return _forcedLoadResult;

            return _document == null
                ? RunSaveLoadResult.NotFound()
                : RunSaveLoadResult.Succeeded(_document);
        }

        /// <summary>按队列返回提交结果，并只在成功时替换最近检查点。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            CommitCount++;
            CommittedDocuments.Add(document);
            RunSaveCommitResult result = _commitResults.Count > 0
                ? _commitResults.Dequeue()
                : RunSaveCommitResult.Succeeded();
            if (result.Status == RunSaveCommitStatus.Success)
                _document = document;
            return result;
        }

        /// <summary>模拟玩家确认后的幂等删除，并清除故障发现结果。</summary>
        public RunSaveDeleteResult Delete()
        {
            DeleteCount++;
            _document = null;
            _forcedLoadResult = null;
            return RunSaveDeleteResult.Succeeded();
        }
    }
}
