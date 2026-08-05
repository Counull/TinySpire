using Cysharp.Threading.Tasks;

/// <summary>
/// ConfigService 读取文本配置所依赖的最小边界。
/// </summary>
internal interface IConfigTextLoader
{
    /// <summary>按稳定 Addressables 地址读取一份配置文本。</summary>
    UniTask<string> LoadTextAsync(string address);
}
