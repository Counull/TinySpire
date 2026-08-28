using System.Linq;
using NUnit.Framework;
using TinySpire.Run.Map;

public sealed class ActMapValidatorTests
{
    /// <summary>普通节点不再能由 Start 经普通边抵达时必须被 validator 明确拒绝。</summary>
    [Test]
    public void Validate_WhenCombatIsUnreachableFromStart_ReportsTypedFailure()
    {
        ActMapProfile profile = CreateProfile();
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNodeId unreachableNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        var broken = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes,
            generated.Edges
                .Where(edge => edge.ToNodeId != unreachableNodeId)
                .ToArray());

        MapValidationResult result = ActMapValidator.Validate(broken, profile);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.CombatUnreachableFromStart));
    }

    /// <summary>普通节点失去全部后半程时必须被 validator 明确拒绝。</summary>
    [Test]
    public void Validate_WhenCombatCannotReachBoss_ReportsTypedFailure()
    {
        ActMapProfile profile = CreateProfile();
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNodeId strandedNodeId = MapNodeId.FromPosition(layer: 2, slot: 0);
        var broken = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes,
            generated.Edges
                .Where(edge => edge.FromNodeId != strandedNodeId)
                .ToArray());

        MapValidationResult result = ActMapValidator.Validate(broken, profile);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.CombatCannotReachBoss));
    }

    /// <summary>本局冻结的任一 Boss 终点没有普通入口时必须被 validator 明确拒绝。</summary>
    [Test]
    public void Validate_WhenBossEndpointIsUnreachable_ReportsTypedFailure()
    {
        ActMapProfile profile = CreateProfile();
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNodeId unreachableBossId = MapNodeId.FromPosition(profile.BossLayer, slot: 0);
        var broken = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes,
            generated.Edges
                .Where(edge => edge.ToNodeId != unreachableBossId)
                .ToArray());

        MapValidationResult result = ActMapValidator.Validate(broken, profile);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.BossUnreachableFromStart));
    }

    /// <summary>第零层除了唯一 Start 之外出现任何节点时必须被结构校验拒绝。</summary>
    [Test]
    public void Validate_WhenLayerZeroContainsExtraNode_ReportsTypedFailure()
    {
        ActMapProfile profile = CreateProfile();
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        var nodes = generated.Nodes.Concat(new[]
        {
            new MapNode(
                new MapNodeId("L00-S01"),
                layer: 0,
                slot: 1,
                MapNodeKind.Combat,
                contentId: 101),
        }).ToArray();
        var broken = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            nodes,
            generated.Edges);

        MapValidationResult result = ActMapValidator.Validate(broken, profile);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.InvalidStartLayerShape));
    }

    /// <summary>recipe 元数据或冻结节点身份漂移时必须给出稳定的类型化诊断。</summary>
    [Test]
    public void Validate_WhenRecipeAndNodeIdentityDrift_ReportTypedFailures()
    {
        ActMapProfile profile = CreateProfile();
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNodeId targetNodeId = MapNodeId.FromPosition(layer: 1, slot: 0);
        MapNode[] nodes = generated.Nodes
            .Select(node => node.Id == targetNodeId
                ? new MapNode(
                    new MapNodeId("drifted-node"),
                    node.Layer,
                    node.Slot,
                    node.Kind,
                    contentId: 999)
                : node)
            .ToArray();
        var broken = new MapDefinition(
            profileId: "drifted.profile",
            generatorVersion: profile.GeneratorVersion + 1,
            generated.MapSeed,
            nodes,
            generated.Edges);

        MapValidationResult result = ActMapValidator.Validate(broken, profile);
        MapValidationErrorCode[] codes = result.Errors.Select(error => error.Code).ToArray();

        Assert.That(result.IsValid, Is.False);
        Assert.That(codes, Does.Contain(MapValidationErrorCode.ProfileMismatch));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.GeneratorVersionMismatch));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.UnstableNodeId));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.ContentReferenceViolation));
    }

    /// <summary>重复的稳定身份与固定位置必须分别被 validator 拒绝。</summary>
    [Test]
    public void Validate_WhenNodeIdAndPositionAreDuplicated_ReportTypedFailures()
    {
        ActMapProfile profile = CreateProfile();
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNode duplicatedNode = generated.Nodes.Single(node =>
            node.Id == MapNodeId.FromPosition(layer: 1, slot: 0));
        var broken = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes.Concat(new[] { duplicatedNode }).ToArray(),
            generated.Edges);

        MapValidationResult result = ActMapValidator.Validate(broken, profile);
        MapValidationErrorCode[] codes = result.Errors.Select(error => error.Code).ToArray();

        Assert.That(result.IsValid, Is.False);
        Assert.That(codes, Does.Contain(MapValidationErrorCode.DuplicateNodeId));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.DuplicateNodePosition));
    }

    /// <summary>重复边、缺失端点、跨层跳边与 Boss 出边都必须由纯结构规则拒绝。</summary>
    [Test]
    public void Validate_WhenEdgesBreakFrozenDag_ReportTypedFailures()
    {
        ActMapProfile profile = CreateProfile();
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNodeId startNodeId = MapNodeId.FromPosition(layer: 0, slot: 0);
        MapNodeId secondLayerNodeId = MapNodeId.FromPosition(layer: 2, slot: 0);
        MapNodeId bossNodeId = MapNodeId.FromPosition(profile.BossLayer, slot: 0);
        MapNodeId afterBossNodeId = MapNodeId.FromPosition(profile.BossLayer + 1, slot: 0);
        var afterBossNode = new MapNode(
            afterBossNodeId,
            profile.BossLayer + 1,
            slot: 0,
            MapNodeKind.Combat,
            contentId: 101);
        MapEdge firstEdge = generated.Edges[0];
        MapEdge[] brokenEdges = generated.Edges.Concat(new[]
        {
            firstEdge,
            new MapEdge(startNodeId, new MapNodeId("missing-node")),
            new MapEdge(startNodeId, secondLayerNodeId),
            new MapEdge(bossNodeId, afterBossNodeId),
        }).ToArray();
        var broken = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes.Concat(new[] { afterBossNode }).ToArray(),
            brokenEdges);

        MapValidationResult result = ActMapValidator.Validate(broken, profile);
        MapValidationErrorCode[] codes = result.Errors.Select(error => error.Code).ToArray();

        Assert.That(result.IsValid, Is.False);
        Assert.That(codes, Does.Contain(MapValidationErrorCode.DuplicateEdge));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.MissingEdgeEndpoint));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.NonAdjacentEdge));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.BossHasOutgoingEdge));
    }

    /// <summary>G6 mixed 的任一 playable 节点既要从 Start 可达，也要能够继续抵达 BossGate。</summary>
    [Test]
    public void Validate_G6MixedWhenRestBreaksReachability_ReportsPlayableFailures()
    {
        ActMapProfile profile = TinySpireActMapProfiles.NewRunG6V1;
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNodeId restNodeId = MapNodeId.FromPosition(layer: 2, slot: 0);
        var unreachable = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes,
            generated.Edges.Where(edge => edge.ToNodeId != restNodeId).ToArray());
        var stranded = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes,
            generated.Edges.Where(edge => edge.FromNodeId != restNodeId).ToArray());

        MapValidationResult unreachableResult = ActMapValidator.Validate(unreachable, profile);
        MapValidationResult strandedResult = ActMapValidator.Validate(stranded, profile);

        Assert.That(unreachableResult.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.PlayableNodeUnreachableFromStart));
        Assert.That(strandedResult.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.PlayableNodeCannotReachBoss));
    }

    /// <summary>G6 mixed 每层的种类、程序化内容 anchor 与 profile 自有生成器版本都必须精确匹配。</summary>
    [Test]
    public void Validate_G6MixedWhenLayerOrVersionDrifts_ReportsTypedFailures()
    {
        ActMapProfile profile = TinySpireActMapProfiles.NewRunG6V1;
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 123456u);
        MapNodeId chestNodeId = MapNodeId.FromPosition(layer: 3, slot: 0);
        MapNode[] nodes = generated.Nodes
            .Select(node => node.Id == chestNodeId
                ? new MapNode(node.Id, node.Layer, node.Slot, node.Kind, contentId: 7299)
                : node)
            .ToArray();
        var broken = new MapDefinition(
            generated.ProfileId,
            ActMapGenerator.LegacyG3Version,
            generated.MapSeed,
            nodes,
            generated.Edges);

        MapValidationResult result = ActMapValidator.Validate(broken, profile);
        MapValidationErrorCode[] codes = result.Errors.Select(error => error.Code).ToArray();

        Assert.That(codes, Does.Contain(MapValidationErrorCode.GeneratorVersionMismatch));
        Assert.That(codes, Does.Contain(MapValidationErrorCode.ContentReferenceViolation));
    }

    /// <summary>G7 精英节点必须同时从 Start 可达并可继续抵达真实 Boss 层。</summary>
    [Test]
    public void Validate_G7EliteWhenRouteBreaks_ReportsPlayableFailures()
    {
        ActMapProfile profile = TinySpireActMapProfiles.NewRunG7V1;
        MapDefinition generated = ActMapGenerator.Generate(profile, mapSeed: 24680u);
        MapNodeId eliteNodeId = MapNodeId.FromPosition(layer: 7, slot: 0);
        var unreachable = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes,
            generated.Edges.Where(edge => edge.ToNodeId != eliteNodeId).ToArray());
        var stranded = new MapDefinition(
            generated.ProfileId,
            generated.GeneratorVersion,
            generated.MapSeed,
            generated.Nodes,
            generated.Edges.Where(edge => edge.FromNodeId != eliteNodeId).ToArray());

        MapValidationResult unreachableResult = ActMapValidator.Validate(unreachable, profile);
        MapValidationResult strandedResult = ActMapValidator.Validate(stranded, profile);

        Assert.That(unreachableResult.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.PlayableNodeUnreachableFromStart));
        Assert.That(strandedResult.Errors.Select(error => error.Code),
            Does.Contain(MapValidationErrorCode.PlayableNodeCannotReachBoss));
    }

    /// <summary>创建覆盖两个普通层与重复 Boss 终点的固定校验 profile。</summary>
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
}
