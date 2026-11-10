using UnityEngine;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        GameObject levelPreview;
        Vector3 levelPreviewPosition, levelPreviewNormal, fitPreviewPosition;
        bool hasLevelPreviewPose, hasFitPreviewPose;

        void DrawLevelGrid(Transform parent)
        {
            for (int i = -6; i <= 6; i++)
            {
                float x = i * 0.1f;
                Color color = i == 0 ? ToolkitVisuals.Mint : ToolkitVisuals.WithAlpha(ToolkitVisuals.Mint, ToolkitVisuals.ColorOption == 2 ? 0.65f : 0.3f);
                ToolkitVisuals.Line(parent, new[] { new Vector3(x, -0.6f, 0), new Vector3(x, 0.6f, 0) }, color, i == 0 ? 0.003f : 0.0015f);
                ToolkitVisuals.Line(parent, new[] { new Vector3(-0.6f, x, 0), new Vector3(0.6f, x, 0) }, color, i == 0 ? 0.003f : 0.0015f);
            }
        }
        void UpdateLevelPreview()
        {
            if (Tool == ToolkitTool.Level && activePath == null && !grabbed && !menuBlocked && validSurface && Mathf.Abs(targetNormal.y) <= 0.3f)
            { levelPreviewPosition = target; levelPreviewNormal = targetNormal; hasLevelPreviewPose = true; }
            bool visible = Selection == null && Tool == ToolkitTool.Level && activePath == null && !grabbed && hasLevelPreviewPose &&
                (menuBlocked || validSurface && Mathf.Abs(targetNormal.y) <= 0.3f);
            if (visible && !levelPreview)
            {
                levelPreview = new GameObject("Level grid preview", typeof(ToolkitOwned), typeof(ToolkitMaterialCleanup));
                DrawLevelGrid(levelPreview.transform);
            }
            if (!levelPreview) return;
            levelPreview.SetActive(visible);
            ToolkitGeometry.SurfaceBasis(levelPreviewNormal, out _, out var up);
            levelPreview.transform.SetPositionAndRotation(levelPreviewPosition, Quaternion.LookRotation(levelPreviewNormal, up));
        }
    }
}
