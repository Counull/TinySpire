using System;
using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;

public sealed class BattleEnemyIntentQueueTests
{
    /// <summary>验证合法完成先发布该敌人的下一意图，再推进到 Encounter 中下一名敌人。</summary>
    [Test]
    public void CompleteCurrentEnemy_AdvancesItsIntentBeforeEncounterOrder()
    {
        var presentation = new ControllableBattleCommandPresentation();
        using var context = new QueueTestContext(
            presentation,
            enemyCount: 2,
            noCandidate: false);
        context.Queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        context.Queue.Submit(new EndPlayerActionCommand(context.Player.Id));
        CombatantId firstEnemyId = context.EnemyIds[0];
        CombatantId secondEnemyId = context.EnemyIds[1];
        int firstBehaviorBefore = GetBehaviorId(context.Intents, firstEnemyId);
        int secondBehaviorBefore = GetBehaviorId(context.Intents, secondEnemyId);
        var publicationOrder = new List<string>();

        using IDisposable intentSubscription = context.Intents.Layout
            .Skip(1)
            .Subscribe(_ => publicationOrder.Add("intent"));
        using IDisposable turnSubscription = context.Queue.Turn
            .Skip(1)
            .Subscribe(_ => publicationOrder.Add("turn"));

        presentation.CompleteNext();

        Assert.That(publicationOrder, Is.EqualTo(new[] { "intent", "turn" }));
        Assert.That(presentation.Results[2].CommandType, Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(GetBehaviorId(context.Intents, firstEnemyId), Is.Not.EqualTo(firstBehaviorBefore));
        Assert.That(GetBehaviorId(context.Intents, secondEnemyId), Is.EqualTo(secondBehaviorBefore));
        Assert.That(context.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondEnemyId));
    }

    /// <summary>验证外部无法伪造错误或重复敌人完成命令，拒绝不分配序号且不写入权威事实。</summary>
    [Test]
    public void ExternalEnemyCompletion_IsRejectedWithoutAdvancingIntentOrTurn()
    {
        var presentation = new ControllableBattleCommandPresentation();
        using var context = new QueueTestContext(
            presentation,
            enemyCount: 2,
            noCandidate: false);
        CombatantId firstEnemyId = context.EnemyIds[0];
        CombatantId secondEnemyId = context.EnemyIds[1];

        context.Queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        EnemyIntentLayoutData playerPhaseLayout = context.Intents.Layout.CurrentValue;
        uint playerPhaseRandomState = context.Intents.RandomState;
        BattleTurnData playerPhaseTurn = context.Queue.Turn.CurrentValue;

        BattleCommandSubmissionResult playerPhaseRejection =
            context.Queue.Submit(new CompleteEnemyActionCommand(firstEnemyId));

        Assert.That(
            playerPhaseRejection.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.SystemCommandNotAuthorized));
        Assert.That(playerPhaseRejection.Accepted, Is.False);
        Assert.That(playerPhaseRejection.AuthoritySequence, Is.Null);
        Assert.That(context.Intents.Layout.CurrentValue, Is.SameAs(playerPhaseLayout));
        Assert.That(context.Intents.RandomState, Is.EqualTo(playerPhaseRandomState));
        Assert.That(context.Queue.Turn.CurrentValue, Is.SameAs(playerPhaseTurn));

        context.Queue.Submit(new EndPlayerActionCommand(context.Player.Id));
        EnemyIntentLayoutData enemyPhaseLayout = context.Intents.Layout.CurrentValue;
        uint enemyPhaseRandomState = context.Intents.RandomState;
        BattleTurnData enemyPhaseTurn = context.Queue.Turn.CurrentValue;
        BattleCommandSubmissionResult wrongEnemyRejection =
            context.Queue.Submit(new CompleteEnemyActionCommand(secondEnemyId));

