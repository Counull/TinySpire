using System;
using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleCardPlayRulesTests
{
    /// <summary>每个用例结束后释放测试队列工厂代建的敌人意图资源。</summary>
    [TearDown]
    public void TearDown()
    {
        BattleCommandQueueTestFactory.DisposeOwnedEnemyIntents();
    }

    /// <summary>验证 Self 卡以玩家自身为目标时可出牌，且规则读取不写入任何权威事实。</summary>
    [Test]
    public void Evaluate_SelfTargetActor_ReturnsPlayableWithoutChangingFacts()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        Tables tables = CreateTables(cardCost: 1, targetRule: cfg.battle.TargetRule.Self);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            tables: tables);
        queue.SubmitRegistered(new StartBattleCommand());
        BattleTurnData turnBeforeEvaluation = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeEvaluation = zones.Layout.CurrentValue;
        uint shuffleRandomBeforeEvaluation = zones.ShuffleRandomState;
        var rules = new BattleCardPlayRules(
            combatants,
            cardZones,
            new[] { enemy.Id },
            tables);

        BattleCardPlayEvaluation evaluation = rules.Evaluate(
            turnBeforeEvaluation,
            new PlayCardCommand(player.Id, cardId, player.Id));

        Assert.That(evaluation.Succeeded, Is.True);
        Assert.That(evaluation.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(evaluation.TargetRule, Is.EqualTo(cfg.battle.TargetRule.Self));
        Assert.That(evaluation.CanStartInteraction, Is.True);
        Assert.That(evaluation.CanPayCost, Is.True);
        Assert.That(evaluation.LegalTargetIds, Is.EqualTo(new[] { player.Id }));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeEvaluation));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeEvaluation));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBeforeEvaluation));
        Assert.That(player.CurrentHealth, Is.EqualTo(30));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(20));

        queue.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证 Enemy 目标按 Encounter 顺序过滤死亡者，重复读取也不推进任何事实或随机流。</summary>
    [Test]
    public void Evaluate_EnemyTarget_DerivesStableLivingTargetsWithoutMutation()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
        EnemyCombatantData firstEnemy = combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
        EnemyCombatantData secondEnemy = combatants.AddEnemy(templateId: 202, maxHealth: 22, strength: 0);
        EnemyCombatantData deadEnemy = combatants.AddEnemy(templateId: 203, maxHealth: 24, strength: 0);
        BattleEffectStateTestDriver.Kill(combatants, player.Id, deadEnemy.Id);
        var enemyIds = new[] { secondEnemy.Id, deadEnemy.Id, firstEnemy.Id };
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones };
        Tables tables = CreateTables(cardCost: 1, targetRule: cfg.battle.TargetRule.Enemy);
        var enemyIntents = new BattleEnemyIntentsData(combatants, enemyIds, tables, battleSeed: 77);
        var presentation = new ControllableBattleCommandPresentation();
        BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            cardZones,
            enemyCombatantIdsInEncounterOrder: enemyIds,
            enemyIntents: enemyIntents,
            tables: tables);
        queue.SubmitRegistered(new StartBattleCommand());
        BattleTurnData turnBeforeEvaluation = queue.Turn.CurrentValue;
        CardZoneLayoutData layoutBeforeEvaluation = zones.Layout.CurrentValue;
        uint intentRandomBeforeEvaluation = enemyIntents.RandomState;
        var rules = new BattleCardPlayRules(combatants, cardZones, enemyIds, tables);
        var command = new PlayCardCommand(player.Id, cardId, firstEnemy.Id);

        BattleCardPlayEvaluation firstEvaluation = rules.Evaluate(turnBeforeEvaluation, command);
        BattleCardPlayEvaluation secondEvaluation = rules.Evaluate(turnBeforeEvaluation, command);

        Assert.That(firstEvaluation.Succeeded, Is.True);
        Assert.That(firstEvaluation.TargetRule, Is.EqualTo(cfg.battle.TargetRule.Enemy));
        Assert.That(firstEvaluation.LegalTargetIds, Is.EqualTo(new[] { secondEnemy.Id, firstEnemy.Id }));
        Assert.That(secondEvaluation.Succeeded, Is.True);
        Assert.That(secondEvaluation.LegalTargetIds, Is.EqualTo(firstEvaluation.LegalTargetIds));
        Assert.That(queue.Turn.CurrentValue, Is.SameAs(turnBeforeEvaluation));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBeforeEvaluation));
        Assert.That(enemyIntents.RandomState, Is.EqualTo(intentRandomBeforeEvaluation));
        Assert.That(deadEnemy.CurrentHealth, Is.Zero);

        queue.Dispose();
        enemyIntents.Dispose();
        zones.Dispose();
        combatants.Dispose();
    }

    /// <summary>验证命令保留可空目标，并在构造时拒绝结构无效的非空目标标识。</summary>
    [Test]
    public void PlayCardCommand_TargetId_IsNullableAndRejectsInvalidNonNullId()
    {
        using (var scenario = new RuleScenario(cfg.battle.TargetRule.Self))
        {
            var withoutTarget = new PlayCardCommand(
                scenario.Player.Id,
                scenario.CardId,
                targetId: null);

            Assert.That(withoutTarget.TargetId, Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, default(CombatantId)));
        }
    }

    /// <summary>验证 Enemy 的空、未知、死亡和玩家目标按稳定优先级失败并保留可用预览。</summary>
    [Test]
    public void Evaluate_EnemyInvalidTargets_ReturnStableReasonsWithoutMutation()
    {
        using (var scenario = new RuleScenario(cfg.battle.TargetRule.Enemy))
        {
            BattleEffectStateTestDriver.Kill(
                scenario.Combatants,
                scenario.Player.Id,
                scenario.SecondEnemy.Id);
            CombatantId unknownTargetId = CreateUnknownCombatantId();
            BattleTurnData turnBeforeEvaluation = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBeforeEvaluation = scenario.Zones.Layout.CurrentValue;

            BattleCardPlayEvaluation missing = scenario.Rules.Evaluate(
                turnBeforeEvaluation,
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, targetId: null));
            BattleCardPlayEvaluation unknown = scenario.Rules.Evaluate(
                turnBeforeEvaluation,
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, unknownTargetId));
            BattleCardPlayEvaluation dead = scenario.Rules.Evaluate(
                turnBeforeEvaluation,
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, scenario.SecondEnemy.Id));
            BattleCardPlayEvaluation wrongFaction = scenario.Rules.Evaluate(
                turnBeforeEvaluation,
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, scenario.Player.Id));

            Assert.That(missing.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetRequired));
            Assert.That(unknown.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotFound));
            Assert.That(dead.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.TargetNotAlive));
            Assert.That(
                wrongFaction.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(missing.CanStartInteraction, Is.True);
            Assert.That(missing.CanPayCost, Is.True);
            Assert.That(missing.LegalTargetIds, Is.EqualTo(new[] { scenario.FirstEnemy.Id }));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBeforeEvaluation));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBeforeEvaluation));
        }
    }

    /// <summary>验证 Self 指向其他参与者时返回目标规则不匹配且不写入卡区。</summary>
    [Test]
    public void Evaluate_SelfTargetOtherCombatant_ReturnsRuleMismatchWithoutMutation()
    {
        using (var scenario = new RuleScenario(cfg.battle.TargetRule.Self))
        {
            CardZoneLayoutData layoutBeforeEvaluation = scenario.Zones.Layout.CurrentValue;

            BattleCardPlayEvaluation evaluation = scenario.Rules.Evaluate(
                scenario.Queue.Turn.CurrentValue,
                new PlayCardCommand(
                    scenario.Player.Id,
                    scenario.CardId,
                    scenario.FirstEnemy.Id));

            Assert.That(
                evaluation.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.TargetRuleMismatch));
            Assert.That(evaluation.LegalTargetIds, Is.EqualTo(new[] { scenario.Player.Id }));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBeforeEvaluation));
        }
    }

    /// <summary>验证未知目标规则在目标缺失之前明确失败，不按 Enemy 或 Self 猜测。</summary>
    [Test]
    public void Evaluate_UnsupportedTargetRule_ReturnsExplicitFailure()
    {
        using (var scenario = new RuleScenario((cfg.battle.TargetRule)99))
        {
            BattleCardPlayEvaluation evaluation = scenario.Rules.Evaluate(
                scenario.Queue.Turn.CurrentValue,
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, targetId: null));

            Assert.That(
                evaluation.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.UnsupportedTargetRule));
            Assert.That(evaluation.TargetRule, Is.EqualTo((cfg.battle.TargetRule)99));
            Assert.That(evaluation.CanStartInteraction, Is.False);
            Assert.That(evaluation.CanPayCost, Is.True);
            Assert.That(evaluation.LegalTargetIds, Is.Empty);
        }
    }

    /// <summary>验证费用不足独立于目标错误，并明确派生不可支付与不可出牌。</summary>
    [Test]
    public void Evaluate_InsufficientEnergy_ReturnsCostFailureBeforeTargetRules()
    {
        using (var scenario = new RuleScenario(cfg.battle.TargetRule.Self, cardCost: 4))
        {
            BattleCardPlayEvaluation evaluation = scenario.Rules.Evaluate(
                scenario.Queue.Turn.CurrentValue,
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, targetId: null));

            Assert.That(
                evaluation.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.InsufficientEnergy));
            Assert.That(evaluation.TargetRule, Is.EqualTo(cfg.battle.TargetRule.Self));
            Assert.That(evaluation.CanStartInteraction, Is.False);
            Assert.That(evaluation.CanPayCost, Is.False);
        }
    }

    /// <summary>验证无存活敌人时只派生战斗不可继续，不创建胜负事实或写入快照。</summary>
    [Test]
    public void Evaluate_WhenAllEnemiesAreDead_ReturnsBattleAlreadyEndedWithoutMutation()
    {
        using (var scenario = new RuleScenario(cfg.battle.TargetRule.Self))
        {
            BattleEffectStateTestDriver.Kill(
                scenario.Combatants,
                scenario.Player.Id,
                scenario.FirstEnemy.Id);
            BattleEffectStateTestDriver.Kill(
                scenario.Combatants,
                scenario.Player.Id,
                scenario.SecondEnemy.Id);
            BattleTurnData turnBeforeEvaluation = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBeforeEvaluation = scenario.Zones.Layout.CurrentValue;

            BattleCardPlayEvaluation evaluation = scenario.Rules.Evaluate(
                turnBeforeEvaluation,
                new PlayCardCommand(scenario.Player.Id, scenario.CardId, scenario.Player.Id));

            Assert.That(
                evaluation.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.BattleAlreadyEnded));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBeforeEvaluation));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBeforeEvaluation));
            Assert.That(typeof(BattleTurnData).GetProperty("BattleOutcome"), Is.Null);
            Assert.That(typeof(BattleTurnData).GetProperty("BattleEnded"), Is.Null);
        }
    }

    /// <summary>从另一个聚合分配不会与当前最小场景碰撞的正数目标标识。</summary>
    private static CombatantId CreateUnknownCombatantId()
    {
        var externalCombatants = new BattleCombatantsData();
        CombatantId unknownId = default;
        for (int index = 0; index < 8; index++)
            unknownId = externalCombatants.AddPlayer(900 + index, 10, 0).Id;

        externalCombatants.Dispose();
        return unknownId;
    }

    /// <summary>创建一张测试卡与一名固定行为敌人所需的最小 Luban 表集合。</summary>
    private static Tables CreateTables(int cardCost, cfg.battle.TargetRule targetRule)
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":201,\"name_i18n_key\":\"battle.enemy.test_201.name\",\"max_health\":20,\"base_strength\":0,\"view_prefab_address\":\"\",\"behavior_group_id\":6001}," +
                "{\"id\":202,\"name_i18n_key\":\"battle.enemy.test_202.name\",\"max_health\":22,\"base_strength\":0,\"view_prefab_address\":\"\",\"behavior_group_id\":6001}," +
                "{\"id\":203,\"name_i18n_key\":\"battle.enemy.test_203.name\",\"max_health\":24,\"base_strength\":0,\"view_prefab_address\":\"\",\"behavior_group_id\":6001}]"),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray
            {
                new JObject
                {
                    ["id"] = 3001,
                    ["name_i18n_key"] = "battle.card.test.name",
                    ["description_i18n_key"] = "battle.card.test.description",
                    ["cost"] = cardCost,
                    ["target_rule"] = (int)targetRule,
                    ["effect_bindings"] = new JArray(),
                    ["illustration_key"] = string.Empty
                }
            },
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4999,\"effect_type\":1,\"attribute\":0,\"value\":1}]"),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":0}]")
        };

        return new Tables(tableName => data[tableName]);
    }

    private sealed class RuleScenario : IDisposable
    {
        /// <summary>当前场景的唯一参与者事实。</summary>
        internal BattleCombatantsData Combatants { get; }

        /// <summary>当前测试玩家。</summary>
        internal PlayerCombatantData Player { get; }

        /// <summary>Encounter 顺序中的第一名敌人。</summary>
        internal EnemyCombatantData FirstEnemy { get; }

        /// <summary>Encounter 顺序中的第二名敌人。</summary>
        internal EnemyCombatantData SecondEnemy { get; }

        /// <summary>当前玩家的唯一卡区事实。</summary>
        internal BattleCardZonesData Zones { get; }

        /// <summary>当前手牌中的测试卡牌实例。</summary>
        internal CardInstanceId CardId { get; }

        /// <summary>用于取得公开回合快照的权威队列。</summary>
        internal BattleCommandQueue Queue { get; }

        /// <summary>当前用例读取的纯出牌规则。</summary>
        internal BattleCardPlayRules Rules { get; }

        /// <summary>建立含一名玩家、两名敌人与一张测试卡的最小权威场景。</summary>
        internal RuleScenario(cfg.battle.TargetRule targetRule, int cardCost = 1)
        {
            Combatants = new BattleCombatantsData();
            Player = Combatants.AddPlayer(templateId: 101, maxHealth: 30, strength: 0);
            FirstEnemy = Combatants.AddEnemy(templateId: 201, maxHealth: 20, strength: 0);
            SecondEnemy = Combatants.AddEnemy(templateId: 202, maxHealth: 22, strength: 0);
            var enemyIds = new[] { FirstEnemy.Id, SecondEnemy.Id };
            Zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1234);
            Zones.Draw(1);
            CardId = Zones.Hand[0];
            var cardZones = new Dictionary<CombatantId, BattleCardZonesData> { [Player.Id] = Zones };
            Tables tables = CreateTables(cardCost, targetRule);
            var presentation = new ControllableBattleCommandPresentation();
            Queue = BattleCommandQueueTestFactory.Create(
                Combatants,
                presentation,
                cardZones,
                enemyCombatantIdsInEncounterOrder: enemyIds,
                tables: tables);
            Queue.SubmitRegistered(new StartBattleCommand());
            Rules = new BattleCardPlayRules(Combatants, cardZones, enemyIds, tables);
        }

        /// <summary>释放测试场景持有的队列、卡区和参与者响应式资源。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            Zones.Dispose();
            Combatants.Dispose();
        }
    }
}
