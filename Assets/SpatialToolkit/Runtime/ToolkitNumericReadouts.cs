using UnityEngine;
using UnityEngine.UI;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        string FitDimensions(Vector3 dimensions, bool live) => "FIT CHECK\n" +
            "W " + ToolkitGeometry.Distance(dimensions.x, Preferences.imperial, live) + "\n" +
            "H " + ToolkitGeometry.Distance(dimensions.y, Preferences.imperial, live) + "\n" +
            "D " + ToolkitGeometry.Distance(dimensions.z, Preferences.imperial, live);

        Text fitDimensionReadout;
        Transform fitPreviewBox;
        void UpdateFitDimensionReadout(bool visible)
        {
            if (!fitPreview) return;
            if (!fitDimensionReadout)
            {
                fitDimensionReadout = ToolkitVisuals.Label(fitPreview.transform, Vector3.zero, "", ToolkitVisuals.Lavender);
                ToolkitDimensionGrid.Configure(fitDimensionReadout, ToolkitDimensionGrid.WorldInset);
            }
            fitDimensionReadout.transform.parent.gameObject.SetActive(visible);
            var label = fitDimensionReadout.transform.parent;
            label.localPosition = Vector3.up * (Preferences.furniture.y * 0.5f + 0.12f);
            fitDimensionReadout.text = FitDimensions(Preferences.furniture, DimensionAdjusting);
        }
    }
}
