using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class HandCardReleaseTargetResolverTests
{
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
}
