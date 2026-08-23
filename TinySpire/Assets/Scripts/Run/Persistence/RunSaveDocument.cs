using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>schema v2 允许持久化的稳定 Run 进度阶段。</summary>
    public enum RunSaveProgressPhase
    {
        MapReady,
        BossGateReached,
        Terminal,
    }

    /// <summary>schema v2 允许持久化的类型化 Run 终局原因。</summary>
    public enum RunSaveTerminalReason
    {
        Defeat,
    }

    /// <summary>只保存地图重建配方与稳定进度、不保存整图或 UI 派生数据的 Run DTO。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveDocument
    {
        private readonly ReadOnlyCollection<string> _pathNodeIds;

        public const int CurrentSchemaVersion = 2;

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

        [JsonProperty("deckTemplateId", Required = Required.Always)]
        public int DeckTemplateId { get; }

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

        /// <summary>建立并验证一份只含 schema v2 稳定事实的存档文档。</summary>
        [JsonConstructor]
        public RunSaveDocument(
            int schemaVersion,
            string runId,
            int heroTemplateId,
            int currentHealth,
            int maxHealth,
            int deckTemplateId,
            uint randomRootSeed,
            string mapProfileId,
            int mapGeneratorVersion,
            uint mapSeed,
            string mapFingerprint,
            IReadOnlyList<string> pathNodeIds,
            RunSaveProgressPhase progressPhase,
            string committedNodeId,
            RunSaveTerminalReason? terminalReason)
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
            if (deckTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(deckTemplateId));
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
                terminalReason);

            SchemaVersion = schemaVersion;
            RunId = parsedRunId.ToString("D");
            HeroTemplateId = heroTemplateId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            DeckTemplateId = deckTemplateId;
            RandomRootSeed = randomRootSeed;
            MapProfileId = mapProfileId;
            MapGeneratorVersion = mapGeneratorVersion;
            MapSeed = mapSeed;
            MapFingerprint = mapFingerprint;
            _pathNodeIds = Array.AsReadOnly(pathNodeIds.ToArray());
            ProgressPhase = progressPhase;
            CommittedNodeId = committedNodeId;
            TerminalReason = terminalReason;
        }

        /// <summary>验证存档阶段只表达可恢复地图页、Boss 门或失败终局。</summary>
        private static void ValidateStablePhase(
            int currentHealth,
            RunSaveProgressPhase progressPhase,
            string committedNodeId,
            RunSaveTerminalReason? terminalReason)
        {
            switch (progressPhase)
            {
                case RunSaveProgressPhase.MapReady:
                case RunSaveProgressPhase.BossGateReached:
                    if (currentHealth <= 0 || committedNodeId != null || terminalReason != null)
                        throw new ArgumentException("Non-terminal save progress contains terminal facts.");
                    break;
                case RunSaveProgressPhase.Terminal:
                    if (currentHealth != 0 ||
                        string.IsNullOrWhiteSpace(committedNodeId) ||
                        terminalReason != RunSaveTerminalReason.Defeat)
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

        /// <summary>判断 Encounter 模板是否仍存在。</summary>
        bool EncounterExists(int templateId);

        /// <summary>按稳定 ID 读取当前 Act 地图 profile；不存在时返回空。</summary>
        ActMapProfile GetActMapProfile(string profileId);
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
        /// <summary>只把没有 Battle transient 的地图页、Boss 门或失败终局转换为文档。</summary>
        public static RunSaveDocument Create(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.ActiveBattle != null ||
                (state.ProgressPhase != RunProgressPhase.MapReady &&
                 state.ProgressPhase != RunProgressPhase.BossGateReached &&
                 state.ProgressPhase != RunProgressPhase.Terminal))
            {
                throw new InvalidOperationException("Only stable Run phases can enter a save document.");
            }

            RunSaveProgressPhase progressPhase = ToSaveProgressPhase(state.ProgressPhase);
            RunSaveTerminalReason? terminalReason = state.TerminalReason == null
                ? null
                : RunSaveTerminalReason.Defeat;
            return new RunSaveDocument(
                RunSaveDocument.CurrentSchemaVersion,
                state.RunId.ToString(),
                state.HeroTemplateId,
                state.CurrentHealth,
                state.MaxHealth,
                state.DeckTemplateId,
                state.RandomRootSeed,
                state.MapDefinition.ProfileId,
                state.MapDefinition.GeneratorVersion,
                state.MapDefinition.MapSeed,
                state.MapDefinition.Fingerprint,
                state.PathNodeIds.Select(nodeId => nodeId.Value).ToArray(),
                progressPhase,
                state.CommittedNodeId?.Value,
                terminalReason);
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
                var options = new RunRestoreOptions(
                    new RunId(Guid.ParseExact(document.RunId, "D")),
                    document.HeroTemplateId,
                    document.CurrentHealth,
                    document.MaxHealth,
                    document.DeckTemplateId,
                    document.RandomRootSeed,
                    map,
                    document.PathNodeIds.Select(value => new MapNodeId(value)).ToArray(),
                    ToDomainProgressPhase(document.ProgressPhase),
                    document.CommittedNodeId == null
                        ? (MapNodeId?)null
                        : new MapNodeId(document.CommittedNodeId),
                    document.TerminalReason == null
                        ? (RunTerminalReason?)null
                        : RunTerminalReason.Defeat);
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

        /// <summary>校验 Hero、生命上限与 Deck 静态引用。</summary>
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
            if (!catalog.DeckExists(document.DeckTemplateId))
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.MissingDeckTemplate,
                    $"Deck template {document.DeckTemplateId} does not exist.");
            }

            return null;
        }

        /// <summary>把领域稳定阶段映射为 schema v2 字符串枚举。</summary>
        private static RunSaveProgressPhase ToSaveProgressPhase(RunProgressPhase progressPhase)
        {
            switch (progressPhase)
            {
                case RunProgressPhase.MapReady:
                    return RunSaveProgressPhase.MapReady;
                case RunProgressPhase.BossGateReached:
                    return RunSaveProgressPhase.BossGateReached;
                case RunProgressPhase.Terminal:
                    return RunSaveProgressPhase.Terminal;
                default:
                    throw new InvalidOperationException("Transient Run phases cannot be persisted.");
            }
        }

        /// <summary>把 schema v2 稳定阶段映射为领域进度阶段。</summary>
        private static RunProgressPhase ToDomainProgressPhase(RunSaveProgressPhase progressPhase)
        {
            switch (progressPhase)
            {
                case RunSaveProgressPhase.MapReady:
                    return RunProgressPhase.MapReady;
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
