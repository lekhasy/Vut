#!/usr/bin/env bash
# Platform — K3s startup.
#
# Run after every WSL restart (and after k3s-wipe.sh) to bring the whole
# platform up. Idempotent: every step checks current state first, so it is
# safe to re-run at any time.
#
#   1. Start K3s and wait for the API server + node
#   2. Install / upgrade ArgoCD to the pinned version, enable insecure mode
#   3. Install / upgrade cluster add-ons (Infisical Operator, Reloader)
#   4. Create each app namespace + its Infisical machine-identity secret
#   5. Register AppProjects, the App-of-Apps root and the ArgoCD ingress
#   6. Wait for the platform Applications to become Healthy, then report
#      (without waiting) on each app workload
#
# Usage (as root inside WSL; re-execs under sudo otherwise):
#   ./infrastructure/scripts/k3s-start.sh
#
# Exits non-zero only if a platform Application is not Healthy by the end of
# step 6. Unhealthy app workloads produce a warning, not a failure.

set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
require_root "$@"

# ── Pinned versions (bump deliberately) ─────────────────────────────

ARGOCD_VERSION="v3.4.5"
HELM_VERSION="v3.20.2"
INFISICAL_CHART_VERSION="v0.11.3"
RELOADER_CHART_VERSION="2.2.14"

API_TIMEOUT=120
APPS_TIMEOUT="${APPS_TIMEOUT:-600}"

# ── 1. K3s ──────────────────────────────────────────────────────────

# True once any node reports Ready (stale nodes from old hostnames stay NotReady).
node_ready() {
    kc get nodes -o jsonpath='{range .items[*]}{.status.conditions[?(@.type=="Ready")].status}{"\n"}{end}' 2>/dev/null \
        | grep -qx True
}

start_k3s() {
    if k3s_is_running; then
        ok "K3s is already running"
    elif has_systemd; then
        info "Starting K3s via systemd..."
        systemctl start k3s
    else
        info "Starting K3s (no systemd)..."
        nohup k3s server >/var/log/k3s.log 2>&1 &
    fi

    info "Waiting for K3s API server and node..."
    local deadline=$((SECONDS + API_TIMEOUT))
    until kc get --raw /readyz &>/dev/null && node_ready; do
        ((SECONDS < deadline)) || fail "K3s not ready after ${API_TIMEOUT}s. Check: journalctl -u k3s (or /var/log/k3s.log)"
        sleep 2
    done
    ok "K3s API server and node are ready"
}

# ── 2. ArgoCD ───────────────────────────────────────────────────────

install_argocd() {
    local current
    current=$(kc get deployment argocd-server -n argocd \
        -o jsonpath='{.spec.template.spec.containers[0].image}' 2>/dev/null || true)

    if [[ "$current" == *":$ARGOCD_VERSION" ]]; then
        ok "ArgoCD $ARGOCD_VERSION is installed"
    else
        info "Installing ArgoCD $ARGOCD_VERSION (currently: ${current:-not installed})..."
        kc create namespace argocd --dry-run=client -o yaml | kc apply -f - >/dev/null
        # Server-side apply: the ApplicationSet CRD is too large for client-side apply.
        kc apply --server-side --force-conflicts -n argocd \
            -f "https://raw.githubusercontent.com/argoproj/argo-cd/${ARGOCD_VERSION}/manifests/install.yaml" >/dev/null
        kc wait --for=condition=Established --timeout=60s \
            crd/applications.argoproj.io crd/appprojects.argoproj.io >/dev/null
        ok "ArgoCD $ARGOCD_VERSION applied"
    fi

    # Insecure mode (plain HTTP) so Traefik can terminate in front of it.
    local insecure
    insecure=$(kc get configmap argocd-cmd-params-cm -n argocd \
        -o jsonpath='{.data.server\.insecure}' 2>/dev/null || true)
    if [[ "$insecure" != "true" ]]; then
        info "Enabling ArgoCD insecure mode..."
        kc patch configmap argocd-cmd-params-cm -n argocd --type merge \
            -p '{"data":{"server.insecure":"true"}}' >/dev/null
        kc rollout restart deployment/argocd-server -n argocd >/dev/null
    fi

    info "Waiting for ArgoCD server..."
    kc rollout status deployment/argocd-server -n argocd --timeout=300s >/dev/null
    ok "ArgoCD server is running"
}

