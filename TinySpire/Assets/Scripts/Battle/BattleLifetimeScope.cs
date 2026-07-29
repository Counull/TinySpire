using VContainer;
using VContainer.Unity;

public sealed class BattleLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // TODO(DEP-005): 回合调度器/抽牌堆/弃牌堆等战斗局内服务确定后在此注册
    }
}
