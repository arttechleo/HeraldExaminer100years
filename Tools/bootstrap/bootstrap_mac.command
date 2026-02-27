#!/bin/bash
# Bootstrap script for fresh clone (macOS)
# Checks git, git-lfs, runs git lfs pull, prints instructions for opening in Unity Hub.

set -e

echo "=== Unity project bootstrap (macOS) ==="

# Check git
if command -v git &>/dev/null; then
    echo "[OK] Git: $(git --version)"
else
    echo "[FAIL] Git is not installed or not in PATH."
    echo "  Install: https://git-scm.com/download/mac"
    exit 1
fi

# Check git-lfs
if command -v git-lfs &>/dev/null || git lfs version &>/dev/null; then
    echo "[OK] Git LFS: $(git lfs version 2>/dev/null || echo 'installed')"
else
    echo "[FAIL] Git LFS is not installed."
    echo "  Install: brew install git-lfs   or https://git-lfs.com/"
    exit 1
fi

# Ensure LFS is installed for this repo
git lfs install || true

# Pull LFS objects
echo "Pulling LFS objects..."
if git lfs pull; then
    echo "[OK] LFS pull completed."
else
    echo "[WARN] git lfs pull had issues. Check network and LFS tracking."
fi

echo ""
echo "=== Open in Unity ==="
echo "1. Install Unity Hub: https://unity.com/download"
echo "2. Add Unity 6000.2.15f1 (see ProjectSettings/ProjectVersion.txt for exact version)"
echo "3. In Unity Hub: Add -> Add project from disk"
echo "4. Select this folder (the one containing Assets, Packages, ProjectSettings)"
echo "5. Open the project; open scene: Assets/Scenes/AIBuildingBlocks.unity"
echo ""
