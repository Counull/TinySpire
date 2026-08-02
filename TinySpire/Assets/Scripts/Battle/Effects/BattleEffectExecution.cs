using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>调用 Effect executor 所需的来源、显式目标与稳定有序 Effect 标识。</summary>
    public sealed class BattleEffectExecutionRequest
    {
        /// <summary>本次 Effect 链的来源参与者。</summary>
        public CombatantId SourceId { get; }

        /// <summary>调用方已经解析并验证的单个显式目标。</summary>
        public CombatantId TargetId { get; }

        /// <summary>按调用方领域顺序冻结的强类型 Effect 标识。</summary>
        public IReadOnlyList<BattleEffectId> EffectIds { get; }

        /// <summary>复制并冻结一次 Effect 执行请求。</summary>
        public BattleEffectExecutionRequest(
            CombatantId sourceId,
            CombatantId targetId,
            IEnumerable<BattleEffectId> effectIds)
        {
            if (effectIds == null)
            {
                throw new ArgumentNullException(nameof(effectIds));
            }

            SourceId = sourceId;
            TargetId = targetId;
            EffectIds = new ReadOnlyCollection<BattleEffectId>(
                new List<BattleEffectId>(effectIds));
        }
    }

    /// <summary>一次 Effect 执行的不可变成功或失败结果。</summary>
    public sealed class BattleEffectExecutionResult
    {
        /// <summary>预校验与全部 Effect 执行是否成功。</summary>
        public bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>失败时可直接映射到命令结果的稳定原因。</summary>
        public BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>按实际发生顺序冻结的 Effect 结算记录。</summary>
        public IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>复制并冻结一次 Effect 执行结果。</summary>
        internal BattleEffectExecutionResult(
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
            {
                throw new ArgumentNullException(nameof(settlements));
            }

            FailureReason = failureReason;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }
    }

    /// <summary>首次写入前完成解析和顺序模拟的单个内部 Effect 操作。</summary>
    internal readonly struct BattlePreparedEffectOperation
    {
        /// <summary>强类型静态 Effect 标识。</summary>
        internal BattleEffectId EffectId { get; }

        /// <summary>已经验证支持的领域操作。</summary>
        internal BattleEffectOperationType OperationType { get; }

        /// <summary>静态配置值。</summary>
        internal int ConfiguredValue { get; }

        /// <summary>属性操作已经验证的属性；其他操作为空。</summary>
        internal BattleAttributeType? Attribute { get; }

        /// <summary>前序操作已令目标死亡时，此操作只产生 skipped 记录。</summary>
        internal bool ShouldSkipTargetNotAlive { get; }

        /// <summary>冻结一个已经完整验证的内部 Effect 操作。</summary>
        internal BattlePreparedEffectOperation(
            BattleEffectId effectId,
            BattleEffectOperationType operationType,
            int configuredValue,
            BattleAttributeType? attribute,
            bool shouldSkipTargetNotAlive)
        {
            EffectId = effectId;
            OperationType = operationType;
            ConfiguredValue = configuredValue;
            Attribute = attribute;
            ShouldSkipTargetNotAlive = shouldSkipTargetNotAlive;
        }
    }

    /// <summary>用于约束 Prepare 与 ExecutePrepared 之间没有参与者事实漂移的内部快照。</summary>
    internal readonly struct BattleCombatantScalarSnapshot
    {
        /// <summary>参与者标识。</summary>
        internal CombatantId Id { get; }

        /// <summary>当前生命。</summary>
        internal int Health { get; }

        /// <summary>当前力量。</summary>
        internal int Strength { get; }

        /// <summary>当前格挡。</summary>
        internal int Block { get; }

        /// <summary>当前易伤。</summary>
        internal int Vulnerable { get; }

        /// <summary>冻结参与者的四项权威标量。</summary>
        internal BattleCombatantScalarSnapshot(CombatantData combatant)
        {
            if (combatant == null)
            {
                throw new ArgumentNullException(nameof(combatant));
            }

            Id = combatant.Id;
            Health = combatant.CurrentHealth;
            Strength = combatant.CurrentStrength;
            Block = combatant.CurrentBlock;
            Vulnerable = combatant.CurrentVulnerable;
        }

        /// <summary>判断参与者当前四项标量是否仍与预构建时一致。</summary>
        internal bool Matches(CombatantData combatant)
        {
            return combatant != null &&
                   combatant.Id == Id &&
                   combatant.CurrentHealth == Health &&
                   combatant.CurrentStrength == Strength &&
                   combatant.CurrentBlock == Block &&
                   combatant.CurrentVulnerable == Vulnerable;
        }
    }

    /// <summary>可在调用方联合校验后无普通失败执行的内部 Effect 计划。</summary>
    internal sealed class BattlePreparedEffectPlan
    {
        /// <summary>创建此计划的 executor，用于阻止跨实例执行。</summary>
        internal BattleEffectExecutor Owner { get; }

        /// <summary>计划来源参与者。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>计划显式目标。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>按 Effect 标识顺序冻结的已验证操作。</summary>
        internal IReadOnlyList<BattlePreparedEffectOperation> Operations { get; }

        /// <summary>预构建时的来源标量。</summary>
        internal BattleCombatantScalarSnapshot SourceSnapshot { get; }

        /// <summary>预构建时的目标标量。</summary>
        internal BattleCombatantScalarSnapshot TargetSnapshot { get; }

        /// <summary>全部已验证操作完成后的 source 投影，用于后续状态时机预构建。</summary>
        internal BattleEffectTargetSnapshot ProjectedSourceAfterEffect { get; }

        /// <summary>全部已验证操作完成后的显式 target 投影，用于联合计划在首写前派生终局。</summary>
        internal BattleEffectTargetSnapshot ProjectedTargetAfterEffect { get; }

        /// <summary>计划是否已经在首次写入前通过快照校验。</summary>
        internal bool IsValidated { get; private set; }

        /// <summary>计划是否已经完成无普通失败提交。</summary>
        internal bool IsConsumed { get; private set; }

        /// <summary>通过校验后冻结的结算起始序号。</summary>
        internal int StartingOrder { get; private set; }

        /// <summary>冻结一次完整预构建计划。</summary>
        internal BattlePreparedEffectPlan(
            BattleEffectExecutor owner,
            CombatantData source,
            CombatantData target,
            IEnumerable<BattlePreparedEffectOperation> operations,
            BattleEffectTargetSnapshot projectedSourceAfterEffect,
            BattleEffectTargetSnapshot projectedTargetAfterEffect)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            SourceId = source.Id;
            TargetId = target.Id;
            SourceSnapshot = new BattleCombatantScalarSnapshot(source);
            TargetSnapshot = new BattleCombatantScalarSnapshot(target);
            ProjectedSourceAfterEffect = projectedSourceAfterEffect;
            ProjectedTargetAfterEffect = projectedTargetAfterEffect;
            Operations = new ReadOnlyCollection<BattlePreparedEffectOperation>(
                new List<BattlePreparedEffectOperation>(operations));
        }

        /// <summary>在唯一首次写入前校验成功后冻结记录起点。</summary>
        internal void MarkValidated(int startingOrder)
        {
            if (IsValidated)
                throw new InvalidOperationException("Effect 计划已经完成过校验。");
            if (IsConsumed)
                throw new InvalidOperationException("Effect 计划已经提交。");

            StartingOrder = startingOrder;
            IsValidated = true;
        }

        /// <summary>在无普通失败提交开始时消费计划，阻止重复写入。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated)
                throw new InvalidOperationException("Effect 计划尚未通过首次写入前校验。");
            if (IsConsumed)
                throw new InvalidOperationException("Effect 计划已经提交。");

            IsConsumed = true;
        }
    }

    /// <summary>内部预构建阶段的不可变成功或失败结果。</summary>
    internal readonly struct BattleEffectPreparationResult
    {
        /// <summary>预构建是否成功。</summary>
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>预构建失败原因。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>成功时可执行的冻结计划。</summary>
        internal BattlePreparedEffectPlan Plan { get; }

        /// <summary>创建一次内部预构建结果。</summary>
        internal BattleEffectPreparationResult(
            BattleCommandExecutionFailureReason failureReason,
            BattlePreparedEffectPlan plan)
        {
            FailureReason = failureReason;
            Plan = plan;
        }
    }
}
