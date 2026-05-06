# Task 08: Read Model API Service

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 08 |
| **Priority** | P1 |
| **Estimated Effort** | 2 days |

## Description

Implement the Read Model API -- a .NET web service that provides read-only HTTP endpoints querying the PostgreSQL read model. The Astro.js BFF calls this service to fetch user profiles, organization details, member lists, and invitation data. This service has no write capabilities; it only reads from projection tables.

## Architecture Reference

- Architecture doc Section 9 (API Design - BFF Endpoints)
- Architecture doc Section 7.1 (Projection Views)
- Architecture doc Section 11 (Data Flow Summary - Read Path)

## Technical Requirements

### Solution Structure
```
src/
  Vut.ReadModelApi/
    Program.cs
    Vut.ReadModelApi.csproj
    Configuration/
      PostgresOptions.cs
    Controllers/
      UsersController.cs
      OrganizationsController.cs
      InvitationsController.cs
    Queries/
      UserQueries.cs
      OrgQueries.cs
    Models/
      UserDto.cs
      OrganizationDto.cs
      MemberDto.cs
      InvitationDto.cs
```

### Endpoints

#### Users

**GET /api/users/by-provider/{providerId}**
- Looks up user by Auth0 provider ID (e.g., `github|12345678`).
- Returns `200` with `UserDto` or `404` if not found.
```json
{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "providerId": "github|12345678",
  "displayName": "Jane Developer",
  "avatarUrl": "https://avatars.githubusercontent.com/u/12345678",
  "createdAt": "2026-05-05T14:30:00.000Z",
  "updatedAt": "2026-05-05T14:30:00.000Z"
}
```

**GET /api/users/{userId}**
- Returns user profile by userId.
- Returns `200` with `UserDto` or `404`.

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

#### Organizations

**GET /api/organizations/{orgId}**
- Returns org details from `org_projection`.
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

### Database Access
- Use `Npgsql` with Dapper (lightweight micro-ORM) or raw SQL.
- Connection string from configuration.
- All queries are read-only -- no writes.

### Cross-Origin (CORS)
- Allow requests from the Astro.js BFF origin.
- This service is internal to the cluster but should support CORS for local dev.

### Dockerfile
- Multi-stage build. Expose port 5001.
- Output image: `vut/readmodel-api`.

## Acceptance Criteria

- [ ] All endpoints return correct data from PostgreSQL projections.
- [ ] `GET /api/users/by-provider/{providerId}` returns `404` for unknown users.
- [ ] `GET /api/users/{userId}/organizations` returns the correct list of org memberships.
- [ ] `GET /api/organizations/{orgId}/members` returns members with their display names and roles.
- [ ] `GET /api/invitations?email=...` returns only pending invitations for that email.
- [ ] All responses follow consistent JSON format (camelCase).
- [ ] Error responses follow a consistent format: `{ "error": "NOT_FOUND", "message": "..." }`.
- [ ] Dockerfile builds successfully.

## Dependencies

- Task 03 (PostgreSQL Schema) -- tables must exist.
- Task 07 (Projector Service) -- projections must be populated for integration testing. However, unit/integration tests can seed test data directly.

## Notes

- This service is intentionally simple -- it is a thin read layer over PostgreSQL projections.
- Authorization checks (org membership, role verification) should be performed by the BFF before calling the Read Model API. The Read Model API trusts the BFF as the caller.
- In a production setup, this service would be internal (not exposed to the internet). The Astro.js BFF proxies requests to it.
- Pagination will be needed for member lists and invitations in the future, but is not required for Epic 1 (organizations will have small teams).
