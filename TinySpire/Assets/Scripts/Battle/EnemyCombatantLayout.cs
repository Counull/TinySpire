using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinySpire.Battle
{
    /// <summary>
    /// Calculates the stable local positions of M3A enemy views from encounter order.
    /// </summary>
    public static class EnemyCombatantLayout
    {
        /// <summary>The largest enemy count supported by the M3A battlefield layout.</summary>
        public const int MaximumEnemyCount = 3;

        /// <summary>
        /// Returns equal-spaced positions ordered from the rightmost enemy to the leftmost enemy.
        /// </summary>
        public static IReadOnlyList<Vector3> CalculateLocalPositions(int enemyCount, float spacing)
        {
            if (enemyCount < 1 || enemyCount > MaximumEnemyCount)
                throw new ArgumentOutOfRangeException(nameof(enemyCount));
            if (spacing <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spacing));

            var positions = new Vector3[enemyCount];
            float rightmostPosition = (enemyCount - 1) * spacing * 0.5f;
            for (int index = 0; index < enemyCount; index++)
                positions[index] = new Vector3(rightmostPosition - index * spacing, 0f, 0f);

            return Array.AsReadOnly(positions);
        }
    }
}
