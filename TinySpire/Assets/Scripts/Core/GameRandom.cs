using System;
using System.Collections.Generic;
using MathematicsRandom = Unity.Mathematics.Random;

namespace TinySpire.Core
{
    /// <summary>
    /// 由实例独占的、可复现的玩法随机流。
    /// 纯表现系统可继续独立使用 UnityEngine.Random。
    /// </summary>
    public sealed class GameRandom
    {
        private MathematicsRandom _random;

        /// <summary>
        /// 当前随机流状态。设置有效状态后，后续随机结果将从该状态继续。
        /// </summary>
        public uint State
        {
            get => _random.state;
            set
            {
                EnsureValidState(value, nameof(value));
                _random.state = value;
            }
        }

        /// <summary>
        /// 用非零种子创建一条独立的确定性随机流。
        /// </summary>
        public GameRandom(uint seed)
        {
            EnsureValidState(seed, nameof(seed));
            _random = new MathematicsRandom(seed);
        }

        /// <summary>
        /// 返回区间 [0, exclusiveMax) 内的下一个随机整数。
        /// </summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));

            return _random.NextInt(exclusiveMax);
        }

        /// <summary>
        /// 使用当前随机流原地执行 Fisher-Yates 洗牌。
        /// </summary>
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

        /// <summary>保证 Unity.Mathematics.Random 接收非零状态。</summary>
        private static void EnsureValidState(uint state, string parameterName)
        {
            if (state == 0)
                throw new ArgumentOutOfRangeException(parameterName, "Random state must be non-zero.");
        }
    }
}
