# Velucid Platform - Infrastructure

Local infrastructure for the Velucid project management SaaS platform.

- **Docker Compose** — local development on your dev machine (Windows/macOS/Linux)
- **K3s + ArgoCD** — staging and production on a WSL2 machine (see `docs/new-machine-setup.md`)

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

## Docker Compose (Local Development)

The primary way to run the infrastructure for day-to-day development.

### Prerequisites

- Docker Engine 24+ and Docker Compose v2
- ~7 GB free RAM
- Git

### Start / Stop / Health Check

```bash
cd infrastructure
./scripts/start.sh          # Start dev environment
./scripts/stop.sh           # Stop (preserves data)
./scripts/health-check.sh   # Verify all services
```

### Manual Docker Compose Commands

```bash
docker compose logs -f kurrentdb   # View logs
docker compose restart silo        # Restart a single service
docker compose up -d --build frontend  # Rebuild after code changes
```

### Access Points (Dev)

| Service | URL |
|---------|-----|
| Frontend | http://localhost:3000 |
| Silo API | http://localhost:5000 |
| Orleans Dashboard | http://localhost:8888 |
| KurrentDB Dashboard | http://localhost:2113 |
| PostgreSQL | localhost:5432 |
| pgAdmin | http://localhost:8081 |

## K3s + ArgoCD (Staging / Production)

For the WSL2 production machine. Full setup guide: **`docs/new-machine-setup.md`**.

### GitOps CI/CD Pipeline

```
git push → GitHub Actions → build image → push to ghcr.io
  → update image tag in K8s manifests (git commit)
  → ArgoCD detects manifest change → syncs to K3s → rolling update
```

| Component | Purpose |
|-----------|---------|
| GitHub Actions (`.github/workflows/ci.yaml`) | Builds Docker images, pushes to ghcr.io, updates manifest image tags |
| ghcr.io (`ghcr.io/lekhasy/vut/`) | Container registry |
| ArgoCD (`k8s/argocd/application.yaml`) | Watches `infrastructure/k8s/` and auto-syncs to K3s |

### After WSL Restart

```bash
cd Vut/infrastructure
./scripts/k3s-start.sh    # Single command — starts K3s, verifies ArgoCD, waits for pods
```

### Deploying Changes

```bash
# Just push to main — ArgoCD handles the rest
git push origin main
# GitHub Actions builds images → updates K8s manifests → ArgoCD deploys
```

## Directory Structure

```
infrastructure/
  docker-compose.yml                # Compose — local dev only
  docker-compose.override.arm.yml   # Dev overrides (ARM/Apple Silicon)
  docker-compose.override.amd64.yml # Dev overrides (AMD64/x86)
  .env.dev                          # Dev environment variables
  scripts/
    start.sh                        # Docker Compose dev startup
    stop.sh                         # Docker Compose dev shutdown
    health-check.sh                 # Docker Compose health check
    k3s-start.sh                    # K3s startup (WSL prod — run after restart)
  k8s/                              # Kubernetes manifests (K3s staging/prod)
    namespace.yaml
    ingress.yaml                    # Traefik ingress routing
    secrets/
    kurrentdb/
    postgresql/
    silo/
    projector-service/
    frontend/
    argocd/
      application.yaml              # ArgoCD auto-sync from git
    cloudflared/
    dev-setup.sh                    # One-command K3s bootstrap (first time)
  README.md                         # This file
```

## Troubleshooting

### Docker Compose

**Port conflicts:** Change the port in `.env.dev` (e.g., `FRONTEND_PORT=4321`).

**Container keeps restarting:** Check logs: `docker compose logs <service>`. Most common causes:
- KurrentDB: insufficient memory (increase `KURRENTDB_MEMORY_LIMIT`)
- PostgreSQL: data corruption (delete volume: `docker volume rm velucid-postgresql-data`)

### Kubernetes (K3s)

**Pods stuck in Pending:** Check node resources with `sudo k3s kubectl describe node`. Reduce memory limits in manifests if needed.

**ImagePullBackOff:** Ensure ghcr.io access is configured. Run `sudo k3s ctr images ls | grep velucid` to verify.

**CrashLoopBackOff:** Check logs: `sudo k3s kubectl logs -n velucid <pod-name>`. Common issue is secrets not being applied before deployments.

**Cloudflare Tunnel not connecting:** Verify credentials in the secret match the tunnel ID in the configmap. Check logs: `sudo k3s kubectl logs -n velucid -l app.kubernetes.io/name=cloudflared`.

## Notes

- Docker Compose is for **local development only**; K3s + ArgoCD handles staging/production
- K3s bundles Traefik (ingress) and local-path-provisioner (storage) — no extra setup
- Cloudflare Tunnel provides internet access without inbound ports or static IPs
- Orleans uses PostgreSQL for cluster membership and grain state — no separate message broker needed
- CI tags images with commit SHA for traceability; manifests are updated automatically by GitHub Actions
- ArgoCD auto-syncs K8s manifests from `infrastructure/k8s/` — git is the single source of truth
