using System;

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

    /// <summary>G1-A 唯一战斗节点的权威状态。</summary>
    public enum RunNodeStatus
    {
        Available,
        InBattle,
        Failed,
        Completed,
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

        /// <summary>本 Run 起始牌组模板标识。</summary>
        public int DeckTemplateId { get; }

        /// <summary>唯一临时节点对应的遭遇模板标识。</summary>
        public int EncounterTemplateId { get; }

        /// <summary>后续本战随机输入的非零根种子。</summary>
        public uint RandomRootSeed { get; }

        /// <summary>冻结并验证新 Run 的全部创建输入。</summary>
        public RunCreationOptions(
            RunId runId,
            int heroTemplateId,
            int initialHealth,
            int maxHealth,
            int deckTemplateId,
            int encounterTemplateId,
            uint randomRootSeed)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run id cannot be empty.", nameof(runId));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (initialHealth <= 0 || initialHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(initialHealth));
            if (deckTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(deckTemplateId));
            if (encounterTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(encounterTemplateId));
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));

            RunId = runId;
            HeroTemplateId = heroTemplateId;
            InitialHealth = initialHealth;
            MaxHealth = maxHealth;
            DeckTemplateId = deckTemplateId;
            EncounterTemplateId = encounterTemplateId;
            RandomRootSeed = randomRootSeed;
        }
    }

    /// <summary>从已验证存档恢复一份地图稳定 Run 所需的全部领域事实。</summary>
    public sealed class RunRestoreOptions
    {
        public RunId RunId { get; }
        public int HeroTemplateId { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
        public int DeckTemplateId { get; }
        public int EncounterTemplateId { get; }
        public uint RandomRootSeed { get; }
        public RunNodeStatus NodeStatus { get; }
        public int BattleAttemptSequence { get; }

        /// <summary>冻结并验证一份不含 Battle transient 的稳定恢复输入。</summary>
        public RunRestoreOptions(
            RunId runId,
            int heroTemplateId,
            int currentHealth,
            int maxHealth,
            int deckTemplateId,
            int encounterTemplateId,
            uint randomRootSeed,
            RunNodeStatus nodeStatus,
            int battleAttemptSequence)
        {
            if (runId.Value == Guid.Empty)
                throw new ArgumentException("Run id cannot be empty.", nameof(runId));
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (currentHealth <= 0 || currentHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            if (deckTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(deckTemplateId));
            if (encounterTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(encounterTemplateId));
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (nodeStatus != RunNodeStatus.Available && nodeStatus != RunNodeStatus.Completed)
                throw new ArgumentOutOfRangeException(nameof(nodeStatus));
            if (battleAttemptSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(battleAttemptSequence));

            RunId = runId;
            HeroTemplateId = heroTemplateId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            DeckTemplateId = deckTemplateId;
            EncounterTemplateId = encounterTemplateId;
            RandomRootSeed = randomRootSeed;
            NodeStatus = nodeStatus;
            BattleAttemptSequence = battleAttemptSequence;
        }
    }

    /// <summary>一局 Run 内某次战斗尝试的稳定关联标识。</summary>
    public readonly struct RunBattleId : IEquatable<RunBattleId>
    {
        /// <summary>所属 Run 标识。</summary>
        public RunId RunId { get; }

        /// <summary>所属 Run 内从一开始递增的尝试序号。</summary>
        public int AttemptSequence { get; }

        /// <summary>组合 Run 身份与正数尝试序号。</summary>
        public RunBattleId(RunId runId, int attemptSequence)
        {
            if (attemptSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(attemptSequence));

            RunId = runId;
            AttemptSequence = attemptSequence;
        }

        /// <summary>比较两个本战标识是否相同。</summary>
        public bool Equals(RunBattleId other)
        {
            return RunId == other.RunId && AttemptSequence == other.AttemptSequence;
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
                return (RunId.GetHashCode() * 397) ^ AttemptSequence;
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

        /// <summary>本战起始牌组模板标识。</summary>
        public int DeckTemplateId { get; }

        /// <summary>本战遭遇模板标识。</summary>
        public int EncounterTemplateId { get; }

        /// <summary>本战全部规则随机域的非零输入种子。</summary>
        public uint RandomSeed { get; }

        /// <summary>冻结一次由 Run 唯一签发的 Battle setup 输入。</summary>
        internal RunBattleInput(RunState state, int attemptSequence, uint randomSeed)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (attemptSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(attemptSequence));
            if (randomSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomSeed));

            BattleId = new RunBattleId(state.RunId, attemptSequence);
            HeroTemplateId = state.HeroTemplateId;
            InitialHealth = state.CurrentHealth;
            MaxHealth = state.MaxHealth;
            DeckTemplateId = state.DeckTemplateId;
            EncounterTemplateId = state.EncounterTemplateId;
            RandomSeed = randomSeed;
        }
    }

    /// <summary>进入战斗前冻结、供失败恢复使用的 Run 事实。</summary>
    public sealed class RunBattleSnapshot
    {
        /// <summary>所属 Run 标识。</summary>
        public RunId RunId { get; }

        /// <summary>快照中的英雄模板标识。</summary>
        public int HeroTemplateId { get; }

        /// <summary>快照中的当前生命。</summary>
        public int CurrentHealth { get; }

        /// <summary>快照中的生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>快照中的牌组模板标识。</summary>
        public int DeckTemplateId { get; }

        /// <summary>快照中的遭遇模板标识。</summary>
        public int EncounterTemplateId { get; }

        /// <summary>快照中的节点状态。</summary>
        public RunNodeStatus NodeStatus { get; }

        /// <summary>从可进入节点的当前 Run 事实创建恢复快照。</summary>
        internal RunBattleSnapshot(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            RunId = state.RunId;
            HeroTemplateId = state.HeroTemplateId;
            CurrentHealth = state.CurrentHealth;
            MaxHealth = state.MaxHealth;
            DeckTemplateId = state.DeckTemplateId;
            EncounterTemplateId = state.EncounterTemplateId;
            NodeStatus = state.NodeStatus;
        }
    }

    /// <summary>跨场景存活的一局 Run 不可变业务事实。</summary>
    public sealed class RunState
    {
        /// <summary>本 Run 的稳定身份。</summary>
        public RunId RunId { get; }

        /// <summary>本 Run 唯一英雄模板标识。</summary>
        public int HeroTemplateId { get; }

        /// <summary>当前跨战斗生命。</summary>
        public int CurrentHealth { get; }

        /// <summary>本 Run 生命上限。</summary>
        public int MaxHealth { get; }

        /// <summary>本 Run 起始牌组模板标识。</summary>
        public int DeckTemplateId { get; }

        /// <summary>唯一临时节点对应的遭遇模板标识。</summary>
        public int EncounterTemplateId { get; }

        /// <summary>派生本战随机输入的根种子。</summary>
        public uint RandomRootSeed { get; }

        /// <summary>唯一临时节点的当前权威状态。</summary>
        public RunNodeStatus NodeStatus { get; }

        /// <summary>已经签发过的本战随机输入序号。</summary>
        public int BattleAttemptSequence { get; }

        /// <summary>当前有效的本战输入；尚未入战时为空。</summary>
        public RunBattleInput ActiveBattle { get; }

        /// <summary>当前有效的进战前快照；尚未入战时为空。</summary>
        public RunBattleSnapshot BattleSnapshot { get; }

        /// <summary>从已验证创建参数建立初始 Run 事实。</summary>
        internal RunState(RunCreationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            RunId = options.RunId;
            HeroTemplateId = options.HeroTemplateId;
            CurrentHealth = options.InitialHealth;
            MaxHealth = options.MaxHealth;
            DeckTemplateId = options.DeckTemplateId;
            EncounterTemplateId = options.EncounterTemplateId;
            RandomRootSeed = options.RandomRootSeed;
            NodeStatus = RunNodeStatus.Available;
            BattleAttemptSequence = 0;
            ActiveBattle = null;
            BattleSnapshot = null;
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
            DeckTemplateId = options.DeckTemplateId;
            EncounterTemplateId = options.EncounterTemplateId;
            RandomRootSeed = options.RandomRootSeed;
            NodeStatus = options.NodeStatus;
            BattleAttemptSequence = options.BattleAttemptSequence;
            ActiveBattle = null;
            BattleSnapshot = null;
        }

        /// <summary>复制不变 Run 事实，并原子替换当前节点、attempt 与本战输入。</summary>
        internal RunState(
            RunState previous,
            int currentHealth,
            RunNodeStatus nodeStatus,
            int battleAttemptSequence,
            RunBattleInput activeBattle,
            RunBattleSnapshot battleSnapshot)
        {
            if (previous == null)
                throw new ArgumentNullException(nameof(previous));
            if (currentHealth < 0 || currentHealth > previous.MaxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            if (battleAttemptSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(battleAttemptSequence));

            RunId = previous.RunId;
            HeroTemplateId = previous.HeroTemplateId;
            CurrentHealth = currentHealth;
            MaxHealth = previous.MaxHealth;
            DeckTemplateId = previous.DeckTemplateId;
            EncounterTemplateId = previous.EncounterTemplateId;
            RandomRootSeed = previous.RandomRootSeed;
            NodeStatus = nodeStatus;
            BattleAttemptSequence = battleAttemptSequence;
            ActiveBattle = activeBattle;
            BattleSnapshot = battleSnapshot;
        }
    }
}
