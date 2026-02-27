# Detection Overlays (DetectionPrefabReplacer)

Production-ready detection → prefab replacement/overlay system for Meta Quest Mixed Reality. Spawns typewriters on screens, newspaper on books, and hats on humans.

## Setup

1. Add **DetectionPrefabReplacer** to the same GameObject as `[BuildingBlock] Object Detection` (which already has ObjectDetectionAgent, DepthTextureAccess, EnvironmentDepthManager).
2. Assign prefabs in the Inspector:
   - **Typewriter High Prefab** – used when screen/monitor is close (≤ nearThreshold)
   - **Typewriter Low Prefab** – used when screen/monitor is far
   - **Newspaperfold Prefab** – used for detected books
   - **Hat Prefab** – used for detected humans

3. **XR Camera Transform** – optional; auto-finds Main Camera if null.

## Prefab Assignment

| Category | Prefab | Source Path |
|----------|--------|-------------|
| Screen (close) | TypewriterHIGH | `Assets/3DAssets/typewriterHIGH/TypewriterHIGH` |
| Screen (far) | TypewriterLOW | `Assets/3DAssets/typewriterLOW/TypewriterLOW` |
| Book | newspaperfold | `Assets/3DAssets/newspaperFolded/newspaperfolded/newspaperfold` |
| Human | Hat | `Assets/3DAssets/hats/Hat` |

## nearThreshold (Screen Distance)

- **Default:** 1.5 meters
- **Behavior:** Distance from XR camera to detected screen/monitor center
  - `distance ≤ nearThreshold` → **TypewriterHIGH** (close)
  - `distance > nearThreshold` → **TypewriterLOW** (far)
- Each detected screen is evaluated separately; multiple screens can use different prefabs.

## Hat Head Offsets

### Screen-space Y offset (`hatScreenSpaceYOffset`)

- **Default:** 0.02 (2% of screen height)
- **Use:** Head position uses bbox center X and bbox **top** Y, then moves **up** by this amount in screen space.
- Positive = up, negative = down.

### World-space up offset (`hatWorldUpOffset`)

- **Default:** 0.08 meters
- **Use:** Added above the resolved head position so the hat sits above the head.

### Per-category rotation (Screen / Book / Human)

- Use **Per-Category Tuning** in the Inspector for each category:
  - **Face Camera Yaw Only:** rotate around world up to face the camera (recommended for Hat).
  - **Use Camera Forward Yaw:** align yaw with camera forward (good for screen/book).
  - **Lock Pitch And Roll:** keep overlay upright (default on for all).
- **Hat:** use context menu **Reset Human Tuning to Defaults** to set **Face Camera Yaw Only** on.

## Per-Category Tuning

Each category (Screen, Book, Human) has a **CategoryTuning** block:

- **World Position Offset** – applied in world space after anchor resolve.
- **Local Position Offset** – applied in local space after rotation is set.
- **Rotation Offset Euler** – degrees applied after base rotation (upright + yaw).
- **Local Scale Multiplier** – multiplied with prefab’s original scale.
- **Lock Pitch And Roll** – keep object upright (default on).
- **Face Camera Yaw Only** / **Use Camera Forward Yaw** – choose base yaw.
- **Min Confidence** – minimum detection confidence (0–1) for this category.

Right-click the component header → **Reset Screen/Book/Human Tuning to Defaults** to reset.

See **BBoxToWorldOverlay.md** for how bbox → screen point → ray → world pose → transform works and what each offset means.

## Resolve cadence and smoothing

- **Resolve Hz** (default 10): world pose is re-resolved at this rate, not every frame, to reduce jitter.
- **Position/Rotation smoothing:** uses `1 - exp(-smoothing * dt)` so overlays interpolate smoothly toward the last resolved target every frame.

## Hysteresis & responsiveness

- **minPersistFrames** (default **1**): detection must be seen this many batches before spawning (1 = as soon as seen).
- **graceSeconds** (default 0.5s): overlay stays visible this long after detection disappears.
- **positionSmoothing** (default 12), **rotationSmoothing** (default 10): higher = snappier follow.

## Debugging

- **Debug Draw Ray:** draw ray from camera through bbox center/top to resolved anchor.
- **Debug Draw Gizmo At Resolved:** wire spheres at resolved anchor (Scene view).
- **Debug Show Text Overlay:** on-screen text with label, confidence, key, lastSeen, timeToSpawn, distance, prefab (HIGH/LOW).
- **Debug Log Bbox Info:** log bbox center (px + normalized), depth method, resolved position.
- **Log Timing:** log time from first detection seen to spawn.
- **Log Label Counts:** log counts per category and overlay count.

## Label Mapping

| Category | Labels |
|----------|--------|
| Screen | computer, laptop, monitor, tv, screen, television |
| Book | book, books |
| Human | person, human, people |

## Build

- Compatible with Android/Quest builds; no editor-only runtime logic.
- Requires MRUK (Meta XR MR Utility Kit), PassthroughCameraAccess, and EnvironmentDepth.
