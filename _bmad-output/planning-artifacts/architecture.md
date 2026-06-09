---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-06-07'
lastRevised: '2026-06-07'
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Velucid-2026-06-07/prd.md
  - _bmad-output/planning-artifacts/prds/prd-Velucid-2026-06-07/addendum.md
  - docs/velucid_forecasting_spec.md
  - docs/new-machine-setup.md
workflowType: 'architecture'
project_name: 'Velucid'
user_name: 'Syle'
date: '2026-06-07'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Last revised 2026-06-07 to match the new aggregate-based PRD (Workspace / User / Account / Initiative / Team / TeamMember / WorkItem / Label / WorkspaceInvitation). The previous tag-based model and the TS/Bun projector port are superseded._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
- **Identity & Access (own auth, no third-party IdP).** Email-OTP login. User enters email → receives a one-time code by email → enters code → session created. The Astro web app is the sole auth entry point; the silo never sees login flow.
- **Workspaces.** Top-level tenant (replaces the old "org" wording). Globally unique name; the creator is the Owner. Per-Workspace tenancy for all reads.
- **Accounts & Roles.** A User has one Account per Workspace they belong to. Account carries display name, avatar, role. Two flat roles: **Owner** and **Member**. Owner-only actions: workspace configuration, team configuration, initiative creation.
- **Workspace Invitations.** First-class aggregate with its own event stream (`workspace-invitation-{invitationId}`). Lifecycle: `Pending` → `Accepted` / `Revoked` / `Expired`. Owner invites by email + optional team(s). On accept, Account is created and TeamMember records are created for the chosen teams.
- **Teams & TeamMembers.** Team belongs to a Workspace, has a name and an ordered WorkItem list (priority order, drag-to-reorder). TeamMember is the join of Account + Team (many-to-many).
- **Initiatives.** A goal a Workspace wants to achieve. WorkItems belong to exactly one Initiative. Owner creates; Members view.
- **WorkItems.** Title, optional description, status, priority (sort position in Team's list), assigned Team (one), optional assigned TeamMember, zero-or-more Labels, exactly one Initiative. Status belongs to one of six default Status Categories (`Backlog`, `Unstarted`, `Started`, `Completed`, `Canceled`, `Duplicate`) with one default status each. Any-to-any status change by any Member. No archive; to stop work, transition to `Canceled`.
- **Labels.** Workspace-scoped. Any Member can create. A WorkItem has zero or more.
- **Forecast.** Two independent flavors, both Monte Carlo on observed WorkItem flow, both parameterized by **Initiative** (not tags — tags are gone from the forecast model):
  1. **Per-Initiative** — overall completion curve for the Initiative's WorkItems.
  2. **Per-Initiative × Per-Team** — answers "which team is the bottleneck / who came last."
- **Local-first behavior.** Snappy on a good network via a local store. **No offline mode** — we do not want users to make changes offline and merge later. Teammate changes appear in the local store live (real-time transport TBD; the real-time client lib is in development, so transport is not pinned here).
- **Notifications.** Email only in MVP. The transactional email provider chosen for OTP codes is reused for notification emails (WorkspaceInvitation email, WorkItem-assigned-to-TeamMember email).
- **Onboarding.** Two mutually-exclusive paths at first sign-in: (1) no pending invitation → create first Workspace with a globally unique name; (2) pending invitation → enter the one-time code, jump straight into the Workspace.
- **UX.** No kanban. No columns, no drag-between-columns. Each Team has one WorkItem list. Drag-to-reorder sets priority; the top item is highest priority.

**Non-Functional Requirements:**
- Zero time-centric features in the product (hard constraint, unchanged)
- Self-hostable: no cloud provider dependency, runs on team hardware
- All reads scoped to Workspaces the user is an Account of (per-Workspace tenancy, enforced everywhere)
- Optimistic UI on writes; eventual consistency on reads, masked by local store
- HTTPS only, no secrets in events, actor+timestamp on all events
- WCAG 2.1 AA color contrast, keyboard-navigable drag-to-reorder, ARIA labels

**Scale & Complexity:**
- Primary domain: full-stack web (Astro 5 BFF + React 19 SPA + .NET 10 Orleans API + event-sourced backend)
- Complexity level: medium-high
- Estimated architectural components: 10–14 major components (grew with the aggregate model and the surface split)

### Technical Constraints & Dependencies

- **Event store**: KurrentDB — catch-up subscriptions + secondary indexes for read model projections (no persistent subscriptions)
- **Read model**: PostgreSQL (EF Core 10) with projections rebuilt via catch-up subscriptions in the C# projector
- **Auth**: own email-OTP. Transactional email provider (TBD — Resend / SES / Postmark). Short-lived OTP code store (Postgres or Redis with TTL). Server-side session via Astro BFF.
- **Authorization**: OpenFGA. Tuples written in the projector (not in grains) — avoids inconsistency windows.
- **Frontend split**: Astro 5 = sole auth entry point + landing page only (BFF, serves no app content). React 19 SPA = product surface, local-first, separate Nx app.
- **Real-time**: transport TBD. The client lib is in development. Behavior: teammate changes appear in the local store live.
- **Projector**: C# (.NET) is canonical. The TS/Bun port is deferred indefinitely.
- **Deployment target**: K3s on WSL2 (prod); Docker Compose (local dev)
- **CI/CD**: GitHub Actions + ArgoCD (gitops workflow)
- **Secrets**: Infisical for production K8s secrets management

### Cross-Cutting Concerns Identified

1. **Event sourcing everywhere.** All mutations emit events. Read models are projections via catch-up subscriptions. The forecast engine consumes the event stream directly. Events encode the *meaning* of actions — no `TagAdded` / `TagRemoved` events for structural relations. Use the explicit aggregate event (e.g., `WorkItemAssignedToTeam`).
2. **Per-Workspace tenancy.** Every read scoped to Workspaces the user is an Account of. API layer enforces isolation — no direct DB access by clients.
3. **Per-invitation event stream.** WorkspaceInvitations are a first-class aggregate with **one stream per invitation** named `workspace-invitation-{uniqueInvitationId}`. Invitations do not appear in the Workspace's event stream — the workspace history stays focused on the workspace's actual evolution. The projector can ignore the whole invitation stream family for workspace read models and surface it only in the invitations view.
4. **Optimistic UI + local-first.** Writes go through the BFF to the silo and reflect immediately in the UI. Reads are served from a local store on the client. Teammate updates push into the local store as they happen, so the UI re-renders live.
5. **Authorization in the projector.** OpenFGA tuple writes happen in the projector (not in grains) — avoids inconsistency windows between event projection and tuple projection.
6. **Auth failures are loud.** `UnauthorizedAccessException` for auth failures. Domain validation throws `InvalidOperationException`. Grain state is rebuilt from events; never mutate grain state directly.
7. **Forecast computation.** Server-side Monte Carlo (10k sims), CDF cached and returned to client. Threshold slider updates without re-simulation. Parameterized by Initiative (per-Initiative and per-Initiative×Per-Team). The math is in `docs/velucid_forecasting_spec.md`. Built LAST in MVP.
8. **BFF auth pattern.** Astro is the sole authentication entry point. All client requests flow through Astro which validates the session cookie and proxies to Silo API. The React SPA also talks to the Astro BFF.

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- BFF architecture: Astro as sole auth entry point and API gateway; React SPA is a separate Nx app
- Orleans grain model for business logic (User grain, Account grain, Workspace grain, Initiative grain, Team grain, TeamMember grain, WorkItem grain, Label grain, WorkspaceInvitation grain)
- KurrentDB as event store with catch-up subscription projections; **per-invitation stream** for WorkspaceInvitations
- PostgreSQL read model via EF Core, projected by the C# projector (TS/Bun port deferred)
- OpenFGA for authorization, tuples written in the projector
- Own email-OTP auth (no Auth0, no third-party IdP); transactional email provider TBD; OTP code store TBD (Postgres or Redis with TTL); server-side session

**Important Decisions (Shape Architecture):**
- Redis dual-use (Orleans clustering + OTP code store, if Redis is chosen)
- nanostores for Astro-side local state
- React 19 SPA holds a local projection; real-time transport TBD
- Tailwind CSS for styling
- K3s + ArgoCD deployment (gitops)
- Nx 22.7.5 monorepo: `apps/web` (Astro BFF + landing), `apps/app` (React 19 SPA, planned), `apps/silo` (.NET 10 Orleans), `apps/projector` (Bun/TS, deferred). Libs: `events`, `projection`, `read-model`, `kurrent-client`.

**Deferred Decisions (Post-MVP):**
- TS/Bun projector (C# is canonical; the port is deferred indefinitely)
- Real-time transport (the client lib is in development; transport is TBD)
- Custom Status definitions per Team (only the six default Status Categories + their default Statuses in MVP)
- Workspace / Team / Initiative / Account deletion; ownership transfer
- Comments, activity feed, attachments, due dates, subtasks on WorkItem
- In-app notification center; SMS / push notifications
- Swagger/OpenAPI documentation (add when API stabilizes)
- Per-WorkItem forecast contribution; forecast export / sharing / notifications

### Data Architecture

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Event store | KurrentDB | 26.1.0 | HTTP atom interface, catch-up subscriptions, secondary indexes. Self-hostable. |
| Read model DB | PostgreSQL | 16-alpine | EF Core 10 + Npgsql. Used for all projections (users, accounts, workspaces, initiatives, teams, team_members, work_items, labels, workspace_invitations). |
| Projector | C# (.NET) | 10 | Canonical. The TS/Bun port in `apps/projector` is deferred indefinitely — not in scope for current planning. |
| Grain persistence | Orleans memory (dev) / KurrentDB | — | Ephemeral grains; all state persisted as events in KurrentDB. |
| Projection delivery | Catch-up subscriptions | — | Projector subscribes to `$ce` stream from last checkpoint. No persistent subscriptions. |
| WorkspaceInvitation streams | One per invitation | — | Stream name: `workspace-invitation-{uniqueInvitationId}`. Keeps invitations out of the Workspace event stream. |
| Other aggregate streams | One per aggregate root | — | Convention: `{aggregate-name}-{aggregateId}` (e.g., `workspace-{workspaceId}`, `work_item-{workItemId}`). |

### Authentication & Security

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Identity provider | **None — own email-OTP** | — | No Auth0. User enters email, receives a one-time code by email, enters code, session created. Astro BFF owns login. |
| OTP code store | TBD — Postgres or Redis with TTL | — | Short-lived (TTL TBD), single-use. Provider choice deferred. |
| Transactional email | TBD — Resend / SES / Postmark | — | Used for both OTP codes and notification emails. |
| Session management | Server-side session via Astro BFF | — | Session model: server-side session vs signed cookie — see Open Questions. |
| API authorization | Workspace membership scoped (OpenFGA) | — | Every Silo API query enforces Workspace membership via OpenFGA. Tenant isolation at API layer. |
| Authorization tuples | Written in the projector | — | Avoids inconsistency windows between event projection and tuple projection. |
| Orleans clustering | Redis | Microsoft.Orleans.Clustering.Redis 10.1.0 | Redis-based clustering for multi-pod Silo deployment. |

**Open sub-decisions** (deferred per Decision Log 2026-06-07 "Auth: drop Auth0 entirely; own email-OTP login"):
- Email provider (Resend / SES / Postmark)
- Session model (server-side session vs signed cookie)
- OTP code TTL
- Rate limiting strategy

### API & Communication Patterns

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Call chain (web product) | React SPA → Astro (BFF) → Silo (gRPC/HTTP) → KurrentDB | — | BFF pattern: Astro is sole auth entry point. Silo API is internal. |
| Call chain (landing) | Public visitor → Astro → static landing | — | Astro serves the landing page directly (no auth). |
| Auth flow | Email-OTP → session cookie → Astro middleware | — | All client requests carry session cookie. Astro validates and proxies to Silo. |
| Inter-service | Orleans grains + Silo API controllers | — | Business logic in grains. Silo API controllers call grains. |
| Orleans intra-cluster | gRPC | — | Grains communicate via Orleans runtime over gRPC within the Silo cluster. |
| Real-time (web product) | Transport TBD → local store → React | — | Teammate updates push into the local store. UI re-renders live. Transport is TBD; the client lib is in development. |

### Frontend Architecture

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Astro framework | Astro 5 | 5.8.0 | SSR. BFF + landing page only. Sole auth entry point. |
| React SPA framework | React 19 + Vite | 19 | Local-first product surface. Separate Nx app (`apps/app`, planned). |
| Interactive components (Astro) | React 19 islands | 19 | Only the React 19 SPA; the Astro shell has minimal interactivity. |
| Client state (React SPA) | Local store with real-time updates | — | Local store caches the read model. Teammate updates push in as they happen. |
| Styling | Tailwind CSS | 3.4.17 | Utility-first. Design system tokens per project. |
| Auth (client) | Astro middleware | — | Cookie validated server-side in Astro `middleware.ts`. Session data attached to `context.locals`. The React SPA authenticates by exchanging the cookie through the BFF. |
| Local-first read model | Shared projection code runs in `apps/app` (browser) AND in the C# projector (Postgres) | — | The browser holds a derived cache from the event log; the backend projector remains the source of truth for the Postgres read model. The same projection logic runs in both — no drift. Real-time transport TBD. |
| Drag-to-reorder | Accessible, keyboard-navigable | — | ARIA labels; replaces the old kanban affordance. |

### Infrastructure & Deployment

| Decision | Choice | Version | Rationale |
|---|---|---|---|
| Production host | K3s on WSL2 | — | Self-hosted. No cloud provider dependency. Per `new-machine-setup.md`. |
| Container orchestration | K3s + ArgoCD | — | Gitops: push to main → CI builds images → ArgoCD syncs manifests → K3s deploys. |
| Secrets (prod) | Infisical | — | Infisical Operator syncs secrets to K8s. No secrets in git. |
| Secrets (local) | `.env` | — | `infrastructure/scripts/start.sh` reads `.env` for local Docker Compose. |
| Container registry | GitHub Container Registry (ghcr.io) | — | Images: `ghcr.io/lekhasy/vut/{silo,web,projector}:<sha>` |
| Service mesh | Traefik (K3s bundled) | — | Ingress routing, TLS termination via Cloudflare Tunnel. |

### Implementation Sequence

1. **Authentication & Identity** (§4.1 PRD) — email-OTP, session, the BFF owns login.
2. **Realtime sync + local-first** (§4.10 PRD) — **immediately after auth, on the client side.** No structural features ship until the React SPA can read from a local store and propagate changes (in both directions) in real time. This is the load-bearing piece.
3. **Workspaces, Accounts, Teams, TeamMembers, Initiatives, WorkspaceInvitations** (§4.2–§4.6, §4.3 PRD) — the structural model. WorkspaceInvitations get their own per-invitation stream.
4. **WorkItems** (§4.8 PRD) — with statuses (six default Status Categories), priority (drag-to-reorder), team assignment. Built before Labels per explicit user call.
5. **Labels** (§4.7 PRD) — workspace-scoped tags. Deliberately deprioritized — do not build before WorkItems.
6. **Forecast** (§4.9 PRD) — **last in MVP.** Per-Initiative and per-Initiative×Per-Team. The math is in `docs/velucid_forecasting_spec.md` and is unchanged in this PRD pass.
7. **Email notifications** (§4.11 PRD) — rides on the same transactional email provider as OTP.

This sequence is the build order within MVP and the source of truth for sprint planning.

## Implementation Patterns & Consistency Rules

### Critical Conflict Points Identified: 8

### Naming Patterns

**Event Type Names (KurrentDB strings):**
- Independent of CLR type name — always registered explicitly via `EventTypeMapping.Register<TEvent>("StringName")`
- Format: PascalCase noun phrase (e.g., `"UserCreated"`, `"WorkspaceCreated"`, `"WorkItemAssignedToTeam"`, `"WorkspaceInvitationAccepted"`)
- Must encode the *meaning* of the action — no `TagAdded` / `TagRemoved` for structural relations. Use the explicit aggregate event (e.g., `WorkItemAssignedToTeam`, not a generic tag flip).
- Must be registered at startup before `EventTypeMapping.Freeze()` is called
- Event C# record class name is independent — the string name is the stable identity in the event stream

**Stream Names (KurrentDB):**
- Convention: `{aggregate-name}-{aggregateId}` (lowercase, hyphen-separated)
- Examples: `workspace-{workspaceId}`, `team-{teamId}`, `work_item-{workItemId}`, `account-{accountId}`
- **WorkspaceInvitations break the pattern by design** — stream name is `workspace-invitation-{uniqueInvitationId}` (one stream per invitation, not per workspace)
- User is the only aggregate whose stream may not follow the pattern (e.g., `user-{userId}`) — confirm in code

**Grain Interfaces & Types:**
- Interface: `I{Name}Grain` (e.g., `IUserGrain`, `IWorkspaceGrain`, `IWorkItemGrain`, `IWorkspaceInvitationGrain`)
- Grain class: `{Name}Grain` with `[GrainType("short-name")]` attribute on the class
- Short-name format: lowercase singular noun (e.g., `"user"`, `"workspace"`, `"work_item"`, `"workspace_invitation"`)
- Example:
  ```csharp
  [GrainType("workspace")]
  public class WorkspaceGrain : EventSourcedGrain<WorkspaceState>, IWorkspaceGrain { ... }
  ```
- Orleans uses `[GrainType]` internally for grain routing — clients still call `GetGrain<I{Name}Grain>(id)`

**API Routes:**
- Plural resource names: `/api/workspaces`, `/api/teams`, `/api/work_items`, `/api/workspace_invitations`
- Route parameters: `{name:guid}` for IDs, e.g., `/api/workspaces/{workspaceId:guid}/work_items`
- No verb in route — actions differentiated by HTTP method (GET, POST, PUT, DELETE)

**Database Tables (EF Core):**
- Entity classes: PascalCase, entity noun (e.g., `UserProjection`, `WorkspaceProjection`, `WorkItemProjection`, `WorkspaceInvitationProjection`)
- Table name: not explicitly configured — EF defaults to `{EntityName}s` (e.g., `WorkspaceProjections`)
- Columns: PascalCase C# property names (e.g., `WorkspaceId`, `DisplayName`, `Priority`)
- Soft-delete: use a status transition to `Canceled` — not a `DeletedAt` column. WorkItems have no archive action in MVP.

**Event Payload Fields (C# records):**
- PascalCase throughout (e.g., `WorkspaceId`, `DisplayName`, `WorkItemId`)
- Every event: `ActorId` (Guid) and `Timestamp` (DateTimeOffset) required
- Nullable fields marked `?` where semantically appropriate
- Example:
  ```csharp
  public record WorkItemAssignedToTeamEvent(
      Guid WorkItemId,
      Guid WorkspaceId,
      Guid TeamId,
      Guid ActorId,
      DateTimeOffset Timestamp
  ) : IEvent;
  ```

**Frontend JSON:**
- snake_case throughout (e.g., `workspace_id`, `display_name`, `work_item_id`)
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
  UserGrain.cs              ← [GrainType("user")]
  IUserGrain.cs
  UserState.cs
  WorkspaceGrain.cs         ← [GrainType("workspace")]
  IWorkspaceGrain.cs
  WorkspaceState.cs
  AccountGrain.cs           ← [GrainType("account")]
  IAccountGrain.cs
  AccountState.cs
  InitiativeGrain.cs        ← [GrainType("initiative")]
  IInitiativeGrain.cs
  InitiativeState.cs
  TeamGrain.cs              ← [GrainType("team")]
  ITeamGrain.cs
  TeamState.cs
  TeamMemberGrain.cs        ← [GrainType("team_member")]
  ITeamMemberGrain.cs
  TeamMemberState.cs
  WorkItemGrain.cs          ← [GrainType("work_item")]
  IWorkItemGrain.cs
  WorkItemState.cs
  LabelGrain.cs             ← [GrainType("label")]
  ILabelGrain.cs
  LabelState.cs
  WorkspaceInvitationGrain.cs ← [GrainType("workspace_invitation")]
  IWorkspaceInvitationGrain.cs
  WorkspaceInvitationState.cs
Events/
  IEvent.cs
  IEventStreamClient.cs
  KurrentDbStreamClient.cs
  EventTypeMapping.cs       ← all EventTypeMapping.Register() calls (do not modify)
  WorkspaceCreatedEvent.cs
  ...
Controllers/
  UserController.cs         ← API endpoints + inline DTO records
  WorkspaceController.cs
  ...
Models/
  CreateUserResult.cs       ← cross-grain result types only
```
- DTO records stay co-located with their controller file
- New grains go in `Grains/`, events in `Events/`, controllers in `Controllers/` — no sub-folders

**ReadModel (EF Core):**
```
Velucid.ReadModel/Entities/
  UserProjection.cs
  AccountProjection.cs
  WorkspaceProjection.cs
  WorkspaceMemberProjection.cs
  InitiativeProjection.cs
  TeamProjection.cs
  TeamMemberProjection.cs
  WorkItemProjection.cs
  LabelProjection.cs
  WorkspaceInvitationProjection.cs
  ProjectionCheckpoint.cs
ReadModelDbContext.cs
```
- No sub-folders — all projection entities at the same level
- One entity class per aggregate root

**Frontend — Astro BFF + landing (`apps/web`):**
```
src/pages/api/             ← Astro API endpoints (BFF)
  auth/
    request-code.ts        ← POST /api/auth/request-code   (start OTP)
    verify-code.ts         ← POST /api/auth/verify-code    (consume OTP, set session)
    sign-out.ts            ← POST /api/auth/sign-out
  workspaces/
    index.ts               ← GET/POST /api/workspaces
    [workspaceId]/
      index.ts             ← GET/PUT /api/workspaces/{workspaceId}
      members/
        index.ts           ← /api/workspaces/{workspaceId}/members
      teams/
        index.ts           ← /api/workspaces/{workspaceId}/teams
        [teamId]/
          index.ts
          work_items/
            index.ts       ← /api/workspaces/{workspaceId}/teams/{teamId}/work_items
      initiatives/
        index.ts
      invitations/
        index.ts           ← /api/workspaces/{workspaceId}/invitations
  work_items/
    [workItemId]/
      index.ts             ← GET/PUT /api/work_items/{workItemId}
  labels/
    index.ts
src/pages/                  ← Astro pages (landing only)
  index.astro              ← landing
  onboarding/              ← first sign-in flow (create-workspace or enter-invite-code)
src/components/
  ui/                      ← shared UI primitives
src/lib/
  api.ts                   ← apiFetch wrapper
  auth/                    ← session utilities
  email/                   ← transactional email send (OTP, notifications)
src/middleware.ts          ← Astro auth middleware
```

**Frontend — React SPA (`apps/app`, planned):**
```
src/
  app/                     ← app shell, routing
  features/
    workspaces/
    work_items/
    initiatives/
    teams/
    invitations/
    forecast/
  components/              ← shared UI primitives
  lib/
    api.ts                 ← BFF fetch wrapper
    store/                 ← local store (real-time cache)
    realtime/              ← real-time transport (TBD)
  pages/                   ← routed views
```

### Format Patterns

**API Response — Success:**
- Raw resource object or array — no envelope (`{ data: ... }`)
- HTTP 200 OK, 201 Created, 204 No Content
- POST to create returns full resource with server-assigned ID

**API Response — Error:**
- Always: `{ "message": "human-readable string" }`
- HTTP status codes: 401 (unauthenticated), 403 (unauthorized), 404 (not found), 409 (conflict), 422 (validation error), 500 (server error)
- Auth failures throw `UnauthorizedAccessException`; domain validation throws `InvalidOperationException`

**Date/Time:**
- UTC ISO-8601 strings in JSON: `"2026-06-07T15:30:00Z"`
- No Unix timestamps — always wall-clock time with timezone

**GUIDs:**
- Standard UUID v4 format in JSON: `"f47ac10b-58cc-4372-a567-0e02b2c3d479"`

### Communication Patterns

**Event Naming:**
- Past-tense noun phrases encoding the *meaning* of the action: `WorkspaceCreated`, `AccountCreatedInWorkspace`, `WorkItemAssignedToTeam`, `WorkItemStatusChanged`, `WorkspaceInvitationAccepted`
- One event per state transition — no combined events
- Event name encodes what happened, never intent or future state
- **No** `TagAdded` / `TagRemoved` / `Tagged` / `Untagged` events for structural relations (Team, Initiative, Label applied to a WorkItem). Use the explicit aggregate event.

**State Update Flow (write):**
```
User action in React SPA
  → BFF API route (apps/web/src/pages/api/...)
  → Silo controller → grain
  → KurrentDB appends Event
  → C# projector catch-up subscription (async, background)
  → PostgreSQL projection updated
  → OpenFGA tuples written in the projector
  → Real-time transport (TBD) pushes to other clients' local stores
  → UI re-renders
```

**State Update Flow (read):**
```
React SPA reads from local store (instant)
  → on hydration / focus: BFF refreshes from Postgres read model
  → real-time updates push in as events happen
```

**Optimistic Updates:**
- UI reflects change immediately on user action (write goes through BFF in the background)
- If BFF call fails → revert UI state + show toast error
- Local store is the source of truth for rendering; backend projector is the source of truth for Postgres

### Process Patterns

**Error Handling (Astro / React SPA):**
```typescript
try {
  const result = await apiFetch('/api/workspaces', { method: 'POST', body: JSON.stringify(data) });
  // do something with result
} catch (err) {
  showToast({ type: 'error', message: err.message });
}
```

**Loading States:**
- Per-component loading state
- Skeleton states for initial page loads
- Local store is always rendered from, so initial loads are essentially instant after first hydration

**Validation:**
- Client-side: basic required-field checks before submit
- Server-side (Silo): always validate — never trust client data, even after client validation
- On validation failure: HTTP 422 with `{ "message": "Field X is required or invalid" }`
- Domain validation throws `InvalidOperationException`; auth throws `UnauthorizedAccessException`

### Enforcement Guidelines

**All AI Agents MUST:**
- Place `[GrainType("short-name")]` on every grain class (never on the interface)
- Register every event in `EventTypeMapping.Register<TEvent>("PascalCaseName")` before `Freeze()`
- Use the explicit aggregate event for structural relations — never `TagAdded` / `TagRemoved` for Team/Initiative/Label relations
- Configure `System.Text.Json` with `JsonNamingPolicy.SnakeCaseLower` globally — no per-property attributes
- Place all BFF endpoints in `apps/web/src/pages/api/` as Astro API routes
- Make all Silo calls from Astro server-side code only — never call KurrentDB or PostgreSQL directly from Astro
- Return `{ "message": "..." }` for all error responses; include `ActorId` and `Timestamp` on every event
- No persistent subscriptions — use catch-up subscriptions only in the C# projector
- Throw `UnauthorizedAccessException` for auth failures; `InvalidOperationException` for domain validation
- Write OpenFGA tuples in the projector, not in grains
- Use the per-invitation stream pattern for WorkspaceInvitations: `workspace-invitation-{uniqueInvitationId}`
- Use `Canceled` status for WorkItem removal; do not introduce a `DeletedAt` soft-delete column

## Project Structure & Boundaries

### Complete Project Directory Structure

```
Vut/
├── apps/
│   ├── web/                 # Astro 5 SSR + React 19 islands (Bun runtime in prod)
│   │                        # Sole auth entry point + landing page only
│   ├── app/                 # React 19 SPA (local-first product surface) — PLANNED
│   │                        # Will be created when the local-first foundation is in place
│   ├── projector/           # TS/Bun projector (deferred indefinitely; not built in current scope)
│   └── silo/                # Nx wrapper over backend/src/Velucid.Silo (dotnet 10)
├── libs/
│   ├── events/              # @velucid/events         (populated)
│   ├── projection/          # @velucid/projection     (populated — runs in C# projector AND in browser)
│   ├── read-model/          # @velucid/read-model     (populated)
│   └── kurrent-client/      # @velucid/kurrent-client (populated)
├── backend/src/
│   ├── Velucid.Silo/             # Orleans API host (Silo + ASP.NET Core)
│   │   ├── Configuration/
│   │   │   └── KurrentDbOptions.cs
│   │   ├── Controllers/
│   │   │   ├── UserController.cs
│   │   │   ├── WorkspaceController.cs
│   │   │   ├── AccountController.cs
│   │   │   ├── TeamController.cs
│   │   │   ├── TeamMemberController.cs
│   │   │   ├── InitiativeController.cs
│   │   │   ├── WorkItemController.cs
│   │   │   ├── LabelController.cs
│   │   │   └── WorkspaceInvitationController.cs
│   │   ├── Events/
│   │   │   ├── IEvent.cs
│   │   │   ├── IEventStreamClient.cs
│   │   │   ├── KurrentDbStreamClient.cs
│   │   │   ├── EventTypeMapping.cs   # all Register() calls - do not rename entries
│   │   │   ├── UserCreatedEvent.cs
│   │   │   ├── WorkspaceCreatedEvent.cs
│   │   │   ├── AccountCreatedInWorkspaceEvent.cs
│   │   │   ├── TeamCreatedEvent.cs
│   │   │   ├── TeamMemberAddedEvent.cs
│   │   │   ├── InitiativeCreatedEvent.cs
│   │   │   ├── WorkItemCreatedEvent.cs
│   │   │   ├── WorkItemAssignedToTeamEvent.cs
│   │   │   ├── WorkItemAssignedToTeamMemberEvent.cs
│   │   │   ├── WorkItemStatusChangedEvent.cs
│   │   │   ├── WorkItemReorderedEvent.cs
│   │   │   ├── LabelCreatedEvent.cs
│   │   │   ├── LabelAppliedToWorkItemEvent.cs
│   │   │   ├── WorkspaceInvitationCreatedEvent.cs
│   │   │   ├── WorkspaceInvitationAcceptedEvent.cs
│   │   │   ├── WorkspaceInvitationRevokedEvent.cs
│   │   │   ├── WorkspaceInvitationExpiredEvent.cs
│   │   │   └── ...                    # future events for the remaining aggregates
│   │   ├── Grains/
│   │   │   ├── EventSourcedGrain.cs   # base class for all event-sourced grains
│   │   │   ├── UserGrain.cs
│   │   │   ├── WorkspaceGrain.cs
│   │   │   ├── AccountGrain.cs
│   │   │   ├── TeamGrain.cs
│   │   │   ├── TeamMemberGrain.cs
│   │   │   ├── InitiativeGrain.cs
│   │   │   ├── WorkItemGrain.cs
│   │   │   ├── LabelGrain.cs
│   │   │   └── WorkspaceInvitationGrain.cs   # stream: workspace-invitation-{invitationId}
│   │   ├── Models/                     # cross-grain result DTOs only
│   │   │   └── ...
│   │   ├── Services/
│   │   │   ├── IOtpService.cs          # email-OTP code store + send
│   │   │   ├── OtpService.cs
│   │   │   ├── ISessionService.cs      # server-side session via Astro BFF
│   │   │   ├── SessionService.cs
│   │   │   ├── IEmailService.cs        # transactional email (OTP, notifications)
│   │   │   └── EmailService.cs
│   │   └── Program.cs
│   ├── Velucid.ReadModel/          # EF Core PostgreSQL projections
│   │   ├── Entities/
│   │   │   ├── UserProjection.cs
│   │   │   ├── AccountProjection.cs
│   │   │   ├── WorkspaceProjection.cs
│   │   │   ├── WorkspaceMemberProjection.cs
│   │   │   ├── InitiativeProjection.cs
│   │   │   ├── TeamProjection.cs
│   │   │   ├── TeamMemberProjection.cs
│   │   │   ├── WorkItemProjection.cs
│   │   │   ├── LabelProjection.cs
│   │   │   ├── WorkspaceInvitationProjection.cs
│   │   │   └── ProjectionCheckpoint.cs
│   │   ├── ReadModelDbContext.cs
│   │   └── Velucid.ReadModel.csproj
│   ├── Velucid.ProjectorService/   # C# background catch-up worker (CANONICAL)
│   │   ├── Handlers/
│   │   │   ├── UserProjector.cs
│   │   │   ├── WorkspaceProjector.cs
│   │   │   ├── AccountProjector.cs
│   │   │   ├── TeamProjector.cs
│   │   │   ├── TeamMemberProjector.cs
│   │   │   ├── InitiativeProjector.cs
│   │   │   ├── WorkItemProjector.cs
│   │   │   ├── LabelProjector.cs
│   │   │   ├── WorkspaceInvitationProjector.cs
│   │   │   └── OpenFgaProjector.cs   # writes OpenFGA tuples from events
│   │   ├── Configuration/
│   │   ├── Program.cs
│   │   └── Velucid.ProjectorService.csproj
│   ├── Velucid.ReadModel.Migrations/
│   │   ├── Migrations/
│   │   └── Program.cs
│   └── tests/
├── docs/
│   ├── project-context.md
│   ├── new-machine-setup.md
│   └── velucid_forecasting_spec.md
├── _bmad-output/
│   ├── planning-artifacts/
│   │   ├── architecture.md       # this document
│   │   ├── authorization-reference.md
│   │   └── prds/
│   │       └── prd-Velucid-2026-06-07/
│   │           ├── prd.md
│   │           ├── addendum.md
│   │           └── .decision-log.md
│   └── implementation-artifacts/
│       ├── sprint-status.yaml
│       └── ...
├── infrastructure/
│   ├── k8s/                        # K8s manifests (per service)
│   │   ├── argocd/
│   │   ├── web/
│   │   ├── silo/
│   │   ├── projector-service/      # not deployed in current scope; placeholder
│   │   ├── kurrentdb/
│   │   ├── postgresql/
│   │   ├── redis/
│   │   └── secrets/
│   ├── docker-compose.yml           # Local dev (reference — not actively maintained)
│   └── scripts/
│       ├── start.sh
│       └── k3s-start.sh
├── CLAUDE.md
└── ...
```

### Architectural Boundaries

**API Boundary:**
- All external client requests hit **Astro BFF only** — never Silo directly
- Astro API routes (`apps/web/src/pages/api/`) validate auth and proxy to Silo HTTP endpoints
- Silo API is internal-only, scoped by K8s network policy to cluster-internal traffic
- The React SPA (`apps/app`, planned) talks to Astro BFF — never directly to the Silo

**Component Boundaries:**
- **Astro (BFF)** → calls Silo API controllers (HTTP, after auth middleware) and sends transactional email
- **Astro (BFF)** → reads PostgreSQL read model only (via Silo / API routes)
- **React SPA** → reads from its local store; calls Astro BFF for writes; receives real-time updates (transport TBD)
- **Silo grains** → append events to KurrentDB; never write directly to PostgreSQL
- **C# ProjectorService** → subscribes to KurrentDB catch-up stream → writes PostgreSQL projections AND OpenFGA tuples
- **OpenFGA** → queried by Silo API controllers for authorization checks
- **Transactional email provider** → called by Astro BFF for OTP codes and notification emails

**Data Boundaries:**
- **KurrentDB** — append-only event store, source of truth. Stream naming: `{aggregate-name}-{aggregateId}`, with the exception of `workspace-invitation-{uniqueInvitationId}` for invitations.
- **PostgreSQL** — derived read model, rebuilt from events via catch-up subscriptions
- **Redis** — Orleans cluster state + ephemeral data (OTP code store, if Redis is chosen)
- **OpenFGA** — derived authorization state, projected from events by the C# projector

### Requirements to Structure Mapping

| Feature | Grain | Projection | Astro API Route | Status |
|---|---|---|---|---|
| Email-OTP auth | UserGrain | UserProjection | `POST /api/auth/request-code`, `POST /api/auth/verify-code` | ⚠️ Needed (PRD §4.1) |
| Session | (Astro BFF) | (cookie) | (middleware) | ⚠️ Needed |
| Local-first + real-time | (browser store) | (browser cache) | (BFF refresh) | ⚠️ Needed (PRD §4.10) |
| Workspaces | WorkspaceGrain | WorkspaceProjection, WorkspaceMemberProjection | `/api/workspaces/*` | ⚠️ Needed (PRD §4.2) |
| Workspace Invitations | WorkspaceInvitationGrain (per-invitation stream) | WorkspaceInvitationProjection | `/api/workspaces/{workspaceId}/invitations`, `/api/auth/redeem-invite` | ⚠️ Needed (PRD §4.3) |
| Accounts & Roles | AccountGrain | AccountProjection, WorkspaceMemberProjection | `/api/workspaces/{workspaceId}/accounts/*` | ⚠️ Needed (PRD §4.4) |
| Teams & TeamMembers | TeamGrain, TeamMemberGrain | TeamProjection, TeamMemberProjection | `/api/workspaces/{workspaceId}/teams/*` | ⚠️ Needed (PRD §4.5) |
| Initiatives | InitiativeGrain | InitiativeProjection | `/api/workspaces/{workspaceId}/initiatives/*` | ⚠️ Needed (PRD §4.6) |
| Labels | LabelGrain | LabelProjection | `/api/workspaces/{workspaceId}/labels/*` | ⚠️ Needed (PRD §4.7) |
| WorkItems | WorkItemGrain | WorkItemProjection | `/api/workspaces/{workspaceId}/teams/{teamId}/work_items/*` | ⚠️ Needed (PRD §4.8) |
| Forecast | (ForecastService) | (none) | `/api/workspaces/{workspaceId}/initiatives/{initiativeId}/forecast` | ⏸️ Deferred (PRD §4.9, last in MVP) |
| Notifications (email) | (EmailService) | (none) | (called by Silo/Astro on relevant events) | ⚠️ Needed (PRD §4.11) |

### Integration Points

**React SPA → Astro BFF:**
```
React component in apps/app
  → apiFetch('/api/workspaces/{id}/teams/{teamId}/work_items', { method: 'POST', body: ... })
  → Astro API route validates session cookie (middleware)
  → Astro forwards to Silo (HTTP, internal)
  → Silo controller → grain → KurrentDB
  → returns 201 with full resource
  → React optimistically updates local store
  → real-time transport (TBD) eventually pushes the same event to other clients
```

**Astro (BFF) → Silo:**
```
Client request (with session cookie)
  → Astro middleware validates cookie (server-side session)
  → Astro API route (src/pages/api/workspaces/index.ts)
  → fetch('http://velucid-silo:5000/api/workspaces', { credentials: 'include', headers: { 'X-Session-Id': ... } })
  → Silo controller validates session via OpenFGA, enforces Workspace membership
  → returns JSON (snake_case)
```

**Silo → KurrentDB:**
- `KurrentDbStreamClient.AppendEventAsync()` — all grain mutations append to stream
- Grain state replay: reads from the aggregate's stream on activation
- WorkspaceInvitation grains append to `workspace-invitation-{invitationId}` (per-invitation stream)

**C# ProjectorService → KurrentDB → PostgreSQL:**
```
C# ProjectorService subscribes to $ce stream from last checkpointed sequence
  → on event: ProjectionCheckpoint.LastSequence
  → map event to entity (e.g., WorkspaceProjection row UPDATE/INSERT)
  → upsert to PostgreSQL via EF Core
  → on the same event: write OpenFGA tuples (OpenFgaProjector)
  → checkpoint advances
```

**Browser local store (React SPA):**
```
KurrentDB event log
  → real-time transport (TBD) → browser consumer
  → browser runs shared @velucid/projection (same functions as C# projector)
  → updates local store
  → React components render from local state
```
Writes bypass this path: React component → Astro BFF → Silo controller → grain → KurrentDB. The local store is a derived cache; the C# projector remains the source of truth for the Postgres read model and for OpenFGA tuples.

**Silo API reads PostgreSQL:**
- Silo API controllers query read model directly via `ReadModelDbContext`
- Needed for: list workspaces, list teams, list work_items, etc.

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:** All decisions work together without conflicts.
- .NET 10 Orleans + KurrentDB HTTP atom + PostgreSQL EF Core — no version or protocol conflicts
- System.Text.Json `SnakeCaseLower` on Silo maps cleanly to Astro `apiFetch` and to the React SPA expectations
- Astro 5 (BFF + landing) ↔ React 19 SPA (local-first product) — clean separation of concerns; both go through Astro for auth
- Per-Workspace tenancy enforced via OpenFGA; tuples written in the C# projector — no inconsistency windows
- C# projector is canonical; TS/Bun port deferred — no parallel implementations to keep in sync
- Per-invitation stream pattern is consistent with the rest of the event-sourcing model; the projector can opt to ignore that stream family for workspace read models
- Six default Status Categories with one default Status each — simple model, easy to extend post-MVP

**Pattern Consistency:** Naming conventions (PascalCase event names, `[GrainType("short-name")]`, snake_case JSON), event registration in `EventTypeMapping`, stream naming (`{aggregate-name}-{aggregateId}` with per-invitation exception), and API routing (plural nouns, `{name:guid}`) are all consistently applied.

**Structure Alignment:** Directory structure follows grains/Events/Controllers/Models conventions. No cross-boundary calls exist (Astro never accesses KurrentDB or PostgreSQL directly; the React SPA never accesses them either — both go through the BFF).

### Requirements Coverage Validation ✅

All MVP functional requirements have architectural support.

**Non-Functional Requirements:**
- Per-Workspace tenancy: OpenFGA tuples written in the projector
- Self-hostable: K3s + ArgoCD + Docker images confirmed in K8s manifests
- Eventual consistency acceptable: local store + optimistic writes
- HTTPS only, no secrets in events: enforced as architectural guardrail
- WCAG 2.1 AA, keyboard-navigable drag-to-reorder: UI implementation concerns, not architecture

### Implementation Readiness Validation ✅

**Decision Completeness:** All critical decisions documented with technology rationale. Open sub-decisions (email provider, session model, OTP code store, OTP TTL, rate limiting) are flagged in the Auth section.

**Structure Completeness:** Full directory tree defined with all key files specified for the new aggregate model.

**Pattern Completeness:** 8 conflict areas resolved with clear enforcement rules.

### Gap Analysis Results

**Critical Gaps:** None. Architecture is coherent and complete for the new model.

**Important Gaps (build order in MVP):**
1. **Email-OTP auth + session** — needed first per the build order. Astro BFF owns login; the silo never sees login flow.
2. **Local-first + real-time** — the load-bearing piece, immediately after auth. The React SPA (`apps/app`) does not yet exist as an Nx app; creating it is part of this step.
3. **Structural grains** (Workspace, Account, Team, TeamMember, Initiative, WorkspaceInvitation) — once auth and local-first are in place
4. **WorkItems** with statuses, priority, team assignment
5. **Labels** — deprioritized, after WorkItems
6. **Forecast** — last in MVP
7. **Email notifications** — rides on the same transactional email provider as OTP

**Nice-to-Have Gaps:**
- Swagger/OpenAPI — add when API surface stabilizes
- Custom Status definitions per Team — post-MVP
- Comments, activity feed, attachments, due dates, subtasks on WorkItem — post-MVP
- In-app notification center — post-MVP

### Architecture Completeness Checklist

**Requirements Analysis**
- [x] Project context thoroughly analyzed for the new aggregate model
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped (including per-invitation stream and local-first)

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

**Overall Status:** READY
**Confidence Level:** high

**Key Strengths:**
- Aggregate-based event model with explicit structural events (no tag flips for Team/Initiative/Label relations)
- Per-Workspace tenancy enforced via OpenFGA, with tuples projected (no inconsistency windows)
- Per-invitation stream pattern keeps Workspace history clean
- Astro BFF owns auth and serves no app content; React SPA is a separate Nx app and is the local-first product surface
- C# projector is canonical — no parallel implementations to keep in sync
- Build order is explicit: auth → local-first + real-time → structural → WorkItems → Labels → Forecast

**Areas for Future Enhancement:**
- Real-time transport (TBD — the client lib is in development)
- Custom Status definitions per Team (post-MVP)
- In-app notification center, comments, activity feed (post-MVP)
- Swagger/OpenAPI documentation (add when API surface stabilizes)
- TS/Bun projector port (deferred indefinitely)

### Implementation Handoff

**AI Agent Guidelines:**
- Follow all architectural decisions exactly as documented
- Use implementation patterns consistently across all components
- Respect project structure and boundaries
- Refer to this document for all architectural questions
- Before implementing new aggregates, register events in `EventTypeMapping` and add a projector handler in the C# ProjectorService
- Use the explicit aggregate event for structural relations — never `TagAdded` / `TagRemoved`

**First Implementation Priority:**
Starting point for new AI agents: implement email-OTP auth (§4.1 PRD) end-to-end. The BFF owns login. Concretely:
1. `IOtpService` / `OtpService` (code store with TTL, code generation, code consumption)
2. `IEmailService` / `EmailService` (transactional email; provider TBD — Resend / SES / Postmark)
3. `ISessionService` / `SessionService` (server-side session via Astro BFF; session model TBD)
4. `apps/web/src/pages/api/auth/request-code.ts` and `verify-code.ts`
5. `apps/web/src/middleware.ts` (session validation)
6. `apps/web/src/pages/onboarding/*` (first-workspace create OR enter-invite-code path)

Once auth is in, the next step is the local-first + real-time foundation (PRD §4.10) — creating the `apps/app` React 19 SPA, wiring the BFF for reads/writes, and standing up the real-time transport (TBD). No structural features ship until this foundation is in place.
