using UnityEngine;
using UnityEngine.XR;
#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
#endif

namespace SpatialToolkit
{
    public sealed class ToolkitTrackedHands
    {
        public static bool UseLeftHand(bool leftTracked, bool rightTracked, bool preferLeft) =>
            preferLeft ? leftTracked : !rightTracked && leftTracked;
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
        HandJointLocations left, right;
        bool eyeStarted, eyePermissionRequested;
        float nextEyeStart;
        EyeTrackingData eyeData = new EyeTrackingData { eyeDatas = new PerEyeData[(int)PerEyeUsage.EyeCount] };
#endif
        public bool ControllersActive
        {
            get
            {
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
                return PXR_HandTracking.GetActiveInputDevice() == ActiveInputDevice.ControllerActive;
#else
                return true;
#endif
            }
        }

        public ToolInput Read(Transform head, Transform origin, bool leftHanded, ToolkitInputMode mode, string previousSource)
        {
            var result = new ToolInput { Rotation = head.rotation, Source = "Hands unavailable", Gesture = true };
            bool eye = false;
            Ray gaze = new Ray(head.position, head.forward);
            if (mode != ToolkitInputMode.HandRay && mode != ToolkitInputMode.HeadPinch) eye = TryEye(origin, out gaze);
            if (!eye) gaze = new Ray(head.position, head.forward);
            if (mode == ToolkitInputMode.Dwell)
            {
                var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                result.Tracked = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked;
                result.Gaze = result.Dwell = true; result.Ray = gaze; result.Position = head.position;
                result.Source = eye ? "Eye gaze dwell" : "Head gaze dwell"; return result;
            }
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            bool l = TryHand(HandType.HandLeft, origin, ref left, out var lp, out var lr, out var la, out var ll, out bool lpinch);
            bool r = TryHand(HandType.HandRight, origin, ref right, out var rp, out var rr, out var ra, out var rl, out bool rpinch);
            bool useLeft = UseLeftHand(l, r, leftHanded);
            if (!(useLeft ? l : r)) return MenuFallback(result, head);
            result.Tracked = true; result.Position = useLeft ? lp : rp; result.Rotation = useLeft ? lr : rr;
            result.PinchHeld = useLeft ? lpinch : rpinch;
            result.AlignmentForward = useLeft ? ll : rl; result.AlignmentTracked = result.AlignmentForward.sqrMagnitude > 0.5f;
            bool handRay = mode == ToolkitInputMode.HandRay || (!eye && mode == ToolkitInputMode.Automatic);
            result.HandRay = handRay;
            result.Gaze = !handRay;
            result.Ray = handRay ? (useLeft ? la : ra) : gaze;
            if (handRay)
            {
                result.VisualOriginTracked = true;
                result.VisualRayOrigin = PinchOrigin(origin, useLeft ? left : right, result.Ray.origin);
            }
            result.Source = (useLeft ? "Left hand" : "Right hand") + (handRay ? " ray" : eye ? " + eye gaze" : " + head gaze");
            result.PaletteTracked = useLeft ? r : l;
            result.PalettePosition = useLeft ? rp : lp; result.PaletteRotation = useLeft ? rr : lr;
#endif
            if (!result.Tracked) return MenuFallback(result, head);
            return result;
        }

