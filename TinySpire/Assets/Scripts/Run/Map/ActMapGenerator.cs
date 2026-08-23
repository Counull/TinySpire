using System;
using System.Collections.Generic;
using System.Linq;

namespace TinySpire.Run.Map
{
    /// <summary>从固定 profile 与独立 seed 一次性生成完整确定性 Act 地图。</summary>
    public static class ActMapGenerator
    {
        /// <summary>当前生成算法的持久化兼容版本。</summary>
        public const int CurrentVersion = 1;

        /// <summary>生成并冻结整张地图；相同输入在同版本内得到相同定义。</summary>
        public static MapDefinition Generate(ActMapProfile profile, uint mapSeed)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (mapSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(mapSeed));

            var nodes = new List<MapNode>();
            var edges = new List<MapEdge>();
            var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<int> bossEndpointIds = FreezeBossEndpointIds(profile, mapSeed);

            nodes.Add(CreateNode(layer: 0, slot: 0, MapNodeKind.Start, contentId: 0));
            for (int layerIndex = 0; layerIndex < profile.NormalLayerSlotCounts.Count; layerIndex++)
            {
                int layer = layerIndex + 1;
                int slotCount = profile.NormalLayerSlotCounts[layerIndex];
                for (int slot = 0; slot < slotCount; slot++)
                {
                    int encounterIndex = DeterministicIndex(
                        mapSeed,
                        layer,
                        slot,
                        salt: 1u,
                        profile.EncounterIds.Count);
                    nodes.Add(CreateNode(
                        layer,
                        slot,
                        MapNodeKind.Combat,
                        profile.EncounterIds[encounterIndex]));
                }
            }

            for (int slot = 0; slot < bossEndpointIds.Count; slot++)
            {
                nodes.Add(CreateNode(
                    profile.BossLayer,
                    slot,
                    MapNodeKind.Boss,
                    bossEndpointIds[slot]));
            }

            ConnectStart(
                profile.NormalLayerSlotCounts[0],
                edges,
                edgeKeys);
            for (int layer = 1; layer < profile.BossLayer - 1; layer++)
            {
                ConnectAdjacentLayers(
                    mapSeed,
                    layer,
                    profile.NormalLayerSlotCounts[layer - 1],
                    profile.NormalLayerSlotCounts[layer],
                    addBranchEdge: true,
                    edges,
                    edgeKeys);
            }

            ConnectAdjacentLayers(
                mapSeed,
                profile.BossLayer - 1,
                profile.NormalLayerSlotCounts[profile.NormalLayerSlotCounts.Count - 1],
                bossEndpointIds.Count,
                addBranchEdge: false,
                edges,
                edgeKeys);

            MapEdge[] orderedEdges = edges
                .OrderBy(edge => edge.FromNodeId.Value, StringComparer.Ordinal)
                .ThenBy(edge => edge.ToNodeId.Value, StringComparer.Ordinal)
                .ToArray();
            return new MapDefinition(
                profile.ProfileId,
                CurrentVersion,
                mapSeed,
                nodes,
                orderedEdges);
        }

        /// <summary>按地图 seed 无放回冻结 Boss 候选，并让每名候选至少占有一个终点。</summary>
        internal static IReadOnlyList<int> FreezeBossEndpointIds(
            ActMapProfile profile,
            uint mapSeed)
        {
            var remainingBossIds = profile.EnabledBossIds.ToList();
            var candidates = new List<int>(profile.BossCandidateCount);
            for (int ordinal = 0; ordinal < profile.BossCandidateCount; ordinal++)
            {
                int selectedIndex = DeterministicIndex(
                    mapSeed,
                    ordinal,
                    remainingBossIds.Count,
                    salt: 5u,
                    remainingBossIds.Count);
                candidates.Add(remainingBossIds[selectedIndex]);
                remainingBossIds.RemoveAt(selectedIndex);
            }

            var endpoints = new int[profile.BossEndpointCount];
            for (int slot = 0; slot < endpoints.Length; slot++)
            {
                int candidateIndex = slot < candidates.Count
                    ? slot
                    : DeterministicIndex(
                        mapSeed,
                        profile.BossLayer,
                        slot,
                        salt: 6u,
                        candidates.Count);
                endpoints[slot] = candidates[candidateIndex];
            }

            return endpoints;
        }

