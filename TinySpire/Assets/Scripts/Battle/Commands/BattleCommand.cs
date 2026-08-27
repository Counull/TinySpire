using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TinySpire.Run;

namespace TinySpire.Battle
{
    /// <summary>Queue 内部触发的免费出牌请求；冻结来源卡区、强制归宿和递归深度。</summary>
    internal sealed class BattleTriggeredCardPlayRequest
    {
        internal const int MaximumDepth = 32;

        internal CombatantId ActorId { get; }
        internal CardInstanceId CardId { get; }
        internal CombatantId? TargetId { get; }
        internal BattleCardZone SourceZone { get; }
        internal BattleCardPaymentMode PaymentMode { get; }
        internal BattleCardZone Destination { get; }
        internal int Depth { get; }
        internal BattleCommand ContinuationAfterPlay { get; }
        internal int? SuppressedSettlementTriggerRegistrationId { get; }

        /// <summary>冻结一次只能由 Queue 消费的触发出牌请求，并拒绝无效身份、模式或深度。</summary>
        internal BattleTriggeredCardPlayRequest(
            CombatantId actorId,
            CardInstanceId cardId,
            CombatantId? targetId,
            BattleCardZone sourceZone,
            BattleCardPaymentMode paymentMode,
            BattleCardZone destination,
            int depth,
            BattleCommand continuationAfterPlay = null,
            int? suppressedSettlementTriggerRegistrationId = null)
        {
            if (actorId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(actorId));
            if (cardId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardId));
            if (targetId.HasValue && targetId.Value.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetId));
            if (sourceZone != BattleCardZone.Hand && sourceZone != BattleCardZone.DrawPile)
                throw new ArgumentOutOfRangeException(nameof(sourceZone));
            if (paymentMode != BattleCardPaymentMode.Waived)
                throw new ArgumentOutOfRangeException(nameof(paymentMode));
            if (destination != BattleCardZone.DiscardPile &&
                destination != BattleCardZone.ExhaustPile &&
                destination != BattleCardZone.PowerPile)
            {
                throw new ArgumentOutOfRangeException(nameof(destination));
            }
            if (depth <= 0 || depth > MaximumDepth)
                throw new ArgumentOutOfRangeException(nameof(depth));
            if (suppressedSettlementTriggerRegistrationId.HasValue &&
                suppressedSettlementTriggerRegistrationId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(suppressedSettlementTriggerRegistrationId));
            }

            ActorId = actorId;
            CardId = cardId;
            TargetId = targetId;
            SourceZone = sourceZone;
            PaymentMode = paymentMode;
            Destination = destination;
            Depth = depth;
            ContinuationAfterPlay = continuationAfterPlay;
            SuppressedSettlementTriggerRegistrationId =
                suppressedSettlementTriggerRegistrationId;
        }

        /// <summary>复制冻结请求并附加父触发批次的后续命令与递归抑制身份。</summary>
        internal BattleTriggeredCardPlayRequest WithContinuation(
            BattleCommand continuationAfterPlay,
            int? suppressedSettlementTriggerRegistrationId)
        {
            return new BattleTriggeredCardPlayRequest(
                ActorId,
                CardId,
                TargetId,
                SourceZone,
                PaymentMode,
                Destination,
                Depth,
                continuationAfterPlay,
                suppressedSettlementTriggerRegistrationId ??
                    SuppressedSettlementTriggerRegistrationId);
        }
    }

    /// <summary>
    /// 战斗命令的稳定类型；调用方只表达意图，不携带执行期规则结果。
    /// </summary>
    public enum BattleCommandType
    {
        StartBattle,
        PlayCard,
        EndPlayerAction,
        CompleteEnemyAction,
        ResolveSettlementTriggers,
        UsePotion,
    }

    /// <summary>表达玩家尝试消费一个 Run 药水稳定实例的意图。</summary>
    public sealed class UsePotionCommand : BattleCommand
    {
        /// <summary>唯一允许调用方携带的 Run 药水实例身份。</summary>
        public RunPotionInstanceId PotionInstanceId { get; }

        /// <summary>返回使用药水命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.UsePotion;

        /// <summary>药水归属由 Battle ledger 终审，命令本身不携带参与者。</summary>
        public override CombatantId? SubmitterId => null;

        /// <summary>创建只含稳定药水实例身份的玩家意图。</summary>
        public UsePotionCommand(RunPotionInstanceId potionInstanceId)
        {
            if (potionInstanceId.Sequence <= 0)
            {
                throw new ArgumentException(
                    "Run potion instance id cannot be empty.",
                    nameof(potionInstanceId));
            }

            PotionInstanceId = potionInstanceId;
        }
    }

    /// <summary>
    /// 所有共享战斗写入意图的抽象根。
    /// </summary>
    public abstract class BattleCommand
    {
        /// <summary>命令的稳定领域类型。</summary>
        public abstract BattleCommandType Type { get; }

        /// <summary>提交该命令的参与者；系统命令没有提交者。</summary>
        public abstract CombatantId? SubmitterId { get; }
    }

    /// <summary>
    /// 表达初始化战斗回合事实的系统命令。
    /// </summary>
    public sealed class StartBattleCommand : BattleCommand
    {
        /// <summary>返回开始战斗命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.StartBattle;

        /// <summary>开始战斗由系统提交，因此没有参与者提交者。</summary>
        public override CombatantId? SubmitterId => null;
    }

    /// <summary>
    /// 表达玩家尝试打出一张运行时卡牌实例的意图。
    /// </summary>
    public sealed class PlayCardCommand : BattleCommand
    {
        /// <summary>尝试出牌的玩家。</summary>
        public CombatantId ActorId { get; }

        /// <summary>尝试打出的运行时卡牌实例。</summary>
        public CardInstanceId CardId { get; }

        /// <summary>调用方选择的单个运行时目标；缺失目标由执行期规则明确拒绝。</summary>
        public CombatantId? TargetId { get; }

        /// <summary>调用方为当前卡牌效果选择的手牌实例快照。</summary>
        public IReadOnlyList<CardInstanceId> SelectedCardIds { get; }

        /// <summary>仅 Queue 内部 continuation 可携带的冻结触发出牌请求。</summary>
        internal BattleTriggeredCardPlayRequest TriggeredPlayRequest { get; }

        /// <summary>指示本命令是否由 Queue 从冻结触发请求构造。</summary>
        internal bool IsTriggeredPlay => TriggeredPlayRequest != null;

        /// <summary>返回出牌命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.PlayCard;

        /// <summary>返回尝试出牌的玩家。</summary>
        public override CombatantId? SubmitterId => ActorId;

        /// <summary>创建出牌意图并拒绝无效参与者、卡牌实例或非空目标标识。</summary>
        public PlayCardCommand(
            CombatantId actorId,
            CardInstanceId cardId,
            CombatantId? targetId)
            : this(actorId, cardId, targetId, Array.Empty<CardInstanceId>())
        {
        }

        /// <summary>创建带不可变手牌选择快照的出牌意图，并防御性复制调用方集合。</summary>
        public PlayCardCommand(
            CombatantId actorId,
            CardInstanceId cardId,
            CombatantId? targetId,
            IEnumerable<CardInstanceId> selectedCardIds)
            : this(actorId, cardId, targetId, selectedCardIds, triggeredPlayRequest: null)
        {
        }

        /// <summary>把冻结触发请求转换为只能凭系统 token 执行的内部出牌命令。</summary>
        internal PlayCardCommand(BattleTriggeredCardPlayRequest triggeredPlayRequest)
            : this(
                triggeredPlayRequest?.ActorId ?? default,
                triggeredPlayRequest?.CardId ?? default,
                triggeredPlayRequest?.TargetId,
                Array.Empty<CardInstanceId>(),
                triggeredPlayRequest)
        {
        }

        /// <summary>统一冻结外部普通出牌或 Queue 内部触发出牌的不可变字段。</summary>
        private PlayCardCommand(
            CombatantId actorId,
            CardInstanceId cardId,
            CombatantId? targetId,
            IEnumerable<CardInstanceId> selectedCardIds,
            BattleTriggeredCardPlayRequest triggeredPlayRequest)
        {
            if (actorId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(actorId));
            if (cardId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardId));
            if (targetId.HasValue && targetId.Value.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetId));
            if (selectedCardIds == null)
                throw new ArgumentNullException(nameof(selectedCardIds));

            var frozenSelectedCardIds = new List<CardInstanceId>(selectedCardIds);
            foreach (CardInstanceId selectedCardId in frozenSelectedCardIds)
            {
                if (selectedCardId.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(selectedCardIds));
            }

            ActorId = actorId;
            CardId = cardId;
            TargetId = targetId;
            SelectedCardIds = new ReadOnlyCollection<CardInstanceId>(frozenSelectedCardIds);
            TriggeredPlayRequest = triggeredPlayRequest;
        }
    }

    /// <summary>
    /// 表达单名玩家结束本轮行动的意图。
    /// </summary>
    public sealed class EndPlayerActionCommand : BattleCommand
    {
        /// <summary>声明结束行动的玩家。</summary>
        public CombatantId ActorId { get; }

        /// <summary>返回结束玩家行动命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.EndPlayerAction;

        /// <summary>返回声明结束行动的玩家。</summary>
        public override CombatantId? SubmitterId => ActorId;

        /// <summary>创建玩家结束行动意图并拒绝无效参与者标识。</summary>
        public EndPlayerActionCommand(CombatantId actorId)
        {
            if (actorId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(actorId));

            ActorId = actorId;
        }
    }

    /// <summary>
    /// 表达当前敌人权威行动已经完成的系统意图。
    /// </summary>
    public sealed class CompleteEnemyActionCommand : BattleCommand
    {
        /// <summary>完成当前行动的敌人。</summary>
        public CombatantId EnemyId { get; }

        /// <summary>返回完成敌人行动命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.CompleteEnemyAction;

        /// <summary>敌人行动完成由系统提交，因此没有参与者提交者。</summary>
        public override CombatantId? SubmitterId => null;

        /// <summary>创建敌人行动完成意图并拒绝无效参与者标识。</summary>
        public CompleteEnemyActionCommand(CombatantId enemyId)
        {
            if (enemyId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(enemyId));

            EnemyId = enemyId;
        }
    }
}
