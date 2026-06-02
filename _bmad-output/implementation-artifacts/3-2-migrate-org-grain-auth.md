# Story 3.2: Migrate OrgGrain Authorization

baseline_commit: ba3b2df
Status: review

## Story

As a platform engineer,
I want all OrgGrain authorization checks replaced with OpenFGA permission checks and membership tuples synced to OpenFGA,
so that authorization is declarative, centralized, and consistent across all operations.

## Acceptance Criteria

1. **All OrgGrain authorization goes through OpenFGA** — no inline `State.Members` role/permission checks remain in command methods (`RenameOrg`, `DeleteOrg`, `AddMember`, `RemoveMember`, `SendInvitation`)
2. **Permission mapping is correct** — `view_org` for member+owner ops, `delete_org`/`invite_member`/`remove_member` for owner-only ops
3. **Owner-only operations correctly deny Member calls** — `DeleteOrg`, `AddMember`, `RemoveMember`, `SendInvitation` reject non-owners
4. **Membership changes write/delete OpenFGA tuples** — `CreateOrg`, `AddMember`, `RemoveMember` sync tuples via the OrgProjector catch-up subscription
5. **Existing orgs seeded to OpenFGA via projector replay** — persistent subscription processes all historical events, naturally seeding tuples for pre-existing orgs
6. **`State.Members` dictionary preserved** — story 3.5 removes it; this story only replaces authorization checks, not the state model
7. **Read methods (`GetOrgInfo`, `GetMembers`, `IsMember`) unchanged** — these don't need authorization (controller handles access control)

## Tasks / Subtasks

