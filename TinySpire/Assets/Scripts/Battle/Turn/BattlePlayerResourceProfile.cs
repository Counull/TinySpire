using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>由 Hero 静态表装配的单名玩家资源初始档案，不保存任何战斗内可变状态。</summary>
    internal sealed class BattlePlayerResourceProfile
    {
        /// <summary>首个玩家回合开始时的能量值。</summary>
        internal int InitialEnergy { get; }

        /// <summary>初始能量上限。</summary>
        internal int MaxEnergy { get; }

        /// <summary>每个后续玩家回合开始时追加的能量值。</summary>
        internal int EnergyGainPerRound { get; }

        /// <summary>首个玩家回合开始时的弹药值。</summary>
        internal int InitialAmmo { get; }

        /// <summary>初始弹药上限；零表示该 Hero 尚不使用弹药资源。</summary>
        internal int MaxAmmo { get; }

        /// <summary>每个后续玩家回合开始时追加的弹药值。</summary>
        internal int AmmoGainPerRound { get; }

        /// <summary>创建一份已验证的 Hero 静态资源档案。</summary>
        internal BattlePlayerResourceProfile(
            int initialEnergy,
            int maxEnergy,
            int energyGainPerRound,
            int initialAmmo,
            int maxAmmo,
            int ammoGainPerRound)
        {
            ValidateEnergy(initialEnergy, maxEnergy, energyGainPerRound);
            ValidateAmmo(initialAmmo, maxAmmo, ammoGainPerRound);

            InitialEnergy = initialEnergy;
            MaxEnergy = maxEnergy;
            EnergyGainPerRound = energyGainPerRound;
            InitialAmmo = initialAmmo;
            MaxAmmo = maxAmmo;
            AmmoGainPerRound = ammoGainPerRound;
        }

        /// <summary>为战斗尚未开始的玩家创建携带上限和每回合增量的零资源事实。</summary>
        internal PlayerTurnData CreateInitialTurnData()
        {
            return new PlayerTurnData(
                energy: 0,
                energyMaximum: MaxEnergy,
                energyGainPerRound: EnergyGainPerRound,
                ammo: 0,
                ammoMaximum: MaxAmmo,
                ammoGainPerRound: AmmoGainPerRound,
                hasEndedAction: false);
        }

        /// <summary>按首回合初始化或后续回合受上限约束的补充规则重建玩家资源事实。</summary>
        internal PlayerTurnData StartPlayerRound(
            PlayerTurnData current,
            bool isFirstRound,
            int energyGainAdjustment = 0)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            if (isFirstRound)
            {
                return new PlayerTurnData(
                    energy: InitialEnergy,
                    energyMaximum: MaxEnergy,
                    energyGainPerRound: EnergyGainPerRound,
                    ammo: InitialAmmo,
                    ammoMaximum: MaxAmmo,
                    ammoGainPerRound: AmmoGainPerRound,
                    hasEndedAction: false);
            }

            long effectiveEnergyGain = Math.Max(
                0L,
                (long)current.EnergyGainPerRound + energyGainAdjustment);
            return new PlayerTurnData(
                energy: AddWithCap(current.Energy, effectiveEnergyGain, current.EnergyMaximum),
                energyMaximum: current.EnergyMaximum,
                energyGainPerRound: current.EnergyGainPerRound,
                ammo: AddWithCap(current.Ammo, current.AmmoGainPerRound, current.AmmoMaximum),
                ammoMaximum: current.AmmoMaximum,
                ammoGainPerRound: current.AmmoGainPerRound,
                hasEndedAction: false);
        }

        /// <summary>为仍使用旧构造入口的测试夹具生成与历史每回合能量行为等价的档案映射。</summary>
        internal static IReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile> CreateLegacyProfiles(
            BattleCombatantsData combatants,
            int energyPerRound)
        {
            if (combatants == null)
                throw new ArgumentNullException(nameof(combatants));
            if (energyPerRound < 0)
                throw new ArgumentOutOfRangeException(nameof(energyPerRound));

            var profiles = new Dictionary<CombatantId, BattlePlayerResourceProfile>();
            foreach (CombatantData combatant in combatants.All.Values)
            {
                if (combatant is PlayerCombatantData player)
                {
                    profiles.Add(
                        player.Id,
                        new BattlePlayerResourceProfile(
                            initialEnergy: energyPerRound,
                            maxEnergy: Math.Max(energyPerRound, 1),
                            energyGainPerRound: energyPerRound,
                            initialAmmo: 0,
                            maxAmmo: 0,
                            ammoGainPerRound: 0));
                }
            }

            return new ReadOnlyDictionary<CombatantId, BattlePlayerResourceProfile>(profiles);
        }

        /// <summary>验证能量资源始终具备非零上限，且初始值不会越过上限。</summary>
        private static void ValidateEnergy(int initial, int maximum, int gainPerRound)
        {
            if (initial < 0)
                throw new ArgumentOutOfRangeException(nameof(initial));
            if (maximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            if (gainPerRound < 0)
                throw new ArgumentOutOfRangeException(nameof(gainPerRound));
            if (initial > maximum)
                throw new ArgumentOutOfRangeException(nameof(initial));
        }

        /// <summary>验证弹药资源允许整体禁用，但启用时初始值不得超过上限。</summary>
        private static void ValidateAmmo(int initial, int maximum, int gainPerRound)
        {
            if (initial < 0)
                throw new ArgumentOutOfRangeException(nameof(initial));
            if (maximum < 0)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            if (gainPerRound < 0)
                throw new ArgumentOutOfRangeException(nameof(gainPerRound));
            if (initial > maximum)
                throw new ArgumentOutOfRangeException(nameof(initial));
            if (maximum == 0 && (initial != 0 || gainPerRound != 0))
                throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        /// <summary>在不溢出的前提下把当前值加上回合补充并裁剪到给定上限。</summary>
        private static int AddWithCap(int current, long gain, int maximum)
        {
            long replenished = (long)current + gain;
            return replenished >= maximum ? maximum : (int)replenished;
        }
    }
}
