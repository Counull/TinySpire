using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>机枪兵运行时能够独立排队的延迟效果身份。</summary>
    internal enum MachineGunnerScheduledEffectKind
    {
        GuidedNuke,
        BansheeStrike,
        FireSupport,
        FireBombardment,
        FiveHundredPounder,
        TripleStrike,
        NeedleStorm,
    }

    /// <summary>延迟实例读取的稳定职业生命周期时点。</summary>
    internal enum MachineGunnerScheduledEffectTiming
    {
        PlayerRoundStart,
        PlayerRoundEnd,
    }

    /// <summary>调度实例对表现与回归公开的生命周期变化类别。</summary>
    internal enum MachineGunnerScheduledEffectChangeKind
    {
        Created,
        Triggered,
        Countdown,
        Removed,
    }

    /// <summary>单场战斗内单调递增的调度实例标识。</summary>
    internal readonly struct MachineGunnerScheduledEffectId : IEquatable<MachineGunnerScheduledEffectId>
    {
        /// <summary>单场内从一开始递增的原始数值。</summary>
        internal int Value { get; }

        /// <summary>创建经过正数校验的调度实例标识。</summary>
        internal MachineGunnerScheduledEffectId(int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            Value = value;
        }

        /// <summary>按原始数值比较两个调度实例标识。</summary>
        public bool Equals(MachineGunnerScheduledEffectId other)
        {
            return Value == other.Value;
        }

        /// <summary>判断对象是否携带相同的调度实例标识。</summary>
        public override bool Equals(object obj)
        {
            return obj is MachineGunnerScheduledEffectId other && Equals(other);
        }

        /// <summary>返回与原始数值一致的哈希。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>输出便于诊断的稳定实例文本。</summary>
        public override string ToString()
        {
            return $"ScheduledEffect({Value})";
        }
    }

    /// <summary>一张卡成功结算时冻结到独立实例中的数值载荷。</summary>
    internal sealed class MachineGunnerScheduledEffectSpec
    {
        /// <summary>延迟效果的稳定种类。</summary>
        internal MachineGunnerScheduledEffectKind Kind { get; }

        /// <summary>实例参与的职业生命周期时点。</summary>
        internal MachineGunnerScheduledEffectTiming Timing { get; }

        /// <summary>创建时的倒计时；零表示按剩余触发次数驱动。</summary>
        internal int Countdown { get; }

        /// <summary>创建时的剩余触发次数；零表示按倒计时驱动。</summary>
        internal int RemainingTriggers { get; }

        /// <summary>冻结的主伤害数值。</summary>
        internal int Damage { get; }

        /// <summary>冻结的每次触发命中次数。</summary>
        internal int HitCount { get; }

        /// <summary>冻结的波次数量。</summary>
        internal int WaveCount { get; }

        /// <summary>冻结的燃烧施加量。</summary>
        internal int Burn { get; }

        /// <summary>冻结的浸油施加量。</summary>
        internal int Oil { get; }

        /// <summary>冻结的破甲施加量。</summary>
        internal int ArmorBreak { get; }

        /// <summary>创建一份不读取卡牌文本、后续不会再解释升级数据的调度规格。</summary>
        internal MachineGunnerScheduledEffectSpec(
            MachineGunnerScheduledEffectKind kind,
            MachineGunnerScheduledEffectTiming timing,
            int countdown,
            int remainingTriggers,
            int damage,
            int hitCount = 1,
            int waveCount = 1,
            int burn = 0,
            int oil = 0,
            int armorBreak = 0)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectTiming), timing))
                throw new ArgumentOutOfRangeException(nameof(timing));
            if (countdown < 0)
                throw new ArgumentOutOfRangeException(nameof(countdown));
            if (remainingTriggers < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingTriggers));
            if ((countdown == 0) == (remainingTriggers == 0))
                throw new ArgumentException("延迟实例必须且只能选择倒计时或剩余触发次数驱动。");
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (hitCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(hitCount));
            if (waveCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(waveCount));
            if (burn < 0)
                throw new ArgumentOutOfRangeException(nameof(burn));
            if (oil < 0)
                throw new ArgumentOutOfRangeException(nameof(oil));
            if (armorBreak < 0)
                throw new ArgumentOutOfRangeException(nameof(armorBreak));

            Kind = kind;
            Timing = timing;
            Countdown = countdown;
            RemainingTriggers = remainingTriggers;
            Damage = damage;
            HitCount = hitCount;
            WaveCount = waveCount;
            Burn = burn;
            Oil = oil;
            ArmorBreak = armorBreak;
        }
    }

    /// <summary>一项已经进入本场职业运行时的不可变延迟效果实例。</summary>
    internal sealed class MachineGunnerScheduledEffectInstance
    {
        /// <summary>单场内的稳定实例标识。</summary>
        internal MachineGunnerScheduledEffectId Id { get; }

        /// <summary>创建并归属该效果的机枪兵玩家。</summary>
        internal CombatantId SourceId { get; }

        /// <summary>延迟效果的稳定种类。</summary>
        internal MachineGunnerScheduledEffectKind Kind { get; }

        /// <summary>实例参与的职业生命周期时点。</summary>
        internal MachineGunnerScheduledEffectTiming Timing { get; }

        /// <summary>当前倒计时。</summary>
        internal int Countdown { get; }

        /// <summary>当前剩余触发次数。</summary>
        internal int RemainingTriggers { get; }

        /// <summary>施放成功时冻结的主伤害。</summary>
        internal int Damage { get; }

        /// <summary>施放成功时冻结的每次触发命中次数。</summary>
        internal int HitCount { get; }

        /// <summary>施放成功时冻结的波次数量。</summary>
        internal int WaveCount { get; }

        /// <summary>施放成功时冻结的燃烧量。</summary>
        internal int Burn { get; }

        /// <summary>施放成功时冻结的浸油量。</summary>
        internal int Oil { get; }

        /// <summary>施放成功时冻结的破甲量。</summary>
        internal int ArmorBreak { get; }

        /// <summary>按创建计划或生命周期更新构造一份不可变实例快照。</summary>
        internal MachineGunnerScheduledEffectInstance(
            MachineGunnerScheduledEffectId id,
            CombatantId sourceId,
            MachineGunnerScheduledEffectKind kind,
            MachineGunnerScheduledEffectTiming timing,
            int countdown,
            int remainingTriggers,
            int damage,
            int hitCount,
            int waveCount,
            int burn,
            int oil,
            int armorBreak)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectTiming), timing))
                throw new ArgumentOutOfRangeException(nameof(timing));
            if (countdown < 0)
                throw new ArgumentOutOfRangeException(nameof(countdown));
            if (remainingTriggers < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingTriggers));
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));
            if (hitCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(hitCount));
            if (waveCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(waveCount));
            if (burn < 0 || oil < 0 || armorBreak < 0)
                throw new ArgumentOutOfRangeException(nameof(burn));

            Id = id;
            SourceId = sourceId;
            Kind = kind;
            Timing = timing;
            Countdown = countdown;
            RemainingTriggers = remainingTriggers;
            Damage = damage;
            HitCount = hitCount;
            WaveCount = waveCount;
            Burn = burn;
            Oil = oil;
            ArmorBreak = armorBreak;
        }

        /// <summary>从冻结规格创建一份带稳定实例标识的首个快照。</summary>
        internal static MachineGunnerScheduledEffectInstance FromSpec(
            MachineGunnerScheduledEffectId id,
            CombatantId sourceId,
            MachineGunnerScheduledEffectSpec spec)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            return new MachineGunnerScheduledEffectInstance(
                id,
                sourceId,
                spec.Kind,
                spec.Timing,
                spec.Countdown,
                spec.RemainingTriggers,
                spec.Damage,
                spec.HitCount,
                spec.WaveCount,
                spec.Burn,
                spec.Oil,
                spec.ArmorBreak);
        }

        /// <summary>以新的倒计时和剩余次数复制本实例，冻结数值载荷保持不变。</summary>
        internal MachineGunnerScheduledEffectInstance WithProgress(
            int countdown,
            int remainingTriggers)
        {
            return new MachineGunnerScheduledEffectInstance(
                Id,
                SourceId,
                Kind,
                Timing,
                countdown,
                remainingTriggers,
                Damage,
                HitCount,
                WaveCount,
                Burn,
                Oil,
                ArmorBreak);
        }

        /// <summary>比较两个快照是否代表完全相同的调度事实。</summary>
        internal bool HasSameFacts(MachineGunnerScheduledEffectInstance other)
        {
            return other != null &&
                   Id.Equals(other.Id) &&
                   SourceId.Equals(other.SourceId) &&
                   Kind == other.Kind &&
                   Timing == other.Timing &&
                   Countdown == other.Countdown &&
                   RemainingTriggers == other.RemainingTriggers &&
                   Damage == other.Damage &&
                   HitCount == other.HitCount &&
                   WaveCount == other.WaveCount &&
                   Burn == other.Burn &&
                   Oil == other.Oil &&
                   ArmorBreak == other.ArmorBreak;
        }
    }

    /// <summary>一项生命周期提交所冻结的实例替换或移除。</summary>
    internal sealed class MachineGunnerScheduledEffectMutation
    {
        /// <summary>计划创建时的实例事实。</summary>
        internal MachineGunnerScheduledEffectInstance Before { get; }

        /// <summary>生命周期后的实例事实；移除时为空。</summary>
        internal MachineGunnerScheduledEffectInstance After { get; }

        /// <summary>此次生命周期是否实际触发了卡牌效果。</summary>
        internal bool Triggered { get; }

        /// <summary>冻结一项只改变进度或移除实例的生命周期写入。</summary>
        internal MachineGunnerScheduledEffectMutation(
            MachineGunnerScheduledEffectInstance before,
            MachineGunnerScheduledEffectInstance after,
            bool triggered)
        {
            Before = before ?? throw new ArgumentNullException(nameof(before));
            if (after != null && !before.Id.Equals(after.Id))
                throw new ArgumentOutOfRangeException(nameof(after));
            if (after != null && before.HasSameFacts(after))
                throw new ArgumentException("生命周期更新必须改变实例事实。", nameof(after));

            After = after;
            Triggered = triggered;
        }
    }

    /// <summary>一次卡牌成功归宿前冻结的调度实例创建计划。</summary>
    internal sealed class MachineGunnerScheduledEffectCreationPlan
    {
        /// <summary>计划依赖的调度器版本。</summary>
        internal int SchedulerVersion { get; }

        /// <summary>计划将要创建的完整实例。</summary>
        internal MachineGunnerScheduledEffectInstance Instance { get; }

        /// <summary>冻结一次实例创建计划。</summary>
        internal MachineGunnerScheduledEffectCreationPlan(
            int schedulerVersion,
            MachineGunnerScheduledEffectInstance instance)
        {
            if (schedulerVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(schedulerVersion));

            SchedulerVersion = schedulerVersion;
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }
    }

    /// <summary>一次稳定生命周期内全部实例进度写入的联合计划。</summary>
    internal sealed class MachineGunnerScheduledEffectMutationPlan
    {
        /// <summary>计划依赖的调度器版本。</summary>
        internal int SchedulerVersion { get; }

        /// <summary>本计划对应的生命周期时点。</summary>
        internal MachineGunnerScheduledEffectTiming Timing { get; }

        /// <summary>按实例插入顺序冻结的全部写入。</summary>
        internal IReadOnlyList<MachineGunnerScheduledEffectMutation> Mutations { get; }

        /// <summary>复制并冻结一次批量生命周期写入。</summary>
        internal MachineGunnerScheduledEffectMutationPlan(
            int schedulerVersion,
            MachineGunnerScheduledEffectTiming timing,
            IEnumerable<MachineGunnerScheduledEffectMutation> mutations)
        {
            if (schedulerVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(schedulerVersion));
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectTiming), timing))
                throw new ArgumentOutOfRangeException(nameof(timing));
            if (mutations == null)
                throw new ArgumentNullException(nameof(mutations));

            var copied = new List<MachineGunnerScheduledEffectMutation>();
            foreach (MachineGunnerScheduledEffectMutation mutation in mutations)
            {
                if (mutation == null)
                    throw new ArgumentException("调度实例写入不能包含空项。", nameof(mutations));
                if (mutation.Before.Timing != timing)
                    throw new ArgumentOutOfRangeException(nameof(mutations));
                copied.Add(mutation);
            }

            SchedulerVersion = schedulerVersion;
            Timing = timing;
            Mutations = new ReadOnlyCollection<MachineGunnerScheduledEffectMutation>(copied);
        }
    }

    /// <summary>机枪兵私有调度实例的单场权威容器，仅负责实例身份与原子替换，不解释伤害规则。</summary>
    internal sealed class MachineGunnerScheduledEffectScheduler
    {
        private readonly List<MachineGunnerScheduledEffectInstance> _instances =
            new List<MachineGunnerScheduledEffectInstance>();
        private int _version;
        private int _nextId = 1;

        /// <summary>返回当前独立实例数量，供目录回归和战斗终局诊断。</summary>
        internal int Count => _instances.Count;

        /// <summary>按插入顺序复制指定生命周期的当前实例快照。</summary>
        internal IReadOnlyList<MachineGunnerScheduledEffectInstance> Snapshot(
            MachineGunnerScheduledEffectTiming timing)
        {
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectTiming), timing))
                throw new ArgumentOutOfRangeException(nameof(timing));

            var copied = new List<MachineGunnerScheduledEffectInstance>();
            foreach (MachineGunnerScheduledEffectInstance instance in _instances)
            {
                if (instance.Timing == timing)
                    copied.Add(instance);
            }

            return new ReadOnlyCollection<MachineGunnerScheduledEffectInstance>(copied);
        }

        /// <summary>在任何战斗写入前冻结下一实例标识与完整载荷。</summary>
        internal MachineGunnerScheduledEffectCreationPlan PrepareCreation(
            CombatantId sourceId,
            MachineGunnerScheduledEffectSpec spec)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));
            if (_nextId == int.MaxValue)
                throw new OverflowException("调度实例标识已耗尽。");

            var id = new MachineGunnerScheduledEffectId(_nextId);
            return new MachineGunnerScheduledEffectCreationPlan(
                _version,
                MachineGunnerScheduledEffectInstance.FromSpec(id, sourceId, spec));
        }

        /// <summary>校验创建计划仍对应同一调度版本与下一个实例标识。</summary>
        internal bool ValidateCreation(MachineGunnerScheduledEffectCreationPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return plan.SchedulerVersion == _version &&
                   plan.Instance.Id.Value == _nextId;
        }

        /// <summary>提交已经验证的实例创建，提交期间不再解释卡牌或返回普通失败。</summary>
        internal MachineGunnerScheduledEffectInstance CommitCreation(
            MachineGunnerScheduledEffectCreationPlan plan)
        {
            if (!ValidateCreation(plan))
                throw new InvalidOperationException("调度实例创建计划发生快照漂移。");

            _instances.Add(plan.Instance);
            _nextId++;
            _version++;
            return plan.Instance;
        }

        /// <summary>以当前版本和调用方已冻结的实例变更创建联合生命周期计划。</summary>
        internal MachineGunnerScheduledEffectMutationPlan PrepareMutation(
            MachineGunnerScheduledEffectTiming timing,
            IEnumerable<MachineGunnerScheduledEffectMutation> mutations)
        {
            return new MachineGunnerScheduledEffectMutationPlan(_version, timing, mutations);
        }

        /// <summary>校验生命周期计划中的每一项 Before 仍按相同插入位置存在。</summary>
        internal bool ValidateMutation(MachineGunnerScheduledEffectMutationPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (plan.SchedulerVersion != _version)
                return false;

            foreach (MachineGunnerScheduledEffectMutation mutation in plan.Mutations)
            {
                int index = FindIndex(mutation.Before.Id);
                if (index < 0 || !_instances[index].HasSameFacts(mutation.Before))
                    return false;
            }

            return true;
        }

        /// <summary>一次性替换或移除联合计划中的全部实例，维持未触及实例的相对插入顺序。</summary>
        internal void CommitMutation(MachineGunnerScheduledEffectMutationPlan plan)
        {
            if (!ValidateMutation(plan))
                throw new InvalidOperationException("调度实例生命周期计划发生快照漂移。");

            foreach (MachineGunnerScheduledEffectMutation mutation in plan.Mutations)
            {
                int index = FindIndex(mutation.Before.Id);
                if (mutation.After == null)
                    _instances.RemoveAt(index);
                else
                    _instances[index] = mutation.After;
            }

            if (plan.Mutations.Count > 0)
                _version++;
        }

        /// <summary>清除单场运行时持有的全部延迟实例，不重用已经发放的实例标识。</summary>
        internal void Clear()
        {
            if (_instances.Count == 0)
                return;

            _instances.Clear();
            _version++;
        }

        /// <summary>按稳定标识定位当前实例在插入序列中的位置。</summary>
        private int FindIndex(MachineGunnerScheduledEffectId id)
        {
            for (int index = 0; index < _instances.Count; index++)
            {
                if (_instances[index].Id.Equals(id))
                    return index;
            }

            return -1;
        }
    }

    /// <summary>延迟实例创建、触发、倒计时或移除的可观察结算记录。</summary>
    internal sealed class MachineGunnerScheduledEffectChangedSettlement : BattleSettlementRecord
    {
        /// <summary>发生变化的稳定实例标识。</summary>
        internal MachineGunnerScheduledEffectId ScheduledEffectId { get; }

        /// <summary>发生变化的延迟效果种类。</summary>
        internal MachineGunnerScheduledEffectKind Kind { get; }

        /// <summary>本条记录代表的生命周期变化。</summary>
        internal MachineGunnerScheduledEffectChangeKind ChangeKind { get; }

        /// <summary>变化前的倒计时或剩余触发次数；创建时为零。</summary>
        internal int RemainingBefore { get; }

        /// <summary>变化后的倒计时或剩余触发次数；移除时为零。</summary>
        internal int RemainingAfter { get; }

        /// <summary>创建一条不伪装为通用 Effect 的调度生命周期记录。</summary>
        internal MachineGunnerScheduledEffectChangedSettlement(
            int order,
            CombatantId sourceId,
            MachineGunnerScheduledEffectInstance instance,
            MachineGunnerScheduledEffectChangeKind changeKind,
            int remainingBefore,
            int remainingAfter)
            : base(
                order,
                BattleSettlementRecordType.StatusApplied,
                null,
                sourceId,
                sourceId)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (!Enum.IsDefined(typeof(MachineGunnerScheduledEffectChangeKind), changeKind))
                throw new ArgumentOutOfRangeException(nameof(changeKind));
            if (remainingBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingBefore));
            if (remainingAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingAfter));

            ScheduledEffectId = instance.Id;
            Kind = instance.Kind;
            ChangeKind = changeKind;
            RemainingBefore = remainingBefore;
            RemainingAfter = remainingAfter;
        }
    }
}
