# Task 12: Organization Management Pages

| Field | Value |
|-------|-------|
| **Developer** | Frontend |
| **Work Order** | 12 |
| **Priority** | P1 |
| **Estimated Effort** | 3 days |

## Description

Implement the organization creation flow, organization settings page (rename), and the member management pages (invite, list, remove, role change). This covers the primary organization CRUD and member management UI that organization owners need.

## Architecture Reference

- Architecture doc Section 8.3 (Create Organization sequence)
- Architecture doc Section 8.4 (Invite and Accept Member sequence)
- Architecture doc Section 9.3 (Organization Endpoints)

## Technical Requirements

### Create Organization

**Location:** Accessible from the empty state ("Create Organization" button) or a "+" button in the org selector dropdown.

**Flow:**
1. Open a modal or navigate to a creation page.
2. Form fields:
   - Organization name (required, text input, max 100 chars).
3. On submit:
   - `POST /api/organizations` with `{ name: "Acme Corp" }`.
   - On success: add new org to `organizations` store, set `currentOrgId` to the new org, navigate to `/orgs/{orgId}`.
   - On error: show toast with error message.
4. Validation: name must be non-empty (trimmed), show inline error if empty on submit.

### Organization Settings Page (`/orgs/{orgId}/settings`)

**Visible to:** Owners only. If a Member navigates here, show a "Not authorized" message.

**Content:**
- Current org name in an editable field.
- "Save" button to rename the org.
- Danger zone: "Delete Organization" button (disabled for Epic 1 -- show a "Coming soon" tooltip).

**Rename Flow:**
1. User edits the name field.
2. Clicks "Save".
3. `PATCH /api/organizations/{orgId}` with `{ name: "New Name" }`.
4. On success: update org name in `organizations` store, show success toast.
5. On error: show error toast, revert field to original name.

### Members Page (`/orgs/{orgId}/members`)

**Visible to:** All org members.

**Content:**
- Header with "Members" title and an "Invite Member" button (Owners only).
- Table/list of members with columns:
  - Avatar + Display Name
  - Role (Owner/Member)
  - Joined date
  - Actions (Owner-only: "Change Role" dropdown, "Remove" button -- not shown for self or last owner)

**Invite Member Flow (Owner-only):**
1. Click "Invite Member" button -> opens modal.
2. Modal fields:
   - Email address (required, validated format).
   - Role (dropdown: Member, Owner -- default Member).
3. On submit:
   - `POST /api/organizations/{orgId}/members/invite` with `{ email: "john@example.com", role: "Member" }`.
   - On success: show success toast "Invitation sent to john@example.com".
   - On error: show error toast (e.g., "User already invited").
4. Modal closes after success.

**Change Role Flow (Owner-only):**
1. Click role dropdown on a member row.
2. Select new role.
3. Confirmation dialog: "Change {name} from {oldRole} to {newRole}?"
4. On confirm:
   - `PATCH /api/organizations/{orgId}/members/{userId}/role` with `{ role: "Owner" }`.
   - On success: update member role in the UI, show success toast.
   - On error: show error toast, revert to old role.
5. Cannot demote the last Owner (button should be disabled with tooltip).

**Remove Member Flow (Owner-only):**
1. Click "Remove" button on a member row.
2. Confirmation dialog: "Remove {name} from this organization? They will lose access to all products."
3. On confirm:
   - `DELETE /api/organizations/{orgId}/members/{userId}`.
   - On success: remove member from the UI, show success toast.
   - On error: show error toast.
4. Cannot remove the last Owner (button disabled).

### File Structure
```
src/
  pages/
    orgs/
      [orgId]/
        settings.astro
        members.astro
  components/
    organization/
      CreateOrgModal.astro
      OrgSettingsForm.astro
      MembersTable.astro
      InviteMemberModal.astro
      ChangeRoleDropdown.astro
      RemoveMemberButton.astro
      ConfirmDialog.astro
```

## API Contracts

### POST /api/organizations
```
Request:
{
  "name": "Acme Corp"
}

Response (201):
{
  "orgId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "name": "Acme Corp"
}

Response (400):
{
  "error": "VALIDATION_ERROR",
  "message": "Organization name is required"
}
```

### PATCH /api/organizations/{orgId}
```
Request:
{
  "name": "Acme Corp Renamed"
}

Response (200):
{
  "orgId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "name": "Acme Corp Renamed"
}

Response (403):
{
  "error": "FORBIDDEN",
  "message": "Only owners can rename the organization"
}
```

### GET /api/organizations/{orgId}/members
```
Response (200):
[
  {
    "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "displayName": "Jane Developer",
    "avatarUrl": "https://avatars.githubusercontent.com/u/12345678",
    "role": "Owner",
    "joinedAt": "2026-05-05T14:30:00.000Z"
  },
  {
    "userId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "displayName": "John Smith",
    "avatarUrl": "https://avatars.githubusercontent.com/u/87654321",
    "role": "Member",
    "joinedAt": "2026-05-05T15:00:00.000Z"
  }
]
```

### POST /api/organizations/{orgId}/members/invite
```
Request:
{
  "email": "john@example.com",
  "role": "Member"
}

Response (200):
{
  "success": true,
  "message": "Invitation sent"
}

Response (400):
{
  "error": "ALREADY_INVITED",
  "message": "An invitation is already pending for this email"
}

Response (403):
{
  "error": "FORBIDDEN",
  "message": "Only owners can invite members"
}
```

### PATCH /api/organizations/{orgId}/members/{userId}/role
```
Request:
{
  "role": "Owner"
}

Response (200):
{
  "success": true
}

Response (403):
{
  "error": "FORBIDDEN",
  "message": "Only owners can change roles"
}

Response (409):
{
  "error": "LAST_OWNER",
  "message": "Cannot demote the last owner"
}
```

### DELETE /api/organizations/{orgId}/members/{userId}
```
Response (200):
{
  "success": true
}

Response (403):
{
  "error": "FORBIDDEN",
  "message": "Only owners can remove members"
}

Response (409):
{
  "error": "LAST_OWNER",
  "message": "Cannot remove the last owner"
}
```

## Acceptance Criteria

- [ ] "Create Organization" modal works and creates a new org via API.
- [ ] After creation, user is redirected to the new org dashboard.
- [ ] Settings page shows the org name and allows renaming (Owners only).
- [ ] Members page shows all members with avatar, name, role, and joined date.
- [ ] Owners can invite a new member by email.
- [ ] Owners can change a member's role (with confirmation dialog).
- [ ] Owners can remove a member (with confirmation dialog).
- [ ] Cannot remove or demote the last Owner (UI prevents this).
- [ ] Members cannot see invite/change-role/remove actions.
- [ ] Members cannot access the settings page (show "Not authorized").
- [ ] All forms validate input before submission.
- [ ] Success and error toasts are shown for all operations.
- [ ] API calls include proper error handling.

## Dependencies

- Task 09 (Astro Project Setup) -- project structure, layouts, UI components.
- Task 10 (Auth Flow) -- session management, user context.
- Task 11 (Org Selector) -- org state store, sidebar integration.

## Notes

- The BFF proxies these API calls to the actor service. The actual API call from the frontend goes to the Astro.js BFF endpoints (e.g., `/api/organizations`), which then forwards to the actor service.
- For optimistic UI: update the member list immediately after a successful mutation, then refetch to ensure consistency with the read model. This handles the eventual consistency delay.
- The "Invite Member" success message should mention that the invitee will receive an email (even if email is deferred and in-app notifications are used for MVP).
- The confirmation dialogs should use a consistent component (not `window.confirm`).
