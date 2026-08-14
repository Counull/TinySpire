using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>一次通用中毒施加预构建的稳定结果。</summary>
    internal sealed class BattlePoisonApplicationPreparationResult
    {
        /// <summary>预构建失败时的稳定原因；成功时为 None。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>成功时尚未写入的中毒计划；失败时为空。</summary>
        internal BattlePreparedPoisonApplication Plan { get; }

        /// <summary>指示预构建是否已经得到可联合校验的计划。</summary>
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>冻结成功计划或零写入失败结果，并拒绝互相矛盾的组合。</summary>
        internal BattlePoisonApplicationPreparationResult(
            BattleCommandExecutionFailureReason failureReason,
            BattlePreparedPoisonApplication plan)
        {
            if ((failureReason == BattleCommandExecutionFailureReason.None) != (plan != null))
                throw new ArgumentException("中毒预构建结果与计划存在性不一致。", nameof(plan));

            FailureReason = failureReason;
            Plan = plan;
        }
    }

    /// <summary>首次写入前冻结来源、目标与中毒前后值的一次性计划。</summary>
    internal sealed class BattlePreparedPoisonApplication
    {
        private bool _validationAttempted;
        private bool _validated;
        private bool _consumed;

        /// <summary>创建本计划的唯一通用中毒模块。</summary>
        internal BattlePoisonApplication Owner { get; }

        /// <summary>施加者在命令起点的通用四标量快照。</summary>
        internal BattleCombatantScalarSnapshot SourceSnapshot { get; }

        /// <summary>目标在命令起点的通用四标量快照。</summary>
        internal BattleCombatantScalarSnapshot TargetSnapshot { get; }

        /// <summary>施加前目标的中毒层数。</summary>
        internal int PoisonBefore { get; }

        /// <summary>施加后目标的中毒层数。</summary>
        internal int PoisonAfter { get; }

        /// <summary>本计划冻结的实际施加量。</summary>
        internal int Amount => PoisonAfter - PoisonBefore;

        /// <summary>指示本计划是否会产生状态写入与结算。</summary>
        internal bool HasWrite => PoisonAfter != PoisonBefore;

        /// <summary>指示本计划是否已完成唯一一次成功校验。</summary>
        internal bool IsValidated => _validationAttempted && _validated;

        /// <summary>指示本计划是否已被提交入口消费。</summary>
        internal bool IsConsumed => _consumed;

        /// <summary>复制施加者、目标及中毒前后值，供联合事务一次校验与提交。</summary>
        internal BattlePreparedPoisonApplication(
            BattlePoisonApplication owner,
            CombatantData source,
            CombatantData target,
            int poisonBefore,
            int poisonAfter)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (poisonBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(poisonBefore));
            if (poisonAfter < poisonBefore)
                throw new ArgumentOutOfRangeException(nameof(poisonAfter));

            SourceSnapshot = new BattleCombatantScalarSnapshot(source);
            TargetSnapshot = new BattleCombatantScalarSnapshot(target);
            PoisonBefore = poisonBefore;
            PoisonAfter = poisonAfter;
        }

        /// <summary>只记录一次校验结论，失败计划同样禁止被重复尝试。</summary>
        internal void MarkValidated(bool succeeded)
        {
            if (_validationAttempted)
                throw new InvalidOperationException("同一中毒计划不得重复校验。");

            _validationAttempted = true;
            _validated = succeeded;
        }

        /// <summary>要求计划已成功校验且未提交，再把它标记为一次性消费。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated)
                throw new InvalidOperationException("中毒计划必须先完成成功校验。");
            if (_consumed)
                throw new InvalidOperationException("同一中毒计划不得重复提交。");

            _consumed = true;
        }
    }

    /// <summary>一次回合开始中毒触发冻结的生命、格挡与中毒终局。</summary>
    internal readonly struct BattlePoisonTickOutcome
    {
        /// <summary>触发前生命。</summary>
        internal int HealthBefore { get; }

        /// <summary>触发后生命。</summary>
        internal int HealthAfter { get; }

        /// <summary>本次实际损失的生命。</summary>
        internal int HealthLoss { get; }

        /// <summary>触发前格挡。</summary>
        internal int BlockBefore { get; }

        /// <summary>触发后格挡；中毒绕过格挡，因此与触发前相同。</summary>
        internal int BlockAfter { get; }

        /// <summary>触发前中毒层数。</summary>
        internal int PoisonBefore { get; }

        /// <summary>触发后中毒层数。</summary>
        internal int PoisonAfter { get; }

        /// <summary>指示本次触发是否产生生命或中毒写入。</summary>
        internal bool HasWrite => PoisonBefore > 0;

        /// <summary>指示本次触发是否把存活目标降至零生命。</summary>
        internal bool WasFatal => HealthBefore > 0 && HealthAfter == 0;

        /// <summary>从目标当前事实计算绕过格挡的生命损失与固定衰减一层的中毒终局。</summary>
        internal BattlePoisonTickOutcome(int healthBefore, int blockBefore, int poisonBefore)
        {
            if (healthBefore <= 0)
                throw new ArgumentOutOfRangeException(nameof(healthBefore));
            if (blockBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(blockBefore));
            if (poisonBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(poisonBefore));

            HealthBefore = healthBefore;
            HealthLoss = Math.Min(poisonBefore, healthBefore);
            HealthAfter = healthBefore - HealthLoss;
            BlockBefore = blockBefore;
            BlockAfter = blockBefore;
            PoisonBefore = poisonBefore;
            PoisonAfter = Math.Max(0, poisonBefore - 1);
        }
    }

    /// <summary>一次回合开始中毒触发预构建的稳定结果。</summary>
    internal sealed class BattlePoisonTickPreparationResult
    {
        /// <summary>预构建失败时的稳定原因；成功时为 None。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>成功时尚未写入的中毒触发计划；失败时为空。</summary>
        internal BattlePreparedPoisonTick Plan { get; }

        /// <summary>指示预构建是否已经得到可联合校验的计划。</summary>
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>冻结成功计划或失败结果，并拒绝互相矛盾的组合。</summary>
        internal BattlePoisonTickPreparationResult(
            BattleCommandExecutionFailureReason failureReason,
            BattlePreparedPoisonTick plan)
        {
            if ((failureReason == BattleCommandExecutionFailureReason.None) != (plan != null))
                throw new ArgumentException("中毒触发预构建结果与计划存在性不一致。", nameof(plan));

            FailureReason = failureReason;
            Plan = plan;
        }
    }

    /// <summary>首次写入前冻结目标四标量、中毒层数与触发终局的一次性计划。</summary>
    internal sealed class BattlePreparedPoisonTick
    {
        private bool _validationAttempted;
        private bool _validated;
        private bool _consumed;

        /// <summary>创建本计划的唯一通用中毒模块。</summary>
        internal BattlePoisonApplication Owner { get; }

        /// <summary>目标在触发起点的通用四标量快照。</summary>
        internal BattleCombatantScalarSnapshot TargetSnapshot { get; }

        /// <summary>本计划冻结的生命、格挡与中毒终局。</summary>
        internal BattlePoisonTickOutcome Outcome { get; }

        /// <summary>触发目标。</summary>
        internal CombatantId TargetId => TargetSnapshot.Id;

        /// <summary>触发前生命。</summary>
        internal int HealthBefore => Outcome.HealthBefore;

        /// <summary>触发后生命。</summary>
        internal int HealthAfter => Outcome.HealthAfter;

        /// <summary>本次实际损失的生命。</summary>
        internal int HealthLoss => Outcome.HealthLoss;

        /// <summary>触发前格挡。</summary>
        internal int BlockBefore => Outcome.BlockBefore;

        /// <summary>触发后格挡。</summary>
        internal int BlockAfter => Outcome.BlockAfter;

        /// <summary>触发前中毒层数。</summary>
        internal int PoisonBefore => Outcome.PoisonBefore;

        /// <summary>触发后中毒层数。</summary>
        internal int PoisonAfter => Outcome.PoisonAfter;

        /// <summary>指示本计划是否会产生生命、中毒写入与一条结算。</summary>
        internal bool HasWrite => Outcome.HasWrite;

        /// <summary>指示本计划是否会令目标死亡。</summary>
        internal bool WasFatal => Outcome.WasFatal;

        /// <summary>指示本计划是否已完成唯一一次成功校验。</summary>
        internal bool IsValidated => _validationAttempted && _validated;

        /// <summary>指示本计划是否已被提交入口消费。</summary>
        internal bool IsConsumed => _consumed;

        /// <summary>复制目标快照并冻结中毒触发终局，供联合事务一次校验与提交。</summary>
        internal BattlePreparedPoisonTick(BattlePoisonApplication owner, CombatantData target)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            TargetSnapshot = new BattleCombatantScalarSnapshot(target);
            Outcome = new BattlePoisonTickOutcome(
                target.CurrentHealth,
                target.CurrentBlock,
                target.CurrentPoison);
        }

        /// <summary>只记录一次校验结论，失败计划同样禁止被重复尝试。</summary>
        internal void MarkValidated(bool succeeded)
        {
            if (_validationAttempted)
                throw new InvalidOperationException("同一中毒触发计划不得重复校验。");

            _validationAttempted = true;
            _validated = succeeded;
        }

        /// <summary>要求计划已成功校验且未提交，再把它标记为一次性消费。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated)
                throw new InvalidOperationException("中毒触发计划必须先完成成功校验。");
            if (_consumed)
                throw new InvalidOperationException("同一中毒触发计划不得重复提交。");

            _consumed = true;
        }
    }

    /// <summary>集中拥有通用中毒施加与回合开始触发的纯预构建、一次校验及无失败提交协议。</summary>
    internal sealed class BattlePoisonApplication
    {
        private static readonly IReadOnlyList<BattleSettlementRecord> NoSettlements =
            Array.Empty<BattleSettlementRecord>();

        private readonly BattleCombatantsData _combatants;

        /// <summary>绑定本场唯一参与者聚合，不创建第二份中毒状态。</summary>
        internal BattlePoisonApplication(BattleCombatantsData combatants)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        }

        /// <summary>从当前权威快照冻结一次非负中毒累加，过程不写入任何事实。</summary>
        internal BattlePoisonApplicationPreparationResult PrepareApply(
            CombatantId sourceId,
            CombatantId targetId,
            int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (!_combatants.TryGet(sourceId, out CombatantData source))
            {
                return new BattlePoisonApplicationPreparationResult(
                    BattleCommandExecutionFailureReason.EffectSourceNotFound,
                    plan: null);
            }
            if (!source.IsAlive)
            {
                return new BattlePoisonApplicationPreparationResult(
                    BattleCommandExecutionFailureReason.EffectSourceNotAlive,
                    plan: null);
            }
            if (!_combatants.TryGet(targetId, out CombatantData target))
            {
                return new BattlePoisonApplicationPreparationResult(
                    BattleCommandExecutionFailureReason.TargetNotFound,
                    plan: null);
            }
            if (!target.IsAlive)
            {
                return new BattlePoisonApplicationPreparationResult(
                    BattleCommandExecutionFailureReason.TargetNotAlive,
                    plan: null);
            }

            int poisonBefore = target.CurrentPoison;
            int poisonAfter = checked(poisonBefore + amount);
            return new BattlePoisonApplicationPreparationResult(
                BattleCommandExecutionFailureReason.None,
                new BattlePreparedPoisonApplication(
                    this,
                    source,
                    target,
                    poisonBefore,
                    poisonAfter));
        }

        /// <summary>从当前权威快照冻结一次回合开始中毒触发，过程不写入任何事实。</summary>
        internal BattlePoisonTickPreparationResult PrepareTick(CombatantId targetId)
        {
            if (!_combatants.TryGet(targetId, out CombatantData target))
            {
                return new BattlePoisonTickPreparationResult(
                    BattleCommandExecutionFailureReason.TargetNotFound,
                    plan: null);
            }
            if (!target.IsAlive)
            {
                return new BattlePoisonTickPreparationResult(
                    BattleCommandExecutionFailureReason.TargetNotAlive,
                    plan: null);
            }

            return new BattlePoisonTickPreparationResult(
                BattleCommandExecutionFailureReason.None,
                new BattlePreparedPoisonTick(this, target));
        }

        /// <summary>首次写入前一次核对计划归属、参与者快照与目标中毒事实。</summary>
        internal bool ValidatePrepared(BattlePreparedPoisonApplication plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能校验其他中毒模块创建的计划。");

            bool succeeded = !plan.IsConsumed &&
                _combatants.TryGet(plan.SourceSnapshot.Id, out CombatantData source) &&
                _combatants.TryGet(plan.TargetSnapshot.Id, out CombatantData target) &&
                source.IsAlive &&
                target.IsAlive &&
                plan.SourceSnapshot.Matches(source) &&
                plan.TargetSnapshot.Matches(target) &&
                target.CurrentPoison == plan.PoisonBefore;
            plan.MarkValidated(succeeded);
            return succeeded;
        }

        /// <summary>首次写入前一次核对触发计划归属、目标快照与中毒事实。</summary>
        internal bool ValidatePreparedTick(BattlePreparedPoisonTick plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能校验其他中毒模块创建的触发计划。");

            bool succeeded = !plan.IsConsumed &&
                _combatants.TryGet(plan.TargetId, out CombatantData target) &&
                target.IsAlive &&
                plan.TargetSnapshot.Matches(target) &&
                target.CurrentPoison == plan.PoisonBefore;
            plan.MarkValidated(succeeded);
            return succeeded;
        }

        /// <summary>提交已经联合校验的中毒计划；零施加量保持零写入与零结算。</summary>
        internal IReadOnlyList<BattleSettlementRecord> CommitPrepared(
            BattlePreparedPoisonApplication plan,
            int startingOrder)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他中毒模块创建的计划。");

            plan.MarkConsumed();
            if (!plan.HasWrite)
                return NoSettlements;
            if (!_combatants.TryGet(plan.TargetSnapshot.Id, out CombatantData target))
                throw new InvalidOperationException("已验证的中毒目标不再存在。");

            target.ApplyPoisonValue(plan.PoisonBefore, plan.PoisonAfter);
            return new ReadOnlyCollection<BattleSettlementRecord>(
                new BattleSettlementRecord[]
                {
                    new BattleStatusAppliedSettlement(
                        startingOrder,
                        effectId: null,
                        plan.SourceSnapshot.Id,
                        plan.TargetSnapshot.Id,
                        BattleStatusType.Poison,
                        plan.PoisonBefore,
                        plan.PoisonAfter),
                });
        }

        /// <summary>提交已经联合校验的中毒触发计划；零层计划保持零写入与零结算。</summary>
        internal IReadOnlyList<BattleSettlementRecord> CommitPreparedTick(
            BattlePreparedPoisonTick plan,
            int startingOrder)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他中毒模块创建的触发计划。");

            plan.MarkConsumed();
            if (!plan.HasWrite)
                return NoSettlements;
            if (!_combatants.TryGet(plan.TargetId, out CombatantData target))
                throw new InvalidOperationException("已验证的中毒触发目标不再存在。");

            target.ApplyPoisonTickOutcome(plan.Outcome);
            return new ReadOnlyCollection<BattleSettlementRecord>(
                new BattleSettlementRecord[]
                {
                    new BattlePoisonTickedSettlement(
                        startingOrder,
                        plan.TargetId,
                        plan.Outcome),
                });
        }
    }
}
