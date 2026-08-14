using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>命令级前奏的互斥类别。</summary>
    internal enum BattleCommandPreludeKind
    {
        StartBattle,
        PlayCard,
    }

    /// <summary>由冻结结算记录派生的可见表现步骤类别。</summary>
    internal enum BattleCommandPresentationStepKind
    {
        BlockAbsorbedNumber,
        HealthLossNumber,
        HealthRestoredNumber,
        HitShake,
        DeathTransition,
        BlockGainedNumber,
        StrengthIconPulse,
        VulnerableIconPulse,
        CardMoved,
        CardCreated,
        CardsReshuffled,
        EnemyIntentPulse,
        PlayerTurnBanner,
        EnemyTurnBanner,
        BattleOutcome,
    }

    /// <summary>独立于 settlement、且每条命令至多一个的不可变前奏。</summary>
    internal sealed class BattleCommandPrelude
    {
        /// <summary>该命令前奏的互斥类别。</summary>
        public BattleCommandPreludeKind Kind { get; }

        /// <summary>出牌前奏使用的冻结卡牌身份；其他前奏为空。</summary>
        public CardInstanceId? CardId { get; }

        /// <summary>出牌前奏使用的首个可见 Effect 目标；其他前奏为空。</summary>
        public CombatantId? TargetId { get; }

        /// <summary>冻结命令前奏类别。</summary>
        internal BattleCommandPrelude(
            BattleCommandPreludeKind kind,
            CardInstanceId? cardId = null,
            CombatantId? targetId = null)
        {
            Kind = kind;
            CardId = cardId;
            TargetId = targetId;
        }
    }

    /// <summary>保留一条原始 settlement 及其零到多个稳定子步骤的不可变条目。</summary>
    internal sealed class BattleCommandPresentationSettlementEntry
    {
        /// <summary>原始 settlement 的权威 Order。</summary>
        public int Order => Settlement.Order;

        /// <summary>未被重排或替换的原始冻结 settlement。</summary>
        public BattleSettlementRecord Settlement { get; }

        /// <summary>该 settlement 按稳定子序派生的可见步骤。</summary>
        public IReadOnlyList<BattleCommandPresentationStep> Steps { get; }

        /// <summary>冻结一条 settlement 与它的稳定表现子步骤。</summary>
        internal BattleCommandPresentationSettlementEntry(
            BattleSettlementRecord settlement,
            IEnumerable<BattleCommandPresentationStep> steps)
        {
            Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            Steps = new ReadOnlyCollection<BattleCommandPresentationStep>(
                new List<BattleCommandPresentationStep>(steps));
        }
    }

    /// <summary>保留原始 settlement 顺序与引用的不可变可见步骤。</summary>
    internal sealed class BattleCommandPresentationStep
    {
        /// <summary>该步骤需要播放的反馈类别。</summary>
        public BattleCommandPresentationStepKind Kind { get; }

        /// <summary>该步骤所属 settlement 的权威顺序。</summary>
        public int SettlementOrder { get; }

        /// <summary>同一 settlement 内从零开始的稳定子步骤顺序。</summary>
        public int SubstepIndex { get; }

        /// <summary>该步骤消费的原始冻结 settlement。</summary>
        public BattleSettlementRecord Settlement { get; }

        /// <summary>冻结一个由 settlement 派生的表现步骤。</summary>
        internal BattleCommandPresentationStep(
            BattleCommandPresentationStepKind kind,
            BattleSettlementRecord settlement,
            int substepIndex)
        {
            Kind = kind;
            Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            SettlementOrder = settlement.Order;
            SubstepIndex = substepIndex;
        }
    }

    /// <summary>冻结单条权威命令的互斥前奏与严格 settlement 顺序步骤。</summary>
    internal sealed class BattleCommandPresentationPlan
    {
        /// <summary>命令级互斥前奏；无前奏时为空。</summary>
        public BattleCommandPrelude Prelude { get; }

        /// <summary>按 settlement Order 与同记录子序冻结的可见步骤。</summary>
        public IReadOnlyList<BattleCommandPresentationStep> SettlementSteps { get; }

        /// <summary>逐条保留全部 settlement；即使没有可见步骤也不会丢失。</summary>
        public IReadOnlyList<BattleCommandPresentationSettlementEntry> SettlementEntries { get; }

        /// <summary>冻结已经按权威顺序建立的表现计划。</summary>
        private BattleCommandPresentationPlan(
            BattleCommandPrelude prelude,
            IEnumerable<BattleCommandPresentationSettlementEntry> settlementEntries)
        {
            Prelude = prelude;
            var entries = new List<BattleCommandPresentationSettlementEntry>(settlementEntries);
            SettlementEntries = new ReadOnlyCollection<BattleCommandPresentationSettlementEntry>(entries);

            var settlementSteps = new List<BattleCommandPresentationStep>();
            foreach (BattleCommandPresentationSettlementEntry entry in entries)
                settlementSteps.AddRange(entry.Steps);
            SettlementSteps = new ReadOnlyCollection<BattleCommandPresentationStep>(
                settlementSteps);
        }

        /// <summary>从一条冻结执行结果建立命令前奏与 settlement 表现步骤。</summary>
        public static BattleCommandPresentationPlan Create(BattleCommandExecutionResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var entries = new List<BattleCommandPresentationSettlementEntry>();

            for (int index = 0; index < result.Settlements.Count; index++)
            {
                BattleSettlementRecord settlement = result.Settlements[index]
                    ?? throw new ArgumentException("表现计划不得消费空 settlement。", nameof(result));
                if (settlement.Order != index)
                {
                    throw new ArgumentException(
                        "表现计划要求 settlement 已按连续 Order 冻结，不得在 UI 层重排。",
                        nameof(result));
                }
                if (settlement is BattlePhaseChangedSettlement phaseChanged &&
                    phaseChanged.PhaseBefore != phaseChanged.PhaseAfter &&
                    phaseChanged.PhaseAfter == BattleTurnPhase.BattleEnded &&
                    index != result.Settlements.Count - 1)
                {
                    throw new ArgumentException(
                        "进入 BattleEnded 必须是最后一条 settlement，终局不得早于前序反馈。",
                        nameof(result));
                }

                var steps = new List<BattleCommandPresentationStep>();
                AddSettlementSteps(settlement, steps);
                entries.Add(new BattleCommandPresentationSettlementEntry(settlement, steps));
            }

            BattleCommandPrelude prelude = CreatePrelude(result.CommandType, entries);
            return new BattleCommandPresentationPlan(prelude, entries);
        }

        /// <summary>只从命令类别、唯一离手记录与首个可见 Effect 派生命令前奏。</summary>
        private static BattleCommandPrelude CreatePrelude(
            BattleCommandType commandType,
            IReadOnlyList<BattleCommandPresentationSettlementEntry> entries)
        {
            if (commandType == BattleCommandType.StartBattle)
                return new BattleCommandPrelude(BattleCommandPreludeKind.StartBattle);
            if (commandType != BattleCommandType.PlayCard)
                return null;

            BattleCardMovedSettlement soleHandDeparture = null;
            bool hasAmbiguousHandDeparture = false;
            BattleSettlementRecord firstVisibleEffect = null;
            foreach (BattleCommandPresentationSettlementEntry entry in entries)
            {
                if (entry.Settlement is BattleCardMovedSettlement moved &&
                    moved.FromZone == BattleCardZone.Hand)
                {
                    if (soleHandDeparture == null)
                        soleHandDeparture = moved;
                    else
                        hasAmbiguousHandDeparture = true;
                }

                if (firstVisibleEffect == null &&
                    entry.Settlement.EffectId.HasValue &&
                    entry.Steps.Count > 0)
                {
                    firstVisibleEffect = entry.Settlement;
                }
            }

            if (hasAmbiguousHandDeparture ||
                soleHandDeparture == null ||
                soleHandDeparture.ToZone != BattleCardZone.DiscardPile ||
                firstVisibleEffect == null)
            {
                return null;
            }
            if (!firstVisibleEffect.TargetId.HasValue)
            {
                throw new ArgumentException(
                    "出牌前奏的首个可见 Effect 必须携带冻结目标。",
                    nameof(entries));
            }

            return new BattleCommandPrelude(
                BattleCommandPreludeKind.PlayCard,
                soleHandDeparture.CardId,
                firstVisibleEffect.TargetId.Value);
        }

        /// <summary>只按当前已覆盖的 settlement 类别追加稳定可见步骤。</summary>
        private static void AddSettlementSteps(
            BattleSettlementRecord settlement,
            ICollection<BattleCommandPresentationStep> steps)
        {
            if (settlement is BattleDamageAppliedSettlement damage)
            {
                int substepIndex = 0;
                if (damage.BlockAbsorbed > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.BlockAbsorbedNumber,
                        settlement,
                        substepIndex++));
                }

                if (damage.HealthLoss > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.HealthLossNumber,
                        settlement,
                        substepIndex++));
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.HitShake,
                        settlement,
                        substepIndex++));
                }

                if (damage.WasFatal)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.DeathTransition,
                        settlement,
                        substepIndex));
                }

                return;
            }

            if (settlement is BattlePoisonTickedSettlement poisonTicked)
            {
                int substepIndex = 0;
                if (poisonTicked.HealthLoss > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.HealthLossNumber,
                        settlement,
                        substepIndex++));
                }

                if (poisonTicked.WasFatal)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.DeathTransition,
                        settlement,
                        substepIndex));
                }

                return;
            }

            if (settlement is BattleHealthRestoredSettlement healthRestored)
            {
                if (healthRestored.Amount > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.HealthRestoredNumber,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is MachineGunnerPrivateStatusChangedSettlement ||
                settlement is MachineGunnerScheduledEffectChangedSettlement)
            {
                return;
            }

            if (settlement is BattleStatusAppliedSettlement statusApplied)
            {
                if (statusApplied.Status == BattleStatusType.Vulnerable &&
                    statusApplied.Amount > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.VulnerableIconPulse,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is BattleBlockGainedSettlement blockGained)
            {
                if (blockGained.Amount > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.BlockGainedNumber,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is BattleAttributeModifiedSettlement attributeModified)
            {
                if (attributeModified.Attribute == BattleAttributeType.Strength &&
                    attributeModified.Amount != 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.StrengthIconPulse,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is BattleStatusReducedSettlement statusReduced)
            {
                if (statusReduced.Status == BattleStatusType.Vulnerable &&
                    statusReduced.Amount > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.VulnerableIconPulse,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is BattleCardMovedSettlement cardMoved)
            {
                bool isDrawToHand = cardMoved.FromZone == BattleCardZone.DrawPile &&
                    cardMoved.ToZone == BattleCardZone.Hand;
                bool isHandToDiscard = cardMoved.FromZone == BattleCardZone.Hand &&
                    cardMoved.ToZone == BattleCardZone.DiscardPile;
                bool isHandToExhaust = cardMoved.FromZone == BattleCardZone.Hand &&
                    cardMoved.ToZone == BattleCardZone.ExhaustPile;
                if (isDrawToHand || isHandToDiscard || isHandToExhaust)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.CardMoved,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is BattleCardCreatedSettlement cardCreated)
            {
                if (cardCreated.ToZone == BattleCardZone.Hand)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.CardCreated,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is BattleCardsReshuffledSettlement cardsReshuffled)
            {
                if (cardsReshuffled.NewDrawPileOrder.Count > 0)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        BattleCommandPresentationStepKind.CardsReshuffled,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            if (settlement is BattleEnemyIntentAdvancedSettlement)
            {
                steps.Add(new BattleCommandPresentationStep(
                    BattleCommandPresentationStepKind.EnemyIntentPulse,
                    settlement,
                    substepIndex: 0));
                return;
            }

            if (settlement is BattleEnergySpentSettlement ||
                settlement is BattleEnergyGainedSettlement ||
                settlement is BattleAmmoSpentSettlement ||
                settlement is BattleOperationSkippedSettlement ||
                settlement is BattleBlockClearedSettlement ||
                settlement is BattleEnergyRefilledSettlement ||
                settlement is BattleAmmoRefilledSettlement ||
                settlement is BattleEnemyActionSkippedSettlement)
            {
                return;
            }

            if (settlement is BattlePhaseChangedSettlement phaseChanged)
            {
                if (phaseChanged.PhaseBefore == phaseChanged.PhaseAfter)
                    return;

                BattleCommandPresentationStepKind? kind = null;
                if (phaseChanged.PhaseAfter == BattleTurnPhase.PlayerAction)
                    kind = BattleCommandPresentationStepKind.PlayerTurnBanner;
                else if (phaseChanged.PhaseAfter == BattleTurnPhase.EnemyAction)
                    kind = BattleCommandPresentationStepKind.EnemyTurnBanner;
                else if (phaseChanged.PhaseAfter == BattleTurnPhase.BattleEnded)
                    kind = BattleCommandPresentationStepKind.BattleOutcome;

                if (kind.HasValue)
                {
                    steps.Add(new BattleCommandPresentationStep(
                        kind.Value,
                        settlement,
                        substepIndex: 0));
                }

                return;
            }

            throw new ArgumentException(
                "表现计划遇到未知 settlement concrete 类型。",
                nameof(settlement));
        }
    }
}
