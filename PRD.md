# Product Requirements Document: Vut

## 1. Overview

### Problem Statement

Project management tools are built on time-centric estimation: story points, sprints, velocity charts, and burn-downs. This creates a culture of false precision, wasted estimation effort, and pressure to meet arbitrary deadlines. Teams spend more time estimating than delivering, and the estimates are wrong anyway. There is no mainstream tool built from the ground up for teams who reject time-based estimation entirely.

### Product Vision

Vut is a **#noestimate project management platform** -- a multi-tenant SaaS product where work is tracked by flow, not by time. There are no story points, no sprints, no velocity, and no time tracking. Instead, Vut provides a single, powerful signal: **a cumulative flow diagram that shows how work flows through your process and projects when it will be done.** Everything else -- the backlog, the kanban board, the tags -- exists to make that signal accurate.

The philosophy is simple: count work, track status transitions, let the data speak. No guessing, no rituals, no theater.

### Target Users

- Software teams and their leads who have adopted or want to adopt #noestimate practices.
- Product managers who want visibility into delivery without mandating estimation ceremonies.
- Organizations that value flow efficiency over utilization metrics.

### Success Metrics

**Product Constraints:**
1. Zero time-centric features exist in the product -- this is a hard constraint, not a guideline.

**Activation & Onboarding:**
2. Teams can go from signup to a working kanban board in under 5 minutes.
3. 60%+ of new signups create at least 1 product and move at least 5 tasks within their first 7 days (activation rate).

**Core Value Delivery:**
4. The cumulative flow diagram produces a projected completion date within the first 2 weeks of use (requires enough data points from status transitions).
5. 50%+ of active products have at least one weekly view of the cumulative flow diagram (report adoption).

**Retention & Engagement:**
6. 70%+ weekly active team rate at 30 days post-signup (team retention).
7. Average team size per organization reaches 3+ members within 30 days (team adoption).

**Performance:**
8. Backlog loads in under 1 second for products with up to 10,000 tasks.
9. Kanban drag-and-drop reflects status change in under 200ms (optimistic update).

---

## 2. Goals & Non-Goals

### Goals

- Provide a fast, opinionated task management tool with a #noestimate philosophy baked in.
- Deliver a single report -- the cumulative flow diagram with projected completion date -- that replaces the need for velocity charts, burn-downs, and story points.
- Support multi-tenant, multi-org usage via GitHub SSO, following GitHub's organization model.
- Build on an event-sourced architecture so that all state changes are auditable, replayable, and suitable for the analytical needs of the cumulative flow report.
- **Self-hostable on developer machines:** The entire stack (frontend, backend, event store, read model, messaging) must be hostable on the team's own machines with no dependency on any cloud provider. This is a cost constraint -- the team must be able to develop, demo, and run Vut without paying for cloud infrastructure. Availability and redundancy details are left to the architecture.
- Ship an MVP that is immediately useful for a single team managing a product backlog and kanban board.

### Non-Goals (Explicitly Out of Scope)

- **No time tracking** -- no timers, no hours logged, no timesheets.
- **No story points or estimates** -- no numeric sizing, no T-shirt sizes, no relative estimation.
- **No sprints or iterations** -- no time-boxed planning cycles, no sprint boards, no retrospectives tied to time periods.
- **No velocity metrics** -- no calculated velocity, no trend lines based on throughput-per-sprint.
- **No Gantt charts** -- no dependency mapping, no critical path, no timeline views.
- **No multiple report types** -- the cumulative flow diagram is the only report for MVP.
- **No custom fields** -- tags and status cover categorization needs; no arbitrary field builder.
- **No email/password authentication** -- Third-party identity provider only (GitHub SSO for MVP).
- **No cloud provider dependency** -- the platform must be self-hostable on the team's own machines. No AWS, Azure, GCP, or any managed cloud service is required to run Vut. Cloud deployment is a future option, not a requirement.
- **No mobile applications** -- web-only for MVP.
- **No public API** -- internal use only for MVP. Can be exposed later.
- **No integrations with other tools** -- no Slack, no Jira import, no CI/CD hooks for MVP.

---

## 3. User Personas

### Team Lead / Engineering Manager

