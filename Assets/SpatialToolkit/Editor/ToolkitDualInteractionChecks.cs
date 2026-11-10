using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;

public static partial class ToolkitFeatureChecks
{
    public static string RunExistingRegressionChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        app.enabled = false;
        FreshItems(); string report = Run();
        FreshItems(); report += "\n" + RunSelectionChecks();
        FreshItems(); report += "\n" + RunHotfixChecks();
        FreshItems(); report += "\n" + RunRoomFeedbackChecks();
        FreshItems(); return report;
    }
    [MenuItem("PICO/Spatial Toolkit/Run Dual Hand and Dimension Checks")]
    public static string RunDualInteractionChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        if (!EditorApplication.isPlaying || !app) throw new Exception("Enter workbench Play Mode first.");
        bool wasEnabled = app.enabled; app.enabled = false;
        app.SendMessage("OnApplicationFocus", true);
        string prefs = JsonUtility.ToJson(app.Preferences), storage = app.StorageDirectoryOverride;
        var head = app.ViewCamera.transform; var headPosition = head.position; var headRotation = head.rotation;
        string path = ".tools/checks/toolkit-dual-interaction.txt";
        Directory.CreateDirectory(".tools/checks/dual-session"); app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/dual-session");
        File.WriteAllText(path, "Dual hand, precision editing, preview and menu checks\n"); int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); count++; File.AppendAllText(path, "PASS: " + label + "\n"); };
        Func<string, bool> has = action => palette.GetComponentsInChildren<ToolkitPaletteAction>().Any(b => b.Action == action);
        Invoke("CancelGrab", true);
        try
        {
            Set("frameSampled", false); FreshItems();
            head.SetPositionAndRotation(new Vector3(0, 1.6f, -2), Quaternion.identity);
            app.Preferences.wristGestureDisabled = true; app.Preferences.imperial = false;
            palette.ResetWristPose(); palette.UpdatePose(new ToolInput { Tracked = true, Source = "Dual replay" }, head);
            app.Dispatch("pin"); palette.transform.position = head.position - Vector3.forward;
            app.Dispatch("controls");
            check(!has("reach:1") && !has("rotate:1"), "Controls contains neither Reach nor Fit rotation");
            app.Dispatch("settings"); check(has("reach:-1") && has("reach:1"), "Reach controls are in Settings");
            app.Dispatch("main"); check(has("hidemenu") && !has("snapshot"), "Hide menu replaces the unsupported snapshot action");
            app.Dispatch("tool:1"); check(!has("rotate:1"), "Rotation buttons require a selected Fit box");
            var box = new ToolkitItemData { kind = ToolkitItemKind.Furniture, dimensions = new Vector3(0.5f, 0.8f, 0.5f), position = app.RoomRoot.InverseTransformPoint(new Vector3(0, 0.405f, 0.5f)) };
            app.Session.items.Add(box); Call("RenderAll"); Select(box);
            float[] rows = { Button("edit:width").transform.localPosition.y, Button("edit:height").transform.localPosition.y,
                Button("edit:depth").transform.localPosition.y, Button("rotate:1").transform.localPosition.y, Button("delete").transform.localPosition.y };
            check(Enumerable.Range(1, 4).All(i => Mathf.Abs(rows[i - 1] - rows[i] - 60) < 0.01f), "All five Fit editing rows have identical 12-unit edge gaps");
            check(rows[4] - Button("undo").transform.localPosition.y > 60, "Delete has a larger gap before the persistent controls");

            var row = Button("edit:width"); var p = row.transform.position;
            Aim(p, true); check((string)Get("dimensionKey") == "width" && !has("num:apply"), "Press captures a dimension without immediately opening the keypad");
            Aim(p); check(has("num:apply") && Get("dimensionKey") == null, "Short press and release opens the keypad");
            app.Dispatch("num:cancel"); row = Button("edit:width"); p = row.transform.position;
            int undoCount = ((List<string>)Get("undo")).Count; float originalWidth = box.dimensions.x;
            Aim(p, true); Set("dimensionAt", Time.unscaledTime - 0.3f);
            Aim(p + palette.transform.right * 0.1f, held: true);
            check(box.dimensions.x > originalWidth && !has("num:apply"), "Holding and dragging right grows the selected box without a keypad");
            float bigger = box.dimensions.x; Aim(p - palette.transform.right * 0.1f, held: true);
            check(box.dimensions.x < originalWidth && box.dimensions.x < bigger, "Dragging left reduces the same live dimension");
            Aim(p); check(((List<string>)Get("undo")).Count == undoCount + 1, "A complete numeric drag creates exactly one undo record");
            app.UndoAction(); box = app.Selection;
            check(Mathf.Abs(box.dimensions.x - originalWidth) < 0.0001f, "Undo restores the size from before the complete drag");
            p = Button("edit:height").transform.position; float height = box.dimensions.y;
            Aim(p, true); Set("dimensionAt", Time.unscaledTime - 0.3f); Aim(p + palette.transform.right * 0.1f, held: true);
            Invoke("OnApplicationFocus", false); Invoke("OnApplicationFocus", true); box = app.Selection;
            check(Get("dimensionKey") == null && Mathf.Abs(box.dimensions.y - height) < 0.0001f, "Focus loss rolls back an unfinished dimension drag");

            foreach (var axis in new[] { Vector3.forward, Vector3.right, new Vector3(0.4f, 0.2f, 1).normalized })
                foreach (float angle in new[] { -90f, -30f, 30f, 90f })
                {
                    Quaternion start = Quaternion.LookRotation(axis, Vector3.up);
                    float twist = SpatialToolkitApp.TwistDegrees(start, Quaternion.AngleAxis(angle, axis) * start, axis);
                    check(Mathf.Abs(Mathf.DeltaAngle(twist, angle)) < 0.001f, "Wrist twist follows roll " + angle + " about captured ray " + axis);
                }
            var sample = new ToolInput { Tracked = true, Gesture = true, Rotation = Quaternion.identity, Ray = new Ray(Vector3.zero, Vector3.forward) };
            Set("state", sample); Call("BeginWristRotation"); sample.Rotation = Quaternion.AngleAxis(31, Vector3.forward); Set("state", sample);
            float snapped = (float)typeof(SpatialToolkitApp).GetMethod("WristYaw", Private).Invoke(app, null);
            check(snapped == 30, "Single-hand wrist rotation snaps to 15 degree steps");
            var center = new Vector3(0, 0.405f, 0.5f); var la = Vector3.left * 0.3f; var lb = Vector3.right * 0.3f;
            var turn = Quaternion.Euler(0, 37, 0);
            SpatialToolkitApp.SolveTwoHandPose(center + turn * la, center + turn * lb, la, lb, Vector3.right, Quaternion.identity, out var solved, out var rotation);
            check(Vector3.Distance(center, solved) < 0.0001f && Quaternion.Angle(turn, rotation) < 0.01f, "Two-hand anchors produce continuous unsnapped rotation");
            SpatialToolkitApp.SolveTwoHandPose(center + turn * la * 2, center + turn * lb * 2, la, lb, Vector3.right, Quaternion.identity, out solved, out rotation);
            check(Vector3.Distance(center, solved) < 0.0001f && Quaternion.Angle(turn, rotation) < 0.01f && box.dimensions == new Vector3(originalWidth, height, 0.5f), "Moving hands apart changes neither box scale nor rigid pose");

            var primary = new ToolInput { Tracked = true, Source = "Right controller", Ray = new Ray(head.position, Vector3.forward), Rotation = Quaternion.identity };
            var off = new ToolInput { Tracked = true, Source = "Left controller", LeftHand = true, TriggerDown = true, TriggerHeld = true, Ray = new Ray(head.position, (Button("units").transform.position - head.position).normalized) };
            Set("state", primary); Set("primaryState", primary); Set("offState", off); Call("ChooseInteractionHand");
            check((bool)Get("interactionOffHand"), "Off-hand can capture a floating UI interaction");
            Set("frameSampled", true); Call("UpdateInteraction"); Set("frameSampled", false);
            check(app.Preferences.imperial, "Off-hand trigger activates the actual Units button");
            off.TriggerDown = off.TriggerHeld = false; Set("state", off); Set("offState", off); Call("ChooseInteractionHand");
            Set("state", primary); off.TriggerDown = off.TriggerHeld = true; off.Ray = new Ray(head.position, Vector3.up);
            Set("offState", off); Call("ChooseInteractionHand");
            check(!(bool)Get("interactionOffHand"), "Off-hand empty-space presses do not take placement ownership");
            Set("state", primary); off.Ray = new Ray(head.position, (center - head.position).normalized); Set("offState", off); Call("ChooseInteractionHand");
            check((bool)Get("interactionOffHand"), "Off-hand can capture an existing Fit box");
            Set("frameSampled", false); Set("state", primary);

            Set("interactionOffHand", false); Select(box); int beforeRotate = ((List<string>)Get("undo")).Count;
            sample = primary; sample.RotateAxis = 1; Set("state", sample); Quaternion beforeRotation = box.rotation;
            Call("UpdateStickRotation"); float expectedTurn = 30 * Time.unscaledDeltaTime;
            check(Quaternion.Angle(box.rotation, beforeRotation) > 0 && Mathf.Abs(Quaternion.Angle(box.rotation, beforeRotation) - expectedTurn) < 0.01f, "Thumbstick rotates at 30 degrees per second without snapping");
            Call("UpdateStickRotation"); Call("EndStickRotation");
            check(((List<string>)Get("undo")).Count == beforeRotate + 1, "A sustained thumbstick rotation creates one undo record");
            box.rotation = Quaternion.identity; Call("RenderAll"); Select(box);
            var handA = new Vector3(-0.3f, 1, -1); var handB = new Vector3(0.3f, 1, -1);
            sample = new ToolInput { Tracked = true, Gesture = true, Gaze = true, Source = "Right hand + eye gaze", TriggerHeld = true, GripHeld = true,
                Position = handA, Rotation = Quaternion.identity, Ray = new Ray(head.position, (center - head.position).normalized) };
            off = sample; off.Source = "Left hand + eye gaze"; off.LeftHand = true; off.Position = handB;
            Set("state", sample); Set("primaryState", sample); Set("offState", off); Set("interactionOffHand", false); Set("frameSampled", true);
            var handle = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().Single(h => h.ItemId == box.id);
            Physics.SyncTransforms(); Invoke("BeginGrabHandle", handle); Invoke("UpdateTwoHandGrab", false);
            check((bool)Get("twoHandGrab"), "Simultaneous held pinches join the same box even after the first hand hold delay");
            Vector3 mid = (handA + handB) * 0.5f; var rigidTurn = Quaternion.Euler(0, 37, 0); Vector3 dimensions = box.dimensions;
            sample.Position = mid + rigidTurn * (handA - mid); off.Position = mid + rigidTurn * (handB - mid);
            Set("state", sample); Set("offState", off); Invoke("UpdateTwoHandGrab", false);
            check(Mathf.Abs(Mathf.DeltaAngle(box.rotation.eulerAngles.y, 37)) < 0.01f && box.dimensions == dimensions, "Actual eye-pinch two-hand manipulation rotates 37 degrees without resizing");
            sample.TriggerHeld = sample.GripHeld = false; Set("state", sample); Invoke("UpdateTwoHandGrab", false);
            check(!(bool)Get("twoHandGrab") && (bool)Get("interactionOffHand") && Get("grabbed") != null, "Releasing the first hand transfers the grab to the remaining hand");
            Vector3 handoffPosition = box.position; Set("validSurface", true); Set("targetNormal", Vector3.up); Call("UpdateGrab");
            check(Vector3.Distance(handoffPosition, box.position) < 0.0001f, "Continuing after two-hand release preserves the box pose without jumping");
            Invoke("CancelGrab", true); Set("frameSampled", false); Set("interactionOffHand", false); box = app.Selection;

            app.Preferences.wristGestureDisabled = false; app.Dispatch("hand");
            var wrist = new ToolInput { Tracked = true, Source = "Wrist replay", PaletteTracked = true, PalettePosition = head.position + Vector3.left * 0.4f, PaletteUp = Vector3.up };
            palette.UpdatePose(wrist, head);
            check(palette.AttachedToWrist && Vector3.Distance(palette.transform.position, wrist.PalettePosition) < 0.5f, "Hand switching clears pinning and attaches to the newly supplied wrist immediately");
            app.Preferences.wristGestureDisabled = true; palette.ResetWristPose(); palette.UpdatePose(primary, head);
            palette.transform.position = head.position - Vector3.forward; app.Dispatch("pin");
            sample = primary; sample.Gaze = true; sample.Source = "Gaze replay"; sample.Ray = new Ray(head.position, Vector3.forward);
            Set("state", sample); Call("UpdateInteraction");
            check(Mathf.Abs(((Transform)Get("reticle")).GetComponent<Renderer>().sharedMaterial.color.a - 0.6f) < 0.0001f, "Gaze dot uses 0.6 alpha");

            FreshItems(); app.Dispatch("tool:3"); Aim(new Vector3(1, 1.4f, 2.497f));
            var preview = (GameObject)Get("levelPreview"); check(preview && preview.activeSelf, "Level grid previews on a vertical wall before placement");
            var previewPosition = preview.transform.position; Aim(Button("units").transform.position);
            check(preview.activeSelf && Vector3.Distance(previewPosition, preview.transform.position) < 0.0001f, "Level preview retains its wall pose while interacting with the menu");
            Aim(new Vector3(1, 0, 0)); check(!preview.activeSelf, "Level preview hides on a floor instead of a wall");
            app.Dispatch("hidemenu"); check(app.MenuHidden && !palette.Visible, "Hide menu immediately removes the palette");
            Aim(new Vector3(1, 1.4f, 2.497f)); Aim(new Vector3(1, 1.4f, 2.497f), true);
            check(!app.MenuHidden && app.Session.items.Count == 0, "The next select restores the menu without accidentally placing a measurement");
            string report = $"All {count} dual hand and dimension checks passed."; File.AppendAllText(path, report + "\n"); return report;
        }
        catch (Exception e) { File.AppendAllText(path, "FAIL: " + e + "\n"); throw; }
        finally
        {
            Set("frameSampled", false); Set("interactionOffHand", false); Invoke("CancelGrab", true);
            app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(prefs); app.StorageDirectoryOverride = storage;
            // The caller runs each suite in its own fresh scene; preserve preferences and head pose here.
            head.SetPositionAndRotation(headPosition, headRotation); app.enabled = wasEnabled;
        }
    }
}
