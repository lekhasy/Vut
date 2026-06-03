---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-05-28'
inputDocuments:
  - PRD.md
  - docs/velucid_forecasting_spec.md
  - docs/auth0-setup.md
  - docs/new-machine-setup.md
workflowType: 'architecture'
project_name: 'Velucid'
user_name: 'Syle'
date: '2026-05-28'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
- Identity & Access: Auth0 + GitHub SSO only (MVP). No email verification gate. No account auto-linking by email (deferred to future).
- Organizations: GitHub-style multi-tenant model (owners, members), invite/accept flow, per-org role enforcement
- Products: Container for work with configurable statuses; first status is backlog-only (excluded from kanban)
- Tasks: Event-sourced CRUD with soft-delete; tracks title, description, status, tags (namespace:value format)
- Backlog View: All tasks filterable by status/tags/text, sortable; loads <1s for up to 10k tasks
- Kanban Board: Non-backlog statuses as columns, drag-and-drop triggers TaskStatusChanged event, per-user saved filter views
- Probabilistic Forecast: Monte Carlo simulation (10,000 runs), two data series only (completed count + scope count), S-curve CDF and dual-cone progress chart, threshold slider 50–99%, never-finish detection

**Non-Functional Requirements:**
- Zero time-centric features in the product (hard constraint)
- Self-hostable: no cloud provider dependency, runs on team hardware
- Backlog loads in <1s for 10,000 tasks
- Kanban drag-and-drop reflects in <200ms (optimistic update)
- 60%+ activation rate target within 7 days
- Eventual consistency acceptable for read models (masked by optimistic UI)
- HTTPS only, no secrets in events, actor+timestamp on all events

**Scale & Complexity:**
- Primary domain: full-stack web (Astro SSR/BFF + .NET 10 Orleans API + event-sourced backend)
- Complexity level: medium-high
- Estimated architectural components: 8–12 major components

### Technical Constraints & Dependencies

- **Event store**: KurrentDB — catch-up subscriptions + secondary indexes for read model projections (no persistent subscriptions)
- **Read model**: PostgreSQL (EF Core 10) with projections rebuilt via catch-up subscriptions in ProjectorService
- **Auth**: Auth0 with GitHub social connection only
- **Email**: Resend (for future verification flows; not active in MVP)
- **Deployment target**: K3s on WSL2 (prod); Docker Compose (local dev, currently not actively maintained)
- **CI/CD**: GitHub Actions + ArgoCD (gitops workflow)
- **Secrets**: Infisical for production K8s secrets management

### Cross-Cutting Concerns Identified

1. **Event sourcing everywhere**: All mutations emit events. Read models are projections via catch-up subscriptions. The forecast engine consumes the event stream directly.
2. **Tenant isolation**: Every query scoped to org membership. API layer enforces isolation — no direct DB access by clients.
3. **Optimistic UI**: Status changes reflected immediately; event processing happens asynchronously. Read models may lag briefly.
4. **Forecast computation**: Server-side Monte Carlo (10k sims), CDF cached and returned to client. Threshold slider updates without re-simulation. Not yet implemented.
5. **BFF auth pattern**: Astro is the sole authentication entry point. All client requests flow through Astro which validates the session cookie and proxies to Silo API.
6. **Catch-up subscription pattern**: Projections rebuild from checkpoint by subscribing to $ce stream from last processed sequence. Secondary indexes in KurrentDB serve point queries (e.g., "all tasks for product X").

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- BFF architecture: Astro as auth entry point and API gateway
- Orleans grain model for business logic (user/identity already implemented)
- KurrentDB as event store with catch-up subscription projections
- PostgreSQL read model via EF Core

**Important Decisions (Shape Architecture):**
- Redis dual-use (Orleans clustering + session/token store)
- nanostores for frontend client state
- Tailwind CSS for styling
- K3s + ArgoCD deployment (gitops)

**Deferred Decisions (Post-MVP):**
- Probabilistic forecast / Monte Carlo engine
- Swagger/OpenAPI documentation (add when API stabilizes after org/product/task modules)
- Redpanda / messaging layer (KurrentDB catch-up subscriptions handle projection delivery directly; Redpanda not needed at MVP scale)
- Email verification flow
- Account auto-linking by email

