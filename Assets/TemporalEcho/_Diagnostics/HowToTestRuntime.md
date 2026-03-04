# How to test the Temporal Echo runtime (Meta XR)

## Before testing

1. **Run setup once:** **Tools → Temporal Echo → Setup Full Runtime (Meta XR)**  
   This ensures EventSystem (OVRInputModule only), EraSwitcherCanvas, anchors, VideoScreen, Temporal Echo Runtime GO, and detection bridge. Check the console for the checklist.

2. **EraDatabase:** In **Assets/TemporalEcho/EraDatabase.asset**, assign for each era (1920s, 1960s, 2026):
   - **Soundtrack** (AudioClip) for 1920s/1960s
   - **Video clip** (optional) for 2026
   - **Rules:** Screen, Book, Human → prefab + local position/rotation/scale

3. **Panel placement:** Enter Play mode, then run **Tools → Temporal Echo → Calibrate Era UI Panel** (or call `WorldSpacePanelPlacer.CalibrateNow()` at runtime) so the panel is at a comfortable distance and faces you.

## What to test

### UI era switching

- Put on the headset (or use Meta XR Simulator).
- Find the floating era panel (1920s / 1960s / 2026).
- Use **hand ray + pinch** to click a button.
- **Expected:** Console logs `[TemporalEcho] UI era click: Era1920s` (or 1960s / Today). Era state and media should switch.

### Detection → replacements

- Ensure **TemporalEchoDetectionBridge** is on the same GameObject as **ObjectDetectionAgent** (setup menu adds it).
- Point the device at a **screen/laptop**, **book**, or **person** (or use simulated detection if available).
- **Expected:** Console logs `[TemporalEchoDetectionBridge] Detection: Screen (confidence=0.xx)` (or Book/Human). Then `[TemporalEcho] Replacement spawn: subject=Screen prefab=... era=...`. One overlay per subject under ScreenAnchor, BookAnchor, HumanAnchor.

### Media playback

- After at least one detection (or on era change), **Expected:** Console logs `[TemporalEcho] Media switch: audio clip=...` or `video clip=...`. 1920s/1960s play soundtrack; 2026 can play video on the screen anchor if configured.

### Panel follow and facing

- Move the camera (walk or rotate).
- **Expected:** Panel follows at ~0.6 m, stays in front and faces the user. If it faces away, enable **flip180** on **WorldSpacePanelPlacer** or run **Calibrate Era UI Panel**.

## Debug logs summary

| Event            | Log message |
|------------------|-------------|
| UI era click     | `[TemporalEcho] UI era click: {era}` |
| Detection        | `[TemporalEchoDetectionBridge] Detection: {subject} (confidence=...)` (if bridge `logDetections` is on) |
| Replacement spawn| `[TemporalEcho] Replacement spawn: subject=... prefab=... era=...` |
| Media switch     | `[TemporalEcho] Media switch: audio clip=...` or `video clip=...` |

## Troubleshooting

- **Panel not clickable:** Ensure EventSystem has **OVRInputModule** only (no Standalone, no XRUIInputModule). EraSwitcherCanvas must have **OVRRaycaster** and **Canvas.worldCamera** set to CenterEyeAnchor (or main camera).
- **No replacements:** Check EraDatabase has prefabs for the active era and subject. Ensure ScreenAnchor, BookAnchor, HumanAnchor exist. If using detection bridge, ensure ObjectDetectionAgent is running and bridge is on the same GO.
- **Two sets of overlays:** For a single canonical path, use only the orchestrator for Screen/Book/Human at anchors. Disable those categories on **DetectionPrefabReplacer** if you do not want PDM to also spawn at resolved poses.
