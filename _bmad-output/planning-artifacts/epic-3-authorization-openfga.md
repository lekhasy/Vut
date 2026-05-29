# Epic 3: Authorization — OpenFGA Integration

## Overview

Replace all inline, manual authorization checks scattered across grains and controllers with a centralized, policy-based authorization system using OpenFGA (Open Fine-Grained Authorization).

**Target:** Authorization logic is declarative, auditable, and consistent across all operations. Team transparency is built into the model — anyone can read the policy and understand who can do what.

**Why OpenFGA:**
- Policy-as-code: authorization logic lives in a model, not in grain methods
- Relationship-based: maps directly to your existing Owner/Member model
- Centralized: one place to audit, update, and reason about
- Infrastructure: single binary, runs as sidecar, no heavy infra needed
- .NET SDK available, Orleans-friendly

**Why now:**
- Current authorization is inline and inconsistent — some grain methods have checks, others don't
- `UserGrain` operations have zero authorization (anyone can pass any `userId`)
- No centralized permission store — impossible to audit who can do what
- As transparency is a core product value, the authorization model should be equally transparent

---

## Authorization Model

### Roles

| Role | Description |
|------|-------------|
| **Owner** | All permissions. Handles rare/dangerous operations. Can delegate. |
| **Member** | Day-to-day work. Can create and work on tasks, view everything in the org. Cannot create products, invite, change roles, or delete. |

### Permission Matrix

| Operation | Owner | Member |
|-----------|-------|--------|
| View org | ✓ | ✓ |
| View members | ✓ | ✓ |
| Create task | ✓ | ✓ |
| Work on task (edit, move, complete) | ✓ | ✓ |
| Rename org | ✓ | ✓ |
| Create product | ✓ | ✗ |
| Delete product | ✓ | ✗ |
| Invite member | ✓ | ✗ |
| Change member role | ✓ | ✗ |
| Remove member | ✓ | ✗ |
| Delete org | ✓ | ✗ |
| Manage org settings | ✓ | ✗ |

**Rationale:** Owner = umbrella permission. Member = all transparent/shared operations. Owner-only = rare or destructive actions.

### OpenFGA Model Definition

```
type organization
  relations
    define owner @user
    define member @user

type user

# Transparent operations — anyone in org can do
define create_task as member or owner
define view_org as member or owner
define view_members as member or owner

# Dangerous / rare ops — owner only
define create_product as owner
define delete_product as owner
define invite_member as owner
define change_member_role as owner
define delete_org as owner
define manage_org_settings as owner
```

### Initial Tuples

```
organization:{orgId}#owner@user:{userId}
organization:{orgId}#member@user:{userId}
```

When a user creates an org → write tuple: `organization:{orgId}#owner@user:{creatorUserId}`
When an owner adds a member → write tuple: `organization:{orgId}#member@user:{newMemberUserId}`

---

## Story 3.1: OpenFGA Infrastructure

**What's needed:**
- Add OpenFGA SDK to backend: `dotnet add package OpenFgaClient` (or `FgaClient` depending on current package)
- Define authorization model in code (or via `.json` model file loaded at startup)
- Create `IOpenFgaAuthorizationService` interface:
  - `Check(userId, permission, resource)` → `Task<bool>`
  - `WriteTuples(tuples)` → `Task` (for membership changes)
  - `DeleteTuples(tuples)` → `Task` (for member removal)
- Implement `OpenFgaAuthorizationService` wrapping the OpenFGA SDK
- Support local mode (embedded, no server needed) for dev + Orleans grain integration
- Configuration: `OPENFGA_API_URL`, `OPENFGA_STORE_ID`, `OPENFGA_MODEL_ID`

**Acceptance criteria:**
- OpenFGA client initializes and can perform Check calls
- Authorization model is loaded at startup
- Membership changes (add/remove owner, add/remove member) write tuples to OpenFGA
- Check calls return correct allow/deny for Owner vs Member operations

---

## Story 3.2: Migrate OrgGrain Authorization

**What's needed:**
- Remove inline auth checks from `OrgGrain.RenameOrg`, `OrgGrain.DeleteOrg`, `OrgGrain.AddMember`, `OrgGrain.RemoveMember`, `OrgGrain.SendInvitation`
- Replace each with `await _authService.Check(userId, "view_org", orgId)` or permission-specific calls
- Seed existing org memberships to OpenFGA tuples on grain initialization (migration path)
- Emit tuple writes as part of event processing (e.g., `MemberAddedEvent` → write `member` tuple)

**Acceptance criteria:**
- All `OrgGrain` authorization goes through OpenFGA
- Owner-only operations correctly deny Member calls
- Membership changes correctly update OpenFGA tuple store
- Existing orgs have their membership migrated to OpenFGA on first access

---

## Story 3.3: Migrate UserGrain Authorization

