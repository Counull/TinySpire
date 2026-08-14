using NUnit.Framework;
using TinySpire.Battle;

/// <summary>机枪兵私有状态与伤害管线的纯规则回归测试。</summary>
public sealed class MachineGunnerDamagePipelineTests
{
    /// <summary>验证虚弱、双方烟雾、易伤、格挡和生命严格按需求顺序与向下取整结算。</summary>
    [Test]
    public void Attack_AppliesWeaknessSmokeVulnerableBlockAndHealthInOrder()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Weakness, 1);
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 2);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.Smoke, 1);
        scenario.Enemy.ApplyVulnerableGain(1);
        scenario.Enemy.ApplyBlockGain(5);

        MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 8,
                kind: MachineGunnerDamageKind.Attack),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(calculation.Outcome.AttackValue, Is.EqualTo(9));
        Assert.That(calculation.Outcome.BlockAfter, Is.EqualTo(0));
        Assert.That(calculation.Outcome.HealthAfter, Is.EqualTo(26));
    }

    /// <summary>验证失去力量只从攻击管线的力量体系中扣除并保留零下限，燃烧等非攻击段不读取该状态。</summary>
    [Test]
    public void LoseStrength_SubtractsFromAttackButDoesNotModifyBurn()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.LoseStrength, 6);

        MachineGunnerDamageCalculation attack = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 8,
                kind: MachineGunnerDamageKind.Attack),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);
        MachineGunnerDamageCalculation burn = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 5,
                kind: MachineGunnerDamageKind.Burn),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);
        scenario.State.Set(
            scenario.Player.Id,
            MachineGunnerCombatantStatus.LoseStrength,
            value: 20);
        MachineGunnerDamageCalculation flooredAttack = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 8,
                kind: MachineGunnerDamageKind.Attack),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(attack.Outcome.AttackValue, Is.EqualTo(6));
        Assert.That(burn.Outcome.AttackValue, Is.EqualTo(5));
        Assert.That(flooredAttack.Outcome.AttackValue, Is.Zero);
    }

    /// <summary>验证开火只作用于普通射击标签，射击加狙击双词条也不会漏掉开火层数。</summary>
    [Test]
    public void FirePower_AppliesToShootAndShootSniperTags()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.FirePower, 2);

        MachineGunnerDamageCalculation standardShoot = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 6,
                kind: MachineGunnerDamageKind.Attack,
                tags: MachineGunnerCardTag.Shoot),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);
        MachineGunnerDamageCalculation shootSniper = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 6,
                kind: MachineGunnerDamageKind.Attack,
                isSniper: true,
                isShoot: true),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(standardShoot.Outcome.AttackValue, Is.EqualTo(12));
        Assert.That(shootSniper.Outcome.AttackValue, Is.EqualTo(12));

        MachineGunnerDamageCalculation pureSniper = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 6,
                kind: MachineGunnerDamageKind.Attack,
                tags: MachineGunnerCardTag.Sniper),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(pureSniper.Outcome.AttackValue, Is.EqualTo(10));
    }

    /// <summary>验证狙击不读取来源烟雾，但仍读取目标烟雾和射击分类的开火层数。</summary>
    [Test]
    public void ShootSniper_IgnoresSourceSmokeButUsesTargetSmokeAndFirePower()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 4);
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.FirePower, 2);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.Smoke, 3);

        MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 6,
                kind: MachineGunnerDamageKind.Attack,
                tags: MachineGunnerCardTag.Shoot | MachineGunnerCardTag.Sniper),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(calculation.Outcome.AttackValue, Is.EqualTo(9));
    }

    /// <summary>验证支援段只读取目标烟雾、易伤和破甲，不把施放者力量、虚弱或烟雾带入延迟伤害。</summary>
    [Test]
    public void Support_UsesTargetSmokeVulnerableAndArmorBreakOnly()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Weakness, 1);
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 5);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.Smoke, 2);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.ArmorBreak, 3);
        scenario.Enemy.ApplyVulnerableGain(1);

        MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 10,
                kind: MachineGunnerDamageKind.Support),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(calculation.Outcome.AttackValue, Is.EqualTo(16));
    }

    /// <summary>验证炸弹只受目标烟雾影响，而燃烧免疫烟雾且仅把破甲附加量纳入最终伤害。</summary>
    [Test]
    public void BombAndBurn_KeepTheirSeparateDamageSemantics()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Weakness, 1);
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 5);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.Smoke, 2);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.ArmorBreak, 3);
        scenario.Enemy.ApplyVulnerableGain(1);

        MachineGunnerDamageCalculation bomb = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 10,
                kind: MachineGunnerDamageKind.Bomb),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);
        MachineGunnerDamageCalculation burn = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 5,
                kind: MachineGunnerDamageKind.Burn),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(bomb.Outcome.AttackValue, Is.EqualTo(8));
        Assert.That(burn.Outcome.AttackValue, Is.EqualTo(9));
        Assert.That(burn.ConsumesArmor, Is.False);
    }

    /// <summary>验证燃烧等 debuff 伤害不读取虚弱、烟雾或易伤，但仍按格挡与生命结算。</summary>
    [Test]
    public void Debuff_IgnoresAttackModifiersButUsesBlockAndHealth()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Weakness, 1);
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 4);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.Smoke, 4);
        scenario.Enemy.ApplyVulnerableGain(2);
        scenario.Enemy.ApplyBlockGain(3);

        MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Player.Id,
                scenario.Enemy.Id,
                baseDamage: 5,
                kind: MachineGunnerDamageKind.Debuff),
            scenario.Player,
            Snapshot(scenario.Enemy),
            scenario.State);

        Assert.That(calculation.Outcome.AttackValue, Is.EqualTo(5));
        Assert.That(calculation.Outcome.BlockAfter, Is.EqualTo(0));
        Assert.That(calculation.Outcome.HealthAfter, Is.EqualTo(28));
        Assert.That(calculation.ConsumesArmor, Is.False);
    }

    /// <summary>验证燃烧只消耗既有浸油，新追加浸油不会在同次燃烧中自触发。</summary>
    [Test]
    public void ApplyBurn_UsesExistingOilThenHalvesIt()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.Oil, 5);

        MachineGunnerBurnApplicationResult burn = scenario.State.ApplyBurn(
            scenario.Enemy.Id,
            baseBurn: 3);
        scenario.State.Add(scenario.Enemy.Id, MachineGunnerCombatantStatus.Oil, 5);

        Assert.That(burn.BurnChange.Before, Is.EqualTo(0));
        Assert.That(burn.BurnChange.After, Is.EqualTo(8));
        Assert.That(burn.OilChange.Before, Is.EqualTo(5));
        Assert.That(burn.OilChange.After, Is.EqualTo(2));
        Assert.That(scenario.State.Get(scenario.Enemy.Id, MachineGunnerCombatantStatus.Oil), Is.EqualTo(7));
    }

    /// <summary>验证护甲只在攻击真实穿透生命后消耗一层，燃烧等 debuff 不会消耗护甲。</summary>
    [Test]
    public void Armor_ConsumesOneLayerOnlyAfterAttackPenetratesHealth()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Armor, 2);
        scenario.Player.ApplyBlockGain(5);

        MachineGunnerDamageCalculation attack = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Enemy.Id,
                scenario.Player.Id,
                baseDamage: 6,
                kind: MachineGunnerDamageKind.Attack),
            scenario.Enemy,
            Snapshot(scenario.Player),
            scenario.State);
        bool consumed = scenario.State.TryConsumeArmorAfterPenetratingAttack(
            scenario.Player.Id,
            attack,
            out MachineGunnerStatusValueChange armorChange);
        MachineGunnerDamageCalculation burn = MachineGunnerDamagePipeline.Calculate(
            new MachineGunnerDamageRequest(
                scenario.Enemy.Id,
                scenario.Player.Id,
                baseDamage: 4,
                kind: MachineGunnerDamageKind.Debuff),
            scenario.Enemy,
            new BattleEffectTargetSnapshot(
                attack.Outcome.HealthAfter,
                attack.Outcome.BlockAfter,
                scenario.Player.CurrentVulnerable),
            scenario.State);

        Assert.That(attack.Outcome.HealthLoss, Is.EqualTo(1));
        Assert.That(consumed, Is.True);
        Assert.That(armorChange.Before, Is.EqualTo(2));
        Assert.That(armorChange.After, Is.EqualTo(1));
        Assert.That(burn.ConsumesArmor, Is.False);
    }

    /// <summary>验证普通烟雾回合开始清空，而烟雾弥漫只递减一层。</summary>
    [Test]
    public void Smoke_RoundStartClearsOrDecaysAccordingToPowerState()
    {
        using var scenario = new MachineGunnerDamageScenario();
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 3);

        MachineGunnerStatusValueChange decayed = scenario.State.AdvanceSmokeAtPlayerRoundStart(
            scenario.Player.Id,
            persistsAndDecays: true);
        scenario.State.Add(scenario.Player.Id, MachineGunnerCombatantStatus.Smoke, 2);
        MachineGunnerStatusValueChange cleared = scenario.State.AdvanceSmokeAtPlayerRoundStart(
            scenario.Player.Id,
            persistsAndDecays: false);

        Assert.That(decayed.After, Is.EqualTo(2));
        Assert.That(cleared.Before, Is.EqualTo(4));
        Assert.That(cleared.After, Is.EqualTo(0));
    }

    /// <summary>从参与者当前权威标量创建纯伤害计算使用的目标快照。</summary>
    private static BattleEffectTargetSnapshot Snapshot(CombatantData combatant)
    {
        return new BattleEffectTargetSnapshot(
            combatant.CurrentHealth,
            combatant.CurrentBlock,
            combatant.CurrentVulnerable);
    }

    /// <summary>构造并释放纯伤害规则测试需要的最小参与者和私有状态。</summary>
    private sealed class MachineGunnerDamageScenario : System.IDisposable
    {
        /// <summary>参与者事实唯一所有者。</summary>
        private readonly BattleCombatantsData _combatants;

        /// <summary>机枪兵玩家，带四点力量用于验证攻击修正顺序。</summary>
        internal PlayerCombatantData Player { get; }

        /// <summary>敌方参与者，初始三十生命且零力量。</summary>
        internal EnemyCombatantData Enemy { get; }

        /// <summary>被测职业会话私有状态。</summary>
        internal MachineGunnerCombatState State { get; }

        /// <summary>创建稳定的玩家、敌人与私有状态。</summary>
        internal MachineGunnerDamageScenario()
        {
            _combatants = new BattleCombatantsData();
            Player = _combatants.AddPlayer(1002, 30, 4);
            Enemy = _combatants.AddEnemy(2001, 30, 0);
            State = new MachineGunnerCombatState();
        }

        /// <summary>释放参与者响应式资源。</summary>
        public void Dispose()
        {
            _combatants.Dispose();
        }
    }
}
