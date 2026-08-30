using TinySpire.Run.History.Presentation;
using TinySpire.Presentation.Audio;
using TinySpire.Settings.Presentation;
using TinySpire.UI.Run;
using VContainer;
using VContainer.Unity;

/// <summary>只持有 RunEntryScene 的 View 与 Presenter，跨场景事实继续来自 Bootstrap 父 Scope。</summary>
public class RunEntryLifetimeScope : LifetimeScope
{
    /// <summary>把场景唯一 View 暴露给三个独立 seam，并让 Presenter 随场景 Scope 启停订阅。</summary>
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<RunEntryView>()
            .As<IRunEntryView>()
            .As<IAppSettingsView>()
            .As<IRunStatisticsView>();
        builder.Register<RunMapIdentityCatalog>(Lifetime.Scoped)
            .As<IRunMapIdentityCatalog>();
        builder.RegisterEntryPoint<AppSettingsPresenter>()
            .AsSelf();
        builder.RegisterEntryPoint<RunEntryPresenter>()
            .AsSelf();
        builder.RegisterEntryPoint<RunStatisticsPresenter>()
            .AsSelf();
        builder.RegisterEntryPoint<RunEntryUiAudioPresenter>();
    }
}
