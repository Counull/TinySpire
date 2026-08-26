using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>把卡牌模板与实例升级等级解析为战斗可消费的不可变配置投影。</summary>
    public sealed class BattleCardLevelProjection
    {
        /// <summary>投影所引用的原始卡牌模板。</summary>
        public cfg.battle.Card Template { get; }

        /// <summary>本投影对应的实例升级等级。</summary>
        public int UpgradeLevel { get; }

        /// <summary>当前等级应使用的说明文本本地化键。</summary>
        public string DescriptionI18nKey { get; }

        /// <summary>当前等级应使用的卡牌费用。</summary>
        public int Cost { get; }

        /// <summary>当前等级成功打出后应进入的卡区。</summary>
        public cfg.battle.CardPlayDestination PlayDestination { get; }

        /// <summary>当前等级由通用效果执行器消费的伤害值；没有对应伤害效果时为空。</summary>
        public int? EffectDamageValue { get; }

        /// <summary>当前等级由职业程序消费的伤害值；没有对应伤害程序时为空。</summary>
        public int? ProgramDamageValue { get; }

        /// <summary>当前等级是否还能合法提升一级。</summary>
        public bool CanUpgradeOneLevel { get; }

        /// <summary>创建一份已经完成配置和等级合法性校验的不可变投影。</summary>
        private BattleCardLevelProjection(
            cfg.battle.Card template,
            int upgradeLevel,
            string descriptionI18nKey,
            int cost,
            cfg.battle.CardPlayDestination playDestination,
            int? effectDamageValue,
            int? programDamageValue,
            bool canUpgradeOneLevel)
        {
            Template = template;
            UpgradeLevel = upgradeLevel;
            DescriptionI18nKey = descriptionI18nKey;
            Cost = cost;
            PlayDestination = playDestination;
            EffectDamageValue = effectDamageValue;
            ProgramDamageValue = programDamageValue;
            CanUpgradeOneLevel = canUpgradeOneLevel;
        }

        /// <summary>按模板标识与实例等级创建战斗卡牌配置投影；非法配置、等级或数值溢出会明确失败。</summary>
        public static BattleCardLevelProjection Create(
            cfg.Tables tables,
            int cardTemplateId,
            int upgradeLevel)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (upgradeLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));

            ProjectionConfiguration configuration = ResolveConfiguration(tables, cardTemplateId);
            switch (configuration.Template.UpgradeTrackKind)
            {
                case cfg.battle.CardUpgradeTrackKind.None:
                    if (upgradeLevel != 0)
                        throw CreateInvalidLevelException(cardTemplateId, upgradeLevel);
                    return CreateLevelZeroProjection(configuration, canUpgradeOneLevel: false);
                case cfg.battle.CardUpgradeTrackKind.Finite:
                    if (upgradeLevel > configuration.FiniteSteps.Count)
                        throw CreateInvalidLevelException(cardTemplateId, upgradeLevel);
                    return CreateFiniteProjection(configuration, upgradeLevel);
                case cfg.battle.CardUpgradeTrackKind.Infinite:
                    return CreateInfiniteProjection(configuration, upgradeLevel);
                default:
                    throw new InvalidOperationException(
                        $"Card {cardTemplateId} has unsupported upgrade track " +
                        $"{configuration.Template.UpgradeTrackKind}.");
            }
        }

        /// <summary>判断指定实例等级能否由当前配置完整投影；配置自身无效时仍会明确抛错。</summary>
        public static bool IsUpgradeLevelValid(
            cfg.Tables tables,
            int cardTemplateId,
            int upgradeLevel)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));
            if (upgradeLevel < 0)
                return false;

            ProjectionConfiguration configuration = ResolveConfiguration(tables, cardTemplateId);
            switch (configuration.Template.UpgradeTrackKind)
            {
                case cfg.battle.CardUpgradeTrackKind.None:
                    return upgradeLevel == 0;
                case cfg.battle.CardUpgradeTrackKind.Finite:
                    return upgradeLevel <= configuration.FiniteSteps.Count;
                case cfg.battle.CardUpgradeTrackKind.Infinite:
                    return CanRepresentInfiniteLevel(configuration, upgradeLevel);
                default:
                    throw new InvalidOperationException(
                        $"Card {cardTemplateId} has unsupported upgrade track " +
                        $"{configuration.Template.UpgradeTrackKind}.");
            }
        }

        /// <summary>解析并校验一张卡牌的升级轨道和基础伤害来源。</summary>
        private static ProjectionConfiguration ResolveConfiguration(
            cfg.Tables tables,
            int cardTemplateId)
        {
            cfg.battle.Card template = tables.TbCard.GetOrDefault(cardTemplateId)
                ?? throw new InvalidOperationException(
                    $"Card template {cardTemplateId} does not exist.");
            if (string.IsNullOrWhiteSpace(template.DescriptionI18nKey))
                throw new InvalidOperationException(
                    $"Card {cardTemplateId} has an empty base description key.");

            List<cfg.battle.CardUpgradeLevel> finiteSteps = FindFiniteSteps(tables, cardTemplateId);
            DamageValues baseDamage = ResolveBaseDamage(tables, template);
            ValidateTrackConfiguration(template, finiteSteps, baseDamage);
            return new ProjectionConfiguration(template, finiteSteps, baseDamage);
        }

        /// <summary>按 CardId 收集并排序有限轨道等级行，供连续性校验和精确等级查询。</summary>
        private static List<cfg.battle.CardUpgradeLevel> FindFiniteSteps(
            cfg.Tables tables,
            int cardTemplateId)
        {
            var steps = new List<cfg.battle.CardUpgradeLevel>();
            foreach (cfg.battle.CardUpgradeLevel row in tables.TbCardUpgradeLevel.DataList)
            {
                if (row.CardId == cardTemplateId)
                    steps.Add(row);
            }

            steps.Sort((left, right) => left.NextUpgradeLevel.CompareTo(right.NextUpgradeLevel));
            return steps;
        }

        /// <summary>按轨道类型拒绝混合尾巴、缺级、未知规则及无可执行目标的配置。</summary>
        private static void ValidateTrackConfiguration(
            cfg.battle.Card template,
            IReadOnlyList<cfg.battle.CardUpgradeLevel> finiteSteps,
            DamageValues baseDamage)
        {
            switch (template.UpgradeTrackKind)
            {
                case cfg.battle.CardUpgradeTrackKind.None:
                    ValidateNoInfiniteRule(template);
                    if (finiteSteps.Count != 0)
                        throw new InvalidOperationException(
                            $"Card {template.Id} uses None track but has finite upgrade rows.");
                    return;
                case cfg.battle.CardUpgradeTrackKind.Finite:
                    ValidateNoInfiniteRule(template);
                    ValidateFiniteSteps(template, finiteSteps, baseDamage);
                    return;
                case cfg.battle.CardUpgradeTrackKind.Infinite:
                    if (finiteSteps.Count != 0)
                        throw new InvalidOperationException(
                            $"Card {template.Id} cannot combine an infinite track with finite rows.");
                    ValidateInfiniteRule(template, baseDamage);
                    return;
                default:
                    throw new InvalidOperationException(
                        $"Card {template.Id} has unsupported upgrade track {template.UpgradeTrackKind}.");
            }
        }

        /// <summary>确认非无限轨道未偷偷携带每级增量事实。</summary>
        private static void ValidateNoInfiniteRule(cfg.battle.Card template)
        {
            if (template.InfiniteUpgradeRuleKind != cfg.battle.CardUpgradeRuleKind.None ||
                template.InfiniteUpgradeValuePerLevel != 0)
            {
                throw new InvalidOperationException(
                    $"Card {template.Id} is not infinite but contains an infinite upgrade rule.");
            }
        }

        /// <summary>确认有限轨道至少一级、从一级连续且每行规则值可由明确伤害通道消费。</summary>
        private static void ValidateFiniteSteps(
            cfg.battle.Card template,
            IReadOnlyList<cfg.battle.CardUpgradeLevel> finiteSteps,
            DamageValues baseDamage)
        {
            if (finiteSteps.Count == 0)
                throw new InvalidOperationException(
                    $"Finite card {template.Id} must define at least one upgrade level.");

            DamageValues currentDamage = baseDamage;
            for (int index = 0; index < finiteSteps.Count; index++)
            {
                cfg.battle.CardUpgradeLevel step = finiteSteps[index];
                int expectedLevel = index + 1;
                if (step.NextUpgradeLevel != expectedLevel)
                {
                    throw new InvalidOperationException(
                        $"Finite card {template.Id} levels must be continuous from 1; " +
                        $"expected {expectedLevel}, found {step.NextUpgradeLevel}.");
                }
                if (string.IsNullOrWhiteSpace(step.DescriptionI18nKey))
                    throw new InvalidOperationException(
                        $"Finite card {template.Id} level {expectedLevel} has an empty description key.");

                currentDamage = ApplyFiniteRule(template, step, currentDamage);
            }
        }

        /// <summary>确认无限轨道只使用当前支持的正数 DamageValue 增量并拥有唯一已知基值。</summary>
        private static void ValidateInfiniteRule(
            cfg.battle.Card template,
            DamageValues baseDamage)
        {
            if (template.InfiniteUpgradeRuleKind != cfg.battle.CardUpgradeRuleKind.DamageValue)
            {
                throw new InvalidOperationException(
                    $"Infinite card {template.Id} must use the typed DamageValue rule.");
            }
            if (template.InfiniteUpgradeValuePerLevel <= 0)
                throw new InvalidOperationException(
                    $"Infinite card {template.Id} must add a positive value per level.");
            if (string.IsNullOrWhiteSpace(template.UpgradedDescriptionI18nKey))
                throw new InvalidOperationException(
                    $"Infinite card {template.Id} has an empty upgraded description key.");

            RequireSingleDamageValue(template, baseDamage);
        }

        /// <summary>从通用效果表与当前已冻结的生产程序事实解析基础伤害通道。</summary>
        private static DamageValues ResolveBaseDamage(
            cfg.Tables tables,
            cfg.battle.Card template)
        {
            int? effectDamage = null;
            int directDamageEffectCount = 0;
            foreach (cfg.battle.CardEffectBinding binding in template.EffectBindings)
            {
                cfg.battle.CardEffect effect = tables.TbCardEffect.GetOrDefault(binding.EffectId)
                    ?? throw new InvalidOperationException(
                        $"Card {template.Id} references missing effect {binding.EffectId}.");
                if (effect.EffectType != cfg.battle.EffectType.DealDamage)
                    continue;

                directDamageEffectCount++;
                effectDamage = directDamageEffectCount == 1
                    ? effect.Value
                    : null;
            }

            int? programDamage = ResolveProgramBaseDamage(template);
            return new DamageValues(effectDamage, programDamage);
        }

        /// <summary>从既有职业程序注册表读取 Shoot 唯一伤害操作，避免复制第二份基础数值。</summary>
        private static int? ResolveProgramBaseDamage(cfg.battle.Card template)
        {
            if (template.ProgramId != cfg.battle.MachineGunnerProgramId.Shoot)
                return null;
            if (!MachineGunnerCardProgramRegistry.TryGet(
                    template.ProgramId,
                    out MachineGunnerCardProgram program))
            {
                throw new InvalidOperationException(
                    $"Card {template.Id} references a missing Shoot program.");
            }

            int? damageValue = null;
            foreach (MachineGunnerProgramOperation operation in program.Operations)
            {
                if (operation.Kind != MachineGunnerProgramOperationKind.Damage)
                    continue;
                if (damageValue.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Card {template.Id} Shoot program has more than one damage operation.");
                }

                damageValue = operation.Value;
            }
            if (!damageValue.HasValue)
            {
                throw new InvalidOperationException(
                    $"Card {template.Id} Shoot program has no damage operation.");
            }

            return damageValue;
        }

        /// <summary>创建零级投影，并保持模板中的基础文本、费用、去向和伤害值。</summary>
        private static BattleCardLevelProjection CreateLevelZeroProjection(
            ProjectionConfiguration configuration,
            bool canUpgradeOneLevel)
        {
            return new BattleCardLevelProjection(
                configuration.Template,
                upgradeLevel: 0,
                configuration.Template.DescriptionI18nKey,
                configuration.Template.Cost,
                configuration.Template.PlayDestination,
                configuration.BaseDamage.Effect,
                configuration.BaseDamage.Program,
                canUpgradeOneLevel);
        }

        /// <summary>逐级应用有限轨道的明确规则，并用目标等级行投影文本、费用与去向。</summary>
        private static BattleCardLevelProjection CreateFiniteProjection(
            ProjectionConfiguration configuration,
            int upgradeLevel)
        {
            if (upgradeLevel == 0)
            {
                return CreateLevelZeroProjection(
                    configuration,
                    canUpgradeOneLevel: configuration.FiniteSteps.Count > 0);
            }

            DamageValues damage = configuration.BaseDamage;
            for (int index = 0; index < upgradeLevel; index++)
                damage = ApplyFiniteRule(configuration.Template, configuration.FiniteSteps[index], damage);

            cfg.battle.CardUpgradeLevel targetStep = configuration.FiniteSteps[upgradeLevel - 1];
            return new BattleCardLevelProjection(
                configuration.Template,
                upgradeLevel,
                targetStep.DescriptionI18nKey,
                targetStep.Cost,
                targetStep.PlayDestination,
                damage.Effect,
                damage.Program,
                canUpgradeOneLevel: upgradeLevel < configuration.FiniteSteps.Count);
        }

        /// <summary>把一条有限轨道规则应用到当前伤害通道；DamageValue 是该等级的绝对值。</summary>
        private static DamageValues ApplyFiniteRule(
            cfg.battle.Card template,
            cfg.battle.CardUpgradeLevel step,
            DamageValues currentDamage)
        {
            switch (step.RuleKind)
            {
                case cfg.battle.CardUpgradeRuleKind.None:
                    if (step.RuleValue != 0)
                    {
                        throw new InvalidOperationException(
                            $"Card {template.Id} level {step.NextUpgradeLevel} uses None with a non-zero value.");
                    }
                    return currentDamage;
                case cfg.battle.CardUpgradeRuleKind.DamageValue:
                    return ReplaceDamageValue(template, currentDamage, step.RuleValue);
                default:
                    throw new InvalidOperationException(
                        $"Card {template.Id} level {step.NextUpgradeLevel} has unsupported rule {step.RuleKind}.");
            }
        }

        /// <summary>创建无限轨道投影，并以 checked 数学累加唯一伤害通道。</summary>
        private static BattleCardLevelProjection CreateInfiniteProjection(
            ProjectionConfiguration configuration,
            int upgradeLevel)
        {
            DamageValues damage = ApplyInfiniteRule(configuration, upgradeLevel);
            bool canUpgradeOneLevel = CanRepresentNextInfiniteLevel(configuration, upgradeLevel);
            cfg.battle.Card template = configuration.Template;
            return new BattleCardLevelProjection(
                template,
                upgradeLevel,
                upgradeLevel == 0 ? template.DescriptionI18nKey : template.UpgradedDescriptionI18nKey,
                upgradeLevel == 0 ? template.Cost : template.UpgradedCost,
                upgradeLevel == 0 ? template.PlayDestination : template.UpgradedPlayDestination,
                damage.Effect,
                damage.Program,
                canUpgradeOneLevel);
        }

        /// <summary>按固定类型化增量计算指定无限等级的最终伤害值。</summary>
        private static DamageValues ApplyInfiniteRule(
            ProjectionConfiguration configuration,
            int upgradeLevel)
        {
            int baseValue = RequireSingleDamageValue(
                configuration.Template,
                configuration.BaseDamage);
            int value = checked(
                baseValue +
                configuration.Template.InfiniteUpgradeValuePerLevel * upgradeLevel);
            return ReplaceDamageValue(configuration.Template, configuration.BaseDamage, value);
        }

        /// <summary>在不抛出溢出的前提下判断无限轨道是否能表达指定等级。</summary>
        private static bool CanRepresentInfiniteLevel(
            ProjectionConfiguration configuration,
            int upgradeLevel)
        {
            try
            {
                ApplyInfiniteRule(configuration, upgradeLevel);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        /// <summary>判断无限轨道的下一等级是否仍能由 Int32 完整表达。</summary>
        private static bool CanRepresentNextInfiniteLevel(
            ProjectionConfiguration configuration,
            int upgradeLevel)
        {
            try
            {
                int nextLevel = checked(upgradeLevel + 1);
                return CanRepresentInfiniteLevel(configuration, nextLevel);
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        /// <summary>把绝对伤害值写到唯一效果或程序通道，不允许猜测含糊来源。</summary>
        private static DamageValues ReplaceDamageValue(
            cfg.battle.Card template,
            DamageValues currentDamage,
            int value)
        {
            if (currentDamage.Effect.HasValue && currentDamage.Program.HasValue)
                throw new InvalidOperationException(
                    $"Card {template.Id} has ambiguous effect and program damage channels.");
            if (currentDamage.Effect.HasValue)
                return new DamageValues(value, null);
            if (currentDamage.Program.HasValue ||
                template.ProgramId != cfg.battle.MachineGunnerProgramId.None)
            {
                return new DamageValues(null, value);
            }

            throw new InvalidOperationException(
                $"Card {template.Id} has a DamageValue rule without a damage channel.");
        }

        /// <summary>读取唯一已知伤害基值；缺失或双通道时拒绝无限累加。</summary>
        private static int RequireSingleDamageValue(
            cfg.battle.Card template,
            DamageValues damage)
        {
            if (damage.Effect.HasValue == damage.Program.HasValue)
            {
                throw new InvalidOperationException(
                    $"Card {template.Id} must expose exactly one known damage channel.");
            }

            return damage.Effect ?? damage.Program.Value;
        }

        /// <summary>创建携带卡牌与等级的统一非法等级异常。</summary>
        private static ArgumentOutOfRangeException CreateInvalidLevelException(
            int cardTemplateId,
            int upgradeLevel)
        {
            return new ArgumentOutOfRangeException(
                nameof(upgradeLevel),
                upgradeLevel,
                $"Upgrade level {upgradeLevel} is not valid for card {cardTemplateId}.");
        }

        /// <summary>封装经过校验的模板、有限等级行与基础伤害值，避免重复解析配置。</summary>
        private sealed class ProjectionConfiguration
        {
            /// <summary>当前卡牌模板。</summary>
            internal cfg.battle.Card Template { get; }

            /// <summary>从一级开始连续的有限等级行。</summary>
            internal IReadOnlyList<cfg.battle.CardUpgradeLevel> FiniteSteps { get; }

            /// <summary>零级伤害通道。</summary>
            internal DamageValues BaseDamage { get; }

            /// <summary>冻结单次投影所需的已校验配置。</summary>
            internal ProjectionConfiguration(
                cfg.battle.Card template,
                IReadOnlyList<cfg.battle.CardUpgradeLevel> finiteSteps,
                DamageValues baseDamage)
            {
                Template = template;
                FiniteSteps = finiteSteps;
                BaseDamage = baseDamage;
            }
        }

        /// <summary>区分通用效果伤害与职业程序伤害，避免把两个执行通道混为一个数值。</summary>
        private readonly struct DamageValues
        {
            /// <summary>通用效果执行器的伤害值。</summary>
            internal int? Effect { get; }

            /// <summary>职业程序执行器的伤害值。</summary>
            internal int? Program { get; }

            /// <summary>创建一份保持通道身份的伤害值。</summary>
            internal DamageValues(int? effect, int? program)
            {
                Effect = effect;
                Program = program;
            }
        }
    }
}
