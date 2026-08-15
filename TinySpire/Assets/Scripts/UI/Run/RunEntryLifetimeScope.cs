using TinySpire.UI.Run;
using VContainer;
using VContainer.Unity;

/// <summary>只持有 RunEntryScene 的 View 与 Presenter，跨场景事实继续来自 Bootstrap 父 Scope。</summary>
public class RunEntryLifetimeScope : LifetimeScope
{
    /// <summary>注册场景唯一 View 接口，并让 Presenter 随场景 Scope 启停订阅。</summary>
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<RunEntryView>()
            .As<IRunEntryView>();
        builder.RegisterEntryPoint<RunEntryPresenter>()
            .AsSelf();
    }
}
