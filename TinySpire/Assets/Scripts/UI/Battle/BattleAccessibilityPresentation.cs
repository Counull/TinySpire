using System;
using System.Collections.Generic;
using TMPro;
using TinySpire.Settings;
using UnityEngine;
using VContainer.Unity;

namespace TinySpire.UI.Battle
{
    /// <summary>Battle 必经页面一次应用使用的完整不可变可访问性投影。</summary>
    public sealed class BattleAccessibilityViewModel
    {
        public const float ReducedMotionSpeedMultiplier = 4f;

        /// <summary>相对原设计字号的倍率。</summary>
        public float TextScaleMultiplier { get; }

        /// <summary>是否启用高对比文字轮廓。</summary>
        public bool HighContrast { get; }

        /// <summary>表现 runner 的时间倍率；只缩短表现，不跳过领域结算。</summary>
        public float MotionSpeedMultiplier { get; }

        /// <summary>从唯一应用设置快照冻结 Battle 投影。</summary>
        public BattleAccessibilityViewModel(AppSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            TextScaleMultiplier = (int)settings.TextScale / 100f;
            HighContrast = settings.HighContrast;
            MotionSpeedMultiplier = settings.ReducedMotion
                ? ReducedMotionSpeedMultiplier
                : 1f;
        }
    }

    /// <summary>Presenter 与真实 Battle 场景表现之间唯一可访问性端口。</summary>
    public interface IBattleAccessibilityView
    {
        /// <summary>以完整投影替换当前文字与表现速度。</summary>
        void Apply(BattleAccessibilityViewModel model);
    }

    /// <summary>把父 Scope 的唯一 AppSettings 快照投影到当前 Battle 场景。</summary>
    public sealed class BattleAccessibilityPresenter : IInitializable, IDisposable
    {
        private readonly IBattleAccessibilityView _view;
        private readonly AppSettingsService _settings;
        private bool _initialized;
        private bool _disposed;

        /// <summary>保存场景 View 与唯一设置 owner。</summary>
        public BattleAccessibilityPresenter(
            IBattleAccessibilityView view,
            AppSettingsService settings)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>进入 Battle 时应用当前快照，并订阅后续耐久设置发布。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;
            if (_settings.Current == null)
                throw new InvalidOperationException("App settings must be initialized before Battle.");

            _initialized = true;
            _settings.Changed += HandleSettingsChanged;
            Apply(_settings.Current);
        }

        /// <summary>离开 Battle 后停止响应父级设置变化。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_initialized)
                _settings.Changed -= HandleSettingsChanged;
        }

        /// <summary>成功发布新设置时重新应用完整投影。</summary>
        private void HandleSettingsChanged(AppSettingsSnapshot settings)
        {
            if (!_disposed)
                Apply(settings);
        }

        /// <summary>把完整设置转换为不持有第二份业务事实的 ViewModel。</summary>
        private void Apply(AppSettingsSnapshot settings)
        {
            _view.Apply(new BattleAccessibilityViewModel(settings));
        }

        /// <summary>拒绝释放后的重复初始化。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BattleAccessibilityPresenter));
        }
    }

    /// <summary>运行时扫描当前 Battle 场景文字并控制现有命令表现 runner。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleAccessibilitySceneView : MonoBehaviour, IBattleAccessibilityView
    {
        private const int DynamicTextScanIntervalFrames = 30;
        private static readonly Color32 HighContrastTextColor =
            new Color32(255, 255, 255, 255);
        private static readonly Color32 HighContrastOutlineColor =
            new Color32(0, 0, 0, 255);

        private readonly Dictionary<EntityId, TextDefaults> _textDefaults =
            new Dictionary<EntityId, TextDefaults>();

        private BattleCommandPresentationAdapter _presentation;
        private BattleAccessibilityViewModel _current;
        private int _framesUntilScan;

        /// <summary>由当前 Battle Scope 注入唯一命令表现 adapter。</summary>
        [VContainer.Inject]
        public void Construct(BattleCommandPresentationAdapter presentation)
        {
            _presentation = presentation
                ?? throw new ArgumentNullException(nameof(presentation));
        }

        /// <summary>立即应用表现速度，并刷新当前场景内全部静态与动态 TMP 文字。</summary>
        public void Apply(BattleAccessibilityViewModel model)
        {
            _current = model ?? throw new ArgumentNullException(nameof(model));
            if (_presentation == null)
                throw new InvalidOperationException("Battle presentation must be injected first.");

            _presentation.SetPresentationSpeed(model.MotionSpeedMultiplier);
            ApplyToSceneTexts();
            _framesUntilScan = DynamicTextScanIntervalFrames;
        }

        /// <summary>低频发现战斗中动态创建的卡牌文字并应用同一快照。</summary>
        private void LateUpdate()
        {
            if (_current == null)
                return;

            _framesUntilScan--;
            if (_framesUntilScan > 0)
                return;

            ApplyToSceneTexts();
            _framesUntilScan = DynamicTextScanIntervalFrames;
        }

        /// <summary>只处理当前 Battle Scene 的 TMP，不污染跨场景 Bootstrap 教程层。</summary>
        private void ApplyToSceneTexts()
        {
            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include);
            for (int index = 0; index < texts.Length; index++)
            {
                TMP_Text text = texts[index];
                if (text == null || text.gameObject.scene != gameObject.scene)
                    continue;

                EntityId entityId = text.GetEntityId();
                if (!_textDefaults.TryGetValue(entityId, out TextDefaults defaults))
                {
                    defaults = new TextDefaults(text);
                    _textDefaults.Add(entityId, defaults);
                }

                ApplyToText(text, defaults, _current);
            }
        }

        /// <summary>从缓存基线幂等计算字号与轮廓，避免重复应用时累计缩放。</summary>
        private static void ApplyToText(
            TMP_Text text,
            TextDefaults defaults,
            BattleAccessibilityViewModel model)
        {
            text.fontSize = defaults.FontSize * model.TextScaleMultiplier;
            if (defaults.EnableAutoSizing)
            {
                text.fontSizeMin = defaults.FontSizeMin * model.TextScaleMultiplier;
                text.fontSizeMax = defaults.FontSizeMax * model.TextScaleMultiplier;
            }

            text.color = model.HighContrast
                ? HighContrastTextColor
                : defaults.Color;
            text.outlineColor = model.HighContrast
                ? HighContrastOutlineColor
                : defaults.OutlineColor;
            text.outlineWidth = model.HighContrast
                ? Mathf.Max(0.18f, defaults.OutlineWidth)
                : defaults.OutlineWidth;
        }

        /// <summary>冻结一项 TMP 的设计基线，供任意次数设置切换恢复。</summary>
        private readonly struct TextDefaults
        {
            public float FontSize { get; }
            public bool EnableAutoSizing { get; }
            public float FontSizeMin { get; }
            public float FontSizeMax { get; }
            public Color32 Color { get; }
            public Color32 OutlineColor { get; }
            public float OutlineWidth { get; }

            /// <summary>从首次发现的真实 TMP 冻结全部会被修改的表现字段。</summary>
            public TextDefaults(TMP_Text text)
            {
                FontSize = text.fontSize;
                EnableAutoSizing = text.enableAutoSizing;
                FontSizeMin = text.fontSizeMin;
                FontSizeMax = text.fontSizeMax;
                Color = text.color;
                OutlineColor = text.outlineColor;
                OutlineWidth = text.outlineWidth;
            }
        }
    }
}
