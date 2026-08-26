using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>schema v4 允许持久化的稳定 Run 进度阶段。</summary>
    public enum RunSaveProgressPhase
    {
        MapReady,
        RewardPending,
        BossGateReached,
        Terminal,
    }

    /// <summary>schema v4 允许持久化的类型化 Run 终局原因。</summary>
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

    /// <summary>只保存可核对的奖励身份文本与三个有序候选模板。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSavePendingCardRewardDocument
    {
        private readonly ReadOnlyCollection<int> _candidateTemplateIds;

        [JsonProperty("rewardId", Required = Required.Always)]
        public string RewardId { get; }

        /// <summary>按奖励页展示顺序保存的三个冻结候选模板。</summary>
        [JsonProperty("candidateTemplateIds", Required = Required.Always)]
        public IReadOnlyList<int> CandidateTemplateIds => _candidateTemplateIds;

        /// <summary>建立恰好三个正数且不同模板的最小 Pending DTO。</summary>
        [JsonConstructor]
        public RunSavePendingCardRewardDocument(
            string rewardId,
            IReadOnlyList<int> candidateTemplateIds)
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
        }
    }

    /// <summary>只保存有序 RunDeck、地图重建配方与稳定进度的 Run DTO。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveDocument
    {
        private readonly ReadOnlyCollection<string> _pathNodeIds;
        private readonly ReadOnlyCollection<RunSaveCardDocument> _runCards;

        public const int CurrentSchemaVersion = 4;

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

        /// <summary>建立并验证一份 canonical RunDeck 或一次性 legacy deck fallback 文档。</summary>
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
            RunSavePendingCardRewardDocument pendingCardReward = null)
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
            ValidateStablePhase(
                currentHealth,
                progressPhase,
                committedNodeId,
                terminalReason,
                pendingCardReward);

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
        }

        /// <summary>验证存档阶段只表达可恢复地图页、Boss 门或失败终局。</summary>
        private static void ValidateStablePhase(
            int currentHealth,
            RunSaveProgressPhase progressPhase,
            string committedNodeId,
            RunSaveTerminalReason? terminalReason,
            RunSavePendingCardRewardDocument pendingCardReward)
        {
            switch (progressPhase)
            {
                case RunSaveProgressPhase.MapReady:
                case RunSaveProgressPhase.BossGateReached:
                    if (currentHealth <= 0 ||
                        committedNodeId != null ||
                        terminalReason != null ||
                        pendingCardReward != null)
                        throw new ArgumentException("Non-terminal save progress contains terminal facts.");
                    break;
                case RunSaveProgressPhase.RewardPending:
                    if (currentHealth <= 0 ||
                        string.IsNullOrWhiteSpace(committedNodeId) ||
                        terminalReason != null ||
                        pendingCardReward == null)
                    {
                        throw new ArgumentException(
                            "RewardPending save requires health, committed node and frozen reward.");
                    }
                    break;
                case RunSaveProgressPhase.Terminal:
                    if (currentHealth != 0 ||
                        string.IsNullOrWhiteSpace(committedNodeId) ||
                        terminalReason != RunSaveTerminalReason.Defeat ||
                        pendingCardReward != null)
                    {
                        throw new ArgumentException("Terminal save must be Terminal(Defeat) with its failed node.");
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
                 state.ProgressPhase != RunProgressPhase.Terminal))
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
                    state.PendingCardReward.CandidateTemplateIds);
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
                pendingCardReward);
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
            if (document.MapGeneratorVersion != ActMapGenerator.CurrentVersion)
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.InvalidDocument,
                    $"Map generator version {document.MapGeneratorVersion} is unsupported.");
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
                    pendingCardReward);
                using var validationStore = new RunStateStore();
                validationStore.RestoreRun(options);
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

        /// <summary>校验 Hero、生命上限与 canonical Card 或 legacy Deck 静态引用。</summary>
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
                document.PendingCardReward.CandidateTemplateIds);
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

        /// <summary>把领域稳定阶段映射为 schema v4 字符串枚举。</summary>
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
                default:
                    throw new InvalidOperationException("Transient Run phases cannot be persisted.");
            }
        }

        /// <summary>把 schema v4 稳定阶段映射为领域进度阶段。</summary>
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(progressPhase));
            }
        }
    }
}
