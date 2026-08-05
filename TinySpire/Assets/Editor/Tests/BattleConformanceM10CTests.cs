using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using DG.Tweening;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;

public sealed class BattleConformanceM10CTests
{
    /// <summary>确认默认 BattleScene 装配在 30、60、120 FPS 表现推进下产生同一份权威回放轨迹。</summary>
    [Test]
    public void DefaultBattleReplay_30_60_120Fps_ProducesTheSameAuthoritativeTrace()
    {
        M10BattleReplayTrace thirtyFps = M10BattleReplayHarness.Replay(frameRate: 30);
        M10BattleReplayTrace sixtyFps = M10BattleReplayHarness.Replay(frameRate: 60);
        M10BattleReplayTrace oneTwentyFps = M10BattleReplayHarness.Replay(frameRate: 120);

        Assert.That(thirtyFps, Is.EqualTo(sixtyFps));
        Assert.That(thirtyFps, Is.EqualTo(oneTwentyFps));
    }

    /// <summary>确认既有表现 runner 的加速和立即完成只改变时间安排，不改变权威回放轨迹。</summary>
    [Test]
    public void DefaultBattleReplay_AcceleratedAndImmediatePresentation_MatchNaturalTrace()
    {
        M10BattleReplayTrace natural = M10BattleReplayHarness.Replay(frameRate: 60);
        M10BattleReplayTrace accelerated = M10BattleReplayHarness.ReplayAccelerated(
            frameRate: 60,
            speedMultiplier: 8f);
        M10BattleReplayTrace immediate = M10BattleReplayHarness.ReplayImmediately();

        Assert.That(accelerated, Is.EqualTo(natural));
        Assert.That(immediate, Is.EqualTo(natural));
    }

    /// <summary>确认取消旧场景表现会清理 Tween、阻止迟到推进，并让同配置重启建立独立的新战斗。</summary>
    [Test]
    public void CancellationAndRestart_DisposeOldPresentationWithoutLateAdvanceOrLeakedTimeline()
    {
        M10BattleLifecycleEvidence evidence = M10BattleReplayHarness.VerifyCancellationAndRestart();

        Assert.That(evidence.QueueStayedAtCancelledPresentationBarrier, Is.True);
        Assert.That(evidence.NoLateLifecyclePublication, Is.True);
        Assert.That(evidence.TweenWasReleasedOnCancellation, Is.True);
        Assert.That(evidence.RestartBeginsAtAuthoritySequenceOne, Is.True);
        Assert.That(evidence.RestartReplaysTheSameDefaultTrace, Is.True);
    }
}

/// <summary>只保存测试执行期从公开只读事实采集的稳定回放签名，不参与任何战斗状态写入。</summary>
internal sealed class M10BattleReplayTrace : IEquatable<M10BattleReplayTrace>
{
    /// <summary>按真实表现层接收顺序冻结的命令和结算签名。</summary>
    public IReadOnlyList<string> CommandSignatures { get; }

    /// <summary>所有命令完成后的 Queue、Turn、参与者、意图与卡区只读终态签名。</summary>
    public string FinalFactsSignature { get; }

    /// <summary>冻结回放中的命令签名和终态事实，避免测试比较时重新读取可变运行时对象。</summary>
    public M10BattleReplayTrace(
        IEnumerable<string> commandSignatures,
        string finalFactsSignature)
    {
        if (commandSignatures == null)
            throw new ArgumentNullException(nameof(commandSignatures));
        if (finalFactsSignature == null)
            throw new ArgumentNullException(nameof(finalFactsSignature));

        CommandSignatures = commandSignatures.ToArray();
        FinalFactsSignature = finalFactsSignature;
    }

    /// <summary>比较两次回放的命令结算顺序和权威终态是否完全一致。</summary>
    public bool Equals(M10BattleReplayTrace other)
    {
        return other != null &&
               CommandSignatures.SequenceEqual(other.CommandSignatures) &&
               string.Equals(FinalFactsSignature, other.FinalFactsSignature, StringComparison.Ordinal);
    }

