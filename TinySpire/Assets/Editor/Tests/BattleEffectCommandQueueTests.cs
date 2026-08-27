using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Run;

public sealed class BattleEffectCommandQueueTests
{
    /// <summary>验证 Strength 经唯一队列按能量、Effect、当前卡归弃牌的事务顺序结算。</summary>
    [Test]
    public void Submit_StrengthCard_RecordsEnergyThenEffectThenDiscard()
    {
        JObject strength = CreateCard(
            3001,
            cost: 0,
            cfg.battle.TargetRule.Self,
            4001);
        JObject strengthEffect = CreateEffect(
            4001,
            cfg.battle.EffectType.ModifyAttribute,
            cfg.battle.Attribute.Strength,
            value: 3);
        using (var scenario = new QueueScenario(
                   new[] { strength },
                   new[] { strengthEffect },
                   new[] { 3001 },
                   playerStrength: 1,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3001);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Player.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var strengthRecord = result.Settlements[1] as BattleAttributeModifiedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(3));
            Assert.That(strengthRecord, Is.Not.Null);
            Assert.That(strengthRecord.Order, Is.EqualTo(1));
            Assert.That(strengthRecord.EffectId.Value.Value, Is.EqualTo(4001));
            Assert.That(strengthRecord.ValueBefore, Is.EqualTo(1));
            Assert.That(strengthRecord.ValueAfter, Is.EqualTo(4));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(4));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Strike 经队列读取来源力量与目标易伤，并先以格挡吸收后再扣生命。</summary>
    [Test]
    public void Submit_StrikeCard_UsesStrengthVulnerableAndBlockBeforeDiscard()
    {
        JObject strike = CreateCard(
            3002,
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4101);
        JObject setup = CreateCard(
            3998,
            cost: 0,
            cfg.battle.TargetRule.Enemy,
            4198,
            4199);
        JObject damage = CreateEffect(
            4101,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 6);
        JObject block = CreateEffect(
            4198,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 5);
        JObject vulnerable = CreateEffect(
            4199,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 1);
        using (var scenario = new QueueScenario(
                   new[] { strike, setup },
                   new[] { damage, block, vulnerable },
                   new[] { 3002 },
                   playerStrength: 2,
                   energyPerRound: 3))
        {
            scenario.ExecuteSetupCardEffects(3998, scenario.Enemy.Id);
            CardInstanceId cardId = scenario.FindCard(3002);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(2));
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.Order, Is.EqualTo(1));
            Assert.That(damageRecord.AttackValue, Is.EqualTo(12));
            Assert.That(damageRecord.BlockBefore, Is.EqualTo(5));
            Assert.That(damageRecord.BlockAfter, Is.Zero);
            Assert.That(damageRecord.BlockAbsorbed, Is.EqualTo(5));
            Assert.That(damageRecord.HealthBefore, Is.EqualTo(40));
            Assert.That(damageRecord.HealthAfter, Is.EqualTo(33));
            Assert.That(damageRecord.HealthLoss, Is.EqualTo(7));
            Assert.That(damageRecord.WasFatal, Is.False);
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(scenario.Enemy.CurrentBlock, Is.Zero);
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(33));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>有限轨道一级 Strike 必须把同一实例的明确伤害九真实送入 Effect 执行。</summary>
    [Test]
    public void Submit_FiniteLevelOneStrike_ExecutesConfiguredDamageValue()
    {
        JObject strike = CreateCard(
            3002,
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4101);
        strike["upgrade_track_kind"] = (int)cfg.battle.CardUpgradeTrackKind.Finite;
        JObject damage = CreateEffect(
            4101,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 6);
        JObject upgrade = CreateUpgradeLevel(
            cardId: 3002,
            nextUpgradeLevel: 1,
            cost: 1,
            cfg.battle.CardPlayDestination.DiscardPile,
            cfg.battle.CardUpgradeRuleKind.DamageValue,
            ruleValue: 9);
        var runCard = new RunCard(
            new RunCardInstanceId(1),
            templateId: 3002,
            upgradeLevel: 1);
        using (var scenario = new QueueScenario(
                   new[] { strike },
                   new[] { damage },
                   deck: Array.Empty<int>(),
                   playerStrength: 0,
                   energyPerRound: 3,
                   runCards: new[] { runCard },
                   upgradeLevels: new[] { upgrade }))
        {
            CardInstanceId cardId = scenario.FindRunCard(runCard.InstanceId);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            var damageRecord = scenario.Presentation.Results[1].Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .Single();
            Assert.That(damageRecord.AttackValue, Is.EqualTo(9));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(31));
        }
    }

    /// <summary>同模板基础与无限二级实例必须分别执行三十二与五十二，禁止按模板混同。</summary>
    [Test]
    public void Submit_SameInfiniteTemplateAtDifferentLevels_ExecutesPerInstanceDamage()
    {
        JObject bludgeon = CreateCard(
            3123,
            cost: 3,
            cfg.battle.TargetRule.Enemy,
            4107);
        bludgeon["upgrade_track_kind"] = (int)cfg.battle.CardUpgradeTrackKind.Infinite;
        bludgeon["infinite_upgrade_rule_kind"] =
            (int)cfg.battle.CardUpgradeRuleKind.DamageValue;
        bludgeon["infinite_upgrade_value_per_level"] = 10;
        JObject damage = CreateEffect(
            4107,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 32);
        var baseCard = new RunCard(
            new RunCardInstanceId(1),
            templateId: 3123,
            upgradeLevel: 0);
        var upgradedCard = new RunCard(
            new RunCardInstanceId(2),
            templateId: 3123,
            upgradeLevel: 2);
        using (var scenario = new QueueScenario(
                   new[] { bludgeon },
                   new[] { damage },
                   deck: Array.Empty<int>(),
                   playerStrength: 0,
                   energyPerRound: 6,
                   enemyHealth: 200,
                   runCards: new[] { baseCard, upgradedCard }))
        {
            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                scenario.FindRunCard(baseCard.InstanceId),
                scenario.Enemy.Id));
            scenario.Presentation.CompleteNext();
            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                scenario.FindRunCard(upgradedCard.InstanceId),
                scenario.Enemy.Id));

            int[] attacks = scenario.Presentation.Results
                .SelectMany(result => result.Settlements)
                .OfType<BattleDamageAppliedSettlement>()
                .Select(record => record.AttackValue)
                .ToArray();
            Assert.That(attacks, Is.EqualTo(new[] { 32, 52 }));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(116));
        }
    }

    /// <summary>验证 Defend 经队列支付一点能量、增加五点格挡，并最后弃置当前卡牌。</summary>
    [Test]
    public void Submit_DefendCard_GainsBlockBeforeDiscard()
    {
        JObject defend = CreateCard(
            3003,
            cost: 1,
            cfg.battle.TargetRule.Self,
            4201);
        JObject block = CreateEffect(
            4201,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 5);
        using (var scenario = new QueueScenario(
                   new[] { defend },
                   new[] { block },
                   new[] { 3003 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3003);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Player.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var blockRecord = result.Settlements[1] as BattleBlockGainedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(2));
            Assert.That(blockRecord, Is.Not.Null);
            Assert.That(blockRecord.Order, Is.EqualTo(1));
            Assert.That(blockRecord.BlockBefore, Is.Zero);
            Assert.That(blockRecord.BlockAfter, Is.EqualTo(5));
            Assert.That(blockRecord.Amount, Is.EqualTo(5));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(5));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Bash 严格按伤害、易伤绑定顺序执行，再把卡牌移入弃牌堆。</summary>
    [Test]
    public void Submit_BashCard_RecordsDamageThenVulnerableThenDiscard()
    {
        JObject bash = CreateCard(
            3004,
            cost: 2,
            cfg.battle.TargetRule.Enemy,
            4301,
            4302);
        JObject damage = CreateEffect(
            4301,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 8);
        JObject vulnerable = CreateEffect(
            4302,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 2);
        using (var scenario = new QueueScenario(
                   new[] { bash },
                   new[] { damage, vulnerable },
                   new[] { 3004 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3004);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(4));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var statusRecord = result.Settlements[2] as BattleStatusAppliedSettlement;
            var moved = result.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(1));
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.Order, Is.EqualTo(1));
            Assert.That(damageRecord.EffectId.Value.Value, Is.EqualTo(4301));
            Assert.That(damageRecord.AttackValue, Is.EqualTo(8));
            Assert.That(damageRecord.HealthAfter, Is.EqualTo(32));
            Assert.That(statusRecord, Is.Not.Null);
            Assert.That(statusRecord.Order, Is.EqualTo(2));
            Assert.That(statusRecord.EffectId.Value.Value, Is.EqualTo(4302));
            Assert.That(statusRecord.Status, Is.EqualTo(BattleStatusType.Vulnerable));
            Assert.That(statusRecord.ValueBefore, Is.Zero);
            Assert.That(statusRecord.ValueAfter, Is.EqualTo(2));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(3));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(32));
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.EqualTo(2));
        }
    }

    /// <summary>验证 Tremble 严格按能量、易伤、消耗归宿顺序完成唯一队列事务。</summary>
    [Test]
    public void Submit_TrembleConfiguredExhaust_RecordsEnergyThenVulnerableThenExhaust()
    {
        JObject tremble = CreateCard(
            3118,
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4006);
        tremble["external_key"] = "TREMBLE";
        tremble["catalog_snapshot_key"] = "sts2-v0.107.1-23811903-59260271";
        tremble["play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        tremble["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        JObject vulnerable = CreateEffect(
            4006,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 3);
        using (var scenario = new QueueScenario(
                   new[] { tremble },
                   new[] { vulnerable },
                   new[] { 3118 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3118);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var status = result.Settlements[1] as BattleStatusAppliedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(2));
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Order, Is.EqualTo(1));
            Assert.That(status.Status, Is.EqualTo(BattleStatusType.Vulnerable));
            Assert.That(status.ValueBefore, Is.Zero);
            Assert.That(status.ValueAfter, Is.EqualTo(3));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Queue.Queue.CurrentValue.IsFaulted, Is.False);
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Phase,
                Is.EqualTo(BattleTurnPhase.PlayerAction));
        }
    }

    /// <summary>验证 Bludgeon 经公共队列支付三点能量、造成三十二点伤害，再把当前牌移入弃牌堆。</summary>
    [Test]
    public void Submit_Bludgeon_DealsThirtyTwoThenDiscards()
    {
        JObject bludgeon = CreateIroncladCard(
            3123,
            "BLUDGEON",
            cost: 3,
            cfg.battle.TargetRule.Enemy,
            4401);
        JObject damage = CreateEffect(
            4401,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 32);
        using (var scenario = new QueueScenario(
                   new[] { bludgeon },
                   new[] { damage },
                   new[] { 3123 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3123);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.Amount, Is.EqualTo(3));
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.Zero);
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.Order, Is.EqualTo(1));
            Assert.That(damageRecord.EffectId.Value.Value, Is.EqualTo(4401));
            Assert.That(damageRecord.AttackValue, Is.EqualTo(32));
            Assert.That(damageRecord.HealthBefore, Is.EqualTo(40));
            Assert.That(damageRecord.HealthAfter, Is.EqualTo(8));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(8));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Twin Strike 的两个五点伤害绑定按顺序独立结算，随后才把当前牌移入弃牌堆。</summary>
    [Test]
    public void Submit_TwinStrike_DealsFiveTwiceThenDiscards()
    {
        JObject twinStrike = CreateIroncladCard(
            3120,
            "TWIN_STRIKE",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4402,
            4402);
        JObject damage = CreateEffect(
            4402,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 5);
        using (var scenario = new QueueScenario(
                   new[] { twinStrike },
                   new[] { damage },
                   new[] { 3120 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3120);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(4));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var firstDamage = result.Settlements[1] as BattleDamageAppliedSettlement;
            var secondDamage = result.Settlements[2] as BattleDamageAppliedSettlement;
            var moved = result.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.Amount, Is.EqualTo(1));
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(2));
            Assert.That(firstDamage, Is.Not.Null);
            Assert.That(firstDamage.Order, Is.EqualTo(1));
            Assert.That(firstDamage.EffectId.Value.Value, Is.EqualTo(4402));
            Assert.That(firstDamage.AttackValue, Is.EqualTo(5));
            Assert.That(firstDamage.HealthBefore, Is.EqualTo(40));
            Assert.That(firstDamage.HealthAfter, Is.EqualTo(35));
            Assert.That(secondDamage, Is.Not.Null);
            Assert.That(secondDamage.Order, Is.EqualTo(2));
            Assert.That(secondDamage.EffectId.Value.Value, Is.EqualTo(4402));
            Assert.That(secondDamage.AttackValue, Is.EqualTo(5));
            Assert.That(secondDamage.HealthBefore, Is.EqualTo(35));
            Assert.That(secondDamage.HealthAfter, Is.EqualTo(30));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(3));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(30));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Sword Boomerang 按固定战斗随机流逐段重选存活敌人，并在三段致死后连续完成弃牌与胜利结算。</summary>
    [Test]
    public void Submit_SwordBoomerang_PerHitRandomTargetsExcludeProjectedDeathsAndSettleContinuously()
    {
        const int cardTemplateId = 3116;
        const int damageEffectId = 4407;
        JObject swordBoomerang = CreateIroncladCard(
            cardTemplateId,
            "SWORD_BOOMERANG",
            cost: 1,
            cfg.battle.TargetRule.RandomEnemy,
            damageEffectId,
            damageEffectId,
            damageEffectId);
        swordBoomerang["card_type"] = (int)cfg.battle.CardType.Attack;
        JObject damage = CreateEffect(
            damageEffectId,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 3);
        using (var scenario = new QueueScenario(
                   new[] { swordBoomerang },
                   new[] { damage },
                   new[] { cardTemplateId },
                   playerStrength: 2,
                   energyPerRound: 3,
                   enemyHealths: new[] { 5, 5, 5 },
                   battleSeed: 1234))
        {
            CardInstanceId cardId = scenario.FindCard(cardTemplateId);
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    targetId: null));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(6));
            Assert.That(
                terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 6)));

            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            BattleDamageAppliedSettlement[] damages = terminal.Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            var moved = terminal.Settlements[4] as BattleCardMovedSettlement;
            var phaseChanged = terminal.Settlements[5] as BattlePhaseChangedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(
                (energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 2, 1)));
            Assert.That(damages, Has.Length.EqualTo(3));
            Assert.That(
                damages.Select(item => item.TargetId.Value),
                Is.EqualTo(new[]
                {
                    scenario.Enemies[0].Id,
                    scenario.Enemies[1].Id,
                    scenario.Enemies[2].Id,
                }));
            Assert.That(
                damages.Select(item => item.EffectId.Value.Value),
                Is.EqualTo(new[] { damageEffectId, damageEffectId, damageEffectId }));
            Assert.That(
                damages.Select(item =>
                    (item.AttackValue, item.HealthBefore, item.HealthAfter, item.WasFatal)),
                Is.EqualTo(new[]
                {
                    (5, 5, 0, true),
                    (5, 5, 0, true),
                    (5, 5, 0, true),
                }));
            Assert.That(moved, Is.Not.Null);
            Assert.That(
                (moved.CardId, moved.FromZone, moved.ToZone),
                Is.EqualTo((cardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
            Assert.That(phaseChanged, Is.Not.Null);
            Assert.That(phaseChanged.PhaseAfter, Is.EqualTo(BattleTurnPhase.BattleEnded));
            Assert.That(scenario.Enemies.Select(enemy => enemy.CurrentHealth),
                Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Sword Boomerang 拒绝显式目标时不会支付能量、造成伤害、移动卡牌、发布结果或推进目标随机流。</summary>
    [Test]
    public void Submit_SwordBoomerang_WithExplicitTarget_FailsWithoutEnergyDamageCardMoveOrRandomAdvance()
    {
        const int cardTemplateId = 3116;
        const int damageEffectId = 4407;
        JObject swordBoomerang = CreateIroncladCard(
            cardTemplateId,
            "SWORD_BOOMERANG",
            cost: 1,
            cfg.battle.TargetRule.RandomEnemy,
            damageEffectId,
            damageEffectId,
            damageEffectId);
        swordBoomerang["card_type"] = (int)cfg.battle.CardType.Attack;
        JObject damage = CreateEffect(
            damageEffectId,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 3);
        using (var scenario = new QueueScenario(
                   new[] { swordBoomerang },
                   new[] { damage },
                   new[] { cardTemplateId },
                   playerStrength: 2,
                   energyPerRound: 3,
                   enemyHealths: new[] { 5, 5, 5 },
                   battleSeed: 1234))
        {
            CardInstanceId cardId = scenario.FindCard(cardTemplateId);
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            int energyBefore = turnBefore.Players[scenario.Player.Id].Energy;
            int[] healthBefore = scenario.Enemies
                .Select(enemy => enemy.CurrentHealth)
                .ToArray();
            CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
            CardInstanceId[] discardBefore = scenario.Zones.DiscardPile.ToArray();
            BattleCommandExecutionResult[] resultsBefore =
                scenario.Presentation.Results.ToArray();
            uint randomBefore = scenario.Queue.CardTargetRandomState;
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Enemy.Id));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(energyBefore));
            Assert.That(
                scenario.Enemies.Select(enemy => enemy.CurrentHealth),
                Is.EqualTo(healthBefore));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardBefore));
            Assert.That(scenario.Presentation.Results, Is.EqualTo(resultsBefore));
            Assert.That(scenario.Queue.CardTargetRandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证 Body Slam 经公共队列读取来源当前七点格挡作为伤害，并原子完成能量、伤害与弃牌结算。</summary>
    [Test]
    public void Submit_BodySlam_WithSevenBlock_DealsExactlySevenAndSettlesCardAtomically()
    {
        const int bodySlamTemplateId = 3105;
        const int setupTemplateId = 3996;
        const int setupBlockEffectId = 4492;
        const int bodySlamDamageEffectId = 4493;
        JObject bodySlam = CreateIroncladCard(
            bodySlamTemplateId,
            "BODY_SLAM",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            bodySlamDamageEffectId);
        JObject setup = CreateCard(
            setupTemplateId,
            cost: 0,
            cfg.battle.TargetRule.Self,
            setupBlockEffectId);
        JObject setupBlock = CreateEffect(
            setupBlockEffectId,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 7);
        JObject bodySlamDamage = CreateEffect(
            bodySlamDamageEffectId,
            cfg.battle.EffectType.DealDamageFromSourceBlock,
            cfg.battle.Attribute.None,
            value: 0);
        using (var scenario = new QueueScenario(
                   new[] { bodySlam, setup },
                   new[] { setupBlock, bodySlamDamage },
                   new[] { bodySlamTemplateId },
                   playerStrength: 0,
                   energyPerRound: 3,
                   enemyHealth: 20))
        {
            scenario.ExecuteSetupCardEffects(setupTemplateId, scenario.Player.Id);
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(7));
            CardInstanceId cardId = scenario.FindCard(bodySlamTemplateId);
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Enemy.Id));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(3));
            Assert.That(
                terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 3)));
            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            var damage = terminal.Settlements[1] as BattleDamageAppliedSettlement;
            var moved = terminal.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(
                (energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 2, 1)));
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.EffectId.Value.Value, Is.EqualTo(bodySlamDamageEffectId));
            Assert.That(
                (damage.AttackValue, damage.HealthBefore, damage.HealthAfter),
                Is.EqualTo((7, 20, 13)));
            Assert.That(moved, Is.Not.Null);
            Assert.That(
                (moved.CardId, moved.FromZone, moved.ToZone),
                Is.EqualTo((cardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(7));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(13));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Body Slam 冻结来源格挡作为攻击基础值，并继续套用力量、易伤与目标格挡的普通伤害公式。</summary>
    [Test]
    public void Submit_BodySlam_UsesFrozenBlockAsAttackBaseBeforeStrengthVulnerableAndTargetBlock()
    {
        const int bodySlamTemplateId = 3105;
        const int sourceSetupTemplateId = 3995;
        const int targetSetupTemplateId = 3994;
        const int sourceBlockEffectId = 4489;
        const int targetBlockEffectId = 4490;
        const int targetVulnerableEffectId = 4491;
        const int bodySlamDamageEffectId = 4494;
        JObject bodySlam = CreateIroncladCard(
            bodySlamTemplateId,
            "BODY_SLAM",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            bodySlamDamageEffectId);
        JObject sourceSetup = CreateCard(
            sourceSetupTemplateId,
            cost: 0,
            cfg.battle.TargetRule.Self,
            sourceBlockEffectId);
        JObject targetSetup = CreateCard(
            targetSetupTemplateId,
            cost: 0,
            cfg.battle.TargetRule.Enemy,
            targetBlockEffectId,
            targetVulnerableEffectId);
        JObject sourceBlock = CreateEffect(
            sourceBlockEffectId,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 7);
        JObject targetBlock = CreateEffect(
            targetBlockEffectId,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 5);
        JObject targetVulnerable = CreateEffect(
            targetVulnerableEffectId,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 1);
        JObject bodySlamDamage = CreateEffect(
            bodySlamDamageEffectId,
            cfg.battle.EffectType.DealDamageFromSourceBlock,
            cfg.battle.Attribute.None,
            value: 0);
        using (var scenario = new QueueScenario(
                   new[] { bodySlam, sourceSetup, targetSetup },
                   new[] { sourceBlock, targetBlock, targetVulnerable, bodySlamDamage },
                   new[] { bodySlamTemplateId },
                   playerStrength: 2,
                   energyPerRound: 3,
                   enemyHealth: 20))
        {
            scenario.ExecuteSetupCardEffects(sourceSetupTemplateId, scenario.Player.Id);
            scenario.ExecuteSetupCardEffects(targetSetupTemplateId, scenario.Enemy.Id);
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(7));
            Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(2));
            Assert.That(scenario.Enemy.CurrentBlock, Is.EqualTo(5));
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.EqualTo(1));
            CardInstanceId cardId = scenario.FindCard(bodySlamTemplateId);
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Enemy.Id));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(3));
            Assert.That(
                terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 3)));
            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            var damage = terminal.Settlements[1] as BattleDamageAppliedSettlement;
            var moved = terminal.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(
                (energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 2, 1)));
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.EffectId.Value.Value, Is.EqualTo(bodySlamDamageEffectId));
            Assert.That(
                (
                    damage.AttackValue,
                    damage.BlockBefore,
                    damage.BlockAfter,
                    damage.BlockAbsorbed,
                    damage.HealthBefore,
                    damage.HealthAfter,
                    damage.HealthLoss,
                    damage.WasFatal),
                Is.EqualTo((13, 5, 0, 5, 20, 12, 8, false)));
            Assert.That(moved, Is.Not.Null);
            Assert.That(
                (moved.CardId, moved.FromZone, moved.ToZone),
                Is.EqualTo((cardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(7));
            Assert.That(scenario.Enemy.CurrentBlock, Is.Zero);
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.EqualTo(1));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(12));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(2));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Pommel Strike 经公共队列先造成九点伤害，再抽一张牌，最后弃置当前牌。</summary>
    [Test]
    public void Submit_PommelStrike_DealsNineThenDrawsOneThenDiscards()
    {
        JObject pommelStrike = CreateIroncladCard(
            3113,
            "POMMEL_STRIKE",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4403,
            4404);
        JObject damage = CreateEffect(
            4403,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 9);
        JObject draw = CreateEffect(
            4404,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        using (var scenario = new QueueScenario(
                   new[] { pommelStrike },
                   new[] { damage, draw },
                   new[] { 3113, 3113 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId cardId = scenario.FindCard(3113);
            Assert.That(scenario.Zones.DrawPile, Has.Count.EqualTo(1));
            CardInstanceId drawnCardId = scenario.Zones.DrawPile[0];

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Enemy.Id));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(4));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var drawn = result.Settlements[2] as BattleCardMovedSettlement;
            var discarded = result.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.Amount, Is.EqualTo(1));
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.Order, Is.EqualTo(1));
            Assert.That(damageRecord.AttackValue, Is.EqualTo(9));
            Assert.That(drawn, Is.Not.Null);
            Assert.That(drawn.Order, Is.EqualTo(2));
            Assert.That(drawn.CardId, Is.EqualTo(drawnCardId));
            Assert.That(drawn.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(drawn.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(discarded, Is.Not.Null);
            Assert.That(discarded.Order, Is.EqualTo(3));
            Assert.That(discarded.CardId, Is.EqualTo(cardId));
            Assert.That(discarded.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(discarded.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(31));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { drawnCardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Shrug It Off 经公共队列先获得八点格挡，再抽一张牌，最后弃置当前牌。</summary>
    [Test]
    public void Submit_ShrugItOff_GainsEightBlockThenDrawsOneThenDiscards()
    {
        JObject shrugItOff = CreateIroncladCard(
            3115,
            "SHRUG_IT_OFF",
            cost: 1,
            cfg.battle.TargetRule.Self,
            4405,
            4406);
        JObject block = CreateEffect(
            4405,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 8);
        JObject draw = CreateEffect(
            4406,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        using (var scenario = new QueueScenario(
                   new[] { shrugItOff },
                   new[] { block, draw },
                   new[] { 3115, 3115 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId cardId = scenario.FindCard(3115);
            Assert.That(scenario.Zones.DrawPile, Has.Count.EqualTo(1));
            CardInstanceId drawnCardId = scenario.Zones.DrawPile[0];

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Player.Id));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(4));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var blockRecord = result.Settlements[1] as BattleBlockGainedSettlement;
            var drawn = result.Settlements[2] as BattleCardMovedSettlement;
            var discarded = result.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.Amount, Is.EqualTo(1));
            Assert.That(blockRecord, Is.Not.Null);
            Assert.That(blockRecord.Order, Is.EqualTo(1));
            Assert.That(blockRecord.Amount, Is.EqualTo(8));
            Assert.That(blockRecord.BlockBefore, Is.Zero);
            Assert.That(blockRecord.BlockAfter, Is.EqualTo(8));
            Assert.That(drawn, Is.Not.Null);
            Assert.That(drawn.Order, Is.EqualTo(2));
            Assert.That(drawn.CardId, Is.EqualTo(drawnCardId));
            Assert.That(drawn.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(drawn.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(discarded, Is.Not.Null);
            Assert.That(discarded.Order, Is.EqualTo(3));
            Assert.That(discarded.CardId, Is.EqualTo(cardId));
            Assert.That(discarded.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(discarded.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(8));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { drawnCardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Not Yet 经公共队列把治疗封顶到生命上限，并在治疗后消耗来源牌。</summary>
    [Test]
    public void Submit_NotYet_CapsHealAtMaximumAndExhaustsThroughSharedQueue()
    {
        const int notYetTemplateId = 3171;
        const int setupTemplateId = 3997;
        const int healEffectId = 4413;
        const int setupDamageEffectId = 4497;
        cfg.battle.EffectType healEffectType = cfg.battle.EffectType.Heal;
        JObject notYet = CreateIroncladCard(
            notYetTemplateId,
            "NOT_YET",
            cost: 2,
            cfg.battle.TargetRule.Self,
            healEffectId);
        notYet["play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        notYet["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        JObject setup = CreateCard(
            setupTemplateId,
            cost: 0,
            cfg.battle.TargetRule.Self,
            setupDamageEffectId);
        JObject heal = CreateEffect(
            healEffectId,
            healEffectType,
            cfg.battle.Attribute.None,
            value: 10);
        JObject setupDamage = CreateEffect(
            setupDamageEffectId,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 7);
        using (var scenario = new QueueScenario(
                   new[] { notYet, setup },
                   new[] { heal, setupDamage },
                   new[] { notYetTemplateId },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            scenario.ExecuteSetupCardEffects(setupTemplateId, scenario.Player.Id);
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(23));
            CardInstanceId cardId = scenario.FindCard(notYetTemplateId);
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Player.Id));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(3));
            Assert.That(terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 3)));

            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            var healthRestored = terminal.Settlements[1] as BattleHealthRestoredSettlement;
            var moved = terminal.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(
                (energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 1, 2)));
            Assert.That(healthRestored, Is.Not.Null);
            Assert.That(
                (healthRestored.RequestedAmount,
                    healthRestored.HealthBefore,
                    healthRestored.HealthAfter,
                    healthRestored.Amount),
                Is.EqualTo((10, 23, 30, 7)));
            Assert.That(moved, Is.Not.Null);
            Assert.That(
                (moved.CardId, moved.FromZone, moved.ToZone),
                Is.EqualTo((cardId, BattleCardZone.Hand, BattleCardZone.ExhaustPile)));

            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(30));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(1));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Not Yet 在满生命时记录零点实际治疗，并仍经公共队列支付能量和消耗来源牌。</summary>
    [Test]
    public void Submit_NotYet_AtFullHealth_RecordsZeroHealAndStillExhaustsThroughSharedQueue()
    {
        const int notYetTemplateId = 3171;
        const int healEffectId = 4413;
        cfg.battle.EffectType healEffectType = cfg.battle.EffectType.Heal;
        JObject notYet = CreateIroncladCard(
            notYetTemplateId,
            "NOT_YET",
            cost: 2,
            cfg.battle.TargetRule.Self,
            healEffectId);
        notYet["play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        notYet["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        JObject heal = CreateEffect(
            healEffectId,
            healEffectType,
            cfg.battle.Attribute.None,
            value: 10);
        using (var scenario = new QueueScenario(
                   new[] { notYet },
                   new[] { heal },
                   new[] { notYetTemplateId },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(30));
            CardInstanceId cardId = scenario.FindCard(notYetTemplateId);
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Player.Id));

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(3));
            Assert.That(
                terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 3)));

            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            var healthRestored = terminal.Settlements[1] as BattleHealthRestoredSettlement;
            var moved = terminal.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(
                (energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 1, 2)));
            Assert.That(healthRestored, Is.Not.Null);
            Assert.That(
                (healthRestored.RequestedAmount,
                    healthRestored.HealthBefore,
                    healthRestored.HealthAfter,
                    healthRestored.Amount),
                Is.EqualTo((10, 30, 30, 0)));
            Assert.That(moved, Is.Not.Null);
            Assert.That(
                (moved.CardId, moved.FromZone, moved.ToZone),
                Is.EqualTo((cardId, BattleCardZone.Hand, BattleCardZone.ExhaustPile)));

            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(30));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(1));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证治疗之后存在缺失 Effect 时，会在能量、生命与卡区写入前整体失败。</summary>
    [Test]
    public void Submit_HealBeforeMissingLaterEffect_FailsBeforeEnergyHealthOrCardZoneWrites()
    {
        const int notYetTemplateId = 3171;
        const int setupTemplateId = 3997;
        const int healEffectId = 4413;
        const int setupDamageEffectId = 4497;
        const int missingEffectId = 499998;
        cfg.battle.EffectType healEffectType = cfg.battle.EffectType.Heal;
        JObject notYet = CreateIroncladCard(
            notYetTemplateId,
            "NOT_YET",
            cost: 2,
            cfg.battle.TargetRule.Self,
            healEffectId,
            missingEffectId);
        notYet["play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        notYet["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.ExhaustPile;
        JObject setup = CreateCard(
            setupTemplateId,
            cost: 0,
            cfg.battle.TargetRule.Self,
            setupDamageEffectId);
        JObject heal = CreateEffect(
            healEffectId,
            healEffectType,
            cfg.battle.Attribute.None,
            value: 10);
        JObject setupDamage = CreateEffect(
            setupDamageEffectId,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 7);
        using (var scenario = new QueueScenario(
                   new[] { notYet, setup },
                   new[] { heal, setupDamage },
                   new[] { notYetTemplateId },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            scenario.ExecuteSetupCardEffects(setupTemplateId, scenario.Player.Id);
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(23));
            CardInstanceId cardId = scenario.FindCard(notYetTemplateId);
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            var healthBefore = scenario.Player.Health;
            KeyValuePair<CardInstanceId, CardInstanceData>[] cardsBefore =
                scenario.Zones.Cards.ToArray();
            uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
            BattleCommandExecutionResult[] resultsBefore =
                scenario.Presentation.Results.ToArray();
            int turnPublicationCount = 0;
            int layoutPublicationCount = 0;
            int healthPublicationCount = 0;
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission;
            using (scenario.Queue.Turn.Skip(1).Subscribe(_ => turnPublicationCount++))
            using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            using (scenario.Player.Health.Skip(1).Subscribe(_ => healthPublicationCount++))
            {
                submission = scenario.Queue.Submit(
                    new PlayCardCommand(
                        scenario.Player.Id,
                        cardId,
                        scenario.Player.Id));
            }

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(
                terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.EffectTemplateNotFound));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Player.Health, Is.SameAs(healthBefore));
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(23));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(scenario.Zones.ExhaustPile, Is.Empty);
            Assert.That(scenario.Zones.Cards.ToArray(), Is.EqualTo(cardsBefore));
            Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
            Assert.That(scenario.Presentation.Results, Is.EqualTo(resultsBefore));
            Assert.That(turnPublicationCount, Is.Zero);
            Assert.That(layoutPublicationCount, Is.Zero);
            Assert.That(healthPublicationCount, Is.Zero);
        }
    }

    /// <summary>验证 Burning Pact 经公共队列消耗所选手牌、抽两张牌，再于最后弃置来源牌。</summary>
    [Test]
    public void Submit_BurningPact_SelectedOtherCard_ExhaustsThenDrawsTwoThenDiscardsSource()
    {
        const cfg.battle.EffectType exhaustSelectedHandCard = cfg.battle.EffectType.ExhaustSelectedHandCard;
        JObject burningPact = CreateIroncladCard(
            3125,
            "BURNING_PACT",
            cost: 1,
            cfg.battle.TargetRule.Self,
            4411,
            4412);
        JObject exhaust = CreateEffect(
            4411,
            exhaustSelectedHandCard,
            cfg.battle.Attribute.None,
            value: 1);
        JObject draw = CreateEffect(
            4412,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 2);
        using (var scenario = new QueueScenario(
                   new[] { burningPact },
                   new[] { exhaust, draw },
                   Enumerable.Repeat(3125, 4),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 2))
        {
            CardInstanceId sourceCardId = scenario.Zones.Hand[0];
            CardInstanceId selectedCardId = scenario.Zones.Hand[1];
            CardInstanceId firstDrawnCardId =
                scenario.Zones.DrawPile[scenario.Zones.DrawPile.Count - 1];
            CardInstanceId secondDrawnCardId =
                scenario.Zones.DrawPile[scenario.Zones.DrawPile.Count - 2];
            int layoutPublicationCount = 0;
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            var unselectedCommand = new PlayCardCommand(
                scenario.Player.Id,
                sourceCardId,
                scenario.Player.Id);
            BattleCardPlayEvaluation selectionEvaluation =
                scenario.Queue.CardPlayRules.Evaluate(
                    scenario.Queue.Turn.CurrentValue,
                    unselectedCommand);
            Assert.That(
                selectionEvaluation.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.CardSelectionRequired));
            Assert.That(selectionEvaluation.HandCardSelectionRequest, Is.Not.Null);
            Assert.That(selectionEvaluation.HandCardSelectionRequest.RequiredCount, Is.EqualTo(1));
            Assert.That(
                selectionEvaluation.HandCardSelectionRequest.LegalCardIds,
                Is.EqualTo(new[] { selectedCardId }));

            BattleCommandSubmissionResult submission;
            using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            {
                submission = scenario.Queue.Submit(new PlayCardCommand(
                    scenario.Player.Id,
                    sourceCardId,
                    scenario.Player.Id,
                    new[] { selectedCardId }));
            }

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(5));
            Assert.That(terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 5)));

            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            var selectedMove = terminal.Settlements[1] as BattleCardMovedSettlement;
            var firstDraw = terminal.Settlements[2] as BattleCardMovedSettlement;
            var secondDraw = terminal.Settlements[3] as BattleCardMovedSettlement;
            var sourceMove = terminal.Settlements[4] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 2, 1)));
            Assert.That(selectedMove, Is.Not.Null);
            Assert.That(
                (selectedMove.CardId, selectedMove.FromZone, selectedMove.ToZone),
                Is.EqualTo((selectedCardId, BattleCardZone.Hand, BattleCardZone.ExhaustPile)));
            Assert.That(firstDraw, Is.Not.Null);
            Assert.That(
                (firstDraw.CardId, firstDraw.FromZone, firstDraw.ToZone),
                Is.EqualTo((firstDrawnCardId, BattleCardZone.DrawPile, BattleCardZone.Hand)));
            Assert.That(secondDraw, Is.Not.Null);
            Assert.That(
                (secondDraw.CardId, secondDraw.FromZone, secondDraw.ToZone),
                Is.EqualTo((secondDrawnCardId, BattleCardZone.DrawPile, BattleCardZone.Hand)));
            Assert.That(sourceMove, Is.Not.Null);
            Assert.That(
                (sourceMove.CardId, sourceMove.FromZone, sourceMove.ToZone),
                Is.EqualTo((sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));

            Assert.That(layoutPublicationCount, Is.EqualTo(1));
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(2));
            Assert.That(scenario.Zones.DrawPile, Is.Empty);
            Assert.That(scenario.Zones.Hand,
                Is.EqualTo(new[] { firstDrawnCardId, secondDrawnCardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
            Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
            CardInstanceId[] cardsInZones = scenario.Zones.DrawPile
                .Concat(scenario.Zones.Hand)
                .Concat(scenario.Zones.DiscardPile)
                .Concat(scenario.Zones.ExhaustPile)
                .Concat(scenario.Zones.PowerPile)
                .ToArray();
            Assert.That(cardsInZones,
                Is.EquivalentTo(new[]
                {
                    sourceCardId,
                    selectedCardId,
                    firstDrawnCardId,
                    secondDrawnCardId,
                }));
            Assert.That(cardsInZones.Distinct().Count(), Is.EqualTo(cardsInZones.Length));
        }
    }

    /// <summary>验证 Burning Pact 无可选牌时仍完整结算，并在满手牌时按来源牌仍占位的投影只抽一张。</summary>
    [Test]
    public void Submit_BurningPact_WithoutCandidateAndAtHandLimit_UsesProjectedAtomicLayout()
    {
        const cfg.battle.EffectType exhaustSelectedHandCard = cfg.battle.EffectType.ExhaustSelectedHandCard;
        JObject burningPact = CreateIroncladCard(
            3125,
            "BURNING_PACT",
            cost: 1,
            cfg.battle.TargetRule.Self,
            4411,
            4412);
        JObject exhaust = CreateEffect(
            4411,
            exhaustSelectedHandCard,
            cfg.battle.Attribute.None,
            value: 1);
        JObject draw = CreateEffect(
            4412,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 2);

        using (var onlySource = new QueueScenario(
                   new[] { burningPact },
                   new[] { exhaust, draw },
                   Enumerable.Repeat(3125, 3),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId sourceCardId = onlySource.Zones.Hand[0];
            CardInstanceId firstDrawnCardId =
                onlySource.Zones.DrawPile[onlySource.Zones.DrawPile.Count - 1];
            CardInstanceId secondDrawnCardId =
                onlySource.Zones.DrawPile[onlySource.Zones.DrawPile.Count - 2];
            uint shuffleBefore = onlySource.Zones.ShuffleRandomState;
            int layoutPublicationCount = 0;
            using BattleCommandLifecycleExecutionRecorder recorder =
                onlySource.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission;
            using (onlySource.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            {
                submission = onlySource.Queue.Submit(new PlayCardCommand(
                    onlySource.Player.Id,
                    sourceCardId,
                    onlySource.Player.Id));
            }

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(4));
            Assert.That(terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 4)));

            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            var firstDraw = terminal.Settlements[1] as BattleCardMovedSettlement;
            var secondDraw = terminal.Settlements[2] as BattleCardMovedSettlement;
            var sourceMove = terminal.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 2, 1)));
            Assert.That(firstDraw, Is.Not.Null);
            Assert.That(
                (firstDraw.CardId, firstDraw.FromZone, firstDraw.ToZone),
                Is.EqualTo((firstDrawnCardId, BattleCardZone.DrawPile, BattleCardZone.Hand)));
            Assert.That(secondDraw, Is.Not.Null);
            Assert.That(
                (secondDraw.CardId, secondDraw.FromZone, secondDraw.ToZone),
                Is.EqualTo((secondDrawnCardId, BattleCardZone.DrawPile, BattleCardZone.Hand)));
            Assert.That(sourceMove, Is.Not.Null);
            Assert.That(
                (sourceMove.CardId, sourceMove.FromZone, sourceMove.ToZone),
                Is.EqualTo((sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));

            Assert.That(layoutPublicationCount, Is.EqualTo(1));
            Assert.That(onlySource.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(onlySource.Queue.Turn.CurrentValue.Players[onlySource.Player.Id].Energy,
                Is.EqualTo(2));
            Assert.That(onlySource.Zones.DrawPile, Is.Empty);
            Assert.That(onlySource.Zones.Hand,
                Is.EqualTo(new[] { firstDrawnCardId, secondDrawnCardId }));
            Assert.That(onlySource.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
            Assert.That(onlySource.Zones.ExhaustPile, Is.Empty);
        }

        using (var fullHand = new QueueScenario(
                   new[] { burningPact },
                   new[] { exhaust, draw },
                   Enumerable.Repeat(3125, 11),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: BattleCardZonesData.BattleCardHandLimit))
        {
            CardInstanceId[] initialHand = fullHand.Zones.Hand.ToArray();
            CardInstanceId sourceCardId = initialHand[0];
            CardInstanceId selectedCardId = initialHand[1];
            CardInstanceId drawnCardId = fullHand.Zones.DrawPile[0];
            CardInstanceId[] expectedHand = initialHand
                .Skip(2)
                .Concat(new[] { drawnCardId })
                .ToArray();
            uint shuffleBefore = fullHand.Zones.ShuffleRandomState;
            int layoutPublicationCount = 0;
            using BattleCommandLifecycleExecutionRecorder recorder =
                fullHand.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission;
            using (fullHand.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            {
                submission = fullHand.Queue.Submit(new PlayCardCommand(
                    fullHand.Player.Id,
                    sourceCardId,
                    fullHand.Player.Id,
                    new[] { selectedCardId }));
            }

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(terminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            Assert.That(terminal.Settlements, Has.Count.EqualTo(4));
            Assert.That(terminal.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, 4)));

            var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
            var selectedMove = terminal.Settlements[1] as BattleCardMovedSettlement;
            var drawMove = terminal.Settlements[2] as BattleCardMovedSettlement;
            var sourceMove = terminal.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
                Is.EqualTo((3, 2, 1)));
            Assert.That(selectedMove, Is.Not.Null);
            Assert.That(
                (selectedMove.CardId, selectedMove.FromZone, selectedMove.ToZone),
                Is.EqualTo((selectedCardId, BattleCardZone.Hand, BattleCardZone.ExhaustPile)));
            Assert.That(drawMove, Is.Not.Null);
            Assert.That(
                (drawMove.CardId, drawMove.FromZone, drawMove.ToZone),
                Is.EqualTo((drawnCardId, BattleCardZone.DrawPile, BattleCardZone.Hand)));
            Assert.That(sourceMove, Is.Not.Null);
            Assert.That(
                (sourceMove.CardId, sourceMove.FromZone, sourceMove.ToZone),
                Is.EqualTo((sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));

            Assert.That(terminal.Settlements.OfType<BattleCardMovedSettlement>()
                    .Count(item => item.FromZone == BattleCardZone.DrawPile &&
                                   item.ToZone == BattleCardZone.Hand),
                Is.EqualTo(1));
            Assert.That(layoutPublicationCount, Is.EqualTo(1));
            Assert.That(fullHand.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(fullHand.Queue.Turn.CurrentValue.Players[fullHand.Player.Id].Energy,
                Is.EqualTo(2));
            Assert.That(fullHand.Zones.DrawPile, Is.Empty);
            Assert.That(fullHand.Zones.Hand, Is.EqualTo(expectedHand));
            Assert.That(fullHand.Zones.Hand, Has.Count.EqualTo(9));
            Assert.That(fullHand.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
            Assert.That(fullHand.Zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
        }
    }

    /// <summary>验证 ProgramNone 的 Burning Pact 对空选、自选来源、多选和过期选择返回稳定失败码，且公共队列保持全部权威事实零写。</summary>
    [Test]
    public void Submit_BurningPact_InvalidGenericSelectionsFailWithoutWrites()
    {
        const cfg.battle.EffectType exhaustSelectedHandCard = cfg.battle.EffectType.ExhaustSelectedHandCard;
        JObject burningPact = CreateIroncladCard(
            3125,
            "BURNING_PACT",
            cost: 1,
            cfg.battle.TargetRule.Self,
            4411,
            4412);
        JObject exhaust = CreateEffect(
            4411,
            exhaustSelectedHandCard,
            cfg.battle.Attribute.None,
            value: 1);
        JObject draw = CreateEffect(
            4412,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 2);

        // 通过唯一公共队列提交预期失败命令，并锁住失败前后的全部权威快照与发布次数。
        void AssertFailureWithoutWrites(
            QueueScenario scenario,
            PlayCardCommand command,
            BattleCommandExecutionFailureReason expectedFailure)
        {
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            int energyBefore = turnBefore.Players[scenario.Player.Id].Energy;
            CardInstanceId[] cardIdsBefore = scenario.Zones.Cards.Keys.ToArray();
            CardInstanceId[] drawPileBefore = scenario.Zones.DrawPile.ToArray();
            CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
            CardInstanceId[] discardPileBefore = scenario.Zones.DiscardPile.ToArray();
            CardInstanceId[] exhaustPileBefore = scenario.Zones.ExhaustPile.ToArray();
            CardInstanceId[] powerPileBefore = scenario.Zones.PowerPile.ToArray();
            uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
            BattleCommandExecutionResult[] resultsBefore =
                scenario.Presentation.Results.ToArray();
            int turnPublicationCount = 0;
            int layoutPublicationCount = 0;
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission;
            using (scenario.Queue.Turn.Skip(1).Subscribe(_ => turnPublicationCount++))
            using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
                submission = scenario.Queue.Submit(command);
            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);

            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(terminal.FailureReason, Is.EqualTo(expectedFailure));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(energyBefore));
            Assert.That(scenario.Zones.Cards.Keys, Is.EquivalentTo(cardIdsBefore));
            Assert.That(scenario.Zones.DrawPile, Is.EqualTo(drawPileBefore));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardPileBefore));
            Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(exhaustPileBefore));
            Assert.That(scenario.Zones.PowerPile, Is.EqualTo(powerPileBefore));
            Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
            Assert.That(scenario.Presentation.Results, Is.EqualTo(resultsBefore));
            Assert.That(turnPublicationCount, Is.Zero);
            Assert.That(layoutPublicationCount, Is.Zero);
        }

        using (var emptySelection = new QueueScenario(
                   new[] { burningPact },
                   new[] { exhaust, draw },
                   Enumerable.Repeat(3125, 2),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 2))
        {
            CardInstanceId sourceCardId = emptySelection.Zones.Hand[0];
            AssertFailureWithoutWrites(
                emptySelection,
                new PlayCardCommand(
                    emptySelection.Player.Id,
                    sourceCardId,
                    emptySelection.Player.Id,
                    Array.Empty<CardInstanceId>()),
                BattleCommandExecutionFailureReason.CardSelectionRequired);
        }

        using (var sourceSelection = new QueueScenario(
                   new[] { burningPact },
                   new[] { exhaust, draw },
                   Enumerable.Repeat(3125, 2),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 2))
        {
            CardInstanceId sourceCardId = sourceSelection.Zones.Hand[0];
            AssertFailureWithoutWrites(
                sourceSelection,
                new PlayCardCommand(
                    sourceSelection.Player.Id,
                    sourceCardId,
                    sourceSelection.Player.Id,
                    new[] { sourceCardId }),
                BattleCommandExecutionFailureReason.SelectedCardNotEligible);
        }

        using (var multipleSelection = new QueueScenario(
                   new[] { burningPact },
                   new[] { exhaust, draw },
                   Enumerable.Repeat(3125, 3),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 3))
        {
            CardInstanceId sourceCardId = multipleSelection.Zones.Hand[0];
            CardInstanceId[] selectedCardIds = multipleSelection.Zones.Hand.Skip(1).ToArray();
            AssertFailureWithoutWrites(
                multipleSelection,
                new PlayCardCommand(
                    multipleSelection.Player.Id,
                    sourceCardId,
                    multipleSelection.Player.Id,
                    selectedCardIds),
                BattleCommandExecutionFailureReason.InvalidCardSelectionCount);
        }

        using (var staleSelection = new QueueScenario(
                   new[] { burningPact },
                   new[] { exhaust, draw },
                   Enumerable.Repeat(3125, 2),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 2))
        {
            CardInstanceId sourceCardId = staleSelection.Zones.Hand[0];
            CardInstanceId selectedCardId = staleSelection.Zones.Hand[1];
            var command = new PlayCardCommand(
                staleSelection.Player.Id,
                sourceCardId,
                staleSelection.Player.Id,
                new[] { selectedCardId });
            Assert.That(staleSelection.Zones.DiscardFromHand(selectedCardId).Succeeded, Is.True);

            AssertFailureWithoutWrites(
                staleSelection,
                command,
                BattleCommandExecutionFailureReason.SelectedCardNotInHand);
        }
    }

    /// <summary>验证选牌后抽牌语法在规则评估与队列执行中拒绝同一批非法组合，并在首次权威写入前保持全部事实不变。</summary>
    [Test]
    public void Submit_BurningPact_InvalidSelectionDrawGrammarFailsConsistentlyWithoutWrites()
    {
        const cfg.battle.EffectType exhaustSelectedHandCard = cfg.battle.EffectType.ExhaustSelectedHandCard;
        var grammarCases = new[]
        {
            new
            {
                Name = "GainBlockBeforeSelection",
                EffectIds = new[] { 4510, 4511, 4512 },
                Effects = new[]
                {
                    CreateEffect(4510, cfg.battle.EffectType.GainBlock, cfg.battle.Attribute.None, 5),
                    CreateEffect(4511, exhaustSelectedHandCard, cfg.battle.Attribute.None, 1),
                    CreateEffect(4512, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 2),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.InvalidEffectBinding,
            },
            new
            {
                Name = "DrawBeforeSelection",
                EffectIds = new[] { 4520, 4521, 4522 },
                Effects = new[]
                {
                    CreateEffect(4520, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 1),
                    CreateEffect(4521, exhaustSelectedHandCard, cfg.battle.Attribute.None, 1),
                    CreateEffect(4522, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 2),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.InvalidEffectBinding,
            },
            new
            {
                Name = "MissingDraw",
                EffectIds = new[] { 4530 },
                Effects = new[]
                {
                    CreateEffect(4530, exhaustSelectedHandCard, cfg.battle.Attribute.None, 1),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.InvalidEffectBinding,
            },
            new
            {
                Name = "DuplicateSelection",
                EffectIds = new[] { 4540, 4541, 4542 },
                Effects = new[]
                {
                    CreateEffect(4540, exhaustSelectedHandCard, cfg.battle.Attribute.None, 1),
                    CreateEffect(4541, exhaustSelectedHandCard, cfg.battle.Attribute.None, 1),
                    CreateEffect(4542, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 2),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.InvalidEffectBinding,
            },
            new
            {
                Name = "DuplicateDraw",
                EffectIds = new[] { 4550, 4551, 4552 },
                Effects = new[]
                {
                    CreateEffect(4550, exhaustSelectedHandCard, cfg.battle.Attribute.None, 1),
                    CreateEffect(4551, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 2),
                    CreateEffect(4552, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 1),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.InvalidEffectBinding,
            },
            new
            {
                Name = "CombatAfterDraw",
                EffectIds = new[] { 4560, 4561, 4562 },
                Effects = new[]
                {
                    CreateEffect(4560, exhaustSelectedHandCard, cfg.battle.Attribute.None, 1),
                    CreateEffect(4561, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 2),
                    CreateEffect(4562, cfg.battle.EffectType.GainBlock, cfg.battle.Attribute.None, 5),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.InvalidEffectBinding,
            },
            new
            {
                Name = "SelectionAttributeMismatch",
                EffectIds = new[] { 4570, 4571 },
                Effects = new[]
                {
                    CreateEffect(4570, exhaustSelectedHandCard, cfg.battle.Attribute.Strength, 1),
                    CreateEffect(4571, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 2),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.UnsupportedEffectAttribute,
            },
            new
            {
                Name = "SelectionValueNotOne",
                EffectIds = new[] { 4580, 4581 },
                Effects = new[]
                {
                    CreateEffect(4580, exhaustSelectedHandCard, cfg.battle.Attribute.None, 2),
                    CreateEffect(4581, cfg.battle.EffectType.DrawCards, cfg.battle.Attribute.None, 2),
                },
                ExpectedFailure = BattleCommandExecutionFailureReason.InvalidEffectBinding,
            },
        };

        foreach (var grammarCase in grammarCases)
        {
            JObject burningPact = CreateIroncladCard(
                3125,
                "BURNING_PACT",
                cost: 1,
                cfg.battle.TargetRule.Self,
                grammarCase.EffectIds);
            using (var scenario = new QueueScenario(
                       new[] { burningPact },
                       grammarCase.Effects,
                       Enumerable.Repeat(3125, 2),
                       playerStrength: 0,
                       energyPerRound: 3,
                       drawDeckIntoHand: false,
                       initialHandCount: 2))
            {
                CardInstanceId sourceCardId = scenario.Zones.Hand[0];
                CardInstanceId selectedCardId = scenario.Zones.Hand[1];
                var command = new PlayCardCommand(
                    scenario.Player.Id,
                    sourceCardId,
                    scenario.Player.Id,
                    new[] { selectedCardId });
                BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
                CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
                int energyBefore = turnBefore.Players[scenario.Player.Id].Energy;
                int healthBefore = scenario.Player.CurrentHealth;
                int blockBefore = scenario.Player.CurrentBlock;
                int strengthBefore = scenario.Player.CurrentStrength;
                int vulnerableBefore = scenario.Player.CurrentVulnerable;
                CardInstanceId[] cardIdsBefore = scenario.Zones.Cards.Keys.ToArray();
                CardInstanceId[] drawPileBefore = scenario.Zones.DrawPile.ToArray();
                CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
                CardInstanceId[] discardPileBefore = scenario.Zones.DiscardPile.ToArray();
                CardInstanceId[] exhaustPileBefore = scenario.Zones.ExhaustPile.ToArray();
                CardInstanceId[] powerPileBefore = scenario.Zones.PowerPile.ToArray();
                uint randomBefore = scenario.Zones.ShuffleRandomState;
                BattleCommandExecutionResult[] resultsBefore =
                    scenario.Presentation.Results.ToArray();

                BattleCardPlayEvaluation evaluation = scenario.Queue.CardPlayRules.Evaluate(
                    turnBefore,
                    command);
                Assert.That(
                    evaluation.FailureReason,
                    Is.EqualTo(grammarCase.ExpectedFailure),
                    grammarCase.Name);

                int turnPublicationCount = 0;
                int layoutPublicationCount = 0;
                using BattleCommandLifecycleExecutionRecorder recorder =
                    scenario.Queue.RecordExecutionLifecycle();
                BattleCommandSubmissionResult submission;
                using (scenario.Queue.Turn.Skip(1).Subscribe(_ => turnPublicationCount++))
                using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
                    submission = scenario.Queue.Submit(command);
                BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);

                Assert.That(submission.Accepted, Is.True, grammarCase.Name);
                Assert.That(
                    terminal.Stage,
                    Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed),
                    grammarCase.Name);
                Assert.That(
                    terminal.FailureReason,
                    Is.EqualTo(grammarCase.ExpectedFailure),
                    grammarCase.Name);
                Assert.That(terminal.Settlements, Is.Empty, grammarCase.Name);
                Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore), grammarCase.Name);
                Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore), grammarCase.Name);
                Assert.That(
                    scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                    Is.EqualTo(energyBefore),
                    grammarCase.Name);
                Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(healthBefore), grammarCase.Name);
                Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(blockBefore), grammarCase.Name);
                Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(strengthBefore), grammarCase.Name);
                Assert.That(scenario.Player.CurrentVulnerable, Is.EqualTo(vulnerableBefore), grammarCase.Name);
                Assert.That(scenario.Zones.Cards.Keys, Is.EquivalentTo(cardIdsBefore), grammarCase.Name);
                Assert.That(scenario.Zones.DrawPile, Is.EqualTo(drawPileBefore), grammarCase.Name);
                Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore), grammarCase.Name);
                Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardPileBefore), grammarCase.Name);
                Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(exhaustPileBefore), grammarCase.Name);
                Assert.That(scenario.Zones.PowerPile, Is.EqualTo(powerPileBefore), grammarCase.Name);
                Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(randomBefore), grammarCase.Name);
                Assert.That(scenario.Presentation.Results, Is.EqualTo(resultsBefore), grammarCase.Name);
                Assert.That(turnPublicationCount, Is.Zero, grammarCase.Name);
                Assert.That(layoutPublicationCount, Is.Zero, grammarCase.Name);
            }
        }
    }

    /// <summary>验证 Twin Strike 首击致死后把第二击记录为跳过，仍完成弃牌和战斗阶段结算。</summary>
    [Test]
    public void Submit_TwinStrike_FirstHitLethalSkipsSecondHitAndStillDiscards()
    {
        JObject twinStrike = CreateIroncladCard(
            3120,
            "TWIN_STRIKE",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4402,
            4402);
        JObject damage = CreateEffect(
            4402,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 5);
        using (var scenario = new QueueScenario(
                   new[] { twinStrike },
                   new[] { damage },
                   new[] { 3120 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   enemyHealth: 5))
        {
            CardInstanceId cardId = scenario.FindCard(3120);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(5));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var skipped = result.Settlements[2] as BattleOperationSkippedSettlement;
            var moved = result.Settlements[3] as BattleCardMovedSettlement;
            var phaseChanged = result.Settlements[4] as BattlePhaseChangedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.Order, energy.Amount), Is.EqualTo((0, 1)));
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.Order, Is.EqualTo(1));
            Assert.That(damageRecord.AttackValue, Is.EqualTo(5));
            Assert.That(damageRecord.WasFatal, Is.True);
            Assert.That(damageRecord.HealthAfter, Is.Zero);
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.Order, Is.EqualTo(2));
            Assert.That(skipped.EffectId.Value.Value, Is.EqualTo(4402));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(3));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(phaseChanged, Is.Not.Null);
            Assert.That(phaseChanged.Order, Is.EqualTo(4));
            Assert.That(result.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(1));
            Assert.That(scenario.Enemy.CurrentHealth, Is.Zero);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证普通抽牌会重洗旧弃牌堆，并在命令开始时已有十张手牌时按上限截断且不推进洗牌随机流。</summary>
    [Test]
    public void Submit_DrawCards_ReshufflesOldDiscardAndRespectsTenCardHandLimit()
    {
        JObject pommelStrike = CreateIroncladCard(
            3113,
            "POMMEL_STRIKE",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4403,
            4404);
        JObject damage = CreateEffect(
            4403,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 9);
        JObject draw = CreateEffect(
            4404,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        using (var reshuffle = new QueueScenario(
                   new[] { pommelStrike },
                   new[] { damage, draw },
                   new[] { 3113, 3113 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = reshuffle.Zones.Hand[0];
            CardInstanceId oldDiscardId = reshuffle.Zones.Hand[1];
            reshuffle.Zones.DiscardFromHand(oldDiscardId);

            reshuffle.Queue.Submit(new PlayCardCommand(
                reshuffle.Player.Id,
                cardId,
                reshuffle.Enemy.Id));

            BattleCommandExecutionResult result = reshuffle.Presentation.Results[1];
            Assert.That(result.Settlements, Has.Count.EqualTo(6));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var reshuffleMove = result.Settlements[2] as BattleCardMovedSettlement;
            var reshuffled = result.Settlements[3] as BattleCardsReshuffledSettlement;
            var drawn = result.Settlements[4] as BattleCardMovedSettlement;
            var departure = result.Settlements[5] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.Order, energy.Amount), Is.EqualTo((0, 1)));
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That((damageRecord.Order, damageRecord.AttackValue), Is.EqualTo((1, 9)));
            Assert.That(reshuffleMove, Is.Not.Null);
            Assert.That(reshuffleMove.Order, Is.EqualTo(2));
            Assert.That(reshuffleMove.CardId, Is.EqualTo(oldDiscardId));
            Assert.That(reshuffleMove.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(reshuffleMove.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(reshuffled, Is.Not.Null);
            Assert.That(reshuffled.Order, Is.EqualTo(3));
            Assert.That(drawn, Is.Not.Null);
            Assert.That(drawn.Order, Is.EqualTo(4));
            Assert.That(drawn.CardId, Is.EqualTo(oldDiscardId));
            Assert.That(drawn.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(drawn.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure, Is.Not.Null);
            Assert.That(departure.Order, Is.EqualTo(5));
            Assert.That(departure.CardId, Is.EqualTo(cardId));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(reshuffle.Zones.Hand, Is.EqualTo(new[] { oldDiscardId }));
            Assert.That(reshuffle.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }

        using (var fullHand = new QueueScenario(
                   new[] { pommelStrike },
                   new[] { damage, draw },
                   Enumerable.Repeat(3113, 11),
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 10))
        {
            CardInstanceId cardId = fullHand.Zones.Hand[0];
            uint shuffleBefore = fullHand.Zones.ShuffleRandomState;
            CardInstanceId drawPileCardId = fullHand.Zones.DrawPile[0];

            fullHand.Queue.Submit(new PlayCardCommand(
                fullHand.Player.Id,
                cardId,
                fullHand.Enemy.Id));

            BattleCommandExecutionResult result = fullHand.Presentation.Results[1];
            Assert.That(result.Settlements, Has.Count.EqualTo(3));
            Assert.That(result.Settlements[0], Is.TypeOf<BattleEnergySpentSettlement>());
            Assert.That(result.Settlements[1], Is.TypeOf<BattleDamageAppliedSettlement>());
            var departure = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(departure, Is.Not.Null);
            Assert.That(departure.Order, Is.EqualTo(2));
            Assert.That(departure.CardId, Is.EqualTo(cardId));
            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
                item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand),
                Is.False);
            Assert.That(fullHand.Zones.Hand, Has.Count.EqualTo(9));
            Assert.That(fullHand.Zones.DrawPile, Is.EqualTo(new[] { drawPileCardId }));
            Assert.That(fullHand.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
            Assert.That(fullHand.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
        }
    }

    /// <summary>验证能量不足或缺少显式目标时，四张首批卡共用的队列准备链在任何权威写入前失败。</summary>
    [Test]
    public void Submit_FirstIroncladSlice_InvalidEnergyOrTargetFailsWithoutWrites()
    {
        JObject bludgeon = CreateIroncladCard(
            3123,
            "BLUDGEON",
            cost: 3,
            cfg.battle.TargetRule.Enemy,
            4401);
        JObject bludgeonDamage = CreateEffect(
            4401,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 32);
        using (var insufficientEnergy = new QueueScenario(
                   new[] { bludgeon },
                   new[] { bludgeonDamage },
                   new[] { 3123 },
                   playerStrength: 0,
                   energyPerRound: 2))
        {
            CardInstanceId cardId = insufficientEnergy.FindCard(3123);
            BattleTurnData turnBefore = insufficientEnergy.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = insufficientEnergy.Zones.Layout.CurrentValue;
            uint shuffleBefore = insufficientEnergy.Zones.ShuffleRandomState;
            int resultCountBefore = insufficientEnergy.Presentation.Results.Count;
            using BattleCommandLifecycleExecutionRecorder recorder =
                insufficientEnergy.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(
                insufficientEnergy.Queue.Submit(new PlayCardCommand(
                    insufficientEnergy.Player.Id,
                    cardId,
                    insufficientEnergy.Enemy.Id)));

            Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(insufficientEnergy.Presentation.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(insufficientEnergy.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(insufficientEnergy.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(insufficientEnergy.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(insufficientEnergy.Enemy.CurrentHealth, Is.EqualTo(40));
            Assert.That(insufficientEnergy.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(insufficientEnergy.Zones.DiscardPile, Is.Empty);
        }

        JObject pommelStrike = CreateIroncladCard(
            3113,
            "POMMEL_STRIKE",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4403,
            4404);
        JObject damage = CreateEffect(
            4403,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 9);
        JObject draw = CreateEffect(
            4404,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        using (var missingTarget = new QueueScenario(
                   new[] { pommelStrike },
                   new[] { damage, draw },
                   new[] { 3113, 3113 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId cardId = missingTarget.FindCard(3113);
            BattleTurnData turnBefore = missingTarget.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = missingTarget.Zones.Layout.CurrentValue;
            uint shuffleBefore = missingTarget.Zones.ShuffleRandomState;
            CardInstanceId drawPileCardId = missingTarget.Zones.DrawPile[0];
            int resultCountBefore = missingTarget.Presentation.Results.Count;
            using BattleCommandLifecycleExecutionRecorder recorder =
                missingTarget.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(
                missingTarget.Queue.Submit(new PlayCardCommand(
                    missingTarget.Player.Id,
                    cardId,
                    targetId: null)));

            Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRequired));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(missingTarget.Presentation.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(missingTarget.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(missingTarget.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(missingTarget.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(missingTarget.Enemy.CurrentHealth, Is.EqualTo(40));
            Assert.That(missingTarget.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(missingTarget.Zones.DrawPile, Is.EqualTo(new[] { drawPileCardId }));
            Assert.That(missingTarget.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>验证一张牌声明两个 Draw binding 时，在能量、战斗事实、卡区布局和随机流写入前稳定失败。</summary>
    [Test]
    public void Submit_DrawSequence_SecondDrawBindingFailsBeforeAnyWrite()
    {
        JObject invalidDrawCard = CreateCard(
            3980,
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4450,
            4451);
        JObject firstDraw = CreateEffect(
            4450,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        JObject secondDraw = CreateEffect(
            4451,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        using (var scenario = new QueueScenario(
                   new[] { invalidDrawCard },
                   new[] { firstDraw, secondDraw },
                   new[] { 3980, 3980 },
                   playerStrength: 2,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId cardId = scenario.FindCard(3980);
            CardInstanceId drawPileCardId = scenario.Zones.DrawPile[0];
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            uint shuffleBefore = scenario.Zones.ShuffleRandomState;
            int resultCountBefore = scenario.Presentation.Results.Count;
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(
                scenario.Queue.Submit(new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Enemy.Id)));

            Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InvalidEffectBinding));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(2));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(40));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Zones.DrawPile, Is.EqualTo(new[] { drawPileCardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>验证 Draw 两侧的战斗 Effect 使用同一完整投影，并且 Draw 不会因前置伤害致死而被取消。</summary>
    [Test]
    public void Submit_DrawSequence_LethalStillDrawsAndProjectedStrengthSurvivesDraw()
    {
        JObject pommelStrike = CreateIroncladCard(
            3113,
            "POMMEL_STRIKE",
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4460,
            4461);
        JObject lethalDamage = CreateEffect(
            4460,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 9);
        JObject drawOne = CreateEffect(
            4461,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        using (var lethal = new QueueScenario(
                   new[] { pommelStrike },
                   new[] { lethalDamage, drawOne },
                   new[] { 3113, 3113 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   enemyHealth: 9,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId sourceId = lethal.FindCard(3113);
            CardInstanceId drawnCardId = lethal.Zones.DrawPile[0];

            lethal.Queue.Submit(new PlayCardCommand(
                lethal.Player.Id,
                sourceId,
                lethal.Enemy.Id));

            BattleCommandExecutionResult result = lethal.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(5));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damage = result.Settlements[1] as BattleDamageAppliedSettlement;
            var draw = result.Settlements[2] as BattleCardMovedSettlement;
            var departure = result.Settlements[3] as BattleCardMovedSettlement;
            var phaseChanged = result.Settlements[4] as BattlePhaseChangedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.Order, energy.Amount), Is.EqualTo((0, 1)));
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.Order, Is.EqualTo(1));
            Assert.That(damage.WasFatal, Is.True);
            Assert.That(damage.HealthAfter, Is.Zero);
            Assert.That(draw, Is.Not.Null);
            Assert.That(draw.Order, Is.EqualTo(2));
            Assert.That(draw.CardId, Is.EqualTo(drawnCardId));
            Assert.That(draw.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(draw.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure, Is.Not.Null);
            Assert.That(departure.Order, Is.EqualTo(3));
            Assert.That(departure.CardId, Is.EqualTo(sourceId));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(phaseChanged, Is.Not.Null);
            Assert.That(phaseChanged.Order, Is.EqualTo(4));
            Assert.That(lethal.Zones.Hand, Is.EqualTo(new[] { drawnCardId }));
            Assert.That(lethal.Zones.DiscardPile, Is.EqualTo(new[] { sourceId }));
        }

        JObject projectedCard = CreateCard(
            3981,
            cost: 0,
            cfg.battle.TargetRule.Self,
            4462,
            4463,
            4464);
        JObject strength = CreateEffect(
            4462,
            cfg.battle.EffectType.ModifyAttribute,
            cfg.battle.Attribute.Strength,
            value: 3);
        JObject projectedDraw = CreateEffect(
            4463,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 1);
        JObject projectedDamage = CreateEffect(
            4464,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 5);
        using (var projected = new QueueScenario(
                   new[] { projectedCard },
                   new[] { strength, projectedDraw, projectedDamage },
                   new[] { 3981, 3981 },
                   playerStrength: 1,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId sourceId = projected.FindCard(3981);
            CardInstanceId drawnCardId = projected.Zones.DrawPile[0];

            projected.Queue.Submit(new PlayCardCommand(
                projected.Player.Id,
                sourceId,
                projected.Player.Id));

            BattleCommandExecutionResult result = projected.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(5));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var strengthRecord = result.Settlements[1] as BattleAttributeModifiedSettlement;
            var draw = result.Settlements[2] as BattleCardMovedSettlement;
            var damage = result.Settlements[3] as BattleDamageAppliedSettlement;
            var departure = result.Settlements[4] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.Order, energy.Amount), Is.EqualTo((0, 0)));
            Assert.That(strengthRecord, Is.Not.Null);
            Assert.That(strengthRecord.Order, Is.EqualTo(1));
            Assert.That((strengthRecord.ValueBefore, strengthRecord.ValueAfter), Is.EqualTo((1, 4)));
            Assert.That(draw, Is.Not.Null);
            Assert.That(draw.Order, Is.EqualTo(2));
            Assert.That(draw.CardId, Is.EqualTo(drawnCardId));
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.Order, Is.EqualTo(3));
            Assert.That(damage.AttackValue, Is.EqualTo(9));
            Assert.That((damage.HealthBefore, damage.HealthAfter), Is.EqualTo((30, 21)));
            Assert.That(departure, Is.Not.Null);
            Assert.That(departure.Order, Is.EqualTo(4));
            Assert.That(departure.CardId, Is.EqualTo(sourceId));
            Assert.That(projected.Player.CurrentStrength, Is.EqualTo(4));
            Assert.That(projected.Player.CurrentHealth, Is.EqualTo(21));
            Assert.That(projected.Zones.Hand, Is.EqualTo(new[] { drawnCardId }));
            Assert.That(projected.Zones.DiscardPile, Is.EqualTo(new[] { sourceId }));
        }
    }

    /// <summary>验证 Draw0 的冻结计划保持布局引用与随机状态，而公共队列仍成功提交当前牌归宿。</summary>
    [Test]
    public void Submit_DrawZero_PreservesDrawPlanStateAndStillDiscardsSource()
    {
        using (var directZones = new BattleCardZonesData(new[] { 3982 }, shuffleSeed: 9753))
        {
            CardZoneLayoutData layoutBefore = directZones.Layout.CurrentValue;
            uint shuffleBefore = directZones.ShuffleRandomState;
            BattlePreparedDraw plan = directZones.PrepareDraw(
                count: 0,
                startingOrder: 7,
                handLimit: BattleCardZonesData.BattleCardHandLimit);

            Assert.That(plan.InitialLayout, Is.SameAs(layoutBefore));
            Assert.That(plan.NextLayout, Is.SameAs(layoutBefore));
            Assert.That(plan.ShuffleRandomStateBefore, Is.EqualTo(shuffleBefore));
            Assert.That(plan.ShuffleRandomStateAfter, Is.EqualTo(shuffleBefore));
            Assert.That(plan.Settlements, Is.Empty);
            BattleCardZoneOperationResult result = directZones.CommitPreparedDraw(plan);
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(directZones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(directZones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
        }

        JObject drawZeroCard = CreateCard(
            3982,
            cost: 0,
            cfg.battle.TargetRule.Self,
            4470);
        JObject drawZero = CreateEffect(
            4470,
            cfg.battle.EffectType.DrawCards,
            cfg.battle.Attribute.None,
            value: 0);
        using (var scenario = new QueueScenario(
                   new[] { drawZeroCard },
                   new[] { drawZero },
                   new[] { 3982, 3982 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1))
        {
            CardInstanceId sourceId = scenario.FindCard(3982);
            CardInstanceId drawPileCardId = scenario.Zones.DrawPile[0];
            uint shuffleBefore = scenario.Zones.ShuffleRandomState;

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                sourceId,
                scenario.Player.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(2));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var departure = result.Settlements[1] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That((energy.Order, energy.Amount), Is.EqualTo((0, 0)));
            Assert.That(departure, Is.Not.Null);
            Assert.That(departure.Order, Is.EqualTo(1));
            Assert.That(departure.CardId, Is.EqualTo(sourceId));
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DrawPile, Is.EqualTo(new[] { drawPileCardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceId }));
            Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
        }
    }

    /// <summary>验证 Bash 首个 Effect 致死后跳过易伤，但命令仍成功并完成卡牌归堆。</summary>
    [Test]
    public void Submit_FatalBash_SkipsVulnerableAndStillDiscards()
    {
        JObject bash = CreateCard(
            3004,
            cost: 2,
            cfg.battle.TargetRule.Enemy,
            4301,
            4302);
        JObject damage = CreateEffect(
            4301,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 8);
        JObject vulnerable = CreateEffect(
            4302,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 2);
        using (var scenario = new QueueScenario(
                   new[] { bash },
                   new[] { damage, vulnerable },
                   new[] { 3004 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   enemyHealth: 8))
        {
            CardInstanceId cardId = scenario.FindCard(3004);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(5));
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var skipped = result.Settlements[2] as BattleOperationSkippedSettlement;
            var moved = result.Settlements[3] as BattleCardMovedSettlement;
            var phaseChanged = result.Settlements[4] as BattlePhaseChangedSettlement;
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.WasFatal, Is.True);
            Assert.That(damageRecord.HealthAfter, Is.Zero);
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.Order, Is.EqualTo(2));
            Assert.That(skipped.EffectId.Value.Value, Is.EqualTo(4302));
            Assert.That(skipped.Reason, Is.EqualTo(BattleOperationSkipReason.TargetNotAlive));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(3));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(phaseChanged, Is.Not.Null);
            Assert.That(phaseChanged.Order, Is.EqualTo(4));
            Assert.That(phaseChanged.PhaseAfter, Is.EqualTo(BattleTurnPhase.BattleEnded));
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.Zero);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Havoc 经公共队列免费打出抽牌堆顶牌，并在父牌表现屏障后把子牌送入消耗堆。</summary>
    [Test]
    public void Submit_Havoc_QueuesTopDrawCardAsFreeExhaustingContinuationInOrder()
    {
        const int havocTemplateId = 3108;
        const int childTemplateId = 3997;
        const int havocEffectId = 4495;
        const int childBlockEffectId = 4496;
        JObject havoc = CreateIroncladCard(
            havocTemplateId,
            "HAVOC",
            cost: 1,
            cfg.battle.TargetRule.Self,
            havocEffectId);
        JObject child = CreateCard(
            childTemplateId,
            cost: 3,
            cfg.battle.TargetRule.Self,
            childBlockEffectId);
        JObject triggerTopDrawCard = CreateEffect(
            havocEffectId,
            (cfg.battle.EffectType)9,
            cfg.battle.Attribute.None,
            value: 0);
        JObject gainBlock = CreateEffect(
            childBlockEffectId,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 7);
        using (var scenario = new QueueScenario(
                   new[] { havoc, child },
                   new[] { triggerTopDrawCard, gainBlock },
                   new[] { havocTemplateId, childTemplateId },
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 1,
                   enemyDamage: 0))
        {
            CardInstanceId havocCardId = scenario.FindCard(havocTemplateId);
            CardInstanceId childCardId = scenario.Zones.DrawPile.Single();

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                havocCardId,
                scenario.Player.Id));
            BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);

            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));

            BattleCommandExecutionResult havocResult = scenario.Presentation.Results[1];
            Assert.That(havocResult.Succeeded, Is.True);
            Assert.That(havocResult.Settlements, Has.Count.EqualTo(2));
            var havocEnergy = havocResult.Settlements[0] as BattleEnergySpentSettlement;
            var havocMoved = havocResult.Settlements[1] as BattleCardMovedSettlement;
            Assert.That(havocEnergy, Is.Not.Null);
            Assert.That(havocEnergy.EnergyBefore, Is.EqualTo(3));
            Assert.That(havocEnergy.EnergyAfter, Is.EqualTo(2));
            Assert.That(havocEnergy.Amount, Is.EqualTo(1));
            Assert.That(havocMoved, Is.Not.Null);
            Assert.That(havocMoved.CardId, Is.EqualTo(havocCardId));
            Assert.That(havocMoved.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(havocMoved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Player.CurrentBlock, Is.Zero);
            Assert.That(scenario.Zones.DrawPile, Is.EqualTo(new[] { childCardId }));
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(2));

            scenario.Presentation.CompleteNext();

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(3));
            BattleCommandExecutionResult childResult = scenario.Presentation.Results[2];
            Assert.That(childResult.Succeeded, Is.True);
            Assert.That(childResult.Settlements, Has.Count.EqualTo(3));
            var childEnergy = childResult.Settlements[0] as BattleEnergySpentSettlement;
            var childBlock = childResult.Settlements[1] as BattleBlockGainedSettlement;
            var childMoved = childResult.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(childEnergy, Is.Not.Null);
            Assert.That(childEnergy.EnergyBefore, Is.EqualTo(2));
            Assert.That(childEnergy.EnergyAfter, Is.EqualTo(2));
            Assert.That(childEnergy.Amount, Is.Zero);
            Assert.That(childBlock, Is.Not.Null);
            Assert.That(childBlock.BlockBefore, Is.Zero);
            Assert.That(childBlock.BlockAfter, Is.EqualTo(7));
            Assert.That(childBlock.Amount, Is.EqualTo(7));
            Assert.That(childMoved, Is.Not.Null);
            Assert.That(childMoved.CardId, Is.EqualTo(childCardId));
            Assert.That(childMoved.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(childMoved.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(2));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(7));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.DrawPile, Is.Empty);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { havocCardId }));
            Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { childCardId }));
        }
    }

    /// <summary>验证 Juggernaut 在后续获得格挡后，把随机敌人伤害排在父命令表现屏障之后独立结算。</summary>
    [Test]
    public void Submit_JuggernautThenGainBlock_QueuesRandomEnemyDamageBehindParentPresentation()
    {
        const int juggernautTemplateId = 3169;
        const int gainBlockTemplateId = 3992;
        const int juggernautEffectId = 4597;
        const int gainBlockEffectId = 4598;
        const cfg.battle.EffectType juggernautEffectType =
            cfg.battle.EffectType.RegisterBlockGainRandomEnemyDamage;
        JObject juggernaut = CreateIroncladCard(
            juggernautTemplateId,
            "JUGGERNAUT",
            cost: 2,
            cfg.battle.TargetRule.Self,
            juggernautEffectId);
        juggernaut["card_type"] = (int)cfg.battle.CardType.Power;
        juggernaut["play_destination"] = (int)cfg.battle.CardPlayDestination.Power;
        juggernaut["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.Power;
        JObject gainBlockCard = CreateCard(
            gainBlockTemplateId,
            cost: 0,
            cfg.battle.TargetRule.Self,
            gainBlockEffectId);
        JObject activateJuggernaut = CreateEffect(
            juggernautEffectId,
            juggernautEffectType,
            cfg.battle.Attribute.None,
            value: 6);
        JObject gainBlock = CreateEffect(
            gainBlockEffectId,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 4);
        using (var scenario = new QueueScenario(
                   new[] { juggernaut, gainBlockCard },
                   new[] { activateJuggernaut, gainBlock },
                   new[] { juggernautTemplateId, gainBlockTemplateId },
                   playerStrength: 0,
                   energyPerRound: 3,
                   enemyHealths: new[] { 20, 20 },
                   battleSeed: 4321,
                   enemyDamage: 0))
        {
            CardInstanceId juggernautCardId = scenario.FindCard(juggernautTemplateId);
            CardInstanceId gainBlockCardId = scenario.FindCard(gainBlockTemplateId);
            using (BattleCommandLifecycleExecutionRecorder recorder =
                   scenario.Queue.RecordExecutionLifecycle())
            {
                BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                    new PlayCardCommand(
                        scenario.Player.Id,
                        juggernautCardId,
                        scenario.Player.Id));
                BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);

                Assert.That(submission.Accepted, Is.True);
                Assert.That(
                    terminal.Stage,
                    Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
                Assert.That(
                    terminal.FailureReason,
                    Is.EqualTo(BattleCommandExecutionFailureReason.None));
            }

            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(2));
            scenario.Presentation.CompleteNext();
            Assert.That(scenario.Zones.PowerPile, Is.EqualTo(new[] { juggernautCardId }));

            int resultCountBeforeGainBlock = scenario.Presentation.Results.Count;
            using (BattleCommandLifecycleExecutionRecorder recorder =
                   scenario.Queue.RecordExecutionLifecycle())
            {
                BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                    new PlayCardCommand(
                        scenario.Player.Id,
                        gainBlockCardId,
                        scenario.Player.Id));
                BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);

                Assert.That(submission.Accepted, Is.True);
                Assert.That(
                    terminal.Stage,
                    Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
                Assert.That(
                    terminal.FailureReason,
                    Is.EqualTo(BattleCommandExecutionFailureReason.None));
            }

            Assert.That(
                scenario.Presentation.Results,
                Has.Count.EqualTo(resultCountBeforeGainBlock + 1));
            BattleCommandExecutionResult parentResult =
                scenario.Presentation.Results[resultCountBeforeGainBlock];
            Assert.That(parentResult.Succeeded, Is.True);
            BattleBlockGainedSettlement[] parentBlockSettlements = parentResult.Settlements
                .OfType<BattleBlockGainedSettlement>()
                .ToArray();
            Assert.That(parentBlockSettlements, Has.Length.EqualTo(1));
            Assert.That(parentBlockSettlements[0].Amount, Is.EqualTo(4));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(4));
            Assert.That(
                scenario.Enemies.Select(enemy => enemy.CurrentHealth),
                Is.EqualTo(new[] { 20, 20 }));

            scenario.Presentation.CompleteNext();

            Assert.That(
                scenario.Presentation.Results,
                Has.Count.EqualTo(resultCountBeforeGainBlock + 2));
            BattleCommandExecutionResult childResult =
                scenario.Presentation.Results[resultCountBeforeGainBlock + 1];
            Assert.That(childResult.Succeeded, Is.True);
            BattleDamageAppliedSettlement[] childDamageSettlements = childResult.Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            Assert.That(childDamageSettlements, Has.Length.EqualTo(1));
            BattleDamageAppliedSettlement childDamage = childDamageSettlements[0];
            Assert.That(childDamage.SourceId, Is.EqualTo(scenario.Player.Id));
            Assert.That(
                scenario.Enemies.Select(enemy => enemy.Id),
                Does.Contain(childDamage.TargetId));
            Assert.That(
                (childDamage.AttackValue, childDamage.HealthBefore, childDamage.HealthAfter),
                Is.EqualTo((6, 20, 14)));
            Assert.That(
                scenario.Enemies.Count(enemy => enemy.CurrentHealth == 14),
                Is.EqualTo(1));
            Assert.That(
                scenario.Enemies.Count(enemy => enemy.CurrentHealth == 20),
                Is.EqualTo(1));
        }
    }

    /// <summary>验证 Barricade 经公共队列进入能力区后，玩家已有格挡跨过完整敌方回合仍不在下一玩家回合开始时清除。</summary>
    [Test]
    public void Submit_BarricadeThenCompleteEnemyRound_PreservesExistingBlockAtNextPlayerRoundStart()
    {
        const int setupTemplateId = 3993;
        const int barricadeTemplateId = 3157;
        const int setupBlockEffectId = 4487;
        const int barricadeEffectId = 4488;
        const cfg.battle.EffectType preserveBlockEffectType = cfg.battle.EffectType.RetainBlock;
        JObject setup = CreateCard(
            setupTemplateId,
            cost: 0,
            cfg.battle.TargetRule.Self,
            setupBlockEffectId);
        JObject barricade = CreateIroncladCard(
            barricadeTemplateId,
            "BARRICADE",
            cost: 3,
            cfg.battle.TargetRule.Self,
            barricadeEffectId);
        barricade["card_type"] = (int)cfg.battle.CardType.Power;
        barricade["play_destination"] = (int)cfg.battle.CardPlayDestination.Power;
        barricade["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.Power;
        JObject setupBlock = CreateEffect(
            setupBlockEffectId,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 7);
        JObject preserveBlock = CreateEffect(
            barricadeEffectId,
            preserveBlockEffectType,
            cfg.battle.Attribute.None,
            value: 0);
        using (var scenario = new QueueScenario(
                   new[] { setup, barricade },
                   new[] { setupBlock, preserveBlock },
                   new[] { setupTemplateId, barricadeTemplateId },
                   playerStrength: 0,
                   energyPerRound: 3,
                   enemyDamage: 0))
        {
            CardInstanceId setupCardId = scenario.FindCard(setupTemplateId);
            CardInstanceId barricadeCardId = scenario.FindCard(barricadeTemplateId);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                setupCardId,
                scenario.Player.Id));
            BattleCommandExecutionResult setupResult = scenario.Presentation.Results[1];
            Assert.That(setupResult.Succeeded, Is.True);
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(7));
            scenario.Presentation.CompleteNext();

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult barricadeSubmission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    barricadeCardId,
                    scenario.Player.Id));
            BattleCommandLifecycleEvent barricadeTerminal =
                recorder.RequireTerminal(barricadeSubmission);

            Assert.That(barricadeSubmission.Accepted, Is.True);
            Assert.That(
                barricadeTerminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.None));
            Assert.That(
                barricadeTerminal.Stage,
                Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
            scenario.Presentation.CompleteNext();
            Assert.That(scenario.Zones.PowerPile, Is.EqualTo(new[] { barricadeCardId }));

            int resultCountBeforeEnd = scenario.Presentation.Results.Count;
            scenario.Queue.Submit(new EndPlayerActionCommand(scenario.Player.Id));
            BattleCommandExecutionResult endPlayerAction =
                scenario.Presentation.Results[resultCountBeforeEnd];
            Assert.That(endPlayerAction.Succeeded, Is.True);
            scenario.Presentation.CompleteNext();
            BattleCommandExecutionResult completeEnemyRound =
                scenario.Presentation.Results[resultCountBeforeEnd + 1];

            Assert.That(completeEnemyRound.Succeeded, Is.True);
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Phase,
                Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(7));
            Assert.That(scenario.Zones.PowerPile, Is.EqualTo(new[] { barricadeCardId }));
            Assert.That(
                endPlayerAction.Settlements
                    .Concat(completeEnemyRound.Settlements)
                    .OfType<BattleBlockClearedSettlement>()
                    .Where(item => item.TargetId == scenario.Player.Id),
                Is.Empty);
        }
    }

    /// <summary>验证开始抽牌、结束弃手与最终敌人完成触发的重洗都进入各自命令记录。</summary>
    [Test]
    public void Submit_PhaseCommands_RecordInitialDrawDiscardAndReshuffle()
    {
        JObject firstCard = CreateCard(3101, 0, cfg.battle.TargetRule.Self);
        JObject secondCard = CreateCard(3102, 0, cfg.battle.TargetRule.Self);
        JObject thirdCard = CreateCard(3103, 0, cfg.battle.TargetRule.Self);
        using (var scenario = new QueueScenario(
                   new[] { firstCard, secondCard, thirdCard },
                   Array.Empty<JObject>(),
                   new[] { 3101, 3102, 3103 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 2))
        {
            BattleCommandExecutionResult start = scenario.Presentation.Results[0];
            Assert.That(start.Succeeded, Is.True);
            Assert.That(start.Settlements.Count, Is.EqualTo(4));
            Assert.That(start.Settlements[0], Is.TypeOf<BattleEnergyRefilledSettlement>());
            for (int index = 0; index < scenario.Zones.Hand.Count; index++)
            {
                var moved = start.Settlements[index + 1] as BattleCardMovedSettlement;
                Assert.That(moved, Is.Not.Null);
                Assert.That(moved.Order, Is.EqualTo(index + 1));
                Assert.That(moved.CardId, Is.EqualTo(scenario.Zones.Hand[index]));
                Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
                Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.Hand));
            }
            Assert.That(start.Settlements[3], Is.TypeOf<BattlePhaseChangedSettlement>());

            CardInstanceId[] handBeforeEnd = new List<CardInstanceId>(scenario.Zones.Hand).ToArray();
            scenario.Queue.Submit(new EndPlayerActionCommand(scenario.Player.Id));
            BattleCommandExecutionResult end = scenario.Presentation.Results[1];
            Assert.That(end.Succeeded, Is.True);
            Assert.That(end.Settlements.Count, Is.EqualTo(3));
            for (int index = 0; index < handBeforeEnd.Length; index++)
            {
                var moved = end.Settlements[index] as BattleCardMovedSettlement;
                Assert.That(moved, Is.Not.Null);
                Assert.That(moved.Order, Is.EqualTo(index));
                Assert.That(moved.CardId, Is.EqualTo(handBeforeEnd[index]));
                Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.Hand));
                Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            }
            Assert.That(end.Settlements[2], Is.TypeOf<BattlePhaseChangedSettlement>());

            scenario.Presentation.CompleteNext();
            BattleCommandExecutionResult completeEnemy = scenario.Presentation.Results[2];
            Assert.That(completeEnemy.Succeeded, Is.True);
            Assert.That(completeEnemy.Settlements.Count, Is.EqualTo(8));
            for (int index = 0; index < completeEnemy.Settlements.Count; index++)
                Assert.That(completeEnemy.Settlements[index].Order, Is.EqualTo(index));

            var damage = completeEnemy.Settlements[0] as BattleDamageAppliedSettlement;
            var intent = completeEnemy.Settlements[1] as BattleEnemyIntentAdvancedSettlement;
            var residualDraw = completeEnemy.Settlements[2] as BattleCardMovedSettlement;
            var firstRecycled = completeEnemy.Settlements[3] as BattleCardMovedSettlement;
            var secondRecycled = completeEnemy.Settlements[4] as BattleCardMovedSettlement;
            var reshuffled = completeEnemy.Settlements[5] as BattleCardsReshuffledSettlement;
            var continuedDraw = completeEnemy.Settlements[6] as BattleCardMovedSettlement;
            var phaseChanged = completeEnemy.Settlements[7] as BattlePhaseChangedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.SourceId, Is.EqualTo(scenario.Enemy.Id));
            Assert.That(damage.TargetId, Is.EqualTo(scenario.Player.Id));
            Assert.That(intent, Is.Not.Null);
            Assert.That(intent.SourceId, Is.EqualTo(scenario.Enemy.Id));
            Assert.That(residualDraw, Is.Not.Null);
            Assert.That(residualDraw.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(residualDraw.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(firstRecycled, Is.Not.Null);
            Assert.That(firstRecycled.CardId, Is.EqualTo(handBeforeEnd[0]));
            Assert.That(firstRecycled.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(firstRecycled.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(secondRecycled, Is.Not.Null);
            Assert.That(secondRecycled.CardId, Is.EqualTo(handBeforeEnd[1]));
            Assert.That(reshuffled, Is.Not.Null);
            Assert.That(reshuffled.NewDrawPileOrder.Count, Is.EqualTo(2));
            Assert.That(continuedDraw, Is.Not.Null);
            Assert.That(
                continuedDraw.CardId,
                Is.EqualTo(reshuffled.NewDrawPileOrder[reshuffled.NewDrawPileOrder.Count - 1]));
            Assert.That(continuedDraw.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(continuedDraw.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(phaseChanged, Is.Not.Null);
            Assert.That(phaseChanged.PhaseBefore, Is.EqualTo(BattleTurnPhase.EnemyAction));
            Assert.That(phaseChanged.PhaseAfter, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[]
            {
                residualDraw.CardId,
                continuedDraw.CardId,
            }));
            Assert.That(scenario.Zones.DrawPile, Is.EqualTo(new[]
            {
                reshuffled.NewDrawPileOrder[0],
            }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>验证目录占位卡经唯一队列明确失败，且不修改任何战斗权威事实。</summary>
    [Test]
    public void Submit_CatalogOnlyCard_FailsBeforeEnergyOrCardZoneWrites()
    {
        JObject catalogOnlyCard = CreateCard(
            3900,
            cost: 2,
            cfg.battle.TargetRule.Self,
            cfg.battle.CardImplementationStatus.CatalogOnly,
            4900);
        JObject catalogOnlyEffect = CreateEffect(
            4900,
            cfg.battle.EffectType.ModifyAttribute,
            cfg.battle.Attribute.Strength,
            value: 9);
        using (var scenario = new QueueScenario(
                   new[] { catalogOnlyCard },
                   new[] { catalogOnlyEffect },
                   new[] { 3900 },
                   playerStrength: 1,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3900);
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            int presentationCountBefore = scenario.Presentation.Results.Count;
            int playerHealthBefore = scenario.Player.CurrentHealth;
            int playerStrengthBefore = scenario.Player.CurrentStrength;
            int playerBlockBefore = scenario.Player.CurrentBlock;
            int playerVulnerableBefore = scenario.Player.CurrentVulnerable;
            int enemyHealthBefore = scenario.Enemy.CurrentHealth;
            int enemyStrengthBefore = scenario.Enemy.CurrentStrength;
            int enemyBlockBefore = scenario.Enemy.CurrentBlock;
            int enemyVulnerableBefore = scenario.Enemy.CurrentVulnerable;

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Player.Id));

            BattleCommandLifecycleEvent result = recorder.RequireTerminal(submission);
            Assert.That(result.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                result.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.CardNotImplemented));
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(presentationCountBefore));
            Assert.That(scenario.Queue.Queue.CurrentValue.IsFaulted, Is.False);
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(playerHealthBefore));
            Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(playerStrengthBefore));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(playerBlockBefore));
            Assert.That(scenario.Player.CurrentVulnerable, Is.EqualTo(playerVulnerableBefore));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(enemyHealthBefore));
            Assert.That(scenario.Enemy.CurrentStrength, Is.EqualTo(enemyStrengthBefore));
            Assert.That(scenario.Enemy.CurrentBlock, Is.EqualTo(enemyBlockBefore));
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.EqualTo(enemyVulnerableBefore));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(scenario.Zones.ExhaustPile, Is.Empty);
        }
    }

    /// <summary>验证后序 Effect 缺表时在支付能量、写参与者与移动卡牌前整体失败。</summary>
    [Test]
    public void Submit_CardWithMissingLaterEffect_FailsBeforeAnyWrite()
    {
        JObject invalidCard = CreateCard(
            3901,
            cost: 1,
            cfg.battle.TargetRule.Self,
            4901,
            499999);
        JObject validFirstEffect = CreateEffect(
            4901,
            cfg.battle.EffectType.ModifyAttribute,
            cfg.battle.Attribute.Strength,
            value: 3);
        using (var scenario = new QueueScenario(
                   new[] { invalidCard },
                   new[] { validFirstEffect },
                   new[] { 3901 },
                   playerStrength: 1,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3901);
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            var healthBefore = scenario.Player.Health;
            var strengthBefore = scenario.Player.Strength;
            var blockBefore = scenario.Player.Block;
            var vulnerableBefore = scenario.Player.Vulnerable;

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Player.Id));

            BattleCommandLifecycleEvent result = recorder.RequireTerminal(submission);
            Assert.That(result.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                result.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.EffectTemplateNotFound));
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(1));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Player.Health, Is.SameAs(healthBefore));
            Assert.That(scenario.Player.Strength, Is.SameAs(strengthBefore));
            Assert.That(scenario.Player.Block, Is.SameAs(blockBefore));
            Assert.That(scenario.Player.Vulnerable, Is.SameAs(vulnerableBefore));
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(30));
            Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(1));
            Assert.That(scenario.Player.CurrentBlock, Is.Zero);
            Assert.That(scenario.Player.CurrentVulnerable, Is.Zero);
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>验证有限升级轨道也必须在投影前把缺失 Effect 映射为稳定失败，而不是队列 Fault。</summary>
    [Test]
    public void Submit_FiniteCardWithMissingEffect_FailsBeforeProjectionFault()
    {
        const int cardId = 3904;
        JObject invalidCard = CreateCard(
            cardId,
            cost: 1,
            cfg.battle.TargetRule.Self,
            499999);
        invalidCard["upgrade_track_kind"] = (int)cfg.battle.CardUpgradeTrackKind.Finite;
        JObject upgrade = CreateUpgradeLevel(
            cardId,
            nextUpgradeLevel: 1,
            cost: 0,
            cfg.battle.CardPlayDestination.DiscardPile,
            cfg.battle.CardUpgradeRuleKind.None,
            ruleValue: 0);
        using (var scenario = new QueueScenario(
                   new[] { invalidCard },
                   Array.Empty<JObject>(),
                   new[] { cardId },
                   playerStrength: 1,
                   energyPerRound: 3,
                   upgradeLevels: new[] { upgrade }))
        {
            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    scenario.FindCard(cardId),
                    scenario.Player.Id));

            BattleCommandLifecycleEvent result = recorder.RequireTerminal(submission);
            Assert.That(result.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(
                result.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.EffectTemplateNotFound));
            Assert.That(result.Settlements, Is.Empty);
        }
    }

    /// <summary>验证 Card 边缘拒绝零 Effect 绑定，并在支付能量、写状态或移牌前整体失败。</summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void Submit_CardWithInvalidEffectBinding_FailsBeforeAnyWrite(int effectId)
    {
        JObject invalidCard = CreateCard(
            3902,
            cost: 1,
            cfg.battle.TargetRule.Self,
            effectId);
        using (var scenario = new QueueScenario(
                   new[] { invalidCard },
                   Array.Empty<JObject>(),
                   new[] { 3902 },
                   playerStrength: 1,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3902);
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            var healthBefore = scenario.Player.Health;
            var strengthBefore = scenario.Player.Strength;
            var blockBefore = scenario.Player.Block;
            var vulnerableBefore = scenario.Player.Vulnerable;

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Player.Id));

            BattleCommandLifecycleEvent result = recorder.RequireTerminal(submission);
            Assert.That(result.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(result.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InvalidEffectBinding));
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(scenario.Presentation.Results, Has.Count.EqualTo(1));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Player.Health, Is.SameAs(healthBefore));
            Assert.That(scenario.Player.Strength, Is.SameAs(strengthBefore));
            Assert.That(scenario.Player.Block, Is.SameAs(blockBefore));
            Assert.That(scenario.Player.Vulnerable, Is.SameAs(vulnerableBefore));
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>验证 Card 边缘拒绝运行时空绑定，并在任何权威写入前返回稳定失败。</summary>
    [Test]
    public void Submit_CardWithNullEffectBinding_FailsBeforeAnyWrite()
    {
        JObject invalidCard = CreateCard(
            3903,
            cost: 1,
            cfg.battle.TargetRule.Self,
            4903);
        JObject effect = CreateEffect(
            4903,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 5);
        using (var scenario = new QueueScenario(
                   new[] { invalidCard },
                   new[] { effect },
                   new[] { 3903 },
                   playerStrength: 1,
                   energyPerRound: 3))
        {
            scenario.Tables.TbCard.GetOrDefault(3903).EffectBindings[0] = null;
            CardInstanceId cardId = scenario.FindCard(3903);
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            var healthBefore = scenario.Player.Health;
            var strengthBefore = scenario.Player.Strength;
            var blockBefore = scenario.Player.Block;
            var vulnerableBefore = scenario.Player.Vulnerable;

            using BattleCommandLifecycleExecutionRecorder recorder =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Queue.Submit(
                new PlayCardCommand(
                    scenario.Player.Id,
                    cardId,
                    scenario.Player.Id));

            BattleCommandLifecycleEvent result = recorder.RequireTerminal(submission);
            Assert.That(result.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
            Assert.That(result.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InvalidEffectBinding));
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Player.Health, Is.SameAs(healthBefore));
            Assert.That(scenario.Player.Strength, Is.SameAs(strengthBefore));
            Assert.That(scenario.Player.Block, Is.SameAs(blockBefore));
            Assert.That(scenario.Player.Vulnerable, Is.SameAs(vulnerableBefore));
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>创建一张携带正式有序 Effect 绑定的最小 Card JSON。</summary>
    private static JObject CreateCard(
        int id,
        int cost,
        cfg.battle.TargetRule targetRule,
        params int[] effectIds)
    {
        return CreateCard(
            id,
            cost,
            targetRule,
            cfg.battle.CardImplementationStatus.Implemented,
            effectIds);
    }

    /// <summary>创建一张带显式实现状态和正式有序 Effect 绑定的最小 Card JSON。</summary>
    private static JObject CreateCard(
        int id,
        int cost,
        cfg.battle.TargetRule targetRule,
        cfg.battle.CardImplementationStatus implementationStatus,
        params int[] effectIds)
    {
        var bindings = new JArray();
        foreach (int effectId in effectIds)
        {
            bindings.Add(new JObject
            {
                ["argument_key"] = string.Empty,
                ["effect_id"] = effectId,
            });
        }

        return new JObject
        {
            ["id"] = id,
            ["external_key"] = $"TEST_EFFECT_COMMAND_QUEUE_CARD_{id}",
            ["catalog_snapshot_key"] = "test-fixture",
            ["name_i18n_key"] = $"battle.card.test_{id}.name",
            ["description_i18n_key"] = $"battle.card.test_{id}.description",
            ["upgraded_description_i18n_key"] = $"battle.card.test_{id}.description",
            ["card_type"] = (int)(targetRule == cfg.battle.TargetRule.Enemy
                ? cfg.battle.CardType.Attack
                : cfg.battle.CardType.Skill),
            ["rarity"] = (int)cfg.battle.CardRarity.Basic,
            ["cost"] = cost,
            ["cost_kind"] = (int)cfg.battle.CardCostKind.Fixed,
            ["upgraded_cost"] = cost,
            ["target_rule"] = (int)targetRule,
            ["play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
            ["upgraded_play_destination"] = (int)cfg.battle.CardPlayDestination.DiscardPile,
            ["has_upgrade"] = false,
            ["implementation_status"] = (int)implementationStatus,
            ["program_id"] = (int)cfg.battle.MachineGunnerProgramId.None,
            ["is_innate"] = false,
            ["upgrade_track_kind"] = (int)cfg.battle.CardUpgradeTrackKind.None,
            ["infinite_upgrade_rule_kind"] = (int)cfg.battle.CardUpgradeRuleKind.None,
            ["infinite_upgrade_value_per_level"] = 0,
            ["effect_bindings"] = bindings,
            ["illustration_key"] = string.Empty,
        };
    }

    /// <summary>创建一条由 G4 配置驱动的有限升级等级测试行。</summary>
    private static JObject CreateUpgradeLevel(
        int cardId,
        int nextUpgradeLevel,
        int cost,
        cfg.battle.CardPlayDestination playDestination,
        cfg.battle.CardUpgradeRuleKind ruleKind,
        int ruleValue)
    {
        return new JObject
        {
            ["card_id"] = cardId,
            ["next_upgrade_level"] = nextUpgradeLevel,
            ["description_i18n_key"] = $"battle.card.test_{cardId}.upgrade_{nextUpgradeLevel}",
            ["cost"] = cost,
            ["play_destination"] = (int)playDestination,
            ["rule_kind"] = (int)ruleKind,
            ["rule_value"] = ruleValue,
        };
    }

    /// <summary>创建一张携带冻结 Ironclad 身份和有序 Effect 绑定的最小测试卡牌。</summary>
    private static JObject CreateIroncladCard(
        int id,
        string externalKey,
        int cost,
        cfg.battle.TargetRule targetRule,
        params int[] effectIds)
    {
        JObject card = CreateCard(id, cost, targetRule, effectIds);
        card["external_key"] = externalKey;
        card["catalog_snapshot_key"] = "sts2-v0.107.1-23811903-59260271";
        return card;
    }

    /// <summary>创建一条最小 CardEffect JSON。</summary>
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

    /// <summary>封装公共队列测试所需的唯一事实、静态表和显式表现屏障。</summary>
    private sealed class QueueScenario : IDisposable
    {
        internal BattleCombatantsData Combatants { get; }
        internal PlayerCombatantData Player { get; }
        internal IReadOnlyList<EnemyCombatantData> Enemies { get; }
        internal EnemyCombatantData Enemy => Enemies[0];
        internal BattleCardZonesData Zones { get; }
        internal BattleEnemyIntentsData EnemyIntents { get; }
        internal cfg.Tables Tables { get; }
        internal ControllableBattleCommandPresentation Presentation { get; }
        internal BattleCommandQueue Queue { get; }

        /// <summary>创建并开始一场仅由公共队列驱动的测试战斗。</summary>
        internal QueueScenario(
            IEnumerable<JObject> cards,
            IEnumerable<JObject> effects,
            IEnumerable<int> deck,
            int playerStrength,
            int energyPerRound,
            int enemyHealth = 40,
            bool drawDeckIntoHand = true,
            int initialHandCount = 0,
            IReadOnlyList<int> enemyHealths = null,
            uint battleSeed = 4321,
            int enemyDamage = 1,
            IEnumerable<RunCard> runCards = null,
            IEnumerable<JObject> upgradeLevels = null)
        {
            Combatants = new BattleCombatantsData();
            Player = Combatants.AddPlayer(101, 30, playerStrength);
            IReadOnlyList<int> resolvedEnemyHealths = enemyHealths ?? new[] { enemyHealth };
            if (resolvedEnemyHealths.Count == 0)
                throw new ArgumentException("测试战斗至少需要一个敌人。", nameof(enemyHealths));

            var enemies = new List<EnemyCombatantData>(resolvedEnemyHealths.Count);
            for (int i = 0; i < resolvedEnemyHealths.Count; i++)
                enemies.Add(Combatants.AddEnemy(201 + i, resolvedEnemyHealths[i], 0));

            Enemies = enemies;
            CombatantId[] enemyIds = Enemies.Select(enemy => enemy.Id).ToArray();
            Zones = runCards == null
                ? new BattleCardZonesData(deck, shuffleSeed: 1234)
                : new BattleCardZonesData(runCards, shuffleSeed: 1234);
            int resolvedInitialHandCount = drawDeckIntoHand
                ? Zones.Cards.Count
                : initialHandCount;
            Tables = CreateTables(cards, effects, Enemies, enemyDamage, upgradeLevels);
            EnemyIntents = new BattleEnemyIntentsData(
                Combatants,
                enemyIds,
                Tables,
                battleSeed);
            Presentation = new ControllableBattleCommandPresentation();
            Queue = BattleCommandQueueTestFactory.Create(
                Combatants,
                Presentation,
                new Dictionary<CombatantId, BattleCardZonesData>
                {
                    [Player.Id] = Zones,
                },
                enemyCombatantIdsInEncounterOrder: enemyIds,
                energyPerRound: energyPerRound,
                initialHandCount: resolvedInitialHandCount,
                enemyIntents: EnemyIntents,
                tables: Tables,
                battleSeed: battleSeed);
            Queue.Submit(new StartBattleCommand());
            Presentation.CompleteNext();
        }

        /// <summary>仅为队列用例建立目标的既有权威状态，仍经正式 Effect module 写入。</summary>
        internal void ExecuteSetupCardEffects(int cardTemplateId, CombatantId targetId)
        {
            cfg.battle.Card card = Tables.TbCard.GetOrDefault(cardTemplateId);
            if (card == null)
                throw new InvalidOperationException($"测试准备卡牌 {cardTemplateId} 不存在。");

            var effectIds = new List<BattleEffectId>(card.EffectBindings.Length);
            foreach (cfg.battle.CardEffectBinding binding in card.EffectBindings)
            {
                if (binding == null || binding.EffectId <= 0)
                    throw new InvalidOperationException("测试准备卡牌包含非法 Effect 绑定。");

                effectIds.Add(new BattleEffectId(binding.EffectId));
            }

            var executor = new BattleEffectExecutor(Tables, Combatants);
            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    Player.Id,
                    targetId,
                    effectIds));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"测试准备 Effect 执行失败：{result.FailureReason}。");
            }
        }

        /// <summary>按静态模板标识查找当前手牌实例。</summary>
        internal CardInstanceId FindCard(int templateId)
        {
            foreach (CardInstanceId cardId in Zones.Hand)
            {
                if (Zones.Cards[cardId].TemplateId == templateId)
                    return cardId;
            }

            throw new InvalidOperationException($"测试手牌中不存在模板 {templateId}。");
        }

        /// <summary>按稳定 RunCard 身份查找同模板也不会混同的当前手牌实例。</summary>
        internal CardInstanceId FindRunCard(RunCardInstanceId runCardInstanceId)
        {
            foreach (CardInstanceId cardId in Zones.Hand)
            {
                if (Zones.Cards[cardId].OriginRunCardInstanceId == runCardInstanceId)
                    return cardId;
            }

            throw new InvalidOperationException(
                $"测试手牌中不存在 RunCard 实例 {runCardInstanceId}。");
        }

        /// <summary>按所有权逆序释放队列、意图、卡区与参与者。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            EnemyIntents.Dispose();
            Zones.Dispose();
            Combatants.Dispose();
        }

        /// <summary>创建队列、规则和敌人意图共用的最小静态表。</summary>
        private static cfg.Tables CreateTables(
            IEnumerable<JObject> cards,
            IEnumerable<JObject> effects,
            IReadOnlyList<EnemyCombatantData> enemies,
            int enemyDamage,
            IEnumerable<JObject> upgradeLevels)
        {
            var effectRows = new JArray();
            foreach (JObject effect in effects)
                effectRows.Add(effect);
            effectRows.Add(CreateEffect(
                4999,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                value: enemyDamage));

            var enemyRows = new JArray();
            foreach (EnemyCombatantData enemy in enemies)
            {
                enemyRows.Add(new JObject
                {
                    ["id"] = enemy.TemplateId,
                    ["name_i18n_key"] = $"battle.enemy.test_{enemy.TemplateId}.name",
                    ["max_health"] = enemy.MaxHealth,
                    ["base_strength"] = enemy.CurrentStrength,
                    ["view_prefab_key"] = string.Empty,
                    ["behavior_group_id"] = 6001,
                });
            }

            var data = new Dictionary<string, JArray>
            {
                ["battle_tbhero"] = new JArray(),
                ["battle_tbenemy"] = enemyRows,
                ["battle_tbdeck"] = new JArray(),
                ["battle_tbcard"] = new JArray(cards),
                ["battle_tbcardeffect"] = effectRows,
                ["battle_tbencounter"] = new JArray(),
                ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                    "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
                ["battle_tbenemybehavior"] = JArray.Parse(
                    "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":0}]"),
                ["battle_tbcardupgradelevel"] = upgradeLevels == null
                    ? new JArray()
                    : new JArray(upgradeLevels),
            };
            return new cfg.Tables(tableName =>
                data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
        }
    }
}
