using System;
using System.Linq;
using ByteDance.PICO.XR;
using SpatialToolkit;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class PicoMRWorkbenchSetup
{
    [MenuItem("PICO/Project/Finish MR Workbench Setup")]
    public static void Configure()
    {
        const string scenePath = "Assets/Scenes/SpatialToolkitWorkbench.unity";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != scenePath || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open SpatialToolkitWorkbench.unity outside Play Mode first.");

        var app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        var origin = UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None)
            .SingleOrDefault(o => o.gameObject.scene == scene);
        var mesh = UnityEngine.Object.FindFirstObjectByType<SpatialMeshManager>();
        if (!app || !origin || !origin.Camera || !mesh)
            throw new InvalidOperationException("Enable the PICO XR Origin, passthrough, controllers and spatial mesh blocks first.");

        var settings = PXR_Settings.GetSettings();
        var project = PXR_ProjectSetting.GetProjectConfig();
        if (settings.appMode != PXR_Settings.AppMode.XR || project.isSpatialAdapter)
            throw new InvalidOperationException("Set PICO App Mode to XR before configuring this MR scene.");

        Undo.RecordObject(project, "Configure MR capabilities");
        project.videoSeeThrough = true;
        project.spatialMesh = true;
        project.spatialAnchor = true;
        project.mrSafeguard = true;
        project.handTracking = true;
        project.handTrackingSupportType = HandTrackingSupport.ControllersAndHands;
        project.eyeTracking = true;
        EditorUtility.SetDirty(project);
        Undo.RecordObject(settings, "Configure MR stereo rendering");
        settings.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.MultiPass;
        EditorUtility.SetDirty(settings);

        if (!origin.GetComponent<PXR_Manager>()) Undo.AddComponent<PXR_Manager>(origin.gameObject);
        var permission = origin.GetComponent<PXR_PermissionRequest>();
        if (!permission) permission = Undo.AddComponent<PXR_PermissionRequest>(origin.gameObject);
        Undo.RecordObject(permission, "Request spatial data access");
        permission.requestMR = true;

        Undo.RecordObject(origin, "Configure room tracking space");
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        origin.CameraYOffset = 0;
        Undo.RecordObject(app, "Connect toolkit to XR tracking");
        app.ViewCamera = origin.Camera;
        app.TrackingOrigin = origin.CameraFloorOffsetObject.transform;

        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera.gameObject.scene != scene) continue;
            Undo.RecordObject(camera, "Use one MR camera");
            camera.enabled = camera == origin.Camera;
            var listener = camera.GetComponent<AudioListener>();
            if (listener)
            {
                Undo.RecordObject(listener, "Use one XR audio listener");
                listener.enabled = camera == origin.Camera;
            }
        }

        var xrCamera = origin.Camera;
        xrCamera.clearFlags = CameraClearFlags.SolidColor;
        xrCamera.backgroundColor = Color.clear;
        xrCamera.allowHDR = false;

        // The imported PICO mesh prefab uses layer 6. Keep its collision geometry
        // while the physical room is visible through passthrough.
        var meshSettings = new SerializedObject(mesh);
        var meshPrefab = meshSettings.FindProperty("meshPrefab").objectReferenceValue as GameObject;
        if (!meshPrefab || !meshPrefab.GetComponentInChildren<MeshCollider>(true))
            throw new InvalidOperationException("Spatial mesh needs its configured MeshCollider prefab.");
        int surfaceLayer = meshPrefab.layer;
        if (surfaceLayer == 0 || surfaceLayer == 5)
            throw new InvalidOperationException("Spatial mesh must use a dedicated layer separate from app overlays and UI.");
        xrCamera.cullingMask &= ~(1 << surfaceLayer);

        foreach (var pipeline in Resources.FindObjectsOfTypeAll<UniversalRenderPipelineAsset>())
        {
            if (!AssetDatabase.Contains(pipeline)) continue;
            Undo.RecordObject(pipeline, "Disable HDR for MR passthrough");
            pipeline.supportsHDR = false;
            EditorUtility.SetDirty(pipeline);
        }

        EditorUtility.SetDirty(app);
        EditorUtility.SetDirty(origin);
        EditorUtility.SetDirty(permission);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("MR scene could not be saved.");
        Debug.Log("MR workbench saved: one transparent XR camera, floor tracking, passthrough, anchors, spatial-data permission, and collision-only room mesh.");
    }
}
