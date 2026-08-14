using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        /// <summary>成功后必须由 Queue 续接结束行动的玩家；没有强制续延时为空。</summary>
        internal CombatantId? RequiredEndPlayerActionActorId { get; }

        /// <summary>成功后由 Queue 在表现屏障后续接的冻结免费出牌请求。</summary>
        internal BattleTriggeredCardPlayRequest TriggeredCardPlayRequest { get; }

        /// <summary>复制并冻结回合内部操作结果，失败时强制记录为空。</summary>
        internal BattleTurnOperationResult(
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements,
            CombatantId? requiredEndPlayerActionActorId = null,
            BattleTriggeredCardPlayRequest triggeredCardPlayRequest = null)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            var frozen = new List<BattleSettlementRecord>(settlements);
            if (failureReason != BattleCommandExecutionFailureReason.None && frozen.Count > 0)
            {
                throw new ArgumentException("失败的回合操作不能携带结算记录。", nameof(settlements));
            }
            if (failureReason != BattleCommandExecutionFailureReason.None &&
                requiredEndPlayerActionActorId.HasValue)
            {
                throw new ArgumentException("失败的回合操作不能请求结束玩家行动。", nameof(requiredEndPlayerActionActorId));
            }
            if (failureReason != BattleCommandExecutionFailureReason.None &&
                triggeredCardPlayRequest != null)
            {
                throw new ArgumentException(
                    "失败的回合操作不能请求触发出牌。",
                    nameof(triggeredCardPlayRequest));
            }
            if (requiredEndPlayerActionActorId.HasValue && triggeredCardPlayRequest != null)
            {
                throw new ArgumentException(
                    "同一次回合操作不能同时请求结束行动和触发出牌。",
                    nameof(triggeredCardPlayRequest));
            }

            FailureReason = failureReason;
            Settlements = frozen.AsReadOnly();
            RequiredEndPlayerActionActorId = requiredEndPlayerActionActorId;
            TriggeredCardPlayRequest = triggeredCardPlayRequest;
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
        /// <summary>所有职业共享的首回合固有牌手牌硬上限。</summary>
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
        private readonly BattleCardEffectSequenceExecutor _cardEffectSequenceExecutor;
        private readonly BattleTriggeredCardPlayExecution _triggeredCardPlayExecution;
        private readonly BattleSettlementTriggerEngine _settlementTriggerEngine;
        private readonly GameRandom _cardTargetRandom;
        private readonly BattleRepeatedDamageExecutor _repeatedDamageExecutor;
        private readonly BattleRepeatedDamageEffectAdapter _repeatedDamageEffectAdapter;
        private readonly BattleBlockRetention _blockRetention;
        private readonly BattleStatusTiming _statusTiming;
        private readonly BattlePoisonApplication _poisonApplication;
        private readonly BattleTerminalRules _terminalRules;
        private readonly IReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile> _playerResourceProfiles;
        private readonly int _initialHandCount;
        private readonly MachineGunnerBattleRuntime _machineGunnerRuntime;
        private CombatantId? _requiredEndPlayerActionActorId;
        private readonly Dictionary<CombatantId, PlayerTurnData> _players;
        private readonly ReactiveProperty<BattleTurnData> _turn;
        private readonly StateMachine<BattleTurnEvent> _stateMachine;
        private IReadOnlyDictionary<CombatantId, BattlePreparedOpeningHand> _preparedOpeningHands;
        private int? _pendingBattleEndRoundNumber;
        private List<BattleSettlementRecord> _activeSettlements;
        private int _activeSettlementStartingOrder;

        /// <summary>回合事实的只读响应式视图。</summary>
        internal ReadOnlyReactiveProperty<BattleTurnData> Turn { get; }

        /// <summary>公开权威执行与交互读取共用的唯一出牌规则实例。</summary>
        internal BattleCardPlayRules CardPlayRules => _cardPlayRules;

        /// <summary>只读返回由本 Turn 独占推进的通用卡牌目标随机状态。</summary>
        internal uint CardTargetRandomState => _cardTargetRandom.State;

        /// <summary>以权威参与者、每玩家卡区、静态卡牌配置和 Hero 资源档案初始化回合事实。</summary>
        internal BattleTurnController(
            BattleCombatantsData combatants,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            Tables tables,
            IReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile> playerResourceProfiles,
            int initialHandCount,
            MachineGunnerBattleRuntime machineGunnerRuntime = null,
            uint cardTargetRandomSeed = 1u,
            BattleSettlementTriggerEngine settlementTriggerEngine = null)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _playerCardZones = playerCardZones ?? throw new ArgumentNullException(nameof(playerCardZones));
            _enemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder
                ?? throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            if (playerResourceProfiles == null)
                throw new ArgumentNullException(nameof(playerResourceProfiles));
            if (initialHandCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialHandCount));
            if (cardTargetRandomSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(cardTargetRandomSeed));

            _initialHandCount = initialHandCount;
            _machineGunnerRuntime = machineGunnerRuntime;
            _cardPlayRules = new BattleCardPlayRules(
                _combatants,
                _playerCardZones,
                _enemyCombatantIdsInEncounterOrder,
                _tables,
                _machineGunnerRuntime);
            _blockRetention = new BattleBlockRetention();
            _settlementTriggerEngine = settlementTriggerEngine ??
                new BattleSettlementTriggerEngine(
                    _combatants,
                    _enemyCombatantIdsInEncounterOrder,
                    new GameRandom(cardTargetRandomSeed),
                    _tables,
                    _playerCardZones);
            _effectExecutor = new BattleEffectExecutor(
                _tables,
                _combatants,
                _machineGunnerRuntime?.DamageFormulaOverride,
                _blockRetention,
                _settlementTriggerEngine);
            _triggeredCardPlayExecution = new BattleTriggeredCardPlayExecution(_tables);
            _cardEffectSequenceExecutor = new BattleCardEffectSequenceExecutor(
                _tables,
                _effectExecutor,
                _triggeredCardPlayExecution);
            _cardTargetRandom = new GameRandom(cardTargetRandomSeed);
            _repeatedDamageExecutor = new BattleRepeatedDamageExecutor(
                _combatants,
                _enemyCombatantIdsInEncounterOrder,
                _cardTargetRandom,
                _machineGunnerRuntime?.DamageFormulaOverride);
            _repeatedDamageEffectAdapter = new BattleRepeatedDamageEffectAdapter(
                _tables,
                _repeatedDamageExecutor);
            _statusTiming = new BattleStatusTiming(_combatants, _blockRetention);
            _poisonApplication = new BattlePoisonApplication(_combatants);
            _terminalRules = new BattleTerminalRules(_combatants);

            var profiles = new Dictionary<CombatantId, BattlePlayerResourceProfile>();
            _players = new Dictionary<CombatantId, PlayerTurnData>();
            foreach (CombatantData combatant in _combatants.All.Values)
            {
                if (!(combatant is PlayerCombatantData player))
                    continue;

                if (!playerResourceProfiles.TryGetValue(player.Id, out BattlePlayerResourceProfile profile) ||
                    profile == null)
                {
                    throw new ArgumentException(
                        $"Player {player.Id} has no resource profile.",
                        nameof(playerResourceProfiles));
                }

                profiles.Add(player.Id, profile);
                _players.Add(player.Id, profile.CreateInitialTurnData());
            }

            if (_players.Count == 0)
                throw new ArgumentException("At least one player is required.", nameof(combatants));

            _playerResourceProfiles = new ReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile>(profiles);

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

            if (!TryPrepareOpeningHands(
                    out IReadOnlyDictionary<CombatantId, BattlePreparedOpeningHand> openingHands))
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.InvalidOpeningHandConfiguration);
            }

            return CollectSuccessfulOperation(() =>
            {
                _preparedOpeningHands = openingHands;
                try
                {
                    _stateMachine.Dispatch(BattleTurnEvent.StartBattle);
                    TickAutomaticPhasesAndFinalizePendingBattleEnd();
                }
                finally
                {
                    _preparedOpeningHands = null;
                }
            });
        }

        /// <summary>按队首事实预构建全部 Effect，再依次支付、执行和把当前卡牌移入配置归宿。</summary>
        internal BattleTurnOperationResult TryPlayCard(
            PlayCardCommand command,
            bool isSystemContinuation = false)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (command.IsTriggeredPlay)
            {
                return isSystemContinuation
                    ? TryPlayTriggeredCard(command)
                    : BattleTurnOperationResult.Failed(
                        BattleCommandExecutionFailureReason.UnsupportedCommand);
            }
            if (isSystemContinuation)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.UnsupportedCommand);
            }
            if (IsPlayerActionEndRequired(command.ActorId))
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded);
            }

            BattleCardPlayEvaluation evaluation = _cardPlayRules.Evaluate(
                Turn.CurrentValue,
                command);
            if (!evaluation.Succeeded)
                return BattleTurnOperationResult.Failed(evaluation.FailureReason);

            PlayerTurnData playerTurn = _players[command.ActorId];
            BattleCardZonesData cardZones = _playerCardZones[command.ActorId];
            CardInstanceData card = cardZones.Cards[command.CardId];
            cfg.battle.Card cardTemplate = _tables.TbCard.GetOrDefault(card.TemplateId);
            if (cardTemplate.ProgramId != cfg.battle.MachineGunnerProgramId.None)
            {
                return TryPlayMachineGunnerCard(
                    command,
                    playerTurn,
                    cardZones,
                    card,
                    cardTemplate);
            }

            BattleCardZone playedCardDestination;
            switch (cardTemplate.PlayDestination)
            {
                case cfg.battle.CardPlayDestination.DiscardPile:
                    playedCardDestination = BattleCardZone.DiscardPile;
                    break;
                case cfg.battle.CardPlayDestination.ExhaustPile:
                    playedCardDestination = BattleCardZone.ExhaustPile;
                    break;
                case cfg.battle.CardPlayDestination.Power:
                    playedCardDestination = BattleCardZone.PowerPile;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"当前出牌归宿尚不支持：{cardTemplate.PlayDestination}。");
            }

            int energySpent = cardTemplate.Cost;
            if (cardTemplate.CostKind == cfg.battle.CardCostKind.Fixed)
            {
                if (!BattleCardCostResolver.TryResolveEnergy(
                        cardTemplate.CostKind,
                        cardTemplate.Cost,
                        playerTurn.Energy,
                        BattleCardPaymentMode.Normal,
                        out BattleCardEnergyCostResolution energyCost,
                        out _))
                {
                    throw new InvalidOperationException(
                        "已通过规则校验的固定费用卡无法再次解析相同能量费用。");
                }

                energySpent = energyCost.ActualEnergySpent;
            }

            BattlePreparedCardEffectSequence ordinaryEffectPlan = null;
            BattleRepeatedDamagePreparationResult repeatedDamagePreparation = null;
            int plannedEffectSettlementCount;
            if (cardTemplate.TargetRule == cfg.battle.TargetRule.RandomEnemy)
            {
                repeatedDamagePreparation = _repeatedDamageEffectAdapter.Prepare(
                    cardTemplate.EffectBindings,
                    command.ActorId,
                    BattleRepeatedDamageTargetPolicy.RandomLivingEnemyPerHit,
                    fixedTargetId: null);
                if (!repeatedDamagePreparation.Succeeded)
                {
                    return BattleTurnOperationResult.Failed(
                        repeatedDamagePreparation.FailureReason);
                }

                plannedEffectSettlementCount =
                    repeatedDamagePreparation.Plan.PlannedSettlementCount;
            }
            else
            {
                BattleCardEffectSequencePreparationResult preparation =
                    _cardEffectSequenceExecutor.Prepare(
                    cardTemplate.EffectBindings,
                    command.ActorId,
                    command.TargetId.Value,
                    cardZones,
                    command.CardId,
                    playedCardDestination,
                    command.SelectedCardIds,
                    startingOrder: 1,
                    triggeredPlayDepth: 0);
                if (!preparation.Succeeded)
                    return BattleTurnOperationResult.Failed(preparation.FailureReason);

                ordinaryEffectPlan = preparation.Plan;
                plannedEffectSettlementCount = ordinaryEffectPlan.PlannedSettlementCount;
            }

            if (plannedEffectSettlementCount > int.MaxValue - 2)
            {
                throw new InvalidOperationException("出牌结算记录数量超出 Int32 可表达范围。");
            }

            if (repeatedDamagePreparation != null)
            {
                _repeatedDamageExecutor.ValidatePrepared(
                    repeatedDamagePreparation.Plan,
                    startingOrder: 1);
            }
            else
            {
                _cardEffectSequenceExecutor.ValidatePrepared(ordinaryEffectPlan);
            }

            var settlements = new List<BattleSettlementRecord>(
                plannedEffectSettlementCount + 2);
            int energyAfter = playerTurn.Energy - energySpent;
            settlements.Add(new BattleEnergySpentSettlement(
                0,
                command.ActorId,
                playerTurn.Energy,
                energyAfter));

            _players[command.ActorId] = playerTurn.WithEnergy(energyAfter);

            settlements.AddRange(
                repeatedDamagePreparation != null
                    ? _repeatedDamageExecutor.CommitPrepared(repeatedDamagePreparation.Plan)
                    : _cardEffectSequenceExecutor.CommitPrepared(ordinaryEffectPlan));

            if (ordinaryEffectPlan == null || !ordinaryEffectPlan.CommitsPlayedCardDeparture)
            {
                BattleCardZoneOperationResult destinationResult;
                switch (playedCardDestination)
                {
                    case BattleCardZone.DiscardPile:
                        destinationResult = cardZones.DiscardFromHand(
                            command.CardId,
                            startingOrder: settlements.Count);
                        break;
                    case BattleCardZone.ExhaustPile:
                        destinationResult = cardZones.ExhaustFromHand(
                            command.CardId,
                            startingOrder: settlements.Count);
                        break;
                    case BattleCardZone.PowerPile:
                        destinationResult = cardZones.MoveToPowerFromHand(
                            command.CardId,
                            startingOrder: settlements.Count);
                        break;
                    default:
                        throw new InvalidOperationException("普通卡牌结算出现未支持的权威归宿。");
                }
                if (!destinationResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Effect 执行完成后当前卡牌意外离开手牌。");
                }

                settlements.AddRange(destinationResult.Settlements);
            }
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
                settlements,
                triggeredCardPlayRequest: ordinaryEffectPlan?.TriggeredCardPlayRequest);
        }

        /// <summary>执行 Queue 签发的顶牌免费出牌；首次写入前联合冻结效果、来源卡区和强制归宿。</summary>
        private BattleTurnOperationResult TryPlayTriggeredCard(PlayCardCommand command)
        {
            BattleTriggeredCardPlayRequest request = command.TriggeredPlayRequest;
            BattleTurnData turn = Turn.CurrentValue;
            if (turn.Phase != BattleTurnPhase.PlayerAction)
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.InvalidTurnPhase);
            if (!_combatants.TryGet(command.ActorId, out CombatantData combatant) ||
                !(combatant is PlayerCombatantData player))
            {
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.InvalidPlayer);
            }
            if (!player.IsAlive)
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.PlayerNotAlive);
            if (IsPlayerActionEndRequired(command.ActorId) ||
                !_players.TryGetValue(command.ActorId, out PlayerTurnData playerTurn) ||
                playerTurn.HasEndedAction)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded);
            }
            if (!_playerCardZones.TryGetValue(command.ActorId, out BattleCardZonesData cardZones))
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerCardZonesNotFound);
            }
            if (!ContainsTriggeredSourceCard(cardZones, request) ||
                !cardZones.TryGetCard(command.CardId, out CardInstanceData card))
            {
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.CardNotInHand);
            }

            cfg.battle.Card cardTemplate = _tables.TbCard.GetOrDefault(card.TemplateId);
            if (cardTemplate == null)
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.CardTemplateNotFound);
            if (cardTemplate.ImplementationStatus != cfg.battle.CardImplementationStatus.Implemented)
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.CardNotImplemented);
            if (cardTemplate.ProgramId != cfg.battle.MachineGunnerProgramId.None)
            {
                if (request.SourceZone != BattleCardZone.Hand)
                {
                    return BattleTurnOperationResult.Failed(
                        BattleCommandExecutionFailureReason.UnsupportedMachineGunnerProgram);
                }

                return TryPlayMachineGunnerCard(
                    command,
                    playerTurn,
                    cardZones,
                    card,
                    cardTemplate,
                    request);
            }
            if (!ValidateTriggeredTarget(command, cardTemplate))
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.TargetRuleMismatch);
            if (!BattleCardCostResolver.TryResolveEnergy(
                    cardTemplate.CostKind,
                    cardTemplate.Cost,
                    playerTurn.Energy,
                    request.PaymentMode,
                    out BattleCardEnergyCostResolution energyCost,
                    out BattleCommandExecutionFailureReason costFailureReason))
            {
                return BattleTurnOperationResult.Failed(costFailureReason);
            }

            BattlePreparedCardEffectSequence ordinaryEffectPlan = null;
            BattleRepeatedDamagePreparationResult repeatedDamagePreparation = null;
            int plannedEffectSettlementCount;
            if (cardTemplate.TargetRule == cfg.battle.TargetRule.RandomEnemy)
            {
                repeatedDamagePreparation = _repeatedDamageEffectAdapter.Prepare(
                    cardTemplate.EffectBindings,
                    command.ActorId,
                    BattleRepeatedDamageTargetPolicy.RandomLivingEnemyPerHit,
                    fixedTargetId: null);
                if (!repeatedDamagePreparation.Succeeded)
                    return BattleTurnOperationResult.Failed(repeatedDamagePreparation.FailureReason);
                plannedEffectSettlementCount = repeatedDamagePreparation.Plan.PlannedSettlementCount;
            }
            else
            {
                BattleCardEffectSequencePreparationResult preparation =
                    _cardEffectSequenceExecutor.Prepare(
                        cardTemplate.EffectBindings,
                        command.ActorId,
                        command.TargetId.Value,
                        cardZones,
                        command.CardId,
                        request.Destination,
                        command.SelectedCardIds,
                        startingOrder: 1,
                        triggeredPlayDepth: request.Depth);
                if (!preparation.Succeeded)
                    return BattleTurnOperationResult.Failed(preparation.FailureReason);
                ordinaryEffectPlan = preparation.Plan;
                if (ordinaryEffectPlan.CommitsPlayedCardDeparture || ordinaryEffectPlan.Draw != null)
                {
                    return BattleTurnOperationResult.Failed(
                        BattleCommandExecutionFailureReason.InvalidEffectBinding);
                }
                plannedEffectSettlementCount = ordinaryEffectPlan.PlannedSettlementCount;
            }

            BattlePreparedPlayedCardDeparture departurePlan;
            try
            {
                departurePlan = cardZones.PreparePlayedCardDeparture(
                    command.CardId,
                    request.SourceZone,
                    request.Destination);
            }
            catch (InvalidOperationException)
            {
                return BattleTurnOperationResult.Failed(BattleCommandExecutionFailureReason.CardNotInHand);
            }

            if (repeatedDamagePreparation != null)
            {
                _repeatedDamageExecutor.ValidatePrepared(
                    repeatedDamagePreparation.Plan,
                    startingOrder: 1);
            }
            else
            {
                _cardEffectSequenceExecutor.ValidatePrepared(ordinaryEffectPlan);
            }
            if (!cardZones.ValidatePreparedPlayedCardDeparture(departurePlan))
                throw new InvalidOperationException("触发出牌离场计划在首次写入前发生快照漂移。");

            var settlements = new List<BattleSettlementRecord>(plannedEffectSettlementCount + 2);
            int energyAfter = playerTurn.Energy - energyCost.ActualEnergySpent;
            settlements.Add(new BattleEnergySpentSettlement(
                0,
                command.ActorId,
                playerTurn.Energy,
                energyAfter));
            _players[command.ActorId] = playerTurn.WithEnergy(energyAfter);
            settlements.AddRange(
                repeatedDamagePreparation != null
                    ? _repeatedDamageExecutor.CommitPrepared(repeatedDamagePreparation.Plan)
                    : _cardEffectSequenceExecutor.CommitPrepared(ordinaryEffectPlan));
            settlements.AddRange(
                cardZones.CommitPreparedPlayedCardDeparture(
                    departurePlan,
                    settlements.Count).Settlements);

            BattleTerminalOutcome terminalOutcome = _terminalRules.Evaluate();
            if (terminalOutcome == BattleTerminalOutcome.Ongoing)
                PublishCurrentPhase();
            else if (terminalOutcome == BattleTerminalOutcome.Victory ||
                     terminalOutcome == BattleTerminalOutcome.Defeat)
                EnterBattleEnded();
            else
                throw new InvalidOperationException("触发出牌事务完成后派生出无效的双方阵营事实。");

            return new BattleTurnOperationResult(
                BattleCommandExecutionFailureReason.None,
                settlements,
                triggeredCardPlayRequest: ordinaryEffectPlan?.TriggeredCardPlayRequest);
        }

        /// <summary>校验当前最小触发出牌切片支持的 Self 或逐段随机敌方目标协议。</summary>
        private static bool ValidateTriggeredTarget(
            PlayCardCommand command,
            cfg.battle.Card cardTemplate)
        {
            switch (cardTemplate.TargetRule)
            {
                case cfg.battle.TargetRule.Self:
                    return command.TargetId.HasValue && command.TargetId.Value == command.ActorId;
                case cfg.battle.TargetRule.Enemy:
                    return command.TargetId.HasValue;
                case cfg.battle.TargetRule.RandomEnemy:
                    return !command.TargetId.HasValue;
                default:
                    return false;
            }
        }

        /// <summary>确认 Queue 内部触发请求引用的卡仍位于冻结来源区；抽牌堆来源还必须保持顶牌身份。</summary>
        private static bool ContainsTriggeredSourceCard(
            BattleCardZonesData cardZones,
            BattleTriggeredCardPlayRequest request)
        {
            switch (request.SourceZone)
            {
                case BattleCardZone.DrawPile:
                    return cardZones.DrawPile.Count > 0 &&
                           cardZones.DrawPile[cardZones.DrawPile.Count - 1] == request.CardId;
                case BattleCardZone.Hand:
                    foreach (CardInstanceId cardId in cardZones.Hand)
                    {
                        if (cardId == request.CardId)
                            return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        /// <summary>把已通过通用合法性校验的机枪兵程序交给职业深模块，并沿用现有终局与回合发布契约。</summary>
        private BattleTurnOperationResult TryPlayMachineGunnerCard(
            PlayCardCommand command,
            PlayerTurnData playerTurn,
            BattleCardZonesData cardZones,
            CardInstanceData card,
            cfg.battle.Card cardTemplate,
            BattleTriggeredCardPlayRequest triggeredPlayRequest = null)
        {
            if (_machineGunnerRuntime == null)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.MachineGunnerRuntimeUnavailable);
            }

            MachineGunnerCardProgramExecutionResult programResult =
                _machineGunnerRuntime.ExecutePlayerCard(
                    command,
                    playerTurn,
                    cardZones,
                    card,
                    cardTemplate,
                    _blockRetention,
                    _tables,
                    _triggeredCardPlayExecution,
                    _settlementTriggerEngine,
                    triggeredPlayRequest);
            if (!programResult.Succeeded)
                return BattleTurnOperationResult.Failed(programResult.FailureReason);

            _players[command.ActorId] = programResult.PlayerTurnAfter;
            BattleTerminalOutcome terminalOutcome = _terminalRules.Evaluate();
            if (terminalOutcome == BattleTerminalOutcome.Ongoing)
            {
                if (programResult.RequestsPlayerActionEnd)
                    _requiredEndPlayerActionActorId = command.ActorId;
                PublishCurrentPhase();
            }
            else if (terminalOutcome == BattleTerminalOutcome.Victory ||
                     terminalOutcome == BattleTerminalOutcome.Defeat)
            {
                EnterBattleEnded();
            }
            else
            {
                throw new InvalidOperationException("机枪兵出牌事务完成后派生出无效的双方阵营事实。");
            }

            return new BattleTurnOperationResult(
                BattleCommandExecutionFailureReason.None,
                programResult.Settlements,
                programResult.RequestsPlayerActionEnd ? command.ActorId : (CombatantId?)null,
                programResult.TriggeredCardPlayRequest);
        }

        /// <summary>处理外部提交的结束玩家行动意图，并在消费一次性保留快照后弃置其余手牌。</summary>
        internal BattleTurnOperationResult TryEndPlayerAction(EndPlayerActionCommand command)
        {
            return TryEndPlayerAction(command, isSystemContinuation: false);
        }

        /// <summary>处理外部或 Queue 签发的结束玩家行动意图；强制结束挂起时只允许对应的系统续延完成该行动。</summary>
        internal BattleTurnOperationResult TryEndPlayerAction(
            EndPlayerActionCommand command,
            bool isSystemContinuation)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (IsPlayerActionEndRequired(command.ActorId) && !isSystemContinuation)
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded);
            }
            if (isSystemContinuation && !IsPlayerActionEndRequired(command.ActorId))
            {
                return BattleTurnOperationResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded);
            }
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
            IReadOnlyList<CardInstanceId> retainedCardIds =
                _machineGunnerRuntime?.GetRetainedCardIdsForActionEnd(command.ActorId) ??
                Array.Empty<CardInstanceId>();

            BattleStatusTimingPreparationResult statusPreparation = _statusTiming.Prepare(
                BattleStatusTimingPoint.PlayerActionEnded,
                command.ActorId,
                cardZones.Hand.Count);
            if (!statusPreparation.Succeeded)
                return BattleTurnOperationResult.Failed(statusPreparation.FailureReason);
            if (!_statusTiming.ValidatePrepared(statusPreparation.Plan))
                throw new InvalidOperationException("玩家行动结束状态计划在首次写入前发生快照漂移。");

            MachineGunnerActorActionEndPlan actorActionEndPlan =
                _machineGunnerRuntime?.PrepareActorActionEnd(command.ActorId);
            if (actorActionEndPlan != null &&
                !_machineGunnerRuntime.ValidatePreparedActorActionEnd(actorActionEndPlan))
            {
                throw new InvalidOperationException("玩家行动结束职业状态计划在首次写入前发生快照漂移。");
            }

            MachineGunnerScheduledEffectLifecyclePlan scheduledRoundEndPlan =
                _machineGunnerRuntime?.PreparePlayerRoundEndScheduledEffects();
            if (scheduledRoundEndPlan != null &&
                !_machineGunnerRuntime.ValidatePreparedScheduledEffects(scheduledRoundEndPlan))
            {
                throw new InvalidOperationException("玩家回合末延迟效果计划在首次写入前发生快照漂移。");
            }

            BattleTerminalOutcome terminalOutcome = _terminalRules.Evaluate();
            if (terminalOutcome == BattleTerminalOutcome.InvalidFacts)
                throw new InvalidOperationException("玩家行动结束前派生出无效的双方阵营事实。");

            return CollectSuccessfulOperation(() =>
            {
                if (isSystemContinuation)
                    _requiredEndPlayerActionActorId = null;
                if (retainedCardIds.Count > 0)
                {
                    AppendCardZoneResult(cardZones.DiscardHandExcept(
                        retainedCardIds,
                        CurrentSettlementOrder));
                    _machineGunnerRuntime.ConsumeRetainedCardIdsForActionEnd(command.ActorId);
                }
                else
                {
                    AppendCardZoneResult(cardZones.DiscardHand(CurrentSettlementOrder));
                }
                AppendStatusTimingResult(_statusTiming.CommitPrepared(statusPreparation.Plan));
                if (actorActionEndPlan != null)
                {
                    AppendMachineGunnerActionEndSettlements(
                        _machineGunnerRuntime.CommitPreparedActorActionEnd(
                            actorActionEndPlan,
                            CurrentSettlementOrder));
                }
                _players[command.ActorId] = playerTurn.WithHasEndedAction(true);
                if (terminalOutcome == BattleTerminalOutcome.Victory ||
                    terminalOutcome == BattleTerminalOutcome.Defeat)
                {
                    EnterBattleEnded();
                }
                else if (HaveAllLivingPlayersEndedAction())
                {
                    if (scheduledRoundEndPlan != null)
                    {
                        AppendMachineGunnerRoundEndSettlements(
                            _machineGunnerRuntime.CommitPreparedScheduledEffects(
                                scheduledRoundEndPlan,
                                CurrentSettlementOrder));
                    }

                    BattleTerminalOutcome terminalOutcomeAfterScheduledEffects =
                        _terminalRules.Evaluate();
                    if (terminalOutcomeAfterScheduledEffects == BattleTerminalOutcome.Victory ||
                        terminalOutcomeAfterScheduledEffects == BattleTerminalOutcome.Defeat)
                    {
                        EnterBattleEnded();
                    }
                    else if (terminalOutcomeAfterScheduledEffects == BattleTerminalOutcome.InvalidFacts)
                    {
                        throw new InvalidOperationException("玩家回合末延迟效果结算后派生出无效的双方阵营事实。");
                    }
                    else
                    {
                        if (_machineGunnerRuntime != null)
                        {
                            AppendMachineGunnerRoundEndSettlements(
                                _machineGunnerRuntime.ResolvePlayerRoundEnd(CurrentSettlementOrder));
                        }

                        BattleTerminalOutcome terminalOutcomeAfterRoundEnd = _terminalRules.Evaluate();
                        if (terminalOutcomeAfterRoundEnd == BattleTerminalOutcome.Victory ||
                            terminalOutcomeAfterRoundEnd == BattleTerminalOutcome.Defeat)
                        {
                            EnterBattleEnded();
                        }
                        else if (terminalOutcomeAfterRoundEnd == BattleTerminalOutcome.InvalidFacts)
                        {
                            throw new InvalidOperationException("机枪兵回合末结算后派生出无效的双方阵营事实。");
                        }
                        else
                        {
                            _stateMachine.Dispatch(BattleTurnEvent.CompletePlayerRound);
                            TickAutomaticPhasesAndFinalizePendingBattleEnd();
                        }
                    }
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
                TickAutomaticPhasesAndFinalizePendingBattleEnd();
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
                if (!ResetPlayersForRound(roundNumber))
                    return;
            }

            _turn.Value = new BattleTurnData(phase, roundNumber, _players, currentActingEnemyId);
        }

        /// <summary>推进全部自动阶段，并只在 Tick 完全退出状态机处理栈后安全发布待定终局。</summary>
        private void TickAutomaticPhasesAndFinalizePendingBattleEnd()
        {
            _stateMachine.Tick(TimeSpan.Zero);
            if (!_pendingBattleEndRoundNumber.HasValue)
                return;

            int roundNumber = _pendingBattleEndRoundNumber.Value;
            _pendingBattleEndRoundNumber = null;
            EnterBattleEnded(roundNumber);
        }

        /// <summary>先联合冻结并校验全部存活玩家的中毒触发，再按稳定顺序逐人结算和重置；终局时延后发布并返回 false。</summary>
        private bool ResetPlayersForRound(int roundNumber)
        {
            if (roundNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(roundNumber));

            bool isFirstRound = roundNumber == 1;
            var poisonPlans = new List<BattlePreparedPoisonTick>();
            foreach (CombatantId playerId in GetPlayerIdsInStableOrder())
            {
                if (!_combatants.TryGet(playerId, out CombatantData combatant) ||
                    !(combatant is PlayerCombatantData) ||
                    !combatant.IsAlive)
                {
                    continue;
                }

                BattlePoisonTickPreparationResult preparation =
                    _poisonApplication.PrepareTick(playerId);
                if (!preparation.Succeeded)
                    throw new InvalidOperationException("存活玩家的回合开始中毒计划意外失败。");

                poisonPlans.Add(preparation.Plan);
            }

            foreach (BattlePreparedPoisonTick poisonPlan in poisonPlans)
            {
                if (!_poisonApplication.ValidatePreparedTick(poisonPlan))
                {
                    throw new InvalidOperationException(
                        "玩家回合开始中毒计划在首次权威写入前发生快照漂移。");
                }
            }

            foreach (BattlePreparedPoisonTick poisonPlan in poisonPlans)
            {
                CombatantId playerId = poisonPlan.TargetId;
                AppendPoisonTickSettlements(
                    _poisonApplication.CommitPreparedTick(
                        poisonPlan,
                        CurrentSettlementOrder));

                // 只有致死中毒会改变阵营存活事实；零层或非致死触发不得额外引入终局判定。
                if (poisonPlan.WasFatal)
                {
                    BattleTerminalOutcome terminalOutcome = _terminalRules.Evaluate();
                    if (terminalOutcome == BattleTerminalOutcome.InvalidFacts)
                    {
                        throw new InvalidOperationException(
                            "玩家回合开始中毒结算后派生出无效的双方阵营事实。");
                    }
                    if (terminalOutcome == BattleTerminalOutcome.Victory ||
                        terminalOutcome == BattleTerminalOutcome.Defeat)
                    {
                        _pendingBattleEndRoundNumber = roundNumber;
                        return false;
                    }
                }

                if (!_combatants.TryGet(playerId, out CombatantData combatant) ||
                    !(combatant is PlayerCombatantData))
                {
                    throw new InvalidOperationException("已验证的玩家中毒目标不再存在。");
                }
                if (!combatant.IsAlive)
                    continue;

                BattleStatusTimingResult blockResult = _statusTiming.Execute(
                    BattleStatusTimingPoint.PlayerRoundStart,
                    playerId,
                    CurrentSettlementOrder);
                if (!blockResult.Succeeded)
                    throw new InvalidOperationException("存活玩家的回合开始状态时机意外失败。");
                AppendStatusTimingResult(blockResult);

                MachineGunnerPlayerRoundStartResult machineGunnerRoundStart =
                    _machineGunnerRuntime?.BeginPlayerRound(playerId);
                AppendMachineGunnerPrivateStatusChange(
                    playerId,
                    machineGunnerRoundStart?.NextRoundBlockClear);
                int blockGain = machineGunnerRoundStart?.BlockGain ?? 0;
                if (blockGain > 0)
                {
                    int blockBefore = combatant.CurrentBlock;
                    combatant.ApplyBlockGain(blockGain);
                    _activeSettlements.Add(new BattleBlockGainedSettlement(
                        CurrentSettlementOrder,
                        null,
                        playerId,
                        playerId,
                        blockBefore,
                        combatant.CurrentBlock));
                }

                if (!_playerResourceProfiles.TryGetValue(playerId, out BattlePlayerResourceProfile profile))
                {
                    throw new InvalidOperationException(
                        $"Player {playerId} has no resource profile during round reset.");
                }

                PlayerTurnData playerTurn = _players[playerId];
                int energyGainAdjustment = machineGunnerRoundStart?.EnergyGainAdjustment ?? 0;
                PlayerTurnData replenished = profile.StartPlayerRound(
                    playerTurn,
                    isFirstRound,
                    energyGainAdjustment);
                if (playerTurn.Energy != replenished.Energy)
                {
                    _activeSettlements.Add(new BattleEnergyRefilledSettlement(
                        CurrentSettlementOrder,
                        playerId,
                        playerTurn.Energy,
                        replenished.Energy));
                }
                AppendMachineGunnerPrivateStatusChange(
                    playerId,
                    machineGunnerRoundStart?.NextRoundEnergyGainBonusClear);
                AppendMachineGunnerPrivateStatusChange(
                    playerId,
                    machineGunnerRoundStart?.NextRoundEnergyGainPenaltyClear);
                if (playerTurn.Ammo != replenished.Ammo)
                {
                    _activeSettlements.Add(new BattleAmmoRefilledSettlement(
                        CurrentSettlementOrder,
                        playerId,
                        playerTurn.Ammo,
                        replenished.Ammo));
                }

                AppendMachineGunnerPrivateStatusChange(
                    playerId,
                    machineGunnerRoundStart?.ReloadAmmoClear);
                if (machineGunnerRoundStart != null &&
                    machineGunnerRoundStart.RefillAmmoAfterNormalReplenish &&
                    replenished.Ammo != replenished.AmmoMaximum)
                {
                    PlayerTurnData refilledAmmo = replenished.WithAmmo(replenished.AmmoMaximum);
                    _activeSettlements.Add(new BattleAmmoRefilledSettlement(
                        CurrentSettlementOrder,
                        playerId,
                        replenished.Ammo,
                        refilledAmmo.Ammo));
                    replenished = refilledAmmo;
                }

                _players[playerId] = replenished;
                DrawPlayerToTargetHand(playerId, isFirstRound);
            }

            return true;
        }

        /// <summary>从指定玩家的权威卡区补抽到静态规则定义的目标手牌数量。</summary>
        private void DrawPlayerToTargetHand(CombatantId playerId, bool isFirstRound)
        {
            if (!_playerCardZones.TryGetValue(playerId, out BattleCardZonesData cardZones) ||
                cardZones == null)
            {
                return;
            }

            if (isFirstRound)
            {
                if (_preparedOpeningHands == null ||
                    !_preparedOpeningHands.TryGetValue(
                        playerId,
                        out BattlePreparedOpeningHand openingHand))
                {
                    throw new InvalidOperationException("首回合发牌缺少在战斗写入前冻结的起手计划。");
                }

                AppendCardZoneResult(cardZones.CommitPreparedOpeningHand(
                    openingHand,
                    CurrentSettlementOrder));
                return;
            }

            int targetHandCount = _machineGunnerRuntime?.GetPlayerRoundHandTarget(
                playerId,
                _initialHandCount) ?? _initialHandCount;
            int handLimit = _machineGunnerRuntime?.GetHandLimit(playerId) ?? int.MaxValue;
            int drawCount = Math.Max(0, targetHandCount - cardZones.Hand.Count);
            if (drawCount > 0)
            {
                AppendCardZoneResult(cardZones.Draw(
                    drawCount,
                    CurrentSettlementOrder,
                    handLimit));
            }
        }

        /// <summary>在开始战斗的任何状态、资源或布局写入前，为全部存活玩家尝试冻结唯一的起手布局计划。</summary>
        private bool TryPrepareOpeningHands(
            out IReadOnlyDictionary<CombatantId, BattlePreparedOpeningHand> openingHands)
        {
            var plans = new Dictionary<CombatantId, BattlePreparedOpeningHand>();
            var claimedZones = new HashSet<BattleCardZonesData>();
            foreach (CombatantId playerId in GetPlayerIdsInStableOrder())
            {
                if (!_combatants.TryGet(playerId, out CombatantData combatant) ||
                    !(combatant is PlayerCombatantData) ||
                    !combatant.IsAlive)
                {
                    continue;
                }
                if (!_playerCardZones.TryGetValue(playerId, out BattleCardZonesData cardZones) ||
                    cardZones == null)
                {
                    continue;
                }
                if (!claimedZones.Add(cardZones))
                {
                    openingHands = null;
                    return false;
                }

                int targetHandCount = _machineGunnerRuntime?.GetPlayerRoundHandTarget(
                    playerId,
                    _initialHandCount) ?? _initialHandCount;
                int handLimit = _machineGunnerRuntime?.GetHandLimit(playerId) ??
                    BattleCardZonesData.BattleCardHandLimit;
                IReadOnlyCollection<CardInstanceId> innateCardIds = CollectInnateCardIds(cardZones);
                try
                {
                    plans.Add(
                        playerId,
                        cardZones.PrepareOpeningHand(
                            innateCardIds,
                            targetHandCount,
                            handLimit));
                }
                catch (ArgumentException)
                {
                    openingHands = null;
                    return false;
                }
                catch (InvalidOperationException)
                {
                    openingHands = null;
                    return false;
                }
            }

            openingHands = new ReadOnlyDictionary<CombatantId, BattlePreparedOpeningHand>(plans);
            return true;
        }

        /// <summary>仅按卡牌配置收集当前牌组的固有牌身份，具体先后顺序完全交由卡区依据已洗牌布局决定。</summary>
        private IReadOnlyCollection<CardInstanceId> CollectInnateCardIds(BattleCardZonesData cardZones)
        {
            if (cardZones == null)
                throw new ArgumentNullException(nameof(cardZones));

            var innateCardIds = new HashSet<CardInstanceId>();
            foreach (CardInstanceData card in cardZones.Cards.Values)
            {
                cfg.battle.Card cardTemplate = _tables.TbCard.GetOrDefault(card.TemplateId);
                if (cardTemplate != null && cardTemplate.IsInnate)
                    innateCardIds.Add(card.Id);
            }

            return innateCardIds;
        }

        /// <summary>按参与者标识返回稳定玩家顺序，供起手预演与正式回合重置共享同一遍历口径。</summary>
        private IReadOnlyList<CombatantId> GetPlayerIdsInStableOrder()
        {
            var playerIds = new List<CombatantId>(_players.Keys);
            playerIds.Sort((left, right) => left.Value.CompareTo(right.Value));
            return playerIds;
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

        /// <summary>校验通用中毒触发记录非空且与当前命令顺序连续后，再追加到唯一 settlement 链。</summary>
        private void AppendPoisonTickSettlements(
            IReadOnlyList<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (_activeSettlements == null)
                throw new InvalidOperationException("当前没有可追加中毒触发记录的命令结算作用域。");

            foreach (BattleSettlementRecord settlement in settlements)
            {
                if (settlement == null || settlement.Order != CurrentSettlementOrder)
                {
                    throw new InvalidOperationException(
                        "中毒触发结算记录必须与当前命令保持连续顺序。");
                }

                _activeSettlements.Add(settlement);
            }
        }

        /// <summary>把已在职业回合开始冻结的私有状态清除写入当前唯一 settlement 链，确保延迟效果可被回归和表现层观察。</summary>
        private void AppendMachineGunnerPrivateStatusChange(
            CombatantId playerId,
            MachineGunnerStatusValueChange? change)
        {
            if (!change.HasValue)
                return;
            if (_activeSettlements == null)
            {
                throw new InvalidOperationException(
                    "当前没有可追加机枪兵私有状态变更的命令结算作用域。");
            }

            MachineGunnerStatusValueChange value = change.Value;
            _activeSettlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                CurrentSettlementOrder,
                playerId,
                playerId,
                value.Status,
                value.Before,
                value.After));
        }

        /// <summary>校验并追加职业行动结束生命周期产生的连续 settlement，不允许运行时绕过当前 Queue 命令。</summary>
        private void AppendMachineGunnerActionEndSettlements(
            IReadOnlyList<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (_activeSettlements == null)
            {
                throw new InvalidOperationException(
                    "当前没有可追加机枪兵行动结束记录的命令结算作用域。");
            }

            foreach (BattleSettlementRecord settlement in settlements)
            {
                if (settlement.Order != CurrentSettlementOrder)
                    throw new InvalidOperationException("职业行动结束记录必须与当前命令保持连续顺序。");
                _activeSettlements.Add(settlement);
            }
        }

        /// <summary>判断指定玩家是否已由一张成功出牌锁定为只能等待 Queue 系统结束行动续延。</summary>
        private bool IsPlayerActionEndRequired(CombatantId playerId)
        {
            return _requiredEndPlayerActionActorId.HasValue &&
                _requiredEndPlayerActionActorId.Value == playerId;
        }

        /// <summary>校验职业回合末结算记录与当前命令的全局顺序连续后再追加，避免职业运行时绕过 Queue 的权威 settlement 链。</summary>
        private void AppendMachineGunnerRoundEndSettlements(
            IReadOnlyList<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (_activeSettlements == null)
            {
                throw new InvalidOperationException(
                    "当前没有可追加机枪兵回合末记录的命令结算作用域。");
            }

            foreach (BattleSettlementRecord settlement in settlements)
            {
                if (settlement == null || settlement.Order != CurrentSettlementOrder)
                {
                    throw new InvalidOperationException(
                        "机枪兵回合末结算记录必须与当前命令保持连续顺序。");
                }

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

        /// <summary>停止内部状态机，并用可选的待定轮次覆盖当前快照后发布不保存胜负镜像的中立终局阶段。</summary>
        private void EnterBattleEnded(int? roundNumberOverride = null)
        {
            if (roundNumberOverride.HasValue && roundNumberOverride.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(roundNumberOverride));

            if (_stateMachine.IsRunning)
                _stateMachine.Stop();

            _machineGunnerRuntime?.ClearScheduledEffectsAtBattleEnd();

            BattleTurnData current = Turn.CurrentValue;
            _turn.Value = new BattleTurnData(
                BattleTurnPhase.BattleEnded,
                roundNumberOverride ?? current.RoundNumber,
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
                if (_owner._pendingBattleEndRoundNumber.HasValue)
                    return StateTransition<BattleTurnEvent>.Stay;

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
