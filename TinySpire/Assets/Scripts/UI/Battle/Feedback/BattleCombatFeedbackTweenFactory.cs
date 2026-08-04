using System;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>把一个冻结表现步骤转换为精确参与者可消费的纯表现提示。</summary>
    internal sealed class BattleCombatFeedbackCue
    {
        /// <summary>应消费本提示的唯一参与者。</summary>
        public CombatantId TargetId { get; }

        /// <summary>提示沿用的表现步骤类别。</summary>
        public BattleCommandPresentationStepKind Kind { get; }

        /// <summary>来自冻结 settlement 的实际显示量；无数字提示时为零。</summary>
        public int Amount { get; }

        /// <summary>状态与意图脉冲消费的该条冻结后值；纯数字及姿态提示为空。</summary>
        public int? FrozenValue { get; }

        /// <summary>冻结一次只读战斗反馈提示。</summary>
        internal BattleCombatFeedbackCue(
            CombatantId targetId,
            BattleCommandPresentationStepKind kind,
            int amount,
            int? frozenValue = null)
        {
            TargetId = targetId;
            Kind = kind;
            Amount = amount;
            FrozenValue = frozenValue;
        }
    }

    /// <summary>把 M9C 的冻结 settlement 步骤路由到唯一 concrete View Tween。</summary>
    internal sealed class BattleCombatFeedbackTweenFactory
    {
        private readonly Func<BattleCombatFeedbackCue, BattleCommandPresentationTween> _createTween;

        /// <summary>保存由唯一参与者映射提供的 concrete Tween 创建入口。</summary>
        internal BattleCombatFeedbackTweenFactory(
            Func<BattleCombatFeedbackCue, BattleCommandPresentationTween> createTween)
        {
            _createTween = createTween ?? throw new ArgumentNullException(nameof(createTween));
        }

        /// <summary>只消费已支持的 M9C 步骤；其他步骤留给同一 runner 的既有分派。</summary>
        internal bool TryCreate(
            BattleCommandPresentationStep step,
            out BattleCommandPresentationTween tween)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));

            tween = null;
            CombatantId? targetId;
            int amount;
            int? frozenValue = null;
            switch (step.Kind)
            {
                case BattleCommandPresentationStepKind.BlockAbsorbedNumber:
                    if (!(step.Settlement is BattleDamageAppliedSettlement blockDamage))
                        throw CreateSettlementMismatch(step);
                    targetId = blockDamage.TargetId;
                    amount = blockDamage.BlockAbsorbed;
                    break;
                case BattleCommandPresentationStepKind.HealthLossNumber:
                    if (!(step.Settlement is BattleDamageAppliedSettlement healthDamage))
                        throw CreateSettlementMismatch(step);
                    targetId = healthDamage.TargetId;
                    amount = healthDamage.HealthLoss;
                    break;
                case BattleCommandPresentationStepKind.HitShake:
                    if (!(step.Settlement is BattleDamageAppliedSettlement shakeDamage))
                        throw CreateSettlementMismatch(step);
                    targetId = shakeDamage.TargetId;
                    amount = 0;
                    break;
                case BattleCommandPresentationStepKind.DeathTransition:
                    if (!(step.Settlement is BattleDamageAppliedSettlement fatalDamage)
                        || !fatalDamage.WasFatal)
                    {
                        throw CreateSettlementMismatch(step);
                    }
                    targetId = fatalDamage.TargetId;
                    amount = 0;
                    break;
                case BattleCommandPresentationStepKind.BlockGainedNumber:
                    if (!(step.Settlement is BattleBlockGainedSettlement blockGained))
                        throw CreateSettlementMismatch(step);
                    targetId = blockGained.TargetId;
                    amount = blockGained.Amount;
                    break;
                case BattleCommandPresentationStepKind.StrengthIconPulse:
                    if (!(step.Settlement is BattleAttributeModifiedSettlement attributeModified))
                        throw CreateSettlementMismatch(step);
                    targetId = attributeModified.TargetId;
                    amount = 0;
                    frozenValue = attributeModified.ValueAfter;
                    break;
                case BattleCommandPresentationStepKind.VulnerableIconPulse:
                    if (step.Settlement is BattleStatusAppliedSettlement statusApplied)
                    {
                        targetId = statusApplied.TargetId;
                        frozenValue = statusApplied.ValueAfter;
                    }
                    else if (step.Settlement is BattleStatusReducedSettlement statusReduced)
                    {
                        targetId = statusReduced.TargetId;
                        frozenValue = statusReduced.ValueAfter;
                    }
                    else
                        throw CreateSettlementMismatch(step);
                    amount = 0;
                    break;
                case BattleCommandPresentationStepKind.EnemyIntentPulse:
                    if (!(step.Settlement is BattleEnemyIntentAdvancedSettlement intentAdvanced))
                        throw CreateSettlementMismatch(step);
                    targetId = intentAdvanced.SourceId;
                    amount = 0;
                    frozenValue = intentAdvanced.NextBehaviorId;
                    break;
                default:
                    return false;
            }

            if (!targetId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Combat feedback {step.Kind} requires a frozen participant identity.");
            }

            var cue = new BattleCombatFeedbackCue(
                targetId.Value,
                step.Kind,
                amount,
                frozenValue);
            tween = _createTween.Invoke(cue)
                ?? throw new InvalidOperationException("Combat feedback tween factory returned null.");
            return true;
        }

        /// <summary>为计划类别与冻结 settlement 类型不匹配建立一致的同步 fault。</summary>
        private static InvalidOperationException CreateSettlementMismatch(
            BattleCommandPresentationStep step)
        {
            return new InvalidOperationException(
                $"Combat feedback {step.Kind} cannot consume {step.Settlement.GetType().Name}.");
        }
    }
}