    /// <summary>让 NUnit 的对象比较继续委托给强类型回放相等性。</summary>
    public override bool Equals(object obj)
    {
        return obj is M10BattleReplayTrace other && Equals(other);
    }

    /// <summary>根据冻结的回放文本生成稳定哈希，供相等对象的集合场景使用。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (string signature in CommandSignatures)
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(signature);
            return hash * 31 + StringComparer.Ordinal.GetHashCode(FinalFactsSignature);
        }
    }
}

/// <summary>只保存取消和重启测试从既有运行时对象读取到的清理结论，不参与战斗事实。</summary>
internal sealed class M10BattleLifecycleEvidence
{
    /// <summary>取消后旧 Queue 仍保持原有表现屏障，未被迟到 completion 推进。</summary>
    public bool QueueStayedAtCancelledPresentationBarrier { get; }

    /// <summary>取消后的 adapter 控制调用没有产生新的 Queue 生命周期事件。</summary>
    public bool NoLateLifecyclePublication { get; }

    /// <summary>取消后的 DOTween 活动数量回到本次表现开始前的基线。</summary>
    public bool TweenWasReleasedOnCancellation { get; }

    /// <summary>两次独立重启都从新的 authority sequence 1 开始。</summary>
    public bool RestartBeginsAtAuthoritySequenceOne { get; }

    /// <summary>同一默认配置重启后的完整回放与第一次一致。</summary>
    public bool RestartReplaysTheSameDefaultTrace { get; }

    /// <summary>冻结测试期清理结论，便于单个生命周期回归同时断言其彼此独立的事实。</summary>
    public M10BattleLifecycleEvidence(
        bool queueStayedAtCancelledPresentationBarrier,
        bool noLateLifecyclePublication,
        bool tweenWasReleasedOnCancellation,
        bool restartBeginsAtAuthoritySequenceOne,
        bool restartReplaysTheSameDefaultTrace)
    {
        QueueStayedAtCancelledPresentationBarrier = queueStayedAtCancelledPresentationBarrier;
        NoLateLifecyclePublication = noLateLifecyclePublication;
        TweenWasReleasedOnCancellation = tweenWasReleasedOnCancellation;
        RestartBeginsAtAuthoritySequenceOne = restartBeginsAtAuthoritySequenceOne;
        RestartReplaysTheSameDefaultTrace = restartReplaysTheSameDefaultTrace;
    }
}

/// <summary>只通过 Submit、既有表现完成和公开只读事实重放默认 BattleScene 的 Editor 测试夹具。</summary>
internal static class M10BattleReplayHarness
{
    private const int DefaultHeroTemplateId = 1001;
    private const int DefaultEncounterTemplateId = 5001;
    private const int DefaultBattleSeed = 5;

    /// <summary>按指定表现帧率推进默认战斗到第二轮玩家行动，并冻结整个权威回放。</summary>
    internal static M10BattleReplayTrace Replay(int frameRate)
    {
        return ReplayCore(frameRate, speedMultiplier: 1f, completeImmediately: false);
    }

