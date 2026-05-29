# Story 3.5: Remove Inline Auth from OrgState

baseline_commit: (use commit from 3-2 after it completes)
Status: backlog

## Story

As a platform engineer,
I want OrgState to no longer contain the Members dictionary,
so that OpenFGA is the sole source of truth for authorization.

## Acceptance Criteria

1. **OrgState.Members removed** — `Dictionary<Guid, string>` no longer in OrgState
2. **Grain methods no longer read from State.Members** — all role checks go through OpenFGA
3. **Projector updated** — if projector reads from Members, it now uses OpenFGA or a read model query
4. **Existing tests updated** — tests that assert on State.Members behavior are updated
5. **Migration verified** — all existing orgs work with OpenFGA as the auth source

## Tasks / Subtasks

- [ ] Task 1: Remove Members from OrgState
  - [ ] Subtask 1.1: Remove `public Dictionary<Guid, string> Members { get; set; }` from OrgState.cs
  - [ ] Subtask 1.2: Update event handlers that reference `State.Members` (e.g., MemberAddedEvent applying to `State.Members`)
- [ ] Task 2: Update OrgGrain to never read State.Members
  - [ ] Subtask 2.1: Find all references to `State.Members` in OrgGrain.cs
  - [ ] Subtask 2.2: Replace with OpenFGA Check calls or read model queries
- [ ] Task 3: Update OrgProjector
  - [ ] Subtask 3.1: Check if OrgProjector reads State.Members for anything
  - [ ] Subtask 3.2: If so, update to query OpenFGA or use read model projections
- [ ] Task 4: Update tests
  - [ ] Subtask 4.1: OrgGrainTests that assert on State.Members — update to assert on OpenFGA calls or remove
  - [ ] Subtask 4.2: Any test that sets up Members dictionary for auth — update to mock OpenFGA
- [ ] Task 5: Verify migration end-to-end
  - [ ] Subtask 5.1: Start Silo with existing DB (has orgs with Members populated)
  - [ ] Subtask 5.2: Verify OpenFGA migration fires on first grain activation (Story 3.2)
  - [ ] Subtask 5.3: Verify all org operations still work after migration

## Dev Notes

### Why This Matters

Before OpenFGA, `OrgState.Members` was both:
1. Aggregate state (who belongs to this org)
2. Authorization source (role checks read from this dictionary)

Now that OpenFGA is the authorization source, `OrgState.Members` is redundant and confusing — it could drift from OpenFGA's tuple store.

### What Replaces State.Members

- **For authorization**: `IOpenFgaAuthorizationService.Check()` — always
- **For membership display**: read model queries via `OrgMemberProjection` (already exists)
- **For aggregate logic**: only if the grain needs to know who belongs for its own operations — query OpenFGA

### Event Handler Changes

```csharp
// Before (MemberAddedEvent handler)
private void Apply(OrgState state, MemberAddedEvent e)
{
    state.Members[e.UserId] = e.Role;
}

// After (MemberAddedEvent handler — still update state for aggregate purposes)
private void Apply(OrgState state, MemberAddedEvent e)
{
    // State no longer has Members — only track aggregate data here
    // Authorization tuples written separately via OpenFgaAuthorizationService
}
```

### Read Model Still Exists

`OrgMemberProjection` is the read model for membership queries — this is separate from OpenFGA tuples and should stay. It powers UI (member lists, role displays) but is not used for authorization.

## File List

**Files to MODIFY:**
- `backend/src/Velucid.Silo/Models/OrgState.cs` — remove Members dictionary
- `backend/src/Velucid.Silo/Grains/OrgGrain.cs` — remove all State.Members references
- `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs` — update if any State.Members references
- `backend/tests/Velucid.Silo.Tests/Grains/OrgGrainTests.cs` — update tests

**Files to READ:**
- `backend/src/Velucid.Silo/Models/OrgState.cs`
- `backend/src/Velucid.Silo/Grains/OrgGrain.cs`
- `backend/tests/Velucid.Silo.Tests/Grains/OrgGrainTests.cs`

## References

- Epic 3 spec: `_bmad-output/planning-artifacts/epic-3-authorization-openfga.md`
- Story 3.2: `_bmad-output/implementation-artifacts/3-2-migrate-org-grain-auth.md`