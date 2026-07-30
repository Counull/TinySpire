using NUnit.Framework;

public sealed class GameStartupOptionsTests
{
    [TestCase("BattleScene", "Assets/Scenes/BattleScene.unity")]
    [TestCase("Assets/Scenes/BattleScene", "Assets/Scenes/BattleScene.unity")]
    [TestCase("Assets/Scenes/BattleScene.unity", "Assets/Scenes/BattleScene.unity")]
    public void SceneAddress_UsesStableAddressableAssetPath(string configuredName, string expectedAddress)
    {
        var options = new GameStartupOptions(configuredName, "LoadingScene");

        Assert.That(options.InitialSceneAddress, Is.EqualTo(expectedAddress));
        Assert.That(options.LoadingSceneAddress, Is.EqualTo("Assets/Scenes/LoadingScene.unity"));
    }
}
