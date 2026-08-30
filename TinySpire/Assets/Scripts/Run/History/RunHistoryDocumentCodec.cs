using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TinySpire.Run.Map;

namespace TinySpire.Run.History
{
    /// <summary>versioned Run history JSON 的读取结果分类。</summary>
    public enum RunHistoryDocumentReadStatus
    {
        Success,
        InvalidJson,
        InvalidDocument,
        UnsupportedSchema,
    }

    /// <summary>冻结 codec 读取结果且不发布半合法摘要。</summary>
    public sealed class RunHistoryDocumentReadResult
    {
        /// <summary>本次解析的封闭状态。</summary>
        public RunHistoryDocumentReadStatus Status { get; }

        /// <summary>成功时重建的完整不可变摘要。</summary>
        public RunSummary Summary { get; }

        /// <summary>失败时可用于日志的本地诊断。</summary>
        public string Detail { get; }

        /// <summary>冻结一个 codec 读取结果。</summary>
        private RunHistoryDocumentReadResult(
            RunHistoryDocumentReadStatus status,
            RunSummary summary,
            string detail)
        {
            Status = status;
            Summary = summary;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回携带完整摘要的成功结果。</summary>
        public static RunHistoryDocumentReadResult Succeeded(RunSummary summary)
        {
            return new RunHistoryDocumentReadResult(
                RunHistoryDocumentReadStatus.Success,
                summary ?? throw new ArgumentNullException(nameof(summary)),
                string.Empty);
        }

        /// <summary>返回不携带半合法摘要的明确解析失败。</summary>
        public static RunHistoryDocumentReadResult Failed(
            RunHistoryDocumentReadStatus status,
            string detail)
        {
            if (status == RunHistoryDocumentReadStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new RunHistoryDocumentReadResult(status, null, detail);
        }
    }

    /// <summary>以 schema v1 严格往返一局完整 RunSummary。</summary>
    public static class RunHistoryDocumentCodec
    {
        /// <summary>当前唯一受支持的历史文档 schema。</summary>
        public const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
            };

        /// <summary>把不可变摘要写成字段顺序稳定的 schema v1 JSON。</summary>
        public static string Write(RunSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            var document = new RunHistoryDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                RunId = summary.RunId.ToString(),
                CompletedAtUtc = summary.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                HeroTemplateId = summary.HeroTemplateId,
                Outcome = summary.OutcomeKind.ToString(),
                OutcomeBattleNodeId = summary.OutcomeBattleNodeId,
                OutcomeBattleAttemptSequence = summary.OutcomeBattleAttemptSequence,
                RandomRootSeed = summary.RandomRootSeed,
                FinalHealth = summary.FinalHealth,
                MaxHealth = summary.MaxHealth,
                BattleAttemptCount = summary.BattleAttemptCount,
                Path = summary.Path.Select(node => new RunHistoryPathNodeDocument
                {
                    NodeId = node.NodeId,
                    Kind = node.Kind.ToString(),
                    ContentId = node.ContentId,
                }).ToArray(),
                Deck = summary.Deck.Select(card => new RunHistoryCardDocument
                {
                    InstanceSequence = card.InstanceSequence,
                    TemplateId = card.TemplateId,
                    UpgradeLevel = card.UpgradeLevel,
                }).ToArray(),
                Holdings = new RunHistoryHoldingsDocument
                {
                    Gold = summary.Holdings.Gold,
                    Relics = summary.Holdings.Relics.Select(relic => new RunHistoryRelicDocument
                    {
                        InstanceSequence = relic.InstanceSequence,
                        TemplateId = relic.TemplateId,
                    }).ToArray(),
                    Potions = summary.Holdings.Potions.Select(potion => new RunHistoryPotionDocument
                    {
                        InstanceSequence = potion.InstanceSequence,
                        TemplateId = potion.TemplateId,
                    }).ToArray(),
                },
            };

            return JsonConvert.SerializeObject(document, SerializerSettings);
        }

