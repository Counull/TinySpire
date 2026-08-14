using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleCommandSchedulingContractTests
{
    /// <summary>验证提交方先取得不暴露数值的句柄，Queue core 接受后才把同一句柄绑定到权威序号。</summary>
    [Test]
    public void AcceptPreRegistered_BindsOpaqueHandleToQueuedLifecycle()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        var command = new StartBattleCommand();

        BattleCommandHandle handle = coordinator.PreRegister(command);
        BattleCommandSchedulingAcceptance acceptance = scheduling.AcceptPreRegistered(
            handle,
            command,
            submittedRoundNumber: 0);

        Assert.That(handle, Is.Not.Null);
        Assert.That(
            typeof(BattleCommandHandle).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            Is.Empty);
        Assert.That(
            typeof(BattleCommandHandle).GetFields(BindingFlags.Instance | BindingFlags.Public),
            Is.Empty);
        Assert.That(acceptance.Submission.Accepted, Is.True);
        Assert.That(acceptance.Submission.AuthoritySequence, Is.EqualTo(1));
        Assert.That(acceptance.QueuedLifecycle.Handle, Is.SameAs(handle));
        Assert.That(acceptance.QueuedLifecycle.AuthoritySequence, Is.EqualTo(1));
        Assert.That(acceptance.QueuedLifecycle.Stage, Is.EqualTo(BattleCommandLifecycleStage.Queued));
        Assert.That(coordinator.IsPending(handle), Is.True);
    }

    /// <summary>验证拒绝会撤销预注册句柄且不消耗下一个权威序号。</summary>
    [Test]
    public void RejectPreRegistered_CancelsHandleWithoutAllocatingSequence()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        var rejectedCommand = new StartBattleCommand();
        BattleCommandHandle rejectedHandle = coordinator.PreRegister(rejectedCommand);

        BattleCommandSubmissionResult rejected = scheduling.RejectPreRegistered(
            rejectedHandle,
            BattleCommandSubmissionFailureReason.BattleAlreadyEnded);
        var acceptedCommand = new StartBattleCommand();
        BattleCommandSchedulingAcceptance accepted = scheduling.AcceptPreRegistered(
            coordinator.PreRegister(acceptedCommand),
            acceptedCommand,
            submittedRoundNumber: 0);

        Assert.That(rejected.Accepted, Is.False);
        Assert.That(rejected.AuthoritySequence, Is.Null);
        Assert.That(rejected.FailureReason, Is.EqualTo(BattleCommandSubmissionFailureReason.BattleAlreadyEnded));
        Assert.That(coordinator.IsPending(rejectedHandle), Is.False);
        Assert.That(accepted.Submission.AuthoritySequence, Is.EqualTo(1));
    }

    /// <summary>验证调用方不能伪造 Queue continuation，拒绝时撤销句柄且不消耗序号。</summary>
    [Test]
    public void AcceptPreRegistered_RejectsForgedSystemCommandWithoutSequence()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        var forged = new CompleteEnemyActionCommand(new CombatantId(2001));
        BattleCommandHandle forgedHandle = coordinator.PreRegister(forged);

        BattleCommandSchedulingAcceptance rejected = scheduling.AcceptPreRegistered(
            forgedHandle,
            forged,
            submittedRoundNumber: 1);
        BattleCommandSchedulingAcceptance accepted = Accept(
            scheduling,
            coordinator,
            new StartBattleCommand());

        Assert.That(rejected.Submission.Accepted, Is.False);
        Assert.That(rejected.Submission.AuthoritySequence, Is.Null);
        Assert.That(
            rejected.Submission.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.SystemCommandNotAuthorized));
        Assert.That(rejected.QueuedLifecycle, Is.Null);
        Assert.That(coordinator.IsPending(forgedHandle), Is.False);
        Assert.That(accepted.AuthoritySequence(), Is.EqualTo(1));
    }

    /// <summary>验证错配命令不能借用旧句柄，拒绝时会撤销原预注册且不消耗序号。</summary>
    [Test]
    public void AcceptPreRegistered_MismatchedCommandCancelsOriginalHandle()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        var registered = new StartBattleCommand();
        BattleCommandHandle handle = coordinator.PreRegister(registered);

        BattleCommandSchedulingAcceptance rejected = scheduling.AcceptPreRegistered(
            handle,
            new EndPlayerActionCommand(new CombatantId(1001)),
            submittedRoundNumber: 1);
        BattleCommandSchedulingAcceptance accepted = Accept(
            scheduling,
            coordinator,
            new StartBattleCommand());

        Assert.That(rejected.Submission.Accepted, Is.False);
        Assert.That(
            rejected.Submission.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.InvalidSubmissionHandle));
        Assert.That(coordinator.IsPending(handle), Is.False);
        Assert.That(accepted.AuthoritySequence(), Is.EqualTo(1));
    }

    /// <summary>验证执行中既有接受项排在 continuation 前，表现期间的新提交排在 continuation 后。</summary>
    [Test]
    public void CompleteCurrent_QueuesContinuationBeforePresentationAndPreservesFifo()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        BattleCommandSchedulingAcceptance first = Accept(scheduling, coordinator, new StartBattleCommand());

        Assert.That(scheduling.TryEnterDrain(), Is.True);
        Assert.That(scheduling.TryBeginNext(out BattleCommandSchedulingEntry firstEntry), Is.True);
        BattleCommandSchedulingAcceptance acceptedDuringExecution =
            Accept(scheduling, coordinator, new StartBattleCommand());
        Assert.That(scheduling.TryEnterDrain(), Is.False, "执行回调内不得重入 drain");

        var continuation = new CompleteEnemyActionCommand(new CombatantId(2001));
        var visibleSettlements = new BattleSettlementRecord[]
        {
            new BattlePhaseChangedSettlement(
                0,
                BattleTurnPhase.BattleStart,
                BattleTurnPhase.PlayerRoundStart,
                0,
                1,
                currentActingEnemyIdBefore: null,
                currentActingEnemyIdAfter: null),
        };
        BattleCommandSchedulingCompletion firstCompletion = scheduling.CompleteCurrent(
            firstEntry,
            BattleCommandExecutionFailureReason.None,
            visibleSettlements,
            continuation,
            continuationSubmittedRoundNumber: 1);
        BattleCommandSchedulingAcceptance acceptedDuringPresentation =
            Accept(scheduling, coordinator, new StartBattleCommand());

        Assert.That(first.AuthoritySequence(), Is.EqualTo(1));
        Assert.That(acceptedDuringExecution.AuthoritySequence(), Is.EqualTo(2));
        Assert.That(firstCompletion.ContinuationQueuedLifecycle.AuthoritySequence, Is.EqualTo(3));
        Assert.That(acceptedDuringPresentation.AuthoritySequence(), Is.EqualTo(4));
        Assert.That(firstCompletion.CurrentLifecycle.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
        Assert.That(scheduling.TryBeginNext(out _), Is.False, "表现屏障完成前不得推进");
        Assert.That(scheduling.CompletePresentation(firstEntry.AuthoritySequence), Is.True);
        Assert.That(scheduling.TryEnterDrain(), Is.False, "同步 completion 仍在外层 drain 内");

        Assert.That(scheduling.TryBeginNext(out BattleCommandSchedulingEntry secondEntry), Is.True);
        Assert.That(secondEntry.AuthoritySequence, Is.EqualTo(2));
        scheduling.CompleteCurrent(
            secondEntry,
            BattleCommandExecutionFailureReason.None,
            Array.Empty<BattleSettlementRecord>(),
            continuationCommand: null,
            continuationSubmittedRoundNumber: 0);

        Assert.That(scheduling.TryBeginNext(out BattleCommandSchedulingEntry continuationEntry), Is.True);
        Assert.That(continuationEntry.AuthoritySequence, Is.EqualTo(3));
        Assert.That(continuationEntry.RequiresSystemToken, Is.True);
        Assert.That(continuationEntry.IsSystemTokenConsumed, Is.True);
        Assert.That(continuationEntry.TryConsumeSystemToken(scheduling), Is.False);
        scheduling.CompleteCurrent(
            continuationEntry,
            BattleCommandExecutionFailureReason.None,
            Array.Empty<BattleSettlementRecord>(),
            continuationCommand: null,
            continuationSubmittedRoundNumber: 0);

        Assert.That(scheduling.TryBeginNext(out BattleCommandSchedulingEntry fourthEntry), Is.True);
        Assert.That(fourthEntry.AuthoritySequence, Is.EqualTo(4));
        scheduling.CompleteCurrent(
            fourthEntry,
            BattleCommandExecutionFailureReason.None,
            Array.Empty<BattleSettlementRecord>(),
            continuationCommand: null,
            continuationSubmittedRoundNumber: 0);
        scheduling.ExitDrain();

        Assert.That(scheduling.TryEnterDrain(), Is.True);
        scheduling.ExitDrain();
    }

    /// <summary>验证 Queue 可为成功卡牌签发一次结束玩家行动续延，并仍以系统 token 限制其唯一消费。</summary>
    [Test]
    public void CompleteCurrent_AllowsEndPlayerActionAsSystemContinuation()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        BattleCommandSchedulingAcceptance accepted = Accept(
            scheduling,
            coordinator,
            new StartBattleCommand());
        var continuation = new EndPlayerActionCommand(new CombatantId(1001));

        Assert.That(scheduling.TryEnterDrain(), Is.True);
        Assert.That(scheduling.TryBeginNext(out BattleCommandSchedulingEntry current), Is.True);
        BattleCommandSchedulingCompletion completion = scheduling.CompleteCurrent(
            current,
            BattleCommandExecutionFailureReason.None,
            Array.Empty<BattleSettlementRecord>(),
            continuation,
            continuationSubmittedRoundNumber: 1);

        Assert.That(accepted.AuthoritySequence(), Is.EqualTo(1));
        Assert.That(completion.ContinuationQueuedLifecycle, Is.Not.Null);
        Assert.That(
            completion.ContinuationQueuedLifecycle.CommandType,
            Is.EqualTo(BattleCommandType.EndPlayerAction));
        Assert.That(scheduling.TryBeginNext(out BattleCommandSchedulingEntry continuationEntry), Is.True);
        Assert.That(continuationEntry.AuthoritySequence, Is.EqualTo(2));
        Assert.That(continuationEntry.RequiresSystemToken, Is.True);
        Assert.That(continuationEntry.IsSystemTokenConsumed, Is.True);
        Assert.That(continuationEntry.TryConsumeSystemToken(scheduling), Is.False);
        scheduling.CompleteCurrent(
            continuationEntry,
            BattleCommandExecutionFailureReason.None,
            Array.Empty<BattleSettlementRecord>(),
            continuationCommand: null,
            continuationSubmittedRoundNumber: 0);
        scheduling.ExitDrain();
    }

    /// <summary>验证 system token 只认签发核心且只能消费一次。</summary>
    [Test]
    public void SystemToken_RejectsCrossOwnerAndSecondConsumption()
    {
        var owner = new BattleCommandSchedulingCore(new BattleCommandSubmissionCoordinator());
        var other = new BattleCommandSchedulingCore(new BattleCommandSubmissionCoordinator());
        var token = new BattleSystemCommandToken(owner);

        Assert.That(token.TryConsume(other), Is.False);
        Assert.That(token.TryConsume(owner), Is.True);
        Assert.That(token.TryConsume(owner), Is.False);
        Assert.That(token.IsConsumed, Is.True);
    }

    /// <summary>验证普通失败只形成空结算终态，不生成 continuation 或展示屏障。</summary>
    [Test]
    public void CompleteCurrent_ExecutionFailureHasEmptyTerminalLifecycleAndNoBarrier()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        BattleCommandSchedulingAcceptance accepted = Accept(
            scheduling,
            coordinator,
            new StartBattleCommand());
        scheduling.TryEnterDrain();
        scheduling.TryBeginNext(out BattleCommandSchedulingEntry current);

        BattleCommandSchedulingCompletion completion = scheduling.CompleteCurrent(
            current,
            BattleCommandExecutionFailureReason.InvalidTurnPhase,
            Array.Empty<BattleSettlementRecord>(),
            continuationCommand: null,
            continuationSubmittedRoundNumber: 0);

        Assert.That(completion.CurrentLifecycle.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(
            completion.CurrentLifecycle.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InvalidTurnPhase));
        Assert.That(completion.CurrentLifecycle.Settlements, Is.Empty);
        Assert.That(completion.ContinuationQueuedLifecycle, Is.Null);
        Assert.That(scheduling.CurrentAuthoritySequence, Is.Null);
        Assert.That(scheduling.CompletePresentation(accepted.AuthoritySequence()), Is.False);
        Assert.That(coordinator.Reconcile(completion.CurrentLifecycle), Is.True);
        scheduling.ExitDrain();
    }

    /// <summary>验证 fault 保留当前序号与待处理项、冻结 drain，并拒绝新提交且不产生结算。</summary>
    [Test]
    public void FaultCurrent_FreezesDrainAndRejectsNewAcceptance()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        Accept(scheduling, coordinator, new StartBattleCommand());
        Accept(scheduling, coordinator, new StartBattleCommand());
        scheduling.TryEnterDrain();
        scheduling.TryBeginNext(out BattleCommandSchedulingEntry current);

        Assert.Throws<ArgumentException>(() => scheduling.FaultCurrent(
            current,
            BattleCommandQueueFaultReason.MissingEffect,
            mayHavePartialWrites: true));

        BattleCommandLifecycleEvent faulted = scheduling.FaultCurrent(
            current,
            BattleCommandQueueFaultReason.MissingEffect,
            mayHavePartialWrites: false);
        var rejectedCommand = new StartBattleCommand();
        BattleCommandHandle rejectedHandle = coordinator.PreRegister(rejectedCommand);
        BattleCommandSchedulingAcceptance rejected = scheduling.AcceptPreRegistered(
            rejectedHandle,
            rejectedCommand,
            submittedRoundNumber: 0);

        Assert.That(faulted.Stage, Is.EqualTo(BattleCommandLifecycleStage.Faulted));
        Assert.That(faulted.Fault, Is.SameAs(scheduling.Fault));
        Assert.That(faulted.Settlements, Is.Empty);
        Assert.That(typeof(BattleSettlementRecord).IsAssignableFrom(faulted.Fault.GetType()), Is.False);
        Assert.That(scheduling.Fault.AuthoritySequence, Is.EqualTo(1));
        Assert.That(scheduling.Fault.Reason, Is.EqualTo(BattleCommandQueueFaultReason.MissingEffect));
        Assert.That(scheduling.Fault.MayHavePartialWrites, Is.False);
        Assert.That(scheduling.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(scheduling.PendingCount, Is.EqualTo(1));
        Assert.That(rejected.Submission.Accepted, Is.False);
        Assert.That(rejected.Submission.AuthoritySequence, Is.Null);
        Assert.That(rejected.Submission.FailureReason, Is.EqualTo(BattleCommandSubmissionFailureReason.QueueFaulted));
        Assert.That(coordinator.IsPending(rejectedHandle), Is.False);
        Assert.That(scheduling.CompletePresentation(current.AuthoritySequence), Is.False);
        scheduling.ExitDrain();
        Assert.That(scheduling.TryEnterDrain(), Is.False);
    }

    /// <summary>验证只有不可预期异常 fault 可以明确标记首次写入后的可能部分权威写入。</summary>
    [Test]
    public void FaultCurrent_UnexpectedExceptionCanMarkPossiblePartialWrites()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        Accept(scheduling, coordinator, new StartBattleCommand());
        scheduling.TryEnterDrain();
        scheduling.TryBeginNext(out BattleCommandSchedulingEntry current);

        BattleCommandLifecycleEvent faulted = scheduling.FaultCurrent(
            current,
            BattleCommandQueueFaultReason.UnexpectedException,
            mayHavePartialWrites: true);

        Assert.That(faulted.Stage, Is.EqualTo(BattleCommandLifecycleStage.Faulted));
        Assert.That(faulted.Fault.MayHavePartialWrites, Is.True);
        Assert.That(faulted.Settlements, Is.Empty);
        scheduling.ExitDrain();
    }

    /// <summary>验证权威提交后表现调用异常会冻结为明确的可能部分写入 fault。</summary>
    [Test]
    public void FaultCurrent_AfterVisibleCommitFreezesPresentationAsPartialWriteFault()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        Accept(scheduling, coordinator, new StartBattleCommand());
        scheduling.TryEnterDrain();
        scheduling.TryBeginNext(out BattleCommandSchedulingEntry current);
        BattleCommandSchedulingCompletion completed = scheduling.CompleteCurrent(
            current,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattlePhaseChangedSettlement(
                    0,
                    BattleTurnPhase.BattleStart,
                    BattleTurnPhase.PlayerRoundStart,
                    0,
                    1,
                    currentActingEnemyIdBefore: null,
                    currentActingEnemyIdAfter: null),
            },
            continuationCommand: null,
            continuationSubmittedRoundNumber: 0);

        BattleCommandLifecycleEvent faulted = scheduling.FaultCurrent(
            current,
            BattleCommandQueueFaultReason.UnexpectedException,
            mayHavePartialWrites: true);

        Assert.That(completed.CurrentLifecycle.Settlements, Has.Count.EqualTo(1));
        Assert.That(faulted.Stage, Is.EqualTo(BattleCommandLifecycleStage.Faulted));
        Assert.That(faulted.Fault.MayHavePartialWrites, Is.True);
        Assert.That(faulted.Settlements, Is.Empty);
        Assert.That(scheduling.CompletePresentation(current.AuthoritySequence), Is.False);
        scheduling.ExitDrain();
        Assert.That(scheduling.TryEnterDrain(), Is.False);
    }

    /// <summary>验证旧终态只结清自己的句柄，不会清除同一语义命令的新 pending。</summary>
    [Test]
    public void Reconcile_OldTerminalLifecycleDoesNotClearNewHandle()
    {
        var coordinator = new BattleCommandSubmissionCoordinator();
        var scheduling = new BattleCommandSchedulingCore(coordinator);
        var command = new StartBattleCommand();
        BattleCommandHandle oldHandle = coordinator.PreRegister(command);
        scheduling.AcceptPreRegistered(oldHandle, command, submittedRoundNumber: 0);
        scheduling.TryEnterDrain();
        scheduling.TryBeginNext(out BattleCommandSchedulingEntry current);
        BattleCommandLifecycleEvent oldTerminal = scheduling.CompleteCurrent(
            current,
            BattleCommandExecutionFailureReason.None,
            Array.Empty<BattleSettlementRecord>(),
            continuationCommand: null,
            continuationSubmittedRoundNumber: 0).CurrentLifecycle;

        BattleCommandHandle newHandle = coordinator.PreRegister(command);
        scheduling.AcceptPreRegistered(newHandle, command, submittedRoundNumber: 0);

        Assert.That(coordinator.Reconcile(oldTerminal), Is.True);
        Assert.That(coordinator.IsPending(oldHandle), Is.False);
        Assert.That(coordinator.IsPending(newHandle), Is.True);
        Assert.That(coordinator.Reconcile(oldTerminal), Is.False);
        scheduling.ExitDrain();
    }

    /// <summary>验证只读 Queue 快照只保存一份 fault 引用，IsFaulted 由它派生且均不可写。</summary>
    [Test]
    public void QueueData_ExposesSingleImmutableFaultFact()
    {
        PropertyInfo fault = typeof(BattleCommandQueueData).GetProperty("Fault");
        PropertyInfo isFaulted = typeof(BattleCommandQueueData).GetProperty("IsFaulted");

        Assert.That(fault, Is.Not.Null);
        Assert.That(fault.PropertyType, Is.EqualTo(typeof(BattleCommandQueueFaultData)));
        Assert.That(fault.SetMethod, Is.Null);
        Assert.That(isFaulted, Is.Not.Null);
        Assert.That(isFaulted.PropertyType, Is.EqualTo(typeof(bool)));
        Assert.That(isFaulted.SetMethod, Is.Null);
        Assert.That(typeof(BattleCommandSchedulingCore).IsPublic, Is.False);
        Assert.That(typeof(BattleCommandSchedulingEntry).IsPublic, Is.False);
        Assert.That(typeof(BattleCommandSchedulingAcceptance).IsPublic, Is.False);
        Assert.That(typeof(BattleCommandSchedulingCompletion).IsPublic, Is.False);
    }

    /// <summary>通过同一预注册与接受路径创建测试命令，避免测试自行分配序号。</summary>
    private static BattleCommandSchedulingAcceptance Accept(
        BattleCommandSchedulingCore scheduling,
        BattleCommandSubmissionCoordinator coordinator,
        BattleCommand command)
    {
        return scheduling.AcceptPreRegistered(
            coordinator.PreRegister(command),
            command,
            submittedRoundNumber: 0);
    }
}

internal static class BattleCommandSchedulingAcceptanceAssertions
{
    /// <summary>读取测试断言所需的已接受权威序号。</summary>
    internal static long AuthoritySequence(this BattleCommandSchedulingAcceptance acceptance)
    {
        return acceptance.Submission.AuthoritySequence.Value;
    }
}
