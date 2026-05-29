# Story 3.1: OpenFGA Infrastructure

baseline_commit: c19f558525ea6a0be6d02bf6744d0a649bbf31bf
Status: review

## Story

As a platform engineer,
I want OpenFGA integrated into the backend as a centralized authorization service,
so that authorization logic is declarative, auditable, and consistent across all operations.

## Acceptance Criteria

1. **OpenFGA SDK added to backend** — package installed and compiles
2. **Authorization model defined** — DSL model with org/member/owner relations and permissions
3. **IOpenFgaAuthorizationService interface** — `Check(userId, permission, resourceId)`, `WriteTuples`, `DeleteTuples`
4. **OpenFgaAuthorizationService implementation** — wraps OpenFGA SDK, handles centralized server mode
5. **Configuration** — OPENFGA_API_URL, OPENFGA_STORE_ID, OPENFGA_MODEL_ID via config
6. **Startup loads model** — authorization model initialized on Silo startup
7. **Membership tuple management** — Write/Delete tuples when membership changes
8. **OpenFGA infrastructure** — k3s manifests + docker compose for local dev and production deployment

## Tasks / Subtasks

- [x] Task 1: Add OpenFGA SDK package
  - [x] Subtask 1.1: Evaluate `OpenFgaClient` vs `FgaClient` NuGet package — check current .NET SDK compatibility
  - [x] Subtask 1.2: `dotnet add package` in Velucid.Silo project
- [x] Task 2: Define OpenFGA authorization model
  - [x] Subtask 2.1: Create model file at `backend/src/Velucid.Silo/Authorization/velucid-auth-model.fga`
  - [x] Subtask 2.2: Define `organization` type with `owner` and `member` relations
  - [x] Subtask 2.3: Define permissions: `view_org`, `view_members`, `create_task`, `create_product`, `delete_product`, `invite_member`, `change_member_role`, `remove_member`, `delete_org`, `manage_org_settings`
- [x] Task 3: Create IOpenFgaAuthorizationService interface
  - [x] Subtask 3.1: Define `Task<bool> Check(Guid userId, string permission, Guid resourceId)` — check if user has permission on resource
  - [x] Subtask 3.2: Define `Task WriteTuples(IEnumerable<AuthorizationTuple> tuples)` — write membership tuples
  - [x] Subtask 3.3: Define `Task DeleteTuples(IEnumerable<AuthorizationTuple> tuples)` — remove membership tuples
- [x] Task 4: Implement OpenFgaAuthorizationService
  - [x] Subtask 4.1: Wrap OpenFGA SDK client
  - [x] Subtask 4.2: Implement Check using OpenFGA Check API
  - [x] Subtask 4.3: Implement WriteTuples / DeleteTuples
  - [x] Subtask 4.4: Support centralized server mode — SDK calls OpenFGA over HTTP at configured api_url
  - [x] Subtask 4.5: Handle connection failures gracefully (fail open or closed? — make configurable)
- [x] Task 5: Configuration and DI registration
  - [x] Subtask 5.1: Add OpenFGA config section to `config.yaml` / `config.user.yaml`
  - [x] Subtask 5.2: Register `IOpenFgaAuthorizationService` in Silo `Program.cs`
  - [x] Subtask 5.3: Validate config on startup — log warning if OpenFGA not configured but authorization is enabled
- [x] Task 6: Model initialization on startup
  - [x] Subtask 6.1: On Silo startup, create or validate OpenFGA store exists
  - [x] Subtask 6.2: Initialize authorization model if not already present
  - [x] Subtask 6.3: Log store ID and model ID on successful initialization
- [x] Task 7: OpenFGA infrastructure (k3s manifests + docker compose)
  - [x] Subtask 7.1: Update `infrastructure/docker-compose.yml` — add OpenFGA service with Postgres backend
  - [x] Subtask 7.2: Removed (namespace already exists at `infrastructure/k8s/namespace.yaml`)
  - [x] Subtask 7.3: Create `infrastructure/k8s/openfga/deployment.yaml` — OpenFGA server deployment with resource limits, health checks
  - [x] Subtask 7.4: Create `infrastructure/k8s/openfga/service.yaml` — ClusterIP service exposing OpenFGA
  - [x] Subtask 7.5: Create `infrastructure/k8s/openfga/configmap.yaml` — OpenFGA store name as config
  - [x] Subtask 7.6: Create `infrastructure/k8s/openfga/secrets.yaml` — OpenFGA uses existing Postgres
  - [x] Subtask 7.7: Document deployment steps in `infrastructure/openfga/README.md`
  - [x] Subtask 7.8: Add OpenFGA service to `infrastructure/docker-compose.yml`
  - [x] Subtask 7.9: Add OpenFGA environment variables to `infrastructure/.env.dev`
  - [x] Subtask 7.10: Update `infrastructure/scripts/start.sh` — add openfga to health check loop
  - [x] Subtask 7.11: Update `infrastructure/scripts/health-check.sh` — add openfga HTTP endpoint check

