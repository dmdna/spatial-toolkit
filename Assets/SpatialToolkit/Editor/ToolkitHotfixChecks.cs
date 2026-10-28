using System;
using System.IO;
using System.Linq;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;

public static partial class ToolkitFeatureChecks
{
    [MenuItem("PICO/Spatial Toolkit/Run Hand and Menu Hotfix Checks")]
    public static string RunHotfixChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        if (!EditorApplication.isPlaying || !app || app.Session.items.Count != 0) throw new Exception("Start in an empty fresh Play Mode workbench.");
        app.SendMessage("OnApplicationFocus", true);
        bool enabled = app.enabled; app.enabled = false;
        string preferences = JsonUtility.ToJson(app.Preferences), storage = app.StorageDirectoryOverride;
        var head = app.ViewCamera.transform; Vector3 headPosition = head.position;
        Directory.CreateDirectory(".tools/checks/hotfix-session"); app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/hotfix-session");
        string reportPath = ".tools/checks/toolkit-hotfix.txt"; File.WriteAllText(reportPath, "Hand-ray and menu-grab hotfix checks\n");
        int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); count++; File.AppendAllText(reportPath, "PASS: " + label + "\n"); };
        var resolver = new ToolkitInput();
        try
        {
            var raw = new ToolInput { Tracked = true, Gesture = true, HandRay = true, Source = "Right hand ray", Position = Vector3.up,
                Ray = new Ray(Vector3.up, Vector3.forward), Rotation = Quaternion.identity };
            resolver.ResolveSample(raw, false, false); resolver.ResolveSample(raw, true, true);
            raw.Rotation = Quaternion.Euler(0, 60, 0); raw.Ray = new Ray(Vector3.one, Vector3.right);
            var held = resolver.ResolveSample(raw, true, true);
            check(Vector3.Distance(held.Ray.origin, Vector3.up) < 0.0001f && Vector3.Angle(held.Ray.direction, Vector3.forward) < 0.001f, "Twisting a pinched hand cannot translate or swing the captured drag ray");
            check(Vector3.Angle(held.Forward, raw.Rotation * Vector3.forward) < 0.001f, "Wrist orientation still reaches the object-rotation controls");
            raw.Position += Vector3.right * 0.1f; held = resolver.ResolveSample(raw, true, true);
            check(Vector3.Distance(held.Ray.origin, Vector3.up + Vector3.right * 0.3f) < 0.0001f, "Physical hand translation preserves the previous drag movement gain");
            resolver.ResolveSample(raw, false, false); var idle = resolver.ResolveSample(raw, false, false);
            check(Vector3.Angle(idle.Ray.direction, Vector3.right) < 0.001f, "Free system-pointer aiming resumes after releasing the pinch");

            FreshItems(); app.Dispatch("tool:1");
            var box = new ToolkitItemData { kind = ToolkitItemKind.Furniture, position = app.RoomRoot.InverseTransformPoint(new Vector3(0.9f, 0.405f, 1)), dimensions = new Vector3(0.4f, 0.8f, 0.4f) };
            app.Session.items.Add(box); Call("RenderAll");
            var state = new ToolInput { Tracked = true, Gesture = true, HandRay = true, Source = "Hotfix replay", GripHeld = true,
                TriggerHeld = true, Position = head.position, Ray = new Ray(head.position, Vector3.forward), Rotation = Quaternion.identity, Forward = Vector3.forward };
            Set("state", state); var handle = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().First(h => h.ItemId == box.id);
            Invoke("BeginGrabHandle", handle); Vector3 original = box.position;
            Set("validSurface", true); Set("target", app.RoomRoot.TransformPoint(box.position) - Vector3.up * 0.4f); Set("targetNormal", Vector3.up);
            state.Rotation = Quaternion.Euler(0, 60, 0); state.Forward = state.Rotation * Vector3.forward; Set("state", state); Call("UpdateGrab");
            check(Vector3.Distance(original, box.position) < 0.0001f && Quaternion.Angle(box.rotation, Quaternion.identity) > 55, "A held Fit box rotates sixty degrees without changing its translation");
            Invoke("CancelGrab", true); FreshItems();

            app.Preferences.wristGestureDisabled = true; palette.ResetWristPose();
            palette.UpdatePose(new ToolInput { Source = "Hotfix replay" }, head);
            palette.transform.SetPositionAndRotation(head.position + head.forward * 0.8f + head.right * 0.1f, ToolkitVisuals.UprightRotation(head.forward * 0.8f + head.right * 0.1f, Quaternion.identity));
            var drag = Button("dragmenu");
            Vector3 grabPoint = drag.transform.TransformPoint(new Vector3(110, 0, -4));
            Vector3 localGrab = palette.transform.InverseTransformPoint(grabPoint);
            var origin = head.position + head.right * 0.2f - head.up * 0.1f;
            raw = new ToolInput { Tracked = true, Gesture = true, HandRay = true, Source = "Right hand ray", Position = origin,
                Rotation = Quaternion.identity, Ray = new Ray(origin, (grabPoint - origin).normalized) };
            resolver.Reset(); resolver.ResolveSample(raw, false, false); held = resolver.ResolveSample(raw, true, true);
            Vector3 start = palette.transform.position; float depth = Vector3.Dot(grabPoint - held.Ray.origin, held.Ray.direction);
            MenuSample(held, drag, grabPoint);
            check(Vector3.Distance(start, palette.transform.position) < 0.0001f, "Pressing an off-center drag point does not jump the menu");
            typeof(ToolkitPalette).GetField("menuPressedAt", Private).SetValue(palette, Time.unscaledTime - 0.5f);
            held = resolver.ResolveSample(raw, true, true); MenuSample(held, drag, grabPoint);
            check(palette.Dragging && Vector3.Distance(palette.DragPoint, grabPoint) < 0.0001f && Vector3.Distance(start, palette.transform.position) < 0.0001f, "Transitioning from press to hold preserves the exact original grab point without a jump");
            raw.Position += new Vector3(0.1f, 0.04f, 0.05f); raw.Rotation = Quaternion.Euler(0, 55, 0);
            raw.Ray = new Ray(raw.Position, raw.Rotation * Vector3.forward); held = resolver.ResolveSample(raw, true, true);
            MenuSample(held, null, Vector3.zero); Vector3 cursor = held.Ray.GetPoint(depth);
            check(Vector3.Distance(palette.transform.TransformPoint(localGrab), cursor) < 0.0001f, "Hand dragging keeps the original panel point exactly on the selection cursor");
            head.position += Vector3.left * 0.25f; MenuSample(held, null, Vector3.zero);
            check(Vector3.Distance(palette.transform.TransformPoint(localGrab), cursor) < 0.0001f, "Turning the panel to face the user rotates around the grab point without cursor drift");
            held.Source = "Hotfix replay"; held.VisualOriginTracked = true; held.VisualRayOrigin = raw.Position + Vector3.up * 0.02f;
            Set("state", held); Physics.SyncTransforms(); Call("UpdateInteraction");
            var pointer = (LineRenderer)Get("pointer"); var dot = (Transform)Get("reticle");
            check(Vector3.Distance(dot.position, palette.DragPoint) < 0.0001f && Vector3.Distance(pointer.GetPosition(1), dot.position) < 0.0001f, "The rendered dot and ray endpoint remain at the active menu grab point");
            check(Vector3.Distance(pointer.GetPosition(0), held.VisualRayOrigin) < 0.0001f, "The visible ray begins at the pinch area while the dot uses the system pointer hit");
            palette.CancelMenuDrag(); head.position = headPosition;

            palette.Navigate("minimize"); var expand = Button("expand"); grabPoint = expand.transform.TransformPoint(new Vector3(-315, 0, -4));
            localGrab = palette.transform.InverseTransformPoint(grabPoint);
            raw.Position = origin; raw.Ray = new Ray(origin, (grabPoint - origin).normalized);
            resolver.ResolveSample(raw, false, false); held = resolver.ResolveSample(raw, true, true); depth = Vector3.Dot(grabPoint - held.Ray.origin, held.Ray.direction);
            MenuSample(held, expand, grabPoint); typeof(ToolkitPalette).GetField("menuPressedAt", Private).SetValue(palette, Time.unscaledTime - 0.5f);
            raw.Position += Vector3.up * 0.12f; held = resolver.ResolveSample(raw, true, true); MenuSample(held, null, Vector3.zero);
            check(palette.Dragging && Vector3.Distance(palette.transform.TransformPoint(localGrab), held.Ray.GetPoint(depth)) < 0.0001f, "The pill background is anchored to the same selection dot during a drag");
            palette.CancelMenuDrag(); palette.Navigate("main"); drag = Button("dragmenu");
            check(drag.GetComponent<RectTransform>().sizeDelta.x == 360, "The top grab handle is shortened to leave more room near the corner buttons");
            state = new ToolInput { Tracked = true, GripDown = true, GripHeld = true, Ray = new Ray(origin, (drag.transform.position - origin).normalized) };
            grabPoint = drag.transform.position; depth = Vector3.Dot(grabPoint - state.Ray.origin, state.Ray.direction); localGrab = palette.transform.InverseTransformPoint(grabPoint);
            MenuSample(state, drag, grabPoint); state.GripDown = false; state.Ray = new Ray(origin, Quaternion.Euler(0, 12, 0) * state.Ray.direction); MenuSample(state, null, Vector3.zero);
            check(Vector3.Distance(palette.transform.TransformPoint(localGrab), state.Ray.GetPoint(depth)) < 0.0001f, "Controller menu dragging preserves the same exact cursor anchor");
            string report = $"All {count} hand and menu hotfix checks passed."; File.AppendAllText(reportPath, report + "\n"); return report;
        }
        catch (Exception e) { File.AppendAllText(reportPath, "FAIL: " + e + "\n"); throw; }
        finally { resolver.Dispose(); palette.CancelMenuDrag(); head.position = headPosition; app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(preferences); app.StorageDirectoryOverride = storage; app.enabled = enabled; }
    }
}
