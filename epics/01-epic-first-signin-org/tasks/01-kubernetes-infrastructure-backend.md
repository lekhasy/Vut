# Task 01: K3s Infrastructure Setup

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 01 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 3 days |

## Description

Set up the complete K3s (lightweight Kubernetes) infrastructure for the Vut platform on a single developer machine. This includes the namespace, all StatefulSets (KurrentDB single node, PostgreSQL single instance), Deployments (Orleans Silo, Projector Service, Frontend, Cloudflare Tunnel daemon), secrets, ConfigMaps, and base service definitions. The Orleans silo uses PostgreSQL (ADO.NET) for cluster membership — no external message broker is needed. Internet access is provided via **Cloudflare Tunnel** through the `vut.app` domain — no static IP or port forwarding required. This task is the foundation -- all other backend and frontend tasks depend on infrastructure being available.

## Architecture Reference

- Architecture doc Sections 9.1–9.7 (K3s manifests, Cloudflare Tunnel, Tailscale scaling)
- Architecture doc Section 3 (Component Diagram — K3s single-machine layout)
- Architecture doc Section 4 (Cluster Topology & Placement)

## Technical Requirements

### Namespace & Resource Quotas
- Create `k8s/namespace.yaml` with the `vut` namespace and labels `app.kubernetes.io/part-of: vut`.

### Secrets
- Create `k8s/secrets/vut-postgresql-secret.yaml` (base64-encoded username/password for PostgreSQL).
- Create `k8s/secrets/vut-auth0-secret.yaml` with keys: `domain`, `audience`, `client-id`, `client-secret`.
- Secrets must be templated for dev vs. prod (use envsubst or Helm values in future).

### KurrentDB StatefulSet
- **Single node** (not clustered), `k8s/kurrentdb/statefulset.yaml` and `k8s/kurrentdb/service.yaml`.
- Ports: 2113 (HTTP/API), 1113 (TCP).
- Env: `EVENTSTORE_INSECURE=true`, `EVENTSTORE_RUN_PROJECTIONS=None`, `EVENTSTORE_DB=/data/db`.
- PersistentVolumeClaim: 10Gi `ReadWriteOnce`, `storageClassName: local-path` (K3s default).
- **Replicas: 1** (single node for single-machine deployment).

