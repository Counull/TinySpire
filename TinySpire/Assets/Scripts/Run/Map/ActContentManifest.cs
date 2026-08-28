using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TinySpire.Run.Map
{
    /// <summary>一个 Act 达成终局所需的权威完成规则。</summary>
    public enum ActCompletionRule
    {
        BossVictory = 1,
    }

    /// <summary>冻结一个程序化非战斗节点的物品、奖励池与文本依赖。</summary>
    public sealed class ActNonCombatContentReference
    {
        private readonly ReadOnlyCollection<int> _relicTemplateIds;
        private readonly ReadOnlyCollection<int> _potionTemplateIds;
        private readonly ReadOnlyCollection<string> _requiredLocalizationKeys;

        /// <summary>该程序化内容只能绑定到明确的非战斗节点种类。</summary>
        public MapNodeKind Kind { get; }

        /// <summary>与地图 profile 逐层冻结的正整数内容 anchor。</summary>
        public int ContentId { get; }

        /// <summary>该节点可能提供且必须存在于物品表的遗物模板。</summary>
        public IReadOnlyList<int> RelicTemplateIds => _relicTemplateIds;

        /// <summary>该节点可能提供且必须存在于物品表的药水模板。</summary>
        public IReadOnlyList<int> PotionTemplateIds => _potionTemplateIds;

        /// <summary>该节点是否依赖现有 Hero 卡牌奖励池生成候选。</summary>
        public bool UsesHeroCardRewardPool { get; }

        /// <summary>完成该节点产品链必须具备的本地化 key。</summary>
        public IReadOnlyList<string> RequiredLocalizationKeys => _requiredLocalizationKeys;

        /// <summary>复制并验证一份窄的非战斗内容引用，不复制运行时 payload 或状态。</summary>
        public ActNonCombatContentReference(
            MapNodeKind kind,
            int contentId,
            IReadOnlyList<int> relicTemplateIds,
            IReadOnlyList<int> potionTemplateIds,
            bool usesHeroCardRewardPool,
            IReadOnlyList<string> requiredLocalizationKeys)
        {
            if (kind != MapNodeKind.Rest &&
                kind != MapNodeKind.Chest &&
                kind != MapNodeKind.Shop &&
                kind != MapNodeKind.Event)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (contentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentId));
            if (usesHeroCardRewardPool && kind != MapNodeKind.Shop)
                throw new ArgumentException("Only Shop content can use a Hero reward pool.", nameof(kind));

            Kind = kind;
            ContentId = contentId;
            _relicTemplateIds = CopyOptionalPositiveUniqueIds(
                relicTemplateIds,
                nameof(relicTemplateIds));
            _potionTemplateIds = CopyOptionalPositiveUniqueIds(
                potionTemplateIds,
                nameof(potionTemplateIds));
            UsesHeroCardRewardPool = usesHeroCardRewardPool;
            _requiredLocalizationKeys = CopyRequiredKeys(
                requiredLocalizationKeys,
                nameof(requiredLocalizationKeys));
        }

        /// <summary>复制允许为空但只含正整数且不重复的模板身份集合。</summary>
        private static ReadOnlyCollection<int> CopyOptionalPositiveUniqueIds(
            IReadOnlyList<int> values,
            string parameterName)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);

            var seen = new HashSet<int>();
            var copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                if (value <= 0 || !seen.Add(value))
                    throw new ArgumentException("Template ids must be positive and unique.", parameterName);
                copy[index] = value;
            }

            return Array.AsReadOnly(copy);
        }

        /// <summary>复制该节点非空、无首尾空白且不重复的产品文案 key。</summary>
        private static ReadOnlyCollection<string> CopyRequiredKeys(
            IReadOnlyList<string> values,
            string parameterName)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);
            if (values.Count == 0)
                throw new ArgumentException("At least one localization key is required.", parameterName);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var copy = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                if (string.IsNullOrWhiteSpace(value) ||
                    !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                    !seen.Add(value))
                {
                    throw new ArgumentException(
                        "Localization keys must be non-empty, trimmed and unique.",
                        parameterName);
                }
                copy[index] = value;
            }

            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>把既有地图 profile 与其普通、精英、Boss、文本和唯一奖励引用冻结为一个内容清单。</summary>
    public sealed class ActContentManifest
    {
        private readonly ReadOnlyCollection<int> _ordinaryEncounterIds;
        private readonly ReadOnlyCollection<int> _eliteEncounterIds;
        private readonly ReadOnlyCollection<ActNonCombatContentReference> _nonCombatContents;
        private readonly ReadOnlyDictionary<int, int> _bossEncounterIds;
        private readonly ReadOnlyCollection<int> _uniqueRelicTemplateIds;
        private readonly ReadOnlyCollection<string> _requiredLocalizationKeys;

        /// <summary>既有地图生成与结构校验继续使用的唯一 profile。</summary>
        public ActMapProfile Profile { get; }

        /// <summary>普通战斗节点允许引用的去重 Encounter 池。</summary>
        public IReadOnlyList<int> OrdinaryEncounterIds => _ordinaryEncounterIds;

        /// <summary>精英战斗节点允许引用的去重 Encounter 池。</summary>
        public IReadOnlyList<int> EliteEncounterIds => _eliteEncounterIds;

        /// <summary>地图中全部程序化非战斗 anchor 及其产品链依赖。</summary>
        public IReadOnlyList<ActNonCombatContentReference> NonCombatContents => _nonCombatContents;

        /// <summary>地图 Boss 身份到真实 Boss Encounter 的只读映射。</summary>
        public IReadOnlyDictionary<int, int> BossEncounterIds => _bossEncounterIds;

        /// <summary>整个 Act 内容目录声明的模板唯一遗物集合。</summary>
        public IReadOnlyList<int> UniqueRelicTemplateIds => _uniqueRelicTemplateIds;

        /// <summary>Act 地图与终局表现必须具备的本地化 key。</summary>
        public IReadOnlyList<string> RequiredLocalizationKeys => _requiredLocalizationKeys;

        /// <summary>Boss 胜利是当前单 Act 唯一完成规则。</summary>
        public ActCompletionRule CompletionRule { get; }

        /// <summary>复制并验证一份不会与既有地图 profile 漂移的静态 Act 内容清单。</summary>
        public ActContentManifest(
            ActMapProfile profile,
            IReadOnlyList<int> ordinaryEncounterIds,
            IReadOnlyList<int> eliteEncounterIds,
            IReadOnlyList<ActNonCombatContentReference> nonCombatContents,
            IReadOnlyDictionary<int, int> bossEncounterIds,
            IReadOnlyList<int> uniqueRelicTemplateIds,
            IReadOnlyList<string> requiredLocalizationKeys,
            ActCompletionRule completionRule)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _ordinaryEncounterIds = CopyPositiveUniqueIds(
                ordinaryEncounterIds,
                nameof(ordinaryEncounterIds));
            _eliteEncounterIds = CopyPositiveUniqueIds(
                eliteEncounterIds,
                nameof(eliteEncounterIds));
            _nonCombatContents = CopyNonCombatContents(nonCombatContents);
            _bossEncounterIds = CopyBossEncounterIds(bossEncounterIds, profile);
            _uniqueRelicTemplateIds = CopyPositiveUniqueIds(
                uniqueRelicTemplateIds,
                nameof(uniqueRelicTemplateIds));
            _requiredLocalizationKeys = CopyUniqueKeys(
                requiredLocalizationKeys,
                nameof(requiredLocalizationKeys));
            if (completionRule != ActCompletionRule.BossVictory)
                throw new ArgumentOutOfRangeException(nameof(completionRule));
            CompletionRule = completionRule;

            ValidateContentReferencesMatchProfile(
                profile,
                _ordinaryEncounterIds,
                _eliteEncounterIds,
                _nonCombatContents);
        }

        /// <summary>按地图 Boss 身份返回唯一真实 Encounter；未登记身份立即失败。</summary>
        public int GetBossEncounterId(int bossIdentity)
        {
            if (!_bossEncounterIds.TryGetValue(bossIdentity, out int encounterId))
                throw new KeyNotFoundException($"Boss identity {bossIdentity} is not registered by this Act.");

            return encounterId;
        }

        /// <summary>复制正整数且不重复的内容身份集合。</summary>
        private static ReadOnlyCollection<int> CopyPositiveUniqueIds(
            IReadOnlyList<int> values,
            string parameterName)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);
            if (values.Count == 0)
                throw new ArgumentException("At least one content id is required.", parameterName);

            var seen = new HashSet<int>();
            var copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                if (value <= 0 || !seen.Add(value))
                    throw new ArgumentException("Content ids must be positive and unique.", parameterName);
                copy[index] = value;
            }

            return Array.AsReadOnly(copy);
        }

        /// <summary>复制 Boss 映射并要求它与 profile 的全部候选身份精确一致。</summary>
        private static ReadOnlyDictionary<int, int> CopyBossEncounterIds(
            IReadOnlyDictionary<int, int> values,
            ActMapProfile profile)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count == 0)
                throw new ArgumentException("At least one Boss encounter is required.", nameof(values));

            var copy = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> value in values)
            {
                if (value.Key <= 0 || value.Value <= 0 || !copy.TryAdd(value.Key, value.Value))
                    throw new ArgumentException("Boss identities must be unique and both ids must be positive.", nameof(values));
            }

            if (!new HashSet<int>(copy.Keys).SetEquals(profile.EnabledBossIds))
                throw new ArgumentException("Boss encounter identities must exactly match the map profile.", nameof(values));

            return new ReadOnlyDictionary<int, int>(copy);
        }

        /// <summary>复制非空、无首尾空白且不重复的本地化 key。</summary>
        private static ReadOnlyCollection<string> CopyUniqueKeys(
            IReadOnlyList<string> values,
            string parameterName)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);
            if (values.Count == 0)
                throw new ArgumentException("At least one localization key is required.", parameterName);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var copy = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                if (string.IsNullOrWhiteSpace(value) ||
                    !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                    !seen.Add(value))
                {
                    throw new ArgumentException(
                        "Localization keys must be non-empty, trimmed and unique.",
                        parameterName);
                }
                copy[index] = value;
            }

            return Array.AsReadOnly(copy);
        }

        /// <summary>复制非空且不含重复 Kind/Content anchor 的非战斗引用集合。</summary>
        private static ReadOnlyCollection<ActNonCombatContentReference> CopyNonCombatContents(
            IReadOnlyList<ActNonCombatContentReference> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count == 0)
                throw new ArgumentException("At least one non-combat content reference is required.", nameof(values));

            var seen = new HashSet<(MapNodeKind Kind, int ContentId)>();
            var copy = new ActNonCombatContentReference[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                ActNonCombatContentReference value = values[index]
                    ?? throw new ArgumentException(
                        "Non-combat content references cannot contain null entries.",
                        nameof(values));
                if (!seen.Add((value.Kind, value.ContentId)))
                {
                    throw new ArgumentException(
                        "Non-combat Kind and content anchors must be unique.",
                        nameof(values));
                }
                copy[index] = value;
            }

            return Array.AsReadOnly(copy);
        }

        /// <summary>确保战斗池与非战斗 anchor 都精确等于 profile 实际冻结的内容集合。</summary>
        private static void ValidateContentReferencesMatchProfile(
            ActMapProfile profile,
            IReadOnlyCollection<int> ordinaryEncounterIds,
            IReadOnlyCollection<int> eliteEncounterIds,
            IReadOnlyCollection<ActNonCombatContentReference> nonCombatContents)
        {
            int[] profileOrdinary = profile.PlayableLayers
                .Where(layer => layer.Kind == MapNodeKind.Combat)
                .Select(layer => layer.ContentId)
                .Distinct()
                .ToArray();
            int[] profileElite = profile.PlayableLayers
                .Where(layer => layer.Kind == MapNodeKind.Elite)
                .Select(layer => layer.ContentId)
                .Distinct()
                .ToArray();
            if (!new HashSet<int>(ordinaryEncounterIds).SetEquals(profileOrdinary))
                throw new ArgumentException("Ordinary encounter pool does not match the map profile.");
            if (!new HashSet<int>(eliteEncounterIds).SetEquals(profileElite))
                throw new ArgumentException("Elite encounter pool does not match the map profile.");

            var profileNonCombat = new HashSet<(MapNodeKind Kind, int ContentId)>(
                profile.PlayableLayers
                    .Where(layer => layer.Kind != MapNodeKind.Combat && layer.Kind != MapNodeKind.Elite)
                    .Select(layer => (layer.Kind, layer.ContentId)));
            var manifestNonCombat = new HashSet<(MapNodeKind Kind, int ContentId)>(
                nonCombatContents.Select(content => (content.Kind, content.ContentId)));
            if (!manifestNonCombat.SetEquals(profileNonCombat))
                throw new ArgumentException("Non-combat content anchors do not match the map profile.");
        }
    }

    /// <summary>登记当前可由新 Run 选择且可由存档 profile 身份恢复的 Act 内容清单。</summary>
    public static class TinySpireActContentCatalog
    {
        private static readonly ActContentManifest NewRunManifest = new ActContentManifest(
            TinySpireActMapProfiles.NewRunG7V1,
            ordinaryEncounterIds: new[] { 5001 },
            eliteEncounterIds: new[] { 5101 },
            nonCombatContents: new[]
            {
                new ActNonCombatContentReference(
                    MapNodeKind.Rest,
                    contentId: 7101,
                    relicTemplateIds: Array.Empty<int>(),
                    potionTemplateIds: Array.Empty<int>(),
                    usesHeroCardRewardPool: false,
                    requiredLocalizationKeys: new[]
                    {
                        "run.entry.rest.title",
                        "run.entry.rest.heal",
                        "run.entry.rest.upgrade",
                    }),
                new ActNonCombatContentReference(
                    MapNodeKind.Chest,
                    contentId: 7201,
                    relicTemplateIds: Array.Empty<int>(),
                    potionTemplateIds: new[] { 9001 },
                    usesHeroCardRewardPool: false,
                    requiredLocalizationKeys: new[]
                    {
                        "run.entry.chest.title",
                        "run.entry.chest.claim",
                        "run.entry.chest.skip",
                        "run.entry.chest.full",
                    }),
                new ActNonCombatContentReference(
                    MapNodeKind.Shop,
                    contentId: 7301,
                    relicTemplateIds: new[] { 8001 },
                    potionTemplateIds: new[] { 9001 },
                    usesHeroCardRewardPool: true,
                    requiredLocalizationKeys: new[]
                    {
                        "run.entry.shop.title",
                        "run.entry.shop.purchase",
                        "run.entry.shop.purchased",
                        "run.entry.shop.leave",
                    }),
                new ActNonCombatContentReference(
                    MapNodeKind.Event,
                    contentId: 7401,
                    relicTemplateIds: Array.Empty<int>(),
                    potionTemplateIds: Array.Empty<int>(),
                    usesHeroCardRewardPool: false,
                    requiredLocalizationKeys: new[]
                    {
                        "run.entry.event.title",
                        "run.entry.event.gain_gold",
                        "run.entry.event.paid_heal",
                    }),
            },
            bossEncounterIds: new Dictionary<int, int>
            {
                [9001] = 5201,
                [9002] = 5201,
                [9003] = 5201,
            },
            uniqueRelicTemplateIds: new[] { 8001 },
            requiredLocalizationKeys: new[]
            {
                "run.entry.map.elite_node",
                "run.entry.map.boss_node",
                "run.entry.outcome.victory",
                "run.entry.outcome.boss_defeat",
                "run.entry.outcome.abandoned",
                "run.entry.outcome.return_to_menu",
                "battle.enemy.boss_phase.one",
                "battle.enemy.boss_phase.two",
            },
            ActCompletionRule.BossVictory);

        /// <summary>读取生产新 Run 应使用的 G7 单 Act 内容清单。</summary>
        public static ActContentManifest NewRunG7V1 => NewRunManifest;

        /// <summary>按稳定地图 profile 身份返回对应 Act 清单；旧 profile 或未知身份返回空。</summary>
        public static ActContentManifest GetByProfileId(string profileId)
        {
            return string.Equals(
                profileId,
                TinySpireActMapProfiles.NewRunG7V1ProfileId,
                StringComparison.Ordinal)
                ? NewRunManifest
                : null;
        }
    }
}
