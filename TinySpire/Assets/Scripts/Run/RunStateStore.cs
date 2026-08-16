using System;
using R3;

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
                state.BattleSnapshot != null ||
                (state.NodeStatus != RunNodeStatus.Available &&
                 state.NodeStatus != RunNodeStatus.Completed))
            {
                throw new InvalidOperationException(
                    "Only a map-stable Run can be cleared outside battle.");
            }

            _state.Value = null;
        }

        /// <summary>从唯一可进入节点冻结恢复快照，并签发新的单场战斗输入。</summary>
        public RunBattleInput BeginBattle()
        {
            RunState state = RequireCurrent();
            if (state.NodeStatus != RunNodeStatus.Available ||
                state.ActiveBattle != null ||
                state.BattleSnapshot != null)
            {
                throw new InvalidOperationException("The run battle node is not available.");
            }

            int attemptSequence = checked(state.BattleAttemptSequence + 1);
            var snapshot = new RunBattleSnapshot(state);
            uint battleSeed = DeriveBattleSeed(state.RandomRootSeed, attemptSequence);
            var input = new RunBattleInput(state, attemptSequence, battleSeed);
            Publish(new RunState(
                state,
                state.CurrentHealth,
                RunNodeStatus.InBattle,
                attemptSequence,
                input,
                snapshot));
            return input;
        }

        /// <summary>仅为当前本战尝试原子写回胜利生命，并完成唯一节点。</summary>
        public RunState ApplyVictory(
            RunBattleId battleId,
            int heroTemplateId,
            int settledHealth,
            int maxHealth)
        {
            RunState state = RequireActiveBattle(battleId);
            ValidateResultPlayer(state, heroTemplateId, settledHealth, maxHealth);
            if (settledHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(settledHealth));

            Publish(new RunState(
                state,
                settledHealth,
                RunNodeStatus.Completed,
                state.BattleAttemptSequence,
                activeBattle: null,
                battleSnapshot: null));
            return Current;
        }

        /// <summary>记录当前本战失败，但拒绝把失败战斗的临时生命写入 Run。</summary>
        public RunState RecordDefeat(
            RunBattleId battleId,
            int heroTemplateId,
            int settledHealth,
            int maxHealth)
        {
            RunState state = RequireActiveBattle(battleId);
            ValidateResultPlayer(state, heroTemplateId, settledHealth, maxHealth);
            int restoredHealth = state.BattleSnapshot.CurrentHealth;
            Publish(new RunState(
                state,
                restoredHealth,
                RunNodeStatus.Failed,
                state.BattleAttemptSequence,
                activeBattle: null,
                battleSnapshot: state.BattleSnapshot));
            return Current;
        }

        /// <summary>从失败页恢复进战前 snapshot，并以新的 attempt 签发重开输入。</summary>
        public RunBattleInput RestartBattle()
        {
            RunState failed = RequireCurrent();
            if (failed.NodeStatus != RunNodeStatus.Failed ||
                failed.ActiveBattle != null ||
                failed.BattleSnapshot == null)
            {
                throw new InvalidOperationException("The run does not have a failed battle to restart.");
            }

            RunBattleSnapshot snapshot = failed.BattleSnapshot;
            ValidateSnapshotMatchesRun(failed, snapshot);
            Publish(new RunState(
                failed,
                snapshot.CurrentHealth,
                RunNodeStatus.Available,
                failed.BattleAttemptSequence,
                activeBattle: null,
                battleSnapshot: null));
            return BeginBattle();
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
            if (state.NodeStatus != RunNodeStatus.InBattle ||
                state.ActiveBattle == null ||
                state.BattleSnapshot == null ||
                state.ActiveBattle.BattleId != battleId)
            {
                throw new InvalidOperationException("The battle result does not match the active run battle.");
            }

            return state;
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

        /// <summary>确认恢复 snapshot 与当前 Run 的不可变身份和模板事实一致。</summary>
        private static void ValidateSnapshotMatchesRun(RunState state, RunBattleSnapshot snapshot)
        {
            if (snapshot.RunId != state.RunId ||
                snapshot.HeroTemplateId != state.HeroTemplateId ||
                snapshot.MaxHealth != state.MaxHealth ||
                snapshot.DeckTemplateId != state.DeckTemplateId ||
                snapshot.EncounterTemplateId != state.EncounterTemplateId ||
                snapshot.NodeStatus != RunNodeStatus.Available)
            {
                throw new InvalidOperationException("The battle snapshot does not belong to the current run.");
            }
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
