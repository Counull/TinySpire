using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using cfg;
using R3;
using TinySpire.Core;

namespace TinySpire.Battle
{
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
                int behaviorId = SelectNextBehavior(enemy.BehaviorGroupId, history);
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
        public void CompleteAndSelectNext(CombatantId enemyId)
        {
            if (!_combatants.TryGet(enemyId, out CombatantData combatant) ||
                combatant is not EnemyCombatantData ||
                !_historyByEnemy.TryGetValue(enemyId, out EnemyBehaviorHistory history) ||
                !Layout.CurrentValue.TryGetBehaviorId(enemyId, out int completedBehaviorId))
            {
                throw new InvalidOperationException($"Enemy {enemyId} does not have an authoritative intent.");
            }

            if (!combatant.IsAlive)
                throw new InvalidOperationException($"Enemy {enemyId} is not alive.");

            cfg.battle.Enemy enemy = _tables.TbEnemy.GetOrDefault(combatant.TemplateId)
                ?? throw new InvalidOperationException($"Enemy template {combatant.TemplateId} does not exist.");
            cfg.battle.EnemyBehavior completedBehavior = _tables.TbEnemyBehavior.GetOrDefault(completedBehaviorId)
                ?? throw new InvalidOperationException($"Enemy behavior {completedBehaviorId} does not exist.");

            uint randomStateBeforeSelection = _random.State;
            EnemyBehaviorHistory nextHistory = history.Clone();
            nextHistory.RecordCompletion(completedBehavior);

            int nextBehaviorId;
            try
            {
                nextBehaviorId = SelectNextBehavior(enemy.BehaviorGroupId, nextHistory);
            }
            catch
            {
                _random.State = randomStateBeforeSelection;
                throw;
            }

            var nextBehaviorIds = new Dictionary<CombatantId, int>(
                Layout.CurrentValue.BehaviorIdsByEnemy)
            {
                [enemyId] = nextBehaviorId
            };
            _historyByEnemy[enemyId] = nextHistory;
            _layout.Value = new EnemyIntentLayoutData(nextBehaviorIds);
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
            if (!Enum.IsDefined(typeof(cfg.battle.TargetRule), behavior.TargetRule))
                throw new InvalidOperationException($"Enemy behavior {behavior.Id} has an invalid target rule.");
            if (_tables.TbCardEffect.GetOrDefault(behavior.EffectId) == null)
                throw new InvalidOperationException(
                    $"Enemy behavior {behavior.Id} references missing effect {behavior.EffectId}.");
        }

        /// <summary>按行为组稳定顺序过滤候选，并以一次整数权重抽样选择下一行为。</summary>
        private int SelectNextBehavior(int behaviorGroupId, EnemyBehaviorHistory history)
        {
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
                throw new InvalidOperationException($"Enemy behavior group {behaviorGroupId} has no legal candidate.");

            int selectedBehaviorId;
            if (candidates.Count == 1)
            {
                selectedBehaviorId = candidates[0].Id;
            }
            else
            {
                int roll = _random.NextInt(totalWeight);
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

        /// <summary>保存单名敌人为冷却和最大连续次数所需的最小已完成历史。</summary>
        private sealed class EnemyBehaviorHistory
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
            private EnemyBehaviorHistory(
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