- Creates or joins an organization, sets up products, invites team members.
- Configures statuses and tags for the team's workflow.
- Relies on the cumulative flow diagram to communicate progress to stakeholders and answer "when will it be done?"
- Wants minimal ceremony -- no estimation meetings, no sprint planning rituals.

### Developer / Team Member

- Creates tasks in the backlog, moves them through statuses on the kanban board.
- Applies tags to categorize work (e.g., `area:frontend`, `type:bug`, `priority:high`).
- Needs the interface to be fast and unopinionated about how they organize their work.

### Organization Owner

- Manages the organization, its membership, and its products.
- Can invite and remove members, assign roles.
- Has visibility across all products within the organization.

---

## 4. Features & Requirements

### 4.1 Authentication & Identity

**Third-Party Identity Provider with Internal Email Verification**

- Authentication is handled by a third-party identity provider (e.g., Auth0) to support multiple login methods now and in the future.
- **MVP:** GitHub SSO is the only enabled login method. Other providers (Google, Microsoft, etc.) can be enabled later via the identity provider configuration.
- No username/password registration. No email-based signup. Users must authenticate through a supported third-party provider.
- On first login, a Vut user profile is created from the provider's profile (display name, avatar).
- The identity provider handles all credential management; Vut does not store passwords.
- The system supports multiple identity providers per user (e.g., a user can link both GitHub and Google to the same Vut account). Auto-linking by email is performed when a new provider's email matches an existing user.

**Email Verification (Required Before Platform Access):**

Identity providers may not always return a verified email (e.g., GitHub users can hide their email). Vut cannot rely on third-party providers for email delivery. Therefore:

- After first login (or any login where email is not yet verified), the user is redirected to an email verification page before accessing any platform features.
- The user must provide their email address on the verification page. If the identity provider supplied an email, it is pre-filled but the user can change it.
- Vut sends a verification code (6-digit) to the provided email address. The code expires after 15 minutes.
- The user enters the code on the verification page to confirm ownership.
- **Email verification is a gate for all platform actions:** creating organizations, joining organizations, creating products, managing tasks — none of these are available until the email is verified.
- Once verified, the user is not asked again unless they explicitly change their email (a future feature).

**Acceptance Criteria:**
- A user can sign in with their GitHub account.
- A user cannot create an account without a GitHub account.
- Existing users are recognized on subsequent GitHub logins.
- After first login, the user is redirected to the email verification page.
- The user cannot access any platform features (orgs, products, tasks) until their email is verified.
- A verification code is sent to the email address provided by the user.
- Entering the correct code within 15 minutes verifies the email and grants platform access.
- If a user logs in with a different provider and the email matches an existing verified user, the identity is auto-linked.

---

### 4.2 Organizations (Multi-Tenancy)

**GitHub-Style Organization Model**

Organizations in Vut follow the same pattern as GitHub organizations:

- A user can create organizations.
- A user can belong to multiple organizations.
- Each organization has owners and members.
- Owners can invite users to the organization via email (the invitee signs in with GitHub to accept).
- Owners can remove members from the organization.
- Owners can promote members to owners or demote owners to members.
- Products belong to organizations. A product cannot exist outside an organization.

