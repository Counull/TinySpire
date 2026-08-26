using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Run;
using VContainer;

public sealed class BattleSessionTests
{
    /// <summary>验证父 Scope 的输入来源只取值一次，并由子 Scope 冻结为本场唯一装配参数。</summary>
    [Test]
    public void BattleSetupRegistration_WithParentSource_FreezesInjectedOptionsExactlyOnce()
    {
        var expected = new BattleSetupOptions(
            heroTemplateId: 1002,
            encounterTemplateId: 5999,
            randomSeed: 2468);
        var source = new TrackingBattleSetupOptionsSource(expected);
        var parentBuilder = new ContainerBuilder();
        parentBuilder.RegisterInstance(source).As<IBattleSetupOptionsSource>();

        using (IObjectResolver parent = parentBuilder.Build())
        using (IScopedObjectResolver child = parent.CreateScope(
                   builder => BattleLifetimeScope.RegisterBattleSetupOptions(builder, 1001, 5001, 5)))
        {
            BattleSetupOptions first = child.Resolve<BattleSetupOptions>();
            BattleSetupOptions second = child.Resolve<BattleSetupOptions>();

            Assert.That(first, Is.SameAs(expected));
            Assert.That(second, Is.SameAs(expected));
            Assert.That(first.HeroTemplateId, Is.EqualTo(1002));
            Assert.That(first.EncounterTemplateId, Is.EqualTo(5999));
            Assert.That(first.RandomSeed, Is.EqualTo(2468u));
            Assert.That(source.CreateCount, Is.EqualTo(1));

            using BattleSession session = BattleSession.FromConfig(CreateTables(), first);
            PlayerCombatantData player = null;
            foreach (CombatantData combatant in session.Combatants.All.Values)
            {
                if (combatant is PlayerCombatantData candidate)
                    player = candidate;
            }

            Assert.That(player, Is.Not.Null);
            Assert.That(player.TemplateId, Is.EqualTo(1002));
            Assert.That(
                session.EnemyCombatantIdsInEncounterOrder.Count,
                Is.EqualTo(2));
            Assert.That(
                session.Combatants.All[session.EnemyCombatantIdsInEncounterOrder[0]].TemplateId,
                Is.EqualTo(2002));
            Assert.That(
                session.Combatants.All[session.EnemyCombatantIdsInEncounterOrder[1]].TemplateId,
                Is.EqualTo(2001));
            Assert.That(session.CardTargetRandomSeed, Is.EqualTo(2468u));
        }
    }

