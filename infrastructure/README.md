# VUT Platform - Infrastructure

Local infrastructure for the VUT project management SaaS platform. This directory contains everything needed to run the full platform stack on a developer machine.

## Architecture Overview

```
                    Browser
                       |
                    Ingress
                   /        \
            Frontend:3000   API:5000
          (Astro.js SSR)   (gRPC)
                  \          /
             Actor Service:5000
              (Proto.Actor)
              /     |     \
     KurrentDB  Redpanda  PostgreSQL
       :2113     :9092     :5432
    (Events)   (Messaging) (Read Model)
```

**Write Path:** Browser -> Frontend/BFF -> Actor Service -> KurrentDB -> Redpanda
**Project Path:** Redpanda -> Projector Service -> PostgreSQL
**Read Path:** Browser -> Frontend/BFF -> Read Model API -> PostgreSQL

## Services

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| KurrentDB | `kurrentplatform/kurrentdb:26.1.0` | 2113 (HTTP), 1113 (TCP) | Event store (event-sourced) |
| Redpanda | `redpandadata/redpanda:v24.2.2` | 9092 (Kafka), 9644 (Admin) | Kafka-compatible message broker |
| PostgreSQL | `postgres:16-alpine` | 5432 | Read model store |
| Actor Service | `vut/actor-service:latest` | 5000 (gRPC), 5001 (HTTP) | .NET Proto.Actor backend |
| Frontend | `vut/frontend:latest` | 3000 (HTTP) | Astro.js SSR + SPA |

## Resource Budget

| Environment | Available RAM | KurrentDB | Redpanda | PostgreSQL | Actor Svc | Frontend | Total Est. |
|-------------|--------------|-----------|----------|------------|-----------|----------|------------|
| **Dev** | ~24 GB | 3 GB | 1 GB | 2 GB | 1 GB | 512 MB | **~7.5 GB** |
| **Staging** | ~40 GB | 6 GB | 2 GB | 4 GB | 2 GB | 1 GB | **~15 GB** |
| **Prod** | ~52 GB | 8 GB | 4 GB | 8 GB | 4 GB | 1 GB | **~25 GB** |

K8s StatefulSet replicas:
- **Dev:** KurrentDB 3 nodes, Redpanda 3 brokers (use 1-node for lighter dev)
- **Staging:** Single-node for all stateful services
- **Prod:** Full 3-node clusters via K3s

## Quick Start (Docker Compose - Recommended for Dev)

This is the primary way to run the infrastructure for day-to-day development.

### Prerequisites

- Docker Engine 24+ and Docker Compose v2
- ~8 GB free RAM for dev environment
- Git

### Start the Platform

```bash
# Navigate to infrastructure directory
cd infrastructure

# Start with dev defaults (uses .env.dev + docker-compose.override.yml)
./scripts/start.sh

# Or specify an environment
./scripts/start.sh staging
./scripts/start.sh prod
```

The start script:
1. Validates the environment file exists
2. Pulls images (staging/prod only; dev uses local builds)
3. Starts all services with `docker compose up -d`
4. Waits for health checks to pass
5. Runs the Redpanda topic initialization job

### Stop the Platform

```bash
./scripts/stop.sh          # Stops containers (preserves data)
./scripts/stop.sh prod     # Stop prod environment

# To remove data volumes (destructive):
docker compose -f docker-compose.yml -f docker-compose.override.yml down -v
```

### Health Check

```bash
./scripts/health-check.sh
# Checks: container health, TCP ports, HTTP endpoints, Redpanda topics, PostgreSQL tables
```

### Manual Docker Compose Commands

```bash
# View logs
docker compose logs -f kurrentdb
docker compose logs -f redpanda
docker compose logs -f actor-service

# Restart a single service
docker compose restart actor-service

# Rebuild after code changes
docker compose up -d --build actor-service frontend
```

### Access Points (Dev)

| Service | URL |
|---------|-----|
| Frontend | http://localhost:3000 |
| Actor Service (gRPC) | localhost:5000 |
| Actor Service (HTTP) | localhost:5001 |
| KurrentDB Dashboard | http://localhost:2113 |
| KurrentDB API | http://localhost:2113/health/live |
| Redpanda Admin | http://localhost:9644 |
| PostgreSQL | localhost:5432 |

### PostgreSQL Connection

```
Host: localhost
Port: 5432
Database: vut_readmodel
Username: vut_app
Password: vut_dev_password
```

Connect with `psql`:
```bash
docker exec -it vut-postgresql psql -U vut_app -d vut_readmodel
```

## Kubernetes Setup (K3s / minikube / kind)

For staging and production, or developers who prefer Kubernetes locally.

### Prerequisites

- minikube, kind, or K3s
- kubectl
- Images pre-loaded into the cluster (for `imagePullPolicy: Never`)

### Load Images into Cluster

```bash
# Build and load actor service
docker build -t vut/actor-service:latest ../backend/src/Vut.ActorService
minikube image load vut/actor-service:latest
# or: kind load docker-image vut/actor-service:latest

# Build and load frontend
docker build -t vut/frontend:latest ../frontend
minikube image load vut/frontend:latest

# Pre-load infrastructure images (optional, they'll be pulled otherwise)
minikube image load kurrentplatform/kurrentdb:26.1.0
minikube image load redpandadata/redpanda:v24.2.2
minikube image load postgres:16-alpine
```

