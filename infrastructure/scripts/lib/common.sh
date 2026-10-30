# Shared helpers for the k3s-* scripts. Source this file; don't execute it.

# ── Paths ───────────────────────────────────────────────────────────

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INFRA_DIR="$(dirname "$SCRIPTS_DIR")"
ARGOCD_DIR="$INFRA_DIR/k8s/argocd"

K3S_DATA_DIR="/var/lib/rancher/k3s"
export KUBECONFIG="/etc/rancher/k3s/k3s.yaml"

# ── Logging ─────────────────────────────────────────────────────────

if [[ -t 1 ]]; then
    RED=$'\033[0;31m' GREEN=$'\033[0;32m' YELLOW=$'\033[1;33m' BLUE=$'\033[0;34m' NC=$'\033[0m'
else
    RED='' GREEN='' YELLOW='' BLUE='' NC=''
fi

info()   { echo "${BLUE}[INFO]${NC}  $*"; }
ok()     { echo "${GREEN}[ OK ]${NC}  $*"; }
warn()   { echo "${YELLOW}[WARN]${NC}  $*" >&2; }
fail()   { echo "${RED}[FAIL]${NC}  $*" >&2; exit 1; }
header() { printf '\n==========================================\n %s\n==========================================\n\n' "$*"; }

# ── Root / k3s ──────────────────────────────────────────────────────

# Re-exec the calling script under sudo if we're not already root.
require_root() {
    [[ $EUID -eq 0 ]] || exec sudo -- "$0" "$@"
}

kc() { k3s kubectl "$@"; }

has_systemd() { [[ -d /run/systemd/system ]]; }

k3s_is_running() {
    if has_systemd; then
        systemctl is-active --quiet k3s
    else
        pgrep -f 'k3s server' >/dev/null
    fi
}

# ── ArgoCD manifest introspection ───────────────────────────────────

# Leaf Application files, one per platform service / app.
leaf_app_files() {
    compgen -G "$ARGOCD_DIR/apps/*.yaml" || true
}

# metadata.name of an Application manifest.
app_name() {
    awk '/^metadata:/{m=1; next} m && /^[^ #]/{m=0} m && /^  name:/{print $2; exit}' "$1"
}

# spec.project of an Application manifest ("platform" or "apps").
app_project() {
    awk '/^spec:/{s=1; next} s && /^[^ #]/{s=0} s && /^  project:/{print $2; exit}' "$1"
}

# spec.destination.namespace of an Application manifest.
app_namespace() {
    awk '/^  destination:/{d=1; next} d && /^  [^ ]/{d=0} d && /^    namespace:/{print $2; exit}' "$1"
}
