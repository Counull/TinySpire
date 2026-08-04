using System.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class BattleCommandPresentationPlanTests
{
    /// <summary>确认战斗开始前奏独立于 settlement，并且严格先于 Order 0、1 的可见步骤。</summary>
    [Test]
    public void Create_StartBattle_PrependsExactlyOnePreludeBeforeOrderedSettlementSteps()
    {
        var moved = new BattleCardMovedSettlement(
            order: 0,
            new CardInstanceId(1),
            BattleCardZone.DrawPile,
            BattleCardZone.Hand);
        var phaseChanged = new BattlePhaseChangedSettlement(
            order: 1,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            roundNumberBefore: 0,
            roundNumberAfter: 1,
            currentActingEnemyIdBefore: null,
            currentActingEnemyIdAfter: null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 1,
            BattleCommandType.StartBattle,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { moved, phaseChanged });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(plan.Prelude, Is.Not.Null);
        Assert.That(plan.Prelude.Kind, Is.EqualTo(BattleCommandPreludeKind.StartBattle));
        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex, item.Settlement)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.CardMoved, 0, 0, (BattleSettlementRecord)moved),
                (BattleCommandPresentationStepKind.PlayerTurnBanner, 1, 0, (BattleSettlementRecord)phaseChanged),
            }));
    }

    /// <summary>确认单段伤害出牌只生成一个前奏，且能量、Effect 与离手记录保持原始 Order。</summary>
    [Test]
    public void Create_StrikePlayCard_DerivesSinglePreludeWithoutReorderingSettlements()
    {
        var playerId = new CombatantId(1001);
        var enemyId = new CombatantId(2001);
        var cardId = new CardInstanceId(11);
        var energySpent = new BattleEnergySpentSettlement(
            order: 0,
            playerId,
            energyBefore: 3,
            energyAfter: 2);
        var damage = new BattleDamageAppliedSettlement(
            order: 1,
            new BattleEffectId(4001),
            playerId,
            enemyId,
            attackValue: 6,
            blockBefore: 0,
            blockAfter: 0,
            healthBefore: 20,
            healthAfter: 14);
        var moved = new BattleCardMovedSettlement(
            order: 2,
            cardId,
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 2,
            BattleCommandType.PlayCard,
            playerId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { energySpent, damage, moved });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(plan.Prelude, Is.Not.Null);
        Assert.That(plan.Prelude.Kind, Is.EqualTo(BattleCommandPreludeKind.PlayCard));
        Assert.That(plan.Prelude.CardId, Is.EqualTo(cardId));
        Assert.That(plan.Prelude.TargetId, Is.EqualTo(enemyId));
        Assert.That(
            plan.SettlementEntries.Select(item => (item.Order, item.Settlement)),
            Is.EqualTo(new[]
            {
                (0, (BattleSettlementRecord)energySpent),
                (1, (BattleSettlementRecord)damage),
                (2, (BattleSettlementRecord)moved),
            }));
        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.HealthLossNumber, 1, 0),
                (BattleCommandPresentationStepKind.HitShake, 1, 1),
                (BattleCommandPresentationStepKind.CardMoved, 2, 0),
            }));
    }

    /// <summary>确认多 Effect 出牌仍只有一个前奏，且所有 Effect 与离手记录保持原始 Order。</summary>
    [Test]
    public void Create_BashPlayCard_DerivesSinglePreludeBeforeEveryOrderedEffect()
    {
        var playerId = new CombatantId(1001);
        var enemyId = new CombatantId(2001);
        var cardId = new CardInstanceId(12);
        var energySpent = new BattleEnergySpentSettlement(0, playerId, 3, 1);
        var damage = new BattleDamageAppliedSettlement(
            order: 1,
            new BattleEffectId(4101),
            playerId,
            enemyId,
            attackValue: 8,
            blockBefore: 0,
            blockAfter: 0,
            healthBefore: 20,
            healthAfter: 12);
        var vulnerable = new BattleStatusAppliedSettlement(
            order: 2,
            new BattleEffectId(4102),
            playerId,
            enemyId,
            BattleStatusType.Vulnerable,
            valueBefore: 0,
            valueAfter: 2);
        var moved = new BattleCardMovedSettlement(
            order: 3,
            cardId,
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 3,
            BattleCommandType.PlayCard,
            playerId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { energySpent, damage, vulnerable, moved });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(plan.Prelude, Is.Not.Null);
        Assert.That(plan.Prelude.Kind, Is.EqualTo(BattleCommandPreludeKind.PlayCard));
        Assert.That(plan.Prelude.CardId, Is.EqualTo(cardId));
        Assert.That(plan.Prelude.TargetId, Is.EqualTo(enemyId));
        Assert.That(
            plan.SettlementEntries.Select(item => (item.Order, item.Settlement)),
            Is.EqualTo(new[]
            {
                (0, (BattleSettlementRecord)energySpent),
                (1, (BattleSettlementRecord)damage),
                (2, (BattleSettlementRecord)vulnerable),
                (3, (BattleSettlementRecord)moved),
            }));
        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.HealthLossNumber, 1, 0),
                (BattleCommandPresentationStepKind.HitShake, 1, 1),
                (BattleCommandPresentationStepKind.VulnerableIconPulse, 2, 0),
                (BattleCommandPresentationStepKind.CardMoved, 3, 0),
            }));
    }

    /// <summary>确认两种 skip 均派生零步骤，且 Effect skip 不能借真实离手记录伪造出牌前奏。</summary>
    [Test]
    public void Create_SkippedSettlements_MapToZeroStepsWithoutFakePrelude()
    {
        var playerId = new CombatantId(1001);
        var enemyId = new CombatantId(2001);
        var operationSkipped = new BattleOperationSkippedSettlement(
            order: 0,
            new BattleEffectId(4201),
            playerId,
            enemyId,
            BattleOperationSkipReason.TargetNotAlive);
        var moved = new BattleCardMovedSettlement(
            order: 1,
            new CardInstanceId(13),
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var playResult = new BattleCommandExecutionResult(
            authoritySequence: 4,
            BattleCommandType.PlayCard,
            playerId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { operationSkipped, moved });
        var enemySkipped = new BattleEnemyActionSkippedSettlement(
            order: 0,
            enemyId,
            BattleEnemyActionSkipReason.SourceNotAlive);
        var enemyResult = new BattleCommandExecutionResult(
            authoritySequence: 5,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { enemySkipped });

        BattleCommandPresentationPlan playPlan = BattleCommandPresentationPlan.Create(playResult);
        BattleCommandPresentationPlan enemyPlan = BattleCommandPresentationPlan.Create(enemyResult);

        Assert.That(playPlan.Prelude, Is.Null);
        Assert.That(playPlan.SettlementEntries[0].Settlement, Is.SameAs(operationSkipped));
        Assert.That(playPlan.SettlementEntries[0].Steps, Is.Empty);
        Assert.That(
            playPlan.SettlementSteps.Select(item => (item.Kind, item.SettlementOrder)),
            Is.EqualTo(new[] { (BattleCommandPresentationStepKind.CardMoved, 1) }));
        Assert.That(enemyPlan.Prelude, Is.Null);
        Assert.That(enemyPlan.SettlementEntries, Has.Count.EqualTo(1));
        Assert.That(enemyPlan.SettlementEntries[0].Steps, Is.Empty);
        Assert.That(enemyPlan.SettlementSteps, Is.Empty);
    }

    /// <summary>确认出牌前奏忽略不可见 skip，并只采用首个可见 Effect 的冻结目标。</summary>
    [Test]
    public void Create_PlayCard_IgnoresSkippedEffectAndUsesFirstVisibleEffectTargetForPrelude()
    {
        var playerId = new CombatantId(1001);
        var skippedTargetId = new CombatantId(2001);
        var visibleTargetId = new CombatantId(2002);
        var cardId = new CardInstanceId(131);
        var energySpent = new BattleEnergySpentSettlement(0, playerId, 3, 2);
        var skipped = new BattleOperationSkippedSettlement(
            order: 1,
            new BattleEffectId(4211),
            playerId,
            skippedTargetId,
            BattleOperationSkipReason.TargetNotAlive);
        var damage = new BattleDamageAppliedSettlement(
            order: 2,
            new BattleEffectId(4212),
            playerId,
            visibleTargetId,
            attackValue: 6,
            blockBefore: 0,
            blockAfter: 0,
            healthBefore: 20,
            healthAfter: 14);
        var moved = new BattleCardMovedSettlement(
            order: 3,
            cardId,
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 41,
            BattleCommandType.PlayCard,
            playerId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { energySpent, skipped, damage, moved });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(plan.Prelude, Is.Not.Null);
        Assert.That(plan.Prelude.Kind, Is.EqualTo(BattleCommandPreludeKind.PlayCard));
        Assert.That(plan.Prelude.CardId, Is.EqualTo(cardId));
        Assert.That(plan.Prelude.TargetId, Is.EqualTo(visibleTargetId));
        Assert.That(
            plan.SettlementEntries.Select(item => item.Settlement),
            Is.EqualTo(new BattleSettlementRecord[] { energySpent, skipped, damage, moved }));
        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.HealthLossNumber, 2, 0),
                (BattleCommandPresentationStepKind.HitShake, 2, 1),
                (BattleCommandPresentationStepKind.CardMoved, 3, 0),
            }));
    }

    /// <summary>确认计划、扁平步骤与单记录子步骤均为复制后的只读集合。</summary>
    [Test]
    public void Create_ExposesCopiedReadOnlyEntriesStepsAndSubsteps()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var damage = new BattleDamageAppliedSettlement(
            order: 0,
            new BattleEffectId(4221),
            sourceId,
            targetId,
            attackValue: 6,
            blockBefore: 0,
            blockAfter: 0,
            healthBefore: 20,
            healthAfter: 14);
        var moved = new BattleCardMovedSettlement(
            order: 1,
            new CardInstanceId(132),
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var sourceSettlements = new BattleSettlementRecord[] { damage, moved };
        var result = new BattleCommandExecutionResult(
            authoritySequence: 42,
            BattleCommandType.PlayCard,
            sourceId,
            BattleCommandExecutionFailureReason.None,
            sourceSettlements);

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        sourceSettlements[0] = moved;
        var entries = (System.Collections.Generic.IList<BattleCommandPresentationSettlementEntry>)
            plan.SettlementEntries;
        var steps = (System.Collections.Generic.IList<BattleCommandPresentationStep>)
            plan.SettlementSteps;
        var substeps = (System.Collections.Generic.IList<BattleCommandPresentationStep>)
            plan.SettlementEntries[0].Steps;

        Assert.That(plan.SettlementEntries[0].Settlement, Is.SameAs(damage));
        Assert.Throws<System.NotSupportedException>(() => entries.RemoveAt(0));
        Assert.Throws<System.NotSupportedException>(() => steps.Clear());
        Assert.Throws<System.NotSupportedException>(() => substeps.Add(substeps[0]));
    }

    /// <summary>确认表现计划拒绝乱序冻结结果，而不是在 UI 层排序并掩盖领域错误。</summary>
    [Test]
    public void Create_ReversedSettlementOrder_ThrowsInsteadOfSorting()
    {
        var playerId = new CombatantId(1001);
        var energySpent = new BattleEnergySpentSettlement(0, playerId, 3, 2);
        var moved = new BattleCardMovedSettlement(
            order: 1,
            new CardInstanceId(14),
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 6,
            BattleCommandType.PlayCard,
            playerId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { moved, energySpent });

        System.ArgumentException exception = Assert.Throws<System.ArgumentException>(
            () => BattleCommandPresentationPlan.Create(result));

        StringAssert.Contains("不得在 UI 层重排", exception.Message);
        Assert.That(result.Settlements.Select(item => item.Order), Is.EqualTo(new[] { 1, 0 }));
    }

    /// <summary>确认出牌结果存在多条离手记录时同步拒绝歧义，而不是静默选择其中一张。</summary>
    [Test]
    public void Create_PlayCardWithMultipleHandToDiscard_ThrowsAmbiguousPrelude()
    {
        var playerId = new CombatantId(1001);
        var enemyId = new CombatantId(2001);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 7,
            BattleCommandType.PlayCard,
            playerId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                new BattleEnergySpentSettlement(0, playerId, 3, 2),
                new BattleDamageAppliedSettlement(
                    1,
                    new BattleEffectId(4301),
                    playerId,
                    enemyId,
                    6,
                    0,
                    0,
                    20,
                    14),
                new BattleCardMovedSettlement(
                    2,
                    new CardInstanceId(15),
                    BattleCardZone.Hand,
                    BattleCardZone.DiscardPile),
                new BattleCardMovedSettlement(
                    3,
                    new CardInstanceId(16),
                    BattleCardZone.Hand,
                    BattleCardZone.DiscardPile),
            });

        Assert.Throws<System.ArgumentException>(
            () => BattleCommandPresentationPlan.Create(result));
    }

    /// <summary>确认伤害记录只读实际吸收与生命损失，并以格挡字、生命字、抖动、死亡固定子序播放。</summary>
    [Test]
    public void Create_DamageSettlements_DeriveActualAmountsAndStableFatalSubsteps()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var fullyBlocked = new BattleDamageAppliedSettlement(
            order: 0,
            new BattleEffectId(4401),
            sourceId,
            targetId,
            attackValue: 6,
            blockBefore: 10,
            blockAfter: 4,
            healthBefore: 20,
            healthAfter: 20);
        var fatalOverflow = new BattleDamageAppliedSettlement(
            order: 1,
            new BattleEffectId(4402),
            sourceId,
            targetId,
            attackValue: 8,
            blockBefore: 3,
            blockAfter: 0,
            healthBefore: 5,
            healthAfter: 0);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 8,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { fullyBlocked, fatalOverflow });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.BlockAbsorbedNumber, 0, 0),
                (BattleCommandPresentationStepKind.BlockAbsorbedNumber, 1, 0),
                (BattleCommandPresentationStepKind.HealthLossNumber, 1, 1),
                (BattleCommandPresentationStepKind.HitShake, 1, 2),
                (BattleCommandPresentationStepKind.DeathTransition, 1, 3),
            }));
        Assert.That(fullyBlocked.BlockAbsorbed, Is.EqualTo(6));
        Assert.That(fullyBlocked.HealthLoss, Is.Zero);
        Assert.That(fatalOverflow.BlockAbsorbed, Is.EqualTo(3));
        Assert.That(fatalOverflow.HealthLoss, Is.EqualTo(5));
        Assert.That(fatalOverflow.WasFatal, Is.True);
    }

    /// <summary>确认格挡增加记录只派生实际增加量飘字，不重算或保存最终 Block。</summary>
    [Test]
    public void Create_BlockGained_DerivesSingleActualAmountStep()
    {
        var targetId = new CombatantId(1001);
        var gained = new BattleBlockGainedSettlement(
            order: 0,
            new BattleEffectId(4501),
            targetId,
            targetId,
            blockBefore: 3,
            blockAfter: 10);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 9,
            BattleCommandType.PlayCard,
            targetId,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { gained });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(plan.Prelude, Is.Null);
        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex, item.Settlement)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.BlockGainedNumber, 0, 0, (BattleSettlementRecord)gained),
            }));
        Assert.That(gained.Amount, Is.EqualTo(7));
    }

    /// <summary>确认 Strength 的正负实际变化都只脉冲既有图标，不保存属性镜像。</summary>
    [Test]
    public void Create_StrengthChanges_DeriveIconPulseFromFrozenRecords()
    {
        var sourceId = new CombatantId(1001);
        var targetId = new CombatantId(2001);
        var increased = new BattleAttributeModifiedSettlement(
            0,
            new BattleEffectId(4601),
            sourceId,
            targetId,
            BattleAttributeType.Strength,
            valueBefore: 0,
            valueAfter: 2);
        var reduced = new BattleAttributeModifiedSettlement(
            1,
            new BattleEffectId(4602),
            sourceId,
            targetId,
            BattleAttributeType.Strength,
            valueBefore: 2,
            valueAfter: 1);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 10,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { increased, reduced });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.StrengthIconPulse, 0, 0),
                (BattleCommandPresentationStepKind.StrengthIconPulse, 1, 0),
            }));
        Assert.That(increased.Amount, Is.EqualTo(2));
        Assert.That(reduced.Amount, Is.EqualTo(-1));
    }

    /// <summary>确认 Vulnerable 衰减到非零或零都只脉冲现有图标，并保留冻结层数事实。</summary>
    [Test]
    public void Create_VulnerableReductions_DeriveIconPulseWithoutWritingStatus()
    {
        var targetId = new CombatantId(2001);
        var reduced = new BattleStatusReducedSettlement(
            order: 0,
            targetId,
            BattleStatusType.Vulnerable,
            valueBefore: 2,
            valueAfter: 1);
        var expired = new BattleStatusReducedSettlement(
            order: 1,
            targetId,
            BattleStatusType.Vulnerable,
            valueBefore: 1,
            valueAfter: 0);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 11,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { reduced, expired });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.VulnerableIconPulse, 0, 0),
                (BattleCommandPresentationStepKind.VulnerableIconPulse, 1, 0),
            }));
        Assert.That(reduced.ValueAfter, Is.EqualTo(1));
        Assert.That(expired.ValueAfter, Is.Zero);
    }

    /// <summary>确认卡区只映射抽牌、离手和重洗，不为 Exhaust 或重洗内部搬运制造额外 cue。</summary>
    [Test]
    public void Create_CardZoneSettlements_MapOnlyAuthorizedMotionRoutes()
    {
        var drawnCard = new CardInstanceId(21);
        var discardedCard = new CardInstanceId(22);
        var reshuffledCard = new CardInstanceId(23);
        var exhaustedCard = new CardInstanceId(24);
        var drawToHand = new BattleCardMovedSettlement(
            0,
            drawnCard,
            BattleCardZone.DrawPile,
            BattleCardZone.Hand);
        var handToDiscard = new BattleCardMovedSettlement(
            1,
            discardedCard,
            BattleCardZone.Hand,
            BattleCardZone.DiscardPile);
        var discardToDraw = new BattleCardMovedSettlement(
            2,
            reshuffledCard,
            BattleCardZone.DiscardPile,
            BattleCardZone.DrawPile);
        var handToExhaust = new BattleCardMovedSettlement(
            3,
            exhaustedCard,
            BattleCardZone.Hand,
            BattleCardZone.ExhaustPile);
        var reshuffled = new BattleCardsReshuffledSettlement(
            4,
            new[] { reshuffledCard, discardedCard });
        var result = new BattleCommandExecutionResult(
            authoritySequence: 12,
            BattleCommandType.EndPlayerAction,
            new CombatantId(1001),
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[]
            {
                drawToHand,
                handToDiscard,
                discardToDraw,
                handToExhaust,
                reshuffled,
            });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(
            plan.SettlementEntries.Select(item => item.Steps.Count),
            Is.EqualTo(new[] { 1, 1, 0, 0, 1 }));
        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.CardMoved, 0, 0),
                (BattleCommandPresentationStepKind.CardMoved, 1, 0),
                (BattleCommandPresentationStepKind.CardsReshuffled, 4, 0),
            }));
        Assert.That(
            reshuffled.NewDrawPileOrder,
            Is.EqualTo(new[] { reshuffledCard, discardedCard }));
    }

    /// <summary>确认意图推进只派生既有意图 HUD 脉冲，不依据行为 ID 重算或推进随机。</summary>
    [Test]
    public void Create_EnemyIntentAdvanced_DerivesSingleIntentPulse()
    {
        var enemyId = new CombatantId(2001);
        var advanced = new BattleEnemyIntentAdvancedSettlement(
            order: 0,
            enemyId,
            completedBehaviorId: 501,
            nextBehaviorId: 502);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 13,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { advanced });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex, item.Settlement)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.EnemyIntentPulse, 0, 0, (BattleSettlementRecord)advanced),
            }));
        Assert.That(advanced.SourceId, Is.EqualTo(enemyId));
        Assert.That(advanced.NextBehaviorId, Is.EqualTo(502));
    }

    /// <summary>确认横幅只在真实阶段进入玩家/敌人行动时播放，行动者交接不重播，终局最后派生 outcome。</summary>
    [Test]
    public void Create_PhaseChanges_DeriveOnlyAuthorizedBannersAndTerminalStep()
    {
        var firstEnemyId = new CombatantId(2001);
        var secondEnemyId = new CombatantId(2002);
        var playerTurn = new BattlePhaseChangedSettlement(
            0,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            0,
            1,
            null,
            null);
        var enemyTurn = new BattlePhaseChangedSettlement(
            1,
            BattleTurnPhase.PlayerAction,
            BattleTurnPhase.EnemyAction,
            1,
            1,
            null,
            firstEnemyId);
        var actorHandoff = new BattlePhaseChangedSettlement(
            2,
            BattleTurnPhase.EnemyAction,
            BattleTurnPhase.EnemyAction,
            1,
            1,
            firstEnemyId,
            secondEnemyId);
        var battleEnded = new BattlePhaseChangedSettlement(
            3,
            BattleTurnPhase.EnemyAction,
            BattleTurnPhase.BattleEnded,
            1,
            1,
            secondEnemyId,
            null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 14,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { playerTurn, enemyTurn, actorHandoff, battleEnded });

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(
            plan.SettlementEntries.Select(item => item.Steps.Count),
            Is.EqualTo(new[] { 1, 1, 0, 1 }));
        Assert.That(
            plan.SettlementSteps.Select(item =>
                (item.Kind, item.SettlementOrder, item.SubstepIndex)),
            Is.EqualTo(new[]
            {
                (BattleCommandPresentationStepKind.PlayerTurnBanner, 0, 0),
                (BattleCommandPresentationStepKind.EnemyTurnBanner, 1, 0),
                (BattleCommandPresentationStepKind.BattleOutcome, 3, 0),
            }));
    }

    /// <summary>确认五类仅更新最终 HUD 或明确 skip 的记录仍保留 entry，但不会制造可见步骤。</summary>
    [Test]
    public void Create_ZeroFeedbackSettlementTypes_PreserveEntriesWithoutVisibleSteps()
    {
        var playerId = new CombatantId(1001);
        var enemyId = new CombatantId(2001);
        var settlements = new BattleSettlementRecord[]
        {
            new BattleEnergySpentSettlement(0, playerId, 3, 2),
            new BattleOperationSkippedSettlement(
                1,
                new BattleEffectId(4701),
                playerId,
                enemyId,
                BattleOperationSkipReason.TargetNotAlive),
            new BattleBlockClearedSettlement(2, enemyId, blockBefore: 4),
            new BattleEnergyRefilledSettlement(3, playerId, 0, 3),
            new BattleEnemyActionSkippedSettlement(
                4,
                enemyId,
                BattleEnemyActionSkipReason.SourceNotAlive),
        };
        var result = new BattleCommandExecutionResult(
            authoritySequence: 15,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            settlements);

        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);

        Assert.That(plan.Prelude, Is.Null);
        Assert.That(plan.SettlementEntries.Select(item => item.Settlement), Is.EqualTo(settlements));
        Assert.That(plan.SettlementEntries.All(item => item.Steps.Count == 0), Is.True);
        Assert.That(plan.SettlementSteps, Is.Empty);
    }

    /// <summary>确认未知 concrete settlement 会同步报错，而不是被误判为零反馈。</summary>
    [Test]
    public void Create_UnsupportedConcreteSettlement_ThrowsInsteadOfSilentlySkipping()
    {
        var result = new BattleCommandExecutionResult(
            authoritySequence: 16,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { new UnsupportedSettlement(order: 0) });

        System.ArgumentException exception = Assert.Throws<System.ArgumentException>(
            () => BattleCommandPresentationPlan.Create(result));

        StringAssert.Contains("未知 settlement", exception.Message);
    }

    /// <summary>确认 BattleEnded 必须是最后一条 settlement，保证终局面板晚于全部前序反馈。</summary>
    [Test]
    public void Create_BattleEndedBeforeLaterSettlement_ThrowsBeforePlayback()
    {
        var playerId = new CombatantId(1001);
        var battleEnded = new BattlePhaseChangedSettlement(
            0,
            BattleTurnPhase.EnemyAction,
            BattleTurnPhase.BattleEnded,
            1,
            1,
            new CombatantId(2001),
            null);
        var energyRefilled = new BattleEnergyRefilledSettlement(1, playerId, 0, 3);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 17,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { battleEnded, energyRefilled });

        System.ArgumentException exception = Assert.Throws<System.ArgumentException>(
            () => BattleCommandPresentationPlan.Create(result));

        StringAssert.Contains("BattleEnded 必须是最后一条", exception.Message);
    }

    /// <summary>只用于证明 planner 不得静默接纳未知 concrete settlement。</summary>
    private sealed class UnsupportedSettlement : BattleSettlementRecord
    {
        /// <summary>以现有 RecordType 构造未知 concrete 类型，隔离测试不完整模式匹配。</summary>
        public UnsupportedSettlement(int order)
            : base(order, BattleSettlementRecordType.EnergySpent, null, null, null)
        {
        }
    }
}
