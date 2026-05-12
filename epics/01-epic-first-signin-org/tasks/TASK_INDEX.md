# Epic 1 Task Index: First Sign-In & Organization (Microsoft Orleans Architecture)

## Overview

This document indexes all tasks for Epic 1, showing dependencies, parallelism opportunities, and the critical path. The architecture uses **Microsoft Orleans virtual actors (grains)** as the core backend framework. Grains are identified by strongly-typed interfaces and GUID keys, auto-activate on first call, and are location-transparent across silo nodes.

**Key simplification:** Orleans uses the existing PostgreSQL database for cluster membership and grain directory — no external message broker is needed. The API is co-hosted inside the Orleans silo process.

**Deployment model:** The platform runs on a **single developer machine** using **K3s** (lightweight Kubernetes). All services start with 1 replica. Internet access is via **Cloudflare Tunnel** (`velucid.app`) — no static IP required. Future scaling to additional machines uses **Tailscale** VPN mesh to join K3s agent nodes, with zero application code changes.

**Total Tasks:** 16
**Backend Tasks:** 8 (Tasks 01-08, 16)
**Frontend Tasks:** 7 (Tasks 09-15)

## Dependency Graph

```
PARALLEL WAVE 1 (Start Immediately - No Dependencies):
  [01] K3s Infrastructure (Backend)              [P0 - 3 days]
  [02] Auth0 Tenant Setup (Backend)          [P0 - 0.5 days]
  [09] Astro.js Project Setup (Frontend)     [P0 - 2 days]

PARALLEL WAVE 2 (Depends on Wave 1):
  [03] PostgreSQL Schema (Backend)           [P0 - 1 day]   <-- depends on [01]
  [04] .NET Orleans Silo Foundation (Backend) [P0 - 3 days]  <-- depends on [01]
  [10] Auth Flow & BFF Session (Frontend)    [P0 - 2.5 days]<-- depends on [02], [09]

PARALLEL WAVE 3 (Depends on Wave 2):
  [05] User Grain (Backend)                  [P1 - 2 days]  <-- depends on [04]
  [06] Organization Grain (Backend)          [P1 - 3 days]  <-- depends on [04]
  [08] Co-hosted API Controllers (Backend)   [P1 - 2 days]  <-- depends on [03], [04]
  [11] Org Selector & Sidebar (Frontend)     [P1 - 2 days]  <-- depends on [09], [10]
  [13] BFF API Route Proxies (Frontend)      [P1 - 2 days]  <-- depends on [09], [10]

PARALLEL WAVE 4 (Depends on Wave 3):
  [07] Projector Service (Backend)           [P1 - 2.5 days]<-- depends on [03], [04], [05], [06]
  [12] Org Management Pages (Frontend)       [P1 - 3 days]  <-- depends on [11], [13]
  [14] Invitation Flow (Frontend)            [P2 - 1.5 days]<-- depends on [11], [13]
  [15] Landing Page & Dashboard (Frontend)   [P1 - 1.5 days]<-- depends on [11], [12]

FINAL WAVE:
  [16] CI/CD & Dockerfiles (Backend)         [P2 - 1.5 days]<-- depends on [04]-[08], [09]
```

## Task List

| # | Task | Developer | Priority | Effort | Dependencies | Can Start |
|---|------|-----------|----------|--------|--------------|-----------|
| 01 | K3s Infrastructure | Backend | P0 | 3d | None | Immediately |
| 02 | Auth0 Tenant Configuration | Backend | P0 | 0.5d | None | Immediately |
| 03 | PostgreSQL Schema & Migrations | Backend | P0 | 1d | 01 | After K8s |
| 04 | .NET Orleans Silo Foundation | Backend | P0 | 3d | 01 | After K8s |
| 05 | User Grain Implementation | Backend | P1 | 2d | 04 | After Silo Foundation |
| 06 | Organization Grain Implementation | Backend | P1 | 3d | 04 | After Silo Foundation |
| 07 | Projector Service Implementation | Backend | P1 | 2.5d | 03, 04, 05, 06 | After Grains + Schema |
| 08 | Co-hosted API Controllers | Backend | P1 | 2d | 03, 04 | After Schema + Silo |
| 09 | Astro.js Project Setup & UI Shell | Frontend | P0 | 2d | None | Immediately |
| 10 | Auth Flow & BFF Session Management | Frontend | P0 | 2.5d | 02, 09 | After Auth0 + Setup |
| 11 | Org Selector & Sidebar Navigation | Frontend | P1 | 2d | 09, 10 | After Auth Flow |
| 12 | Organization Management Pages | Frontend | P1 | 3d | 11, 13 | After Org Selector + BFF |
| 13 | BFF API Route Proxies | Frontend | P1 | 2d | 09, 10 | After Auth Flow |
| 14 | Invitation Acceptance Flow | Frontend | P2 | 1.5d | 11, 13 | After Org Selector + BFF |
| 15 | Landing Page & Dashboard Empty State | Frontend | P1 | 1.5d | 11, 12 | After Org Selector + Org Mgmt |
| 16 | CI/CD Pipeline & Dockerfiles | Backend | P2 | 1.5d | 04-08, 09 | After services are built |