### Data Architecture

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Event store | KurrentDB | 26.1.0 | HTTP atom interface, catch-up subscriptions, secondary indexes. Self-hostable. |
| Read model DB | PostgreSQL | 16-alpine | EF Core 10 + Npgsql. Used for all projections (orgs, products, tasks, users). |
| Grain persistence | Orleans memory (dev) / KurrentDB | — | Ephemeral grains; all state persisted as events in KurrentDB. |
| Projection delivery | Catch-up subscriptions | — | ProjectorService subscribes to $ce stream from last checkpoint. No persistent subscriptions. |

### Authentication & Security

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Identity provider | Auth0 | — | GitHub SSO only (MVP). Handled entirely by Astro BFF. |
| Session management | Encrypted cookie | jose 6.2.3 | AES-GCM encrypted cookie via `jose`. Stateless Astro — no server-side session store. |
| Session store | Redis (token store) + cookie | — | Redis used by Silo for Orleans clustering and email verification token store (email verification deferred). |
| API authorization | Org membership scoped | — | Every Silo API query enforces org membership. Tenant isolation at API layer. |
| Orleans clustering | Redis | Microsoft.Orleans.Clustering.Redis 10.1.0 | Redis-based clustering for multi-pod Silo deployment. |

### API & Communication Patterns

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Call chain | Client → Astro (BFF) → Silo (gRPC/HTTP) → KurrentDB | — | BFF pattern: Astro is sole auth entry point. Silo API is internal. |
| Auth flow | Auth0 GitHub SSO → session cookie → Astro middleware | — | All client requests carry session cookie. Astro validates and proxies to Silo. |
| Inter-service | Orleans grains + Silo API controllers | — | Business logic in grains (UserGrain, IdentityGrain). Silo API controllers call grains. |
| Orleans intra-cluster | gRPC | — | Grains communicate via Orleans runtime over gRPC within the Silo cluster. |

### Frontend Architecture

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Framework | Astro | 5.8.0 | SSR with React islands for interactive components. BFF role. |
| Interactive components | React 18 | — | Islands within Astro for kanban, task cards, etc. |
| Client state | nanostores | 0.11.4 | Lightweight framework-agnostic store for cross-component state. |
| Styling | Tailwind CSS | 3.4.17 | Utility-first. Design system tokens per project. |
| Auth (client) | Astro middleware | — | Cookie validated server-side in Astro `middleware.ts`. Session data attached to `context.locals`. |
| Local-first read model | Kurrent TypeScript sync engine + shared `@velucid/projection` | — | Web client maintains its own read model by projecting events from the event log in the browser. The same projection code runs in `apps/projector` (backend Node worker) and in `apps/web` (frontend bundle) — no drift. Writes still flow through Astro BFF → Silo → KurrentDB. Backend projector is the source of truth for the Postgres read model; the browser store is a derived cache. Forward-looking: requires Epic 4 (Nx monorepo migration) and the upcoming Kurrent TypeScript sync engine release. |

### Infrastructure & Deployment

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Production host | K3s on WSL2 | — | Self-hosted. No cloud provider dependency. Per `new-machine-setup.md`. |
| Container orchestration | K3s + ArgoCD | — | Gitops: push to main → CI builds images → ArgoCD syncs manifests → K3s deploys. |
| Secrets (prod) | Infisical | — | Infisical Operator syncs secrets to K8s. No secrets in git. |
| Secrets (local) | `.env` | — | `infrastructure/scripts/start.sh` reads `.env` for local Docker Compose. |
| Container registry | GitHub Container Registry (ghcr.io) | — | Images: `ghcr.io/lekhasy/vut/{silo,frontend,projector-service}:<sha>` |
| Service mesh | Traefik (K3s bundled) | — | Ingress routing, TLS termination via Cloudflare Tunnel. |

### Implementation Sequence

1. **User/Identity** (already done) — login, session cookie, user grain + projection
2. **Organizations** — Org grain, OrgProjection, Astro API endpoints, frontend org management UI
3. **Products** — Product grain, ProductProjection, status configuration, product UI
4. **Tasks** — Task grain, TaskProjection, backlog + kanban board, drag-and-drop
5. **Tags** — Tag handling in task grain, filter support in backlog/kanban
6. **Saved Views** — Per-user filter view storage and recall
7. **Forecast** (deferred) — Monte Carlo engine, CDF computation, forecast UI

