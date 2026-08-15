using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;

namespace TinySpire.Battle
{
    /// <summary>为每个 Battle child Scope 提供一次不可变战斗装配参数。</summary>
    public interface IBattleSetupOptionsSource
    {
        /// <summary>创建或返回当前单场战斗唯一使用的装配参数。</summary>
        BattleSetupOptions CreateBattleSetupOptions();
    }

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

        /// <summary>Run 显式提供的玩家入场当前生命；旧入口未提供时为空。</summary>
        public int? PlayerInitialHealth { get; }

        /// <summary>Run 显式提供的起始牌组模板；旧入口未提供时为空。</summary>
        public int? DeckTemplateId { get; }

        /// <summary>
        /// 创建战斗装配参数，并验证所有模板标识和种子均有效。
        /// </summary>
        public BattleSetupOptions(
            int heroTemplateId,
            int encounterTemplateId,
            int randomSeed = 1,
            int? playerInitialHealth = null,
            int? deckTemplateId = null)
        {
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (encounterTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(encounterTemplateId));
            if (randomSeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(randomSeed));
            if (playerInitialHealth.HasValue && playerInitialHealth.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerInitialHealth));
            if (deckTemplateId.HasValue && deckTemplateId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(deckTemplateId));

            HeroTemplateId = heroTemplateId;
            EncounterTemplateId = encounterTemplateId;
            RandomSeed = (uint)randomSeed;
            PlayerInitialHealth = playerInitialHealth;
            DeckTemplateId = deckTemplateId;
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

        /// <summary>按玩家 CombatantId 冻结的 Hero 资源档案，仅供场景装配权威队列。</summary>
        internal IReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile> PlayerResourceProfiles { get; }

        /// <summary>仅在当前 Hero 声明机枪兵档案时创建的职业运行时；默认职业保持为空。</summary>
        internal MachineGunnerBattleRuntime MachineGunnerRuntime { get; }

        /// <summary>本场初始牌组及职业程序可能直接创建的全部卡牌模板，供表现层在异步启动阶段一次准备。</summary>
        internal IReadOnlyList<int> AvailableCardTemplateIds { get; }

