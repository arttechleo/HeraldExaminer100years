# Bootstrap script for fresh clone (Windows)
# Checks git, git-lfs, runs git lfs pull, prints instructions for opening in Unity Hub.

$ErrorActionPreference = "Stop"

Write-Host "=== Unity project bootstrap (Windows) ===" -ForegroundColor Cyan

# Check git
try {
    $gitVersion = git --version 2>&1
    Write-Host "[OK] Git: $gitVersion" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Git is not installed or not in PATH." -ForegroundColor Red
    Write-Host "  Install: https://git-scm.com/download/win" -ForegroundColor Yellow
    exit 1
}

# Check git-lfs
try {
    $lfsVersion = git lfs version 2>&1
    Write-Host "[OK] Git LFS: $lfsVersion" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Git LFS is not installed or not in PATH." -ForegroundColor Red
    Write-Host "  Install: https://git-lfs.com/ (or: winget install GitHub.GitLFS)" -ForegroundColor Yellow
    exit 1
}

# Ensure LFS is installed for this repo
git lfs install
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] git lfs install failed (non-fatal)." -ForegroundColor Yellow
}

# Pull LFS objects
Write-Host "Pulling LFS objects..." -ForegroundColor Cyan
git lfs pull
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] git lfs pull had issues. Check network and LFS tracking." -ForegroundColor Yellow
} else {
    Write-Host "[OK] LFS pull completed." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Open in Unity ===" -ForegroundColor Cyan
Write-Host "1. Install Unity Hub: https://unity.com/download"
Write-Host "2. Add Unity 6000.2.15f1 (see ProjectSettings/ProjectVersion.txt for exact version)"
Write-Host "3. In Unity Hub: Add -> Add project from disk"
Write-Host "4. Select this folder (the one containing Assets, Packages, ProjectSettings)"
Write-Host "5. Open the project; open scene: Assets/Scenes/AIBuildingBlocks.unity"
Write-Host ""
