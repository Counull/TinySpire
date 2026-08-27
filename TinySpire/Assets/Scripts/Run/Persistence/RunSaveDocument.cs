using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>schema v5 允许持久化的稳定 Run 进度阶段。</summary>
    public enum RunSaveProgressPhase
    {
        MapReady,
        RewardPending,
        BossGateReached,
        Terminal,
        NodeVisitPending,
    }

    /// <summary>schema v5 允许持久化的类型化 Run 终局原因。</summary>
    public enum RunSaveTerminalReason
    {
        Defeat,
    }

    /// <summary>只保存一张 RunCard 的实例身份、模板与升级等级。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveCardDocument
    {
        [JsonProperty("instanceId", Required = Required.Always)]
        public int InstanceId { get; }

        [JsonProperty("templateId", Required = Required.Always)]
        public int TemplateId { get; }

        [JsonProperty("upgradeLevel", Required = Required.Always)]
        public int UpgradeLevel { get; }

        /// <summary>建立并验证一张最小实例级存档卡牌。</summary>
        [JsonConstructor]
        public RunSaveCardDocument(int instanceId, int templateId, int upgradeLevel)
        {
            if (instanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (upgradeLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));

            InstanceId = instanceId;
            TemplateId = templateId;
            UpgradeLevel = upgradeLevel;
        }
    }

    /// <summary>只保存一个遗物实例在所属 Run 内的稳定身份与模板引用。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveRelicDocument
    {
        [JsonProperty("instanceId", Required = Required.Always)]
        public int InstanceId { get; }

        [JsonProperty("templateId", Required = Required.Always)]
        public int TemplateId { get; }

        /// <summary>建立并验证一个最小遗物实例存档事实。</summary>
        [JsonConstructor]
        public RunSaveRelicDocument(int instanceId, int templateId)
        {
            if (instanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));

            InstanceId = instanceId;
            TemplateId = templateId;
        }
    }

    /// <summary>只保存一瓶药水在所属 Run 内的稳定身份与模板引用。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSavePotionDocument
    {
        [JsonProperty("instanceId", Required = Required.Always)]
        public int InstanceId { get; }

        [JsonProperty("templateId", Required = Required.Always)]
        public int TemplateId { get; }

        /// <summary>建立并验证一个最小药水实例存档事实。</summary>
        [JsonConstructor]
        public RunSavePotionDocument(int instanceId, int templateId)
        {
            if (instanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));

            InstanceId = instanceId;
            TemplateId = templateId;
        }
    }

    /// <summary>保存卡牌奖励附着的可空遗物与药水模板。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveCardRewardAttachedLootDocument
    {
        [JsonProperty("relicTemplateId", Required = Required.AllowNull)]
        public int? RelicTemplateId { get; }

        [JsonProperty("potionTemplateId", Required = Required.AllowNull)]
        public int? PotionTemplateId { get; }

        /// <summary>验证并冻结两个可空且非空时为正数的附着模板。</summary>
        [JsonConstructor]
        public RunSaveCardRewardAttachedLootDocument(
            int? relicTemplateId,
            int? potionTemplateId)
        {
            if (relicTemplateId.HasValue && relicTemplateId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(relicTemplateId));
            if (potionTemplateId.HasValue && potionTemplateId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(potionTemplateId));

            RelicTemplateId = relicTemplateId;
            PotionTemplateId = potionTemplateId;
        }
    }

    /// <summary>保存可核对的奖励身份、三个有序候选与显式附着掉落。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSavePendingCardRewardDocument
    {
        private readonly ReadOnlyCollection<int> _candidateTemplateIds;

        [JsonProperty("rewardId", Required = Required.Always)]
        public string RewardId { get; }

        /// <summary>按奖励页展示顺序保存的三个冻结候选模板。</summary>
        [JsonProperty("candidateTemplateIds", Required = Required.Always)]
        public IReadOnlyList<int> CandidateTemplateIds => _candidateTemplateIds;

        [JsonProperty("attachedLoot", Required = Required.Always)]
        public RunSaveCardRewardAttachedLootDocument AttachedLoot { get; }

        /// <summary>建立三个候选与永不为空的附着掉落；直接构造省略时显式采用 Empty。</summary>
        [JsonConstructor]
        public RunSavePendingCardRewardDocument(
            string rewardId,
            IReadOnlyList<int> candidateTemplateIds,
            RunSaveCardRewardAttachedLootDocument attachedLoot = null)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
                throw new ArgumentException("Run save reward id cannot be empty.", nameof(rewardId));
            if (candidateTemplateIds == null ||
                candidateTemplateIds.Count != RunCardRewardGenerator.CandidateCount ||
                candidateTemplateIds.Any(templateId => templateId <= 0) ||
                candidateTemplateIds.Distinct().Count() != candidateTemplateIds.Count)
            {
                throw new ArgumentException(
                    "Run save pending reward requires three positive distinct templates.",
                    nameof(candidateTemplateIds));
            }

            RewardId = rewardId;
            _candidateTemplateIds = Array.AsReadOnly(candidateTemplateIds.ToArray());
            RunSaveCardRewardAttachedLootDocument source = attachedLoot ??
                new RunSaveCardRewardAttachedLootDocument(
                    relicTemplateId: null,
                    potionTemplateId: null);
            AttachedLoot = new RunSaveCardRewardAttachedLootDocument(
                source.RelicTemplateId,
                source.PotionTemplateId);
        }
    }

    /// <summary>保存休息点冻结的治疗量与有序升级候选实例。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveRestNodeVisitPayloadDocument
    {
        private readonly ReadOnlyCollection<int> _upgradeCandidateInstanceIds;

        [JsonProperty("healAmount", Required = Required.Always)]
        public int HealAmount { get; }

        [JsonProperty("upgradeCandidateInstanceIds", Required = Required.Always)]
        public IReadOnlyList<int> UpgradeCandidateInstanceIds =>
            _upgradeCandidateInstanceIds;

        /// <summary>验证并冻结休息点的治疗值与有序候选。</summary>
        [JsonConstructor]
        public RunSaveRestNodeVisitPayloadDocument(
            int healAmount,
            IReadOnlyList<int> upgradeCandidateInstanceIds)
        {
            if (healAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(healAmount));
            if (upgradeCandidateInstanceIds == null)
                throw new ArgumentNullException(nameof(upgradeCandidateInstanceIds));
            if (upgradeCandidateInstanceIds.Any(instanceId => instanceId <= 0) ||
                upgradeCandidateInstanceIds.Distinct().Count() !=
                upgradeCandidateInstanceIds.Count)
            {
                throw new ArgumentException(
                    "Rest upgrade candidates must contain distinct positive instance ids.",
                    nameof(upgradeCandidateInstanceIds));
            }

            HealAmount = healAmount;
            _upgradeCandidateInstanceIds = Array.AsReadOnly(
                upgradeCandidateInstanceIds.ToArray());
        }
    }

    /// <summary>保存宝箱冻结的单一药水模板奖励。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveChestNodeVisitPayloadDocument
    {
        [JsonProperty("potionTemplateId", Required = Required.Always)]
        public int PotionTemplateId { get; }

        /// <summary>建立一个正数药水模板奖励事实。</summary>
        [JsonConstructor]
        public RunSaveChestNodeVisitPayloadDocument(int potionTemplateId)
        {
            if (potionTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(potionTemplateId));

            PotionTemplateId = potionTemplateId;
        }
    }

    /// <summary>保存商店库存项的稳定身份、内容域、价格与购买状态。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveShopStockEntryDocument
    {
        [JsonProperty("entryId", Required = Required.Always)]
        public int EntryId { get; }

        [JsonProperty("kind", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RunShopStockKind Kind { get; }

        [JsonProperty("templateId", Required = Required.Always)]
        public int TemplateId { get; }

        [JsonProperty("price", Required = Required.Always)]
        public int Price { get; }

        [JsonProperty("purchased", Required = Required.Always)]
        public bool Purchased { get; }

        /// <summary>验证并冻结一项完整商店库存事实。</summary>
        [JsonConstructor]
        public RunSaveShopStockEntryDocument(
            int entryId,
            RunShopStockKind kind,
            int templateId,
            int price,
            bool purchased)
        {
            if (entryId <= 0)
                throw new ArgumentOutOfRangeException(nameof(entryId));
            if (kind != RunShopStockKind.Relic &&
                kind != RunShopStockKind.Potion &&
                kind != RunShopStockKind.Card)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (price <= 0)
                throw new ArgumentOutOfRangeException(nameof(price));

            EntryId = entryId;
            Kind = kind;
            TemplateId = templateId;
            Price = price;
            Purchased = purchased;
        }
    }

    /// <summary>保存恰好三项有序商店库存。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveShopNodeVisitPayloadDocument
    {
        private readonly ReadOnlyCollection<RunSaveShopStockEntryDocument> _entries;

        [JsonProperty("entries", Required = Required.Always)]
        public IReadOnlyList<RunSaveShopStockEntryDocument> Entries => _entries;

        /// <summary>验证并冻结三项无重复身份的商店库存。</summary>
        [JsonConstructor]
        public RunSaveShopNodeVisitPayloadDocument(
            IReadOnlyList<RunSaveShopStockEntryDocument> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            if (entries.Count != 3)
                throw new ArgumentException(
                    "Shop stock must contain exactly three entries.",
                    nameof(entries));

            var entryIds = new HashSet<int>();
            var frozen = new RunSaveShopStockEntryDocument[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                RunSaveShopStockEntryDocument entry = entries[index]
                    ?? throw new ArgumentException(
                        "Shop stock cannot contain a null entry.",
                        nameof(entries));
                if (!entryIds.Add(entry.EntryId))
                {
                    throw new ArgumentException(
                        "Shop stock cannot contain duplicate entry ids.",
                        nameof(entries));
                }

                frozen[index] = entry;
            }

            _entries = Array.AsReadOnly(frozen);
        }
    }

    /// <summary>保存事件冻结的获得金币与付费治疗两个明确结果。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveEventNodeVisitPayloadDocument
    {
        [JsonProperty("gainGoldAmount", Required = Required.Always)]
        public int GainGoldAmount { get; }

        [JsonProperty("paidHealCost", Required = Required.Always)]
        public int PaidHealCost { get; }

        [JsonProperty("paidHealAmount", Required = Required.Always)]
        public int PaidHealAmount { get; }

        /// <summary>验证并冻结事件的三个正数结算参数。</summary>
        [JsonConstructor]
        public RunSaveEventNodeVisitPayloadDocument(
            int gainGoldAmount,
            int paidHealCost,
            int paidHealAmount)
        {
            if (gainGoldAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(gainGoldAmount));
            if (paidHealCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(paidHealCost));
            if (paidHealAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(paidHealAmount));

            GainGoldAmount = gainGoldAmount;
            PaidHealCost = paidHealCost;
            PaidHealAmount = paidHealAmount;
        }
    }

    /// <summary>保存一次非战斗节点进入后冻结的完整 Pending envelope。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSavePendingNodeVisitDocument
    {
        [JsonProperty("visitId", Required = Required.Always)]
        public string VisitId { get; }

        [JsonProperty("nodeId", Required = Required.Always)]
        public string NodeId { get; }

        [JsonProperty("contentId", Required = Required.Always)]
        public int ContentId { get; }

        [JsonProperty("kind", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public MapNodeKind Kind { get; }

        [JsonProperty("restPayload", Required = Required.AllowNull)]
        public RunSaveRestNodeVisitPayloadDocument RestPayload { get; }

        [JsonProperty("chestPayload", Required = Required.AllowNull)]
        public RunSaveChestNodeVisitPayloadDocument ChestPayload { get; }

        [JsonProperty("shopPayload", Required = Required.AllowNull)]
        public RunSaveShopNodeVisitPayloadDocument ShopPayload { get; }

        [JsonProperty("eventPayload", Required = Required.AllowNull)]
        public RunSaveEventNodeVisitPayloadDocument EventPayload { get; }

        /// <summary>验证身份、类型与唯一匹配 payload 后冻结 Pending envelope。</summary>
        [JsonConstructor]
        public RunSavePendingNodeVisitDocument(
            string visitId,
            string nodeId,
            int contentId,
            MapNodeKind kind,
            RunSaveRestNodeVisitPayloadDocument restPayload,
            RunSaveChestNodeVisitPayloadDocument chestPayload,
            RunSaveShopNodeVisitPayloadDocument shopPayload,
            RunSaveEventNodeVisitPayloadDocument eventPayload)
        {
            if (string.IsNullOrWhiteSpace(visitId))
                throw new ArgumentException("Node visit id cannot be empty.", nameof(visitId));
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
            if (contentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentId));

            ValidatePayloadMatch(kind, restPayload, chestPayload, shopPayload, eventPayload);
            VisitId = visitId;
            NodeId = nodeId;
            ContentId = contentId;
            Kind = kind;
            RestPayload = restPayload;
            ChestPayload = chestPayload;
            ShopPayload = shopPayload;
            EventPayload = eventPayload;
        }

        /// <summary>要求四种非战斗类型恰好携带一个同类型 payload。</summary>
        private static void ValidatePayloadMatch(
            MapNodeKind kind,
            RunSaveRestNodeVisitPayloadDocument restPayload,
            RunSaveChestNodeVisitPayloadDocument chestPayload,
            RunSaveShopNodeVisitPayloadDocument shopPayload,
            RunSaveEventNodeVisitPayloadDocument eventPayload)
        {
            bool matches = kind == MapNodeKind.Rest
                ? restPayload != null && chestPayload == null && shopPayload == null && eventPayload == null
                : kind == MapNodeKind.Chest
                    ? restPayload == null && chestPayload != null && shopPayload == null && eventPayload == null
                    : kind == MapNodeKind.Shop
                        ? restPayload == null && chestPayload == null && shopPayload != null && eventPayload == null
                        : kind == MapNodeKind.Event &&
                          restPayload == null && chestPayload == null && shopPayload == null && eventPayload != null;
            if (!matches)
            {
                throw new ArgumentException(
                    "Pending node visit must carry exactly one payload matching its node kind.");
            }
        }
    }

    /// <summary>只保存有序 RunDeck、地图重建配方与稳定进度的 Run DTO。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveDocument
    {
        private const int MaximumPotionCount = 3;

        private readonly ReadOnlyCollection<string> _pathNodeIds;
        private readonly ReadOnlyCollection<RunSaveCardDocument> _runCards;
        private readonly ReadOnlyCollection<RunSaveRelicDocument> _relics;
        private readonly ReadOnlyCollection<RunSavePotionDocument> _potions;

        public const int CurrentSchemaVersion = 5;

        [JsonProperty("schemaVersion", Required = Required.Always)]
        public int SchemaVersion { get; }

        [JsonProperty("runId", Required = Required.Always)]
        public string RunId { get; }

        [JsonProperty("heroTemplateId", Required = Required.Always)]
        public int HeroTemplateId { get; }

        [JsonProperty("currentHealth", Required = Required.Always)]
        public int CurrentHealth { get; }

        [JsonProperty("maxHealth", Required = Required.Always)]
        public int MaxHealth { get; }

        /// <summary>按 RunDeck 顺序保存的全部实例级卡牌事实。</summary>
        [JsonProperty("runCards", Required = Required.AllowNull)]
        public IReadOnlyList<RunSaveCardDocument> RunCards => _runCards;

        /// <summary>按获得顺序保存的全部遗物实例事实。</summary>
        [JsonProperty("relics", Required = Required.Always)]
        public IReadOnlyList<RunSaveRelicDocument> Relics => _relics;

        /// <summary>按槽位顺序保存的全部药水实例事实。</summary>
        [JsonProperty("potions", Required = Required.Always)]
        public IReadOnlyList<RunSavePotionDocument> Potions => _potions;

        /// <summary>保存当前非负金币数量。</summary>
        [JsonProperty("gold", Required = Required.Always)]
        public int Gold { get; }

        [JsonProperty(
            "legacyDeckTemplateId",
            Required = Required.Default,
            NullValueHandling = NullValueHandling.Ignore)]
        public int? LegacyDeckTemplateId { get; }

        [JsonProperty("randomRootSeed", Required = Required.Always)]
        public uint RandomRootSeed { get; }

        [JsonProperty("mapProfileId", Required = Required.Always)]
        public string MapProfileId { get; }

        [JsonProperty("mapGeneratorVersion", Required = Required.Always)]
        public int MapGeneratorVersion { get; }

        [JsonProperty("mapSeed", Required = Required.Always)]
        public uint MapSeed { get; }

        [JsonProperty("mapFingerprint", Required = Required.Always)]
        public string MapFingerprint { get; }

        [JsonProperty("pathNodeIds", Required = Required.Always)]
        public IReadOnlyList<string> PathNodeIds => _pathNodeIds;

        [JsonProperty("progressPhase", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RunSaveProgressPhase ProgressPhase { get; }

        [JsonProperty("committedNodeId", Required = Required.AllowNull)]
        public string CommittedNodeId { get; }

        [JsonProperty("terminalReason", Required = Required.AllowNull)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RunSaveTerminalReason? TerminalReason { get; }

        [JsonProperty("pendingCardReward", Required = Required.AllowNull)]
        public RunSavePendingCardRewardDocument PendingCardReward { get; }

        [JsonProperty("pendingNodeVisit", Required = Required.AllowNull)]
        public RunSavePendingNodeVisitDocument PendingNodeVisit { get; }

        /// <summary>指示本对象是否由旧 schema 迁移而来，Continue 应先重写 canonical v5。</summary>
        [JsonIgnore]
        public bool RequiresCanonicalRewrite { get; private set; }

        /// <summary>建立并验证一份包含完整持有物的 canonical v5 文档。</summary>
        [JsonConstructor]
        public RunSaveDocument(
            int schemaVersion,
            string runId,
            int heroTemplateId,
            int currentHealth,
            int maxHealth,
            IReadOnlyList<RunSaveCardDocument> runCards,
            int? legacyDeckTemplateId,
            uint randomRootSeed,
            string mapProfileId,
            int mapGeneratorVersion,
            uint mapSeed,
            string mapFingerprint,
            IReadOnlyList<string> pathNodeIds,
            RunSaveProgressPhase progressPhase,
            string committedNodeId,
            RunSaveTerminalReason? terminalReason,
            RunSavePendingCardRewardDocument pendingCardReward,
            IReadOnlyList<RunSaveRelicDocument> relics,
            IReadOnlyList<RunSavePotionDocument> potions,
            int gold,
            RunSavePendingNodeVisitDocument pendingNodeVisit = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (!Guid.TryParseExact(runId, "D", out Guid parsedRunId) || parsedRunId == Guid.Empty)
                throw new ArgumentException("Run save id must be a non-empty D-format Guid.", nameof(runId));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (currentHealth < 0 || currentHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            bool hasRunCards = runCards != null;
            bool hasLegacyDeck = legacyDeckTemplateId.HasValue;
            if (hasRunCards == hasLegacyDeck)
            {
                throw new ArgumentException(
                    "Run save must contain either canonical RunCards or one legacy deck fallback.",
                    nameof(runCards));
            }
            if (hasRunCards && runCards.Count == 0)
                throw new ArgumentException("Run save RunCards cannot be empty.", nameof(runCards));
            if (hasLegacyDeck && legacyDeckTemplateId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(legacyDeckTemplateId));
            if (hasLegacyDeck &&
                (progressPhase == RunSaveProgressPhase.RewardPending || pendingCardReward != null))
            {
                throw new ArgumentException(
                    "Legacy deck fallback cannot contain a pending card reward.",
                    nameof(legacyDeckTemplateId));
            }
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (string.IsNullOrWhiteSpace(mapProfileId))
                throw new ArgumentException("Map profile id cannot be empty.", nameof(mapProfileId));
            if (mapGeneratorVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(mapGeneratorVersion));
            if (mapSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(mapSeed));
            if (!IsSha256Fingerprint(mapFingerprint))
                throw new ArgumentException("Map fingerprint must be lowercase SHA-256 hex.", nameof(mapFingerprint));
            if (pathNodeIds == null || pathNodeIds.Count == 0)
                throw new ArgumentException("Run save path must contain Start.", nameof(pathNodeIds));
            if (pathNodeIds.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Run save path contains an empty node id.", nameof(pathNodeIds));
            if (gold < 0)
                throw new ArgumentOutOfRangeException(nameof(gold));
            ValidateStablePhase(
                currentHealth,
                progressPhase,
                committedNodeId,
                terminalReason,
                pendingCardReward,
                pendingNodeVisit);

            SchemaVersion = schemaVersion;
            RunId = parsedRunId.ToString("D");
            HeroTemplateId = heroTemplateId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            if (runCards == null)
            {
                _runCards = null;
            }
            else
            {
                var seenInstanceIds = new HashSet<int>();
                var frozenCards = new RunSaveCardDocument[runCards.Count];
                for (int index = 0; index < runCards.Count; index++)
                {
                    RunSaveCardDocument card = runCards[index]
                        ?? throw new ArgumentException("Run save cannot contain a null card.", nameof(runCards));
                    if (!seenInstanceIds.Add(card.InstanceId))
                    {
                        throw new ArgumentException(
                            "Run save cannot contain duplicate card instance ids.",
                            nameof(runCards));
                    }

                    frozenCards[index] = card;
                }

                _runCards = Array.AsReadOnly(frozenCards);
            }
            LegacyDeckTemplateId = legacyDeckTemplateId;
            RandomRootSeed = randomRootSeed;
            MapProfileId = mapProfileId;
            MapGeneratorVersion = mapGeneratorVersion;
            MapSeed = mapSeed;
            MapFingerprint = mapFingerprint;
            _pathNodeIds = Array.AsReadOnly(pathNodeIds.ToArray());
            ProgressPhase = progressPhase;
            CommittedNodeId = committedNodeId;
            TerminalReason = terminalReason;
            PendingCardReward = pendingCardReward;
            PendingNodeVisit = pendingNodeVisit;
            _relics = FreezeRelics(relics);
            _potions = FreezePotions(potions);
            Gold = gold;
            if (pendingNodeVisit != null)
            {
                string expectedVisitId =
                    $"{parsedRunId:D}/{pendingNodeVisit.NodeId}";
                if (!string.Equals(
                        pendingNodeVisit.VisitId,
                        expectedVisitId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Pending node visit id does not match the outer Run and node ids.",
                        nameof(pendingNodeVisit));
                }
            }
        }

        /// <summary>仅由 codec 在旧 schema 成功迁移后标记一次性 canonical 重写需求。</summary>
        internal void MarkRequiresCanonicalRewrite()
        {
            RequiresCanonicalRewrite = true;
        }

        /// <summary>验证并冻结有序遗物，拒绝空项、重复实例与重复模板。</summary>
        private static ReadOnlyCollection<RunSaveRelicDocument> FreezeRelics(
            IReadOnlyList<RunSaveRelicDocument> relics)
        {
            if (relics == null)
                throw new ArgumentNullException(nameof(relics));

            var seenInstanceIds = new HashSet<int>();
            var seenTemplateIds = new HashSet<int>();
            var frozen = new RunSaveRelicDocument[relics.Count];
            for (int index = 0; index < relics.Count; index++)
            {
                RunSaveRelicDocument relic = relics[index]
                    ?? throw new ArgumentException(
                        "Run save cannot contain a null relic.",
                        nameof(relics));
                if (!seenInstanceIds.Add(relic.InstanceId))
                {
                    throw new ArgumentException(
                        "Run save cannot contain duplicate relic instance ids.",
                        nameof(relics));
                }
                if (!seenTemplateIds.Add(relic.TemplateId))
                {
                    throw new ArgumentException(
                        "Run save cannot contain duplicate relic templates.",
                        nameof(relics));
                }

                frozen[index] = relic;
            }

            return Array.AsReadOnly(frozen);
        }

        /// <summary>验证并冻结药水槽，拒绝空项、重复实例与超过三瓶的容量。</summary>
        private static ReadOnlyCollection<RunSavePotionDocument> FreezePotions(
            IReadOnlyList<RunSavePotionDocument> potions)
        {
            if (potions == null)
                throw new ArgumentNullException(nameof(potions));
            if (potions.Count > MaximumPotionCount)
            {
                throw new ArgumentException(
                    $"Run save cannot contain more than {MaximumPotionCount} potions.",
                    nameof(potions));
            }

            var seenInstanceIds = new HashSet<int>();
            var frozen = new RunSavePotionDocument[potions.Count];
            for (int index = 0; index < potions.Count; index++)
            {
                RunSavePotionDocument potion = potions[index]
                    ?? throw new ArgumentException(
                        "Run save cannot contain a null potion.",
                        nameof(potions));
                if (!seenInstanceIds.Add(potion.InstanceId))
                {
                    throw new ArgumentException(
                        "Run save cannot contain duplicate potion instance ids.",
                        nameof(potions));
                }

                frozen[index] = potion;
            }

            return Array.AsReadOnly(frozen);
        }

        /// <summary>验证存档阶段只表达可恢复地图页、Boss 门或失败终局。</summary>
        private static void ValidateStablePhase(
            int currentHealth,
            RunSaveProgressPhase progressPhase,
            string committedNodeId,
            RunSaveTerminalReason? terminalReason,
            RunSavePendingCardRewardDocument pendingCardReward,
            RunSavePendingNodeVisitDocument pendingNodeVisit)
        {
            switch (progressPhase)
            {
                case RunSaveProgressPhase.MapReady:
                case RunSaveProgressPhase.BossGateReached:
                    if (currentHealth <= 0 ||
                        committedNodeId != null ||
                        terminalReason != null ||
                        pendingCardReward != null ||
                        pendingNodeVisit != null)
                        throw new ArgumentException("Non-terminal save progress contains terminal facts.");
                    break;
                case RunSaveProgressPhase.RewardPending:
                    if (currentHealth <= 0 ||
                        string.IsNullOrWhiteSpace(committedNodeId) ||
                        terminalReason != null ||
                        pendingCardReward == null ||
                        pendingNodeVisit != null)
                    {
                        throw new ArgumentException(
                            "RewardPending save requires health, committed node and frozen reward.");
                    }
                    break;
                case RunSaveProgressPhase.Terminal:
                    if (currentHealth != 0 ||
                        string.IsNullOrWhiteSpace(committedNodeId) ||
                        terminalReason != RunSaveTerminalReason.Defeat ||
                        pendingCardReward != null ||
                        pendingNodeVisit != null)
                    {
                        throw new ArgumentException("Terminal save must be Terminal(Defeat) with its failed node.");
                    }
                    break;
                case RunSaveProgressPhase.NodeVisitPending:
                    if (currentHealth <= 0 ||
                        committedNodeId != null ||
                        terminalReason != null ||
                        pendingCardReward != null ||
                        pendingNodeVisit == null)
                    {
                        throw new ArgumentException(
                            "NodeVisitPending save requires health and one frozen node visit.");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(progressPhase));
            }
        }

        /// <summary>确认字符串是规范化的小写 SHA-256 十六进制。</summary>
        private static bool IsSha256Fingerprint(string value)
        {
            return value != null &&
                   value.Length == 64 &&
                   value.All(character =>
                       (character >= '0' && character <= '9') ||
                       (character >= 'a' && character <= 'f'));
        }
    }

    /// <summary>为读档领域校验提供当前配置与 Act 地图 profile 的只读查询。</summary>
    public interface IRunSaveConfigurationCatalog
    {
        /// <summary>判断 Hero 模板是否仍存在。</summary>
        bool HeroExists(int templateId);

        /// <summary>读取当前 Hero 配置的生命上限。</summary>
        int GetHeroMaxHealth(int templateId);

        /// <summary>判断 Deck 模板是否仍存在。</summary>
        bool DeckExists(int templateId);

        /// <summary>按配置顺序读取已确认存在 Deck 的初始卡牌模板。</summary>
        IReadOnlyList<int> GetDeckCardTemplateIds(int templateId);

        /// <summary>判断 Card 模板是否仍存在。</summary>
        bool CardExists(int templateId);

        /// <summary>判断 Relic 模板是否仍存在。</summary>
        bool RelicExists(int templateId);

        /// <summary>判断 Potion 模板是否仍存在。</summary>
        bool PotionExists(int templateId);

        /// <summary>判断 Card 仍是当前 Hero 配置中 Implemented 且可奖励的显式候选。</summary>
        bool IsRewardCardForHero(int heroTemplateId, int cardTemplateId);

        /// <summary>判断 Encounter 模板是否仍存在。</summary>
        bool EncounterExists(int templateId);

        /// <summary>按稳定 ID 读取当前 Act 地图 profile；不存在时返回空。</summary>
        ActMapProfile GetActMapProfile(string profileId);
    }

    /// <summary>为实例升级与非零等级读档提供同一份配置驱动合法性判断。</summary>
    public interface IRunCardUpgradeConfigurationCatalog
    {
        /// <summary>判断指定卡牌模板的完整升级等级是否存在且可投影。</summary>
        bool IsCardUpgradeLevelValid(int templateId, int upgradeLevel);
    }

    /// <summary>存档文档恢复为领域输入时的类型化结果。</summary>
    public enum RunSaveRestoreStatus
    {
        Success,
        InvalidDocument,
        MissingHeroTemplate,
        MissingDeckTemplate,
        MissingEncounterTemplate,
        MissingMapProfile,
    }

    /// <summary>冻结恢复输入或明确失败原因，不以默认配置掩盖坏档。</summary>
    public sealed class RunSaveRestoreResult
    {
        public RunSaveRestoreStatus Status { get; }
        public RunRestoreOptions Options { get; }
        public string Detail { get; }

        /// <summary>建立一次成功或失败的不可变恢复结果。</summary>
        private RunSaveRestoreResult(
            RunSaveRestoreStatus status,
            RunRestoreOptions options,
            string detail)
        {
            Status = status;
            Options = options;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回包含完整恢复输入的成功结果。</summary>
        internal static RunSaveRestoreResult Succeeded(RunRestoreOptions options)
        {
            return new RunSaveRestoreResult(
                RunSaveRestoreStatus.Success,
                options ?? throw new ArgumentNullException(nameof(options)),
                string.Empty);
        }

        /// <summary>返回不携带恢复输入的显式失败结果。</summary>
        internal static RunSaveRestoreResult Failed(
            RunSaveRestoreStatus status,
            string detail)
        {
            if (status == RunSaveRestoreStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new RunSaveRestoreResult(status, null, detail);
        }
    }

    /// <summary>在稳定 RunState 与地图配方 Save Document 之间执行唯一领域映射。</summary>
    public static class RunSaveDocumentMapper
    {
        /// <summary>把已验证的恢复输入投影为 canonical 文档，供 legacy Continue 先落盘再发布。</summary>
        public static RunSaveDocument Create(RunRestoreOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            return Create(new RunState(options));
        }

        /// <summary>只把没有 Battle transient 的地图页、冻结奖励、Boss 门或失败终局转换为文档。</summary>
        public static RunSaveDocument Create(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.ActiveBattle != null ||
                (state.ProgressPhase != RunProgressPhase.MapReady &&
                 state.ProgressPhase != RunProgressPhase.RewardPending &&
                 state.ProgressPhase != RunProgressPhase.BossGateReached &&
                 state.ProgressPhase != RunProgressPhase.Terminal &&
                 state.ProgressPhase != RunProgressPhase.NodeVisitPending))
            {
                throw new InvalidOperationException("Only stable Run phases can enter a save document.");
            }

            RunSaveProgressPhase progressPhase = ToSaveProgressPhase(state.ProgressPhase);
            RunSaveTerminalReason? terminalReason = state.TerminalReason == null
                ? null
                : RunSaveTerminalReason.Defeat;
            RunSavePendingCardRewardDocument pendingCardReward = state.PendingCardReward == null
                ? null
                : new RunSavePendingCardRewardDocument(
                    state.PendingCardReward.Id.ToString(),
                    state.PendingCardReward.CandidateTemplateIds,
                    new RunSaveCardRewardAttachedLootDocument(
                        state.PendingCardReward.AttachedLoot.RelicTemplateId,
                        state.PendingCardReward.AttachedLoot.PotionTemplateId));
            RunSavePendingNodeVisitDocument pendingNodeVisit =
                CreatePendingNodeVisitDocument(state.PendingNodeVisit);
            return new RunSaveDocument(
                RunSaveDocument.CurrentSchemaVersion,
                state.RunId.ToString(),
                state.HeroTemplateId,
                state.CurrentHealth,
                state.MaxHealth,
                state.RunDeck.Cards.Select(card => new RunSaveCardDocument(
                    card.InstanceId.Sequence,
                    card.TemplateId,
                    card.UpgradeLevel)).ToArray(),
                legacyDeckTemplateId: null,
                state.RandomRootSeed,
                state.MapDefinition.ProfileId,
                state.MapDefinition.GeneratorVersion,
                state.MapDefinition.MapSeed,
                state.MapDefinition.Fingerprint,
                state.PathNodeIds.Select(nodeId => nodeId.Value).ToArray(),
                progressPhase,
                state.CommittedNodeId?.Value,
                terminalReason,
                pendingCardReward,
                state.Holdings.Relics.Select(relic => new RunSaveRelicDocument(
                    relic.InstanceId.Sequence,
                    relic.TemplateId)).ToArray(),
                state.Holdings.Potions.Select(potion => new RunSavePotionDocument(
                    potion.InstanceId.Sequence,
                    potion.TemplateId)).ToArray(),
                state.Holdings.Gold,
                pendingNodeVisit);
        }

        /// <summary>校验配置与地图配方，重建整图并比对指纹后创建 Store 恢复输入。</summary>
        public static RunSaveRestoreResult CreateRestore(
            RunSaveDocument document,
            IRunSaveConfigurationCatalog catalog)
        {
            if (document == null)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    "Run save document is missing.");
            }
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            RunSaveRestoreResult configurationFailure = ValidateConfiguration(document, catalog);
            if (configurationFailure != null)
                return configurationFailure;

            ActMapProfile profile = catalog.GetActMapProfile(document.MapProfileId);
            if (profile == null)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.MissingMapProfile,
                    $"Map profile '{document.MapProfileId}' does not exist.");
            }
            if (document.MapGeneratorVersion != profile.GeneratorVersion)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Map generator version {document.MapGeneratorVersion} does not match " +
                    $"profile '{profile.ProfileId}' version {profile.GeneratorVersion}.");
            }

            MapDefinition map;
            try
            {
                map = ActMapGenerator.Generate(profile, document.MapSeed);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Map recipe could not be rebuilt: {exception.Message}");
            }

            MapValidationResult validation = ActMapValidator.Validate(map, profile);
            if (!validation.IsValid)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Rebuilt map is invalid: {validation.Errors[0].Message}");
            }
            if (!string.Equals(map.Fingerprint, document.MapFingerprint, StringComparison.Ordinal))
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    "Rebuilt map fingerprint does not match the save document.");
            }

            foreach (MapNode combat in map.Nodes.Where(node => node.Kind == MapNodeKind.Combat))
            {
                if (!catalog.EncounterExists(combat.ContentId))
                {
                    return RunSaveRestoreResult.Failed(
                        RunSaveRestoreStatus.MissingEncounterTemplate,
                        $"Encounter template {combat.ContentId} does not exist.");
                }
            }

            try
            {
                RunDeck runDeck = document.RunCards == null
                    ? CreateLegacyRunDeck(document.LegacyDeckTemplateId.Value, catalog)
                    : new RunDeck(document.RunCards.Select(card => new RunCard(
                        new RunCardInstanceId(card.InstanceId),
                        card.TemplateId,
                        card.UpgradeLevel)));
                var runId = new RunId(Guid.ParseExact(document.RunId, "D"));
                MapNodeId[] pathNodeIds = document.PathNodeIds
                    .Select(value => new MapNodeId(value))
                    .ToArray();
                RunProgressPhase progressPhase = ToDomainProgressPhase(document.ProgressPhase);
                MapNodeId? committedNodeId = document.CommittedNodeId == null
                    ? (MapNodeId?)null
                    : new MapNodeId(document.CommittedNodeId);
                PendingCardReward pendingCardReward = CreatePendingCardReward(
                    document,
                    runId,
                    map,
                    pathNodeIds,
                    progressPhase,
                    committedNodeId);
                PendingRunNodeVisit pendingNodeVisit = CreatePendingNodeVisit(
                    document.PendingNodeVisit,
                    runId);
                var holdings = new RunHoldings(
                    document.Relics.Select(relic => new RunRelic(
                        new RunRelicInstanceId(relic.InstanceId),
                        relic.TemplateId)),
                    document.Potions.Select(potion => new RunPotion(
                        new RunPotionInstanceId(potion.InstanceId),
                        potion.TemplateId)),
                    document.Gold);
                var options = new RunRestoreOptions(
                    runId,
                    document.HeroTemplateId,
                    document.CurrentHealth,
                    document.MaxHealth,
                    runDeck,
                    document.RandomRootSeed,
                    map,
                    pathNodeIds,
                    progressPhase,
                    committedNodeId,
                    document.TerminalReason == null
                        ? (RunTerminalReason?)null
                        : RunTerminalReason.Defeat,
                    holdings,
                    pendingCardReward,
                    pendingNodeVisit);
                using var validationStore = new RunStateStore();
                RunState restored = validationStore.RestoreRun(options);
                ValidatePendingCardRewardAuthority(restored);
                ValidatePendingNodeVisitAuthority(restored, catalog);
                return RunSaveRestoreResult.Succeeded(options);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is KeyNotFoundException)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Run progress is invalid: {exception.Message}");
            }
        }

        /// <summary>从已完成普通战斗路径与当前持有物重建附着掉落，拒绝冷档重复获利或删除应冻结事实。</summary>
        private static void ValidatePendingCardRewardAuthority(RunState restored)
        {
            if (restored.PendingCardReward == null)
                return;

            int completedOrdinaryCombatCount = restored.PathNodeIds.Count(nodeId =>
                restored.MapDefinition.GetNode(nodeId).Kind == MapNodeKind.Combat);
            int? expectedRelicTemplateId = null;
            int? expectedPotionTemplateId = null;
            if (completedOrdinaryCombatCount == 0)
            {
                if (!restored.Holdings.Relics.Any(relic =>
                        relic.TemplateId ==
                        RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic))
                {
                    expectedRelicTemplateId =
                        RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic;
                }
                if (restored.Holdings.Potions.Count < 3)
                {
                    expectedPotionTemplateId =
                        RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattlePotion;
                }
            }

            RunCardRewardAttachedLoot actual = restored.PendingCardReward.AttachedLoot;
            if (actual.RelicTemplateId != expectedRelicTemplateId ||
                actual.PotionTemplateId != expectedPotionTemplateId)
            {
                throw new InvalidOperationException(
                    "Pending card reward attached loot does not match authoritative Run facts.");
            }
        }

        /// <summary>从恢复后的 Run、冻结地图与当前配置重建非战斗初始事实，逐字段拒绝伪造存档。</summary>
        private static void ValidatePendingNodeVisitAuthority(
            RunState restored,
            IRunSaveConfigurationCatalog catalog)
        {
            if (restored.PendingNodeVisit == null)
                return;
            if (!(catalog is IRunNodeVisitEntryCatalog entryCatalog))
            {
                throw new InvalidOperationException(
                    "Pending node visit restore requires an authoritative entry catalog.");
            }

            MapNode node = restored.MapDefinition.GetNode(restored.PendingNodeVisit.NodeId);
            PendingRunNodeVisit expected = RunNodeVisitEntryFactory.Create(
                restored,
                node,
                entryCatalog);
            if (!RunNodeVisitEntryFactory.HasSameFrozenFacts(
                    restored.PendingNodeVisit,
                    expected))
            {
                throw new InvalidOperationException(
                    "Pending node visit payload does not match authoritative entry facts.");
            }
        }

        /// <summary>校验 Hero、生命上限、卡牌与 Run 持有物的全部静态配置引用。</summary>
        private static RunSaveRestoreResult ValidateConfiguration(
            RunSaveDocument document,
            IRunSaveConfigurationCatalog catalog)
        {
            if (!catalog.HeroExists(document.HeroTemplateId))
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.MissingHeroTemplate,
                    $"Hero template {document.HeroTemplateId} does not exist.");
            }

            int configuredMaxHealth = catalog.GetHeroMaxHealth(document.HeroTemplateId);
            if (configuredMaxHealth <= 0 || document.MaxHealth != configuredMaxHealth)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Run save max health {document.MaxHealth} does not match current Hero " +
                    $"template {document.HeroTemplateId} max health {configuredMaxHealth}.");
            }
            if (document.LegacyDeckTemplateId.HasValue &&
                !catalog.DeckExists(document.LegacyDeckTemplateId.Value))
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.MissingDeckTemplate,
                    $"Deck template {document.LegacyDeckTemplateId.Value} does not exist.");
            }
            if (document.RunCards != null)
            {
                foreach (RunSaveCardDocument card in document.RunCards)
                {
                    if (!catalog.CardExists(card.TemplateId))
                    {
                        return RunSaveRestoreResult.Failed(
                            RunSaveRestoreStatus.InvalidDocument,
                            $"Card template {card.TemplateId} does not exist.");
                    }

                    var upgradeCatalog = catalog as IRunCardUpgradeConfigurationCatalog;
                    bool upgradeLevelValid = upgradeCatalog == null
                        ? card.UpgradeLevel == 0
                        : upgradeCatalog.IsCardUpgradeLevelValid(
                            card.TemplateId,
                            card.UpgradeLevel);
                    if (!upgradeLevelValid)
                    {
                        return RunSaveRestoreResult.Failed(
                            RunSaveRestoreStatus.InvalidDocument,
                            $"Card instance {card.InstanceId} template {card.TemplateId} " +
                            $"upgrade level {card.UpgradeLevel} is not valid.");
                    }
                }
            }
            if (document.PendingCardReward != null)
            {
                foreach (int cardTemplateId in document.PendingCardReward.CandidateTemplateIds)
                {
                    if (!catalog.CardExists(cardTemplateId))
                    {
                        return RunSaveRestoreResult.Failed(
                            RunSaveRestoreStatus.InvalidDocument,
                            $"Pending reward card template {cardTemplateId} does not exist.");
                    }
                    if (!catalog.IsRewardCardForHero(document.HeroTemplateId, cardTemplateId))
                    {
                        return RunSaveRestoreResult.Failed(
                            RunSaveRestoreStatus.InvalidDocument,
                            $"Pending reward card template {cardTemplateId} is not in Hero " +
                            $"{document.HeroTemplateId}'s reward pool.");
                    }
                }
            }

            return ValidateRunItemConfiguration(document, catalog);
        }

        /// <summary>校验现有持有物以及待结算奖励附着掉落引用的遗物与药水模板。</summary>
        private static RunSaveRestoreResult ValidateRunItemConfiguration(
            RunSaveDocument document,
            IRunSaveConfigurationCatalog catalog)
        {
            foreach (RunSaveRelicDocument relic in document.Relics)
            {
                if (!catalog.RelicExists(relic.TemplateId))
                {
                    return RunSaveRestoreResult.Failed(
                        RunSaveRestoreStatus.InvalidDocument,
                        $"Relic template {relic.TemplateId} does not exist.");
                }
            }

            foreach (RunSavePotionDocument potion in document.Potions)
            {
                if (!catalog.PotionExists(potion.TemplateId))
                {
                    return RunSaveRestoreResult.Failed(
                        RunSaveRestoreStatus.InvalidDocument,
                        $"Potion template {potion.TemplateId} does not exist.");
                }
            }

            RunSaveCardRewardAttachedLootDocument attachedLoot =
                document.PendingCardReward?.AttachedLoot;
            if (attachedLoot?.RelicTemplateId is int relicTemplateId &&
                !catalog.RelicExists(relicTemplateId))
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Pending reward attached Relic template {relicTemplateId} does not exist.");
            }
            if (attachedLoot?.PotionTemplateId is int potionTemplateId &&
                !catalog.PotionExists(potionTemplateId))
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Pending reward attached Potion template {potionTemplateId} does not exist.");
            }

            return null;
        }

        /// <summary>从 outer Run/phase 事实重建奖励身份，并拒绝存档中的伪造 ID。</summary>
        private static PendingCardReward CreatePendingCardReward(
            RunSaveDocument document,
            RunId runId,
            MapDefinition map,
            IReadOnlyList<MapNodeId> pathNodeIds,
            RunProgressPhase progressPhase,
            MapNodeId? committedNodeId)
        {
            if (document.PendingCardReward == null)
                return null;
            if (committedNodeId == null)
                throw new InvalidOperationException("Pending reward restore requires a committed node.");

            int attemptSequence = RunRestoreOptions.DeriveBattleAttemptSequence(
                map,
                pathNodeIds,
                progressPhase);
            var rewardId = new RunCardRewardId(new RunBattleId(
                runId,
                attemptSequence,
                committedNodeId.Value));
            if (!string.Equals(
                    document.PendingCardReward.RewardId,
                    rewardId.ToString(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Pending reward id does not match the Run battle facts.");
            }

            return new PendingCardReward(
                rewardId,
                document.PendingCardReward.CandidateTemplateIds,
                new RunCardRewardAttachedLoot(
                    document.PendingCardReward.AttachedLoot.RelicTemplateId,
                    document.PendingCardReward.AttachedLoot.PotionTemplateId));
        }

        /// <summary>把领域 Pending 节点访问逐字段投影为显式 DTO，并保留所有有序 payload。</summary>
        private static RunSavePendingNodeVisitDocument CreatePendingNodeVisitDocument(
            PendingRunNodeVisit pendingNodeVisit)
        {
            if (pendingNodeVisit == null)
                return null;

            RunSaveRestNodeVisitPayloadDocument restPayload = pendingNodeVisit.RestPayload == null
                ? null
                : new RunSaveRestNodeVisitPayloadDocument(
                    pendingNodeVisit.RestPayload.HealAmount,
                    pendingNodeVisit.RestPayload.UpgradeCandidateInstanceIds
                        .Select(instanceId => instanceId.Sequence)
                        .ToArray());
            RunSaveChestNodeVisitPayloadDocument chestPayload =
                pendingNodeVisit.ChestPayload == null
                    ? null
                    : new RunSaveChestNodeVisitPayloadDocument(
                        pendingNodeVisit.ChestPayload.PotionTemplateId);
            RunSaveShopNodeVisitPayloadDocument shopPayload = pendingNodeVisit.ShopPayload == null
                ? null
                : new RunSaveShopNodeVisitPayloadDocument(
                    pendingNodeVisit.ShopPayload.Entries
                        .Select(entry => new RunSaveShopStockEntryDocument(
                            entry.EntryId,
                            entry.Kind,
                            entry.TemplateId,
                            entry.Price,
                            entry.Purchased))
                        .ToArray());
            RunSaveEventNodeVisitPayloadDocument eventPayload =
                pendingNodeVisit.EventPayload == null
                    ? null
                    : new RunSaveEventNodeVisitPayloadDocument(
                        pendingNodeVisit.EventPayload.GainGoldAmount,
                        pendingNodeVisit.EventPayload.PaidHealCost,
                        pendingNodeVisit.EventPayload.PaidHealAmount);
            return new RunSavePendingNodeVisitDocument(
                pendingNodeVisit.Id.ToString(),
                pendingNodeVisit.NodeId.Value,
                pendingNodeVisit.ContentId,
                pendingNodeVisit.Kind,
                restPayload,
                chestPayload,
                shopPayload,
                eventPayload);
        }

        /// <summary>从 DTO 重建领域 Pending 节点访问，并再次核对 outer Run 与节点组成的身份。</summary>
        private static PendingRunNodeVisit CreatePendingNodeVisit(
            RunSavePendingNodeVisitDocument document,
            RunId runId)
        {
            if (document == null)
                return null;

            var nodeId = new MapNodeId(document.NodeId);
            var visitId = new RunNodeVisitId(runId, nodeId);
            if (!string.Equals(document.VisitId, visitId.ToString(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Pending node visit id does not match the outer Run and node facts.");
            }

            switch (document.Kind)
            {
                case MapNodeKind.Rest:
                    return PendingRunNodeVisit.CreateRest(
                        visitId,
                        document.ContentId,
                        document.RestPayload.HealAmount,
                        document.RestPayload.UpgradeCandidateInstanceIds
                            .Select(sequence => new RunCardInstanceId(sequence)));
                case MapNodeKind.Chest:
                    return PendingRunNodeVisit.CreateChest(
                        visitId,
                        document.ContentId,
                        document.ChestPayload.PotionTemplateId);
                case MapNodeKind.Shop:
                    return PendingRunNodeVisit.CreateShop(
                        visitId,
                        document.ContentId,
                        document.ShopPayload.Entries.Select(entry => new RunShopStockEntry(
                            entry.EntryId,
                            entry.Kind,
                            entry.TemplateId,
                            entry.Price,
                            entry.Purchased)));
                case MapNodeKind.Event:
                    return PendingRunNodeVisit.CreateEvent(
                        visitId,
                        document.ContentId,
                        document.EventPayload.GainGoldAmount,
                        document.EventPayload.PaidHealCost,
                        document.EventPayload.PaidHealAmount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(document.Kind));
            }
        }

        /// <summary>只读取一次旧 Deck 配置，逐卡校验当前模板后展开稳定 RunCard 身份。</summary>
        private static RunDeck CreateLegacyRunDeck(
            int deckTemplateId,
            IRunSaveConfigurationCatalog catalog)
        {
            IReadOnlyList<int> cardTemplateIds = catalog.GetDeckCardTemplateIds(deckTemplateId);
            if (cardTemplateIds == null)
                throw new ArgumentException($"Deck template {deckTemplateId} has no card list.");

            foreach (int cardTemplateId in cardTemplateIds)
            {
                if (!catalog.CardExists(cardTemplateId))
                {
                    throw new ArgumentException(
                        $"Card template {cardTemplateId} referenced by legacy deck " +
                        $"{deckTemplateId} does not exist.");
                }
            }

            return RunDeck.CreateInitial(cardTemplateIds);
        }

        /// <summary>把领域稳定阶段映射为 schema v5 字符串枚举。</summary>
        private static RunSaveProgressPhase ToSaveProgressPhase(RunProgressPhase progressPhase)
        {
            switch (progressPhase)
            {
                case RunProgressPhase.MapReady:
                    return RunSaveProgressPhase.MapReady;
                case RunProgressPhase.RewardPending:
                    return RunSaveProgressPhase.RewardPending;
                case RunProgressPhase.BossGateReached:
                    return RunSaveProgressPhase.BossGateReached;
                case RunProgressPhase.Terminal:
                    return RunSaveProgressPhase.Terminal;
                case RunProgressPhase.NodeVisitPending:
                    return RunSaveProgressPhase.NodeVisitPending;
                default:
                    throw new InvalidOperationException("Transient Run phases cannot be persisted.");
            }
        }

        /// <summary>把 schema v5 稳定阶段映射为领域进度阶段。</summary>
        private static RunProgressPhase ToDomainProgressPhase(RunSaveProgressPhase progressPhase)
        {
            switch (progressPhase)
            {
                case RunSaveProgressPhase.MapReady:
                    return RunProgressPhase.MapReady;
                case RunSaveProgressPhase.RewardPending:
                    return RunProgressPhase.RewardPending;
                case RunSaveProgressPhase.BossGateReached:
                    return RunProgressPhase.BossGateReached;
                case RunSaveProgressPhase.Terminal:
                    return RunProgressPhase.Terminal;
                case RunSaveProgressPhase.NodeVisitPending:
                    return RunProgressPhase.NodeVisitPending;
                default:
                    throw new ArgumentOutOfRangeException(nameof(progressPhase));
            }
        }
    }
}
