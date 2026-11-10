using System;
using System.IO;
using System.Linq;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;

public static partial class ToolkitFeatureChecks
{
    [MenuItem("PICO/Spatial Toolkit/Run Preview Rotation Checks")]
    public static string RunPreviewRotationChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        if (!EditorApplication.isPlaying || !app) throw new Exception("Enter workbench Play Mode first.");
        bool wasEnabled = app.enabled; app.enabled = false; app.SendMessage("OnApplicationFocus", true);
        string prefs = JsonUtility.ToJson(app.Preferences), storage = app.StorageDirectoryOverride;
        var head = app.ViewCamera.transform; Vector3 headPosition = head.position, roomPosition = app.RoomRoot.position;
        Quaternion headRotation = head.rotation, roomRotation = app.RoomRoot.rotation;
        float originalYaw = (float)Get("fitPreviewYaw");
        const string reportPath = ".tools/checks/toolkit-preview-rotation.txt";
        Directory.CreateDirectory(".tools/checks/rotation-session"); app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/rotation-session");
        File.WriteAllText(reportPath, "Live Fit preview rotation checks\n"); int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); count++; File.AppendAllText(reportPath, "PASS: " + label + "\n"); };
        try
        {
            head.SetPositionAndRotation(new Vector3(0, 1.6f, -2), Quaternion.identity);
            foreach (bool left in new[] { false, true })
            {
                Invoke("CancelGrab", true); Set("frameSampled", false); Set("interactionOffHand", false); Set("state", default(ToolInput)); FreshItems();
                Set("fitPreviewYaw", 0f); app.Preferences.leftHanded = left; app.Preferences.imperial = false; app.Preferences.wristGestureDisabled = true;
                app.Preferences.furniture = new Vector3(1.2f, 0.8f, 0.3f); app.Dispatch("tool:1"); if (!app.Snapping) app.Dispatch("snap");
                var main = new ToolInput { Tracked = true, LeftHand = left, Source = "Rotation main", Rotation = Quaternion.identity,
                    Ray = new Ray(head.position, (new Vector3(0.7f, -0.007f, 0.7f) - head.position).normalized) };
                palette.ResetWristPose(); palette.UpdatePose(main, head); app.Dispatch("pin"); palette.transform.position = head.position - Vector3.forward;
                var off = new ToolInput { Tracked = true, LeftHand = !left, Source = "Rotation off", Rotation = Quaternion.identity, Ray = new Ray(head.position, Vector3.up) };
                string label = (left ? "Left main: " : "Right main: ");
                PreviewFrame(main, off); var preview = (GameObject)Get("fitPreview"); Vector3 position = preview.transform.position;
                int undoCount = ((System.Collections.Generic.List<string>)Get("undo")).Count;
                main.RotateAxis = 1; PreviewFrame(main, off); float yaw = (float)Get("fitPreviewYaw");
                check(Mathf.Abs(Mathf.DeltaAngle(0, yaw) - 30 * Time.unscaledDeltaTime) < 0.01f && yaw > 0, label + "unplaced thumbstick rotation is 30 degrees per second with snapping enabled");
                check(Quaternion.Angle(preview.transform.rotation, Quaternion.Euler(0, yaw, 0)) < 0.01f && Vector3.Distance(position, preview.transform.position) < 0.001f,
                    label + "preview rotates without changing position");
                main.RotateAxis = -1; PreviewFrame(main, off);
                check(Mathf.Abs(Mathf.DeltaAngle((float)Get("fitPreviewYaw"), 0)) < 0.01f, label + "left stick reverses preview rotation at the same speed");
                main.RotateAxis = 0; off.RotateAxis = 1; PreviewFrame(main, off);
                check(Mathf.Abs(Mathf.DeltaAngle((float)Get("fitPreviewYaw"), 0)) < 0.01f, label + "off-hand stick cannot take ownership of an unplaced preview");
                off.RotateAxis = 0; var row = Button("edit:width").transform.position;
                off.Ray = new Ray(head.position, (row - head.position).normalized); off.TriggerDown = off.TriggerHeld = true; PreviewFrame(main, off);
                off.TriggerDown = false; off.Ray = new Ray(head.position, (row + palette.transform.right * 0.5f - head.position).normalized);
                Set("dimensionAt", Time.unscaledTime - 0.3f); main.RotateAxis = 1; PreviewFrame(main, off); yaw = (float)Get("fitPreviewYaw");
                check((bool)Get("interactionOffHand") && Get("dimensionKey") != null && yaw > 0 && preview.transform.localScale == app.Preferences.furniture,
                    label + "main-hand stick rotates and resizes the preview during an off-hand drag beyond the menu");
                main.RotateAxis = 0; off.TriggerHeld = false; PreviewFrame(main, off);
                check(((System.Collections.Generic.List<string>)Get("undo")).Count == undoCount, label + "preview changes create no placed-item undo records");
                main.TriggerDown = main.TriggerHeld = true; PreviewFrame(main, off);
                var item = app.Session.items.Single();
                check(Quaternion.Angle(app.RoomRoot.rotation * item.rotation, Quaternion.Euler(0, yaw, 0)) < 0.01f,
                    label + "placement preserves the exact unsnapped preview rotation");

                Set("frameSampled", false); FreshItems(); Set("state", default(ToolInput));
                app.Preferences.furniture = new Vector3(1.2f, 0.8f, 0.3f);
                main.TriggerDown = main.TriggerHeld = false; off.Ray = new Ray(head.position, Vector3.up);
                main.Ray = new Ray(head.position, (new Vector3(0, -0.007f, 2.1f) - head.position).normalized);
                Set("fitPreviewYaw", 0f); PreviewFrame(main, off);
                check(!app.Status.Contains("blocked"), label + "narrow depth clears the wall before rotation");
                Set("fitPreviewYaw", 90f); PreviewFrame(main, off);
                check(app.Status.Contains("blocked") || ((GameObject)Get("fitPreview")).GetComponentsInChildren<LineRenderer>().Any(l => l.sharedMaterial.color == ToolkitVisuals.Coral),
                    label + "rotated preview detects and displays its wider wall overlap");
                main.TriggerDown = main.TriggerHeld = true; PreviewFrame(main, off);
                check(app.Session.items.Count == 0, label + "placement rejects the same rotated overlap shown by the preview");
                main.TriggerDown = main.TriggerHeld = false;
                main.Ray = new Ray(head.position, (new Vector3(0.7f, -0.007f, 0.7f) - head.position).normalized);
                app.RoomRoot.rotation = Quaternion.Euler(0, 37, 0); Set("fitPreviewYaw", 23f); PreviewFrame(main, off);
                main.TriggerDown = main.TriggerHeld = true; PreviewFrame(main, off);
                check(app.Session.items.Count == 1 && Quaternion.Angle(app.RoomRoot.rotation * app.Session.items[0].rotation, Quaternion.Euler(0, 23, 0)) < 0.01f,
                    label + "a rotated room anchor preserves preview-to-placement world orientation");
                Set("frameSampled", false); FreshItems(); app.RoomRoot.SetPositionAndRotation(roomPosition, roomRotation);
            }
            string report = $"All {count} preview rotation checks passed."; File.AppendAllText(reportPath, report + "\n"); return report;
        }
        catch (Exception e) { File.AppendAllText(reportPath, "FAIL: " + e + "\n"); throw; }
        finally
        {
            Set("frameSampled", false); Set("interactionOffHand", false); Invoke("CancelGrab", true); Set("state", default(ToolInput)); FreshItems();
            Set("fitPreviewYaw", originalYaw); app.RoomRoot.SetPositionAndRotation(roomPosition, roomRotation);
            app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(prefs); app.StorageDirectoryOverride = storage;
            head.SetPositionAndRotation(headPosition, headRotation); palette.ResetWristPose(); app.enabled = wasEnabled;
        }
    }

    static void PreviewFrame(ToolInput main, ToolInput off)
    {
        Set("primaryState", main); Set("offState", off); Set("frameSampled", true);
        Physics.SyncTransforms(); Call("ChooseInteractionHand"); Call("UpdateInteraction");
    }

    [MenuItem("PICO/Spatial Toolkit/Run Preview Ownership Checks")]
    public static string RunPreviewOwnershipChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        if (!EditorApplication.isPlaying || !app) throw new Exception("Enter workbench Play Mode first.");
        bool wasEnabled = app.enabled; app.enabled = false; app.SendMessage("OnApplicationFocus", true);
        string prefs = JsonUtility.ToJson(app.Preferences), storage = app.StorageDirectoryOverride;
        var head = app.ViewCamera.transform; Vector3 headPosition = head.position; Quaternion headRotation = head.rotation;
        const string reportPath = ".tools/checks/toolkit-preview-ownership.txt";
        Directory.CreateDirectory(".tools/checks/preview-session"); app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/preview-session");
        File.WriteAllText(reportPath, "Main-hand preview and off-hand dimension drag checks\n"); int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); count++; File.AppendAllText(reportPath, "PASS: " + label + "\n"); };
        try
        {
            head.SetPositionAndRotation(new Vector3(0, 1.6f, -2), Quaternion.identity);
            foreach (bool left in new[] { false, true }) foreach (bool gesture in new[] { false, true })
                foreach (int tool in new[] { 1, 2 })
                {
                    Invoke("CancelGrab", true); Set("frameSampled", false); Set("interactionOffHand", false); Set("state", default(ToolInput)); FreshItems();
                    app.Preferences.leftHanded = left; app.Preferences.imperial = false; app.Preferences.wristGestureDisabled = true;
                    app.Preferences.furniture = new Vector3(0.5f, 0.8f, 0.5f); app.Preferences.frame = new Vector3(0.4f, 0.5f, 0);
                    app.Dispatch("tool:" + tool); palette.ResetWristPose();
                    var main = new ToolInput { Tracked = true, LeftHand = left, Gesture = gesture, Source = "Main replay", Rotation = Quaternion.identity };
                    palette.UpdatePose(main, head); app.Dispatch("pin"); palette.transform.position = head.position - Vector3.forward;
                    string key = tool == 1 ? "width" : "framewidth";
                    Vector3 surface = tool == 1 ? new Vector3(0.7f, -0.007f, 0.7f) : new Vector3(0.7f, 1.4f, 2.497f);
                    main.Ray = new Ray(head.position, (surface - head.position).normalized);
                    Vector3 row = Button("edit:" + key).transform.position;
                    var off = new ToolInput { Tracked = true, LeftHand = !left, Gesture = gesture, Source = "Off replay", Position = head.position,
                        Rotation = Quaternion.identity, Ray = new Ray(head.position, (row - head.position).normalized) };
                    string label = (left ? "Left main" : "Right main") + (gesture ? " pinch " : " controller ") + tool + ": ";
                    PreviewFrame(main, off);
                    var preview = (GameObject)Get(tool == 1 ? "fitPreview" : "hangPreview"); Vector3 initial = preview.transform.position;
                    check(preview.activeSelf, label + "unplaced preview starts on the main hand's surface");
                    off.TriggerDown = off.TriggerHeld = true; off.GripHeld = gesture; PreviewFrame(main, off);
                    check((bool)Get("interactionOffHand") && (string)Get("dimensionKey") == key, label + "off-hand captures the dimension row");
                    off.TriggerDown = false; off.Ray = new Ray(head.position, (row + palette.transform.right * 0.5f - head.position).normalized);
                    Set("dimensionAt", Time.unscaledTime - 0.3f); PreviewFrame(main, off);
                    preview = (GameObject)Get(tool == 1 ? "fitPreview" : "hangPreview");
                    check(!(bool)Get("menuBlocked") && (string)Get("dimensionKey") == key && (bool)Get("interactionOffHand"), label + "dimension drag stays captured beyond the entire panel");
                    check(preview.activeSelf && Vector3.Distance(initial, preview.transform.position) < 0.001f, label + "leaving the UI never moves the preview to the off-hand surface");
                    check((tool == 1 ? app.Preferences.furniture.x : app.Preferences.frame.x) > (tool == 1 ? 0.5f : 0.4f), label + "dimension continues increasing outside the UI");
                    if (tool == 1) check(preview.transform.localScale == app.Preferences.furniture, label + "preview size updates live outside the UI");
                    surface.x += 0.2f; main.Ray = new Ray(head.position, (surface - head.position).normalized); PreviewFrame(main, off);
                    preview = (GameObject)Get(tool == 1 ? "fitPreview" : "hangPreview");
                    check(Mathf.Abs(preview.transform.position.x - surface.x) < 0.02f && preview.activeSelf, label + "main hand continues moving its preview during the off-hand drag");
                    check(((ToolInput)Get("state")).Source == off.Source, label + "preview sampling preserves the captured editing hand");
                    off.TriggerHeld = off.GripHeld = false; off.GripUp = gesture; PreviewFrame(main, off);
                    check(Get("dimensionKey") == null && app.Session.items.Count == 0, label + "release finishes editing without placing");
                    off.GripUp = false; PreviewFrame(main, off);
                    check(!(bool)Get("interactionOffHand"), label + "normal main-hand interaction resumes after release");
                    off.TriggerDown = off.TriggerHeld = true; PreviewFrame(main, off);
                    check(app.Session.items.Count == 0, label + "off-hand empty-space select cannot place a new item");
                    off.TriggerDown = off.TriggerHeld = false; main.TriggerDown = main.TriggerHeld = true; PreviewFrame(main, off);
                    check(app.Session.items.Count == 1 && Mathf.Abs(app.RoomRoot.TransformPoint(app.Session.items[0].position).x - surface.x) < 0.02f,
                        label + "main hand places the resized item on its own surface");
                    Set("frameSampled", false); FreshItems(); main.LeftHand = !left; Set("state", default(ToolInput));
                    PreviewFrame(main, off); Invoke("Place"); preview = (GameObject)Get(tool == 1 ? "fitPreview" : "hangPreview");
                    check(app.Session.items.Count == 0 && (!preview || !preview.activeSelf), label + "a fallback to the unselected hand cannot own placement or previews");
                }
            string report = $"All {count} preview ownership checks passed."; File.AppendAllText(reportPath, report + "\n"); return report;
        }
        catch (Exception e) { File.AppendAllText(reportPath, "FAIL: " + e + "\n"); throw; }
        finally
        {
            Set("frameSampled", false); Set("interactionOffHand", false); Invoke("CancelGrab", true); Set("state", default(ToolInput)); FreshItems();
            app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(prefs); app.StorageDirectoryOverride = storage;
            head.SetPositionAndRotation(headPosition, headRotation); palette.ResetWristPose(); app.enabled = wasEnabled;
        }
    }
}
