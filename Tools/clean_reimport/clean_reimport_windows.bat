@echo off
REM Deletes Library folder for a clean reimport. Close Unity before running.
REM After running, reopen the project from Unity Hub.

cd /d "%~dp0"
cd ..\..
if not exist "Assets" (
    echo ERROR: Assets folder not found. Run this from Tools/clean_reimport/
    pause
    exit /b 1
)

if exist "Library" (
    echo Deleting Library/ ...
    rmdir /s /q "Library"
    echo Library/ deleted.
) else (
    echo Library/ not found (already clean or first open).
)

echo.
echo Next steps:
echo 1. Open Unity Hub
echo 2. Add project from disk: select this folder (see path above)
echo 3. Open the project - Unity will reimport all assets (may take a few minutes)
echo.
pause
