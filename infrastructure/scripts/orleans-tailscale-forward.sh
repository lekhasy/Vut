#!/usr/bin/env bash
# Velucid Platform - Orleans Cluster Port Forward via Tailscale
#
# Exposes Orleans cluster ports to other machines on the Tailscale network.
# Run this script on the WSL machine where K3s is running.
#
# What it does:
#   1. Detects the Tailscale IP address in WSL
#   2. Starts kubectl port-forward to Orleans Silo service (local side)
#   3. Uses socat to bridge Tailscale interface to the local port-forward
#   4. Outputs the connection strings for external clients
#
# Usage:
#   ./orleans-tailscale-forward.sh start   # Start port forwarding
#   ./orleans-tailscale-forward.sh stop    # Stop port forwarding
#   ./orleans-tailscale-forward.sh status  # Check if running
#   ./orleans-tailscale-forward.sh logs    # View socat logs
#
# Connection from other Tailscale machines:
#   Gateway:  100.99.15.109:30000
#   Silo:     100.99.15.109:11111
#   Dashboard: http://100.99.15.109:8888

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

# Orleans Silo ports
ORLEANS_HTTP_PORT=5000
ORLEANS_SILO_PORT=11111
ORLEANS_GATEWAY_PORT=30000
ORLEANS_DASHBOARD_PORT=8888

NAMESPACE=velucid
DEPLOYMENT_NAME=velucid-silo
PIDFILE="/tmp/orleans-forward.pid"
LOGFILE="/tmp/orleans-forward.log"

# Local port offsets to avoid conflicts with kurrentdb
LOCAL_HTTP_PORT=25000
LOCAL_SILO_PORT=21111
LOCAL_GATEWAY_PORT=23000
LOCAL_DASHBOARD_PORT=28888

# ── Detect Tailscale IP ───────────────────────────────────────────────

detect_tailscale_ip() {
    local ts_ip=""
    if command -v tailscale &>/dev/null; then
        ts_ip=$(tailscale status --self --json 2>/dev/null | grep -o '"Self":{[^}]*}' | grep -o '"DNSName":"[^"]*"' | cut -d'"' -f4 | cut -d'.' -f1)
        if [[ -z "$ts_ip" ]]; then
            ts_ip=$(ip -4 addr show tailscale0 2>/dev/null | grep -oP 'inet \K[\d.]+' || true)
        fi
        if [[ -z "$ts_ip" ]]; then
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

    if ! sudo k3s kubectl get ns "$NAMESPACE" &>/dev/null; then
        fail "Cannot reach K3s cluster. Is K3s running?"
    fi

    if ! sudo k3s kubectl get deployment "$DEPLOYMENT_NAME" -n "$NAMESPACE" &>/dev/null; then
        fail "Deployment $DEPLOYMENT_NAME not found in namespace $NAMESPACE"
    fi
}

# ── Start port forwarding ─────────────────────────────────────────────

