using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Run.Map
{
    /// <summary>冻结一个 Act 地图的固定层形、遭遇池与 Boss 终点配置。</summary>
    public sealed class ActMapProfile
    {
        private readonly ReadOnlyCollection<int> _normalLayerSlotCounts;
        private readonly ReadOnlyCollection<int> _encounterIds;
        private readonly ReadOnlyCollection<int> _enabledBossIds;

        /// <summary>用于存档重建的稳定配置标识。</summary>
        public string ProfileId { get; }

        /// <summary>普通战斗层从 Start 后开始的固定 Slot 数。</summary>
        public IReadOnlyList<int> NormalLayerSlotCounts => _normalLayerSlotCounts;

        /// <summary>生成普通节点时允许冻结的遭遇身份。</summary>
        public IReadOnlyList<int> EncounterIds => _encounterIds;

        /// <summary>当前 Act 允许按地图 seed 抽取候选的去重 Boss 池。</summary>
        public IReadOnlyList<int> EnabledBossIds => _enabledBossIds;

        /// <summary>每局从启用池中无放回冻结的 Boss 候选数量。</summary>
        public int BossCandidateCount { get; }

        /// <summary>Boss 层固定终点 Slot 数；可以大于候选数以形成同 Boss 多终点。</summary>
        public int BossEndpointCount { get; }

        /// <summary>Boss 终点所在的固定层号。</summary>
        public int BossLayer => _normalLayerSlotCounts.Count + 1;

        /// <summary>复制并验证构成整张 Act 地图的最小固定配置。</summary>
        public ActMapProfile(
            string profileId,
            IReadOnlyList<int> normalLayerSlotCounts,
            IReadOnlyList<int> encounterIds,
            IReadOnlyList<int> enabledBossIds,
            int bossCandidateCount,
            int bossEndpointCount)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));

            ProfileId = profileId;
            _normalLayerSlotCounts = CopyPositiveValues(
                normalLayerSlotCounts,
                nameof(normalLayerSlotCounts));
            _encounterIds = CopyPositiveValues(encounterIds, nameof(encounterIds));
            _enabledBossIds = CopyPositiveUniqueValues(enabledBossIds, nameof(enabledBossIds));
            if (bossCandidateCount <= 0 || bossCandidateCount > _enabledBossIds.Count)
                throw new ArgumentOutOfRangeException(nameof(bossCandidateCount));
            if (bossEndpointCount < bossCandidateCount)
                throw new ArgumentOutOfRangeException(nameof(bossEndpointCount));

            BossCandidateCount = bossCandidateCount;
            BossEndpointCount = bossEndpointCount;
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

    /// <summary>G3 首个可游玩 Act 使用的固定地图测试 profile 目录。</summary>
    public static class TinySpireActMapProfiles
    {
        public const string CurrentProfileId = "tinyspire.act1.g3.v1";

        private static readonly ActMapProfile CurrentProfile = new ActMapProfile(
            CurrentProfileId,
            normalLayerSlotCounts: new[] { 2, 2 },
            encounterIds: new[] { 5001 },
            enabledBossIds: new[] { 9001, 9002, 9003 },
            bossCandidateCount: 2,
            bossEndpointCount: 3);

        /// <summary>读取当前 G3 Act 的不可变固定 profile。</summary>
        public static ActMapProfile Current => CurrentProfile;

        /// <summary>按稳定配置 ID 解析当前支持的 Act profile，不存在时返回空。</summary>
        public static ActMapProfile GetById(string profileId)
        {
            return string.Equals(profileId, CurrentProfileId, StringComparison.Ordinal)
                ? CurrentProfile
                : null;
        }
    }
}