# ── 3. Add-ons ──────────────────────────────────────────────────────

ensure_helm() {
    if command -v helm &>/dev/null; then
        return
    fi

    local arch tarball tmp
    case "$(uname -m)" in
        x86_64)  arch=amd64 ;;
        aarch64) arch=arm64 ;;
        *)       fail "Unsupported architecture for Helm install: $(uname -m)" ;;
    esac
    tarball="helm-${HELM_VERSION}-linux-${arch}.tar.gz"
    tmp=$(mktemp -d)

    info "Installing Helm $HELM_VERSION..."
    curl -fsSL -o "$tmp/$tarball"        "https://get.helm.sh/$tarball"
    curl -fsSL -o "$tmp/$tarball.sha256" "https://get.helm.sh/$tarball.sha256sum"
    (cd "$tmp" && sha256sum -c "$tarball.sha256" >/dev/null) || fail "Helm checksum mismatch"
    tar -xzf "$tmp/$tarball" -C "$tmp"
    install -m 0755 "$tmp/linux-${arch}/helm" /usr/local/bin/helm
    rm -rf "$tmp"
    ok "Helm $HELM_VERSION installed"
}

# Install a chart, or upgrade it when the deployed version differs from the pin.
ensure_helm_release() {
    local release="$1" ns="$2" repo="$3" repo_url="$4" chart="$5" version="$6"
    local current
    current=$(helm get metadata "$release" -n "$ns" -o json 2>/dev/null \
        | sed -n 's/.*"version":"\([^"]*\)".*/\1/p' || true)

    if [[ "$current" == "$version" ]]; then
        ok "$release $version is installed"
        return
    fi

    info "Installing $release $version (currently: ${current:-not installed})..."
    helm repo add "$repo" "$repo_url" --force-update >/dev/null
    helm upgrade --install "$release" "$repo/$chart" \
        --version "$version" --namespace "$ns" --create-namespace \
        --wait --timeout 180s >/dev/null
    ok "$release $version installed"
}

install_addons() {
    ensure_helm
    ensure_helm_release infisical-secrets-operator infisical \
        infisical https://dl.cloudsmith.io/public/infisical/helm-charts/helm/charts/ \
        secrets-operator "$INFISICAL_CHART_VERSION"
    ensure_helm_release reloader reloader \
        stakater https://stakater.github.io/stakater-charts \
        reloader "$RELOADER_CHART_VERSION"
}

# ── 4. App namespaces + Infisical machine identities ────────────────

# Each leaf Application's destination namespace gets its own machine identity,
# scoped to that app's Infisical project. Namespaces are created up front so
# the secret exists before ArgoCD's first sync.
ensure_machine_identity() {
    local ns="$1"

    kc create namespace "$ns" --dry-run=client -o yaml | kc apply -f - >/dev/null

    if kc get secret infisical-machine-identity -n "$ns" &>/dev/null; then
        ok "Infisical machine identity present in '$ns'"
        return
    fi
    if [[ ! -t 0 ]]; then
        warn "No infisical-machine-identity in '$ns' and stdin is not a terminal — skipping."
        return
    fi

    local client_id client_secret
    warn "No infisical-machine-identity secret in '$ns'. Enter the credentials for its Infisical project."
    read -rp  "  Client ID for '$ns': " client_id
    read -rsp "  Client Secret for '$ns': " client_secret
    echo

    if [[ -z "$client_id" || -z "$client_secret" ]]; then
        warn "Skipped — Infisical secrets will not sync in '$ns'."
        return
    fi
    kc create secret generic infisical-machine-identity -n "$ns" \
        --from-literal=clientId="$client_id" \
        --from-literal=clientSecret="$client_secret" >/dev/null
    ok "Infisical machine identity created in '$ns'"
}

setup_app_namespaces() {
    local file ns
    for file in "${LEAF_APPS[@]}"; do
        ns=$(app_namespace "$file")
        [[ -n "$ns" ]] || fail "No spec.destination.namespace in $file"
        ensure_machine_identity "$ns"
    done
}

# ── 5. Register ArgoCD config ───────────────────────────────────────

register_argocd_apps() {
    local manifest
    # AppProjects first so the root and leaf Applications can reference them.
    for manifest in appproject-platform.yaml appproject-apps.yaml root-app.yaml ingress.yaml; do
        [[ -f "$ARGOCD_DIR/$manifest" ]] || fail "Missing $ARGOCD_DIR/$manifest"
        kc apply -f "$ARGOCD_DIR/$manifest" >/dev/null
    done
    ok "AppProjects, App-of-Apps root and ArgoCD ingress applied"
}

