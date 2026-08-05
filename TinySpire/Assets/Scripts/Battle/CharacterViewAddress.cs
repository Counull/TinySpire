using System;
using System.IO;

namespace TinySpire.Battle
{
    public static class CharacterViewAddress
    {
        public const string Prefix = "character-view/";

        /// <summary>将配置中的角色视图短键转换为稳定的 Addressables 逻辑地址。</summary>
        public static string FromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Character view key cannot be empty.", nameof(key));
            if (!string.Equals(key, key.Trim(), StringComparison.Ordinal)
                || key.IndexOf('/') >= 0
                || key.IndexOf('\\') >= 0
                || Path.HasExtension(key))
            {
                throw new ArgumentException(
                    $"Character view key must be a filename stem without whitespace, directories, or extension: '{key}'.",
                    nameof(key));
            }

            return Prefix + key;
        }
    }
}
