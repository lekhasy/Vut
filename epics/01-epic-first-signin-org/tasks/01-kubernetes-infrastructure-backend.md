# Task 01: Kubernetes Infrastructure Setup

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 01 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 3 days |

## Description

Set up the complete Kubernetes infrastructure for the Vut platform. This includes the namespace, all StatefulSets (KurrentDB, PostgreSQL), Deployments (Orleans Silo, Projector Service, Frontend), secrets, ConfigMaps, and base service definitions. The Orleans silo uses PostgreSQL (ADO.NET) for cluster membership — no external message broker is needed. This task is the foundation -- all other backend and frontend tasks depend on infrastructure being available for local development (via minikube/kind) and CI/CD.

## Architecture Reference

- Architecture doc Sections 9.1-9.5 (Kubernetes manifests)
- Architecture doc Section 3 (Component Diagram)
- Architecture doc Section 4 (Cluster Topology & Placement)

## Technical Requirements

### Namespace & Resource Quotas
- Create `k8s/namespace.yaml` with the `vut` namespace and labels `app.kubernetes.io/part-of: vut`.

### Secrets
- Create `k8s/secrets/vut-postgresql-secret.yaml` (base64-encoded username/password for PostgreSQL).
- Create `k8s/secrets/vut-auth0-secret.yaml` with keys: `domain`, `audience`, `client-id`, `client-secret`.
- Secrets must be templated for dev vs. prod (use envsubst or Helm values in future).

### KurrentDB StatefulSet
- 3-node cluster, `k8s/kurrentdb/statefulset.yaml` and `k8s/kurrentdb/service.yaml`.
- Ports: 2113 (HTTP/API), 1113 (TCP).
- Env: `EVENTSTORE_CLUSTER_SIZE=3`, `EVENTSTORE_RUN_PROJECTIONS=None`, `EVENTSTORE_DB=/data/db`.
- PersistentVolumeClaim: 10Gi `ReadWriteOnce`.
- Disable TLS for dev (`EVENTSTORE_DEV=true` is acceptable for local).

### PostgreSQL StatefulSet
- Single primary (replica later), `k8s/postgresql/statefulset.yaml` and `k8s/postgresql/service.yaml`.
- Port 5432. Database: `vut_readmodel`.
- Credentials from secret `vut-postgresql-secret`.
- PostgreSQL serves dual purpose: **read model projections** AND **Orleans clustering tables** (`OrleansMembershipTable`, `OrleansMembershipVersionTable`). The Orleans ADO.NET clustering provider creates the clustering tables automatically on first silo startup.
- **Projected tables must follow a code-first approach, not database-first.** The read model schema is defined in application code (e.g., C# entity classes with EF Core migrations). PostgreSQL serves as the persistence layer only — schema changes are driven by code migrations, never by manual DDL scripts or direct database modifications. Do not create SQL init scripts or schema files for the projected tables. The application startup (or migration tooling) is responsible for creating and evolving the schema.

### Orleans Silo Deployment
- `k8s/silo/deployment.yaml` and `k8s/silo/service.yaml`.
- 3 replicas (for grain distribution across silo nodes).
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
- Port 3000. Image: `vut/frontend:latest` (placeholder).
- Env vars:
  - `SILO_API_URL`: `http://vut-silo:5000` (single backend URL for both reads and writes).
  - Auth0 config (`AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_CLIENT_SECRET`) from `vut-auth0-secret`.

### Ingress
- `k8s/ingress.yaml` with NGINX or Traefik Ingress rules routing `/api/*` to `vut-silo:5000`, `/auth/*` to frontend, everything else to frontend.

### Local Development Script
- `k8s/dev-setup.sh` that applies all manifests to a local minikube/kind cluster.
- Include a health-check loop that waits for all pods to be Ready.

### Directory Structure
```
k8s/
  namespace.yaml
  ingress.yaml
  secrets/
    vut-postgresql-secret.yaml
    vut-auth0-secret.yaml
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

- [ ] `kubectl apply -f k8s/` succeeds on a fresh minikube/kind cluster.
- [ ] All pods reach `Running` and `Ready` state within 3 minutes.
- [ ] KurrentDB is reachable at `vut-kurrentdb:2113` inside the cluster.
- [ ] PostgreSQL is reachable at `vut-postgresql:5432` with database `vut_readmodel`.
- [ ] Orleans silo pods (3 replicas) are running and form a cluster via PostgreSQL membership.
- [ ] Projector service pod is running.
- [ ] Ingress routes correctly to frontend and silo.
- [ ] `dev-setup.sh` runs end-to-end and reports success/failure for each component.

## Dependencies

- None. This is the first task. Can start immediately.

## Notes

- Use `imagePullPolicy: Never` for local dev (minikube/kind loads images from local Docker).
- For CI, the image tags should use the commit SHA or build number.
- Helm chart conversion is a future improvement -- raw manifests are fine for Epic 1.
- The silo, projector-service, and frontend deployments will be updated in later tasks with actual images once the Dockerfiles are ready.
