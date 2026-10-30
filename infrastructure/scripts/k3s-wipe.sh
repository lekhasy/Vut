#!/usr/bin/env bash
# Platform — K3s wipe.
#
# Resets the cluster to an empty state by deleting the K3s datastore and all
# local-path volume data. K3s itself (binary, certs, node config, cached
# container images) is kept, so the next k3s-start.sh run rebuilds everything
# from git without re-downloading images.
#
# WARNING: destroys ALL cluster state and ALL PersistentVolume data.
#
# Usage:
#   sudo ./infrastructure/scripts/k3s-wipe.sh

set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
require_root "$@"

KILLALL="/usr/local/bin/k3s-killall.sh"

header "WARNING: K3s Cluster Wipe"
cat <<EOF
This will DELETE:
  - All Kubernetes objects (datastore: $K3S_DATA_DIR/server/db)
  - All PersistentVolume data      ($K3S_DATA_DIR/storage)

EOF
read -rp "Type 'yes-delete-everything' to confirm: " confirm
[[ "$confirm" == "yes-delete-everything" ]] || { echo "Aborted."; exit 1; }
echo

[[ -x "$KILLALL" ]] || fail "$KILLALL not found — is K3s installed via the official installer?"

info "[1/3] Stopping K3s..."
if has_systemd; then
    systemctl stop k3s
fi
# Stops the server (if not under systemd), all pods/containers, and unmounts volumes.
"$KILLALL" >/dev/null
ok "K3s stopped"

info "[2/3] Deleting datastore..."
rm -rf "$K3S_DATA_DIR/server/db"
ok "Datastore deleted"

info "[3/3] Deleting local-path volume data..."
rm -rf "$K3S_DATA_DIR/storage"
mkdir -p "$K3S_DATA_DIR/storage"
ok "Volume data deleted"

header "Cluster wiped"
echo "Next: sudo ./infrastructure/scripts/k3s-start.sh"
echo
