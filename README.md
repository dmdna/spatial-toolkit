# Spatial Toolkit

## Overview

Spatial Toolkit is a mixed reality workbench for measuring and planning changes to a physical room. It overlays measurement paths, furniture-sized boxes, picture layouts, and level guides on PICO video passthrough, using the scanned room mesh for surface placement and collision checks.

The app targets **PICO OS 6 / Project Swan** through Unity's native PICO XR runtime on Android. The current build is **0.4.2 (Android version code 8)**. It is developed with **Unity 6000.3.11f1**. The project uses `picoxr` mode; Spatial Adapter is also installed, but this app runs in PICO XR mode.

Created by **Diego Medina Molina and the PICO Vibe Lab**.

## Features

- **Smart Measure:** Place connected measurement nodes on scanned surfaces, read segment lengths, and close planar outlines to calculate area. Supports concave outlines, node editing, and metric or imperial units. Invalid or nonplanar areas are rejected.
- **Fit Check:** Enter exact furniture width, height, and depth, then place, rotate, drag, or resize a translucent box in the room. The selected main hand owns the placement preview, even while the off-hand holds and drags a dimension beyond the menu. Its thumbstick rotates the preview smoothly before placement; the placed box keeps that exact orientation. Collision checks reject overlapping placements and resizes; blocked drag previews turn red.
- **Picture Hanging:** Preview and place groups of one to eight equal-sized frames with exact dimensions, spacing, and hanger inset. Frame outlines and individual nail crosshairs update as values change.
- **Level & Plumb:** Preview a gravity-aligned grid on a wall before placing it and connected paths with horizontal and vertical angle readouts. Controller orientation or approximate finger alignment supplies the live level reference; the app does not automatically recognize physical object edges.
- **Flexible input:** Use controllers, native hand rays, eye gaze with pinch, head aim with pinch, or hands-free dwell. The selected hand places new measurements; both hands can operate menus and select or drag placed items. The off-hand pointer appears over interactable targets, including in wrist mode. Two hands rotate a Fit box smoothly without scaling it; single-hand wrist twists rotate in 15-degree steps. Eye input depends on supported hardware, calibration, and permission. Gaze dots use 0.6 alpha to keep covered text readable.
- **Wrist and floating menus:** Reveal the wrist palette with a wristwatch gesture, pin it in space, or minimize it to a compact bar. Floating panels can be dragged from their header or compact background and stay upright facing the user.
- **Selection and editing:** Select placed items to edit their values, drag nodes and objects, delete individual items, or start new paths from existing nodes. Tap and release a dimension row for the keypad, or hold for 0.25 seconds and drag horizontally for millimeter adjustments with live previews. One complete drag is one undo step. Fit rotation buttons appear above Delete in the selected box's editing panel, with equal spacing between editing rows.
- **Undo and workspace controls:** Undo up to 50 actions. Hold Undo to clear the live workspace, or hold Load room to delete the saved room and its anchors. Clearing the workspace is undoable and preserves the saved room.
- **Room persistence:** Save room geometry locally with a PICO spatial anchor and restore it after relocalization. Tracking loss hides anchored overlays and blocks placement; a failed load preserves the current workspace. Saved room geometry uses no cloud service or account.
- **Visual preferences:** Choose from three presets, including a high-contrast option, and retain input, handedness, units, and wrist-gesture preferences between sessions.
- **Hide menu:** Hide the menu and pointers before using the headset's capture feature. Release and select again to restore the menu without placing an item; dwell users get automatic restoration after eight seconds. In-app snapshots were removed because this build has no PICO Platform app ID and no verified supported alternative for triggering the native passthrough capture. No snapshot timer is exposed.
- **Editor preview:** Try the tools in an explicitly simulated room in Unity Play Mode. This room is excluded from headset runtime.

Measurements depend on room scanning and tracking quality. Physical measurement accuracy, hand ergonomics, stereo presentation, and anchor relocalization require acceptance testing on the target headset. Fit Check uses constrained placement rather than a free-falling furniture simulation. Picture Hanging uses the hanger inset you provide; it does not infer wire sag or fastener suitability.

### Running and building

