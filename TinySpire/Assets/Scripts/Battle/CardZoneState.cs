using System;
using System.Collections.Generic;
using TinySpire.Core;

namespace TinySpire.Battle
{
    public readonly struct CardInstanceId : IEquatable<CardInstanceId>
    {
        public int Value { get; }

        internal CardInstanceId(int value)
        {
            Value = value;
        }

        public bool Equals(CardInstanceId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CardInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(CardInstanceId left, CardInstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CardInstanceId left, CardInstanceId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// A unique card instance within one battle. TemplateId only references static card data.
    /// </summary>
    public sealed class CardInstanceState
    {
        public CardInstanceId Id { get; }
        public int TemplateId { get; }

        internal CardInstanceState(CardInstanceId id, int templateId)
        {
            Id = id;
            TemplateId = templateId;
        }
    }

    /// <summary>
    /// Owns every card instance and its single ordered battle zone.
    /// A card's zone is represented only by membership in one of the four lists.
    /// </summary>
    public sealed class CardZoneState
    {
        private readonly Dictionary<CardInstanceId, CardInstanceState> _cards;
        private readonly List<CardInstanceId> _drawPile;
        private readonly List<CardInstanceId> _hand;
        private readonly List<CardInstanceId> _discardPile;
        private readonly List<CardInstanceId> _exhaustPile;
        private readonly GameRandom _shuffleRandom;

        public event Action Changed;

        public IReadOnlyDictionary<CardInstanceId, CardInstanceState> Cards => _cards;
        public IReadOnlyList<CardInstanceId> DrawPile => _drawPile;
        public IReadOnlyList<CardInstanceId> Hand => _hand;
        public IReadOnlyList<CardInstanceId> DiscardPile => _discardPile;
        public IReadOnlyList<CardInstanceId> ExhaustPile => _exhaustPile;
        public uint ShuffleRandomState => _shuffleRandom.State;

        public CardZoneState(IEnumerable<int> cardTemplateIds, uint shuffleSeed)
        {
            if (cardTemplateIds == null)
                throw new ArgumentNullException(nameof(cardTemplateIds));

            _shuffleRandom = new GameRandom(shuffleSeed);
            _cards = new Dictionary<CardInstanceId, CardInstanceState>();
            _drawPile = new List<CardInstanceId>();
            _hand = new List<CardInstanceId>();
            _discardPile = new List<CardInstanceId>();
            _exhaustPile = new List<CardInstanceId>();

            int nextInstanceId = 1;
            foreach (int templateId in cardTemplateIds)
            {
                if (templateId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(cardTemplateIds));

                var cardId = new CardInstanceId(nextInstanceId++);
                _cards.Add(cardId, new CardInstanceState(cardId, templateId));
                _drawPile.Add(cardId);
            }

            _shuffleRandom.Shuffle(_drawPile);
        }

        public bool TryGetCard(CardInstanceId cardId, out CardInstanceState card)
        {
            return _cards.TryGetValue(cardId, out card);
        }

        public int Draw(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            int drawnCount = 0;
            while (drawnCount < count)
            {
                if (_drawPile.Count == 0)
                    ReshuffleDiscardPile();

                if (_drawPile.Count == 0)
                    break;

                int topIndex = _drawPile.Count - 1;
                CardInstanceId cardId = _drawPile[topIndex];
                _drawPile.RemoveAt(topIndex);
                _hand.Add(cardId);
                drawnCount++;
            }

            if (drawnCount > 0)
                Changed?.Invoke();

            return drawnCount;
        }

        public bool DiscardFromHand(CardInstanceId cardId)
        {
            return MoveFromHand(cardId, _discardPile);
        }

        public bool ExhaustFromHand(CardInstanceId cardId)
        {
            return MoveFromHand(cardId, _exhaustPile);
        }

        public int DiscardHand()
        {
            int discardedCount = _hand.Count;
            if (discardedCount == 0)
                return 0;

            _discardPile.AddRange(_hand);
            _hand.Clear();
            Changed?.Invoke();
            return discardedCount;
        }

        private bool MoveFromHand(CardInstanceId cardId, List<CardInstanceId> destination)
        {
            int cardIndex = _hand.IndexOf(cardId);
            if (cardIndex < 0)
                return false;

            _hand.RemoveAt(cardIndex);
            destination.Add(cardId);
            Changed?.Invoke();
            return true;
        }

        private void ReshuffleDiscardPile()
        {
            if (_discardPile.Count == 0)
                return;

            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            _shuffleRandom.Shuffle(_drawPile);
        }
    }
}
