using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;

namespace TinySpire.Battle
{
    /// <summary>一次出牌交互需要的不可变手牌选择请求，只保存当前权威快照派生事实。</summary>
    public sealed class BattleHandCardSelectionRequest
    {
        /// <summary>本次命令必须选择的手牌数量。</summary>
        public int RequiredCount { get; }

        /// <summary>按当前手牌顺序冻结的全部合法候选。</summary>
        public IReadOnlyList<CardInstanceId> LegalCardIds { get; }

        /// <summary>防御性复制候选并冻结选择数量。</summary>
        internal BattleHandCardSelectionRequest(
            int requiredCount,
            IEnumerable<CardInstanceId> legalCardIds)
        {
            if (requiredCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            if (legalCardIds == null)
                throw new ArgumentNullException(nameof(legalCardIds));

            RequiredCount = requiredCount;
            LegalCardIds = new ReadOnlyCollection<CardInstanceId>(
                new List<CardInstanceId>(legalCardIds));
        }
    }

    /// <summary>
    /// 一次出牌合法性读取形成的不可变结果；它不保存为第二份运行时事实。
    /// </summary>
    public sealed class BattleCardPlayEvaluation
    {
        /// <summary>当前具体命令是否通过全部规则。</summary>
        public bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>当前具体命令的稳定失败原因。</summary>
        public BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>静态卡牌声明的目标规则；模板尚不可用时为空。</summary>
        public cfg.battle.TargetRule? TargetRule { get; }

        /// <summary>除目标选择以外的当前事实是否允许开始交互。</summary>
        public bool CanStartInteraction { get; }

        /// <summary>当前玩家能量是否足以支付静态卡牌费用。</summary>
        public bool CanPayCost { get; }

        /// <summary>本次出牌是否必须由玩家显式选择一个敌方目标；自动目标与自目标均为 false。</summary>
        public bool RequiresExplicitTargetInput { get; }

        /// <summary>按稳定规则顺序派生的一次性合法目标快照。</summary>
        public IReadOnlyList<CombatantId> LegalTargetIds { get; }

        /// <summary>当前卡牌需要额外手牌选择时返回的不可变请求；无此需求时为空。</summary>
        public BattleHandCardSelectionRequest HandCardSelectionRequest { get; }

        /// <summary>冻结本次读取结果，避免调用方把派生目标改成可变镜像。</summary>
        internal BattleCardPlayEvaluation(
            BattleCommandExecutionFailureReason failureReason,
            cfg.battle.TargetRule? targetRule,
            bool canStartInteraction,
            bool canPayCost,
            IEnumerable<CombatantId> legalTargetIds,
            bool? requiresExplicitTargetInput = null,
            BattleHandCardSelectionRequest handCardSelectionRequest = null)
        {
            if (legalTargetIds == null)
                throw new ArgumentNullException(nameof(legalTargetIds));

            FailureReason = failureReason;
            TargetRule = targetRule;
            CanStartInteraction = canStartInteraction;
            CanPayCost = canPayCost;
            RequiresExplicitTargetInput = requiresExplicitTargetInput ??
                targetRule == cfg.battle.TargetRule.Enemy;
            LegalTargetIds = new ReadOnlyCollection<CombatantId>(
                new List<CombatantId>(legalTargetIds));
            HandCardSelectionRequest = handCardSelectionRequest;
        }
    }

    /// <summary>集中派生“从当前手牌选择指定数量其他牌”的候选、零候选与稳定失败语义。</summary>
    internal static class BattleSingleOtherHandCardSelectionRules
    {
        /// <summary>只读校验选择数量、手牌归属与候选资格；有候选但未选择时返回交互请求。</summary>
        internal static BattleCommandExecutionFailureReason Evaluate(
            PlayCardCommand command,
            BattleCardZonesData cardZones,
            out BattleHandCardSelectionRequest request,
            int requiredCount = 1)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (cardZones == null)
                throw new ArgumentNullException(nameof(cardZones));
            if (requiredCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));

            request = null;
            if (command.SelectedCardIds.Count > 0)
            {
                if (command.SelectedCardIds.Count != requiredCount)
                    return BattleCommandExecutionFailureReason.InvalidCardSelectionCount;

                var uniqueCardIds = new HashSet<CardInstanceId>();
                foreach (CardInstanceId selectedCardId in command.SelectedCardIds)
                {
                    if (!uniqueCardIds.Add(selectedCardId))
                        return BattleCommandExecutionFailureReason.InvalidCardSelectionCount;
                    if (selectedCardId == command.CardId)
                        return BattleCommandExecutionFailureReason.SelectedCardNotEligible;
                    if (!ContainsCardId(cardZones.Hand, selectedCardId))
                        return BattleCommandExecutionFailureReason.SelectedCardNotInHand;
                }

                return BattleCommandExecutionFailureReason.None;
            }