1. Open the repository with Unity **6000.3.11f1**, with Android Build Support installed, and wait for compilation.
2. Open `Assets/Scenes/SpatialToolkitWorkbench.unity`.
3. Press Play for the simulated desktop room, or select **PICO > Project > Build Development APK** for Android. Keep the Android build target and configure signing credentials in Unity when required.
4. Find the APK at `Builds/Android/spatial-toolkit.apk`. The build helper records its result in `Logs/AndroidBuildResult.json`.

Desktop controls: left click places or selects, right drag moves an item, **Z** undoes, **B** toggles snapping, and the mouse wheel changes pointer reach. Click and release a dimension row to enter a value, or hold and drag left/right to adjust it.

Controller controls: the selected hand's trigger places new measurements; either trigger selects UI or placed items, and grip drags. **A/X** undoes, **B/Y** toggles snapping, thumbstick up/down changes pointer reach, and left/right turns the live Fit preview or selected Fit box at a constant **30 degrees per second** without snapping. Before placement, only the selected main hand's stick rotates the preview. Pinch selects with either hand; hold a pinch on an item to drag, then release to drop. Twist a single pinched wrist for 15-degree Fit rotation, or grab the same box with both hands for continuous rotation without scaling. Releasing either hand continues the grab with the remaining hand. Reach controls are in Settings. The Left/Right selection also resets the menu to the other wrist.

See [SETUP.txt](SETUP.txt) for MR configuration, emulator and device installation, controller-free operation, and command-line helpers. See [APP-NOTES.txt](APP-NOTES.txt) for implementation details and historical verification records; the changelog below describes subsequent changes to earlier behavior.

## Changelog

All eight numbered builds recorded to date are listed newest first. Earlier changes and verification are reconstructed from the repository's implementation notes and build records. Verification counts describe checks recorded for each build.

### 0.4.2 — Thumbstick rotation before Fit placement

Android version code **8**.

- Added constant 30-degree-per-second thumbstick rotation to the live Fit preview, without snapping. The selected main hand controls the preview angle, including while the off-hand holds a dimension drag beyond the menu. Selected-box and grabbed-box rotation remain available.
- Placement now preserves the preview's exact world orientation, including when the saved room anchor is rotated. Preview overlap warnings, blocked-placement status, and final placement checks all use that rotation instead of an axis-aligned box. Rotating a preview creates no placed-item undo record.
- Updated the documented controller controls. Verification: **359 automated checks passed**: the existing 337 checks plus 22 preview-rotation checks covering both main hands, direction and speed, snapping independence, off-hand dimension dragging, exact placement orientation, rotated wall collision, and rotated room anchors. Editor and Android C# compilation passed. No emulator run was performed for this patch.
- Android APK built successfully on its first attempt and installed on the connected Swan headset. Verified installed version **0.4.2 / code 8**, a successful cold launch, and the foreground resumed activity. The sampled startup log (`Logs/Toolkit042Startup.log`) contains no Unity/AndroidRuntime error entries; previously observed native extension/scheduler diagnostics remain. Physical controller acceptance of preview rotation still requires headset testing.

### 0.4.1 — Main-hand preview ownership fix

Android version code **7**.

- Fixed unplaced previews jumping to the off-hand's surface when a held dimension drag leaves the menu. Preview surface sampling now uses the selected main hand independently of the captured editing hand. Off-hand drags remain active beyond the entire panel until release; dimensions continue updating the preview live, and moving the main hand still moves that preview.
- Applied main-hand preview ownership across the tools and preserved the interaction ray and surface target used for editing placed items. Release returns to normal interaction without an accidental placement.
- Restricted new placement and unplaced previews to the selected main hand, including when the input reader temporarily falls back to the other tracked hand. Existing-item and menu interaction with either hand and hands-free dwell remain available.
- Verification: **337 automated checks passed**, including 100 new preview-ownership checks across left/right main hands, controller/pinch input, and unplaced Fit/Hang tools; the 237 existing geometry, input, editing, selection, room, and full placement/save/restore workflow checks also passed. Editor and Android C# compilation passed. No emulator run was performed for this patch.
- Android APK built successfully on its first attempt, then installed on the connected Swan headset. Verified installed version **0.4.1 / code 7**, a successful cold launch, and the app's foreground resumed activity. The sampled startup log (`Logs/Toolkit041Startup.log`) has no Unity/AndroidRuntime error entries; vendor controller-resource JSON warnings and native extension/scheduler diagnostics are present. Physical acceptance of off-hand dragging beyond the menu still requires headset testing.

