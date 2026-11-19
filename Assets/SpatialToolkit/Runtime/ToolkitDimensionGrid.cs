using System;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialToolkit
{
    // Lay out the existing readout glyphs in fixed columns before Outline and
    // other UI mesh effects. The backing text and interaction targets are unchanged.
    public static class ToolkitDimensionGrid
    {
        public const float FitMenuInset = 80;
        public const float HangMenuInset = 20;
        public const float WorldInset = 80;

        public static void Configure(Text text, float inset) => ((ToolkitNumericText)text).UseDimensionGrid(inset);

        public static void Apply(ToolkitNumericText text, VertexHelper mesh, float cell)
        {
            string content = text.text;
            int[] quads = new int[content.Length]; int count = 0;
            for (int i = 0; i < content.Length; i++)
                quads[i] = HasQuad(content[i]) ? count++ * 4 : -1;
            float left = text.rectTransform.rect.xMin + text.DimensionInset;
            float right = text.rectTransform.rect.xMax - text.DimensionInset;
            float units = 1 / text.pixelsPerUnit;
            float gap = 8;
            float finalUnitLeft = right - Width(text, "in") * units;
            // Inches reserve two whole digits, a decimal point, and three decimals.
            float inchesWidth = 5 * cell + Width(text, ".") * units;
            float feetLeft = finalUnitLeft - gap - inchesWidth - gap - Width(text, "ft") * units;
            for (int start = 0; start < content.Length;)
            {
                int end = content.IndexOf('\n', start); if (end < 0) end = content.Length;
                bool imperial = end >= start + 3 && content.Substring(start, end - start).EndsWith(" in", StringComparison.Ordinal);
                bool metric = end >= start + 2 && content.Substring(start, end - start).EndsWith(" m", StringComparison.Ordinal);
                if (imperial || metric)
                {
                    int number = start;
                    while (number < end && content[number] != '\u2007' && !char.IsDigit(content[number])) number++;
                    int labelEnd = number;
                    while (labelEnd > start && content[labelEnd - 1] == ' ') labelEnd--;
                    MoveInk(mesh, quads, start, labelEnd, left, false);
                    int unitStart = end - (imperial ? 2 : 1);
                    MoveInk(mesh, quads, unitStart, end, right, true);
                    if (imperial)
                    {
                        int ft = content.IndexOf(" ft ", number, end - number, StringComparison.Ordinal);
                        if (ft >= 0)
                        {
                            MoveInk(mesh, quads, ft + 1, ft + 3, feetLeft, false);
                            MoveNumber(text, mesh, quads, number, ft, feetLeft - gap, cell);
                            MoveNumber(text, mesh, quads, ft + 4, unitStart - 1, finalUnitLeft - gap, cell);
                        }
                    }
                    else MoveNumber(text, mesh, quads, number, unitStart - 1, finalUnitLeft - gap, cell);
                }
                start = end + 1;
            }
        }

        static float Width(Text text, string value)
        {
            float cursor = 0, min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (char c in value)
            {
                if (!text.font.GetCharacterInfo(c, out var info, text.fontSize, text.fontStyle)) continue;
                min = Mathf.Min(min, cursor + info.minX); max = Mathf.Max(max, cursor + info.maxX);
                cursor += info.advance;
            }
            return float.IsInfinity(min) ? 0 : max - min;
        }

        static void MoveNumber(Text text, VertexHelper mesh, int[] quads, int start, int end, float right, float cell)
        {
            int last = end - 1;
            while (last >= start && !char.IsDigit(text.text[last])) last--;
            if (last < start || !text.font.GetCharacterInfo(text.text[last], out var info, text.fontSize, text.fontStyle)) return;
            var a = Vertex(mesh, quads[last]); var b = Vertex(mesh, quads[last] + 2);
            float units = 1 / text.pixelsPerUnit;
            float cellRight = (a.position.x + b.position.x) * .5f - (info.minX + info.maxX) * .5f * units
                - (cell - info.advance * units) * .5f + cell;
            Shift(mesh, quads, start, end, right - cellRight);
        }

        static void MoveInk(VertexHelper mesh, int[] quads, int start, int end, float target, bool right)
        {
            float edge = right ? float.NegativeInfinity : float.PositiveInfinity;
            for (int i = start; i < end; i++)
                if (quads[i] >= 0)
                    for (int v = 0; v < 4; v++)
                    {
                        float x = Vertex(mesh, quads[i] + v).position.x;
                        edge = right ? Mathf.Max(edge, x) : Mathf.Min(edge, x);
                    }
            if (!float.IsInfinity(edge)) Shift(mesh, quads, start, end, target - edge);
        }

        static void Shift(VertexHelper mesh, int[] quads, int start, int end, float shift)
        {
            for (int i = start; i < end; i++)
                if (quads[i] >= 0)
                    for (int v = 0; v < 4 && quads[i] + v < mesh.currentVertCount; v++)
                    {
                        var vertex = Vertex(mesh, quads[i] + v); vertex.position.x += shift;
                        mesh.SetUIVertex(vertex, quads[i] + v);
                    }
        }
        static UIVertex Vertex(VertexHelper mesh, int index)
        { var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, index); return vertex; }
        static bool HasQuad(char c) => c != ' ' && c != '\t' && c != '\r' && c != '\n';
    }
}
