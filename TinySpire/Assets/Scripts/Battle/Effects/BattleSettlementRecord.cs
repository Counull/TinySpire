using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>
    /// 一次权威命令内按发生顺序冻结的结算记录基类。
    /// </summary>
    public abstract class BattleSettlementRecord
    {
        /// <summary>命令内从零开始的稳定记录顺序。</summary>
        public int Order { get; }

        /// <summary>记录的可辨识类别。</summary>
        public BattleSettlementRecordType RecordType { get; }

        /// <summary>产生此记录的 Effect；非 Effect 操作为空。</summary>
        public BattleEffectId? EffectId { get; }

        /// <summary>操作来源参与者；卡区操作等记录可为空。</summary>
        public CombatantId? SourceId { get; }

        /// <summary>操作目标参与者；卡区操作等记录可为空。</summary>
        public CombatantId? TargetId { get; }

        /// <summary>冻结所有记录共有的顺序与关联标识。</summary>
        internal BattleSettlementRecord(
            int order,
            BattleSettlementRecordType recordType,
            BattleEffectId? effectId,
            CombatantId? sourceId,
            CombatantId? targetId)
        {
            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            Order = order;
            RecordType = recordType;
            EffectId = effectId;
            SourceId = sourceId;
            TargetId = targetId;
        }
    }

    /// <summary>成功支付出牌能量的不可变记录。</summary>
    public sealed class BattleEnergySpentSettlement : BattleSettlementRecord
    {
        /// <summary>支付前能量。</summary>
        public int EnergyBefore { get; }

        /// <summary>支付后能量。</summary>
        public int EnergyAfter { get; }

        /// <summary>实际支付量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次能量支付结果。</summary>
        internal BattleEnergySpentSettlement(
            int order,
            CombatantId sourceId,
            int energyBefore,
            int energyAfter)
            : base(
                order,
                BattleSettlementRecordType.EnergySpent,
                null,
                sourceId,
                null)
        {
            EnergyBefore = energyBefore;
            EnergyAfter = energyAfter;
            Amount = energyBefore - energyAfter;
        }
    }

    /// <summary>一次伤害对格挡与生命造成的不可变结果。</summary>
    public sealed class BattleDamageAppliedSettlement : BattleSettlementRecord
    {
        /// <summary>公式计算后的攻击值。</summary>
        public int AttackValue { get; }

        /// <summary>伤害前格挡。</summary>
        public int BlockBefore { get; }

        /// <summary>伤害后格挡。</summary>
        public int BlockAfter { get; }

        /// <summary>实际吸收伤害的格挡量。</summary>
        public int BlockAbsorbed { get; }

        /// <summary>伤害前生命。</summary>
        public int HealthBefore { get; }

        /// <summary>伤害后生命。</summary>
        public int HealthAfter { get; }

        /// <summary>实际生命损失。</summary>
        public int HealthLoss { get; }

        /// <summary>此次伤害是否令原本存活的目标死亡。</summary>
        public bool WasFatal { get; }

        /// <summary>冻结一次伤害对格挡和生命的完整结果。</summary>
        internal BattleDamageAppliedSettlement(
            int order,
            BattleEffectId effectId,
            CombatantId sourceId,
            CombatantId targetId,
            int attackValue,
            int blockBefore,
            int blockAfter,
            int healthBefore,
            int healthAfter)
            : base(
                order,
                BattleSettlementRecordType.DamageApplied,
                effectId,
                sourceId,
                targetId)
        {
            AttackValue = attackValue;
            BlockBefore = blockBefore;
            BlockAfter = blockAfter;
            BlockAbsorbed = blockBefore - blockAfter;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
            HealthLoss = healthBefore - healthAfter;
            WasFatal = healthBefore > 0 && healthAfter == 0;
        }
    }

    /// <summary>一次格挡累加的不可变结果。</summary>
    public sealed class BattleBlockGainedSettlement : BattleSettlementRecord
    {
        /// <summary>操作前格挡。</summary>
        public int BlockBefore { get; }

        /// <summary>操作后格挡。</summary>
        public int BlockAfter { get; }

        /// <summary>实际增加量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次格挡累加结果。</summary>
        internal BattleBlockGainedSettlement(
            int order,
            BattleEffectId effectId,
            CombatantId sourceId,
            CombatantId targetId,
            int blockBefore,
            int blockAfter)
            : base(
                order,
                BattleSettlementRecordType.BlockGained,
                effectId,
                sourceId,
                targetId)
        {
            BlockBefore = blockBefore;
            BlockAfter = blockAfter;
            Amount = blockAfter - blockBefore;
        }
    }

    /// <summary>一次属性修改的不可变结果。</summary>
    public sealed class BattleAttributeModifiedSettlement : BattleSettlementRecord
    {
        /// <summary>被修改的属性。</summary>
        public BattleAttributeType Attribute { get; }

        /// <summary>操作前属性值。</summary>
        public int ValueBefore { get; }

        /// <summary>操作后属性值。</summary>
        public int ValueAfter { get; }

        /// <summary>实际有符号变化量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次属性修改结果。</summary>
        internal BattleAttributeModifiedSettlement(
            int order,
            BattleEffectId effectId,
            CombatantId sourceId,
            CombatantId targetId,
            BattleAttributeType attribute,
            int valueBefore,
            int valueAfter)
            : base(
                order,
                BattleSettlementRecordType.AttributeModified,
                effectId,
                sourceId,
                targetId)
        {
            Attribute = attribute;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
            Amount = valueAfter - valueBefore;
        }
    }

    /// <summary>一次状态累加的不可变结果。</summary>
    public sealed class BattleStatusAppliedSettlement : BattleSettlementRecord
    {
        /// <summary>被施加的状态。</summary>
        public BattleStatusType Status { get; }

        /// <summary>操作前状态值。</summary>
        public int ValueBefore { get; }

        /// <summary>操作后状态值。</summary>
        public int ValueAfter { get; }

        /// <summary>实际施加量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次状态累加结果。</summary>
        internal BattleStatusAppliedSettlement(
            int order,
            BattleEffectId effectId,
            CombatantId sourceId,
            CombatantId targetId,
            BattleStatusType status,
            int valueBefore,
            int valueAfter)
            : base(
                order,
                BattleSettlementRecordType.StatusApplied,
                effectId,
                sourceId,
                targetId)
        {
            Status = status;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
            Amount = valueAfter - valueBefore;
        }
    }

    /// <summary>一张卡牌在两个权威卡区之间移动的不可变记录。</summary>
    public sealed class BattleCardMovedSettlement : BattleSettlementRecord
    {
        /// <summary>发生移动的卡牌实例。</summary>
        public CardInstanceId CardId { get; }

        /// <summary>来源卡区。</summary>
        public BattleCardZone FromZone { get; }

        /// <summary>目标卡区。</summary>
        public BattleCardZone ToZone { get; }

        /// <summary>冻结一次卡牌移动。</summary>
        internal BattleCardMovedSettlement(
            int order,
            CardInstanceId cardId,
            BattleCardZone fromZone,
            BattleCardZone toZone)
            : base(
                order,
                BattleSettlementRecordType.CardMoved,
                null,
                null,
                null)
        {
            CardId = cardId;
            FromZone = fromZone;
            ToZone = toZone;
        }
    }

    /// <summary>弃牌堆重洗后新抽牌堆稳定顺序的不可变记录。</summary>
    public sealed class BattleCardsReshuffledSettlement : BattleSettlementRecord
    {
        /// <summary>重洗后从索引零开始的完整抽牌堆顺序。</summary>
        public IReadOnlyList<CardInstanceId> NewDrawPileOrder { get; }

        /// <summary>复制并冻结重洗后的抽牌堆顺序。</summary>
        internal BattleCardsReshuffledSettlement(
            int order,
            IEnumerable<CardInstanceId> newDrawPileOrder)
            : base(
                order,
                BattleSettlementRecordType.CardsReshuffled,
                null,
                null,
                null)
        {
            if (newDrawPileOrder == null)
            {
                throw new ArgumentNullException(nameof(newDrawPileOrder));
            }

            NewDrawPileOrder = new ReadOnlyCollection<CardInstanceId>(
                new List<CardInstanceId>(newDrawPileOrder));
        }
    }

    /// <summary>成功命令中因明确规则跳过一个操作的不可变记录。</summary>
    public sealed class BattleOperationSkippedSettlement : BattleSettlementRecord
    {
        /// <summary>此次操作未执行的原因。</summary>
        public BattleOperationSkipReason Reason { get; }

        /// <summary>冻结一次已知原因的操作跳过结果。</summary>
        internal BattleOperationSkippedSettlement(
            int order,
            BattleEffectId effectId,
            CombatantId sourceId,
            CombatantId targetId,
            BattleOperationSkipReason reason)
            : base(
                order,
                BattleSettlementRecordType.OperationSkipped,
                effectId,
                sourceId,
                targetId)
        {
            Reason = reason;
        }
    }
}
