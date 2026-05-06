# Task 13: BFF API Route Proxies

| Field | Value |
|-------|-------|
| **Developer** | Frontend |
| **Work Order** | 13 |
| **Priority** | P1 |
| **Estimated Effort** | 2 days |

## Description

Implement the Astro.js BFF server-side API routes that proxy frontend requests to the backend services (Actor Service and Read Model API). These routes handle session validation, authorization checks (org membership, role verification), and command construction. The BFF is the single entry point for all frontend data requests.

## Architecture Reference

- Architecture doc Section 9 (API Design - BFF Endpoints)
- Architecture doc Section 10.3 (Authorization Model - Frontend)
- Architecture doc Section 11 (Data Flow Summary)

## Technical Requirements

### Architecture Pattern
Each BFF API route is an Astro API endpoint (`src/pages/api/*.ts`) that:
1. Validates the user session (from middleware -- `Astro.locals`).
2. Performs authorization checks (org membership, role).
3. Constructs the command or query for the backend.
4. Calls the Actor Service (for writes) or Read Model API (for reads).
5. Returns the response to the frontend.

### User Endpoints

#### GET /api/users/me
- Read session data from `Astro.locals` (already populated by middleware).
- Optionally refetch from read model for fresh data.
```typescript
// src/pages/api/users/me.ts
export async function GET({ locals }: APIRoute) {
  return new Response(JSON.stringify({
    userId: locals.userId,
    providerId: locals.providerId,
    displayName: locals.displayName,
    avatarUrl: locals.avatarUrl,
  }), { status: 200 });
}
```

#### PATCH /api/users/me
- Body: `{ displayName?: string, avatarUrl?: string }`.
- Forward to actor service: `CreateUpdateProfileCommand(userId, displayName, avatarUrl)`.
```
POST http://{ACTOR_SERVICE_URL}/commands
{
  "commandType": "UpdateProfile",
  "payload": { "userId": "...", "displayName": "...", "avatarUrl": "..." },
  "actorId": "{userId}"
}
```

### Organization Endpoints

#### POST /api/organizations
- Body: `{ name: string }`.
- Validate: name is non-empty.
- Forward to actor service:
```json
{
  "commandType": "CreateOrganization",
  "payload": { "name": "Acme Corp", "ownerId": "{userId}" },
  "actorId": "{userId}"
}
```
- Return: `{ orgId, name }` with 201 status.

#### GET /api/organizations
- Forward to read model: `GET http://{READMODEL_URL}/api/users/{userId}/organizations`.
- Return the list of organizations.

