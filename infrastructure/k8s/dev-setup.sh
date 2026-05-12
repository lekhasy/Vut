#!/usr/bin/env bash
# Velucid Platform - K8s Development Setup Script (K3s)
#
# Applies all manifests to a local K3s cluster and waits for
# all pods to become Ready.
#
# Prerequisites:
#   - K3s installed: curl -sfL https://get.k3s.io | sh -
#   - kubectl configured (K3s sets this up automatically)
#   - Images pre-loaded into K3s (sudo k3s ctr images import ...)
#
# Usage:
#   ./k8s/dev-setup.sh
#   ./k8s/dev-setup.sh --cleanup   # Delete everything first

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
K8S_DIR="$SCRIPT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

info()  { echo -e "${BLUE}[INFO]${NC}  $*"; }
ok()    { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
fail()  { echo -e "${RED}[FAIL]${NC}  $*"; }

# Cleanup if requested
if [[ "${1:-}" == "--cleanup" ]]; then
    info "Cleaning up existing Velucid resources..."
    kubectl delete namespace velucid --ignore-not-found --wait=true --timeout=60s
    info "Namespace 'velucid' deleted (or did not exist)."
    echo ""
fi

# 1. Create namespace
info "Creating namespace..."
kubectl apply -f "$K8S_DIR/namespace.yaml"
ok "Namespace 'velucid' created."

# 2. Create secrets
info "Creating secrets..."
kubectl apply -f "$K8S_DIR/secrets/"
ok "Secrets applied."

# 3. Create PostgreSQL
info "Creating PostgreSQL..."
kubectl apply -f "$K8S_DIR/postgresql/"
ok "PostgreSQL StatefulSet and Service applied."

# 4. Create KurrentDB
info "Creating KurrentDB..."
kubectl apply -f "$K8S_DIR/kurrentdb/"
ok "KurrentDB StatefulSet and Service applied."

# 5. Wait for infrastructure services to be ready
info "Waiting for infrastructure pods to be ready..."
echo ""

MAX_WAIT=180  # 3 minutes
ELAPSED=0
INTERVAL=5

check_pod_ready() {
    local label="$1"
    local count
    count=$(kubectl get pods -n velucid -l "$label" --no-headers 2>/dev/null | grep -c "Running" || echo "0")
    echo "$count"
}

while [[ $ELAPSED -lt $MAX_WAIT ]]; do
    all_ready=true

    # Check PostgreSQL
    pg_ready=$(check_pod_ready "app.kubernetes.io/name=postgresql")
    if [[ "$pg_ready" -ge 1 ]]; then
        ok "PostgreSQL: $pg_ready pod(s) running"
    else
        echo -e "  [WAIT] PostgreSQL: starting..."
        all_ready=false
    fi

    # Check KurrentDB
    kdb_ready=$(check_pod_ready "app.kubernetes.io/name=kurrentdb")
    if [[ "$kdb_ready" -ge 1 ]]; then
        ok "KurrentDB: $kdb_ready pod(s) running"
    else
        echo -e "  [WAIT] KurrentDB: starting..."
        all_ready=false
    fi

    if $all_ready; then
        echo ""
        ok "All infrastructure services are running!"
        break
    fi

    sleep "$INTERVAL"
    ELAPSED=$((ELAPSED + INTERVAL))
    echo ""
done

if ! $all_ready; then
    fail "Infrastructure services did not become ready within ${MAX_WAIT}s."
    echo ""
    fail "Diagnostic info:"
    kubectl get pods -n velucid
    echo ""
    exit 1
fi

# 6. Create application services (Silo + Projector + Frontend)
info "Creating application services..."
kubectl apply -f "$K8S_DIR/silo/"
kubectl apply -f "$K8S_DIR/projector-service/"
kubectl apply -f "$K8S_DIR/frontend/"
ok "Silo, Projector Service, and Frontend deployments applied."

# 7. Create Ingress
info "Creating Ingress..."
kubectl apply -f "$K8S_DIR/ingress.yaml"
ok "Traefik Ingress applied."

# 8. Create Cloudflare Tunnel (optional — skip if secrets not configured)
if kubectl get secret -n velucid cloudflared-tunnel-credentials &>/dev/null 2>&1; then
    info "Creating Cloudflare Tunnel..."
    kubectl apply -f "$K8S_DIR/cloudflared/"
    ok "Cloudflared deployment applied."
else
    info "Deploying Cloudflare Tunnel manifests (credentials may need updating)..."
    kubectl apply -f "$K8S_DIR/cloudflared/" 2>/dev/null || warn "Cloudflared skipped — configure secret first."
fi

# 9. Final health check
echo ""
info "Final health check..."
echo ""

# Wait a moment for deployments to register
sleep 5

echo "--- Pod Status ---"
kubectl get pods -n velucid -o wide
echo ""

echo "--- Service Status ---"
kubectl get svc -n velucid
echo ""

# Check specific connectivity
info "Connectivity verification:"

# KurrentDB
if kubectl exec -n velucid velucid-kurrentdb-0 -- curl -sf http://localhost:2113/health/live > /dev/null 2>&1; then
    ok "KurrentDB: http://velucid-kurrentdb:2113/health/live"
else
    warn "KurrentDB: not yet reachable (may still be starting)"
fi

# PostgreSQL
if kubectl exec -n velucid velucid-postgresql-0 -- pg_isready -U velucid_app -d velucid_readmodel > /dev/null 2>&1; then
    ok "PostgreSQL: velucid-postgresql:5432/velucid_readmodel"
else
    warn "PostgreSQL: not yet reachable (may still be starting)"
fi

echo ""
echo "=========================================="
echo " Velucid Platform K3s setup complete!"
echo "=========================================="
echo ""
echo "Access points:"
echo ""
echo "  Via Cloudflare Tunnel (if configured):"
echo "    https://velucid.app"
echo ""
echo "  Via port-forward (local dev):"
echo "    kubectl port-forward -n velucid svc/velucid-frontend 3000:3000"
echo "    kubectl port-forward -n velucid svc/velucid-silo 5000:5000"
echo "    kubectl port-forward -n velucid svc/velucid-kurrentdb 2113:2113"
echo ""
echo "View logs:"
echo "  kubectl logs -f -n velucid -l app.kubernetes.io/component=eventstore"
echo "  kubectl logs -f -n velucid -l app.kubernetes.io/component=database"
echo "  kubectl logs -f -n velucid -l app.kubernetes.io/component=backend"
echo "  kubectl logs -f -n velucid -l app.kubernetes.io/component=frontend"
echo "  kubectl logs -f -n velucid -l app.kubernetes.io/component=tunnel"
echo ""
