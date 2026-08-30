using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TinySpire.Presentation.Audio;
using TinySpire.Profile.Presentation;
using TinySpire.Run;
using TinySpire.Run.Map;
using TinySpire.Settings.Presentation;
using TinySpire.UI.Run;

public sealed class UiAudioFeedbackG8Tests
{
    /// <summary>RunEntry 的页面导航、选择与返回动作统一播放轻量 Click。</summary>
    [TestCase(RunEntryActionKind.StartGame)]
    [TestCase(RunEntryActionKind.OpenSettings)]
    [TestCase(RunEntryActionKind.OpenCompendium)]
    [TestCase(RunEntryActionKind.OpenStatistics)]
    [TestCase(RunEntryActionKind.Back)]
    [TestCase(RunEntryActionKind.SelectHero)]
    [TestCase(RunEntryActionKind.LeaveTerminalRun)]
    [TestCase(RunEntryActionKind.RequestAbandon)]
    [TestCase(RunEntryActionKind.RequestExitAfterSaveFailure)]
    public void RunEntryNavigationAndBack_PlayClick(RunEntryActionKind actionKind)
    {
        var runView = new RecordingRunEntryView();
        var settingsView = new RecordingAppSettingsView();
        var player = new RecordingUiAudioPlayer();
        using var presenter = new RunEntryUiAudioPresenter(runView, settingsView, player);
        presenter.Initialize();

        runView.Emit(CreateRunEntryAction(actionKind));

        Assert.That(player.Cues, Is.EqualTo(new[] { UiAudioCue.Click }));
    }

    /// <summary>RunEntry 的继续、确认与领域结算动作统一播放强调 Confirm。</summary>
    [TestCase(RunEntryActionKind.ConfirmHero)]
    [TestCase(RunEntryActionKind.EnterMapNode)]
    [TestCase(RunEntryActionKind.ContinueGame)]
    [TestCase(RunEntryActionKind.ConfirmAbandon)]
    [TestCase(RunEntryActionKind.RetrySave)]
    [TestCase(RunEntryActionKind.ConfirmRollback)]
    [TestCase(RunEntryActionKind.SelectCardReward)]
    [TestCase(RunEntryActionKind.SkipCardReward)]
    [TestCase(RunEntryActionKind.HealAtRest)]
    [TestCase(RunEntryActionKind.UpgradeCardAtRest)]
    [TestCase(RunEntryActionKind.ClaimChest)]
    [TestCase(RunEntryActionKind.SkipChest)]
    [TestCase(RunEntryActionKind.PurchaseShopStock)]
    [TestCase(RunEntryActionKind.LeaveShop)]
    [TestCase(RunEntryActionKind.ChooseEvent)]
    public void RunEntryConfirmationAndSettlement_PlayConfirm(RunEntryActionKind actionKind)
    {
        var runView = new RecordingRunEntryView();
        var settingsView = new RecordingAppSettingsView();
        var player = new RecordingUiAudioPlayer();
        using var presenter = new RunEntryUiAudioPresenter(runView, settingsView, player);
        presenter.Initialize();

        runView.Emit(CreateRunEntryAction(actionKind));

        Assert.That(player.Cues, Is.EqualTo(new[] { UiAudioCue.Confirm }));
    }

    /// <summary>设置页全部离散调整动作都播放 Click。</summary>
    [TestCase(AppSettingsActionKind.CycleLocale)]
    [TestCase(AppSettingsActionKind.DecreaseMasterVolume)]
    [TestCase(AppSettingsActionKind.IncreaseMasterVolume)]
    [TestCase(AppSettingsActionKind.ToggleDisplayMode)]
    [TestCase(AppSettingsActionKind.PreviousResolution)]
    [TestCase(AppSettingsActionKind.NextResolution)]
    [TestCase(AppSettingsActionKind.CycleTextScale)]
    [TestCase(AppSettingsActionKind.ToggleHighContrast)]
    [TestCase(AppSettingsActionKind.ToggleReducedMotion)]
    public void AppSettingsAdjustment_PlayClick(AppSettingsActionKind actionKind)
    {
        var runView = new RecordingRunEntryView();
        var settingsView = new RecordingAppSettingsView();
        var player = new RecordingUiAudioPlayer();
        using var presenter = new RunEntryUiAudioPresenter(runView, settingsView, player);
        presenter.Initialize();

        settingsView.Emit(new AppSettingsAction(actionKind));

        Assert.That(player.Cues, Is.EqualTo(new[] { UiAudioCue.Click }));
    }

    /// <summary>教程确认播放 Confirm，跳过与重置播放 Click。</summary>
    [Test]
    public void TutorialActions_PlayConfirmThenClicks()
    {
        var view = new RecordingTutorialGuideView();
        var player = new RecordingUiAudioPlayer();
        using var presenter = new TutorialUiAudioPresenter(view, player);
        presenter.Initialize();

        view.EmitConfirm();
        view.EmitSkip();
        view.EmitReset();

        Assert.That(
            player.Cues,
            Is.EqualTo(new[] { UiAudioCue.Confirm, UiAudioCue.Click, UiAudioCue.Click }));
    }

