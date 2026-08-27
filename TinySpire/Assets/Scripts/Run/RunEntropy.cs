using System;
using System.Security.Cryptography;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>创建一局新 Run 所需的一次性身份与根随机输入。</summary>
    public readonly struct RunEntropy
    {
        /// <summary>本局 Run 的唯一身份。</summary>
        public RunId RunId { get; }

        /// <summary>派生后续本战随机输入的非零根种子。</summary>
        public uint RandomRootSeed { get; }

        /// <summary>冻结并验证一组新 Run 随机输入。</summary>
        public RunEntropy(RunId runId, uint randomRootSeed)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run id cannot be empty.", nameof(runId));
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));

            RunId = runId;
            RandomRootSeed = randomRootSeed;
        }
    }

    /// <summary>为每局新 Run 提供一次不可预测且可在测试中替换的输入。</summary>
    public interface IRunEntropySource
    {
        /// <summary>签发下一局 Run 的唯一身份与非零根种子。</summary>
        RunEntropy Next();
    }

    /// <summary>从 Run 根输入隔离派生各规则随机域的稳定 seed。</summary>
    public static class RunRandomDomains
    {
        /// <summary>以固定 domain salt 派生非零地图 seed，不推进 Battle 随机序列。</summary>
        public static uint DeriveMapSeed(uint randomRootSeed)
        {
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));

            uint mixed = randomRootSeed;
            unchecked
            {
                mixed ^= 0x4D415000u;
                mixed ^= mixed >> 16;
                mixed *= 0x7FEB352Du;
                mixed ^= mixed >> 15;
                mixed *= 0x846CA68Bu;
                mixed ^= mixed >> 16;
            }

            return mixed == 0 ? 1u : mixed;
        }

        /// <summary>以固定 Reward domain salt 与 attempt 派生非零 seed，不推进 Map/Battle 随机序列。</summary>
        public static uint DeriveRewardSeed(uint randomRootSeed, int attemptSequence)
        {
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (attemptSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(attemptSequence));

            uint mixed = randomRootSeed;
            unchecked
            {
                mixed ^= 0x52574400u;
                mixed += (uint)attemptSequence * 0x9E3779B9u;
                mixed ^= mixed >> 16;
                mixed *= 0x7FEB352Du;
                mixed ^= mixed >> 15;
                mixed *= 0x846CA68Bu;
                mixed ^= mixed >> 16;
            }

            return mixed == 0 ? 1u : mixed;
        }

        /// <summary>从根种子与稳定节点身份派生 Shop 专属非零 seed。</summary>
        public static uint DeriveShopSeed(uint randomRootSeed, MapNodeId nodeId)
        {
            return DeriveNodeDomainSeed(randomRootSeed, nodeId, domainSalt: 0x53484F50u);
        }

        /// <summary>从根种子与稳定节点身份派生 Event 专属非零 seed。</summary>
        public static uint DeriveEventSeed(uint randomRootSeed, MapNodeId nodeId)
        {
            return DeriveNodeDomainSeed(randomRootSeed, nodeId, domainSalt: 0x45564E54u);
        }

        /// <summary>以稳定文本逐字符混合节点身份，避免依赖进程随机化哈希。</summary>
        private static uint DeriveNodeDomainSeed(
            uint randomRootSeed,
            MapNodeId nodeId,
            uint domainSalt)
        {
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (string.IsNullOrEmpty(nodeId.Value))
                throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));

            uint mixed = randomRootSeed;
            unchecked
            {
                mixed ^= domainSalt;
                foreach (char character in nodeId.Value)
                {
                    mixed ^= character;
                    mixed *= 0x01000193u;
                }
                mixed ^= mixed >> 16;
                mixed *= 0x7FEB352Du;
                mixed ^= mixed >> 15;
                mixed *= 0x846CA68Bu;
                mixed ^= mixed >> 16;
            }

            return mixed == 0 ? 1u : mixed;
        }
    }

    /// <summary>使用系统 Guid 与密码学随机源签发生产 Run 输入。</summary>
    public sealed class SystemRunEntropySource : IRunEntropySource
    {
        /// <summary>签发一组新的生产 Run 身份与非零根种子。</summary>
        public RunEntropy Next()
        {
            var bytes = new byte[sizeof(uint)];
            uint seed;
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                do
                {
                    generator.GetBytes(bytes);
                    seed = BitConverter.ToUInt32(bytes, 0);
                }
                while (seed == 0);
            }

            return new RunEntropy(new RunId(Guid.NewGuid()), seed);
        }
    }
}