### 0.4.0 — Two-hand interaction and precision editing

Android version code **6**.

- Added independent input sampling for both hands/controllers. Left/Right still selects the placement hand and opposite menu wrist. Either hand can select UI, nodes, and placed objects; the off-hand ray and dot appear over interactable targets, including in wrist mode. Eye/head pinch accepts either hand. One hand owns each interaction through release, and a second hand can join a Fit box grab.
- Added continuous, rigid two-hand Fit rotation without scaling. Both simultaneous pinches and later second-hand grabs work. Releasing one hand transfers the grab to the remaining hand without a position jump. Fit boxes stay upright, maintain floor contact, and retain collision checks.
- Added constant 30-degree-per-second thumbstick left/right rotation without snapping, including during controller grabs. A sustained selected-box rotation is one undo step.
- Replaced hand-forward yaw with wrist twist around the captured pinch ray, with 15-degree steps and a small hysteresis band to reduce jitter. Single-hand pinch rotation snaps regardless of placement snapping.
- Moved Fit rotation buttons above Delete in the selected box's panel. Width, Height, Depth, Rotate, and Delete use equal gaps, with a larger separation before the persistent controls. Moved Reach from Controls to Settings.
- Added tap-and-release keypad entry and hold-and-drag dimension editing. Hold 0.25 seconds, then drag horizontally for millimeter adjustments. Selected objects and placement previews update live; a whole drag is one undo step. Focus or tracking loss cancels unfinished edits, and overlap checks still reject invalid Fit sizes.
- Added a gravity-aligned wall-grid preview for Level, retaining its wall pose while the menu is targeted. Fit previews also retain their floor pose while dimensions are edited in the menu.
- Set gaze/dwell dot alpha to 0.6. Hand switching now clears floating/pinned and cached wrist state and attaches the menu to the new wrist immediately.
- Replaced the nonworking snapshot action with **Hide menu**, removing its Platform capture code and avoiding a nonfunctional Snapshot Timer. The installed Highlights API requires Platform initialization, and `Assets/Resources/PXR_PlatformSetting.asset` has no app ID. No supported equivalent of the native passthrough capture was verified for this consumer build; Enterprise capture and developer/ADB capture are not shipped as app workarounds. Use the headset capture feature while the menu is hidden. Release and select to restore without placing; dwell restores after eight seconds.
- Verification: **237 automated checks passed**: 18 geometry, 28 input replay, 33 menu/path, 42 selection/menu drag, 14 hand/menu, 43 room/selection feedback, 45 new dual-hand/dimension checks, and 14 full interactive workflow checks. Updated the legacy yaw replay to test the newly requested wrist twist. Editor and Android C# compilation passed. Rendered and inspected the selected Fit menu's control layout.
- Android APK built successfully on its first attempt. Installed on the connected Swan headset and verified installed version **0.4.0 / code 6**. Cold launch returned `Status: ok`, and the app was the foreground resumed activity. No Unity/AndroidRuntime errors or exceptions were found in the sampled process startup log (`Logs/Toolkit040Startup.log`). Native runtime also emitted unsupported FB triangle-mesh entry-point probes and scheduler diagnostics, which are present in the earlier `Logs/PicoDeviceRuntime.log` too. Physical headset acceptance of gesture feel, two-hand ergonomics, placement accuracy, and native screenshots with the menu hidden remains outstanding. No emulator run was performed for this update.

### 0.3.2 — Room controls and selection feedback

Android version code **5**.

