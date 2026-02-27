# Herald Examiner 100 Years – Unity MR (Quest)

Unity Mixed Reality project for Meta Quest. AI object detection overlays (typewriters on screens, newspaper on books, hats on humans) with detection-based prefab placement.

## Unity version

**6000.2.15f1** (see `ProjectSettings/ProjectVersion.txt`)

## Build target

- **Primary:** Android (Meta Quest)
- OpenXR / Meta XR SDK; URP

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

Then open the project in Unity and use scene `Assets/Scenes/AIBuildingBlocks.unity`. On first load, the **Project Bootstrap Check** (Editor script) may show a dialog if Unity version, packages, or Android settings need attention; follow the steps it suggests.

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
