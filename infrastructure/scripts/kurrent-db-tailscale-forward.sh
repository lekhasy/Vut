#!/usr/bin/env bash
# Velucid Platform - KurrentDB Port Forward via Tailscale
#
# Exposes KurrentDB ports to other machines on the Tailscale network.
# Run this script on the WSL machine where K3s is running.
#
# What it does:
#   1. Detects the Tailscale IP address in WSL
#   2. Starts kubectl port-forward to KurrentDB service (local side)
#   3. Uses socat to bridge Tailscale interface to the local port-forward
#   4. Outputs the connection strings for external clients
#
# Usage:
#   ./kurrentdb-tailscale-forward.sh start   # Start port forwarding
#   ./kurrentdb-tailscale-forward.sh stop    # Stop port forwarding
#   ./kurrentdb-tailscale-forward.sh status  # Check if running
#   ./kurrentdb-tailscale-forward.sh logs    # View socat logs
#
# Connection from other Tailscale machines:
#   HTTP:  http://100.99.15.109:2113
#   TCP:   100.99.15.109:1113

set -euo pipefail

# ── Colors & Helpers ──────────────────────────────────────────────────

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

info()  { echo -e "${BLUE}[INFO]${NC}  $*"; }
ok()    { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
fail()  { echo -e "${RED}[FAIL]${NC}  $*"; exit 1; }

# ── Constants ─────────────────────────────────────────────────────────

KURRENTDB_HTTP_PORT=2113
KURRENTDB_TCP_PORT=1113
NAMESPACE=velucid
SERVICE_NAME=velucid-kurrentdb
PIDFILE="/tmp/kurrentdb-forward.pid"
LOGFILE="/tmp/kurrentdb-forward.log"

# ── Detect Tailscale IP ───────────────────────────────────────────────

detect_tailscale_ip() {
    local ts_ip=""
    # Method 1: tailscale status (most reliable)
    if command -v tailscale &>/dev/null; then
        ts_ip=$(tailscale status --self --json 2>/dev/null | grep -o '"Self":{[^}]*}' | grep -o '"DNSName":"[^"]*"' | cut -d'"' -f4 | cut -d'.' -f1)
        if [[ -z "$ts_ip" ]]; then
            # Method 2:直接从tailscale0接口获取IP
            ts_ip=$(ip -4 addr show tailscale0 2>/dev/null | grep -oP 'inet \K[\d.]+' || true)
        fi
        if [[ -z "$ts_ip" ]]; then
            # Method 3: use tsnet via ip command
            ts_ip=$(ip -4 addr show tailscale0 | head -n2 | tail -n1 | awk '{print $2}' | cut -d'/' -f1 || true)
        fi
    fi

    if [[ -z "$ts_ip" ]]; then
        fail "Could not detect Tailscale IP. Is Tailscale running?"
    fi

    echo "$ts_ip"
}

# ── Check prerequisites ────────────────────────────────────────────────

check_prereqs() {
    if ! command -v kubectl &>/dev/null; then
        fail "kubectl not found. Install k3s or kubectl first."
    fi

    if ! command -v socat &>/dev/null; then
        warn "socat not found. Installing..."
        sudo apt-get update -qq && sudo apt-get install -y -qq socat
    fi

    # Check if K3s kubectl can reach the cluster
    if ! sudo k3s kubectl get ns "$NAMESPACE" &>/dev/null; then
        fail "Cannot reach K3s cluster. Is K3s running?"
    fi

    # Check if KurrentDB service exists
    if ! sudo k3s kubectl get svc "$SERVICE_NAME" -n "$NAMESPACE" &>/dev/null; then
        fail "Service $SERVICE_NAME not found in namespace $NAMESPACE"
    fi
}

# ── Start port forwarding ─────────────────────────────────────────────

start_forwarding() {
    if status_forwarding; then
        ok "Port forwarding is already running"
        show_connection_info
        return 0
    fi

    info "Starting KurrentDB port forwarding via Tailscale..."

    local ts_ip
    ts_ip=$(detect_tailscale_ip)
    ok "Detected Tailscale IP: $ts_ip"

    # Kill any existing port forwards on these ports
    stop_forwarding || true

    info "Starting kubectl port-forward (HTTP)..."
    # Forward HTTP port to local 21130
    sudo k3s kubectl port-forward -n "$NAMESPACE" svc/"$SERVICE_NAME" 21130:2113 > /dev/null 2>&1 &
    local kfp_http_pid=$!
    sleep 1

    # Verify kubectl port-forward started
    if ! kill -0 "$kfp_http_pid" 2>/dev/null; then
        fail "Failed to start kubectl port-forward for HTTP"
    fi
    ok "kubectl port-forward started (HTTP -> 21130)"

    info "Starting kubectl port-forward (TCP)..."
    # Forward TCP port to local 11130
    sudo k3s kubectl port-forward -n "$NAMESPACE" svc/"$SERVICE_NAME" 11130:1113 > /dev/null 2>&1 &
    local kfp_tcp_pid=$!
    sleep 1

    if ! kill -0 "$kfp_tcp_pid" 2>/dev/null; then
        fail "Failed to start kubectl port-forward for TCP"
    fi
    ok "kubectl port-forward started (TCP -> 11130)"

    info "Starting socat listeners on Tailscale interface..."

    # socat bridges Tailscale IP to local port-forward
    # HTTP bridge
    socat TCP-LISTEN:2113,bind="$ts_ip",fork,reuseaddr \
        TCP:127.0.0.1:21130 > "$LOGFILE" 2>&1 &
    local socat_http_pid=$!

    # TCP bridge
    socat TCP-LISTEN:1113,bind="$ts_ip",fork,reuseaddr \
        TCP:127.0.0.1:11130 >> "$LOGFILE" 2>&1 &
    local socat_tcp_pid=$!

    sleep 1

    # Verify socat processes started
    if ! kill -0 "$socat_http_pid" 2>/dev/null || ! kill -0 "$socat_tcp_pid" 2>/dev/null; then
        fail "Failed to start socat listeners"
    fi
    ok "socat listeners started on $ts_ip"

    # Save PIDs
    cat > "$PIDFILE" << EOF
TS_IP=$ts_ip
KFP_HTTP_PID=$kfp_http_pid
KFP_TCP_PID=$kfp_tcp_pid
SOCAT_HTTP_PID=$socat_http_pid
SOCAT_TCP_PID=$socat_tcp_pid
EOF

    ok "Port forwarding started successfully!"
    echo ""
    show_connection_info
}

# ── Stop port forwarding ───────────────────────────────────────────────

stop_forwarding() {
    if [[ ! -f "$PIDFILE" ]]; then
        info "No port forwarding is running (no PID file)"
        return 0
    fi

    info "Stopping KurrentDB port forwarding..."

    source "$PIDFILE"

    # Kill processes, ignoring failures
    for pid in "$KFP_HTTP_PID" "$KFP_TCP_PID" "$SOCAT_HTTP_PID" "$SOCAT_TCP_PID"; do
        if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
        fi
    done

    rm -f "$PIDFILE"
    ok "Port forwarding stopped"
}

# ── Check status ──────────────────────────────────────────────────────

status_forwarding() {
    if [[ ! -f "$PIDFILE" ]]; then
        return 1
    fi

    source "$PIDFILE"

    for pid in "$KFP_HTTP_PID" "$KFP_TCP_PID" "$SOCAT_HTTP_PID" "$SOCAT_TCP_PID"; do
        if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
    done

    return 1
}

show_status() {
    if status_forwarding; then
        source "$PIDFILE"
        ok "Port forwarding is running (Tailscale IP: ${TS_IP:-unknown})"
        echo ""
        echo "PIDs:"
        echo "  kubectl HTTP: $KFP_HTTP_PID"
        echo "  kubectl TCP:  $KFP_TCP_PID"
        echo "  socat HTTP:   $SOCAT_HTTP_PID"
        echo "  socat TCP:    $SOCAT_TCP_PID"
        echo ""
        show_connection_info
    else
        warn "Port forwarding is NOT running"
    fi
}

# ── Show connection info ───────────────────────────────────────────────

show_connection_info() {
    local ts_ip
    ts_ip=$(detect_tailscale_ip)

    echo "=========================================="
    echo " KurrentDB Connection Info"
    echo "=========================================="
    echo ""
    echo "Connect from any machine on Tailscale network:"
    echo ""
    echo "  HTTP (Atom Pub over HTTP):"
    echo "    http://$ts_ip:$KURRENTDB_HTTP_PORT"
    echo ""
    echo "  TCP (KurrentDB native):"
    echo "    $ts_ip:$KURRENTDB_TCP_PORT"
    echo ""
    echo "Example connection string for EventStoreDB client:"
    echo "  esdb://$ts_ip:$KURRENTDB_TCP_PORT?tls=false"
    echo ""
}

# ── Main ───────────────────────────────────────────────────────────────

case "${1:-start}" in
    start)
        check_prereqs
        start_forwarding
        ;;
    stop)
        stop_forwarding
        ;;
    status)
        show_status
        ;;
    logs)
        if [[ -f "$LOGFILE" ]]; then
            echo "=== Last 50 lines of log ==="
            tail -50 "$LOGFILE"
        else
            info "No log file found"
        fi
        ;;
    restart)
        stop_forwarding || true
        check_prereqs
        start_forwarding
        ;;
    *)
        echo "Usage: $0 {start|stop|status|logs|restart}"
        exit 1
        ;;
esac