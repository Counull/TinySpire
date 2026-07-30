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

        /// <summary>复制四个区域的顺序并冻结为一份完整布局。</summary>
        internal CardZoneLayoutData(
            IEnumerable<CardInstanceId> drawPile,
            IEnumerable<CardInstanceId> hand,
            IEnumerable<CardInstanceId> discardPile,
            IEnumerable<CardInstanceId> exhaustPile)
        {
            DrawPile = Freeze(drawPile);
            Hand = Freeze(hand);
            DiscardPile = Freeze(discardPile);
            ExhaustPile = Freeze(exhaustPile);
        }

        /// <summary>复制可枚举序列，防止已发布布局被外部集合后续修改。</summary>
        private static IReadOnlyList<CardInstanceId> Freeze(IEnumerable<CardInstanceId> cardIds)
        {
            if (cardIds == null)
                throw new ArgumentNullException(nameof(cardIds));

            return new ReadOnlyCollection<CardInstanceId>(new List<CardInstanceId>(cardIds));
        }
    }

    /// <summary>
    /// 持有全部卡牌实例，以及四个战斗卡区的唯一响应式布局事实。
    /// </summary>
    public sealed class BattleCardZonesData : IDisposable
    {
        private readonly Dictionary<CardInstanceId, CardInstanceData> _cards;
        private readonly GameRandom _shuffleRandom;
        private readonly ReactiveProperty<CardZoneLayoutData> _layout;

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

            int nextInstanceId = 1;
            foreach (int templateId in cardTemplateIds)
            {
                if (templateId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(cardTemplateIds));

                var cardId = new CardInstanceId(nextInstanceId++);
                _cards.Add(cardId, new CardInstanceData(cardId, templateId));
                drawPile.Add(cardId);
            }

            _shuffleRandom.Shuffle(drawPile);
            _layout = new ReactiveProperty<CardZoneLayoutData>(
                new CardZoneLayoutData(drawPile, Array.Empty<CardInstanceId>(), Array.Empty<CardInstanceId>(), Array.Empty<CardInstanceId>()));
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
        /// 返回实际抽到的数量。
        /// </summary>
        public int Draw(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var drawPile = new List<CardInstanceId>(DrawPile);
            var hand = new List<CardInstanceId>(Hand);
            var discardPile = new List<CardInstanceId>(DiscardPile);
            int drawnCount = 0;
            while (drawnCount < count)
            {
                if (drawPile.Count == 0)
                    ReshuffleDiscardPile(drawPile, discardPile);
                if (drawPile.Count == 0)
                    break;

                int topIndex = drawPile.Count - 1;
                CardInstanceId cardId = drawPile[topIndex];
                drawPile.RemoveAt(topIndex);
                hand.Add(cardId);
                drawnCount++;
            }

            if (drawnCount > 0)
                Publish(drawPile, hand, discardPile, ExhaustPile);

            return drawnCount;
        }

        /// <summary>
        /// 将指定手牌移入弃牌堆；该牌不在手牌中时返回 false。
        /// </summary>
        public bool DiscardFromHand(CardInstanceId cardId)
        {
            return MoveFromHand(cardId, toExhaustPile: false);
        }

        /// <summary>
        /// 将指定手牌移入消耗区；该牌不在手牌中时返回 false。
        /// </summary>
        public bool ExhaustFromHand(CardInstanceId cardId)
        {
            return MoveFromHand(cardId, toExhaustPile: true);
        }

        /// <summary>
        /// 将全部手牌移入弃牌堆，并返回实际弃掉的数量。
        /// </summary>
        public int DiscardHand()
        {
            if (Hand.Count == 0)
                return 0;

            var discardPile = new List<CardInstanceId>(DiscardPile);
            discardPile.AddRange(Hand);
            int discardedCount = Hand.Count;
            Publish(DrawPile, Array.Empty<CardInstanceId>(), discardPile, ExhaustPile);
            return discardedCount;
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
        private bool MoveFromHand(CardInstanceId cardId, bool toExhaustPile)
        {
            var hand = new List<CardInstanceId>(Hand);
            int cardIndex = hand.IndexOf(cardId);
            if (cardIndex < 0)
                return false;

            hand.RemoveAt(cardIndex);
            var discardPile = new List<CardInstanceId>(DiscardPile);
            var exhaustPile = new List<CardInstanceId>(ExhaustPile);
            if (toExhaustPile)
                exhaustPile.Add(cardId);
            else
                discardPile.Add(cardId);

            Publish(DrawPile, hand, discardPile, exhaustPile);
            return true;
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

        /// <summary>以四个完整区域创建新快照并作为唯一布局事实发布。</summary>
        private void Publish(
            IEnumerable<CardInstanceId> drawPile,
            IEnumerable<CardInstanceId> hand,
            IEnumerable<CardInstanceId> discardPile,
            IEnumerable<CardInstanceId> exhaustPile)
        {
            _layout.Value = new CardZoneLayoutData(drawPile, hand, discardPile, exhaustPile);
        }
    }
}
