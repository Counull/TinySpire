using System;
using System.Collections.Generic;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class BattleLifetimeScope : LifetimeScope
{
    [SerializeField] private int heroTemplateId = 1001;
    [SerializeField] private int encounterTemplateId = 5001;
    // TODO(DEP-007): Replace the Inspector seed with a RunState-derived battle seed once run lifecycle exists.
    [SerializeField, Min(1)] private int battleSeed = 1;

    /// <summary>注册本场战斗的运行时事实、权威命令队列、逐帧入口与现有只读视图。</summary>
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(new BattleSetupOptions(heroTemplateId, encounterTemplateId, battleSeed));
        builder.Register(
            resolver => new BattleSession(
                resolver.Resolve<ConfigService>(),
                resolver.Resolve<BattleSetupOptions>()),
            Lifetime.Singleton);
        builder.Register<CardTextFormatter>(Lifetime.Singleton);
        builder.RegisterComponentInHierarchy<HandCardContainer>();
        builder.RegisterComponentInHierarchy<BattleParticipantPresenter>();
        builder.RegisterComponentInHierarchy<BattleCardPileHudView>();
        builder.RegisterComponentInHierarchy<BattleTurnHudView>();
        builder.RegisterEntryPoint<BattleCommandPresentationAdapter>()
            .AsSelf();
        builder.Register(
            resolver => CreateBattleCommandQueue(
                resolver.Resolve<BattleSession>(),
                resolver.Resolve<ConfigService>(),
                resolver.Resolve<IBattleCommandPresentation>()),
            Lifetime.Singleton);
        builder.RegisterEntryPoint<BattleCommandRuntimeDriver>();
    }

    /// <summary>从当前单玩家生产 Session 组装多人根兼容的权威命令队列。</summary>
    private static BattleCommandQueue CreateBattleCommandQueue(
        BattleSession session,
        ConfigService configs,
        IBattleCommandPresentation presentation)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (configs == null)
            throw new ArgumentNullException(nameof(configs));
        if (configs.Tables == null || configs.GameConfig == null)
            throw new InvalidOperationException(
                "ConfigService must be initialized before creating the battle command queue.");

        IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones =
            CreateCurrentPlayerCardZones(session);
        return new BattleCommandQueue(
            session.Combatants,
            playerCardZones,
            session.EnemyCombatantIdsInEncounterOrder,
            configs.Tables,
            configs.GameConfig.EnergyPerRound,
            configs.GameConfig.InitialHandCount,
            presentation);
    }

    /// <summary>把当前唯一玩家映射到 Session 卡区，并在生产接线超出 DEP-008 边界时立即失败。</summary>
    private static IReadOnlyDictionary<CombatantId, BattleCardZonesData> CreateCurrentPlayerCardZones(
        BattleSession session)
    {
        var playerCardZones = new Dictionary<CombatantId, BattleCardZonesData>();
        foreach (CombatantData combatant in session.Combatants.All.Values)
        {
            if (!(combatant is PlayerCombatantData player))
                continue;
            if (playerCardZones.Count > 0)
            {
                throw new InvalidOperationException(
                    "The current production BattleSession supports exactly one player card zone (DEP-008).");
            }

            playerCardZones.Add(player.Id, session.CardZones);
        }

        if (playerCardZones.Count == 0)
            throw new InvalidOperationException("BattleSession must contain one player combatant.");

        return playerCardZones;
    }
}

namespace TinySpire.Battle
{
    /// <summary>
    /// 生产生命周期入口：启动战斗，并让当前无行为敌人在后续帧通过同一队列依次完成。
    /// </summary>
    public sealed class BattleCommandRuntimeDriver : IStartable, ITickable
    {
        private readonly BattleCommandQueue _queue;

        /// <summary>保存生产命令队列，所有启动与敌人完成意图都只通过该 seam 提交。</summary>
        public BattleCommandRuntimeDriver(BattleCommandQueue queue)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        /// <summary>在 BattleLifetimeScope 启动时提交唯一的开始战斗命令。</summary>
        public void Start()
        {
            BattleCommandSubmissionResult result = _queue.Submit(new StartBattleCommand());
            if (!result.Accepted)
                throw new InvalidOperationException("Battle command queue rejected StartBattleCommand.");
        }

        /// <summary>队列空闲且正在等待敌人时，每帧最多提交一名当前敌人的完成命令。</summary>
        public void Tick()
        {
            BattleCommandQueueData queue = _queue.Queue.CurrentValue;
            if (queue.CurrentAuthoritySequence.HasValue ||
                queue.PendingCount > 0 ||
                queue.IsWaitingForPresentation)
            {
                return;
            }

            BattleTurnData turn = _queue.Turn.CurrentValue;
            if (turn.Phase != BattleTurnPhase.EnemyAction ||
                !turn.CurrentActingEnemyId.HasValue)
            {
                return;
            }

            BattleCommandSubmissionResult result = _queue.Submit(
                new CompleteEnemyActionCommand(turn.CurrentActingEnemyId.Value));
            if (!result.Accepted)
                throw new InvalidOperationException("Battle command queue rejected CompleteEnemyActionCommand.");
        }
    }
}