#### GET /api/organizations/{orgId}
- Validate: user belongs to this org (check against user's org list from read model).
- Forward to read model: `GET http://{READMODEL_URL}/api/organizations/{orgId}`.
- Return org details.

#### PATCH /api/organizations/{orgId}
- Validate: user is Owner of this org.
- Body: `{ name: string }`.
- Forward to actor service:
```json
{
  "commandType": "RenameOrganization",
  "payload": { "orgId": "...", "newName": "..." },
  "actorId": "{userId}"
}
```

#### GET /api/organizations/{orgId}/members
- Validate: user belongs to this org.
- Forward to read model: `GET http://{READMODEL_URL}/api/organizations/{orgId}/members`.

#### POST /api/organizations/{orgId}/members/invite
- Validate: user is Owner of this org.
- Body: `{ email: string, role: string }`.
- Forward to actor service:
```json
{
  "commandType": "InviteMember",
  "payload": { "orgId": "...", "inviteeEmail": "...", "role": "Member" },
  "actorId": "{userId}"
}
```

#### POST /api/organizations/{orgId}/members/accept
- Validate: user belongs to this org's pending invitations.
- Body: `{ email: string }`.
- Forward to actor service:
```json
{
  "commandType": "AcceptInvitation",
  "payload": { "orgId": "...", "userId": "{userId}", "email": "..." },
  "actorId": "{userId}"
}
```

#### POST /api/organizations/{orgId}/members/decline
- Same as accept but with `DeclineInvitation` command type.

#### DELETE /api/organizations/{orgId}/members/{targetUserId}
- Validate: user is Owner of this org.
- Validate: target is not the last Owner.
- Forward to actor service:
```json
{
  "commandType": "RemoveMember",
  "payload": { "orgId": "...", "userId": "{targetUserId}" },
  "actorId": "{userId}"
}
```

#### PATCH /api/organizations/{orgId}/members/{targetUserId}/role
- Validate: user is Owner of this org.
- Body: `{ role: string }`.
- Forward to actor service:
```json
{
  "commandType": "ChangeMemberRole",
  "payload": { "orgId": "...", "userId": "{targetUserId}", "newRole": "Owner" },
  "actorId": "{userId}"
}
```

### Invitation Endpoints

#### GET /api/invitations
- Get user's email from session (or read model).
- Forward to read model: `GET http://{READMODEL_URL}/api/invitations?email={email}`.
- Return list of pending invitations.

### Authorization Helper
Create a shared utility for org membership and role checks:

```typescript
// src/lib/authz.ts
export async function checkOrgMembership(
  userId: string,
  orgId: string
): Promise<{ isMember: boolean; role: string | null }> {
  const response = await fetch(`${READMODEL_URL}/api/users/${userId}/organizations`);
  const orgs = await response.json();
  const org = orgs.find((o: any) => o.orgId === orgId);
  return { isMember: !!org, role: org?.role ?? null };
}

export function requireOwner(role: string | null): boolean {
  return role === 'Owner';
}
```

### File Structure
```
src/
  pages/
    api/
      users/
        me.ts          # GET, PATCH
      organizations/
        index.ts       # POST, GET
        [orgId]/
          index.ts     # GET, PATCH
          members/
            index.ts           # GET
            invite.ts          # POST
            accept.ts          # POST
            decline.ts         # POST
            [userId]/
              index.ts         # DELETE
              role.ts          # PATCH
      invitations/
        index.ts       # GET
  lib/
    authz.ts          # Authorization helpers
    backend.ts        # Actor service and read model API clients
```

## API Contracts

### BFF -> Actor Service (gRPC Command)
```
POST http://{ACTOR_SERVICE_URL}/commands
Content-Type: application/json

{
  "commandType": "CreateOrganization",
  "payload": "{\"name\":\"Acme Corp\",\"ownerId\":\"a1b2c3d4-...\"}",
  "actorId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}

Response (200):
{
  "success": true,
  "payload": "{\"orgId\":\"f7e6d5c4-...\",\"name\":\"Acme Corp\"}",
  "error": null
}

Response (error):
{
  "success": false,
  "payload": null,
  "error": "NOT_OWNER"
}
```

### BFF -> Read Model API (HTTP)
```
GET http://{READMODEL_URL}/api/users/{userId}/organizations

Response (200):
[
  {
    "orgId": "f7e6d5c4-...",
    "name": "Acme Corp",
    "role": "Owner",
    "isDeleted": false
  }
]
```

## Acceptance Criteria

- [ ] All 14 API endpoints are implemented as Astro server routes.
- [ ] Each endpoint validates the user session before processing.
- [ ] Write endpoints (POST, PATCH, DELETE) forward commands to the actor service.
- [ ] Read endpoints (GET) forward queries to the read model API.
- [ ] Authorization checks prevent non-owners from owner-only actions.
- [ ] Authorization checks prevent non-members from accessing org data.
- [ ] Error responses from the backend are properly forwarded to the frontend with correct HTTP status codes.
- [ ] The `actorId` is always set to the authenticated user's `userId`.
- [ ] Consistent error response format: `{ "error": "CODE", "message": "..." }`.

## Dependencies

- Task 09 (Astro Project Setup) -- project structure.
- Task 10 (Auth Flow) -- session middleware, `Astro.locals`.

## Notes

- The BFF is the security boundary. The frontend trusts the BFF, and the BFF enforces authorization. The backend services trust the BFF as a trusted caller.
- Org membership checks should be cached per-request (not per-session) to avoid multiple lookups. A simple in-request cache (Map) works for Astro server routes.
- The actor service command format may evolve. Use a generic command proxy pattern rather than hardcoding each command type.
- Rate limiting should be added at the BFF level (e.g., limit invite requests to 10/minute per user). This can be deferred to post-Epic 1.
