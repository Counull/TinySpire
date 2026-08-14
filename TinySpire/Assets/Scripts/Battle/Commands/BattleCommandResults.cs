using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>
    /// 命令未进入权威顺序时的结构性拒绝原因。
    /// </summary>
    public enum BattleCommandSubmissionFailureReason
    {
        None,
        BattleNotStarted,
        InvalidSubmissionHandle,
        SystemCommandNotAuthorized,
        BattleAlreadyEnded,
        QueueFaulted,
    }

    /// <summary>
    /// 命令提交结果；接受只表示已经进入权威排序。
    /// </summary>
    public readonly struct BattleCommandSubmissionResult
    {
        /// <summary>命令是否已经进入权威排序。</summary>
        public bool Accepted { get; }

        /// <summary>接受时分配的权威序号；拒绝时为空。</summary>
        public long? AuthoritySequence { get; }

        /// <summary>拒绝原因；接受时为 None。</summary>
        public BattleCommandSubmissionFailureReason FailureReason { get; }

        /// <summary>创建一份不可变提交结果。</summary>
        private BattleCommandSubmissionResult(
            bool accepted,
            long? authoritySequence,
            BattleCommandSubmissionFailureReason failureReason)
        {
            Accepted = accepted;
            AuthoritySequence = authoritySequence;
            FailureReason = failureReason;
        }

        /// <summary>创建未进入队列的拒绝结果。</summary>
        internal static BattleCommandSubmissionResult Rejected(BattleCommandSubmissionFailureReason failureReason)
        {
            return new BattleCommandSubmissionResult(false, null, failureReason);
        }

        /// <summary>创建已经进入权威顺序的接受结果。</summary>
        internal static BattleCommandSubmissionResult AcceptedWith(long authoritySequence)
        {
            return new BattleCommandSubmissionResult(true, authoritySequence, BattleCommandSubmissionFailureReason.None);
        }
    }

    /// <summary>
    /// 命令到达队首后未能完成权威写入的明确原因。
    /// </summary>
    public enum BattleCommandExecutionFailureReason
    {
        None,
        BattleAlreadyStarted,
        InvalidTurnPhase,
        PlayerActionWindowExpired,
        InvalidPlayer,
        PlayerNotAlive,
        PlayerActionAlreadyEnded,
        PlayerCardZonesNotFound,
        CardNotInHand,
        CardTemplateNotFound,
        InsufficientEnergy,
        InvalidEnemy,
        EnemyNotCurrentActor,
        UnsupportedCommand,
        BattleAlreadyEnded,
        TargetRequired,
        TargetNotFound,
        TargetNotAlive,
        TargetRuleMismatch,
        UnsupportedTargetRule,
        EffectSourceNotFound,
        EffectSourceNotAlive,
        InvalidEffectBinding,
        EffectTemplateNotFound,
        UnsupportedEffectType,
        UnsupportedEffectAttribute,
        EffectValueOverflow,
        CardNotImplemented,
        InsufficientAmmo,
        AttackBlockedByShackle,
        MachineGunnerRuntimeUnavailable,
        UnsupportedMachineGunnerProgram,
        InvalidOpeningHandConfiguration,
        CardSelectionRequired,
        InvalidCardSelectionCount,
        SelectedCardNotEligible,
        SelectedCardNotInHand,
    }

    /// <summary>
    /// 命令到达队首后形成的权威执行结果。
    /// </summary>
    public sealed class BattleCommandExecutionResult
    {
        /// <summary>该次执行对应的权威序号。</summary>
        public long AuthoritySequence { get; }

        /// <summary>该次执行对应的命令类型。</summary>
        public BattleCommandType CommandType { get; }

        /// <summary>该次执行命令的提交者。</summary>
        public CombatantId? SubmitterId { get; }

        /// <summary>执行期校验和权威写入是否成功。</summary>
        public bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>执行失败原因；成功时为 None。</summary>
        public BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>本次命令按发生顺序冻结的只读结算记录。</summary>
        public IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>创建一份不可变的权威执行结果。</summary>
        internal BattleCommandExecutionResult(
            long authoritySequence,
            BattleCommandType commandType,
            CombatantId? submitterId,
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            AuthoritySequence = authoritySequence;
            CommandType = commandType;
            SubmitterId = submitterId;
            FailureReason = failureReason;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }
    }
}
