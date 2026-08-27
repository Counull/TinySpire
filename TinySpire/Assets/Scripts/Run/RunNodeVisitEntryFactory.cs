using System;
using System.Collections.Generic;
using System.Linq;
using TinySpire.Core;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>非战斗进入工厂读取升级、物品与 Hero 卡池所需的最窄配置目录。</summary>
    internal interface IRunNodeVisitEntryCatalog : IRunCardUpgradeConfigurationCatalog
    {
        /// <summary>判断固定遗物模板是否仍存在。</summary>
        bool RelicExists(int templateId);

        /// <summary>判断固定药水模板是否仍存在。</summary>
        bool PotionExists(int templateId);

        /// <summary>读取指定 Hero 当前经过配置门禁的有序奖励卡池。</summary>
        HeroCardRewardPool CreateHeroCardRewardPool(int heroTemplateId);
    }

    /// <summary>集中保存 G6 v2 程序化地图与初始节点 payload 使用的稳定身份。</summary>
    internal static class RunNodeVisitIdentityCatalog
    {
        internal const int RestContentId = 7101;
        internal const int ChestContentId = 7201;
        internal const int ShopContentId = 7301;
        internal const int EventContentId = 7401;
        internal const int SampleRelicTemplateId = 8001;
        internal const int SamplePotionTemplateId = 9001;

        /// <summary>拒绝种类与程序化内容 anchor 不完全匹配的非战斗节点。</summary>
        internal static void ValidateNonCombatNode(MapNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            int expectedContentId;
            switch (node.Kind)
            {
                case MapNodeKind.Rest:
                    expectedContentId = RestContentId;
                    break;
                case MapNodeKind.Chest:
                    expectedContentId = ChestContentId;
                    break;
                case MapNodeKind.Shop:
                    expectedContentId = ShopContentId;
                    break;
                case MapNodeKind.Event:
                    expectedContentId = EventContentId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(node),
                        "Only explicit non-combat nodes have Run entry anchors.");
            }

            if (node.ContentId != expectedContentId)
            {
                throw new InvalidOperationException(
                    $"{node.Kind} node content {node.ContentId} does not match anchor {expectedContentId}.");
            }
        }
    }

    /// <summary>从当前 Run 与冻结地图节点权威创建不可由 caller 拼装的初始 Pending。</summary>
    internal static class RunNodeVisitEntryFactory
    {
        /// <summary>按明确节点种类冻结一次初始 payload，不执行任何玩法结算。</summary>
        internal static PendingRunNodeVisit Create(
            RunState run,
            MapNode node,
            IRunNodeVisitEntryCatalog catalog)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            MapNode frozenNode = run.MapDefinition.GetNode(node.Id);
            if (frozenNode.Kind != node.Kind || frozenNode.ContentId != node.ContentId)
                throw new InvalidOperationException("Node does not match the Run's frozen map definition.");

            RunNodeVisitIdentityCatalog.ValidateNonCombatNode(node);
            var visitId = new RunNodeVisitId(run.RunId, node.Id);
            switch (node.Kind)
            {
                case MapNodeKind.Rest:
                    return PendingRunNodeVisit.CreateRest(
                        visitId,
                        node.ContentId,
                        CalculateRestHealAmount(run.MaxHealth),
                        CollectUpgradeCandidates(run.RunDeck, catalog));
                case MapNodeKind.Chest:
                    RequirePotion(catalog, RunNodeVisitIdentityCatalog.SamplePotionTemplateId);
                    return PendingRunNodeVisit.CreateChest(
                        visitId,
                        node.ContentId,
                        RunNodeVisitIdentityCatalog.SamplePotionTemplateId);
                case MapNodeKind.Shop:
                    return CreateShop(run, node, visitId, catalog);
                case MapNodeKind.Event:
                    return PendingRunNodeVisit.CreateEvent(
                        visitId,
                        node.ContentId,
                        gainGoldAmount: 50,
                        paidHealCost: 25,
                        paidHealAmount: 15);
                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
        }

        /// <summary>逐字段比较存档 Pending 与当前权威工厂重建结果。</summary>
        internal static bool HasSameFrozenFacts(
            PendingRunNodeVisit saved,
            PendingRunNodeVisit expected)
        {
            if (saved == null || expected == null ||
                saved.Id != expected.Id ||
                saved.NodeId != expected.NodeId ||
                saved.Kind != expected.Kind ||
                saved.ContentId != expected.ContentId)
            {
                return false;
            }

            switch (saved.Kind)
            {
                case MapNodeKind.Rest:
                    return saved.RestPayload.HealAmount == expected.RestPayload.HealAmount &&
                           saved.RestPayload.UpgradeCandidateInstanceIds.SequenceEqual(
                               expected.RestPayload.UpgradeCandidateInstanceIds);
                case MapNodeKind.Chest:
                    return saved.ChestPayload.PotionTemplateId ==
                           expected.ChestPayload.PotionTemplateId;
                case MapNodeKind.Shop:
                    return ShopEntriesMatch(
                        saved.ShopPayload.Entries,
                        expected.ShopPayload.Entries);
                case MapNodeKind.Event:
                    return saved.EventPayload.GainGoldAmount == expected.EventPayload.GainGoldAmount &&
                           saved.EventPayload.PaidHealCost == expected.EventPayload.PaidHealCost &&
                           saved.EventPayload.PaidHealAmount == expected.EventPayload.PaidHealAmount;
                default:
                    return false;
            }
        }

        /// <summary>以整数安全的向上取整计算最大生命百分之三十。</summary>
        private static int CalculateRestHealAmount(int maxHealth)
        {
            long amount = ((long)maxHealth * 3L + 9L) / 10L;
            return checked((int)amount);
        }

        /// <summary>按 RunDeck 原顺序收集下一等级仍由配置允许的实例身份。</summary>
        private static IReadOnlyList<RunCardInstanceId> CollectUpgradeCandidates(
            RunDeck runDeck,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            var candidates = new List<RunCardInstanceId>();
            foreach (RunCard card in runDeck.Cards)
            {
                if (card.UpgradeLevel == int.MaxValue)
                    continue;

                int nextLevel = card.UpgradeLevel + 1;
                if (catalog.IsCardUpgradeLevelValid(card.TemplateId, nextLevel))
                    candidates.Add(card.InstanceId);
            }

            return candidates;
        }

        /// <summary>使用 Shop 专属 seed 从 Hero 卡池冻结一张卡并建立三项初始库存。</summary>
        private static PendingRunNodeVisit CreateShop(
            RunState run,
            MapNode node,
            RunNodeVisitId visitId,
            IRunNodeVisitEntryCatalog catalog)
        {
            RequireRelic(catalog, RunNodeVisitIdentityCatalog.SampleRelicTemplateId);
            RequirePotion(catalog, RunNodeVisitIdentityCatalog.SamplePotionTemplateId);
            HeroCardRewardPool pool = catalog.CreateHeroCardRewardPool(run.HeroTemplateId)
                ?? throw new InvalidOperationException(
                    $"Hero {run.HeroTemplateId} card reward pool does not exist.");
            if (pool.HeroTemplateId != run.HeroTemplateId || pool.Candidates.Count == 0)
                throw new InvalidOperationException("Hero card reward pool does not match the current Run.");

            uint shopSeed = RunRandomDomains.DeriveShopSeed(run.RandomRootSeed, node.Id);
            int selectedIndex = new GameRandom(shopSeed).NextInt(pool.Candidates.Count);
            int cardTemplateId = pool.Candidates[selectedIndex].TemplateId;
            return PendingRunNodeVisit.CreateShop(
                visitId,
                node.ContentId,
                new[]
                {
                    new RunShopStockEntry(
                        entryId: 1,
                        RunShopStockKind.Relic,
                        RunNodeVisitIdentityCatalog.SampleRelicTemplateId,
                        price: 75,
                        purchased: false),
                    new RunShopStockEntry(
                        entryId: 2,
                        RunShopStockKind.Potion,
                        RunNodeVisitIdentityCatalog.SamplePotionTemplateId,
                        price: 25,
                        purchased: false),
                    new RunShopStockEntry(
                        entryId: 3,
                        RunShopStockKind.Card,
                        cardTemplateId,
                        price: 50,
                        purchased: false),
                });
        }

        /// <summary>要求程序化遗物 anchor 仍存在于当前配置。</summary>
        private static void RequireRelic(IRunNodeVisitEntryCatalog catalog, int templateId)
        {
            if (!catalog.RelicExists(templateId))
                throw new InvalidOperationException($"Relic template {templateId} does not exist.");
        }

        /// <summary>要求程序化药水 anchor 仍存在于当前配置。</summary>
        private static void RequirePotion(IRunNodeVisitEntryCatalog catalog, int templateId)
        {
            if (!catalog.PotionExists(templateId))
                throw new InvalidOperationException($"Potion template {templateId} does not exist.");
        }

        /// <summary>严格比较三项静态库存的顺序、身份、种类、模板与价格；购买子集由存档动态事实保留。</summary>
        private static bool ShopEntriesMatch(
            IReadOnlyList<RunShopStockEntry> saved,
            IReadOnlyList<RunShopStockEntry> expected)
        {
            if (saved.Count != expected.Count)
                return false;

            for (int index = 0; index < saved.Count; index++)
            {
                RunShopStockEntry left = saved[index];
                RunShopStockEntry right = expected[index];
                if (left.EntryId != right.EntryId ||
                    left.Kind != right.Kind ||
                    left.TemplateId != right.TemplateId ||
                    left.Price != right.Price)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
