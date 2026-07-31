using System;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using TinySpire.Battle;

public sealed class BattleCommandQueueTests
{
    /// <summary>验证战斗开始前的玩家命令会在提交 seam 被拒绝，且不会占用权威序号。</summary>
    [Test]
    public void Submit_PlayerCommandBeforeBattleStart_RejectsWithoutQueueing()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new RejectUnexpectedPresentation();
        var queue = new BattleCommandQueue(new[] { player.Id }, presentation);

        BattleCommandSubmissionResult result = queue.Submit(new EndPlayerActionCommand(player.Id));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.AuthoritySequence, Is.Null);
        Assert.That(result.FailureReason, Is.EqualTo(BattleCommandSubmissionFailureReason.BattleNotStarted));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.NotStarted));

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证开始命令取得首个权威序号，并在权威写入后等待表现完成。</summary>
    [Test]
    public void Submit_StartBattle_AssignsFirstSequenceAndWaitsForPresentation()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        var queue = new BattleCommandQueue(new[] { player.Id }, presentation);

        BattleCommandSubmissionResult result = queue.Submit(new StartBattleCommand());

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.AuthoritySequence, Is.EqualTo(1));
        Assert.That(result.FailureReason, Is.EqualTo(BattleCommandSubmissionFailureReason.None));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.CurrentCommandType, Is.EqualTo(BattleCommandType.StartBattle));
        Assert.That(queue.Queue.CurrentValue.CurrentSubmitterId, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(1));
        Assert.That(presentation.Results.Count, Is.EqualTo(1));
        Assert.That(presentation.Results[0].AuthoritySequence, Is.EqualTo(1));
        Assert.That(presentation.Results[0].Succeeded, Is.True);

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证当前命令等待表现时，不同玩家仍可提交且不会提前修改共享事实。</summary>
    [Test]
    public void Submit_WhilePresentationIsPending_AcceptsPlayersWithoutExecutingThem()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        var queue = new BattleCommandQueue(new[] { firstPlayer.Id, secondPlayer.Id }, presentation);
        queue.Submit(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        BattleCommandSubmissionResult firstSubmission =
            queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));
        BattleCommandSubmissionResult secondSubmission =
            queue.Submit(new EndPlayerActionCommand(secondPlayer.Id));

        Assert.That(firstSubmission.Accepted, Is.True);
        Assert.That(firstSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(secondSubmission.Accepted, Is.True);
        Assert.That(secondSubmission.AuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.False);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);
        Assert.That(presentation.Results.Count, Is.EqualTo(1));

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证表现完成信号只推进一条命令，且执行结果严格遵循权威序号。</summary>
    [Test]
    public void PresentationCompletion_AdvancesExactlyOneCommandInAuthorityOrder()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        var queue = new BattleCommandQueue(new[] { player.Id }, presentation);
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());

        presentation.CompleteNext();

        Assert.That(presentation.Results.Count, Is.EqualTo(2));
        Assert.That(presentation.Results[0].AuthoritySequence, Is.EqualTo(1));
        Assert.That(presentation.Results[0].Succeeded, Is.True);
        Assert.That(presentation.Results[1].AuthoritySequence, Is.EqualTo(2));
        Assert.That(presentation.Results[1].Succeeded, Is.False);
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

        presentation.CompleteNext();

        Assert.That(presentation.Results.Count, Is.EqualTo(3));
        Assert.That(presentation.Results[2].AuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(3));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);

        presentation.CompleteNext();

        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证后续里程碑命令只排队并明确失败，不会在 M4A 修改回合或卡区事实。</summary>
    [Test]
    public void CommandsReservedForLaterMilestones_FailWithoutChangingSharedFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var presentation = new ControllableBattleCommandPresentation();
        var queue = new BattleCommandQueue(new[] { player.Id }, presentation);
        queue.Submit(new StartBattleCommand());
        BattleTurnData turnAfterStart = queue.Turn.CurrentValue;

        queue.Submit(new PlayCardCommand(player.Id, cardId));
        queue.Submit(new EndPlayerActionCommand(player.Id));
        queue.Submit(new CompleteEnemyActionCommand(enemy.Id));

        presentation.CompleteNext();
        Assert.That(presentation.Results[1].CommandType, Is.EqualTo(BattleCommandType.PlayCard));
        Assert.That(
            presentation.Results[1].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.UnsupportedCommand));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));
        Assert.That(zones.Hand, Is.EqualTo(new[] { cardId }));

        presentation.CompleteNext();
        Assert.That(presentation.Results[2].CommandType, Is.EqualTo(BattleCommandType.EndPlayerAction));
        Assert.That(
            presentation.Results[2].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.UnsupportedCommand));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].HasEndedAction, Is.False);

        presentation.CompleteNext();
        Assert.That(presentation.Results[3].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(presentation.Results[3].SubmitterId, Is.Null);
        Assert.That(
            presentation.Results[3].FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.UnsupportedCommand));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnAfterStart));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证当前命令执行期间可以重入提交，且执行与等待表现事实不会混淆。</summary>
    [Test]
    public void Submit_DuringCurrentExecution_QueuesWithoutWaitingOrChangingPlayerFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData firstPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        PlayerCombatantData secondPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 28, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        var queue = new BattleCommandQueue(new[] { firstPlayer.Id, secondPlayer.Id }, presentation);
        BattleCommandSubmissionResult firstSubmission = default;
        BattleCommandSubmissionResult secondSubmission = default;
        BattleCommandQueueData queueDuringExecution = null;

        using (queue.Turn.Skip(1).Subscribe(turn =>
               {
                   // BattleStart 的同步发布发生在队首命令执行尚未结束时。
                   if (turn.Phase != BattleTurnPhase.BattleStart)
                       return;

                   firstSubmission = queue.Submit(new EndPlayerActionCommand(firstPlayer.Id));
                   secondSubmission = queue.Submit(new EndPlayerActionCommand(secondPlayer.Id));
                   queueDuringExecution = queue.Queue.CurrentValue;
               }))
        {
            queue.Submit(new StartBattleCommand());
        }

        Assert.That(firstSubmission.Accepted, Is.True);
        Assert.That(firstSubmission.AuthoritySequence, Is.EqualTo(2));
        Assert.That(secondSubmission.Accepted, Is.True);
        Assert.That(secondSubmission.AuthoritySequence, Is.EqualTo(3));
        Assert.That(queueDuringExecution.CurrentAuthoritySequence, Is.EqualTo(1));
        Assert.That(queueDuringExecution.PendingCount, Is.EqualTo(2));
        Assert.That(queueDuringExecution.IsWaitingForPresentation, Is.False);
        Assert.That(queue.Turn.CurrentValue.Players[firstPlayer.Id].HasEndedAction, Is.False);
        Assert.That(queue.Turn.CurrentValue.Players[secondPlayer.Id].HasEndedAction, Is.False);
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);
        Assert.That(presentation.Results.Count, Is.EqualTo(1));

        queue.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证过期的重复表现回调不会越过正在等待的当前命令。</summary>
    [Test]
    public void PresentationCompletion_WhenRepeated_DoesNotSkipCurrentCommand()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        var presentation = new ControllableBattleCommandPresentation();
        var queue = new BattleCommandQueue(new[] { player.Id }, presentation);
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());
        queue.Submit(new StartBattleCommand());

        presentation.CompleteNext();
        presentation.CompleteLastAgain();

        Assert.That(presentation.Results.Count, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.EqualTo(2));
        Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));
        Assert.That(queue.Queue.CurrentValue.IsWaitingForPresentation, Is.True);

        queue.Dispose();
        combatants.Dispose();
    }

    private sealed class RejectUnexpectedPresentation : IBattleCommandPresentation
    {
        /// <summary>任何展示请求都表示被拒绝的命令错误地进入了执行流程。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            Assert.Fail($"Rejected command unexpectedly reached presentation: {result.AuthoritySequence}.");
        }
    }

}

internal sealed class ControllableBattleCommandPresentation : IBattleCommandPresentation
{
    private readonly Queue<Action> _completions = new Queue<Action>();
    private Action _lastCompleted;

    /// <summary>按收到顺序保存的权威执行结果。</summary>
    internal List<BattleCommandExecutionResult> Results { get; } =
        new List<BattleCommandExecutionResult>();

    /// <summary>记录当前展示结果，并故意保持未完成以观察等待事实。</summary>
    public void Present(BattleCommandExecutionResult result, Action onCompleted)
    {
        Results.Add(result);
        _completions.Enqueue(onCompleted);
    }

    /// <summary>完成最早收到的展示请求，模拟表现层按顺序回报完成。</summary>
    internal void CompleteNext()
    {
        _lastCompleted = _completions.Dequeue();
        _lastCompleted.Invoke();
    }

    /// <summary>再次触发最近完成的回调，用于验证过期信号不会重复推进。</summary>
    internal void CompleteLastAgain()
    {
        _lastCompleted.Invoke();
    }
}
