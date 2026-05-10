# VUT Platform - Infrastructure

Local infrastructure for the VUT project management SaaS platform. This directory contains everything needed to run the full platform stack on a single developer machine using Docker Compose (dev) or K3s (staging/prod).

## Architecture Overview

```
              Internet
                 |
         Cloudflare Edge
                 |
          cloudflared tunnel
            (outbound-only)
                 |
     ┌───────────────────────┐
     │   K3s (single node)   │
     │                       │
     │  Traefik Ingress      │
     │   /      \            │
     │ Frontend  Silo:5000   │
     │ :3000    (API+Grains) │
     │   \       / \         │
     │  KurrentDB  PostgreSQL│
     │   :2113      :5432    │
     └───────────────────────┘
```

**Write Path:** Browser → Frontend/BFF → Silo API → KurrentDB
**Projection Path:** KurrentDB subscriptions → Silo projections → PostgreSQL
**Read Path:** Browser → Frontend/BFF → Silo API → PostgreSQL

## Services

| Service | Image | Ports | Purpose |
|---------|-------|-------|---------|
| KurrentDB | `kurrentplatform/kurrentdb:26.1.0` | 2113 (HTTP), 1113 (TCP) | Event store (event-sourced) |
| PostgreSQL | `postgres:16-alpine` | 5432 | Read model + Orleans clustering/storage |
| Silo | `ghcr.io/lekhasy/vut/silo:latest` | 5000 (HTTP API), 11111 (silo-to-silo), 30000 (gateway), 8888 (Orleans Dashboard) | .NET Orleans backend (API + Grains co-hosted) |
| Projector Service | `ghcr.io/lekhasy/vut/projector-service:latest` | — | Background worker (KurrentDB → PostgreSQL projections) |
| Frontend | `ghcr.io/lekhasy/vut/frontend:latest` | 3000 (HTTP) | Astro.js SSR + BFF |
| cloudflared | `cloudflare/cloudflared:latest` | — | Cloudflare Tunnel (internet ingress) |

## Resource Budget

| Environment | KurrentDB | PostgreSQL | Silo | Projector | Frontend | cloudflared | Total Est. |
|-------------|-----------|------------|------|-----------|----------|-------------|------------|
| **Dev** | 3 GB | 2 GB | 1 GB | 512 MB | 512 MB | 128 MB | **~7.1 GB** |
| **Staging** | 6 GB | 4 GB | 2 GB | 512 MB | 1 GB | 128 MB | **~13.6 GB** |
| **Prod** | 8 GB | 8 GB | 4 GB | 1 GB | 1 GB | 128 MB | **~22 GB** |

All deployments run with `replicas: 1` on a single machine (K3s node).

## Quick Start (Docker Compose — Recommended for Dev)

This is the primary way to run the infrastructure for day-to-day development.

### Prerequisites

- Docker Engine 24+ and Docker Compose v2
- ~7 GB free RAM for dev environment
- Git

### Start the Platform

```bash
cd infrastructure

# Start with dev defaults
./scripts/start.sh

# Or specify an environment
./scripts/start.sh staging
./scripts/start.sh prod
```

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
# Checks: container health, TCP ports, HTTP endpoints, PostgreSQL tables
```

### Manual Docker Compose Commands

```bash
# View logs
docker compose logs -f kurrentdb
docker compose logs -f silo

# Restart a single service
docker compose restart silo

# Rebuild after code changes
docker compose up -d --build silo frontend
```

### Access Points (Dev)

| Service | URL |
|---------|-----|
| Frontend | http://localhost:3000 |
| Silo API | http://localhost:5000 |
| Orleans Dashboard | http://localhost:8888 |
| KurrentDB Dashboard | http://localhost:2113 |
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

## Kubernetes Setup (K3s)

For staging, production, or developers who prefer Kubernetes locally. K3s is the target runtime — it bundles Traefik as the default ingress controller and uses `local-path` storage provisioner.

### Prerequisites

- K3s installed: `curl -sfL https://get.k3s.io | sh -`
- kubectl (included with K3s)
- ArgoCD installed in K3s (see GitOps section below)

### GitOps CI/CD Pipeline

Images are built automatically by GitHub Actions and pushed to `ghcr.io/lekhasy/vut/`. ArgoCD watches the K8s manifests in git and syncs changes to K3s automatically.

```
git push → GitHub Actions → build image → push to ghcr.io
  → update image tag in K8s manifests (git commit)
  → ArgoCD detects manifest change → syncs to K3s → rolling update
```

**Pipeline components:**

| Component | Purpose |
|-----------|---------|
| GitHub Actions (`.github/workflows/ci.yaml`) | Builds Docker images, pushes to ghcr.io, updates manifest image tags |
| ghcr.io (`ghcr.io/lekhasy/vut/`) | Container registry (free for public repos) |
| ArgoCD (`k8s/argocd/application.yaml`) | Watches `infrastructure/k8s/` and auto-syncs to K3s |

**Image tagging:** CI tags images with both the commit SHA (e.g., `abc1234`) and `latest`. Manifests are updated to the commit SHA for traceability.

### Install ArgoCD

```bash
# Create ArgoCD namespace and install
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# Wait for ArgoCD to be ready
kubectl wait --for=condition=available deployment/argocd-server -n argocd --timeout=120s

# Apply the VUT ArgoCD Application manifest
kubectl apply -f k8s/argocd/application.yaml

# Access ArgoCD UI (optional)
kubectl port-forward svc/argocd-server -n argocd 8080:443 &
# Get initial admin password:
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
```

