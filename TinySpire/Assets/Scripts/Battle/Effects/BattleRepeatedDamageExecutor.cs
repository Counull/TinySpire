using System;
using System.Collections.Generic;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>隐藏重复伤害逐段选目标、投影、随机提交与结算排序的具体深模块。</summary>
    internal sealed class BattleRepeatedDamageExecutor
    {
        private readonly BattleCombatantsData _combatants;
        private readonly IReadOnlyList<CombatantId> _enemyCombatantIdsInEncounterOrder;
        private readonly GameRandom _cardTargetRandom;
        private readonly BattleCombatantEffectOperations _stateOperations;
        private readonly IBattleDamageFormulaOverride _damageFormulaOverride;

        /// <summary>绑定本场权威参与者、Encounter 顺序和由 Turn 独占的卡牌目标随机流。</summary>
        internal BattleRepeatedDamageExecutor(
            BattleCombatantsData combatants,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            GameRandom cardTargetRandom,
            IBattleDamageFormulaOverride damageFormulaOverride = null)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _enemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder
                ?? throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
            _cardTargetRandom = cardTargetRandom ?? throw new ArgumentNullException(nameof(cardTargetRandom));
            _stateOperations = new BattleCombatantEffectOperations(combatants);
            _damageFormulaOverride = damageFormulaOverride;
        }

        /// <summary>只读返回当前权威卡牌目标随机状态，供队列级原子性验证观察。</summary>
        internal uint RandomState => _cardTargetRandom.State;

        /// <summary>在零写入下冻结全部敌人快照、逐段随机目标与完整伤害结果。</summary>
        internal BattleRepeatedDamagePreparationResult Prepare(
            BattleRepeatedDamageRequest request)
        {
            return Prepare(request, hitSequence: null);
        }

        /// <summary>使用调用方提供的逐段管线，在零写入下冻结目标、全部后效投影与提交序列。</summary>
        internal BattleRepeatedDamagePreparationResult Prepare(
            BattleRepeatedDamageRequest request,
            IBattleRepeatedDamageHitSequence hitSequence)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!_combatants.TryGet(request.SourceId, out CombatantData source))
                return Failed(BattleCommandExecutionFailureReason.EffectSourceNotFound);
            if (!source.IsAlive)
                return Failed(BattleCommandExecutionFailureReason.EffectSourceNotAlive);
            if (request.Hits.Count == 0)
                return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

            BattleCommandExecutionFailureReason targetContractFailure =
                ValidateTargetContract(request);
            if (targetContractFailure != BattleCommandExecutionFailureReason.None)
                return Failed(targetContractFailure);

            var enemySnapshots = new List<BattleCombatantScalarSnapshot>(
                _enemyCombatantIdsInEncounterOrder.Count);
            var projectedTargets = new Dictionary<CombatantId, BattleEffectTargetSnapshot>(
                _enemyCombatantIdsInEncounterOrder.Count);
            foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
            {
                if (!_combatants.TryGet(enemyId, out CombatantData enemy) ||
                    !(enemy is EnemyCombatantData) ||
                    projectedTargets.ContainsKey(enemyId))
                {
                    throw new InvalidOperationException(
                        "Encounter 敌人顺序必须由存在且不重复的敌方参与者组成。");
                }

                enemySnapshots.Add(new BattleCombatantScalarSnapshot(enemy));
                projectedTargets.Add(
                    enemyId,
                    new BattleEffectTargetSnapshot(
                        enemy.CurrentHealth,
                        enemy.CurrentBlock,
                        enemy.CurrentVulnerable));
            }

            uint randomStateBefore = _cardTargetRandom.State;
            var candidateRandom = new GameRandom(1u)
            {
                State = randomStateBefore,
            };
            IBattleRepeatedDamageHitSequence effectiveHitSequence = hitSequence ??
                new DefaultRepeatedDamageHitSequence(
                    _stateOperations,
                    _damageFormulaOverride);

            var segments = new List<BattlePreparedRepeatedDamageSegment>(request.Hits.Count);
            try
            {
                foreach (BattleRepeatedDamageHitRequest hit in request.Hits)
                {
                    if (!TryResolveProjectedTarget(
                            request,
                            projectedTargets,
                            candidateRandom,
                            out CombatantId targetId))
                    {
                        break;
                    }

                    BattleEffectTargetSnapshot target = projectedTargets[targetId];
                    BattleRepeatedDamageHitPreparation preparation =
                        effectiveHitSequence.PrepareHit(
                        source,
                        hit,
                        targetId,
                        target);
                    segments.Add(new BattlePreparedRepeatedDamageSegment(
                        hit.EffectId,
                        hit.ConfiguredValue,
                        targetId,
                        preparation.PrimaryOutcome,
                        preparation.ProjectedTargetAfterHit));
                    projectedTargets[targetId] = preparation.ProjectedTargetAfterHit;
                }

                var enemyProjections = new List<BattleRepeatedDamageTargetProjection>(
                    _enemyCombatantIdsInEncounterOrder.Count);
                foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
                {
                    enemyProjections.Add(new BattleRepeatedDamageTargetProjection(
                        enemyId,
                        projectedTargets[enemyId]));
                }

                var plan = new BattlePreparedRepeatedDamagePlan(
                    this,
                    source,
                    enemySnapshots,
                    segments,
                    enemyProjections,
                    randomStateBefore,
                    candidateRandom.State,
                    effectiveHitSequence);
                return new BattleRepeatedDamagePreparationResult(
                    BattleCommandExecutionFailureReason.None,
                    plan);
            }
            catch (OverflowException)
            {
                return Failed(BattleCommandExecutionFailureReason.EffectValueOverflow);
            }
        }

        /// <summary>在调用方首次写入前校验归属、一次性、全部快照、随机状态和结算顺序。</summary>
        internal void ValidatePrepared(
            BattlePreparedRepeatedDamagePlan plan,
            int startingOrder)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("重复伤害计划不能由其他执行器校验。");
            if (plan.IsValidated || plan.IsConsumed)
                throw new InvalidOperationException("重复伤害计划已经校验或提交。");
            if (plan.PlannedSettlementCount > 0 &&
                (long)startingOrder + plan.PlannedSettlementCount - 1L > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingOrder),
                    "重复伤害结算顺序超出 Int32 范围。");
            }
            if (_cardTargetRandom.State != plan.RandomStateBefore)
                throw new InvalidOperationException("重复伤害计划的卡牌目标随机状态已经漂移。");
            if (!_combatants.TryGet(plan.SourceId, out CombatantData source) ||
                !plan.SourceSnapshot.Matches(source))
            {
                throw new InvalidOperationException("重复伤害计划的来源标量快照已经漂移。");
            }
            if (plan.EnemySnapshots.Count != _enemyCombatantIdsInEncounterOrder.Count)
                throw new InvalidOperationException("重复伤害计划的 Encounter 敌人数量已经漂移。");

            for (int index = 0; index < plan.EnemySnapshots.Count; index++)
            {
                BattleCombatantScalarSnapshot snapshot = plan.EnemySnapshots[index];
                if (snapshot.Id != _enemyCombatantIdsInEncounterOrder[index] ||
                    !_combatants.TryGet(snapshot.Id, out CombatantData enemy) ||
                    !(enemy is EnemyCombatantData) ||
                    !snapshot.Matches(enemy))
                {
                    throw new InvalidOperationException(
                        "重复伤害计划的 Encounter 顺序或敌人标量快照已经漂移。");
                }
            }

            if (plan.EnemyProjections.Count != plan.EnemySnapshots.Count)
                throw new InvalidOperationException("重复伤害计划的敌方终态投影数量不完整。");
            for (int index = 0; index < plan.EnemyProjections.Count; index++)
            {
                if (plan.EnemyProjections[index].TargetId != plan.EnemySnapshots[index].Id)
                {
                    throw new InvalidOperationException(
                        "重复伤害计划的敌方终态投影顺序与 Encounter 不一致。");
                }
            }

            plan.HitSequence.ValidatePrepared(plan.Segments);

            plan.MarkValidated(startingOrder);
        }

        /// <summary>提交已校验计划中的冻结伤害，不再随机或重算，并在全部伤害后推进权威随机流。</summary>
        internal IReadOnlyList<BattleSettlementRecord> CommitPrepared(
            BattlePreparedRepeatedDamagePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("重复伤害计划不能由其他执行器提交。");
            if (!plan.IsValidated || plan.IsConsumed)
                throw new InvalidOperationException("重复伤害计划尚未校验或已经提交。");

            plan.MarkConsumed();
            var settlements = new List<BattleSettlementRecord>(plan.PlannedSettlementCount);
            foreach (BattlePreparedRepeatedDamageSegment segment in plan.Segments)
            {
                IReadOnlyList<BattleSettlementRecord> hitSettlements =
                    plan.HitSequence.CommitPreparedHit(
                        segment,
                        checked(plan.StartingOrder + settlements.Count));
                if (hitSettlements == null)
                    throw new InvalidOperationException("重复伤害序列不能返回空 settlement 集合。");
                foreach (BattleSettlementRecord settlement in hitSettlements)
                {
                    if (settlement == null ||
                        settlement.Order != plan.StartingOrder + settlements.Count)
                    {
                        throw new InvalidOperationException(
                            "重复伤害序列必须按冻结段返回连续 settlement。");
                    }

                    settlements.Add(settlement);
                }
            }

            if (settlements.Count != plan.PlannedSettlementCount)
                throw new InvalidOperationException("重复伤害计划的实际结算数量与预演不一致。");

            _cardTargetRandom.State = plan.RandomStateAfter;
            return settlements.AsReadOnly();
        }

        /// <summary>校验固定目标与逐段随机策略对显式目标输入的互斥契约。</summary>
        private BattleCommandExecutionFailureReason ValidateTargetContract(
            BattleRepeatedDamageRequest request)
        {
            switch (request.TargetPolicy)
            {
                case BattleRepeatedDamageTargetPolicy.FixedEnemy:
                    if (!request.FixedTargetId.HasValue)
                        return BattleCommandExecutionFailureReason.TargetRequired;
                    if (!TryGetEncounterEnemy(request.FixedTargetId.Value, out CombatantData fixedTarget))
                        return BattleCommandExecutionFailureReason.TargetRuleMismatch;
                    return fixedTarget.IsAlive
                        ? BattleCommandExecutionFailureReason.None
                        : BattleCommandExecutionFailureReason.TargetNotAlive;
                case BattleRepeatedDamageTargetPolicy.RandomLivingEnemyPerHit:
                    if (request.FixedTargetId.HasValue)
                        return BattleCommandExecutionFailureReason.TargetRuleMismatch;
                    return HasLivingEncounterEnemy()
                        ? BattleCommandExecutionFailureReason.None
                        : BattleCommandExecutionFailureReason.TargetNotAlive;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.TargetPolicy));
            }
        }

        /// <summary>按策略从当前投影解析一段目标；逐段随机即使只有一个候选也消费一次随机。</summary>
        private bool TryResolveProjectedTarget(
            BattleRepeatedDamageRequest request,
            IReadOnlyDictionary<CombatantId, BattleEffectTargetSnapshot> projectedTargets,
            GameRandom candidateRandom,
            out CombatantId targetId)
        {
            if (request.TargetPolicy == BattleRepeatedDamageTargetPolicy.FixedEnemy)
            {
                targetId = request.FixedTargetId.Value;
                return projectedTargets[targetId].Health > 0;
            }
            if (request.TargetPolicy != BattleRepeatedDamageTargetPolicy.RandomLivingEnemyPerHit)
                throw new ArgumentOutOfRangeException(nameof(request.TargetPolicy));

            var candidates = new List<CombatantId>();
            foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
            {
                if (projectedTargets[enemyId].Health > 0)
                    candidates.Add(enemyId);
            }
            if (candidates.Count == 0)
            {
                targetId = default;
                return false;
            }

            targetId = candidates[candidateRandom.NextInt(candidates.Count)];
            return true;
        }

        /// <summary>通过现有普通公式或现有职业公式序列计算一段完整伤害结果。</summary>
        private static BattleDamageFormulaOutcome CalculateDamage(
            IBattleDamageFormulaOverrideSequence damageFormulaSequence,
            CombatantData source,
            CombatantId targetId,
            int configuredValue,
            BattleEffectTargetSnapshot target)
        {
            if (damageFormulaSequence != null)
            {
                return damageFormulaSequence.Calculate(
                    source,
                    source.CurrentStrength,
                    targetId,
                    configuredValue,
                    target);
            }

            BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
                new BattleEffectFormulaContext(
                    BattleEffectOperationType.DealDamage,
                    configuredValue,
                    source.CurrentStrength,
                    target));
            if (!formula.DamageOutcome.HasValue)
                throw new InvalidOperationException("普通重复伤害公式未返回伤害结果。");

            return formula.DamageOutcome.Value;
        }

        /// <summary>按当前段之后的连续顺序提交现有职业伤害序列已冻结的私有后效。</summary>
        private static void AppendDamageAftermath(
            IBattleDamageFormulaOverrideSequence damageFormulaSequence,
            CombatantId sourceId,
            CombatantId targetId,
            BattleDamageFormulaOutcome damageOutcome,
            int startingOrder,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (damageFormulaSequence == null)
                return;

            IReadOnlyList<BattleSettlementRecord> aftermath =
                damageFormulaSequence.CommitDamageAftermath(
                    sourceId,
                    targetId,
                    damageOutcome,
                    checked(startingOrder + settlements.Count));
            if (aftermath == null)
                throw new InvalidOperationException("职业伤害序列不能返回空后效集合。");

            foreach (BattleSettlementRecord settlement in aftermath)
            {
                if (settlement == null || settlement.Order != startingOrder + settlements.Count)
                    throw new InvalidOperationException("职业伤害后效必须连续排序。");
                settlements.Add(settlement);
            }
        }

        /// <summary>按 Encounter 身份确认指定参与者是本场敌人并返回当前事实。</summary>
        private bool TryGetEncounterEnemy(
            CombatantId targetId,
            out CombatantData target)
        {
            foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
            {
                if (enemyId != targetId)
                    continue;
                return _combatants.TryGet(targetId, out target) && target is EnemyCombatantData;
            }

            target = null;
            return false;
        }

        /// <summary>按 Encounter 顺序判断至少存在一名当前存活敌人。</summary>
        private bool HasLivingEncounterEnemy()
        {
            foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
            {
                if (_combatants.TryGet(enemyId, out CombatantData enemy) &&
                    enemy is EnemyCombatantData &&
                    enemy.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>把现有普通伤害公式及可选职业公式后效适配为共享逐段序列。</summary>
        private sealed class DefaultRepeatedDamageHitSequence : IBattleRepeatedDamageHitSequence
        {
            private readonly BattleCombatantEffectOperations _stateOperations;
            private readonly IBattleDamageFormulaOverrideSequence _damageFormulaSequence;
            private readonly List<BattlePreparedRepeatedDamageSegment> _preparedSegments;
            private CombatantId? _sourceId;
            private bool _isValidated;
            private int _nextCommitIndex;

            /// <summary>返回主伤害记录及现有公式序列声明的后效记录总数。</summary>
            public int PlannedSettlementCount => checked(
                _preparedSegments.Count +
                (_damageFormulaSequence?.PlannedAftermathSettlementCount ?? 0));

            /// <summary>为一份重复伤害计划创建独立的普通公式与提交序列。</summary>
            internal DefaultRepeatedDamageHitSequence(
                BattleCombatantEffectOperations stateOperations,
                IBattleDamageFormulaOverride damageFormulaOverride)
            {
                _stateOperations = stateOperations ??
                    throw new ArgumentNullException(nameof(stateOperations));
                _damageFormulaSequence = damageFormulaOverride?.CreateSequence();
                if (damageFormulaOverride != null && _damageFormulaSequence == null)
                {
                    throw new InvalidOperationException(
                        "伤害公式覆盖必须为每份重复伤害计划创建独立局部序列。");
                }

                _preparedSegments = new List<BattlePreparedRepeatedDamageSegment>();
            }

            /// <summary>预演一段普通攻击及公式后效，并返回该段结束后的标量投影。</summary>
            public BattleRepeatedDamageHitPreparation PrepareHit(
                CombatantData source,
                BattleRepeatedDamageHitRequest hit,
                CombatantId targetId,
                BattleEffectTargetSnapshot projectedTarget)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (_isValidated)
                    throw new InvalidOperationException("已校验的重复伤害序列不能继续预演。");
                if (_sourceId.HasValue && _sourceId.Value != source.Id)
                    throw new InvalidOperationException("重复伤害默认序列不能混用伤害来源。");

                _sourceId = source.Id;
                BattleDamageFormulaOutcome outcome = CalculateDamage(
                    _damageFormulaSequence,
                    source,
                    targetId,
                    hit.ConfiguredValue,
                    projectedTarget);
                var projectedAfter = new BattleEffectTargetSnapshot(
                    outcome.HealthAfter,
                    outcome.BlockAfter,
                    projectedTarget.Vulnerable);
                _preparedSegments.Add(new BattlePreparedRepeatedDamageSegment(
                    hit.EffectId,
                    hit.ConfiguredValue,
                    targetId,
                    outcome,
                    projectedAfter));
                return new BattleRepeatedDamageHitPreparation(outcome, projectedAfter);
            }

            /// <summary>在首写前确认共享段与本序列的冻结段数量、内容及顺序完全一致。</summary>
            public void ValidatePrepared(
                IReadOnlyList<BattlePreparedRepeatedDamageSegment> segments)
            {
                if (segments == null)
                    throw new ArgumentNullException(nameof(segments));
                if (_isValidated || _nextCommitIndex != 0)
                    throw new InvalidOperationException("重复伤害默认序列已经校验或提交。");
                if (segments.Count != _preparedSegments.Count ||
                    (segments.Count > 0 && !_sourceId.HasValue))
                {
                    throw new InvalidOperationException("重复伤害默认序列的冻结段数量已漂移。");
                }
                for (int index = 0; index < segments.Count; index++)
                {
                    if (!MatchesSegment(_preparedSegments[index], segments[index]))
                    {
                        throw new InvalidOperationException(
                            "重复伤害默认序列的冻结段顺序或结果已漂移。");
                    }
                }

                _isValidated = true;
            }

            /// <summary>按已校验顺序提交一段普通伤害及其冻结公式后效。</summary>
            public IReadOnlyList<BattleSettlementRecord> CommitPreparedHit(
                BattlePreparedRepeatedDamageSegment segment,
                int startingOrder)
            {
                if (startingOrder < 0)
                    throw new ArgumentOutOfRangeException(nameof(startingOrder));
                if (!_isValidated || !_sourceId.HasValue ||
                    _nextCommitIndex >= _preparedSegments.Count)
                {
                    throw new InvalidOperationException("重复伤害默认序列尚未校验或已经提交完毕。");
                }
                if (!MatchesSegment(_preparedSegments[_nextCommitIndex], segment))
                    throw new InvalidOperationException("重复伤害默认序列的提交段顺序已漂移。");

                _nextCommitIndex++;
                BattleCombatantEffectOperationResult stateResult =
                    _stateOperations.ApplyPreparedDamage(
                        _sourceId.Value,
                        segment.TargetId,
                        segment.Outcome);
                if (stateResult.Status != BattleCombatantEffectOperationStatus.Applied)
                {
                    throw new InvalidOperationException(
                        $"已校验的重复伤害段提交失败：{stateResult.Status}。");
                }

                BattleDamageFormulaOutcome outcome = stateResult.DamageOutcome.Value;
                var settlements = new List<BattleSettlementRecord>();
                settlements.Add(new BattleDamageAppliedSettlement(
                    startingOrder,
                    segment.EffectId,
                    _sourceId.Value,
                    segment.TargetId,
                    outcome.AttackValue,
                    outcome.BlockBefore,
                    outcome.BlockAfter,
                    outcome.HealthBefore,
                    outcome.HealthAfter));
                AppendDamageAftermath(
                    _damageFormulaSequence,
                    _sourceId.Value,
                    segment.TargetId,
                    outcome,
                    startingOrder,
                    settlements);
                return settlements.AsReadOnly();
            }

            /// <summary>比较两段冻结伤害的身份、基础值、主结果及后效终态。</summary>
            private static bool MatchesSegment(
                BattlePreparedRepeatedDamageSegment expected,
                BattlePreparedRepeatedDamageSegment actual)
            {
                return expected.EffectId == actual.EffectId &&
                    expected.ConfiguredValue == actual.ConfiguredValue &&
                    expected.TargetId == actual.TargetId &&
                    MatchesOutcome(expected.Outcome, actual.Outcome) &&
                    MatchesTarget(
                        expected.ProjectedTargetAfterSegment,
                        actual.ProjectedTargetAfterSegment);
            }

            /// <summary>比较两份伤害结果的全部公开数值，避免提交阶段重算。</summary>
            private static bool MatchesOutcome(
                BattleDamageFormulaOutcome expected,
                BattleDamageFormulaOutcome actual)
            {
                return expected.AttackValue == actual.AttackValue &&
                    expected.BlockBefore == actual.BlockBefore &&
                    expected.BlockAfter == actual.BlockAfter &&
                    expected.BlockAbsorbed == actual.BlockAbsorbed &&
                    expected.HealthBefore == actual.HealthBefore &&
                    expected.HealthAfter == actual.HealthAfter &&
                    expected.HealthLoss == actual.HealthLoss &&
                    expected.WasFatal == actual.WasFatal;
            }

            /// <summary>比较两份目标标量投影的生命、格挡与易伤。</summary>
            private static bool MatchesTarget(
                BattleEffectTargetSnapshot expected,
                BattleEffectTargetSnapshot actual)
            {
                return expected.Health == actual.Health &&
                    expected.Block == actual.Block &&
                    expected.Vulnerable == actual.Vulnerable;
            }
        }

        /// <summary>创建零计划的稳定重复伤害准备失败结果。</summary>
        private static BattleRepeatedDamagePreparationResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleRepeatedDamagePreparationResult(failureReason, plan: null);
        }
    }

    /// <summary>把普通卡牌连续 DealDamage Effect grammar 适配为共享重复伤害请求。</summary>
    internal sealed class BattleRepeatedDamageEffectAdapter
    {
        private readonly cfg.Tables _tables;
        private readonly BattleRepeatedDamageExecutor _executor;

        /// <summary>绑定静态 Effect 表与本场唯一重复伤害执行器。</summary>
        internal BattleRepeatedDamageEffectAdapter(
            cfg.Tables tables,
            BattleRepeatedDamageExecutor executor)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>要求一至多条连续纯伤害绑定，并按指定目标策略创建共享计划。</summary>
        internal BattleRepeatedDamagePreparationResult Prepare(
            IEnumerable<cfg.battle.CardEffectBinding> bindings,
            CombatantId sourceId,
            BattleRepeatedDamageTargetPolicy targetPolicy,
            CombatantId? fixedTargetId)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            var hits = new List<BattleRepeatedDamageHitRequest>();
            foreach (cfg.battle.CardEffectBinding binding in bindings)
            {
                if (binding == null || binding.EffectId <= 0)
                    return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

                cfg.battle.CardEffect effect = _tables.TbCardEffect.GetOrDefault(binding.EffectId);
                if (effect == null)
                    return Failed(BattleCommandExecutionFailureReason.EffectTemplateNotFound);
                if (effect.EffectType != cfg.battle.EffectType.DealDamage)
                    return Failed(BattleCommandExecutionFailureReason.UnsupportedEffectType);
                if (effect.Attribute != cfg.battle.Attribute.None)
                    return Failed(BattleCommandExecutionFailureReason.UnsupportedEffectAttribute);

                hits.Add(new BattleRepeatedDamageHitRequest(
                    new BattleEffectId(effect.Id),
                    effect.Value));
            }
            if (hits.Count == 0)
                return Failed(BattleCommandExecutionFailureReason.InvalidEffectBinding);

            return _executor.Prepare(new BattleRepeatedDamageRequest(
                sourceId,
                fixedTargetId,
                targetPolicy,
                hits));
        }

        /// <summary>创建不携带计划的稳定 grammar 失败结果。</summary>
        private static BattleRepeatedDamagePreparationResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            return new BattleRepeatedDamagePreparationResult(failureReason, plan: null);
        }
    }
}
