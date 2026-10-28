using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PicoDevelopmentBuild
{
    [MenuItem("PICO/Project/Build Development APK")]
    public static void Build()
    {
        WriteBuildResult("building", "");
        try
        {
            BuildCore();
            WriteBuildResult("succeeded", "Builds/Android/spatial-toolkit.apk");
        }
        catch (System.Exception exception)
        {
            WriteBuildResult("failed", exception.Message);
            throw;
        }
    }

    [System.Serializable]
    sealed class BuildStatusRecord
    {
        public string status;
        public string detail;
        public string timestampUtc;
    }

    static void WriteBuildResult(string status, string detail)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/AndroidBuildResult.json", JsonUtility.ToJson(new BuildStatusRecord
        {
            status = status, detail = detail,
            timestampUtc = System.DateTime.UtcNow.ToString("O")
        }, true));
    }

    static void BuildCore()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new BuildFailedException("Switch the active build target to Android first.");

        const string appScene = "Assets/Scenes/SpatialToolkitWorkbench.unity";
        if (!EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == appScene))
            throw new BuildFailedException("Enable SpatialToolkitWorkbench.unity in the Android build profile first.");
        if (Application.isBatchMode) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(appScene);
        else if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != appScene)
            throw new BuildFailedException("Open SpatialToolkitWorkbench.unity before building.");
#if ENABLE_PICO_XR_SDK
        var picoSettings = ByteDance.PICO.XR.PXR_ProjectSetting.GetProjectConfig();
        if (!picoSettings.videoSeeThrough || !picoSettings.spatialMesh || !picoSettings.spatialAnchor)
            throw new BuildFailedException("Configure PICO passthrough, spatial mesh and spatial anchors before building the measurement app.");
        if (!UnityEngine.Object.FindFirstObjectByType<ByteDance.PICO.XR.PXR_Manager>())
            throw new BuildFailedException("Open the app scene with its configured PICO XR Origin before building.");
#else
        throw new BuildFailedException("The measurement app requires the PICO XR runtime.");
#endif

        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled && scene.path == appScene)
            .Select(scene => scene.path).ToArray();
        if (scenes.Length == 0)
            throw new BuildFailedException("Enable a scene in the Android build profile first.");

        Directory.CreateDirectory("Builds/Android");
        var previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = "Builds/Android/spatial-toolkit.apk",
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result}");
            Debug.Log($"Development APK: {Path.GetFullPath(report.summary.outputPath)}");
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = previousAppBundle;
        }
    }
}
