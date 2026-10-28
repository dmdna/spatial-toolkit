using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace SpatialToolkit
{
    [DefaultExecutionOrder(-100)]
    public sealed partial class SpatialToolkitApp : MonoBehaviour
    {
        public Transform TrackingOrigin;
        public Camera ViewCamera;
        public Material OverlayMaterial;
        public Material ReadoutMaterial;
        [NonSerialized] public string StorageDirectoryOverride;
        public ToolkitTool Tool { get; private set; }
        public ToolkitPreferences Preferences = new ToolkitPreferences();
        public bool Snapping { get; private set; } = true;
        public string Status { get; private set; } = "Look at your wrist to choose a tool.";
        public ToolkitSession Session { get; private set; } = new ToolkitSession();
        public Transform RoomRoot { get; private set; }
        readonly ToolkitInput input = new ToolkitInput();
        readonly ToolkitDwell dwell = new ToolkitDwell();
        readonly List<string> undo = new List<string>();
        readonly Dictionary<string, GameObject> visuals = new Dictionary<string, GameObject>();
        ToolkitPalette palette;
        ToolkitPlatform platform;
        Transform reticle;
        LineRenderer pointer, previewLine;
        Text liveReadout, levelReadout;
        ToolInput state;
        Vector3 target, targetNormal;
        bool validSurface, captureBusy, busy, roomFrameEstablished;
        float reach = 5, messageUntil, loadConfirmationUntil;
        string activePath;
        ToolkitHandle grabbed;
        string beforeGrab;
        float grabDepth;
        float grabYaw;
        Quaternion grabWorldRotation;
        Vector3 previousGrabPosition, throwVelocity;
        GameObject fitPreview, hangPreview;
        Vector3 hangPreviewFrame;
        int hangPreviewCount, hangPreviewOption;
        float hangPreviewGap, hangPreviewInset;
        Vector3 hangPreviewPosition, hangPreviewNormal;
        bool hasHangPreviewPose, menuBlocked;
        float undoHeldSince = -1, lastNodePress = -10;
        bool undoHoldFired;
        ToolkitPaletteAction undoHoldButton;
        public static bool IsPath(ToolkitItemData item) => item != null && (item.kind == ToolkitItemKind.Measurement || item.kind == ToolkitItemKind.AnglePath);
        bool PathTool => Tool == ToolkitTool.Measure || Tool == ToolkitTool.Level;
        string selectedId;
        int selectedNode = -1;
        bool hasFocus = true;
        public string InputSource => state.Source ?? "Waiting for tracking";
        public bool HandsInput => state.Gesture;
        public bool DwellInput => state.Dwell;
        public float Reach => reach;
        string StorageDirectory => string.IsNullOrEmpty(StorageDirectoryOverride) ? Application.persistentDataPath : StorageDirectoryOverride;
        string SessionPath => Path.Combine(StorageDirectory, "spatial-toolkit-room.json");
        string PreferencesPath => Path.Combine(StorageDirectory, "spatial-toolkit-settings.json");

        void Start()
        {
            Application.runInBackground = true;
            if (!ViewCamera) ViewCamera = Camera.main;
            if (!ViewCamera) { enabled = false; Debug.LogError("Spatial Toolkit needs an active camera."); return; }
            ToolkitVisuals.Template = OverlayMaterial;
            ToolkitVisuals.LabelTemplate = ReadoutMaterial;
            if (!TrackingOrigin) TrackingOrigin = ViewCamera.transform.parent ? ViewCamera.transform.parent : transform;
            try { if (File.Exists(PreferencesPath)) Preferences = JsonUtility.FromJson<ToolkitPreferences>(File.ReadAllText(PreferencesPath)); }
            catch (Exception e) { Debug.LogWarning("Preferences could not be read: " + e.Message); }
            Preferences = Preferences ?? new ToolkitPreferences();
            Preferences.colorOption = Mathf.Clamp(Preferences.colorOption, 0, 2);
            ToolkitVisuals.SetColorOption(Preferences.colorOption);
            RoomRoot = new GameObject("Room measurements").transform;
            RoomRoot.gameObject.AddComponent<ToolkitOwned>();
            var paletteObject = new GameObject("Non-dominant wrist palette", typeof(ToolkitOwned));
            palette = paletteObject.AddComponent<ToolkitPalette>(); palette.Initialize(this);
            palette.transform.position = ViewCamera.transform.position + ViewCamera.transform.forward * 0.8f;
            var pointerRoot = new GameObject("Controller pointer", typeof(ToolkitOwned), typeof(ToolkitMaterialCleanup));
            pointer = ToolkitVisuals.Line(pointerRoot.transform, new[] { Vector3.zero, Vector3.zero }, ToolkitVisuals.Mint);
            pointer.useWorldSpace = true;
            pointer.GetComponent<ToolkitReadableLine>().enabled = false;
            reticle = ToolkitVisuals.Sphere(pointerRoot.transform, Vector3.zero, 0.014f, ToolkitVisuals.Mint).transform;
            ToolkitVisuals.PointerOverlay(pointer.transform); ToolkitVisuals.PointerOverlay(reticle);
            previewLine = ToolkitVisuals.Line(pointerRoot.transform, new[] { Vector3.zero, Vector3.zero }, ToolkitVisuals.Mint);
            previewLine.useWorldSpace = true;
            liveReadout = ToolkitVisuals.Label(pointerRoot.transform, Vector3.zero, "", Color.white);
            platform = gameObject.AddComponent<ToolkitPlatform>(); platform.Initialize(this);
            Message("Aim at a mapped surface to begin.", 3);
        }

        void Update()
        {
            if (!ViewCamera || !RoomRoot || captureBusy || !hasFocus) return;
            input.LeftHanded = Preferences.leftHanded;
            input.Mode = Preferences.inputMode;
            state = input.Read(ViewCamera.transform, TrackingOrigin, ViewCamera);
            UpdateInteraction();
        }

        void CancelGrab(bool resetDwell = true)
        {
            if (grabbed) { Session = JsonUtility.FromJson<ToolkitSession>(beforeGrab); grabbed = null; RenderAll(); }
            CancelUndoHold(); CancelLoadHold(); pendingGrab = null; if (palette) palette.CancelMenuDrag(); lastNodePress = -10;
            if (resetDwell) dwell.Reset();
        }

        void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            if (!focus) CancelGrab();
            input.Reset();
        }
        void OnApplicationPause(bool paused) { OnApplicationFocus(!paused); }

        void UpdateInteraction()
        {
            if (state.SourceChanged) CancelGrab();
            palette.UpdatePose(state, ViewCamera.transform);
            pointer.gameObject.SetActive(state.Tracked && !state.Gaze && !state.Dwell && !state.MenuOnly);
            reticle.gameObject.SetActive(state.Tracked);
            if (!state.Tracked)
            {
                CancelGrab();
                previewLine.gameObject.SetActive(false); liveReadout.transform.parent.gameObject.SetActive(false);
                if (fitPreview) fitPreview.SetActive(false);
                if (hangPreview) hangPreview.SetActive(false);
                UpdateWorldHover(null);
                Status = "Tracking lost. Bring a hand into view or reconnect a controller."; return;
            }
            reach = Mathf.Clamp(reach + state.DepthDelta, 0.2f, 12);
            FindSurface();
            bool menuHit = TryPaletteAction(out var action, out var menuPoint);
            menuBlocked = menuHit;
            // Snapping changes placement only; the displayed ray always follows the input direction.
            Vector3 rayEnd = menuHit ? menuPoint : state.Ray.GetPoint(Vector3.Dot(target - state.Ray.origin, state.Ray.direction));
            pointer.SetPosition(0, state.VisualOriginTracked ? state.VisualRayOrigin : state.Ray.origin); pointer.SetPosition(1, rayEnd);
            reticle.position = rayEnd;
            var color = !validSurface ? new Color(0.65f, 0.7f, 0.76f) : ToolColor();
            reticle.GetComponent<Renderer>().sharedMaterial.color = color;
            pointer.sharedMaterial.color = new Color(color.r, color.g, color.b, 0.65f);
            if (state.Undo) UndoAction();
            if (state.ToggleSnap) Dispatch("snap");
            var handle = FindHandle();
            UpdateWorldHover(menuHit ? null : handle);
            palette.Highlight(menuHit && action.Action != "block" && action.Action != "expand" ? action : null, state.Dwell ? dwell.Progress : 0);
            UpdateUndoHold(menuHit ? action : null);
            UpdateLoadHold(menuHit ? action : null);
            bool menuDragConsumed = !busy && palette.HandleMenuDrag(state, menuHit ? action : null, menuPoint, ViewCamera.transform);
            if (palette.Dragging)
            {
                pointer.SetPosition(1, palette.DragPoint); reticle.position = palette.DragPoint;
                menuBlocked = true;
            }
            bool select = state.TriggerDown && !menuDragConsumed && !busy;
            if (state.Dwell)
            {
                string focus = busy ? null : menuHit ? (action.Action == "block" ? null : "menu:" + action.Action) : state.MenuOnly || !platform.IsLocalized ? null :
                    grabbed ? (validSurface ? "drop" : null) : handle ? "item:" + handle.ItemId + ":" + handle.Node : validSurface ? "surface" : null;
                select = dwell.Step(focus, menuHit ? menuPoint : target, Time.unscaledTime);
                state.GripHeld = grabbed;
                state.GripUp = select && grabbed && !menuHit;
            }
            else dwell.Reset();
            if (menuDragConsumed) select = false;
            reticle.localScale = Vector3.one * (0.014f + (state.Dwell ? dwell.Progress * 0.014f : 0));
            if (select && menuHit)
            {
                lastNodePress = -10;
                if (grabbed) CancelGrab(false);
                if (action.Action == "undo" && !state.Dwell)
                {
                    UndoAction(); undoHeldSince = Time.unscaledTime; undoHoldFired = false;
                    undoHoldButton = palette.GetComponentsInChildren<ToolkitPaletteAction>().FirstOrDefault(a => a.Action == "undo");
                }
                else if (action.Action == "load" && !state.Dwell)
                {
                    loadHeldSince = Time.unscaledTime; loadHoldFired = false; loadHoldButton = action;
                }
                else Dispatch(action.Action);
            }
            else if (select && !grabbed && !busy && platform.IsLocalized)
            {
                if (PathTool && TryNodeAction(handle)) { }
                else if (handle)
                {
                    SelectHandle(handle, state.Gesture && !state.Dwell);
                    if (state.Dwell) BeginGrabHandle(handle);
                }
                else
                {
                    bool doublePress = !state.Dwell && PathTool && Time.unscaledTime - lastNodePress <= 0.32f;
                    int before = Session.items.Sum(i => IsPath(i) ? i.points.Count : 0);
                    Place();
                    if (PathTool && Session.items.Sum(i => IsPath(i) ? i.points.Count : 0) > before)
                    {
                        if (doublePress) FinishPath(); else lastNodePress = Time.unscaledTime;
                    }
                }
            }
            UpdatePendingGrab(menuHit || menuDragConsumed);
            if (!state.Gesture && state.GripDown && !menuDragConsumed && !menuHit && !busy && platform.IsLocalized) BeginGrab();
            if (grabbed && menuHit && state.GripUp) { CancelGrab(); Message("Move cancelled over the menu."); }
            if (grabbed && !menuHit) UpdateGrab();
            UpdatePreview(); RefreshSelectionFeedback();
            if (Time.unscaledTime > messageUntil && !grabbed && !busy)
                Status = state.MenuOnly ? "Look at Controls to enable hands-free dwell, or bring a hand into view." :
                    validSurface ? (Tool == ToolkitTool.Fit && targetNormal.y > 0.8f && Overlaps(target + Vector3.up * (Preferences.furniture.y * 0.5f + 0.004f), Preferences.furniture, Quaternion.identity, null)
                        ? "Preview blocked: overlaps a surface." : Snapping ? "Surface found • snapping on" : "Surface found • free placement") :
                    "No mapped surface • scan the room • reach " + reach.ToString("0.0") + " m";
        }

        ToolkitHandle FindHandle()
        {
            ToolkitHandle nearest = null; float distance = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(state.Ray, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponent<ToolkitHandle>();
                if (candidate && Tool == ToolkitTool.Level && activePath != null && candidate.Node < 0 &&
                    Session.items.Any(i => i.id == candidate.ItemId && i.kind == ToolkitItemKind.Level)) continue;
                bool nearNode = candidate && candidate.Node >= 0 && nearest && nearest.Node < 0 && hit.distance < distance + 0.03f;
                bool behindNode = candidate && candidate.Node < 0 && nearest && nearest.Node >= 0 && hit.distance > distance - 0.03f;
                if (candidate && !behindNode && (hit.distance < distance || nearNode)) { nearest = candidate; distance = hit.distance; }
            }
            return nearest;
        }

        void FindSurface()
        {
            validSurface = false; target = state.Ray.GetPoint(reach); targetNormal = Vector3.up;
            var hits = Physics.RaycastAll(state.Ray, reach, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.distance >= closest || hit.collider.GetComponentInParent<ToolkitOwned>()) continue;
                closest = hit.distance; target = hit.point + hit.normal * 0.003f;
                targetNormal = hit.normal.normalized; validSurface = true;
            }
            if (!validSurface || !Snapping) return;
            foreach (var item in Session.items.Where(i => IsPath(i)))
                foreach (var point in item.points)
                {
                    var world = RoomRoot.TransformPoint(point);
                    if (Vector3.Distance(world, target) < 0.035f) { target = world; return; }
                }
            var path = Session.items.Find(i => i.id == activePath);
            var origin = path != null && path.points.Count > 0 ? RoomRoot.TransformPoint(path.points[0]) : RoomRoot.position;
            target = ToolkitGeometry.SnapOnSurface(target, origin, targetNormal, Preferences.imperial ? 0.0127f : 0.01f);
        }
        bool TryPaletteAction(out ToolkitPaletteAction action, out Vector3 point)
        {
            action = null; point = Vector3.zero;
            if (!palette.Visible) return false;
            float distance = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(state.Ray, Mathf.Max(3, reach), ~0, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponent<ToolkitPaletteAction>();
                if (candidate && hit.distance < distance) { action = candidate; distance = hit.distance; point = hit.point; }
            }
            return action;
        }
        Color ToolColor() => Tool == ToolkitTool.Fit ? ToolkitVisuals.Lavender : Tool == ToolkitTool.Hang ? ToolkitVisuals.Coral : ToolkitVisuals.Mint;

        void Place()
        {
            if (!validSurface) { Message("Find a mapped wall or floor before placing."); return; }
            if (Session.items.Count >= 100) { Message("This room has 100 items. Delete an item to add another."); return; }
            if (Selection != null) ClearSelection(false);
            if (!roomFrameEstablished)
            { if (Session.items.Count == 0) RoomRoot.position = target; roomFrameEstablished = true; }
            if (PathTool)
            {
                var path = Session.items.Find(i => i.id == activePath);
                if (Tool == ToolkitTool.Level && path == null && Session.items.Count > 98)
                { Message("This room needs space for a level grid and its path. Delete an item first."); return; }
                if (Tool == ToolkitTool.Level && Mathf.Abs(targetNormal.y) > 0.3f) { Message("Aim at a vertical wall for this tool."); return; }
                if (path != null && path.points.Count >= 128) { Message("Finish this path before adding more points."); return; }
                if (path != null && path.points.Count > 0 && Vector3.Distance(target, RoomRoot.TransformPoint(path.points[path.points.Count - 1])) < 0.005f)
                { Message("Move a little farther from the last node."); return; }
                Remember();
                if (path == null)
                {
                    path = new ToolkitItemData { kind = Tool == ToolkitTool.Level ? ToolkitItemKind.AnglePath : ToolkitItemKind.Measurement };
                    if (Tool == ToolkitTool.Level)
                    {
                        ToolkitGeometry.SurfaceBasis(targetNormal, out _, out var up);
                        var grid = new ToolkitItemData { kind = ToolkitItemKind.Level,
                            position = RoomRoot.InverseTransformPoint(target), rotation = Quaternion.Inverse(RoomRoot.rotation) * Quaternion.LookRotation(targetNormal, up) };
                        Session.items.Add(grid); path.gridId = grid.id;
                    }
                    Session.items.Add(path); activePath = path.id;
                }
                path.points.Add(RoomRoot.InverseTransformPoint(target));
                Message(path.points.Count == 1 ? "First point placed. Aim at the next point." : "Point placed. Double-press or select a node to finish.");
            }
            else if (Tool == ToolkitTool.Fit)
            {
                if (Vector3.Dot(targetNormal, Vector3.up) < 0.8f) { Message("Aim at the floor to place your furniture box."); return; }
                var center = target + Vector3.up * (Preferences.furniture.y * 0.5f + 0.004f);
                if (Overlaps(center, Preferences.furniture, Quaternion.identity, null)) { Message("The box overlaps a surface. Choose more space."); return; }
                Remember(); Session.items.Add(new ToolkitItemData { kind = ToolkitItemKind.Furniture,
                    position = RoomRoot.InverseTransformPoint(center), dimensions = Preferences.furniture });
                Message(state.Gesture ? "Furniture placed. Pinch the box and hold to move; release to drop." : "Furniture placed. Hold grip to move it.");
            }
            else
            {
                if (Mathf.Abs(Vector3.Dot(targetNormal, Vector3.up)) > 0.3f) { Message("Aim at a vertical wall for this tool."); return; }
                ToolkitGeometry.SurfaceBasis(targetNormal, out _, out var up);
                var rotation = Quaternion.LookRotation(targetNormal, up);
                Remember(); Session.items.Add(new ToolkitItemData { kind = Tool == ToolkitTool.Hang ? ToolkitItemKind.Frames : ToolkitItemKind.Level,
                    position = RoomRoot.InverseTransformPoint(target), rotation = Quaternion.Inverse(RoomRoot.rotation) * rotation,
                    dimensions = Preferences.frame, count = Preferences.frameCount, gap = Preferences.gap, inset = Preferences.inset });
                Message(Tool == ToolkitTool.Hang ? "Layout placed. Nail marks use your hanger inset." : "Level grid placed. Compare an edge with the grid.");
            }
            Haptic(); RenderAll(); palette.Rebuild();
        }

        public void Dispatch(string command)
        {
            if (busy || captureBusy) return;
            var parts = command.Split(':'); var key = parts[0];
            if (key == "block" || key == "dragmenu") return;
            if (key == "settings" || key == "about" || key == "main" || key == "minimize" || key == "expand") { CancelUndoHold(); CancelLoadHold(); palette.Navigate(key); return; }
            if (key == "num") { palette.NumericKey(parts[1]); return; }
            if (key == "edit") { palette.BeginNumeric(parts[1], GetDimension(parts[1])); return; }
            if (key == "controls") { palette.ToggleControls(); return; }
            if (key == "pin") { palette.TogglePin(); return; }
            float step = Preferences.imperial ? 0.0127f : 0.01f;
            int direction = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            if (DispatchSelectionEdit(key, direction, step)) { palette.Rebuild(); return; }
            switch (key)
            {
                case "startpath": StartPathAtSelection(); return;
                case "tool": ClearSelection(false); Tool = (ToolkitTool)direction; FinishPath(); hasHangPreviewPose = false; break;
                case "wrist": Preferences.wristGestureDisabled = !Preferences.wristGestureDisabled; palette.ResetWristPose(); break;
                case "colors": Preferences.colorOption = (Preferences.colorOption + direction + 3) % 3; ApplyColors(); break;
                case "snap": Snapping = !Snapping; Message(Snapping ? "Snapping on." : "Snapping off."); break;
                case "units": Preferences.imperial = !Preferences.imperial; RenderAll(); break;
                case "hand": Preferences.leftHanded = !Preferences.leftHanded; input.Reset(); break;
                case "input": Preferences.inputMode = (ToolkitInputMode)direction; input.Reset(); CancelGrab(); break;
                case "reach": reach = Mathf.Clamp(reach + direction * 0.5f, 0.2f, 12); break;
                case "rotate": RotateSelection(direction * 15); break;
                case "delete": DeleteSelection(); break;
                case "undo": UndoAction(); break;
                case "close": CloseArea(); break;
                case "finish": FinishPath(); break;
                case "width": Preferences.furniture.x = Mathf.Clamp(Preferences.furniture.x + direction * step, 0.05f, 10); break;
                case "height": Preferences.furniture.y = Mathf.Clamp(Preferences.furniture.y + direction * step, 0.05f, 5); break;
                case "depth": Preferences.furniture.z = Mathf.Clamp(Preferences.furniture.z + direction * step, 0.05f, 10); break;
                case "framewidth": Preferences.frame.x = Mathf.Clamp(Preferences.frame.x + direction * step, 0.05f, 3); break;
                case "frameheight": Preferences.frame.y = Mathf.Clamp(Preferences.frame.y + direction * step, 0.05f, 3); Preferences.inset = Mathf.Min(Preferences.inset, Preferences.frame.y); break;
                case "gap": Preferences.gap = Mathf.Clamp(Preferences.gap + direction * step, 0, 1); break;
                case "inset": Preferences.inset = Mathf.Clamp(Preferences.inset + direction * step, 0, Preferences.frame.y); break;
                case "count": Preferences.frameCount = Mathf.Clamp(Preferences.frameCount + direction, 1, 8); break;
                case "save": SaveRoom(); return;
                case "load":
                    if (Session.items.Count > 0 && Time.unscaledTime > loadConfirmationUntil)
                    { loadConfirmationUntil = Time.unscaledTime + 6; Message("Load replaces this workspace. Save first, or press Load again.", 6); return; }
                    LoadRoom(); return;
                case "snapshot": StartCoroutine(Capture()); return;
            }
            palette.Rebuild();
            try { File.WriteAllText(PreferencesPath, JsonUtility.ToJson(Preferences)); }
            catch (Exception e) { Debug.LogWarning("Settings not saved: " + e.Message); }
        }

        void DeleteSelection()
        {
            var item = Session.items.Find(i => i.id == selectedId);
            if (item == null) { Message("Select an item first: pinch it and release, or dwell on it."); return; }
            Remember();
            RemoveNodeOrItem(item, selectedNode);
            ClearSelection(false); RenderAll(); palette.Rebuild(); Message("Deleted. Undo brings it back.");
        }

        void RotateSelection(float degrees)
        {
            var item = Session.items.Find(i => i.id == selectedId);
            if (item == null || item.kind != ToolkitItemKind.Furniture) { Message("Select a furniture box to rotate it."); return; }
            var rotation = Quaternion.Inverse(RoomRoot.rotation) * Quaternion.AngleAxis(degrees, Vector3.up) * RoomRoot.rotation * item.rotation;
            if (Overlaps(RoomRoot.TransformPoint(item.position), item.dimensions, RoomRoot.rotation * rotation, visuals[item.id].transform))
            { Message("Rotation would overlap a surface."); return; }
            Remember(); item.rotation = rotation; RenderAll(); Message("Rotated 15 degrees.");
        }
        float GetDimension(string key)
        {
            switch (key)
            {
                case "width": return FurnitureDimensions.x;
                case "height": return FurnitureDimensions.y;
                case "depth": return FurnitureDimensions.z;
                case "framewidth": return FrameDimensions.x;
                case "frameheight": return FrameDimensions.y;
                case "gap": return FrameGap;
                default: return FrameInset;
            }
        }
        public bool SetDimension(string key, float meters)
        {
            float min = key == "gap" || key == "inset" ? 0 : 0.05f;
            float max = key == "gap" ? 1 : key == "inset" ? FrameDimensions.y : key.StartsWith("frame") ? 3 : key == "height" ? 5 : 10;
            if (float.IsNaN(meters) || float.IsInfinity(meters) || meters < min || meters > max)
            { Message("Enter a value between " + ToolkitGeometry.Distance(min, Preferences.imperial) + " and " + ToolkitGeometry.Distance(max, Preferences.imperial) + "."); return false; }
            if (Selection != null && (Selection.kind == ToolkitItemKind.Furniture || Selection.kind == ToolkitItemKind.Frames))
                return EditSelectedDimension(key, meters);
            switch (key)
            {
                case "width": Preferences.furniture.x = meters; break;
                case "height": Preferences.furniture.y = meters; break;
                case "depth": Preferences.furniture.z = meters; break;
                case "framewidth": Preferences.frame.x = meters; break;
                case "frameheight": Preferences.frame.y = meters; Preferences.inset = Mathf.Min(Preferences.inset, meters); break;
                case "gap": Preferences.gap = meters; break;
                case "inset": Preferences.inset = meters; break;
                default: return false;
            }
            try { File.WriteAllText(PreferencesPath, JsonUtility.ToJson(Preferences)); }
            catch (Exception e) { Debug.LogWarning("Settings not saved: " + e.Message); }
            Message("Dimension updated."); return true;
        }

        void CloseArea()
        {
            var path = Session.items.Find(i => i.id == activePath);
            string reason = "Start a measurement first.";
            if (path == null || path.kind != ToolkitItemKind.Measurement || !ToolkitGeometry.TryArea(path.points, out _, out reason))
            { Message(path == null ? "Start a measurement first." : reason); return; }
            Remember(); path.closed = true; activePath = null; RenderAll(); Message("Area closed. Grab any node to adjust it.");
        }
        void FinishPath()
        {
            activePath = null; lastNodePress = -10; palette.Rebuild(); Message("Path finished. Select a surface to start a new measurement.");
        }
        void Remember()
        {
            undo.Add(JsonUtility.ToJson(Session)); if (undo.Count > 50) undo.RemoveAt(0);
        }
        public void UndoAction()
        {
            if (undo.Count == 0 || busy || grabbed) { Message("Nothing to undo."); return; }
            Session = JsonUtility.FromJson<ToolkitSession>(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1);
            var last = Session.items.LastOrDefault();
            activePath = IsPath(last) && !last.closed &&
                (Tool == ToolkitTool.Measure && last.kind == ToolkitItemKind.Measurement || Tool == ToolkitTool.Level && last.kind == ToolkitItemKind.AnglePath) ? last.id : null;
            if (Selection == null) { selectedId = null; selectedNode = -1; }
            else if (IsPath(Selection)) selectedNode = Mathf.Clamp(selectedNode, 0, Selection.points.Count - 1);
            RenderAll(); palette.Rebuild(); lastNodePress = -10; Message("Undone. Hold the button to Clear All.");
        }

        void BeginGrab() { BeginGrabHandle(FindHandle()); }
        void BeginGrabHandle(ToolkitHandle handle)
        {
            if (!handle) return;
            SelectHandle(handle, false);
            var linked = Session.items.Find(i => i.kind == ToolkitItemKind.Level && i.id == handle.ItemId);
            if (linked != null && Selection != null && Selection.kind == ToolkitItemKind.AnglePath)
                handle = visuals[Selection.id].GetComponentsInChildren<ToolkitHandle>().First(h => h.Node == 0);
            grabbed = handle;
            float distance = Mathf.Max(0.05f, Vector3.Dot(handle.transform.position - state.Ray.origin, state.Ray.direction));
            beforeGrab = JsonUtility.ToJson(Session); grabDepth = distance; throwVelocity = Vector3.zero;
            grabYaw = Mathf.Atan2(state.Forward.x, state.Forward.z) * Mathf.Rad2Deg;
            var item = Session.items.Find(i => i.id == grabbed.ItemId);
            grabWorldRotation = RoomRoot.rotation * item.rotation;
            previousGrabPosition = state.Ray.GetPoint(grabDepth); Haptic();
        }
        void UpdateGrab()
        {
            var item = Session.items.Find(i => i.id == grabbed.ItemId);
            if (item == null) { grabbed = null; return; }
            string id = grabbed.ItemId; int node = grabbed.Node;
            Vector3 point = validSurface ? target : state.Ray.GetPoint(grabDepth);
            bool behind = Vector3.Dot(point - ViewCamera.transform.position, ViewCamera.transform.forward) < -0.15f;
            throwVelocity = Vector3.Lerp(throwVelocity, (point - previousGrabPosition) / Mathf.Max(Time.unscaledDeltaTime, 0.001f), 0.35f);
            previousGrabPosition = point;
            if (IsPath(item) && node >= 0)
            {
                item.points[node] = RoomRoot.InverseTransformPoint(point);
                if (node == 0 && item.kind == ToolkitItemKind.AnglePath)
                {
                    var grid = Session.items.Find(i => i.id == item.gridId);
                    if (grid != null) grid.position = item.points[0];
                }
            }
            else
            {
                if (item.kind == ToolkitItemKind.Furniture)
                {
                    point += Vector3.up * item.dimensions.y * 0.5f;
                    float yaw = Mathf.DeltaAngle(grabYaw, Mathf.Atan2(state.Forward.x, state.Forward.z) * Mathf.Rad2Deg);
                    if (Snapping) yaw = Mathf.Round(yaw / 15) * 15;
                    item.rotation = Quaternion.Inverse(RoomRoot.rotation) * Quaternion.AngleAxis(yaw, Vector3.up) * grabWorldRotation;
                }
                item.position = RoomRoot.InverseTransformPoint(point);
                if (item.kind == ToolkitItemKind.Frames || item.kind == ToolkitItemKind.Level)
                {
                    if (validSurface && Mathf.Abs(targetNormal.y) < 0.3f)
                    {
                        ToolkitGeometry.SurfaceBasis(targetNormal, out _, out var up);
                        item.rotation = Quaternion.Inverse(RoomRoot.rotation) * Quaternion.LookRotation(targetNormal, up);
                    }
                }
            }
            bool blocked = item.kind == ToolkitItemKind.Furniture && Overlaps(RoomRoot.TransformPoint(item.position), item.dimensions,
                RoomRoot.rotation * item.rotation, visuals.TryGetValue(id, out var body) ? body.transform : null);
            bool acceptable = validSurface && (item.kind != ToolkitItemKind.Furniture || targetNormal.y > 0.8f) &&
                ((item.kind != ToolkitItemKind.Frames && item.kind != ToolkitItemKind.Level) || Mathf.Abs(targetNormal.y) < 0.3f);
            Status = behind ? "Release with a throw to delete." : blocked ? "Overlaps a surface • move to clear space" : "Release to place • throw behind you to delete";
            if (state.GripUp)
            {
                bool remove = behind && throwVelocity.magnitude > 1.1f;
                if (remove)
                {
                    RemoveNodeOrItem(item, node); ClearSelection(false);
                    undo.Add(beforeGrab); Message("Deleted. Undo brings it back.");
                }
                else if (!acceptable || blocked)
                { Session = JsonUtility.FromJson<ToolkitSession>(beforeGrab); Message("Move cancelled. Choose a clear mapped surface."); }
                else { undo.Add(beforeGrab); Message("Placed."); }
                if (undo.Count > 50) undo.RemoveAt(0);
                grabbed = null; RenderAll(); palette.Rebuild(); return;
            }
            RefreshMovedItem(item, blocked);
        }

        void RefreshMovedItem(ToolkitItemData item, bool blocked)
        {
            var root = visuals[item.id];
            if (item.kind == ToolkitItemKind.AnglePath && visuals.TryGetValue(item.gridId ?? "", out var gridVisual))
                gridVisual.transform.localPosition = item.points[0];
            if (!IsPath(item))
            {
                root.transform.localPosition = item.position; root.transform.localRotation = item.rotation;
                if (item.kind == ToolkitItemKind.Furniture)
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        if (renderer.GetComponent<ToolkitLineOutline>()) continue;
                        Color color = blocked ? ToolkitVisuals.Coral : ToolkitVisuals.Lavender;
                        if (!(renderer is LineRenderer)) color.a = 0.12f;
                        renderer.sharedMaterial.color = color;
                    }
                return;
            }
            foreach (var handle in root.GetComponentsInChildren<ToolkitHandle>()) handle.transform.localPosition = item.points[handle.Node];
            var line = root.GetComponentInChildren<LineRenderer>();
            if (line) for (int i = 0; i < item.points.Count; i++) line.SetPosition(i, item.points[i]);
            var labels = root.GetComponentsInChildren<Text>(); int labelIndex = 0;
            for (int i = 1; i < item.points.Count; i++) RefreshSegment(labels[labelIndex++], item.points[i - 1], item.points[i], item.kind == ToolkitItemKind.AnglePath);
            if (item.closed && item.points.Count > 2)
            {
                RefreshSegment(labels[labelIndex++], item.points[item.points.Count - 1], item.points[0]);
                bool valid = ToolkitGeometry.TryArea(item.points, out var area, out var reason);
                var areaLabel = labels[labelIndex]; areaLabel.text = valid ? ToolkitGeometry.Area(area, Preferences.imperial) : reason;
                areaLabel.color = valid ? ToolkitVisuals.Mint : ToolkitVisuals.Coral;
                areaLabel.transform.parent.localPosition = item.points.Aggregate(Vector3.zero, (a, b) => a + b) / item.points.Count + Vector3.up * 0.07f;
            }
        }
        void RefreshSegment(Text label, Vector3 a, Vector3 b, bool angles = false)
        {
            label.text = SegmentText(a, b, angles);
            label.transform.parent.localPosition = (a + b) * 0.5f + Vector3.up * 0.04f;
        }

        bool Overlaps(Vector3 center, Vector3 dimensions, Quaternion rotation, Transform ignore)
        {
            var half = Vector3.Max(dimensions * 0.5f - Vector3.one * 0.008f, Vector3.one * 0.001f);
            foreach (var collider in Physics.OverlapBox(center, half, rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ignore && collider.transform.IsChildOf(ignore)) continue;
                if (collider.GetComponentInParent<ToolkitOwned>() && !collider.GetComponent<ToolkitFurniture>()) continue;
                return true;
            }
            return false;
        }

        void RenderAll()
        {
            foreach (Transform child in RoomRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            selectionVisuals.Clear(); hoverId = null; hoverNode = -1;
            visuals.Clear(); levelReadout = null; EnsureLevelLinks();
            if (platform && platform.IsLocalized) RoomRoot.gameObject.SetActive(true);
            foreach (var item in Session.items) RenderItem(item);
        }
        void RenderItem(ToolkitItemData item, bool blocked = false)
        {
            selectionVisuals.RemoveAll(v => !v || v.ItemId == item.id);
            if (visuals.TryGetValue(item.id, out var previous)) { previous.SetActive(false); Destroy(previous); }
            var root = new GameObject(item.kind.ToString(), typeof(ToolkitOwned), typeof(ToolkitMaterialCleanup));
            root.transform.SetParent(RoomRoot, false); root.transform.localPosition = item.position; root.transform.localRotation = item.rotation;
            visuals[item.id] = root;
            if (IsPath(item))
            {
                root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity;
                if (item.points.Count > 1) ToolkitVisuals.Line(root.transform, item.points, ToolkitVisuals.Mint, 0.002f, item.closed);
                for (int i = 0; i < item.points.Count; i++)
                {
                    var node = ToolkitVisuals.Sphere(root.transform, item.points[i], 0.02f, ToolkitVisuals.Mint, true);
                    node.GetComponent<SphereCollider>().radius = 0.75f;
                    var handle = node.AddComponent<ToolkitHandle>(); handle.ItemId = item.id; handle.Node = i;
                    if (i > 0) SegmentLabel(root.transform, item.points[i - 1], item.points[i], item.kind == ToolkitItemKind.AnglePath);
                }
                if (item.closed && item.points.Count > 2)
                {
                    SegmentLabel(root.transform, item.points[item.points.Count - 1], item.points[0]);
                    bool valid = ToolkitGeometry.TryArea(item.points, out var area, out var reason);
                    var center = item.points.Aggregate(Vector3.zero, (a, b) => a + b) / item.points.Count;
                    ToolkitVisuals.Label(root.transform, center + Vector3.up * 0.07f, valid ? ToolkitGeometry.Area(area, Preferences.imperial) : reason, valid ? ToolkitVisuals.Mint : ToolkitVisuals.Coral, 32);
                }
            }
            else if (item.kind == ToolkitItemKind.Furniture)
            {
                Color color = blocked ? ToolkitVisuals.Coral : ToolkitVisuals.Lavender;
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.SetParent(root.transform, false);
                body.transform.localScale = item.dimensions; Destroy(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = ToolkitVisuals.Material(new Color(color.r, color.g, color.b, 0.12f));
                ToolkitVisuals.BoxEdges(root.transform, item.dimensions, color);
                var collider = root.AddComponent<BoxCollider>(); collider.size = item.dimensions;
                root.AddComponent<Rigidbody>().isKinematic = true; root.AddComponent<ToolkitFurniture>();
                root.AddComponent<ToolkitHandle>().ItemId = item.id;
                ToolkitVisuals.Label(root.transform, Vector3.up * (item.dimensions.y * 0.5f + 0.08f),
                    "FIT CHECK\n" + ToolkitGeometry.Distance(item.dimensions.x, Preferences.imperial) + " × " + ToolkitGeometry.Distance(item.dimensions.y, Preferences.imperial) + " × " + ToolkitGeometry.Distance(item.dimensions.z, Preferences.imperial), color);
            }
            else if (item.kind == ToolkitItemKind.Frames)
            {
                DrawFrames(root.transform, item.count, item.dimensions, item.gap, item.inset);
                var size = new Vector3(item.count * item.dimensions.x + (item.count - 1) * item.gap, item.dimensions.y, 0.018f);
                root.AddComponent<BoxCollider>().size = size; root.AddComponent<ToolkitHandle>().ItemId = item.id;
            }
            else
            {
                for (int i = -6; i <= 6; i++)
                {
                    float x = i * 0.1f; Color color = i == 0 ? ToolkitVisuals.Mint : ToolkitVisuals.WithAlpha(ToolkitVisuals.Mint, ToolkitVisuals.ColorOption == 2 ? 0.65f : 0.3f);
                    ToolkitVisuals.Line(root.transform, new[] { new Vector3(x, -0.6f, 0), new Vector3(x, 0.6f, 0) }, color, i == 0 ? 0.003f : 0.0015f);
                    ToolkitVisuals.Line(root.transform, new[] { new Vector3(-0.6f, x, 0), new Vector3(0.6f, x, 0) }, color, i == 0 ? 0.003f : 0.0015f);
                }
                root.AddComponent<BoxCollider>().size = new Vector3(1.2f, 1.2f, 0.015f);
                root.AddComponent<ToolkitHandle>().ItemId = item.id;
                levelReadout = ToolkitVisuals.Label(root.transform, Vector3.up * 0.7f, "Align controller with edge", ToolkitVisuals.Mint);
            }
            foreach (var handle in root.GetComponentsInChildren<ToolkitHandle>()) RegisterSelectionVisual(handle, item);
        }
        void SegmentLabel(Transform parent, Vector3 a, Vector3 b, bool angles = false) => ToolkitVisuals.Label(parent, (a + b) * 0.5f + Vector3.up * 0.04f,
            SegmentText(a, b, angles), Color.white);
        string SegmentText(Vector3 a, Vector3 b, bool angles = false)
        {
            if (!angles) return ToolkitGeometry.Distance(Vector3.Distance(a, b), Preferences.imperial);
            Vector3 axis = RoomRoot.TransformDirection(b - a);
            return "Horizontal " + ToolkitGeometry.LevelError(axis).ToString("0.0") + "°\nVertical " + ToolkitGeometry.PlumbError(axis).ToString("0.0") + "°";
        }

        void UpdatePreview()
        {
            var path = Session.items.Find(i => i.id == activePath);
            bool live = PathTool && path != null && path.points.Count > 0 && validSurface && !grabbed && !menuBlocked && Selection == null;
            previewLine.gameObject.SetActive(live); liveReadout.transform.parent.gameObject.SetActive(live);
            if (live)
            {
                var last = RoomRoot.TransformPoint(path.points[path.points.Count - 1]);
                previewLine.SetPosition(0, last); previewLine.SetPosition(1, target);
                liveReadout.transform.parent.position = (last + target) * 0.5f + Vector3.up * 0.06f;
                liveReadout.text = SegmentText(RoomRoot.InverseTransformPoint(last), RoomRoot.InverseTransformPoint(target), Tool == ToolkitTool.Level);
            }
            bool fit = Tool == ToolkitTool.Fit && validSurface && targetNormal.y > 0.8f && !grabbed && !menuBlocked && Selection == null;
            if (fit && !fitPreview)
            {
                fitPreview = new GameObject("Furniture preview", typeof(ToolkitOwned), typeof(ToolkitMaterialCleanup));
                ToolkitVisuals.BoxEdges(fitPreview.transform, Vector3.one, ToolkitVisuals.Lavender);
            }
            if (fitPreview)
            {
                fitPreview.SetActive(fit); fitPreview.transform.position = target + Vector3.up * (Preferences.furniture.y * 0.5f + 0.004f);
                fitPreview.transform.localScale = Preferences.furniture;
                bool blocked = fit && Overlaps(fitPreview.transform.position, Preferences.furniture, Quaternion.identity, null);
                foreach (var line in fitPreview.GetComponentsInChildren<LineRenderer>())
                    if (!line.GetComponent<ToolkitLineOutline>()) line.sharedMaterial.color = blocked ? ToolkitVisuals.Coral : ToolkitVisuals.Lavender;
            }
            UpdateHangPreview();
            if (levelReadout)
            {
                if (!state.AlignmentTracked)
                {
                    levelReadout.text = "Gravity-aligned grid\nUse the grid to compare the edge";
                    levelReadout.color = ToolkitVisuals.Mint; return;
                }
                float horizontal = ToolkitGeometry.LevelError(state.AlignmentForward), vertical = ToolkitGeometry.PlumbError(state.AlignmentForward);
                float error = Mathf.Min(horizontal, vertical);
                levelReadout.text = (horizontal < vertical ? "LEVEL  " : "PLUMB  ") + error.ToString("0.0") + (state.Gesture ? "°\nIndex-finger alignment (approximate)" : "°\nController alignment");
                levelReadout.color = error <= 1 ? ToolkitVisuals.Mint : ToolkitVisuals.Coral;
            }
        }

        void DrawFrames(Transform parent, int count, Vector3 dimensions, float gap, float inset)
        {
                var nails = ToolkitGeometry.NailPositions(count, dimensions.x, dimensions.y, gap, inset);
                for (int i = 0; i < nails.Length; i++)
                {
                    float x = nails[i].x, w = dimensions.x * 0.5f, h = dimensions.y * 0.5f;
                    ToolkitVisuals.Line(parent, new[] { new Vector3(x-w,-h,0), new Vector3(x-w,h,0), new Vector3(x+w,h,0), new Vector3(x+w,-h,0) }, ToolkitVisuals.Coral, 0.002f, true);
                    var nail = new Vector3(x, nails[i].y, 0.004f);
                    ToolkitVisuals.Line(parent, new[] { nail - Vector3.right * 0.012f, nail + Vector3.right * 0.012f }, Color.white);
                    ToolkitVisuals.Line(parent, new[] { nail - Vector3.up * 0.012f, nail + Vector3.up * 0.012f }, Color.white);
                    ToolkitVisuals.Label(parent, nail + Vector3.up * 0.045f, "Nail " + (i + 1), ToolkitVisuals.Coral, 20);
                }
        }
        void UpdateHangPreview()
        {
            if (Tool == ToolkitTool.Hang && !grabbed && !menuBlocked && validSurface && Mathf.Abs(targetNormal.y) <= 0.3f)
            { hangPreviewPosition = target; hangPreviewNormal = targetNormal; hasHangPreviewPose = true; }
            bool visible = Selection == null && Tool == ToolkitTool.Hang && !grabbed && hasHangPreviewPose && (menuBlocked || validSurface && Mathf.Abs(targetNormal.y) <= 0.3f);
            bool changed = hangPreviewFrame != Preferences.frame || hangPreviewCount != Preferences.frameCount ||
                hangPreviewGap != Preferences.gap || hangPreviewInset != Preferences.inset || hangPreviewOption != Preferences.colorOption;
            if (visible && (!hangPreview || changed))
            {
                if (hangPreview) { hangPreview.SetActive(false); Destroy(hangPreview); }
                hangPreview = new GameObject("Hang layout preview", typeof(ToolkitOwned), typeof(ToolkitMaterialCleanup));
                DrawFrames(hangPreview.transform, Preferences.frameCount, Preferences.frame, Preferences.gap, Preferences.inset);
                hangPreviewFrame = Preferences.frame; hangPreviewCount = Preferences.frameCount;
                hangPreviewGap = Preferences.gap; hangPreviewInset = Preferences.inset; hangPreviewOption = Preferences.colorOption;
            }
            if (hangPreview)
            {
                hangPreview.SetActive(visible);
                ToolkitGeometry.SurfaceBasis(hangPreviewNormal, out _, out var up);
                hangPreview.transform.SetPositionAndRotation(hangPreviewPosition, Quaternion.LookRotation(hangPreviewNormal, up));
            }
        }
        void CancelUndoHold()
        {
            undoHeldSince = -1; undoHoldFired = false; undoHoldButton = null;
            if (palette) palette.UndoHold(0, false);
        }
        void UpdateUndoHold(ToolkitPaletteAction action)
        {
            if (undoHeldSince < 0) return;
            if (!state.TriggerHeld || state.Dwell || !action || action != undoHoldButton || action.Action != "undo" || busy || !hasFocus)
            { CancelUndoHold(); return; }
            if (undoHoldFired) return;
            float held = Time.unscaledTime - undoHeldSince;
            float progress = Mathf.Clamp01((held - 0.45f) / 1.2f);
            palette.UndoHold(progress, held >= 0.45f);
            if (progress >= 1)
            {
                ClearAll(); undoHoldFired = true; palette.UndoHold(0, false);
            }
        }
        public void ClearAll()
        {
            if (busy || captureBusy || grabbed) return;
            if (Session.items.Count > 0) Remember();
            platform.DetachRoom(); Session.anchorUuid = null; pendingGrab = null; Session.items.Clear(); activePath = selectedId = null; selectedNode = -1;
            lastNodePress = -10; hasHangPreviewPose = false; RenderAll(); palette.Rebuild(); Haptic();
            Message("All nodes and measurements cleared. Undo brings them back.");
        }
        void ApplyColors()
        {
            ToolkitVisuals.SetColorOption(Preferences.colorOption); RenderAll();
            if (fitPreview) { Destroy(fitPreview); fitPreview = null; }
            if (hangPreview) { Destroy(hangPreview); hangPreview = null; }
            previewLine.sharedMaterial.color = ToolkitVisuals.Mint;
            liveReadout.color = Color.white;
        }

        public void Message(string message, float seconds = 4) { Status = message; messageUntil = Time.unscaledTime + seconds; }
        void Haptic()
        {
            var device = InputDevices.GetDeviceAtXRNode(Preferences.leftHanded ? XRNode.LeftHand : XRNode.RightHand);
            if (device.TryGetHapticCapabilities(out var capability) && capability.supportsImpulse) device.SendHapticImpulse(0, 0.25f, 0.035f);
        }
        async void SaveRoom()
        {
            if (Session.items.Count == 0) { Message("Place a measurement or object first."); return; }
            busy = true; Message("Saving this room…", 30);
            try
            {
                var uuid = await platform.SaveAnchor(RoomRoot);
                if (uuid == null) return;
                Session.anchorUuid = uuid; Session.simulated = Application.isEditor;
                if (Session.simulated) { Session.previewOrigin = RoomRoot.position; Session.previewRotation = RoomRoot.rotation; }
                string temporary = SessionPath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(Session, true));
                if (File.Exists(SessionPath)) File.Replace(temporary, SessionPath, SessionPath + ".bak");
                else File.Move(temporary, SessionPath);
                RenderAll(); roomFrameEstablished = true;
                Message(Application.isEditor ? "Editor session saved • simulated room coordinates" : "Room and spatial anchor saved.");
            }
            catch (Exception e) { Message("Room could not be saved. Try again."); Debug.LogException(e); }
            finally { busy = false; }
        }
        async void LoadRoom()
        {
            if (!File.Exists(SessionPath)) { Message("No saved room yet."); return; }
            busy = true; Message("Finding the saved room anchor…", 30);
            try
            {
                var saved = JsonUtility.FromJson<ToolkitSession>(File.ReadAllText(SessionPath));
                ValidateSession(saved);
                if (!Application.isEditor && saved.simulated) { Message("This is an editor-only room. Create a room on your headset."); return; }
                if (!await platform.RestoreAnchor(saved.anchorUuid, RoomRoot)) return;
                if (saved.simulated) RoomRoot.SetPositionAndRotation(saved.previewOrigin, saved.previewRotation);
                undo.Clear(); Session = saved; roomFrameEstablished = true; activePath = null; ClearSelection(false); RenderAll(); palette.Rebuild(); Message("Saved room restored. Hold the button to Delete Saved Room.");
            }
            catch (Exception e) { Message("Saved room could not be loaded."); Debug.LogException(e); }
            finally { busy = false; }
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        public static void ValidateSession(ToolkitSession saved)
        {
            if (saved == null || saved.version != 1 || saved.items == null || saved.items.Count > 100)
                throw new InvalidDataException("Unsupported saved room.");
            var ids = new HashSet<string>();
            foreach (var item in saved.items)
            {
                if (item == null || string.IsNullOrEmpty(item.id) || !ids.Add(item.id) || !Enum.IsDefined(typeof(ToolkitItemKind), item.kind) ||
                    !Finite(item.position) || !Finite(item.dimensions) || !Finite(item.rotation.x) || !Finite(item.rotation.y) || !Finite(item.rotation.z) || !Finite(item.rotation.w) ||
                    item.rotation.x * item.rotation.x + item.rotation.y * item.rotation.y + item.rotation.z * item.rotation.z + item.rotation.w * item.rotation.w < 0.5f)
                    throw new InvalidDataException("Saved item has invalid coordinates.");
                if (IsPath(item) && (item.points == null || item.points.Count == 0 || item.points.Count > 128 || item.points.Any(p => !Finite(p))))
                    throw new InvalidDataException("Invalid saved measurement.");
                if (item.kind == ToolkitItemKind.Furniture && (item.dimensions.x < 0.05f || item.dimensions.y < 0.05f || item.dimensions.z < 0.05f))
                    throw new InvalidDataException("Invalid furniture dimensions.");
                if (item.kind == ToolkitItemKind.Frames)
                    ToolkitGeometry.NailPositions(item.count, item.dimensions.x, item.dimensions.y, item.gap, item.inset);
            }
            if (saved.simulated && !Finite(saved.previewOrigin)) throw new InvalidDataException("Invalid editor room origin.");
        }
        IEnumerator Capture()
        {
            captureBusy = true; palette.gameObject.SetActive(false); pointer.transform.parent.gameObject.SetActive(false);
            CancelUndoHold();
            if (fitPreview) fitPreview.SetActive(false);
            if (hangPreview) hangPreview.SetActive(false);
            yield return new WaitForEndOfFrame();
            var capture = platform.CaptureSnapshot();
            while (!capture.IsCompleted) yield return null;
            palette.gameObject.SetActive(true); pointer.transform.parent.gameObject.SetActive(true); captureBusy = false;
            if (capture.IsFaulted) { Message("Snapshot failed. Use the headset's capture control."); Debug.LogException(capture.Exception); }
            else Message(capture.Result, 7);
        }
        void OnDestroy()
        {
            input.Dispose();
            if (RoomRoot) Destroy(RoomRoot.gameObject);
            if (palette) Destroy(palette.gameObject);
            if (pointer) Destroy(pointer.transform.parent.gameObject);
            if (fitPreview) Destroy(fitPreview);
            if (hangPreview) Destroy(hangPreview);
        }
    }
    public sealed class ToolkitFurniture : MonoBehaviour { }
}
