using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SpatialToolkit
{
    public static class ToolkitGeometry
    {
        public static bool TryArea(IReadOnlyList<Vector3> points, out float area, out string reason)
        {
            area = 0;
            reason = "Add at least three points.";
            if (points == null || points.Count < 3) return false;
            Vector3 origin = points[0], normal = Vector3.zero;
            for (int i = 1; i < points.Count - 1; i++)
            {
                normal = Vector3.Cross(points[i] - origin, points[i + 1] - origin);
                if (normal.sqrMagnitude > 0.000001f) break;
            }
            if (normal.sqrMagnitude < 0.000001f) { reason = "Points are on one line."; return false; }
            normal.Normalize();
            var u = (points[1] - origin).normalized;
            if (u.sqrMagnitude < 0.5f) { reason = "Two points overlap."; return false; }
            var v = Vector3.Cross(normal, u).normalized;
            var projected = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var delta = points[i] - origin;
                if (Mathf.Abs(Vector3.Dot(delta, normal)) > 0.02f)
                { reason = "Area needs points on the same surface."; return false; }
                projected[i] = new Vector2(Vector3.Dot(delta, u), Vector3.Dot(delta, v));
                if ((points[i] - points[(i + 1) % points.Count]).sqrMagnitude < 0.000001f)
                { reason = "Two points overlap."; return false; }
            }
            for (int i = 0; i < projected.Length; i++)
                for (int j = i + 1; j < projected.Length; j++)
                {
                    int nextI = (i + 1) % projected.Length, nextJ = (j + 1) % projected.Length;
                    if (i == j || nextI == j || nextJ == i) continue;
                    if (SegmentsIntersect(projected[i], projected[nextI], projected[j], projected[nextJ]))
                    { reason = "Outline crosses itself."; return false; }
                }
            double twiceArea = 0;
            for (int i = 0; i < projected.Length; i++)
            {
                var a = projected[i]; var b = projected[(i + 1) % projected.Length];
                twiceArea += (double)a.x * b.y - (double)b.x * a.y;
            }
            area = (float)(Math.Abs(twiceArea) * 0.5);
            reason = area > 0.0001f ? "" : "Outline is too small.";
            return area > 0.0001f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            const float epsilon = 0.000001f;
            if (abC * abD < -epsilon && cdA * cdB < -epsilon) return true;
            return (Mathf.Abs(abC) < epsilon && OnSegment(a, b, c)) ||
                (Mathf.Abs(abD) < epsilon && OnSegment(a, b, d)) ||
                (Mathf.Abs(cdA) < epsilon && OnSegment(c, d, a)) ||
                (Mathf.Abs(cdB) < epsilon && OnSegment(c, d, b));
        }
        static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
            p.x >= Mathf.Min(a.x, b.x) - 0.00001f && p.x <= Mathf.Max(a.x, b.x) + 0.00001f &&
            p.y >= Mathf.Min(a.y, b.y) - 0.00001f && p.y <= Mathf.Max(a.y, b.y) + 0.00001f;

        public static Vector3 SnapOnSurface(Vector3 point, Vector3 origin, Vector3 normal, float interval)
        {
            if (interval <= 0) return point;
            SurfaceBasis(normal, out var right, out var up);
            var delta = point - origin;
            return origin + right * (Mathf.Round(Vector3.Dot(delta, right) / interval) * interval) +
                up * (Mathf.Round(Vector3.Dot(delta, up) / interval) * interval) +
                normal.normalized * Vector3.Dot(delta, normal.normalized);
        }
        public static void SurfaceBasis(Vector3 normal, out Vector3 right, out Vector3 up)
        {
            normal = normal.normalized;
            up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            if (up.sqrMagnitude < 0.5f) up = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
            right = Vector3.Cross(up, normal).normalized;
        }
        public static Vector2[] NailPositions(int count, float width, float height, float gap, float topInset)
        {
            if (count < 1 || count > 8 || width <= 0 || height <= 0 || gap < 0 || topInset < 0 || topInset > height)
                throw new ArgumentOutOfRangeException(nameof(count), "Check frame dimensions and hanger inset.");
            var result = new Vector2[count];
            float total = count * width + (count - 1) * gap;
            for (int i = 0; i < count; i++)
                result[i] = new Vector2(-total * 0.5f + width * 0.5f + i * (width + gap), height * 0.5f - topInset);
            return result;
        }
        public static string Distance(float meters, bool imperial)
        {
            if (!imperial) return meters.ToString("0.00", CultureInfo.InvariantCulture) + " m";
            int eighths = (int)Math.Round(meters / 0.0254 * 8, MidpointRounding.AwayFromZero);
            int feet = eighths / 96; float inches = (eighths % 96) / 8f;
            return feet + " ft " + inches.ToString("0.###", CultureInfo.InvariantCulture) + " in";
        }
        public static string Area(float squareMeters, bool imperial) =>
            (imperial ? squareMeters * 10.7639104f : squareMeters).ToString("0.00", CultureInfo.InvariantCulture) +
            (imperial ? " sq ft" : " m²");
        public static float LevelError(Vector3 axis) => Mathf.Asin(Mathf.Clamp(Mathf.Abs(axis.normalized.y), 0, 1)) * Mathf.Rad2Deg;
        public static float PlumbError(Vector3 axis) => Vector3.Angle(axis, axis.y < 0 ? Vector3.down : Vector3.up);
    }
}
