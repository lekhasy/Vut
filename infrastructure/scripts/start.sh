#!/usr/bin/env bash
# Velucid Platform - Local Dev Startup Script (Docker Compose)
# For staging/production, use K3s: ./scripts/k3s-start.sh
#
# Usage: ./scripts/start.sh

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
echo " Velucid Platform - Starting (dev)"
echo "=========================================="
echo ""
echo "Compose: docker-compose.yml + docker-compose.override.${PLATFORM}.yml"
echo "Env:     .env.dev"
echo ""

# Start services
echo ">>> Starting services..."
docker compose $COMPOSE_FILES $ENV_ARG up -d
echo ""

# Wait for services to be healthy
echo ">>> Waiting for services to become healthy..."
echo ""

timeout=120
elapsed=0
interval=5

check_health() {
    local service="$1"
    local container_name="velucid-${service}"
    local status
    status=$(docker inspect --format='{{.State.Health.Status}}' "$container_name" 2>/dev/null || echo "unknown")

    if [[ "$status" == "healthy" ]]; then
        echo "  [OK]   $service"
        return 0
    elif [[ "$status" == "unhealthy" ]]; then
        echo "  [FAIL] $service (unhealthy)"
        return 1
    else
        echo "  [WAIT] $service ($status)"
        return 2
    fi
}

while [[ $elapsed -lt $timeout ]]; do
    all_healthy=true
    echo "--- Health check at ${elapsed}s ---"

    for service in kurrentdb postgresql; do
        result=$(check_health "$service") || true
        if ! echo "$result" | grep -q "\[OK\]"; then
            all_healthy=false
        fi
        echo "$result"
    done

    if $all_healthy; then
        echo ""
        echo "=========================================="
        echo " All core services are healthy!"
        echo "=========================================="
        echo ""

        # Check application services if they exist
        echo ">>> Checking application services..."
        for service in silo projector-service frontend; do
            container=$(docker ps --filter "name=velucid-${service}" --format "{{.Names}}" 2>/dev/null || true)
            if [[ -n "$container" ]]; then
                check_health "$service" || true
            else
                echo "  [SKIP] $service (not running — image not built yet)"
            fi
        done

        echo ""
        echo "Access points:"
        if docker ps --filter "name=velucid-frontend" --format "{{.Names}}" | grep -q velucid-frontend 2>/dev/null; then
            echo "  Frontend:       http://localhost:${FRONTEND_PORT:-3000}"
        fi
        if docker ps --filter "name=velucid-silo" --format "{{.Names}}" | grep -q velucid-silo 2>/dev/null; then
            echo "  Silo (API):     http://localhost:${SILO_API_PORT:-5000}"
        fi
        echo "  KurrentDB:      http://localhost:${KURRENTDB_HTTP_PORT:-2113}"
        echo "  PostgreSQL:     localhost:${POSTGRESQL_PORT:-5432}"
        echo ""
        exit 0
    fi

    sleep "$interval"
    elapsed=$((elapsed + interval))
done

echo ""
echo "=========================================="
echo " TIMEOUT: Not all services became healthy"
echo " within ${timeout}s. Check logs:"
echo "   docker compose $COMPOSE_FILES $ENV_ARG logs"
echo "=========================================="
exit 1
