# Herald Examiner 100 Years – Unity MR (Quest)

Mixed reality AI experience for Meta Quest. Recognizes laptops/screens, humans, and books to overlay 3D objects (typewriters, newspaper, hats) via AI object detection and tracking. Includes **Temporal Echo** era switching (1920s / 1960s / 2026) with era-specific overlays and media.

---

## Quick Start

1. Install [Unity Hub](https://unity.com/download) and add **Unity 6000.2.15f1**.
2. Clone this repo: `git clone https://github.com/arttechleo/HeraldExaminer100years.git`
3. Run `git lfs pull` (or the bootstrap script: `.\Tools\bootstrap\bootstrap_windows.ps1`).
4. Open the project in Unity Hub → Add → select the cloned folder.
5. Open scene **Assets/Scenes/AIBuildingBlocks.unity**.
6. Run **Tools → Temporal Echo → Setup Full Runtime (Meta XR)** (run once).
7. Enter Play mode or Build for Android (Quest).

---

## Setup (deliberate steps)

**Unity version:** **6000.2.15f1** (see `ProjectSettings/ProjectVersion.txt`)

**Required packages** (from `Packages/manifest.json`):
- Meta XR MR Utility Kit
- Meta OpenXR
- Unity AI Inference
- URP (com.unity.render-pipelines.universal)

**Android build settings:**
- Platform: Android
- Scripting Backend: IL2CPP
- Target Architectures: ARM64
- Minimum API Level: 29
- OpenXR feature groups: Oculus Quest, Oculus Quest 2, Meta Quest 3

**One-click setup (run once after opening the scene):**
1. Open `Assets/Scenes/AIBuildingBlocks.unity`
2. **Tools → Temporal Echo → Setup Full Runtime (Meta XR)**
3. Enter Play mode or build to device

---

## Interactions (how to use the app)

**Detection overlays:** Point your headset at screens/laptops, books, and people. The app overlays:
- **Screens** → typewriter (era-specific)
- **Books** → newspaper (era-specific)
- **Humans** → hat (era-specific)

**Era switching (Temporal Echo):**
- **UI panel:** Pinch-click the era buttons (**1920s** / **1960s** / **2026**). Overlays and media (audio/video) switch immediately.
- **Auto-advance:** When the current era’s soundtrack or video finishes, the app advances to the next era (1920s → 1960s → 2026 → 1920s).

**Bounding box demo visibility (ObjectDetectionVisualizerV2):**
- **Show Bounding Boxes** checkbox: tick = show boxes/labels when AI detects objects; untick = hide.
- Optional: assign a UI Button to toggle boxes; button text updates to "Show Boxes" / "Hide Boxes".
- Add **DisableDetectionVisualizerOnStart** to start with boxes hidden; use the checkbox or button to show for demos.

**Editor testing:** Use XR Simulation (Meta XR) or Link; detection may run at reduced rate. Era UI panel floats in front; switch eras to change overlays and media.

---

## Build

**How to build the APK:**
1. File → Build Settings
2. Platform: **Android** (switch if needed)
3. Scene: `Assets/Scenes/AIBuildingBlocks.unity` (ensure it is in Scenes In Build, enabled)
4. **Build** or **Build And Run** (deploys to connected Quest)

Full build steps and troubleshooting: see **Assets/Docs/BUILD.md**.

**Release candidate:** **AIBuildingBlocksDemoIT8RC.apk**  
- Built from this repo.  
- Build output directory: `S:\Projects\2026\Unity\BUILDS\AIBuildingBlocksDemo`  
- ~146 MB (Quest 2/3 compatible).

---

## How to open the project

1. Install [Unity Hub](https://unity.com/download) and add **Unity 6000.2.15f1** (or the exact version from `ProjectSettings/ProjectVersion.txt`).
2. Clone this repo (and ensure [Git LFS](https://git-lfs.com/) is installed and run `git lfs pull` after clone if binaries are LFS-tracked).
3. In Unity Hub: **Add** → select the folder containing this `README.md` and the `Assets`, `Packages`, and `ProjectSettings` folders.
4. Open the project; open scene `Assets/Scenes/AIBuildingBlocks.unity` for the main MR detection demo.

## Getting Started (Fresh Clone)

After cloning the repo, run the bootstrap script so Git LFS content is pulled and you have clear steps to open in Unity:

- **Windows (PowerShell):**  
  `.\Tools\bootstrap\bootstrap_windows.ps1`
- **macOS:**  
  `Tools/bootstrap/bootstrap_mac.command` (or double‑click; make executable with `chmod +x` if needed)

The script will:

1. Check that **Git** and **Git LFS** are installed (with install links if not).
2. Run **`git lfs pull`** to download LFS-tracked assets.
3. Print instructions to open the project in Unity Hub (**Add → Add project from disk** → select this folder).

Then open the project in Unity and use scene `Assets/Scenes/AIBuildingBlocks.unity`. Run **Tools → Temporal Echo → Setup Full Runtime (Meta XR)** to wire the demo.

## Requirements

- Meta XR SDK / MR Utility Kit (via Packages)
- Unity AI Inference (for object detection)
- Quest device or Link for testing

**Hugging Face (optional):** If you use Hugging Face providers (e.g. DETR, LLM), set your API key in Unity: the `apiKey` field in the provider assets under `Assets/MetaXR/` is intentionally empty in the repo. Configure it in the Inspector or keep the assets local-only.

## Repository layout

- `Assets/` – Scenes, scripts, 3D assets, prefabs
- `Packages/` – Unity package manifest (dependencies resolved by Unity)
- `ProjectSettings/` – Project and player settings

Excluded from the repo (see `.gitignore`): `Library/`, `Temp/`, `Logs/`, `Build/`, `UserSettings/`, etc.
