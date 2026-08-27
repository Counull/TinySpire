using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using cfg.battle;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.Run.Map;

/// <summary>验证 G5/G6 非战斗节点的一次性 Pending 领域事实。</summary>
public sealed class RunNodeVisitTests
{
    /// <summary>权威工厂必须从 Run、地图节点与配置一次冻结四类初始 payload。</summary>
    [Test]
    public void EntryFactory_ForMixedNodes_FreezesAuthoritativeInitialPayloads()
    {
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.NewRunG6V1,
            mapSeed: 2468u);
        var store = new RunStateStore();
        RunState run = store.CreateNewRun(new RunCreationOptions(
            new RunId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            heroTemplateId: 1001,
            initialHealth: 61,
            maxHealth: 61,
            new RunDeck(new[]
            {
                new RunCard(new RunCardInstanceId(8), templateId: 3101, upgradeLevel: 0),
                new RunCard(new RunCardInstanceId(3), templateId: 3102, upgradeLevel: 2),
            }),
            randomRootSeed: 123456u,
            map));
        var catalog = new EntryCatalogStub();

        PendingRunNodeVisit rest = RunNodeVisitEntryFactory.Create(
            run,
            map.GetNode(new MapNodeId("L02-S00")),
            catalog);
        PendingRunNodeVisit chest = RunNodeVisitEntryFactory.Create(
            run,
            map.GetNode(new MapNodeId("L03-S00")),
            catalog);
        PendingRunNodeVisit shop = RunNodeVisitEntryFactory.Create(
            run,
            map.GetNode(new MapNodeId("L04-S00")),
            catalog);
        PendingRunNodeVisit eventVisit = RunNodeVisitEntryFactory.Create(
            run,
            map.GetNode(new MapNodeId("L05-S00")),
            catalog);

