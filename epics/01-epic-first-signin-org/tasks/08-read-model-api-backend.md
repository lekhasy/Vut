# Task 08: Co-hosted API Controllers (Silo API Endpoints)

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 08 |
| **Priority** | P1 |
| **Estimated Effort** | 2 days |

## Description

Implement ASP.NET Core API controllers co-hosted inside the Orleans silo process. These controllers provide HTTP endpoints that the Astro.js BFF calls to read user profiles, organization details, member lists, and invitation data, as well as to invoke write operations on grains. Controllers use `IGrainFactory` (injected via DI) for write operations and query PostgreSQL directly (via `Npgsql`/Dapper) for reads. There is no separate Read Model API service — all endpoints live in the `Velucid.Silo` project and share port 5000 with the silo.

## Architecture Reference

- Architecture doc Section 5.5 (Calling Grains from API Controllers)
- Architecture doc Section 11 (API Design - BFF Endpoints)
- Architecture doc Section 13 (Data Flow Summary - Read Path)
- Architecture doc Section 7.1 (Entity Relationship Diagram - projection tables used in read queries)

## Technical Requirements

### Solution Structure
```
src/
  Velucid.Silo/
    Program.cs                    # Already exists from Task 04 (Orleans silo bootstrap)
    Velucid.Silo.csproj
    Controllers/
      UsersController.cs          # Read queries + write commands via IGrainFactory
      OrganizationsController.cs  # Read queries + write commands via IGrainFactory
      InvitationsController.cs    # Read queries
      IdentitiesController.cs     # Read queries
    Queries/
      UserQueries.cs              # Dapper/raw SQL read queries
      OrgQueries.cs
    Services/
      IEmailService.cs            # Abstraction for sending emails
      ResendEmailService.cs       # Resend SDK implementation
    Models/
      UserDto.cs
      OrganizationDto.cs
      MemberDto.cs
      InvitationDto.cs
```

Controllers are registered in the existing `Velucid.Silo/Program.cs` alongside the Orleans silo configuration (from Task 04). The silo already calls `builder.Services.AddControllers()` and `app.MapControllers()`.

### Endpoints

#### Internal Lookup Endpoints (BFF-to-API, read-only)

**GET /api/users/by-provider/{providerId}**
- Looks up user by any linked Auth0 provider ID via `user_identity` table.
- Query: `SELECT ui.user_id, up.* FROM user_identity ui JOIN user_projection up ON ui.user_id = up.user_id WHERE ui.provider_id = @providerId`.
- Returns `200` with `UserDto` or `404` if not found.
```json
{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "displayName": "Jane Developer",
  "avatarUrl": "https://avatars.githubusercontent.com/u/12345678",
  "email": "jane@example.com",
  "isEmailVerified": false,
  "createdAt": "2026-05-05T14:30:00.000Z",
  "updatedAt": "2026-05-05T14:30:00.000Z"
}
```
Note: `email` may be `null` if the identity provider did not return one and the user has not yet completed email verification.

**GET /api/users/by-email/{email}**
- Looks up user by email via `user_identity` table (for auto-linking). Only usable when the identity provider returned an email — the BFF should skip this call when email is null.
- Query: `SELECT ui.user_id FROM user_identity ui WHERE ui.email = @email LIMIT 1`.
- Returns `200` with `{ userId }` or `404` if not found.

#### User Endpoints

**GET /api/users/{userId}**
- Returns user profile by userId (read from PostgreSQL).
- Returns `200` with `UserDto` or `404`.

**GET /api/users/{userId}/identities**
- Returns all linked identity providers for a user.
- Query: `SELECT provider_id, provider_name, email, linked_at FROM user_identity WHERE user_id = @userId`.
```json
[
  {
    "providerId": "github|12345678",
    "providerName": "github",
    "email": "jane@example.com",
    "linkedAt": "2026-05-05T14:30:00.000Z"
  }
]
```
Note: `email` may be `null` if the identity provider did not return one.

**GET /api/users/{userId}/organizations**
- Returns all organizations the user belongs to.
- Queries `user_org_projection` joined with `org_projection`.
```json
[
  {
    "orgId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
    "name": "Acme Corp",
    "role": "Owner",
    "isDeleted": false
  }
]
```

**POST /api/users/create**
- Creates a new user via `IGrainFactory.GetGrain<IUserGrain>(userId)`.
- Calls `grain.CreateUser(...)` — write operation routed through Orleans.
- Returns `200` with the result from the grain.

#### Organization Endpoints

**GET /api/organizations/{orgId}**
- Returns org details from `org_projection` (read from PostgreSQL).
```json
{
  "orgId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "name": "Acme Corp",
  "isDeleted": false,
  "createdAt": "2026-05-05T14:30:00.000Z",
  "updatedAt": "2026-05-05T14:30:00.000Z"
}
```

**GET /api/organizations/{orgId}/members**
- Returns all members of an organization.
- Queries `org_member_projection` joined with `user_projection`.
```json
[
  {
    "userId": "a1b2c3d4-...",
    "displayName": "Jane Developer",
    "avatarUrl": "https://...",
    "role": "Owner",
    "joinedAt": "2026-05-05T14:30:00.000Z"
  }
]
```

**POST /api/organizations**
- Creates an organization via `IGrainFactory.GetGrain<IOrganizationGrain>(orgId)`.
- Write operation routed through Orleans grain.

