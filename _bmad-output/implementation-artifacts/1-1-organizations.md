# Story 1.1: Organizations

baseline_commit: 219a05fd952753d7e46b9d283125891fcc0afe1d
Status: review

## Story

As a team lead / organization owner,
I want to create and manage my organization with members,
so that my team can collaborate on products and tasks within a shared workspace.

## Acceptance Criteria

1. **User can create an organization** — user becomes owner automatically
2. **User can view their org list in sidebar** — dropdown shows all orgs they belong to
3. **User can navigate to org dashboard** — org landing page shows product list
4. **User can invite a member by email** — invitation stored in DB, no email sent yet
5. **Owner can remove members** — only owners can remove non-owner members

## Tasks / Subtasks

- [x] Task 1: Create `OrgGrain` with `[GrainType("org")]` (AC: #1, #3)
  - [x] Subtask 1.1: Define `IOrgGrain` interface in `backend/src/Velucid.Silo/Grains/IOrgGrain.cs`
  - [x] Subtask 1.2: Implement `OrgGrain` in `backend/src/Velucid.Silo/Grains/OrgGrain.cs` — extend `EventSourcedGrain<OrgState>`
  - [x] Subtask 1.3: Add `OrgState` model in `backend/src/Velucid.Silo/Models/OrgState.cs`
  - [x] Subtask 1.4: Create events: `OrgCreatedEvent`, `OrgRenamedEvent`, `OrgDeletedEvent`, `MemberAddedEvent`, `MemberRemovedEvent`, `InvitationSentEvent` in `backend/src/Velucid.Silo/Events/`
  - [x] Subtask 1.5: Register all event types in `EventTypeMapping.cs`
- [x] Task 2: Create/update projection entities + EF Core migration (AC: #2, #4, #5)
  - [x] Subtask 2.1: `OrgProjection` entity already exists at `backend/src/Velucid.ReadModel/Entities/OrgProjection.cs` — **READ THIS FILE** before modifying
  - [x] Subtask 2.2: `OrgMemberProjection` entity already exists at `backend/src/Velucid.ReadModel/Entities/OrgMemberProjection.cs` — **READ THIS FILE** before modifying
  - [x] Subtask 2.3: `OrgInvitationProjection` entity already exists at `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs`
  - [x] Subtask 2.4: Add migration for `OrgInvitationProjection` (already included in existing migrations)
  - [x] Subtask 2.5: `UserOrgProjection` entity already exists at `backend/src/Velucid.ReadModel/Entities/UserOrgProjection.cs`
- [x] Task 3: Create `OrgController` on Silo (AC: #1, #2, #3, #4, #5)
  - [x] Subtask 3.1: `GET /api/orgs` — list user's orgs via ReadModelDbContext query on `OrgMemberProjection`
  - [x] Subtask 3.2: `POST /api/orgs` — create org (delegate to OrgGrain), then return org data
  - [x] Subtask 3.3: `GET /api/orgs/{orgId}` — get org details
  - [x] Subtask 3.4: `PUT /api/orgs/{orgId}` — rename org
  - [x] Subtask 3.5: `DELETE /api/orgs/{orgId}` — soft delete org
  - [x] Subtask 3.6: `POST /api/orgs/{orgId}/invitations` — send invitation (store in DB, no email)
  - [x] Subtask 3.7: `DELETE /api/orgs/{orgId}/members/{userId}` — remove member (only if requester is owner)
  - [x] Subtask 3.8: `GET /api/orgs/{orgId}/members` — list all members with roles
- [x] Task 4: Create Astro API routes (frontend BFF layer) — **CREATE NEW DIRECTORY STRUCTURE**
  - [x] Subtask 4.1: `src/pages/api/orgs/index.ts` — GET (list), POST (create)
  - [x] Subtask 4.2: `src/pages/api/orgs/[orgId]/index.ts` — GET, PUT, DELETE
  - [x] Subtask 4.3: `src/pages/api/orgs/[orgId]/invitations/index.ts` — POST
  - [x] Subtask 4.4: `src/pages/api/orgs/[orgId]/members/index.ts` — GET
  - [x] Subtask 4.5: `src/pages/api/orgs/[orgId]/members/[userId].ts` — DELETE
- [x] Task 5: Create projector handler for org projections
  - [x] Subtask 5.1: Create `OrgProjector.cs` handler in `backend/src/Velucid.ProjectorService/Handlers/`
  - [x] Subtask 5.2: Handle all org events and update `OrgProjection`, `OrgMemberProjection`, `OrgInvitationProjection`
  - [x] Subtask 5.3: Register projector in `Program.cs` of ProjectorService
  - [x] Subtask 5.4: **READ EXISTING PROJECTOR FILES** (`UserProjector.cs`) to understand the handler pattern
- [x] Task 6: Frontend — org selector in sidebar (AC: #2)
  - [x] Subtask 6.1: Read existing `OrgSelector.astro` at `frontend/src/components/sidebar/OrgSelector.astro`
  - [x] Subtask 6.2: Wire up `organizations` store at `frontend/src/stores/organizations.ts` — already exists, read it
  - [x] Subtask 6.3: Load orgs on app initialization via GET /api/orgs
  - [x] Subtask 6.4: Implement `currentOrgId` atom for selection state
- [x] Task 7: Frontend — org creation modal (AC: #1)
  - [x] Subtask 7.1: Add create org form/modal in sidebar or dashboard
  - [x] Subtask 7.2: POST to `/api/orgs` and update `organizations` store on success
- [x] Task 8: Frontend — org settings page (AC: #3, #4, #5)
  - [x] Subtask 8.1: Enhance existing `src/pages/orgs/[orgId]/settings.astro` — **READ THIS FILE FIRST**
  - [x] Subtask 8.2: Add member list display with roles
  - [x] Subtask 8.3: Add remove member capability (DELETE request)
  - [x] Subtask 8.4: Add invitation form (POST to /api/orgs/{orgId}/invitations)

## Dev Notes

### Critical Architecture Patterns

**Event-Sourced Grain Pattern** (from `EventSourcedGrain.cs`):
- Grain base class with `Apply(state, event)` method for event handling
- `EmitEvent(event)` for persistence + state update
- `BuildStreamId()` must return `"org-{orgId}"` format
- `Exists` property tracks whether aggregate has been created
- Optimistic concurrency via `_currentStreamState`

**Projector Pattern** (from `UserProjector.cs`):
- Projectors handle events from the event store and update read model projections
- One projector per aggregate (or per group of related projections)
- Uses `IEventStreamClient` subscription to process events
- Checkpointing via `ProjectionCheckpoint` table for resumable processing

**Frontend API Route Pattern** (from `middleware.ts`, `api.ts`):
- API routes in `src/pages/api/` mirror the Silo controller structure
- Use `API_URL` env variable to call Silo (defaults to `http://localhost:5000`)
- Return JSON responses; handle auth via session cookie already decrypted in middleware
- `context.locals.userId` available after auth middleware

### Project Structure Alignment

**Backend Paths:**
- Grains: `backend/src/Velucid.Silo/Grains/`
- Events: `backend/src/Velucid.Silo/Events/`
- Controllers: `backend/src/Velucid.Silo/Controllers/`
- Models: `backend/src/Velucid.Silo/Models/`
- Projector Handlers: `backend/src/Velucid.ProjectorService/Handlers/`
- ReadModel Entities: `backend/src/Velucid.ReadModel/Entities/`

**Frontend Paths:**
- API Routes: `frontend/src/pages/api/orgs/[orgId]/...`
- Stores: `frontend/src/stores/organizations.ts`
- Components: `frontend/src/components/sidebar/OrgSelector.astro`
- Pages: `frontend/src/pages/orgs/[orgId]/settings.astro`

**Existing Entities (DO NOT RECREATE — READ AND EXTEND):**
- `OrgProjection.cs` — exists at `backend/src/Velucid.ReadModel/Entities/OrgProjection.cs`
- `OrgMemberProjection.cs` — exists at `backend/src/Velucid.ReadModel/Entities/OrgMemberProjection.cs`
- `UserGrain.cs` — study this for grain patterns
- `UserController.cs` — study this for controller patterns
- `UserProjector.cs` — study this for projector patterns

### Key Implementation Notes

1. **Stream ID format:** `org-{orgId}` — use `this.GetPrimaryKey()` as the orgId GUID
2. **Invitation state:** Store invitation with `Email`, `InviterUserId`, `CreatedAt`, `Status (Pending/Accepted/Declined)`
3. **Membership roles:** `Owner` and `Member` — owner cannot remove themselves
4. **Soft delete:** Org has `IsDeleted` flag; soft-deleted orgs excluded from queries
5. **Authorization:** Only owner can delete org or remove members; check via `OrgMemberProjection` role
6. **No email sending:** Invitation is stored only; no email service integration yet

### Testing Standards

- Unit tests for `OrgGrain` command handling and event emission
- Unit tests for `OrgController` HTTP endpoint responses
- Integration tests for projector: event → projection update
- Frontend: API route tests (if test framework added)

### References

- Epic 1 story details: `_bmad-output/planning-artifacts/epic-1-core-platform-kanban-mvp.md` [Story 1.1]
- Event sourcing base: `backend/src/Velucid.Silo/Grains/EventSourcedGrain.cs`
- User grain example: `backend/src/Velucid.Silo/Grains/UserGrain.cs`
- Projector example: `backend/src/Velucid.ProjectorService/Handlers/UserProjector.cs`
- Existing projection entities:
  - `backend/src/Velucid.ReadModel/Entities/OrgProjection.cs`
  - `backend/src/Velucid.ReadModel/Entities/OrgMemberProjection.cs`
- Frontend stores: `frontend/src/stores/organizations.ts`
- Frontend existing org pages: `frontend/src/pages/orgs/[orgId]/`
- Frontend auth middleware: `frontend/src/middleware.ts`

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

**Files to CREATE:**
- `backend/src/Velucid.Silo/Grains/IOrgGrain.cs`
- `backend/src/Velucid.Silo/Grains/OrgGrain.cs`
- `backend/src/Velucid.Silo/Models/OrgState.cs`
- `backend/src/Velucid.Silo/Events/OrgCreatedEvent.cs`
- `backend/src/Velucid.Silo/Events/OrgRenamedEvent.cs`
- `backend/src/Velucid.Silo/Events/OrgDeletedEvent.cs`
- `backend/src/Velucid.Silo/Events/MemberAddedEvent.cs`
- `backend/src/Velucid.Silo/Events/MemberRemovedEvent.cs`
- `backend/src/Velucid.Silo/Events/InvitationSentEvent.cs`
- `backend/src/Velucid.Silo/Controllers/OrgController.cs`
- `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs`
- `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs`
- `frontend/src/pages/api/orgs/index.ts`
- `frontend/src/pages/api/orgs/[orgId]/index.ts`
- `frontend/src/pages/api/orgs/[orgId]/invitations/index.ts`
- `frontend/src/pages/api/orgs/[orgId]/members/index.ts`
- `frontend/src/pages/api/orgs/[orgId]/members/[userId].ts`

**Files to MODIFY:**
- `backend/src/Velucid.Silo/Events/EventTypeMapping.cs` — register new event types
- `backend/src/Velucid.ReadModel/ReadModelDbContext.cs` — add OrgInvitationProjection DbSet
- `backend/src/Velucid.ProjectorService/Program.cs` — register OrgProjector
- `frontend/src/stores/organizations.ts` — enhance for org CRUD
- `frontend/src/components/sidebar/OrgSelector.astro` — wire up API
- `frontend/src/pages/orgs/[orgId]/settings.astro` — add member management
- `backend/src/Velucid.Silo/Program.cs` — add OrgController DI (if needed)

**Files to READ (before modifying):**
- `backend/src/Velucid.ReadModel/Entities/OrgProjection.cs`
- `backend/src/Velucid.ReadModel/Entities/OrgMemberProjection.cs`
- `backend/src/Velucid.Silo/Grains/UserGrain.cs`
- `backend/src/Velucid.Silo/Controllers/UserController.cs`
- `backend/src/Velucid.ProjectorService/Handlers/UserProjector.cs`
- `frontend/src/components/sidebar/OrgSelector.astro`
- `frontend/src/pages/orgs/[orgId]/settings.astro`

## Change Log

| Date | Changes |
|------|---------|
| 2026-05-28 | Implemented complete org management: OrgGrain with event sourcing, OrgController with all CRUD endpoints, OrgProjector for read model updates, frontend API routes, OrgSelector component with create modal, and org settings page with member management and invitation features. |

### Review Findings

- [x] [Review][Patch] Frontend localStorage auth spoofing on isOwner — `frontend/src/pages/orgs/[orgId]/settings.astro:124-127` — mitigated: backend grain authorization now enforced on all paths; localStorage read is inert since backend rejects unauthorized requests. Still recommend migrating to Astro.locals server-side userId.
- [x] [Review][Patch] DeleteOrg has no owner authorization in grain — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:96-107` — fixed: grain now takes `requesterUserId` and validates role is "Owner"
- [x] [Review][Patch] GetOrg exposes org data to non-members — `backend/src/Velucid.Silo/Controllers/OrgController.cs:66-80` — fixed: controller now queries membership before returning org data
- [x] [Review][Patch] ListMembers exposes member PII to non-members — `backend/src/Velucid.Silo/Controllers/OrgController.cs:119-142` — fixed: controller now checks membership via OrgMemberProjections before listing members
- [x] [Review][Patch] UpdateOrg/RenameOrg missing membership authorization — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:81-94` — fixed: grain now takes `requesterUserId` and validates caller is a member
- [x] [Review][Patch] Invitation status reverted to Pending on projector replay — `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs:426-449` — fixed: now preserves Accepted status on projector replay
- [x] [Review][Patch] GetOrg hardcodes "Owner" when membership lookup fails — `backend/src/Velucid.Silo/Controllers/OrgController.cs:557` — fixed: returns 404 if membership not found
- [x] [Review][Patch] sendInvitation drops inviterUserId — `frontend/src/pages/api/orgs/[orgId]/invitations/index.ts` — fixed: invitations route now passes inviterUserId from server-side locals.userId in query param (trusted); grain validates inviterUserId is a member
- [x] [Review][Patch] AddMember accepts arbitrary role string — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:950-961` — fixed: validates role is "Owner" or "Member"
- [x] [Review][Patch] OrgSelector ignores organizations nanostore — `frontend/src/components/sidebar/OrgSelector.astro:1189-1264` — fixed: replaced window.__orgStore with nanostore imports
- [x] [Review][Defer] Projector at-least-once delivery causes duplicate event reprocessing on Nack/Retry — `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs:245-252` — deferred, inherent to persistent subscription pattern
- [x] [Review][Defer] CreateOrg TOCTOU race during Orleans grain activation — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:914-925` — deferred, inherent to Orleans grain activation lifecycle
- [x] [Review][Defer] Stream ID check may drop events with resolveLinkTos enabled — `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs:260-261` — deferred, pre-existing design issue
- [x] [Review][Defer] OrgInvitationProjection missing unique constraint on (OrgId, Email) — `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs` — deferred, pre-existing schema gap
- [x] [Review][Defer] InviterUserId never persisted to OrgInvitationProjection — `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs` — deferred, pre-existing entity design
- [x] [Review][Defer] TOCTOU race: RenameOrg and DeleteOrg concurrent execution order — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:927-948` — deferred, grain concurrency model issue