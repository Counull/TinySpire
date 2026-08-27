using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>由 Run 与地图节点共同组成的一次稳定节点访问身份。</summary>
    public readonly struct RunNodeVisitId : IEquatable<RunNodeVisitId>
    {
        /// <summary>节点访问所属的稳定 Run 身份。</summary>
        public RunId RunId { get; }

        /// <summary>本次访问绑定的稳定地图节点身份。</summary>
        public MapNodeId NodeId { get; }

        /// <summary>从非空 Run 与节点身份创建稳定访问身份。</summary>
        public RunNodeVisitId(RunId runId, MapNodeId nodeId)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run id cannot be empty.", nameof(runId));
            if (string.IsNullOrEmpty(nodeId.Value))
                throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));

            RunId = runId;
            NodeId = nodeId;
        }

        /// <summary>比较两个节点访问身份是否相同。</summary>
        public bool Equals(RunNodeVisitId other)
        {
            return RunId == other.RunId && NodeId == other.NodeId;
        }

        /// <summary>比较此访问身份与任意对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is RunNodeVisitId other && Equals(other);
        }

        /// <summary>返回 Run 与节点身份组合后的稳定哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (RunId.GetHashCode() * 397) ^ NodeId.GetHashCode();
            }
        }

        /// <summary>返回便于日志诊断的 Run 与节点组合文本。</summary>
        public override string ToString()
        {
            return $"{RunId}/{NodeId}";
        }

        /// <summary>判断两个节点访问身份是否相同。</summary>
        public static bool operator ==(RunNodeVisitId left, RunNodeVisitId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个节点访问身份是否不同。</summary>
        public static bool operator !=(RunNodeVisitId left, RunNodeVisitId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>休息点冻结的治疗量与有序升级候选事实。</summary>
    public sealed class RunRestNodeVisitPayload
    {
        private readonly ReadOnlyCollection<RunCardInstanceId> _upgradeCandidateInstanceIds;

        /// <summary>选择休息时可恢复的正数生命值。</summary>
        public int HealAmount { get; }

        /// <summary>按稳定展示顺序冻结的可升级卡牌实例身份。</summary>
        public IReadOnlyList<RunCardInstanceId> UpgradeCandidateInstanceIds =>
            _upgradeCandidateInstanceIds;

        /// <summary>防御性复制休息点的治疗值与有序升级候选。</summary>
        public RunRestNodeVisitPayload(
            int healAmount,
            IEnumerable<RunCardInstanceId> upgradeCandidateInstanceIds)
        {
            if (healAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(healAmount));
            if (upgradeCandidateInstanceIds == null)
                throw new ArgumentNullException(nameof(upgradeCandidateInstanceIds));

            RunCardInstanceId[] frozenCandidateIds = upgradeCandidateInstanceIds.ToArray();
            var uniqueCandidateIds = new HashSet<RunCardInstanceId>();
            foreach (RunCardInstanceId candidateId in frozenCandidateIds)
            {
                if (candidateId.Sequence <= 0)
                {
                    throw new ArgumentException(
                        "Rest upgrade candidates cannot contain an empty card instance id.",
                        nameof(upgradeCandidateInstanceIds));
                }
                if (!uniqueCandidateIds.Add(candidateId))
                {
                    throw new ArgumentException(
                        "Rest upgrade candidates cannot contain duplicate card instance ids.",
                        nameof(upgradeCandidateInstanceIds));
                }
            }

            HealAmount = healAmount;
            _upgradeCandidateInstanceIds = Array.AsReadOnly(frozenCandidateIds);
        }
    }

    /// <summary>宝箱节点冻结的单一药水奖励事实。</summary>
    public sealed class RunChestNodeVisitPayload
    {
        /// <summary>认领宝箱后可获得的正整数药水模板身份。</summary>
        public int PotionTemplateId { get; }

        /// <summary>创建一个经过正数约束的宝箱药水奖励。</summary>
        public RunChestNodeVisitPayload(int potionTemplateId)
        {
            if (potionTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(potionTemplateId));

            PotionTemplateId = potionTemplateId;
        }
    }

    /// <summary>商店一项库存明确引用的内容域。</summary>
    public enum RunShopStockKind
    {
        Relic,
        Potion,
        Card,
    }

    /// <summary>商店内一项具有稳定身份、价格与购买状态的库存事实。</summary>
    public sealed class RunShopStockEntry
    {
        /// <summary>本次商店库存内的稳定正整数身份。</summary>
        public int EntryId { get; }

        /// <summary>库存项引用的明确内容域。</summary>
        public RunShopStockKind Kind { get; }

        /// <summary>库存项引用的正整数静态模板身份。</summary>
        public int TemplateId { get; }

        /// <summary>购买库存项所需的正数金币。</summary>
        public int Price { get; }

        /// <summary>该库存项是否已经完成一次购买。</summary>
        public bool Purchased { get; }

        /// <summary>冻结一项商店库存的稳定事实。</summary>
        public RunShopStockEntry(
            int entryId,
            RunShopStockKind kind,
            int templateId,
            int price,
            bool purchased)
        {
            if (entryId <= 0)
                throw new ArgumentOutOfRangeException(nameof(entryId));
            if (kind != RunShopStockKind.Relic &&
                kind != RunShopStockKind.Potion &&
                kind != RunShopStockKind.Card)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (price <= 0)
                throw new ArgumentOutOfRangeException(nameof(price));

            EntryId = entryId;
            Kind = kind;
            TemplateId = templateId;
            Price = price;
            Purchased = purchased;
        }
    }

    /// <summary>商店节点冻结的三项有序库存。</summary>
    public sealed class RunShopNodeVisitPayload
    {
        private readonly ReadOnlyCollection<RunShopStockEntry> _entries;

        /// <summary>按展示与结算顺序冻结的全部库存项。</summary>
        public IReadOnlyList<RunShopStockEntry> Entries => _entries;

        /// <summary>防御性复制商店的有序库存项。</summary>
        public RunShopNodeVisitPayload(IEnumerable<RunShopStockEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            RunShopStockEntry[] frozenEntries = entries.ToArray();
            if (frozenEntries.Length != 3)
                throw new ArgumentException("Shop stock must contain exactly three entries.", nameof(entries));

            var entryIds = new HashSet<int>();
            foreach (RunShopStockEntry entry in frozenEntries)
            {
                if (entry == null)
                    throw new ArgumentException("Shop stock cannot contain null entries.", nameof(entries));
                if (!entryIds.Add(entry.EntryId))
                    throw new ArgumentException("Shop stock cannot contain duplicate entry ids.", nameof(entries));
            }

            _entries = Array.AsReadOnly(frozenEntries);
        }
    }

    /// <summary>事件页可提交给 Store 的两个有限类型化选择。</summary>
    public enum RunEventChoiceKind
    {
        GainGold,
        PaidHeal,
    }

    /// <summary>事件节点冻结的两个明确选择结果。</summary>
    public sealed class RunEventNodeVisitPayload
    {
        /// <summary>免费选择可获得的正数金币。</summary>
        public int GainGoldAmount { get; }

        /// <summary>付费治疗选择需要花费的正数金币。</summary>
        public int PaidHealCost { get; }

        /// <summary>付费治疗选择可恢复的正数生命值。</summary>
        public int PaidHealAmount { get; }

        /// <summary>冻结事件的获得金币与付费治疗结果。</summary>
        public RunEventNodeVisitPayload(
            int gainGoldAmount,
            int paidHealCost,
            int paidHealAmount)
        {
            if (gainGoldAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(gainGoldAmount));
            if (paidHealCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(paidHealCost));
            if (paidHealAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(paidHealAmount));

            GainGoldAmount = gainGoldAmount;
            PaidHealCost = paidHealCost;
            PaidHealAmount = paidHealAmount;
        }
    }

    /// <summary>一次非战斗节点进入后冻结、结算前可持久化的 Pending envelope。</summary>
    public sealed class PendingRunNodeVisit
    {
        /// <summary>由 Run 与地图节点组成的稳定访问身份。</summary>
        public RunNodeVisitId Id { get; }

        /// <summary>本 envelope 绑定的稳定地图节点身份。</summary>
        public MapNodeId NodeId => Id.NodeId;

        /// <summary>节点对应的正整数静态内容身份。</summary>
        public int ContentId { get; }

        /// <summary>节点冻结的具体非战斗玩法种类。</summary>
        public MapNodeKind Kind { get; }

        /// <summary>仅 Rest envelope 持有的明确休息点 payload。</summary>
        public RunRestNodeVisitPayload RestPayload { get; }

        /// <summary>仅 Chest envelope 持有的明确宝箱 payload。</summary>
        public RunChestNodeVisitPayload ChestPayload { get; }

        /// <summary>仅 Shop envelope 持有的明确商店 payload。</summary>
        public RunShopNodeVisitPayload ShopPayload { get; }

        /// <summary>仅 Event envelope 持有的明确事件 payload。</summary>
        public RunEventNodeVisitPayload EventPayload { get; }

        /// <summary>验证身份、种类与唯一匹配 payload 后冻结非战斗节点 envelope。</summary>
        internal PendingRunNodeVisit(
            RunNodeVisitId id,
            int contentId,
            MapNodeKind kind,
            RunRestNodeVisitPayload restPayload,
            RunChestNodeVisitPayload chestPayload,
            RunShopNodeVisitPayload shopPayload,
            RunEventNodeVisitPayload eventPayload)
        {
            ValidateEnvelopeIdentityAndContent(id, contentId);
            ValidatePayloadMatch(
                kind,
                restPayload,
                chestPayload,
                shopPayload,
                eventPayload);

            Id = id;
            ContentId = contentId;
            Kind = kind;
            RestPayload = restPayload;
            ChestPayload = chestPayload;
            ShopPayload = shopPayload;
            EventPayload = eventPayload;
        }

        /// <summary>建立只携带休息点 payload 的稳定 Pending 访问事实。</summary>
        internal static PendingRunNodeVisit CreateRest(
            RunNodeVisitId id,
            int contentId,
            int healAmount,
            IEnumerable<RunCardInstanceId> upgradeCandidateInstanceIds)
        {
            ValidateEnvelopeIdentityAndContent(id, contentId);
            var payload = new RunRestNodeVisitPayload(
                healAmount,
                upgradeCandidateInstanceIds);
            return new PendingRunNodeVisit(
                id,
                contentId,
                MapNodeKind.Rest,
                payload,
                chestPayload: null,
                shopPayload: null,
                eventPayload: null);
        }

        /// <summary>建立只携带宝箱 payload 的稳定 Pending 访问事实。</summary>
        internal static PendingRunNodeVisit CreateChest(
            RunNodeVisitId id,
            int contentId,
            int potionTemplateId)
        {
            ValidateEnvelopeIdentityAndContent(id, contentId);
            var payload = new RunChestNodeVisitPayload(potionTemplateId);
            return new PendingRunNodeVisit(
                id,
                contentId,
                MapNodeKind.Chest,
                restPayload: null,
                chestPayload: payload,
                shopPayload: null,
                eventPayload: null);
        }

        /// <summary>建立只携带商店 payload 的稳定 Pending 访问事实。</summary>
        internal static PendingRunNodeVisit CreateShop(
            RunNodeVisitId id,
            int contentId,
            IEnumerable<RunShopStockEntry> entries)
        {
            ValidateEnvelopeIdentityAndContent(id, contentId);
            var payload = new RunShopNodeVisitPayload(entries);
            return new PendingRunNodeVisit(
                id,
                contentId,
                MapNodeKind.Shop,
                restPayload: null,
                chestPayload: null,
                shopPayload: payload,
                eventPayload: null);
        }

        /// <summary>建立只携带事件 payload 的稳定 Pending 访问事实。</summary>
        internal static PendingRunNodeVisit CreateEvent(
            RunNodeVisitId id,
            int contentId,
            int gainGoldAmount,
            int paidHealCost,
            int paidHealAmount)
        {
            ValidateEnvelopeIdentityAndContent(id, contentId);
            var payload = new RunEventNodeVisitPayload(
                gainGoldAmount,
                paidHealCost,
                paidHealAmount);
            return new PendingRunNodeVisit(
                id,
                contentId,
                MapNodeKind.Event,
                restPayload: null,
                chestPayload: null,
                shopPayload: null,
                eventPayload: payload);
        }

        /// <summary>要求四种明确节点类型恰好携带一个同类型 payload。</summary>
        private static void ValidatePayloadMatch(
            MapNodeKind kind,
            RunRestNodeVisitPayload restPayload,
            RunChestNodeVisitPayload chestPayload,
            RunShopNodeVisitPayload shopPayload,
            RunEventNodeVisitPayload eventPayload)
        {
            switch (kind)
            {
                case MapNodeKind.Rest:
                    if (restPayload != null &&
                        chestPayload == null &&
                        shopPayload == null &&
                        eventPayload == null)
                    {
                        return;
                    }
                    break;
                case MapNodeKind.Chest:
                    if (restPayload == null &&
                        chestPayload != null &&
                        shopPayload == null &&
                        eventPayload == null)
                    {
                        return;
                    }
                    break;
                case MapNodeKind.Shop:
                    if (restPayload == null &&
                        chestPayload == null &&
                        shopPayload != null &&
                        eventPayload == null)
                    {
                        return;
                    }
                    break;
                case MapNodeKind.Event:
                    if (restPayload == null &&
                        chestPayload == null &&
                        shopPayload == null &&
                        eventPayload != null)
                    {
                        return;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Only explicit non-combat node kinds can create a Pending visit.");
            }

            throw new ArgumentException(
                "Pending node visit must carry exactly one payload matching its node kind.");
        }

        /// <summary>拒绝默认访问身份与非正静态内容身份。</summary>
        private static void ValidateEnvelopeIdentityAndContent(
            RunNodeVisitId id,
            int contentId)
        {
            if (id.RunId.Value == Guid.Empty || string.IsNullOrEmpty(id.NodeId.Value))
                throw new ArgumentException("Run node visit id cannot be empty.", nameof(id));
            if (contentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentId));
        }
    }
}
