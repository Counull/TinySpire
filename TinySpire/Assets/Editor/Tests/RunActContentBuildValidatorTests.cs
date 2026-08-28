using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Run.Map;

/// <summary>验证 G7 单 Act 从地图内容到 Battle 配置与文本的构建期引用门禁。</summary>
public sealed class RunActContentBuildValidatorTests
{
    /// <summary>当前生产 G7 内容图必须通过与 Sync and Build All 相同的真实文件适配器。</summary>
    [Test]
    public void ValidateCurrentProject_ProductionG7ContentPasses()
    {
        Assert.DoesNotThrow(RunActContentBuildValidator.ValidateCurrentProject);
    }

    /// <summary>完整的普通、精英、Boss、行为、效果、唯一遗物和文本引用图必须通过。</summary>
    [Test]
    public void Validate_CompleteActContentGraph_Passes()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);

        Assert.DoesNotThrow(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>普通、精英或 Boss 池引用不存在的 Encounter 时必须阻止构建。</summary>
    [Test]
    public void Validate_MissingEncounter_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Encounters.Remove("5101");

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>任一 Act Encounter 引用不存在的 Enemy 时必须阻止构建。</summary>
    [Test]
    public void Validate_MissingEnemy_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Enemies.Remove("2101");

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>Enemy 素材字段必须保持可转换为角色 Addressables 地址的短键形状。</summary>
    [Test]
    public void Validate_InvalidEnemyAssetKey_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Enemies["2101"]["view_prefab_key"] = "Assets/Prefabs/pfb_char_enemy.prefab";

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>任一 Act Enemy 引用不存在的行为组时必须阻止构建。</summary>
    [Test]
    public void Validate_MissingBehaviorGroup_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.BehaviorGroups.Remove("6101");

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>任一可达行为组引用不存在的 Behavior 时必须阻止构建。</summary>
    [Test]
    public void Validate_MissingBehavior_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Behaviors.Remove("7101");

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>任一可达 Behavior 引用不存在的 Effect 时必须阻止构建。</summary>
    [Test]
    public void Validate_MissingEffect_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Effects.Remove("4101");

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>真实 Boss Encounter 缺少第二阶段行为组时必须阻止构建。</summary>
    [Test]
    public void Validate_BossWithoutPhaseTwoBehaviorGroup_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Encounters["5201"]["phase_two_behavior_group_id"] = 0;

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>Act 自身或可达 Enemy 的任一必需文本 key 缺失时必须阻止构建。</summary>
    [Test]
    public void Validate_MissingRequiredLocalization_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        var keys = new HashSet<string>(localizationKeys, StringComparer.Ordinal);
        keys.Remove("run.entry.map.elite_node");

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            keys,
            Array.Empty<int>()));

        keys.Add("run.entry.map.elite_node");
        keys.Remove("battle.enemy.elite.name");
        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            keys,
            Array.Empty<int>()));
    }

    /// <summary>Act 声明的模板唯一遗物缺记录或文本时必须阻止构建。</summary>
    [Test]
    public void Validate_MissingUniqueRelic_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Relics.Remove("8001");

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>空内容池和重复唯一奖励必须在 manifest 构造时直接失败。</summary>
    [Test]
    public void Manifest_EmptyPoolOrDuplicateUniqueReward_Throws()
    {
        ActContentManifest production = TinySpireActContentCatalog.NewRunG7V1;

        Assert.Throws<ArgumentException>(() => new ActContentManifest(
            production.Profile,
            production.OrdinaryEncounterIds,
            Array.Empty<int>(),
            production.NonCombatContents,
            production.BossEncounterIds,
            production.UniqueRelicTemplateIds,
            production.RequiredLocalizationKeys,
            production.CompletionRule));
        Assert.Throws<ArgumentException>(() => new ActContentManifest(
            production.Profile,
            production.OrdinaryEncounterIds,
            production.EliteEncounterIds,
            production.NonCombatContents,
            production.BossEncounterIds,
            new[] { 8001, 8001 },
            production.RequiredLocalizationKeys,
            production.CompletionRule));
    }

    /// <summary>G7 多个冻结 Boss 身份只能映射同一个真实 Boss Encounter。</summary>
    [Test]
    public void Validate_MultipleRealBossEncounters_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Encounters["5202"] = CreateEncounter(5202, 2201, phaseTwoBehaviorGroupId: 6202);
        ActContentManifest production = TinySpireActContentCatalog.NewRunG7V1;
        var multipleBosses = new ActContentManifest(
            production.Profile,
            production.OrdinaryEncounterIds,
            production.EliteEncounterIds,
            production.NonCombatContents,
            new Dictionary<int, int>
            {
                [9001] = 5201,
                [9002] = 5202,
                [9003] = 5201,
            },
            production.UniqueRelicTemplateIds,
            production.RequiredLocalizationKeys,
            production.CompletionRule);

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            multipleBosses,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>真实 Boss Encounter 必须只包含一个敌人，避免阶段事实落到多目标上。</summary>
    [Test]
    public void Validate_BossWithMultipleEnemies_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Encounters["5201"]["enemy_template_ids"] = new JArray(2201, 2101);

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>Boss 两阶段必须使用不同 Group，且两个 Group 的 Behavior 身份不得重叠。</summary>
    [Test]
    public void Validate_BossPhaseGroupsMatchOrOverlap_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Encounters["5201"]["phase_two_behavior_group_id"] = 6201;

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));

        tables.Encounters["5201"]["phase_two_behavior_group_id"] = 6202;
        tables.BehaviorGroups["6202"]["behavior_ids"] = new JArray(7201);
        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>行为组重复成员与单项行为的权重、限制、枚举漂移都必须阻止构建。</summary>
    [TestCase("duplicate")]
    [TestCase("weight")]
    [TestCase("cooldown")]
    [TestCase("max-consecutive")]
    [TestCase("intent")]
    [TestCase("target")]
    public void Validate_InvalidBehaviorContract_Throws(string fault)
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        switch (fault)
        {
            case "duplicate":
                tables.BehaviorGroups["6101"]["behavior_ids"] = new JArray(7101, 7101);
                break;
            case "weight":
                tables.Behaviors["7101"]["weight"] = 0;
                break;
            case "cooldown":
                tables.Behaviors["7101"]["cooldown_selections"] = -1;
                break;
            case "max-consecutive":
                tables.Behaviors["7101"]["max_consecutive"] = -1;
                break;
            case "intent":
                tables.Behaviors["7101"]["intent_type"] = 99;
                break;
            case "target":
                tables.Behaviors["7101"]["target_rule"] = 2;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fault));
        }

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>Chest/Shop 固定药水、非战斗文案与物品数值任一漂移都必须阻止构建。</summary>
    [Test]
    public void Validate_NonCombatItemOrLocalizationDrift_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Potions.Remove("9001");
        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));

        tables = CreateValidContentGraph(out localizationKeys);
        var missingEventText = new HashSet<string>(localizationKeys, StringComparer.Ordinal);
        missingEventText.Remove("run.entry.event.title");
        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            missingEventText,
            Array.Empty<int>()));

        tables = CreateValidContentGraph(out localizationKeys);
        var missingItemText = new HashSet<string>(localizationKeys, StringComparer.Ordinal);
        missingItemText.Remove("run.potion.healing.description");
        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            missingItemText,
            Array.Empty<int>()));

        tables = CreateValidContentGraph(out localizationKeys);
        tables.Potions["9001"]["heal_amount"] = 0;
        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));

        tables = CreateValidContentGraph(out localizationKeys);
        tables.Relics["8001"]["battle_start_strength"] = 0;
        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>Shop 依赖的现有 Hero 奖励卡池门禁失败时，聚合 Act 门禁也必须失败。</summary>
    [Test]
    public void Validate_ShopRewardPoolInvalid_Throws()
    {
        RunActContentTables tables = CreateValidContentGraph(
            out IReadOnlyCollection<string> localizationKeys);
        tables.Heroes["1001"]["reward_card_template_ids"] = new JArray();

        Assert.Throws<InvalidOperationException>(() => RunActContentBuildValidator.Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            Array.Empty<int>()));
    }

    /// <summary>Manifest 中任一程序化非战斗 anchor 漂移时必须在构造阶段失败。</summary>
    [Test]
    public void Manifest_NonCombatAnchorDrift_Throws()
    {
        ActContentManifest production = TinySpireActContentCatalog.NewRunG7V1;
        var driftedContents = production.NonCombatContents
            .Select(content => content.Kind == MapNodeKind.Shop
                ? new ActNonCombatContentReference(
                    kind: MapNodeKind.Shop,
                    contentId: 7302,
                    relicTemplateIds: content.RelicTemplateIds,
                    potionTemplateIds: content.PotionTemplateIds,
                    usesHeroCardRewardPool: true,
                    requiredLocalizationKeys: content.RequiredLocalizationKeys)
                : content)
            .ToArray();

        Assert.Throws<ArgumentException>(() => new ActContentManifest(
            production.Profile,
            production.OrdinaryEncounterIds,
            production.EliteEncounterIds,
            driftedContents,
            production.BossEncounterIds,
            production.UniqueRelicTemplateIds,
            production.RequiredLocalizationKeys,
            production.CompletionRule));
    }

    /// <summary>建立一条与 G7 生产 manifest 身份完全匹配的最小跨表内容图。</summary>
    private static RunActContentTables CreateValidContentGraph(
        out IReadOnlyCollection<string> localizationKeys)
    {
        var encounters = new JObject
        {
            ["5001"] = CreateEncounter(5001, 2001, phaseTwoBehaviorGroupId: 0),
            ["5101"] = CreateEncounter(5101, 2101, phaseTwoBehaviorGroupId: 0),
            ["5201"] = CreateEncounter(5201, 2201, phaseTwoBehaviorGroupId: 6202),
        };
        var enemies = new JObject
        {
            ["2001"] = CreateEnemy(2001, "battle.enemy.normal.name", 6001),
            ["2101"] = CreateEnemy(2101, "battle.enemy.elite.name", 6101),
            ["2201"] = CreateEnemy(2201, "battle.enemy.boss.name", 6201),
        };
        var behaviorGroups = new JObject
        {
            ["6001"] = CreateBehaviorGroup(6001, 7001),
            ["6101"] = CreateBehaviorGroup(6101, 7101),
            ["6201"] = CreateBehaviorGroup(6201, 7201),
            ["6202"] = CreateBehaviorGroup(6202, 7202),
        };
        var behaviors = new JObject
        {
            ["7001"] = CreateBehavior(7001, 4001),
            ["7101"] = CreateBehavior(7101, 4101),
            ["7201"] = CreateBehavior(7201, 4201),
            ["7202"] = CreateBehavior(7202, 4202),
        };
        var effects = new JObject
        {
            ["4001"] = new JObject { ["id"] = 4001 },
            ["4101"] = new JObject { ["id"] = 4101 },
            ["4201"] = new JObject { ["id"] = 4201 },
            ["4202"] = new JObject { ["id"] = 4202 },
        };
        var relics = new JObject
        {
            ["8001"] = new JObject
            {
                ["id"] = 8001,
                ["name_i18n_key"] = "run.relic.unique.name",
                ["description_i18n_key"] = "run.relic.unique.description",
                ["battle_start_strength"] = 1,
            },
        };
        var potions = new JObject
        {
            ["9001"] = new JObject
            {
                ["id"] = 9001,
                ["name_i18n_key"] = "run.potion.healing.name",
                ["description_i18n_key"] = "run.potion.healing.description",
                ["heal_amount"] = 10,
            },
        };
        var heroes = new JObject
        {
            ["1001"] = CreateHero(1001, 1001, 3001, 3002, 3003),
            ["1002"] = CreateHero(1002, 1002, 3004, 3005, 3006),
        };
        var decks = new JObject
        {
            ["1001"] = new JObject { ["id"] = 1001 },
            ["1002"] = new JObject { ["id"] = 1002 },
        };
        var cards = new JObject
        {
            ["3001"] = CreateRewardCard(3001),
            ["3002"] = CreateRewardCard(3002),
            ["3003"] = CreateRewardCard(3003),
            ["3004"] = CreateRewardCard(3004),
            ["3005"] = CreateRewardCard(3005),
            ["3006"] = CreateRewardCard(3006),
        };
        var keys = new HashSet<string>(TinySpireActContentCatalog.NewRunG7V1.RequiredLocalizationKeys,
            StringComparer.Ordinal)
        {
            "battle.enemy.normal.name",
            "battle.enemy.elite.name",
            "battle.enemy.boss.name",
            "run.relic.unique.name",
            "run.relic.unique.description",
            "run.potion.healing.name",
            "run.potion.healing.description",
        };
        foreach (ActNonCombatContentReference content in
                 TinySpireActContentCatalog.NewRunG7V1.NonCombatContents)
        {
            keys.UnionWith(content.RequiredLocalizationKeys);
        }
        localizationKeys = keys;
        return new RunActContentTables(
            encounters,
            enemies,
            behaviorGroups,
            behaviors,
            effects,
            relics,
            potions,
            heroes,
            decks,
            cards);
    }

    /// <summary>建立一个引用单名敌人的 Encounter 记录。</summary>
    private static JObject CreateEncounter(
        int id,
        int enemyTemplateId,
        int phaseTwoBehaviorGroupId)
    {
        return new JObject
        {
            ["id"] = id,
            ["enemy_template_ids"] = new JArray(enemyTemplateId),
            ["phase_two_behavior_group_id"] = phaseTwoBehaviorGroupId,
        };
    }

    /// <summary>建立一个具备文本、素材短键和行为组的 Enemy 记录。</summary>
    private static JObject CreateEnemy(int id, string nameKey, int behaviorGroupId)
    {
        return new JObject
        {
            ["id"] = id,
            ["name_i18n_key"] = nameKey,
            ["view_prefab_key"] = "pfb_char_enemy",
            ["behavior_group_id"] = behaviorGroupId,
        };
    }

    /// <summary>建立一个只含单项行为的非空 BehaviorGroup 记录。</summary>
    private static JObject CreateBehaviorGroup(int id, int behaviorId)
    {
        return new JObject
        {
            ["id"] = id,
            ["behavior_ids"] = new JArray(behaviorId),
        };
    }

    /// <summary>建立一个引用单项 Effect 的正权重行为记录。</summary>
    private static JObject CreateBehavior(int id, int effectId)
    {
        return new JObject
        {
            ["id"] = id,
            ["intent_type"] = 0,
            ["target_rule"] = 1,
            ["effect_id"] = effectId,
            ["weight"] = 1,
            ["cooldown_selections"] = 0,
            ["max_consecutive"] = 0,
        };
    }

    /// <summary>建立一名具有三张有效普通奖励卡和正权重的可选择 Hero。</summary>
    private static JObject CreateHero(int id, int initialDeckId, params int[] rewardCardTemplateIds)
    {
        return new JObject
        {
            ["id"] = id,
            ["initial_deck_id"] = initialDeckId,
            ["reward_card_template_ids"] = new JArray(rewardCardTemplateIds),
            ["reward_common_weight"] = 1,
            ["reward_uncommon_weight"] = 0,
            ["reward_rare_weight"] = 0,
        };
    }

    /// <summary>建立一张已实现且可进入普通奖励池的卡牌记录。</summary>
    private static JObject CreateRewardCard(int id)
    {
        return new JObject
        {
            ["id"] = id,
            ["implementation_status"] = 0,
            ["rarity"] = 1,
        };
    }
}
