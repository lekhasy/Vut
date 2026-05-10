# Task 14: Invitation Acceptance Flow

| Field | Value |
|-------|-------|
| **Developer** | Frontend |
| **Work Order** | 14 |
| **Priority** | P2 |
| **Estimated Effort** | 1.5 days |

## Description

Implement the invitation acceptance/decline UI for invited users. After signing in, users with pending invitations should see them prominently and be able to accept or decline. This task covers the invitation list view, the accept/decline actions, and the post-acceptance redirect.

## Architecture Reference

- Architecture doc Section 8.4 (Invite and Accept Member sequence)
- Architecture doc Section 9.4 (Invitation Endpoints)
- PRD Section 6.6 (Inviting a Team Member user flow)

## Technical Requirements

### Invitation Banner/Notification
When a user signs in and has pending invitations:
- Show a prominent banner or notification at the top of the dashboard.
- Text: "You have {N} pending invitation(s)."
- Clicking the banner navigates to the invitations view.

### Invitations Page / Section

**Location:** Could be a dedicated page (`/invitations`) or a modal/panel in the dashboard. Recommend a section within the dashboard for Epic 1.

**Content:**
- List of pending invitations, each showing:
  - Organization name.
  - Invited role (Owner or Member).
  - Invited date.
  - "Accept" button (green/primary).
  - "Decline" button (red/secondary).

**Accept Flow:**
1. User clicks "Accept" on an invitation.
2. `POST /api/organizations/{orgId}/members/accept` with `{ email: "user@example.com" }`.
3. On success:
   - Add the org to the `organizations` store.
   - Remove the invitation from `pendingInvitations` store.
   - Show success toast: "You've joined {orgName}!".
   - Optionally navigate to the new org dashboard.
4. On error: show error toast.

**Decline Flow:**
1. User clicks "Decline" on an invitation.
2. Confirmation dialog: "Decline invitation to {orgName}?"
3. `POST /api/organizations/{orgId}/members/decline` with `{ email: "user@example.com" }`.
4. On success:
   - Remove the invitation from `pendingInvitations` store.
   - Show info toast: "Invitation declined."
5. On error: show error toast.

### Email Link Handling (MVP)
For MVP, invitations can be discovered after login through the in-app notification system. The email link can simply redirect to the Vut login page. After login, the user sees their pending invitations.

If email contains a direct invite link (e.g., `/invite/{orgId}`):
1. If user is not logged in: redirect to `/auth/login?returnTo=/invite/{orgId}`.
2. After login: redirect to `/invite/{orgId}` which shows the specific invitation with Accept/Decline.
3. If the invitation doesn't exist or is not for this user: show error.

### Data Fetching
On login/session restoration:
1. Fetch pending invitations: `GET /api/invitations`.
2. Populate `pendingInvitations` store.
3. Show notification badge if count > 0.

### File Structure
```
src/
  pages/
    invitations.astro         # Dedicated invitations page (optional)
    invite/
      [orgId].astro           # Direct invite link handler
  components/
    invitations/
      InvitationBanner.astro
      InvitationList.astro
      InvitationCard.astro
```

## API Contracts

### GET /api/invitations
```json
[
  {
    "orgId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
    "orgName": "Acme Corp",
    "email": "john@example.com",
    "role": "Member",
    "status": "Pending",
    "invitedAt": "2026-05-05T14:30:00.000Z"
  }
]
```

### POST /api/organizations/{orgId}/members/accept
```
Request:
{
  "email": "john@example.com"
}

Response (200):
{
  "success": true,
  "message": "You have joined the organization"
}

Response (404):
{
  "error": "INVITATION_NOT_FOUND",
  "message": "No pending invitation found"
}
```

### POST /api/organizations/{orgId}/members/decline
```
Request:
{
  "email": "john@example.com"
}

Response (200):
{
  "success": true,
  "message": "Invitation declined"
}
```

## Acceptance Criteria

- [ ] Users with pending invitations see a notification after logging in.
- [ ] Invitation list shows org name, role, and invited date.
- [ ] "Accept" adds the user to the org and removes the invitation.
- [ ] "Decline" removes the invitation (with confirmation dialog).
- [ ] After accepting, the new org appears in the org selector.
- [ ] After accepting, the invitation count updates.
- [ ] Direct invite link (`/invite/{orgId}`) works for logged-in users.
- [ ] Direct invite link redirects to login for unauthenticated users.
- [ ] Invalid or expired invitations show an appropriate error message.
- [ ] Toast notifications are shown for all outcomes.

## Dependencies

- Task 09 (Astro Project Setup) -- project structure, state stores.
- Task 10 (Auth Flow) -- session management, login flow.
- Task 11 (Org Selector) -- organizations store updates after acceptance.
- Task 13 (BFF API Routes) -- invitation API endpoints.

## Notes

- The backend sends invitation emails via **Resend**. The invitee also discovers their invitation in-app after logging in (the invitation banner). Email delivery via Resend is the primary notification channel.
- The invitation banner should not be intrusive -- a subtle notification badge or toast is sufficient. Do not block the user from their dashboard.
- Consider the case where a user has pending invitations from multiple organizations. The list should handle this gracefully.
- The `email` field in the accept/decline request comes from the user's session (their verified email from Auth0). The BFF should inject this automatically rather than requiring the frontend to send it.