### PostgreSQL StatefulSet
- Single instance, `k8s/postgresql/statefulset.yaml` and `k8s/postgresql/service.yaml`.
- Port 5432. Database: `vut_readmodel`.
- Credentials from secret `vut-postgresql-secret`.
- PersistentVolumeClaim: 5Gi `ReadWriteOnce`, `storageClassName: local-path` (K3s default).
- **Replicas: 1** (single instance for single-machine deployment).
- PostgreSQL serves dual purpose: **read model projections** AND **Orleans clustering tables** (`OrleansMembershipTable`, `OrleansMembershipVersionTable`). The Orleans ADO.NET clustering provider creates the clustering tables automatically on first silo startup.
- **Projected tables must follow a code-first approach, not database-first.** The read model schema is defined in application code (e.g., C# entity classes with EF Core migrations). PostgreSQL serves as the persistence layer only — schema changes are driven by code migrations, never by manual DDL scripts or direct database modifications. Do not create SQL init scripts or schema files for the projected tables. The application startup (or migration tooling) is responsible for creating and evolving the schema.

### Orleans Silo Deployment
- `k8s/silo/deployment.yaml` and `k8s/silo/service.yaml`.
- **1 replica** (single silo on single machine; increase when scaling via Tailscale).
- Ports: 5000 (HTTP API), 11111 (silo-to-silo), 30000 (Orleans gateway).
- Image: `vut/silo:latest` (placeholder for now).
- The ASP.NET Core API is co-hosted inside the silo — no separate API service is needed.
- Env vars:
  - `KurrentDb__ConnectionString`: KurrentDB connection string.
  - `ConnectionStrings__PostgreSQL`: PostgreSQL connection string (used for both Orleans clustering and read model queries).
  - `Orleans__ClusterId`: `vut-cluster`.
  - `Orleans__ServiceId`: `vut`.
  - `Auth0__Domain`, `Auth0__Audience`: from `vut-auth0-secret`.

### Projector Service Deployment
- `k8s/projector-service/deployment.yaml` and `k8s/projector-service/service.yaml`.
- 1 replica. No exposed ports (background worker subscribing to KurrentDB persistent subscriptions).
- Image: `vut/projector-service:latest` (placeholder for now).
- Env vars:
  - `KurrentDb__ConnectionString`: KurrentDB connection string.
  - `ConnectionStrings__PostgreSQL`: PostgreSQL connection string.

### Frontend Deployment (skeleton)
- `k8s/frontend/deployment.yaml` and `k8s/frontend/service.yaml`.
- **1 replica.**
- Port 3000. Image: `vut/frontend:latest` (placeholder).
- Env vars:
  - `SILO_API_URL`: `http://vut-silo:5000` (single backend URL for both reads and writes).
  - Auth0 config (`AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_CLIENT_SECRET`) from `vut-auth0-secret`.

### Cloudflare Tunnel Deployment
- `k8s/cloudflared/secret.yaml` — Cloudflare Tunnel credentials (base64-encoded `credentials.json`).
- `k8s/cloudflared/configmap.yaml` — Tunnel config mapping `vut.app` and `*.vut.app` to Traefik.
- `k8s/cloudflared/deployment.yaml` — 1 replica of `cloudflare/cloudflared:latest`.
- The `cloudflared` daemon establishes an **outbound-only** encrypted connection to Cloudflare's edge network. No static IP, port forwarding, or firewall rules needed on the dev machine.
- DNS: CNAME `vut.app` and `*.vut.app` → `<tunnel-id>.cfargotunnel.com` (configured in Cloudflare dashboard).
- Traffic flow: `User → vut.app (Cloudflare DNS) → Cloudflare Edge (TLS) → Cloudflare Tunnel → cloudflared pod → Traefik Ingress → vut-frontend`.

### Ingress
- Traefik is **bundled with K3s** — no separate NGINX installation needed.
- `k8s/ingress.yaml` with Traefik IngressRoute or standard Ingress rules routing `/api/*` to `vut-silo:5000`, `/auth/*` to frontend, everything else to frontend.

### Local Development Script
- `k8s/dev-setup.sh` that applies all manifests to the K3s cluster using `k3s kubectl` (or `kubectl` if kubeconfig is configured).
- Include a health-check loop that waits for all pods to be Ready.
- Prerequisite: K3s must be installed (`curl -sfL https://get.k3s.io | sh -`).

### Directory Structure
```
k8s/
  namespace.yaml
  ingress.yaml
  secrets/
    vut-postgresql-secret.yaml
    vut-auth0-secret.yaml
  cloudflared/
    secret.yaml
    configmap.yaml
    deployment.yaml
  kurrentdb/
    statefulset.yaml
    service.yaml
  postgresql/
    statefulset.yaml
    service.yaml
  silo/
    deployment.yaml
    service.yaml
  projector-service/
    deployment.yaml
    service.yaml
  frontend/
    deployment.yaml
    service.yaml
```

## Acceptance Criteria

- [ ] `k3s kubectl apply -f k8s/` succeeds on a K3s cluster.
- [ ] All pods reach `Running` and `Ready` state within 3 minutes.
- [ ] KurrentDB (single node) is reachable at `vut-kurrentdb:2113` inside the cluster.
- [ ] PostgreSQL is reachable at `vut-postgresql:5432` with database `vut_readmodel`.
- [ ] Orleans silo pod (1 replica) is running and registers in PostgreSQL membership table.
- [ ] Projector service pod is running.
- [ ] `cloudflared` pod is running and establishes tunnel to Cloudflare.
- [ ] Traefik ingress (K3s built-in) routes correctly to frontend and silo.
- [ ] `dev-setup.sh` runs end-to-end and reports success/failure for each component.
- [ ] All PVCs use `storageClassName: local-path`.

## Dependencies

- None. This is the first task. Can start immediately.

## Notes

- **K3s** is a lightweight certified Kubernetes distribution that runs as a single binary. Install via `curl -sfL https://get.k3s.io | sh -` (Linux/macOS, or WSL2 on Windows).
- K3s bundles **Traefik** as the default ingress controller — no separate installation needed.
- All `storageClassName` must be `local-path` (K3s default provisioner).
- For CI, the image tags should use the commit SHA or build number. Images can be imported into K3s via `k3s ctr images import` or pushed to a local registry.
- Helm chart conversion is a future improvement -- raw manifests are fine for Epic 1.
- The silo, projector-service, and frontend deployments will be updated in later tasks with actual images once the Dockerfiles are ready.
- **Cloudflare Tunnel** must be set up in the Cloudflare Zero Trust dashboard before deploying the `cloudflared` manifests. See Architecture doc Section 9.2 for detailed setup steps.
- **Scaling to multiple machines (future):** Additional dev machines can join the K3s cluster via **Tailscale** VPN mesh. Install Tailscale, join the tailnet, then run `curl -sfL https://get.k3s.io | K3S_URL=https://<tailscale-ip>:6443 K3S_TOKEN=<token> sh -` on agent nodes. Orleans discovers new silos automatically via the PostgreSQL membership table — no application code changes needed.
