# Temporal Echo — Canonical System Report

## Scene objects after Rebuild

| Object | Purpose |
|--------|---------|
| **EventSystem** | One only. OVRInputModule. |
| **EraSwitcherCanvas** | World Space UI. OVRRaycaster, WorldSpacePanelPlacer, EraSwitcherUI. |
| **ScreenAnchor** | Anchor for Screen overlays + VideoScreen child. |
| **BookAnchor** | Anchor for Book overlays. |
| **HumanAnchor** | Anchor for Human overlays. |
| **VideoScreen** | Single instance under ScreenAnchor. Quad + VideoPlayer + EraVideoScreen. |
| **TemporalEchoSystem** | Main system GO. All runtime logic. |

## TemporalEchoSystem components

| Component | Role |
|-----------|------|
| **ShowcaseSanitizer** | Disables duplicates at runtime (DefaultExecutionOrder -1000). |
| **TemporalEchoRuntimeOrchestrator** | Only source of overlays/media from EraDatabase. |
| **TemporalEchoDetectionBridge** | Strict token mapping, top-1 per subject, edge-trigger. |
| **EraMediaController** | One AudioSource, one EraVideoScreen. Play once → auto-advance. |
| **AudioSource** | Era soundtracks (1920s/1960s). |
| **TemporalEchoDetectionBridge** | Maps detections to Screen/Book/Human; connects to ObjectDetectionAgent. |

## Scripts disabled by Rebuild

- Duplicate Orchestrators, Bridges, EraMediaControllers (kept only on TemporalEchoSystem)
- StorySelectionManager
- Mic-related GameObjects (names containing Mic, Microphone)
- Duplicate EventSystems
- Duplicate VideoScreens / VideoPlayers
- StandaloneInputModule, XRUIInputModule (replaced by OVRInputModule)

## How to test in-headset

1. **Launch APK** — UI panel should be close (~0.55 m) and face the user.
2. **Show laptop / book / person** — Overlay appears for first detection; media starts once.
3. **Media auto-advance** — When audio/video finishes, era advances (1920s → 1960s → 2026 → 1920s); overlays update.
4. **Switch eras via UI** — Pinch-click 1920s/1960s/2026; overlays and media update immediately.

## Detection tokens (configurable in inspector)

- **Screen:** laptop, screen, monitor, computer, notebook, tv, television
- **Book:** book, newspaper, magazine
- **Human:** person, human, man, woman, face

Labels like "lamp" are never accepted.
