#!/usr/bin/env bash
# Velucid Platform - Local Dev Shutdown Script (Docker Compose)
# Usage: ./scripts/stop.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"

cd "$INFRA_DIR"

# Detect platform
ARCH="$(uname -m)"
case "$ARCH" in
    arm64|aarch64) PLATFORM="arm" ;;
    *)             PLATFORM="amd64" ;;
esac

COMPOSE_FILES="-f docker-compose.yml -f docker-compose.override.${PLATFORM}.yml"
ENV_ARG="--env-file .env.dev"

echo "=========================================="
echo " Velucid Platform - Stopping (dev)"
echo "=========================================="
echo ""

echo ">>> Stopping services..."
docker compose $COMPOSE_FILES $ENV_ARG down --remove-orphans
echo ""

echo ">>> All services stopped."
echo ""
echo "To remove volumes (WARNING: deletes all data):"
echo "  docker compose $COMPOSE_FILES $ENV_ARG down -v"
echo ""
echo "To remove volumes AND images:"
echo "  docker compose $COMPOSE_FILES $ENV_ARG down -v --rmi all"
