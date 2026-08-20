using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;

/// <summary>从 Hero 配置到唯一命令队列验证机枪兵初始五张卡的最小可玩运行时切片。</summary>
public sealed class MachineGunnerStarterRuntimeTests
{
    /// <summary>验证机枪牌组未携带临时连射时，会话仍只按程序注册表补充该动态模板。</summary>
    [Test]
    public void FromConfig_MachinegunDeckWithoutBurst_PredeclaresOnlyRegisteredCreatedTemplate()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3261, 3201, 3201 },
            initialHandCount: 0);

        Assert.That(
            scenario.Zones.Cards.Values.Select(card => card.TemplateId),
            Has.None.EqualTo(3263));
        Assert.That(
            MachineGunnerCardProgramRegistry.PotentiallyCreatedCardTemplateIds,
            Is.EqualTo(new[] { 3263 }));
        Assert.That(
            scenario.Session.AvailableCardTemplateIds,
            Is.EqualTo(new[] { 3261, 3201, 3263 }));
    }

    /// <summary>验证先发制人先造成普通攻击伤害，再按施放者命令起始时的活跃状态种类抽牌并弃置自身。</summary>
    [Test]
    public void PreemptiveStrike_SourceStatus_DamagesThenDrawsAndDiscardsSelfInOrder()
    {
        Assert.That(MachineGunnerCardProgramRegistry.TryGet(
            cfg.battle.MachineGunnerProgramId.PreemptiveStrike,
            out MachineGunnerCardProgram program), Is.True);
        Assert.That(program.TargetInputMode, Is.EqualTo(MachineGunnerTargetInputMode.ExplicitEnemy));
        Assert.That(program.BaseAmmoCost, Is.EqualTo(1));
        Assert.That(program.BaseShootHitCount, Is.Zero);
        Assert.That(program.AmmoSpendMode, Is.EqualTo(MachineGunnerAmmoSpendMode.Fixed));
        Assert.That(program.IsAttack, Is.True);
        Assert.That(program.Tags, Is.EqualTo(MachineGunnerCardTag.None));
        Assert.That(program.IsShootCategory, Is.False);
        Assert.That(program.ReceivesStimBonus, Is.False);
        Assert.That(program.Operations.Select(operation =>
                $"{operation.Kind}/{operation.Value}/{operation.TargetScope}"),
            Is.EqualTo(new[]
            {
                "Damage/8/ProgramTargets",
                "DrawCardsByActiveStatusKinds/1/Source",
            }));

        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3277, 3201 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 0,
            initialAmmo: 1);
        scenario.StartBattle();
        CardInstanceId sourceId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3277);
        CardInstanceId fillerId = scenario.Zones.Hand.Single(cardId => cardId != sourceId);
        scenario.Zones.DiscardFromHand(fillerId);
        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Burn,
            4);

        BattleCommandExecutionResult result = scenario.Play(3277, scenario.FirstEnemy.Id);

        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleAmmoSpentSettlement ammo = FindSettlement<BattleAmmoSpentSettlement>(result);
        BattleDamageAppliedSettlement damage = FindSettlement<BattleDamageAppliedSettlement>(result);
        BattleCardsReshuffledSettlement reshuffle = FindSettlement<BattleCardsReshuffledSettlement>(result);
        BattleCardMovedSettlement reshuffleMove = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == fillerId &&
                item.FromZone == BattleCardZone.DiscardPile &&
                item.ToZone == BattleCardZone.DrawPile);
        BattleCardMovedSettlement draw = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == fillerId && item.ToZone == BattleCardZone.Hand);
        BattleCardMovedSettlement departure = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == sourceId);
        Assert.That((energy.Order, energy.Amount), Is.EqualTo((0, 0)));
        Assert.That((ammo.Order, ammo.Amount), Is.EqualTo((1, 1)));
        Assert.That((damage.Order, damage.AttackValue), Is.EqualTo((2, 8)));
        Assert.That((damage.HealthBefore, damage.HealthAfter), Is.EqualTo((100, 92)));
        Assert.That(reshuffleMove.Order, Is.EqualTo(3));
        Assert.That(reshuffle.Order, Is.EqualTo(4));
        Assert.That(draw.Order, Is.EqualTo(5));
        Assert.That(departure.Order, Is.EqualTo(6));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证施放者的通用强度、易伤与十五种可达职业私有状态逐一计数；Shackle 单独锁定攻击禁用。</summary>
    [Test]
    public void PreemptiveStrike_SourceStatusKindMatrix_CountsEachKindOnce()
    {
        using (var combined = new MachineGunnerStarterScenario(
                   new[] { 3277, 3201, 3202, 3203, 3204 },
                   initialHandCount: 5,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 0,
                   initialAmmo: 1))
        {
            combined.StartBattle();
            CardInstanceId sourceId = combined.Zones.Hand.Single(cardId =>
                combined.Zones.Cards[cardId].TemplateId == 3277);
            foreach (CardInstanceId cardId in combined.Zones.Hand.Where(id => id != sourceId).ToArray())
                combined.Zones.DiscardFromHand(cardId);
            combined.Player.ApplyStrengthChange(-3);
            combined.Player.ApplyVulnerableGain(4);
            MachineGunnerCombatState state = combined.Session.MachineGunnerRuntime.CombatState;
            state.Add(combined.Player.Id, MachineGunnerCombatantStatus.Burn, 7);
            state.Add(combined.Player.Id, MachineGunnerCombatantStatus.Burn, 5);
            state.Add(combined.Player.Id, MachineGunnerCombatantStatus.Oil, 8);

            BattleCommandExecutionResult result = combined.Play(3277, combined.FirstEnemy.Id);

            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Count(item =>
                    item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand),
                Is.EqualTo(4));
        }

        MachineGunnerCombatantStatus[] privateStatuses =
            (MachineGunnerCombatantStatus[])Enum.GetValues(typeof(MachineGunnerCombatantStatus));
        Assert.That(privateStatuses, Has.Length.EqualTo(17));
        foreach (MachineGunnerCombatantStatus privateStatus in privateStatuses)
        {
            using var scenario = new MachineGunnerStarterScenario(
                new[] { 3277, 3201 },
                initialHandCount: 2,
                firstEnemyHealth: 100,
                enemyDamage: 0,
                initialEnergy: 0,
                initialAmmo: 1);
            scenario.StartBattle();
            CardInstanceId sourceId = scenario.Zones.Hand.Single(cardId =>
                scenario.Zones.Cards[cardId].TemplateId == 3277);
            scenario.Zones.DiscardFromHand(scenario.Zones.Hand.Single(cardId => cardId != sourceId));
            scenario.Session.MachineGunnerRuntime.CombatState.Add(
                scenario.Player.Id,
                privateStatus,
                7);

            if (privateStatus == MachineGunnerCombatantStatus.Shackle)
            {
                using BattleCommandLifecycleExecutionRecorder lifecycle =
                    scenario.Queue.RecordExecutionLifecycle();
                BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                    scenario.Submit(3277, scenario.FirstEnemy.Id));
                Assert.That(terminal.FailureReason,
                    Is.EqualTo(BattleCommandExecutionFailureReason.AttackBlockedByShackle));
                Assert.That(terminal.Settlements, Is.Empty);
                continue;
            }

            BattleCommandExecutionResult result = scenario.Play(3277, scenario.FirstEnemy.Id);

            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Count(item =>
                    item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand),
                Is.EqualTo(1), privateStatus.ToString());
        }
    }

    /// <summary>验证先发制人只把施放者的通用中毒计为一种状态，不计目标中毒且不会消耗任一方层数。</summary>
    [Test]
    public void PreemptiveStrike_GenericPoisonCountsOneSourceKindWithoutConsumingStacks()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3277, 3201, 3202, 3203 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 0,
            initialAmmo: 1);
        scenario.StartBattle();
        CardInstanceId sourceId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3277);
        CardInstanceId[] fillerIds = scenario.Zones.Hand
            .Where(cardId => cardId != sourceId)
            .ToArray();
        foreach (CardInstanceId fillerId in fillerIds)
            scenario.Zones.DiscardFromHand(fillerId);

        var poisonApplication = new BattlePoisonApplication(scenario.Session.Combatants);
        BattlePoisonApplicationPreparationResult sourcePoisonPreparation =
            poisonApplication.PrepareApply(
                scenario.FirstEnemy.Id,
                scenario.Player.Id,
                amount: 7);
        Assert.That(sourcePoisonPreparation.Succeeded, Is.True);
        Assert.That(poisonApplication.ValidatePrepared(sourcePoisonPreparation.Plan), Is.True);
        Assert.That(
            poisonApplication.CommitPrepared(sourcePoisonPreparation.Plan, startingOrder: 0),
            Has.Count.EqualTo(1));
        BattlePoisonApplicationPreparationResult targetPoisonPreparation =
            poisonApplication.PrepareApply(
                scenario.Player.Id,
                scenario.FirstEnemy.Id,
                amount: 11);
        Assert.That(targetPoisonPreparation.Succeeded, Is.True);
        Assert.That(poisonApplication.ValidatePrepared(targetPoisonPreparation.Plan), Is.True);
        Assert.That(
            poisonApplication.CommitPrepared(targetPoisonPreparation.Plan, startingOrder: 0),
            Has.Count.EqualTo(1));

        BattleCommandExecutionResult result = scenario.Play(3277, scenario.FirstEnemy.Id);

        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleAmmoSpentSettlement ammo = FindSettlement<BattleAmmoSpentSettlement>(result);
        BattleDamageAppliedSettlement damage = FindSettlement<BattleDamageAppliedSettlement>(result);
        BattleCardsReshuffledSettlement reshuffle = FindSettlement<BattleCardsReshuffledSettlement>(result);
        BattleCardMovedSettlement[] reshuffleMoves = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Where(item => item.FromZone == BattleCardZone.DiscardPile &&
                item.ToZone == BattleCardZone.DrawPile)
            .ToArray();
        BattleCardMovedSettlement[] draws = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Where(item => item.FromZone == BattleCardZone.DrawPile &&
                item.ToZone == BattleCardZone.Hand)
            .ToArray();
        BattleCardMovedSettlement departure = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == sourceId);
        Assert.That((energy.Order, energy.Amount), Is.EqualTo((0, 0)));
        Assert.That((ammo.Order, ammo.AmmoBefore, ammo.AmmoAfter, ammo.Amount),
            Is.EqualTo((1, 1, 0, 1)));
        Assert.That((damage.Order, damage.AttackValue, damage.HealthBefore, damage.HealthAfter),
            Is.EqualTo((2, 8, 100, 92)));
        Assert.That(reshuffleMoves, Has.Length.EqualTo(3));
        Assert.That(reshuffle.Order, Is.EqualTo(6));
        Assert.That(draws, Has.Length.EqualTo(1));
        Assert.That(draws[0].Order, Is.EqualTo(7));
        Assert.That(
            (departure.Order, departure.FromZone, departure.ToZone),
            Is.EqualTo((8, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(fillerIds, Does.Contain(scenario.Zones.Hand[0]));
        Assert.That(scenario.Zones.DrawPile, Has.Count.EqualTo(2));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceId }));
        Assert.That(scenario.Player.CurrentPoison, Is.EqualTo(7));
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(11));
    }

    /// <summary>验证能力、兴奋剂、延迟实例、格挡与生命都不是可计数状态，零种类时不制造抽牌。</summary>
    [Test]
    public void PreemptiveStrike_PowerStimScheduledBlockAndHealth_DoNotCountAsStatuses()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3206, 3205, 3274, 3277 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 3,
            initialAmmo: 1);
        scenario.StartBattle();
        scenario.Play(3206, targetId: null);
        scenario.Play(3205, targetId: null);
        scenario.Play(3274, targetId: null);
        scenario.Player.ApplyBlockGain(99);
        Assert.That(scenario.Session.MachineGunnerRuntime.GetPowerStack(
            MachineGunnerPowerKind.CoreExpansion), Is.EqualTo(1));
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.EqualTo(1));

        BattleCommandExecutionResult result = scenario.Play(3277, scenario.FirstEnemy.Id);

        Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
            item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand), Is.False);
        Assert.That(FindSettlement<BattleDamageAppliedSettlement>(result).AttackValue, Is.EqualTo(8));
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(result).Amount, Is.EqualTo(1));
    }

    /// <summary>验证状态数在命令开始时冻结：致死和隐身消耗不取消抽牌，命中后新增目标浸油也不反哺本次抽牌。</summary>
    [Test]
    public void PreemptiveStrike_FrozenSourceCountSurvivesLethalAndIgnoresPostHitTargetStatus()
    {
        using (var lethal = new MachineGunnerStarterScenario(
                   new[] { 3277, 3201 }, 2, firstEnemyHealth: 8, enemyDamage: 0,
                   initialEnergy: 0, initialAmmo: 1))
        {
            lethal.StartBattle();
            CardInstanceId sourceId = lethal.Zones.Hand.Single(cardId =>
                lethal.Zones.Cards[cardId].TemplateId == 3277);
            lethal.Zones.DiscardFromHand(lethal.Zones.Hand.Single(cardId => cardId != sourceId));
            lethal.Session.MachineGunnerRuntime.CombatState.Add(
                lethal.Player.Id, MachineGunnerCombatantStatus.Invisible, 1);

            BattleCommandExecutionResult result = lethal.Play(3277, lethal.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleDamageAppliedSettlement>(result).WasFatal, Is.True);
            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Count(item =>
                    item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand),
                Is.EqualTo(1));
            Assert.That(lethal.Session.MachineGunnerRuntime.CombatState.Get(
                lethal.Player.Id, MachineGunnerCombatantStatus.Invisible), Is.Zero);
        }

        using (var postHitOil = new MachineGunnerStarterScenario(
                   new[] { 3253, 3277, 3201 }, 3, firstEnemyHealth: 100, enemyDamage: 0,
                   initialEnergy: 1, initialAmmo: 1))
        {
            postHitOil.StartBattle();
            CardInstanceId fillerId = postHitOil.Zones.Hand.Single(cardId =>
                postHitOil.Zones.Cards[cardId].TemplateId == 3201);
            postHitOil.Zones.DiscardFromHand(fillerId);
            postHitOil.Play(3253, targetId: null);

            BattleCommandExecutionResult result = postHitOil.Play(3277, postHitOil.FirstEnemy.Id);

            Assert.That(postHitOil.Session.MachineGunnerRuntime.CombatState.Get(
                postHitOil.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(2));
            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
                item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand), Is.False);
        }
    }

    /// <summary>验证手牌上限截断抽牌，并且缺弹或缺少显式目标在所有权威写入前失败。</summary>
    [Test]
    public void PreemptiveStrike_HandLimitAndInvalidCostsOrTarget_KeepAtomicContract()
    {
        using (var fullHand = new MachineGunnerStarterScenario(
                   Enumerable.Repeat(3277, 11).ToArray(), 10, firstEnemyHealth: 100,
                   enemyDamage: 0, initialEnergy: 0, initialAmmo: 1))
        {
            fullHand.StartBattle();
            fullHand.Session.MachineGunnerRuntime.CombatState.Add(
                fullHand.Player.Id, MachineGunnerCombatantStatus.Burn, 3);
            uint shuffleBefore = fullHand.Zones.ShuffleRandomState;

            BattleCommandExecutionResult result = fullHand.Play(3277, fullHand.FirstEnemy.Id);

            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
                item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand), Is.False);
            Assert.That(fullHand.Zones.Hand, Has.Count.EqualTo(9));
            Assert.That(fullHand.Zones.DrawPile, Has.Count.EqualTo(1));
            Assert.That(fullHand.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
        }

        using (var noAmmo = new MachineGunnerStarterScenario(
                   new[] { 3277 }, 1, firstEnemyHealth: 100, enemyDamage: 0,
                   initialEnergy: 0, initialAmmo: 0))
        {
            noAmmo.StartBattle();
            CardZoneLayoutData layoutBefore = noAmmo.Zones.Layout.CurrentValue;
            uint shuffleBefore = noAmmo.Zones.ShuffleRandomState;
            uint randomBefore = noAmmo.Session.MachineGunnerRuntime.CardRandomState;
            int resultCountBefore = noAmmo.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle = noAmmo.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                noAmmo.Submit(3277, noAmmo.FirstEnemy.Id));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(noAmmo.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(noAmmo.FirstEnemy.CurrentHealth, Is.EqualTo(100));
            Assert.That(noAmmo.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(noAmmo.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(noAmmo.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
        }

        using (var missingTarget = new MachineGunnerStarterScenario(
                   new[] { 3277 }, 1, firstEnemyHealth: 100, enemyDamage: 0,
                   initialEnergy: 0, initialAmmo: 1))
        {
            missingTarget.StartBattle();
            CardZoneLayoutData layoutBefore = missingTarget.Zones.Layout.CurrentValue;
            uint shuffleBefore = missingTarget.Zones.ShuffleRandomState;
            uint randomBefore = missingTarget.Session.MachineGunnerRuntime.CardRandomState;
            int resultCountBefore = missingTarget.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                missingTarget.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                missingTarget.Submit(3277, targetId: null));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRequired));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(missingTarget.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(missingTarget.FirstEnemy.CurrentHealth, Is.EqualTo(100));
            Assert.That(missingTarget.Queue.Turn.CurrentValue.Players[missingTarget.Player.Id].Ammo,
                Is.EqualTo(1));
            Assert.That(missingTarget.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(missingTarget.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(missingTarget.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证欺凌先造成普通攻击伤害，再按目标命令起始时的活跃状态种类抽牌并弃置自身。</summary>
    [Test]
    public void Bully_TwoStartingPrivateStatusKinds_DamagesThenDrawsTwoAndDiscardsSelf()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3278, 3201, 3202 },
            initialHandCount: 3,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 0);
        scenario.StartBattle();
        CardInstanceId bullyId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3278);
        CardInstanceId[] oldDiscard = scenario.Zones.Hand
            .Where(cardId => cardId != bullyId)
            .ToArray();
        foreach (CardInstanceId cardId in oldDiscard)
            scenario.Zones.DiscardFromHand(cardId);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 4);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 7);

        BattleCommandExecutionResult result = scenario.Play(3278, scenario.FirstEnemy.Id);

        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleDamageAppliedSettlement damage = FindSettlement<BattleDamageAppliedSettlement>(result);
        Assert.That(energy.Order, Is.Zero);
        Assert.That(energy.Amount, Is.Zero);
        Assert.That(damage.Order, Is.EqualTo(1));
        Assert.That(damage.AttackValue, Is.EqualTo(6));
        Assert.That(damage.HealthBefore, Is.EqualTo(100));
        Assert.That(damage.HealthAfter, Is.EqualTo(94));
        Assert.That(result.Settlements.OfType<BattleCardsReshuffledSettlement>().Count(), Is.EqualTo(1));
        BattleCardMovedSettlement[] drawn = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Where(item => item.FromZone == BattleCardZone.DrawPile &&
                item.ToZone == BattleCardZone.Hand)
            .ToArray();
        Assert.That(drawn, Has.Length.EqualTo(2));
        Assert.That(drawn.All(item => item.Order > damage.Order), Is.True);
        BattleCardMovedSettlement departure = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == bullyId);
        Assert.That(departure.Order, Is.GreaterThan(drawn.Max(item => item.Order)));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Zones.Hand, Is.EquivalentTo(oldDiscard));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { bullyId }));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证通用强度、易伤与各职业私有状态按种类计数，同种多层只算一次且格挡与生命不计数。</summary>
    [Test]
    public void Bully_StatusKindMatrix_CountsKindsOnceAndIgnoresBlockAndHealth()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3278, 3201, 3202, 3203, 3204, 3205 },
            initialHandCount: 6,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 0);
        scenario.StartBattle();
        CardInstanceId bullyId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3278);
        foreach (CardInstanceId cardId in scenario.Zones.Hand.Where(id => id != bullyId).ToArray())
            scenario.Zones.DiscardFromHand(cardId);
        scenario.FirstEnemy.ApplyStrengthChange(3);
        scenario.FirstEnemy.ApplyVulnerableGain(4);
        scenario.FirstEnemy.ApplyBlockGain(99);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 7);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 8);
        var poisonApplication = new BattlePoisonApplication(scenario.Session.Combatants);
        BattlePoisonApplicationPreparationResult poisonPreparation =
            poisonApplication.PrepareApply(
                scenario.Player.Id,
                scenario.FirstEnemy.Id,
                amount: 5);
        Assert.That(poisonPreparation.Succeeded, Is.True);
        Assert.That(poisonApplication.ValidatePrepared(poisonPreparation.Plan), Is.True);
        IReadOnlyList<BattleSettlementRecord> poisonSettlements =
            poisonApplication.CommitPrepared(poisonPreparation.Plan, startingOrder: 0);
        Assert.That(poisonSettlements, Has.Count.EqualTo(1));

        BattleCommandExecutionResult result = scenario.Play(3278, scenario.FirstEnemy.Id);

        BattleCardMovedSettlement[] drawn = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Where(item => item.FromZone == BattleCardZone.DrawPile &&
                item.ToZone == BattleCardZone.Hand)
            .ToArray();
        Assert.That(drawn, Has.Length.EqualTo(5));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(5));
        Assert.That(scenario.Zones.DrawPile, Is.Empty);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(100));
        Assert.That(scenario.FirstEnemy.CurrentBlock, Is.EqualTo(90));
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(5));

        foreach (MachineGunnerCombatantStatus privateStatus in
                 (MachineGunnerCombatantStatus[])Enum.GetValues(
                     typeof(MachineGunnerCombatantStatus)))
        {
            using var privateStatusScenario = new MachineGunnerStarterScenario(
                new[] { 3278, 3201 },
                initialHandCount: 2,
                firstEnemyHealth: 100,
                enemyDamage: 0,
                initialEnergy: 0);
            privateStatusScenario.StartBattle();
            CardInstanceId privateBullyId = privateStatusScenario.Zones.Hand.Single(cardId =>
                privateStatusScenario.Zones.Cards[cardId].TemplateId == 3278);
            privateStatusScenario.Zones.DiscardFromHand(
                privateStatusScenario.Zones.Hand.Single(cardId => cardId != privateBullyId));
            privateStatusScenario.Session.MachineGunnerRuntime.CombatState.Add(
                privateStatusScenario.FirstEnemy.Id,
                privateStatus,
                7);

            BattleCommandExecutionResult privateResult = privateStatusScenario.Play(
                3278,
                privateStatusScenario.FirstEnemy.Id);

            Assert.That(privateResult.Settlements.OfType<BattleCardMovedSettlement>().Count(item =>
                    item.FromZone == BattleCardZone.DrawPile &&
                    item.ToZone == BattleCardZone.Hand),
                Is.EqualTo(1), privateStatus.ToString());
        }
    }

    /// <summary>验证欺凌冻结起始状态计数：致死后仍抽、命中后新浸油不反哺零计数，且十张手牌上限阻止抽牌。</summary>
    [Test]
    public void Bully_FrozenCountSurvivesLethalIgnoresPostHitOilAndHonorsHandLimit()
    {
        using (var lethal = new MachineGunnerStarterScenario(
                   new[] { 3278, 3201 },
                   initialHandCount: 2,
                   firstEnemyHealth: 5,
                   enemyDamage: 0,
                   initialEnergy: 0))
        {
            lethal.StartBattle();
            CardInstanceId bullyId = lethal.Zones.Hand
                .Single(cardId => lethal.Zones.Cards[cardId].TemplateId == 3278);
            lethal.Zones.DiscardFromHand(lethal.Zones.Hand.Single(id => id != bullyId));
            lethal.Session.MachineGunnerRuntime.CombatState.Add(
                lethal.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Burn,
                5);

            BattleCommandExecutionResult result = lethal.Play(3278, lethal.FirstEnemy.Id);

            BattleDamageAppliedSettlement damage = FindSettlement<BattleDamageAppliedSettlement>(result);
            Assert.That(damage.WasFatal, Is.True);
            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Count(item =>
                item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand),
                Is.EqualTo(1));
        }

        using (var postHitOil = new MachineGunnerStarterScenario(
                   new[] { 3253, 3278, 3201 },
                   initialHandCount: 3,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 1))
        {
            postHitOil.StartBattle();
            CardInstanceId fillerId = postHitOil.Zones.Hand.Single(cardId =>
                postHitOil.Zones.Cards[cardId].TemplateId == 3201);
            postHitOil.Zones.DiscardFromHand(fillerId);
            postHitOil.Play(3253, targetId: null);

            BattleCommandExecutionResult result = postHitOil.Play(3278, postHitOil.FirstEnemy.Id);

            Assert.That(postHitOil.Session.MachineGunnerRuntime.CombatState.Get(
                postHitOil.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Oil), Is.EqualTo(2));
            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
                item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand),
                Is.False);
        }

        using (var fullHand = new MachineGunnerStarterScenario(
                   Enumerable.Repeat(3278, 11).ToArray(),
                   initialHandCount: 10,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 0))
        {
            fullHand.StartBattle();
            fullHand.Session.MachineGunnerRuntime.CombatState.Add(
                fullHand.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Burn,
                3);
            fullHand.Session.MachineGunnerRuntime.CombatState.Add(
                fullHand.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Oil,
                4);
            uint shuffleBefore = fullHand.Zones.ShuffleRandomState;

            BattleCommandExecutionResult result = fullHand.Play(3278, fullHand.FirstEnemy.Id);

            Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
                item.FromZone == BattleCardZone.DrawPile && item.ToZone == BattleCardZone.Hand),
                Is.False);
            Assert.That(fullHand.Zones.Hand, Has.Count.EqualTo(9));
            Assert.That(fullHand.Zones.DrawPile, Has.Count.EqualTo(1));
            Assert.That(fullHand.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
        }
    }

    /// <summary>验证欺凌注册为零弹药无标签显式敌人普通攻击，且缺失目标会在任何战斗、资源、随机流与卡区写入前失败。</summary>
    [Test]
    public void Bully_RegistryAndMissingTargetFailureKeepFrozenContractAndZeroWrites()
    {
        Assert.That(MachineGunnerCardProgramRegistry.TryGet(
            cfg.battle.MachineGunnerProgramId.Bully,
            out MachineGunnerCardProgram program), Is.True);
        Assert.That(program.TargetInputMode, Is.EqualTo(MachineGunnerTargetInputMode.ExplicitEnemy));
        Assert.That(program.BaseAmmoCost, Is.Zero);
        Assert.That(program.BaseShootHitCount, Is.Zero);
        Assert.That(program.IsAttack, Is.True);
        Assert.That(program.Tags, Is.EqualTo(MachineGunnerCardTag.None));
        Assert.That(program.IsShootCategory, Is.False);
        Assert.That(program.ReceivesStimBonus, Is.False);
        Assert.That(program.ReceivesIncendiaryAmmo, Is.False);
        Assert.That(program.Operations.Select(operation =>
                $"{operation.Kind}/{operation.Value}/{operation.TargetScope}"),
            Is.EqualTo(new[]
            {
                "Damage/6/ProgramTargets",
                "DrawCardsByActiveStatusKinds/1/ProgramTargets",
            }));

        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3278 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 0);
        scenario.StartBattle();
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        uint shuffleBefore = scenario.Zones.ShuffleRandomState;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        int resultCountBefore = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
            scenario.Submit(3278, targetId: null));

        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.TargetRequired));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(100));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(cardRandomBefore));
    }

    /// <summary>验证幻彩射击冻结目标起始状态种类，并让每段原生伤害与兴奋剂复制依次触发燃烧弹药和便携帮手。</summary>
    [Test]
    public void PrismaticShot_TwoFrozenStatuses_InterleavesSixAndNineStimCopiesWithIncendiaryAndHelper()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3205, 3210, 3267, 3279 },
            initialHandCount: 4,
            firstEnemyHealth: 200,
            enemyDamage: 0,
            initialEnergy: 3,
            initialAmmo: 4,
            ammoMaximum: 5);
        scenario.StartBattle();
        scenario.Play(3205, targetId: null);
        scenario.Play(3210, targetId: null);
        scenario.Play(3267, targetId: null);
        scenario.FirstEnemy.ApplyStrengthChange(3);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Weakness, 7);
        CardInstanceId prismaticShotId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3279);
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3279, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
        Assert.That(terminal.Settlements, Has.Count.EqualTo(21));
        Assert.That(
            terminal.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 21)));

        var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
        var ammo = terminal.Settlements[1] as BattleAmmoSpentSettlement;
        var departure = terminal.Settlements[20] as BattleCardMovedSettlement;
        Assert.That(energy, Is.Not.Null);
        Assert.That(
            (energy.Order, energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
            Is.EqualTo((0, 0, 0, 0)));
        Assert.That(ammo, Is.Not.Null);
        Assert.That(
            (ammo.Order, ammo.AmmoBefore, ammo.AmmoAfter, ammo.Amount),
            Is.EqualTo((1, 4, 0, 4)));

        BattleDamageAppliedSettlement[] damages = terminal.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] burns = terminal.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn)
            .ToArray();
        Assert.That(
            damages.Select(item => item.Order),
            Is.EqualTo(new[] { 2, 4, 5, 7, 8, 10, 11, 13, 14, 16, 17, 19 }));
        Assert.That(
            damages.Select(item => item.AttackValue),
            Is.EqualTo(new[] { 6, 1, 6, 1, 9, 1, 9, 1, 9, 1, 9, 1 }));
        Assert.That(
            damages.Select(item => item.HealthBefore),
            Is.EqualTo(new[] { 200, 194, 193, 187, 186, 177, 176, 167, 166, 157, 156, 147 }));
        Assert.That(
            damages.Select(item => item.HealthAfter),
            Is.EqualTo(new[] { 194, 193, 187, 186, 177, 176, 167, 166, 157, 156, 147, 146 }));
        Assert.That(
            damages.Select(item => item.TargetId),
            Is.All.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(
            burns.Select(item => item.Order),
            Is.EqualTo(new[] { 3, 6, 9, 12, 15, 18 }));
        Assert.That(
            burns.Select(item => item.ValueBefore),
            Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        Assert.That(
            burns.Select(item => item.ValueAfter),
            Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));

        Assert.That(departure, Is.Not.Null);
        Assert.That(
            (departure.Order, departure.CardId, departure.FromZone, departure.ToZone),
            Is.EqualTo((20, prismaticShotId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(146));
        Assert.That(scenario.FirstEnemy.CurrentStrength, Is.EqualTo(3));
        Assert.That(
            state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Weakness),
            Is.EqualTo(7));
        Assert.That(
            state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn),
            Is.EqualTo(6));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.Zero);
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Does.Contain(prismaticShotId));
    }

    /// <summary>验证通用中毒会被幻彩射击冻结为一种目标状态，并让兴奋剂复制后的弹药费用与四段伤害完整结算。</summary>
    [Test]
    public void PrismaticShot_GenericPoisonCountsAsFrozenStatusAndRaisesStimAmmoCost()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3205, 3279 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 1,
            initialAmmo: 3,
            ammoMaximum: 3);
        scenario.StartBattle();

        BattleCommandExecutionResult stim = scenario.Play(3205, targetId: null);
        Assert.That(stim.Succeeded, Is.True);
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));

        var poisonApplication = new BattlePoisonApplication(scenario.Session.Combatants);
        BattlePoisonApplicationPreparationResult poisonPreparation =
            poisonApplication.PrepareApply(
                scenario.Player.Id,
                scenario.FirstEnemy.Id,
                amount: 4);
        Assert.That(poisonPreparation.Succeeded, Is.True);
        Assert.That(poisonApplication.ValidatePrepared(poisonPreparation.Plan), Is.True);
        IReadOnlyList<BattleSettlementRecord> poisonSettlements =
            poisonApplication.CommitPrepared(poisonPreparation.Plan, startingOrder: 0);
        Assert.That(poisonSettlements, Has.Count.EqualTo(1));
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(4));

        BattleCommandExecutionResult result = scenario.Play(3279, scenario.FirstEnemy.Id);
        BattleAmmoSpentSettlement ammo = FindSettlement<BattleAmmoSpentSettlement>(result);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(
            (ammo.AmmoBefore, ammo.AmmoAfter, ammo.Amount),
            Is.EqualTo((3, 0, 3)));
        BattleDamageAppliedSettlement[] sourceDamages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Where(damage => damage.SourceId == scenario.Player.Id)
            .ToArray();
        Assert.That(
            sourceDamages.Select(damage => damage.AttackValue),
            Is.EqualTo(new[] { 6, 6, 9, 9 }));
        Assert.That(
            sourceDamages.Select(damage => damage.HealthBefore),
            Is.EqualTo(new[] { 100, 94, 88, 79 }));
        Assert.That(
            sourceDamages.Select(damage => damage.HealthAfter),
            Is.EqualTo(new[] { 94, 88, 79, 70 }));
        Assert.That(
            sourceDamages.Select(damage => damage.Order),
            Is.EqualTo(new[] { 2, 3, 4, 5 }));
        Assert.That(result.Settlements, Has.Count.EqualTo(7));
        Assert.That(
            result.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, 7)));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(70));
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(4));
    }

    /// <summary>验证幻彩射击在目标两种起始状态且兴奋剂生效时必须全额预付四点弹药；仅三点时经公共 Queue 原子失败且所有权威事实零写入。</summary>
    [Test]
    public void PrismaticShot_TwoStartingStatusesWithStimAndThreeAmmo_QueueFailsBeforeAnyWrites()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3205, 3210, 3267, 3279 },
            initialHandCount: 4,
            firstEnemyHealth: 200,
            enemyDamage: 0,
            initialEnergy: 3,
            initialAmmo: 3,
            ammoMaximum: 5);
        scenario.StartBattle();
        scenario.Play(3205, targetId: null);
        scenario.Play(3210, targetId: null);
        scenario.Play(3267, targetId: null);
        scenario.FirstEnemy.ApplyStrengthChange(3);
        scenario.FirstEnemy.ApplyBlockGain(13);
        MachineGunnerBattleRuntime runtime = scenario.Session.MachineGunnerRuntime;
        MachineGunnerCombatState state = runtime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Weakness, 7);

        BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
        PlayerTurnData playerTurnBefore = turnBefore.Players[scenario.Player.Id];
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        CardInstanceId[] drawPileBefore = scenario.Zones.DrawPile.ToArray();
        CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
        CardInstanceId[] discardPileBefore = scenario.Zones.DiscardPile.ToArray();
        CardInstanceId[] exhaustPileBefore = scenario.Zones.ExhaustPile.ToArray();
        CardInstanceId[] powerPileBefore = scenario.Zones.PowerPile.ToArray();
        Dictionary<CardInstanceId, CardInstanceData> cardsBefore = scenario.Zones.Cards
            .ToDictionary(item => item.Key, item => item.Value);
        int healthBefore = scenario.FirstEnemy.CurrentHealth;
        int blockBefore = scenario.FirstEnemy.CurrentBlock;
        int strengthBefore = scenario.FirstEnemy.CurrentStrength;
        int vulnerableBefore = scenario.FirstEnemy.CurrentVulnerable;
        MachineGunnerCombatantStatus[] privateStatuses =
            (MachineGunnerCombatantStatus[])Enum.GetValues(
                typeof(MachineGunnerCombatantStatus));
        Dictionary<MachineGunnerCombatantStatus, int> playerStatusesBefore = privateStatuses
            .ToDictionary(status => status, status => state.Get(scenario.Player.Id, status));
        Dictionary<MachineGunnerCombatantStatus, int> targetStatusesBefore = privateStatuses
            .ToDictionary(status => status, status => state.Get(scenario.FirstEnemy.Id, status));
        MachineGunnerPowerKind[] powerKinds =
            (MachineGunnerPowerKind[])Enum.GetValues(typeof(MachineGunnerPowerKind));
        Dictionary<MachineGunnerPowerKind, int> powerStacksBefore = powerKinds
            .ToDictionary(powerKind => powerKind, runtime.GetPowerStack);
        int stimTurnsBefore = runtime.StimTurns;
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        uint machineGunnerRandomBefore = runtime.CardRandomState;
        uint cardTargetRandomBefore = scenario.Queue.CardTargetRandomState;
        int resultCountBefore = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(
            3279,
            scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Results.Skip(resultCountBefore), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(playerTurnBefore.Energy));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(playerTurnBefore.Ammo));
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(scenario.Zones.DrawPile, Is.EqualTo(drawPileBefore));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardPileBefore));
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(exhaustPileBefore));
        Assert.That(scenario.Zones.PowerPile, Is.EqualTo(powerPileBefore));
        Assert.That(scenario.Zones.Cards, Has.Count.EqualTo(cardsBefore.Count));
        foreach (KeyValuePair<CardInstanceId, CardInstanceData> card in cardsBefore)
            Assert.That(scenario.Zones.Cards[card.Key], Is.SameAs(card.Value));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(healthBefore));
        Assert.That(scenario.FirstEnemy.CurrentBlock, Is.EqualTo(blockBefore));
        Assert.That(scenario.FirstEnemy.CurrentStrength, Is.EqualTo(strengthBefore));
        Assert.That(scenario.FirstEnemy.CurrentVulnerable, Is.EqualTo(vulnerableBefore));
        foreach (MachineGunnerCombatantStatus status in privateStatuses)
        {
            Assert.That(state.Get(scenario.Player.Id, status),
                Is.EqualTo(playerStatusesBefore[status]));
            Assert.That(state.Get(scenario.FirstEnemy.Id, status),
                Is.EqualTo(targetStatusesBefore[status]));
        }
        foreach (MachineGunnerPowerKind powerKind in powerKinds)
            Assert.That(runtime.GetPowerStack(powerKind), Is.EqualTo(powerStacksBefore[powerKind]));
        Assert.That(runtime.StimTurns, Is.EqualTo(stimTurnsBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(runtime.CardRandomState, Is.EqualTo(machineGunnerRandomBefore));
        Assert.That(scenario.Queue.CardTargetRandomState, Is.EqualTo(cardTargetRandomBefore));
    }

    /// <summary>确认 Hero 1002 会创建职业运行时，并沿用共享默认抽五张规则与首回合资源档案。</summary>
    [Test]
    public void StartBattle_MachineGunnerProfile_CreatesRuntimeAndUsesSharedDefaultHandCount()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3201, 3201, 3201, 3201, 3202, 3203, 3203, 3203, 3203, 3203, 3204, 3205 },
            initialHandCount: 5);

        scenario.StartBattle();

        Assert.That(scenario.Session.MachineGunnerRuntime, Is.Not.Null);
        Assert.That(scenario.Player.MaxHealth, Is.EqualTo(70));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(5));
        PlayerTurnData turn = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        Assert.That(turn.Energy, Is.EqualTo(3));
        Assert.That(turn.EnergyMaximum, Is.EqualTo(5));
        Assert.That(turn.Ammo, Is.EqualTo(5));
        Assert.That(turn.AmmoMaximum, Is.EqualTo(5));
    }

    /// <summary>确认射击、自动最近肘击、自目标格挡和装填均经唯一队列完成正确结算。</summary>
    [Test]
    public void StarterPrograms_PlayThroughQueue_ResolveTargetsResourcesAndDestinations()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3201, 3202, 3203, 3204 },
            initialHandCount: 4);
        scenario.StartBattle();

        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);
        BattleCommandExecutionResult elbow = scenario.Play(3202, targetId: null);
        BattleCommandExecutionResult block = scenario.Play(3203, targetId: null);
        BattleCommandExecutionResult reload = scenario.Play(3204, targetId: null);

        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(8));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(5));
        PlayerTurnData turn = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        Assert.That(turn.Energy, Is.Zero);
        Assert.That(turn.Ammo, Is.EqualTo(5));
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(4));

        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(shoot).Amount, Is.EqualTo(1));
        Assert.That(FindSettlement<BattleDamageAppliedSettlement>(shoot).TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(FindSettlement<BattleDamageAppliedSettlement>(elbow).TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(block).Amount, Is.EqualTo(5));
        Assert.That(FindSettlement<BattleAmmoRefilledSettlement>(reload).AmmoAfter, Is.EqualTo(5));
    }

    /// <summary>确认兴奋剂抽两张、追加真实弹药和额外射击，并在下一玩家回合开始失效。</summary>
    [Test]
    public void Stim_ExtraShootConsumesTwoAmmoAndExpiresAtNextPlayerRound()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3205, 3205, 3205, 3205, 3205, 3205, 3201 },
            initialHandCount: 5);
        scenario.StartBattle();

        BattleCommandExecutionResult stim = scenario.Play(3205, targetId: null);
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(6));
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(stim.Settlements.OfType<BattleCardMovedSettlement>().Count(), Is.EqualTo(3));

        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(3));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(8));
        Assert.That(shoot.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(2));
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(shoot).Amount, Is.EqualTo(2));

        scenario.EndPlayerAction();

        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.Zero);
    }

    /// <summary>确认敌方通用 Effect 会读取机枪兵私有虚弱和烟雾，并仅在伤害穿透生命后消耗一层护甲。</summary>
    [Test]
    public void EnemyEffect_UsesMachineGunnerPipelineAndArmorLifecycle()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3203 },
            initialHandCount: 1,
            enemyDamage: 8);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Weakness, 1);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke, 1);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Smoke, 8);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 1);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Armor, 2);
        scenario.Player.ApplyBlockGain(1);

        scenario.EndPlayerAction();

        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(67));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(1));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Armor), Is.EqualTo(1));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Weakness), Is.Zero);
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.Zero);
    }

    /// <summary>验证最后一名玩家结束行动后，燃烧在通用状态时机之后按 Encounter 顺序结算，且只经过 Debuff 的格挡与生命管线。</summary>
    [Test]
    public void EndPlayerAction_ResolvesBurnAfterGenericStatusAndUsesBlock()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new int[0],
            initialHandCount: 0,
            enemyDamage: 0);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        scenario.Player.ApplyVulnerableGain(1);
        scenario.Player.ApplyBlockGain(4);
        scenario.FirstEnemy.ApplyBlockGain(2);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 4);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 5);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        BattleDamageAppliedSettlement[] burnSettlements = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(scenario.Player.CurrentVulnerable, Is.Zero);
        Assert.That(burnSettlements, Has.Length.EqualTo(2));
        Assert.That(burnSettlements[0].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(burnSettlements[0].EffectId, Is.Null);
        Assert.That(burnSettlements[0].AttackValue, Is.EqualTo(4));
        Assert.That(burnSettlements[0].BlockBefore, Is.EqualTo(2));
        Assert.That(burnSettlements[0].BlockAfter, Is.Zero);
        Assert.That(burnSettlements[0].HealthBefore, Is.EqualTo(20));
        Assert.That(burnSettlements[0].HealthAfter, Is.EqualTo(18));
        Assert.That(burnSettlements[1].TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(burnSettlements[1].EffectId, Is.Null);
        Assert.That(burnSettlements[1].AttackValue, Is.EqualTo(5));
        Assert.That(burnSettlements[1].BlockBefore, Is.EqualTo(4));
        Assert.That(burnSettlements[1].BlockAfter, Is.Zero);
        Assert.That(burnSettlements[1].HealthBefore, Is.EqualTo(70));
        Assert.That(burnSettlements[1].HealthAfter, Is.EqualTo(69));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(4));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(5));
        Assert.That(result.Settlements.Select(item => item.Order), Is.EqualTo(
            Enumerable.Range(0, result.Settlements.Count)));
        Assert.That(result.Settlements.Last(), Is.TypeOf<BattlePhaseChangedSettlement>());
    }

    /// <summary>验证敌方燃烧依 Encounter 顺序击杀最后敌人时立即进入胜利，不再错误结算玩家自身燃烧或创建敌方续接。</summary>
    [Test]
    public void EndPlayerAction_BurnKillsLastEnemies_SkipsPlayerBurnAndEndsBattle()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new int[0],
            initialHandCount: 0,
            firstEnemyHealth: 5,
            secondEnemyHealth: 5,
            enemyDamage: 0);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 70);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        BattleDamageAppliedSettlement[] burnSettlements = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(burnSettlements.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(70));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(70));
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
        Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(result.Settlements.Last(), Is.TypeOf<BattlePhaseChangedSettlement>());
    }

    /// <summary>验证烈火烹油会先按 Encounter 顺序增长所有敌方燃烧，再统一结算燃烧伤害，且不会消耗任何浸油或影响玩家自身燃烧。</summary>
    [Test]
    public void BurningOil_GrowsEnemyBurnsBeforeDamageWithoutConsumingOil()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3254 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        scenario.Play(3254, targetId: null);

        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.Zero);

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 3);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 2);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn, 1);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil, 4);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Oil, 7);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        MachineGunnerPrivateStatusChangedSettlement[] burnGrowths = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn)
            .ToArray();
        BattleDamageAppliedSettlement[] burnDamages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        string[] orderedKinds = result.Settlements
            .Where(item => item is BattleDamageAppliedSettlement ||
                item is MachineGunnerPrivateStatusChangedSettlement status &&
                status.Status == MachineGunnerCombatantStatus.Burn)
            .Select(item => item is BattleDamageAppliedSettlement ? "Damage" : "Burn")
            .ToArray();

        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.BurningOil),
            Is.EqualTo(1));
        Assert.That(burnGrowths.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(burnGrowths.Select(item => item.ValueBefore), Is.EqualTo(new[] { 3, 1 }));
        Assert.That(burnGrowths.Select(item => item.ValueAfter), Is.EqualTo(new[] { 6, 6 }));
        Assert.That(burnDamages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.Player.Id,
        }));
        Assert.That(burnDamages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 6, 5 }));
        Assert.That(orderedKinds, Is.EqualTo(new[] { "Burn", "Burn", "Damage", "Damage", "Damage" }));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(2));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(4));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(5));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(7));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(94));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(94));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(65));
    }

    /// <summary>验证多张烈火烹油只启用一次增长规则，且没有燃烧的敌人不会被凭空写入燃烧。</summary>
    [Test]
    public void BurningOil_DuplicatePowersOnlyEnableOneGrowthAndSkipUnburnedEnemy()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3254, 3254 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 4);
        scenario.StartBattle();
        scenario.Play(3254, targetId: null);
        scenario.Play(3254, targetId: null);

        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(2));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.Zero);

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 3);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 2);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil, 4);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 5);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        MachineGunnerPrivateStatusChangedSettlement[] burnGrowths = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn)
            .ToArray();
        BattleDamageAppliedSettlement[] burnDamages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.BurningOil),
            Is.EqualTo(2));
        Assert.That(burnGrowths, Has.Length.EqualTo(1));
        Assert.That(burnGrowths[0].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(burnGrowths[0].ValueBefore, Is.EqualTo(3));
        Assert.That(burnGrowths[0].ValueAfter, Is.EqualTo(6));
        Assert.That(burnDamages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.Player.Id,
        }));
        Assert.That(burnDamages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 5 }));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(4));
    }

    /// <summary>验证烈火烹油增长后的敌方燃烧击杀最后敌人时，继续保持跳过玩家燃烧并结束战斗的既有收口。</summary>
    [Test]
    public void BurningOil_LastEnemyBurnKills_SkipsPlayerBurnAndEndsBattle()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3254 },
            initialHandCount: 1,
            firstEnemyHealth: 6,
            secondEnemyHealth: 6,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        scenario.Play(3254, targetId: null);

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 4);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 1);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 70);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Oil, 7);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        MachineGunnerPrivateStatusChangedSettlement[] burnGrowths = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn)
            .ToArray();
        BattleDamageAppliedSettlement[] burnDamages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(burnGrowths.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(burnGrowths.Select(item => item.ValueAfter), Is.EqualTo(new[] { 6, 6 }));
        Assert.That(burnDamages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(1));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(70));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(70));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(7));
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
        Assert.That(result.Settlements.Last(), Is.TypeOf<BattlePhaseChangedSettlement>());
    }

    /// <summary>验证不充分爆燃以初始燃烧敌人为来源命中全体敌人（包含自身），再在所有伤害后转烟并把卡移入消耗区。</summary>
    [Test]
    public void IncompleteCombustion_UsesBurnersForCrossDamageThenConvertsLivingEnemiesAndExhausts()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3222 },
            initialHandCount: 1,
            firstEnemyHealth: 20,
            secondEnemyHealth: 20,
            enemyDamage: 0,
            initialEnergy: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        scenario.FirstEnemy.ApplyBlockGain(2);
        scenario.SecondEnemy.ApplyBlockGain(3);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 4);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke, 1);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 5);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn, 6);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Smoke, 2);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil, 7);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 9);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 2);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Oil, 11);

        BattleCommandExecutionResult result = scenario.Play(3222, targetId: null);
        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] conversions = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(energy.EnergyBefore, Is.EqualTo(3));
        Assert.That(energy.EnergyAfter, Is.Zero);
        Assert.That(damages.Select(item => item.SourceId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.TargetId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 4, 4, 6, 6 }));
        Assert.That(damages.Select(item => item.BlockBefore), Is.EqualTo(new[] { 2, 3, 0, 0 }));
        Assert.That(damages.Select(item => item.BlockAfter), Is.EqualTo(new[] { 0, 0, 0, 0 }));
        Assert.That(damages.Select(item => item.HealthBefore), Is.EqualTo(new[] { 20, 20, 18, 19 }));
        Assert.That(damages.Select(item => item.HealthAfter), Is.EqualTo(new[] { 18, 19, 12, 13 }));
        Assert.That(conversions.Select(item => item.TargetId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(conversions.Select(item => item.Status), Is.EqualTo(new[]
        {
            MachineGunnerCombatantStatus.Smoke,
            MachineGunnerCombatantStatus.Burn,
            MachineGunnerCombatantStatus.Smoke,
            MachineGunnerCombatantStatus.Burn,
        }));
        Assert.That(conversions.Select(item => item.SourceId.Value), Is.EqualTo(new[]
        {
            scenario.Player.Id,
            scenario.Player.Id,
            scenario.Player.Id,
            scenario.Player.Id,
        }));
        Assert.That(conversions.Select(item => item.ValueBefore), Is.EqualTo(new[] { 1, 4, 2, 6 }));
        Assert.That(conversions.Select(item => item.ValueAfter), Is.EqualTo(new[] { 5, 0, 8, 0 }));
        Assert.That(destination.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(destination.Order, Is.EqualTo(9));
        Assert.That(result.Settlements.Select(item => item.Order), Is.EqualTo(
            Enumerable.Range(0, result.Settlements.Count)));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(12));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(13));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(5));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(5));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(8));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(7));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(70));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(2));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(9));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(11));
        Assert.That(scenario.Zones.ExhaustPile, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Zones.PowerPile, Is.Empty);
    }

    /// <summary>验证不充分爆燃固定效果开始时的燃烧来源，即使其中一名来源先死亡仍会继续伤害剩余目标，但死亡目标不会转烟。</summary>
    [Test]
    public void IncompleteCombustion_CapturedDeadBurnerStillDamagesLivingTargetsWithoutConvertingDeadEnemy()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3222 },
            initialHandCount: 1,
            firstEnemyHealth: 20,
            secondEnemyHealth: 6,
            enemyDamage: 0,
            initialEnergy: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 6);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke, 1);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Smoke, 2);

        BattleCommandExecutionResult result = scenario.Play(3222, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] conversions = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();

        Assert.That(damages.Select(item => item.SourceId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.TargetId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.FirstEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 6, 5 }));
        Assert.That(damages.Select(item => item.WasFatal), Is.EqualTo(new[] { false, true, false }));
        Assert.That(conversions.Select(item => item.TargetId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.FirstEnemy.Id,
        }));
        Assert.That(conversions.Select(item => item.Status), Is.EqualTo(new[]
        {
            MachineGunnerCombatantStatus.Smoke,
            MachineGunnerCombatantStatus.Burn,
        }));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(9));
        Assert.That(scenario.SecondEnemy.IsAlive, Is.False);
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(7));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(2));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(5));
        Assert.That(scenario.Zones.ExhaustPile, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
    }

    /// <summary>验证没有燃烧来源时不充分爆燃仍支付费用并消耗自身，但不伪造伤害、状态写入或浸油变化。</summary>
    [Test]
    public void IncompleteCombustion_WithoutBurners_OnlyPaysEnergyAndExhausts()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3222 },
            initialHandCount: 1,
            enemyDamage: 0,
            initialEnergy: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 2);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil, 7);

        BattleCommandExecutionResult result = scenario.Play(3222, targetId: null);
        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(energy.Amount, Is.EqualTo(3));
        Assert.That(result.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(result.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
        Assert.That(destination.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(2));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(7));
        Assert.That(scenario.Zones.ExhaustPile, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
    }

    /// <summary>验证不充分爆燃击杀全体敌人时仍先把自身移入消耗区，再由既有控制器统一进入战斗结束。</summary>
    [Test]
    public void IncompleteCombustion_KillsAllEnemies_ExhaustsBeforeBattleEnds()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3222 },
            initialHandCount: 1,
            firstEnemyHealth: 5,
            secondEnemyHealth: 5,
            enemyDamage: 0,
            initialEnergy: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn, 1);

        BattleCommandExecutionResult result = scenario.Play(3222, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);
        BattlePhaseChangedSettlement phase = FindSettlement<BattlePhaseChangedSettlement>(result);

        Assert.That(damages.Select(item => item.SourceId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.FirstEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.TargetId.Value), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(result.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
        Assert.That(destination.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(destination.Order, Is.LessThan(phase.Order));
        Assert.That(phase.PhaseAfter, Is.EqualTo(BattleTurnPhase.BattleEnded));
        Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
        Assert.That(scenario.SecondEnemy.IsAlive, Is.False);
        Assert.That(scenario.Zones.ExhaustPile, Has.Count.EqualTo(1));
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
        Assert.That(result.Settlements.Last(), Is.TypeOf<BattlePhaseChangedSettlement>());
    }

    /// <summary>验证光学迷彩成功支付后为玩家增加两层隐身，并按作者表进入弃牌堆。</summary>
    [Test]
    public void OpticalCamo_AppliesTwoInvisibleAndDiscards()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3249 },
            initialHandCount: 1,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3249, targetId: null);
        MachineGunnerPrivateStatusChangedSettlement invisible = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.Invisible);
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(invisible.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(invisible.ValueBefore, Is.Zero);
        Assert.That(invisible.ValueAfter, Is.EqualTo(2));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Invisible),
            Is.EqualTo(2));
        Assert.That(destination.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证普通肘击和普通射击均消耗隐身，而声明保留隐身的狙击与射击加狙击双词条均不消耗。</summary>
    [Test]
    public void OpticalCamo_OrdinaryAttacksConsumeButDeclaredPreservingAttacksDoNot()
    {
        using (var normalScenario = new MachineGunnerStarterScenario(
                   new[] { 3249, 3202, 3201 },
                   initialHandCount: 3,
                   firstEnemyHealth: 50,
                   initialEnergy: 3,
                   initialAmmo: 1))
        {
            normalScenario.StartBattle();
            normalScenario.Play(3249, targetId: null);
            normalScenario.Play(3202, targetId: null);
            normalScenario.Play(3201, normalScenario.FirstEnemy.Id);

            Assert.That(
                normalScenario.Session.MachineGunnerRuntime.CombatState.Get(
                    normalScenario.Player.Id,
                    MachineGunnerCombatantStatus.Invisible),
                Is.Zero);
        }

        using (var spikeScenario = new MachineGunnerStarterScenario(
                   new[] { 3249, 3248 },
                   initialHandCount: 2,
                   firstEnemyHealth: 50,
                   initialEnergy: 2,
                   initialAmmo: 1))
        {
            spikeScenario.StartBattle();
            spikeScenario.Play(3249, targetId: null);
            MachineGunnerCombatState state = spikeScenario.Session.MachineGunnerRuntime.CombatState;
            state.Add(spikeScenario.Player.Id, MachineGunnerCombatantStatus.FirePower, 2);
            BattleCommandExecutionResult spike = spikeScenario.Play(3248, spikeScenario.FirstEnemy.Id);

            Assert.That(
                spike.Settlements.OfType<BattleDamageAppliedSettlement>().Single().AttackValue,
                Is.EqualTo(6));
            Assert.That(
                state.Get(spikeScenario.Player.Id, MachineGunnerCombatantStatus.Invisible),
                Is.EqualTo(2));
        }

        using (var sniperScenario = new MachineGunnerStarterScenario(
                   new[] { 3249, 3247 },
                   initialHandCount: 2,
                   firstEnemyHealth: 50,
                   secondEnemyHealth: 50,
                   initialEnergy: 3,
                   initialAmmo: 2))
        {
            sniperScenario.StartBattle();
            sniperScenario.Play(3249, targetId: null);
            BattleCommandExecutionResult sniper = sniperScenario.Play(3247, targetId: null);
            BattleDamageAppliedSettlement damage = sniper.Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .Single();

            Assert.That(damage.TargetId, Is.EqualTo(sniperScenario.SecondEnemy.Id));
            Assert.That(damage.AttackValue, Is.EqualTo(26));
            Assert.That(
                sniperScenario.Session.MachineGunnerRuntime.CombatState.Get(
                    sniperScenario.Player.Id,
                    MachineGunnerCombatantStatus.Invisible),
                Is.EqualTo(2));
        }
    }

    /// <summary>验证玩家行动结束会先消耗最后一层隐身，再让敌方攻击按无隐身的完整伤害结算。</summary>
    [Test]
    public void OpticalCamo_PlayerActionEndConsumesRemainingInvisibleBeforeIncomingDamage()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3249, 3202 },
            initialHandCount: 2,
            initialEnergy: 3,
            enemyDamage: 10);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Smoke, 10);
        scenario.Play(3249, targetId: null);
        scenario.Play(3202, targetId: null);
        Assert.That(
            state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Invisible),
            Is.EqualTo(1));
        scenario.EndPlayerAction();

        Assert.That(
            state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Invisible),
            Is.Zero);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(60));
    }

    /// <summary>验证弹药不足导致的普通攻击在队首零写入失败，不会消耗已经由光学迷彩获得的隐身层数。</summary>
    [Test]
    public void OpticalCamo_FailedNonSniperAttackDoesNotConsumeInvisible()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3249, 3201 },
            initialHandCount: 2,
            initialEnergy: 2,
            initialAmmo: 0);
        scenario.StartBattle();
        scenario.Play(3249, targetId: null);

        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = scenario.Submit(3201, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Invisible),
            Is.EqualTo(2));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
    }

    /// <summary>验证全息诱饵成功支付后为玩家增加一层缓冲，并按基础作者表从手牌进入消耗堆。</summary>
    [Test]
    public void HoloDecoy_AppliesBufferAndExhausts()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3259 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3259, targetId: null);
        MachineGunnerPrivateStatusChangedSettlement buffer = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.Buffer);
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.Zero);
        Assert.That(buffer.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(buffer.ValueBefore, Is.Zero);
        Assert.That(buffer.ValueAfter, Is.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Buffer),
            Is.EqualTo(1));
        Assert.That(destination.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Zones.ExhaustPile, Has.Count.EqualTo(1));
        scenario.EndPlayerAction();
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Buffer),
            Is.EqualTo(1));
    }

    /// <summary>验证战地手术经公共队列只施加恢复与束缚、不立即治疗，并把自身移入消耗区。</summary>
    [Test]
    public void FieldSurgery_Play_AppliesRegenerationAndShackleWithoutImmediateHealAndExhausts()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3231 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        BattleEffectFormulaResult setupDamage = BattleEffectFormula.Calculate(
            new BattleEffectFormulaContext(
                BattleEffectOperationType.DealDamage,
                configuredValue: 10,
                sourceStrength: 0,
                target: new BattleEffectTargetSnapshot(
                    scenario.Player.CurrentHealth,
                    scenario.Player.CurrentBlock,
                    scenario.Player.CurrentVulnerable)));
        scenario.Player.ApplyDamageOutcome(setupDamage.DamageOutcome.Value);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(60));
        CardInstanceId cardId = scenario.Zones.Hand.Single();
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3231, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Settlements, Has.Count.EqualTo(4));
        Assert.That(terminal.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 4)));
        var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
        var regeneration = terminal.Settlements[1] as MachineGunnerPrivateStatusChangedSettlement;
        var shackle = terminal.Settlements[2] as MachineGunnerPrivateStatusChangedSettlement;
        var destination = terminal.Settlements[3] as BattleCardMovedSettlement;
        Assert.That(energy, Is.Not.Null);
        Assert.That(energy.Amount, Is.EqualTo(1));
        Assert.That(regeneration, Is.Not.Null);
        Assert.That(regeneration.Status.ToString(), Is.EqualTo("Regeneration"));
        Assert.That((regeneration.ValueBefore, regeneration.ValueAfter), Is.EqualTo((0, 5)));
        Assert.That(regeneration.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That(regeneration.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(shackle, Is.Not.Null);
        Assert.That(shackle.Status, Is.EqualTo(MachineGunnerCombatantStatus.Shackle));
        Assert.That((shackle.ValueBefore, shackle.ValueAfter), Is.EqualTo((0, 1)));
        Assert.That(shackle.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That(shackle.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(destination, Is.Not.Null);
        Assert.That(
            (destination.CardId, destination.FromZone, destination.ToZone),
            Is.EqualTo((cardId, BattleCardZone.Hand, BattleCardZone.ExhaustPile)));
        Assert.That(terminal.Settlements.OfType<BattleHealthRestoredSettlement>(), Is.Empty);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(60));
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { cardId }));
    }

    /// <summary>验证战地手术的束缚预演溢出会在再生、能量与卡区首次写入前原子失败。</summary>
    [Test]
    public void FieldSurgery_ShackleOverflowFailsBeforeRegenerationEnergyOrCardZoneWrites()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3231 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Session.MachineGunnerRuntime.CombatState.Set(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Shackle,
            int.MaxValue);
        BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        int healthBefore = scenario.Player.CurrentHealth;
        CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
        Dictionary<CardInstanceId, CardInstanceData> cardsBefore = scenario.Zones.Cards
            .ToDictionary(item => item.Key, item => item.Value);
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        int resultCountBefore = scenario.Results.Count;
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Regeneration),
            Is.Zero);
        int turnPublicationCount = 0;
        int layoutPublicationCount = 0;
        int healthPublicationCount = 0;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission;
        using (scenario.Queue.Turn.Skip(1).Subscribe(_ => turnPublicationCount++))
        using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
        using (scenario.Player.Health.Skip(1).Subscribe(_ => healthPublicationCount++))
            submission = scenario.Submit(3231, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionFailed));
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.EffectValueOverflow));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(1));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(healthBefore));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Regeneration),
            Is.Zero);
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Shackle),
            Is.EqualTo(int.MaxValue));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore));
        Assert.That(scenario.Zones.Cards, Has.Count.EqualTo(cardsBefore.Count));
        foreach (KeyValuePair<CardInstanceId, CardInstanceData> card in cardsBefore)
            Assert.That(scenario.Zones.Cards[card.Key], Is.SameAs(card.Value));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(turnPublicationCount, Is.Zero);
        Assert.That(layoutPublicationCount, Is.Zero);
        Assert.That(healthPublicationCount, Is.Zero);
    }

    /// <summary>验证战地手术在玩家行动结束时依次清除束缚与失去力量、按共享治疗契约受上限恢复生命、递减恢复层数并切入敌方行动。</summary>
    [Test]
    public void FieldSurgery_EndPlayerAction_CleansTemporaryStatusesBeforeCappedHealAndRegenerationDecay()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3231 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        BattleEffectFormulaResult setupDamage = BattleEffectFormula.Calculate(
            new BattleEffectFormulaContext(
                BattleEffectOperationType.DealDamage,
                configuredValue: 2,
                sourceStrength: 0,
                target: new BattleEffectTargetSnapshot(
                    scenario.Player.CurrentHealth,
                    scenario.Player.CurrentBlock,
                    scenario.Player.CurrentVulnerable)));
        scenario.Player.ApplyDamageOutcome(setupDamage.DamageOutcome.Value);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(68));
        scenario.Play(3231, targetId: null);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(68));
        scenario.Session.MachineGunnerRuntime.CombatState.Set(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.LoseStrength,
            2);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Settlements, Has.Count.EqualTo(5));
        Assert.That(
            result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 5)));
        var shackle = result.Settlements[0] as MachineGunnerPrivateStatusChangedSettlement;
        var loseStrength = result.Settlements[1] as MachineGunnerPrivateStatusChangedSettlement;
        var health = result.Settlements[2] as BattleHealthRestoredSettlement;
        var regeneration = result.Settlements[3] as MachineGunnerPrivateStatusChangedSettlement;
        var phase = result.Settlements[4] as BattlePhaseChangedSettlement;
        Assert.That(shackle, Is.Not.Null);
        Assert.That(shackle.Status, Is.EqualTo(MachineGunnerCombatantStatus.Shackle));
        Assert.That(
            (shackle.ValueBefore, shackle.ValueAfter),
            Is.EqualTo((1, 0)));
        Assert.That(loseStrength, Is.Not.Null);
        Assert.That(loseStrength.Status, Is.EqualTo(MachineGunnerCombatantStatus.LoseStrength));
        Assert.That(
            (loseStrength.ValueBefore, loseStrength.ValueAfter),
            Is.EqualTo((2, 0)));
        Assert.That(health, Is.Not.Null);
        Assert.That(health.EffectId, Is.Null);
        Assert.That(health.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That(health.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(
            (health.RequestedAmount, health.HealthBefore, health.HealthAfter, health.Amount),
            Is.EqualTo((5, 68, 70, 2)));
        Assert.That(regeneration, Is.Not.Null);
        Assert.That(regeneration.Status, Is.EqualTo(MachineGunnerCombatantStatus.Regeneration));
        Assert.That(
            (regeneration.ValueBefore, regeneration.ValueAfter),
            Is.EqualTo((5, 4)));
        Assert.That(phase, Is.Not.Null);
        Assert.That(phase.PhaseBefore, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(phase.PhaseAfter, Is.EqualTo(BattleTurnPhase.EnemyAction));
        Assert.That(
            (phase.RoundNumberBefore, phase.RoundNumberAfter),
            Is.EqualTo((1, 1)));
    }

    /// <summary>验证战地手术在满血时先清除束缚，仍记录零值治疗，再递减恢复层数并切换阶段。</summary>
    [Test]
    public void FieldSurgery_EndPlayerAction_AtFullHealthRecordsZeroHealAndStillReducesRegeneration()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3231 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(70));
        scenario.Play(3231, targetId: null);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Settlements, Has.Count.EqualTo(4));
        Assert.That(
            result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 4)));
        var shackle = result.Settlements[0] as MachineGunnerPrivateStatusChangedSettlement;
        var health = result.Settlements[1] as BattleHealthRestoredSettlement;
        var regeneration = result.Settlements[2] as MachineGunnerPrivateStatusChangedSettlement;
        var phase = result.Settlements[3] as BattlePhaseChangedSettlement;
        Assert.That(shackle, Is.Not.Null);
        Assert.That(shackle.Status, Is.EqualTo(MachineGunnerCombatantStatus.Shackle));
        Assert.That(
            (shackle.ValueBefore, shackle.ValueAfter),
            Is.EqualTo((1, 0)));
        Assert.That(health, Is.Not.Null);
        Assert.That(health.EffectId, Is.Null);
        Assert.That(health.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That(health.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(
            (health.RequestedAmount, health.HealthBefore, health.HealthAfter, health.Amount),
            Is.EqualTo((5, 70, 70, 0)));
        Assert.That(regeneration, Is.Not.Null);
        Assert.That(regeneration.Status, Is.EqualTo(MachineGunnerCombatantStatus.Regeneration));
        Assert.That(
            (regeneration.ValueBefore, regeneration.ValueAfter),
            Is.EqualTo((5, 4)));
        Assert.That(phase, Is.Not.Null);
        Assert.That(phase.PhaseBefore, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(phase.PhaseAfter, Is.EqualTo(BattleTurnPhase.EnemyAction));
        Assert.That(
            (phase.RoundNumberBefore, phase.RoundNumberAfter),
            Is.EqualTo((1, 1)));
    }

    /// <summary>验证一层缓冲只抵挡同一轮敌方攻击序列的第一段伤害，并紧随该伤害主记录消费。</summary>
    [Test]
    public void HoloDecoy_BufferPreventsOnlyFirstIncomingAttackAndConsumesAfterDamage()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3259 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 8);
        scenario.StartBattle();
        scenario.Play(3259, targetId: null);

        int resultCountBeforeEnd = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult[] roundEndResults = scenario.Results
            .Skip(resultCountBeforeEnd)
            .ToArray();
        foreach (BattleCommandExecutionResult roundEndResult in roundEndResults)
        {
            Assert.That(
                roundEndResult.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, roundEndResult.Settlements.Count)));
        }
        BattleDamageAppliedSettlement[] incomingDamages = roundEndResults
            .SelectMany(item => item.Settlements)
            .OfType<BattleDamageAppliedSettlement>()
            .Where(item => item.TargetId == scenario.Player.Id)
            .ToArray();
        BattleCommandExecutionResult protectedDamageResult = roundEndResults
            .Single(item => item.Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .Any(damage => damage.TargetId == scenario.Player.Id &&
                    damage.AttackValue == 8 &&
                    damage.HealthBefore == 70 &&
                    damage.HealthAfter == 70));
        BattleDamageAppliedSettlement protectedDamage = protectedDamageResult.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Single(item => item.TargetId == scenario.Player.Id &&
                item.AttackValue == 8 &&
                item.HealthBefore == 70 &&
                item.HealthAfter == 70);
        MachineGunnerPrivateStatusChangedSettlement consumed = protectedDamageResult.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.Buffer &&
                item.ValueBefore == 1 && item.ValueAfter == 0);

        Assert.That(incomingDamages.Select(item => item.SourceId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(incomingDamages.Select(item => item.AttackValue), Is.EqualTo(new[] { 8, 8 }));
        Assert.That(incomingDamages.Select(item => item.HealthAfter), Is.EqualTo(new[] { 70, 62 }));
        Assert.That(consumed.Order, Is.EqualTo(protectedDamage.Order + 1));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(62));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Buffer),
            Is.Zero);
    }

    /// <summary>验证多层缓冲按敌方攻击次数逐层消费，而不是把两次伤害错误地合并为一次防御。</summary>
    [Test]
    public void HoloDecoy_StackedBuffersProtectOneIncomingAttackEach()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3259, 3259 },
            initialHandCount: 2,
            initialEnergy: 2,
            enemyDamage: 8);
        scenario.StartBattle();
        scenario.Play(3259, targetId: null);
        scenario.Play(3259, targetId: null);

        int resultCountBeforeEnd = scenario.Results.Count;
        scenario.EndPlayerAction();
        MachineGunnerPrivateStatusChangedSettlement[] consumed = scenario.Results
            .Skip(resultCountBeforeEnd)
            .SelectMany(item => item.Settlements)
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Buffer)
            .ToArray();

        Assert.That(consumed.Select(item => item.ValueBefore), Is.EqualTo(new[] { 2, 1 }));
        Assert.That(consumed.Select(item => item.ValueAfter), Is.EqualTo(new[] { 1, 0 }));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(70));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Buffer),
            Is.Zero);
    }

    /// <summary>验证能量不足时全息诱饵保持零写入，既不会获得缓冲也不会移动手牌。</summary>
    [Test]
    public void HoloDecoy_InsufficientEnergyLeavesBufferAndZonesUnchanged()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3259 },
            initialHandCount: 1,
            initialEnergy: 0,
            enemyDamage: 0);
        scenario.StartBattle();

        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = scenario.Submit(3259, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(
            terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Buffer),
            Is.Zero);
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.ExhaustPile, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
    }

        /// <summary>验证撤退在完整归宿后由 Queue 签发一次系统结束行动续延，并在下一玩家回合把弹药补至当前上限。</summary>
        [Test]
        public void Retreat_EndsActionThroughQueueAndRefillsAmmoAtNextPlayerRound()
        {
            using var scenario = new MachineGunnerStarterScenario(
                new[] { 3216, 3203 },
                initialHandCount: 2,
                initialEnergy: 2,
                initialAmmo: 0,
                enemyDamage: 0);
            scenario.StartBattle();

            int resultCountBefore = scenario.Results.Count;
            BattleCommandSubmissionResult submission = scenario.Submit(3216, targetId: null);
            Assert.That(submission.Accepted, Is.True);
            BattleCommandExecutionResult[] results = scenario.Results
                .Skip(resultCountBefore)
                .ToArray();
            Assert.That(results, Has.Length.GreaterThanOrEqualTo(2));
            BattleCommandExecutionResult retreat = results[0];
            BattleCommandExecutionResult automaticEnd = results[1];
            MachineGunnerPrivateStatusChangedSettlement scheduledReload = retreat.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Single(item => item.Status ==
                    MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound);
            MachineGunnerPrivateStatusChangedSettlement consumedReload = results
                .SelectMany(item => item.Settlements)
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Single(item => item.Status ==
                    MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound &&
                    item.ValueBefore == 1 && item.ValueAfter == 0);
            BattleAmmoRefilledSettlement[] ammoRefills = results
                .SelectMany(item => item.Settlements)
                .OfType<BattleAmmoRefilledSettlement>()
                .Where(item => item.SourceId == scenario.Player.Id)
                .ToArray();

            Assert.That(retreat.CommandType, Is.EqualTo(BattleCommandType.PlayCard));
            Assert.That(automaticEnd.CommandType, Is.EqualTo(BattleCommandType.EndPlayerAction));
            Assert.That(automaticEnd.AuthoritySequence, Is.EqualTo(retreat.AuthoritySequence + 1));
            Assert.That(FindSettlement<BattleEnergySpentSettlement>(retreat).Amount, Is.EqualTo(2));
            Assert.That(
                retreat.Settlements.OfType<BattleBlockGainedSettlement>().Single().Amount,
                Is.EqualTo(15));
            Assert.That(scheduledReload.ValueBefore, Is.Zero);
            Assert.That(scheduledReload.ValueAfter, Is.EqualTo(1));
            Assert.That(FindSettlement<BattleCardMovedSettlement>(retreat).ToZone,
                Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(
                automaticEnd.Settlements.OfType<BattleCardMovedSettlement>()
                    .Any(item => item.ToZone == BattleCardZone.DiscardPile),
                Is.True);
            Assert.That(consumedReload.Order, Is.GreaterThanOrEqualTo(0));
            Assert.That(ammoRefills.Select(item => (item.AmmoBefore, item.AmmoAfter)),
                Is.EqualTo(new[] { (0, 1), (1, 5) }));
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(5));
            Assert.That(
                scenario.Session.MachineGunnerRuntime.CombatState.Get(
                    scenario.Player.Id,
                    MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound),
                Is.Zero);
        }

        /// <summary>验证撤退在费用不足时保持零写入，不生成系统结束行动续延，也不预约下回合补弹。</summary>
        [Test]
        public void Retreat_InsufficientEnergyLeavesZonesAndScheduledReloadUnchanged()
        {
            using var scenario = new MachineGunnerStarterScenario(
                new[] { 3216 },
                initialHandCount: 1,
                initialEnergy: 1,
                initialAmmo: 0,
                enemyDamage: 0);
            scenario.StartBattle();

            int resultCountBefore = scenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Submit(3216, targetId: null);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(scenario.Player.CurrentBlock, Is.Zero);
            Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
            Assert.That(
                scenario.Session.MachineGunnerRuntime.CombatState.Get(
                    scenario.Player.Id,
                    MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound),
                Is.Zero);
        }

        /// <summary>验证快速翻滚的即时格挡与可叠加下回合格挡只在下一玩家回合开始转化一次并清除。</summary>
        [Test]
        public void QuickRoll_StacksNextRoundBlockAndConsumesItOnceAtPlayerRoundStart()
        {
            using var scenario = new MachineGunnerStarterScenario(
                new[] { 3235, 3235 },
                initialHandCount: 2,
                initialEnergy: 2,
                enemyDamage: 0);
            scenario.StartBattle();

            BattleCommandExecutionResult first = scenario.Play(3235, targetId: null);
            BattleCommandExecutionResult second = scenario.Play(3235, targetId: null);
            MachineGunnerPrivateStatusChangedSettlement[] scheduled = new[] { first, second }
                .SelectMany(item => item.Settlements)
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Where(item => item.Status == MachineGunnerCombatantStatus.NextRoundBlock)
                .ToArray();

            Assert.That(scheduled.Select(item => (item.ValueBefore, item.ValueAfter)),
                Is.EqualTo(new[] { (0, 5), (5, 10) }));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(10));
            int resultCountBeforeEnd = scenario.Results.Count;
            scenario.EndPlayerAction();
            BattleCommandExecutionResult[] nextRoundResults = scenario.Results
                .Skip(resultCountBeforeEnd)
                .ToArray();
            MachineGunnerPrivateStatusChangedSettlement consumed = nextRoundResults
                .SelectMany(item => item.Settlements)
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Single(item => item.Status == MachineGunnerCombatantStatus.NextRoundBlock &&
                    item.ValueBefore == 10 && item.ValueAfter == 0);
            BattleBlockGainedSettlement restoredBlock = nextRoundResults
                .SelectMany(item => item.Settlements)
                .OfType<BattleBlockGainedSettlement>()
                .Single(item => item.TargetId == scenario.Player.Id && item.Amount == 10 &&
                    item.BlockBefore == 0 && item.BlockAfter == 10);

            Assert.That(consumed.Order, Is.LessThan(restoredBlock.Order));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(10));
            Assert.That(
                scenario.Session.MachineGunnerRuntime.CombatState.Get(
                    scenario.Player.Id,
                    MachineGunnerCombatantStatus.NextRoundBlock),
                Is.Zero);
            scenario.EndPlayerAction();
            Assert.That(scenario.Player.CurrentBlock, Is.Zero);
        }

        /// <summary>验证快速翻滚在费用不足时既不获得即时格挡，也不写入下回合格挡或移动卡牌。</summary>
        [Test]
        public void QuickRoll_InsufficientEnergyLeavesBlockAndNextRoundStateUnchanged()
        {
            using var scenario = new MachineGunnerStarterScenario(
                new[] { 3235 },
                initialHandCount: 1,
                initialEnergy: 0,
                enemyDamage: 0);
            scenario.StartBattle();

            using BattleCommandLifecycleExecutionRecorder lifecycle =
                scenario.Queue.RecordExecutionLifecycle();
            BattleCommandSubmissionResult submission = scenario.Submit(3235, targetId: null);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(scenario.Player.CurrentBlock, Is.Zero);
            Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
            Assert.That(
                scenario.Session.MachineGunnerRuntime.CombatState.Get(
                    scenario.Player.Id,
                    MachineGunnerCombatantStatus.NextRoundBlock),
                Is.Zero);
        }

        /// <summary>验证爆炸肘先走完整攻击修正，再写入陈年机油，最后按既有燃烧层数结算不读取攻击修正的 Debuff 伤害。</summary>
        [Test]
        public void ExplosiveElbow_AttackThenAgedOilThenCurrentBurnDebuff()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3253, 3252 },
            initialHandCount: 2,
            firstEnemyHealth: 40,
            initialEnergy: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Weakness, 1);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 1);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke, 1);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Armor, 2);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);
        scenario.FirstEnemy.ApplyVulnerableGain(1);
        scenario.FirstEnemy.ApplyBlockGain(4);

        scenario.Play(3253, targetId: null);
        BattleCommandExecutionResult result = scenario.Play(3252, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement oil = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.Oil);
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(damages, Has.Length.EqualTo(2));
        Assert.That(damages[0].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damages[0].AttackValue, Is.EqualTo(7));
        Assert.That(damages[0].BlockBefore, Is.EqualTo(4));
        Assert.That(damages[0].BlockAfter, Is.Zero);
        Assert.That(damages[0].HealthBefore, Is.EqualTo(40));
        Assert.That(damages[0].HealthAfter, Is.EqualTo(37));
        Assert.That(damages[1].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damages[1].AttackValue, Is.EqualTo(5));
        Assert.That(damages[1].BlockBefore, Is.Zero);
        Assert.That(damages[1].BlockAfter, Is.Zero);
        Assert.That(damages[1].HealthBefore, Is.EqualTo(37));
        Assert.That(damages[1].HealthAfter, Is.EqualTo(32));
        Assert.That(oil.ValueBefore, Is.EqualTo(3));
        Assert.That(oil.ValueAfter, Is.EqualTo(5));
        Assert.That(damages[0].Order, Is.LessThan(oil.Order));
        Assert.That(oil.Order, Is.LessThan(damages[1].Order));
        Assert.That(damages[1].Order, Is.LessThan(destination.Order));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(5));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(5));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Armor), Is.EqualTo(1));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证目标没有燃烧时爆炸肘不伪造第二段伤害，仍会按非射击攻击获得陈年机油并进入弃牌堆。</summary>
    [Test]
    public void ExplosiveElbow_WithoutBurnOnlyAttacksAndDiscards()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3253, 3252 },
            initialHandCount: 2,
            firstEnemyHealth: 30,
            initialEnergy: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);
        scenario.Play(3253, targetId: null);
        BattleCommandExecutionResult result = scenario.Play(3252, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] privateStatuses = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(damages, Has.Length.EqualTo(1));
        Assert.That(damages[0].AttackValue, Is.EqualTo(10));
        Assert.That(privateStatuses.Select(item => item.Status),
            Is.EqualTo(new[] { MachineGunnerCombatantStatus.Oil }));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(5));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Zones.ExhaustPile, Is.Empty);
    }

    /// <summary>验证爆炸肘的普通攻击已经致死时不会继续写入陈年机油或触发即时燃烧，且仍完成弃牌归宿。</summary>
    [Test]
    public void ExplosiveElbow_NormalLethalHitSkipsAgedOilAndCurrentBurnDebuff()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3253, 3252 },
            initialHandCount: 2,
            firstEnemyHealth: 10,
            initialEnergy: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);
        scenario.Play(3253, targetId: null);
        BattleCommandExecutionResult result = scenario.Play(3252, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(damages, Has.Length.EqualTo(1));
        Assert.That(damages[0].WasFatal, Is.True);
        Assert.That(result.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(5));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(3));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
        Assert.That(scenario.SecondEnemy.IsAlive, Is.True);
    }

    /// <summary>验证爆炸肘的即时燃烧击杀最后敌人时，先把卡牌移入弃牌堆再由既有控制器结束战斗。</summary>
    [Test]
    public void ExplosiveElbow_CurrentBurnDebuffKillsLastEnemy_DiscardsBeforeBattleEnds()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3201, 3252 },
            initialHandCount: 2,
            firstEnemyHealth: 15,
            secondEnemyHealth: 6,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();

        scenario.Play(3201, scenario.SecondEnemy.Id);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 5);

        BattleCommandExecutionResult result = scenario.Play(3252, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);
        BattlePhaseChangedSettlement phase = FindSettlement<BattlePhaseChangedSettlement>(result);

        Assert.That(damages, Has.Length.EqualTo(2));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 10, 5 }));
        Assert.That(damages[1].WasFatal, Is.True);
        Assert.That(destination.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(destination.Order, Is.LessThan(phase.Order));
        Assert.That(phase.PhaseAfter, Is.EqualTo(BattleTurnPhase.BattleEnded));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(5));
        Assert.That(scenario.Zones.ExhaustPile, Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
    }

    /// <summary>验证 Encounter 中前一名敌人被燃烧击杀但仍有后续敌人时，后续敌人与玩家燃烧都继续按既定顺序结算。</summary>
    [Test]
    public void EndPlayerAction_BurnKillsEarlierEnemy_ContinuesLaterEnemyAndPlayerBurn()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new int[0],
            initialHandCount: 0,
            firstEnemyHealth: 4,
            secondEnemyHealth: 10,
            enemyDamage: 0);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 4);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn, 3);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 5);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        BattleDamageAppliedSettlement[] burnSettlements = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(burnSettlements.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.Player.Id,
        }));
        Assert.That(burnSettlements[0].WasFatal, Is.True);
        Assert.That(burnSettlements[1].WasFatal, Is.False);
        Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(7));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(65));
    }

    /// <summary>验证玩家自身燃烧致死后同一命令直接进入败北，既不创建敌方行动续接也不让死亡玩家跨越到下一阶段。</summary>
    [Test]
    public void EndPlayerAction_PlayerBurnKillsPlayer_EndsBattleBeforeEnemyContinuation()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new int[0],
            initialHandCount: 0,
            enemyDamage: 0);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 70);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        BattleDamageAppliedSettlement[] burnSettlements = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(burnSettlements, Has.Length.EqualTo(1));
        Assert.That(burnSettlements[0].TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(burnSettlements[0].WasFatal, Is.True);
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
        Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.Zero);
        Assert.That(result.Settlements.Last(), Is.TypeOf<BattlePhaseChangedSettlement>());
    }

    /// <summary>验证汽油弹先向全体敌人加油，凝固汽油弹随后只消费既有浸油并在同张卡末尾再施加本次浸油。</summary>
    [Test]
    public void BurnOilPrograms_GasPumpThenNapalm_UseOldOilBeforeAddingNewOil()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3217, 3218 },
            initialHandCount: 2);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 2);

        BattleCommandExecutionResult gasPump = scenario.Play(3217, targetId: null);
        BattleCommandExecutionResult napalm = scenario.Play(3218, targetId: null);

        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(10));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(8));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(8));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(7));
        Assert.That(
            gasPump.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>().ToArray(),
            Has.Length.EqualTo(2));
        Assert.That(
            napalm.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>().ToArray(),
            Has.Length.EqualTo(6));
        MachineGunnerPrivateStatusChangedSettlement[] oilConsumption = napalm.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Oil && item.Amount < 0)
            .ToArray();
        Assert.That(oilConsumption, Has.Length.EqualTo(2));
        Assert.That(oilConsumption.Select(item => item.ValueBefore), Is.EqualTo(new[] { 7, 5 }));
        Assert.That(oilConsumption.Select(item => item.ValueAfter), Is.EqualTo(new[] { 3, 2 }));
        Assert.That(oilConsumption.Select(item => item.Amount), Is.EqualTo(new[] { -4, -3 }));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
    }

    /// <summary>验证燃烧瓶使用显式敌人，烈焰肘先造成最近目标的攻击伤害再按当时既有浸油施加燃烧。</summary>
    [Test]
    public void BurnOilPrograms_MolotovThenFlameElbow_ResolveDamageBeforeBurn()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3219, 3255 },
            initialHandCount: 2);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);

        BattleCommandExecutionResult molotov = scenario.Play(3219, scenario.FirstEnemy.Id);
        BattleCommandExecutionResult flameElbow = scenario.Play(3255, targetId: null);
        int damageIndex = flameElbow.Settlements
            .Select((item, index) => new { item, index })
            .Single(item => item.item is BattleDamageAppliedSettlement)
            .index;
        int burnIndex = flameElbow.Settlements
            .Select((item, index) => new { item, index })
            .First(item => item.item is MachineGunnerPrivateStatusChangedSettlement status &&
                status.Status == MachineGunnerCombatantStatus.Burn)
            .index;

        Assert.That(
            molotov.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .First(item => item.Status == MachineGunnerCombatantStatus.Burn)
                .ValueAfter,
            Is.EqualTo(8));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(14));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(12));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.Zero);
        Assert.That(damageIndex, Is.LessThan(burnIndex));
    }

    /// <summary>验证烈焰肘的攻击若先击杀最近敌人，不会再对已死亡目标提交燃烧或浸油变化。</summary>
    [Test]
    public void BurnOilPrograms_FlameElbowLethalHit_SkipsBurnApplication()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3255 },
            initialHandCount: 1,
            firstEnemyHealth: 6);
        scenario.StartBattle();

        BattleCommandExecutionResult flameElbow = scenario.Play(3255, targetId: null);

        Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
        Assert.That(
            flameElbow.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(),
            Is.Empty);
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Burn),
            Is.Zero);
    }

    /// <summary>验证燃烧弹药可叠层，并覆盖狙击这一不吃兴奋剂额外段但仍属于射击命中的边界。</summary>
    [Test]
    public void IncendiaryAmmo_StacksAndTriggersOnSniperShot()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3210, 3210, 3247 },
            initialHandCount: 3,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100);
        scenario.StartBattle();

        scenario.Play(3210, targetId: null);
        scenario.Play(3210, targetId: null);
        BattleCommandExecutionResult sniper = scenario.Play(3247, targetId: null);

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.IncendiaryAmmo),
            Is.EqualTo(2));
        Assert.That(sniper.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(1));
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(sniper).Amount, Is.EqualTo(2));
        Assert.That(
            state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn),
            Is.EqualTo(2));
    }

    /// <summary>验证钉刺射击在兴奋剂追加的每一段都按伤害、虚弱、易伤、燃烧弹药的顺序结算，并让首段易伤影响次段。</summary>
    [Test]
    public void SpikeShot_StimInterleavesEveryHitAndFeedsNextHit()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3210, 3236, 3205, 3248 },
            initialHandCount: 4,
            firstEnemyHealth: 100);
        scenario.StartBattle();

        scenario.Play(3210, targetId: null);
        scenario.Play(3236, targetId: null);
        scenario.Play(3205, targetId: null);
        BattleCommandExecutionResult spikeShot = scenario.Play(3248, scenario.FirstEnemy.Id);

        BattleDamageAppliedSettlement[] damages = spikeShot.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] privateStatuses = spikeShot.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();
        string[] interleavedKinds = spikeShot.Settlements
            .Select(item =>
            {
                if (item is BattleDamageAppliedSettlement)
                    return "Damage";
                if (item is MachineGunnerPrivateStatusChangedSettlement weaknessChange &&
                    weaknessChange.Status == MachineGunnerCombatantStatus.Weakness)
                {
                    return "Weakness";
                }

                if (item is BattleStatusAppliedSettlement)
                    return "Vulnerable";
                if (item is MachineGunnerPrivateStatusChangedSettlement burnChange &&
                    burnChange.Status == MachineGunnerCombatantStatus.Burn)
                {
                    return "Burn";
                }

                return null;
            })
            .Where(kind => kind != null)
            .ToArray();

        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 4, 8 }));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(88));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Weakness),
            Is.EqualTo(2));
        Assert.That(scenario.FirstEnemy.CurrentVulnerable, Is.EqualTo(2));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Burn),
            Is.EqualTo(2));
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(spikeShot).Amount, Is.EqualTo(2));
        Assert.That(
            privateStatuses.Count(item => item.Status == MachineGunnerCombatantStatus.Weakness),
            Is.EqualTo(2));
        Assert.That(
            privateStatuses.Count(item => item.Status == MachineGunnerCombatantStatus.Burn),
            Is.EqualTo(2));
        Assert.That(
            spikeShot.Settlements.OfType<BattleStatusAppliedSettlement>().Count(),
            Is.EqualTo(2));
        Assert.That(
            interleavedKinds,
            Is.EqualTo(new[]
            {
                "Damage", "Weakness", "Vulnerable", "Burn",
                "Damage", "Weakness", "Vulnerable", "Burn",
            }));
    }

    /// <summary>验证钉刺射击只对命中后仍存活的目标写入状态，同时保持全格挡存活和支付失败的既有边界。</summary>
    [Test]
    public void SpikeShot_SkipsPostHitWhenLethalButAppliesItWhenBlockedAndAlive()
    {
        using var lethalScenario = new MachineGunnerStarterScenario(
            new[] { 3248 },
            initialHandCount: 1,
            firstEnemyHealth: 1);
        lethalScenario.StartBattle();
        BattleCommandExecutionResult lethal = lethalScenario.Play(3248, lethalScenario.FirstEnemy.Id);

        Assert.That(lethalScenario.FirstEnemy.IsAlive, Is.False);
        Assert.That(lethal.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
        Assert.That(lethal.Settlements.OfType<BattleStatusAppliedSettlement>(), Is.Empty);

        using var blockedScenario = new MachineGunnerStarterScenario(
            new[] { 3210, 3248 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            initialEnergy: 1);
        blockedScenario.StartBattle();
        blockedScenario.FirstEnemy.ApplyBlockGain(1);
        blockedScenario.Play(3210, targetId: null);
        blockedScenario.Play(3248, blockedScenario.FirstEnemy.Id);

        MachineGunnerCombatState blockedState = blockedScenario.Session.MachineGunnerRuntime.CombatState;
        Assert.That(blockedScenario.FirstEnemy.CurrentHealth, Is.EqualTo(100));
        Assert.That(blockedScenario.FirstEnemy.CurrentBlock, Is.Zero);
        Assert.That(
            blockedState.Get(blockedScenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Weakness),
            Is.EqualTo(1));
        Assert.That(blockedScenario.FirstEnemy.CurrentVulnerable, Is.EqualTo(1));
        Assert.That(
            blockedState.Get(blockedScenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn),
            Is.EqualTo(1));

        using var failedScenario = new MachineGunnerStarterScenario(
            new[] { 3210, 3248 },
            initialHandCount: 2,
            initialEnergy: 1,
            initialAmmo: 0);
        failedScenario.StartBattle();
        failedScenario.Play(3210, targetId: null);
        int handBefore = failedScenario.Zones.Hand.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            failedScenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = failedScenario.Submit(3248, failedScenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        MachineGunnerCombatState failedState = failedScenario.Session.MachineGunnerRuntime.CombatState;
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(failedScenario.Zones.Hand, Has.Count.EqualTo(handBefore));
        Assert.That(failedScenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(
            failedState.Get(failedScenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Weakness),
            Is.Zero);
        Assert.That(failedScenario.FirstEnemy.CurrentVulnerable, Is.Zero);
        Assert.That(
            failedState.Get(failedScenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn),
            Is.Zero);
    }

    /// <summary>验证陈年机油的多张能力牌只开启一次固定数值效果，且射击命中不会写入浸油。</summary>
    [Test]
    public void AgedOil_StacksOnlyEnableAndOnlyNonShootAttack()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3253, 3253, 3202, 3201 },
            initialHandCount: 4,
            firstEnemyHealth: 100);
        scenario.StartBattle();

        scenario.Play(3253, targetId: null);
        scenario.Play(3253, targetId: null);
        scenario.Play(3202, targetId: null);
        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);

        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.AgedOil),
            Is.EqualTo(2));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Oil),
            Is.EqualTo(2));
        Assert.That(
            shoot.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Where(item => item.Status == MachineGunnerCombatantStatus.Oil),
            Is.Empty);
    }

    /// <summary>验证陈年机油对随机 X 段逐段写入固定浸油，且 X 为零不会写入状态或推进随机流。</summary>
    [Test]
    public void AgedOil_HurricaneElbow_AppliesPerRandomHitAndZeroXWritesNothing()
    {
        using var hurricaneScenario = new MachineGunnerStarterScenario(
            new[] { 3253, 3232 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            initialEnergy: 4);
        hurricaneScenario.StartBattle();
        hurricaneScenario.Play(3253, targetId: null);
        uint randomBefore = hurricaneScenario.Session.MachineGunnerRuntime.CardRandomState;
        BattleCommandExecutionResult hurricane = hurricaneScenario.Play(3232, targetId: null);

        BattleDamageAppliedSettlement[] damages = hurricane.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] oilChanges = hurricane.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Oil)
            .ToArray();

        Assert.That(damages, Has.Length.EqualTo(3));
        Assert.That(oilChanges, Has.Length.EqualTo(3));
        Assert.That(oilChanges.Select(item => item.Amount), Is.EqualTo(new[] { 2, 2, 2 }));
        Assert.That(oilChanges.Select(item => item.TargetId),
            Is.EqualTo(damages.Select(item => item.TargetId)));
        Assert.That(
            hurricaneScenario.Session.MachineGunnerRuntime.CombatState.Get(
                hurricaneScenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Oil) +
            hurricaneScenario.Session.MachineGunnerRuntime.CombatState.Get(
                hurricaneScenario.SecondEnemy.Id,
                MachineGunnerCombatantStatus.Oil),
            Is.EqualTo(6));
        Assert.That(
            hurricaneScenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.Not.EqualTo(randomBefore));

        using var zeroScenario = new MachineGunnerStarterScenario(
            new[] { 3253, 3232 },
            initialHandCount: 2,
            initialEnergy: 1);
        zeroScenario.StartBattle();
        zeroScenario.Play(3253, targetId: null);
        uint zeroRandomBefore = zeroScenario.Session.MachineGunnerRuntime.CardRandomState;
        BattleCommandExecutionResult zeroHurricane = zeroScenario.Play(3232, targetId: null);

        Assert.That(zeroHurricane.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(
            zeroHurricane.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Where(item => item.Status == MachineGunnerCombatantStatus.Oil),
            Is.Empty);
        Assert.That(
            zeroScenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(zeroRandomBefore));
    }

    /// <summary>验证烈焰肘先以旧浸油施加并减半燃烧，再由陈年机油把该实际命中段追加固定浸油。</summary>
    [Test]
    public void AgedOil_FlameElbow_ConsumesOldOilBeforeAddingPerHitOil()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3253, 3255 },
            initialHandCount: 2,
            initialEnergy: 2);
        scenario.StartBattle();
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);

        scenario.Play(3253, targetId: null);
        BattleCommandExecutionResult flameElbow = scenario.Play(3255, targetId: null);
        MachineGunnerPrivateStatusChangedSettlement[] privateStatuses = flameElbow.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] oilChanges = privateStatuses
            .Where(item => item.Status == MachineGunnerCombatantStatus.Oil)
            .ToArray();
        string[] kinds = flameElbow.Settlements
            .Select(item =>
            {
                if (item is BattleDamageAppliedSettlement)
                    return "Damage";
                if (item is MachineGunnerPrivateStatusChangedSettlement burnStatus &&
                    burnStatus.Status == MachineGunnerCombatantStatus.Burn)
                {
                    return "Burn";
                }

                if (item is MachineGunnerPrivateStatusChangedSettlement oilStatus &&
                    oilStatus.Status == MachineGunnerCombatantStatus.Oil)
                {
                    return "Oil";
                }

                return null;
            })
            .Where(kind => kind != null)
            .ToArray();

        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(14));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(6));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(3));
        Assert.That(oilChanges.Select(item => item.ValueBefore), Is.EqualTo(new[] { 3, 1 }));
        Assert.That(oilChanges.Select(item => item.ValueAfter), Is.EqualTo(new[] { 1, 3 }));
        Assert.That(kinds, Is.EqualTo(new[] { "Damage", "Burn", "Oil", "Oil" }));
    }

    /// <summary>验证功夫机甲按叠层在每张成功的非射击攻击后获得格挡，射击攻击不会误触发。</summary>
    [Test]
    public void KungfuMech_NonShootAttackGainsStackedBlockButShootDoesNot()
        {
            using var scenario = new MachineGunnerStarterScenario(
                new[] { 3212, 3212, 3202, 3201 },
                initialHandCount: 4,
                firstEnemyHealth: 100,
                initialEnergy: 4);
        scenario.StartBattle();

        scenario.Play(3212, targetId: null);
        scenario.Play(3212, targetId: null);
        BattleCommandExecutionResult elbow = scenario.Play(3202, targetId: null);
        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);

        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.KungfuMech),
            Is.EqualTo(2));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(8));
        Assert.That(
            FindSettlement<BattleDamageAppliedSettlement>(elbow).AttackValue,
            Is.EqualTo(6));
        Assert.That(
            FindSettlement<BattleDamageAppliedSettlement>(shoot).AttackValue,
            Is.EqualTo(6));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(8));
    }

    /// <summary>验证游击战术一张能力牌提供两层，按普通和兴奋剂后的实际耗弹量分别追加格挡。</summary>
    [Test]
    public void GuerrillaTactics_GrantsBlockFromActualAmmoSpendIncludingStimBonus()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3251, 3201, 3205, 3201 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            initialEnergy: 3,
            initialAmmo: 3);
        scenario.StartBattle();

        BattleCommandExecutionResult guerrilla = scenario.Play(3251, targetId: null);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(guerrilla).Amount, Is.EqualTo(1));
        Assert.That(guerrilla.Settlements.OfType<BattleBlockGainedSettlement>(), Is.Empty);
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.GuerrillaTactics),
            Is.EqualTo(2));

        BattleCommandExecutionResult normalShoot = scenario.Play(3201, scenario.FirstEnemy.Id);
        BattleBlockGainedSettlement normalBlock = FindSettlement<BattleBlockGainedSettlement>(normalShoot);

        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(normalShoot).Amount, Is.EqualTo(1));
        Assert.That(normalBlock.Amount, Is.EqualTo(2));
        Assert.That(normalBlock.BlockBefore, Is.Zero);
        Assert.That(normalBlock.BlockAfter, Is.EqualTo(2));

        scenario.Play(3205, targetId: null);
        BattleCommandExecutionResult stimulatedShoot = scenario.Play(3201, scenario.FirstEnemy.Id);
        BattleAmmoSpentSettlement stimulatedAmmo = FindSettlement<BattleAmmoSpentSettlement>(stimulatedShoot);
        BattleBlockGainedSettlement stimulatedBlock = FindSettlement<BattleBlockGainedSettlement>(stimulatedShoot);

        Assert.That(stimulatedAmmo.Amount, Is.EqualTo(2));
        Assert.That(stimulatedBlock.Amount, Is.EqualTo(4));
        Assert.That(stimulatedBlock.BlockBefore, Is.EqualTo(2));
        Assert.That(stimulatedBlock.BlockAfter, Is.EqualTo(6));
        Assert.That(stimulatedAmmo.Order, Is.LessThan(stimulatedBlock.Order));
        Assert.That(
            stimulatedShoot.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, stimulatedShoot.Settlements.Count)));
    }

    /// <summary>验证每张游击战术都增加两层，且能量不足时不会写入层数、卡区或格挡。</summary>
    [Test]
    public void GuerrillaTactics_StacksTwoPerPowerCardAndInsufficientEnergyDoesNotActivate()
    {
        using var stackedScenario = new MachineGunnerStarterScenario(
            new[] { 3251, 3251, 3201 },
            initialHandCount: 3,
            firstEnemyHealth: 100,
            initialEnergy: 2,
            initialAmmo: 1);
        stackedScenario.StartBattle();

        BattleCommandExecutionResult firstGuerrilla = stackedScenario.Play(3251, targetId: null);
        BattleCommandExecutionResult secondGuerrilla = stackedScenario.Play(3251, targetId: null);
        BattleCardMovedSettlement firstPowerMove = FindSettlement<BattleCardMovedSettlement>(firstGuerrilla);
        BattleCardMovedSettlement secondPowerMove = FindSettlement<BattleCardMovedSettlement>(secondGuerrilla);

        Assert.That(stackedScenario.Zones.PowerPile, Has.Count.EqualTo(2));
        Assert.That(stackedScenario.Zones.DiscardPile, Is.Empty);
        Assert.That(stackedScenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(
            stackedScenario.Zones.PowerPile.Select(cardId => stackedScenario.Zones.Cards[cardId].TemplateId),
            Is.EqualTo(new[] { 3251, 3251 }));
        Assert.That(firstPowerMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(firstPowerMove.ToZone, Is.EqualTo(BattleCardZone.PowerPile));
        Assert.That(secondPowerMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(secondPowerMove.ToZone, Is.EqualTo(BattleCardZone.PowerPile));
        Assert.That(secondPowerMove.CardId, Is.Not.EqualTo(firstPowerMove.CardId));
        Assert.That(
            stackedScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.GuerrillaTactics),
            Is.EqualTo(4));

        BattleCommandExecutionResult shoot = stackedScenario.Play(3201, stackedScenario.FirstEnemy.Id);

        Assert.That(FindSettlement<BattleBlockGainedSettlement>(shoot).Amount, Is.EqualTo(4));

        using var insufficientEnergyScenario = new MachineGunnerStarterScenario(
            new[] { 3251 },
            initialHandCount: 1,
            initialEnergy: 0);
        insufficientEnergyScenario.StartBattle();
        int resultCountBefore = insufficientEnergyScenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            insufficientEnergyScenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = insufficientEnergyScenario.Submit(3251, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(insufficientEnergyScenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(insufficientEnergyScenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(insufficientEnergyScenario.Zones.PowerPile, Is.Empty);
        Assert.That(insufficientEnergyScenario.Player.CurrentBlock, Is.Zero);
        Assert.That(
            insufficientEnergyScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.GuerrillaTactics),
            Is.Zero);
    }

    /// <summary>验证电磁增压以能力牌进入 PowerPile、提供三层战斗持续开火且不影响非射击攻击。</summary>
    [Test]
    public void ElectroBoost_EntersPowerPileAndKeepsFirePowerAfterActionEnd()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3236, 3202, 3201 },
            initialHandCount: 3);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        BattleCommandExecutionResult boost = scenario.Play(3236, targetId: null);
        BattleCommandExecutionResult elbow = scenario.Play(3202, targetId: null);
        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);

        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.FirePower), Is.EqualTo(3));
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.ElectroBoost),
            Is.EqualTo(1));
        Assert.That(
            boost.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Single(item => item.Status == MachineGunnerCombatantStatus.FirePower)
                .ValueAfter,
            Is.EqualTo(3));
        Assert.That(
            FindSettlement<BattleDamageAppliedSettlement>(elbow).AttackValue,
            Is.EqualTo(6));
        Assert.That(
            FindSettlement<BattleDamageAppliedSettlement>(shoot).AttackValue,
            Is.EqualTo(9));

        scenario.EndPlayerAction();

        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.FirePower), Is.EqualTo(3));
    }

    /// <summary>验证防御靶机以实际消耗的每三枚弹药换取一层无实体，进入消耗堆且状态不会随本回合结束衰减。</summary>
    [Test]
    public void DefenseTarget_SpendsAmmoInThreesAppliesIntangibleAndExhausts()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3262 },
            initialHandCount: 1,
            initialEnergy: 2,
            initialAmmo: 3,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3262, targetId: null);
        MachineGunnerPrivateStatusChangedSettlement intangible = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.Intangible);
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(2));
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(result).Amount, Is.EqualTo(3));
        Assert.That(intangible.ValueBefore, Is.Zero);
        Assert.That(intangible.ValueAfter, Is.EqualTo(1));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Zones.ExhaustPile, Has.Count.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Intangible),
            Is.EqualTo(1));

        scenario.EndPlayerAction();

        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Intangible),
            Is.EqualTo(1));
    }

    /// <summary>验证防御靶机允许最低两枚弹药但不伪造零层状态记录，并把九枚弹药作为硬上限。</summary>
    [Test]
    public void DefenseTarget_UsesActualAmmoWithMinimumAndMaximumBoundaries()
    {
        using (var twoAmmoScenario = new MachineGunnerStarterScenario(
                   new[] { 3262 },
                   initialHandCount: 1,
                   initialEnergy: 2,
                   initialAmmo: 2,
                   ammoMaximum: 10))
        {
            twoAmmoScenario.StartBattle();
            BattleCommandExecutionResult result = twoAmmoScenario.Play(3262, targetId: null);

            Assert.That(FindSettlement<BattleAmmoSpentSettlement>(result).Amount, Is.EqualTo(2));
            Assert.That(
                result.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>()
                    .Where(item => item.Status == MachineGunnerCombatantStatus.Intangible),
                Is.Empty);
            Assert.That(twoAmmoScenario.Queue.Turn.CurrentValue.Players[twoAmmoScenario.Player.Id].Ammo, Is.Zero);
            Assert.That(twoAmmoScenario.Zones.ExhaustPile, Has.Count.EqualTo(1));
        }

        using (var cappedScenario = new MachineGunnerStarterScenario(
                   new[] { 3262 },
                   initialHandCount: 1,
                   initialEnergy: 2,
                   initialAmmo: 10,
                   ammoMaximum: 10))
        {
            cappedScenario.StartBattle();
            BattleCommandExecutionResult result = cappedScenario.Play(3262, targetId: null);
            MachineGunnerPrivateStatusChangedSettlement intangible = result.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Single(item => item.Status == MachineGunnerCombatantStatus.Intangible);

            Assert.That(FindSettlement<BattleAmmoSpentSettlement>(result).Amount, Is.EqualTo(9));
            Assert.That(cappedScenario.Queue.Turn.CurrentValue.Players[cappedScenario.Player.Id].Ammo, Is.EqualTo(1));
            Assert.That(intangible.ValueBefore, Is.Zero);
            Assert.That(intangible.ValueAfter, Is.EqualTo(3));
        }
    }

    /// <summary>验证防御靶机弹药不足时经唯一 Queue seam 失败，不消耗资源、卡牌、状态或职业随机流。</summary>
    [Test]
    public void DefenseTarget_RequiresAtLeastTwoAmmoWithoutWritingAnyState()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3262 },
            initialHandCount: 1,
            initialEnergy: 2,
            initialAmmo: 1);
        scenario.StartBattle();

        int handBefore = scenario.Zones.Hand.Count;
        int exhaustBefore = scenario.Zones.ExhaustPile.Count;
        PlayerTurnData turnBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = scenario.Submit(3262, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(handBefore));
        Assert.That(scenario.Zones.ExhaustPile, Has.Count.EqualTo(exhaustBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(turnBefore.Energy));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(turnBefore.Ammo));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.Intangible),
            Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证击退射击冻结最近两名敌人，依次造成不同伤害并施加会在各自行动结束清除的失去力量。</summary>
    [Test]
    public void KnockbackShot_FreezesNearestTwoTargetsAndClearsLoseStrengthAfterTheirActions()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3223 },
            initialHandCount: 1,
            firstEnemyHealth: 30,
            secondEnemyHealth: 30,
            enemyDamage: 8,
            initialEnergy: 0,
            initialAmmo: 1);
        scenario.StartBattle();
        scenario.FirstEnemy.ApplyStrengthChange(4);
        scenario.SecondEnemy.ApplyStrengthChange(3);

        BattleCommandExecutionResult play = scenario.Play(3223, targetId: null);
        BattleDamageAppliedSettlement[] cardDamages = play.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] applies = play.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.LoseStrength)
            .ToArray();

        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(play).Amount, Is.EqualTo(1));
        Assert.That(cardDamages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(cardDamages.Select(item => item.AttackValue), Is.EqualTo(new[] { 7, 3 }));
        Assert.That(applies.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(applies.Select(item => item.ValueAfter), Is.EqualTo(new[] { 2, 2 }));
        Assert.That(cardDamages[0].Order, Is.LessThan(applies[0].Order));
        Assert.That(applies[0].Order, Is.LessThan(cardDamages[1].Order));
        Assert.That(cardDamages[1].Order, Is.LessThan(applies[1].Order));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(23));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(27));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(play).ToZone, Is.EqualTo(BattleCardZone.DiscardPile));

        int resultCountBeforeEnd = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult[] roundResults = scenario.Results
            .Skip(resultCountBeforeEnd)
            .ToArray();
        BattleDamageAppliedSettlement[] enemyDamages = roundResults
            .SelectMany(item => item.Settlements)
            .OfType<BattleDamageAppliedSettlement>()
            .Where(item => item.TargetId == scenario.Player.Id)
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] clears = roundResults
            .SelectMany(item => item.Settlements)
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.LoseStrength)
            .ToArray();

        Assert.That(enemyDamages.Select(item => item.SourceId), Is.EqualTo(new CombatantId?[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(enemyDamages.Select(item => item.AttackValue), Is.EqualTo(new[] { 10, 9 }));
        Assert.That(clears.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(clears.Select(item => item.ValueBefore), Is.EqualTo(new[] { 2, 2 }));
        Assert.That(clears.Select(item => item.ValueAfter), Is.EqualTo(new[] { 0, 0 }));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.LoseStrength),
            Is.Zero);
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.SecondEnemy.Id,
                MachineGunnerCombatantStatus.LoseStrength),
            Is.Zero);
        foreach (BattleCommandExecutionResult result in roundResults)
        {
            Assert.That(
                result.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
            BattleDamageAppliedSettlement enemyDamage = result.Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .SingleOrDefault(item => item.TargetId == scenario.Player.Id);
            MachineGunnerPrivateStatusChangedSettlement loseStrengthClear = result.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .SingleOrDefault(item => item.Status == MachineGunnerCombatantStatus.LoseStrength);
            BattleEnemyIntentAdvancedSettlement intentAdvanced = result.Settlements
                .OfType<BattleEnemyIntentAdvancedSettlement>()
                .SingleOrDefault();
            if (enemyDamage != null && loseStrengthClear != null && intentAdvanced != null)
            {
                Assert.That(enemyDamage.Order, Is.LessThan(loseStrengthClear.Order));
                Assert.That(loseStrengthClear.Order, Is.LessThan(intentAdvanced.Order));
            }
        }
    }

    /// <summary>验证玩家携带的失去力量在其行动结束时先清零，随后才进入回合末燃烧结算。</summary>
    [Test]
    public void EndPlayerAction_ClearsPlayerLoseStrengthBeforeRoundEndBurn()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new int[0],
            initialHandCount: 0,
            enemyDamage: 0);
        scenario.StartBattle();
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.LoseStrength, 3);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Burn, 2);

        BattleCommandExecutionResult result = scenario.EndPlayerActionResult();
        MachineGunnerPrivateStatusChangedSettlement clear = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.LoseStrength);
        BattleDamageAppliedSettlement playerBurn = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Single(item => item.TargetId == scenario.Player.Id);

        Assert.That(clear.ValueBefore, Is.EqualTo(3));
        Assert.That(clear.ValueAfter, Is.Zero);
        Assert.That(clear.Order, Is.LessThan(playerBurn.Order));
        Assert.That(
            state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.LoseStrength),
            Is.Zero);
    }

    /// <summary>验证只剩一名敌人时击退射击仍可结算首段，并稳定跳过缺席的第二段。</summary>
    [Test]
    public void KnockbackShot_WithOneLivingEnemySkipsMissingSecondSegment()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3201, 3223 },
            initialHandCount: 2,
            secondEnemyHealth: 4,
            enemyDamage: 0,
            initialEnergy: 1,
            initialAmmo: 2);
        scenario.StartBattle();
        scenario.Play(3201, scenario.SecondEnemy.Id);

        BattleCommandExecutionResult result = scenario.Play(3223, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] loseStrength = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.LoseStrength)
            .ToArray();

        Assert.That(damages, Has.Length.EqualTo(1));
        Assert.That(damages[0].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damages[0].AttackValue, Is.EqualTo(7));
        Assert.That(loseStrength, Has.Length.EqualTo(1));
        Assert.That(loseStrength[0].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(loseStrength[0].ValueAfter, Is.EqualTo(2));
        Assert.That(scenario.SecondEnemy.IsAlive, Is.False);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.Zero);
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(2));
    }

    /// <summary>验证首段击杀最近敌人后，第二段仍命中施放时冻结的第二近敌人且不会递补第三名。</summary>
    [Test]
    public void KnockbackShot_FirstKillDoesNotRetargetMissingSnapshotSlot()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3223 },
            initialHandCount: 1,
            firstEnemyHealth: 7,
            secondEnemyHealth: 20,
            enemyDamage: 0,
            initialEnergy: 0,
            initialAmmo: 1,
            thirdEnemyHealth: 20);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3223, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] loseStrength = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.LoseStrength)
            .ToArray();

        Assert.That(damages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 7, 3 }));
        Assert.That(damages[0].WasFatal, Is.True);
        Assert.That(loseStrength, Has.Length.EqualTo(1));
        Assert.That(loseStrength[0].TargetId, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(17));
        Assert.That(scenario.ThirdEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.ThirdEnemy.Id,
                MachineGunnerCombatantStatus.LoseStrength),
            Is.Zero);
    }

    /// <summary>验证击退射击弹药不足时不写入伤害、状态、卡区或随机流。</summary>
    [Test]
    public void KnockbackShot_InsufficientAmmoFailsWithoutWritingCombatOrZones()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3223 },
            initialHandCount: 1,
            initialEnergy: 0,
            initialAmmo: 0,
            enemyDamage: 0);
        scenario.StartBattle();
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3223, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.LoseStrength),
            Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证击退射击是全自动双目标程序，携带显式目标时稳定失败且保持零写入。</summary>
    [Test]
    public void KnockbackShot_ExplicitTargetFailsWithoutWritingCombatOrZones()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3223 },
            initialHandCount: 1,
            initialEnergy: 0,
            initialAmmo: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3223, scenario.SecondEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(1));
    }

    /// <summary>验证让我抽抽抽仅对自己增加五层烟雾，支付一费并进入弃牌堆，不会改写其他参与者。</summary>
    [Test]
    public void ChainSmoke_AddsFiveSourceSmokeAndDiscards()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3269 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3269, targetId: null);
        MachineGunnerPrivateStatusChangedSettlement smoke =
            FindSettlement<MachineGunnerPrivateStatusChangedSettlement>(result);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(1));
        Assert.That(smoke.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(smoke.Status, Is.EqualTo(MachineGunnerCombatantStatus.Smoke));
        Assert.That(smoke.ValueBefore, Is.Zero);
        Assert.That(smoke.ValueAfter, Is.EqualTo(5));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(5));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.Zero);
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.Zero);
        Assert.That(scenario.Player.CurrentBlock, Is.Zero);
        Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证二手烟把施放者当前烟雾层数转为目标毒素，且不消耗双方烟雾、不立即改写目标生命与格挡，并原子弃置自身。</summary>
    [Test]
    public void SecondhandSmoke_SourceSmokeAppliesPoisonWithoutChangingSmokeAndDiscards()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3270 },
            initialHandCount: 1,
            initialEnergy: 3,
            enemyDamage: 0);
        scenario.StartBattle();
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 4);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke, 3);
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single();
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3270, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
        Assert.That(terminal.Settlements, Has.Count.EqualTo(3));
        Assert.That(
            terminal.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 3)));

        var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
        var poison = terminal.Settlements[1] as BattleStatusAppliedSettlement;
        var departure = terminal.Settlements[2] as BattleCardMovedSettlement;
        Assert.That(energy, Is.Not.Null);
        Assert.That(
            (energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
            Is.EqualTo((3, 3, 0)));
        Assert.That(poison, Is.Not.Null);
        Assert.That(poison.Order, Is.EqualTo(1));
        Assert.That(poison.Status, Is.EqualTo(BattleStatusType.Poison));
        Assert.That(poison.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That(poison.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(poison.Amount, Is.EqualTo(4));
        Assert.That(poison.ValueBefore, Is.Zero);
        Assert.That(poison.ValueAfter, Is.EqualTo(4));
        Assert.That(departure, Is.Not.Null);
        Assert.That(
            (departure.CardId, departure.FromZone, departure.ToZone),
            Is.EqualTo((sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(4));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(3));
        Assert.That(scenario.Player.CurrentPoison, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(4));
        Assert.That(scenario.SecondEnemy.CurrentPoison, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.FirstEnemy.CurrentBlock, Is.Zero);
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
    }

    /// <summary>验证二手烟的同步中毒观察者即使临时改写卡区布局，命令仍以起点冻结布局原子提交归宿且不留下部分故障。</summary>
    [Test]
    public void SecondhandSmoke_PoisonObserverDriftsCardLayout_CommitsFrozenDepartureWithoutPartialFault()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3270, 3203 },
            initialHandCount: 2,
            initialEnergy: 3,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Smoke,
            4);
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3270);
        CardInstanceId otherCardId = scenario.Zones.Hand.Single(cardId => cardId != sourceCardId);
        int resultCountBefore = scenario.Results.Count;
        bool observerTriggered = false;
        BattleCardZoneOperationResult observerMove = null;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();
        using IDisposable poisonSubscription = scenario.FirstEnemy.Poison
            .Skip(1)
            .Subscribe(_ =>
            {
                if (observerTriggered)
                    return;

                observerTriggered = true;
                observerMove = scenario.Zones.DiscardFromHand(otherCardId);
            });

        BattleCommandSubmissionResult submission = scenario.Submit(3270, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(observerTriggered, Is.True);
        Assert.That(observerMove, Is.Not.Null);
        Assert.That(observerMove.Succeeded, Is.True);
        Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Settlements, Has.Count.EqualTo(3));
        Assert.That(terminal.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 3)));
        Assert.That(terminal.Settlements.Select(item => item.GetType()),
            Is.EqualTo(new[]
            {
                typeof(BattleEnergySpentSettlement),
                typeof(BattleStatusAppliedSettlement),
                typeof(BattleCardMovedSettlement),
            }));
        BattleStatusAppliedSettlement poison = terminal.Settlements
            .OfType<BattleStatusAppliedSettlement>()
            .Single();
        BattleCardMovedSettlement departure = terminal.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single();
        Assert.That((poison.Status, poison.Amount, poison.ValueBefore, poison.ValueAfter),
            Is.EqualTo((BattleStatusType.Poison, 4, 0, 4)));
        Assert.That((departure.CardId, departure.FromZone, departure.ToZone),
            Is.EqualTo((sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(4));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { otherCardId }));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(3));
        Assert.That(scenario.Queue.Queue.CurrentValue.IsFaulted, Is.False);
        Assert.That(scenario.Queue.Queue.CurrentValue.Fault, Is.Null);
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore + 1));
        Assert.That(scenario.Results[resultCountBefore].Succeeded, Is.True);
    }

    /// <summary>验证施放者零烟雾时二手烟仍成功且只支付零费用并弃置自身，不产生中毒结算或改写战斗事实。</summary>
    [Test]
    public void SecondhandSmoke_ZeroSourceSmoke_SucceedsWithoutPoisonSettlementAndDiscards()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3270 },
            initialHandCount: 1,
            initialEnergy: 3,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Player.ApplyBlockGain(5);
        scenario.FirstEnemy.ApplyBlockGain(4);
        scenario.SecondEnemy.ApplyBlockGain(3);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single();
        PlayerTurnData resourcesBefore =
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        int playerHealthBefore = scenario.Player.CurrentHealth;
        int playerBlockBefore = scenario.Player.CurrentBlock;
        int firstEnemyHealthBefore = scenario.FirstEnemy.CurrentHealth;
        int firstEnemyBlockBefore = scenario.FirstEnemy.CurrentBlock;
        int secondEnemyHealthBefore = scenario.SecondEnemy.CurrentHealth;
        int secondEnemyBlockBefore = scenario.SecondEnemy.CurrentBlock;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3270, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.Stage, Is.EqualTo(BattleCommandLifecycleStage.ExecutionCompleted));
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Settlements, Has.Count.EqualTo(2));
        Assert.That(
            terminal.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, 2)));
        Assert.That(
            terminal.Settlements.Select(settlement => settlement.GetType()),
            Is.EqualTo(new[]
            {
                typeof(BattleEnergySpentSettlement),
                typeof(BattleCardMovedSettlement),
            }));

        var energy = terminal.Settlements[0] as BattleEnergySpentSettlement;
        var departure = terminal.Settlements[1] as BattleCardMovedSettlement;
        Assert.That(energy, Is.Not.Null);
        Assert.That(
            (energy.Order, energy.EnergyBefore, energy.EnergyAfter, energy.Amount),
            Is.EqualTo((0, 3, 3, 0)));
        Assert.That(departure, Is.Not.Null);
        Assert.That(
            (departure.Order, departure.CardId, departure.FromZone, departure.ToZone),
            Is.EqualTo((1, sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile)));
        Assert.That(terminal.Settlements.OfType<BattleStatusAppliedSettlement>(), Is.Empty);

        Assert.That(scenario.Player.CurrentPoison, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.Zero);
        Assert.That(scenario.SecondEnemy.CurrentPoison, Is.Zero);
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.Zero);
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke), Is.Zero);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(playerHealthBefore));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(playerBlockBefore));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(firstEnemyHealthBefore));
        Assert.That(scenario.FirstEnemy.CurrentBlock, Is.EqualTo(firstEnemyBlockBefore));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(secondEnemyHealthBefore));
        Assert.That(scenario.SecondEnemy.CurrentBlock, Is.EqualTo(secondEnemyBlockBefore));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(resourcesBefore.Energy));
        Assert.That(
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(resourcesBefore.Ammo));
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
    }

    /// <summary>验证非致死中毒在敌人行动开始时绕过格挡扣血并减一层，随后仍清除格挡、执行行为并推进意图。</summary>
    [Test]
    public void PoisonedEnemyTurnStart_NonFatalBypassesBlockThenActsAndAdvancesIntent()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3270 },
            initialHandCount: 1,
            firstEnemyHealth: 10,
            enemyDamage: 2,
            initialEnergy: 3);
        scenario.StartBattle();
        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Smoke,
            4);
        scenario.Play(3270, scenario.FirstEnemy.Id);
        // 本用例只验证中毒时序，避免施毒来源的 Smoke 抵消随后敌人攻击。
        scenario.Session.MachineGunnerRuntime.CombatState.Set(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Smoke,
            0);
        scenario.FirstEnemy.ApplyBlockGain(5);
        int resultCountBeforeEnd = scenario.Results.Count;

        scenario.EndPlayerAction();

        BattleCommandExecutionResult firstEnemyAction = scenario.Results
            .Skip(resultCountBeforeEnd)
            .Single(result =>
                result.CommandType == BattleCommandType.CompleteEnemyAction &&
                result.Settlements.OfType<BattleDamageAppliedSettlement>().Any(damage =>
                    damage.SourceId == scenario.FirstEnemy.Id));
        Assert.That(firstEnemyAction.Succeeded, Is.True);
        Assert.That(
            firstEnemyAction.Settlements.Select(item => item.GetType().Name),
            Is.EqualTo(new[]
            {
                "BattlePoisonTickedSettlement",
                nameof(BattleBlockClearedSettlement),
                nameof(BattleDamageAppliedSettlement),
                nameof(BattleEnemyIntentAdvancedSettlement),
                nameof(BattlePhaseChangedSettlement),
            }));
        Assert.That(
            firstEnemyAction.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 5)));

        BattleSettlementRecord poison = firstEnemyAction.Settlements[0];
        Assert.That(poison.EffectId, Is.Null);
        Assert.That(poison.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(poison.GetType().GetProperty("HealthBefore")?.GetValue(poison), Is.EqualTo(10));
        Assert.That(poison.GetType().GetProperty("HealthAfter")?.GetValue(poison), Is.EqualTo(6));
        Assert.That(poison.GetType().GetProperty("HealthLoss")?.GetValue(poison), Is.EqualTo(4));
        Assert.That(poison.GetType().GetProperty("BlockBefore")?.GetValue(poison), Is.EqualTo(5));
        Assert.That(poison.GetType().GetProperty("BlockAfter")?.GetValue(poison), Is.EqualTo(5));
        Assert.That(poison.GetType().GetProperty("PoisonBefore")?.GetValue(poison), Is.EqualTo(4));
        Assert.That(poison.GetType().GetProperty("PoisonAfter")?.GetValue(poison), Is.EqualTo(3));
        Assert.That(poison.GetType().GetProperty("WasFatal")?.GetValue(poison), Is.False);

        var cleared = firstEnemyAction.Settlements[1] as BattleBlockClearedSettlement;
        var damage = firstEnemyAction.Settlements[2] as BattleDamageAppliedSettlement;
        var intent = firstEnemyAction.Settlements[3] as BattleEnemyIntentAdvancedSettlement;
        var phase = firstEnemyAction.Settlements[4] as BattlePhaseChangedSettlement;
        Assert.That(cleared, Is.Not.Null);
        Assert.That((cleared.BlockBefore, cleared.BlockAfter), Is.EqualTo((5, 0)));
        Assert.That(damage, Is.Not.Null);
        Assert.That(damage.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damage.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(damage.AttackValue, Is.EqualTo(2));
        Assert.That(intent, Is.Not.Null);
        Assert.That(intent.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(phase, Is.Not.Null);
        Assert.That(phase.CurrentActingEnemyIdBefore, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(phase.CurrentActingEnemyIdAfter, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(6));
        Assert.That(scenario.FirstEnemy.CurrentBlock, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(3));
    }

    /// <summary>验证致死中毒绕过格挡并减层后跳过敌人行动与意图推进，同时保留下一名敌人的正常续接。</summary>
    [Test]
    public void PoisonedEnemyTurnStart_FatalBypassesBlockDecrementsThenSkipsWithoutAdvancingIntentAndContinues()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3270 },
            initialHandCount: 1,
            firstEnemyHealth: 3,
            enemyDamage: 2,
            initialEnergy: 3);
        scenario.StartBattle();
        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Smoke,
            4);
        scenario.Play(3270, scenario.FirstEnemy.Id);
        // 本用例只验证中毒致死与续接，避免施毒来源的 Smoke 抵消第二名敌人的攻击。
        scenario.Session.MachineGunnerRuntime.CombatState.Set(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Smoke,
            0);
        scenario.FirstEnemy.ApplyBlockGain(5);
        BattleEnemyIntentAuthoritySnapshot firstIntentBefore =
            scenario.Session.EnemyIntents.CaptureAuthoritySnapshot(scenario.FirstEnemy.Id);
        int resultCountBeforeEnd = scenario.Results.Count;

        scenario.EndPlayerAction();

        BattleCommandExecutionResult firstEnemyAction = scenario.Results
            .Skip(resultCountBeforeEnd)
            .Single(result =>
                result.CommandType == BattleCommandType.CompleteEnemyAction &&
                result.Settlements.OfType<BattlePoisonTickedSettlement>().Any(tick =>
                    tick.TargetId == scenario.FirstEnemy.Id));
        Assert.That(firstEnemyAction.Succeeded, Is.True);
        Assert.That(
            firstEnemyAction.Settlements.Select(item => item.GetType()),
            Is.EqualTo(new[]
            {
                typeof(BattlePoisonTickedSettlement),
                typeof(BattleEnemyActionSkippedSettlement),
                typeof(BattlePhaseChangedSettlement),
            }));
        Assert.That(
            firstEnemyAction.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 3)));

        var poison = firstEnemyAction.Settlements[0] as BattlePoisonTickedSettlement;
        var skipped = firstEnemyAction.Settlements[1] as BattleEnemyActionSkippedSettlement;
        var phase = firstEnemyAction.Settlements[2] as BattlePhaseChangedSettlement;
        Assert.That(poison, Is.Not.Null);
        Assert.That(poison.EffectId, Is.Null);
        Assert.That(poison.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(poison.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That((poison.HealthBefore, poison.HealthAfter, poison.HealthLoss),
            Is.EqualTo((3, 0, 3)));
        Assert.That((poison.BlockBefore, poison.BlockAfter), Is.EqualTo((5, 5)));
        Assert.That((poison.PoisonBefore, poison.PoisonAfter), Is.EqualTo((4, 3)));
        Assert.That(poison.WasFatal, Is.True);
        Assert.That(skipped, Is.Not.Null);
        Assert.That(skipped.SourceId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(skipped.TargetId, Is.Null);
        Assert.That(skipped.EffectId, Is.Null);
        Assert.That(skipped.Reason, Is.EqualTo(BattleEnemyActionSkipReason.SourceNotAlive));
        Assert.That(phase, Is.Not.Null);
        Assert.That(phase.CurrentActingEnemyIdBefore, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(phase.CurrentActingEnemyIdAfter, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(firstEnemyAction.Settlements.OfType<BattleBlockClearedSettlement>(), Is.Empty);
        Assert.That(firstEnemyAction.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(firstEnemyAction.Settlements.OfType<BattleEnemyIntentAdvancedSettlement>(), Is.Empty);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentBlock, Is.EqualTo(5));
        Assert.That(scenario.FirstEnemy.CurrentPoison, Is.EqualTo(3));

        BattleEnemyIntentAuthoritySnapshot firstIntentAfter =
            scenario.Session.EnemyIntents.CaptureAuthoritySnapshot(scenario.FirstEnemy.Id);
        Assert.That(firstIntentAfter.CurrentBehaviorId, Is.EqualTo(firstIntentBefore.CurrentBehaviorId));
        Assert.That(firstIntentAfter.History.LastCompletedBehaviorId,
            Is.EqualTo(firstIntentBefore.History.LastCompletedBehaviorId));
        Assert.That(firstIntentAfter.History.ConsecutiveCompletedCount,
            Is.EqualTo(firstIntentBefore.History.ConsecutiveCompletedCount));
        Assert.That(firstIntentAfter.History.CooldownsByBehaviorId,
            Is.EqualTo(firstIntentBefore.History.CooldownsByBehaviorId));

        BattleCommandExecutionResult secondEnemyAction = scenario.Results
            .Skip(resultCountBeforeEnd)
            .Single(result =>
                result.CommandType == BattleCommandType.CompleteEnemyAction &&
                result.Settlements.OfType<BattleDamageAppliedSettlement>().Any(damage =>
                    damage.SourceId == scenario.SecondEnemy.Id));
        BattleDamageAppliedSettlement secondDamage = secondEnemyAction.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Single(damage => damage.SourceId == scenario.SecondEnemy.Id);
        BattleEnemyIntentAdvancedSettlement secondIntent = secondEnemyAction.Settlements
            .OfType<BattleEnemyIntentAdvancedSettlement>()
            .Single();
        Assert.That(secondEnemyAction.Succeeded, Is.True);
        Assert.That(secondDamage.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(secondDamage.AttackValue, Is.EqualTo(2));
        Assert.That(secondIntent.SourceId, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(68));
    }

    /// <summary>验证最后敌人完成后先结算致死中毒，再以新轮次结束战斗且不执行任何玩家回合重置。</summary>
    [Test]
    public void CompleteLastEnemy_PlayerRoundStartFatalPoisonTicksBeforeResetAndEndsBattle()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3202 },
            initialHandCount: 1,
            firstEnemyHealth: 6,
            enemyDamage: 1,
            initialEnergy: 3);
        scenario.StartBattle();
        BattleEffectStateTestDriver.ApplyDamage(
            scenario.Session.Combatants,
            scenario.SecondEnemy.Id,
            scenario.Player.Id,
            configuredValue: 66);
        scenario.Player.ApplyBlockGain(5);
        scenario.Player.ApplyPoisonValue(expectedBefore: 0, valueAfter: 4);

        BattleCommandExecutionResult played = scenario.Play(3202, targetId: null);
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(played).Amount, Is.EqualTo(1));
        Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
        PlayerTurnData playerBeforeEnd = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        Assert.That(playerBeforeEnd.Energy, Is.EqualTo(2));
        Assert.That(playerBeforeEnd.HasEndedAction, Is.False);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(4));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(5));
        Assert.That(scenario.Player.CurrentPoison, Is.EqualTo(4));
        CardInstanceId[] drawPileBeforeEnd = scenario.Zones.DrawPile.ToArray();
        CardInstanceId[] handBeforeEnd = scenario.Zones.Hand.ToArray();
        CardInstanceId[] discardPileBeforeEnd = scenario.Zones.DiscardPile.ToArray();
        CardInstanceId[] exhaustPileBeforeEnd = scenario.Zones.ExhaustPile.ToArray();
        CardInstanceId[] powerPileBeforeEnd = scenario.Zones.PowerPile.ToArray();
        uint shuffleRandomBeforeEnd = scenario.Zones.ShuffleRandomState;
        int resultCountBeforeEnd = scenario.Results.Count;

        scenario.EndPlayerAction();

        BattleCommandExecutionResult lastEnemyAction = scenario.Results
            .Skip(resultCountBeforeEnd)
            .Single(result =>
                result.CommandType == BattleCommandType.CompleteEnemyAction &&
                result.Settlements.OfType<BattleDamageAppliedSettlement>().Any(damage =>
                    damage.SourceId == scenario.SecondEnemy.Id));
        BattleDamageAppliedSettlement enemyDamage = lastEnemyAction.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Single(damage => damage.SourceId == scenario.SecondEnemy.Id);
        BattleEnemyIntentAdvancedSettlement intent = lastEnemyAction.Settlements
            .OfType<BattleEnemyIntentAdvancedSettlement>()
            .Single();
        BattlePoisonTickedSettlement poison = lastEnemyAction.Settlements
            .OfType<BattlePoisonTickedSettlement>()
            .Single(tick => tick.TargetId == scenario.Player.Id);
        BattlePhaseChangedSettlement phase = lastEnemyAction.Settlements
            .OfType<BattlePhaseChangedSettlement>()
            .Single();

        Assert.That(lastEnemyAction.Succeeded, Is.True);
        Assert.That(enemyDamage.TargetId, Is.EqualTo(scenario.Player.Id));
        Assert.That(enemyDamage.AttackValue, Is.EqualTo(1));
        Assert.That((enemyDamage.BlockBefore, enemyDamage.BlockAfter, enemyDamage.BlockAbsorbed),
            Is.EqualTo((5, 4, 1)));
        Assert.That((enemyDamage.HealthBefore, enemyDamage.HealthAfter, enemyDamage.HealthLoss),
            Is.EqualTo((4, 4, 0)));
        Assert.That(intent.SourceId, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(poison.EffectId, Is.Null);
        Assert.That(poison.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That((poison.HealthBefore, poison.HealthAfter, poison.HealthLoss),
            Is.EqualTo((4, 0, 4)));
        Assert.That((poison.BlockBefore, poison.BlockAfter), Is.EqualTo((4, 4)));
        Assert.That((poison.PoisonBefore, poison.PoisonAfter), Is.EqualTo((4, 3)));
        Assert.That(poison.WasFatal, Is.True);
        Assert.That(enemyDamage.Order, Is.LessThan(intent.Order));
        Assert.That(intent.Order, Is.LessThan(poison.Order));
        Assert.That(poison.Order, Is.LessThan(phase.Order));
        Assert.That((phase.PhaseBefore, phase.PhaseAfter),
            Is.EqualTo((BattleTurnPhase.EnemyAction, BattleTurnPhase.BattleEnded)));
        Assert.That((phase.RoundNumberBefore, phase.RoundNumberAfter), Is.EqualTo((1, 2)));
        Assert.That(phase.CurrentActingEnemyIdBefore, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(phase.CurrentActingEnemyIdAfter, Is.Null);

        PlayerTurnData playerAfterFatalTick = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.BattleEnded));
        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(playerAfterFatalTick.Energy, Is.EqualTo(2));
        Assert.That(playerAfterFatalTick.HasEndedAction, Is.True);
        Assert.That(scenario.Player.CurrentHealth, Is.Zero);
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(4));
        Assert.That(scenario.Player.CurrentPoison, Is.EqualTo(3));
        Assert.That(scenario.Zones.DrawPile, Is.EqualTo(drawPileBeforeEnd));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(handBeforeEnd));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardPileBeforeEnd));
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(exhaustPileBeforeEnd));
        Assert.That(scenario.Zones.PowerPile, Is.EqualTo(powerPileBeforeEnd));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBeforeEnd));
        Assert.That(lastEnemyAction.Settlements.OfType<BattleBlockClearedSettlement>(), Is.Empty);
        Assert.That(lastEnemyAction.Settlements.OfType<BattleEnergyRefilledSettlement>(), Is.Empty);
        Assert.That(lastEnemyAction.Settlements.OfType<BattleAmmoRefilledSettlement>(), Is.Empty);
        Assert.That(lastEnemyAction.Settlements.OfType<BattleCardMovedSettlement>(), Is.Empty);
        Assert.That(lastEnemyAction.Settlements.OfType<BattleCardsReshuffledSettlement>(), Is.Empty);
    }

    /// <summary>验证最后敌人完成后，非致死中毒先于格挡清理、职业重置、资源补给与抽牌结算，并进入新一轮玩家行动。</summary>
    [Test]
    public void CompleteLastEnemy_PlayerRoundStartNonfatalPoisonTicksBeforeBlockResourcesAndDraw()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3202 },
            initialHandCount: 1,
            firstEnemyHealth: 6,
            enemyDamage: 1,
            initialEnergy: 3,
            initialAmmo: 2,
            ammoMaximum: 5);
        scenario.StartBattle();
        BattleEffectStateTestDriver.ApplyDamage(
            scenario.Session.Combatants,
            scenario.SecondEnemy.Id,
            scenario.Player.Id,
            configuredValue: 60);
        scenario.Player.ApplyBlockGain(5);
        scenario.Player.ApplyPoisonValue(expectedBefore: 0, valueAfter: 4);

        BattleCommandExecutionResult played = scenario.Play(3202, targetId: null);
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(played).Amount, Is.EqualTo(1));
        Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.NextRoundBlock,
            2);
        int resultCountBeforeEnd = scenario.Results.Count;

        scenario.EndPlayerAction();

        BattleCommandExecutionResult lastEnemyAction = scenario.Results
            .Skip(resultCountBeforeEnd)
            .Single(result =>
                result.CommandType == BattleCommandType.CompleteEnemyAction &&
                result.Settlements.OfType<BattleDamageAppliedSettlement>().Any(damage =>
                    damage.SourceId == scenario.SecondEnemy.Id));
        BattleDamageAppliedSettlement enemyDamage = lastEnemyAction.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Single(damage => damage.SourceId == scenario.SecondEnemy.Id);
        BattleEnemyIntentAdvancedSettlement intent = lastEnemyAction.Settlements
            .OfType<BattleEnemyIntentAdvancedSettlement>()
            .Single();
        BattlePoisonTickedSettlement poison = lastEnemyAction.Settlements
            .OfType<BattlePoisonTickedSettlement>()
            .Single(tick => tick.TargetId == scenario.Player.Id);
        BattleBlockClearedSettlement blockClear = lastEnemyAction.Settlements
            .OfType<BattleBlockClearedSettlement>()
            .Single();
        MachineGunnerPrivateStatusChangedSettlement nextRoundBlockClear =
            lastEnemyAction.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .Single(change =>
                    change.Status == MachineGunnerCombatantStatus.NextRoundBlock);
        BattleBlockGainedSettlement blockGain = lastEnemyAction.Settlements
            .OfType<BattleBlockGainedSettlement>()
            .Single();
        BattleEnergyRefilledSettlement energyRefill = lastEnemyAction.Settlements
            .OfType<BattleEnergyRefilledSettlement>()
            .Single();
        BattleAmmoRefilledSettlement ammoRefill = lastEnemyAction.Settlements
            .OfType<BattleAmmoRefilledSettlement>()
            .Single();
        BattleCardMovedSettlement draw = lastEnemyAction.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(move =>
                move.FromZone == BattleCardZone.DrawPile &&
                move.ToZone == BattleCardZone.Hand);
        BattlePhaseChangedSettlement phase = lastEnemyAction.Settlements
            .OfType<BattlePhaseChangedSettlement>()
            .Single();

        Assert.That(lastEnemyAction.Succeeded, Is.True);
        Assert.That((enemyDamage.BlockBefore, enemyDamage.BlockAfter, enemyDamage.BlockAbsorbed),
            Is.EqualTo((5, 4, 1)));
        Assert.That((enemyDamage.HealthBefore, enemyDamage.HealthAfter, enemyDamage.HealthLoss),
            Is.EqualTo((10, 10, 0)));
        Assert.That(intent.SourceId, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That((poison.HealthBefore, poison.HealthAfter, poison.HealthLoss),
            Is.EqualTo((10, 6, 4)));
        Assert.That((poison.BlockBefore, poison.BlockAfter), Is.EqualTo((4, 4)));
        Assert.That((poison.PoisonBefore, poison.PoisonAfter), Is.EqualTo((4, 3)));
        Assert.That(poison.WasFatal, Is.False);
        Assert.That((blockClear.BlockBefore, blockClear.BlockAfter, blockClear.Amount),
            Is.EqualTo((4, 0, 4)));
        Assert.That(
            (nextRoundBlockClear.ValueBefore, nextRoundBlockClear.ValueAfter),
            Is.EqualTo((2, 0)));
        Assert.That((blockGain.BlockBefore, blockGain.BlockAfter, blockGain.Amount),
            Is.EqualTo((0, 2, 2)));
        Assert.That((energyRefill.EnergyBefore, energyRefill.EnergyAfter, energyRefill.Amount),
            Is.EqualTo((2, 5, 3)));
        Assert.That((ammoRefill.AmmoBefore, ammoRefill.AmmoAfter, ammoRefill.Amount),
            Is.EqualTo((2, 3, 1)));
        Assert.That(enemyDamage.Order, Is.LessThan(intent.Order));
        Assert.That(intent.Order, Is.LessThan(poison.Order));
        Assert.That(poison.Order, Is.LessThan(blockClear.Order));
        Assert.That(blockClear.Order, Is.LessThan(nextRoundBlockClear.Order));
        Assert.That(nextRoundBlockClear.Order, Is.LessThan(blockGain.Order));
        Assert.That(blockGain.Order, Is.LessThan(energyRefill.Order));
        Assert.That(energyRefill.Order, Is.LessThan(ammoRefill.Order));
        Assert.That(ammoRefill.Order, Is.LessThan(draw.Order));
        Assert.That(draw.Order, Is.LessThan(phase.Order));
        Assert.That((phase.PhaseBefore, phase.PhaseAfter),
            Is.EqualTo((BattleTurnPhase.EnemyAction, BattleTurnPhase.PlayerAction)));
        Assert.That((phase.RoundNumberBefore, phase.RoundNumberAfter), Is.EqualTo((1, 2)));

        PlayerTurnData playerAfterReset =
            scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(playerAfterReset.Energy, Is.EqualTo(5));
        Assert.That(playerAfterReset.Ammo, Is.EqualTo(3));
        Assert.That(playerAfterReset.HasEndedAction, Is.False);
        Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(6));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(2));
        Assert.That(scenario.Player.CurrentPoison, Is.EqualTo(3));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DrawPile, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
    }

    /// <summary>验证紧急散热严格先获得八点格挡再获得三层烟雾，并在能量不足时保持资源、状态、卡区和随机流零写入。</summary>
    [Test]
    public void EmergencyCooling_GainsBlockBeforeSmokeAndEnergyFailureWritesNothing()
    {
        using (var successScenario = new MachineGunnerStarterScenario(
                   new[] { 3272 },
                   initialHandCount: 1,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            successScenario.StartBattle();

            BattleCommandExecutionResult result = successScenario.Play(3272, targetId: null);
            BattleBlockGainedSettlement block = FindSettlement<BattleBlockGainedSettlement>(result);
            MachineGunnerPrivateStatusChangedSettlement smoke =
                FindSettlement<MachineGunnerPrivateStatusChangedSettlement>(result);

            Assert.That(block.TargetId, Is.EqualTo(successScenario.Player.Id));
            Assert.That(block.BlockBefore, Is.Zero);
            Assert.That(block.BlockAfter, Is.EqualTo(8));
            Assert.That(smoke.TargetId, Is.EqualTo(successScenario.Player.Id));
            Assert.That(smoke.Status, Is.EqualTo(MachineGunnerCombatantStatus.Smoke));
            Assert.That(smoke.ValueBefore, Is.Zero);
            Assert.That(smoke.ValueAfter, Is.EqualTo(3));
            Assert.That(block.Order, Is.LessThan(smoke.Order));
            Assert.That(
                result.Settlements
                    .Where(item => item is BattleBlockGainedSettlement ||
                        item is MachineGunnerPrivateStatusChangedSettlement)
                    .Select(item => item.GetType()),
                Is.EqualTo(new[]
                {
                    typeof(BattleBlockGainedSettlement),
                    typeof(MachineGunnerPrivateStatusChangedSettlement),
                }));
            Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(1));
            Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        }

        using var failedScenario = new MachineGunnerStarterScenario(
            new[] { 3272 },
            initialHandCount: 1,
            initialEnergy: 0,
            enemyDamage: 0);
        failedScenario.StartBattle();
        int resultCountBefore = failedScenario.Results.Count;
        uint randomBefore = failedScenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            failedScenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = failedScenario.Submit(3272, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(failedScenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(failedScenario.Player.CurrentBlock, Is.Zero);
        Assert.That(
            failedScenario.Session.MachineGunnerRuntime.CombatState.Get(
                failedScenario.Player.Id,
                MachineGunnerCombatantStatus.Smoke),
            Is.Zero);
        Assert.That(failedScenario.Queue.Turn.CurrentValue.Players[failedScenario.Player.Id].Energy, Is.Zero);
        Assert.That(failedScenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(failedScenario.Zones.DiscardPile, Is.Empty);
        Assert.That(failedScenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证标记支付一发弹药后先造成五点普通攻击伤害，再仅对仍存活目标施加两层破甲。</summary>
    [Test]
    public void Mark_DamagesBeforeApplyingArmorBreakToLivingTarget()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3280 },
            initialHandCount: 1,
            firstEnemyHealth: 20,
            initialEnergy: 0,
            initialAmmo: 1,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3280, scenario.FirstEnemy.Id);
        BattleDamageAppliedSettlement damage = FindSettlement<BattleDamageAppliedSettlement>(result);
        MachineGunnerPrivateStatusChangedSettlement armorBreak =
            FindSettlement<MachineGunnerPrivateStatusChangedSettlement>(result);

        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(result).Amount, Is.EqualTo(1));
        Assert.That(damage.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damage.AttackValue, Is.EqualTo(5));
        Assert.That(damage.HealthBefore, Is.EqualTo(20));
        Assert.That(damage.HealthAfter, Is.EqualTo(15));
        Assert.That(armorBreak.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(armorBreak.Status, Is.EqualTo(MachineGunnerCombatantStatus.ArmorBreak));
        Assert.That(armorBreak.ValueBefore, Is.Zero);
        Assert.That(armorBreak.ValueAfter, Is.EqualTo(2));
        Assert.That(armorBreak.Order, Is.EqualTo(damage.Order + 1));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证标记的致死命中不再给死亡目标写破甲，弹药不足则不写伤害、状态、卡区或随机流。</summary>
    [Test]
    public void Mark_LethalHitSkipsArmorBreakAndAmmoFailureWritesNothing()
    {
        using (var lethalScenario = new MachineGunnerStarterScenario(
                   new[] { 3280 },
                   initialHandCount: 1,
                   firstEnemyHealth: 5,
                   initialEnergy: 0,
                   initialAmmo: 1,
                   enemyDamage: 0))
        {
            lethalScenario.StartBattle();

            BattleCommandExecutionResult lethal = lethalScenario.Play(3280, lethalScenario.FirstEnemy.Id);

            Assert.That(lethalScenario.FirstEnemy.IsAlive, Is.False);
            Assert.That(FindSettlement<BattleDamageAppliedSettlement>(lethal).WasFatal, Is.True);
            Assert.That(lethal.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
            Assert.That(
                lethalScenario.Session.MachineGunnerRuntime.CombatState.Get(
                    lethalScenario.FirstEnemy.Id,
                    MachineGunnerCombatantStatus.ArmorBreak),
                Is.Zero);
        }

        using var failedScenario = new MachineGunnerStarterScenario(
            new[] { 3280 },
            initialHandCount: 1,
            initialEnergy: 0,
            initialAmmo: 0,
            enemyDamage: 0);
        failedScenario.StartBattle();
        int resultCountBefore = failedScenario.Results.Count;
        uint randomBefore = failedScenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            failedScenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission =
            failedScenario.Submit(3280, failedScenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(failedScenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(failedScenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(
            failedScenario.Session.MachineGunnerRuntime.CombatState.Get(
                failedScenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.ArmorBreak),
            Is.Zero);
        Assert.That(failedScenario.Queue.Turn.CurrentValue.Players[failedScenario.Player.Id].Ammo, Is.Zero);
        Assert.That(failedScenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(failedScenario.Zones.DiscardPile, Is.Empty);
        Assert.That(failedScenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证标记显式使用无射击标签，不获得兴奋剂额外段、火力加成或燃烧弹药状态。</summary>
    [Test]
    public void Mark_NoneTagsIgnoreStimFirePowerAndIncendiaryAmmo()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3210, 3236, 3205, 3280 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            initialEnergy: 3,
            initialAmmo: 5,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3210, targetId: null);
        scenario.Play(3236, targetId: null);
        scenario.Play(3205, targetId: null);

        BattleCommandExecutionResult result = scenario.Play(3280, scenario.FirstEnemy.Id);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;

        Assert.That(MachineGunnerCardProgramRegistry.TryGet(
            cfg.battle.MachineGunnerProgramId.Mark,
            out MachineGunnerCardProgram program), Is.True);
        Assert.That(program.IsAttack, Is.True);
        Assert.That(program.Tags, Is.EqualTo(MachineGunnerCardTag.None));
        Assert.That(program.IsShootCategory, Is.False);
        Assert.That(program.ReceivesStimBonus, Is.False);
        Assert.That(program.ReceivesIncendiaryAmmo, Is.False);
        Assert.That(result.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(1));
        Assert.That(FindSettlement<BattleDamageAppliedSettlement>(result).AttackValue, Is.EqualTo(5));
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(result).Amount, Is.EqualTo(1));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(4));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.ArmorBreak), Is.EqualTo(2));
    }

    /// <summary>验证铝热炸弹按 Encounter 顺序先施加燃烧及其浸油变化，再施加持久破甲，并让下一回合末燃烧读取破甲。</summary>
    [Test]
    public void ThermiteBomb_AppliesBurnThenArmorBreakToAllEnemiesAndAmplifiesLaterBurn()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3273 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 1);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 2);
        state.Add(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);
        scenario.SecondEnemy.ApplyVulnerableGain(1);

        BattleCommandExecutionResult result = scenario.Play(3273, targetId: null);
        MachineGunnerPrivateStatusChangedSettlement[] statusChanges = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn ||
                item.Status == MachineGunnerCombatantStatus.Oil ||
                item.Status == MachineGunnerCombatantStatus.ArmorBreak)
            .ToArray();

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(1));
        Assert.That(
            statusChanges.Select(item => $"{item.TargetId}/{item.Status}"),
            Is.EqualTo(new[]
            {
                $"{scenario.FirstEnemy.Id}/{MachineGunnerCombatantStatus.Burn}",
                $"{scenario.FirstEnemy.Id}/{MachineGunnerCombatantStatus.Oil}",
                $"{scenario.SecondEnemy.Id}/{MachineGunnerCombatantStatus.Burn}",
                $"{scenario.SecondEnemy.Id}/{MachineGunnerCombatantStatus.Oil}",
                $"{scenario.FirstEnemy.Id}/{MachineGunnerCombatantStatus.ArmorBreak}",
                $"{scenario.SecondEnemy.Id}/{MachineGunnerCombatantStatus.ArmorBreak}",
            }));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(6));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(7));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(1));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(1));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.ArmorBreak), Is.EqualTo(2));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.ArmorBreak), Is.EqualTo(2));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(result.Settlements.Select(item => item.Order), Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));

        BattleCommandExecutionResult end = scenario.EndPlayerActionResult();
        BattleDamageAppliedSettlement[] burnDamages = end.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(burnDamages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(burnDamages.Select(item => item.AttackValue), Is.EqualTo(new[] { 8, 10 }));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.ArmorBreak), Is.EqualTo(2));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.ArmorBreak), Is.EqualTo(2));
    }

    /// <summary>验证铝热炸弹能量不足时没有状态、卡区或随机写入。</summary>
    [Test]
    public void ThermiteBomb_InsufficientEnergyFailsWithoutWritingStatusesOrZones()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3273 },
            initialHandCount: 1,
            initialEnergy: 0,
            enemyDamage: 0);
        scenario.StartBattle();

        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3273, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Burn),
            Is.Zero);
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.SecondEnemy.Id,
                MachineGunnerCombatantStatus.ArmorBreak),
            Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证焚风把施术者五层烟雾与目标三层浸油都计入燃烧，随后依次折半浸油、清空烟雾并弃牌。</summary>
    [Test]
    public void FoehnWind_ConsumesSmokeAfterBurnAndOilThenDiscards()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3276 },
            initialHandCount: 1,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 5);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 2);

        BattleCommandExecutionResult result = scenario.Play(3276, scenario.FirstEnemy.Id);
        MachineGunnerPrivateStatusChangedSettlement[] statuses = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();
        BattleCardMovedSettlement cardMoved = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(2));
        Assert.That(
            statuses.Select(item => $"{item.TargetId}/{item.Status}/{item.ValueBefore}/{item.ValueAfter}"),
            Is.EqualTo(new[]
            {
                $"{scenario.FirstEnemy.Id}/{MachineGunnerCombatantStatus.Burn}/2/10",
                $"{scenario.FirstEnemy.Id}/{MachineGunnerCombatantStatus.Oil}/3/1",
                $"{scenario.Player.Id}/{MachineGunnerCombatantStatus.Smoke}/5/0",
            }));
        Assert.That(statuses[0].Order, Is.LessThan(statuses[1].Order));
        Assert.That(statuses[1].Order, Is.LessThan(statuses[2].Order));
        Assert.That(statuses[2].Order, Is.LessThan(cardMoved.Order));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(10));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(1));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.Zero);
        Assert.That(cardMoved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证没有烟雾时焚风仍正常支付两费并弃牌，但不制造任何私有状态变化。</summary>
    [Test]
    public void FoehnWind_ZeroSmokeStillPaysEnergyAndDiscardsWithoutStatusChanges()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3276 },
            initialHandCount: 1,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3276, scenario.FirstEnemy.Id);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(2));
        Assert.That(result.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证能量不足时焚风不改变能量、私有状态、随机流或卡区。</summary>
    [Test]
    public void FoehnWind_InsufficientEnergyLeavesStatusesAndZonesUnchanged()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3276 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 5);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 2);
        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3276, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(1));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(5));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(3));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(2));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证把焚风指向玩家时以目标规则不匹配拒绝，并保持资源、私有状态、随机流和卡区零写入。</summary>
    [Test]
    public void FoehnWind_PlayerTargetLeavesResourcesStatusesAndZonesUnchanged()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3276 },
            initialHandCount: 1,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 5);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 3);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 2);
        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3276, scenario.Player.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(2));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke), Is.EqualTo(5));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(3));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(2));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证踏碎自动命中最近敌人，在普通攻击结算后才施加破甲并进入弃牌堆。</summary>
    [Test]
    public void Crush_AutomaticallyHitsNearestThenAppliesArmorBreakAndDiscards()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3281 },
            initialHandCount: 1,
            firstEnemyHealth: 20,
            secondEnemyHealth: 20,
            enemyDamage: 0,
            initialEnergy: 1);
        scenario.StartBattle();
        scenario.FirstEnemy.ApplyBlockGain(2);

        BattleCommandExecutionResult result = scenario.Play(3281, targetId: null);
        BattleDamageAppliedSettlement damage = FindSettlement<BattleDamageAppliedSettlement>(result);
        MachineGunnerPrivateStatusChangedSettlement armorBreak = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.ArmorBreak);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(1));
        Assert.That(damage.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damage.AttackValue, Is.EqualTo(9));
        Assert.That(damage.BlockBefore, Is.EqualTo(2));
        Assert.That(damage.BlockAfter, Is.Zero);
        Assert.That(damage.HealthBefore, Is.EqualTo(20));
        Assert.That(damage.HealthAfter, Is.EqualTo(13));
        Assert.That(armorBreak.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(armorBreak.ValueBefore, Is.Zero);
        Assert.That(armorBreak.ValueAfter, Is.EqualTo(4));
        Assert.That(damage.Order, Is.LessThan(armorBreak.Order));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.SecondEnemy.Id,
                MachineGunnerCombatantStatus.ArmorBreak),
            Is.Zero);
        Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证连肘只在上一张成功卡是非射击攻击时免费，连续连肘可延续且新回合会重置该条件。</summary>
    [Test]
        public void ComboElbow_IsFreeAfterNonShootAttackAndResetsAtNextPlayerRound()
        {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3202, 3242, 3242 },
            initialHandCount: 3,
            firstEnemyHealth: 100,
            enemyDamage: 0);
        scenario.StartBattle();

        scenario.Play(3202, targetId: null);
        BattleCommandExecutionResult firstCombo = scenario.Play(3242, targetId: null);
        BattleCommandExecutionResult secondCombo = scenario.Play(3242, targetId: null);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(firstCombo).Amount, Is.Zero);
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(secondCombo).Amount, Is.Zero);

        scenario.EndPlayerAction();
        BattleCommandExecutionResult nextRoundCombo = scenario.Play(3242, targetId: null);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(nextRoundCombo).Amount, Is.EqualTo(2));
    }

    /// <summary>验证剩余零能量时，连肘仍由规则层和队首提交共用的免费成本预览放行，不会被通用静态费用提前拒绝。</summary>
    [Test]
    public void ComboElbow_FreePreview_AllowsZeroEnergyAfterNonShootAttack()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3202, 3242 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            initialEnergy: 1);
        scenario.StartBattle();

        scenario.Play(3202, targetId: null);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);

        BattleCommandExecutionResult combo = scenario.Play(3242, targetId: null);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(combo).Amount, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
    }

    /// <summary>确认弹药不足的射击会经队列以零写入失败，既不移牌也不改变敌方生命。</summary>
    [Test]
    public void Shoot_InsufficientAmmo_FailsWithoutChangingZonesOrCombatants()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3201, 3201, 3201, 3201, 3201, 3201 },
            initialHandCount: 6,
            firstEnemyHealth: 100);
        scenario.StartBattle();

        for (int index = 0; index < 5; index++)
            scenario.Play(3201, scenario.FirstEnemy.Id);

        int healthBefore = scenario.FirstEnemy.CurrentHealth;
        int handBefore = scenario.Zones.Hand.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = scenario.Submit(3201, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(healthBefore));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(handBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.Zero);
    }

    /// <summary>验证无延迟、无选择的固定费用 MG5 程序通过职业运行时完成资源、目标、抽牌和弃牌结算。</summary>
    [Test]
    public void MG5FixedPrograms_ResolveThroughPrivateRuntimeWithoutChangingDefaultRules()
    {
        using var reloadScenario = new MachineGunnerStarterScenario(
            new[] { 3214 },
            initialHandCount: 1,
            initialAmmo: 2);
        reloadScenario.StartBattle();
        BattleCommandExecutionResult tumbleReload = reloadScenario.Play(3214, targetId: null);

        PlayerTurnData reloadTurn = reloadScenario.Queue.Turn.CurrentValue.Players[reloadScenario.Player.Id];
        Assert.That(reloadTurn.Energy, Is.EqualTo(1));
        Assert.That(reloadTurn.Ammo, Is.EqualTo(5));
        Assert.That(reloadScenario.Player.CurrentBlock, Is.EqualTo(10));
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(tumbleReload).Amount, Is.EqualTo(10));

        using var bayonetScenario = new MachineGunnerStarterScenario(
            new[] { 3225 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        bayonetScenario.StartBattle();
        BattleCommandExecutionResult bayonet = bayonetScenario.Play(3225, targetId: null);

        Assert.That(bayonetScenario.FirstEnemy.CurrentHealth, Is.EqualTo(93));
        Assert.That(bayonetScenario.Player.CurrentBlock, Is.EqualTo(7));
        Assert.That(FindSettlement<BattleDamageAppliedSettlement>(bayonet).TargetId, Is.EqualTo(bayonetScenario.FirstEnemy.Id));

        using var heavyScenario = new MachineGunnerStarterScenario(
            new[] { 3230 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        heavyScenario.StartBattle();
        heavyScenario.Play(3230, heavyScenario.FirstEnemy.Id);

        Assert.That(heavyScenario.FirstEnemy.CurrentHealth, Is.EqualTo(67));
        Assert.That(heavyScenario.Queue.Turn.CurrentValue.Players[heavyScenario.Player.Id].Energy, Is.Zero);

        using var quickElbowScenario = new MachineGunnerStarterScenario(
            new[] { 3227 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        quickElbowScenario.StartBattle();
        quickElbowScenario.Play(3227, targetId: null);

        Assert.That(quickElbowScenario.FirstEnemy.CurrentHealth, Is.EqualTo(94));
        Assert.That(quickElbowScenario.Queue.Turn.CurrentValue.Players[quickElbowScenario.Player.Id].Energy, Is.EqualTo(3));

        using var maneuverScenario = new MachineGunnerStarterScenario(
            new[] { 3258, 3201 },
            initialHandCount: 1);
        maneuverScenario.StartBattle();
        BattleCommandExecutionResult maneuver = maneuverScenario.Play(3258, targetId: null);

        Assert.That(maneuverScenario.Player.CurrentBlock, Is.EqualTo(5));
        Assert.That(maneuverScenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(maneuverScenario.Zones.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(maneuver.Settlements.OfType<BattleCardMovedSettlement>().Count(), Is.EqualTo(2));
    }

    /// <summary>验证首批即时状态程序在职业模块内冻结私有状态、通用易伤和狙击倍率，而不借用通用 Effect 执行器。</summary>
    [Test]
    public void MG5ImmediateStatusPrograms_ResolveThroughPrivateRuntimeInDeclaredOrder()
    {
        using var stunScenario = new MachineGunnerStarterScenario(
            new[] { 3215 },
            initialHandCount: 1,
            firstEnemyHealth: 8);
        stunScenario.StartBattle();
        BattleCommandExecutionResult stun = stunScenario.Play(3215, targetId: null);

        Assert.That(stunScenario.FirstEnemy.CurrentHealth, Is.Zero);
        Assert.That(stunScenario.SecondEnemy.CurrentHealth, Is.EqualTo(12));
        Assert.That(
            stunScenario.Session.MachineGunnerRuntime.CombatState.Get(
                stunScenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Weakness),
            Is.Zero);
        Assert.That(
            stunScenario.Session.MachineGunnerRuntime.CombatState.Get(
                stunScenario.SecondEnemy.Id,
                MachineGunnerCombatantStatus.Weakness),
            Is.EqualTo(1));
        MachineGunnerPrivateStatusChangedSettlement stunWeakness =
            FindSettlement<MachineGunnerPrivateStatusChangedSettlement>(stun);
        Assert.That(stunWeakness.TargetId, Is.EqualTo(stunScenario.SecondEnemy.Id));
        Assert.That(stunWeakness.Status, Is.EqualTo(MachineGunnerCombatantStatus.Weakness));

        using var smokeScenario = new MachineGunnerStarterScenario(
            new[] { 3221 },
            initialHandCount: 1);
        smokeScenario.StartBattle();
        BattleCommandExecutionResult smoke = smokeScenario.Play(3221, targetId: null);

        Assert.That(smokeScenario.Player.CurrentBlock, Is.EqualTo(10));
        foreach (CombatantId combatantId in new[]
                 {
                     smokeScenario.Player.Id,
                     smokeScenario.FirstEnemy.Id,
                     smokeScenario.SecondEnemy.Id,
                 })
        {
            Assert.That(
                smokeScenario.Session.MachineGunnerRuntime.CombatState.Get(
                    combatantId,
                    MachineGunnerCombatantStatus.Smoke),
                Is.EqualTo(3));
        }

        Assert.That(
            smoke.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>().Count(),
            Is.EqualTo(3));

        using var kidneyScenario = new MachineGunnerStarterScenario(
            new[] { 3228 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        kidneyScenario.StartBattle();
        kidneyScenario.Play(3228, kidneyScenario.FirstEnemy.Id);

        Assert.That(kidneyScenario.FirstEnemy.CurrentHealth, Is.EqualTo(92));
        Assert.That(
            kidneyScenario.Session.MachineGunnerRuntime.CombatState.Get(
                kidneyScenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Weakness),
            Is.EqualTo(1));

        using var painfulScenario = new MachineGunnerStarterScenario(
            new[] { 3229 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        painfulScenario.StartBattle();
        BattleCommandExecutionResult painful = painfulScenario.Play(3229, painfulScenario.FirstEnemy.Id);

        BattleStatusAppliedSettlement painfulVulnerable =
            FindSettlement<BattleStatusAppliedSettlement>(painful);
        Assert.That(painfulScenario.FirstEnemy.CurrentHealth, Is.EqualTo(90));
        Assert.That(painfulScenario.FirstEnemy.CurrentVulnerable, Is.EqualTo(2));
        Assert.That(painfulVulnerable.EffectId, Is.Null);
        Assert.That(painfulVulnerable.SourceId, Is.EqualTo(painfulScenario.Player.Id));
        Assert.That(painfulVulnerable.TargetId, Is.EqualTo(painfulScenario.FirstEnemy.Id));
        Assert.That(painfulVulnerable.ValueBefore, Is.Zero);
        Assert.That(painfulVulnerable.ValueAfter, Is.EqualTo(2));
        BattleCommandPresentationPlan painfulPlan = BattleCommandPresentationPlan.Create(painful);
        Assert.That(
            painfulPlan.SettlementSteps.Any(step =>
                step.Kind == BattleCommandPresentationStepKind.VulnerableIconPulse &&
                step.Settlement == painfulVulnerable),
            Is.True);

        using var sniperScenario = new MachineGunnerStarterScenario(
            new[] { 3205, 3247 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100);
        sniperScenario.SecondEnemy.ApplyVulnerableGain(1);
        sniperScenario.StartBattle();
        sniperScenario.Play(3205, targetId: null);
        BattleCommandExecutionResult sniper = sniperScenario.Play(3247, targetId: null);

        Assert.That(sniperScenario.SecondEnemy.CurrentHealth, Is.EqualTo(74));
        Assert.That(sniperScenario.SecondEnemy.CurrentVulnerable, Is.EqualTo(2));
        Assert.That(
            FindSettlement<BattleAmmoSpentSettlement>(sniper).Amount,
            Is.EqualTo(2),
            "狙击即使在兴奋剂期间也不应支付额外一发弹药。");
        Assert.That(sniper.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(1));
        Assert.That(MachineGunnerCardProgramRegistry.TryGet(
            cfg.battle.MachineGunnerProgramId.SniperShot,
            out MachineGunnerCardProgram sniperProgram), Is.True);
        Assert.That(sniperProgram.IsSniper, Is.True);
        Assert.That(sniperProgram.ReceivesStimBonus, Is.False);
    }

    /// <summary>验证 X 在队首冻结为当前能量，X 为零仍可出牌且不会虚构格挡或弹药变化。</summary>
    [Test]
    public void HoldLine_XCostFreezesCurrentEnergyAndAllowsZero()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3201, 3220, 3220 },
            initialHandCount: 3,
            firstEnemyHealth: 100);
        scenario.StartBattle();
        scenario.Play(3201, scenario.FirstEnemy.Id);

        BattleCommandExecutionResult firstHoldLine = scenario.Play(3220, targetId: null);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(5));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(15));
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(firstHoldLine).Amount, Is.EqualTo(3));

        BattleCommandExecutionResult zeroHoldLine = scenario.Play(3220, targetId: null);
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(15));
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(zeroHoldLine).Amount, Is.Zero);
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(3));
    }

    /// <summary>验证战术突进重复打出只刷新一份免攻，下一张成功攻击保留名义效果但不实际耗能耗弹，随后攻击恢复正常支付。</summary>
    [Test]
    public void TacticalAdvance_TwoPlaysRefreshOneWaiverAndSuccessfulAttackConsumesIt()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3234, 3234, 3233, 3233 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 4,
            initialAmmo: 3);
        scenario.StartBattle();

        BattleCommandExecutionResult firstAdvance = scenario.Play(3234, targetId: null);
        BattleCommandExecutionResult secondAdvance = scenario.Play(3234, targetId: null);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(firstAdvance).Amount, Is.EqualTo(2));
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(firstAdvance).Amount, Is.EqualTo(10));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(firstAdvance).ToZone,
            Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(secondAdvance).Amount, Is.EqualTo(2));
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(secondAdvance).Amount, Is.EqualTo(10));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(secondAdvance).ToZone,
            Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(20));

        BattleCommandExecutionResult freePrecision = scenario.Play(
            3233,
            scenario.FirstEnemy.Id);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(freePrecision).Amount, Is.Zero);
        Assert.That(freePrecision.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(freePrecision.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
            Is.EqualTo(3));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(3));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(freePrecision).ToZone,
            Is.EqualTo(BattleCardZone.DiscardPile));

        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
            scenario.Submit(3233, scenario.FirstEnemy.Id));

        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(79));
        Assert.That(scenario.Zones.Cards[scenario.Zones.Hand.Single()].TemplateId,
            Is.EqualTo(3233));
    }

    /// <summary>验证战术突进的免攻不会被技能或行动结束消耗，跨到下一玩家回合后由成功攻击消费并恢复正常支付。</summary>
    [Test]
    public void TacticalAdvance_SkillAndRoundTransitionPreserveWaiverUntilSuccessfulAttack()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3234, 3203, 3233, 3233 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 4,
            initialAmmo: 3);
        scenario.StartBattle();

        BattleCommandExecutionResult advance = scenario.Play(3234, targetId: null);
        BattleCommandExecutionResult block = scenario.Play(3203, targetId: null);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(advance).Amount, Is.EqualTo(2));
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(advance).Amount, Is.EqualTo(10));
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(block).Amount, Is.EqualTo(1));
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(block).Amount, Is.EqualTo(5));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(15));

        scenario.EndPlayerAction();

        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(4));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(4));

        BattleCommandExecutionResult freePrecision = scenario.Play(
            3233,
            scenario.FirstEnemy.Id);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(freePrecision).Amount, Is.Zero);
        Assert.That(freePrecision.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(freePrecision.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
            Is.EqualTo(3));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(4));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(4));

        BattleCommandExecutionResult paidPrecision = scenario.Play(
            3233,
            scenario.FirstEnemy.Id);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(paidPrecision).Amount, Is.EqualTo(1));
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(paidPrecision).Amount, Is.EqualTo(3));
        Assert.That(paidPrecision.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
            Is.EqualTo(3));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(3));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(1));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(58));
    }

    /// <summary>验证束缚与缺失目标拒绝攻击时保持资源、卡区和随机流零写入，解除失败条件后同一攻击仍可消费保留的免攻。</summary>
    [Test]
    public void TacticalAdvance_FailedAttacksPreserveWaiverUntilSuccessfulRetry()
    {
        using (var shackleScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3233 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 3))
        {
            shackleScenario.StartBattle();
            shackleScenario.Play(3234, targetId: null);
            shackleScenario.Session.MachineGunnerRuntime.CombatState.Add(
                shackleScenario.Player.Id,
                MachineGunnerCombatantStatus.Shackle,
                1);
            BattleTurnData turnBefore = shackleScenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = shackleScenario.Zones.Layout.CurrentValue;
            uint shuffleBefore = shackleScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore =
                shackleScenario.Session.MachineGunnerRuntime.CardRandomState;
            int resultCountBefore = shackleScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                shackleScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                shackleScenario.Submit(3233, shackleScenario.FirstEnemy.Id));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.AttackBlockedByShackle));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(shackleScenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(shackleScenario.Queue.Turn.CurrentValue.Players[shackleScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(shackleScenario.Queue.Turn.CurrentValue.Players[shackleScenario.Player.Id].Ammo,
                Is.EqualTo(3));
            Assert.That(shackleScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(shackleScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(shackleScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(shackleScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(shackleScenario.FirstEnemy.CurrentHealth, Is.EqualTo(100));

            shackleScenario.Session.MachineGunnerRuntime.CombatState.Set(
                shackleScenario.Player.Id,
                MachineGunnerCombatantStatus.Shackle,
                0);
            BattleCommandExecutionResult retry = shackleScenario.Play(
                3233,
                shackleScenario.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(retry).Amount, Is.Zero);
            Assert.That(retry.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(retry.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
                Is.EqualTo(3));
            Assert.That(shackleScenario.Queue.Turn.CurrentValue.Players[shackleScenario.Player.Id].Ammo,
                Is.EqualTo(3));
            Assert.That(shackleScenario.FirstEnemy.CurrentHealth, Is.EqualTo(79));
        }

        using (var missingTargetScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3233 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 3))
        {
            missingTargetScenario.StartBattle();
            missingTargetScenario.Play(3234, targetId: null);
            BattleTurnData turnBefore = missingTargetScenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = missingTargetScenario.Zones.Layout.CurrentValue;
            uint shuffleBefore = missingTargetScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore =
                missingTargetScenario.Session.MachineGunnerRuntime.CardRandomState;
            int resultCountBefore = missingTargetScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                missingTargetScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                missingTargetScenario.Submit(3233, targetId: null));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRequired));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(missingTargetScenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(missingTargetScenario.Queue.Turn.CurrentValue.Players[missingTargetScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(missingTargetScenario.Queue.Turn.CurrentValue.Players[missingTargetScenario.Player.Id].Ammo,
                Is.EqualTo(3));
            Assert.That(missingTargetScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(missingTargetScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(missingTargetScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(missingTargetScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(missingTargetScenario.FirstEnemy.CurrentHealth, Is.EqualTo(100));

            BattleCommandExecutionResult retry = missingTargetScenario.Play(
                3233,
                missingTargetScenario.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(retry).Amount, Is.Zero);
            Assert.That(retry.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(retry.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
                Is.EqualTo(3));
            Assert.That(missingTargetScenario.Queue.Turn.CurrentValue.Players[missingTargetScenario.Player.Id].Ammo,
                Is.EqualTo(3));
            Assert.That(missingTargetScenario.FirstEnemy.CurrentHealth, Is.EqualTo(79));
        }
    }

    /// <summary>验证免攻把固定、按上限与 X 加全弹药三种攻击的实际支付归零，同时保留各自冻结的伤害段数并正常归宿。</summary>
    [Test]
    public void TacticalAdvance_WaivedCostModesPreserveEffectsWithoutActualResourceSpend()
    {
        using (var fixedScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3277 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 0))
        {
            fixedScenario.StartBattle();
            fixedScenario.Play(3234, targetId: null);

            BattleCommandExecutionResult attack = fixedScenario.Play(
                3277,
                fixedScenario.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(attack).Amount, Is.Zero);
            Assert.That(attack.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(FindSettlement<BattleDamageAppliedSettlement>(attack).AttackValue,
                Is.EqualTo(8));
            Assert.That(fixedScenario.Queue.Turn.CurrentValue.Players[fixedScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(fixedScenario.Queue.Turn.CurrentValue.Players[fixedScenario.Player.Id].Ammo,
                Is.Zero);
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(attack);
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(fixedScenario.FirstEnemy.CurrentHealth, Is.EqualTo(92));
        }

        using (var upToLimitScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3233 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 3))
        {
            upToLimitScenario.StartBattle();
            upToLimitScenario.Play(3234, targetId: null);

            BattleCommandExecutionResult attack = upToLimitScenario.Play(
                3233,
                upToLimitScenario.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(attack).Amount, Is.Zero);
            Assert.That(attack.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(attack.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
                Is.EqualTo(3));
            Assert.That(upToLimitScenario.Queue.Turn.CurrentValue.Players[upToLimitScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(upToLimitScenario.Queue.Turn.CurrentValue.Players[upToLimitScenario.Player.Id].Ammo,
                Is.EqualTo(3));
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(attack);
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(upToLimitScenario.FirstEnemy.CurrentHealth, Is.EqualTo(79));
        }

        using (var xAndAllAvailableScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3226 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 5,
                   initialAmmo: 4))
        {
            xAndAllAvailableScenario.StartBattle();
            xAndAllAvailableScenario.Play(3234, targetId: null);

            BattleCommandExecutionResult attack = xAndAllAvailableScenario.Play(
                3226,
                targetId: null);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(attack).Amount, Is.Zero);
            Assert.That(attack.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(attack.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
                Is.EqualTo(7));
            Assert.That(xAndAllAvailableScenario.Queue.Turn.CurrentValue.Players[xAndAllAvailableScenario.Player.Id].Energy,
                Is.EqualTo(3));
            Assert.That(xAndAllAvailableScenario.Queue.Turn.CurrentValue.Players[xAndAllAvailableScenario.Player.Id].Ammo,
                Is.EqualTo(4));
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(attack);
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(
                xAndAllAvailableScenario.FirstEnemy.CurrentHealth +
                xAndAllAvailableScenario.SecondEnemy.CurrentHealth,
                Is.EqualTo(165));
        }
    }

    /// <summary>验证免攻射击不实际耗弹，但兴奋剂仍追加一段命中，游击战术仍按名义两弹在伤害后、归宿前提供四点格挡。</summary>
    [Test]
    public void TacticalAdvance_WaivedStimShootUsesNominalAmmoForGuerrilla()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3251, 3205, 3234, 3201 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 5,
            initialAmmo: 2);
        scenario.StartBattle();
        scenario.Play(3251, targetId: null);
        scenario.Play(3205, targetId: null);
        scenario.Play(3234, targetId: null);
        Assert.That(scenario.Session.MachineGunnerRuntime.GetPowerStack(
            MachineGunnerPowerKind.GuerrillaTactics), Is.EqualTo(2));
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(10));
        int energyBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy;
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;

        BattleCommandExecutionResult shoot = scenario.Play(
            3201,
            scenario.FirstEnemy.Id);

        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(shoot);
        BattleDamageAppliedSettlement[] damages = shoot.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleBlockGainedSettlement block = FindSettlement<BattleBlockGainedSettlement>(shoot);
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(shoot);
        Assert.That(energy.Amount, Is.Zero);
        Assert.That(shoot.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(damages, Has.Length.EqualTo(2));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 6 }));
        Assert.That(block.Amount, Is.EqualTo(4));
        Assert.That(block.BlockBefore, Is.EqualTo(10));
        Assert.That(block.BlockAfter, Is.EqualTo(14));
        Assert.That(damages.Last().Order, Is.LessThan(block.Order));
        Assert.That(block.Order, Is.LessThan(departure.Order));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(energyBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(88));
        Assert.That(shoot.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, shoot.Settlements.Count)));
    }

    /// <summary>验证免攻生命周期不进入十七种职业状态，先发制人与欺凌都不会把它计为来源或目标状态并额外抽牌。</summary>
    [Test]
    public void TacticalAdvance_WaiverDoesNotCountAsActiveStatusForPreemptiveStrikeOrBully()
    {
        MachineGunnerCombatantStatus[] privateStatuses =
            (MachineGunnerCombatantStatus[])Enum.GetValues(
                typeof(MachineGunnerCombatantStatus));
        Assert.That(privateStatuses, Has.Length.EqualTo(17));

        using (var sourceStatusScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3277, 3230, 3203 },
                   initialHandCount: 4,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 0))
        {
            sourceStatusScenario.StartBattle();
            sourceStatusScenario.Play(3234, targetId: null);
            CardInstanceId drawCandidateId = sourceStatusScenario.Zones.Hand.Single(cardId =>
                sourceStatusScenario.Zones.Cards[cardId].TemplateId == 3203);
            sourceStatusScenario.Zones.DiscardFromHand(drawCandidateId);
            uint shuffleBefore = sourceStatusScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore =
                sourceStatusScenario.Session.MachineGunnerRuntime.CardRandomState;

            BattleCommandExecutionResult attack = sourceStatusScenario.Play(
                3277,
                sourceStatusScenario.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(attack).Amount, Is.Zero);
            Assert.That(attack.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(attack.Settlements.OfType<BattleCardsReshuffledSettlement>(), Is.Empty);
            Assert.That(attack.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
                item.ToZone == BattleCardZone.Hand), Is.False);
            Assert.That(sourceStatusScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(sourceStatusScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(sourceStatusScenario.Zones.DrawPile, Is.Empty);
            Assert.That(sourceStatusScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(sourceStatusScenario.Zones.Cards[sourceStatusScenario.Zones.Hand[0]].TemplateId,
                Is.EqualTo(3230));
            Assert.That(sourceStatusScenario.Queue.Turn.CurrentValue.Players[sourceStatusScenario.Player.Id].Ammo,
                Is.Zero);
            Assert.That(FindSettlement<BattleDamageAppliedSettlement>(attack).AttackValue,
                Is.EqualTo(8));
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(attack);
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));

            using BattleCommandLifecycleExecutionRecorder lifecycle =
                sourceStatusScenario.Queue.RecordExecutionLifecycle();
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                sourceStatusScenario.Submit(3230, sourceStatusScenario.FirstEnemy.Id));
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
        }

        using (var targetStatusScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3278, 3230, 3203 },
                   initialHandCount: 4,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 0))
        {
            targetStatusScenario.StartBattle();
            targetStatusScenario.Play(3234, targetId: null);
            CardInstanceId drawCandidateId = targetStatusScenario.Zones.Hand.Single(cardId =>
                targetStatusScenario.Zones.Cards[cardId].TemplateId == 3203);
            targetStatusScenario.Zones.DiscardFromHand(drawCandidateId);
            uint shuffleBefore = targetStatusScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore =
                targetStatusScenario.Session.MachineGunnerRuntime.CardRandomState;

            BattleCommandExecutionResult attack = targetStatusScenario.Play(
                3278,
                targetStatusScenario.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(attack).Amount, Is.Zero);
            Assert.That(attack.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(attack.Settlements.OfType<BattleCardsReshuffledSettlement>(), Is.Empty);
            Assert.That(attack.Settlements.OfType<BattleCardMovedSettlement>().Any(item =>
                item.ToZone == BattleCardZone.Hand), Is.False);
            Assert.That(targetStatusScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(targetStatusScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(targetStatusScenario.Zones.DrawPile, Is.Empty);
            Assert.That(targetStatusScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(targetStatusScenario.Zones.Cards[targetStatusScenario.Zones.Hand[0]].TemplateId,
                Is.EqualTo(3230));
            Assert.That(FindSettlement<BattleDamageAppliedSettlement>(attack).AttackValue,
                Is.EqualTo(6));
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(attack);
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));

            using BattleCommandLifecycleExecutionRecorder lifecycle =
                targetStatusScenario.Queue.RecordExecutionLifecycle();
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                targetStatusScenario.Submit(3230, targetStatusScenario.FirstEnemy.Id));
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
        }
    }

    /// <summary>验证致死攻击成功归宿后仍消费免攻，而伤害预演溢出保持全量零写入并允许清除条件后免费重试。</summary>
    [Test]
    public void TacticalAdvance_LethalAttackConsumesWaiverButOverflowFailurePreservesIt()
    {
        using (var lethalScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3230, 3230 },
                   initialHandCount: 3,
                   firstEnemyHealth: 33,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 0))
        {
            lethalScenario.StartBattle();
            lethalScenario.Play(3234, targetId: null);

            BattleCommandExecutionResult lethal = lethalScenario.Play(
                3230,
                lethalScenario.FirstEnemy.Id);

            BattleDamageAppliedSettlement damage =
                FindSettlement<BattleDamageAppliedSettlement>(lethal);
            BattleCardMovedSettlement departure =
                FindSettlement<BattleCardMovedSettlement>(lethal);
            Assert.That(FindSettlement<BattleEnergySpentSettlement>(lethal).Amount, Is.Zero);
            Assert.That(lethal.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(damage.WasFatal, Is.True);
            Assert.That(damage.HealthBefore, Is.EqualTo(33));
            Assert.That(damage.HealthAfter, Is.Zero);
            Assert.That(damage.Order, Is.LessThan(departure.Order));
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(lethalScenario.FirstEnemy.IsAlive, Is.False);

            using BattleCommandLifecycleExecutionRecorder lifecycle =
                lethalScenario.Queue.RecordExecutionLifecycle();
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                lethalScenario.Submit(3230, lethalScenario.SecondEnemy.Id));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(lethalScenario.SecondEnemy.CurrentHealth, Is.EqualTo(100));
            Assert.That(lethalScenario.Zones.Cards[lethalScenario.Zones.Hand.Single()].TemplateId,
                Is.EqualTo(3230));
        }

        using (var overflowScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3201 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 1))
        {
            overflowScenario.StartBattle();
            overflowScenario.Play(3234, targetId: null);
            overflowScenario.Session.MachineGunnerRuntime.CombatState.Set(
                overflowScenario.Player.Id,
                MachineGunnerCombatantStatus.FirePower,
                int.MaxValue);
            BattleTurnData turnBefore = overflowScenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = overflowScenario.Zones.Layout.CurrentValue;
            uint shuffleBefore = overflowScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore = overflowScenario.Session.MachineGunnerRuntime.CardRandomState;
            int resultCountBefore = overflowScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                overflowScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                overflowScenario.Submit(3201, overflowScenario.FirstEnemy.Id));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.EffectValueOverflow));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(overflowScenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(overflowScenario.Queue.Turn.CurrentValue.Players[overflowScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(overflowScenario.Queue.Turn.CurrentValue.Players[overflowScenario.Player.Id].Ammo,
                Is.EqualTo(1));
            Assert.That(overflowScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(overflowScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(overflowScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(overflowScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(overflowScenario.FirstEnemy.CurrentHealth, Is.EqualTo(100));
            Assert.That(overflowScenario.Session.MachineGunnerRuntime.CombatState.Get(
                overflowScenario.Player.Id,
                MachineGunnerCombatantStatus.FirePower), Is.EqualTo(int.MaxValue));

            overflowScenario.Session.MachineGunnerRuntime.CombatState.Set(
                overflowScenario.Player.Id,
                MachineGunnerCombatantStatus.FirePower,
                0);
            BattleCommandExecutionResult retry = overflowScenario.Play(
                3201,
                overflowScenario.FirstEnemy.Id);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(retry).Amount, Is.Zero);
            Assert.That(retry.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            Assert.That(FindSettlement<BattleDamageAppliedSettlement>(retry).AttackValue,
                Is.EqualTo(6));
            Assert.That(overflowScenario.Queue.Turn.CurrentValue.Players[overflowScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(overflowScenario.Queue.Turn.CurrentValue.Players[overflowScenario.Player.Id].Ammo,
                Is.EqualTo(1));
            Assert.That(overflowScenario.FirstEnemy.CurrentHealth, Is.EqualTo(94));
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(retry);
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        }
    }

    /// <summary>验证随机 X、多弹射击和全弹药射击在职业私有随机流中逐段冻结并只在成功后提交。</summary>
    [Test]
    public void MG5DynamicShots_UseFrozenXAmmoAndRandomState()
    {
        using var hurricaneScenario = new MachineGunnerStarterScenario(
            new[] { 3232 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        hurricaneScenario.StartBattle();
        uint hurricaneRandomBefore = hurricaneScenario.Session.MachineGunnerRuntime.CardRandomState;
        BattleCommandExecutionResult hurricane = hurricaneScenario.Play(3232, targetId: null);

        Assert.That(hurricane.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(3));
        Assert.That(hurricaneScenario.Queue.Turn.CurrentValue.Players[hurricaneScenario.Player.Id].Energy, Is.Zero);
        Assert.That(hurricaneScenario.Session.MachineGunnerRuntime.CardRandomState, Is.Not.EqualTo(hurricaneRandomBefore));

        using var zeroHurricaneScenario = new MachineGunnerStarterScenario(
            new[] { 3232 },
            initialHandCount: 1,
            initialEnergy: 0);
        zeroHurricaneScenario.StartBattle();
        uint zeroRandomBefore = zeroHurricaneScenario.Session.MachineGunnerRuntime.CardRandomState;
        BattleCommandExecutionResult zeroHurricane = zeroHurricaneScenario.Play(3232, targetId: null);

        Assert.That(zeroHurricane.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(
            zeroHurricaneScenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(zeroRandomBefore),
            "X=0 的疾风肘击不应推进职业随机流。");

        using var wildScenario = new MachineGunnerStarterScenario(
            new[] { 3226 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        wildScenario.StartBattle();
        BattleCommandExecutionResult wild = wildScenario.Play(3226, targetId: null);

        PlayerTurnData wildTurn = wildScenario.Queue.Turn.CurrentValue.Players[wildScenario.Player.Id];
        Assert.That(wildTurn.Energy, Is.Zero);
        Assert.That(wildTurn.Ammo, Is.Zero);
        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(wild).Amount, Is.EqualTo(5));
        Assert.That(wild.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(8));

        using var zeroWildScenario = new MachineGunnerStarterScenario(
            new[] { 3226 },
            initialHandCount: 1,
            initialEnergy: 0,
            initialAmmo: 0);
        zeroWildScenario.StartBattle();
        uint zeroWildRandomBefore = zeroWildScenario.Session.MachineGunnerRuntime.CardRandomState;
        BattleCommandExecutionResult zeroWild = zeroWildScenario.Play(3226, targetId: null);

        Assert.That(zeroWild.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(
            zeroWildScenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(zeroWildRandomBefore),
            "能量和弹药均为零的猛烈发狂不应推进职业随机流。");
    }

    /// <summary>验证射击程序按实际弹药展开命中，并将兴奋剂附加弹药与命中作为同一支付快照。</summary>
    [Test]
    public void MG5AmmoShotPrograms_ResolveSpendLimitsAndStimBonus()
    {
        using var sprayScenario = new MachineGunnerStarterScenario(
            new[] { 3205, 3224 },
            initialHandCount: 2,
            firstEnemyHealth: 100);
        sprayScenario.StartBattle();
        sprayScenario.Play(3205, targetId: null);
        BattleCommandExecutionResult spray = sprayScenario.Play(3224, targetId: null);

        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(spray).Amount, Is.EqualTo(3));
        Assert.That(spray.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(3));

        using var precisionScenario = new MachineGunnerStarterScenario(
            new[] { 3233 },
            initialHandCount: 1,
            firstEnemyHealth: 100);
        precisionScenario.StartBattle();
        BattleCommandExecutionResult precision = precisionScenario.Play(3233, precisionScenario.FirstEnemy.Id);

        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(precision).Amount, Is.EqualTo(3));
        Assert.That(precision.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(3));

        using var sixHitsScenario = new MachineGunnerStarterScenario(
            new[] { 3256 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            initialAmmo: 4);
        sixHitsScenario.StartBattle();
        BattleCommandExecutionResult sixHits = sixHitsScenario.Play(3256, targetId: null);

        Assert.That(FindSettlement<BattleAmmoSpentSettlement>(sixHits).Amount, Is.EqualTo(4));
        Assert.That(sixHits.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(4));
    }

    /// <summary>验证十二连在零弹药时先完成空首波，再于同一 Queue 事务内补满并支付第二波五弹后归入弃牌堆。</summary>
    [Test]
    public void TwelveHits_ZeroAmmoRefillsBetweenWavesAndSpendsSecondWaveThroughQueue()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3257 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 3,
            initialAmmo: 0,
            ammoMaximum: 5);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3257, targetId: null);

        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(result);
        BattleAmmoSpentSettlement ammoSpent = FindSettlement<BattleAmmoSpentSettlement>(result);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);
        Assert.That(energy.Amount, Is.EqualTo(3));
        Assert.That(refill.AmmoBefore, Is.Zero);
        Assert.That(refill.AmmoAfter, Is.EqualTo(5));
        Assert.That(ammoSpent.AmmoBefore, Is.EqualTo(5));
        Assert.That(ammoSpent.AmmoAfter, Is.Zero);
        Assert.That(ammoSpent.Amount, Is.EqualTo(5));
        Assert.That(ammoSpent.Order, Is.GreaterThan(refill.Order),
            "零弹首波不得伪造 AmmoSpent；唯一支付必须发生在换弹之后。");
        Assert.That(damages, Has.Length.EqualTo(5));
        Assert.That(damages.Select(item => item.AttackValue),
            Is.EqualTo(Enumerable.Repeat(5, 5)));
        Assert.That(damages.All(item => item.TargetId == scenario.FirstEnemy.Id), Is.True);
        Assert.That(ammoSpent.Order, Is.LessThan(damages[0].Order));
        Assert.That(damages[^1].Order, Is.LessThan(departure.Order));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(75));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.Cards[scenario.Zones.DiscardPile[0]].TemplateId, Is.EqualTo(3257));
        Assert.That(result.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证十二连的两波各自最多支付六弹，波间补满且全部射击持续命中命令开始时冻结的最近目标。</summary>
    [Test]
    public void TwelveHits_NormalAmmoCapsEachWaveAtSixAndRefillsBetweenWaves()
    {
        using (var partialScenario = new MachineGunnerStarterScenario(
                   new[] { 3257 },
                   initialHandCount: 1,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 3,
                   initialAmmo: 2,
                   ammoMaximum: 5))
        {
            partialScenario.StartBattle();

            BattleCommandExecutionResult result = partialScenario.Play(3257, targetId: null);

            BattleAmmoSpentSettlement[] spends = result.Settlements
                .OfType<BattleAmmoSpentSettlement>()
                .ToArray();
            BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(result);
            BattleDamageAppliedSettlement[] damages = result.Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);
            Assert.That(spends, Has.Length.EqualTo(2));
            Assert.That(spends.Select(item => item.Amount), Is.EqualTo(new[] { 2, 5 }));
            Assert.That(spends.Select(item => item.AmmoBefore), Is.EqualTo(new[] { 2, 5 }));
            Assert.That(spends.Select(item => item.AmmoAfter), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(refill.AmmoBefore, Is.Zero);
            Assert.That(refill.AmmoAfter, Is.EqualTo(5));
            Assert.That(damages, Has.Length.EqualTo(7));
            Assert.That(damages.Select(item => item.AttackValue),
                Is.EqualTo(Enumerable.Repeat(5, 7)));
            Assert.That(damages.All(item => item.TargetId == partialScenario.FirstEnemy.Id), Is.True);
            Assert.That(spends[0].Order, Is.LessThan(damages[0].Order));
            Assert.That(damages[1].Order, Is.LessThan(refill.Order));
            Assert.That(refill.Order, Is.LessThan(spends[1].Order));
            Assert.That(spends[1].Order, Is.LessThan(damages[2].Order));
            Assert.That(damages[^1].Order, Is.LessThan(departure.Order));
            Assert.That(partialScenario.Queue.Turn.CurrentValue.Players[partialScenario.Player.Id].Ammo,
                Is.Zero);
            Assert.That(partialScenario.FirstEnemy.CurrentHealth, Is.EqualTo(65));
            Assert.That(partialScenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(result.Settlements.Select(settlement => settlement.Order),
                Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
        }

        using (var expandedScenario = new MachineGunnerStarterScenario(
                   new[] { 3257 },
                   initialHandCount: 1,
                   firstEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 3,
                   initialAmmo: 8,
                   ammoMaximum: 8))
        {
            expandedScenario.StartBattle();

            BattleCommandExecutionResult result = expandedScenario.Play(3257, targetId: null);

            BattleAmmoSpentSettlement[] spends = result.Settlements
                .OfType<BattleAmmoSpentSettlement>()
                .ToArray();
            BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(result);
            BattleDamageAppliedSettlement[] damages = result.Settlements
                .OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);
            Assert.That(spends, Has.Length.EqualTo(2));
            Assert.That(spends.Select(item => item.Amount), Is.EqualTo(new[] { 6, 6 }));
            Assert.That(spends.Select(item => item.AmmoBefore), Is.EqualTo(new[] { 8, 8 }));
            Assert.That(spends.Select(item => item.AmmoAfter), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(refill.AmmoBefore, Is.EqualTo(2));
            Assert.That(refill.AmmoAfter, Is.EqualTo(8));
            Assert.That(damages, Has.Length.EqualTo(12));
            Assert.That(damages.Select(item => item.AttackValue),
                Is.EqualTo(Enumerable.Repeat(5, 12)));
            Assert.That(damages.All(item => item.TargetId == expandedScenario.FirstEnemy.Id), Is.True);
            Assert.That(spends[0].Order, Is.LessThan(damages[0].Order));
            Assert.That(damages[5].Order, Is.LessThan(refill.Order));
            Assert.That(refill.Order, Is.LessThan(spends[1].Order));
            Assert.That(spends[1].Order, Is.LessThan(damages[6].Order));
            Assert.That(damages[^1].Order, Is.LessThan(departure.Order));
            Assert.That(expandedScenario.Queue.Turn.CurrentValue.Players[expandedScenario.Player.Id].Ammo,
                Is.EqualTo(2));
            Assert.That(expandedScenario.FirstEnemy.CurrentHealth, Is.EqualTo(40));
            Assert.That(expandedScenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(result.Settlements.Select(settlement => settlement.Order),
                Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
        }
    }

    /// <summary>验证十二连冻结最近目标后首段致死会截断全部后续伤害，但仍按既定轨迹完成换弹、第二波支付与弃牌归宿。</summary>
    [Test]
    public void TwelveHits_LethalFirstHitStopsDamageWithoutRetargetingOrCancellingSecondWavePayment()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3257 },
            initialHandCount: 1,
            firstEnemyHealth: 5,
            secondEnemyHealth: 20,
            enemyDamage: 0,
            initialEnergy: 3,
            initialAmmo: 8,
            ammoMaximum: 8);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3257, targetId: null);

        BattleAmmoSpentSettlement[] spends = result.Settlements
            .OfType<BattleAmmoSpentSettlement>()
            .ToArray();
        BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(result);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);
        Assert.That(spends, Has.Length.EqualTo(2));
        Assert.That(spends.Select(item => item.Amount), Is.EqualTo(new[] { 6, 6 }));
        Assert.That(spends.Select(item => item.AmmoBefore), Is.EqualTo(new[] { 8, 8 }));
        Assert.That(spends.Select(item => item.AmmoAfter), Is.EqualTo(new[] { 2, 2 }));
        Assert.That(damages, Has.Length.EqualTo(1));
        Assert.That(damages[0].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damages[0].AttackValue, Is.EqualTo(5));
        Assert.That(damages[0].WasFatal, Is.True);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.Zero);
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20),
            "第二波不得在冻结目标死亡后重选下一名存活敌人。");
        Assert.That(refill.AmmoBefore, Is.EqualTo(2));
        Assert.That(refill.AmmoAfter, Is.EqualTo(8));
        Assert.That(spends[0].Order, Is.LessThan(damages[0].Order));
        Assert.That(damages[0].Order, Is.LessThan(refill.Order));
        Assert.That(refill.Order, Is.LessThan(spends[1].Order));
        Assert.That(spends[1].Order, Is.LessThan(departure.Order));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(2));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.Cards[scenario.Zones.DiscardPile[0]].TemplateId, Is.EqualTo(3257));
        Assert.That(result.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证十二连整张卡只在第二波末追加一段兴奋剂射击，且十三个来源段各自严格触发燃烧后再触发一次非递归帮手伤害。</summary>
    [Test]
    public void TwelveHits_StimAddsOneSecondWaveShotAndEachSourceTriggersIncendiaryThenHelper()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3206, 3213, 3205, 3210, 3267, 3257 },
            initialHandCount: 6,
            firstEnemyHealth: 200,
            enemyDamage: 0,
            initialEnergy: 5,
            initialAmmo: 7,
            ammoMaximum: 7);
        scenario.StartBattle();
        scenario.Play(3206, targetId: null);
        scenario.Play(3213, targetId: null);
        scenario.Play(3205, targetId: null);
        scenario.Play(3210, targetId: null);
        scenario.Play(3267, targetId: null);

        BattleCommandExecutionResult result = scenario.Play(3257, targetId: null);

        BattleAmmoSpentSettlement[] spends = result.Settlements
            .OfType<BattleAmmoSpentSettlement>()
            .ToArray();
        BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(result);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleDamageAppliedSettlement[] sourceDamages = damages
            .Where(item => item.AttackValue == 5)
            .ToArray();
        BattleDamageAppliedSettlement[] helperDamages = damages
            .Where(item => item.AttackValue == 1)
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] burns = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn)
            .ToArray();
        string[] relevantOrder = result.Settlements
            .Where(item => item is BattleDamageAppliedSettlement ||
                item is MachineGunnerPrivateStatusChangedSettlement status &&
                status.Status == MachineGunnerCombatantStatus.Burn)
            .Select(item => item is MachineGunnerPrivateStatusChangedSettlement
                ? "Burn"
                : ((BattleDamageAppliedSettlement)item).AttackValue == 5
                    ? "Source"
                    : "Helper")
            .ToArray();
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);
        Assert.That(spends, Has.Length.EqualTo(2));
        Assert.That(spends.Select(item => item.Amount), Is.EqualTo(new[] { 6, 7 }));
        Assert.That(spends.Select(item => item.AmmoBefore), Is.EqualTo(new[] { 7, 7 }));
        Assert.That(spends.Select(item => item.AmmoAfter), Is.EqualTo(new[] { 1, 0 }));
        Assert.That(refill.AmmoBefore, Is.EqualTo(1));
        Assert.That(refill.AmmoAfter, Is.EqualTo(7));
        Assert.That(sourceDamages, Has.Length.EqualTo(13));
        Assert.That(helperDamages, Has.Length.EqualTo(13),
            "每个来源段只允许一个帮手段，帮手自身不得递归触发帮手。");
        Assert.That(burns, Has.Length.EqualTo(13));
        Assert.That(sourceDamages.All(item => item.TargetId == scenario.FirstEnemy.Id), Is.True);
        Assert.That(helperDamages.All(item => item.TargetId == scenario.FirstEnemy.Id), Is.True);
        Assert.That(burns.Select(item => item.ValueBefore), Is.EqualTo(Enumerable.Range(0, 13)));
        Assert.That(burns.Select(item => item.ValueAfter), Is.EqualTo(Enumerable.Range(1, 13)));
        Assert.That(
            relevantOrder,
            Is.EqualTo(Enumerable.Range(0, 13)
                .SelectMany(_ => new[] { "Source", "Burn", "Helper" })));
        Assert.That(sourceDamages.Take(6).All(item => item.Order < refill.Order), Is.True);
        Assert.That(sourceDamages.Skip(6).All(item => item.Order > spends[1].Order), Is.True);
        Assert.That(sourceDamages[5].Order, Is.LessThan(refill.Order));
        Assert.That(refill.Order, Is.LessThan(spends[1].Order));
        Assert.That(spends[1].Order, Is.LessThan(sourceDamages[6].Order));
        Assert.That(helperDamages[^1].Order, Is.LessThan(departure.Order));
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(122));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(result.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证战术突进免除十二连的实际能量与弹药支付，但仍按含兴奋剂的十三点名义耗弹触发游击格挡，并在成功归宿后消费免攻。</summary>
    [Test]
    public void TwelveHits_WaiverUsesNominalThirteenAmmoForGuerrillaThenIsConsumed()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3251, 3205, 3234, 3257, 3230 },
            initialHandCount: 5,
            firstEnemyHealth: 200,
            enemyDamage: 0,
            initialEnergy: 4,
            initialAmmo: 0,
            ammoMaximum: 5);
        scenario.StartBattle();
        scenario.Play(3251, targetId: null);
        scenario.Play(3205, targetId: null);
        BattleCommandExecutionResult advance = scenario.Play(3234, targetId: null);

        BattleCommandExecutionResult result = scenario.Play(3257, targetId: null);

        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(result);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleBlockGainedSettlement block = FindSettlement<BattleBlockGainedSettlement>(result);
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(advance).Amount, Is.EqualTo(10));
        Assert.That(energy.Amount, Is.Zero);
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(refill.AmmoBefore, Is.Zero);
        Assert.That(refill.AmmoAfter, Is.EqualTo(5));
        Assert.That(damages, Has.Length.EqualTo(13));
        Assert.That(damages.All(item => item.AttackValue == 5), Is.True);
        Assert.That(damages.All(item => item.TargetId == scenario.FirstEnemy.Id), Is.True);
        Assert.That(block.Amount, Is.EqualTo(26));
        Assert.That(block.BlockBefore, Is.EqualTo(10));
        Assert.That(block.BlockAfter, Is.EqualTo(36));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(36));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(5));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(135));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(damages.Last().Order, Is.LessThan(block.Order));
        Assert.That(block.Order, Is.LessThan(departure.Order));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(result.Settlements.Select(settlement => settlement.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));

        int resultCountBeforeFailure = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = scenario.Submit(
            3230,
            scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBeforeFailure));
        Assert.That(scenario.Zones.Hand.Select(cardId => scenario.Zones.Cards[cardId].TemplateId),
            Does.Contain(3230));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(135));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(36));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(5));
    }

    /// <summary>验证十二连因能量、非最近显式目标或束缚失败时保持回合、卡区、随机流与战斗事实零写入，并让免攻保留到解除束缚后的成功重试。</summary>
    [Test]
    public void TwelveHits_FailedCostTargetOrShackleWritesNothingAndPreservesWaiver()
    {
        using (var energyScenario = new MachineGunnerStarterScenario(
                   new[] { 3257 },
                   initialHandCount: 1,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 0,
                   ammoMaximum: 5))
        {
            energyScenario.StartBattle();
            BattleTurnData turnBefore = energyScenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = energyScenario.Zones.Layout.CurrentValue;
            CardInstanceId[] handBefore = energyScenario.Zones.Hand.ToArray();
            uint shuffleBefore = energyScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore = energyScenario.Session.MachineGunnerRuntime.CardRandomState;
            int firstHealthBefore = energyScenario.FirstEnemy.CurrentHealth;
            int secondHealthBefore = energyScenario.SecondEnemy.CurrentHealth;
            int resultCountBefore = energyScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                energyScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = energyScenario.Submit(3257, targetId: null);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(energyScenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(energyScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(energyScenario.Zones.Hand, Is.EqualTo(handBefore));
            Assert.That(energyScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(energyScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(energyScenario.FirstEnemy.CurrentHealth, Is.EqualTo(firstHealthBefore));
            Assert.That(energyScenario.SecondEnemy.CurrentHealth, Is.EqualTo(secondHealthBefore));
            Assert.That(energyScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(energyScenario.Session.MachineGunnerRuntime.IsNextAttackFree, Is.False);
        }

        using (var targetScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3257 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 5,
                   initialAmmo: 4,
                   ammoMaximum: 5))
        {
            targetScenario.StartBattle();
            targetScenario.Play(3234, targetId: null);
            Assert.That(targetScenario.Session.MachineGunnerRuntime.IsNextAttackFree, Is.True);
            BattleTurnData turnBefore = targetScenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = targetScenario.Zones.Layout.CurrentValue;
            CardInstanceId[] handBefore = targetScenario.Zones.Hand.ToArray();
            uint shuffleBefore = targetScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore = targetScenario.Session.MachineGunnerRuntime.CardRandomState;
            int firstHealthBefore = targetScenario.FirstEnemy.CurrentHealth;
            int secondHealthBefore = targetScenario.SecondEnemy.CurrentHealth;
            int resultCountBefore = targetScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                targetScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = targetScenario.Submit(
                3257,
                targetScenario.SecondEnemy.Id);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(targetScenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(targetScenario.Queue.Turn.CurrentValue.Players[targetScenario.Player.Id].Ammo,
                Is.EqualTo(4));
            Assert.That(targetScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(targetScenario.Zones.Hand, Is.EqualTo(handBefore));
            Assert.That(targetScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.IsNextAttackFree, Is.True);
            Assert.That(targetScenario.FirstEnemy.CurrentHealth, Is.EqualTo(firstHealthBefore));
            Assert.That(targetScenario.SecondEnemy.CurrentHealth, Is.EqualTo(secondHealthBefore));
            Assert.That(targetScenario.Results, Has.Count.EqualTo(resultCountBefore));
        }

        using (var shackleScenario = new MachineGunnerStarterScenario(
                   new[] { 3234, 3257 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2,
                   initialAmmo: 0,
                   ammoMaximum: 5))
        {
            shackleScenario.StartBattle();
            shackleScenario.Play(3234, targetId: null);
            shackleScenario.Session.MachineGunnerRuntime.CombatState.Add(
                shackleScenario.Player.Id,
                MachineGunnerCombatantStatus.Shackle,
                1);
            Assert.That(shackleScenario.Session.MachineGunnerRuntime.IsNextAttackFree, Is.True);
            BattleTurnData turnBefore = shackleScenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = shackleScenario.Zones.Layout.CurrentValue;
            CardInstanceId[] handBefore = shackleScenario.Zones.Hand.ToArray();
            uint shuffleBefore = shackleScenario.Zones.ShuffleRandomState;
            uint cardRandomBefore = shackleScenario.Session.MachineGunnerRuntime.CardRandomState;
            int firstHealthBefore = shackleScenario.FirstEnemy.CurrentHealth;
            int secondHealthBefore = shackleScenario.SecondEnemy.CurrentHealth;
            int resultCountBefore = shackleScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                shackleScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = shackleScenario.Submit(3257, targetId: null);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.AttackBlockedByShackle));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(shackleScenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(shackleScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(shackleScenario.Zones.Hand, Is.EqualTo(handBefore));
            Assert.That(shackleScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleBefore));
            Assert.That(shackleScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(shackleScenario.Session.MachineGunnerRuntime.IsNextAttackFree, Is.True);
            Assert.That(shackleScenario.Session.MachineGunnerRuntime.CombatState.Get(
                shackleScenario.Player.Id,
                MachineGunnerCombatantStatus.Shackle), Is.EqualTo(1));
            Assert.That(shackleScenario.FirstEnemy.CurrentHealth, Is.EqualTo(firstHealthBefore));
            Assert.That(shackleScenario.SecondEnemy.CurrentHealth, Is.EqualTo(secondHealthBefore));
            Assert.That(shackleScenario.Results, Has.Count.EqualTo(resultCountBefore));

            shackleScenario.Session.MachineGunnerRuntime.CombatState.Set(
                shackleScenario.Player.Id,
                MachineGunnerCombatantStatus.Shackle,
                0);
            BattleCommandExecutionResult retry = shackleScenario.Play(3257, targetId: null);

            Assert.That(FindSettlement<BattleEnergySpentSettlement>(retry).Amount, Is.Zero);
            Assert.That(retry.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
            BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(retry);
            Assert.That(refill.AmmoBefore, Is.Zero);
            Assert.That(refill.AmmoAfter, Is.EqualTo(5));
            Assert.That(retry.Settlements.OfType<BattleDamageAppliedSettlement>().Count(),
                Is.EqualTo(12));
            Assert.That(shackleScenario.Queue.Turn.CurrentValue.Players[shackleScenario.Player.Id].Ammo,
                Is.EqualTo(5));
            Assert.That(shackleScenario.FirstEnemy.CurrentHealth, Is.EqualTo(40));
            Assert.That(shackleScenario.SecondEnemy.CurrentHealth, Is.EqualTo(100));
            Assert.That(shackleScenario.Session.MachineGunnerRuntime.IsNextAttackFree, Is.False);
            Assert.That(FindSettlement<BattleCardMovedSettlement>(retry).ToZone,
                Is.EqualTo(BattleCardZone.DiscardPile));
        }
    }

    /// <summary>验证六张已实现能力牌进入 PowerPile 后分别改变资源、护甲、烟雾和额外抽牌规则。</summary>
    [Test]
    public void PowerPrograms_ActivatePrivateStateAndMoveCardsToPowerPile()
    {
        using var expansionScenario = new MachineGunnerStarterScenario(
            new[] { 3206 },
            initialHandCount: 1);
        expansionScenario.StartBattle();

        BattleCommandExecutionResult expansion = expansionScenario.Play(3206, targetId: null);

        PlayerTurnData expansionTurn = expansionScenario.Queue.Turn.CurrentValue.Players[expansionScenario.Player.Id];
        Assert.That(expansionTurn.Energy, Is.EqualTo(2));
        Assert.That(expansionTurn.EnergyMaximum, Is.EqualTo(6));
        Assert.That(expansionScenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(expansionScenario.Zones.DiscardPile, Is.Empty);
        var powerMove = FindSettlement<BattleCardMovedSettlement>(expansion);
        Assert.That(powerMove.ToZone, Is.EqualTo(BattleCardZone.PowerPile));

        using var outputScenario = new MachineGunnerStarterScenario(
            new[] { 3207 },
            initialHandCount: 1);
        outputScenario.StartBattle();
        outputScenario.Play(3207, targetId: null);

        PlayerTurnData outputTurn = outputScenario.Queue.Turn.CurrentValue.Players[outputScenario.Player.Id];
        Assert.That(outputTurn.Energy, Is.EqualTo(2));
        Assert.That(outputTurn.EnergyMaximum, Is.EqualTo(4));
        Assert.That(outputTurn.EnergyGainPerRound, Is.EqualTo(4));
        Assert.That(outputScenario.Zones.PowerPile, Has.Count.EqualTo(1));

        using var armorScenario = new MachineGunnerStarterScenario(
            new[] { 3208 },
            initialHandCount: 1);
        armorScenario.StartBattle();
        armorScenario.Play(3208, targetId: null);

        Assert.That(
            armorScenario.Session.MachineGunnerRuntime.CombatState.Get(
                armorScenario.Player.Id,
                MachineGunnerCombatantStatus.Armor),
            Is.EqualTo(6));
        Assert.That(armorScenario.Zones.PowerPile, Has.Count.EqualTo(1));

        using var magazineScenario = new MachineGunnerStarterScenario(
            new[] { 3209 },
            initialHandCount: 1);
        magazineScenario.StartBattle();
        magazineScenario.Play(3209, targetId: null);

        PlayerTurnData magazineTurn = magazineScenario.Queue.Turn.CurrentValue.Players[magazineScenario.Player.Id];
        Assert.That(magazineTurn.Ammo, Is.EqualTo(5));
        Assert.That(magazineTurn.AmmoMaximum, Is.EqualTo(8));
        Assert.That(magazineScenario.Zones.PowerPile, Has.Count.EqualTo(1));

        using var overclockScenario = new MachineGunnerStarterScenario(
            new[] { 3245, 3245, 3245 },
            initialHandCount: 1);
        overclockScenario.StartBattle();
        overclockScenario.Play(3245, targetId: null);

        Assert.That(
            overclockScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.PowerOverclock),
            Is.EqualTo(1));
        overclockScenario.EndPlayerAction();

        Assert.That(overclockScenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(overclockScenario.Zones.Hand, Has.Count.EqualTo(2));

        using var smokeScenario = new MachineGunnerStarterScenario(
            new[] { 3211 },
            initialHandCount: 1);
        smokeScenario.StartBattle();
        smokeScenario.Play(3211, targetId: null);
        smokeScenario.Session.MachineGunnerRuntime.CombatState.Add(
            smokeScenario.Player.Id,
            MachineGunnerCombatantStatus.Smoke,
            amount: 3);
        smokeScenario.EndPlayerAction();

        Assert.That(
            smokeScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.SmokePersist),
            Is.EqualTo(1));
        Assert.That(
            smokeScenario.Session.MachineGunnerRuntime.CombatState.Get(
                smokeScenario.Player.Id,
                MachineGunnerCombatantStatus.Smoke),
            Is.EqualTo(2));
    }

    /// <summary>验证私人改装消耗一点能量进入能力区，只提高弹药上限，并让后续射击的每段伤害获得一层开火加成。</summary>
    [Test]
    public void PrivateMod_RaisesAmmoMaximumWithoutRefillAndBuffsEveryShootHit()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3268, 3205, 3201, 3204 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            initialEnergy: 3,
            initialAmmo: 2,
            ammoMaximum: 5);
        scenario.StartBattle();

        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        BattleCommandExecutionResult privateMod = scenario.Play(3268, targetId: null);
        PlayerTurnData turnAfterPower = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];

        Assert.That(turnAfterPower.Energy, Is.EqualTo(2));
        Assert.That(turnAfterPower.Ammo, Is.EqualTo(2));
        Assert.That(turnAfterPower.AmmoMaximum, Is.EqualTo(6));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.FirePower), Is.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.PrivateMod),
            Is.EqualTo(1));
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(privateMod).ToZone,
            Is.EqualTo(BattleCardZone.PowerPile));
        MachineGunnerPrivateStatusChangedSettlement firePower = privateMod.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.FirePower);
        Assert.That(firePower.ValueBefore, Is.Zero);
        Assert.That(firePower.ValueAfter, Is.EqualTo(1));

        scenario.Play(3205, targetId: null);
        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);
        BattleDamageAppliedSettlement[] shootHits = shoot.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(shootHits, Has.Length.EqualTo(2));
        Assert.That(shootHits.Select(hit => hit.AttackValue), Is.EqualTo(new[] { 7, 7 }));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.Zero);

        BattleCommandExecutionResult reload = scenario.Play(3204, targetId: null);
        PlayerTurnData turnAfterReload = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        BattleAmmoRefilledSettlement refill = FindSettlement<BattleAmmoRefilledSettlement>(reload);

        Assert.That(refill.AmmoBefore, Is.Zero);
        Assert.That(refill.AmmoAfter, Is.EqualTo(6));
        Assert.That(turnAfterReload.Ammo, Is.EqualTo(6));
        Assert.That(turnAfterReload.AmmoMaximum, Is.EqualTo(6));
    }

    /// <summary>验证能量不足时私人改装保持零写入，不改变资源、开火、能力层数、随机流或卡区。</summary>
    [Test]
    public void PrivateMod_InsufficientEnergyLeavesResourcesStatusesAndZonesUnchanged()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3268 },
            initialHandCount: 1,
            initialEnergy: 0,
            initialAmmo: 2,
            ammoMaximum: 5);
        scenario.StartBattle();
        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3268, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);
        PlayerTurnData turnAfterFailure = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(turnAfterFailure.Energy, Is.Zero);
        Assert.That(turnAfterFailure.Ammo, Is.EqualTo(2));
        Assert.That(turnAfterFailure.AmmoMaximum, Is.EqualTo(5));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.Player.Id,
                MachineGunnerCombatantStatus.FirePower),
            Is.Zero);
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.PrivateMod),
            Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.PowerPile, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
    }

    /// <summary>验证充能爆射按 Encounter 顺序以基础伤害线性增加百分之五十，并在同一事务支付能量、弃牌和写出连续记录。</summary>
    [Test]
    public void ChargedBurst_ThreeEnemiesDealTwelveEighteenTwentyFourAndDiscard()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3282 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            thirdEnemyHealth: 100,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3282, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleCardMovedSettlement destination = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(damages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.ThirdEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 12, 18, 24 }));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(88));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(82));
        Assert.That(scenario.ThirdEnemy.CurrentHealth, Is.EqualTo(76));
        Assert.That(energy.Amount, Is.EqualTo(2));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(destination.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(destination.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(
            result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证首名和中间敌人被各自穿透段击杀后，后续敌人仍保留施放时快照中的第三段伤害序号。</summary>
    [Test]
    public void ChargedBurst_EarlierFatalHitsDoNotRenumberLaterEnemyDamage()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3282 },
            initialHandCount: 1,
            firstEnemyHealth: 12,
            secondEnemyHealth: 18,
            thirdEnemyHealth: 100,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3282, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(damages.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.ThirdEnemy.Id,
        }));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 12, 18, 24 }));
        Assert.That(damages.Select(item => item.WasFatal), Is.EqualTo(new[] { true, true, false }));
        Assert.That(scenario.FirstEnemy.IsAlive, Is.False);
        Assert.That(scenario.SecondEnemy.IsAlive, Is.False);
        Assert.That(scenario.ThirdEnemy.CurrentHealth, Is.EqualTo(76));
    }

    /// <summary>验证充能爆射是纯狙击：不吃兴奋剂额外段或开火加值，保留隐身，并让燃烧弹药逐目标命中。</summary>
    [Test]
    public void ChargedBurst_PureSniperIgnoresStimAndFirePowerPreservesInvisibleAndAppliesIncendiaryPerTarget()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3210, 3236, 3205, 3282 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            thirdEnemyHealth: 100,
            initialEnergy: 5,
            initialAmmo: 5,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3210, targetId: null);
        scenario.Play(3236, targetId: null);
        scenario.Play(3205, targetId: null);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Invisible, 2);

        BattleCommandExecutionResult result = scenario.Play(3282, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] burns = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn)
            .ToArray();

        Assert.That(MachineGunnerCardProgramRegistry.TryGet(
            cfg.battle.MachineGunnerProgramId.ChargedBurst,
            out MachineGunnerCardProgram program), Is.True);
        Assert.That(program.Tags, Is.EqualTo(MachineGunnerCardTag.Sniper));
        Assert.That(program.ReceivesStimBonus, Is.False);
        Assert.That(program.ReceivesIncendiaryAmmo, Is.True);
        Assert.That(program.PreservesInvisibleAfterSuccessfulAttack, Is.True);
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 24, 36, 48 }));
        Assert.That(burns.Select(item => item.TargetId), Is.EqualTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.ThirdEnemy.Id,
        }));
        Assert.That(burns.Select(item => item.ValueAfter), Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(damages[0].Order, Is.LessThan(burns[0].Order));
        Assert.That(burns[0].Order, Is.LessThan(damages[1].Order));
        Assert.That(damages[1].Order, Is.LessThan(burns[1].Order));
        Assert.That(burns[1].Order, Is.LessThan(damages[2].Order));
        Assert.That(damages[2].Order, Is.LessThan(burns[2].Order));
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(5));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.FirePower), Is.EqualTo(3));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Invisible), Is.EqualTo(2));
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
    }

    /// <summary>验证全体自动目标卡拒绝显式目标时保持参与者、资源、卡区、随机流与表现结果零写入。</summary>
    [Test]
    public void ChargedBurst_ExplicitTargetFailsWithoutWritingCombatResourcesOrZones()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3282 },
            initialHandCount: 1,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();
        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3282, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(2));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(5));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证能量不足时充能爆射不写入伤害、隐身、资源、卡区、随机流或表现结果。</summary>
    [Test]
    public void ChargedBurst_InsufficientEnergyFailsWithoutWritingCombatStatusesOrZones()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3282 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Invisible, 2);
        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3282, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(1));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(5));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.Invisible), Is.EqualTo(2));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证过载供能即时获得能量受最大值约束，并累计一次性下回合能量惩罚。</summary>
    [Test]
    public void Overload_GainsTwoEnergyUpToMaximumAndSchedulesPenalty()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3213 },
            initialHandCount: 1,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3213, targetId: null);
        BattleEnergyGainedSettlement gained = FindSettlement<BattleEnergyGainedSettlement>(result);
        MachineGunnerPrivateStatusChangedSettlement penalty = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty);

        Assert.That(gained.EnergyBefore, Is.EqualTo(2));
        Assert.That(gained.EnergyAfter, Is.EqualTo(4));
        Assert.That(gained.Amount, Is.EqualTo(2));
        Assert.That(penalty.ValueBefore, Is.Zero);
        Assert.That(penalty.ValueAfter, Is.EqualTo(1));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(4));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone,
            Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证能量已满时过载供能仍会成功弃牌并累计惩罚，但不会伪造零增量即时获能记录。</summary>
    [Test]
    public void Overload_AtMaximumSchedulesPenaltyWithoutFakeEnergyGain()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3213 },
            initialHandCount: 1,
            initialEnergy: 5,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3213, targetId: null);
        MachineGunnerPrivateStatusChangedSettlement penalty = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty);

        Assert.That(result.Settlements.OfType<BattleEnergyGainedSettlement>(), Is.Empty);
        Assert.That(penalty.ValueBefore, Is.Zero);
        Assert.That(penalty.ValueAfter, Is.EqualTo(1));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(5));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(result).ToZone,
            Is.EqualTo(BattleCardZone.DiscardPile));
    }

    /// <summary>验证防御姿态支付一费后先获得八点格挡，再累计一次性下回合能量加成。</summary>
    [Test]
    public void DefensiveStance_SpendsOneEnergyGainsBlockAndSchedulesBonus()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3271 },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult result = scenario.Play(3271, targetId: null);
        BattleEnergySpentSettlement spent = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleBlockGainedSettlement block = FindSettlement<BattleBlockGainedSettlement>(result);
        MachineGunnerPrivateStatusChangedSettlement bonus = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainBonus);

        Assert.That(spent.Amount, Is.EqualTo(1));
        Assert.That(block.Amount, Is.EqualTo(8));
        Assert.That(bonus.ValueBefore, Is.Zero);
        Assert.That(bonus.ValueAfter, Is.EqualTo(1));
        Assert.That(spent.Order, Is.LessThan(block.Order));
        Assert.That(block.Order, Is.LessThan(bonus.Order));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(8));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
    }

    /// <summary>验证能量加成与惩罚分别叠加后只修正下一回合基础补给一次，并在同次回合开始清零。</summary>
    [Test]
    public void RoundStart_StacksBonusAndPenaltyAppliesNetGainOnceAndClearsBoth()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3213, 3213, 3271, 3230 },
            initialHandCount: 4,
            initialEnergy: 0,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3213, targetId: null);
        scenario.Play(3213, targetId: null);
        scenario.Play(3271, targetId: null);
        scenario.Play(3230, scenario.FirstEnemy.Id);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainBonus), Is.EqualTo(1));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.EqualTo(2));
        int resultCountBeforeEnd = scenario.Results.Count;

        scenario.EndPlayerAction();
        BattleCommandExecutionResult[] nextRoundResults = scenario.Results.Skip(resultCountBeforeEnd).ToArray();
        BattleSettlementRecord[] nextRoundSettlements = nextRoundResults
            .SelectMany(item => item.Settlements)
            .ToArray();
        BattleEnergyRefilledSettlement refill = nextRoundSettlements
            .OfType<BattleEnergyRefilledSettlement>()
            .Single();
        MachineGunnerPrivateStatusChangedSettlement bonusClear = nextRoundSettlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainBonus);
        MachineGunnerPrivateStatusChangedSettlement penaltyClear = nextRoundSettlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty);

        Assert.That(refill.EnergyBefore, Is.Zero);
        Assert.That(refill.EnergyAfter, Is.EqualTo(2));
        Assert.That(refill.Amount, Is.EqualTo(2));
        Assert.That(nextRoundSettlements.OfType<BattleEnergyGainedSettlement>(), Is.Empty);
        Assert.That(bonusClear.ValueBefore, Is.EqualTo(1));
        Assert.That(bonusClear.ValueAfter, Is.Zero);
        Assert.That(penaltyClear.ValueBefore, Is.EqualTo(2));
        Assert.That(penaltyClear.ValueAfter, Is.Zero);
        Assert.That(refill.Order, Is.LessThan(bonusClear.Order));
        Assert.That(bonusClear.Order, Is.LessThan(penaltyClear.Order));
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainBonus), Is.Zero);
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Zero);

        int resultCountBeforeSecondEnd = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleSettlementRecord[] secondRoundSettlements = scenario.Results
            .Skip(resultCountBeforeSecondEnd)
            .SelectMany(item => item.Settlements)
            .ToArray();
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(5));
        Assert.That(secondRoundSettlements.OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainBonus ||
                           item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Empty);
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainBonus), Is.Zero);
        Assert.That(state.Get(scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Zero);
    }

    /// <summary>验证累计惩罚等于基础补给时有效能量补给下限为零，并仍只清除一次惩罚状态。</summary>
    [Test]
    public void RoundStart_PenaltyAtBaseGainFloorsRefillAtZeroAndClearsOnce()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3213, 3213, 3213, 3230, 3203, 3203 },
            initialHandCount: 6,
            initialEnergy: 0,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3213, targetId: null);
        scenario.Play(3213, targetId: null);
        scenario.Play(3213, targetId: null);
        scenario.Play(3230, scenario.FirstEnemy.Id);
        scenario.Play(3203, targetId: null);
        scenario.Play(3203, targetId: null);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        int resultCountBeforeEnd = scenario.Results.Count;

        scenario.EndPlayerAction();
        BattleSettlementRecord[] nextRoundSettlements = scenario.Results
            .Skip(resultCountBeforeEnd)
            .SelectMany(item => item.Settlements)
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement penaltyClear = nextRoundSettlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty);

        Assert.That(nextRoundSettlements.OfType<BattleEnergyRefilledSettlement>(), Is.Empty);
        Assert.That(penaltyClear.ValueBefore, Is.EqualTo(3));
        Assert.That(penaltyClear.ValueAfter, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Zero);
    }

    /// <summary>验证防御姿态能量不足时保持格挡、延迟状态、资源、卡区、随机流和表现结果零写入。</summary>
    [Test]
    public void DefensiveStance_InsufficientEnergyFailsWithoutWritingBlockBonusOrZones()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3271 },
            initialHandCount: 1,
            initialEnergy: 0,
            enemyDamage: 0);
        scenario.StartBattle();
        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(3271, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.Player.CurrentBlock, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainBonus), Is.Zero);
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证两张自目标能量卡都拒绝显式敌人目标，并在目标门禁失败时保持全域零写入。</summary>
    [TestCase(3213)]
    [TestCase(3271)]
    public void RoundEnergyCards_ExplicitTargetFailsWithoutWritingResourcesStatusesOrZones(int templateId)
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { templateId },
            initialHandCount: 1,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        int resultCountBefore = scenario.Results.Count;
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        using BattleCommandLifecycleExecutionRecorder lifecycle = scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(templateId, scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.Player.CurrentBlock, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(1));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainBonus), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id, MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Zero);
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证便携帮手进入能力区并叠层，随后让每个帮手按来源射击之后的顺序各攻击同一目标一次。</summary>
    [Test]
    public void PortableHelper_StacksInPowerPileAndEachStackFollowsShootDamage()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3267, 3267, 3201 },
            initialHandCount: 3,
            firstEnemyHealth: 100,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult firstHelper = scenario.Play(3267, targetId: null);
        Assert.That(firstHelper.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.PortableHelper),
            Is.EqualTo(1));

        BattleCommandExecutionResult secondHelper = scenario.Play(3267, targetId: null);
        Assert.That(secondHelper.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(2));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.PortableHelper),
            Is.EqualTo(2));

        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);
        BattleDamageAppliedSettlement[] damages = shoot.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 1, 1 }));
        Assert.That(damages.Select(item => item.TargetId), Is.All.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(damages.Select(item => item.SourceId), Is.All.EqualTo(scenario.Player.Id));
        Assert.That(damages.Select(item => item.HealthBefore), Is.EqualTo(new[] { 100, 94, 93 }));
        Assert.That(damages.Select(item => item.HealthAfter), Is.EqualTo(new[] { 94, 93, 92 }));
        Assert.That(damages.Select(item => item.Order), Is.Ordered.Ascending);
    }

    /// <summary>验证便携帮手跨过完整玩家与敌人轮次后仍保留层数和能力区归属，并在下一轮射击时继续触发。</summary>
    [Test]
    public void PortableHelper_PersistsAcrossFullRoundAndTriggersOnNextRoundShoot()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3267, 3201 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3267, targetId: null);

        scenario.EndPlayerAction();

        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.PortableHelper),
            Is.EqualTo(1));

        BattleDamageAppliedSettlement[] damages = scenario.Play(3201, scenario.FirstEnemy.Id)
            .Settlements.OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 1 }));
        Assert.That(damages.Select(item => item.TargetId), Is.All.EqualTo(scenario.FirstEnemy.Id));
    }

    /// <summary>验证兴奋剂的两段来源射击各自触发全部帮手，并保持来源段后紧跟两个帮手且帮手伤害不递归。</summary>
    [Test]
    public void PortableHelper_TwoStacksFollowEachStimShootSegmentWithoutRecursion()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3267, 3267, 3205, 3201 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            initialEnergy: 3,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3267, targetId: null);
        scenario.Play(3267, targetId: null);
        scenario.Play(3205, targetId: null);

        BattleCommandExecutionResult shoot = scenario.Play(3201, scenario.FirstEnemy.Id);
        BattleDamageAppliedSettlement[] damages = shoot.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(damages, Has.Length.EqualTo(6));
        Assert.That(
            damages.Select(item => item.AttackValue),
            Is.EqualTo(new[] { 6, 1, 1, 6, 1, 1 }));
        Assert.That(
            damages.Select(item => item.HealthBefore),
            Is.EqualTo(new[] { 100, 94, 93, 92, 86, 85 }));
        Assert.That(
            damages.Select(item => item.HealthAfter),
            Is.EqualTo(new[] { 94, 93, 92, 86, 85, 84 }));
    }

    /// <summary>验证纯狙击和射击加狙击都触发帮手，而无射击分类的标记与肘击不会触发。</summary>
    [Test]
    public void PortableHelper_UsesShootCategoryAndExcludesMarkAndElbow()
    {
        using (var sniperScenario = new MachineGunnerStarterScenario(
                   new[] { 3267, 3247 },
                   initialHandCount: 2,
                   secondEnemyHealth: 100,
                   initialEnergy: 2,
                   enemyDamage: 0))
        {
            sniperScenario.StartBattle();
            sniperScenario.Play(3267, targetId: null);
            BattleDamageAppliedSettlement[] damages = sniperScenario.Play(3247, targetId: null)
                .Settlements.OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 13, 1 }));
            Assert.That(damages.Select(item => item.TargetId),
                Is.All.EqualTo(sniperScenario.SecondEnemy.Id));
        }

        using (var dualTagScenario = new MachineGunnerStarterScenario(
                   new[] { 3267, 3248 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            dualTagScenario.StartBattle();
            dualTagScenario.Play(3267, targetId: null);
            BattleDamageAppliedSettlement[] damages = dualTagScenario
                .Play(3248, dualTagScenario.FirstEnemy.Id)
                .Settlements.OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 1, 1 }));
        }

        using (var markScenario = new MachineGunnerStarterScenario(
                   new[] { 3267, 3280 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            markScenario.StartBattle();
            markScenario.Play(3267, targetId: null);
            BattleDamageAppliedSettlement[] damages = markScenario
                .Play(3280, markScenario.FirstEnemy.Id)
                .Settlements.OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 5 }));
        }

        using (var elbowScenario = new MachineGunnerStarterScenario(
                   new[] { 3267, 3202 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   initialEnergy: 2,
                   enemyDamage: 0))
        {
            elbowScenario.StartBattle();
            elbowScenario.Play(3267, targetId: null);
            BattleDamageAppliedSettlement[] damages = elbowScenario.Play(3202, targetId: null)
                .Settlements.OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6 }));
        }
    }

    /// <summary>验证帮手只读取开火、目标易伤和破甲，忽略力量、虚弱、双方烟雾与目标隐身，同时仍经目标格挡结算。</summary>
    [Test]
    public void PortableHelper_DamageReadsFirePowerVulnerableArmorBreakButIgnoresAttackModifiers()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3267, 3201 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3267, targetId: null);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        scenario.Player.ApplyStrengthChange(10);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Weakness, 1);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 9);
        state.Add(scenario.Player.Id, MachineGunnerCombatantStatus.FirePower, 3);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Smoke, 9);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.ArmorBreak, 2);
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Invisible, 1);
        scenario.FirstEnemy.ApplyVulnerableGain(1);
        scenario.FirstEnemy.ApplyBlockGain(5);

        BattleDamageAppliedSettlement[] damages = scenario.Play(3201, scenario.FirstEnemy.Id)
            .Settlements.OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 1, 9 }));
        Assert.That(damages.Select(item => item.BlockBefore), Is.EqualTo(new[] { 5, 4 }));
        Assert.That(damages.Select(item => item.BlockAfter), Is.EqualTo(new[] { 4, 0 }));
        Assert.That(damages.Select(item => item.HealthAfter), Is.EqualTo(new[] { 100, 95 }));
        Assert.That(damages[1].HealthBefore, Is.EqualTo(damages[0].HealthAfter));
    }

    /// <summary>验证来源射击致死时不再触发帮手，且首个帮手致死后立即停止剩余帮手。</summary>
    [Test]
    public void PortableHelper_StopsWhenSourceOrEarlierHelperKillsTarget()
    {
        using (var sourceFatalScenario = new MachineGunnerStarterScenario(
                   new[] { 3267, 3201 },
                   initialHandCount: 2,
                   firstEnemyHealth: 6,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            sourceFatalScenario.StartBattle();
            sourceFatalScenario.Play(3267, targetId: null);
            BattleDamageAppliedSettlement[] damages = sourceFatalScenario
                .Play(3201, sourceFatalScenario.FirstEnemy.Id)
                .Settlements.OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6 }));
            Assert.That(damages[0].WasFatal, Is.True);
        }

        using (var helperFatalScenario = new MachineGunnerStarterScenario(
                   new[] { 3267, 3267, 3201 },
                   initialHandCount: 3,
                   firstEnemyHealth: 7,
                   initialEnergy: 2,
                   enemyDamage: 0))
        {
            helperFatalScenario.StartBattle();
            helperFatalScenario.Play(3267, targetId: null);
            helperFatalScenario.Play(3267, targetId: null);
            BattleDamageAppliedSettlement[] damages = helperFatalScenario
                .Play(3201, helperFatalScenario.FirstEnemy.Id)
                .Settlements.OfType<BattleDamageAppliedSettlement>()
                .ToArray();
            Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 1 }));
            Assert.That(damages.Select(item => item.WasFatal), Is.EqualTo(new[] { false, true }));
        }
    }

    /// <summary>验证燃烧弹药只由来源射击施加一次燃烧，帮手段不会再次触发燃烧弹药。</summary>
    [Test]
    public void PortableHelper_IncendiaryAppliesOnceFromSourceBeforeHelperDamage()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3267, 3210, 3201 },
            initialHandCount: 3,
            firstEnemyHealth: 100,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3267, targetId: null);
        scenario.Play(3210, targetId: null);

        BattleCommandExecutionResult result = scenario.Play(3201, scenario.FirstEnemy.Id);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] burns = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn)
            .ToArray();
        string[] relevantOrder = result.Settlements
            .Where(item => item is BattleDamageAppliedSettlement ||
                item is MachineGunnerPrivateStatusChangedSettlement status &&
                status.Status == MachineGunnerCombatantStatus.Burn)
            .Select(item => item is BattleDamageAppliedSettlement ? "Damage" : "Burn")
            .ToArray();

        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 1 }));
        Assert.That(burns, Has.Length.EqualTo(1));
        Assert.That(burns[0].ValueBefore, Is.Zero);
        Assert.That(burns[0].ValueAfter, Is.EqualTo(1));
        Assert.That(relevantOrder, Is.EqualTo(new[] { "Damage", "Burn", "Damage" }));
    }

    /// <summary>验证便携帮手在能量不足或错误携带显式目标时保持资源、能力层数、卡区与随机流零写入。</summary>
    [Test]
    public void PortableHelper_FailedCostOrExplicitTargetWritesNothing()
    {
        using (var energyScenario = new MachineGunnerStarterScenario(
                   new[] { 3267 },
                   initialHandCount: 1,
                   initialEnergy: 0,
                   enemyDamage: 0))
        {
            energyScenario.StartBattle();
            int resultCountBefore = energyScenario.Results.Count;
            uint randomBefore = energyScenario.Session.MachineGunnerRuntime.CardRandomState;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                energyScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = energyScenario.Submit(3267, targetId: null);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(energyScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(energyScenario.Queue.Turn.CurrentValue.Players[energyScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(energyScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.PortableHelper), Is.Zero);
            Assert.That(energyScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(energyScenario.Zones.PowerPile, Is.Empty);
            Assert.That(energyScenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
        }

        using (var targetScenario = new MachineGunnerStarterScenario(
                   new[] { 3267 },
                   initialHandCount: 1,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            targetScenario.StartBattle();
            int resultCountBefore = targetScenario.Results.Count;
            uint randomBefore = targetScenario.Session.MachineGunnerRuntime.CardRandomState;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                targetScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = targetScenario.Submit(
                3267,
                targetScenario.FirstEnemy.Id);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(submission.Accepted, Is.True);
            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(targetScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(targetScenario.Queue.Turn.CurrentValue.Players[targetScenario.Player.Id].Energy,
                Is.EqualTo(1));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.PortableHelper), Is.Zero);
            Assert.That(targetScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(targetScenario.Zones.PowerPile, Is.Empty);
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证极限过载零费获得一点能量，在自身离手后抽至十张并累计三层下回合能量惩罚。</summary>
    [Test]
    public void LimitOverload_GainsEnergyDrawsToTenAfterDepartureAndSchedulesPenalty()
    {
        using var scenario = new MachineGunnerStarterScenario(
            Enumerable.Repeat(3260, 11).ToArray(),
            initialHandCount: 1,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();
        CardInstanceId playedCardId = scenario.Zones.Hand.Single();
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;

        BattleCommandExecutionResult result = scenario.Play(3260, targetId: null);
        BattleEnergyGainedSettlement gained = FindSettlement<BattleEnergyGainedSettlement>(result);
        MachineGunnerPrivateStatusChangedSettlement penalty = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status ==
                MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty);
        BattleCardMovedSettlement departure = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == playedCardId);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.Zero);
        Assert.That(gained.EnergyBefore, Is.EqualTo(2));
        Assert.That(gained.EnergyAfter, Is.EqualTo(3));
        Assert.That(gained.Amount, Is.EqualTo(1));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(penalty.ValueBefore, Is.Zero);
        Assert.That(penalty.ValueAfter, Is.EqualTo(3));
        Assert.That(gained.Order, Is.LessThan(departure.Order));
        Assert.That(departure.Order, Is.LessThan(penalty.Order));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(10));
        Assert.That(scenario.Zones.Hand.Contains(playedCardId), Is.False);
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { playedCardId }));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(3));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证满能量和十张起手时不伪造获能记录，仍按离手后的一个空位补回十张。</summary>
    [Test]
    public void LimitOverload_AtEnergyAndHandMaximumAvoidsFakeGainAndRefillsDepartureSlot()
    {
        using var scenario = new MachineGunnerStarterScenario(
            Enumerable.Repeat(3260, 11).ToArray(),
            initialHandCount: 10,
            initialEnergy: 5,
            enemyDamage: 0);
        scenario.StartBattle();
        CardInstanceId playedCardId = scenario.Zones.Hand[0];

        BattleCommandExecutionResult result = scenario.Play(3260, targetId: null);

        Assert.That(result.Settlements.OfType<BattleEnergyGainedSettlement>(), Is.Empty);
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(5));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(10));
        Assert.That(scenario.Zones.Hand.Contains(playedCardId), Is.False);
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { playedCardId }));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.EqualTo(3));
    }

    /// <summary>验证多张极限过载累加惩罚，下一回合补能下限为零并在同次回合开始清除状态。</summary>
    [Test]
    public void LimitOverload_StacksPenaltyThenNextRoundFloorsEnergyGainAtZeroAndClears()
    {
        int[] deck = Enumerable.Repeat(3260, 9)
            .Concat(Enumerable.Repeat(3203, 2))
            .ToArray();
        using var scenario = new MachineGunnerStarterScenario(
            deck,
            initialHandCount: 10,
            initialEnergy: 0,
            enemyDamage: 0);
        scenario.StartBattle();

        scenario.Play(3260, targetId: null);
        scenario.Play(3260, targetId: null);
        scenario.Play(3203, targetId: null);
        scenario.Play(3203, targetId: null);
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        Assert.That(state.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.EqualTo(6));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.Zero);
        int resultIndex = scenario.Results.Count;

        scenario.EndPlayerAction();
        BattleSettlementRecord[] nextRoundSettlements = scenario.Results
            .Skip(resultIndex)
            .SelectMany(item => item.Settlements)
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement penaltyClear = nextRoundSettlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status ==
                MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty);

        Assert.That(nextRoundSettlements.OfType<BattleEnergyRefilledSettlement>(), Is.Empty);
        Assert.That(penaltyClear.ValueBefore, Is.EqualTo(6));
        Assert.That(penaltyClear.ValueAfter, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.Zero);
        Assert.That(state.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Zero);
    }

    /// <summary>验证极限过载的显式敌方目标和不在手牌两种非法输入均保持资源、状态、卡区与随机流零写入。</summary>
    [Test]
    public void LimitOverload_InvalidTargetOrMissingHandCardWritesNothing()
    {
        using (var targetScenario = new MachineGunnerStarterScenario(
                   new[] { 3260 },
                   initialHandCount: 1,
                   initialEnergy: 2,
                   enemyDamage: 0))
        {
            targetScenario.StartBattle();
            CardZoneLayoutData layoutBefore = targetScenario.Zones.Layout.CurrentValue;
            uint cardRandomBefore = targetScenario.Session.MachineGunnerRuntime.CardRandomState;
            uint shuffleRandomBefore = targetScenario.Zones.ShuffleRandomState;
            int resultCountBefore = targetScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                targetScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                targetScenario.Submit(3260, targetScenario.FirstEnemy.Id));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(targetScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(targetScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(targetScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(targetScenario.Queue.Turn.CurrentValue.Players[targetScenario.Player.Id].Energy,
                Is.EqualTo(2));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CombatState.Get(
                targetScenario.Player.Id,
                MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Zero);
        }

        using (var missingScenario = new MachineGunnerStarterScenario(
                   new[] { 3260 },
                   initialHandCount: 1,
                   initialEnergy: 2,
                   enemyDamage: 0))
        {
            missingScenario.StartBattle();
            CardInstanceId playedCardId = missingScenario.Zones.Hand.Single();
            Assert.That(missingScenario.Zones.DiscardFromHand(playedCardId).Succeeded, Is.True);
            CardZoneLayoutData layoutBefore = missingScenario.Zones.Layout.CurrentValue;
            uint cardRandomBefore = missingScenario.Session.MachineGunnerRuntime.CardRandomState;
            uint shuffleRandomBefore = missingScenario.Zones.ShuffleRandomState;
            int resultCountBefore = missingScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                missingScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = missingScenario.Queue.Submit(
                new PlayCardCommand(missingScenario.Player.Id, playedCardId, targetId: null));
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.CardNotInHand));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(missingScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(missingScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(missingScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
            Assert.That(missingScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(missingScenario.Queue.Turn.CurrentValue.Players[missingScenario.Player.Id].Energy,
                Is.EqualTo(2));
            Assert.That(missingScenario.Session.MachineGunnerRuntime.CombatState.Get(
                missingScenario.Player.Id,
                MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty), Is.Zero);
        }
    }

    /// <summary>验证极限过载不是射击或攻击，不消耗弹药、刺激回合，也不触发燃烧弹药和便携帮手。</summary>
    [Test]
    public void LimitOverload_DoesNotTriggerShootStimIncendiaryOrPortableHelper()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3267, 3210, 3205, 3260 },
            initialHandCount: 4,
            initialEnergy: 3,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3267, targetId: null);
        scenario.Play(3210, targetId: null);
        scenario.Play(3205, targetId: null);
        CardInstanceId limitOverloadId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3260);
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;

        BattleCommandExecutionResult result = scenario.Play(3260, targetId: null);

        Assert.That(result.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(result.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn), Is.Empty);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
        Assert.That(scenario.Session.MachineGunnerRuntime.GetPowerStack(
            MachineGunnerPowerKind.PortableHelper), Is.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Does.Contain(limitOverloadId));
        Assert.That(scenario.Zones.Hand.Contains(limitOverloadId), Is.False);
        Assert.That(scenario.Zones.Hand.Select(cardId => scenario.Zones.Cards[cardId].TemplateId),
            Does.Contain(3205));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
    }

    /// <summary>验证固定机枪经唯一队列支付二费并获得十格挡，再先消耗自身、按原手牌顺序弃三张牌、创建三张基础机枪爆射，且卡区只发布一次。</summary>
    [Test]
    public void Machinegun_ReplacesThreeRemainingCardsWithThreeBurstsInOneAtomicLayout()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3261, 3201, 3202, 3203 },
            initialHandCount: 4,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId resolvingCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3261);
        CardInstanceId[] discardedCardIds = scenario.Zones.Hand
            .Where(cardId => cardId != resolvingCardId)
            .ToArray();
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;
        int layoutPublicationCount = 0;

        BattleCommandExecutionResult result;
        using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = scenario.Play(3261, targetId: null);

        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 9)));
        BattleEnergySpentSettlement energy = FindSettlement<BattleEnergySpentSettlement>(result);
        BattleBlockGainedSettlement block = FindSettlement<BattleBlockGainedSettlement>(result);
        Assert.That(energy.Order, Is.EqualTo(0));
        Assert.That(energy.Amount, Is.EqualTo(2));
        Assert.That(block.Order, Is.EqualTo(1));
        Assert.That(block.Amount, Is.EqualTo(10));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(10));

        BattleCardMovedSettlement[] moves = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .ToArray();
        Assert.That(moves, Has.Length.EqualTo(4));
        Assert.That(moves[0].Order, Is.EqualTo(2));
        Assert.That(moves[0].CardId, Is.EqualTo(resolvingCardId));
        Assert.That(moves[0].FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(moves[0].ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(moves.Skip(1).Select(item => item.Order), Is.EqualTo(new[] { 3, 4, 5 }));
        Assert.That(moves.Skip(1).Select(item => item.CardId), Is.EqualTo(discardedCardIds));
        Assert.That(moves.Skip(1).All(item =>
            item.FromZone == BattleCardZone.Hand &&
            item.ToZone == BattleCardZone.DiscardPile), Is.True);

        BattleCardCreatedSettlement[] created = result.Settlements
            .OfType<BattleCardCreatedSettlement>()
            .ToArray();
        Assert.That(created, Has.Length.EqualTo(3));
        Assert.That(created.Select(item => item.Order), Is.EqualTo(new[] { 6, 7, 8 }));
        Assert.That(created.Select(item => item.TemplateId), Is.EqualTo(new[] { 3263, 3263, 3263 }));
        Assert.That(created.All(item => item.ToZone == BattleCardZone.Hand), Is.True);
        Assert.That(scenario.Zones.Hand, Is.EqualTo(created.Select(item => item.CardId)));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardedCardIds));
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { resolvingCardId }));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
    }

    /// <summary>验证固定机枪是唯一手牌时零换零合法，仍支付二费、获得十格挡并只把自身移入消耗区。</summary>
    [Test]
    public void Machinegun_AsOnlyCard_AllowsZeroReplacement()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3261 },
            initialHandCount: 1,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId resolvingCardId = scenario.Zones.Hand.Single();
        int cardsBefore = scenario.Zones.Cards.Count;
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;

        BattleCommandExecutionResult result = scenario.Play(3261, targetId: null);

        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 3)));
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(2));
        Assert.That(FindSettlement<BattleBlockGainedSettlement>(result).Amount, Is.EqualTo(10));
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);
        Assert.That(departure.Order, Is.EqualTo(2));
        Assert.That(departure.CardId, Is.EqualTo(resolvingCardId));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(result.Settlements.OfType<BattleCardCreatedSettlement>(), Is.Empty);
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { resolvingCardId }));
        Assert.That(scenario.Zones.Cards, Has.Count.EqualTo(cardsBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
    }

    /// <summary>验证固定机枪生成的基础机枪爆射拥有真实本局实例，并能继续经同一队列打出两段五伤后进入消耗区。</summary>
    [Test]
    public void Machinegun_CreatedBurstCanBePlayedThroughTheSameQueue()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3261, 3203 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 2,
            initialAmmo: 0);
        scenario.StartBattle();
        uint cardRandomBeforeMachinegun = scenario.Session.MachineGunnerRuntime.CardRandomState;

        BattleCommandExecutionResult machinegun = scenario.Play(3261, targetId: null);
        BattleCardCreatedSettlement created = machinegun.Settlements
            .OfType<BattleCardCreatedSettlement>()
            .Single();

        Assert.That(created.TemplateId, Is.EqualTo(3263));
        Assert.That(created.ToZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { created.CardId }));
        Assert.That(scenario.Zones.TryGetCard(created.CardId, out CardInstanceData createdCard), Is.True);
        Assert.That(createdCard.TemplateId, Is.EqualTo(3263));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBeforeMachinegun));

        BattleCommandExecutionResult burst = scenario.Play(3263, targetId: null);

        Assert.That(burst.Settlements.OfType<BattleDamageAppliedSettlement>().Count(), Is.EqualTo(2));
        Assert.That(burst.Settlements.OfType<BattleDamageAppliedSettlement>()
            .Select(item => item.AttackValue), Is.EqualTo(new[] { 5, 5 }));
        Assert.That(burst.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        BattleCardMovedSettlement departure = burst.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single();
        Assert.That(departure.CardId, Is.EqualTo(created.CardId));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(scenario.Zones.ExhaustPile, Does.Contain(created.CardId));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.Not.EqualTo(cardRandomBeforeMachinegun));
    }

    /// <summary>验证固定机枪能量不足时经队列失败且保持能量、格挡、卡区、实例、两条随机流和表现结果全零写。</summary>
    [Test]
    public void Machinegun_InsufficientEnergyFailsWithoutAnyWrites()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3261, 3201, 3202 },
            initialHandCount: 3,
            enemyDamage: 0,
            initialEnergy: 1);
        scenario.StartBattle();
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        CardInstanceId[] cardIdsBefore = scenario.Zones.Cards.Keys.ToArray();
        int energyBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy;
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;
        int blockBefore = scenario.Player.CurrentBlock;
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        int resultCountBefore = scenario.Results.Count;
        int layoutPublicationCount = 0;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission;
        using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            submission = scenario.Submit(3261, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(blockBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(energyBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(scenario.Zones.Cards.Keys, Is.EquivalentTo(cardIdsBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
        Assert.That(layoutPublicationCount, Is.Zero);
    }

    /// <summary>验证机枪爆射经公共队列随机结算两段五点伤害，不实际耗弹，并在结算后进入消耗区。</summary>
    [Test]
    public void MachinegunBurst_DealsTwoRandomFiveDamageWithoutAmmoSpendAndExhausts()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3263 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 0,
            initialAmmo: 0);
        scenario.StartBattle();
        CardInstanceId playedCardId = scenario.Zones.Hand.Single();
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;

        BattleCommandExecutionResult result = scenario.Play(3263, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(MachineGunnerCardProgramRegistry.TryGet(
            cfg.battle.MachineGunnerProgramId.MachinegunBurst,
            out MachineGunnerCardProgram program), Is.True);
        Assert.That(program.Tags, Is.EqualTo(MachineGunnerCardTag.None));
        Assert.That(program.IsShootCategory, Is.False);
        Assert.That(program.ReceivesStimBonus, Is.False);
        Assert.That(program.ReceivesIncendiaryAmmo, Is.False);
        Assert.That(program.ParticipatesInNonShootAttackSynergies, Is.False);
        Assert.That(damages, Has.Length.EqualTo(2));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 5, 5 }));
        foreach (BattleDamageAppliedSettlement damage in damages)
        {
            Assert.That(
                new[] { scenario.FirstEnemy.Id, scenario.SecondEnemy.Id },
                Does.Contain(damage.TargetId));
        }

        Assert.That(scenario.FirstEnemy.CurrentHealth + scenario.SecondEnemy.CurrentHealth,
            Is.EqualTo(190));
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.Not.EqualTo(cardRandomBefore));
        Assert.That(departure.CardId, Is.EqualTo(playedCardId));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(damages.Last().Order, Is.LessThan(departure.Order));
        Assert.That(scenario.Zones.ExhaustPile, Does.Contain(playedCardId));
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证机枪爆射不实际耗弹，但游击战术仍按名义耗弹二在两段伤害后结算四点格挡。</summary>
    [Test]
    public void MachinegunBurst_GuerrillaUsesNominalTwoAmmoAfterDamageBeforeDeparture()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3251, 3263 },
            initialHandCount: 2,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 1,
            initialAmmo: 3);
        scenario.StartBattle();
        scenario.Play(3251, targetId: null);
        CardInstanceId playedCardId = scenario.Zones.Hand.Single();
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;

        BattleCommandExecutionResult result = scenario.Play(3263, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        BattleBlockGainedSettlement block = FindSettlement<BattleBlockGainedSettlement>(result);
        BattleCardMovedSettlement departure = FindSettlement<BattleCardMovedSettlement>(result);

        Assert.That(scenario.Session.MachineGunnerRuntime.GetPowerStack(
            MachineGunnerPowerKind.GuerrillaTactics), Is.EqualTo(2));
        Assert.That(damages, Has.Length.EqualTo(2));
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(block.Amount, Is.EqualTo(4));
        Assert.That(block.BlockBefore, Is.Zero);
        Assert.That(block.BlockAfter, Is.EqualTo(4));
        Assert.That(damages.Last().Order, Is.LessThan(block.Order));
        Assert.That(block.Order, Is.LessThan(departure.Order));
        Assert.That(departure.CardId, Is.EqualTo(playedCardId));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证机枪爆射作为特殊攻击既不接收射击联动，也不参与非射击攻击联动或令连肘免费。</summary>
    [Test]
    public void MachinegunBurst_WithoutShootTagDoesNotReceiveShootOrNonShootSynergies()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3210, 3267, 3212, 3253, 3205, 3260, 3260, 3263, 3242 },
            initialHandCount: 9,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 5,
            initialAmmo: 3);
        scenario.StartBattle();
        scenario.Play(3210, targetId: null);
        scenario.Play(3267, targetId: null);
        scenario.Play(3212, targetId: null);
        scenario.Play(3253, targetId: null);
        scenario.Play(3205, targetId: null);
        scenario.Play(3260, targetId: null);
        scenario.Play(3260, targetId: null);
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;

        BattleCommandExecutionResult result = scenario.Play(3263, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();
        MachineGunnerPrivateStatusChangedSettlement[] privateStatuses = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();

        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
        Assert.That(damages, Has.Length.EqualTo(2));
        Assert.That(damages.Select(item => item.AttackValue), Is.EqualTo(new[] { 5, 5 }));
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(privateStatuses.Where(item =>
            item.Status == MachineGunnerCombatantStatus.Burn), Is.Empty);
        Assert.That(privateStatuses.Where(item =>
            item.Status == MachineGunnerCombatantStatus.Oil), Is.Empty);
        Assert.That(result.Settlements.OfType<BattleBlockGainedSettlement>(), Is.Empty);
        Assert.That(scenario.Player.CurrentBlock, Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.FirstEnemy.Id,
            MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.SecondEnemy.Id,
            MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.FirstEnemy.Id,
            MachineGunnerCombatantStatus.Oil), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.SecondEnemy.Id,
            MachineGunnerCombatantStatus.Oil), Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(2));

        BattleCommandExecutionResult combo = scenario.Play(3242, targetId: null);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(combo).Amount, Is.EqualTo(2));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
    }

    /// <summary>验证机枪爆射首段击杀后会从剩余存活敌人中重新随机选择第二段目标。</summary>
    [Test]
    public void MachinegunBurst_ReSelectsFromLivingEnemiesAfterFirstHitKillsTarget()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3263 },
            initialHandCount: 1,
            firstEnemyHealth: 5,
            secondEnemyHealth: 5,
            enemyDamage: 0,
            initialEnergy: 0,
            initialAmmo: 3);
        scenario.StartBattle();
        CardInstanceId playedCardId = scenario.Zones.Hand.Single();
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;

        BattleCommandExecutionResult result = scenario.Play(3263, targetId: null);
        BattleDamageAppliedSettlement[] damages = result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .ToArray();

        Assert.That(damages, Has.Length.EqualTo(2));
        Assert.That(damages.Select(item => item.TargetId), Is.EquivalentTo(new[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.Zero);
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.Zero);
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.Zones.ExhaustPile, Does.Contain(playedCardId));
    }

    /// <summary>验证机枪爆射携带显式敌方目标时由公共队列拒绝，并保持参与者、资源、随机流、卡区和表现结果零写入。</summary>
    [Test]
    public void MachinegunBurst_ExplicitTargetFailsWithoutWrites()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3263 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 2,
            initialAmmo: 3);
        scenario.StartBattle();
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
        CardInstanceId[] exhaustBefore = scenario.Zones.ExhaustPile.ToArray();
        int firstEnemyHealthBefore = scenario.FirstEnemy.CurrentHealth;
        int secondEnemyHealthBefore = scenario.SecondEnemy.CurrentHealth;
        PlayerTurnData turnBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id];
        int energyBefore = turnBefore.Energy;
        int ammoBefore = turnBefore.Ammo;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        int resultCountBefore = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Submit(
            3263,
            scenario.FirstEnemy.Id);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(firstEnemyHealthBefore));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(secondEnemyHealthBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(energyBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore));
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(exhaustBefore));
    }

    /// <summary>验证天空之怒成功施放时进入能力区并每张叠加一层，自身不产生伤害也不推进随机流。</summary>
    [Test]
    public void SkyWrath_StacksOnePerPowerCardWithoutImmediateDamageOrRandomAdvance()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3266, 3266 },
            initialHandCount: 2,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();
        uint randomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;

        BattleCommandExecutionResult first = scenario.Play(3266, targetId: null);
        BattleCommandExecutionResult second = scenario.Play(3266, targetId: null);

        Assert.That(first.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(second.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(first).Amount, Is.EqualTo(1));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(first).ToZone,
            Is.EqualTo(BattleCardZone.PowerPile));
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(2));
        Assert.That(scenario.Session.MachineGunnerRuntime.GetPowerStack(
            MachineGunnerPowerKind.SkyWrath), Is.EqualTo(2));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
    }

    /// <summary>验证天空之怒因能量或目标门禁失败时，不写入能量、层数、卡区、伤害与随机流。</summary>
    [Test]
    public void SkyWrath_FailedCostOrExplicitTargetWritesNothing()
    {
        using (var energyScenario = new MachineGunnerStarterScenario(
                   new[] { 3266 },
                   initialHandCount: 1,
                   initialEnergy: 0,
                   enemyDamage: 0))
        {
            energyScenario.StartBattle();
            int resultCountBefore = energyScenario.Results.Count;
            uint randomBefore = energyScenario.Session.MachineGunnerRuntime.CardRandomState;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                energyScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                energyScenario.Submit(3266, targetId: null));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(energyScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(energyScenario.Queue.Turn.CurrentValue.Players[energyScenario.Player.Id].Energy,
                Is.Zero);
            Assert.That(energyScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.SkyWrath), Is.Zero);
            Assert.That(energyScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(energyScenario.Zones.PowerPile, Is.Empty);
            Assert.That(energyScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(randomBefore));
        }

        using (var targetScenario = new MachineGunnerStarterScenario(
                   new[] { 3266 },
                   initialHandCount: 1,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            targetScenario.StartBattle();
            int resultCountBefore = targetScenario.Results.Count;
            uint randomBefore = targetScenario.Session.MachineGunnerRuntime.CardRandomState;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                targetScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                targetScenario.Submit(3266, targetScenario.FirstEnemy.Id));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(targetScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(targetScenario.Queue.Turn.CurrentValue.Players[targetScenario.Player.Id].Energy,
                Is.EqualTo(1));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.GetPowerStack(
                MachineGunnerPowerKind.SkyWrath), Is.Zero);
            Assert.That(targetScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(targetScenario.Zones.PowerPile, Is.Empty);
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证狂轰滥炸每次成功打出叠加四层、进入能力区，且自身不产生即时伤害或状态。</summary>
    [Test]
    public void Bombard_StacksFourPerPowerCardWithoutImmediateCombatEffect()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3265, 3265 },
            initialHandCount: 2,
            initialEnergy: 2,
            enemyDamage: 0);
        scenario.StartBattle();

        BattleCommandExecutionResult first = scenario.Play(3265, targetId: null);

        Assert.That(first.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(first.Settlements.OfType<BattleBlockGainedSettlement>(), Is.Empty);
        Assert.That(first.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(first).Amount, Is.EqualTo(1));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(first).ToZone, Is.EqualTo(BattleCardZone.PowerPile));
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(1));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.Bombard),
            Is.EqualTo(4));

        BattleCommandExecutionResult second = scenario.Play(3265, targetId: null);

        Assert.That(second.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(second.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>(), Is.Empty);
        Assert.That(scenario.Zones.PowerPile, Has.Count.EqualTo(2));
        Assert.That(
            scenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.Bombard),
            Is.EqualTo(8));
    }

    /// <summary>验证狂轰滥炸支付或目标门禁失败时不写入能量、能力层数、卡区和随机流。</summary>
    [Test]
    public void Bombard_FailedCostOrExplicitTargetWritesNothing()
    {
        using (var energyScenario = new MachineGunnerStarterScenario(
                   new[] { 3265 },
                   initialHandCount: 1,
                   initialEnergy: 0,
                   enemyDamage: 0))
        {
            energyScenario.StartBattle();
            int resultCountBefore = energyScenario.Results.Count;
            uint randomBefore = energyScenario.Session.MachineGunnerRuntime.CardRandomState;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                energyScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = energyScenario.Submit(3265, targetId: null);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(energyScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(energyScenario.Queue.Turn.CurrentValue.Players[energyScenario.Player.Id].Energy, Is.Zero);
            Assert.That(energyScenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.Bombard), Is.Zero);
            Assert.That(energyScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(energyScenario.Zones.PowerPile, Is.Empty);
            Assert.That(energyScenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
        }

        using (var targetScenario = new MachineGunnerStarterScenario(
                   new[] { 3265 },
                   initialHandCount: 1,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            targetScenario.StartBattle();
            int resultCountBefore = targetScenario.Results.Count;
            uint randomBefore = targetScenario.Session.MachineGunnerRuntime.CardRandomState;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                targetScenario.Queue.RecordExecutionLifecycle();

            BattleCommandSubmissionResult submission = targetScenario.Submit(
                3265,
                targetScenario.FirstEnemy.Id);
            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

            Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(targetScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(targetScenario.Queue.Turn.CurrentValue.Players[targetScenario.Player.Id].Energy, Is.EqualTo(1));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.GetPowerStack(MachineGunnerPowerKind.Bombard), Is.Zero);
            Assert.That(targetScenario.Zones.Hand, Has.Count.EqualTo(1));
            Assert.That(targetScenario.Zones.PowerPile, Is.Empty);
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证单张固有牌在不同洗牌种子下都先进入五张起手，且启战只发布一次完整布局。</summary>
    [Test]
    public void StartBattle_SingleInnateAlwaysEntersFiveCardOpeningHandWithOneLayoutPublication()
    {
        int[] seeds = { 1, 2, 1234, 9876 };
        foreach (int seed in seeds)
        {
            using var scenario = new MachineGunnerStarterScenario(
                new[] { 3201, 3202, 3203, 3204, 3205, 3275, 3201, 3202 },
                initialHandCount: 5,
                enemyDamage: 0,
                randomSeed: seed);
            int layoutPublicationCount = 0;
            using IDisposable subscription = scenario.Zones.Layout
                .Skip(1)
                .Subscribe(_ => layoutPublicationCount++);

            BattleCommandExecutionResult result = scenario.StartBattleResult();

            Assert.That(result.Succeeded, Is.True, $"seed={seed}");
            Assert.That(layoutPublicationCount, Is.EqualTo(1), $"seed={seed}");
            Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(5), $"seed={seed}");
            Assert.That(
                scenario.Zones.Cards[scenario.Zones.Hand[0]].TemplateId,
                Is.EqualTo(3275),
                $"seed={seed}");
            Assert.That(
                scenario.Zones.Hand.Count(cardId =>
                    scenario.Zones.Cards[cardId].TemplateId == 3275),
                Is.EqualTo(1),
                $"seed={seed}");
            Assert.That(
                result.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)),
                $"seed={seed}");
        }
    }

    /// <summary>验证多张固有牌依已洗牌抽取顺序稳定前置，再由普通牌补足五张。</summary>
    [Test]
    public void StartBattle_MultipleInnatesKeepShuffledDrawOrderThenOrdinaryCardsFillTarget()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3275, 3201, 3202, 3275, 3203, 3204, 3205, 3201 },
            initialHandCount: 5,
            enemyDamage: 0,
            randomSeed: 1357);
        CardInstanceId[] drawOrder = scenario.Zones.DrawPile.Reverse().ToArray();
        CardInstanceId[] expectedInnates = drawOrder
            .Where(cardId => scenario.Zones.Cards[cardId].TemplateId == 3275)
            .ToArray();
        CardInstanceId[] expectedOrdinary = drawOrder
            .Where(cardId => scenario.Zones.Cards[cardId].TemplateId != 3275)
            .Take(5 - expectedInnates.Length)
            .ToArray();
        CardInstanceId[] expectedHand = expectedInnates.Concat(expectedOrdinary).ToArray();
        int layoutPublicationCount = 0;
        using IDisposable subscription = scenario.Zones.Layout
            .Skip(1)
            .Subscribe(_ => layoutPublicationCount++);

        BattleCommandExecutionResult result = scenario.StartBattleResult();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(expectedHand));
        Assert.That(
            result.Settlements.OfType<BattleCardMovedSettlement>().Select(item => item.CardId),
            Is.EqualTo(expectedHand));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证固有牌数量超过开局目标但未超手牌上限时全部入手，不再补普通牌。</summary>
    [Test]
    public void StartBattle_InnatesAboveTargetButWithinHandLimitAllEnterWithoutOrdinaryFill()
    {
        int[] deck = Enumerable.Repeat(3275, 6)
            .Concat(new[] { 3201, 3202, 3203 })
            .ToArray();
        using var scenario = new MachineGunnerStarterScenario(
            deck,
            initialHandCount: 5,
            enemyDamage: 0,
            randomSeed: 2468);
        CardInstanceId[] expectedInnates = scenario.Zones.DrawPile
            .Reverse()
            .Where(cardId => scenario.Zones.Cards[cardId].TemplateId == 3275)
            .ToArray();
        int layoutPublicationCount = 0;
        using IDisposable subscription = scenario.Zones.Layout
            .Skip(1)
            .Subscribe(_ => layoutPublicationCount++);

        BattleCommandExecutionResult result = scenario.StartBattleResult();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(expectedInnates));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(6));
        Assert.That(scenario.Zones.Hand.All(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3275), Is.True);
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证固有牌数量超过十张手牌上限时启战明确失败，且首次布局发布前保持零写入。</summary>
    [Test]
    public void StartBattle_InnatesAboveHandLimitFailBeforeAnyLayoutWrite()
    {
        using var scenario = new MachineGunnerStarterScenario(
            Enumerable.Repeat(3275, 11).ToArray(),
            initialHandCount: 5,
            enemyDamage: 0,
            randomSeed: 7531);
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        int layoutPublicationCount = 0;
        using IDisposable subscription = scenario.Zones.Layout
            .Skip(1)
            .Subscribe(_ => layoutPublicationCount++);
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Queue.Submit(
            new StartBattleCommand());
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InvalidOpeningHandConfiguration));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(layoutPublicationCount, Is.Zero);
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DrawPile, Has.Count.EqualTo(11));
        Assert.That(scenario.Queue.Turn.CurrentValue.Phase,
            Is.EqualTo(BattleTurnPhase.NotStarted));
    }

    /// <summary>验证没有固有牌时，新起手规则完全保留旧的同种子抽牌确定性。</summary>
    [Test]
    public void StartBattle_WithoutInnatesPreservesExistingSeededOpeningDraw()
    {
        int[] deck = { 3201, 3202, 3203, 3204, 3205, 3201, 3202, 3203 };
        using var first = new MachineGunnerStarterScenario(
            deck,
            initialHandCount: 5,
            enemyDamage: 0,
            randomSeed: 8642);
        using var second = new MachineGunnerStarterScenario(
            deck,
            initialHandCount: 5,
            enemyDamage: 0,
            randomSeed: 8642);
        CardInstanceId[] expected = first.Zones.DrawPile.Reverse().Take(5).ToArray();

        BattleCommandExecutionResult firstResult = first.StartBattleResult();
        BattleCommandExecutionResult secondResult = second.StartBattleResult();

        Assert.That(firstResult.Succeeded, Is.True);
        Assert.That(secondResult.Succeeded, Is.True);
        Assert.That(first.Zones.Hand, Is.EqualTo(expected));
        Assert.That(second.Zones.Hand, Is.EqualTo(first.Zones.Hand));
        Assert.That(second.Zones.DrawPile, Is.EqualTo(first.Zones.DrawPile));
        Assert.That(second.Zones.ShuffleRandomState, Is.EqualTo(first.Zones.ShuffleRandomState));
    }

    /// <summary>验证隐秘行动支付一点能量、获得一层隐身，并在自身离手前执行普通抽一张。</summary>
    [Test]
    public void StealthAction_GainsInvisibleAndDrawsBeforeThePlayedCardLeavesHand()
    {
        int[] deck = new[] { 3275 }
            .Concat(Enumerable.Repeat(3203, 10))
            .ToArray();
        using var scenario = new MachineGunnerStarterScenario(
            deck,
            initialHandCount: 9,
            initialEnergy: 3,
            enemyDamage: 0);
        scenario.StartBattle();
        CardInstanceId playedCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3275);

        BattleCommandExecutionResult result = scenario.Play(3275, targetId: null);
        BattleCardMovedSettlement drawn = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.FromZone == BattleCardZone.DrawPile &&
                item.ToZone == BattleCardZone.Hand);
        BattleCardMovedSettlement departure = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == playedCardId);
        MachineGunnerPrivateStatusChangedSettlement invisible = result.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.Invisible);

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(result).Amount, Is.EqualTo(1));
        Assert.That(invisible.ValueBefore, Is.Zero);
        Assert.That(invisible.ValueAfter, Is.EqualTo(1));
        Assert.That(drawn.Order, Is.LessThan(departure.Order));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(9));
        Assert.That(scenario.Zones.Hand.Contains(playedCardId), Is.False);
        Assert.That(scenario.Zones.DiscardPile, Does.Contain(playedCardId));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(2));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Invisible), Is.EqualTo(1));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }

    /// <summary>验证十张满手时隐秘行动的抽牌先被上限截止，随后自身弃置使最终手牌为九张。</summary>
    [Test]
    public void StealthAction_AtTenCardsDrawsZeroBeforeDepartureAndEndsWithNine()
    {
        int[] deck = new[] { 3275 }
            .Concat(Enumerable.Repeat(3203, 10))
            .ToArray();
        using var scenario = new MachineGunnerStarterScenario(
            deck,
            initialHandCount: 10,
            initialEnergy: 1,
            enemyDamage: 0);
        scenario.StartBattle();
        CardInstanceId playedCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3275);
        int drawPileBefore = scenario.Zones.DrawPile.Count;

        BattleCommandExecutionResult result = scenario.Play(3275, targetId: null);

        Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>()
            .Where(item => item.FromZone == BattleCardZone.DrawPile &&
                item.ToZone == BattleCardZone.Hand), Is.Empty);
        Assert.That(scenario.Zones.DrawPile, Has.Count.EqualTo(drawPileBefore));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(9));
        Assert.That(scenario.Zones.Hand.Contains(playedCardId), Is.False);
        Assert.That(scenario.Zones.DiscardPile, Does.Contain(playedCardId));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Invisible), Is.EqualTo(1));
    }

    /// <summary>验证隐秘行动因能量不足或显式敌方目标被拒绝时，资源、隐身、卡区与随机流全部零写入。</summary>
    [Test]
    public void StealthAction_FailedCostOrExplicitTargetWritesNothing()
    {
        using (var energyScenario = new MachineGunnerStarterScenario(
                   new[] { 3275 },
                   initialHandCount: 1,
                   initialEnergy: 0,
                   enemyDamage: 0))
        {
            energyScenario.StartBattle();
            CardZoneLayoutData layoutBefore = energyScenario.Zones.Layout.CurrentValue;
            uint cardRandomBefore = energyScenario.Session.MachineGunnerRuntime.CardRandomState;
            uint shuffleRandomBefore = energyScenario.Zones.ShuffleRandomState;
            int resultCountBefore = energyScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                energyScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                energyScenario.Submit(3275, targetId: null));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(energyScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(energyScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(energyScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
            Assert.That(energyScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(energyScenario.Session.MachineGunnerRuntime.CombatState.Get(
                energyScenario.Player.Id,
                MachineGunnerCombatantStatus.Invisible), Is.Zero);
        }

        using (var targetScenario = new MachineGunnerStarterScenario(
                   new[] { 3275 },
                   initialHandCount: 1,
                   initialEnergy: 1,
                   enemyDamage: 0))
        {
            targetScenario.StartBattle();
            CardZoneLayoutData layoutBefore = targetScenario.Zones.Layout.CurrentValue;
            uint cardRandomBefore = targetScenario.Session.MachineGunnerRuntime.CardRandomState;
            uint shuffleRandomBefore = targetScenario.Zones.ShuffleRandomState;
            int resultCountBefore = targetScenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                targetScenario.Queue.RecordExecutionLifecycle();

            BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(
                targetScenario.Submit(3275, targetScenario.FirstEnemy.Id));

            Assert.That(terminal.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(terminal.Settlements, Is.Empty);
            Assert.That(targetScenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(targetScenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(targetScenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CardRandomState,
                Is.EqualTo(cardRandomBefore));
            Assert.That(targetScenario.Session.MachineGunnerRuntime.CombatState.Get(
                targetScenario.Player.Id,
                MachineGunnerCombatantStatus.Invisible), Is.Zero);
        }
    }

    /// <summary>验证隐秘行动不是射击或攻击，不消耗弹药且不触发兴奋剂、燃烧弹药与便携帮手联动。</summary>
    [Test]
    public void StealthAction_DoesNotTriggerShootStimIncendiaryOrPortableHelper()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3267, 3210, 3205, 3275 },
            initialHandCount: 4,
            initialEnergy: 4,
            enemyDamage: 0);
        scenario.StartBattle();
        scenario.Play(3267, targetId: null);
        scenario.Play(3210, targetId: null);
        scenario.Play(3205, targetId: null);
        int ammoBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;

        BattleCommandExecutionResult result = scenario.Play(3275, targetId: null);

        Assert.That(result.Settlements.OfType<BattleDamageAppliedSettlement>(), Is.Empty);
        Assert.That(result.Settlements.OfType<BattleAmmoSpentSettlement>(), Is.Empty);
        Assert.That(result.Settlements.OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn), Is.Empty);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo,
            Is.EqualTo(ammoBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.StimTurns, Is.EqualTo(1));
        Assert.That(scenario.Session.MachineGunnerRuntime.GetPowerStack(
            MachineGunnerPowerKind.PortableHelper), Is.EqualTo(1));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Invisible), Is.EqualTo(1));
    }

    /// <summary>验证散热在没有可选择的其他手牌时仍成功弃置自身，但不会获得能量。</summary>
    [Test]
    public void VentHeat_AsOnlyCard_DiscardsSourceWithoutEnergyGain()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3244 },
            initialHandCount: 1,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single();
        int energyBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy;
        int layoutPublicationCount = 0;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission;
        using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            submission = scenario.Submit(3244, targetId: null);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Settlements.OfType<BattleEnergyGainedSettlement>(), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(energyBefore));
        Assert.That(scenario.Zones.Hand.Contains(sourceCardId), Is.False);
        Assert.That(scenario.Zones.DiscardPile, Does.Contain(sourceCardId));
        Assert.That(scenario.Zones.ExhaustPile.Contains(sourceCardId), Is.False);
        Assert.That(layoutPublicationCount, Is.EqualTo(1));
    }

    /// <summary>验证散热通过不可变选择快照消耗另一张手牌后获得一点能量，并以单次布局发布弃置自身。</summary>
    [Test]
    public void VentHeat_SelectedOtherCard_ExhaustsThenGainsEnergyAndDiscardsSource()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId selectedCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3203);
        int energyBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy;
        int layoutPublicationCount = 0;
        var selectedCardIds = new[] { selectedCardId };
        var command = new PlayCardCommand(
            scenario.Player.Id,
            sourceCardId,
            targetId: null,
            selectedCardIds: selectedCardIds);
        selectedCardIds[0] = sourceCardId;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission;
        using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            submission = scenario.Queue.Submit(command);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(command.SelectedCardIds, Is.EqualTo(new[] { selectedCardId }));
        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Settlements, Has.Count.EqualTo(4));
        Assert.That(terminal.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 4)));

        BattleEnergySpentSettlement spent = terminal.Settlements
            .OfType<BattleEnergySpentSettlement>()
            .Single();
        BattleCardMovedSettlement selectedMove = terminal.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == selectedCardId);
        BattleEnergyGainedSettlement gained = terminal.Settlements
            .OfType<BattleEnergyGainedSettlement>()
            .Single();
        BattleCardMovedSettlement sourceMove = terminal.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == sourceCardId);

        Assert.That(spent.Order, Is.Zero);
        Assert.That(spent.Amount, Is.Zero);
        Assert.That(selectedMove.Order, Is.EqualTo(1));
        Assert.That(selectedMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(selectedMove.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(gained.Order, Is.EqualTo(2));
        Assert.That(gained.EnergyBefore, Is.EqualTo(energyBefore));
        Assert.That(gained.EnergyAfter, Is.EqualTo(energyBefore + 1));
        Assert.That(gained.Amount, Is.EqualTo(1));
        Assert.That(sourceMove.Order, Is.EqualTo(3));
        Assert.That(sourceMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(sourceMove.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(energyBefore + 1));
        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(scenario.Zones.DrawPile, Is.Empty);
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
        Assert.That(scenario.Zones.PowerPile, Is.Empty);
        CardInstanceId[] cardsInZones = scenario.Zones.DrawPile
            .Concat(scenario.Zones.Hand)
            .Concat(scenario.Zones.DiscardPile)
            .Concat(scenario.Zones.ExhaustPile)
            .Concat(scenario.Zones.PowerPile)
            .ToArray();
        Assert.That(cardsInZones, Is.EquivalentTo(new[] { sourceCardId, selectedCardId }));
        Assert.That(cardsInZones.Distinct().Count(), Is.EqualTo(cardsInZones.Length));
    }

    /// <summary>验证散热缺少必选手牌时由公共规则返回可交互选择请求，并由队列保持全部权威事实零写。</summary>
    [Test]
    public void VentHeat_EmptySelection_RequiresOneLegalOtherHandCardWithoutWrites()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId legalCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3203);
        var command = new PlayCardCommand(
            scenario.Player.Id,
            sourceCardId,
            targetId: null,
            selectedCardIds: Array.Empty<CardInstanceId>());
        BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        int energyBefore = turnBefore.Players[scenario.Player.Id].Energy;
        CardInstanceId[] drawPileBefore = scenario.Zones.DrawPile.ToArray();
        CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
        CardInstanceId[] discardPileBefore = scenario.Zones.DiscardPile.ToArray();
        CardInstanceId[] exhaustPileBefore = scenario.Zones.ExhaustPile.ToArray();
        CardInstanceId[] powerPileBefore = scenario.Zones.PowerPile.ToArray();
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        int resultCountBefore = scenario.Results.Count;

        BattleCardPlayEvaluation evaluation = scenario.CardPlayRules.Evaluate(turnBefore, command);

        Assert.That(evaluation.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.CardSelectionRequired));
        Assert.That(evaluation.Succeeded, Is.False);
        Assert.That(evaluation.CanStartInteraction, Is.True);
        Assert.That(evaluation.HandCardSelectionRequest, Is.Not.Null);
        Assert.That(evaluation.HandCardSelectionRequest.RequiredCount, Is.EqualTo(1));
        Assert.That(evaluation.HandCardSelectionRequest.LegalCardIds,
            Is.EqualTo(new[] { legalCardId }));
        Assert.That(((IList<CardInstanceId>)evaluation.HandCardSelectionRequest.LegalCardIds).IsReadOnly,
            Is.True);

        int turnPublicationCount = 0;
        int layoutPublicationCount = 0;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission;
        using (scenario.Queue.Turn.Skip(1).Subscribe(_ => turnPublicationCount++))
        using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            submission = scenario.Queue.Submit(command);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.CardSelectionRequired));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(energyBefore));
        Assert.That(scenario.Zones.DrawPile, Is.EqualTo(drawPileBefore));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardPileBefore));
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(exhaustPileBefore));
        Assert.That(scenario.Zones.PowerPile, Is.EqualTo(powerPileBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
        Assert.That(turnPublicationCount, Is.Zero);
        Assert.That(layoutPublicationCount, Is.Zero);
    }

    /// <summary>验证散热的数量非法、自选来源与已过期选择分别返回稳定失败码，并保持提交前权威事实零写。</summary>
    [Test]
    public void VentHeat_InvalidOrStaleSelectionsFailWithoutWrites()
    {
        using (var countScenario = new MachineGunnerStarterScenario(
                   new[] { 3244, 3203, 3204 },
                   initialHandCount: 3,
                   enemyDamage: 0,
                   initialEnergy: 2))
        {
            countScenario.StartBattle();
            CardInstanceId sourceCardId = countScenario.Zones.Hand
                .Single(cardId => countScenario.Zones.Cards[cardId].TemplateId == 3244);
            CardInstanceId[] selectedCardIds = countScenario.Zones.Hand
                .Where(cardId => cardId != sourceCardId)
                .ToArray();
            var command = new PlayCardCommand(
                countScenario.Player.Id,
                sourceCardId,
                targetId: null,
                selectedCardIds: selectedCardIds);

            AssertPlayCardFailureWithoutWrites(
                countScenario,
                command,
                BattleCommandExecutionFailureReason.InvalidCardSelectionCount);
        }

        using (var sourceScenario = new MachineGunnerStarterScenario(
                   new[] { 3244, 3203 },
                   initialHandCount: 2,
                   enemyDamage: 0,
                   initialEnergy: 2))
        {
            sourceScenario.StartBattle();
            CardInstanceId sourceCardId = sourceScenario.Zones.Hand
                .Single(cardId => sourceScenario.Zones.Cards[cardId].TemplateId == 3244);
            var command = new PlayCardCommand(
                sourceScenario.Player.Id,
                sourceCardId,
                targetId: null,
                selectedCardIds: new[] { sourceCardId });

            AssertPlayCardFailureWithoutWrites(
                sourceScenario,
                command,
                BattleCommandExecutionFailureReason.SelectedCardNotEligible);
        }

        using (var staleScenario = new MachineGunnerStarterScenario(
                   new[] { 3244, 3203 },
                   initialHandCount: 2,
                   enemyDamage: 0,
                   initialEnergy: 2))
        {
            staleScenario.StartBattle();
            CardInstanceId sourceCardId = staleScenario.Zones.Hand
                .Single(cardId => staleScenario.Zones.Cards[cardId].TemplateId == 3244);
            CardInstanceId selectedCardId = staleScenario.Zones.Hand
                .Single(cardId => staleScenario.Zones.Cards[cardId].TemplateId == 3203);
            var command = new PlayCardCommand(
                staleScenario.Player.Id,
                sourceCardId,
                targetId: null,
                selectedCardIds: new[] { selectedCardId });
            BattleCardZoneOperationResult setupMove =
                staleScenario.Zones.DiscardFromHand(selectedCardId);
            Assert.That(setupMove.Succeeded, Is.True);

            AssertPlayCardFailureWithoutWrites(
                staleScenario,
                command,
                BattleCommandExecutionFailureReason.SelectedCardNotInHand);
        }
    }

    /// <summary>验证散热在能量已满时仍消耗所选手牌并弃置自身，但不伪造零增量获能记录。</summary>
    [Test]
    public void VentHeat_AtEnergyMaximumExhaustsSelectionWithoutFakeEnergyGain()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 5);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId selectedCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3203);
        var command = new PlayCardCommand(
            scenario.Player.Id,
            sourceCardId,
            targetId: null,
            selectedCardIds: new[] { selectedCardId });
        int resultCountBefore = scenario.Results.Count;
        int layoutPublicationCount = 0;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission;
        using (scenario.Zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            submission = scenario.Queue.Submit(command);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(terminal.Settlements, Has.Count.EqualTo(3));
        Assert.That(terminal.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, 3)));
        BattleEnergySpentSettlement spent = terminal.Settlements
            .OfType<BattleEnergySpentSettlement>()
            .Single();
        BattleCardMovedSettlement selectedMove = terminal.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == selectedCardId);
        BattleCardMovedSettlement sourceMove = terminal.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == sourceCardId);
        Assert.That(spent.Order, Is.Zero);
        Assert.That(spent.Amount, Is.Zero);
        Assert.That(selectedMove.Order, Is.EqualTo(1));
        Assert.That(selectedMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(selectedMove.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(terminal.Settlements.OfType<BattleEnergyGainedSettlement>(), Is.Empty);
        Assert.That(sourceMove.Order, Is.EqualTo(2));
        Assert.That(sourceMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(sourceMove.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(5));
        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore + 1));
        Assert.That(scenario.Results[resultCountBefore].Succeeded, Is.True);
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
    }

    /// <summary>验证驻防经公共队列保留两张指定手牌，并让既有格挡连续跨过两次回合开始后才恢复清除。</summary>
    [Test]
    public void Garrison_WithTwoSelectedCards_RetainsSelectionAndPreservesBlockForTwoRoundStarts()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3246, 3201, 3202, 3203 },
            initialHandCount: 4,
            enemyDamage: 0,
            initialEnergy: 3);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3246);
        CardInstanceId firstSelectedCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3201);
        CardInstanceId secondSelectedCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3202);
        CardInstanceId unselectedCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3203);
        var selectedCardIds = new[] { firstSelectedCardId, secondSelectedCardId };
        var command = new PlayCardCommand(
            scenario.Player.Id,
            sourceCardId,
            targetId: null,
            selectedCardIds: selectedCardIds);
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Queue.Submit(command);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        BattleEnergySpentSettlement energy = terminal.Settlements
            .OfType<BattleEnergySpentSettlement>()
            .Single();
        BattleBlockGainedSettlement block = terminal.Settlements
            .OfType<BattleBlockGainedSettlement>()
            .Single();
        BattleStatusAppliedSettlement applied = terminal.Settlements
            .OfType<BattleStatusAppliedSettlement>()
            .Single(item => item.Status == BattleStatusType.Garrison);
        BattleCardMovedSettlement sourceMove = terminal.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == sourceCardId);
        Assert.That(energy.Amount, Is.EqualTo(2));
        Assert.That(energy.EnergyAfter, Is.EqualTo(1));
        Assert.That(block.Amount, Is.EqualTo(12));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(12));
        Assert.That(applied.ValueBefore, Is.Zero);
        Assert.That(applied.ValueAfter, Is.EqualTo(2));
        Assert.That(applied.Amount, Is.EqualTo(2));
        Assert.That(sourceMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(sourceMove.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));

        int resultCountBeforeFirstEnd = scenario.Results.Count;
        BattleCommandExecutionResult firstEnd = scenario.EndPlayerActionResult();
        BattleCardMovedSettlement[] firstEndDiscards = firstEnd.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Where(item => item.FromZone == BattleCardZone.Hand &&
                           item.ToZone == BattleCardZone.DiscardPile)
            .ToArray();
        BattleSettlementRecord[] firstRoundStartSettlements = scenario.Results
            .Skip(resultCountBeforeFirstEnd)
            .SelectMany(result => result.Settlements)
            .ToArray();
        BattleStatusReducedSettlement firstReduction = firstRoundStartSettlements
            .OfType<BattleStatusReducedSettlement>()
            .Single(item => item.Status == BattleStatusType.Garrison);
        Assert.That(firstEndDiscards.Select(item => item.CardId),
            Is.EqualTo(new[] { unselectedCardId }));
        Assert.That(firstEndDiscards.Select(item => item.CardId),
            Has.None.EqualTo(firstSelectedCardId));
        Assert.That(firstEndDiscards.Select(item => item.CardId),
            Has.None.EqualTo(secondSelectedCardId));
        Assert.That(scenario.Zones.Hand, Does.Contain(firstSelectedCardId));
        Assert.That(scenario.Zones.Hand, Does.Contain(secondSelectedCardId));
        Assert.That(firstReduction.ValueBefore, Is.EqualTo(2));
        Assert.That(firstReduction.ValueAfter, Is.EqualTo(1));
        Assert.That(firstRoundStartSettlements.OfType<BattleBlockClearedSettlement>()
            .Where(item => item.TargetId == scenario.Player.Id), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(12));

        int resultCountBeforeSecondEnd = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleSettlementRecord[] secondRoundStartSettlements = scenario.Results
            .Skip(resultCountBeforeSecondEnd)
            .SelectMany(result => result.Settlements)
            .ToArray();
        BattleStatusReducedSettlement secondReduction = secondRoundStartSettlements
            .OfType<BattleStatusReducedSettlement>()
            .Single(item => item.Status == BattleStatusType.Garrison);
        Assert.That(secondReduction.ValueBefore, Is.EqualTo(1));
        Assert.That(secondReduction.ValueAfter, Is.Zero);
        Assert.That(secondRoundStartSettlements.OfType<BattleBlockClearedSettlement>()
            .Where(item => item.TargetId == scenario.Player.Id), Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(3));
        Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(12));

        int resultCountBeforeThirdEnd = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleSettlementRecord[] thirdRoundStartSettlements = scenario.Results
            .Skip(resultCountBeforeThirdEnd)
            .SelectMany(result => result.Settlements)
            .ToArray();
        BattleBlockClearedSettlement cleared = thirdRoundStartSettlements
            .OfType<BattleBlockClearedSettlement>()
            .Single(item => item.TargetId == scenario.Player.Id);
        Assert.That(thirdRoundStartSettlements.OfType<BattleStatusReducedSettlement>()
            .Where(item => item.Status == BattleStatusType.Garrison), Is.Empty);
        Assert.That(cleared.BlockBefore, Is.EqualTo(12));
        Assert.That(cleared.BlockAfter, Is.Zero);
        Assert.That(scenario.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(4));
        Assert.That(scenario.Player.CurrentBlock, Is.Zero);
    }

    /// <summary>验证趁势追击在上一张为射击时，经同一 Queue continuation 随机选择唯一攻击手牌并免费打出。</summary>
    [Test]
    public void OpportunisticStrike_AfterShoot_QueuesRandomHandAttackAsFreeContinuation()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3201, 3243, 3202 },
            initialHandCount: 3,
            firstEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 3,
            initialAmmo: 1);
        scenario.StartBattle();
        scenario.Play(3201, scenario.FirstEnemy.Id);
        CardInstanceId opportunisticId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3243);
        CardInstanceId childAttackId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3202);
        int resultCountBefore = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Queue.Submit(
            new PlayCardCommand(scenario.Player.Id, opportunisticId, targetId: null));
        BattleCommandLifecycleEvent parentTerminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(parentTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore + 2));
        BattleCommandExecutionResult parent = scenario.Results[resultCountBefore];
        BattleCommandExecutionResult child = scenario.Results[resultCountBefore + 1];
        Assert.That(parent.Succeeded, Is.True);
        Assert.That(child.Succeeded, Is.True);
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(parent).Amount, Is.EqualTo(1));
        Assert.That(FindSettlement<BattleDamageAppliedSettlement>(parent).AttackValue,
            Is.EqualTo(6));
        Assert.That(FindSettlement<BattleCardMovedSettlement>(parent).CardId,
            Is.EqualTo(opportunisticId));
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(child).Amount, Is.Zero);
        Assert.That(FindSettlement<BattleDamageAppliedSettlement>(child).AttackValue,
            Is.EqualTo(6));
        BattleCardMovedSettlement childMove = FindSettlement<BattleCardMovedSettlement>(child);
        Assert.That(childMove.CardId, Is.EqualTo(childAttackId));
        Assert.That(childMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(childMove.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(2));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(82));
        Assert.That(scenario.Zones.Hand, Is.Empty);
    }

    /// <summary>验证不可阻挡在致死伤害的父表现完成后创建唯一合法临时攻击，再以零实际能量自动命中最近存活敌人并强制消耗。</summary>
    [Test]
    public void Unstoppable_AfterLethalDamage_CreatesAndQueuesFreeImplementedNonShootAttack()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3250, 3201 },
            initialHandCount: 2,
            firstEnemyHealth: 6,
            secondEnemyHealth: 20,
            enemyDamage: 0,
            initialEnergy: 1,
            initialAmmo: 1,
            isolateUnstoppableCandidate: true);
        scenario.StartBattle();
        cfg.battle.Card unstoppableTemplate = scenario.Tables.TbCard.Get(3250);
        cfg.battle.Card candidateTemplate = scenario.Tables.TbCard.Get(3202);

        Assert.That(unstoppableTemplate.Cost, Is.EqualTo(1));
        Assert.That(unstoppableTemplate.UpgradedCost, Is.EqualTo(1));
        Assert.That(unstoppableTemplate.HasUpgrade, Is.True);
        CardInstanceId unstoppableCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3250);
        int activationResultIndex = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder activationLifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult activationSubmission =
            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                unstoppableCardId,
                targetId: null));
        BattleCommandLifecycleEvent activationTerminal =
            activationLifecycle.RequireTerminal(activationSubmission);

        Assert.That(activationSubmission.Accepted, Is.True);
        Assert.That(activationTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        BattleCommandExecutionResult activation = scenario.Results[activationResultIndex];
        Assert.That(FindSettlement<BattleEnergySpentSettlement>(activation).Amount, Is.EqualTo(1));
        BattleCardMovedSettlement activationMove = FindSettlement<BattleCardMovedSettlement>(activation);
        Assert.That(activationMove.ToZone, Is.EqualTo(BattleCardZone.PowerPile));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);

        CardInstanceId shootCardId = scenario.Zones.Hand.Single();
        int resultCountBefore = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Queue.Submit(
            new PlayCardCommand(scenario.Player.Id, shootCardId, scenario.FirstEnemy.Id));
        BattleCommandLifecycleEvent parentTerminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(parentTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore + 3));
        BattleCommandExecutionResult parent = scenario.Results[resultCountBefore];
        BattleCommandExecutionResult creation = scenario.Results[resultCountBefore + 1];
        BattleCommandExecutionResult child = scenario.Results[resultCountBefore + 2];
        Assert.That(parent.CommandType, Is.EqualTo(BattleCommandType.PlayCard));
        Assert.That(creation.CommandType, Is.EqualTo(BattleCommandType.ResolveSettlementTriggers));
        Assert.That(child.CommandType, Is.EqualTo(BattleCommandType.PlayCard));
        Assert.That(creation.AuthoritySequence, Is.EqualTo(parent.AuthoritySequence + 1));
        Assert.That(child.AuthoritySequence, Is.EqualTo(creation.AuthoritySequence + 1));

        BattleDamageAppliedSettlement lethal = FindSettlement<BattleDamageAppliedSettlement>(parent);
        Assert.That(lethal.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(lethal.WasFatal, Is.True);
        Assert.That(lethal.BlockBefore, Is.Zero);
        Assert.That(lethal.BlockAfter, Is.Zero);
        Assert.That(parent.Settlements.OfType<BattleCardCreatedSettlement>(), Is.Empty);

        BattleCardCreatedSettlement created = FindSettlement<BattleCardCreatedSettlement>(creation);
        Assert.That(created.TemplateId, Is.EqualTo(3202));
        Assert.That(created.ToZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(candidateTemplate.CardType, Is.EqualTo(cfg.battle.CardType.Attack));
        Assert.That(candidateTemplate.ImplementationStatus,
            Is.EqualTo(cfg.battle.CardImplementationStatus.Implemented));
        Assert.That(candidateTemplate.Cost, Is.EqualTo(1));

        Assert.That(FindSettlement<BattleEnergySpentSettlement>(child).Amount, Is.Zero);
        BattleDamageAppliedSettlement automaticDamage =
            FindSettlement<BattleDamageAppliedSettlement>(child);
        Assert.That(automaticDamage.TargetId, Is.EqualTo(scenario.SecondEnemy.Id));
        BattleCardMovedSettlement childMove = child.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == created.CardId);
        Assert.That(childMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(childMove.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.Zero);
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(14));
        Assert.That(scenario.Zones.Hand, Is.Empty);
        Assert.That(scenario.Zones.ExhaustPile, Does.Contain(created.CardId));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
    }

    /// <summary>验证不可阻挡在一条非致死破挡结算后只创建并免费续接一次临时攻击，且自动牌保留名义费用并强制消耗。</summary>
    [Test]
    public void Unstoppable_AfterNonFatalBlockBreak_CreatesAndQueuesOneFreeAttack()
    {
        using var scenario = new MachineGunnerStarterScenario(
            new[] { 3250, 3201 },
            initialHandCount: 2,
            firstEnemyHealth: 20,
            secondEnemyHealth: 20,
            enemyDamage: 0,
            initialEnergy: 1,
            initialAmmo: 1,
            isolateUnstoppableCandidate: true);
        scenario.StartBattle();
        scenario.FirstEnemy.ApplyBlockGain(6);
        cfg.battle.Card candidateTemplate = scenario.Tables.TbCard.Get(3202);
        CardInstanceId unstoppableCardId = scenario.Zones.Hand
            .Single(cardId => scenario.Zones.Cards[cardId].TemplateId == 3250);

        BattleCommandSubmissionResult activationSubmission =
            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                unstoppableCardId,
                targetId: null));

        Assert.That(activationSubmission.Accepted, Is.True);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
        CardInstanceId shootCardId = scenario.Zones.Hand.Single();
        int resultCountBefore = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Queue.Submit(
            new PlayCardCommand(scenario.Player.Id, shootCardId, scenario.FirstEnemy.Id));
        BattleCommandLifecycleEvent parentTerminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(parentTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.None));
        BattleCommandExecutionResult[] derivedResults = scenario.Results
            .Skip(resultCountBefore)
            .ToArray();
        Assert.That(derivedResults, Has.Length.EqualTo(3));
        BattleCommandExecutionResult parent = derivedResults[0];
        BattleCommandExecutionResult creation = derivedResults[1];
        BattleCommandExecutionResult child = derivedResults[2];
        Assert.That(parent.CommandType, Is.EqualTo(BattleCommandType.PlayCard));
        Assert.That(creation.CommandType, Is.EqualTo(BattleCommandType.ResolveSettlementTriggers));
        Assert.That(child.CommandType, Is.EqualTo(BattleCommandType.PlayCard));
        Assert.That(creation.AuthoritySequence, Is.EqualTo(parent.AuthoritySequence + 1));
        Assert.That(child.AuthoritySequence, Is.EqualTo(parent.AuthoritySequence + 2));

        BattleDamageAppliedSettlement blockBreak =
            FindSettlement<BattleDamageAppliedSettlement>(parent);
        Assert.That(blockBreak.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That(blockBreak.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That((blockBreak.BlockBefore, blockBreak.BlockAfter), Is.EqualTo((6, 0)));
        Assert.That((blockBreak.HealthBefore, blockBreak.HealthAfter), Is.EqualTo((20, 20)));
        Assert.That(blockBreak.WasFatal, Is.False);
        Assert.That(parent.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, parent.Settlements.Count)));

        BattleCardCreatedSettlement[] createdAcrossChain = derivedResults
            .SelectMany(result => result.Settlements)
            .OfType<BattleCardCreatedSettlement>()
            .ToArray();
        Assert.That(createdAcrossChain, Has.Length.EqualTo(1));
        BattleCardCreatedSettlement created = createdAcrossChain[0];
        Assert.That(created.TemplateId, Is.EqualTo(3202));
        Assert.That(created.ToZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(creation.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, creation.Settlements.Count)));

        BattleEnergySpentSettlement childEnergy =
            FindSettlement<BattleEnergySpentSettlement>(child);
        BattleDamageAppliedSettlement childDamage =
            FindSettlement<BattleDamageAppliedSettlement>(child);
        BattleCardMovedSettlement childMove = child.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Single(item => item.CardId == created.CardId);
        Assert.That(candidateTemplate.Cost, Is.EqualTo(1));
        Assert.That(childEnergy.Amount, Is.Zero);
        Assert.That(childDamage.SourceId, Is.EqualTo(scenario.Player.Id));
        Assert.That(childDamage.TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(childMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(childMove.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(child.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, child.Settlements.Count)));
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(14));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(scenario.Zones.ExhaustPile, Does.Contain(created.CardId));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.Zero);
    }

    /// <summary>验证能力牌注册表只暴露已有真实职业私有规则的十七种持续效果。</summary>
    [Test]
    public void PowerProgramRegistry_ContainsImplementedPowerKinds()
    {
        var expected = new Dictionary<cfg.battle.MachineGunnerProgramId, MachineGunnerPowerKind>
        {
            [cfg.battle.MachineGunnerProgramId.Bombard] = MachineGunnerPowerKind.Bombard,
            [cfg.battle.MachineGunnerProgramId.SkyWrath] = MachineGunnerPowerKind.SkyWrath,
            [cfg.battle.MachineGunnerProgramId.CoreExpansion] = MachineGunnerPowerKind.CoreExpansion,
            [cfg.battle.MachineGunnerProgramId.OutputAdjust] = MachineGunnerPowerKind.OutputAdjust,
            [cfg.battle.MachineGunnerProgramId.BlastShield] = MachineGunnerPowerKind.BlastShield,
            [cfg.battle.MachineGunnerProgramId.MagExpansion] = MachineGunnerPowerKind.MagExpansion,
            [cfg.battle.MachineGunnerProgramId.SmokePersist] = MachineGunnerPowerKind.SmokePersist,
            [cfg.battle.MachineGunnerProgramId.PowerOverclock] = MachineGunnerPowerKind.PowerOverclock,
            [cfg.battle.MachineGunnerProgramId.KungfuMech] = MachineGunnerPowerKind.KungfuMech,
            [cfg.battle.MachineGunnerProgramId.IncendiaryAmmo] = MachineGunnerPowerKind.IncendiaryAmmo,
            [cfg.battle.MachineGunnerProgramId.AgedOil] = MachineGunnerPowerKind.AgedOil,
            [cfg.battle.MachineGunnerProgramId.BurningOil] = MachineGunnerPowerKind.BurningOil,
            [cfg.battle.MachineGunnerProgramId.GuerrillaTactics] = MachineGunnerPowerKind.GuerrillaTactics,
            [cfg.battle.MachineGunnerProgramId.ElectroBoost] = MachineGunnerPowerKind.ElectroBoost,
            [cfg.battle.MachineGunnerProgramId.PrivateMod] = MachineGunnerPowerKind.PrivateMod,
            [cfg.battle.MachineGunnerProgramId.PortableHelper] = MachineGunnerPowerKind.PortableHelper,
            [cfg.battle.MachineGunnerProgramId.Unstoppable] = MachineGunnerPowerKind.Unstoppable,
        };

        foreach (KeyValuePair<cfg.battle.MachineGunnerProgramId, MachineGunnerPowerKind> entry in expected)
        {
            Assert.That(MachineGunnerCardProgramRegistry.TryGet(entry.Key, out MachineGunnerCardProgram program), Is.True);
            Assert.That(program.PowerKind, Is.EqualTo(entry.Value));
        }
    }

    /// <summary>通过公共队列提交一条预期失败的出牌命令，并验证提交前全部权威事实保持不变。</summary>
    private static void AssertPlayCardFailureWithoutWrites(
        MachineGunnerStarterScenario scenario,
        PlayCardCommand command,
        BattleCommandExecutionFailureReason expectedFailure)
    {
        BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
        int energyBefore = turnBefore.Players[scenario.Player.Id].Energy;
        CardInstanceId[] drawPileBefore = scenario.Zones.DrawPile.ToArray();
        CardInstanceId[] handBefore = scenario.Zones.Hand.ToArray();
        CardInstanceId[] discardPileBefore = scenario.Zones.DiscardPile.ToArray();
        CardInstanceId[] exhaustPileBefore = scenario.Zones.ExhaustPile.ToArray();
        CardInstanceId[] powerPileBefore = scenario.Zones.PowerPile.ToArray();
        uint shuffleRandomBefore = scenario.Zones.ShuffleRandomState;
        uint cardRandomBefore = scenario.Session.MachineGunnerRuntime.CardRandomState;
        int resultCountBefore = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();

        BattleCommandSubmissionResult submission = scenario.Queue.Submit(command);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(expectedFailure));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
            Is.EqualTo(energyBefore));
        Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(scenario.Zones.DrawPile, Is.EqualTo(drawPileBefore));
        Assert.That(scenario.Zones.Hand, Is.EqualTo(handBefore));
        Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(discardPileBefore));
        Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(exhaustPileBefore));
        Assert.That(scenario.Zones.PowerPile, Is.EqualTo(powerPileBefore));
        Assert.That(scenario.Zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(scenario.Session.MachineGunnerRuntime.CardRandomState,
            Is.EqualTo(cardRandomBefore));
        Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
    }

    /// <summary>从一条命令结算链中读取唯一指定类型，避免测试重排权威记录。</summary>
    private static T FindSettlement<T>(BattleCommandExecutionResult result)
        where T : BattleSettlementRecord
    {
        T[] found = result.Settlements.OfType<T>().ToArray();
        Assert.That(found, Has.Length.EqualTo(1), $"Expected exactly one {typeof(T).Name}.");
        return found[0];
    }

    /// <summary>封装机枪兵 Hero 配置、场景运行时和直连唯一 Queue seam 的独立 EditMode 场景。</summary>
    internal sealed class MachineGunnerStarterScenario : IDisposable
    {
        /// <summary>构造本场景所用的同一份内存配置表，供测试从外部 seam 核对静态卡牌契约。</summary>
        internal Tables Tables { get; }

        /// <summary>由 Hero 模板构造出的单场权威运行时。</summary>
        internal BattleSession Session { get; }

        /// <summary>唯一玩家参与者。</summary>
        internal PlayerCombatantData Player { get; }

        /// <summary>Encounter 顺序中的第一名敌人。</summary>
        internal EnemyCombatantData FirstEnemy { get; }

        /// <summary>Encounter 顺序中的第二名敌人。</summary>
        internal EnemyCombatantData SecondEnemy { get; }

        /// <summary>仅在测试显式请求时存在的 Encounter 第三名敌人。</summary>
        internal EnemyCombatantData ThirdEnemy { get; }

        /// <summary>当前玩家唯一的权威卡区。</summary>
        internal BattleCardZonesData Zones => Session.CardZones;

        /// <summary>所有输入均经过其 Submit seam 的队列。</summary>
        internal BattleCommandQueue Queue { get; }

        /// <summary>只读暴露队列执行时使用的同一出牌规则实例，避免测试构造第二套规则事实。</summary>
        internal BattleCardPlayRules CardPlayRules => Queue.CardPlayRules;

        /// <summary>按表现层确认顺序公开已完成命令的结果，供回合续接测试核对跨命令结算。</summary>
        internal IReadOnlyList<BattleCommandExecutionResult> Results => _presentation.Results;

        private readonly RecordingImmediateBattleCommandPresentation _presentation;

        /// <summary>用指定牌组构造 Hero 1002 的完整最小配置和 Queue，避免修改共享测试工厂。</summary>
        internal MachineGunnerStarterScenario(
            int[] deckCards,
            int initialHandCount,
            int firstEnemyHealth = 20,
            int secondEnemyHealth = 20,
            int enemyDamage = 1,
            int initialEnergy = 3,
            int initialAmmo = 5,
            int ammoMaximum = 5,
            int? thirdEnemyHealth = null,
            int randomSeed = 1234,
            bool isolateUnstoppableCandidate = false)
        {
            Tables = CreateTables(
                deckCards,
                firstEnemyHealth,
                secondEnemyHealth,
                enemyDamage,
                initialEnergy,
                initialAmmo,
                ammoMaximum,
                thirdEnemyHealth,
                isolateUnstoppableCandidate);
            Session = BattleSession.FromConfig(
                Tables,
                new BattleSetupOptions(
                    heroTemplateId: 1002,
                    encounterTemplateId: 5001,
                    randomSeed: randomSeed));
            Player = FindCombatant<PlayerCombatantData>(Session.Combatants);
            FirstEnemy = (EnemyCombatantData)Session.Combatants.All[Session.EnemyCombatantIdsInEncounterOrder[0]];
            SecondEnemy = (EnemyCombatantData)Session.Combatants.All[Session.EnemyCombatantIdsInEncounterOrder[1]];
            ThirdEnemy = Session.EnemyCombatantIdsInEncounterOrder.Count > 2
                ? (EnemyCombatantData)Session.Combatants.All[Session.EnemyCombatantIdsInEncounterOrder[2]]
                : null;
            _presentation = new RecordingImmediateBattleCommandPresentation();
            var coordinator = new BattleCommandSubmissionCoordinator();
            Queue = new BattleCommandQueue(
                Session.Combatants,
                new Dictionary<CombatantId, BattleCardZonesData> { [Player.Id] = Session.CardZones },
                Session.EnemyCombatantIdsInEncounterOrder,
                Session.EnemyIntents,
                Tables,
                Session.PlayerResourceProfiles,
                initialHandCount,
                _presentation,
                coordinator,
                Session.MachineGunnerRuntime);
        }

        /// <summary>从未开始阶段经唯一 Queue 启动本场战斗。</summary>
        internal void StartBattle()
        {
            Assert.That(StartBattleResult().Succeeded, Is.True);
        }

        /// <summary>从唯一 Queue seam 启动战斗并返回本次权威结果，供起手顺序测试读取结算链。</summary>
        internal BattleCommandExecutionResult StartBattleResult()
        {
            int resultCountBefore = _presentation.Results.Count;
            BattleCommandSubmissionResult submission = Queue.Submit(
                new StartBattleCommand());
            Assert.That(submission.Accepted, Is.True);
            Assert.That(_presentation.Results.Count, Is.EqualTo(resultCountBefore + 1));
            return _presentation.Results[resultCountBefore];
        }

        /// <summary>提交一张当前手牌中的模板卡，并返回该次权威执行结果。</summary>
        internal BattleCommandExecutionResult Play(int templateId, CombatantId? targetId)
        {
            int resultCountBefore = _presentation.Results.Count;
            BattleCommandSubmissionResult submission = Submit(templateId, targetId);
            Assert.That(submission.Accepted, Is.True);
            Assert.That(_presentation.Results.Count, Is.EqualTo(resultCountBefore + 1));
            BattleCommandExecutionResult result = _presentation.Results[resultCountBefore];
            Assert.That(result.Succeeded, Is.True);
            return result;
        }

        /// <summary>提交当前手牌中的模板卡，供失败路径读取真实 lifecycle 终态。</summary>
        internal BattleCommandSubmissionResult Submit(int templateId, CombatantId? targetId)
        {
            CardInstanceId cardId = FindCardInHand(templateId);
            return Queue.Submit(new PlayCardCommand(Player.Id, cardId, targetId));
        }

        /// <summary>结束当前玩家行动并同步推进敌人及下一玩家回合。</summary>
        internal void EndPlayerAction()
        {
            EndPlayerActionResult();
        }

        /// <summary>结束当前玩家行动并返回该命令本身的第一条表现结果，使回归可在自动续接继续执行前冻结其权威 settlement。</summary>
        internal BattleCommandExecutionResult EndPlayerActionResult()
        {
            int resultCountBefore = _presentation.Results.Count;
            BattleCommandSubmissionResult submission = Queue.Submit(
                new EndPlayerActionCommand(Player.Id));
            Assert.That(submission.Accepted, Is.True);
            Assert.That(_presentation.Results.Count, Is.GreaterThan(resultCountBefore));
            return _presentation.Results[resultCountBefore];
        }

        /// <summary>先停止 Queue 的响应式资源，再释放其不拥有的 Session 聚合。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            Session.Dispose();
        }

        /// <summary>按运行时注册顺序取得唯一指定类型参与者。</summary>
        private static T FindCombatant<T>(BattleCombatantsData combatants)
            where T : CombatantData
        {
            T[] found = combatants.All.Values.OfType<T>().ToArray();
            Assert.That(found, Has.Length.EqualTo(1));
            return found[0];
        }

        /// <summary>在权威手牌中按静态模板定位一个可提交实例。</summary>
        private CardInstanceId FindCardInHand(int templateId)
        {
            foreach (CardInstanceId cardId in Zones.Hand)
            {
                if (Zones.Cards[cardId].TemplateId == templateId)
                    return cardId;
            }

            Assert.Fail($"Template {templateId} was not found in hand.");
            return default;
        }

        /// <summary>创建只含机枪兵初始牌、两名敌人和固定敌方意图的最小 Luban 表集。</summary>
        private static Tables CreateTables(
            int[] deckCards,
            int firstEnemyHealth,
            int secondEnemyHealth,
            int enemyDamage,
            int initialEnergy,
            int initialAmmo,
            int ammoMaximum,
            int? thirdEnemyHealth,
            bool isolateUnstoppableCandidate)
        {
            if (deckCards == null)
                throw new ArgumentNullException(nameof(deckCards));
            if (enemyDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(enemyDamage));
            if (firstEnemyHealth <= 0 || secondEnemyHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(firstEnemyHealth));
            if (thirdEnemyHealth.HasValue && thirdEnemyHealth.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(thirdEnemyHealth));
            if (initialEnergy < 0 || initialEnergy > 5)
                throw new ArgumentOutOfRangeException(nameof(initialEnergy));
            if (ammoMaximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(ammoMaximum));
            if (initialAmmo < 0 || initialAmmo > ammoMaximum)
                throw new ArgumentOutOfRangeException(nameof(initialAmmo));

            var cards = new JArray
            {
                CreateCard(3201, "TEST_MARINE_SHOOT", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.Shoot),
                CreateCard(3202, "TEST_MARINE_ELBOW", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.Elbow),
                CreateCard(3203, "TEST_MARINE_BLOCK", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Block),
                CreateCard(3204, "TEST_MARINE_RELOAD", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Reload),
                CreateCard(3205, "TEST_MARINE_STIM", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Stim),
                CreateCard(3206, "TEST_MARINE_CORE_EXPANSION", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.CoreExpansion, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3207, "TEST_MARINE_OUTPUT_ADJUST", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.OutputAdjust, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3208, "TEST_MARINE_BLAST_SHIELD", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.BlastShield, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3209, "TEST_MARINE_MAG_EXPANSION", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.MagExpansion, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3210, "TEST_MARINE_INCENDIARY_AMMO", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.IncendiaryAmmo, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3211, "TEST_MARINE_SMOKE_PERSIST", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.SmokePersist, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3212, "TEST_MARINE_KUNGFU_MECH", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.KungfuMech, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3213, "TEST_MARINE_OVERLOAD", 0, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Overload, cfg.battle.CardType.Skill),
                CreateCard(3214, "TEST_MARINE_TUMBLE_RELOAD", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.TumbleReload, cfg.battle.CardType.Skill),
                CreateCard(3216, "TEST_MARINE_RETREAT", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Retreat, cfg.battle.CardType.Skill),
                CreateCard(3217, "TEST_MARINE_GAS_PUMP", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.GasPump, cfg.battle.CardType.Skill),
                CreateCard(3218, "TEST_MARINE_NAPALM", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Napalm, cfg.battle.CardType.Skill),
                CreateCard(3219, "TEST_MARINE_MOLOTOV", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.Molotov, cfg.battle.CardType.Skill),
                CreateCard(3215, "TEST_MARINE_STUN_GRENADE", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.StunGrenade, cfg.battle.CardType.Skill),
                CreateCard(3220, "TEST_MARINE_HOLD_LINE", 0, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.HoldLine, cfg.battle.CardType.Skill, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.CardCostKind.X),
                CreateCard(3221, "TEST_MARINE_SMOKE_BOMB", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.SmokeBomb, cfg.battle.CardType.Skill),
                CreateCard(3222, "TEST_MARINE_INCOMPLETE_COMBUSTION", 3, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.IncompleteCombustion, cfg.battle.CardType.Skill, cfg.battle.CardPlayDestination.ExhaustPile),
                CreateCard(3223, "TEST_MARINE_KNOCKBACK_SHOT", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.KnockbackShot),
                CreateCard(3224, "TEST_MARINE_SPRAY", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.Spray),
                CreateCard(3225, "TEST_MARINE_BAYONET_PARRY", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.BayonetParry),
                CreateCard(3226, "TEST_MARINE_WILD_RAMPAGE", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.WildRampage, cfg.battle.CardType.Attack, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.CardCostKind.X),
                CreateCard(3227, "TEST_MARINE_QUICK_ELBOW", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.QuickElbow),
                CreateCard(3228, "TEST_MARINE_KIDNEY_SHOT", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.KidneyShot),
                CreateCard(3229, "TEST_MARINE_PAINFUL_ELBOW", 2, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.PainfulElbow),
                CreateCard(3230, "TEST_MARINE_HEAVY_ELBOW", 3, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.HeavyElbow),
                CreateCard(3231, "TEST_MARINE_FIELD_SURGERY", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.FieldSurgery, cfg.battle.CardType.Skill, cfg.battle.CardPlayDestination.ExhaustPile),
                CreateCard(3232, "TEST_MARINE_HURRICANE_ELBOW", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.HurricaneElbow, cfg.battle.CardType.Attack, cfg.battle.CardPlayDestination.DiscardPile, cfg.battle.CardCostKind.X),
                CreateCard(3233, "TEST_MARINE_PRECISION_SHOT", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.PrecisionShot),
                CreateCard(3234, "TEST_MARINE_TACTICAL_ADVANCE", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.TacticalAdvance, cfg.battle.CardType.Skill),
                CreateCard(3235, "TEST_MARINE_QUICK_ROLL", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.QuickRoll, cfg.battle.CardType.Skill),
                CreateCard(3236, "TEST_MARINE_ELECTRO_BOOST", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.ElectroBoost, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3237, "TEST_MARINE_GUIDED_NUKE", 5, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.GuidedNuke, cfg.battle.CardType.Skill),
                CreateCard(3238, "TEST_MARINE_BANSHEE_STRIKE", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.BansheeStrike, cfg.battle.CardType.Skill),
                CreateCard(3239, "TEST_MARINE_FIRE_SUPPORT", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.FireSupport, cfg.battle.CardType.Skill),
                CreateCard(3240, "TEST_MARINE_FIRE_BOMBARDMENT", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.FireBombardment, cfg.battle.CardType.Skill),
                CreateCard(3241, "TEST_MARINE_FIVE_HUNDRED_POUNDER", 3, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.FiveHundredPounder, cfg.battle.CardType.Skill),
                CreateCard(3242, "TEST_MARINE_COMBO_ELBOW", 2, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.ComboElbow),
                CreateCard(3243, "TEST_MARINE_OPPORTUNISTIC_STRIKE", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.OpportunisticStrike),
                CreateCard(3244, "TEST_MARINE_VENT_HEAT", 0, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.VentHeat, cfg.battle.CardType.Skill),
                CreateCard(3245, "TEST_MARINE_POWER_OVERCLOCK", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.PowerOverclock, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3246, "TEST_MARINE_GARRISON", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Garrison, cfg.battle.CardType.Skill),
                CreateCard(3247, "TEST_MARINE_SNIPER_SHOT", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.SniperShot),
                CreateCard(3248, "TEST_MARINE_SPIKE_SHOT", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.SpikeShot),
                CreateCard(3249, "TEST_MARINE_OPTICAL_CAMO", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.OpticalCamo, cfg.battle.CardType.Skill),
                CreateCard(3250, "TEST_MARINE_UNSTOPPABLE", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Unstoppable, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3251, "TEST_MARINE_GUERRILLA_TACTICS", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.GuerrillaTactics, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3259, "TEST_MARINE_HOLO_DECOY", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.HoloDecoy, cfg.battle.CardType.Skill, cfg.battle.CardPlayDestination.ExhaustPile),
                CreateCard(3260, "TEST_MARINE_LIMIT_OVERLOAD", 0, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.LimitOverload, cfg.battle.CardType.Skill),
                CreateCard(3261, "TEST_MARINE_MACHINEGUN", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Machinegun, cfg.battle.CardType.Skill, cfg.battle.CardPlayDestination.ExhaustPile),
                CreateCard(3262, "TEST_MARINE_DEFENSE_TARGET", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.DefenseTarget, cfg.battle.CardType.Skill, cfg.battle.CardPlayDestination.ExhaustPile),
                CreateCard(3263, "TEST_MARINE_MACHINEGUN_BURST", 0, cfg.battle.TargetRule.RandomEnemy, cfg.battle.MachineGunnerProgramId.MachinegunBurst, cfg.battle.CardType.Attack, cfg.battle.CardPlayDestination.ExhaustPile, hasUpgrade: false),
                CreateCard(3264, "TEST_MARINE_TRIPLE_STRIKE", 4, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.TripleStrike, cfg.battle.CardType.Attack, cfg.battle.CardPlayDestination.ExhaustPile),
                CreateCard(3265, "TEST_MARINE_BOMBARD", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.Bombard, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3266, "TEST_MARINE_SKY_WRATH", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.SkyWrath, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3267, "TEST_MARINE_PORTABLE_HELPER", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.PortableHelper, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3268, "TEST_MARINE_PRIVATE_MOD", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.PrivateMod, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3269, "TEST_MARINE_CHAIN_SMOKE", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.ChainSmoke, cfg.battle.CardType.Skill),
                CreateCard(3270, "TEST_MARINE_SECONDHAND_SMOKE", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.SecondhandSmoke, cfg.battle.CardType.Skill),
                CreateCard(3271, "TEST_MARINE_DEFENSIVE_STANCE", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.DefensiveStance, cfg.battle.CardType.Skill),
                CreateCard(3272, "TEST_MARINE_EMERGENCY_COOLING", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.EmergencyCooling, cfg.battle.CardType.Skill),
                CreateCard(3273, "TEST_MARINE_THERMITE_BOMB", 1, cfg.battle.TargetRule.AllEnemies, cfg.battle.MachineGunnerProgramId.ThermiteBomb, cfg.battle.CardType.Skill),
                CreateCard(3274, "TEST_MARINE_NEEDLE_STORM", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.NeedleStorm, cfg.battle.CardType.Skill),
                CreateCard(3275, "TEST_MARINE_STEALTH_ACTION", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.StealthAction, cfg.battle.CardType.Skill, isInnate: true),
                CreateCard(3276, "TEST_MARINE_FOEHN_WIND", 2, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.FoehnWind, cfg.battle.CardType.Skill),
                CreateCard(3277, "TEST_MARINE_PREEMPTIVE_STRIKE", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.PreemptiveStrike),
                CreateCard(3278, "TEST_MARINE_BULLY", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.Bully),
                CreateCard(3279, "TEST_MARINE_PRISMATIC_SHOT", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.PrismaticShot),
                CreateCard(3280, "TEST_MARINE_MARK", 0, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.Mark, cfg.battle.CardType.Attack),
                CreateCard(3252, "TEST_MARINE_EXPLOSIVE_ELBOW", 2, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.ExplosiveElbow),
                CreateCard(3253, "TEST_MARINE_AGED_OIL", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.AgedOil, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3254, "TEST_MARINE_BURNING_OIL", 2, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.BurningOil, cfg.battle.CardType.Power, cfg.battle.CardPlayDestination.Power),
                CreateCard(3255, "TEST_MARINE_FLAME_ELBOW", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.FlameElbow),
                CreateCard(3256, "TEST_MARINE_SIX_HITS", 2, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.SixHits),
                CreateCard(3257, "TEST_MARINE_TWELVE_HITS", 3, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.TwelveHits),
                CreateCard(3258, "TEST_MARINE_QUICK_MANEUVER", 1, cfg.battle.TargetRule.Self, cfg.battle.MachineGunnerProgramId.QuickManeuver, cfg.battle.CardType.Skill),
                CreateCard(3281, "TEST_MARINE_CRUSH", 1, cfg.battle.TargetRule.Enemy, cfg.battle.MachineGunnerProgramId.Crush),
                CreateCard(3282, "TEST_MARINE_CHARGED_BURST", 2, cfg.battle.TargetRule.AllEnemies, cfg.battle.MachineGunnerProgramId.ChargedBurst),
            };
            if (isolateUnstoppableCandidate)
            {
                foreach (JObject card in cards.OfType<JObject>())
                {
                    int cardId = card.Value<int>("id");
                    if (cardId == 3263)
                    {
                        card["card_type"] = (int)cfg.battle.CardType.Skill;
                        continue;
                    }

                    bool isAttack = card.Value<int>("card_type") ==
                        (int)cfg.battle.CardType.Attack;
                    if (isAttack && cardId != 3201 && cardId != 3202)
                    {
                        card["implementation_status"] =
                            (int)cfg.battle.CardImplementationStatus.CatalogOnly;
                    }
                }
            }
            var enemies = new JArray
            {
                CreateEnemy(2001, firstEnemyHealth, 6001),
                CreateEnemy(2002, secondEnemyHealth, 6001),
            };
            var encounterEnemyIds = new JArray(2001, 2002);
            if (thirdEnemyHealth.HasValue)
            {
                enemies.Add(CreateEnemy(2003, thirdEnemyHealth.Value, 6001));
                encounterEnemyIds.Add(2003);
            }

            var data = new Dictionary<string, JArray>
            {
                ["battle_tbhero"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = 1002,
                        ["name_i18n_key"] = "battle.hero.machine_gunner.name",
                        ["view_prefab_key"] = "pfb_char_player",
                        ["max_health"] = 70,
                        ["base_strength"] = 0,
                        ["initial_deck_id"] = 1002,
                        ["initial_energy"] = initialEnergy,
                        ["max_energy"] = 5,
                        ["energy_gain_per_round"] = 3,
                        ["initial_ammo"] = initialAmmo,
                        ["max_ammo"] = ammoMaximum,
                        ["ammo_gain_per_round"] = 1,
                        ["runtime_profile"] = (int)cfg.battle.HeroRuntimeProfile.MachineGunner,
                    },
                },
                ["battle_tbenemy"] = enemies,
                ["battle_tbdeck"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = 1002,
                        ["card_template_ids"] = new JArray(deckCards),
                    },
                },
                ["battle_tbcard"] = cards,
                ["battle_tbcardeffect"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = 4001,
                        ["effect_type"] = (int)cfg.battle.EffectType.DealDamage,
                        ["attribute"] = (int)cfg.battle.Attribute.None,
                        ["value"] = enemyDamage,
                    },
                },
                ["battle_tbencounter"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = 5001,
                        ["enemy_template_ids"] = encounterEnemyIds,
                    },
                },
                ["battle_tbenemybehaviorgroup"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = 6001,
                        ["behavior_ids"] = new JArray(7001),
                    },
                },
                ["battle_tbenemybehavior"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = 7001,
                        ["intent_type"] = (int)cfg.battle.EnemyIntentType.Attack,
                        ["target_rule"] = (int)cfg.battle.TargetRule.Enemy,
                        ["effect_id"] = 4001,
                        ["weight"] = 1,
                        ["cooldown_selections"] = 0,
                        ["max_consecutive"] = 0,
                    },
                },
            };
            return new Tables(tableName => data[tableName]);
        }

        /// <summary>创建满足生成 Card 全部字段的机枪兵程序卡记录。</summary>
        private static JObject CreateCard(
            int id,
            string externalKey,
            int cost,
            cfg.battle.TargetRule targetRule,
            cfg.battle.MachineGunnerProgramId programId,
            cfg.battle.CardType cardType = cfg.battle.CardType.Attack,
            cfg.battle.CardPlayDestination playDestination = cfg.battle.CardPlayDestination.DiscardPile,
            cfg.battle.CardCostKind costKind = cfg.battle.CardCostKind.Fixed,
            bool isInnate = false,
            bool hasUpgrade = true)
        {
            return new JObject
            {
                ["id"] = id,
                ["external_key"] = externalKey,
                ["catalog_snapshot_key"] = "test-machine-gunner-starter",
                ["name_i18n_key"] = $"battle.card.{externalKey}.name",
                ["description_i18n_key"] = $"battle.card.{externalKey}.description",
                ["upgraded_description_i18n_key"] = $"battle.card.{externalKey}.upgrade_description",
                ["card_type"] = (int)cardType,
                ["rarity"] = (int)cfg.battle.CardRarity.Basic,
                ["cost"] = cost,
                ["cost_kind"] = (int)costKind,
                ["upgraded_cost"] = cost,
                ["target_rule"] = (int)targetRule,
                ["play_destination"] = (int)playDestination,
                ["upgraded_play_destination"] = (int)playDestination,
                ["has_upgrade"] = hasUpgrade,
                ["implementation_status"] = (int)cfg.battle.CardImplementationStatus.Implemented,
                ["effect_bindings"] = new JArray(),
                ["illustration_key"] = "art_placeholder",
                ["program_id"] = (int)programId,
                ["is_innate"] = isInnate,
            };
        }

        /// <summary>创建固定基础数值与行为组的敌人记录。</summary>
        private static JObject CreateEnemy(int id, int maxHealth, int behaviorGroupId)
        {
            return new JObject
            {
                ["id"] = id,
                ["name_i18n_key"] = $"battle.enemy.test_{id}.name",
                ["view_prefab_key"] = "pfb_char_enemy",
                ["max_health"] = maxHealth,
                ["base_strength"] = 0,
                ["behavior_group_id"] = behaviorGroupId,
            };
        }
    }

    /// <summary>记录并同步完成表现屏障，使每条测试命令在 Submit 返回前推进其完整后继链。</summary>
    private sealed class RecordingImmediateBattleCommandPresentation : IBattleCommandPresentation
    {
        /// <summary>按收到顺序保存已进入表现层的权威结果。</summary>
        internal List<BattleCommandExecutionResult> Results { get; } =
            new List<BattleCommandExecutionResult>();

        /// <summary>记录结果后立即确认完成，不制造第二条测试专用命令入口。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            Results.Add(result);
            onCompleted.Invoke();
        }
    }
}
