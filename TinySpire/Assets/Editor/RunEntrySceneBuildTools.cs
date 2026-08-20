using System;
using System.IO;
using System.Linq;
using TinySpire.Run;
using TinySpire.UI.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

/// <summary>通过 Unity Scene API 创建 G1-A 入口场景并切换 Bootstrap 序列化入口。</summary>
public static class RunEntrySceneBuildTools
{
    private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.unity";
    private const string InitialScenePropertyName = "initialSceneName";
    private const string EntryBackgroundPath =
        "Assets/Arts/Runtime/UI/RunEntry/ui_run_entry_background.png";
    private const string EntryPaperTexturePath =
        "Assets/Arts/Runtime/UI/RunEntry/ui_run_entry_paper_grain.png";

    /// <summary>创建缺失的 RunEntryScene，并把 BootstrapScene 入口原子切换到该场景。</summary>
    [MenuItem("TinySpire/Run/Create G1-A Run Entry Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Cannot build RunEntryScene during Play Mode.");

        CreateRunEntryScene();
        UpdateBootstrapInitialScene();
        AssetDatabase.SaveAssets();
        Debug.Log("TinySpire G1-A RunEntryScene created and Bootstrap entry updated.");
    }

    /// <summary>创建独立附加场景，避免触碰 Editor 中尚未保存的用户场景。</summary>
    private static void CreateRunEntryScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RunSceneAddresses.RunEntry) != null)
        {
            throw new InvalidOperationException(
                $"Run entry scene already exists; refusing to overwrite: {RunSceneAddresses.RunEntry}");
        }

        Scene previousActive = SceneManager.GetActiveScene();
        bool replacesCleanUntitledScene = CanReplaceCleanUntitledScene(previousActive);
        if (HasLoadedUntitledScene() && !replacesCleanUntitledScene)
        {
            throw new InvalidOperationException(
                "An untitled or modified scene is open; save or close it before building RunEntryScene.");
        }

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.DefaultGameObjects,
            replacesCleanUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive);
        try
        {
            if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
                throw new InvalidOperationException("Unable to activate the new RunEntryScene.");

            EnsureCameraAndDirectionalLight(scene);
            var scopeObject = new GameObject(nameof(RunEntryLifetimeScope));
            var scope = scopeObject.AddComponent<RunEntryLifetimeScope>();
            scope.parentReference = ParentReference.Create<Bootstrap>();

            var viewObject = new GameObject(nameof(RunEntryView));
            RunEntryView view = viewObject.AddComponent<RunEntryView>();
            AssignEntryVisualAssets(view);

            Camera camera = RequireSingle<Camera>(scene, "Camera");
            if (!camera.CompareTag("MainCamera"))
                camera.tag = "MainCamera";
            RequireSingleDirectionalLight(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, RunSceneAddresses.RunEntry))
            {
                throw new InvalidOperationException(
                    $"Unable to save run entry scene: {RunSceneAddresses.RunEntry}");
            }
        }
        finally
        {
            if (!replacesCleanUntitledScene && previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
            if (!replacesCleanUntitledScene && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    /// <summary>给新建入口场景写入两项直接场景依赖，不引入 Resources 或额外 Addressables 地址。</summary>
    private static void AssignEntryVisualAssets(RunEntryView view)
    {
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(EntryBackgroundPath)
            ?? throw new InvalidOperationException(
                $"Run entry background is missing: {EntryBackgroundPath}");
        Texture2D paperTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(EntryPaperTexturePath)
            ?? throw new InvalidOperationException(
                $"Run entry paper texture is missing: {EntryPaperTexturePath}");

        var serialized = new SerializedObject(view);
        SerializedProperty backgroundProperty = serialized.FindProperty("_entryBackground")
            ?? throw new InvalidOperationException("RunEntryView._entryBackground was not found.");
        SerializedProperty paperProperty = serialized.FindProperty("_entryPaperTexture")
            ?? throw new InvalidOperationException("RunEntryView._entryPaperTexture was not found.");
        backgroundProperty.objectReferenceValue = background;
        paperProperty.objectReferenceValue = paperTexture;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    /// <summary>仅允许替换唯一、干净且未命名的 Editor 占位场景。</summary>
    private static bool CanReplaceCleanUntitledScene(Scene scene)
    {
        return SceneManager.sceneCount == 1 &&
               scene.IsValid() &&
               scene.isLoaded &&
               string.IsNullOrEmpty(scene.path) &&
               !scene.isDirty;
    }

    /// <summary>检查当前 Editor 是否仍加载任何无法 Additive 绕过的无标题场景。</summary>
    private static bool HasLoadedUntitledScene()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded && string.IsNullOrEmpty(scene.path))
                return true;
        }

        return false;
    }

    /// <summary>保留 Unity 默认对象，并只补足缺失的主 Camera、AudioListener 与方向光。</summary>
    private static void EnsureCameraAndDirectionalLight(Scene scene)
    {
        Camera[] cameras = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(includeInactive: true))
            .ToArray();
        if (cameras.Length > 1)
            throw new InvalidOperationException($"RunEntryScene has {cameras.Length} Cameras.");
        if (cameras.Length == 0)
        {
            var cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener));
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameras = new[] { cameraObject.GetComponent<Camera>() };
        }
        cameras[0].tag = "MainCamera";

        Light[] directionalLights = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(includeInactive: true))
            .Where(light => light.type == LightType.Directional)
            .ToArray();
        if (directionalLights.Length > 1)
        {
            throw new InvalidOperationException(
                $"RunEntryScene has {directionalLights.Length} Directional Lights.");
        }
        if (directionalLights.Length == 0)
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            SceneManager.MoveGameObjectToScene(lightObject, scene);
        }
    }

    /// <summary>通过 SerializedObject 最小修改 Bootstrap 私有入口字段并保存已有场景。</summary>
    private static void UpdateBootstrapInitialScene()
    {
        Scene scene = SceneManager.GetSceneByPath(BootstrapScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Additive);
        }
        else if (scene.isDirty)
        {
            throw new InvalidOperationException(
                "BootstrapScene has unsaved changes; refusing to save over them.");
        }

        try
        {
            Bootstrap bootstrap = RequireSingle<Bootstrap>(scene, nameof(Bootstrap));
            var serialized = new SerializedObject(bootstrap);
            SerializedProperty initialScene = serialized.FindProperty(InitialScenePropertyName)
                ?? throw new InvalidOperationException(
                    $"Bootstrap.{InitialScenePropertyName} was not found.");
            initialScene.stringValue = Path.GetFileNameWithoutExtension(RunSceneAddresses.RunEntry);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unable to save BootstrapScene.");
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    /// <summary>只在指定场景中要求恰好一个组件，避免跨场景全局查找。</summary>
    private static T RequireSingle<T>(Scene scene, string label) where T : Component
    {
        T[] values = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive: true))
            .ToArray();
        if (values.Length != 1)
        {
            throw new InvalidOperationException(
                $"RunEntryScene requires exactly one {label}; found {values.Length}.");
        }

        return values[0];
    }

    /// <summary>确认 Unity 默认场景只生成一个主方向光。</summary>
    private static Light RequireSingleDirectionalLight(Scene scene)
    {
        Light[] values = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(includeInactive: true))
            .Where(light => light.type == LightType.Directional)
            .ToArray();
        if (values.Length != 1)
        {
            throw new InvalidOperationException(
                $"RunEntryScene requires exactly one Directional Light; found {values.Length}.");
        }

        return values[0];
    }
}