- Kept wrist, pinned, fallback, and dragged menus upright with world-up orientation while facing the player. Dragging preserves the exact cursor anchor as the panel turns.
- Changed eye/head aiming and dwell to show a dot only; hands and controllers retain both the ray and dot.
- Unified selection accents across Measure and Level nodes, Fit boxes, and Hang layouts. Added hover outlines and node halos across visual presets.
- Enlarged node hit targets to 1.5 times their visible radius without changing visible size or snapping. Nearby nodes take priority over a level grid's background collider.
- Restored the preferred menu hand immediately when tracking returns, with release required before held input can activate again after a source change.
- Changed a physical Load room press to load on release. Holding now offers **Delete Room (Hold)** with progress and cancellation on aim, focus, or tracking loss.
- Added saved-room deletion, including its backup and temporary files and persisted PICO anchors, while retaining current live measurements.
- Fixed workspace coordinates so empty geometry does not rebase the room. Saving an active path now rebuilds its visuals.
- Fixed Clear All to detach the live anchor, restore visibility, clear transient path/selection state, and remove orphaned visuals even in an already-empty workspace. The saved room remains available.
- Added the Room and Selection Feedback regression suite. Recorded verification: **192 automated checks passed**, Editor and Android C# compilation passed, and camera inspection confirmed upright menus, pointer visibility, and selection feedback.
- Built, installed, and cold-launched on a Swan headset; verified the installed version and foreground activity with no Unity or AndroidRuntime errors in the sampled startup logs. Gesture acceptance remains a physical headset check.

### 0.3.1 — Hand and menu hotfix

Android version code **4**.

- Restored the pre-0.3 native PICO hand aim poses, Auto mode's optional eye/head fallback policy, and captured ray direction during pinched dragging, replacing the wrist-to-knuckle aiming introduced in 0.3.0.
- Retained hand rotation for Fit box yaw and hand translation for moving the captured ray.
- Started the visible hand ray between the thumb and index fingertips when valid poses are available, ending at the actual system-pointer hit dot without changing hit testing or snapping to button centers.
- Fixed full and minimized menu dragging to preserve the exact local grab point as the panel moves and turns toward the user. The pointer dot remains at that point.
- Narrowed the header drag handle from 490 to 360 UI units to leave more clearance at the corners.
- Added the Hand and Menu Hotfix regression suite. Recorded verification: **149 automated checks passed**, including the complete placement, collision, dragging, keypad, and save/load workflow.
- Built, installed, and launched on a Swan headset; confirmed the version and resumed activity with no Unity or AndroidRuntime errors in the sampled startup logs. Hand-control feel remained a user acceptance check.

### 0.3.0 — Selection editing and interaction

Android version code **3**. Built on **2026-10-28**.

- Added short-pinch selection and an **Edit Selection** panel for placed items. Fit and Hang edits load the selected item's actual values and update only that item in real time.
- Added selected-node actions to finish a path, start a new path while preserving the original, and delete. Fit resizing preserves floor contact and rejects overlaps; edits and deletes remain undoable.
- Improved active measurement completion: selecting the current or previous node finishes the path; returning to an earlier node closes a valid area. Closing a suffix retains earlier segments as a separate open measurement. Invalid closures stay active with a reason.
- Allowed a node from another path to provide a closing vertex when it produces a valid area.
- Added pinch-hold dragging after 0.22 seconds with movement, or 0.4 seconds held. Controller grip continues to drag directly.
- Linked Level paths to their gravity grids, with compatibility for older saves. Deleting an intermediate node reconnects the line; deleting the gridded first node removes its path and grid; moving it also moves the grid.
- Added dragging for floating menus through the top margin and minimized background. Short pinches still expand the compact menu; wrist menus remain attached to the wrist.
- Added a 0.7-second pose hold during brief menu-wrist occlusion.
- Introduced wrist-to-middle-knuckle hand aiming with fallback poses and smoothing in Auto and Hands Only modes. **This aiming change was reverted in 0.3.1.** Hand presence became independent of SDK aim-ray validity.
- Rendered the interaction ray and dot above opaque menus while retaining ordinary depth testing for measurement geometry.
- Added the Selection and Menu Drag regression suite. Recorded verification: **135 automated checks passed**, with camera captures confirming pointer visibility over the high-contrast editing menus.
- Built, installed, and launched on a Swan headset; verified the installed version and foreground activity with no Unity or AndroidRuntime errors in the sampled startup logs.

### 0.2.0 — Menu, appearance, and path tools