### Cross-Component Dependencies

- **Astro middleware** depends on Redis for session decryption and on Auth0 for token validation
- **Silo API** depends on KurrentDB (events), PostgreSQL (read model), Redis (Orleans clustering)
- **ProjectorService** depends on KurrentDB (catch-up) and PostgreSQL (write projections)
- **All components** depend on Auth0 for user identity

## Implementation Patterns & Consistency Rules

### Critical Conflict Points Identified: 7

### Naming Patterns

**Event Type Names (KurrentDB strings):**
- Independent of CLR type name — always registered explicitly via `EventTypeMapping.Register<TEvent>("StringName")`
- Format: PascalCase noun phrase (e.g., `"UserCreated"`, `"OrgMemberAdded"`, `"TaskStatusChanged"`, `"TaskDeleted"`)
- Must be registered at startup before `EventTypeMapping.Freeze()` is called
- Event C# record class name is independent — the string name is the stable identity in the event stream

**Grain Interfaces & Types:**
- Interface: `I{Name}Grain` (e.g., `IUserGrain`, `IOrgGrain`)
- Grain class: `{Name}Grain` with `[GrainType("short-name")]` attribute on the class
- Short-name format: lowercase singular noun (e.g., `"user"`, `"org"`, `"product"`, `"task"`)
- Example:
  ```csharp
  [GrainType("user")]
  public class UserGrain : EventSourcedGrain<UserState>, IUserGrain { ... }
  ```
- Orleans uses `[GrainType]` internally for grain routing — clients still call `GetGrain<I{Name}Grain>(id)`

**API Routes:**
- Plural resource names: `/api/orgs`, `/api/products`, `/api/tasks`
- Route parameters: `{name:guid}` for IDs, e.g., `/api/orgs/{orgId:guid}/products`
- No verb in route — actions differentiated by HTTP method (GET, POST, PUT, DELETE)

**Database Tables (EF Core):**
- Entity classes: PascalCase, entity noun (e.g., `UserProjection`, `OrgProjection`, `ProductProjection`)
- Table name: not explicitly configured — EF defaults to `{EntityName}s` (e.g., `UserProjections`)
- Columns: PascalCase C# property names (e.g., `UserId`, `DeletedAt`)
- Soft-delete column: `DeletedAt` (nullable `timestamps` in PostgreSQL) — not `IsDeleted` or `Active`

