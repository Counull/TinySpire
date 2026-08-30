using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TinySpire.Settings
{
    /// <summary>首发 Windows Player 支持的两种显示模式。</summary>
    public enum AppDisplayMode
    {
        Windowed,
        BorderlessFullscreen,
    }

    /// <summary>应用 UI 支持的离散文字缩放档位。</summary>
    public enum AppTextScale
    {
        Percent100 = 100,
        Percent125 = 125,
    }

    /// <summary>一组稳定、可比较的显示分辨率。</summary>
    public readonly struct AppResolution : IEquatable<AppResolution>
    {
        /// <summary>水平像素。</summary>
        public int Width { get; }

        /// <summary>垂直像素。</summary>
        public int Height { get; }

        /// <summary>以正像素尺寸创建分辨率。</summary>
        public AppResolution(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
        }

        /// <summary>逐像素比较两组分辨率。</summary>
        public bool Equals(AppResolution other)
        {
            return Width == other.Width && Height == other.Height;
        }

        /// <summary>比较对象是否为相同分辨率。</summary>
        public override bool Equals(object obj)
        {
            return obj is AppResolution other && Equals(other);
        }

        /// <summary>返回由宽高共同组成的稳定哈希。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        /// <summary>返回便于日志读取的像素尺寸。</summary>
        public override string ToString()
        {
            return $"{Width}x{Height}";
        }

        /// <summary>判断两组分辨率相同。</summary>
        public static bool operator ==(AppResolution left, AppResolution right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两组分辨率不同。</summary>
        public static bool operator !=(AppResolution left, AppResolution right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>独立于 Run save 的完整不可变应用设置快照。</summary>
    public sealed class AppSettingsSnapshot : IEquatable<AppSettingsSnapshot>
    {
        public const string EnglishLocaleCode = "en";
        public const string SimplifiedChineseLocaleCode = "zh-CN";

        /// <summary>Unity Localization 使用的稳定 locale code。</summary>
        public string LocaleCode { get; }

        /// <summary>0～100 的主音量百分比。</summary>
        public int MasterVolumePercent { get; }

        /// <summary>当前窗口/无边框显示模式。</summary>
        public AppDisplayMode DisplayMode { get; }

        /// <summary>当前目标显示分辨率。</summary>
        public AppResolution Resolution { get; }

        /// <summary>当前离散文字缩放档位。</summary>
        public AppTextScale TextScale { get; }

        /// <summary>是否启用高对比表现。</summary>
        public bool HighContrast { get; }

        /// <summary>是否减少非必要动态效果。</summary>
        public bool ReducedMotion { get; }

        /// <summary>冻结并验证一份完整应用设置。</summary>
        public AppSettingsSnapshot(
            string localeCode,
            int masterVolumePercent,
            AppDisplayMode displayMode,
            AppResolution resolution,
            AppTextScale textScale,
            bool highContrast,
            bool reducedMotion)
        {
            if (!IsSupportedLocale(localeCode))
                throw new ArgumentOutOfRangeException(nameof(localeCode));
            if (masterVolumePercent < 0 || masterVolumePercent > 100)
                throw new ArgumentOutOfRangeException(nameof(masterVolumePercent));
            if (!Enum.IsDefined(typeof(AppDisplayMode), displayMode))
                throw new ArgumentOutOfRangeException(nameof(displayMode));
            if (!Enum.IsDefined(typeof(AppTextScale), textScale))
                throw new ArgumentOutOfRangeException(nameof(textScale));

            LocaleCode = localeCode;
            MasterVolumePercent = masterVolumePercent;
            DisplayMode = displayMode;
            Resolution = resolution;
            TextScale = textScale;
            HighContrast = highContrast;
            ReducedMotion = reducedMotion;
        }

        /// <summary>确认 locale code 属于首发明确支持集合。</summary>
        public static bool IsSupportedLocale(string localeCode)
        {
            return string.Equals(localeCode, EnglishLocaleCode, StringComparison.Ordinal) ||
                   string.Equals(localeCode, SimplifiedChineseLocaleCode, StringComparison.Ordinal);
        }

        /// <summary>逐字段比较两份完整设置快照。</summary>
        public bool Equals(AppSettingsSnapshot other)
        {
            return other != null &&
                   string.Equals(LocaleCode, other.LocaleCode, StringComparison.Ordinal) &&
                   MasterVolumePercent == other.MasterVolumePercent &&
                   DisplayMode == other.DisplayMode &&
                   Resolution == other.Resolution &&
                   TextScale == other.TextScale &&
                   HighContrast == other.HighContrast &&
                   ReducedMotion == other.ReducedMotion;
        }

        /// <summary>比较对象是否为逐字段相同的设置快照。</summary>
        public override bool Equals(object obj)
        {
            return obj is AppSettingsSnapshot other && Equals(other);
        }

        /// <summary>返回全部设置事实组成的稳定哈希。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(LocaleCode);
                hash = (hash * 397) ^ MasterVolumePercent;
                hash = (hash * 397) ^ (int)DisplayMode;
                hash = (hash * 397) ^ Resolution.GetHashCode();
                hash = (hash * 397) ^ (int)TextScale;
                hash = (hash * 397) ^ HighContrast.GetHashCode();
                return (hash * 397) ^ ReducedMotion.GetHashCode();
            }
        }
    }

    /// <summary>应用设置文档读取的封闭结果分类。</summary>
    public enum AppSettingsDocumentReadStatus
    {
        Success,
        InvalidJson,
        InvalidDocument,
        UnsupportedSchema,
    }

    /// <summary>严格 codec 返回的设置或类型化失败。</summary>
    public sealed class AppSettingsDocumentReadResult
    {
        /// <summary>读取结果分类。</summary>
        public AppSettingsDocumentReadStatus Status { get; }

        /// <summary>成功时的完整设置快照。</summary>
        public AppSettingsSnapshot Settings { get; }

        /// <summary>失败时供诊断的非空原因。</summary>
        public string Detail { get; }

        /// <summary>冻结一项 codec 读取结果。</summary>
        private AppSettingsDocumentReadResult(
            AppSettingsDocumentReadStatus status,
            AppSettingsSnapshot settings,
            string detail)
        {
            Status = status;
            Settings = settings;
            Detail = detail ?? string.Empty;
        }

        /// <summary>创建成功读取结果。</summary>
        internal static AppSettingsDocumentReadResult Succeeded(AppSettingsSnapshot settings)
        {
            return new AppSettingsDocumentReadResult(
                AppSettingsDocumentReadStatus.Success,
                settings ?? throw new ArgumentNullException(nameof(settings)),
                string.Empty);
        }

        /// <summary>创建不携带设置的失败结果。</summary>
        internal static AppSettingsDocumentReadResult Failed(
            AppSettingsDocumentReadStatus status,
            string detail)
        {
            if (status == AppSettingsDocumentReadStatus.Success)
                throw new ArgumentOutOfRangeException(nameof(status));

            return new AppSettingsDocumentReadResult(status, null, detail);
        }
    }

    /// <summary>schema v1 应用设置 JSON 的唯一编码与严格读取入口。</summary>
    public static class AppSettingsDocumentCodec
    {
        public const int CurrentSchemaVersion = 1;

        private static readonly HashSet<string> SchemaV1PropertyNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "schemaVersion",
                "localeCode",
                "masterVolumePercent",
                "displayMode",
                "resolutionWidth",
                "resolutionHeight",
                "textScalePercent",
                "highContrast",
                "reducedMotion",
            };

        /// <summary>把完整设置编码为稳定、无缩进 JSON。</summary>
        public static string Write(AppSettingsSnapshot settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var document = new JObject
            {
                ["schemaVersion"] = CurrentSchemaVersion,
                ["localeCode"] = settings.LocaleCode,
                ["masterVolumePercent"] = settings.MasterVolumePercent,
                ["displayMode"] = settings.DisplayMode.ToString(),
                ["resolutionWidth"] = settings.Resolution.Width,
                ["resolutionHeight"] = settings.Resolution.Height,
                ["textScalePercent"] = (int)settings.TextScale,
                ["highContrast"] = settings.HighContrast,
                ["reducedMotion"] = settings.ReducedMotion,
            };
            return document.ToString(Formatting.None);
        }

        /// <summary>严格读取 schema v1，任何缺失或越界字段都不发布半合法设置。</summary>
        public static AppSettingsDocumentReadResult Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return AppSettingsDocumentReadResult.Failed(
                    AppSettingsDocumentReadStatus.InvalidJson,
                    "App settings JSON cannot be empty.");
            }

            JObject document;
            try
            {
                document = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                return AppSettingsDocumentReadResult.Failed(
                    AppSettingsDocumentReadStatus.InvalidJson,
                    exception.Message);
            }

            try
            {
                int schemaVersion =
                    RequireValue<int>(document, "schemaVersion", JTokenType.Integer);
                if (schemaVersion != CurrentSchemaVersion)
                {
                    return AppSettingsDocumentReadResult.Failed(
                        AppSettingsDocumentReadStatus.UnsupportedSchema,
                        $"Unsupported app settings schema {schemaVersion}.");
                }

                ValidateSchemaV1Properties(document);
                var settings = new AppSettingsSnapshot(
                    RequireValue<string>(document, "localeCode", JTokenType.String),
                    RequireValue<int>(document, "masterVolumePercent", JTokenType.Integer),
                    ParseEnum<AppDisplayMode>(document, "displayMode"),
                    new AppResolution(
                        RequireValue<int>(document, "resolutionWidth", JTokenType.Integer),
                        RequireValue<int>(document, "resolutionHeight", JTokenType.Integer)),
                    ParseTextScale(document),
                    RequireValue<bool>(document, "highContrast", JTokenType.Boolean),
                    RequireValue<bool>(document, "reducedMotion", JTokenType.Boolean));
                return AppSettingsDocumentReadResult.Succeeded(settings);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is FormatException ||
                exception is OverflowException)
            {
                return AppSettingsDocumentReadResult.Failed(
                    AppSettingsDocumentReadStatus.InvalidDocument,
                    exception.Message);
            }
        }

        /// <summary>拒绝 schema v1 未声明的额外属性。</summary>
        private static void ValidateSchemaV1Properties(JObject document)
        {
            foreach (JProperty property in document.Properties())
            {
                if (!SchemaV1PropertyNames.Contains(property.Name))
                {
                    throw new FormatException(
                        $"App settings field '{property.Name}' is not supported by schema v1.");
                }
            }
        }

        /// <summary>读取必需属性并拒绝 null、错误 token 类型或数值越界。</summary>
        private static T RequireValue<T>(
            JObject document,
            string propertyName,
            JTokenType expectedTokenType)
        {
            JToken token = document[propertyName];
            if (token == null || token.Type == JTokenType.Null)
                throw new FormatException($"App settings field '{propertyName}' is required.");

            if (token.Type != expectedTokenType)
            {
                throw new FormatException(
                    $"App settings field '{propertyName}' has an invalid type.");
            }

            try
            {
                return token.Value<T>();
            }
            catch (Exception exception) when (
                exception is InvalidCastException ||
                exception is FormatException ||
                exception is OverflowException)
            {
                throw new FormatException(
                    $"App settings field '{propertyName}' has an invalid type.",
                    exception);
            }
        }

        /// <summary>按稳定名称读取封闭枚举并拒绝数字或未知名称。</summary>
        private static T ParseEnum<T>(JObject document, string propertyName)
            where T : struct
        {
            string value = RequireValue<string>(document, propertyName, JTokenType.String);
            if (!Enum.TryParse(value, ignoreCase: false, out T parsed) ||
                !Enum.IsDefined(typeof(T), parsed))
            {
                throw new FormatException(
                    $"App settings field '{propertyName}' has unsupported value '{value}'.");
            }

            return parsed;
        }

        /// <summary>把离散数值读取为受支持文字缩放档位。</summary>
        private static AppTextScale ParseTextScale(JObject document)
        {
            int value =
                RequireValue<int>(document, "textScalePercent", JTokenType.Integer);
            if (!Enum.IsDefined(typeof(AppTextScale), value))
                throw new FormatException($"Unsupported text scale {value}.");

            return (AppTextScale)value;
        }
    }
}
