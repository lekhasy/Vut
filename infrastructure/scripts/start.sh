#!/usr/bin/env bash
# VUT Platform - Environment-Aware Startup Script
# Usage: ./scripts/start.sh [dev|staging|prod]
# Defaults to 'dev' if no argument is provided.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"

# Default to dev environment
ENV="${1:-dev}"
ENV_FILE="$INFRA_DIR/.env.${ENV}"

# Validate environment
if [[ ! "$ENV" =~ ^(dev|staging|prod)$ ]]; then
    echo "ERROR: Invalid environment '$ENV'. Must be one of: dev, staging, prod"
    exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
    echo "ERROR: Environment file not found: $ENV_FILE"
    echo "Copy .env.dev to .env.${ENV} and adjust values if needed."
    exit 1
fi

echo "=========================================="
echo " VUT Platform - Starting ($ENV)"
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

echo "Using compose files: $COMPOSE_FILES"
echo "Using env file:     $ENV_FILE"
echo ""

# Pull images first (skip for dev to use local builds)
if [[ "$ENV" != "dev" ]]; then
    echo ">>> Pulling images..."
    docker compose $COMPOSE_FILES $ENV_ARG pull
    echo ""
fi

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
    local healthy
    healthy=$(docker compose $COMPOSE_FILES $ENV_ARG ps --format json 2>/dev/null \
        | jq -r "select(.Service == \"$service\") | .Health" 2>/dev/null || echo "unknown")

    if [[ "$healthy" == "healthy" ]]; then
        echo "  [OK]   $service"
        return 0
    elif [[ "$healthy" == "unhealthy" ]]; then
        echo "  [FAIL] $service (unhealthy)"
        return 1
    else
        echo "  [WAIT] $service ($healthy)"
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
        for service in actor-service frontend; do
            container=$(docker ps --filter "name=vut-${service}" --format "{{.Names}}" 2>/dev/null || true)
            if [[ -n "$container" ]]; then
                check_health "$service" || true
            else
                echo "  [SKIP] $service (not running - image not built yet)"
            fi
        done

        echo ""
        echo ">>> PostgreSQL ready (Orleans clustering + read model store)"
        echo ""
        echo "Access points:"
        # Only show app service URLs if the containers are running
        if docker ps --filter "name=vut-frontend" --format "{{.Names}}" | grep -q vut-frontend 2>/dev/null; then
            echo "  Frontend:       http://localhost:${FRONTEND_PORT:-3000}"
        fi
        if docker ps --filter "name=vut-actor-service" --format "{{.Names}}" | grep -q vut-actor-service 2>/dev/null; then
            echo "  Actor Service:  http://localhost:${ACTOR_SERVICE_PORT:-5000}"
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
echo "   docker compose logs"
echo "=========================================="
exit 1