start_forwarding() {
    if status_forwarding; then
        ok "Port forwarding is already running"
        show_connection_info
        return 0
    fi

    info "Starting Orleans cluster port forwarding via Tailscale..."

    local ts_ip
    ts_ip=$(detect_tailscale_ip)
    ok "Detected Tailscale IP: $ts_ip"

    # Kill any existing port forwards on these ports
    stop_forwarding || true

    info "Starting kubectl port-forward (Gateway)..."
    sudo k3s kubectl port-forward -n "$NAMESPACE" deployment/"$DEPLOYMENT_NAME" "$LOCAL_GATEWAY_PORT:$ORLEANS_GATEWAY_PORT" > /dev/null 2>&1 &
    local kfp_gateway_pid=$!
    sleep 1

    if ! kill -0 "$kfp_gateway_pid" 2>/dev/null; then
        fail "Failed to start kubectl port-forward for Gateway"
    fi
    ok "kubectl port-forward started (Gateway -> $LOCAL_GATEWAY_PORT)"

    info "Starting kubectl port-forward (Silo)..."
    sudo k3s kubectl port-forward -n "$NAMESPACE" deployment/"$DEPLOYMENT_NAME" "$LOCAL_SILO_PORT:$ORLEANS_SILO_PORT" > /dev/null 2>&1 &
    local kfp_silo_pid=$!
    sleep 1

    if ! kill -0 "$kfp_silo_pid" 2>/dev/null; then
        fail "Failed to start kubectl port-forward for Silo"
    fi
    ok "kubectl port-forward started (Silo -> $LOCAL_SILO_PORT)"

    info "Starting kubectl port-forward (Dashboard)..."
    sudo k3s kubectl port-forward -n "$NAMESPACE" deployment/"$DEPLOYMENT_NAME" "$LOCAL_DASHBOARD_PORT:$ORLEANS_DASHBOARD_PORT" > /dev/null 2>&1 &
    local kfp_dashboard_pid=$!
    sleep 1

    if ! kill -0 "$kfp_dashboard_pid" 2>/dev/null; then
        fail "Failed to start kubectl port-forward for Dashboard"
    fi
    ok "kubectl port-forward started (Dashboard -> $LOCAL_DASHBOARD_PORT)"

    info "Starting kubectl port-forward (HTTP)..."
    sudo k3s kubectl port-forward -n "$NAMESPACE" deployment/"$DEPLOYMENT_NAME" "$LOCAL_HTTP_PORT:$ORLEANS_HTTP_PORT" > /dev/null 2>&1 &
    local kfp_http_pid=$!
    sleep 1

    if ! kill -0 "$kfp_http_pid" 2>/dev/null; then
        fail "Failed to start kubectl port-forward for HTTP"
    fi
    ok "kubectl port-forward started (HTTP -> $LOCAL_HTTP_PORT)"

    info "Starting socat listeners on Tailscale interface..."

    # Gateway bridge
    socat TCP-LISTEN:$ORLEANS_GATEWAY_PORT,bind="$ts_ip",fork,reuseaddr \
        TCP:127.0.0.1:$LOCAL_GATEWAY_PORT >> "$LOGFILE" 2>&1 &
    local socat_gateway_pid=$!

    # Silo bridge
    socat TCP-LISTEN:$ORLEANS_SILO_PORT,bind="$ts_ip",fork,reuseaddr \
        TCP:127.0.0.1:$LOCAL_SILO_PORT >> "$LOGFILE" 2>&1 &
    local socat_silo_pid=$!

    # Dashboard bridge
    socat TCP-LISTEN:$ORLEANS_DASHBOARD_PORT,bind="$ts_ip",fork,reuseaddr \
        TCP:127.0.0.1:$LOCAL_DASHBOARD_PORT >> "$LOGFILE" 2>&1 &
    local socat_dashboard_pid=$!

    sleep 1

    # Verify socat processes started
    if ! kill -0 "$socat_gateway_pid" 2>/dev/null || \
       ! kill -0 "$socat_silo_pid" 2>/dev/null || \
       ! kill -0 "$socat_dashboard_pid" 2>/dev/null; then
        fail "Failed to start socat listeners"
    fi
    ok "socat listeners started on $ts_ip"

    # Save PIDs
    cat > "$PIDFILE" << EOF
TS_IP=$ts_ip
KFP_GATEWAY_PID=$kfp_gateway_pid
KFP_SILO_PID=$kfp_silo_pid
KFP_DASHBOARD_PID=$kfp_dashboard_pid
KFP_HTTP_PID=$kfp_http_pid
SOCAT_GATEWAY_PID=$socat_gateway_pid
SOCAT_SILO_PID=$socat_silo_pid
SOCAT_DASHBOARD_PID=$socat_dashboard_pid
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

    info "Stopping Orleans cluster port forwarding..."

    source "$PIDFILE"

    for pid in "$KFP_GATEWAY_PID" "$KFP_SILO_PID" "$KFP_DASHBOARD_PID" "$KFP_HTTP_PID" \
              "$SOCAT_GATEWAY_PID" "$SOCAT_SILO_PID" "$SOCAT_DASHBOARD_PID"; do
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

    for pid in "$KFP_GATEWAY_PID" "$KFP_SILO_PID" "$KFP_DASHBOARD_PID" "$KFP_HTTP_PID" \
              "$SOCAT_GATEWAY_PID" "$SOCAT_SILO_PID" "$SOCAT_DASHBOARD_PID"; do
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
        echo "  kubectl Gateway:   $KFP_GATEWAY_PID"
        echo "  kubectl Silo:      $KFP_SILO_PID"
        echo "  kubectl Dashboard: $KFP_DASHBOARD_PID"
        echo "  kubectl HTTP:      $KFP_HTTP_PID"
        echo "  socat Gateway:     $SOCAT_GATEWAY_PID"
        echo "  socat Silo:        $SOCAT_SILO_PID"
        echo "  socat Dashboard:   $SOCAT_DASHBOARD_PID"
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
    echo " Orleans Cluster Connection Info"
    echo "=========================================="
    echo ""
    echo "Connect from any machine on Tailscale network:"
    echo ""
    echo "  Orleans Dashboard:"
    echo "    http://$ts_ip:$ORLEANS_DASHBOARD_PORT"
    echo ""
    echo "  Gateway (client connection):"
    echo "    $ts_ip:$ORLEANS_GATEWAY_PORT"
    echo ""
    echo "  Silo (intra-cluster):"
    echo "    $ts_ip:$ORLEANS_SILO_PORT"
    echo ""
    echo "  HTTP (health/readiness):"
    echo "    http://$ts_ip:$LOCAL_HTTP_PORT"
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