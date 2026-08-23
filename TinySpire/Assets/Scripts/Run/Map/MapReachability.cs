using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TinySpire.Run.Map
{
    /// <summary>选择下一层节点时采用的纯地图移动规则。</summary>
    public enum MapTraversalMode
    {
        Ordinary,
        WingBootsNextLayer,
    }

    /// <summary>从一个候选节点出发可见的完整后半程与 Boss 身份。</summary>
    public sealed class MapDownstreamRoute
    {
        private readonly ReadOnlyCollection<MapNodeId> _nodeIds;
        private readonly ReadOnlyCollection<MapEdge> _edges;
        private readonly ReadOnlyCollection<int> _reachableBossIds;

        /// <summary>包含候选本身在内的全部后继节点身份。</summary>
        public IReadOnlyList<MapNodeId> NodeIds => _nodeIds;

        /// <summary>后半程中连接可达节点的全部冻结边。</summary>
        public IReadOnlyList<MapEdge> Edges => _edges;

        /// <summary>后半程可抵达的去重 Boss 身份。</summary>
        public IReadOnlyList<int> ReachableBossIds => _reachableBossIds;

        /// <summary>复制并冻结一次后半程遍历结果。</summary>
        public MapDownstreamRoute(
            IReadOnlyList<MapNodeId> nodeIds,
            IReadOnlyList<MapEdge> edges,
            IReadOnlyList<int> reachableBossIds)
        {
            if (nodeIds == null)
                throw new ArgumentNullException(nameof(nodeIds));
            if (edges == null)
                throw new ArgumentNullException(nameof(edges));
            if (reachableBossIds == null)
                throw new ArgumentNullException(nameof(reachableBossIds));

            _nodeIds = Array.AsReadOnly(nodeIds.ToArray());
            _edges = Array.AsReadOnly(edges.ToArray());
            _reachableBossIds = Array.AsReadOnly(reachableBossIds.ToArray());
        }
    }

    /// <summary>集中计算地图可选性，避免 View 或节点对象私自解释拓扑。</summary>
    public static class MapReachability
    {
        /// <summary>按指定纯规则返回从当前节点可选择的稳定节点身份。</summary>
        public static IReadOnlyList<MapNodeId> GetSelectableNodeIds(
            MapDefinition map,
            MapNodeId currentNodeId,
            MapTraversalMode traversalMode)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            MapNode current = map.GetNode(currentNodeId);
            IEnumerable<MapNode> selectable;
            switch (traversalMode)
            {
                case MapTraversalMode.Ordinary:
                    selectable = map.Edges
                        .Where(edge => edge.FromNodeId == currentNodeId)
                        .Select(edge => map.GetNode(edge.ToNodeId));
                    break;

                case MapTraversalMode.WingBootsNextLayer:
                    selectable = map.Nodes.Where(node => node.Layer == current.Layer + 1);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(traversalMode));
            }

            return selectable
                .OrderBy(node => node.Layer)
                .ThenBy(node => node.Slot)
                .Select(node => node.Id)
                .ToArray();
        }

        /// <summary>返回候选节点的全部后继节点、边与可达 Boss，供悬停投影使用。</summary>
        public static MapDownstreamRoute GetDownstreamRoute(
            MapDefinition map,
            MapNodeId candidateNodeId)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            map.GetNode(candidateNodeId);
            var reachableNodeIds = new HashSet<MapNodeId> { candidateNodeId };
            var pending = new Queue<MapNodeId>();
            var routeEdges = new List<MapEdge>();
            pending.Enqueue(candidateNodeId);

            while (pending.Count > 0)
            {
                MapNodeId currentNodeId = pending.Dequeue();
                foreach (MapEdge edge in map.Edges.Where(edge =>
                    edge.FromNodeId == currentNodeId))
                {
                    map.GetNode(edge.ToNodeId);
                    routeEdges.Add(edge);
                    if (reachableNodeIds.Add(edge.ToNodeId))
                        pending.Enqueue(edge.ToNodeId);
                }
            }

            MapNodeId[] orderedNodeIds = reachableNodeIds
                .Select(map.GetNode)
                .OrderBy(node => node.Layer)
                .ThenBy(node => node.Slot)
                .Select(node => node.Id)
                .ToArray();
            MapEdge[] orderedEdges = routeEdges
                .OrderBy(edge => map.GetNode(edge.FromNodeId).Layer)
                .ThenBy(edge => map.GetNode(edge.FromNodeId).Slot)
                .ThenBy(edge => map.GetNode(edge.ToNodeId).Slot)
                .ToArray();
            int[] bossIds = orderedNodeIds
                .Select(map.GetNode)
                .Where(node => node.Kind == MapNodeKind.Boss)
                .Select(node => node.ContentId)
                .Distinct()
                .OrderBy(bossId => bossId)
                .ToArray();

            return new MapDownstreamRoute(orderedNodeIds, orderedEdges, bossIds);
        }
    }
}
