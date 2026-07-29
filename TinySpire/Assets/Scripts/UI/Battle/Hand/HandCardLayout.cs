using UnityEngine;

/// <summary>
/// Immutable tuning values for a hand's fan layout.
/// </summary>
public readonly struct HandCardLayoutSettings
{
    public readonly float BaseSpacing;
    public readonly float MaxAngleDegrees;
    public readonly float VerticalDrop;
    public readonly float MaxHandWidth;

    public HandCardLayoutSettings(float baseSpacing, float maxAngleDegrees, float verticalDrop, float maxHandWidth)
    {
        BaseSpacing = baseSpacing;
        MaxAngleDegrees = maxAngleDegrees;
        VerticalDrop = verticalDrop;
        MaxHandWidth = maxHandWidth;
    }
}

/// <summary>
/// A card's calculated baseline pose in the hand.
/// </summary>
public readonly struct HandCardPose
{
    public readonly Vector2 AnchoredPosition;
    public readonly float RotationDegrees;
    public readonly int SortingOrder;

    public HandCardPose(Vector2 anchoredPosition, float rotationDegrees, int sortingOrder)
    {
        AnchoredPosition = anchoredPosition;
        RotationDegrees = rotationDegrees;
        SortingOrder = sortingOrder;
    }
}

/// <summary>
/// Stateless fan-layout calculation for the BattleScene hand UI.
/// </summary>
public static class HandCardLayout
{
    public static HandCardPose[] Calculate(int cardCount, HandCardLayoutSettings settings)
    {
        if (cardCount <= 0)
            return System.Array.Empty<HandCardPose>();

        var poses = new HandCardPose[cardCount];
        if (cardCount == 1)
        {
            poses[0] = new HandCardPose(Vector2.zero, 0f, 0);
            return poses;
        }

        float spacing = Mathf.Min(settings.BaseSpacing, settings.MaxHandWidth / (cardCount - 1));
        float midpoint = (cardCount - 1) * 0.5f;

        for (int index = 0; index < cardCount; index++)
        {
            float t = (index - midpoint) / midpoint;
            float x = (index - midpoint) * spacing;
            float y = -settings.VerticalDrop * t * t;
            poses[index] = new HandCardPose(
                new Vector2(x, y),
                // Opposite signs make card axes converge toward a point below the hand.
                -t * settings.MaxAngleDegrees,
                index);
        }

        return poses;
    }
}
