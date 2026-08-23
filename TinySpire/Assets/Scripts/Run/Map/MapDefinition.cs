using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TinySpire.Run.Map
{
    /// <summary>节点在整张 Act 地图中的稳定身份。</summary>
    public readonly struct MapNodeId : IEquatable<MapNodeId>
    {
        /// <summary>可序列化且可读的稳定文本值。</summary>
        public string Value { get; }

        /// <summary>从非空稳定文本建立节点身份。</summary>
        public MapNodeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Node id cannot be empty.", nameof(value));

            Value = value;
        }

        /// <summary>从冻结的层与 Slot 位置建立稳定节点身份。</summary>
        public static MapNodeId FromPosition(int layer, int slot)
        {
            if (layer < 0)
                throw new ArgumentOutOfRangeException(nameof(layer));
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));

            return new MapNodeId($"L{layer:D2}-S{slot:D2}");
        }

        /// <summary>比较两个节点身份是否相同。</summary>
        public bool Equals(MapNodeId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>比较此节点身份与另一个对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is MapNodeId other && Equals(other);
        }

        /// <summary>返回基于稳定文本的哈希值。</summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>返回可读的稳定节点身份。</summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>判断两个节点身份是否相同。</summary>
        public static bool operator ==(MapNodeId left, MapNodeId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个节点身份是否不同。</summary>
        public static bool operator !=(MapNodeId left, MapNodeId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>地图节点冻结的玩法种类。</summary>
    public enum MapNodeKind
    {
        Start,
        Combat,
        Boss,
    }

    /// <summary>冻结一个节点的位置、种类与开局明牌内容身份。</summary>
    public sealed class MapNode
    {
        /// <summary>节点稳定身份。</summary>
        public MapNodeId Id { get; }

        /// <summary>节点固定层号。</summary>
        public int Layer { get; }

        /// <summary>节点在本层的固定 Slot。</summary>
        public int Slot { get; }

        /// <summary>节点玩法种类。</summary>
        public MapNodeKind Kind { get; }

        /// <summary>Combat 的 EncounterId 或 Boss 的 BossId；Start 固定为零。</summary>
        public int ContentId { get; }

        /// <summary>冻结并局部验证一个地图节点。</summary>
        public MapNode(MapNodeId id, int layer, int slot, MapNodeKind kind, int contentId)
        {
            if (string.IsNullOrEmpty(id.Value))
                throw new ArgumentException("Node id cannot be empty.", nameof(id));
            if (layer < 0)
                throw new ArgumentOutOfRangeException(nameof(layer));
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));
            if (kind == MapNodeKind.Start && contentId != 0)
                throw new ArgumentException("Start content id must be zero.", nameof(contentId));
            if (kind != MapNodeKind.Start && contentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentId));

            Id = id;
            Layer = layer;
            Slot = slot;
            Kind = kind;
            ContentId = contentId;
        }
    }

    /// <summary>冻结一条从前一层节点指向后一层节点的有向边。</summary>
    public sealed class MapEdge
    {
        /// <summary>边的起点身份。</summary>
        public MapNodeId FromNodeId { get; }

        /// <summary>边的终点身份。</summary>
        public MapNodeId ToNodeId { get; }

        /// <summary>冻结一条非自环有向边。</summary>
        public MapEdge(MapNodeId fromNodeId, MapNodeId toNodeId)
        {
            if (string.IsNullOrEmpty(fromNodeId.Value))
                throw new ArgumentException("From node id cannot be empty.", nameof(fromNodeId));
            if (string.IsNullOrEmpty(toNodeId.Value))
                throw new ArgumentException("To node id cannot be empty.", nameof(toNodeId));
            if (fromNodeId == toNodeId)
                throw new ArgumentException("Self edges are not allowed.", nameof(toNodeId));

            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }
    }

    /// <summary>一次生成后冻结、可由种子与配置重建的完整 Act 地图。</summary>
    public sealed class MapDefinition
    {
        private readonly ReadOnlyCollection<MapNode> _nodes;
        private readonly ReadOnlyCollection<MapEdge> _edges;

        /// <summary>用于重建本图的稳定 profile 身份。</summary>
        public string ProfileId { get; }

        /// <summary>用于重建本图的生成器版本。</summary>
        public int GeneratorVersion { get; }

        /// <summary>仅供地图生成器使用的非零确定性 seed。</summary>
        public uint MapSeed { get; }

        /// <summary>整图的不可变节点序列。</summary>
        public IReadOnlyList<MapNode> Nodes => _nodes;

        /// <summary>整图的不可变有向边序列。</summary>
        public IReadOnlyList<MapEdge> Edges => _edges;

        /// <summary>规范化整图事实的 SHA-256 指纹。</summary>
        public string Fingerprint { get; }

        /// <summary>复制整图事实并计算不包含 UI 或派生数据的稳定指纹。</summary>
        public MapDefinition(
            string profileId,
            int generatorVersion,
            uint mapSeed,
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));
            if (generatorVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            if (mapSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(mapSeed));
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (edges == null)
                throw new ArgumentNullException(nameof(edges));

            ProfileId = profileId;
            GeneratorVersion = generatorVersion;
            MapSeed = mapSeed;
            _nodes = Array.AsReadOnly(nodes.ToArray());
            _edges = Array.AsReadOnly(edges.ToArray());
            Fingerprint = ComputeFingerprint();
        }

        /// <summary>按稳定身份查找恰好一个节点，缺失或重复时拒绝继续。</summary>
        public MapNode GetNode(MapNodeId nodeId)
        {
            MapNode match = null;
            foreach (MapNode node in _nodes)
            {
                if (node.Id != nodeId)
                    continue;
                if (match != null)
                    throw new InvalidOperationException($"Duplicate map node id '{nodeId}'.");

                match = node;
            }

            return match ?? throw new KeyNotFoundException($"Map node '{nodeId}' was not found.");
        }

        /// <summary>把基础整图事实规范化后计算稳定 SHA-256 指纹。</summary>
        private string ComputeFingerprint()
        {
            var canonical = new StringBuilder();
            canonical.Append("profile=").Append(ProfileId).Append('\n');
            canonical.Append("generator=").Append(GeneratorVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            canonical.Append("seed=").Append(MapSeed.ToString(CultureInfo.InvariantCulture)).Append('\n');

            foreach (MapNode node in _nodes
                .OrderBy(value => value.Layer)
                .ThenBy(value => value.Slot)
                .ThenBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                canonical.Append("node=")
                    .Append(node.Id.Value).Append(',')
                    .Append(node.Layer.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(node.Slot.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(node.Kind).Append(',')
                    .Append(node.ContentId.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            foreach (MapEdge edge in _edges
                .OrderBy(value => value.FromNodeId.Value, StringComparer.Ordinal)
                .ThenBy(value => value.ToNodeId.Value, StringComparer.Ordinal))
            {
                canonical.Append("edge=")
                    .Append(edge.FromNodeId.Value).Append('>')
                    .Append(edge.ToNodeId.Value).Append('\n');
            }

            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                return string.Concat(hash.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