        /// <summary>严格解析 schema、枚举、UTC 与全部嵌套数组后一次发布摘要。</summary>
        public static RunHistoryDocumentReadResult Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return RunHistoryDocumentReadResult.Failed(
                    RunHistoryDocumentReadStatus.InvalidJson,
                    "Run history JSON is empty.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonReaderException exception)
            {
                return RunHistoryDocumentReadResult.Failed(
                    RunHistoryDocumentReadStatus.InvalidJson,
                    exception.Message);
            }

            JToken schemaToken = root["schemaVersion"];
            if (schemaToken == null || schemaToken.Type != JTokenType.Integer)
            {
                return RunHistoryDocumentReadResult.Failed(
                    RunHistoryDocumentReadStatus.InvalidDocument,
                    "Run history schemaVersion is missing or invalid.");
            }
            int schemaVersion;
            try
            {
                schemaVersion = schemaToken.Value<int>();
            }
            catch (Exception exception) when (
                exception is OverflowException || exception is FormatException)
            {
                return RunHistoryDocumentReadResult.Failed(
                    RunHistoryDocumentReadStatus.InvalidDocument,
                    exception.Message);
            }
            if (schemaVersion != CurrentSchemaVersion)
            {
                return RunHistoryDocumentReadResult.Failed(
                    RunHistoryDocumentReadStatus.UnsupportedSchema,
                    $"Unsupported Run history schema '{schemaVersion}'.");
            }

            try
            {
                RunHistoryDocument document = JsonConvert.DeserializeObject<RunHistoryDocument>(
                    json,
                    SerializerSettings);
                return RunHistoryDocumentReadResult.Succeeded(CreateSummary(document));
            }
            catch (JsonReaderException exception)
            {
                return RunHistoryDocumentReadResult.Failed(
                    RunHistoryDocumentReadStatus.InvalidJson,
                    exception.Message);
            }
            catch (Exception exception) when (
                exception is JsonSerializationException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is OverflowException)
            {
                return RunHistoryDocumentReadResult.Failed(
                    RunHistoryDocumentReadStatus.InvalidDocument,
                    exception.Message);
            }
        }

        /// <summary>从已反序列化 DTO 验证全部字段并建立深冻结摘要。</summary>
        private static RunSummary CreateSummary(RunHistoryDocument document)
        {
            if (document == null)
                throw new ArgumentException("Run history document is missing.", nameof(document));
            if (!Guid.TryParseExact(document.RunId, "D", out Guid runGuid) || runGuid == Guid.Empty)
                throw new ArgumentException("Run history RunId is invalid.", nameof(document));
            if (!DateTimeOffset.TryParseExact(
                    document.CompletedAtUtc,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset completedAtUtc) ||
                completedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("Run history completedAtUtc must be UTC round-trip text.", nameof(document));
            }
            if (!TryParseOutcome(document.Outcome, out RunOutcomeKind outcomeKind))
                throw new ArgumentException("Run history outcome is invalid.", nameof(document));
            if (document.RandomRootSeed <= 0 || document.RandomRootSeed > uint.MaxValue)
                throw new ArgumentException("Run history randomRootSeed is invalid.", nameof(document));
            if (document.Path == null || document.Deck == null || document.Holdings == null ||
                document.Holdings.Relics == null || document.Holdings.Potions == null)
            {
                throw new ArgumentException("Run history nested collections are incomplete.", nameof(document));
            }

            var path = document.Path.Select(node =>
            {
                if (node == null || !TryParseNodeKind(node.Kind, out MapNodeKind kind))
                    throw new ArgumentException("Run history path node is invalid.", nameof(document));
                return new RunSummaryPathNode(node.NodeId, kind, node.ContentId);
            }).ToArray();
            var deck = document.Deck.Select(card =>
            {
                if (card == null)
                    throw new ArgumentException("Run history deck card is missing.", nameof(document));
                return new RunSummaryCard(
                    card.InstanceSequence,
                    card.TemplateId,
                    card.UpgradeLevel);
            }).ToArray();
            var holdings = new RunSummaryHoldings(
                document.Holdings.Gold,
                document.Holdings.Relics.Select(relic =>
                {
                    if (relic == null)
                        throw new ArgumentException("Run history relic is missing.", nameof(document));
                    return new RunSummaryRelic(relic.InstanceSequence, relic.TemplateId);
                }),
                document.Holdings.Potions.Select(potion =>
                {
                    if (potion == null)
                        throw new ArgumentException("Run history potion is missing.", nameof(document));
                    return new RunSummaryPotion(potion.InstanceSequence, potion.TemplateId);
                }));

            return new RunSummary(
                new RunId(runGuid),
                completedAtUtc,
                document.HeroTemplateId,
                outcomeKind,
                document.OutcomeBattleNodeId,
                document.OutcomeBattleAttemptSequence,
                checked((uint)document.RandomRootSeed),
                document.FinalHealth,
                document.MaxHealth,
                document.BattleAttemptCount,
                path,
                deck,
                holdings);
        }

        /// <summary>只接受三种大小写精确的终局分类文本。</summary>
        private static bool TryParseOutcome(string value, out RunOutcomeKind outcomeKind)
        {
            switch (value)
            {
                case nameof(RunOutcomeKind.Victory):
                    outcomeKind = RunOutcomeKind.Victory;
                    return true;
                case nameof(RunOutcomeKind.Defeat):
                    outcomeKind = RunOutcomeKind.Defeat;
                    return true;
                case nameof(RunOutcomeKind.Abandoned):
                    outcomeKind = RunOutcomeKind.Abandoned;
                    return true;
                default:
                    outcomeKind = default;
                    return false;
            }
        }

