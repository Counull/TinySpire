using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Core;

/// <summary>机枪兵职业私有目标选择器的确定性与输入边界测试。</summary>
public sealed class MachineGunnerTargetSelectorTests
{
    /// <summary>验证最近、最远与全体目标都严格使用 Encounter 顺序，不依赖参与者字典枚举。</summary>
    [Test]
    public void Resolve_AutomaticModesFollowEncounterOrder()
    {
        using var scenario = new MachineGunnerTargetSelectorScenario();
        var random = new GameRandom(17u);

        MachineGunnerTargetSelectionResult nearest = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.NearestLivingEnemy,
            scenario.Player.Id,
            selectedTargetId: null,
            random);
        MachineGunnerTargetSelectionResult nearestTwo = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.NearestTwoLivingEnemies,
            scenario.Player.Id,
            selectedTargetId: null,
            random);
        MachineGunnerTargetSelectionResult furthest = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.FurthestLivingEnemy,
            scenario.Player.Id,
            selectedTargetId: null,
            random);
        MachineGunnerTargetSelectionResult all = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.AllLivingEnemies,
            scenario.Player.Id,
            selectedTargetId: null,
            random);

        Assert.That(nearest.Succeeded, Is.True);
        Assert.That(nearest.TargetIds, Is.EqualTo(new[] { scenario.Enemies[0].Id }));
        Assert.That(nearestTwo.Succeeded, Is.True);
        Assert.That(nearestTwo.TargetIds, Is.EqualTo(new[]
        {
            scenario.Enemies[0].Id,
            scenario.Enemies[1].Id,
        }));
        Assert.That(furthest.Succeeded, Is.True);
        Assert.That(furthest.TargetIds, Is.EqualTo(new[] { scenario.Enemies[2].Id }));
        Assert.That(all.Succeeded, Is.True);
        Assert.That(all.TargetIds, Is.EqualTo(new[]
        {
            scenario.Enemies[0].Id,
            scenario.Enemies[1].Id,
            scenario.Enemies[2].Id,
        }));

        Assert.That(scenario.Selector.TryGetLivingEnemyAt(1, out CombatantId secondNearest), Is.True);
        Assert.That(secondNearest, Is.EqualTo(scenario.Enemies[1].Id));

        Kill(scenario.Enemies[0]);
        MachineGunnerTargetSelectionResult nearestTwoAfterDeath = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.NearestTwoLivingEnemies,
            scenario.Player.Id,
            selectedTargetId: null,
            random);
        Assert.That(nearestTwoAfterDeath.TargetIds, Is.EqualTo(new[]
        {
            scenario.Enemies[1].Id,
            scenario.Enemies[2].Id,
        }));
    }

    /// <summary>验证相同种子的职业随机流在同一目标快照中产生相同序列和最终状态。</summary>
    [Test]
    public void Resolve_RandomModeWithSameSeedReplaysTargetSequence()
    {
        using var scenario = new MachineGunnerTargetSelectorScenario();
        var firstRandom = new GameRandom(91u);
        var secondRandom = new GameRandom(91u);
        var firstTargets = new List<CombatantId>();
        var secondTargets = new List<CombatantId>();

        for (int index = 0; index < 8; index++)
        {
            MachineGunnerTargetSelectionResult first = scenario.Selector.Resolve(
                MachineGunnerTargetSelectionMode.RandomLivingEnemy,
                scenario.Player.Id,
                selectedTargetId: null,
                firstRandom);
            MachineGunnerTargetSelectionResult second = scenario.Selector.Resolve(
                MachineGunnerTargetSelectionMode.RandomLivingEnemy,
                scenario.Player.Id,
                selectedTargetId: null,
                secondRandom);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            firstTargets.Add(first.TargetIds[0]);
            secondTargets.Add(second.TargetIds[0]);
        }

        Assert.That(firstTargets, Is.EqualTo(secondTargets));
        Assert.That(firstRandom.State, Is.EqualTo(secondRandom.State));
    }

    /// <summary>验证伪造随机目标在消耗职业随机流前被拒绝，随后正常随机仍可与基线重放一致。</summary>
    [Test]
    public void Resolve_RandomModeRejectsProvidedTargetWithoutAdvancingRandom()
    {
        using var scenario = new MachineGunnerTargetSelectorScenario();
        var actualRandom = new GameRandom(37u);
        var baselineRandom = new GameRandom(37u);

        MachineGunnerTargetSelectionResult rejected = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.RandomLivingEnemy,
            scenario.Player.Id,
            scenario.Enemies[0].Id,
            actualRandom);
        MachineGunnerTargetSelectionResult actual = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.RandomLivingEnemy,
            scenario.Player.Id,
            selectedTargetId: null,
            actualRandom);
        MachineGunnerTargetSelectionResult baseline = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.RandomLivingEnemy,
            scenario.Player.Id,
            selectedTargetId: null,
            baselineRandom);

        Assert.That(rejected.Succeeded, Is.False);
        Assert.That(rejected.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
        Assert.That(actual.Succeeded, Is.True);
        Assert.That(actual.TargetIds, Is.EqualTo(baseline.TargetIds));
        Assert.That(actualRandom.State, Is.EqualTo(baselineRandom.State));
    }

    /// <summary>验证不存在存活目标时随机模式不会推进职业随机流，死亡敌人也不会作为候选被重放。</summary>
    [Test]
    public void Resolve_RandomModeWithoutLivingEnemies_DoesNotAdvanceRandom()
    {
        using var scenario = new MachineGunnerTargetSelectorScenario();
        var random = new GameRandom(73u);
        uint stateBefore = random.State;

        foreach (EnemyCombatantData enemy in scenario.Enemies)
            Kill(enemy);

        MachineGunnerTargetSelectionResult result = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.RandomLivingEnemy,
            scenario.Player.Id,
            selectedTargetId: null,
            random);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotAlive));
        Assert.That(random.State, Is.EqualTo(stateBefore));
        Assert.That(scenario.Selector.TryGetLivingEnemyAt(0, out _), Is.False);
    }

    /// <summary>验证显式目标只接受存活敌人，并将不存在与阵营不匹配区分为稳定失败。</summary>
    [Test]
    public void Resolve_PlayerSelectedEnemyValidatesTargetIdentityAndFaction()
    {
        using var scenario = new MachineGunnerTargetSelectorScenario();
        var random = new GameRandom(7u);

        MachineGunnerTargetSelectionResult selected = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.PlayerSelectedEnemy,
            scenario.Player.Id,
            scenario.Enemies[1].Id,
            random);
        MachineGunnerTargetSelectionResult playerTarget = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.PlayerSelectedEnemy,
            scenario.Player.Id,
            scenario.Player.Id,
            random);
        MachineGunnerTargetSelectionResult missingTarget = scenario.Selector.Resolve(
            MachineGunnerTargetSelectionMode.PlayerSelectedEnemy,
            scenario.Player.Id,
            new CombatantId(9999),
            random);

        Assert.That(selected.Succeeded, Is.True);
        Assert.That(selected.TargetIds, Is.EqualTo(new[] { scenario.Enemies[1].Id }));
        Assert.That(playerTarget.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotAlive));
        Assert.That(missingTarget.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotFound));
    }

    /// <summary>使用统一伤害公式把测试敌人推进到零生命，避免绕开参与者权威写入入口。</summary>
    private static void Kill(EnemyCombatantData enemy)
    {
        BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
            new BattleEffectFormulaContext(
                BattleEffectOperationType.DealDamage,
                enemy.CurrentHealth,
                sourceStrength: 0,
                new BattleEffectTargetSnapshot(
                    enemy.CurrentHealth,
                    enemy.CurrentBlock,
                    enemy.CurrentVulnerable)));
        enemy.ApplyDamageOutcome(formula.DamageOutcome.Value);
    }

    /// <summary>构造并释放目标选择器需要的唯一参与者和 Encounter 顺序事实。</summary>
    private sealed class MachineGunnerTargetSelectorScenario : System.IDisposable
    {
        /// <summary>本场唯一参与者事实所有者。</summary>
        private readonly BattleCombatantsData _combatants;

        /// <summary>供目标模式传入的玩家参与者。</summary>
        internal PlayerCombatantData Player { get; }

        /// <summary>按 Encounter 顺序创建的三名活敌人。</summary>
        internal IReadOnlyList<EnemyCombatantData> Enemies { get; }

        /// <summary>被测职业私有选择器。</summary>
        internal MachineGunnerTargetSelector Selector { get; }

        /// <summary>创建固定 Encounter 顺序的最小场景。</summary>
        internal MachineGunnerTargetSelectorScenario()
        {
            _combatants = new BattleCombatantsData();
            Player = _combatants.AddPlayer(1001, 30, 0);
            var enemies = new[]
            {
                _combatants.AddEnemy(2003, 10, 0),
                _combatants.AddEnemy(2001, 10, 0),
                _combatants.AddEnemy(2002, 10, 0),
            };
            Enemies = enemies;
            Selector = new MachineGunnerTargetSelector(
                _combatants,
                new[] { enemies[0].Id, enemies[1].Id, enemies[2].Id });
        }

        /// <summary>释放参与者响应式资源。</summary>
        public void Dispose()
        {
            _combatants.Dispose();
        }
    }
}
