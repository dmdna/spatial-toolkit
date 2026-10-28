using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpatialToolkit
{
    public sealed partial class SpatialToolkitApp
    {
        float loadHeldSince = -1;
        bool loadHoldFired;
        ToolkitPaletteAction loadHoldButton;
        void CancelLoadHold()
        {
            loadHeldSince = -1; loadHoldFired = false; loadHoldButton = null;
            if (palette) palette.LoadHold(0, false);
        }
        void UpdateLoadHold(ToolkitPaletteAction action)
        {
            if (loadHeldSince < 0) return;
            bool overButton = action && action == loadHoldButton && action.Action == "load";
            if (!state.TriggerHeld || !overButton || state.Dwell || !hasFocus || busy)
            {
                bool tap = !state.TriggerHeld && overButton && !loadHoldFired && !state.Dwell && hasFocus && !busy;
                CancelLoadHold();
                if (tap) Dispatch("load");
                return;
            }
            if (loadHoldFired) return;
            float held = Time.unscaledTime - loadHeldSince;
            float progress = Mathf.Clamp01((held - 0.45f) / 1.2f);
            palette.LoadHold(progress, held >= 0.45f);
            if (held >= 0.45f) Message("Keep holding to Delete Saved Room.", 1);
            if (progress >= 1)
            {
                loadHoldFired = true; palette.LoadHold(0, false); DeleteSavedRoom();
            }
        }
        async void DeleteSavedRoom()
        {
            if (busy || captureBusy) return;
            busy = true;
            Message("Deleting saved room…", 30);
            try
            {
                var paths = new[] { SessionPath, SessionPath + ".bak", SessionPath + ".tmp" };
                var uuids = new HashSet<string>(); bool found = false;
                foreach (var path in paths) if (File.Exists(path))
                {
                    found = true;
                    try
                    {
                        var saved = JsonUtility.FromJson<ToolkitSession>(File.ReadAllText(path));
                        if (saved != null && !string.IsNullOrEmpty(saved.anchorUuid)) uuids.Add(saved.anchorUuid);
                    }
                    catch (Exception) { /* A corrupt local save can still be deleted. */ }
                }
                foreach (var uuid in uuids) if (!await platform.DeleteSavedAnchor(uuid)) return;
                foreach (var path in paths) if (File.Exists(path)) File.Delete(path);
                if (Session.anchorUuid != null && uuids.Contains(Session.anchorUuid)) Session.anchorUuid = null;
                loadConfirmationUntil = 0; RenderAll(); palette.Rebuild();
                Message(found ? "Saved room deleted. Current measurements remain in the room." : "No saved room to delete.");
            }
            catch (Exception e) { Message("Saved room could not be deleted. Try again."); Debug.LogException(e); }
            finally { busy = false; }
        }
    }
}
