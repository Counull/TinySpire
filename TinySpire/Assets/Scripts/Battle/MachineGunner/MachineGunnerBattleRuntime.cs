using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>机枪兵程序向命令输入公开的目标获取方式。</summary>
    internal enum MachineGunnerTargetInputMode
    {
        ExplicitEnemy,
        AutomaticNearestEnemy,
        AutomaticNearestTwoEnemies,
        AutomaticFurthestEnemy,
        AllLivingEnemies,
        RandomLivingEnemy,
        Self,
    }

    /// <summary>机枪兵攻击卡的可组合来源标签；标签只表达分类，具体伤害修正仍由伤害管线按段落语义集中决定。</summary>
    [Flags]
    internal enum MachineGunnerCardTag
    {
        None = 0,
        Shoot = 1 << 0,
        Sniper = 1 << 1,
        Shotgun = 1 << 2,
    }

    /// <summary>机枪兵程序的资源支付策略；具体费用仍只来自生成的卡牌 CostKind 与程序的弹药声明。</summary>
    internal enum MachineGunnerAmmoSpendMode
    {
        None,
        Fixed,
        UpToLimit,
        AllAvailable,
    }

    /// <summary>少量需要动态次数或资源循环的机枪兵程序执行形态。</summary>
    internal enum MachineGunnerProgramExecutionKind
    {
        Standard,
        RepeatByX,
        WildRampage,
        SpendAmmoShots,
        ReloadedAmmoVolley,
        OrderedTargetDamageOperations,
        LinearDamageByTargetOrdinal,
        InitialThenRepeatByTargetStatusKinds,
    }

    /// <summary>本玩家回合最近一张成功结算卡的最低分类，只保留连肘折扣所需事实。</summary>
    internal enum MachineGunnerRecentSuccessfulCardCategory
    {
        None,
        NonShootAttack,
        ShootAttack,
        OtherAttack,
        Other,
    }

    /// <summary>机枪兵程序当前支持的可组合原子操作。</summary>
    internal enum MachineGunnerProgramOperationKind
    {
        Damage,
        GainBlock,
        GainEnergy,
        GainAmmo,
        SpendAmmo,
        FillAmmo,
        DrawCards,
        DrawToHandLimitAfterPlayedCardDeparture,
        AddStimTurns,
        ApplyPrivateStatus,
        ApplyPrivateStatusFromSpentAmmo,
        ApplyBurn,
        ApplyVulnerable,
        ApplyPoisonFromSourceSmoke,
        ResolveIncompleteCombustion,
        ConvertSourceSmokeToTargetBurn,
        ReplaceRemainingHandWithTemporaryCards,
        DrawCardsByActiveStatusKinds,
    }

    /// <summary>机枪兵程序原子操作写入参与者时使用的稳定目标范围。</summary>
    internal enum MachineGunnerOperationTargetScope
    {
        ProgramTargets,
        Source,
        SourceAndProgramTargets,
    }

    /// <summary>机枪兵能力牌在单场职业运行时中的稳定效果身份，不依赖卡牌模板编号或展示文本。</summary>
    internal enum MachineGunnerPowerKind
    {
        CoreExpansion,
        OutputAdjust,
        BlastShield,
        MagExpansion,
        SmokePersist,
        PowerOverclock,
        KungfuMech,
        IncendiaryAmmo,
        AgedOil,
        BurningOil,
        GuerrillaTactics,
        ElectroBoost,
        Bombard,
        SkyWrath,
        PrivateMod,
        PortableHelper,
        Unstoppable,
    }

    /// <summary>一项声明式机枪兵程序操作，不保存战斗内可变事实。</summary>
    internal sealed class MachineGunnerProgramOperation
    {
        /// <summary>本项操作的稳定类别。</summary>
        internal MachineGunnerProgramOperationKind Kind { get; }

        /// <summary>本项操作使用的非负声明值；动态来源操作可用零作为占位。</summary>
        internal int Value { get; }

        /// <summary>该操作从程序目标、施放者或两者组合中读取实际写入目标。</summary>
        internal MachineGunnerOperationTargetScope TargetScope { get; }

        /// <summary>私有状态操作的状态身份；其他操作保持为空。</summary>
        internal MachineGunnerCombatantStatus? PrivateStatus { get; }

        /// <summary>创建经基础数值校验的程序原子操作。</summary>
        internal MachineGunnerProgramOperation(MachineGunnerProgramOperationKind kind, int value)
            : this(
                kind,
                value,
                MachineGunnerOperationTargetScope.ProgramTargets,
                privateStatus: null)
        {
        }

        /// <summary>创建一条为程序目标累加职业私有状态的声明式操作。</summary>
        internal static MachineGunnerProgramOperation ApplyPrivateStatus(
            MachineGunnerCombatantStatus status,
            int value,
            MachineGunnerOperationTargetScope targetScope =
                MachineGunnerOperationTargetScope.ProgramTargets)
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ApplyPrivateStatus,
                value,
                targetScope,
                status);
        }

        /// <summary>创建一条按本次实际弹药消耗向施放者累加私有状态的声明式操作；Value 表示每层状态所需的弹药数。</summary>
        internal static MachineGunnerProgramOperation ApplyPrivateStatusFromSpentAmmo(
            MachineGunnerCombatantStatus status,
            int ammoPerStack,
            MachineGunnerOperationTargetScope targetScope =
                MachineGunnerOperationTargetScope.Source)
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ApplyPrivateStatusFromSpentAmmo,
                ammoPerStack,
                targetScope,
                status);
        }

        /// <summary>创建一条向指定范围施加燃烧并只消耗其既有浸油的声明式操作。</summary>
        internal static MachineGunnerProgramOperation ApplyBurn(
            int value,
            MachineGunnerOperationTargetScope targetScope =
                MachineGunnerOperationTargetScope.ProgramTargets)
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ApplyBurn,
                value,
                targetScope,
                privateStatus: null);
        }

        /// <summary>创建一条为指定范围累加通用易伤的声明式操作。</summary>
        internal static MachineGunnerProgramOperation ApplyVulnerable(
            int value,
            MachineGunnerOperationTargetScope targetScope =
                MachineGunnerOperationTargetScope.ProgramTargets)
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ApplyVulnerable,
                value,
                targetScope,
                privateStatus: null);
        }

        /// <summary>创建一条按施放者命令起点烟雾层数向单一敌方目标施加通用中毒的操作。</summary>
        internal static MachineGunnerProgramOperation ApplyPoisonFromSourceSmoke()
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke,
                value: 0,
                MachineGunnerOperationTargetScope.ProgramTargets,
                privateStatus: null);
        }

        /// <summary>创建一条按初始燃烧来源展开多段 Debuff 伤害，并在伤害全部完成后把幸存者燃烧转为烟雾的专用操作。</summary>
        internal static MachineGunnerProgramOperation ResolveIncompleteCombustion()
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ResolveIncompleteCombustion,
                value: 1,
                MachineGunnerOperationTargetScope.ProgramTargets,
                privateStatus: null);
        }

        /// <summary>创建一条把施放者当前全部烟雾转为目标燃烧的专用复合操作。</summary>
        internal static MachineGunnerProgramOperation ConvertSourceSmokeToTargetBurn()
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ConvertSourceSmokeToTargetBurn,
                value: 1,
                MachineGunnerOperationTargetScope.ProgramTargets,
                privateStatus: null);
        }

        /// <summary>创建一条让当前牌先离手、弃置其余手牌并生成等量指定模板临时牌的专用复合操作。</summary>
        internal static MachineGunnerProgramOperation ReplaceRemainingHandWithTemporaryCards(
            int templateId)
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.ReplaceRemainingHandWithTemporaryCards,
                templateId,
                MachineGunnerOperationTargetScope.Source,
                privateStatus: null);
        }

        /// <summary>创建一条按指定范围内唯一参与者的命令初始活跃状态种类抽牌的声明式操作。</summary>
        internal static MachineGunnerProgramOperation DrawCardsByActiveStatusKinds(
            MachineGunnerOperationTargetScope targetScope)
        {
            return new MachineGunnerProgramOperation(
                MachineGunnerProgramOperationKind.DrawCardsByActiveStatusKinds,
                value: 1,
                targetScope,
                privateStatus: null);
        }

        /// <summary>创建包含范围与可选私有状态身份的完整程序原子操作。</summary>
        private MachineGunnerProgramOperation(
            MachineGunnerProgramOperationKind kind,
            int value,
            MachineGunnerOperationTargetScope targetScope,
            MachineGunnerCombatantStatus? privateStatus)
        {
            bool allowsZeroValue = kind ==
                MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke;
            if (value < 0 || (value == 0 && !allowsZeroValue))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (!Enum.IsDefined(typeof(MachineGunnerProgramOperationKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(MachineGunnerOperationTargetScope), targetScope))
                throw new ArgumentOutOfRangeException(nameof(targetScope));
            if ((kind == MachineGunnerProgramOperationKind.ApplyPrivateStatus ||
                 kind == MachineGunnerProgramOperationKind.ApplyPrivateStatusFromSpentAmmo) &&
                !privateStatus.HasValue)
            {
                throw new ArgumentNullException(nameof(privateStatus));
            }
            if (kind != MachineGunnerProgramOperationKind.ApplyPrivateStatus &&
                kind != MachineGunnerProgramOperationKind.ApplyPrivateStatusFromSpentAmmo &&
                privateStatus.HasValue)
            {
                throw new ArgumentOutOfRangeException(nameof(privateStatus));
            }
            if (privateStatus.HasValue &&
                !Enum.IsDefined(typeof(MachineGunnerCombatantStatus), privateStatus.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(privateStatus));
            }

            Kind = kind;
            Value = value;
            TargetScope = targetScope;
            PrivateStatus = privateStatus;
        }
    }

    /// <summary>一张机枪兵卡的静态程序定义，由生成的 ProgramId 唯一索引。</summary>
    internal sealed class MachineGunnerCardProgram
    {
        /// <summary>配置表绑定的稳定程序标识。</summary>
        internal cfg.battle.MachineGunnerProgramId Id { get; }

        /// <summary>该程序如何取得玩家输入或自动目标。</summary>
        internal MachineGunnerTargetInputMode TargetInputMode { get; }

        /// <summary>基础真实弹药消耗；兴奋剂额外弹由运行时统一叠加。</summary>
        internal int BaseAmmoCost { get; }

        /// <summary>卡牌程序声明的基础命中段数；是否属于射击分类由标签独立决定。</summary>
        internal int BaseShootHitCount { get; }

        /// <summary>程序按声明顺序执行的操作。</summary>
        internal IReadOnlyList<MachineGunnerProgramOperation> Operations { get; }

        /// <summary>每个实际命中段伤害提交后才执行的受限状态操作，始终只作用于该命中目标。</summary>
        internal IReadOnlyList<MachineGunnerProgramOperation> PostHitOperations { get; }

        /// <summary>每个存活命中目标在全局命中钩子完成后，是否按其当前燃烧层数立即结算一次 Debuff 伤害。</summary>
        internal bool TriggersCurrentBurnDebuffAfterGlobalHitEffects { get; }

        /// <summary>能力牌在成功支付后激活的持续效果；非能力牌为 null。</summary>
        internal MachineGunnerPowerKind? PowerKind { get; }

        /// <summary>能力牌每次成功进入 PowerPile 时增加的持续效果层数；非能力牌固定为一且不会读取。</summary>
        internal int PowerStackGain { get; }

        /// <summary>本程序对弹药的动态支付方式；固定费用沿用 BaseAmmoCost。</summary>
        internal MachineGunnerAmmoSpendMode AmmoSpendMode { get; }

        /// <summary>仅供游击战术读取的名义弹药覆盖；为空时沿用本次实际支付。</summary>
        internal int? AmmoSpentForGuerrillaOverride { get; }

        /// <summary>弹药按上限消费时可消耗的最大数量；非该模式固定为零。</summary>
        internal int MaximumAmmoSpend { get; }

        /// <summary>程序是否属于攻击卡，供后续职业私有免费攻击等规则唯一读取。</summary>
        internal bool IsAttack { get; }

        /// <summary>本程序在上一张成功卡为非射击攻击时是否免费支付固定能量。</summary>
        internal bool IsFreeAfterPreviousNonShootAttack { get; }

        /// <summary>本程序成功归宿后是否把“下一张攻击免费”刷新为已激活。</summary>
        internal bool GrantsNextAttackFreeOnSuccess { get; }

        /// <summary>本程序成功时是否在上一张成功牌为攻击或射击后，冻结一张随机攻击手牌作为 Queue continuation。</summary>
        internal bool TriggersRandomHandAttackAfterPreviousAttackOrShoot { get; }

        /// <summary>程序来源的唯一分类事实；卡程序只声明标签，伤害修正由伤害管线集中解释。</summary>
        internal MachineGunnerCardTag Tags { get; }

        /// <summary>程序是否带有普通射击标签；该标签决定兴奋剂额外弹。</summary>
        internal bool IsShoot { get; }

        /// <summary>该程序是否属于任一种射击分类，只供“非射击”条件统一读取。</summary>
        internal bool IsShootCategory { get; }

        /// <summary>该程序是否作为非射击攻击参与连肘、功夫机甲与陈年机油联动。</summary>
        internal bool ParticipatesInNonShootAttackSynergies { get; }

        /// <summary>该射击是否可获得兴奋剂额外弹；由普通射击标签派生，纯狙击不满足此条件。</summary>
        internal bool ReceivesStimBonus { get; }

        /// <summary>该程序是否可从燃烧弹药获得命中后燃烧；普通射击与纯狙击均满足，霰弹另由其专属规则决定。</summary>
        internal bool ReceivesIncendiaryAmmo { get; }

        /// <summary>狙击程序在易伤或施放者隐身时使用职业专属的二倍倍率。</summary>
        internal bool IsSniper { get; }

        /// <summary>程序成功攻击后是否保留施放者的隐身；该生命周期语义独立于狙击伤害公式，支持射击加狙击双词条。</summary>
        internal bool PreservesInvisibleAfterSuccessfulAttack { get; }

        /// <summary>程序完整提交并进入卡区归宿后是否要求 Queue 续接一次结束玩家行动命令。</summary>
        internal bool EndsPlayerActionAfterSuccessfulPlay { get; }

        /// <summary>成功出牌时创建的独立延迟效果规格；即时卡与能力牌为空。</summary>
        internal MachineGunnerScheduledEffectSpec ScheduledEffect { get; }

        /// <summary>本程序需要按 X、当前弹药等动态输入展开的专用执行形态。</summary>
        internal MachineGunnerProgramExecutionKind ExecutionKind { get; }

        /// <summary>创建一个不依赖卡牌名称、外部键或裸模板 ID 的程序定义。</summary>
        internal MachineGunnerCardProgram(
            cfg.battle.MachineGunnerProgramId id,
            MachineGunnerTargetInputMode targetInputMode,
            int baseAmmoCost,
            int baseShootHitCount,
            IEnumerable<MachineGunnerProgramOperation> operations,
            MachineGunnerPowerKind? powerKind = null,
            int powerStackGain = 1,
            MachineGunnerAmmoSpendMode ammoSpendMode = MachineGunnerAmmoSpendMode.Fixed,
            int maximumAmmoSpend = 0,
            bool isAttack = false,
            bool isFreeAfterPreviousNonShootAttack = false,
            bool grantsNextAttackFreeOnSuccess = false,
            bool isShoot = false,
            bool receivesStimBonus = false,
            bool isSniper = false,
            MachineGunnerCardTag tags = MachineGunnerCardTag.None,
            MachineGunnerProgramExecutionKind executionKind = MachineGunnerProgramExecutionKind.Standard,
            IEnumerable<MachineGunnerProgramOperation> postHitOperations = null,
            bool triggersCurrentBurnDebuffAfterGlobalHitEffects = false,
            bool preservesInvisibleAfterSuccessfulAttack = false,
            bool endsPlayerActionAfterSuccessfulPlay = false,
            MachineGunnerScheduledEffectSpec scheduledEffect = null,
            int? ammoSpentForGuerrillaOverride = null,
            bool participatesInNonShootAttackSynergies = true,
            bool triggersRandomHandAttackAfterPreviousAttackOrShoot = false)
        {
            if (id == cfg.battle.MachineGunnerProgramId.None)
                throw new ArgumentOutOfRangeException(nameof(id));
            if (baseAmmoCost < 0)
                throw new ArgumentOutOfRangeException(nameof(baseAmmoCost));
            if (baseShootHitCount < 0)
                throw new ArgumentOutOfRangeException(nameof(baseShootHitCount));
            if (!Enum.IsDefined(typeof(MachineGunnerAmmoSpendMode), ammoSpendMode))
                throw new ArgumentOutOfRangeException(nameof(ammoSpendMode));
            if (maximumAmmoSpend < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumAmmoSpend));
            if (ammoSpendMode == MachineGunnerAmmoSpendMode.UpToLimit &&
                maximumAmmoSpend < baseAmmoCost)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumAmmoSpend));
            }
            if (ammoSpendMode != MachineGunnerAmmoSpendMode.UpToLimit && maximumAmmoSpend != 0)
                throw new ArgumentOutOfRangeException(nameof(maximumAmmoSpend));
            if (!Enum.IsDefined(typeof(MachineGunnerProgramExecutionKind), executionKind))
                throw new ArgumentOutOfRangeException(nameof(executionKind));
            const MachineGunnerCardTag supportedTags =
                MachineGunnerCardTag.Shoot |
                MachineGunnerCardTag.Sniper |
                MachineGunnerCardTag.Shotgun;
            if ((tags & ~supportedTags) != MachineGunnerCardTag.None)
                throw new ArgumentOutOfRangeException(nameof(tags));

            MachineGunnerCardTag legacyTags = MachineGunnerCardTag.None;
            if (isShoot)
                legacyTags |= MachineGunnerCardTag.Shoot;
            if (isSniper)
                legacyTags |= MachineGunnerCardTag.Sniper;
            if (tags != MachineGunnerCardTag.None &&
                (tags & legacyTags) != legacyTags)
            {
                throw new ArgumentOutOfRangeException(nameof(tags));
            }

            MachineGunnerCardTag resolvedTags = tags | legacyTags;
            bool isShootCategory = (resolvedTags &
                (MachineGunnerCardTag.Shoot |
                 MachineGunnerCardTag.Sniper |
                 MachineGunnerCardTag.Shotgun)) != MachineGunnerCardTag.None;
            bool receivesStimFromTag =
                (resolvedTags & MachineGunnerCardTag.Shoot) != MachineGunnerCardTag.None;
            bool receivesIncendiaryFromTag = (resolvedTags &
                (MachineGunnerCardTag.Shoot | MachineGunnerCardTag.Sniper)) !=
                MachineGunnerCardTag.None;
            bool isSniperFromTag =
                (resolvedTags & MachineGunnerCardTag.Sniper) != MachineGunnerCardTag.None;
            if (receivesStimBonus && !receivesStimFromTag)
                throw new ArgumentOutOfRangeException(nameof(receivesStimBonus));
            if (isSniperFromTag && !isAttack)
                throw new ArgumentOutOfRangeException(nameof(tags));
            if (isShootCategory && !isAttack)
                throw new ArgumentOutOfRangeException(nameof(tags));
            if (isFreeAfterPreviousNonShootAttack && (!isAttack || isShootCategory))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(isFreeAfterPreviousNonShootAttack));
            }
            if (triggersCurrentBurnDebuffAfterGlobalHitEffects && !isAttack)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triggersCurrentBurnDebuffAfterGlobalHitEffects));
            }
            if (preservesInvisibleAfterSuccessfulAttack && !isAttack)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preservesInvisibleAfterSuccessfulAttack));
            }
            if (triggersRandomHandAttackAfterPreviousAttackOrShoot && !isAttack)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triggersRandomHandAttackAfterPreviousAttackOrShoot));
            }
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (powerKind.HasValue &&
                !Enum.IsDefined(typeof(MachineGunnerPowerKind), powerKind.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(powerKind));
            }
            if (powerStackGain <= 0)
                throw new ArgumentOutOfRangeException(nameof(powerStackGain));
            if (!powerKind.HasValue && powerStackGain != 1)
                throw new ArgumentOutOfRangeException(nameof(powerStackGain));
            if (powerKind.HasValue && scheduledEffect != null)
                throw new ArgumentOutOfRangeException(nameof(scheduledEffect));
            if (ammoSpentForGuerrillaOverride.HasValue &&
                ammoSpentForGuerrillaOverride.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoSpentForGuerrillaOverride));
            }

            var copiedPostHitOperations = new List<MachineGunnerProgramOperation>();
            if (postHitOperations != null)
            {
                foreach (MachineGunnerProgramOperation postHitOperation in postHitOperations)
                {
                    if (postHitOperation == null)
                        throw new ArgumentException("命中后操作不能包含空值。", nameof(postHitOperations));
                    if (postHitOperation.TargetScope != MachineGunnerOperationTargetScope.ProgramTargets)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(postHitOperations),
                            "命中后操作只能作用于当前实际命中的目标。");
                    }

                    switch (postHitOperation.Kind)
                    {
                        case MachineGunnerProgramOperationKind.ApplyPrivateStatus:
                        case MachineGunnerProgramOperationKind.ApplyBurn:
                        case MachineGunnerProgramOperationKind.ApplyVulnerable:
                            copiedPostHitOperations.Add(postHitOperation);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(postHitOperations),
                                "命中后操作只能写入私有状态、燃烧或易伤。");
                    }
                }
            }
            if (copiedPostHitOperations.Count > 0 && !isAttack)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(postHitOperations),
                    "只有攻击程序可以声明逐段命中后的状态操作。");
            }

            Id = id;
            TargetInputMode = targetInputMode;
            BaseAmmoCost = baseAmmoCost;
            BaseShootHitCount = baseShootHitCount;
            Operations = new ReadOnlyCollection<MachineGunnerProgramOperation>(
                new List<MachineGunnerProgramOperation>(operations));
            if (executionKind == MachineGunnerProgramExecutionKind.OrderedTargetDamageOperations)
            {
                if (targetInputMode != MachineGunnerTargetInputMode.AutomaticNearestTwoEnemies ||
                    Operations.Count != 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(executionKind));
                }
                foreach (MachineGunnerProgramOperation operation in Operations)
                {
                    if (operation == null || operation.Kind != MachineGunnerProgramOperationKind.Damage)
                        throw new ArgumentOutOfRangeException(nameof(operations));
                }
            }
            if (executionKind == MachineGunnerProgramExecutionKind.LinearDamageByTargetOrdinal)
            {
                if (targetInputMode != MachineGunnerTargetInputMode.AllLivingEnemies ||
                    !isAttack ||
                    baseShootHitCount != 0 ||
                    Operations.Count != 1 ||
                    Operations[0] == null ||
                    Operations[0].Kind != MachineGunnerProgramOperationKind.Damage)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(executionKind),
                        "按目标序号线性增长只支持一段作用于全体存活敌人的攻击伤害。");
                }
            }
            if (executionKind == MachineGunnerProgramExecutionKind.ReloadedAmmoVolley)
            {
                if (targetInputMode != MachineGunnerTargetInputMode.AutomaticNearestEnemy ||
                    !isAttack ||
                    !receivesStimFromTag ||
                    ammoSpendMode != MachineGunnerAmmoSpendMode.None ||
                    baseAmmoCost != 0 ||
                    baseShootHitCount != 0 ||
                    Operations.Count != 1 ||
                    Operations[0] == null ||
                    Operations[0].Kind != MachineGunnerProgramOperationKind.Damage)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(executionKind),
                        "换弹连射只支持一项对最近敌人的普通射击伤害声明，弹药轨迹由专用纯计划冻结。");
                }
            }
            if (executionKind ==
                MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds)
            {
                if (targetInputMode != MachineGunnerTargetInputMode.ExplicitEnemy ||
                    !isAttack ||
                    !receivesStimFromTag ||
                    ammoSpendMode != MachineGunnerAmmoSpendMode.Fixed ||
                    baseAmmoCost != 1 ||
                    baseShootHitCount != 0 ||
                    Operations.Count != 2 ||
                    Operations[0] == null ||
                    Operations[0].Kind != MachineGunnerProgramOperationKind.Damage ||
                    Operations[1] == null ||
                    Operations[1].Kind != MachineGunnerProgramOperationKind.Damage)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(executionKind),
                        "按目标起始状态展开的射击必须声明两段固定基础伤害及一发基础弹药。");
                }
            }
            PostHitOperations = new ReadOnlyCollection<MachineGunnerProgramOperation>(
                copiedPostHitOperations);
            TriggersCurrentBurnDebuffAfterGlobalHitEffects =
                triggersCurrentBurnDebuffAfterGlobalHitEffects;
            PowerKind = powerKind;
            PowerStackGain = powerStackGain;
            AmmoSpendMode = ammoSpendMode;
            AmmoSpentForGuerrillaOverride = ammoSpentForGuerrillaOverride;
            MaximumAmmoSpend = maximumAmmoSpend;
            IsAttack = isAttack;
            IsFreeAfterPreviousNonShootAttack = isFreeAfterPreviousNonShootAttack;
            GrantsNextAttackFreeOnSuccess = grantsNextAttackFreeOnSuccess;
            TriggersRandomHandAttackAfterPreviousAttackOrShoot =
                triggersRandomHandAttackAfterPreviousAttackOrShoot;
            Tags = resolvedTags;
            IsShoot = receivesStimFromTag;
            IsShootCategory = isShootCategory;
            ParticipatesInNonShootAttackSynergies =
                participatesInNonShootAttackSynergies && isAttack && !isShootCategory;
            ReceivesStimBonus = receivesStimFromTag;
            ReceivesIncendiaryAmmo = receivesIncendiaryFromTag;
            IsSniper = isSniperFromTag;
            ExecutionKind = executionKind;
            PreservesInvisibleAfterSuccessfulAttack = preservesInvisibleAfterSuccessfulAttack;
            EndsPlayerActionAfterSuccessfulPlay = endsPlayerActionAfterSuccessfulPlay;
            ScheduledEffect = scheduledEffect;
        }
    }

    /// <summary>把生成的机枪兵 ProgramId 映射为可组合的运行时程序，不解析卡牌文本或外部键。</summary>
    internal static class MachineGunnerCardProgramRegistry
    {
        private const int MachinegunBurstTemplateId = 3263;

        private static readonly IReadOnlyDictionary<cfg.battle.MachineGunnerProgramId, MachineGunnerCardProgram>
            Programs = new ReadOnlyDictionary<cfg.battle.MachineGunnerProgramId, MachineGunnerCardProgram>(
                new Dictionary<cfg.battle.MachineGunnerProgramId, MachineGunnerCardProgram>
                {
                    [cfg.battle.MachineGunnerProgramId.Shoot] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Shoot,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 1,
                        baseShootHitCount: 1,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 6),
                        },
                        isAttack: true,
                        isShoot: true,
                        receivesStimBonus: true),
                    [cfg.battle.MachineGunnerProgramId.Elbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Elbow,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 6),
                        },
                        isAttack: true),
                    [cfg.battle.MachineGunnerProgramId.Block] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Block,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 5),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Reload] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Reload,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.FillAmmo, 1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Stim] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Stim,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.DrawCards, 2),
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.AddStimTurns, 1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.CoreExpansion] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.CoreExpansion,
                        MachineGunnerPowerKind.CoreExpansion),
                    [cfg.battle.MachineGunnerProgramId.OutputAdjust] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.OutputAdjust,
                        MachineGunnerPowerKind.OutputAdjust),
                    [cfg.battle.MachineGunnerProgramId.BlastShield] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.BlastShield,
                        MachineGunnerPowerKind.BlastShield),
                    [cfg.battle.MachineGunnerProgramId.MagExpansion] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.MagExpansion,
                        MachineGunnerPowerKind.MagExpansion),
                    [cfg.battle.MachineGunnerProgramId.IncendiaryAmmo] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.IncendiaryAmmo,
                        MachineGunnerPowerKind.IncendiaryAmmo),
                    [cfg.battle.MachineGunnerProgramId.SmokePersist] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.SmokePersist,
                        MachineGunnerPowerKind.SmokePersist),
                    [cfg.battle.MachineGunnerProgramId.PowerOverclock] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.PowerOverclock,
                        MachineGunnerPowerKind.PowerOverclock),
                    // 驻防复用通用格挡操作；跨回合保留与选牌由各自深模块独立拥有。
                    [cfg.battle.MachineGunnerProgramId.Garrison] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Garrison,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.GainBlock,
                                12),
                        }),
                    [cfg.battle.MachineGunnerProgramId.KungfuMech] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.KungfuMech,
                        MachineGunnerPowerKind.KungfuMech),
                    [cfg.battle.MachineGunnerProgramId.AgedOil] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.AgedOil,
                        MachineGunnerPowerKind.AgedOil),
                    [cfg.battle.MachineGunnerProgramId.BurningOil] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.BurningOil,
                        MachineGunnerPowerKind.BurningOil),
                    [cfg.battle.MachineGunnerProgramId.GuerrillaTactics] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.GuerrillaTactics,
                        MachineGunnerPowerKind.GuerrillaTactics,
                        powerStackGain: 2),
                    [cfg.battle.MachineGunnerProgramId.Overload] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Overload,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.GainEnergy,
                                2),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    [cfg.battle.MachineGunnerProgramId.TumbleReload] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.TumbleReload,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 10),
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.FillAmmo, 1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Retreat] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Retreat,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 15),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                        },
                        endsPlayerActionAfterSuccessfulPlay: true),
                    [cfg.battle.MachineGunnerProgramId.GasPump] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.GasPump,
                        MachineGunnerTargetInputMode.AllLivingEnemies,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Oil,
                                5),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Napalm] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Napalm,
                        MachineGunnerTargetInputMode.AllLivingEnemies,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyBurn(3),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Oil,
                                5),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Molotov] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Molotov,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyBurn(5),
                        }),
                    [cfg.battle.MachineGunnerProgramId.StunGrenade] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.StunGrenade,
                        MachineGunnerTargetInputMode.AllLivingEnemies,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 8),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Weakness,
                                1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.HoldLine] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.HoldLine,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 5),
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainAmmo, 1),
                        },
                        executionKind: MachineGunnerProgramExecutionKind.RepeatByX),
                    [cfg.battle.MachineGunnerProgramId.SmokeBomb] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.SmokeBomb,
                        MachineGunnerTargetInputMode.AllLivingEnemies,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 10),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Smoke,
                                3,
                                MachineGunnerOperationTargetScope.SourceAndProgramTargets),
                        }),
                    [cfg.battle.MachineGunnerProgramId.KnockbackShot] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.KnockbackShot,
                        MachineGunnerTargetInputMode.AutomaticNearestTwoEnemies,
                        baseAmmoCost: 1,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 7),
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 3),
                        },
                        isAttack: true,
                        executionKind: MachineGunnerProgramExecutionKind.OrderedTargetDamageOperations,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.LoseStrength,
                                2),
                        }),
                    [cfg.battle.MachineGunnerProgramId.IncompleteCombustion] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.IncompleteCombustion,
                        MachineGunnerTargetInputMode.AllLivingEnemies,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ResolveIncompleteCombustion(),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Spray] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Spray,
                        MachineGunnerTargetInputMode.RandomLivingEnemy,
                        baseAmmoCost: 2,
                        baseShootHitCount: 2,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 7),
                        },
                        isAttack: true,
                        isShoot: true,
                        receivesStimBonus: true),
                    [cfg.battle.MachineGunnerProgramId.BayonetParry] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.BayonetParry,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 7),
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 7),
                        },
                        isAttack: true),
                    [cfg.battle.MachineGunnerProgramId.WildRampage] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.WildRampage,
                        MachineGunnerTargetInputMode.RandomLivingEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 5),
                        },
                        ammoSpendMode: MachineGunnerAmmoSpendMode.AllAvailable,
                        isAttack: true,
                        isShoot: true,
                        receivesStimBonus: true,
                        executionKind: MachineGunnerProgramExecutionKind.WildRampage),
                    [cfg.battle.MachineGunnerProgramId.QuickElbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.QuickElbow,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 6),
                        },
                        isAttack: true),
                    [cfg.battle.MachineGunnerProgramId.KidneyShot] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.KidneyShot,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 8),
                        },
                        isAttack: true,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Weakness,
                                1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.PainfulElbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.PainfulElbow,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 10),
                        },
                        isAttack: true,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyVulnerable(2),
                        }),
                    [cfg.battle.MachineGunnerProgramId.HeavyElbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.HeavyElbow,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 33),
                        },
                        isAttack: true),
                    [cfg.battle.MachineGunnerProgramId.FieldSurgery] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.FieldSurgery,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Regeneration,
                                5,
                                MachineGunnerOperationTargetScope.Source),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Shackle,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    [cfg.battle.MachineGunnerProgramId.HurricaneElbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.HurricaneElbow,
                        MachineGunnerTargetInputMode.RandomLivingEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 7),
                        },
                        isAttack: true,
                        executionKind: MachineGunnerProgramExecutionKind.RepeatByX),
                    [cfg.battle.MachineGunnerProgramId.PrecisionShot] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.PrecisionShot,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 1,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 7),
                        },
                        ammoSpendMode: MachineGunnerAmmoSpendMode.UpToLimit,
                        maximumAmmoSpend: 3,
                        isAttack: true,
                        isShoot: true,
                        receivesStimBonus: true,
                        executionKind: MachineGunnerProgramExecutionKind.SpendAmmoShots),
                    // 战术突进先获得格挡，只有成功归宿后才以二进制刷新下一张攻击免费。
                    [cfg.battle.MachineGunnerProgramId.TacticalAdvance] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.TacticalAdvance,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.GainBlock,
                                10),
                        },
                        grantsNextAttackFreeOnSuccess: true),
                    [cfg.battle.MachineGunnerProgramId.QuickRoll] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.QuickRoll,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 5),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.NextRoundBlock,
                                5,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    [cfg.battle.MachineGunnerProgramId.SixHits] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.SixHits,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 1,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 5),
                        },
                        ammoSpendMode: MachineGunnerAmmoSpendMode.UpToLimit,
                        maximumAmmoSpend: 6,
                        isAttack: true,
                        isShoot: true,
                        receivesStimBonus: true,
                        executionKind: MachineGunnerProgramExecutionKind.SpendAmmoShots),
                    [cfg.battle.MachineGunnerProgramId.TwelveHits] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.TwelveHits,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 5),
                        },
                        ammoSpendMode: MachineGunnerAmmoSpendMode.None,
                        isAttack: true,
                        isShoot: true,
                        receivesStimBonus: true,
                        executionKind: MachineGunnerProgramExecutionKind.ReloadedAmmoVolley),
                    [cfg.battle.MachineGunnerProgramId.QuickManeuver] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.QuickManeuver,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.GainBlock, 5),
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.DrawCards, 1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.SniperShot] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.SniperShot,
                        MachineGunnerTargetInputMode.AutomaticFurthestEnemy,
                        baseAmmoCost: 2,
                        baseShootHitCount: 1,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 13),
                        },
                        isAttack: true,
                        tags: MachineGunnerCardTag.Sniper,
                        preservesInvisibleAfterSuccessfulAttack: true,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyVulnerable(1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.SpikeShot] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.SpikeShot,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 1,
                        baseShootHitCount: 1,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 1),
                        },
                        isAttack: true,
                        tags: MachineGunnerCardTag.Shoot | MachineGunnerCardTag.Sniper,
                        preservesInvisibleAfterSuccessfulAttack: true,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Weakness,
                                1),
                            MachineGunnerProgramOperation.ApplyVulnerable(1),
                        }),
                    [cfg.battle.MachineGunnerProgramId.OpticalCamo] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.OpticalCamo,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Invisible,
                                2,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    [cfg.battle.MachineGunnerProgramId.HoloDecoy] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.HoloDecoy,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Buffer,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    [cfg.battle.MachineGunnerProgramId.DefenseTarget] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.DefenseTarget,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 2,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatusFromSpentAmmo(
                                MachineGunnerCombatantStatus.Intangible,
                                ammoPerStack: 3),
                        },
                        ammoSpendMode: MachineGunnerAmmoSpendMode.UpToLimit,
                        maximumAmmoSpend: 9),
                    // 固定机枪先获得格挡，再让自身离手并把其余手牌原序替换为等量机枪扫射。
                    [cfg.battle.MachineGunnerProgramId.Machinegun] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Machinegun,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.GainBlock,
                                10),
                            MachineGunnerProgramOperation.ReplaceRemainingHandWithTemporaryCards(
                                MachinegunBurstTemplateId),
                        }),
                    [cfg.battle.MachineGunnerProgramId.MachinegunBurst] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.MachinegunBurst,
                        MachineGunnerTargetInputMode.RandomLivingEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 2,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.Damage,
                                5),
                        },
                        isAttack: true,
                        ammoSpentForGuerrillaOverride: 2,
                        participatesInNonShootAttackSynergies: false),
                    [cfg.battle.MachineGunnerProgramId.ExplosiveElbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.ExplosiveElbow,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 10),
                        },
                        isAttack: true,
                        triggersCurrentBurnDebuffAfterGlobalHitEffects: true),
                    [cfg.battle.MachineGunnerProgramId.FlameElbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.FlameElbow,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 6),
                        },
                        isAttack: true,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyBurn(3),
                        }),
                    [cfg.battle.MachineGunnerProgramId.ElectroBoost] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.ElectroBoost,
                        MachineGunnerPowerKind.ElectroBoost,
                        operations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.FirePower,
                                3,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    // 轰炸每次成功打出叠加四层，触发时再由支援延迟效果读取当前总层数。
                    [cfg.battle.MachineGunnerProgramId.Bombard] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.Bombard,
                        MachineGunnerPowerKind.Bombard,
                        powerStackGain: 4),
                    // 天空之怒每次成功打出只叠加一层持续效果，实际支援追击在延迟实例预演时逐段展开。
                    [cfg.battle.MachineGunnerProgramId.SkyWrath] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.SkyWrath,
                        MachineGunnerPowerKind.SkyWrath),
                    // 便携帮手每次成功打出只叠加一层持续效果，不产生即时资源或状态写入。
                    [cfg.battle.MachineGunnerProgramId.PortableHelper] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.PortableHelper,
                        MachineGunnerPowerKind.PortableHelper),
                    // 私人改装只提高弹药上限而不补充当前弹药，并在同一出牌事务内叠加一层开火。
                    [cfg.battle.MachineGunnerProgramId.PrivateMod] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.PrivateMod,
                        MachineGunnerPowerKind.PrivateMod,
                        operations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.FirePower,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Unstoppable] = CreatePower(
                        cfg.battle.MachineGunnerProgramId.Unstoppable,
                        MachineGunnerPowerKind.Unstoppable),
                    [cfg.battle.MachineGunnerProgramId.GuidedNuke] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.GuidedNuke,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Shackle,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                        },
                        scheduledEffect: new MachineGunnerScheduledEffectSpec(
                            MachineGunnerScheduledEffectKind.GuidedNuke,
                            MachineGunnerScheduledEffectTiming.PlayerRoundEnd,
                            countdown: 4,
                            remainingTriggers: 0,
                            damage: 99)),
                    [cfg.battle.MachineGunnerProgramId.BansheeStrike] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.BansheeStrike,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        Array.Empty<MachineGunnerProgramOperation>(),
                        scheduledEffect: new MachineGunnerScheduledEffectSpec(
                            MachineGunnerScheduledEffectKind.BansheeStrike,
                            MachineGunnerScheduledEffectTiming.PlayerRoundStart,
                            countdown: 0,
                            remainingTriggers: 2,
                            damage: 8,
                            hitCount: 2)),
                    [cfg.battle.MachineGunnerProgramId.FireSupport] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.FireSupport,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        Array.Empty<MachineGunnerProgramOperation>(),
                        scheduledEffect: new MachineGunnerScheduledEffectSpec(
                            MachineGunnerScheduledEffectKind.FireSupport,
                            MachineGunnerScheduledEffectTiming.PlayerRoundStart,
                            countdown: 0,
                            remainingTriggers: 1,
                            damage: 2,
                            hitCount: 5)),
                    [cfg.battle.MachineGunnerProgramId.FireBombardment] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.FireBombardment,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        Array.Empty<MachineGunnerProgramOperation>(),
                        scheduledEffect: new MachineGunnerScheduledEffectSpec(
                            MachineGunnerScheduledEffectKind.FireBombardment,
                            MachineGunnerScheduledEffectTiming.PlayerRoundStart,
                            countdown: 0,
                            remainingTriggers: 1,
                            damage: 2,
                            waveCount: 2,
                            burn: 4,
                            oil: 3)),
                    [cfg.battle.MachineGunnerProgramId.FiveHundredPounder] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.FiveHundredPounder,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        Array.Empty<MachineGunnerProgramOperation>(),
                        scheduledEffect: new MachineGunnerScheduledEffectSpec(
                            MachineGunnerScheduledEffectKind.FiveHundredPounder,
                            MachineGunnerScheduledEffectTiming.PlayerRoundEnd,
                            countdown: 3,
                            remainingTriggers: 0,
                            damage: 60)),
                    [cfg.battle.MachineGunnerProgramId.ComboElbow] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.ComboElbow,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 10),
                        },
                        isAttack: true,
                        isFreeAfterPreviousNonShootAttack: true),
                    // 排气散热的零候选切片只按自目标成功出牌；选牌与能量收益由后续切片补齐。
                    [cfg.battle.MachineGunnerProgramId.OpportunisticStrike] =
                        new MachineGunnerCardProgram(
                            cfg.battle.MachineGunnerProgramId.OpportunisticStrike,
                            MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                            baseAmmoCost: 0,
                            baseShootHitCount: 0,
                            new[]
                            {
                                new MachineGunnerProgramOperation(
                                    MachineGunnerProgramOperationKind.Damage,
                                    6),
                            },
                            isAttack: true,
                            triggersRandomHandAttackAfterPreviousAttackOrShoot: true),
                    [cfg.battle.MachineGunnerProgramId.VentHeat] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.VentHeat,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        Array.Empty<MachineGunnerProgramOperation>()),
                    [cfg.battle.MachineGunnerProgramId.ThermiteBomb] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.ThermiteBomb,
                        MachineGunnerTargetInputMode.AllLivingEnemies,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyBurn(4),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.ArmorBreak,
                                2),
                        }),
                    [cfg.battle.MachineGunnerProgramId.Crush] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Crush,
                        MachineGunnerTargetInputMode.AutomaticNearestEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(MachineGunnerProgramOperationKind.Damage, 9),
                        },
                        isAttack: true,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.ArmorBreak,
                                4),
                        }),
                    [cfg.battle.MachineGunnerProgramId.TripleStrike] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.TripleStrike,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 3,
                        baseShootHitCount: 2,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Invisible,
                                2,
                                MachineGunnerOperationTargetScope.Source),
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.Damage,
                                12),
                        },
                        isAttack: true,
                        tags: MachineGunnerCardTag.Sniper,
                        preservesInvisibleAfterSuccessfulAttack: true,
                        scheduledEffect: new MachineGunnerScheduledEffectSpec(
                            MachineGunnerScheduledEffectKind.TripleStrike,
                            MachineGunnerScheduledEffectTiming.PlayerRoundStart,
                            countdown: 0,
                            remainingTriggers: 1,
                            damage: 20)),
                    [cfg.battle.MachineGunnerProgramId.NeedleStorm] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.NeedleStorm,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        Array.Empty<MachineGunnerProgramOperation>(),
                        scheduledEffect: new MachineGunnerScheduledEffectSpec(
                            MachineGunnerScheduledEffectKind.NeedleStorm,
                            MachineGunnerScheduledEffectTiming.PlayerRoundStart,
                            countdown: 0,
                            remainingTriggers: 1,
                            damage: 1,
                             hitCount: 4,
                             armorBreak: 1)),
                    [cfg.battle.MachineGunnerProgramId.DefensiveStance] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.DefensiveStance,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.GainBlock,
                                8),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.NextRoundEnergyGainBonus,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    // 连锁烟雾只为施放者叠加烟雾，不借用敌方目标或射击分类。
                    [cfg.battle.MachineGunnerProgramId.ChainSmoke] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.ChainSmoke,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Smoke,
                                5,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    // 紧急冷却严格先获得格挡，再为施放者叠加烟雾。
                    // 二手烟只读取施放者命令起点烟雾并向显式敌方施加等量通用中毒，烟雾保持不变。
                    [cfg.battle.MachineGunnerProgramId.SecondhandSmoke] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.SecondhandSmoke,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPoisonFromSourceSmoke(),
                        }),
                    [cfg.battle.MachineGunnerProgramId.EmergencyCooling] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.EmergencyCooling,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.GainBlock,
                                8),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Smoke,
                                3,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    // 标记是无射击分类的普通攻击，且只在目标受击后仍存活时施加破甲。
                    [cfg.battle.MachineGunnerProgramId.Mark] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Mark,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 1,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.Damage,
                                5),
                        },
                        isAttack: true,
                        tags: MachineGunnerCardTag.None,
                        postHitOperations: new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.ArmorBreak,
                                2),
                        }),
                    // 欺凌冻结命令开始时目标的活跃状态种类，伤害及既有命中后链完成后仍按该旧值抽牌。
                    [cfg.battle.MachineGunnerProgramId.Bully] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.Bully,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.Damage,
                                6),
                            MachineGunnerProgramOperation.DrawCardsByActiveStatusKinds(
                                MachineGunnerOperationTargetScope.ProgramTargets),
                        },
                        isAttack: true,
                        tags: MachineGunnerCardTag.None),
                    // 幻彩射击先造成固定首击，再按目标命令起始状态种类展开九点逻辑射击。
                    [cfg.battle.MachineGunnerProgramId.PrismaticShot] =
                        new MachineGunnerCardProgram(
                            cfg.battle.MachineGunnerProgramId.PrismaticShot,
                            MachineGunnerTargetInputMode.ExplicitEnemy,
                            baseAmmoCost: 1,
                            baseShootHitCount: 0,
                            new[]
                            {
                                new MachineGunnerProgramOperation(
                                    MachineGunnerProgramOperationKind.Damage,
                                    6),
                                new MachineGunnerProgramOperation(
                                    MachineGunnerProgramOperationKind.Damage,
                                    9),
                            },
                            isAttack: true,
                            tags: MachineGunnerCardTag.Shoot,
                            executionKind:
                                MachineGunnerProgramExecutionKind.
                                    InitialThenRepeatByTargetStatusKinds),
                    // 先发制人冻结命令开始时施放者的活跃状态种类，并在伤害及既有命中后链完成后按旧值抽牌。
                    [cfg.battle.MachineGunnerProgramId.PreemptiveStrike] =
                        new MachineGunnerCardProgram(
                            cfg.battle.MachineGunnerProgramId.PreemptiveStrike,
                            MachineGunnerTargetInputMode.ExplicitEnemy,
                            baseAmmoCost: 1,
                            baseShootHitCount: 0,
                            new[]
                            {
                                new MachineGunnerProgramOperation(
                                    MachineGunnerProgramOperationKind.Damage,
                                    8),
                                MachineGunnerProgramOperation.DrawCardsByActiveStatusKinds(
                                    MachineGunnerOperationTargetScope.Source),
                            },
                            isAttack: true,
                            tags: MachineGunnerCardTag.None),
                    // 焚风只接受显式敌方目标，并把施放者当前全部烟雾作为一次燃烧基础值后清零。
                    [cfg.battle.MachineGunnerProgramId.FoehnWind] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.FoehnWind,
                        MachineGunnerTargetInputMode.ExplicitEnemy,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ConvertSourceSmokeToTargetBurn(),
                        }),
                    // 蓄力爆发按施放时存活敌人的 Encounter 序号线性提高基础伤害，并保留狙击隐身。
                    [cfg.battle.MachineGunnerProgramId.ChargedBurst] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.ChargedBurst,
                        MachineGunnerTargetInputMode.AllLivingEnemies,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.Damage,
                                12),
                        },
                        isAttack: true,
                        tags: MachineGunnerCardTag.Sniper,
                        executionKind: MachineGunnerProgramExecutionKind.LinearDamageByTargetOrdinal,
                        preservesInvisibleAfterSuccessfulAttack: true),
                    // 极限超载先获得能量，再让当前牌逻辑离手并抽至十张，最后叠加三层下回合能量惩罚。
                    [cfg.battle.MachineGunnerProgramId.LimitOverload] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.LimitOverload,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.GainEnergy,
                                1),
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.DrawToHandLimitAfterPlayedCardDeparture,
                                10),
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty,
                                3,
                                MachineGunnerOperationTargetScope.Source),
                        }),
                    // 隐秘行动先为施放者叠加一层隐身，再按当前手牌上限执行一次普通抽牌。
                    [cfg.battle.MachineGunnerProgramId.StealthAction] = new MachineGunnerCardProgram(
                        cfg.battle.MachineGunnerProgramId.StealthAction,
                        MachineGunnerTargetInputMode.Self,
                        baseAmmoCost: 0,
                        baseShootHitCount: 0,
                        new[]
                        {
                            MachineGunnerProgramOperation.ApplyPrivateStatus(
                                MachineGunnerCombatantStatus.Invisible,
                                1,
                                MachineGunnerOperationTargetScope.Source),
                            new MachineGunnerProgramOperation(
                                MachineGunnerProgramOperationKind.DrawCards,
                                1),
                        }),
                });

        private static readonly IReadOnlyList<int> CreatedCardTemplateIds =
            CollectCreatedCardTemplateIds();

        /// <summary>本职业全部声明式程序在本场可能直接创建的静态卡牌模板，供会话提前准备表现资源。</summary>
        internal static IReadOnlyList<int> PotentiallyCreatedCardTemplateIds =>
            CreatedCardTemplateIds;

        /// <summary>从程序操作本身收集并冻结动态创建模板，不让会话或 UI 复制具体卡牌编号。</summary>
        private static IReadOnlyList<int> CollectCreatedCardTemplateIds()
        {
            var uniqueTemplateIds = new HashSet<int>();
            foreach (MachineGunnerCardProgram program in Programs.Values)
            {
                foreach (MachineGunnerProgramOperation operation in program.Operations)
                {
                    if (operation.Kind ==
                        MachineGunnerProgramOperationKind.ReplaceRemainingHandWithTemporaryCards)
                    {
                        uniqueTemplateIds.Add(operation.Value);
                    }
                }
            }

            var templateIds = new List<int>(uniqueTemplateIds);
            templateIds.Sort();
            return new ReadOnlyCollection<int>(templateIds);
        }

        /// <summary>创建在成功打出后进入职业持续状态、并可声明同一事务内即时效果的能力牌程序定义。</summary>
        private static MachineGunnerCardProgram CreatePower(
            cfg.battle.MachineGunnerProgramId id,
            MachineGunnerPowerKind powerKind,
            int powerStackGain = 1,
            IEnumerable<MachineGunnerProgramOperation> operations = null)
        {
            return new MachineGunnerCardProgram(
                id,
                MachineGunnerTargetInputMode.Self,
                baseAmmoCost: 0,
                baseShootHitCount: 0,
                operations ?? Array.Empty<MachineGunnerProgramOperation>(),
                powerKind: powerKind,
                powerStackGain: powerStackGain);
        }

        /// <summary>按生成的程序标识查找当前已实现的声明式程序。</summary>
        internal static bool TryGet(
            cfg.battle.MachineGunnerProgramId id,
            out MachineGunnerCardProgram program)
        {
            return Programs.TryGetValue(id, out program);
        }
    }

    /// <summary>机枪兵程序的一段已预演伤害，提交时会核对同一份参与者标量。</summary>
    internal sealed class MachineGunnerPreparedDamage
    {
        /// <summary>伤害的冻结目标。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>纯公式计算得到的格挡与生命结果。</summary>
        internal BattleDamageFormulaOutcome Outcome { get; }

        /// <summary>本段命中是否在穿透生命后需要消耗目标一层护甲。</summary>
        internal bool ConsumesArmor { get; }

        /// <summary>冻结一段目标伤害投影。</summary>
        internal MachineGunnerPreparedDamage(
            CombatantId targetId,
            BattleDamageFormulaOutcome outcome,
            bool consumesArmor)
        {
            TargetId = targetId;
            Outcome = outcome;
            ConsumesArmor = consumesArmor;
        }
    }

    /// <summary>机枪兵程序在预演期冻结的状态累加类型。</summary>
    internal enum MachineGunnerPreparedStatusChangeKind
    {
        PrivateStatus,
        Vulnerable,
    }

    /// <summary>一项已冻结目标、前后值与身份的状态写入，提交期不得重新解释程序定义。</summary>
    internal sealed class MachineGunnerPreparedStatusChange
    {
        /// <summary>本次状态写入的稳定类别。</summary>
        internal MachineGunnerPreparedStatusChangeKind Kind { get; }

        /// <summary>被写入状态的参与者。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>私有状态的身份；通用易伤操作保持为空。</summary>
        internal MachineGunnerCombatantStatus? PrivateStatus { get; }

        /// <summary>状态写入前的冻结层数。</summary>
        internal int ValueBefore { get; }

        /// <summary>状态写入后的冻结层数。</summary>
        internal int ValueAfter { get; }

        /// <summary>本次实际累加的非负层数。</summary>
        internal int Amount => ValueAfter - ValueBefore;

        /// <summary>创建一项已完成数值预演的状态写入。</summary>
        internal MachineGunnerPreparedStatusChange(
            MachineGunnerPreparedStatusChangeKind kind,
            CombatantId targetId,
            MachineGunnerCombatantStatus? privateStatus,
            int valueBefore,
            int valueAfter)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerPreparedStatusChangeKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (valueBefore < 0 || valueAfter <= valueBefore)
                throw new ArgumentOutOfRangeException(nameof(valueAfter));
            if (kind == MachineGunnerPreparedStatusChangeKind.PrivateStatus &&
                !privateStatus.HasValue)
            {
                throw new ArgumentNullException(nameof(privateStatus));
            }
            if (kind != MachineGunnerPreparedStatusChangeKind.PrivateStatus &&
                privateStatus.HasValue)
            {
                throw new ArgumentOutOfRangeException(nameof(privateStatus));
            }
            if (privateStatus.HasValue &&
                !Enum.IsDefined(typeof(MachineGunnerCombatantStatus), privateStatus.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(privateStatus));
            }

            Kind = kind;
            TargetId = targetId;
            PrivateStatus = privateStatus;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
        }
    }

    /// <summary>一名目标在预演期冻结的燃烧施加结果；燃烧与浸油必须作为同一原子规则提交，不能拆成普通状态累加。</summary>
    internal sealed class MachineGunnerPreparedBurnApplication
    {
        /// <summary>本次施加燃烧的存活目标。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>燃烧与既有浸油的冻结前后值。</summary>
        internal MachineGunnerBurnApplicationResult Result { get; }

        /// <summary>创建已按既有浸油规则预演完成的一名燃烧目标。</summary>
        internal MachineGunnerPreparedBurnApplication(
            CombatantId targetId,
            MachineGunnerBurnApplicationResult result)
        {
            TargetId = targetId;
            Result = result;
        }
    }

    /// <summary>不充分爆燃的一段冻结伤害；来源必须保留为触发它的燃烧敌人而非施放卡牌的玩家。</summary>
    internal sealed class MachineGunnerPreparedIncompleteCombustionDamage
    {
        /// <summary>在效果开始时捕获的燃烧敌人来源。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>本段针对当前仍存活目标的伤害投影。</summary>
        internal MachineGunnerPreparedDamage Damage { get; }

        /// <summary>冻结一段包含真实燃烧来源的爆燃 Debuff 伤害。</summary>
        internal MachineGunnerPreparedIncompleteCombustionDamage(
            CombatantId sourceId,
            MachineGunnerPreparedDamage damage)
        {
            SourceId = sourceId;
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
        }
    }

    /// <summary>不充分爆燃在全部伤害完成后，为一名幸存敌人冻结的烟雾增加和燃烧清零。</summary>
    internal sealed class MachineGunnerPreparedBurnSmokeConversion
    {
        /// <summary>被转换状态的仍存活敌人。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>燃烧转入的烟雾前后值。</summary>
        internal MachineGunnerStatusValueChange SmokeChange { get; }

        /// <summary>被清零的燃烧前后值。</summary>
        internal MachineGunnerStatusValueChange BurnChange { get; }

        /// <summary>冻结符合一比一转换契约的烟雾和燃烧变化。</summary>
        internal MachineGunnerPreparedBurnSmokeConversion(
            CombatantId targetId,
            MachineGunnerStatusValueChange smokeChange,
            MachineGunnerStatusValueChange burnChange)
        {
            if (smokeChange.Status != MachineGunnerCombatantStatus.Smoke ||
                smokeChange.After <= smokeChange.Before)
            {
                throw new ArgumentOutOfRangeException(nameof(smokeChange));
            }
            if (burnChange.Status != MachineGunnerCombatantStatus.Burn ||
                burnChange.Before <= 0 ||
                burnChange.After != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(burnChange));
            }
            if (smokeChange.Amount != burnChange.Before)
            {
                throw new ArgumentException("不充分爆燃的烟雾增加必须与清零前燃烧一比一对应。");
            }

            TargetId = targetId;
            SmokeChange = smokeChange;
            BurnChange = burnChange;
        }
    }

    /// <summary>焚风在首次写入前冻结的目标燃烧、既有浸油与来源烟雾清零结果。</summary>
    internal sealed class MachineGunnerPreparedSmokeToBurnConversion
    {
        /// <summary>提供并消耗烟雾的施放者。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>接受燃烧与既有浸油结算的显式敌方目标。</summary>
        internal CombatantId TargetId { get; }

        /// <summary>按来源烟雾值和目标既有浸油冻结的燃烧原子结果。</summary>
        internal MachineGunnerBurnApplicationResult BurnApplication { get; }

        /// <summary>来源烟雾从正数清零的冻结变化。</summary>
        internal MachineGunnerStatusValueChange SmokeChange { get; }

        /// <summary>创建一项完整且有实际烟雾可消耗的焚风预演结果。</summary>
        internal MachineGunnerPreparedSmokeToBurnConversion(
            CombatantId sourceId,
            CombatantId targetId,
            MachineGunnerBurnApplicationResult burnApplication,
            MachineGunnerStatusValueChange smokeChange)
        {
            if (sourceId == targetId)
                throw new ArgumentException("焚风来源与目标不能是同一参与者。", nameof(targetId));
            if (smokeChange.Status != MachineGunnerCombatantStatus.Smoke ||
                smokeChange.Before <= 0 ||
                smokeChange.After != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(smokeChange));
            }
            int frozenBaseBurn = checked(
                burnApplication.BurnChange.After -
                burnApplication.BurnChange.Before -
                burnApplication.OilChange.Before);
            if (frozenBaseBurn != smokeChange.Before ||
                burnApplication.OilChange.After != burnApplication.OilChange.Before / 2)
            {
                throw new ArgumentException(
                    "焚风燃烧与浸油预演必须精确来自被冻结的来源烟雾。",
                    nameof(burnApplication));
            }

            SourceId = sourceId;
            TargetId = targetId;
            BurnApplication = burnApplication;
            SmokeChange = smokeChange;
        }
    }

    /// <summary>二手烟冻结的来源烟雾快照与通用中毒施加计划。</summary>
    internal sealed class MachineGunnerPreparedPoisonApplication
    {
        /// <summary>提供烟雾数值但不消耗烟雾的施放者。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>命令起点冻结的施放者烟雾层数。</summary>
        internal int SourceSmokeBefore { get; }

        /// <summary>由通用中毒模块创建且尚未提交的计划。</summary>
        internal BattlePreparedPoisonApplication Plan { get; }

        /// <summary>绑定来源烟雾与等量通用中毒计划，并拒绝跨来源或数值不一致。</summary>
        internal MachineGunnerPreparedPoisonApplication(
            CombatantId sourceId,
            int sourceSmokeBefore,
            BattlePreparedPoisonApplication plan)
        {
            if (sourceSmokeBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceSmokeBefore));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (plan.SourceSnapshot.Id != sourceId || plan.Amount != sourceSmokeBefore)
            {
                throw new ArgumentException(
                    "二手烟的来源或施加量必须与冻结烟雾一致。",
                    nameof(plan));
            }

            SourceId = sourceId;
            SourceSmokeBefore = sourceSmokeBefore;
        }
    }

    /// <summary>机枪兵程序的一项冻结操作，避免提交阶段再次解释卡牌定义。</summary>
    internal sealed class MachineGunnerPreparedOperation
    {
        /// <summary>操作类别。</summary>
        internal MachineGunnerProgramOperationKind Kind { get; }

        /// <summary>操作的冻结数值；动态来源与按目标状态种类抽牌操作允许为零。</summary>
        internal int Value { get; }

        /// <summary>伤害操作按实际命中的稳定顺序保存全部段。</summary>
        internal IReadOnlyList<MachineGunnerPreparedDamage> Damages { get; }

        /// <summary>状态操作按实际提交顺序冻结的全部目标状态写入。</summary>
        internal IReadOnlyList<MachineGunnerPreparedStatusChange> StatusChanges { get; }

        /// <summary>燃烧操作按实际提交顺序冻结的目标及其原子结果；其他操作为空。</summary>
        internal IReadOnlyList<MachineGunnerPreparedBurnApplication> BurnApplications { get; }

        /// <summary>不充分爆燃按来源与目标动态存活顺序冻结的 Debuff 伤害；其他操作为空。</summary>
        internal IReadOnlyList<MachineGunnerPreparedIncompleteCombustionDamage>
            IncompleteCombustionDamages { get; }

        /// <summary>不充分爆燃在全部伤害后按 Encounter 顺序冻结的燃烧转烟雾记录；其他操作为空。</summary>
        internal IReadOnlyList<MachineGunnerPreparedBurnSmokeConversion>
            BurnSmokeConversions { get; }

        /// <summary>焚风专用的跨来源与目标状态转换；其他操作保持为空。</summary>
        internal MachineGunnerPreparedSmokeToBurnConversion SmokeToBurnConversion { get; }

        /// <summary>二手烟专用的来源烟雾与通用中毒计划；其他操作保持为空。</summary>
        internal MachineGunnerPreparedPoisonApplication PoisonApplication { get; }

        /// <summary>格挡操作的冻结前值；其他操作为零。</summary>
        internal int BlockBefore { get; }

        /// <summary>格挡操作的冻结后值；其他操作为零。</summary>
        internal int BlockAfter { get; }

        /// <summary>创建一项已完成全部纯计算的程序操作。</summary>
        internal MachineGunnerPreparedOperation(
            MachineGunnerProgramOperationKind kind,
            int value,
            IEnumerable<MachineGunnerPreparedDamage> damages,
            int blockBefore,
            int blockAfter,
            IEnumerable<MachineGunnerPreparedStatusChange> statusChanges = null,
            IEnumerable<MachineGunnerPreparedBurnApplication> burnApplications = null,
            IEnumerable<MachineGunnerPreparedIncompleteCombustionDamage>
                incompleteCombustionDamages = null,
            IEnumerable<MachineGunnerPreparedBurnSmokeConversion> burnSmokeConversions = null,
            MachineGunnerPreparedSmokeToBurnConversion smokeToBurnConversion = null,
            MachineGunnerPreparedPoisonApplication poisonApplication = null)
        {
            bool allowsZeroValue = kind ==
                    MachineGunnerProgramOperationKind.DrawCardsByActiveStatusKinds ||
                kind == MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke;
            if (value < 0 || (value == 0 && !allowsZeroValue))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (damages == null)
                throw new ArgumentNullException(nameof(damages));
            if (blockBefore < 0 || blockAfter < blockBefore)
                throw new ArgumentOutOfRangeException(nameof(blockAfter));

            Kind = kind;
            Value = value;
            Damages = new ReadOnlyCollection<MachineGunnerPreparedDamage>(
                new List<MachineGunnerPreparedDamage>(damages));
            StatusChanges = new ReadOnlyCollection<MachineGunnerPreparedStatusChange>(
                statusChanges == null
                    ? new List<MachineGunnerPreparedStatusChange>()
                    : new List<MachineGunnerPreparedStatusChange>(statusChanges));
            BurnApplications = new ReadOnlyCollection<MachineGunnerPreparedBurnApplication>(
                burnApplications == null
                    ? new List<MachineGunnerPreparedBurnApplication>()
                    : new List<MachineGunnerPreparedBurnApplication>(burnApplications));
            IncompleteCombustionDamages =
                new ReadOnlyCollection<MachineGunnerPreparedIncompleteCombustionDamage>(
                    incompleteCombustionDamages == null
                        ? new List<MachineGunnerPreparedIncompleteCombustionDamage>()
                        : new List<MachineGunnerPreparedIncompleteCombustionDamage>(
                            incompleteCombustionDamages));
            BurnSmokeConversions = new ReadOnlyCollection<MachineGunnerPreparedBurnSmokeConversion>(
                burnSmokeConversions == null
                    ? new List<MachineGunnerPreparedBurnSmokeConversion>()
                    : new List<MachineGunnerPreparedBurnSmokeConversion>(burnSmokeConversions));
            SmokeToBurnConversion = smokeToBurnConversion;
            PoisonApplication = poisonApplication;
            if (kind == MachineGunnerProgramOperationKind.ApplyBurn)
            {
                if (StatusChanges.Count > 0)
                {
                    throw new ArgumentException(
                        "燃烧预演不能混入普通状态写入。",
                        nameof(statusChanges));
                }
            }
            else if (BurnApplications.Count > 0)
            {
                throw new ArgumentException(
                    "非燃烧操作不能携带燃烧预演结果。",
                    nameof(burnApplications));
            }
            if (kind == MachineGunnerProgramOperationKind.ResolveIncompleteCombustion)
            {
                if (Damages.Count > 0 || StatusChanges.Count > 0 || BurnApplications.Count > 0)
                {
                    throw new ArgumentException(
                        "不充分爆燃必须使用专用伤害和状态转换预演记录。",
                        nameof(damages));
                }
            }
            else if (IncompleteCombustionDamages.Count > 0 || BurnSmokeConversions.Count > 0)
            {
                throw new ArgumentException(
                    "只有不充分爆燃可以携带专用爆燃预演记录。",
                    nameof(incompleteCombustionDamages));
            }
            if (kind == MachineGunnerProgramOperationKind.ConvertSourceSmokeToTargetBurn)
            {
                if (SmokeToBurnConversion == null ||
                    SmokeToBurnConversion.SmokeChange.Before != value ||
                    Damages.Count > 0 ||
                    StatusChanges.Count > 0 ||
                    BurnApplications.Count > 0 ||
                    IncompleteCombustionDamages.Count > 0 ||
                    BurnSmokeConversions.Count > 0)
                {
                    throw new ArgumentException(
                        "焚风必须且只能携带一项来源烟雾转目标燃烧的专用预演结果。",
                        nameof(smokeToBurnConversion));
                }
            }
            else if (SmokeToBurnConversion != null)
            {
                throw new ArgumentException(
                    "非焚风操作不能携带来源烟雾转目标燃烧的预演结果。",
                    nameof(smokeToBurnConversion));
            }
            if (kind == MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke)
            {
                if (PoisonApplication == null ||
                    PoisonApplication.SourceSmokeBefore != value ||
                    Damages.Count > 0 ||
                    StatusChanges.Count > 0 ||
                    BurnApplications.Count > 0 ||
                    IncompleteCombustionDamages.Count > 0 ||
                    BurnSmokeConversions.Count > 0 ||
                    SmokeToBurnConversion != null)
                {
                    throw new ArgumentException(
                        "二手烟必须且只能携带一项来源烟雾派生的通用中毒计划。",
                        nameof(poisonApplication));
                }
            }
            else if (PoisonApplication != null)
            {
                throw new ArgumentException(
                    "非二手烟操作不能携带通用中毒计划。",
                    nameof(poisonApplication));
            }
            BlockBefore = blockBefore;
            BlockAfter = blockAfter;
        }
    }

    /// <summary>一次“射击、补满、再射击”程序的纯资源轨迹；伤害预演只消费这些冻结段数，不重新解释弹药规则。</summary>
    internal sealed class MachineGunnerPreparedReloadedVolley
    {
        /// <summary>第一波应展开的来源射击段数；免攻时保留声明上限。</summary>
        internal int FirstWaveEffectShotCount { get; }

        /// <summary>第一波真正从回合资源扣除的弹药。</summary>
        internal int FirstWaveActualAmmoSpent { get; }

        /// <summary>波间补满前的弹药快照。</summary>
        internal int ReloadAmmoBefore { get; }

        /// <summary>波间补满后的弹药快照，固定等于命令开始时的弹药上限。</summary>
        internal int ReloadAmmoAfter { get; }

        /// <summary>第二波连同全卡唯一兴奋剂附加段应展开的来源射击段数。</summary>
        internal int SecondWaveEffectShotCount { get; }

        /// <summary>第二波连同可支付兴奋剂附加段真正扣除的弹药。</summary>
        internal int SecondWaveActualAmmoSpent { get; }

        /// <summary>兴奋剂为整张卡冻结的额外来源射击段，最多一段。</summary>
        internal int StimBonusShotCount { get; }

        /// <summary>供游击战术读取的整张卡名义弹耗；免攻时使用两波声明上限并计入兴奋剂。</summary>
        internal int NominalAmmoSpentForTriggers { get; }

        /// <summary>全部实际弹药变化提交后的最终弹药。</summary>
        internal int FinalAmmo { get; }

        /// <summary>冻结一份已经由纯解析器校验的两波资源轨迹。</summary>
        internal MachineGunnerPreparedReloadedVolley(
            int firstWaveEffectShotCount,
            int firstWaveActualAmmoSpent,
            int reloadAmmoBefore,
            int reloadAmmoAfter,
            int secondWaveEffectShotCount,
            int secondWaveActualAmmoSpent,
            int stimBonusShotCount,
            int nominalAmmoSpentForTriggers,
            int finalAmmo)
        {
            if (firstWaveEffectShotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(firstWaveEffectShotCount));
            if (firstWaveActualAmmoSpent < 0)
                throw new ArgumentOutOfRangeException(nameof(firstWaveActualAmmoSpent));
            if (reloadAmmoBefore < 0 || reloadAmmoAfter < reloadAmmoBefore)
                throw new ArgumentOutOfRangeException(nameof(reloadAmmoAfter));
            if (secondWaveEffectShotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(secondWaveEffectShotCount));
            if (secondWaveActualAmmoSpent < 0 || secondWaveActualAmmoSpent > reloadAmmoAfter)
                throw new ArgumentOutOfRangeException(nameof(secondWaveActualAmmoSpent));
            if (stimBonusShotCount < 0 || stimBonusShotCount > 1)
                throw new ArgumentOutOfRangeException(nameof(stimBonusShotCount));
            if (nominalAmmoSpentForTriggers < 0)
                throw new ArgumentOutOfRangeException(nameof(nominalAmmoSpentForTriggers));
            if (finalAmmo < 0 || finalAmmo > reloadAmmoAfter)
                throw new ArgumentOutOfRangeException(nameof(finalAmmo));

            FirstWaveEffectShotCount = firstWaveEffectShotCount;
            FirstWaveActualAmmoSpent = firstWaveActualAmmoSpent;
            ReloadAmmoBefore = reloadAmmoBefore;
            ReloadAmmoAfter = reloadAmmoAfter;
            SecondWaveEffectShotCount = secondWaveEffectShotCount;
            SecondWaveActualAmmoSpent = secondWaveActualAmmoSpent;
            StimBonusShotCount = stimBonusShotCount;
            NominalAmmoSpentForTriggers = nominalAmmoSpentForTriggers;
            FinalAmmo = finalAmmo;
        }
    }

    /// <summary>纯解析两波射击、波间补满、兴奋剂和免攻的资源轨迹，不读取或写入战斗对象。</summary>
    internal static class MachineGunnerReloadedVolleyResolver
    {
        /// <summary>按命令开始时资源和支付模式冻结两波的效果段数、实际支付与名义触发费用。</summary>
        internal static MachineGunnerPreparedReloadedVolley Prepare(
            int initialAmmo,
            int ammoMaximum,
            int waveShotLimit,
            bool stimActive,
            BattleCardPaymentMode paymentMode)
        {
            if (initialAmmo < 0)
                throw new ArgumentOutOfRangeException(nameof(initialAmmo));
            if (ammoMaximum < 0 || initialAmmo > ammoMaximum)
                throw new ArgumentOutOfRangeException(nameof(ammoMaximum));
            if (waveShotLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(waveShotLimit));
            if (!Enum.IsDefined(typeof(BattleCardPaymentMode), paymentMode))
                throw new ArgumentOutOfRangeException(nameof(paymentMode));

            bool isWaived = paymentMode == BattleCardPaymentMode.Waived;
            int firstWaveActualAmmoSpent = isWaived
                ? 0
                : Math.Min(initialAmmo, waveShotLimit);
            int firstWaveEffectShotCount = isWaived
                ? waveShotLimit
                : firstWaveActualAmmoSpent;
            int reloadAmmoBefore = initialAmmo - firstWaveActualAmmoSpent;
            int reloadAmmoAfter = ammoMaximum;

            int secondWaveBaseAmmoSpent = Math.Min(ammoMaximum, waveShotLimit);
            int ammoAfterSecondBase = ammoMaximum - secondWaveBaseAmmoSpent;
            int stimBonusShotCount = stimActive && (isWaived || ammoAfterSecondBase > 0)
                ? 1
                : 0;
            int secondWaveActualAmmoSpent = isWaived
                ? 0
                : checked(secondWaveBaseAmmoSpent + stimBonusShotCount);
            int secondWaveEffectShotCount = isWaived
                ? checked(waveShotLimit + stimBonusShotCount)
                : secondWaveActualAmmoSpent;
            int nominalAmmoSpentForTriggers = isWaived
                ? checked(checked(waveShotLimit * 2) + stimBonusShotCount)
                : checked(firstWaveActualAmmoSpent + secondWaveActualAmmoSpent);
            int finalAmmo = isWaived
                ? ammoMaximum
                : ammoMaximum - secondWaveActualAmmoSpent;

            return new MachineGunnerPreparedReloadedVolley(
                firstWaveEffectShotCount,
                firstWaveActualAmmoSpent,
                reloadAmmoBefore,
                reloadAmmoAfter,
                secondWaveEffectShotCount,
                secondWaveActualAmmoSpent,
                stimBonusShotCount,
                nominalAmmoSpentForTriggers,
                finalAmmo);
        }
    }

    /// <summary>一张机枪兵卡在队首快照中冻结的实际支付、效果取值和名义触发费用，后续程序段只读取对应语义。</summary>
    internal readonly struct MachineGunnerCostResolution
    {
        /// <summary>由共享费用解析器冻结的能量三轴结果。</summary>
        internal BattleCardEnergyCostResolution EnergyCost { get; }

        /// <summary>本次成功命令实际支付的能量。</summary>
        internal int EnergySpent => EnergyCost.ActualEnergySpent;

        /// <summary>X 费卡在命令开始时冻结的 X；固定费用卡固定为零。</summary>
        internal int XValue => EnergyCost.EffectValue;

        /// <summary>本次成功命令实际支付的弹药。</summary>
        internal int ActualAmmoSpent { get; }

        /// <summary>供弹药驱动伤害段和状态值读取的冻结效果弹药；免费攻击仍保留该值。</summary>
        internal int AmmoEffectValue { get; }

        /// <summary>供游击战术等“视为原本消耗”触发读取的冻结名义弹药。</summary>
        internal int NominalAmmoSpentForTriggers { get; }

        /// <summary>兴奋剂为本张可受益射击卡增加的额外命中数。</summary>
        internal int StimBonusHitCount { get; }

        /// <summary>需要波间补满的专用射击程序资源轨迹；普通程序保持为空。</summary>
        internal MachineGunnerPreparedReloadedVolley ReloadedVolley { get; }

        /// <summary>冻结已经完成合法性检查的职业资源支付，不允许出现负数或多个兴奋剂额外段。</summary>
        internal MachineGunnerCostResolution(
            BattleCardEnergyCostResolution energyCost,
            int actualAmmoSpent,
            int ammoEffectValue,
            int nominalAmmoSpentForTriggers,
            int stimBonusHitCount,
            MachineGunnerPreparedReloadedVolley reloadedVolley = null)
        {
            if (actualAmmoSpent < 0)
                throw new ArgumentOutOfRangeException(nameof(actualAmmoSpent));
            if (ammoEffectValue < 0)
                throw new ArgumentOutOfRangeException(nameof(ammoEffectValue));
            if (nominalAmmoSpentForTriggers < 0)
                throw new ArgumentOutOfRangeException(nameof(nominalAmmoSpentForTriggers));
            if (stimBonusHitCount < 0)
                throw new ArgumentOutOfRangeException(nameof(stimBonusHitCount));
            if (reloadedVolley != null &&
                (actualAmmoSpent != 0 ||
                 nominalAmmoSpentForTriggers != reloadedVolley.NominalAmmoSpentForTriggers ||
                 stimBonusHitCount != reloadedVolley.StimBonusShotCount))
            {
                throw new ArgumentException(
                    "换弹连射必须把实际弹药支付保留给有序操作，并复用同一份名义费用与兴奋剂快照。",
                    nameof(reloadedVolley));
            }

            EnergyCost = energyCost;
            ActualAmmoSpent = actualAmmoSpent;
            AmmoEffectValue = ammoEffectValue;
            NominalAmmoSpentForTriggers = nominalAmmoSpentForTriggers;
            StimBonusHitCount = stimBonusHitCount;
            ReloadedVolley = reloadedVolley;
        }
    }

    /// <summary>冻结一张机枪兵卡的费用与免攻生命周期快照，提交前可验证且只允许成功归宿后提交一次。</summary>
    internal sealed class MachineGunnerPreparedCost
    {
        /// <summary>创建本计划的唯一职业运行时。</summary>
        internal MachineGunnerBattleRuntime Owner { get; }

        /// <summary>费用解析时读取的不可变玩家回合事实。</summary>
        internal PlayerTurnData InitialPlayerTurn { get; }

        /// <summary>费用解析绑定的职业程序实例。</summary>
        internal MachineGunnerCardProgram Program { get; }

        /// <summary>费用解析时最近成功卡的分类快照。</summary>
        internal MachineGunnerRecentSuccessfulCardCategory RecentSuccessfulCardCategoryBefore { get; }

        /// <summary>费用解析时下一张攻击免费是否已激活。</summary>
        internal bool NextAttackFreeBefore { get; }

        /// <summary>费用解析时免攻生命周期的修订号。</summary>
        internal ulong CostModifierRevisionBefore { get; }

        /// <summary>后续效果和资源提交唯一读取的冻结费用。</summary>
        internal MachineGunnerCostResolution Resolution { get; }

        /// <summary>本张成功攻击是否应在归宿后消费命令开始时的免攻。</summary>
        internal bool ConsumesNextAttackFreeOnSuccess { get; }

        /// <summary>计划是否已经随成功归宿提交过生命周期。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结费用、状态版本与成功生命周期意图，不写入运行时事实。</summary>
        internal MachineGunnerPreparedCost(
            MachineGunnerBattleRuntime owner,
            PlayerTurnData initialPlayerTurn,
            MachineGunnerCardProgram program,
            MachineGunnerRecentSuccessfulCardCategory recentSuccessfulCardCategoryBefore,
            bool nextAttackFreeBefore,
            ulong costModifierRevisionBefore,
            MachineGunnerCostResolution resolution,
            bool consumesNextAttackFreeOnSuccess)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialPlayerTurn = initialPlayerTurn ??
                throw new ArgumentNullException(nameof(initialPlayerTurn));
            Program = program ?? throw new ArgumentNullException(nameof(program));
            RecentSuccessfulCardCategoryBefore = recentSuccessfulCardCategoryBefore;
            NextAttackFreeBefore = nextAttackFreeBefore;
            CostModifierRevisionBefore = costModifierRevisionBefore;
            Resolution = resolution;
            ConsumesNextAttackFreeOnSuccess = consumesNextAttackFreeOnSuccess;
        }

        /// <summary>把本计划标记为已随成功归宿提交，阻止同一费用生命周期重复消费。</summary>
        internal void MarkCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("机枪兵费用生命周期计划不能重复提交。");

            IsCommitted = true;
        }
    }

    /// <summary>机枪兵玩家回合开始时冻结的延迟状态清除、格挡转化、资源修正和补弹请求。</summary>
    internal sealed class MachineGunnerPlayerRoundStartResult
    {
        /// <summary>护甲与下回合格挡在本次回合开始合并提供的格挡值。</summary>
        internal int BlockGain { get; }

        /// <summary>是否应在通用资源档案补充后把当前弹药补至上限。</summary>
        internal bool RefillAmmoAfterNormalReplenish { get; }

        /// <summary>下回合格挡转化后清零的私有状态变更；没有待结算层时为空。</summary>
        internal MachineGunnerStatusValueChange? NextRoundBlockClear { get; }

        /// <summary>下回合补弹执行后清零的私有状态变更；没有待结算层时为空。</summary>
        internal MachineGunnerStatusValueChange? ReloadAmmoClear { get; }

        /// <summary>下一回合基础能量补给使用的有符号净修正，正数增加、负数减少。</summary>
        internal int EnergyGainAdjustment { get; }

        /// <summary>能量补给加成结算后清零的私有状态变更；没有待结算层数时为空。</summary>
        internal MachineGunnerStatusValueChange? NextRoundEnergyGainBonusClear { get; }

        /// <summary>能量补给惩罚结算后清零的私有状态变更；没有待结算层数时为空。</summary>
        internal MachineGunnerStatusValueChange? NextRoundEnergyGainPenaltyClear { get; }

        /// <summary>创建已经完成状态预演的玩家回合开始结果，并拒绝不合法的延迟状态快照。</summary>
        internal MachineGunnerPlayerRoundStartResult(
            int blockGain,
            MachineGunnerStatusValueChange? nextRoundBlockClear,
            MachineGunnerStatusValueChange? reloadAmmoClear,
            MachineGunnerStatusValueChange? nextRoundEnergyGainBonusClear,
            MachineGunnerStatusValueChange? nextRoundEnergyGainPenaltyClear)
        {
            if (blockGain < 0)
                throw new ArgumentOutOfRangeException(nameof(blockGain));
            if (nextRoundBlockClear.HasValue &&
                (nextRoundBlockClear.Value.Status != MachineGunnerCombatantStatus.NextRoundBlock ||
                 nextRoundBlockClear.Value.After != 0))
            {
                throw new ArgumentOutOfRangeException(nameof(nextRoundBlockClear));
            }
            if (reloadAmmoClear.HasValue &&
                (reloadAmmoClear.Value.Status != MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound ||
                 reloadAmmoClear.Value.After != 0))
            {
                throw new ArgumentOutOfRangeException(nameof(reloadAmmoClear));
            }
            if (nextRoundEnergyGainBonusClear.HasValue &&
                (nextRoundEnergyGainBonusClear.Value.Status !=
                     MachineGunnerCombatantStatus.NextRoundEnergyGainBonus ||
                 nextRoundEnergyGainBonusClear.Value.After != 0))
            {
                throw new ArgumentOutOfRangeException(nameof(nextRoundEnergyGainBonusClear));
            }
            if (nextRoundEnergyGainPenaltyClear.HasValue &&
                (nextRoundEnergyGainPenaltyClear.Value.Status !=
                     MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty ||
                 nextRoundEnergyGainPenaltyClear.Value.After != 0))
            {
                throw new ArgumentOutOfRangeException(nameof(nextRoundEnergyGainPenaltyClear));
            }

            BlockGain = blockGain;
            RefillAmmoAfterNormalReplenish = reloadAmmoClear.HasValue;
            NextRoundBlockClear = nextRoundBlockClear;
            ReloadAmmoClear = reloadAmmoClear;
            NextRoundEnergyGainBonusClear = nextRoundEnergyGainBonusClear;
            NextRoundEnergyGainPenaltyClear = nextRoundEnergyGainPenaltyClear;
            int bonus = nextRoundEnergyGainBonusClear?.Before ?? 0;
            int penalty = nextRoundEnergyGainPenaltyClear?.Before ?? 0;
            EnergyGainAdjustment = bonus - penalty;
        }
    }

    /// <summary>机枪兵卡牌程序在一次队首命令内交给回合模块的不可变结果。</summary>
    internal sealed class MachineGunnerCardProgramExecutionResult
    {
        /// <summary>执行失败原因；成功时为 None。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>完成资源支付后的玩家回合事实；失败时为空。</summary>
        internal PlayerTurnData PlayerTurnAfter { get; }

        /// <summary>按命令顺序冻结的全部结算记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>程序完整提交后是否要求 Queue 续接结束本次玩家行动。</summary>
        internal bool RequestsPlayerActionEnd { get; }

        /// <summary>成功后交给 Queue 在父表现屏障结束后执行的冻结免费出牌请求。</summary>
        internal BattleTriggeredCardPlayRequest TriggeredCardPlayRequest { get; }

        /// <summary>当前程序是否已经成功提交。</summary>
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>冻结一次程序提交结果，失败时强制不携带资源或结算。</summary>
        private MachineGunnerCardProgramExecutionResult(
            BattleCommandExecutionFailureReason failureReason,
            PlayerTurnData playerTurnAfter,
            IEnumerable<BattleSettlementRecord> settlements,
            bool requestsPlayerActionEnd,
            BattleTriggeredCardPlayRequest triggeredCardPlayRequest)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (failureReason != BattleCommandExecutionFailureReason.None &&
                (playerTurnAfter != null || new List<BattleSettlementRecord>(settlements).Count > 0 ||
                 requestsPlayerActionEnd || triggeredCardPlayRequest != null))
            {
                throw new ArgumentException("失败的机枪兵程序不能携带写入结果。", nameof(settlements));
            }

            FailureReason = failureReason;
            PlayerTurnAfter = playerTurnAfter;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
            RequestsPlayerActionEnd = requestsPlayerActionEnd;
            TriggeredCardPlayRequest = triggeredCardPlayRequest;
        }

        /// <summary>创建零写入的稳定失败结果。</summary>
        internal static MachineGunnerCardProgramExecutionResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            if (failureReason == BattleCommandExecutionFailureReason.None)
                throw new ArgumentOutOfRangeException(nameof(failureReason));

            return new MachineGunnerCardProgramExecutionResult(
                failureReason,
                playerTurnAfter: null,
                Array.Empty<BattleSettlementRecord>(),
                requestsPlayerActionEnd: false,
                triggeredCardPlayRequest: null);
        }

        /// <summary>创建已经完成一次提交的成功结果。</summary>
        internal static MachineGunnerCardProgramExecutionResult SucceededWith(
            PlayerTurnData playerTurnAfter,
            IEnumerable<BattleSettlementRecord> settlements,
            bool requestsPlayerActionEnd = false,
            BattleTriggeredCardPlayRequest triggeredCardPlayRequest = null)
        {
            if (playerTurnAfter == null)
                throw new ArgumentNullException(nameof(playerTurnAfter));

            return new MachineGunnerCardProgramExecutionResult(
                BattleCommandExecutionFailureReason.None,
                playerTurnAfter,
                settlements,
                requestsPlayerActionEnd,
                triggeredCardPlayRequest);
        }
    }

    /// <summary>冻结一名参与者行动结束时的再生治疗与职业私有状态变化，供 Queue 首次写入前统一校验。</summary>
    internal sealed class MachineGunnerActorActionEndPlan
    {
        /// <summary>预构建时冻结的参与者实例，用于首写前拒绝身份漂移。</summary>
        internal CombatantData Actor { get; }

        /// <summary>本计划所属的参与者。</summary>
        internal CombatantId ActorId { get; }

        /// <summary>行动结束前冻结的生命值。</summary>
        internal int HealthBefore { get; }

        /// <summary>行动结束前冻结的再生层数。</summary>
        internal int RegenerationBefore { get; }

        /// <summary>按冻结生命和再生层数预演的治疗结果；没有再生时为空。</summary>
        internal BattleHealthRestorationOutcome? PreparedRegenerationHealing { get; }

        /// <summary>行动结束前冻结的失去力量层数。</summary>
        internal int LoseStrengthBefore { get; }

        /// <summary>行动结束前冻结的束缚层数。</summary>
        internal int ShackleBefore { get; }

        /// <summary>本计划提交时将追加的 settlement 数。</summary>
        internal int SettlementCount =>
            (PreparedRegenerationHealing.HasValue ? 2 : 0) +
            (LoseStrengthBefore > 0 ? 1 : 0) +
            (ShackleBefore > 0 ? 1 : 0);

        /// <summary>指示本计划是否已经尝试过首次写入前校验。</summary>
        internal bool ValidationAttempted { get; private set; }

        /// <summary>指示本计划是否已经通过首次写入前校验。</summary>
        internal bool IsValidated { get; private set; }

        /// <summary>指示本计划是否已经被唯一提交消费。</summary>
        internal bool IsConsumed { get; private set; }

        /// <summary>冻结参与者、生命、再生治疗与当前非负临时状态层数。</summary>
        internal MachineGunnerActorActionEndPlan(
            CombatantData actor,
            int healthBefore,
            int regenerationBefore,
            BattleHealthRestorationOutcome? preparedRegenerationHealing,
            int loseStrengthBefore,
            int shackleBefore)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            if (healthBefore < 0 || healthBefore > actor.MaxHealth)
                throw new ArgumentOutOfRangeException(nameof(healthBefore));
            if (regenerationBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(regenerationBefore));
            if (loseStrengthBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(loseStrengthBefore));
            if (shackleBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(shackleBefore));
            if (preparedRegenerationHealing.HasValue != (regenerationBefore > 0))
                throw new ArgumentOutOfRangeException(nameof(preparedRegenerationHealing));
            if (preparedRegenerationHealing.HasValue)
            {
                BattleHealthRestorationOutcome outcome = preparedRegenerationHealing.Value;
                if (outcome.RequestedAmount != regenerationBefore ||
                    outcome.HealthBefore != healthBefore ||
                    outcome.HealthAfter > actor.MaxHealth)
                {
                    throw new ArgumentOutOfRangeException(nameof(preparedRegenerationHealing));
                }
            }

            Actor = actor;
            ActorId = actor.Id;
            HealthBefore = healthBefore;
            RegenerationBefore = regenerationBefore;
            PreparedRegenerationHealing = preparedRegenerationHealing;
            LoseStrengthBefore = loseStrengthBefore;
            ShackleBefore = shackleBefore;
        }

        /// <summary>冻结唯一一次校验结果；失败计划同样禁止再次校验。</summary>
        internal void MarkValidated(bool succeeded)
        {
            if (ValidationAttempted)
                throw new InvalidOperationException("行动结束职业状态计划已经执行过校验。");

            ValidationAttempted = true;
            IsValidated = succeeded;
        }

        /// <summary>消费已经校验的计划一次，提交期间不再读取可能被前序组件修改的事实。</summary>
        internal void MarkConsumed()
        {
            if (!ValidationAttempted || !IsValidated)
                throw new InvalidOperationException("行动结束职业状态计划尚未通过首次写入前校验。");
            if (IsConsumed)
                throw new InvalidOperationException("行动结束职业状态计划已经提交。");

            IsConsumed = true;
        }
    }

    /// <summary>一个调度实例在稳定生命周期中的触发、操作与进度写入计划。</summary>
    internal sealed class MachineGunnerPreparedScheduledEffect
    {
        /// <summary>计划开始时的不可变实例快照。</summary>
        internal MachineGunnerScheduledEffectInstance Instance { get; }

        /// <summary>按真实结算顺序冻结的伤害与状态操作。</summary>
        internal IReadOnlyList<MachineGunnerPreparedOperation> Operations { get; }

        /// <summary>本次生命周期是否实际触发该实例效果。</summary>
        internal bool Triggered { get; }

        /// <summary>生命周期结束时的实例进度；移除时为空。</summary>
        internal MachineGunnerScheduledEffectInstance After { get; }

        /// <summary>冻结一个实例在本次生命周期中的完整计划。</summary>
        internal MachineGunnerPreparedScheduledEffect(
            MachineGunnerScheduledEffectInstance instance,
            IEnumerable<MachineGunnerPreparedOperation> operations,
            bool triggered,
            MachineGunnerScheduledEffectInstance after)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (after != null && !instance.Id.Equals(after.Id))
                throw new ArgumentOutOfRangeException(nameof(after));

            Operations = new ReadOnlyCollection<MachineGunnerPreparedOperation>(
                new List<MachineGunnerPreparedOperation>(operations));
            Triggered = triggered;
            After = after;
        }
    }

    /// <summary>一次回合开始或回合结束的全部延迟实例联合预演计划。</summary>
    internal sealed class MachineGunnerScheduledEffectLifecyclePlan
    {
        /// <summary>本计划对应的稳定生命周期时点。</summary>
        internal MachineGunnerScheduledEffectTiming Timing { get; }

        /// <summary>按实例插入顺序冻结的全部触发或倒计时。</summary>
        internal IReadOnlyList<MachineGunnerPreparedScheduledEffect> Effects { get; }

        /// <summary>调度容器需要原子提交的联合实例写入。</summary>
        internal MachineGunnerScheduledEffectMutationPlan MutationPlan { get; }

        /// <summary>预演前的职业随机流状态。</summary>
        internal uint RandomStateBefore { get; }

        /// <summary>全部随机目标冻结后的候选随机流状态。</summary>
        internal uint RandomStateAfter { get; }

        /// <summary>按生命周期操作的最终 settlement 数量。</summary>
        internal int SettlementCount { get; }

        /// <summary>复制并冻结一次完整生命周期计划。</summary>
        internal MachineGunnerScheduledEffectLifecyclePlan(
            MachineGunnerScheduledEffectTiming timing,
            IEnumerable<MachineGunnerPreparedScheduledEffect> effects,
            MachineGunnerScheduledEffectMutationPlan mutationPlan,
            uint randomStateBefore,
            uint randomStateAfter,
            int settlementCount)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectTiming), timing))
                throw new ArgumentOutOfRangeException(nameof(timing));
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));
            if (settlementCount < 0)
                throw new ArgumentOutOfRangeException(nameof(settlementCount));

            Timing = timing;
            Effects = new ReadOnlyCollection<MachineGunnerPreparedScheduledEffect>(
                new List<MachineGunnerPreparedScheduledEffect>(effects));
            MutationPlan = mutationPlan ?? throw new ArgumentNullException(nameof(mutationPlan));
            RandomStateBefore = randomStateBefore;
            RandomStateAfter = randomStateAfter;
            SettlementCount = settlementCount;
        }
    }

    /// <summary>机枪兵私有状态被程序累加后的权威记录。</summary>
    internal sealed class MachineGunnerPrivateStatusChangedSettlement : BattleSettlementRecord
    {
        /// <summary>被程序累加的职业私有状态。</summary>
        internal MachineGunnerCombatantStatus Status { get; }

        /// <summary>状态写入前的冻结层数。</summary>
        internal int ValueBefore { get; }

        /// <summary>状态写入后的冻结层数。</summary>
        internal int ValueAfter { get; }

        /// <summary>本次实际累加的层数。</summary>
        internal int Amount => ValueAfter - ValueBefore;

        /// <summary>创建一条不伪装为通用 Effect 的职业私有状态结算记录。</summary>
        internal MachineGunnerPrivateStatusChangedSettlement(
            int order,
            CombatantId sourceId,
            CombatantId targetId,
            MachineGunnerCombatantStatus status,
            int valueBefore,
            int valueAfter)
            : base(
                order,
                BattleSettlementRecordType.StatusApplied,
                null,
                sourceId,
                targetId)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerCombatantStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (valueBefore < 0 || valueAfter < 0 || valueAfter == valueBefore)
                throw new ArgumentOutOfRangeException(nameof(valueAfter));

            Status = status;
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
        }
    }

    /// <summary>仅为机枪兵玩家持有卡牌程序状态与确定性目标规则的单场深模块。</summary>
    internal sealed class MachineGunnerBattleRuntime : IDisposable, IBattleDamageFormulaOverride
    {
        private const int HandLimit = 10;
        private const int KungfuMechBlockPerStack = 4;
        private const int IncendiaryAmmoBurnPerStack = 1;
        private const int AgedOilPerNonShootHit = 2;
        private const int BurningOilBurnGrowth = 1;
        private const int ReloadedVolleyWaveShotLimit = 6;

        private readonly BattleCombatantsData _combatants;
        private readonly BattleCombatantEffectOperations _stateOperations;
        private readonly BattlePoisonApplication _poisonApplication;
        private readonly CombatantId _playerId;
        private readonly IReadOnlyList<CombatantId> _enemyCombatantIdsInEncounterOrder;
        private readonly MachineGunnerTargetSelector _targetSelector;
        private readonly GameRandom _cardRandom;
        private readonly MachineGunnerCombatState _combatState;
        private readonly MachineGunnerScheduledEffectScheduler _scheduledEffects;
        private readonly Dictionary<MachineGunnerPowerKind, int> _powerStacks;
        private int _stimTurns;
        private MachineGunnerRecentSuccessfulCardCategory _recentSuccessfulCardCategory;
        private bool _nextAttackFree;
        private ulong _costModifierRevision;
        private IReadOnlyList<CardInstanceId> _retainedCardIdsForActionEnd =
            Array.Empty<CardInstanceId>();

        /// <summary>创建只服务于指定玩家的机枪兵运行时，不引入第二条命令或队列入口。</summary>
        internal MachineGunnerBattleRuntime(
            BattleCombatantsData combatants,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            CombatantId playerId,
            uint cardRandomSeed = 1u)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _stateOperations = new BattleCombatantEffectOperations(_combatants);
            _poisonApplication = new BattlePoisonApplication(_combatants);
            if (enemyCombatantIdsInEncounterOrder == null)
                throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
            if (!_combatants.TryGet(playerId, out CombatantData player) ||
                !(player is PlayerCombatantData))
            {
                throw new ArgumentException("机枪兵运行时必须绑定已存在的玩家参与者。", nameof(playerId));
            }

            _playerId = playerId;
            _enemyCombatantIdsInEncounterOrder =
                new ReadOnlyCollection<CombatantId>(
                    new List<CombatantId>(enemyCombatantIdsInEncounterOrder));
            _targetSelector = new MachineGunnerTargetSelector(
                _combatants,
                _enemyCombatantIdsInEncounterOrder);
            _cardRandom = new GameRandom(cardRandomSeed);
            _combatState = new MachineGunnerCombatState();
            _scheduledEffects = new MachineGunnerScheduledEffectScheduler();
            _powerStacks = new Dictionary<MachineGunnerPowerKind, int>();
        }

        /// <summary>确认本运行时是否服务于指定玩家。</summary>
        internal bool SupportsPlayer(CombatantId playerId)
        {
            return playerId == _playerId;
        }

        /// <summary>读取本次行动结束需要保留的一次性手牌实例快照。</summary>
        internal IReadOnlyList<CardInstanceId> GetRetainedCardIdsForActionEnd(
            CombatantId playerId)
        {
            return SupportsPlayer(playerId)
                ? _retainedCardIdsForActionEnd
                : Array.Empty<CardInstanceId>();
        }

        /// <summary>在行动结束已成功处理保留牌后消费一次快照，后续回合恢复普通弃手。</summary>
        internal void ConsumeRetainedCardIdsForActionEnd(CombatantId playerId)
        {
            if (!SupportsPlayer(playerId))
                throw new ArgumentException("当前机枪兵运行时不服务于该玩家。", nameof(playerId));

            _retainedCardIdsForActionEnd = Array.Empty<CardInstanceId>();
        }

        /// <summary>返回当前兴奋剂剩余回合，仅供规则和测试读取而不复制可变状态。</summary>
        internal int StimTurns => _stimTurns;

        /// <summary>返回下一张成功攻击是否会免除实际能量与弹药支付。</summary>
        internal bool IsNextAttackFree => _nextAttackFree;

        /// <summary>返回职业专属卡牌随机流的当前状态，供确定性测试确认失败不会污染随机序列。</summary>
        internal uint CardRandomState => _cardRandom.State;

        /// <summary>返回当前会话唯一的机枪兵私有状态，用于同一内部模块的后续生命周期与伤害计算。</summary>
        internal MachineGunnerCombatState CombatState => _combatState;

        /// <summary>返回当前仍由本场职业运行时持有的独立延迟实例数量。</summary>
        internal int ScheduledEffectCount => _scheduledEffects.Count;

        /// <summary>按插入顺序读取指定生命周期的延迟实例快照，供同程序集回归验证。</summary>
        internal IReadOnlyList<MachineGunnerScheduledEffectInstance> GetScheduledEffects(
            MachineGunnerScheduledEffectTiming timing)
        {
            return _scheduledEffects.Snapshot(timing);
        }

        /// <summary>战斗进入终局时丢弃尚未触发的延迟实例，避免终局事实继续持有可结算任务。</summary>
        internal void ClearScheduledEffectsAtBattleEnd()
        {
            _scheduledEffects.Clear();
        }

        /// <summary>返回该职业在新玩家回合应补至的手牌目标，保留共享基础抽牌数并为后续能力牌叠加留出唯一入口。</summary>
        internal int GetPlayerRoundHandTarget(CombatantId playerId, int defaultHandTarget)
        {
            if (defaultHandTarget < 0)
                throw new ArgumentOutOfRangeException(nameof(defaultHandTarget));
            if (!SupportsPlayer(playerId))
                return defaultHandTarget;

            long desiredHandTarget = (long)defaultHandTarget +
                GetPowerStack(MachineGunnerPowerKind.PowerOverclock);
            return desiredHandTarget >= HandLimit ? HandLimit : (int)desiredHandTarget;
        }

        /// <summary>返回该职业单场手牌可容纳的最大数量，非本职业仍沿用既有无额外限制的卡区调用。</summary>
        internal int GetHandLimit(CombatantId playerId)
        {
            return SupportsPlayer(playerId) ? HandLimit : int.MaxValue;
        }

        /// <summary>只读判断程序所需的最低弹药是否存在；动态实际消耗仍由队首执行时的成本冻结统一决定。</summary>
        internal bool CanPayMinimumAmmo(MachineGunnerCardProgram program, int currentAmmo)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (currentAmmo < 0)
                throw new ArgumentOutOfRangeException(nameof(currentAmmo));

            switch (program.AmmoSpendMode)
            {
                case MachineGunnerAmmoSpendMode.None:
                case MachineGunnerAmmoSpendMode.AllAvailable:
                    return true;
                case MachineGunnerAmmoSpendMode.Fixed:
                case MachineGunnerAmmoSpendMode.UpToLimit:
                    return currentAmmo >= program.BaseAmmoCost;
                default:
                    throw new ArgumentOutOfRangeException(nameof(program.AmmoSpendMode));
            }
        }

        /// <summary>只读判断指定玩家的束缚是否会拒绝当前攻击程序。</summary>
        internal bool IsAttackBlockedByShackle(
            CombatantId playerId,
            MachineGunnerCardProgram program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));

            return SupportsPlayer(playerId) &&
                   program.IsAttack &&
                   _combatState.Get(playerId, MachineGunnerCombatantStatus.Shackle) > 0;
        }

        /// <summary>返回当前已生效能力牌的叠层数，未激活时为零；仅供同一职业运行时的规则与回归读取。</summary>
        internal int GetPowerStack(MachineGunnerPowerKind powerKind)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerPowerKind), powerKind))
                throw new ArgumentOutOfRangeException(nameof(powerKind));

            return _powerStacks.TryGetValue(powerKind, out int stacks) ? stacks : 0;
        }

        /// <summary>将本会话私有的攻击伤害链以内部公式覆盖形式提供给通用 Effect 执行器。</summary>
        internal IBattleDamageFormulaOverride DamageFormulaOverride => this;

        /// <summary>在玩家回合开始时清理全场烟雾、递减兴奋剂，并冻结下回合状态的清除、格挡、能量修正和补弹请求。</summary>
        internal MachineGunnerPlayerRoundStartResult BeginPlayerRound(CombatantId playerId)
        {
            if (!SupportsPlayer(playerId))
                return null;

            _recentSuccessfulCardCategory = MachineGunnerRecentSuccessfulCardCategory.None;
            _stimTurns = Math.Max(0, _stimTurns - 1);
            int armorBlock = _combatState.Get(playerId, MachineGunnerCombatantStatus.Armor);
            int nextRoundBlock = _combatState.Get(
                playerId,
                MachineGunnerCombatantStatus.NextRoundBlock);
            int blockGain = checked(armorBlock + nextRoundBlock);
            bool refillAmmo = _combatState.Get(
                playerId,
                MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound) > 0;
            int nextRoundEnergyGainBonus = _combatState.Get(
                playerId,
                MachineGunnerCombatantStatus.NextRoundEnergyGainBonus);
            int nextRoundEnergyGainPenalty = _combatState.Get(
                playerId,
                MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty);
            var combatantIds = new List<CombatantId>(_combatants.All.Keys);
            combatantIds.Sort((left, right) => left.Value.CompareTo(right.Value));
            foreach (CombatantId combatantId in combatantIds)
            {
                _combatState.AdvanceSmokeAtPlayerRoundStart(
                    combatantId,
                    persistsAndDecays: GetPowerStack(MachineGunnerPowerKind.SmokePersist) > 0);
            }

            MachineGunnerStatusValueChange? nextRoundBlockClear = nextRoundBlock > 0
                ? _combatState.Set(
                    playerId,
                    MachineGunnerCombatantStatus.NextRoundBlock,
                    value: 0)
                : null;
            MachineGunnerStatusValueChange? reloadAmmoClear = refillAmmo
                ? _combatState.Set(
                    playerId,
                    MachineGunnerCombatantStatus.ReloadAmmoAtNextPlayerRound,
                    value: 0)
                : null;
            MachineGunnerStatusValueChange? nextRoundEnergyGainBonusClear =
                nextRoundEnergyGainBonus > 0
                    ? _combatState.Set(
                        playerId,
                        MachineGunnerCombatantStatus.NextRoundEnergyGainBonus,
                        value: 0)
                    : null;
            MachineGunnerStatusValueChange? nextRoundEnergyGainPenaltyClear =
                nextRoundEnergyGainPenalty > 0
                    ? _combatState.Set(
                        playerId,
                        MachineGunnerCombatantStatus.NextRoundEnergyGainPenalty,
                        value: 0)
                    : null;
            return new MachineGunnerPlayerRoundStartResult(
                blockGain,
                nextRoundBlockClear,
                reloadAmmoClear,
                nextRoundEnergyGainBonusClear,
                nextRoundEnergyGainPenaltyClear);
        }

        /// <summary>冻结任一参与者行动结束时的再生治疗与临时状态清理计划，不提前修改战斗事实。</summary>
        internal MachineGunnerActorActionEndPlan PrepareActorActionEnd(CombatantId actorId)
        {
            if (!_combatants.TryGet(actorId, out CombatantData actor))
                throw new InvalidOperationException("行动结束计划找不到对应参与者。");

            int regenerationBefore = _combatState.Get(
                actorId,
                MachineGunnerCombatantStatus.Regeneration);
            BattleHealthRestorationOutcome? preparedRegenerationHealing =
                regenerationBefore > 0
                    ? BattleHealthRestorationOutcomeResolver.Resolve(
                        regenerationBefore,
                        actor.CurrentHealth,
                        actor.MaxHealth)
                    : null;
            return new MachineGunnerActorActionEndPlan(
                actor,
                actor.CurrentHealth,
                regenerationBefore,
                preparedRegenerationHealing,
                _combatState.Get(actorId, MachineGunnerCombatantStatus.LoseStrength),
                _combatState.Get(actorId, MachineGunnerCombatantStatus.Shackle));
        }

        /// <summary>校验行动结束计划仍对应当前职业私有状态，供联合命令在首次写入前拒绝快照漂移。</summary>
        internal bool ValidatePreparedActorActionEnd(MachineGunnerActorActionEndPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            bool succeeded = !plan.IsConsumed &&
                _combatants.TryGet(plan.ActorId, out CombatantData actor) &&
                ReferenceEquals(actor, plan.Actor) &&
                actor.IsAlive &&
                actor.CurrentHealth == plan.HealthBefore &&
                _combatState.Get(
                    plan.ActorId,
                    MachineGunnerCombatantStatus.Regeneration) == plan.RegenerationBefore &&
                _combatState.Get(
                    plan.ActorId,
                    MachineGunnerCombatantStatus.LoseStrength) == plan.LoseStrengthBefore &&
                _combatState.Get(
                    plan.ActorId,
                    MachineGunnerCombatantStatus.Shackle) == plan.ShackleBefore;
            plan.MarkValidated(succeeded);
            return succeeded;
        }

        /// <summary>提交已验证的行动结束生命周期：先清除束缚与失去力量，再治疗、递减再生及持续状态。</summary>
        internal IReadOnlyList<BattleSettlementRecord> CommitPreparedActorActionEnd(
            MachineGunnerActorActionEndPlan plan,
            int startingOrder)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            plan.MarkConsumed();

            var settlements = new List<BattleSettlementRecord>();
            if (plan.ShackleBefore > 0)
            {
                MachineGunnerStatusValueChange change = _combatState.Set(
                    plan.ActorId,
                    MachineGunnerCombatantStatus.Shackle,
                    value: 0);
                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    checked(startingOrder + settlements.Count),
                    plan.ActorId,
                    plan.ActorId,
                    change.Status,
                    change.Before,
                    change.After));
            }
            if (plan.LoseStrengthBefore > 0)
            {
                MachineGunnerStatusValueChange change = _combatState.Set(
                    plan.ActorId,
                    MachineGunnerCombatantStatus.LoseStrength,
                    value: 0);
                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    checked(startingOrder + settlements.Count),
                    plan.ActorId,
                    plan.ActorId,
                    change.Status,
                    change.Before,
                    change.After));
            }
            if (plan.PreparedRegenerationHealing.HasValue)
            {
                BattleHealthRestorationOutcome outcome =
                    plan.PreparedRegenerationHealing.Value;
                BattleCombatantEffectOperationResult healing =
                    _stateOperations.ApplyPreparedHealthRestoration(
                        plan.ActorId,
                        outcome);
                if (healing.Status != BattleCombatantEffectOperationStatus.Applied)
                {
                    throw new InvalidOperationException(
                        $"预构建后的再生治疗意外失败：{healing.Status}。");
                }

                settlements.Add(new BattleHealthRestoredSettlement(
                    checked(startingOrder + settlements.Count),
                    null,
                    plan.ActorId,
                    plan.ActorId,
                    healing.HealthRestorationOutcome.Value));
                MachineGunnerStatusValueChange regenerationChange = _combatState.Set(
                    plan.ActorId,
                    MachineGunnerCombatantStatus.Regeneration,
                    plan.RegenerationBefore - 1);
                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    checked(startingOrder + settlements.Count),
                    plan.ActorId,
                    plan.ActorId,
                    regenerationChange.Status,
                    regenerationChange.Before,
                    regenerationChange.After));
            }

            _combatState.ReduceDuration(
                plan.ActorId,
                MachineGunnerCombatantStatus.Weakness);
            if (SupportsPlayer(plan.ActorId))
            {
                _combatState.ReduceDuration(
                    plan.ActorId,
                    MachineGunnerCombatantStatus.Invisible);
            }

            return settlements.AsReadOnly();
        }

        /// <summary>在 Smoke 清理、资源补充和抽牌前预演全部玩家回合开始延迟实例。</summary>
        internal MachineGunnerScheduledEffectLifecyclePlan
            PreparePlayerRoundStartScheduledEffects()
        {
            return PrepareScheduledEffects(MachineGunnerScheduledEffectTiming.PlayerRoundStart);
        }

        /// <summary>在玩家回合末 Burn 之前预演全部炸弹倒计时与引爆。</summary>
        internal MachineGunnerScheduledEffectLifecyclePlan
            PreparePlayerRoundEndScheduledEffects()
        {
            return PrepareScheduledEffects(MachineGunnerScheduledEffectTiming.PlayerRoundEnd);
        }

        /// <summary>校验调度计划仍对应当前实例容器与职业随机流。</summary>
        internal bool ValidatePreparedScheduledEffects(
            MachineGunnerScheduledEffectLifecyclePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return _cardRandom.State == plan.RandomStateBefore &&
                   _scheduledEffects.ValidateMutation(plan.MutationPlan);
        }

        /// <summary>提交已验证的调度计划，并按触发、效果、进度、移除顺序生成连续记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> CommitPreparedScheduledEffects(
            MachineGunnerScheduledEffectLifecyclePlan plan,
            int startingOrder)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!ValidatePreparedScheduledEffects(plan))
                throw new InvalidOperationException("延迟实例生命周期计划发生快照漂移。");

            var settlements = new List<BattleSettlementRecord>();
            var offsetSettlements = new OffsetSettlementCollection(startingOrder, settlements);
            foreach (MachineGunnerPreparedScheduledEffect effect in plan.Effects)
            {
                int remainingBefore = GetScheduledRemaining(effect.Instance);
                if (effect.Triggered)
                {
                    settlements.Add(new MachineGunnerScheduledEffectChangedSettlement(
                        checked(startingOrder + settlements.Count),
                        effect.Instance.SourceId,
                        effect.Instance,
                        MachineGunnerScheduledEffectChangeKind.Triggered,
                        remainingBefore,
                        remainingBefore));
                }

                foreach (MachineGunnerPreparedOperation operation in effect.Operations)
                {
                    CommitScheduledOperation(
                        effect.Instance.SourceId,
                        operation,
                        offsetSettlements);
                }

                int remainingAfter = effect.After == null
                    ? 0
                    : GetScheduledRemaining(effect.After);
                settlements.Add(new MachineGunnerScheduledEffectChangedSettlement(
                    checked(startingOrder + settlements.Count),
                    effect.Instance.SourceId,
                    effect.Instance,
                    MachineGunnerScheduledEffectChangeKind.Countdown,
                    remainingBefore,
                    remainingAfter));
                if (effect.After == null)
                {
                    settlements.Add(new MachineGunnerScheduledEffectChangedSettlement(
                        checked(startingOrder + settlements.Count),
                        effect.Instance.SourceId,
                        effect.Instance,
                        MachineGunnerScheduledEffectChangeKind.Removed,
                        remainingAfter,
                        remainingAfter));
                }
            }

            _scheduledEffects.CommitMutation(plan.MutationPlan);
            _cardRandom.State = plan.RandomStateAfter;
            if (settlements.Count != plan.SettlementCount)
                throw new InvalidOperationException("延迟实例计划的 settlement 数量与提交结果不一致。");
            return settlements.AsReadOnly();
        }

        /// <summary>按插入顺序冻结指定生命周期的目标、随机流、伤害、状态与实例进度。</summary>
        private MachineGunnerScheduledEffectLifecyclePlan PrepareScheduledEffects(
            MachineGunnerScheduledEffectTiming timing)
        {
            IReadOnlyList<MachineGunnerScheduledEffectInstance> instances =
                _scheduledEffects.Snapshot(timing);
            var preparedEffects = new List<MachineGunnerPreparedScheduledEffect>();
            var mutations = new List<MachineGunnerScheduledEffectMutation>();
            var projectedTargets = CreateProjectedCombatants();
            GameRandom candidateRandom = CreateCardRandomCandidate();
            int settlementCount = 0;

            foreach (MachineGunnerScheduledEffectInstance instance in instances)
            {
                if (GetProjectedLivingEnemies(projectedTargets).Count == 0)
                    break;

                bool triggers = instance.Countdown == 1 || instance.RemainingTriggers > 0;
                var operations = new List<MachineGunnerPreparedOperation>();
                if (triggers)
                {
                    PrepareScheduledEffectOperations(
                        instance,
                        candidateRandom,
                        projectedTargets,
                        operations);
                }

                int countdownAfter = instance.Countdown > 0
                    ? instance.Countdown - 1
                    : 0;
                int triggersAfter = instance.RemainingTriggers > 0
                    ? instance.RemainingTriggers - 1
                    : 0;
                MachineGunnerScheduledEffectInstance after =
                    countdownAfter == 0 && triggersAfter == 0
                        ? null
                        : instance.WithProgress(countdownAfter, triggersAfter);
                var prepared = new MachineGunnerPreparedScheduledEffect(
                    instance,
                    operations,
                    triggers,
                    after);
                preparedEffects.Add(prepared);
                mutations.Add(new MachineGunnerScheduledEffectMutation(instance, after, triggers));

                settlementCount = checked(settlementCount + 1);
                if (triggers)
                    settlementCount = checked(settlementCount + 1);
                foreach (MachineGunnerPreparedOperation operation in operations)
                {
                    settlementCount = checked(
                        settlementCount + GetPreparedOperationSettlementCount(operation));
                }
                if (after == null)
                    settlementCount = checked(settlementCount + 1);
            }

            MachineGunnerScheduledEffectMutationPlan mutationPlan =
                _scheduledEffects.PrepareMutation(timing, mutations);
            return new MachineGunnerScheduledEffectLifecyclePlan(
                timing,
                preparedEffects,
                mutationPlan,
                _cardRandom.State,
                candidateRandom.State,
                settlementCount);
        }

        /// <summary>按实例种类把冻结载荷展开为既有伤害和私有状态预演操作。</summary>
        private void PrepareScheduledEffectOperations(
            MachineGunnerScheduledEffectInstance instance,
            GameRandom candidateRandom,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> operations)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (candidateRandom == null)
                throw new ArgumentNullException(nameof(candidateRandom));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (!_combatants.TryGet(instance.SourceId, out CombatantData source) || !source.IsAlive)
                return;

            switch (instance.Kind)
            {
                case MachineGunnerScheduledEffectKind.GuidedNuke:
                case MachineGunnerScheduledEffectKind.FiveHundredPounder:
                    AppendScheduledAllEnemyDamage(
                        instance,
                        source,
                        MachineGunnerDamageKind.Bomb,
                        projectedTargets,
                        operations);
                    break;
                case MachineGunnerScheduledEffectKind.BansheeStrike:
                    PrepareBansheeOperations(
                        instance,
                        source,
                        candidateRandom,
                        projectedTargets,
                        operations);
                    break;
                case MachineGunnerScheduledEffectKind.FireSupport:
                    PrepareRandomSupportOperations(
                        instance,
                        source,
                        candidateRandom,
                        projectedTargets,
                        operations,
                        applyArmorBreakAfterHit: false);
                    break;
                case MachineGunnerScheduledEffectKind.FireBombardment:
                    PrepareFireBombardmentOperations(
                        instance,
                        source,
                        candidateRandom,
                        projectedTargets,
                        operations);
                    break;
                case MachineGunnerScheduledEffectKind.TripleStrike:
                    PrepareFurthestSupportOperation(
                        instance,
                        source,
                        candidateRandom,
                        projectedTargets,
                        operations);
                    break;
                case MachineGunnerScheduledEffectKind.NeedleStorm:
                    PrepareRandomSupportOperations(
                        instance,
                        source,
                        candidateRandom,
                        projectedTargets,
                        operations,
                        applyArmorBreakAfterHit: true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(instance.Kind));
            }
        }

        /// <summary>把炸弹伤害按当前 Encounter 存活顺序冻结为一次全敌操作。</summary>
        private void AppendScheduledAllEnemyDamage(
            MachineGunnerScheduledEffectInstance instance,
            CombatantData source,
            MachineGunnerDamageKind damageKind,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> operations)
        {
            var damages = new List<MachineGunnerPreparedDamage>();
            foreach (CombatantId targetId in GetProjectedLivingEnemies(projectedTargets))
            {
                AppendPreparedScheduledDamageIfAlive(
                    instance.SourceId,
                    source,
                    targetId,
                    instance.Damage,
                    damageKind,
                    projectedTargets,
                    damages);
            }
            AppendScheduledDamageOperation(instance.Damage, damages, operations);
        }

        /// <summary>女妖每次触发只锁定当时最近目标，两段之间不重新选人。</summary>
        private void PrepareBansheeOperations(
            MachineGunnerScheduledEffectInstance instance,
            CombatantData source,
            GameRandom candidateRandom,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> operations)
        {
            int damage = ResolveBombardAdjustedSupportValue(instance, instance.Damage);
            IReadOnlyList<CombatantId> living = GetProjectedLivingEnemies(projectedTargets);
            if (living.Count == 0)
                return;

            CombatantId targetId = living[0];
            for (int hit = 0; hit < instance.HitCount; hit++)
            {
                var damages = new List<MachineGunnerPreparedDamage>(capacity: 1);
                if (!AppendPreparedScheduledDamageIfAlive(
                        instance.SourceId,
                        source,
                        targetId,
                        damage,
                        MachineGunnerDamageKind.Support,
                        projectedTargets,
                        damages))
                {
                    break;
                }
                AppendScheduledDamageOperation(damage, damages, operations);
                AppendSkyWrathOperations(
                    instance.SourceId,
                    source,
                    candidateRandom,
                    projectedTargets,
                    operations);
            }
        }

        /// <summary>火力支援与钢针逐段从当前投影中的存活敌人重新抽取随机目标。</summary>
        private void PrepareRandomSupportOperations(
            MachineGunnerScheduledEffectInstance instance,
            CombatantData source,
            GameRandom candidateRandom,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> operations,
            bool applyArmorBreakAfterHit)
        {
            int damage = ResolveBombardAdjustedSupportValue(instance, instance.Damage);
            MachineGunnerDamageKind damageKind = applyArmorBreakAfterHit
                ? MachineGunnerDamageKind.Delayed
                : MachineGunnerDamageKind.Support;
            for (int hit = 0; hit < instance.HitCount; hit++)
            {
                IReadOnlyList<CombatantId> living = GetProjectedLivingEnemies(projectedTargets);
                if (living.Count == 0)
                    break;

                CombatantId targetId = living[candidateRandom.NextInt(living.Count)];
                var damages = new List<MachineGunnerPreparedDamage>(capacity: 1);
                if (!AppendPreparedScheduledDamageIfAlive(
                        instance.SourceId,
                        source,
                        targetId,
                        damage,
                        damageKind,
                        projectedTargets,
                        damages))
                {
                    continue;
                }
                AppendScheduledDamageOperation(damage, damages, operations);
                if (instance.Kind == MachineGunnerScheduledEffectKind.FireSupport)
                {
                    AppendSkyWrathOperations(
                        instance.SourceId,
                        source,
                        candidateRandom,
                        projectedTargets,
                        operations);
                }
                if (applyArmorBreakAfterHit &&
                    projectedTargets[targetId].Health > 0)
                {
                    operations.Add(PrepareScheduledPrivateStatusOperation(
                        targetId,
                        MachineGunnerCombatantStatus.ArmorBreak,
                        instance.ArmorBreak,
                        projectedTargets));
                }
            }
        }

        /// <summary>燃烧轰炸逐波重取存活敌人，并对每名目标冻结伤害、燃烧、浸油顺序。</summary>
        private void PrepareFireBombardmentOperations(
            MachineGunnerScheduledEffectInstance instance,
            CombatantData source,
            GameRandom candidateRandom,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> operations)
        {
            int damage = ResolveBombardAdjustedSupportValue(instance, instance.Damage);
            int burn = ResolveBombardAdjustedSupportValue(instance, instance.Burn);
            int oil = ResolveBombardAdjustedSupportValue(instance, instance.Oil);
            for (int wave = 0; wave < instance.WaveCount; wave++)
            {
                IReadOnlyList<CombatantId> livingAtWaveStart =
                    GetProjectedLivingEnemies(projectedTargets);
                foreach (CombatantId targetId in livingAtWaveStart)
                {
                    var damages = new List<MachineGunnerPreparedDamage>(capacity: 1);
                    if (!AppendPreparedScheduledDamageIfAlive(
                            instance.SourceId,
                            source,
                            targetId,
                            damage,
                            MachineGunnerDamageKind.Support,
                            projectedTargets,
                            damages))
                    {
                        continue;
                    }
                    AppendScheduledDamageOperation(damage, damages, operations);
                    if (projectedTargets[targetId].Health <= 0)
                        continue;

                    operations.Add(PrepareScheduledBurnOperation(
                        targetId,
                        burn,
                        projectedTargets));
                    operations.Add(PrepareScheduledPrivateStatusOperation(
                        targetId,
                        MachineGunnerCombatantStatus.Oil,
                        oil,
                        projectedTargets));
                }
                AppendSkyWrathOperations(
                    instance.SourceId,
                    source,
                    candidateRandom,
                    projectedTargets,
                    operations);
            }
        }

        /// <summary>三连击延迟段在触发时选择当前最远存活敌人并使用支援公式。</summary>
        private void PrepareFurthestSupportOperation(
            MachineGunnerScheduledEffectInstance instance,
            CombatantData source,
            GameRandom candidateRandom,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> operations)
        {
            int damage = ResolveBombardAdjustedSupportValue(instance, instance.Damage);
            IReadOnlyList<CombatantId> living = GetProjectedLivingEnemies(projectedTargets);
            if (living.Count == 0)
                return;

            var damages = new List<MachineGunnerPreparedDamage>(capacity: 1);
            if (!AppendPreparedScheduledDamageIfAlive(
                instance.SourceId,
                source,
                living[living.Count - 1],
                damage,
                MachineGunnerDamageKind.Support,
                projectedTargets,
                damages))
            {
                return;
            }
            AppendScheduledDamageOperation(damage, damages, operations);
            AppendSkyWrathOperations(
                instance.SourceId,
                source,
                candidateRandom,
                projectedTargets,
                operations);
        }

        /// <summary>在指定支援延迟效果触发时读取当前轰炸层数，并按正数半入规则集中换算伤害或附加状态值。</summary>
        private int ResolveBombardAdjustedSupportValue(
            MachineGunnerScheduledEffectInstance instance,
            int baseValue)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (baseValue < 0)
                throw new ArgumentOutOfRangeException(nameof(baseValue));

            switch (instance.Kind)
            {
                case MachineGunnerScheduledEffectKind.BansheeStrike:
                case MachineGunnerScheduledEffectKind.FireSupport:
                case MachineGunnerScheduledEffectKind.FireBombardment:
                case MachineGunnerScheduledEffectKind.TripleStrike:
                    break;
                default:
                    return baseValue;
            }

            return ResolveBombardScaledSupportValue(baseValue);
        }

        /// <summary>按当前狂轰滥炸总层数对一个支援基础值执行线性百分比与正数半入取整，不绑定具体延迟实例种类。</summary>
        private int ResolveBombardScaledSupportValue(int baseValue)
        {
            if (baseValue < 0)
                throw new ArgumentOutOfRangeException(nameof(baseValue));

            int bombardStacks = GetPowerStack(MachineGunnerPowerKind.Bombard);
            if (baseValue == 0 || bombardStacks == 0)
                return baseValue;

            long percentage = checked(100L + checked(10L * bombardStacks));
            long scaledNumerator = checked(checked((long)baseValue * percentage) + 50L);
            return checked((int)(scaledNumerator / 100L));
        }

        /// <summary>在一个原始支援段完整结束后，按天空之怒层数逐层随机主目标并依 Encounter 顺序追加同层溅射伤害。</summary>
        private void AppendSkyWrathOperations(
            CombatantId sourceId,
            CombatantData source,
            GameRandom candidateRandom,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> operations)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (candidateRandom == null)
                throw new ArgumentNullException(nameof(candidateRandom));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            int skyWrathStacks = GetPowerStack(MachineGunnerPowerKind.SkyWrath);
            if (skyWrathStacks == 0)
                return;

            int mainDamage = ResolveBombardScaledSupportValue(8);
            int splashDamage = ResolveBombardScaledSupportValue(4);
            for (int stack = 0; stack < skyWrathStacks; stack++)
            {
                IReadOnlyList<CombatantId> livingAtLayerStart =
                    GetProjectedLivingEnemies(projectedTargets);
                if (livingAtLayerStart.Count == 0)
                    break;

                CombatantId mainTargetId = livingAtLayerStart[
                    candidateRandom.NextInt(livingAtLayerStart.Count)];
                var mainDamages = new List<MachineGunnerPreparedDamage>(capacity: 1);
                AppendPreparedScheduledDamageIfAlive(
                    sourceId,
                    source,
                    mainTargetId,
                    mainDamage,
                    MachineGunnerDamageKind.Support,
                    projectedTargets,
                    mainDamages);
                AppendScheduledDamageOperation(mainDamage, mainDamages, operations);

                foreach (CombatantId targetId in livingAtLayerStart)
                {
                    if (targetId.Equals(mainTargetId))
                        continue;

                    var splashDamages = new List<MachineGunnerPreparedDamage>(capacity: 1);
                    AppendPreparedScheduledDamageIfAlive(
                        sourceId,
                        source,
                        targetId,
                        splashDamage,
                        MachineGunnerDamageKind.Support,
                        projectedTargets,
                        splashDamages);
                    AppendScheduledDamageOperation(splashDamage, splashDamages, operations);
                }
            }
        }

        /// <summary>以投影私有状态计算一段延迟伤害并更新同生命周期后续段读取的生命与格挡。</summary>
        private bool AppendPreparedScheduledDamageIfAlive(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            int damage,
            MachineGunnerDamageKind damageKind,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedDamage> damages)
        {
            if (!projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) ||
                projected.Health <= 0)
            {
                return false;
            }

            MachineGunnerCombatState projectedState =
                CreateCombatStateFromProjection(projectedTargets);
            MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
                new MachineGunnerDamageRequest(
                    sourceId,
                    targetId,
                    damage,
                    damageKind,
                    MachineGunnerCardTag.None),
                source,
                new BattleEffectTargetSnapshot(
                    projected.Health,
                    projected.Block,
                    projected.Vulnerable),
                projectedState);
            damages.Add(new MachineGunnerPreparedDamage(
                targetId,
                calculation.Outcome,
                calculation.ConsumesArmor));
            projected.Health = calculation.Outcome.HealthAfter;
            projected.Block = calculation.Outcome.BlockAfter;
            return true;
        }

        /// <summary>仅在至少冻结一段伤害时追加现有伤害操作。</summary>
        private static void AppendScheduledDamageOperation(
            int damage,
            IReadOnlyList<MachineGunnerPreparedDamage> damages,
            ICollection<MachineGunnerPreparedOperation> operations)
        {
            if (damages.Count == 0)
                return;

            operations.Add(new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.Damage,
                damage,
                damages,
                blockBefore: 0,
                blockAfter: 0));
        }

        /// <summary>冻结一名存活目标的私有状态增量并更新后续投影。</summary>
        private static MachineGunnerPreparedOperation PrepareScheduledPrivateStatusOperation(
            CombatantId targetId,
            MachineGunnerCombatantStatus status,
            int value,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            MachineGunnerProjectedCombatant projected = projectedTargets[targetId];
            int before = projected.GetPrivateStatus(status);
            int after = checked(before + value);
            projected.SetPrivateStatus(status, after);
            return new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.ApplyPrivateStatus,
                value,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                statusChanges: new[]
                {
                    new MachineGunnerPreparedStatusChange(
                        MachineGunnerPreparedStatusChangeKind.PrivateStatus,
                        targetId,
                        status,
                        before,
                        after),
                });
        }

        /// <summary>冻结一名存活目标的燃烧与既有浸油减半，并更新后续投影。</summary>
        private static MachineGunnerPreparedOperation PrepareScheduledBurnOperation(
            CombatantId targetId,
            int burn,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets)
        {
            if (burn <= 0)
                throw new ArgumentOutOfRangeException(nameof(burn));
            MachineGunnerProjectedCombatant projected = projectedTargets[targetId];
            MachineGunnerBurnApplicationResult result =
                MachineGunnerCombatState.CalculateBurnApplication(
                    projected.GetPrivateStatus(MachineGunnerCombatantStatus.Burn),
                    projected.GetPrivateStatus(MachineGunnerCombatantStatus.Oil),
                    burn);
            projected.SetPrivateStatus(
                MachineGunnerCombatantStatus.Burn,
                result.BurnChange.After);
            projected.SetPrivateStatus(
                MachineGunnerCombatantStatus.Oil,
                result.OilChange.After);
            return new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.ApplyBurn,
                burn,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                burnApplications: new[]
                {
                    new MachineGunnerPreparedBurnApplication(targetId, result),
                });
        }

        /// <summary>按 Encounter 顺序返回投影中仍存活的敌人身份。</summary>
        private IReadOnlyList<CombatantId> GetProjectedLivingEnemies(
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets)
        {
            var living = new List<CombatantId>();
            foreach (CombatantId targetId in _targetSelector.GetLivingEnemiesInEncounterOrder())
            {
                if (projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) &&
                    projected.Health > 0)
                {
                    living.Add(targetId);
                }
            }
            return living.AsReadOnly();
        }

        /// <summary>把投影私有状态复制到只供本次伤害公式读取的临时状态容器。</summary>
        private static MachineGunnerCombatState CreateCombatStateFromProjection(
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets)
        {
            var state = new MachineGunnerCombatState();
            foreach (KeyValuePair<CombatantId, MachineGunnerProjectedCombatant> pair in projectedTargets)
            {
                foreach (MachineGunnerCombatantStatus status in
                         (MachineGunnerCombatantStatus[])Enum.GetValues(
                             typeof(MachineGunnerCombatantStatus)))
                {
                    int value = pair.Value.GetPrivateStatus(status);
                    if (value > 0)
                        state.Set(pair.Key, status, value);
                }
            }
            return state;
        }

        /// <summary>返回倒计时或剩余触发次数中的唯一进度值。</summary>
        private static int GetScheduledRemaining(MachineGunnerScheduledEffectInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            return instance.Countdown > 0 ? instance.Countdown : instance.RemainingTriggers;
        }

        /// <summary>计算既有预演操作提交时会产生的权威记录数量。</summary>
        private static int GetPreparedOperationSettlementCount(
            MachineGunnerPreparedOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            switch (operation.Kind)
            {
                case MachineGunnerProgramOperationKind.Damage:
                    return operation.Damages.Count;
                case MachineGunnerProgramOperationKind.ApplyPrivateStatus:
                case MachineGunnerProgramOperationKind.ApplyPrivateStatusFromSpentAmmo:
                case MachineGunnerProgramOperationKind.ApplyVulnerable:
                    return operation.StatusChanges.Count;
                case MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke:
                    return operation.PoisonApplication.Plan.HasWrite ? 1 : 0;
                case MachineGunnerProgramOperationKind.ApplyBurn:
                    int count = 0;
                    foreach (MachineGunnerPreparedBurnApplication application in
                             operation.BurnApplications)
                    {
                        count = checked(count + 1);
                        if (application.Result.OilChange.Before !=
                            application.Result.OilChange.After)
                        {
                            count = checked(count + 1);
                        }
                    }
                    return count;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation.Kind));
            }
        }

        /// <summary>把调度触发复用的既有伤害与状态操作提交到带全局 order 偏移的集合。</summary>
        private void CommitScheduledOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            switch (operation.Kind)
            {
                case MachineGunnerProgramOperationKind.Damage:
                    CommitDamageOperation(sourceId, operation, settlements);
                    break;
                case MachineGunnerProgramOperationKind.ApplyPrivateStatus:
                    CommitPrivateStatusOperation(sourceId, operation, settlements);
                    break;
                case MachineGunnerProgramOperationKind.ApplyBurn:
                    CommitBurnOperation(sourceId, operation, settlements);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation.Kind));
            }
        }

        /// <summary>按 Encounter 顺序结算回合末烈火烹油增长与燃烧伤害，返回当前 Queue 命令中的连续记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> ResolvePlayerRoundEnd(int startingOrder)
        {
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));

            var settlements = new List<BattleSettlementRecord>();
            IReadOnlyList<CombatantId> livingEnemyIds =
                _targetSelector.GetLivingEnemiesInEncounterOrder();
            AppendBurningOilGrowthForLivingEnemies(
                livingEnemyIds,
                startingOrder,
                settlements);
            foreach (CombatantId enemyId in livingEnemyIds)
            {
                AppendBurnDamageIfLiving(enemyId, startingOrder, settlements);
                if (_targetSelector.GetLivingEnemiesInEncounterOrder().Count == 0)
                    return settlements.AsReadOnly();
            }

            if (_combatants.TryGet(_playerId, out CombatantData player) && player.IsAlive)
                AppendBurnDamageIfLiving(_playerId, startingOrder, settlements);

            return settlements.AsReadOnly();
        }

        /// <summary>预演并一次提交一张已绑定程序的机枪兵卡；失败时不改参与者、卡区或职业状态。</summary>
        internal MachineGunnerCardProgramExecutionResult ExecutePlayerCard(
            PlayCardCommand command,
            PlayerTurnData playerTurn,
            BattleCardZonesData cardZones,
            CardInstanceData card,
            cfg.battle.Card cardTemplate,
            BattleCardLevelProjection cardLevelProjection,
            BattleBlockRetention blockRetention,
            cfg.Tables tables,
            BattleTriggeredCardPlayExecution triggeredCardPlayExecution,
            BattleSettlementTriggerEngine settlementTriggerEngine,
            BattleTriggeredCardPlayRequest incomingTriggeredPlayRequest = null)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (playerTurn == null)
                throw new ArgumentNullException(nameof(playerTurn));
            if (cardZones == null)
                throw new ArgumentNullException(nameof(cardZones));
            if (card == null)
                throw new ArgumentNullException(nameof(card));
            if (cardTemplate == null)
                throw new ArgumentNullException(nameof(cardTemplate));
            if (cardLevelProjection == null)
                throw new ArgumentNullException(nameof(cardLevelProjection));
            if (blockRetention == null)
                throw new ArgumentNullException(nameof(blockRetention));
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (triggeredCardPlayExecution == null)
                throw new ArgumentNullException(nameof(triggeredCardPlayExecution));
            if (settlementTriggerEngine == null)
                throw new ArgumentNullException(nameof(settlementTriggerEngine));
            if (!SupportsPlayer(command.ActorId))
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.MachineGunnerRuntimeUnavailable);
            if (card.TemplateId != cardTemplate.Id)
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.CardTemplateNotFound);
            if (cardLevelProjection.Template.Id != cardTemplate.Id ||
                cardLevelProjection.UpgradeLevel != card.UpgradeLevel)
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.CardTemplateNotFound);
            }
            if (!MachineGunnerCardProgramRegistry.TryGet(cardTemplate.ProgramId, out MachineGunnerCardProgram program))
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.CardNotImplemented);
            }
            if (IsAttackBlockedByShackle(command.ActorId, program))
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.AttackBlockedByShackle);
            }
            if (cardLevelProjection.PlayDestination == cfg.battle.CardPlayDestination.Power &&
                !program.PowerKind.HasValue)
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.UnsupportedMachineGunnerProgram);
            }
            if (cardLevelProjection.PlayDestination != cfg.battle.CardPlayDestination.DiscardPile &&
                cardLevelProjection.PlayDestination != cfg.battle.CardPlayDestination.Power &&
                cardLevelProjection.PlayDestination != cfg.battle.CardPlayDestination.ExhaustPile)
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.UnsupportedMachineGunnerProgram);
            }
            if (program.PowerKind.HasValue &&
                cardLevelProjection.PlayDestination != cfg.battle.CardPlayDestination.Power)
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.UnsupportedMachineGunnerProgram);
            }
            if (!IsCardInHand(cardZones, command.CardId))
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.CardNotInHand);
            }
            BattleCardPaymentMode requestedPaymentMode = incomingTriggeredPlayRequest?.PaymentMode ??
                BattleCardPaymentMode.Normal;
            BattleCardZone playedCardDestination = incomingTriggeredPlayRequest?.Destination ??
                MapPlayedCardDestination(cardLevelProjection.PlayDestination);

            GameRandom candidateRandom = CreateCardRandomCandidate();
            IReadOnlyList<CombatantId> targetIds = null;
            int? prismaticInitialStatusKindCount = null;
            if (program.ExecutionKind ==
                MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds)
            {
                if (!TryResolveTargets(
                        command,
                        program,
                        candidateRandom,
                        out targetIds,
                        out BattleCommandExecutionFailureReason earlyTargetFailure))
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(earlyTargetFailure);
                }
                if (targetIds.Count != 1)
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(
                        BattleCommandExecutionFailureReason.TargetRuleMismatch);
                }

                prismaticInitialStatusKindCount =
                    CountInitialActiveStatusKindsForCombatant(targetIds[0]);
            }
            if (!TryPrepareCost(
                    cardTemplate,
                    cardLevelProjection.Cost,
                    playerTurn,
                    program,
                    prismaticInitialStatusKindCount,
                    requestedPaymentMode,
                    out MachineGunnerPreparedCost preparedCost,
                    out BattleCommandExecutionFailureReason costFailure))
            {
                return MachineGunnerCardProgramExecutionResult.Failed(costFailure);
            }
            MachineGunnerCostResolution cost = preparedCost.Resolution;
            if (!_combatants.TryGet(command.ActorId, out CombatantData player) || !player.IsAlive)
            {
                return MachineGunnerCardProgramExecutionResult.Failed(
                    BattleCommandExecutionFailureReason.PlayerNotAlive);
            }

            if (targetIds == null && !TryResolveTargets(
                    command,
                    program,
                    candidateRandom,
                    out targetIds,
                    out BattleCommandExecutionFailureReason targetFailure))
            {
                return MachineGunnerCardProgramExecutionResult.Failed(targetFailure);
            }

            BattlePreparedSettlementTriggerRegistration unstoppableTriggerPlan = null;
            if (program.PowerKind == MachineGunnerPowerKind.Unstoppable)
            {
                unstoppableTriggerPlan =
                    settlementTriggerEngine.PrepareFatalOrBlockBrokenRandomCardPlay(
                        command.ActorId,
                        CollectUnstoppableCandidateTemplateIds(tables));
            }

            IReadOnlyList<CardInstanceId> garrisonRetainedCardIds = null;
            BattlePreparedBlockRetentionPlan garrisonRetentionPlan = null;
            if (program.Id == cfg.battle.MachineGunnerProgramId.Garrison)
            {
                BattleCommandExecutionFailureReason selectionFailure =
                    TryFreezeGarrisonSelection(
                        command,
                        cardZones,
                        out garrisonRetainedCardIds);
                if (selectionFailure != BattleCommandExecutionFailureReason.None)
                    return MachineGunnerCardProgramExecutionResult.Failed(selectionFailure);

                try
                {
                    garrisonRetentionPlan = blockRetention.PrepareTimed(command.ActorId, rounds: 2);
                }
                catch (OverflowException)
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(
                        BattleCommandExecutionFailureReason.EffectValueOverflow);
                }
            }

            IReadOnlyList<MachineGunnerPreparedOperation> operations =
                Array.Empty<MachineGunnerPreparedOperation>();
            BattlePreparedRepeatedDamagePlan repeatedDamagePlan = null;
            BattleRepeatedDamageExecutor repeatedDamageExecutor = null;
            if (program.ExecutionKind ==
                MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds)
            {
                BattleRepeatedDamagePreparationResult repeatedPreparation =
                    PreparePrismaticRepeatedDamage(
                        command.ActorId,
                        player,
                        targetIds[0],
                        program,
                        prismaticInitialStatusKindCount.Value,
                        cost,
                        out repeatedDamageExecutor);
                if (!repeatedPreparation.Succeeded)
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(
                        repeatedPreparation.FailureReason);
                }

                repeatedDamagePlan = repeatedPreparation.Plan;
            }
            else if (!TryPrepareOperations(
                         command.ActorId,
                         player,
                         targetIds,
                         program,
                         cardLevelProjection,
                         cost,
                         candidateRandom,
                         out operations,
                         out BattleCommandExecutionFailureReason preparationFailure))
            {
                return MachineGunnerCardProgramExecutionResult.Failed(preparationFailure);
            }

            BattlePreparedTriggeredCardPlay triggeredHandAttackPlan = null;
            if (program.TriggersRandomHandAttackAfterPreviousAttackOrShoot &&
                IsPreviousSuccessfulCardAttackOrShoot())
            {
                if (!TryPrepareRandomHandAttackContinuation(
                        command.ActorId,
                        command.CardId,
                        cardZones,
                        tables,
                        candidateRandom,
                        triggeredCardPlayExecution,
                        incomingTriggeredPlayRequest?.Depth ?? 0,
                        out triggeredHandAttackPlan,
                        out BattleCommandExecutionFailureReason triggeredFailure))
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(triggeredFailure);
                }
            }

            MachineGunnerScheduledEffectCreationPlan scheduledCreationPlan = null;
            if (program.ScheduledEffect != null)
            {
                try
                {
                    scheduledCreationPlan = _scheduledEffects.PrepareCreation(
                        command.ActorId,
                        program.ScheduledEffect);
                }
                catch (OverflowException)
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(
                        BattleCommandExecutionFailureReason.EffectValueOverflow);
                }

                if (!_scheduledEffects.ValidateCreation(scheduledCreationPlan))
                {
                    throw new InvalidOperationException(
                        "延迟实例创建计划在首次战斗写入前发生快照漂移。");
                }
            }

            int energyAfter = playerTurn.Energy - cost.EnergySpent;
            int ammoAfter = playerTurn.Ammo - cost.ActualAmmoSpent;
            PlayerTurnData playerTurnAfter = playerTurn.WithResources(
                energyAfter,
                playerTurn.EnergyMaximum,
                playerTurn.EnergyGainPerRound,
                ammoAfter,
                playerTurn.AmmoMaximum,
                playerTurn.AmmoGainPerRound);
            // 先把固定付款记录放进本地序列，确保所有深卡区计划冻结的起始顺序包含同一前缀。
            var settlements = new List<BattleSettlementRecord>
            {
                new BattleEnergySpentSettlement(
                    order: 0,
                    command.ActorId,
                    playerTurn.Energy,
                    energyAfter)
            };
            if (cost.ActualAmmoSpent > 0)
            {
                settlements.Add(new BattleAmmoSpentSettlement(
                    settlements.Count,
                    command.ActorId,
                    playerTurn.Ammo,
                    ammoAfter));
            }

            BattlePreparedHandCardSelectionResolution ventHeatSelectionPlan = null;
            if (program.Id == cfg.battle.MachineGunnerProgramId.VentHeat &&
                command.SelectedCardIds.Count > 0)
            {
                if (command.SelectedCardIds.Count != 1)
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(
                        BattleCommandExecutionFailureReason.UnsupportedMachineGunnerProgram);
                }

                CardInstanceId selectedCardId = command.SelectedCardIds[0];
                if (selectedCardId == command.CardId)
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(
                        BattleCommandExecutionFailureReason.UnsupportedMachineGunnerProgram);
                }
                if (!IsCardInHand(cardZones, selectedCardId))
                {
                    return MachineGunnerCardProgramExecutionResult.Failed(
                        BattleCommandExecutionFailureReason.CardNotInHand);
                }

                int energyGainSettlementCount =
                    playerTurnAfter.Energy < playerTurnAfter.EnergyMaximum ? 1 : 0;
                int selectedStartingOrder = settlements.Count;
                int playedCardStartingOrder = checked(
                    selectedStartingOrder + 1 + energyGainSettlementCount);
                ventHeatSelectionPlan = cardZones.PrepareHandCardSelectionResolution(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    command.CardId,
                    playedCardDestination,
                    selectedStartingOrder,
                    playedCardStartingOrder);
                if (!cardZones.ValidatePreparedHandCardSelectionResolution(
                        ventHeatSelectionPlan))
                {
                    throw new InvalidOperationException(
                        "排气散热手牌选择计划在首次战斗写入前发生快照漂移。");
                }
            }

            int firstOperationToCommit = 0;
            int playedCardDepartureOperationIndex = -1;
            BattlePreparedPlayedCardDepartureAndDraw playedCardDeparturePlan = null;
            int handReplacementOperationIndex = -1;
            BattlePreparedPlayedCardDepartureDiscardHandAndCreate handReplacementPlan = null;
            int preparedDrawOperationIndex = -1;
            BattlePreparedDraw preparedDrawPlan = null;
            bool hasCardZoneMutationOperation = false;
            for (int operationIndex = 0; operationIndex < operations.Count; operationIndex++)
            {
                MachineGunnerPreparedOperation operation = operations[operationIndex];
                if (operation.Kind == MachineGunnerProgramOperationKind.DrawCards ||
                    operation.Kind ==
                        MachineGunnerProgramOperationKind.DrawCardsByActiveStatusKinds ||
                    operation.Kind ==
                        MachineGunnerProgramOperationKind.DrawToHandLimitAfterPlayedCardDeparture ||
                    operation.Kind ==
                        MachineGunnerProgramOperationKind.ReplaceRemainingHandWithTemporaryCards)
                {
                    hasCardZoneMutationOperation = true;
                }
                if (operation.Kind ==
                    MachineGunnerProgramOperationKind.DrawToHandLimitAfterPlayedCardDeparture)
                {
                    if (handReplacementPlan != null)
                        throw new InvalidOperationException("单张机枪兵卡不能组合两种当前牌离手复合操作。");
                    if (preparedDrawPlan != null)
                        throw new InvalidOperationException("离手后抽牌不能与已冻结的普通抽牌组合。");
                    if (playedCardDeparturePlan != null)
                        throw new InvalidOperationException("单张机枪兵卡不能声明多次离手后抽牌操作。");

                    while (firstOperationToCommit < operationIndex)
                    {
                        AppendLocalResourceOperationBeforePlayedCardDeparture(
                            command.ActorId,
                            operations[firstOperationToCommit],
                            ref playerTurnAfter,
                            settlements);
                        firstOperationToCommit++;
                    }

                    playedCardDepartureOperationIndex = operationIndex;
                    playedCardDeparturePlan =
                        cardZones.PreparePlayedCardDepartureAndDrawToHandLimit(
                            command.CardId,
                            playedCardDestination,
                            operation.Value,
                            settlements.Count);
                    if (!cardZones.ValidatePreparedPlayedCardDepartureAndDraw(
                            playedCardDeparturePlan))
                    {
                        throw new InvalidOperationException(
                            "离手后抽牌计划在首次战斗写入前发生快照漂移。");
                    }

                    continue;
                }
                if (operation.Kind ==
                    MachineGunnerProgramOperationKind.DrawCardsByActiveStatusKinds)
                {
                    if (playedCardDeparturePlan != null || handReplacementPlan != null)
                        throw new InvalidOperationException("按状态种类抽牌不能与当前牌离手复合操作组合。");
                    if (preparedDrawPlan != null)
                        throw new InvalidOperationException("单张机枪兵卡不能声明多次冻结普通抽牌。");

                    int preparedDrawStartingOrder = DeterminePreparedDrawStartingOrder(
                        operations,
                        operationIndex,
                        settlements.Count);
                    preparedDrawOperationIndex = operationIndex;
                    preparedDrawPlan = cardZones.PrepareDraw(
                        operation.Value,
                        preparedDrawStartingOrder,
                        GetHandLimit(command.ActorId));
                    if (!cardZones.ValidatePreparedDraw(preparedDrawPlan))
                    {
                        throw new InvalidOperationException(
                            "按状态种类抽牌计划在首次战斗写入前发生快照漂移。");
                    }

                    continue;
                }
                if (operation.Kind !=
                    MachineGunnerProgramOperationKind.ReplaceRemainingHandWithTemporaryCards)
                {
                    continue;
                }
                if (playedCardDeparturePlan != null)
                    throw new InvalidOperationException("单张机枪兵卡不能组合两种当前牌离手复合操作。");
                if (preparedDrawPlan != null)
                    throw new InvalidOperationException("换手创建临时牌不能与已冻结的普通抽牌组合。");
                if (handReplacementPlan != null)
                    throw new InvalidOperationException("单张机枪兵卡不能声明多次离手弃牌并创建操作。");

                int handReplacementStartingOrder =
                    DetermineHandReplacementStartingOrder(
                        operations,
                        operationIndex,
                        settlements.Count);
                handReplacementOperationIndex = operationIndex;
                handReplacementPlan =
                    cardZones.PreparePlayedCardDepartureDiscardHandAndCreate(
                        command.CardId,
                        playedCardDestination,
                        operation.Value,
                        handReplacementStartingOrder);
                if (!cardZones.ValidatePreparedPlayedCardDepartureDiscardHandAndCreate(
                        handReplacementPlan))
                {
                    throw new InvalidOperationException(
                        "离手弃牌并创建临时牌计划在首次战斗写入前发生快照漂移。");
                }
            }

            BattlePreparedPlayedCardDeparture ordinaryPlayedCardDeparturePlan = null;
            if (!hasCardZoneMutationOperation &&
                ventHeatSelectionPlan == null &&
                playedCardDeparturePlan == null &&
                handReplacementPlan == null)
            {
                ordinaryPlayedCardDeparturePlan = cardZones.PreparePlayedCardDeparture(
                    command.CardId,
                    playedCardDestination);
            }

            if (!ValidatePreparedCost(preparedCost, playerTurn))
            {
                throw new InvalidOperationException(
                    "机枪兵费用计划在首次战斗写入前发生快照漂移。");
            }
            if (unstoppableTriggerPlan != null &&
                !settlementTriggerEngine.ValidatePrepared(unstoppableTriggerPlan))
            {
                throw new InvalidOperationException(
                    "势不可挡结算触发注册在首次战斗写入前发生快照漂移。");
            }
            if (!ValidatePreparedPoisonApplications(command.ActorId, operations))
            {
                throw new InvalidOperationException(
                    "二手烟计划在首次战斗写入前发生烟雾或通用状态快照漂移。");
            }
            if (ordinaryPlayedCardDeparturePlan != null &&
                !cardZones.ValidatePreparedPlayedCardDeparture(
                    ordinaryPlayedCardDeparturePlan))
            {
                throw new InvalidOperationException(
                    "普通离手计划在首次战斗写入前发生布局快照漂移。");
            }
            if (triggeredHandAttackPlan != null)
                triggeredCardPlayExecution.ValidatePrepared(triggeredHandAttackPlan);
            if (garrisonRetentionPlan != null)
            {
                if (!blockRetention.ValidatePrepared(garrisonRetentionPlan))
                    throw new InvalidOperationException("驻防格挡保留计划在首次战斗写入前发生快照漂移。");
                if (!IsGarrisonSelectionSnapshotCurrent(
                        command,
                        cardZones,
                        garrisonRetainedCardIds))
                {
                    throw new InvalidOperationException("驻防保留手牌快照在首次战斗写入前发生漂移。");
                }
            }
            if (repeatedDamagePlan != null)
            {
                repeatedDamageExecutor.ValidatePrepared(
                    repeatedDamagePlan,
                    settlements.Count);
            }

            if (repeatedDamagePlan != null)
            {
                IReadOnlyList<BattleSettlementRecord> repeatedSettlements =
                    repeatedDamageExecutor.CommitPrepared(repeatedDamagePlan);
                foreach (BattleSettlementRecord settlement in repeatedSettlements)
                    settlements.Add(settlement);
            }

            bool playedCardDestinationCommitted = false;
            for (int operationIndex = firstOperationToCommit;
                 operationIndex < operations.Count;
                 operationIndex++)
            {
                MachineGunnerPreparedOperation operation = operations[operationIndex];
                switch (operation.Kind)
                {
                    case MachineGunnerProgramOperationKind.Damage:
                        CommitDamageOperation(command.ActorId, operation, settlements);
                        break;
                    case MachineGunnerProgramOperationKind.GainBlock:
                        CommitBlockOperation(command.ActorId, operation, settlements);
                        break;
                    case MachineGunnerProgramOperationKind.GainEnergy:
                        AppendEnergyGainSettlement(
                            command.ActorId,
                            operation.Value,
                            ref playerTurnAfter,
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.GainAmmo:
                        AppendAmmoGainSettlement(
                            command.ActorId,
                            operation.Value,
                            ref playerTurnAfter,
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.SpendAmmo:
                        AppendAmmoSpendSettlement(
                            command.ActorId,
                            operation.Value,
                            ref playerTurnAfter,
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.FillAmmo:
                        AppendAmmoFillSettlement(
                            command.ActorId,
                            ref playerTurnAfter,
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.DrawCards:
                        AppendCardZoneResult(
                            cardZones.Draw(
                                operation.Value,
                                settlements.Count,
                                GetHandLimit(command.ActorId)),
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.DrawCardsByActiveStatusKinds:
                        if (preparedDrawPlan == null ||
                            operationIndex != preparedDrawOperationIndex)
                        {
                            throw new InvalidOperationException(
                                "按状态种类抽牌操作缺少唯一冻结计划。");
                        }

                        AppendCardZoneResult(
                            cardZones.CommitPreparedDraw(preparedDrawPlan),
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.DrawToHandLimitAfterPlayedCardDeparture:
                        if (playedCardDeparturePlan == null ||
                            operationIndex != playedCardDepartureOperationIndex)
                        {
                            throw new InvalidOperationException("离手后抽牌操作缺少唯一冻结计划。");
                        }

                        AppendCardZoneResult(
                            cardZones.CommitPreparedPlayedCardDepartureAndDraw(
                                playedCardDeparturePlan),
                            settlements);
                        playedCardDestinationCommitted = true;
                        break;
                    case MachineGunnerProgramOperationKind.ReplaceRemainingHandWithTemporaryCards:
                        if (handReplacementPlan == null ||
                            operationIndex != handReplacementOperationIndex)
                        {
                            throw new InvalidOperationException(
                                "离手弃牌并创建临时牌操作缺少唯一冻结计划。");
                        }

                        AppendCardZoneResult(
                            cardZones.CommitPreparedPlayedCardDepartureDiscardHandAndCreate(
                                handReplacementPlan),
                            settlements);
                        playedCardDestinationCommitted = true;
                        break;
                    case MachineGunnerProgramOperationKind.AddStimTurns:
                        _stimTurns = checked(_stimTurns + operation.Value);
                        break;
                    case MachineGunnerProgramOperationKind.ApplyPrivateStatus:
                        CommitPrivateStatusOperation(command.ActorId, operation, settlements);
                        break;
                    case MachineGunnerProgramOperationKind.ApplyPrivateStatusFromSpentAmmo:
                        CommitPrivateStatusOperation(command.ActorId, operation, settlements);
                        break;
                    case MachineGunnerProgramOperationKind.ApplyBurn:
                        CommitBurnOperation(command.ActorId, operation, settlements);
                        break;
                    case MachineGunnerProgramOperationKind.ConvertSourceSmokeToTargetBurn:
                        CommitSourceSmokeToTargetBurnOperation(
                            command.ActorId,
                            operation,
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.ApplyVulnerable:
                        CommitVulnerableOperation(command.ActorId, operation, settlements);
                        break;
                    case MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke:
                        CommitPoisonFromSourceSmokeOperation(
                            command.ActorId,
                            operation,
                            settlements);
                        break;
                    case MachineGunnerProgramOperationKind.ResolveIncompleteCombustion:
                        CommitIncompleteCombustionOperation(command.ActorId, operation, settlements);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation.Kind));
                }
            }

            if (garrisonRetentionPlan != null)
            {
                BattleBlockRetentionSnapshot retentionAfter =
                    blockRetention.CommitPrepared(garrisonRetentionPlan);
                settlements.Add(new BattleStatusAppliedSettlement(
                    settlements.Count,
                    effectId: null,
                    command.ActorId,
                    command.ActorId,
                    BattleStatusType.Garrison,
                    garrisonRetentionPlan.Before.TimedRounds,
                    retentionAfter.TimedRounds));
            }

            if (program.PowerKind.HasValue)
            {
                CommitPowerActivation(
                    command.ActorId,
                    program.PowerKind.Value,
                    program.PowerStackGain,
                    ref playerTurnAfter);
            }
            if (unstoppableTriggerPlan != null)
            {
                BattleSettlementTriggerRegistrationOutcome registration =
                    settlementTriggerEngine.CommitPrepared(unstoppableTriggerPlan);
                settlements.Add(new BattleStatusAppliedSettlement(
                    settlements.Count,
                    effectId: null,
                    command.ActorId,
                    command.ActorId,
                    BattleStatusType.FatalOrBlockBreakCardTrigger,
                    registration.ValueBefore,
                    registration.ValueAfter));
            }
            if (scheduledCreationPlan != null)
            {
                MachineGunnerScheduledEffectInstance instance =
                    _scheduledEffects.CommitCreation(scheduledCreationPlan);
                settlements.Add(new MachineGunnerScheduledEffectChangedSettlement(
                    settlements.Count,
                    command.ActorId,
                    instance,
                    MachineGunnerScheduledEffectChangeKind.Created,
                    remainingBefore: 0,
                    remainingAfter: GetScheduledRemaining(instance)));
            }

            if (ventHeatSelectionPlan != null)
            {
                BattleCardZoneOperationResult selectionResult =
                    cardZones.CommitPreparedHandCardSelectionResolution(
                        ventHeatSelectionPlan);
                if (!selectionResult.Succeeded || selectionResult.Settlements.Count != 2)
                    throw new InvalidOperationException("排气散热手牌选择计划未返回两段冻结移动记录。");

                AppendCardZoneSettlement(selectionResult.Settlements[0], settlements);
                AppendEnergyGainSettlement(
                    command.ActorId,
                    requestedGain: 1,
                    ref playerTurnAfter,
                    settlements);
                AppendCardZoneSettlement(selectionResult.Settlements[1], settlements);
                playedCardDestinationCommitted = true;
            }

            if (!playedCardDestinationCommitted)
            {
                if (ordinaryPlayedCardDeparturePlan != null)
                {
                    AppendCardZoneResult(
                        cardZones.CommitPreparedPlayedCardDeparture(
                            ordinaryPlayedCardDeparturePlan,
                            settlements.Count),
                        settlements);
                }
                else
                {
                    BattleCardZoneOperationResult destinationResult;
                    switch (playedCardDestination)
                    {
                        case BattleCardZone.PowerPile:
                            destinationResult = cardZones.MoveToPowerFromHand(
                                command.CardId,
                                settlements.Count);
                            break;
                        case BattleCardZone.ExhaustPile:
                            destinationResult = cardZones.ExhaustFromHand(
                                command.CardId,
                                settlements.Count);
                            break;
                        case BattleCardZone.DiscardPile:
                            destinationResult = cardZones.DiscardFromHand(
                                command.CardId,
                                settlements.Count);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(playedCardDestination));
                    }

                    AppendCardZoneResult(destinationResult, settlements);
                }
            }
            BattleTriggeredCardPlayRequest triggeredCardPlayRequest =
                triggeredHandAttackPlan == null
                    ? null
                    : triggeredCardPlayExecution.CommitPrepared(triggeredHandAttackPlan);
            if (garrisonRetainedCardIds != null)
            {
                _retainedCardIdsForActionEnd =
                    new ReadOnlyCollection<CardInstanceId>(
                        new List<CardInstanceId>(garrisonRetainedCardIds));
            }
            CommitSuccessfulCardCostLifecycle(preparedCost);
            ReduceInvisibleAfterSuccessfulAttackIfNeeded(command.ActorId, program);
            _cardRandom.State = candidateRandom.State;
            RecordSuccessfulCard(program);
            return MachineGunnerCardProgramExecutionResult.SucceededWith(
                playerTurnAfter,
                settlements,
                program.EndsPlayerActionAfterSuccessfulPlay,
                triggeredCardPlayRequest);
        }

        /// <summary>仅在攻击牌已成功进入卡区归宿且未声明保留隐身时消耗玩家一层；失败出牌绝不触发该生命周期规则。</summary>
        private void ReduceInvisibleAfterSuccessfulAttackIfNeeded(
            CombatantId playerId,
            MachineGunnerCardProgram program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (!SupportsPlayer(playerId) || !program.IsAttack ||
                program.PreservesInvisibleAfterSuccessfulAttack)
                return;
            if (_combatState.Get(playerId, MachineGunnerCombatantStatus.Invisible) <= 0)
                return;

            _combatState.ReduceDuration(
                playerId,
                MachineGunnerCombatantStatus.Invisible);
        }

        /// <summary>提交一张已完成支付的能力牌，将资源上限变动和职业私有持续状态保留在同一场机枪兵会话内。</summary>
        private void CommitPowerActivation(
            CombatantId playerId,
            MachineGunnerPowerKind powerKind,
            int powerStackGain,
            ref PlayerTurnData playerTurnAfter)
        {
            if (!SupportsPlayer(playerId))
                throw new InvalidOperationException("只有已绑定的机枪兵可以激活机枪兵能力牌。");
            if (powerStackGain <= 0)
                throw new ArgumentOutOfRangeException(nameof(powerStackGain));

            int stacksBefore = GetPowerStack(powerKind);
            _powerStacks[powerKind] = checked(stacksBefore + powerStackGain);
            switch (powerKind)
            {
                case MachineGunnerPowerKind.CoreExpansion:
                    playerTurnAfter = playerTurnAfter.WithResources(
                        playerTurnAfter.Energy,
                        checked(playerTurnAfter.EnergyMaximum + 1),
                        playerTurnAfter.EnergyGainPerRound,
                        playerTurnAfter.Ammo,
                        playerTurnAfter.AmmoMaximum,
                        playerTurnAfter.AmmoGainPerRound);
                    break;
                case MachineGunnerPowerKind.OutputAdjust:
                    playerTurnAfter = playerTurnAfter.WithResources(
                        playerTurnAfter.Energy,
                        Math.Max(0, playerTurnAfter.EnergyMaximum - 1),
                        checked(playerTurnAfter.EnergyGainPerRound + 1),
                        playerTurnAfter.Ammo,
                        playerTurnAfter.AmmoMaximum,
                        playerTurnAfter.AmmoGainPerRound);
                    break;
                case MachineGunnerPowerKind.BlastShield:
                    _combatState.Add(
                        playerId,
                        MachineGunnerCombatantStatus.Armor,
                        amount: 6);
                    break;
                case MachineGunnerPowerKind.MagExpansion:
                    playerTurnAfter = playerTurnAfter.WithResources(
                        playerTurnAfter.Energy,
                        playerTurnAfter.EnergyMaximum,
                        playerTurnAfter.EnergyGainPerRound,
                        playerTurnAfter.Ammo,
                        checked(playerTurnAfter.AmmoMaximum + 3),
                        playerTurnAfter.AmmoGainPerRound);
                    break;
                case MachineGunnerPowerKind.PrivateMod:
                    playerTurnAfter = playerTurnAfter.WithResources(
                        playerTurnAfter.Energy,
                        playerTurnAfter.EnergyMaximum,
                        playerTurnAfter.EnergyGainPerRound,
                        playerTurnAfter.Ammo,
                        checked(playerTurnAfter.AmmoMaximum + 1),
                        playerTurnAfter.AmmoGainPerRound);
                    break;
                case MachineGunnerPowerKind.SmokePersist:
                case MachineGunnerPowerKind.PowerOverclock:
                case MachineGunnerPowerKind.KungfuMech:
                case MachineGunnerPowerKind.IncendiaryAmmo:
                case MachineGunnerPowerKind.AgedOil:
                case MachineGunnerPowerKind.BurningOil:
                case MachineGunnerPowerKind.GuerrillaTactics:
                case MachineGunnerPowerKind.ElectroBoost:
                case MachineGunnerPowerKind.Bombard:
                case MachineGunnerPowerKind.SkyWrath:
                case MachineGunnerPowerKind.PortableHelper:
                case MachineGunnerPowerKind.Unstoppable:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(powerKind));
            }
        }

        /// <summary>按静态表顺序冻结当前已实现、非射击且可由共享目标策略自动打出的攻击候选。</summary>
        private static IReadOnlyList<int> CollectUnstoppableCandidateTemplateIds(
            cfg.Tables tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            var templateIds = new List<int>();
            foreach (cfg.battle.Card template in tables.TbCard.DataList)
            {
                if (template.ImplementationStatus !=
                        cfg.battle.CardImplementationStatus.Implemented ||
                    template.CardType != cfg.battle.CardType.Attack ||
                    !SupportsTriggeredCardTargetRule(template.TargetRule))
                {
                    continue;
                }

                if (template.ProgramId == cfg.battle.MachineGunnerProgramId.None)
                {
                    templateIds.Add(template.Id);
                    continue;
                }
                if (MachineGunnerCardProgramRegistry.TryGet(
                        template.ProgramId,
                        out MachineGunnerCardProgram candidateProgram) &&
                    !candidateProgram.IsShootCategory)
                {
                    templateIds.Add(template.Id);
                }
            }

            return new ReadOnlyCollection<int>(templateIds);
        }

        /// <summary>确认共享临时出牌能为候选模板冻结一个无需玩家交互的目标。</summary>
        private static bool SupportsTriggeredCardTargetRule(cfg.battle.TargetRule targetRule)
        {
            return targetRule == cfg.battle.TargetRule.Self ||
                   targetRule == cfg.battle.TargetRule.Enemy ||
                   targetRule == cfg.battle.TargetRule.RandomEnemy ||
                   targetRule == cfg.battle.TargetRule.AllEnemies;
        }

        /// <summary>在卡区归宿成功后记录本回合最近一张成功卡的最小分类，失败或未归宿的卡绝不污染连肘折扣。</summary>
        private void RecordSuccessfulCard(MachineGunnerCardProgram program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));

            if (!program.IsAttack)
                _recentSuccessfulCardCategory = MachineGunnerRecentSuccessfulCardCategory.Other;
            else if (program.IsShootCategory)
                _recentSuccessfulCardCategory = MachineGunnerRecentSuccessfulCardCategory.ShootAttack;
            else if (program.ParticipatesInNonShootAttackSynergies)
                _recentSuccessfulCardCategory = MachineGunnerRecentSuccessfulCardCategory.NonShootAttack;
            else
                _recentSuccessfulCardCategory = MachineGunnerRecentSuccessfulCardCategory.OtherAttack;
        }

        /// <summary>判断父牌开始执行时冻结的上一张成功牌是否属于任意攻击或射击分类。</summary>
        private bool IsPreviousSuccessfulCardAttackOrShoot()
        {
            return _recentSuccessfulCardCategory ==
                       MachineGunnerRecentSuccessfulCardCategory.NonShootAttack ||
                   _recentSuccessfulCardCategory ==
                       MachineGunnerRecentSuccessfulCardCategory.ShootAttack ||
                   _recentSuccessfulCardCategory ==
                       MachineGunnerRecentSuccessfulCardCategory.OtherAttack;
        }

        /// <summary>释放职业私有状态；参与者与卡区仍由所属 Session 负责释放。</summary>
        public void Dispose()
        {
            _stimTurns = 0;
            _recentSuccessfulCardCategory = MachineGunnerRecentSuccessfulCardCategory.None;
            _nextAttackFree = false;
            _retainedCardIdsForActionEnd = Array.Empty<CardInstanceId>();
            unchecked
            {
                _costModifierRevision++;
            }
            _powerStacks.Clear();
            _scheduledEffects.Clear();
        }

        /// <summary>为通用 Effect 创建独占的机枪兵伤害预演序列，缓冲等一次性防御不会在未提交链中写回真实状态。</summary>
        IBattleDamageFormulaOverrideSequence IBattleDamageFormulaOverride.CreateSequence()
        {
            return new MachineGunnerDamageFormulaSequence(this);
        }

        /// <summary>按机枪兵私有状态计算通用 Effect 的单段攻击伤害，但不在预构建阶段写入任何战斗事实。</summary>
        private BattleDamageFormulaOutcome CalculateGenericAttackDamage(
            CombatantData source,
            int sourceStrength,
            CombatantId targetId,
            int configuredValue,
            BattleEffectTargetSnapshot target)
        {
            return MachineGunnerDamagePipeline.Calculate(
                new MachineGunnerDamageRequest(
                    source.Id,
                    targetId,
                    configuredValue,
                    MachineGunnerDamageKind.Attack),
                sourceStrength,
                target,
                _combatState).Outcome;
        }

        /// <summary>只读预览固定或 X 能量支付，以及本张牌实际能支付的弹药和兴奋剂额外段；规则层与队首提交共用此唯一成本口径。</summary>
        internal bool TryPreviewCost(
            cfg.battle.Card cardTemplate,
            PlayerTurnData playerTurn,
            MachineGunnerCardProgram program,
            out MachineGunnerCostResolution cost,
            out BattleCommandExecutionFailureReason failureReason)
        {
            return TryResolveCost(
                cardTemplate,
                cardTemplate.Cost,
                playerTurn,
                program,
                prismaticInitialStatusKindCount: null,
                BattleCardPaymentMode.Normal,
                out cost,
                out failureReason);
        }

        /// <summary>按实例等级投影预览费用，其余职业减免、X 费和弹药语义保持同一口径。</summary>
        internal bool TryPreviewCost(
            BattleCardLevelProjection cardLevelProjection,
            PlayerTurnData playerTurn,
            MachineGunnerCardProgram program,
            out MachineGunnerCostResolution cost,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (cardLevelProjection == null)
                throw new ArgumentNullException(nameof(cardLevelProjection));

            return TryResolveCost(
                cardLevelProjection.Template,
                cardLevelProjection.Cost,
                playerTurn,
                program,
                prismaticInitialStatusKindCount: null,
                BattleCardPaymentMode.Normal,
                out cost,
                out failureReason);
        }

        /// <summary>当显式目标已知时按其命令起始状态种类精确预览动态弹药费用。</summary>
        internal bool TryPreviewCost(
            cfg.battle.Card cardTemplate,
            PlayerTurnData playerTurn,
            MachineGunnerCardProgram program,
            CombatantId targetId,
            out MachineGunnerCostResolution cost,
            out BattleCommandExecutionFailureReason failureReason)
        {
            int? initialStatusKindCount = program.ExecutionKind ==
                MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds
                    ? CountInitialActiveStatusKindsForCombatant(targetId)
                    : (int?)null;
            return TryResolveCost(
                cardTemplate,
                cardTemplate.Cost,
                playerTurn,
                program,
                initialStatusKindCount,
                BattleCardPaymentMode.Normal,
                out cost,
                out failureReason);
        }

        /// <summary>按实例等级投影和显式目标起始状态精确预览动态职业费用。</summary>
        internal bool TryPreviewCost(
            BattleCardLevelProjection cardLevelProjection,
            PlayerTurnData playerTurn,
            MachineGunnerCardProgram program,
            CombatantId targetId,
            out MachineGunnerCostResolution cost,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (cardLevelProjection == null)
                throw new ArgumentNullException(nameof(cardLevelProjection));

            int? initialStatusKindCount = program.ExecutionKind ==
                MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds
                    ? CountInitialActiveStatusKindsForCombatant(targetId)
                    : (int?)null;
            return TryResolveCost(
                cardLevelProjection.Template,
                cardLevelProjection.Cost,
                playerTurn,
                program,
                initialStatusKindCount,
                BattleCardPaymentMode.Normal,
                out cost,
                out failureReason);
        }

        /// <summary>在命令首次写入前冻结费用、连肘分类和免攻版本，后续提交只接受这份计划。</summary>
        internal bool TryPrepareCost(
            cfg.battle.Card cardTemplate,
            PlayerTurnData playerTurn,
            MachineGunnerCardProgram program,
            out MachineGunnerPreparedCost plan,
            out BattleCommandExecutionFailureReason failureReason)
        {
            return TryPrepareCost(
                cardTemplate,
                cardTemplate.Cost,
                playerTurn,
                program,
                prismaticInitialStatusKindCount: null,
                BattleCardPaymentMode.Normal,
                out plan,
                out failureReason);
        }

        /// <summary>使用可选目标起始状态种类冻结动态弹药费用，其余程序沿用既有费用口径。</summary>
        private bool TryPrepareCost(
            cfg.battle.Card cardTemplate,
            int resolvedCardCost,
            PlayerTurnData playerTurn,
            MachineGunnerCardProgram program,
            int? prismaticInitialStatusKindCount,
            BattleCardPaymentMode requestedPaymentMode,
            out MachineGunnerPreparedCost plan,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (!TryResolveCost(
                    cardTemplate,
                    resolvedCardCost,
                    playerTurn,
                    program,
                    prismaticInitialStatusKindCount,
                    requestedPaymentMode,
                    out MachineGunnerCostResolution resolution,
                    out failureReason))
            {
                plan = null;
                return false;
            }

            bool consumesNextAttackFree = _nextAttackFree && program.IsAttack;
            plan = new MachineGunnerPreparedCost(
                this,
                playerTurn,
                program,
                _recentSuccessfulCardCategory,
                _nextAttackFree,
                _costModifierRevision,
                resolution,
                consumesNextAttackFree);
            return true;
        }

        /// <summary>只读确认费用计划仍属于本运行时，且资源、连肘分类、免攻版本和一次性状态均未漂移。</summary>
        internal bool ValidatePreparedCost(
            MachineGunnerPreparedCost plan,
            PlayerTurnData currentPlayerTurn)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (currentPlayerTurn == null)
                throw new ArgumentNullException(nameof(currentPlayerTurn));

            return ReferenceEquals(plan.Owner, this) &&
                   ReferenceEquals(plan.InitialPlayerTurn, currentPlayerTurn) &&
                   !plan.IsCommitted &&
                   plan.RecentSuccessfulCardCategoryBefore ==
                   _recentSuccessfulCardCategory &&
                   plan.NextAttackFreeBefore == _nextAttackFree &&
                   plan.CostModifierRevisionBefore == _costModifierRevision &&
                   plan.ConsumesNextAttackFreeOnSuccess ==
                   (plan.NextAttackFreeBefore && plan.Program.IsAttack);
        }

        /// <summary>在卡牌成功归宿后一次性消费旧免攻并应用本张授予，二进制刷新不叠层。</summary>
        private void CommitSuccessfulCardCostLifecycle(MachineGunnerPreparedCost plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ValidatePreparedCost(plan, plan.InitialPlayerTurn))
            {
                throw new InvalidOperationException(
                    "机枪兵费用生命周期计划已提交或其权威快照已经漂移。");
            }

            bool hasLifecycleMutation =
                plan.ConsumesNextAttackFreeOnSuccess ||
                plan.Program.GrantsNextAttackFreeOnSuccess;
            if (plan.ConsumesNextAttackFreeOnSuccess)
                _nextAttackFree = false;
            if (plan.Program.GrantsNextAttackFreeOnSuccess)
                _nextAttackFree = true;
            if (hasLifecycleMutation)
            {
                unchecked
                {
                    _costModifierRevision++;
                }
            }

            plan.MarkCommitted();
        }

        /// <summary>按同一命令开始快照解析能量、弹药、兴奋剂和游击名义费用，不写入任何运行时事实。</summary>
        private bool TryResolveCost(
            cfg.battle.Card cardTemplate,
            int resolvedCardCost,
            PlayerTurnData playerTurn,
            MachineGunnerCardProgram program,
            int? prismaticInitialStatusKindCount,
            BattleCardPaymentMode requestedPaymentMode,
            out MachineGunnerCostResolution cost,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (cardTemplate == null)
                throw new ArgumentNullException(nameof(cardTemplate));
            if (playerTurn == null)
                throw new ArgumentNullException(nameof(playerTurn));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (resolvedCardCost < 0 && cardTemplate.CostKind == cfg.battle.CardCostKind.Fixed)
                throw new ArgumentOutOfRangeException(nameof(resolvedCardCost));

            bool waivedByNextAttack = _nextAttackFree && program.IsAttack;
            bool waivedByRecentNonShootAttack =
                program.IsFreeAfterPreviousNonShootAttack &&
                _recentSuccessfulCardCategory ==
                MachineGunnerRecentSuccessfulCardCategory.NonShootAttack;
            bool resourcesWaived = requestedPaymentMode == BattleCardPaymentMode.Waived ||
                waivedByNextAttack || waivedByRecentNonShootAttack;
            BattleCardPaymentMode energyPaymentMode =
                resourcesWaived
                    ? BattleCardPaymentMode.Waived
                    : BattleCardPaymentMode.Normal;
            if (!BattleCardCostResolver.TryResolveEnergy(
                    cardTemplate.CostKind,
                    resolvedCardCost,
                    playerTurn.Energy,
                    energyPaymentMode,
                    out BattleCardEnergyCostResolution energyCost,
                    out failureReason))
            {
                cost = default;
                return false;
            }

            int ammoEffectValue;
            int actualAmmoSpent;
            int stimBonusHitCount = 0;
            MachineGunnerPreparedReloadedVolley reloadedVolley = null;
            if (program.ExecutionKind == MachineGunnerProgramExecutionKind.ReloadedAmmoVolley)
            {
                reloadedVolley = MachineGunnerReloadedVolleyResolver.Prepare(
                    playerTurn.Ammo,
                    playerTurn.AmmoMaximum,
                    ReloadedVolleyWaveShotLimit,
                    CanReceiveStimBonus(program),
                    resourcesWaived
                        ? BattleCardPaymentMode.Waived
                        : BattleCardPaymentMode.Normal);
                actualAmmoSpent = 0;
                ammoEffectValue = checked(
                    reloadedVolley.FirstWaveEffectShotCount +
                    reloadedVolley.SecondWaveEffectShotCount);
                stimBonusHitCount = reloadedVolley.StimBonusShotCount;
            }
            else if (program.ExecutionKind ==
                MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds)
            {
                if (program.TargetInputMode != MachineGunnerTargetInputMode.ExplicitEnemy)
                    throw new InvalidOperationException("按目标状态展开的射击缺少显式敌方目标规则。");

                int logicalHitCount = 1;
                if (CanReceiveStimBonus(program))
                {
                    logicalHitCount = prismaticInitialStatusKindCount.HasValue
                        ? checked(1 + prismaticInitialStatusKindCount.Value)
                        : 1;
                    stimBonusHitCount = logicalHitCount;
                }

                ammoEffectValue = checked(program.BaseAmmoCost + stimBonusHitCount);
                actualAmmoSpent = resourcesWaived ? 0 : ammoEffectValue;
            }
            else
            {
                switch (program.AmmoSpendMode)
                {
                    case MachineGunnerAmmoSpendMode.None:
                        ammoEffectValue = 0;
                        break;
                    case MachineGunnerAmmoSpendMode.Fixed:
                        ammoEffectValue = program.BaseAmmoCost;
                        if (CanReceiveStimBonus(program) &&
                            (resourcesWaived ||
                             playerTurn.Ammo >= program.BaseAmmoCost + 1))
                        {
                            ammoEffectValue++;
                            stimBonusHitCount = 1;
                        }

                        break;
                    case MachineGunnerAmmoSpendMode.UpToLimit:
                        ammoEffectValue = Math.Min(playerTurn.Ammo, program.MaximumAmmoSpend);
                        if (resourcesWaived)
                        {
                            ammoEffectValue = Math.Max(
                                program.BaseAmmoCost,
                                ammoEffectValue);
                        }
                        else if (ammoEffectValue < program.BaseAmmoCost)
                        {
                            cost = default;
                            failureReason = BattleCommandExecutionFailureReason.InsufficientAmmo;
                            return false;
                        }
                        if (CanReceiveStimBonus(program) &&
                            (resourcesWaived || playerTurn.Ammo > ammoEffectValue))
                        {
                            ammoEffectValue++;
                            stimBonusHitCount = 1;
                        }

                        break;
                    case MachineGunnerAmmoSpendMode.AllAvailable:
                        ammoEffectValue = playerTurn.Ammo;
                        if (CanReceiveStimBonus(program))
                            stimBonusHitCount = 1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(program.AmmoSpendMode));
                }

                actualAmmoSpent = resourcesWaived ? 0 : ammoEffectValue;
            }
            if (actualAmmoSpent > playerTurn.Ammo)
            {
                cost = default;
                failureReason = BattleCommandExecutionFailureReason.InsufficientAmmo;
                return false;
            }

            cost = new MachineGunnerCostResolution(
                energyCost,
                actualAmmoSpent,
                ammoEffectValue,
                reloadedVolley?.NominalAmmoSpentForTriggers ??
                    program.AmmoSpentForGuerrillaOverride ?? ammoEffectValue,
                stimBonusHitCount,
                reloadedVolley);
            failureReason = BattleCommandExecutionFailureReason.None;
            return true;
        }

        /// <summary>判断当前程序是否在本场兴奋剂持续期间获得一段额外射击；特殊全弹药程序可显式使用免费段。</summary>
        private bool CanReceiveStimBonus(MachineGunnerCardProgram program)
        {
            return _stimTurns > 0 && program.ReceivesStimBonus;
        }

        /// <summary>用状态赋值而非构造参数复制随机流，避免底层随机构造器初始化时重写冻结状态。</summary>
        private GameRandom CreateCardRandomCandidate()
        {
            var candidate = new GameRandom(1u);
            candidate.State = _cardRandom.State;
            return candidate;
        }

        /// <summary>解析玩家输入或稳定自动目标；随机逐段目标留给预演阶段按当时存活投影抽取。</summary>
        /// <summary>从当前手牌攻击池冻结一张随机子牌及其自动目标，并交由共享触发模块构造免支付请求。</summary>
        private bool TryPrepareRandomHandAttackContinuation(
            CombatantId actorId,
            CardInstanceId sourceCardId,
            BattleCardZonesData cardZones,
            cfg.Tables tables,
            GameRandom candidateRandom,
            BattleTriggeredCardPlayExecution triggeredCardPlayExecution,
            int parentDepth,
            out BattlePreparedTriggeredCardPlay plan,
            out BattleCommandExecutionFailureReason failureReason)
        {
            var candidates = new List<CardInstanceId>();
            foreach (CardInstanceId candidateId in cardZones.Hand)
            {
                if (candidateId == sourceCardId ||
                    !cardZones.TryGetCard(candidateId, out CardInstanceData candidateCard))
                {
                    continue;
                }

                cfg.battle.Card candidateTemplate =
                    tables.TbCard.GetOrDefault(candidateCard.TemplateId);
                if (candidateTemplate != null &&
                    candidateTemplate.CardType == cfg.battle.CardType.Attack &&
                    candidateTemplate.ImplementationStatus ==
                        cfg.battle.CardImplementationStatus.Implemented)
                {
                    candidates.Add(candidateId);
                }
            }

            if (candidates.Count == 0)
            {
                plan = null;
                failureReason = BattleCommandExecutionFailureReason.None;
                return true;
            }

            CardInstanceId selectedCardId = candidates[candidateRandom.NextInt(candidates.Count)];
            CardInstanceData selectedCard = cardZones.Cards[selectedCardId];
            cfg.battle.Card selectedTemplate = tables.TbCard.GetOrDefault(selectedCard.TemplateId);
            if (!TryResolveTriggeredAttackTarget(
                    actorId,
                    selectedTemplate,
                    candidateRandom,
                    out CombatantId? targetId,
                    out failureReason))
            {
                plan = null;
                return false;
            }

            plan = triggeredCardPlayExecution.PrepareHandCard(
                actorId,
                cardZones,
                selectedCardId,
                targetId,
                MapPlayedCardDestination(selectedTemplate.PlayDestination),
                parentDepth,
                out failureReason);
            return plan != null;
        }

        /// <summary>按子牌程序自己的目标协议冻结触发目标；显式敌人缺少玩家输入时使用职业卡牌随机流。</summary>
        private bool TryResolveTriggeredAttackTarget(
            CombatantId actorId,
            cfg.battle.Card template,
            GameRandom candidateRandom,
            out CombatantId? targetId,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            if (template.ProgramId != cfg.battle.MachineGunnerProgramId.None &&
                MachineGunnerCardProgramRegistry.TryGet(
                    template.ProgramId,
                    out MachineGunnerCardProgram childProgram))
            {
                if (childProgram.TargetInputMode == MachineGunnerTargetInputMode.Self)
                {
                    targetId = actorId;
                    failureReason = BattleCommandExecutionFailureReason.None;
                    return true;
                }
                if (childProgram.TargetInputMode != MachineGunnerTargetInputMode.ExplicitEnemy)
                {
                    targetId = null;
                    failureReason = BattleCommandExecutionFailureReason.None;
                    return true;
                }
            }

            switch (template.TargetRule)
            {
                case cfg.battle.TargetRule.Self:
                    targetId = actorId;
                    failureReason = BattleCommandExecutionFailureReason.None;
                    return true;
                case cfg.battle.TargetRule.RandomEnemy:
                case cfg.battle.TargetRule.AllEnemies:
                    targetId = null;
                    failureReason = BattleCommandExecutionFailureReason.None;
                    return true;
                case cfg.battle.TargetRule.Enemy:
                    MachineGunnerTargetSelectionResult selection = _targetSelector.Resolve(
                        MachineGunnerTargetSelectionMode.RandomLivingEnemy,
                        actorId,
                        selectedTargetId: null,
                        candidateRandom);
                    if (!selection.Succeeded)
                    {
                        targetId = null;
                        failureReason = selection.FailureReason;
                        return false;
                    }

                    targetId = selection.TargetIds[0];
                    failureReason = BattleCommandExecutionFailureReason.None;
                    return true;
                default:
                    targetId = null;
                    failureReason = BattleCommandExecutionFailureReason.UnsupportedTargetRule;
                    return false;
            }
        }

        /// <summary>解析玩家输入或稳定自动目标；随机逐段目标留给预演阶段按当时存活投影抽取。</summary>
        private bool TryResolveTargets(
            PlayCardCommand command,
            MachineGunnerCardProgram program,
            GameRandom candidateRandom,
            out IReadOnlyList<CombatantId> targetIds,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (candidateRandom == null)
                throw new ArgumentNullException(nameof(candidateRandom));

            if (program.TargetInputMode == MachineGunnerTargetInputMode.RandomLivingEnemy)
            {
                if (command.TargetId.HasValue)
                {
                    targetIds = Array.Empty<CombatantId>();
                    failureReason = BattleCommandExecutionFailureReason.TargetRuleMismatch;
                    return false;
                }
                if (_targetSelector.GetLivingEnemiesInEncounterOrder().Count == 0)
                {
                    targetIds = Array.Empty<CombatantId>();
                    failureReason = BattleCommandExecutionFailureReason.TargetNotAlive;
                    return false;
                }

                targetIds = Array.Empty<CombatantId>();
                failureReason = BattleCommandExecutionFailureReason.None;
                return true;
            }

            MachineGunnerTargetSelectionResult selection = _targetSelector.Resolve(
                MapTargetInputMode(program.TargetInputMode),
                command.ActorId,
                command.TargetId,
                candidateRandom);
            if (!selection.Succeeded)
            {
                targetIds = Array.Empty<CombatantId>();
                failureReason = selection.FailureReason;
                return false;
            }

            targetIds = selection.TargetIds;
            failureReason = BattleCommandExecutionFailureReason.None;
            return true;
        }

        /// <summary>把首批程序的输入声明映射到可扩展的职业目标选择器，而不是复制 Encounter 遍历。</summary>
        private static MachineGunnerTargetSelectionMode MapTargetInputMode(
            MachineGunnerTargetInputMode targetInputMode)
        {
            switch (targetInputMode)
            {
                case MachineGunnerTargetInputMode.ExplicitEnemy:
                    return MachineGunnerTargetSelectionMode.PlayerSelectedEnemy;
                case MachineGunnerTargetInputMode.AutomaticNearestEnemy:
                    return MachineGunnerTargetSelectionMode.NearestLivingEnemy;
                case MachineGunnerTargetInputMode.AutomaticNearestTwoEnemies:
                    return MachineGunnerTargetSelectionMode.NearestTwoLivingEnemies;
                case MachineGunnerTargetInputMode.AutomaticFurthestEnemy:
                    return MachineGunnerTargetSelectionMode.FurthestLivingEnemy;
                case MachineGunnerTargetInputMode.AllLivingEnemies:
                    return MachineGunnerTargetSelectionMode.AllLivingEnemies;
                case MachineGunnerTargetInputMode.RandomLivingEnemy:
                    return MachineGunnerTargetSelectionMode.RandomLivingEnemy;
                case MachineGunnerTargetInputMode.Self:
                    return MachineGunnerTargetSelectionMode.Self;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetInputMode));
            }
        }

        /// <summary>以纯公式预演全部程序操作，任何异常都在首次战斗写入前转换为稳定失败。</summary>
        private bool TryPrepareOperations(
            CombatantId sourceId,
            CombatantData source,
            IReadOnlyList<CombatantId> targetIds,
            MachineGunnerCardProgram program,
            BattleCardLevelProjection cardLevelProjection,
            MachineGunnerCostResolution cost,
            GameRandom candidateRandom,
            out IReadOnlyList<MachineGunnerPreparedOperation> operations,
            out BattleCommandExecutionFailureReason failureReason)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (targetIds == null)
                throw new ArgumentNullException(nameof(targetIds));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (cardLevelProjection == null)
                throw new ArgumentNullException(nameof(cardLevelProjection));
            if (cardLevelProjection.Template.ProgramId != program.Id)
            {
                throw new InvalidOperationException(
                    "卡牌等级投影与机枪兵程序身份不一致。");
            }
            if (candidateRandom == null)
                throw new ArgumentNullException(nameof(candidateRandom));

            try
            {
                var projectedTargets = CreateProjectedCombatants();
                int simulatedSourceBlock = source.CurrentBlock;
                var prepared = new List<MachineGunnerPreparedOperation>();
                int orderedTargetDamageIndex = 0;
                foreach (MachineGunnerProgramOperation operation in program.Operations)
                {
                    switch (operation.Kind)
                    {
                        case MachineGunnerProgramOperationKind.Damage:
                            if (program.ExecutionKind ==
                                MachineGunnerProgramExecutionKind.ReloadedAmmoVolley)
                            {
                                AppendPreparedReloadedAmmoVolley(
                                    sourceId,
                                    source,
                                    targetIds,
                                    program,
                                    operation,
                                    cost,
                                    projectedTargets,
                                    prepared);
                                break;
                            }
                            if (program.ExecutionKind ==
                                MachineGunnerProgramExecutionKind.OrderedTargetDamageOperations)
                            {
                                if (orderedTargetDamageIndex < targetIds.Count)
                                {
                                    AppendPreparedHitAndPostHitOperations(
                                        sourceId,
                                        source,
                                        targetIds[orderedTargetDamageIndex],
                                        program,
                                        operation,
                                        cost,
                                        projectedTargets,
                                        prepared);
                                }

                                orderedTargetDamageIndex++;
                                break;
                            }

                            int hitCount = DetermineDamageHitCount(program, cost);
                            if (program.TargetInputMode == MachineGunnerTargetInputMode.AllLivingEnemies)
                            {
                                for (int targetOrdinal = 0;
                                     targetOrdinal < targetIds.Count;
                                     targetOrdinal++)
                                {
                                    int? targetOrdinalDamage = program.ExecutionKind ==
                                        MachineGunnerProgramExecutionKind.LinearDamageByTargetOrdinal
                                            ? CalculateLinearTargetOrdinalDamage(
                                                operation.Value,
                                                targetOrdinal)
                                            : cardLevelProjection.ProgramDamageValue;
                                    AppendPreparedHitAndPostHitOperations(
                                        sourceId,
                                        source,
                                        targetIds[targetOrdinal],
                                        program,
                                        operation,
                                        cost,
                                        projectedTargets,
                                        prepared,
                                        targetOrdinalDamage);
                                }
                            }
                            else
                            {
                                for (int index = 0; index < hitCount; index++)
                                {
                                    if (!TryResolvePreparedDamageTarget(
                                            program,
                                            targetIds,
                                            projectedTargets,
                                            candidateRandom,
                                            out CombatantId targetId))
                                    {
                                        break;
                                    }

                                    bool appended = AppendPreparedHitAndPostHitOperations(
                                        sourceId,
                                        source,
                                        targetId,
                                        program,
                                        operation,
                                        cost,
                                        projectedTargets,
                                        prepared,
                                        cardLevelProjection.ProgramDamageValue);
                                    if (!appended &&
                                        program.TargetInputMode != MachineGunnerTargetInputMode.RandomLivingEnemy)
                                    {
                                        break;
                                    }
                                }
                            }

                            break;
                        case MachineGunnerProgramOperationKind.GainBlock:
                            int blockValue = DetermineOperationValue(program, operation.Value, cost);
                            if (blockValue <= 0)
                                break;
                            int blockBefore = simulatedSourceBlock;
                            simulatedSourceBlock = checked(simulatedSourceBlock + blockValue);
                            prepared.Add(new MachineGunnerPreparedOperation(
                                operation.Kind,
                                blockValue,
                                Array.Empty<MachineGunnerPreparedDamage>(),
                                blockBefore,
                                simulatedSourceBlock));
                            break;
                        case MachineGunnerProgramOperationKind.GainEnergy:
                            int energyValue = DetermineOperationValue(program, operation.Value, cost);
                            if (energyValue <= 0)
                                break;
                            prepared.Add(new MachineGunnerPreparedOperation(
                                operation.Kind,
                                energyValue,
                                Array.Empty<MachineGunnerPreparedDamage>(),
                                blockBefore: 0,
                                blockAfter: 0));
                            break;
                        case MachineGunnerProgramOperationKind.GainAmmo:
                            int ammoValue = DetermineOperationValue(program, operation.Value, cost);
                            if (ammoValue <= 0)
                                break;
                            prepared.Add(new MachineGunnerPreparedOperation(
                                operation.Kind,
                                ammoValue,
                                Array.Empty<MachineGunnerPreparedDamage>(),
                                blockBefore: 0,
                                blockAfter: 0));
                            break;
                        case MachineGunnerProgramOperationKind.FillAmmo:
                        case MachineGunnerProgramOperationKind.DrawCards:
                        case MachineGunnerProgramOperationKind.DrawToHandLimitAfterPlayedCardDeparture:
                        case MachineGunnerProgramOperationKind.ReplaceRemainingHandWithTemporaryCards:
                        case MachineGunnerProgramOperationKind.AddStimTurns:
                            prepared.Add(new MachineGunnerPreparedOperation(
                                operation.Kind,
                                operation.Value,
                                Array.Empty<MachineGunnerPreparedDamage>(),
                                blockBefore: 0,
                                blockAfter: 0));
                            break;
                        case MachineGunnerProgramOperationKind.DrawCardsByActiveStatusKinds:
                            int activeStatusKindCount = CountInitialActiveStatusKinds(
                                operation.TargetScope,
                                sourceId,
                                targetIds);
                            prepared.Add(new MachineGunnerPreparedOperation(
                                operation.Kind,
                                activeStatusKindCount,
                                Array.Empty<MachineGunnerPreparedDamage>(),
                                blockBefore: 0,
                                blockAfter: 0));
                            break;
                        case MachineGunnerProgramOperationKind.ApplyPrivateStatus:
                            prepared.Add(PreparePrivateStatusOperation(
                                sourceId,
                                targetIds,
                                program,
                                operation,
                                cost,
                                projectedTargets));
                            break;
                        case MachineGunnerProgramOperationKind.ApplyPrivateStatusFromSpentAmmo:
                            int statusValue = cost.AmmoEffectValue / operation.Value;
                            if (statusValue > 0)
                            {
                                prepared.Add(PreparePrivateStatusOperation(
                                    sourceId,
                                    targetIds,
                                    program,
                                    operation,
                                    cost,
                                    projectedTargets,
                                    scaleWithExecutionKind: false,
                                    explicitValue: statusValue));
                            }

                            break;
                        case MachineGunnerProgramOperationKind.ApplyBurn:
                            prepared.Add(PrepareBurnOperation(
                                sourceId,
                                targetIds,
                                program,
                                operation,
                                cost,
                                projectedTargets));
                            break;
                        case MachineGunnerProgramOperationKind.ConvertSourceSmokeToTargetBurn:
                            MachineGunnerPreparedOperation smokeConversion =
                                PrepareSourceSmokeToTargetBurnOperation(
                                    sourceId,
                                    targetIds,
                                    projectedTargets);
                            if (smokeConversion != null)
                                prepared.Add(smokeConversion);
                            break;
                        case MachineGunnerProgramOperationKind.ApplyVulnerable:
                            prepared.Add(PrepareVulnerableOperation(
                                sourceId,
                                targetIds,
                                program,
                                operation,
                                cost,
                                projectedTargets));
                            break;
                        case MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke:
                            if (!TryPreparePoisonFromSourceSmokeOperation(
                                    sourceId,
                                    targetIds,
                                    projectedTargets,
                                    out MachineGunnerPreparedOperation poisonOperation,
                                    out failureReason))
                            {
                                operations = Array.Empty<MachineGunnerPreparedOperation>();
                                return false;
                            }

                            prepared.Add(poisonOperation);
                            break;
                        case MachineGunnerProgramOperationKind.ResolveIncompleteCombustion:
                            prepared.Add(PrepareIncompleteCombustionOperation(
                                targetIds,
                                projectedTargets));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(operation.Kind));
                    }
                }

                int kungfuMechStacks = GetPowerStack(MachineGunnerPowerKind.KungfuMech);
                if (kungfuMechStacks > 0 && program.ParticipatesInNonShootAttackSynergies)
                {
                    int blockValue = checked(kungfuMechStacks * KungfuMechBlockPerStack);
                    int blockBefore = simulatedSourceBlock;
                    simulatedSourceBlock = checked(simulatedSourceBlock + blockValue);
                    prepared.Add(new MachineGunnerPreparedOperation(
                        MachineGunnerProgramOperationKind.GainBlock,
                        blockValue,
                        Array.Empty<MachineGunnerPreparedDamage>(),
                        blockBefore,
                        simulatedSourceBlock));
                }

                int guerrillaTacticsStacks = GetPowerStack(MachineGunnerPowerKind.GuerrillaTactics);
                if (guerrillaTacticsStacks > 0 && cost.NominalAmmoSpentForTriggers > 0)
                {
                    int blockValue = checked(
                        guerrillaTacticsStacks * cost.NominalAmmoSpentForTriggers);
                    int blockBefore = simulatedSourceBlock;
                    simulatedSourceBlock = checked(simulatedSourceBlock + blockValue);
                    prepared.Add(new MachineGunnerPreparedOperation(
                        MachineGunnerProgramOperationKind.GainBlock,
                        blockValue,
                        Array.Empty<MachineGunnerPreparedDamage>(),
                        blockBefore,
                        simulatedSourceBlock));
                }

                operations = new ReadOnlyCollection<MachineGunnerPreparedOperation>(prepared);
                failureReason = BattleCommandExecutionFailureReason.None;
                return true;
            }
            catch (OverflowException)
            {
                operations = Array.Empty<MachineGunnerPreparedOperation>();
                failureReason = BattleCommandExecutionFailureReason.EffectValueOverflow;
                return false;
            }
        }

        /// <summary>按冻结资源计划依次追加首波支付与命中、补满、次波支付与命中，目标死亡只截断伤害而不改写资源轨迹。</summary>
        private void AppendPreparedReloadedAmmoVolley(
            CombatantId sourceId,
            CombatantData source,
            IReadOnlyList<CombatantId> targetIds,
            MachineGunnerCardProgram program,
            MachineGunnerProgramOperation damageOperation,
            MachineGunnerCostResolution cost,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> prepared)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (targetIds == null)
                throw new ArgumentNullException(nameof(targetIds));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (damageOperation == null)
                throw new ArgumentNullException(nameof(damageOperation));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (targetIds.Count != 1 || cost.ReloadedVolley == null)
            {
                throw new InvalidOperationException(
                    "换弹连射必须绑定一个命令开始时冻结的最近敌人和一份资源轨迹。");
            }

            MachineGunnerPreparedReloadedVolley volley = cost.ReloadedVolley;
            AppendPreparedAmmoSpendIfPositive(volley.FirstWaveActualAmmoSpent, prepared);
            AppendPreparedReloadedVolleyHits(
                sourceId,
                source,
                targetIds[0],
                program,
                damageOperation,
                cost,
                volley.FirstWaveEffectShotCount,
                projectedTargets,
                prepared);
            prepared.Add(new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.FillAmmo,
                value: 1,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0));
            AppendPreparedAmmoSpendIfPositive(volley.SecondWaveActualAmmoSpent, prepared);
            AppendPreparedReloadedVolleyHits(
                sourceId,
                source,
                targetIds[0],
                program,
                damageOperation,
                cost,
                volley.SecondWaveEffectShotCount,
                projectedTargets,
                prepared);
        }

        /// <summary>把一笔正数弹药支付冻结为有序操作；零支付不伪造 AmmoSpent 记录。</summary>
        private static void AppendPreparedAmmoSpendIfPositive(
            int amount,
            ICollection<MachineGunnerPreparedOperation> prepared)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (amount == 0)
                return;

            prepared.Add(new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.SpendAmmo,
                amount,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0));
        }

        /// <summary>按同一冻结目标展开一波来源射击及其命中后钩子，目标投影死亡时停止剩余伤害段。</summary>
        private void AppendPreparedReloadedVolleyHits(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            MachineGunnerCardProgram program,
            MachineGunnerProgramOperation damageOperation,
            MachineGunnerCostResolution cost,
            int hitCount,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> prepared)
        {
            if (hitCount < 0)
                throw new ArgumentOutOfRangeException(nameof(hitCount));

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                if (!AppendPreparedHitAndPostHitOperations(
                        sourceId,
                        source,
                        targetId,
                        program,
                        damageOperation,
                        cost,
                        projectedTargets,
                        prepared))
                {
                    break;
                }
            }
        }

        /// <summary>冻结一段实际命中及紧随其后的卡牌和能力命中后效果，保证下一段伤害读取已更新的投影状态。</summary>
        private bool AppendPreparedHitAndPostHitOperations(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            MachineGunnerCardProgram program,
            MachineGunnerProgramOperation damageOperation,
            MachineGunnerCostResolution cost,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> prepared,
            int? explicitDamageValue = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (damageOperation == null)
                throw new ArgumentNullException(nameof(damageOperation));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            int damageValue = explicitDamageValue ?? damageOperation.Value;
            if (damageValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(explicitDamageValue));

            var damages = new List<MachineGunnerPreparedDamage>(capacity: 1);
            if (!AppendPreparedDamageIfProjectedAlive(
                    sourceId,
                    source,
                    targetId,
                    damageValue,
                    program.Tags,
                    MachineGunnerDamageKind.Attack,
                    projectedTargets,
                    damages))
            {
                return false;
            }

            prepared.Add(new MachineGunnerPreparedOperation(
                damageOperation.Kind,
                damageValue,
                damages,
                blockBefore: 0,
                blockAfter: 0));
            AppendPostHitOperationsForTarget(
                sourceId,
                source,
                targetId,
                program,
                cost,
                projectedTargets,
                prepared);
            AppendPortableHelperDamageForShootHit(
                sourceId,
                source,
                targetId,
                program,
                projectedTargets,
                prepared);
            return true;
        }

        /// <summary>在射击来源段及全部既有命中后效果完成后，按帮手层数逐段攻击原目标，目标死亡即停止且不递归触发。</summary>
        private void AppendPortableHelperDamageForShootHit(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            MachineGunnerCardProgram program,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> prepared)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (!program.IsShootCategory)
                return;

            int helperStacks = GetPowerStack(MachineGunnerPowerKind.PortableHelper);
            for (int helperIndex = 0; helperIndex < helperStacks; helperIndex++)
            {
                var damages = new List<MachineGunnerPreparedDamage>(capacity: 1);
                if (!AppendPreparedDamageIfProjectedAlive(
                        sourceId,
                        source,
                        targetId,
                        1,
                        MachineGunnerCardTag.None,
                        MachineGunnerDamageKind.PortableHelper,
                        projectedTargets,
                        damages))
                {
                    break;
                }

                prepared.Add(new MachineGunnerPreparedOperation(
                    MachineGunnerProgramOperationKind.Damage,
                    1,
                    damages,
                    blockBefore: 0,
                    blockAfter: 0));
            }
        }

        /// <summary>在目标经本段伤害后仍存活时，依次冻结卡牌声明和已激活能力带来的命中后状态写入。</summary>
        private void AppendPostHitOperationsForTarget(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            MachineGunnerCardProgram program,
            MachineGunnerCostResolution cost,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> prepared)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (!projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) ||
                projected.Health <= 0)
            {
                return;
            }

            IReadOnlyList<CombatantId> hitTargetIds = new[] { targetId };
            foreach (MachineGunnerProgramOperation operation in program.PostHitOperations)
            {
                AppendPreparedPostHitStatusOperation(
                    sourceId,
                    hitTargetIds,
                    program,
                    operation,
                    cost,
                    projectedTargets,
                    prepared);
            }

            int incendiaryAmmoStacks = GetPowerStack(MachineGunnerPowerKind.IncendiaryAmmo);
            if (program.ReceivesIncendiaryAmmo && incendiaryAmmoStacks > 0)
            {
                AppendPreparedPostHitStatusOperation(
                    sourceId,
                    hitTargetIds,
                    program,
                    MachineGunnerProgramOperation.ApplyBurn(
                        checked(incendiaryAmmoStacks * IncendiaryAmmoBurnPerStack)),
                    cost,
                    projectedTargets,
                    prepared);
            }

            if (program.ParticipatesInNonShootAttackSynergies &&
                GetPowerStack(MachineGunnerPowerKind.AgedOil) > 0)
            {
                AppendPreparedPostHitStatusOperation(
                    sourceId,
                    hitTargetIds,
                    program,
                    MachineGunnerProgramOperation.ApplyPrivateStatus(
                        MachineGunnerCombatantStatus.Oil,
                        AgedOilPerNonShootHit),
                    cost,
                    projectedTargets,
                    prepared);
            }

            if (program.TriggersCurrentBurnDebuffAfterGlobalHitEffects)
            {
                AppendPreparedCurrentBurnDebuffIfProjectedAlive(
                    sourceId,
                    source,
                    targetId,
                    projectedTargets,
                    prepared);
            }
        }

        /// <summary>在本段攻击和全部全局命中钩子后，按目标既有燃烧层数冻结一次不读取攻击修正、但读取破甲的燃烧伤害。</summary>
        private void AppendPreparedCurrentBurnDebuffIfProjectedAlive(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> prepared)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (!projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) ||
                projected.Health <= 0)
            {
                return;
            }

            int burn = projected.GetPrivateStatus(MachineGunnerCombatantStatus.Burn);
            if (burn <= 0)
                return;

            var damages = new List<MachineGunnerPreparedDamage>(capacity: 1);
            if (!AppendPreparedDamageIfProjectedAlive(
                    sourceId,
                    source,
                    targetId,
                    burn,
                    MachineGunnerCardTag.None,
                    MachineGunnerDamageKind.Burn,
                    projectedTargets,
                    damages))
            {
                return;
            }

            prepared.Add(new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.Damage,
                burn,
                damages,
                blockBefore: 0,
                blockAfter: 0));
        }

        /// <summary>将受限命中后原子操作复用既有投影器冻结为单一实际命中目标的提交记录，且不让 X 费放大单段状态值。</summary>
        private void AppendPreparedPostHitStatusOperation(
            CombatantId sourceId,
            IReadOnlyList<CombatantId> hitTargetIds,
            MachineGunnerCardProgram program,
            MachineGunnerProgramOperation operation,
            MachineGunnerCostResolution cost,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedOperation> prepared)
        {
            if (hitTargetIds == null)
                throw new ArgumentNullException(nameof(hitTargetIds));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));

            switch (operation.Kind)
            {
                case MachineGunnerProgramOperationKind.ApplyPrivateStatus:
                    prepared.Add(PreparePrivateStatusOperation(
                        sourceId,
                        hitTargetIds,
                        program,
                        operation,
                        cost,
                        projectedTargets,
                        scaleWithExecutionKind: false));
                    break;
                case MachineGunnerProgramOperationKind.ApplyBurn:
                    prepared.Add(PrepareBurnOperation(
                        sourceId,
                        hitTargetIds,
                        program,
                        operation,
                        cost,
                        projectedTargets,
                        scaleWithExecutionKind: false));
                    break;
                case MachineGunnerProgramOperationKind.ApplyVulnerable:
                    prepared.Add(PrepareVulnerableOperation(
                        sourceId,
                        hitTargetIds,
                        program,
                        operation,
                        cost,
                        projectedTargets,
                        scaleWithExecutionKind: false));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        "命中后操作包含了未受支持的类别。");
            }
        }

        /// <summary>从程序目标范围预演燃烧的原子增量与既有浸油减半，并同步更新后续操作读取的职业状态投影。</summary>
        private MachineGunnerPreparedOperation PrepareBurnOperation(
            CombatantId sourceId,
            IReadOnlyList<CombatantId> programTargetIds,
            MachineGunnerCardProgram program,
            MachineGunnerProgramOperation operation,
            MachineGunnerCostResolution cost,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            bool scaleWithExecutionKind = true,
            int? explicitValue = null)
        {
            int baseBurn = DetermineOperationValue(
                program,
                operation.Value,
                cost,
                scaleWithExecutionKind);
            var applications = new List<MachineGunnerPreparedBurnApplication>();
            foreach (CombatantId targetId in ResolveOperationTargets(
                         operation.TargetScope,
                         sourceId,
                         programTargetIds))
            {
                if (!projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) ||
                    projected.Health <= 0)
                {
                    continue;
                }

                MachineGunnerBurnApplicationResult result =
                    MachineGunnerCombatState.CalculateBurnApplication(
                        projected.GetPrivateStatus(MachineGunnerCombatantStatus.Burn),
                        projected.GetPrivateStatus(MachineGunnerCombatantStatus.Oil),
                        baseBurn);
                projected.SetPrivateStatus(
                    MachineGunnerCombatantStatus.Burn,
                    result.BurnChange.After);
                projected.SetPrivateStatus(
                    MachineGunnerCombatantStatus.Oil,
                    result.OilChange.After);
                applications.Add(new MachineGunnerPreparedBurnApplication(targetId, result));
            }

            return new MachineGunnerPreparedOperation(
                operation.Kind,
                baseBurn,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                burnApplications: applications);
        }

        /// <summary>冻结焚风的来源烟雾、目标燃烧与既有浸油；来源没有烟雾时返回空操作且不制造状态记录。</summary>
        private static MachineGunnerPreparedOperation PrepareSourceSmokeToTargetBurnOperation(
            CombatantId sourceId,
            IReadOnlyList<CombatantId> programTargetIds,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets)
        {
            if (programTargetIds == null)
                throw new ArgumentNullException(nameof(programTargetIds));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (programTargetIds.Count != 1 || programTargetIds[0] == sourceId)
                throw new InvalidOperationException("焚风必须且只能绑定一名显式敌方目标。");
            if (!projectedTargets.TryGetValue(sourceId, out MachineGunnerProjectedCombatant source) ||
                source.Health <= 0)
            {
                throw new InvalidOperationException("焚风预演缺少存活的施放者投影。");
            }
            if (!projectedTargets.TryGetValue(
                    programTargetIds[0],
                    out MachineGunnerProjectedCombatant target) ||
                target.Health <= 0)
            {
                throw new InvalidOperationException("焚风预演缺少存活的显式敌方目标投影。");
            }

            int sourceSmoke = source.GetPrivateStatus(MachineGunnerCombatantStatus.Smoke);
            if (sourceSmoke <= 0)
                return null;

            MachineGunnerBurnApplicationResult burnApplication =
                MachineGunnerCombatState.CalculateBurnApplication(
                    target.GetPrivateStatus(MachineGunnerCombatantStatus.Burn),
                    target.GetPrivateStatus(MachineGunnerCombatantStatus.Oil),
                    sourceSmoke);
            target.SetPrivateStatus(
                MachineGunnerCombatantStatus.Burn,
                burnApplication.BurnChange.After);
            target.SetPrivateStatus(
                MachineGunnerCombatantStatus.Oil,
                burnApplication.OilChange.After);
            source.SetPrivateStatus(MachineGunnerCombatantStatus.Smoke, 0);

            var conversion = new MachineGunnerPreparedSmokeToBurnConversion(
                sourceId,
                programTargetIds[0],
                burnApplication,
                new MachineGunnerStatusValueChange(
                    MachineGunnerCombatantStatus.Smoke,
                    sourceSmoke,
                    after: 0));
            return new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.ConvertSourceSmokeToTargetBurn,
                sourceSmoke,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                smokeToBurnConversion: conversion);
        }

        /// <summary>按效果开始时的燃烧来源快照，先以动态存活目标预演全部 Debuff 伤害，再把最终幸存者的燃烧一比一转为烟雾且不读取浸油。</summary>
        private MachineGunnerPreparedOperation PrepareIncompleteCombustionOperation(
            IReadOnlyList<CombatantId> programTargetIds,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets)
        {
            if (programTargetIds == null)
                throw new ArgumentNullException(nameof(programTargetIds));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));

            var burningSources = new List<KeyValuePair<CombatantId, int>>();
            foreach (CombatantId candidateId in programTargetIds)
            {
                if (!projectedTargets.TryGetValue(
                        candidateId,
                        out MachineGunnerProjectedCombatant candidate) ||
                    candidate.Health <= 0)
                {
                    continue;
                }

                int burn = candidate.GetPrivateStatus(MachineGunnerCombatantStatus.Burn);
                if (burn > 0)
                    burningSources.Add(new KeyValuePair<CombatantId, int>(candidateId, burn));
            }

            var damages = new List<MachineGunnerPreparedIncompleteCombustionDamage>();
            foreach (KeyValuePair<CombatantId, int> burningSource in burningSources)
            {
                if (!_combatants.TryGet(burningSource.Key, out CombatantData source))
                {
                    throw new InvalidOperationException("不充分爆燃的燃烧来源在预演前丢失。");
                }

                foreach (CombatantId targetId in programTargetIds)
                {
                    if (!projectedTargets.TryGetValue(
                            targetId,
                            out MachineGunnerProjectedCombatant target) ||
                        target.Health <= 0)
                    {
                        continue;
                    }

                    MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
                        new MachineGunnerDamageRequest(
                            burningSource.Key,
                            targetId,
                            burningSource.Value,
                            MachineGunnerDamageKind.Burn),
                        source,
                        new BattleEffectTargetSnapshot(
                            target.Health,
                            target.Block,
                            target.Vulnerable),
                        _combatState);
                    BattleDamageFormulaOutcome outcome = calculation.Outcome;
                    target.Health = outcome.HealthAfter;
                    target.Block = outcome.BlockAfter;
                    damages.Add(new MachineGunnerPreparedIncompleteCombustionDamage(
                        burningSource.Key,
                        new MachineGunnerPreparedDamage(
                            targetId,
                            outcome,
                            calculation.ConsumesArmor)));
                }
            }

            var conversions = new List<MachineGunnerPreparedBurnSmokeConversion>();
            foreach (CombatantId targetId in programTargetIds)
            {
                if (!projectedTargets.TryGetValue(
                        targetId,
                        out MachineGunnerProjectedCombatant target) ||
                    target.Health <= 0)
                {
                    continue;
                }

                int burnBefore = target.GetPrivateStatus(MachineGunnerCombatantStatus.Burn);
                if (burnBefore <= 0)
                    continue;

                int smokeBefore = target.GetPrivateStatus(MachineGunnerCombatantStatus.Smoke);
                MachineGunnerStatusValueChange smokeChange = new MachineGunnerStatusValueChange(
                    MachineGunnerCombatantStatus.Smoke,
                    smokeBefore,
                    checked(smokeBefore + burnBefore));
                MachineGunnerStatusValueChange burnChange = new MachineGunnerStatusValueChange(
                    MachineGunnerCombatantStatus.Burn,
                    burnBefore,
                    after: 0);
                target.SetPrivateStatus(MachineGunnerCombatantStatus.Smoke, smokeChange.After);
                target.SetPrivateStatus(MachineGunnerCombatantStatus.Burn, burnChange.After);
                conversions.Add(new MachineGunnerPreparedBurnSmokeConversion(
                    targetId,
                    smokeChange,
                    burnChange));
            }

            return new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.ResolveIncompleteCombustion,
                value: 1,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                incompleteCombustionDamages: damages,
                burnSmokeConversions: conversions);
        }

        /// <summary>从程序目标范围中冻结本次私有状态操作的所有存活参与者和前后层数。</summary>
        private MachineGunnerPreparedOperation PreparePrivateStatusOperation(
            CombatantId sourceId,
            IReadOnlyList<CombatantId> programTargetIds,
            MachineGunnerCardProgram program,
            MachineGunnerProgramOperation operation,
            MachineGunnerCostResolution cost,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            bool scaleWithExecutionKind = true,
            int? explicitValue = null)
        {
            if (!operation.PrivateStatus.HasValue)
                throw new InvalidOperationException("私有状态程序操作缺少状态身份。");

            int value = explicitValue ?? DetermineOperationValue(
                program,
                operation.Value,
                cost,
                scaleWithExecutionKind);
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(explicitValue));
            var changes = new List<MachineGunnerPreparedStatusChange>();
            foreach (CombatantId targetId in ResolveOperationTargets(
                         operation.TargetScope,
                         sourceId,
                         programTargetIds))
            {
                if (!projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) ||
                    projected.Health <= 0)
                {
                    continue;
                }

                int before = projected.GetPrivateStatus(operation.PrivateStatus.Value);
                int after = checked(before + value);
                projected.SetPrivateStatus(operation.PrivateStatus.Value, after);
                changes.Add(new MachineGunnerPreparedStatusChange(
                    MachineGunnerPreparedStatusChangeKind.PrivateStatus,
                    targetId,
                    operation.PrivateStatus.Value,
                    before,
                    after));
            }

            return new MachineGunnerPreparedOperation(
                operation.Kind,
                value,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                statusChanges: changes);
        }

        /// <summary>按通用易伤公式冻结职业程序的目标状态变化，同时更新同命令后续操作的投影。</summary>
        private MachineGunnerPreparedOperation PrepareVulnerableOperation(
            CombatantId sourceId,
            IReadOnlyList<CombatantId> programTargetIds,
            MachineGunnerCardProgram program,
            MachineGunnerProgramOperation operation,
            MachineGunnerCostResolution cost,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            bool scaleWithExecutionKind = true)
        {
            int declaredValue = DetermineOperationValue(
                program,
                operation.Value,
                cost,
                scaleWithExecutionKind);
            BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
                new BattleEffectFormulaContext(
                    BattleEffectOperationType.ApplyVulnerable,
                    declaredValue,
                    sourceStrength: 0,
                    target: null));
            if (formula.Value <= 0)
                throw new InvalidOperationException("易伤公式必须返回正数。");

            var changes = new List<MachineGunnerPreparedStatusChange>();
            foreach (CombatantId targetId in ResolveOperationTargets(
                         operation.TargetScope,
                         sourceId,
                         programTargetIds))
            {
                if (!projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) ||
                    projected.Health <= 0)
                {
                    continue;
                }

                int before = projected.Vulnerable;
                int after = checked(before + formula.Value);
                projected.Vulnerable = after;
                changes.Add(new MachineGunnerPreparedStatusChange(
                    MachineGunnerPreparedStatusChangeKind.Vulnerable,
                    targetId,
                    null,
                    before,
                    after));
            }

            return new MachineGunnerPreparedOperation(
                operation.Kind,
                formula.Value,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                statusChanges: changes);
        }

        /// <summary>冻结单一敌方目标、施放者命令起点烟雾与等量通用中毒计划，过程不消耗烟雾。</summary>
        private bool TryPreparePoisonFromSourceSmokeOperation(
            CombatantId sourceId,
            IReadOnlyList<CombatantId> programTargetIds,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            out MachineGunnerPreparedOperation preparedOperation,
            out BattleCommandExecutionFailureReason failureReason)
        {
            preparedOperation = null;
            if (programTargetIds == null)
                throw new ArgumentNullException(nameof(programTargetIds));
            if (projectedTargets == null)
                throw new ArgumentNullException(nameof(projectedTargets));
            if (programTargetIds.Count != 1)
            {
                failureReason = BattleCommandExecutionFailureReason.TargetRuleMismatch;
                return false;
            }
            if (!projectedTargets.TryGetValue(
                    sourceId,
                    out MachineGunnerProjectedCombatant projectedSource) ||
                projectedSource.Health <= 0)
            {
                failureReason = BattleCommandExecutionFailureReason.EffectSourceNotAlive;
                return false;
            }

            CombatantId targetId = programTargetIds[0];
            if (!projectedTargets.TryGetValue(
                    targetId,
                    out MachineGunnerProjectedCombatant projectedTarget))
            {
                failureReason = BattleCommandExecutionFailureReason.TargetNotFound;
                return false;
            }
            if (projectedTarget.Health <= 0)
            {
                failureReason = BattleCommandExecutionFailureReason.TargetNotAlive;
                return false;
            }

            int sourceSmokeBefore = projectedSource.GetPrivateStatus(
                MachineGunnerCombatantStatus.Smoke);
            BattlePoisonApplicationPreparationResult poisonPreparation =
                _poisonApplication.PrepareApply(
                    sourceId,
                    targetId,
                    sourceSmokeBefore);
            if (!poisonPreparation.Succeeded)
            {
                failureReason = poisonPreparation.FailureReason;
                return false;
            }

            var poisonApplication = new MachineGunnerPreparedPoisonApplication(
                sourceId,
                sourceSmokeBefore,
                poisonPreparation.Plan);
            preparedOperation = new MachineGunnerPreparedOperation(
                MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke,
                sourceSmokeBefore,
                Array.Empty<MachineGunnerPreparedDamage>(),
                blockBefore: 0,
                blockAfter: 0,
                poisonApplication: poisonApplication);
            failureReason = BattleCommandExecutionFailureReason.None;
            return true;
        }

        /// <summary>按声明范围解析命令开始时的唯一参与者，并委托单一状态种类计数口径。</summary>
        private int CountInitialActiveStatusKinds(
            MachineGunnerOperationTargetScope targetScope,
            CombatantId sourceId,
            IReadOnlyList<CombatantId> programTargetIds)
        {
            IReadOnlyList<CombatantId> combatantIds = ResolveOperationTargets(
                targetScope,
                sourceId,
                programTargetIds);
            if (combatantIds.Count != 1)
            {
                throw new InvalidOperationException(
                    "按活跃状态种类抽牌要求目标范围解析为唯一参与者。");
            }

            return CountInitialActiveStatusKindsForCombatant(combatantIds[0]);
        }

        /// <summary>按命令开始快照统计单一参与者的 Strength、Vulnerable、Poison 与全部十七种职业状态种类；同种正数层数只计一次。</summary>
        private int CountInitialActiveStatusKindsForCombatant(CombatantId combatantId)
        {
            if (!_combatants.TryGet(combatantId, out CombatantData combatant))
                throw new InvalidOperationException("按状态种类展开的参与者不存在。");

            int activeKindCount = 0;
            if (combatant.Strength.CurrentValue != 0)
                activeKindCount++;
            if (combatant.CurrentVulnerable > 0)
                activeKindCount++;
            if (combatant.CurrentPoison > 0)
                activeKindCount++;
            foreach (MachineGunnerCombatantStatus status in
                     (MachineGunnerCombatantStatus[])Enum.GetValues(
                         typeof(MachineGunnerCombatantStatus)))
            {
                if (_combatState.Get(combatantId, status) > 0)
                    activeKindCount++;
            }

            return activeKindCount;
        }

        /// <summary>把幻彩射击的逻辑基础段与兴奋剂紧邻复制展开后交给共享固定目标规划器。</summary>
        private BattleRepeatedDamagePreparationResult PreparePrismaticRepeatedDamage(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            MachineGunnerCardProgram program,
            int initialStatusKindCount,
            MachineGunnerCostResolution cost,
            out BattleRepeatedDamageExecutor executor)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (initialStatusKindCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialStatusKindCount));
            if (program.ExecutionKind !=
                    MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds ||
                program.Operations.Count != 2)
            {
                throw new InvalidOperationException("幻彩射击共享计划收到不匹配的程序 grammar。");
            }

            var hits = new List<BattleRepeatedDamageHitRequest>();
            AppendPrismaticLogicalHit(
                hits,
                program.Operations[0].Value,
                CanReceiveStimBonus(program));
            for (int index = 0; index < initialStatusKindCount; index++)
            {
                AppendPrismaticLogicalHit(
                    hits,
                    program.Operations[1].Value,
                    CanReceiveStimBonus(program));
            }
            int expectedAmmoEffectValue = checked(
                program.BaseAmmoCost +
                (CanReceiveStimBonus(program) ? hits.Count / 2 : 0));
            if (cost.AmmoEffectValue != expectedAmmoEffectValue)
                throw new InvalidOperationException("幻彩射击的冻结弹药费用与逻辑射击展开不一致。");

            executor = new BattleRepeatedDamageExecutor(
                _combatants,
                _enemyCombatantIdsInEncounterOrder,
                new GameRandom(1u));
            var sequence = new MachineGunnerRepeatedDamageHitSequence(
                this,
                sourceId,
                targetId,
                program,
                cost);
            return executor.Prepare(
                new BattleRepeatedDamageRequest(
                    sourceId,
                    targetId,
                    BattleRepeatedDamageTargetPolicy.FixedEnemy,
                    hits),
                sequence);
        }

        /// <summary>追加一个逻辑射击，并在兴奋剂生效时立即复制同一基础值的相邻段。</summary>
        private static void AppendPrismaticLogicalHit(
            ICollection<BattleRepeatedDamageHitRequest> hits,
            int baseDamage,
            bool stimActive)
        {
            if (hits == null)
                throw new ArgumentNullException(nameof(hits));
            if (baseDamage <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseDamage));

            hits.Add(new BattleRepeatedDamageHitRequest(null, baseDamage));
            if (stimActive)
                hits.Add(new BattleRepeatedDamageHitRequest(null, baseDamage));
        }

        /// <summary>将操作声明的目标范围转换为稳定的、去重后的参与者顺序。</summary>
        private static IReadOnlyList<CombatantId> ResolveOperationTargets(
            MachineGunnerOperationTargetScope targetScope,
            CombatantId sourceId,
            IReadOnlyList<CombatantId> programTargetIds)
        {
            if (programTargetIds == null)
                throw new ArgumentNullException(nameof(programTargetIds));

            switch (targetScope)
            {
                case MachineGunnerOperationTargetScope.ProgramTargets:
                    return programTargetIds;
                case MachineGunnerOperationTargetScope.Source:
                    return new[] { sourceId };
                case MachineGunnerOperationTargetScope.SourceAndProgramTargets:
                    var targets = new List<CombatantId> { sourceId };
                    foreach (CombatantId targetId in programTargetIds)
                    {
                        if (targetId != sourceId)
                            targets.Add(targetId);
                    }

                    return targets;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetScope));
            }
        }

        /// <summary>为一次程序预演复制所有参与者会影响伤害的标量，避免多段伤害读取已经提交前的旧目标状态。</summary>
        private Dictionary<CombatantId, MachineGunnerProjectedCombatant> CreateProjectedCombatants()
        {
            var projected = new Dictionary<CombatantId, MachineGunnerProjectedCombatant>();
            foreach (KeyValuePair<CombatantId, CombatantData> entry in _combatants.All)
            {
                CombatantData combatant = entry.Value;
                projected.Add(
                    entry.Key,
                    new MachineGunnerProjectedCombatant(
                        combatant.CurrentHealth,
                        combatant.CurrentBlock,
                        combatant.CurrentVulnerable,
                        _combatState,
                        entry.Key));
            }

            return projected;
        }

        /// <summary>按参与者身份冻结全场每人的十七项职业私有状态，供复合计划首写前联合校验。</summary>
        private Dictionary<CombatantId, int[]> CaptureAllPrivateStatusSnapshots()
        {
            MachineGunnerCombatantStatus[] statuses =
                (MachineGunnerCombatantStatus[])Enum.GetValues(
                    typeof(MachineGunnerCombatantStatus));
            var snapshots = new Dictionary<CombatantId, int[]>();
            foreach (KeyValuePair<CombatantId, CombatantData> entry in _combatants.All)
            {
                var values = new int[statuses.Length];
                for (int index = 0; index < statuses.Length; index++)
                    values[index] = _combatState.Get(entry.Key, statuses[index]);
                snapshots.Add(entry.Key, values);
            }

            return snapshots;
        }

        /// <summary>确认全场参与者身份及每人的十七项职业私有状态仍与冻结快照完全一致。</summary>
        private bool MatchesAllPrivateStatusSnapshots(
            IReadOnlyDictionary<CombatantId, int[]> snapshots)
        {
            if (snapshots == null || snapshots.Count != _combatants.All.Count)
                return false;
            MachineGunnerCombatantStatus[] statuses =
                (MachineGunnerCombatantStatus[])Enum.GetValues(
                    typeof(MachineGunnerCombatantStatus));
            foreach (KeyValuePair<CombatantId, CombatantData> entry in _combatants.All)
            {
                if (!snapshots.TryGetValue(entry.Key, out int[] values) ||
                    values == null || values.Length != statuses.Length)
                {
                    return false;
                }
                for (int index = 0; index < statuses.Length; index++)
                {
                    if (_combatState.Get(entry.Key, statuses[index]) != values[index])
                        return false;
                }
            }

            return true;
        }

        /// <summary>按程序执行形态从已冻结资源支付推导一项伤害操作的段数，零段仍允许卡牌正常完成归宿。</summary>
        private static int DetermineDamageHitCount(
            MachineGunnerCardProgram program,
            MachineGunnerCostResolution cost)
        {
            switch (program.ExecutionKind)
            {
                case MachineGunnerProgramExecutionKind.Standard:
                    return program.BaseShootHitCount > 0
                        ? checked(program.BaseShootHitCount + cost.StimBonusHitCount)
                        : 1;
                case MachineGunnerProgramExecutionKind.RepeatByX:
                    return cost.XValue;
                case MachineGunnerProgramExecutionKind.WildRampage:
                    return checked(
                        cost.AmmoEffectValue + cost.XValue + cost.StimBonusHitCount);
                case MachineGunnerProgramExecutionKind.SpendAmmoShots:
                    return cost.AmmoEffectValue;
                case MachineGunnerProgramExecutionKind.OrderedTargetDamageOperations:
                case MachineGunnerProgramExecutionKind.LinearDamageByTargetOrdinal:
                    return 1;
                case MachineGunnerProgramExecutionKind.InitialThenRepeatByTargetStatusKinds:
                    throw new InvalidOperationException(
                        "按目标起始状态展开的射击必须通过共享重复伤害计划执行。");
                default:
                    throw new ArgumentOutOfRangeException(nameof(program.ExecutionKind));
            }
        }

        /// <summary>按施放快照中的零基目标序号，每位敌人增加基础伤害的百分之五十并向下取整。</summary>
        private static int CalculateLinearTargetOrdinalDamage(
            int baseDamage,
            int targetOrdinal)
        {
            if (baseDamage <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (targetOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(targetOrdinal));

            long doubledMultiplier = checked(targetOrdinal + 2L);
            long doubledDamage = checked((long)baseDamage * doubledMultiplier);
            return checked((int)(doubledDamage / 2L));
        }

        /// <summary>按调用方语义决定是否把顶层 X 重复程序的原子数值放大为 X 倍；逐段命中后的状态值始终保留声明值。</summary>
        private static int DetermineOperationValue(
            MachineGunnerCardProgram program,
            int declaredValue,
            MachineGunnerCostResolution cost,
            bool scaleWithExecutionKind = true)
        {
            if (declaredValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(declaredValue));

            return scaleWithExecutionKind &&
                program.ExecutionKind == MachineGunnerProgramExecutionKind.RepeatByX
                ? checked(declaredValue * cost.XValue)
                : declaredValue;
        }

        /// <summary>为一段伤害选择已冻结的目标；随机模式每段只从投影中仍存活的敌人候选重新取样。</summary>
        private bool TryResolvePreparedDamageTarget(
            MachineGunnerCardProgram program,
            IReadOnlyList<CombatantId> targetIds,
            IReadOnlyDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            GameRandom candidateRandom,
            out CombatantId targetId)
        {
            if (program.TargetInputMode == MachineGunnerTargetInputMode.RandomLivingEnemy)
            {
                var candidates = new List<CombatantId>();
                foreach (CombatantId enemyId in _targetSelector.GetLivingEnemiesInEncounterOrder())
                {
                    if (projectedTargets.TryGetValue(enemyId, out MachineGunnerProjectedCombatant projected) &&
                        projected.Health > 0)
                    {
                        candidates.Add(enemyId);
                    }
                }

                if (candidates.Count == 0)
                {
                    targetId = default;
                    return false;
                }

                targetId = candidates[candidateRandom.NextInt(candidates.Count)];
                return true;
            }

            if (targetIds.Count != 1)
            {
                targetId = default;
                return false;
            }

            targetId = targetIds[0];
            return true;
        }

        /// <summary>在投影目标仍存活时计算并保存一段伤害；目标死亡不视为整张已合法卡牌失败。</summary>
        private bool AppendPreparedDamageIfProjectedAlive(
            CombatantId sourceId,
            CombatantData source,
            CombatantId targetId,
            int damageValue,
            MachineGunnerCardTag tags,
            MachineGunnerDamageKind damageKind,
            IDictionary<CombatantId, MachineGunnerProjectedCombatant> projectedTargets,
            ICollection<MachineGunnerPreparedDamage> damages)
        {
            if (!projectedTargets.TryGetValue(targetId, out MachineGunnerProjectedCombatant projected) ||
                projected.Health <= 0)
            {
                return false;
            }

            MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
                new MachineGunnerDamageRequest(
                    sourceId,
                    targetId,
                    damageValue,
                    damageKind,
                    tags),
                source,
                new BattleEffectTargetSnapshot(
                    projected.Health,
                    projected.Block,
                    projected.Vulnerable),
                CreateCombatStateFromProjection(projectedTargets));
            BattleDamageFormulaOutcome outcome = calculation.Outcome;
            damages.Add(new MachineGunnerPreparedDamage(
                targetId,
                outcome,
                calculation.ConsumesArmor));
            projected.Health = outcome.HealthAfter;
            projected.Block = outcome.BlockAfter;
            return true;
        }

        /// <summary>在回合末燃烧伤害前，若烈火烹油已激活则按 Encounter 顺序只为已有燃烧的存活敌人增加固定燃烧；浸油保持不变。</summary>
        private void AppendBurningOilGrowthForLivingEnemies(
            IReadOnlyList<CombatantId> livingEnemyIds,
            int startingOrder,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (livingEnemyIds == null)
                throw new ArgumentNullException(nameof(livingEnemyIds));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (GetPowerStack(MachineGunnerPowerKind.BurningOil) <= 0)
                return;

            foreach (CombatantId enemyId in livingEnemyIds)
            {
                if (!_combatants.TryGet(enemyId, out CombatantData enemy) || !enemy.IsAlive)
                    continue;

                int burnBefore = _combatState.Get(enemyId, MachineGunnerCombatantStatus.Burn);
                if (burnBefore <= 0)
                    continue;

                int oil = _combatState.Get(enemyId, MachineGunnerCombatantStatus.Oil);
                MachineGunnerStatusValueChange growth = _combatState.Add(
                    enemyId,
                    MachineGunnerCombatantStatus.Burn,
                    checked(BurningOilBurnGrowth + oil));
                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    checked(startingOrder + settlements.Count),
                    _playerId,
                    enemyId,
                    MachineGunnerCombatantStatus.Burn,
                    growth.Before,
                    growth.After));
            }
        }

        /// <summary>对仍存活且带有燃烧的参与者提交一段燃烧伤害；它不读取攻击修正、不消耗护甲，也不减少自身层数。</summary>
        private void AppendBurnDamageIfLiving(
            CombatantId targetId,
            int startingOrder,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (!_combatants.TryGet(targetId, out CombatantData target) || !target.IsAlive)
                return;

            int burn = _combatState.Get(targetId, MachineGunnerCombatantStatus.Burn);
            if (burn <= 0)
                return;

            int settlementOrder = checked(startingOrder + settlements.Count);
            MachineGunnerDamageCalculation calculation = MachineGunnerDamagePipeline.Calculate(
                new MachineGunnerDamageRequest(
                    targetId,
                    targetId,
                    burn,
                    MachineGunnerDamageKind.Burn),
                target,
                new BattleEffectTargetSnapshot(
                    target.CurrentHealth,
                    target.CurrentBlock,
                    target.CurrentVulnerable),
                _combatState);
            BattleDamageFormulaOutcome outcome = calculation.Outcome;
            target.ApplyDamageOutcome(outcome);
            settlements.Add(new BattleDamageAppliedSettlement(
                settlementOrder,
                null,
                targetId,
                targetId,
                outcome.AttackValue,
                outcome.BlockBefore,
                outcome.BlockAfter,
                outcome.HealthBefore,
                outcome.HealthAfter));
        }

        /// <summary>首次写入前联合核对全部二手烟操作的来源烟雾与通用中毒计划。</summary>
        private bool ValidatePreparedPoisonApplications(
            CombatantId sourceId,
            IReadOnlyList<MachineGunnerPreparedOperation> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            foreach (MachineGunnerPreparedOperation operation in operations)
            {
                if (operation.Kind !=
                    MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke)
                {
                    continue;
                }

                MachineGunnerPreparedPoisonApplication poison =
                    operation.PoisonApplication ??
                    throw new InvalidOperationException("二手烟操作缺少冻结中毒计划。");
                if (poison.SourceId != sourceId ||
                    poison.SourceSmokeBefore != operation.Value ||
                    _combatState.Get(sourceId, MachineGunnerCombatantStatus.Smoke) !=
                        poison.SourceSmokeBefore ||
                    !_poisonApplication.ValidatePrepared(poison.Plan))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>提交已联合校验的等量中毒计划，并保持来源烟雾及攻击命中链完全不变。</summary>
        private void CommitPoisonFromSourceSmokeOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            MachineGunnerPreparedPoisonApplication poison =
                operation.PoisonApplication ??
                throw new InvalidOperationException("二手烟提交缺少冻结中毒计划。");
            if (operation.Kind !=
                    MachineGunnerProgramOperationKind.ApplyPoisonFromSourceSmoke ||
                poison.SourceId != sourceId ||
                poison.SourceSmokeBefore != operation.Value)
            {
                throw new InvalidOperationException("二手烟提交的来源或烟雾快照不一致。");
            }

            IReadOnlyList<BattleSettlementRecord> poisonSettlements =
                _poisonApplication.CommitPrepared(poison.Plan, settlements.Count);
            foreach (BattleSettlementRecord settlement in poisonSettlements)
            {
                if (settlement.Order != settlements.Count)
                    throw new InvalidOperationException("二手烟中毒 settlement 顺序不连续。");
                settlements.Add(settlement);
            }
        }

        /// <summary>提交已冻结的职业私有状态累加，并以私有 settlement 保留权威顺序而不伪造通用 Effect。</summary>
        private void CommitPrivateStatusOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            foreach (MachineGunnerPreparedStatusChange change in operation.StatusChanges)
            {
                if (change.Kind != MachineGunnerPreparedStatusChangeKind.PrivateStatus ||
                    !change.PrivateStatus.HasValue)
                {
                    throw new InvalidOperationException("私有状态提交混入了非私有状态预演记录。");
                }
                if (!_combatants.TryGet(change.TargetId, out CombatantData target) || !target.IsAlive)
                {
                    throw new InvalidOperationException("私有状态目标在提交前已失效。");
                }
                if (_combatState.Get(change.TargetId, change.PrivateStatus.Value) != change.ValueBefore)
                {
                    throw new InvalidOperationException("私有状态预演与当前战斗事实不一致。");
                }

                MachineGunnerStatusValueChange actual = _combatState.Add(
                    change.TargetId,
                    change.PrivateStatus.Value,
                    change.Amount);
                if (actual.Before != change.ValueBefore || actual.After != change.ValueAfter)
                {
                    throw new InvalidOperationException("私有状态提交后未得到预演层数。");
                }

                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    settlements.Count,
                    sourceId,
                    change.TargetId,
                    change.PrivateStatus.Value,
                    change.ValueBefore,
                    change.ValueAfter));
            }
        }

        /// <summary>提交预演完成的燃烧施加，并验证真实状态只读取旧浸油、随后以既有规则将其减半。</summary>
        private void CommitBurnOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            foreach (MachineGunnerPreparedBurnApplication application in operation.BurnApplications)
            {
                if (application == null)
                    throw new InvalidOperationException("燃烧提交包含空的目标预演结果。");
                if (!_combatants.TryGet(application.TargetId, out CombatantData target) || !target.IsAlive)
                    throw new InvalidOperationException("燃烧目标在提交前已失效。");

                MachineGunnerBurnApplicationResult expected = application.Result;
                if (_combatState.Get(application.TargetId, MachineGunnerCombatantStatus.Burn) !=
                        expected.BurnChange.Before ||
                    _combatState.Get(application.TargetId, MachineGunnerCombatantStatus.Oil) !=
                        expected.OilChange.Before)
                {
                    throw new InvalidOperationException("燃烧预演与当前职业状态不一致。");
                }

                MachineGunnerBurnApplicationResult actual = _combatState.ApplyBurn(
                    application.TargetId,
                    operation.Value);
                if (actual.BurnChange.Before != expected.BurnChange.Before ||
                    actual.BurnChange.After != expected.BurnChange.After ||
                    actual.OilChange.Before != expected.OilChange.Before ||
                    actual.OilChange.After != expected.OilChange.After)
                {
                    throw new InvalidOperationException("燃烧提交后未得到预演层数。");
                }

                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    settlements.Count,
                    sourceId,
                    application.TargetId,
                    MachineGunnerCombatantStatus.Burn,
                    actual.BurnChange.Before,
                    actual.BurnChange.After));
                if (actual.OilChange.Before != actual.OilChange.After)
                {
                    settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                        settlements.Count,
                        sourceId,
                        application.TargetId,
                        MachineGunnerCombatantStatus.Oil,
                        actual.OilChange.Before,
                        actual.OilChange.After));
                }
            }
        }

        /// <summary>先核对焚风跨参与者的全部快照，再按燃烧、可选浸油、来源烟雾清零的权威顺序一次提交。</summary>
        private void CommitSourceSmokeToTargetBurnOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            MachineGunnerPreparedSmokeToBurnConversion conversion =
                operation.SmokeToBurnConversion ??
                throw new InvalidOperationException("焚风提交缺少专用预演结果。");
            if (conversion.SourceId != sourceId ||
                conversion.SmokeChange.Before != operation.Value)
            {
                throw new InvalidOperationException("焚风提交的来源或烟雾快照与操作不一致。");
            }
            if (!_combatants.TryGet(conversion.SourceId, out CombatantData source) || !source.IsAlive)
                throw new InvalidOperationException("焚风施放者在提交前已失效。");
            if (!_combatants.TryGet(conversion.TargetId, out CombatantData target) || !target.IsAlive)
                throw new InvalidOperationException("焚风目标在提交前已失效。");

            MachineGunnerBurnApplicationResult expected = conversion.BurnApplication;
            if (_combatState.Get(conversion.TargetId, MachineGunnerCombatantStatus.Burn) !=
                    expected.BurnChange.Before ||
                _combatState.Get(conversion.TargetId, MachineGunnerCombatantStatus.Oil) !=
                    expected.OilChange.Before ||
                _combatState.Get(conversion.SourceId, MachineGunnerCombatantStatus.Smoke) !=
                    conversion.SmokeChange.Before)
            {
                throw new InvalidOperationException("焚风预演与当前职业状态事实不一致。");
            }

            MachineGunnerBurnApplicationResult actualBurn = _combatState.ApplyBurn(
                conversion.TargetId,
                operation.Value);
            if (actualBurn.BurnChange.Before != expected.BurnChange.Before ||
                actualBurn.BurnChange.After != expected.BurnChange.After ||
                actualBurn.OilChange.Before != expected.OilChange.Before ||
                actualBurn.OilChange.After != expected.OilChange.After)
            {
                throw new InvalidOperationException("焚风提交后未得到预演的燃烧与浸油结果。");
            }
            settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                settlements.Count,
                sourceId,
                conversion.TargetId,
                MachineGunnerCombatantStatus.Burn,
                actualBurn.BurnChange.Before,
                actualBurn.BurnChange.After));
            if (actualBurn.OilChange.Before != actualBurn.OilChange.After)
            {
                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    settlements.Count,
                    sourceId,
                    conversion.TargetId,
                    MachineGunnerCombatantStatus.Oil,
                    actualBurn.OilChange.Before,
                    actualBurn.OilChange.After));
            }

            MachineGunnerStatusValueChange actualSmoke = _combatState.Set(
                conversion.SourceId,
                MachineGunnerCombatantStatus.Smoke,
                value: 0);
            if (actualSmoke.Before != conversion.SmokeChange.Before ||
                actualSmoke.After != conversion.SmokeChange.After)
            {
                throw new InvalidOperationException("焚风提交后未清空预演的来源烟雾。");
            }
            settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                settlements.Count,
                sourceId,
                conversion.SourceId,
                MachineGunnerCombatantStatus.Smoke,
                actualSmoke.Before,
                actualSmoke.After));
        }

        /// <summary>提交不充分爆燃的全部敌方来源伤害，再按 Encounter 顺序只转换仍存活目标的燃烧；浸油在整个过程保持不变。</summary>
        private void CommitIncompleteCombustionOperation(
            CombatantId playerId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            foreach (MachineGunnerPreparedIncompleteCombustionDamage preparedDamage in
                     operation.IncompleteCombustionDamages)
            {
                if (preparedDamage == null)
                    throw new InvalidOperationException("不充分爆燃提交包含空的伤害预演结果。");

                MachineGunnerPreparedDamage damage = preparedDamage.Damage;
                if (!_combatants.TryGet(damage.TargetId, out CombatantData target))
                    throw new InvalidOperationException("不充分爆燃伤害目标在提交前丢失。");
                if (target.CurrentBlock != damage.Outcome.BlockBefore ||
                    target.CurrentHealth != damage.Outcome.HealthBefore)
                {
                    throw new InvalidOperationException("不充分爆燃伤害预演与当前参与者事实不一致。");
                }
                if (damage.ConsumesArmor)
                {
                    throw new InvalidOperationException("不充分爆燃的 Debuff 伤害不应消耗护甲。");
                }

                target.ApplyDamageOutcome(damage.Outcome);
                settlements.Add(new BattleDamageAppliedSettlement(
                    settlements.Count,
                    null,
                    preparedDamage.SourceId,
                    damage.TargetId,
                    damage.Outcome.AttackValue,
                    damage.Outcome.BlockBefore,
                    damage.Outcome.BlockAfter,
                    damage.Outcome.HealthBefore,
                    damage.Outcome.HealthAfter));
            }

            foreach (MachineGunnerPreparedBurnSmokeConversion conversion in operation.BurnSmokeConversions)
            {
                if (conversion == null)
                    throw new InvalidOperationException("不充分爆燃提交包含空的状态转换预演结果。");
                if (!_combatants.TryGet(conversion.TargetId, out CombatantData target) || !target.IsAlive)
                {
                    throw new InvalidOperationException("不充分爆燃状态转换目标在提交前已失效。");
                }
                if (_combatState.Get(conversion.TargetId, MachineGunnerCombatantStatus.Smoke) !=
                        conversion.SmokeChange.Before ||
                    _combatState.Get(conversion.TargetId, MachineGunnerCombatantStatus.Burn) !=
                        conversion.BurnChange.Before)
                {
                    throw new InvalidOperationException("不充分爆燃状态转换预演与当前职业状态不一致。");
                }

                MachineGunnerStatusValueChange smokeChange = _combatState.Set(
                    conversion.TargetId,
                    MachineGunnerCombatantStatus.Smoke,
                    conversion.SmokeChange.After);
                if (smokeChange.Before != conversion.SmokeChange.Before ||
                    smokeChange.After != conversion.SmokeChange.After)
                {
                    throw new InvalidOperationException("不充分爆燃提交后未得到预演烟雾层数。");
                }
                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    settlements.Count,
                    playerId,
                    conversion.TargetId,
                    MachineGunnerCombatantStatus.Smoke,
                    smokeChange.Before,
                    smokeChange.After));

                MachineGunnerStatusValueChange burnChange = _combatState.Set(
                    conversion.TargetId,
                    MachineGunnerCombatantStatus.Burn,
                    conversion.BurnChange.After);
                if (burnChange.Before != conversion.BurnChange.Before ||
                    burnChange.After != conversion.BurnChange.After)
                {
                    throw new InvalidOperationException("不充分爆燃提交后未得到预演燃烧层数。");
                }
                settlements.Add(new MachineGunnerPrivateStatusChangedSettlement(
                    settlements.Count,
                    playerId,
                    conversion.TargetId,
                    MachineGunnerCombatantStatus.Burn,
                    burnChange.Before,
                    burnChange.After));
            }
        }

        /// <summary>提交已冻结的通用易伤累加，复用 CombatantData 写入入口和既有易伤表现 settlement。</summary>
        private void CommitVulnerableOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            foreach (MachineGunnerPreparedStatusChange change in operation.StatusChanges)
            {
                if (change.Kind != MachineGunnerPreparedStatusChangeKind.Vulnerable ||
                    change.PrivateStatus.HasValue)
                {
                    throw new InvalidOperationException("易伤提交混入了非易伤预演记录。");
                }
                if (!_combatants.TryGet(change.TargetId, out CombatantData target) || !target.IsAlive)
                {
                    throw new InvalidOperationException("易伤目标在提交前已失效。");
                }
                if (target.CurrentVulnerable != change.ValueBefore)
                {
                    throw new InvalidOperationException("易伤预演与当前战斗事实不一致。");
                }

                target.ApplyVulnerableGain(change.Amount);
                if (target.CurrentVulnerable != change.ValueAfter)
                {
                    throw new InvalidOperationException("易伤提交后未得到预演层数。");
                }

                settlements.Add(new BattleStatusAppliedSettlement(
                    settlements.Count,
                    null,
                    sourceId,
                    change.TargetId,
                    BattleStatusType.Vulnerable,
                    change.ValueBefore,
                    change.ValueAfter));
            }
        }

        /// <summary>依次提交已冻结的伤害段，并把每一段映射为现有可表现的伤害 settlement。</summary>
        private void CommitDamageOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            foreach (MachineGunnerPreparedDamage damage in operation.Damages)
            {
                if (!_combatants.TryGet(damage.TargetId, out CombatantData target))
                    throw new InvalidOperationException("机枪兵伤害目标在提交前丢失。");

                target.ApplyDamageOutcome(damage.Outcome);
                if (damage.ConsumesArmor)
                {
                    _combatState.TryConsumeArmorAfterPenetratingAttack(
                        damage.TargetId,
                        new MachineGunnerDamageCalculation(
                            damage.Outcome,
                            consumesArmor: true),
                        out _);
                }
                settlements.Add(new BattleDamageAppliedSettlement(
                    settlements.Count,
                    null,
                    sourceId,
                    damage.TargetId,
                    damage.Outcome.AttackValue,
                    damage.Outcome.BlockBefore,
                    damage.Outcome.BlockAfter,
                    damage.Outcome.HealthBefore,
                    damage.Outcome.HealthAfter));
            }
        }

        /// <summary>提交已预演的格挡增加，并验证参与者当前格挡没有在同一命令内漂移。</summary>
        private void CommitBlockOperation(
            CombatantId sourceId,
            MachineGunnerPreparedOperation operation,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (!_combatants.TryGet(sourceId, out CombatantData source))
                throw new InvalidOperationException("机枪兵格挡来源在提交前丢失。");
            if (source.CurrentBlock != operation.BlockBefore)
                throw new InvalidOperationException("机枪兵格挡预演与当前参与者事实不一致。");

            source.ApplyBlockGain(operation.Value);
            if (source.CurrentBlock != operation.BlockAfter)
                throw new InvalidOperationException("机枪兵格挡提交后未得到预演值。");

            settlements.Add(new BattleBlockGainedSettlement(
                settlements.Count,
                null,
                sourceId,
                sourceId,
                operation.BlockBefore,
                operation.BlockAfter));
        }

        /// <summary>把受硬上限约束的即时能量增加写回回合事实，并只在实际增加时记录专用获得结算。</summary>
        private static void AppendEnergyGainSettlement(
            CombatantId playerId,
            int requestedGain,
            ref PlayerTurnData playerTurnAfter,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (requestedGain <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedGain));

            int energyBefore = playerTurnAfter.Energy;
            long requestedAfter = (long)energyBefore + requestedGain;
            int energyAfter = requestedAfter >= playerTurnAfter.EnergyMaximum
                ? playerTurnAfter.EnergyMaximum
                : (int)requestedAfter;
            if (energyAfter == energyBefore)
                return;

            playerTurnAfter = playerTurnAfter.WithEnergy(energyAfter);
            settlements.Add(new BattleEnergyGainedSettlement(
                settlements.Count,
                playerId,
                energyBefore,
                energyAfter));
        }

        /// <summary>计算冻结普通抽牌的首条记录序号，并要求其前方操作都能在准备阶段精确给出结算数量。</summary>
        private static int DeterminePreparedDrawStartingOrder(
            IReadOnlyList<MachineGunnerPreparedOperation> operations,
            int drawOperationIndex,
            int currentSettlementCount)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (drawOperationIndex < 0 || drawOperationIndex >= operations.Count)
                throw new ArgumentOutOfRangeException(nameof(drawOperationIndex));
            if (currentSettlementCount < 0)
                throw new ArgumentOutOfRangeException(nameof(currentSettlementCount));

            int startingOrder = currentSettlementCount;
            for (int index = 0; index < drawOperationIndex; index++)
            {
                startingOrder = checked(
                    startingOrder + GetPreparedOperationSettlementCount(operations[index]));
            }

            return startingOrder;
        }

        /// <summary>计算换手复合操作的首条记录序号，并只允许其前方出现结算数量确定的一条或多条格挡操作。</summary>
        private static int DetermineHandReplacementStartingOrder(
            IReadOnlyList<MachineGunnerPreparedOperation> operations,
            int replacementOperationIndex,
            int currentSettlementCount)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (replacementOperationIndex < 0 || replacementOperationIndex >= operations.Count)
                throw new ArgumentOutOfRangeException(nameof(replacementOperationIndex));
            if (currentSettlementCount < 0)
                throw new ArgumentOutOfRangeException(nameof(currentSettlementCount));

            int startingOrder = currentSettlementCount;
            for (int index = 0; index < replacementOperationIndex; index++)
            {
                MachineGunnerPreparedOperation precedingOperation = operations[index];
                if (precedingOperation.Kind != MachineGunnerProgramOperationKind.GainBlock)
                {
                    throw new InvalidOperationException(
                        "离手弃牌并创建临时牌之前只允许声明结算数量确定的格挡操作。");
                }

                startingOrder = checked(startingOrder + 1);
            }

            return startingOrder;
        }

        /// <summary>在离手抽牌计划冻结前，只投影不会写入战斗聚合的本地资源操作，并拒绝跨越任何有副作用的操作。</summary>
        private static void AppendLocalResourceOperationBeforePlayedCardDeparture(
            CombatantId playerId,
            MachineGunnerPreparedOperation operation,
            ref PlayerTurnData playerTurnAfter,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            switch (operation.Kind)
            {
                case MachineGunnerProgramOperationKind.GainEnergy:
                    AppendEnergyGainSettlement(
                        playerId,
                        operation.Value,
                        ref playerTurnAfter,
                        settlements);
                    break;
                case MachineGunnerProgramOperationKind.GainAmmo:
                    AppendAmmoGainSettlement(
                        playerId,
                        operation.Value,
                        ref playerTurnAfter,
                        settlements);
                    break;
                case MachineGunnerProgramOperationKind.FillAmmo:
                    AppendAmmoFillSettlement(
                        playerId,
                        ref playerTurnAfter,
                        settlements);
                    break;
                default:
                    throw new InvalidOperationException(
                        "离手后抽牌前只能声明可在返回值中完成的本地资源操作。");
            }
        }

        /// <summary>把有上限的弹药增加写回返回回合事实，并只在实际数值变化时记录现有补充结算。</summary>
        private static void AppendAmmoGainSettlement(
            CombatantId playerId,
            int requestedGain,
            ref PlayerTurnData playerTurnAfter,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (requestedGain <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedGain));

            int ammoBefore = playerTurnAfter.Ammo;
            long requestedAfter = (long)ammoBefore + requestedGain;
            int ammoAfter = requestedAfter >= playerTurnAfter.AmmoMaximum
                ? playerTurnAfter.AmmoMaximum
                : (int)requestedAfter;
            if (ammoAfter == ammoBefore)
                return;

            playerTurnAfter = playerTurnAfter.WithAmmo(ammoAfter);
            settlements.Add(new BattleAmmoRefilledSettlement(
                settlements.Count,
                playerId,
                ammoBefore,
                ammoAfter));
        }

        /// <summary>按已冻结的正数支付更新回合弹药并追加一条支付记录；资源漂移会提升为事务不变量错误。</summary>
        private static void AppendAmmoSpendSettlement(
            CombatantId playerId,
            int requestedSpend,
            ref PlayerTurnData playerTurnAfter,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (requestedSpend <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedSpend));
            if (playerTurnAfter == null)
                throw new ArgumentNullException(nameof(playerTurnAfter));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (requestedSpend > playerTurnAfter.Ammo)
            {
                throw new InvalidOperationException(
                    "换弹连射的冻结弹药支付超过了当前有序资源轨迹。");
            }

            int ammoBefore = playerTurnAfter.Ammo;
            int ammoAfter = ammoBefore - requestedSpend;
            playerTurnAfter = playerTurnAfter.WithAmmo(ammoAfter);
            settlements.Add(new BattleAmmoSpentSettlement(
                settlements.Count,
                playerId,
                ammoBefore,
                ammoAfter));
        }

        /// <summary>把补满弹药投影写回返回的 PlayerTurnData，并在实际变化时追加现有补充记录。</summary>
        private static void AppendAmmoFillSettlement(
            CombatantId playerId,
            ref PlayerTurnData playerTurnAfter,
            ICollection<BattleSettlementRecord> settlements)
        {
            int ammoAfter = playerTurnAfter.AmmoMaximum;
            if (ammoAfter == playerTurnAfter.Ammo)
                return;

            int ammoBefore = playerTurnAfter.Ammo;
            playerTurnAfter = playerTurnAfter.WithAmmo(ammoAfter);
            settlements.Add(new BattleAmmoRefilledSettlement(
                settlements.Count,
                playerId,
                ammoBefore,
                ammoAfter));
        }

        /// <summary>追加已成功执行的卡区记录，并把卡区内部失败提升为事务不变量错误。</summary>
        private static void AppendCardZoneResult(
            BattleCardZoneOperationResult result,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.Succeeded)
                throw new InvalidOperationException("机枪兵程序预校验后卡区操作意外失败。");

            foreach (BattleSettlementRecord settlement in result.Settlements)
            {
                if (settlement.Order != settlements.Count)
                    throw new InvalidOperationException("机枪兵卡区 settlement 顺序不连续。");
                settlements.Add(settlement);
            }
        }

        /// <summary>追加一条已由复合卡区计划冻结的移动记录，并要求其逻辑序号与当前命令严格连续。</summary>
        private static void AppendCardZoneSettlement(
            BattleSettlementRecord settlement,
            ICollection<BattleSettlementRecord> settlements)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            if (!(settlement is BattleCardMovedSettlement))
                throw new InvalidOperationException("复合手牌选择计划只能追加卡牌移动记录。");
            if (settlement.Order != settlements.Count)
                throw new InvalidOperationException("复合手牌选择 settlement 顺序不连续。");

            settlements.Add(settlement);
        }

        /// <summary>把配置中的出牌归宿映射为卡区深模块认识的稳定归宿。</summary>
        private static BattleCardZone MapPlayedCardDestination(
            cfg.battle.CardPlayDestination destination)
        {
            switch (destination)
            {
                case cfg.battle.CardPlayDestination.DiscardPile:
                    return BattleCardZone.DiscardPile;
                case cfg.battle.CardPlayDestination.ExhaustPile:
                    return BattleCardZone.ExhaustPile;
                case cfg.battle.CardPlayDestination.Power:
                    return BattleCardZone.PowerPile;
                default:
                    throw new ArgumentOutOfRangeException(nameof(destination));
            }
        }

        /// <summary>冻结驻防要求的两张唯一其他手牌，并返回与公共规则一致的稳定失败原因。</summary>
        private static BattleCommandExecutionFailureReason TryFreezeGarrisonSelection(
            PlayCardCommand command,
            BattleCardZonesData cardZones,
            out IReadOnlyList<CardInstanceId> retainedCardIds)
        {
            retainedCardIds = Array.Empty<CardInstanceId>();
            if (command.SelectedCardIds.Count != 2)
                return BattleCommandExecutionFailureReason.InvalidCardSelectionCount;

            var uniqueCardIds = new HashSet<CardInstanceId>();
            var frozenCardIds = new List<CardInstanceId>(capacity: 2);
            foreach (CardInstanceId selectedCardId in command.SelectedCardIds)
            {
                if (!uniqueCardIds.Add(selectedCardId))
                    return BattleCommandExecutionFailureReason.InvalidCardSelectionCount;
                if (selectedCardId == command.CardId)
                    return BattleCommandExecutionFailureReason.SelectedCardNotEligible;
                if (!IsCardInHand(cardZones, selectedCardId))
                    return BattleCommandExecutionFailureReason.SelectedCardNotInHand;

                frozenCardIds.Add(selectedCardId);
            }

            retainedCardIds = new ReadOnlyCollection<CardInstanceId>(frozenCardIds);
            return BattleCommandExecutionFailureReason.None;
        }

        /// <summary>在首次权威写入前确认驻防冻结的两张牌仍与当前手牌快照一致。</summary>
        private static bool IsGarrisonSelectionSnapshotCurrent(
            PlayCardCommand command,
            BattleCardZonesData cardZones,
            IReadOnlyList<CardInstanceId> retainedCardIds)
        {
            if (retainedCardIds == null || retainedCardIds.Count != 2)
                return false;

            var uniqueCardIds = new HashSet<CardInstanceId>();
            foreach (CardInstanceId retainedCardId in retainedCardIds)
            {
                if (retainedCardId == command.CardId ||
                    !uniqueCardIds.Add(retainedCardId) ||
                    !IsCardInHand(cardZones, retainedCardId))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>只读确认当前实例仍处于手牌，防止测试或错误调用绕开程序前置条件。</summary>
        private static bool IsCardInHand(BattleCardZonesData cardZones, CardInstanceId cardId)
        {
            foreach (CardInstanceId handCardId in cardZones.Hand)
            {
                if (handCardId == cardId)
                    return true;
            }

            return false;
        }

        /// <summary>在一条通用 Effect 链中冻结机枪兵伤害与一次性受击防御；所有预约都只保存在本对象，直到 Effect 计划通过校验后才会写回运行时。</summary>
        private sealed class MachineGunnerDamageFormulaSequence : IBattleDamageFormulaOverrideSequence
        {
            private readonly MachineGunnerBattleRuntime _runtime;
            private readonly Dictionary<CombatantId, int> _remainingBufferByTarget;
            private readonly Dictionary<CombatantId, int> _remainingIntangibleByTarget;
            private readonly Queue<MachineGunnerIncomingAttackAftermath> _aftermath;
            private int _plannedAftermathSettlementCount;

            /// <summary>创建一条绑定当前机枪兵运行时、但不写入其可变状态的 Effect 伤害序列。</summary>
            internal MachineGunnerDamageFormulaSequence(MachineGunnerBattleRuntime runtime)
            {
                _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
                _remainingBufferByTarget = new Dictionary<CombatantId, int>();
                _remainingIntangibleByTarget = new Dictionary<CombatantId, int>();
                _aftermath = new Queue<MachineGunnerIncomingAttackAftermath>();
            }

            /// <summary>返回本条 Effect 链预演得到的私有受击防御消费记录数量，供外层在第一次写入前验证连续 order 范围。</summary>
            int IBattleDamageFormulaOverrideSequence.PlannedAftermathSettlementCount =>
                _plannedAftermathSettlementCount;

            /// <summary>计算一段通用攻击伤害，并在局部序列中预约缓冲或无实体而不改变真实战斗状态。</summary>
            BattleDamageFormulaOutcome IBattleDamageFormulaOverrideSequence.Calculate(
                CombatantData source,
                int sourceStrength,
                CombatantId targetId,
                int configuredValue,
                BattleEffectTargetSnapshot target)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                BattleDamageFormulaOutcome outcome = _runtime.CalculateGenericAttackDamage(
                    source,
                    sourceStrength,
                    targetId,
                    configuredValue,
                    target);
                int bufferBefore = 0;
                bool consumesBuffer = _runtime.SupportsPlayer(targetId) &&
                    outcome.AttackValue > 0 &&
                    TryReserveBuffer(targetId, out bufferBefore);
                if (consumesBuffer)
                {
                    outcome = MachineGunnerDamagePipeline.PreventAttackWithBuffer(outcome);
                }
                int intangibleBefore = 0;
                bool consumesIntangible = !consumesBuffer &&
                    _runtime.SupportsPlayer(targetId) &&
                    outcome.AttackValue > 0 &&
                    TryReserveIntangible(targetId, out intangibleBefore);
                if (consumesIntangible)
                    outcome = MachineGunnerDamagePipeline.CapAttackWithIntangible(outcome);
                if (consumesBuffer || consumesIntangible)
                {
                    _plannedAftermathSettlementCount = checked(
                        _plannedAftermathSettlementCount + 1);
                }

                _aftermath.Enqueue(new MachineGunnerIncomingAttackAftermath(
                    source.Id,
                    targetId,
                    outcome,
                    consumesBuffer,
                    consumesBuffer ? bufferBefore : 0,
                    consumesIntangible,
                    consumesIntangible ? intangibleBefore : 0));
                return outcome;
            }

            /// <summary>在对应伤害主记录提交后按预演结果消费护甲及一项私有受击防御；变化以独立私有状态 settlement 跟随伤害主记录。</summary>
            IReadOnlyList<BattleSettlementRecord>
                IBattleDamageFormulaOverrideSequence.CommitDamageAftermath(
                    CombatantId sourceId,
                    CombatantId targetId,
                    BattleDamageFormulaOutcome damageOutcome,
                    int startingOrder)
            {
                if (startingOrder < 0)
                    throw new ArgumentOutOfRangeException(nameof(startingOrder));
                if (_aftermath.Count == 0)
                {
                    throw new InvalidOperationException(
                        "机枪兵伤害后效提交找不到对应的预演段。");
                }

                MachineGunnerIncomingAttackAftermath prepared = _aftermath.Dequeue();
                if (prepared.SourceId != sourceId || prepared.TargetId != targetId ||
                    !Matches(prepared.Outcome, damageOutcome))
                {
                    throw new InvalidOperationException(
                        "机枪兵伤害后效提交与预演段不一致。");
                }

                if (damageOutcome.HealthLoss > 0)
                {
                    _runtime._combatState.TryConsumeArmorAfterPenetratingAttack(
                        targetId,
                        new MachineGunnerDamageCalculation(
                            damageOutcome,
                            consumesArmor: true),
                        out _);
                }

                if (!prepared.ConsumesBuffer && !prepared.ConsumesIntangible)
                    return Array.Empty<BattleSettlementRecord>();

                var settlements = new List<BattleSettlementRecord>(capacity: 1);
                if (prepared.ConsumesBuffer)
                {
                    settlements.Add(CommitReservedDefense(
                        targetId,
                        MachineGunnerCombatantStatus.Buffer,
                        prepared.BufferBefore,
                        checked(startingOrder + settlements.Count)));
                }
                if (prepared.ConsumesIntangible)
                {
                    settlements.Add(CommitReservedDefense(
                        targetId,
                        MachineGunnerCombatantStatus.Intangible,
                        prepared.IntangibleBefore,
                        checked(startingOrder + settlements.Count)));
                }

                return settlements.AsReadOnly();
            }

            /// <summary>读取并在本条 Effect 链内预留一层缓冲；真实状态保持不变，后续多段伤害只能看到递减后的局部层数。</summary>
            private bool TryReserveBuffer(CombatantId targetId, out int bufferBefore)
            {
                if (!_remainingBufferByTarget.TryGetValue(targetId, out bufferBefore))
                {
                    bufferBefore = _runtime._combatState.Get(
                        targetId,
                        MachineGunnerCombatantStatus.Buffer);
                    _remainingBufferByTarget.Add(targetId, bufferBefore);
                }

                if (bufferBefore <= 0)
                    return false;

                _remainingBufferByTarget[targetId] = bufferBefore - 1;
                return true;
            }

            /// <summary>读取并在本条 Effect 链内预留一层无实体；缓冲优先时调用方不会进入本方法。</summary>
            private bool TryReserveIntangible(CombatantId targetId, out int intangibleBefore)
            {
                if (!_remainingIntangibleByTarget.TryGetValue(targetId, out intangibleBefore))
                {
                    intangibleBefore = _runtime._combatState.Get(
                        targetId,
                        MachineGunnerCombatantStatus.Intangible);
                    _remainingIntangibleByTarget.Add(targetId, intangibleBefore);
                }

                if (intangibleBefore <= 0)
                    return false;

                _remainingIntangibleByTarget[targetId] = intangibleBefore - 1;
                return true;
            }

            /// <summary>提交一项已经在局部序列预留的受击防御层数，并返回与伤害主记录连续的私有状态记录。</summary>
            private BattleSettlementRecord CommitReservedDefense(
                CombatantId targetId,
                MachineGunnerCombatantStatus status,
                int before,
                int settlementOrder)
            {
                if (before <= 0)
                    throw new ArgumentOutOfRangeException(nameof(before));

                MachineGunnerStatusValueChange change = _runtime._combatState.Set(
                    targetId,
                    status,
                    before - 1);
                if (change.Before != before || change.After != before - 1)
                {
                    throw new InvalidOperationException(
                        "机枪兵受击防御提交与预演层数不一致。");
                }

                return new MachineGunnerPrivateStatusChangedSettlement(
                    settlementOrder,
                    _runtime._playerId,
                    targetId,
                    status,
                    change.Before,
                    change.After);
            }

            /// <summary>比较两份已经冻结的伤害推演，防止提交顺序或目标漂移时错误消费另一段的私有防御。</summary>
            private static bool Matches(
                BattleDamageFormulaOutcome expected,
                BattleDamageFormulaOutcome actual)
            {
                return expected.AttackValue == actual.AttackValue &&
                    expected.BlockBefore == actual.BlockBefore &&
                    expected.BlockAfter == actual.BlockAfter &&
                    expected.BlockAbsorbed == actual.BlockAbsorbed &&
                    expected.HealthBefore == actual.HealthBefore &&
                    expected.HealthAfter == actual.HealthAfter &&
                    expected.HealthLoss == actual.HealthLoss &&
                    expected.WasFatal == actual.WasFatal;
            }
        }

        /// <summary>以现有机枪手逐段命中准备器冻结主伤害、燃烧弹药与便携助手，并适配共享重复伤害协议。</summary>
        private sealed class MachineGunnerRepeatedDamageHitSequence :
            IBattleRepeatedDamageHitSequence
        {
            private readonly MachineGunnerBattleRuntime _runtime;
            private readonly CombatantId _sourceId;
            private readonly CombatantId _targetId;
            private readonly MachineGunnerCardProgram _program;
            private readonly MachineGunnerCostResolution _cost;
            private readonly int _stimTurnsBefore;
            private readonly int _incendiaryStacksBefore;
            private readonly int _helperStacksBefore;
            private readonly bool _targetPoisonWasActive;
            private readonly Dictionary<CombatantId, int[]> _privateStatusSnapshots;
            private readonly Dictionary<CombatantId, MachineGunnerProjectedCombatant>
                _projectedTargets;
            private readonly List<MachineGunnerPreparedRepeatedHit> _hits;
            private bool _isValidated;
            private int _nextCommitIndex;

            /// <summary>返回全部主伤害、燃烧及助手冻结操作会产生的 settlement 总数。</summary>
            public int PlannedSettlementCount { get; private set; }

            /// <summary>冻结运行时归属、能力层数、目标中毒活跃性和全部参与者十七项私有状态快照。</summary>
            internal MachineGunnerRepeatedDamageHitSequence(
                MachineGunnerBattleRuntime runtime,
                CombatantId sourceId,
                CombatantId targetId,
                MachineGunnerCardProgram program,
                MachineGunnerCostResolution cost)
            {
                _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
                _sourceId = sourceId;
                _targetId = targetId;
                _program = program ?? throw new ArgumentNullException(nameof(program));
                _cost = cost;
                if (!runtime._combatants.TryGet(targetId, out CombatantData target))
                    throw new InvalidOperationException("幻彩射击冻结中毒活跃性时目标不存在。");
                _targetPoisonWasActive = target.CurrentPoison > 0;
                _stimTurnsBefore = runtime._stimTurns;
                _incendiaryStacksBefore = runtime.GetPowerStack(
                    MachineGunnerPowerKind.IncendiaryAmmo);
                _helperStacksBefore = runtime.GetPowerStack(
                    MachineGunnerPowerKind.PortableHelper);
                _privateStatusSnapshots = runtime.CaptureAllPrivateStatusSnapshots();
                _projectedTargets = runtime.CreateProjectedCombatants();
                _hits = new List<MachineGunnerPreparedRepeatedHit>();
            }

            /// <summary>复用现有主伤害至燃烧再至助手的准备器，并返回全部后效完成后的目标投影。</summary>
            public BattleRepeatedDamageHitPreparation PrepareHit(
                CombatantData source,
                BattleRepeatedDamageHitRequest hit,
                CombatantId targetId,
                BattleEffectTargetSnapshot projectedTarget)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (_isValidated)
                    throw new InvalidOperationException("已校验的机枪手重复伤害序列不能继续预演。");
                if (source.Id != _sourceId || targetId != _targetId)
                    throw new InvalidOperationException("机枪手重复伤害序列的来源或固定目标已漂移。");
                if (!_projectedTargets.TryGetValue(
                        targetId,
                        out MachineGunnerProjectedCombatant projected) ||
                    projected.Health != projectedTarget.Health ||
                    projected.Block != projectedTarget.Block ||
                    projected.Vulnerable != projectedTarget.Vulnerable)
                {
                    throw new InvalidOperationException("共享目标投影与机枪手私有投影不一致。");
                }

                var operations = new List<MachineGunnerPreparedOperation>();
                bool appended = _runtime.AppendPreparedHitAndPostHitOperations(
                    _sourceId,
                    source,
                    targetId,
                    _program,
                    new MachineGunnerProgramOperation(
                        MachineGunnerProgramOperationKind.Damage,
                        hit.ConfiguredValue),
                    _cost,
                    _projectedTargets,
                    operations,
                    hit.ConfiguredValue);
                if (!appended || operations.Count == 0 || operations[0].Damages.Count != 1)
                    throw new InvalidOperationException("存活固定目标未能冻结幻彩射击来源段。");

                MachineGunnerPreparedDamage primaryDamage = operations[0].Damages[0];
                foreach (MachineGunnerPreparedOperation operation in operations)
                {
                    if (operation.Kind == MachineGunnerProgramOperationKind.Damage)
                    {
                        foreach (MachineGunnerPreparedDamage damage in operation.Damages)
                        {
                            if (!damage.ConsumesArmor)
                                continue;
                            MachineGunnerProjectedCombatant armorTarget =
                                _projectedTargets[damage.TargetId];
                            int armorBefore = armorTarget.GetPrivateStatus(
                                MachineGunnerCombatantStatus.Armor);
                            if (armorBefore > 0)
                            {
                                armorTarget.SetPrivateStatus(
                                    MachineGunnerCombatantStatus.Armor,
                                    armorBefore - 1);
                            }
                        }
                    }
                }
                int settlementCount = 0;
                foreach (MachineGunnerPreparedOperation operation in operations)
                {
                    settlementCount = checked(
                        settlementCount + GetPreparedOperationSettlementCount(operation));
                }
                PlannedSettlementCount = checked(
                    PlannedSettlementCount + settlementCount);
                _hits.Add(new MachineGunnerPreparedRepeatedHit(
                    hit,
                    targetId,
                    primaryDamage.Outcome,
                    operations));
                return new BattleRepeatedDamageHitPreparation(
                    primaryDamage.Outcome,
                    new BattleEffectTargetSnapshot(
                        projected.Health,
                        projected.Block,
                        projected.Vulnerable));
            }

            /// <summary>在首写前校验运行时能力、目标中毒活跃性、十七项私有状态与共享冻结段顺序。</summary>
            public void ValidatePrepared(
                IReadOnlyList<BattlePreparedRepeatedDamageSegment> segments)
            {
                if (segments == null)
                    throw new ArgumentNullException(nameof(segments));
                if (_isValidated || _nextCommitIndex != 0)
                    throw new InvalidOperationException("机枪手重复伤害序列已经校验或提交。");
                if (_runtime._stimTurns != _stimTurnsBefore ||
                    _runtime.GetPowerStack(MachineGunnerPowerKind.IncendiaryAmmo) !=
                        _incendiaryStacksBefore ||
                    _runtime.GetPowerStack(MachineGunnerPowerKind.PortableHelper) !=
                        _helperStacksBefore ||
                    !_runtime.MatchesAllPrivateStatusSnapshots(_privateStatusSnapshots))
                {
                    throw new InvalidOperationException(
                        "幻彩射击的兴奋剂、能力或私有状态快照在首写前已漂移。");
                }
                if (!_runtime._combatants.TryGet(_targetId, out CombatantData target) ||
                    (target.CurrentPoison > 0) != _targetPoisonWasActive)
                {
                    throw new InvalidOperationException(
                        "幻彩射击的目标中毒活跃性在首写前已漂移。");
                }
                if (segments.Count != _hits.Count)
                    throw new InvalidOperationException("幻彩射击共享段与职业段数量不一致。");
                for (int index = 0; index < segments.Count; index++)
                {
                    if (!_hits[index].Matches(segments[index]))
                        throw new InvalidOperationException("幻彩射击冻结段顺序或结果已漂移。");
                }

                _isValidated = true;
            }

            /// <summary>按冻结顺序提交主伤害、燃烧与助手操作，不再解释职业规则。</summary>
            public IReadOnlyList<BattleSettlementRecord> CommitPreparedHit(
                BattlePreparedRepeatedDamageSegment segment,
                int startingOrder)
            {
                if (startingOrder < 0)
                    throw new ArgumentOutOfRangeException(nameof(startingOrder));
                if (!_isValidated || _nextCommitIndex >= _hits.Count)
                    throw new InvalidOperationException("机枪手重复伤害序列尚未校验或已经提交完毕。");

                MachineGunnerPreparedRepeatedHit hit = _hits[_nextCommitIndex];
                if (!hit.Matches(segment))
                    throw new InvalidOperationException("幻彩射击提交段顺序已漂移。");
                _nextCommitIndex++;

                var settlements = new List<BattleSettlementRecord>();
                var offsetSettlements = new OffsetSettlementCollection(
                    startingOrder,
                    settlements);
                foreach (MachineGunnerPreparedOperation operation in hit.Operations)
                    _runtime.CommitScheduledOperation(_sourceId, operation, offsetSettlements);
                return settlements.AsReadOnly();
            }
        }

        /// <summary>保存一个逻辑来源段及其主伤害、燃烧和助手冻结操作。</summary>
        private sealed class MachineGunnerPreparedRepeatedHit
        {
            internal BattleRepeatedDamageHitRequest Request { get; }
            internal CombatantId TargetId { get; }
            internal BattleDamageFormulaOutcome PrimaryOutcome { get; }
            internal IReadOnlyList<MachineGunnerPreparedOperation> Operations { get; }

            /// <summary>冻结一段幻彩射击的职业操作顺序。</summary>
            internal MachineGunnerPreparedRepeatedHit(
                BattleRepeatedDamageHitRequest request,
                CombatantId targetId,
                BattleDamageFormulaOutcome primaryOutcome,
                IEnumerable<MachineGunnerPreparedOperation> operations)
            {
                if (operations == null)
                    throw new ArgumentNullException(nameof(operations));
                Request = request;
                TargetId = targetId;
                PrimaryOutcome = primaryOutcome;
                Operations = new ReadOnlyCollection<MachineGunnerPreparedOperation>(
                    new List<MachineGunnerPreparedOperation>(operations));
            }

            /// <summary>比较共享段与职业段的身份、基础值及完整主伤害结果。</summary>
            internal bool Matches(BattlePreparedRepeatedDamageSegment segment)
            {
                return Request.EffectId == segment.EffectId &&
                    Request.ConfiguredValue == segment.ConfiguredValue &&
                    TargetId == segment.TargetId &&
                    OutcomesMatch(PrimaryOutcome, segment.Outcome);
            }

            /// <summary>比较两份冻结伤害结果的全部公开数值。</summary>
            private static bool OutcomesMatch(
                BattleDamageFormulaOutcome expected,
                BattleDamageFormulaOutcome actual)
            {
                return expected.AttackValue == actual.AttackValue &&
                    expected.BlockBefore == actual.BlockBefore &&
                    expected.BlockAfter == actual.BlockAfter &&
                    expected.BlockAbsorbed == actual.BlockAbsorbed &&
                    expected.HealthBefore == actual.HealthBefore &&
                    expected.HealthAfter == actual.HealthAfter &&
                    expected.HealthLoss == actual.HealthLoss &&
                    expected.WasFatal == actual.WasFatal;
            }
        }

        /// <summary>单段通用攻击在预演时冻结的来源、目标、结果以及受击防御消费，提交阶段不得重新解释这些语义。</summary>
        private sealed class MachineGunnerIncomingAttackAftermath
        {
            /// <summary>预演伤害的来源参与者。</summary>
            internal CombatantId SourceId { get; }

            /// <summary>预演伤害的目标参与者。</summary>
            internal CombatantId TargetId { get; }

            /// <summary>已计入缓冲或无实体后的伤害结果。</summary>
            internal BattleDamageFormulaOutcome Outcome { get; }

            /// <summary>本段是否预约消费一层缓冲。</summary>
            internal bool ConsumesBuffer { get; }

            /// <summary>若预约缓冲，提交前真实状态必须保持的缓冲层数。</summary>
            internal int BufferBefore { get; }

            /// <summary>本段是否预约消费一层无实体。</summary>
            internal bool ConsumesIntangible { get; }

            /// <summary>若预约无实体，提交前真实状态必须保持的无实体层数。</summary>
            internal int IntangibleBefore { get; }

            /// <summary>冻结一段攻击对应的最小后效事实。</summary>
            internal MachineGunnerIncomingAttackAftermath(
                CombatantId sourceId,
                CombatantId targetId,
                BattleDamageFormulaOutcome outcome,
                bool consumesBuffer,
                int bufferBefore,
                bool consumesIntangible,
                int intangibleBefore)
            {
                if (bufferBefore < 0 || (consumesBuffer && bufferBefore <= 0) ||
                    (!consumesBuffer && bufferBefore != 0))
                {
                    throw new ArgumentOutOfRangeException(nameof(bufferBefore));
                }
                if (intangibleBefore < 0 ||
                    (consumesIntangible && intangibleBefore <= 0) ||
                    (!consumesIntangible && intangibleBefore != 0))
                {
                    throw new ArgumentOutOfRangeException(nameof(intangibleBefore));
                }
                if (consumesBuffer && consumesIntangible)
                {
                    throw new ArgumentException("缓冲优先的单段攻击不能同时消费无实体。");
                }

                SourceId = sourceId;
                TargetId = targetId;
                Outcome = outcome;
                ConsumesBuffer = consumesBuffer;
                BufferBefore = bufferBefore;
                ConsumesIntangible = consumesIntangible;
                IntangibleBefore = intangibleBefore;
            }
        }

        /// <summary>让既有提交器以命令内全局 order 写入局部结果列表的受限集合。</summary>
        private sealed class OffsetSettlementCollection : ICollection<BattleSettlementRecord>
        {
            private readonly int _offset;
            private readonly List<BattleSettlementRecord> _inner;

            /// <summary>返回包含全局起始偏移的下一记录序号。</summary>
            public int Count => checked(_offset + _inner.Count);

            /// <summary>本集合允许既有提交器追加记录。</summary>
            public bool IsReadOnly => false;

            /// <summary>绑定全局起始序号与实际保存记录的局部列表。</summary>
            internal OffsetSettlementCollection(
                int offset,
                List<BattleSettlementRecord> inner)
            {
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset));

                _offset = offset;
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            /// <summary>仅接受与当前全局下一序号一致的预构建记录。</summary>
            public void Add(BattleSettlementRecord item)
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));
                if (item.Order != Count)
                    throw new InvalidOperationException("调度效果 settlement 的全局 order 不连续。");

                _inner.Add(item);
            }

            /// <summary>调度提交不允许通过集合接口清除既有记录。</summary>
            public void Clear()
            {
                throw new NotSupportedException();
            }

            /// <summary>按引用判断实际局部列表是否包含指定记录。</summary>
            public bool Contains(BattleSettlementRecord item)
            {
                return _inner.Contains(item);
            }

            /// <summary>把已经提交的局部记录复制到调用方数组。</summary>
            public void CopyTo(BattleSettlementRecord[] array, int arrayIndex)
            {
                _inner.CopyTo(array, arrayIndex);
            }

            /// <summary>调度提交不允许通过集合接口移除单条记录。</summary>
            public bool Remove(BattleSettlementRecord item)
            {
                throw new NotSupportedException();
            }

            /// <summary>按局部保存顺序枚举已经提交的记录。</summary>
            public IEnumerator<BattleSettlementRecord> GetEnumerator()
            {
                return _inner.GetEnumerator();
            }

            /// <summary>为非泛型枚举调用返回同一局部记录顺序。</summary>
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>单段程序预演使用的参与者可变投影。</summary>
        private sealed class MachineGunnerProjectedCombatant
        {
            /// <summary>当前预演中的生命值。</summary>
            internal int Health { get; set; }

            /// <summary>当前预演中的格挡值。</summary>
            internal int Block { get; set; }

            /// <summary>当前预演中的通用易伤值。</summary>
            internal int Vulnerable { get; set; }

            private readonly Dictionary<MachineGunnerCombatantStatus, int> _privateStatuses;

            /// <summary>冻结一名参与者在命令开始时用于多段伤害的必要标量。</summary>
            internal MachineGunnerProjectedCombatant(
                int health,
                int block,
                int vulnerable,
                MachineGunnerCombatState combatState,
                CombatantId combatantId)
            {
                if (health < 0)
                    throw new ArgumentOutOfRangeException(nameof(health));
                if (block < 0)
                    throw new ArgumentOutOfRangeException(nameof(block));
                if (vulnerable < 0)
                    throw new ArgumentOutOfRangeException(nameof(vulnerable));
                if (combatState == null)
                    throw new ArgumentNullException(nameof(combatState));

                Health = health;
                Block = block;
                Vulnerable = vulnerable;
                _privateStatuses = new Dictionary<MachineGunnerCombatantStatus, int>();
                foreach (MachineGunnerCombatantStatus status in
                         (MachineGunnerCombatantStatus[])Enum.GetValues(
                             typeof(MachineGunnerCombatantStatus)))
                {
                    _privateStatuses.Add(status, combatState.Get(combatantId, status));
                }
            }

            /// <summary>读取预演快照中的职业私有状态层数。</summary>
            internal int GetPrivateStatus(MachineGunnerCombatantStatus status)
            {
                if (!_privateStatuses.TryGetValue(status, out int value))
                    throw new ArgumentOutOfRangeException(nameof(status));

                return value;
            }

            /// <summary>写入已经校验为非负数的职业私有状态投影。</summary>
            internal void SetPrivateStatus(MachineGunnerCombatantStatus status, int value)
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (!_privateStatuses.ContainsKey(status))
                    throw new ArgumentOutOfRangeException(nameof(status));

                _privateStatuses[status] = value;
            }
        }
    }
}
