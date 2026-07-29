using System;
using cfg;
using TinySpire.Core;

namespace TinySpire.Battle
{
    public sealed class BattleSetupOptions
    {
        public int HeroTemplateId { get; }
        public int EncounterTemplateId { get; }
        public uint RandomSeed { get; }

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
    public sealed class BattleSession
    {
        public BattleState BattleState { get; }
        public CardZoneState CardZones { get; }

        public BattleSession(ConfigService configs, BattleSetupOptions options)
        {
            BattleSession initialized = CreateFromConfigService(configs, options);
            BattleState = initialized.BattleState;
            CardZones = initialized.CardZones;
        }

        private BattleSession(BattleState battleState, CardZoneState cardZones)
        {
            BattleState = battleState;
            CardZones = cardZones;
        }

        public static BattleSession FromConfig(Tables tables, GameConfig gameConfig, BattleSetupOptions options)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (gameConfig == null)
                throw new ArgumentNullException(nameof(gameConfig));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            cfg.battle.Hero hero = tables.TbHero.GetOrDefault(options.HeroTemplateId)
                ?? throw new InvalidOperationException($"Hero template {options.HeroTemplateId} does not exist.");
            cfg.battle.Deck deck = tables.TbDeck.GetOrDefault(hero.InitialDeckId)
                ?? throw new InvalidOperationException($"Deck template {hero.InitialDeckId} does not exist.");
            cfg.battle.Encounter encounter = tables.TbEncounter.GetOrDefault(options.EncounterTemplateId)
                ?? throw new InvalidOperationException($"Encounter template {options.EncounterTemplateId} does not exist.");

            ValidateDeckCards(tables, deck);

            var battleState = new BattleState();
            battleState.AddPlayer(hero.Id, hero.MaxHealth, hero.BaseStrength);
            foreach (int enemyTemplateId in encounter.EnemyTemplateIds)
            {
                cfg.battle.Enemy enemy = tables.TbEnemy.GetOrDefault(enemyTemplateId)
                    ?? throw new InvalidOperationException($"Enemy template {enemyTemplateId} does not exist.");
                battleState.AddEnemy(enemy.Id, enemy.MaxHealth, enemy.BaseStrength);
            }

            var cardZones = new CardZoneState(
                deck.CardTemplateIds,
                options.RandomSeed);
            int initialHandCount = Math.Min(Math.Max(0, gameConfig.InitialHandCount), deck.CardTemplateIds.Length);
            cardZones.Draw(initialHandCount);

            return new BattleSession(battleState, cardZones);
        }

        private static BattleSession CreateFromConfigService(ConfigService configs, BattleSetupOptions options)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));
            if (configs.Tables == null || configs.GameConfig == null)
                throw new InvalidOperationException("ConfigService must be initialized before creating a battle session.");

            return FromConfig(configs.Tables, configs.GameConfig, options);
        }

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
