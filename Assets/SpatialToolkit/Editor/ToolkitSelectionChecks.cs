using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class ToolkitFeatureChecks
{
    static void Invoke(string method, params object[] args) => typeof(SpatialToolkitApp).GetMethod(method, Private).Invoke(app, args);
    static void FreshItems()
    {
        Set("grabbed", null); Set("pendingGrab", null); Set("activePath", null); Set("selectedId", null); Set("selectedNode", -1);
        app.Session.items.Clear(); Call("RenderAll"); palette.Navigate("main");
    }
    static ToolkitItemData AddPath(ToolkitItemKind kind, params Vector3[] world)
    {
        var path = new ToolkitItemData { kind = kind, points = world.Select(app.RoomRoot.InverseTransformPoint).ToList() };
        app.Session.items.Add(path);
        if (kind == ToolkitItemKind.AnglePath)
        {
            var grid = new ToolkitItemData { kind = ToolkitItemKind.Level, position = path.points[0] };
            app.Session.items.Add(grid); path.gridId = grid.id;
        }
        Call("RenderAll"); return path;
    }
    static void CheckEdit(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        checks++; File.AppendAllText(".tools/checks/toolkit-selection.txt", "PASS: " + label + "\n");
    }
    static void Select(ToolkitItemData item, int node = -1)
    {
        var handle = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().First(h => h.ItemId == item.id && h.Node == node);
        Invoke("SelectHandle", handle, false);
    }
    static void MenuSample(ToolInput sample, ToolkitPaletteAction hit, Vector3 point)
    { palette.HandleMenuDrag(sample, hit, point, app.ViewCamera.transform); }

    [MenuItem("PICO/Spatial Toolkit/Run Selection and Menu Drag Checks")]
    public static string RunSelectionChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        if (!EditorApplication.isPlaying || !app || app.Session.items.Count != 0) throw new Exception("Start in an empty fresh Play Mode workbench.");
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        app.SendMessage("OnApplicationFocus", true);
        var preferences = JsonUtility.ToJson(app.Preferences); string storage = app.StorageDirectoryOverride;
        bool enabled = app.enabled; app.enabled = false;
        Directory.CreateDirectory(".tools/checks/selection-session"); app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/selection-session");
        File.WriteAllText(".tools/checks/toolkit-selection.txt", "Selection, closure, hand ray, and menu dragging checks\n"); checks = 0;
        try
        {
            app.Preferences.wristGestureDisabled = true; Aim(new Vector3(1, 1, 2.49f)); app.Dispatch("pin");
            var head = app.ViewCamera.transform;
            palette.transform.SetPositionAndRotation(head.position + head.right * 0.9f + head.forward * 0.8f, Quaternion.LookRotation(head.right * 0.9f + head.forward * 0.8f));
            var a = new Vector3(-1, 0.7f, 2.497f); var b = a + Vector3.right; var c = b + Vector3.up; var d = a + Vector3.up;
            FreshItems(); app.Dispatch("tool:0");
            var path = AddPath(ToolkitItemKind.Measurement, a, b, c, d); Set("activePath", path.id); Aim(a, true, gesture: true);
            CheckEdit(path.closed && Active == null && path.points.Count == 4, "Returning to the first non-adjacent node closes a valid Measure area");
            Select(path, 0);
            CheckEdit(Button("startpath") && Button("delete") && !palette.GetComponentsInChildren<ToolkitPaletteAction>().Any(i => i.Action == "close"), "Finished Measure node displays Start new path and Delete");
            app.Dispatch("startpath"); var branch = app.Selection;
            CheckEdit(Active == branch.id && branch.id != path.id && branch.points.Count == 1 && branch.points[0] == path.points[0] && path.closed && Button("finish"), "Start new path branches from the selected node without altering its closed source");
            app.Dispatch("finish"); CheckEdit(Button("startpath"), "Finishing a branch restores Start new path");
            app.Dispatch("startpath"); Aim(a, true);
            CheckEdit(Active == null && !app.Selection.closed, "A branch's shared origin resolves to its current node and finishes instead of closing the source");
            FreshItems(); app.Dispatch("tool:0"); path = AddPath(ToolkitItemKind.Measurement, a, b, c, d); Set("activePath", path.id); Aim(d, true);
            CheckEdit(Active == null && !path.closed && app.SelectedNode == 3, "Selecting the current node finishes and selects it");
            Set("activePath", path.id); Aim(c, true);
            CheckEdit(Active == null && !path.closed, "Selecting the previous node finishes without closing an area");
            Set("activePath", path.id); Aim(b, true);
            CheckEdit(path.closed && path.points.Count == 3 && app.Session.items.Count == 2 && app.Session.items.Any(i => !i.closed && i.points.SequenceEqual(new[] { app.RoomRoot.InverseTransformPoint(a), app.RoomRoot.InverseTransformPoint(b) })), "Closing at an interior node closes the suffix and preserves its earlier segment");
            FreshItems(); app.Dispatch("tool:0"); path = AddPath(ToolkitItemKind.Measurement, a, b, b + Vector3.right); Set("activePath", path.id); Aim(a, true);
            CheckEdit(Active == path.id && !path.closed && path.points.Count == 3, "An invalid collinear closure stays active rather than silently finishing");
            FreshItems(); path = AddPath(ToolkitItemKind.Measurement, a, b); var other = AddPath(ToolkitItemKind.Measurement, c); Set("activePath", path.id); Aim(c, true);
            CheckEdit(path.closed && path.points.Count == 3 && app.Session.items.Count == 2, "A valid closure through another existing path's node closes at that selected node");
            Select(path, 1); app.Dispatch("delete");
            CheckEdit(path.points.Count == 2 && !path.closed && app.Selection == null, "Deleting a selected Measure node opens its area"); app.UndoAction();
            CheckEdit(app.Session.items.First(i => i.id == path.id).closed, "Node deletion and area reopening are undoable");

            FreshItems(); app.Dispatch("tool:3"); var angle = AddPath(ToolkitItemKind.AnglePath, a, b, c, d); string gridId = angle.gridId;
            Select(angle, 2); app.Dispatch("delete");
            CheckEdit(angle.points.Count == 3 && angle.points[2] == app.RoomRoot.InverseTransformPoint(d) && app.Session.items.Any(i => i.id == gridId), "Deleting an intermediate Level node reconnects the remaining sequence and retains its grid");
            Select(angle, 1); app.Dispatch("startpath");
            CheckEdit(app.Selection.kind == ToolkitItemKind.AnglePath && !string.IsNullOrEmpty(app.Selection.gridId) && app.Selection.gridId != gridId, "A new Level path has its own grid and starts at the selected node");
            app.Dispatch("finish"); Select(angle, 0); app.Dispatch("delete");
            CheckEdit(!app.Session.items.Any(i => i.id == angle.id || i.id == gridId), "Deleting the gridded Level node removes its entire attached path and grid");
            FreshItems(); angle = AddPath(ToolkitItemKind.AnglePath, a, b, c); app.Dispatch("tool:3");
            Set("activePath", angle.id); Set("lastNodePress", -10f); Aim(a + Vector3.up * 0.2f, true);
            CheckEdit(angle.points.Count == 4 && app.Selection == null, "Level nodes can be placed inside the grid without its background stealing the selection");
            app.Dispatch("finish");
            Aim(b, true, held: true, gesture: true);
            CheckEdit(app.Selection?.id == angle.id && Get("grabbed") == null && Get("pendingGrab") != null, "A single pinch selects a Level node without moving it");
            Set("pendingGrabAt", Time.unscaledTime - 0.5f); Aim(b + Vector3.up * 0.2f, held: true, gesture: true);
            CheckEdit(Get("grabbed") != null && Vector3.Distance(angle.points[1], app.RoomRoot.InverseTransformPoint(b)) > 0.1f, "Sustaining the pinch transitions a selected Level node into a drag");
            var sample = (ToolInput)Get("state"); sample.TriggerHeld = sample.GripHeld = false; sample.GripUp = true; Set("state", sample); Call("UpdateInteraction");
            CheckEdit(Get("grabbed") == null && app.Selection?.id == angle.id, "Releasing the Level node finishes its move and keeps the edit selection");
            Select(angle, 0); var grid = app.Session.items.First(i => i.id == angle.gridId);
            var first = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().First(i => i.ItemId == angle.id && i.Node == 0);
            Invoke("BeginGrabHandle", first); Set("target", a + Vector3.up * 0.2f); Set("validSurface", true); sample = (ToolInput)Get("state"); sample.GripUp = false; sample.GripHeld = true; Set("state", sample); Call("UpdateGrab");
            CheckEdit(grid.position == angle.points[0], "Moving the first Level node also moves its grid");
            Invoke("CancelGrab", true); angle = app.Session.items.First(i => i.id == angle.id); angle.gridId = null; Call("RenderAll");
            CheckEdit(angle.gridId == grid.id, "Older saved Level paths recover their grid association");

            FreshItems(); app.Dispatch("tool:1");
            var box1 = new ToolkitItemData { kind = ToolkitItemKind.Furniture, position = app.RoomRoot.InverseTransformPoint(new Vector3(0.8f, 0.505f, 1)), dimensions = new Vector3(0.6f, 1, 0.6f) };
            var box2 = new ToolkitItemData { kind = ToolkitItemKind.Furniture, position = app.RoomRoot.InverseTransformPoint(new Vector3(-0.8f, 0.405f, 1)), dimensions = new Vector3(0.8f, 0.8f, 0.5f) };
            app.Session.items.Add(box1); app.Session.items.Add(box2); Call("RenderAll"); Select(box1);
            CheckEdit(app.FurnitureDimensions == box1.dimensions && Button("delete"), "Selecting an older Fit box loads its actual measurements into edit controls");
            var defaults = app.Preferences.furniture; app.Dispatch("width:1");
            CheckEdit(box1.dimensions.x > 0.6f && box2.dimensions.x == 0.8f && app.Preferences.furniture == defaults, "Fit plus/minus edits only the selected box, leaving other boxes and placement defaults intact");
            float bottom = app.RoomRoot.TransformPoint(box1.position).y - box1.dimensions.y / 2; app.SetDimension("height", 1.2f);
            CheckEdit(Mathf.Abs(app.RoomRoot.TransformPoint(box1.position).y - box1.dimensions.y / 2 - bottom) < 0.0001f, "Resizing a selected Fit box keeps its base on the floor");
            CheckEdit(!app.SetDimension("width", 8) && box1.dimensions.x < 1, "Resize collision checks preserve the previous dimensions when the box would overlap a wall");
            Select(box2); CheckEdit(app.FurnitureDimensions == box2.dimensions, "Switching Fit selection refreshes dimensions from the newly selected box"); app.Dispatch("delete");
            CheckEdit(!app.Session.items.Contains(box2) && app.Session.items.Contains(box1), "Fit Delete affects only its selected box");
            FreshItems(); app.Dispatch("tool:2"); var frames = new ToolkitItemData { kind = ToolkitItemKind.Frames, position = app.RoomRoot.InverseTransformPoint(c), dimensions = new Vector3(0.3f, 0.4f, 0), gap = 0.03f, inset = 0.08f, count = 2 };
            app.Session.items.Add(frames); Call("RenderAll"); Select(frames);
            CheckEdit(app.FrameDimensions == frames.dimensions && app.FrameCount == 2 && app.FrameGap == 0.03f && app.FrameInset == 0.08f, "Selecting Hang loads its own dimensions, count, gap and hanger inset");
            app.SetDimension("frameheight", 0.3f); app.SetDimension("gap", 0.06f); app.Dispatch("count:1");
            CheckEdit(frames.dimensions.y == 0.3f && frames.gap == 0.06f && frames.count == 3 && app.RoomRoot.GetComponentsInChildren<Text>().Count(t => t.text.StartsWith("Nail ")) == 3, "Hang edits rebuild the selected frame and nail layout immediately");
            app.Dispatch("delete"); CheckEdit(app.Session.items.Count == 0, "Hang has a working Delete control");

            app.Dispatch("main"); var drag = Button("dragmenu");
            var input = new ToolInput { Tracked = true, Gesture = true, TriggerDown = true, TriggerHeld = true, Position = Vector3.zero, Ray = new Ray(head.position, (drag.transform.position - head.position).normalized) };
            Vector3 start = palette.transform.position; MenuSample(input, drag, drag.transform.position);
            typeof(ToolkitPalette).GetField("menuPressedAt", Private).SetValue(palette, Time.unscaledTime - 0.5f);
            input.TriggerDown = false; input.Position = Vector3.right * 0.2f; input.Ray = new Ray(input.Ray.origin + Vector3.right * 0.3f, input.Ray.direction); MenuSample(input, drag, drag.transform.position);
            CheckEdit(palette.Dragging && Vector3.Distance(start, palette.transform.position) > 0.25f, "Holding and moving the floating full menu's top margin drags it freely");
            input.TriggerHeld = false; MenuSample(input, null, Vector3.zero); CheckEdit(!palette.Dragging, "Releasing drops the full menu in world space");
            app.Dispatch("minimize"); var expand = Button("expand"); input.Position = Vector3.zero; input.TriggerHeld = input.TriggerDown = true;
            MenuSample(input, expand, expand.transform.position); input.TriggerHeld = input.TriggerDown = false; MenuSample(input, expand, expand.transform.position);
            CheckEdit(Button("settings"), "A short single pinch on the pill background still expands it");
            app.Dispatch("minimize"); expand = Button("expand"); input.TriggerHeld = input.TriggerDown = true; MenuSample(input, expand, expand.transform.position);
            typeof(ToolkitPalette).GetField("menuPressedAt", Private).SetValue(palette, Time.unscaledTime - 0.5f); input.TriggerDown = false; input.Position = Vector3.up * 0.2f; MenuSample(input, expand, expand.transform.position);
            input.TriggerHeld = false; MenuSample(input, null, Vector3.zero);
            CheckEdit(Button("expand") && !palette.Dragging, "Pinch-holding the pill moves it without expanding it");
            app.Dispatch("main"); drag = Button("dragmenu"); input.Gesture = false; input.GripDown = input.GripHeld = true; input.Ray = new Ray(head.position, (drag.transform.position - head.position).normalized);
            MenuSample(input, drag, drag.transform.position); CheckEdit(palette.Dragging, "Controller grip begins a floating menu drag");
            input.SourceChanged = true; MenuSample(input, null, Vector3.zero); CheckEdit(!palette.Dragging, "Tracking/source changes cancel floating menu drags");
            app.Preferences.wristGestureDisabled = false; palette.ResetWristPose(); input = new ToolInput { Tracked = true, PaletteTracked = true, PalettePosition = head.position + head.forward * 0.5f, PaletteUp = Vector3.up, Source = "Selection replay" };
            palette.UpdatePose(input, head); CheckEdit(palette.AttachedToWrist, "Menu reports its actual wrist attachment state");
            input.PaletteTracked = false; palette.UpdatePose(input, head);
            CheckEdit(palette.AttachedToWrist, "A brief wrist occlusion holds the menu instead of jumping to floating mode");
            input.Gesture = true; input.TriggerDown = input.TriggerHeld = true; MenuSample(input, Button("dragmenu"), Vector3.zero);
            CheckEdit(!palette.Dragging, "Wrist-attached menus cannot be grabbed");
            var pointer = (LineRenderer)Get("pointer"); var dot = (Transform)Get("reticle");
            CheckEdit(pointer.sortingOrder > 100 && dot.GetComponent<Renderer>().sortingOrder > 100 && pointer.sharedMaterial.renderQueue > 4000 && pointer.sharedMaterial.GetFloat("_ZTest") == 8, "Ray and dot render after the menu with depth testing disabled");
            CheckEdit(Button("dragmenu").GetComponent<RectTransform>().sizeDelta.x == 360, "The shorter menu drag handle leaves clearance from both corner buttons");
            SpatialToolkitApp.ValidateSession(JsonUtility.FromJson<ToolkitSession>(JsonUtility.ToJson(app.Session)));
            CheckEdit(true, "New selection and Level-link data round-trip through room persistence");
            string report = $"All {checks} selection and menu drag checks passed."; File.AppendAllText(".tools/checks/toolkit-selection.txt", report + "\n"); return report;
        }
        catch (Exception e) { File.AppendAllText(".tools/checks/toolkit-selection.txt", "FAIL: " + e + "\n"); throw; }
        finally { app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(preferences); app.StorageDirectoryOverride = storage; app.enabled = enabled; }
    }

    public static void PrepareEditPreview(int tool)
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        app.enabled = false; FreshItems();
        app.Preferences.wristGestureDisabled = true; app.Preferences.colorOption = 2;
        ToolkitVisuals.SetColorOption(2);
        var item = tool == 1 ? new ToolkitItemData { kind = ToolkitItemKind.Furniture, position = Vector3.right, dimensions = new Vector3(0.6f, 1, 0.6f) } :
            new ToolkitItemData { kind = ToolkitItemKind.Frames, position = Vector3.right, dimensions = new Vector3(0.4f, 0.5f, 0), count = 3 };
        app.Session.items.Add(item); Call("RenderAll"); Select(item);
        var head = app.ViewCamera.transform;
        palette.ResetWristPose(); palette.UpdatePose(new ToolInput { Source = "Preview" }, head);
        palette.transform.SetPositionAndRotation(head.position + head.forward * 0.65f, Quaternion.LookRotation(head.forward, Vector3.up));
        var origin = head.position + head.right * 0.25f - head.up * 0.08f;
        var point = palette.GetComponentInChildren<Canvas>().transform.TransformPoint(new Vector3(-140, -30, 0));
        Set("state", new ToolInput { Tracked = true, Gesture = true, HandRay = true, Source = "Preview", Position = origin,
            Ray = new Ray(origin, (point - origin).normalized), Rotation = head.rotation });
        Physics.SyncTransforms(); Call("UpdateInteraction");
    }
}
