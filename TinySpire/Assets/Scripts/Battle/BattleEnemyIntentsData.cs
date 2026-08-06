using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;
using R3;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>下一意图不存在合法候选时使用的稳定、首次写入前故障类型。</summary>
    internal sealed class BattleNoLegalNextIntentException : InvalidOperationException
    {
        /// <summary>保留可诊断消息，同时让 Queue 无需解析文本即可稳定分类。</summary>
        internal BattleNoLegalNextIntentException(string message)
            : base(message)
        {
        }
    }

    /// <summary>下一意图零写入预构建形成的冻结计划。</summary>
    internal sealed class BattlePreparedEnemyIntentCompletion
    {
        /// <summary>创建计划的唯一意图聚合。</summary>
        internal BattleEnemyIntentsData Owner { get; }

        /// <summary>预构建时当前意图、历史与随机的完整权威快照。</summary>
        internal BattleEnemyIntentAuthoritySnapshot InitialSnapshot { get; }

        /// <summary>本次完成行动的敌人。</summary>
        internal CombatantId EnemyId { get; }

        /// <summary>本次已经完成的行为模板标识。</summary>
        internal int CompletedBehaviorId { get; }

        /// <summary>预构建选择的下一行为模板标识。</summary>
        internal int NextBehaviorId { get; }

        /// <summary>成功选择下一意图后的确定性随机状态。</summary>
        internal uint RandomStateAfter { get; }

        /// <summary>成功提交时一次发布的完整下一布局。</summary>
        internal EnemyIntentLayoutData NextLayout { get; }

        /// <summary>成功提交时替换的完整下一选择历史。</summary>
        internal BattleEnemyIntentsData.EnemyBehaviorHistory NextHistory { get; }

        /// <summary>意图推进记录在联合事务中的冻结顺序。</summary>
        internal int StartingOrder { get; }

        /// <summary>首次写入前已经完整构造的下一意图结算记录。</summary>
        internal BattleEnemyIntentAdvancedSettlement Settlement { get; }

        /// <summary>计划是否已经执行过唯一校验尝试。</summary>
        internal bool ValidationAttempted { get; private set; }

        /// <summary>计划是否已经通过唯一校验。</summary>
        internal bool IsValidated { get; private set; }

        /// <summary>计划是否已经完成无普通失败提交。</summary>
        internal bool IsCommitted { get; private set; }

        /// <summary>冻结下一意图、历史、随机与记录顺序。</summary>
        internal BattlePreparedEnemyIntentCompletion(
            BattleEnemyIntentsData owner,
            BattleEnemyIntentAuthoritySnapshot initialSnapshot,
            BattleEnemyIntentsData.EnemyBehaviorHistory nextHistory,
            int nextBehaviorId,
            uint randomStateAfter,
            EnemyIntentLayoutData nextLayout,
            BattleEnemyIntentAdvancedSettlement settlement)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialSnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            NextHistory = nextHistory ?? throw new ArgumentNullException(nameof(nextHistory));
            NextLayout = nextLayout ?? throw new ArgumentNullException(nameof(nextLayout));
            Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));

            EnemyId = initialSnapshot.EnemyId;
            CompletedBehaviorId = initialSnapshot.CurrentBehaviorId;
            NextBehaviorId = nextBehaviorId;
            RandomStateAfter = randomStateAfter;
            StartingOrder = settlement.Order;
        }

        /// <summary>记录唯一校验结果；失败计划也不能再次校验。</summary>
        internal void MarkValidated(bool succeeded)
        {
            if (ValidationAttempted)
                throw new InvalidOperationException("下一意图计划已经执行过校验。");

            ValidationAttempted = true;
            IsValidated = succeeded;
        }

        /// <summary>在无普通失败提交前消费唯一计划。</summary>
        internal void MarkCommitted()
        {
            if (!ValidationAttempted || !IsValidated)
                throw new InvalidOperationException("下一意图计划尚未通过首次写入前校验。");
            if (IsCommitted)
                throw new InvalidOperationException("下一意图计划已经提交。");

            IsCommitted = true;
        }
    }

    /// <summary>下一意图预构建的成功计划或稳定 Queue fault。</summary>
    internal readonly struct BattleEnemyIntentCompletionPreparationResult
    {
        /// <summary>预构建是否形成了完整零写入计划。</summary>
        internal bool Succeeded => !FaultReason.HasValue && Plan != null;

        /// <summary>预构建失败时的稳定 Queue fault。</summary>
        internal BattleCommandQueueFaultReason? FaultReason { get; }

        /// <summary>成功时的冻结下一意图计划。</summary>
        internal BattlePreparedEnemyIntentCompletion Plan { get; }

        /// <summary>冻结一次下一意图预构建结果。</summary>
        internal BattleEnemyIntentCompletionPreparationResult(
            BattleCommandQueueFaultReason? faultReason,
            BattlePreparedEnemyIntentCompletion plan)
        {
            FaultReason = faultReason;
            Plan = plan;
        }
    }

    /// <summary>下一意图无普通失败提交返回的冻结结算。</summary>
    internal sealed class BattleEnemyIntentCompletionResult
    {
        /// <summary>唯一意图推进记录。</summary>
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>冻结一次意图推进提交结果。</summary>
        internal BattleEnemyIntentCompletionResult(IEnumerable<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));

            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
        }
    }

    /// <summary>
    /// 单场战斗内每名敌人的当前行为模板快照。
    /// 快照发布后不可变，观察者不会读到只更新了一部分的敌人意图。
    /// </summary>
    public sealed class EnemyIntentLayoutData
    {
        /// <summary>敌人运行时标识到当前行为模板标识的只读映射。</summary>
        public IReadOnlyDictionary<CombatantId, int> BehaviorIdsByEnemy { get; }

        /// <summary>复制完整映射并冻结，防止已发布意图被后续修改。</summary>
        internal EnemyIntentLayoutData(IEnumerable<KeyValuePair<CombatantId, int>> behaviorIdsByEnemy)
        {
            if (behaviorIdsByEnemy == null)
                throw new ArgumentNullException(nameof(behaviorIdsByEnemy));

            BehaviorIdsByEnemy = new ReadOnlyDictionary<CombatantId, int>(
                new Dictionary<CombatantId, int>(behaviorIdsByEnemy));
        }

        /// <summary>读取指定敌人的当前行为模板标识。</summary>
        public bool TryGetBehaviorId(CombatantId enemyId, out int behaviorId)
        {
            return BehaviorIdsByEnemy.TryGetValue(enemyId, out behaviorId);
        }
    }

    /// <summary>单名敌人选择下一意图所依赖的完整不可变历史快照。</summary>
    internal sealed class BattleEnemyIntentHistorySnapshot
    {
        /// <summary>最近一次已完成的行为模板标识；零表示尚未完成过行为。</summary>
        internal int LastCompletedBehaviorId { get; }

        /// <summary>最近行为已经连续完成的次数。</summary>
        internal int ConsecutiveCompletedCount { get; }

        /// <summary>行为模板标识到尚未消费选择次数的只读映射。</summary>
        internal IReadOnlyDictionary<int, int> CooldownsByBehaviorId { get; }

        /// <summary>复制并冻结一次敌人行为历史。</summary>
        internal BattleEnemyIntentHistorySnapshot(
            int lastCompletedBehaviorId,
            int consecutiveCompletedCount,
            IDictionary<int, int> cooldownsByBehaviorId)
        {
            if (cooldownsByBehaviorId == null)
                throw new ArgumentNullException(nameof(cooldownsByBehaviorId));

            LastCompletedBehaviorId = lastCompletedBehaviorId;
            ConsecutiveCompletedCount = consecutiveCompletedCount;
            CooldownsByBehaviorId = new ReadOnlyDictionary<int, int>(
                new Dictionary<int, int>(cooldownsByBehaviorId));
        }
    }

    /// <summary>
    /// 一次敌人行动预构建所读取的完整意图权威快照。
    /// 同时冻结已发布 Layout、目标敌人的真实选择历史与全局敌人意图随机流。
    /// </summary>
    internal sealed class BattleEnemyIntentAuthoritySnapshot
    {
        /// <summary>创建此快照的唯一敌人意图聚合。</summary>
        internal BattleEnemyIntentsData Owner { get; }

        /// <summary>预构建时已经发布且本身不可变的完整意图布局。</summary>
        internal EnemyIntentLayoutData Layout { get; }

        /// <summary>本次行动敌人的运行时标识。</summary>
        internal CombatantId EnemyId { get; }

        /// <summary>该敌人在预构建时的当前行为模板标识。</summary>
        internal int CurrentBehaviorId { get; }

        /// <summary>该敌人在预构建时的完整选择历史。</summary>
        internal BattleEnemyIntentHistorySnapshot History { get; }

        /// <summary>预构建时敌人意图专属随机流状态。</summary>
        internal uint RandomState { get; }

        /// <summary>冻结一次完整敌人意图权威读取。</summary>
        internal BattleEnemyIntentAuthoritySnapshot(
            BattleEnemyIntentsData owner,
            EnemyIntentLayoutData layout,
            CombatantId enemyId,
            int currentBehaviorId,
            BattleEnemyIntentHistorySnapshot history,
            uint randomState)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            History = history ?? throw new ArgumentNullException(nameof(history));

            EnemyId = enemyId;
            CurrentBehaviorId = currentBehaviorId;
            RandomState = randomState;
        }

        /// <summary>委托所属意图聚合判断全部权威事实是否仍等于本快照。</summary>
        internal bool Matches(BattleEnemyIntentsData intents)
        {
            return intents != null && intents.MatchesAuthoritySnapshot(this);
        }
    }

    /// <summary>
    /// 持有每名敌人的权威当前意图、最小选择历史与独立确定性随机流。
    /// 静态行为细节始终回查 Luban 表，不在运行时复制模板字段。
    /// </summary>
    public sealed class BattleEnemyIntentsData : IDisposable
    {
        private const uint EnemyIntentRandomSalt = 0xE17A1465u;
        private const uint NonZeroFallbackSeed = 0x6D2B79F5u;

        private readonly BattleCombatantsData _combatants;
        private readonly Tables _tables;
        private readonly GameRandom _random;
        private readonly Dictionary<CombatantId, EnemyBehaviorHistory> _historyByEnemy;
        private readonly ReactiveProperty<EnemyIntentLayoutData> _layout;

        /// <summary>全部敌人当前意图的完整只读响应式快照。</summary>
        public ReadOnlyReactiveProperty<EnemyIntentLayoutData> Layout { get; }

        /// <summary>敌人行为专属确定性随机流的当前状态，供验收与复现使用。</summary>
        public uint RandomState => _random.State;

        /// <summary>
        /// 按 Encounter 顺序验证配置并为每名敌人选择初始意图。
        /// </summary>
        public BattleEnemyIntentsData(
            BattleCombatantsData combatants,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder,
            Tables tables,
            uint battleSeed)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            if (enemyCombatantIdsInEncounterOrder == null)
                throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
            if (battleSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(battleSeed));

            _random = new GameRandom(DeriveRandomSeed(battleSeed));
            _historyByEnemy = new Dictionary<CombatantId, EnemyBehaviorHistory>(
                enemyCombatantIdsInEncounterOrder.Count);
            var initialBehaviorIds = new Dictionary<CombatantId, int>(
                enemyCombatantIdsInEncounterOrder.Count);

            foreach (CombatantId enemyId in enemyCombatantIdsInEncounterOrder)
            {
                cfg.battle.Enemy enemy = ValidateEnemyAndBehaviorGroup(enemyId);
                var history = new EnemyBehaviorHistory();
                int behaviorId = SelectNextBehavior(enemy.BehaviorGroupId, history, _random);
                if (!initialBehaviorIds.TryAdd(enemyId, behaviorId))
                    throw new InvalidOperationException($"Enemy {enemyId} appears more than once in encounter order.");

                _historyByEnemy.Add(enemyId, history);
            }

            _layout = new ReactiveProperty<EnemyIntentLayoutData>(
                new EnemyIntentLayoutData(initialBehaviorIds));
            Layout = _layout.ToReadOnlyReactiveProperty();
        }

        /// <summary>
        /// 记录指定敌人已完成当前行为，并原子选择、发布其下一意图。
        /// 配置无合法候选时抛出异常，且不会修改意图、历史或随机状态。
        /// </summary>
        internal void CompleteAndSelectNext(CombatantId enemyId)
        {
            BattleEnemyIntentCompletionPreparationResult preparation =
                PrepareCompletion(enemyId, startingOrder: 0);
            if (!preparation.Succeeded)
            {
                if (preparation.FaultReason == BattleCommandQueueFaultReason.NoLegalNextIntent)
                {
                    throw new BattleNoLegalNextIntentException(
                        $"Enemy {enemyId} has no legal candidate.");
                }

                throw new InvalidOperationException(
                    $"Enemy {enemyId} intent completion failed: {preparation.FaultReason}.");
            }

            if (!ValidatePreparedCompletion(preparation.Plan))
                throw new InvalidOperationException("下一意图计划在首次写入前发生权威事实漂移。");

            CommitPreparedCompletion(preparation.Plan);
        }

        /// <summary>从当前权威意图快照零写入预构建下一历史、随机、布局与记录。</summary>
        internal BattleEnemyIntentCompletionPreparationResult PrepareCompletion(
            CombatantId enemyId,
            int startingOrder)
        {
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));

            BattleEnemyIntentAuthoritySnapshot snapshot;
            try
            {
                snapshot = CaptureAuthoritySnapshot(enemyId);
            }
            catch (InvalidOperationException)
            {
                return FailedCompletion(BattleCommandQueueFaultReason.MissingEnemyBehavior);
            }

            return PrepareCompletion(snapshot, startingOrder);
        }

        /// <summary>使用联合事务已经冻结的同一意图快照预构建完成计划，不再另抓权威事实。</summary>
        internal BattleEnemyIntentCompletionPreparationResult PrepareCompletion(
            BattleEnemyIntentAuthoritySnapshot snapshot,
            int startingOrder)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (startingOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(startingOrder));
            if (!ReferenceEquals(snapshot.Owner, this))
                return FailedCompletion(BattleCommandQueueFaultReason.PreparedInvariantViolation);
            if (!_combatants.TryGet(snapshot.EnemyId, out CombatantData combatant) ||
                combatant is not EnemyCombatantData ||
                !combatant.IsAlive)
            {
                return FailedCompletion(BattleCommandQueueFaultReason.UnsupportedConfiguration);
            }

            cfg.battle.Enemy enemy = _tables.TbEnemy.GetOrDefault(combatant.TemplateId);
            cfg.battle.EnemyBehavior completedBehavior =
                _tables.TbEnemyBehavior.GetOrDefault(snapshot.CurrentBehaviorId);
            if (enemy == null || completedBehavior == null)
                return FailedCompletion(BattleCommandQueueFaultReason.MissingEnemyBehavior);

            var nextHistory = new EnemyBehaviorHistory(
                snapshot.History.LastCompletedBehaviorId,
                snapshot.History.ConsecutiveCompletedCount,
                new Dictionary<int, int>(snapshot.History.CooldownsByBehaviorId));
            nextHistory.RecordCompletion(completedBehavior);
            // GameRandom 构造函数接收的是种子而非可直接恢复的流状态；显式复位后再做本地预演。
            var preparedRandom = new GameRandom(snapshot.RandomState)
            {
                State = snapshot.RandomState,
            };

            try
            {
                int nextBehaviorId = SelectNextBehavior(
                    enemy.BehaviorGroupId,
                    nextHistory,
                    preparedRandom);
                var nextBehaviorIds = new Dictionary<CombatantId, int>(
                    snapshot.Layout.BehaviorIdsByEnemy)
                {
                    [snapshot.EnemyId] = nextBehaviorId
                };
                if (snapshot.CurrentBehaviorId <= 0 || nextBehaviorId <= 0)
                {
                    return FailedCompletion(
                        BattleCommandQueueFaultReason.UnsupportedConfiguration);
                }

                var settlement = new BattleEnemyIntentAdvancedSettlement(
                    startingOrder,
                    snapshot.EnemyId,
                    snapshot.CurrentBehaviorId,
                    nextBehaviorId);
                return new BattleEnemyIntentCompletionPreparationResult(
                    faultReason: null,
                    new BattlePreparedEnemyIntentCompletion(
                        this,
                        snapshot,
                        nextHistory,
                        nextBehaviorId,
                        preparedRandom.State,
                        new EnemyIntentLayoutData(nextBehaviorIds),
                        settlement));
            }
            catch (BattleNoLegalNextIntentException)
            {
                return FailedCompletion(BattleCommandQueueFaultReason.NoLegalNextIntent);
            }
            catch (OverflowException)
            {
                return FailedCompletion(BattleCommandQueueFaultReason.UnsupportedConfiguration);
            }
            catch (InvalidOperationException)
            {
                return FailedCompletion(BattleCommandQueueFaultReason.MissingEnemyBehavior);
            }
            catch (ArgumentException)
            {
                return FailedCompletion(BattleCommandQueueFaultReason.UnsupportedConfiguration);
            }
        }

        /// <summary>首次写入前只校验一次计划归属以及 Layout、历史与随机权威快照。</summary>
        internal bool ValidatePreparedCompletion(BattlePreparedEnemyIntentCompletion plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能校验其他意图聚合创建的计划。");

            bool succeeded = !plan.IsCommitted && plan.InitialSnapshot.Matches(this);
            plan.MarkValidated(succeeded);
            return succeeded;
        }

        /// <summary>提交已验证的下一意图计划一次；不复验、不再随机且不返回普通失败。</summary>
        internal BattleEnemyIntentCompletionResult CommitPreparedCompletion(
            BattlePreparedEnemyIntentCompletion plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("不能提交其他意图聚合创建的计划。");

            plan.MarkCommitted();
            _historyByEnemy[plan.EnemyId] = plan.NextHistory;
            _random.State = plan.RandomStateAfter;
            _layout.Value = plan.NextLayout;
            return new BattleEnemyIntentCompletionResult(
                new BattleSettlementRecord[]
                {
                    plan.Settlement,
                });
        }

        /// <summary>零写入捕获当前 Layout、指定敌人完整历史与随机状态。</summary>
        internal BattleEnemyIntentAuthoritySnapshot CaptureAuthoritySnapshot(CombatantId enemyId)
        {
            EnemyIntentLayoutData layout = Layout.CurrentValue;
            if (!_historyByEnemy.TryGetValue(enemyId, out EnemyBehaviorHistory history) ||
                !layout.TryGetBehaviorId(enemyId, out int behaviorId))
            {
                throw new InvalidOperationException($"Enemy {enemyId} does not have an authoritative intent.");
            }

            return new BattleEnemyIntentAuthoritySnapshot(
                this,
                layout,
                enemyId,
                behaviorId,
                history.CaptureSnapshot(),
                _random.State);
        }

        /// <summary>比较一次意图权威快照的归属、Layout、完整历史与随机状态。</summary>
        internal bool MatchesAuthoritySnapshot(BattleEnemyIntentAuthoritySnapshot snapshot)
        {
            if (snapshot == null ||
                !ReferenceEquals(snapshot.Owner, this) ||
                !ReferenceEquals(snapshot.Layout, Layout.CurrentValue) ||
                snapshot.RandomState != _random.State ||
                !_historyByEnemy.TryGetValue(snapshot.EnemyId, out EnemyBehaviorHistory history) ||
                !Layout.CurrentValue.TryGetBehaviorId(snapshot.EnemyId, out int behaviorId) ||
                behaviorId != snapshot.CurrentBehaviorId)
            {
                return false;
            }

            return history.Matches(snapshot.History);
        }

        /// <summary>释放意图快照持有的响应式资源。</summary>
        public void Dispose()
        {
            Layout.Dispose();
            _layout.Dispose();
        }

        /// <summary>从战斗种子派生稳定、非零且与洗牌命名域隔离的敌人行为种子。</summary>
        private static uint DeriveRandomSeed(uint battleSeed)
        {
            uint derivedSeed = battleSeed ^ EnemyIntentRandomSalt;
            return derivedSeed == 0 ? NonZeroFallbackSeed : derivedSeed;
        }

        /// <summary>验证运行时敌人及其行为组引用，并返回静态敌人模板。</summary>
        private cfg.battle.Enemy ValidateEnemyAndBehaviorGroup(CombatantId enemyId)
        {
            if (!_combatants.TryGet(enemyId, out CombatantData combatant) || combatant is not EnemyCombatantData)
                throw new InvalidOperationException($"Combatant {enemyId} is not an enemy.");

            cfg.battle.Enemy enemy = _tables.TbEnemy.GetOrDefault(combatant.TemplateId)
                ?? throw new InvalidOperationException($"Enemy template {combatant.TemplateId} does not exist.");
            ValidateBehaviorGroup(enemy.BehaviorGroupId);
            return enemy;
        }

        /// <summary>验证行为组有序成员、行为约束和 Effect 引用均满足运行时契约。</summary>
        private void ValidateBehaviorGroup(int behaviorGroupId)
        {
            cfg.battle.EnemyBehaviorGroup group = _tables.TbEnemyBehaviorGroup.GetOrDefault(behaviorGroupId)
                ?? throw new InvalidOperationException($"Enemy behavior group {behaviorGroupId} does not exist.");
            if (group.BehaviorIds.Length == 0)
                throw new InvalidOperationException($"Enemy behavior group {behaviorGroupId} must not be empty.");

            var uniqueBehaviorIds = new HashSet<int>();
            long totalWeight = 0;
            foreach (int behaviorId in group.BehaviorIds)
            {
                if (!uniqueBehaviorIds.Add(behaviorId))
                    throw new InvalidOperationException(
                        $"Enemy behavior group {behaviorGroupId} contains duplicate behavior {behaviorId}.");

                cfg.battle.EnemyBehavior behavior = _tables.TbEnemyBehavior.GetOrDefault(behaviorId)
                    ?? throw new InvalidOperationException($"Enemy behavior {behaviorId} does not exist.");
                ValidateBehavior(behavior);
                totalWeight += behavior.Weight;
                if (totalWeight > int.MaxValue)
                    throw new InvalidOperationException(
                        $"Enemy behavior group {behaviorGroupId} total weight exceeds Int32.MaxValue.");
            }
        }

        /// <summary>验证单个行为的权重、限制、枚举与 Effect 引用。</summary>
        private void ValidateBehavior(cfg.battle.EnemyBehavior behavior)
        {
            if (behavior.Weight <= 0)
                throw new InvalidOperationException($"Enemy behavior {behavior.Id} weight must be positive.");
            if (behavior.CooldownSelections < 0)
                throw new InvalidOperationException(
                    $"Enemy behavior {behavior.Id} cooldown selections must not be negative.");
            if (behavior.MaxConsecutive < 0)
                throw new InvalidOperationException(
                    $"Enemy behavior {behavior.Id} max consecutive must not be negative.");
            if (!Enum.IsDefined(typeof(cfg.battle.EnemyIntentType), behavior.IntentType))
                throw new InvalidOperationException($"Enemy behavior {behavior.Id} has an invalid intent type.");
            if (behavior.TargetRule != cfg.battle.TargetRule.Self
                && behavior.TargetRule != cfg.battle.TargetRule.Enemy)
            {
                throw new InvalidOperationException(
                    $"Enemy behavior {behavior.Id} has an unsupported target rule {behavior.TargetRule}.");
            }
            if (_tables.TbCardEffect.GetOrDefault(behavior.EffectId) == null)
                throw new InvalidOperationException(
                    $"Enemy behavior {behavior.Id} references missing effect {behavior.EffectId}.");
        }

        /// <summary>按行为组稳定顺序过滤候选，并以一次整数权重抽样选择下一行为。</summary>
        private int SelectNextBehavior(
            int behaviorGroupId,
            EnemyBehaviorHistory history,
            GameRandom random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            cfg.battle.EnemyBehaviorGroup group = _tables.TbEnemyBehaviorGroup.GetOrDefault(behaviorGroupId)
                ?? throw new InvalidOperationException($"Enemy behavior group {behaviorGroupId} does not exist.");
            var candidates = new List<cfg.battle.EnemyBehavior>(group.BehaviorIds.Length);
            int totalWeight = 0;
            foreach (int behaviorId in group.BehaviorIds)
            {
                cfg.battle.EnemyBehavior behavior = _tables.TbEnemyBehavior.GetOrDefault(behaviorId)
                    ?? throw new InvalidOperationException($"Enemy behavior {behaviorId} does not exist.");
                if (!history.CanSelect(behavior))
                    continue;

                candidates.Add(behavior);
                totalWeight = checked(totalWeight + behavior.Weight);
            }

            if (candidates.Count == 0)
            {
                throw new BattleNoLegalNextIntentException(
                    $"Enemy behavior group {behaviorGroupId} has no legal candidate.");
            }

            int selectedBehaviorId;
            if (candidates.Count == 1)
            {
                selectedBehaviorId = candidates[0].Id;
            }
            else
            {
                int roll = random.NextInt(totalWeight);
                selectedBehaviorId = candidates[candidates.Count - 1].Id;
                foreach (cfg.battle.EnemyBehavior candidate in candidates)
                {
                    if (roll < candidate.Weight)
                    {
                        selectedBehaviorId = candidate.Id;
                        break;
                    }

                    roll -= candidate.Weight;
                }
            }

            history.AdvanceCooldownsAfterSelection();
            return selectedBehaviorId;
        }

        /// <summary>创建不携带计划、不会写入任何意图事实的稳定预构建 fault。</summary>
        private static BattleEnemyIntentCompletionPreparationResult FailedCompletion(
            BattleCommandQueueFaultReason faultReason)
        {
            return new BattleEnemyIntentCompletionPreparationResult(faultReason, plan: null);
        }

        /// <summary>保存单名敌人为冷却和最大连续次数所需的最小已完成历史。</summary>
        internal sealed class EnemyBehaviorHistory
        {
            private readonly Dictionary<int, int> _cooldownsByBehaviorId;

            /// <summary>最近一次已完成的行为模板标识；零表示尚未完成过行为。</summary>
            private int LastCompletedBehaviorId { get; set; }

            /// <summary>最近行为已经连续完成的次数。</summary>
            private int ConsecutiveCompletedCount { get; set; }

            /// <summary>创建尚无已完成行为和冷却的初始历史。</summary>
            internal EnemyBehaviorHistory()
            {
                _cooldownsByBehaviorId = new Dictionary<int, int>();
            }

            /// <summary>复制完整历史，供原子尝试下一次选择。</summary>
            internal EnemyBehaviorHistory(
                int lastCompletedBehaviorId,
                int consecutiveCompletedCount,
                Dictionary<int, int> cooldownsByBehaviorId)
            {
                LastCompletedBehaviorId = lastCompletedBehaviorId;
                ConsecutiveCompletedCount = consecutiveCompletedCount;
                _cooldownsByBehaviorId = cooldownsByBehaviorId;
            }

            /// <summary>创建不会影响当前权威历史的可变副本。</summary>
            internal EnemyBehaviorHistory Clone()
            {
                return new EnemyBehaviorHistory(
                    LastCompletedBehaviorId,
                    ConsecutiveCompletedCount,
                    new Dictionary<int, int>(_cooldownsByBehaviorId));
            }

            /// <summary>复制最近完成事实与全部冷却，形成不可变权威快照。</summary>
            internal BattleEnemyIntentHistorySnapshot CaptureSnapshot()
            {
                return new BattleEnemyIntentHistorySnapshot(
                    LastCompletedBehaviorId,
                    ConsecutiveCompletedCount,
                    _cooldownsByBehaviorId);
            }

            /// <summary>比较当前完整历史是否仍等于给定不可变快照。</summary>
            internal bool Matches(BattleEnemyIntentHistorySnapshot snapshot)
            {
                if (snapshot == null ||
                    LastCompletedBehaviorId != snapshot.LastCompletedBehaviorId ||
                    ConsecutiveCompletedCount != snapshot.ConsecutiveCompletedCount ||
                    _cooldownsByBehaviorId.Count != snapshot.CooldownsByBehaviorId.Count)
                {
                    return false;
                }

                foreach (KeyValuePair<int, int> pair in _cooldownsByBehaviorId)
                {
                    if (!snapshot.CooldownsByBehaviorId.TryGetValue(pair.Key, out int remainingSelections) ||
                        remainingSelections != pair.Value)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>记录刚完成的行为，并开始其后续选择冷却。</summary>
            internal void RecordCompletion(cfg.battle.EnemyBehavior behavior)
            {
                if (LastCompletedBehaviorId == behavior.Id)
                    ConsecutiveCompletedCount++;
                else
                {
                    LastCompletedBehaviorId = behavior.Id;
                    ConsecutiveCompletedCount = 1;
                }

                if (behavior.CooldownSelections > 0)
                    _cooldownsByBehaviorId[behavior.Id] = behavior.CooldownSelections;
                else
                    _cooldownsByBehaviorId.Remove(behavior.Id);
            }

            /// <summary>根据尚未消费的冷却和最大连续次数判断行为是否可选。</summary>
            internal bool CanSelect(cfg.battle.EnemyBehavior behavior)
            {
                if (_cooldownsByBehaviorId.TryGetValue(behavior.Id, out int remainingSelections) &&
                    remainingSelections > 0)
                {
                    return false;
                }

                return behavior.MaxConsecutive == 0 ||
                       behavior.Id != LastCompletedBehaviorId ||
                       ConsecutiveCompletedCount < behavior.MaxConsecutive;
            }

            /// <summary>一次成功选择后消费全部仍在计数的冷却次数。</summary>
            internal void AdvanceCooldownsAfterSelection()
            {
                if (_cooldownsByBehaviorId.Count == 0)
                    return;

                var behaviorIds = new List<int>(_cooldownsByBehaviorId.Keys);
                foreach (int behaviorId in behaviorIds)
                {
                    int remainingSelections = _cooldownsByBehaviorId[behaviorId] - 1;
                    if (remainingSelections <= 0)
                        _cooldownsByBehaviorId.Remove(behaviorId);
                    else
                        _cooldownsByBehaviorId[behaviorId] = remainingSelections;
                }
            }
        }
    }
}
