using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Unity Localization 的轻量运行时适配器。
/// SelectedLocale 仍是语言设置的唯一事实；本服务只转发变化。
/// </summary>
public sealed class LocalizationService : IDisposable
{
    /// <summary>战斗卡牌文本所在的 String Table 名称。</summary>
    public const string BattleCardTableName = "Battle Cards";

    private bool _initialized;
    private bool _subscribed;

    private readonly Subject<Locale> _localeChanged = new Subject<Locale>();

    /// <summary>Unity 当前语言变更时发布具体的 Locale，不维护第二份语言状态。</summary>
    public Observable<Locale> LocaleChanged => _localeChanged;

    /// <summary>当前 Unity 选中语言的代码；未初始化时可能为 null。</summary>
    public string CurrentLocaleCode => LocalizationSettings.SelectedLocale?.Identifier.Code;

    /// <summary>
    /// 等待 Unity Localization 初始化完成，并开始转发语言变化。
    /// </summary>
    public async UniTask InitializeAsync()
    {
        if (_initialized)
            return;

        AsyncOperationHandle<LocalizationSettings> handle = LocalizationSettings.InitializationOperation;
        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            throw new InvalidOperationException(
                "Unable to initialize Unity Localization.",
                handle.OperationException);

        if (!_subscribed)
        {
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
            _subscribed = true;
        }

        _initialized = true;
    }

    /// <summary>
    /// 按语言代码切换 Unity 当前语言；代码未配置时返回 false。
    /// </summary>
    public bool SetLocale(string localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode))
            throw new ArgumentException("Locale code cannot be empty.", nameof(localeCode));
        if (!_initialized)
            throw new InvalidOperationException("LocalizationService must be initialized before changing locale.");

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(localeCode));
        if (locale == null)
            return false;

        LocalizationSettings.SelectedLocale = locale;
        return true;
    }

    /// <summary>
    /// 从战斗卡牌 String Table 读取文本，并可按键名代入动态参数。
    /// </summary>
    public string GetString(string key, IReadOnlyDictionary<string, object> arguments = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Localization key cannot be empty.", nameof(key));
        if (!_initialized)
            throw new InvalidOperationException("LocalizationService must be initialized before reading strings.");

        string value;
        if (arguments == null || arguments.Count == 0)
        {
            value = LocalizationSettings.StringDatabase.GetLocalizedString(BattleCardTableName, key);
        }
        else
        {
            var copiedArguments = new Dictionary<string, object>(arguments.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> argument in arguments)
                copiedArguments.Add(argument.Key, argument.Value);

            value = LocalizationSettings.StringDatabase.GetLocalizedString(
                BattleCardTableName,
                key,
                new object[] { copiedArguments });
        }

        if (!string.IsNullOrEmpty(value))
            return value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError($"Missing localized string '{key}' in table '{BattleCardTableName}'.");
        return $"[missing:{key}]";
#else
        return key;
#endif
    }

    /// <summary>
    /// 取消 Unity 回调并释放语言变更流。
    /// </summary>
    public void Dispose()
    {
        if (_subscribed)
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            _subscribed = false;
        }

        _localeChanged.Dispose();
    }

    /// <summary>将 Unity 的语言变化原样转发到观察者。</summary>
    private void HandleSelectedLocaleChanged(Locale locale)
    {
        _localeChanged.OnNext(locale);
    }
}
