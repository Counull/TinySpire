using System;
using System.Security.Cryptography;

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
