using UnityEngine;
using UnityEngine.XR;

namespace SpatialToolkit
{
    // Editor-only test geometry is never created on a headset build.
    public sealed class ToolkitDesktopRoom : MonoBehaviour
    {
#if UNITY_EDITOR
        void Start()
        {
            if (XRSettings.isDeviceActive) return;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.GetComponent<Camera>() != Camera.main) listener.enabled = false;
            var room = new GameObject("EDITOR ONLY • simulated room", typeof(ToolkitMaterialCleanup));
            room.transform.SetParent(transform, false);
            Surface(room.transform, "Floor", new Vector3(0, -0.06f, 0), new Vector3(5, 0.1f, 6), new Color(0.18f, 0.22f, 0.25f));
            Surface(room.transform, "Wall", new Vector3(0, 1.5f, 2.55f), new Vector3(5, 3, 0.1f), new Color(0.25f, 0.31f, 0.34f));
            Surface(room.transform, "Side wall", new Vector3(-2.55f, 1.5f, 0), new Vector3(0.1f, 3, 5), new Color(0.22f, 0.27f, 0.3f));
            for (int i = -5; i <= 5; i++)
            {
                float x = i * 0.5f;
                ToolkitVisuals.Line(room.transform, new[] { new Vector3(x, 0, -2.5f), new Vector3(x, 0, 2.5f) }, new Color(0.45f, 0.6f, 0.65f, 0.25f), 0.001f);
                ToolkitVisuals.Line(room.transform, new[] { new Vector3(-2.5f, 0, x), new Vector3(2.5f, 0, x) }, new Color(0.45f, 0.6f, 0.65f, 0.25f), 0.001f);
            }
            ToolkitVisuals.Label(room.transform, new Vector3(0.7f, 2.3f, 2.48f), "EDITOR PREVIEW • SIMULATED ROOM\nClick: place  /  Right drag: move\nZ: undo  /  B: snap", ToolkitVisuals.Mint, 26);
        }
        void Surface(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            { name = "Spatial Toolkit runtime test surface", color = color, enableInstancing = true };
        }
#endif
    }
}
