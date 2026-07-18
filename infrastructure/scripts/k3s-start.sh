#!/usr/bin/env bash
# Platform - K3s Startup Script (WSL Production)
#
# Run this after every WSL restart to bring up the full production stack.
# Idempotent — safe to run multiple times.
#
# What it does:
#   1. Starts K3s (if not already running)
#   2. Waits for the K3s API server to be ready
#   3. Verifies ArgoCD is running + registers App-of-Apps root
#   4. Ensures Infisical Operator + Stakater Reloader are installed
#   5. Prompts for per-namespace Infisical machine identities (one per app)
#   6. Waits for platform-observability + app pods to be healthy
#   7. Shows status and access points
#
# Usage:
#   ./scripts/k3s-start.sh
#
# First-time setup: see docs/new-machine-setup.md

# ── Helpers ──────────────────────────────────────────────────────────

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

info()  { echo -e "${BLUE}[INFO]${NC}  $*"; }
ok()    { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
fail()  { echo -e "${RED}[FAIL]${NC}  $*"; exit 1; }

# Use K3s-bundled kubectl so there's no dependency on a separate install
KUBECTL="sudo k3s kubectl"

# ── 1. Start K3s ────────────────────────────────────────────────────

echo ""
echo "=========================================="
echo " Velucid Platform — K3s Startup"
echo "=========================================="
echo ""

if command -v systemctl &>/dev/null && systemctl is-system-running &>/dev/null 2>&1; then
    # systemd is available (WSL2 with systemd enabled)
    if systemctl is-active --quiet k3s 2>/dev/null; then
        ok "K3s is already running (systemd)"
    else
        info "Starting K3s via systemd..."
        sudo systemctl start k3s
        ok "K3s started"
    fi
else
    # No systemd — check if K3s server process is running
    if pgrep -x "k3s-server" > /dev/null 2>&1 || pgrep -f "k3s server" > /dev/null 2>&1; then
        ok "K3s is already running"
    else
        info "Starting K3s (no systemd)..."
        sudo nohup k3s server > /var/log/k3s.log 2>&1 &
        ok "K3s started (PID $!)"
    fi
fi

# ── 2. Wait for K3s API ─────────────────────────────────────────────

info "Waiting for K3s API server..."

MAX_WAIT=60
ELAPSED=0
while [[ $ELAPSED -lt $MAX_WAIT ]]; do
    if $KUBECTL get nodes &>/dev/null; then
        ok "K3s API server is ready"
        break
    fi
    sleep 2
    ELAPSED=$((ELAPSED + 2))
done

if [[ $ELAPSED -ge $MAX_WAIT ]]; then
    fail "K3s API server did not become ready within ${MAX_WAIT}s. Check: sudo journalctl -u k3s"
fi

echo ""

# ── 3. Ensure ArgoCD is installed and running ───────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"

info "Checking ArgoCD..."

if ! $KUBECTL get namespace argocd &>/dev/null; then
    info "ArgoCD not found — installing..."
    $KUBECTL create namespace argocd
    # Use --server-side to avoid "Too long" error on large CRDs (ApplicationSets)
    $KUBECTL apply --server-side -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
    ok "ArgoCD installed"

    info "Waiting for ArgoCD server to be ready (first install takes ~90s)..."
    $KUBECTL wait --for=condition=available deployment/argocd-server -n argocd --timeout=180s || true

    # Register AppProject CRs (restrict source repos + destination namespaces).
    # These are applied first so leaf Applications can reference them.
    if [[ -f "$INFRA_DIR/k8s/argocd/appproject-platform.yaml" ]]; then
        $KUBECTL apply -f "$INFRA_DIR/k8s/argocd/appproject-platform.yaml"
        ok "AppProject 'platform' registered"
    fi
    if [[ -f "$INFRA_DIR/k8s/argocd/appproject-apps.yaml" ]]; then
        $KUBECTL apply -f "$INFRA_DIR/k8s/argocd/appproject-apps.yaml"
        ok "AppProject 'apps' registered"
    fi

    # Register the App-of-Apps root. This watches infrastructure/k8s/argocd/apps/
    # and recursively applies each leaf Application (platform-observability, velucid, ...).
    if [[ -f "$INFRA_DIR/k8s/argocd/root-app.yaml" ]]; then
        $KUBECTL apply -f "$INFRA_DIR/k8s/argocd/root-app.yaml"
        ok "App-of-Apps root registered"
    else
        warn "App-of-Apps root not found at $INFRA_DIR/k8s/argocd/root-app.yaml"
    fi
fi

# Wait for ArgoCD server to be available
ARGO_READY=false
for i in $(seq 1 30); do
    if $KUBECTL get deployment argocd-server -n argocd &>/dev/null; then
        AVAILABLE=$($KUBECTL get deployment argocd-server -n argocd -o jsonpath='{.status.availableReplicas}' 2>/dev/null || echo "0")
        if [[ "${AVAILABLE:-0}" -ge 1 ]]; then
            ok "ArgoCD server is running"
            ARGO_READY=true
            break
        fi
    fi
    sleep 5
done

if ! $ARGO_READY; then
    warn "ArgoCD server not ready yet — it may still be starting. Pods will sync once it's up."
fi

# Ensure ArgoCD runs in insecure mode (HTTP) so Traefik ingress can proxy
CURRENT_INSECURE=$($KUBECTL get configmap argocd-cmd-params-cm -n argocd -o jsonpath='{.data.server\.insecure}' 2>/dev/null || echo "")
if [[ "$CURRENT_INSECURE" != "true" ]]; then
    info "Enabling ArgoCD insecure mode (HTTP for Traefik ingress)..."
    $KUBECTL patch configmap argocd-cmd-params-cm -n argocd \
        --type merge -p '{"data":{"server.insecure":"true"}}'
    $KUBECTL rollout restart deployment/argocd-server -n argocd
    $KUBECTL wait --for=condition=available deployment/argocd-server -n argocd --timeout=120s || true
    ok "ArgoCD patched for insecure mode"
fi

# Apply ArgoCD ingress (argo.velucid.app)
if [[ -f "$INFRA_DIR/k8s/argocd/ingress.yaml" ]]; then
    $KUBECTL apply -f "$INFRA_DIR/k8s/argocd/ingress.yaml"
    ok "ArgoCD ingress applied (argo.velucid.app)"
fi

echo ""

# ── 4. Ensure Infisical Operator is installed ───────────────────────

info "Checking Infisical Operator..."

if ! $KUBECTL get crd infisicalsecrets.secrets.infisical.com &>/dev/null; then
    info "Infisical Operator not found — installing via Helm..."

    # Install Helm if not available
    if ! command -v helm &>/dev/null; then
        info "Installing Helm..."
        curl -fsSL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
    fi

    helm repo add infisical https://dl.cloudsmith.io/public/infisical/helm-charts/helm/charts/ 2>/dev/null || true
    helm repo update infisical

    helm upgrade --install infisical-secrets-operator infisical/secrets-operator \
        --namespace infisical \
        --create-namespace \
        --wait --timeout 120s

    ok "Infisical Operator installed"
else
    ok "Infisical Operator is installed"
fi

# Ensure Infisical machine identity secrets exist in each app namespace.
# Each app has its own Infisical project, so each namespace needs its own
# machine identity credentials.
ensure_machine_identity() {
    local ns="$1"
    local label="$2"
    if ! $KUBECTL get namespace "$ns" &>/dev/null; then
        return
    fi
    if $KUBECTL get secret infisical-machine-identity -n "$ns" &>/dev/null; then
        return
    fi
    warn "Infisical machine identity secret not found in '$ns' namespace ($label)."
    echo ""
    read -p "  Client ID for $label: " CLIENT_ID
    read -p "  Client Secret for $label: " CLIENT_SECRET
    if [ -n "$CLIENT_ID" ] && [ -n "$CLIENT_SECRET" ]; then
        $KUBECTL create secret generic infisical-machine-identity \
            -n "$ns" \
            --from-literal=clientId="$CLIENT_ID" \
            --from-literal=clientSecret="$CLIENT_SECRET" \
            2>/dev/null && ok "Infisical machine identity secret created in '$ns'" \
            || warn "Failed to create infisical-machine-identity secret in '$ns'"
    else
        warn "Skipped — Infisical secrets will not be synced for '$ns'"
    fi
}

ensure_machine_identity "platform-observability" "platform Infisical project"
ensure_machine_identity "velucid"               "velucid Infisical project"

# ── 5. Ensure Stakater Reloader is installed ────────────────────────

info "Checking Stakater Reloader..."

if ! $KUBECTL get deployment reloader-reloader -n reloader &>/dev/null 2>&1; then
    info "Reloader not found — installing via Helm..."

    if ! command -v helm &>/dev/null; then
        info "Installing Helm..."
        curl -fsSL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
    fi

    helm repo add stakater https://stakater.github.io/stakater-charts 2>/dev/null || true
    helm repo update stakater

    helm upgrade --install reloader stakater/reloader \
        --namespace reloader \
        --create-namespace \
        --wait --timeout 120s

    ok "Stakater Reloader installed"
else
    ok "Stakater Reloader is installed"
fi

echo ""

# ── 6. Wait for platform + app pods ─────────────────────────────────

# Wait for both platform-observability and velucid namespaces to sync.
# Skip a namespace if it doesn't exist yet (ArgoCD creates it on first sync).
WAIT_NAMESPACES=()
for ns in platform-observability velucid; do
    if $KUBECTL get namespace "$ns" &>/dev/null; then
        WAIT_NAMESPACES+=("$ns")
    else
        warn "Namespace '$ns' does not exist yet — ArgoCD will create it on first sync."
    fi
done

if [[ ${#WAIT_NAMESPACES[@]} -eq 0 ]]; then
    echo ""
    echo "If this is a fresh setup, ArgoCD needs a moment to sync manifests from git."
    echo "Run this script again in a minute, or check ArgoCD status:"
    echo "  sudo k3s kubectl get applications -n argocd"
    echo ""
    exit 0
fi

# Wait for pods to be ready (up to 5 minutes — images may need pulling)
MAX_WAIT=300
ELAPSED=0
INTERVAL=10

while [[ $ELAPSED -lt $MAX_WAIT ]]; do
    TOTAL=0
    READY=0
    for ns in "${WAIT_NAMESPACES[@]}"; do
        NS_TOTAL=$($KUBECTL get pods -n "$ns" --no-headers 2>/dev/null | wc -l || echo "0")
        NS_READY=$($KUBECTL get pods -n "$ns" --no-headers 2>/dev/null | grep -c "Running" || echo "0")
        TOTAL=$((TOTAL + NS_TOTAL))
        READY=$((READY + NS_READY))
    done

    if [[ "$TOTAL" -gt 0 && "$TOTAL" -eq "$READY" ]]; then
        ok "All $READY/$TOTAL pods are running across ${#WAIT_NAMESPACES[@]} namespace(s)"
        break
    fi

    echo -e "  [WAIT] $READY/$TOTAL pods running (${ELAPSED}s elapsed)..."
    sleep "$INTERVAL"
    ELAPSED=$((ELAPSED + INTERVAL))
done

if [[ "$TOTAL" -eq 0 || "$TOTAL" -ne "$READY" ]]; then
    warn "Not all pods are ready after ${MAX_WAIT}s. Current status:"
    for ns in "${WAIT_NAMESPACES[@]}"; do
        $KUBECTL get pods -n "$ns" -o wide
        echo ""
    done
    echo "Check logs: sudo k3s kubectl logs -n <namespace> <pod-name>"
fi

echo ""

# ── 7. Show status ──────────────────────────────────────────────────

echo "=========================================="
echo " Pod Status"
echo "=========================================="
echo ""
$KUBECTL get pods -n platform-observability -o wide 2>/dev/null || echo "  (platform-observability not yet synced)"
echo ""
$KUBECTL get pods -n velucid -o wide 2>/dev/null || echo "  (velucid not yet synced)"
echo ""

echo "=========================================="
echo " Services"
echo "=========================================="
echo ""
$KUBECTL get svc -n platform-observability 2>/dev/null || echo "  (platform-observability not yet synced)"
echo ""
$KUBECTL get svc -n velucid 2>/dev/null || echo "  (velucid not yet synced)"
echo ""

echo "=========================================="
echo " Platform is running!"
echo "=========================================="
echo ""
echo "Access via Cloudflare Tunnel (if configured):"
echo "  https://velucid.app"
echo "  https://argo.velucid.app     (ArgoCD UI)"
echo "  https://grafana.velucid.app  (Grafana)"
echo ""
echo "ArgoCD admin password:"
echo "  sudo k3s kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d && echo"
echo ""
echo "Inspect apps:"
echo "  sudo k3s kubectl get applications -n argocd"
echo "  sudo k3s kubectl get pods -A"
echo ""
echo "Port-forward examples (Velucid):"
echo "  sudo k3s kubectl port-forward -n velucid svc/velucid-frontend 3000:3000 &"
echo "  sudo k3s kubectl port-forward -n velucid svc/velucid-silo 5000:5000 &"
echo "  sudo k3s kubectl port-forward -n velucid svc/velucid-kurrentdb 2113:2113 &"
echo ""
echo "Port-forward examples (Observability):"
echo "  sudo k3s kubectl port-forward -n platform-observability svc/platform-grafana 3000:3000 &"
echo "  sudo k3s kubectl port-forward -n platform-observability svc/platform-prometheus 9090:9090 &"
echo ""
