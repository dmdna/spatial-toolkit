using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        ToolkitInput secondaryInput;
        ToolInput primaryState, offState;
        bool frameSampled, interactionOffHand;
        GameObject otherPointerRoot;
        LineRenderer otherPointer;
        Transform otherReticle;
        string otherHoverId;
        int otherHoverNode = -1;
        float hiddenAt;
        bool hiddenButtonReleased;
        public bool MenuHidden { get; private set; }

        void ReadInteractionFrame()
        {
            secondaryInput = secondaryInput ?? input.CreateSecondary();
            input.LeftHanded = Preferences.leftHanded; secondaryInput.LeftHanded = !Preferences.leftHanded;
            input.Mode = secondaryInput.Mode = Preferences.inputMode;
            primaryState = input.Read(ViewCamera.transform, TrackingOrigin, ViewCamera);
            offState = secondaryInput.Read(ViewCamera.transform, TrackingOrigin, ViewCamera);
            if (!primaryState.MenuOnly && primaryState.Tracked && primaryState.LeftHand == offState.LeftHand) offState.Tracked = false;
            ChooseInteractionHand();
            frameSampled = true;
        }
        void ChooseInteractionHand()
        {
            // One hand owns an interaction through release. The other may join a box grab.
            bool captured = grabbed || pendingGrab || dimensionKey != null || palette.Dragging ||
                undoHeldSince >= 0 || loadHeldSince >= 0 || state.TriggerHeld || state.GripHeld;
            if (!captured)
            {
                interactionOffHand = offState.Tracked && !offState.SourceChanged && (offState.TriggerDown || offState.GripDown) &&
                    IsInteractable(offState.Ray) && !(primaryState.TriggerDown || primaryState.GripDown);
            }
            state = interactionOffHand ? offState : primaryState;
        }

        bool IsInteractable(Ray ray) => TryPaletteAction(ray, out _, out _) || FindHandle(ray);
        ToolInput OtherState => interactionOffHand ? primaryState : offState;
        bool HasPlacementSource => primaryState.Tracked && !primaryState.MenuOnly &&
            (primaryState.Dwell || primaryState.Source == "Desktop" || primaryState.LeftHand == Preferences.leftHanded);
        bool OwnsPlacement => !frameSampled || !interactionOffHand && HasPlacementSource;

        void UpdatePlacementPreview()
        {
            if (!frameSampled) { UpdatePreview(); return; }
            // Editing retains the captured hand's ray, even outside the menu. Placement
            // always samples the selected main hand independently of that interaction.
            var interactionState = state;
            var interactionTarget = target; var interactionNormal = targetNormal;
            bool interactionSurface = validSurface, interactionMenu = menuBlocked;
            try
            {
                state = primaryState;
                FindSurface();
                menuBlocked = HasPlacementSource && TryPaletteAction(out _, out _);
                validSurface &= HasPlacementSource;
                UpdatePreview();
            }
            finally
            {
                state = interactionState; target = interactionTarget; targetNormal = interactionNormal;
                validSurface = interactionSurface; menuBlocked = interactionMenu;
            }
        }

        void DrawOtherPointer()
        {
            if (MenuHidden) return;
            if (!otherPointerRoot)
            {
                otherPointerRoot = new GameObject("Second hand pointer", typeof(ToolkitOwned), typeof(ToolkitMaterialCleanup));
                otherPointer = ToolkitVisuals.Line(otherPointerRoot.transform, new[] { Vector3.zero, Vector3.zero }, ToolkitVisuals.Mint);
                otherPointer.useWorldSpace = true; otherPointer.GetComponent<ToolkitReadableLine>().enabled = false;
                otherReticle = ToolkitVisuals.Sphere(otherPointerRoot.transform, Vector3.zero, 0.014f, ToolkitVisuals.Mint).transform;
                ToolkitVisuals.PointerOverlay(otherPointer.transform); ToolkitVisuals.PointerOverlay(otherReticle);
            }
            var sample = OtherState;
            bool menu = TryPaletteAction(sample.Ray, out var action, out var point);
            var handle = menu ? null : FindHandle(sample.Ray);
            bool visible = sample.Tracked && !sample.MenuOnly && !sample.Dwell && (interactionOffHand || menu || handle || twoHandGrab);
            otherPointerRoot.SetActive(visible && !MenuHidden);
            otherHoverId = visible && handle ? handle.ItemId : null; otherHoverNode = handle ? handle.Node : -1;
            var linked = GridPath(otherHoverId);
            if (linked != null) { otherHoverId = linked.id; otherHoverNode = 0; }
            // The off-hand pointer appears only on an interactive target, even in wrist mode.
            if (interactionOffHand && !IsInteractable(state.Ray) && !grabbed && dimensionKey == null && !palette.Dragging)
            { pointer.gameObject.SetActive(false); reticle.gameObject.SetActive(false); }
            if (!visible) { RefreshSelectionFeedback(); return; }
            if (!menu)
            {
                point = sample.Ray.GetPoint(reach);
                if (handle && handle.GetComponent<Collider>().Raycast(sample.Ray, out var hit, reach)) point = hit.point;
                else if (Physics.Raycast(sample.Ray, out var surface, reach)) point = surface.point;
            }
            otherPointer.gameObject.SetActive(!sample.Gaze);
            otherReticle.position = point;
            otherPointer.SetPosition(0, sample.VisualOriginTracked ? sample.VisualRayOrigin : sample.Ray.origin); otherPointer.SetPosition(1, point);
            otherReticle.GetComponent<Renderer>().sharedMaterial.color = ToolkitVisuals.WithAlpha(ToolkitVisuals.Mint, sample.Gaze ? 0.6f : 1);
            RefreshSelectionFeedback();
        }

        void HideMenu()
        {
            CancelGrab(); MenuHidden = true; hiddenAt = Time.unscaledTime; hiddenButtonReleased = false;
            palette.UpdatePose(state, ViewCamera.transform);
            pointer.transform.parent.gameObject.SetActive(false);
            if (otherPointerRoot) otherPointerRoot.SetActive(false);
            if (fitPreview) fitPreview.SetActive(false);
            if (hangPreview) hangPreview.SetActive(false);
            if (levelPreview) levelPreview.SetActive(false);
            Message("Menu hidden. Release, then select to show it again.");
        }
        void UpdateHiddenMenu()
        {
            if (!MenuHidden) return;
            bool held = state.TriggerHeld || state.GripHeld || frameSampled && (OtherState.TriggerHeld || OtherState.GripHeld);
            if (!held) hiddenButtonReleased = true;
            bool show = hiddenButtonReleased && (state.TriggerDown || state.GripDown || state.PinMenu ||
                frameSampled && (OtherState.TriggerDown || OtherState.GripDown)) || state.Dwell && Time.unscaledTime - hiddenAt >= 8;
            if (show)
            {
                MenuHidden = false; state.TriggerDown = state.GripDown = false;
                state.TriggerHeld = state.GripHeld = false; input.Reset(); secondaryInput?.Reset();
                palette.UpdatePose(frameSampled ? primaryState : state, ViewCamera.transform);
            }
            pointer.transform.parent.gameObject.SetActive(!MenuHidden);
            if (otherPointerRoot) otherPointerRoot.SetActive(false);
        }

        Quaternion wristStart;
        Vector3 wristAxis;
        float previousWristAngle, accumulatedWristAngle, snappedWristAngle, grabStickYaw;
        void BeginWristRotation()
        {
            wristStart = state.Rotation; wristAxis = state.Ray.direction.normalized;
            previousWristAngle = accumulatedWristAngle = snappedWristAngle = grabStickYaw = 0;
        }
        public static float TwistDegrees(Quaternion start, Quaternion current, Vector3 axis)
        {
            Quaternion delta = current * Quaternion.Inverse(start);
            float along = Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), axis.normalized);
            if (along * along + delta.w * delta.w < 0.000001f) return 0;
            return Mathf.DeltaAngle(0, 2 * Mathf.Atan2(along, delta.w) * Mathf.Rad2Deg);
        }
        float WristYaw()
        {
            float angle = TwistDegrees(wristStart, state.Rotation, wristAxis);
            accumulatedWristAngle += Mathf.DeltaAngle(previousWristAngle, angle); previousWristAngle = angle;
            // A small hysteresis band prevents tracking noise from flickering between adjacent steps.
            while (accumulatedWristAngle - snappedWristAngle > 9.5f) snappedWristAngle += 15;
            while (accumulatedWristAngle - snappedWristAngle < -9.5f) snappedWristAngle -= 15;
            return snappedWristAngle;
        }

        string beforeStick;
        float fitPreviewYaw;
        Quaternion FitPreviewRotation => Quaternion.AngleAxis(fitPreviewYaw, Vector3.up);
        void EndStickRotation()
        {
            if (beforeStick == null) return;
            if (beforeStick != JsonUtility.ToJson(Session)) { undo.Add(beforeStick); if (undo.Count > 50) undo.RemoveAt(0); }
            beforeStick = null;
        }
        void UpdateStickRotation()
        {
            var item = RotationSelection;
            if (item == null && (Tool == ToolkitTool.Fit || Tool == ToolkitTool.Level && activePath == null && hasLevelPreviewPose && HorizontalLevelSurface(levelPreviewNormal)) && !grabbed && !busy && !MenuHidden)
            {
                // Preview aim and rotation belong to the selected placement hand,
                // including while the off-hand is editing dimensions.
                var placement = frameSampled ? primaryState : state;
                if (placement.Tracked && !placement.Gesture && (!frameSampled || HasPlacementSource))
                {
                    if (Tool == ToolkitTool.Fit) fitPreviewYaw = Mathf.Repeat(fitPreviewYaw + placement.RotateAxis * 30 * Time.unscaledDeltaTime, 360);
                    else { EnsureLevelHeading(levelPreviewPosition); levelPreviewYaw = Mathf.Repeat(levelPreviewYaw + placement.RotateAxis * 30 * Time.unscaledDeltaTime, 360); }
                }
                EndStickRotation(); return;
            }
            float axis = state.RotateAxis;
            if (frameSampled && !state.Gesture) axis = primaryState.RotateAxis != 0 ? primaryState.RotateAxis : offState.RotateAxis;
            if (grabbed || dimensionKey != null || busy || MenuHidden || !RotatesUpright(item) || axis == 0)
            { EndStickRotation(); return; }
            beforeStick = beforeStick ?? JsonUtility.ToJson(Session);
            var rotation = Quaternion.AngleAxis(axis * 30 * Time.unscaledDeltaTime, Vector3.up) * RoomRoot.rotation * item.rotation;
            if (item.kind == ToolkitItemKind.Furniture && Overlaps(RoomRoot.TransformPoint(item.position), item.dimensions, rotation, visuals[item.id].transform))
            { Message("Rotation would overlap a surface."); return; }
            item.rotation = Quaternion.Inverse(RoomRoot.rotation) * rotation;
            RefreshMovedItem(item, false);
        }

        bool twoHandGrab;
        Vector3 twoLocalA, twoLocalB, twoVector;
        Quaternion twoRotation;
        float twoDepthA, twoDepthB;
        bool TwoHeld(ToolInput sample) => sample.Gesture ? sample.TriggerHeld : sample.GripHeld;
        Vector3 GrabPoint(ToolInput sample, float depth) => sample.Gesture ? sample.Position : sample.Ray.GetPoint(depth);
        void CaptureTwoHandAnchors(ToolkitItemData item, ToolInput other)
        {
            var collider = grabbed.GetComponent<Collider>();
            Vector3 center = RoomRoot.TransformPoint(item.position);
            twoDepthA = Mathf.Max(0.05f, Vector3.Dot(center - state.Ray.origin, state.Ray.direction));
            twoDepthB = Mathf.Max(0.05f, Vector3.Dot(center - other.Ray.origin, other.Ray.direction));
            if (collider && collider.Raycast(state.Ray, out var a, reach)) twoDepthA = a.distance;
            if (collider && collider.Raycast(other.Ray, out var b, reach)) twoDepthB = b.distance;
            var pa = GrabPoint(state, twoDepthA); var pb = GrabPoint(other, twoDepthB);
            twoRotation = RoomRoot.rotation * item.rotation;
            twoLocalA = Quaternion.Inverse(twoRotation) * (pa - center); twoLocalB = Quaternion.Inverse(twoRotation) * (pb - center);
            twoVector = Vector3.ProjectOnPlane(pb - pa, Vector3.up);
        }
        public static void SolveTwoHandPose(Vector3 a, Vector3 b, Vector3 localA, Vector3 localB, Vector3 startVector,
            Quaternion startRotation, out Vector3 position, out Quaternion rotation)
        {
            var direction = Vector3.ProjectOnPlane(b - a, Vector3.up);
            float yaw = direction.sqrMagnitude < 0.0001f || startVector.sqrMagnitude < 0.0001f ? 0 : Vector3.SignedAngle(startVector, direction, Vector3.up);
            rotation = Quaternion.AngleAxis(yaw, Vector3.up) * startRotation;
            // Rigid fit of the two anchors: separation changes never change dimensions.
            position = (a + b) * 0.5f - rotation * ((localA + localB) * 0.5f);
        }
        bool UpdateTwoHandGrab(bool menuHit)
        {
            if (!frameSampled) return false;
            var item = Session.items.Find(i => i.id == grabbed.ItemId);
            var other = OtherState;
            if (!RotatesUpright(item)) return false;
            if (!twoHandGrab)
            {
                bool pressed = TwoHeld(other);
                var hit = FindHandle(other.Ray);
                bool sameItem = hit && (hit.ItemId == item.id || item.horizontalLevel && hit.Node == 0 && GridPath(item.id)?.id == hit.ItemId);
                if (!other.Tracked || other.SourceChanged || !pressed || !sameItem || TryPaletteAction(other.Ray, out _, out _)) return false;
                CaptureTwoHandAnchors(item, other); twoHandGrab = true;
            }
            if (!other.Tracked || other.SourceChanged || state.SourceChanged) { CancelGrab(); return true; }
            bool ownerHeld = TwoHeld(state), otherHeld = TwoHeld(other);
            SolveTwoHandPose(GrabPoint(state, twoDepthA), GrabPoint(other, twoDepthB), twoLocalA, twoLocalB, twoVector, twoRotation, out var center, out var rotation);
            // Fit boxes remain upright and retain their floor contact throughout a two-hand turn.
            center.y = RoomRoot.TransformPoint(item.position).y;
            bool blocked = item.kind == ToolkitItemKind.Furniture && Overlaps(center, item.dimensions, rotation, visuals[item.id].transform);
            if (!blocked) { item.position = RoomRoot.InverseTransformPoint(center); item.rotation = Quaternion.Inverse(RoomRoot.rotation) * rotation; }
            RefreshMovedItem(item, blocked);
            Status = blocked ? "Rotation blocked by a surface." : "Two-hand rotate • move both hands • size stays fixed";
            if (!ownerHeld && !otherHeld)
            {
                undo.Add(beforeGrab); if (undo.Count > 50) undo.RemoveAt(0);
                grabbed = null; twoHandGrab = false; RenderAll(); palette.Rebuild(); Message("Placed.");
            }
            else if (!ownerHeld || !otherHeld)
            {
                if (!ownerHeld) { interactionOffHand = !interactionOffHand; state = other; }
                // Rebase single-hand movement at the current pose, without snapping back to the first grip.
                twoHandGrab = false; grabWorldRotation = RoomRoot.rotation * item.rotation;
                grabYaw = Mathf.Atan2(state.Forward.x, state.Forward.z) * Mathf.Rad2Deg;
                grabDepth = Mathf.Max(0.05f, Vector3.Dot(center - state.Ray.origin, state.Ray.direction));
                BeginWristRotation(); previousGrabPosition = state.Ray.GetPoint(grabDepth); throwVelocity = Vector3.zero;
                rebaseGrab = true; rebasePoint = center - Vector3.up * item.dimensions.y * 0.5f;
                rebaseRayPoint = state.Ray.GetPoint(grabDepth);
            }
            return true;
        }
        bool rebaseGrab;
        Vector3 rebasePoint, rebaseRayPoint;
    }
}
