using System;
using System.Collections.Generic;
using MathematicsRandom = Unity.Mathematics.Random;

namespace TinySpire.Core
{
    /// <summary>
    /// An instance-owned deterministic random stream for gameplay rules.
    /// Cosmetic systems may continue to use UnityEngine.Random independently.
    /// </summary>
    public sealed class GameRandom
    {
        private MathematicsRandom _random;

        public uint State
        {
            get => _random.state;
            set
            {
                EnsureValidState(value, nameof(value));
                _random.state = value;
            }
        }

        public GameRandom(uint seed)
        {
            EnsureValidState(seed, nameof(seed));
            _random = new MathematicsRandom(seed);
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));

            return _random.NextInt(exclusiveMax);
        }

        public void Shuffle<T>(IList<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = NextInt(index + 1);
                if (swapIndex == index)
                    continue;

                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }

        private static void EnsureValidState(uint state, string parameterName)
        {
            if (state == 0)
                throw new ArgumentOutOfRangeException(parameterName, "Random state must be non-zero.");
        }
    }
}
