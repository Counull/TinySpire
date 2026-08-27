using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TinySpire.Run.Map
{
    /// <summary>地图定义违反的稳定合同类别。</summary>
    public enum MapValidationErrorCode
    {
        ProfileMismatch,
        GeneratorVersionMismatch,
        DuplicateNodeId,
        DuplicateNodePosition,
        UnstableNodeId,
        StartContractViolation,
        InvalidStartLayerShape,
        NormalLayerContractViolation,
        BossLayerContractViolation,
        ContentReferenceViolation,
        DuplicateEdge,
        MissingEdgeEndpoint,
        NonAdjacentEdge,
        BossHasOutgoingEdge,
        CombatUnreachableFromStart,
        CombatCannotReachBoss,
        PlayableNodeUnreachableFromStart,
        PlayableNodeCannotReachBoss,
        BossUnreachableFromStart,
    }

    /// <summary>一条可诊断且不依赖异常文本解析的地图校验错误。</summary>
    public sealed class MapValidationError
    {
        /// <summary>稳定错误类别。</summary>
        public MapValidationErrorCode Code { get; }

        /// <summary>包含具体节点或边身份的诊断文本。</summary>
        public string Message { get; }

        /// <summary>冻结一条类型化校验错误。</summary>
        public MapValidationError(MapValidationErrorCode code, string message)
        {
            Code = code;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }
    }

    /// <summary>一次完整地图合同校验的不可变结果。</summary>
    public sealed class MapValidationResult
    {
        private readonly ReadOnlyCollection<MapValidationError> _errors;

        /// <summary>没有任何合同错误时为 true。</summary>
        public bool IsValid => _errors.Count == 0;

        /// <summary>全部类型化错误；一次校验尽量收集完整诊断。</summary>
        public IReadOnlyList<MapValidationError> Errors => _errors;

        /// <summary>复制并冻结一次校验收集的全部错误。</summary>
        public MapValidationResult(IReadOnlyList<MapValidationError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            _errors = Array.AsReadOnly(errors.ToArray());
        }
    }

    /// <summary>独立于 UI 与可变进度，验证整张冻结 Act 地图的结构与可达性。</summary>
    public static class ActMapValidator
    {
        /// <summary>对 profile、节点、边、内容引用与全图可达性执行完整校验。</summary>
        public static MapValidationResult Validate(MapDefinition map, ActMapProfile profile)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var errors = new List<MapValidationError>();
            ValidateMetadata(map, profile, errors);
            Dictionary<MapNodeId, MapNode> nodeById = ValidateNodes(map, profile, errors);
            Dictionary<MapNodeId, List<MapNodeId>> outgoing = ValidateEdges(
                map,
                nodeById,
                errors,
                out Dictionary<MapNodeId, List<MapNodeId>> incoming);
            ValidateReachability(profile, nodeById, outgoing, incoming, errors);
            return new MapValidationResult(errors);
        }

        /// <summary>验证地图 recipe 元数据与当前 profile、生成器版本完全一致。</summary>
        private static void ValidateMetadata(
            MapDefinition map,
            ActMapProfile profile,
            ICollection<MapValidationError> errors)
        {
            if (!string.Equals(map.ProfileId, profile.ProfileId, StringComparison.Ordinal))
            {
                AddError(
                    errors,
                    MapValidationErrorCode.ProfileMismatch,
                    $"Map profile '{map.ProfileId}' does not match '{profile.ProfileId}'.");
            }

            if (map.GeneratorVersion != profile.GeneratorVersion)
            {
                AddError(
                    errors,
                    MapValidationErrorCode.GeneratorVersionMismatch,
                    $"Map generator version '{map.GeneratorVersion}' does not match " +
                    $"profile version '{profile.GeneratorVersion}'.");
            }
        }

        /// <summary>验证固定位置、稳定 NodeId、节点种类与开局冻结内容引用。</summary>
        private static Dictionary<MapNodeId, MapNode> ValidateNodes(
            MapDefinition map,
            ActMapProfile profile,
            ICollection<MapValidationError> errors)
        {
            var nodeById = new Dictionary<MapNodeId, MapNode>();
            var positions = new HashSet<string>(StringComparer.Ordinal);
            foreach (MapNode node in map.Nodes)
            {
                if (!nodeById.TryAdd(node.Id, node))
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.DuplicateNodeId,
                        $"Duplicate node id '{node.Id}'.");
                }

                string position = $"{node.Layer}:{node.Slot}";
                if (!positions.Add(position))
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.DuplicateNodePosition,
                        $"Duplicate node position '{position}'.");
                }

                if (node.Id != MapNodeId.FromPosition(node.Layer, node.Slot))
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.UnstableNodeId,
                        $"Node '{node.Id}' does not match layer {node.Layer}, slot {node.Slot}.");
                }
            }

            ValidateStartNode(map, errors);
            if (profile.GeneratorVersion == ActMapGenerator.LegacyG3Version)
                ValidateNormalLayers(map, profile, errors);
            else
                ValidatePlayableLayers(map, profile, errors);
            ValidateBossLayer(map, profile, errors);
            return nodeById;
        }

        /// <summary>验证 v2 每个单 Slot mixed 层的种类与内容身份完全匹配 profile。</summary>
        private static void ValidatePlayableLayers(
            MapDefinition map,
            ActMapProfile profile,
            ICollection<MapValidationError> errors)
        {
            for (int index = 0; index < profile.PlayableLayers.Count; index++)
            {
                int layer = index + 1;
                ActMapPlayableLayer expected = profile.PlayableLayers[index];
                MapNode[] layerNodes = map.Nodes.Where(node => node.Layer == layer).ToArray();
                bool shapeMatches = layerNodes.Length == 1 &&
                                    layerNodes[0].Slot == 0 &&
                                    layerNodes[0].Kind == expected.Kind;
                if (!shapeMatches)
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.NormalLayerContractViolation,
                        $"Playable layer {layer} does not match its fixed kind and Slot.");
                    continue;
                }

                if (layerNodes[0].ContentId != expected.ContentId)
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.ContentReferenceViolation,
                        $"Playable node '{layerNodes[0].Id}' content {layerNodes[0].ContentId} " +
                        $"does not match frozen content {expected.ContentId}.");
                }
            }

            foreach (MapNode node in map.Nodes.Where(node =>
                node.Layer > 0 && node.Layer < profile.BossLayer))
            {
                if (node.Layer > profile.PlayableLayers.Count)
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.NormalLayerContractViolation,
                        $"Node '{node.Id}' is outside the fixed playable layers.");
                }
            }
        }

        /// <summary>验证唯一 Start 固定在第零层第零 Slot 且不携带内容。</summary>
        private static void ValidateStartNode(
            MapDefinition map,
            ICollection<MapValidationError> errors)
        {
            MapNode[] starts = map.Nodes.Where(node => node.Kind == MapNodeKind.Start).ToArray();
            if (starts.Length != 1 ||
                starts[0].Layer != 0 ||
                starts[0].Slot != 0 ||
                starts[0].ContentId != 0)
            {
                AddError(
                    errors,
                    MapValidationErrorCode.StartContractViolation,
                    "Map must contain exactly one empty Start at layer 0, slot 0.");
            }

            MapNode[] startLayerNodes = map.Nodes.Where(node => node.Layer == 0).ToArray();
            if (startLayerNodes.Length != 1 ||
                startLayerNodes[0].Kind != MapNodeKind.Start ||
                startLayerNodes[0].Slot != 0 ||
                startLayerNodes[0].ContentId != 0)
            {
                AddError(
                    errors,
                    MapValidationErrorCode.InvalidStartLayerShape,
                    "Layer 0 must contain only the empty Start at slot 0.");
            }
        }

        /// <summary>验证每个普通层精确采用 profile 的固定 Slot 集与 Encounter 池。</summary>
        private static void ValidateNormalLayers(
            MapDefinition map,
            ActMapProfile profile,
            ICollection<MapValidationError> errors)
        {
            var encounterIds = new HashSet<int>(profile.EncounterIds);
            for (int layerIndex = 0; layerIndex < profile.NormalLayerSlotCounts.Count; layerIndex++)
            {
                int layer = layerIndex + 1;
                int expectedSlotCount = profile.NormalLayerSlotCounts[layerIndex];
                MapNode[] layerNodes = map.Nodes.Where(node => node.Layer == layer).ToArray();
                bool positionsMatch = layerNodes.Length == expectedSlotCount &&
                    Enumerable.Range(0, expectedSlotCount).All(slot =>
                        layerNodes.Count(node =>
                            node.Slot == slot && node.Kind == MapNodeKind.Combat) == 1);
                if (!positionsMatch)
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.NormalLayerContractViolation,
                        $"Normal layer {layer} does not match its fixed Slot configuration.");
                }

                foreach (MapNode node in layerNodes.Where(node => node.Kind == MapNodeKind.Combat))
                {
                    if (!encounterIds.Contains(node.ContentId))
                    {
                        AddError(
                            errors,
                            MapValidationErrorCode.ContentReferenceViolation,
                            $"Combat node '{node.Id}' references EncounterId {node.ContentId} outside the profile.");
                    }
                }
            }

            foreach (MapNode node in map.Nodes.Where(node =>
                node.Layer > 0 && node.Layer < profile.BossLayer))
            {
                if (node.Kind != MapNodeKind.Combat)
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.NormalLayerContractViolation,
                        $"Node '{node.Id}' in a normal layer is not Combat.");
                }
            }
        }

        /// <summary>验证 Boss 层 Slot 数与 seed 冻结的候选及终点身份完全一致。</summary>
        private static void ValidateBossLayer(
            MapDefinition map,
            ActMapProfile profile,
            ICollection<MapValidationError> errors)
        {
            MapNode[] bossLayerNodes = map.Nodes
                .Where(node => node.Layer == profile.BossLayer)
                .ToArray();
            IReadOnlyList<int> expectedBossIds = ActMapGenerator.FreezeBossEndpointIds(
                profile,
                map.MapSeed);
            bool positionsMatch = bossLayerNodes.Length == expectedBossIds.Count &&
                Enumerable.Range(0, expectedBossIds.Count).All(slot =>
                    bossLayerNodes.Count(node =>
                        node.Slot == slot &&
                        node.Kind == MapNodeKind.Boss &&
                        node.ContentId == expectedBossIds[slot]) == 1);
            bool noNodesOutsideProfile = map.Nodes.All(node => node.Layer <= profile.BossLayer);
            if (!positionsMatch || !noNodesOutsideProfile)
            {
                AddError(
                    errors,
                    MapValidationErrorCode.BossLayerContractViolation,
                    "Boss layer does not match the profile endpoints.");
            }
        }

        /// <summary>验证边唯一、端点存在、只跨越相邻层且 Boss 没有出边。</summary>
        private static Dictionary<MapNodeId, List<MapNodeId>> ValidateEdges(
            MapDefinition map,
            IReadOnlyDictionary<MapNodeId, MapNode> nodeById,
            ICollection<MapValidationError> errors,
            out Dictionary<MapNodeId, List<MapNodeId>> incoming)
        {
            var outgoing = CreateAdjacency(nodeById.Keys);
            incoming = CreateAdjacency(nodeById.Keys);
            var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (MapEdge edge in map.Edges)
            {
                string edgeKey = $"{edge.FromNodeId.Value}>{edge.ToNodeId.Value}";
                if (!edgeKeys.Add(edgeKey))
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.DuplicateEdge,
                        $"Duplicate edge '{edgeKey}'.");
                    continue;
                }

                if (!nodeById.TryGetValue(edge.FromNodeId, out MapNode fromNode) ||
                    !nodeById.TryGetValue(edge.ToNodeId, out MapNode toNode))
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.MissingEdgeEndpoint,
                        $"Edge '{edgeKey}' references a missing endpoint.");
                    continue;
                }

                if (toNode.Layer != fromNode.Layer + 1 || toNode.Kind == MapNodeKind.Start)
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.NonAdjacentEdge,
                        $"Edge '{edgeKey}' must target the immediately following layer.");
                    continue;
                }

                if (fromNode.Kind == MapNodeKind.Boss)
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.BossHasOutgoingEdge,
                        $"Boss node '{fromNode.Id}' cannot have outgoing edges.");
                    continue;
                }

                outgoing[fromNode.Id].Add(toNode.Id);
                incoming[toNode.Id].Add(fromNode.Id);
            }

            return outgoing;
        }

        /// <summary>建立包含全部节点且初始为空的邻接表。</summary>
        private static Dictionary<MapNodeId, List<MapNodeId>> CreateAdjacency(
            IEnumerable<MapNodeId> nodeIds)
        {
            var adjacency = new Dictionary<MapNodeId, List<MapNodeId>>();
            foreach (MapNodeId nodeId in nodeIds)
                adjacency[nodeId] = new List<MapNodeId>();
            return adjacency;
        }

        /// <summary>验证全部普通节点与 Boss 终点均从 Start 可达，且普通节点没有 Boss 死路。</summary>
        private static void ValidateReachability(
            ActMapProfile profile,
            IReadOnlyDictionary<MapNodeId, MapNode> nodeById,
            IReadOnlyDictionary<MapNodeId, List<MapNodeId>> outgoing,
            IReadOnlyDictionary<MapNodeId, List<MapNodeId>> incoming,
            ICollection<MapValidationError> errors)
        {
            MapNodeId startId = MapNodeId.FromPosition(layer: 0, slot: 0);
            if (!nodeById.ContainsKey(startId))
                return;

            HashSet<MapNodeId> reachableFromStart = Traverse(
                new[] { startId },
                outgoing);
            MapNodeId[] bossNodeIds = nodeById.Values
                .Where(node => node.Kind == MapNodeKind.Boss)
                .Select(node => node.Id)
                .ToArray();
            HashSet<MapNodeId> canReachBoss = Traverse(bossNodeIds, incoming);

            bool validatesAllPlayableNodes =
                profile.GeneratorVersion == ActMapGenerator.NewRunG6Version;
            IEnumerable<MapNode> playableNodes = validatesAllPlayableNodes
                ? nodeById.Values.Where(node =>
                    node.Kind != MapNodeKind.Start && node.Kind != MapNodeKind.Boss)
                : nodeById.Values.Where(node => node.Kind == MapNodeKind.Combat);
            foreach (MapNode node in playableNodes)
            {
                if (!reachableFromStart.Contains(node.Id))
                {
                    AddError(
                        errors,
                        validatesAllPlayableNodes
                            ? MapValidationErrorCode.PlayableNodeUnreachableFromStart
                            : MapValidationErrorCode.CombatUnreachableFromStart,
                        $"Playable node '{node.Id}' is unreachable from Start.");
                }

                if (!canReachBoss.Contains(node.Id))
                {
                    AddError(
                        errors,
                        validatesAllPlayableNodes
                            ? MapValidationErrorCode.PlayableNodeCannotReachBoss
                            : MapValidationErrorCode.CombatCannotReachBoss,
                        $"Playable node '{node.Id}' cannot reach any Boss endpoint.");
                }
            }

            foreach (MapNode boss in nodeById.Values.Where(node => node.Kind == MapNodeKind.Boss))
            {
                if (!reachableFromStart.Contains(boss.Id))
                {
                    AddError(
                        errors,
                        MapValidationErrorCode.BossUnreachableFromStart,
                        $"Boss node '{boss.Id}' is unreachable from Start.");
                }
            }
        }

        /// <summary>从一个或多个起点沿给定邻接表收集全部可达节点。</summary>
        private static HashSet<MapNodeId> Traverse(
            IEnumerable<MapNodeId> startNodeIds,
            IReadOnlyDictionary<MapNodeId, List<MapNodeId>> adjacency)
        {
            var visited = new HashSet<MapNodeId>();
            var pending = new Queue<MapNodeId>();
            foreach (MapNodeId startNodeId in startNodeIds)
            {
                if (visited.Add(startNodeId))
                    pending.Enqueue(startNodeId);
            }

            while (pending.Count > 0)
            {
                MapNodeId current = pending.Dequeue();
                if (!adjacency.TryGetValue(current, out List<MapNodeId> nextNodeIds))
                    continue;

                foreach (MapNodeId nextNodeId in nextNodeIds)
                {
                    if (visited.Add(nextNodeId))
                        pending.Enqueue(nextNodeId);
                }
            }

            return visited;
        }

        /// <summary>向结果追加一条类型化错误。</summary>
        private static void AddError(
            ICollection<MapValidationError> errors,
            MapValidationErrorCode code,
            string message)
        {
            errors.Add(new MapValidationError(code, message));
        }
    }
}
