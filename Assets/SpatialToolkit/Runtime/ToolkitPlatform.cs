using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
#endif

namespace SpatialToolkit
{
    public sealed class ToolkitPlatform : MonoBehaviour
    {
        SpatialToolkitApp app;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
        Transform anchoredRoot;
        ulong anchorHandle;
        string anchorUuid;
#endif
        public bool IsLocalized { get; private set; } = true;
        public void DetachRoom()
        {
            ReleaseHandle(); IsLocalized = true;
            if (app && app.RoomRoot) app.RoomRoot.gameObject.SetActive(true);
        }
        public async System.Threading.Tasks.Task<bool> DeleteSavedAnchor(string uuid)
        {
            if (Application.isEditor || string.IsNullOrEmpty(uuid)) return true;
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            if (!Guid.TryParse(uuid, out var savedUuid)) return true;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                if (!await ReadyAnchorProvider(timeout.Token)) return false;
                if (anchorHandle != 0 && anchorUuid == uuid)
                {
                    var removed = await PXR_MixedReality.UnPersistSpatialAnchorAsync(anchorHandle, timeout.Token);
                    if (removed != PxrResult.SUCCESS) { app.Message("Saved anchor could not be deleted: " + removed); return false; }
                    DetachRoom(); return true;
                }
                var query = await PXR_MixedReality.QuerySpatialAnchorAsync(new[] { savedUuid }, timeout.Token);
                if (query.result != PxrResult.SUCCESS) { app.Message("Saved anchor lookup failed: " + query.result); return false; }
                if (query.anchorHandleList == null) return true;
                bool success = true;
                try
                {
                    foreach (var handle in query.anchorHandleList)
                    {
                        if (PXR_MixedReality.GetAnchorUuid(handle, out var found) != PxrResult.SUCCESS || found != savedUuid) continue;
                        var removed = await PXR_MixedReality.UnPersistSpatialAnchorAsync(handle, timeout.Token);
                        if (removed != PxrResult.SUCCESS) { success = false; app.Message("Saved anchor could not be deleted: " + removed); }
                    }
                }
                finally
                {
                    foreach (var handle in query.anchorHandleList) if (handle != anchorHandle) PXR_MixedReality.DestroyAnchor(handle);
                }
                return success;
            }
#else
            await System.Threading.Tasks.Task.Yield(); return true;
#endif
        }
        public void Initialize(SpatialToolkitApp owner)
        {
            app = owner;
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            PXR_SpatialMeshManager.MeshAdded += OnMesh;
            PXR_SpatialMeshManager.MeshUpdated += OnMesh;
#endif
        }
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
        void OnMesh(Guid id, GameObject meshObject)
        {
            if (!meshObject) return;
            var filter = meshObject.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) return;
            var collider = meshObject.GetComponent<MeshCollider>();
            if (!collider) collider = meshObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            // Room geometry is collision-only; passthrough supplies the visible room.
            var renderer = meshObject.GetComponent<MeshRenderer>();
            if (renderer) renderer.enabled = false;
        }
        async System.Threading.Tasks.Task<bool> ReadyAnchorProvider(CancellationToken token)
        {
            if (!PXR_ProjectSetting.GetProjectConfig().spatialAnchor)
            { app.Message("Spatial anchors need to be enabled in PICO project settings."); return false; }
            var result = PXR_MixedReality.GetSenseDataProviderState(PxrSenseDataProviderType.SpatialAnchor, out var state);
            if (result == PxrResult.SUCCESS && state == PxrSenseDataProviderState.Running) return true;
            result = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SpatialAnchor, token);
            if (result == PxrResult.SUCCESS) return true;
            app.Message("Room anchor service unavailable: " + result); return false;
        }