### Deploy (Manual — for initial setup or without ArgoCD)

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
kubectl apply -f k8s/silo/
kubectl apply -f k8s/projector-service/
kubectl apply -f k8s/frontend/
kubectl apply -f k8s/ingress.yaml
kubectl apply -f k8s/cloudflared/   # After configuring tunnel credentials
```

> **Note:** Once ArgoCD is installed and the Application manifest is applied, manual `kubectl apply` is no longer needed — ArgoCD auto-syncs from git.

### Cloudflare Tunnel Setup

Cloudflare Tunnel provides internet ingress without opening inbound ports or needing a static IP.

```bash
# 1. Install cloudflared CLI
# https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/

# 2. Authenticate
cloudflared tunnel login

# 3. Create tunnel
cloudflared tunnel create vut

# 4. Create DNS records
cloudflared tunnel route dns vut vut.app
cloudflared tunnel route dns vut "*.vut.app"

# 5. Encode credentials for K8s secret
cat ~/.cloudflared/<TUNNEL_ID>.json | base64

# 6. Update k8s/cloudflared/secret.yaml with the base64 value
# 7. Update k8s/cloudflared/configmap.yaml with the <TUNNEL_ID>
# 8. Apply manifests
kubectl apply -f k8s/cloudflared/
```

Traffic flow: `Internet → Cloudflare Edge → cloudflared pod → Traefik → services`

### Port Forwarding for Local Access

```bash
kubectl port-forward -n vut svc/vut-frontend 3000:3000 &
kubectl port-forward -n vut svc/vut-silo 5000:5000 &
kubectl port-forward -n vut svc/vut-kurrentdb 2113:2113 &
```

### Teardown

```bash
kubectl delete namespace vut
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
  docker-compose.override.arm.yml   # Dev overrides (ARM)
  docker-compose.override.amd64.yml # Dev overrides (AMD64)
  docker-compose.staging.yml    # Staging overrides
  docker-compose.prod.yml       # Production overrides
  .env.dev                      # Dev environment variables
  .env.staging                  # Staging environment variables
  .env.prod                     # Production environment variables
  scripts/
    start.sh                    # Environment-aware startup
    stop.sh                     # Clean shutdown
    health-check.sh             # Verify all services
  k8s/                          # Kubernetes manifests (K3s)
    namespace.yaml              # vut namespace
    ingress.yaml                # Traefik ingress routing
    secrets/
      vut-postgresql-secret.yaml
      vut-auth0-secret.yaml
    kurrentdb/
      statefulset.yaml          # 1-node event store
      service.yaml
    postgresql/
      statefulset.yaml          # PostgreSQL + Orleans clustering
      service.yaml
    silo/
      deployment.yaml           # .NET Orleans silo (API + Grains)
      service.yaml
    projector-service/
      deployment.yaml           # Background worker (KurrentDB → PostgreSQL)
      service.yaml
    frontend/
      deployment.yaml           # Astro.js SSR + BFF
      service.yaml
    argocd/
      application.yaml          # ArgoCD Application (auto-sync from git)
    cloudflared/
      secret.yaml               # Tunnel credentials
      configmap.yaml            # Tunnel config (routes)
      deployment.yaml           # cloudflared daemon
    dev-setup.sh                # One-command K3s deployment
  README.md                     # This file
.github/
  workflows/
    ci.yaml                     # GitHub Actions CI (build → ghcr.io → update manifests)
```

## Troubleshooting

### Docker Compose

**Port conflicts:** Change the port in `.env.dev` (e.g., `FRONTEND_PORT=4321`).

**Container keeps restarting:** Check logs: `docker compose logs <service>`. Most common causes:
- KurrentDB: insufficient memory (increase `KURRENTDB_MEMORY_LIMIT`)
- PostgreSQL: data corruption (delete volume: `docker volume rm vut-postgresql-data`)

### Kubernetes (K3s)

**Pods stuck in Pending:** Check node resources with `kubectl describe node`. Reduce memory limits in manifests if needed.

**ImagePullBackOff:** Ensure images are loaded into K3s. Run `sudo k3s ctr images ls | grep vut` to verify.

**CrashLoopBackOff:** Check logs: `kubectl logs -n vut <pod-name>`. Common issue is secrets not being applied before deployments.

**Cloudflare Tunnel not connecting:** Verify credentials in the secret match the tunnel ID in the configmap. Check logs: `kubectl logs -n vut -l app.kubernetes.io/name=cloudflared`.

## Notes

- All services run locally on a single developer machine — no cloud providers required
- Docker Compose is the primary development workflow; K8s manifests target K3s
- K3s bundles Traefik (ingress) and local-path-provisioner (storage) — no extra setup
- Cloudflare Tunnel provides internet access without inbound ports or static IPs
- Orleans uses PostgreSQL for cluster membership and grain state storage — no separate message broker needed
- Application images are pulled from ghcr.io (`ghcr.io/lekhasy/vut/silo`, `ghcr.io/lekhasy/vut/frontend`, `ghcr.io/lekhasy/vut/projector-service`)
- CI tags images with commit SHA for traceability; manifests are updated automatically by GitHub Actions
- ArgoCD auto-syncs K8s manifests from the `infrastructure/k8s/` directory — git is the single source of truth
- Helm chart conversion is a future improvement — raw manifests are used for Epic 1
