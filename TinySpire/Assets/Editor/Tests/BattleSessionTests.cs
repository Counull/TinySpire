using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleSessionTests
{
    [Test]
    public void FromConfig_CreatesCombatantsAndInitialHandFromStaticTemplates()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001);

        BattleSession session = BattleSession.FromConfig(tables, new GameConfig(), options);

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
        Assert.That(session.CardZones.Hand.Count, Is.EqualTo(5));
        Assert.That(session.CardZones.DrawPile.Count, Is.EqualTo(5));
        Assert.That(session.CardZones.DiscardPile, Is.Empty);
        Assert.That(session.CardZones.ExhaustPile, Is.Empty);
    }

    [Test]
    public void FromConfig_WithTheSameSeed_CreatesTheSameInitialHand()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1001,
            encounterTemplateId: 5001,
            randomSeed: 2468);

        BattleSession first = BattleSession.FromConfig(tables, new GameConfig(), options);
        BattleSession second = BattleSession.FromConfig(tables, new GameConfig(), options);

        Assert.That(second.CardZones.Hand, Is.EqualTo(first.CardZones.Hand));
    }

    [Test]
    public void FromConfig_PreservesEncounterEnemyOrderAsCombatantIds()
    {
        Tables tables = CreateTables("[2002,2001,2002]");
        var options = new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001);

        BattleSession session = BattleSession.FromConfig(tables, new GameConfig(), options);

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
    }

    private static Tables CreateTables(string encounterEnemyTemplateIds = "[2001]")
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_address\":\"Assets/Arts/Runtime/Character/Prefabs/pfb_char_player.prefab\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001}]"),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"view_prefab_address\":\"Assets/Arts/Runtime/Character/Prefabs/pfb_char_enemy.prefab\",\"max_health\":20,\"base_strength\":0},{\"id\":2002,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"view_prefab_address\":\"Assets/Arts/Runtime/Character/Prefabs/pfb_char_enemy.prefab\",\"max_health\":20,\"base_strength\":0}]"),
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
                $"[{{\"id\":5001,\"enemy_template_ids\":{encounterEnemyTemplateIds}}}]")
        };

        return new Tables(tableName => data[tableName]);
    }
}
