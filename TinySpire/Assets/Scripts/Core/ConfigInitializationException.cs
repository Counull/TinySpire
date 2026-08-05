using System;

/// <summary>
/// 配置初始化失败的稳定分类，供启动层和测试诊断失败来源。
/// </summary>
public enum ConfigInitializationFailureReason
{
    AssetLoadFailed,
    InvalidJson,
    InvalidGameConfigShape,
    MissingRequiredGameConfigField,
    UnsupportedTableShape,
    InvalidTableRowShape,
    TableConstructionFailed
}

/// <summary>
/// 表示 ConfigService 在发布任何配置前遇到的可诊断初始化失败。
/// </summary>
public sealed class ConfigInitializationException : Exception
{
    /// <summary>失败的稳定资源地址。</summary>
    public string Address { get; }

    /// <summary>失败关联的表名；game-config 失败时为空。</summary>
    public string TableName { get; }

    /// <summary>失败的稳定分类。</summary>
    public ConfigInitializationFailureReason Reason { get; }

    /// <summary>创建包含地址、表名与原因的配置初始化异常。</summary>
    public ConfigInitializationException(
        string address,
        string tableName,
        ConfigInitializationFailureReason reason,
        Exception innerException = null)
        : base(CreateMessage(address, tableName, reason), innerException)
    {
        Address = address;
        TableName = tableName;
        Reason = reason;
    }

    /// <summary>生成不依赖内部异常文本的稳定诊断消息。</summary>
    private static string CreateMessage(
        string address,
        string tableName,
        ConfigInitializationFailureReason reason)
    {
        string tableSegment = string.IsNullOrEmpty(tableName) ? string.Empty : $", table '{tableName}'";
        return $"Configuration initialization failed for '{address}'{tableSegment}: {reason}.";
    }
}
