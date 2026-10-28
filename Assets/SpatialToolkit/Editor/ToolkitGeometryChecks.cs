using System;
using SpatialToolkit;
using UnityEngine;

public static class ToolkitGeometryChecks
{
    static int assertions;
    static void Check(bool condition, string description)
    {
        assertions++; if (!condition) throw new Exception("Geometry check failed: " + description);
    }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;
    public static string Run()
    {
        assertions = 0;
        var floor = new[] { Vector3.zero, new Vector3(3,0,0), new Vector3(3,0,4), new Vector3(0,0,4) };
        Check(ToolkitGeometry.TryArea(floor, out var area, out _) && Near(area,12), "3 by 4 meter rectangle");
        Array.Reverse(floor);
        Check(ToolkitGeometry.TryArea(floor, out area, out _) && Near(area,12), "winding independent area");
        var wall = new[] { new Vector3(1,1,2), new Vector3(3,1,2), new Vector3(3,4,2), new Vector3(1,4,2) };
        Check(ToolkitGeometry.TryArea(wall, out area, out _) && Near(area,6), "vertical wall area");
        var concave = new[] { Vector3.zero, new Vector3(3,0,0), new Vector3(3,0,1), new Vector3(1,0,1), new Vector3(1,0,3), new Vector3(0,0,3) };
        Check(ToolkitGeometry.TryArea(concave, out area, out _) && Near(area,5), "concave L outline");
        var crossing = new[] { Vector3.zero, new Vector3(2,0,2), new Vector3(0,0,2), new Vector3(2,0,0) };
        Check(!ToolkitGeometry.TryArea(crossing, out _, out _), "self crossing rejected");
        var nonplanar = new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.forward, new Vector3(0,0.1f,1) };
        Check(!ToolkitGeometry.TryArea(nonplanar, out _, out _), "nonplanar outline rejected");
        Check(!ToolkitGeometry.TryArea(new[] { Vector3.zero, Vector3.right, Vector3.right * 2 }, out _, out _), "collinear area rejected");
        Check(!ToolkitGeometry.TryArea(new[] { Vector3.zero, Vector3.zero, Vector3.right }, out _, out _), "duplicate vertices rejected");
        Check(ToolkitGeometry.Distance(0.3048f, true) == "1 ft 0 in", "foot conversion");
        Check(ToolkitGeometry.Distance(0.3047999f, true) == "1 ft 0 in", "inch carry rounding");
        Check(ToolkitGeometry.Area(1, true) == "10.76 sq ft", "square foot conversion");
        var snapped = ToolkitGeometry.SnapOnSurface(new Vector3(1.014f,2.027f,3), new Vector3(1,2,3), Vector3.forward, 0.01f);
        Check(Near(snapped.x,1.01f) && Near(snapped.y,2.03f) && Near(snapped.z,3), "wall snap preserves surface depth");
        var nails = ToolkitGeometry.NailPositions(3,0.4f,0.5f,0.08f,0.06f);
        Check(Near(nails[0].x,-0.48f) && Near(nails[1].x,0) && Near(nails[2].x,0.48f), "centered frame spacing");
        Check(Near(nails[1].y,0.19f), "hanger inset from top");
        bool rejected = false;
        try { ToolkitGeometry.NailPositions(1,0.4f,0.5f,0.1f,0.6f); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected,"invalid hanger inset rejected");
        Check(Near(ToolkitGeometry.LevelError(Vector3.forward),0),"horizontal edge");
        Check(Near(ToolkitGeometry.PlumbError(Vector3.up),0) && Near(ToolkitGeometry.PlumbError(Vector3.down),0),"up and down plumb");
        Check(Near(ToolkitGeometry.LevelError(new Vector3(1,1,0)),45),"45 degree inclination");
        return assertions + " measurement geometry checks passed.";
    }
}
