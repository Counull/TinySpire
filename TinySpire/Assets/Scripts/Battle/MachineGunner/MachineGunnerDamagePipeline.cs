using System;

namespace TinySpire.Battle
{
    /// <summary>机枪兵规则对伤害来源语义的分类，决定是否经过攻击修正链。</summary>
    internal enum MachineGunnerDamageKind
    {
        /// <summary>由角色主动打出的攻击牌造成的伤害。</summary>
        Attack,

        /// <summary>旧延迟伤害的兼容类别；只读取目标烟雾，不读取支援或炸弹的新规则。</summary>
        Delayed,

        /// <summary>女妖、火力支援、燃烧轰炸和延迟狙击等支援段，读取目标烟雾、易伤和破甲。</summary>
        Support,

        /// <summary>炸弹段，只读取目标烟雾，不读取易伤或破甲。</summary>
        Bomb,

        /// <summary>燃烧及燃烧波等可格挡持续伤害，忽略烟雾并读取破甲。</summary>
        Burn,

        /// <summary>不属于燃烧的通用 debuff 伤害，保持不读取攻击、烟雾或破甲修正。</summary>
        Debuff,

        /// <summary>便携帮手追加段，只读取来源开火、目标易伤和目标破甲。</summary>
        PortableHelper,
    }

