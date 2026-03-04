# Temporal Echo — Showcase Steps & Troubleshooting

## Exact steps

1. **Run one-click setup**  
   **Tools → Temporal Echo → Setup Full Runtime (Meta XR)**  
   - Prepares scene (removes duplicates, disables mic objects, one EventSystem, one VideoPlayer, one AudioSource).  
   - Creates/assigns orchestrator, canvas, anchors, VideoScreen, detection bridge.  
   - Calibrates UI panel.  
   - Prints **READY FOR SHOWCASE** checklist in Console.

2. **Assign EraDatabase**  
   - Open **Assets/TemporalEcho/EraDatabase.asset** (or your instance).  
   - For **1920s** and **1960s**: set **Soundtrack** (AudioClip), and **Rules** (Screen/Book/Human → prefab + local position/rotation/scale).  
   - For **2026**: set **Video Clip** (VideoClip) and **Rules**.  
   - Prefabs are spawned at ScreenAnchor, BookAnchor, HumanAnchor when that subject is detected.

3. **Build to device and test**  
   - Build to Quest (Meta XR).  
   - Enter Play: detection health logs once per second (detector, box count, sample label).  
   - Point at screen/book/person: first detection spawns overlay and starts era media (audio or video, play once).  
   - When media finishes, era auto-advances (1920s → 1960s → 2026 → 1920s).  
   - Use UI panel (pinch): click era to switch immediately; overlays and media update.

---

## Troubleshooting

| Issue | What to check |
|-------|----------------|
| **UI not clickable** | Single EventSystem with **OVRInputModule** only (no Standalone, no XRUIInputModule). EraSwitcherCanvas has **OVRRaycaster** and **Canvas.worldCamera** = CenterEyeAnchor or Main Camera. Run **Prepare Scene for Showcase** or **Setup Full Runtime** again. |
| **Panel far / backwards** | **WorldSpacePanelPlacer**: distance 0.45–0.85 (default 0.60), heightOffset -0.10. Run **Tools → Temporal Echo → Calibrate Era UI Panel**. Enable **flip180** if panel faces away. CenterEye is auto-found (OVRCameraRig or Camera.main). |
| **Detection not firing** | **TemporalEchoDetectionBridge** on same GameObject as **ObjectDetectionAgent**. Console: "Detection health: detector=True/False lastBoxCount=…". If detector=False, add/find ObjectDetectionAgent (Meta XR Building Blocks). If lastBoxCount always 0, check camera/permissions and that the detector is running on device. Enable **logDetections** on bridge for per-hit logs. |
| **Two videos** | **Prepare Scene for Showcase** disables all VideoPlayers except the one on **VideoScreen** under **ScreenAnchor**. **EraMediaController.StopAllVideosExceptCanonical()** runs before playing. Ensure only one EraVideoScreen in scene (under ScreenAnchor). |
| **Audio not finishing / auto-advance not happening** | Audio is **play once** (loop=false). **EraMediaController** fires **OnEraMediaFinished** when audio stops or video **loopPointReached**. Orchestrator subscribes and advances era. If using PlayClipAtPoint (no AudioSource), advance won’t trigger—assign **AudioSource** on "Temporal Echo Runtime" and assign it in EraMediaController. |
| **Setup menu exception (Type null)** | All **GetComponent(type)** and **AddComponent(type)** in editor scripts now null-check the type before use. If you see "Type cannot be null", ensure Meta XR SDK is present; XR Interaction Toolkit is optional and its type is checked for null before use. |

---

## Files changed/created (summary)

- **Editor:** `TemporalEchoRuntimeSetupMenu.cs` (hardened null checks; Setup Full Runtime calls Prepare + Calibrate + READY log).  
- **Editor:** `TemporalEchoPrepareShowcase.cs` (new – Prepare Scene for Showcase).  
- **Editor:** `EraSwitcherUI.cs` (null-check for XRI type; SetDebugMediaState; _lastDetectedLabel).  
- **Runtime:** `EraMediaController.cs` (play once, OnEraMediaFinished, StopAllVideosExceptCanonical, MediaState, no TemporalEraManager).  
- **Runtime:** `EraVideoScreen.cs` (Play(clip, loop), removed ExecuteAlways).  
- **Runtime:** `TemporalEchoRuntimeOrchestrator.cs` (SetEra, OnEraMediaFinished auto-advance, SetDebugMediaState).  
- **Runtime:** `WorldSpacePanelPlacer.cs` (CalibrateNow in Start).  
- **Runtime:** `TemporalEchoDetectionBridge.cs` (detection health log once per second, logDetectionHealth).  
- **Runtime:** `PersistentDiscoveryManager.cs` (DisableVirtualMic = true, skip mic spawn/activate).  
- **Runtime:** `MicrophoneInteractable.cs` (DISABLE_VIRTUAL_MIC = true, Trigger early-out).  
- **Doc:** `ShowcaseStepsAndTroubleshooting.md` (this file).
