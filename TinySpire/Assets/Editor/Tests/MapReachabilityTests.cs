using System.Linq;
using NUnit.Framework;
using TinySpire.Run.Map;

public sealed class MapReachabilityTests
{
    /// <summary>普通移动只返回直接出边，翼靴预留规则返回下一层全部已生成节点。</summary>
    [Test]
    public void GetSelectableNodeIds_UsesTheRequestedPureTraversalRule()
    {
        MapDefinition map = CreateBranchedMap();
        MapNodeId current = MapNodeId.FromPosition(layer: 1, slot: 0);

        var ordinary = MapReachability.GetSelectableNodeIds(
            map,
            current,
            MapTraversalMode.Ordinary);
        var wingBoots = MapReachability.GetSelectableNodeIds(
            map,
            current,
            MapTraversalMode.WingBootsNextLayer);

        Assert.That(
            ordinary.Select(nodeId => nodeId.Value),
            Is.EqualTo(new[] { "L02-S00" }));
        Assert.That(
            wingBoots.Select(nodeId => nodeId.Value),
            Is.EqualTo(new[] { "L02-S00", "L02-S01" }));
    }

    /// <summary>悬停候选节点时返回其完整后半程，并排除另一条会被放弃的路线与 Boss。</summary>
    [Test]
    public void GetDownstreamRoute_ReturnsCompleteTailAndReachableBosses()
    {
        MapDefinition map = CreateBranchedMap();

        MapDownstreamRoute route = MapReachability.GetDownstreamRoute(
            map,
            MapNodeId.FromPosition(layer: 1, slot: 0));

        Assert.That(
            route.NodeIds.Select(nodeId => nodeId.Value),
            Is.EqualTo(new[] { "L01-S00", "L02-S00", "L03-S00" }));
        Assert.That(
            route.Edges.Select(edge => $"{edge.FromNodeId}>{edge.ToNodeId}"),
            Is.EqualTo(new[]
            {
                "L01-S00>L02-S00",
                "L02-S00>L03-S00",
            }));
        Assert.That(route.ReachableBossIds, Is.EqualTo(new[] { 901 }));
    }

    /// <summary>创建两条互不交叉且分别通向不同 Boss 的冻结分支图。</summary>
    private static MapDefinition CreateBranchedMap()
    {
        var nodes = new[]
        {
            CreateNode(0, 0, MapNodeKind.Start, 0),
            CreateNode(1, 0, MapNodeKind.Combat, 101),
            CreateNode(1, 1, MapNodeKind.Combat, 102),
            CreateNode(2, 0, MapNodeKind.Combat, 101),
            CreateNode(2, 1, MapNodeKind.Combat, 102),
            CreateNode(3, 0, MapNodeKind.Boss, 901),
            CreateNode(3, 1, MapNodeKind.Boss, 902),
        };
        var edges = new[]
        {
            CreateEdge(0, 0, 1, 0),
            CreateEdge(0, 0, 1, 1),
            CreateEdge(1, 0, 2, 0),
            CreateEdge(1, 1, 2, 1),
            CreateEdge(2, 0, 3, 0),
            CreateEdge(2, 1, 3, 1),
        };

        return new MapDefinition(
            "test.reachability.v1",
            ActMapGenerator.LegacyG3Version,
            mapSeed: 77u,
            nodes,
            edges);
    }

    /// <summary>从稳定位置与内容创建一个测试节点。</summary>
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

    /// <summary>从两个稳定位置创建一条测试边。</summary>
    private static MapEdge CreateEdge(
        int fromLayer,
        int fromSlot,
        int toLayer,
        int toSlot)
    {
        return new MapEdge(
            MapNodeId.FromPosition(fromLayer, fromSlot),
            MapNodeId.FromPosition(toLayer, toSlot));
    }
}
