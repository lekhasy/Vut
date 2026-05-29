# OpenFGA Infrastructure

This directory contains OpenFGA deployment configurations for the Velucid platform.

## Overview

OpenFGA is a centralized authorization service using Fine-Grained Authorization (FGA).
All Silo pods communicate with a single OpenFGA instance over HTTP.

## Local Development

Start OpenFGA using the main docker compose stack:

```bash
cd infrastructure
docker compose up -d openfga
```

This starts OpenFGA and its PostgreSQL dependency only. Health check: `http://localhost:8080/healthz`

## Production Deployment (Kubernetes)

### Prerequisites

1. PostgreSQL database for OpenFGA state (can reuse existing `velucid-postgresql` instance)
2. Create the database: `velucid_openfga`

### Deployment Steps

1. Create namespace:
   ```bash
   kubectl apply -f namespace.yaml
   ```

2. Create secrets (update with actual credentials):
   ```bash
   kubectl apply -f secrets.yaml
   ```

3. Create configmap:
   ```bash
   kubectl apply -f configmap.yaml
   ```

4. Deploy:
   ```bash
   kubectl apply -f deployment.yaml
   kubectl apply -f service.yaml
   ```

5. Initialize store and model:
   - The application startup (`OpenFgaInitializer`) automatically creates the store (by name) and authorization model if they don't exist
   - Store name is logged on successful initialization

### Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `OPENFGA_STORE_NAME` | OpenFGA store name | `velucid` |
| `OPENFGA_DATASTORE_URI` | Postgres connection string | Required |

### Health Check

```bash
kubectl get pods -n velucid -l app.kubernetes.io/name=openfga
kubectl logs -n velucid -l app.kubernetes.io/name=openfga
```

## Architecture

```
┌─────────────────────────────────────────────────────┐
│  Production K3s                                     │
│                                                     │
│  ┌─────────────┐      ┌──────────────────────────┐  │
│  │ Silo Pod 1  │──HTTP→│  OpenFGA Service        │  │
│  └─────────────┘      │  (ClusterIP:8080)        │  │
│  ┌─────────────┐      │                          │  │
│  │ Silo Pod 2  │──HTTP→│  ┌────────────────────┐ │  │
│  └─────────────┘      │  │  Postgres Backend   │ │  │
│  ┌─────────────┐      │  │  (shared with app)  │ │  │
│  │ Silo Pod 3  │──HTTP→│  └────────────────────┘ │  │
│  └─────────────┘      └──────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

## Authorization Model

The Velucid authorization model defines:

- **organization**: The resource type being authorized
- **relations**: owner, member
- **permissions**:
  - `view_org`, `view_members`: owner or member
  - `create_task`: owner or member
  - `create_product`, `delete_product`, `invite_member`, `change_member_role`, `remove_member`, `delete_org`, `manage_org_settings`: owner only

See `backend/src/Velucid.Silo/Authorization/velucid-auth-model.fga` for the DSL model.
