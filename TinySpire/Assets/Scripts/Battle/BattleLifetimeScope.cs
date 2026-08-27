using System;
using System.Collections.Generic;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.UI.Battle;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class BattleLifetimeScope : LifetimeScope
{
    [SerializeField] private int heroTemplateId = 1001;
    [SerializeField] private int encounterTemplateId = 5001;
    // 保留 Inspector 值作为没有 active Run 时的 legacy/debug Battle fallback。
    [SerializeField, Min(1)] private int battleSeed = 1;

    /// <summary>注册本场战斗的运行时事实、权威命令队列、逐帧入口与现有只读视图。</summary>
    protected override void Configure(IContainerBuilder builder)
    {
        RegisterBattleSetupOptions(builder, heroTemplateId, encounterTemplateId, battleSeed);
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
        builder.Register<BattleCommandSubmissionCoordinator>(Lifetime.Singleton);
        builder.Register(
            resolver => CreateBattleCommandQueue(
                resolver.Resolve<BattleSession>(),
                resolver.Resolve<ConfigService>(),
                resolver.Resolve<IBattleCommandPresentation>(),
                resolver.Resolve<BattleCommandSubmissionCoordinator>()),
            Lifetime.Singleton);
        builder.RegisterEntryPoint<BattleResultRunBridge>();
        builder.RegisterEntryPoint<BattleCommandRuntimeDriver>();
    }

    /// <summary>优先冻结父 Scope 输入来源，并在缺少来源时使用 Inspector 默认值。</summary>
    internal static void RegisterBattleSetupOptions(
        IContainerBuilder builder,
        int defaultHeroTemplateId,
        int defaultEncounterTemplateId,
        int defaultBattleSeed)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        builder.Register(
            resolver =>
            {
                if (resolver.TryResolve(
                        out IBattleSetupOptionsSource source))
                {
                    if (source is RunFlowService runFlow && !runFlow.HasActiveBattleInput)
                    {
                        return new BattleSetupOptions(
                            defaultHeroTemplateId,
                            defaultEncounterTemplateId,
                            defaultBattleSeed);
                    }

                    BattleSetupOptions injected = source.CreateBattleSetupOptions();
                    if (injected == null)
                    {
                        throw new InvalidOperationException(
                            "IBattleSetupOptionsSource must return one immutable BattleSetupOptions instance.");
                    }

                    return injected;
                }

                return new BattleSetupOptions(
                    defaultHeroTemplateId,
                    defaultEncounterTemplateId,
                    defaultBattleSeed);
            },
            Lifetime.Singleton);
    }

    /// <summary>从当前单玩家生产 Session 组装多人根兼容的权威命令队列。</summary>
    private static BattleCommandQueue CreateBattleCommandQueue(
        BattleSession session,
        ConfigService configs,
        IBattleCommandPresentation presentation,
        BattleCommandSubmissionCoordinator coordinator)
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
            session.EnemyIntents,
            configs.Tables,
            session.PlayerResourceProfiles,
            configs.GameConfig.InitialHandCount,
            presentation,
            coordinator,
            session.MachineGunnerRuntime,
            session.CardTargetRandomSeed,
            session.BattleStartRelicEffects,
            session.PotionLedger);
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
    /// <summary>生产生命周期入口：只提交启动命令，敌人推进完全由 Queue continuation 驱动。</summary>
    public sealed class BattleCommandRuntimeDriver : IStartable
    {
        private readonly BattleCommandQueue _queue;

        /// <summary>保存生产 Queue，仅负责场景启动命令。</summary>
        public BattleCommandRuntimeDriver(BattleCommandQueue queue)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        /// <summary>在 BattleLifetimeScope 启动时提交唯一的开始战斗命令。</summary>
        public void Start()
        {
            var command = new StartBattleCommand();
            BattleCommandSubmissionResult result = _queue.Submit(command);
            if (!result.Accepted)
                throw new InvalidOperationException("Battle command queue rejected StartBattleCommand.");
        }
    }
}
