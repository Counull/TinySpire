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

    /// <summary>所有旧 schema 进入当前 v1 文档前必须经过的唯一迁移入口。</summary>
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
                case RunSaveDocument.CurrentSchemaVersion:
                    return RunSaveDocumentMigrationResult.Succeeded((JObject)source.DeepClone());
                default:
                    return RunSaveDocumentMigrationResult.Failed(
                        RunSaveDocumentMigrationStatus.UnsupportedSchema,
                        $"Run save schemaVersion {schemaVersion} cannot be migrated to " +
                        $"{RunSaveDocument.CurrentSchemaVersion}.");
            }
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
                source = JToken.Parse(json) as JObject;
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

            JToken nodeStatusToken = migration.CurrentDocument["nodeStatus"];
            string nodeStatus = nodeStatusToken?.Type == JTokenType.String
                ? nodeStatusToken.Value<string>()
                : null;
            if (nodeStatus != nameof(RunSaveNodeStatus.Available) &&
                nodeStatus != nameof(RunSaveNodeStatus.Completed))
            {
                return RunSaveDocumentReadResult.Failed(
                    RunSaveDocumentReadStatus.InvalidDocument,
                    "Run save nodeStatus must be an exact supported string value.");
            }

            try
            {
                RunSaveDocument document = migration.CurrentDocument.ToObject<RunSaveDocument>(
                    JsonSerializer.Create(SerializerSettings));
                return document == null
                    ? RunSaveDocumentReadResult.Failed(
                        RunSaveDocumentReadStatus.InvalidDocument,
                        "Run save document could not be created.")
                    : RunSaveDocumentReadResult.Succeeded(document);
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
    }
}