        /// <summary>由战斗种子原样复制的通用卡牌目标随机流初始种子。</summary>
        internal uint CardTargetRandomSeed { get; }

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
            PlayerResourceProfiles = initialized.PlayerResourceProfiles;
            MachineGunnerRuntime = initialized.MachineGunnerRuntime;
            AvailableCardTemplateIds = initialized.AvailableCardTemplateIds;
            CardTargetRandomSeed = initialized.CardTargetRandomSeed;
        }

        /// <summary>组合已完成初始化的运行时数据聚合与遇敌顺序。</summary>
        private BattleSession(
            BattleCombatantsData combatants,
            BattleCardZonesData cardZones,
            BattleEnemyIntentsData enemyIntents,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            IReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile> playerResourceProfiles,
            MachineGunnerBattleRuntime machineGunnerRuntime,
            IReadOnlyList<int> availableCardTemplateIds,
            uint cardTargetRandomSeed)
        {
            Combatants = combatants;
            CardZones = cardZones;
            EnemyIntents = enemyIntents;
            EnemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder;
            PlayerResourceProfiles = playerResourceProfiles;
            MachineGunnerRuntime = machineGunnerRuntime;
            AvailableCardTemplateIds = availableCardTemplateIds
                ?? throw new ArgumentNullException(nameof(availableCardTemplateIds));
            if (cardTargetRandomSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(cardTargetRandomSeed));
            CardTargetRandomSeed = cardTargetRandomSeed;
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
            int deckTemplateId = options.DeckTemplateId ?? hero.InitialDeckId;
            cfg.battle.Deck deck = tables.TbDeck.GetOrDefault(deckTemplateId)
                ?? throw new InvalidOperationException($"Deck template {deckTemplateId} does not exist.");
            cfg.battle.Encounter encounter = tables.TbEncounter.GetOrDefault(options.EncounterTemplateId)
                ?? throw new InvalidOperationException($"Encounter template {options.EncounterTemplateId} does not exist.");
            int playerInitialHealth = options.PlayerInitialHealth ?? hero.MaxHealth;
            if (playerInitialHealth > hero.MaxHealth)
            {
                throw new InvalidOperationException(
                    $"Player initial health {playerInitialHealth} exceeds Hero {hero.Id} max health {hero.MaxHealth}.");
            }

            IReadOnlyList<int> availableCardTemplateIds =
                BuildAvailableCardTemplateIds(tables, deck, hero.RuntimeProfile);

            BattlePlayerResourceProfile playerResourceProfile = new BattlePlayerResourceProfile(
                hero.InitialEnergy,
                hero.MaxEnergy,
                hero.EnergyGainPerRound,
                hero.InitialAmmo,
                hero.MaxAmmo,
                hero.AmmoGainPerRound);
            var combatants = new BattleCombatantsData();
            PlayerCombatantData player = combatants.AddPlayer(
                hero.Id,
                playerInitialHealth,
                hero.MaxHealth,
                hero.BaseStrength);
            var playerResourceProfiles = new Dictionary<CombatantId, BattlePlayerResourceProfile>
            {
                [player.Id] = playerResourceProfile,
            };
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
            MachineGunnerBattleRuntime machineGunnerRuntime = null;
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
                if (hero.RuntimeProfile == cfg.battle.HeroRuntimeProfile.MachineGunner)
                {
                    machineGunnerRuntime = new MachineGunnerBattleRuntime(
                        combatants,
                        enemyCombatantIdsInEncounterOrder.AsReadOnly(),
                        player.Id,
                        options.RandomSeed);
                }

                return new BattleSession(
                    combatants,
                    cardZones,
                    enemyIntents,
                    enemyCombatantIdsInEncounterOrder.AsReadOnly(),
                    new ReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile>(
                        playerResourceProfiles),
                    machineGunnerRuntime,
                    availableCardTemplateIds,
                    options.RandomSeed);
            }
            catch
            {
                machineGunnerRuntime?.Dispose();
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
            MachineGunnerRuntime?.Dispose();
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

        /// <summary>冻结初始牌组与职业程序可能直接创建的模板，并在会话发布前拒绝缺失或不可运行的动态模板。</summary>
        private static IReadOnlyList<int> BuildAvailableCardTemplateIds(
            Tables tables,
            cfg.battle.Deck deck,
            cfg.battle.HeroRuntimeProfile runtimeProfile)
        {
            var templateIds = new List<int>();
            var seenTemplateIds = new HashSet<int>();
            foreach (int cardTemplateId in deck.CardTemplateIds)
            {
                if (tables.TbCard.GetOrDefault(cardTemplateId) == null)
                    throw new InvalidOperationException($"Card template {cardTemplateId} does not exist.");

                if (seenTemplateIds.Add(cardTemplateId))
                    templateIds.Add(cardTemplateId);
            }

            if (runtimeProfile != cfg.battle.HeroRuntimeProfile.MachineGunner)
                return new ReadOnlyCollection<int>(templateIds);

            foreach (int cardTemplateId in MachineGunnerCardProgramRegistry.PotentiallyCreatedCardTemplateIds)
            {
                cfg.battle.Card cardTemplate = tables.TbCard.GetOrDefault(cardTemplateId)
                    ?? throw new InvalidOperationException(
                        $"Machine Gunner dynamically created card template {cardTemplateId} does not exist.");
                if (cardTemplate.ImplementationStatus !=
                    cfg.battle.CardImplementationStatus.Implemented ||
                    cardTemplate.ProgramId == cfg.battle.MachineGunnerProgramId.None ||
                    !MachineGunnerCardProgramRegistry.TryGet(cardTemplate.ProgramId, out _))
                {
                    throw new InvalidOperationException(
                        $"Machine Gunner dynamically created card template {cardTemplateId} is not runnable.");
                }

                if (seenTemplateIds.Add(cardTemplateId))
                    templateIds.Add(cardTemplateId);
            }

            return new ReadOnlyCollection<int>(templateIds);
        }
    }
}