        Assert.That(
            wrongEnemyRejection.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.SystemCommandNotAuthorized));
        Assert.That(wrongEnemyRejection.AuthoritySequence, Is.Null);
        Assert.That(context.Intents.Layout.CurrentValue, Is.SameAs(enemyPhaseLayout));
        Assert.That(context.Intents.RandomState, Is.EqualTo(enemyPhaseRandomState));
        Assert.That(context.Queue.Turn.CurrentValue, Is.SameAs(enemyPhaseTurn));
        Assert.That(context.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(firstEnemyId));
        Assert.That(context.Queue.Queue.CurrentValue.PendingCount, Is.EqualTo(1));

        presentation.CompleteNext();
        Assert.That(presentation.Results[2].Succeeded, Is.True);
        Assert.That(context.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(secondEnemyId));
        EnemyIntentLayoutData layoutAfterValidCompletion = context.Intents.Layout.CurrentValue;
        uint randomStateAfterValidCompletion = context.Intents.RandomState;
        BattleTurnData turnAfterValidCompletion = context.Queue.Turn.CurrentValue;

        BattleCommandSubmissionResult repeatedEnemyRejection =
            context.Queue.Submit(new CompleteEnemyActionCommand(firstEnemyId));

        Assert.That(
            repeatedEnemyRejection.FailureReason,
            Is.EqualTo(BattleCommandSubmissionFailureReason.SystemCommandNotAuthorized));
        Assert.That(repeatedEnemyRejection.AuthoritySequence, Is.Null);
        Assert.That(context.Intents.Layout.CurrentValue, Is.SameAs(layoutAfterValidCompletion));
        Assert.That(context.Intents.RandomState, Is.EqualTo(randomStateAfterValidCompletion));
        Assert.That(context.Queue.Turn.CurrentValue, Is.SameAs(turnAfterValidCompletion));
    }

    /// <summary>验证进入敌人阶段前死亡的敌人由 M4 顺序跳过，且不会为其补选下一意图。</summary>
    [Test]
    public void DeadEnemy_IsSkippedWithoutSelectingAnotherIntent()
    {
        var presentation = new ControllableBattleCommandPresentation();
        using var context = new QueueTestContext(
            presentation,
            enemyCount: 2,
            noCandidate: false);
        CombatantId deadEnemyId = context.EnemyIds[0];
        CombatantId livingEnemyId = context.EnemyIds[1];
        int deadBehaviorBefore = GetBehaviorId(context.Intents, deadEnemyId);
        EnemyIntentLayoutData initialLayout = context.Intents.Layout.CurrentValue;

        context.Queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        BattleEffectStateTestDriver.Kill(
            context.Combatants,
            context.Player.Id,
            deadEnemyId);
        context.Queue.Submit(new EndPlayerActionCommand(context.Player.Id));

        Assert.That(context.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(livingEnemyId));
        Assert.That(context.Intents.Layout.CurrentValue, Is.SameAs(initialLayout));
        Assert.That(GetBehaviorId(context.Intents, deadEnemyId), Is.EqualTo(deadBehaviorBefore));

        presentation.CompleteNext();

        Assert.That(GetBehaviorId(context.Intents, deadEnemyId), Is.EqualTo(deadBehaviorBefore));
        Assert.That(context.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));
        Assert.That(context.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
    }

    /// <summary>验证无合法下一候选时命令链显式停止，且意图与回合均保持在原敌人。</summary>
    [Test]
    public void NoCandidate_StopsBeforeTurnAdvancementWithoutPartialMutation()
    {
        using var context = new QueueTestContext(
            new ImmediateBattleCommandPresentation(),
            enemyCount: 1,
            noCandidate: true);
        CombatantId enemyId = context.EnemyIds[0];
        context.Queue.Submit(new StartBattleCommand());
        EnemyIntentLayoutData layoutBefore = context.Intents.Layout.CurrentValue;
        uint randomStateBefore = context.Intents.RandomState;
        BattleTurnData enemyTurnBeforeAction = null;
        var lifecycles = new List<BattleCommandLifecycleEvent>();
        using IDisposable turnSubscription = context.Queue.Turn
            .Skip(1)
            .Subscribe(turn =>
            {
                if (turn.Phase == BattleTurnPhase.EnemyAction)
                    enemyTurnBeforeAction = turn;
            });
        using IDisposable lifecycleSubscription = context.Coordinator.Lifecycle
            .Subscribe(lifecycles.Add);

        BattleCommandSubmissionResult endSubmission =
            context.Queue.Submit(new EndPlayerActionCommand(context.Player.Id));
        BattleCommandLifecycleEvent faulted = lifecycles.Find(
            lifecycle => lifecycle.Stage == BattleCommandLifecycleStage.Faulted);

        Assert.That(endSubmission.Accepted, Is.True);
        Assert.That(enemyTurnBeforeAction, Is.Not.Null);
        Assert.That(context.Queue.Turn.CurrentValue, Is.SameAs(enemyTurnBeforeAction));
        Assert.That(context.Queue.Turn.CurrentValue.CurrentActingEnemyId, Is.EqualTo(enemyId));
        Assert.That(context.Intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(context.Intents.RandomState, Is.EqualTo(randomStateBefore));
        Assert.That(faulted, Is.Not.Null);
        Assert.That(faulted.Fault, Is.SameAs(context.Queue.Queue.CurrentValue.Fault));
        Assert.That(
            faulted.Fault.Reason,
            Is.EqualTo(BattleCommandQueueFaultReason.NoLegalNextIntent));
        Assert.That(faulted.Fault.MayHavePartialWrites, Is.False);
        Assert.That(faulted.Settlements, Is.Empty);
        Assert.That(context.Queue.Queue.CurrentValue.IsFaulted, Is.True);
        Assert.That(
            context.Queue.Queue.CurrentValue.CurrentCommandType,
            Is.EqualTo(BattleCommandType.CompleteEnemyAction));
        Assert.That(
            context.Queue.Queue.CurrentValue.CurrentAuthoritySequence,
            Is.EqualTo(faulted.AuthoritySequence));
        Assert.That(context.Queue.Queue.CurrentValue.IsWaitingForPresentation, Is.False);
    }

    /// <summary>验证生产驱动只负责启动，Queue 自动 continuation 连续两轮各推进每名敌人一次。</summary>
    [Test]
    public void AutomaticContinuation_TwoRounds_AdvancesEachEnemyIntentOncePerRound()
    {
        using var context = new QueueTestContext(
            new ImmediateBattleCommandPresentation(),
            enemyCount: 2,
            noCandidate: false);
        using var replayContext = new QueueTestContext(
            new ImmediateBattleCommandPresentation(),
            enemyCount: 2,
            noCandidate: false);
        var driver = new BattleCommandRuntimeDriver(context.Queue);
        var replayDriver = new BattleCommandRuntimeDriver(replayContext.Queue);
        CombatantId firstEnemyId = context.EnemyIds[0];
        CombatantId secondEnemyId = context.EnemyIds[1];
        CombatantId replayFirstEnemyId = replayContext.EnemyIds[0];
        CombatantId replaySecondEnemyId = replayContext.EnemyIds[1];
        int firstInitialBehavior = GetBehaviorId(context.Intents, firstEnemyId);
        int secondInitialBehavior = GetBehaviorId(context.Intents, secondEnemyId);
        int playerInitialHealth = context.Player.CurrentHealth;
        int firstInitialHealth = context.Combatants.All[firstEnemyId].CurrentHealth;
        int secondInitialHealth = context.Combatants.All[secondEnemyId].CurrentHealth;
        Assert.That(
            GetBehaviorId(replayContext.Intents, replayFirstEnemyId),
            Is.EqualTo(firstInitialBehavior));
        Assert.That(
            GetBehaviorId(replayContext.Intents, replaySecondEnemyId),
            Is.EqualTo(secondInitialBehavior));

        driver.Start();
        replayDriver.Start();
        context.Queue.Submit(new EndPlayerActionCommand(context.Player.Id));
        replayContext.Queue.Submit(
            new EndPlayerActionCommand(replayContext.Player.Id));

        Assert.That(GetBehaviorId(context.Intents, firstEnemyId), Is.Not.EqualTo(firstInitialBehavior));
        Assert.That(GetBehaviorId(context.Intents, secondEnemyId), Is.Not.EqualTo(secondInitialBehavior));
        Assert.That(
            GetBehaviorId(replayContext.Intents, replayFirstEnemyId),
            Is.EqualTo(GetBehaviorId(context.Intents, firstEnemyId)));
        Assert.That(
            GetBehaviorId(replayContext.Intents, replaySecondEnemyId),
            Is.EqualTo(GetBehaviorId(context.Intents, secondEnemyId)));
        Assert.That(replayContext.Intents.RandomState, Is.EqualTo(context.Intents.RandomState));
        Assert.That(context.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(2));
        Assert.That(context.Queue.Turn.CurrentValue.Phase, Is.EqualTo(BattleTurnPhase.PlayerAction));

        context.Queue.Submit(new EndPlayerActionCommand(context.Player.Id));
        replayContext.Queue.Submit(
            new EndPlayerActionCommand(replayContext.Player.Id));

        Assert.That(GetBehaviorId(context.Intents, firstEnemyId), Is.EqualTo(firstInitialBehavior));
        Assert.That(GetBehaviorId(context.Intents, secondEnemyId), Is.EqualTo(secondInitialBehavior));
        Assert.That(
            GetBehaviorId(replayContext.Intents, replayFirstEnemyId),
            Is.EqualTo(firstInitialBehavior));
        Assert.That(
            GetBehaviorId(replayContext.Intents, replaySecondEnemyId),
            Is.EqualTo(secondInitialBehavior));
        Assert.That(replayContext.Intents.RandomState, Is.EqualTo(context.Intents.RandomState));
        Assert.That(context.Queue.Turn.CurrentValue.RoundNumber, Is.EqualTo(3));
        Assert.That(context.Player.CurrentHealth, Is.EqualTo(playerInitialHealth - 2));
        Assert.That(replayContext.Player.CurrentHealth, Is.EqualTo(context.Player.CurrentHealth));
        Assert.That(context.Combatants.All[firstEnemyId].CurrentHealth, Is.EqualTo(firstInitialHealth));
        Assert.That(context.Combatants.All[secondEnemyId].CurrentHealth, Is.EqualTo(secondInitialHealth));
        Assert.That(
            replayContext.Combatants.All[replayFirstEnemyId].CurrentHealth,
            Is.EqualTo(context.Combatants.All[firstEnemyId].CurrentHealth));
        Assert.That(
            replayContext.Combatants.All[replaySecondEnemyId].CurrentHealth,
            Is.EqualTo(context.Combatants.All[secondEnemyId].CurrentHealth));
        Assert.That(
            replayContext.Combatants.All[replayFirstEnemyId].CurrentBlock,
            Is.EqualTo(context.Combatants.All[firstEnemyId].CurrentBlock));
        Assert.That(
            replayContext.Combatants.All[replaySecondEnemyId].CurrentBlock,
            Is.EqualTo(context.Combatants.All[secondEnemyId].CurrentBlock));
    }

    /// <summary>读取指定敌人的权威当前行为标识。</summary>
    private static int GetBehaviorId(BattleEnemyIntentsData intents, CombatantId enemyId)
    {
        Assert.That(intents.Layout.CurrentValue.TryGetBehaviorId(enemyId, out int behaviorId), Is.True);
        return behaviorId;
    }

    /// <summary>持有一条可独立释放的最小 Session 等价命令接线，供 M8B Queue 语义测试使用。</summary>
    private sealed class QueueTestContext : IDisposable
    {
        /// <summary>测试中的权威参与者聚合。</summary>
        internal BattleCombatantsData Combatants { get; }

        /// <summary>测试中的唯一玩家。</summary>
        internal PlayerCombatantData Player { get; }

        /// <summary>按 Encounter 顺序创建的敌人标识。</summary>
        internal IReadOnlyList<CombatantId> EnemyIds { get; }

        /// <summary>测试中的权威敌人意图聚合。</summary>
        internal BattleEnemyIntentsData Intents { get; }

        /// <summary>测试中的统一权威命令队列。</summary>
        internal BattleCommandQueue Queue { get; }

        /// <summary>为测试提交预注册句柄并发布 Queue 生命周期的唯一协调器。</summary>
        internal BattleCommandSubmissionCoordinator Coordinator { get; }

        private readonly BattleCardZonesData _cardZones;

        /// <summary>创建固定数量敌人、最小表格、意图聚合与命令队列。</summary>
        internal QueueTestContext(
            IBattleCommandPresentation presentation,
            int enemyCount,
            bool noCandidate)
        {
            Combatants = new BattleCombatantsData();
            Player = Combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            var enemyIds = new List<CombatantId>(enemyCount);
            for (int index = 0; index < enemyCount; index++)
            {
                EnemyCombatantData enemy = Combatants.AddEnemy(
                    templateId: 201 + index,
                    maxHealth: 20 + index,
                    strength: 0);
                enemyIds.Add(enemy.Id);
            }

            EnemyIds = enemyIds.AsReadOnly();
            Tables tables = CreateTables(noCandidate);
            Intents = new BattleEnemyIntentsData(Combatants, EnemyIds, tables, battleSeed: 2468);
            _cardZones = new BattleCardZonesData(Array.Empty<int>(), shuffleSeed: 2468);
            Coordinator = new BattleCommandSubmissionCoordinator();
            Queue = new BattleCommandQueue(
                Combatants,
                new Dictionary<CombatantId, BattleCardZonesData> { [Player.Id] = _cardZones },
                EnemyIds,
                Intents,
                tables,
                energyPerRound: 3,
                initialHandCount: 0,
                presentation,
                Coordinator);
        }

        /// <summary>按命令队列、意图、卡区、参与者的依赖顺序释放测试资源。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            Coordinator.Dispose();
            Intents.Dispose();
            _cardZones.Dispose();
            Combatants.Dispose();
        }

        /// <summary>创建两名交替行为敌人，或单行为无候选敌人的最小 Luban 表。</summary>
        private static Tables CreateTables(bool noCandidate)
        {
            var data = new Dictionary<string, JArray>
            {
                ["battle_tbhero"] = new JArray(),
                ["battle_tbenemy"] = noCandidate
                    ? JArray.Parse(
                        "[{\"id\":201,\"name_i18n_key\":\"battle.enemy.test_201.name\",\"max_health\":20,\"base_strength\":0,\"view_prefab_key\":\"\",\"behavior_group_id\":6003}]")
                    : JArray.Parse(
                        "[{\"id\":201,\"name_i18n_key\":\"battle.enemy.test_201.name\",\"max_health\":20,\"base_strength\":0,\"view_prefab_key\":\"\",\"behavior_group_id\":6001},{\"id\":202,\"name_i18n_key\":\"battle.enemy.test_202.name\",\"max_health\":21,\"base_strength\":0,\"view_prefab_key\":\"\",\"behavior_group_id\":6002}]"),
                ["battle_tbdeck"] = new JArray(),
                ["battle_tbcard"] = new JArray(),
                ["battle_tbcardeffect"] = JArray.Parse(
                    "[{\"id\":4998,\"effect_type\":2,\"attribute\":0,\"value\":1},{\"id\":4999,\"effect_type\":1,\"attribute\":0,\"value\":1}]"),
                ["battle_tbencounter"] = new JArray(),
                ["battle_tbenemybehaviorgroup"] = noCandidate
                    ? JArray.Parse("[{\"id\":6003,\"behavior_ids\":[7005]}]")
                    : JArray.Parse(
                        "[{\"id\":6001,\"behavior_ids\":[7001,7002]},{\"id\":6002,\"behavior_ids\":[7003,7004]}]"),
                ["battle_tbenemybehavior"] = noCandidate
                    ? JArray.Parse(
                        "[{\"id\":7005,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":1}]")
                    : JArray.Parse(
                        "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":1},{\"id\":7002,\"intent_type\":1,\"target_rule\":0,\"effect_id\":4998,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":1},{\"id\":7003,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":1},{\"id\":7004,\"intent_type\":1,\"target_rule\":0,\"effect_id\":4998,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":1}]"),
                ["battle_tbcardupgradelevel"] = new JArray(),
            };
            return new Tables(tableName => data[tableName]);
        }
    }
}
