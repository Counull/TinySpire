using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>M8 明确支持的四个 Block/Vulnerable 规则时点。</summary>
    internal enum BattleStatusTimingPoint
    {
        PlayerRoundStart,
        PlayerActionEnded,
        EnemyActionStarted,
        EnemyActionCompleted,
    }

    /// <summary>一次状态时机执行的不可变成功或普通失败结果。</summary>
    internal sealed class BattleStatusTimingResult
    {
        /// <summary>状态时机读取与提交是否成功。</summary>
        public bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>找不到目标时的普通失败；成功时为 None。</summary>
        public BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>按实际写入顺序冻结的非零状态结算。</summary>
        public IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>冻结一次状态时机结果。</summary>
        internal BattleStatusTimingResult(
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            FailureReason = failureReason;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }
    }

    /// <summary>完整预构建的一次内部状态时机计划。</summary>
    internal sealed class BattlePreparedStatusTimingPlan
    {
        /// <summary>创建计划的唯一 module 实例。</summary>
        internal BattleStatusTiming Owner { get; }

        /// <summary>计划作用的参与者。</summary>
        internal CombatantId CombatantId { get; }

        /// <summary>计划对应的唯一状态时点。</summary>
        internal BattleStatusTimingPoint TimingPoint { get; }

        /// <summary>预构建时的初始四标量快照。</summary>
        internal BattleCombatantScalarSnapshot InitialSnapshot { get; }

        /// <summary>合法提交后的 Block。</summary>
        internal int BlockAfter { get; }

        /// <summary>合法提交后的 Vulnerable。</summary>
        internal int VulnerableAfter { get; }

        /// <summary>计划是否包含真实状态写入。</summary>
        internal bool HasWrite { get; }

        /// <summary>按真实状态写入顺序冻结的记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>玩家新一轮时与标量计划共同冻结的格挡保留计划。</summary>
        internal BattlePreparedBlockRetentionPlan BlockRetentionPlan { get; }

        /// <summary>计划是否已经提交，防止重复写入。</summary>
        internal bool IsConsumed { get; private set; }

        /// <summary>计划是否已经执行过唯一校验尝试。</summary>
        internal bool ValidationAttempted { get; private set; }

        /// <summary>计划是否已经通过首次写入前校验。</summary>
        internal bool IsValidated { get; private set; }

        /// <summary>冻结一次状态时机的初始快照、投影值与结算。</summary>
        internal BattlePreparedStatusTimingPlan(
            BattleStatusTiming owner,
            CombatantData combatant,
            BattleStatusTimingPoint timingPoint,
            int blockAfter,
            int vulnerableAfter,
            bool hasWrite,
            IEnumerable<BattleSettlementRecord> settlements,
            BattlePreparedBlockRetentionPlan blockRetentionPlan)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            CombatantId = combatant.Id;
            TimingPoint = timingPoint;
            InitialSnapshot = new BattleCombatantScalarSnapshot(combatant);
            BlockAfter = blockAfter;
            VulnerableAfter = vulnerableAfter;
            HasWrite = hasWrite;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
            BlockRetentionPlan = blockRetentionPlan;
        }

        /// <summary>记录唯一校验结果；失败后也禁止重复校验。</summary>
        internal void MarkValidated(bool succeeded)
        {
            if (ValidationAttempted)
                throw new InvalidOperationException("状态时机计划已经执行过校验。");

            ValidationAttempted = true;
            IsValidated = succeeded;
        }

        /// <summary>在唯一 commit 时标记计划已消费。</summary>
        internal void MarkConsumed()
        {
            if (!ValidationAttempted || !IsValidated)
                throw new InvalidOperationException("状态时机计划尚未通过首次写入前校验。");
            if (IsConsumed)
                throw new InvalidOperationException("状态时机计划已经提交。");

            IsConsumed = true;
        }
    }

    /// <summary>状态时机内部预构建的成功或普通失败结果。</summary>
    internal readonly struct BattleStatusTimingPreparationResult
    {
        /// <summary>预构建是否成功。</summary>
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>预构建普通失败原因。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>成功时的完整状态时机计划。</summary>
        internal BattlePreparedStatusTimingPlan Plan { get; }

        /// <summary>冻结一次状态时机预构建结果。</summary>
        internal BattleStatusTimingPreparationResult(
            BattleCommandExecutionFailureReason failureReason,
            BattlePreparedStatusTimingPlan plan)
        {
            FailureReason = failureReason;
            Plan = plan;
        }
    }

    /// <summary>独占 M8 Block 清理与 Vulnerable 衰减写入口的具体 module。</summary>
    internal sealed class BattleStatusTiming
    {
        private static readonly IReadOnlyList<BattleSettlementRecord> NoSettlements =
            Array.Empty<BattleSettlementRecord>();

        private readonly BattleCombatantsData _combatants;
        private readonly BattleBlockRetention _blockRetention;

        /// <summary>绑定本场唯一参与者聚合。</summary>
        public BattleStatusTiming(BattleCombatantsData combatants)
            : this(combatants, new BattleBlockRetention())
        {
        }

        /// <summary>绑定参与者聚合与本场唯一格挡保留 module。</summary>
        internal BattleStatusTiming(
            BattleCombatantsData combatants,
            BattleBlockRetention blockRetention)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _blockRetention = blockRetention ?? throw new ArgumentNullException(nameof(blockRetention));
        }

        /// <summary>在不可变标量上投影指定时点；死亡或零值输入保持不变。</summary>
        public static BattleEffectTargetSnapshot Project(
            BattleStatusTimingPoint timingPoint,
            BattleEffectTargetSnapshot snapshot)
        {
            if (!Enum.IsDefined(typeof(BattleStatusTimingPoint), timingPoint))
                throw new ArgumentOutOfRangeException(nameof(timingPoint));
            if (snapshot.Health == 0)
                return snapshot;

            bool clearsBlock = timingPoint == BattleStatusTimingPoint.PlayerRoundStart ||
                               timingPoint == BattleStatusTimingPoint.EnemyActionStarted;
            bool reducesVulnerable = timingPoint == BattleStatusTimingPoint.PlayerActionEnded ||
                                     timingPoint == BattleStatusTimingPoint.EnemyActionCompleted;
            int blockAfter = clearsBlock ? 0 : snapshot.Block;
            int vulnerableAfter = reducesVulnerable && snapshot.Vulnerable > 0
                ? snapshot.Vulnerable - 1
                : snapshot.Vulnerable;
            return new BattleEffectTargetSnapshot(
                snapshot.Health,
                blockAfter,
                vulnerableAfter);
        }

        /// <summary>在独立调用中完成 prepare、单次 validate 与无普通失败 commit。</summary>
        public BattleStatusTimingResult Execute(
            BattleStatusTimingPoint timingPoint,
            CombatantId combatantId,
            int startingOrder)
        {
            BattleStatusTimingPreparationResult preparation = Prepare(
                timingPoint,
                combatantId,
                startingOrder);
            if (!preparation.Succeeded)
            {
                return new BattleStatusTimingResult(
                    preparation.FailureReason,
                    NoSettlements);
            }
            if (!ValidatePrepared(preparation.Plan))
                throw new InvalidOperationException("状态时机计划在首次写入前发生快照漂移。");

            return CommitPrepared(preparation.Plan);
        }

        /// <summary>从当前权威快照预构建一次 Block 清理或 Vulnerable 衰减，过程零写入。</summary>
        internal BattleStatusTimingPreparationResult Prepare(
            BattleStatusTimingPoint timingPoint,
            CombatantId combatantId,
            int startingOrder)
        {
            return PrepareCore(
                timingPoint,
                combatantId,
                projectedBefore: null,
                startingOrder);
        }

        /// <summary>以联合事务投影事实预构建状态时机，同时冻结真实初始标量供唯一校验。</summary>
        internal BattleStatusTimingPreparationResult PrepareProjected(
            BattleStatusTimingPoint timingPoint,
            CombatantId combatantId,
            BattleEffectTargetSnapshot projectedBefore,
            int startingOrder)
        {
            return PrepareCore(
                timingPoint,
                combatantId,
                projectedBefore,
                startingOrder);
        }

        /// <summary>从实际或投影输入构建只修改当前时点所拥有标量的冻结计划。</summary>
        private BattleStatusTimingPreparationResult PrepareCore(
            BattleStatusTimingPoint timingPoint,
            CombatantId combatantId,
            BattleEffectTargetSnapshot? projectedBefore,
            int startingOrder)
        {
            if (!Enum.IsDefined(typeof(BattleStatusTimingPoint), timingPoint))
                throw new ArgumentOutOfRangeException(nameof(timingPoint));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!_combatants.TryGet(combatantId, out CombatantData combatant))
            {
                return new BattleStatusTimingPreparationResult(
                    BattleCommandExecutionFailureReason.TargetNotFound,
                    plan: null);
            }

            BattleEffectTargetSnapshot initial = projectedBefore ??
                new BattleEffectTargetSnapshot(
                    combatant.CurrentHealth,
                    combatant.CurrentBlock,
                    combatant.CurrentVulnerable);
            BattlePreparedBlockRetentionPlan blockRetentionPlan =
                timingPoint == BattleStatusTimingPoint.PlayerRoundStart
                    ? _blockRetention.PreparePlayerRoundStart(combatantId)
                    : null;
            BattleEffectTargetSnapshot projected = Project(timingPoint, initial);
            if (blockRetentionPlan != null &&
                blockRetentionPlan.Before.PreservesBlock)
            {
                projected = new BattleEffectTargetSnapshot(
                    projected.Health,
                    initial.Block,
                    projected.Vulnerable);
            }
            BattleSettlementRecord settlement = null;
            if (projected.Block != initial.Block)
            {
                settlement = new BattleBlockClearedSettlement(
                    startingOrder,
                    combatant.Id,
                    initial.Block);
            }
            else if (projected.Vulnerable != initial.Vulnerable)
            {
                settlement = new BattleStatusReducedSettlement(
                    startingOrder,
                    combatant.Id,
                    BattleStatusType.Vulnerable,
                    initial.Vulnerable,
                    projected.Vulnerable);
            }
            else if (blockRetentionPlan != null &&
                     blockRetentionPlan.Before.TimedRounds !=
                     blockRetentionPlan.After.TimedRounds)
            {
                settlement = new BattleStatusReducedSettlement(
                    startingOrder,
                    combatant.Id,
                    BattleStatusType.Garrison,
                    blockRetentionPlan.Before.TimedRounds,
                    blockRetentionPlan.After.TimedRounds);
            }

            return new BattleStatusTimingPreparationResult(
                BattleCommandExecutionFailureReason.None,
                new BattlePreparedStatusTimingPlan(
                    this,
                    combatant,
                    timingPoint,
                    projected.Block,
                    projected.Vulnerable,
                    hasWrite: settlement != null,
                    settlement == null
                        ? NoSettlements
                        : new[] { settlement },
                    blockRetentionPlan));
        }

        /// <summary>只在联合事务首次写入前校验计划归属、未消费与初始四标量快照。</summary>
        internal bool ValidatePrepared(BattlePreparedStatusTimingPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能校验其他状态时机 module 创建的计划。");

            bool succeeded = !plan.IsConsumed &&
                _combatants.TryGet(plan.CombatantId, out CombatantData combatant) &&
                plan.InitialSnapshot.Matches(combatant);
            if (succeeded && plan.BlockRetentionPlan != null)
                succeeded = _blockRetention.ValidatePrepared(plan.BlockRetentionPlan);
            plan.MarkValidated(succeeded);
            return succeeded;
        }

        /// <summary>提交已经联合验证的计划；不再复验中间快照，也不返回普通失败。</summary>
        internal BattleStatusTimingResult CommitPrepared(BattlePreparedStatusTimingPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他状态时机 module 创建的计划。");

            plan.MarkConsumed();
            if (plan.BlockRetentionPlan != null)
                _blockRetention.CommitPrepared(plan.BlockRetentionPlan);
            if (plan.HasWrite)
            {
                if (!_combatants.TryGet(plan.CombatantId, out CombatantData combatant))
                    throw new InvalidOperationException("已验证的状态时机目标不再存在。");

                bool clearsBlock = plan.TimingPoint == BattleStatusTimingPoint.PlayerRoundStart ||
                                   plan.TimingPoint == BattleStatusTimingPoint.EnemyActionStarted;
                if (clearsBlock)
                {
                    combatant.ApplyStatusTimingValues(
                        plan.BlockAfter,
                        combatant.CurrentVulnerable);
                }
                else
                {
                    combatant.ApplyStatusTimingValues(
                        combatant.CurrentBlock,
                        plan.VulnerableAfter);
                }
            }

            return new BattleStatusTimingResult(
                BattleCommandExecutionFailureReason.None,
                plan.Settlements);
        }
    }
}
