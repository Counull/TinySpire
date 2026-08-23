using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinySpire.Run.Map;

public sealed class ActMapGeneratorTests
{
    /// <summary>相同 profile 与地图 seed 必须冻结完全相同的整图定义。</summary>
    [Test]
    public void Generate_WithSameProfileAndSeed_ProducesIdenticalFrozenDefinition()
    {
        ActMapProfile profile = CreateProfile();

        MapDefinition first = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapDefinition second = ActMapGenerator.Generate(profile, mapSeed: 123456u);

        Assert.That(first.ProfileId, Is.EqualTo("test.act.g3.v1"));
        Assert.That(first.GeneratorVersion, Is.EqualTo(ActMapGenerator.CurrentVersion));
        Assert.That(first.MapSeed, Is.EqualTo(123456u));
        Assert.That(first.Nodes, Has.Count.EqualTo(8));
        Assert.That(first.Nodes.Select(DescribeNode), Is.EqualTo(second.Nodes.Select(DescribeNode)));
        Assert.That(first.Edges.Select(DescribeEdge), Is.EqualTo(second.Edges.Select(DescribeEdge)));
        Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
        Assert.That(first.Fingerprint, Has.Length.EqualTo(64));
        Assert.That(ActMapValidator.Validate(first, profile).IsValid, Is.True);

        int[] frozenBossIds = first.Nodes
            .Where(node => node.Kind == MapNodeKind.Boss)
            .Select(node => node.ContentId)
            .ToArray();
        Assert.That(frozenBossIds, Has.Length.EqualTo(3));
        Assert.That(frozenBossIds.Distinct().Count(), Is.EqualTo(2));
        Assert.That(
            frozenBossIds.All(bossId => profile.EnabledBossIds.Contains(bossId)),
            Is.True);
    }

    /// <summary>不同地图 seed 保持固定层/Slot 合同，同时确定性改变内容或连线并产生不同指纹。</summary>
    [Test]
    public void Generate_WithDifferentSeeds_PreservesProfileShapeButChangesFrozenMap()
    {
        ActMapProfile profile = CreateProfile();

        MapDefinition first = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapDefinition second = ActMapGenerator.Generate(profile, mapSeed: 123457u);

        Assert.That(
            first.Nodes.Select(node => $"{node.Id}|{node.Layer}|{node.Slot}|{node.Kind}"),
            Is.EqualTo(second.Nodes.Select(node => $"{node.Id}|{node.Layer}|{node.Slot}|{node.Kind}")));
        Assert.That(first.Fingerprint, Is.Not.EqualTo(second.Fingerprint));
        Assert.That(
            first.Nodes.Select(node => node.ContentId),
            Is.Not.EqualTo(second.Nodes.Select(node => node.ContentId)));
        Assert.That(ActMapValidator.Validate(first, profile).IsValid, Is.True);
        Assert.That(ActMapValidator.Validate(second, profile).IsValid, Is.True);
    }

    /// <summary>Profile 必须复制输入，且调用方不能经公开集合改写冻结配置。</summary>
    [Test]
    public void Profile_CopiesInputsAndPublishesReadOnlyCollections()
    {
        var layers = new[] { 2, 2 };
        var encounters = new[] { 101, 102 };
        var bosses = new[] { 901, 902, 903 };
        var profile = new ActMapProfile(
            "test.act.g3.v1",
            layers,
            encounters,
            bosses,
            bossCandidateCount: 2,
            bossEndpointCount: 3);

        layers[0] = 9;
        encounters[0] = 999;
        bosses[0] = 999;
        var publishedLayers = (IList<int>)profile.NormalLayerSlotCounts;

        Assert.That(profile.NormalLayerSlotCounts, Is.EqualTo(new[] { 2, 2 }));
        Assert.That(profile.EncounterIds, Is.EqualTo(new[] { 101, 102 }));
        Assert.That(profile.EnabledBossIds, Is.EqualTo(new[] { 901, 902, 903 }));
        Assert.That(profile.BossCandidateCount, Is.EqualTo(2));
        Assert.That(profile.BossEndpointCount, Is.EqualTo(3));
        Assert.That(publishedLayers.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => publishedLayers[0] = 9);
    }

    /// <summary>MapDefinition 必须复制构造输入，且不得允许调用方改写公开节点或边集合。</summary>
    [Test]
    public void MapDefinition_CopiesInputsAndPublishesReadOnlyCollections()
    {
        MapNode startNode = new MapNode(
            MapNodeId.FromPosition(layer: 0, slot: 0),
            layer: 0,
            slot: 0,
            MapNodeKind.Start,
            contentId: 0);
        MapNode combatNode = new MapNode(
            MapNodeId.FromPosition(layer: 1, slot: 0),
            layer: 1,
            slot: 0,
            MapNodeKind.Combat,
            contentId: 101);
        MapNode[] sourceNodes = { startNode, combatNode };
        MapEdge[] sourceEdges = { new MapEdge(startNode.Id, combatNode.Id) };
        var map = new MapDefinition(
            "test.immutable.g3.v1",
            ActMapGenerator.CurrentVersion,
            mapSeed: 42u,
            sourceNodes,
            sourceEdges);
        string frozenFingerprint = map.Fingerprint;

        sourceNodes[0] = combatNode;
        sourceEdges[0] = new MapEdge(combatNode.Id, startNode.Id);

        Assert.That(map.Nodes[0], Is.SameAs(startNode));
        Assert.That(map.Edges[0].FromNodeId, Is.EqualTo(startNode.Id));
        Assert.That(map.Fingerprint, Is.EqualTo(frozenFingerprint));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<MapNode>)map.Nodes)[0] = combatNode);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<MapEdge>)map.Edges)[0] = sourceEdges[0]);
    }

    /// <summary>创建覆盖多遭遇、多个 Boss 候选与重复 Boss 终点的最小 profile。</summary>
    private static ActMapProfile CreateProfile()
    {
        return new ActMapProfile(
            profileId: "test.act.g3.v1",
            normalLayerSlotCounts: new[] { 2, 2 },
            encounterIds: new[] { 101, 102 },
            enabledBossIds: new[] { 901, 902, 903 },
            bossCandidateCount: 2,
            bossEndpointCount: 3);
    }

    /// <summary>把节点公开事实投影为稳定测试文本。</summary>
    private static string DescribeNode(MapNode node)
    {
        return $"{node.Id}|{node.Layer}|{node.Slot}|{node.Kind}|{node.ContentId}";
    }

    /// <summary>把边公开事实投影为稳定测试文本。</summary>
    private static string DescribeEdge(MapEdge edge)
    {
        return $"{edge.FromNodeId}>{edge.ToNodeId}";
    }
}
