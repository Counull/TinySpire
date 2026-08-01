using System;
using System.Collections.Generic;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>描述卡牌输入在当前事实下是禁用、仅视觉拖动还是可进入出牌语义。</summary>
    public enum HandCardInteractionMode
    {
        Disabled,
        VisualOnly,
        Playable
    }

    /// <summary>描述一张活动拖拽牌当前处于普通跟手、敌人瞄准或无活动拖拽。</summary>
    public enum HandCardDragPhase
    {
        Idle,
        Dragging,
        EnemyTargeting
    }

    /// <summary>集中表达事实变化后活动拖拽应保留、排除重排及清理哪些瞬时表现。</summary>
    public readonly struct HandCardDragTransition
    {
        public HandCardDragPhase NextPhase { get; }
        public bool PreserveActiveCard { get; }
        public bool ExcludeActiveCardFromLayout { get; }
        public bool ClearPlayFeedback { get; }
        public bool ClearTargetingPresentation { get; }
        public bool RebuildEnemyTargeting { get; }

        /// <summary>创建一次不写玩法事实的拖拽状态转换结果。</summary>
        internal HandCardDragTransition(
            HandCardDragPhase nextPhase,
            bool preserveActiveCard,
            bool excludeActiveCardFromLayout,
            bool clearPlayFeedback,
            bool clearTargetingPresentation,
            bool rebuildEnemyTargeting)
        {
            NextPhase = nextPhase;
            PreserveActiveCard = preserveActiveCard;
            ExcludeActiveCardFromLayout = excludeActiveCardFromLayout;
            ClearPlayFeedback = clearPlayFeedback;
            ClearTargetingPresentation = clearTargetingPresentation;
            RebuildEnemyTargeting = rebuildEnemyTargeting;
        }
    }

    /// <summary>把 CardZones 与 Turn 的先后发布收敛为一份可测试的活动拖拽转换决策。</summary>
    public static class HandCardDragTransitionPolicy
    {
        /// <summary>依据当前手牌归属、交互模式和目标规则决定下一步拖拽表现。</summary>
        public static HandCardDragTransition Resolve(
            HandCardDragPhase currentPhase,
            bool activeCardStillInHand,
            HandCardInteractionMode interactionMode,
            cfg.battle.TargetRule? targetRule)
        {
            if (!activeCardStillInHand || interactionMode == HandCardInteractionMode.Disabled)
            {
                return new HandCardDragTransition(
                    HandCardDragPhase.Idle,
                    preserveActiveCard: false,
                    excludeActiveCardFromLayout: false,
                    clearPlayFeedback: true,
                    clearTargetingPresentation: true,
                    rebuildEnemyTargeting: false);
            }

            if (interactionMode == HandCardInteractionMode.VisualOnly)
            {
                return new HandCardDragTransition(
                    HandCardDragPhase.Dragging,
                    preserveActiveCard: true,
                    excludeActiveCardFromLayout: true,
                    clearPlayFeedback: true,
                    clearTargetingPresentation: true,
                    rebuildEnemyTargeting: false);
            }

            if (currentPhase == HandCardDragPhase.EnemyTargeting)
            {
                if (targetRule != cfg.battle.TargetRule.Enemy)
                {
                    return new HandCardDragTransition(
                        HandCardDragPhase.Idle,
                        preserveActiveCard: false,
                        excludeActiveCardFromLayout: false,
                        clearPlayFeedback: true,
                        clearTargetingPresentation: true,
                        rebuildEnemyTargeting: false);
                }

                return new HandCardDragTransition(
                    HandCardDragPhase.EnemyTargeting,
                    preserveActiveCard: true,
                    excludeActiveCardFromLayout: true,
                    clearPlayFeedback: false,
                    clearTargetingPresentation: false,
                    rebuildEnemyTargeting: true);
            }

            return new HandCardDragTransition(
                HandCardDragPhase.Dragging,
                preserveActiveCard: true,
                excludeActiveCardFromLayout: true,
                clearPlayFeedback: false,
                clearTargetingPresentation: false,
                rebuildEnemyTargeting: false);
        }
    }

    /// <summary>把“可拿起拖动”的 UI 可供性与“可组成出牌目标”的规则许可分开。</summary>
    public static class HandCardInteractionAvailability
    {
        /// <summary>把规则许可与精确失败原因收敛为唯一 UI 交互模式。</summary>
        public static HandCardInteractionMode ResolveMode(
            bool canStartInteraction,
            BattleCommandExecutionFailureReason failureReason)
        {
            if (canStartInteraction)
                return HandCardInteractionMode.Playable;

            return failureReason == BattleCommandExecutionFailureReason.InsufficientEnergy
                ? HandCardInteractionMode.VisualOnly
                : HandCardInteractionMode.Disabled;
        }

        /// <summary>规则允许出牌或仅费用不足时可以拿起拖动；其他失败仍锁定输入。</summary>
        public static bool CanBeginDrag(
            bool canStartInteraction,
            BattleCommandExecutionFailureReason failureReason)
        {
            return ResolveMode(canStartInteraction, failureReason) !=
                   HandCardInteractionMode.Disabled;
        }

    }

    /// <summary>把规则 module 的一次性预览与当前屏幕命中组成松手目标，不保存玩法状态。</summary>
    public static class HandCardReleaseTargetResolver
    {
        /// <summary>Self 自动返回 Actor；Enemy 仅返回当前合法候选中的命中目标。</summary>
        public static CombatantId? Resolve(
            bool isPastPlayLine,
            cfg.battle.TargetRule? targetRule,
            bool canStartInteraction,
            CombatantId actorId,
            CombatantId? hoveredTargetId,
            IReadOnlyList<CombatantId> legalTargetIds)
        {
            if (legalTargetIds == null)
                throw new ArgumentNullException(nameof(legalTargetIds));
            if (!isPastPlayLine || !canStartInteraction || !targetRule.HasValue)
                return null;

            switch (targetRule.Value)
            {
                case cfg.battle.TargetRule.Self:
                    return Contains(legalTargetIds, actorId) ? actorId : (CombatantId?)null;
                case cfg.battle.TargetRule.Enemy:
                    return hoveredTargetId.HasValue && Contains(legalTargetIds, hoveredTargetId.Value)
                        ? hoveredTargetId
                        : null;
                default:
                    return null;
            }
        }

        /// <summary>按规则 module 给出的稳定候选快照判断目标是否仍在本次预览内。</summary>
        private static bool Contains(
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
    }
}
