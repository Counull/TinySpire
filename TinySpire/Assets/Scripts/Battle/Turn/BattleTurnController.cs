using System;
using System.Collections.Generic;
using R3;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>
    /// 命令队列内部持有的回合事实模块。
    /// </summary>
    internal sealed class BattleTurnController : IDisposable
    {
        private enum BattleTurnEvent
        {
            StartBattle
        }

        private readonly Dictionary<CombatantId, PlayerTurnData> _players;
        private readonly ReactiveProperty<BattleTurnData> _turn;
        private readonly StateMachine<BattleTurnEvent> _stateMachine;

        /// <summary>回合事实的只读响应式视图。</summary>
        internal ReadOnlyReactiveProperty<BattleTurnData> Turn { get; }

        /// <summary>以 CombatantId 映射初始化所有玩家的独立回合事实。</summary>
        internal BattleTurnController(IEnumerable<CombatantId> playerIds)
        {
            if (playerIds == null)
                throw new ArgumentNullException(nameof(playerIds));

            _players = new Dictionary<CombatantId, PlayerTurnData>();
            foreach (CombatantId playerId in playerIds)
            {
                if (playerId.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(playerIds));
                if (_players.ContainsKey(playerId))
                    throw new ArgumentException("Player identifiers must be unique.", nameof(playerIds));

                _players.Add(playerId, new PlayerTurnData(energy: 0, hasEndedAction: false));
            }

            if (_players.Count == 0)
                throw new ArgumentException("At least one player is required.", nameof(playerIds));

            _turn = new ReactiveProperty<BattleTurnData>(
                new BattleTurnData(BattleTurnPhase.NotStarted, 0, _players, null));
            Turn = _turn.ToReadOnlyReactiveProperty();
            _stateMachine = new StateMachine<BattleTurnEvent>(new NotStartedState(this));
        }

        /// <summary>仅在未开始阶段执行一次战斗初始化，并自动进入玩家行动阶段。</summary>
        internal bool TryStartBattle()
        {
            if (Turn.CurrentValue.Phase != BattleTurnPhase.NotStarted)
                return false;

            _stateMachine.Dispatch(BattleTurnEvent.StartBattle);
            _stateMachine.Tick(TimeSpan.Zero);
            return true;
        }

        /// <summary>释放回合事实持有的响应式资源。</summary>
        public void Dispose()
        {
            _stateMachine.Stop();
            Turn.Dispose();
            _turn.Dispose();
        }

        /// <summary>进入阶段时发布一份包含全部玩家事实的完整快照。</summary>
        private void EnterPhase(BattleTurnPhase phase)
        {
            int roundNumber = Turn.CurrentValue.RoundNumber;
            if (phase == BattleTurnPhase.PlayerRoundStart)
            {
                roundNumber++;
                ResetPlayersForRound();
            }

            _turn.Value = new BattleTurnData(phase, roundNumber, _players, null);
        }

        /// <summary>为新一轮替换每名玩家的独立回合事实，当前骨架能量保持为零。</summary>
        private void ResetPlayersForRound()
        {
            var playerIds = new List<CombatantId>(_players.Keys);
            foreach (CombatantId playerId in playerIds)
                _players[playerId] = new PlayerTurnData(energy: 0, hasEndedAction: false);
        }

        /// <summary>战斗尚未开始时只接受开始战斗事件。</summary>
        private sealed class NotStartedState : IState<BattleTurnEvent>
        {
            private readonly BattleTurnController _owner;

            /// <summary>保存阶段事实所有者，供事件触发时建立后续阶段。</summary>
            internal NotStartedState(BattleTurnController owner)
            {
                _owner = owner;
            }

            /// <summary>初始快照已由控制器构造，因此进入时无需重复发布。</summary>
            public void Enter()
            {
            }

            /// <summary>开始事件把状态机交给自动阶段状态，其他事件保持不变。</summary>
            public StateTransition<BattleTurnEvent> Handle(BattleTurnEvent @event)
            {
                return @event == BattleTurnEvent.StartBattle
                    ? StateTransition<BattleTurnEvent>.To(
                        new AutomaticPhaseState(_owner, BattleTurnPhase.BattleStart))
                    : StateTransition<BattleTurnEvent>.Stay;
            }

            /// <summary>未收到开始事件时 Tick 不推进战斗。</summary>
            public StateTransition<BattleTurnEvent> Tick(TimeSpan deltaTime)
            {
                return StateTransition<BattleTurnEvent>.Stay;
            }

            /// <summary>离开未开始阶段时无需释放额外资源。</summary>
            public void Exit()
            {
            }
        }

        /// <summary>组合 M4A 自动阶段转换，并在玩家行动阶段稳定停留。</summary>
        private sealed class AutomaticPhaseState : IState<BattleTurnEvent>
        {
            private readonly BattleTurnController _owner;
            private readonly BattleTurnPhase _phase;

            /// <summary>保存将要发布的阶段及其事实所有者。</summary>
            internal AutomaticPhaseState(BattleTurnController owner, BattleTurnPhase phase)
            {
                _owner = owner;
                _phase = phase;
            }

            /// <summary>进入状态时原子发布该阶段的完整回合快照。</summary>
            public void Enter()
            {
                _owner.EnterPhase(_phase);
            }

            /// <summary>M4A 自动阶段不消费额外事件。</summary>
            public StateTransition<BattleTurnEvent> Handle(BattleTurnEvent @event)
            {
                return StateTransition<BattleTurnEvent>.Stay;
            }

            /// <summary>依次跨过开始和玩家轮开始阶段，并在玩家行动阶段停留。</summary>
            public StateTransition<BattleTurnEvent> Tick(TimeSpan deltaTime)
            {
                switch (_phase)
                {
                    case BattleTurnPhase.BattleStart:
                        return StateTransition<BattleTurnEvent>.To(
                            new AutomaticPhaseState(_owner, BattleTurnPhase.PlayerRoundStart));
                    case BattleTurnPhase.PlayerRoundStart:
                        return StateTransition<BattleTurnEvent>.To(
                            new AutomaticPhaseState(_owner, BattleTurnPhase.PlayerAction));
                    default:
                        return StateTransition<BattleTurnEvent>.Stay;
                }
            }

            /// <summary>自动阶段对象不持有额外资源。</summary>
            public void Exit()
            {
            }
        }
    }
}
