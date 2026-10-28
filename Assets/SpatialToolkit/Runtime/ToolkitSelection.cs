using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        ToolkitHandle pendingGrab;
        float pendingGrabAt;
        Vector3 pendingGrabPosition;
        public ToolkitItemData Selection => Session.items.Find(i => i.id == selectedId);
        public int SelectedNode => selectedNode;
        public bool SelectedPathActive => IsPath(Selection) && Selection.id == activePath;
        public Vector3 FurnitureDimensions => Selection?.kind == ToolkitItemKind.Furniture ? Selection.dimensions : Preferences.furniture;
        public Vector3 FrameDimensions => Selection?.kind == ToolkitItemKind.Frames ? Selection.dimensions : Preferences.frame;
        public float FrameGap => Selection?.kind == ToolkitItemKind.Frames ? Selection.gap : Preferences.gap;
        public float FrameInset => Selection?.kind == ToolkitItemKind.Frames ? Selection.inset : Preferences.inset;
        public int FrameCount => Selection?.kind == ToolkitItemKind.Frames ? Selection.count : Preferences.frameCount;

        void ClearSelection(bool rebuild = true)
        {
            pendingGrab = null; selectedId = null; selectedNode = -1;
            RefreshSelectionFeedback();
            if (rebuild) { RenderAll(); palette.Rebuild(); }
        }

        ToolkitItemData GridPath(string grid) => string.IsNullOrEmpty(grid) ? null : Session.items.Find(i => i.kind == ToolkitItemKind.AnglePath && i.gridId == grid);

        void EnsureLevelLinks()
        {
            foreach (var path in Session.items.Where(i => i.kind == ToolkitItemKind.AnglePath && string.IsNullOrEmpty(i.gridId) && i.points.Count > 0))
            {
                var grid = Session.items.FirstOrDefault(i => i.kind == ToolkitItemKind.Level && Vector3.Distance(i.position, path.points[0]) < 0.03f && !Session.items.Any(p => p.gridId == i.id));
                if (grid != null) path.gridId = grid.id;
            }
        }

        void SelectHandle(ToolkitHandle handle, bool armDrag)
        {
            if (!handle) return;
            var item = Session.items.Find(i => i.id == handle.ItemId);
            if (item == null) return;
            var linked = item.kind == ToolkitItemKind.Level ? GridPath(item.id) : null;
            selectedId = linked?.id ?? item.id;
            selectedNode = linked != null ? 0 : handle.Node;
            var selection = Selection;
            Tool = selection.kind == ToolkitItemKind.Furniture ? ToolkitTool.Fit : selection.kind == ToolkitItemKind.Frames ? ToolkitTool.Hang :
                selection.kind == ToolkitItemKind.Measurement ? ToolkitTool.Measure : ToolkitTool.Level;
            if (activePath != null && activePath != selectedId) activePath = null;
            pendingGrab = armDrag ? handle : null; pendingGrabAt = Time.unscaledTime; pendingGrabPosition = state.Position;
            // Keep the selected handle alive so a sustained pinch can become a drag.
            RefreshSelectionFeedback(); palette.Rebuild(); Message("Selected. Edit in the menu, or hold and move to drag.");
        }

        void UpdatePendingGrab(bool blockedByMenu)
        {
            if (!pendingGrab) return;
            if (blockedByMenu || !state.TriggerHeld || state.SourceChanged || busy || !platform.IsLocalized)
            { pendingGrab = null; return; }
            if (Time.unscaledTime - pendingGrabAt < 0.22f) return;
            if (Vector3.Distance(state.Position, pendingGrabPosition) < 0.008f && Time.unscaledTime - pendingGrabAt < 0.4f) return;
            var handle = pendingGrab; pendingGrab = null; BeginGrabHandle(handle);
        }

        bool TryNodeAction(ToolkitHandle handle)
        {
            var path = Session.items.Find(i => i.id == activePath);
            if (!IsPath(path) || path.points.Count == 0) return false;
            ToolkitItemData hitPath = handle ? Session.items.Find(i => i.id == handle.ItemId && IsPath(i)) : null;
            int node = hitPath != null ? handle.Node : -1;
            if (node < 0 && Snapping && validSurface)
            {
                foreach (var candidate in Session.items.Where(IsPath))
                {
                    int index = candidate.points.FindIndex(p => Vector3.Distance(RoomRoot.TransformPoint(p), target) < 0.001f);
                    if (index >= 0) { hitPath = candidate; node = index; break; }
                }
            }
            if (hitPath == null || node < 0) return false;
            // Branches can share a visible origin with another path. Resolve that
            // shared node against the active path before applying closure rules.
            Vector3 hitPoint = hitPath.points[node];
            int activeNode = path.points.FindLastIndex(p => Vector3.Distance(p, hitPoint) < 0.001f);
            if (activeNode >= 0) { hitPath = path; node = activeNode; }
            int count = path.points.Count;
            if (hitPath == path && (node == count - 1 || node == count - 2))
            { FinishPath(); SelectNodeWithoutDrag(path, node); return true; }
            if (path.kind == ToolkitItemKind.AnglePath)
            { FinishPath(); SelectNodeWithoutDrag(hitPath, node); return true; }

            // A return to an earlier vertex closes the suffix beginning at that vertex.
            // Earlier segments remain as their own open measurement, without losing nodes.
            int start = hitPath == path ? node : 0;
            var outline = path.points.Skip(start).ToList();
            if (hitPath != path) outline.Add(hitPath.points[node]);
            if (!ToolkitGeometry.TryArea(outline, out _, out var reason))
            { Message(reason); return true; }
            if (hitPath != path && path.points.Count >= 128 || start > 0 && Session.items.Count >= 100)
            { Message("Finish this path before adding more points."); return true; }
            Remember();
            if (start > 0)
                Session.items.Add(new ToolkitItemData { kind = ToolkitItemKind.Measurement, points = path.points.Take(start + 1).ToList() });
            path.points = outline; path.closed = true; activePath = null; lastNodePress = -10;
            selectedId = path.id; selectedNode = 0; RenderAll(); palette.Rebuild(); ArmSelectedNodeDrag(); Haptic();
            Message("Area closed at the selected node."); return true;
        }

        void SelectNodeWithoutDrag(ToolkitItemData item, int node)
        {
            selectedId = item.id; selectedNode = node; pendingGrab = null;
            Tool = item.kind == ToolkitItemKind.AnglePath ? ToolkitTool.Level : ToolkitTool.Measure;
            RenderAll(); palette.Rebuild(); ArmSelectedNodeDrag();
        }

        void ArmSelectedNodeDrag()
        {
            if (!state.Gesture || state.Dwell || !state.TriggerHeld || Selection == null) return;
            pendingGrab = visuals[Selection.id].GetComponentsInChildren<ToolkitHandle>().FirstOrDefault(h => h.Node == selectedNode);
            pendingGrabAt = Time.unscaledTime; pendingGrabPosition = state.Position;
        }

        void StartPathAtSelection()
        {
            var source = Selection;
            if (!IsPath(source) || selectedNode < 0 || selectedNode >= source.points.Count) return;
            int required = source.kind == ToolkitItemKind.AnglePath ? 2 : 1;
            if (Session.items.Count + required > 100) { Message("Delete an item to make room for a new path."); return; }
            Vector3 point = source.points[selectedNode]; Remember();
            var path = new ToolkitItemData { kind = source.kind, points = new List<Vector3> { point } };
            if (source.kind == ToolkitItemKind.AnglePath)
            {
                var previousGrid = Session.items.Find(i => i.id == source.gridId);
                var grid = new ToolkitItemData { kind = ToolkitItemKind.Level, position = point, rotation = previousGrid?.rotation ?? Quaternion.identity };
                Session.items.Add(grid); path.gridId = grid.id;
            }
            Session.items.Add(path); activePath = path.id; selectedId = path.id; selectedNode = 0; lastNodePress = -10;
            RenderAll(); palette.Rebuild(); Message("New path started at the selected node.");
        }

        void RemoveNodeOrItem(ToolkitItemData item, int node)
        {
            if (IsPath(item) && node >= 0 && node < item.points.Count)
            {
                if (item.kind == ToolkitItemKind.AnglePath && node == 0)
                { Session.items.RemoveAll(i => i.id == item.gridId || i.id == item.id); }
                else
                {
                    item.points.RemoveAt(node); item.closed = false;
                    if (item.points.Count == 0) Session.items.Remove(item);
                }
            }
            else
            {
                Session.items.Remove(item);
                if (item.kind == ToolkitItemKind.Level) Session.items.RemoveAll(i => i.gridId == item.id);
            }
            if (!Session.items.Any(i => i.id == activePath)) activePath = null;
        }

        bool EditSelectedDimension(string key, float value)
        {
            var item = Selection;
            if (item == null || item.kind != ToolkitItemKind.Furniture && item.kind != ToolkitItemKind.Frames) return false;
            var dimensions = item.dimensions; float gap = item.gap, inset = item.inset;
            switch (key)
            {
                case "width": dimensions.x = value; break;
                case "height": dimensions.y = value; break;
                case "depth": dimensions.z = value; break;
                case "framewidth": dimensions.x = value; break;
                case "frameheight": dimensions.y = value; inset = Mathf.Min(inset, value); break;
                case "gap": gap = value; break;
                case "inset": inset = value; break;
                default: return false;
            }
            Vector3 position = item.position;
            if (item.kind == ToolkitItemKind.Furniture)
            {
                position += RoomRoot.InverseTransformDirection(Vector3.up * (dimensions.y - item.dimensions.y) * 0.5f);
                if (Overlaps(RoomRoot.TransformPoint(position), dimensions, RoomRoot.rotation * item.rotation, visuals[item.id].transform))
                { Message("That size overlaps a surface. The selection was kept at its previous size."); return false; }
            }
            Remember(); item.dimensions = dimensions; item.position = position; item.gap = gap; item.inset = inset;
            RenderItem(item); Message("Selection updated."); return true;
        }

        bool DispatchSelectionEdit(string key, int direction, float step)
        {
            var item = Selection;
            if (item == null) return false;
            if (key == "count" && item.kind == ToolkitItemKind.Frames)
            { Remember(); item.count = Mathf.Clamp(item.count + direction, 1, 8); RenderItem(item); return true; }
            bool dimension = item.kind == ToolkitItemKind.Furniture && (key == "width" || key == "height" || key == "depth") ||
                item.kind == ToolkitItemKind.Frames && (key == "framewidth" || key == "frameheight" || key == "gap" || key == "inset");
            if (!dimension) return false;
            float min = key == "gap" || key == "inset" ? 0 : 0.05f;
            float max = key == "gap" ? 1 : key == "inset" ? FrameDimensions.y : key.StartsWith("frame") ? 3 : key == "height" ? 5 : 10;
            EditSelectedDimension(key, Mathf.Clamp(GetDimension(key) + direction * step, min, max)); return true;
        }
    }
}
