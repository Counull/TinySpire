using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using cfg;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;

public sealed class BattleEnemyIntentsDataTests
{
    /// <summary>验证 ConfigService 会预加载两张新增的敌人行为配置表。</summary>
    [Test]
    public void ConfigServiceTableNames_ContainEnemyBehaviorTables()
    {
        FieldInfo tableNamesField = typeof(ConfigService).GetField(
            "TableNames",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(tableNamesField, Is.Not.Null);
        var tableNames = (string[])tableNamesField.GetValue(null);
        Assert.That(tableNames, Does.Contain("battle_tbenemybehaviorgroup"));
        Assert.That(tableNames, Does.Contain("battle_tbenemybehavior"));
    }

    /// <summary>验证固定行为在初始选择和行动完成后都不推进敌人随机流。</summary>
    [Test]
    public void FixedBehavior_AlwaysRemainsSelectedWithoutAdvancingRandomState()
    {
        Tables tables = CreateTables(
            new JArray(CreateEnemy(2001, 6001)),
            new JArray(CreateGroup(6001, 7001)),
            new JArray(CreateBehavior(7001, weight: 1)));
        BattleEnemyIntentsData intents = CreateIntents(
            tables,
            battleSeed: 1234,
            out BattleCombatantsData combatants,
            out IReadOnlyList<CombatantId> enemyIds,
            2001);

        try
        {
            uint randomState = intents.RandomState;
            Assert.That(GetCurrentBehaviorId(intents, enemyIds[0]), Is.EqualTo(7001));

            for (int index = 0; index < 5; index++)
            {
                intents.CompleteAndSelectNext(enemyIds[0]);
                Assert.That(GetCurrentBehaviorId(intents, enemyIds[0]), Is.EqualTo(7001));
                Assert.That(intents.RandomState, Is.EqualTo(randomState));
            }
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证相同种子、配置与 Encounter 顺序得到完全相同的加权行为序列。</summary>
    [Test]
    public void WeightedBehaviors_WithTheSameSeed_ProduceTheSameSequence()
    {
        Tables tables = CreateWeightedTables();
        BattleEnemyIntentsData first = CreateIntents(
            tables,
            battleSeed: 2468,
            out BattleCombatantsData firstCombatants,
            out IReadOnlyList<CombatantId> firstEnemyIds,
            2002);
        BattleEnemyIntentsData second = CreateIntents(
            tables,
            battleSeed: 2468,
            out BattleCombatantsData secondCombatants,
            out IReadOnlyList<CombatantId> secondEnemyIds,
            2002);

        try
        {
            IReadOnlyList<int> firstSequence = ReadSequence(first, firstEnemyIds[0], 24);
            IReadOnlyList<int> secondSequence = ReadSequence(second, secondEnemyIds[0], 24);

            Assert.That(secondSequence, Is.EqualTo(firstSequence));
            Assert.That(first.RandomState, Is.EqualTo(second.RandomState));
            Assert.That(firstSequence.Distinct().Count(), Is.EqualTo(2));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
            firstCombatants.Dispose();
            secondCombatants.Dispose();
        }
    }

    /// <summary>验证洗牌和敌人行为使用两个互不推进的确定性随机实例。</summary>
    [Test]
    public void EnemyBehaviorAndShuffleRandomStreams_DoNotAdvanceEachOther()
    {
        Tables tables = CreateWeightedTables();
        BattleEnemyIntentsData intents = CreateIntents(
            tables,
            battleSeed: 9753,
            out BattleCombatantsData combatants,
            out IReadOnlyList<CombatantId> enemyIds,
            2002);
        var cardZones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004, 3005, 3006 },
            shuffleSeed: 9753);

        try
        {
            uint intentStateBeforeReshuffle = intents.RandomState;
            cardZones.Draw(6);
            cardZones.DiscardHand();
            uint shuffleStateBeforeReshuffle = cardZones.ShuffleRandomState;
            cardZones.Draw(1);

            Assert.That(cardZones.ShuffleRandomState, Is.Not.EqualTo(shuffleStateBeforeReshuffle));
            Assert.That(intents.RandomState, Is.EqualTo(intentStateBeforeReshuffle));

            uint shuffleStateBeforeIntentSelection = cardZones.ShuffleRandomState;
            uint intentStateBeforeSelection = intents.RandomState;
            intents.CompleteAndSelectNext(enemyIds[0]);

            Assert.That(intents.RandomState, Is.Not.EqualTo(intentStateBeforeSelection));
            Assert.That(cardZones.ShuffleRandomState, Is.EqualTo(shuffleStateBeforeIntentSelection));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
            cardZones.Dispose();
        }
    }

    /// <summary>验证读取当前意图、遍历映射和订阅快照都不会推进随机流。</summary>
    [Test]
    public void ReadingCurrentIntent_DoesNotAdvanceRandomState()
    {
        Tables tables = CreateWeightedTables();
        BattleEnemyIntentsData intents = CreateIntents(
            tables,
            battleSeed: 8642,
            out BattleCombatantsData combatants,
            out IReadOnlyList<CombatantId> enemyIds,
            2002);

        try
        {
            uint randomState = intents.RandomState;
            int subscribedBehaviorId = 0;
            using IDisposable subscription = intents.Layout.Subscribe(layout =>
                layout.TryGetBehaviorId(enemyIds[0], out subscribedBehaviorId));
            for (int index = 0; index < 20; index++)
            {
                Assert.That(intents.Layout.CurrentValue.BehaviorIdsByEnemy.Count, Is.EqualTo(1));
                Assert.That(intents.Layout.CurrentValue.TryGetBehaviorId(enemyIds[0], out int behaviorId), Is.True);
                Assert.That(behaviorId, Is.EqualTo(7002).Or.EqualTo(7003));
                Assert.That(subscribedBehaviorId, Is.EqualTo(behaviorId));
            }

            Assert.That(intents.RandomState, Is.EqualTo(randomState));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证冷却跳过次数与最大连续次数共同产生预期的最小序列。</summary>
    [Test]
    public void CooldownAndMaxConsecutive_ConstrainFutureSelections()
    {
        Tables tables = CreateTables(
            new JArray(CreateEnemy(2001, 6001)),
            new JArray(CreateGroup(6001, 7001, 7002)),
            new JArray(
                CreateBehavior(7001, weight: 1, cooldownSelections: 2),
                CreateBehavior(7002, weight: 1, maxConsecutive: 2)));
        BattleEnemyIntentsData intents = CreateIntents(
            tables,
            battleSeed: 1357,
            out BattleCombatantsData combatants,
            out IReadOnlyList<CombatantId> enemyIds,
            2001);

        try
        {
            CombatantId enemyId = enemyIds[0];
            for (int attempts = 0; attempts < 3 && GetCurrentBehaviorId(intents, enemyId) != 7001; attempts++)
                intents.CompleteAndSelectNext(enemyId);

            Assert.That(GetCurrentBehaviorId(intents, enemyId), Is.EqualTo(7001));
            intents.CompleteAndSelectNext(enemyId);
            Assert.That(GetCurrentBehaviorId(intents, enemyId), Is.EqualTo(7002));
            intents.CompleteAndSelectNext(enemyId);
            Assert.That(GetCurrentBehaviorId(intents, enemyId), Is.EqualTo(7002));
            intents.CompleteAndSelectNext(enemyId);
            Assert.That(GetCurrentBehaviorId(intents, enemyId), Is.EqualTo(7001));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证无合法候选时意图快照、历史与随机状态都保持不变。</summary>
    [Test]
    public void NoLegalCandidate_FailsWithoutPartialMutation()
    {
        Tables tables = CreateTables(
            new JArray(CreateEnemy(2001, 6001)),
            new JArray(CreateGroup(6001, 7001, 7002)),
            new JArray(
                CreateBehavior(7001, weight: 1, maxConsecutive: 1),
                CreateBehavior(7002, weight: 1, cooldownSelections: 2, maxConsecutive: 1)));
        BattleEnemyIntentsData intents = CreateIntents(
            tables,
            battleSeed: 4321,
            out BattleCombatantsData combatants,
            out IReadOnlyList<CombatantId> enemyIds,
            2001);

        try
        {
            CombatantId enemyId = enemyIds[0];
            if (GetCurrentBehaviorId(intents, enemyId) == 7001)
                intents.CompleteAndSelectNext(enemyId);

            Assert.That(GetCurrentBehaviorId(intents, enemyId), Is.EqualTo(7002));
            intents.CompleteAndSelectNext(enemyId);
            Assert.That(GetCurrentBehaviorId(intents, enemyId), Is.EqualTo(7001));

            EnemyIntentLayoutData layoutBefore = intents.Layout.CurrentValue;
            uint randomStateBefore = intents.RandomState;

            Assert.That(
                () => intents.CompleteAndSelectNext(enemyId),
                Throws.InvalidOperationException.With.Message.Contains("no legal candidate"));
            Assert.That(intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(intents.RandomState, Is.EqualTo(randomStateBefore));

            Assert.That(
                () => intents.CompleteAndSelectNext(enemyId),
                Throws.InvalidOperationException.With.Message.Contains("no legal candidate"));
            Assert.That(intents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(intents.RandomState, Is.EqualTo(randomStateBefore));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证权重总和等于 Int32 上限时仍可执行一次整数权重选择。</summary>
    [Test]
    public void WeightSum_AtInt32Maximum_IsAccepted()
    {
        Tables tables = CreateTables(
            new JArray(CreateEnemy(2001, 6001)),
            new JArray(CreateGroup(6001, 7001, 7002)),
            new JArray(
                CreateBehavior(7001, weight: int.MaxValue - 1),
                CreateBehavior(7002, weight: 1)));
        BattleEnemyIntentsData intents = CreateIntents(
            tables,
            battleSeed: 2468,
            out BattleCombatantsData combatants,
            out IReadOnlyList<CombatantId> enemyIds,
            2001);

        try
        {
            Assert.That(
                GetCurrentBehaviorId(intents, enemyIds[0]),
                Is.EqualTo(7001).Or.EqualTo(7002));
        }
        finally
        {
            intents.Dispose();
            combatants.Dispose();
        }
    }

    /// <summary>验证权重总和超过 Int32 上限时在创建阶段明确失败。</summary>
    [Test]
    public void WeightSum_AboveInt32Maximum_IsRejected()
    {
        Tables tables = CreateTables(
            new JArray(CreateEnemy(2001, 6001)),
            new JArray(CreateGroup(6001, 7001, 7002)),
            new JArray(
                CreateBehavior(7001, weight: int.MaxValue),
                CreateBehavior(7002, weight: 1)));
        var combatants = new BattleCombatantsData();
        EnemyCombatantData enemy = combatants.AddEnemy(2001, maxHealth: 20, strength: 0);

        try
        {
            Assert.That(
                () => new BattleEnemyIntentsData(combatants, new[] { enemy.Id }, tables, battleSeed: 1),
                Throws.InvalidOperationException.With.Message.Contains("total weight"));
        }
        finally
        {
            combatants.Dispose();
        }
    }

    /// <summary>验证非正权重、负冷却和负连续上限都会被配置契约拒绝。</summary>
    [TestCase(0, 0, 0, "weight")]
    [TestCase(1, -1, 0, "cooldown")]
    [TestCase(1, 0, -1, "max consecutive")]
    public void InvalidBehaviorLimits_AreRejected(
        int weight,
        int cooldownSelections,
        int maxConsecutive,
        string expectedMessage)
    {
        Tables tables = CreateTables(
            new JArray(CreateEnemy(2001, 6001)),
            new JArray(CreateGroup(6001, 7001)),
            new JArray(CreateBehavior(7001, weight, cooldownSelections, maxConsecutive)));
        var combatants = new BattleCombatantsData();
        EnemyCombatantData enemy = combatants.AddEnemy(2001, maxHealth: 20, strength: 0);

        try
        {
            Assert.That(
                () => new BattleEnemyIntentsData(combatants, new[] { enemy.Id }, tables, battleSeed: 1),
                Throws.InvalidOperationException.With.Message.Contains(expectedMessage));
        }
        finally
        {
            combatants.Dispose();
        }
    }

    /// <summary>验证缺少行为组、行为或 Effect 引用时都在发布初始意图前失败。</summary>
    [TestCase("group", "behavior group")]
    [TestCase("behavior", "behavior 7001")]
    [TestCase("effect", "missing effect")]
    public void MissingStaticReferences_AreRejected(string missingReference, string expectedMessage)
    {
        JArray groups = missingReference == "group"
            ? new JArray()
            : new JArray(CreateGroup(6001, 7001));
        JArray behaviors = missingReference == "behavior"
            ? new JArray()
            : new JArray(CreateBehavior(7001, weight: 1));
        JArray effects = missingReference == "effect"
            ? new JArray()
            : CreateDefaultEffects();
        Tables tables = CreateTables(
            new JArray(CreateEnemy(2001, 6001)),
            groups,
            behaviors,
            effects);
        var combatants = new BattleCombatantsData();
        EnemyCombatantData enemy = combatants.AddEnemy(2001, maxHealth: 20, strength: 0);

        try
        {
            Assert.That(
                () => new BattleEnemyIntentsData(combatants, new[] { enemy.Id }, tables, battleSeed: 1),
                Throws.InvalidOperationException.With.Message.Contains(expectedMessage));
        }
        finally
        {
            combatants.Dispose();
        }
    }

    /// <summary>创建第一版加权随机敌人的最小静态配置。</summary>
    private static Tables CreateWeightedTables()
    {
        return CreateTables(
            new JArray(CreateEnemy(2002, 6002)),
            new JArray(CreateGroup(6002, 7002, 7003)),
            new JArray(
                CreateBehavior(7002, weight: 3),
                CreateBehavior(7003, weight: 1, intentType: 1, targetRule: 0)));
    }

    /// <summary>创建测试所需的完整 Luban 表集合。</summary>
    private static Tables CreateTables(
        JArray enemies,
        JArray behaviorGroups,
        JArray behaviors,
        JArray effects = null)
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = enemies,
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = effects ?? CreateDefaultEffects(),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = behaviorGroups,
            ["battle_tbenemybehavior"] = behaviors
        };
        return new Tables(tableName => data[tableName]);
    }

    /// <summary>创建一个引用指定行为组的敌人模板 JSON。</summary>
    private static JObject CreateEnemy(int enemyId, int behaviorGroupId)
    {
        return new JObject
        {
            ["id"] = enemyId,
            ["name_i18n_key"] = $"battle.enemy.test_{enemyId}.name",
            ["max_health"] = 20,
            ["base_strength"] = 0,
            ["view_prefab_address"] = "Assets/Arts/Runtime/Character/Prefabs/pfb_char_enemy.prefab",
            ["behavior_group_id"] = behaviorGroupId
        };
    }

    /// <summary>创建保留传入行为顺序的行为组模板 JSON。</summary>
    private static JObject CreateGroup(int groupId, params int[] behaviorIds)
    {
        return new JObject
        {
            ["id"] = groupId,
            ["behavior_ids"] = new JArray(behaviorIds)
        };
    }

    /// <summary>创建包含选择约束和 Effect 引用的行为模板 JSON。</summary>
    private static JObject CreateBehavior(
        int behaviorId,
        int weight,
        int cooldownSelections = 0,
        int maxConsecutive = 0,
        int intentType = 0,
        int targetRule = 1,
        int effectId = 4002)
    {
        return new JObject
        {
            ["id"] = behaviorId,
            ["intent_type"] = intentType,
            ["target_rule"] = targetRule,
            ["effect_id"] = effectId,
            ["weight"] = weight,
            ["cooldown_selections"] = cooldownSelections,
            ["max_consecutive"] = maxConsecutive
        };
    }

    /// <summary>创建行为测试默认引用的最小 Effect 配置。</summary>
    private static JArray CreateDefaultEffects()
    {
        return new JArray(
            new JObject
            {
                ["id"] = 4002,
                ["effect_type"] = 1,
                ["attribute"] = 0,
                ["value"] = 6
            });
    }

    /// <summary>按模板顺序创建敌人运行时实例和意图聚合。</summary>
    private static BattleEnemyIntentsData CreateIntents(
        Tables tables,
        uint battleSeed,
        out BattleCombatantsData combatants,
        out IReadOnlyList<CombatantId> enemyIds,
        params int[] enemyTemplateIds)
    {
        combatants = new BattleCombatantsData();
        var orderedEnemyIds = new List<CombatantId>(enemyTemplateIds.Length);
        foreach (int enemyTemplateId in enemyTemplateIds)
        {
            EnemyCombatantData enemy = combatants.AddEnemy(enemyTemplateId, maxHealth: 20, strength: 0);
            orderedEnemyIds.Add(enemy.Id);
        }

        enemyIds = orderedEnemyIds.AsReadOnly();
        return new BattleEnemyIntentsData(combatants, enemyIds, tables, battleSeed);
    }

    /// <summary>读取指定敌人的权威当前行为模板标识。</summary>
    private static int GetCurrentBehaviorId(BattleEnemyIntentsData intents, CombatantId enemyId)
    {
        Assert.That(intents.Layout.CurrentValue.TryGetBehaviorId(enemyId, out int behaviorId), Is.True);
        return behaviorId;
    }

    /// <summary>从当前意图开始读取固定长度序列，并在每次读取后推进一次行为。</summary>
    private static IReadOnlyList<int> ReadSequence(
        BattleEnemyIntentsData intents,
        CombatantId enemyId,
        int count)
    {
        var sequence = new List<int>(count);
        for (int index = 0; index < count; index++)
        {
            sequence.Add(GetCurrentBehaviorId(intents, enemyId));
            intents.CompleteAndSelectNext(enemyId);
        }

        return sequence;
    }
}
