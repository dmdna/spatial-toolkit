using UnityEngine;
using UnityEngine.UI;

namespace SpatialToolkit
{
    public sealed partial class ToolkitPalette : MonoBehaviour
    {
        SpatialToolkitApp app;
        Canvas canvas;
        Text status;
        bool pinned;
        Sprite rounded;
        string editing, number;
        bool replaceNumber;
        bool controls, fallbackPlaced, settings, about, minimized;
        Text undoLabel;
        Image undoProgress;
        Sprite pill;
        string inputSource;
        ToolkitPaletteAction highlighted;
        Color highlightColor;
        Color highlightTextColor;
        public bool Visible => canvas && canvas.gameObject.activeSelf;
        public void Initialize(SpatialToolkitApp owner) { app = owner; Build(); }
        public void Rebuild() { Highlight(null, 0); undoLabel = null; undoProgress = null; if (canvas) { canvas.gameObject.SetActive(false); Destroy(canvas.gameObject); } Build(); }
        public void Navigate(string page)
        {
            settings = page == "settings"; about = page == "about";
            controls = false; editing = null; minimized = page == "minimize";
            Rebuild();
        }
        public void ResetWristPose() { pinned = false; fallbackPlaced = false; }
        public void UndoHold(float progress, bool loading)
        {
            if (undoLabel) { undoLabel.text = loading ? "Clear All (Hold)" : "Undo"; undoLabel.fontSize = loading ? 17 : 23; }
            if (undoProgress) { undoProgress.gameObject.SetActive(loading); undoProgress.fillAmount = progress; }
        }
        public void ToggleControls() { controls = !controls; Rebuild(); }
        public void TogglePin() { pinned = !pinned; Rebuild(); }
        public void Highlight(ToolkitPaletteAction action, float progress)
        {
            if (action != highlighted)
            {
                if (highlighted)
                {
                    highlighted.GetComponent<Image>().color = highlightColor;
                    var oldOutline = highlighted.GetComponent<Outline>();
                    if (oldOutline) oldOutline.enabled = false;
                    var oldText = highlighted.GetComponentInChildren<Text>();
                    if (oldText) oldText.color = highlightTextColor;
                }
                highlighted = action;
                if (highlighted)
                {
                    highlightColor = highlighted.GetComponent<Image>().color;
                    var text = highlighted.GetComponentInChildren<Text>();
                    if (text) highlightTextColor = text.color;
                }
            }
            if (highlighted)
            {
                var text = highlighted.GetComponentInChildren<Text>();
                highlighted.GetComponent<Image>().color = text ? Color.Lerp(highlightColor, ToolkitVisuals.Mint, 0.55f + 0.45f * progress) : highlightColor;
                if (text) text.color = Color.black;
                var outline = highlighted.GetComponent<Outline>();
                if (!outline) outline = highlighted.gameObject.AddComponent<Outline>();
                outline.enabled = true; outline.effectColor = ToolkitVisuals.Mint; outline.effectDistance = new Vector2(2, -2);
            }
        }
        void Build()
        {
            if (!rounded) rounded = RoundedSprite();
            var panel = new GameObject("Wrist palette", typeof(RectTransform), typeof(Canvas), typeof(ToolkitOwned));
            panel.transform.SetParent(transform, false);
            panel.transform.localScale = Vector3.one * 0.0006f;
            canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            var rect = panel.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(620, 790);
            if (minimized) { BuildPill(panel.transform, rect); return; }
            var background = Image(panel.transform, rect.sizeDelta, Vector2.zero, ToolkitVisuals.Panel);
            AddHitTarget(background, "block", rect.sizeDelta, 14);
            if (!controls && editing == null && !settings && !about)
            {
                IconButton(panel.transform, new Vector2(-265, 350), "settings", true);
                IconButton(panel.transform, new Vector2(265, 350), "minimize", false);
            }
            if (!about)
            {
                var dragMargin = Image(panel.transform, new Vector2(360, 14), new Vector2(0, 384), ToolkitVisuals.WithAlpha(ToolkitVisuals.Mint, 0.16f));
                AddHitTarget(dragMargin, "dragmenu", new Vector2(360, 14));
            }
            ToolkitVisuals.Text(panel.transform, new Vector2(450, 35), new Vector2(0, 350), "S P A T I A L   T O O L K I T", 22, ToolkitVisuals.Mint);
            ToolkitVisuals.Text(panel.transform, new Vector2(560, 50), new Vector2(0, 305), app.Selection != null ? "EDIT SELECTION" : app.InputSource, 24, Color.white);
            if (settings || about)
            {
                BuildSettings(panel.transform);
                status = ToolkitVisuals.Text(panel.transform, new Vector2(560, 65), new Vector2(0, -350), app.Status, 21, ToolkitVisuals.Mint);
                return;
            }
            if (controls)
            {
                BuildControls(panel.transform);
                status = ToolkitVisuals.Text(panel.transform, new Vector2(560, 65), new Vector2(0, -350), app.Status, 21, ToolkitVisuals.Mint);
                return;
            }
            if (editing != null)
            {
                BuildKeypad(panel.transform);
                status = ToolkitVisuals.Text(panel.transform, new Vector2(560, 70), new Vector2(0, -350), app.Status, 21, ToolkitVisuals.Mint);
                return;
            }
            string[] titles = { "Measure", "Fit check", "Hang", "Level" };
            for (int i = 0; i < titles.Length; i++)
                Button(panel.transform, new Vector2(-225 + i * 150, 240), new Vector2(138, 64), titles[i], "tool:" + i,
                    (int)app.Tool == i ? ToolkitVisuals.Mint : ToolkitVisuals.Button);
            var selection = app.Selection;
            float y = selection != null && app.Tool == ToolkitTool.Hang ? 165 : selection != null ? 130 : 155;
            if (selection != null && SpatialToolkitApp.IsPath(selection))
            {
                BuildPathEdit(panel.transform);
            }
            else if (app.Tool == ToolkitTool.Measure)
            {
                ToolkitVisuals.Text(panel.transform, new Vector2(550, 85), new Vector2(0, y), (app.DwellInput ? "Dwell on a surface to add a node." : app.HandsInput ? "Aim, then pinch to add a node." : "Point at a surface. Trigger to add a node.") + "\nDouble-press or select a node to finish.", 24, Color.white);
                Button(panel.transform, new Vector2(-140, y - 95), new Vector2(260, 58), "Finish path", "finish");
                Button(panel.transform, new Vector2(140, y - 95), new Vector2(260, 58), "Close area", "close");
            }
            else if (app.Tool == ToolkitTool.Fit)
            {
                Row("Width", app.FurnitureDimensions.x, y, "width");
                Row("Height", app.FurnitureDimensions.y, y - 65, "height");
                Row("Depth", app.FurnitureDimensions.z, y - 130, "depth");
                if (selection == null) ToolkitVisuals.Text(panel.transform, new Vector2(550, 60), new Vector2(0, y - 210), (app.DwellInput ? "Dwell to place or pick up; dwell again to drop." : app.HandsInput ? "Pinch floor to place. Pinch a box and drag." : "Trigger on floor to place. Grip to move.") + "\nBlocked placements show an overlap warning.", 21, Color.white);
            }
            else if (app.Tool == ToolkitTool.Hang)
            {
                Row("Frame width", app.FrameDimensions.x, y, "framewidth");
                Row("Frame height", app.FrameDimensions.y, y - 58, "frameheight");
                Row("Gap", app.FrameGap, y - 116, "gap");
                Row("Hanger below top", app.FrameInset, y - 174, "inset");
                Button(panel.transform, new Vector2(-240, y - 232), new Vector2(60, 48), "−", "count:-1");
                ToolkitVisuals.Text(panel.transform, new Vector2(350, 48), new Vector2(0, y - 232), app.FrameCount + " equal frames", 24, Color.white);
                Button(panel.transform, new Vector2(240, y - 232), new Vector2(60, 48), "+", "count:1");
            }
            else
            {
                ToolkitVisuals.Text(panel.transform, new Vector2(550, 120), new Vector2(0, y - 15), "Place wall nodes to measure line angles.\nDouble-press or select a node to finish.\nThe grid and line angles follow gravity.", 24, Color.white);
                Button(panel.transform, new Vector2(0, y - 115), new Vector2(260, 48), "Finish path", "finish");
                ToolkitVisuals.Text(panel.transform, new Vector2(550, 70), new Vector2(0, y - 185), "The grid follows gravity.\nThe app does not track the physical object.", 21, ToolkitVisuals.Lavender);
            }
            if (selection != null && !SpatialToolkitApp.IsPath(selection))
            {
                Button(panel.transform, new Vector2(0, app.Tool == ToolkitTool.Fit ? -105 : -112), new Vector2(260, app.Tool == ToolkitTool.Fit ? 40 : 32), "Delete", "delete");
            }
            Button(panel.transform, new Vector2(-190, -155), new Vector2(175, 50), "Undo", "undo");
            Button(panel.transform, new Vector2(0, -155), new Vector2(175, 50), app.Preferences.imperial ? "Feet / inches" : "Meters", "units");
            Button(panel.transform, new Vector2(190, -155), new Vector2(175, 50), app.Preferences.leftHanded ? "Left hand" : "Right hand", "hand");
            Button(panel.transform, new Vector2(-190, -220), new Vector2(175, 55), app.Snapping ? "Snap on" : "Snap off", "snap");
            Button(panel.transform, new Vector2(0, -220), new Vector2(175, 55), "Save room", "save");
            Button(panel.transform, new Vector2(190, -220), new Vector2(175, 55), "Load room", "load");
            Button(panel.transform, new Vector2(-140, -285), new Vector2(265, 55), "Take snapshot", "snapshot", ToolkitVisuals.Lavender);
            Button(panel.transform, new Vector2(145, -285), new Vector2(265, 55), "Controls", "controls");
            status = ToolkitVisuals.Text(panel.transform, new Vector2(560, 65), new Vector2(0, -350), app.Status, 21, ToolkitVisuals.Mint);
        }
        void BuildPathEdit(Transform panel)
        {
            ToolkitVisuals.Text(panel, new Vector2(540, 65), new Vector2(0, 120), "Node " + (app.SelectedNode + 1) + " selected\nHold and move to adjust its position.", 24, Color.white);
            Button(panel, new Vector2(-140, 15), new Vector2(260, 58), app.SelectedPathActive ? "Finish path" : "Start new path", app.SelectedPathActive ? "finish" : "startpath");
            Button(panel, new Vector2(140, 15), new Vector2(260, 58), "Delete", "delete");
        }
        void BuildPill(Transform panel, RectTransform rect)
        {
            rect.sizeDelta = new Vector2(840, 100);
            if (!pill) pill = RoundedSprite(31);
            var background = Image(panel, rect.sizeDelta, Vector2.zero, ToolkitVisuals.Panel);
            background.sprite = pill;
            background.pixelsPerUnitMultiplier = 0.64f;
            AddHitTarget(background, "expand", rect.sizeDelta, 14);
            ToolkitVisuals.Text(panel, new Vector2(180, 65), new Vector2(-315, 0), new[] { "Measure", "Fit check", "Hang", "Level" }[(int)app.Tool], 25, ToolkitVisuals.Mint);
            Button(panel, new Vector2(-125, 0), new Vector2(175, 62), "Undo", "undo");
            Button(panel, new Vector2(65, 0), new Vector2(175, 62), app.Preferences.imperial ? "Feet / inches" : "Meters", "units");
            Button(panel, new Vector2(255, 0), new Vector2(175, 62), app.Preferences.leftHanded ? "Left hand" : "Right hand", "hand");
            status = null;
        }
        void BuildSettings(Transform panel)
        {
            Button(panel, new Vector2(0, -275), new Vector2(540, 58), "Back", about ? "settings" : "main");
            if (about)
            {
                ToolkitVisuals.Text(panel, new Vector2(540, 60), new Vector2(0, 215), "ABOUT", 30, ToolkitVisuals.Mint);
                ToolkitVisuals.Text(panel, new Vector2(540, 190), new Vector2(0, 50), "Spatial Toolkit\n\nDeveloped by Diego Medina Molina\nAs part of the PICO Vibe Lab", 27, Color.white);
                ToolkitVisuals.Text(panel, new Vector2(540, 60), new Vector2(0, -110), "Version " + Application.version, 25, ToolkitVisuals.Lavender);
                return;
            }
            ToolkitVisuals.Text(panel, new Vector2(540, 50), new Vector2(0, 220), "SETTINGS", 30, ToolkitVisuals.Mint);
            ToolkitVisuals.Text(panel, new Vector2(340, 60), new Vector2(-90, 115), "Wristwatch Gesture", 25, Color.white);
            var toggle = Image(panel, new Vector2(110, 52), new Vector2(205, 115), app.Preferences.wristGestureDisabled ? ToolkitVisuals.Button : ToolkitVisuals.Mint);
            if (!pill) pill = RoundedSprite(31);
            toggle.sprite = pill; toggle.pixelsPerUnitMultiplier = 32f / 26;
            AddHitTarget(toggle, "wrist", new Vector2(110, 52));
            var knob = Image(toggle.transform, new Vector2(38, 38), new Vector2(app.Preferences.wristGestureDisabled ? -27 : 27, 0), app.Preferences.wristGestureDisabled ? Color.white : Color.black);
            knob.sprite = pill; knob.pixelsPerUnitMultiplier = 32f / 19;
            ToolkitVisuals.Text(panel, new Vector2(540, 48), new Vector2(0, 55), app.Preferences.wristGestureDisabled ? "Off — menu floats in the room" : "On — raise your wrist to show the menu", 21, ToolkitVisuals.Lavender);
            ToolkitVisuals.Text(panel, new Vector2(540, 50), new Vector2(0, -20), "Color Settings", 25, Color.white);
            Button(panel, new Vector2(-225, -85), new Vector2(70, 54), "<", "colors:-1");
            ToolkitVisuals.Text(panel, new Vector2(340, 54), new Vector2(0, -85), "Option " + (app.Preferences.colorOption + 1), 27, ToolkitVisuals.Mint);
            Button(panel, new Vector2(225, -85), new Vector2(70, 54), ">", "colors:1");
            Button(panel, new Vector2(0, -180), new Vector2(540, 58), "About", "about");
        }
        void AddHitTarget(Image image, string action, Vector2 size, float depth = 8)
        {
            image.gameObject.AddComponent<ToolkitPaletteAction>().Action = action;
            var collider = image.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(size.x, size.y, depth);
            // Background sits behind controls on the camera-facing side of the canvas.
            if (depth > 8) collider.center = new Vector3(0, 0, 12);
        }
        void IconButton(Transform panel, Vector2 position, string action, bool gear)
        {
            var image = Image(panel, new Vector2(55, 55), position, ToolkitVisuals.Button);
            AddHitTarget(image, action, new Vector2(55, 55));
            // Geometry avoids font-dependent icon glyphs.
            if (!gear) { Image(image.transform, new Vector2(25, 4), new Vector2(0, -7), Color.white); return; }
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var tooth = Image(image.transform, new Vector2(7, 13), new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 15, Color.white);
                tooth.rectTransform.localRotation = Quaternion.Euler(0, 0, -i * 45);
            }
            Image(image.transform, new Vector2(29, 29), Vector2.zero, Color.white);
            Image(image.transform, new Vector2(13, 13), Vector2.zero, ToolkitVisuals.Button);
        }
        void BuildControls(Transform panel)
        {
            string[] names = { "Auto: controllers / hands", "Hands only: hand ray + pinch", "Eye gaze + pinch (head fallback)", "Head aim + pinch", "Hands-free: gaze / head dwell" };
            for (int i = 0; i < names.Length; i++)
                Button(panel, new Vector2(0, 242 - i * 52), new Vector2(555, 46), names[i], "input:" + i,
                    (int)app.Preferences.inputMode == i ? ToolkitVisuals.Mint : ToolkitVisuals.Button);
            string help = app.DwellInput ? "Hold focus 1.3 s; button brightens. Look away to repeat.\nDwell on item to move; dwell on surface to drop." :
                app.HandsInput ? "Pinch selects. Hold a pinched item and move to drag.\nRelease to drop. Look elsewhere without losing the item." : "Trigger selects. Grip moves. A/X undo. B/Y snap.";
            ToolkitVisuals.Text(panel, new Vector2(550, 65), new Vector2(0, -15), help, 21, Color.white);
            Button(panel, new Vector2(-190, -85), new Vector2(175, 48), "Reach -", "reach:-1");
            ToolkitVisuals.Text(panel, new Vector2(175, 48), new Vector2(0, -85), app.Reach.ToString("0.0") + " m", 24, Color.white);
            Button(panel, new Vector2(190, -85), new Vector2(175, 48), "Reach +", "reach:1");
            Button(panel, new Vector2(-140, -145), new Vector2(265, 48), "Rotate -15°", "rotate:-1");
            Button(panel, new Vector2(140, -145), new Vector2(265, 48), "Rotate +15°", "rotate:1");
            Button(panel, new Vector2(-140, -205), new Vector2(265, 48), "Delete selected", "delete");
            Button(panel, new Vector2(140, -205), new Vector2(265, 48), pinned ? "Unpin menu" : "Pin menu", "pin");
            Button(panel, new Vector2(0, -280), new Vector2(555, 55), "Back to tools", "controls", ToolkitVisuals.Lavender);
        }
        void Row(string label, float value, float y, string action)
        {
            Button(canvas.transform, new Vector2(-245, y), new Vector2(55, 48), "−", action + ":-1");
            Button(canvas.transform, new Vector2(0, y), new Vector2(420, 48), label + "   " + ToolkitGeometry.Distance(value, app.Preferences.imperial), "edit:" + action);
            Button(canvas.transform, new Vector2(245, y), new Vector2(55, 48), "+", action + ":1");
        }
        public void BeginNumeric(string property, float meters)
        {
            editing = property;
            replaceNumber = true;
            number = (app.Preferences.imperial ? meters / 0.0254f : meters * 100).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            Rebuild();
        }
        void BuildKeypad(Transform panel)
        {
            ToolkitVisuals.Text(panel, new Vector2(540, 50), new Vector2(0, 220), editing.ToUpperInvariant() + (app.Preferences.imperial ? "  •  INCHES" : "  •  CENTIMETERS"), 25, ToolkitVisuals.Lavender);
            ToolkitVisuals.Text(panel, new Vector2(540, 70), new Vector2(0, 150), string.IsNullOrEmpty(number) ? "0" : number, 42, Color.white);
            for (int i = 1; i <= 9; i++)
                Button(panel, new Vector2(-180 + ((i - 1) % 3) * 180, 60 - ((i - 1) / 3) * 70), new Vector2(160, 60), i.ToString(), "num:" + i);
            Button(panel, new Vector2(-180, -150), new Vector2(160, 60), ".", "num:.");
            Button(panel, new Vector2(0, -150), new Vector2(160, 60), "0", "num:0");
            Button(panel, new Vector2(180, -150), new Vector2(160, 60), "Delete", "num:delete");
            Button(panel, new Vector2(-140, -240), new Vector2(260, 60), "Cancel", "num:cancel");
            Button(panel, new Vector2(140, -240), new Vector2(260, 60), "Apply", "num:apply", ToolkitVisuals.Mint);
        }
        public void NumericKey(string key)
        {
            if (editing == null) return;
            if (key == "cancel") editing = null;
            else if (key == "delete") { number = number.Length > 0 ? number.Substring(0, number.Length - 1) : ""; replaceNumber = false; }
            else if (key == "apply")
            {
                if (!float.TryParse(number, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value))
                { app.Message("Enter a number first."); return; }
                if (!app.SetDimension(editing, app.Preferences.imperial ? value * 0.0254f : value / 100)) return;
                editing = null;
            }
            else if (key == ".") { if (replaceNumber) { number = "0"; replaceNumber = false; } if (!number.Contains(".")) number += "."; }
            else { if (replaceNumber) { number = ""; replaceNumber = false; } if (number.Length < 9) number = number == "0" ? key : number + key; }
            Rebuild();
        }
        Image Image(Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var go = new GameObject("Glass", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.sprite = rounded; image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = color; image.raycastTarget = false; return image;
        }
        void Button(Transform parent, Vector2 pos, Vector2 size, string label, string action, Color? tint = null)
        {
            var image = Image(parent, size, pos, tint ?? ToolkitVisuals.Button);
            AddHitTarget(image, action, size);
            var labelText = ToolkitVisuals.Text(image.transform, size - new Vector2(8, 0), Vector2.zero, label, 23,
                tint.HasValue && tint.Value.grayscale > 0.45f ? new Color(0.04f, 0.08f, 0.12f) : Color.white);
            if (action == "undo")
            {
                undoLabel = labelText;
                undoProgress = Image(image.transform, new Vector2(size.x - 12, 5), new Vector2(0, -size.y / 2 + 7), ToolkitVisuals.Mint);
                undoProgress.type = UnityEngine.UI.Image.Type.Filled; undoProgress.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                undoProgress.fillOrigin = 0; undoProgress.gameObject.SetActive(false);
            }
        }
        public void UpdatePose(ToolInput input, Transform head)
        {
            if (!canvas) return;
            if (inputSource != input.Source) { inputSource = input.Source; Rebuild(); }
            if (input.PaletteTracked) { wristLastSeen = Time.unscaledTime; lastWristUp = input.PaletteUp; }
            bool wristGrace = AttachedToWrist && !input.PaletteTracked && Time.unscaledTime - wristLastSeen < 0.7f && !input.Dwell;
            if (input.PinMenu && input.PaletteTracked && !app.Preferences.wristGestureDisabled) pinned = !pinned;
            bool wrist = !pinned && !app.Preferences.wristGestureDisabled && !input.Dwell && (input.PaletteTracked || wristGrace);
            AttachedToWrist = wrist;
            bool reveal = !wrist || controls || settings || about || Vector3.Dot(wristGrace ? lastWristUp : input.PaletteUp, Vector3.up) > 0.35f;
            canvas.gameObject.SetActive(reveal);
            canvas.transform.localScale = Vector3.one * (wrist ? 0.00048f : 0.0006f);
            if (wristGrace) { if (status) status.text = app.Status; return; }
            if (!pinned && head && !menuPending && !menuDragging)
            {
                if (!wrist && fallbackPlaced && Vector3.Distance(transform.position, head.position) < 1.8f) { if (status) status.text = app.Status; return; }
                Vector3 target = wrist ? input.PalettePosition + Vector3.up * 0.25f + (input.PalettePosition - head.position).normalized * 0.10f :
                    head.position + head.forward * 0.8f + head.right * (input.MenuOnly ? 0 : -0.32f);
                transform.position = wrist ? Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime)) : target;
                Vector3 facing = transform.position - head.position;
                if (facing.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                fallbackPlaced = !wrist;
            }
            if (status) status.text = app.Status;
        }
        Sprite RoundedSprite(int radius = 12)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float dx = Mathf.Max(radius - x, x - (63 - radius), 0), dy = Mathf.Max(radius - y, y - (63 - radius), 0);
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }
        void OnDestroy() { if (rounded) { Destroy(rounded.texture); Destroy(rounded); } if (pill) { Destroy(pill.texture); Destroy(pill); } }
    }
}
