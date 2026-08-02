using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleSettlementContractTests
{
    /// <summary>每个用例结束后释放测试工厂代建的敌人意图响应式资源。</summary>
    [TearDown]
    public void TearDown()
    {
        BattleCommandQueueTestFactory.DisposeOwnedEnemyIntents();
    }

    /// <summary>验证既有命令新增阶段记录后仍暴露非空且冻结的结算列表。</summary>
    [Test]
    public void ExistingCommand_ExposesFrozenPhaseSettlements()
    {
        using (var combatants = new BattleCombatantsData())
        {
            combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            var presentation = new ControllableBattleCommandPresentation();
            using (BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
                       combatants,
                       presentation))
            {
                queue.SubmitRegistered(new StartBattleCommand());

                IReadOnlyList<BattleSettlementRecord> settlements =
                    presentation.Results[0].Settlements;
                Assert.That(settlements, Is.Not.Null);
                Assert.That(settlements, Has.Count.EqualTo(2));
                Assert.That(settlements[0], Is.TypeOf<BattleEnergyRefilledSettlement>());
                Assert.That(settlements[1], Is.TypeOf<BattlePhaseChangedSettlement>());

                var listSurface = settlements as IList<BattleSettlementRecord>;
                Assert.That(listSurface, Is.Not.Null);
                Assert.That(listSurface.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => listSurface.Add(null));
            }
        }
    }

    /// <summary>验证已进入执行阶段但失败的命令返回非空且为空的冻结结算列表。</summary>
    [Test]
    public void FailedCommand_ExposesEmptySettlements()
    {
        using (var combatants = new BattleCombatantsData())
        {
            combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            var presentation = new ControllableBattleCommandPresentation();
            using (BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
                       combatants,
                       presentation))
            {
                queue.SubmitRegistered(new StartBattleCommand());
                presentation.CompleteNext();
                using BattleCommandLifecycleExecutionRecorder recorder =
                    queue.RecordExecutionLifecycle();

                BattleCommandSubmissionResult submission =
                    queue.SubmitRegistered(new StartBattleCommand());
                BattleCommandLifecycleEvent result = recorder.RequireTerminal(submission);

                Assert.That(result.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
                Assert.That(result.Settlements, Is.Not.Null);
                Assert.That(result.Settlements, Is.Empty);
                Assert.That(presentation.Results, Has.Count.EqualTo(1));
            }
        }
    }

    /// <summary>验证每种结算记录都是可辨识的 sealed 类型，且公开状态没有写入器。</summary>
    [TestCase(typeof(BattleEnergySpentSettlement))]
    [TestCase(typeof(BattleDamageAppliedSettlement))]
    [TestCase(typeof(BattleBlockGainedSettlement))]
    [TestCase(typeof(BattleAttributeModifiedSettlement))]
    [TestCase(typeof(BattleStatusAppliedSettlement))]
    [TestCase(typeof(BattleCardMovedSettlement))]
    [TestCase(typeof(BattleCardsReshuffledSettlement))]
    [TestCase(typeof(BattleOperationSkippedSettlement))]
    [TestCase(typeof(BattleBlockClearedSettlement))]
    [TestCase(typeof(BattleStatusReducedSettlement))]
    [TestCase(typeof(BattleEnergyRefilledSettlement))]
    [TestCase(typeof(BattleEnemyIntentAdvancedSettlement))]
    [TestCase(typeof(BattleEnemyActionSkippedSettlement))]
    [TestCase(typeof(BattlePhaseChangedSettlement))]
    public void SettlementType_ExposesOnlyImmutablePublicState(Type settlementType)
    {
        Assert.That(settlementType.IsSealed, Is.True);
        Assert.That(typeof(BattleSettlementRecord).IsAssignableFrom(settlementType), Is.True);

        foreach (PropertyInfo property in settlementType.GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            Assert.That(property.SetMethod, Is.Null, property.Name);
        }
    }

    /// <summary>验证 M8 终局使用中立 BattleEnded 阶段，而不是保存可变胜负镜像。</summary>
    [Test]
    public void TurnPhase_ContainsNeutralBattleEndedWithoutOutcomeMirror()
    {
        Assert.That(Enum.IsDefined(typeof(BattleTurnPhase), nameof(BattleTurnPhase.BattleEnded)), Is.True);
        Assert.That(typeof(BattleTurnData).GetProperty("BattleOutcome"), Is.Null);
        Assert.That(typeof(BattleTurnData).GetProperty("BattleEnded"), Is.Null);
    }

    /// <summary>验证阶段记录沿用 Turn 的 CurrentActingEnemyId 领域名称，不建立第二套简称。</summary>
    [Test]
    public void PhaseChangedSettlement_UsesCurrentActingEnemyDomainName()
    {
        Assert.That(
            typeof(BattlePhaseChangedSettlement).GetProperty("CurrentActingEnemyIdBefore"),
            Is.Not.Null);
        Assert.That(
            typeof(BattlePhaseChangedSettlement).GetProperty("CurrentActingEnemyIdAfter"),
            Is.Not.Null);
        Assert.That(typeof(BattlePhaseChangedSettlement).GetProperty("ActingEnemyIdBefore"), Is.Null);
        Assert.That(typeof(BattlePhaseChangedSettlement).GetProperty("ActingEnemyIdAfter"), Is.Null);
    }

    /// <summary>验证基础能量恢复记录保留顺序、玩家来源与完整有符号变化，且不伪造目标或 Effect。</summary>
    [Test]
    public void EnergyRefilledSettlement_ExposesCompleteSourceOnlySemantics()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            var settlement = new BattleEnergyRefilledSettlement(
                order: 3,
                sourceId: player.Id,
                energyBefore: 1,
                energyAfter: 4);

            Assert.That(settlement.Order, Is.EqualTo(3));
            Assert.That(settlement.RecordType, Is.EqualTo(BattleSettlementRecordType.EnergyRefilled));
            Assert.That(settlement.SourceId, Is.EqualTo(player.Id));
            Assert.That(settlement.TargetId, Is.Null);
            Assert.That(settlement.EffectId, Is.Null);
            Assert.That(settlement.EnergyBefore, Is.EqualTo(1));
            Assert.That(settlement.EnergyAfter, Is.EqualTo(4));
            Assert.That(settlement.Amount, Is.EqualTo(3));
        }
    }

    /// <summary>验证意图推进记录只关联行动敌人，并完整冻结已完成与下一行为标识。</summary>
    [Test]
    public void EnemyIntentAdvancedSettlement_ExposesCompleteSourceOnlySemantics()
    {
        using (var combatants = new BattleCombatantsData())
        {
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            var settlement = new BattleEnemyIntentAdvancedSettlement(
                order: 5,
                enemyId: enemy.Id,
                completedBehaviorId: 301,
                nextBehaviorId: 302);

            Assert.That(settlement.Order, Is.EqualTo(5));
            Assert.That(settlement.RecordType, Is.EqualTo(BattleSettlementRecordType.EnemyIntentAdvanced));
            Assert.That(settlement.SourceId, Is.EqualTo(enemy.Id));
            Assert.That(settlement.TargetId, Is.Null);
            Assert.That(settlement.EffectId, Is.Null);
            Assert.That(settlement.CompletedBehaviorId, Is.EqualTo(301));
            Assert.That(settlement.NextBehaviorId, Is.EqualTo(302));
        }
    }

    /// <summary>验证阶段相同但行动敌人变化仍构成完整阶段记录，且该记录不关联参与者或 Effect。</summary>
    [Test]
    public void PhaseChangedSettlement_SameEnemyActionPhaseCanAdvanceActingEnemy()
    {
        using (var combatants = new BattleCombatantsData())
        {
            EnemyCombatantData firstEnemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            EnemyCombatantData secondEnemy = combatants.AddEnemy(templateId: 202, maxHealth: 18, strength: 0);
            var settlement = new BattlePhaseChangedSettlement(
                order: 8,
                phaseBefore: BattleTurnPhase.EnemyAction,
                phaseAfter: BattleTurnPhase.EnemyAction,
                roundNumberBefore: 2,
                roundNumberAfter: 2,
                currentActingEnemyIdBefore: firstEnemy.Id,
                currentActingEnemyIdAfter: secondEnemy.Id);

            Assert.That(settlement.Order, Is.EqualTo(8));
            Assert.That(settlement.RecordType, Is.EqualTo(BattleSettlementRecordType.BattlePhaseChanged));
            Assert.That(settlement.SourceId, Is.Null);
            Assert.That(settlement.TargetId, Is.Null);
            Assert.That(settlement.EffectId, Is.Null);
            Assert.That(settlement.PhaseBefore, Is.EqualTo(BattleTurnPhase.EnemyAction));
            Assert.That(settlement.PhaseAfter, Is.EqualTo(BattleTurnPhase.EnemyAction));
            Assert.That(settlement.RoundNumberBefore, Is.EqualTo(2));
            Assert.That(settlement.RoundNumberAfter, Is.EqualTo(2));
            Assert.That(settlement.CurrentActingEnemyIdBefore, Is.EqualTo(firstEnemy.Id));
            Assert.That(settlement.CurrentActingEnemyIdAfter, Is.EqualTo(secondEnemy.Id));
        }
    }
}
