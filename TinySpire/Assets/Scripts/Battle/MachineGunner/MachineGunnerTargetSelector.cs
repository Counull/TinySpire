using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>机枪兵程序可声明的稳定目标取得方式，不复用展示层的弱目标枚举。</summary>
    internal enum MachineGunnerTargetSelectionMode
    {
        PlayerSelectedEnemy,
        NearestLivingEnemy,
        NearestTwoLivingEnemies,
        FurthestLivingEnemy,
        AllLivingEnemies,
        RandomLivingEnemy,
        Self,
    }

    /// <summary>一次机枪兵目标解析的只读结果，目标顺序即后续程序的执行顺序。</summary>
    internal sealed class MachineGunnerTargetSelectionResult
    {
        /// <summary>解析是否已获得可执行目标。</summary>
        internal bool Succeeded => FailureReason == BattleCommandExecutionFailureReason.None;

        /// <summary>未能解析时的稳定命令失败原因。</summary>
        internal BattleCommandExecutionFailureReason FailureReason { get; }

        /// <summary>按 Encounter 或冻结随机选择得到的只读目标列表。</summary>
        internal IReadOnlyList<CombatantId> TargetIds { get; }

        /// <summary>冻结本次解析结果，失败时不允许携带目标。</summary>
        private MachineGunnerTargetSelectionResult(
            BattleCommandExecutionFailureReason failureReason,
            IEnumerable<CombatantId> targetIds)
        {
            if (targetIds == null)
                throw new ArgumentNullException(nameof(targetIds));

            var frozenTargetIds = new List<CombatantId>(targetIds);
            if (failureReason != BattleCommandExecutionFailureReason.None &&
                frozenTargetIds.Count > 0)
            {
                throw new ArgumentException("失败的目标解析不能携带可执行目标。", nameof(targetIds));
            }

            FailureReason = failureReason;
            TargetIds = new ReadOnlyCollection<CombatantId>(frozenTargetIds);
        }

        /// <summary>创建零目标、零随机写入的失败结果。</summary>
        internal static MachineGunnerTargetSelectionResult Failed(
            BattleCommandExecutionFailureReason failureReason)
        {
            if (failureReason == BattleCommandExecutionFailureReason.None)
                throw new ArgumentOutOfRangeException(nameof(failureReason));

            return new MachineGunnerTargetSelectionResult(
                failureReason,
                Array.Empty<CombatantId>());
        }

        /// <summary>创建已经冻结顺序的成功目标结果。</summary>
        internal static MachineGunnerTargetSelectionResult SucceededWith(
            IEnumerable<CombatantId> targetIds)
        {
            return new MachineGunnerTargetSelectionResult(
                BattleCommandExecutionFailureReason.None,
                targetIds);
        }
    }

    /// <summary>
    /// 机枪兵单场程序唯一的目标选择器；它只读取参与者和 Encounter 顺序，随机流由调用方以本地副本传入。
    /// </summary>
    internal sealed class MachineGunnerTargetSelector
    {
        private readonly BattleCombatantsData _combatants;
        private readonly IReadOnlyList<CombatantId> _enemyCombatantIdsInEncounterOrder;

        /// <summary>绑定本场参与者事实和不可变 Encounter 顺序。</summary>
        internal MachineGunnerTargetSelector(
            BattleCombatantsData combatants,
            IReadOnlyList<CombatantId> enemyCombatantIdsInEncounterOrder)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _enemyCombatantIdsInEncounterOrder = enemyCombatantIdsInEncounterOrder
                ?? throw new ArgumentNullException(nameof(enemyCombatantIdsInEncounterOrder));
        }

        /// <summary>
        /// 在当前权威参与者事实中解析一次程序目标；随机模式只推进传入的局部随机流，调用方可在完整预构建成功后再提交其状态。
        /// </summary>
        internal MachineGunnerTargetSelectionResult Resolve(
            MachineGunnerTargetSelectionMode mode,
            CombatantId actorId,
            CombatantId? selectedTargetId,
            GameRandom random)
        {
            IReadOnlyList<CombatantId> livingEnemies = GetLivingEnemiesInEncounterOrder();
            switch (mode)
            {
                case MachineGunnerTargetSelectionMode.PlayerSelectedEnemy:
                    return ResolvePlayerSelectedEnemy(selectedTargetId, livingEnemies);
                case MachineGunnerTargetSelectionMode.NearestLivingEnemy:
                    return ResolveSingleAutomaticTarget(
                        selectedTargetId,
                        livingEnemies,
                        targetIndex: 0);
                case MachineGunnerTargetSelectionMode.NearestTwoLivingEnemies:
                    return ResolveNearestTwoLivingEnemies(selectedTargetId, livingEnemies);
                case MachineGunnerTargetSelectionMode.FurthestLivingEnemy:
                    return ResolveSingleAutomaticTarget(
                        selectedTargetId,
                        livingEnemies,
                        targetIndex: livingEnemies.Count - 1);
                case MachineGunnerTargetSelectionMode.AllLivingEnemies:
                    return ResolveAllLivingEnemies(selectedTargetId, livingEnemies);
                case MachineGunnerTargetSelectionMode.RandomLivingEnemy:
                    return ResolveRandomLivingEnemy(selectedTargetId, livingEnemies, random);
                case MachineGunnerTargetSelectionMode.Self:
                    return ResolveSelf(actorId, selectedTargetId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        /// <summary>按 Encounter 顺序返回当前全部存活敌人，供多段和多目标程序在预构建中复用。</summary>
        internal IReadOnlyList<CombatantId> GetLivingEnemiesInEncounterOrder()
        {
            var livingEnemies = new List<CombatantId>();
            foreach (CombatantId enemyId in _enemyCombatantIdsInEncounterOrder)
            {
                if (IsLivingEnemy(enemyId))
                    livingEnemies.Add(enemyId);
            }

            return new ReadOnlyCollection<CombatantId>(livingEnemies);
        }

        /// <summary>按当前存活敌人的 Encounter 顺序读取指定位置，供“第二近”等可选后续目标安全复用。</summary>
        internal bool TryGetLivingEnemyAt(int livingEnemyIndex, out CombatantId enemyId)
        {
            if (livingEnemyIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(livingEnemyIndex));

            IReadOnlyList<CombatantId> livingEnemies = GetLivingEnemiesInEncounterOrder();
            if (livingEnemyIndex >= livingEnemies.Count)
            {
                enemyId = default;
                return false;
            }

            enemyId = livingEnemies[livingEnemyIndex];
            return true;
        }

        /// <summary>确认一个参与者标识仍代表存活敌人，不允许自动目标落到玩家或死亡对象。</summary>
        internal bool IsLivingEnemy(CombatantId combatantId)
        {
            return _combatants.TryGet(combatantId, out CombatantData combatant) &&
                combatant is EnemyCombatantData &&
                combatant.IsAlive;
        }

        /// <summary>冻结当前 Encounter 顺序中的前两名存活敌人；只有一名时仍返回成功的单目标快照。</summary>
        private static MachineGunnerTargetSelectionResult ResolveNearestTwoLivingEnemies(
            CombatantId? selectedTargetId,
            IReadOnlyList<CombatantId> livingEnemies)
        {
            if (selectedTargetId.HasValue)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch);
            }
            if (livingEnemies.Count == 0)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetNotAlive);
            }

            int targetCount = Math.Min(2, livingEnemies.Count);
            var targets = new List<CombatantId>(targetCount);
            for (int index = 0; index < targetCount; index++)
                targets.Add(livingEnemies[index]);
            return MachineGunnerTargetSelectionResult.SucceededWith(targets);
        }

        /// <summary>校验显式目标位于当前活敌快照中，缺失和失效目标分别返回稳定失败。</summary>
        private MachineGunnerTargetSelectionResult ResolvePlayerSelectedEnemy(
            CombatantId? selectedTargetId,
            IReadOnlyList<CombatantId> livingEnemies)
        {
            if (!selectedTargetId.HasValue)
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetRequired);
            if (!_combatants.TryGet(selectedTargetId.Value, out CombatantData target))
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetNotFound);
            }
            if (!(target is EnemyCombatantData) || !target.IsAlive)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetNotAlive);
            }
            if (!Contains(livingEnemies, selectedTargetId.Value))
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch);
            }

            return MachineGunnerTargetSelectionResult.SucceededWith(
                new[] { selectedTargetId.Value });
        }

        /// <summary>解析最近或最远这类单一自动目标，并拒绝调用方伪造另一个目标。</summary>
        private static MachineGunnerTargetSelectionResult ResolveSingleAutomaticTarget(
            CombatantId? selectedTargetId,
            IReadOnlyList<CombatantId> livingEnemies,
            int targetIndex)
        {
            if (livingEnemies.Count == 0)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetNotAlive);
            }

            CombatantId targetId = livingEnemies[targetIndex];
            if (selectedTargetId.HasValue && selectedTargetId.Value != targetId)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch);
            }

            return MachineGunnerTargetSelectionResult.SucceededWith(new[] { targetId });
        }

        /// <summary>解析全体目标并拒绝外部再附带单一目标，保持程序自己拥有顺序。</summary>
        private static MachineGunnerTargetSelectionResult ResolveAllLivingEnemies(
            CombatantId? selectedTargetId,
            IReadOnlyList<CombatantId> livingEnemies)
        {
            if (selectedTargetId.HasValue)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch);
            }
            if (livingEnemies.Count == 0)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetNotAlive);
            }

            return MachineGunnerTargetSelectionResult.SucceededWith(livingEnemies);
        }

        /// <summary>从已冻结的活敌候选中推进调用方随机流一次，并拒绝外部伪造随机结果。</summary>
        private static MachineGunnerTargetSelectionResult ResolveRandomLivingEnemy(
            CombatantId? selectedTargetId,
            IReadOnlyList<CombatantId> livingEnemies,
            GameRandom random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (selectedTargetId.HasValue)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch);
            }
            if (livingEnemies.Count == 0)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetNotAlive);
            }

            CombatantId targetId = livingEnemies[random.NextInt(livingEnemies.Count)];
            return MachineGunnerTargetSelectionResult.SucceededWith(new[] { targetId });
        }

        /// <summary>解析自目标并拒绝外部将程序重定向给其他参与者。</summary>
        private static MachineGunnerTargetSelectionResult ResolveSelf(
            CombatantId actorId,
            CombatantId? selectedTargetId)
        {
            if (selectedTargetId.HasValue && selectedTargetId.Value != actorId)
            {
                return MachineGunnerTargetSelectionResult.Failed(
                    BattleCommandExecutionFailureReason.TargetRuleMismatch);
            }

            return MachineGunnerTargetSelectionResult.SucceededWith(new[] { actorId });
        }

        /// <summary>在一次只读候选快照中判断目标身份是否存在。</summary>
        private static bool Contains(IReadOnlyList<CombatantId> targets, CombatantId targetId)
        {
            foreach (CombatantId candidateId in targets)
            {
                if (candidateId == targetId)
                    return true;
            }

            return false;
        }
    }
}
