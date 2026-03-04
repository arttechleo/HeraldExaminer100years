# Persistent Discovery — QA Checklist

## Components per GameObject

| GameObject | Components |
|------------|------------|
| **Detection / MR block** (e.g. [BuildingBlock] Object Detection) | `ObjectDetectionAgent`, `DepthTextureAccess`, `EnvironmentDepthManager`, **`DetectionPrefabReplacer`**, **`PersistentDiscoveryManager`**, **`DetectionLabelCollector`** (optional, for label export) |

- **DetectionPrefabReplacer**: Requires `ObjectDetectionAgent`, `DepthTextureAccess`, `EnvironmentDepthManager`, and **PersistentDiscoveryManager**.
- **PersistentDiscoveryManager**: No other requirements; same GameObject as the replacer is typical.
- **DetectionLabelCollector**: Optional; add to the same GameObject as the replacer to collect labels for **Tools > Temporal Echo > Export Seen Labels**.

## Prefabs to assign (DetectionPrefabReplacer)

| Field | Required | Use |
|-------|----------|-----|
| **Typewriter Low Prefab** | Yes | Only typewriter used; all screens get this (low-poly only). |
| Typewriter High Prefab | No | Unused in persistent mode. |
| **Newspaperfold Prefab** | Yes | Spawned on books; persists. |
| **Hat Prefab** | Yes | Real-time only; follows human, hides when person leaves. |
| Chair 1920s Prefab | No | Future; leave unassigned until asset exists. |
| Desk Items Prefab | No | Future; leave unassigned until asset exists. |

## How to test persistence

1. **Screen (typewriter)**  
   - Look at a monitor/laptop/screen until a typewriter appears.  
   - Look away (or occlude the screen).  
   - **Pass:** Typewriter stays in the scene and does not disappear.

2. **Book (newspaper)**  
   - Look at a book until a newspaper stack/fold appears.  
   - Look away.  
   - **Pass:** Newspaper stays in the scene and does not disappear.

3. **Human (hat)**  
   - Look at a person until a hat appears above their head.  
   - Hat should follow the head while the person is detected.  
   - Look away or have the person leave frame.  
   - **Pass:** After ~10 frames without detection, the hat disappears (real-time only, does not persist).

## Label enumeration

- **Runtime:** Add **DetectionLabelCollector** to the detection GameObject and enable **Show Label Overlay** to see count + first N labels on screen.
- **Export:** Enter Play Mode, run detection for a while, then use **Tools > Temporal Echo > Export Seen Labels**.  
  - Writes `Assets/Logs/SeenLabels_<timestamp>.txt` in Editor (or `Application.persistentDataPath` on device).  
  - Copies the label list to the system clipboard in Editor.

## Chair / Desk (future)

- **DetectionCategoryFilter** already classifies Chair (chair, seat, stool) and Desk (desk, table).
- **DetectionPrefabReplacer** has **Enable Chair Category** and **Enable Desk Category** (default OFF) and prefab slots **Chair 1920s Prefab** and **Desk Items Prefab** (null-safe).
- Assign prefabs and enable the toggles when assets are ready.
