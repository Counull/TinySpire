using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleStatusTimingTests
{
    /// <summary>验证玩家轮开始时只清除非零 Block，并按调用方提供的真实顺序记录。</summary>
    [Test]
    public void Execute_PlayerRoundStart_ClearsBlockBeforeOtherRoundWork()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            ApplyBlock(combatants, player.Id, player.Id, amount: 6);
            ApplyVulnerable(combatants, enemy.Id, player.Id, amount: 2);
            var timing = new BattleStatusTiming(combatants);

            BattleStatusTimingResult result = timing.Execute(
                BattleStatusTimingPoint.PlayerRoundStart,
                player.Id,
                startingOrder: 3);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(player.CurrentBlock, Is.Zero);
            Assert.That(player.CurrentVulnerable, Is.EqualTo(2));
            Assert.That(result.Settlements, Has.Count.EqualTo(1));
            var cleared = result.Settlements[0] as BattleBlockClearedSettlement;
            Assert.That(cleared, Is.Not.Null);
            Assert.That(cleared.Order, Is.EqualTo(3));
            Assert.That(cleared.SourceId, Is.Null);
            Assert.That(cleared.TargetId, Is.EqualTo(player.Id));
            Assert.That(cleared.EffectId, Is.Null);
            Assert.That(cleared.BlockBefore, Is.EqualTo(6));
            Assert.That(cleared.BlockAfter, Is.Zero);
            Assert.That(cleared.Amount, Is.EqualTo(6));
        }
    }

    /// <summary>验证成功结束玩家行动后的命名时点只让 Vulnerable 恰好衰减一层。</summary>
    [Test]
    public void Execute_PlayerActionEnded_ReducesVulnerableExactlyOne()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            ApplyBlock(combatants, player.Id, player.Id, amount: 4);
            ApplyVulnerable(combatants, enemy.Id, player.Id, amount: 2);
            var timing = new BattleStatusTiming(combatants);

            BattleStatusTimingResult result = timing.Execute(
                BattleStatusTimingPoint.PlayerActionEnded,
                player.Id,
                startingOrder: 5);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(player.CurrentBlock, Is.EqualTo(4));
            Assert.That(player.CurrentVulnerable, Is.EqualTo(1));
            Assert.That(result.Settlements, Has.Count.EqualTo(1));
            var reduced = result.Settlements[0] as BattleStatusReducedSettlement;
            Assert.That(reduced, Is.Not.Null);
            Assert.That(reduced.Order, Is.EqualTo(5));
            Assert.That(reduced.Status, Is.EqualTo(BattleStatusType.Vulnerable));
            Assert.That(reduced.ValueBefore, Is.EqualTo(2));
            Assert.That(reduced.ValueAfter, Is.EqualTo(1));
            Assert.That(reduced.Amount, Is.EqualTo(1));
            Assert.That(reduced.SourceId, Is.Null);
            Assert.That(reduced.TargetId, Is.EqualTo(player.Id));
            Assert.That(reduced.EffectId, Is.Null);
        }
    }

    /// <summary>验证敌人先清旧 Block，Effect 可从零累加，再在行动完成时衰减 Vulnerable。</summary>
    [Test]
    public void Execute_EnemyBoundaries_ProjectClearBeforeEffectAndReductionAfterEffect()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            ApplyBlock(combatants, enemy.Id, enemy.Id, amount: 8);
            ApplyVulnerable(combatants, player.Id, enemy.Id, amount: 2);
            var timing = new BattleStatusTiming(combatants);

            BattleStatusTimingResult started = timing.Execute(
                BattleStatusTimingPoint.EnemyActionStarted,
                enemy.Id,
                startingOrder: 0);
            ApplyBlock(combatants, enemy.Id, enemy.Id, amount: 5);
            BattleStatusTimingResult completed = timing.Execute(
                BattleStatusTimingPoint.EnemyActionCompleted,
                enemy.Id,
                startingOrder: 2);

            Assert.That(started.Settlements[0], Is.TypeOf<BattleBlockClearedSettlement>());
            Assert.That(started.Settlements[0].Order, Is.Zero);
            Assert.That(enemy.CurrentBlock, Is.EqualTo(5), "Self defend 必须从清理后的零开始累加");
            Assert.That(enemy.CurrentVulnerable, Is.EqualTo(1));
            Assert.That(completed.Settlements[0], Is.TypeOf<BattleStatusReducedSettlement>());
            Assert.That(completed.Settlements[0].Order, Is.EqualTo(2));
        }
    }

    /// <summary>验证相关值为零或参与者死亡时不写入、不造状态记录。</summary>
    [Test]
    public void Execute_ZeroOrDeadStatus_ReturnsEmptyWithoutMutation()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 4, strength: 0);
            ApplyVulnerable(combatants, player.Id, enemy.Id, amount: 2);
            BattleEffectStateTestDriver.ApplyDamage(combatants, player.Id, enemy.Id, configuredValue: 100);
            int vulnerableBefore = enemy.CurrentVulnerable;
            var timing = new BattleStatusTiming(combatants);

            BattleStatusTimingResult zeroBlock = timing.Execute(
                BattleStatusTimingPoint.PlayerRoundStart,
                player.Id,
                startingOrder: 0);
            BattleStatusTimingResult deadReduction = timing.Execute(
                BattleStatusTimingPoint.EnemyActionCompleted,
                enemy.Id,
                startingOrder: 0);

            Assert.That(zeroBlock.Settlements, Is.Empty);
            Assert.That(deadReduction.Settlements, Is.Empty);
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.CurrentVulnerable, Is.EqualTo(vulnerableBefore));
        }
    }

    /// <summary>验证敌人联合预构建先在不可变初始快照上投影 Block=0，再对 Effect 后快照衰减 Vulnerable。</summary>
    [Test]
    public void Project_EnemyActionUsesInitialThenAfterEffectSnapshots()
    {
        var initial = new BattleEffectTargetSnapshot(health: 20, block: 8, vulnerable: 2);

        BattleEffectTargetSnapshot beforeEffect = BattleStatusTiming.Project(
            BattleStatusTimingPoint.EnemyActionStarted,
            initial);
        var afterSelfDefend = new BattleEffectTargetSnapshot(
            beforeEffect.Health,
            block: beforeEffect.Block + 5,
            vulnerable: beforeEffect.Vulnerable);
        BattleEffectTargetSnapshot afterAction = BattleStatusTiming.Project(
            BattleStatusTimingPoint.EnemyActionCompleted,
            afterSelfDefend);

        Assert.That(initial.Block, Is.EqualTo(8), "初始权威快照不得被投影改写");
        Assert.That(initial.Vulnerable, Is.EqualTo(2));
        Assert.That(beforeEffect.Block, Is.Zero);
        Assert.That(beforeEffect.Vulnerable, Is.EqualTo(2));
        Assert.That(afterAction.Block, Is.EqualTo(5));
        Assert.That(afterAction.Vulnerable, Is.EqualTo(1));

        var dead = new BattleEffectTargetSnapshot(health: 0, block: 4, vulnerable: 3);
        BattleEffectTargetSnapshot deadProjected = BattleStatusTiming.Project(
            BattleStatusTimingPoint.EnemyActionStarted,
            dead);
        Assert.That(deadProjected.Block, Is.EqualTo(4));
        Assert.That(deadProjected.Vulnerable, Is.EqualTo(3));
    }

    /// <summary>验证玩家 Block 记录先于能量/抽牌，弃手记录先于 Vulnerable 衰减。</summary>
    [Test]
    public void PlayerTimingContract_OrdersStatusAgainstEnergyDrawAndDiscard()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            ApplyBlock(combatants, player.Id, player.Id, amount: 4);
            ApplyVulnerable(combatants, enemy.Id, player.Id, amount: 2);
            var timing = new BattleStatusTiming(combatants);

            BattleStatusTimingResult roundStart = timing.Execute(
                BattleStatusTimingPoint.PlayerRoundStart,
                player.Id,
                startingOrder: 0);
            BattleSettlementRecord[] roundStartOrder =
            {
                roundStart.Settlements[0],
                new BattleEnergyRefilledSettlement(1, player.Id, energyBefore: 0, energyAfter: 3),
                new BattleCardMovedSettlement(
                    2,
                    new CardInstanceId(1),
                    BattleCardZone.DrawPile,
                    BattleCardZone.Hand),
            };

            BattleSettlementRecord discard = new BattleCardMovedSettlement(
                0,
                new CardInstanceId(1),
                BattleCardZone.Hand,
                BattleCardZone.DiscardPile);
            BattleStatusTimingResult actionEnded = timing.Execute(
                BattleStatusTimingPoint.PlayerActionEnded,
                player.Id,
                startingOrder: 1);
            BattleSettlementRecord[] actionEndOrder =
            {
                discard,
                actionEnded.Settlements[0],
            };

            Assert.That(roundStartOrder[0].Order, Is.EqualTo(0));
            Assert.That(roundStartOrder[1].Order, Is.EqualTo(1));
            Assert.That(roundStartOrder[2].Order, Is.EqualTo(2));
            Assert.That(roundStartOrder[0], Is.TypeOf<BattleBlockClearedSettlement>());
            Assert.That(roundStartOrder[1], Is.TypeOf<BattleEnergyRefilledSettlement>());
            Assert.That(roundStartOrder[2], Is.TypeOf<BattleCardMovedSettlement>());
            Assert.That(actionEndOrder[0].Order, Is.EqualTo(0));
            Assert.That(actionEndOrder[1].Order, Is.EqualTo(1));
            Assert.That(actionEndOrder[0], Is.TypeOf<BattleCardMovedSettlement>());
            Assert.That(actionEndOrder[1], Is.TypeOf<BattleStatusReducedSettlement>());
        }
    }

    /// <summary>验证状态时机普通失败冻结空结算且不改变任何参与者权威标量。</summary>
    [Test]
    public void Execute_MissingCombatantReturnsFrozenEmptyFailureWithoutWrites()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 2);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 1);
            ApplyBlock(combatants, player.Id, player.Id, amount: 4);
            ApplyVulnerable(combatants, player.Id, enemy.Id, amount: 2);
            int playerBlockBefore = player.CurrentBlock;
            int enemyVulnerableBefore = enemy.CurrentVulnerable;

            BattleStatusTimingResult result = new BattleStatusTiming(combatants).Execute(
                BattleStatusTimingPoint.PlayerRoundStart,
                new CombatantId(9999),
                startingOrder: 0);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotFound));
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(((IList<BattleSettlementRecord>)result.Settlements).IsReadOnly, Is.True);
            Assert.That(player.CurrentBlock, Is.EqualTo(playerBlockBefore));
            Assert.That(enemy.CurrentVulnerable, Is.EqualTo(enemyVulnerableBefore));
            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth));
        }
    }

    /// <summary>通过公共 Effect seam 建立非零 Block 夹具。</summary>
    private static void ApplyBlock(
        BattleCombatantsData combatants,
        CombatantId sourceId,
        CombatantId targetId,
        int amount)
    {
        BattleEffectExecutionResult result = BattleEffectStateTestDriver.Execute(
            combatants,
            sourceId,
            targetId,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            amount);
        Assert.That(result.Succeeded, Is.True);
    }

    /// <summary>通过公共 Effect seam 建立非零 Vulnerable 夹具。</summary>
    private static void ApplyVulnerable(
        BattleCombatantsData combatants,
        CombatantId sourceId,
        CombatantId targetId,
        int amount)
    {
        BattleEffectExecutionResult result = BattleEffectStateTestDriver.Execute(
            combatants,
            sourceId,
            targetId,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            amount);
        Assert.That(result.Succeeded, Is.True);
    }
}
