using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TinySpire.Run
{
    /// <summary>v1 存档允许持久化的地图稳定节点状态。</summary>
    public enum RunSaveNodeStatus
    {
        Available,
        Completed,
    }

    /// <summary>只包含一份地图稳定态事实的显式版本化 Run 存档 DTO。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RunSaveDocument
    {
        public const int CurrentSchemaVersion = 1;

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

        [JsonProperty("encounterTemplateId", Required = Required.Always)]
        public int EncounterTemplateId { get; }

        [JsonProperty("randomRootSeed", Required = Required.Always)]
        public uint RandomRootSeed { get; }

        [JsonProperty("nodeStatus", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RunSaveNodeStatus NodeStatus { get; }

        [JsonProperty("battleAttemptSequence", Required = Required.Always)]
        public int BattleAttemptSequence { get; }

        /// <summary>建立并验证一份只含当前 schema 稳定事实的存档文档。</summary>
        [JsonConstructor]
        public RunSaveDocument(
            int schemaVersion,
            string runId,
            int heroTemplateId,
            int currentHealth,
            int maxHealth,
            int deckTemplateId,
            int encounterTemplateId,
            uint randomRootSeed,
            RunSaveNodeStatus nodeStatus,
            int battleAttemptSequence)
        {
            if (schemaVersion != CurrentSchemaVersion)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (!Guid.TryParseExact(runId, "D", out Guid parsedRunId) || parsedRunId == Guid.Empty)
                throw new ArgumentException("Run save id must be a non-empty D-format Guid.", nameof(runId));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (currentHealth <= 0 || currentHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            if (deckTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(deckTemplateId));
            if (encounterTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(encounterTemplateId));
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (nodeStatus != RunSaveNodeStatus.Available &&
                nodeStatus != RunSaveNodeStatus.Completed)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeStatus));
            }
            if (battleAttemptSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(battleAttemptSequence));

            SchemaVersion = schemaVersion;
            RunId = parsedRunId.ToString("D");
            HeroTemplateId = heroTemplateId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            DeckTemplateId = deckTemplateId;
            EncounterTemplateId = encounterTemplateId;
            RandomRootSeed = randomRootSeed;
            NodeStatus = nodeStatus;
            BattleAttemptSequence = battleAttemptSequence;
        }
    }

    /// <summary>为读档领域校验提供当前配置 ID 的只读存在性查询。</summary>
    public interface IRunSaveConfigurationCatalog
    {
        /// <summary>判断 Hero 模板是否仍存在。</summary>
        bool HeroExists(int templateId);

        /// <summary>读取当前 Hero 配置的生命上限，用于拒绝无法安全接入 Battle 的漂移存档。</summary>
        int GetHeroMaxHealth(int templateId);

        /// <summary>判断 Deck 模板是否仍存在。</summary>
        bool DeckExists(int templateId);

        /// <summary>判断 Encounter 模板是否仍存在。</summary>
        bool EncounterExists(int templateId);
    }

    /// <summary>存档文档恢复为领域输入时的类型化结果。</summary>
    public enum RunSaveRestoreStatus
    {
        Success,
        InvalidDocument,
        MissingHeroTemplate,
        MissingDeckTemplate,
        MissingEncounterTemplate,
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

    /// <summary>在稳定 RunState 与显式 Save Document 之间执行唯一领域映射。</summary>
    public static class RunSaveDocumentMapper
    {
        /// <summary>只把没有战斗暂存事实的 Available/Completed 地图稳定态转换为文档。</summary>
        public static RunSaveDocument Create(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.ActiveBattle != null || state.BattleSnapshot != null)
                throw new InvalidOperationException("Battle transient facts cannot enter a Run save document.");

            RunSaveNodeStatus nodeStatus;
            switch (state.NodeStatus)
            {
                case RunNodeStatus.Available:
                    nodeStatus = RunSaveNodeStatus.Available;
                    break;
                case RunNodeStatus.Completed:
                    nodeStatus = RunSaveNodeStatus.Completed;
                    break;
                default:
                    throw new InvalidOperationException("Only map-stable Run states can be persisted.");
            }

            return new RunSaveDocument(
                RunSaveDocument.CurrentSchemaVersion,
                state.RunId.ToString(),
                state.HeroTemplateId,
                state.CurrentHealth,
                state.MaxHealth,
                state.DeckTemplateId,
                state.EncounterTemplateId,
                state.RandomRootSeed,
                nodeStatus,
                state.BattleAttemptSequence);
        }

        /// <summary>校验配置引用并把当前 schema 文档转换为唯一 Store 恢复输入。</summary>
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
            if (!catalog.EncounterExists(document.EncounterTemplateId))
            {
                return RunSaveRestoreResult.Failed(
                    RunSaveRestoreStatus.MissingEncounterTemplate,
                    $"Encounter template {document.EncounterTemplateId} does not exist.");
            }

            var options = new RunRestoreOptions(
                new RunId(Guid.ParseExact(document.RunId, "D")),
                document.HeroTemplateId,
                document.CurrentHealth,
                document.MaxHealth,
                document.DeckTemplateId,
                document.EncounterTemplateId,
                document.RandomRootSeed,
                document.NodeStatus == RunSaveNodeStatus.Available
                    ? RunNodeStatus.Available
                    : RunNodeStatus.Completed,
                document.BattleAttemptSequence);
            return RunSaveRestoreResult.Succeeded(options);
        }
    }
}
