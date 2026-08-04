using System.Reflection;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public sealed class HandCardReleaseTargetResolverTests
{
    /// <summary>确认活跃拖拽在 Presenter 同帧失效时由生产 Drag 入口立即取消，不继续瞬时瞄准。</summary>
    [Test]
    public void HandleDrag_WhenPresentationBecomesUnavailableBeforeUpdate_CancelsImmediately()
    {
        var containerObject = new GameObject("HandCardDragReadinessContainer");
        var cardObject = new GameObject("HandCardDragReadinessCard");
        HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
        HandCardVisual card = cardObject.AddComponent<HandCardVisual>();
        var eventData = new PointerEventData(eventSystem: null);
        try
        {
            SetPrivateField(container, "_draggingCard", card);
            SetPrivateField(container, "_dragPhase", HandCardDragPhase.Dragging);

            Assert.DoesNotThrow(() => container.HandleDrag(card, eventData));
            Assert.That(GetPrivateField<HandCardVisual>(container, "_draggingCard"), Is.Null);
            Assert.That(
                GetPrivateField<HandCardDragPhase>(container, "_dragPhase"),
                Is.EqualTo(HandCardDragPhase.Idle));
        }
        finally
        {
            Object.DestroyImmediate(containerObject);
            Object.DestroyImmediate(cardObject);
        }
    }

    /// <summary>确认活跃拖拽在 Presenter 同帧失效时由生产 EndDrag 入口立即取消，不进入提交依赖。</summary>
    [Test]
    public void HandleEndDrag_WhenPresentationBecomesUnavailableBeforeUpdate_CancelsImmediately()
    {
        var containerObject = new GameObject("HandCardReadinessContainer");
        var cardObject = new GameObject("HandCardReadinessCard");
        HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
        HandCardVisual card = cardObject.AddComponent<HandCardVisual>();
        try
        {
            SetPrivateField(container, "_draggingCard", card);
            SetPrivateField(container, "_dragPhase", HandCardDragPhase.Dragging);

            Assert.DoesNotThrow(() => container.HandleEndDrag(card, eventData: null));
            Assert.That(GetPrivateField<HandCardVisual>(container, "_draggingCard"), Is.Null);
            Assert.That(
                GetPrivateField<HandCardDragPhase>(container, "_dragPhase"),
                Is.EqualTo(HandCardDragPhase.Idle));
        }
        finally
        {
            Object.DestroyImmediate(containerObject);
            Object.DestroyImmediate(cardObject);
        }
    }

    /// <summary>确认参与者表现未就绪时禁用 Playable 与 VisualOnly 卡的系统指针拖拽。</summary>
    [Test]
    public void CanBeginDrag_WhenParticipantPresentationIsNotReady_RejectsSystemPointerOnly()
    {
        Assert.That(
            HandCardInteractionAvailability.CanBeginDrag(
                canStartInteraction: true,
                BattleCommandExecutionFailureReason.None,
                participantPresentationReady: false),
            Is.False);
        Assert.That(
            HandCardInteractionAvailability.CanBeginDrag(
                canStartInteraction: false,
                BattleCommandExecutionFailureReason.InsufficientEnergy,
                participantPresentationReady: false),
            Is.False);
        Assert.That(
            HandCardInteractionAvailability.CanBeginDrag(
                canStartInteraction: true,
                BattleCommandExecutionFailureReason.None,
                participantPresentationReady: true),
            Is.True);
    }

    /// <summary>验证卡区先发布时保留并排除拖拽牌，能量随后不足时降级并清理目标表现。</summary>
    [Test]
    public void ActiveDrag_WhenCardZonesThenEnergyChange_PreservesExcludesThenClearsTargeting()
    {
        HandCardDragTransition afterCardZones = HandCardDragTransitionPolicy.Resolve(
            HandCardDragPhase.EnemyTargeting,
            activeCardStillInHand: true,
            HandCardInteractionMode.Playable,
            cfg.battle.TargetRule.Enemy);

        Assert.That(afterCardZones.PreserveActiveCard, Is.True);
        Assert.That(afterCardZones.ExcludeActiveCardFromLayout, Is.True);
        Assert.That(afterCardZones.NextPhase, Is.EqualTo(HandCardDragPhase.EnemyTargeting));
        Assert.That(afterCardZones.ClearPlayFeedback, Is.False);
        Assert.That(afterCardZones.ClearTargetingPresentation, Is.False);
        Assert.That(afterCardZones.RebuildEnemyTargeting, Is.True);

        HandCardDragTransition afterEnergy = HandCardDragTransitionPolicy.Resolve(
            afterCardZones.NextPhase,
            activeCardStillInHand: true,
            HandCardInteractionMode.VisualOnly,
            cfg.battle.TargetRule.Enemy);

        Assert.That(afterEnergy.PreserveActiveCard, Is.True);
        Assert.That(afterEnergy.ExcludeActiveCardFromLayout, Is.True);
        Assert.That(afterEnergy.NextPhase, Is.EqualTo(HandCardDragPhase.Dragging));
        Assert.That(afterEnergy.ClearPlayFeedback, Is.True);
        Assert.That(afterEnergy.ClearTargetingPresentation, Is.True);
        Assert.That(afterEnergy.RebuildEnemyTargeting, Is.False);

        HandCardDragTransition afterDraggedCardLeaves = HandCardDragTransitionPolicy.Resolve(
            HandCardDragPhase.Dragging,
            activeCardStillInHand: false,
            HandCardInteractionMode.Playable,
            cfg.battle.TargetRule.Self);

        Assert.That(afterDraggedCardLeaves.PreserveActiveCard, Is.False);
        Assert.That(afterDraggedCardLeaves.ExcludeActiveCardFromLayout, Is.False);
        Assert.That(afterDraggedCardLeaves.NextPhase, Is.EqualTo(HandCardDragPhase.Idle));
        Assert.That(afterDraggedCardLeaves.ClearPlayFeedback, Is.True);
        Assert.That(afterDraggedCardLeaves.ClearTargetingPresentation, Is.True);
        Assert.That(afterDraggedCardLeaves.RebuildEnemyTargeting, Is.False);
    }

    /// <summary>验证拖动中费用变为不足时降级为纯视觉拖动，越线释放仍不能组成提交目标。</summary>
    [Test]
    public void InteractionMode_WhenEnergyBecomesInsufficient_DowngradesToVisualOnlyWithoutTarget()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);

            HandCardInteractionMode interactionMode = HandCardInteractionAvailability.ResolveMode(
                canStartInteraction: false,
                BattleCommandExecutionFailureReason.InsufficientEnergy);
            CombatantId? targetId = HandCardReleaseTargetResolver.Resolve(
                isPastPlayLine: true,
                cfg.battle.TargetRule.Self,
                canStartInteraction: false,
                player.Id,
                hoveredTargetId: null,
                new[] { player.Id });

            Assert.That(interactionMode, Is.EqualTo(HandCardInteractionMode.VisualOnly));
            Assert.That(targetId, Is.Null);
            Assert.That(
                HandCardInteractionAvailability.ResolveMode(
                    canStartInteraction: false,
                    BattleCommandExecutionFailureReason.InvalidTurnPhase),
                Is.EqualTo(HandCardInteractionMode.Disabled));
            Assert.That(
                HandCardInteractionAvailability.ResolveMode(
                    canStartInteraction: true,
                    BattleCommandExecutionFailureReason.TargetRequired),
                Is.EqualTo(HandCardInteractionMode.Playable));
        }
    }

    /// <summary>验证 Self 卡越线后自动选择 Actor，线内释放不会产生目标。</summary>
    [Test]
    public void Resolve_SelfCard_UsesActorOnlyAfterPlayLine()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            CombatantId? aboveLine = HandCardReleaseTargetResolver.Resolve(
                isPastPlayLine: true,
                cfg.battle.TargetRule.Self,
                canStartInteraction: true,
                player.Id,
                hoveredTargetId: null,
                new[] { player.Id });
            CombatantId? belowLine = HandCardReleaseTargetResolver.Resolve(
                isPastPlayLine: false,
                cfg.battle.TargetRule.Self,
                canStartInteraction: true,
                player.Id,
                hoveredTargetId: null,
                new[] { player.Id });

            Assert.That(aboveLine, Is.EqualTo(player.Id));
            Assert.That(belowLine, Is.Null);
        }
    }

    /// <summary>验证 Enemy 卡只接受规则候选中的精确命中，空白、玩家与非候选敌人均不产生目标。</summary>
    [Test]
    public void Resolve_EnemyCard_AcceptsOnlyExactLegalHit()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData legalEnemy = combatants.AddEnemy(201, 20, 0);
            EnemyCombatantData otherEnemy = combatants.AddEnemy(202, 20, 0);
            var legalTargets = new[] { legalEnemy.Id };

            CombatantId? exact = HandCardReleaseTargetResolver.Resolve(
                true,
                cfg.battle.TargetRule.Enemy,
                true,
                player.Id,
                legalEnemy.Id,
                legalTargets);
            CombatantId? blank = HandCardReleaseTargetResolver.Resolve(
                true,
                cfg.battle.TargetRule.Enemy,
                true,
                player.Id,
                hoveredTargetId: null,
                legalTargets);
            CombatantId? playerHit = HandCardReleaseTargetResolver.Resolve(
                true,
                cfg.battle.TargetRule.Enemy,
                true,
                player.Id,
                player.Id,
                legalTargets);
            CombatantId? nonCandidate = HandCardReleaseTargetResolver.Resolve(
                true,
                cfg.battle.TargetRule.Enemy,
                true,
                player.Id,
                otherEnemy.Id,
                legalTargets);

            Assert.That(exact, Is.EqualTo(legalEnemy.Id));
            Assert.That(blank, Is.Null);
            Assert.That(playerHit, Is.Null);
            Assert.That(nonCandidate, Is.Null);
        }
    }

    /// <summary>为生产 View 边界测试写入指定私有状态，不替代运行时系统指针验收。</summary>
    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }

    /// <summary>为生产 View 边界测试读取指定私有状态，不保存第二份运行时事实。</summary>
    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        return (T)field.GetValue(target);
    }
}
