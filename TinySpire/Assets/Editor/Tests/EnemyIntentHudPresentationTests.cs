using System.Collections.Generic;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class EnemyIntentHudPresentationTests
{
    /// <summary>验证攻击意图值复用共享效果入口并叠加敌人的当前力量。</summary>
    [Test]
    public void DeriveEnemyIntent_AttackUsesEffectValueAndEnemyStrength()
    {
        Tables tables = CreateTables(
            new JArray(CreateBehavior(7001, intentType: 0, effectId: 4002, weight: 1)),
            new JArray(CreateGroup(6001, 7001)));
        var combatants = new BattleCombatantsData();
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 2);
        var intents = new BattleEnemyIntentsData(combatants, new[] { enemy.Id }, tables, battleSeed: 1234);

        try
        {
            EnemyIntentPresentationData presentation = ParticipantHudPresentation.DeriveEnemyIntent(
                intents.Layout.CurrentValue,
                tables,
                enemy);

            Assert.That(presentation.IntentType, Is.EqualTo(cfg.battle.EnemyIntentType.Attack));
            Assert.That(presentation.Value, Is.EqualTo(8));
            Assert.That(presentation.IsVisible, Is.True);
            Assert.That(ParticipantHudPresentation.FormatIntentValue(presentation.Value), Is.EqualTo("8"));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证非伤害意图只显示静态 Effect 数值，不错误叠加力量。</summary>
    [Test]
    public void DeriveEnemyIntent_DefendUsesConfiguredEffectValue()
    {
        Tables tables = CreateTables(
            new JArray(CreateBehavior(7002, intentType: 1, effectId: 4003, weight: 1, targetRule: 0)),
            new JArray(CreateGroup(6001, 7002)));
        var combatants = new BattleCombatantsData();
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 99);
        var intents = new BattleEnemyIntentsData(combatants, new[] { enemy.Id }, tables, battleSeed: 1234);

        try
        {
            EnemyIntentPresentationData presentation = ParticipantHudPresentation.DeriveEnemyIntent(
                intents.Layout.CurrentValue,
                tables,
                enemy);

            Assert.That(presentation.IntentType, Is.EqualTo(cfg.battle.EnemyIntentType.Defend));
            Assert.That(presentation.Value, Is.EqualTo(5));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证玩家和死亡敌人隐藏意图，存活敌人显示意图。</summary>
    [Test]
    public void ShouldShowIntent_OnlyReturnsTrueForLivingEnemy()
    {
        var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(templateId: 1001, maxHealth: 30, strength: 0);
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 0);

        Assert.That(ParticipantHudPresentation.ShouldShowIntent(player), Is.False);
        Assert.That(ParticipantHudPresentation.ShouldShowIntent(enemy), Is.True);

        BattleEffectStateTestDriver.Kill(combatants, player.Id, enemy.Id);

        Assert.That(ParticipantHudPresentation.ShouldShowIntent(enemy), Is.False);
        combatants.Dispose();
    }

    /// <summary>验证重复投影和等价 View 重建只重派生展示，不推进敌人随机流。</summary>
    [Test]
    public void RepeatedProjection_DoesNotAdvanceEnemyRandomState()
    {
        Tables tables = CreateTables(
            new JArray(
                CreateBehavior(7001, intentType: 0, effectId: 4002, weight: 1),
                CreateBehavior(7002, intentType: 1, effectId: 4003, weight: 1, targetRule: 0)),
            new JArray(CreateGroup(6001, 7001, 7002)));
        var combatants = new BattleCombatantsData();
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 1);
        var intents = new BattleEnemyIntentsData(combatants, new[] { enemy.Id }, tables, battleSeed: 2468);

        try
        {
            uint randomState = intents.RandomState;
            for (int index = 0; index < 20; index++)
            {
                EnemyIntentPresentationData presentation = ParticipantHudPresentation.DeriveEnemyIntent(
                    intents.Layout.CurrentValue,
                    tables,
                    enemy);
                Assert.That(presentation.Value, Is.GreaterThan(0));
            }

            Assert.That(intents.RandomState, Is.EqualTo(randomState));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证行动完成前后 HUD 投影始终跟随同一权威 BehaviorId。</summary>
    [Test]
    public void DeriveEnemyIntent_FollowsCurrentAndNextBehavior()
    {
        Tables tables = CreateTables(
            new JArray(
                CreateBehavior(7001, intentType: 0, effectId: 4002, weight: 1, maxConsecutive: 1),
                CreateBehavior(7002, intentType: 1, effectId: 4003, weight: 1, targetRule: 0, maxConsecutive: 1)),
            new JArray(CreateGroup(6001, 7001, 7002)));
        var combatants = new BattleCombatantsData();
        EnemyCombatantData enemy = combatants.AddEnemy(templateId: 2001, maxHealth: 20, strength: 2);
        var intents = new BattleEnemyIntentsData(combatants, new[] { enemy.Id }, tables, battleSeed: 9753);

        try
        {
            int currentBehaviorId = GetCurrentBehaviorId(intents, enemy.Id);
            EnemyIntentPresentationData currentPresentation = ParticipantHudPresentation.DeriveEnemyIntent(
                intents.Layout.CurrentValue,
                tables,
                enemy);
            AssertPresentationMatchesBehavior(currentPresentation, tables, currentBehaviorId, enemy);

            intents.CompleteAndSelectNext(enemy.Id);

            int nextBehaviorId = GetCurrentBehaviorId(intents, enemy.Id);
            EnemyIntentPresentationData nextPresentation = ParticipantHudPresentation.DeriveEnemyIntent(
                intents.Layout.CurrentValue,
                tables,
                enemy);
            Assert.That(nextBehaviorId, Is.Not.EqualTo(currentBehaviorId));
            AssertPresentationMatchesBehavior(nextPresentation, tables, nextBehaviorId, enemy);
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>创建含最小 Enemy、Effect、行为与行为组的完整 Luban 表集合。</summary>
    private static Tables CreateTables(JArray behaviors, JArray groups)
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test.name\",\"max_health\":20,\"base_strength\":0,\"view_prefab_address\":\"\",\"behavior_group_id\":6001}]"),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6},{\"id\":4003,\"effect_type\":2,\"attribute\":0,\"value\":5}]"),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = groups,
            ["battle_tbenemybehavior"] = behaviors
        };
        return new Tables(tableName => data[tableName]);
    }

    /// <summary>创建保留传入稳定顺序的行为组 JSON。</summary>
    private static JObject CreateGroup(int groupId, params int[] behaviorIds)
    {
        return new JObject
        {
            ["id"] = groupId,
            ["behavior_ids"] = new JArray(behaviorIds)
        };
    }

    /// <summary>创建供 HUD 投影使用的最小行为 JSON。</summary>
    private static JObject CreateBehavior(
        int behaviorId,
        int intentType,
        int effectId,
        int weight,
        int targetRule = 1,
        int maxConsecutive = 0)
    {
        return new JObject
        {
            ["id"] = behaviorId,
            ["intent_type"] = intentType,
            ["target_rule"] = targetRule,
            ["effect_id"] = effectId,
            ["weight"] = weight,
            ["cooldown_selections"] = 0,
            ["max_consecutive"] = maxConsecutive
        };
    }

    /// <summary>读取指定敌人的权威当前 BehaviorId。</summary>
    private static int GetCurrentBehaviorId(BattleEnemyIntentsData intents, CombatantId enemyId)
    {
        Assert.That(intents.Layout.CurrentValue.TryGetBehaviorId(enemyId, out int behaviorId), Is.True);
        return behaviorId;
    }

    /// <summary>验证投影类型与数值完全来自指定行为及共享效果计算入口。</summary>
    private static void AssertPresentationMatchesBehavior(
        EnemyIntentPresentationData presentation,
        Tables tables,
        int behaviorId,
        EnemyCombatantData enemy)
    {
        cfg.battle.EnemyBehavior behavior = tables.TbEnemyBehavior.Get(behaviorId);
        cfg.battle.CardEffect effect = tables.TbCardEffect.Get(behavior.EffectId);
        Assert.That(presentation.IntentType, Is.EqualTo(behavior.IntentType));
        Assert.That(
            presentation.Value,
            Is.EqualTo(BattleEffectValueCalculator.Calculate(effect, enemy)));
    }
}
