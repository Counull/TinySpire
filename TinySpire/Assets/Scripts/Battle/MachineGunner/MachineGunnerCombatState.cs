using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>机枪兵单场私有状态可使用的参与者状态种类。</summary>
    internal enum MachineGunnerCombatantStatus
    {
        Weakness,
        LoseStrength,
        Shackle,
        Smoke,
        Burn,
        Oil,
        FirePower,
        Armor,
        ArmorBreak,
        Intangible,
        Invisible,
        Buffer,
        NextRoundBlock,
        ReloadAmmoAtNextPlayerRound,
        NextRoundEnergyGainBonus,
        NextRoundEnergyGainPenalty,
        Regeneration,
    }

    /// <summary>一次机枪兵私有状态数值变化的不可变前后快照。</summary>
    internal readonly struct MachineGunnerStatusValueChange
    {
        /// <summary>发生变化的职业私有状态。</summary>
        internal MachineGunnerCombatantStatus Status { get; }

        /// <summary>变化前的层数。</summary>
        internal int Before { get; }

        /// <summary>变化后的层数。</summary>
        internal int After { get; }

        /// <summary>本次变化的有符号层数。</summary>
        internal int Amount => After - Before;

        /// <summary>冻结状态种类及其非负层数前后值。</summary>
        internal MachineGunnerStatusValueChange(
            MachineGunnerCombatantStatus status,
            int before,
            int after)
        {
            if (before < 0)
                throw new ArgumentOutOfRangeException(nameof(before));
            if (after < 0)
                throw new ArgumentOutOfRangeException(nameof(after));

            Status = status;
            Before = before;
            After = after;
        }
    }

    /// <summary>一次燃烧施加同时冻结燃烧与既有浸油的变化，避免新浸油在同次施加中自触发。</summary>
    internal readonly struct MachineGunnerBurnApplicationResult
    {
        /// <summary>本次燃烧层数变化。</summary>
        internal MachineGunnerStatusValueChange BurnChange { get; }

        /// <summary>本次消耗既有浸油后的层数变化。</summary>
        internal MachineGunnerStatusValueChange OilChange { get; }

        /// <summary>冻结燃烧和浸油两个有序的状态变化。</summary>
        internal MachineGunnerBurnApplicationResult(
            MachineGunnerStatusValueChange burnChange,
            MachineGunnerStatusValueChange oilChange)
        {
            if (burnChange.Status != MachineGunnerCombatantStatus.Burn)
                throw new ArgumentOutOfRangeException(nameof(burnChange));
            if (oilChange.Status != MachineGunnerCombatantStatus.Oil)
                throw new ArgumentOutOfRangeException(nameof(oilChange));

            BurnChange = burnChange;
            OilChange = oilChange;
        }
    }

    /// <summary>只属于一场机枪兵战斗的状态仓储；它不写通用 CombatantData，也不暴露第二条命令入口。</summary>
    internal sealed class MachineGunnerCombatState
    {
        private readonly Dictionary<CombatantId, MachineGunnerCombatantStatusValues> _values =
            new Dictionary<CombatantId, MachineGunnerCombatantStatusValues>();

        /// <summary>读取指定参与者的职业私有状态层数；未持有任何状态的参与者返回零。</summary>
        internal int Get(CombatantId combatantId, MachineGunnerCombatantStatus status)
        {
            ValidateStatus(status);
            return _values.TryGetValue(combatantId, out MachineGunnerCombatantStatusValues values)
                ? values.Get(status)
                : 0;
        }

        /// <summary>为指定参与者叠加正数层数，并返回冻结的前后快照。</summary>
        internal MachineGunnerStatusValueChange Add(
            CombatantId combatantId,
            MachineGunnerCombatantStatus status,
            int amount)
        {
            ValidateStatus(status);
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            MachineGunnerCombatantStatusValues values = GetOrCreate(combatantId);
            int before = values.Get(status);
            int after = checked(before + amount);
            values.Set(status, after);
            return new MachineGunnerStatusValueChange(status, before, after);
        }

        /// <summary>把指定状态减少至零以上的目标值，并返回冻结的前后快照。</summary>
        internal MachineGunnerStatusValueChange Set(
            CombatantId combatantId,
            MachineGunnerCombatantStatus status,
            int value)
        {
            ValidateStatus(status);
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            MachineGunnerCombatantStatusValues values = GetOrCreate(combatantId);
            int before = values.Get(status);
            values.Set(status, value);
            return new MachineGunnerStatusValueChange(status, before, value);
        }

        /// <summary>施加燃烧并只读取施加前已有的浸油作为额外燃烧，随后将该浸油向下减半。</summary>
        internal MachineGunnerBurnApplicationResult ApplyBurn(CombatantId combatantId, int baseBurn)
        {
            MachineGunnerCombatantStatusValues values = GetOrCreate(combatantId);
            MachineGunnerBurnApplicationResult result = CalculateBurnApplication(
                values.Burn,
                values.Oil,
                baseBurn);
            values.Burn = result.BurnChange.After;
            values.Oil = result.OilChange.After;
            return result;
        }

        /// <summary>从任意已验证的燃烧与浸油层数纯计算一次施加燃烧的前后值，供真实状态写入与卡牌预演共用同一条不触发新浸油的规则。</summary>
        internal static MachineGunnerBurnApplicationResult CalculateBurnApplication(
            int burnBefore,
            int oilBefore,
            int baseBurn)
        {
            if (burnBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(burnBefore));
            if (oilBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(oilBefore));
            if (baseBurn <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseBurn));

            int burnAfter = checked(burnBefore + baseBurn + oilBefore);
            int oilAfter = oilBefore / 2;
            return new MachineGunnerBurnApplicationResult(
                new MachineGunnerStatusValueChange(
                    MachineGunnerCombatantStatus.Burn,
                    burnBefore,
                    burnAfter),
                new MachineGunnerStatusValueChange(
                    MachineGunnerCombatantStatus.Oil,
                    oilBefore,
                    oilAfter));
        }

        /// <summary>在玩家回合开始时按烟雾弥漫开关清空烟雾或只减少一层。</summary>
        internal MachineGunnerStatusValueChange AdvanceSmokeAtPlayerRoundStart(
            CombatantId combatantId,
            bool persistsAndDecays)
        {
            int before = Get(combatantId, MachineGunnerCombatantStatus.Smoke);
            int after = persistsAndDecays ? Math.Max(0, before - 1) : 0;
            return Set(combatantId, MachineGunnerCombatantStatus.Smoke, after);
        }

        /// <summary>在调用方声明的生命周期时机递减一层持续时间状态，零层保持零。</summary>
        internal MachineGunnerStatusValueChange ReduceDuration(
            CombatantId combatantId,
            MachineGunnerCombatantStatus status)
        {
            if (status != MachineGunnerCombatantStatus.Weakness &&
                status != MachineGunnerCombatantStatus.Invisible)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            int before = Get(combatantId, status);
            return Set(combatantId, status, Math.Max(0, before - 1));
        }

        /// <summary>在一次攻击真实穿透生命后消耗一层护甲；没有护甲或未穿透时不写入。</summary>
        internal bool TryConsumeArmorAfterPenetratingAttack(
            CombatantId combatantId,
            MachineGunnerDamageCalculation calculation,
            out MachineGunnerStatusValueChange change)
        {
            if (!calculation.ConsumesArmor)
            {
                change = default;
                return false;
            }

            int before = Get(combatantId, MachineGunnerCombatantStatus.Armor);
            if (before <= 0)
            {
                change = default;
                return false;
            }

            change = Set(
                combatantId,
                MachineGunnerCombatantStatus.Armor,
                before - 1);
            return true;
        }

        /// <summary>取得或创建单个参与者的私有状态槽，状态槽本身不对外暴露。</summary>
        private MachineGunnerCombatantStatusValues GetOrCreate(CombatantId combatantId)
        {
            if (_values.TryGetValue(combatantId, out MachineGunnerCombatantStatusValues values))
                return values;

            values = new MachineGunnerCombatantStatusValues();
            _values.Add(combatantId, values);
            return values;
        }

        /// <summary>拒绝未知枚举值，防止静默创建不能被伤害管线读取的状态。</summary>
        private static void ValidateStatus(MachineGunnerCombatantStatus status)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerCombatantStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        /// <summary>单个参与者的私有状态存储，不具备任何规则或写入入口。</summary>
        private sealed class MachineGunnerCombatantStatusValues
        {
            /// <summary>造成攻击伤害降低百分之二十五的剩余持续层数。</summary>
            internal int Weakness { get; set; }

            /// <summary>在携带者本次行动结束前从攻击力量修正中扣除的临时层数。</summary>
            internal int LoseStrength { get; set; }

            /// <summary>在携带者本次行动结束前禁止继续打出攻击牌的临时层数。</summary>
            internal int Shackle { get; set; }

            /// <summary>造成和承受攻击伤害时各自扣减一点的层数。</summary>
            internal int Smoke { get; set; }

            /// <summary>回合结束结算的可格挡持续伤害层数。</summary>
            internal int Burn { get; set; }

            /// <summary>下次燃烧施加时追加燃烧并减半的层数。</summary>
            internal int Oil { get; set; }

            /// <summary>本回合常规射击伤害每段增加的开火层数。</summary>
            internal int FirePower { get; set; }

            /// <summary>穿透生命的攻击每段消耗一层的玩家护甲层数。</summary>
            internal int Armor { get; set; }

            /// <summary>受到攻击、支援或燃烧伤害时按层追加的敌方破甲层数。</summary>
            internal int ArmorBreak { get; set; }

            /// <summary>受到攻击伤害时将单段伤害封顶为一点、随后消耗一层的玩家无实体层数。</summary>
            internal int Intangible { get; set; }

            /// <summary>受到伤害减半及狙击视为易伤的剩余层数。</summary>
            internal int Invisible { get; set; }

            /// <summary>完全抵挡一次攻击伤害并在命中后消耗一层的剩余缓冲层数。</summary>
            internal int Buffer { get; set; }

            /// <summary>在下一次玩家回合开始时转化为格挡并清除的累计层数。</summary>
            internal int NextRoundBlock { get; set; }

            /// <summary>在下一次玩家回合开始时把弹药补至上限并清除的一次性标记层数。</summary>
            internal int ReloadAmmoAtNextPlayerRound { get; set; }

            /// <summary>在下一次玩家回合开始时增加基础能量补给并清除的累计层数。</summary>
            internal int NextRoundEnergyGainBonus { get; set; }

            /// <summary>在下一次玩家回合开始时减少基础能量补给并清除的累计层数。</summary>
            internal int NextRoundEnergyGainPenalty { get; set; }

            /// <summary>在携带者行动结束时恢复等量生命并递减一层的再生层数。</summary>
            internal int Regeneration { get; set; }

            /// <summary>读取指定状态的当前非负层数。</summary>
            internal int Get(MachineGunnerCombatantStatus status)
            {
                switch (status)
                {
                    case MachineGunnerCombatantStatus.Weakness:
                        return Weakness;
                    case MachineGunnerCombatantStatus.LoseStrength:
                        return LoseStrength;
                    case MachineGunnerCombatantStatus.Shackle:
                        return Shackle;
                    case MachineGunnerCombatantStatus.Smoke:
                        return Smoke;
                    case MachineGunnerCombatantStatus.Burn:
                        return Burn;
                    case MachineGunnerCombatantStatus.Oil:
                        return Oil;
                    case MachineGunnerCombatantStatus.FirePower:
                        return FirePower;
                    case MachineGunnerCombatantStatus.Armor:
                        return Armor;
                    case MachineGunnerCombatantStatus.ArmorBreak:
                        return ArmorBreak;
                    case MachineGunnerCombatantStatus.Intangible:
                        return Intangible;
                    case MachineGunnerCombatantStatus.Invisible:
                        return Invisible;
                    case MachineGunnerCombatantStatus.Buffer:
                        return Buffer;
                    case MachineGunnerCombatantStatus.NextRoundBlock:
                        return NextRoundBlock;
                    case MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound:
                        return ReloadAmmoAtNextPlayerRound;
                    case MachineGunnerCombatantStatus.NextRoundEnergyGainBonus:
                        return NextRoundEnergyGainBonus;
                    case MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty:
                        return NextRoundEnergyGainPenalty;
                    case MachineGunnerCombatantStatus.Regeneration:
                        return Regeneration;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(status));
                }
            }

            /// <summary>写入指定状态的已校验非负层数。</summary>
            internal void Set(MachineGunnerCombatantStatus status, int value)
            {
                switch (status)
                {
                    case MachineGunnerCombatantStatus.Weakness:
                        Weakness = value;
                        break;
                    case MachineGunnerCombatantStatus.LoseStrength:
                        LoseStrength = value;
                        break;
                    case MachineGunnerCombatantStatus.Shackle:
                        Shackle = value;
                        break;
                    case MachineGunnerCombatantStatus.Smoke:
                        Smoke = value;
                        break;
                    case MachineGunnerCombatantStatus.Burn:
                        Burn = value;
                        break;
                    case MachineGunnerCombatantStatus.Oil:
                        Oil = value;
                        break;
                    case MachineGunnerCombatantStatus.FirePower:
                        FirePower = value;
                        break;
                    case MachineGunnerCombatantStatus.Armor:
                        Armor = value;
                        break;
                    case MachineGunnerCombatantStatus.ArmorBreak:
                        ArmorBreak = value;
                        break;
                    case MachineGunnerCombatantStatus.Intangible:
                        Intangible = value;
                        break;
                    case MachineGunnerCombatantStatus.Invisible:
                        Invisible = value;
                        break;
                    case MachineGunnerCombatantStatus.Buffer:
                        Buffer = value;
                        break;
                    case MachineGunnerCombatantStatus.NextRoundBlock:
                        NextRoundBlock = value;
                        break;
                    case MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound:
                        ReloadAmmoAtNextPlayerRound = value;
                        break;
                    case MachineGunnerCombatantStatus.NextRoundEnergyGainBonus:
                        NextRoundEnergyGainBonus = value;
                        break;
                    case MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty:
                        NextRoundEnergyGainPenalty = value;
                        break;
                    case MachineGunnerCombatantStatus.Regeneration:
                        Regeneration = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(status));
                }
            }
        }
    }
}
