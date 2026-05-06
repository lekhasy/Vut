# Task 11: Organization Selector & Sidebar Navigation

| Field | Value |
|-------|-------|
| **Developer** | Frontend |
| **Work Order** | 11 |
| **Priority** | P1 |
| **Estimated Effort** | 2 days |

## Description

Implement the organization selector dropdown in the sidebar and the dynamic sidebar navigation that adapts based on the currently selected organization. This includes fetching the user's organizations from the read model, rendering the dropdown, persisting the selected org in the state store, and updating navigation items based on the user's role.

## Architecture Reference

- Architecture doc Section 10.1 (Astro.js SPA Shell - Org Selector)
- Architecture doc Section 10.3 (Authorization Model - Frontend)
- Architecture doc Section 8.5 (Organization Switching sequence)
- PRD Section 7.3 (Sidebar navigation, organization selector)

## Technical Requirements

### OrgSelector Component
- Dropdown in the sidebar header (below the logo).
- Shows the current organization name and a chevron icon.
- On click/tap: opens a dropdown list of all organizations the user belongs to.
- Each org item shows: org name and role badge (Owner/Member).
- Selecting an org updates `currentOrgId` in the store and navigates to `/orgs/{orgId}`.
- If the user has no organizations, show a "Create Organization" button in the dropdown.

### Sidebar Navigation
Navigation items depend on whether an org is selected:

**No org selected (or first visit):**
- Dashboard (shows empty state)

**Org selected:**
- Dashboard (org-level)
- Members
- Settings (Owner-only -- hidden for Members)

### Data Fetching
On initial page load (or when `isAuthenticated` becomes true):
1. `GET /api/users/me` -> populate `currentUser` store.
2. `GET /api/users/{userId}/organizations` -> populate `organizations` store.
3. If `organizations` is not empty, set `currentOrgId` to the first org's ID.
4. If `organizations` is empty, keep `currentOrgId` as null and show empty state.

### Empty State
When `currentOrgId` is null:
- Sidebar shows "No organization" in the selector.
- Main content area shows an empty state component:
  - Illustration or icon.
  - Text: "Welcome to Vut! Create your first organization to get started."
  - Button: "Create Organization" (links to creation flow).

### Organization Switching
When the user selects a different org:
1. Update `currentOrgId` in the store.
2. Navigate to `/orgs/{newOrgId}`.
3. Refetch org-specific data (members, etc.) for the new org.

### File Structure
```
src/
  components/
    sidebar/
      Sidebar.astro
      OrgSelector.astro
      OrgSelectorItem.astro
      NavItem.astro
      SidebarUser.astro
    empty-state/
      NoOrganization.astro
```

## API Contracts

### GET /api/users/me
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

### GET /api/users/{userId}/organizations
```json
[
  {
    "orgId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
    "name": "Acme Corp",
    "role": "Owner",
    "isDeleted": false
  },
  {
    "orgId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "name": "Beta LLC",
    "role": "Member",
    "isDeleted": false
  }
]
```

### GET /api/organizations/{orgId}
```json
{
  "orgId": "f7e6d5c4-b3a2-1098-7654-321fedcba098",
  "name": "Acme Corp",
  "isDeleted": false,
  "createdAt": "2026-05-05T14:30:00.000Z",
  "updatedAt": "2026-05-05T14:30:00.000Z"
}
```

## Acceptance Criteria

- [ ] OrgSelector dropdown renders with the user's organizations.
- [ ] Selecting an org updates the sidebar navigation and navigates to the org dashboard.
- [ ] The current org is highlighted in the dropdown.
- [ ] When user has no orgs, the empty state is shown with a "Create Organization" button.
- [ ] Navigation items update based on the selected org.
- [ ] "Settings" nav item is only visible to Owners.
- [ ] Org data is fetched on page load and stored in the client-side state.
- [ ] Switching orgs triggers a refetch of org-specific data.
- [ ] The sidebar user section shows the user's avatar and display name.
- [ ] The sidebar is responsive (collapses on mobile with hamburger toggle).

## Dependencies

- Task 09 (Astro Project Setup) -- project structure, layouts, state stores.
- Task 10 (Auth Flow) -- session management, `Astro.locals` with userId.

## Notes

- The `currentOrgId` should be persisted in `localStorage` so it survives page reloads. Default to the first org if the stored ID is no longer valid.
- The org selector should handle the case where the user's org membership changes (e.g., they are removed from an org). If the `currentOrgId` is no longer in the user's org list, reset to the first available org.
- Consider adding a "pending invitations" badge count next to the org selector if the user has pending invites. This is a nice-to-have for Epic 1.
- For the role badge, use subtle styling: "Owner" in a small badge with a distinct color, "Member" in a muted style.
