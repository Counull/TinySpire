using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class BattleCommandPresentationAdapterTests
{
    /// <summary>确认即时执行也会先保留 UI 发布的排队反馈，再于 Tick 发布执行完成。</summary>
    [Test]
    public void ImmediateExecution_PublishesQueuedBeforeExecutionCompleted()
    {
        var adapter = new BattleCommandPresentationAdapter(0f, () => 0f);
        var feedback = new List<BattleCommandFeedback>();
        using (adapter.Feedback.Subscribe(feedback.Add))
        using (var combatants = new BattleCombatantsData())
        using (var zones = new BattleCardZonesData(new int[0], shuffleSeed: 1u))
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones
            };
            using (BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
                       combatants,
                       adapter,
                       playerZones,
                       initialHandCount: 0))
            {
                queue.Submit(new StartBattleCommand());
                adapter.Tick();
                feedback.Clear();

                var command = new EndPlayerActionCommand(player.Id);
                BattleCommandSubmissionResult submission = queue.Submit(command);
                adapter.PublishQueued(command, submission);

                Assert.That(feedback.Select(item => item.Stage), Is.EqualTo(new[]
                {
                    BattleCommandFeedbackStage.Queued
                }));

                adapter.Tick();

                Assert.That(feedback.Select(item => item.Stage), Is.EqualTo(new[]
                {
                    BattleCommandFeedbackStage.Queued,
                    BattleCommandFeedbackStage.ExecutionCompleted
                }));
                Assert.That(queue.Queue.CurrentValue.CurrentAuthoritySequence, Is.Null);
            }
        }

        adapter.Dispose();
    }

    /// <summary>确认展示等待期间仍可排队第二张牌，且它不会提前扣能量或移动卡区。</summary>
    [Test]
    public void PresentationWait_AllowsSecondPlayWithoutPrematureMutation()
    {
        float deltaTime = 0f;
        var adapter = new BattleCommandPresentationAdapter(1f, () => deltaTime);
        using (var combatants = new BattleCombatantsData())
        using (var zones = new BattleCardZonesData(new[] { 1001, 1002 }, shuffleSeed: 1u))
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones
            };
            var costs = new Dictionary<int, int>
            {
                [1001] = 1,
                [1002] = 1
            };
            using (BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
                       combatants,
                       adapter,
                       playerZones,
                       costs,
                       energyPerRound: 3,
                       initialHandCount: 2))
            {
                queue.Submit(new StartBattleCommand());
                deltaTime = 1f;
                adapter.Tick();

                deltaTime = 0f;
                CardInstanceId firstCardId = zones.Hand[0];
                CardInstanceId secondCardId = zones.Hand[1];
                var firstCommand = new PlayCardCommand(player.Id, firstCardId);
                BattleCommandSubmissionResult firstSubmission = queue.Submit(firstCommand);
                adapter.PublishQueued(firstCommand, firstSubmission);
                var secondCommand = new PlayCardCommand(player.Id, secondCardId);
                BattleCommandSubmissionResult secondSubmission = queue.Submit(secondCommand);
                adapter.PublishQueued(secondCommand, secondSubmission);

                Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(2));
                Assert.That(zones.Hand.Contains(secondCardId), Is.True);
                Assert.That(zones.DiscardPile.Contains(secondCardId), Is.False);
                Assert.That(queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));

                deltaTime = 1f;
                adapter.Tick();

                Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(1));
                Assert.That(zones.DiscardPile, Does.Contain(secondCardId));
            }
        }

        adapter.Dispose();
    }

    /// <summary>确认执行期能量不足会发布失败反馈，并保持能量与卡区权威事实不变。</summary>
    [Test]
    public void ExecutionFailure_PublishesFailureWithoutEnergyOrZoneMutation()
    {
        var adapter = new BattleCommandPresentationAdapter(0f, () => 0f);
        var feedback = new List<BattleCommandFeedback>();
        using (adapter.Feedback.Subscribe(feedback.Add))
        using (var combatants = new BattleCombatantsData())
        using (var zones = new BattleCardZonesData(new[] { 1001 }, shuffleSeed: 1u))
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            var playerZones = new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones
            };
            var costs = new Dictionary<int, int>
            {
                [1001] = 1
            };
            using (BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
                       combatants,
                       adapter,
                       playerZones,
                       costs,
                       energyPerRound: 0,
                       initialHandCount: 1))
            {
                queue.Submit(new StartBattleCommand());
                adapter.Tick();
                feedback.Clear();

                CardInstanceId cardId = zones.Hand[0];
                var command = new PlayCardCommand(player.Id, cardId);
                BattleCommandSubmissionResult submission = queue.Submit(command);
                adapter.PublishQueued(command, submission);
                adapter.Tick();

                Assert.That(feedback[^1].Stage, Is.EqualTo(BattleCommandFeedbackStage.ExecutionFailed));
                Assert.That(
                    feedback[^1].FailureReason,
                    Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
                Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.Zero);
                Assert.That(zones.Hand, Does.Contain(cardId));
                Assert.That(zones.DiscardPile.Contains(cardId), Is.False);
            }
        }

        adapter.Dispose();
    }
}
