using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TinySpire.Run.Map;

namespace TinySpire.Run.History
{
    /// <summary>终局路径中的一个不可变节点快照。</summary>
    public sealed class RunSummaryPathNode : IEquatable<RunSummaryPathNode>
    {
        /// <summary>节点的稳定文本身份。</summary>
        public string NodeId { get; }

        /// <summary>节点在本局地图中的冻结种类。</summary>
        public MapNodeKind Kind { get; }

        /// <summary>节点在本局地图中的冻结内容身份。</summary>
        public int ContentId { get; }

        /// <summary>冻结并验证一个路径节点。</summary>
        public RunSummaryPathNode(string nodeId, MapNodeKind kind, int contentId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Run summary path node id cannot be empty.", nameof(nodeId));
            if (!Enum.IsDefined(typeof(MapNodeKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == MapNodeKind.Start && contentId != 0)
                throw new ArgumentException("Run summary Start content id must be zero.", nameof(contentId));
            if (kind != MapNodeKind.Start && contentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentId));

            NodeId = nodeId;
            Kind = kind;
            ContentId = contentId;
        }

        /// <summary>按稳定节点身份与种类比较两个快照。</summary>
        public bool Equals(RunSummaryPathNode other)
        {
            return other != null &&
                   string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
                   Kind == other.Kind &&
                   ContentId == other.ContentId;
        }

        /// <summary>按值比较当前快照与任意对象。</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as RunSummaryPathNode);
        }

