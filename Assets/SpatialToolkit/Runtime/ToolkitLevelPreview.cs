using UnityEngine;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        GameObject levelPreview;
        Vector3 levelPreviewPosition, levelPreviewNormal, fitPreviewPosition;
        bool hasLevelPreviewPose, hasFitPreviewPose;
        float levelPreviewYaw;
        bool levelHeadingInitialized, levelPreviewHorizontal;
        public static bool HorizontalLevelSurface(Vector3 normal) => Mathf.Abs(normal.normalized.y) >= 0.8f;
        public static bool LevelSurface(Vector3 normal) => Mathf.Abs(normal.normalized.y) <= 0.3f || HorizontalLevelSurface(normal);
        public static bool RotatesUpright(ToolkitItemData item) => item != null &&
            (item.kind == ToolkitItemKind.Furniture || item.kind == ToolkitItemKind.Level && item.horizontalLevel);
        public ToolkitItemData RotationSelection => Selection?.kind == ToolkitItemKind.AnglePath && selectedNode == 0 ?
            Session.items.Find(i => i.id == Selection.gridId && i.horizontalLevel) : Selection;
        Quaternion HorizontalLevelRotation => Quaternion.AngleAxis(levelPreviewYaw, Vector3.up) * Quaternion.LookRotation(Vector3.up, Vector3.forward);

        void EnsureLevelHeading(Vector3 point)
        {
            if (levelHeadingInitialized) return;
            // Query the same scanned-room colliders used for placement; app geometry
            // is excluded. A nearby vertical face provides the room's planar axes.
            Vector3 direction = RoomRoot ? RoomRoot.forward : Vector3.forward;
            float nearest = 8;
            for (int i = 0; i < 16; i++)
            {
                var ray = new Ray(point + Vector3.up * 0.6f, Quaternion.AngleAxis(i * 22.5f, Vector3.up) * Vector3.forward);
                foreach (var hit in Physics.RaycastAll(ray, 8, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.distance < nearest && !hit.collider.GetComponentInParent<ToolkitOwned>() && Mathf.Abs(hit.normal.y) <= 0.3f)
                    { nearest = hit.distance; direction = hit.normal; }
            }
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            levelPreviewYaw = direction.sqrMagnitude < 0.001f ? 0 : Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            levelHeadingInitialized = true;
        }

        void DrawLevelGrid(Transform parent, bool horizontal = false)
        {
            for (int i = -6; i <= 6; i++)
            {
                float x = i * 0.1f;
                Color color = i == 0 ? ToolkitVisuals.Mint : ToolkitVisuals.WithAlpha(ToolkitVisuals.Mint, ToolkitVisuals.ColorOption == 2 ? 0.65f : 0.3f);
                ToolkitVisuals.Line(parent, new[] { new Vector3(x, -0.6f, 0), new Vector3(x, 0.6f, 0) }, color, i == 0 ? 0.003f : 0.0015f);
                ToolkitVisuals.Line(parent, new[] { new Vector3(-0.6f, x, 0), new Vector3(0.6f, x, 0) }, color, i == 0 ? 0.003f : 0.0015f);
            }
            if (!horizontal) return;
            ToolkitVisuals.Line(parent, new[] { Vector3.zero, Vector3.forward * 1.2f }, ToolkitVisuals.Mint, 0.003f);
            for (int axis = 0; axis < 2; axis++)
            {
                var half = new GameObject("Vertical half-grid " + axis);
                half.transform.SetParent(parent, false);
                if (axis == 1) half.transform.localRotation = Quaternion.AngleAxis(90, Vector3.forward);
                for (int i = -6; i <= 6; i++)
                {
                    float x = i * 0.1f;
                    // The shared center plumb line is drawn once, at full opacity.
                    if (i != 0) ToolkitVisuals.Line(half.transform, new[] { new Vector3(x, 0, 0), new Vector3(x, 0, 1.2f) }, ToolkitVisuals.WithAlpha(ToolkitVisuals.Mint, 0.12f), 0.0015f);
                }
                for (int i = 1; i <= 12; i++)
                    ToolkitVisuals.Line(half.transform, new[] { new Vector3(-0.6f, 0, i * 0.1f), new Vector3(0.6f, 0, i * 0.1f) }, ToolkitVisuals.WithAlpha(ToolkitVisuals.Mint, 0.12f), 0.0015f);
                var fade = half.AddComponent<ToolkitLevelGridFade>();
                fade.View = ViewCamera ? ViewCamera.transform : null;
                fade.Initialize();
            }
        }
        void UpdateLevelPreview()
        {
            if (Tool == ToolkitTool.Level && activePath == null && !grabbed && !menuBlocked && validSurface && LevelSurface(targetNormal))
            { levelPreviewPosition = target; levelPreviewNormal = targetNormal; hasLevelPreviewPose = true; }
            bool visible = Selection == null && Tool == ToolkitTool.Level && activePath == null && !grabbed && hasLevelPreviewPose &&
                (menuBlocked || validSurface && LevelSurface(targetNormal));
            bool horizontal = HorizontalLevelSurface(levelPreviewNormal);
            if (horizontal) EnsureLevelHeading(levelPreviewPosition);
            if (levelPreview && levelPreviewHorizontal != horizontal)
            { levelPreview.SetActive(false); Destroy(levelPreview); levelPreview = null; }
            if (visible && !levelPreview)
            {
                levelPreview = new GameObject("Level grid preview", typeof(ToolkitOwned), typeof(ToolkitMaterialCleanup));
                levelPreviewHorizontal = horizontal;
                DrawLevelGrid(levelPreview.transform, horizontal);
            }
            if (!levelPreview) return;
            levelPreview.SetActive(visible);
            ToolkitGeometry.SurfaceBasis(levelPreviewNormal, out _, out var up);
            levelPreview.transform.SetPositionAndRotation(levelPreviewPosition, horizontal ? HorizontalLevelRotation : Quaternion.LookRotation(levelPreviewNormal, up));
        }
    }

    public sealed class ToolkitLevelGridFade : MonoBehaviour
    {
        public Transform View;
        LineRenderer[] lines;
        public void Initialize() { lines = GetComponentsInChildren<LineRenderer>(); UpdateVisibility(); }
        public static float Visibility(Vector3 normal, Vector3 viewDirection) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.1f, 0.75f, Mathf.Abs(Vector3.Dot(normal.normalized, viewDirection.normalized))));
        public void UpdateVisibility()
        {
            if (!View || lines == null) return;
            float alpha = 0.12f * Visibility(transform.up, View.position - transform.parent.position);
            foreach (var line in lines)
            {
                var color = line.sharedMaterial.color; color.a = alpha; line.sharedMaterial.color = color;
            }
        }
        void LateUpdate() { UpdateVisibility(); }
    }
}