**POST /api/organizations/{orgId}/members/invite**
- Invites a member via the `IOrganizationGrain`.
- After the grain confirms the invitation, sends an invitation email via **Resend** (using `IEmailService`).

**POST /api/organizations/{orgId}/members/accept**
- Accepts an invitation via the `IOrganizationGrain`.

#### Invitations

**GET /api/invitations?email={email}**
- Returns all pending invitations for the given email.
- Queries `org_invitation_projection` joined with `org_projection`.
- Filter: `status = 'Pending'`.
```json
[
  {
    "orgId": "f7e6d5c4-...",
    "orgName": "Acme Corp",
    "email": "john@example.com",
    "role": "Member",
    "status": "Pending",
    "invitedAt": "2026-05-05T14:30:00.000Z"
  }
]
```

**GET /api/organizations/{orgId}/invitations**
- Returns all invitations for an organization (all statuses).
- Owner-only (authorization is handled by the BFF, but the API should also check).

### Database Access (Reads)
- Use `Npgsql` with Dapper (lightweight micro-ORM) or raw SQL for all read queries.
- Connection string from configuration (same PostgreSQL instance used by Orleans clustering).
- Read queries go directly to PostgreSQL — they do NOT pass through Orleans grains.

### Grain Access (Writes)
- Inject `IGrainFactory` via constructor DI into controllers.
- Obtain grain references via `_grainFactory.GetGrain<IUserGrain>(userId)` or `_grainFactory.GetGrain<IOrganizationGrain>(orgId)`.
- Orleans handles grain activation, placement, and routing transparently.

### Email Sending (Resend)
- Inject `IEmailService` (backed by `ResendEmailService`) into controllers that send emails.
- **Email verification** (`POST /api/users/{userId}/verify-email`): After the UserGrain returns the 6-digit verification code, the controller sends the code to the user's email via Resend.
- **Member invitation** (`POST /api/organizations/{orgId}/members/invite`): After the OrgGrain confirms the invitation, the controller sends an invitation email via Resend with a link to `https://velucid.app/invite/{orgId}`.
- `ResendEmailService` uses the `Resend` .NET SDK with the API key from configuration (`Resend:ApiKey`).
- Wrap Resend calls in try/catch — email delivery failure should be logged but should NOT fail the API request (the grain event is already persisted).

### Cross-Origin (CORS)
- Allow requests from the Astro.js BFF origin.
- The API is internal to the cluster but should support CORS for local dev.

### Hosting
- Co-hosted in the `Velucid.Silo` process — no separate Dockerfile or service.
- Shares port 5000 with the Orleans silo HTTP endpoint.
- The silo `Program.cs` (from Task 04) already configures `WebApplication` with Orleans and ASP.NET Core controllers.

## Acceptance Criteria

- [ ] All read endpoints return correct data from PostgreSQL projections.
- [ ] Write endpoints (create user, create org, invite member, accept invitation) correctly invoke grains via `IGrainFactory`.
- [ ] `GET /api/users/by-provider/{providerId}` returns `404` for unknown users.
- [ ] `GET /api/users/by-email/{email}` returns `404` for unknown emails.
- [ ] `GET /api/users/{userId}/identities` returns all linked providers for a user.
- [ ] `GET /api/users/{userId}/organizations` returns the correct list of org memberships.
- [ ] `GET /api/organizations/{orgId}/members` returns members with their display names and roles.
- [ ] `GET /api/invitations?email=...` returns only pending invitations for that email.
- [ ] All responses follow consistent JSON format (camelCase).
- [ ] Error responses follow a consistent format: `{ "error": "NOT_FOUND", "message": "..." }`.
- [ ] Controllers are co-hosted in the silo process and accessible on port 5000.
- [ ] Email verification endpoint sends verification code via Resend after grain call succeeds.
- [ ] Member invitation endpoint sends invitation email via Resend after grain call succeeds.
- [ ] Resend email delivery failures are logged but do not fail the API request.

## Dependencies

- Task 03 (PostgreSQL Schema) -- projection tables must exist.
- Task 04 (Orleans Silo Foundation) -- silo `Program.cs` with `UseOrleans()`, `AddControllers()`, and `MapControllers()` must be in place. `IGrainFactory` is available via DI.
- Task 05 (User Grain) -- `IUserGrain` interface must exist for write operations.
- Task 06 (Organization Grain) -- `IOrganizationGrain` interface must exist for write operations.
- Task 07 (Projector Service) -- projections must be populated for integration testing. However, unit/integration tests can seed test data directly.

## Notes

- **No separate service or Dockerfile.** Controllers live inside `Velucid.Silo` and are co-hosted with Orleans grains in the same process. See architecture Section 3 (Component Diagram) and Section 5.5.
- Authorization checks (org membership, role verification) should be performed by the BFF before calling the API. The API trusts the BFF as the caller.
- In a production setup, this API is internal (not exposed to the internet). The Astro.js BFF proxies requests to port 5000 on the silo service.
- Pagination will be needed for member lists and invitations in the future, but is not required for Epic 1 (organizations will have small teams).
- Write operations go through grains (ensuring event sourcing and single-threaded consistency). Read operations bypass grains and go directly to PostgreSQL projections (ensuring fast, scalable reads).
