# Build Instructions – Herald Examiner 100 Years

How to build the APK for Meta Quest from this Unity project.

---

## Prerequisites

- **Unity 6000.2.15f1** (see `ProjectSettings/ProjectVersion.txt`)
- **Android Build Support** (IL2CPP) installed via Unity Hub
- **Meta XR / OpenXR** packages (see `Packages/manifest.json`)
- **Quest device** or Link for testing

---

## Build steps

1. Open the project in Unity.
2. **File → Build Settings**
3. **Platform:** Android (click **Switch Platform** if needed)
4. **Scenes In Build:** Ensure `Assets/Scenes/AIBuildingBlocks.unity` is included and enabled
5. **Build** or **Build And Run**
   - **Build:** Creates an APK (choose output folder)
   - **Build And Run:** Builds and deploys to a connected Quest

---

## Player Settings (Android)

Verify in Edit → Project Settings → Player → Android:

- **Scripting Backend:** IL2CPP
- **Target Architectures:** ARM64
- **Minimum API Level:** 29
- **OpenXR:** Feature groups Oculus Quest, Oculus Quest 2, Meta Quest 3 enabled

---

## Build output

- **Output folder:** Choose a folder (e.g. `BUILDS/AIBuildingBlocksDemo/`)
- **APK name:** Set in Build Settings → Product Name (e.g. `AIBuildingBlocksDemoIT8RC`)

---

## Release candidate

**AIBuildingBlocksDemoIT8RC.apk** is the designated release candidate.

- **Location:** `S:\Projects\2026\Unity\BUILDS\AIBuildingBlocksDemo\AIBuildingBlocksDemoIT8RC.apk` (local build output)
- **Size:** ~146 MB
- **Target:** Quest 2 / Quest 3

---

## Troubleshooting

- **IL2CPP errors:** Ensure Android Build Support (IL2CPP) is installed
- **OpenXR missing:** Run **Tools → Temporal Echo → Setup Full Runtime (Meta XR)**
- **Detection not working on device:** Ensure Quest has camera permissions and is in MR mode
- **Overlays not appearing:** Check DetectionPrefabReplacer prefab references; run Setup Full Runtime
