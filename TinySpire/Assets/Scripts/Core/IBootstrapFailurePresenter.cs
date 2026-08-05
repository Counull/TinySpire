/// <summary>
/// 向 Bootstrap 场景呈现可诊断的启动失败；仅接收已分类的配置失败。
/// </summary>
public interface IBootstrapFailurePresenter
{
    /// <summary>展示稳定失败码、失败资源地址和恢复后重启指引。</summary>
    void ShowConfigurationFailure(ConfigInitializationException failure);
}
