# Epic 1 Task Index: First Sign-In & Organization

## Overview

This document indexes all tasks for Epic 1, showing dependencies, parallelism opportunities, and the critical path.

**Total Tasks:** 16
**Backend Tasks:** 8 (Tasks 01-08, 16)
**Frontend Tasks:** 7 (Tasks 09-15)

## Dependency Graph

```
PARALLEL WAVE 1 (Start Immediately - No Dependencies):
  [01] K8s Infrastructure (Backend)         [P0 - 3 days]
  [02] Auth0 Tenant Setup (Backend)          [P0 - 0.5 days]
  [09] Astro.js Project Setup (Frontend)     [P0 - 2 days]

PARALLEL WAVE 2 (Depends on Wave 1):
  [03] PostgreSQL Schema (Backend)           [P0 - 1 day]   <-- depends on [01]
  [04] .NET Actor Service Foundation (Backend)[P0 - 3 days]  <-- depends on [01]
  [10] Auth Flow & BFF Session (Frontend)    [P0 - 2.5 days]<-- depends on [02], [09]

PARALLEL WAVE 3 (Depends on Wave 2):
  [05] User Actor (Backend)                  [P1 - 1.5 days]<-- depends on [04]
  [06] Organization Actor (Backend)          [P1 - 3 days]  <-- depends on [04]
  [08] Read Model API (Backend)              [P1 - 2 days]  <-- depends on [03]
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
| 01 | Kubernetes Infrastructure | Backend | P0 | 3d | None | Immediately |
| 02 | Auth0 Tenant Configuration | Backend | P0 | 0.5d | None | Immediately |
| 03 | PostgreSQL Schema & Migrations | Backend | P0 | 1d | 01 | After K8s |
| 04 | .NET Actor Service Foundation | Backend | P0 | 3d | 01 | After K8s |
| 05 | User Actor Implementation | Backend | P1 | 1.5d | 04 | After Actor Foundation |
| 06 | Organization Actor Implementation | Backend | P1 | 3d | 04 | After Actor Foundation |
| 07 | Projector Service Implementation | Backend | P1 | 2.5d | 03, 04, 05, 06 | After Actors + Schema |
| 08 | Read Model API Service | Backend | P1 | 2d | 03 | After Schema |
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
[01] K8s (3d) -> [04] Actor Foundation (3d) -> [06] Org Actor (3d) -> [07] Projector (2.5d)
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
2. Then: Task 03 (Schema) + Task 04 (Actor Foundation) in parallel.
3. Then: Task 05 (User Actor) + Task 06 (Org Actor) in parallel.
4. Then: Task 08 (Read Model API) + Task 07 (Projector) in parallel.
5. Finally: Task 16 (CI/CD).

### Frontend Developer
1. Start: Task 09 (Astro Setup) immediately.
2. Then: Task 10 (Auth Flow).
3. Then: Task 11 (Org Selector) + Task 13 (BFF Routes) in parallel.
4. Then: Task 12 (Org Mgmt) + Task 14 (Invitations) in parallel.
5. Then: Task 15 (Landing Page).

### Parallelism Summary
- **Days 1-3:** Backend on K8s, Frontend on Astro Setup, Backend on Auth0.
- **Days 3-6:** Backend on Actor Foundation + Schema, Frontend on Auth Flow.
- **Days 6-9:** Backend on User+Org Actors, Frontend on Org Selector + BFF Routes.
- **Days 9-12:** Backend on Projector + Read Model API, Frontend on Org Mgmt + Invitations.
- **Days 12-14:** Backend on CI/CD, Frontend on Landing Page.

## Estimated Total Effort

- **Backend:** 3 + 0.5 + 1 + 3 + 1.5 + 3 + 2.5 + 2 + 1.5 = **18 days**
- **Frontend:** 2 + 2.5 + 2 + 3 + 2 + 1.5 + 1.5 = **14.5 days**
- **Calendar Time (2 developers):** ~**12-14 days** with optimal parallelism.
- **Calendar Time (1 developer doing both):** ~**32.5 days** (not recommended).

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
| `projection_checkpoint` | Task 03 | Task 07 |
