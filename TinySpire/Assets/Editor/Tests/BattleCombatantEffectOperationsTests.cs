using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleCombatantEffectOperationsTests
{
    /// <summary>验证新参与者的格挡与易伤权威事实初值均为零且可同步读取。</summary>
    [Test]
    public void NewCombatant_ExposesZeroBlockAndVulnerableFacts()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 101,
                maxHealth: 30,
                strength: 2);

            Assert.That(player.Block.CurrentValue, Is.Zero);
            Assert.That(player.CurrentBlock, Is.Zero);
            Assert.That(player.Vulnerable.CurrentValue, Is.Zero);
            Assert.That(player.CurrentVulnerable, Is.Zero);
        }
    }

    /// <summary>验证公共 Effect executor 累加格挡并返回可审计的前后值。</summary>
    [Test]
    public void GainBlock_AccumulatesAuthoritativeFactAndReturnsChange()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 2);

            BattleEffectExecutionResult first = BattleEffectStateTestDriver.Execute(
                combatants,
                player.Id,
                player.Id,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                configuredValue: 5);
            BattleEffectExecutionResult second = BattleEffectStateTestDriver.Execute(
                combatants,
                player.Id,
                player.Id,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                configuredValue: 3);

            var firstRecord = first.Settlements[0] as BattleBlockGainedSettlement;
            var secondRecord = second.Settlements[0] as BattleBlockGainedSettlement;
            Assert.That(first.Succeeded, Is.True);
            Assert.That(firstRecord, Is.Not.Null);
            Assert.That(firstRecord.BlockBefore, Is.Zero);
            Assert.That(firstRecord.BlockAfter, Is.EqualTo(5));
            Assert.That(firstRecord.Amount, Is.EqualTo(5));
            Assert.That(second.Succeeded, Is.True);
            Assert.That(secondRecord, Is.Not.Null);
            Assert.That(secondRecord.BlockBefore, Is.EqualTo(5));
            Assert.That(secondRecord.BlockAfter, Is.EqualTo(8));
            Assert.That(player.CurrentBlock, Is.EqualTo(8));
        }
    }

    /// <summary>验证公共 Effect executor 对力量保留正负配置值与有符号变化。</summary>
    [Test]
    public void ModifyStrength_AppliesPositiveAndNegativeConfiguredValues()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 1);

            BattleEffectExecutionResult increased = BattleEffectStateTestDriver.Execute(
                combatants,
                player.Id,
                player.Id,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                configuredValue: 3);
            BattleEffectExecutionResult decreased = BattleEffectStateTestDriver.Execute(
                combatants,
                player.Id,
                player.Id,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                configuredValue: -5);

            var increasedRecord = increased.Settlements[0] as BattleAttributeModifiedSettlement;
            var decreasedRecord = decreased.Settlements[0] as BattleAttributeModifiedSettlement;
            Assert.That(increasedRecord, Is.Not.Null);
            Assert.That(increasedRecord.ValueBefore, Is.EqualTo(1));
            Assert.That(increasedRecord.ValueAfter, Is.EqualTo(4));
            Assert.That(increasedRecord.Amount, Is.EqualTo(3));
            Assert.That(decreasedRecord, Is.Not.Null);
            Assert.That(decreasedRecord.ValueBefore, Is.EqualTo(4));
            Assert.That(decreasedRecord.ValueAfter, Is.EqualTo(-1));
            Assert.That(decreasedRecord.Amount, Is.EqualTo(-5));
            Assert.That(player.CurrentStrength, Is.EqualTo(-1));
        }
    }

    /// <summary>验证公共 Effect executor 累加非负易伤并返回前后状态。</summary>
    [Test]
    public void ApplyVulnerable_AccumulatesAuthoritativeFact()
    {
        using (var combatants = new BattleCombatantsData())
        {
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);

            BattleEffectExecutionResult first = BattleEffectStateTestDriver.Execute(
                combatants,
                enemy.Id,
                enemy.Id,
                cfg.battle.EffectType.ApplyVulnerable,
                cfg.battle.Attribute.None,
                configuredValue: 2);
            BattleEffectExecutionResult second = BattleEffectStateTestDriver.Execute(
                combatants,
                enemy.Id,
                enemy.Id,
                cfg.battle.EffectType.ApplyVulnerable,
                cfg.battle.Attribute.None,
                configuredValue: 1);

            var firstRecord = first.Settlements[0] as BattleStatusAppliedSettlement;
            var secondRecord = second.Settlements[0] as BattleStatusAppliedSettlement;
            Assert.That(firstRecord, Is.Not.Null);
            Assert.That(firstRecord.ValueBefore, Is.Zero);
            Assert.That(firstRecord.ValueAfter, Is.EqualTo(2));
            Assert.That(secondRecord, Is.Not.Null);
            Assert.That(secondRecord.ValueBefore, Is.EqualTo(2));
            Assert.That(secondRecord.ValueAfter, Is.EqualTo(3));
            Assert.That(enemy.CurrentVulnerable, Is.EqualTo(3));
        }
    }

    /// <summary>验证伤害经公共 executor 先消耗格挡且不提前扣除生命。</summary>
    [Test]
    public void ApplyDamage_BlockFullyAbsorbsAttackBeforeHealth()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = combatants.AddPlayer(101, 30, 1);
            EnemyCombatantData target = combatants.AddEnemy(201, 20, 0);
            BattleEffectStateTestDriver.Execute(
                combatants,
                target.Id,
                target.Id,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                configuredValue: 10);

            BattleEffectExecutionResult result = BattleEffectStateTestDriver.Execute(
                combatants,
                source.Id,
                target.Id,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                configuredValue: 6);

            var damage = result.Settlements[0] as BattleDamageAppliedSettlement;
            Assert.That(result.Succeeded, Is.True);
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.AttackValue, Is.EqualTo(7));
            Assert.That(damage.BlockAbsorbed, Is.EqualTo(7));
            Assert.That(damage.HealthLoss, Is.Zero);
            Assert.That(target.CurrentBlock, Is.EqualTo(3));
            Assert.That(target.CurrentHealth, Is.EqualTo(20));
        }
    }

    /// <summary>验证格挡不足时公共 executor 只把剩余伤害写入生命且生命不低于零。</summary>
    [Test]
    public void ApplyDamage_BlockOverflowReducesHealthByRemainder()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = combatants.AddPlayer(101, 30, 1);
            EnemyCombatantData target = combatants.AddEnemy(201, 20, 0);
            BattleEffectStateTestDriver.Execute(
                combatants,
                target.Id,
                target.Id,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                configuredValue: 2);

            BattleEffectExecutionResult result = BattleEffectStateTestDriver.Execute(
                combatants,
                source.Id,
                target.Id,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                configuredValue: 6);

            var damage = result.Settlements[0] as BattleDamageAppliedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.AttackValue, Is.EqualTo(7));
            Assert.That(damage.BlockAbsorbed, Is.EqualTo(2));
            Assert.That(damage.HealthLoss, Is.EqualTo(5));
            Assert.That(target.CurrentBlock, Is.Zero);
            Assert.That(target.CurrentHealth, Is.EqualTo(15));
        }
    }

    /// <summary>验证致死伤害把生命钳制到零，后续公共请求明确失败且不再写入。</summary>
    [Test]
    public void ApplyDamage_AfterFatalHit_ReturnsTargetNotAliveWithoutFurtherWrites()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData target = combatants.AddEnemy(201, 5, 0);

            BattleEffectExecutionResult fatal = BattleEffectStateTestDriver.Execute(
                combatants,
                source.Id,
                target.Id,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                configuredValue: 8);
            var healthFactAfterFatal = target.Health;
            var blockFactAfterFatal = target.Block;
            BattleEffectExecutionResult repeated = BattleEffectStateTestDriver.Execute(
                combatants,
                source.Id,
                target.Id,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                configuredValue: 8);

            var damage = fatal.Settlements[0] as BattleDamageAppliedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.HealthBefore, Is.EqualTo(5));
            Assert.That(damage.HealthAfter, Is.Zero);
            Assert.That(damage.HealthLoss, Is.EqualTo(5));
            Assert.That(damage.WasFatal, Is.True);
            Assert.That(repeated.Succeeded, Is.False);
            Assert.That(repeated.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotAlive));
            Assert.That(repeated.Settlements, Is.Empty);
            Assert.That(target.Health, Is.SameAs(healthFactAfterFatal));
            Assert.That(target.Block, Is.SameAs(blockFactAfterFatal));
            Assert.That(target.CurrentHealth, Is.Zero);
            Assert.That(target.CurrentBlock, Is.Zero);
        }
    }

    /// <summary>验证通用中毒触发在零层、正数、事实漂移与重复消费下都遵守一次性原子协议。</summary>
    [Test]
    public void PoisonTick_ZeroPositiveAndDriftedPlans_PreserveAtomicOneShotContract()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData target = combatants.AddEnemy(201, 20, 0);
            var poison = new BattlePoisonApplication(combatants);

            BattlePoisonTickPreparationResult zeroPreparation = poison.PrepareTick(target.Id);
            Assert.That(zeroPreparation.Succeeded, Is.True);
            Assert.That(zeroPreparation.Plan.HasWrite, Is.False);
            Assert.That(poison.ValidatePreparedTick(zeroPreparation.Plan), Is.True);
            IReadOnlyList<BattleSettlementRecord> zeroSettlements =
                poison.CommitPreparedTick(zeroPreparation.Plan, startingOrder: 0);

            Assert.That(zeroSettlements, Is.Empty);
            Assert.That(target.CurrentHealth, Is.EqualTo(20));
            Assert.That(target.CurrentBlock, Is.Zero);
            Assert.That(target.CurrentPoison, Is.Zero);

            target.ApplyBlockGain(5);
            BattlePoisonApplicationPreparationResult application = poison.PrepareApply(
                source.Id,
                target.Id,
                amount: 4);
            Assert.That(application.Succeeded, Is.True);
            Assert.That(target.CurrentPoison, Is.Zero);
            Assert.That(poison.ValidatePrepared(application.Plan), Is.True);
            Assert.That(target.CurrentPoison, Is.Zero);
            poison.CommitPrepared(application.Plan, startingOrder: 1);

            BattlePoisonTickPreparationResult positivePreparation = poison.PrepareTick(target.Id);
            Assert.That(positivePreparation.Succeeded, Is.True);
            Assert.That(positivePreparation.Plan.HasWrite, Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(20));
            Assert.That(target.CurrentBlock, Is.EqualTo(5));
            Assert.That(target.CurrentPoison, Is.EqualTo(4));
            Assert.That(poison.ValidatePreparedTick(positivePreparation.Plan), Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(20));
            Assert.That(target.CurrentBlock, Is.EqualTo(5));
            Assert.That(target.CurrentPoison, Is.EqualTo(4));

            IReadOnlyList<BattleSettlementRecord> positiveSettlements =
                poison.CommitPreparedTick(positivePreparation.Plan, startingOrder: 7);
            Assert.That(positiveSettlements, Has.Count.EqualTo(1));
            var ticked = positiveSettlements[0] as BattlePoisonTickedSettlement;
            Assert.That(ticked, Is.Not.Null);
            Assert.That(ticked.Order, Is.EqualTo(7));
            Assert.That(ticked.HealthBefore, Is.EqualTo(20));
            Assert.That(ticked.HealthAfter, Is.EqualTo(16));
            Assert.That(ticked.HealthLoss, Is.EqualTo(4));
            Assert.That(ticked.BlockBefore, Is.EqualTo(5));
            Assert.That(ticked.BlockAfter, Is.EqualTo(5));
            Assert.That(ticked.PoisonBefore, Is.EqualTo(4));
            Assert.That(ticked.PoisonAfter, Is.EqualTo(3));
            Assert.That(ticked.WasFatal, Is.False);
            Assert.That(target.CurrentHealth, Is.EqualTo(16));
            Assert.That(target.CurrentBlock, Is.EqualTo(5));
            Assert.That(target.CurrentPoison, Is.EqualTo(3));
            Assert.Throws<System.InvalidOperationException>(
                () => poison.ValidatePreparedTick(positivePreparation.Plan));
            Assert.Throws<System.InvalidOperationException>(
                () => poison.CommitPreparedTick(positivePreparation.Plan, startingOrder: 8));

            BattlePoisonTickPreparationResult driftedPreparation = poison.PrepareTick(target.Id);
            BattleEffectStateTestDriver.ApplyDamage(
                combatants,
                source.Id,
                target.Id,
                configuredValue: 6);
            Assert.That(target.CurrentHealth, Is.EqualTo(15));
            Assert.That(target.CurrentBlock, Is.Zero);
            Assert.That(target.CurrentPoison, Is.EqualTo(3));
            Assert.That(poison.ValidatePreparedTick(driftedPreparation.Plan), Is.False);
            Assert.Throws<System.InvalidOperationException>(
                () => poison.CommitPreparedTick(driftedPreparation.Plan, startingOrder: 9));
            Assert.That(target.CurrentHealth, Is.EqualTo(15));
            Assert.That(target.CurrentBlock, Is.Zero);
            Assert.That(target.CurrentPoison, Is.EqualTo(3));
        }
    }
}

