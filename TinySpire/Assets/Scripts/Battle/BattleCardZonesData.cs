using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using R3;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>
    /// 单场战斗内卡牌实例的唯一标识。
    /// </summary>
    public readonly struct CardInstanceId : IEquatable<CardInstanceId>
    {
        /// <summary>标识的整数值。</summary>
        public int Value { get; }

        /// <summary>由卡区聚合分配卡牌实例标识。</summary>
        internal CardInstanceId(int value)
        {
            Value = value;
        }

        /// <summary>比较两个卡牌实例标识是否相同。</summary>
        public bool Equals(CardInstanceId other)
        {
            return Value == other.Value;
        }

        /// <summary>比较此标识与另一个对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is CardInstanceId other && Equals(other);
        }

        /// <summary>返回可用于字典键的稳定哈希值。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>返回标识的文本形式。</summary>
        public override string ToString()
        {
            return Value.ToString();
        }

        /// <summary>判断两个卡牌实例标识是否相同。</summary>
        public static bool operator ==(CardInstanceId left, CardInstanceId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个卡牌实例标识是否不同。</summary>
        public static bool operator !=(CardInstanceId left, CardInstanceId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 单场战斗中创建的唯一卡牌实例；只引用静态卡牌模板，不复制模板字段。
    /// </summary>
    public sealed class CardInstanceData
    {
        /// <summary>本场战斗内卡牌实例标识。</summary>
        public CardInstanceId Id { get; }

        /// <summary>静态 Card 配置表中的模板标识。</summary>
        public int TemplateId { get; }

        /// <summary>由卡区聚合建立模板引用与实例标识的对应关系。</summary>
        internal CardInstanceData(CardInstanceId id, int templateId)
        {
            Id = id;
            TemplateId = templateId;
        }
    }

    /// <summary>
    /// 一副战斗牌组四个区域的完整有序布局。
    /// 布局发布后不可变，因此观察者不会读到只移动了一半的卡牌。
    /// </summary>
    public sealed class CardZoneLayoutData
    {
        /// <summary>抽牌堆，从列表末尾抽取。</summary>
        public IReadOnlyList<CardInstanceId> DrawPile { get; }

        /// <summary>当前手牌。</summary>
        public IReadOnlyList<CardInstanceId> Hand { get; }

        /// <summary>弃牌堆。</summary>
        public IReadOnlyList<CardInstanceId> DiscardPile { get; }

        /// <summary>消耗区。</summary>
        public IReadOnlyList<CardInstanceId> ExhaustPile { get; }

        /// <summary>已生效能力牌的战斗内归宿；它们不再参与抽弃牌循环。</summary>
        public IReadOnlyList<CardInstanceId> PowerPile { get; }

        /// <summary>复制四个区域的顺序并冻结为一份完整布局。</summary>
        internal CardZoneLayoutData(
            IEnumerable<CardInstanceId> drawPile,
            IEnumerable<CardInstanceId> hand,
            IEnumerable<CardInstanceId> discardPile,
            IEnumerable<CardInstanceId> exhaustPile)
            : this(
                drawPile,
                hand,
                discardPile,
                exhaustPile,
                Array.Empty<CardInstanceId>())
        {
        }

        /// <summary>复制并冻结五个卡区的顺序，能力牌区不参与常规抽弃牌循环。</summary>
        internal CardZoneLayoutData(
            IEnumerable<CardInstanceId> drawPile,
            IEnumerable<CardInstanceId> hand,
            IEnumerable<CardInstanceId> discardPile,
            IEnumerable<CardInstanceId> exhaustPile,
            IEnumerable<CardInstanceId> powerPile)
        {
            DrawPile = Freeze(drawPile);
            Hand = Freeze(hand);
            DiscardPile = Freeze(discardPile);
            ExhaustPile = Freeze(exhaustPile);
            PowerPile = Freeze(powerPile);
        }

        /// <summary>复制可枚举序列，防止已发布布局被外部集合后续修改。</summary>
        private static IReadOnlyList<CardInstanceId> Freeze(IEnumerable<CardInstanceId> cardIds)
        {
            if (cardIds == null)
                throw new ArgumentNullException(nameof(cardIds));

            return new ReadOnlyCollection<CardInstanceId>(new List<CardInstanceId>(cardIds));
        }
    }

    /// <summary>一次普通抽牌的冻结计划；卡区布局、洗牌随机与结算顺序均在首次写入前确定。</summary>
    internal sealed class BattlePreparedDraw
    {
        /// <summary>创建本计划的唯一卡区聚合。</summary>
        internal BattleCardZonesData Owner { get; }

        /// <summary>预演时读取的不可变权威布局。</summary>
        internal CardZoneLayoutData InitialLayout { get; }

        /// <summary>预演时读取的洗牌随机状态。</summary>
        internal uint ShuffleRandomStateBefore { get; }

        /// <summary>完成本次可能重洗后的洗牌随机状态。</summary>
        internal uint ShuffleRandomStateAfter { get; }

        /// <summary>有实际抽牌时提交的完整下一布局；零抽牌时保持初始布局引用。</summary>
        internal CardZoneLayoutData NextLayout { get; }

        /// <summary>按逻辑发生顺序冻结的重洗与抽牌记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>计划是否已经完成唯一提交。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结计划归属、前后随机状态、下一布局与连续结算。</summary>
        internal BattlePreparedDraw(
            BattleCardZonesData owner,
            CardZoneLayoutData initialLayout,
            uint shuffleRandomStateBefore,
            uint shuffleRandomStateAfter,
            CardZoneLayoutData nextLayout,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            ShuffleRandomStateBefore = shuffleRandomStateBefore;
            ShuffleRandomStateAfter = shuffleRandomStateAfter;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }

        /// <summary>消费已经通过权威快照校验的计划，并拒绝重复提交。</summary>
        internal void MarkCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("普通抽牌计划已经提交。");

            IsCommitted = true;
        }
    }

    /// <summary>一张普通已打出卡牌的冻结离手计划；校验后提交不再读取可能被前序结算观察者改写的布局。</summary>
    internal sealed class BattlePreparedPlayedCardDeparture
    {
        private bool _validationAttempted;
        private bool _validated;
        private bool _consumed;

        /// <summary>创建本计划的唯一卡区聚合。</summary>
        internal BattleCardZonesData Owner { get; }

        /// <summary>预演时读取的不可变权威布局。</summary>
        internal CardZoneLayoutData InitialLayout { get; }

        /// <summary>提交时一次发布的冻结下一布局。</summary>
        internal CardZoneLayoutData NextLayout { get; }

        /// <summary>本次离手的卡牌实例。</summary>
        internal CardInstanceId CardId { get; }

        /// <summary>本次冻结移动的来源卡区。</summary>
        internal BattleCardZone FromZone { get; }

        /// <summary>本次冻结移动的目标卡区。</summary>
        internal BattleCardZone ToZone { get; }

        /// <summary>指示本计划是否已完成唯一一次成功校验。</summary>
        internal bool IsValidated => _validationAttempted && _validated;

        /// <summary>指示本计划是否已被提交入口消费。</summary>
        internal bool IsConsumed => _consumed;

        /// <summary>冻结计划归属、前后布局与唯一移动事实。</summary>
        internal BattlePreparedPlayedCardDeparture(
            BattleCardZonesData owner,
            CardZoneLayoutData initialLayout,
            CardZoneLayoutData nextLayout,
            CardInstanceId cardId,
            BattleCardZone fromZone,
            BattleCardZone toZone)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            CardId = cardId;
            FromZone = fromZone;
            ToZone = toZone;
        }

        /// <summary>只记录一次校验结论，失败计划同样禁止被重复尝试。</summary>
        internal void MarkValidated(bool succeeded)
        {
            if (_validationAttempted)
                throw new InvalidOperationException("同一普通离手计划不得重复校验。");

            _validationAttempted = true;
            _validated = succeeded;
        }

        /// <summary>要求计划已成功校验且未提交，再把它标记为一次性消费。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated)
                throw new InvalidOperationException("普通离手计划必须先完成成功校验。");
            if (_consumed)
                throw new InvalidOperationException("同一普通离手计划不得重复提交。");

            _consumed = true;
        }
    }

    /// <summary>一张已打出卡牌离手后抽至手牌上限的冻结计划；卡区布局、洗牌随机与结算顺序均在首次写入前确定。</summary>
    internal sealed class BattlePreparedPlayedCardDepartureAndDraw
    {
        /// <summary>创建本计划的唯一卡区聚合。</summary>
        internal BattleCardZonesData Owner { get; }

        /// <summary>预演时读取的不可变权威布局。</summary>
        internal CardZoneLayoutData InitialLayout { get; }

        /// <summary>预演时读取的洗牌随机状态。</summary>
        internal uint ShuffleRandomStateBefore { get; }

        /// <summary>完成本次可能重洗后的洗牌随机状态。</summary>
        internal uint ShuffleRandomStateAfter { get; }

        /// <summary>提交时一次发布的完整下一布局。</summary>
        internal CardZoneLayoutData NextLayout { get; }

        /// <summary>按逻辑发生顺序冻结的离手、重洗与抽牌记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>计划是否已经完成唯一提交。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结计划归属、前后随机状态、下一布局与连续结算。</summary>
        internal BattlePreparedPlayedCardDepartureAndDraw(
            BattleCardZonesData owner,
            CardZoneLayoutData initialLayout,
            uint shuffleRandomStateBefore,
            uint shuffleRandomStateAfter,
            CardZoneLayoutData nextLayout,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            ShuffleRandomStateBefore = shuffleRandomStateBefore;
            ShuffleRandomStateAfter = shuffleRandomStateAfter;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }

        /// <summary>消费已经通过权威快照校验的计划，并拒绝重复提交。</summary>
        internal void MarkCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("已打出卡牌离手抽牌计划已经提交。");

            IsCommitted = true;
        }
    }

    /// <summary>一张被选手牌与当前打出卡共享唯一布局提交的冻结计划。</summary>
    internal sealed class BattlePreparedHandCardSelectionResolution
    {
        /// <summary>创建本计划的唯一卡区聚合。</summary>
        internal BattleCardZonesData Owner { get; }

        /// <summary>预演时读取的不可变权威布局。</summary>
        internal CardZoneLayoutData InitialLayout { get; }

        /// <summary>提交时一次发布的完整下一布局。</summary>
        internal CardZoneLayoutData NextLayout { get; }

        /// <summary>被选手牌的冻结移动记录。</summary>
        internal BattleCardMovedSettlement SelectedCardMovement { get; }

        /// <summary>当前打出卡的冻结归宿记录。</summary>
        internal BattleCardMovedSettlement PlayedCardDeparture { get; }

        /// <summary>计划是否已经完成唯一提交。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结计划归属、布局快照与两段逻辑移动记录。</summary>
        internal BattlePreparedHandCardSelectionResolution(
            BattleCardZonesData owner,
            CardZoneLayoutData initialLayout,
            CardZoneLayoutData nextLayout,
            BattleCardMovedSettlement selectedCardMovement,
            BattleCardMovedSettlement playedCardDeparture)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            SelectedCardMovement = selectedCardMovement
                ?? throw new ArgumentNullException(nameof(selectedCardMovement));
            PlayedCardDeparture = playedCardDeparture
                ?? throw new ArgumentNullException(nameof(playedCardDeparture));
        }

        /// <summary>消费已通过权威快照校验的计划，并拒绝重复提交。</summary>
        internal void MarkCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("手牌选择解算计划已经提交。");

            IsCommitted = true;
        }
    }

    /// <summary>一次可选手牌消耗、投影抽牌与当前牌归宿共享唯一布局提交的冻结计划。</summary>
    internal sealed class BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture
    {
        /// <summary>创建本计划的唯一卡区聚合。</summary>
        internal BattleCardZonesData Owner { get; }

        /// <summary>预演时读取的不可变权威布局。</summary>
        internal CardZoneLayoutData InitialLayout { get; }

        /// <summary>预演时读取的洗牌随机状态。</summary>
        internal uint ShuffleRandomStateBefore { get; }

        /// <summary>完成本次可能重洗后的洗牌随机状态。</summary>
        internal uint ShuffleRandomStateAfter { get; }

        /// <summary>提交时一次发布的完整下一布局。</summary>
        internal CardZoneLayoutData NextLayout { get; }

        /// <summary>按选择、重洗、抽牌和来源归宿顺序冻结的连续记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>计划是否已经完成唯一提交。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结计划归属、布局、随机前后状态、下一布局与连续结算。</summary>
        internal BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture(
            BattleCardZonesData owner,
            CardZoneLayoutData initialLayout,
            uint shuffleRandomStateBefore,
            uint shuffleRandomStateAfter,
            CardZoneLayoutData nextLayout,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            ShuffleRandomStateBefore = shuffleRandomStateBefore;
            ShuffleRandomStateAfter = shuffleRandomStateAfter;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }

        /// <summary>消费仍匹配权威快照的计划，并拒绝重复提交。</summary>
        internal void MarkCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("选择抽牌归宿计划已经提交。");

            IsCommitted = true;
        }
    }

    /// <summary>当前牌离手、其余手牌弃置并创建等量临时牌的冻结计划；实例身份与唯一下一布局均在首次写入前确定。</summary>
    internal sealed class BattlePreparedPlayedCardDepartureDiscardHandAndCreate
    {
        /// <summary>创建本计划的唯一卡区聚合。</summary>
        internal BattleCardZonesData Owner { get; }

        /// <summary>预演时读取的不可变权威布局。</summary>
        internal CardZoneLayoutData InitialLayout { get; }

        /// <summary>预演时卡牌实例映射中的条目数量。</summary>
        internal int InitialCardCount { get; }

        /// <summary>预演时下一张卡牌实例将使用的标识。</summary>
        internal int NextInstanceIdBefore { get; }

        /// <summary>提交全部冻结实例后下一张卡牌将使用的标识。</summary>
        internal int NextInstanceIdAfter { get; }

        /// <summary>预演时读取的洗牌随机状态；本操作自身不会推进该随机流。</summary>
        internal uint ShuffleRandomStateBefore { get; }

        /// <summary>提交时一次发布的完整下一布局。</summary>
        internal CardZoneLayoutData NextLayout { get; }

        /// <summary>本次创建并直接加入新手牌的全部冻结实例。</summary>
        internal IReadOnlyList<CardInstanceData> CreatedCards { get; }

        /// <summary>按逻辑发生顺序冻结的当前牌离手、其余手牌弃置与创建记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>计划是否已经完成唯一提交。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结完整权威快照、待创建实例、下一布局与连续结算。</summary>
        internal BattlePreparedPlayedCardDepartureDiscardHandAndCreate(
            BattleCardZonesData owner,
            CardZoneLayoutData initialLayout,
            int initialCardCount,
            int nextInstanceIdBefore,
            int nextInstanceIdAfter,
            uint shuffleRandomStateBefore,
            CardZoneLayoutData nextLayout,
            IEnumerable<CardInstanceData> createdCards,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            if (initialCardCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCardCount));
            if (nextInstanceIdBefore <= 0)
                throw new ArgumentOutOfRangeException(nameof(nextInstanceIdBefore));
            if (nextInstanceIdAfter < nextInstanceIdBefore)
                throw new ArgumentOutOfRangeException(nameof(nextInstanceIdAfter));
            if (createdCards == null)
                throw new ArgumentNullException(nameof(createdCards));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            InitialCardCount = initialCardCount;
            NextInstanceIdBefore = nextInstanceIdBefore;
            NextInstanceIdAfter = nextInstanceIdAfter;
            ShuffleRandomStateBefore = shuffleRandomStateBefore;
            CreatedCards = new ReadOnlyCollection<CardInstanceData>(
                new List<CardInstanceData>(createdCards));
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }

        /// <summary>消费已经通过完整权威快照校验的计划，并拒绝重复提交。</summary>
        internal void MarkCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("离手弃牌并创建临时牌的计划已经提交。");

            IsCommitted = true;
        }
    }

    /// <summary>首回合起手牌的冻结计划；固有牌选择、补牌顺序与最终布局均在任何战斗写入前确定。</summary>
    internal sealed class BattlePreparedOpeningHand
    {
        /// <summary>创建本计划的唯一卡区聚合。</summary>
        internal BattleCardZonesData Owner { get; }

        /// <summary>预演时读取的不可变权威布局。</summary>
        internal CardZoneLayoutData InitialLayout { get; }

        /// <summary>预演时读取的洗牌随机状态。</summary>
        internal uint ShuffleRandomStateBefore { get; }

        /// <summary>提交时一次发布的完整起手布局。</summary>
        internal CardZoneLayoutData NextLayout { get; }

        /// <summary>按真实抽取顺序冻结的全部起手牌实例。</summary>
        internal IReadOnlyList<CardInstanceId> DealtCardIds { get; }

        /// <summary>计划是否已经完成唯一提交。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结计划归属、权威快照、最终布局与起手顺序。</summary>
        internal BattlePreparedOpeningHand(
            BattleCardZonesData owner,
            CardZoneLayoutData initialLayout,
            uint shuffleRandomStateBefore,
            CardZoneLayoutData nextLayout,
            IEnumerable<CardInstanceId> dealtCardIds)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialLayout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            if (dealtCardIds == null)
                throw new ArgumentNullException(nameof(dealtCardIds));

            ShuffleRandomStateBefore = shuffleRandomStateBefore;
            DealtCardIds = new ReadOnlyCollection<CardInstanceId>(
                new List<CardInstanceId>(dealtCardIds));
        }

        /// <summary>消费已经通过权威快照校验的起手计划，并拒绝重复提交。</summary>
        internal void MarkCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("起手牌计划已经提交。");

            IsCommitted = true;
        }
    }

    /// <summary>
    /// 持有全部卡牌实例，以及四个战斗卡区的唯一响应式布局事实。
    /// </summary>
    public sealed class BattleCardZonesData : IDisposable
    {
        /// <summary>普通战斗规则统一使用的手牌容量上限。</summary>
        internal const int BattleCardHandLimit = 10;

        private readonly Dictionary<CardInstanceId, CardInstanceData> _cards;
        private readonly GameRandom _shuffleRandom;
        private readonly ReactiveProperty<CardZoneLayoutData> _layout;
        private int _nextInstanceId;

        /// <summary>所有卡牌实例的只读映射。</summary>
        public IReadOnlyDictionary<CardInstanceId, CardInstanceData> Cards => _cards;

        /// <summary>四个卡区的完整只读响应式布局；一次操作至多发布一次完整快照。</summary>
        public ReadOnlyReactiveProperty<CardZoneLayoutData> Layout { get; }

        /// <summary>当前抽牌堆的只读视图。</summary>
        public IReadOnlyList<CardInstanceId> DrawPile => Layout.CurrentValue.DrawPile;

        /// <summary>当前手牌的只读视图。</summary>
        public IReadOnlyList<CardInstanceId> Hand => Layout.CurrentValue.Hand;

        /// <summary>当前弃牌堆的只读视图。</summary>
        public IReadOnlyList<CardInstanceId> DiscardPile => Layout.CurrentValue.DiscardPile;

        /// <summary>当前消耗区的只读视图。</summary>
        public IReadOnlyList<CardInstanceId> ExhaustPile => Layout.CurrentValue.ExhaustPile;

        /// <summary>当前已生效能力牌的只读视图。</summary>
        public IReadOnlyList<CardInstanceId> PowerPile => Layout.CurrentValue.PowerPile;

        /// <summary>牌堆洗牌随机流的当前状态，供确定性验证使用。</summary>
        public uint ShuffleRandomState => _shuffleRandom.State;

        /// <summary>
        /// 从卡牌模板列表创建实例、使用给定种子洗牌，并发布初始布局。
        /// </summary>
        public BattleCardZonesData(IEnumerable<int> cardTemplateIds, uint shuffleSeed)
        {
            if (cardTemplateIds == null)
                throw new ArgumentNullException(nameof(cardTemplateIds));

            _shuffleRandom = new GameRandom(shuffleSeed);
            _cards = new Dictionary<CardInstanceId, CardInstanceData>();
            var drawPile = new List<CardInstanceId>();

            _nextInstanceId = 1;
            foreach (int templateId in cardTemplateIds)
            {
                if (templateId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(cardTemplateIds));

                var cardId = AllocateCardInstanceId();
                _cards.Add(cardId, new CardInstanceData(cardId, templateId));
                drawPile.Add(cardId);
            }

            _shuffleRandom.Shuffle(drawPile);
            _layout = new ReactiveProperty<CardZoneLayoutData>(
                new CardZoneLayoutData(
                    drawPile,
                    Array.Empty<CardInstanceId>(),
                    Array.Empty<CardInstanceId>(),
                    Array.Empty<CardInstanceId>(),
                    Array.Empty<CardInstanceId>()));
            Layout = _layout.ToReadOnlyReactiveProperty();
        }

        /// <summary>
        /// 按实例标识查找卡牌，不依赖其当前所在区域。
        /// </summary>
        public bool TryGetCard(CardInstanceId cardId, out CardInstanceData card)
        {
            return _cards.TryGetValue(cardId, out card);
        }

        /// <summary>
        /// 抽取至多 count 张卡；抽牌堆为空时将弃牌堆洗回抽牌堆。
        /// 返回全部移动与重洗顺序，调用方可指定其在当前命令中的起始记录序号。
        /// </summary>
        public BattleCardZoneOperationResult Draw(
            int count,
            int startingOrder = 0,
            int handLimit = int.MaxValue)
        {
            return CommitPreparedDraw(PrepareDraw(count, startingOrder, handLimit));
        }

        /// <summary>在本地副本中冻结普通抽牌、可能重洗、下一布局与随机状态，准备阶段保持权威事实零写入。</summary>
        internal BattlePreparedDraw PrepareDraw(
            int count,
            int startingOrder = 0,
            int handLimit = int.MaxValue)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (handLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(handLimit));

            CardZoneLayoutData initialLayout = Layout.CurrentValue;
            var drawPile = new List<CardInstanceId>(initialLayout.DrawPile);
            var hand = new List<CardInstanceId>(initialLayout.Hand);
            var discardPile = new List<CardInstanceId>(initialLayout.DiscardPile);
            int requestedCount = Math.Min(count, Math.Max(0, handLimit - hand.Count));
            int drawableCount = Math.Min(requestedCount, drawPile.Count + discardPile.Count);
            bool willReshuffle = requestedCount > drawPile.Count && discardPile.Count > 0;
            long settlementCount = drawableCount;
            if (willReshuffle)
                settlementCount += discardPile.Count + 1L;
            ValidateSettlementOrderRange(startingOrder, settlementCount);

            uint randomStateBefore = _shuffleRandom.State;
            var candidateRandom = new GameRandom(randomStateBefore)
            {
                State = randomStateBefore,
            };
            var settlements = new List<BattleSettlementRecord>((int)settlementCount);
            int drawnCount = 0;
            while (drawnCount < requestedCount)
            {
                if (drawPile.Count == 0)
                {
                    foreach (CardInstanceId discardedCardId in discardPile)
                    {
                        settlements.Add(new BattleCardMovedSettlement(
                            startingOrder + settlements.Count,
                            discardedCardId,
                            BattleCardZone.DiscardPile,
                            BattleCardZone.DrawPile));
                    }

                    drawPile.AddRange(discardPile);
                    discardPile.Clear();
                    candidateRandom.Shuffle(drawPile);
                    if (drawPile.Count > 0)
                    {
                        settlements.Add(new BattleCardsReshuffledSettlement(
                            startingOrder + settlements.Count,
                            drawPile));
                    }
                }

                if (drawPile.Count == 0)
                    break;

                int topIndex = drawPile.Count - 1;
                CardInstanceId cardId = drawPile[topIndex];
                drawPile.RemoveAt(topIndex);
                hand.Add(cardId);
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder + settlements.Count,
                    cardId,
                    BattleCardZone.DrawPile,
                    BattleCardZone.Hand));
                drawnCount++;
            }

            CardZoneLayoutData nextLayout = drawnCount > 0
                ? new CardZoneLayoutData(
                    drawPile,
                    hand,
                    discardPile,
                    initialLayout.ExhaustPile,
                    initialLayout.PowerPile)
                : initialLayout;
            return new BattlePreparedDraw(
                this,
                initialLayout,
                randomStateBefore,
                candidateRandom.State,
                nextLayout,
                settlements);
        }

        /// <summary>纯只读确认普通抽牌计划仍归属于本聚合，且布局、随机与一次性状态均未漂移。</summary>
        internal bool ValidatePreparedDraw(BattlePreparedDraw plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return ReferenceEquals(plan.Owner, this) &&
                !plan.IsCommitted &&
                ReferenceEquals(Layout.CurrentValue, plan.InitialLayout) &&
                _shuffleRandom.State == plan.ShuffleRandomStateBefore;
        }

        /// <summary>提交仍匹配权威快照的普通抽牌计划一次；不再随机，并只在实际抽牌时发布一次完整布局。</summary>
        internal BattleCardZoneOperationResult CommitPreparedDraw(BattlePreparedDraw plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他卡区聚合创建的普通抽牌计划。");
            if (!ValidatePreparedDraw(plan))
                throw new InvalidOperationException("普通抽牌计划已提交，或其布局与随机权威快照已经漂移。");

            plan.MarkCommitted();
            _shuffleRandom.State = plan.ShuffleRandomStateAfter;
            if (!ReferenceEquals(plan.NextLayout, plan.InitialLayout))
                _layout.Value = plan.NextLayout;
            return new BattleCardZoneOperationResult(
                succeeded: true,
                plan.Settlements);
        }

        /// <summary>
        /// 从尚未发牌的已洗牌抽牌堆冻结首回合起手：先按真实抽取顺序取出全部固有牌，
        /// 再从剩余牌堆顶部补普通牌至目标数量；调用方只提供无序的固有牌身份集合。
        /// </summary>
        internal BattlePreparedOpeningHand PrepareOpeningHand(
            IReadOnlyCollection<CardInstanceId> innateCardIds,
            int targetHandCount,
            int handLimit)
        {
            if (innateCardIds == null)
                throw new ArgumentNullException(nameof(innateCardIds));
            if (targetHandCount < 0)
                throw new ArgumentOutOfRangeException(nameof(targetHandCount));
            if (handLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(handLimit));
            if (targetHandCount > handLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetHandCount),
                    "起手目标数量不能超过手牌上限。");
            }

            CardZoneLayoutData initialLayout = Layout.CurrentValue;
            ValidateInitialOpeningLayout(initialLayout);

            var innateSet = new HashSet<CardInstanceId>();
            foreach (CardInstanceId cardId in innateCardIds)
            {
                if (!innateSet.Add(cardId))
                    throw new ArgumentException("固有牌身份集合不能包含重复实例。", nameof(innateCardIds));
                if (!_cards.ContainsKey(cardId))
                    throw new ArgumentException("固有牌身份必须属于当前卡区聚合。", nameof(innateCardIds));
            }
            if (innateSet.Count > handLimit)
            {
                throw new InvalidOperationException(
                    $"固有牌数量 {innateSet.Count} 超过手牌上限 {handLimit}。");
            }

            var innateInDrawOrder = new List<CardInstanceId>(innateSet.Count);
            for (int index = initialLayout.DrawPile.Count - 1; index >= 0; index--)
            {
                CardInstanceId cardId = initialLayout.DrawPile[index];
                if (innateSet.Contains(cardId))
                    innateInDrawOrder.Add(cardId);
            }
            if (innateInDrawOrder.Count != innateSet.Count)
                throw new InvalidOperationException("全部固有牌实例都必须仍在初始抽牌堆中。");

            var drawPile = new List<CardInstanceId>(initialLayout.DrawPile.Count - innateSet.Count);
            foreach (CardInstanceId cardId in initialLayout.DrawPile)
            {
                if (!innateSet.Contains(cardId))
                    drawPile.Add(cardId);
            }

            var hand = new List<CardInstanceId>(Math.Max(targetHandCount, innateSet.Count));
            hand.AddRange(innateInDrawOrder);
            while (hand.Count < targetHandCount && drawPile.Count > 0)
            {
                int topIndex = drawPile.Count - 1;
                CardInstanceId cardId = drawPile[topIndex];
                drawPile.RemoveAt(topIndex);
                hand.Add(cardId);
            }

            return new BattlePreparedOpeningHand(
                this,
                initialLayout,
                _shuffleRandom.State,
                new CardZoneLayoutData(
                    drawPile,
                    hand,
                    initialLayout.DiscardPile,
                    initialLayout.ExhaustPile,
                    initialLayout.PowerPile),
                hand);
        }

        /// <summary>纯只读确认起手计划仍归属于本聚合，且布局、随机与一次性状态均未漂移。</summary>
        internal bool ValidatePreparedOpeningHand(BattlePreparedOpeningHand plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return ReferenceEquals(plan.Owner, this) &&
                !plan.IsCommitted &&
                ReferenceEquals(Layout.CurrentValue, plan.InitialLayout) &&
                _shuffleRandom.State == plan.ShuffleRandomStateBefore;
        }

        /// <summary>提交仍匹配权威快照的起手计划一次，并从指定序号生成连续移动记录后只发布一次完整布局。</summary>
        internal BattleCardZoneOperationResult CommitPreparedOpeningHand(
            BattlePreparedOpeningHand plan,
            int startingOrder = 0)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他卡区聚合创建的起手牌计划。");
            if (!ValidatePreparedOpeningHand(plan))
                throw new InvalidOperationException("起手牌计划已提交，或其布局与随机权威快照已经漂移。");

            ValidateSettlementOrderRange(startingOrder, plan.DealtCardIds.Count);
            var settlements = new List<BattleSettlementRecord>(plan.DealtCardIds.Count);
            foreach (CardInstanceId cardId in plan.DealtCardIds)
            {
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder + settlements.Count,
                    cardId,
                    BattleCardZone.DrawPile,
                    BattleCardZone.Hand));
            }

            plan.MarkCommitted();
            _layout.Value = plan.NextLayout;
            return new BattleCardZoneOperationResult(succeeded: true, settlements);
        }

        /// <summary>
        /// 在本地副本中同时移出被选手牌与当前打出卡，并冻结可与其他逻辑结算交错的记录序号。
        /// </summary>
        internal BattlePreparedHandCardSelectionResolution PrepareHandCardSelectionResolution(
            CardInstanceId selectedCardId,
            BattleCardZone selectedDestination,
            CardInstanceId playedCardId,
            BattleCardZone playedCardDestination,
            int selectedStartingOrder,
            int playedCardStartingOrder)
        {
            if (selectedCardId == playedCardId)
                throw new InvalidOperationException("被选手牌不能是当前打出卡。");
            if (selectedStartingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(selectedStartingOrder));
            if (playedCardStartingOrder <= selectedStartingOrder)
                throw new ArgumentOutOfRangeException(nameof(playedCardStartingOrder));
            ValidatePlayedCardDestination(selectedDestination);
            ValidatePlayedCardDestination(playedCardDestination);
            ValidateSettlementOrderRange(selectedStartingOrder, settlementCount: 1);
            ValidateSettlementOrderRange(playedCardStartingOrder, settlementCount: 1);

            CardZoneLayoutData initialLayout = Layout.CurrentValue;
            var hand = new List<CardInstanceId>(initialLayout.Hand);
            int selectedIndex = hand.IndexOf(selectedCardId);
            if (selectedIndex < 0)
                throw new InvalidOperationException("只有当前手牌才能作为卡牌效果的选择。");
            if (hand.IndexOf(playedCardId) < 0)
                throw new InvalidOperationException("只有仍在手牌中的当前卡才能预演选择解算。");

            hand.RemoveAt(selectedIndex);
            hand.Remove(playedCardId);
            var discardPile = new List<CardInstanceId>(initialLayout.DiscardPile);
            var exhaustPile = new List<CardInstanceId>(initialLayout.ExhaustPile);
            var powerPile = new List<CardInstanceId>(initialLayout.PowerPile);
            AddCardToDestination(
                selectedCardId,
                selectedDestination,
                discardPile,
                exhaustPile,
                powerPile);
            AddCardToDestination(
                playedCardId,
                playedCardDestination,
                discardPile,
                exhaustPile,
                powerPile);

            return new BattlePreparedHandCardSelectionResolution(
                this,
                initialLayout,
                new CardZoneLayoutData(
                    initialLayout.DrawPile,
                    hand,
                    discardPile,
                    exhaustPile,
                    powerPile),
                new BattleCardMovedSettlement(
                    selectedStartingOrder,
                    selectedCardId,
                    BattleCardZone.Hand,
                    selectedDestination),
                new BattleCardMovedSettlement(
                    playedCardStartingOrder,
                    playedCardId,
                    BattleCardZone.Hand,
                    playedCardDestination));
        }

        /// <summary>纯只读确认选择解算计划仍归属本聚合，且布局与一次性状态均未漂移。</summary>
        internal bool ValidatePreparedHandCardSelectionResolution(
            BattlePreparedHandCardSelectionResolution plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return ReferenceEquals(plan.Owner, this) &&
                !plan.IsCommitted &&
                ReferenceEquals(Layout.CurrentValue, plan.InitialLayout);
        }

        /// <summary>一次提交已冻结的两张手牌归宿；不重新计算，并只发布一次完整卡区布局。</summary>
        internal BattleCardZoneOperationResult CommitPreparedHandCardSelectionResolution(
            BattlePreparedHandCardSelectionResolution plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他卡区聚合创建的手牌选择解算计划。");
            if (!ValidatePreparedHandCardSelectionResolution(plan))
                throw new InvalidOperationException("手牌选择解算计划已提交，或其权威布局已经漂移。");

            plan.MarkCommitted();
            _layout.Value = plan.NextLayout;
            return new BattleCardZoneOperationResult(
                succeeded: true,
                new BattleSettlementRecord[]
                {
                    plan.SelectedCardMovement,
                    plan.PlayedCardDeparture,
                });
        }

        /// <summary>
        /// 在本地副本中先处理可选的另一张手牌，再让当前牌继续占用手牌上限完成指定抽牌，最后冻结当前牌归宿；
        /// 选择、重洗、抽牌和当前牌离手共享同一随机快照、下一布局与连续 settlement 链。
        /// </summary>
        internal BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture
            PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                CardInstanceId? selectedCardId,
                BattleCardZone selectedDestination,
                int drawCount,
                int handLimit,
                CardInstanceId playedCardId,
                BattleCardZone playedCardDestination,
                int startingOrder)
        {
            if (drawCount < 0)
                throw new ArgumentOutOfRangeException(nameof(drawCount));
            if (handLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(handLimit));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            ValidatePlayedCardDestination(selectedDestination);
            ValidatePlayedCardDestination(playedCardDestination);
            if (selectedCardId.HasValue && selectedCardId.Value == playedCardId)
                throw new InvalidOperationException("被选手牌不能是当前打出的卡牌。");

            CardZoneLayoutData initialLayout = Layout.CurrentValue;
            var hand = new List<CardInstanceId>(initialLayout.Hand);
            if (hand.IndexOf(playedCardId) < 0)
                throw new InvalidOperationException("只有仍在手牌中的当前卡才能预演选择、抽牌与归宿。");
            if (!selectedCardId.HasValue)
            {
                foreach (CardInstanceId cardId in hand)
                {
                    if (cardId != playedCardId)
                    {
                        throw new InvalidOperationException(
                            "仍有另一张合法手牌时不能跳过唯一手牌选择。");
                    }
                }
            }

            var drawPile = new List<CardInstanceId>(initialLayout.DrawPile);
            var discardPile = new List<CardInstanceId>(initialLayout.DiscardPile);
            var exhaustPile = new List<CardInstanceId>(initialLayout.ExhaustPile);
            var powerPile = new List<CardInstanceId>(initialLayout.PowerPile);
            if (selectedCardId.HasValue)
            {
                int selectedIndex = hand.IndexOf(selectedCardId.Value);
                if (selectedIndex < 0)
                    throw new InvalidOperationException("只有当前手牌才能作为卡牌效果的选择。");

                hand.RemoveAt(selectedIndex);
                AddCardToDestination(
                    selectedCardId.Value,
                    selectedDestination,
                    discardPile,
                    exhaustPile,
                    powerPile);
            }

            int requestedCount = Math.Min(drawCount, Math.Max(0, handLimit - hand.Count));
            int drawableCount = Math.Min(requestedCount, drawPile.Count + discardPile.Count);
            bool willReshuffle = requestedCount > drawPile.Count && discardPile.Count > 0;
            long settlementCount = 1L + drawableCount + (selectedCardId.HasValue ? 1L : 0L);
            if (willReshuffle)
                settlementCount += discardPile.Count + 1L;
            ValidateSettlementOrderRange(startingOrder, settlementCount);

            uint randomStateBefore = _shuffleRandom.State;
            var candidateRandom = new GameRandom(randomStateBefore)
            {
                State = randomStateBefore,
            };
            var settlements = new List<BattleSettlementRecord>((int)settlementCount);
            if (selectedCardId.HasValue)
            {
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder,
                    selectedCardId.Value,
                    BattleCardZone.Hand,
                    selectedDestination));
            }

            int drawnCount = 0;
            while (drawnCount < requestedCount)
            {
                if (drawPile.Count == 0)
                {
                    foreach (CardInstanceId discardedCardId in discardPile)
                    {
                        settlements.Add(new BattleCardMovedSettlement(
                            startingOrder + settlements.Count,
                            discardedCardId,
                            BattleCardZone.DiscardPile,
                            BattleCardZone.DrawPile));
                    }

                    drawPile.AddRange(discardPile);
                    discardPile.Clear();
                    candidateRandom.Shuffle(drawPile);
                    if (drawPile.Count > 0)
                    {
                        settlements.Add(new BattleCardsReshuffledSettlement(
                            startingOrder + settlements.Count,
                            drawPile));
                    }
                }

                if (drawPile.Count == 0)
                    break;

                int topIndex = drawPile.Count - 1;
                CardInstanceId drawnCardId = drawPile[topIndex];
                drawPile.RemoveAt(topIndex);
                hand.Add(drawnCardId);
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder + settlements.Count,
                    drawnCardId,
                    BattleCardZone.DrawPile,
                    BattleCardZone.Hand));
                drawnCount++;
            }

            if (!hand.Remove(playedCardId))
                throw new InvalidOperationException("投影抽牌完成后当前卡牌意外离开手牌。");
            AddCardToDestination(
                playedCardId,
                playedCardDestination,
                discardPile,
                exhaustPile,
                powerPile);
            settlements.Add(new BattleCardMovedSettlement(
                startingOrder + settlements.Count,
                playedCardId,
                BattleCardZone.Hand,
                playedCardDestination));

            return new BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture(
                this,
                initialLayout,
                randomStateBefore,
                candidateRandom.State,
                new CardZoneLayoutData(drawPile, hand, discardPile, exhaustPile, powerPile),
                settlements);
        }

        /// <summary>纯只读确认选择抽牌归宿计划仍归属本聚合，且布局、随机与一次性状态均未漂移。</summary>
        internal bool ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(
            BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return ReferenceEquals(plan.Owner, this) &&
                !plan.IsCommitted &&
                ReferenceEquals(Layout.CurrentValue, plan.InitialLayout) &&
                _shuffleRandom.State == plan.ShuffleRandomStateBefore;
        }

        /// <summary>一次提交仍匹配权威快照的选择、投影抽牌与当前牌归宿计划，并只发布一次完整布局。</summary>
        internal BattleCardZoneOperationResult
            CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(
                BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他卡区聚合创建的选择抽牌归宿计划。");
            if (!ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(plan))
            {
                throw new InvalidOperationException(
                    "选择抽牌归宿计划已提交，或其布局与随机权威快照已经漂移。");
            }

            plan.MarkCommitted();
            _shuffleRandom.State = plan.ShuffleRandomStateAfter;
            _layout.Value = plan.NextLayout;
            return new BattleCardZoneOperationResult(
                succeeded: true,
                plan.Settlements);
        }

        /// <summary>在本地副本中冻结一张普通已打出卡的离手归宿，准备阶段不写入权威布局。</summary>
        internal BattlePreparedPlayedCardDeparture PreparePlayedCardDeparture(
            CardInstanceId cardId,
            BattleCardZone destination)
        {
            return PreparePlayedCardDeparture(cardId, BattleCardZone.Hand, destination);
        }

        /// <summary>从手牌或抽牌堆顶冻结指定卡牌的普通离场；用于 Queue 内部触发出牌时不经临时手牌。</summary>
        internal BattlePreparedPlayedCardDeparture PreparePlayedCardDeparture(
            CardInstanceId cardId,
            BattleCardZone source,
            BattleCardZone destination)
        {
            ValidatePlayedCardDestination(destination);

            CardZoneLayoutData initialLayout = Layout.CurrentValue;
            var hand = new List<CardInstanceId>(initialLayout.Hand);
            var drawPile = new List<CardInstanceId>(initialLayout.DrawPile);
            switch (source)
            {
                case BattleCardZone.Hand:
                    int cardIndex = hand.IndexOf(cardId);
                    if (cardIndex < 0)
                        throw new InvalidOperationException("只有仍在手牌中的当前卡才能预演普通离手。");
                    hand.RemoveAt(cardIndex);
                    break;
                case BattleCardZone.DrawPile:
                    if (drawPile.Count == 0 || drawPile[drawPile.Count - 1] != cardId)
                    {
                        throw new InvalidOperationException(
                            "触发出牌只能预演当前抽牌堆顶实例离场。");
                    }
                    drawPile.RemoveAt(drawPile.Count - 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }

            var discardPile = new List<CardInstanceId>(initialLayout.DiscardPile);
            var exhaustPile = new List<CardInstanceId>(initialLayout.ExhaustPile);
            var powerPile = new List<CardInstanceId>(initialLayout.PowerPile);
            AddCardToDestination(cardId, destination, discardPile, exhaustPile, powerPile);
            return new BattlePreparedPlayedCardDeparture(
                this,
                initialLayout,
                new CardZoneLayoutData(
                    drawPile,
                    hand,
                    discardPile,
                    exhaustPile,
                    powerPile),
                cardId,
                source,
                destination);
        }

        /// <summary>首次写入前唯一一次核对普通离手计划的归属、布局快照与一次性状态。</summary>
        internal bool ValidatePreparedPlayedCardDeparture(
            BattlePreparedPlayedCardDeparture plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能校验其他卡区聚合创建的普通离手计划。");

            bool succeeded = !plan.IsConsumed &&
                ReferenceEquals(Layout.CurrentValue, plan.InitialLayout);
            plan.MarkValidated(succeeded);
            return succeeded;
        }

        /// <summary>提交已校验的普通离手计划一次；不复验当前布局，只发布冻结下一布局与移动记录。</summary>
        internal BattleCardZoneOperationResult CommitPreparedPlayedCardDeparture(
            BattlePreparedPlayedCardDeparture plan,
            int startingOrder)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他卡区聚合创建的普通离手计划。");

            ValidateSettlementOrderRange(startingOrder, settlementCount: 1);
            var movement = new BattleCardMovedSettlement(
                startingOrder,
                plan.CardId,
                plan.FromZone,
                plan.ToZone);
            plan.MarkConsumed();
            _layout.Value = plan.NextLayout;
            return new BattleCardZoneOperationResult(
                succeeded: true,
                new BattleSettlementRecord[] { movement });
        }

        /// <summary>
        /// 在本地副本中先让当前牌逻辑离手，再只用原抽牌堆与原弃牌堆抽至手牌上限；
        /// 当前牌不会进入本次重洗候选，最终归宿、随机状态与全部结算均冻结在计划中。
        /// </summary>
        internal BattlePreparedPlayedCardDepartureAndDraw PreparePlayedCardDepartureAndDrawToHandLimit(
            CardInstanceId cardId,
            BattleCardZone destination,
            int handLimit,
            int startingOrder)
        {
            if (handLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(handLimit));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            ValidatePlayedCardDestination(destination);

            CardZoneLayoutData initialLayout = Layout.CurrentValue;
            var hand = new List<CardInstanceId>(initialLayout.Hand);
            int cardIndex = hand.IndexOf(cardId);
            if (cardIndex < 0)
                throw new InvalidOperationException("只有仍在手牌中的当前卡才能预演离手后抽牌。");

            hand.RemoveAt(cardIndex);
            var drawPile = new List<CardInstanceId>(initialLayout.DrawPile);
            var discardPile = new List<CardInstanceId>(initialLayout.DiscardPile);
            var exhaustPile = new List<CardInstanceId>(initialLayout.ExhaustPile);
            var powerPile = new List<CardInstanceId>(initialLayout.PowerPile);
            int requestedCount = Math.Max(0, handLimit - hand.Count);
            int drawableCount = Math.Min(requestedCount, drawPile.Count + discardPile.Count);
            bool willReshuffle = requestedCount > drawPile.Count && discardPile.Count > 0;
            long settlementCount = 1L + drawableCount;
            if (willReshuffle)
                settlementCount += discardPile.Count + 1L;
            ValidateSettlementOrderRange(startingOrder, settlementCount);

            uint randomStateBefore = _shuffleRandom.State;
            var candidateRandom = new GameRandom(randomStateBefore)
            {
                State = randomStateBefore,
            };
            var settlements = new List<BattleSettlementRecord>((int)settlementCount)
            {
                new BattleCardMovedSettlement(
                    startingOrder,
                    cardId,
                    BattleCardZone.Hand,
                    destination),
            };

            int drawnCount = 0;
            while (drawnCount < requestedCount)
            {
                if (drawPile.Count == 0)
                {
                    foreach (CardInstanceId discardedCardId in discardPile)
                    {
                        settlements.Add(new BattleCardMovedSettlement(
                            startingOrder + settlements.Count,
                            discardedCardId,
                            BattleCardZone.DiscardPile,
                            BattleCardZone.DrawPile));
                    }

                    drawPile.AddRange(discardPile);
                    discardPile.Clear();
                    candidateRandom.Shuffle(drawPile);
                    if (drawPile.Count > 0)
                    {
                        settlements.Add(new BattleCardsReshuffledSettlement(
                            startingOrder + settlements.Count,
                            drawPile));
                    }
                }

                if (drawPile.Count == 0)
                    break;

                int topIndex = drawPile.Count - 1;
                CardInstanceId drawnCardId = drawPile[topIndex];
                drawPile.RemoveAt(topIndex);
                hand.Add(drawnCardId);
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder + settlements.Count,
                    drawnCardId,
                    BattleCardZone.DrawPile,
                    BattleCardZone.Hand));
                drawnCount++;
            }

            AddCardToDestination(
                cardId,
                destination,
                discardPile,
                exhaustPile,
                powerPile);
            return new BattlePreparedPlayedCardDepartureAndDraw(
                this,
                initialLayout,
                randomStateBefore,
                candidateRandom.State,
                new CardZoneLayoutData(drawPile, hand, discardPile, exhaustPile, powerPile),
                settlements);
        }

        /// <summary>纯只读确认计划仍归属于本聚合，且布局、随机与一次性状态均未漂移。</summary>
        internal bool ValidatePreparedPlayedCardDepartureAndDraw(
            BattlePreparedPlayedCardDepartureAndDraw plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return ReferenceEquals(plan.Owner, this) &&
                !plan.IsCommitted &&
                ReferenceEquals(Layout.CurrentValue, plan.InitialLayout) &&
                _shuffleRandom.State == plan.ShuffleRandomStateBefore;
        }

        /// <summary>提交仍匹配权威快照的冻结计划一次；不再随机，并只发布一次完整卡区布局。</summary>
        internal BattleCardZoneOperationResult CommitPreparedPlayedCardDepartureAndDraw(
            BattlePreparedPlayedCardDepartureAndDraw plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他卡区聚合创建的离手抽牌计划。");
            if (!ValidatePreparedPlayedCardDepartureAndDraw(plan))
                throw new InvalidOperationException("离手抽牌计划已提交，或其布局与随机权威快照已经漂移。");

            plan.MarkCommitted();
            _shuffleRandom.State = plan.ShuffleRandomStateAfter;
            _layout.Value = plan.NextLayout;
            return new BattleCardZoneOperationResult(
                succeeded: true,
                plan.Settlements);
        }

        /// <summary>
        /// 冻结当前牌归宿、其余手牌原序弃置和等量临时牌创建；新实例身份、最终布局与记录顺序均在首次写入前确定。
        /// </summary>
        internal BattlePreparedPlayedCardDepartureDiscardHandAndCreate
            PreparePlayedCardDepartureDiscardHandAndCreate(
                CardInstanceId cardId,
                BattleCardZone destination,
                int createdTemplateId,
                int startingOrder)
        {
            if (createdTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(createdTemplateId));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            ValidatePlayedCardDestination(destination);

            CardZoneLayoutData initialLayout = Layout.CurrentValue;
            var discardedCards = new List<CardInstanceId>(initialLayout.Hand);
            int playedCardIndex = discardedCards.IndexOf(cardId);
            if (playedCardIndex < 0)
                throw new InvalidOperationException("只有仍在手牌中的当前卡才能预演离手弃牌与临时牌创建。");

            discardedCards.RemoveAt(playedCardIndex);
            int createdCount = discardedCards.Count;
            ValidateSettlementOrderRange(startingOrder, 1L + createdCount * 2L);

            int nextInstanceIdBefore = _nextInstanceId;
            long nextInstanceIdAfterValue = nextInstanceIdBefore + (long)createdCount;
            if (nextInstanceIdAfterValue > int.MaxValue)
                throw new InvalidOperationException("战斗卡牌实例标识不足以完成本次临时牌创建。");
            int nextInstanceIdAfter = (int)nextInstanceIdAfterValue;

            var createdCards = new List<CardInstanceData>(createdCount);
            var createdCardIds = new List<CardInstanceId>(createdCount);
            for (int index = 0; index < createdCount; index++)
            {
                var createdCardId = new CardInstanceId(nextInstanceIdBefore + index);
                createdCards.Add(new CardInstanceData(createdCardId, createdTemplateId));
                createdCardIds.Add(createdCardId);
            }

            var discardPile = new List<CardInstanceId>(initialLayout.DiscardPile);
            var exhaustPile = new List<CardInstanceId>(initialLayout.ExhaustPile);
            var powerPile = new List<CardInstanceId>(initialLayout.PowerPile);
            AddCardToDestination(
                cardId,
                destination,
                discardPile,
                exhaustPile,
                powerPile);
            discardPile.AddRange(discardedCards);

            var settlements = new List<BattleSettlementRecord>(1 + createdCount * 2)
            {
                new BattleCardMovedSettlement(
                    startingOrder,
                    cardId,
                    BattleCardZone.Hand,
                    destination),
            };
            foreach (CardInstanceId discardedCardId in discardedCards)
            {
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder + settlements.Count,
                    discardedCardId,
                    BattleCardZone.Hand,
                    BattleCardZone.DiscardPile));
            }
            foreach (CardInstanceData createdCard in createdCards)
            {
                settlements.Add(new BattleCardCreatedSettlement(
                    startingOrder + settlements.Count,
                    createdCard.Id,
                    createdCard.TemplateId,
                    BattleCardZone.Hand));
            }

            return new BattlePreparedPlayedCardDepartureDiscardHandAndCreate(
                this,
                initialLayout,
                _cards.Count,
                nextInstanceIdBefore,
                nextInstanceIdAfter,
                _shuffleRandom.State,
                new CardZoneLayoutData(
                    initialLayout.DrawPile,
                    createdCardIds,
                    discardPile,
                    exhaustPile,
                    powerPile),
                createdCards,
                settlements);
        }

        /// <summary>纯只读确认离手弃牌创建计划仍匹配聚合、布局、实例映射、分配器、洗牌随机与一次性状态。</summary>
        internal bool ValidatePreparedPlayedCardDepartureDiscardHandAndCreate(
            BattlePreparedPlayedCardDepartureDiscardHandAndCreate plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this) ||
                plan.IsCommitted ||
                !ReferenceEquals(Layout.CurrentValue, plan.InitialLayout) ||
                _cards.Count != plan.InitialCardCount ||
                _nextInstanceId != plan.NextInstanceIdBefore ||
                _shuffleRandom.State != plan.ShuffleRandomStateBefore ||
                plan.NextInstanceIdAfter - plan.NextInstanceIdBefore != plan.CreatedCards.Count)
            {
                return false;
            }

            for (int index = 0; index < plan.CreatedCards.Count; index++)
            {
                CardInstanceData createdCard = plan.CreatedCards[index];
                if (createdCard == null ||
                    createdCard.Id.Value != plan.NextInstanceIdBefore + index ||
                    _cards.ContainsKey(createdCard.Id))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>提交仍匹配完整权威快照的计划一次；加入全部冻结实例后只发布一次最终卡区布局。</summary>
        internal BattleCardZoneOperationResult CommitPreparedPlayedCardDepartureDiscardHandAndCreate(
            BattlePreparedPlayedCardDepartureDiscardHandAndCreate plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他卡区聚合创建的离手弃牌与临时牌计划。");
            if (!ValidatePreparedPlayedCardDepartureDiscardHandAndCreate(plan))
            {
                throw new InvalidOperationException(
                    "离手弃牌与临时牌计划已提交，或其权威快照已经漂移。");
            }

            plan.MarkCommitted();
            foreach (CardInstanceData createdCard in plan.CreatedCards)
                _cards.Add(createdCard.Id, createdCard);
            _nextInstanceId = plan.NextInstanceIdAfter;
            _layout.Value = plan.NextLayout;
            return new BattleCardZoneOperationResult(
                succeeded: true,
                plan.Settlements);
        }

        /// <summary>
        /// 将指定手牌移入弃牌堆；该牌不在手牌中时返回失败且不携带记录。
        /// </summary>
        public BattleCardZoneOperationResult DiscardFromHand(
            CardInstanceId cardId,
            int startingOrder = 0)
        {
            return MoveFromHand(
                cardId,
                BattleCardZone.DiscardPile,
                startingOrder: startingOrder);
        }

        /// <summary>
        /// 将指定手牌移入消耗区；该牌不在手牌中时返回失败且不携带记录。
        /// </summary>
        public BattleCardZoneOperationResult ExhaustFromHand(
            CardInstanceId cardId,
            int startingOrder = 0)
        {
            return MoveFromHand(
                cardId,
                BattleCardZone.ExhaustPile,
                startingOrder: startingOrder);
        }

        /// <summary>将指定手牌移入能力牌区，使其在本场持续生效且不再返回常规牌堆。</summary>
        public BattleCardZoneOperationResult MoveToPowerFromHand(
            CardInstanceId cardId,
            int startingOrder = 0)
        {
            return MoveFromHand(
                cardId,
                BattleCardZone.PowerPile,
                startingOrder: startingOrder);
        }

        /// <summary>
        /// 将全部手牌按权威手牌顺序移入弃牌堆，并返回每张牌的明确移动记录。
        /// </summary>
        public BattleCardZoneOperationResult DiscardHand(int startingOrder = 0)
        {
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            ValidateSettlementOrderRange(startingOrder, Hand.Count);
            if (Hand.Count == 0)
            {
                return new BattleCardZoneOperationResult(
                    succeeded: true,
                    Array.Empty<BattleSettlementRecord>());
            }

            var discardPile = new List<CardInstanceId>(DiscardPile);
            discardPile.AddRange(Hand);
            var settlements = new List<BattleSettlementRecord>(Hand.Count);
            foreach (CardInstanceId cardId in Hand)
            {
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder + settlements.Count,
                    cardId,
                    BattleCardZone.Hand,
                    BattleCardZone.DiscardPile));
            }

            Publish(DrawPile, Array.Empty<CardInstanceId>(), discardPile, ExhaustPile, PowerPile);
            return new BattleCardZoneOperationResult(succeeded: true, settlements);
        }

        /// <summary>保留指定仍在手中的实例，仅将其余手牌依原顺序移入弃牌堆。</summary>
        public BattleCardZoneOperationResult DiscardHandExcept(
            IEnumerable<CardInstanceId> retainedCardIds,
            int startingOrder = 0)
        {
            if (retainedCardIds == null)
                throw new ArgumentNullException(nameof(retainedCardIds));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));

            var retained = new HashSet<CardInstanceId>(retainedCardIds);
            var hand = new List<CardInstanceId>(Hand);
            foreach (CardInstanceId retainedCardId in retained)
            {
                if (!hand.Contains(retainedCardId))
                {
                    throw new ArgumentException(
                        "保留的卡牌实例必须仍在当前手牌中。",
                        nameof(retainedCardIds));
                }
            }

            var retainedInHand = new List<CardInstanceId>();
            var discarded = new List<CardInstanceId>();
            foreach (CardInstanceId cardId in hand)
            {
                if (retained.Contains(cardId))
                    retainedInHand.Add(cardId);
                else
                    discarded.Add(cardId);
            }

            ValidateSettlementOrderRange(startingOrder, discarded.Count);
            if (discarded.Count == 0)
            {
                return new BattleCardZoneOperationResult(
                    succeeded: true,
                    Array.Empty<BattleSettlementRecord>());
            }

            var discardPile = new List<CardInstanceId>(DiscardPile);
            discardPile.AddRange(discarded);
            var settlements = new List<BattleSettlementRecord>(discarded.Count);
            foreach (CardInstanceId cardId in discarded)
            {
                settlements.Add(new BattleCardMovedSettlement(
                    startingOrder + settlements.Count,
                    cardId,
                    BattleCardZone.Hand,
                    BattleCardZone.DiscardPile));
            }

            Publish(DrawPile, retainedInHand, discardPile, ExhaustPile, PowerPile);
            return new BattleCardZoneOperationResult(succeeded: true, settlements);
        }

        /// <summary>在同一战斗会话内创建临时卡并放入手牌，临时实例不会修改静态牌组定义。</summary>
        public IReadOnlyList<CardInstanceId> AddTemporaryToHand(int templateId, int count)
        {
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var hand = new List<CardInstanceId>(Hand);
            var created = new List<CardInstanceId>(count);
            for (int index = 0; index < count; index++)
            {
                CardInstanceId cardId = AllocateCardInstanceId();
                _cards.Add(cardId, new CardInstanceData(cardId, templateId));
                hand.Add(cardId);
                created.Add(cardId);
            }

            Publish(DrawPile, hand, DiscardPile, ExhaustPile, PowerPile);
            return new ReadOnlyCollection<CardInstanceId>(created);
        }

        /// <summary>
        /// 释放布局响应式资源；由所属战斗会话统一调用。
        /// </summary>
        public void Dispose()
        {
            Layout.Dispose();
            _layout.Dispose();
        }

        /// <summary>在本地副本完成手牌移出后，一次性发布新的完整布局。</summary>
        private BattleCardZoneOperationResult MoveFromHand(
            CardInstanceId cardId,
            BattleCardZone destination,
            int startingOrder)
        {
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (destination != BattleCardZone.DiscardPile &&
                destination != BattleCardZone.ExhaustPile &&
                destination != BattleCardZone.PowerPile)
            {
                throw new ArgumentOutOfRangeException(nameof(destination));
            }

            var hand = new List<CardInstanceId>(Hand);
            int cardIndex = hand.IndexOf(cardId);
            if (cardIndex < 0)
            {
                return new BattleCardZoneOperationResult(
                    succeeded: false,
                    Array.Empty<BattleSettlementRecord>());
            }

            ValidateSettlementOrderRange(startingOrder, settlementCount: 1);

            hand.RemoveAt(cardIndex);
            var discardPile = new List<CardInstanceId>(DiscardPile);
            var exhaustPile = new List<CardInstanceId>(ExhaustPile);
            var powerPile = new List<CardInstanceId>(PowerPile);
            switch (destination)
            {
                case BattleCardZone.DiscardPile:
                    discardPile.Add(cardId);
                    break;
                case BattleCardZone.ExhaustPile:
                    exhaustPile.Add(cardId);
                    break;
                case BattleCardZone.PowerPile:
                    powerPile.Add(cardId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(destination));
            }

            Publish(DrawPile, hand, discardPile, exhaustPile, powerPile);
            return new BattleCardZoneOperationResult(
                succeeded: true,
                new BattleSettlementRecord[]
                {
                    new BattleCardMovedSettlement(
                        startingOrder,
                        cardId,
                        BattleCardZone.Hand,
                        destination),
                });
        }

        /// <summary>校验已打出卡牌可进入的三个稳定归宿。</summary>
        private static void ValidatePlayedCardDestination(BattleCardZone destination)
        {
            if (destination != BattleCardZone.DiscardPile &&
                destination != BattleCardZone.ExhaustPile &&
                destination != BattleCardZone.PowerPile)
            {
                throw new ArgumentOutOfRangeException(nameof(destination));
            }
        }

        /// <summary>确认起手预演只发生在全副实例仍完整位于抽牌堆、其他卡区均为空的初始布局。</summary>
        private void ValidateInitialOpeningLayout(CardZoneLayoutData layout)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (layout.Hand.Count != 0 ||
                layout.DiscardPile.Count != 0 ||
                layout.ExhaustPile.Count != 0 ||
                layout.PowerPile.Count != 0 ||
                layout.DrawPile.Count != _cards.Count)
            {
                throw new InvalidOperationException("起手牌只能从尚未发牌的完整初始牌堆预演。");
            }

            var seen = new HashSet<CardInstanceId>();
            foreach (CardInstanceId cardId in layout.DrawPile)
            {
                if (!_cards.ContainsKey(cardId) || !seen.Add(cardId))
                    throw new InvalidOperationException("初始抽牌堆必须且只能包含当前会话的全部卡牌实例。");
            }
        }

        /// <summary>把已经从手牌移除的当前牌只加入最终布局的指定归宿，不让其参与本次重洗。</summary>
        private static void AddCardToDestination(
            CardInstanceId cardId,
            BattleCardZone destination,
            ICollection<CardInstanceId> discardPile,
            ICollection<CardInstanceId> exhaustPile,
            ICollection<CardInstanceId> powerPile)
        {
            switch (destination)
            {
                case BattleCardZone.DiscardPile:
                    discardPile.Add(cardId);
                    break;
                case BattleCardZone.ExhaustPile:
                    exhaustPile.Add(cardId);
                    break;
                case BattleCardZone.PowerPile:
                    powerPile.Add(cardId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(destination));
            }
        }

        /// <summary>将弃牌堆移入抽牌堆并使用本场专属随机流洗牌。</summary>
        private void ReshuffleDiscardPile(List<CardInstanceId> drawPile, List<CardInstanceId> discardPile)
        {
            if (discardPile.Count == 0)
                return;

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            _shuffleRandom.Shuffle(drawPile);
        }

        /// <summary>在任何布局或随机写入前确认本次记录序号不会超出 Int32。</summary>
        private static void ValidateSettlementOrderRange(
            int startingOrder,
            long settlementCount)
        {
            if (settlementCount < 0)
                throw new ArgumentOutOfRangeException(nameof(settlementCount));
            if (settlementCount > 0 &&
                startingOrder + settlementCount - 1L > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingOrder),
                    "卡区结算记录顺序超出 Int32 范围。");
            }
        }

        /// <summary>以四个完整区域创建新快照并作为唯一布局事实发布。</summary>
        private void Publish(
            IEnumerable<CardInstanceId> drawPile,
            IEnumerable<CardInstanceId> hand,
            IEnumerable<CardInstanceId> discardPile,
            IEnumerable<CardInstanceId> exhaustPile,
            IEnumerable<CardInstanceId> powerPile = null)
        {
            _layout.Value = new CardZoneLayoutData(
                drawPile,
                hand,
                discardPile,
                exhaustPile,
                powerPile ?? PowerPile);
        }

        /// <summary>分配一个未被本会话使用的正整数卡牌实例标识。</summary>
        private CardInstanceId AllocateCardInstanceId()
        {
            if (_nextInstanceId == int.MaxValue)
                throw new InvalidOperationException("战斗卡牌实例标识已耗尽。");

            return new CardInstanceId(_nextInstanceId++);
        }
    }
}
