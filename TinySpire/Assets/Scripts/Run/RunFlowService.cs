using System;
using cfg;
using Cysharp.Threading.Tasks;
using TinySpire.Battle;

namespace TinySpire.Run
{
    /// <summary>G1-A 使用的三个稳定 Addressables 场景目标。</summary>
    public static class RunSceneAddresses
    {
        public const string RunEntry = "Assets/Scenes/RunEntryScene.unity";
        public const string Battle = "Assets/Scenes/BattleScene.unity";
    }

    /// <summary>只编排 Run 状态迁移、Battle setup 与场景请求，不复制业务事实。</summary>
    public sealed class RunFlowService : IBattleSetupOptionsSource
    {
        private const int EncounterTemplateId = 5001;

        private readonly RunStateStore _store;
        private readonly Func<Tables> _tablesProvider;
        private readonly ISceneFlowService _scenes;
        private readonly IRunEntropySource _entropy;

        /// <summary>当前是否确有一份可由 Battle child Scope 冻结的 Run attempt。</summary>
        internal bool HasActiveBattleInput
        {
            get
            {
                RunState state = _store.Current;
                return state != null &&
                       state.NodeStatus == RunNodeStatus.InBattle &&
                       state.ActiveBattle != null &&
                       state.BattleSnapshot != null;
            }
        }

        /// <summary>以生产配置服务、场景接口与随机输入源建立跨场景 Run 编排。</summary>
        public RunFlowService(
            RunStateStore store,
            ConfigService configs,
            ISceneFlowService scenes,
            IRunEntropySource entropy)
            : this(store, CreateTablesProvider(configs), scenes, entropy)
        {
        }

        /// <summary>以显式配置表提供器建立可在 EditMode 直接验证的 Run 编排。</summary>
        internal RunFlowService(
            RunStateStore store,
            Func<Tables> tablesProvider,
            ISceneFlowService scenes,
            IRunEntropySource entropy)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _tablesProvider = tablesProvider ?? throw new ArgumentNullException(nameof(tablesProvider));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            _entropy = entropy ?? throw new ArgumentNullException(nameof(entropy));
        }

        /// <summary>从两名冻结候选 Hero 的配置创建唯一新 Run，不触发额外场景切换。</summary>
        public RunState CreateNewRun(int heroTemplateId)
        {
            if (heroTemplateId != 1001 && heroTemplateId != 1002)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));

            Tables tables = RequireTables();
            cfg.battle.Hero hero = tables.TbHero.GetOrDefault(heroTemplateId)
                ?? throw new InvalidOperationException($"Hero template {heroTemplateId} does not exist.");
            if (tables.TbDeck.GetOrDefault(hero.InitialDeckId) == null)
                throw new InvalidOperationException($"Deck template {hero.InitialDeckId} does not exist.");
            if (tables.TbEncounter.GetOrDefault(EncounterTemplateId) == null)
                throw new InvalidOperationException($"Encounter template {EncounterTemplateId} does not exist.");

            RunEntropy entropy = _entropy.Next();
            return _store.CreateNewRun(new RunCreationOptions(
                entropy.RunId,
                hero.Id,
                hero.MaxHealth,
                hero.MaxHealth,
                hero.InitialDeckId,
                EncounterTemplateId,
                entropy.RandomRootSeed));
        }

        /// <summary>冻结进战 snapshot 与本战输入后，请求进入既有 BattleScene。</summary>
        public async UniTask<RunBattleInput> EnterBattleNodeAsync()
        {
            RunBattleInput input = _store.BeginBattle();
            await _scenes.LoadSceneWithLoadingAsync(RunSceneAddresses.Battle);
            return input;
        }

        /// <summary>从失败 snapshot 恢复并签发新 attempt 后，再次请求进入 BattleScene。</summary>
        public async UniTask<RunBattleInput> RestartFailedBattleAsync()
        {
            RunBattleInput input = _store.RestartBattle();
            await _scenes.LoadSceneWithLoadingAsync(RunSceneAddresses.Battle);
            return input;
        }

        /// <summary>把当前有效本战输入映射为 Battle child Scope 唯一读取的装配参数。</summary>
        public BattleSetupOptions CreateBattleSetupOptions()
        {
            RunBattleInput input = RequireActiveBattle();
            return new BattleSetupOptions(
                input.HeroTemplateId,
                input.EncounterTemplateId,
                checked((int)input.RandomSeed),
                input.InitialHealth,
                input.DeckTemplateId);
        }

        /// <summary>确认 BattleScope 冻结参数仍精确对应当前 attempt，并返回关联身份。</summary>
        internal RunBattleId BindBattleAttempt(BattleSetupOptions setup)
        {
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            RunBattleInput input = RequireActiveBattle();
            if (setup.HeroTemplateId != input.HeroTemplateId ||
                setup.EncounterTemplateId != input.EncounterTemplateId ||
                setup.RandomSeed != input.RandomSeed ||
                setup.PlayerInitialHealth != input.InitialHealth ||
                setup.DeckTemplateId != input.DeckTemplateId)
            {
                throw new InvalidOperationException("Battle setup does not match the active Run attempt.");
            }

            return input.BattleId;
        }

        /// <summary>消费当前 attempt 的单玩家稳定结果，先写回 Run 再返回入口场景。</summary>
        internal async UniTask HandleBattleResultAsync(
            RunBattleId battleId,
            BattleResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.Players.Count != 1)
                throw new InvalidOperationException("G1-A requires exactly one player BattleResult snapshot.");

            BattleResultPlayerSnapshot player = result.Players[0];
            switch (result.Kind)
            {
                case BattleResultKind.Victory:
                    _store.ApplyVictory(
                        battleId,
                        player.TemplateId,
                        player.Health,
                        player.MaxHealth);
                    break;
                case BattleResultKind.Defeat:
                    _store.RecordDefeat(
                        battleId,
                        player.TemplateId,
                        player.Health,
                        player.MaxHealth);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result));
            }

            await _scenes.LoadSceneWithLoadingAsync(RunSceneAddresses.RunEntry);
        }

        /// <summary>从已初始化配置服务创建延迟读取表格的生产提供器。</summary>
        private static Func<Tables> CreateTablesProvider(ConfigService configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            return () => configs.Tables;
        }

        /// <summary>读取已初始化的 Luban 表，并拒绝在启动配置完成前创建 Run。</summary>
        private Tables RequireTables()
        {
            return _tablesProvider()
                ?? throw new InvalidOperationException("ConfigService must be initialized before creating a run.");
        }

        /// <summary>读取唯一处于 InBattle 的本战输入。</summary>
        private RunBattleInput RequireActiveBattle()
        {
            RunState state = _store.Current;
            if (state == null ||
                state.NodeStatus != RunNodeStatus.InBattle ||
                state.ActiveBattle == null)
            {
                throw new InvalidOperationException("No active Run battle input exists.");
            }

            return state.ActiveBattle;
        }
    }
}
