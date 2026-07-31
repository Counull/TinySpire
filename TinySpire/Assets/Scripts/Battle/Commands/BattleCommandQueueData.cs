namespace TinySpire.Battle
{
    /// <summary>
    /// 统一权威命令顺序的只读事实快照。
    /// </summary>
    public sealed class BattleCommandQueueData
    {
        /// <summary>当前命令的权威序号；空闲时为空。</summary>
        public long? CurrentAuthoritySequence { get; }

        /// <summary>当前命令的类型；空闲时为空。</summary>
        public BattleCommandType? CurrentCommandType { get; }

        /// <summary>当前命令的提交者；系统命令或空闲时为空。</summary>
        public CombatantId? CurrentSubmitterId { get; }

        /// <summary>已确认但尚未成为当前命令的数量。</summary>
        public int PendingCount { get; }

        /// <summary>当前命令是否正在等待表现确认完成。</summary>
        public bool IsWaitingForPresentation { get; }

        /// <summary>创建一份不可变的队列事实快照。</summary>
        internal BattleCommandQueueData(
            long? currentAuthoritySequence,
            BattleCommandType? currentCommandType,
            CombatantId? currentSubmitterId,
            int pendingCount,
            bool isWaitingForPresentation)
        {
            CurrentAuthoritySequence = currentAuthoritySequence;
            CurrentCommandType = currentCommandType;
            CurrentSubmitterId = currentSubmitterId;
            PendingCount = pendingCount;
            IsWaitingForPresentation = isWaitingForPresentation;
        }

        /// <summary>创建未持有任何命令的初始快照。</summary>
        internal static BattleCommandQueueData Empty()
        {
            return new BattleCommandQueueData(null, null, null, 0, false);
        }
    }
}
