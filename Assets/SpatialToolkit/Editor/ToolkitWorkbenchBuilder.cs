using System.IO;
using SpatialToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ToolkitWorkbenchBuilder
{
    public const string ScenePath = "Assets/Scenes/SpatialToolkitWorkbench.unity";
    [MenuItem("PICO/Spatial Toolkit/Create or Open Workbench")]
    public static void CreateOrOpen()
    {
        if (File.Exists(ScenePath))
        {
            ConfigureBuildScene();
            if (SceneManager.GetActiveScene().path == ScenePath)
            {
                var existing = Object.FindFirstObjectByType<SpatialToolkitApp>();
                if (existing && !existing.ReadoutMaterial) { AssignReadoutMaterial(existing); EditorSceneManager.SaveScene(existing.gameObject.scene); }
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
            return;
        }
        var previous = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path))
        {
            string preserved = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/WorkspaceBeforeToolkit.unity");
            if (!EditorSceneManager.SaveScene(previous, preserved)) throw new IOException("Could not preserve the untitled scene.");
            Debug.Log("Preserved the previously untitled scene at " + preserved);
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var root = new GameObject("Spatial Toolkit", typeof(SpatialToolkitApp), typeof(ToolkitDesktopRoom));
        Undo.RegisterCreatedObjectUndo(root, "Create Spatial Toolkit workbench");
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Undo.RegisterCreatedObjectUndo(cameraObject, "Create workbench camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 1.6f, -2.2f);
        cameraObject.transform.rotation = Quaternion.Euler(6, 0, 0);
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.10f, 0.14f);
        camera.nearClipPlane = 0.03f; camera.farClipPlane = 30; camera.fieldOfView = 65;
        var app = root.GetComponent<SpatialToolkitApp>(); app.ViewCamera = camera;
        var shader = Shader.Find("SpatialToolkit/Glass");
        if (!shader) throw new System.InvalidOperationException("Toolkit glass shader has not imported yet.");
        if (!AssetDatabase.IsValidFolder("Assets/SpatialToolkit/Materials")) AssetDatabase.CreateFolder("Assets/SpatialToolkit", "Materials");
        const string materialPath = "Assets/SpatialToolkit/Materials/ToolkitGlass.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(shader) { color = ToolkitVisuals.Mint, enableInstancing = true };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        app.OverlayMaterial = material;
        AssignReadoutMaterial(app);
        EditorUtility.SetDirty(app);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new System.IO.IOException("Could not save workbench scene.");
        ConfigureBuildScene();
        // Keep any unsaved user scene open. A second camera there can be disabled reversibly.
        foreach (var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (other != camera && other.gameObject.scene != scene)
            { Undo.RecordObject(other, "Preview workbench camera"); other.enabled = false; }
        Selection.activeGameObject = root;
        Debug.Log("Saved " + ScenePath + ". This workbench has an editor-only simulated room; PICO XR blocks still need provisioning.");
    }
    static void ConfigureBuildScene()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var scene in EditorBuildSettings.scenes)
            if (scene.path != ScenePath) scenes.Add(new EditorBuildSettingsScene(scene.path, false));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
    static void AssignReadoutMaterial(SpatialToolkitApp app)
    {
        const string path = "Assets/SpatialToolkit/Materials/ToolkitLabel.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            var shader = Shader.Find("SpatialToolkit/Label");
            if (!shader) throw new System.InvalidOperationException("Toolkit label shader has not imported yet.");
            material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        Undo.RecordObject(app, "Assign readable overlay material"); app.ReadoutMaterial = material; EditorUtility.SetDirty(app);
    }
}
