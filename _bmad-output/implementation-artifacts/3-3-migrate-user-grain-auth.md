# Story 3.3: Migrate UserGrain Authorization

baseline_commit: (use commit from 3-1 or 3-2 after later one completes)
Status: backlog

## Story

As a platform engineer,
I want UserGrain to enforce that users can only operate on their own profiles,
so that the current security hole (anyone can pass any userId) is closed.

## Acceptance Criteria

1. **UpdateProfile rejects cross-user calls** — if `callerUserId != targetUserId`, throw `UnauthorizedAccessException`
2. **RequestEmailVerification rejects cross-user calls** — same check
3. **VerifyEmail rejects cross-user calls** — same check
4. **Error message is clear** — "You can only perform this operation on your own account"

## Tasks / Subtasks

- [ ] Task 1: Fix UpdateProfile authorization
  - [ ] Subtask 1.1: Add `if (callerUserId != userId) throw new UnauthorizedAccessException("You can only perform this operation on your own account")`
  - [ ] Subtask 1.2: Current implementation takes `userId` as param from caller — determine caller identity (grain's own UserId? Or passed in?)
  - [ ] Subtask 1.3: Determine whether this needs OpenFGA or is self-service only
- [ ] Task 2: Fix RequestEmailVerification authorization
  - [ ] Subtask 2.1: Same pattern — validate caller == target
- [ ] Task 3: Fix VerifyEmail authorization
  - [ ] Subtask 3.1: Same pattern — validate caller == target
- [ ] Task 4: Consider OpenFGA for user-scoped authorization
  - [ ] Subtask 4.1: For self-service operations, direct `caller == target` check is sufficient
  - [ ] Subtask 4.2: If we later need delegation (user A can act on behalf of user B), add `work_on_user` relation to model
  - [ ] Subtask 4.3: For now, document that user-scoped ops use self-authorization pattern

## Dev Notes

### Current UserGrain Problem

Looking at `UserGrain.UpdateProfile`, `RequestEmailVerification`, `VerifyEmail` — they take `userId` as a parameter from the caller with no validation. Any caller can pass any userId.

### Fix Pattern

The grain's primary key IS the userId. So the caller identity should be implicit — if someone is calling `UserGrain` for user X, the grain itself IS user X. The grain doesn't need to receive userId as a parameter — it already knows it.

But if we need to support admin scenarios later (owner can act on behalf of user), we'd add a `delegate` field or use OpenFGA.

For now: since `UserGrain` has `this.GetPrimaryKey()` as the userId, the grain's identity IS the target user. Any caller-supplied `userId` parameter should be removed or validated against the grain's own identity.

### Open Question

Is `userId` passed into these methods just for convenience (to route to the right grain), or is it used as the target? If it's the target, remove it and use `this.GetPrimaryKey()`. If callers pass a different userId to route to a different grain, that's a security issue in the calling controller.

## File List

**Files to MODIFY:**
- `backend/src/Velucid.Silo/Grains/UserGrain.cs` — add self-authorization checks

**Files to READ:**
- `backend/src/Velucid.Silo/Grains/UserGrain.cs` — current implementation of UpdateProfile, RequestEmailVerification, VerifyEmail
- `backend/src/Velucid.Silo/Controllers/UserController.cs` — how these are called

## References

- Epic 3 spec: `_bmad-output/planning-artifacts/epic-3-authorization-openfga.md`
- Story 3.2: `_bmad-output/implementation-artifacts/3-2-migrate-org-grain-auth.md`