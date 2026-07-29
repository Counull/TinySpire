using System;
using System.Collections.Generic;

/// <summary>
/// Minimal, UI-independent ownership of the current hand's placeholder card IDs.
/// </summary>
public sealed class HandState
{
    private readonly List<int> _cardIds;

    public event Action Changed;

    public IReadOnlyList<int> CardIds => _cardIds;

    public HandState(int initialCardCount)
    {
        int cardCount = Math.Max(0, initialCardCount);
        _cardIds = new List<int>(cardCount);
        for (int cardId = 0; cardId < cardCount; cardId++)
            _cardIds.Add(cardId);
    }

    public bool PlayCard(int cardId)
    {
        // TODO(DEP-002): Future cost deduction belongs in HandState or its successor aggregate, not in UI code.

        // TODO(DEP-001): Target detection depends on whether monster/player anchors are UGUI or World Space Sprites.
        int? targetId = null;

        if (targetId.HasValue)
            return false;

        if (!_cardIds.Remove(cardId))
            return false;

        Changed?.Invoke();
        return true;
    }

}
