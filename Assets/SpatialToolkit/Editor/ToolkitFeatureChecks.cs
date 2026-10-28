using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Replays actual raycasts, menu colliders and app actions in the editor room.
public static partial class ToolkitFeatureChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static SpatialToolkitApp app;
    static ToolkitPalette palette;
    static int checks;
    static object Get(string name) => typeof(SpatialToolkitApp).GetField(name, Private).GetValue(app);
    static void Set(string name, object value) => typeof(SpatialToolkitApp).GetField(name, Private).SetValue(app, value);
    static void Call(string name) => typeof(SpatialToolkitApp).GetMethod(name, Private).Invoke(app, null);
    static ToolkitPaletteAction Button(string action) => palette.GetComponentsInChildren<ToolkitPaletteAction>().Single(b => b.Action == action);
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        checks++; File.AppendAllText(".tools/checks/toolkit-features.txt", "PASS: " + label + "\n");
    }
    static void Aim(Vector3 point, bool press = false, bool held = false, bool gesture = false)
    {
        var head = app.ViewCamera.transform;
        var ray = new Ray(head.position, (point - head.position).normalized);
        Set("state", new ToolInput { Tracked = true, TriggerDown = press, TriggerHeld = held || press,
            Gesture = gesture, GripHeld = gesture && (held || press), Source = "Feature replay", Ray = ray,
            Position = head.position, Rotation = Quaternion.LookRotation(ray.direction), AlignmentForward = ray.direction });
        Physics.SyncTransforms(); Call("UpdateInteraction");
    }
    static void Place(Vector3 point)
    {
        Set("lastNodePress", -10f); Aim(point); Aim(point, true); Aim(point);
    }
    static string Active => Get("activePath") as string;

    [MenuItem("PICO/Spatial Toolkit/Run Menu and Path Feature Checks")]
    public static string Run()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        if (!EditorApplication.isPlaying || !app || app.Session.items.Count != 0) throw new Exception("Start in a fresh empty workbench Play Mode session.");
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        app.SendMessage("OnApplicationFocus", true);
        string originalPreferences = JsonUtility.ToJson(app.Preferences), originalStorage = app.StorageDirectoryOverride;
        bool wasEnabled = app.enabled;
        app.enabled = false;
        Directory.CreateDirectory(".tools/checks/feature-session");
        app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/feature-session");
        File.WriteAllText(".tools/checks/toolkit-features.txt", "Menu and path feature checks\n"); checks = 0;
        try
        {
            app.Preferences.wristGestureDisabled = false;
            Aim(new Vector3(1, 1, 2.49f));
            app.Dispatch("main");
            Check(Button("settings").GetComponentsInChildren<Text>().Length == 0 && Button("minimize").GetComponentsInChildren<Text>().Length == 0, "Gear and minimize controls use drawn icons");
            Check(Button("undo").transform.localPosition.x < Button("units").transform.localPosition.x && Button("units").transform.localPosition.x < Button("hand").transform.localPosition.x && Button("undo").transform.localPosition.y == Button("units").transform.localPosition.y, "Full menu matches compact Undo / units / hand order");
            var blank = palette.GetComponentInChildren<Canvas>().transform.TransformPoint(new Vector3(290, 20, 0));
            Aim(blank, true);
            Check(app.Session.items.Count == 0 && (bool)Get("menuBlocked"), "Panel gaps block placement on the surface behind them");
            var units = Button("units");
            Vector3 edge = units.transform.TransformPoint(new Vector3(65, 0, 0)); Aim(edge);
            var pointer = (LineRenderer)Get("pointer"); var sample = (ToolInput)Get("state");
            Check(Vector3.Cross((pointer.GetPosition(1) - pointer.GetPosition(0)).normalized, sample.Ray.direction).magnitude < 0.0001f && Vector3.Distance(pointer.GetPosition(1), units.transform.position) > 0.02f, "UI ray hits the pointed edge without rotating toward the button center");
            app.Dispatch("settings");
            Check(!palette.GetComponentsInChildren<ToolkitPaletteAction>().Any(b => b.Action.StartsWith("tool:")) && Button("about"), "Settings replace the tools on the same panel");
            app.Dispatch("about");
            var texts = palette.GetComponentsInChildren<Text>();
            Check(palette.GetComponentsInChildren<ToolkitPaletteAction>().Count(b => b.Action != "block") == 1 && texts.Any(t => t.text.Contains("Diego Medina Molina")) && texts.Any(t => t.text.Contains("PICO Vibe Lab")) && texts.Any(t => t.text.Contains(Application.version)), "About contains the developer, lab and current version, with only Back actionable");
            app.Dispatch("settings"); app.Dispatch("wrist");
            var head = app.ViewCamera.transform;
            var wrist = new ToolInput { PaletteTracked = true, PalettePosition = head.position + head.forward * 0.4f, PaletteUp = Vector3.down, Source = "Feature replay" };
            palette.UpdatePose(wrist, head); Vector3 floating = palette.transform.position;
            wrist.PalettePosition += Vector3.right; palette.UpdatePose(wrist, head);
            Check(palette.Visible && Vector3.Distance(floating, palette.transform.position) < 0.0001f, "Disabling wrist gesture keeps a world-pinned menu with two tracked hands");
            app.Dispatch("wrist"); app.Dispatch("main"); wrist.PaletteUp = Vector3.up;
            palette.UpdatePose(wrist, head);
            Check(Mathf.Abs(palette.GetComponentInChildren<Canvas>().transform.localScale.x - 0.00048f) < 0.000001f, "Wrist menu is twenty percent smaller");
            palette.ResetWristPose(); Aim(new Vector3(1, 1, 2.49f));
            app.Dispatch("minimize");
            Check(palette.GetComponentsInChildren<ToolkitPaletteAction>().Length == 4 && Button("expand") && Button("undo") && Button("units") && Button("hand"), "Compact bar retains only selection expansion and the three quick buttons");
            bool imperial = app.Preferences.imperial; Aim(Button("units").transform.position, true);
            Check(app.Preferences.imperial != imperial && Button("expand"), "Compact units button works without expanding the menu");
            Aim(Button("expand").transform.TransformPoint(new Vector3(-310, 0, 0)), true);
            Check(Button("settings"), "Selecting the compact tool label restores the full menu");
            // Keep the menu out of the wall-placement rays after testing its blocker.
            app.Dispatch("pin");
            palette.transform.SetPositionAndRotation(head.position + head.right * 0.9f + head.forward * 0.8f,
                Quaternion.LookRotation(head.right * 0.9f + head.forward * 0.8f, Vector3.up));
            app.Dispatch("tool:0");
            Place(new Vector3(0.3f, 1, 2.49f)); Place(new Vector3(1.3f, 1, 2.49f));
            Check(app.Session.items.Single().points.Count == 2 && Active != null, "Measure still chains nodes normally");
            Set("lastNodePress", Time.unscaledTime); Aim(new Vector3(1.3f, 1.5f, 2.49f), true);
            Check(Active == null && app.Session.items.Single().points.Count == 3, "Rapid second press places the final node and finishes Measure");
            Place(new Vector3(0.4f, 1.5f, 2.49f));
            int nodes = app.Session.items.Sum(i => i.points.Count);
            var oldNode = app.RoomRoot.TransformPoint(app.Session.items.Last().points.Last()); Aim(oldNode, true, gesture: true);
            Check(Active == null && app.Session.items.Sum(i => i.points.Count) == nodes && Get("grabbed") == null, "Pinching an existing snapped node finishes without duplication or dragging");
            app.Dispatch("snap"); Place(new Vector3(0.5f, 1.6f, 2.49f));
            oldNode = app.RoomRoot.TransformPoint(app.Session.items.Last().points.Last()); Aim(oldNode, true);
            Check(Active == null, "Free-placement path finishes only on the visible node collider");
            Place(new Vector3(0.6f, 1.7f, 2.49f)); Set("lastNodePress", -10f); Aim(oldNode + Vector3.right * 0.04f, true);
            Check(Active != null, "Free-placement input outside the node circle keeps placing");
            app.Dispatch("finish"); app.Dispatch("tool:3");
            Place(new Vector3(-1.6f, 0.8f, 2.49f)); Place(new Vector3(-0.6f, 1.8f, 2.49f));
            var angle = app.Session.items.Single(i => i.kind == ToolkitItemKind.AnglePath);
            var angleText = app.RoomRoot.GetComponentsInChildren<Text>().FirstOrDefault(t => t.text.Contains("Horizontal") && t.text.Contains("Vertical"));
            Check(angle.points.Count == 2 && angleText && angleText.text.Contains("45.0"), "Level draws connected nodes with horizontal and vertical angles");
            Set("lastNodePress", Time.unscaledTime); Aim(new Vector3(-0.4f, 2, 2.49f), true);
            Check(Active == null, "Rapid second press also finishes the Level angle path");
            app.Dispatch("tool:2"); Aim(new Vector3(0.8f, 1.8f, 2.49f));
            var preview = (GameObject)Get("hangPreview");
            Check(preview && preview.activeSelf && preview.GetComponentsInChildren<LineRenderer>().Length >= app.Preferences.frameCount * 3 && preview.GetComponentsInChildren<Collider>().Length == 0, "Hang shows frame outlines and nail marks before placement without intercepting rays");
            Vector3 previewPosition = preview.transform.position;
            app.Dispatch("count:1"); app.Dispatch("framewidth:1"); app.Dispatch("inset:1");
            Aim(Button("units").transform.position);
            preview = (GameObject)Get("hangPreview");
            Check(preview.activeSelf && Vector3.Distance(previewPosition, preview.transform.position) < 0.0001f && preview.GetComponentsInChildren<Text>().Length == app.Preferences.frameCount, "Hang preview stays on its wall and updates live while changing settings");
            Aim(new Vector3(0.8f, 1.8f, 2.49f), true);
            Check(app.Session.items.Any(i => i.kind == ToolkitItemKind.Frames && i.count == app.Preferences.frameCount), "Placed Hang geometry uses the live preview dimensions");
            Color original = ToolkitVisuals.Mint; app.Dispatch("colors:1");
            Check(ToolkitVisuals.Mint != original && app.RoomRoot.GetComponentsInChildren<LineRenderer>().Any(l => l.sharedMaterial.color == ToolkitVisuals.Mint), "Option 2 recolors existing room geometry immediately");
            app.Dispatch("colors:1");
            Check(ToolkitVisuals.Panel == Color.black && app.RoomRoot.GetComponentsInChildren<ToolkitLineOutline>().Length > 0, "Option 3 adds strong contrasting outlines and an opaque menu");
            app.Dispatch("colors:1"); Check(ToolkitVisuals.Mint == original, "Preset selection wraps back to Option 1");
            app.Dispatch("tool:0"); app.Dispatch("minimize");
            Aim(Button("undo").transform.position, true);
            Check(app.Status == "Undone. Hold the button to Clear All.", "Undo tap retains the requested status hint");
            Set("undoHeldSince", Time.unscaledTime - 0.3f); Aim(Button("undo").transform.position, held: true);
            Check(Button("undo").GetComponentInChildren<Text>().text == "Undo", "Clear All waits through the initial hold grace period");
            Set("undoHeldSince", Time.unscaledTime - 0.9f); Aim(Button("undo").transform.position, held: true);
            Check(Button("undo").GetComponentInChildren<Text>().text == "Clear All (Hold)" && palette.GetComponentsInChildren<Image>().Any(i => i.type == Image.Type.Filled && i.fillAmount > 0.2f && i.fillAmount < 0.6f), "Holding shows the Clear All label and smooth loading progress");
            int retained = app.Session.items.Count; Aim(Button("undo").transform.position);
            Check(app.Session.items.Count == retained && Button("undo").GetComponentInChildren<Text>().text == "Undo", "Releasing cancels Clear All and restores Undo");
            Aim(Button("undo").transform.position, true); Set("undoHeldSince", Time.unscaledTime - 0.9f);
            Aim(new Vector3(1.5f, 1.8f, 2.49f), held: true);
            Check((float)Get("undoHeldSince") < 0, "Moving off Undo cancels the hold");
            Aim(Button("undo").transform.position, true); Set("undoHeldSince", Time.unscaledTime - 1.7f); Aim(Button("undo").transform.position, held: true);
            Check(app.Session.items.Count == 0, "Completed hold clears every tool artifact");
            app.UndoAction(); Check(app.Session.items.Count > 0, "Clear All can itself be undone");
            Aim(Button("undo").transform.position, true); Set("undoHeldSince", Time.unscaledTime - 1f);
            typeof(SpatialToolkitApp).GetMethod("OnApplicationFocus", Private).Invoke(app, new object[] { false });
            Check((float)Get("undoHeldSince") < 0, "Focus loss cancels a pending destructive hold");
            typeof(SpatialToolkitApp).GetMethod("OnApplicationFocus", Private).Invoke(app, new object[] { true });
            SpatialToolkitApp.ValidateSession(JsonUtility.FromJson<ToolkitSession>(JsonUtility.ToJson(app.Session)));
            Check(true, "Saved sessions round-trip the new angle paths");
            string report = $"All {checks} menu and path feature checks passed.";
            File.AppendAllText(".tools/checks/toolkit-features.txt", report + "\n"); return report;
        }
        catch (Exception e)
        {
            File.AppendAllText(".tools/checks/toolkit-features.txt", "FAIL: " + e + "\n"); throw;
        }
        finally
        {
            app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(originalPreferences);
            app.StorageDirectoryOverride = originalStorage;
            app.enabled = wasEnabled;
            ToolkitVisuals.SetColorOption(app.Preferences.colorOption);
        }
    }
}
