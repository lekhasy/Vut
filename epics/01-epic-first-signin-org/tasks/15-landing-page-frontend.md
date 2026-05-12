# Task 15: Landing Page & Dashboard Empty State

| Field | Value |
|-------|-------|
| **Developer** | Frontend |
| **Work Order** | 15 |
| **Priority** | P1 |
| **Estimated Effort** | 1.5 days |

## Description

Build the public-facing landing page (marketing page) and the authenticated dashboard with its empty state. The landing page is the first thing new visitors see and includes the "Sign in with GitHub" CTA. The dashboard is the first page after login and shows either the user's org dashboard or an empty state prompting org creation.

## Architecture Reference

- Architecture doc Section 10.1 (Astro.js SPA Shell - Pages: Landing, Dashboard, Verify Email)
- Architecture doc Section 8.2 (Email Verification sequence)
- PRD Section 6.1 (First-Time User Onboarding flow)
- PRD Section 7.1 (Design Philosophy - fast and minimal)

## Technical Requirements

### Landing Page (`/`)

**Public page** (no auth required).

**Layout:** AuthLayout (centered, no sidebar).

**Content:**
- Hero section:
  - Velucid logo and tagline: "#NoEstimate project management. Track flow, not time."
  - "Sign in with GitHub" button (primary CTA).
  - Brief value proposition: 2-3 bullet points about what Velucid does differently.
- Features section (brief):
  - Kanban board (no sprints, no velocity).
  - Probabilistic forecast (know when work will be done — with honest probabilities, not single dates).
  - Tags (flexible categorization without estimates).
- Footer: minimal, copyright + link to GitHub repo.

**"Sign in with GitHub" button:**
- Links to `/auth/login`.
- Shows the GitHub icon.
- Styled as a prominent primary button.

### Dashboard (`/dashboard`)

**Protected page** (auth required, email verified required).

**Layout:** AppLayout (with sidebar).

**Logic:**
1. Fetch user's organizations.
2. If user has organizations:
   - Redirect to `/orgs/{firstOrgId}` (or `currentOrgId` if stored).
3. If user has NO organizations:
   - Show the empty state.
   - Check for pending invitations and show invitation banner if any.

### Email Verification Page (`/verify-email`)

**Protected page** (auth required, email NOT verified — this is the page unverified users see).

**Layout:** AuthLayout (centered, no sidebar).

**Content:**
- Heading: "Verify your email"
- Subheading: "Enter your email address to receive a verification code."
- Email input field (pre-filled if email was provided by OAuth provider).
- "Send Code" button -> calls `POST /api/users/me/verify-email`.
- After sending:
  - Show 6-digit code input.
  - "Verify" button -> calls `POST /api/users/me/verify-email/confirm`.
  - "Resend code" link (resends after cooldown).
- On success: redirect to `/dashboard`.

### Empty State (no organizations)

**Content:**
- Large illustration or icon (e.g., a building/org icon).
- Heading: "Welcome to Velucid!"
- Subheading: "Create your first organization to start managing your projects."
- Primary button: "Create Organization" -> opens `CreateOrgModal`.
- Secondary link: "Or check your pending invitations" (only if invitations exist).

### Dashboard with Organization (`/orgs/{orgId}`)

**Content:**
- Top section: Org name and quick stats placeholder (e.g., "0 products" for Epic 1).
- Main content: Placeholder for product list (empty in Epic 1).
  - Text: "Products will appear here. Create your first product to start tracking work."
  - "Create Product" button (disabled for Epic 1 -- shows "Coming soon" tooltip).
- Sidebar shows the org is selected.

### File Structure
```
src/
  pages/
    index.astro           # Landing page
    dashboard.astro       # Dashboard (redirects or shows empty state)
    verify-email.astro    # Email verification page
    orgs/
      [orgId]/
        index.astro       # Org dashboard
  components/
    landing/
      Hero.astro
      FeatureCard.astro
      SignInButton.astro
    dashboard/
      EmptyState.astro
      OrgDashboard.astro
      QuickStats.astro
    verify-email/
      EmailForm.astro
      CodeInput.astro
```

## API Contracts

### GET /api/users/me (session check)
```json
{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "providerId": "github|12345678",
  "displayName": "Jane Developer",
  "avatarUrl": "https://avatars.githubusercontent.com/u/12345678",
  "isEmailVerified": false,
  "createdAt": "2026-05-05T14:30:00.000Z",
  "updatedAt": "2026-05-05T14:30:00.000Z"
}
```

### POST /api/users/me/verify-email
```json
Request: { "email": "jane@example.com" }
Response: { "success": true }
```

### POST /api/users/me/verify-email/confirm
```json
Request: { "code": "123456" }
Response: { "success": true }
```

### GET /api/users/{userId}/organizations
```json
[]
```
(Empty array for first-time user)

### GET /api/invitations
```json
[]
```
(Empty for users with no invitations, or populated per Task 14 contract)

## Acceptance Criteria

- [ ] Landing page renders at `/` without authentication.
- [ ] "Sign in with GitHub" button redirects to `/auth/login`.
- [ ] Landing page has a clear value proposition about #noestimate.
- [ ] Email verification page renders at `/verify-email` for authenticated but unverified users.
- [ ] Email verification sends a code and verifies it, then redirects to `/dashboard`.
- [ ] Dashboard redirects to org view if the user has organizations.
- [ ] Dashboard shows empty state if the user has no organizations.
- [ ] Empty state includes "Create Organization" button that opens the creation modal.
- [ ] Empty state shows invitation notification if the user has pending invitations.
- [ ] Org dashboard page loads at `/orgs/{orgId}` and shows the org name.
- [ ] Org dashboard shows placeholder for products (empty state for Epic 1).
- [ ] Pages load in under 1 second on a normal connection.

## Dependencies

- Task 09 (Astro Project Setup) -- layouts, components, state stores.
- Task 10 (Auth Flow) -- session management, auth guard.
- Task 11 (Org Selector) -- org state store.
- Task 12 (Org Management) -- CreateOrgModal component.

## Notes

- The landing page content (copy) should be simple and accurate for MVP. It will be refined by product/marketing later.
- The landing page does not need SEO optimization for Epic 1. Focus on functionality.
- The dashboard redirect logic should happen client-side after the stores are hydrated, not as a server-side redirect. This avoids issues with the read model not yet being updated after first login.
- The empty state illustration can be a simple SVG icon, not a complex illustration. Keep it minimal.
- "Create Product" button on the org dashboard is intentionally disabled for Epic 1. It serves as a visual placeholder showing where the feature will appear in Epic 2.
