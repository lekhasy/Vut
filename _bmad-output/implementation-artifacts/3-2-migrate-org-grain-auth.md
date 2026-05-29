# Story 3.2: Migrate OrgGrain Authorization

baseline_commit: (use commit from 3-1 after it completes)
Status: backlog

## Story

As a platform engineer,
I want OrgGrain to use OpenFGA for all authorization decisions instead of inline role checks,
so that authorization is centralized and consistent.

## Acceptance Criteria

1. **All inline auth checks removed from OrgGrain** — `RenameOrg`, `DeleteOrg`, `AddMember`, `RemoveMember`, `SendInvitation`
2. **Each operation uses `IOpenFgaAuthorizationService.Check`** — replace inline `State.Members.ContainsKey()` and role string checks
3. **Membership changes write tuples** — `MemberAddedEvent` → write `organization:{orgId}#member@user:{userId}` tuple; `MemberRemovedEvent` → delete tuple
4. **Owner tuple written on org creation** — `OrgCreatedEvent` handler writes `organization:{orgId}#owner@user:{creatorUserId}`
5. **Existing orgs migrated on first access** — when an OrgGrain activates, if OpenFGA has no tuples for it, seed from `OrgState.Members`

## Tasks / Subtasks

- [ ] Task 1: Remove inline auth from RenameOrg
  - [ ] Subtask 1.1: Remove `State.Members.ContainsKey(requesterUserId)` check
  - [ ] Subtask 1.2: Add `await _authService.Check(requesterUserId, "view_org", orgId)` — rename is a transparent op, so `view_org` check is sufficient
  - [ ] Subtask 1.3: Verify same behavior as before (member or owner allowed)
- [ ] Task 2: Remove inline auth from DeleteOrg
  - [ ] Subtask 2.1: Remove `role != "Owner"` check
  - [ ] Subtask 2.2: Add `await _authService.Check(requesterUserId, "delete_org", orgId)`
  - [ ] Subtask 2.3: Verify only owner is allowed
- [ ] Task 3: Remove inline auth from AddMember
  - [ ] Subtask 3.1: Remove `State.Members.ContainsKey(requesterUserId)` check
  - [ ] Subtask 3.2: Add `await _authService.Check(requesterUserId, "view_org", orgId)` — member can invite
  - [ ] Subtask 3.3: On success, write `organization:{orgId}#member@user:{newMemberId}` tuple
- [ ] Task 4: Remove inline auth from RemoveMember
  - [ ] Subtask 4.1: Remove `requesterRole != "Owner"` check
  - [ ] Subtask 4.2: Add `await _authService.Check(requesterUserId, "remove_member", orgId)`
  - [ ] Subtask 4.3: On success, delete `organization:{orgId}#member@user:{removedUserId}` tuple
- [ ] Task 5: Remove inline auth from SendInvitation
  - [ ] Subtask 5.1: Remove `State.Members.ContainsKey(inviterUserId)` check
  - [ ] Subtask 5.2: Add `await _authService.Check(inviterUserId, "invite_member", orgId)`
  - [ ] Subtask 5.3: Invitation stores role — also write tuple for that role once invitation accepted
- [ ] Task 6: Migrate existing org memberships to OpenFGA
  - [ ] Subtask 6.1: In `OrgGrain` constructor or `OnActivateAsync`, check if OpenFGA has tuples for this org
  - [ ] Subtask 6.2: If not, read `State.Members` and write all tuples to OpenFGA
  - [ ] Subtask 6.3: Log migration so we can verify it ran

## Dev Notes

### Permission Mapping

| OrgGrain Method | OpenFGA Permission |
|-----------------|-------------------|
| RenameOrg | `view_org` (transparent — any member can rename) |
| DeleteOrg | `delete_org` (owner only) |
| AddMember | `view_org` (transparent — any member can invite) |
| RemoveMember | `remove_member` (owner only) |
| SendInvitation | `invite_member` (owner only) |

### Tuple Write Timing

- **OrgCreatedEvent**: write `organization:{orgId}#owner@user:{creatorUserId}`
- **MemberAddedEvent**: write `organization:{orgId}#member@user:{memberUserId}` (if role == Member)
- **MemberRemovedEvent**: delete `organization:{orgId}#member@user:{memberUserId}`
- **Invitation accepted**: write appropriate tuple based on role from invitation

### Migration Logic

```csharp
// In OnActivateAsync or constructor
private async Task MigrateMembershipToOpenFga()
{
    var existingTuples = await _authService.GetTuplesForObject($"organization:{this.GetPrimaryKey()}");
    if (existingTuples.Count == 0 && State.Members.Count > 0)
    {
        var tuples = State.Members.Select(kvp =>
            new AuthorizationTuple
            {
                User = $"user:{kvp.Key}",
                Relation = kvp.Value.ToLowerInvariant(), // "owner" or "member"
                Object = $"organization:{this.GetPrimaryKey()}"
            });
        await _authService.WriteTuples(tuples);
        _logger.LogInformation("Migrated {Count} membership tuples to OpenFGA for org {OrgId}", State.Members.Count, this.GetPrimaryKey());
    }
}
```

### Null State Handling

If `State.Members` is empty and OpenFGA has no tuples, the org may have been created before this migration. On next membership change, tuples will be written.

## File List

**Files to MODIFY:**
- `backend/src/Velucid.Silo/Grains/OrgGrain.cs` — replace all inline auth checks with OpenFGA calls; add tuple writes

**Files to READ:**
- `backend/src/Velucid.Silo/Grains/OrgGrain.cs` — current inline auth implementations
- `backend/src/Velucid.Silo/Authorization/IOpenFgaAuthorizationService.cs` — from Story 3.1

## References

- Epic 3 spec: `_bmad-output/planning-artifacts/epic-3-authorization-openfga.md`
- Story 3.1: `_bmad-output/implementation-artifacts/3-1-openfga-infrastructure.md`
- Current OrgGrain: `backend/src/Velucid.Silo/Grains/OrgGrain.cs`