    /// <summary>以既有 runner 的时间倍速推进默认战斗，供验证时间缩放不改变权威轨迹。</summary>
    internal static M10BattleReplayTrace ReplayAccelerated(int frameRate, float speedMultiplier)
    {
        if (speedMultiplier <= 1f)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));

        return ReplayCore(frameRate, speedMultiplier, completeImmediately: false);
    }

    /// <summary>以既有 runner 的立即完成入口推进默认战斗，供验证同帧收口不改写战斗事实。</summary>
    internal static M10BattleReplayTrace ReplayImmediately()
    {
        return ReplayCore(frameRate: 60, speedMultiplier: 1f, completeImmediately: true);
    }

    /// <summary>验证取消旧表现不会留下迟到 completion 或 Tween，并验证同配置重启创建独立回放。</summary>
    internal static M10BattleLifecycleEvidence VerifyCancellationAndRestart()
    {
        Tables tables = LoadDefaultTables(out int energyPerRound, out int initialHandCount);
        using var session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(
                DefaultHeroTemplateId,
                DefaultEncounterTemplateId,
                DefaultBattleSeed));
        using var coordinator = new BattleCommandSubmissionCoordinator();
        using var adapter = new BattleCommandPresentationAdapter(
            cueDurationSeconds: 0.05f,
            unscaledDeltaTimeProvider: () => 1f / 60f);
        using var lifecycleRecorder = new M10LifecycleRecorder(coordinator);
        var presentation = new M10TracingPresentation(adapter);
        IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones =
            CreatePlayerCardZones(session);
        using var queue = BattleCommandQueueTestFactory.Create(
            session.Combatants,
            presentation,
            playerCardZones,
            enemyCombatantIdsInEncounterOrder: session.EnemyCombatantIdsInEncounterOrder,
            energyPerRound: energyPerRound,
            initialHandCount: initialHandCount,
            enemyIntents: session.EnemyIntents,
            tables: tables,
            battleSeed: DefaultBattleSeed,
            coordinator: coordinator);

        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        BattleCommandSubmissionResult startSubmission = Submit(
            queue,
            coordinator,
            new StartBattleCommand());
        if (!startSubmission.Accepted)
            throw new InvalidOperationException("Default BattleScene rejected StartBattleCommand.");

        BattleCommandQueueData queueBeforeCancellation = queue.Queue.CurrentValue;
        int lifecycleEventCountBeforeCancellation = lifecycleRecorder.EventCount;
        if (!queueBeforeCancellation.IsWaitingForPresentation)
            throw new InvalidOperationException("Default BattleScene did not create the expected presentation barrier.");

        adapter.Dispose();
        adapter.Tick();
        adapter.CompleteImmediately();

        BattleCommandQueueData queueAfterCancellation = queue.Queue.CurrentValue;
        bool queueStayedAtCancelledPresentationBarrier =
            queueAfterCancellation.CurrentAuthoritySequence ==
            queueBeforeCancellation.CurrentAuthoritySequence &&
            queueAfterCancellation.CurrentCommandType ==
            queueBeforeCancellation.CurrentCommandType &&
            queueAfterCancellation.PendingCount == queueBeforeCancellation.PendingCount &&
            queueAfterCancellation.IsWaitingForPresentation ==
            queueBeforeCancellation.IsWaitingForPresentation;
        bool noLateLifecyclePublication = lifecycleRecorder.EventCount ==
                                         lifecycleEventCountBeforeCancellation;
        bool tweenWasReleasedOnCancellation = DOTween.TotalActiveTweens() == activeTweenCountBefore;

        M10BattleReplayTrace firstRestart = Replay(frameRate: 60);
        M10BattleReplayTrace secondRestart = Replay(frameRate: 60);
        bool restartBeginsAtAuthoritySequenceOne =
            firstRestart.CommandSignatures.Count > 0 &&
            secondRestart.CommandSignatures.Count > 0 &&
            firstRestart.CommandSignatures[0].StartsWith("1:", StringComparison.Ordinal) &&
            secondRestart.CommandSignatures[0].StartsWith("1:", StringComparison.Ordinal);

        return new M10BattleLifecycleEvidence(
            queueStayedAtCancelledPresentationBarrier,
            noLateLifecyclePublication,
            tweenWasReleasedOnCancellation,
            restartBeginsAtAuthoritySequenceOne,
            firstRestart.Equals(secondRestart));
    }

    /// <summary>按指定帧率和已有表现推进策略重放默认战斗，不创建第二条调度或动画时间线。</summary>
    private static M10BattleReplayTrace ReplayCore(
        int frameRate,
        float speedMultiplier,
        bool completeImmediately)
    {
        if (frameRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameRate));

        Tables tables = LoadDefaultTables(out int energyPerRound, out int initialHandCount);
        using var session = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(
                DefaultHeroTemplateId,
                DefaultEncounterTemplateId,
                DefaultBattleSeed));
        using var coordinator = new BattleCommandSubmissionCoordinator();
        float frameDeltaTime = 1f / frameRate;
        using var adapter = new BattleCommandPresentationAdapter(
            cueDurationSeconds: 0.05f,
            unscaledDeltaTimeProvider: () => frameDeltaTime);
        adapter.SetPresentationSpeed(speedMultiplier);
        var presentation = new M10TracingPresentation(adapter);
        IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones =
            CreatePlayerCardZones(session);
        using var queue = BattleCommandQueueTestFactory.Create(
            session.Combatants,
            presentation,
            playerCardZones,
            enemyCombatantIdsInEncounterOrder: session.EnemyCombatantIdsInEncounterOrder,
            energyPerRound: energyPerRound,
            initialHandCount: initialHandCount,
            enemyIntents: session.EnemyIntents,
            tables: tables,
            battleSeed: DefaultBattleSeed,
            coordinator: coordinator);

        BattleCommandSubmissionResult startSubmission = Submit(
            queue,
            coordinator,
            new StartBattleCommand());
        if (!startSubmission.Accepted)
            throw new InvalidOperationException("Default BattleScene rejected StartBattleCommand.");
        DrainToPlayerAction(queue, adapter, expectedRound: 1, completeImmediately);

        PlayerCombatantData player = session.Combatants.All.Values
            .OfType<PlayerCombatantData>()
            .Single();
        BattleCommandSubmissionResult endSubmission = Submit(
            queue,
            coordinator,
            new EndPlayerActionCommand(player.Id));
        if (!endSubmission.Accepted)
            throw new InvalidOperationException("Default BattleScene rejected EndPlayerActionCommand.");
        DrainToPlayerAction(queue, adapter, expectedRound: 2, completeImmediately);

        return new M10BattleReplayTrace(
            presentation.CommandSignatures,
            CaptureFinalFacts(queue, session));
    }

    /// <summary>使用运行时必需表清单读取生成 GameData，并同步取得本局的能量和初始手牌配置。</summary>
    private static Tables LoadDefaultTables(out int energyPerRound, out int initialHandCount)
    {
        JObject gameConfig = LoadGeneratedObject("game-config");
        energyPerRound = gameConfig.Value<int>("energyPerRound");
        initialHandCount = gameConfig.Value<int>("initialHandCount");
        return new Tables(LoadGeneratedRows);
    }

    /// <summary>把当前生成 JSON 的对象索引转换为 Luban Tables 构造器所需的行数组。</summary>
    private static JArray LoadGeneratedRows(string tableName)
    {
        return new JArray(LoadGeneratedObject(tableName).Properties().Select(property => property.Value));
    }

    /// <summary>从项目内稳定 GameData 地址读取一个生成 JSON 对象，缺失时立刻暴露测试装配错误。</summary>
    private static JObject LoadGeneratedObject(string fileNameWithoutExtension)
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
            $"Assets/GameData/{fileNameWithoutExtension}.json");
        if (asset == null)
            throw new InvalidOperationException($"Generated GameData is missing: {fileNameWithoutExtension}.");

        return JObject.Parse(asset.text);
    }

    /// <summary>把单一默认玩家映射到同一 BattleSession 卡区，不创建任何卡区镜像。</summary>
    private static IReadOnlyDictionary<CombatantId, BattleCardZonesData> CreatePlayerCardZones(
        BattleSession session)
    {
        PlayerCombatantData player = session.Combatants.All.Values
            .OfType<PlayerCombatantData>()
            .Single();
        return new Dictionary<CombatantId, BattleCardZonesData>
        {
            [player.Id] = session.CardZones,
        };
    }

    /// <summary>按生产提交前的既有协调器登记要求调用唯一 Queue.Submit 写入入口。</summary>
    private static BattleCommandSubmissionResult Submit(
        BattleCommandQueue queue,
        BattleCommandSubmissionCoordinator coordinator,
        BattleCommand command)
    {
        coordinator.PreRegister(command);
        return queue.Submit(command);
    }

    /// <summary>仅以 adapter 的既有表现 Tick 释放 Queue 屏障，直到指定玩家回合成为空闲权威终态。</summary>
    private static void DrainToPlayerAction(
        BattleCommandQueue queue,
        BattleCommandPresentationAdapter adapter,
        int expectedRound,
        bool completeImmediately)
    {
        const int maximumFrameCount = 20000;
        for (int frame = 0; frame < maximumFrameCount; frame++)
        {
            BattleCommandQueueData queueFacts = queue.Queue.CurrentValue;
            BattleTurnData turnFacts = queue.Turn.CurrentValue;
            if (queueFacts.IsFaulted)
                throw new InvalidOperationException("Default BattleScene replay reached a Queue fault.");
            if (queueFacts.CurrentAuthoritySequence == null &&
                queueFacts.PendingCount == 0 &&
                !queueFacts.IsWaitingForPresentation &&
                turnFacts.Phase == BattleTurnPhase.PlayerAction &&
                turnFacts.RoundNumber == expectedRound)
            {
                return;
            }

            if (completeImmediately)
                adapter.CompleteImmediately();
            else
                adapter.Tick();
        }

        throw new TimeoutException($"Default BattleScene did not reach player round {expectedRound}.");
    }

    /// <summary>从 Queue、Turn、BattleSession、CardZones 和意图只读快照生成最终对比文本。</summary>
    private static string CaptureFinalFacts(BattleCommandQueue queue, BattleSession session)
    {
        BattleCommandQueueData queueFacts = queue.Queue.CurrentValue;
        BattleTurnData turnFacts = queue.Turn.CurrentValue;
        string players = string.Join(
            ",",
            turnFacts.Players
                .OrderBy(entry => entry.Key.Value)
                .Select(entry => $"{entry.Key.Value}:{entry.Value.Energy}:{entry.Value.HasEndedAction}"));
        string combatants = string.Join(
            ",",
            session.Combatants.All
                .OrderBy(entry => entry.Key.Value)
                .Select(entry =>
                    $"{entry.Key.Value}:{entry.Value.TemplateId}:{entry.Value.CurrentHealth}:{entry.Value.CurrentStrength}:{entry.Value.CurrentBlock}:{entry.Value.CurrentVulnerable}:{entry.Value.IsAlive}"));
        string intents = string.Join(
            ",",
            session.EnemyIntents.Layout.CurrentValue.BehaviorIdsByEnemy
                .OrderBy(entry => entry.Key.Value)
                .Select(entry => $"{entry.Key.Value}:{entry.Value}"));
        string cardZones = string.Join(
            ";",
            session.CardZones.DrawPile.Select(card => card.Value),
            session.CardZones.Hand.Select(card => card.Value),
            session.CardZones.DiscardPile.Select(card => card.Value),
            session.CardZones.ExhaustPile.Select(card => card.Value));

        return string.Join(
            "|",
            queueFacts.CurrentAuthoritySequence,
            queueFacts.CurrentCommandType,
            queueFacts.CurrentSubmitterId,
            queueFacts.PendingCount,
            queueFacts.IsWaitingForPresentation,
            queueFacts.IsFaulted,
            turnFacts.Phase,
            turnFacts.RoundNumber,
            turnFacts.CurrentActingEnemyId,
            players,
            combatants,
            intents,
            session.EnemyIntents.RandomState,
            session.CardZones.ShuffleRandomState,
            cardZones);
    }

    /// <summary>记录 Queue 交给真实表现层的冻结结果，并把原 completion 原样委托给 adapter。</summary>
    private sealed class M10TracingPresentation : IBattleCommandPresentation
    {
        private readonly BattleCommandPresentationAdapter _adapter;

        /// <summary>按 Queue 发布顺序冻结的命令签名，测试结束后仅用于断言。</summary>
        internal List<string> CommandSignatures { get; } = new List<string>();

        /// <summary>保存现有生产 adapter，避免夹具伪造表现完成或另建动画队列。</summary>
        internal M10TracingPresentation(BattleCommandPresentationAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>先提取不可变结果的比较签名，再交给同一 adapter 和其唯一 completion 路径。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            CommandSignatures.Add(CreateCommandSignature(result));
            _adapter.Present(result, onCompleted);
        }
    }

    /// <summary>订阅既有 coordinator 生命周期流，只计数取消前后是否发生迟到发布。</summary>
    private sealed class M10LifecycleRecorder : IDisposable
    {
        private readonly IDisposable _subscription;

        /// <summary>截至当前已从 Queue 收到的真实生命周期事件数。</summary>
        internal int EventCount { get; private set; }

        /// <summary>建立只读生命周期订阅，不创建新的 Queue 写入或 completion 通道。</summary>
        internal M10LifecycleRecorder(BattleCommandSubmissionCoordinator coordinator)
        {
            if (coordinator == null)
                throw new ArgumentNullException(nameof(coordinator));

            _subscription = coordinator.Lifecycle.Subscribe(_ => EventCount++);
        }

        /// <summary>释放测试订阅，避免夹具本身跨用例保留 coordinator 引用。</summary>
        public void Dispose()
        {
            _subscription.Dispose();
        }
    }

    /// <summary>把单条公开执行结果及其每个结算 Order 压缩为确定性比较文本。</summary>
    private static string CreateCommandSignature(BattleCommandExecutionResult result)
    {
        return $"{result.AuthoritySequence}:{result.CommandType}:{result.SubmitterId}:{result.FailureReason}[{string.Join(",", result.Settlements.Select(CreateSettlementSignature))}]";
    }

    /// <summary>保留每种当前结算记录的公共字段，避免只比较类型而遗漏卡区或数值顺序。</summary>
    private static string CreateSettlementSignature(BattleSettlementRecord settlement)
    {
        string detail = settlement switch
        {
            BattleDamageAppliedSettlement damage =>
                $":{damage.AttackValue}:{damage.BlockBefore}:{damage.BlockAfter}:{damage.HealthBefore}:{damage.HealthAfter}",
            BattleBlockGainedSettlement block =>
                $":{block.BlockBefore}:{block.BlockAfter}",
            BattleAttributeModifiedSettlement attribute =>
                $":{attribute.Attribute}:{attribute.ValueBefore}:{attribute.ValueAfter}",
            BattleStatusAppliedSettlement status =>
                $":{status.Status}:{status.ValueBefore}:{status.ValueAfter}",
            BattleCardMovedSettlement moved =>
                $":{moved.CardId.Value}:{moved.FromZone}:{moved.ToZone}",
            BattleCardsReshuffledSettlement reshuffled =>
                $":{string.Join("/", reshuffled.NewDrawPileOrder.Select(card => card.Value))}",
            BattleOperationSkippedSettlement skipped => $":{skipped.Reason}",
            BattleBlockClearedSettlement cleared =>
                $":{cleared.BlockBefore}:{cleared.BlockAfter}",
            BattleStatusReducedSettlement reduced =>
                $":{reduced.Status}:{reduced.ValueBefore}:{reduced.ValueAfter}",
            BattleEnergySpentSettlement spent =>
                $":{spent.EnergyBefore}:{spent.EnergyAfter}",
            BattleEnergyRefilledSettlement refilled =>
                $":{refilled.EnergyBefore}:{refilled.EnergyAfter}",
            BattleEnemyIntentAdvancedSettlement intent =>
                $":{intent.CompletedBehaviorId}:{intent.NextBehaviorId}",
            BattleEnemyActionSkippedSettlement enemySkipped => $":{enemySkipped.Reason}",
            BattlePhaseChangedSettlement phase =>
                $":{phase.PhaseBefore}:{phase.PhaseAfter}:{phase.RoundNumberBefore}:{phase.RoundNumberAfter}:{phase.CurrentActingEnemyIdBefore}:{phase.CurrentActingEnemyIdAfter}",
            _ => string.Empty,
        };
        return $"{settlement.Order}:{settlement.RecordType}:{settlement.EffectId}:{settlement.SourceId}:{settlement.TargetId}{detail}";
    }
}
