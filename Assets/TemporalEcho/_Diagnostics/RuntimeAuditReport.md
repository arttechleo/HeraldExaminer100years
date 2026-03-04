# Temporal Echo — Runtime Audit Report

**Date:** 2026-03-02  
**Scope:** Assets folder — era switching, detection/replacement, UI placement, UI input.

---

## 1. Scripts by concern

### Era switching / era configs

| Script | Location | Purpose | Canonical? |
|--------|----------|---------|------------|
| **EraDatabase** | TemporalEcho/EraDatabase.cs | ScriptableObject: 1920s/1960s/2026 definitions, rules per Screen/Book/Human, soundtrack/videoClip. | **YES** — single data source for canonical runtime. |
| **EraSwitcherUI** | TemporalEcho/EraSwitcherUI.cs | World-space canvas with 1920s/1960s/2026 buttons; fires `OnEraSelected`. | **YES** — single UI for era selection. |
| **TemporalEchoRuntimeOrchestrator** | TemporalEcho/TemporalEchoRuntimeOrchestrator.cs | Holds current era; subscribes to `OnEraSelected`; applies overlays and media. | **YES** — single runtime orchestrator. |
| **EraMediaController** | TemporalEcho/EraMediaController.cs | Plays soundtrack or video per era (EraDatabase + optional TemporalEraManager). | **YES** — used by orchestrator for media. |
| **EraPreviewApplier** | TemporalEcho/EraPreviewApplier.cs | Editor-time preview of era prefabs (EraDatabase); prefix `__EraPreview__`. | **KEEP** — editor only; does not run at runtime for spawns. |
| **TemporalEraConfig** | TemporalEcho/TemporalEraConfig.cs | ScriptableObject: category→era slot mappings (prefab, offset, scale). | **LEGACY** — used by TemporalEraManager / PDM / DetectionPrefabReplacer. |
| **TemporalEraManager** | TemporalEcho/TemporalEraManager.cs | Holds current era from TemporalEraConfig; provides slot per category. | **LEGACY** — second era path; not used by canonical orchestrator. |
| **EraSwitcherUIBridge** | TemporalEcho/EraSwitcherUIBridge.cs | Wires EraSwitcherUI → TemporalEraManager.SetEra. | **QUARANTINE** — duplicate era path; orchestrator subscribes to UI directly. |
| **StorySelectionManager** | TemporalEcho/StorySelectionManager.cs | Story flow; uses TemporalEraManager. | **LEGACY** — optional; not part of canonical pipeline. |
| **EraSwitchPanel** | TemporalEcho/EraSwitchPanel.cs | Alternative 3D panel with placeInFrontOfCamera. | **QUARANTINE** — duplicate UI placement; use EraSwitcherCanvas + WorldSpacePanelPlacer only. |

### Detection bridging / replacements

| Script | Location | Purpose | Canonical? |
|--------|----------|---------|------------|
| **ObjectDetectionAgent** | Meta.XR.BuildingBlocks.AIBlocks | Emits `OnBoxesUpdated` (List&lt;BoxData&gt;) each frame. | **YES** — single detection source. |
| **DetectionPrefabReplacer** | Scripts/DetectionPrefabReplacer.cs | Subscribes to ObjectDetectionAgent; resolves pose; uses PDM + TemporalEraManager for prefabs. | **OVERLAP** — for canonical path, disable Screen/Book/Human categories so only orchestrator spawns at anchors. |
| **PersistentDiscoveryManager** | Scripts/PersistentDiscoveryManager.cs | Single instance per category (Screen/Book/Human etc.); uses TemporalEraManager for prefabs. | **PARTIAL** — orchestrator can poll it for “detected” counts, or use event bridge from detection. |
| **TemporalEchoRuntimeOrchestrator** | TemporalEcho/TemporalEchoRuntimeOrchestrator.cs | Spawns at ScreenAnchor/BookAnchor/HumanAnchor from EraDatabase; prefix `__EraOverlay__`. | **YES** — single replacement path at anchors. |
| **DetectionCategoryFilter** | Scripts/DetectionCategoryFilter.cs | Maps label → Human/Screen/Book etc.; allowlist. | **KEEP** — shared. |
| **DetectionCategory** | Scripts/DetectionCategory.cs | Enum Screen, Book, Human, … | **KEEP** — shared. |
| **ObjectDetectionVisualizerV2** | Scripts/ObjectDetectionVisualizerV2.cs | Debug visualization of bboxes. | **KEEP** — optional debug. |
| **DetectionLabelCollector** | Scripts/DetectionLabelCollector.cs | Collects labels at runtime (export). | **KEEP** — editor/tooling. |