    /// <summary>RunState 从非终局成功进入 Terminal 时恰好播放一次 Confirm。</summary>
    [Test]
    public void RunOutcome_NonTerminalToTerminal_PlaysConfirm()
    {
        using var store = new RunStateStore();
        CreateMapReadyRun(store, randomRootSeed: 6101u);
        var saves = new ScriptedRunSaveStore(failCommit: false);
        RunFlowService flow = CreateFlow(store, saves);
        var player = new RecordingUiAudioPlayer();
        using var presenter = new RunOutcomeUiAudioPresenter(store, flow, player);
        presenter.Initialize();

        RunSaveCommitResult result = flow.AbandonActiveRun();

        Assert.That(result.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.Terminal));
        Assert.That(player.Cues, Is.EqualTo(new[] { UiAudioCue.Confirm }));
    }

    /// <summary>RunFlow 首次进入 CommitFailed 时播放 Error，且未发布 Terminal Confirm。</summary>
    [Test]
    public void RunOutcome_PersistenceFirstEntersCommitFailed_PlaysError()
    {
        using var store = new RunStateStore();
        CreateMapReadyRun(store, randomRootSeed: 6201u);
        var saves = new ScriptedRunSaveStore(failCommit: true);
        RunFlowService flow = CreateFlow(store, saves);
        var player = new RecordingUiAudioPlayer();
        using var presenter = new RunOutcomeUiAudioPresenter(store, flow, player);
        presenter.Initialize();

        RunSaveCommitResult result = flow.AbandonActiveRun();

        Assert.That(result.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(flow.Persistence.Status, Is.EqualTo(RunPersistenceStatus.CommitFailed));
        Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.MapReady));
        Assert.That(player.Cues, Is.EqualTo(new[] { UiAudioCue.Error }));
    }

    /// <summary>用真实 RunFlow 与可替换系统边界创建全局 outcome 音频夹具。</summary>
    private static RunFlowService CreateFlow(
        RunStateStore store,
        IRunSaveStore saves)
    {
        return new RunFlowService(
            store,
            () => null,
            new RecordingSceneFlow(),
            new UnusedRunEntropySource(),
            saves);
    }

    /// <summary>创建一份无需配置表即可主动放弃的合法 MapReady Run。</summary>
    private static RunState CreateMapReadyRun(
        RunStateStore store,
        uint randomRootSeed)
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.Current,
            RunRandomDomains.DeriveMapSeed(randomRootSeed));
        return store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.NewGuid()),
            heroTemplateId: 1001,
            initialHealth: 80,
            maxHealth: 80,
            RunDeck.CreateInitial(new[] { 3002, 3003 }),
            randomRootSeed,
            map));
    }

    /// <summary>为带身份的 RunEntry 动作创建一项合法、与音频分类无关的输入。</summary>
    private static RunEntryAction CreateRunEntryAction(RunEntryActionKind kind)
    {
        var runId = new RunId(Guid.Parse("89abcdef-0123-4567-89ab-cdef01234567"));
        var nodeId = new MapNodeId("L00-S00");
        var battleId = new RunBattleId(runId, 1, nodeId);
        var rewardId = new RunCardRewardId(battleId);
        var visitId = new RunNodeVisitId(runId, nodeId);
        switch (kind)
        {
            case RunEntryActionKind.SelectHero:
                return new RunEntryAction(kind, heroTemplateId: 1001);
            case RunEntryActionKind.EnterMapNode:
                return new RunEntryAction(kind, mapNodeId: nodeId);
            case RunEntryActionKind.SelectCardReward:
                return new RunEntryAction(
                    kind,
                    cardRewardId: rewardId,
                    cardTemplateId: 3002);
            case RunEntryActionKind.SkipCardReward:
                return new RunEntryAction(kind, cardRewardId: rewardId);
            case RunEntryActionKind.HealAtRest:
            case RunEntryActionKind.ClaimChest:
            case RunEntryActionKind.SkipChest:
            case RunEntryActionKind.LeaveShop:
                return new RunEntryAction(kind, nodeVisitId: visitId);
            case RunEntryActionKind.UpgradeCardAtRest:
                return new RunEntryAction(
                    kind,
                    nodeVisitId: visitId,
                    cardInstanceId: new RunCardInstanceId(1));
            case RunEntryActionKind.PurchaseShopStock:
                return new RunEntryAction(
                    kind,
                    nodeVisitId: visitId,
                    shopStockEntryId: 1);
            case RunEntryActionKind.ChooseEvent:
                return new RunEntryAction(
                    kind,
                    nodeVisitId: visitId,
                    eventChoice: RunEventChoiceKind.GainGold);
            default:
                return new RunEntryAction(kind);
        }
    }

    /// <summary>记录 Presenter 请求播放的稳定 cue，不创建真实 AudioClip。</summary>
    private sealed class RecordingUiAudioPlayer : IUiAudioPlayer
    {
        /// <summary>按播放顺序保存全部 cue。</summary>
        public List<UiAudioCue> Cues { get; } = new List<UiAudioCue>();

        /// <summary>记录一次类型化 UI 音频播放请求。</summary>
        public void Play(UiAudioCue cue)
        {
            Cues.Add(cue);
        }
    }

    /// <summary>只公开 RunEntry 动作事件的纯测试 View。</summary>
    private sealed class RecordingRunEntryView : IRunEntryView
    {
        /// <summary>Presenter 订阅的唯一入口动作事件。</summary>
        public event Action<RunEntryAction> ActionRequested;

        /// <summary>音频反馈测试不消费页面投影。</summary>
        public void Render(RunEntryViewModel model)
        {
        }

        /// <summary>模拟 RunEntry 控件发布合法动作。</summary>
        public void Emit(RunEntryAction action)
        {
            ActionRequested?.Invoke(action);
        }
    }

    /// <summary>只公开设置动作事件的纯测试 View。</summary>
    private sealed class RecordingAppSettingsView : IAppSettingsView
    {
        /// <summary>Presenter 订阅的唯一设置动作事件。</summary>
        public event Action<AppSettingsAction> ActionRequested;

        /// <summary>音频反馈测试不消费设置投影。</summary>
        public void Render(AppSettingsViewModel model)
        {
        }

        /// <summary>模拟设置控件发布合法动作。</summary>
        public void Emit(AppSettingsAction action)
        {
            ActionRequested?.Invoke(action);
        }
    }

    /// <summary>只公开教程三类动作事件的纯测试 View。</summary>
    private sealed class RecordingTutorialGuideView : ITutorialGuideView
    {
        /// <summary>玩家确认当前教程提示的事件。</summary>
        public event Action ConfirmRequested;

        /// <summary>玩家跳过余下教程的事件。</summary>
        public event Action SkipRequested;

        /// <summary>玩家重置教程的事件。</summary>
        public event Action ResetRequested;

        /// <summary>音频反馈测试不消费教程投影。</summary>
        public void Render(TutorialGuideViewModel model)
        {
        }

        /// <summary>音效映射测试不观察教程可访问性投影。</summary>
        public void ApplyAccessibility(TutorialGuideAccessibilityViewModel model)
        {
        }

        /// <summary>模拟确认当前教程提示。</summary>
        public void EmitConfirm()
        {
            ConfirmRequested?.Invoke();
        }

        /// <summary>模拟跳过余下教程。</summary>
        public void EmitSkip()
        {
            SkipRequested?.Invoke();
        }

        /// <summary>模拟重置教程。</summary>
        public void EmitReset()
        {
            ResetRequested?.Invoke();
        }
    }

    /// <summary>记录但不执行真实 Addressables 场景切换。</summary>
    private sealed class RecordingSceneFlow : ISceneFlowService
    {
        /// <summary>同步完成测试期场景请求。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            return UniTask.CompletedTask;
        }
    }

    /// <summary>当前 outcome 测试不创建新 Run，意外读取时立即失败。</summary>
    private sealed class UnusedRunEntropySource : IRunEntropySource
    {
        /// <summary>拒绝测试之外的意外新 Run 随机输入请求。</summary>
        public RunEntropy Next()
        {
            throw new InvalidOperationException("Outcome audio test must not request Run entropy.");
        }
    }

    /// <summary>按脚本返回成功或 IO 失败的单槽存档系统边界。</summary>
    private sealed class ScriptedRunSaveStore : IRunSaveStore
    {
        private readonly bool _failCommit;
        private RunSaveDocument _document;

        /// <summary>冻结本夹具是否应让提交失败。</summary>
        public ScriptedRunSaveStore(bool failCommit)
        {
            _failCommit = failCommit;
        }

        /// <summary>返回最近成功文档或空槽，供提交失败回退探测。</summary>
        public RunSaveLoadResult Load()
        {
            return _document == null
                ? RunSaveLoadResult.NotFound()
                : RunSaveLoadResult.Succeeded(_document);
        }

        /// <summary>按脚本提交；失败时不替换最近成功文档。</summary>
        public RunSaveCommitResult Commit(RunSaveDocument document)
        {
            if (_failCommit)
            {
                return RunSaveCommitResult.Failed(
                    RunSaveCommitStatus.IoFailure,
                    "Injected UI audio outcome commit failure.");
            }

            _document = document ?? throw new ArgumentNullException(nameof(document));
            return RunSaveCommitResult.Succeeded();
        }

        /// <summary>清空最近成功文档并返回幂等成功。</summary>
        public RunSaveDeleteResult Delete()
        {
            _document = null;
            return RunSaveDeleteResult.Succeeded();
        }
    }
}
