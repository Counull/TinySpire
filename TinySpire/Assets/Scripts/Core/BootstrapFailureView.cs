using System;
using UnityEngine;

/// <summary>
/// Bootstrap 的最小可见失败状态；不负责重试、场景切换或保存任何配置事实。
/// </summary>
public sealed class BootstrapFailureView : MonoBehaviour, IBootstrapFailurePresenter
{
    private const string RestartGuidance =
        "Restore the invalid configuration content, then restart the application.";

    private bool _isVisible;
    private string _message;

    /// <summary>展示已分类配置失败的稳定诊断信息，并阻止本视图继续隐藏错误。</summary>
    public void ShowConfigurationFailure(ConfigInitializationException failure)
    {
        if (failure == null)
            throw new ArgumentNullException(nameof(failure));

        _message =
            $"Startup stopped ({GetFailureCode(failure.Reason)}).\n" +
            $"Resource: {failure.Address}\n" +
            RestartGuidance;
        _isVisible = true;
    }

    /// <summary>仅在启动失败后绘制覆盖层，使尚未初始化的本地化或 UI 资源不成为第二个依赖。</summary>
    private void OnGUI()
    {
        if (!_isVisible)
            return;

        const float panelWidth = 680f;
        const float panelHeight = 180f;
        Rect panel = new Rect(
            Mathf.Max(16f, (Screen.width - panelWidth) * 0.5f),
            Mathf.Max(16f, (Screen.height - panelHeight) * 0.5f),
            Mathf.Min(panelWidth, Screen.width - 32f),
            Mathf.Min(panelHeight, Screen.height - 32f));
        GUI.Box(panel, "TinySpire Bootstrap Failure");
        GUI.Label(new Rect(panel.x + 20f, panel.y + 35f, panel.width - 40f, panel.height - 50f), _message);
    }

    /// <summary>将 typed failure 映射为不随异常文本变化的稳定失败码。</summary>
    private static string GetFailureCode(ConfigInitializationFailureReason reason)
    {
        return reason switch
        {
            ConfigInitializationFailureReason.AssetLoadFailed => "CFG-001",
            ConfigInitializationFailureReason.InvalidJson => "CFG-002",
            ConfigInitializationFailureReason.InvalidGameConfigShape => "CFG-003",
            ConfigInitializationFailureReason.MissingRequiredGameConfigField => "CFG-004",
            ConfigInitializationFailureReason.UnsupportedTableShape => "CFG-005",
            ConfigInitializationFailureReason.InvalidTableRowShape => "CFG-006",
            ConfigInitializationFailureReason.TableConstructionFailed => "CFG-007",
            _ => "CFG-999"
        };
    }
}
