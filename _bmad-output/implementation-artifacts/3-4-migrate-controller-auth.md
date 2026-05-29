# Story 3.4: Migrate Controller Authorization

baseline_commit: (use commit from 3-1 or 3-2 after later one completes)
Status: backlog

## Story

As a platform engineer,
I want all controller endpoints to use server-side userId from auth middleware instead of caller-supplied query params,
so that the authorization chain is solid end-to-end.

## Acceptance Criteria

1. **OrgController uses context.locals.userId** — all endpoints that pass `userId` to grains use `context.locals.userId` from server-side auth middleware
2. **UserController uses context.locals.userId** — same pattern
3. **No endpoint accepts userId from request body or query param** — caller-supplied userId is never used for authorization decisions
4. **All controller endpoints that call grains pass the server-resolved userId**

## Tasks / Subtasks

- [ ] Task 1: Audit all controllers for userId parameter sources
  - [ ] Subtask 1.1: List all controller endpoints that pass `userId` to grains
  - [ ] Subtask 1.2: Identify which use `context.locals.userId` vs query param vs request body
  - [ ] Subtask 1.3: Categorize: already correct vs needs fix
- [ ] Task 2: Fix OrgController userId sources
  - [ ] Subtask 2.1: `UpdateOrg` — currently passes query param userId
  - [ ] Subtask 2.2: `DeleteOrg` — currently passes query param userId
  - [ ] Subtask 2.3: All other endpoints that pass userId
  - [ ] Subtask 2.4: Verify `context.locals.userId` is available in all routes (check middleware)
- [ ] Task 3: Fix UserController userId sources
  - [ ] Subtask 3.1: `UpdateProfile` — passes query param userId
  - [ ] Subtask 3.2: `RequestEmailVerification` — passes query param userId
  - [ ] Subtask 3.3: `VerifyEmail` — passes query param userId
- [ ] Task 4: Audit Astro API routes for same issues
  - [ ] Subtask 4.1: Frontend BFF routes at `frontend/src/pages/api/` also pass userId to Silo
  - [ ] Subtask 4.2: Ensure these use server-side `locals.userId` before calling Silo

## Dev Notes

### Auth Middleware Flow

1. User authenticates via Auth0 → session cookie
2. `frontend/src/middleware.ts` decrypts session, sets `context.locals.userId`
3. API routes read `context.locals.userId` (trusted, server-set)
4. API routes call Silo with this userId

### Current Problem

`OrgController.GetOrg`, `ListMembers`, etc. currently receive `userId` as a query param from the caller. The caller (frontend) gets it from localStorage or the store — not verified server-side.

### Fix Pattern

```typescript
// frontend/src/pages/api/orgs/[orgId]/index.ts
export const PUT: APIRoute = async ({ request, locals, params }) => {
  const orgId = params.orgId;
  const userId = locals.userId; // server-side, trusted

  // Call Silo with server-side userId
  const response = await fetch(`${API_URL}/api/orgs/${orgId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId }) // trusted server-side userId
  });
};
```

Same fix applies to Silo controllers — they receive `userId` from the API layer (which is now trusted), not from the original caller.

## File List

**Files to MODIFY:**
- `backend/src/Velucid.Silo/Controllers/OrgController.cs` — use server-side userId
- `backend/src/Velucid.Silo/Controllers/UserController.cs` — use server-side userId
- `frontend/src/pages/api/orgs/[orgId]/index.ts`
- `frontend/src/pages/api/orgs/[orgId]/invitations/index.ts`
- `frontend/src/pages/api/orgs/[orgId]/members/index.ts`
- `frontend/src/pages/api/orgs/[orgId]/members/[userId].ts`
- `frontend/src/pages/api/user/*.ts`

**Files to READ:**
- `backend/src/Velucid.Silo/Controllers/OrgController.cs`
- `backend/src/Velucid.Silo/Controllers/UserController.cs`
- `frontend/src/middleware.ts`

## References

- Epic 3 spec: `_bmad-output/planning-artifacts/epic-3-authorization-openfga.md`
- Story 3.2: `_bmad-output/implementation-artifacts/3-2-migrate-org-grain-auth.md`
- Story 3.3: `_bmad-output/implementation-artifacts/3-3-migrate-user-grain-auth.md`