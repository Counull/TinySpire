using System;

namespace TinySpire.Battle
{
    /// <summary>声明一张卡本次是否实际支付共享能量费用；职业规则只负责选择模式。</summary>
    internal enum BattleCardPaymentMode
    {
        Normal = 0,
        Waived = 1,
    }

    /// <summary>冻结共享能量费用的实际支付、效果取值与名义触发值，避免免费卡混用三个语义。</summary>
    internal readonly struct BattleCardEnergyCostResolution
    {
        /// <summary>本次成功出牌实际从玩家资源中扣除的能量。</summary>
        internal int ActualEnergySpent { get; }

        /// <summary>X 费效果在命令开始时冻结的取值；固定费用卡固定为零。</summary>
        internal int EffectValue { get; }

        /// <summary>供“视为原本消耗”等触发读取的名义能量费用。</summary>
        internal int NominalEnergySpentForTriggers { get; }

        /// <summary>创建一份已经完成非负校验的共享能量费用结果。</summary>
        internal BattleCardEnergyCostResolution(
            int actualEnergySpent,
            int effectValue,
            int nominalEnergySpentForTriggers)
        {
            if (actualEnergySpent < 0)
                throw new ArgumentOutOfRangeException(nameof(actualEnergySpent));
            if (effectValue < 0)
                throw new ArgumentOutOfRangeException(nameof(effectValue));
            if (nominalEnergySpentForTriggers < 0)
                throw new ArgumentOutOfRangeException(nameof(nominalEnergySpentForTriggers));

            ActualEnergySpent = actualEnergySpent;
            EffectValue = effectValue;
            NominalEnergySpentForTriggers = nominalEnergySpentForTriggers;
        }
    }

    /// <summary>集中解析 Fixed/X 的纯能量费用数学，不持有职业状态，也不决定免费效果的生命周期。</summary>
    internal static class BattleCardCostResolver
    {
        /// <summary>按命令开始时能量与支付模式解析实际、效果和名义费用；失败时不写入任何事实。</summary>
        internal static bool TryResolveEnergy(
            cfg.battle.CardCostKind costKind,
            int configuredCost,
            int availableEnergy,
            BattleCardPaymentMode paymentMode,
            out BattleCardEnergyCostResolution resolution,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (availableEnergy < 0)
                throw new ArgumentOutOfRangeException(nameof(availableEnergy));
            if (!Enum.IsDefined(typeof(BattleCardPaymentMode), paymentMode))
                throw new ArgumentOutOfRangeException(nameof(paymentMode));

            int nominalEnergySpent;
            int effectValue;
            switch (costKind)
            {
                case cfg.battle.CardCostKind.Fixed:
                    if (configuredCost < 0)
                    {
                        resolution = default;
                        failureReason = BattleCommandExecutionFailureReason.CardTemplateNotFound;
                        return false;
                    }

                    nominalEnergySpent = configuredCost;
                    effectValue = 0;
                    break;
                case cfg.battle.CardCostKind.X:
                    nominalEnergySpent = availableEnergy;
                    effectValue = availableEnergy;
                    break;
                default:
                    resolution = default;
                    failureReason = BattleCommandExecutionFailureReason.CardTemplateNotFound;
                    return false;
            }

            int actualEnergySpent = paymentMode == BattleCardPaymentMode.Waived
                ? 0
                : nominalEnergySpent;
            if (actualEnergySpent > availableEnergy)
            {
                resolution = default;
                failureReason = BattleCommandExecutionFailureReason.InsufficientEnergy;
                return false;
            }

            resolution = new BattleCardEnergyCostResolution(
                actualEnergySpent,
                effectValue,
                nominalEnergySpent);
            failureReason = BattleCommandExecutionFailureReason.None;
            return true;
        }
    }
}
