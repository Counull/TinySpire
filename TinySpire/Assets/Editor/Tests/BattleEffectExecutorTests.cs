using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Core;

public sealed class BattleEffectExecutorTests
{
    /// <summary>列举 prepared repeated-damage 计划在首次提交前必须拒绝的两类漂移。</summary>
    public enum RepeatedDamagePreparedPlanInvalidation
    {
        CrossOwner,
        EnemyHealthDrift,
    }

    /// <summary>验证 Effect 核心直接冻结强类型 ID 顺序，不依赖卡牌绑定结构。</summary>
    [Test]
    public void Execute_OrderedEffectIds_PreservesCoreOrderWithoutCardBindings()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            cfg.Tables tables = CreateTables(
                CreateEffect(3991, cfg.battle.EffectType.ModifyAttribute, cfg.battle.Attribute.Strength, 2),
                CreateEffect(3992, cfg.battle.EffectType.GainBlock, cfg.battle.Attribute.None, 3));
            var request = new BattleEffectExecutionRequest(
                player.Id,
                player.Id,
                new[] { new BattleEffectId(3991), new BattleEffectId(3992) });

            BattleEffectExecutionResult result =
                new BattleEffectExecutor(tables, combatants).Execute(request);

            Assert.That(request.EffectIds, Is.EqualTo(new[]
            {
                new BattleEffectId(3991),
                new BattleEffectId(3992),
            }));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements[0], Is.TypeOf<BattleAttributeModifiedSettlement>());
            Assert.That(result.Settlements[1], Is.TypeOf<BattleBlockGainedSettlement>());
        }
    }

    /// <summary>验证 Strength 绑定经公开 executor seam 修改唯一力量事实并生成一条记录。</summary>
    [Test]
    public void Execute_StrengthBinding_AppliesAttributeAndReturnsSettlement()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 101,
                maxHealth: 30,
                strength: 1);
            cfg.Tables tables = CreateTables(CreateEffect(
                id: 4001,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                value: 3));
            var executor = new BattleEffectExecutor(tables, combatants);
            var request = new BattleEffectExecutionRequest(
                player.Id,
                player.Id,
                new[] { CreateEffectId(effectId: 4001) });

            BattleEffectExecutionResult result = executor.Execute(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(1));
            var settlement = result.Settlements[0] as BattleAttributeModifiedSettlement;
            Assert.That(settlement, Is.Not.Null);
            Assert.That(settlement.Order, Is.Zero);
            Assert.That(settlement.EffectId.Value.Value, Is.EqualTo(4001));
            Assert.That(settlement.SourceId, Is.EqualTo(player.Id));
            Assert.That(settlement.TargetId, Is.EqualTo(player.Id));
            Assert.That(settlement.Attribute, Is.EqualTo(BattleAttributeType.Strength));
            Assert.That(settlement.ValueBefore, Is.EqualTo(1));
            Assert.That(settlement.ValueAfter, Is.EqualTo(4));
            Assert.That(settlement.Amount, Is.EqualTo(3));
            Assert.That(player.CurrentStrength, Is.EqualTo(4));
        }
    }

    /// <summary>验证 Strike 读取当前来源力量并把伤害 outcome 写入目标唯一事实。</summary>
    [Test]
    public void Execute_StrikeBinding_AppliesStrengthAdjustedDamage()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 101,
                maxHealth: 30,
                strength: 2);
            EnemyCombatantData enemy = combatants.AddEnemy(
                templateId: 201,
                maxHealth: 20,
                strength: 0);
            cfg.Tables tables = CreateTables(CreateEffect(
                id: 4002,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                value: 6));
            var executor = new BattleEffectExecutor(tables, combatants);

            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    player.Id,
                    enemy.Id,
                    new[] { CreateEffectId(effectId: 4002) }));

            Assert.That(result.Succeeded, Is.True);
            var settlement = result.Settlements[0] as BattleDamageAppliedSettlement;
            Assert.That(settlement, Is.Not.Null);
            Assert.That(settlement.AttackValue, Is.EqualTo(8));
            Assert.That(settlement.HealthBefore, Is.EqualTo(20));
            Assert.That(settlement.HealthAfter, Is.EqualTo(12));
            Assert.That(settlement.HealthLoss, Is.EqualTo(8));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(12));
        }
    }

    /// <summary>验证 Defend 通过同一 executor seam 累加格挡并生成格挡记录。</summary>
    [Test]
    public void Execute_DefendBinding_GainsBlock()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 101,
                maxHealth: 30,
                strength: 0);
            cfg.Tables tables = CreateTables(CreateEffect(
                id: 4003,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                value: 5));
            var executor = new BattleEffectExecutor(tables, combatants);

            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    player.Id,
                    player.Id,
                    new[] { CreateEffectId(effectId: 4003) }));

            Assert.That(result.Succeeded, Is.True);
            var settlement = result.Settlements[0] as BattleBlockGainedSettlement;
            Assert.That(settlement, Is.Not.Null);
            Assert.That(settlement.BlockBefore, Is.Zero);
            Assert.That(settlement.BlockAfter, Is.EqualTo(5));
            Assert.That(settlement.Amount, Is.EqualTo(5));
            Assert.That(player.CurrentBlock, Is.EqualTo(5));
        }
    }

    /// <summary>验证 Bash 严格按 Damage 再 Vulnerable 的绑定顺序结算，重复执行只读取最新事实。</summary>
    [Test]
    public void Execute_BashBindings_PreserveOrderAndReadLatestFactsOnRepeat()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 40, 0);
            cfg.Tables tables = CreateTables(
                CreateEffect(4004, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 8),
                CreateEffect(4005, cfg.battle.EffectType.ApplyVulnerable, cfg.battle.Attribute.None, 2));
            var executor = new BattleEffectExecutor(tables, combatants);
            var request = new BattleEffectExecutionRequest(
                player.Id,
                enemy.Id,
                new[] { CreateEffectId(4004), CreateEffectId(4005) });

            BattleEffectExecutionResult first = executor.Execute(request);
            BattleEffectExecutionResult second = executor.Execute(request);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Settlements.Count, Is.EqualTo(2));
            var firstDamage = first.Settlements[0] as BattleDamageAppliedSettlement;
            var firstVulnerable = first.Settlements[1] as BattleStatusAppliedSettlement;
            Assert.That(firstDamage, Is.Not.Null);
            Assert.That(firstDamage.Order, Is.Zero);
            Assert.That(firstDamage.EffectId.Value.Value, Is.EqualTo(4004));
            Assert.That(firstDamage.AttackValue, Is.EqualTo(8));
            Assert.That(firstDamage.HealthBefore, Is.EqualTo(40));
            Assert.That(firstDamage.HealthAfter, Is.EqualTo(32));
            Assert.That(firstVulnerable, Is.Not.Null);
            Assert.That(firstVulnerable.Order, Is.EqualTo(1));
            Assert.That(firstVulnerable.EffectId.Value.Value, Is.EqualTo(4005));
            Assert.That(firstVulnerable.Status, Is.EqualTo(BattleStatusType.Vulnerable));
            Assert.That(firstVulnerable.ValueBefore, Is.Zero);
            Assert.That(firstVulnerable.ValueAfter, Is.EqualTo(2));

            Assert.That(second.Succeeded, Is.True);
            var secondDamage = second.Settlements[0] as BattleDamageAppliedSettlement;
            var secondVulnerable = second.Settlements[1] as BattleStatusAppliedSettlement;
            Assert.That(secondDamage, Is.Not.Null);
            Assert.That(secondDamage.AttackValue, Is.EqualTo(12));
            Assert.That(secondDamage.HealthBefore, Is.EqualTo(32));
            Assert.That(secondDamage.HealthAfter, Is.EqualTo(20));
            Assert.That(secondVulnerable, Is.Not.Null);
            Assert.That(secondVulnerable.ValueBefore, Is.EqualTo(2));
            Assert.That(secondVulnerable.ValueAfter, Is.EqualTo(4));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(20));
            Assert.That(enemy.CurrentVulnerable, Is.EqualTo(4));
        }
    }

    /// <summary>验证绑定顺序不会按类型重排，先施加易伤时后续伤害立即读取该事实。</summary>
    [Test]
    public void Execute_VulnerableThenDamage_UsesDeclaredBindingOrder()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            cfg.Tables tables = CreateTables(
                CreateEffect(4101, cfg.battle.EffectType.ApplyVulnerable, cfg.battle.Attribute.None, 1),
                CreateEffect(4102, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6));
            var executor = new BattleEffectExecutor(tables, combatants);

            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    player.Id,
                    enemy.Id,
                    new[] { CreateEffectId(4101), CreateEffectId(4102) }));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements[0], Is.TypeOf<BattleStatusAppliedSettlement>());
            Assert.That(result.Settlements[1], Is.TypeOf<BattleDamageAppliedSettlement>());
            var damage = (BattleDamageAppliedSettlement)result.Settlements[1];
            Assert.That(damage.AttackValue, Is.EqualTo(9));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(11));
        }
    }

    /// <summary>验证前序致死后后续绑定形成成功命令内的明确跳过记录。</summary>
    [Test]
    public void Execute_FatalDamageThenVulnerable_RecordsTargetNotAliveSkip()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 8, 0);
            cfg.Tables tables = CreateTables(
                CreateEffect(4004, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 8),
                CreateEffect(4005, cfg.battle.EffectType.ApplyVulnerable, cfg.battle.Attribute.None, 2));
            var executor = new BattleEffectExecutor(tables, combatants);

            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    player.Id,
                    enemy.Id,
                    new[] { CreateEffectId(4004), CreateEffectId(4005) }));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(2));
            var damage = result.Settlements[0] as BattleDamageAppliedSettlement;
            var skipped = result.Settlements[1] as BattleOperationSkippedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.WasFatal, Is.True);
            Assert.That(damage.HealthAfter, Is.Zero);
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.Order, Is.EqualTo(1));
            Assert.That(skipped.EffectId.Value.Value, Is.EqualTo(4005));
            Assert.That(skipped.Reason, Is.EqualTo(BattleOperationSkipReason.TargetNotAlive));
            Assert.That(enemy.CurrentHealth, Is.Zero);
            Assert.That(enemy.CurrentVulnerable, Is.Zero);
        }
    }

    /// <summary>验证空绑定请求保持 M4～M6 测试卡兼容，成功且零记录零写入。</summary>
    [Test]
    public void Execute_EmptyBindings_SucceedsWithoutWritesOrSettlements()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 2);
            cfg.Tables tables = CreateTables();
            var before = new CombatantFactsSnapshot(player);
            var executor = new BattleEffectExecutor(tables, combatants);

            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    player.Id,
                    player.Id,
                    new BattleEffectId[0]));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Is.Empty);
            before.AssertUnchanged();
        }
    }

    /// <summary>验证默认零值 EffectId 在预构建阶段失败且保持全部参与者事实。</summary>
    [Test]
    public void Execute_InvalidBindings_FailAtomically()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            cfg.Tables tables = CreateTables();
            var executor = new BattleEffectExecutor(tables, combatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    player.Id,
                    enemy.Id,
                    new[] { default(BattleEffectId) })),
                BattleCommandExecutionFailureReason.InvalidEffectBinding,
                new CombatantFactsSnapshot(player),
                new CombatantFactsSnapshot(enemy));
        }
    }

    /// <summary>验证合法首绑定之后的缺失表项仍令整条请求零写入且记录为空。</summary>
    [Test]
    public void Execute_MissingLaterEffect_FailsBeforeFirstWrite()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 1);
            cfg.Tables tables = CreateTables(CreateEffect(
                4201,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                3));
            var before = new CombatantFactsSnapshot(player);
            var executor = new BattleEffectExecutor(tables, combatants);

            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    player.Id,
                    player.Id,
                    new[] { CreateEffectId(4201), CreateEffectId(999999) }));

            AssertAtomicFailure(
                result,
                BattleCommandExecutionFailureReason.EffectTemplateNotFound,
                before);
        }
    }

    /// <summary>验证未知 EffectType 在预构建阶段明确失败且不改任何事实。</summary>
    [Test]
    public void Execute_UnknownEffectType_FailsAtomically()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            cfg.Tables tables = CreateTables(CreateEffect(
                4301,
                (cfg.battle.EffectType)99,
                cfg.battle.Attribute.None,
                1));
            var executor = new BattleEffectExecutor(tables, combatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    player.Id,
                    player.Id,
                    new[] { CreateEffectId(4301) })),
                BattleCommandExecutionFailureReason.UnsupportedEffectType,
                new CombatantFactsSnapshot(player));
        }
    }

    /// <summary>验证 Modify 的未知属性与非属性 Effect 的 Strength 属性都不会静默退化。</summary>
    [Test]
    public void Execute_UnsupportedAttributes_FailAtomically()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            cfg.Tables tables = CreateTables(
                CreateEffect(4401, cfg.battle.EffectType.ModifyAttribute, (cfg.battle.Attribute)99, 1),
                CreateEffect(4402, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.Strength, 1));
            var executor = new BattleEffectExecutor(tables, combatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    player.Id,
                    player.Id,
                    new[] { CreateEffectId(4401) })),
                BattleCommandExecutionFailureReason.UnsupportedEffectAttribute,
                new CombatantFactsSnapshot(player));
            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    player.Id,
                    player.Id,
                    new[] { CreateEffectId(4402) })),
                BattleCommandExecutionFailureReason.UnsupportedEffectAttribute,
                new CombatantFactsSnapshot(player));
        }
    }

    /// <summary>验证来源与目标不存在时分别返回稳定原因且不改现有参与者事实。</summary>
    [Test]
    public void Execute_MissingSourceOrTarget_FailsAtomically()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            cfg.Tables tables = CreateTables(CreateEffect(
                4501,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                1));
            var executor = new BattleEffectExecutor(tables, combatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    default(CombatantId),
                    enemy.Id,
                    new[] { CreateEffectId(4501) })),
                BattleCommandExecutionFailureReason.EffectSourceNotFound,
                new CombatantFactsSnapshot(player),
                new CombatantFactsSnapshot(enemy));
            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    player.Id,
                    default(CombatantId),
                    new[] { CreateEffectId(4501) })),
                BattleCommandExecutionFailureReason.TargetNotFound,
                new CombatantFactsSnapshot(player),
                new CombatantFactsSnapshot(enemy));
        }
    }

    /// <summary>验证死亡来源与死亡目标在任何新写入前分别失败。</summary>
    [Test]
    public void Execute_DeadSourceOrTarget_FailsAtomically()
    {
        cfg.Tables tables = CreateTables(CreateEffect(
            4601,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            30));

        using (var deadSourceCombatants = new BattleCombatantsData())
        {
            PlayerCombatantData deadSource = deadSourceCombatants.AddPlayer(101, 5, 0);
            EnemyCombatantData target = deadSourceCombatants.AddEnemy(201, 30, 0);
            BattleEffectStateTestDriver.Kill(
                deadSourceCombatants,
                target.Id,
                deadSource.Id);
            var executor = new BattleEffectExecutor(tables, deadSourceCombatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    deadSource.Id,
                    target.Id,
                    new[] { CreateEffectId(4601) })),
                BattleCommandExecutionFailureReason.EffectSourceNotAlive,
                new CombatantFactsSnapshot(deadSource),
                new CombatantFactsSnapshot(target));
        }

        using (var deadTargetCombatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = deadTargetCombatants.AddPlayer(101, 30, 0);
            EnemyCombatantData deadTarget = deadTargetCombatants.AddEnemy(201, 5, 0);
            BattleEffectStateTestDriver.Kill(
                deadTargetCombatants,
                source.Id,
                deadTarget.Id);
            var executor = new BattleEffectExecutor(tables, deadTargetCombatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    source.Id,
                    deadTarget.Id,
                    new[] { CreateEffectId(4601) })),
                BattleCommandExecutionFailureReason.TargetNotAlive,
                new CombatantFactsSnapshot(source),
                new CombatantFactsSnapshot(deadTarget));
        }
    }

    /// <summary>验证后序数值溢出会让已经可执行的首绑定也保持零写入。</summary>
    [Test]
    public void Execute_LaterStrengthOverflow_FailsBeforeFirstWrite()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, int.MaxValue - 1);
            cfg.Tables tables = CreateTables(CreateEffect(
                4701,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                1));
            var executor = new BattleEffectExecutor(tables, combatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    player.Id,
                    player.Id,
                    new[] { CreateEffectId(4701), CreateEffectId(4701) })),
                BattleCommandExecutionFailureReason.EffectValueOverflow,
                new CombatantFactsSnapshot(player));
        }
    }

    /// <summary>验证致死后的后续绑定仍完成表校验，缺失项会令整条链保持零写入。</summary>
    [Test]
    public void Execute_MissingEffectAfterSimulatedFatalHit_FailsBeforeFirstWrite()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 5, 0);
            cfg.Tables tables = CreateTables(CreateEffect(
                4801,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                10));
            var executor = new BattleEffectExecutor(tables, combatants);

            AssertAtomicFailure(
                executor.Execute(new BattleEffectExecutionRequest(
                    player.Id,
                    enemy.Id,
                    new[] { CreateEffectId(4801), CreateEffectId(999999) })),
                BattleCommandExecutionFailureReason.EffectTemplateNotFound,
                new CombatantFactsSnapshot(player),
                new CombatantFactsSnapshot(enemy));
        }
    }

    /// <summary>验证外部目标 Effect 在预构建时冻结目标最终投影，同时保持 source 投影与权威事实一致且零写入。</summary>
    [Test]
    public void Prepare_Damage_FreezesTargetProjectionWithoutWrites()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = combatants.AddPlayer(101, 30, 2);
            EnemyCombatantData target = combatants.AddEnemy(201, 8, 0);
            cfg.Tables tables = CreateTables(CreateEffect(
                4901,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                4));
            var executor = new BattleEffectExecutor(tables, combatants);

            BattleEffectPreparationResult preparation = executor.Prepare(
                new BattleEffectExecutionRequest(
                    source.Id,
                    target.Id,
                    new[] { CreateEffectId(4901) }));

            Assert.That(preparation.Succeeded, Is.True);
            Assert.That(preparation.Plan.ProjectedTargetAfterEffect.Health, Is.EqualTo(2));
            Assert.That(preparation.Plan.ProjectedTargetAfterEffect.Block, Is.Zero);
            Assert.That(preparation.Plan.ProjectedTargetAfterEffect.Vulnerable, Is.Zero);
            Assert.That(preparation.Plan.ProjectedSourceAfterEffect.Health, Is.EqualTo(30));
            Assert.That(preparation.Plan.ProjectedSourceAfterEffect.Block, Is.Zero);
            Assert.That(preparation.Plan.ProjectedSourceAfterEffect.Vulnerable, Is.Zero);
            Assert.That(source.CurrentHealth, Is.EqualTo(30));
            Assert.That(target.CurrentHealth, Is.EqualTo(8));
        }
    }

    /// <summary>验证 Self Effect 以调用方投影预构建时，source 与 target 最终投影保持完全一致且不写权威事实。</summary>
    [Test]
    public void PrepareProjected_SelfTarget_KeepsSourceAndTargetProjectionAligned()
    {
        using (var combatants = new BattleCombatantsData())
        {
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            cfg.Tables tables = CreateTables(CreateEffect(
                4902,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                5));
            var executor = new BattleEffectExecutor(tables, combatants);
            var projected = new BattleEffectTargetSnapshot(20, 0, 2);

            BattleEffectPreparationResult preparation = executor.PrepareProjected(
                new BattleEffectExecutionRequest(
                    enemy.Id,
                    enemy.Id,
                    new[] { CreateEffectId(4902) }),
                projected,
                projected);

            Assert.That(preparation.Succeeded, Is.True);
            Assert.That(preparation.Plan.ProjectedTargetAfterEffect.Health, Is.EqualTo(20));
            Assert.That(preparation.Plan.ProjectedTargetAfterEffect.Block, Is.EqualTo(5));
            Assert.That(preparation.Plan.ProjectedTargetAfterEffect.Vulnerable, Is.EqualTo(2));
            Assert.That(preparation.Plan.ProjectedSourceAfterEffect.Health,
                Is.EqualTo(preparation.Plan.ProjectedTargetAfterEffect.Health));
            Assert.That(preparation.Plan.ProjectedSourceAfterEffect.Block,
                Is.EqualTo(preparation.Plan.ProjectedTargetAfterEffect.Block));
            Assert.That(preparation.Plan.ProjectedSourceAfterEffect.Vulnerable,
                Is.EqualTo(preparation.Plan.ProjectedTargetAfterEffect.Vulnerable));
            Assert.That(enemy.CurrentBlock, Is.Zero);
            Assert.That(enemy.CurrentVulnerable, Is.Zero);
        }
    }

    /// <summary>验证同一条 Effect 链预演两段敌方伤害时只预约一层缓冲，预演零写入且提交记录连续。</summary>
    [Test]
    public void PrepareAndCommit_MachineGunnerBuffer_ReservesOnlyFirstDamageInSameEffectChain()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            cfg.Tables tables = CreateTables(
                CreateEffect(5001, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6),
                CreateEffect(5002, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6));
            using var runtime = new MachineGunnerBattleRuntime(
                combatants,
                new[] { enemy.Id },
                player.Id);
            runtime.CombatState.Add(player.Id, MachineGunnerCombatantStatus.Buffer, 1);
            var executor = new BattleEffectExecutor(tables, combatants, runtime);

            BattleEffectPreparationResult preparation = executor.Prepare(
                new BattleEffectExecutionRequest(
                    enemy.Id,
                    player.Id,
                    new[] { CreateEffectId(5001), CreateEffectId(5002) }));

            Assert.That(preparation.Succeeded, Is.True);
            Assert.That(preparation.Plan.PlannedSettlementCount, Is.EqualTo(3));
            Assert.That(player.CurrentHealth, Is.EqualTo(30));
            Assert.That(
                runtime.CombatState.Get(player.Id, MachineGunnerCombatantStatus.Buffer),
                Is.EqualTo(1));
            executor.ValidatePreparedExecution(preparation.Plan, startingOrder: 0);
            BattleEffectExecutionResult result = executor.CommitPrepared(preparation.Plan);
            BattleDamageAppliedSettlement firstDamage =
                (BattleDamageAppliedSettlement)result.Settlements[0];
            MachineGunnerPrivateStatusChangedSettlement consumed =
                (MachineGunnerPrivateStatusChangedSettlement)result.Settlements[1];
            BattleDamageAppliedSettlement secondDamage =
                (BattleDamageAppliedSettlement)result.Settlements[2];

            Assert.That(firstDamage.Order, Is.Zero);
            Assert.That(firstDamage.AttackValue, Is.EqualTo(6));
            Assert.That(firstDamage.HealthBefore, Is.EqualTo(30));
            Assert.That(firstDamage.HealthAfter, Is.EqualTo(30));
            Assert.That(consumed.Order, Is.EqualTo(1));
            Assert.That(consumed.Status, Is.EqualTo(MachineGunnerCombatantStatus.Buffer));
            Assert.That(consumed.ValueBefore, Is.EqualTo(1));
            Assert.That(consumed.ValueAfter, Is.Zero);
            Assert.That(secondDamage.Order, Is.EqualTo(2));
            Assert.That(secondDamage.AttackValue, Is.EqualTo(6));
            Assert.That(secondDamage.HealthBefore, Is.EqualTo(30));
            Assert.That(secondDamage.HealthAfter, Is.EqualTo(24));
            Assert.That(player.CurrentHealth, Is.EqualTo(24));
            Assert.That(
                runtime.CombatState.Get(player.Id, MachineGunnerCombatantStatus.Buffer),
                Is.Zero);
        }
    }

    /// <summary>验证无实体在同一条多段敌方攻击链中逐段预约、先把伤害封顶再过格挡，并在耗尽后恢复原伤害。</summary>
    [Test]
    public void PrepareAndCommit_MachineGunnerIntangible_CapsEachReservedAttackBeforeBlock()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            player.ApplyBlockGain(2);
            cfg.Tables tables = CreateTables(
                CreateEffect(5011, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6),
                CreateEffect(5012, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6),
                CreateEffect(5013, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6));
            using var runtime = new MachineGunnerBattleRuntime(
                combatants,
                new[] { enemy.Id },
                player.Id);
            runtime.CombatState.Add(player.Id, MachineGunnerCombatantStatus.Intangible, 2);
            var executor = new BattleEffectExecutor(tables, combatants, runtime);

            BattleEffectPreparationResult preparation = executor.Prepare(
                new BattleEffectExecutionRequest(
                    enemy.Id,
                    player.Id,
                    new[] { CreateEffectId(5011), CreateEffectId(5012), CreateEffectId(5013) }));

            Assert.That(preparation.Succeeded, Is.True);
            Assert.That(preparation.Plan.PlannedSettlementCount, Is.EqualTo(5));
            Assert.That(player.CurrentHealth, Is.EqualTo(30));
            Assert.That(player.CurrentBlock, Is.EqualTo(2));
            Assert.That(
                runtime.CombatState.Get(player.Id, MachineGunnerCombatantStatus.Intangible),
                Is.EqualTo(2));
            executor.ValidatePreparedExecution(preparation.Plan, startingOrder: 0);
            BattleEffectExecutionResult result = executor.CommitPrepared(preparation.Plan);

            BattleDamageAppliedSettlement firstDamage =
                (BattleDamageAppliedSettlement)result.Settlements[0];
            MachineGunnerPrivateStatusChangedSettlement firstIntangible =
                (MachineGunnerPrivateStatusChangedSettlement)result.Settlements[1];
            BattleDamageAppliedSettlement secondDamage =
                (BattleDamageAppliedSettlement)result.Settlements[2];
            MachineGunnerPrivateStatusChangedSettlement secondIntangible =
                (MachineGunnerPrivateStatusChangedSettlement)result.Settlements[3];
            BattleDamageAppliedSettlement thirdDamage =
                (BattleDamageAppliedSettlement)result.Settlements[4];

            Assert.That(firstDamage.AttackValue, Is.EqualTo(1));
            Assert.That(firstDamage.BlockBefore, Is.EqualTo(2));
            Assert.That(firstDamage.BlockAfter, Is.EqualTo(1));
            Assert.That(firstDamage.HealthAfter, Is.EqualTo(30));
            Assert.That(firstIntangible.Status, Is.EqualTo(MachineGunnerCombatantStatus.Intangible));
            Assert.That(firstIntangible.ValueBefore, Is.EqualTo(2));
            Assert.That(firstIntangible.ValueAfter, Is.EqualTo(1));
            Assert.That(secondDamage.AttackValue, Is.EqualTo(1));
            Assert.That(secondDamage.BlockBefore, Is.EqualTo(1));
            Assert.That(secondDamage.BlockAfter, Is.Zero);
            Assert.That(secondDamage.HealthAfter, Is.EqualTo(30));
            Assert.That(secondIntangible.ValueBefore, Is.EqualTo(1));
            Assert.That(secondIntangible.ValueAfter, Is.Zero);
            Assert.That(thirdDamage.AttackValue, Is.EqualTo(6));
            Assert.That(thirdDamage.HealthAfter, Is.EqualTo(24));
            Assert.That(player.CurrentHealth, Is.EqualTo(24));
            Assert.That(player.CurrentBlock, Is.Zero);
            Assert.That(
                runtime.CombatState.Get(player.Id, MachineGunnerCombatantStatus.Intangible),
                Is.Zero);
        }
    }

    /// <summary>验证缓冲优先完全抵挡首段攻击且不消耗无实体，下一段才由无实体封顶并消费。</summary>
    [Test]
    public void PrepareAndCommit_MachineGunnerBuffer_PrioritizesOverIntangible()
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData player = combatants.AddPlayer(101, 30, 0);
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            cfg.Tables tables = CreateTables(
                CreateEffect(5021, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6),
                CreateEffect(5022, cfg.battle.EffectType.DealDamage, cfg.battle.Attribute.None, 6));
            using var runtime = new MachineGunnerBattleRuntime(
                combatants,
                new[] { enemy.Id },
                player.Id);
            runtime.CombatState.Add(player.Id, MachineGunnerCombatantStatus.Buffer, 1);
            runtime.CombatState.Add(player.Id, MachineGunnerCombatantStatus.Intangible, 1);
            var executor = new BattleEffectExecutor(tables, combatants, runtime);

            BattleEffectPreparationResult preparation = executor.Prepare(
                new BattleEffectExecutionRequest(
                    enemy.Id,
                    player.Id,
                    new[] { CreateEffectId(5021), CreateEffectId(5022) }));

            Assert.That(preparation.Succeeded, Is.True);
            executor.ValidatePreparedExecution(preparation.Plan, startingOrder: 0);
            BattleEffectExecutionResult result = executor.CommitPrepared(preparation.Plan);

            BattleDamageAppliedSettlement firstDamage =
                (BattleDamageAppliedSettlement)result.Settlements[0];
            MachineGunnerPrivateStatusChangedSettlement buffer =
                (MachineGunnerPrivateStatusChangedSettlement)result.Settlements[1];
            BattleDamageAppliedSettlement secondDamage =
                (BattleDamageAppliedSettlement)result.Settlements[2];
            MachineGunnerPrivateStatusChangedSettlement intangible =
                (MachineGunnerPrivateStatusChangedSettlement)result.Settlements[3];

            Assert.That(firstDamage.AttackValue, Is.EqualTo(6));
            Assert.That(firstDamage.HealthAfter, Is.EqualTo(30));
            Assert.That(buffer.Status, Is.EqualTo(MachineGunnerCombatantStatus.Buffer));
            Assert.That(buffer.ValueBefore, Is.EqualTo(1));
            Assert.That(buffer.ValueAfter, Is.Zero);
            Assert.That(secondDamage.AttackValue, Is.EqualTo(1));
            Assert.That(secondDamage.HealthAfter, Is.EqualTo(29));
            Assert.That(intangible.Status, Is.EqualTo(MachineGunnerCombatantStatus.Intangible));
            Assert.That(intangible.ValueBefore, Is.EqualTo(1));
            Assert.That(intangible.ValueAfter, Is.Zero);
        }
    }

    /// <summary>验证随机逐段伤害只在提交时写入冻结目标并推进一次随机流，准备、校验和重复提交都保持原子性。</summary>
    [Test]
    public void RandomPlan_PrepareValidateCommitAndRepeat_PreservesAtomicRandomContract()
    {
        const uint expectedRandomBefore = 332584831u;
        const uint expectedRandomAfter = 3348358578u;
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = combatants.AddPlayer(101, 30, 2);
            EnemyCombatantData firstEnemy = combatants.AddEnemy(201, 5, 0);
            EnemyCombatantData secondEnemy = combatants.AddEnemy(202, 5, 0);
            EnemyCombatantData thirdEnemy = combatants.AddEnemy(203, 5, 0);
            var enemies = new[] { firstEnemy, secondEnemy, thirdEnemy };
            var random = new GameRandom(1234u);
            var executor = new BattleRepeatedDamageExecutor(
                combatants,
                enemies.Select(enemy => enemy.Id).ToArray(),
                random);
            var request = new BattleRepeatedDamageRequest(
                source.Id,
                fixedTargetId: null,
                BattleRepeatedDamageTargetPolicy.RandomLivingEnemyPerHit,
                new[]
                {
                    new BattleRepeatedDamageHitRequest(new BattleEffectId(4407), 3),
                    new BattleRepeatedDamageHitRequest(new BattleEffectId(4407), 3),
                    new BattleRepeatedDamageHitRequest(new BattleEffectId(4407), 3),
                });
            var sourceBefore = new CombatantFactsSnapshot(source);
            CombatantFactsSnapshot[] enemiesBefore = enemies
                .Select(enemy => new CombatantFactsSnapshot(enemy))
                .ToArray();

            BattleRepeatedDamagePreparationResult preparation = executor.Prepare(request);

            Assert.That(preparation.Succeeded, Is.True);
            BattlePreparedRepeatedDamagePlan plan = preparation.Plan;
            Assert.That(plan.RandomStateBefore, Is.EqualTo(expectedRandomBefore));
            Assert.That(plan.RandomStateAfter, Is.EqualTo(expectedRandomAfter));
            Assert.That(executor.RandomState, Is.EqualTo(expectedRandomBefore));
            Assert.That(
                plan.Segments.Select(segment => segment.TargetId),
                Is.EqualTo(enemies.Select(enemy => enemy.Id)));
            sourceBefore.AssertUnchanged();
            foreach (CombatantFactsSnapshot snapshot in enemiesBefore)
                snapshot.AssertUnchanged();

            executor.ValidatePrepared(plan, startingOrder: 5);

            Assert.That(plan.IsValidated, Is.True);
            Assert.That(plan.IsConsumed, Is.False);
            Assert.That(executor.RandomState, Is.EqualTo(expectedRandomBefore));
            sourceBefore.AssertUnchanged();
            foreach (CombatantFactsSnapshot snapshot in enemiesBefore)
                snapshot.AssertUnchanged();

            IReadOnlyList<BattleSettlementRecord> settlements = executor.CommitPrepared(plan);

            Assert.That(plan.IsConsumed, Is.True);
            Assert.That(settlements, Has.Count.EqualTo(3));
            Assert.That(
                settlements.Select(settlement => settlement.Order),
                Is.EqualTo(new[] { 5, 6, 7 }));
            Assert.That(
                settlements.Cast<BattleDamageAppliedSettlement>()
                    .Select(settlement => settlement.TargetId),
                Is.EqualTo(enemies.Select(enemy => enemy.Id)));
            Assert.That(
                settlements.Cast<BattleDamageAppliedSettlement>()
                    .Select(settlement =>
                        (settlement.AttackValue, settlement.HealthBefore, settlement.HealthAfter)),
                Is.EqualTo(new[] { (5, 5, 0), (5, 5, 0), (5, 5, 0) }));
            Assert.That(executor.RandomState, Is.EqualTo(expectedRandomAfter));
            Assert.That(enemies.Select(enemy => enemy.CurrentHealth), Is.EqualTo(new[] { 0, 0, 0 }));
            sourceBefore.AssertUnchanged();
            var sourceAfterCommit = new CombatantFactsSnapshot(source);
            CombatantFactsSnapshot[] enemiesAfterCommit = enemies
                .Select(enemy => new CombatantFactsSnapshot(enemy))
                .ToArray();

            Assert.Throws<InvalidOperationException>(() => executor.CommitPrepared(plan));

            sourceAfterCommit.AssertUnchanged();
            foreach (CombatantFactsSnapshot snapshot in enemiesAfterCommit)
                snapshot.AssertUnchanged();
            Assert.That(executor.RandomState, Is.EqualTo(expectedRandomAfter));
        }
    }

    /// <summary>验证跨执行器计划与敌方生命漂移都在首次写入前被拒绝，且不推进随机流、不改其他事实或消费计划。</summary>
    [TestCase(RepeatedDamagePreparedPlanInvalidation.CrossOwner)]
    [TestCase(RepeatedDamagePreparedPlanInvalidation.EnemyHealthDrift)]
    public void ValidatePrepared_CrossOwnerOrEnemyHealthDrift_RejectsWithoutWritesOrConsumption(
        RepeatedDamagePreparedPlanInvalidation invalidation)
    {
        using (var combatants = new BattleCombatantsData())
        {
            PlayerCombatantData source = combatants.AddPlayer(101, 30, 2);
            EnemyCombatantData firstEnemy = combatants.AddEnemy(201, 20, 0);
            EnemyCombatantData secondEnemy = combatants.AddEnemy(202, 20, 0);
            EnemyCombatantData thirdEnemy = combatants.AddEnemy(203, 20, 0);
            var enemies = new[] { firstEnemy, secondEnemy, thirdEnemy };
            CombatantId[] enemyIds = enemies.Select(enemy => enemy.Id).ToArray();
            var owner = new BattleRepeatedDamageExecutor(
                combatants,
                enemyIds,
                new GameRandom(1234u));
            var foreignOwner = new BattleRepeatedDamageExecutor(
                combatants,
                enemyIds,
                new GameRandom(4321u));
            var request = new BattleRepeatedDamageRequest(
                source.Id,
                fixedTargetId: null,
                BattleRepeatedDamageTargetPolicy.RandomLivingEnemyPerHit,
                new[]
                {
                    new BattleRepeatedDamageHitRequest(new BattleEffectId(4407), 3),
                    new BattleRepeatedDamageHitRequest(new BattleEffectId(4407), 3),
                    new BattleRepeatedDamageHitRequest(new BattleEffectId(4407), 3),
                });
            BattleRepeatedDamagePreparationResult preparation = owner.Prepare(request);
            Assert.That(preparation.Succeeded, Is.True);
            BattlePreparedRepeatedDamagePlan plan = preparation.Plan;
            if (invalidation == RepeatedDamagePreparedPlanInvalidation.EnemyHealthDrift)
            {
                BattleCombatantEffectOperationResult drift =
                    new BattleCombatantEffectOperations(combatants).ApplyDamage(
                        source.Id,
                        firstEnemy.Id,
                        configuredValue: 1);
                Assert.That(drift.Status, Is.EqualTo(BattleCombatantEffectOperationStatus.Applied));
                Assert.That(firstEnemy.CurrentHealth, Is.EqualTo(17));
            }

            var sourceBeforeValidation = new CombatantFactsSnapshot(source);
            CombatantFactsSnapshot[] enemiesBeforeValidation = enemies
                .Select(enemy => new CombatantFactsSnapshot(enemy))
                .ToArray();
            uint ownerRandomBeforeValidation = owner.RandomState;
            uint foreignRandomBeforeValidation = foreignOwner.RandomState;

            Assert.Throws<InvalidOperationException>(() =>
            {
                if (invalidation == RepeatedDamagePreparedPlanInvalidation.CrossOwner)
                    foreignOwner.ValidatePrepared(plan, startingOrder: 5);
                else
                    owner.ValidatePrepared(plan, startingOrder: 5);
            });

            sourceBeforeValidation.AssertUnchanged();
            foreach (CombatantFactsSnapshot snapshot in enemiesBeforeValidation)
                snapshot.AssertUnchanged();
            Assert.That(owner.RandomState, Is.EqualTo(ownerRandomBeforeValidation));
            Assert.That(foreignOwner.RandomState, Is.EqualTo(foreignRandomBeforeValidation));
            Assert.That(plan.IsValidated, Is.False);
            Assert.That(plan.IsConsumed, Is.False);
        }
    }

    /// <summary>统一断言预构建失败原因、空记录以及全部只读事实对象和值不变。</summary>
    private static void AssertAtomicFailure(
        BattleEffectExecutionResult result,
        BattleCommandExecutionFailureReason expectedReason,
        params CombatantFactsSnapshot[] snapshots)
    {
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.FailureReason, Is.EqualTo(expectedReason));
        Assert.That(result.Settlements, Is.Empty);
        foreach (CombatantFactsSnapshot snapshot in snapshots)
        {
            snapshot.AssertUnchanged();
        }
    }

    /// <summary>创建只包含指定 Effect 的最小 Luban Tables。</summary>
    private static cfg.Tables CreateTables(params JObject[] effects)
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = new JArray(),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = new JArray(effects),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = new JArray(),
            ["battle_tbenemybehavior"] = new JArray(),
            ["battle_tbcardupgradelevel"] = new JArray(),
        };
        return new cfg.Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>创建一条最小静态 Effect JSON。</summary>
    private static JObject CreateEffect(
        int id,
        cfg.battle.EffectType effectType,
        cfg.battle.Attribute attribute,
        int value)
    {
        return new JObject
        {
            ["id"] = id,
            ["effect_type"] = (int)effectType,
            ["attribute"] = (int)attribute,
            ["value"] = value,
        };
    }

    /// <summary>创建测试核心直接消费的强类型 Effect 标识。</summary>
    private static BattleEffectId CreateEffectId(int effectId)
    {
        return new BattleEffectId(effectId);
    }

    /// <summary>冻结一个参与者四项只读事实对象与同步值，供原子性断言复用。</summary>
    private sealed class CombatantFactsSnapshot
    {
        private readonly CombatantData _combatant;
        private readonly object _health;
        private readonly object _strength;
        private readonly object _block;
        private readonly object _vulnerable;
        private readonly int _healthValue;
        private readonly int _strengthValue;
        private readonly int _blockValue;
        private readonly int _vulnerableValue;

        /// <summary>冻结指定参与者当前的四项权威事实引用和值。</summary>
        internal CombatantFactsSnapshot(CombatantData combatant)
        {
            _combatant = combatant;
            _health = combatant.Health;
            _strength = combatant.Strength;
            _block = combatant.Block;
            _vulnerable = combatant.Vulnerable;
            _healthValue = combatant.CurrentHealth;
            _strengthValue = combatant.CurrentStrength;
            _blockValue = combatant.CurrentBlock;
            _vulnerableValue = combatant.CurrentVulnerable;
        }

        /// <summary>断言四项只读事实仍是同一对象且同步值完全未变。</summary>
        internal void AssertUnchanged()
        {
            Assert.That(_combatant.Health, Is.SameAs(_health));
            Assert.That(_combatant.Strength, Is.SameAs(_strength));
            Assert.That(_combatant.Block, Is.SameAs(_block));
            Assert.That(_combatant.Vulnerable, Is.SameAs(_vulnerable));
            Assert.That(_combatant.CurrentHealth, Is.EqualTo(_healthValue));
            Assert.That(_combatant.CurrentStrength, Is.EqualTo(_strengthValue));
            Assert.That(_combatant.CurrentBlock, Is.EqualTo(_blockValue));
            Assert.That(_combatant.CurrentVulnerable, Is.EqualTo(_vulnerableValue));
        }
    }
}
