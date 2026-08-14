using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>重复伤害计划选择目标时支持的稳定策略。</summary>
    internal enum BattleRepeatedDamageTargetPolicy
    {
        /// <summary>全部伤害段锁定同一个显式敌人，目标死亡后停止剩余段。</summary>
        FixedEnemy,

        /// <summary>每段伤害都从当时投影仍存活的敌人中重新随机选择。</summary>
        RandomLivingEnemyPerHit,
    }

    /// <summary>一段重复伤害在解析配置后交给共享执行器的不可变输入。</summary>
    internal readonly struct BattleRepeatedDamageHitRequest
    {
        /// <summary>生成结算记录时保留的配置 Effect 标识。</summary>
        internal BattleEffectId? EffectId { get; }

        /// <summary>本段伤害公式使用的配置基础值。</summary>
        internal int ConfiguredValue { get; }

        /// <summary>冻结一段已由普通卡牌 Effect grammar 验证的伤害输入。</summary>
        internal BattleRepeatedDamageHitRequest(
            BattleEffectId? effectId,
            int configuredValue)
        {
            if (effectId.HasValue && effectId.Value.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(effectId));

            EffectId = effectId;
            ConfiguredValue = configuredValue;
        }
    }

    /// <summary>一段重复伤害连同职业后效完成预演后交回共享规划器的冻结结果。</summary>
    internal readonly struct BattleRepeatedDamageHitPreparation
    {
        /// <summary>本段来源伤害自身的冻结结果，供共享计划保留可观测事实。</summary>
        internal BattleDamageFormulaOutcome PrimaryOutcome { get; }

        /// <summary>来源伤害及其全部紧邻后效结束后的目标标量投影。</summary>
        internal BattleEffectTargetSnapshot ProjectedTargetAfterHit { get; }

        /// <summary>冻结一段来源伤害及其后效得到的完整投影。</summary>
        internal BattleRepeatedDamageHitPreparation(
            BattleDamageFormulaOutcome primaryOutcome,
            BattleEffectTargetSnapshot projectedTargetAfterHit)
        {
            PrimaryOutcome = primaryOutcome;
            ProjectedTargetAfterHit = projectedTargetAfterHit;
        }
    }

    /// <summary>为共享选目标规划器封装一条具体伤害管线的逐段预演、校验与提交。</summary>
    internal interface IBattleRepeatedDamageHitSequence
    {
        /// <summary>返回全部已冻结段提交时产生的 settlement 总数。</summary>
        int PlannedSettlementCount { get; }

        /// <summary>只读预演一段来源伤害及其紧邻后效，并推进序列私有投影。</summary>
        BattleRepeatedDamageHitPreparation PrepareHit(
            CombatantData source,
            BattleRepeatedDamageHitRequest hit,
            CombatantId targetId,
            BattleEffectTargetSnapshot projectedTarget);

        /// <summary>在任何战斗写入前校验序列归属、私有快照及冻结段顺序。</summary>
        void ValidatePrepared(IReadOnlyList<BattlePreparedRepeatedDamageSegment> segments);

        /// <summary>不再重算公式或后效，按冻结顺序提交一段及其全部记录。</summary>
        IReadOnlyList<BattleSettlementRecord> CommitPreparedHit(
            BattlePreparedRepeatedDamageSegment segment,
            int startingOrder);
    }

    /// <summary>共享重复伤害执行器所需的来源、目标策略与有序伤害段。</summary>
    internal sealed class BattleRepeatedDamageRequest
    {
        /// <summary>本次伤害链的来源参与者。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>固定目标策略使用的敌人；逐段随机策略必须为空。</summary>
        internal CombatantId? FixedTargetId { get; }

        /// <summary>本次伤害链使用的目标选择策略。</summary>
        internal BattleRepeatedDamageTargetPolicy TargetPolicy { get; }

        /// <summary>按逻辑发生顺序冻结的全部伤害段。</summary>
        internal IReadOnlyList<BattleRepeatedDamageHitRequest> Hits { get; }

        /// <summary>防御性复制调用方输入并冻结一次重复伤害请求。</summary>
        internal BattleRepeatedDamageRequest(
            CombatantId sourceId,
            CombatantId? fixedTargetId,
            BattleRepeatedDamageTargetPolicy targetPolicy,
            IEnumerable<BattleRepeatedDamageHitRequest> hits)
        {
            if (hits == null)
                throw new ArgumentNullException(nameof(hits));

            SourceId = sourceId;
            FixedTargetId = fixedTargetId;
            TargetPolicy = targetPolicy;
            Hits = new ReadOnlyCollection<BattleRepeatedDamageHitRequest>(
                new List<BattleRepeatedDamageHitRequest>(hits));
        }
    }

    /// <summary>一段已完成目标选择与公式投影的不可变伤害计划。</summary>
    internal readonly struct BattlePreparedRepeatedDamageSegment
    {
        /// <summary>本段对应的配置 Effect 标识。</summary>
        internal BattleEffectId? EffectId { get; }

        /// <summary>本段来源伤害在进入具体公式前的冻结基础值。</summary>
        internal int ConfiguredValue { get; }

        /// <summary>本段冻结的目标参与者。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>本段冻结的完整格挡与生命伤害结果。</summary>
        internal BattleDamageFormulaOutcome Outcome { get; }

        /// <summary>本段来源伤害及其全部紧邻后效结束后的目标标量投影。</summary>
        internal BattleEffectTargetSnapshot ProjectedTargetAfterSegment { get; }

        /// <summary>冻结一段提交时无需重新选目标或计算公式的伤害结果。</summary>
        internal BattlePreparedRepeatedDamageSegment(
            BattleEffectId? effectId,
            int configuredValue,
            CombatantId targetId,
            BattleDamageFormulaOutcome outcome,
            BattleEffectTargetSnapshot projectedTargetAfterSegment)
        {
            if (effectId.HasValue && effectId.Value.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(effectId));

            EffectId = effectId;
            ConfiguredValue = configuredValue;
            TargetId = targetId;
            Outcome = outcome;
            ProjectedTargetAfterSegment = projectedTargetAfterSegment;
        }
    }

    /// <summary>一名 Encounter 敌人在全部重复伤害及后效结束后的终态投影。</summary>
    internal readonly struct BattleRepeatedDamageTargetProjection
    {
        /// <summary>终态投影所属的敌方参与者。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>冻结的生命、格挡与易伤终态。</summary>
        internal BattleEffectTargetSnapshot Target { get; }

        /// <summary>绑定一名敌人与其重复伤害终态投影。</summary>
        internal BattleRepeatedDamageTargetProjection(
            CombatantId targetId,
            BattleEffectTargetSnapshot target)
        {
            TargetId = targetId;
            Target = target;
        }
    }

    /// <summary>首写前保存全部目标、公式、随机状态与参与者快照的重复伤害计划。</summary>
    internal sealed class BattlePreparedRepeatedDamagePlan
    {
        /// <summary>创建本计划的唯一执行器。</summary>
        internal BattleRepeatedDamageExecutor Owner { get; }

        /// <summary>本次伤害链的来源参与者。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>预演开始时的来源标量快照。</summary>
        internal BattleCombatantScalarSnapshot SourceSnapshot { get; }

        /// <summary>按 Encounter 顺序冻结的全部敌人标量快照，包括已死亡敌人。</summary>
        internal IReadOnlyList<BattleCombatantScalarSnapshot> EnemySnapshots { get; }

        /// <summary>实际会提交的伤害段；没有存活候选后的尾段不会进入本集合。</summary>
        internal IReadOnlyList<BattlePreparedRepeatedDamageSegment> Segments { get; }

        /// <summary>按 Encounter 顺序冻结的全部敌方终态投影，包含未被命中的敌人。</summary>
        internal IReadOnlyList<BattleRepeatedDamageTargetProjection> EnemyProjections { get; }

        /// <summary>预演前权威卡牌目标随机流的状态。</summary>
        internal uint RandomStateBefore { get; }

        /// <summary>全部逐段目标选择完成后的候选随机状态。</summary>
        internal uint RandomStateAfter { get; }

        /// <summary>本计划独占的职业伤害公式与后效局部序列；普通公式时为空。</summary>
        internal IBattleRepeatedDamageHitSequence HitSequence { get; }

        /// <summary>本计划提交时会产生的伤害与职业后效结算总数。</summary>
        internal int PlannedSettlementCount { get; }

        /// <summary>本计划是否已经在首写前完成唯一一次校验。</summary>
        internal bool IsValidated { get; private set; }

        /// <summary>本计划是否已经完成唯一一次提交。</summary>
        internal bool IsConsumed { get; private set; }

        /// <summary>校验时冻结的首条伤害结算顺序。</summary>
        internal int StartingOrder { get; private set; }

        /// <summary>冻结执行器归属、快照、随机状态、伤害段和结算总数。</summary>
        internal BattlePreparedRepeatedDamagePlan(
            BattleRepeatedDamageExecutor owner,
            CombatantData source,
            IEnumerable<BattleCombatantScalarSnapshot> enemySnapshots,
            IEnumerable<BattlePreparedRepeatedDamageSegment> segments,
            IEnumerable<BattleRepeatedDamageTargetProjection> enemyProjections,
            uint randomStateBefore,
            uint randomStateAfter,
            IBattleRepeatedDamageHitSequence hitSequence)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (enemySnapshots == null)
                throw new ArgumentNullException(nameof(enemySnapshots));
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));
            if (enemyProjections == null)
                throw new ArgumentNullException(nameof(enemyProjections));
            if (hitSequence == null)
                throw new ArgumentNullException(nameof(hitSequence));

            SourceId = source.Id;
            SourceSnapshot = new BattleCombatantScalarSnapshot(source);
            EnemySnapshots = new ReadOnlyCollection<BattleCombatantScalarSnapshot>(
                new List<BattleCombatantScalarSnapshot>(enemySnapshots));
            Segments = new ReadOnlyCollection<BattlePreparedRepeatedDamageSegment>(
                new List<BattlePreparedRepeatedDamageSegment>(segments));
            EnemyProjections = new ReadOnlyCollection<BattleRepeatedDamageTargetProjection>(
                new List<BattleRepeatedDamageTargetProjection>(enemyProjections));
            RandomStateBefore = randomStateBefore;
            RandomStateAfter = randomStateAfter;
            HitSequence = hitSequence;
            PlannedSettlementCount = hitSequence.PlannedSettlementCount;
            if (PlannedSettlementCount < 0)
                throw new ArgumentOutOfRangeException(nameof(hitSequence));
        }

        /// <summary>冻结首条结算顺序并阻止计划被再次校验。</summary>
        internal void MarkValidated(int startingOrder)
        {
            if (IsValidated || IsConsumed)
                throw new InvalidOperationException("重复伤害计划已经校验或提交。");

            StartingOrder = startingOrder;
            IsValidated = true;
        }

        /// <summary>把已校验计划标记为已消费并阻止重复提交。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated || IsConsumed)
                throw new InvalidOperationException("重复伤害计划尚未校验或已经提交。");

            IsConsumed = true;
        }
    }

    /// <summary>重复伤害 Prepare 阶段的稳定成功计划或失败原因。</summary>
    internal sealed class BattleRepeatedDamagePreparationResult
    {
        /// <summary>Prepare 是否成功且携带可校验计划。</summary>
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>Prepare 失败时可映射到出牌结果的稳定原因。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>Prepare 成功时冻结的计划；失败时为空。</summary>
        internal BattlePreparedRepeatedDamagePlan Plan { get; }

        /// <summary>冻结一次 Prepare 的成功计划或失败原因。</summary>
        internal BattleRepeatedDamagePreparationResult(
            BattleCommandExecutionFailureReason failureReason,
            BattlePreparedRepeatedDamagePlan plan)
        {
            if (failureReason == BattleCommandExecutionFailureReason.None && plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (failureReason != BattleCommandExecutionFailureReason.None && plan != null)
                throw new ArgumentException("失败的重复伤害准备结果不能携带计划。", nameof(plan));

            FailureReason = failureReason;
            Plan = plan;
        }
    }
}
