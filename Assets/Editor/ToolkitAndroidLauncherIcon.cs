using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;

// PICO's library/dock must be able to resolve a bitmap directly from both
// ApplicationInfo and the launch activity, without an adaptive-icon decoder.
public sealed class ToolkitAndroidLauncherIcon : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 10000;
    const string Android = "http://schemas.android.com/apk/res/android";
    const string Resource = "@drawable/spatial_toolkit_launcher";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string resources = Path.Combine(path, "src/main/res/drawable-nodpi");
        Directory.CreateDirectory(resources);
        File.Copy("Assets/SpatialToolkit/Art/SpatialToolkitIcon.png",
            Path.Combine(resources, "spatial_toolkit_launcher.png"), true);
        Patch(Path.Combine(path, "src/main/AndroidManifest.xml"), true);
        Patch(Path.Combine(Path.GetDirectoryName(path), "launcher/src/main/AndroidManifest.xml"), false);
    }

    static void Patch(string path, bool requireActivity)
    {
        if (!File.Exists(path)) throw new BuildFailedException("Missing generated Android manifest: " + path);
        var document = new XmlDocument(); document.Load(path);
        var app = document.SelectSingleNode("/manifest/application") as XmlElement;
        if (app == null) throw new BuildFailedException("Generated manifest has no application: " + path);
        app.SetAttribute("icon", Android, Resource);
        app.SetAttribute("roundIcon", Android, Resource);
        // This library manifest is merged beneath the launcher's manifest.
        // The launcher itself supplies the same attributes at higher priority.
        int activities = 0;
        foreach (XmlElement activity in app.SelectNodes("activity"))
        {
            if (activity.GetAttribute("name", Android) != "com.unity3d.player.UnityPlayerActivity") continue;
            activity.SetAttribute("icon", Android, Resource);
            activity.SetAttribute("label", Android, "@string/app_name");
            activities++;
        }
        if (requireActivity && activities != 1)
            throw new BuildFailedException("Expected exactly one Unity launcher activity.");
        document.Save(path);
    }
}
