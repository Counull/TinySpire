using System;
using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleEnemyActionExecutorTests
{
    /// <summary>验证致死攻击在首写前冻结为空 continuation，提交时仍复用共享公式并推进下一意图。</summary>
    [Test]
    public void PrepareThenCommit_FatalAttack_DropsContinuationAndStillAdvancesIntent()
    {
        using (var fixture = EnemyActionFixture.CreateAttack(
                   playerHealth: 7,
                   enemyStrength: 1))
        {
            fixture.ApplyBlock(fixture.Player.Id, amount: 3);
            fixture.ApplyVulnerable(fixture.Player.Id, amount: 1);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;

            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                new CompleteEnemyActionCommand(fixture.Enemy.Id),
                startingOrder: 0);

            Assert.That(preparation.Kind, Is.EqualTo(BattleEnemyActionPreparationKind.Prepared));
            Assert.That(preparation.Plan.InitialSnapshot.Continuation.HasCommand, Is.False);
            Assert.That(preparation.Plan.Continuation.HasCommand, Is.False);
            Assert.That(fixture.Player.CurrentHealth, Is.EqualTo(7));
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Executor.ValidatePrepared(preparation.Plan, fixture.Turn), Is.True);
            BattleEnemyActionExecutionResult result =
                fixture.Executor.CommitPrepared(preparation.Plan);

            BattleEffectFormulaResult expected = BattleEffectFormula.Calculate(
                new BattleEffectFormulaContext(
                    BattleEffectOperationType.DealDamage,
                    configuredValue: 6,
                    sourceStrength: 1,
                    new BattleEffectTargetSnapshot(7, 3, 1)));
            Assert.That(result.Kind, Is.EqualTo(BattleEnemyActionResultKind.Succeeded));
            Assert.That(result.Settlements, Has.Count.EqualTo(2));
            var damage = result.Settlements[0] as BattleDamageAppliedSettlement;
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.AttackValue, Is.EqualTo(expected.DamageOutcome.Value.AttackValue));
            Assert.That(damage.BlockBefore, Is.EqualTo(3));
            Assert.That(damage.BlockAfter, Is.Zero);
            Assert.That(damage.HealthBefore, Is.EqualTo(7));
            Assert.That(damage.HealthAfter, Is.Zero);
            Assert.That(fixture.Player.CurrentHealth, Is.Zero);
            Assert.That(result.Settlements[1], Is.TypeOf<BattleEnemyIntentAdvancedSettlement>());
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.Not.SameAs(layoutBefore));
            Assert.That(result.Continuation.HasCommand, Is.False);
            AssertOrdersAreContinuous(result.Settlements);
        }
    }

    /// <summary>验证已有 source Block 会在攻击 Effect 前清零，且不会让 prepared 快照自判漂移。</summary>
    [Test]
    public void Execute_Attack_WithExistingSourceBlock_ClearsBeforeEffectWithoutSnapshotDrift()
    {
        using (var fixture = EnemyActionFixture.CreateAttack())
        {
            fixture.ApplyBlock(fixture.Enemy.Id, amount: 8);

            BattleEnemyActionExecutionResult result = fixture.Executor.Execute(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: 0);

            Assert.That(result.Kind, Is.EqualTo(BattleEnemyActionResultKind.Succeeded));
            Assert.That(result.Settlements, Has.Count.EqualTo(3));
            Assert.That(result.Settlements[0], Is.TypeOf<BattleBlockClearedSettlement>());
            Assert.That(result.Settlements[1], Is.TypeOf<BattleDamageAppliedSettlement>());
            Assert.That(result.Settlements[2], Is.TypeOf<BattleEnemyIntentAdvancedSettlement>());
            Assert.That(fixture.Enemy.CurrentBlock, Is.Zero);
            AssertOrdersAreContinuous(result.Settlements);
        }
    }

    /// <summary>验证 Self Defend 从 Block=0 投影执行，行动结束只衰减易伤，最终 Block 精确为 5。</summary>
    [Test]
    public void Execute_SelfDefend_ClearsExistingBlockAndFinishesAtFive()
    {
        using (var fixture = EnemyActionFixture.CreateDefend())
        {
            fixture.ApplyBlock(fixture.Enemy.Id, amount: 8);
            fixture.ApplyVulnerable(fixture.Enemy.Id, amount: 2);

            BattleEnemyActionExecutionResult result = fixture.Executor.Execute(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: 0);

            Assert.That(result.Kind, Is.EqualTo(BattleEnemyActionResultKind.Succeeded));
            Assert.That(result.Settlements, Has.Count.EqualTo(4));
            Assert.That(result.Settlements[0], Is.TypeOf<BattleBlockClearedSettlement>());
            var gained = result.Settlements[1] as BattleBlockGainedSettlement;
            Assert.That(gained, Is.Not.Null);
            Assert.That(gained.BlockBefore, Is.Zero);
            Assert.That(gained.BlockAfter, Is.EqualTo(5));
            Assert.That(result.Settlements[2], Is.TypeOf<BattleStatusReducedSettlement>());
            Assert.That(result.Settlements[3], Is.TypeOf<BattleEnemyIntentAdvancedSettlement>());
            Assert.That(fixture.Enemy.CurrentBlock, Is.EqualTo(5));
            Assert.That(fixture.Enemy.CurrentVulnerable, Is.EqualTo(1));
            AssertOrdersAreContinuous(result.Settlements);
        }
    }

    /// <summary>验证联合 Prepare 对参与者、Turn、Intent Layout/history/random 完全零写入。</summary>
    [Test]
    public void Prepare_ResolvedAction_IsZeroWriteAcrossAllAuthorityFacts()
    {
        using (var fixture = EnemyActionFixture.CreateAttack(weightedBehaviors: true))
        {
            fixture.ApplyBlock(fixture.Enemy.Id, amount: 4);
            fixture.ApplyVulnerable(fixture.Enemy.Id, amount: 2);
            var enemyBefore = new CombatantFactsSnapshot(fixture.Enemy);
            var playerBefore = new CombatantFactsSnapshot(fixture.Player);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            BattleEnemyIntentAuthoritySnapshot intentBefore =
                fixture.Intents.CaptureAuthoritySnapshot(fixture.Enemy.Id);
            uint randomBefore = fixture.Intents.RandomState;

            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                new CompleteEnemyActionCommand(fixture.Enemy.Id),
                startingOrder: 3);

            Assert.That(preparation.Kind, Is.EqualTo(BattleEnemyActionPreparationKind.Prepared));
            enemyBefore.AssertUnchanged();
            playerBefore.AssertUnchanged();
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
            Assert.That(intentBefore.Matches(fixture.Intents), Is.True);
            Assert.That(preparation.Settlements, Is.Empty);
            Assert.That(preparation.Plan, Is.Not.Null);
        }
    }

    /// <summary>验证联合计划只允许一次初始校验和一次提交，重复调用不会追加写入。</summary>
    [Test]
    public void ValidateThenCommit_CommitsPreparedActionAndIntentExactlyOnce()
    {
        using (var fixture = EnemyActionFixture.CreateAttack(weightedBehaviors: true))
        {
            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                new CompleteEnemyActionCommand(fixture.Enemy.Id),
                startingOrder: 2);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            uint randomBefore = fixture.Intents.RandomState;

            Assert.That(preparation.Plan.InitialSnapshot.Continuation.HasCommand, Is.True);
            Assert.That(preparation.Plan.Continuation.HasCommand, Is.True);
            Assert.That(fixture.Executor.ValidatePrepared(preparation.Plan, fixture.Turn), Is.True);
            Assert.That(() => fixture.Executor.ValidatePrepared(preparation.Plan, fixture.Turn),
                Throws.InvalidOperationException);
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));

            BattleEnemyActionExecutionResult result =
                fixture.Executor.CommitPrepared(preparation.Plan);

            Assert.That(result.Kind, Is.EqualTo(BattleEnemyActionResultKind.Succeeded));
            Assert.That(result.Continuation.HasCommand, Is.True);
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.Not.SameAs(layoutBefore));
            AssertOrdersAreContinuous(result.Settlements, startingOrder: 2);
            Assert.That(() => fixture.Executor.CommitPrepared(preparation.Plan),
                Throws.InvalidOperationException);
        }
    }

    /// <summary>验证缺 Behavior、缺 Effect、未知目标与未知 Effect 均在首次写入前形成结构化 fault。</summary>
    [TestCase(EnemyActionConfigurationFault.MissingBehavior, BattleCommandQueueFaultReason.MissingEnemyBehavior)]
    [TestCase(EnemyActionConfigurationFault.MissingEffect, BattleCommandQueueFaultReason.MissingEffect)]
    [TestCase(EnemyActionConfigurationFault.UnsupportedTargetRule, BattleCommandQueueFaultReason.UnsupportedConfiguration)]
    [TestCase(EnemyActionConfigurationFault.UnsupportedEffectType, BattleCommandQueueFaultReason.UnsupportedConfiguration)]
    public void Prepare_InvalidConfiguration_FaultsBeforeWrites(
        EnemyActionConfigurationFault fault,
        BattleCommandQueueFaultReason expectedReason)
    {
        using (var fixture = EnemyActionFixture.CreateAttack())
        {
            BattleEnemyActionExecutor executor = fixture.CreateExecutorWithConfigurationFault(fault);
            var enemyBefore = new CombatantFactsSnapshot(fixture.Enemy);
            var playerBefore = new CombatantFactsSnapshot(fixture.Player);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            uint randomBefore = fixture.Intents.RandomState;

            BattleEnemyActionPreparationResult preparation = executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: 0);

            Assert.That(preparation.Kind, Is.EqualTo(BattleEnemyActionPreparationKind.Faulted));
            Assert.That(preparation.FaultReason, Is.EqualTo(expectedReason));
            Assert.That(preparation.Settlements, Is.Empty);
            Assert.That(preparation.Plan, Is.Null);
            enemyBefore.AssertUnchanged();
            playerBefore.AssertUnchanged();
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证多存活玩家明确 fault，不私选目标且不写任何权威事实。</summary>
    [Test]
    public void Prepare_MultipleLivingPlayers_FaultsWithoutSelectingTarget()
    {
        using (var fixture = EnemyActionFixture.CreateAttack())
        {
            fixture.Combatants.AddPlayer(templateId: 1002, maxHealth: 25, strength: 0);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            uint randomBefore = fixture.Intents.RandomState;

            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: 0);

            Assert.That(preparation.Kind, Is.EqualTo(BattleEnemyActionPreparationKind.Faulted));
            Assert.That(preparation.FaultReason,
                Is.EqualTo(BattleCommandQueueFaultReason.MultipleLivingPlayers));
            Assert.That(preparation.Settlements, Is.Empty);
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证没有存活玩家时返回 BattleEnded，不读取 Effect 或推进意图。</summary>
    [Test]
    public void Prepare_NoLivingPlayer_ReturnsBattleEndedWithoutWrites()
    {
        using (var fixture = EnemyActionFixture.CreateAttack())
        {
            BattleEffectStateTestDriver.Kill(
                fixture.Combatants,
                fixture.Enemy.Id,
                fixture.Player.Id);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            uint randomBefore = fixture.Intents.RandomState;

            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: 0);

            Assert.That(preparation.Kind, Is.EqualTo(BattleEnemyActionPreparationKind.BattleEnded));
            Assert.That(preparation.Settlements, Is.Empty);
            Assert.That(preparation.FaultReason, Is.Null);
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证无下一意图候选在 Effect 首写前 fault，状态、历史、布局和随机均不变。</summary>
    [Test]
    public void Prepare_NoLegalNextIntent_FaultsWithoutWrites()
    {
        using (var fixture = EnemyActionFixture.CreateAttack(maxConsecutive: 1))
        {
            var enemyBefore = new CombatantFactsSnapshot(fixture.Enemy);
            var playerBefore = new CombatantFactsSnapshot(fixture.Player);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            BattleEnemyIntentAuthoritySnapshot authorityBefore =
                fixture.Intents.CaptureAuthoritySnapshot(fixture.Enemy.Id);
            uint randomBefore = fixture.Intents.RandomState;

            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: 0);

            Assert.That(preparation.Kind, Is.EqualTo(BattleEnemyActionPreparationKind.Faulted));
            Assert.That(preparation.FaultReason,
                Is.EqualTo(BattleCommandQueueFaultReason.NoLegalNextIntent));
            Assert.That(preparation.Settlements, Is.Empty);
            enemyBefore.AssertUnchanged();
            playerBefore.AssertUnchanged();
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
            Assert.That(authorityBefore.Matches(fixture.Intents), Is.True);
        }
    }

    /// <summary>验证联合记录序号无法容纳完整 Effect 与下一意图时结构化 fault，且不会先写任何事实。</summary>
    [Test]
    public void Prepare_SettlementOrderOverflow_FaultsWithoutWrites()
    {
        using (var fixture = EnemyActionFixture.CreateAttack())
        {
            var enemyBefore = new CombatantFactsSnapshot(fixture.Enemy);
            var playerBefore = new CombatantFactsSnapshot(fixture.Player);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            uint randomBefore = fixture.Intents.RandomState;

            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: int.MaxValue);

            Assert.That(preparation.Kind, Is.EqualTo(BattleEnemyActionPreparationKind.Faulted));
            Assert.That(preparation.FaultReason,
                Is.EqualTo(BattleCommandQueueFaultReason.PreparedInvariantViolation));
            Assert.That(preparation.Settlements, Is.Empty);
            Assert.That(preparation.Plan, Is.Null);
            enemyBefore.AssertUnchanged();
            playerBefore.AssertUnchanged();
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证 source、target、Turn 或 intent/random 漂移会锁死计划且不会追加任何事务写入。</summary>
    [TestCase(EnemyActionAuthorityDrift.Source)]
    [TestCase(EnemyActionAuthorityDrift.Target)]
    [TestCase(EnemyActionAuthorityDrift.Turn)]
    [TestCase(EnemyActionAuthorityDrift.IntentAndRandom)]
    public void ValidatePrepared_WhenInitialAuthorityDrifts_RejectsWithoutAdditionalWrites(
        EnemyActionAuthorityDrift drift)
    {
        using (var fixture = EnemyActionFixture.CreateAttack(weightedBehaviors: true))
        {
            BattleEnemyActionPreparationResult preparation = fixture.Executor.Prepare(
                fixture.Enemy.Id,
                fixture.Turn,
                plannedContinuation: null,
                startingOrder: 0);
            BattleTurnData currentTurn = fixture.ApplyDrift(drift);
            var enemyAfterDrift = new CombatantFactsSnapshot(fixture.Enemy);
            var playerAfterDrift = new CombatantFactsSnapshot(fixture.Player);
            EnemyIntentLayoutData layoutAfterDrift = fixture.Intents.Layout.CurrentValue;
            uint randomAfterDrift = fixture.Intents.RandomState;

            Assert.That(fixture.Executor.ValidatePrepared(preparation.Plan, currentTurn), Is.False);
            Assert.That(() => fixture.Executor.CommitPrepared(preparation.Plan),
                Throws.InvalidOperationException);
            enemyAfterDrift.AssertUnchanged();
            playerAfterDrift.AssertUnchanged();
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutAfterDrift));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomAfterDrift));
        }
    }

    /// <summary>验证死亡 source 在读取 Behavior、目标或 Intent 前成功跳过，只返回 source-only 记录。</summary>
    [TestCase(EnemyActionConfigurationFault.MissingBehavior)]
    [TestCase(EnemyActionConfigurationFault.MissingEffect)]
    [TestCase(EnemyActionConfigurationFault.UnsupportedTargetRule)]
    [TestCase(EnemyActionConfigurationFault.UnsupportedEffectType)]
    public void Execute_DeadSource_ReturnsSourceOnlySkipWithoutConsumingIntent(
        EnemyActionConfigurationFault configurationFault)
    {
        using (var fixture = EnemyActionFixture.CreateAttack())
        {
            BattleEffectStateTestDriver.Kill(
                fixture.Combatants,
                fixture.Player.Id,
                fixture.Enemy.Id);
            fixture.Enemy.ApplyBlockGain(amount: 4);
            fixture.Enemy.ApplyVulnerableGain(amount: 2);
            fixture.Combatants.AddPlayer(templateId: 1002, maxHealth: 25, strength: 0);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            uint randomBefore = fixture.Intents.RandomState;
            int blockBefore = fixture.Enemy.CurrentBlock;
            int vulnerableBefore = fixture.Enemy.CurrentVulnerable;
            BattleEnemyActionExecutor invalidConfigurationExecutor =
                fixture.CreateExecutorWithConfigurationFault(
                    configurationFault);

            BattleEnemyActionExecutionResult result = invalidConfigurationExecutor.Execute(
                fixture.Enemy.Id,
                fixture.Turn,
                new CompleteEnemyActionCommand(fixture.Enemy.Id),
                startingOrder: 6);

            Assert.That(result.Kind, Is.EqualTo(BattleEnemyActionResultKind.Succeeded));
            Assert.That(result.Settlements, Has.Count.EqualTo(1));
            var skipped = result.Settlements[0] as BattleEnemyActionSkippedSettlement;
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.Order, Is.EqualTo(6));
            Assert.That(skipped.SourceId, Is.EqualTo(fixture.Enemy.Id));
            Assert.That(skipped.TargetId, Is.Null);
            Assert.That(skipped.EffectId, Is.Null);
            Assert.That(skipped.Reason, Is.EqualTo(BattleEnemyActionSkipReason.SourceNotAlive));
            Assert.That(result.Continuation.HasCommand, Is.True);
            Assert.That(fixture.Enemy.CurrentBlock, Is.EqualTo(blockBefore));
            Assert.That(fixture.Enemy.CurrentVulnerable, Is.EqualTo(vulnerableBefore));
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
        }
    }

    /// <summary>验证终局、错误阶段、无效敌人和非当前行动者均返回普通失败且权威事实零写入。</summary>
    [TestCase(EnemyActionOrdinaryFailure.BattleEnded, BattleCommandExecutionFailureReason.BattleAlreadyEnded)]
    [TestCase(EnemyActionOrdinaryFailure.InvalidPhase, BattleCommandExecutionFailureReason.InvalidTurnPhase)]
    [TestCase(EnemyActionOrdinaryFailure.InvalidEnemy, BattleCommandExecutionFailureReason.InvalidEnemy)]
    [TestCase(EnemyActionOrdinaryFailure.WrongActor, BattleCommandExecutionFailureReason.EnemyNotCurrentActor)]
    public void Execute_InvalidTurnOrActor_FailsWithoutWrites(
        EnemyActionOrdinaryFailure failure,
        BattleCommandExecutionFailureReason expectedReason)
    {
        using (var fixture = EnemyActionFixture.CreateAttack())
        {
            CombatantId enemyId = fixture.Enemy.Id;
            BattleTurnData turn = fixture.Turn;
            switch (failure)
            {
                case EnemyActionOrdinaryFailure.BattleEnded:
                    turn = new BattleTurnData(
                        BattleTurnPhase.BattleEnded,
                        fixture.Turn.RoundNumber,
                        new Dictionary<CombatantId, PlayerTurnData>
                        {
                            [fixture.Player.Id] = fixture.Turn.Players[fixture.Player.Id],
                        },
                        currentActingEnemyId: null);
                    break;
                case EnemyActionOrdinaryFailure.InvalidPhase:
                    turn = new BattleTurnData(
                        BattleTurnPhase.PlayerAction,
                        fixture.Turn.RoundNumber,
                        new Dictionary<CombatantId, PlayerTurnData>
                        {
                            [fixture.Player.Id] = fixture.Turn.Players[fixture.Player.Id],
                        },
                        currentActingEnemyId: null);
                    break;
                case EnemyActionOrdinaryFailure.InvalidEnemy:
                    enemyId = new CombatantId(9999);
                    break;
                case EnemyActionOrdinaryFailure.WrongActor:
                    turn = new BattleTurnData(
                        BattleTurnPhase.EnemyAction,
                        fixture.Turn.RoundNumber,
                        new Dictionary<CombatantId, PlayerTurnData>
                        {
                            [fixture.Player.Id] = fixture.Turn.Players[fixture.Player.Id],
                        },
                        new CombatantId(9999));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }

            var enemyBefore = new CombatantFactsSnapshot(fixture.Enemy);
            var playerBefore = new CombatantFactsSnapshot(fixture.Player);
            EnemyIntentLayoutData layoutBefore = fixture.Intents.Layout.CurrentValue;
            BattleEnemyIntentAuthoritySnapshot intentBefore =
                fixture.Intents.CaptureAuthoritySnapshot(fixture.Enemy.Id);
            uint randomBefore = fixture.Intents.RandomState;

            BattleEnemyActionExecutionResult result = fixture.Executor.Execute(
                enemyId,
                turn,
                new CompleteEnemyActionCommand(fixture.Enemy.Id),
                startingOrder: 0);

            Assert.That(result.Kind, Is.EqualTo(BattleEnemyActionResultKind.Failed));
            Assert.That(result.FailureReason, Is.EqualTo(expectedReason));
            Assert.That(result.FaultReason, Is.Null);
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(result.Continuation.HasCommand, Is.False);
            enemyBefore.AssertUnchanged();
            playerBefore.AssertUnchanged();
            Assert.That(fixture.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fixture.Intents.RandomState, Is.EqualTo(randomBefore));
            Assert.That(intentBefore.Matches(fixture.Intents), Is.True);
        }
    }

    /// <summary>断言一组结算记录从指定起点开始连续编号。</summary>
    private static void AssertOrdersAreContinuous(
        IReadOnlyList<BattleSettlementRecord> settlements,
        int startingOrder = 0)
    {
        for (int index = 0; index < settlements.Count; index++)
            Assert.That(settlements[index].Order, Is.EqualTo(startingOrder + index));
    }

    public enum EnemyActionConfigurationFault
    {
        MissingBehavior,
        MissingEffect,
        UnsupportedTargetRule,
        UnsupportedEffectType,
    }

    public enum EnemyActionAuthorityDrift
    {
        Source,
        Target,
        Turn,
        IntentAndRandom,
    }

    public enum EnemyActionOrdinaryFailure
    {
        BattleEnded,
        InvalidPhase,
        InvalidEnemy,
        WrongActor,
    }

    /// <summary>组合单敌联合事务所需的最小运行时事实与静态表。</summary>
    private sealed class EnemyActionFixture : IDisposable
    {
        internal BattleCombatantsData Combatants { get; }
        internal PlayerCombatantData Player { get; }
        internal EnemyCombatantData Enemy { get; }
        internal Tables Tables { get; }
        internal BattleEnemyIntentsData Intents { get; }
        internal BattleTurnData Turn { get; }
        internal BattleEnemyActionExecutor Executor { get; }

        /// <summary>创建一份完整 fixture；调用方使用命名工厂选择行为类型。</summary>
        private EnemyActionFixture(
            cfg.battle.EffectType effectType,
            cfg.battle.TargetRule targetRule,
            int effectValue,
            int playerHealth,
            int enemyStrength,
            bool weightedBehaviors,
            int maxConsecutive)
        {
            Combatants = new BattleCombatantsData();
            Player = Combatants.AddPlayer(1001, playerHealth, strength: 0);
            Enemy = Combatants.AddEnemy(2001, maxHealth: 20, strength: enemyStrength);
            Tables = CreateTables(
                includeBehavior: true,
                includeEffect: true,
                effectType,
                targetRule,
                effectValue,
                weightedBehaviors,
                maxConsecutive);
            Intents = new BattleEnemyIntentsData(
                Combatants,
                new[] { Enemy.Id },
                Tables,
                battleSeed: 1234);
            Turn = CreateTurn();
            Executor = new BattleEnemyActionExecutor(Tables, Combatants, Intents);
        }

        /// <summary>创建 Enemy 目标的攻击行为 fixture。</summary>
        internal static EnemyActionFixture CreateAttack(
            int playerHealth = 30,
            int enemyStrength = 0,
            bool weightedBehaviors = false,
            int maxConsecutive = 0)
        {
            return new EnemyActionFixture(
                cfg.battle.EffectType.DealDamage,
                cfg.battle.TargetRule.Enemy,
                effectValue: 6,
                playerHealth,
                enemyStrength,
                weightedBehaviors,
                maxConsecutive);
        }

        /// <summary>创建 Self 目标的五点格挡行为 fixture。</summary>
        internal static EnemyActionFixture CreateDefend()
        {
            return new EnemyActionFixture(
                cfg.battle.EffectType.GainBlock,
                cfg.battle.TargetRule.Self,
                effectValue: 5,
                playerHealth: 30,
                enemyStrength: 0,
                weightedBehaviors: false,
                maxConsecutive: 0);
        }

        /// <summary>用同一参与者与意图事实创建指定错误静态配置的联合 executor。</summary>
        internal BattleEnemyActionExecutor CreateExecutorWithConfigurationFault(
            EnemyActionConfigurationFault fault)
        {
            bool includeBehavior = fault != EnemyActionConfigurationFault.MissingBehavior;
            bool includeEffect = fault != EnemyActionConfigurationFault.MissingEffect;
            cfg.battle.TargetRule targetRule =
                fault == EnemyActionConfigurationFault.UnsupportedTargetRule
                    ? (cfg.battle.TargetRule)99
                    : cfg.battle.TargetRule.Enemy;
            cfg.battle.EffectType effectType =
                fault == EnemyActionConfigurationFault.UnsupportedEffectType
                    ? (cfg.battle.EffectType)99
                    : cfg.battle.EffectType.DealDamage;
            Tables faultyTables = CreateTables(
                includeBehavior,
                includeEffect,
                effectType,
                targetRule,
                effectValue: 6,
                weightedBehaviors: false,
                maxConsecutive: 0);
            return new BattleEnemyActionExecutor(faultyTables, Combatants, Intents);
        }

        /// <summary>通过共享正式 Effect seam 为指定参与者增加 Block。</summary>
        internal void ApplyBlock(CombatantId targetId, int amount)
        {
            BattleEffectExecutionResult result = BattleEffectStateTestDriver.Execute(
                Combatants,
                Enemy.Id,
                targetId,
                cfg.battle.EffectType.GainBlock,
                cfg.battle.Attribute.None,
                amount);
            Assert.That(result.Succeeded, Is.True);
        }

        /// <summary>通过共享正式 Effect seam 为指定参与者增加 Vulnerable。</summary>
        internal void ApplyVulnerable(CombatantId targetId, int amount)
        {
            BattleEffectExecutionResult result = BattleEffectStateTestDriver.Execute(
                Combatants,
                Enemy.Id,
                targetId,
                cfg.battle.EffectType.ApplyVulnerable,
                cfg.battle.Attribute.None,
                amount);
            Assert.That(result.Succeeded, Is.True);
        }

        /// <summary>按测试类型制造单一权威漂移，并返回校验时应使用的当前 Turn。</summary>
        internal BattleTurnData ApplyDrift(EnemyActionAuthorityDrift drift)
        {
            switch (drift)
            {
                case EnemyActionAuthorityDrift.Source:
                    ApplyBlock(Enemy.Id, amount: 1);
                    return Turn;
                case EnemyActionAuthorityDrift.Target:
                    ApplyVulnerable(Player.Id, amount: 1);
                    return Turn;
                case EnemyActionAuthorityDrift.Turn:
                    return CreateTurn(energy: 2);
                case EnemyActionAuthorityDrift.IntentAndRandom:
                    Intents.CompleteAndSelectNext(Enemy.Id);
                    return Turn;
                default:
                    throw new ArgumentOutOfRangeException(nameof(drift));
            }
        }

        /// <summary>创建当前敌人行动阶段的完整 Turn 权威事实。</summary>
        private BattleTurnData CreateTurn(int energy = 3)
        {
            return new BattleTurnData(
                BattleTurnPhase.EnemyAction,
                roundNumber: 2,
                new Dictionary<CombatantId, PlayerTurnData>
                {
                    [Player.Id] = new PlayerTurnData(energy, hasEndedAction: true),
                },
                Enemy.Id);
        }

        /// <summary>释放意图与参与者响应式资源。</summary>
        public void Dispose()
        {
            Intents.Dispose();
            Combatants.Dispose();
        }
    }

    /// <summary>冻结一个参与者四项只读事实对象和值。</summary>
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

        /// <summary>捕获四项事实对象和值，供零写入断言。</summary>
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

        /// <summary>断言事实对象和值均保持冻结时状态。</summary>
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

    /// <summary>创建联合事务测试所需的完整最小 Luban Tables。</summary>
    private static Tables CreateTables(
        bool includeBehavior,
        bool includeEffect,
        cfg.battle.EffectType effectType,
        cfg.battle.TargetRule targetRule,
        int effectValue,
        bool weightedBehaviors,
        int maxConsecutive)
    {
        var behaviors = new JArray();
        var behaviorIds = new JArray();
        if (includeBehavior)
        {
            behaviors.Add(CreateBehavior(
                behaviorId: 7001,
                effectId: 4001,
                effectType,
                targetRule,
                weight: weightedBehaviors ? 3 : 1,
                maxConsecutive));
            behaviorIds.Add(7001);
            if (weightedBehaviors)
            {
                behaviors.Add(CreateBehavior(
                    behaviorId: 7002,
                    effectId: 4001,
                    effectType,
                    targetRule,
                    weight: 1,
                    maxConsecutive: 0));
                behaviorIds.Add(7002);
            }
        }

        var effects = new JArray();
        if (includeEffect)
        {
            effects.Add(new JObject
            {
                ["id"] = 4001,
                ["effect_type"] = (int)effectType,
                ["attribute"] = 0,
                ["value"] = effectValue,
            });
        }

        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = new JArray(new JObject
            {
                ["id"] = 2001,
                ["name_i18n_key"] = "battle.enemy.m8c.name",
                ["max_health"] = 20,
                ["base_strength"] = 0,
                ["view_prefab_key"] = "pfb_char_enemy",
                ["behavior_group_id"] = 6001,
            }),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = effects,
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = new JArray(new JObject
            {
                ["id"] = 6001,
                ["behavior_ids"] = behaviorIds,
            }),
            ["battle_tbenemybehavior"] = behaviors,
        };
        return new Tables(tableName => data[tableName]);
    }

    /// <summary>创建一条单 Effect 敌人行为配置。</summary>
    private static JObject CreateBehavior(
        int behaviorId,
        int effectId,
        cfg.battle.EffectType effectType,
        cfg.battle.TargetRule targetRule,
        int weight,
        int maxConsecutive)
    {
        cfg.battle.EnemyIntentType intentType =
            effectType == cfg.battle.EffectType.GainBlock
                ? cfg.battle.EnemyIntentType.Defend
                : cfg.battle.EnemyIntentType.Attack;
        return new JObject
        {
            ["id"] = behaviorId,
            ["intent_type"] = (int)intentType,
            ["target_rule"] = (int)targetRule,
            ["effect_id"] = effectId,
            ["weight"] = weight,
            ["cooldown_selections"] = 0,
            ["max_consecutive"] = maxConsecutive,
        };
    }
}
