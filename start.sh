#!/bin/bash
# ─────────────────────────────────────────────
#  MeetingRoomWeb — Startup Script
#  Chạy: bash start.sh
# ─────────────────────────────────────────────

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
BOLD=$(tput bold); GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

log()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
step() { echo -e "\n${BOLD}── $1 ──${NC}"; }

# ─── 1. Docker Desktop ───────────────────────
step "Khởi động Docker Desktop"
if ! docker info > /dev/null 2>&1; then
    warn "Docker chưa chạy, đang mở..."
    open -a Docker
    echo -n "  Chờ Docker sẵn sàng"
    until docker info > /dev/null 2>&1; do
        echo -n "."
        sleep 2
    done
    echo ""
fi
log "Docker Desktop đang chạy"

# ─── 2. PostgreSQL Container ─────────────────
step "Khởi động PostgreSQL"
if [ "$(docker inspect -f '{{.State.Running}}' meeting-postgres 2>/dev/null)" = "true" ]; then
    log "meeting-postgres đã chạy rồi"
else
    docker start meeting-postgres > /dev/null 2>&1
    sleep 2
    log "meeting-postgres đã start"
fi

# ─── 3. Tailscale Funnel ─────────────────────
step "Kích hoạt Tailscale Funnel"
if tailscale funnel status 2>/dev/null | grep -q "5038\|proxy"; then
    log "Tailscale Funnel đang active"
else
    tailscale funnel --bg 5038 2>/dev/null
    log "Tailscale Funnel đã bật → https://2026.tailba3e29.ts.net"
fi

# ─── 4. Chạy App ─────────────────────────────
step "Chạy MeetingRoomWeb"
log "Local:  http://localhost:5038"
log "Public: https://2026.tailba3e29.ts.net"
echo ""
cd "$PROJECT_DIR"
dotnet run --project MeetingRoomWeb.csproj
