using UnityEngine;

namespace SpatialToolkit
{
    public sealed partial class ToolkitPalette
    {
        public bool AttachedToWrist { get; private set; }
        public bool Dragging => menuDragging;
        public Vector3 DragPoint => transform.TransformPoint(menuGrabLocal);
        bool menuPending, menuDragging;
        string pendingMenuAction;
        float menuPressedAt, menuDepth;
        Vector3 menuStartPosition, menuHandStart, menuGrabLocal, menuGrabWorldStart;
        Quaternion menuStartRotation;
        bool wasPinned;
        float wristLastSeen = -10;
        Vector3 lastWristUp;
        public void FacePlayer(Transform head)
        {
            if (head && !menuDragging)
                transform.rotation = ToolkitVisuals.UprightRotation(transform.position - head.position, transform.rotation);
        }

        public void CancelMenuDrag()
        {
            if (menuDragging)
            { transform.SetPositionAndRotation(menuStartPosition, ToolkitVisuals.UprightRotation(menuStartRotation * Vector3.forward, menuStartRotation)); pinned = wasPinned; }
            menuPending = menuDragging = false;
        }

        public bool HandleMenuDrag(ToolInput input, ToolkitPaletteAction hit, Vector3 point, Transform head)
        {
            if (input.SourceChanged || !input.Tracked || AttachedToWrist)
            { bool consumed = menuPending || menuDragging; CancelMenuDrag(); return consumed; }
            bool zone = hit && (hit.Action == "dragmenu" || hit.Action == "expand");
            if (!menuPending && !menuDragging)
            {
                if (!zone || input.Dwell) return false;
                if (!(input.Gesture ? input.TriggerDown : input.GripDown)) return false;
                menuPending = true; pendingMenuAction = hit.Action; menuPressedAt = Time.unscaledTime;
                menuStartPosition = transform.position; menuStartRotation = transform.rotation; wasPinned = pinned;
                menuHandStart = input.Position; menuDepth = Mathf.Max(0.01f, Vector3.Dot(point - input.Ray.origin, input.Ray.direction));
                menuGrabLocal = transform.InverseTransformPoint(point);
                menuGrabWorldStart = point;
                menuDragging = !input.Gesture;
            }
            bool held = input.Gesture ? input.TriggerHeld : input.GripHeld;
            if (!held)
            {
                bool expand = !menuDragging && pendingMenuAction == "expand";
                if (menuDragging) { pinned = true; fallbackPlaced = true; }
                menuPending = menuDragging = false;
                if (expand) app.Dispatch("expand");
                return true;
            }
            if (!menuDragging && Time.unscaledTime - menuPressedAt >= 0.22f &&
                (Vector3.Distance(input.Position, menuHandStart) >= 0.006f || Time.unscaledTime - menuPressedAt >= 0.4f))
                menuDragging = true;
            if (menuDragging)
            {
                pinned = true;
                // Rotate about the original grab point, then translate that point onto
                // the cursor. This preserves the exact dot-to-panel attachment, including
                // off-center grabs and changes in facing while moving the menu.
                Vector3 cursor = input.Ray.GetPoint(menuDepth);
                Quaternion rotation = ToolkitVisuals.UprightRotation(menuStartRotation * Vector3.forward, menuStartRotation);
                Vector3 center = menuStartPosition + (cursor - menuGrabWorldStart);
                for (int i = 0; i < 8 && head; i++)
                {
                    rotation = ToolkitVisuals.UprightRotation(center - head.position, rotation);
                    center = cursor - rotation * Vector3.Scale(transform.lossyScale, menuGrabLocal);
                }
                transform.rotation = rotation;
                transform.position = cursor - transform.TransformVector(menuGrabLocal);

            }
            return true;
        }
    }
}
