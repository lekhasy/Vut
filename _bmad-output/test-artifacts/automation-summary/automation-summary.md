---
stepsCompleted: ['step-01-preflight-and-context', 'step-02-identify-targets', 'step-03-generate-tests']
lastStep: 'step-03-generate-tests'
lastSaved: '2026-05-28'
inputDocuments:
  - "_bmad-output/implementation-artifacts/1-1-organizations.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "backend/tests/Velucid.Silo.Tests/Grains/UserGrainTests.cs"
  - "backend/src/Velucid.Silo/Grains/OrgGrain.cs"
  - "backend/src/Velucid.Silo/Controllers/OrgController.cs"
  - "backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs"
  - ".claude/skills/bmad-testarch-automate/resources/knowledge/test-levels-framework.md"
  - ".claude/skills/bmad-testarch-automate/resources/knowledge/test-priorities-matrix.md"
  - ".claude/skills/bmad-testarch-automate/resources/knowledge/test-quality.md"
  - ".claude/skills/bmad-testarch-automate/resources/knowledge/data-factories.md"
  - ".claude/skills/bmad-testarch-automate/resources/knowledge/selective-testing.md"
  - ".claude/skills/bmad-testarch-automate/resources/knowledge/ci-burn-in.md"
  - "_bmad/tea/config.yaml"
knowledgeFragmentsLoaded:
  core:
    - test-levels-framework.md
    - test-priorities-matrix.md
    - data-factories.md
    - selective-testing.md
    - ci-burn-in.md
    - test-quality.md
---

# Step 1: Preflight & Context Loading — Velucid / Org Story (1.1)

## Stack Detection

