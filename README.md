# Spatial Toolkit

## Overview

Spatial Toolkit is a mixed reality workbench for measuring and planning changes to a physical room. It overlays measurement paths, furniture-sized boxes, picture layouts, and level guides on PICO video passthrough, using the scanned room mesh for surface placement and collision checks.

The app targets **PICO OS 6 / Project Swan** through Unity's native PICO XR runtime on Android. The current build is **0.3.2 (Android version code 5)**. It is developed with **Unity 6000.3.11f1**. The project uses `picoxr` mode; Spatial Adapter is also installed, but this app runs in PICO XR mode.

Created by **Diego Medina Molina and the PICO Vibe Lab**.

## Features

- **Smart Measure:** Place connected measurement nodes on scanned surfaces, read segment lengths, and close planar outlines to calculate area. Supports concave outlines, node editing, and metric or imperial units. Invalid or nonplanar areas are rejected.
- **Fit Check:** Enter exact furniture width, height, and depth, then place, rotate, drag, or resize a translucent box in the room. Collision checks reject overlapping placements and resizes; blocked drag previews turn red.
- **Picture Hanging:** Preview and place groups of one to eight equal-sized frames with exact dimensions, spacing, and hanger inset. Frame outlines and individual nail crosshairs update as values change.
- **Level & Plumb:** Place a gravity-aligned wall grid and connected paths with horizontal and vertical angle readouts. Controller orientation or approximate finger alignment supplies the live level reference; the app does not automatically recognize physical object edges.
- **Flexible input:** Use controllers, native hand rays, eye gaze with pinch, head aim with pinch, or hands-free dwell. Supports either hand, single-hand use, adjustable pointer reach, and a recovery menu when hands are unavailable. Eye input depends on supported hardware, calibration, and permission.
- **Wrist and floating menus:** Reveal the wrist palette with a wristwatch gesture, pin it in space, or minimize it to a compact bar. Floating panels can be dragged from their header or compact background and stay upright facing the user.
- **Selection and editing:** Select placed items to edit their values, drag nodes and objects, delete individual items, or start new paths from existing nodes. Selected items share an accent color; hovered items gain an outline or halo.
- **Undo and workspace controls:** Undo up to 50 actions. Hold Undo to clear the live workspace, or hold Load room to delete the saved room and its anchors. Clearing the workspace is undoable and preserves the saved room.
- **Room persistence:** Save room geometry locally with a PICO spatial anchor and restore it after relocalization. Tracking loss hides anchored overlays and blocks placement; a failed load preserves the current workspace. Saved room geometry uses no cloud service or account.
- **Visual preferences:** Choose from three presets, including a high-contrast option, and retain input, handedness, units, and wrist-gesture preferences between sessions.
- **Snapshots:** Capture overlays through the PICO Platform Highlight API on Android, with the menu and pointer hidden. Requires a configured PICO Platform app ID. Passthrough capture, Gallery visibility, and phone access still require device verification.
- **Editor preview:** Try the tools in an explicitly simulated room in Unity Play Mode. This room is excluded from headset runtime.

Measurements depend on room scanning and tracking quality. Physical measurement accuracy, hand ergonomics, stereo presentation, and anchor relocalization require acceptance testing on the target headset. Fit Check uses constrained placement rather than a free-falling furniture simulation. Picture Hanging uses the hanger inset you provide; it does not infer wire sag or fastener suitability.

### Running and building

1. Open the repository with Unity **6000.3.11f1**, with Android Build Support installed, and wait for compilation.
2. Open `Assets/Scenes/SpatialToolkitWorkbench.unity`.
3. Press Play for the simulated desktop room, or select **PICO > Project > Build Development APK** for Android. Keep the Android build target and configure signing credentials in Unity when required.
4. Find the APK at `Builds/Android/spatial-toolkit.apk`. The build helper records its result in `Logs/AndroidBuildResult.json`.

Desktop controls: left click places or selects, right drag moves an item, **Z** undoes, **B** toggles snapping, and the mouse wheel changes pointer reach. Click a dimension row to enter a value.

Controller controls: dominant trigger places or selects, grip drags, **A/X** undoes, **B/Y** toggles snapping, and thumbstick up/down changes pointer reach. Pinch selects with hands; hold a pinch on an item to drag, then release to drop. Menu controls expose the tools and editing actions without controller shortcuts.

See [SETUP.txt](SETUP.txt) for MR configuration, emulator and device installation, controller-free operation, and command-line helpers. See [APP-NOTES.txt](APP-NOTES.txt) for implementation details and historical verification records; the changelog below describes subsequent changes to earlier behavior.

## Changelog

All five numbered builds recorded to date are listed newest first. Changes and verification below are reconstructed from the repository's implementation notes and build records. Verification counts describe checks recorded for those builds, rather than tests rerun for this README.

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