# ── 6. Application health ───────────────────────────────────────────

# Prints "<name>=<health>" for each named Application ("Missing" if absent).
app_health() {
    local statuses app health
    statuses=$(kc get applications -n argocd \
        -o jsonpath='{range .items[*]}{.metadata.name}={.status.health.status}{"\n"}{end}' 2>/dev/null || true)
    for app in "$@"; do
        health=$(sed -n "s/^${app}=//p" <<<"$statuses")
        echo "$app=${health:-Missing}"
    done
}

# Blocks until the platform's own Applications (project "platform") are
# Healthy. App workloads are deliberately not waited on — one app being down
# must not hold up the platform boot.
wait_for_platform_apps() {
    local apps=("$(app_name "$ARGOCD_DIR/root-app.yaml")") file
    for file in "${LEAF_APPS[@]}"; do
        if [[ "$(app_project "$file")" == "platform" ]]; then
            apps+=("$(app_name "$file")")
        fi
    done

    info "Waiting for platform Applications to become Healthy (up to ${APPS_TIMEOUT}s): ${apps[*]}"
    local deadline=$((SECONDS + APPS_TIMEOUT)) pending
    while true; do
        mapfile -t pending < <(app_health "${apps[@]}" | grep -v '=Healthy$' || true)

        if ((${#pending[@]} == 0)); then
            ok "Platform Applications are Healthy"
            return 0
        fi
        if ((SECONDS >= deadline)); then
            warn "Platform not Healthy after ${APPS_TIMEOUT}s: ${pending[*]}"
            return 1
        fi
        echo "  [WAIT] ${pending[*]}"
        sleep 10
    done
}

# One-shot health report for app workloads (project "apps"); never blocks.
report_app_health() {
    local apps=() file status
    for file in "${LEAF_APPS[@]}"; do
        [[ "$(app_project "$file")" == "platform" ]] || apps+=("$(app_name "$file")")
    done
    ((${#apps[@]} > 0)) || return 0

    while read -r status; do
        if [[ "$status" == *=Healthy ]]; then
            ok "App ${status%%=*} is Healthy"
        else
            warn "App ${status%%=*} is ${status#*=} (not waiting — check ArgoCD)"
        fi
    done < <(app_health "${apps[@]}")
}

# ── Summary ─────────────────────────────────────────────────────────

print_summary() {
    local file ns

    header "ArgoCD Applications"
    kc get applications -n argocd

    for file in "${LEAF_APPS[@]}"; do
        ns=$(app_namespace "$file")
        header "Pods — $ns"
        kc get pods -n "$ns" -o wide
    done

    header "Access"
    cat <<'EOF'
Via Cloudflare Tunnel (if configured):
  https://velucid.app
  https://argo.velucid.app     (ArgoCD UI)
  https://grafana.velucid.app  (Grafana)

ArgoCD admin password:
  sudo k3s kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d && echo

Debug an unhealthy app:
  sudo k3s kubectl get applications -n argocd
  sudo k3s kubectl describe application <name> -n argocd
  sudo k3s kubectl logs -n <namespace> <pod>

Port-forward examples:
  sudo k3s kubectl port-forward -n velucid svc/velucid-frontend 3000:3000
  sudo k3s kubectl port-forward -n velucid svc/velucid-silo 5000:5000
  sudo k3s kubectl port-forward -n platform-observability svc/platform-grafana 3001:3000
  sudo k3s kubectl port-forward -n platform-observability svc/platform-prometheus 9090:9090
EOF
}

# ── Main ────────────────────────────────────────────────────────────

main() {
    header "Platform — K3s Startup"

    mapfile -t LEAF_APPS < <(leaf_app_files)
    ((${#LEAF_APPS[@]} > 0)) || fail "No leaf Applications found in $ARGOCD_DIR/apps/"

    start_k3s
    install_argocd
    install_addons
    setup_app_namespaces
    register_argocd_apps

    local healthy=0
    wait_for_platform_apps || healthy=1
    report_app_health
    print_summary

    if ((healthy != 0)); then
        warn "Platform Applications are not Healthy (see above)."
        exit 1
    fi
    header "Platform is running!"
}

main "$@"
