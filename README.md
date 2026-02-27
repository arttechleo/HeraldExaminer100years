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

## Requirements

- Meta XR SDK / MR Utility Kit (via Packages)
- Unity AI Inference (for object detection)
- Quest device or Link for testing

## Repository layout

- `Assets/` – Scenes, scripts, 3D assets, prefabs
- `Packages/` – Unity package manifest (dependencies resolved by Unity)
- `ProjectSettings/` – Project and player settings

Excluded from the repo (see `.gitignore`): `Library/`, `Temp/`, `Logs/`, `Build/`, `UserSettings/`, etc.
