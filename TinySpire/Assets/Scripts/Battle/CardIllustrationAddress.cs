using System;
using System.IO;

namespace TinySpire.Battle
{
    public static class CardIllustrationAddress
    {
        public const string Prefix = "card-art/";

        /// <summary>将配置中的牌面短键转换为稳定的 Addressables 逻辑地址。</summary>
        public static string FromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Card illustration key cannot be empty.", nameof(key));
            if (!string.Equals(key, key.Trim(), StringComparison.Ordinal)
                || key.IndexOf('/') >= 0
                || key.IndexOf('\\') >= 0
                || Path.HasExtension(key))
            {
                throw new ArgumentException(
                    $"Card illustration key must be a filename stem without whitespace, directories, or extension: '{key}'.",
                    nameof(key));
            }

            return Prefix + key;
        }
    }
}
