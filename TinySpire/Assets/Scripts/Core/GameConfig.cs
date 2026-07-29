using Newtonsoft.Json;

/// <summary>
/// 从 Assets/GameData/game-config.json 加载的运行时配置。
/// 给手写 JSON 配置用，不走 Luban 表格管线。
/// 所有字段带默认值，JSON 中缺失时仍能正常工作。
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public sealed class GameConfig
{
    [JsonProperty] public int InitialHandCount { get; private set; } = 5;
}
