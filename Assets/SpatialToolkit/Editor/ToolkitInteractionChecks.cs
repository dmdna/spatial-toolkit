using System;
using System.IO;
using System.Linq;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ToolkitInteractionChecks
{
    public const string Report = ".tools/checks/workbench-interaction.txt";
    [MenuItem("PICO/Spatial Toolkit/Run Play Mode Interaction Checks")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in the empty workbench first.");
        var app = UnityEngine.Object.FindFirstObjectByType<SpatialToolkitApp>();
        if (!app || app.Session.items.Count != 0) throw new InvalidOperationException("Checks require an empty workbench.");
        Application.runInBackground = true;
        var background = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        var mouse = InputSystem.AddDevice<Mouse>("Toolkit verification mouse"); mouse.MakeCurrent();
        Directory.CreateDirectory(".tools/checks/workbench-session");
        File.WriteAllText(Report, "Spatial Toolkit interactive Play Mode checks\n");
        string preferences = JsonUtility.ToJson(app.Preferences), oldStorage = app.StorageDirectoryOverride;
        app.StorageDirectoryOverride = Path.GetFullPath(".tools/checks/workbench-session");
        app.Preferences.imperial = false;
        // Surface tests aim behind the default menu position; move the pinned test
        // palette behind the camera now that the complete panel blocks rays.
        app.Dispatch("pin");
        UnityEngine.Object.FindFirstObjectByType<ToolkitPalette>().transform.position = app.ViewCamera.transform.position - app.ViewCamera.transform.forward;
        int step = 0; double next = EditorApplication.timeSinceStartup + 0.5, started = next;
        Action<Vector3, int> point = (world, buttons) =>
        {
            var p = app.ViewCamera.WorldToScreenPoint(world); mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(p.x, p.y), buttons = (ushort)buttons });
            InputSystem.Update();
        };
        Action<bool, string> check = (ok, name) =>
        { if (!ok) throw new Exception(name); File.AppendAllText(Report, "PASS: " + name + "\n"); };
        Action cleanup = () =>
        {
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            if (app) { app.StorageDirectoryOverride = oldStorage; app.Preferences = JsonUtility.FromJson<ToolkitPreferences>(preferences); }
        };
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            try
            {
                if (!app || !EditorApplication.isPlaying) throw new Exception("Play Mode stopped before checks completed.");
                if (EditorApplication.timeSinceStartup - started > 30) throw new Exception("Interaction checks timed out.");
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + 0.25;
                switch (step++)
                {
                    case 0: app.SendMessage("OnApplicationFocus", true); app.Dispatch("tool:0"); point(new Vector3(0.1f,1,2.49f),0); break;
                    case 1: point(new Vector3(0.1f,1,2.49f),1); break;
                    case 2: point(new Vector3(0.1f,1,2.49f),0); break;
                    case 3: check(app.Session.items.Count==1 && app.Session.items[0].points.Count==1,"Trigger creates the first surface node"); point(new Vector3(1.1f,1,2.49f),1); break;
                    case 4: point(new Vector3(1.1f,1,2.49f),0); break;
                    case 5: point(new Vector3(1.1f,2,2.49f),1); break;
                    case 6: point(new Vector3(1.1f,2,2.49f),0); break;
                    case 7: point(new Vector3(0.1f,2,2.49f),1); break;
                    case 8: point(new Vector3(0.1f,2,2.49f),0); break;
                    case 9:
                        check(app.Session.items[0].points.Count==4,"Pointer creates four distinct wall nodes");
                        app.Dispatch("close");
                        check(app.Session.items[0].closed && ToolkitGeometry.TryArea(app.Session.items[0].points,out var area,out _) && Mathf.Abs(area-1)<0.03f,"Closed live wall outline reports one square meter"); break;
                    case 10:
                        app.UndoAction(); check(!app.Session.items[0].closed,"Undo reopens the outline");
                        app.UndoAction(); check(app.Session.items[0].points.Count==3,"Undo removes the last node"); app.Dispatch("finish"); break;
                    case 11: point(app.RoomRoot.TransformPoint(app.Session.items[0].points[0]),2); break;
                    case 12: point(new Vector3(0.2f,1.3f,2.49f),2); break;
                    case 13: point(new Vector3(0.2f,1.3f,2.49f),0); break;
                    case 14:
                        check(Vector3.Distance(app.RoomRoot.TransformPoint(app.Session.items[0].points[0]),new Vector3(0.2f,1.3f,2.49f))<0.04f,"Grip drags a node on the wall");
                        app.UndoAction();
                        check(Vector3.Distance(app.RoomRoot.TransformPoint(app.Session.items[0].points[0]),new Vector3(0.1f,1,2.49f))<0.04f,"Undo restores a dragged node");
                        app.Dispatch("tool:1"); app.Dispatch("edit:width"); app.Dispatch("num:1"); app.Dispatch("num:2"); app.Dispatch("num:5"); app.Dispatch("num:apply");
                        check(Mathf.Abs(app.Preferences.furniture.x-1.25f)<0.0001f,"Wrist keypad accepts exact dimensions in centimeters");
                        app.SetDimension("height",0.8f);app.SetDimension("depth",0.6f); point(new Vector3(1.4f,0,0.8f),0); break;
                    case 15: point(new Vector3(1.4f,0,0.8f),1); break;
                    case 16: point(new Vector3(1.4f,0,0.8f),0); break;
                    case 17: check(app.Session.items.Count(i=>i.kind==ToolkitItemKind.Furniture)==1,"Trigger places furniture flush to the floor"); point(new Vector3(1.4f,0,2.3f),1); break;
                    case 18: point(new Vector3(1.4f,0,2.3f),0); break;
                    case 19: check(app.Session.items.Count(i=>i.kind==ToolkitItemKind.Furniture)==1,"Collision checks reject furniture intersecting the back wall"); app.Dispatch("tool:2");point(new Vector3(-0.4f,1.2f,2.49f),0);break;
                    case 20: point(new Vector3(-0.4f,1.2f,2.49f),1);break;
                    case 21: point(new Vector3(-0.4f,1.2f,2.49f),0);break;
                    case 22: check(app.Session.items.Count(i=>i.kind==ToolkitItemKind.Frames)==1,"Trigger places the frame and nail layout");app.Dispatch("tool:3");point(new Vector3(-1.4f,1.2f,2.49f),0);break;
                    case 23: point(new Vector3(-1.4f,1.2f,2.49f),1);break;
                    case 24: point(new Vector3(-1.4f,1.2f,2.49f),0);break;
                    case 25: check(app.Session.items.Count(i=>i.kind==ToolkitItemKind.Level)==1,"Trigger places the level grid on a wall");app.Dispatch("save");break;
                    case 26: check(app.Status.Contains("saved"),"Editor session saves simulated coordinates");app.Dispatch("load");app.Dispatch("load");break;
                    case 27: check(app.Status.Contains("restored") && app.Session.items.Count==5,"Restore retains all four tools including the Level angle path");break;
                    default:
                        EditorApplication.update-=tick; cleanup();File.AppendAllText(Report,"All interactive checks passed.\n");
                        Debug.Log("Spatial Toolkit: all interactive workbench checks passed.");break;
                }
            }
            catch (Exception e)
            {
                EditorApplication.update-=tick; cleanup(); File.AppendAllText(Report,"FAIL: "+e+"\nApp: "+app.Status+" / "+app.InputSource+"\n"); Debug.LogException(e);
            }
        };
        EditorApplication.update+=tick;
    }
}