        /// <summary>从层与 Slot 创建身份完全一致的冻结节点。</summary>
        private static MapNode CreateNode(
            int layer,
            int slot,
            MapNodeKind kind,
            int contentId)
        {
            return new MapNode(
                MapNodeId.FromPosition(layer, slot),
                layer,
                slot,
                kind,
                contentId);
        }

        /// <summary>令唯一 Start 普通连接第一层全部已生成节点。</summary>
        private static void ConnectStart(
            int firstLayerSlotCount,
            ICollection<MapEdge> edges,
            ISet<string> edgeKeys)
        {
            MapNodeId start = MapNodeId.FromPosition(layer: 0, slot: 0);
            for (int slot = 0; slot < firstLayerSlotCount; slot++)
                AddEdge(start, MapNodeId.FromPosition(layer: 1, slot), edges, edgeKeys);
        }

        /// <summary>以确定性偏移覆盖相邻两层的全部入口与出口，并可增加一条分支边。</summary>
        private static void ConnectAdjacentLayers(
            uint mapSeed,
            int sourceLayer,
            int sourceSlotCount,
            int targetSlotCount,
            bool addBranchEdge,
            ICollection<MapEdge> edges,
            ISet<string> edgeKeys)
        {
            int offset = DeterministicIndex(
                mapSeed,
                sourceLayer,
                targetSlotCount,
                salt: 2u,
                targetSlotCount);

            for (int targetOrdinal = 0; targetOrdinal < targetSlotCount; targetOrdinal++)
            {
                int sourceSlot = targetOrdinal % sourceSlotCount;
                int targetSlot = (targetOrdinal + offset) % targetSlotCount;
                AddEdge(
                    MapNodeId.FromPosition(sourceLayer, sourceSlot),
                    MapNodeId.FromPosition(sourceLayer + 1, targetSlot),
                    edges,
                    edgeKeys);
            }

            for (int sourceSlot = 0; sourceSlot < sourceSlotCount; sourceSlot++)
            {
                int targetSlot = (sourceSlot + offset) % targetSlotCount;
                AddEdge(
                    MapNodeId.FromPosition(sourceLayer, sourceSlot),
                    MapNodeId.FromPosition(sourceLayer + 1, targetSlot),
                    edges,
                    edgeKeys);
            }

            if (!addBranchEdge || sourceSlotCount == 0 || targetSlotCount <= 1)
                return;

            int branchSource = DeterministicIndex(
                mapSeed,
                sourceLayer,
                sourceSlotCount,
                salt: 3u,
                sourceSlotCount);
            int primaryTarget = (branchSource + offset) % targetSlotCount;
            int branchShift = 1 + DeterministicIndex(
                mapSeed,
                sourceLayer,
                branchSource,
                salt: 4u,
                targetSlotCount - 1);
            int branchTarget = (primaryTarget + branchShift) % targetSlotCount;
            AddEdge(
                MapNodeId.FromPosition(sourceLayer, branchSource),
                MapNodeId.FromPosition(sourceLayer + 1, branchTarget),
                edges,
                edgeKeys);
        }

        /// <summary>只把尚未存在的稳定边加入整图。</summary>
        private static void AddEdge(
            MapNodeId fromNodeId,
            MapNodeId toNodeId,
            ICollection<MapEdge> edges,
            ISet<string> edgeKeys)
        {
            string key = $"{fromNodeId.Value}>{toNodeId.Value}";
            if (!edgeKeys.Add(key))
                return;

            edges.Add(new MapEdge(fromNodeId, toNodeId));
        }

        /// <summary>把 seed、位置与用途混合成稳定的零基索引。</summary>
        private static int DeterministicIndex(
            uint mapSeed,
            int firstCoordinate,
            int secondCoordinate,
            uint salt,
            int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));

            uint mixed = mapSeed;
            unchecked
            {
                mixed ^= ((uint)firstCoordinate + 1u) * 0x9E3779B9u;
                mixed ^= ((uint)secondCoordinate + 1u) * 0x85EBCA6Bu;
                mixed ^= salt * 0xC2B2AE35u;
                mixed ^= mixed >> 16;
                mixed *= 0x7FEB352Du;
                mixed ^= mixed >> 15;
                mixed *= 0x846CA68Bu;
                mixed ^= mixed >> 16;
            }

            return (int)(mixed % (uint)exclusiveMax);
        }
    }
}
