using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>敌人行动来源与目标解析的稳定四态结果。</summary>
    internal enum BattleEnemyActionTargetResolutionKind
    {
        Resolved,
        SourceNotAlive,
        BattleEnded,
        Faulted,
    }

    /// <summary>一次敌人行动目标读取形成的不可变结果；它不保存为第二份目标事实。</summary>
    internal sealed class BattleEnemyActionTargetEvaluation
    {
        /// <summary>本次解析的稳定结果类别。</summary>
        internal BattleEnemyActionTargetResolutionKind Kind { get; }

        /// <summary>解析成功时的唯一显式目标；其他结果为空。</summary>
        internal CombatantId? TargetId { get; }

        /// <summary>配置 fault 时的稳定原因；非 fault 结果为空。</summary>
        internal BattleCommandQueueFaultReason? FaultReason { get; }

        /// <summary>死亡 source 成功跳过时的专用冻结记录；其他结果为空。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>冻结一次互斥的目标、跳过、终局或 fault 结果。</summary>
        internal BattleEnemyActionTargetEvaluation(
            BattleEnemyActionTargetResolutionKind kind,
            CombatantId? targetId,
            BattleCommandQueueFaultReason? faultReason,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            Kind = kind;
            TargetId = targetId;
            FaultReason = faultReason;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }
    }

    /// <summary>从当前参与者事实解析敌人 Self 或唯一存活玩家目标，不持有目标镜像。</summary>
    internal sealed class BattleEnemyActionTargetResolver
    {
        private static readonly IReadOnlyList<BattleSettlementRecord> NoSettlements =
            Array.Empty<BattleSettlementRecord>();

        private readonly BattleCombatantsData _combatants;

        /// <summary>绑定本场唯一参与者聚合。</summary>
        internal BattleEnemyActionTargetResolver(BattleCombatantsData combatants)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        }

        /// <summary>按死亡 source 优先级与当前唯一存活玩家规则解析一次行动目标。</summary>
        internal BattleEnemyActionTargetEvaluation Resolve(
            CombatantId sourceId,
            cfg.battle.TargetRule targetRule,
            int startingOrder)
        {
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!_combatants.TryGet(sourceId, out CombatantData source) ||
                !(source is EnemyCombatantData))
            {
                return Fault(BattleCommandQueueFaultReason.UnsupportedConfiguration);
            }
            if (!source.IsAlive)
            {
                return new BattleEnemyActionTargetEvaluation(
                    BattleEnemyActionTargetResolutionKind.SourceNotAlive,
                    targetId: null,
                    faultReason: null,
                    new BattleSettlementRecord[]
                    {
                        new BattleEnemyActionSkippedSettlement(
                            startingOrder,
                            sourceId,
                            BattleEnemyActionSkipReason.SourceNotAlive),
                    });
            }
            PlayerCombatantData livingPlayer = null;
            foreach (CombatantData combatant in _combatants.All.Values)
            {
                if (!(combatant is PlayerCombatantData player) || !player.IsAlive)
                    continue;
                if (livingPlayer != null)
                    return Fault(BattleCommandQueueFaultReason.MultipleLivingPlayers);

                livingPlayer = player;
            }

            if (livingPlayer == null)
            {
                return new BattleEnemyActionTargetEvaluation(
                    BattleEnemyActionTargetResolutionKind.BattleEnded,
                    targetId: null,
                    faultReason: null,
                    NoSettlements);
            }

            if (targetRule == cfg.battle.TargetRule.Self)
                return Resolved(sourceId);
            if (targetRule != cfg.battle.TargetRule.Enemy)
                return Fault(BattleCommandQueueFaultReason.UnsupportedConfiguration);

            return Resolved(livingPlayer.Id);
        }

        /// <summary>创建不携带记录的成功唯一目标结果。</summary>
        private static BattleEnemyActionTargetEvaluation Resolved(CombatantId targetId)
        {
            return new BattleEnemyActionTargetEvaluation(
                BattleEnemyActionTargetResolutionKind.Resolved,
                targetId,
                faultReason: null,
                NoSettlements);
        }

        /// <summary>创建不携带目标或 battle settlement 的配置 fault 结果。</summary>
        private static BattleEnemyActionTargetEvaluation Fault(
            BattleCommandQueueFaultReason reason)
        {
            return new BattleEnemyActionTargetEvaluation(
                BattleEnemyActionTargetResolutionKind.Faulted,
                targetId: null,
                reason,
                NoSettlements);
        }
    }
}
