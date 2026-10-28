using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpatialToolkit
{
    public enum ToolkitTool { Measure, Fit, Hang, Level }
    public enum ToolkitInputMode { Automatic, HandRay, EyePinch, HeadPinch, Dwell }
    public enum ToolkitItemKind { Measurement, Furniture, Frames, Level, AnglePath }
    [Serializable]
    public sealed class ToolkitItemData
    {
        public string id = Guid.NewGuid().ToString("N");
        public ToolkitItemKind kind;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 dimensions;
        public List<Vector3> points = new List<Vector3>();
        public bool closed;
        public int count = 3;
        public float gap = 0.08f, inset = 0.06f;
    }
    [Serializable]
    public sealed class ToolkitSession
    {
        public int version = 1;
        public string anchorUuid;
        public bool simulated;
        public Vector3 previewOrigin;
        public Quaternion previewRotation = Quaternion.identity;
        public List<ToolkitItemData> items = new List<ToolkitItemData>();
    }
    [Serializable]
    public sealed class ToolkitPreferences
    {
        public bool imperial, leftHanded;
        public ToolkitInputMode inputMode;
        // Stored as disabled so older preference files retain the wrist gesture.
        public bool wristGestureDisabled;
        public int colorOption;
        public Vector3 furniture = new Vector3(0.8f, 0.9f, 0.6f);
        public Vector3 frame = new Vector3(0.4f, 0.5f, 0);
        public float gap = 0.08f, inset = 0.06f;
        public int frameCount = 3;
    }
}