- [x] Task 1: Inject `IOpenFgaAuthorizationService` into OrgGrain (AC: #1)
  - [x] Subtask 1.1: Add `IOpenFgaAuthorizationService` parameter to OrgGrain constructor, store as `_authService` field
  - [x] Subtask 1.2: Constructor signature becomes: `(IEventStreamClient eventStreamClient, TimeProvider timeProvider, IOpenFgaAuthorizationService authService)` — Orleans DI resolves it automatically

- [x] Task 2: Replace inline auth checks with OpenFGA Check calls (AC: #1, #2, #3)
  - [x] Subtask 2.1: `RenameOrg` — replace `State.Members.ContainsKey(requesterUserId)` with `await _authService.Check(requesterUserId, "view_org", State.OrgId)`. Throw `UnauthorizedAccessException` on deny.
  - [x] Subtask 2.2: `DeleteOrg` — replace `State.Members.TryGetValue(requesterUserId, out var role) || role != "Owner"` with `await _authService.Check(requesterUserId, "delete_org", State.OrgId)`. Throw `UnauthorizedAccessException` on deny.
  - [x] Subtask 2.3: `AddMember` — replace `State.Members.ContainsKey(requesterUserId)` with `await _authService.Check(requesterUserId, "invite_member", State.OrgId)`. Throw `UnauthorizedAccessException` on deny. **This tightens access from any-member to owner-only.**
  - [x] Subtask 2.4: `RemoveMember` — replace `State.Members.TryGetValue + requesterRole != "Owner"` with `await _authService.Check(requesterUserId, "remove_member", State.OrgId)`. Throw `UnauthorizedAccessException` on deny.
  - [x] Subtask 2.5: `SendInvitation` — replace `State.Members.ContainsKey(inviterUserId)` with `await _authService.Check(inviterUserId, "invite_member", State.OrgId)`. Throw `UnauthorizedAccessException` on deny. **This tightens access from any-member to owner-only.**

- [x] Task 3: Write OpenFGA tuples on membership events (AC: #4) — superseded by Task 7 (moved to projector)
  - [x] Subtask 3.1: `CreateOrg` — after `EmitEvent(OrgCreatedEvent)`, call `_authService.WriteTuples` with: `new AuthorizationTuple($"user:{ownerUserId}", "owner", $"organization:{orgId}")` — **removed in Task 7, now handled by projector**
  - [x] Subtask 3.2: `AddMember` — after `EmitEvent(MemberAddedEvent)`, call `_authService.WriteTuples` with: `new AuthorizationTuple($"user:{userId}", role.ToLowerInvariant(), $"organization:{State.OrgId}")` — **removed in Task 7, now handled by projector**
  - [x] Subtask 3.3: `RemoveMember` — capture role before EmitEvent, call `_authService.DeleteTuples` — **removed in Task 7, now handled by projector**

- [x] Task 4: Seed existing memberships on grain activation (AC: #5) — superseded by Task 7 (projector handles seeding)
  - [x] Subtask 4.1-4.4 — **removed in Task 7, projector replay handles seeding**

- [x] Task 5: Error handling (AC: #3, #6)
  - [x] Subtask 5.1: Use `UnauthorizedAccessException` for authorization failures — distinguishes from `InvalidOperationException` used for domain validation.
  - [x] Subtask 5.2: Keep all existing domain validation: `!Exists`, `State.IsDeleted`, duplicate member check, role validation (`"Owner"`/`"Member"`), owner-removal protection. These are NOT authorization.
  - [x] Subtask 5.3: Do NOT wrap OpenFGA calls in try/catch — the service's `FailureMode` (configured as `LogAndDeny`) handles errors internally.

- [x] Task 6: Build and verify (AC: all)
  - [x] Subtask 6.1: `dotnet build backend/src/Velucid.Silo/Velucid.Silo.csproj` — must compile clean
  - [x] Subtask 6.2: Verify no authorization-related `State.Members` lookups remain in command methods (grep `State.Members.ContainsKey` and `State.Members.TryGetValue` in OrgGrain.cs — should only appear in domain checks, not auth)

- [x] Task 7: Move tuple writes from OrgGrain to OrgProjector (AC: #4, #5)
  - [x] Subtask 7.1: Add `OpenFga.Sdk` NuGet package to `Velucid.ProjectorService.csproj`
  - [x] Subtask 7.2: Create `OpenFgaOptions` config class in ProjectorService (ApiUrl, StoreName, Enabled)
  - [x] Subtask 7.3: Create `OpenFgaTupleSync` service in ProjectorService — wraps OpenFGA client for `WriteTuples`/`DeleteTuples`, resolves store ID on startup
  - [x] Subtask 7.4: Register OpenFGA services in `ProjectorService/Program.cs`
  - [x] Subtask 7.5: Update `OrgProjector` — inject `OpenFgaTupleSync`, add tuple writes on `OrgCreated` (owner), `MemberAdded` (role), and tuple deletes on `MemberRemoved` (capture role from read model before removal)
  - [x] Subtask 7.6: Remove tuple writes from `OrgGrain.CreateOrg`, `AddMember`, `RemoveMember`
  - [x] Subtask 7.7: Remove `OnActivateAsync` override and `SeedMembershipAsync` from OrgGrain (projector replay handles seeding)
  - [x] Subtask 7.8: Update `OrgGrainTests` — share `InMemoryOpenFgaAuthorizationService` instance, manually sync tuples after grain operations (simulating projector behavior)

- [x] Task 8: Build and verify after refactor (AC: all)
  - [x] Subtask 8.1: `dotnet build` both Silo and ProjectorService — must compile clean
  - [x] Subtask 8.2: Run full test suite — all tests pass, no regressions

## Dev Notes

### Permission Mapping — Current vs OpenFGA

| Method | Current Check | OpenFGA Permission | Who Can Access | Behavior Change? |
|--------|--------------|-------------------|----------------|-----------------|
| `RenameOrg` | `State.Members.ContainsKey(userId)` | `view_org` | Owner + Member | No |
| `DeleteOrg` | `role == "Owner"` | `delete_org` | Owner only | No |
| `AddMember` | `State.Members.ContainsKey(userId)` | `invite_member` | Owner only | **Yes — tightened** |
| `RemoveMember` | `role == "Owner"` | `remove_member` | Owner only | No |
| `SendInvitation` | `State.Members.ContainsKey(userId)` | `invite_member` | Owner only | **Yes — tightened** |

### OpenFGA Tuple Format

```
User:       "user:{guid}"           e.g. "user:f47ac10b-58cc-4372-a567-0e02b2c3d479"
Relation:   "owner" or "member"     (lowercase — matches OpenFGA model relations)
Object:     "organization:{guid}"   e.g. "organization:abc12345-..."
```

AuthorizationTuple record: `new AuthorizationTuple(string User, string Relation, string Object)`

### Constructor Change

Current:
```csharp
public OrgGrain(IEventStreamClient eventStreamClient, TimeProvider timeProvider)
    : base(eventStreamClient, timeProvider) { }
```

After:
```csharp
private readonly IOpenFgaAuthorizationService _authService;

public OrgGrain(
    IEventStreamClient eventStreamClient,
    TimeProvider timeProvider,
    IOpenFgaAuthorizationService authService)
    : base(eventStreamClient, timeProvider)
{
    _authService = authService;
}
```

No DI registration changes needed — `IOpenFgaAuthorizationService` already registered in Program.cs.

### RemoveMember — Capture Role Before Event

`EmitEvent` applies the event to state synchronously. For `MemberRemovedEvent`, `Apply()` removes the user from `State.Members`. Must capture role first:

```csharp
var removedRole = State.Members[userId]; // capture BEFORE EmitEvent
await EmitEvent(new MemberRemovedEvent(State.OrgId, userId, requesterUserId, UtcNow));
await _authService.DeleteTuples([
    new AuthorizationTuple($"user:{userId}", removedRole.ToLowerInvariant(), $"organization:{State.OrgId}")
]);
```

### Seeding — Idempotent by Design

OpenFGA silently ignores duplicate tuple writes. Seeding on every grain activation is safe and requires no tracking flags or migration state.

### Auth Check Pattern

For every command method, the pattern is:
1. Domain validation (exists, not deleted, etc.) — keep as-is
2. Authorization check via OpenFGA — new
3. Domain business logic (duplicate checks, etc.) — keep as-is
4. Emit event — keep as-is
5. Sync tuple to OpenFGA — new

### What NOT to Change

- `IOrgGrain.cs` — no interface changes
- `OrgController.cs` — controller changes are story 3.4
- `OrgState.cs` — state model changes are story 3.5
- `EventTypeMapping.cs` — no new events
- `OrgProjector.cs` — no projection changes
- Event files — no new event types
- `Apply()` method — continues maintaining `State.Members` from events
- Read methods (`GetOrgInfo`, `GetMembers`, `IsMember`) — no auth needed

### Files MODIFIED

- `backend/src/Velucid.Silo/Grains/OrgGrain.cs` — constructor, command methods, OnActivateAsync override

### Files READ (reference only)

- `backend/src/Velucid.Silo/Authorization/IOpenFgaAuthorizationService.cs` — `Check`, `WriteTuples`, `DeleteTuples`
- `backend/src/Velucid.Silo/Authorization/AuthorizationTuple.cs` — `record AuthorizationTuple(string User, string Relation, string Object)`
- `backend/src/Velucid.Silo/Authorization/velucid-auth-model.fga` — permission definitions
- `backend/src/Velucid.Silo/Grains/EventSourcedGrain.cs` — `OnActivateAsync` is virtual, can be overridden

### References

- [Source: _bmad-output/planning-artifacts/epic-3-authorization-openfga.md#Story 3.2]
- [Source: _bmad-output/implementation-artifacts/3-1-openfga-infrastructure.md — previous story learnings]
- [Source: backend/src/Velucid.Silo/Authorization/IOpenFgaAuthorizationService.cs]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- All 5 inline `State.Members` authorization checks replaced with `_authService.Check()` calls using correct OpenFGA permissions (`view_org`, `delete_org`, `invite_member`, `remove_member`)
- Auth failures now throw `UnauthorizedAccessException` (distinct from domain `InvalidOperationException`)
- `AddMember` and `SendInvitation` tightened from any-member to owner-only (per OpenFGA model)
- **Refactored:** Moved OpenFGA tuple writes from OrgGrain to OrgProjector catch-up subscription — eliminates inconsistency window between event emission and tuple sync
- OrgProjector now writes owner tuple on `OrgCreated`, member tuple on `MemberAdded`, and deletes tuple on `MemberRemoved` (captures role from read model before removal)
- Removed `OnActivateAsync` seeding from OrgGrain — projector replay handles seeding for pre-existing orgs naturally
- OrgGrain now only uses `_authService` for `Check()` calls (read-only authorization)
- Added `OpenFgaTupleSync` service to ProjectorService — thin wrapper for OpenFGA tuple writes/deletes with store ID resolution
- Tests manually sync tuples to mock auth service (simulating projector behavior) via `SyncOwnerTuple`/`SyncMemberTuple` helpers
- Both Silo and ProjectorService build clean, all 46 tests pass (0 regressions)

### File List

**Files MODIFIED:**
- `backend/src/Velucid.Silo/Grains/OrgGrain.cs` — constructor (removed ILogger, removed ILogger), auth checks only, no tuple writes or seeding
- `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs` — added OpenFGA tuple sync on OrgCreated, MemberAdded, MemberRemoved
- `backend/src/Velucid.ProjectorService/Program.cs` — registered OpenFGA options and tuple sync service
- `backend/src/Velucid.ProjectorService/Velucid.ProjectorService.csproj` — added OpenFga.Sdk package
- `backend/tests/Velucid.Silo.Tests/Grains/OrgGrainTests.cs` — shared auth mock, manual tuple sync helpers
- `backend/tests/Velucid.Silo.Tests/Infrastructure/TestSiloConfigurator.cs` — registered shared auth mock

**Files CREATED:**
- `backend/tests/Velucid.Silo.Tests/Infrastructure/InMemoryOpenFgaAuthorizationService.cs` — test mock
- `backend/src/Velucid.ProjectorService/Configuration/OpenFgaOptions.cs` — config class
- `backend/src/Velucid.ProjectorService/Services/OpenFgaTupleSync.cs` — tuple sync service