        Assert.That(rest.RestPayload.HealAmount, Is.EqualTo(19));
        Assert.That(
            rest.RestPayload.UpgradeCandidateInstanceIds,
            Is.EqualTo(new[] { new RunCardInstanceId(8) }));
        Assert.That(chest.ChestPayload.PotionTemplateId, Is.EqualTo(9001));
        Assert.That(
            shop.ShopPayload.Entries.Select(entry =>
                $"{entry.EntryId}|{entry.Kind}|{entry.TemplateId}|{entry.Price}|{entry.Purchased}"),
            Is.EqualTo(new[]
            {
                "1|Relic|8001|75|False",
                "2|Potion|9001|25|False",
                "3|Card|3102|50|False",
            }));
        Assert.That(eventVisit.EventPayload.GainGoldAmount, Is.EqualTo(50));
        Assert.That(eventVisit.EventPayload.PaidHealCost, Is.EqualTo(25));
        Assert.That(eventVisit.EventPayload.PaidHealAmount, Is.EqualTo(15));
    }

    /// <summary>Flow/Store 不得再暴露 caller-built Pending 或 generic completion 后门。</summary>
    [Test]
    public void NodeVisitApi_DoesNotExposeCallerBuiltPendingOrGenericCompletion()
    {
        MethodInfo[] methods = typeof(RunFlowService).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Concat(typeof(RunStateStore).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .ToArray();

        Assert.That(methods.Any(method => method.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(PendingRunNodeVisit))), Is.False);
        Assert.That(methods.Any(method =>
            method.Name.IndexOf("CompleteNodeVisit", StringComparison.Ordinal) >= 0 ||
            method.Name.IndexOf("NodeVisitCompletion", StringComparison.Ordinal) >= 0), Is.False);
        Assert.That(
            typeof(RunFlowService).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Count(method => method.Name == nameof(RunFlowService.EnterMapNodeAsync)),
            Is.EqualTo(1));
        Assert.That(
            typeof(PendingRunNodeVisit).GetConstructors(BindingFlags.Instance | BindingFlags.Public),
            Is.Empty);
        Assert.That(
            typeof(PendingRunNodeVisit).GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.ReturnType == typeof(PendingRunNodeVisit)),
            Is.Empty);
        Assert.That(
            typeof(RunRestoreOptions).GetConstructors(BindingFlags.Instance | BindingFlags.Public),
            Is.Empty);
    }

    /// <summary>Event 只暴露闭合双 choice 与三个冻结整数，不提供字符串脚本或通用执行器入口。</summary>
    [Test]
    public void EventApi_ExposesOnlyClosedTypedChoicesAndExplicitScalarPayload()
    {
        Assert.That(
            Enum.GetValues(typeof(RunEventChoiceKind)).Cast<RunEventChoiceKind>(),
            Is.EqualTo(new[]
            {
                RunEventChoiceKind.GainGold,
                RunEventChoiceKind.PaidHeal,
            }));
        PropertyInfo[] payloadProperties = typeof(RunEventNodeVisitPayload).GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.That(payloadProperties.Select(property => property.Name), Is.EquivalentTo(new[]
        {
            nameof(RunEventNodeVisitPayload.GainGoldAmount),
            nameof(RunEventNodeVisitPayload.PaidHealCost),
            nameof(RunEventNodeVisitPayload.PaidHealAmount),
        }));
        Assert.That(payloadProperties.All(property => property.PropertyType == typeof(int)), Is.True);

        MethodInfo settle = typeof(RunFlowService).GetMethod(
            nameof(RunFlowService.SettleEventChoice),
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(settle, Is.Not.Null);
        Assert.That(
            settle.GetParameters().Select(parameter => parameter.ParameterType),
            Is.EqualTo(new[] { typeof(RunNodeVisitId), typeof(RunEventChoiceKind) }));

        MethodInfo[] eventMethods = typeof(RunFlowService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Concat(typeof(RunStateStore).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => method.Name.IndexOf("Event", StringComparison.Ordinal) >= 0)
            .ToArray();
        Assert.That(eventMethods.Any(method => method.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(string) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType))), Is.False);
        Assert.That(eventMethods.Any(method =>
            method.Name.IndexOf("Script", StringComparison.OrdinalIgnoreCase) >= 0 ||
            method.Name.IndexOf("Execute", StringComparison.OrdinalIgnoreCase) >= 0 ||
            method.Name.IndexOf("Generic", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
    }

    /// <summary>相同 Run 与地图节点组成相等且可稳定哈希的访问身份。</summary>
    [Test]
    public void RunNodeVisitId_WithSameRunAndNode_IsValueEqual()
    {
        var runId = new RunId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var nodeId = new MapNodeId("L03-S01");

        var first = new RunNodeVisitId(runId, nodeId);
        var second = new RunNodeVisitId(runId, nodeId);

        Assert.That(first.RunId, Is.EqualTo(runId));
        Assert.That(first.NodeId, Is.EqualTo(nodeId));
        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
    }

    /// <summary>休息点工厂冻结治疗量与升级候选顺序，并完整绑定访问身份。</summary>
    [Test]
    public void CreateRest_WithValidPayload_FreezesOrderedCandidates()
    {
        RunNodeVisitId visitId = CreateVisitId("L03-S01");
        var source = new[]
        {
            new RunCardInstanceId(8),
            new RunCardInstanceId(3),
        };

        PendingRunNodeVisit pending = PendingRunNodeVisit.CreateRest(
            visitId,
            contentId: 6101,
            healAmount: 18,
            upgradeCandidateInstanceIds: source);
        source[0] = new RunCardInstanceId(99);

        Assert.That(pending.Id, Is.EqualTo(visitId));
        Assert.That(pending.NodeId, Is.EqualTo(new MapNodeId("L03-S01")));
        Assert.That(pending.ContentId, Is.EqualTo(6101));
        Assert.That(pending.Kind, Is.EqualTo(MapNodeKind.Rest));
        Assert.That(pending.RestPayload.HealAmount, Is.EqualTo(18));
        Assert.That(
            pending.RestPayload.UpgradeCandidateInstanceIds,
            Is.EqualTo(new[] { new RunCardInstanceId(8), new RunCardInstanceId(3) }));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RunCardInstanceId>)pending.RestPayload.UpgradeCandidateInstanceIds)
            .Add(new RunCardInstanceId(10)));
    }

    /// <summary>默认访问身份、坏内容、坏治疗量与伪造或重复候选均被拒绝。</summary>
    [Test]
    public void CreateRest_WithInvalidIdentityContentOrPayload_IsRejected()
    {
        RunNodeVisitId visitId = CreateVisitId("L03-S01");

        Assert.Throws<ArgumentException>(() => new RunNodeVisitId(
            default,
            new MapNodeId("L03-S01")));
        Assert.Throws<ArgumentException>(() => new RunNodeVisitId(
            new RunId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            default));
        Assert.Throws<ArgumentException>(() => PendingRunNodeVisit.CreateRest(
            default,
            contentId: 6101,
            healAmount: 18,
            upgradeCandidateInstanceIds: Array.Empty<RunCardInstanceId>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => PendingRunNodeVisit.CreateRest(
            visitId,
            contentId: 0,
            healAmount: 18,
            upgradeCandidateInstanceIds: Array.Empty<RunCardInstanceId>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => PendingRunNodeVisit.CreateRest(
            visitId,
            contentId: 6101,
            healAmount: 0,
            upgradeCandidateInstanceIds: Array.Empty<RunCardInstanceId>()));
        Assert.Throws<ArgumentNullException>(() => PendingRunNodeVisit.CreateRest(
            visitId,
            contentId: 6101,
            healAmount: 18,
            upgradeCandidateInstanceIds: null));
        Assert.Throws<ArgumentException>(() => PendingRunNodeVisit.CreateRest(
            visitId,
            contentId: 6101,
            healAmount: 18,
            upgradeCandidateInstanceIds: new[] { default(RunCardInstanceId) }));
        Assert.Throws<ArgumentException>(() => PendingRunNodeVisit.CreateRest(
            visitId,
            contentId: 6101,
            healAmount: 18,
            upgradeCandidateInstanceIds: new[]
            {
                new RunCardInstanceId(2),
                new RunCardInstanceId(2),
            }));
    }

    /// <summary>宝箱工厂冻结唯一药水模板，并且不携带休息点 payload。</summary>
    [Test]
    public void CreateChest_WithValidPayload_FreezesPotionReward()
    {
        RunNodeVisitId visitId = CreateVisitId("L04-S00");

        PendingRunNodeVisit pending = PendingRunNodeVisit.CreateChest(
            visitId,
            contentId: 6201,
            potionTemplateId: 5201);

        Assert.That(pending.Id, Is.EqualTo(visitId));
        Assert.That(pending.NodeId, Is.EqualTo(new MapNodeId("L04-S00")));
        Assert.That(pending.ContentId, Is.EqualTo(6201));
        Assert.That(pending.Kind, Is.EqualTo(MapNodeKind.Chest));
        Assert.That(pending.ChestPayload.PotionTemplateId, Is.EqualTo(5201));
        Assert.That(pending.RestPayload, Is.Null);
    }

    /// <summary>商店工厂冻结恰好三项有序库存及每项的购买状态。</summary>
    [Test]
    public void CreateShop_WithValidStock_FreezesThreeOrderedEntries()
    {
        RunNodeVisitId visitId = CreateVisitId("L05-S02");
        var source = new[]
        {
            new RunShopStockEntry(7, RunShopStockKind.Relic, 5101, 90, purchased: false),
            new RunShopStockEntry(2, RunShopStockKind.Potion, 5201, 35, purchased: true),
            new RunShopStockEntry(9, RunShopStockKind.Card, 3105, 55, purchased: false),
        };

        PendingRunNodeVisit pending = PendingRunNodeVisit.CreateShop(
            visitId,
            contentId: 6301,
            entries: source);
        source[0] = new RunShopStockEntry(
            99,
            RunShopStockKind.Card,
            3999,
            999,
            purchased: true);

        Assert.That(pending.Kind, Is.EqualTo(MapNodeKind.Shop));
        Assert.That(pending.ShopPayload.Entries.Count, Is.EqualTo(3));
        Assert.That(pending.ShopPayload.Entries[0].EntryId, Is.EqualTo(7));
        Assert.That(pending.ShopPayload.Entries[0].Kind, Is.EqualTo(RunShopStockKind.Relic));
        Assert.That(pending.ShopPayload.Entries[0].TemplateId, Is.EqualTo(5101));
        Assert.That(pending.ShopPayload.Entries[0].Price, Is.EqualTo(90));
        Assert.That(pending.ShopPayload.Entries[0].Purchased, Is.False);
        Assert.That(pending.ShopPayload.Entries[1].EntryId, Is.EqualTo(2));
        Assert.That(pending.ShopPayload.Entries[1].Purchased, Is.True);
        Assert.That(pending.ShopPayload.Entries[2].EntryId, Is.EqualTo(9));
        Assert.That(pending.RestPayload, Is.Null);
        Assert.That(pending.ChestPayload, Is.Null);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RunShopStockEntry>)pending.ShopPayload.Entries).Add(
                new RunShopStockEntry(
                    10,
                    RunShopStockKind.Card,
                    3106,
                    60,
                    purchased: false)));
    }

    /// <summary>商店拒绝坏库存字段、非三项列表、空项和重复稳定身份。</summary>
    [Test]
    public void CreateShop_WithBrokenEntryOrStockList_IsRejected()
    {
        RunNodeVisitId visitId = CreateVisitId("L05-S02");
        var first = new RunShopStockEntry(
            1,
            RunShopStockKind.Relic,
            5101,
            90,
            purchased: false);
        var second = new RunShopStockEntry(
            2,
            RunShopStockKind.Potion,
            5201,
            35,
            purchased: false);
        var third = new RunShopStockEntry(
            3,
            RunShopStockKind.Card,
            3105,
            55,
            purchased: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RunShopStockEntry(
            0,
            RunShopStockKind.Relic,
            5101,
            90,
            purchased: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunShopStockEntry(
            1,
            (RunShopStockKind)99,
            5101,
            90,
            purchased: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunShopStockEntry(
            1,
            RunShopStockKind.Relic,
            0,
            90,
            purchased: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunShopStockEntry(
            1,
            RunShopStockKind.Relic,
            5101,
            0,
            purchased: false));
        Assert.Throws<ArgumentNullException>(() => PendingRunNodeVisit.CreateShop(
            visitId,
            contentId: 6301,
            entries: null));
        Assert.Throws<ArgumentException>(() => PendingRunNodeVisit.CreateShop(
            visitId,
            contentId: 6301,
            entries: new[] { first, second }));
        Assert.Throws<ArgumentException>(() => PendingRunNodeVisit.CreateShop(
            visitId,
            contentId: 6301,
            entries: new[] { first, second, third, first }));
        Assert.Throws<ArgumentException>(() => PendingRunNodeVisit.CreateShop(
            visitId,
            contentId: 6301,
            entries: new[] { first, null, third }));
        Assert.Throws<ArgumentException>(() => PendingRunNodeVisit.CreateShop(
            visitId,
            contentId: 6301,
            entries: new[]
            {
                first,
                new RunShopStockEntry(
                    1,
                    RunShopStockKind.Card,
                    3106,
                    60,
                    purchased: false),
                third,
            }));
    }

    /// <summary>事件工厂明确冻结获得金币与付费治疗两个结果，不引入通用效果结构。</summary>
    [Test]
    public void CreateEvent_WithValidOutcomes_FreezesExplicitPayload()
    {
        RunNodeVisitId visitId = CreateVisitId("L06-S01");

        PendingRunNodeVisit pending = PendingRunNodeVisit.CreateEvent(
            visitId,
            contentId: 6401,
            gainGoldAmount: 45,
            paidHealCost: 30,
            paidHealAmount: 22);

        Assert.That(pending.Kind, Is.EqualTo(MapNodeKind.Event));
        Assert.That(pending.EventPayload.GainGoldAmount, Is.EqualTo(45));
        Assert.That(pending.EventPayload.PaidHealCost, Is.EqualTo(30));
        Assert.That(pending.EventPayload.PaidHealAmount, Is.EqualTo(22));
        Assert.That(pending.RestPayload, Is.Null);
        Assert.That(pending.ChestPayload, Is.Null);
        Assert.That(pending.ShopPayload, Is.Null);
    }

    /// <summary>宝箱模板与事件的三个数值都必须是正数。</summary>
    [Test]
    public void ScalarPayloads_WithNonPositiveValues_AreRejected()
    {
        RunNodeVisitId visitId = CreateVisitId("L06-S01");

        Assert.Throws<ArgumentOutOfRangeException>(() => PendingRunNodeVisit.CreateChest(
            visitId,
            contentId: 6201,
            potionTemplateId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PendingRunNodeVisit.CreateEvent(
            visitId,
            contentId: 6401,
            gainGoldAmount: 0,
            paidHealCost: 30,
            paidHealAmount: 22));
        Assert.Throws<ArgumentOutOfRangeException>(() => PendingRunNodeVisit.CreateEvent(
            visitId,
            contentId: 6401,
            gainGoldAmount: 45,
            paidHealCost: -1,
            paidHealAmount: 22));
        Assert.Throws<ArgumentOutOfRangeException>(() => PendingRunNodeVisit.CreateEvent(
            visitId,
            contentId: 6401,
            gainGoldAmount: 45,
            paidHealCost: 30,
            paidHealAmount: 0));
    }

    /// <summary>envelope 构造拒绝不支持的节点种类、空身份内容以及错配或多重 payload。</summary>
    [Test]
    public void Constructor_WithUnsupportedKindOrMismatchedPayload_IsRejected()
    {
        RunNodeVisitId visitId = CreateVisitId("L06-S01");
        var rest = new RunRestNodeVisitPayload(
            healAmount: 18,
            upgradeCandidateInstanceIds: Array.Empty<RunCardInstanceId>());
        var chest = new RunChestNodeVisitPayload(potionTemplateId: 5201);

        Assert.Throws<ArgumentException>(() => new PendingRunNodeVisit(
            default,
            contentId: 6101,
            MapNodeKind.Rest,
            rest,
            chestPayload: null,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 0,
            MapNodeKind.Rest,
            rest,
            chestPayload: null,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 6101,
            MapNodeKind.Start,
            rest,
            chestPayload: null,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 6101,
            MapNodeKind.Combat,
            rest,
            chestPayload: null,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 6101,
            MapNodeKind.Boss,
            rest,
            chestPayload: null,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 6101,
            (MapNodeKind)99,
            rest,
            chestPayload: null,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 6101,
            MapNodeKind.Rest,
            restPayload: null,
            chest,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 6101,
            MapNodeKind.Rest,
            rest,
            chest,
            shopPayload: null,
            eventPayload: null));
        Assert.Throws<ArgumentException>(() => new PendingRunNodeVisit(
            visitId,
            contentId: 6201,
            MapNodeKind.Chest,
            restPayload: null,
            chestPayload: null,
            shopPayload: null,
            eventPayload: null));
    }

    /// <summary>创建测试用的固定 Run 与节点组合身份。</summary>
    private static RunNodeVisitId CreateVisitId(string nodeId)
    {
        return new RunNodeVisitId(
            new RunId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new MapNodeId(nodeId));
    }

    /// <summary>为权威进入工厂提供固定的升级、物品与 Hero 奖励池目录。</summary>
    private sealed class EntryCatalogStub : IRunNodeVisitEntryCatalog
    {
        /// <summary>仅允许第一张测试卡从零级升级到一级。</summary>
        public bool IsCardUpgradeLevelValid(int templateId, int upgradeLevel)
        {
            return templateId == 3101 && upgradeLevel == 1;
        }

        /// <summary>测试样本只登记唯一遗物模板。</summary>
        public bool RelicExists(int templateId)
        {
            return templateId == 8001;
        }

        /// <summary>测试样本只登记唯一药水模板。</summary>
        public bool PotionExists(int templateId)
        {
            return templateId == 9001;
        }

        /// <summary>按固定顺序返回三张合法 Hero 商店候选。</summary>
        public HeroCardRewardPool CreateHeroCardRewardPool(int heroTemplateId)
        {
            return new HeroCardRewardPool(
                heroTemplateId,
                new CardRewardRarityWeights(60, 37, 3),
                new[]
                {
                    new CardRewardCandidate(3101, CardRarity.Common),
                    new CardRewardCandidate(3102, CardRarity.Uncommon),
                    new CardRewardCandidate(3103, CardRarity.Rare),
                });
        }
    }
}
