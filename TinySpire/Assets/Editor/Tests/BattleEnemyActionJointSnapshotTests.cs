using System;
using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleEnemyActionJointSnapshotTests
{
    /// <summary>验证联合捕获零写入，并复用状态 module 形成 Block 与 Vulnerable 投影。</summary>
    [Test]
    public void Capture_FreezesAuthorityContinuationAndStatusProjectionsWithoutWrites()
    {
        using (var fixture = new JointFixture(weightedBehaviors: false))
        {
            ApplyBlock(fixture.Combatants, fixture.Enemy.Id, fixture.Enemy.Id, amount: 8);
            ApplyVulnerable(fixture.Combatants, fixture.Player.Id, fixture.Enemy.Id, amount: 2);
            int blockBefore = fixture.Enemy.CurrentBlock;
            int vulnerableBefore = fixture.Enemy.CurrentVulnerable;
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            uint randomBefore = fixture.Intents.RandomState;
            BattleEnemyIntentAuthoritySnapshot intentBefore =
                fixture.Intents.CaptureAuthoritySnapshot(fixture.Enemy.Id);
            var continuation = new CompleteEnemyActionCommand(fixture.Enemy.Id);
            var effectIds = new List<BattleEffectId> { new BattleEffectId(4002) };

            var snapshot = new BattleEnemyActionJointInitialSnapshot(
                fixture.Enemy,
                fixture.Player,
                fixture.Turn,
                fixture.Intents,
                effectIds,
                continuation);
            effectIds.Clear();

            Assert.That(fixture.Enemy.CurrentBlock, Is.EqualTo(blockBefore));
            Assert.That(fixture.Enemy.CurrentVulnerable, Is.EqualTo(vulnerableBefore));
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
            Assert.That(snapshot.Intent.History.LastCompletedBehaviorId,
                Is.EqualTo(intentBefore.History.LastCompletedBehaviorId));
            Assert.That(snapshot.Intent.History.ConsecutiveCompletedCount,
                Is.EqualTo(intentBefore.History.ConsecutiveCompletedCount));
            Assert.That(snapshot.Intent.History.CooldownsByBehaviorId,
                Is.EqualTo(intentBefore.History.CooldownsByBehaviorId));
            Assert.That(snapshot.SourceBeforeEffect.Block, Is.Zero);
            Assert.That(snapshot.SourceBeforeEffect.Vulnerable, Is.EqualTo(2));
            Assert.That(snapshot.EffectIds, Has.Count.EqualTo(1));
            Assert.That(snapshot.EffectIds[0], Is.EqualTo(new BattleEffectId(4002)));
            Assert.That(((IList<BattleEffectId>)snapshot.EffectIds).IsReadOnly, Is.True);

            BattleEffectTargetSnapshot afterAction = snapshot.ProjectSourceAfterEffect(
                new BattleEffectTargetSnapshot(health: 20, block: 5, vulnerable: 2));
            Assert.That(afterAction.Block, Is.EqualTo(5));
            Assert.That(afterAction.Vulnerable, Is.EqualTo(1));

            Assert.That(snapshot.Continuation.HasCommand, Is.True);
            Assert.That(snapshot.Continuation.Command, Is.Not.SameAs(continuation));
            Assert.That(snapshot.Continuation.Command, Is.TypeOf<CompleteEnemyActionCommand>());
            Assert.That(
                ((CompleteEnemyActionCommand)snapshot.Continuation.Command).EnemyId,
                Is.EqualTo(fixture.Enemy.Id));
        }
    }

    /// <summary>验证 source 任一标量漂移都会令唯一一次联合验证失败。</summary>
    [Test]
    public void ValidateInitial_WhenSourceDrifts_Fails()
    {
        using (var fixture = new JointFixture(weightedBehaviors: false))
        {
            BattleEnemyActionJointCommitGuard guard = fixture.CreateGuard();
            ApplyBlock(fixture.Combatants, fixture.Enemy.Id, fixture.Enemy.Id, amount: 1);

            Assert.That(fixture.Validate(guard), Is.False);
            Assert.That(guard.IsValidated, Is.False);
            Assert.That(() => fixture.Validate(guard), Throws.InvalidOperationException);
        }
    }

    /// <summary>验证显式 target 标量漂移会令联合验证失败。</summary>
    [Test]
    public void ValidateInitial_WhenTargetDrifts_Fails()
    {
        using (var fixture = new JointFixture(weightedBehaviors: false))
        {
            BattleEnemyActionJointCommitGuard guard = fixture.CreateGuard();
            ApplyVulnerable(fixture.Combatants, fixture.Enemy.Id, fixture.Player.Id, amount: 1);

            Assert.That(fixture.Validate(guard), Is.False);
        }
    }

    /// <summary>验证完整 Turn 快照会逐项拒绝阶段、轮次、行动者或玩家事实漂移。</summary>
    [Test]
    public void TurnSnapshot_MatchesEveryPublishedTurnFact()
    {
        using (var fixture = new JointFixture(weightedBehaviors: false))
        {
            var snapshot = new BattleTurnAuthoritySnapshot(fixture.Turn);

            Assert.That(snapshot.Matches(fixture.CreateTurn()), Is.True);
            Assert.That(snapshot.Matches(fixture.CreateTurn(phase: BattleTurnPhase.EnemyRoundEnd)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(roundNumber: 3)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(hasCurrentEnemy: false)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(energy: 2)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(energyMaximum: 2)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(energyGainPerRound: 2)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(ammo: 1)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(ammoMaximum: 1)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(ammoGainPerRound: 1)), Is.False);
            Assert.That(snapshot.Matches(fixture.CreateTurn(hasEndedAction: false)), Is.False);
            Assert.That(snapshot.Matches(new BattleTurnData(
                BattleTurnPhase.EnemyAction,
                2,
                new Dictionary<CombatantId, PlayerTurnData>(),
                fixture.Enemy.Id)), Is.False);
        }
    }

    /// <summary>验证固定行为推进时 behavior/random 不变，真实 history 与 Layout 发布漂移仍会被拒绝。</summary>
    [Test]
    public void ValidateInitial_WhenIntentHistoryAndLayoutDrift_Fails()
    {
        using (var fixture = new JointFixture(weightedBehaviors: false))
        {
            BattleEnemyActionJointCommitGuard guard = fixture.CreateGuard();
            BattleEnemyIntentAuthoritySnapshot before =
                fixture.Intents.CaptureAuthoritySnapshot(fixture.Enemy.Id);

            fixture.Intents.CompleteAndSelectNext(fixture.Enemy.Id);
            BattleEnemyIntentAuthoritySnapshot after =
                fixture.Intents.CaptureAuthoritySnapshot(fixture.Enemy.Id);

            Assert.That(after.CurrentBehaviorId, Is.EqualTo(before.CurrentBehaviorId));
            Assert.That(after.RandomState, Is.EqualTo(before.RandomState));
            Assert.That(after.Layout, Is.Not.SameAs(before.Layout));
            Assert.That(after.History.LastCompletedBehaviorId, Is.EqualTo(before.CurrentBehaviorId));
            Assert.That(after.History.ConsecutiveCompletedCount, Is.EqualTo(1));
            Assert.That(after.History.CooldownsByBehaviorId, Is.Empty);
            Assert.That(before.Matches(fixture.Intents), Is.False);
            Assert.That(fixture.Validate(guard), Is.False);
        }
    }

    /// <summary>验证加权下一意图推进随机流后，联合初始快照不再匹配。</summary>
    [Test]
    public void ValidateInitial_WhenIntentRandomDrifts_Fails()
    {
        using (var fixture = new JointFixture(weightedBehaviors: true))
        {
            BattleEnemyActionJointCommitGuard guard = fixture.CreateGuard();
            uint randomBefore = fixture.Intents.RandomState;

            fixture.Intents.CompleteAndSelectNext(fixture.Enemy.Id);

            Assert.That(fixture.Intents.RandomState, Is.Not.EqualTo(randomBefore));
            Assert.That(fixture.Validate(guard), Is.False);
        }
    }

    /// <summary>验证成功验证只允许一次提交，且提交阶段不会复验已经发生的中间事实变化。</summary>
    [Test]
    public void Commit_AfterSingleValidation_DoesNotRevalidateAndCannotRepeat()
    {
        using (var fixture = new JointFixture(weightedBehaviors: false))
        {
            BattleEnemyActionJointCommitGuard guard = fixture.CreateGuard();
            Assert.That(fixture.Validate(guard), Is.True);
            ApplyBlock(fixture.Combatants, fixture.Enemy.Id, fixture.Enemy.Id, amount: 3);
            int commitCount = 0;

            guard.Commit(() => commitCount++);

            Assert.That(commitCount, Is.EqualTo(1));
            Assert.That(guard.IsCommitted, Is.True);
            Assert.That(() => guard.Commit(() => commitCount++), Throws.InvalidOperationException);
            Assert.That(() => fixture.Validate(guard), Throws.InvalidOperationException);
            Assert.That(commitCount, Is.EqualTo(1));
        }
    }

    /// <summary>通过共享 Effect seam 建立非零 Block 事实。</summary>
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

    /// <summary>通过共享 Effect seam 建立非零 Vulnerable 事实。</summary>
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

    /// <summary>组合联合快照测试所需的最小权威事实并负责释放。</summary>
    private sealed class JointFixture : IDisposable
    {
        /// <summary>测试持有的参与者聚合。</summary>
        internal BattleCombatantsData Combatants { get; }

        /// <summary>测试持有的唯一玩家。</summary>
        internal PlayerCombatantData Player { get; }

        /// <summary>测试持有的行动敌人。</summary>
        internal EnemyCombatantData Enemy { get; }

        /// <summary>测试持有的权威意图聚合。</summary>
        internal BattleEnemyIntentsData Intents { get; }

        /// <summary>测试持有的初始 EnemyAction 回合事实。</summary>
        internal BattleTurnData Turn { get; }

        /// <summary>按固定或加权行为配置创建最小联合快照夹具。</summary>
        internal JointFixture(bool weightedBehaviors)
        {
            Combatants = new BattleCombatantsData();
            Player = Combatants.AddPlayer(templateId: 1001, maxHealth: 30, strength: 0);
            Enemy = Combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 0);
            Tables tables = CreateTables(weightedBehaviors);
            Intents = new BattleEnemyIntentsData(
                Combatants,
                new[] { Enemy.Id },
                tables,
                battleSeed: 1234);
            Turn = CreateTurn();
        }

        /// <summary>从当前权威事实创建新的联合验证 guard。</summary>
        internal BattleEnemyActionJointCommitGuard CreateGuard()
        {
            return new BattleEnemyActionJointCommitGuard(
                new BattleEnemyActionJointInitialSnapshot(
                    Enemy,
                    Player,
                    Turn,
                    Intents,
                    new[] { new BattleEffectId(4002) },
                    new CompleteEnemyActionCommand(Enemy.Id)));
        }

        /// <summary>使用夹具当前事实调用唯一一次联合验证。</summary>
        internal bool Validate(BattleEnemyActionJointCommitGuard guard)
        {
            return guard.ValidateInitial(Enemy, Player, Turn, Intents);
        }

        /// <summary>创建可逐项覆盖的完整回合事实。</summary>
        internal BattleTurnData CreateTurn(
            BattleTurnPhase phase = BattleTurnPhase.EnemyAction,
            int roundNumber = 2,
            int energy = 3,
            int energyMaximum = 3,
            int energyGainPerRound = 3,
            int ammo = 0,
            int ammoMaximum = 5,
            int ammoGainPerRound = 0,
            bool hasEndedAction = true,
            bool hasCurrentEnemy = true)
        {
            return new BattleTurnData(
                phase,
                roundNumber,
                new Dictionary<CombatantId, PlayerTurnData>
                {
                    [Player.Id] = new PlayerTurnData(
                        energy,
                        energyMaximum,
                        energyGainPerRound,
                        ammo,
                        ammoMaximum,
                        ammoGainPerRound,
                        hasEndedAction)
                },
                hasCurrentEnemy ? Enemy.Id : (CombatantId?)null);
        }

        /// <summary>释放意图响应式资源与参与者聚合。</summary>
        public void Dispose()
        {
            Intents.Dispose();
            Combatants.Dispose();
        }
    }

    /// <summary>创建固定或加权敌人所需的最小完整 Luban 表集合。</summary>
    private static Tables CreateTables(bool weightedBehaviors)
    {
        var behaviors = new JArray(
            CreateBehavior(7001, weight: weightedBehaviors ? 3 : 1, cooldownSelections: 0));
        var behaviorIds = new JArray(7001);
        if (weightedBehaviors)
        {
            behaviors.Add(CreateBehavior(7002, weight: 1, cooldownSelections: 0));
            behaviorIds.Add(7002);
        }

        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = new JArray(new JObject
            {
                ["id"] = 2001,
                ["name_i18n_key"] = "battle.enemy.joint.name",
                ["max_health"] = 20,
                ["base_strength"] = 0,
                ["view_prefab_key"] = "pfb_char_enemy",
                ["behavior_group_id"] = 6001
            }),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = new JArray(new JObject
            {
                ["id"] = 4002,
                ["effect_type"] = 1,
                ["attribute"] = 0,
                ["value"] = 6
            }),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = new JArray(new JObject
            {
                ["id"] = 6001,
                ["behavior_ids"] = behaviorIds
            }),
            ["battle_tbenemybehavior"] = behaviors,
            ["battle_tbcardupgradelevel"] = new JArray(),
        };
        return new Tables(tableName =>
            data.TryGetValue(tableName, out JArray rows) ? rows : new JArray());
    }

    /// <summary>创建一个带真实选择约束的敌人行为配置。</summary>
    private static JObject CreateBehavior(int behaviorId, int weight, int cooldownSelections)
    {
        return new JObject
        {
            ["id"] = behaviorId,
            ["intent_type"] = 0,
            ["target_rule"] = 1,
            ["effect_id"] = 4002,
            ["weight"] = weight,
            ["cooldown_selections"] = cooldownSelections,
            ["max_consecutive"] = 0
        };
    }
}