/// <summary>让既有测试夹具经 M7C 公共 Effect executor 建立受伤或死亡事实。</summary>
internal static class BattleEffectStateTestDriver
{
    private const int FixtureEffectId = 990001;

    /// <summary>通过公共 executor 执行一个测试 Effect 并返回不可变结算结果。</summary>
    internal static BattleEffectExecutionResult Execute(
        BattleCombatantsData combatants,
        CombatantId sourceId,
        CombatantId targetId,
        cfg.battle.EffectType effectType,
        cfg.battle.Attribute attribute,
        int configuredValue)
    {
        cfg.Tables tables = CreateTables(effectType, attribute, configuredValue);
        var executor = new BattleEffectExecutor(tables, combatants);
        return executor.Execute(new BattleEffectExecutionRequest(
            sourceId,
            targetId,
            new[] { new BattleEffectId(FixtureEffectId) }));
    }

    /// <summary>以明确来源和目标执行一次伤害，失败时让夹具立即暴露错误。</summary>
    internal static void ApplyDamage(
        BattleCombatantsData combatants,
        CombatantId sourceId,
        CombatantId targetId,
        int configuredValue)
    {
        BattleEffectExecutionResult result = Execute(
            combatants,
            sourceId,
            targetId,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            configuredValue);
        if (!result.Succeeded)
        {
            throw new System.InvalidOperationException(
                $"测试伤害夹具执行失败：{result.FailureReason}。");
        }
    }