        /// <summary>返回节点身份与种类组合的哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (StringComparer.Ordinal.GetHashCode(NodeId) * 397) ^ (int)Kind;
                return (hash * 397) ^ ContentId;
            }
        }
    }

    /// <summary>终局牌组中的一张不可变卡牌实例快照。</summary>
    public sealed class RunSummaryCard : IEquatable<RunSummaryCard>
    {
        /// <summary>卡牌在所属 Run 内的稳定实例序号。</summary>
        public int InstanceSequence { get; }

        /// <summary>卡牌静态模板身份。</summary>
        public int TemplateId { get; }

        /// <summary>终局时的非负升级等级。</summary>
        public int UpgradeLevel { get; }

        /// <summary>冻结并验证一张终局卡牌实例。</summary>
        public RunSummaryCard(int instanceSequence, int templateId, int upgradeLevel)
        {
            if (instanceSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceSequence));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (upgradeLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));

            InstanceSequence = instanceSequence;
            TemplateId = templateId;
            UpgradeLevel = upgradeLevel;
        }

        /// <summary>按实例、模板与升级等级比较两个卡牌快照。</summary>
        public bool Equals(RunSummaryCard other)
        {
            return other != null &&
                   InstanceSequence == other.InstanceSequence &&
                   TemplateId == other.TemplateId &&
                   UpgradeLevel == other.UpgradeLevel;
        }

        /// <summary>按值比较当前卡牌快照与任意对象。</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as RunSummaryCard);
        }

        /// <summary>返回卡牌实例全部字段组合的哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (InstanceSequence * 397) ^ TemplateId;
                return (hash * 397) ^ UpgradeLevel;
            }
        }
    }

    /// <summary>终局持有物中的一个不可变遗物实例快照。</summary>
    public sealed class RunSummaryRelic : IEquatable<RunSummaryRelic>
    {
        /// <summary>遗物在所属 Run 内的稳定实例序号。</summary>
        public int InstanceSequence { get; }

        /// <summary>遗物静态模板身份。</summary>
        public int TemplateId { get; }

        /// <summary>冻结并验证一个终局遗物实例。</summary>
        public RunSummaryRelic(int instanceSequence, int templateId)
        {
            if (instanceSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceSequence));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));

            InstanceSequence = instanceSequence;
            TemplateId = templateId;
        }

        /// <summary>按实例与模板比较两个遗物快照。</summary>
        public bool Equals(RunSummaryRelic other)
        {
            return other != null &&
                   InstanceSequence == other.InstanceSequence &&
                   TemplateId == other.TemplateId;
        }

        /// <summary>按值比较当前遗物快照与任意对象。</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as RunSummaryRelic);
        }

        /// <summary>返回遗物实例全部字段组合的哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (InstanceSequence * 397) ^ TemplateId;
            }
        }
    }

    /// <summary>终局持有物中的一个不可变药水实例快照。</summary>
    public sealed class RunSummaryPotion : IEquatable<RunSummaryPotion>
    {
        /// <summary>药水在所属 Run 内的稳定实例序号。</summary>
        public int InstanceSequence { get; }

        /// <summary>药水静态模板身份。</summary>
        public int TemplateId { get; }

        /// <summary>冻结并验证一个终局药水实例。</summary>
        public RunSummaryPotion(int instanceSequence, int templateId)
        {
            if (instanceSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceSequence));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));

            InstanceSequence = instanceSequence;
            TemplateId = templateId;
        }

        /// <summary>按实例与模板比较两个药水快照。</summary>
        public bool Equals(RunSummaryPotion other)
        {
            return other != null &&
                   InstanceSequence == other.InstanceSequence &&
                   TemplateId == other.TemplateId;
        }

        /// <summary>按值比较当前药水快照与任意对象。</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as RunSummaryPotion);
        }

        /// <summary>返回药水实例全部字段组合的哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (InstanceSequence * 397) ^ TemplateId;
            }
        }
    }

    /// <summary>终局金币、遗物与药水的深冻结快照。</summary>
    public sealed class RunSummaryHoldings : IEquatable<RunSummaryHoldings>
    {
        private readonly ReadOnlyCollection<RunSummaryRelic> _relics;
        private readonly ReadOnlyCollection<RunSummaryPotion> _potions;

        /// <summary>终局非负金币。</summary>
        public int Gold { get; }

        /// <summary>按获得顺序冻结的遗物实例。</summary>
        public IReadOnlyList<RunSummaryRelic> Relics => _relics;

        /// <summary>按槽位顺序冻结的药水实例。</summary>
        public IReadOnlyList<RunSummaryPotion> Potions => _potions;

        /// <summary>复制并验证终局持有物的全部事实。</summary>
        public RunSummaryHoldings(
            int gold,
            IEnumerable<RunSummaryRelic> relics,
            IEnumerable<RunSummaryPotion> potions)
        {
            if (gold < 0)
                throw new ArgumentOutOfRangeException(nameof(gold));
            if (relics == null)
                throw new ArgumentNullException(nameof(relics));
            if (potions == null)
                throw new ArgumentNullException(nameof(potions));

            RunSummaryRelic[] frozenRelics = relics.ToArray();
            RunSummaryPotion[] frozenPotions = potions.ToArray();
            if (frozenRelics.Any(relic => relic == null))
                throw new ArgumentException("Run summary relics cannot contain null.", nameof(relics));
            if (frozenPotions.Any(potion => potion == null))
                throw new ArgumentException("Run summary potions cannot contain null.", nameof(potions));
            if (frozenPotions.Length > 3)
                throw new ArgumentException("Run summary cannot contain more than three potions.", nameof(potions));
            if (frozenRelics.Select(relic => relic.InstanceSequence).Distinct().Count() != frozenRelics.Length)
                throw new ArgumentException("Run summary relic instance ids must be unique.", nameof(relics));
            if (frozenRelics.Select(relic => relic.TemplateId).Distinct().Count() != frozenRelics.Length)
                throw new ArgumentException("Run summary relic templates must be unique.", nameof(relics));
            if (frozenPotions.Select(potion => potion.InstanceSequence).Distinct().Count() != frozenPotions.Length)
                throw new ArgumentException("Run summary potion instance ids must be unique.", nameof(potions));

            Gold = gold;
            _relics = Array.AsReadOnly(frozenRelics);
            _potions = Array.AsReadOnly(frozenPotions);
        }

        /// <summary>按金币与有序实例集合比较两份持有物快照。</summary>
        public bool Equals(RunSummaryHoldings other)
        {
            return other != null &&
                   Gold == other.Gold &&
                   _relics.SequenceEqual(other._relics) &&
                   _potions.SequenceEqual(other._potions);
        }

        /// <summary>按值比较当前持有物快照与任意对象。</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as RunSummaryHoldings);
        }

        /// <summary>返回持有物全部冻结事实组合的哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Gold;
                foreach (RunSummaryRelic relic in _relics)
                    hash = (hash * 397) ^ relic.GetHashCode();
                foreach (RunSummaryPotion potion in _potions)
                    hash = (hash * 397) ^ potion.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>从唯一 Terminal RunState 生成并逐 RunId 保存的一局深冻结摘要。</summary>
    public sealed class RunSummary : IEquatable<RunSummary>
    {
        private readonly ReadOnlyCollection<RunSummaryPathNode> _path;
        private readonly ReadOnlyCollection<RunSummaryCard> _deck;

        /// <summary>本局稳定身份。</summary>
        public RunId RunId { get; }

        /// <summary>首次观察到耐久终局的 UTC 时间。</summary>
        public DateTimeOffset CompletedAtUtc { get; }

        /// <summary>本局英雄模板身份。</summary>
        public int HeroTemplateId { get; }

        /// <summary>Victory、Defeat 或 Abandoned 唯一终局分类。</summary>
        public RunOutcomeKind OutcomeKind { get; }

        /// <summary>胜败终局绑定的 Battle 节点；主动放弃为空。</summary>
        public string OutcomeBattleNodeId { get; }

        /// <summary>胜败终局绑定的 attempt 序号；主动放弃为空。</summary>
        public int? OutcomeBattleAttemptSequence { get; }

        /// <summary>本局规则随机域的非零根种子。</summary>
        public uint RandomRootSeed { get; }

        /// <summary>终局生命。</summary>
        public int FinalHealth { get; }

        /// <summary>本局生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>本局已经签发的 Battle attempt 数。</summary>
        public int BattleAttemptCount { get; }

        /// <summary>按 Run 路径顺序深冻结的节点快照。</summary>
        public IReadOnlyList<RunSummaryPathNode> Path => _path;

        /// <summary>按牌组顺序深冻结的卡牌实例快照。</summary>
        public IReadOnlyList<RunSummaryCard> Deck => _deck;

        /// <summary>终局金币、遗物与药水深冻结快照。</summary>
        public RunSummaryHoldings Holdings { get; }

        /// <summary>终局路径节点数的只读派生值。</summary>
        public int PathNodeCount => _path.Count;

        /// <summary>终局牌组数量的只读派生值。</summary>
        public int DeckCount => _deck.Count;

        /// <summary>终局遗物数量的只读派生值。</summary>
        public int RelicCount => Holdings.Relics.Count;

        /// <summary>终局药水数量的只读派生值。</summary>
        public int PotionCount => Holdings.Potions.Count;

        /// <summary>复制并完整验证一局不可变终局摘要。</summary>
        public RunSummary(
            RunId runId,
            DateTimeOffset completedAtUtc,
            int heroTemplateId,
            RunOutcomeKind outcomeKind,
            string outcomeBattleNodeId,
            int? outcomeBattleAttemptSequence,
            uint randomRootSeed,
            int finalHealth,
            int maxHealth,
            int battleAttemptCount,
            IEnumerable<RunSummaryPathNode> path,
            IEnumerable<RunSummaryCard> deck,
            RunSummaryHoldings holdings)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run summary id cannot be empty.", nameof(runId));
            if (completedAtUtc == default)
                throw new ArgumentException("Run summary completion time is required.", nameof(completedAtUtc));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (!Enum.IsDefined(typeof(RunOutcomeKind), outcomeKind))
                throw new ArgumentOutOfRangeException(nameof(outcomeKind));
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (finalHealth < 0 || finalHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(finalHealth));
            if (battleAttemptCount < 0)
                throw new ArgumentOutOfRangeException(nameof(battleAttemptCount));
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (deck == null)
                throw new ArgumentNullException(nameof(deck));
            if (holdings == null)
                throw new ArgumentNullException(nameof(holdings));

            bool battleOutcome = outcomeKind == RunOutcomeKind.Victory ||
                                 outcomeKind == RunOutcomeKind.Defeat;
            if (battleOutcome != !string.IsNullOrWhiteSpace(outcomeBattleNodeId) ||
                battleOutcome != outcomeBattleAttemptSequence.HasValue)
            {
                throw new ArgumentException(
                    "Victory and Defeat require one outcome battle, while Abandoned cannot carry one.",
                    nameof(outcomeBattleNodeId));
            }
            if (outcomeBattleAttemptSequence.HasValue && outcomeBattleAttemptSequence.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(outcomeBattleAttemptSequence));
            if (battleOutcome && battleAttemptCount < outcomeBattleAttemptSequence.Value)
                throw new ArgumentException("Outcome battle exceeds the Run attempt count.", nameof(battleAttemptCount));
            if (outcomeKind == RunOutcomeKind.Victory && finalHealth <= 0)
                throw new ArgumentException("Victory requires positive final health.", nameof(finalHealth));
            if (outcomeKind == RunOutcomeKind.Defeat && finalHealth != 0)
                throw new ArgumentException("Defeat requires zero final health.", nameof(finalHealth));
            if (outcomeKind == RunOutcomeKind.Abandoned && finalHealth <= 0)
                throw new ArgumentException("Abandoned requires positive final health.", nameof(finalHealth));

            RunSummaryPathNode[] frozenPath = path.ToArray();
            RunSummaryCard[] frozenDeck = deck.ToArray();
            if (frozenPath.Length == 0 || frozenPath.Any(node => node == null))
                throw new ArgumentException("Run summary path must contain non-null Start.", nameof(path));
            if (frozenPath[0].Kind != MapNodeKind.Start)
                throw new ArgumentException("Run summary path must begin at Start.", nameof(path));
            if (frozenPath.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != frozenPath.Length)
                throw new ArgumentException("Run summary path node ids must be unique.", nameof(path));
            if (frozenDeck.Any(card => card == null))
                throw new ArgumentException("Run summary deck cannot contain null cards.", nameof(deck));
            if (frozenDeck.Select(card => card.InstanceSequence).Distinct().Count() != frozenDeck.Length)
                throw new ArgumentException("Run summary card instance ids must be unique.", nameof(deck));

            RunId = runId;
            CompletedAtUtc = completedAtUtc.ToUniversalTime();
            HeroTemplateId = heroTemplateId;
            OutcomeKind = outcomeKind;
            OutcomeBattleNodeId = battleOutcome ? outcomeBattleNodeId : null;
            OutcomeBattleAttemptSequence = outcomeBattleAttemptSequence;
            RandomRootSeed = randomRootSeed;
            FinalHealth = finalHealth;
            MaxHealth = maxHealth;
            BattleAttemptCount = battleAttemptCount;
            _path = Array.AsReadOnly(frozenPath);
            _deck = Array.AsReadOnly(frozenDeck);
            Holdings = holdings;
        }

        /// <summary>按全部持久业务字段比较两份终局摘要内容。</summary>
        public bool Equals(RunSummary other)
        {
            return other != null &&
                   RunId == other.RunId &&
                   CompletedAtUtc.Equals(other.CompletedAtUtc) &&
                   HeroTemplateId == other.HeroTemplateId &&
                   OutcomeKind == other.OutcomeKind &&
                   string.Equals(OutcomeBattleNodeId, other.OutcomeBattleNodeId, StringComparison.Ordinal) &&
                   OutcomeBattleAttemptSequence == other.OutcomeBattleAttemptSequence &&
                   RandomRootSeed == other.RandomRootSeed &&
                   FinalHealth == other.FinalHealth &&
                   MaxHealth == other.MaxHealth &&
                   BattleAttemptCount == other.BattleAttemptCount &&
                   _path.SequenceEqual(other._path) &&
                   _deck.SequenceEqual(other._deck) &&
                   Holdings.Equals(other.Holdings);
        }

        /// <summary>按值比较当前终局摘要与任意对象。</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as RunSummary);
        }

        /// <summary>返回全部持久业务字段组合的哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RunId.GetHashCode();
                hash = (hash * 397) ^ CompletedAtUtc.GetHashCode();
                hash = (hash * 397) ^ HeroTemplateId;
                hash = (hash * 397) ^ (int)OutcomeKind;
                hash = (hash * 397) ^ (OutcomeBattleNodeId == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(OutcomeBattleNodeId));
                hash = (hash * 397) ^ OutcomeBattleAttemptSequence.GetHashCode();
                hash = (hash * 397) ^ RandomRootSeed.GetHashCode();
                hash = (hash * 397) ^ FinalHealth;
                hash = (hash * 397) ^ MaxHealth;
                hash = (hash * 397) ^ BattleAttemptCount;
                foreach (RunSummaryPathNode node in _path)
                    hash = (hash * 397) ^ node.GetHashCode();
                foreach (RunSummaryCard card in _deck)
                    hash = (hash * 397) ^ card.GetHashCode();
                return (hash * 397) ^ Holdings.GetHashCode();
            }
        }
    }
}