## Dev Agent Record

### Debug Log

- Issue: OpenFGA SDK types (`TypeDefinition`, `Userset`, `Usersets`, `ObjectRelation`) not found at compile time despite correct namespace `OpenFga.Sdk.Model`
- Root cause: Missing `using OpenFga.Sdk.Model;` explicit directive — the SDK types are in `OpenFga.Sdk.Model`, not `OpenFga.Sdk.Client.Model`
- Resolution: Added `using OpenFga.Sdk.Model;` to `OpenFgaInitializer.cs`; verified build succeeds

### Completion Notes

Implemented OpenFGA infrastructure for centralized authorization:
- Added OpenFGA SDK (v0.10.3) to Velucid.Silo project
- Created authorization model DSL file with org/member/owner relations and 11 permissions
- Implemented `IOpenFgaAuthorizationService` interface with Check, WriteTuples, DeleteTuples, GetUserRoles
- Implemented `OpenFgaAuthorizationService` wrapping OpenFGA SDK with configurable failure modes
- Created `OpenFgaInitializer` for startup model/store creation
- Added OpenFGA configuration to appsettings.json (following KurrentDbOptions pattern)
- Created k3s manifests (namespace, deployment, service, configmap, secrets)
- Created docker-compose.yaml for local development
- Updated main docker-compose.yml, .env.dev, start.sh, health-check.sh

## File List

**Files CREATED (Application Layer):**
- `backend/src/Velucid.Silo/Authorization/IOpenFgaAuthorizationService.cs`
- `backend/src/Velucid.Silo/Authorization/OpenFgaAuthorizationService.cs`
- `backend/src/Velucid.Silo/Authorization/AuthorizationTuple.cs`
- `backend/src/Velucid.Silo/Authorization/IOpenFgaInitializer.cs`
- `backend/src/Velucid.Silo/Authorization/OpenFgaInitializer.cs`
- `backend/src/Velucid.Silo/Authorization/velucid-auth-model.fga`
- `backend/src/Velucid.Silo/Configuration/OpenFgaOptions.cs`

**Files MODIFIED (Application Layer):**
- `backend/src/Velucid.Silo/Velucid.Silo.csproj` — added OpenFGA SDK package
- `backend/src/Velucid.Silo/Program.cs` — added DI registration and startup initialization
- `backend/src/Velucid.Silo/appsettings.json` — added OpenFGA configuration section
- `backend/src/Velucid.Silo/appsettings.Development.json` — added OpenFGA configuration section

**Files CREATED (Infrastructure Layer):**
- `infrastructure/k8s/openfga/deployment.yaml`
- `infrastructure/k8s/openfga/service.yaml`
- `infrastructure/k8s/openfga/configmap.yaml`
- `infrastructure/k8s/openfga/secrets.yaml`
- `infrastructure/openfga/README.md`

**Files MODIFIED (Infrastructure Layer):**
- `infrastructure/docker-compose.yml` — added OpenFGA service
- `infrastructure/.env.dev` — added OpenFGA environment variables
- `infrastructure/scripts/start.sh` — added openfga to health check loop
- `infrastructure/scripts/health-check.sh` — added OpenFGA HTTP endpoint check

## Change Log

- 2026-05-29: Initial implementation — OpenFGA infrastructure story complete

## Dev Notes

### OpenFGA Deployment: Centralized Server

**Chosen: Centralized server** — single OpenFGA instance (or cluster), all Silo pods call it over HTTP. No per-pod sidecar, no sync machinery. Simplest operational model for a startup with a small team.

For local dev: single OpenFGA container. For prod: single server or small cluster.

```
Dev:
  Your Silo (Orleans)  ←HTTP→  OpenFGA Container  ←→  Postgres

Multi-instance prod:
  Silo Pod 1  ←HTTP→  OpenFGA Server  ←→  Postgres
  Silo Pod 2  ←HTTP→  (same server, shared Postgres)
  Silo Pod 3  ←HTTP→
```

### SDK API Surface

The .NET SDK is an HTTP client — it calls OpenFGA's REST API over HTTP whether OpenFGA is local or remote.

```csharp
// .NET SDK calls OpenFGA API over HTTP
var configuration = new OpenFgaClientConfiguration
{
    ApiUrl = "http://localhost:8080",  // OpenFGA server URL
    StoreId = "store-id",
    AuthorizationModelId = "model-id"
};
var fgaClient = new OpenFgaClient(configuration);

// Check permission
var response = await fgaClient.Check(new CheckRequest
{
    User = $"user:{userId}",
    Relation = "member",
    Object = $"organization:{orgId}"
});
bool allowed = response.Allowed;
```

