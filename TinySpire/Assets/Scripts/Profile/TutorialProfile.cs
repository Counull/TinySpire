using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TinySpire.Profile
{
    /// <summary>教程提示可以出现的稳定产品上下文。</summary>
    public enum TutorialContext
    {
        MainMenu,
        HeroSelection,
        ActMap,
        Battle,
        CardReward,
        NonCombatNode,
        RunOutcome,
    }

    /// <summary>首轮完整 Run 教程的稳定步骤身份。</summary>
    public enum TutorialPromptId
    {
        MainMenuWelcome,
        HeroSelection,
        MapRoute,
        BattleBasics,
        CardReward,
        NonCombatNode,
        RunOutcome,
    }

    /// <summary>一个只描述出现上下文和稳定身份、不持有玩法事实的教程提示。</summary>
    public sealed class TutorialPromptDefinition
    {
        /// <summary>领域内稳定步骤身份。</summary>
        public TutorialPromptId Id { get; }

        /// <summary>允许展示本提示的唯一产品上下文。</summary>
        public TutorialContext Context { get; }

        /// <summary>写入 Profile JSON 的显式稳定短键。</summary>
        public string StorageId { get; }

        /// <summary>冻结一个教程提示定义。</summary>
        internal TutorialPromptDefinition(
            TutorialPromptId id,
            TutorialContext context,
            string storageId)
        {
            if (string.IsNullOrWhiteSpace(storageId))
                throw new ArgumentException("Tutorial prompt storage id is required.", nameof(storageId));

            Id = id;
            Context = context;
            StorageId = storageId;
        }
    }

    /// <summary>G8-C 首轮教程的唯一有序目录。</summary>
    public static class TutorialPromptCatalog
    {
        private static readonly TutorialPromptDefinition[] OrderedDefinitions =
        {
            new TutorialPromptDefinition(
                TutorialPromptId.MainMenuWelcome,
                TutorialContext.MainMenu,
                "main-menu-welcome"),
            new TutorialPromptDefinition(
                TutorialPromptId.HeroSelection,
                TutorialContext.HeroSelection,
                "hero-selection"),
            new TutorialPromptDefinition(
                TutorialPromptId.MapRoute,
                TutorialContext.ActMap,
                "map-route"),
            new TutorialPromptDefinition(
                TutorialPromptId.BattleBasics,
                TutorialContext.Battle,
                "battle-basics"),
            new TutorialPromptDefinition(
                TutorialPromptId.CardReward,
                TutorialContext.CardReward,
                "card-reward"),
            new TutorialPromptDefinition(
                TutorialPromptId.NonCombatNode,
                TutorialContext.NonCombatNode,
                "non-combat-node"),
            new TutorialPromptDefinition(
                TutorialPromptId.RunOutcome,
                TutorialContext.RunOutcome,
                "run-outcome"),
        };

        private static readonly ReadOnlyCollection<TutorialPromptDefinition> ReadOnlyDefinitions =
            Array.AsReadOnly(OrderedDefinitions);

        /// <summary>按首轮完整 Run 顺序公开只读提示目录。</summary>
        public static IReadOnlyList<TutorialPromptDefinition> Ordered => ReadOnlyDefinitions;

        /// <summary>按领域身份查找唯一提示定义。</summary>
        public static bool TryGet(
            TutorialPromptId id,
            out TutorialPromptDefinition definition)
        {
            for (int index = 0; index < OrderedDefinitions.Length; index++)
            {
                TutorialPromptDefinition candidate = OrderedDefinitions[index];
                if (candidate.Id != id)
                    continue;

                definition = candidate;
                return true;
            }

            definition = null;
            return false;
        }

        /// <summary>按文档短键查找唯一提示定义，比较严格区分大小写。</summary>
        public static bool TryGetByStorageId(
            string storageId,
            out TutorialPromptDefinition definition)
        {
            for (int index = 0; index < OrderedDefinitions.Length; index++)
            {
                TutorialPromptDefinition candidate = OrderedDefinitions[index];
                if (!string.Equals(candidate.StorageId, storageId, StringComparison.Ordinal))
                    continue;

                definition = candidate;
                return true;
            }

            definition = null;
            return false;
        }
    }

    /// <summary>独立于设置、Run save 与历史的不可变玩家教程 Profile。</summary>
    public sealed class PlayerProfileSnapshot : IEquatable<PlayerProfileSnapshot>
    {
        private readonly ReadOnlyCollection<TutorialPromptId> _acknowledgedPromptIds;

        /// <summary>玩家是否显式跳过余下教程。</summary>
        public bool TutorialSkipped { get; }

        /// <summary>严格按目录前缀排列、已耐久确认的教程步骤。</summary>
        public IReadOnlyList<TutorialPromptId> AcknowledgedPromptIds =>
            _acknowledgedPromptIds;

        /// <summary>全部步骤是否已经逐项确认完成。</summary>
        public bool TutorialCompleted =>
            _acknowledgedPromptIds.Count == TutorialPromptCatalog.Ordered.Count;

        /// <summary>教程仍启用时的唯一下一步骤；跳过或完成时为空。</summary>
        public TutorialPromptDefinition CurrentPrompt =>
            TutorialSkipped || TutorialCompleted
                ? null
                : TutorialPromptCatalog.Ordered[_acknowledgedPromptIds.Count];

        /// <summary>冻结一份只含教程进度的 Profile，并拒绝乱序或未知步骤。</summary>
        public PlayerProfileSnapshot(
            bool tutorialSkipped,
            IEnumerable<TutorialPromptId> acknowledgedPromptIds)
        {
            if (acknowledgedPromptIds == null)
                throw new ArgumentNullException(nameof(acknowledgedPromptIds));

            TutorialPromptId[] copied = acknowledgedPromptIds.ToArray();
            if (copied.Length > TutorialPromptCatalog.Ordered.Count)
                throw new ArgumentException("Too many acknowledged tutorial prompts.", nameof(acknowledgedPromptIds));

            for (int index = 0; index < copied.Length; index++)
            {
                TutorialPromptDefinition expected = TutorialPromptCatalog.Ordered[index];
                if (!TutorialPromptCatalog.TryGet(copied[index], out _) ||
                    copied[index] != expected.Id)
                {
                    throw new ArgumentException(
                        "Acknowledged tutorial prompts must be the ordered catalog prefix.",
                        nameof(acknowledgedPromptIds));
                }
            }

            TutorialSkipped = tutorialSkipped;
            _acknowledgedPromptIds = Array.AsReadOnly(copied);
        }

        /// <summary>创建首次启动且尚未确认任何提示的 Profile。</summary>
        public static PlayerProfileSnapshot CreateNew()
        {
            return new PlayerProfileSnapshot(
                tutorialSkipped: false,
                acknowledgedPromptIds: Array.Empty<TutorialPromptId>());
        }

        /// <summary>确认指定步骤是否已包含在耐久前缀中。</summary>
        public bool HasAcknowledged(TutorialPromptId promptId)
        {
            for (int index = 0; index < _acknowledgedPromptIds.Count; index++)
            {
                if (_acknowledgedPromptIds[index] == promptId)
                    return true;
            }

            return false;
        }

        /// <summary>创建确认当前唯一步骤后的新快照。</summary>
        internal PlayerProfileSnapshot AcknowledgeCurrent(TutorialPromptId promptId)
        {
            if (CurrentPrompt == null || CurrentPrompt.Id != promptId)
                throw new InvalidOperationException("Only the current tutorial prompt can be acknowledged.");

            var acknowledged = new List<TutorialPromptId>(_acknowledgedPromptIds)
            {
                promptId,
            };
            return new PlayerProfileSnapshot(TutorialSkipped, acknowledged);
        }

        /// <summary>创建显式跳过余下教程的新快照。</summary>
        internal PlayerProfileSnapshot SkipTutorial()
        {
            return new PlayerProfileSnapshot(
                tutorialSkipped: true,
                acknowledgedPromptIds: _acknowledgedPromptIds);
        }

        /// <summary>逐字段比较两份 Profile 教程事实。</summary>
        public bool Equals(PlayerProfileSnapshot other)
        {
            if (other == null || TutorialSkipped != other.TutorialSkipped)
                return false;
            if (_acknowledgedPromptIds.Count != other._acknowledgedPromptIds.Count)
                return false;

            for (int index = 0; index < _acknowledgedPromptIds.Count; index++)
            {
                if (_acknowledgedPromptIds[index] != other._acknowledgedPromptIds[index])
                    return false;
            }

            return true;
        }

        /// <summary>比较对象是否为相同教程 Profile。</summary>
        public override bool Equals(object obj)
        {
            return obj is PlayerProfileSnapshot other && Equals(other);
        }

        /// <summary>返回跳过状态与有序确认前缀组成的稳定哈希。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TutorialSkipped.GetHashCode();
                for (int index = 0; index < _acknowledgedPromptIds.Count; index++)
                    hash = (hash * 397) ^ (int)_acknowledgedPromptIds[index];
                return hash;
            }
        }
    }

    /// <summary>Player Profile JSON 读取的封闭结果分类。</summary>
    public enum PlayerProfileDocumentReadStatus
    {
        Success,
        InvalidJson,
        InvalidDocument,
        UnsupportedSchema,
    }

    /// <summary>严格 codec 返回的 Profile 或类型化失败。</summary>
    public sealed class PlayerProfileDocumentReadResult
    {
        /// <summary>读取结果分类。</summary>
        public PlayerProfileDocumentReadStatus Status { get; }

        /// <summary>成功时的完整教程 Profile。</summary>
        public PlayerProfileSnapshot Profile { get; }

        /// <summary>失败时供诊断的非空原因。</summary>
        public string Detail { get; }

        /// <summary>冻结一项 codec 读取结果。</summary>
        private PlayerProfileDocumentReadResult(
            PlayerProfileDocumentReadStatus status,
            PlayerProfileSnapshot profile,
            string detail)
        {
            Status = status;
            Profile = profile;
            Detail = detail ?? string.Empty;
        }

        /// <summary>创建成功读取结果。</summary>
        internal static PlayerProfileDocumentReadResult Succeeded(
            PlayerProfileSnapshot profile)
        {
            return new PlayerProfileDocumentReadResult(
                PlayerProfileDocumentReadStatus.Success,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                string.Empty);
        }

        /// <summary>创建不携带 Profile 的失败结果。</summary>
        internal static PlayerProfileDocumentReadResult Failed(
            PlayerProfileDocumentReadStatus status,
            string detail)
        {
            if (status == PlayerProfileDocumentReadStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new PlayerProfileDocumentReadResult(status, null, detail);
        }
    }

    /// <summary>schema v1 player-profile.json 的唯一编码与严格读取入口。</summary>
    public static class PlayerProfileDocumentCodec
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>把只含教程进度的 Profile 编码为稳定、无缩进 JSON。</summary>
        public static string Write(PlayerProfileSnapshot profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var acknowledged = new JArray();
            for (int index = 0; index < profile.AcknowledgedPromptIds.Count; index++)
            {
                TutorialPromptId promptId = profile.AcknowledgedPromptIds[index];
                if (!TutorialPromptCatalog.TryGet(promptId, out TutorialPromptDefinition definition))
                    throw new InvalidOperationException($"Unknown tutorial prompt '{promptId}'.");
                acknowledged.Add(definition.StorageId);
            }

            var document = new JObject
            {
                ["schemaVersion"] = CurrentSchemaVersion,
                ["tutorial"] = new JObject
                {
                    ["skipped"] = profile.TutorialSkipped,
                    ["acknowledgedPromptIds"] = acknowledged,
                },
            };
            return document.ToString(Formatting.None);
        }

        /// <summary>严格读取 schema v1，并拒绝设置、历史或其他未知字段混入 Profile。</summary>
        public static PlayerProfileDocumentReadResult Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return PlayerProfileDocumentReadResult.Failed(
                    PlayerProfileDocumentReadStatus.InvalidJson,
                    "Player Profile JSON cannot be empty.");
            }

            JObject document;
            try
            {
                document = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                return PlayerProfileDocumentReadResult.Failed(
                    PlayerProfileDocumentReadStatus.InvalidJson,
                    exception.Message);
            }

            try
            {
                int schemaVersion = RequireInteger(document, "schemaVersion");
                if (schemaVersion != CurrentSchemaVersion)
                {
                    return PlayerProfileDocumentReadResult.Failed(
                        PlayerProfileDocumentReadStatus.UnsupportedSchema,
                        $"Unsupported Player Profile schema {schemaVersion}.");
                }

                RequireExactProperties(document, "schemaVersion", "tutorial");
                JObject tutorial = RequireObject(document, "tutorial");
                RequireExactProperties(tutorial, "skipped", "acknowledgedPromptIds");

                bool skipped = RequireBoolean(tutorial, "skipped");
                JArray acknowledgedTokens = RequireArray(tutorial, "acknowledgedPromptIds");
                var acknowledgedIds = new List<TutorialPromptId>(acknowledgedTokens.Count);
                for (int index = 0; index < acknowledgedTokens.Count; index++)
                {
                    JToken token = acknowledgedTokens[index];
                    if (token == null || token.Type != JTokenType.String)
                        throw new FormatException("Tutorial acknowledged prompt ids must be strings.");

                    string storageId = token.Value<string>();
                    if (!TutorialPromptCatalog.TryGetByStorageId(
                            storageId,
                            out TutorialPromptDefinition definition))
                    {
                        throw new FormatException(
                            $"Unknown tutorial prompt storage id '{storageId}'.");
                    }

                    acknowledgedIds.Add(definition.Id);
                }

                var profile = new PlayerProfileSnapshot(skipped, acknowledgedIds);
                return PlayerProfileDocumentReadResult.Succeeded(profile);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is FormatException ||
                exception is OverflowException)
            {
                return PlayerProfileDocumentReadResult.Failed(
                    PlayerProfileDocumentReadStatus.InvalidDocument,
                    exception.Message);
            }
        }

        /// <summary>要求对象属性集合与当前 schema 完全一致。</summary>
        private static void RequireExactProperties(JObject document, params string[] expectedNames)
        {
            List<string> actualNames = document.Properties()
                .Select(property => property.Name)
                .ToList();
            if (actualNames.Count != expectedNames.Length ||
                expectedNames.Any(expected => !actualNames.Contains(expected)))
            {
                throw new FormatException("Player Profile contains missing or unknown fields.");
            }
        }

        /// <summary>严格读取一个必需整数，不接受字符串或浮点转换。</summary>
        private static int RequireInteger(JObject document, string propertyName)
        {
            JToken token = RequireToken(document, propertyName, JTokenType.Integer);
            return token.Value<int>();
        }

        /// <summary>严格读取一个必需布尔值，不接受字符串或数值转换。</summary>
        private static bool RequireBoolean(JObject document, string propertyName)
        {
            JToken token = RequireToken(document, propertyName, JTokenType.Boolean);
            return token.Value<bool>();
        }

        /// <summary>严格读取一个必需对象。</summary>
        private static JObject RequireObject(JObject document, string propertyName)
        {
            return (JObject)RequireToken(document, propertyName, JTokenType.Object);
        }

        /// <summary>严格读取一个必需数组。</summary>
        private static JArray RequireArray(JObject document, string propertyName)
        {
            return (JArray)RequireToken(document, propertyName, JTokenType.Array);
        }

        /// <summary>读取必需属性并拒绝 null 或类型不匹配。</summary>
        private static JToken RequireToken(
            JObject document,
            string propertyName,
            JTokenType expectedType)
        {
            JToken token = document[propertyName];
            if (token == null || token.Type != expectedType)
            {
                throw new FormatException(
                    $"Player Profile field '{propertyName}' must be {expectedType}.");
            }

            return token;
        }
    }
}
