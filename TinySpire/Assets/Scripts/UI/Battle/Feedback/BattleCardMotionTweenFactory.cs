using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>M9E 当前已接线的冻结卡牌运动提示类别。</summary>
    internal enum BattleCardMotionCueKind
    {
        PlayCardTransientHold,
        HandToDiscard,
        HandToExhaust,
        DrawToHand,
        CreatedToHand,
        CardsReshuffled,
    }

    /// <summary>只携带前奏或 settlement 已冻结身份的卡牌运动提示。</summary>
    internal sealed class BattleCardMotionCue
    {
        /// <summary>本提示对应的运动类别。</summary>
        public BattleCardMotionCueKind Kind { get; }

        /// <summary>应移动的精确运行时卡牌实例。</summary>
        public CardInstanceId? CardId { get; }

        /// <summary>settlement 派生运动的原始 Order；命令级前奏为空。</summary>
        public int? SettlementOrder { get; }

        /// <summary>重洗记录冻结的新抽牌堆顺序；其他运动为空只读集合。</summary>
        public IReadOnlyList<CardInstanceId> NewDrawPileOrder { get; }

        /// <summary>冻结一条不保存 Hand 或 CardZones 镜像的卡牌运动提示。</summary>
        internal BattleCardMotionCue(
            BattleCardMotionCueKind kind,
            CardInstanceId? cardId,
            int? settlementOrder,
            IEnumerable<CardInstanceId> newDrawPileOrder = null)
        {
            Kind = kind;
            CardId = cardId;
            SettlementOrder = settlementOrder;
            NewDrawPileOrder = new ReadOnlyCollection<CardInstanceId>(
                new List<CardInstanceId>(newDrawPileOrder ?? Array.Empty<CardInstanceId>()));
        }
    }

    /// <summary>把 M9E 的 transient 持有与冻结卡区步骤路由到同一 presentation runner。</summary>
    internal sealed class BattleCardMotionTweenFactory
    {
        private readonly Func<BattleCardMotionCue, BattleCommandPresentationTween> _createTween;

        /// <summary>保存由现有 Hand、Pile 与 Participant View 提供的 concrete Tween 创建入口。</summary>
        internal BattleCardMotionTweenFactory(
            Func<BattleCardMotionCue, BattleCommandPresentationTween> createTween)
        {
            _createTween = createTween ?? throw new ArgumentNullException(nameof(createTween));
        }

        /// <summary>只消费带冻结卡牌身份的 PlayCard 前奏，持有离手 transient 但不移动到目标。</summary>
        internal bool TryCreate(
            BattleCommandPrelude prelude,
            out BattleCommandPresentationTween tween)
        {
            if (prelude == null)
                throw new ArgumentNullException(nameof(prelude));

            tween = null;
            if (prelude.Kind != BattleCommandPreludeKind.PlayCard)
                return false;
            if (!prelude.CardId.HasValue)
            {
                throw new InvalidOperationException(
                    "PlayCard transient hold prelude requires a frozen card identity.");
            }

            var cue = new BattleCardMotionCue(
                BattleCardMotionCueKind.PlayCardTransientHold,
                prelude.CardId.Value,
                settlementOrder: null);
            tween = CreateTween(cue);
            return true;
        }

        /// <summary>消费冻结的手牌离场、抽牌入手与临时创建入手事实；其他步骤仍交给同一 adapter 分派。</summary>
        internal bool TryCreate(
            BattleCommandPresentationStep step,
            out BattleCommandPresentationTween tween)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));

            tween = null;
            if (step.Kind == BattleCommandPresentationStepKind.CardMoved)
            {
                if (!(step.Settlement is BattleCardMovedSettlement moved))
                    throw CreateSettlementMismatch(step);

                BattleCardMotionCueKind? kind = null;
                if (moved.FromZone == BattleCardZone.Hand &&
                    moved.ToZone == BattleCardZone.DiscardPile)
                {
                    kind = BattleCardMotionCueKind.HandToDiscard;
                }
                else if (moved.FromZone == BattleCardZone.Hand &&
                         moved.ToZone == BattleCardZone.ExhaustPile)
                {
                    kind = BattleCardMotionCueKind.HandToExhaust;
                }
                else if (moved.FromZone == BattleCardZone.DrawPile &&
                         moved.ToZone == BattleCardZone.Hand)
                {
                    kind = BattleCardMotionCueKind.DrawToHand;
                }

                if (!kind.HasValue)
                    return false;

                var movedCue = new BattleCardMotionCue(
                    kind.Value,
                    moved.CardId,
                    settlementOrder: step.SettlementOrder);
                tween = CreateTween(movedCue);
                return true;
            }

            if (step.Kind == BattleCommandPresentationStepKind.CardCreated)
            {
                if (!(step.Settlement is BattleCardCreatedSettlement created))
                    throw CreateSettlementMismatch(step);
                if (created.ToZone != BattleCardZone.Hand)
                    return false;

                var createdCue = new BattleCardMotionCue(
                    BattleCardMotionCueKind.CreatedToHand,
                    created.CardId,
                    settlementOrder: step.SettlementOrder);
                tween = CreateTween(createdCue);
                return true;
            }

            if (step.Kind == BattleCommandPresentationStepKind.CardsReshuffled)
            {
                if (!(step.Settlement is BattleCardsReshuffledSettlement reshuffled))
                    throw CreateSettlementMismatch(step);

                var reshuffleCue = new BattleCardMotionCue(
                    BattleCardMotionCueKind.CardsReshuffled,
                    cardId: null,
                    settlementOrder: step.SettlementOrder,
                    newDrawPileOrder: reshuffled.NewDrawPileOrder);
                tween = CreateTween(reshuffleCue);
                return true;
            }

            return false;
        }

        /// <summary>校验 concrete 边界始终返回一个由 runner 统一拥有的 cue lease。</summary>
        private BattleCommandPresentationTween CreateTween(BattleCardMotionCue cue)
        {
            return _createTween.Invoke(cue)
                ?? throw new InvalidOperationException("Card motion tween factory returned null.");
        }

        /// <summary>为计划类别与冻结 settlement 类型不匹配建立一致的同步 fault。</summary>
        private static InvalidOperationException CreateSettlementMismatch(
            BattleCommandPresentationStep step)
        {
            return new InvalidOperationException(
                $"Card motion {step.Kind} cannot consume {step.Settlement.GetType().Name}.");
        }
    }
}
