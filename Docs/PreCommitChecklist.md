# Pre-Commit Checklist

Run through this list before committing so the repo stays clean and the project remains the single source of truth.

## 1. Verify build

- [ ] Project opens in Unity without errors (correct version; see `ProjectSettings/ProjectVersion.txt`).
- [ ] **Build for Android/Quest** succeeds (or at least builds without new errors you introduced).
- [ ] If you changed scripts or prefabs, do a quick test in Editor or on device.

## 2. Do not commit generated or local-only paths

Ensure these are **not** staged (they should be in `.gitignore`):

- [ ] `Library/`
- [ ] `Temp/`
- [ ] `Obj/`
- [ ] `Build/` or `Builds/`
- [ ] `Logs/`
- [ ] `UserSettings/`
- [ ] `.vs/` (Visual Studio)

Quick check:

```bash
git status
# Ensure no paths like Library/, Temp/, Obj/ appear
```

## 3. Packages: manifest and lock file

- [ ] **Do not** remove or edit `Packages/manifest.json` or `Packages/packages-lock.json` unless you intentionally changed dependencies.
- [ ] If you added/removed/updated a package: **commit both** `manifest.json` and `packages-lock.json` so restore is deterministic (see `Docs/Dependencies.md`).

## 4. Git LFS – large binary files

- [ ] Large binaries (e.g. `.fbx`, `.png`, `.wav`, `.unitypackage`) should be **tracked by Git LFS**.
- [ ] If you add new binary types, add them to `.gitattributes` and run `git lfs track "*.ext"` (see project `.gitattributes` for current patterns).
- [ ] After changing LFS track patterns, run `git lfs pull` and re-commit if needed.

## 5. No secrets

- [ ] No API keys, passwords, or tokens in committed files. Use empty placeholders or env/local config; see README for Hugging Face key setup.

## 6. Optional: run hygiene check

If the repo has a GitHub Action for repo hygiene (e.g. `.github/workflows/repo_hygiene.yml`), push to a branch and ensure the workflow passes (no `Library/`, `Temp/`, `Obj/`; manifest/lock present; LFS in use).

---

**Summary:** Build works → no Library/Temp/Obj → manifest + lock committed when deps change → LFS for binaries → no secrets.
