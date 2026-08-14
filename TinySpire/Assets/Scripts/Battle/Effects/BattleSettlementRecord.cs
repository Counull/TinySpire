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

    /// <summary>卡牌效果即时获得能量的不可变记录，与回合开始的基础补给语义分离。</summary>
    public sealed class BattleEnergyGainedSettlement : BattleSettlementRecord
    {
        /// <summary>获得前能量。</summary>
        public int EnergyBefore { get; }

        /// <summary>获得后能量。</summary>
        public int EnergyAfter { get; }

        /// <summary>本次实际获得量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次受硬上限约束的即时能量获得结果。</summary>
        internal BattleEnergyGainedSettlement(
            int order,
            CombatantId sourceId,
            int energyBefore,
            int energyAfter)
            : base(
                order,
                BattleSettlementRecordType.EnergyGained,
                null,
                sourceId,
                null)
        {
            if (energyBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(energyBefore));
            if (energyAfter <= energyBefore)
                throw new ArgumentOutOfRangeException(nameof(energyAfter));

            EnergyBefore = energyBefore;
            EnergyAfter = energyAfter;
            Amount = energyAfter - energyBefore;
        }
    }

    /// <summary>成功支付卡牌弹药消耗的不可变记录。</summary>
    public sealed class BattleAmmoSpentSettlement : BattleSettlementRecord
    {
        /// <summary>支付前弹药。</summary>
        public int AmmoBefore { get; }

        /// <summary>支付后弹药。</summary>
        public int AmmoAfter { get; }

        /// <summary>实际支付量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次弹药支付结果。</summary>
        internal BattleAmmoSpentSettlement(
            int order,
            CombatantId sourceId,
            int ammoBefore,
            int ammoAfter)
            : base(
                order,
                BattleSettlementRecordType.AmmoSpent,
                null,
                sourceId,
                null)
        {
            if (ammoBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(ammoBefore));
            if (ammoAfter < 0 || ammoAfter > ammoBefore)
                throw new ArgumentOutOfRangeException(nameof(ammoAfter));

            AmmoBefore = ammoBefore;
            AmmoAfter = ammoAfter;
            Amount = ammoBefore - ammoAfter;
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
            BattleEffectId? effectId,
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

    /// <summary>一次回合开始中毒触发产生的绕过格挡生命损失与层数衰减记录。</summary>
    public sealed class BattlePoisonTickedSettlement : BattleSettlementRecord
    {
        /// <summary>触发前生命。</summary>
        public int HealthBefore { get; }

        /// <summary>触发后生命。</summary>
        public int HealthAfter { get; }

        /// <summary>本次实际损失的生命。</summary>
        public int HealthLoss { get; }

        /// <summary>触发前格挡。</summary>
        public int BlockBefore { get; }

        /// <summary>触发后格挡；中毒绕过格挡，因此与触发前相同。</summary>
        public int BlockAfter { get; }

        /// <summary>触发前中毒层数。</summary>
        public int PoisonBefore { get; }

        /// <summary>触发后中毒层数。</summary>
        public int PoisonAfter { get; }

        /// <summary>此次触发是否令原本存活的目标死亡。</summary>
        public bool WasFatal { get; }

        /// <summary>冻结一次中毒触发的生命、格挡与层数终局；来源和目标都归因于触发者自身。</summary>
        internal BattlePoisonTickedSettlement(
            int order,
            CombatantId targetId,
            BattlePoisonTickOutcome outcome)
            : base(
                order,
                BattleSettlementRecordType.PoisonTicked,
                effectId: null,
                sourceId: targetId,
                targetId: targetId)
        {
            HealthBefore = outcome.HealthBefore;
            HealthAfter = outcome.HealthAfter;
            HealthLoss = outcome.HealthLoss;
            BlockBefore = outcome.BlockBefore;
            BlockAfter = outcome.BlockAfter;
            PoisonBefore = outcome.PoisonBefore;
            PoisonAfter = outcome.PoisonAfter;
            WasFatal = outcome.WasFatal;
        }
    }

    /// <summary>一次受生命上限约束的治疗产生的不可变结果。</summary>
    public sealed class BattleHealthRestoredSettlement : BattleSettlementRecord
    {
        /// <summary>归一化后请求恢复的非负生命量。</summary>
        public int RequestedAmount { get; }

        /// <summary>治疗前生命。</summary>
        public int HealthBefore { get; }

        /// <summary>治疗后生命。</summary>
        public int HealthAfter { get; }

        /// <summary>受生命上限约束后实际恢复的生命量，可为零。</summary>
        public int Amount { get; }

        /// <summary>冻结一次治疗的请求量、前后生命与实际恢复量。</summary>
        internal BattleHealthRestoredSettlement(
            int order,
            BattleEffectId? effectId,
            CombatantId sourceId,
            CombatantId targetId,
            BattleHealthRestorationOutcome outcome)
            : base(
                order,
                BattleSettlementRecordType.HealthRestored,
                effectId,
                sourceId,
                targetId)
        {
            RequestedAmount = outcome.RequestedAmount;
            HealthBefore = outcome.HealthBefore;
            HealthAfter = outcome.HealthAfter;
            Amount = outcome.Amount;
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
            BattleEffectId? effectId,
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
            BattleEffectId? effectId,
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

    /// <summary>一张战斗内临时卡牌实例被创建并直接进入指定权威卡区的不可变记录。</summary>
    public sealed class BattleCardCreatedSettlement : BattleSettlementRecord
    {
        /// <summary>本次创建得到的战斗内卡牌实例。</summary>
        public CardInstanceId CardId { get; }

        /// <summary>新实例引用的静态卡牌模板。</summary>
        public int TemplateId { get; }

        /// <summary>新实例创建后直接进入的权威卡区。</summary>
        public BattleCardZone ToZone { get; }

        /// <summary>冻结一次不伪造成跨卡区移动的战斗内卡牌创建。</summary>
        internal BattleCardCreatedSettlement(
            int order,
            CardInstanceId cardId,
            int templateId,
            BattleCardZone toZone)
            : base(
                order,
                BattleSettlementRecordType.CardCreated,
                null,
                null,
                null)
        {
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (toZone != BattleCardZone.Hand)
                throw new ArgumentOutOfRangeException(nameof(toZone));

            CardId = cardId;
            TemplateId = templateId;
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

    /// <summary>参与者行动时机清除全部格挡的不可变记录。</summary>
    public sealed class BattleBlockClearedSettlement : BattleSettlementRecord
    {
        /// <summary>清理前格挡。</summary>
        public int BlockBefore { get; }

        /// <summary>清理后格挡；M8 契约固定为零。</summary>
        public int BlockAfter { get; }

        /// <summary>实际清除量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次非零格挡清理。</summary>
        internal BattleBlockClearedSettlement(
            int order,
            CombatantId targetId,
            int blockBefore)
            : base(
                order,
                BattleSettlementRecordType.BlockCleared,
                null,
                null,
                targetId)
        {
            if (blockBefore <= 0)
                throw new ArgumentOutOfRangeException(nameof(blockBefore));

            BlockBefore = blockBefore;
            BlockAfter = 0;
            Amount = blockBefore;
        }
    }

    /// <summary>参与者行动时机衰减一层状态的不可变记录。</summary>
    public sealed class BattleStatusReducedSettlement : BattleSettlementRecord
    {
        /// <summary>被衰减的状态。</summary>
        public BattleStatusType Status { get; }

        /// <summary>衰减前状态值。</summary>
        public int ValueBefore { get; }

        /// <summary>衰减后状态值。</summary>
        public int ValueAfter { get; }

        /// <summary>实际减少量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次正数状态衰减。</summary>
        internal BattleStatusReducedSettlement(
            int order,
            CombatantId targetId,
            BattleStatusType status,
            int valueBefore,
            int valueAfter)
            : base(
                order,
                BattleSettlementRecordType.StatusReduced,
                null,
                null,
                targetId)
        {
            if (valueBefore <= valueAfter || valueAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(valueAfter));

            Status = status;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
            Amount = valueBefore - valueAfter;
        }
    }

    /// <summary>玩家新一轮恢复基础能量的不可变记录。</summary>
    public sealed class BattleEnergyRefilledSettlement : BattleSettlementRecord
    {
        /// <summary>恢复前能量。</summary>
        public int EnergyBefore { get; }

        /// <summary>恢复后能量。</summary>
        public int EnergyAfter { get; }

        /// <summary>本次能量的有符号变化量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次基础能量恢复。</summary>
        internal BattleEnergyRefilledSettlement(
            int order,
            CombatantId sourceId,
            int energyBefore,
            int energyAfter)
            : base(
                order,
                BattleSettlementRecordType.EnergyRefilled,
                null,
                sourceId,
                null)
        {
            if (energyBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(energyBefore));
            if (energyAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(energyAfter));

            EnergyBefore = energyBefore;
            EnergyAfter = energyAfter;
            Amount = energyAfter - energyBefore;
        }
    }

    /// <summary>玩家在新一轮补充基础弹药的不可变记录。</summary>
    public sealed class BattleAmmoRefilledSettlement : BattleSettlementRecord
    {
        /// <summary>补充前弹药。</summary>
        public int AmmoBefore { get; }

        /// <summary>补充后弹药。</summary>
        public int AmmoAfter { get; }

        /// <summary>本次弹药的有符号变化量。</summary>
        public int Amount { get; }

        /// <summary>冻结一次基础弹药补充。</summary>
        internal BattleAmmoRefilledSettlement(
            int order,
            CombatantId sourceId,
            int ammoBefore,
            int ammoAfter)
            : base(
                order,
                BattleSettlementRecordType.AmmoRefilled,
                null,
                sourceId,
                null)
        {
            if (ammoBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(ammoBefore));
            if (ammoAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(ammoAfter));

            AmmoBefore = ammoBefore;
            AmmoAfter = ammoAfter;
            Amount = ammoAfter - ammoBefore;
        }
    }

    /// <summary>敌人完成当前行为并提交下一权威意图的不可变记录。</summary>
    public sealed class BattleEnemyIntentAdvancedSettlement : BattleSettlementRecord
    {
        /// <summary>本次已经完成的行为模板标识。</summary>
        public int CompletedBehaviorId { get; }

        /// <summary>完成后提交的下一行为模板标识。</summary>
        public int NextBehaviorId { get; }

        /// <summary>冻结一次敌人意图推进。</summary>
        internal BattleEnemyIntentAdvancedSettlement(
            int order,
            CombatantId enemyId,
            int completedBehaviorId,
            int nextBehaviorId)
            : base(
                order,
                BattleSettlementRecordType.EnemyIntentAdvanced,
                null,
                enemyId,
                null)
        {
            if (completedBehaviorId <= 0)
                throw new ArgumentOutOfRangeException(nameof(completedBehaviorId));
            if (nextBehaviorId <= 0)
                throw new ArgumentOutOfRangeException(nameof(nextBehaviorId));

            CompletedBehaviorId = completedBehaviorId;
            NextBehaviorId = nextBehaviorId;
        }
    }

    /// <summary>排队后来源死亡而跳过整次敌人行动的不可变记录。</summary>
    public sealed class BattleEnemyActionSkippedSettlement : BattleSettlementRecord
    {
        /// <summary>整次行动未执行的原因。</summary>
        public BattleEnemyActionSkipReason Reason { get; }

        /// <summary>冻结一次只关联行动来源、不伪造目标或 Effect 的敌人行动跳过。</summary>
        internal BattleEnemyActionSkippedSettlement(
            int order,
            CombatantId sourceId,
            BattleEnemyActionSkipReason reason)
            : base(
                order,
                BattleSettlementRecordType.EnemyActionSkipped,
                null,
                sourceId,
                null)
        {
            Reason = reason;
        }
    }

    /// <summary>一条命令至多一次的权威阶段变化不可变记录。</summary>
    public sealed class BattlePhaseChangedSettlement : BattleSettlementRecord
    {
        /// <summary>变化前阶段。</summary>
        public BattleTurnPhase PhaseBefore { get; }

        /// <summary>变化后阶段。</summary>
        public BattleTurnPhase PhaseAfter { get; }

        /// <summary>变化前轮次。</summary>
        public int RoundNumberBefore { get; }

        /// <summary>变化后轮次。</summary>
        public int RoundNumberAfter { get; }

        /// <summary>变化前的当前行动敌人。</summary>
        public CombatantId? CurrentActingEnemyIdBefore { get; }

        /// <summary>变化后的当前行动敌人。</summary>
        public CombatantId? CurrentActingEnemyIdAfter { get; }

        /// <summary>冻结一次完整阶段变化。</summary>
        internal BattlePhaseChangedSettlement(
            int order,
            BattleTurnPhase phaseBefore,
            BattleTurnPhase phaseAfter,
            int roundNumberBefore,
            int roundNumberAfter,
            CombatantId? currentActingEnemyIdBefore,
            CombatantId? currentActingEnemyIdAfter)
            : base(
                order,
                BattleSettlementRecordType.BattlePhaseChanged,
                null,
                null,
                null)
        {
            if (phaseBefore == phaseAfter &&
                roundNumberBefore == roundNumberAfter &&
                currentActingEnemyIdBefore == currentActingEnemyIdAfter)
            {
                throw new ArgumentException("阶段变化记录必须包含至少一项权威回合事实变化。");
            }

            if (roundNumberBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(roundNumberBefore));
            if (roundNumberAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(roundNumberAfter));

            PhaseBefore = phaseBefore;
            PhaseAfter = phaseAfter;
            RoundNumberBefore = roundNumberBefore;
            RoundNumberAfter = roundNumberAfter;
            CurrentActingEnemyIdBefore = currentActingEnemyIdBefore;
            CurrentActingEnemyIdAfter = currentActingEnemyIdAfter;
        }
    }
}