            var legalCardIds = new List<CardInstanceId>();
            foreach (CardInstanceId cardId in cardZones.Hand)
            {
                if (cardId != command.CardId)
                    legalCardIds.Add(cardId);
            }
            if (legalCardIds.Count < requiredCount)
                return BattleCommandExecutionFailureReason.None;

            request = new BattleHandCardSelectionRequest(requiredCount, legalCardIds);
            return BattleCommandExecutionFailureReason.CardSelectionRequired;
        }

        /// <summary>确认冻结手牌序列包含指定实例。</summary>
        private static bool ContainsCardId(
            IReadOnlyList<CardInstanceId> cardIds,
            CardInstanceId targetCardId)
        {
            foreach (CardInstanceId cardId in cardIds)
            {
                if (cardId == targetCardId)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 从当前回合、参与者、卡区和静态模板即时派生出牌合法性，不持有可变玩法镜像。
    /// </summary>
    public sealed class BattleCardPlayRules
    {
        private static readonly IReadOnlyList<CombatantId> NoTargets =
            Array.Empty<CombatantId>();

        private readonly BattleCombatantsData _combatants;
        private readonly IReadOnlyDictionary<CombatantId, BattleCardZonesData> _playerCardZones;
        private readonly IReadOnlyList<CombatantId> _enemyCombatantIdsInEncounterOrder;
        private readonly Tables _tables;
        private readonly MachineGunnerBattleRuntime _machineGunnerRuntime;

        /// <summary>为公共读取方保存规则派生所需的唯一权威事实入口与静态表；未绑定职业私有运行时。</summary>
        public BattleCardPlayRules(
            BattleCombatantsData combatants,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            Tables tables)
            : this(
                combatants,
                playerCardZones,
                enemyCombatantIdsInEncounterOrder,
                tables,
                machineGunnerRuntime: null)
        {
        }

        /// <summary>为权威回合模块保存规则派生所需事实，并按需接入职业私有运行时。</summary>
        internal BattleCardPlayRules(
            BattleCombatantsData combatants,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            Tables tables,
            MachineGunnerBattleRuntime machineGunnerRuntime)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _playerCardZones = playerCardZones ?? throw new ArgumentNullException(nameof(playerCardZones));
            _enemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder
                ?? throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _machineGunnerRuntime = machineGunnerRuntime;
        }

        /// <summary>按当前权威快照评估一条出牌意图，整个过程不写入任何战斗事实。</summary>
        public BattleCardPlayEvaluation Evaluate(BattleTurnData turn, PlayCardCommand command)
        {
            if (turn == null)
                throw new ArgumentNullException(nameof(turn));
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (turn.Phase != BattleTurnPhase.PlayerAction)
                return Failure(BattleCommandExecutionFailureReason.InvalidTurnPhase);
            if (!turn.Players.TryGetValue(command.ActorId, out PlayerTurnData playerTurn) ||
                !_combatants.TryGet(command.ActorId, out CombatantData actor) ||
                !(actor is PlayerCombatantData))
            {
                return Failure(BattleCommandExecutionFailureReason.InvalidPlayer);
            }
            if (!actor.IsAlive)
                return Failure(BattleCommandExecutionFailureReason.PlayerNotAlive);
            if (playerTurn.HasEndedAction)
                return Failure(BattleCommandExecutionFailureReason.PlayerActionAlreadyEnded);
            if (!HasLivingPlayer() || !HasLivingEnemy())
                return Failure(BattleCommandExecutionFailureReason.BattleAlreadyEnded);
            if (!_playerCardZones.TryGetValue(command.ActorId, out BattleCardZonesData cardZones) ||
                cardZones == null)
            {
                return Failure(BattleCommandExecutionFailureReason.PlayerCardZonesNotFound);
            }
            if (!IsCardInHand(cardZones, command.CardId) ||
                !cardZones.TryGetCard(command.CardId, out CardInstanceData card))
            {
                return Failure(BattleCommandExecutionFailureReason.CardNotInHand);
            }

            cfg.battle.Card cardTemplate = _tables.TbCard.GetOrDefault(card.TemplateId);
            if (cardTemplate == null)
                return Failure(BattleCommandExecutionFailureReason.CardTemplateNotFound);
            if (cardTemplate.ImplementationStatus !=
                cfg.battle.CardImplementationStatus.Implemented)
            {
                return Failure(BattleCommandExecutionFailureReason.CardNotImplemented);
            }
            if (cardTemplate.ProgramId != cfg.battle.MachineGunnerProgramId.None)
            {
                return EvaluateMachineGunnerCard(
                    command,
                    playerTurn,
                    cardTemplate);
            }
            if (cardTemplate.CostKind == cfg.battle.CardCostKind.Fixed && cardTemplate.Cost < 0)
                return Failure(BattleCommandExecutionFailureReason.CardTemplateNotFound);

            bool canPayCost;
            switch (cardTemplate.CostKind)
            {
                case cfg.battle.CardCostKind.Fixed:
                    canPayCost = BattleCardCostResolver.TryResolveEnergy(
                        cardTemplate.CostKind,
                        cardTemplate.Cost,
                        playerTurn.Energy,
                        BattleCardPaymentMode.Normal,
                        out _,
                        out _);
                    break;
                case cfg.battle.CardCostKind.X:
                    canPayCost = true;
                    break;
                default:
                    return Failure(BattleCommandExecutionFailureReason.CardTemplateNotFound);
            }
            if (!canPayCost)
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.InsufficientEnergy,
                    cardTemplate.TargetRule,
                    canStartInteraction: false,
                    canPayCost: false,
                    NoTargets);
            }
            if (cardTemplate.TargetRule != cfg.battle.TargetRule.Self &&
                cardTemplate.TargetRule != cfg.battle.TargetRule.Enemy &&
                cardTemplate.TargetRule != cfg.battle.TargetRule.RandomEnemy)
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.UnsupportedTargetRule,
                    cardTemplate.TargetRule,
                    canStartInteraction: false,
                    canPayCost: true,
                    NoTargets);
            }

            IReadOnlyList<CombatantId> legalTargets = DeriveLegalTargets(
                cardTemplate.TargetRule,
                command.ActorId);
            if (cardTemplate.TargetRule == cfg.battle.TargetRule.RandomEnemy)
            {
                if (command.TargetId.HasValue)
                {
                    return new BattleCardPlayEvaluation(
                        BattleCommandExecutionFailureReason.TargetRuleMismatch,
                        cardTemplate.TargetRule,
                        canStartInteraction: true,
                        canPayCost: true,
                        legalTargets,
                        requiresExplicitTargetInput: false);
                }
                if (legalTargets.Count == 0)
                {
                    return new BattleCardPlayEvaluation(
                        BattleCommandExecutionFailureReason.TargetNotAlive,
                        cardTemplate.TargetRule,
                        canStartInteraction: false,
                        canPayCost: true,
                        legalTargets,
                        requiresExplicitTargetInput: false);
                }
                if (command.SelectedCardIds.Count > 0)
                {
                    return new BattleCardPlayEvaluation(
                        BattleCommandExecutionFailureReason.InvalidCardSelectionCount,
                        cardTemplate.TargetRule,
                        canStartInteraction: true,
                        canPayCost: true,
                        legalTargets,
                        requiresExplicitTargetInput: false);
                }

                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.None,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets,
                    requiresExplicitTargetInput: false);
            }
            if (!command.TargetId.HasValue)
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetRequired,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets);
            }
            if (!_combatants.TryGet(command.TargetId.Value, out CombatantData target))
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetNotFound,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets);
            }
            if (!target.IsAlive)
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetNotAlive,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets);
            }
            if (!ContainsTarget(legalTargets, command.TargetId.Value))
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets);
            }

            BattleCommandExecutionFailureReason selectionFailure =
                ValidateGenericHandCardSelection(
                    command,
                    cardTemplate,
                    cardZones,
                    out BattleHandCardSelectionRequest handCardSelectionRequest);
            if (selectionFailure != BattleCommandExecutionFailureReason.None)
            {
                return new BattleCardPlayEvaluation(
                    selectionFailure,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets,
                    handCardSelectionRequest: handCardSelectionRequest);
            }

            return new BattleCardPlayEvaluation(
                BattleCommandExecutionFailureReason.None,
                cardTemplate.TargetRule,
                canStartInteraction: true,
                canPayCost: true,
                legalTargets);
        }

        /// <summary>按 Effect 声明识别唯一选牌→抽牌序列，并在规则层统一冻结 Value、Attribute 与零候选语义。</summary>
        private BattleCommandExecutionFailureReason ValidateGenericHandCardSelection(
            PlayCardCommand command,
            cfg.battle.Card cardTemplate,
            BattleCardZonesData cardZones,
            out BattleHandCardSelectionRequest request)
        {
            request = null;
            bool foundSelection = false;
            bool foundDrawAfterSelection = false;
            bool foundEffectBeforeSelection = false;
            foreach (cfg.battle.CardEffectBinding binding in cardTemplate.EffectBindings)
            {
                if (binding == null || binding.EffectId <= 0)
                    return BattleCommandExecutionFailureReason.InvalidEffectBinding;

                cfg.battle.CardEffect effect = _tables.TbCardEffect.GetOrDefault(binding.EffectId);
                if (effect == null)
                    return BattleCommandExecutionFailureReason.EffectTemplateNotFound;
                if (BattleCardEffectTypeMapping.IsExhaustSelectedHandCard(effect.EffectType))
                {
                    if (foundSelection || foundDrawAfterSelection || foundEffectBeforeSelection)
                        return BattleCommandExecutionFailureReason.InvalidEffectBinding;
                    if (effect.Attribute != cfg.battle.Attribute.None)
                        return BattleCommandExecutionFailureReason.UnsupportedEffectAttribute;
                    if (effect.Value != 1)
                        return BattleCommandExecutionFailureReason.InvalidEffectBinding;

                    foundSelection = true;
                    continue;
                }
                if (!foundSelection)
                {
                    foundEffectBeforeSelection = true;
                    continue;
                }
                if (!BattleCardEffectTypeMapping.IsDrawCards(effect.EffectType) ||
                    foundDrawAfterSelection)
                {
                    return BattleCommandExecutionFailureReason.InvalidEffectBinding;
                }
                if (effect.Attribute != cfg.battle.Attribute.None)
                    return BattleCommandExecutionFailureReason.UnsupportedEffectAttribute;
                if (effect.Value < 0)
                    return BattleCommandExecutionFailureReason.InvalidEffectBinding;

                foundDrawAfterSelection = true;
            }

            if (!foundSelection)
            {
                return command.SelectedCardIds.Count == 0
                    ? BattleCommandExecutionFailureReason.None
                    : BattleCommandExecutionFailureReason.InvalidCardSelectionCount;
            }
            if (!foundDrawAfterSelection)
                return BattleCommandExecutionFailureReason.InvalidEffectBinding;

            return BattleSingleOtherHandCardSelectionRules.Evaluate(
                command,
                cardZones,
                out request);
        }

        /// <summary>按声明式机枪兵程序而非静态展示 TargetRule 评估弹药、自动目标和自目标输入。</summary>
        private BattleCardPlayEvaluation EvaluateMachineGunnerCard(
            PlayCardCommand command,
            PlayerTurnData playerTurn,
            cfg.battle.Card cardTemplate)
        {
            if (_machineGunnerRuntime == null ||
                !_machineGunnerRuntime.SupportsPlayer(command.ActorId))
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.MachineGunnerRuntimeUnavailable,
                    cardTemplate.TargetRule,
                    canStartInteraction: false,
                    canPayCost: true,
                    NoTargets);
            }
            if (!MachineGunnerCardProgramRegistry.TryGet(cardTemplate.ProgramId, out MachineGunnerCardProgram program))
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.CardNotImplemented,
                    cardTemplate.TargetRule,
                    canStartInteraction: false,
                    canPayCost: true,
                    NoTargets);
            }
            if (_machineGunnerRuntime.IsAttackBlockedByShackle(command.ActorId, program))
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.AttackBlockedByShackle,
                    cardTemplate.TargetRule,
                    canStartInteraction: false,
                    canPayCost: true,
                    NoTargets);
            }
            if (!_machineGunnerRuntime.TryPreviewCost(
                    cardTemplate,
                    playerTurn,
                    program,
                    out _,
                    out BattleCommandExecutionFailureReason costFailure))
            {
                return new BattleCardPlayEvaluation(
                    costFailure,
                    cardTemplate.TargetRule,
                    canStartInteraction: false,
                    canPayCost: costFailure != BattleCommandExecutionFailureReason.InsufficientEnergy,
                    NoTargets);
            }

            IReadOnlyList<CombatantId> legalTargets = DeriveMachineGunnerTargets(
                program.TargetInputMode,
                command.ActorId);
            if (legalTargets.Count == 0)
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetNotAlive,
                    cardTemplate.TargetRule,
                    canStartInteraction: false,
                    canPayCost: true,
                    legalTargets);
            }
            if (program.TargetInputMode == MachineGunnerTargetInputMode.ExplicitEnemy &&
                !command.TargetId.HasValue)
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetRequired,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets);
            }
            if ((program.TargetInputMode == MachineGunnerTargetInputMode.AllLivingEnemies ||
                 program.TargetInputMode == MachineGunnerTargetInputMode.RandomLivingEnemy ||
                 program.TargetInputMode == MachineGunnerTargetInputMode.AutomaticNearestTwoEnemies) &&
                command.TargetId.HasValue)
            {
                return new BattleCardPlayEvaluation(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets);
            }
            if (command.TargetId.HasValue)
            {
                if (!_combatants.TryGet(command.TargetId.Value, out CombatantData target))
                {
                    return new BattleCardPlayEvaluation(
                        BattleCommandExecutionFailureReason.TargetNotFound,
                        cardTemplate.TargetRule,
                        canStartInteraction: true,
                        canPayCost: true,
                        legalTargets);
                }
                if (!target.IsAlive)
                {
                    return new BattleCardPlayEvaluation(
                        BattleCommandExecutionFailureReason.TargetNotAlive,
                        cardTemplate.TargetRule,
                        canStartInteraction: true,
                        canPayCost: true,
                        legalTargets);
                }
                if (!ContainsTarget(legalTargets, command.TargetId.Value))
                {
                    return new BattleCardPlayEvaluation(
                        BattleCommandExecutionFailureReason.TargetRuleMismatch,
                        cardTemplate.TargetRule,
                        canStartInteraction: true,
                        canPayCost: true,
                        legalTargets);
                }
                if (program.ExecutionKind ==
                        MachineGunnerProgramExecutionKind.
                            InitialThenRepeatByTargetStatusKinds &&
                    !_machineGunnerRuntime.TryPreviewCost(
                        cardTemplate,
                        playerTurn,
                        program,
                        command.TargetId.Value,
                        out _,
                        out BattleCommandExecutionFailureReason exactCostFailure))
                {
                    return new BattleCardPlayEvaluation(
                        exactCostFailure,
                        cardTemplate.TargetRule,
                        canStartInteraction: true,
                        canPayCost: exactCostFailure !=
                                BattleCommandExecutionFailureReason.InsufficientEnergy &&
                            exactCostFailure !=
                                BattleCommandExecutionFailureReason.InsufficientAmmo,
                        legalTargets);
                }
            }

            BattleCommandExecutionFailureReason selectionFailure =
                ValidateMachineGunnerHandCardSelection(
                    command,
                    program,
                    out BattleHandCardSelectionRequest handCardSelectionRequest);
            if (selectionFailure != BattleCommandExecutionFailureReason.None)
            {
                return new BattleCardPlayEvaluation(
                    selectionFailure,
                    cardTemplate.TargetRule,
                    canStartInteraction: true,
                    canPayCost: true,
                    legalTargets,
                    requiresExplicitTargetInput:
                    program.TargetInputMode == MachineGunnerTargetInputMode.ExplicitEnemy,
                    handCardSelectionRequest: handCardSelectionRequest);
            }

            return new BattleCardPlayEvaluation(
                BattleCommandExecutionFailureReason.None,
                cardTemplate.TargetRule,
                canStartInteraction: true,
                canPayCost: true,
                legalTargets,
                requiresExplicitTargetInput:
                program.TargetInputMode == MachineGunnerTargetInputMode.ExplicitEnemy);
        }

        /// <summary>对需要额外手牌的职业程序重验选择数量、所属卡区与候选资格，并按需返回交互请求。</summary>
        private BattleCommandExecutionFailureReason ValidateMachineGunnerHandCardSelection(
            PlayCardCommand command,
            MachineGunnerCardProgram program,
            out BattleHandCardSelectionRequest request)
        {
            request = null;
            int requiredCount;
            switch (program.Id)
            {
                case cfg.battle.MachineGunnerProgramId.VentHeat:
                    requiredCount = 1;
                    break;
                case cfg.battle.MachineGunnerProgramId.Garrison:
                    requiredCount = 2;
                    break;
                default:
                    return BattleCommandExecutionFailureReason.None;
            }
            if (!_playerCardZones.TryGetValue(command.ActorId, out BattleCardZonesData cardZones) ||
                cardZones == null)
            {
                return BattleCommandExecutionFailureReason.PlayerCardZonesNotFound;
            }

            return BattleSingleOtherHandCardSelectionRules.Evaluate(
                command,
                cardZones,
                out request,
                requiredCount);
        }

        /// <summary>将机枪兵程序声明转换为一次稳定目标快照，自动目标只公开当前可用的最小输入集合。</summary>
        private IReadOnlyList<CombatantId> DeriveMachineGunnerTargets(
            MachineGunnerTargetInputMode targetInputMode,
            CombatantId actorId)
        {
            switch (targetInputMode)
            {
                case MachineGunnerTargetInputMode.Self:
                    return new[] { actorId };
                case MachineGunnerTargetInputMode.ExplicitEnemy:
                    return DeriveLegalTargets(cfg.battle.TargetRule.Enemy, actorId);
                case MachineGunnerTargetInputMode.AutomaticNearestEnemy:
                    IReadOnlyList<CombatantId> livingEnemies = DeriveLegalTargets(
                        cfg.battle.TargetRule.Enemy,
                        actorId);
                    return livingEnemies.Count == 0
                        ? NoTargets
                        : new[] { livingEnemies[0] };
                case MachineGunnerTargetInputMode.AutomaticNearestTwoEnemies:
                    IReadOnlyList<CombatantId> nearestCandidates = DeriveLegalTargets(
                        cfg.battle.TargetRule.Enemy,
                        actorId);
                    if (nearestCandidates.Count == 0)
                        return NoTargets;
                    if (nearestCandidates.Count == 1)
                        return new[] { nearestCandidates[0] };
                    return new[] { nearestCandidates[0], nearestCandidates[1] };
                case MachineGunnerTargetInputMode.AutomaticFurthestEnemy:
                    IReadOnlyList<CombatantId> furthestCandidates = DeriveLegalTargets(
                        cfg.battle.TargetRule.Enemy,
                        actorId);
                    return furthestCandidates.Count == 0
                        ? NoTargets
                        : new[] { furthestCandidates[furthestCandidates.Count - 1] };
                case MachineGunnerTargetInputMode.AllLivingEnemies:
                case MachineGunnerTargetInputMode.RandomLivingEnemy:
                    return DeriveLegalTargets(cfg.battle.TargetRule.Enemy, actorId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetInputMode));
            }
        }

        /// <summary>按静态目标规则和 Encounter 顺序派生一次性合法目标列表。</summary>
        private IReadOnlyList<CombatantId> DeriveLegalTargets(
            cfg.battle.TargetRule targetRule,
            CombatantId actorId)
        {
            if (targetRule == cfg.battle.TargetRule.Self)
                return new[] { actorId };

            var targets = new List<CombatantId>();
            foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
            {
                if (_combatants.TryGet(enemyId, out CombatantData combatant) &&
                    combatant is EnemyCombatantData &&
                    combatant.IsAlive)
                {
                    targets.Add(enemyId);
                }
            }

            return targets;
        }

        /// <summary>按派生快照顺序判断指定目标是否属于当前合法候选。</summary>
        private static bool ContainsTarget(
            IReadOnlyList<CombatantId> legalTargetIds,
            CombatantId targetId)
        {
            foreach (CombatantId legalTargetId in legalTargetIds)
            {
                if (legalTargetId == targetId)
                    return true;
            }

            return false;
        }

        /// <summary>判断当前唯一参与者映射中是否仍有存活玩家。</summary>
        private bool HasLivingPlayer()
        {
            foreach (CombatantData combatant in _combatants.All.Values)
            {
                if (combatant is PlayerCombatantData && combatant.IsAlive)
                    return true;
            }

            return false;
        }

        /// <summary>按 Encounter 顺序判断本场是否仍有存活敌人。</summary>
        private bool HasLivingEnemy()
        {
            foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
            {
                if (_combatants.TryGet(enemyId, out CombatantData combatant) &&
                    combatant is EnemyCombatantData &&
                    combatant.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>只读取当前手牌快照，判断实例是否仍属于该玩家手牌。</summary>
        private static bool IsCardInHand(BattleCardZonesData cardZones, CardInstanceId cardId)
        {
            foreach (CardInstanceId handCardId in cardZones.Hand)
            {
                if (handCardId == cardId)
                    return true;
            }

            return false;
        }

        /// <summary>创建尚未解析静态卡牌或目标规则时的空目标失败结果。</summary>
        private static BattleCardPlayEvaluation Failure(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleCardPlayEvaluation(
                failureReason,
                targetRule: null,
                canStartInteraction: false,
                canPayCost: false,
                NoTargets);
        }
    }
}
