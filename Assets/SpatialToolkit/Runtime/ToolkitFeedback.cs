using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        readonly List<ToolkitSelectionVisual> selectionVisuals = new List<ToolkitSelectionVisual>();
        string hoverId;
        int hoverNode = -1;

        void RegisterSelectionVisual(ToolkitHandle handle, ToolkitItemData item)
        {
            var visual = handle.gameObject.AddComponent<ToolkitSelectionVisual>();
            visual.ItemId = item.id; visual.Node = handle.Node;
            var linked = item.kind == ToolkitItemKind.Level ? GridPath(item.id) : null;
            visual.TargetId = linked?.id ?? item.id; visual.TargetNode = linked != null ? 0 : handle.Node;
            Vector3 bounds = item.kind == ToolkitItemKind.Furniture ? item.dimensions : item.kind == ToolkitItemKind.Frames ?
                new Vector3(item.count * item.dimensions.x + (item.count - 1) * item.gap, item.dimensions.y, 0.018f) : new Vector3(1.2f, 1.2f, 0.015f);
            visual.Initialize(bounds, handle.Node >= 0);
            selectionVisuals.Add(visual); ApplySelectionVisual(visual);
        }
        void UpdateWorldHover(ToolkitHandle handle)
        {
            hoverId = handle ? handle.ItemId : null; hoverNode = handle ? handle.Node : -1;
            var linked = GridPath(hoverId);
            if (linked != null) { hoverId = linked.id; hoverNode = 0; }
            RefreshSelectionFeedback();
        }
        void RefreshSelectionFeedback()
        {
            foreach (var visual in selectionVisuals) if (visual) ApplySelectionVisual(visual);
        }
        void ApplySelectionVisual(ToolkitSelectionVisual visual)
        {
            string id = visual.TargetId; int node = visual.TargetNode;
            visual.SetState(id == selectedId && node == selectedNode, id == hoverId && node == hoverNode);
        }
    }

    // Captures original tints once; hover adds an outline and selection also tints
    // the item. Every tool uses the same accent within the chosen color preset.
    public sealed class ToolkitSelectionVisual : MonoBehaviour
    {
        public string ItemId;
        public int Node;
        public string TargetId;
        public int TargetNode;
        Renderer[] renderers;
        Color[] colors;
        Text[] labels;
        Color[] labelColors;
        GameObject outline;
        Renderer[] outlineRenderers;
        bool wasSelected;
        public bool Selected { get; private set; }
        public bool Hovered { get; private set; }
        public void Initialize(Vector3 bounds, bool sphere)
        {
            var sources = new List<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                if (!renderer.GetComponent<ToolkitLineOutline>()) sources.Add(renderer);
            renderers = sources.ToArray(); colors = new Color[renderers.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = renderers[i].sharedMaterial.color;
            labels = GetComponentsInChildren<Text>(); labelColors = new Color[labels.Length];
            for (int i = 0; i < labels.Length; i++) labelColors[i] = labels[i].color;
            outline = new GameObject("Selection outline");
            // Node outlines have world-space vertices, keeping line widths in meters.
            outline.transform.SetParent(sphere ? transform.parent : transform, false);
            if (sphere)
            {
                var points = new Vector3[40];
                var line = ToolkitVisuals.Line(outline.transform, points, ToolkitVisuals.Selection, 0.0015f, true);
                line.useWorldSpace = true;
                var halo = outline.AddComponent<ToolkitNodeHalo>(); halo.Source = transform; halo.Line = line;
            }
            else ToolkitVisuals.BoxEdges(outline.transform, bounds + Vector3.one * 0.006f, ToolkitVisuals.Selection);
            outlineRenderers = outline.GetComponentsInChildren<Renderer>();
            foreach (var renderer in outlineRenderers)
            {
                renderer.sortingOrder = renderer.GetComponent<ToolkitLineOutline>() ? 29 : 30;
                renderer.sharedMaterial.renderQueue = 3050;
                if (renderer.sharedMaterial.HasProperty("_ZTest")) renderer.sharedMaterial.SetFloat("_ZTest", 8);
            }
            outline.SetActive(false);
        }
        public void SetState(bool selected, bool hovered)
        {
            bool visible = Selected || Hovered;
            Selected = selected; Hovered = hovered;
            if (outline && visible != (selected || hovered)) outline.SetActive(selected || hovered);
            if (selected || hovered)
                foreach (var renderer in outlineRenderers)
                    if (renderer && !renderer.GetComponent<ToolkitLineOutline>()) renderer.sharedMaterial.color = ToolkitVisuals.Selection;
            if (selected || wasSelected)
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i])
                {
                    Color tint = ToolkitVisuals.Selection;
                    tint.a = colors[i].a >= 0.8f ? 1 : renderers[i] is LineRenderer ? 0.85f : 0.22f;
                    renderers[i].sharedMaterial.color = selected ? tint : colors[i];
                }
                for (int i = 0; i < labels.Length; i++) if (labels[i]) labels[i].color = selected ? ToolkitVisuals.Selection : labelColors[i];
            }
            wasSelected = selected;
        }
    }
    public sealed class ToolkitNodeHalo : MonoBehaviour
    {
        public Transform Source;
        public LineRenderer Line;
        void LateUpdate()
        {
            if (!Source || !Line || !Camera.main) return;
            var camera = Camera.main.transform;
            float radius = Mathf.Max(Source.lossyScale.x, Source.lossyScale.y, Source.lossyScale.z) * 0.625f;
            for (int i = 0; i < Line.positionCount; i++)
            {
                float angle = 2 * Mathf.PI * i / Line.positionCount;
                Line.SetPosition(i, Source.position + (camera.right * Mathf.Cos(angle) + camera.up * Mathf.Sin(angle)) * radius);
            }
        }
    }
}
