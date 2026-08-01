using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;

namespace TinySpire.Battle
{
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

        /// <summary>按稳定规则顺序派生的一次性合法目标快照。</summary>
        public IReadOnlyList<CombatantId> LegalTargetIds { get; }

        /// <summary>冻结本次读取结果，避免调用方把派生目标改成可变镜像。</summary>
        internal BattleCardPlayEvaluation(
            BattleCommandExecutionFailureReason failureReason,
            cfg.battle.TargetRule? targetRule,
            bool canStartInteraction,
            bool canPayCost,
            IEnumerable<CombatantId> legalTargetIds)
        {
            if (legalTargetIds == null)
                throw new ArgumentNullException(nameof(legalTargetIds));

            FailureReason = failureReason;
            TargetRule = targetRule;
            CanStartInteraction = canStartInteraction;
            CanPayCost = canPayCost;
            LegalTargetIds = new ReadOnlyCollection<CombatantId>(
                new List<CombatantId>(legalTargetIds));
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

        /// <summary>保存规则派生所需的唯一权威事实入口与静态表。</summary>
        public BattleCardPlayRules(
            BattleCombatantsData combatants,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            Tables tables)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _playerCardZones = playerCardZones ?? throw new ArgumentNullException(nameof(playerCardZones));
            _enemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder
                ?? throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
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
            if (cardTemplate == null || cardTemplate.Cost < 0)
                return Failure(BattleCommandExecutionFailureReason.CardTemplateNotFound);

            bool canPayCost = playerTurn.Energy >= cardTemplate.Cost;
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
                cardTemplate.TargetRule != cfg.battle.TargetRule.Enemy)
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

            return new BattleCardPlayEvaluation(
                BattleCommandExecutionFailureReason.None,
                cardTemplate.TargetRule,
                canStartInteraction: true,
                canPayCost: true,
                legalTargets);
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
