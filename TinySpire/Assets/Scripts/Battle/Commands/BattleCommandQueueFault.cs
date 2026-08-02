using System;

namespace TinySpire.Battle
{
    /// <summary>不可作为普通规则失败继续 drain 的稳定 Queue fault 原因。</summary>
    public enum BattleCommandQueueFaultReason
    {
        MissingEnemyBehavior,
        MissingEffect,
        UnsupportedConfiguration,
        NoLegalNextIntent,
        MultipleLivingPlayers,
        PreparedInvariantViolation,
        UnexpectedException,
    }

    /// <summary>Queue 冻结时保留的不可变诊断事实。</summary>
    public sealed class BattleCommandQueueFaultData
    {
        /// <summary>发生 fault 的当前权威序号。</summary>
        public long AuthoritySequence { get; }

        /// <summary>发生 fault 的当前命令类型。</summary>
        public BattleCommandType CommandType { get; }

        /// <summary>稳定可分支的 fault 原因。</summary>
        public BattleCommandQueueFaultReason Reason { get; }

        /// <summary>fault 前是否可能已发生部分权威写入。</summary>
        public bool MayHavePartialWrites { get; }

        /// <summary>冻结一次 Queue fault；只有 Queue 调度核心可以创建。</summary>
        internal BattleCommandQueueFaultData(
            long authoritySequence,
            BattleCommandType commandType,
            BattleCommandQueueFaultReason reason,
            bool mayHavePartialWrites)
        {
            if (authoritySequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(authoritySequence));

            AuthoritySequence = authoritySequence;
            CommandType = commandType;
            Reason = reason;
            MayHavePartialWrites = mayHavePartialWrites;
        }
    }
}
