using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Core;

/// <summary>通过唯一命令队列验证机枪兵延迟实例的创建、触发、进度与移除。</summary>
public sealed class MachineGunnerScheduledEffectsRuntimeTests
{
    /// <summary>确认 500 磅包含施放回合的两回合倒计时，并在第二个未来回合末先于燃烧命中全体敌人。</summary>
    [Test]
    public void FiveHundredPounder_CountsCastRoundAndExplodesBeforeBurnOnSecondFutureRoundEnd()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3241 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 3);
        scenario.StartBattle();

        BattleCommandExecutionResult play = scenario.Play(3241, targetId: null);
        MachineGunnerScheduledEffectChangedSettlement created = play.Settlements
            .OfType<MachineGunnerScheduledEffectChangedSettlement>()
            .Single();
        Assert.That(created.Kind, Is.EqualTo(MachineGunnerScheduledEffectKind.FiveHundredPounder));
        Assert.That(created.ChangeKind, Is.EqualTo(MachineGunnerScheduledEffectChangeKind.Created));
        Assert.That(created.RemainingBefore, Is.Zero);
        Assert.That(created.RemainingAfter, Is.EqualTo(3));
        Assert.That(play.Settlements.OfType<BattleCardMovedSettlement>().Single().ToZone,
            Is.EqualTo(BattleCardZone.DiscardPile));

        BattleCommandExecutionResult castRoundEnd = scenario.EndPlayerActionResult();
        AssertScheduledProgress(castRoundEnd, MachineGunnerScheduledEffectKind.FiveHundredPounder, 3, 2);
        BattleCommandExecutionResult firstFutureRoundEnd = scenario.EndPlayerActionResult();
        AssertScheduledProgress(firstFutureRoundEnd, MachineGunnerScheduledEffectKind.FiveHundredPounder, 2, 1);

        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.FirstEnemy.Id,
            MachineGunnerCombatantStatus.Burn,
            5);
        BattleCommandExecutionResult explosion = scenario.EndPlayerActionResult();
        BattleDamageAppliedSettlement[] playerDamages = explosion.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Where(item => item.SourceId == scenario.Player.Id)
            .ToArray();
        Assert.That(playerDamages.Select(item => item.TargetId), Is.EqualTo(new CombatantId?[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(playerDamages.Select(item => item.AttackValue), Is.EqualTo(new[] { 60, 60 }));
        BattleDamageAppliedSettlement burnDamage = explosion.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Single(item => item.SourceId == scenario.FirstEnemy.Id &&
                            item.TargetId == scenario.FirstEnemy.Id);
        Assert.That(burnDamage.AttackValue, Is.EqualTo(5));

        MachineGunnerScheduledEffectChangedSettlement[] lifecycle = explosion.Settlements
            .OfType<MachineGunnerScheduledEffectChangedSettlement>()
            .Where(item => item.Kind == MachineGunnerScheduledEffectKind.FiveHundredPounder)
            .ToArray();
        Assert.That(lifecycle.Select(item => item.ChangeKind), Is.EqualTo(new[]
        {
            MachineGunnerScheduledEffectChangeKind.Triggered,
            MachineGunnerScheduledEffectChangeKind.Countdown,
            MachineGunnerScheduledEffectChangeKind.Removed,
        }));
        Assert.That(lifecycle[0].Order, Is.LessThan(playerDamages[0].Order));
        Assert.That(lifecycle[2].Order, Is.LessThan(burnDamage.Order));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(explosion);
    }

    /// <summary>确认火力支援在下回合 Smoke 清空前逐段随机当前存活敌人，触发一次后移除。</summary>
    [Test]
    public void FireSupport_TriggersFiveRandomSupportHitsBeforeRoundStartSmokeClearAndRemoves()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3239 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 1);
        scenario.StartBattle();
        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.FirstEnemy.Id,
            MachineGunnerCombatantStatus.Smoke,
            1);
        scenario.Session.MachineGunnerRuntime.CombatState.Add(
            scenario.SecondEnemy.Id,
            MachineGunnerCombatantStatus.Smoke,
            1);

        BattleCommandExecutionResult play = scenario.Play(3239, targetId: null);
        MachineGunnerScheduledEffectChangedSettlement created = play.Settlements
            .OfType<MachineGunnerScheduledEffectChangedSettlement>()
            .Single();
        Assert.That(created.Kind, Is.EqualTo(MachineGunnerScheduledEffectKind.FireSupport));
        Assert.That(created.RemainingAfter, Is.EqualTo(1));

        int resultCountBeforeEnd = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult[] roundResults = scenario.Results
            .Skip(resultCountBeforeEnd)
            .ToArray();
        BattleCommandExecutionResult triggerResult = roundResults.Single(result =>
            result.Settlements.OfType<MachineGunnerScheduledEffectChangedSettlement>()
                .Any(item => item.Kind == MachineGunnerScheduledEffectKind.FireSupport &&
                             item.ChangeKind == MachineGunnerScheduledEffectChangeKind.Triggered));
        BattleDamageAppliedSettlement[] supportHits = triggerResult.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Where(item => item.SourceId == scenario.Player.Id)
            .ToArray();
        Assert.That(supportHits, Has.Length.EqualTo(5));
        Assert.That(supportHits.Select(item => item.AttackValue), Is.All.EqualTo(1));
        Assert.That(supportHits.Select(item => item.TargetId), Is.All.Matches<CombatantId?>(targetId =>
            targetId == scenario.FirstEnemy.Id || targetId == scenario.SecondEnemy.Id));

        MachineGunnerScheduledEffectChangedSettlement[] lifecycle = triggerResult.Settlements
            .OfType<MachineGunnerScheduledEffectChangedSettlement>()
            .Where(item => item.Kind == MachineGunnerScheduledEffectKind.FireSupport)
            .ToArray();
        Assert.That(lifecycle.Select(item => item.ChangeKind), Is.EqualTo(new[]
        {
            MachineGunnerScheduledEffectChangeKind.Triggered,
            MachineGunnerScheduledEffectChangeKind.Countdown,
            MachineGunnerScheduledEffectChangeKind.Removed,
        }));
        Assert.That(lifecycle[0].Order, Is.LessThan(supportHits[0].Order));
        Assert.That(lifecycle[1].Order, Is.GreaterThan(supportHits[4].Order));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.FirstEnemy.Id,
            MachineGunnerCombatantStatus.Smoke), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.SecondEnemy.Id,
            MachineGunnerCombatantStatus.Smoke), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(triggerResult);
    }

    /// <summary>确认女妖持续两个玩家回合开始，每架每轮锁定一个最近目标且首段击杀后不在同次触发内递补。</summary>
    [Test]
    public void BansheeStrike_TriggersForTwoRoundStartsAndLocksNearestPerActivation()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3238 },
            initialHandCount: 1,
            firstEnemyHealth: 8,
            secondEnemyHealth: 40,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        scenario.Play(3238, targetId: null);

        int firstRoundResultIndex = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult firstTrigger = FindScheduledTriggerResult(
            scenario.Results.Skip(firstRoundResultIndex),
            MachineGunnerScheduledEffectKind.BansheeStrike);
        BattleDamageAppliedSettlement[] firstHits = GetPlayerDamageSettlements(firstTrigger, scenario.Player.Id);
        Assert.That(firstHits, Has.Length.EqualTo(1));
        Assert.That(firstHits[0].TargetId, Is.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(firstHits[0].AttackValue, Is.EqualTo(8));
        Assert.That(firstHits[0].WasFatal, Is.True);
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(40));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.EqualTo(1));

        int secondRoundResultIndex = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult secondTrigger = FindScheduledTriggerResult(
            scenario.Results.Skip(secondRoundResultIndex),
            MachineGunnerScheduledEffectKind.BansheeStrike);
        BattleDamageAppliedSettlement[] secondHits = GetPlayerDamageSettlements(secondTrigger, scenario.Player.Id);
        Assert.That(secondHits, Has.Length.EqualTo(2));
        Assert.That(secondHits.Select(item => item.TargetId), Is.All.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(secondHits.Select(item => item.AttackValue), Is.All.EqualTo(8));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(24));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(firstTrigger);
        AssertOrdersAreContinuous(secondTrigger);
    }

    /// <summary>确认燃烧轰炸两波逐目标执行支援伤害、读取旧浸油施加燃烧、再增加新浸油。</summary>
    [Test]
    public void FireBombardment_ResolvesDamageBurnOilPerTargetAcrossTwoWaves()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3240 },
            initialHandCount: 1,
            firstEnemyHealth: 40,
            secondEnemyHealth: 40,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        MachineGunnerCombatState state = scenario.Session.MachineGunnerRuntime.CombatState;
        state.Add(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil, 2);
        scenario.Play(3240, targetId: null);

        int resultIndex = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
            scenario.Results.Skip(resultIndex),
            MachineGunnerScheduledEffectKind.FireBombardment);
        BattleDamageAppliedSettlement[] hits = GetPlayerDamageSettlements(trigger, scenario.Player.Id);
        Assert.That(hits.Select(item => item.TargetId), Is.EqualTo(new CombatantId?[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(hits.Select(item => item.AttackValue), Is.All.EqualTo(2));

        MachineGunnerPrivateStatusChangedSettlement[] statusSettlements = trigger.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .ToArray();
        Assert.That(statusSettlements.Select(item => item.Status), Is.EqualTo(new[]
        {
            MachineGunnerCombatantStatus.Burn,
            MachineGunnerCombatantStatus.Oil,
            MachineGunnerCombatantStatus.Oil,
            MachineGunnerCombatantStatus.Burn,
            MachineGunnerCombatantStatus.Oil,
            MachineGunnerCombatantStatus.Burn,
            MachineGunnerCombatantStatus.Oil,
            MachineGunnerCombatantStatus.Oil,
            MachineGunnerCombatantStatus.Burn,
            MachineGunnerCombatantStatus.Oil,
            MachineGunnerCombatantStatus.Oil,
        }));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(14));
        Assert.That(state.Get(scenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(5));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Burn), Is.EqualTo(11));
        Assert.That(state.Get(scenario.SecondEnemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(4));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(trigger);
    }

    /// <summary>确认三连击在同一事务先加隐身再执行两段狙击，消耗卡牌并于下回合命中最远存活敌人。</summary>
    [Test]
    public void TripleStrike_GainsInvisibleBeforeTwoSniperHitsThenSupportsFurthestNextRound()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3264 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 4,
            initialAmmo: 3);
        scenario.StartBattle();

        BattleCommandExecutionResult play = scenario.Play(3264, scenario.FirstEnemy.Id);
        BattleDamageAppliedSettlement[] immediateHits = GetPlayerDamageSettlements(play, scenario.Player.Id);
        Assert.That(immediateHits, Has.Length.EqualTo(2));
        Assert.That(immediateHits.Select(item => item.TargetId), Is.All.EqualTo(scenario.FirstEnemy.Id));
        Assert.That(immediateHits.Select(item => item.AttackValue), Is.All.EqualTo(24));
        MachineGunnerPrivateStatusChangedSettlement invisible = play.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Single(item => item.Status == MachineGunnerCombatantStatus.Invisible);
        Assert.That(invisible.Order, Is.LessThan(immediateHits[0].Order));
        Assert.That(play.Settlements.OfType<BattleCardMovedSettlement>().Single().ToZone,
            Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Invisible), Is.EqualTo(2));

        int resultIndex = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
            scenario.Results.Skip(resultIndex),
            MachineGunnerScheduledEffectKind.TripleStrike);
        BattleDamageAppliedSettlement delayed = GetPlayerDamageSettlements(trigger, scenario.Player.Id).Single();
        Assert.That(delayed.TargetId, Is.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(delayed.AttackValue, Is.EqualTo(20));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(play);
        AssertOrdersAreContinuous(trigger);
    }

    /// <summary>确认钢针风暴逐段随机存活敌人，每次延迟伤害后再加一层破甲且不反哺后续针伤。</summary>
    [Test]
    public void NeedleStorm_DealsFourDelayedHitsThenAppliesArmorBreakPerLivingHit()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3274 },
            initialHandCount: 1,
            firstEnemyHealth: 40,
            secondEnemyHealth: 40,
            enemyDamage: 0,
            initialEnergy: 1);
        scenario.StartBattle();
        scenario.Play(3274, targetId: null);

        int resultIndex = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
            scenario.Results.Skip(resultIndex),
            MachineGunnerScheduledEffectKind.NeedleStorm);
        BattleDamageAppliedSettlement[] hits = GetPlayerDamageSettlements(trigger, scenario.Player.Id);
        MachineGunnerPrivateStatusChangedSettlement[] armorBreaks = trigger.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.ArmorBreak)
            .ToArray();
        Assert.That(hits, Has.Length.EqualTo(4));
        Assert.That(hits.Select(item => item.AttackValue), Is.All.EqualTo(1));
        Assert.That(armorBreaks, Has.Length.EqualTo(4));
        for (int index = 0; index < hits.Length; index++)
        {
            Assert.That(armorBreaks[index].TargetId, Is.EqualTo(hits[index].TargetId));
            Assert.That(armorBreaks[index].Order, Is.EqualTo(hits[index].Order + 1));
        }
        Assert.That(
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.FirstEnemy.Id,
                MachineGunnerCombatantStatus.ArmorBreak) +
            scenario.Session.MachineGunnerRuntime.CombatState.Get(
                scenario.SecondEnemy.Id,
                MachineGunnerCombatantStatus.ArmorBreak),
            Is.EqualTo(4));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(trigger);
    }

    /// <summary>确认引导核弹立即施加束缚，束缚只拒绝攻击并在行动结束清除，第三个未来回合末引爆。</summary>
    [Test]
    public void GuidedNuke_ShacklesCurrentTurnAndExplodesOnThirdFutureRoundEnd()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3237, 3201, 3220 },
            initialHandCount: 3,
            firstEnemyHealth: 200,
            secondEnemyHealth: 200,
            enemyDamage: 0,
            initialEnergy: 5,
            initialAmmo: 2);
        scenario.StartBattle();
        scenario.Play(3237, targetId: null);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Shackle), Is.EqualTo(1));

        int resultCountBeforeAttack = scenario.Results.Count;
        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult blockedAttack = scenario.Submit(3201, scenario.FirstEnemy.Id);
        Assert.That(blockedAttack.Accepted, Is.True);
        BattleCommandLifecycleEvent attackTerminal = lifecycle.RequireTerminal(blockedAttack);
        Assert.That(attackTerminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.AttackBlockedByShackle));
        Assert.That(attackTerminal.Settlements, Is.Empty);
        Assert.That(scenario.Results.Count, Is.EqualTo(resultCountBeforeAttack));

        BattleCommandExecutionResult skill = scenario.Play(3220, targetId: null);
        Assert.That(skill.Succeeded, Is.True);
        BattleCommandExecutionResult castRoundEnd = scenario.EndPlayerActionResult();
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Shackle), Is.Zero);
        AssertScheduledProgress(castRoundEnd, MachineGunnerScheduledEffectKind.GuidedNuke, 4, 3);
        BattleCommandExecutionResult firstFuture = scenario.EndPlayerActionResult();
        AssertScheduledProgress(firstFuture, MachineGunnerScheduledEffectKind.GuidedNuke, 3, 2);
        BattleCommandExecutionResult secondFuture = scenario.EndPlayerActionResult();
        AssertScheduledProgress(secondFuture, MachineGunnerScheduledEffectKind.GuidedNuke, 2, 1);
        BattleCommandExecutionResult thirdFuture = scenario.EndPlayerActionResult();
        BattleDamageAppliedSettlement[] explosion = GetPlayerDamageSettlements(thirdFuture, scenario.Player.Id);
        Assert.That(explosion.Select(item => item.TargetId), Is.EqualTo(new CombatantId?[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(explosion.Select(item => item.AttackValue), Is.All.EqualTo(99));
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(thirdFuture);
    }

    /// <summary>确认三连击弹药不足时不会写入隐身、定时实例、资源、生命或卡区。</summary>
    [Test]
    public void TripleStrike_InsufficientAmmoFailsWithoutImmediateOrScheduledWrites()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3264 },
            initialHandCount: 1,
            firstEnemyHealth: 100,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 4,
            initialAmmo: 2);
        scenario.StartBattle();

        using BattleCommandLifecycleExecutionRecorder lifecycle =
            scenario.Queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = scenario.Submit(3264, scenario.FirstEnemy.Id);
        Assert.That(submission.Accepted, Is.True);
        BattleCommandLifecycleEvent terminal = lifecycle.RequireTerminal(submission);

        Assert.That(terminal.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientAmmo));
        Assert.That(terminal.Settlements, Is.Empty);
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(4));
        Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Ammo, Is.EqualTo(2));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.Invisible), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        Assert.That(scenario.FirstEnemy.CurrentHealth, Is.EqualTo(100));
        Assert.That(scenario.SecondEnemy.CurrentHealth, Is.EqualTo(100));
        Assert.That(scenario.Zones.Hand, Has.Count.EqualTo(1));
        Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        Assert.That(scenario.Zones.ExhaustPile, Is.Empty);
    }

    /// <summary>验证已先创建的火力支援在触发时读取当前轰炸层数，并以正数半入规则换算四层与八层。</summary>
    [Test]
    public void Bombard_FireSupportReadsCurrentStacksAtTriggerAndRoundsLinearly()
    {
        using (var fourStackScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3239, 3265 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2))
        {
            fourStackScenario.StartBattle();
            fourStackScenario.Play(3239, targetId: null);
            Assert.That(fourStackScenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.EqualTo(1));
            fourStackScenario.Play(3265, targetId: null);

            int resultIndex = fourStackScenario.Results.Count;
            fourStackScenario.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                fourStackScenario.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.FireSupport);
            BattleDamageAppliedSettlement[] hits = GetPlayerDamageSettlements(
                trigger,
                fourStackScenario.Player.Id);

            Assert.That(hits, Has.Length.EqualTo(5));
            Assert.That(hits.Select(item => item.AttackValue), Is.All.EqualTo(3));
            AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.FireSupport);
            AssertOrdersAreContinuous(trigger);
        }

        using (var eightStackScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3239, 3265, 3265 },
                   initialHandCount: 3,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 3))
        {
            eightStackScenario.StartBattle();
            eightStackScenario.Play(3239, targetId: null);
            eightStackScenario.Play(3265, targetId: null);
            eightStackScenario.Play(3265, targetId: null);

            int resultIndex = eightStackScenario.Results.Count;
            eightStackScenario.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                eightStackScenario.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.FireSupport);

            Assert.That(
                GetPlayerDamageSettlements(trigger, eightStackScenario.Player.Id)
                    .Select(item => item.AttackValue),
                Is.All.EqualTo(4));
            AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.FireSupport);
            AssertOrdersAreContinuous(trigger);
        }
    }

    /// <summary>验证四层轰炸分别放大女妖、燃烧轰炸的伤害与状态，以及三连击延迟支援。</summary>
    [Test]
    public void Bombard_FourStacksBoostEveryDeclaredSupportPayload()
    {
        using (var bansheeScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265, 3238 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 3))
        {
            bansheeScenario.StartBattle();
            bansheeScenario.Play(3265, targetId: null);
            bansheeScenario.Play(3238, targetId: null);

            int resultIndex = bansheeScenario.Results.Count;
            bansheeScenario.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                bansheeScenario.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.BansheeStrike);

            Assert.That(
                GetPlayerDamageSettlements(trigger, bansheeScenario.Player.Id)
                    .Select(item => item.AttackValue),
                Is.All.EqualTo(11));
            AssertOrdersAreContinuous(trigger);
        }

        using (var bombardmentScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265, 3240 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 3))
        {
            bombardmentScenario.StartBattle();
            bombardmentScenario.Play(3265, targetId: null);
            bombardmentScenario.Play(3240, targetId: null);

            int resultIndex = bombardmentScenario.Results.Count;
            bombardmentScenario.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                bombardmentScenario.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.FireBombardment);
            MachineGunnerPrivateStatusChangedSettlement firstBurn = trigger.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .First(item => item.Status == MachineGunnerCombatantStatus.Burn);
            MachineGunnerPrivateStatusChangedSettlement firstOil = trigger.Settlements
                .OfType<MachineGunnerPrivateStatusChangedSettlement>()
                .First(item => item.Status == MachineGunnerCombatantStatus.Oil);

            Assert.That(
                GetPlayerDamageSettlements(trigger, bombardmentScenario.Player.Id)
                    .Select(item => item.AttackValue),
                Is.All.EqualTo(3));
            Assert.That(firstBurn.ValueAfter - firstBurn.ValueBefore, Is.EqualTo(6));
            Assert.That(firstOil.ValueAfter - firstOil.ValueBefore, Is.EqualTo(4));
            AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.FireBombardment);
            AssertOrdersAreContinuous(trigger);
        }

        using (var tripleScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265, 3264 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 5,
                   initialAmmo: 3))
        {
            tripleScenario.StartBattle();
            tripleScenario.Play(3265, targetId: null);
            tripleScenario.Play(3264, tripleScenario.FirstEnemy.Id);

            int resultIndex = tripleScenario.Results.Count;
            tripleScenario.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                tripleScenario.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.TripleStrike);

            Assert.That(
                GetPlayerDamageSettlements(trigger, tripleScenario.Player.Id).Single().AttackValue,
                Is.EqualTo(28));
            AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.TripleStrike);
            AssertOrdersAreContinuous(trigger);
        }
    }

    /// <summary>验证轰炸先换算支援基值再进入烟雾、易伤和破甲管线；预置两层易伤以跨过敌方行动衰减，并确认炸弹、钢针与回合末燃烧不受影响。</summary>
    [Test]
    public void Bombard_PreservesSupportPipelineOrderAndExcludesNonDeclaredDamage()
    {
        using (var pipelineScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265, 3239 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2))
        {
            pipelineScenario.StartBattle();
            pipelineScenario.Play(3265, targetId: null);
            pipelineScenario.Play(3239, targetId: null);
            foreach (EnemyCombatantData enemy in new[] { pipelineScenario.FirstEnemy, pipelineScenario.SecondEnemy })
            {
                // 敌方行动阶段会衰减一层易伤；先加两层，确保下个玩家回合支援触发时仍保留一层。
                enemy.ApplyVulnerableGain(2);
                MachineGunnerCombatState state = pipelineScenario.Session.MachineGunnerRuntime.CombatState;
                state.Add(enemy.Id, MachineGunnerCombatantStatus.Smoke, 1);
                state.Add(enemy.Id, MachineGunnerCombatantStatus.ArmorBreak, 2);
            }

            int resultIndex = pipelineScenario.Results.Count;
            pipelineScenario.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                pipelineScenario.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.FireSupport);

            Assert.That(
                GetPlayerDamageSettlements(trigger, pipelineScenario.Player.Id)
                    .Select(item => item.AttackValue),
                Is.All.EqualTo(6),
                "预置易伤2会在敌方行动后剩1；2×1.4 半入为3，再经烟雾成为2、易伤成为3，最后追加破甲易伤值3。 ");
        }

        using (var needleScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265, 3274 },
                   initialHandCount: 2,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 2))
        {
            needleScenario.StartBattle();
            needleScenario.Play(3265, targetId: null);
            needleScenario.Play(3274, targetId: null);

            int resultIndex = needleScenario.Results.Count;
            needleScenario.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                needleScenario.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.NeedleStorm);

            Assert.That(
                GetPlayerDamageSettlements(trigger, needleScenario.Player.Id)
                    .Select(item => item.AttackValue),
                Is.All.EqualTo(1));
        }

        using (var bombScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265, 3241 },
                   initialHandCount: 2,
                   firstEnemyHealth: 200,
                   secondEnemyHealth: 200,
                   enemyDamage: 0,
                   initialEnergy: 4))
        {
            bombScenario.StartBattle();
            bombScenario.Play(3265, targetId: null);
            bombScenario.Play(3241, targetId: null);
            bombScenario.EndPlayerActionResult();
            bombScenario.EndPlayerActionResult();
            BattleCommandExecutionResult explosion = bombScenario.EndPlayerActionResult();

            Assert.That(
                GetPlayerDamageSettlements(explosion, bombScenario.Player.Id)
                    .Select(item => item.AttackValue),
                Is.All.EqualTo(60));
        }

        using (var burnScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265 },
                   initialHandCount: 1,
                   firstEnemyHealth: 100,
                   secondEnemyHealth: 100,
                   enemyDamage: 0,
                   initialEnergy: 1))
        {
            burnScenario.StartBattle();
            burnScenario.Play(3265, targetId: null);
            MachineGunnerCombatState state = burnScenario.Session.MachineGunnerRuntime.CombatState;
            state.Add(burnScenario.FirstEnemy.Id, MachineGunnerCombatantStatus.Burn, 2);
            state.Add(burnScenario.Player.Id, MachineGunnerCombatantStatus.Burn, 2);

            BattleCommandExecutionResult roundEnd = burnScenario.EndPlayerActionResult();

            Assert.That(
                roundEnd.Settlements.OfType<BattleDamageAppliedSettlement>()
                    .Select(item => item.AttackValue),
                Is.EqualTo(new[] { 2, 2 }));
        }
    }

    /// <summary>验证燃烧轰炸被放大后的致死伤害跳过该目标状态，同时保持触发、倒计时、移除与连续序号。</summary>
    [Test]
    public void Bombard_FireBombardmentFatalHitSkipsStatusesAndKeepsLifecycleOrder()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3265, 3240 },
            initialHandCount: 2,
            firstEnemyHealth: 3,
            secondEnemyHealth: 100,
            enemyDamage: 0,
            initialEnergy: 3);
        scenario.StartBattle();
        scenario.Play(3265, targetId: null);
        scenario.Play(3240, targetId: null);

        int resultIndex = scenario.Results.Count;
        scenario.EndPlayerAction();
        BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
            scenario.Results.Skip(resultIndex),
            MachineGunnerScheduledEffectKind.FireBombardment);
        BattleDamageAppliedSettlement[] hits = GetPlayerDamageSettlements(trigger, scenario.Player.Id);
        MachineGunnerPrivateStatusChangedSettlement[] statuses = trigger.Settlements
            .OfType<MachineGunnerPrivateStatusChangedSettlement>()
            .Where(item => item.Status == MachineGunnerCombatantStatus.Burn ||
                item.Status == MachineGunnerCombatantStatus.Oil)
            .ToArray();

        Assert.That(hits.Select(item => item.TargetId), Is.EqualTo(new CombatantId?[]
        {
            scenario.FirstEnemy.Id,
            scenario.SecondEnemy.Id,
            scenario.SecondEnemy.Id,
        }));
        Assert.That(hits.Select(item => item.AttackValue), Is.All.EqualTo(3));
        Assert.That(hits[0].WasFatal, Is.True);
        Assert.That(statuses.Select(item => item.TargetId), Is.All.EqualTo(scenario.SecondEnemy.Id));
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.FirstEnemy.Id,
            MachineGunnerCombatantStatus.Burn), Is.Zero);
        Assert.That(scenario.Session.MachineGunnerRuntime.CombatState.Get(
            scenario.FirstEnemy.Id,
            MachineGunnerCombatantStatus.Oil), Is.Zero);
        AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.FireBombardment);
        Assert.That(scenario.Session.MachineGunnerRuntime.ScheduledEffectCount, Is.Zero);
        AssertOrdersAreContinuous(trigger);
    }

    /// <summary>验证天空之怒只在四类原始支援逻辑段完整结算后触发，并保持逐段插入与生命周期顺序。</summary>
    [Test]
    public void SkyWrath_TriggersOnceAfterEveryDeclaredSupportSegment()
    {
        using (var banshee = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3238 }, 2, 200, 200, 0, 3))
        {
            banshee.StartBattle();
            banshee.Play(3266, targetId: null);
            banshee.Play(3238, targetId: null);
            int resultIndex = banshee.Results.Count;
            banshee.EndPlayerAction();

            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                banshee.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.BansheeStrike);
            Assert.That(
                GetPlayerDamageSettlements(trigger, banshee.Player.Id).Select(item => item.AttackValue),
                Is.EqualTo(new[] { 8, 8, 4, 8, 8, 4 }));
            AssertOrdersAreContinuous(trigger);
        }

        using (var fireSupport = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3239 }, 2, 300, 300, 0, 2))
        {
            fireSupport.StartBattle();
            fireSupport.Play(3266, targetId: null);
            fireSupport.Play(3239, targetId: null);
            int resultIndex = fireSupport.Results.Count;
            fireSupport.EndPlayerAction();

            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                fireSupport.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.FireSupport);
            Assert.That(
                GetPlayerDamageSettlements(trigger, fireSupport.Player.Id).Select(item => item.AttackValue),
                Is.EqualTo(Enumerable.Repeat(new[] { 2, 8, 4 }, 5).SelectMany(values => values)));
            AssertOrdersAreContinuous(trigger);
        }

        using (var fireBombardment = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3240 }, 2, 200, 200, 0, 3))
        {
            fireBombardment.StartBattle();
            fireBombardment.Play(3266, targetId: null);
            fireBombardment.Play(3240, targetId: null);
            int resultIndex = fireBombardment.Results.Count;
            fireBombardment.EndPlayerAction();

            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                fireBombardment.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.FireBombardment);
            Assert.That(
                GetPlayerDamageSettlements(trigger, fireBombardment.Player.Id)
                    .Select(item => item.AttackValue),
                Is.EqualTo(new[] { 2, 2, 8, 4, 2, 2, 8, 4 }));
            AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.FireBombardment);
            AssertOrdersAreContinuous(trigger);
        }

        using (var triple = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3264 }, 2, 200, 200, 0, 5, 3))
        {
            triple.StartBattle();
            triple.Play(3266, targetId: null);
            triple.Play(3264, triple.FirstEnemy.Id);
            int resultIndex = triple.Results.Count;
            triple.EndPlayerAction();

            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                triple.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.TripleStrike);
            Assert.That(
                GetPlayerDamageSettlements(trigger, triple.Player.Id).Select(item => item.AttackValue),
                Is.EqualTo(new[] { 20, 8, 4 }));
            AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.TripleStrike);
            AssertOrdersAreContinuous(trigger);
        }
    }

    /// <summary>验证天空之怒每层独立推进随机流，主目标死亡后下一层重取候选，并在单候选时仍消费一次随机数。</summary>
    [Test]
    public void SkyWrath_EachLayerRerollsLivingCandidatesAndSingleCandidateStillAdvancesRandom()
    {
        using (var layered = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3266, 3238 },
                   initialHandCount: 3,
                   firstEnemyHealth: 200,
                   secondEnemyHealth: 200,
                   enemyDamage: 0,
                   initialEnergy: 4,
                   thirdEnemyHealth: 200))
        {
            layered.StartBattle();
            layered.Play(3266, targetId: null);
            layered.Play(3266, targetId: null);
            layered.Play(3238, targetId: null);
            uint randomBefore = layered.Session.MachineGunnerRuntime.CardRandomState;
            var oracle = new GameRandom(1u) { State = randomBefore };
            EnemyCombatantData[] encounter =
            {
                layered.FirstEnemy,
                layered.SecondEnemy,
                layered.ThirdEnemy,
            };
            int firstMainIndex = oracle.NextInt(encounter.Length);
            EnemyCombatantData firstMain = encounter[firstMainIndex];
            EnemyCombatantData[] secondLayerCandidates = encounter
                .Where(enemy => enemy != firstMain)
                .ToArray();
            EnemyCombatantData secondMain = secondLayerCandidates[
                oracle.NextInt(secondLayerCandidates.Length)];
            EnemyCombatantData lastSplash = secondLayerCandidates.Single(enemy => enemy != secondMain);
            foreach (EnemyCombatantData enemy in encounter)
            {
                int healthAtSkyStart = enemy == firstMain
                    ? 8
                    : enemy == secondMain
                        ? 12
                        : 8;
                int healthBeforeBanshee = healthAtSkyStart + (enemy == layered.FirstEnemy ? 8 : 0);
                ReduceHealthTo(enemy, healthBeforeBanshee);
            }

            int resultIndex = layered.Results.Count;
            layered.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                layered.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.BansheeStrike);
            BattleDamageAppliedSettlement[] hits = GetPlayerDamageSettlements(trigger, layered.Player.Id);

            Assert.That(hits.Select(item => item.AttackValue),
                Is.EqualTo(new[] { 8, 8, 4, 4, 8, 4 }));
            Assert.That(hits[1].TargetId, Is.EqualTo(firstMain.Id),
                "第一层主目标必须匹配该层独立随机结果。");
            Assert.That(hits[1].WasFatal, Is.True);
            Assert.That(hits[4].TargetId, Is.EqualTo(secondMain.Id),
                "第一层主目标死亡后，第二层必须从剩余存活候选重抽。");
            Assert.That(hits[5].TargetId, Is.EqualTo(lastSplash.Id));
            Assert.That(hits.Skip(4).Select(item => item.WasFatal), Is.All.True);
            Assert.That(layered.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(oracle.State));
            AssertOrdersAreContinuous(trigger);
        }

        using (var single = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3238 }, 2, 200, 16, 0, 3))
        {
            single.StartBattle();
            single.Play(3266, targetId: null);
            single.Play(3238, targetId: null);
            ReduceHealthTo(single.FirstEnemy, 0);
            uint randomBefore = single.Session.MachineGunnerRuntime.CardRandomState;
            var oracle = new GameRandom(1u) { State = randomBefore };
            oracle.NextInt(1);

            int resultIndex = single.Results.Count;
            single.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                single.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.BansheeStrike);

            Assert.That(
                GetPlayerDamageSettlements(trigger, single.Player.Id).Select(item => item.AttackValue),
                Is.EqualTo(new[] { 8, 8 }));
            Assert.That(single.Session.MachineGunnerRuntime.CardRandomState, Is.EqualTo(oracle.State));
            AssertOrdersAreContinuous(trigger);
        }
    }

    /// <summary>验证轰炸先放大天空之怒，且钢针、炸弹、燃烧、即时射击与便携助手均不会反向或递归触发天空之怒。</summary>
    [Test]
    public void SkyWrath_UsesBombardScalingAndExcludesNonSupportDamageWithoutRecursion()
    {
        using (var scaled = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3265, 3266, 3239 }, 3, 400, 400, 0, 3))
        {
            scaled.StartBattle();
            scaled.Play(3265, targetId: null);
            scaled.Play(3266, targetId: null);
            scaled.Play(3239, targetId: null);
            int resultIndex = scaled.Results.Count;
            scaled.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                scaled.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.FireSupport);

            Assert.That(
                GetPlayerDamageSettlements(trigger, scaled.Player.Id).Select(item => item.AttackValue),
                Is.EqualTo(Enumerable.Repeat(new[] { 3, 11, 6 }, 5).SelectMany(values => values)));
            AssertTriggeredCountdownRemoved(trigger, MachineGunnerScheduledEffectKind.FireSupport);
            AssertOrdersAreContinuous(trigger);
        }

        using (var needle = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3274 }, 2, 100, 100, 0, 2))
        {
            needle.StartBattle();
            needle.Play(3266, targetId: null);
            needle.Play(3274, targetId: null);
            int resultIndex = needle.Results.Count;
            needle.EndPlayerAction();
            BattleCommandExecutionResult trigger = FindScheduledTriggerResult(
                needle.Results.Skip(resultIndex),
                MachineGunnerScheduledEffectKind.NeedleStorm);
            Assert.That(GetPlayerDamageSettlements(trigger, needle.Player.Id)
                .Select(item => item.AttackValue), Is.EqualTo(new[] { 1, 1, 1, 1 }));
        }

        using (var bomb = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3241 }, 2, 200, 200, 0, 4))
        {
            bomb.StartBattle();
            bomb.Play(3266, targetId: null);
            bomb.Play(3241, targetId: null);
            bomb.EndPlayerActionResult();
            bomb.EndPlayerActionResult();
            BattleCommandExecutionResult explosion = bomb.EndPlayerActionResult();
            Assert.That(GetPlayerDamageSettlements(explosion, bomb.Player.Id)
                .Select(item => item.AttackValue), Is.EqualTo(new[] { 60, 60 }));
        }

        using (var burn = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266 }, 1, 100, 100, 0, 1))
        {
            burn.StartBattle();
            burn.Play(3266, targetId: null);
            burn.Session.MachineGunnerRuntime.CombatState.Add(
                burn.FirstEnemy.Id,
                MachineGunnerCombatantStatus.Burn,
                2);
            BattleCommandExecutionResult roundEnd = burn.EndPlayerActionResult();
            Assert.That(roundEnd.Settlements.OfType<BattleDamageAppliedSettlement>()
                .Select(item => item.AttackValue), Is.EqualTo(new[] { 2 }));
        }

        using (var helper = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
                   new[] { 3266, 3267, 3201 }, 3, 100, 100, 0, 2))
        {
            helper.StartBattle();
            helper.Play(3266, targetId: null);
            helper.Play(3267, targetId: null);
            BattleCommandExecutionResult shot = helper.Play(3201, helper.FirstEnemy.Id);
            Assert.That(GetPlayerDamageSettlements(shot, helper.Player.Id)
                .Select(item => item.AttackValue), Is.EqualTo(new[] { 6, 1 }));
            AssertOrdersAreContinuous(shot);
        }
    }

    /// <summary>通过统一伤害公式把指定敌人压到目标生命值，供随机候选重取测试建立确定场景。</summary>
    private static void ReduceHealthTo(EnemyCombatantData enemy, int healthAfter)
    {
        if (enemy == null)
            throw new ArgumentNullException(nameof(enemy));
        if (healthAfter < 0 || healthAfter >= enemy.CurrentHealth)
            throw new ArgumentOutOfRangeException(nameof(healthAfter));

        BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
            new BattleEffectFormulaContext(
                BattleEffectOperationType.DealDamage,
                enemy.CurrentHealth - healthAfter,
                sourceStrength: 0,
                new BattleEffectTargetSnapshot(
                    enemy.CurrentHealth,
                    enemy.CurrentBlock,
                    enemy.CurrentVulnerable)));
        enemy.ApplyDamageOutcome(formula.DamageOutcome.Value);
    }

    /// <summary>确认一次性支援触发仍按触发、倒计时、移除三条稳定顺序结算。</summary>
    private static void AssertTriggeredCountdownRemoved(
        BattleCommandExecutionResult result,
        MachineGunnerScheduledEffectKind kind)
    {
        Assert.That(
            result.Settlements
                .OfType<MachineGunnerScheduledEffectChangedSettlement>()
                .Where(item => item.Kind == kind)
                .Select(item => item.ChangeKind),
            Is.EqualTo(new[]
            {
                MachineGunnerScheduledEffectChangeKind.Triggered,
                MachineGunnerScheduledEffectChangeKind.Countdown,
                MachineGunnerScheduledEffectChangeKind.Removed,
            }));
    }

    private static void AssertScheduledProgress(
        BattleCommandExecutionResult result,
        MachineGunnerScheduledEffectKind kind,
        int before,
        int after)
    {
        MachineGunnerScheduledEffectChangedSettlement progress = result.Settlements
            .OfType<MachineGunnerScheduledEffectChangedSettlement>()
            .Single(item => item.Kind == kind);
        Assert.That(progress.ChangeKind, Is.EqualTo(MachineGunnerScheduledEffectChangeKind.Countdown));
        Assert.That(progress.RemainingBefore, Is.EqualTo(before));
        Assert.That(progress.RemainingAfter, Is.EqualTo(after));
        AssertOrdersAreContinuous(result);
    }

    /// <summary>在自动续接产生的命令序列中定位指定延迟实例的唯一触发结果。</summary>
    private static BattleCommandExecutionResult FindScheduledTriggerResult(
        IEnumerable<BattleCommandExecutionResult> results,
        MachineGunnerScheduledEffectKind kind)
    {
        return results.Single(result => result.Settlements
            .OfType<MachineGunnerScheduledEffectChangedSettlement>()
            .Any(item => item.Kind == kind &&
                         item.ChangeKind == MachineGunnerScheduledEffectChangeKind.Triggered));
    }

    /// <summary>返回一条命令中由指定玩家来源造成的所有伤害记录。</summary>
    private static BattleDamageAppliedSettlement[] GetPlayerDamageSettlements(
        BattleCommandExecutionResult result,
        CombatantId playerId)
    {
        return result.Settlements
            .OfType<BattleDamageAppliedSettlement>()
            .Where(item => item.SourceId == playerId)
            .ToArray();
    }

    /// <summary>确认一条命令内的所有 settlement 从零开始连续编号。</summary>
    private static void AssertOrdersAreContinuous(BattleCommandExecutionResult result)
    {
        Assert.That(
            result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(0, result.Settlements.Count)));
    }
}