#endif
        public async System.Threading.Tasks.Task<string> SaveAnchor(Transform room)
        {
            if (Application.isEditor) return "editor-preview";
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                if (!await ReadyAnchorProvider(timeout.Token)) return null;
                if (anchorHandle != 0 && anchoredRoot == room) return anchorUuid;
                var created = await PXR_MixedReality.CreateSpatialAnchorAsync(room.position, room.rotation, timeout.Token);
                if (created.result != PxrResult.SUCCESS)
                { app.Message("Could not create room anchor: " + created.result); return null; }
                bool retained = false;
                try
                {
                    var persisted = await PXR_MixedReality.PersistSpatialAnchorAsync(created.anchorHandle, timeout.Token);
                    if (persisted != PxrResult.SUCCESS)
                    { app.Message("Anchor was not saved: " + persisted); return null; }
                    ReleaseHandle(); anchorHandle = created.anchorHandle; anchorUuid = created.uuid.ToString(); anchoredRoot = room;
                    retained = true; return anchorUuid;
                }
                finally { if (!retained) PXR_MixedReality.DestroyAnchor(created.anchorHandle); }
            }
#else
            await System.Threading.Tasks.Task.Yield();
            app.Message("Persistent room anchors need the PICO XR runtime."); return null;
#endif
        }
        public async System.Threading.Tasks.Task<bool> RestoreAnchor(string uuid, Transform room)
        {
            if (Application.isEditor)
            {
                if (uuid != "editor-preview") { app.Message("Headset rooms must be localized on the headset."); return false; }
                return true;
            }
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            if (!Guid.TryParse(uuid, out var savedUuid)) { app.Message("Saved room has no valid spatial anchor."); return false; }
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(25));
                if (!await ReadyAnchorProvider(timeout.Token)) return false;
                if (anchorHandle != 0 && anchorUuid == uuid && PXR_MixedReality.LocateAnchor(anchorHandle, out var existingPosition, out var existingRotation) == PxrResult.SUCCESS)
                { anchoredRoot = room; room.SetPositionAndRotation(existingPosition, existingRotation); IsLocalized = true; room.gameObject.SetActive(true); return true; }
                var query = await PXR_MixedReality.QuerySpatialAnchorAsync(new[] { savedUuid }, timeout.Token);
                if (query.result != PxrResult.SUCCESS || query.anchorHandleList == null) { app.Message("Room anchor was not found. Scan the original room."); return false; }
                foreach (var handle in query.anchorHandleList)
                {
                    bool match = PXR_MixedReality.GetAnchorUuid(handle, out var foundUuid) == PxrResult.SUCCESS && foundUuid == savedUuid;
                    if (match && PXR_MixedReality.LocateAnchor(handle, out var position, out var rotation) == PxrResult.SUCCESS)
                    {
                        if (anchorHandle != handle) ReleaseHandle(); anchorHandle = handle; anchorUuid = uuid; anchoredRoot = room;
                        room.SetPositionAndRotation(position, rotation); IsLocalized = true; room.gameObject.SetActive(true); return true;
                    }
                    if (handle != anchorHandle) PXR_MixedReality.DestroyAnchor(handle);
                }
                app.Message("Anchor not localized yet. Look around the original room and try Load again."); return false;
            }
#else
            await System.Threading.Tasks.Task.Yield();
            app.Message("Room restoration needs a PICO XR device."); return false;
#endif
        }
        void Update()
        {
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            if (anchorHandle == 0 || !anchoredRoot) return;
            bool localized = PXR_MixedReality.LocateAnchor(anchorHandle, out var position, out var rotation) == PxrResult.SUCCESS;
            if (localized) anchoredRoot.SetPositionAndRotation(position, rotation);
            else if (IsLocalized) app.Message("Room anchor tracking lost. Saved overlays are hidden.", 30);
            IsLocalized = localized; anchoredRoot.gameObject.SetActive(localized);
#endif
        }

        void ReleaseHandle()
        {
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            if (anchorHandle != 0) PXR_MixedReality.DestroyAnchor(anchorHandle);
            anchorHandle = 0; anchoredRoot = null;
#endif
        }
        void OnDestroy()
        {
            lifetime.Cancel();
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            PXR_SpatialMeshManager.MeshAdded -= OnMesh;
            PXR_SpatialMeshManager.MeshUpdated -= OnMesh;
#endif
            ReleaseHandle(); lifetime.Dispose();
        }
    }
}
