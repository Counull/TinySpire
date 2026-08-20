using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;

public sealed class BattleCommandQueueM8BTests
{
    /// <summary>每个用例结束后释放共享工厂代建的敌人意图响应式资源。</summary>
    [TearDown]
    public void TearDown()
    {
        BattleCommandQueueTestFactory.DisposeOwnedEnemyIntents();
    }

    /// <summary>验证结构性拒绝由 Queue 内部撤销注册，不分配序号或发布生命周期。</summary>
    [Test]
    public void Submit_BeforeBattleRejectsWithoutSequenceOrLifecycle()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(
            templateId: 101,
            maxHealth: 30,
            strength: 0);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            coordinator: coordinator);

        BattleCommandSubmissionResult result = queue.Submit(
            new EndPlayerActionCommand(player.Id));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.AuthoritySequence, Is.Null);
        Assert.That(
            result.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.BattleNotStarted));
        Assert.That(lifecycles, Is.Empty);
        Assert.That(presentation.Results, Is.Empty);
        Assert.That(presentation.PendingCompletionCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.NotStarted));
    }

    /// <summary>验证开始战斗使用同一句柄发布一次 Queued、一次 Completed，并只建立一个阶段表现屏障。</summary>
    [Test]
    public void StartBattle_PublishesSameHandleLifecycleWithOnePhaseSettlementAndBarrier()
    {
        using var combatants = new BattleCombatantsData();
        combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            coordinator: coordinator);
        var command = new StartBattleCommand();

        BattleCommandSubmissionResult result = queue.Submit(command);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.AuthoritySequence, Is.EqualTo(1));
        Assert.That(lifecycles, Has.Count.EqualTo(2));
        BattleCommandHandle handle = lifecycles[0].Handle;
        Assert.That(lifecycles[0].Command, Is.SameAs(command));
        Assert.That(lifecycles[1].Command, Is.SameAs(command));
        AssertLifecycle(lifecycles[0], handle, 1, BattleCommandLifecycleStage.Queued);
        AssertLifecycle(lifecycles[1], handle, 1, BattleCommandLifecycleStage.ExecutionCompleted);
        Assert.That(lifecycles[0].Settlements, Is.Empty);
        Assert.That(lifecycles[1].Settlements, Has.Count.EqualTo(2));
        Assert.That(lifecycles[1].Settlements[0], Is.TypeOf<BattleEnergyRefilledSettlement>());
        var phaseChanged = lifecycles[1].Settlements[1] as BattlePhaseChangedSettlement;
        Assert.That(phaseChanged, Is.Not.Null);
        Assert.That(phaseChanged.Order, Is.EqualTo(1));
        Assert.That(phaseChanged.PhaseBefore, Is.EqualTo(BattleTurnPhase.NotStarted));
        Assert.That(phaseChanged.PhaseAfter, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(phaseChanged.RoundNumberBefore, Is.Zero);
        Assert.That(phaseChanged.RoundNumberAfter, Is.EqualTo(1));
        Assert.That(phaseChanged.CurrentActingEnemyIdBefore, Is.Null);
        Assert.That(phaseChanged.CurrentActingEnemyIdAfter, Is.Null);
        Assert.That(presentation.Results, Has.Count.EqualTo(1));
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

        presentation.CompleteNext();

        Assert.That(presentation.PendingCompletionCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
    }

    /// <summary>验证普通执行失败发布空结算终态，并在同一次 drain 中零表现直通。</summary>
    [Test]
    public void OrdinaryExecutionFailure_HasEmptySettlementsAndNoPresentationBarrier()
    {
        using var combatants = new BattleCombatantsData();
        combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            coordinator: coordinator);
        SubmitAndCaptureHandle(queue, new StartBattleCommand(), out _);
        presentation.CompleteNext();
        lifecycles.Clear();
        int presentedCountBeforeFailure = presentation.Results.Count;

        BattleCommandSubmissionResult result = SubmitAndCaptureHandle(queue,
            new StartBattleCommand(),
            out BattleCommandHandle handle);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.AuthoritySequence, Is.EqualTo(2));
        Assert.That(lifecycles, Has.Count.EqualTo(2));
        AssertLifecycle(lifecycles[0], handle, 2, BattleCommandLifecycleStage.Queued);
        AssertLifecycle(lifecycles[1], handle, 2, BattleCommandLifecycleStage.ExecutionFailed);
        Assert.That(
            lifecycles[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.BattleAlreadyStarted));
        Assert.That(lifecycles[1].Settlements, Is.Empty);
        Assert.That(presentation.Results.Count, Is.EqualTo(presentedCountBeforeFailure));
        Assert.That(presentation.PendingCompletionCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
    }

    /// <summary>验证表现异常会冻结当前与待处理项，发布独立 fault，并稳定拒绝后续提交。</summary>
    [Test]
    public void PresentationException_FaultsQueueAndPreservesPendingDiagnostics()
    {
        using var combatants = new BattleCombatantsData();
        combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BThrowingBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            coordinator: coordinator);
        BattleCommandSubmissionResult nestedSubmission = default;
        BattleCommandHandle nestedHandle = null;
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycle =>
        {
            lifecycles.Add(lifecycle);
            if (lifecycle.Stage != BattleCommandLifecycleStage.Queued ||
                lifecycle.AuthoritySequence != 1)
            {
                return;
            }

            nestedSubmission = SubmitAndCaptureHandle(queue,
                new StartBattleCommand(),
                out nestedHandle);
        });

        BattleCommandSubmissionResult firstSubmission = SubmitAndCaptureHandle(queue,
            new StartBattleCommand(),
            out BattleCommandHandle firstHandle);

        Assert.That(firstSubmission.AuthoritySequence, Is.EqualTo(1));
        Assert.That(nestedSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(
            lifecycles.Select(item => (item.AuthoritySequence, item.Stage)),
            Is.EqualTo(new[]
            {
                (1L, BattleCommandLifecycleStage.Queued),
                (2L, BattleCommandLifecycleStage.Queued),
                (1L, BattleCommandLifecycleStage.Faulted),
            }));
        Assert.That(lifecycles[2].Handle, Is.SameAs(firstHandle));
        Assert.That(lifecycles[2].Settlements, Is.Empty);
        Assert.That(lifecycles[2].Fault, Is.Not.Null);
        Assert.That(
            lifecycles[2].Fault.Reason,
            Is.EqualTo(BattleCommandQueueFaultReason.UnexpectedException));
        Assert.That(lifecycles[2].Fault.MayHavePartialWrites, Is.True);
        Assert.That(presentation.PresentCount, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsFaulted, Is.True);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(coordinator.IsPending(firstHandle), Is.False);
        Assert.That(coordinator.IsPending(nestedHandle), Is.True);

        var rejectedCommand = new StartBattleCommand();
        BattleCommandSubmissionResult rejected = queue.Submit(rejectedCommand);

        Assert.That(rejected.Accepted, Is.False);
        Assert.That(rejected.AuthoritySequence, Is.Null);
        Assert.That(
            rejected.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.QueueFaulted));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(lifecycles, Has.Count.EqualTo(3));
    }

    /// <summary>验证 Queued 订阅回调内的新提交只会排队，不会重入执行并越过先提交命令。</summary>
    [Test]
    public void QueuedLifecycleCallback_SubmitDoesNotReenterDrain()
    {
        using var combatants = new BattleCombatantsData();
        combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            coordinator: coordinator);
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        bool submittedFromCallback = false;
        BattleCommandSubmissionResult callbackSubmission = default;
        BattleTurnPhase phaseBeforeNestedSubmit = default;
        BattleTurnPhase phaseAfterNestedSubmit = default;
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycle =>
        {
            lifecycles.Add(lifecycle);
            if (submittedFromCallback ||
                lifecycle.Stage != BattleCommandLifecycleStage.Queued ||
                lifecycle.AuthoritySequence != 1)
            {
                return;
            }

            submittedFromCallback = true;
            phaseBeforeNestedSubmit = queue.Turn.CurrentValue.Phase;
            callbackSubmission = SubmitAndCaptureHandle(queue,
                new StartBattleCommand(),
                out _);
            phaseAfterNestedSubmit = queue.Turn.CurrentValue.Phase;
        });

        BattleCommandSubmissionResult firstSubmission = SubmitAndCaptureHandle(queue,
            new StartBattleCommand(),
            out _);

        Assert.That(firstSubmission.AuthoritySequence, Is.EqualTo(1));
        Assert.That(callbackSubmission.Accepted, Is.True);
        Assert.That(callbackSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(phaseBeforeNestedSubmit, Is.EqualTo(BattleTurnPhase.NotStarted));
        Assert.That(phaseAfterNestedSubmit, Is.EqualTo(BattleTurnPhase.NotStarted));
        Assert.That(
            lifecycles.Select(item => (item.AuthoritySequence, item.Stage)),
            Is.EqualTo(new[]
            {
                (1L, BattleCommandLifecycleStage.Queued),
                (2L, BattleCommandLifecycleStage.Queued),
                (1L, BattleCommandLifecycleStage.ExecutionCompleted),
            }));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));

        presentation.CompleteNext();

        Assert.That(
            lifecycles.Select(item => (item.AuthoritySequence, item.Stage)),
            Is.EqualTo(new[]
            {
                (1L, BattleCommandLifecycleStage.Queued),
                (2L, BattleCommandLifecycleStage.Queued),
                (1L, BattleCommandLifecycleStage.ExecutionCompleted),
                (2L, BattleCommandLifecycleStage.ExecutionFailed),
            }));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(presentation.PendingCompletionCount, Is.Zero);
    }

    /// <summary>验证 Queue 快照回调内提交时，较小序号的 Queued 必须已经先发布。</summary>
    [Test]
    public void QueueSnapshotCallback_SubmitPreservesQueuedLifecycleOrder()
    {
        using var combatants = new BattleCombatantsData();
        combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            coordinator: coordinator);
        bool submittedFromSnapshot = false;
        using IDisposable queueSubscription = queue.Queue.Skip(1).Subscribe(snapshot =>
        {
            if (submittedFromSnapshot || snapshot.PendingCount == 0)
                return;

            submittedFromSnapshot = true;
            SubmitAndCaptureHandle(queue,
                new StartBattleCommand(),
                out _);
        });

        SubmitAndCaptureHandle(queue, new StartBattleCommand(), out _);

        Assert.That(
            lifecycles.Select(item => (item.AuthoritySequence, item.Stage)),
            Is.EqualTo(new[]
            {
                (1L, BattleCommandLifecycleStage.Queued),
                (2L, BattleCommandLifecycleStage.Queued),
                (1L, BattleCommandLifecycleStage.ExecutionCompleted),
            }));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
    }

    /// <summary>验证结束行动只由 Queue 生成敌人 continuation，外部伪造系统命令会撤销句柄且不占序号。</summary>
    [Test]
    public void EndPlayerAction_QueuesInternalEnemyContinuationAndRejectsExternalSystemCommand()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        using var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            coordinator: coordinator);
        SubmitAndCaptureHandle(queue, new StartBattleCommand(), out _);
        presentation.CompleteNext();
        lifecycles.Clear();

        var forgedSystemCommand = new CompleteEnemyActionCommand(enemy.Id);
        BattleCommandSubmissionResult forgedSubmission = SubmitAndCaptureHandle(queue,
            forgedSystemCommand,
            out BattleCommandHandle forgedHandle);

        Assert.That(forgedSubmission.Accepted, Is.False);
        Assert.That(forgedSubmission.AuthoritySequence, Is.Null);
        Assert.That(
            forgedSubmission.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.SystemCommandNotAuthorized));
        Assert.That(coordinator.IsPending(forgedHandle), Is.False);
        Assert.That(lifecycles, Is.Empty);

        BattleCommandSubmissionResult endSubmission = SubmitAndCaptureHandle(queue,
            new EndPlayerActionCommand(player.Id),
            out BattleCommandHandle endHandle);

        BattleCommandLifecycleEvent[] queued = lifecycles
            .Where(item => item.Stage == BattleCommandLifecycleStage.Queued)
            .ToArray();
        Assert.That(endSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(queued, Has.Length.EqualTo(2));
        AssertLifecycle(queued[0], endHandle, 2, BattleCommandLifecycleStage.Queued);
        Assert.That(queued[0].CommandType, Is.EqualTo(BattleCommandType.EndPlayerAction));
        Assert.That(queued[1].AuthoritySequence, Is.EqualTo(3));
        Assert.That(queued[1].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(queued[1].SubmitterId, Is.Null);
        Assert.That(queued[1].Handle, Is.Not.SameAs(endHandle));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));
        Assert.That(
            lifecycles.Count(item =>
                item.AuthoritySequence == 3 &&
                item.Stage == BattleCommandLifecycleStage.ExecutionCompleted),
            Is.EqualTo(1));

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
    }

    /// <summary>验证执行期既有接受项、内部 continuation 与表现回调新提交保持稳定 FIFO。</summary>
    [Test]
    public void ContinuationOrdering_PreservesExistingAcceptedBeforeContinuationAndPresentationSubmissionAfter()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        using var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            coordinator: coordinator);
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        SubmitAndCaptureHandle(queue, new StartBattleCommand(), out _);
        presentation.CompleteNext();
        lifecycles.Clear();

        bool acceptedDuringExecution = false;
        BattleCommandSubmissionResult executionCallbackSubmission = default;
        using IDisposable turnSubscription = queue.Turn.Subscribe(turn =>
        {
            if (acceptedDuringExecution || turn.Phase != BattleTurnPhase.EnemyAction)
                return;

            acceptedDuringExecution = true;
            executionCallbackSubmission = SubmitAndCaptureHandle(queue,
                new StartBattleCommand(),
                out _);
        });

        bool acceptedDuringPresentation = false;
        BattleCommandSubmissionResult presentationCallbackSubmission = default;
        presentation.OnPresent = result =>
        {
            if (acceptedDuringPresentation || result.CommandType != BattleCommandType.EndPlayerAction)
                return;

            acceptedDuringPresentation = true;
            presentationCallbackSubmission = SubmitAndCaptureHandle(queue,
                new StartBattleCommand(),
                out _);
        };

        BattleCommandSubmissionResult endSubmission = SubmitAndCaptureHandle(queue,
            new EndPlayerActionCommand(player.Id),
            out _);

        Assert.That(endSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(executionCallbackSubmission.AuthoritySequence, Is.EqualTo(3));
        Assert.That(presentationCallbackSubmission.AuthoritySequence, Is.EqualTo(5));
        Assert.That(
            lifecycles
                .Where(item => item.Stage == BattleCommandLifecycleStage.Queued)
                .Select(item => (item.AuthoritySequence, item.CommandType)),
            Is.EqualTo(new[]
            {
                (2L, BattleCommandType.EndPlayerAction),
                (3L, BattleCommandType.StartBattle),
                (4L, BattleCommandType.CompleteEnemyAction),
                (5L, BattleCommandType.StartBattle),
            }));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(3));
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(4));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));
        Assert.That(
            lifecycles
                .Where(item => item.Stage != BattleCommandLifecycleStage.Queued)
                .Select(item => (item.AuthoritySequence, item.Stage)),
            Is.EqualTo(new[]
            {
                (2L, BattleCommandLifecycleStage.ExecutionCompleted),
                (3L, BattleCommandLifecycleStage.ExecutionFailed),
                (4L, BattleCommandLifecycleStage.ExecutionCompleted),
            }));

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(presentation.PendingCompletionCount, Is.Zero);
        Assert.That(
            lifecycles
                .Where(item => item.Stage != BattleCommandLifecycleStage.Queued)
                .Select(item => (item.AuthoritySequence, item.Stage)),
            Is.EqualTo(new[]
            {
                (2L, BattleCommandLifecycleStage.ExecutionCompleted),
                (3L, BattleCommandLifecycleStage.ExecutionFailed),
                (4L, BattleCommandLifecycleStage.ExecutionCompleted),
                (5L, BattleCommandLifecycleStage.ExecutionFailed),
            }));
    }

    /// <summary>验证 continuation 的 Queued 先于其入队快照回调中新接受的更大序号命令。</summary>
    [Test]
    public void ContinuationQueued_PrecedesSubmissionFromItsQueueSnapshot()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        using var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        using var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            coordinator: coordinator);
        SubmitAndCaptureHandle(queue, new StartBattleCommand(), out _);
        presentation.CompleteNext();
        lifecycles.Clear();
        bool submittedFromContinuationSnapshot = false;
        using IDisposable queueSubscription = queue.Queue.Skip(1).Subscribe(snapshot =>
        {
            if (submittedFromContinuationSnapshot ||
                snapshot.CurrentAuthoritySequence != 2 ||
                !snapshot.IsWaitingForPresentation ||
                snapshot.PendingCount != 1)
            {
                return;
            }

            submittedFromContinuationSnapshot = true;
            SubmitAndCaptureHandle(queue,
                new StartBattleCommand(),
                out _);
        });

        SubmitAndCaptureHandle(queue,
            new EndPlayerActionCommand(player.Id),
            out _);

        Assert.That(
            lifecycles
                .Where(item => item.Stage == BattleCommandLifecycleStage.Queued)
                .Select(item => (item.AuthoritySequence, item.CommandType)),
            Is.EqualTo(new[]
            {
                (2L, BattleCommandType.EndPlayerAction),
                (3L, BattleCommandType.CompleteEnemyAction),
                (4L, BattleCommandType.StartBattle),
            }));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(2));
    }

    /// <summary>验证无可见结算的成功命令仍发布终态，但不会调用表现层或阻塞后续 drain。</summary>
    [Test]
    public void ZeroVisibleSuccessfulCommand_PublishesCompletionAndDrainsImmediately()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        using var firstZones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1);
        using var secondZones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 2);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData>
            {
                [firstPlayer.Id] = firstZones,
                [secondPlayer.Id] = secondZones,
            },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            coordinator: coordinator);
        SubmitAndCaptureHandle(queue, new StartBattleCommand(), out _);
        presentation.CompleteNext();
        lifecycles.Clear();
        int presentedCountBeforeEnd = presentation.Results.Count;

        BattleCommandSubmissionResult result = SubmitAndCaptureHandle(queue,
            new EndPlayerActionCommand(firstPlayer.Id),
            out BattleCommandHandle handle);

        Assert.That(result.Accepted, Is.True);
        Assert.That(lifecycles, Has.Count.EqualTo(2));
        AssertLifecycle(lifecycles[0], handle, 2, BattleCommandLifecycleStage.Queued);
        AssertLifecycle(lifecycles[1], handle, 2, BattleCommandLifecycleStage.ExecutionCompleted);
        Assert.That(lifecycles[1].Settlements, Is.Empty);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.True);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);
        Assert.That(presentation.Results.Count, Is.EqualTo(presentedCountBeforeEnd));
        Assert.That(presentation.PendingCompletionCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
    }

    /// <summary>验证旧表现 completion 不会解除当前新命令的屏障或跳过其权威序号。</summary>
    [Test]
    public void OldPresentationCompletion_DoesNotReleaseNewVisibleCommand()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        using var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1);
        var coordinator = new BattleCommandSubmissionCoordinator();
        var presentation = new M8BControllableBattleCommandPresentation();
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable lifecycleSubscription = coordinator.Lifecycle.Subscribe(lifecycles.Add);
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            initialHandCount: 1,
            coordinator: coordinator);

        SubmitAndCaptureHandle(queue, new StartBattleCommand(), out _);
        BattleCommandSubmissionResult endSubmission = SubmitAndCaptureHandle(queue,
            new EndPlayerActionCommand(player.Id),
            out _);

        Assert.That(
            endSubmission.Accepted,
            Is.True,
            $"Unexpected submission rejection: {endSubmission.FailureReason}.");
        Assert.That(endSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));
        int lifecycleCountBeforeOldCompletion = lifecycles.Count;

        presentation.CompleteLastAgain();

        Assert.That(lifecycles, Has.Count.EqualTo(lifecycleCountBeforeOldCompletion));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(presentation.PendingCompletionCount, Is.EqualTo(1));

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
    }

    /// <summary>通过 Queue 的公开生命周期捕获同一命令引用对应的句柄。</summary>
    private static BattleCommandSubmissionResult SubmitAndCaptureHandle(
        BattleCommandQueue queue,
        BattleCommand command,
        out BattleCommandHandle handle)
    {
        if (queue == null)
            throw new ArgumentNullException(nameof(queue));
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        BattleCommandHandle capturedHandle = null;
        using IDisposable subscription = queue.Lifecycle.Subscribe(lifecycle =>
        {
            if (capturedHandle == null &&
                lifecycle.Stage == BattleCommandLifecycleStage.Queued &&
                ReferenceEquals(lifecycle.Command, command))
            {
                capturedHandle = lifecycle.Handle;
            }
        });

        BattleCommandSubmissionResult result = queue.Submit(command);
        handle = capturedHandle;
        return result;
    }

    /// <summary>断言生命周期始终携带预注册句柄、权威序号与稳定阶段。</summary>
    private static void AssertLifecycle(
        BattleCommandLifecycleEvent lifecycle,
        BattleCommandHandle handle,
        long authoritySequence,
        BattleCommandLifecycleStage stage)
    {
        Assert.That(lifecycle.Handle, Is.SameAs(handle));
        Assert.That(lifecycle.AuthoritySequence, Is.EqualTo(authoritySequence));
        Assert.That(lifecycle.Stage, Is.EqualTo(stage));
    }

    /// <summary>保存可见执行结果与 completion，使测试能精确控制每一道表现屏障。</summary>
    private sealed class M8BControllableBattleCommandPresentation : IBattleCommandPresentation
    {
        private readonly Queue<Action> _completions = new Queue<Action>();
        private Action _lastCompleted;

        /// <summary>按 Queue 调用顺序保存真正进入表现屏障的执行结果。</summary>
        internal List<BattleCommandExecutionResult> Results { get; } =
            new List<BattleCommandExecutionResult>();

        /// <summary>表现层收到新结果时同步调用的可控测试回调。</summary>
        internal Action<BattleCommandExecutionResult> OnPresent { get; set; }

        /// <summary>当前尚未回报的表现 completion 数量。</summary>
        internal int PendingCompletionCount => _completions.Count;

        /// <summary>记录非空可见结果与精确 completion，再允许测试触发表现期提交。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));

            Results.Add(result);
            _completions.Enqueue(onCompleted);
            OnPresent?.Invoke(result);
        }

        /// <summary>只完成最早的一道表现屏障，并保存其回调供旧 completion 验证。</summary>
        internal void CompleteNext()
        {
            Assert.That(_completions, Is.Not.Empty);
            _lastCompleted = _completions.Dequeue();
            _lastCompleted.Invoke();
        }

        /// <summary>重复触发最近完成的旧回调，模拟迟到或重复的表现信号。</summary>
        internal void CompleteLastAgain()
        {
            Assert.That(_lastCompleted, Is.Not.Null);
            _lastCompleted.Invoke();
        }
    }

    /// <summary>在收到可见结果后抛出异常，用于证明 Queue 的表现后 fault 语义。</summary>
    private sealed class M8BThrowingBattleCommandPresentation : IBattleCommandPresentation
    {
        /// <summary>实际进入表现入口的次数。</summary>
        internal int PresentCount { get; private set; }

        /// <summary>先同步回报 completion 再抛错，模拟最苛刻的表现层后写入异常。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            PresentCount++;
            onCompleted.Invoke();
            throw new InvalidOperationException("M8B presentation fault fixture.");
        }
    }
}
