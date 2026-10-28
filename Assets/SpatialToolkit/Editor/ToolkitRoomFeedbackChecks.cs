using System;
using System.IO;
using System.Linq;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class ToolkitFeatureChecks
{
    static ToolkitSelectionVisual Visual(string id, int node = -1) => app.RoomRoot.GetComponentsInChildren<ToolkitSelectionVisual>().Single(v => v.ItemId == id && v.Node == node);
    static bool SameRgb(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.001f;
    static bool Upright(Transform t) => Mathf.Abs(Vector3.Dot(t.right, Vector3.up)) < 0.0001f && Vector3.Dot(t.up, Vector3.up) >= -0.0001f;

    [MenuItem("PICO/Spatial Toolkit/Run Room and Selection Feedback Checks")]
    public static string RunRoomFeedbackChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        if (!EditorApplication.isPlaying || !app || app.Session.items.Count != 0) throw new Exception("Start in a fresh empty workbench Play Mode session.");
        app.SendMessage("OnApplicationFocus", true);
        bool enabled = app.enabled; app.enabled = false;
        string preferences = JsonUtility.ToJson(app.Preferences), storage = app.StorageDirectoryOverride;
        var head = app.ViewCamera.transform; Vector3 originalHead = head.position; Quaternion originalHeadRotation = head.rotation;
        Directory.CreateDirectory(".tools/checks/feedback-session"); app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/feedback-session");
        string sessionFile = Path.Combine(app.StorageDirectoryOverride, "spatial-toolkit-room.json");
        string reportFile = ".tools/checks/toolkit-room-feedback.txt"; File.WriteAllText(reportFile, "Room, orientation, gaze, hover and hold-delete checks\n");
        int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); count++; File.AppendAllText(reportFile, "PASS: " + label + "\n"); };
        try
        {
            app.Preferences.wristGestureDisabled = true; app.Preferences.colorOption = 0; ToolkitVisuals.SetColorOption(0);
            Aim(new Vector3(1, 1, 2.49f)); app.Dispatch("pin");
            head.rotation = Quaternion.Euler(20, 15, 55);
            var sample = new ToolInput { Tracked = true, Source = "Orientation replay" };
            foreach (var direction in new[] { Vector3.forward, new Vector3(1, 1, 1), new Vector3(-1, -0.7f, 1), Vector3.up, Vector3.down })
            {
                palette.transform.SetPositionAndRotation(head.position + direction.normalized, Quaternion.Euler(12, 25, 70));
                palette.UpdatePose(sample, head);
                check(Upright(palette.transform) && Vector3.Dot(palette.transform.forward, direction.normalized) > 0.999f, "Menu faces the player without roll at " + direction);
            }
            head.rotation = originalHeadRotation;
            palette.transform.position = head.position + head.forward * 0.8f; palette.FacePlayer(head);
            var zone = Button("dragmenu"); var point = zone.transform.position;
            sample = new ToolInput { Tracked = true, Gesture = true, TriggerDown = true, TriggerHeld = true, Position = head.position,
                Ray = new Ray(head.position, (point - head.position).normalized) };
            MenuSample(sample, zone, point); typeof(ToolkitPalette).GetField("menuPressedAt", Private).SetValue(palette, Time.unscaledTime - 0.5f);
            sample.TriggerDown = false; sample.Position += Vector3.right * 0.2f; sample.Ray = new Ray(sample.Ray.origin + Vector3.right * 0.3f, sample.Ray.direction);
            MenuSample(sample, null, Vector3.zero);
            check(palette.Dragging && Upright(palette.transform), "Dragging also keeps the menu aligned to gravity");
            palette.CancelMenuDrag();
            palette.transform.position = head.position - head.forward;

            sample = new ToolInput { Tracked = true, Gaze = true, Gesture = true, Source = "Eye gaze", Ray = new Ray(head.position, Vector3.forward), Rotation = head.rotation };
            Set("state", sample); Call("UpdateInteraction");
            var ray = (LineRenderer)Get("pointer"); var dot = (Transform)Get("reticle");
            check(!ray.gameObject.activeSelf && dot.gameObject.activeSelf, "Gaze pinch shows the dot and hides the pointer ray");
            sample.Gaze = false; sample.Dwell = true; Set("state", sample); Call("UpdateInteraction");
            check(!ray.gameObject.activeSelf && dot.gameObject.activeSelf, "Hands-free dwell also uses a dot without a ray");
            sample.Dwell = false; sample.HandRay = true; Set("state", sample); Call("UpdateInteraction");
            check(ray.gameObject.activeSelf && dot.gameObject.activeSelf, "Hand-ray controls retain both ray and dot");
            sample.HandRay = sample.Gesture = false; Set("state", sample); Call("UpdateInteraction");
            check(ray.gameObject.activeSelf, "Controller rays remain visible");

            bool choices = true;
            foreach (bool preferLeft in new[] { false, true }) foreach (bool left in new[] { false, true }) foreach (bool right in new[] { false, true })
                choices &= ToolkitTrackedHands.UseLeftHand(left, right, preferLeft) == (preferLeft ? left : !right && left);
            check(choices, "Both-hand availability always recovers the preferred hand, with one-hand fallback");

            FreshItems(); app.Dispatch("tool:0");
            var a = new Vector3(0.3f, 1.1f, 2.497f); var b = new Vector3(0.9f, 1.1f, 2.497f);
            var path = AddPath(ToolkitItemKind.Measurement, a, b);
            var node = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().Single(h => h.ItemId == path.id && h.Node == 0);
            check(Mathf.Abs(node.GetComponent<SphereCollider>().radius - 0.75f) < 0.0001f, "Node hit radius is exactly 1.5 times the visible sphere radius");
            Aim(a + Vector3.right * 0.013f);
            check(Visual(path.id, 0).Hovered && !Visual(path.id, 0).Selected, "The extra half-radius region highlights a node before selection");
            var halo = app.RoomRoot.GetComponentsInChildren<ToolkitNodeHalo>(true).Single(h => h.Source == node.transform);
            check(halo.gameObject.activeSelf && SameRgb(halo.Line.sharedMaterial.color, ToolkitVisuals.Selection), "Node hover displays the shared selection-color outline");
            Aim(a + Vector3.right * 0.024f); check(!Visual(path.id, 0).Hovered, "Hover target does not extend beyond the bounded hit radius");
            Aim(a, true, gesture: true);
            check(Visual(path.id, 0).Selected && SameRgb(node.GetComponent<Renderer>().sharedMaterial.color, ToolkitVisuals.Selection), "Pinching a Measure node applies the selected tint immediately");
            Aim(a);
            var angle = AddPath(ToolkitItemKind.AnglePath, new Vector3(-1.3f, 0.8f, 2.497f), new Vector3(-0.8f, 1.4f, 2.497f));
            var box = new ToolkitItemData { kind = ToolkitItemKind.Furniture, position = app.RoomRoot.InverseTransformPoint(new Vector3(1.2f, 0.405f, 1)), dimensions = new Vector3(0.5f, 0.8f, 0.5f) };
            var frames = new ToolkitItemData { kind = ToolkitItemKind.Frames, position = app.RoomRoot.InverseTransformPoint(new Vector3(1.1f, 1.7f, 2.497f)), dimensions = new Vector3(0.3f, 0.4f, 0), count = 2 };
            app.Session.items.Add(box); app.Session.items.Add(frames); Call("RenderAll");
            for (int option = 0; option < 3; option++)
            {
                foreach (var item in new[] { path, angle, box, frames })
                {
                    int index = SpatialToolkitApp.IsPath(item) ? 0 : -1; Select(item, index);
                    var visual = Visual(item.id, index);
                    var source = visual.GetComponent<Renderer>();
                    if (!source) source = visual.GetComponentsInChildren<Renderer>().First(r => !r.GetComponent<ToolkitLineOutline>());
                    check(visual.Selected && SameRgb(source.sharedMaterial.color, ToolkitVisuals.Selection), item.kind + " uses the shared selection tint in Option " + (option + 1));
                }
                if (option < 2) app.Dispatch("colors:1");
            }
            Select(path, 0); Aim(app.RoomRoot.TransformPoint(box.position));
            check(Visual(box.id).Hovered && Visual(path.id, 0).Selected, "Hovering a box preserves an existing node selection");
            var boxOutline = Visual(box.id).transform.Find("Selection outline");
            check(boxOutline.gameObject.activeSelf && boxOutline.GetComponentsInChildren<LineRenderer>().Any(l => SameRgb(l.sharedMaterial.color, ToolkitVisuals.Selection)), "Box hover has the same accent outline as nodes");

            FreshItems(); app.Dispatch("tool:0"); Place(a); string savedActive = Active;
            app.Dispatch("save");
            check(File.Exists(sessionFile) && Active == savedActive && app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().Length == 1, "Saving an active one-node path keeps its node rendered and its path active");
            Vector3 frame = app.RoomRoot.position; app.ClearAll();
            check(File.Exists(sessionFile) && Active == null && app.RoomRoot.gameObject.activeSelf, "Clear All clears transient path/anchor state without deleting the saved room");
            Set("activePath", "stale path"); app.RoomRoot.gameObject.SetActive(false);
            var orphan = new GameObject("Orphan node", typeof(ToolkitHandle)); orphan.transform.SetParent(app.RoomRoot, false);
            app.ClearAll();
            check(Active == null && app.RoomRoot.gameObject.activeSelf && app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().Length == 0, "Clear All repairs stale empty paths, hidden roots and orphaned render objects");
            Place(b);
            var first = app.Session.items.Single();
            check(app.RoomRoot.position == frame && Vector3.Distance(app.RoomRoot.TransformPoint(first.points[0]), b) < 0.015f && app.RoomRoot.GetComponentsInChildren<Renderer>().Length > 0, "First placement after Save/Clear All stays in the established frame and is immediately visible");
            app.UndoAction(); app.ClearAll(); Place(a + Vector3.up * 0.2f);
            check(app.RoomRoot.position == frame && app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().Length == 1, "Undoing all geometry and clearing again cannot leave an invisible first node");
            app.Dispatch("save"); Place(b + Vector3.up * 0.2f);
            int nodes = app.Session.items.Sum(i => i.points.Count); string currentPath = Active;
            File.WriteAllText(sessionFile + ".tmp", File.ReadAllText(sessionFile));
            Aim(Button("load").transform.position, true);
            check(app.Session.items.Sum(i => i.points.Count) == nodes && (float)Get("loadHeldSince") >= 0, "Pressing Load waits for release rather than replacing the room before a delete hold");
            Set("loadHeldSince", Time.unscaledTime - 0.3f); Aim(Button("load").transform.position, held: true);
            check(Button("load").GetComponentInChildren<Text>().text == "Load room", "Load hold has the same initial grace period as Undo");
            Set("loadHeldSince", Time.unscaledTime - 0.9f); Aim(Button("load").transform.position, held: true);
            check(Button("load").GetComponentInChildren<Text>().text == "Delete Room (Hold)" && palette.GetComponentsInChildren<Image>().Any(i => i.type == Image.Type.Filled && i.fillAmount > 0.2f && i.fillAmount < 0.6f), "Holding Load changes its label and shows smooth deletion progress");
            Aim(new Vector3(0, 2.5f, 2.49f), held: true);
            check(File.Exists(sessionFile) && (float)Get("loadHeldSince") < 0 && Button("load").GetComponentInChildren<Text>().text == "Load room", "Moving off Load cancels deletion and restores its label");
            Aim(Button("load").transform.position); Aim(Button("load").transform.position, true);
            Set("loadHeldSince", Time.unscaledTime - 1.7f); Aim(Button("load").transform.position, held: true);
            check(!File.Exists(sessionFile) && !File.Exists(sessionFile + ".bak") && !File.Exists(sessionFile + ".tmp"), "Completed Load hold deletes the main save, backup and temporary save");
            check(app.Session.items.Sum(i => i.points.Count) == nodes && Active == currentPath && app.RoomRoot.gameObject.activeSelf, "Deleting the saved room preserves current measurements and their active path");
            app.Dispatch("save"); app.ClearAll(); Aim(Button("load").transform.position, true); Aim(Button("load").transform.position);
            check(app.Session.items.Sum(i => i.points.Count) == nodes && app.Status.Contains("Hold the button to Delete Saved Room"), "A short Load press still restores the room on release and explains the delete gesture");
            Aim(Button("load").transform.position, true); Set("loadHeldSince", Time.unscaledTime - 0.9f);
            Invoke("OnApplicationFocus", false);
            check(File.Exists(sessionFile) && (float)Get("loadHeldSince") < 0, "Focus loss cancels saved-room deletion"); Invoke("OnApplicationFocus", true);
            string report = $"All {count} room and selection feedback checks passed."; File.AppendAllText(reportFile, report + "\n"); return report;
        }
        catch (Exception e) { File.AppendAllText(reportFile, "FAIL: " + e + "\n"); throw; }
        finally
        {
            head.SetPositionAndRotation(originalHead, originalHeadRotation); FreshItems();
            app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(preferences); ToolkitVisuals.SetColorOption(app.Preferences.colorOption);
            app.StorageDirectoryOverride = storage; app.enabled = enabled;
        }
    }
}
