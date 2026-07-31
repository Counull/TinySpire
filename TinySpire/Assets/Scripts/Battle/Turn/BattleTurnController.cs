using System;
using System.Collections.Generic;
using cfg;
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
            StartBattle,
            CompletePlayerRound,
            CompleteEnemyAction
        }

        private readonly BattleCombatantsData _combatants;
        private readonly IReadOnlyDictionary<CombatantId, BattleCardZonesData> _playerCardZones;
        private readonly IReadOnlyList<CombatantId> _enemyCombatantIdsInEncounterOrder;
        private readonly Tables _tables;
        private readonly int _energyPerRound;
        private readonly int _initialHandCount;
        private readonly Dictionary<CombatantId, PlayerTurnData> _players;
        private readonly ReactiveProperty<BattleTurnData> _turn;
        private readonly StateMachine<BattleTurnEvent> _stateMachine;

        /// <summary>回合事实的只读响应式视图。</summary>
        internal ReadOnlyReactiveProperty<BattleTurnData> Turn { get; }

        /// <summary>以权威参与者、每玩家卡区、静态卡牌配置和每轮能量初始化回合事实。</summary>
        internal BattleTurnController(
            BattleCombatantsData combatants,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            Tables tables,
            int energyPerRound,
            int initialHandCount)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _playerCardZones = playerCardZones ?? throw new ArgumentNullException(nameof(playerCardZones));
            _enemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder
                ?? throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            if (energyPerRound < 0)
                throw new ArgumentOutOfRangeException(nameof(energyPerRound));
            if (initialHandCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialHandCount));

            _energyPerRound = energyPerRound;
            _initialHandCount = initialHandCount;

            _players = new Dictionary<CombatantId, PlayerTurnData>();
            foreach (CombatantData combatant in _combatants.All.Values)
            {
                if (!(combatant is PlayerCombatantData player))
                    continue;

                _players.Add(player.Id, new PlayerTurnData(energy: 0, hasEndedAction: false));
            }

            if (_players.Count == 0)
                throw new ArgumentException("At least one player is required.", nameof(combatants));

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

        /// <summary>按队首到达时的权威事实校验出牌，成功后移动卡牌并扣除该玩家能量。</summary>
        internal BattleCommandExecutionFailureReason TryPlayCard(PlayCardCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (Turn.CurrentValue.Phase != BattleTurnPhase.PlayerAction)
                return BattleCommandExecutionFailureReason.InvalidTurnPhase;
            if (!_players.TryGetValue(command.ActorId, out PlayerTurnData playerTurn) ||
                !_combatants.TryGet(command.ActorId, out CombatantData combatant) ||
                !(combatant is PlayerCombatantData))
            {
                return BattleCommandExecutionFailureReason.InvalidPlayer;
            }
            if (!combatant.IsAlive)
                return BattleCommandExecutionFailureReason.PlayerNotAlive;
            if (playerTurn.HasEndedAction)
                return BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded;
            if (!_playerCardZones.TryGetValue(command.ActorId, out BattleCardZonesData cardZones) ||
                cardZones == null)
            {
                return BattleCommandExecutionFailureReason.PlayerCardZonesNotFound;
            }
            if (!IsCardInHand(cardZones, command.CardId) ||
                !cardZones.TryGetCard(command.CardId, out CardInstanceData card))
            {
                return BattleCommandExecutionFailureReason.CardNotInHand;
            }

            cfg.battle.Card cardTemplate = _tables.TbCard.GetOrDefault(card.TemplateId);
            if (cardTemplate == null)
                return BattleCommandExecutionFailureReason.CardTemplateNotFound;
            if (cardTemplate.Cost < 0)
                return BattleCommandExecutionFailureReason.CardTemplateNotFound;
            if (playerTurn.Energy < cardTemplate.Cost)
                return BattleCommandExecutionFailureReason.InsufficientEnergy;
            if (!cardZones.DiscardFromHand(command.CardId))
                return BattleCommandExecutionFailureReason.CardNotInHand;

            _players[command.ActorId] = new PlayerTurnData(
                playerTurn.Energy - cardTemplate.Cost,
                playerTurn.HasEndedAction);
            PublishCurrentPhase();
            return BattleCommandExecutionFailureReason.None;
        }

        /// <summary>在玩家行动阶段结束指定存活玩家的行动，并把其剩余手牌全部移入弃牌堆。</summary>
        internal BattleCommandExecutionFailureReason TryEndPlayerAction(EndPlayerActionCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (Turn.CurrentValue.Phase != BattleTurnPhase.PlayerAction)
                return BattleCommandExecutionFailureReason.InvalidTurnPhase;
            if (!_players.TryGetValue(command.ActorId, out PlayerTurnData playerTurn) ||
                !_combatants.TryGet(command.ActorId, out CombatantData combatant) ||
                !(combatant is PlayerCombatantData))
            {
                return BattleCommandExecutionFailureReason.InvalidPlayer;
            }
            if (!combatant.IsAlive)
                return BattleCommandExecutionFailureReason.PlayerNotAlive;
            if (playerTurn.HasEndedAction)
                return BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded;
            if (!_playerCardZones.TryGetValue(command.ActorId, out BattleCardZonesData cardZones) ||
                cardZones == null)
            {
                return BattleCommandExecutionFailureReason.PlayerCardZonesNotFound;
            }

            cardZones.DiscardHand();
            _players[command.ActorId] = new PlayerTurnData(playerTurn.Energy, hasEndedAction: true);
            if (HaveAllLivingPlayersEndedAction())
            {
                _stateMachine.Dispatch(BattleTurnEvent.CompletePlayerRound);
                _stateMachine.Tick(TimeSpan.Zero);
            }
            else
            {
                PublishCurrentPhase();
            }

            return BattleCommandExecutionFailureReason.None;
        }

        /// <summary>只允许当前行动敌人的权威完成命令推进 Encounter 顺序。</summary>
        internal BattleCommandExecutionFailureReason TryCompleteEnemyAction(
            CompleteEnemyActionCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            BattleTurnData current = Turn.CurrentValue;
            if (current.Phase != BattleTurnPhase.EnemyAction)
                return BattleCommandExecutionFailureReason.InvalidTurnPhase;
            if (!_combatants.TryGet(command.EnemyId, out CombatantData combatant) ||
                !(combatant is EnemyCombatantData))
            {
                return BattleCommandExecutionFailureReason.InvalidEnemy;
            }
            if (!current.CurrentActingEnemyId.HasValue ||
                current.CurrentActingEnemyId.Value != command.EnemyId)
            {
                return BattleCommandExecutionFailureReason.EnemyNotCurrentActor;
            }

            _stateMachine.Dispatch(BattleTurnEvent.CompleteEnemyAction);
            _stateMachine.Tick(TimeSpan.Zero);
            return BattleCommandExecutionFailureReason.None;
        }

        /// <summary>释放回合事实持有的响应式资源。</summary>
        public void Dispose()
        {
            _stateMachine.Stop();
            Turn.Dispose();
            _turn.Dispose();
        }

        /// <summary>进入阶段时发布一份包含全部玩家事实的完整快照。</summary>
        private void EnterPhase(BattleTurnPhase phase, CombatantId? currentActingEnemyId = null)
        {
            int roundNumber = Turn.CurrentValue.RoundNumber;
            if (phase == BattleTurnPhase.PlayerRoundStart)
            {
                roundNumber++;
                ResetPlayersForRound();
            }

            _turn.Value = new BattleTurnData(phase, roundNumber, _players, currentActingEnemyId);
        }

        /// <summary>为新一轮替换每名玩家的独立回合事实，并恢复静态规则定义的基础能量。</summary>
        private void ResetPlayersForRound()
        {
            var playerIds = new List<CombatantId>(_players.Keys);
            foreach (CombatantId playerId in playerIds)
            {
                _players[playerId] = new PlayerTurnData(_energyPerRound, hasEndedAction: false);
                DrawPlayerToTargetHand(playerId);
            }
        }

        /// <summary>从指定玩家的权威卡区补抽到静态规则定义的目标手牌数量。</summary>
        private void DrawPlayerToTargetHand(CombatantId playerId)
        {
            if (!_playerCardZones.TryGetValue(playerId, out BattleCardZonesData cardZones) ||
                cardZones == null)
            {
                return;
            }

            int drawCount = Math.Max(0, _initialHandCount - cardZones.Hand.Count);
            if (drawCount > 0)
                cardZones.Draw(drawCount);
        }

        /// <summary>保持阶段和轮次不变，发布包含最新玩家事实的一次完整快照。</summary>
        private void PublishCurrentPhase()
        {
            BattleTurnData current = Turn.CurrentValue;
            _turn.Value = new BattleTurnData(
                current.Phase,
                current.RoundNumber,
                _players,
                current.CurrentActingEnemyId);
        }

        /// <summary>判断所有仍存活的玩家是否都已经执行了本轮结束行动。</summary>
        private bool HaveAllLivingPlayersEndedAction()
        {
            foreach (KeyValuePair<CombatantId, PlayerTurnData> entry in _players)
            {
                if (_combatants.TryGet(entry.Key, out CombatantData combatant) &&
                    combatant.IsAlive &&
                    !entry.Value.HasEndedAction)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>从 Encounter 指定位置起查找下一名存活敌人，并返回稳定的敌人行动状态。</summary>
        private IState<BattleTurnEvent> CreateNextEnemyState(int startIndex)
        {
            for (int index = startIndex; index < _enemyCombatantIdsInEncounterOrder.Count; index++)
            {
                CombatantId enemyId = _enemyCombatantIdsInEncounterOrder[index];
                if (_combatants.TryGet(enemyId, out CombatantData combatant) &&
                    combatant is EnemyCombatantData &&
                    combatant.IsAlive)
                {
                    return new EnemyActionState(this, enemyId, index + 1);
                }
            }

            return new AutomaticPhaseState(this, BattleTurnPhase.EnemyRoundEnd);
        }

        /// <summary>只读取当前手牌快照，判断指定实例是否仍归属于该玩家手牌。</summary>
        private static bool IsCardInHand(BattleCardZonesData cardZones, CardInstanceId cardId)
        {
            foreach (CardInstanceId handCardId in cardZones.Hand)
            {
                if (handCardId == cardId)
                    return true;
            }

            return false;
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

        /// <summary>组合无需外部输入的阶段转换，并在玩家行动阶段稳定停留。</summary>
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

            /// <summary>玩家行动稳定阶段只在全体玩家已经结束后进入玩家轮结束阶段。</summary>
            public StateTransition<BattleTurnEvent> Handle(BattleTurnEvent @event)
            {
                if (_phase == BattleTurnPhase.PlayerAction &&
                    @event == BattleTurnEvent.CompletePlayerRound)
                {
                    return StateTransition<BattleTurnEvent>.To(
                        new AutomaticPhaseState(_owner, BattleTurnPhase.PlayerRoundEnd));
                }

                return StateTransition<BattleTurnEvent>.Stay;
            }

            /// <summary>跨过无需外部输入的阶段，并在玩家或敌人行动阶段停留。</summary>
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
                    case BattleTurnPhase.PlayerRoundEnd:
                        return StateTransition<BattleTurnEvent>.To(
                            new AutomaticPhaseState(_owner, BattleTurnPhase.EnemyRoundStart));
                    case BattleTurnPhase.EnemyRoundStart:
                        return StateTransition<BattleTurnEvent>.To(
                            _owner.CreateNextEnemyState(startIndex: 0));
                    case BattleTurnPhase.EnemyRoundEnd:
                        return StateTransition<BattleTurnEvent>.To(
                            new AutomaticPhaseState(_owner, BattleTurnPhase.RoundEnd));
                    case BattleTurnPhase.RoundEnd:
                        return StateTransition<BattleTurnEvent>.To(
                            new AutomaticPhaseState(_owner, BattleTurnPhase.PlayerRoundStart));
                    default:
                        return StateTransition<BattleTurnEvent>.Stay;
                }
            }

            /// <summary>自动阶段对象不持有额外资源。</summary>
            public void Exit()
            {
            }
        }

        /// <summary>公布一名 Encounter 顺序敌人的稳定行动阶段，等待其完成命令。</summary>
        private sealed class EnemyActionState : IState<BattleTurnEvent>
        {
            private readonly BattleTurnController _owner;
            private readonly CombatantId _enemyId;
            private readonly int _nextEncounterIndex;

            /// <summary>保存当前敌人和后续查找起点，确保完成后不依赖字典枚举。</summary>
            internal EnemyActionState(
                BattleTurnController owner,
                CombatantId enemyId,
                int nextEncounterIndex)
            {
                _owner = owner;
                _enemyId = enemyId;
                _nextEncounterIndex = nextEncounterIndex;
            }

            /// <summary>进入状态时发布唯一的当前行动敌人。</summary>
            public void Enter()
            {
                _owner.EnterPhase(BattleTurnPhase.EnemyAction, _enemyId);
            }

            /// <summary>收到当前敌人完成事件后转交 Encounter 中下一名存活敌人。</summary>
            public StateTransition<BattleTurnEvent> Handle(BattleTurnEvent @event)
            {
                return @event == BattleTurnEvent.CompleteEnemyAction
                    ? StateTransition<BattleTurnEvent>.To(
                        _owner.CreateNextEnemyState(_nextEncounterIndex))
                    : StateTransition<BattleTurnEvent>.Stay;
            }

            /// <summary>敌人行动必须等待权威完成命令，普通 Tick 不自动推进。</summary>
            public StateTransition<BattleTurnEvent> Tick(TimeSpan deltaTime)
            {
                return StateTransition<BattleTurnEvent>.Stay;
            }

            /// <summary>敌人行动状态不持有额外资源。</summary>
            public void Exit()
            {
            }
        }
    }
}
