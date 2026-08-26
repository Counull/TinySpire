using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>跨场景 Run 业务事实的唯一写入所有者。</summary>
    public sealed class RunStateStore : IDisposable
    {
        private readonly ReactiveProperty<RunState> _state;

        /// <summary>当前 Run 完整不可变事实的只读响应式视图。</summary>
        public ReadOnlyReactiveProperty<RunState> State { get; }

        /// <summary>当前 Run 的不可变快照；创建前为空。</summary>
        public RunState Current => State.CurrentValue;

        /// <summary>建立初始为空的唯一 Run 事实容器。</summary>
        public RunStateStore()
        {
            _state = new ReactiveProperty<RunState>(null);
            State = _state.ToReadOnlyReactiveProperty();
        }

        /// <summary>创建并发布当前进程内唯一的一局新 Run。</summary>
        public RunState CreateNewRun(RunCreationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (Current != null)
                throw new InvalidOperationException("An active run already exists.");

            Publish(new RunState(options));
            return Current;
        }

        /// <summary>只在没有 active Run 时发布一份已验证的地图稳定读档状态。</summary>
        public RunState RestoreRun(RunRestoreOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (Current != null)
                throw new InvalidOperationException("An active run already exists.");

            Publish(new RunState(options));
            return Current;
        }

        /// <summary>只在没有战斗暂存事实的稳定页显式销毁当前进程 Run。</summary>
        public void ClearStableRun()
        {
            RunState state = Current;
            if (state == null)
                return;
            if (state.ActiveBattle != null ||
                (state.ProgressPhase != RunProgressPhase.MapReady &&
                 state.ProgressPhase != RunProgressPhase.BossGateReached &&
                 state.ProgressPhase != RunProgressPhase.Terminal))
            {
                throw new InvalidOperationException(
                    "Only a map-stable Run can be cleared outside battle.");
            }

            _state.Value = null;
        }

        /// <summary>按普通可达性提交下一节点；Combat 进入承诺态，Boss 只抵达稳定门。</summary>
        public RunState CommitNode(MapNodeId nodeId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.MapReady ||
                state.ActiveBattle != null ||
                state.CommittedNodeId != null)
            {
                throw new InvalidOperationException("The Run is not ready to select a map node.");
            }

            IReadOnlyList<MapNodeId> selectableNodeIds = MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary);
            if (!selectableNodeIds.Contains(nodeId))
                throw new InvalidOperationException("The requested map node is not ordinarily selectable.");

            MapNode node = state.MapDefinition.GetNode(nodeId);
            switch (node.Kind)
            {
                case MapNodeKind.Combat:
                    Publish(new RunState(
                        state,
                        state.CurrentHealth,
                        state.PathNodeIds,
                        RunProgressPhase.EncounterCommitted,
                        node.Id,
                        state.BattleAttemptSequence,
                        activeBattle: null,
                        terminalReason: null));
                    return Current;
                case MapNodeKind.Boss:
                    var reachedPath = state.PathNodeIds.Concat(new[] { node.Id }).ToArray();
                    Publish(new RunState(
                        state,
                        state.CurrentHealth,
                        reachedPath,
                        RunProgressPhase.BossGateReached,
                        committedNodeId: null,
                        battleAttemptSequence: state.BattleAttemptSequence,
                        activeBattle: null,
                        terminalReason: null));
                    return Current;
                default:
                    throw new InvalidOperationException("Start cannot be selected as a destination.");
            }
        }

        /// <summary>为已承诺的 Combat 节点签发唯一 attempt 与冻结 Encounter 输入。</summary>
        public RunBattleInput BeginCommittedBattle()
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.EncounterCommitted ||
                state.CommittedNodeId == null ||
                state.ActiveBattle != null)
            {
                throw new InvalidOperationException("The Run does not have a committed Combat node.");
            }

            int attemptSequence = checked(state.BattleAttemptSequence + 1);
            uint battleSeed = DeriveBattleSeed(state.RandomRootSeed, attemptSequence);
            var input = new RunBattleInput(state, attemptSequence, battleSeed);
            Publish(new RunState(
                state,
                state.CurrentHealth,
                state.PathNodeIds,
                RunProgressPhase.InBattle,
                state.CommittedNodeId,
                attemptSequence,
                input,
                terminalReason: null));
            return input;
        }

        /// <summary>只为当前 attempt 生成一次奖励，并原子冻结胜利生命与 RewardPending。</summary>
        public RunState RecordVictoryAndFreezeReward(
            RunBattleId battleId,
            int heroTemplateId,
            int settledHealth,
            int maxHealth,
            Func<RunBattleInput, PendingCardReward> rewardFactory)
        {
            RunState state = RequireActiveBattle(battleId);
            ValidateResultPlayer(state, heroTemplateId, settledHealth, maxHealth);
            if (settledHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(settledHealth));
            if (rewardFactory == null)
                throw new ArgumentNullException(nameof(rewardFactory));

            PendingCardReward pendingCardReward = rewardFactory(state.ActiveBattle)
                ?? throw new InvalidOperationException("Victory reward factory returned null.");
            if (pendingCardReward.Id.BattleId != battleId)
            {
                throw new InvalidOperationException(
                    "Victory reward id does not match the active run battle.");
            }

            Publish(new RunState(
                state,
                settledHealth,
                state.PathNodeIds,
                RunProgressPhase.RewardPending,
                committedNodeId: state.CommittedNodeId,
                battleAttemptSequence: state.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: pendingCardReward));
            return Current;
        }

        /// <summary>校验奖励身份与候选并构造结算后快照，但不发布任何 Run 业务事实。</summary>
        internal RunState PreviewCardRewardSettlement(
            RunCardRewardId rewardId,
            int? selectedCardTemplateId)
        {
            return CreateCardRewardSettlement(rewardId, selectedCardTemplateId);
        }

        /// <summary>再次校验同一奖励命令，并通过唯一写入口发布已完成节点的稳定快照。</summary>
        internal RunState CommitCardRewardSettlement(
            RunCardRewardId rewardId,
            int? selectedCardTemplateId)
        {
            RunState settled = CreateCardRewardSettlement(rewardId, selectedCardTemplateId);
            Publish(settled);
            return Current;
        }

        /// <summary>在地图稳定态预演指定实例恰好升一级，但不发布任何 Run 业务事实。</summary>
        internal RunState PreviewCardUpgrade(
            RunCardInstanceId instanceId,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            return CreateCardUpgrade(instanceId, catalog);
        }

        /// <summary>再次校验指定实例的下一等级，并通过唯一写入口发布升级后的稳定快照。</summary>
        internal RunState CommitCardUpgrade(
            RunCardInstanceId instanceId,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            RunState upgraded = CreateCardUpgrade(instanceId, catalog);
            Publish(upgraded);
            return Current;
        }

        /// <summary>记录当前普通战斗失败并原子进入不可继续的 Terminal(Defeat)。</summary>
        public RunState RecordDefeat(
            RunBattleId battleId,
            int heroTemplateId,
            int settledHealth,
            int maxHealth)
        {
            RunState state = RequireActiveBattle(battleId);
            ValidateResultPlayer(state, heroTemplateId, settledHealth, maxHealth);
            if (settledHealth != 0)
                throw new InvalidOperationException("A defeat result must settle the Run hero at zero health.");

            Publish(new RunState(
                previous: state,
                currentHealth: 0,
                pathNodeIds: state.PathNodeIds,
                progressPhase: RunProgressPhase.Terminal,
                committedNodeId: state.CommittedNodeId,
                battleAttemptSequence: state.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: RunTerminalReason.Defeat));
            return Current;
        }

        /// <summary>释放 Run 事实的只读视图与唯一可写属性。</summary>
        public void Dispose()
        {
            State.Dispose();
            _state.Dispose();
        }

        /// <summary>通过唯一写入口发布下一份完整 Run 事实。</summary>
        private void Publish(RunState state)
        {
            _state.Value = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>读取已存在的当前 Run；尚未创建时立即拒绝迁移。</summary>
        private RunState RequireCurrent()
        {
            return Current ?? throw new InvalidOperationException("No active run exists.");
        }

        /// <summary>确认结果只结算当前仍处于战斗中的 attempt。</summary>
        private RunState RequireActiveBattle(RunBattleId battleId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.InBattle ||
                state.ActiveBattle == null ||
                state.CommittedNodeId == null ||
                state.ActiveBattle.BattleId != battleId)
            {
                throw new InvalidOperationException("The battle result does not match the active run battle.");
            }

            return state;
        }

        /// <summary>从当前唯一 Pending 纯计算选择或跳过后的 MapReady 后继。</summary>
        private RunState CreateCardRewardSettlement(
            RunCardRewardId rewardId,
            int? selectedCardTemplateId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.RewardPending ||
                state.PendingCardReward == null ||
                state.CommittedNodeId == null)
            {
                throw new InvalidOperationException("The Run does not have a pending card reward.");
            }
            if (state.PendingCardReward.Id != rewardId)
                throw new InvalidOperationException("The card reward id is stale or forged.");
            if (selectedCardTemplateId.HasValue &&
                !state.PendingCardReward.CandidateTemplateIds.Contains(
                    selectedCardTemplateId.Value))
            {
                throw new InvalidOperationException(
                    "The selected card template is not a frozen reward candidate.");
            }

            RunDeck settledDeck = selectedCardTemplateId.HasValue
                ? state.RunDeck.AppendNewInstance(selectedCardTemplateId.Value)
                : state.RunDeck;
            MapNodeId[] settledPath = state.PathNodeIds
                .Concat(new[] { state.CommittedNodeId.Value })
                .ToArray();
            return new RunState(
                state,
                state.CurrentHealth,
                settledPath,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: state.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: settledDeck);
        }

        /// <summary>从当前 MapReady 快照纯计算一次实例升级后继，不引入篝火或其他选择入口。</summary>
        private RunState CreateCardUpgrade(
            RunCardInstanceId instanceId,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            RunState state = RequireCurrent();
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (state.ProgressPhase != RunProgressPhase.MapReady ||
                state.ActiveBattle != null ||
                state.CommittedNodeId != null ||
                state.PendingCardReward != null)
            {
                throw new InvalidOperationException(
                    "Run cards can only be upgraded from a map-stable Run snapshot.");
            }

            RunDeck upgradedDeck = state.RunDeck.UpgradeInstanceOneLevel(
                instanceId,
                catalog.IsCardUpgradeLevelValid);
            return new RunState(
                state,
                state.CurrentHealth,
                state.PathNodeIds,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: state.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: upgradedDeck);
        }

        /// <summary>确认单玩家结果身份、生命与当前 Run 的冻结上限一致。</summary>
        private static void ValidateResultPlayer(
            RunState state,
            int heroTemplateId,
            int health,
            int maxHealth)
        {
            if (heroTemplateId != state.HeroTemplateId)
                throw new InvalidOperationException("The battle result hero does not match the run hero.");
            if (maxHealth != state.MaxHealth)
                throw new InvalidOperationException("The battle result max health does not match the run hero.");
            if (health < 0 || health > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(health));
        }

        /// <summary>以互素步进从 Run 根种子派生正整数空间内不重复的本战种子。</summary>
        internal static uint DeriveBattleSeed(uint randomRootSeed, int attemptSequence)
        {
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (attemptSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(attemptSequence));

            const ulong seedModulus = int.MaxValue;
            const ulong coprimeStep = 1640531527u;
            ulong root = randomRootSeed % seedModulus;
            ulong attemptOffset = (ulong)(attemptSequence - 1) * coprimeStep;
            return (uint)(((root + attemptOffset) % seedModulus) + 1u);
        }
    }
}
