using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cfg;
using TinySpire.Run;

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

        /// <summary>Run 显式提供的有序实例投影；旧调试入口未提供时为空。</summary>
        public IReadOnlyList<RunCard> RunCards { get; }

        /// <summary>Run 显式提供的不可变持有物投影；旧调试入口未提供时为空。</summary>
        public RunHoldings Holdings { get; }

        /// <summary>
        /// 创建战斗装配参数，并验证所有模板标识和种子均有效。
        /// </summary>
        public BattleSetupOptions(
            int heroTemplateId,
            int encounterTemplateId,
            int randomSeed = 1,
            int? playerInitialHealth = null,
            int? deckTemplateId = null,
            IReadOnlyList<RunCard> runCards = null,
            RunHoldings holdings = null)
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
            if (deckTemplateId.HasValue && runCards != null)
                throw new ArgumentException("Battle setup cannot contain two deck authorities.");

            HeroTemplateId = heroTemplateId;
            EncounterTemplateId = encounterTemplateId;
            RandomSeed = (uint)randomSeed;
            PlayerInitialHealth = playerInitialHealth;
            DeckTemplateId = deckTemplateId;
            RunCards = runCards == null
                ? null
                : Array.AsReadOnly(runCards.ToArray());
            Holdings = CopyHoldings(holdings);
        }

        /// <summary>复制 Run 持有物的有序列表与标量，避免 Battle setup 共享外部快照身份。</summary>
        private static RunHoldings CopyHoldings(RunHoldings holdings)
        {
            return holdings == null
                ? null
                : new RunHoldings(holdings.Relics, holdings.Potions, holdings.Gold);
        }
    }

    /// <summary>从一件 Run 遗物解析出的单场 BattleStart 力量结算输入。</summary>
    internal sealed class BattleStartRelicEffect
    {
        /// <summary>所属 Run 中的稳定遗物实例身份。</summary>
        internal RunRelicInstanceId InstanceId { get; }

        /// <summary>产生该效果的遗物静态模板标识。</summary>
        internal int TemplateId { get; }

        /// <summary>本场唯一拥有该遗物的玩家参与者。</summary>
        internal CombatantId OwnerId { get; }

        /// <summary>BattleStart 时应增加的正数力量。</summary>
        internal int StrengthAmount { get; }

        /// <summary>冻结一条已通过 Run 与配置校验的遗物开战效果。</summary>
        internal BattleStartRelicEffect(
            RunRelicInstanceId instanceId,
            int templateId,
            CombatantId ownerId,
            int strengthAmount)
        {
            if (instanceId.Sequence <= 0)
                throw new ArgumentException("Battle relic instance id cannot be empty.", nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (ownerId.Value <= 0)
                throw new ArgumentException("Battle relic owner id cannot be empty.", nameof(ownerId));
            if (strengthAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(strengthAmount));

            InstanceId = instanceId;
            TemplateId = templateId;
            OwnerId = ownerId;
            StrengthAmount = strengthAmount;
        }
    }

    /// <summary>从一个 Run 药水实例解析出的单场 Battle 治疗事实。</summary>
    internal sealed class BattlePotionEntry
    {
        /// <summary>所属 Run 中的稳定药水实例身份。</summary>
        internal RunPotionInstanceId InstanceId { get; }

        /// <summary>该实例引用的静态药水模板。</summary>
        internal int TemplateId { get; }

        /// <summary>本场唯一拥有该药水的玩家参与者。</summary>
        internal CombatantId OwnerId { get; }

        /// <summary>使用成功时请求恢复的正数生命量。</summary>
        internal int HealAmount { get; }

        /// <summary>冻结一条已经通过 Run 与配置校验的药水事实。</summary>
        internal BattlePotionEntry(
            RunPotionInstanceId instanceId,
            int templateId,
            CombatantId ownerId,
            int healAmount)
        {
            if (instanceId.Sequence <= 0)
                throw new ArgumentException("Battle potion instance id cannot be empty.", nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (ownerId.Value <= 0)
                throw new ArgumentException("Battle potion owner id cannot be empty.", nameof(ownerId));
            if (healAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(healAmount));

            InstanceId = instanceId;
            TemplateId = templateId;
            OwnerId = ownerId;
            HealAmount = healAmount;
        }
    }

    /// <summary>由单场 Battle 唯一持有并记录成功消费顺序的药水账本。</summary>
    internal sealed class BattlePotionLedger
    {
        private readonly ReadOnlyCollection<BattlePotionEntry> _entries;
        private readonly Dictionary<RunPotionInstanceId, BattlePotionEntry> _entriesById;
        private readonly HashSet<RunPotionInstanceId> _consumedIds =
            new HashSet<RunPotionInstanceId>();
        private readonly List<RunPotionInstanceId> _consumedInOrder =
            new List<RunPotionInstanceId>();

        /// <summary>按 Run 药水槽位顺序冻结的全部本战药水。</summary>
        internal IReadOnlyList<BattlePotionEntry> Entries => _entries;

        /// <summary>按本战成功消费顺序返回一份防御性只读快照。</summary>
        internal IReadOnlyList<RunPotionInstanceId> ConsumedInstanceIds =>
            new ReadOnlyCollection<RunPotionInstanceId>(_consumedInOrder.ToArray());

        /// <summary>复制并索引本场药水事实，拒绝空项和重复实例身份。</summary>
        internal BattlePotionLedger(IEnumerable<BattlePotionEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            BattlePotionEntry[] frozen = entries.ToArray();
            _entriesById = new Dictionary<RunPotionInstanceId, BattlePotionEntry>();
            foreach (BattlePotionEntry entry in frozen)
            {
                if (entry == null || !_entriesById.TryAdd(entry.InstanceId, entry))
                {
                    throw new ArgumentException(
                        "Battle potion ledger cannot contain null or duplicate entries.",
                        nameof(entries));
                }
            }

            _entries = new ReadOnlyCollection<BattlePotionEntry>(frozen);
        }

        /// <summary>按稳定实例身份读取本场冻结药水，不生成或回退到静态模板。</summary>
        internal bool TryGet(
            RunPotionInstanceId instanceId,
            out BattlePotionEntry entry)
        {
            return _entriesById.TryGetValue(instanceId, out entry);
        }

        /// <summary>只从本战账本判断指定实例是否已经成功消费。</summary>
        internal bool IsConsumed(RunPotionInstanceId instanceId)
        {
            return _consumedIds.Contains(instanceId);
        }

        /// <summary>在治疗成功后把一个存在且未消费的实例原子标记一次。</summary>
        internal bool TryMarkConsumed(RunPotionInstanceId instanceId)
        {
            if (!_entriesById.ContainsKey(instanceId) || !_consumedIds.Add(instanceId))
                return false;

            _consumedInOrder.Add(instanceId);
            return true;
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

        /// <summary>按 Run 获得顺序冻结、等待 StartBattle 唯一消费的遗物效果。</summary>
        internal IReadOnlyList<BattleStartRelicEffect> BattleStartRelicEffects { get; }

        /// <summary>本场独占的药水可用性与成功消费账本。</summary>
        internal BattlePotionLedger PotionLedger { get; }

        /// <summary>本场从 setup 防御性复制的只读 Run 持有物投影。</summary>
        internal RunHoldings Holdings { get; }

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
            BattleStartRelicEffects = initialized.BattleStartRelicEffects;
            PotionLedger = initialized.PotionLedger;
            Holdings = initialized.Holdings;
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
            uint cardTargetRandomSeed,
            IReadOnlyList<BattleStartRelicEffect> battleStartRelicEffects,
            BattlePotionLedger potionLedger,
            RunHoldings holdings)
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
            BattleStartRelicEffects = battleStartRelicEffects
                ?? throw new ArgumentNullException(nameof(battleStartRelicEffects));
            PotionLedger = potionLedger ?? throw new ArgumentNullException(nameof(potionLedger));
            Holdings = holdings;
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
            RunDeck runDeck = options.RunCards == null
                ? null
                : new RunDeck(options.RunCards);
            IReadOnlyList<int> battleCardTemplateIds;
            if (runDeck == null)
            {
                int deckTemplateId = options.DeckTemplateId ?? hero.InitialDeckId;
                cfg.battle.Deck deck = tables.TbDeck.GetOrDefault(deckTemplateId)
                    ?? throw new InvalidOperationException($"Deck template {deckTemplateId} does not exist.");
                battleCardTemplateIds = deck.CardTemplateIds;
            }
            else
            {
                battleCardTemplateIds = runDeck.Cards
                    .Select(card => card.TemplateId)
                    .ToArray();
            }
            ValidateCardLevelsForBattle(tables, runDeck, battleCardTemplateIds);
            cfg.battle.Encounter encounter = tables.TbEncounter.GetOrDefault(options.EncounterTemplateId)
                ?? throw new InvalidOperationException($"Encounter template {options.EncounterTemplateId} does not exist.");
            int playerInitialHealth = options.PlayerInitialHealth ?? hero.MaxHealth;
            if (playerInitialHealth > hero.MaxHealth)
            {
                throw new InvalidOperationException(
                    $"Player initial health {playerInitialHealth} exceeds Hero {hero.Id} max health {hero.MaxHealth}.");
            }

            IReadOnlyList<int> availableCardTemplateIds =
                BuildAvailableCardTemplateIds(tables, battleCardTemplateIds, hero.RuntimeProfile);

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
                IReadOnlyList<BattleStartRelicEffect> battleStartRelicEffects =
                    ResolveBattleStartRelicEffects(tables, options.Holdings, player.Id);
                BattlePotionLedger potionLedger = ResolveBattlePotionLedger(
                    tables,
                    options.Holdings,
                    player.Id);
                ValidateBattleStartRelicStrengthTotal(
                    player.CurrentStrength,
                    battleStartRelicEffects);
                cardZones = runDeck == null
                    ? new BattleCardZonesData(battleCardTemplateIds, options.RandomSeed)
                    : new BattleCardZonesData(runDeck.Cards, options.RandomSeed);
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
                    options.RandomSeed,
                    battleStartRelicEffects,
                    potionLedger,
                    options.Holdings);
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
            IEnumerable<int> cardTemplateIds,
            cfg.battle.HeroRuntimeProfile runtimeProfile)
        {
            var templateIds = new List<int>();
            var seenTemplateIds = new HashSet<int>();
            foreach (int cardTemplateId in cardTemplateIds)
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

        /// <summary>按 Run 获得顺序解析遗物配置，并在缺失、重复或非正效果值时于 Session 发布前失败。</summary>
        private static IReadOnlyList<BattleStartRelicEffect> ResolveBattleStartRelicEffects(
            Tables tables,
            RunHoldings holdings,
            CombatantId ownerId)
        {
            if (holdings == null || holdings.Relics.Count == 0)
                return Array.Empty<BattleStartRelicEffect>();

            var instanceIds = new HashSet<RunRelicInstanceId>();
            var templateIds = new HashSet<int>();
            var effects = new List<BattleStartRelicEffect>(holdings.Relics.Count);
            foreach (RunRelic relic in holdings.Relics)
            {
                if (relic == null || !instanceIds.Add(relic.InstanceId) || !templateIds.Add(relic.TemplateId))
                {
                    throw new InvalidOperationException(
                        "Battle setup contains duplicate or invalid Run relic facts.");
                }

                cfg.run.Relic template = tables.TbRelic.GetOrDefault(relic.TemplateId)
                    ?? throw new InvalidOperationException(
                        $"Run relic template {relic.TemplateId} does not exist.");
                if (template.BattleStartStrength <= 0)
                {
                    throw new InvalidOperationException(
                        $"Run relic template {relic.TemplateId} has an invalid BattleStart Strength value.");
                }

                effects.Add(new BattleStartRelicEffect(
                    relic.InstanceId,
                    relic.TemplateId,
                    ownerId,
                    template.BattleStartStrength));
            }

            return new ReadOnlyCollection<BattleStartRelicEffect>(effects);
        }

        /// <summary>在 StartBattle 写入前预演全部力量累加，拒绝会导致中途溢出的配置组合。</summary>
        private static void ValidateBattleStartRelicStrengthTotal(
            int initialStrength,
            IReadOnlyList<BattleStartRelicEffect> effects)
        {
            try
            {
                int projectedStrength = initialStrength;
                foreach (BattleStartRelicEffect effect in effects)
                    projectedStrength = checked(projectedStrength + effect.StrengthAmount);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Run relic BattleStart Strength total exceeds the supported range.",
                    exception);
            }
        }

        /// <summary>按 Run 槽位顺序解析药水配置，并在缺失、重复实例或非正治疗量时失败关闭。</summary>
        private static BattlePotionLedger ResolveBattlePotionLedger(
            Tables tables,
            RunHoldings holdings,
            CombatantId ownerId)
        {
            if (holdings == null || holdings.Potions.Count == 0)
                return new BattlePotionLedger(Array.Empty<BattlePotionEntry>());

            var instanceIds = new HashSet<RunPotionInstanceId>();
            var entries = new List<BattlePotionEntry>(holdings.Potions.Count);
            foreach (RunPotion potion in holdings.Potions)
            {
                if (potion == null || !instanceIds.Add(potion.InstanceId))
                {
                    throw new InvalidOperationException(
                        "Battle setup contains duplicate or invalid Run potion facts.");
                }

                cfg.run.Potion template = tables.TbPotion.GetOrDefault(potion.TemplateId)
                    ?? throw new InvalidOperationException(
                        $"Run potion template {potion.TemplateId} does not exist.");
                if (template.HealAmount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Run potion template {potion.TemplateId} has an invalid Heal Amount value.");
                }

                entries.Add(new BattlePotionEntry(
                    potion.InstanceId,
                    potion.TemplateId,
                    ownerId,
                    template.HealAmount));
            }

            return new BattlePotionLedger(entries);
        }

        /// <summary>在发布任何战斗事实前验证 Run 实例等级；旧模板牌组统一按零级投影。</summary>
        private static void ValidateCardLevelsForBattle(
            Tables tables,
            RunDeck runDeck,
            IEnumerable<int> legacyCardTemplateIds)
        {
            if (runDeck != null)
            {
                foreach (RunCard card in runDeck.Cards)
                {
                    BattleCardLevelProjection.Create(
                        tables,
                        card.TemplateId,
                        card.UpgradeLevel);
                }

                return;
            }

            foreach (int cardTemplateId in legacyCardTemplateIds)
                BattleCardLevelProjection.Create(tables, cardTemplateId, upgradeLevel: 0);
        }
    }
}
