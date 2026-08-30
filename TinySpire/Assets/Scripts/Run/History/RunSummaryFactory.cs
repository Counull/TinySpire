using System;
using System.Linq;

namespace TinySpire.Run.History
{
    /// <summary>只把唯一 Terminal RunState 转换为一次完整终局快照。</summary>
    public static class RunSummaryFactory
    {
        /// <summary>从终局状态深复制路径、牌组与持有物并冻结完成时间。</summary>
        public static RunSummary Create(RunState terminalState, DateTimeOffset completedAtUtc)
        {
            if (terminalState == null)
                throw new ArgumentNullException(nameof(terminalState));
            if (terminalState.ProgressPhase != RunProgressPhase.Terminal ||
                terminalState.Outcome == null)
            {
                throw new InvalidOperationException("Only a Terminal RunState can create Run history.");
            }
            if (terminalState.Outcome.RunId != terminalState.RunId)
                throw new InvalidOperationException("Run outcome belongs to another Run.");

            string outcomeBattleNodeId = null;
            int? outcomeBattleAttemptSequence = null;
            switch (terminalState.Outcome.Kind)
            {
                case RunOutcomeKind.Victory:
                case RunOutcomeKind.Defeat:
                    if (!terminalState.Outcome.BattleId.HasValue)
                        throw new InvalidOperationException("Battle outcome is missing its Battle id.");

                    outcomeBattleNodeId = terminalState.Outcome.BattleId.Value.NodeId.Value;
                    outcomeBattleAttemptSequence =
                        terminalState.Outcome.BattleId.Value.AttemptSequence;
                    break;
                case RunOutcomeKind.Abandoned:
                    if (terminalState.Outcome.BattleId.HasValue)
                        throw new InvalidOperationException("Abandoned outcome cannot carry a Battle id.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(terminalState));
            }

            var path = terminalState.PathNodeIds
                .Select(nodeId => terminalState.MapDefinition.GetNode(nodeId))
                .Select(node => new RunSummaryPathNode(
                    node.Id.Value,
                    node.Kind,
                    node.ContentId))
                .ToArray();
            var deck = terminalState.RunDeck.Cards
                .Select(card => new RunSummaryCard(
                    card.InstanceId.Sequence,
                    card.TemplateId,
                    card.UpgradeLevel))
                .ToArray();
            var holdings = new RunSummaryHoldings(
                terminalState.Holdings.Gold,
                terminalState.Holdings.Relics.Select(relic => new RunSummaryRelic(
                    relic.InstanceId.Sequence,
                    relic.TemplateId)),
                terminalState.Holdings.Potions.Select(potion => new RunSummaryPotion(
                    potion.InstanceId.Sequence,
                    potion.TemplateId)));

            return new RunSummary(
                terminalState.RunId,
                completedAtUtc,
                terminalState.HeroTemplateId,
                terminalState.Outcome.Kind,
                outcomeBattleNodeId,
                outcomeBattleAttemptSequence,
                terminalState.RandomRootSeed,
                terminalState.CurrentHealth,
                terminalState.MaxHealth,
                terminalState.BattleAttemptSequence,
                path,
                deck,
                holdings);
        }
    }
}
