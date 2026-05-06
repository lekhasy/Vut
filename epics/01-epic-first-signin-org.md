# Epic 1: First Sign-In & Organization

## Vertical Slice Statement

A new user arrives at the Vut landing page, authenticates with their GitHub account, verifies their email address (required before any platform access), and creates or joins an organization. After this Epic, the user has a named workspace with team members and can navigate the application shell. Email verification is a hard gate -- no platform features are available until the user's email is confirmed.

## Target Personas

- Organization Owner (primary)
- Developer / Team Member (secondary -- accepting invites)

## User Stories

1. As a first-time visitor, I want to sign in with my GitHub account so that I can start using Vut without creating a new username and password.
2. As a first-time user, I want to be redirected to an email verification page after my first login so that I can confirm my email before accessing the platform.
3. As a first-time user, I want the email verification page to pre-fill my email from GitHub (if available) but allow me to change it so that I can use my preferred email address.
4. As a first-time user, I want to receive a 6-digit verification code at my email address so that I can prove ownership of that email.
5. As a first-time user, I want to enter the verification code and gain immediate access to the platform so that the onboarding process is smooth and quick.
6. As a returning user, I want to sign in with a different identity provider (e.g., Google) and have it automatically linked to my existing Vut account if the emails match so that I am not forced into a separate account.
7. As a first-time user, I want to see an empty state prompting me to create an organization so that I am guided toward my first meaningful action.
8. As an organization owner, I want to invite team members by email or GitHub username so that my collaborators can access the workspace.
9. As an invited user, I want to see my pending invitation after signing in (and verifying my email) so that I can accept or decline it.
10. As an organization owner, I want to promote a member to owner (or demote an owner to member) so that I can manage organizational control.
11. As an organization owner, I want to remove a member from the organization so that departing team members lose access.
12. As a returning user who belongs to multiple organizations, I want to see a list of my organizations and switch between them so that I can work across teams.

## Acceptance Criteria

### Authentication & Email Verification
- [ ] A user can click "Sign in with GitHub" and complete the OAuth flow via Auth0, landing on the Vut dashboard.
- [ ] No email/password sign-up path exists anywhere in the product.
- [ ] On first login, `UserCreated` and `IdentityLinked` events are emitted to KurrentDB and a user profile (display name, avatar from GitHub) is visible in the UI.
- [ ] After first login (or any login where email is not yet verified), the user is redirected to the email verification page before accessing any platform features.
- [ ] The email verification page pre-fills the email address from the identity provider (if available) but allows the user to change it.
- [ ] A 6-digit verification code is sent to the email address provided by the user (`EmailVerificationRequested` event). The code expires after 15 minutes.
- [ ] Entering the correct code within 15 minutes verifies the email (`EmailVerified` event) and grants platform access.
- [ ] The user cannot access any platform features (orgs, products, tasks) until their email is verified. This is enforced at the BFF middleware level.
- [ ] If a user logs in with a different provider and the email matches an existing verified user, the identity is auto-linked (`IdentityLinked` event) to the existing account.

### Organizations
- [ ] A first-time user with verified email and no organizations sees an empty state with a clear call-to-action to create one.
- [ ] Creating an organization emits `OrganizationCreated` and makes the creator the first owner.
- [ ] An owner can invite a member by providing an email or GitHub username; a `MemberInvited` event is emitted.
- [ ] An invited user sees the invitation after logging in and verifying their email, and can accept (emitting `MemberJoined`) or decline.
- [ ] An owner can change a member's role (emitting `MemberRoleChanged`), remove a member (emitting `MemberRemoved`), or rename the organization (emitting `OrganizationRenamed`).
- [ ] The sidebar shows the current organization selector with a list of the user's organizations; switching organizations updates the visible products.
- [ ] Cross-org data isolation is enforced: a user cannot see data from an organization they do not belong to.

## Event Streams Introduced

| Stream | Events |
|--------|--------|
| User | `UserCreated` (userId, displayName, avatarUrl, email, actorId, timestamp), `IdentityLinked` (userId, providerId, providerName, email, actorId, timestamp), `UserProfileUpdated`, `EmailVerificationRequested` (userId, email, token, actorId, timestamp), `EmailVerified` (userId, email, actorId, timestamp) |
| Organization | `OrganizationCreated`, `OrganizationRenamed`, `MemberInvited`, `MemberJoined`, `MemberRemoved`, `MemberRoleChanged`, `OrganizationDeleted` |

## Projection Views Introduced

| View | Purpose |
|------|---------|
| User Projection | Display name, avatar URL, email, email verification status (`is_email_verified`) |
| User Identity | Linked identity providers per user (provider_id, provider_name, email) -- supports multi-provider login and auto-linking |
| Organization Projection | Org name, member list with roles |

## Technical Scope

- **Auth0 tenant setup** with GitHub as the sole enabled connection (other providers can be enabled later via Auth0 configuration).
- **Email verification flow**: verification page (`/verify-email`), 6-digit code generation in User Actor, code delivery via SMTP email service, 15-minute expiry, BFF middleware guard that blocks all platform access for unverified users.
- **Multi-provider identity support**: User Actor handles `IdentityLinked` events for multiple providers, BFF performs auto-linking by email on login with a new provider.
- **Astro.js SPA shell**: layout, sidebar navigation, organization selector, routing skeleton, email verification page.
- **Proto.Actor User and Organization actors**: process commands, emit events to KurrentDB. User Actor handles email verification lifecycle and identity linking.
- **KurrentDB streams**: `user-{userId}`, `organization-{orgId}`.
- **Redpanda topics**: user events, organization events.
- **PostgreSQL read model**: user projection (with `is_email_verified`), user identity table (multi-provider), organization projection tables -- updated by Redpanda consumers.
- **Invitation flow**: email delivery with acceptance link.
- **SMTP email service**: sends verification codes and invitation emails.
- **Kubernetes manifests** for API gateway, actor services, KurrentDB, Redpanda, PostgreSQL.

## Out of Scope for This Epic

- Products and tasks (Epic 2, Epic 3).
- Kanban board, cumulative flow diagram, saved views (Epic 4, Epic 5, Epic 6).
- Organization deletion (can be deferred; a `OrganizationDeleted` event is defined but the UI action is not required for this Epic).
- Billing, subscriptions, custom roles.

## Estimated Complexity

**Large** -- This Epic establishes the entire infrastructure footprint (event sourcing, actors, messaging, read models, Kubernetes, CI/CD). Subsequent Epics add domain features on top of this foundation.

## How to Demo

1. Show the Vut landing page. Click "Sign in with GitHub."
2. Complete GitHub OAuth via Auth0. User is created (`UserCreated` + `IdentityLinked` events emitted).
3. User is redirected to the email verification page. The email from GitHub is pre-filled (if available).
4. User clicks "Send Code." A 6-digit verification code is sent to the email (`EmailVerificationRequested` event).
5. User enters the code. On success, `EmailVerified` event is emitted, email is confirmed.
6. User is redirected to the dashboard with empty state -- prompt to create an organization.
7. Create an organization named "Acme Corp." (`OrganizationCreated` event). User is now the owner.
8. Invite a teammate by email.
9. In a second browser session, sign in as the invitee with GitHub, complete email verification, accept the invitation.
10. As the owner, promote the new member to owner.
11. Switch between organizations in the sidebar (create a second org to demonstrate).
12. (Bonus) Show auto-linking: sign in with a different provider (e.g., Google) using the same email -- the identity is linked to the existing account.
