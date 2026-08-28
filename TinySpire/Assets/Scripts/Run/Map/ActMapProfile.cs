using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Run.Map
{
    /// <summary>生成器 v2 使用的固定单 Slot 可游玩层种类与内容身份。</summary>
    public sealed class ActMapPlayableLayer
    {
        /// <summary>该层唯一节点的明确玩法种类。</summary>
        public MapNodeKind Kind { get; }

        /// <summary>该层唯一节点冻结的正整数内容身份。</summary>
        public int ContentId { get; }

        /// <summary>冻结一个普通战斗、精英战斗或明确非战斗节点层。</summary>
        public ActMapPlayableLayer(MapNodeKind kind, int contentId)
        {
            if (kind != MapNodeKind.Combat &&
                kind != MapNodeKind.Elite &&
                kind != MapNodeKind.Rest &&
                kind != MapNodeKind.Chest &&
                kind != MapNodeKind.Shop &&
                kind != MapNodeKind.Event)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (contentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentId));

            Kind = kind;
            ContentId = contentId;
        }
    }

    /// <summary>冻结一个 Act 地图的固定层形、遭遇池与 Boss 终点配置。</summary>
    public sealed class ActMapProfile
    {
        private readonly ReadOnlyCollection<int> _normalLayerSlotCounts;
        private readonly ReadOnlyCollection<int> _encounterIds;
        private readonly ReadOnlyCollection<int> _enabledBossIds;
        private readonly ReadOnlyCollection<ActMapPlayableLayer> _playableLayers;

        /// <summary>用于存档重建的稳定配置标识。</summary>
        public string ProfileId { get; }

        /// <summary>该 profile 唯一绑定的持久化生成算法版本。</summary>
        public int GeneratorVersion { get; }

        /// <summary>普通战斗层从 Start 后开始的固定 Slot 数。</summary>
        public IReadOnlyList<int> NormalLayerSlotCounts => _normalLayerSlotCounts;

        /// <summary>生成普通或精英战斗节点时允许冻结的去重 Encounter 身份。</summary>
        public IReadOnlyList<int> EncounterIds => _encounterIds;

        /// <summary>生成器 v2 按路线顺序冻结的单 Slot 可游玩层；legacy G3 v1 为空。</summary>
        public IReadOnlyList<ActMapPlayableLayer> PlayableLayers => _playableLayers;

        /// <summary>当前 Act 允许按地图 seed 抽取候选的去重 Boss 池。</summary>
        public IReadOnlyList<int> EnabledBossIds => _enabledBossIds;

        /// <summary>每局从启用池中无放回冻结的 Boss 候选数量。</summary>
        public int BossCandidateCount { get; }

        /// <summary>Boss 层固定终点 Slot 数；可以大于候选数以形成同 Boss 多终点。</summary>
        public int BossEndpointCount { get; }

        /// <summary>Boss 终点所在的固定层号。</summary>
        public int BossLayer => GeneratorVersion == ActMapGenerator.NewRunG6Version
            ? _playableLayers.Count + 1
            : _normalLayerSlotCounts.Count + 1;

        /// <summary>复制并验证构成整张 Act 地图的最小固定配置。</summary>
        public ActMapProfile(
            string profileId,
            IReadOnlyList<int> normalLayerSlotCounts,
            IReadOnlyList<int> encounterIds,
            IReadOnlyList<int> enabledBossIds,
            int bossCandidateCount,
            int bossEndpointCount)
        {
            ValidateCommon(profileId, enabledBossIds, bossCandidateCount, bossEndpointCount);

            ProfileId = profileId;
            GeneratorVersion = ActMapGenerator.LegacyG3Version;
            _normalLayerSlotCounts = CopyPositiveValues(
                normalLayerSlotCounts,
                nameof(normalLayerSlotCounts));
            _encounterIds = CopyPositiveValues(encounterIds, nameof(encounterIds));
            _enabledBossIds = CopyPositiveUniqueValues(enabledBossIds, nameof(enabledBossIds));
            _playableLayers = Array.AsReadOnly(Array.Empty<ActMapPlayableLayer>());
            BossCandidateCount = bossCandidateCount;
            BossEndpointCount = bossEndpointCount;
        }

        /// <summary>创建只由 v2 生成器解释的固定 mixed 单路线 profile。</summary>
        public ActMapProfile(
            string profileId,
            IReadOnlyList<ActMapPlayableLayer> playableLayers,
            IReadOnlyList<int> enabledBossIds,
            int bossCandidateCount,
            int bossEndpointCount)
        {
            ValidateCommon(profileId, enabledBossIds, bossCandidateCount, bossEndpointCount);
            if (playableLayers == null)
                throw new ArgumentNullException(nameof(playableLayers));
            if (playableLayers.Count == 0)
                throw new ArgumentException("At least one playable layer is required.", nameof(playableLayers));

            var frozenLayers = new ActMapPlayableLayer[playableLayers.Count];
            var encounterIds = new List<int>();
            var seenEncounterIds = new HashSet<int>();
            for (int index = 0; index < playableLayers.Count; index++)
            {
                ActMapPlayableLayer layer = playableLayers[index]
                    ?? throw new ArgumentException(
                        "Playable layers cannot contain null entries.",
                        nameof(playableLayers));
                frozenLayers[index] = layer;
                if ((layer.Kind == MapNodeKind.Combat || layer.Kind == MapNodeKind.Elite) &&
                    seenEncounterIds.Add(layer.ContentId))
                    encounterIds.Add(layer.ContentId);
            }
            if (encounterIds.Count == 0)
                throw new ArgumentException("A mixed profile requires at least one Combat or Elite layer.", nameof(playableLayers));

            ProfileId = profileId;
            GeneratorVersion = ActMapGenerator.NewRunG6Version;
            _normalLayerSlotCounts = Array.AsReadOnly(Array.Empty<int>());
            _encounterIds = Array.AsReadOnly(encounterIds.ToArray());
            _enabledBossIds = CopyPositiveUniqueValues(enabledBossIds, nameof(enabledBossIds));
            _playableLayers = Array.AsReadOnly(frozenLayers);
            BossCandidateCount = bossCandidateCount;
            BossEndpointCount = bossEndpointCount;
        }

        /// <summary>验证两代 profile 共享的身份与 Boss 终点约束。</summary>
        private static void ValidateCommon(
            string profileId,
            IReadOnlyList<int> enabledBossIds,
            int bossCandidateCount,
            int bossEndpointCount)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));

            ReadOnlyCollection<int> bosses = CopyPositiveUniqueValues(
                enabledBossIds,
                nameof(enabledBossIds));
            if (bossCandidateCount <= 0 || bossCandidateCount > bosses.Count)
                throw new ArgumentOutOfRangeException(nameof(bossCandidateCount));
            if (bossEndpointCount < bossCandidateCount)
                throw new ArgumentOutOfRangeException(nameof(bossEndpointCount));
        }

        /// <summary>复制一个非空正整数序列，避免调用方后续改写 profile。</summary>
        private static ReadOnlyCollection<int> CopyPositiveValues(
            IReadOnlyList<int> values,
            string parameterName)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);
            if (values.Count == 0)
                throw new ArgumentException("At least one value is required.", parameterName);

            var copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] <= 0)
                    throw new ArgumentOutOfRangeException(parameterName, "Values must be positive.");

                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }

        /// <summary>复制一个非空、正数且不含重复身份的序列。</summary>
        private static ReadOnlyCollection<int> CopyPositiveUniqueValues(
            IReadOnlyList<int> values,
            string parameterName)
        {
            ReadOnlyCollection<int> copy = CopyPositiveValues(values, parameterName);
            var seen = new HashSet<int>();
            foreach (int value in copy)
            {
                if (!seen.Add(value))
                {
                    throw new ArgumentException(
                        "Boss ids in an enabled pool must be unique.",
                        parameterName);
                }
            }

            return copy;
        }
    }

    /// <summary>同时保留 G3/G6/G7 profile 与其生成器版本的稳定目录。</summary>
    public static class TinySpireActMapProfiles
    {
        public const string LegacyG3V1ProfileId = "tinyspire.act1.g3.v1";
        public const string NewRunG6V1ProfileId = "tinyspire.act1.g6.v1";
        public const string NewRunG7V1ProfileId = "tinyspire.act1.g7.v1";

        /// <summary>保留既有测试与旧调用方使用的 G3 profile 常量。</summary>
        public const string CurrentProfileId = LegacyG3V1ProfileId;

        private static readonly ActMapProfile LegacyProfile = new ActMapProfile(
            LegacyG3V1ProfileId,
            normalLayerSlotCounts: new[] { 2, 2 },
            encounterIds: new[] { 5001 },
            enabledBossIds: new[] { 9001, 9002, 9003 },
            bossCandidateCount: 2,
            bossEndpointCount: 3);

        private static readonly ActMapProfile NewRunProfile = new ActMapProfile(
            NewRunG6V1ProfileId,
            playableLayers: new[]
            {
                new ActMapPlayableLayer(MapNodeKind.Combat, 5001),
                new ActMapPlayableLayer(MapNodeKind.Rest, 7101),
                new ActMapPlayableLayer(MapNodeKind.Chest, 7201),
                new ActMapPlayableLayer(MapNodeKind.Shop, 7301),
                new ActMapPlayableLayer(MapNodeKind.Event, 7401),
                new ActMapPlayableLayer(MapNodeKind.Combat, 5001),
            },
            enabledBossIds: new[] { 9001, 9002, 9003 },
            bossCandidateCount: 2,
            bossEndpointCount: 3);

        private static readonly ActMapProfile NewRunG7Profile = new ActMapProfile(
            NewRunG7V1ProfileId,
            playableLayers: new[]
            {
                new ActMapPlayableLayer(MapNodeKind.Combat, 5001),
                new ActMapPlayableLayer(MapNodeKind.Rest, 7101),
                new ActMapPlayableLayer(MapNodeKind.Chest, 7201),
                new ActMapPlayableLayer(MapNodeKind.Shop, 7301),
                new ActMapPlayableLayer(MapNodeKind.Event, 7401),
                new ActMapPlayableLayer(MapNodeKind.Combat, 5001),
                new ActMapPlayableLayer(MapNodeKind.Elite, 5101),
            },
            enabledBossIds: new[] { 9001, 9002, 9003 },
            bossCandidateCount: 2,
            bossEndpointCount: 3);

        /// <summary>读取必须逐字节兼容恢复的 legacy G3 v1 profile。</summary>
        public static ActMapProfile LegacyG3V1 => LegacyProfile;

        /// <summary>读取生产新 Run 直接使用的 G6 profile v1 mixed 配方。</summary>
        public static ActMapProfile NewRunG6V1 => NewRunProfile;

        /// <summary>读取包含一个精英节点与真实 Boss 内容映射的 G7 单 Act profile。</summary>
        public static ActMapProfile NewRunG7V1 => NewRunG7Profile;

        /// <summary>保留既有 G3 测试调用方的兼容别名，不作为生产新 Run 选择器。</summary>
        public static ActMapProfile Current => LegacyProfile;

        /// <summary>按稳定配置 ID 精确解析各自版本的 Act profile，不存在时返回空。</summary>
        public static ActMapProfile GetById(string profileId)
        {
            if (string.Equals(profileId, LegacyG3V1ProfileId, StringComparison.Ordinal))
                return LegacyProfile;
            if (string.Equals(profileId, NewRunG6V1ProfileId, StringComparison.Ordinal))
                return NewRunProfile;
            if (string.Equals(profileId, NewRunG7V1ProfileId, StringComparison.Ordinal))
                return NewRunG7Profile;
            return null;
        }
    }
}
