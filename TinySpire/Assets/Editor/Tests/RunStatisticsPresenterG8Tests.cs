using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.Run.History;
using TinySpire.Run.History.Presentation;
using TinySpire.Run.Map;

public sealed class RunStatisticsPresenterG8Tests
{
    private static readonly DateTimeOffset FrozenUtc =
        new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>没有任何历史时必须渲染正常零态，而不是故障或伪造一局。</summary>
    [Test]
    public void Initialize_EmptyHistoryRendersReadyZeroState()
    {
        using var harness = new PresenterHarness(new InMemoryRunHistoryRepository());

        harness.Presenter.Initialize();

        Assert.That(harness.View.Models, Has.Count.EqualTo(1));
        StatisticsViewModel model = harness.View.Models[0];
        Assert.That(model.Status, Is.EqualTo(RunStatisticsViewStatus.Ready));
        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.VictoryCount, Is.EqualTo(0));
        Assert.That(model.DefeatCount, Is.EqualTo(0));
        Assert.That(model.AbandonedCount, Is.EqualTo(0));
        Assert.That(model.VictoryRate, Is.EqualTo(0d));
        Assert.That(model.VictoryRateText, Is.EqualTo("0%"));
        Assert.That(model.HeroRows, Is.Empty);
        Assert.That(model.IsEmpty, Is.True);
        Assert.That(
            model.GetText(RunStatisticsTextSlot.EmptyHistory),
            Is.EqualTo("en:app.statistics.empty"));
        Assert.That(model.FailureText, Is.Empty);
    }

    /// <summary>三种 outcome 必须从逐局历史精确聚合为 1/1/1 与 33.3% 胜率。</summary>
    [Test]
    public void Initialize_ThreeOutcomesRenderExactAggregate()
    {
        var repository = new InMemoryRunHistoryRepository();
        repository.Record(CreateSummary(
            RunOutcomeKind.Victory,
            "11111111-aaaa-bbbb-cccc-111111111111",
            heroTemplateId: 1001,
            completedMinute: 1));
        repository.Record(CreateSummary(
            RunOutcomeKind.Defeat,
            "22222222-aaaa-bbbb-cccc-222222222222",
            heroTemplateId: 1001,
            completedMinute: 2));
        repository.Record(CreateSummary(
            RunOutcomeKind.Abandoned,
            "33333333-aaaa-bbbb-cccc-333333333333",
            heroTemplateId: 1001,
            completedMinute: 3));
        using var harness = new PresenterHarness(repository);

        harness.Presenter.Initialize();

        StatisticsViewModel model = harness.View.Models.Single();
        Assert.That(model.Status, Is.EqualTo(RunStatisticsViewStatus.Ready));
        Assert.That(model.TotalRuns, Is.EqualTo(3));
        Assert.That(model.VictoryCount, Is.EqualTo(1));
        Assert.That(model.DefeatCount, Is.EqualTo(1));
        Assert.That(model.AbandonedCount, Is.EqualTo(1));
        Assert.That(model.VictoryRate, Is.EqualTo(1d / 3d).Within(0.000001d));
        Assert.That(model.VictoryRateText, Is.EqualTo("33.3%"));
        Assert.That(model.IsEmpty, Is.False);
        Assert.That(
            model.GetText(RunStatisticsTextSlot.VictoryLabel),
            Is.EqualTo("en:app.statistics.victory"));
    }

    /// <summary>按 Hero 分组必须稳定排序，并保留各自 V/D/A 与胜率。</summary>
    [Test]
    public void Initialize_MultipleHeroesRenderStableIndependentRows()
    {
        var repository = new InMemoryRunHistoryRepository();
        repository.Record(CreateSummary(
            RunOutcomeKind.Victory,
            "44444444-aaaa-bbbb-cccc-444444444444",
            heroTemplateId: 1002,
            completedMinute: 4));
        repository.Record(CreateSummary(
            RunOutcomeKind.Defeat,
            "55555555-aaaa-bbbb-cccc-555555555555",
            heroTemplateId: 1001,
            completedMinute: 5));
        repository.Record(CreateSummary(
            RunOutcomeKind.Abandoned,
            "66666666-aaaa-bbbb-cccc-666666666666",
            heroTemplateId: 1001,
            completedMinute: 6));
        using var harness = new PresenterHarness(repository);

        harness.Presenter.Initialize();

        IReadOnlyList<StatisticsHeroRowViewModel> rows =
            harness.View.Models.Single().HeroRows;
        Assert.That(rows.Select(row => row.HeroTemplateId),
            Is.EqualTo(new[] { 1001, 1002 }));
        Assert.That(rows[0].HeroText, Is.EqualTo("en:app.statistics.hero:1001"));
        Assert.That(rows[0].TotalRuns, Is.EqualTo(2));
        Assert.That(rows[0].VictoryCount, Is.Zero);
        Assert.That(rows[0].DefeatCount, Is.EqualTo(1));
        Assert.That(rows[0].AbandonedCount, Is.EqualTo(1));
        Assert.That(rows[0].VictoryRateText, Is.EqualTo("0%"));
        Assert.That(rows[1].HeroText, Is.EqualTo("en:app.statistics.hero:1002"));
        Assert.That(rows[1].TotalRuns, Is.EqualTo(1));
        Assert.That(rows[1].VictoryCount, Is.EqualTo(1));
        Assert.That(rows[1].VictoryRateText, Is.EqualTo("100%"));
    }

    /// <summary>locale 变化必须重绘文本，同时保持全部历史统计数值不变。</summary>
    [Test]
    public void LocaleChange_RerendersLocalizedTextWithoutChangingStatistics()
    {
        var repository = new InMemoryRunHistoryRepository();
        repository.Record(CreateSummary(
            RunOutcomeKind.Victory,
            "77777777-aaaa-bbbb-cccc-777777777777",
            heroTemplateId: 1002,
            completedMinute: 7));
        using var harness = new PresenterHarness(repository);
        harness.Presenter.Initialize();
        StatisticsViewModel english = harness.View.Models.Single();

        harness.SetLocaleAndRaise("zh-CN");

        Assert.That(harness.View.Models, Has.Count.EqualTo(2));
        StatisticsViewModel chinese = harness.View.Models[1];
        Assert.That(
            english.GetText(RunStatisticsTextSlot.Title),
            Is.EqualTo("en:app.statistics.title"));
        Assert.That(
            chinese.GetText(RunStatisticsTextSlot.Title),
            Is.EqualTo("zh-CN:app.statistics.title"));
        Assert.That(chinese.HeroRows.Single().HeroText,
            Is.EqualTo("zh-CN:app.statistics.hero:1002"));
        Assert.That(chinese.TotalRuns, Is.EqualTo(english.TotalRuns));
        Assert.That(chinese.VictoryCount, Is.EqualTo(english.VictoryCount));
        Assert.That(chinese.VictoryRate, Is.EqualTo(english.VictoryRate));
    }

    /// <summary>同一场景首次记录新终局后必须消费 owner 的完整快照并替换统计投影。</summary>
    [Test]
    public void RecordSuccess_AfterInitializeRendersNewStatistics()
    {
        using var harness = new PresenterHarness(new InMemoryRunHistoryRepository());
        harness.Presenter.Initialize();

        harness.RecordTerminal(CreateAbandonedTerminalState(
            "88888888-aaaa-bbbb-cccc-888888888888",
            heroTemplateId: 1002));

        Assert.That(harness.View.Models, Has.Count.EqualTo(2));
        StatisticsViewModel refreshed = harness.View.Models[1];
        Assert.That(refreshed.Status, Is.EqualTo(RunStatisticsViewStatus.Ready));
        Assert.That(refreshed.TotalRuns, Is.EqualTo(1));
        Assert.That(refreshed.AbandonedCount, Is.EqualTo(1));
        Assert.That(refreshed.HeroRows.Single().HeroTemplateId, Is.EqualTo(1002));
    }

    /// <summary>终局记录失败时 owner 不得发布变化事件并把零态伪刷新成成功。</summary>
    [Test]
    public void RecordFailure_DoesNotRerenderStatistics()
    {
        using var harness = new PresenterHarness(new FailingRecordRepository());
        harness.Presenter.Initialize();

        RunHistoryServiceRecordResult result = harness.RecordTerminal(
            CreateAbandonedTerminalState(
                "99999999-aaaa-bbbb-cccc-999999999999",
                heroTemplateId: 1001));

        Assert.That(result.Status, Is.EqualTo(RunHistoryServiceRecordStatus.Unavailable));
        Assert.That(harness.View.Models, Has.Count.EqualTo(1));
        Assert.That(harness.View.Models[0].TotalRuns, Is.Zero);
    }

    /// <summary>Presenter 释放后必须解除 history 订阅，后续成功记录不能触碰旧 View。</summary>
    [Test]
    public void Dispose_RecordSuccessDoesNotRenderAgain()
    {
        using var harness = new PresenterHarness(new InMemoryRunHistoryRepository());
        harness.Presenter.Initialize();
        harness.Presenter.Dispose();

        harness.RecordTerminal(CreateAbandonedTerminalState(
            "aaaaaaaa-aaaa-bbbb-cccc-aaaaaaaaaaaa",
            heroTemplateId: 1001));

        Assert.That(harness.View.Models, Has.Count.EqualTo(1));
    }

    /// <summary>坏单历史必须投影明确不可用状态，不能伪装成正常零历史。</summary>
    [Test]
    public void LoadFailure_RendersUnavailableWithoutZeroStatistics()
    {
        using var harness = new PresenterHarness(new FailingReadAllRepository());

        harness.Presenter.Initialize();

        StatisticsViewModel model = harness.View.Models.Single();
        Assert.That(model.Status, Is.EqualTo(RunStatisticsViewStatus.Unavailable));
        Assert.That(model.TotalRuns, Is.Null);
        Assert.That(model.VictoryCount, Is.Null);
        Assert.That(model.DefeatCount, Is.Null);
        Assert.That(model.AbandonedCount, Is.Null);
        Assert.That(model.VictoryRate, Is.Null);
        Assert.That(model.VictoryRateText, Is.Empty);
        Assert.That(model.HeroRows, Is.Empty);
        Assert.That(model.IsEmpty, Is.False);
        Assert.That(model.FailureText,
            Is.EqualTo("en:app.statistics.failure.load"));
        Assert.That(
            model.GetText(RunStatisticsTextSlot.Title),
            Is.EqualTo("en:app.statistics.title"));
    }

    /// <summary>建立一局满足 RunSummary 全部不变量的 Presenter 测试历史。</summary>
    private static RunSummary CreateSummary(
        RunOutcomeKind outcomeKind,
        string runId,
        int heroTemplateId,
        int completedMinute)
    {
        bool battleOutcome = outcomeKind == RunOutcomeKind.Victory ||
                             outcomeKind == RunOutcomeKind.Defeat;
        IEnumerable<RunSummaryPathNode> path = outcomeKind == RunOutcomeKind.Victory
            ? new[]
            {
                new RunSummaryPathNode("L00-S00", MapNodeKind.Start, contentId: 0),
                new RunSummaryPathNode("L01-S00", MapNodeKind.Boss, contentId: 7001),
            }
            : new[]
            {
                new RunSummaryPathNode("L00-S00", MapNodeKind.Start, contentId: 0),
            };
        return new RunSummary(
            new RunId(Guid.Parse(runId)),
            FrozenUtc.AddMinutes(completedMinute),
            heroTemplateId,
            outcomeKind,
            battleOutcome ? "L01-S00" : null,
            battleOutcome ? (int?)1 : null,
            randomRootSeed: checked((uint)(424200 + completedMinute)),
            finalHealth: outcomeKind == RunOutcomeKind.Defeat ? 0 : 37,
            maxHealth: 80,
            battleAttemptCount: battleOutcome ? 1 : 0,
            path: path,
            deck: new[] { new RunSummaryCard(1, templateId: 3002, upgradeLevel: 0) },
            holdings: new RunSummaryHoldings(
                gold: 100,
                relics: Array.Empty<RunSummaryRelic>(),
                potions: Array.Empty<RunSummaryPotion>()));
    }

    /// <summary>建立一份只停在 Start、可由 history service 正常冻结的 Abandoned 终局。</summary>
    private static RunState CreateAbandonedTerminalState(
        string runId,
        int heroTemplateId)
    {
        MapNodeId start = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId combat = MapNodeId.FromPosition(layer: 1, slot: 0);
        var map = new MapDefinition(
            profileId: "tinyspire.test.statistics-refresh.v1",
            generatorVersion: 1,
            mapSeed: 616161u,
            nodes: new[]
            {
                new MapNode(start, layer: 0, slot: 0, MapNodeKind.Start, contentId: 0),
                new MapNode(combat, layer: 1, slot: 0, MapNodeKind.Combat, contentId: 5001),
            },
            edges: new[] { new MapEdge(start, combat) });
        var options = new RunRestoreOptions(
            new RunId(Guid.Parse(runId)),
            heroTemplateId,
            currentHealth: 70,
            maxHealth: 70,
            runDeck: RunDeck.CreateInitial(new[] { 3002 }),
            randomRootSeed: map.MapSeed,
            map: map,
            pathNodeIds: new[] { start },
            progressPhase: RunProgressPhase.Terminal,
            committedNodeId: null,
            outcomeKind: RunOutcomeKind.Abandoned,
            holdings: RunHoldings.Empty(initialGold: 100));
        using var store = new RunStateStore();
        return store.RestoreRun(options);
    }

    /// <summary>用真实 history service 和可观察 View 组装 Presenter public seam。</summary>
    private sealed class PresenterHarness : IDisposable
    {
        /// <summary>记录 Presenter 全部不可变投影的 View。</summary>
        public RecordingView View { get; }

        /// <summary>测试直接驱动的只读 Statistics Presenter。</summary>
        public RunStatisticsPresenter Presenter { get; }

        /// <summary>测试 localizer 当前使用的语言前缀。</summary>
        public string LocaleCode { get; private set; }

        private readonly RunHistoryService _history;
        private readonly RecordingLocaleChanges _localeChanges;

        /// <summary>从指定历史库建立英语环境的 Presenter。</summary>
        public PresenterHarness(IRunHistoryRepository repository)
        {
            View = new RecordingView();
            _history = new RunHistoryService(repository);
            _localeChanges = new RecordingLocaleChanges();
            LocaleCode = "en";
            Presenter = new RunStatisticsPresenter(
                View,
                _history,
                (key, arguments) => key == RunStatisticsLocalizationKeys.Hero
                    ? $"{LocaleCode}:{key}:{arguments["hero_template_id"]}"
                    : LocaleCode + ":" + key,
                _localeChanges.Subscribe);
        }

        /// <summary>切换测试语言并发布一次 locale 变化。</summary>
        public void SetLocaleAndRaise(string localeCode)
        {
            LocaleCode = localeCode ?? throw new ArgumentNullException(nameof(localeCode));
            _localeChanges.Raise();
        }

        /// <summary>经真实 history owner 处理一份终局并返回其类型化结果。</summary>
        public RunHistoryServiceRecordResult RecordTerminal(RunState terminal)
        {
            return _history.EnsureRecorded(terminal);
        }

        /// <summary>释放 Presenter 与真实 history service。</summary>
        public void Dispose()
        {
            Presenter.Dispose();
            _history.Dispose();
        }
    }

    /// <summary>只通过 IRunStatisticsView 保存完整渲染模型。</summary>
    private sealed class RecordingView : IRunStatisticsView
    {
        /// <summary>按时间顺序保存每次完整替换模型。</summary>
        public List<StatisticsViewModel> Models { get; } =
            new List<StatisticsViewModel>();

        /// <summary>记录 Presenter 交付的不可变模型。</summary>
        public void Render(StatisticsViewModel model)
        {
            Models.Add(model);
        }
    }

    /// <summary>以明确订阅 seam 模拟 locale 变化。</summary>
    private sealed class RecordingLocaleChanges
    {
        private Action _handler;

        /// <summary>保存 Presenter 回调并返回可释放订阅。</summary>
        public IDisposable Subscribe(Action handler)
        {
            _handler += handler ?? throw new ArgumentNullException(nameof(handler));
            return new DelegateDisposable(() => _handler -= handler);
        }

        /// <summary>向当前订阅者发布一次 locale 变化。</summary>
        public void Raise()
        {
            _handler?.Invoke();
        }
    }

    /// <summary>模拟一个坏历史导致的类型化完整目录读取失败。</summary>
    private sealed class FailingReadAllRepository : IRunHistoryRepository
    {
        /// <summary>单局读取不参与本 Presenter 测试。</summary>
        public RunHistoryLoadResult Load(RunId runId)
        {
            return RunHistoryLoadResult.NotFound();
        }

        /// <summary>Presenter 为只读 seam，不应调用记录入口。</summary>
        public RunHistoryRecordResult Record(RunSummary summary)
        {
            throw new InvalidOperationException("Statistics presenter cannot record history.");
        }

        /// <summary>返回坏单历史对应的类型化完整目录失败。</summary>
        public RunHistoryReadAllResult ReadAll()
        {
            return RunHistoryReadAllResult.Failed(
                RunHistoryReadAllStatus.InvalidData,
                "corrupt Run history");
        }
    }

    /// <summary>允许初始统计读取但让终局物理记录失败的 repository 边界。</summary>
    private sealed class FailingRecordRepository : IRunHistoryRepository
    {
        /// <summary>新终局在记录前尚不存在。</summary>
        public RunHistoryLoadResult Load(RunId runId)
        {
            return RunHistoryLoadResult.NotFound();
        }

        /// <summary>模拟一次不创建历史的 I/O 失败。</summary>
        public RunHistoryRecordResult Record(RunSummary summary)
        {
            return RunHistoryRecordResult.Failed(
                RunHistoryRecordStatus.IoFailure,
                summary.RunId,
                "scripted record failure");
        }

        /// <summary>Presenter 初始化仍能读取正常空历史。</summary>
        public RunHistoryReadAllResult ReadAll()
        {
            return RunHistoryReadAllResult.Succeeded(Array.Empty<RunSummary>());
        }
    }

    /// <summary>以一次性回调释放测试订阅。</summary>
    private sealed class DelegateDisposable : IDisposable
    {
        private Action _dispose;

        /// <summary>冻结本订阅的释放回调。</summary>
        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        /// <summary>只执行一次释放回调。</summary>
        public void Dispose()
        {
            Action dispose = _dispose;
            _dispose = null;
            dispose?.Invoke();
        }
    }
}
