#!/usr/bin/env bash
# Velucid Platform - Health Check Script
# Usage: ./scripts/health-check.sh [dev|staging|prod]
# Defaults to 'dev' if no argument is provided.
#
# Exit codes:
#   0 - All services healthy
#   1 - One or more services unhealthy
#   2 - Configuration error

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"

ENV="${1:-dev}"

if [[ ! "$ENV" =~ ^(dev|staging|prod)$ ]]; then
    echo "ERROR: Invalid environment '$ENV'. Must be one of: dev, staging, prod"
    exit 2
fi

# Source env file for port defaults
ENV_FILE="$INFRA_DIR/.env.${ENV}"
if [[ -f "$ENV_FILE" ]]; then
    set -a
    source "$ENV_FILE"
    set +a
fi

echo "=========================================="
echo " Velucid Platform - Health Check ($ENV)"
echo "=========================================="
echo ""

FAILURES=0

check_docker_service() {
    local name="$1"
    local container_name="velucid-${name}"

    if ! docker ps --filter "name=${container_name}" --format "{{.Names}}" | grep -q "$container_name"; then
        echo "  [FAIL] $name - container not running"
        ((FAILURES++))
        return 1
    fi

    local health
    health=$(docker inspect --format='{{.State.Health.Status}}' "$container_name" 2>/dev/null || echo "none")

    if [[ "$health" == "healthy" ]]; then
        echo "  [OK]   $name (container healthy)"
        return 0
    elif [[ "$health" == "unhealthy" ]]; then
        echo "  [FAIL] $name (container unhealthy)"
        ((FAILURES++))
        return 1
    else
        echo "  [WARN] $name (no health check or starting up)"
        return 0
    fi
}

check_tcp_port() {
    local name="$1"
    local host="$2"
    local port="$3"

    if docker exec velucid-"$name" sh -c "nc -z $host $port" 2>/dev/null; then
        echo "  [OK]   $name:$port reachable"
        return 0
    elif command -v nc &>/dev/null && nc -z "$host" "$port" 2>/dev/null; then
        echo "  [OK]   $name:$port reachable (via host)"
        return 0
    else
        echo "  [FAIL] $name:$port not reachable"
        ((FAILURES++))
        return 1
    fi
}

check_url() {
    local name="$1"
    local url="$2"
    local expected_code="${3:-200}"

    local http_code
    http_code=$(curl -s -o /dev/null -w "%{http_code}" "$url" 2>/dev/null || echo "000")

    if [[ "$http_code" == "$expected_code" ]]; then
        echo "  [OK]   $name -> $url (HTTP $http_code)"
        return 0
    else
        echo "  [FAIL] $name -> $url (HTTP $http_code, expected $expected_code)"
        ((FAILURES++))
        return 1
    fi
}

# 1. Check infrastructure services
echo "--- Infrastructure Services ---"
check_docker_service "kurrentdb"
check_docker_service "postgresql"
echo ""

# 2. Check application services
echo "--- Application Services ---"
check_docker_service "silo"
check_docker_service "projector-service"
check_docker_service "frontend"
echo ""

# 3. Check network connectivity
echo "--- Network Connectivity ---"
check_tcp_port "kurrentdb" "localhost" "${KURRENTDB_HTTP_PORT:-2113}"
check_tcp_port "postgresql" "localhost" "${POSTGRESQL_PORT:-5432}"
echo ""

# 4. Check HTTP endpoints
echo "--- HTTP Endpoints ---"
check_url "KurrentDB (live)" "http://localhost:${KURRENTDB_HTTP_PORT:-2113}/health/live"
check_url "KurrentDB (ready)" "http://localhost:${KURRENTDB_HTTP_PORT:-2113}/health/ready"
echo ""

# 5. Check PostgreSQL database
echo "--- PostgreSQL ---"
if docker exec velucid-postgresql psql -U "${POSTGRESQL_USER:-velucid_app}" -d velucid_readmodel -c "SELECT 1;" &>/dev/null; then
    echo "  [OK]   velucid_readmodel database accessible"

    # Check tables exist
    tables=$(docker exec velucid-postgresql psql -U "${POSTGRESQL_USER:-velucid_app}" -d velucid_readmodel -t -c \
        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('user_projection','org_projection','org_member_projection','org_invitation_projection','user_org_projection','user_identity');" 2>/dev/null | tr -d ' ')
    if [[ "$tables" -ge 3 ]]; then
        echo "  [OK]   Read model tables present ($tables tables)"
    else
        echo "  [WARN] Expected 3+ tables, found $tables"
    fi
else
    echo "  [FAIL] Cannot connect to PostgreSQL"
    ((FAILURES++))
fi
echo ""

# Summary
echo "=========================================="
if [[ $FAILURES -eq 0 ]]; then
    echo " All checks passed!"
    echo "=========================================="
    exit 0
else
    echo " $FAILURES check(s) failed"
    echo "=========================================="
    echo ""
    echo "Troubleshooting:"
    echo "  docker compose logs <service-name>"
    echo ""
    exit 1
fi
