using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialToolkit
{
    public static class ToolkitVisuals
    {
        public static int ColorOption { get; private set; }
        public static void SetColorOption(int option) { ColorOption = Mathf.Clamp(option, 0, 2); }
        public static Color Mint => ColorOption == 0 ? new Color(0.35f, 1f, 0.81f) : ColorOption == 1 ? new Color(0.35f, 0.78f, 1f) : Color.white;
        public static Color Lavender => ColorOption == 0 ? new Color(0.73f, 0.65f, 1f) : ColorOption == 1 ? new Color(1f, 0.85f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
        public static Color Coral => ColorOption == 0 ? new Color(1f, 0.55f, 0.56f) : ColorOption == 1 ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.88f, 0.15f);
        public static Color Panel => ColorOption == 2 ? Color.black : new Color(0.035f, 0.065f, 0.10f, ColorOption == 0 ? 0.94f : 0.98f);
        public static Color Button => ColorOption == 2 ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.19f, 0.24f, 0.31f);
        public static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
        static Font font;
        public static Material Template;
        public static Material LabelTemplate;
        public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Material Material(Color color)
        {
            if (Template) return new Material(Template) { name = "Spatial Toolkit runtime overlay", color = color, enableInstancing = true };
            var shader = Shader.Find("SpatialToolkit/Glass");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            return new Material(shader) { name = "Spatial Toolkit runtime overlay", color = color, enableInstancing = true };
        }
        public static LineRenderer Line(Transform parent, IReadOnlyList<Vector3> positions, Color color, float width = 0.002f, bool loop = false)
        {
            var go = new GameObject("Guide line"); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Material(color); line.useWorldSpace = false;
            line.widthMultiplier = width; line.numCapVertices = 4; line.numCornerVertices = 3;
            line.positionCount = positions.Count; line.loop = loop;
            for (int i = 0; i < positions.Count; i++) line.SetPosition(i, positions[i]);
            go.AddComponent<ToolkitReadableLine>().BaseWidth = width;
            if (ColorOption == 2)
            {
                go.GetComponent<ToolkitReadableLine>().BaseWidth = width * 1.4f;
                var outline = new GameObject("Contrast outline"); outline.transform.SetParent(go.transform, false);
                var border = outline.AddComponent<LineRenderer>();
                border.sharedMaterial = Material(Color.black); border.useWorldSpace = false;
                border.widthMultiplier = width * 3.2f; border.numCapVertices = 4; border.numCornerVertices = 3;
                border.positionCount = positions.Count; border.loop = loop; border.sortingOrder = -1;
                for (int i = 0; i < positions.Count; i++) border.SetPosition(i, positions[i]);
                outline.AddComponent<ToolkitReadableLine>().BaseWidth = width * 3.2f;
                var follower = outline.AddComponent<ToolkitLineOutline>(); follower.Source = line;
            }
            return line;
        }
        public static GameObject Sphere(Transform parent, Vector3 position, float diameter, Color color, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Measurement node"; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
        public static Text Label(Transform parent, Vector3 position, string content, Color color, int size = 26)
        {
            var go = new GameObject("Readout", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.001f;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(460, 90);
            var text = Text(go.transform, new Vector2(460, 90), Vector2.zero, content, size, color);
            if (ColorOption > 0)
            {
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black; outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            if (LabelTemplate)
            {
                var material = new Material(LabelTemplate);
                text.material = material; text.gameObject.AddComponent<ToolkitLabelCleanup>().Material = material;
            }
            text.gameObject.AddComponent<ToolkitBillboard>();
            return text;
        }
        public static Text Text(Transform parent, Vector2 size, Vector2 position, string content, int fontSize, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = go.GetComponent<Text>(); text.font = Font; text.fontSize = fontSize;
            text.color = color; text.alignment = TextAnchor.MiddleCenter; text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; return text;
        }
        public static void BoxEdges(Transform parent, Vector3 size, Color color)
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++) p[i] = new Vector3((i & 1) == 0 ? -size.x / 2 : size.x / 2,
                (i & 2) == 0 ? -size.y / 2 : size.y / 2, (i & 4) == 0 ? -size.z / 2 : size.z / 2);
            for (int i = 0; i < 8; i++) for (int bit = 1; bit <= 4; bit *= 2)
                if ((i & bit) == 0) Line(parent, new[] { p[i], p[i | bit] }, color);
        }
    }

    public sealed class ToolkitBillboard : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Camera.main && transform.parent)
            {
                var direction = transform.parent.position - Camera.main.transform.position;
                if (direction.sqrMagnitude > 0.0001f) transform.parent.rotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.parent.localScale = Vector3.one * (0.001f * Mathf.Clamp(direction.magnitude / 1.4f, 1, 3));
            }
        }
    }
    public sealed class ToolkitReadableLine : MonoBehaviour
    {
        public float BaseWidth;
        LineRenderer line;
        void LateUpdate()
        {
            if (!line) line = GetComponent<LineRenderer>();
            if (!line || !Camera.main) return;
            float distance = Vector3.Distance(line.bounds.center, Camera.main.transform.position);
            line.widthMultiplier = BaseWidth * Mathf.Clamp(distance / 1.5f, 1, 4);
        }
    }
    public sealed class ToolkitLineOutline : MonoBehaviour
    {
        public LineRenderer Source;
        LineRenderer line;
        void LateUpdate()
        {
            if (!Source) return;
            if (!line) line = GetComponent<LineRenderer>();
            line.useWorldSpace = Source.useWorldSpace;
            line.positionCount = Source.positionCount; line.loop = Source.loop;
            for (int i = 0; i < Source.positionCount; i++) line.SetPosition(i, Source.GetPosition(i));
        }
    }
    public sealed class ToolkitLabelCleanup : MonoBehaviour
    {
        public Material Material;
        void OnDestroy() { if (Material) Destroy(Material); }
    }

    public sealed class ToolkitPaletteAction : MonoBehaviour { public string Action; }
    public sealed class ToolkitHandle : MonoBehaviour { public string ItemId; public int Node = -1; }
    public sealed class ToolkitOwned : MonoBehaviour { }

    // Materials created for runtime geometry are owned by that geometry.
    public sealed class ToolkitMaterialCleanup : MonoBehaviour
    {
        void OnDestroy()
        {
            var materials = new HashSet<Material>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterial && renderer.sharedMaterial.name.StartsWith("Spatial Toolkit runtime"))
                    materials.Add(renderer.sharedMaterial);
            foreach (var material in materials) Destroy(material);
        }
    }
}