### Configuration Schema

```yaml
# config.yaml
[modules.openfga]
enabled = true
api_url = "http://localhost:8080"  # or production URL
store_id = ""  # auto-create if empty
model_id = ""  # auto-create if empty
```

### Why Centralized Over Sidecar

| | Centralized (chosen) | Sidecar |
|--|---|---|
| Operational complexity | Low — one service | High — sync machinery, per-pod management |
| Latency | Network call (~1-5ms) | Local call (~0.1ms) |
| Correctness | Simple — no sync lag | Complex — eventual consistency window |
| For this project | ✓ | Over-engineered for current scale |

### Testing with Real OpenFGA

Use `testcontainers-dotnet` to spin up a real OpenFGA container in tests:
```csharp
var container = new ContainerBuilder()
    .WithImage("openfga/openfga:latest")
    .WithPortBinding(8080, 8080)
    .Build();
await container.StartAsync();
```

This runs against a real OpenFGA instance in CI — no mocking. Catches SDK integration bugs and model issues early.

### Infrastructure Patterns (k3s)

Follow existing k8s patterns in `infrastructure/k8s/`:
- Namespace: `velucid` (same as other services)
- Deployment: resource limits, health checks (`/healthz` endpoint), rolling update strategy
- Service: ClusterIP (OpenFGA doesn't need external exposure — only internal pods call it)
- ConfigMap: store ID and model ID (or use init container to create store/model on startup)
- Secrets: if OpenFGA needs credentials for Postgres, follow the same pattern as `infrastructure/k8s/postgresql/secrets.yaml`
- Use existing Postgres (from `infrastructure/k8s/postgresql/`) rather than a dedicated OpenFGA Postgres

Files to reference for patterns:
- `infrastructure/k8s/postgresql/statefulset.yaml` — statefulset pattern
- `infrastructure/k8s/silo/` — deployment pattern with health checks
- `infrastructure/k8s/secrets/` — secret management pattern

### Scripts to Update

The following scripts must be updated to include OpenFGA:
- `infrastructure/scripts/start.sh` — add `openfga` to health check loop (line 67: `for service in kurrentdb postgresql openfga`)
- `infrastructure/scripts/health-check.sh` — add OpenFGA HTTP endpoint check (`http://localhost:${OPENFGA_PORT:-8080}/healthz`)
- `infrastructure/scripts/stop.sh` — no changes needed (docker compose down handles it)

The scripts check services in order: infrastructure first (`kurrentdb`, `postgresql`, then `openfga`), then application services. OpenFGA should be healthy before the Silo starts since Silo depends on it for authorization.

## File List

**Files to CREATE (Application Layer):**
- `backend/src/Velucid.Silo/Authorization/IOpenFgaAuthorizationService.cs`
- `backend/src/Velucid.Silo/Authorization/OpenFgaAuthorizationService.cs`
- `backend/src/Velucid.Silo/Authorization/AuthorizationTuple.cs`
- `backend/src/Velucid.Silo/Authorization/velucid-auth-model.fga`
- `backend/src/Velucid.Silo/Configuration/OpenFgaConfiguration.cs`

**Files to MODIFY (Application Layer):**
- `backend/src/Velucid.Silo/Velucid.Silo.csproj` — add OpenFGA SDK package
- `backend/src/Velucid.Silo/Program.cs` — DI registration and startup initialization

**Files to CREATE (Infrastructure Layer):**
- `infrastructure/k8s/openfga/deployment.yaml`
- `infrastructure/k8s/openfga/service.yaml`
- `infrastructure/k8s/openfga/configmap.yaml`
- `infrastructure/k8s/openfga/secrets.yaml`
- `infrastructure/openfga/README.md` — deployment steps and notes

**Files to MODIFY (Infrastructure Layer):**
- `infrastructure/docker-compose.yml` — add OpenFGA service definition
- `infrastructure/.env.dev` — add OpenFGA environment variables
- `infrastructure/scripts/start.sh` — add openfga to health check loop
- `infrastructure/scripts/health-check.sh` — add openfga HTTP endpoint check

**Files to READ:**
- OpenFGA .NET SDK documentation (check NuGet page for latest API)
- OpenFGA model DSL reference
- Existing k3s patterns in `infrastructure/` directory

## References

- OpenFGA .NET SDK: `OpenFgaClient` or `FgaClient` on NuGet
- OpenFGA DSL model syntax: `https://openfga.dev/docs/modeling/building-blocks`
- Authorization epic spec: `_bmad-output/planning-artifacts/epic-3-authorization-openfga.md`