**What's needed:**
- Fix the security hole: `UpdateProfile`, `RequestEmailVerification`, `VerifyEmail` currently accept any `userId` from caller
- Add authorization: caller must be the same user they're trying to operate on
- Simpler: just validate `callerUserId == targetUserId` as a baseline — self-service only
- Future: consider OpenFGA's `user:*` relation pattern if needed

**Acceptance criteria:**
- User can only update their own profile, request their own email verification, verify their own email
- Calling these with a different userId is rejected with 403

---

## Story 3.4: Migrate Controller Authorization

**What's needed:**
- `OrgController` currently passes `userId` from query params without verifying caller identity
- Replace query-param `userId` with `context.locals.userId` (already set by auth middleware)
- Remove inline auth in controllers — delegate entirely to grain-level OpenFGA checks
- For `UserController`: pass `context.locals.userId` to grain calls

**Acceptance criteria:**
- All controller endpoints use server-side `userId` from `context.locals`, not caller-supplied
- No endpoint accepts `userId` from request body or query param

---

## Story 3.5: Remove Inline Auth from OrgState

**What's needed:**
- Remove the `Dictionary<Guid, string> Members` role map from `OrgState` — membership now lives in OpenFGA
- Remove role validation from grain methods — OpenFGA is the source of truth
- Update projector if it reads from `Members` dictionary
- Keep `OrgState` for aggregate state (name, settings, etc.) — not auth

**Acceptance criteria:**
- `OrgState` no longer contains `Members` dictionary
- OpenFGA is the sole source of truth for authorization decisions
- All existing tests updated to reflect the new authorization pattern

---

## Story 3.6: Authorization Test Suite

**What's needed:**
- Unit tests: each permission check in OpenFGA model
- Integration tests: grain authorization with real OpenFGA (or test container)
- Test coverage for: Owner allowed, Member denied for owner-only ops, non-member denied
- Test the security holes from Story 3.3 specifically

**Acceptance criteria:**
- All owner-only operations reject Member callers
- All transparent operations accept both Owner and Member
- Non-members are rejected from all org operations
- User self-service operations reject cross-user calls

---

## Retrospective 3: Authorization Epic Review

**Purpose:** Assess the OpenFGA integration — developer experience,运行时 performance, and model clarity.

**Topics to cover:**
- Did the OpenFGA model feel natural to work with?
- Any latency concerns from OpenFGA Check calls in grain hot paths?
- Is the authorization model clear enough for a new developer to understand?
- What's next: product-level permissions, task-level permissions, or move back to Epic 1?

---

## Migration Notes

### Phase 1: Infrastructure (Story 3.1)
- No behavior change yet
- OpenFGA runs as a **centralized server** — single OpenFGA instance. All Silo pods call it over HTTP. No per-pod sidecar, no sync machinery. Simplest operational model.
- Infrastructure: k3s manifests in `infrastructure/k8s/openfga/`, docker compose for local dev in `infrastructure/openfga/`

### Phase 2: Seed + Grain Integration (Stories 3.2–3.4)
- Existing orgs: on first grain access, write membership tuples to OpenFGA via SDK
- New orgs: emit tuple writes from grain event handlers via SDK
- Inline checks removed after OpenFGA confirms same behavior

### Phase 3: Cleanup (Story 3.5)
- Remove `Members` dictionary after migration confirmed
- Update tests

### Critical Path
1. OpenFGA client + model setup (3.1)
2. OrgGrain migration (3.2) — highest impact
3. Controller auth fix (3.4) — closes the security gap
4. UserGrain fix (3.3) — closes the security hole
5. Remove OrgState Members (3.5)
6. Tests (3.6)

---

## Open Questions

1. **OpenFGA instance:** Run as sidecar binary alongside the Silo? Embed in-process for dev?
2. **Model versioning:** When the model changes, how do we migrate existing tuples? OpenFGA supports model stages but may need migration scripts.
3. **Caching:** OpenFGA Check calls are fast (<10ms local) but grains are hot paths. Consider a short TTL cache for membership checks.
4. **Testing in CI:** Use OpenFGA's docker image in tests, or mock the SDK?

### Resolved Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| OpenFGA deployment | **Centralized server** | Single OpenFGA server (or cluster) — all Silo pods call it over HTTP. Simplest operational model: one service to deploy, monitor, upgrade. No per-pod sidecar, no sync machinery. For this project (startup, small team), simplicity wins over marginal latency gains. |
| Model versioning | **Extend carefully, don't rename** | Stable model (owner/member). Only add new relations, never rename existing ones. Avoids migration complexity. When renaming becomes necessary, that's the signal to invest in versioning. |
| Caching | **No cache** | Not a real problem at this scale. Simpler to start without cache. Add caching only when latency profiling shows it's actually needed. |
| CI testing | **Real OpenFGA via testcontainer** | End-to-end test with real SDK catches integration bugs and model issues early. No mocking — tests verify the actual authorization behavior. |