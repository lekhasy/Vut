# Task 01: Kubernetes Infrastructure Setup

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 01 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 3 days |

## Description

Set up the complete Kubernetes infrastructure for the Vut platform. This includes the namespace, all StatefulSets (KurrentDB, Redpanda, PostgreSQL), secrets, ConfigMaps, and base service definitions. This task is the foundation -- all other backend and frontend tasks depend on infrastructure being available for local development (via minikube/kind) and CI/CD.

## Architecture Reference

- Architecture doc Sections 3.1-3.6 (Kubernetes manifests)
- Architecture doc Section 2 (Component Diagram)

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

### Redpanda StatefulSet
- 3-broker cluster, `k8s/redpanda/statefulset.yaml` and `k8s/redpanda/service.yaml`.
- Port 9092 (Kafka API), 9644 (Admin API).
- Command flags: `--smp 1 --memory 512M --overprovisioned --kafka-addr internal://0.0.0.0:9092`.
- Redpanda is used exclusively for Proto.Actor cluster transport (actor location routing, membership gossip). No topics need to be created — projectors subscribe directly to KurrentDB persistent subscriptions instead.

### PostgreSQL StatefulSet
- Single primary (replica later), `k8s/postgresql/statefulset.yaml` and `k8s/postgresql/service.yaml`.
- Port 5432. Database: `vut_readmodel`.
- Credentials from secret `vut-postgresql-secret`.
- **Projected tables must follow a code-first approach, not database-first.** The read model schema is defined in application code (e.g., C# entity classes with EF Core migrations). PostgreSQL serves as the persistence layer only — schema changes are driven by code migrations, never by manual DDL scripts or direct database modifications. Do not create SQL init scripts or schema files for the projected tables. The application startup (or migration tooling) is responsible for creating and evolving the schema.

### Actor Service Deployment (skeleton)
- `k8s/actor-service/deployment.yaml` and `k8s/actor-service/service.yaml`.
- Port 5000 (gRPC). Image: `vut/actor-service:latest` (placeholder for now).
- Env vars for KurrentDB, Redpanda, and Auth0 connections.

### Frontend Deployment (skeleton)
- `k8s/frontend/deployment.yaml` and `k8s/frontend/service.yaml`.
- Port 3000. Image: `vut/frontend:latest` (placeholder).
- Env vars for Actor Service URL, Read Model URL, and Auth0 config.

### Ingress
- `k8s/ingress.yaml` with NGINX or Traefik Ingress rules routing `/api/*` to actor-service, `/auth/*` to frontend, everything else to frontend.

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
  redpanda/
    statefulset.yaml
    service.yaml
  postgresql/
    statefulset.yaml
    service.yaml
  actor-service/
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
- [ ] Redpanda is reachable at `vut-redpanda:9092` inside the cluster.
- [ ] PostgreSQL is reachable at `vut-postgresql:5432` with database `vut_readmodel`.
- [ ] Ingress routes correctly to frontend and actor-service.
- [ ] `dev-setup.sh` runs end-to-end and reports success/failure for each component.

## Dependencies

- None. This is the first task. Can start immediately.

## Notes

- Use `imagePullPolicy: Never` for local dev (minikube/kind loads images from local Docker).
- For CI, the image tags should use the commit SHA or build number.
- Helm chart conversion is a future improvement -- raw manifests are fine for Epic 1.
- The actor-service and frontend deployments will be updated in later tasks with actual images once the Dockerfiles are ready.