Android version code **2**. Built on **2026-10-28**.

- Added Settings and About pages, creator credits, and the running application version.
- Added a persistent wristwatch-gesture toggle and three visual presets: the original palette, blue/amber/orange, and a high-contrast black/white/gray/yellow option. Presets update existing and future artifacts and retain text cues for meaningful states.
- Made the wrist panel 20% smaller and moved it 10 cm farther outward and 3 cm higher. Disabling the gesture enables the world-pinned fallback behavior even with two tracked hands.
- Added a compact menu with the current tool, Undo, units, and handedness, keeping shared actions in consistent positions between layouts.
- Made the full panel block placement rays. UI pointer endpoints now follow the actual ray intersection instead of turning toward control centers.
- Added hold-to-clear on Undo: a 0.45-second grace period followed by a 1.2-second progress fill. Release, leaving the button, tracking/focus loss, or navigation cancels it. Clearing all artifacts remains undoable.
- Added double-press completion within 0.32 seconds for Measure and Level paths, plus completion through an existing node. Close Area remains an explicit action.
- Added editable, saveable Level angle paths with horizontal and vertical angles calculated from their endpoints, alongside the gravity grid.
- Made Hang previews use the same frame and nail drawing as placed layouts, updating live with numeric edits.
- Added automatic build versioning for menu, Build Profile, and batch builds: an explicitly advanced feature version is retained once, subsequent attempts increment the patch, and every attempt increments the Android version code. Version state is stored in `ProjectSettings/ToolkitBuildVersion.json`.
- Added the Menu and Path Feature regression suite. Recorded verification: **18 geometry, 27 input replay, 33 menu/path, and 14 interactive workflow checks passed**; Editor and Android runtime compilation passed and menu layouts were reviewed through camera captures.
- Built the Android APK successfully. This build was recorded as not installed or tested on a physical headset.

### 0.1.0 — Initial mixed reality workbench

Initial numbered build; subsequent build records begin at 0.2.0 / Android code 2.

- Introduced the four core tools: chained distance and planar-area measurement, collision-aware furniture fit boxes, multi-frame picture layouts with nail guides, and a gravity-aligned level/plumb grid.
- Added exact numeric dimension entry, metric and imperial units, 1 cm or half-inch snapping, 15-degree Fit yaw snapping, and eighth-inch rounding for imperial length readouts.
- Added validation for invalid area outlines, including crossing, duplicate, collinear, and nonplanar geometry, with a 2 cm coplanarity tolerance. Placement requires a mapped surface.
- Added the wrist palette, controller placement and dragging, handedness switching, pointer reach adjustment, throw-behind deletion, and a 50-action undo history. Workspaces support up to 100 artifacts and measurement paths up to 128 nodes.
- Established passthrough rendering with one native PICO XR Origin, controller models, an invisible collidable room mesh, spatial-data permission, and spatial-anchor support. Used MultiPass stereo rendering with HDR disabled.
- Added local room save/load using persisted PICO anchors and room-local geometry, ongoing localization updates, tracking-loss handling, input validation, staged writes, and a backup save. Editor saves are marked simulated and cannot load on device.
- Added Android snapshots through the PICO Platform Highlight API and Editor screenshots, hiding the menu and pointer during capture.
- Added translucent panels, colored tool accents, readable world labels, stereo-aware overlay shaders, and an Editor-only simulated room with desktop controls.
- Added repeatable MR setup, development APK builds, emulator/device helper commands, and geometry and interactive workflow checks. Recorded baseline verification: **18 geometry checks and 14 interactive checks passed**, with Editor and Android runtime compilation passing.
- The initial development APK was recorded as built, installed, and brought to the foreground in the emulator on **2026-10-21**. Physical headset measurement, tracking, anchor, and snapshot acceptance remained outstanding.

### Keeping this README current

For every new numbered build or patch, prepend its entry immediately below the Changelog introduction. Record the exact app version and Android version code, meaningful additions/changes/fixes, build outcome, and verification actually performed. Build retries consume version numbers too; document failed attempts honestly when they produce a new numbered build. Update the current version, overview, features, controls, and setup details whenever the app changes. Preserve older entries and newest-first ordering.
