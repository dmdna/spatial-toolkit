using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace SpatialToolkit
{
    public struct ToolInput
    {
        public Ray Ray;
        public bool Tracked, TriggerHeld, TriggerDown, GripDown, GripUp, GripHeld, Undo, ToggleSnap, PinMenu;
        public bool Gesture, Dwell, MenuOnly, SourceChanged, PinchHeld, AlignmentTracked;
        public float DepthDelta;
        public Vector3 Position, Up, Forward, AlignmentForward, PalettePosition, PaletteUp;
        public Quaternion Rotation, PaletteRotation;
        public bool PaletteTracked;
        public string Source;
    }

    public sealed class ToolkitInput
    {
        bool previousTrigger, previousGrip, previousUndo, previousSnap, previousPin, blocked, wasTracked;
        string previousSource;
        Ray heldRay;
        Vector3 heldPosition;
        public bool LeftHanded;
        public ToolkitInputMode Mode;
        readonly ToolkitTrackedHands hands = new ToolkitTrackedHands();
        public void Reset() { previousSource = null; blocked = true; wasTracked = false; }
        public void Dispose() { hands.Dispose(); }

        public ToolInput Read(Transform head, Transform trackingOrigin, Camera desktopCamera)
        {
            XRDevice dominant = InputDevices.GetDeviceAtXRNode(LeftHanded ? XRNode.LeftHand : XRNode.RightHand);
            XRDevice palette = InputDevices.GetDeviceAtXRNode(LeftHanded ? XRNode.RightHand : XRNode.LeftHand);
            var state = new ToolInput { Rotation = Quaternion.identity, Source = "Tracking unavailable" };
            bool trigger = false, grip = false, undo = false, snap = false, pin = false;
            bool controller = Mode == ToolkitInputMode.Automatic && hands.ControllersActive && ControllerPose(dominant, trackingOrigin, out state.Position, out state.Rotation);
            if (!controller && Mode == ToolkitInputMode.Automatic && hands.ControllersActive && ControllerPose(palette, trackingOrigin, out state.Position, out state.Rotation))
            { var missing = dominant; dominant = palette; palette = missing; controller = true; }
            if (controller)
            {
                state.Tracked = state.AlignmentTracked = true;
                state.Source = (dominant.characteristics & InputDeviceCharacteristics.Left) != 0 ? "Left controller" : "Right controller";
                dominant.TryGetFeatureValue(CommonUsages.triggerButton, out trigger);
                dominant.TryGetFeatureValue(CommonUsages.gripButton, out grip);
                dominant.TryGetFeatureValue(CommonUsages.primaryButton, out undo);
                dominant.TryGetFeatureValue(CommonUsages.secondaryButton, out snap);
                dominant.TryGetFeatureValue(CommonUsages.primary2DAxis, out var stick);
                state.DepthDelta = stick.y * Time.unscaledDeltaTime * 1.5f;
                state.PaletteTracked = ControllerPose(palette, trackingOrigin, out state.PalettePosition, out state.PaletteRotation);
                if (state.PaletteTracked) palette.TryGetFeatureValue(CommonUsages.triggerButton, out pin);
                state.Ray = new Ray(state.Position + state.Rotation * Vector3.forward * 0.04f, state.Rotation * Vector3.forward);
                state.AlignmentForward = state.Ray.direction;
            }
            else if (XRSettings.isDeviceActive)
            {
                state = hands.Read(head, trackingOrigin, LeftHanded, Mode, previousSource);
                trigger = grip = state.PinchHeld;
            }
#if UNITY_EDITOR
            else if (desktopCamera && Mouse.current != null)
            {
                state.Tracked = state.AlignmentTracked = true; state.Source = "Desktop";
                state.Ray = desktopCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                state.Position = state.Ray.origin;
                state.Rotation = Quaternion.LookRotation(state.Ray.direction, Vector3.up);
                state.AlignmentForward = state.Ray.direction;
                trigger = Mouse.current.leftButton.isPressed;
                grip = Mouse.current.rightButton.isPressed;
                var keys = Keyboard.current;
                undo = keys != null && keys.zKey.isPressed;
                snap = keys != null && keys.bKey.isPressed;
                pin = keys != null && keys.tabKey.isPressed;
                state.DepthDelta = Mouse.current.scroll.ReadValue().y * 0.001f;
            }
#endif
            if (XRSettings.isDeviceActive)
            {
                var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                state.Tracked &= hmd.TryGetFeatureValue(CommonUsages.isTracked, out bool headTracked) && headTracked;
            }
            return ResolveSample(state, trigger, grip, undo, snap, pin);
        }

        public ToolInput ResolveSample(ToolInput state, bool trigger, bool grip, bool undo = false, bool snap = false, bool pin = false)
        {
            // Eye validity can change during a blink; the captured hand still owns its drag.
            string identity = state.Source != null && state.Source.StartsWith("Left hand") ? "Left hand" :
                state.Source != null && state.Source.StartsWith("Right hand") ? "Right hand" : state.Source;
            string source = identity + ":" + Mode + ":" + LeftHanded;
            state.SourceChanged = source != previousSource || (state.Tracked && !wasTracked);
            wasTracked = state.Tracked;
            if (state.SourceChanged || !state.Tracked)
            {
                blocked = trigger || grip;
                previousTrigger = previousGrip = previousUndo = previousSnap = previousPin = false;
            }
            previousSource = source;
            if (blocked)
            {
                blocked = trigger || grip;
                trigger = grip = undo = snap = pin = false;
            }
            state.TriggerHeld = trigger;
            state.TriggerDown = trigger && !previousTrigger;
            state.GripDown = grip && !previousGrip;
            state.GripUp = !grip && previousGrip;
            state.GripHeld = grip;
            // Capture gaze on pinch. Looking elsewhere cannot move a held item.
            if (state.Gesture && !state.Dwell)
            {
                if (state.TriggerDown) { heldRay = state.Ray; heldPosition = state.Position; }
                if (grip || state.GripUp) state.Ray = new Ray(heldRay.origin + (state.Position - heldPosition) * 3, heldRay.direction);
            }
            state.Forward = state.Rotation * Vector3.forward;
            state.Up = state.Rotation * Vector3.up;
            state.PaletteUp = state.PaletteRotation * Vector3.up;
            state.Undo = undo && !previousUndo;
            state.ToggleSnap = snap && !previousSnap;
            state.PinMenu = pin && !previousPin;
            previousTrigger = trigger; previousGrip = grip; previousUndo = undo; previousSnap = snap; previousPin = pin;
            return state;
        }

        static bool ControllerPose(XRDevice device, Transform origin, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            if (!device.isValid || (device.characteristics & InputDeviceCharacteristics.Controller) == 0 ||
                !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked ||
                !device.TryGetFeatureValue(CommonUsages.devicePosition, out var p) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var q)) return false;
            position = origin.TransformPoint(p); rotation = origin.rotation * q; return true;
        }
    }

    public sealed class ToolkitDwell
    {
        string focus;
        Vector3 anchor;
        float started;
        bool fired;
        public float Progress { get; private set; }
        public void Reset() { focus = null; Progress = 0; fired = false; }
        public bool Step(string target, Vector3 point, float now, float duration = 1.3f)
        {
            if (target == null) { Reset(); return false; }
            if (target != focus || Vector3.Distance(anchor, point) > 0.035f)
            { focus = target; anchor = point; started = now; fired = false; }
            Progress = fired ? 0 : Mathf.Clamp01((now - started) / duration);
            if (fired || Progress < 1) return false;
            fired = true; return true;
        }
    }
}