    /// <summary>以明确来源经公共 executor 把目标生命降到零。</summary>
    internal static void Kill(
        BattleCombatantsData combatants,
        CombatantId sourceId,
        CombatantId targetId)
    {
        if (!combatants.TryGet(sourceId, out CombatantData source))
        {
            throw new System.InvalidOperationException("测试伤害来源不存在。");
        }

        if (!combatants.TryGet(targetId, out CombatantData target))
        {
            throw new System.InvalidOperationException("测试伤害目标不存在。");
        }

        int configuredValue = source.CurrentStrength < 0
            ? checked(target.CurrentHealth - source.CurrentStrength)
            : target.CurrentHealth;
        ApplyDamage(combatants, sourceId, targetId, configuredValue);
        if (target.IsAlive)
        {
            throw new System.InvalidOperationException("测试伤害夹具未能令目标死亡。");
        }
    }

    /// <summary>创建只包含一个测试 Effect 的最小 Luban Tables。</summary>
    private static cfg.Tables CreateTables(
        cfg.battle.EffectType effectType,
        cfg.battle.Attribute attribute,
        int configuredValue)
    {
        var effect = new JObject
        {
            ["id"] = FixtureEffectId,
            ["effect_type"] = (int)effectType,
            ["attribute"] = (int)attribute,
            ["value"] = configuredValue,
        };
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = new JArray(),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = new JArray(effect),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = new JArray(),
            ["battle_tbenemybehavior"] = new JArray(),
            ["battle_tbcardupgradelevel"] = new JArray(),
        };
        return new cfg.Tables(tableName => data[tableName]);
    }

}
