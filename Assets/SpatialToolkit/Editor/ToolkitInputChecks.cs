using System;
using SpatialToolkit;
using UnityEngine;

// Deterministic replay of platform samples; no headset or native tracking calls.
public static class ToolkitInputChecks
{
    public static string Run()
    {
        int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("Input check failed: " + label); count++; };
        var input = new ToolkitInput();
        var sample = new ToolInput { Tracked = true, Gesture = true, Source = "Right hand + eye gaze", Rotation = Quaternion.identity,
            Ray = new Ray(Vector3.zero, Vector3.forward), Position = Vector3.zero };
        var s = input.ResolveSample(sample, false, false);
        check(s.SourceChanged && !s.TriggerDown, "Idle acquisition does not select");
        s = input.ResolveSample(sample, true, true);
        check(s.TriggerDown && s.GripDown && s.GripHeld, "Pinch begins selection and contextual grab");
        sample.Ray = new Ray(Vector3.zero, Vector3.right);
        sample.Position = new Vector3(0.1f, 0.2f, 0.3f);
        s = input.ResolveSample(sample, true, true);
        check(!s.TriggerDown && !s.GripDown && s.GripHeld, "Holding cannot repeat selection");
        check(Vector3.Distance(s.Ray.direction, Vector3.forward) < 0.0001f, "Looking elsewhere does not change captured drag direction");
        check(Vector3.Distance(s.Ray.origin, sample.Position * 3) < 0.0001f, "Hand translation moves the captured ray");
        sample.Source = "Right hand ray";
        s = input.ResolveSample(sample, true, true);
        check(!s.SourceChanged && s.GripHeld && Vector3.Distance(s.Ray.direction, Vector3.forward) < 0.0001f, "A blink or eye fallback cannot steal a held drag");
        s = input.ResolveSample(sample, false, false);
        check(s.GripUp && !s.GripHeld && !s.TriggerDown, "Release drops without another placement");
        s = input.ResolveSample(sample, false, false);
        check(!s.GripUp && Vector3.Distance(s.Ray.direction, Vector3.right) < 0.0001f, "Aim resumes after release");
        input.ResolveSample(sample, true, true);
        sample.Tracked = false;
        s = input.ResolveSample(sample, false, false);
        check(!s.TriggerDown && !s.GripUp, "Tracking loss cannot commit a release or throw");
        sample.Tracked = true;
        s = input.ResolveSample(sample, true, true);
        check(!s.TriggerDown && !s.GripHeld, "Reacquiring a pinched hand requires release");
        s = input.ResolveSample(sample, true, true);
        check(!s.TriggerDown && !s.GripHeld, "Held reacquisition stays suppressed");
        input.ResolveSample(sample, false, false);
        s = input.ResolveSample(sample, true, true);
        check(s.TriggerDown, "Release rearms the hand");
        sample.Source = "Left hand ray";
        s = input.ResolveSample(sample, true, true);
        check(s.SourceChanged && !s.TriggerDown && !s.GripHeld, "Changing hands cancels a held gesture");
        input.ResolveSample(sample, false, false);
        input.Mode = ToolkitInputMode.HeadPinch;
        s = input.ResolveSample(sample, true, true);
        check(s.SourceChanged && !s.TriggerDown, "Changing input mode cannot select accidentally");
        input.ResolveSample(sample, false, false);
        input.LeftHanded = true;
        s = input.ResolveSample(sample, true, true);
        check(s.SourceChanged && !s.TriggerDown, "Changing handedness cannot select accidentally");
        sample.Gesture = false; sample.Source = "Left controller";
        input.ResolveSample(sample, false, false);
        s = input.ResolveSample(sample, true, false, true, true, true);
        check(s.TriggerDown && !s.GripDown && s.Undo && s.ToggleSnap && s.PinMenu, "Controller trigger and shortcuts stay independent");
        s = input.ResolveSample(sample, true, true, true, true, true);
        check(!s.TriggerDown && s.GripDown && !s.Undo && !s.ToggleSnap && !s.PinMenu, "Controller shortcuts remain edge triggered");
        s = input.ResolveSample(sample, false, false);
        check(s.GripUp, "Controller grip release is preserved");
        input.Dispose();

        var dwell = new ToolkitDwell();
        check(!dwell.Step("menu:save", Vector3.zero, 0), "Dwell starts without activation");
        check(!dwell.Step("menu:save", Vector3.zero, 1) && dwell.Progress > 0.7f, "Dwell reports progress");
        check(dwell.Step("menu:save", Vector3.zero, 1.31f), "Dwell activates after 1.3 seconds");
        check(!dwell.Step("menu:save", Vector3.zero, 5), "Lingering cannot repeatedly save or confirm Load");
        check(!dwell.Step(null, Vector3.zero, 6) && dwell.Progress == 0, "Looking away clears progress");
        dwell.Step("menu:save", Vector3.zero, 7);
        check(dwell.Step("menu:save", Vector3.zero, 8.31f), "Looking away rearms the target");
        check(!dwell.Step("menu:load", Vector3.zero, 9), "Changing target restarts dwell");
        check(!dwell.Step("menu:load", Vector3.one, 10.5f), "Moving the target resets dwell rather than activating");
        dwell.Reset();
        check(dwell.Progress == 0 && !dwell.Step("surface", Vector3.zero, 20), "Tracking loss reset clears partial dwell");
        return "All " + count + " input checks passed.";
    }

    [UnityEditor.MenuItem("PICO/Spatial Toolkit/Run Input Replay Checks")]
    public static void RunMenu() { Debug.Log(Run()); }
}
