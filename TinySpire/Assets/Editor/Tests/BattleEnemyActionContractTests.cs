using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleEnemyActionContractTests
{
    /// <summary>验证 Self 规则精确返回当前行动敌人，不枚举或替换目标。</summary>
    [Test]
    public void Resolve_Self_ReturnsSourceEnemy()
    {
        using (var combatants = new BattleCombatantsData())
        {
            combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                cfg.battle.TargetRule.Self,
                startingOrder: 4);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.Resolved));
            Assert.That(evaluation.TargetId, Is.EqualTo(enemy.Id));
            Assert.That(evaluation.FaultReason, Is.Null);
            Assert.That(evaluation.Settlements, Is.Empty);
        }
    }

    /// <summary>验证活 source 即使使用 Self，也必须先确认至少仍有一名存活玩家。</summary>
    [Test]
    public void Resolve_SelfWithNoLivingPlayer_ReturnsBattleEnded()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 3, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                cfg.battle.TargetRule.Self,
                startingOrder: 0);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.BattleEnded));
            Assert.That(evaluation.TargetId, Is.Null);
            Assert.That(evaluation.FaultReason, Is.Null);
            Assert.That(evaluation.Settlements, Is.Empty);
        }
    }

    /// <summary>验证活 source 即使使用 Self，也不得绕过多存活玩家配置 fault。</summary>
    [Test]
    public void Resolve_SelfWithMultipleLivingPlayers_ReturnsConfigurationFault()
    {
        using (var combatants = new BattleCombatantsData())
        {
            combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            combatants.AddPlayer(templateId: 102, maxHealth: 25, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                cfg.battle.TargetRule.Self,
                startingOrder: 0);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.Faulted));
            Assert.That(evaluation.TargetId, Is.Null);
            Assert.That(evaluation.FaultReason, Is.EqualTo(BattleCommandQueueFaultReason.MultipleLivingPlayers));
            Assert.That(evaluation.Settlements, Is.Empty);
        }
    }

    /// <summary>验证 Enemy 规则只接受当前唯一存活玩家，死亡玩家不会成为候选。</summary>
    [Test]
    public void Resolve_Enemy_ReturnsOnlyLivingPlayer()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData livingPlayer = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            PlayerCombatantData deadPlayer = combatants.AddPlayer(templateId: 102, maxHealth: 25, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, deadPlayer.Id);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                cfg.battle.TargetRule.Enemy,
                startingOrder: 0);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.Resolved));
            Assert.That(evaluation.TargetId, Is.EqualTo(livingPlayer.Id));
            Assert.That(deadPlayer.IsAlive, Is.False);
        }
    }

    /// <summary>验证没有存活玩家时直接返回终局，不伪造目标或 fault。</summary>
    [Test]
    public void Resolve_EnemyWithNoLivingPlayer_ReturnsBattleEnded()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 3, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                cfg.battle.TargetRule.Enemy,
                startingOrder: 0);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.BattleEnded));
            Assert.That(evaluation.TargetId, Is.Null);
            Assert.That(evaluation.FaultReason, Is.Null);
            Assert.That(evaluation.Settlements, Is.Empty);
        }
    }

    /// <summary>验证多名存活玩家进入 configuration fault，不按字典顺序私选目标。</summary>
    [Test]
    public void Resolve_EnemyWithMultipleLivingPlayers_ReturnsConfigurationFault()
    {
        using (var combatants = new BattleCombatantsData())
        {
            combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            combatants.AddPlayer(templateId: 102, maxHealth: 25, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                cfg.battle.TargetRule.Enemy,
                startingOrder: 0);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.Faulted));
            Assert.That(evaluation.TargetId, Is.Null);
            Assert.That(evaluation.FaultReason, Is.EqualTo(BattleCommandQueueFaultReason.MultipleLivingPlayers));
            Assert.That(evaluation.Settlements, Is.Empty);
        }
    }

    /// <summary>验证死亡 source 在目标规则解析前成功跳过，记录只关联 source。</summary>
    [Test]
    public void Resolve_DeadSource_ReturnsSourceOnlySkipBeforeTargetRule()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 2, strength: 0);
            BattleEffectStateTestDriver.Kill(combatants, player.Id, enemy.Id);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                (cfg.battle.TargetRule)99,
                startingOrder: 7);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.SourceNotAlive));
            Assert.That(evaluation.TargetId, Is.Null);
            Assert.That(evaluation.FaultReason, Is.Null);
            Assert.That(evaluation.Settlements, Has.Count.EqualTo(1));
            var skipped = evaluation.Settlements[0] as BattleEnemyActionSkippedSettlement;
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.Order, Is.EqualTo(7));
            Assert.That(skipped.RecordType, Is.EqualTo(BattleSettlementRecordType.EnemyActionSkipped));
            Assert.That(skipped.SourceId, Is.EqualTo(enemy.Id));
            Assert.That(skipped.TargetId, Is.Null);
            Assert.That(skipped.EffectId, Is.Null);
            Assert.That(skipped.Reason, Is.EqualTo(BattleEnemyActionSkipReason.SourceNotAlive));
            Assert.That(
                ((IList<BattleSettlementRecord>)evaluation.Settlements).IsReadOnly,
                Is.True);
        }
    }

    /// <summary>验证存活 source 的未知目标规则明确进入配置 fault。</summary>
    [Test]
    public void Resolve_UnsupportedRule_ReturnsConfigurationFault()
    {
        using (var combatants = new BattleCombatantsData())
        {
            combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            var resolver = new BattleEnemyActionTargetResolver(combatants);

            BattleEnemyActionTargetEvaluation evaluation = resolver.Resolve(
                enemy.Id,
                (cfg.battle.TargetRule)99,
                startingOrder: 0);

            Assert.That(evaluation.Kind, Is.EqualTo(BattleEnemyActionTargetResolutionKind.Faulted));
            Assert.That(evaluation.FaultReason, Is.EqualTo(BattleCommandQueueFaultReason.UnsupportedConfiguration));
            Assert.That(evaluation.TargetId, Is.Null);
        }
    }

    /// <summary>验证终局结果每次都从当前存活事实派生，不缓存胜负镜像。</summary>
    [Test]
    public void TerminalRules_ReevaluatesCurrentLivingFactsWithoutCache()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 3, strength: 0);
            var rules = new BattleTerminalRules(combatants);

            Assert.That(rules.Evaluate(), Is.EqualTo(BattleTerminalOutcome.Ongoing));

            BattleEffectStateTestDriver.Kill(combatants, player.Id, enemy.Id);

            Assert.That(rules.Evaluate(), Is.EqualTo(BattleTerminalOutcome.Victory));
        }
    }

    /// <summary>验证没有存活玩家且仍有存活敌人时即时派生失败。</summary>
    [Test]
    public void TerminalRules_WhenNoPlayerLives_ReturnsDefeat()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 3, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);

            Assert.That(
                new BattleTerminalRules(combatants).Evaluate(),
                Is.EqualTo(BattleTerminalOutcome.Defeat));
        }
    }

    /// <summary>验证双方均无存活者时不私定胜负，保留为显式无效事实供 Queue fault 处理。</summary>
    [Test]
    public void TerminalRules_WhenNeitherSideLives_ReturnsInvalidFacts()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 3, strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 3, strength: 0);
            BattleEffectStateTestDriver.Kill(combatants, player.Id, enemy.Id);
            BattleEffectStateTestDriver.ApplyDamage(
                combatants,
                player.Id,
                player.Id,
                configuredValue: 100);

            Assert.That(
                new BattleTerminalRules(combatants).Evaluate(),
                Is.EqualTo(BattleTerminalOutcome.InvalidFacts));
        }
    }
}
