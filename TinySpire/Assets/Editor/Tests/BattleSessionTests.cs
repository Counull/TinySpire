using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleSessionTests
{
    /// <summary>验证 Session 只创建参与者与洗牌后的未发牌卡区，不提前执行首轮抽牌。</summary>
    [Test]
    public void FromConfig_CreatesCombatantsAndUndealtDeckFromStaticTemplates()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001);

        BattleSession session = BattleSession.FromConfig(tables, options);

        Assert.That(session.Combatants.All.Count, Is.EqualTo(2));

        PlayerCombatantData player = null;
        EnemyCombatantData enemy = null;
        foreach (CombatantData combatant in session.Combatants.All.Values)
        {
            if (combatant is PlayerCombatantData playerCombatant)
                player = playerCombatant;
            else if (combatant is EnemyCombatantData enemyCombatant)
                enemy = enemyCombatant;
        }

        Assert.That(player, Is.Not.Null);
        Assert.That(player.TemplateId, Is.EqualTo(1001));
        Assert.That(player.MaxHealth, Is.EqualTo(80));
        Assert.That(player.Strength.CurrentValue, Is.EqualTo(1));
        Assert.That(enemy, Is.Not.Null);
        Assert.That(enemy.TemplateId, Is.EqualTo(2001));
        Assert.That(enemy.MaxHealth, Is.EqualTo(20));
        Assert.That(session.EnemyCombatantIdsInEncounterOrder, Is.EqualTo(new[] { enemy.Id }));

        Assert.That(session.CardZones.Cards.Count, Is.EqualTo(10));
        Assert.That(session.CardZones.Hand, Is.Empty);
        Assert.That(session.CardZones.DrawPile.Count, Is.EqualTo(10));
        Assert.That(session.CardZones.DiscardPile, Is.Empty);
        Assert.That(session.CardZones.ExhaustPile, Is.Empty);
        Assert.That(session.EnemyIntents.Layout.CurrentValue.TryGetBehaviorId(enemy.Id, out int behaviorId), Is.True);
        Assert.That(behaviorId, Is.EqualTo(7001));

        session.Dispose();
    }

    /// <summary>验证相同战斗种子产生完全相同的洗牌后抽牌堆。</summary>
    [Test]
    public void FromConfig_WithTheSameSeed_CreatesTheSameShuffledDrawPile()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1001,
            encounterTemplateId: 5001,
            randomSeed: 2468);

        BattleSession first = BattleSession.FromConfig(tables, options);
        BattleSession second = BattleSession.FromConfig(tables, options);

        Assert.That(second.CardZones.DrawPile, Is.EqualTo(first.CardZones.DrawPile));

        first.Dispose();
        second.Dispose();
    }

    /// <summary>验证 Session 保留 Encounter 配置顺序对应的敌人运行时标识。</summary>
    [Test]
    public void FromConfig_PreservesEncounterEnemyOrderAsCombatantIds()
    {
        Tables tables = CreateTables("[2002,2001,2002]");
        var options = new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001);

        BattleSession session = BattleSession.FromConfig(tables, options);

        Assert.That(session.EnemyCombatantIdsInEncounterOrder.Count, Is.EqualTo(3));
        Assert.That(
            session.Combatants.All[session.EnemyCombatantIdsInEncounterOrder[0]].TemplateId,
            Is.EqualTo(2002));
        Assert.That(
            session.Combatants.All[session.EnemyCombatantIdsInEncounterOrder[1]].TemplateId,
            Is.EqualTo(2001));
        Assert.That(
            session.Combatants.All[session.EnemyCombatantIdsInEncounterOrder[2]].TemplateId,
            Is.EqualTo(2002));
        foreach (CombatantId enemyId in session.EnemyCombatantIdsInEncounterOrder)
        {
            Assert.That(
                session.EnemyIntents.Layout.CurrentValue.TryGetBehaviorId(enemyId, out _),
                Is.True);
        }

        session.Dispose();
    }

    /// <summary>验证 Session 中相同种子的加权敌人序列一致，且意图推进不改变既有洗牌布局。</summary>
    [Test]
    public void FromConfig_WithTheSameSeed_CreatesTheSameWeightedIntentSequence()
    {
        Tables tables = CreateTables("[2002]");
        var options = new BattleSetupOptions(
            heroTemplateId: 1001,
            encounterTemplateId: 5001,
            randomSeed: 8642);
        BattleSession first = BattleSession.FromConfig(tables, options);
        BattleSession second = BattleSession.FromConfig(tables, options);

        try
        {
            IReadOnlyList<CardInstanceId> firstDrawPile = first.CardZones.DrawPile;
            IReadOnlyList<CardInstanceId> secondDrawPile = second.CardZones.DrawPile;
            CombatantId firstEnemyId = first.EnemyCombatantIdsInEncounterOrder[0];
            CombatantId secondEnemyId = second.EnemyCombatantIdsInEncounterOrder[0];

            for (int index = 0; index < 16; index++)
            {
                Assert.That(
                    GetCurrentBehaviorId(second.EnemyIntents, secondEnemyId),
                    Is.EqualTo(GetCurrentBehaviorId(first.EnemyIntents, firstEnemyId)));
                first.EnemyIntents.CompleteAndSelectNext(firstEnemyId);
                second.EnemyIntents.CompleteAndSelectNext(secondEnemyId);
            }

            Assert.That(second.EnemyIntents.RandomState, Is.EqualTo(first.EnemyIntents.RandomState));
            Assert.That(first.CardZones.DrawPile, Is.SameAs(firstDrawPile));
            Assert.That(second.CardZones.DrawPile, Is.SameAs(secondDrawPile));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    /// <summary>读取指定敌人的当前行为模板标识，并在测试数据缺失时立即失败。</summary>
    private static int GetCurrentBehaviorId(BattleEnemyIntentsData intents, CombatantId enemyId)
    {
        Assert.That(intents.Layout.CurrentValue.TryGetBehaviorId(enemyId, out int behaviorId), Is.True);
        return behaviorId;
    }

    /// <summary>创建包含测试英雄、牌组、卡牌与可配置敌人顺序的最小静态表。</summary>
    private static Tables CreateTables(string encounterEnemyTemplateIds = "[2001]")
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001}]"),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"view_prefab_key\":\"pfb_char_enemy\",\"max_health\":20,\"base_strength\":0,\"behavior_group_id\":6001},{\"id\":2002,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"view_prefab_key\":\"pfb_char_enemy\",\"max_health\":20,\"base_strength\":0,\"behavior_group_id\":6002}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002,3002,3002,3002,3002,3003,3003,3003,3003,3004]}]"),
            ["battle_tbcard"] = JArray.Parse(
                "[{\"id\":3002,\"name_i18n_key\":\"battle.card.strike.name\",\"description_i18n_key\":\"battle.card.strike.description\",\"cost\":1,\"target_rule\":1,\"effect_bindings\":[{\"argument_key\":\"damage\",\"effect_id\":4002}]},{" +
                "\"id\":3003,\"name_i18n_key\":\"battle.card.defend.name\",\"description_i18n_key\":\"battle.card.defend.description\",\"cost\":1,\"target_rule\":0,\"effect_bindings\":[{\"argument_key\":\"block\",\"effect_id\":4003}]},{" +
                "\"id\":3004,\"name_i18n_key\":\"battle.card.bash.name\",\"description_i18n_key\":\"battle.card.bash.description\",\"cost\":2,\"target_rule\":1,\"effect_bindings\":[{\"argument_key\":\"damage\",\"effect_id\":4004},{\"argument_key\":\"vulnerable\",\"effect_id\":4005}]}]"),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6},{" +
                "\"id\":4003,\"effect_type\":2,\"attribute\":0,\"value\":5},{" +
                "\"id\":4004,\"effect_type\":1,\"attribute\":0,\"value\":8},{" +
                "\"id\":4005,\"effect_type\":3,\"attribute\":0,\"value\":2}]"),
            ["battle_tbencounter"] = JArray.Parse(
                $"[{{\"id\":5001,\"enemy_template_ids\":{encounterEnemyTemplateIds}}}]"),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]},{\"id\":6002,\"behavior_ids\":[7002,7003]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4002,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":0},{\"id\":7002,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4002,\"weight\":3,\"cooldown_selections\":0,\"max_consecutive\":2},{\"id\":7003,\"intent_type\":1,\"target_rule\":0,\"effect_id\":4003,\"weight\":1,\"cooldown_selections\":1,\"max_consecutive\":1}]")
        };

        return new Tables(tableName => data[tableName]);
    }
}
