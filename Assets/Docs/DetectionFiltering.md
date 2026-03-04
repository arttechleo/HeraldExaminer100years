# Detection Filtering: 3-Category Allowlist

Only **Human**, **Screen** (laptop/TV/monitor), and **Book** detections are used for overlays. Everything else is excluded as early as possible to avoid inference, postprocess, and overlay work.

## How category filtering works

- **DetectionCategoryFilter** (`Assets/Scripts/DetectionCategoryFilter.cs`) is the single place that maps raw labels to the three allowed categories.
- **Label tokens** (case-insensitive):
  - **Human:** `person`, `human`
  - **Screen:** `laptop`, `computer`, `monitor`, `screen`, `tv`, `television`
  - **Book:** `book`
- Any other label (e.g. `cell phone`, `bottle`, `chair`) is classified as **None** and filtered out.
- **Classify(label)** returns `Human`, `Screen`, `Book`, or `None`. **IsAllowed(category)** is true only for the three categories.
- If the detector ever exposes **numeric class IDs** (e.g. from a model label list), you can call **SetClassIdMapping** and use **ClassifyFromClassId** for faster ID-based filtering instead of string comparison.

## Where in the pipeline filtering is applied

- **Detector / NMS:** The detector (ObjectDetectionAgent and Unity Inference or Hugging Face provider) runs inside the Meta XR package. We do not modify it. NMS and postprocess run there; by the time we receive **BoxData**, we only get a **label string** (e.g. `"laptop 0.87"`), not class IDs.
- **Earliest point in our code:** In **DetectionPrefabReplacer.HandleBatch**, the **first** thing we do for each detection is:
  1. Parse label and confidence from `b.label`.
  2. **DetectionCategoryFilter.Classify(baseLabel)** → if **!IsAllowed(filterCat)** we **continue** immediately.
  3. Only then do we check category toggles, confidence threshold, and run anchor resolve / spawn / update.
- So filtering happens **before** any anchor resolve, pooling, or overlay logic. Excluded detections never allocate overlay state or run depth/raycast.

## Confidence thresholds and responsiveness

- Per-category minimum confidence is set in **DetectionPrefabReplacer** under **Per-Category Tuning**: **screenTuning.minConfidence**, **bookTuning.minConfidence**, **humanTuning.minConfidence** (default **0.35**). Lower = more responsive but more false positives; higher = fewer false positives but may miss detections.
- **minPersistFrames** (default **1**) and **graceSeconds** (default **0.4**) control how quickly overlays appear and how long they stay after the object leaves the frame. See **LatencyTuning.md** for keeping “first detection → overlay visible” under 1 second.
- The detector runs at its own rate (inference-bound); we do not add a “detection interval” in this project. For faster feedback, use **minPersistFrames = 1** and the first-spawn **fixed-depth** path so the overlay appears immediately and refines on the next resolve.

## Overlay behavior (unchanged)

- **Screen** → TypewriterHIGH (near) / TypewriterLOW (far).
- **Book** → newspaperfold.
- **Human** → Hat (head placement, camera-facing).
- Overlay rotation does **not** use bbox rotation; orientation is derived from world/camera as configured in per-category tuning.
