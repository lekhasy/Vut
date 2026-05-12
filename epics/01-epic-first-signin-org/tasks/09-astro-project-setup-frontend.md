# Task 09: Astro.js Project Setup & UI Shell

| Field | Value |
|-------|-------|
| **Developer** | Frontend |
| **Work Order** | 09 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 2 days |

## Description

Initialize the Astro.js project with Tailwind CSS, establish the application shell (layout, sidebar, routing), and set up the client-side state management store. This task creates the foundational frontend structure that all other frontend tasks build upon.

## Architecture Reference

- Architecture doc Section 10 (Frontend Architecture)
- Architecture doc Section 10.1 (Astro.js SPA Shell)
- Architecture doc Section 10.2 (Client-Side State)
- PRD Section 7.2 (Platform - SPA with client-side routing)
- PRD Section 7.3 (Key UI Patterns - sidebar navigation)

## Technical Requirements

### Project Initialization
```bash
npm create astro@latest -- --template minimal
npm install tailwindcss @astrojs/tailwind
```

### Project Structure
```
frontend/
  astro.config.mjs
  tailwind.config.mjs
  tsconfig.json
  package.json
  public/
    favicon.svg
  src/
    layouts/
      AppLayout.astro       # Main layout with sidebar
      AuthLayout.astro      # Layout for login/callback pages (no sidebar)
    pages/
      index.astro           # Landing page
      dashboard.astro       # Dashboard (empty state or org view)
      auth/
        login.astro         # Login redirect page
        callback.astro      # Auth0 callback handler
      orgs/
        [orgId]/
          index.astro       # Org dashboard
          settings.astro    # Org settings
          members.astro     # Member management
    components/
      sidebar/
        Sidebar.astro       # Main sidebar
        OrgSelector.astro   # Org dropdown in sidebar
        NavItem.astro       # Navigation item
      ui/
        Button.astro
        Input.astro
        Modal.astro
        Toast.astro
        Avatar.astro
        EmptyState.astro
        Dropdown.astro
    stores/
      auth.ts               # Auth state (currentUser, isAuthenticated)
      organizations.ts      # Organizations list, currentOrgId
      invitations.ts        # Pending invitations
      types.ts              # TypeScript interfaces
    lib/
      api.ts                # API client (fetch wrapper)
      auth.ts               # Auth helpers (session, login, logout)
      router.ts             # Client-side routing utilities
    styles/
      global.css            # Tailwind base + custom styles
```

### Client-Side State Store

Use nanostores (Astro-recommended, lightweight):

```typescript
// stores/types.ts
export interface User {
  userId: string;
  providerId: string;
  displayName: string;
  avatarUrl: string;
}

export interface Organization {
  orgId: string;
  name: string;
  role: 'Owner' | 'Member';
  isDeleted: boolean;
}

export interface Member {
  userId: string;
  displayName: string;
  avatarUrl: string;
  role: 'Owner' | 'Member';
  joinedAt: string;
}

export interface Invitation {
  orgId: string;
  orgName: string;
  email: string;
  role: 'Owner' | 'Member';
  status: 'Pending' | 'Accepted' | 'Declined';
  invitedAt: string;
}
```

```typescript
// stores/auth.ts
import { atom } from 'nanostores';
import type { User } from './types';

export const currentUser = atom<User | null>(null);
export const isAuthenticated = atom<boolean>(false);
```

```typescript
// stores/organizations.ts
import { atom } from 'nanostores';
import type { Organization } from './types';

export const organizations = atom<Organization[]>([]);
export const currentOrgId = atom<string | null>(null);
```

```typescript
// stores/invitations.ts
import { atom } from 'nanostores';
import type { Invitation } from './types';

export const pendingInvitations = atom<Invitation[]>([]);
```

### API Client (`lib/api.ts`)
```typescript
const API_BASE = import.meta.env.API_BASE_URL || '';

export async function apiFetch<T>(
  path: string,
  options?: RequestInit
): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    credentials: 'include', // send session cookie
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    ...options,
  });

  if (response.status === 401) {
    window.location.href = '/auth/login';
    throw new Error('Unauthorized');
  }

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Unknown error' }));
    throw new Error(error.message || `HTTP ${response.status}`);
  }

  return response.json();
}
```

### AppLayout
- Full-height layout with fixed sidebar (left, 240px) and main content area.
- Sidebar includes:
  - Logo/brand at top.
  - `OrgSelector` dropdown below logo.
  - Navigation items: Dashboard, Members, Settings (only for Owners).
  - User avatar and name at bottom, with logout button.
- Main content area has a top bar with the current page title.
- Responsive: sidebar collapses on mobile (hamburger menu).

### AuthLayout
- Centered layout for login and callback pages.
- No sidebar. Minimal chrome.
- Dark or light background with Velucid logo.

### Routing
- Client-side routing using the History API (or a lightweight router like `nanostores/router`).
- Pages are Astro pages that hydrate on navigation.
- Auth guard: if `isAuthenticated` is false, redirect to `/auth/login`.

### Tailwind Configuration
- Dark mode support (class-based, default to dark).
- Custom color palette for Velucid brand.
- Extend with Inter or similar clean sans-serif font.

## API Contracts (for mock data)

The frontend should develop against these API contracts. Mock data should match these shapes exactly.

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
  }
]
```

## Acceptance Criteria

- [ ] Astro project initializes and runs with `npm run dev`.
- [ ] Tailwind CSS is applied globally.
- [ ] AppLayout renders sidebar + main content area.
- [ ] Sidebar shows placeholder org selector and navigation items.
- [ ] Client-side routing works between pages.
- [ ] Auth guard redirects unauthenticated users to `/auth/login`.
- [ ] State stores (`currentUser`, `organizations`, `currentOrgId`, `pendingInvitations`) are set up with nanostores.
- [ ] API client (`apiFetch`) is implemented with credential handling.
- [ ] Responsive design: sidebar collapses on mobile.

## Dependencies

- None. Can start immediately in parallel with all backend tasks.

## Notes

- Use Astro's island architecture -- keep client-side JavaScript minimal. Only components that need interactivity should hydrate.
- The `AppLayout` should use `<slot />` for page content, not full-page client-side rendering.
- Consider using `@astrojs/react` or `@astrojs/preact` for interactive islands (OrgSelector, modals) if pure Astro components are insufficient.
- The component library is TBD per the PRD open questions. For Epic 1, build minimal custom components (Button, Input, Modal, Toast, Avatar, EmptyState). These can be replaced later.
- Do NOT set up Storybook or component documentation in Epic 1.
