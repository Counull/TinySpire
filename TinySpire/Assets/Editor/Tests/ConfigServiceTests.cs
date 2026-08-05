using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// ConfigService 的配置加载原子性契约测试。
/// </summary>
public sealed class ConfigServiceTests
{
    /// <summary>验证 game-config 读取失败时会返回可诊断的 typed failure，且不发布半成品配置。</summary>
    [Test]
    public async Task InitializeAsync_WhenGameConfigLoadFails_ThrowsTypedFailureWithoutPublishingPartialState()
    {
        var loader = new FakeConfigTextLoader();
        loader.SetFailure(
            "Assets/GameData/game-config.json",
            new InvalidOperationException("game-config asset is missing."));
        var configs = new ConfigService();

        ConfigInitializationException failure = null;
        try
        {
            await configs.InitializeAsync(loader);
        }
        catch (ConfigInitializationException exception)
        {
            failure = exception;
        }

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure.Address, Is.EqualTo("Assets/GameData/game-config.json"));
        Assert.That(failure.TableName, Is.Null);
        Assert.That(configs.Tables, Is.Null);
        Assert.That(configs.GameConfig, Is.Null);
    }

    /// <summary>验证任一必需表缺失时会携带表名失败，并且不发布已经读取的其他表。</summary>
    [Test]
    public async Task InitializeAsync_WhenRequiredTableLoadFails_ThrowsTypedFailureWithoutPublishingPartialState()
    {
        const string tableName = "battle_tbcard";
        const string address = "Assets/GameData/battle_tbcard.json";
        var loader = new FakeConfigTextLoader();
        loader.SetFailure(address, new InvalidOperationException("card table asset is missing."));
        var configs = new ConfigService();

        ConfigInitializationException failure = null;
        try
        {
            await configs.InitializeAsync(loader);
        }
        catch (ConfigInitializationException exception)
        {
            failure = exception;
        }

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure.Address, Is.EqualTo(address));
        Assert.That(failure.TableName, Is.EqualTo(tableName));
        Assert.That(failure.Reason, Is.EqualTo(ConfigInitializationFailureReason.AssetLoadFailed));
        Assert.That(configs.Tables, Is.Null);
        Assert.That(configs.GameConfig, Is.Null);
    }

    /// <summary>验证表对象中的非对象行会在解析边界携带对应表名失败，而非延迟到 Tables 构造期。</summary>
    [Test]
    public async Task InitializeAsync_WhenTableContainsNonObjectRow_ThrowsTypedFailureWithTableName()
    {
        const string tableName = "battle_tbcard";
        const string address = "Assets/GameData/battle_tbcard.json";
        var loader = new FakeConfigTextLoader();
        loader.SetText(address, "{\"1001\":42}");
        var configs = new ConfigService();

        ConfigInitializationException failure = null;
        try
        {
            await configs.InitializeAsync(loader);
        }
        catch (ConfigInitializationException exception)
        {
            failure = exception;
        }

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure.Address, Is.EqualTo(address));
        Assert.That(failure.TableName, Is.EqualTo(tableName));
        Assert.That(failure.Reason, Is.EqualTo(ConfigInitializationFailureReason.InvalidTableRowShape));
        Assert.That(configs.Tables, Is.Null);
        Assert.That(configs.GameConfig, Is.Null);
    }

    /// <summary>验证 game-config 的坏 JSON、错误根节点或缺失字段不会回退到代码默认值。</summary>
    [TestCase("{", ConfigInitializationFailureReason.InvalidJson)]
    [TestCase("[]", ConfigInitializationFailureReason.InvalidGameConfigShape)]
    [TestCase("{}", ConfigInitializationFailureReason.MissingRequiredGameConfigField)]
    public async Task InitializeAsync_WhenGameConfigIsInvalid_ThrowsTypedFailureWithoutPublishingDefaults(
        string gameConfigJson,
        ConfigInitializationFailureReason expectedReason)
    {
        const string address = "Assets/GameData/game-config.json";
        var loader = new FakeConfigTextLoader();
        loader.SetText(address, gameConfigJson);
        var configs = new ConfigService();

        ConfigInitializationException failure = null;
        try
        {
            await configs.InitializeAsync(loader);
        }
        catch (ConfigInitializationException exception)
        {
            failure = exception;
        }

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure.Address, Is.EqualTo(address));
        Assert.That(failure.TableName, Is.Null);
        Assert.That(failure.Reason, Is.EqualTo(expectedReason));
        Assert.That(configs.Tables, Is.Null);
        Assert.That(configs.GameConfig, Is.Null);
    }

    /// <summary>验证初始化失败后可在同一服务实例中重试，并且只在完整成功后一次性发布配置。</summary>
    [Test]
    public async Task InitializeAsync_AfterFailureCanRetryAndPublishCompleteConfiguration()
    {
        const string address = "Assets/GameData/game-config.json";
        var loader = new FakeConfigTextLoader();
        loader.SetFailure(address, new InvalidOperationException("temporary game-config failure."));
        var configs = new ConfigService();

        try
        {
            await configs.InitializeAsync(loader);
            Assert.Fail("The first initialization attempt should fail.");
        }
        catch (ConfigInitializationException exception)
        {
            Assert.That(exception.Address, Is.EqualTo(address));
        }

        loader.ClearFailure(address);
        await configs.InitializeAsync(loader);

        Assert.That(configs.Tables, Is.Not.Null);
        Assert.That(configs.GameConfig, Is.Not.Null);
        Assert.That(configs.GameConfig.InitialHandCount, Is.EqualTo(5));
        Assert.That(configs.GameConfig.EnergyPerRound, Is.EqualTo(3));
    }

    /// <summary>提供仅供测试使用的内存文本加载器，并为表格地址返回合法空对象。</summary>
    private sealed class FakeConfigTextLoader : IConfigTextLoader
    {
        private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);

        /// <summary>为指定地址配置一次可观察的加载失败。</summary>
        public void SetFailure(string address, Exception exception)
        {
            _failures.Add(address, exception);
        }

        /// <summary>移除指定地址的预设失败，使测试可验证同一服务实例的重试路径。</summary>
        public void ClearFailure(string address)
        {
            _failures.Remove(address);
        }

        /// <summary>为指定地址覆盖默认的合法配置文本。</summary>
        public void SetText(string address, string text)
        {
            _texts.Add(address, text);
        }

        /// <summary>按地址提供配置文本或抛出预设加载异常。</summary>
        public async UniTask<string> LoadTextAsync(string address)
        {
            await UniTask.Yield();
            if (_failures.TryGetValue(address, out Exception exception))
                throw exception;

            if (_texts.TryGetValue(address, out string text))
                return text;

            return address == "Assets/GameData/game-config.json"
                ? "{\"initialHandCount\":5,\"energyPerRound\":3}"
                : "{}";
        }
    }
}
