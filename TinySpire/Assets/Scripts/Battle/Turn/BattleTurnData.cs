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
        RoundEnd,
        BattleEnded
    }

    /// <summary>
    /// 单名玩家在当前一轮中的独立事实。
    /// </summary>
    public sealed class PlayerTurnData
    {
        /// <summary>该玩家当前持有的能量。</summary>
        public int Energy { get; }

        /// <summary>该玩家当前能量的权威上限。</summary>
        public int EnergyMaximum { get; }

        /// <summary>该玩家每个后续回合开始时补充的能量。</summary>
        public int EnergyGainPerRound { get; }

        /// <summary>该玩家当前持有的弹药。</summary>
        public int Ammo { get; }

        /// <summary>该玩家当前弹药的权威上限。</summary>
        public int AmmoMaximum { get; }

        /// <summary>该玩家每个后续回合开始时补充的弹药。</summary>
        public int AmmoGainPerRound { get; }

        /// <summary>该玩家是否已经结束本轮行动。</summary>
        public bool HasEndedAction { get; }

        /// <summary>创建一份不可变的单玩家回合事实。</summary>
        internal PlayerTurnData(
            int energy,
            int energyMaximum,
            int energyGainPerRound,
            int ammo,
            int ammoMaximum,
            int ammoGainPerRound,
            bool hasEndedAction)
        {
            if (energy < 0)
                throw new ArgumentOutOfRangeException(nameof(energy));
            if (energyMaximum < 0)
                throw new ArgumentOutOfRangeException(nameof(energyMaximum));
            if (energyGainPerRound < 0)
                throw new ArgumentOutOfRangeException(nameof(energyGainPerRound));
            if (ammo < 0)
                throw new ArgumentOutOfRangeException(nameof(ammo));
            if (ammoMaximum < 0)
                throw new ArgumentOutOfRangeException(nameof(ammoMaximum));
            if (ammoGainPerRound < 0)
                throw new ArgumentOutOfRangeException(nameof(ammoGainPerRound));

            EnergyMaximum = energyMaximum;
            EnergyGainPerRound = energyGainPerRound;
            AmmoMaximum = ammoMaximum;
            AmmoGainPerRound = ammoGainPerRound;
            Energy = Math.Min(energy, energyMaximum);
            Ammo = Math.Min(ammo, ammoMaximum);
            HasEndedAction = hasEndedAction;
        }

        /// <summary>兼容仅关心能量与行动结束标记的旧测试快照构造方式。</summary>
        internal PlayerTurnData(int energy, bool hasEndedAction)
            : this(
                energy: energy,
                energyMaximum: energy,
                energyGainPerRound: 0,
                ammo: 0,
                ammoMaximum: 0,
                ammoGainPerRound: 0,
                hasEndedAction: hasEndedAction)
        {
        }

        /// <summary>只改变当前能量并保留同一份资源上限、回合补充和弹药事实。</summary>
        internal PlayerTurnData WithEnergy(int energy)
        {
            return new PlayerTurnData(
                energy,
                EnergyMaximum,
                EnergyGainPerRound,
                Ammo,
                AmmoMaximum,
                AmmoGainPerRound,
                HasEndedAction);
        }

        /// <summary>只改变当前弹药并保留同一份能量、资源上限、回合补充和行动结束事实。</summary>
        internal PlayerTurnData WithAmmo(int ammo)
        {
            return new PlayerTurnData(
                Energy,
                EnergyMaximum,
                EnergyGainPerRound,
                ammo,
                AmmoMaximum,
                AmmoGainPerRound,
                HasEndedAction);
        }

        /// <summary>以一次完整资源投影替换当前值、上限和每回合补充，并由构造函数统一执行上限裁剪。</summary>
        internal PlayerTurnData WithResources(
            int energy,
            int energyMaximum,
            int energyGainPerRound,
            int ammo,
            int ammoMaximum,
            int ammoGainPerRound)
        {
            return new PlayerTurnData(
                energy,
                energyMaximum,
                energyGainPerRound,
                ammo,
                ammoMaximum,
                ammoGainPerRound,
                HasEndedAction);
        }

        /// <summary>只改变行动结束标记并保留同一份资源事实。</summary>
        internal PlayerTurnData WithHasEndedAction(bool hasEndedAction)
        {
            return new PlayerTurnData(
                Energy,
                EnergyMaximum,
                EnergyGainPerRound,
                Ammo,
                AmmoMaximum,
                AmmoGainPerRound,
                hasEndedAction);
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