        /// <summary>只接受当前封闭地图节点枚举的大小写精确文本。</summary>
        private static bool TryParseNodeKind(string value, out MapNodeKind kind)
        {
            return Enum.TryParse(value, false, out kind) &&
                   Enum.IsDefined(typeof(MapNodeKind), kind);
        }

        /// <summary>schema v1 根文档 DTO。</summary>
        private sealed class RunHistoryDocument
        {
            [JsonProperty("schemaVersion", Order = 1, Required = Required.Always)]
            public int SchemaVersion { get; set; }

            [JsonProperty("runId", Order = 2, Required = Required.Always)]
            public string RunId { get; set; }

            [JsonProperty("completedAtUtc", Order = 3, Required = Required.Always)]
            public string CompletedAtUtc { get; set; }

            [JsonProperty("heroTemplateId", Order = 4, Required = Required.Always)]
            public int HeroTemplateId { get; set; }

            [JsonProperty("outcome", Order = 5, Required = Required.Always)]
            public string Outcome { get; set; }

            [JsonProperty("outcomeBattleNodeId", Order = 6, Required = Required.AllowNull)]
            public string OutcomeBattleNodeId { get; set; }

            [JsonProperty("outcomeBattleAttemptSequence", Order = 7, Required = Required.AllowNull)]
            public int? OutcomeBattleAttemptSequence { get; set; }

            [JsonProperty("randomRootSeed", Order = 8, Required = Required.Always)]
            public long RandomRootSeed { get; set; }

            [JsonProperty("finalHealth", Order = 9, Required = Required.Always)]
            public int FinalHealth { get; set; }

            [JsonProperty("maxHealth", Order = 10, Required = Required.Always)]
            public int MaxHealth { get; set; }

            [JsonProperty("battleAttemptCount", Order = 11, Required = Required.Always)]
            public int BattleAttemptCount { get; set; }

            [JsonProperty("path", Order = 12, Required = Required.Always)]
            public RunHistoryPathNodeDocument[] Path { get; set; }

            [JsonProperty("deck", Order = 13, Required = Required.Always)]
            public RunHistoryCardDocument[] Deck { get; set; }

            [JsonProperty("holdings", Order = 14, Required = Required.Always)]
            public RunHistoryHoldingsDocument Holdings { get; set; }
        }

        /// <summary>schema v1 路径节点 DTO。</summary>
        private sealed class RunHistoryPathNodeDocument
        {
            [JsonProperty("nodeId", Order = 1, Required = Required.Always)]
            public string NodeId { get; set; }

            [JsonProperty("kind", Order = 2, Required = Required.Always)]
            public string Kind { get; set; }

            [JsonProperty("contentId", Order = 3, Required = Required.Always)]
            public int ContentId { get; set; }
        }

        /// <summary>schema v1 牌组实例 DTO。</summary>
        private sealed class RunHistoryCardDocument
        {
            [JsonProperty("instanceSequence", Order = 1, Required = Required.Always)]
            public int InstanceSequence { get; set; }

            [JsonProperty("templateId", Order = 2, Required = Required.Always)]
            public int TemplateId { get; set; }

            [JsonProperty("upgradeLevel", Order = 3, Required = Required.Always)]
            public int UpgradeLevel { get; set; }
        }

        /// <summary>schema v1 持有物 DTO。</summary>
        private sealed class RunHistoryHoldingsDocument
        {
            [JsonProperty("gold", Order = 1, Required = Required.Always)]
            public int Gold { get; set; }

            [JsonProperty("relics", Order = 2, Required = Required.Always)]
            public RunHistoryRelicDocument[] Relics { get; set; }

            [JsonProperty("potions", Order = 3, Required = Required.Always)]
            public RunHistoryPotionDocument[] Potions { get; set; }
        }

        /// <summary>schema v1 遗物实例 DTO。</summary>
        private sealed class RunHistoryRelicDocument
        {
            [JsonProperty("instanceSequence", Order = 1, Required = Required.Always)]
            public int InstanceSequence { get; set; }

            [JsonProperty("templateId", Order = 2, Required = Required.Always)]
            public int TemplateId { get; set; }
        }

        /// <summary>schema v1 药水实例 DTO。</summary>
        private sealed class RunHistoryPotionDocument
        {
            [JsonProperty("instanceSequence", Order = 1, Required = Required.Always)]
            public int InstanceSequence { get; set; }

            [JsonProperty("templateId", Order = 2, Required = Required.Always)]
            public int TemplateId { get; set; }
        }
    }
}