- **Detected stack:** `fullstack` (frontend: Astro + TypeScript, backend: C# .NET 10 Orleans)
- **Frontend:** Astro + Tailwind, no test framework detected (`package.json` has no test deps)
- **Backend:** .NET 10 / Orleans with xUnit + FluentAssertions + NSubstitute
- **Test config:** `backend/tests/Velucid.Silo.Tests/Velucid.Silo.Tests.csproj`
- **Config override:** `test_stack_type: auto` → confirmed `fullstack`

## Framework Status

- **Frontend E2E/UI:** `playwright.config.*` missing — **no UI test framework detected**
- **Backend unit:** xUnit framework confirmed with Orleans TestCluster (in-memory event stream)
- **HALT condition (TF missing):** Skippable — backend unit framework exists; frontend lacks E2E but UI automation is out-of-scope for story-level test generation

## Execution Mode

**BMad-Integrated** — Story 1.1 (`1-1-organizations.md`) is the active target with:
- 5 acceptance criteria (AC #1–5)
- 8 tasks, 50+ subtasks
- Review findings (14 patches applied, 5 deferred)
- Baseline commit: `219a05f`

## Context Loaded

### Story 1.1: Organizations — Acceptance Criteria

| AC | Description | Risk |
|----|-------------|------|
| #1 | User creates org — becomes owner automatically | P0 — data creation |
| #2 | User views org list in sidebar | P1 — read path |
| #3 | User navigates to org dashboard (landing page shows product list) | P1 — navigation |
| #4 | User invites member by email (stored, no email sent) | P0 — authZ + data integrity |
| #5 | Owner removes non-owner members | P0 — authZ |

### Key Architecture (from story file)

- **Grain:** `OrgGrain` / `IOrgGrain` — event-sourced, Orleans
- **Events:** `OrgCreatedEvent`, `OrgRenamedEvent`, `OrgDeletedEvent`, `MemberAddedEvent`, `MemberRemovedEvent`, `InvitationSentEvent`
- **Controller:** `OrgController` on Silo (7 endpoints)
- **Frontend BFF:** Astro API routes under `frontend/src/pages/api/orgs/`
- **Projector:** `OrgProjector.cs` → read model (`OrgProjection`, `OrgMemberProjection`, `OrgInvitationProjection`)

### Existing Test Patterns

**Backend unit tests (UserGrainTests.cs):**
- Orleans `TestCluster` with `InMemoryEventStreamClient`
- `EventTypeMapping.Register<T>()` for event type resolution
- FluentAssertions for assertions
- 14 tests covering: creation, idempotency, linking, profile updates, email verification
- Test naming: `Method_StateUnderTest_ExpectedBehavior`

**Projector:** `OrgProjector.cs` at `backend/src/Velucid.ProjectorService/Handlers/`

**Grain interface:** `IOrgGrain.cs` methods: `CreateOrg`, `RenameOrg`, `DeleteOrg`, `AddMember`, `RemoveMember`, `SendInvitation`, `GetOrgInfo`, `GetMembers`, `IsMember`

## TEA Config Flags

| Flag | Value |
|------|-------|
| `tea_use_playwright_utils` | `true` (ignored — no Playwright detected) |
| `tea_browser_automation` | `auto` |
| `test_stack_type` | `auto` → `fullstack` |
| `ci_platform` | `auto` |

## Knowledge Fragments Loaded

| Fragment | Lines | Purpose |
|----------|-------|---------|
| `test-levels-framework.md` | ~474 | Unit vs integration vs E2E selection |
| `test-priorities-matrix.md` | ~374 | P0–P3 criteria and coverage targets |
| `test-quality.md` | ~665 | DoD: deterministic, isolated, <300 lines |
| `data-factories.md` | ~501 | Factory patterns with overrides, API seeding |
| `selective-testing.md` | ~733 | Tag/grep execution, diff-based runs |
| `ci-burn-in.md` | ~718 | CI staging, burn-in loops, shard orchestration |

## Risk Assessment for Story 1.1

**P0 (Must Cover):**
- `CreateOrg` — AC#1: idempotency, owner assignment, duplicate creation guard
- `DeleteOrg` — authorization (owner-only), soft-delete state
- `RemoveMember` — owner-only authorization, cannot remove self
- `SendInvitation` — membership check, duplicate invite prevention

**P1 (Should Cover):**
- `RenameOrg` — membership check, no-op rename
- `AddMember` — role validation ("Owner"/"Member"), duplicate guard
- `GetOrgInfo` — non-existent org throws
- `GetMembers` — non-existent org throws
- `IsMember` — returns false for non-existent org

**P2/P3:**
- Invitation status transitions (deferred: projector replay status preservation)
- TOCTOU race conditions (deferred)
- Unique constraint on (OrgId, Email) for invitations (deferred)

## Next Action

Proceed to **Step 2: Identify Targets** — enumerate grain methods, controller endpoints, projector flows, and frontend API routes, then map to test scenarios with priority and risk scoring.

---

# Step 2: Identify Automation Targets — Velucid / Org Story (1.1)

## Target Landscape

### Layer 1 — Grain (Orleans Event-Sourced Aggregate)

| Method | Description | AuthZ | Idempotent |
|--------|-------------|-------|------------|
| `CreateOrg(name, ownerUserId)` | Creates org, assigns owner | None | Yes (returns existing) |
| `RenameOrg(name, requesterUserId)` | Renames org | Member check | Yes (no-op if same name) |
| `DeleteOrg(requesterUserId)` | Soft-deletes org | Owner-only | Yes (no-op if already deleted) |
| `AddMember(userId, role, requesterUserId)` | Adds member | Member check | Yes (no-op if already member) |
| `RemoveMember(userId, requesterUserId)` | Removes member | Owner-only | Yes (no-op if not member) |
| `SendInvitation(email, role, inviterUserId)` | Stores invitation | Member check | Yes (no-op if pending invite exists) |
| `GetOrgInfo()` | Returns org info | Throws if not exists | — |
| `GetMembers()` | Returns member list | Throws if not exists | — |
| `IsMember(userId)` | Checks membership | Returns false if not exists | — |

### Layer 2 — Controller (ASP.NET Core, Silo)

| Endpoint | Method | AuthZ Check | Response |
|----------|--------|-------------|----------|
| `GET /api/orgs?userId=` | List user's orgs | Via DB query | `200 List<OrgDto>` |
| `POST /api/orgs` | Create org | None (userId from query) | `201 OrgDto` |
| `GET /api/orgs/{orgId}?userId=` | Get org details | DB membership check | `200 OrgDto` / `404` |
| `PUT /api/orgs/{orgId}` | Rename org | Delegates to grain | `200` |
| `DELETE /api/orgs/{orgId}` | Soft-delete org | Delegates to grain | `204` |
| `POST /api/orgs/{orgId}/invitations` | Send invitation | Delegates to grain | `200` |
| `GET /api/orgs/{orgId}/members?userId=` | List members | DB membership check | `200 List<OrgMemberDto>` |
| `DELETE /api/orgs/{orgId}/members/{userId}?requesterUserId=` | Remove member | Delegates to grain | `204` |

### Layer 3 — Projector (Event → Read Model)

| Event | Read Model Update |
|-------|-------------------|
| `OrgCreatedEvent` | Insert `OrgProjection`, insert `OrgMemberProjection` (owner) |
| `OrgRenamedEvent` | Update `OrgProjection.Name` |
| `OrgDeletedEvent` | Update `OrgProjection.IsDeleted = true` |
| `MemberAddedEvent` | Insert `OrgMemberProjection` |
| `MemberRemovedEvent` | Delete `OrgMemberProjection` |
| `InvitationSentEvent` | Insert `OrgInvitationProjection` |

### Layer 4 — Frontend BFF (Astro API Routes)

`frontend/src/pages/api/orgs/` — mirrors Silo controller endpoints.

---

## Test Level Selection

Following `test-levels-framework.md` guidance:

| Test Level | Target | Rationale |
|------------|--------|-----------|
| **Unit** | `OrgGrain` command handling + event emission | Pure business logic, no external deps |
| **Integration** | `OrgController` HTTP endpoints | Tests service boundary + authZ wiring |
| **Integration** | `OrgProjector` event → projection | Tests persistence boundary |
| **E2E** | Not applicable | No Playwright/UI test framework; frontend UI out-of-scope for story-level generation |

**Decision: Unit + Integration only.** E2E deferred until Playwright framework is scaffolded.

---

## Coverage Plan

### Scope: Critical-Paths (P0 focus, P1 covered, P2 deferred)

**Rationale:** Story 1.1 has 14 review findings patched and 5 deferred. Focus test energy on the 4 P0 grain methods + controller authZ wiring.

---

## Priority Mapping

### UNIT TESTS — OrgGrain (`backend/tests/Velucid.Silo.Tests/Grains/OrgGrainTests.cs`)

| Test ID | Scenario | Priority | Risk |
|---------|----------|----------|------|
| `1.1-UNIT-001` | `CreateOrg` — first time — emits `OrgCreatedEvent` with correct fields | P0 | AuthZ=none, data creation |
| `1.1-UNIT-002` | `CreateOrg` — duplicate call — idempotent, no new events | P0 | Data integrity |
| `1.1-UNIT-003` | `CreateOrg` — owner automatically added as "Owner" in Members | P0 | Core invariant |
| `1.1-UNIT-004` | `DeleteOrg` — owner calling — emits `OrgDeletedEvent`, sets `IsDeleted=true` | P0 | AuthZ + soft-delete |
| `1.1-UNIT-005` | `DeleteOrg` — non-owner calling — throws `InvalidOperationException` | P0 | AuthZ enforcement |
| `1.1-UNIT-006` | `DeleteOrg` — already deleted — no-op (idempotent) | P1 | Idempotency |
| `1.1-UNIT-007` | `RemoveMember` — owner removing member — emits `MemberRemovedEvent` | P0 | AuthZ + data integrity |
| `1.1-UNIT-008` | `RemoveMember` — owner removing self — throws `InvalidOperationException` | P0 | Business rule |
| `1.1-UNIT-009` | `RemoveMember` — non-owner calling — throws `InvalidOperationException` | P0 | AuthZ enforcement |
| `1.1-UNIT-010` | `RemoveMember` — non-existent member — no-op (idempotent) | P1 | Idempotency |
| `1.1-UNIT-011` | `SendInvitation` — member inviting — emits `InvitationSentEvent` | P0 | AuthZ + data integrity |
| `1.1-UNIT-012` | `SendInvitation` — non-member inviting — throws `InvalidOperationException` | P0 | AuthZ enforcement |
| `1.1-UNIT-013` | `SendInvitation` — duplicate email (pending) — no-op idempotent | P1 | Duplicate prevention |
| `1.1-UNIT-014` | `SendInvitation` — invalid role string — throws `ArgumentException` | P1 | Input validation |
| `1.1-UNIT-015` | `RenameOrg` — member renaming — emits `OrgRenamedEvent` | P1 | Core flow |
| `1.1-UNIT-016` | `RenameOrg` — non-member renaming — throws `InvalidOperationException` | P1 | AuthZ enforcement |
| `1.1-UNIT-017` | `RenameOrg` — same name — no-op idempotent | P2 | Edge case |
| `1.1-UNIT-018` | `AddMember` — member adding new member — emits `MemberAddedEvent` | P1 | Core flow |
| `1.1-UNIT-019` | `AddMember` — invalid role — throws `ArgumentException` | P1 | Input validation |
| `1.1-UNIT-020` | `AddMember` — already member — no-op idempotent | P2 | Idempotency |
| `1.1-UNIT-021` | `GetOrgInfo` — non-existent org — throws `InvalidOperationException` | P1 | Error handling |
| `1.1-UNIT-022` | `GetMembers` — non-existent org — throws `InvalidOperationException` | P1 | Error handling |
| `1.1-UNIT-023` | `IsMember` — existing member — returns `true` | P1 | Core query |
| `1.1-UNIT-024` | `IsMember` — non-existent org — returns `false` | P1 | Null-state handling |
| `1.1-UNIT-025` | `IsMember` — existing org, non-member user — returns `false` | P1 | Core query |
| `1.1-UNIT-026` | `CreateOrg` — deleted org re-created — throws or returns existing | P2 | Edge case (deferred) |

**Unit test count: 26 tests**

### INTEGRATION TESTS — OrgController (`backend/tests/Velucid.Silo.Tests/Controllers/OrgControllerTests.cs`)

| Test ID | Scenario | Priority | Risk |
|---------|----------|----------|------|
| `1.1-INT-001` | `POST /api/orgs` — creates org, returns `201` with `OrgDto` | P0 | Core endpoint |
| `1.1-INT-002` | `GET /api/orgs?userId=` — returns user's orgs list | P1 | Read path |
| `1.1-INT-003` | `GET /api/orgs/{orgId}?userId=` — member gets org, returns `200` | P1 | Read path + authZ |
| `1.1-INT-004` | `GET /api/orgs/{orgId}?userId=` — non-member gets `404` | P0 | AuthZ enforcement |
| `1.1-INT-005` | `PUT /api/orgs/{orgId}` — renames org, returns `200` | P1 | Core endpoint |
| `1.1-INT-006` | `DELETE /api/orgs/{orgId}` — owner deletes, returns `204` | P0 | Core endpoint |
| `1.1-INT-007` | `POST /api/orgs/{orgId}/invitations` — member invites, returns `200` | P0 | AuthZ + data |
| `1.1-INT-008` | `GET /api/orgs/{orgId}/members?userId=` — member lists members, returns `200` | P1 | Read path |
| `1.1-INT-009` | `GET /api/orgs/{orgId}/members?userId=` — non-member gets `404` | P0 | AuthZ enforcement |
| `1.1-INT-010` | `DELETE /api/orgs/{orgId}/members/{userId}` — owner removes, returns `204` | P0 | Core endpoint |
| `1.1-INT-011` | `DELETE /api/orgs/{orgId}/members/{userId}` — non-owner gets error | P0 | AuthZ enforcement |
| `1.1-INT-012` | `GET /api/orgs` — user with no orgs returns empty list `[]` | P2 | Edge case |

**Integration test count: 12 tests**

### INTEGRATION TESTS — OrgProjector (`backend/tests/Velucid.Silo.Tests/Projector/OrgProjectorTests.cs`)

| Test ID | Scenario | Priority | Risk |
|---------|----------|----------|------|
| `1.1-PRJ-001` | `OrgCreatedEvent` → inserts `OrgProjection` + `OrgMemberProjection` | P0 | Projection integrity |
| `1.1-PRJ-002` | `OrgDeletedEvent` → sets `IsDeleted=true` on `OrgProjection` | P1 | Soft-delete |
| `1.1-PRJ-003` | `MemberAddedEvent` → inserts `OrgMemberProjection` | P1 | Membership projection |
| `1.1-PRJ-004` | `MemberRemovedEvent` → deletes `OrgMemberProjection` | P1 | Membership projection |
| `1.1-PRJ-005` | `InvitationSentEvent` → inserts `OrgInvitationProjection` | P1 | Invitation projection |
| `1.1-PRJ-006` | Replay: `InvitationSentEvent` for accepted invitation — preserves `Accepted` status | P2 | Deferred issue (review finding) |

**Projector test count: 6 tests**

---

## Summary

| Layer | Level | Tests | Coverage |
|-------|-------|-------|----------|
| OrgGrain | Unit | 26 | P0: 8, P1: 12, P2: 6 |
| OrgController | Integration | 12 | P0: 6, P1: 5, P2: 1 |
| OrgProjector | Integration | 6 | P0: 1, P1: 4, P2: 1 |
| **Total** | | **44** | **P0: 15, P1: 21, P2: 8** |

## Next Action

Proceed to **Step 3: Generate Tests** — produce the actual test code for OrgGrain unit tests following the existing UserGrainTests.cs pattern.