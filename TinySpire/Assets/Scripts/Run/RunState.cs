using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>一局 Run 的稳定唯一标识。</summary>
    public readonly struct RunId : IEquatable<RunId>
    {
        /// <summary>底层不可变标识值。</summary>
        public Guid Value { get; }

        /// <summary>从非空 Guid 创建 Run 标识。</summary>
        public RunId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Run id cannot be empty.", nameof(value));

            Value = value;
        }

        /// <summary>比较两个 Run 标识是否相同。</summary>
        public bool Equals(RunId other)
        {
            return Value.Equals(other.Value);
        }

        /// <summary>比较此标识与另一个对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is RunId other && Equals(other);
        }

        /// <summary>返回稳定哈希值。</summary>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>返回便于日志读取的标识文本。</summary>
        public override string ToString()
        {
            return Value.ToString("D");
        }

        /// <summary>判断两个 Run 标识是否相同。</summary>
        public static bool operator ==(RunId left, RunId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个 Run 标识是否不同。</summary>
        public static bool operator !=(RunId left, RunId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>一局 Act 地图进度的互斥权威阶段。</summary>
    public enum RunProgressPhase
    {
        MapReady,
        EncounterCommitted,
        InBattle,
        RewardPending,
        BossGateReached,
        Terminal,
        NodeVisitPending,
    }

    /// <summary>旧调用方读取普通失败原因的兼容投影；终局权威事实由 RunOutcome 持有。</summary>
    public enum RunTerminalReason
    {
        Defeat,
    }

    /// <summary>Run 唯一终局事实的封闭业务分类。</summary>
    public enum RunOutcomeKind
    {
        Victory,
        Defeat,
        Abandoned,
    }

    /// <summary>由 RunStateStore 一次冻结的不可变终局事实。</summary>
    public sealed class RunOutcome
    {
        /// <summary>所属 Run 的稳定身份。</summary>
        public RunId RunId { get; }

        /// <summary>本次终局的封闭业务分类。</summary>
        public RunOutcomeKind Kind { get; }

        /// <summary>胜败所绑定的唯一 Battle；主动放弃没有 Battle。</summary>
        public RunBattleId? BattleId { get; }

        /// <summary>冻结并验证一次胜败或主动放弃终局。</summary>
        internal RunOutcome(
            RunId runId,
            RunOutcomeKind kind,
            RunBattleId? battleId)
        {
            bool requiresBattle = kind == RunOutcomeKind.Victory ||
                                  kind == RunOutcomeKind.Defeat;
            if (requiresBattle != battleId.HasValue)
            {
                throw new ArgumentException(
                    "Victory and Defeat require one battle, while Abandoned cannot carry one.",
                    nameof(battleId));
            }
            if (battleId.HasValue && battleId.Value.RunId != runId)
                throw new ArgumentException("Run outcome battle belongs to another Run.", nameof(battleId));

            RunId = runId;
            Kind = kind;
            BattleId = battleId;
        }
    }

    /// <summary>创建新 Run 所需的已验证静态事实与根随机输入。</summary>
    public sealed class RunCreationOptions
    {
        /// <summary>新 Run 的稳定身份。</summary>
        public RunId RunId { get; }

        /// <summary>本 Run 唯一英雄模板标识。</summary>
        public int HeroTemplateId { get; }

        /// <summary>创建时当前生命。</summary>
        public int InitialHealth { get; }

        /// <summary>本 Run 角色生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>创建时已从初始牌组模板一次展开的有序 RunDeck。</summary>
        public RunDeck RunDeck { get; }

        /// <summary>创建时冻结的遗物、药水与金币持有物。</summary>
        public RunHoldings Holdings { get; }

        /// <summary>后续本战随机输入的非零根种子。</summary>
        public uint RandomRootSeed { get; }

        /// <summary>创建时已完整生成并通过校验的不可变 Act 地图。</summary>
        public MapDefinition Map { get; }

        /// <summary>冻结并验证新 Run 的全部创建输入。</summary>
        public RunCreationOptions(
            RunId runId,
            int heroTemplateId,
            int initialHealth,
            int maxHealth,
            RunDeck runDeck,
            uint randomRootSeed,
            MapDefinition map,
            RunHoldings holdings = null)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run id cannot be empty.", nameof(runId));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (initialHealth <= 0 || initialHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(initialHealth));
            if (runDeck == null)
                throw new ArgumentNullException(nameof(runDeck));
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            RunId = runId;
            HeroTemplateId = heroTemplateId;
            InitialHealth = initialHealth;
            MaxHealth = maxHealth;
            RunDeck = runDeck;
            Holdings = holdings ?? RunHoldings.Empty(initialGold: 100);
            RandomRootSeed = randomRootSeed;
            Map = map;
        }
    }

    /// <summary>从已验证存档恢复一份地图稳定 Run 所需的全部领域事实。</summary>
    public sealed class RunRestoreOptions
    {
        public RunId RunId { get; }
        public int HeroTemplateId { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
        public RunDeck RunDeck { get; }
        public RunHoldings Holdings { get; }
        public uint RandomRootSeed { get; }
        public MapDefinition Map { get; }
        public IReadOnlyList<MapNodeId> PathNodeIds { get; }
        public RunProgressPhase ProgressPhase { get; }
        public MapNodeId? CommittedNodeId { get; }
        public RunOutcomeKind? OutcomeKind { get; }

        /// <summary>旧失败终局读取口径的派生兼容投影，不保存第二份业务事实。</summary>
        public RunTerminalReason? TerminalReason => OutcomeKind == RunOutcomeKind.Defeat
            ? RunTerminalReason.Defeat
            : (RunTerminalReason?)null;
        public int BattleAttemptSequence { get; }
        public PendingCardReward PendingCardReward { get; }
        public PendingRunNodeVisit PendingNodeVisit { get; }

        /// <summary>冻结并验证一份不含 Battle transient 的稳定恢复输入。</summary>
        internal RunRestoreOptions(
            RunId runId,
            int heroTemplateId,
            int currentHealth,
            int maxHealth,
            RunDeck runDeck,
            uint randomRootSeed,
            MapDefinition map,
            IReadOnlyList<MapNodeId> pathNodeIds,
            RunProgressPhase progressPhase,
            MapNodeId? committedNodeId,
            RunOutcomeKind? outcomeKind,
            RunHoldings holdings,
            PendingCardReward pendingCardReward = null,
            PendingRunNodeVisit pendingNodeVisit = null)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run id cannot be empty.", nameof(runId));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (currentHealth < 0 || currentHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            if (runDeck == null)
                throw new ArgumentNullException(nameof(runDeck));
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (holdings == null)
                throw new ArgumentNullException(nameof(holdings));
            if (pathNodeIds == null || pathNodeIds.Count == 0)
                throw new ArgumentException("A restored Run path must contain Start.", nameof(pathNodeIds));
            if (progressPhase != RunProgressPhase.MapReady &&
                progressPhase != RunProgressPhase.RewardPending &&
                progressPhase != RunProgressPhase.BossGateReached &&
                progressPhase != RunProgressPhase.Terminal &&
                progressPhase != RunProgressPhase.NodeVisitPending)
            {
                throw new ArgumentOutOfRangeException(nameof(progressPhase));
            }
            if (progressPhase == RunProgressPhase.Terminal && outcomeKind == null)
                throw new ArgumentException("A terminal Run requires an outcome.", nameof(outcomeKind));
            if (progressPhase != RunProgressPhase.Terminal && outcomeKind != null)
                throw new ArgumentException("Only a terminal Run can carry an outcome.", nameof(outcomeKind));
            if (outcomeKind == RunOutcomeKind.Victory && currentHealth <= 0)
                throw new ArgumentException("Victory requires positive remaining health.", nameof(currentHealth));
            if (outcomeKind == RunOutcomeKind.Defeat && currentHealth != 0)
                throw new ArgumentException("Defeat requires zero remaining health.", nameof(currentHealth));
            if (outcomeKind == RunOutcomeKind.Abandoned && currentHealth <= 0)
                throw new ArgumentException("Abandoned requires an active hero.", nameof(currentHealth));
            bool isBattleOutcome = outcomeKind == RunOutcomeKind.Victory ||
                                   outcomeKind == RunOutcomeKind.Defeat;
            if (isBattleOutcome && committedNodeId == null)
                throw new ArgumentException("A battle outcome requires its committed node.", nameof(committedNodeId));
            if (outcomeKind == RunOutcomeKind.Abandoned && committedNodeId != null)
                throw new ArgumentException("Abandoned cannot carry a committed node.", nameof(committedNodeId));
            if (currentHealth == 0 && progressPhase != RunProgressPhase.Terminal)
                throw new ArgumentException("Only a terminal Run can have zero health.", nameof(currentHealth));
            if ((progressPhase == RunProgressPhase.RewardPending) != (pendingCardReward != null))
            {
                throw new ArgumentException(
                    "Only RewardPending restore facts may carry a pending card reward.",
                    nameof(pendingCardReward));
            }
            if ((progressPhase == RunProgressPhase.NodeVisitPending) !=
                (pendingNodeVisit != null))
            {
                throw new ArgumentException(
                    "Only NodeVisitPending restore facts may carry a pending node visit.",
                    nameof(pendingNodeVisit));
            }
            RunId = runId;
            HeroTemplateId = heroTemplateId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            RunDeck = runDeck;
            Holdings = holdings;
            RandomRootSeed = randomRootSeed;
            Map = map;
            PathNodeIds = Array.AsReadOnly(pathNodeIds.ToArray());
            ProgressPhase = progressPhase;
            CommittedNodeId = committedNodeId;
            OutcomeKind = outcomeKind;
            BattleAttemptSequence = DeriveBattleAttemptSequence(
                map,
                pathNodeIds,
                progressPhase,
                outcomeKind);
            PendingCardReward = pendingCardReward;
            PendingNodeVisit = pendingNodeVisit;
        }

        /// <summary>把旧 Terminal(Defeat) 恢复入口无损投影到唯一 Defeat outcome。</summary>
        internal RunRestoreOptions(
            RunId runId,
            int heroTemplateId,
            int currentHealth,
            int maxHealth,
            RunDeck runDeck,
            uint randomRootSeed,
            MapDefinition map,
            IReadOnlyList<MapNodeId> pathNodeIds,
            RunProgressPhase progressPhase,
            MapNodeId? committedNodeId,
            RunTerminalReason? terminalReason,
            RunHoldings holdings,
            PendingCardReward pendingCardReward = null,
            PendingRunNodeVisit pendingNodeVisit = null)
            : this(
                runId,
                heroTemplateId,
                currentHealth,
                maxHealth,
                runDeck,
                randomRootSeed,
                map,
                pathNodeIds,
                progressPhase,
                committedNodeId,
                terminalReason.HasValue
                    ? RunOutcomeKind.Defeat
                    : (RunOutcomeKind?)null,
                holdings,
                pendingCardReward,
                pendingNodeVisit)
        {
        }

        /// <summary>无重试语义下，从已完成 Combat/Elite 与可选当前 Battle 唯一推导 attempt 序号。</summary>
        internal static int DeriveBattleAttemptSequence(
            MapDefinition map,
            IReadOnlyList<MapNodeId> pathNodeIds,
            RunProgressPhase progressPhase,
            RunOutcomeKind? outcomeKind = null)
        {
            int completedRewardBattleCount = 0;
            foreach (MapNodeId nodeId in pathNodeIds)
            {
                MapNodeKind kind = map.GetNode(nodeId).Kind;
                if (kind == MapNodeKind.Combat || kind == MapNodeKind.Elite)
                    completedRewardBattleCount = checked(completedRewardBattleCount + 1);
            }

            bool hasUncompletedBattle = progressPhase == RunProgressPhase.RewardPending ||
                                        (progressPhase == RunProgressPhase.Terminal &&
                                         outcomeKind != RunOutcomeKind.Abandoned);
            return hasUncompletedBattle
                ? checked(completedRewardBattleCount + 1)
                : completedRewardBattleCount;
        }
    }

    /// <summary>一局 Run 内某次战斗尝试的稳定关联标识。</summary>
    public readonly struct RunBattleId : IEquatable<RunBattleId>
    {
        /// <summary>所属 Run 标识。</summary>
        public RunId RunId { get; }

        /// <summary>所属 Run 内从一开始递增的尝试序号。</summary>
        public int AttemptSequence { get; }

        /// <summary>该 attempt 唯一绑定的冻结地图节点。</summary>
        public MapNodeId NodeId { get; }

        /// <summary>组合 Run、正数尝试序号与节点身份。</summary>
        public RunBattleId(RunId runId, int attemptSequence, MapNodeId nodeId)
        {
            if (attemptSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(attemptSequence));
            if (string.IsNullOrEmpty(nodeId.Value))
                throw new ArgumentException("Battle node id cannot be empty.", nameof(nodeId));

            RunId = runId;
            AttemptSequence = attemptSequence;
            NodeId = nodeId;
        }

        /// <summary>比较两个本战标识是否相同。</summary>
        public bool Equals(RunBattleId other)
        {
            return RunId == other.RunId &&
                   AttemptSequence == other.AttemptSequence &&
                   NodeId == other.NodeId;
        }

        /// <summary>比较此标识与另一个对象是否相同。</summary>
        public override bool Equals(object obj)
        {
            return obj is RunBattleId other && Equals(other);
        }

        /// <summary>返回可用于字典键的稳定哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (RunId.GetHashCode() * 397) ^ AttemptSequence;
                return (hash * 397) ^ NodeId.GetHashCode();
            }
        }

        /// <summary>判断两个本战标识是否相同。</summary>
        public static bool operator ==(RunBattleId left, RunBattleId right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个本战标识是否不同。</summary>
        public static bool operator !=(RunBattleId left, RunBattleId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>即将由 Battle setup seam 消费的单场不可变输入。</summary>
    public sealed class RunBattleInput
    {
        /// <summary>当前尝试的跨层关联标识。</summary>
        public RunBattleId BattleId { get; }

        /// <summary>本战唯一英雄模板标识。</summary>
        public int HeroTemplateId { get; }

        /// <summary>本战初始当前生命。</summary>
        public int InitialHealth { get; }

        /// <summary>本战玩家生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>按 RunDeck 顺序复制的不可变实例投影。</summary>
        public IReadOnlyList<RunCard> RunCards { get; }

        /// <summary>按 Run 快照签发的不可变遗物、药水与金币投影。</summary>
        public RunHoldings Holdings { get; }

        /// <summary>本战在唯一 MapDefinition 中冻结的节点类型。</summary>
        public MapNodeKind NodeKind { get; }

        /// <summary>地图节点冻结的内容身份；Boss 时是已明牌 Boss identity。</summary>
        public int MapNodeContentId { get; }

        /// <summary>本战遭遇模板标识。</summary>
        public int EncounterTemplateId { get; }

        /// <summary>本战全部规则随机域的非零输入种子。</summary>
        public uint RandomSeed { get; }

        /// <summary>冻结一次由 Run 唯一签发的 Battle setup 输入。</summary>
        internal RunBattleInput(
            RunState state,
            MapNode battleNode,
            int encounterTemplateId,
            int attemptSequence,
            uint randomSeed)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (battleNode == null)
                throw new ArgumentNullException(nameof(battleNode));
            if (encounterTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(encounterTemplateId));
            if (attemptSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(attemptSequence));
            if (randomSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomSeed));

            if (battleNode.Kind != MapNodeKind.Combat &&
                battleNode.Kind != MapNodeKind.Elite &&
                battleNode.Kind != MapNodeKind.Boss)
            {
                throw new InvalidOperationException(
                    "Only Combat, Elite or Boss nodes can create Run battle input.");
            }

            BattleId = new RunBattleId(state.RunId, attemptSequence, battleNode.Id);
            HeroTemplateId = state.HeroTemplateId;
            InitialHealth = state.CurrentHealth;
            MaxHealth = state.MaxHealth;
            RunCards = Array.AsReadOnly(state.RunDeck.Cards.ToArray());
            Holdings = state.Holdings;
            NodeKind = battleNode.Kind;
            MapNodeContentId = battleNode.ContentId;
            EncounterTemplateId = encounterTemplateId;
            RandomSeed = randomSeed;
        }
    }

    /// <summary>跨场景存活的一局 Run 不可变业务事实。</summary>
    public sealed class RunState
    {
        private readonly ReadOnlyCollection<MapNodeId> _pathNodeIds;

        /// <summary>本 Run 的稳定身份。</summary>
        public RunId RunId { get; }

        /// <summary>本 Run 唯一英雄模板标识。</summary>
        public int HeroTemplateId { get; }

        /// <summary>当前跨战斗生命。</summary>
        public int CurrentHealth { get; }

        /// <summary>本 Run 生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>跨战斗保持顺序、实例身份与升级事实的不可变牌组。</summary>
        public RunDeck RunDeck { get; }

        /// <summary>跨战斗保持顺序、实例身份与金币事实的不可变持有物。</summary>
        public RunHoldings Holdings { get; }

        /// <summary>派生本战随机输入的根种子。</summary>
        public uint RandomRootSeed { get; }

        /// <summary>本 Run 开局一次生成后冻结的完整 Act 地图。</summary>
        public MapDefinition MapDefinition { get; }

        /// <summary>从 Start 到已完成普通节点的稳定路径；抵达 Boss 门后末项就是尚待结算的 Boss。</summary>
        public IReadOnlyList<MapNodeId> PathNodeIds => _pathNodeIds;

        /// <summary>最后一个已完成或已抵达节点。</summary>
        public MapNodeId CurrentNodeId => _pathNodeIds[_pathNodeIds.Count - 1];

        /// <summary>已选择且正在结算的 Combat、Elite 或 Boss 节点。</summary>
        public MapNodeId? CommittedNodeId { get; }

        /// <summary>当前互斥地图/战斗/终局阶段。</summary>
        public RunProgressPhase ProgressPhase { get; }

        /// <summary>Terminal 阶段唯一不可变终局事实，非终局为空。</summary>
        public RunOutcome Outcome { get; }

        /// <summary>旧失败终局读取口径的派生兼容投影，不保存第二份业务事实。</summary>
        public RunTerminalReason? TerminalReason => Outcome?.Kind == RunOutcomeKind.Defeat
            ? RunTerminalReason.Defeat
            : (RunTerminalReason?)null;

        /// <summary>已经签发过的本战随机输入序号。</summary>
        public int BattleAttemptSequence { get; }

        /// <summary>当前有效的本战输入；尚未入战时为空。</summary>
        public RunBattleInput ActiveBattle { get; }

        /// <summary>Combat/Elite 胜利后已冻结且尚未选择或跳过的卡牌奖励。</summary>
        public PendingCardReward PendingCardReward { get; }

        /// <summary>已耐久进入且尚未完成的一次非战斗节点访问。</summary>
        public PendingRunNodeVisit PendingNodeVisit { get; }

        /// <summary>从已验证创建参数建立初始 Run 事实。</summary>
        internal RunState(RunCreationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            RunId = options.RunId;
            HeroTemplateId = options.HeroTemplateId;
            CurrentHealth = options.InitialHealth;
            MaxHealth = options.MaxHealth;
            RunDeck = options.RunDeck;
            Holdings = options.Holdings;
            RandomRootSeed = options.RandomRootSeed;
            MapDefinition = options.Map;
            _pathNodeIds = Array.AsReadOnly(new[]
            {
                MapNodeId.FromPosition(layer: 0, slot: 0),
            });
            CommittedNodeId = null;
            ProgressPhase = RunProgressPhase.MapReady;
            Outcome = null;
            BattleAttemptSequence = 0;
            ActiveBattle = null;
            PendingCardReward = null;
            PendingNodeVisit = null;
            ValidateShape();
        }

        /// <summary>从已验证存档输入重建一份没有 Battle transient 的稳定 RunState。</summary>
        internal RunState(RunRestoreOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            RunId = options.RunId;
            HeroTemplateId = options.HeroTemplateId;
            CurrentHealth = options.CurrentHealth;
            MaxHealth = options.MaxHealth;
            RunDeck = options.RunDeck;
            Holdings = options.Holdings;
            RandomRootSeed = options.RandomRootSeed;
            MapDefinition = options.Map;
            _pathNodeIds = Array.AsReadOnly(options.PathNodeIds.ToArray());
            CommittedNodeId = options.CommittedNodeId;
            ProgressPhase = options.ProgressPhase;
            BattleAttemptSequence = options.BattleAttemptSequence;
            Outcome = options.OutcomeKind.HasValue
                ? new RunOutcome(
                    RunId,
                    options.OutcomeKind.Value,
                    options.OutcomeKind == RunOutcomeKind.Abandoned
                        ? (RunBattleId?)null
                        : new RunBattleId(
                            RunId,
                            BattleAttemptSequence,
                            options.CommittedNodeId.Value))
                : null;
            ActiveBattle = null;
            PendingCardReward = options.PendingCardReward;
            PendingNodeVisit = options.PendingNodeVisit;
            ValidateShape();
        }

        /// <summary>复制不变 Run 事实，并原子替换生命、路径、阶段、attempt 与本战输入。</summary>
        internal RunState(
            RunState previous,
            int currentHealth,
            IReadOnlyList<MapNodeId> pathNodeIds,
            RunProgressPhase progressPhase,
            MapNodeId? committedNodeId,
            int battleAttemptSequence,
            RunBattleInput activeBattle,
            RunOutcome outcome,
            PendingCardReward pendingCardReward = null,
            RunDeck runDeck = null,
            RunHoldings holdings = null,
            PendingRunNodeVisit pendingNodeVisit = null)
        {
            if (previous == null)
                throw new ArgumentNullException(nameof(previous));
            if (currentHealth < 0 || currentHealth > previous.MaxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            if (pathNodeIds == null || pathNodeIds.Count == 0)
                throw new ArgumentException("A Run path must contain Start.", nameof(pathNodeIds));
            if (battleAttemptSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(battleAttemptSequence));

            RunId = previous.RunId;
            HeroTemplateId = previous.HeroTemplateId;
            CurrentHealth = currentHealth;
            MaxHealth = previous.MaxHealth;
            RunDeck = runDeck ?? previous.RunDeck;
            Holdings = holdings ?? previous.Holdings;
            RandomRootSeed = previous.RandomRootSeed;
            MapDefinition = previous.MapDefinition;
            _pathNodeIds = Array.AsReadOnly(pathNodeIds.ToArray());
            CommittedNodeId = committedNodeId;
            ProgressPhase = progressPhase;
            Outcome = outcome;
            BattleAttemptSequence = battleAttemptSequence;
            ActiveBattle = activeBattle;
            PendingCardReward = pendingCardReward;
            PendingNodeVisit = pendingNodeVisit;
            ValidateShape();
        }

        /// <summary>验证不可变快照内部阶段、地图、路径与 transient 的组合一致。</summary>
        private void ValidateShape()
        {
            if (Holdings == null)
                throw new InvalidOperationException("Run holdings cannot be null.");

            ValidateCompletedPath();

            bool activeBattleMatches = ProgressPhase == RunProgressPhase.InBattle
                ? ActiveBattle != null &&
                  CommittedNodeId != null &&
                  ActiveBattle.BattleId.NodeId == CommittedNodeId.Value &&
                  ActiveBattle.BattleId.RunId == RunId &&
                  ActiveBattle.BattleId.AttemptSequence == BattleAttemptSequence
                : ActiveBattle == null;
            if (!activeBattleMatches)
                throw new InvalidOperationException("Run battle transient does not match its progress phase.");

            bool pendingRewardMatches = ProgressPhase == RunProgressPhase.RewardPending
                ? PendingCardReward != null &&
                  CommittedNodeId != null &&
                  PendingCardReward.Id.BattleId.RunId == RunId &&
                  PendingCardReward.Id.BattleId.AttemptSequence == BattleAttemptSequence &&
                  PendingCardReward.Id.BattleId.NodeId == CommittedNodeId.Value
                : PendingCardReward == null;
            if (!pendingRewardMatches)
            {
                throw new InvalidOperationException(
                    "Run pending card reward does not match its progress phase.");
            }

            bool pendingNodeVisitMatches = ProgressPhase == RunProgressPhase.NodeVisitPending
                ? PendingNodeVisit != null &&
                  PendingNodeVisit.Id.RunId == RunId
                : PendingNodeVisit == null;
            if (!pendingNodeVisitMatches)
            {
                throw new InvalidOperationException(
                    "Run pending node visit does not match its progress phase.");
            }

            switch (ProgressPhase)
            {
                case RunProgressPhase.MapReady:
                    if (CommittedNodeId != null || Outcome != null || CurrentHealth <= 0)
                        throw new InvalidOperationException("MapReady Run facts are inconsistent.");
                    break;
                case RunProgressPhase.EncounterCommitted:
                case RunProgressPhase.InBattle:
                    ValidateCommittedBattleNode(allowBossAtCurrentNode: ProgressPhase == RunProgressPhase.InBattle);
                    if (Outcome != null || CurrentHealth <= 0)
                        throw new InvalidOperationException("Active encounter Run facts are inconsistent.");
                    break;
                case RunProgressPhase.BossGateReached:
                    if (CommittedNodeId != null ||
                        Outcome != null ||
                        MapDefinition.GetNode(CurrentNodeId).Kind != MapNodeKind.Boss ||
                        CurrentHealth <= 0)
                    {
                        throw new InvalidOperationException("Boss gate Run facts are inconsistent.");
                    }
                    break;
                case RunProgressPhase.RewardPending:
                    ValidateCommittedBattleNode(allowBossAtCurrentNode: false);
                    if (Outcome != null || CurrentHealth <= 0)
                        throw new InvalidOperationException("RewardPending Run facts are inconsistent.");
                    break;
                case RunProgressPhase.Terminal:
                    ValidateTerminalOutcome();
                    break;
                case RunProgressPhase.NodeVisitPending:
                    if (CommittedNodeId != null || Outcome != null || CurrentHealth <= 0)
                        throw new InvalidOperationException("NodeVisitPending Run facts are inconsistent.");
                    ValidatePendingNodeVisit();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ProgressPhase));
            }
        }

        /// <summary>确认唯一路径从 Start 沿冻结普通边前进，且只在末尾保留已抵达 Boss。</summary>
        private void ValidateCompletedPath()
        {
            if (_pathNodeIds.Count == 0 ||
                _pathNodeIds[0] != MapNodeId.FromPosition(layer: 0, slot: 0) ||
                MapDefinition.GetNode(_pathNodeIds[0]).Kind != MapNodeKind.Start)
            {
                throw new InvalidOperationException("Run path must begin at the frozen Start node.");
            }

            var seen = new HashSet<MapNodeId>();
            for (int index = 0; index < _pathNodeIds.Count; index++)
            {
                MapNodeId nodeId = _pathNodeIds[index];
                MapNode node = MapDefinition.GetNode(nodeId);
                if (!seen.Add(nodeId))
                    throw new InvalidOperationException("Run path cannot contain duplicate nodes.");
                if (index == 0)
                    continue;

                bool isFinalBossPathNode = index == _pathNodeIds.Count - 1 &&
                                           node.Kind == MapNodeKind.Boss &&
                                           IsBossPathPhase();
                if (!IsCompletableOrdinaryNodeKind(node.Kind) && !isFinalBossPathNode)
                {
                    throw new InvalidOperationException(
                        "Run path contains a node kind that cannot be completed ordinarily.");
                }

                MapNodeId previousNodeId = _pathNodeIds[index - 1];
                bool hasOrdinaryEdge = MapDefinition.Edges.Any(edge =>
                    edge.FromNodeId == previousNodeId && edge.ToNodeId == nodeId);
                if (!hasOrdinaryEdge)
                    throw new InvalidOperationException("Run path contains a non-ordinary map move.");
            }
        }

        /// <summary>确认已承诺节点是当前位置的普通直接出边 Combat/Elite，或已经抵达的 Boss。</summary>
        private void ValidateCommittedBattleNode(bool allowBossAtCurrentNode)
        {
            if (CommittedNodeId == null)
                throw new InvalidOperationException("The Run phase requires a committed node.");

            MapNode committed = MapDefinition.GetNode(CommittedNodeId.Value);
            bool isBossAtCurrentNode = allowBossAtCurrentNode &&
                                       committed.Kind == MapNodeKind.Boss &&
                                       committed.Id == CurrentNodeId;
            bool hasOrdinaryEdge = MapDefinition.Edges.Any(edge =>
                edge.FromNodeId == CurrentNodeId && edge.ToNodeId == committed.Id);
            bool isRewardBattle = committed.Kind == MapNodeKind.Combat ||
                                  committed.Kind == MapNodeKind.Elite;
            if (!isBossAtCurrentNode && (!isRewardBattle || !hasOrdinaryEdge))
            {
                throw new InvalidOperationException(
                    "Committed node is not a reachable Combat, Elite or arrived Boss.");
            }

            if (ActiveBattle != null &&
                (ActiveBattle.NodeKind != committed.Kind ||
                 ActiveBattle.MapNodeContentId != committed.ContentId ||
                 (committed.Kind != MapNodeKind.Boss &&
                  ActiveBattle.EncounterTemplateId != committed.ContentId)))
            {
                throw new InvalidOperationException(
                    "Active battle projection does not match its committed map node.");
            }
        }

        /// <summary>按 outcome 分类封闭验证终局生命、Battle 身份与节点语义。</summary>
        private void ValidateTerminalOutcome()
        {
            if (Outcome == null || Outcome.RunId != RunId)
                throw new InvalidOperationException("Terminal Run requires its unique outcome.");

            switch (Outcome.Kind)
            {
                case RunOutcomeKind.Victory:
                    ValidateBattleOutcomeShape(requireBoss: true, requireZeroHealth: false);
                    break;
                case RunOutcomeKind.Defeat:
                    ValidateBattleOutcomeShape(requireBoss: false, requireZeroHealth: true);
                    break;
                case RunOutcomeKind.Abandoned:
                    if (CurrentHealth <= 0 ||
                        CommittedNodeId != null ||
                        Outcome.BattleId != null)
                    {
                        throw new InvalidOperationException(
                            "Abandoned outcome must preserve an active stable Run without Battle facts.");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Outcome.Kind));
            }
        }

        /// <summary>验证胜败终局严格绑定最后签发的 Battle，并按需要限定 Boss 与零生命。</summary>
        private void ValidateBattleOutcomeShape(bool requireBoss, bool requireZeroHealth)
        {
            if (CommittedNodeId == null || !Outcome.BattleId.HasValue)
                throw new InvalidOperationException("Battle outcome requires a committed Battle node.");

            RunBattleId battleId = Outcome.BattleId.Value;
            if (battleId.RunId != RunId ||
                battleId.AttemptSequence != BattleAttemptSequence ||
                battleId.NodeId != CommittedNodeId.Value)
            {
                throw new InvalidOperationException(
                    "Battle outcome does not match the last signed Run battle.");
            }

            MapNode committed = MapDefinition.GetNode(CommittedNodeId.Value);
            bool isBoss = committed.Kind == MapNodeKind.Boss && committed.Id == CurrentNodeId;
            bool isRewardBattle = committed.Kind == MapNodeKind.Combat ||
                                  committed.Kind == MapNodeKind.Elite;
            bool hasOrdinaryEdge = MapDefinition.Edges.Any(edge =>
                edge.FromNodeId == CurrentNodeId && edge.ToNodeId == committed.Id);
            if ((requireBoss && !isBoss) ||
                (!requireBoss && !isBoss && (!isRewardBattle || !hasOrdinaryEdge)) ||
                (requireZeroHealth ? CurrentHealth != 0 : CurrentHealth <= 0))
            {
                throw new InvalidOperationException("Terminal battle outcome shape is inconsistent.");
            }
        }

        /// <summary>判断最终 Boss 是否只作为已抵达门、Boss 战或其终局保留在唯一完成路径中。</summary>
        private bool IsBossPathPhase()
        {
            if (ProgressPhase == RunProgressPhase.BossGateReached)
                return true;
            if (ProgressPhase == RunProgressPhase.InBattle)
                return CommittedNodeId == CurrentNodeId;
            if (ProgressPhase != RunProgressPhase.Terminal || Outcome == null)
                return false;

            return Outcome.Kind == RunOutcomeKind.Abandoned ||
                   (Outcome.BattleId.HasValue &&
                    Outcome.BattleId.Value.NodeId == CurrentNodeId);
        }

        /// <summary>确认 Pending 访问严格绑定当前位置的普通直接出边及冻结内容。</summary>
        private void ValidatePendingNodeVisit()
        {
            if (PendingNodeVisit == null)
                throw new InvalidOperationException("The Run phase requires a pending node visit.");

            MapNode node = MapDefinition.GetNode(PendingNodeVisit.NodeId);
            bool hasOrdinaryEdge = MapDefinition.Edges.Any(edge =>
                edge.FromNodeId == CurrentNodeId && edge.ToNodeId == node.Id);
            if (!hasOrdinaryEdge ||
                !IsNonCombatNodeKind(node.Kind) ||
                node.Kind != PendingNodeVisit.Kind ||
                node.ContentId != PendingNodeVisit.ContentId)
            {
                throw new InvalidOperationException(
                    "Pending node visit does not match an ordinary reachable non-combat node.");
            }
        }

        /// <summary>判断节点是否可在成功结算后进入已完成路径。</summary>
        private static bool IsCompletableOrdinaryNodeKind(MapNodeKind kind)
        {
            return kind == MapNodeKind.Combat ||
                   kind == MapNodeKind.Elite ||
                   IsNonCombatNodeKind(kind);
        }

        /// <summary>判断节点是否属于本轮支持的四种非战斗访问。</summary>
        private static bool IsNonCombatNodeKind(MapNodeKind kind)
        {
            return kind == MapNodeKind.Rest ||
                   kind == MapNodeKind.Chest ||
                   kind == MapNodeKind.Shop ||
                   kind == MapNodeKind.Event;
        }
    }
}