**Event Payload Fields (C# records):**
- PascalCase throughout (e.g., `UserId`, `DisplayName`, `TaskId`)
- Every event: `ActorId` (Guid) and `Timestamp` (DateTimeOffset) required
- Nullable fields marked `?` where semantically appropriate
- Example:
  ```csharp
  public record TaskStatusChangedEvent(
      Guid TaskId,
      Guid ProductId,
      string OldStatus,
      string NewStatus,
      Guid ActorId,
      DateTimeOffset Timestamp
  ) : IEvent;
  ```

**Frontend JSON:**
- snake_case throughout (e.g., `user_id`, `display_name`, `task_id`)
- .NET System.Text.Json handles conversion globally:
  ```csharp
  builder.Services.Configure<JsonSerializerOptions>(options =>
  {
      options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
      options.PropertyNameCaseInsensitive = true;
  });
  ```

### Structure Patterns

**Backend (.NET Silo):**
```
Velucid.Silo/Grains/
  UserGrain.cs          ← grain class + [GrainType("user")]
  IUserGrain.cs         ← grain interface
  UserState.cs         ← state record
  OrgGrain.cs           ← new grains follow same pattern
  IOrgGrain.cs
  OrgState.cs
Events/
  UserCreatedEvent.cs   ← event records
  IEvent.cs
  EventTypeMapping.cs   ← all EventTypeMapping.Register() calls (do not modify)
Controllers/
  UserController.cs     ← API endpoints + inline DTO records
Models/
  CreateUserResult.cs   ← cross-grain result types only
```
- DTO records stay co-located with their controller file
- New grains go in `Grains/`, events in `Events/`, controllers in `Controllers/` — no sub-folders

**ReadModel (EF Core):**
```
Velucid.ReadModel/Entities/
  UserProjection.cs
  OrgProjection.cs
  ProductProjection.cs
  TaskProjection.cs
  ProjectionCheckpoint.cs
ReadModelDbContext.cs
```
- No sub-folders — all projection entities at the same level
- One entity class per aggregate root

**Frontend (Astro):**
```
src/pages/api/           ← Astro API endpoints
  orgs/
    index.ts            ← GET /api/orgs, POST /api/orgs
    [orgId]/
      index.ts          ← GET/PUT/DELETE /api/orgs/{orgId}
      products/
        index.ts        ← /api/orgs/{orgId}/products
  tasks/
    index.ts
    [taskId]/
      index.ts
  members/
    index.ts
src/components/
  ui/                   ← shared UI primitives (Button, Modal, Toast, etc.)
  kanban/               ← kanban-specific components (future)
src/lib/
  api.ts                ← apiFetch wrapper
  auth/                 ← session utilities
src/stores/             ← nanostores atoms
```

### Format Patterns

**API Response — Success:**
- Raw resource object or array — no envelope (`{ data: ... }`)
- HTTP 200 OK, 201 Created, 204 No Content
- POST to create returns full resource with server-assigned ID

**API Response — Error:**
- Always: `{ "message": "human-readable string" }`
- HTTP status codes: 401 (unauthenticated), 403 (unauthorized), 404 (not found), 409 (conflict), 422 (validation error), 500 (server error)

**Date/Time:**
- UTC ISO-8601 strings in JSON: `"2026-05-28T15:30:00Z"`
- No Unix timestamps — always wall-clock time with timezone

**GUIDs:**
- Standard UUID v4 format in JSON: `"f47ac10b-58cc-4372-a567-0e02b2c3d479"`

### Communication Patterns

**Event Naming:**
- Past-tense noun phrases: `UserCreated`, `OrgMemberAdded`, `TaskStatusChanged`, `TaskDeleted`
- One event per state transition — no combined events
- Event name encodes what happened, never intent or future state

**State Update Flow:**
```
User drag-drop
  → Astro API route (src/pages/api/...)
  → Silo controller → grain
  → KurrentDB appends Event
  → ProjectorService catch-up subscription (async, background)
  → PostgreSQL projection updated
  → Astro reads updated state on next request
```

**Optimistic Updates:**
- UI reflects change immediately on user action
- If Silo call fails → revert UI state + show toast error
- No polling — reads served from Astro's PostgreSQL read model

### Process Patterns

**Error Handling (Astro):**
```typescript
try {
  const result = await apiFetch('/api/orgs', { method: 'POST', body: JSON.stringify(data) });
  // do something with result
} catch (err) {
  showToast({ type: 'error', message: err.message });
}
```

**Loading States:**
- Per-component loading state via nanostores atom: `isLoading = atom(false)`
- No global loading overlay for routine operations
- Skeleton states for initial page loads

**Validation:**
- Client-side: basic required-field checks before submit
- Server-side (Silo): always validate — never trust client data, even after client validation
- On validation failure: HTTP 422 with `{ "message": "Field X is required or invalid" }`

### Enforcement Guidelines

**All AI Agents MUST:**
- Place `[GrainType("short-name")]` on every grain class (never on the interface)
- Register every event in `EventTypeMapping.Register<TEvent>("PascalCaseName")` before `Freeze()`
- Configure `System.Text.Json` with `JsonNamingPolicy.SnakeCaseLower` globally — no per-property attributes
- Use `DeletedAt` (nullable `DateTime`) for soft-delete, not `IsDeleted` or `Active`
- Place all BFF endpoints in `src/pages/api/` as Astro API routes
- Make all Silo calls from Astro server-side code only — never call KurrentDB or PostgreSQL directly from Astro
- Return `{ "message": "..." }` for all error responses; include `ActorId` and `Timestamp` on every event
- No persistent subscriptions — use catch-up subscriptions only in ProjectorService

## Project Structure & Boundaries

### Complete Project Directory Structure

```
Vut/
├── backend/src/
│   ├── Velucid.Silo/              # Orleans API host (Silo + ASP.NET Core)
│   │   ├── Configuration/
│   │   │   └── KurrentDbOptions.cs
│   │   ├── Controllers/
│   │   │   ├── UserController.cs
│   │   │   └── OrgController.cs    # new (Organizations API)
│   │   ├── Events/
│   │   │   ├── IEvent.cs
│   │   │   ├── IEventStreamClient.cs
│   │   │   ├── KurrentDbStreamClient.cs
│   │   │   ├── EventTypeMapping.cs   # all Register() calls - do not rename entries
│   │   │   ├── UserCreatedEvent.cs
│   │   │   └── ...                    # future: OrgCreated, TaskCreated, etc.
│   │   ├── Grains/
│   │   │   ├── EventSourcedGrain.cs   # base class for all event-sourced grains
│   │   │   ├── UserGrain.cs           # [GrainType("user")]
│   │   │   ├── IUserGrain.cs
│   │   │   ├── UserState.cs
│   │   │   ├── OrgGrain.cs             # new
│   │   │   ├── IOrgGrain.cs            # new
│   │   │   ├── OrgState.cs             # new
│   │   │   ├── ProductGrain.cs        # new
│   │   │   ├── IProductGrain.cs       # new
│   │   │   ├── ProductState.cs        # new
│   │   │   ├── TaskGrain.cs            # new
│   │   │   ├── ITaskGrain.cs          # new
│   │   │   └── TaskState.cs            # new
│   │   ├── Models/                     # cross-grain result DTOs only
│   │   │   └── CreateUserResult.cs
│   │   ├── Services/
│   │   │   ├── ISignInService.cs
│   │   │   ├── SignInService.cs
│   │   │   └── IEmailVerificationStore.cs  # deferred - email verification not MVP
│   │   └── Program.cs
│   ├── Velucid.ReadModel/          # EF Core PostgreSQL projections
│   │   ├── Entities/
│   │   │   ├── UserProjection.cs
│   │   │   ├── OrgProjection.cs          # exists - confirm completeness
│   │   │   ├── OrgMemberProjection.cs     # exists
│   │   │   ├── OrgInvitationProjection.cs
│   │   │   ├── UserOrgProjection.cs       # exists
│   │   │   ├── ProductProjection.cs      # new
│   │   │   ├── TaskProjection.cs          # new
│   │   │   └── ProjectionCheckpoint.cs
│   │   ├── ReadModelDbContext.cs
│   │   └── Velucid.ReadModel.csproj
│   ├── Velucid.ProjectorService/   # Background catch-up worker
│   │   ├── Handlers/
│   │   │   └── UserProjector.cs         # existing - add handlers for new projections
│   │   ├── Configuration/
│   │   ├── Program.cs
│   │   └── Velucid.ProjectorService.csproj
│   ├── Velucid.ReadModel.Migrations/
│   │   ├── Migrations/
│   │   └── Program.cs
│   └── tests/
├── docs/
│   ├── auth0-setup.md
│   ├── new-machine-setup.md
│   └── velucid_forecasting_spec.md
├── frontend/src/
│   ├── components/
│   │   ├── ui/                      # shared primitives (Button, Modal, Toast, etc.)
│   │   ├── sidebar/
│   │   └── kanban/                   # new - when kanban board is implemented
│   ├── layouts/
│   │   ├── AppLayout.astro
│   │   └── AuthLayout.astro
│   ├── lib/
│   │   ├── api.ts                   # apiFetch wrapper
│   │   └── auth/                    # session utilities (session.ts, jwt.ts, auth0.ts)
│   ├── middleware.ts                # Astro auth middleware
│   ├── pages/
│   │   ├── api/                     # BFF routes — proxy to Silo after auth
│   │   │   ├── auth/
│   │   │   │   ├── sign-in.ts       # POST /api/auth/sign-in
│   │   │   │   └── callback.ts      # GET /api/auth/callback
│   │   │   └── orgs/
│   │   │       ├── index.ts          # GET/POST /api/orgs
│   │   │       └── [orgId]/
│   │   │           ├── index.ts     # GET/PUT/DELETE /api/orgs/{orgId}
│   │   │           ├── products/
│   │   │           │   └── index.ts # GET/POST /api/orgs/{orgId}/products
│   │   │           ├── members/
│   │   │           │   └── index.ts
│   │   │           └── invitations/
│   │   ├── tasks/
│   │   │   ├── index.ts            # GET/POST /api/tasks
│   │   │   └── [taskId]/
│   │   │       └── index.ts        # GET/PUT/DELETE /api/tasks/{taskId}
│   │   ├── auth/
│   │   ├── dashboard.astro
│   │   └── orgs/
│   │       └── [orgId]/
│   ├── stores/                      # nanostores atoms
│   │   ├── auth.ts
│   │   ├── types.ts
│   │   ├── organizations.ts
│   │   └── invitations.ts
│   └── styles/
├── infrastructure/
│   ├── k8s/                        # K8s manifests (per service)
│   │   ├── argocd/
│   │   ├── frontend/
│   │   ├── silo/
│   │   ├── projector-service/
│   │   ├── kurrentdb/
│   │   ├── postgresql/
│   │   ├── redis/
│   │   └── secrets/
│   ├── docker-compose.yml           # Local dev (reference — not actively maintained)
│   └── scripts/
│       ├── start.sh
│       └── k3s-start.sh
├── PRD.md
├── CLAUDE.md
└── _bmad-output/
    └── planning-artifacts/
        └── architecture.md          # this document
```

### Architectural Boundaries

**API Boundary:**
- All external client requests hit **Astro BFF only** — never Silo directly
- Astro API routes (`src/pages/api/`) validate auth and proxy to Silo HTTP endpoints
- Silo API is internal-only, scoped by K8s network policy to cluster-internal traffic

**Component Boundaries:**
- **Astro** → calls Silo API controllers (HTTP, after auth middleware)
- **Astro** → reads PostgreSQL read model only (via EF Core / API routes)
- **Silo grains** → append events to KurrentDB; never write directly to PostgreSQL
- **ProjectorService** → subscribes to KurrentDB catch-up stream → writes PostgreSQL projections
- **Redis** → Orleans clustering + token store (not a business data store)

**Data Boundaries:**
- **KurrentDB** — append-only event store, source of truth
- **PostgreSQL** — derived read model, rebuilt from events via catch-up subscriptions
- **Redis** — Orleans cluster state + ephemeral tokens (email verification, deferred)

### Requirements to Structure Mapping

| Feature | Grain | Projection | Astro API Route | Status |
|---|---|---|---|---|
| Auth / GitHub SSO | UserGrain | UserProjection | `POST /api/auth/sign-in` | ✅ Done |
| Organizations | OrgGrain | OrgProjection, OrgMemberProjection | `GET/POST /api/orgs`, `GET/PUT/DELETE /api/orgs/{orgId}` | ⚠️ Needed |
| Products | ProductGrain | ProductProjection | `/api/orgs/{orgId}/products/*` | ⚠️ Needed |
| Tasks + Kanban | TaskGrain | TaskProjection | `/api/tasks/*`, `/api/orgs/{orgId}/kanban` | ⚠️ Needed |
| Tags | (in TaskGrain) | TaskProjection.tags (jsonb) | filter params in task list | ⚠️ Needed |
| Saved Views | (in TaskGrain) | TaskProjection views (jsonb) | `/api/views/*` (per-user) | ⚠️ Needed |
| Forecast | ForecastService | — | `/api/forecast` | ⏸️ Deferred |

### Integration Points

**Astro → Silo:**
```
Client request (with session cookie)
  → Astro middleware validates cookie
  → Astro API route (src/pages/api/orgs/index.ts)
  → fetch('http://velucid-silo:5000/api/orgs', { credentials: 'include' })
  → Silo controller validates org membership
  → returns JSON (snake_case)
```

**Silo → KurrentDB:**
- `KurrentDbStreamClient.AppendEventAsync()` — all grain mutations append to stream
- Grain state replay: reads from `$ce` stream on activation

**ProjectorService → KurrentDB → PostgreSQL:**
```
ProjectorService subscribes to $ce stream from last checkpointed sequence
  → on event: ProjectionCheckpoint.LastSequence
  → map event to entity (e.g., OrgProjection row UPDATE/INSERT)
  → upsert to PostgreSQL via EF Core
```

**Frontend → Kurrent → Local Read Model (post-Epic-4, post-Kurrent-sync-engine):**
```
KurrentDB event log
  → Kurrent TypeScript sync engine (running in browser)
  → apps/web consumes `@velucid/projection` (same functions as ProjectorService)
  → updates nanostores local store
  → React components render from local state
```
Writes bypass this path: React component → Astro BFF → Silo controller → grain → KurrentDB. The local store is a derived cache; the backend projector remains the source of truth for the Postgres read model.

**Silo API reads PostgreSQL:**
- Silo API controllers query read model directly via `ReadModelDbContext`
- Needed for: list orgs, list products, list tasks (rebuild read model from events)

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:** All decisions work together without conflicts.
- .NET 10 Orleans + KurrentDB HTTP atom + PostgreSQL EF Core — no version or protocol conflicts
- System.Text.Json `SnakeCaseLower` on Silo maps cleanly to Astro `apiFetch` expectations
- Session cookie via `jose` (AES-GCM) — validated by Astro middleware, Silo trusts relayed requests
- Redis dual-use (Orleans clustering + token store) cleanly separated; email verification tokens use same Redis but are logically isolated by key prefix
- Catch-up subscriptions only (no persistent subscriptions) — confirmed by user

**Pattern Consistency:** Naming conventions (PascalCase event names, `[GrainType("short-name")]`, snake_case JSON), event registration in `EventTypeMapping`, and API routing (plural nouns, `{name:guid}`) are all consistently applied across existing code and documented patterns.

**Structure Alignment:** Directory structure follows grains/Events/Controllers/Models conventions already established. No cross-boundary calls exist (Astro never accesses KurrentDB or PostgreSQL directly).

### Requirements Coverage Validation ✅

All MVP functional requirements have architectural support.

**Non-Functional Requirements:**
- Performance (<1s backlog load, <200ms drag-drop): PostgreSQL read model + optimistic UI
- Self-hostable: K3s + ArgoCD + Docker images confirmed in K8s manifests
- Tenant isolation: org membership checks enforced in API controllers
- Event-sourcing: KurrentDB + catch-up subscriptions confirmed
- No time-centric features: architectural constraint honored by design

### Implementation Readiness Validation ✅

**Decision Completeness:** All critical decisions documented with technology rationale.
**Structure Completeness:** Full directory tree defined with all key files specified.
**Pattern Completeness:** 7 conflict areas resolved with clear enforcement rules.

### Gap Analysis Results

**Critical Gaps:** None. Architecture is coherent and complete.

**Important Gaps (needed for MVP Kanban):**
1. OrgGrain + OrgController — Organizations module needs implementation
2. ProductGrain + ProductProjection + Astro API — Products module not started
3. TaskGrain + TaskProjection + Astro API — Tasks + Kanban not started
4. Astro API routes for org/product/task operations
5. Projector handlers for newörg/product/task projections

**Nice-to-Have Gaps:**
- Swagger/OpenAPI — add when API surface stabilizes
- Real-time WebSocket updates for collaborative kanban — future iteration

### Architecture Completeness Checklist

**Requirements Analysis**
- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped

**Architectural Decisions**
- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed

**Implementation Patterns**
- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented

**Project Structure**
- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY WITH MINOR GAPS
**Confidence Level:** high

**Key Strengths:**
- Well-established BFF pattern with Astro as sole auth entry point
- Clean event-sourcing foundation with KurrentDB + catch-up subscriptions
- Orleans grain model provides natural aggregate boundaries (org → product → task)
- Self-hosting constraint met by K3s + Docker + ArgoCD
- Implementation patterns and consistency rules comprehensive and enforceable

**Areas for Future Enhancement:**
- Forecast module (Monte Carlo engine, S-curve CDF computation) — not MVP priority
- Swagger/OpenAPI documentation — add when API surface stabilizes
- Real-time WebSocket updates for collaborative kanban — future iteration

### Implementation Handoff

**AI Agent Guidelines:**
- Follow all architectural decisions exactly as documented
- Use implementation patterns consistently across all components
- Respect project structure and boundaries
- Refer to this document for all architectural questions
- Before implementing new modules, register events in `EventTypeMapping` and add projector handler in `ProjectorService`

**First Implementation Priority:**
Starting point for new AI agents: implement OrgGrain → OrgController → OrgProjection → Astro API routes → frontend org management UI.