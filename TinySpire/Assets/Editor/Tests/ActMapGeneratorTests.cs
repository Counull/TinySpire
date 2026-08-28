using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class ActMapGeneratorTests
{
    /// <summary>G3 v1 原始三组与额外历史指纹及一份完整节点边 golden 必须逐字节保持不变。</summary>
    [Test]
    public void Generate_LegacyG3V1_PreservesHistoricalFingerprintsAndGoldenGraph()
    {
        ActMapProfile profile = TinySpireActMapProfiles.GetById("tinyspire.act1.g3.v1");

        Assert.That(profile, Is.Not.Null);
        Assert.That(
            ActMapGenerator.Generate(profile, mapSeed: 123456u).Fingerprint,
            Is.EqualTo("bbdecb909ced3e6f49de02f49ebeec79c395733f5f8bf0c6820d53f8039eb6af"));
        Assert.That(
            ActMapGenerator.Generate(profile, mapSeed: 314159265u).Fingerprint,
            Is.EqualTo("fcb0e1b95636e416183fa68e5049a03e4cb268178008ca7153cafcb26ffcb2b3"));
        Assert.That(
            ActMapGenerator.Generate(profile, mapSeed: 42424242u).Fingerprint,
            Is.EqualTo("0b9834fadc2e41d66b33deaac6107fd1e18e7074b54b7c048eb02392b5a283eb"));
        Assert.That(
            ActMapGenerator.Generate(profile, mapSeed: 1u).Fingerprint,
            Is.EqualTo("cc993cc9fcbf25a6a82efaee024afaeb8695f176a03042c19c6fb7f30ce9b7fe"));
        Assert.That(
            ActMapGenerator.Generate(profile, mapSeed: 424242u).Fingerprint,
            Is.EqualTo("bc6a55a66d86e845dcf6d659ac472d89bb4685e80baba2040da1e4a0ca609704"));

        MapDefinition golden = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        Assert.That(golden.GeneratorVersion, Is.EqualTo(1));
        Assert.That(golden.Nodes.Select(DescribeNode), Is.EqualTo(new[]
        {
            "L00-S00|0|0|Start|0",
            "L01-S00|1|0|Combat|5001",
            "L01-S01|1|1|Combat|5001",
            "L02-S00|2|0|Combat|5001",
            "L02-S01|2|1|Combat|5001",
            "L03-S00|3|0|Boss|9001",
            "L03-S01|3|1|Boss|9002",
            "L03-S02|3|2|Boss|9001",
        }));
        Assert.That(golden.Edges.Select(DescribeEdge), Is.EqualTo(new[]
        {
            "L00-S00>L01-S00",
            "L00-S00>L01-S01",
            "L01-S00>L02-S00",
            "L01-S00>L02-S01",
            "L01-S01>L02-S00",
            "L02-S00>L03-S00",
            "L02-S00>L03-S01",
            "L02-S01>L03-S02",
        }));
    }

    /// <summary>生产 G6 profile v1 必须以生成器 v2 生成经过四类非战斗节点的固定单路线。</summary>
    [Test]
    public void Generate_NewRunG6V1WithGeneratorV2_ProducesMixedPlayableRoute()
    {
        ActMapProfile profile = TinySpireActMapProfiles.GetById("tinyspire.act1.g6.v1");

        Assert.That(profile, Is.Not.Null, "G6-A must register the production mixed profile.");
        MapDefinition map = ActMapGenerator.Generate(profile, mapSeed: 123456u);

        Assert.That(map.ProfileId, Is.EqualTo("tinyspire.act1.g6.v1"));
        Assert.That(map.GeneratorVersion, Is.EqualTo(2));
        Assert.That(map.Nodes.Select(DescribeNode), Is.EqualTo(new[]
        {
            "L00-S00|0|0|Start|0",
            "L01-S00|1|0|Combat|5001",
            "L02-S00|2|0|Rest|7101",
            "L03-S00|3|0|Chest|7201",
            "L04-S00|4|0|Shop|7301",
            "L05-S00|5|0|Event|7401",
            "L06-S00|6|0|Combat|5001",
            "L07-S00|7|0|Boss|9001",
            "L07-S01|7|1|Boss|9002",
                "L07-S02|7|2|Boss|9002",
        }));
        Assert.That(map.Edges.Select(DescribeEdge), Is.EqualTo(new[]
        {
            "L00-S00>L01-S00",
            "L01-S00>L02-S00",
            "L02-S00>L03-S00",
            "L03-S00>L04-S00",
            "L04-S00>L05-S00",
            "L05-S00>L06-S00",
            "L06-S00>L07-S00",
            "L06-S00>L07-S01",
            "L06-S00>L07-S02",
        }));
        Assert.That(ActMapValidator.Validate(map, profile).IsValid, Is.True);
    }

    /// <summary>G7 生产 Act 必须复用 v2 地图模型，并冻结普通、精英与真实 Boss 内容引用。</summary>
    [Test]
    public void Generate_NewRunG7V1_ProducesEliteRouteAndBossEncounterManifest()
    {
        ActContentManifest manifest = TinySpireActContentCatalog.NewRunG7V1;

        Assert.That(manifest.Profile.ProfileId, Is.EqualTo("tinyspire.act1.g7.v1"));
        Assert.That(manifest.Profile.GeneratorVersion, Is.EqualTo(ActMapGenerator.NewRunG6Version));
        Assert.That(manifest.OrdinaryEncounterIds, Is.EqualTo(new[] { 5001 }));
        Assert.That(manifest.EliteEncounterIds, Is.EqualTo(new[] { 5101 }));
        Assert.That(
            manifest.NonCombatContents.Select(content => (content.Kind, content.ContentId)),
            Is.EqualTo(new[]
            {
                (MapNodeKind.Rest, 7101),
                (MapNodeKind.Chest, 7201),
                (MapNodeKind.Shop, 7301),
                (MapNodeKind.Event, 7401),
            }));
        ActNonCombatContentReference chest = manifest.NonCombatContents
            .Single(content => content.Kind == MapNodeKind.Chest);
        ActNonCombatContentReference shop = manifest.NonCombatContents
            .Single(content => content.Kind == MapNodeKind.Shop);
        Assert.That(chest.PotionTemplateIds, Is.EqualTo(new[] { 9001 }));
        Assert.That(shop.RelicTemplateIds, Is.EqualTo(new[] { 8001 }));
        Assert.That(shop.PotionTemplateIds, Is.EqualTo(new[] { 9001 }));
        Assert.That(shop.UsesHeroCardRewardPool, Is.True);
        Assert.That(manifest.CompletionRule, Is.EqualTo(ActCompletionRule.BossVictory));
        Assert.That(manifest.GetBossEncounterId(9001), Is.EqualTo(5201));
        Assert.That(manifest.GetBossEncounterId(9002), Is.EqualTo(5201));
        Assert.That(manifest.GetBossEncounterId(9003), Is.EqualTo(5201));
        Assert.That(
            TinySpireActContentCatalog.GetByProfileId(manifest.Profile.ProfileId),
            Is.SameAs(manifest));

        MapDefinition map = ActMapGenerator.Generate(manifest.Profile, mapSeed: 24680u);
        MapNodeKind[] playableKinds = map.Nodes
            .Where(node => node.Layer > 0 && node.Layer < manifest.Profile.BossLayer)
            .OrderBy(node => node.Layer)
            .Select(node => node.Kind)
            .ToArray();

        Assert.That(playableKinds, Is.EqualTo(new[]
        {
            MapNodeKind.Combat,
            MapNodeKind.Rest,
            MapNodeKind.Chest,
            MapNodeKind.Shop,
            MapNodeKind.Event,
            MapNodeKind.Combat,
            MapNodeKind.Elite,
        }));
        Assert.That(
            map.Nodes.Single(node => node.Kind == MapNodeKind.Elite).ContentId,
            Is.EqualTo(5101));
        Assert.That(
            map.Nodes
                .Where(node => node.Kind == MapNodeKind.Boss)
                .All(node => manifest.BossEncounterIds.ContainsKey(node.ContentId)),
            Is.True);

        MapValidationResult validation = ActMapValidator.Validate(map, manifest.Profile);
        Assert.That(validation.IsValid, Is.True, validation.Errors.FirstOrDefault()?.Message);
    }

    /// <summary>新增 Shop/Event 域不得改变既有 Map/Reward 派生结果且必须彼此隔离。</summary>
    [Test]
    public void RandomDomains_ShopAndEvent_AreStableAndIsolatedFromExistingDomains()
    {
        MethodInfo shopMethod = typeof(RunRandomDomains).GetMethod(
            "DeriveShopSeed",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo eventMethod = typeof(RunRandomDomains).GetMethod(
            "DeriveEventSeed",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(shopMethod, Is.Not.Null);
        Assert.That(eventMethod, Is.Not.Null);
        Assert.That(RunRandomDomains.DeriveMapSeed(123456u), Is.EqualTo(3967031089u));
        Assert.That(RunRandomDomains.DeriveRewardSeed(123456u, 2), Is.EqualTo(4036868525u));

        object[] arguments = { 123456u, new MapNodeId("L04-S00") };
        uint shopSeed = (uint)shopMethod.Invoke(null, arguments);
        uint eventSeed = (uint)eventMethod.Invoke(null, arguments);
        Assert.That(shopSeed, Is.EqualTo(1145244530u));
        Assert.That(eventSeed, Is.EqualTo(2377970197u));
        Assert.That(shopSeed, Is.Not.Zero);
        Assert.That(eventSeed, Is.Not.Zero);
        Assert.That(shopSeed, Is.Not.EqualTo(eventSeed));
        Assert.That(shopSeed, Is.Not.EqualTo(RunRandomDomains.DeriveMapSeed(123456u)));
        Assert.That(eventSeed, Is.Not.EqualTo(RunRandomDomains.DeriveRewardSeed(123456u, 2)));
    }

    /// <summary>相同 profile 与地图 seed 必须冻结完全相同的整图定义。</summary>
    [Test]
    public void Generate_WithSameProfileAndSeed_ProducesIdenticalFrozenDefinition()
    {
        ActMapProfile profile = CreateProfile();

        MapDefinition first = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapDefinition second = ActMapGenerator.Generate(profile, mapSeed: 123456u);

        Assert.That(first.ProfileId, Is.EqualTo("test.act.g3.v1"));
        Assert.That(first.GeneratorVersion, Is.EqualTo(profile.GeneratorVersion));
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
            ActMapGenerator.LegacyG3Version,
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
