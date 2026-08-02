using System;
using System.Collections.Generic;
using cfg;
using R3;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>回合内部操作交给权威队列的不可变失败原因与有序结算。</summary>
    internal sealed class BattleTurnOperationResult
    {
        /// <summary>操作失败原因；成功时为 None。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>本次同步操作按发生顺序冻结的记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>复制并冻结回合内部操作结果，失败时强制记录为空。</summary>
        internal BattleTurnOperationResult(
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            var frozen = new List<BattleSettlementRecord>(settlements);
            if (failureReason != BattleCommandExecutionFailureReason.None && frozen.Count > 0)
            {
                throw new ArgumentException("失败的回合操作不能携带结算记录。", nameof(settlements));
            }

            FailureReason = failureReason;
            Settlements = frozen.AsReadOnly();
        }

        /// <summary>创建不含任何写入和记录的明确失败结果。</summary>
        internal static BattleTurnOperationResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            if (failureReason == BattleCommandExecutionFailureReason.None)
                throw new ArgumentOutOfRangeException(nameof(failureReason));

            return new BattleTurnOperationResult(
                failureReason,
                Array.Empty<BattleSettlementRecord>());
        }
    }

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
        private readonly BattleCardPlayRules _cardPlayRules;
        private readonly BattleEffectExecutor _effectExecutor;
        private readonly BattleStatusTiming _statusTiming;
        private readonly BattleTerminalRules _terminalRules;
        private readonly int _energyPerRound;
        private readonly int _initialHandCount;
        private readonly Dictionary<CombatantId, PlayerTurnData> _players;
        private readonly ReactiveProperty<BattleTurnData> _turn;
        private readonly StateMachine<BattleTurnEvent> _stateMachine;
        private List<BattleSettlementRecord> _activeSettlements;
        private int _activeSettlementStartingOrder;

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
            _cardPlayRules = new BattleCardPlayRules(
                _combatants,
                _playerCardZones,
                _enemyCombatantIdsInEncounterOrder,
                _tables);
            _effectExecutor = new BattleEffectExecutor(_tables, _combatants);
            _statusTiming = new BattleStatusTiming(_combatants);
            _terminalRules = new BattleTerminalRules(_combatants);

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
        internal BattleTurnOperationResult TryStartBattle()
        {
            if (Turn.CurrentValue.Phase != BattleTurnPhase.NotStarted)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.BattleAlreadyStarted);
            }

            return CollectSuccessfulOperation(() =>
            {
                _stateMachine.Dispatch(BattleTurnEvent.StartBattle);
                _stateMachine.Tick(TimeSpan.Zero);
            });
        }

        /// <summary>按队首事实预构建全部 Effect，再依次支付、执行和把当前卡牌移入弃牌堆。</summary>
        internal BattleTurnOperationResult TryPlayCard(PlayCardCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            BattleCardPlayEvaluation evaluation = _cardPlayRules.Evaluate(
                Turn.CurrentValue,
                command);
            if (!evaluation.Succeeded)
                return BattleTurnOperationResult.Failed(evaluation.FailureReason);

            PlayerTurnData playerTurn = _players[command.ActorId];
            BattleCardZonesData cardZones = _playerCardZones[command.ActorId];
            CardInstanceData card = cardZones.Cards[command.CardId];
            cfg.battle.Card cardTemplate = _tables.TbCard.GetOrDefault(card.TemplateId);
            if (!TryCreateOrderedCardEffectIds(cardTemplate.EffectBindings, out IReadOnlyList<BattleEffectId> effectIds))
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.InvalidEffectBinding);
            }

            var effectRequest = new BattleEffectExecutionRequest(
                command.ActorId,
                command.TargetId.Value,
                effectIds);
            BattleEffectPreparationResult preparation = _effectExecutor.Prepare(effectRequest);
            if (!preparation.Succeeded)
                return BattleTurnOperationResult.Failed(preparation.FailureReason);

            if (preparation.Plan.Operations.Count > int.MaxValue - 2)
            {
                throw new InvalidOperationException("出牌结算记录数量超出 Int32 可表达范围。");
            }

            _effectExecutor.ValidatePreparedExecution(
                preparation.Plan,
                startingOrder: 1);

            var settlements = new List<BattleSettlementRecord>(
                preparation.Plan.Operations.Count + 2);
            int energyAfter = playerTurn.Energy - cardTemplate.Cost;
            settlements.Add(new BattleEnergySpentSettlement(
                0,
                command.ActorId,
                playerTurn.Energy,
                energyAfter));

            _players[command.ActorId] = new PlayerTurnData(
                energyAfter,
                playerTurn.HasEndedAction);

            BattleEffectExecutionResult effectResult =
                _effectExecutor.CommitPrepared(preparation.Plan);
            settlements.AddRange(effectResult.Settlements);

            BattleCardZoneOperationResult discardResult = cardZones.DiscardFromHand(
                command.CardId,
                startingOrder: settlements.Count);
            if (!discardResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Effect 执行完成后当前卡牌意外离开手牌。");
            }

            settlements.AddRange(discardResult.Settlements);
            BattleTerminalOutcome terminalOutcome = _terminalRules.Evaluate();
            if (terminalOutcome == BattleTerminalOutcome.Ongoing)
            {
                PublishCurrentPhase();
            }
            else if (terminalOutcome == BattleTerminalOutcome.Victory ||
                     terminalOutcome == BattleTerminalOutcome.Defeat)
            {
                EnterBattleEnded();
            }
            else
            {
                throw new InvalidOperationException("出牌事务完成后派生出无效的双方阵营事实。");
            }

            return new BattleTurnOperationResult(
                BattleCommandExecutionFailureReason.None,
                settlements);
        }

        /// <summary>只在 Card 边缘按配置原序把绑定适配为核心认识的强类型 Effect 标识。</summary>
        private static bool TryCreateOrderedCardEffectIds(
            IEnumerable<cfg.battle.CardEffectBinding> bindings,
            out IReadOnlyList<BattleEffectId> effectIds)
        {
            var orderedEffectIds = new List<BattleEffectId>();
            if (bindings == null)
            {
                effectIds = Array.Empty<BattleEffectId>();
                return false;
            }

            foreach (cfg.battle.CardEffectBinding binding in bindings)
            {
                if (binding == null || binding.EffectId <= 0)
                {
                    effectIds = Array.Empty<BattleEffectId>();
                    return false;
                }

                orderedEffectIds.Add(new BattleEffectId(binding.EffectId));
            }

            effectIds = orderedEffectIds.AsReadOnly();
            return true;
        }

        /// <summary>在玩家行动阶段结束指定存活玩家的行动，并把其剩余手牌全部移入弃牌堆。</summary>
        internal BattleTurnOperationResult TryEndPlayerAction(EndPlayerActionCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (Turn.CurrentValue.Phase != BattleTurnPhase.PlayerAction)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.InvalidTurnPhase);
            }
            if (!_players.TryGetValue(command.ActorId, out PlayerTurnData playerTurn) ||
                !_combatants.TryGet(command.ActorId, out CombatantData combatant) ||
                !(combatant is PlayerCombatantData))
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.InvalidPlayer);
            }
            if (!combatant.IsAlive)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerNotAlive);
            }
            if (playerTurn.HasEndedAction)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded);
            }
            if (!_playerCardZones.TryGetValue(command.ActorId, out BattleCardZonesData cardZones) ||
                cardZones == null)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerCardZonesNotFound);
            }

            BattleStatusTimingPreparationResult statusPreparation = _statusTiming.Prepare(
                BattleStatusTimingPoint.PlayerActionEnded,
                command.ActorId,
                cardZones.Hand.Count);
            if (!statusPreparation.Succeeded)
                return BattleTurnOperationResult.Failed(statusPreparation.FailureReason);
            if (!_statusTiming.ValidatePrepared(statusPreparation.Plan))
                throw new InvalidOperationException("玩家行动结束状态计划在首次写入前发生快照漂移。");

            BattleTerminalOutcome terminalOutcome = _terminalRules.Evaluate();
            if (terminalOutcome == BattleTerminalOutcome.InvalidFacts)
                throw new InvalidOperationException("玩家行动结束前派生出无效的双方阵营事实。");

            return CollectSuccessfulOperation(() =>
            {
                AppendCardZoneResult(cardZones.DiscardHand(CurrentSettlementOrder));
                AppendStatusTimingResult(_statusTiming.CommitPrepared(statusPreparation.Plan));
                _players[command.ActorId] = new PlayerTurnData(
                    playerTurn.Energy,
                    hasEndedAction: true);
                if (terminalOutcome == BattleTerminalOutcome.Victory ||
                    terminalOutcome == BattleTerminalOutcome.Defeat)
                {
                    EnterBattleEnded();
                }
                else if (HaveAllLivingPlayersEndedAction())
                {
                    _stateMachine.Dispatch(BattleTurnEvent.CompletePlayerRound);
                    _stateMachine.Tick(TimeSpan.Zero);
                }
                else
                {
                    PublishCurrentPhase();
                }
            });
        }

        /// <summary>只读取权威阶段与身份，校验敌人完成命令是否可以进入写入链。</summary>
        internal BattleCommandExecutionFailureReason ValidateCompleteEnemyAction(
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

            return BattleCommandExecutionFailureReason.None;
        }

        /// <summary>只读预览当前敌人之后 Encounter 中下一名存活敌人的 Queue continuation。</summary>
        internal CompleteEnemyActionCommand CreateNextEnemyContinuation()
        {
            BattleTurnData current = Turn.CurrentValue;
            if (current.Phase != BattleTurnPhase.EnemyAction ||
                !current.CurrentActingEnemyId.HasValue)
            {
                return null;
            }
            if (!(_stateMachine.CurrentState is EnemyActionState enemyState))
                throw new InvalidOperationException("敌人行动阶段缺少对应的 Encounter 状态。");

            return TryFindNextLivingEnemy(
                enemyState.NextEncounterIndex,
                out CombatantId enemyId,
                out _)
                    ? new CompleteEnemyActionCommand(enemyId)
                    : null;
        }

        /// <summary>在敌人事务提交后进入终局，或从指定记录序号继续推进 Encounter 与下一轮。</summary>
        internal BattleTurnOperationResult AdvanceAfterValidatedEnemyAction(
            BattleTerminalOutcome terminalOutcome,
            int startingOrder)
        {
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (terminalOutcome == BattleTerminalOutcome.InvalidFacts)
                throw new InvalidOperationException("敌人事务完成后派生出无效的双方阵营事实。");
            if (!Enum.IsDefined(typeof(BattleTerminalOutcome), terminalOutcome))
                throw new ArgumentOutOfRangeException(nameof(terminalOutcome));

            return CollectSuccessfulOperation(startingOrder, () =>
            {
                if (terminalOutcome == BattleTerminalOutcome.Victory ||
                    terminalOutcome == BattleTerminalOutcome.Defeat)
                {
                    EnterBattleEnded();
                    return;
                }

                _stateMachine.Dispatch(BattleTurnEvent.CompleteEnemyAction);
                _stateMachine.Tick(TimeSpan.Zero);
            });
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

        /// <summary>为每名存活玩家依次清 Block、恢复变化的能量并补抽手牌。</summary>
        private void ResetPlayersForRound()
        {
            var playerIds = new List<CombatantId>(_players.Keys);
            playerIds.Sort((left, right) => left.Value.CompareTo(right.Value));
            foreach (CombatantId playerId in playerIds)
            {
                if (!_combatants.TryGet(playerId, out CombatantData combatant) ||
                    !(combatant is PlayerCombatantData) ||
                    !combatant.IsAlive)
                {
                    continue;
                }

                BattleStatusTimingResult blockResult = _statusTiming.Execute(
                    BattleStatusTimingPoint.PlayerRoundStart,
                    playerId,
                    CurrentSettlementOrder);
                if (!blockResult.Succeeded)
                    throw new InvalidOperationException("存活玩家的回合开始状态时机意外失败。");
                AppendStatusTimingResult(blockResult);

                PlayerTurnData playerTurn = _players[playerId];
                if (playerTurn.Energy != _energyPerRound)
                {
                    _activeSettlements.Add(new BattleEnergyRefilledSettlement(
                        CurrentSettlementOrder,
                        playerId,
                        playerTurn.Energy,
                        _energyPerRound));
                }

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
            {
                AppendCardZoneResult(cardZones.Draw(
                    drawCount,
                    CurrentSettlementOrder));
            }
        }

        /// <summary>返回当前命令下一条结算记录的连续顺序；仅允许在同步收集作用域内读取。</summary>
        private int CurrentSettlementOrder
        {
            get
            {
                if (_activeSettlements == null)
                {
                    throw new InvalidOperationException(
                        "当前没有可接收阶段卡区变化的命令结算作用域。");
                }

                long order = (long)_activeSettlementStartingOrder + _activeSettlements.Count;
                if (order > int.MaxValue)
                    throw new InvalidOperationException("当前命令结算记录顺序超出 Int32 范围。");

                return (int)order;
            }
        }

        /// <summary>在一次同步命令内收集阶段推进产生的全部卡区记录，并在返回前冻结结果。</summary>
        private BattleTurnOperationResult CollectSuccessfulOperation(Action operation)
        {
            return CollectSuccessfulOperation(0, operation);
        }

        /// <summary>从指定序号开始收集阶段推进记录，并保持与前序事务记录连续。</summary>
        private BattleTurnOperationResult CollectSuccessfulOperation(
            int startingOrder,
            Action operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (_activeSettlements != null)
            {
                throw new InvalidOperationException(
                    "回合命令结算作用域不能嵌套。");
            }

            var settlements = new List<BattleSettlementRecord>();
            _activeSettlements = settlements;
            _activeSettlementStartingOrder = startingOrder;
            try
            {
                operation();
                return new BattleTurnOperationResult(
                    BattleCommandExecutionFailureReason.None,
                    settlements);
            }
            finally
            {
                _activeSettlements = null;
                _activeSettlementStartingOrder = 0;
            }
        }

        /// <summary>校验卡区操作成功且顺序连续后，将其记录追加到当前命令。</summary>
        private void AppendCardZoneResult(BattleCardZoneOperationResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (_activeSettlements == null)
            {
                throw new InvalidOperationException(
                    "当前没有可追加卡区记录的命令结算作用域。");
            }
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "已完成前置校验的阶段卡区操作意外失败。");
            }

            foreach (BattleSettlementRecord settlement in result.Settlements)
            {
                if (settlement.Order != CurrentSettlementOrder)
                {
                    throw new InvalidOperationException(
                        "卡区结算记录必须与当前命令保持连续顺序。");
                }

                _activeSettlements.Add(settlement);
            }
        }

        /// <summary>校验状态时机记录连续后，将其追加到当前命令结算。</summary>
        private void AppendStatusTimingResult(BattleStatusTimingResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (_activeSettlements == null)
                throw new InvalidOperationException("当前没有可追加状态记录的命令结算作用域。");
            if (!result.Succeeded)
                throw new InvalidOperationException("已完成前置校验的状态时机意外失败。");

            foreach (BattleSettlementRecord settlement in result.Settlements)
            {
                if (settlement.Order != CurrentSettlementOrder)
                    throw new InvalidOperationException("状态结算记录必须与当前命令保持连续顺序。");

                _activeSettlements.Add(settlement);
            }
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

        /// <summary>停止内部状态机并发布不保存胜负镜像的中立终局阶段。</summary>
        private void EnterBattleEnded()
        {
            if (_stateMachine.IsRunning)
                _stateMachine.Stop();

            BattleTurnData current = Turn.CurrentValue;
            _turn.Value = new BattleTurnData(
                BattleTurnPhase.BattleEnded,
                current.RoundNumber,
                _players,
                currentActingEnemyId: null);
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
            if (TryFindNextLivingEnemy(startIndex, out CombatantId enemyId, out int nextIndex))
                return new EnemyActionState(this, enemyId, nextIndex);

            return new AutomaticPhaseState(this, BattleTurnPhase.EnemyRoundEnd);
        }

        /// <summary>从 Encounter 指定游标起只读查找下一名存活敌人及其后继游标。</summary>
        private bool TryFindNextLivingEnemy(
            int startIndex,
            out CombatantId enemyId,
            out int nextIndex)
        {
            if (startIndex < 0 || startIndex > _enemyCombatantIdsInEncounterOrder.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            for (int index = startIndex; index < _enemyCombatantIdsInEncounterOrder.Count; index++)
            {
                CombatantId candidateId = _enemyCombatantIdsInEncounterOrder[index];
                if (_combatants.TryGet(candidateId, out CombatantData combatant) &&
                    combatant is EnemyCombatantData &&
                    combatant.IsAlive)
                {
                    enemyId = candidateId;
                    nextIndex = index + 1;
                    return true;
                }
            }

            enemyId = default;
            nextIndex = _enemyCombatantIdsInEncounterOrder.Count;
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

            /// <summary>下一次 Encounter 查找必须沿用的稳定游标。</summary>
            internal int NextEncounterIndex => _nextEncounterIndex;

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
