using TinySpire.Battle;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class BattleLifetimeScope : LifetimeScope
{
    [SerializeField] private int heroTemplateId = 1001;
    [SerializeField] private int encounterTemplateId = 5001;
    // TODO(DEP-007): Replace the Inspector seed with a RunState-derived battle seed once run lifecycle exists.
    [SerializeField, Min(1)] private int battleSeed = 1;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(new BattleSetupOptions(heroTemplateId, encounterTemplateId, battleSeed));
        builder.Register<BattleSession>(Lifetime.Singleton);
        builder.RegisterComponentInHierarchy<HandCardContainer>();

        // TODO(DEP-005): 回合调度器与其余战斗局内模块确定后在此注册
    }
}
