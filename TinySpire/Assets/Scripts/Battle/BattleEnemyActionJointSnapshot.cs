using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>联合预构建时冻结的单名玩家完整回合事实。</summary>
    internal readonly struct BattlePlayerTurnAuthoritySnapshot
    {
        /// <summary>预构建时的当前能量。</summary>
        internal int Energy { get; }

        /// <summary>预构建时是否已经结束行动。</summary>
        internal bool HasEndedAction { get; }

        /// <summary>复制一名玩家的完整回合事实。</summary>
        internal BattlePlayerTurnAuthoritySnapshot(PlayerTurnData player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            Energy = player.Energy;
            HasEndedAction = player.HasEndedAction;
        }

        /// <summary>比较当前玩家事实是否仍等于预构建快照。</summary>
        internal bool Matches(PlayerTurnData player)
        {
            return player != null &&
                   player.Energy == Energy &&
                   player.HasEndedAction == HasEndedAction;
        }
    }

    /// <summary>联合预构建时冻结的完整 BattleTurnData 权威事实。</summary>
    internal sealed class BattleTurnAuthoritySnapshot
    {
        /// <summary>预构建时的权威阶段。</summary>
        internal BattleTurnPhase Phase { get; }

        /// <summary>预构建时的权威轮次。</summary>
        internal int RoundNumber { get; }

        /// <summary>预构建时全部玩家的完整只读回合事实。</summary>
        internal IReadOnlyDictionary<CombatantId, BattlePlayerTurnAuthoritySnapshot> Players { get; }

        /// <summary>预构建时的当前行动敌人。</summary>
        internal CombatantId? CurrentActingEnemyId { get; }

        /// <summary>复制并冻结完整回合事实。</summary>
        internal BattleTurnAuthoritySnapshot(BattleTurnData turn)
        {
            if (turn == null)
                throw new ArgumentNullException(nameof(turn));

            var players = new Dictionary<CombatantId, BattlePlayerTurnAuthoritySnapshot>(
                turn.Players.Count);
            foreach (KeyValuePair<CombatantId, PlayerTurnData> pair in turn.Players)
                players.Add(pair.Key, new BattlePlayerTurnAuthoritySnapshot(pair.Value));

            Phase = turn.Phase;
            RoundNumber = turn.RoundNumber;
            Players = new ReadOnlyDictionary<CombatantId, BattlePlayerTurnAuthoritySnapshot>(players);
            CurrentActingEnemyId = turn.CurrentActingEnemyId;
        }

        /// <summary>比较阶段、轮次、行动敌人与全部玩家事实是否仍等于本快照。</summary>
        internal bool Matches(BattleTurnData turn)
        {
            if (turn == null ||
                turn.Phase != Phase ||
                turn.RoundNumber != RoundNumber ||
                turn.CurrentActingEnemyId != CurrentActingEnemyId ||
                turn.Players.Count != Players.Count)
            {
                return false;
            }

            foreach (KeyValuePair<CombatantId, BattlePlayerTurnAuthoritySnapshot> pair in Players)
            {
                if (!turn.Players.TryGetValue(pair.Key, out PlayerTurnData player) ||
                    !pair.Value.Matches(player))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>联合计划明确冻结的可选 Queue continuation。</summary>
    internal sealed class BattleEnemyActionContinuationSnapshot
    {
        /// <summary>没有后继命令时为空；有值时只保存受支持系统命令的不可变副本。</summary>
        internal BattleCommand Command { get; }

        /// <summary>指示联合计划是否预定了后继命令。</summary>
        internal bool HasCommand => Command != null;

        /// <summary>复制受支持的系统 continuation，避免依赖调用方对象身份。</summary>
        internal BattleEnemyActionContinuationSnapshot(BattleCommand command)
        {
            Command = Freeze(command);
        }

        /// <summary>把已知不可变系统命令复制为联合计划持有的独立值。</summary>
        private static BattleCommand Freeze(BattleCommand command)
        {
            if (command == null)
                return null;
            if (command is CompleteEnemyActionCommand completeEnemyAction)
                return new CompleteEnemyActionCommand(completeEnemyAction.EnemyId);

            throw new ArgumentException(
                "敌人联合计划只允许冻结系统 continuation。",
                nameof(command));
        }
    }

    /// <summary>
    /// 敌人行动首次写入前的联合权威快照。
    /// 同时冻结 source、target、Turn、Intent 与预定 continuation，并提供状态投影事实。
    /// </summary>
    internal sealed class BattleEnemyActionJointInitialSnapshot
    {
        /// <summary>敌人 source 的初始四标量快照。</summary>
        internal BattleCombatantScalarSnapshot Source { get; }

        /// <summary>显式 target 的初始四标量快照。</summary>
        internal BattleCombatantScalarSnapshot Target { get; }

        /// <summary>完整回合权威快照。</summary>
        internal BattleTurnAuthoritySnapshot Turn { get; }

        /// <summary>当前意图、真实历史与随机状态的权威快照。</summary>
        internal BattleEnemyIntentAuthoritySnapshot Intent { get; }

        /// <summary>按敌人行为配置顺序冻结的 Effect 标识；M8 单行动固定恰好一个。</summary>
        internal IReadOnlyList<BattleEffectId> EffectIds { get; }

        /// <summary>从初始 source 先清 Block 后得到的 Effect 输入事实。</summary>
        internal BattleEffectTargetSnapshot SourceBeforeEffect { get; }

        /// <summary>联合计划预定的可选 Queue continuation。</summary>
        internal BattleEnemyActionContinuationSnapshot Continuation { get; }

        /// <summary>零写入捕获敌人行动依赖的全部初始权威事实。</summary>
        internal BattleEnemyActionJointInitialSnapshot(
            EnemyCombatantData source,
            CombatantData target,
            BattleTurnData turn,
            BattleEnemyIntentsData intents,
            IEnumerable<BattleEffectId> effectIds,
            BattleCommand continuation)
            : this(
                source,
                target,
                turn,
                CaptureIntentSnapshot(source, intents),
                effectIds,
                continuation)
        {
        }

        /// <summary>先验证兼容入口依赖，再只捕获一次属于该 source 的意图权威快照。</summary>
        private static BattleEnemyIntentAuthoritySnapshot CaptureIntentSnapshot(
            EnemyCombatantData source,
            BattleEnemyIntentsData intents)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (intents == null)
                throw new ArgumentNullException(nameof(intents));

            return intents.CaptureAuthoritySnapshot(source.Id);
        }

        /// <summary>复用调用方已捕获的唯一 Intent 快照，避免联合 prepare 二次读取权威事实。</summary>
        internal BattleEnemyActionJointInitialSnapshot(
            EnemyCombatantData source,
            CombatantData target,
            BattleTurnData turn,
            BattleEnemyIntentAuthoritySnapshot intent,
            IEnumerable<BattleEffectId> effectIds,
            BattleCommand continuation)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (intent == null)
                throw new ArgumentNullException(nameof(intent));
            if (effectIds == null)
                throw new ArgumentNullException(nameof(effectIds));
            if (intent.EnemyId != source.Id)
                throw new ArgumentException("Intent 快照必须属于当前敌人 source。", nameof(intent));

            var copiedEffectIds = new List<BattleEffectId>(effectIds);
            if (copiedEffectIds.Count != 1)
            {
                throw new ArgumentException(
                    "M8 敌人单次行动必须恰好引用一个 Effect。",
                    nameof(effectIds));
            }

            Source = new BattleCombatantScalarSnapshot(source);
            Target = new BattleCombatantScalarSnapshot(target);
            Turn = new BattleTurnAuthoritySnapshot(turn);
            Intent = intent;
            EffectIds = new ReadOnlyCollection<BattleEffectId>(copiedEffectIds);
            SourceBeforeEffect = BattleStatusTiming.Project(
                BattleStatusTimingPoint.EnemyActionStarted,
                new BattleEffectTargetSnapshot(
                    Source.Health,
                    Source.Block,
                    Source.Vulnerable));
            Continuation = new BattleEnemyActionContinuationSnapshot(continuation);
        }

        /// <summary>从 Effect 后 source 事实投影本次行动结束时的 Vulnerable 衰减。</summary>
        internal BattleEffectTargetSnapshot ProjectSourceAfterEffect(
            BattleEffectTargetSnapshot sourceAfterEffect)
        {
            return BattleStatusTiming.Project(
                BattleStatusTimingPoint.EnemyActionCompleted,
                sourceAfterEffect);
        }

        /// <summary>一次性比较 source、target、Turn 与 Intent 的全部初始权威事实。</summary>
        internal bool Matches(
            CombatantData source,
            CombatantData target,
            BattleTurnData turn,
            BattleEnemyIntentsData intents)
        {
            return MatchesWithoutIntent(source, target, turn) && Intent.Matches(intents);
        }

        /// <summary>比较联合 source、target 与 Turn，让同一 Intent 快照由三段式 validator 唯一消费。</summary>
        internal bool MatchesWithoutIntent(
            CombatantData source,
            CombatantData target,
            BattleTurnData turn)
        {
            return Source.Matches(source) &&
                   Target.Matches(target) &&
                   Turn.Matches(turn);
        }
    }

    /// <summary>强制联合事务只验证一次、只提交一次且提交阶段不复验初始事实。</summary>
    internal sealed class BattleEnemyActionJointCommitGuard
    {
        private readonly BattleEnemyActionJointInitialSnapshot _snapshot;
        private bool _validationAttempted;
        private bool _validated;
        private bool _commitAttempted;

        /// <summary>联合初始事实是否已经完成唯一一次成功验证。</summary>
        internal bool IsValidated => _validationAttempted && _validated;

        /// <summary>联合提交是否已经消费。</summary>
        internal bool IsCommitted => _commitAttempted;

        /// <summary>绑定一份不可替换的联合初始快照。</summary>
        internal BattleEnemyActionJointCommitGuard(BattleEnemyActionJointInitialSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <summary>首次写入前执行唯一一次联合验证；失败后也禁止用同一计划重试。</summary>
        internal bool ValidateInitial(
            CombatantData source,
            CombatantData target,
            BattleTurnData turn,
            BattleEnemyIntentsData intents)
        {
            if (_validationAttempted)
                throw new InvalidOperationException("敌人联合计划已经执行过初始验证。");

            _validationAttempted = true;
            _validated = _snapshot.Matches(source, target, turn, intents);
            return _validated;
        }

        /// <summary>联合校验 source/target/Turn，并让同一 Intent plan 完成唯一权威校验。</summary>
        internal bool ValidateInitial(
            CombatantData source,
            CombatantData target,
            BattleTurnData turn,
            Func<bool> validateIntent)
        {
            if (_validationAttempted)
                throw new InvalidOperationException("敌人联合计划已经执行过初始验证。");
            if (validateIntent == null)
                throw new ArgumentNullException(nameof(validateIntent));

            _validationAttempted = true;
            _validated = _snapshot.MatchesWithoutIntent(source, target, turn) && validateIntent();
            return _validated;
        }

        /// <summary>提交已经验证的联合计划一次；不接收当前事实，因此不会复验中间写入。</summary>
        internal void Commit(Action commitAction)
        {
            if (!_validationAttempted || !_validated)
                throw new InvalidOperationException("敌人联合计划尚未通过初始验证。");
            if (_commitAttempted)
                throw new InvalidOperationException("敌人联合计划已经提交。");
            if (commitAction == null)
                throw new ArgumentNullException(nameof(commitAction));

            _commitAttempted = true;
            commitAction();
        }
    }
}
