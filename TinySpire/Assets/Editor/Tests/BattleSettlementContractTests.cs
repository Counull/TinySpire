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

    /// <summary>验证尚无 M7 写入的既有命令仍暴露非空、冻结且为空的结算列表。</summary>
    [Test]
    public void ExistingCommand_ExposesFrozenEmptySettlements()
    {
        using (var combatants = new BattleCombatantsData())
        {
            combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            var presentation = new ControllableBattleCommandPresentation();
            using (BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
                       combatants,
                       presentation))
            {
                queue.Submit(new StartBattleCommand());

                IReadOnlyList<BattleSettlementRecord> settlements =
                    presentation.Results[0].Settlements;
                Assert.That(settlements, Is.Not.Null);
                Assert.That(settlements, Is.Empty);

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
            EnemyCombatantData enemy = combatants.AddEnemy(
                templateId: 201,
                maxHealth: 20,
                strength: 0);
            var presentation = new ControllableBattleCommandPresentation();
            using (BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
                       combatants,
                       presentation))
            {
                queue.Submit(new StartBattleCommand());
                presentation.CompleteNext();

                queue.Submit(new CompleteEnemyActionCommand(enemy.Id));

                BattleCommandExecutionResult result = presentation.Results[1];
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Settlements, Is.Not.Null);
                Assert.That(result.Settlements, Is.Empty);
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
}