## Critical Path

The longest dependency chain determines the minimum time to complete Epic 1:

```
[01] K3s (3d) -> [04] Silo Foundation (3d) -> [06] Org Grain (3d) -> [07] Projector (2.5d)
                                                                          |
                                                                    +-----+-----+
                                                                    |           |
                                                               [12] Org Mgmt  [15] Landing
                                                               (3d)           (1.5d)

Critical Path Total: 3 + 3 + 3 + 2.5 + 3 = 14.5 days (sequential)
With parallelism: approximately 10-11 days with 2 developers.
```

## Developer Assignment Strategy

### Backend Developer
1. Start: Task 01 (K8s) + Task 02 (Auth0) in parallel.
2. Then: Task 03 (Schema) + Task 04 (Orleans Silo Foundation — UseOrleans(), EventSourcedGrain base, KurrentDB EventStoreClient) in parallel.
3. Then: Task 05 (User Grain) + Task 06 (Org Grain) in parallel.
4. Then: Task 08 (Co-hosted API Controllers) + Task 07 (Projector) in parallel.
5. Finally: Task 16 (CI/CD).

### Frontend Developer
1. Start: Task 09 (Astro Setup) immediately.
2. Then: Task 10 (Auth Flow).
3. Then: Task 11 (Org Selector) + Task 13 (BFF Routes) in parallel.
4. Then: Task 12 (Org Mgmt) + Task 14 (Invitations) in parallel.
5. Then: Task 15 (Landing Page).

### Parallelism Summary
- **Days 1-3:** Backend on K3s + Cloudflare Tunnel, Frontend on Astro Setup, Backend on Auth0.
- **Days 3-6:** Backend on Orleans Silo Foundation + Schema, Frontend on Auth Flow.
- **Days 6-9:** Backend on User+Org Grains, Frontend on Org Selector + BFF Routes.
- **Days 9-12:** Backend on Projector + Co-hosted API, Frontend on Org Mgmt + Invitations.
- **Days 12-14:** Backend on CI/CD, Frontend on Landing Page.

## Estimated Total Effort

- **Backend:** 3 + 0.5 + 1 + 3 + 2 + 3 + 2.5 + 2 + 1.5 = **18.5 days**
- **Frontend:** 2 + 2.5 + 2 + 3 + 2 + 1.5 + 1.5 = **14.5 days**
- **Calendar Time (2 developers):** ~**12-14 days** with optimal parallelism.
- **Calendar Time (1 developer doing both):** ~**33 days** (not recommended).

## Key Architecture Decisions

| Pattern | Implementation | Where |
|---------|---------------|-------|
| Container Orchestration | K3s (lightweight Kubernetes) on single dev machine | Task 01 |
| Internet Ingress | Cloudflare Tunnel — outbound-only, no static IP, `velucid.app` domain | Task 01 |
| Ingress Controller | Traefik (bundled with K3s) | Task 01 |
| Multi-Machine Scaling | Tailscale VPN mesh + K3s agent join (future) | Task 01 (notes) |
| Virtual Actor Framework | Microsoft Orleans with PostgreSQL (ADO.NET) clustering | Task 04 |
| Cluster Membership | `UseAdoNetClustering()` from day one — never `UseLocalhostClustering()` | Task 04 |
| Event Sourcing | Custom `EventSourcedGrain<TState>` with direct KurrentDB `EventStore.Client` | Task 04 |
| Base Grain | `EventSourcedGrain<TState> : Grain` with event replay and `DelayDeactivation` | Task 04 |
| Grain Interfaces | `IUserGrain : IGrainWithGuidKey` (Task 05), `IOrganizationGrain : IGrainWithGuidKey` (Task 06) | Tasks 05, 06 |
| Grain Deactivation | `GrainCollectionOptions.CollectionAge` (30 min), re-hydrate from KurrentDB on re-activation | Task 04 |
| API Hosting | Co-hosted ASP.NET Core controllers in Orleans silo, using `IGrainFactory` for writes | Tasks 04, 08 |
| Projection | KurrentDB persistent subscriptions -> Projector (.NET Worker) -> PostgreSQL | Task 07 |
| Email Service | Resend — verification codes and invitation emails sent from co-hosted API | Tasks 04, 08 |

## Event Streams Covered

| Stream | Events | Created In |
|--------|--------|-----------|
| `user-{userId}` | `UserCreated`, `IdentityLinked`, `UserProfileUpdated`, `EmailVerificationRequested`, `EmailVerified` | Task 05 |
| `organization-{orgId}` | `OrganizationCreated`, `OrganizationRenamed`, `MemberInvited`, `MemberJoined`, `MemberRemoved`, `MemberRoleChanged`, `OrganizationDeleted` | Task 06 |

## Projection Tables Covered

| Table | Created In | Populated By |
|-------|-----------|-------------|
| `user_projection` | Task 03 | Task 07 |
| `user_identity` | Task 03 | Task 07 |
| `org_projection` | Task 03 | Task 07 |
| `org_member_projection` | Task 03 | Task 07 |
| `org_invitation_projection` | Task 03 | Task 07 |
| `user_org_projection` | Task 03 | Task 07 |
