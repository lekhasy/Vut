#!/usr/bin/env bash
# K3s Cluster Wipe Script
# WARNING: This deletes ALL resources including PersistentVolumes and their data.
# Use only when you want a completely fresh start.

set -euo pipefail

RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

warn() { echo -e "${YELLOW}[WARN]${NC}  $*"; }

KUBECTL="sudo k3s kubectl"

echo ""
echo "=========================================="
echo " WARNING: K3s Cluster Wipe"
echo "=========================================="
echo ""
echo "This will DELETE:"
echo "  - All namespaces and resources"
echo "  - All PersistentVolumeClaims"
echo "  - All PersistentVolumes (DATA LOSS)"
echo "  - All storage on local-path provisioner"
echo ""
echo -n "Type 'yes-delete-everything' to confirm: "
read -r CONFIRM

if [[ "$CONFIRM" != "yes-delete-everything" ]]; then
    echo "Aborted."
    exit 1
fi

echo ""
echo "[1/5] Deleting all namespaced resources..."
$KUBECTL delete all --all --all-namespaces 2>/dev/null || true
$KUBECTL delete crd --all 2>/dev/null || true
$KUBECTL delete namespace --all 2>/dev/null || true

echo ""
echo "[2/5] Deleting all PVCs..."
$KUBECTL delete pvc --all --all-namespaces 2>/dev/null || true

echo ""
echo "[3/5] Deleting all PVs..."
$KUBECTL delete pv --all 2>/dev/null || true

echo ""
echo "[4/5] Cleaning up local-path provisioner data..."
# K3s local-path-provisioner stores data in /var/lib/rancher/k3s/storage
STORAGE_DIR="/var/lib/rancher/k3s/storage"
if [[ -d "$STORAGE_DIR" ]]; then
    warn "Wiping $STORAGE_DIR..."
    sudo rm -rf "$STORAGE_DIR"/*
fi

echo ""
echo "[5/5] Cleaning up remaining kube system artifacts..."
$KUBECTL delete pods,svc,cm,secret --all -n kube-system 2>/dev/null || true

echo ""
echo "=========================================="
echo " Cluster wiped successfully"
echo "=========================================="
echo ""
echo "Next steps:"
echo "  1. Restart k3s:     sudo systemctl restart k3s"
echo "  2. Run startup:     ./scripts/k3s-start.sh"
echo ""