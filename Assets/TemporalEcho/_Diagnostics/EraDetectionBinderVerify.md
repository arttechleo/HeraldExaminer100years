# EraDetectionBinder — 1 Minute Verification

## Setup
1. **Tools → Temporal Echo → Setup Full Runtime (Meta XR)**
2. Assign prefabs in EraDatabase for Screen, Book, Human per era.

## Editor Play Mode
1. Enter Play Mode.
2. Point camera at laptop/screen, book, or person.
3. **Expected logs:**
   - `[EraDetectionBinder] DETECTION UPDATE count=N first3=[...]` — binder receives detections
   - `[EraDetectionBinder] ACCEPT Screen label=... conf=...` — subject accepted
   - `[EraDetectionBinder] SPAWN era=... subject=... prefab=... anchor=...` — overlay spawned
   - `[EraDetectionBinder] MEDIA audio=... / video=...` — era media started on first detection

4. **If no overlay but logs show ACCEPT/SPAWN:** prefab or anchor placement issue (check anchors exist, prefab assigned).
5. **If no DETECTION UPDATE:** ObjectDetectionAgent not found or not running (Editor may not run detection; test on device).

## On Device
1. Build and run on Quest.
2. Point at laptop/book/person.
3. **Expected:** DETECTION UPDATE logs; ACCEPT; SPAWN; overlays appear at anchors.
4. **If no overlay:** check EraDatabase prefabs, anchors (ScreenAnchor, BookAnchor, HumanAnchor).
