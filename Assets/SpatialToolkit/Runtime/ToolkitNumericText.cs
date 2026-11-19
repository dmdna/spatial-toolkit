using UnityEngine;
using UnityEngine.UI;

namespace SpatialToolkit
{
    // Runtime labels are plain Legacy UI Text (no TMP-only <mspace> tags).
    // Keep the normal generator's layout, then give digits and figure spaces
    // identical advances in its mesh. Prose, punctuation and units keep their font.
    public sealed class ToolkitNumericText : Text
    {
        public bool DimensionGrid { get; private set; }
        public float DimensionInset { get; private set; }
        public void UseDimensionGrid(float inset)
        {
            DimensionGrid = true; DimensionInset = inset;
            horizontalOverflow = HorizontalWrapMode.Overflow;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (font) font.RequestCharactersInTexture("0123456789\u2007.ftinm", fontSize, fontStyle);
            base.OnPopulateMesh(mesh);
            if (!font || supportRichText || string.IsNullOrEmpty(text)) return;
            float cell = 0;
            for (char digit = '0'; digit <= '9'; digit++)
                if (font.GetCharacterInfo(digit, out var info, fontSize, fontStyle)) cell = Mathf.Max(cell, info.advance);
            if (cell <= 0) return;

            var characters = cachedTextGenerator.characters;
            var lines = cachedTextGenerator.lines;
            float units = 1 / pixelsPerUnit;
            int quad = 0;
            float anchor = ((int)alignment % 3) * 0.5f;
            for (int line = 0; line < lines.Count; line++)
            {
                int start = lines[line].startCharIdx;
                int end = Mathf.Min(text.Length, line + 1 < lines.Count ? lines[line + 1].startCharIdx : text.Length);
                end = Mathf.Min(end, characters.Count);
                float extra = 0;
                for (int i = start; i < end; i++)
                    if (Cell(text[i])) extra += cell - characters[i].charWidth;
                float shift = -extra * anchor;
                for (int i = start; i < end; i++)
                {
                    float delta = Cell(text[i]) ? cell - characters[i].charWidth : 0;
                    // Legacy emits a quad for U+2007 (even a fallback mark), but
                    // omits ASCII spaces, tabs and line breaks. Consume the padding
                    // quad and hide its ink; never let it shift the following glyph mapping.
                    if (HasQuad(text[i]))
                    {
                        for (int v = 0; v < 4 && quad * 4 + v < mesh.currentVertCount; v++)
                        {
                            var vertex = new UIVertex();
                            mesh.PopulateUIVertex(ref vertex, quad * 4 + v);
                            vertex.position.x += (shift + delta * 0.5f) * units;
                            if (text[i] == '\u2007') vertex.color.a = 0;
                            mesh.SetUIVertex(vertex, quad * 4 + v);
                        }
                        quad++;
                    }
                    shift += delta;
                }
            }
            if (DimensionGrid) ToolkitDimensionGrid.Apply(this, mesh, cell * units);
        }

        static bool Cell(char c) => c >= '0' && c <= '9' || c == '\u2007';
        static bool HasQuad(char c) => c != ' ' && c != '\t' && c != '\r' && c != '\n';
    }
}
