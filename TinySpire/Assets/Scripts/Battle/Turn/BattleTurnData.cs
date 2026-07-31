using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>
    /// 一轮战斗在权威调度中的阶段。
    /// </summary>
    public enum BattleTurnPhase
    {
        NotStarted,
        BattleStart,
        PlayerRoundStart,
        PlayerAction,
        PlayerRoundEnd,
        EnemyRoundStart,
        EnemyAction,
        EnemyRoundEnd,
        RoundEnd
    }

    /// <summary>
    /// 单名玩家在当前一轮中的独立事实。
    /// </summary>
    public sealed class PlayerTurnData
    {
        /// <summary>该玩家当前持有的能量。</summary>
        public int Energy { get; }

        /// <summary>该玩家是否已经结束本轮行动。</summary>
        public bool HasEndedAction { get; }

        /// <summary>创建一份不可变的单玩家回合事实。</summary>
        internal PlayerTurnData(int energy, bool hasEndedAction)
        {
            Energy = energy;
            HasEndedAction = hasEndedAction;
        }
    }

    /// <summary>
    /// 阶段、轮次与每玩家事实的一次完整不可变发布。
    /// </summary>
    public sealed class BattleTurnData
    {
        /// <summary>当前权威阶段。</summary>
        public BattleTurnPhase Phase { get; }

        /// <summary>当前轮次；战斗未开始时为零。</summary>
        public int RoundNumber { get; }

        /// <summary>按 CombatantId 保存的每玩家独立回合事实。</summary>
        public IReadOnlyDictionary<CombatantId, PlayerTurnData> Players { get; }

        /// <summary>当前行动敌人；仅 EnemyAction 阶段有值。</summary>
        public CombatantId? CurrentActingEnemyId { get; }

        /// <summary>复制并冻结所有玩家事实，创建完整回合快照。</summary>
        internal BattleTurnData(
            BattleTurnPhase phase,
            int roundNumber,
            IDictionary<CombatantId, PlayerTurnData> players,
            CombatantId? currentActingEnemyId)
        {
            if (players == null)
                throw new ArgumentNullException(nameof(players));

            Phase = phase;
            RoundNumber = roundNumber;
            Players = new ReadOnlyDictionary<CombatantId, PlayerTurnData>(
                new Dictionary<CombatantId, PlayerTurnData>(players));
            CurrentActingEnemyId = currentActingEnemyId;
        }
    }
}