### Deploy

```bash
# Run the full setup script (applies all manifests + waits for health)
./k8s/dev-setup.sh

# Clean start (delete namespace first)
./k8s/dev-setup.sh --cleanup

# Or apply manually in order:
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets/
kubectl apply -f k8s/postgresql/
kubectl apply -f k8s/kurrentdb/
kubectl apply -f k8s/redpanda/
kubectl apply -f k8s/actor-service/
kubectl apply -f k8s/frontend/
kubectl apply -f k8s/ingress.yaml

# Initialize Redpanda topics
kubectl apply -f k8s/redpanda/topic-init-job.yaml
```

### Port Forwarding for Local Access

```bash
kubectl port-forward -n vut svc/vut-frontend 3000:3000 &
kubectl port-forward -n vut svc/vut-actor-service 5000:5000 &
kubectl port-forward -n vut svc/vut-kurrentdb 2113:2113 &
```

### Teardown

```bash
kubectl delete namespace vut
```

## Redpanda Topics

| Topic | Partitions | Purpose |
|-------|-----------|---------|
| `vut.user-events` | 3 | All User aggregate events |
| `vut.org-events` | 6 | All Organization aggregate events |

Topics are created automatically on startup by the `redpanda-init` container (Docker Compose) or the `vut-redpanda-topic-init` Job (K8s).

Verify topics:
```bash
# Docker Compose
docker exec vut-redpanda rpk topic list --brokers localhost:9092

# K8s
kubectl exec -n vut vut-redpanda-0 -- rpk topic list --brokers localhost:9092
```

## Environment Variables

Three env files control configuration per environment:

| File | When Used | Notes |
|------|----------|-------|
| `.env.dev` | Default / `VUT_ENV=dev` | Local development defaults |
| `.env.staging` | `VUT_ENV=staging` | Staging on local machine |
| `.env.prod` | `VUT_ENV=prod` | Production on local machine |

Sensitive values (Auth0, passwords) use placeholder values. Copy and customize:

```bash
cp .env.dev .env.local  # For personal overrides (git-ignored)
```

## Directory Structure

```
infrastructure/
  docker-compose.yml            # Base compose (all services)
  docker-compose.override.yml   # Dev overrides (auto-loaded)
  docker-compose.staging.yml    # Staging overrides
  docker-compose.prod.yml       # Production overrides
  .env.dev                      # Dev environment variables
  .env.staging                  # Staging environment variables
  .env.prod                     # Production environment variables
  scripts/
    start.sh                    # Environment-aware startup
    stop.sh                     # Clean shutdown
    health-check.sh             # Verify all services
    # No SQL init scripts — read model schema is managed by the application
    # via code-first migrations (EF Core)
  k8s/                          # Kubernetes manifests
    namespace.yaml              # vut namespace
    ingress.yaml                # NGINX ingress routing
    secrets/
      vut-postgresql-secret.yaml
      vut-auth0-secret.yaml
    kurrentdb/
      statefulset.yaml          # 3-node event store
      service.yaml
    redpanda/
      statefulset.yaml          # 3-broker cluster
      service.yaml
      topic-init-job.yaml       # Creates vut.* topics
    postgresql/
      statefulset.yaml          # Primary + init SQL ConfigMap
      service.yaml
    actor-service/
      deployment.yaml           # .NET Proto.Actor backend
      service.yaml
    frontend/
      deployment.yaml           # Astro.js SSR
      service.yaml
    dev-setup.sh                # One-command K8s deployment
  README.md                     # This file
```

## Troubleshooting

### Docker Compose

**Port conflicts:** Change the port in `.env.dev` (e.g., `FRONTEND_PORT=4321`).

**Container keeps restarting:** Check logs: `docker compose logs <service>`. Most common causes:
- KurrentDB: insufficient memory (increase `KURRENTDB_MEMORY_LIMIT`)
- Redpanda: data corruption (delete volume: `docker volume rm vut-redpanda-data`)

**Database tables missing:** The init SQL only runs on first container start. If volumes were created before the init script existed, delete the volume:
```bash
docker compose down -v
docker compose up -d
```

### Kubernetes

**Pods stuck in Pending:** Check node resources with `kubectl describe node`. Reduce memory limits in manifests if needed.

**ImagePullBackOff:** Ensure images are loaded into the cluster. Run `minikube image ls | grep vut` to verify.

**CrashLoopBackOff:** Check logs: `kubectl logs -n vut <pod-name>`. Common issue is the PostgreSQL init ConfigMap not being applied before the StatefulSet.

## Notes

- All services run locally on developer machines -- no cloud providers required
- Docker Compose is the primary development workflow; K8s manifests are for K3s staging/production
- The actor-service and frontend images are placeholders (`vut/actor-service:latest`, `vut/frontend:latest`) until Dockerfiles are created in later tasks
- For CI, image tags should use commit SHA or build number instead of `latest`
- Helm chart conversion is a future improvement -- raw manifests are used for Epic 1