### UI placement

| Script | Location | Purpose | Canonical? |
|--------|----------|---------|------------|
| **WorldSpacePanelPlacer** | TemporalEcho/WorldSpacePanelPlacer.cs | Follows CenterEyeAnchor; distance/heightOffset; facing user; CalibrateNow(). | **YES** — only component that moves the era panel. |
| **EraSwitcherUI** | TemporalEcho/EraSwitcherUI.cs | No panel movement (follow disabled by default); placement only via placer. | **YES** — buttons only. |
| **EraSwitchPanel** | TemporalEcho/EraSwitchPanel.cs | placeInFrontOfCamera / world position. | **QUARANTINE** — duplicate. |

### UI input (Meta XR)

| Script / Component | Purpose | Canonical? |
|--------------------|--------|------------|
| **EventSystem + OVRInputModule** | Single EventSystem; OVRInputModule only (no Standalone, no XRUIInputModule). | **YES** — ensured by setup menu. |
| **OVRRaycaster** | On EraSwitcherCanvas for hand ray. | **YES** — on canvas. |
| **Canvas.worldCamera** | CenterEyeAnchor or Camera.main. | **YES** — assigned by setup. |
| **EraSwitcherUI.EnsureEventSystem** | Removes Standalone/XRUI, adds OVRInputModule. | **YES** — runtime fallback. |

---

## 2. Duplicates / overlaps

- **Two era sources:** (1) EraDatabase + orchestrator + EraSwitcherUI. (2) TemporalEraConfig + TemporalEraManager + EraSwitcherUIBridge. Canonical = (1).
- **Two replacement spawners:** (1) Orchestrator spawns at anchors from EraDatabase (`__EraOverlay__`). (2) DetectionPrefabReplacer + PDM spawn at resolved world pose from TemporalEraManager. For one path: use (1) only for Screen/Book/Human; disable those categories in DetectionPrefabReplacer or accept overlap.
- **Two UI panels:** EraSwitcherCanvas (World Space + WorldSpacePanelPlacer) vs EraSwitchPanel (3D panel). Canonical = EraSwitcherCanvas.

---

## 3. Single canonical runtime path

| Concern | Canonical component(s) |
|--------|------------------------|
| Era selection state | **TemporalEchoRuntimeOrchestrator** (from EraSwitcherUI.OnEraSelected). |
| Detection events | **ObjectDetectionAgent.OnBoxesUpdated** → **TemporalEchoDetectionBridge** (event-based) and/or orchestrator polls PDM. |
| Replacement spawning | **TemporalEchoRuntimeOrchestrator** at ScreenAnchor/BookAnchor/HumanAnchor from EraDatabase; one instance per subject; prefix `__EraOverlay__`. |
| Media playback | **EraMediaController** (soundtrack or video per era); driven by orchestrator / OnEraSelected. |
| UI placement | **WorldSpacePanelPlacer** (follow + facing); EraSwitcherUI does not move panel. |
| UI input | **EventSystem** with **OVRInputModule** only; **OVRRaycaster** on EraSwitcherCanvas; **worldCamera** set. |

---

## 4. Quarantine (do not delete)

Moved to **Assets/_QuarantineLegacy/** with README:

- **EraSwitcherUIBridge** — UI→TemporalEraManager; era is driven by orchestrator from EraSwitcherUI.
- **EraSwitchPanel** — alternative 3D panel; canonical = EraSwitcherCanvas + WorldSpacePanelPlacer.

Other legacy (remain in place; optional to quarantine later): StorySelectionManager, PrevObjectButton, NextObjectButton, ChooseStoryButton, TemporalEraManager, TemporalEraConfig (still referenced by PDM/DetectionPrefabReplacer if those stay enabled for non–Screen/Book/Human).

---

## 5. Scene checklist after setup

- One **EventSystem** with **OVRInputModule** only.
- **EraSwitcherCanvas** with OVRRaycaster, WorldSpacePanelPlacer, Canvas.worldCamera = CenterEyeAnchor.
- **ScreenAnchor**, **BookAnchor**, **HumanAnchor** present.
- **VideoScreen** under ScreenAnchor (for 2026 video).
- **Temporal Echo Runtime** GO with TemporalEchoRuntimeOrchestrator, EraMediaController; refs: database, eraUI, anchors, videoScreen, audio, detection bridge (or PDM).
- For canonical path only: DetectionPrefabReplacer categories Screen/Book/Human disabled, or only orchestrator used for those three at anchors.