    /// <summary>一次职业私有伤害计算的冻结输入，不包含可变参与者或状态引用。</summary>
    internal readonly struct MachineGunnerDamageRequest
    {
        /// <summary>造成伤害的参与者。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>承受伤害的参与者。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>卡牌或延迟效果声明的基础伤害。</summary>
        internal int BaseDamage { get; }

        /// <summary>本段伤害的规则类别。</summary>
        internal MachineGunnerDamageKind Kind { get; }

        /// <summary>本段来源的唯一射击分类；具体修正只由伤害管线内部规则档案读取。</summary>
        internal MachineGunnerCardTag Tags { get; }

        /// <summary>本段是否带狙击标签。</summary>
        internal bool IsSniper =>
            (Tags & MachineGunnerCardTag.Sniper) != MachineGunnerCardTag.None;

        /// <summary>本段是否带普通射击标签；它决定兴奋剂额外段，而不代表全部射击分类。</summary>
        internal bool IsShoot =>
            (Tags & MachineGunnerCardTag.Shoot) != MachineGunnerCardTag.None;

        /// <summary>本段是否属于任一种射击分类；它只用于“非射击”规则，不等同于开火或燃烧弹药资格。</summary>
        internal bool IsShootCategory =>
            (Tags & (MachineGunnerCardTag.Shoot |
                     MachineGunnerCardTag.Sniper |
                     MachineGunnerCardTag.Shotgun)) != MachineGunnerCardTag.None;

        /// <summary>冻结一次非负基础伤害的职业私有请求。</summary>
        internal MachineGunnerDamageRequest(
            CombatantId sourceId,
            CombatantId targetId,
            int baseDamage,
            MachineGunnerDamageKind kind,
            MachineGunnerCardTag tags = MachineGunnerCardTag.None)
        {
            if (baseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (!Enum.IsDefined(typeof(MachineGunnerDamageKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            const MachineGunnerCardTag supportedTags =
                MachineGunnerCardTag.Shoot |
                MachineGunnerCardTag.Sniper |
                MachineGunnerCardTag.Shotgun;
            if ((tags & ~supportedTags) != MachineGunnerCardTag.None)
                throw new ArgumentOutOfRangeException(nameof(tags));
            if (tags != MachineGunnerCardTag.None && kind != MachineGunnerDamageKind.Attack)
            {
                throw new ArgumentOutOfRangeException(nameof(tags));
            }

            SourceId = sourceId;
            TargetId = targetId;
            BaseDamage = baseDamage;
            Kind = kind;
            Tags = tags;
        }

        /// <summary>兼容既有调用者的标签构造入口；新卡牌程序应直接传入完整标签。</summary>
        internal MachineGunnerDamageRequest(
            CombatantId sourceId,
            CombatantId targetId,
            int baseDamage,
            MachineGunnerDamageKind kind,
            bool isSniper,
            bool isShoot)
            : this(
                sourceId,
                targetId,
                baseDamage,
                kind,
                (isShoot ? MachineGunnerCardTag.Shoot : MachineGunnerCardTag.None) |
                (isSniper ? MachineGunnerCardTag.Sniper : MachineGunnerCardTag.None))
        {
        }
    }

    /// <summary>一次职业私有伤害计算的结果及后续护甲消耗条件。</summary>
    internal readonly struct MachineGunnerDamageCalculation
    {
        /// <summary>可直接提交到 CombatantData 的格挡与生命推演。</summary>
        internal BattleDamageFormulaOutcome Outcome { get; }

        /// <summary>本段属于攻击或延迟攻击且真实穿透生命时，护甲应消耗一层。</summary>
        internal bool ConsumesArmor { get; }

        /// <summary>冻结伤害推演和与状态相关的后续处理标记。</summary>
        internal MachineGunnerDamageCalculation(
            BattleDamageFormulaOutcome outcome,
            bool consumesArmor)
        {
            Outcome = outcome;
            ConsumesArmor = consumesArmor;
        }
    }

    /// <summary>机枪兵的纯伤害计算器：它只读取私有状态并返回推演，不直接写入参与者或状态。</summary>
    internal static class MachineGunnerDamagePipeline
    {
        /// <summary>按虚弱、双方烟雾、易伤、格挡和生命的冻结顺序计算一段职业伤害。</summary>
        internal static MachineGunnerDamageCalculation Calculate(
            MachineGunnerDamageRequest request,
            CombatantData source,
            BattleEffectTargetSnapshot target,
            MachineGunnerCombatState state)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Id != request.SourceId)
                throw new ArgumentException("伤害来源与请求标识不一致。", nameof(source));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return Calculate(request, source.CurrentStrength, target, state);
        }

        /// <summary>使用联合预构建中的来源力量投影计算伤害，避免同一 Effect 链的前置加力量在提交前丢失。</summary>
        internal static MachineGunnerDamageCalculation Calculate(
            MachineGunnerDamageRequest request,
            int sourceStrength,
            BattleEffectTargetSnapshot target,
            MachineGunnerCombatState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            int attackValue = CalculateAttackValue(request, sourceStrength, target, state);
            int blockAbsorbed = Math.Min(target.Block, attackValue);
            int blockAfter = target.Block - blockAbsorbed;
            int healthLoss = Math.Min(target.Health, attackValue - blockAbsorbed);
            int healthAfter = target.Health - healthLoss;
            var outcome = new BattleDamageFormulaOutcome(
                attackValue,
                target.Block,
                blockAfter,
                blockAbsorbed,
                target.Health,
                healthAfter,
                healthLoss,
                target.Health > 0 && healthAfter == 0);
            MachineGunnerDamageRuleProfile ruleProfile =
                MachineGunnerDamageRuleProfile.Create(request);
            bool consumesArmor = ruleProfile.ConsumesArmor &&
                healthLoss > 0 &&
                state.Get(request.TargetId, MachineGunnerCombatantStatus.Armor) > 0;
            return new MachineGunnerDamageCalculation(outcome, consumesArmor);
        }

        /// <summary>把已经完成攻击修正的伤害冻结为缓冲完全抵挡结果，保留展示攻击值但绝不消耗目标格挡或生命。</summary>
        internal static BattleDamageFormulaOutcome PreventAttackWithBuffer(
            BattleDamageFormulaOutcome attackOutcome)
        {
            if (attackOutcome.AttackValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(attackOutcome));

            return new BattleDamageFormulaOutcome(
                attackOutcome.AttackValue,
                attackOutcome.BlockBefore,
                attackOutcome.BlockBefore,
                0,
                attackOutcome.HealthBefore,
                attackOutcome.HealthBefore,
                0,
                false);
        }

        /// <summary>把已经完成攻击修正的伤害冻结为无实体封顶结果；封顶发生在格挡与生命推演之前。</summary>
        internal static BattleDamageFormulaOutcome CapAttackWithIntangible(
            BattleDamageFormulaOutcome attackOutcome)
        {
            if (attackOutcome.AttackValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(attackOutcome));

            const int cappedAttackValue = 1;
            int blockAbsorbed = Math.Min(attackOutcome.BlockBefore, cappedAttackValue);
            int blockAfter = attackOutcome.BlockBefore - blockAbsorbed;
            int healthLoss = Math.Min(
                attackOutcome.HealthBefore,
                cappedAttackValue - blockAbsorbed);
            int healthAfter = attackOutcome.HealthBefore - healthLoss;
            return new BattleDamageFormulaOutcome(
                cappedAttackValue,
                attackOutcome.BlockBefore,
                blockAfter,
                blockAbsorbed,
                attackOutcome.HealthBefore,
                healthAfter,
                healthLoss,
                attackOutcome.HealthBefore > 0 && healthAfter == 0);
        }

        /// <summary>先按伤害类别选择可用修正，再在同一函数中保留所有取整的唯一实现。</summary>
        private static int CalculateAttackValue(
            MachineGunnerDamageRequest request,
            int sourceStrength,
            BattleEffectTargetSnapshot target,
            MachineGunnerCombatState state)
        {
            MachineGunnerDamageRuleProfile ruleProfile =
                MachineGunnerDamageRuleProfile.Create(request);
            int value = request.BaseDamage;
            if (ruleProfile.ReadsSourceStrength)
            {
                value = Math.Max(
                    0,
                    checked(value + sourceStrength - state.Get(
                        request.SourceId,
                        MachineGunnerCombatantStatus.LoseStrength)));
            }
            if (ruleProfile.ReadsFirePower)
            {
                value = checked(value + state.Get(
                    request.SourceId,
                    MachineGunnerCombatantStatus.FirePower));
            }
            if (ruleProfile.ReadsSourceWeakness &&
                state.Get(request.SourceId, MachineGunnerCombatantStatus.Weakness) > 0)
            {
                value = checked(value * 3) / 4;
            }
            if (ruleProfile.ReadsSourceSmoke)
            {
                value = Math.Max(
                    0,
                    checked(value - state.Get(
                        request.SourceId,
                        MachineGunnerCombatantStatus.Smoke)));
            }
            if (ruleProfile.ReadsTargetSmoke)
            {
                value = Math.Max(
                    0,
                    checked(value - state.Get(
                        request.TargetId,
                        MachineGunnerCombatantStatus.Smoke)));
            }
            if (ruleProfile.ReadsTargetVulnerable && target.Vulnerable > 0)
            {
                value = ruleProfile.UsesSniperMultiplier
                    ? checked(value * 2)
                    : checked(value * 3) / 2;
            }
            else if (ruleProfile.UsesSniperMultiplier &&
                state.Get(request.SourceId, MachineGunnerCombatantStatus.Invisible) > 0)
            {
                value = checked(value * 2);
            }

            if (ruleProfile.ReadsArmorBreak)
            {
                int armorBreak = state.Get(
                    request.TargetId,
                    MachineGunnerCombatantStatus.ArmorBreak);
                if (armorBreak > 0)
                {
                    int armorBreakValue = target.Vulnerable > 0
                        ? checked(armorBreak * 3) / 2
                        : armorBreak;
                    value = checked(value + armorBreakValue);
                }
            }

            if (ruleProfile.ReadsTargetInvisible &&
                state.Get(request.TargetId, MachineGunnerCombatantStatus.Invisible) > 0)
            {
                value /= 2;
            }
            return value;
        }

        /// <summary>把有限的伤害来源类别收敛为一份规则档案，使卡牌调用者不必逐项传递力量、烟雾和易伤开关。</summary>
        private readonly struct MachineGunnerDamageRuleProfile
        {
            /// <summary>是否读取来源当前力量。</summary>
            internal bool ReadsSourceStrength { get; }

            /// <summary>是否读取来源虚弱。</summary>
            internal bool ReadsSourceWeakness { get; }

            /// <summary>是否读取来源烟雾。</summary>
            internal bool ReadsSourceSmoke { get; }

            /// <summary>是否读取目标烟雾。</summary>
            internal bool ReadsTargetSmoke { get; }

            /// <summary>是否读取目标易伤。</summary>
            internal bool ReadsTargetVulnerable { get; }

            /// <summary>是否读取来源开火。</summary>
            internal bool ReadsFirePower { get; }

            /// <summary>是否读取目标破甲。</summary>
            internal bool ReadsArmorBreak { get; }

            /// <summary>狙击在易伤或来源隐身时是否改用二倍伤害。</summary>
            internal bool UsesSniperMultiplier { get; }

            /// <summary>是否读取目标隐身并把最终伤害减半。</summary>
            internal bool ReadsTargetInvisible { get; }

            /// <summary>穿透生命后是否按原护甲规则消耗一层。</summary>
            internal bool ConsumesArmor { get; }

            /// <summary>创建一份仅由伤害来源语义决定的不可变规则档案。</summary>
            private MachineGunnerDamageRuleProfile(
                bool readsSourceStrength,
                bool readsSourceWeakness,
                bool readsSourceSmoke,
                bool readsTargetSmoke,
                bool readsTargetVulnerable,
                bool readsFirePower,
                bool readsArmorBreak,
                bool usesSniperMultiplier,
                bool readsTargetInvisible,
                bool consumesArmor)
            {
                ReadsSourceStrength = readsSourceStrength;
                ReadsSourceWeakness = readsSourceWeakness;
                ReadsSourceSmoke = readsSourceSmoke;
                ReadsTargetSmoke = readsTargetSmoke;
                ReadsTargetVulnerable = readsTargetVulnerable;
                ReadsFirePower = readsFirePower;
                ReadsArmorBreak = readsArmorBreak;
                UsesSniperMultiplier = usesSniperMultiplier;
                ReadsTargetInvisible = readsTargetInvisible;
                ConsumesArmor = consumesArmor;
            }

            /// <summary>按来源类别集中解析新版规则，避免各张卡把规则散落在自己的程序分支中。</summary>
            internal static MachineGunnerDamageRuleProfile Create(
                MachineGunnerDamageRequest request)
            {
                switch (request.Kind)
                {
                    case MachineGunnerDamageKind.Attack:
                        return new MachineGunnerDamageRuleProfile(
                            readsSourceStrength: true,
                            readsSourceWeakness: true,
                            readsSourceSmoke: !request.IsSniper,
                            readsTargetSmoke: true,
                            readsTargetVulnerable: true,
                            readsFirePower: request.IsShoot,
                            readsArmorBreak: true,
                            usesSniperMultiplier: request.IsSniper,
                            readsTargetInvisible: true,
                            consumesArmor: true);
                    case MachineGunnerDamageKind.Delayed:
                        return new MachineGunnerDamageRuleProfile(
                            readsSourceStrength: false,
                            readsSourceWeakness: false,
                            readsSourceSmoke: false,
                            readsTargetSmoke: true,
                            readsTargetVulnerable: false,
                            readsFirePower: false,
                            readsArmorBreak: false,
                            usesSniperMultiplier: false,
                            readsTargetInvisible: true,
                            consumesArmor: true);
                    case MachineGunnerDamageKind.Support:
                        return new MachineGunnerDamageRuleProfile(
                            readsSourceStrength: false,
                            readsSourceWeakness: false,
                            readsSourceSmoke: false,
                            readsTargetSmoke: true,
                            readsTargetVulnerable: true,
                            readsFirePower: false,
                            readsArmorBreak: true,
                            usesSniperMultiplier: false,
                            readsTargetInvisible: true,
                            consumesArmor: true);
                    case MachineGunnerDamageKind.Bomb:
                        return new MachineGunnerDamageRuleProfile(
                            readsSourceStrength: false,
                            readsSourceWeakness: false,
                            readsSourceSmoke: false,
                            readsTargetSmoke: true,
                            readsTargetVulnerable: false,
                            readsFirePower: false,
                            readsArmorBreak: false,
                            usesSniperMultiplier: false,
                            readsTargetInvisible: true,
                            consumesArmor: true);
                    case MachineGunnerDamageKind.Burn:
                        return new MachineGunnerDamageRuleProfile(
                            readsSourceStrength: false,
                            readsSourceWeakness: false,
                            readsSourceSmoke: false,
                            readsTargetSmoke: false,
                            readsTargetVulnerable: false,
                            readsFirePower: false,
                            readsArmorBreak: true,
                            usesSniperMultiplier: false,
                            readsTargetInvisible: true,
                            consumesArmor: false);
                    case MachineGunnerDamageKind.Debuff:
                        return new MachineGunnerDamageRuleProfile(
                            readsSourceStrength: false,
                            readsSourceWeakness: false,
                            readsSourceSmoke: false,
                            readsTargetSmoke: false,
                            readsTargetVulnerable: false,
                            readsFirePower: false,
                            readsArmorBreak: false,
                            usesSniperMultiplier: false,
                            readsTargetInvisible: true,
                            consumesArmor: false);
                    case MachineGunnerDamageKind.PortableHelper:
                        return new MachineGunnerDamageRuleProfile(
                            readsSourceStrength: false,
                            readsSourceWeakness: false,
                            readsSourceSmoke: false,
                            readsTargetSmoke: false,
                            readsTargetVulnerable: true,
                            readsFirePower: true,
                            readsArmorBreak: true,
                            usesSniperMultiplier: false,
                            readsTargetInvisible: false,
                            consumesArmor: false);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(request));
                }
            }
        }
    }
}
