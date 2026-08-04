using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class BattleCombatFeedbackTweenFactoryTests
{
    private readonly object _testTweenId = new object();

    /// <summary>精确注销本测试夹具直接创建但未交给 runner 的 Tween。</summary>
    [TearDown]
    public void TearDown()
    {
        DOTween.Kill(_testTweenId, complete: false);
    }

    /// <summary>确认全格挡只把冻结吸收量路由到精确目标，不伪造生命损失或抖动。</summary>
    [Test]
    public void TryCreate_FullyBlockedDamage_UsesFrozenAbsorbedAmountAndExactTargetWithoutShake()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var damage = new BattleDamageAppliedSettlement(
            order: 0,
            new BattleEffectId(4002),
            sourceId,
            targetId,
            attackValue: 6,
            blockBefore: 10,
            blockAfter: 4,
            healthBefore: 20,
            healthAfter: 20);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 1,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { damage });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var captured = new List<BattleCombatFeedbackCue>();
        var factory = new BattleCombatFeedbackTweenFactory(cue =>
        {
            captured.Add(cue);
            return new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => { }),
                cleanup: null);
        });

        Assert.That(plan.SettlementSteps, Has.Count.EqualTo(1));
        Assert.That(
            factory.TryCreate(plan.SettlementSteps[0], out BattleCommandPresentationTween tween),
            Is.True);

        Assert.That(tween, Is.Not.Null);
        Assert.That(captured, Has.Count.EqualTo(1));
        Assert.That(captured[0].TargetId, Is.EqualTo(targetId));
        Assert.That(
            captured[0].Kind,
            Is.EqualTo(BattleCommandPresentationStepKind.BlockAbsorbedNumber));
        Assert.That(captured[0].Amount, Is.EqualTo(6));
    }

    /// <summary>确认格挡溢出只展示冻结实际量，并在数字之后产生无数值抖动提示。</summary>
    [Test]
    public void TryCreate_BlockOverflow_UsesFrozenBlockAndHealthLossBeforeShake()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var damage = new BattleDamageAppliedSettlement(
            order: 0,
            new BattleEffectId(4003),
            sourceId,
            targetId,
            attackValue: 12,
            blockBefore: 5,
            blockAfter: 0,
            healthBefore: 40,
            healthAfter: 33);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 2,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { damage });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var captured = new List<BattleCombatFeedbackCue>();
        var factory = new BattleCombatFeedbackTweenFactory(cue =>
        {
            captured.Add(cue);
            return new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => { }),
                cleanup: null);
        });

        Assert.That(plan.SettlementSteps, Has.Count.EqualTo(3));
        foreach (BattleCommandPresentationStep step in plan.SettlementSteps)
        {
            Assert.That(
                factory.TryCreate(step, out BattleCommandPresentationTween tween),
                Is.True);
            Assert.That(tween, Is.Not.Null);
        }

        Assert.That(captured, Has.Count.EqualTo(3));
        Assert.That(captured[0].TargetId, Is.EqualTo(targetId));
        Assert.That(captured[0].Kind, Is.EqualTo(BattleCommandPresentationStepKind.BlockAbsorbedNumber));
        Assert.That(captured[0].Amount, Is.EqualTo(5));
        Assert.That(captured[1].TargetId, Is.EqualTo(targetId));
        Assert.That(captured[1].Kind, Is.EqualTo(BattleCommandPresentationStepKind.HealthLossNumber));
        Assert.That(captured[1].Amount, Is.EqualTo(7));
        Assert.That(captured[2].TargetId, Is.EqualTo(targetId));
        Assert.That(captured[2].Kind, Is.EqualTo(BattleCommandPresentationStepKind.HitShake));
        Assert.That(captured[2].Amount, Is.Zero);
    }

    /// <summary>确认无格挡普通伤害只展示冻结生命损失并随后抖动。</summary>
    [Test]
    public void TryCreate_HealthOnlyDamage_UsesFrozenLossThenShake()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var damage = new BattleDamageAppliedSettlement(
            order: 0,
            new BattleEffectId(4004),
            sourceId,
            targetId,
            attackValue: 6,
            blockBefore: 0,
            blockAfter: 0,
            healthBefore: 20,
            healthAfter: 14);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 6,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { damage });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var captured = new List<BattleCombatFeedbackCue>();
        var factory = new BattleCombatFeedbackTweenFactory(cue =>
        {
            captured.Add(cue);
            return new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => { }),
                cleanup: null);
        });

        foreach (BattleCommandPresentationStep step in plan.SettlementSteps)
            Assert.That(factory.TryCreate(step, out _), Is.True);

        Assert.That(captured, Has.Count.EqualTo(2));
        AssertCue(captured[0], targetId, BattleCommandPresentationStepKind.HealthLossNumber, 6);
        AssertCue(captured[1], targetId, BattleCommandPresentationStepKind.HitShake, 0);
    }

    /// <summary>确认格挡、力量、易伤和意图提示只路由到冻结记录指定的参与者。</summary>
    [Test]
    public void TryCreate_BlockStrengthVulnerableAndIntent_UseExactTargetOrSource()
    {
        var sourceId = new CombatantId(1001);
        var firstTargetId = new CombatantId(2001);
        var secondTargetId = new CombatantId(2002);
        var records = new BattleSettlementRecord[]
        {
            new BattleBlockGainedSettlement(
                order: 0,
                new BattleEffectId(4101),
                sourceId,
                firstTargetId,
                blockBefore: 0,
                blockAfter: 5),
            new BattleAttributeModifiedSettlement(
                order: 1,
                new BattleEffectId(4102),
                sourceId,
                secondTargetId,
                BattleAttributeType.Strength,
                valueBefore: 0,
                valueAfter: 3),
            new BattleStatusAppliedSettlement(
                order: 2,
                new BattleEffectId(4103),
                sourceId,
                firstTargetId,
                BattleStatusType.Vulnerable,
                valueBefore: 0,
                valueAfter: 2),
            new BattleStatusReducedSettlement(
                order: 3,
                secondTargetId,
                BattleStatusType.Vulnerable,
                valueBefore: 2,
                valueAfter: 1),
            new BattleEnemyIntentAdvancedSettlement(
                order: 4,
                secondTargetId,
                completedBehaviorId: 501,
                nextBehaviorId: 502),
        };
        var result = new BattleCommandExecutionResult(
            authoritySequence: 3,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            records);
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var captured = new List<BattleCombatFeedbackCue>();
        var factory = new BattleCombatFeedbackTweenFactory(cue =>
        {
            captured.Add(cue);
            return new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => { }),
                cleanup: null);
        });

        foreach (BattleCommandPresentationStep step in plan.SettlementSteps)
            Assert.That(factory.TryCreate(step, out _), Is.True);

        Assert.That(captured, Has.Count.EqualTo(5));
        AssertCue(captured[0], firstTargetId, BattleCommandPresentationStepKind.BlockGainedNumber, 5);
        AssertCue(captured[1], secondTargetId, BattleCommandPresentationStepKind.StrengthIconPulse, 0);
        AssertCue(captured[2], firstTargetId, BattleCommandPresentationStepKind.VulnerableIconPulse, 0);
        AssertCue(captured[3], secondTargetId, BattleCommandPresentationStepKind.VulnerableIconPulse, 0);
        AssertCue(captured[4], secondTargetId, BattleCommandPresentationStepKind.EnemyIntentPulse, 0);
        Assert.That(captured[1].FrozenValue, Is.EqualTo(3));
        Assert.That(captured[2].FrozenValue, Is.EqualTo(2));
        Assert.That(captured[3].FrozenValue, Is.EqualTo(1));
        Assert.That(captured[4].FrozenValue, Is.EqualTo(502));
    }

    /// <summary>确认致命伤害在生命数字和抖动之后才路由同一目标的死亡过渡。</summary>
    [Test]
    public void TryCreate_FatalDamage_RoutesDeathAfterHealthLossAndShake()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var damage = new BattleDamageAppliedSettlement(
            order: 0,
            new BattleEffectId(4201),
            sourceId,
            targetId,
            attackValue: 6,
            blockBefore: 0,
            blockAfter: 0,
            healthBefore: 6,
            healthAfter: 0);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 4,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { damage });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var captured = new List<BattleCombatFeedbackCue>();
        var factory = new BattleCombatFeedbackTweenFactory(cue =>
        {
            captured.Add(cue);
            return new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => { }),
                cleanup: null);
        });

        foreach (BattleCommandPresentationStep step in plan.SettlementSteps)
            Assert.That(factory.TryCreate(step, out _), Is.True);

        Assert.That(captured, Has.Count.EqualTo(3));
        AssertCue(captured[0], targetId, BattleCommandPresentationStepKind.HealthLossNumber, 6);
        AssertCue(captured[1], targetId, BattleCommandPresentationStepKind.HitShake, 0);
        AssertCue(captured[2], targetId, BattleCommandPresentationStepKind.DeathTransition, 0);
    }

    /// <summary>确认两目标多记录严格串行，且 M9C factory 不消费终局步骤。</summary>
    [Test]
    public void Play_OrderedCombatFeedbackAcrossTwoTargets_DoesNotConsumeOutcome()
    {
        var firstTargetId = new CombatantId(2001);
        var secondTargetId = new CombatantId(2002);
        var sourceId = new CombatantId(1001);
        var records = new BattleSettlementRecord[]
        {
            new BattleDamageAppliedSettlement(
                order: 0,
                new BattleEffectId(4401),
                sourceId,
                firstTargetId,
                attackValue: 7,
                blockBefore: 2,
                blockAfter: 0,
                healthBefore: 5,
                healthAfter: 0),
            new BattleBlockGainedSettlement(
                order: 1,
                new BattleEffectId(4402),
                secondTargetId,
                secondTargetId,
                blockBefore: 0,
                blockAfter: 4),
            new BattleAttributeModifiedSettlement(
                order: 2,
                new BattleEffectId(4403),
                secondTargetId,
                secondTargetId,
                BattleAttributeType.Strength,
                valueBefore: 0,
                valueAfter: 2),
            new BattleStatusAppliedSettlement(
                order: 3,
                new BattleEffectId(4404),
                sourceId,
                secondTargetId,
                BattleStatusType.Vulnerable,
                valueBefore: 0,
                valueAfter: 1),
            new BattleEnemyIntentAdvancedSettlement(
                order: 4,
                secondTargetId,
                completedBehaviorId: 501,
                nextBehaviorId: 502),
            new BattlePhaseChangedSettlement(
                order: 5,
                BattleTurnPhase.EnemyAction,
                BattleTurnPhase.BattleEnded,
                roundNumberBefore: 1,
                roundNumberAfter: 1,
                currentActingEnemyIdBefore: secondTargetId,
                currentActingEnemyIdAfter: null),
        };
        var result = new BattleCommandExecutionResult(
            authoritySequence: 5,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            records);
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var played = new List<(CombatantId TargetId, BattleCommandPresentationStepKind Kind, int Amount)>();
        var fallbackPlayed = new List<BattleCommandPresentationStepKind>();
        var factory = new BattleCombatFeedbackTweenFactory(cue =>
            new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() =>
                    played.Add((cue.TargetId, cue.Kind, cue.Amount))),
                cleanup: null));
        using var runner = new BattleCommandPresentationRunner(
            _ => throw new AssertionException("CompleteEnemyAction 不得建立命令前奏。"),
            step =>
            {
                if (factory.TryCreate(step, out BattleCommandPresentationTween tween))
                    return tween;
                return new BattleCommandPresentationTween(
                    CreateTestSequence().AppendCallback(() => fallbackPlayed.Add(step.Kind)),
                    cleanup: null);
            });
        var completionCount = 0;

        runner.Play(plan, () => completionCount++);
        runner.CompleteImmediately();

        Assert.That(
            played,
            Is.EqualTo(new[]
            {
                (firstTargetId, BattleCommandPresentationStepKind.BlockAbsorbedNumber, 2),
                (firstTargetId, BattleCommandPresentationStepKind.HealthLossNumber, 5),
                (firstTargetId, BattleCommandPresentationStepKind.HitShake, 0),
                (firstTargetId, BattleCommandPresentationStepKind.DeathTransition, 0),
                (secondTargetId, BattleCommandPresentationStepKind.BlockGainedNumber, 4),
                (secondTargetId, BattleCommandPresentationStepKind.StrengthIconPulse, 0),
                (secondTargetId, BattleCommandPresentationStepKind.VulnerableIconPulse, 0),
                (secondTargetId, BattleCommandPresentationStepKind.EnemyIntentPulse, 0),
            }));
        Assert.That(
            fallbackPlayed,
            Is.EqualTo(new[] { BattleCommandPresentationStepKind.BattleOutcome }));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认致命数字、抖动、死亡、终局面板与唯一 completion 共用同一严格串行时间线。</summary>
    [Test]
    public void Play_FatalDamageThenOutcome_PlaysAllVisibleFeedbackBeforeCompletion()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var records = new BattleSettlementRecord[]
        {
            new BattleDamageAppliedSettlement(
                order: 0,
                new BattleEffectId(4501),
                sourceId,
                targetId,
                attackValue: 6,
                blockBefore: 0,
                blockAfter: 0,
                healthBefore: 6,
                healthAfter: 0),
            new BattlePhaseChangedSettlement(
                order: 1,
                BattleTurnPhase.EnemyAction,
                BattleTurnPhase.BattleEnded,
                roundNumberBefore: 2,
                roundNumberAfter: 2,
                currentActingEnemyIdBefore: targetId,
                currentActingEnemyIdAfter: null),
        };
        var result = new BattleCommandExecutionResult(
            authoritySequence: 6,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            records);
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var playbackOrder = new List<string>();
        var combatFactory = new BattleCombatFeedbackTweenFactory(cue =>
            new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => playbackOrder.Add(cue.Kind.ToString())),
                cleanup: null));
        var flowFactory = new BattleFlowFeedbackTweenFactory(
            cue => new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => playbackOrder.Add(cue.Kind.ToString())),
                cleanup: null),
            () => "battle.ui.result.victory");
        using var runner = new BattleCommandPresentationRunner(
            _ => throw new AssertionException("CompleteEnemyAction 不得建立命令前奏。"),
            step =>
            {
                if (flowFactory.TryCreate(step, out BattleCommandPresentationTween tween))
                    return tween;
                if (combatFactory.TryCreate(step, out tween))
                    return tween;
                throw new AssertionException($"未消费的表现步骤：{step.Kind}");
            });
        int completionCount = 0;

        runner.Play(plan, () =>
        {
            completionCount++;
            playbackOrder.Add("Completion");
        });

        Assert.That(playbackOrder, Is.Empty);

        runner.CompleteImmediately();
        runner.CompleteImmediately();

        Assert.That(
            playbackOrder,
            Is.EqualTo(new[]
            {
                "HealthLossNumber",
                "HitShake",
                "DeathTransition",
                "BattleOutcome",
                "Completion",
            }));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认卡牌、横幅与终局步骤留给后续切片，M9C 不创建任何战斗反馈 View。</summary>
    [Test]
    public void TryCreate_FutureStepsReturnFalseWithoutViewCreation()
    {
        var moved = new BattleCardMovedSettlement(
            order: 0,
            new CardInstanceId(61),
            BattleCardZone.DrawPile,
            BattleCardZone.Hand);
        var phaseChanged = new BattlePhaseChangedSettlement(
            order: 1,
            BattleTurnPhase.EnemyAction,
            BattleTurnPhase.BattleEnded,
            roundNumberBefore: 1,
            roundNumberAfter: 1,
            currentActingEnemyIdBefore: new CombatantId(2001),
            currentActingEnemyIdAfter: null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 7,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { moved, phaseChanged });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var createCount = 0;
        var factory = new BattleCombatFeedbackTweenFactory(_ =>
        {
            createCount++;
            return new BattleCommandPresentationTween(
                CreateTestSequence().AppendCallback(() => { }),
                cleanup: null);
        });

        foreach (BattleCommandPresentationStep step in plan.SettlementSteps)
            Assert.That(factory.TryCreate(step, out _), Is.False);

        Assert.That(createCount, Is.Zero);
        Assert.That(
            plan.SettlementSteps[0].Kind,
            Is.EqualTo(BattleCommandPresentationStepKind.CardMoved));
        Assert.That(
            plan.SettlementSteps[1].Kind,
            Is.EqualTo(BattleCommandPresentationStepKind.BattleOutcome));
    }

    /// <summary>确认一条 concrete cue 的目标、类别与冻结显示量完全匹配。</summary>
    private static void AssertCue(
        BattleCombatFeedbackCue cue,
        CombatantId expectedTargetId,
        BattleCommandPresentationStepKind expectedKind,
        int expectedAmount)
    {
        Assert.That(cue.TargetId, Is.EqualTo(expectedTargetId));
        Assert.That(cue.Kind, Is.EqualTo(expectedKind));
        Assert.That(cue.Amount, Is.EqualTo(expectedAmount));
    }

    /// <summary>创建带夹具私有标识的测试 Sequence，便于 TearDown 精确回收 orphan。</summary>
    private Sequence CreateTestSequence()
    {
        return DOTween.Sequence().SetId(_testTweenId);
    }
}
