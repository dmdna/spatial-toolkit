using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class ToolkitAppIdentity
{
    const string IconPath = "Assets/SpatialToolkit/Art/SpatialToolkitIcon.png";
    const string BackgroundPath = "Assets/SpatialToolkit/Art/SpatialToolkitIconBackground.png";

    [MenuItem("PICO/Project/Apply Spatial Toolkit Identity")]
    public static void Apply()
    {
        if (!File.Exists(BackgroundPath))
        {
            var background = new Texture2D(32, 32, TextureFormat.RGB24, false);
            var pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(9, 17, 26, 255);
            background.SetPixels(pixels); background.Apply();
            File.WriteAllBytes(BackgroundPath, background.EncodeToPNG());
            Object.DestroyImmediate(background);
        }
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(BackgroundPath, ImportAssetOptions.ForceSynchronousImport);
        foreach (string path in new[] { IconPath, BackgroundPath })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (!icon || !backdrop) throw new System.Exception("App icon import failed.");
        PlayerSettings.productName = "Spatial Toolkit";
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var slot in slots)
            {
                if (slot.minLayerCount == 2) slot.SetTextures(new[] { backdrop, icon });
                else slot.SetTexture(icon);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Spatial Toolkit display name and default/Android legacy, round, adaptive icons applied.");
    }
}
