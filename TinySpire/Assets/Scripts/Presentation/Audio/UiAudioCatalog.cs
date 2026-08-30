using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Presentation.Audio
{
    /// <summary>首发 UI 音频的封闭 cue 身份。</summary>
    public enum UiAudioCue
    {
        Hover,
        Click,
        Confirm,
        Error,
    }

    /// <summary>配置和资源文件共同使用的稳定、无路径短键。</summary>
    public static class UiAudioCueKeys
    {
        public const string Hover = "hover";
        public const string Click = "click";
        public const string Confirm = "confirm";
        public const string Error = "error";
    }

    /// <summary>一个 cue 的领域身份、稳定短键和唯一逻辑地址。</summary>
    public sealed class UiAudioCueDefinition
    {
        /// <summary>调用方使用的封闭 cue 身份。</summary>
        public UiAudioCue Cue { get; }

        /// <summary>不含目录与扩展名的稳定短键。</summary>
        public string Key { get; }

        /// <summary>Addressables 使用的唯一逻辑地址。</summary>
        public string Address { get; }

        /// <summary>冻结并验证一项 UI 音频声明。</summary>
        internal UiAudioCueDefinition(UiAudioCue cue, string key)
        {
            if (!Enum.IsDefined(typeof(UiAudioCue), cue))
                throw new ArgumentOutOfRangeException(nameof(cue));

            Cue = cue;
            Key = key;
            Address = UiAudioAddress.FromKey(key);
        }
    }

    /// <summary>首发必须完整加载的唯一 UI 音频声明集合。</summary>
    public static class UiAudioCatalog
    {
        private static readonly UiAudioCueDefinition[] OrderedDefinitions =
        {
            new UiAudioCueDefinition(UiAudioCue.Hover, UiAudioCueKeys.Hover),
            new UiAudioCueDefinition(UiAudioCue.Click, UiAudioCueKeys.Click),
            new UiAudioCueDefinition(UiAudioCue.Confirm, UiAudioCueKeys.Confirm),
            new UiAudioCueDefinition(UiAudioCue.Error, UiAudioCueKeys.Error),
        };

        private static readonly ReadOnlyCollection<UiAudioCueDefinition> ReadOnlyDefinitions =
            Array.AsReadOnly(OrderedDefinitions);

        /// <summary>按初始化与构建门禁顺序公开完整只读声明集合。</summary>
        public static IReadOnlyList<UiAudioCueDefinition> Ordered => ReadOnlyDefinitions;

        /// <summary>按封闭 cue 身份查找唯一声明。</summary>
        public static bool TryGet(UiAudioCue cue, out UiAudioCueDefinition definition)
        {
            for (int index = 0; index < OrderedDefinitions.Length; index++)
            {
                UiAudioCueDefinition candidate = OrderedDefinitions[index];
                if (candidate.Cue != cue)
                    continue;

                definition = candidate;
                return true;
            }

            definition = null;
            return false;
        }
    }

    /// <summary>把 UI 音频短键严格转换为 ui-audio/{key} 逻辑地址。</summary>
    public static class UiAudioAddress
    {
        public const string Prefix = "ui-audio/";

        /// <summary>验证小写 ASCII 短键并转换为唯一逻辑地址。</summary>
        public static string FromKey(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("UI audio key cannot be empty.", nameof(key));
            if (key[0] < 'a' || key[0] > 'z')
                throw new ArgumentException("UI audio key must begin with a lowercase letter.", nameof(key));

            for (int index = 0; index < key.Length; index++)
            {
                char value = key[index];
                bool valid = value >= 'a' && value <= 'z' ||
                             value >= '0' && value <= '9' ||
                             value == '-' ||
                             value == '_';
                if (!valid)
                {
                    throw new ArgumentException(
                        "UI audio key must contain only lowercase ASCII letters, digits, '-' or '_'.",
                        nameof(key));
                }
            }

            return Prefix + key;
        }

        /// <summary>验证完整地址严格等于当前域的短键转换结果。</summary>
        public static void ValidateAddress(string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (!address.StartsWith(Prefix, StringComparison.Ordinal))
                throw new ArgumentException("UI audio address must use the exact ui-audio/ prefix.", nameof(address));

            string key = address.Substring(Prefix.Length);
            string canonical = FromKey(key);
            if (!string.Equals(address, canonical, StringComparison.Ordinal))
                throw new ArgumentException("UI audio address is not canonical.", nameof(address));
        }
    }
}
