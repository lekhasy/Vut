# Epic 1: First Sign-In & Organization

## Vertical Slice Statement

A new user arrives at the Vut landing page, authenticates with their GitHub account, and creates or joins an organization. After this Epic, the user has a named workspace with team members and can navigate the application shell.

## Target Personas

- Organization Owner (primary)
- Developer / Team Member (secondary -- accepting invites)

## User Stories

1. As a first-time visitor, I want to sign in with my GitHub account so that I can start using Vut without creating a new username and password.
2. As a first-time user, I want to see an empty state prompting me to create an organization so that I am guided toward my first meaningful action.
3. As an organization owner, I want to name my organization so that my team can identify it.
4. As an organization owner, I want to invite team members by email or GitHub username so that my collaborators can access the workspace.
5. As an invited user, I want to see my pending invitation after signing in so that I can accept or decline it.
6. As an organization owner, I want to promote a member to owner (or demote an owner to member) so that I can manage organizational control.
7. As an organization owner, I want to remove a member from the organization so that departing team members lose access.
8. As a returning user who belongs to multiple organizations, I want to see a list of my organizations and switch between them so that I can work across teams.

## Acceptance Criteria

- [ ] A user can click "Sign in with GitHub" and complete the OAuth flow via Auth0, landing on the Vut dashboard.
- [ ] No email/password sign-up path exists anywhere in the product.
- [ ] On first login, a `UserCreated` event is emitted to KurrentDB and a user profile (display name, avatar from GitHub) is visible in the UI.
- [ ] A first-time user with no organizations sees an empty state with a clear call-to-action to create one.
- [ ] Creating an organization emits `OrganizationCreated` and makes the creator the first owner.
- [ ] An owner can invite a member by providing an email or GitHub username; a `MemberInvited` event is emitted.
- [ ] An invited user sees the invitation after logging in and can accept (emitting `MemberJoined`) or decline.
- [ ] An owner can change a member's role (emitting `MemberRoleChanged`), remove a member (emitting `MemberRemoved`), or rename the organization (emitting `OrganizationRenamed`).
- [ ] The sidebar shows the current organization selector with a list of the user's organizations; switching organizations updates the visible products.
- [ ] Cross-org data isolation is enforced: a user cannot see data from an organization they do not belong to.

## Event Streams Introduced

| Stream | Events |
|--------|--------|
| User | `UserCreated`, `UserProfileUpdated` |
| Organization | `OrganizationCreated`, `OrganizationRenamed`, `MemberInvited`, `MemberJoined`, `MemberRemoved`, `MemberRoleChanged`, `OrganizationDeleted` |

## Projection Views Introduced

| View | Purpose |
|------|---------|
| User Projection | Display name, avatar URL, provider ID |
| Organization Projection | Org name, member list with roles |

## Technical Scope

- **Auth0 tenant setup** with GitHub as the sole enabled connection.
- **Astro.js SPA shell**: layout, sidebar navigation, organization selector, routing skeleton.
- **Proto.Actor User and Organization actors**: process commands, emit events to KurrentDB.
- **KurrentDB streams**: `user-{userId}`, `organization-{orgId}`.
- **Redpanda topics**: user events, organization events.
- **PostgreSQL read model**: user and organization projection tables, updated by Redpanda consumers.
- **Invitation flow**: email delivery (or in-app notification for MVP) with acceptance link.
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
2. Complete GitHub OAuth. Land on the dashboard with empty state.
3. Create an organization named "Acme Corp."
4. Invite a teammate by email.
5. In a second browser session, sign in as the invitee, accept the invitation.
6. As the owner, promote the new member to owner.
7. Switch between organizations in the sidebar (create a second org to demonstrate).
