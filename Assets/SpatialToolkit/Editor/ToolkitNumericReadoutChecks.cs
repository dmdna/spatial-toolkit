using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SpatialToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ToolkitNumericReadoutChecks
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Numeric readout check failed: " + message);
    }

    // Runs in Edit Mode; creates and destroys only temporary UI objects.
    [MenuItem("PICO/Spatial Toolkit/Run Numeric Readout Checks")]
    public static string Run()
    {
        checks = 0;
        var culture = CultureInfo.CurrentCulture;
        GameObject root = null;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(ToolkitGeometry.Distance(1.25f, false).TrimStart('\u2007') == "1.250 m", "default metric distance and invariant decimal");
            Check(ToolkitGeometry.Distance(1, false, true).TrimStart('\u2007') == "1.000 m", "three live decimals");
            Check(ToolkitGeometry.Area(1, true).TrimStart('\u2007') == "10.764 sq ft", "default square feet at millesimal precision");
            Check(ToolkitGeometry.Area(1, false).TrimStart('\u2007') == "1.000 m\u00b2", "squared unit glyph");
            Check(ToolkitGeometry.Angle(90).TrimStart('\u2007') == "90.000\u00b0", "default angle and degree glyph");
            Check(ToolkitGeometry.Angle(9, true).TrimStart('\u2007') == "9.000\u00b0", "live angle decimals");
            Check(ToolkitGeometry.Distance(.3048f, true).TrimStart('\u2007') == "1 ft \u20070.000 in", "default feet conversion");
            Check(ToolkitGeometry.Distance(.3047999f, true, true).TrimStart('\u2007') == "1 ft \u20070.000 in", "live inch carry does not emit twelve inches");
            Check(ToolkitGeometry.Distance(.3047999f, true).TrimStart('\u2007') == "1 ft \u20070.000 in", "default millesimal carry");
            Check(ToolkitGeometry.Distance(8.5f, false) == ToolkitGeometry.Distance(8.5f, false, true), "metric formatting unchanged on release");
            Check(ToolkitGeometry.Distance(8.5f, true) == ToolkitGeometry.Distance(8.5f, true, true), "imperial formatting unchanged on release");
            Check(ToolkitGeometry.Area(8.5f, false) == ToolkitGeometry.Area(8.5f, false, true), "area formatting unchanged on release");
            Check(ToolkitGeometry.Angle(8.5f) == ToolkitGeometry.Angle(8.5f, true), "angle formatting unchanged on release");
            Check(ToolkitGeometry.Number(9.9996, true).Length == ToolkitGeometry.Number(10, true).Length, "reservation counts rounded integer digits");

            root = new GameObject("Temporary numeric checks", typeof(RectTransform), typeof(Canvas));
            root.hideFlags = HideFlags.HideAndDontSave;
            var label = ToolkitVisuals.Text(root.transform, new Vector2(900, 300), Vector2.zero, "", 26, Color.white);
            Check(label is ToolkitNumericText && !label.supportRichText, "production factory selects plain tabular numeric mesh");
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (var anchor in new[] { TextAnchor.MiddleLeft, TextAnchor.MiddleCenter, TextAnchor.MiddleRight })
            {
                label.alignment = anchor;
                Stable(label, v => "Length " + ToolkitGeometry.Distance(v, false, true), new[] { 1.111f, 8.888f, 9.999f, 10, 99.999f, 100, 999.999f, 1000 });
                Stable(label, v => "Area " + ToolkitGeometry.Area(v, false, true), new[] { 9.999f, 10, 99.999f, 100 });
                Stable(label, v => ToolkitGeometry.Angle(v, true), new[] { 9.999f, 10, 89.999f, 90 });
                Stable(label, v => ToolkitGeometry.Distance(v * .0254f, true, true), new[] { 9.999f, 10, 11.9999f, 12, 119.9999f, 120 });
                Stable(label, v => "W " + ToolkitGeometry.Distance(v, false, true) + "\nH " + ToolkitGeometry.Distance(v, false, true) + "\nD " + ToolkitGeometry.Distance(v, false, true), new[] { 9.999f, 10, 99.999f, 100 });
            }
            label.text = " A  \u200711.111 m\nB \u200788.888 m";
            var before = Mesh(label);
            // Exercise atlas churn, then regenerate through the production mesh path.
            label.font.RequestCharactersInTexture("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", 97, FontStyle.BoldAndItalic);
            label.FontTextureChanged();
            var after = Mesh(label);
            Check(before.Length == after.Length && before.Zip(after, (a, b) => Vector3.Distance(a.position, b.position)).All(d => d < .01f), "font texture rebuild preserves positions including whitespace/newlines");
            label.rectTransform.sizeDelta = new Vector2(175, 48);
            label.fontSize = 18;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = ToolkitGeometry.Distance(8, true, true);
            Mesh(label);
            Check(label.cachedTextGenerator.lineCount == 1, "imperial live reach fits its actual Settings field without wrapping");
            string report = checks + " numeric readout checks passed (Edit Mode, real Legacy Text mesh).";
            Debug.Log(report);
            return report;
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            if (root) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static string RunGrid()
    {
        checks = 0;
        var root = new GameObject("Temporary dimension grid checks", typeof(RectTransform), typeof(Canvas));
        try
        {
            var text = ToolkitVisuals.Text(root.transform, new Vector2(412, 48), Vector2.zero, "", 23, Color.white);
            foreach (bool hang in new[] { false, true })
            {
                float inset = (hang ? ToolkitDimensionGrid.HangMenuInset : ToolkitDimensionGrid.FitMenuInset) - 4;
                ToolkitDimensionGrid.Configure(text, inset);
                float left = -206 + inset, right = 206 - inset;
                float? feetX = null, decimalX = null;
                foreach (bool imperial in new[] { true, false })
                    foreach (string name in hang ? new[] { "Frame width", "Frame height", "Gap", "Hanger below top" } : new[] { "Width", "Height", "Depth" })
                        foreach (float value in new[] { 0f, .2159f, .3047999f, 2.9999f, 9.9996f, 10f })
                        {
                            text.text = name + "   " + ToolkitGeometry.Distance(value, imperial);
                            var mesh = Mesh(text);
                            var label = Ink(text.text, mesh, 0, name.Length);
                            int unit = text.text.LastIndexOf(imperial ? " in" : " m", StringComparison.Ordinal) + 1;
                            var finalUnit = Ink(text.text, mesh, unit, text.text.Length);
                            Check(Mathf.Abs(label.x - left) < .01f, "label left column: " + name);
                            Check(Mathf.Abs(finalUnit.y - right) < .01f, "symmetric outside margins and shared final unit column");
                            int dot = text.text.LastIndexOf('.');
                            float currentDecimal = Ink(text.text, mesh, dot, dot + 1).x;
                            if (!decimalX.HasValue) decimalX = currentDecimal;
                            Check(Mathf.Abs(decimalX.Value - currentDecimal) < .01f, "metric and inches decimals stay in the same fixed column");
                            int firstNumber = name.Length + 3;
                            var numberInk = Ink(text.text, mesh, firstNumber, unit - 1);
                            Check(label.y + 8 < numberInk.x, "labels have a clear gap before numeric columns: " + name);
                            if (imperial)
                            {
                                int ft = text.text.IndexOf(" ft ", StringComparison.Ordinal) + 1;
                                float currentFeet = Ink(text.text, mesh, ft, ft + 2).x;
                                if (!feetX.HasValue) feetX = currentFeet;
                                Check(Mathf.Abs(feetX.Value - currentFeet) < .01f, "feet unit stays fixed across values and labels");
                                float inchGap = Ink(text.text, mesh, ft + 3, unit - 1).x - Ink(text.text, mesh, ft, ft + 2).y;
                                Check(inchGap >= 7.9f, "two whole inches digits fit clear of ft: " + text.text + " gap=" + inchGap);
                            }
                        }
            }
            text.rectTransform.sizeDelta = new Vector2(460, 160); text.fontSize = 26;
            ToolkitDimensionGrid.Configure(text, ToolkitDimensionGrid.WorldInset);
            foreach (bool imperial in new[] { true, false })
            {
                text.text = "FIT CHECK\nW " + ToolkitGeometry.Distance(.2159f, imperial) + "\nH " + ToolkitGeometry.Distance(9.9996f, imperial) + "\nD " + ToolkitGeometry.Distance(.3047999f, imperial);
                var mesh = Mesh(text); float? feet = null;
                foreach (string name in new[] { "W", "H", "D" })
                {
                    int start = text.text.IndexOf("\n" + name + " ", StringComparison.Ordinal) + 1;
                    int end = text.text.IndexOf('\n', start); if (end < 0) end = text.text.Length;
                    Check(Mathf.Abs(Ink(text.text, mesh, start, start + 1).x + 150) < .01f, "world labels share a left column");
                    Check(Mathf.Abs(Ink(text.text, mesh, end - (imperial ? 2 : 1), end).y - 150) < .01f, "world final units share symmetric right column");
                    if (imperial)
                    {
                        int ft = text.text.IndexOf(" ft ", start, StringComparison.Ordinal) + 1;
                        float x = Ink(text.text, mesh, ft, ft + 2).x;
                        if (!feet.HasValue) feet = x;
                        Check(Mathf.Abs(feet.Value - x) < .01f, "world feet units align across W/H/D");
                    }
                }
            }
            return checks + " fixed dimension grid mesh checks passed.";
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static Vector2 Ink(string content, UIVertex[] mesh, int start, int end)
    {
        int quad = 0; float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int i = 0; i < end; i++)
            if (HasQuad(content[i]))
            {
                if (i >= start && content[i] != '\u2007')
                    for (int v = 0; v < 4; v++)
                    { min = Mathf.Min(min, mesh[quad * 4 + v].position.x); max = Mathf.Max(max, mesh[quad * 4 + v].position.x); }
                quad++;
            }
        return new Vector2(min, max);
    }

    static UIVertex[] Mesh(Text label)
    {
        using (var helper = new VertexHelper())
        {
            typeof(ToolkitNumericText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(label, new object[] { helper });
            int glyphs = label.text.Count(HasQuad);
            Check(helper.currentVertCount == glyphs * 4, "actual mesh maps glyphs including figure-space quads one-to-one");
            var vertices = new UIVertex[helper.currentVertCount];
            for (int i = 0; i < vertices.Length; i++) helper.PopulateUIVertex(ref vertices[i], i);
            var source = label.text.Where(HasQuad).ToArray();
            for (int i = 0; i < source.Length; i++)
                if (source[i] == '\u2007') Check(vertices[i * 4].color.a == 0, "figure-space fallback ink is invisible");
            return vertices;
        }
    }

    static void Stable(Text label, Func<float, string> format, float[] values)
    {
        Vector3[] reference = null;
        foreach (float value in values)
        {
            label.text = format(value);
            var mesh = Mesh(label);
            // Decimal points and the last unit glyph of every line must stay fixed.
            var visible = label.text.Where(HasQuad).ToArray();
            var positions = visible.Select((c, i) => new { c, i }).Where(x => x.c == '.' || x.c == 'm' || x.c == '\u00b2' || x.c == '\u00b0' || x.c == 'n')
                .Select(x => (mesh[x.i * 4].position + mesh[x.i * 4 + 2].position) * .5f).ToArray();
            if (reference == null) reference = positions;
            else Check(reference.Length == positions.Length && reference.Zip(positions, (a, b) => Vector3.Distance(a, b)).All(d => d < .01f), "decimal and unit vertex positions stable across digits/carry: " + label.text);
        }
    }
    static bool HasQuad(char c) => c != ' ' && c != '\t' && c != '\r' && c != '\n';
}
