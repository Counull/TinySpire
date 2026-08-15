using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TinySpire.Run;
using TinySpire.UI.Run;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public sealed class RunEntrySceneContractTests
{
    private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.unity";

    /// <summary>生产 Addressables 配置源必须精确列出 Loading、RunEntry 与 Battle 三个场景。</summary>
    [Test]
    public void AddressablesBuildSource_IncludesThreeG1AScenesExactlyOnce()
    {
        FieldInfo field = typeof(AddressablesBuildTools).GetField(
            "ScenePaths",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(field, Is.Not.Null);
        var actual = (string[])field.GetValue(null);
        Assert.That(actual, Has.Length.EqualTo(3));
        Assert.That(
            actual,
            Is.EquivalentTo(new[]
            {
                "Assets/Scenes/LoadingScene.unity",
                RunSceneAddresses.RunEntry,
                RunSceneAddresses.Battle,
            }));
    }

    /// <summary>RunEntryScene 必须只有场景级入口 Scope/View，并由 Bootstrap 作为父 Scope。</summary>
    [Test]
    public void RunEntryScene_HasCameraLightViewAndBootstrapParentScope()
    {
        SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(RunSceneAddresses.RunEntry);
        Assert.That(asset, Is.Not.Null);
        Scene scene = LoadSceneForTest(RunSceneAddresses.RunEntry, out bool openedHere);
        try
        {
            Camera[] cameras = FindComponents<Camera>(scene);
            Light[] directionalLights = FindComponents<Light>(scene)
                .Where(light => light.type == LightType.Directional)
                .ToArray();
            RunEntryLifetimeScope[] scopes = FindComponents<RunEntryLifetimeScope>(scene);
            RunEntryView[] views = FindComponents<RunEntryView>(scene);

            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].CompareTag("MainCamera"), Is.True);
            Assert.That(directionalLights, Has.Length.EqualTo(1));
            Assert.That(scopes, Has.Length.EqualTo(1));
            Assert.That(scopes[0].transform.parent, Is.Null);
            Assert.That(scopes[0].parentReference.Type, Is.EqualTo(typeof(Bootstrap)));
            Assert.That(scopes[0].parentReference.TypeName, Is.EqualTo(typeof(Bootstrap).FullName));
            Assert.That(views, Has.Length.EqualTo(1));
            Assert.That(views[0].transform.parent, Is.Null);
            Assert.That(FindComponents<Bootstrap>(scene), Is.Empty);
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    /// <summary>RunEntry 场景 Scope 必须把唯一 View 暴露给接口并启动 Presenter entry point。</summary>
    [Test]
    public void RunEntryScope_RegistersViewInterfaceAndPresenterEntryPoint()
    {
        Scene scene = LoadSceneForTest(RunSceneAddresses.RunEntry, out bool openedHere);
        try
        {
            RunEntryLifetimeScope scope = FindComponents<RunEntryLifetimeScope>(scene).Single();
            var builder = new ContainerBuilder
            {
                ApplicationOrigin = scope,
            };
            MethodInfo configure = typeof(RunEntryLifetimeScope).GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(configure, Is.Not.Null);
            configure.Invoke(scope, new object[] { builder });

            Assert.That(
                builder.Exists(typeof(IRunEntryView), includeInterfaceTypes: true),
                Is.True);
            Assert.That(
                builder.Exists(typeof(RunEntryPresenter), includeInterfaceTypes: true),
                Is.True);
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    /// <summary>生产 Presenter 构造器必须显式标记 Inject，避免 VContainer 误选更长的测试 seam。</summary>
    [Test]
    public void RunEntryPresenter_ProductionConstructor_IsExplicitInjectionPoint()
    {
        ConstructorInfo constructor = typeof(RunEntryPresenter).GetConstructor(new[]
        {
            typeof(IRunEntryView),
            typeof(RunStateStore),
            typeof(RunFlowService),
            typeof(ConfigService),
            typeof(LocalizationService),
        });

        Assert.That(constructor, Is.Not.Null);
        Assert.That(constructor.GetCustomAttribute<InjectAttribute>(), Is.Not.Null);
    }

    /// <summary>BootstrapScene 的真实序列化入口必须指向 RunEntryScene，而不是只改 C# 默认值。</summary>
    [Test]
    public void BootstrapScene_UsesRunEntryAsSerializedInitialScene()
    {
        Scene scene = LoadSceneForTest(BootstrapScenePath, out bool openedHere);
        try
        {
            Bootstrap[] bootstraps = FindComponents<Bootstrap>(scene);
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            var serialized = new SerializedObject(bootstraps[0]);
            SerializedProperty initialScene = serialized.FindProperty("initialSceneName");
            Assert.That(initialScene, Is.Not.Null);

            var options = new GameStartupOptions(initialScene.stringValue, "LoadingScene");
            Assert.That(options.InitialSceneAddress, Is.EqualTo(RunSceneAddresses.RunEntry));
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    /// <summary>同步后的 TinySpire Scenes 组必须只暴露三个稳定完整场景地址并分别打包。</summary>
    [Test]
    public void AddressableScenesGroup_ContainsExactG1ASceneSet()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        Assert.That(settings, Is.Not.Null);
        AddressableAssetGroup group = settings.FindGroup("TinySpire Scenes");
        Assert.That(group, Is.Not.Null);
        Assert.That(group.entries, Has.Count.EqualTo(3));
        Assert.That(
            group.entries.Select(entry => entry.address),
            Is.EquivalentTo(new[]
            {
                "Assets/Scenes/LoadingScene.unity",
                RunSceneAddresses.RunEntry,
                RunSceneAddresses.Battle,
            }));

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        Assert.That(schema, Is.Not.Null);
        Assert.That(
            schema.BundleMode,
            Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackSeparately));
        Assert.That(schema.IncludeInBuild, Is.True);
    }

    /// <summary>Player Build Settings 继续只直接包含 BootstrapScene，运行场景均由 Addressables 管理。</summary>
    [Test]
    public void BuildSettings_StillContainsOnlyBootstrapScene()
    {
        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        Assert.That(enabledScenes, Is.EqualTo(new[] { BootstrapScenePath }));
    }

    /// <summary>读取已加载场景或以 Additive 临时打开，并标记是否应由本测试关闭。</summary>
    private static Scene LoadSceneForTest(string scenePath, out bool openedHere)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        openedHere = !scene.IsValid() || !scene.isLoaded;
        return openedHere
            ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
            : scene;
    }

    /// <summary>只在指定 Scene 根层级中寻找组件，避免污染或误关闭用户已加载场景。</summary>
    private static T[] FindComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive: true))
            .ToArray();
    }
}
