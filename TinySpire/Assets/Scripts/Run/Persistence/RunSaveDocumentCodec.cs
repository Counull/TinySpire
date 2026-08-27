using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TinySpire.Run
{
    /// <summary>原始 JSON 进入当前 schema 的显式迁移结果。</summary>
    public enum RunSaveDocumentMigrationStatus
    {
        Success,
        InvalidDocument,
        UnsupportedSchema,
    }

    /// <summary>保存迁移后的当前 JSON 对象或不可迁移原因。</summary>
    public sealed class RunSaveDocumentMigrationResult
    {
        public RunSaveDocumentMigrationStatus Status { get; }
        public JObject CurrentDocument { get; }
        public string Detail { get; }

        /// <summary>建立不可变迁移结果。</summary>
        private RunSaveDocumentMigrationResult(
            RunSaveDocumentMigrationStatus status,
            JObject currentDocument,
            string detail)
        {
            Status = status;
            CurrentDocument = currentDocument;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回已转换到当前 schema 的独立 JSON 对象。</summary>
        internal static RunSaveDocumentMigrationResult Succeeded(JObject currentDocument)
        {
            return new RunSaveDocumentMigrationResult(
                RunSaveDocumentMigrationStatus.Success,
                currentDocument ?? throw new ArgumentNullException(nameof(currentDocument)),
                string.Empty);
        }

        /// <summary>返回不携带当前文档的迁移失败。</summary>
        internal static RunSaveDocumentMigrationResult Failed(
            RunSaveDocumentMigrationStatus status,
            string detail)
        {
            if (status == RunSaveDocumentMigrationStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new RunSaveDocumentMigrationResult(status, null, detail);
        }
    }

    /// <summary>所有旧 schema 进入当前 v5 文档前必须经过的唯一迁移入口。</summary>
    public static class RunSaveDocumentMigrator
    {
        /// <summary>读取明确版本并只迁移可证明无歧义的文档。</summary>
        public static RunSaveDocumentMigrationResult MigrateToCurrent(JObject source)
        {
            if (source == null)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    "Run save JSON root must be an object.");
            }

            JToken schemaToken = source["schemaVersion"];
            if (schemaToken == null || schemaToken.Type != JTokenType.Integer)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    "Run save schemaVersion is missing or is not an integer.");
            }

            int schemaVersion;
            try
            {
                schemaVersion = schemaToken.Value<int>();
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is InvalidCastException)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    $"Run save schemaVersion is invalid: {exception.Message}");
            }

            switch (schemaVersion)
            {
                case 2:
                    return MigrateV2ToV5(source);
                case 3:
                    return MigrateV3ToV5(source);
                case 4:
                    return MigrateV4ToV5(source);
                case RunSaveDocument.CurrentSchemaVersion:
                    return RunSaveDocumentMigrationResult.Succeeded((JObject)source.DeepClone());
                default:
                    return RunSaveDocumentMigrationResult.Failed(
                        RunSaveDocumentMigrationStatus.UnsupportedSchema,
                        $"Run save schemaVersion {schemaVersion} cannot be migrated to " +
                        $"{RunSaveDocument.CurrentSchemaVersion}.");
            }
        }

        /// <summary>按显式 v2→v3→v4→v5 链迁移，不跨级猜测旧字段。</summary>
        private static RunSaveDocumentMigrationResult MigrateV2ToV5(JObject source)
        {
            RunSaveDocumentMigrationResult v3 = MigrateV2ToV3(source);
            if (v3.Status != RunSaveDocumentMigrationStatus.Success)
                return v3;

            RunSaveDocumentMigrationResult v4 = MigrateV3ToV4(v3.CurrentDocument);
            return v4.Status == RunSaveDocumentMigrationStatus.Success
                ? MigrateV4ToV5(v4.CurrentDocument)
                : v4;
        }

        /// <summary>按显式 v3→v4→v5 链迁移并保留每一级的字段所有权检查。</summary>
        private static RunSaveDocumentMigrationResult MigrateV3ToV5(JObject source)
        {
            RunSaveDocumentMigrationResult v4 = MigrateV3ToV4(source);
            return v4.Status == RunSaveDocumentMigrationStatus.Success
                ? MigrateV4ToV5(v4.CurrentDocument)
                : v4;
        }

        /// <summary>把 v2 的初始牌组模板转换为 v3 一次性 legacy fallback，不伪造实例事实。</summary>
        private static RunSaveDocumentMigrationResult MigrateV2ToV3(JObject source)
        {
            JProperty newerDeckProperty = source.Property("runCards") ??
                                           source.Property("legacyDeckTemplateId");
            if (newerDeckProperty != null)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    $"Schema v2 cannot contain {newerDeckProperty.Name}.");
            }

            JToken deckTemplateToken = source["deckTemplateId"];
            if (deckTemplateToken == null || deckTemplateToken.Type != JTokenType.Integer)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    "Schema v2 deckTemplateId is missing or is not an integer.");
            }

            int deckTemplateId;
            try
            {
                deckTemplateId = deckTemplateToken.Value<int>();
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is InvalidCastException)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    $"Schema v2 deckTemplateId is invalid: {exception.Message}");
            }

            if (deckTemplateId <= 0)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    "Schema v2 deckTemplateId must be positive.");
            }

            var current = (JObject)source.DeepClone();
            current["schemaVersion"] = 3;
            current.Remove("deckTemplateId");
            current["runCards"] = JValue.CreateNull();
            current["legacyDeckTemplateId"] = deckTemplateId;
            return RunSaveDocumentMigrationResult.Succeeded(current);
        }

        /// <summary>把 v3 稳定检查点扩为 v4，并显式声明当时不存在 Pending reward。</summary>
        private static RunSaveDocumentMigrationResult MigrateV3ToV4(JObject source)
        {
            if (source.Property("pendingCardReward") != null)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    "Schema v3 cannot contain pendingCardReward.");
            }

            var current = (JObject)source.DeepClone();
            current["schemaVersion"] = 4;
            current["pendingCardReward"] = JValue.CreateNull();
            return RunSaveDocumentMigrationResult.Succeeded(current);
        }

        /// <summary>把 v4 扩为 v5 空持有物，并拒绝旧版本夹带尚未拥有的字段。</summary>
        private static RunSaveDocumentMigrationResult MigrateV4ToV5(JObject source)
        {
            JProperty newerProperty = source.Property("relics") ??
                                        source.Property("potions") ??
                                        source.Property("gold") ??
                                        source.Property("pendingNodeVisit");
            if (newerProperty != null)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    $"Schema v4 cannot contain {newerProperty.Name}.");
            }
            var sourcePendingReward = source["pendingCardReward"] as JObject;
            if (sourcePendingReward?.Property("attachedLoot") != null)
            {
                return RunSaveDocumentMigrationResult.Failed(
                    RunSaveDocumentMigrationStatus.InvalidDocument,
                    "Schema v4 cannot contain pendingCardReward.attachedLoot.");
            }

            var current = (JObject)source.DeepClone();
            current["schemaVersion"] = RunSaveDocument.CurrentSchemaVersion;
            current["relics"] = new JArray();
            current["potions"] = new JArray();
            current["gold"] = 100;
            current["pendingNodeVisit"] = JValue.CreateNull();
            var currentPendingReward = current["pendingCardReward"] as JObject;
            if (currentPendingReward != null)
            {
                bool firstOrdinaryBattleReward =
                    IsFirstOrdinaryBattleReward(source, sourcePendingReward);
                currentPendingReward["attachedLoot"] = new JObject
                {
                    ["relicTemplateId"] = firstOrdinaryBattleReward
                        ? new JValue(
                            RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic)
                        : JValue.CreateNull(),
                    ["potionTemplateId"] = firstOrdinaryBattleReward
                        ? new JValue(
                            RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattlePotion)
                        : JValue.CreateNull(),
                };
            }
            return RunSaveDocumentMigrationResult.Succeeded(current);
        }

        /// <summary>只把 v4 RewardPending 中可解析为 attempt 1 的稳定奖励身份识别为首次普通战斗。</summary>
        private static bool IsFirstOrdinaryBattleReward(
            JObject source,
            JObject pendingReward)
        {
            if (source?.Value<string>("progressPhase") !=
                    nameof(RunSaveProgressPhase.RewardPending) ||
                pendingReward == null)
            {
                return false;
            }

            string rewardId = pendingReward.Value<string>("rewardId");
            if (string.IsNullOrWhiteSpace(rewardId))
                return false;
            string[] parts = rewardId.Split(new[] { ':' }, count: 3);
            return parts.Length == 3 &&
                   Guid.TryParseExact(parts[0], "N", out Guid parsedRunId) &&
                   parsedRunId != Guid.Empty &&
                   int.TryParse(parts[1], out int attemptSequence) &&
                   attemptSequence == 1 &&
                   !string.IsNullOrWhiteSpace(parts[2]);
        }
    }

    /// <summary>JSON 文档读取的类型化成功或故障分类。</summary>
    public enum RunSaveDocumentReadStatus
    {
        Success,
        InvalidJson,
        InvalidDocument,
        UnsupportedSchema,
    }

    /// <summary>冻结成功解析的 DTO 或面向诊断的明确失败原因。</summary>
    public sealed class RunSaveDocumentReadResult
    {
        public RunSaveDocumentReadStatus Status { get; }
        public RunSaveDocument Document { get; }
        public string Detail { get; }

        /// <summary>建立不可变 JSON 读取结果。</summary>
        private RunSaveDocumentReadResult(
            RunSaveDocumentReadStatus status,
            RunSaveDocument document,
            string detail)
        {
            Status = status;
            Document = document;
            Detail = detail ?? string.Empty;
        }

        /// <summary>返回携带完整 DTO 的成功结果。</summary>
        internal static RunSaveDocumentReadResult Succeeded(RunSaveDocument document)
        {
            return new RunSaveDocumentReadResult(
                RunSaveDocumentReadStatus.Success,
                document ?? throw new ArgumentNullException(nameof(document)),
                string.Empty);
        }

        /// <summary>返回不携带 DTO 的读取失败。</summary>
        internal static RunSaveDocumentReadResult Failed(
            RunSaveDocumentReadStatus status,
            string detail)
        {
            if (status == RunSaveDocumentReadStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new RunSaveDocumentReadResult(status, null, detail);
        }
    }

    /// <summary>以严格白名单设置序列化、迁移并读取 RunSaveDocument。</summary>
    public static class RunSaveDocumentCodec
    {
        private static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
            };

        /// <summary>把已验证 DTO 序列化为便于诊断的 versioned JSON。</summary>
        public static string Serialize(RunSaveDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return JsonConvert.SerializeObject(document, SerializerSettings);
        }

        /// <summary>解析 JSON、经过唯一迁移入口，并严格反序列化当前白名单字段。</summary>
        public static RunSaveDocumentReadResult Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidJson,
                    "Run save JSON is empty.");
            }

            JObject source;
            try
            {
                source = JToken.Parse(
                    json,
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    }) as JObject;
            }
            catch (JsonException exception)
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidJson,
                    exception.Message);
            }

            if (source == null)
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidDocument,
                    "Run save JSON root must be an object.");
            }

            RunSaveDocumentMigrationResult migration =
                RunSaveDocumentMigrator.MigrateToCurrent(source);
            if (migration.Status == RunSaveDocumentMigrationStatus.UnsupportedSchema)
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.UnsupportedSchema,
                    migration.Detail);
            }
            if (migration.Status != RunSaveDocumentMigrationStatus.Success)
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidDocument,
                    migration.Detail);
            }

            JToken progressPhaseToken = migration.CurrentDocument["progressPhase"];
            string progressPhase = progressPhaseToken?.Type == JTokenType.String
                ? progressPhaseToken.Value<string>()
                : null;
            if (progressPhase != nameof(RunSaveProgressPhase.MapReady) &&
                progressPhase != nameof(RunSaveProgressPhase.RewardPending) &&
                progressPhase != nameof(RunSaveProgressPhase.BossGateReached) &&
                progressPhase != nameof(RunSaveProgressPhase.Terminal) &&
                progressPhase != nameof(RunSaveProgressPhase.NodeVisitPending))
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidDocument,
                    "Run save progressPhase must be an exact supported string value.");
            }

            JToken terminalReasonToken = migration.CurrentDocument["terminalReason"];
            bool terminalReasonIsValid = progressPhase == nameof(RunSaveProgressPhase.Terminal)
                ? terminalReasonToken?.Type == JTokenType.String &&
                  terminalReasonToken.Value<string>() == nameof(RunSaveTerminalReason.Defeat)
                : terminalReasonToken?.Type == JTokenType.Null;
            if (!terminalReasonIsValid)
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidDocument,
                    "Run save terminalReason does not match progressPhase.");
            }

            if (!ValidateNodeVisitEnumStrings(migration.CurrentDocument, out string enumDetail))
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidDocument,
                    enumDetail);
            }

            try
            {
                RunSaveDocument document = migration.CurrentDocument.ToObject<RunSaveDocument>(
                    JsonSerializer.Create(SerializerSettings));
                if (document == null)
                {
                    return RunSaveDocumentReadResult.Failed(
                        RunSaveDocumentReadStatus.InvalidDocument,
                        "Run save document could not be created.");
                }

                if (source.Value<int>("schemaVersion") != RunSaveDocument.CurrentSchemaVersion)
                    document.MarkRequiresCanonicalRewrite();
                return RunSaveDocumentReadResult.Succeeded(document);
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is ArgumentException ||
                exception is OverflowException)
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidDocument,
                    exception.Message);
            }
        }

        /// <summary>拒绝节点与库存类型的数值枚举、未知文本或非字符串形状。</summary>
        private static bool ValidateNodeVisitEnumStrings(
            JObject document,
            out string detail)
        {
            detail = string.Empty;
            JToken pendingToken = document?["pendingNodeVisit"];
            if (pendingToken == null || pendingToken.Type == JTokenType.Null)
                return true;
            if (pendingToken.Type != JTokenType.Object)
            {
                detail = "Run save pendingNodeVisit must be an object or null.";
                return false;
            }

            var pending = (JObject)pendingToken;
            JToken kindToken = pending["kind"];
            string kind = kindToken?.Type == JTokenType.String
                ? kindToken.Value<string>()
                : null;
            if (kind != nameof(Map.MapNodeKind.Rest) &&
                kind != nameof(Map.MapNodeKind.Chest) &&
                kind != nameof(Map.MapNodeKind.Shop) &&
                kind != nameof(Map.MapNodeKind.Event))
            {
                detail = "Run save pendingNodeVisit kind must be an exact supported string value.";
                return false;
            }

            JToken shopPayloadToken = pending["shopPayload"];
            if (shopPayloadToken == null || shopPayloadToken.Type == JTokenType.Null)
                return true;
            if (shopPayloadToken.Type != JTokenType.Object)
            {
                detail = "Run save shopPayload must be an object or null.";
                return false;
            }

            JToken entriesToken = ((JObject)shopPayloadToken)["entries"];
            if (entriesToken == null || entriesToken.Type == JTokenType.Null)
                return true;
            if (entriesToken.Type != JTokenType.Array)
            {
                detail = "Run save shop entries must be an array.";
                return false;
            }

            foreach (JToken entryToken in (JArray)entriesToken)
            {
                JToken stockKindToken = entryToken?["kind"];
                string stockKind = stockKindToken?.Type == JTokenType.String
                    ? stockKindToken.Value<string>()
                    : null;
                if (stockKind != nameof(RunShopStockKind.Relic) &&
                    stockKind != nameof(RunShopStockKind.Potion) &&
                    stockKind != nameof(RunShopStockKind.Card))
                {
                    detail = "Run save shop stock kind must be an exact supported string value.";
                    return false;
                }
            }

            return true;
        }
    }
}