        static ToolInput MenuFallback(ToolInput result, Transform head)
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            result.Tracked = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked;
            result.Gaze = result.Dwell = result.MenuOnly = true; result.Ray = new Ray(head.position, head.forward);
            result.Position = head.position; result.Source = "Head gaze menu (hands unavailable)";
            return result;
        }

#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
        static bool TryHand(HandType type, Transform origin, ref HandJointLocations joints, out Vector3 position,
            out Quaternion rotation, out Ray ray, out Vector3 alignment, out bool pinch)
        {
            position = alignment = Vector3.zero; rotation = Quaternion.identity; ray = default; pinch = false;
            if (!PXR_HandTracking.GetJointLocations(type, ref joints) || joints.isActive == 0 || joints.jointLocations == null || joints.jointLocations.Length < 26) return false;
            var wrist = joints.jointLocations[(int)HandJoint.JointWrist];
            var flags = HandLocationStatus.PositionTracked | HandLocationStatus.OrientationTracked;
            if ((wrist.locationStatus & flags) != flags) return false;
            position = origin.TransformPoint(wrist.pose.Position.ToVector3());
            rotation = origin.rotation * wrist.pose.Orientation.ToQuat();
            var aim = new HandAimState();
            bool aimComputed = PXR_HandTracking.GetAimState(type, ref aim) && (aim.aimStatus & HandAimStatus.AimComputed) != 0;
            if (aimComputed) pinch = (aim.aimStatus & (HandAimStatus.AimIndexPinching | HandAimStatus.AimRayTouched)) != 0;
            // Use the runtime's stabilized pointing pose, rather than a wrist/knuckle axis.
            ray = new Ray(position, rotation * Vector3.forward);
            if (aimComputed && (aim.aimStatus & HandAimStatus.AimRayValid) != 0)
                ray = new Ray(origin.TransformPoint(aim.aimRayPose.Position.ToVector3()), origin.rotation * aim.aimRayPose.Orientation.ToQuat() * Vector3.forward);
            var tip = joints.jointLocations[(int)HandJoint.JointIndexTip];
            var root = joints.jointLocations[(int)HandJoint.JointIndexProximal];
            if ((tip.locationStatus & HandLocationStatus.PositionTracked) != 0 && (root.locationStatus & HandLocationStatus.PositionTracked) != 0)
                alignment = origin.TransformDirection((tip.pose.Position.ToVector3() - root.pose.Position.ToVector3()).normalized);
            return true;
        }
        static Vector3 PinchOrigin(Transform origin, HandJointLocations joints, Vector3 fallback)
        {
            var thumb = joints.jointLocations[(int)HandJoint.JointThumbTip];
            var index = joints.jointLocations[(int)HandJoint.JointIndexTip];
            if ((thumb.locationStatus & HandLocationStatus.PositionTracked) == 0 ||
                (index.locationStatus & HandLocationStatus.PositionTracked) == 0) return fallback;
            return origin.TransformPoint((thumb.pose.Position.ToVector3() + index.pose.Position.ToVector3()) * 0.5f);
        }
#endif
        bool TryEye(Transform origin, out Ray ray)
        {
            ray = default;
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            if (!PXR_ProjectSetting.GetProjectConfig().eyeTracking) return false;
            bool supported = false; int count = 0; EyeTrackingMode[] modes = new EyeTrackingMode[8];
            if (!eyeStarted)
            {
                if (Time.unscaledTime < nextEyeStart) return false;
                nextEyeStart = Time.unscaledTime + 5;
                if (PXR_MotionTracking.GetEyeTrackingSupported(ref supported, ref count, ref modes) != 0 || !supported) return false;
                const string permission = "com.picovr.permission.EYE_TRACKING";
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
                {
                    if (!eyePermissionRequested) { eyePermissionRequested = true; UnityEngine.Android.Permission.RequestUserPermission(permission); }
                    return false;
                }
                var start = new EyeTrackingStartInfo { needCalibration = 0, mode = EyeTrackingMode.PXR_ETM_BOTH };
                eyeStarted = PXR_MotionTracking.StartEyeTracking(ref start) == 0;
                if (!eyeStarted) return false;
            }
            bool tracking = false; var state = new EyeTrackingState();
            if (PXR_MotionTracking.GetEyeTrackingState(ref tracking, ref state) != 0 || !tracking)
            { eyeStarted = false; nextEyeStart = Time.unscaledTime + 5; return false; }
            var info = new EyeTrackingDataGetInfo { flags = EyeTrackingDataGetFlags.PXR_EYE_POSITION | EyeTrackingDataGetFlags.PXR_EYE_ORIENTATION };
            if (eyeData.eyeDatas == null || eyeData.eyeDatas.Length < (int)PerEyeUsage.EyeCount)
                eyeData.eyeDatas = new PerEyeData[(int)PerEyeUsage.EyeCount];
            for (int i = 0; i < eyeData.eyeDatas.Length; i++) eyeData.eyeDatas[i].SetVersion(PXR_MotionTracking.PXR_EYE_TRACKING_API_VERSION);
            if (PXR_MotionTracking.GetEyeTrackingData(ref info, ref eyeData) != 0 || eyeData.eyeDatas == null || eyeData.eyeDatas.Length <= (int)PerEyeUsage.Combined) return false;
            var combined = eyeData.eyeDatas[(int)PerEyeUsage.Combined];
            if (combined.isPoseValid == 0) return false;
            var p = combined.pose.position; var q = combined.pose.orientation;
            ray = new Ray(origin.TransformPoint(new Vector3(p.x, p.y, -p.z)), origin.rotation * new Quaternion(q.x, q.y, -q.z, -q.w) * Vector3.forward);
            return true;
#else
            return false;
#endif
        }
        public void Dispose()
        {
#if ENABLE_PICO_XR_SDK && UNITY_ANDROID && !UNITY_EDITOR
            if (eyeStarted) { var stop = new EyeTrackingStopInfo(); PXR_MotionTracking.StopEyeTracking(ref stop); eyeStarted = false; }
#endif
        }
    }
}
