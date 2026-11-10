using System;
using System.IO;
using UnityEngine;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        string dimensionKey, dimensionBefore, dimensionPreferences;
        Vector3 dimensionStart, dimensionRight;
        Plane dimensionPlane;
        float dimensionAt, dimensionValue;
        bool dimensionDragging;

        Vector3 DimensionCursor(Ray ray)
        {
            return dimensionPlane.Raycast(ray, out float distance) && distance >= 0 && distance < 20 ? ray.GetPoint(distance) : dimensionStart;
        }
        bool HandleDimensionDrag(ToolkitPaletteAction hit, Vector3 point)
        {
            if (state.Dwell) return false; // Dwell retains direct keypad entry.
            if (dimensionKey == null)
            {
                if (!state.TriggerDown || !hit || !hit.Action.StartsWith("edit:") || busy) return false;
                EndStickRotation();
                dimensionKey = hit.Action.Substring(5); dimensionValue = GetDimension(dimensionKey);
                dimensionBefore = JsonUtility.ToJson(Session); dimensionPreferences = JsonUtility.ToJson(Preferences);
                dimensionStart = point; dimensionRight = palette.transform.right;
                dimensionPlane = new Plane(palette.transform.forward, point);
                dimensionAt = Time.unscaledTime; dimensionDragging = false;
                return true;
            }
            if (!state.Tracked || state.SourceChanged || busy || !hasFocus)
            { CancelDimensionDrag(); return true; }
            if (!state.TriggerHeld)
            {
                string key = dimensionKey; bool dragged = dimensionDragging;
                if (dragged) FinishDimensionDrag(); else dimensionKey = null;
                if (!dragged) palette.BeginNumeric(key, GetDimension(key));
                return true;
            }
            float elapsed = Time.unscaledTime - dimensionAt;
            if (elapsed < 0.25f) return true;
            dimensionDragging = true;
            float delta = Vector3.Dot(DimensionCursor(state.Ray) - dimensionStart, dimensionRight);
            float min = dimensionKey == "gap" || dimensionKey == "inset" ? 0 : 0.05f;
            float max = dimensionKey == "gap" ? 1 : dimensionKey == "inset" ? FrameDimensions.y : dimensionKey.StartsWith("frame") ? 3 : dimensionKey == "height" ? 5 : 10;
            // A 10 cm cursor movement adjusts 2 cm, in millimeter increments; no placement snap.
            float value = Mathf.Clamp(Mathf.Round((dimensionValue + delta * 0.2f) * 1000) / 1000, min, max);
            if (Mathf.Abs(value - GetDimension(dimensionKey)) >= 0.0005f && SetDimension(dimensionKey, value))
                palette.RefreshDimension(dimensionKey, GetDimension(dimensionKey));
            return true;
        }
        void FinishDimensionDrag()
        {
            if (dimensionKey == null) return;
            if (dimensionBefore != JsonUtility.ToJson(Session))
            { undo.Add(dimensionBefore); if (undo.Count > 50) undo.RemoveAt(0); }
            dimensionKey = null;
            try { File.WriteAllText(PreferencesPath, JsonUtility.ToJson(Preferences)); }
            catch (Exception e) { Debug.LogWarning("Settings not saved: " + e.Message); }
            Message("Dimension updated.");
        }
        void CancelDimensionDrag()
        {
            if (dimensionKey == null) return;
            Session = JsonUtility.FromJson<ToolkitSession>(dimensionBefore);
            Preferences = JsonUtility.FromJson<ToolkitPreferences>(dimensionPreferences);
            dimensionKey = null; RenderAll(); if (palette) palette.Rebuild();
        }
    }
}
