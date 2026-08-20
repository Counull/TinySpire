using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattlePlayerResourceProfileTests
{
    /// <summary>释放共享工厂为敌人意图创建但不由命令队列拥有的响应式资源。</summary>
    [TearDown]
    public void TearDown()
    {
        BattleCommandQueueTestFactory.DisposeOwnedEnemyIntents();
    }

    /// <summary>验证首回合只采用 Hero 初始资源，并固定按 Block、Energy、Ammo、发牌顺序结算。</summary>
    [Test]
    public void StartBattle_FirstPlayerRound_InitializesEnergyAndAmmoAfterBlockClear()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 1001, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 0);
        using var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1u);
        var presentation = new ControllableBattleCommandPresentation();
        var profiles = new Dictionary<CombatantId, BattlePlayerResourceProfile>
        {
            [player.Id] = new BattlePlayerResourceProfile(
                initialEnergy: 3,
                maxEnergy: 5,
                energyGainPerRound: 3,
                initialAmmo: 5,
                maxAmmo: 5,
                ammoGainPerRound: 1),
        };
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            playerCardZones: new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones,
            },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            playerResourceProfiles: profiles);
        BattleEffectStateTestDriver.Execute(
            combatants,
            player.Id,
            player.Id,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            configuredValue: 4);

        queue.Submit(new StartBattleCommand());

        BattleCommandExecutionResult result = presentation.Results[0];
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Settlements, Has.Count.EqualTo(4));
        Assert.That(result.Settlements[0], Is.TypeOf<BattleBlockClearedSettlement>());
        Assert.That(result.Settlements[1], Is.TypeOf<BattleEnergyRefilledSettlement>());
        Assert.That(result.Settlements[2], Is.TypeOf<BattleAmmoRefilledSettlement>());
        Assert.That(result.Settlements[3], Is.TypeOf<BattlePhaseChangedSettlement>());

        var energy = (BattleEnergyRefilledSettlement)result.Settlements[1];
        var ammo = (BattleAmmoRefilledSettlement)result.Settlements[2];
        Assert.That(energy.EnergyBefore, Is.Zero);
        Assert.That(energy.EnergyAfter, Is.EqualTo(3));
        Assert.That(ammo.AmmoBefore, Is.Zero);
        Assert.That(ammo.AmmoAfter, Is.EqualTo(5));

        PlayerTurnData turn = queue.Turn.CurrentValue.Players[player.Id];
        Assert.That(turn.Energy, Is.EqualTo(3));
        Assert.That(turn.EnergyMaximum, Is.EqualTo(5));
        Assert.That(turn.EnergyGainPerRound, Is.EqualTo(3));
        Assert.That(turn.Ammo, Is.EqualTo(5));
        Assert.That(turn.AmmoMaximum, Is.EqualTo(5));
        Assert.That(turn.AmmoGainPerRound, Is.EqualTo(1));
    }

    /// <summary>验证 Hero 资源档案不接管抽牌数量，首回合仍通过共享规则补至默认 5 张手牌。</summary>
    [Test]
    public void StartBattle_ResourceProfile_ReusesSharedDefaultHandCount()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 1001, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 0);
        using var zones = new BattleCardZonesData(
            new[] { 3001, 3001, 3001, 3001, 3001 },
            shuffleSeed: 1u);
        var presentation = new ControllableBattleCommandPresentation();
        var profiles = new Dictionary<CombatantId, BattlePlayerResourceProfile>
        {
            [player.Id] = new BattlePlayerResourceProfile(
                initialEnergy: 3,
                maxEnergy: 3,
                energyGainPerRound: 3,
                initialAmmo: 0,
                maxAmmo: 0,
                ammoGainPerRound: 0),
        };
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            playerCardZones: new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones,
            },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            initialHandCount: 5,
            playerResourceProfiles: profiles);

        queue.Submit(new StartBattleCommand());

        Assert.That(presentation.Results[0].Succeeded, Is.True);
        Assert.That(zones.Hand, Has.Count.EqualTo(5));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(3));
    }

    /// <summary>验证后续回合保留未消耗资源并按各自增量补充到上限，而非回写首回合初始值。</summary>
    [Test]
    public void SecondPlayerRound_PreservesAndCapsEnergyAndAmmo()
    {
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 1001, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 0);
        using var zones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 1u);
        var presentation = new ControllableBattleCommandPresentation();
        var profiles = new Dictionary<CombatantId, BattlePlayerResourceProfile>
        {
            [player.Id] = new BattlePlayerResourceProfile(
                initialEnergy: 3,
                maxEnergy: 5,
                energyGainPerRound: 3,
                initialAmmo: 4,
                maxAmmo: 5,
                ammoGainPerRound: 1),
        };
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            playerCardZones: new Dictionary<CombatantId, BattleCardZonesData>
            {
                [player.Id] = zones,
            },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            playerResourceProfiles: profiles);

        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        queue.Submit(new EndPlayerActionCommand(player.Id));
        presentation.CompleteNext();

        Assert.That(presentation.Results, Has.Count.EqualTo(3));
        BattleCommandExecutionResult secondRound = presentation.Results[2];
        Assert.That(secondRound.Succeeded, Is.True);
        var energy = FindSettlement<BattleEnergyRefilledSettlement>(secondRound);
        var ammo = FindSettlement<BattleAmmoRefilledSettlement>(secondRound);
        Assert.That(energy.EnergyBefore, Is.EqualTo(3));
        Assert.That(energy.EnergyAfter, Is.EqualTo(5));
        Assert.That(ammo.AmmoBefore, Is.EqualTo(4));
        Assert.That(ammo.AmmoAfter, Is.EqualTo(5));
        Assert.That(energy.Order, Is.LessThan(ammo.Order));
        Assert.That(queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Energy, Is.EqualTo(5));
        Assert.That(queue.Turn.CurrentValue.Players[player.Id].Ammo, Is.EqualTo(5));
    }

    /// <summary>验证上限降低时在同一次不可变事实重建中立即裁剪当前值，且两种资源互不串改。</summary>
    [Test]
    public void PlayerTurnData_RebuiltWithLowerCaps_ClampsCurrentValuesImmediately()
    {
        var lowered = new PlayerTurnData(
            energy: 5,
            energyMaximum: 3,
            energyGainPerRound: 4,
            ammo: 5,
            ammoMaximum: 4,
            ammoGainPerRound: 2,
            hasEndedAction: false);

        Assert.That(lowered.Energy, Is.EqualTo(3));
        Assert.That(lowered.EnergyMaximum, Is.EqualTo(3));
        Assert.That(lowered.EnergyGainPerRound, Is.EqualTo(4));
        Assert.That(lowered.Ammo, Is.EqualTo(4));
        Assert.That(lowered.AmmoMaximum, Is.EqualTo(4));
        Assert.That(lowered.AmmoGainPerRound, Is.EqualTo(2));
    }

    /// <summary>验证非法 Hero 资源档案在创建任何战斗聚合前立即失败。</summary>
    [Test]
    public void ResourceProfile_RejectsInvalidStaticValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattlePlayerResourceProfile(
            initialEnergy: 4,
            maxEnergy: 3,
            energyGainPerRound: 1,
            initialAmmo: 0,
            maxAmmo: 0,
            ammoGainPerRound: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattlePlayerResourceProfile(
            initialEnergy: 3,
            maxEnergy: 3,
            energyGainPerRound: 3,
            initialAmmo: 1,
            maxAmmo: 0,
            ammoGainPerRound: 0));
    }

    /// <summary>从一条连续结算链中读取恰好一条指定类型记录，避免测试自己重排权威顺序。</summary>
    private static T FindSettlement<T>(BattleCommandExecutionResult result)
        where T : BattleSettlementRecord
    {
        T found = null;
        foreach (BattleSettlementRecord settlement in result.Settlements)
        {
            if (!(settlement is T typed))
                continue;
            if (found != null)
                Assert.Fail($"Expected exactly one {typeof(T).Name} settlement.");

            found = typed;
        }

        Assert.That(found, Is.Not.Null, $"Missing {typeof(T).Name} settlement.");
        return found;
    }
}