    /// <summary>验证缺少父输入来源时仍冻结 Inspector 默认值为单场唯一实例。</summary>
    [Test]
    public void BattleSetupRegistration_WithoutParentSource_UsesInspectorDefaultsOnce()
    {
        var parentBuilder = new ContainerBuilder();
        using (IObjectResolver parent = parentBuilder.Build())
        using (IScopedObjectResolver child = parent.CreateScope(
                   builder => BattleLifetimeScope.RegisterBattleSetupOptions(builder, 1001, 5001, 5)))
        {
            BattleSetupOptions first = child.Resolve<BattleSetupOptions>();
            BattleSetupOptions second = child.Resolve<BattleSetupOptions>();

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.HeroTemplateId, Is.EqualTo(1001));
            Assert.That(first.EncounterTemplateId, Is.EqualTo(5001));
            Assert.That(first.RandomSeed, Is.EqualTo(5u));
        }
    }

    /// <summary>Bootstrap 已注册但尚无 active Run 时，legacy/debug Battle 仍使用 Inspector 默认输入。</summary>
    [Test]
    public void BattleSetupRegistration_WithIdleRunFlow_UsesInspectorDefaults()
    {
        using var store = new RunStateStore();
        var flow = new RunFlowService(
            store,
            new ConfigService(),
            new NoOpSceneFlow(),
            new UnusedRunEntropySource(),
            new InMemoryRunSaveStore());
        var parentBuilder = new ContainerBuilder();
        parentBuilder.RegisterInstance(flow)
            .AsSelf()
            .As<IBattleSetupOptionsSource>();

        using (IObjectResolver parent = parentBuilder.Build())
        using (IScopedObjectResolver child = parent.CreateScope(
                   builder => BattleLifetimeScope.RegisterBattleSetupOptions(builder, 1001, 5001, 5)))
        {
            BattleSetupOptions options = child.Resolve<BattleSetupOptions>();

            Assert.That(options.HeroTemplateId, Is.EqualTo(1001));
            Assert.That(options.EncounterTemplateId, Is.EqualTo(5001));
            Assert.That(options.RandomSeed, Is.EqualTo(5u));
            Assert.That(options.PlayerInitialHealth, Is.Null);
            Assert.That(options.DeckTemplateId, Is.Null);
        }
    }

    /// <summary>验证 Session 只创建参与者与洗牌后的未发牌卡区，不提前执行首轮抽牌。</summary>
    [Test]
    public void FromConfig_CreatesCombatantsAndUndealtDeckFromStaticTemplates()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(heroTemplateId: 1001, encounterTemplateId: 5001);

        BattleSession session = BattleSession.FromConfig(tables, options);

        Assert.That(session.Combatants.All.Count, Is.EqualTo(2));
        Assert.That(session.MachineGunnerRuntime, Is.Null);

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
        Assert.That(player.CurrentHealth, Is.EqualTo(80));
        Assert.That(player.MaxHealth, Is.EqualTo(80));
        Assert.That(player.Strength.CurrentValue, Is.EqualTo(1));
        Assert.That(session.PlayerResourceProfiles.Count, Is.EqualTo(1));
        BattlePlayerResourceProfile profile = session.PlayerResourceProfiles[player.Id];
        Assert.That(profile.InitialEnergy, Is.EqualTo(3));
        Assert.That(profile.MaxEnergy, Is.EqualTo(3));
        Assert.That(profile.EnergyGainPerRound, Is.EqualTo(3));
        Assert.That(profile.InitialAmmo, Is.Zero);
        Assert.That(profile.MaxAmmo, Is.Zero);
        Assert.That(profile.AmmoGainPerRound, Is.Zero);
        Assert.That(enemy, Is.Not.Null);
        Assert.That(enemy.TemplateId, Is.EqualTo(2001));
        Assert.That(enemy.MaxHealth, Is.EqualTo(20));
        Assert.That(session.EnemyCombatantIdsInEncounterOrder, Is.EqualTo(new[] { enemy.Id }));

        Assert.That(session.CardZones.Cards.Count, Is.EqualTo(10));
        Assert.That(session.CardZones.Hand, Is.Empty);
        Assert.That(session.CardZones.DrawPile.Count, Is.EqualTo(10));
        Assert.That(session.CardZones.DiscardPile, Is.Empty);
        Assert.That(session.CardZones.ExhaustPile, Is.Empty);
        Assert.That(
            session.AvailableCardTemplateIds,
            Is.EqualTo(new[] { 3002, 3003, 3004 }),
            "普通职业只能预声明初始牌组的去重模板。");
        Assert.That(session.EnemyIntents.Layout.CurrentValue.TryGetBehaviorId(enemy.Id, out int behaviorId), Is.True);
        Assert.That(behaviorId, Is.EqualTo(7001));

        session.Dispose();
    }

    /// <summary>验证 Run 显式输入会覆盖 Hero 默认生命与牌组，并把同一本战 seed 交给 Session。</summary>
    [Test]
    public void FromConfig_WithRunInputs_UsesHeroCurrentHealthDeckAndBattleSeed()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1002,
            encounterTemplateId: 5001,
            randomSeed: 2468,
            playerInitialHealth: 57,
            deckTemplateId: 1001);

        using BattleSession session = BattleSession.FromConfig(tables, options);
        PlayerCombatantData player = null;
        foreach (CombatantData combatant in session.Combatants.All.Values)
        {
            if (combatant is PlayerCombatantData candidate)
                player = candidate;
        }

        Assert.That(player, Is.Not.Null);
        Assert.That(player.TemplateId, Is.EqualTo(1002));
        Assert.That(player.CurrentHealth, Is.EqualTo(57));
        Assert.That(player.MaxHealth, Is.EqualTo(90));
        Assert.That(session.CardZones.Cards.Count, Is.EqualTo(10));
        Assert.That(session.AvailableCardTemplateIds, Is.EqualTo(new[] { 3002, 3003, 3004 }));
        Assert.That(session.CardTargetRandomSeed, Is.EqualTo(2468u));
    }

    /// <summary>显式 RunCard 投影必须覆盖 Hero 初始牌组，并原样带入每个稳定实例与等级。</summary>
    [Test]
    public void FromConfig_WithRunCards_UsesRunProjectionInsteadOfHeroDeck()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1002,
            encounterTemplateId: 5001,
            randomSeed: 2468,
            playerInitialHealth: 57,
            runCards: new[]
            {
                new RunCard(new RunCardInstanceId(7), templateId: 3004, upgradeLevel: 0),
                new RunCard(new RunCardInstanceId(8), templateId: 3002, upgradeLevel: 0),
                new RunCard(new RunCardInstanceId(9), templateId: 3002, upgradeLevel: 1),
            });

        using BattleSession session = BattleSession.FromConfig(tables, options);
        CardInstanceData[] projected = session.CardZones.Cards
            .OrderBy(pair => pair.Key.Value)
            .Select(pair => pair.Value)
            .ToArray();

        Assert.That(
            projected.Select(card => card.OriginRunCardInstanceId?.Sequence),
            Is.EqualTo(new int?[] { 7, 8, 9 }));
        Assert.That(projected.Select(card => card.TemplateId), Is.EqualTo(new[] { 3004, 3002, 3002 }));
        Assert.That(projected.Select(card => card.UpgradeLevel), Is.EqualTo(new[] { 0, 0, 1 }));
        Assert.That(session.AvailableCardTemplateIds, Is.EqualTo(new[] { 3004, 3002 }));
    }

    /// <summary>有限轨道不存在的二级必须在任何战斗聚合发布前拒绝装配。</summary>
    [Test]
    public void FromConfig_WithRunCardBeyondFiniteTrack_IsRejectedBeforeSessionPublication()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1002,
            encounterTemplateId: 5001,
            randomSeed: 2468,
            playerInitialHealth: 57,
            runCards: new[]
            {
                new RunCard(new RunCardInstanceId(7), templateId: 3002, upgradeLevel: 2),
            });

        Assert.Throws<ArgumentOutOfRangeException>(() => BattleSession.FromConfig(tables, options));
    }

    /// <summary>验证 Run 当前生命超过 Hero 上限时 Session 在发布前立即拒绝装配。</summary>
    [Test]
    public void FromConfig_WithRunHealthAboveHeroMaximum_IsRejected()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1002,
            encounterTemplateId: 5001,
            randomSeed: 2468,
            playerInitialHealth: 91,
            deckTemplateId: 1002);

        Assert.Throws<System.InvalidOperationException>(() => BattleSession.FromConfig(tables, options));
    }

    /// <summary>验证 Run 指定不存在的牌组模板时不会回退到 Hero 默认牌组。</summary>
    [Test]
    public void FromConfig_WithMissingRunDeck_IsRejectedWithoutFallback()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1002,
            encounterTemplateId: 5001,
            randomSeed: 2468,
            playerInitialHealth: 57,
            deckTemplateId: 9999);

        Assert.Throws<System.InvalidOperationException>(() => BattleSession.FromConfig(tables, options));
    }

    /// <summary>显式 RunCard 引用缺失模板时必须拒绝装配，不能回退到 Hero 初始牌组。</summary>
    [Test]
    public void FromConfig_WithMissingRunCardTemplate_IsRejectedWithoutDeckFallback()
    {
        Tables tables = CreateTables();
        var options = new BattleSetupOptions(
            heroTemplateId: 1002,
            encounterTemplateId: 5001,
            randomSeed: 2468,
            playerInitialHealth: 57,
            runCards: new[]
            {
                new RunCard(new RunCardInstanceId(71), templateId: 9999, upgradeLevel: 0),
            });

        Assert.Throws<InvalidOperationException>(() => BattleSession.FromConfig(tables, options));
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

    /// <summary>验证不同战斗种子会导出不同的洗牌轨迹，而不改变输入载体职责。</summary>
    [Test]
    public void FromConfig_WithDifferentSeeds_CreatesDifferentShuffledDrawPile()
    {
        Tables tables = CreateTables();
        using BattleSession first = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(1001, 5001, randomSeed: 2468));
        using BattleSession second = BattleSession.FromConfig(
            tables,
            new BattleSetupOptions(1001, 5001, randomSeed: 8642));

        Assert.That(second.CardZones.DrawPile, Is.Not.EqualTo(first.CardZones.DrawPile));
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

    /// <summary>记录父 Scope 输入来源的取值次数，并返回同一个不可变装配参数。</summary>
    private sealed class TrackingBattleSetupOptionsSource : IBattleSetupOptionsSource
    {
        private readonly BattleSetupOptions _options;

        /// <summary>已请求本场装配参数的次数。</summary>
        public int CreateCount { get; private set; }

        /// <summary>保存测试期望的本场装配参数。</summary>
        public TrackingBattleSetupOptionsSource(BattleSetupOptions options)
        {
            _options = options;
        }

        /// <summary>返回注入的装配参数，并记录本场来源只被求值一次。</summary>
        public BattleSetupOptions CreateBattleSetupOptions()
        {
            CreateCount++;
            return _options;
        }
    }

    /// <summary>legacy setup 测试不切换场景，只同步完成请求。</summary>
    private sealed class NoOpSceneFlow : ISceneFlowService
    {
        /// <summary>同步完成未使用的场景切换。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            return UniTask.CompletedTask;
        }
    }

    /// <summary>legacy setup 测试不得创建新 Run，因此误调用时立即失败。</summary>
    private sealed class UnusedRunEntropySource : IRunEntropySource
    {
        /// <summary>拒绝签发测试不需要的 Run 随机输入。</summary>
        public RunEntropy Next()
        {
            throw new InvalidOperationException("Legacy setup must not create a Run.");
        }
    }

    /// <summary>创建包含测试英雄、牌组、卡牌与可配置敌人顺序的最小静态表。</summary>
    private static Tables CreateTables(string encounterEnemyTemplateIds = "[2001]")
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = JArray.Parse(
                "[{\"id\":1001,\"name_i18n_key\":\"battle.hero.test_warrior.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":80,\"base_strength\":1,\"initial_deck_id\":1001,\"initial_energy\":3,\"max_energy\":3,\"energy_gain_per_round\":3,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0,\"reward_card_template_ids\":[],\"reward_common_weight\":0,\"reward_uncommon_weight\":0,\"reward_rare_weight\":0}," +
                "{\"id\":1002,\"name_i18n_key\":\"battle.hero.test_injected.name\",\"view_prefab_key\":\"pfb_char_player\",\"max_health\":90,\"base_strength\":2,\"initial_deck_id\":1002,\"initial_energy\":4,\"max_energy\":4,\"energy_gain_per_round\":4,\"initial_ammo\":0,\"max_ammo\":0,\"ammo_gain_per_round\":0,\"runtime_profile\":0,\"reward_card_template_ids\":[],\"reward_common_weight\":0,\"reward_uncommon_weight\":0,\"reward_rare_weight\":0}]"),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"view_prefab_key\":\"pfb_char_enemy\",\"max_health\":20,\"base_strength\":0,\"behavior_group_id\":6001},{\"id\":2002,\"name_i18n_key\":\"battle.enemy.test_slime.name\",\"view_prefab_key\":\"pfb_char_enemy\",\"max_health\":20,\"base_strength\":0,\"behavior_group_id\":6002}]"),
            ["battle_tbdeck"] = JArray.Parse(
                "[{\"id\":1001,\"card_template_ids\":[3002,3002,3002,3002,3002,3003,3003,3003,3003,3004]}," +
                "{\"id\":1002,\"card_template_ids\":[3002,3003,3004]}]"),
            ["battle_tbcard"] = JArray.Parse(
                "[{\"id\":3002,\"external_key\":\"TEST_BATTLE_SESSION_STRIKE\",\"catalog_snapshot_key\":\"test-fixture\",\"name_i18n_key\":\"battle.card.strike.name\",\"description_i18n_key\":\"battle.card.strike.description\",\"upgraded_description_i18n_key\":\"battle.card.strike.description\",\"card_type\":0,\"rarity\":0,\"cost\":1,\"cost_kind\":0,\"upgraded_cost\":1,\"target_rule\":1,\"play_destination\":0,\"upgraded_play_destination\":0,\"has_upgrade\":true,\"implementation_status\":0,\"effect_bindings\":[{\"argument_key\":\"damage\",\"effect_id\":4002}],\"program_id\":0,\"is_innate\":false,\"upgrade_track_kind\":1,\"infinite_upgrade_rule_kind\":0,\"infinite_upgrade_value_per_level\":0},{" +
                "\"id\":3003,\"external_key\":\"TEST_BATTLE_SESSION_DEFEND\",\"catalog_snapshot_key\":\"test-fixture\",\"name_i18n_key\":\"battle.card.defend.name\",\"description_i18n_key\":\"battle.card.defend.description\",\"upgraded_description_i18n_key\":\"battle.card.defend.description\",\"card_type\":1,\"rarity\":0,\"cost\":1,\"cost_kind\":0,\"upgraded_cost\":1,\"target_rule\":0,\"play_destination\":0,\"upgraded_play_destination\":0,\"has_upgrade\":false,\"implementation_status\":0,\"effect_bindings\":[{\"argument_key\":\"block\",\"effect_id\":4003}],\"program_id\":0,\"is_innate\":false,\"upgrade_track_kind\":0,\"infinite_upgrade_rule_kind\":0,\"infinite_upgrade_value_per_level\":0},{" +
                "\"id\":3004,\"external_key\":\"TEST_BATTLE_SESSION_BASH\",\"catalog_snapshot_key\":\"test-fixture\",\"name_i18n_key\":\"battle.card.bash.name\",\"description_i18n_key\":\"battle.card.bash.description\",\"upgraded_description_i18n_key\":\"battle.card.bash.description\",\"card_type\":0,\"rarity\":0,\"cost\":2,\"cost_kind\":0,\"upgraded_cost\":2,\"target_rule\":1,\"play_destination\":0,\"upgraded_play_destination\":0,\"has_upgrade\":false,\"implementation_status\":0,\"effect_bindings\":[{\"argument_key\":\"damage\",\"effect_id\":4004},{\"argument_key\":\"vulnerable\",\"effect_id\":4005}],\"program_id\":0,\"is_innate\":false,\"upgrade_track_kind\":0,\"infinite_upgrade_rule_kind\":0,\"infinite_upgrade_value_per_level\":0}]"),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6},{" +
                "\"id\":4003,\"effect_type\":2,\"attribute\":0,\"value\":5},{" +
                "\"id\":4004,\"effect_type\":1,\"attribute\":0,\"value\":8},{" +
                "\"id\":4005,\"effect_type\":3,\"attribute\":0,\"value\":2}]"),
            ["battle_tbencounter"] = JArray.Parse(
                $"[{{\"id\":5001,\"enemy_template_ids\":{encounterEnemyTemplateIds}}}," +
                "{\"id\":5999,\"enemy_template_ids\":[2002,2001]}]"),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]},{\"id\":6002,\"behavior_ids\":[7002,7003]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4002,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":0},{\"id\":7002,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4002,\"weight\":3,\"cooldown_selections\":0,\"max_consecutive\":2},{\"id\":7003,\"intent_type\":1,\"target_rule\":0,\"effect_id\":4003,\"weight\":1,\"cooldown_selections\":1,\"max_consecutive\":1}]"),
            ["battle_tbcardupgradelevel"] = JArray.Parse(
                "[{\"card_id\":3002,\"next_upgrade_level\":1,\"description_i18n_key\":\"battle.card.strike.upgrade_description\",\"cost\":1,\"play_destination\":0,\"rule_kind\":1,\"rule_value\":9}]"),
        };

        return new Tables(tableName => data[tableName]);
    }
}
