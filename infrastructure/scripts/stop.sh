#!/usr/bin/env bash
# VUT Platform - Clean Shutdown Script
# Usage: ./scripts/stop.sh [dev|staging|prod]
# Defaults to 'dev' if no argument is provided.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"

ENV="${1:-dev}"

if [[ ! "$ENV" =~ ^(dev|staging|prod)$ ]]; then
    echo "ERROR: Invalid environment '$ENV'. Must be one of: dev, staging, prod"
    exit 1
fi

echo "=========================================="
echo " VUT Platform - Stopping ($ENV)"
echo "=========================================="
echo ""

# Determine compose files
ARCH="$(uname -m)"
case "$ARCH" in
    arm64|aarch64)
        PLATFORM="arm"
        ;;
    *)
        PLATFORM="amd64"
        ;;
esac

case "$ENV" in
    dev)
        COMPOSE_FILES="-f docker-compose.yml -f docker-compose.override.${PLATFORM}.yml"
        ENV_ARG="--env-file .env.dev"
        ;;
    staging)
        COMPOSE_FILES="-f docker-compose.yml -f docker-compose.staging.yml"
        ENV_ARG="--env-file .env.staging"
        ;;
    prod)
        COMPOSE_FILES="-f docker-compose.yml -f docker-compose.prod.yml"
        ENV_ARG="--env-file .env.prod"
        ;;
esac

cd "$INFRA_DIR"

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