**Organization Roles:**
- **Owner:** Full control -- manage members, manage products, configure org-level settings, delete the organization.
- **Member:** Can access all products within the organization, create tasks, manage tags and statuses (within the product's configuration).

**Acceptance Criteria:**
- A user sees a list of their organizations on login and can switch between them.
- An owner can invite a new member by providing their email or GitHub username.
- An invited user sees the invitation after logging in and can accept or decline.
- A user can be a member of Organization A and an owner of Organization B simultaneously.
- Removing a user from an org revokes their access to all products in that org.

---

### 4.3 Products

**The Container for Work**

Products sit below organizations in the hierarchy:

```
Organization > Product > Task
```

- Each product belongs to exactly one organization.
- Tasks can only be created within a product.
- A product has a name, a description, and a set of configured statuses.
- Products are proper entities, fully event-sourced (not tags).

**Product Configuration:**
- When a product is created, the creator defines its initial set of statuses (e.g., "Backlog", "In Progress", "In Review", "Done").
- The **first status in the list is the "backlog-only" status.** Tasks in this status appear in the backlog view but are excluded from the kanban board. This is a structural designation, not a naming convention -- the creator can name it anything ("Backlog", "New", "Inbox", etc.), but the first status always serves this role.
- Statuses can be added, renamed, or removed later by organization members. The backlog-only status can be renamed but cannot be removed or reordered (it always remains the first status).

**Acceptance Criteria:**
- A user with member or owner role can create a product within their organization.
- A product has a unique name within its organization.
- Creating a product requires defining at least two statuses.
- The first status in the list is automatically designated as the backlog-only status (excluded from kanban board).
- The backlog-only status can be renamed but cannot be removed or moved from the first position.
- Tasks cannot exist outside a product.

---

### 4.4 Tasks

**The Unit of Work**

- Each task belongs to exactly one product.
- A task has:
  - **Title** (required)
  - **Description** (optional, markdown-supported)
  - **Status** (required, defaults to the product's first status -- the backlog-only status)
  - **Tags** (optional, zero or more)
  - **Created timestamp**
  - **Last updated timestamp**
- Tasks are fully event-sourced. Every change (creation, status change, tag addition/removal, title/description edit, deletion) is recorded as an event in the event stream.

**Task Deletion:**
- Tasks can be deleted by any member of the product's organization.
- Deletion is a soft delete: a `TaskDeleted` event is recorded, and the task is excluded from the backlog, kanban board, and cumulative flow diagram going forward.
- Deleted tasks are NOT purged from the event store -- their history is preserved for auditability.
- Deleted tasks no longer count toward the cumulative flow diagram's projected completion date.
- Deletion is irreversible in MVP (no "undelete" feature).

**Task Events (minimum set for MVP):**
| Event | Payload |
|---|---|
| `TaskCreated` | taskId, productId, title, description, actorId, timestamp |
| `TaskTitleChanged` | taskId, newTitle, actorId, timestamp |
| `TaskDescriptionChanged` | taskId, newDescription, actorId, timestamp |
| `TaskStatusChanged` | taskId, oldStatus, newStatus, actorId, timestamp |
| `TagAdded` | taskId, tag, actorId, timestamp |
| `TagRemoved` | taskId, tag, actorId, timestamp |
| `TaskDeleted` | taskId, actorId, timestamp |

**Status Transitions:**
- A task's status can be changed to any other status defined for the product. There are no enforced transition rules in MVP (any status to any status).
- Every status change is an event, which feeds the cumulative flow diagram.

**Acceptance Criteria:**
- A user can create a task in any product they have access to.
- A task's status can be changed to any status defined in the product's configuration.
- A task can have zero, one, or many tags applied.
- Tags follow the namespaced format `namespace:value` (e.g., `area:backend`, `type:bug`).
- A user can delete a task; a `TaskDeleted` event is recorded and the task is excluded from all views.
- All task mutations are recorded as immutable events.

---

### 4.5 Tags

**Namespaced, Flexible Categorization**

- Tags are strings in the format `namespace:value`.
  - Namespace: the category (e.g., `area`, `type`, `priority`, `team`).
  - Value: the specific label (e.g., `frontend`, `bug`, `high`, `infra`).
- Tags are free-form -- users type them in, and they are stored as-is.
- Tags are defined at the product level. When a user types a tag, autocomplete suggests previously used tags in that product.
- Tags are NOT a separate entity -- they are strings attached to task events.
- Tags are used for filtering in the backlog and kanban views, and for filtering what's included in the cumulative flow report.

**Acceptance Criteria:**
- A user can add a tag `area:frontend` to a task, and it is stored exactly as that string.
- Autocomplete suggests tags that have been previously used in the same product.
- Tags can be used as filters in both backlog and kanban views.

---

### 4.6 Backlog View

**All Tasks, Filterable**

- The backlog view shows ALL tasks in a product, regardless of status.
- This is the default landing page when navigating to a product.
- Tasks are displayed in a list format with columns for: title, status, tags, created date, last updated.
- Users can filter by:
  - Status (single or multiple)
  - Tags (include/exclude)
  - Text search (title and description)
- Users can sort by: created date, last updated, title, status.
- Users can create new tasks directly from the backlog view.

**Acceptance Criteria:**
- All tasks in the product are visible in the backlog.
- Filters and sorting are applied client-side with server-side support for large datasets.
- The backlog loads in under 1 second for products with up to 10,000 tasks.

---

### 4.7 Kanban Board

**Active Work, Column-Based**

- The kanban board shows ONLY tasks whose status is NOT the product's backlog-only status (the first status in the product's status list).
- Each column represents a non-backlog-only status defined for the product.
- Tasks are displayed as cards within their status column.
- Users can drag-and-drop cards between columns to change status.
- Dragging a card to a column triggers a `TaskStatusChanged` event.

**Saved Filters and Sorting:**
- Users can apply filters (by tags, assignee, text) and sorting on the kanban board.
- Users can save a filter + sort configuration as a named view (e.g., "Backend Team", "High Priority Only").
- Saved views appear as tabs or a dropdown at the top of the kanban board.
- Clicking a saved view instantly applies its filter and sort.
- Saved views are per-user (not shared) in MVP.
- This replaces the need for multiple dedicated views -- the user creates their own via saved filters.

**Acceptance Criteria:**
- Tasks in the backlog-only status never appear on the kanban board.
- Each non-backlog-only status gets its own column.
- Drag-and-drop between columns changes the task's status and records the event.
- A user can create, rename, and delete saved views.
- Switching between saved views is instantaneous (no page reload).

---

### 4.8 Cumulative Flow Diagram

**The Core Report**

This is the primary -- and only -- report in Vut. It visualizes how work flows through the process over time and projects a completion date.

**What It Shows:**
- An area/stacked chart where each colored band represents a status.
- The X-axis is time (days/weeks).
- The Y-axis is count of tasks.
- Each band shows how many tasks were in that status on a given day.
- The total height of the chart at any point represents the total number of tasks.

**Projected Completion Date:**
- The chart must clearly indicate the projected completion date -- the date when all tasks are expected to reach the final status (e.g., "Done").
- Projection method: use the historical rate of tasks reaching the final status (throughput) to estimate when remaining tasks will complete. This is derived purely from status transition events -- no estimates, no story points.
- The projected date is displayed as a vertical line or annotation on the chart, with a clear label.
- The projection should include a confidence indicator (e.g., range rather than a single date).

**Accessibility:**
- All members of the product (all org members with access to the product) can view the chart.
- The chart is read-only -- it is a visualization, not interactive editing.

**Tag-Based Filtering:**
- Users can select which tags to include in the report (e.g., show only tasks tagged `team:backend`).
- This allows sub-reports without needing separate report types.
- When tags are filtered, the projection recalculates based on the filtered subset.

**Data Source:**
- The chart is built entirely from `TaskStatusChanged` events in the event store.
- The read model maintains a daily snapshot of task counts per status for efficient querying.

**Acceptance Criteria:**
- The chart renders correctly with at least 1 day of data.
- The projected completion date appears as a clear visual element on the chart.
- Filtering by tags updates the chart and projection in real-time.
- All product members can access the chart from the product navigation.

---

### 4.9 Architectural Constraints

The following architectural decisions are **product-level constraints** that shape how Vut behaves. Detailed technical design (event schemas, infrastructure, projections) is documented separately in `architecture.md`.

- **Event-sourced by design:** All state changes (tasks, products, organizations, users) are recorded as immutable events. This is not an implementation detail -- it is a product feature. It enables full auditability, the cumulative flow diagram's historical accuracy, and the ability to rebuild any view from the event history.
- **Self-hostable on developer machines:** The entire stack must be hostable on the team's own machines with no cloud provider or managed service required. This keeps operating costs at zero during development and early adoption. Deployment topology and availability details are defined in `architecture.md`.
- **Every event captures who and when:** All events must include the actor who triggered it and a UTC timestamp. No exceptions.
- **Eventual consistency:** The read model (used for backlog, kanban, and reports) is derived from the event stream and may lag slightly behind writes. The product uses optimistic UI updates to mask this delay.
- **No direct database access by clients:** All data access goes through the API layer to enforce tenant isolation and authorization.
- **HTTPS only:** All communication is over TLS.
- **No secrets in events:** Events never contain authentication tokens or sensitive credentials.

> **See also:** `architecture.md` for tech stack, component architecture, event schemas, read model projections, security details, and scalability considerations.

---

## 5. Security & Authorization

- **Authentication:** Third-party identity provider (Auth0). No stored passwords.
- **Authorization:** Role-based access at the organization level (owner vs. member). Product access is inherited from org membership.
- **Tenant Isolation:** All queries are scoped to the user's organization memberships. Cross-org data access is prevented at the API layer.

---

## 6. User Flows

### 6.1 First-Time User Onboarding

1. User clicks "Sign in with GitHub" on the Vut landing page.
2. Auth0 handles the GitHub OAuth flow; user is redirected back to Vut.
3. Vut creates a user profile from GitHub data (`UserCreated` + `IdentityLinked` events).
4. User is redirected to the email verification page.
5. User enters their email address (pre-filled if GitHub provided one) and clicks "Send Code."
6. Vut sends a 6-digit verification code to the email (`EmailVerificationRequested` event).
7. User enters the code. On success, `EmailVerified` event is emitted.
8. User sees an empty state with a prompt to create or join an organization.
9. User creates their first organization (`OrganizationCreated` event).
10. User is now the owner of the org and can create products.

### 6.2 Creating a Product and First Tasks

1. Owner/member navigates to the organization dashboard.
2. Clicks "Create Product."
3. Enters product name, description, and defines initial statuses (e.g., "Backlog", "In Progress", "Done"). The first status is automatically the backlog-only status.
4. `ProductCreated` event is emitted with the initial status configuration.
5. User is taken to the product backlog (empty).
6. User creates their first task -- enters title and optional description.
7. `TaskCreated` event is emitted; task appears in backlog with the first (backlog-only) status.

### 6.3 Working with the Kanban Board

1. User navigates to a product's kanban board.
2. Board shows columns for all statuses except the backlog-only status (e.g., "In Progress", "In Review", "Done").
3. User changes a task's status from the backlog-only status to "In Progress" via the backlog or a quick action.
4. The task now appears on the kanban board in the "In Progress" column.
5. User drags the card from "In Progress" to "In Review."
6. `TaskStatusChanged` event is recorded; the card moves to the new column.

### 6.4 Using Saved Views on the Kanban Board

1. User applies a filter on the kanban board (e.g., tag `area:frontend`).
2. Board updates to show only tasks with that tag.
3. User clicks "Save this view" and names it "Frontend Tasks."
4. "Frontend Tasks" appears in the saved views list.
5. User clears the filter, then clicks "Frontend Tasks" to re-apply it instantly.

### 6.5 Viewing the Cumulative Flow Diagram

1. User navigates to a product's report view.
2. The cumulative flow diagram displays task counts per status over time.
3. A projected completion date is shown as a vertical line on the chart.
4. User applies a tag filter (e.g., `team:backend`) to see the projection for a subset of work.
5. The chart and projection update to reflect the filtered data.

### 6.6 Inviting a Team Member

1. Organization owner navigates to org settings > Members.
2. Owner clicks "Invite Member" and enters the invitee's email or GitHub username.
3. `MemberInvited` event is emitted.
4. The invitee receives an email with a link to accept.
5. Invitee clicks the link, signs in via the identity provider, and sees the invitation.
6. Invitee accepts; `MemberJoined` event is emitted.
7. The new member can now access all products in the organization.

---

## 7. Design & UX Guidelines

### 7.1 Design Philosophy

- **Fast and minimal:** The UI should feel instant. No loading spinners for routine interactions. Optimistic updates where possible.
- **Opinionated but flexible:** Vut has strong opinions about workflow (no time tracking, status-driven flow), but is flexible about categorization (free-form tags).
- **Data over ceremony:** The cumulative flow diagram is the hero element. Every other UI element exists to make the data in that chart accurate.

### 7.2 Platform

- **Web-only** for MVP. Desktop-first responsive design.
- SPA with client-side routing for snappy navigation.

### 7.3 Key UI Patterns

- **Sidebar navigation:** Organization selector at the top, product list below.
- **Backlog as default landing page** for a product, since it shows everything.
- **Kanban board** accessible via tab or navigation change within the product.
- **Report** accessible via tab or navigation -- always one click away.
- **Toast notifications** for async operations (e.g., "Task created", "Member invited").
- **Drag-and-drop** for kanban card movement.
- **Inline editing** for task title, description, and tags (no modal for quick edits).

### 7.4 Accessibility

- Keyboard-navigable kanban board (arrow keys to move between cards, Enter to open).
- Semantic HTML and ARIA labels for screen readers.
- Sufficient color contrast for status bands in the cumulative flow diagram.

---

## 8. Milestones & Phasing

### Phase 1: Foundation (MVP)

**Goal:** A single team can manage a product with a backlog, kanban board, and cumulative flow diagram.

**Deliverables:**
- Identity provider integration (Auth0 with GitHub SSO for MVP)
- Organization creation and member management (owners and members)
- Product creation with configurable statuses
- Task CRUD (including deletion) with status changes and tags
- Event sourcing infrastructure (KurrentDB, Redpanda)
- PostgreSQL read model with projections
- Backlog view with filtering and sorting
- Kanban board with drag-and-drop status changes
- Saved filters/views on the kanban board
- Cumulative flow diagram with projected completion date
- Tag-based filtering on the report
- Self-hosted deployment setup for the team's own machines (no cloud dependency)

### Phase 2: Collaboration & Refinement

**Goal:** Support multiple teams and cross-team visibility.

**Potential Deliverables:**
- Task assignments (assigning users to tasks)
- Comments/activity feed on tasks
- Real-time updates (WebSocket-based board updates when teammates move cards)
- Shared saved views (team-level views)
- Organization-level settings and policies
- Improved onboarding flow

### Phase 3: Scale & Integration

**Goal:** Enterprise readiness and ecosystem integration.

**Potential Deliverables:**
- Public API for integrations
- Webhook support for CI/CD and notification systems
- Import/export capabilities (CSV, JSON)
- Organization analytics dashboard (across products)
- Custom roles and permissions (beyond owner/member)
- Audit log viewer (leveraging the event store)
- Billing and subscription management

---

## 9. Open Questions

1. **Projection confidence model:** The exact statistical model for the projected completion date (e.g., linear regression on throughput, Monte Carlo simulation) needs to be selected during implementation.
2. **Tag namespace registry:** Should tags be strictly validated against known namespaces, or is any `namespace:value` string accepted? The PRD assumes free-form, but a registry would enable better autocomplete and consistency.
3. **Minimum data threshold for projections:** How many data points (days of status transitions) are needed before the cumulative flow diagram shows a projected completion date? Too few data points produce misleading projections.
4. **Email change flow:** Should users be able to change their verified email after the initial verification? If so, what's the flow (re-verify, cool-down period, notification to org owners)?

> **Technical open questions** (component library selection, projection recalculation strategy, organization deletion semantics) have been moved to `architecture.md`.

### Resolved Decisions

- **Email verification strategy:** Vut collects and verifies email internally rather than relying on identity providers. Rationale: providers like GitHub may not return email (user privacy settings), and provider APIs/policies can change without notice. Internal verification ensures Vut always has a verified communication channel for invitations and notifications.
- **Backlog-only status designation:** The first status in a product's status list is automatically the backlog-only status (excluded from the kanban board). This is a structural rule, not a naming convention. The status can be renamed but not removed or reordered.

---

## 10. Appendix

### Inspiration & References

- **#noestimate movement:** The product philosophy is rooted in the #noestimate movement, which challenges the value of time-based estimation in software development.
- **GitHub organization model:** Multi-tenancy, roles, and org membership follow GitHub's established pattern.
- **Kanban method:** The cumulative flow diagram is a core Kanban metric. Vut adopts this without adopting the full Kanban method's prescriptive elements.
- **Event sourcing:** The architecture follows event-sourcing principles as described by Martin Fowler and implemented in systems like KurrentDB.

### Key Terminology

| Term | Definition |
|---|---|
| Organization | The top-level tenant entity. Contains users (owners and members) and products. |
| Product | A container for work within an organization. Contains tasks and has its own status configuration. |
| Task | The unit of work. Has a title, description, status, and tags. |
| Status | A dedicated property on a task indicating its position in the workflow. Defined per product. |
| Tag | A namespaced label (`namespace:value`) for flexible categorization. |
| Backlog | A list view of all tasks in a product. |
| Kanban Board | A column-based view of tasks, excluding tasks in the backlog-only status (the first status in the product's status list). |
| Cumulative Flow Diagram | A stacked area chart showing task counts per status over time, with a projected completion date. |
| Event Stream | An append-only log of events that represents the history of an entity. |
