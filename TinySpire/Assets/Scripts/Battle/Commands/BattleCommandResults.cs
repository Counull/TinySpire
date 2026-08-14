using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>单场战斗稳定结束后的公开结果类别。</summary>
    public enum BattleResultKind
    {
        Victory,
        Defeat,
    }

    /// <summary>终局时冻结的单个玩家结算后生命事实。</summary>
    public sealed class BattleResultPlayerSnapshot
    {
        /// <summary>该玩家在本场战斗内的参与者标识。</summary>
        public CombatantId CombatantId { get; }

        /// <summary>该玩家对应的静态 Hero 模板标识。</summary>
        public int TemplateId { get; }

        /// <summary>终局 settlement 与 continuation 冻结后的当前生命。</summary>
        public int Health { get; }

        /// <summary>本场战斗期间不变的生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>由冻结生命派生的存活状态，不建立第二份事实。</summary>
        public bool IsAlive => Health > 0;

        /// <summary>冻结一个玩家的结算后生命事实。</summary>
        internal BattleResultPlayerSnapshot(
            CombatantId combatantId,
            int templateId,
            int health,
            int maxHealth)
        {
            if (combatantId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(combatantId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (health < 0 || health > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(health));

            CombatantId = combatantId;
            TemplateId = templateId;
            Health = health;
            MaxHealth = maxHealth;
        }
    }

    /// <summary>由权威命令队列冻结并在终局表现完成后发布一次的战斗结果。</summary>
    public sealed class BattleResult
    {
        /// <summary>本场战斗的胜负结果。</summary>
        public BattleResultKind Kind { get; }

        /// <summary>产生本结果的终局命令权威序号。</summary>
        public long AuthoritySequence { get; }

        /// <summary>终局回合快照中的轮次号。</summary>
        public int RoundNumber { get; }

        /// <summary>按 CombatantId 稳定排序的全部玩家结算后生命事实。</summary>
        public IReadOnlyList<BattleResultPlayerSnapshot> Players { get; }

        /// <summary>冻结一次与终局命令和回合对应的公开结果。</summary>
        internal BattleResult(
            BattleResultKind kind,
            long authoritySequence,
            int roundNumber,
            IReadOnlyList<BattleResultPlayerSnapshot> players)
        {
            if (!Enum.IsDefined(typeof(BattleResultKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (authoritySequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(authoritySequence));
            if (roundNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(roundNumber));
            if (players == null)
                throw new ArgumentNullException(nameof(players));
            if (players.Count == 0)
                throw new ArgumentException("战斗结果必须包含至少一个玩家结算快照。", nameof(players));

            var playerCopies = new List<BattleResultPlayerSnapshot>(players.Count);
            var playerIds = new HashSet<CombatantId>();
            for (int index = 0; index < players.Count; index++)
            {
                BattleResultPlayerSnapshot player = players[index];
                if (player == null)
                    throw new ArgumentException("玩家结算快照不得包含空项。", nameof(players));
                if (!playerIds.Add(player.CombatantId))
                    throw new ArgumentException("玩家结算快照不得包含重复参与者。", nameof(players));

                playerCopies.Add(player);
            }

            Kind = kind;
            AuthoritySequence = authoritySequence;
            RoundNumber = roundNumber;
            Players = new ReadOnlyCollection<BattleResultPlayerSnapshot>(playerCopies);
        }
    }

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

        /// <summary>本次命令若首次进入终局则携带同一份冻结结果，否则为空。</summary>
        public BattleResult BattleResult { get; }

        /// <summary>创建一份不可变的权威执行结果。</summary>
        internal BattleCommandExecutionResult(
            long authoritySequence,
            BattleCommandType commandType,
            CombatantId? submitterId,
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements,
            BattleResult battleResult = null)
        {
            if (authoritySequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(authoritySequence));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (battleResult != null &&
                (failureReason != BattleCommandExecutionFailureReason.None ||
                 battleResult.AuthoritySequence != authoritySequence))
            {
                throw new ArgumentException(
                    "战斗结果只能附着到同一权威序号的成功终局执行。",
                    nameof(battleResult));
            }

            AuthoritySequence = authoritySequence;
            CommandType = commandType;
            SubmitterId = submitterId;
            FailureReason = failureReason;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
            BattleResult = battleResult;
        }
    }
}
