using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Applies to builds from the menu, Build Profiles, and batch builds.
public sealed class ToolkitBuildVersion : IPreprocessBuildWithReport
{
    const string RecordPath = "ProjectSettings/ToolkitBuildVersion.json";
    public int callbackOrder => -1000;

    [Serializable]
    sealed class Record
    {
        public string lastVersion = "0.1.0";
        public int lastAndroidCode = 1;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        var record = File.Exists(RecordPath) ? JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath)) : new Record();
        if (!Version.TryParse(PlayerSettings.bundleVersion, out var current) || current.Build < 0 || current.Revision >= 0)
            throw new BuildFailedException("Use a three-part app version, for example 0.2.0.");
        if (!Version.TryParse(record.lastVersion, out var previous)) previous = new Version(0, 1, 0);
        // A deliberately advanced feature version (0.X.0) is retained for its first build.
        // Every subsequent build advances the patch, including retries of a failed build.
        if (current <= previous) current = new Version(previous.Major, previous.Minor, previous.Build + 1);
        PlayerSettings.bundleVersion = current.ToString(3);
        PlayerSettings.Android.bundleVersionCode = Math.Max(PlayerSettings.Android.bundleVersionCode, record.lastAndroidCode) + 1;
        record.lastVersion = PlayerSettings.bundleVersion;
        record.lastAndroidCode = PlayerSettings.Android.bundleVersionCode;
        File.WriteAllText(RecordPath, JsonUtility.ToJson(record, true));
        AssetDatabase.SaveAssets();
        Debug.Log($"Spatial Toolkit build version {record.lastVersion} ({record.lastAndroidCode})");
    }
}
