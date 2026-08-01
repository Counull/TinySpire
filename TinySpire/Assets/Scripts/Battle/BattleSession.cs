using System;
using System.Collections.Generic;
using cfg;

namespace TinySpire.Battle
{
    /// <summary>
    /// 创建单场战斗运行时数据所需的静态模板标识与随机种子。
    /// </summary>
    public sealed class BattleSetupOptions
    {
        /// <summary>玩家 Hero 模板标识。</summary>
        public int HeroTemplateId { get; }

        /// <summary>敌方 Encounter 模板标识。</summary>
        public int EncounterTemplateId { get; }

        /// <summary>本场确定性随机流的初始种子。</summary>
        public uint RandomSeed { get; }

        /// <summary>
        /// 创建战斗装配参数，并验证所有模板标识和种子均有效。
        /// </summary>
        public BattleSetupOptions(int heroTemplateId, int encounterTemplateId, int randomSeed = 1)
        {
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (encounterTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(encounterTemplateId));
            if (randomSeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(randomSeed));

            HeroTemplateId = heroTemplateId;
            EncounterTemplateId = encounterTemplateId;
            RandomSeed = (uint)randomSeed;
        }
    }

    /// <summary>
    /// 把静态战斗模板实例化为一场战斗的运行时事实。
    /// 不保存配置表的镜像，也不执行卡牌效果。
    /// </summary>
    public sealed class BattleSession : IDisposable
    {
        /// <summary>本场战斗的参与者运行时数据。</summary>
        public BattleCombatantsData Combatants { get; }

        /// <summary>本场战斗的卡区运行时数据。</summary>
        public BattleCardZonesData CardZones { get; }

        /// <summary>本场战斗中每名敌人的权威当前意图与独立确定性选择历史。</summary>
        public BattleEnemyIntentsData EnemyIntents { get; }

        /// <summary>
        /// Encounter 配置顺序对应的敌方参与者标识，供布局和未来敌方行动使用。
        /// 这不是由字典枚举派生的镜像列表。
        /// </summary>
        public IReadOnlyList<CombatantId> EnemyCombatantIdsInEncounterOrder { get; }

        /// <summary>
        /// 从已初始化的配置服务创建一场战斗。
        /// </summary>
        public BattleSession(ConfigService configs, BattleSetupOptions options)
        {
            BattleSession initialized = CreateFromConfigService(configs, options);
            Combatants = initialized.Combatants;
            CardZones = initialized.CardZones;
            EnemyIntents = initialized.EnemyIntents;
            EnemyCombatantIdsInEncounterOrder = initialized.EnemyCombatantIdsInEncounterOrder;
        }

        /// <summary>组合已完成初始化的运行时数据聚合与遇敌顺序。</summary>
        private BattleSession(
            BattleCombatantsData combatants,
            BattleCardZonesData cardZones,
            BattleEnemyIntentsData enemyIntents,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder)
        {
            Combatants = combatants;
            CardZones = cardZones;
            EnemyIntents = enemyIntents;
            EnemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder;
        }

        /// <summary>
        /// 从显式静态配置表创建尚未发牌的一场战斗，便于启动流程与测试复用。
        /// </summary>
        public static BattleSession FromConfig(Tables tables, BattleSetupOptions options)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            cfg.battle.Hero hero = tables.TbHero.GetOrDefault(options.HeroTemplateId)
                ?? throw new InvalidOperationException($"Hero template {options.HeroTemplateId} does not exist.");
            cfg.battle.Deck deck = tables.TbDeck.GetOrDefault(hero.InitialDeckId)
                ?? throw new InvalidOperationException($"Deck template {hero.InitialDeckId} does not exist.");
            cfg.battle.Encounter encounter = tables.TbEncounter.GetOrDefault(options.EncounterTemplateId)
                ?? throw new InvalidOperationException($"Encounter template {options.EncounterTemplateId} does not exist.");

            ValidateDeckCards(tables, deck);

            var combatants = new BattleCombatantsData();
            combatants.AddPlayer(hero.Id, hero.MaxHealth, hero.BaseStrength);
            var enemyCombatantIdsInEncounterOrder = new List<CombatantId>(encounter.EnemyTemplateIds.Length);
            foreach (int enemyTemplateId in encounter.EnemyTemplateIds)
            {
                cfg.battle.Enemy enemy = tables.TbEnemy.GetOrDefault(enemyTemplateId)
                    ?? throw new InvalidOperationException($"Enemy template {enemyTemplateId} does not exist.");
                EnemyCombatantData combatant = combatants.AddEnemy(enemy.Id, enemy.MaxHealth, enemy.BaseStrength);
                enemyCombatantIdsInEncounterOrder.Add(combatant.Id);
            }

            BattleCardZonesData cardZones = null;
            BattleEnemyIntentsData enemyIntents = null;
            try
            {
                cardZones = new BattleCardZonesData(
                    deck.CardTemplateIds,
                    options.RandomSeed);
                enemyIntents = new BattleEnemyIntentsData(
                    combatants,
                    enemyCombatantIdsInEncounterOrder,
                    tables,
                    options.RandomSeed);

                return new BattleSession(
                    combatants,
                    cardZones,
                    enemyIntents,
                    enemyCombatantIdsInEncounterOrder.AsReadOnly());
            }
            catch
            {
                enemyIntents?.Dispose();
                cardZones?.Dispose();
                combatants.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 释放本场战斗持有的敌人意图、参与者和卡区响应式资源。
        /// </summary>
        public void Dispose()
        {
            EnemyIntents.Dispose();
            Combatants.Dispose();
            CardZones.Dispose();
        }

        /// <summary>确认配置服务可用后，将其委托给显式配置装配入口。</summary>
        private static BattleSession CreateFromConfigService(ConfigService configs, BattleSetupOptions options)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));
            if (configs.Tables == null)
                throw new InvalidOperationException("ConfigService must be initialized before creating a battle session.");

            return FromConfig(configs.Tables, options);
        }

        /// <summary>确认初始牌组引用的每张静态卡牌模板均存在。</summary>
        private static void ValidateDeckCards(Tables tables, cfg.battle.Deck deck)
        {
            foreach (int cardTemplateId in deck.CardTemplateIds)
            {
                if (tables.TbCard.GetOrDefault(cardTemplateId) == null)
                    throw new InvalidOperationException($"Card template {cardTemplateId} does not exist.");
            }
        }
    }
}
