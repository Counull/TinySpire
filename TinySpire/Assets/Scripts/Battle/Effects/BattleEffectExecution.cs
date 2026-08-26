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

        /// <summary>卡牌出牌链携带的实例等级投影；敌人与非卡牌 Effect 保持为空。</summary>
        internal BattleCardLevelProjection CardLevelProjection { get; }

        /// <summary>复制并冻结一次 Effect 执行请求。</summary>
        public BattleEffectExecutionRequest(
            CombatantId sourceId,
            CombatantId targetId,
            IEnumerable<BattleEffectId> effectIds)
            : this(sourceId, targetId, effectIds, cardLevelProjection: null)
        {
        }

        /// <summary>为卡牌出牌链冻结同一实例等级投影，避免执行阶段重新按模板猜测等级。</summary>
        internal BattleEffectExecutionRequest(
            CombatantId sourceId,
            CombatantId targetId,
            IEnumerable<BattleEffectId> effectIds,
            BattleCardLevelProjection cardLevelProjection)
        {
            if (effectIds == null)
            {
                throw new ArgumentNullException(nameof(effectIds));
            }

            SourceId = sourceId;
            TargetId = targetId;
            EffectIds = new ReadOnlyCollection<BattleEffectId>(
                new List<BattleEffectId>(effectIds));
            CardLevelProjection = cardLevelProjection;
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

    /// <summary>为一个 Session 注入纯伤害计算覆盖的内部边界；默认 Effect executor 不持有该覆盖。</summary>
    internal interface IBattleDamageFormulaOverride
    {
        /// <summary>为一条 Effect 链创建只服务于本次预演和提交的职业伤害序列，禁止把预演中的私有状态预约写回战斗事实。</summary>
        IBattleDamageFormulaOverrideSequence CreateSequence();
    }

    /// <summary>职业伤害覆盖在单条 Effect 链中的局部预演状态；它只冻结伤害和后效计划，实际写入仍由已验证计划统一提交。</summary>
    internal interface IBattleDamageFormulaOverrideSequence
    {
        /// <summary>从冻结来源和目标标量预演一段可提交伤害，并推进仅属于当前 Effect 链的局部状态。</summary>
        BattleDamageFormulaOutcome Calculate(
            CombatantData source,
            int sourceStrength,
            CombatantId targetId,
            int configuredValue,
            BattleEffectTargetSnapshot target);

        /// <summary>返回这条 Effect 链在伤害主记录之后还会写出的职业私有后效记录数量，用于首写前的顺序范围验证。</summary>
        int PlannedAftermathSettlementCount { get; }

        /// <summary>在对应伤害主记录已提交后写入已预演的职业私有后效，并返回顺序连续的补充结算记录。</summary>
        IReadOnlyList<BattleSettlementRecord> CommitDamageAftermath(
            CombatantId sourceId,
            CombatantId targetId,
            BattleDamageFormulaOutcome damageOutcome,
            int startingOrder);
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

        /// <summary>伤害操作在预构建时冻结的推演结果；非伤害或已跳过操作为空。</summary>
        internal BattleDamageFormulaOutcome? PreparedDamageOutcome { get; }

        /// <summary>治疗操作在预构建时冻结的推演结果；非治疗或已跳过操作为空。</summary>
        internal BattleHealthRestorationOutcome? PreparedHealthRestorationOutcome { get; }

        /// <summary>格挡保留操作在预构建时冻结的一次性写入计划。</summary>
        internal BattlePreparedBlockRetentionPlan PreparedBlockRetention { get; }

        /// <summary>结算触发注册在预构建时冻结的一次性写入计划。</summary>
        internal BattlePreparedSettlementTriggerRegistration PreparedSettlementTriggerRegistration { get; }

        /// <summary>冻结一个已经完整验证的内部 Effect 操作。</summary>
        internal BattlePreparedEffectOperation(
            BattleEffectId effectId,
            BattleEffectOperationType operationType,
            int configuredValue,
            BattleAttributeType? attribute,
            bool shouldSkipTargetNotAlive,
            BattleDamageFormulaOutcome? preparedDamageOutcome = null,
            BattleHealthRestorationOutcome? preparedHealthRestorationOutcome = null,
            BattlePreparedBlockRetentionPlan preparedBlockRetention = null,
            BattlePreparedSettlementTriggerRegistration preparedSettlementTriggerRegistration = null)
        {
            if (operationType == BattleEffectOperationType.DealDamage &&
                !shouldSkipTargetNotAlive &&
                !preparedDamageOutcome.HasValue)
            {
                throw new ArgumentException("可执行伤害操作必须携带预构建的伤害结果。", nameof(preparedDamageOutcome));
            }
            if (operationType == BattleEffectOperationType.Heal &&
                !shouldSkipTargetNotAlive &&
                !preparedHealthRestorationOutcome.HasValue)
            {
                throw new ArgumentException(
                    "可执行治疗操作必须携带预构建的治疗结果。",
                    nameof(preparedHealthRestorationOutcome));
            }
            if (operationType == BattleEffectOperationType.RetainBlock &&
                !shouldSkipTargetNotAlive &&
                preparedBlockRetention == null)
            {
                throw new ArgumentException(
                    "可执行格挡保留操作必须携带预构建计划。",
                    nameof(preparedBlockRetention));
            }
            if (operationType == BattleEffectOperationType.RegisterBlockGainRandomEnemyDamage &&
                !shouldSkipTargetNotAlive &&
                preparedSettlementTriggerRegistration == null)
            {
                throw new ArgumentException(
                    "可执行结算触发注册必须携带预构建计划。",
                    nameof(preparedSettlementTriggerRegistration));
            }

            EffectId = effectId;
            OperationType = operationType;
            ConfiguredValue = configuredValue;
            Attribute = attribute;
            ShouldSkipTargetNotAlive = shouldSkipTargetNotAlive;
            PreparedDamageOutcome = preparedDamageOutcome;
            PreparedHealthRestorationOutcome = preparedHealthRestorationOutcome;
            PreparedBlockRetention = preparedBlockRetention;
            PreparedSettlementTriggerRegistration = preparedSettlementTriggerRegistration;
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

        /// <summary>全部已验证操作完成后的 source 力量投影。</summary>
        internal int ProjectedSourceStrengthAfterEffect { get; }

        /// <summary>全部已验证操作完成后的显式 target 力量投影。</summary>
        internal int ProjectedTargetStrengthAfterEffect { get; }

        /// <summary>本计划独占的职业伤害序列；未注入职业覆盖时保持为空。</summary>
        internal IBattleDamageFormulaOverrideSequence DamageFormulaSequence { get; }

        /// <summary>返回本计划提交时会写入的全部结算记录数，包含每段伤害主记录之后已冻结的职业私有后效。</summary>
        internal int PlannedSettlementCount
        {
            get
            {
                int aftermathCount = DamageFormulaSequence?.PlannedAftermathSettlementCount ?? 0;
                if (aftermathCount < 0)
                    throw new InvalidOperationException("职业伤害序列不能声明负数后效记录。");

                return checked(Operations.Count + aftermathCount);
            }
        }

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
            IBattleDamageFormulaOverrideSequence damageFormulaSequence,
            BattleEffectTargetSnapshot projectedSourceAfterEffect,
            BattleEffectTargetSnapshot projectedTargetAfterEffect,
            int projectedSourceStrengthAfterEffect,
            int projectedTargetStrengthAfterEffect)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            SourceId = source.Id;
            TargetId = target.Id;
            SourceSnapshot = new BattleCombatantScalarSnapshot(source);
            TargetSnapshot = new BattleCombatantScalarSnapshot(target);
            ProjectedSourceAfterEffect = projectedSourceAfterEffect;
            ProjectedTargetAfterEffect = projectedTargetAfterEffect;
            ProjectedSourceStrengthAfterEffect = projectedSourceStrengthAfterEffect;
            ProjectedTargetStrengthAfterEffect = projectedTargetStrengthAfterEffect;
            DamageFormulaSequence = damageFormulaSequence;
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
