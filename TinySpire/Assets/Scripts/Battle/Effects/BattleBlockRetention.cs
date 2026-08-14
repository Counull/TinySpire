using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>格挡跨轮保留的不可变状态，永久来源与限时层数彼此独立。</summary>
    internal readonly struct BattleBlockRetentionSnapshot
    {
        internal bool IsPermanent { get; }
        internal int TimedRounds { get; }
        internal bool PreservesBlock => IsPermanent || TimedRounds > 0;

        /// <summary>冻结一次参与者的格挡保留事实。</summary>
        internal BattleBlockRetentionSnapshot(bool isPermanent, int timedRounds)
        {
            if (timedRounds < 0)
                throw new ArgumentOutOfRangeException(nameof(timedRounds));

            IsPermanent = isPermanent;
            TimedRounds = timedRounds;
        }
    }

    /// <summary>格挡保留计划的写入类别。</summary>
    internal enum BattleBlockRetentionPlanKind
    {
        GrantPermanent,
        GrantTimed,
        PlayerRoundStart,
    }

    /// <summary>格挡保留 module 在首次写入前冻结的一次性计划。</summary>
    internal sealed class BattlePreparedBlockRetentionPlan
    {
        internal BattleBlockRetention Owner { get; }
        internal CombatantId TargetId { get; }
        internal BattleBlockRetentionPlanKind Kind { get; }
        internal BattleBlockRetentionSnapshot Before { get; }
        internal BattleBlockRetentionSnapshot After { get; }
        internal bool ValidationAttempted { get; private set; }
        internal bool IsValidated { get; private set; }
        internal bool IsConsumed { get; private set; }

        /// <summary>冻结一次格挡保留状态变更。</summary>
        internal BattlePreparedBlockRetentionPlan(
            BattleBlockRetention owner,
            CombatantId targetId,
            BattleBlockRetentionPlanKind kind,
            BattleBlockRetentionSnapshot before,
            BattleBlockRetentionSnapshot after)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            TargetId = targetId;
            Kind = kind;
            Before = before;
            After = after;
        }

        /// <summary>记录唯一一次首次写入前校验。</summary>
        internal void MarkValidated(bool succeeded)
        {
            if (ValidationAttempted)
                throw new InvalidOperationException("格挡保留计划已经执行过校验。");

            ValidationAttempted = true;
            IsValidated = succeeded;
        }

        /// <summary>把已校验计划标记为一次性提交。</summary>
        internal void MarkConsumed()
        {
            if (!ValidationAttempted || !IsValidated)
                throw new InvalidOperationException("格挡保留计划尚未通过首次写入前校验。");
            if (IsConsumed)
                throw new InvalidOperationException("格挡保留计划已经提交。");

            IsConsumed = true;
        }
    }

    /// <summary>集中拥有永久与限时格挡保留事实，并提供 prepare/validate/commit 事务边界。</summary>
    internal sealed class BattleBlockRetention
    {
        private static readonly BattleBlockRetentionSnapshot Empty =
            new BattleBlockRetentionSnapshot(false, 0);

        private readonly Dictionary<CombatantId, BattleBlockRetentionSnapshot> _states =
            new Dictionary<CombatantId, BattleBlockRetentionSnapshot>();

        /// <summary>冻结赋予永久格挡保留的计划，重复赋予保持幂等。</summary>
        internal BattlePreparedBlockRetentionPlan PreparePermanent(CombatantId targetId)
        {
            BattleBlockRetentionSnapshot before = GetSnapshot(targetId);
            return new BattlePreparedBlockRetentionPlan(
                this,
                targetId,
                BattleBlockRetentionPlanKind.GrantPermanent,
                before,
                new BattleBlockRetentionSnapshot(true, before.TimedRounds));
        }

        /// <summary>冻结叠加限时格挡保留轮数的计划。</summary>
        internal BattlePreparedBlockRetentionPlan PrepareTimed(
            CombatantId targetId,
            int rounds)
        {
            if (rounds <= 0)
                throw new ArgumentOutOfRangeException(nameof(rounds));

            BattleBlockRetentionSnapshot before = GetSnapshot(targetId);
            return new BattlePreparedBlockRetentionPlan(
                this,
                targetId,
                BattleBlockRetentionPlanKind.GrantTimed,
                before,
                new BattleBlockRetentionSnapshot(
                    before.IsPermanent,
                    checked(before.TimedRounds + rounds)));
        }

        /// <summary>冻结玩家新一轮是否保留格挡以及限时层数衰减后的状态。</summary>
        internal BattlePreparedBlockRetentionPlan PreparePlayerRoundStart(
            CombatantId targetId)
        {
            BattleBlockRetentionSnapshot before = GetSnapshot(targetId);
            int timedAfter = before.TimedRounds > 0 ? before.TimedRounds - 1 : 0;
            return new BattlePreparedBlockRetentionPlan(
                this,
                targetId,
                BattleBlockRetentionPlanKind.PlayerRoundStart,
                before,
                new BattleBlockRetentionSnapshot(before.IsPermanent, timedAfter));
        }

        /// <summary>验证计划归属、一次性阶段与 module 权威事实未漂移。</summary>
        internal bool ValidatePrepared(BattlePreparedBlockRetentionPlan plan)
        {
            ValidateOwner(plan);
            bool succeeded = !plan.IsConsumed &&
                SnapshotsEqual(GetSnapshot(plan.TargetId), plan.Before);
            plan.MarkValidated(succeeded);
            return succeeded;
        }

        /// <summary>提交已验证的格挡保留计划，不在写入阶段重复读取外部事实。</summary>
        internal BattleBlockRetentionSnapshot CommitPrepared(
            BattlePreparedBlockRetentionPlan plan)
        {
            ValidateOwner(plan);
            plan.MarkConsumed();
            if (!plan.After.IsPermanent && plan.After.TimedRounds == 0)
                _states.Remove(plan.TargetId);
            else
                _states[plan.TargetId] = plan.After;

            return plan.After;
        }

        /// <summary>读取指定参与者当前不可变格挡保留事实。</summary>
        internal BattleBlockRetentionSnapshot GetSnapshot(CombatantId targetId)
        {
            return _states.TryGetValue(targetId, out BattleBlockRetentionSnapshot snapshot)
                ? snapshot
                : Empty;
        }

        /// <summary>校验计划属于当前 module。</summary>
        private void ValidateOwner(BattlePreparedBlockRetentionPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能处理其他格挡保留 module 创建的计划。");
        }

        /// <summary>比较两份不可变格挡保留快照。</summary>
        private static bool SnapshotsEqual(
            BattleBlockRetentionSnapshot left,
            BattleBlockRetentionSnapshot right)
        {
            return left.IsPermanent == right.IsPermanent &&
                   left.TimedRounds == right.TimedRounds;
        }
    }
}
