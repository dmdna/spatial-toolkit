using System;
using System.IO;
using System.Linq;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;

public static partial class ToolkitFeatureChecks
{
    public static string RunHorizontalLevelChecks()
    {
        app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        palette = UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>();
        if (!EditorApplication.isPlaying || !app) throw new Exception("Enter workbench Play Mode first.");
        bool enabled = app.enabled; app.enabled = false;
        string prefs = JsonUtility.ToJson(app.Preferences), storage = app.StorageDirectoryOverride;
        var head = app.ViewCamera.transform; var headPosition = head.position; var headRotation = head.rotation;
        var roomPosition = app.RoomRoot.position; var roomRotation = app.RoomRoot.rotation;
        Directory.CreateDirectory(".tools/checks/horizontal-level-session");
        app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/horizontal-level-session");
        const string reportPath = ".tools/checks/toolkit-horizontal-level.txt";
        File.WriteAllText(reportPath, "Horizontal Level checks\n"); int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); count++; File.AppendAllText(reportPath, "PASS: " + label + "\n"); };
        try
        {
            app.SendMessage("OnApplicationFocus", true); Set("frameSampled", false); FreshItems();
            head.SetPositionAndRotation(new Vector3(0, 1.6f, -2), Quaternion.identity);
            app.Preferences.wristGestureDisabled = true; app.Dispatch("tool:3");
            palette.ResetWristPose(); palette.UpdatePose(new ToolInput { Source = "Level replay" }, head);
            app.Dispatch("pin"); palette.transform.position = head.position - Vector3.forward;
            Aim(new Vector3(1, 1.4f, 2.497f));
            var preview = (GameObject)Get("levelPreview");
            check(preview.activeSelf && preview.GetComponentsInChildren<ToolkitLevelGridFade>().Length == 0, "Wall preview remains planar");
            Aim(new Vector3(0.7f, -0.007f, 0.7f)); preview = (GameObject)Get("levelPreview");
            check(preview.activeSelf && Vector3.Dot(preview.transform.forward, Vector3.up) > 0.999f, "Floor aim automatically swaps to a horizontal preview");
            check(preview.GetComponentsInChildren<ToolkitLevelGridFade>().Length == 2, "Horizontal preview has both vertical half-grids");
            check(Mathf.Abs(Mathf.DeltaAngle((float)Get("levelPreviewYaw"), Mathf.Round((float)Get("levelPreviewYaw") / 90) * 90)) < 0.01f, "Scanned room walls initialize perpendicular planar axes");
            Set("levelPreviewYaw", 37f); Aim(new Vector3(0.7f, -0.007f, 0.7f));
            var expected = preview.transform.rotation;
            Place(new Vector3(0.7f, -0.007f, 0.7f));
            var grid = app.Session.items.Single(i => i.kind == ToolkitItemKind.Level);
            var path = app.Session.items.Single(i => i.kind == ToolkitItemKind.AnglePath);
            check(grid.horizontalLevel && path.gridId == grid.id && path.points.Count == 1, "Floor placement retains the linked Level path");
            check(Quaternion.Angle(app.RoomRoot.rotation * grid.rotation, expected) < 0.01f, "Floor placement preserves exact preview rotation");
            app.Dispatch("finish"); Select(grid);
            check(app.Selection.id == grid.id && palette.GetComponentsInChildren<ToolkitPaletteAction>().Any(a => a.Action == "rotate:1"), "Selecting a horizontal grid offers rotation buttons");
            var initial = app.RoomRoot.rotation * grid.rotation; app.Dispatch("rotate:1");
            check(Quaternion.Angle(app.RoomRoot.rotation * grid.rotation, Quaternion.AngleAxis(15, Vector3.up) * initial) < 0.01f, "Menu rotation turns a placed horizontal grid 15 degrees");
            grid = app.Session.items.Single(i => i.kind == ToolkitItemKind.Level); Select(grid);
            var turn = new ToolInput { Tracked = true, Source = "Level controller", RotateAxis = 1 };
            Set("state", turn); initial = app.RoomRoot.rotation * grid.rotation; Call("UpdateStickRotation");
            check(Quaternion.Angle(app.RoomRoot.rotation * grid.rotation, Quaternion.AngleAxis(30 * Time.unscaledDeltaTime, Vector3.up) * initial) < 0.01f, "Placed grid thumbstick rotation matches Fit speed");
            Call("EndStickRotation");
            var centerHandle = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().First(h => h.ItemId == path.id && h.Node == 0);
            var hand = new ToolInput { Tracked = true, Gesture = true, GripHeld = true, TriggerHeld = true,
                Source = "Level pinch", Ray = new Ray(head.position, Vector3.forward), Rotation = Quaternion.identity };
            Set("state", hand); Invoke("BeginGrabHandle", centerHandle);
            check(((ToolkitHandle)Get("grabbed")).ItemId == grid.id, "Grabbing the center node captures the horizontal grid for Fit-style rotation");
            initial = app.RoomRoot.rotation * grid.rotation;
            Set("target", app.RoomRoot.TransformPoint(grid.position) + Vector3.right * 0.2f); Set("validSurface", true); Set("targetNormal", Vector3.up);
            hand.Rotation = Quaternion.AngleAxis(20, Vector3.forward); Set("state", hand); Call("UpdateGrab");
            check(Quaternion.Angle(app.RoomRoot.rotation * grid.rotation, Quaternion.AngleAxis(15, Vector3.up) * initial) < 0.01f, "Single-hand wrist twist rotates a floor grid in 15-degree steps");
            check(path.points[0] == grid.position, "Moving the horizontal grid keeps its first path node attached");
            hand.GripHeld = hand.TriggerHeld = false; hand.GripUp = true; Set("state", hand); Call("UpdateGrab");
            check(Get("grabbed") == null, "Releasing a horizontal grid commits on a horizontal surface");
            Vector3 center = app.RoomRoot.TransformPoint(grid.position);
            var first = new ToolInput { Tracked = true, GripHeld = true, Source = "Level left controller",
                Ray = new Ray(center + Vector3.left * 0.25f + Vector3.up, Vector3.down), Rotation = Quaternion.identity };
            var second = first; second.Source = "Level right controller";
            second.Ray = new Ray(center + Vector3.right * 0.25f + Vector3.up, Vector3.down);
            Set("state", first); Set("primaryState", first); Set("offState", second); Set("frameSampled", true); Set("interactionOffHand", false);
            var gridHandle = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().First(h => h.ItemId == grid.id);
            Invoke("BeginGrabHandle", gridHandle); Physics.SyncTransforms(); Invoke("UpdateTwoHandGrab", false);
            check((bool)Get("twoHandGrab"), "Both controllers can capture a horizontal grid");
            initial = app.RoomRoot.rotation * grid.rotation;
            var yawTurn = Quaternion.AngleAxis(25, Vector3.up);
            first.Ray = new Ray(center + yawTurn * (Vector3.left * 0.25f) + Vector3.up, Vector3.down);
            second.Ray = new Ray(center + yawTurn * (Vector3.right * 0.25f) + Vector3.up, Vector3.down);
            Set("state", first); Set("offState", second); Invoke("UpdateTwoHandGrab", false);
            check(Quaternion.Angle(app.RoomRoot.rotation * grid.rotation, yawTurn * initial) < 0.1f, "Two-hand rotation turns the horizontal grid continuously without snapping");
            check(Vector3.Distance(center, app.RoomRoot.TransformPoint(grid.position)) < 0.001f && path.points[0] == grid.position, "Two-hand rotation preserves floor contact and the linked path");
            first.GripHeld = second.GripHeld = false; Set("state", first); Set("offState", second); Invoke("UpdateTwoHandGrab", false);
            check(Get("grabbed") == null && !(bool)Get("twoHandGrab"), "Releasing both controllers commits the horizontal grid");
            Set("frameSampled", false);
            var saved = JsonUtility.FromJson<ToolkitSession>(JsonUtility.ToJson(app.Session));
            check(saved.items.Single(i => i.id == grid.id).horizontalLevel && saved.items.Single(i => i.id == path.id).gridId == grid.id, "Save data retains horizontal type and path link");
            var root = app.RoomRoot.GetComponentsInChildren<ToolkitHandle>().Single(h => h.ItemId == grid.id).transform;
            var halves = root.GetComponentsInChildren<ToolkitLevelGridFade>();
            foreach (var fade in halves)
            {
                float previous = -1;
                for (int angle = 0; angle <= 90; angle++)
                {
                    var direction = Quaternion.AngleAxis(angle, root.forward) * fade.transform.right;
                    float alpha = ToolkitLevelGridFade.Visibility(fade.transform.up, direction);
                    check(alpha >= previous - 0.00001f && alpha >= 0 && alpha <= 1, "Orbit fade is smooth and monotonic: " + fade.name + " / " + angle);
                    previous = alpha;
                }
                head.position = root.position + fade.transform.up * 2;
                fade.UpdateVisibility();
                check(fade.GetComponentsInChildren<LineRenderer>().All(l => l.sharedMaterial.color.a <= 0.1201f), "Vertical grid alpha is capped below floor-grid alpha");
                head.position = root.position + fade.transform.right * 2; fade.UpdateVisibility();
                check(fade.GetComponentsInChildren<LineRenderer>().All(l => l.sharedMaterial.color.a < 0.0001f), "Edge-on vertical grid becomes invisible");
            }
            head.SetPositionAndRotation(new Vector3(0, 1.6f, -2), Quaternion.identity);
            FreshItems(); app.Dispatch("tool:3"); palette.transform.position = head.position - Vector3.forward;
            Place(new Vector3(1, 1.4f, 2.497f));
            grid = app.Session.items.Single(i => i.kind == ToolkitItemKind.Level);
            check(!grid.horizontalLevel && Mathf.Abs((app.RoomRoot.rotation * grid.rotation * Vector3.forward).y) < 0.01f, "Wall placement remains vertical and unchanged");
            check(!JsonUtility.FromJson<ToolkitItemData>("{\"kind\":3}").horizontalLevel, "Older saved Level grids remain wall grids");
            check(!SpatialToolkitApp.LevelSurface(new Vector3(0.7f, 0.7f, 0)), "Steep slopes do not masquerade as walls or floors");
            foreach (bool left in new[] { false, true })
            {
                Set("frameSampled", false); FreshItems(); app.Dispatch("tool:3"); app.Preferences.leftHanded = left;
                palette.transform.position = head.position - Vector3.forward;
                var main = new ToolInput { Tracked = true, LeftHand = left, Source = "Level main", Rotation = Quaternion.identity,
                    Ray = new Ray(head.position, (new Vector3(0.7f, -0.007f, 0.7f) - head.position).normalized) };
                var off = new ToolInput { Tracked = true, LeftHand = !left, Source = "Level off", Ray = new Ray(head.position, Vector3.up) };
                PreviewFrame(main, off); float yaw = (float)Get("levelPreviewYaw");
                main.RotateAxis = 1; PreviewFrame(main, off);
                check(Mathf.Abs(Mathf.DeltaAngle(yaw, (float)Get("levelPreviewYaw")) - 30 * Time.unscaledDeltaTime) < 0.01f, "Main-hand horizontal preview rotates at Fit speed: " + left);
                main.RotateAxis = 0; off.RotateAxis = 1; yaw = (float)Get("levelPreviewYaw"); PreviewFrame(main, off);
                check(Mathf.Abs(Mathf.DeltaAngle(yaw, (float)Get("levelPreviewYaw"))) < 0.01f, "Off-hand cannot steal horizontal preview rotation: " + left);
                app.RoomRoot.rotation = Quaternion.Euler(0, 31, 0);
                var shown = ((GameObject)Get("levelPreview")).transform.rotation;
                off.RotateAxis = 0; main.TriggerDown = main.TriggerHeld = true; PreviewFrame(main, off);
                grid = app.Session.items.Single(i => i.kind == ToolkitItemKind.Level);
                check(Quaternion.Angle(app.RoomRoot.rotation * grid.rotation, shown) < 0.01f, "Room anchor rotation preserves horizontal preview placement: " + left);
                Set("frameSampled", false); FreshItems(); app.RoomRoot.rotation = roomRotation;
            }
            string report = "All " + count + " horizontal Level checks passed.";
            File.AppendAllText(reportPath, report + "\n"); return report;
        }
        catch (Exception e) { File.AppendAllText(reportPath, "FAIL: " + e + "\n"); throw; }
        finally
        {
            Invoke("CancelGrab", true); Set("frameSampled", false); Set("state", default(ToolInput)); FreshItems();
            app.RoomRoot.SetPositionAndRotation(roomPosition, roomRotation);
            app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(prefs); app.StorageDirectoryOverride = storage;
            head.SetPositionAndRotation(headPosition, headRotation); palette.ResetWristPose(); app.enabled = enabled;
        }
    }
}
