#!/bin/bash
# ── Build MeetingRoom Launcher trên macOS ──
# Yêu cầu: cmake, SDL2 (brew install cmake sdl2)

set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "==> Checking dependencies..."
command -v cmake >/dev/null || { echo "Install cmake: brew install cmake"; exit 1; }
command -v sdl2-config >/dev/null 2>&1 || brew install sdl2

echo "==> Building..."
cmake -B "$SCRIPT_DIR/build" -DCMAKE_BUILD_TYPE=Release -S "$SCRIPT_DIR"
cmake --build "$SCRIPT_DIR/build" --config Release -j$(sysctl -n hw.logicalcpu)

echo ""
echo "  Build xong! Chạy:"
echo "  $SCRIPT_DIR/build/MeetingRoomLauncher"
