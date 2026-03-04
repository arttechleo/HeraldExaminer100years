# Latency Tuning: Detection → Overlay in &lt; 1 Second

This doc explains how to get overlays to appear within **0.2–0.8 seconds** after an object is first recognized.

## What controls detection rate

- **Detector (ObjectDetectionAgent)** runs inference every frame when not busy. There is no “detection interval” in this project’s scripts; the rate is limited by inference time (model + device). The Building Block uses `realtimeInference = true`, so it calls inference as often as it can.
- **Overlay manager (DetectionPrefabReplacer)** only receives boxes when the detector fires; it does not throttle. To see effective **detection Hz**, enable **Show Debug Timing** and check the periodic log line `detectionHz≈…`.

## minPersistFrames and graceSeconds

- **minPersistFrames** (default **1**): How many consecutive detection batches must contain this object before we spawn an overlay.  
  - **1** = spawn as soon as the object is seen once (fastest; may flicker if the detector is noisy).  
  - **2** = wait one more frame (slightly more stable).  
  - Use **1** for lowest latency.

- **Grace (per category):** After the object disappears from detections, overlays stay visible for a category-specific time: **screenGraceSeconds** (default 1.5 s), **bookGraceSeconds** (1.5 s), **humanGraceSeconds** (1.2 s). An extra **extraGraceOnCameraMoveSeconds** (0.75 s) is added when the camera has moved or turned beyond threshold, so overlays don’t pop off when you turn your head. See **OverlayPersistence.md** for details.

## How to hit &lt; 1 s latency

1. **Use default gating:**  
   - `minPersistFrames = 1`  
   - `graceSeconds = 0.35–0.5`

2. **First-frame placement:**  
   The overlay manager uses a **fast path** on first spawn: it places the overlay with **fixed depth** (no depth texture read) so it appears immediately. On the next resolve tick (~10 Hz), it refines position with depth when available.

3. **Keep resolve cadence:**  
   - `resolveHz` (default 10) controls how often we re-resolve world pose. Smoothing runs every frame toward the last resolved target.  
   - Do not lower resolve rate to “save cost” if you want responsive movement; 10 Hz is a good default.

4. **Per-class confidence:**  
   In **Per-Category Tuning**, set **Min Confidence** (e.g. **0.35**) for Screen, Book, and Human. Too high and objects are missed; too low and false positives appear.

5. **Recognition filter:**  
   Only **Screen**, **Book**, and **Human** are processed. Other classes are ignored as soon as the batch is received, which avoids extra work and keeps latency low.

## Debug timing (Show Debug Timing)

With **Show Debug Timing** enabled:

- On spawn you’ll see: `timeFirstSeen=… timeSpawned=… timeToSpawn=…s detectionHz≈…`
- Every **Log Every N Seconds** you’ll see: `detectionHz≈… lastResolveMs=… minPersistFrames=… graceSeconds=…`

**timeToSpawn** = time from first time the object was seen in a batch to the moment the overlay was spawned. In typical conditions this should be **&lt; 1 s** (often 0.2–0.8 s) with the defaults above.
