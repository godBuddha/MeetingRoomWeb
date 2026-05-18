#!/bin/bash
# ── Build MeetingRoom Launcher trên Ubuntu/Debian ──
# Yêu cầu: cmake, build-essential, libsdl2-dev, libgl1-mesa-dev

set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "==> Installing dependencies..."
sudo apt-get update -qq
sudo apt-get install -y cmake build-essential libsdl2-dev libgl1-mesa-dev libglu1-mesa-dev

echo "==> Building..."
cmake -B "$SCRIPT_DIR/build" -DCMAKE_BUILD_TYPE=Release -S "$SCRIPT_DIR"
cmake --build "$SCRIPT_DIR/build" --config Release -j$(nproc)

echo ""
echo "  Build xong! Chạy:"
echo "  $SCRIPT_DIR/build/MeetingRoomLauncher"
