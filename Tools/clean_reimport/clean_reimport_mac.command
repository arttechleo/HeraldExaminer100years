#!/bin/bash
# Deletes Library folder for a clean reimport. Close Unity before running.
# After running, reopen the project from Unity Hub.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$PROJECT_ROOT"

if [ ! -d "Assets" ]; then
    echo "ERROR: Assets folder not found. Run this from Tools/clean_reimport/"
    exit 1
fi

if [ -d "Library" ]; then
    echo "Deleting Library/ ..."
    rm -rf "Library"
    echo "Library/ deleted."
else
    echo "Library/ not found (already clean or first open)."
fi

echo ""
echo "Next steps:"
echo "1. Open Unity Hub"
echo "2. Add project from disk: select this folder ($PROJECT_ROOT)"
echo "3. Open the project - Unity will reimport all assets (may take a few minutes)"
echo ""
