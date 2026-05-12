# Product Requirements Document: Velucid

## 1. Overview

### Problem Statement

Project management tools are built on time-centric estimation: story points, sprints, velocity charts, and burn-downs. This creates a culture of false precision, wasted estimation effort, and pressure to meet arbitrary deadlines. Teams spend more time estimating than delivering, and the estimates are wrong anyway. There is no mainstream tool built from the ground up for teams who reject time-based estimation entirely.

### Product Vision

Velucid is a **#noestimate project management platform** -- a multi-tenant SaaS product where work is tracked by flow, not by time. There are no story points, no sprints, no velocity, and no time tracking. Instead, Velucid provides a single, powerful signal: **a probabilistic forecast that shows when your work will be done — not as a single date, but as an honest probability distribution derived from Monte Carlo simulation.** Everything else -- the backlog, the kanban board, the tags -- exists to make that signal accurate.

The philosophy is simple: count work, track status transitions, model the uncertainty honestly, let the data speak. No guessing, no rituals, no theater. Never display a single completion date — every forecast is a probability curve.

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
4. The probabilistic forecast produces confidence-based completion dates (50%, 70%, 95%) within the first 2 weeks of use (requires at least 7 days of status transition data).
5. 50%+ of active products have at least one weekly view of the probabilistic forecast (report adoption).

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
- Deliver a single report -- a probabilistic forecast powered by Monte Carlo simulation -- that replaces the need for velocity charts, burn-downs, and story points. Every forecast is expressed as a probability distribution, never a single date.
- Support multi-tenant, multi-org usage via GitHub SSO, following GitHub's organization model.
- Build on an event-sourced architecture so that all state changes are auditable, replayable, and suitable for the analytical needs of the probabilistic forecast.
- **Self-hostable on developer machines:** The entire stack (frontend, backend, event store, read model, messaging) must be hostable on the team's own machines with no dependency on any cloud provider. This is a cost constraint -- the team must be able to develop, demo, and run Velucid without paying for cloud infrastructure. Availability and redundancy details are left to the architecture.
- Ship an MVP that is immediately useful for a single team managing a product backlog and kanban board.

### Non-Goals (Explicitly Out of Scope)

- **No time tracking** -- no timers, no hours logged, no timesheets.
- **No story points or estimates** -- no numeric sizing, no T-shirt sizes, no relative estimation.
- **No sprints or iterations** -- no time-boxed planning cycles, no sprint boards, no retrospectives tied to time periods.
- **No velocity metrics** -- no calculated velocity, no trend lines based on throughput-per-sprint.
- **No Gantt charts** -- no dependency mapping, no critical path, no timeline views.
- **No multiple report types** -- the probabilistic forecast (with its S-curve and progress views) is the only report for MVP.
- **No custom fields** -- tags and status cover categorization needs; no arbitrary field builder.
- **No email/password authentication** -- Third-party identity provider only (GitHub SSO for MVP).
- **No cloud provider dependency** -- the platform must be self-hostable on the team's own machines. No AWS, Azure, GCP, or any managed cloud service is required to run Velucid. Cloud deployment is a future option, not a requirement.
- **No mobile applications** -- web-only for MVP.
- **No public API** -- internal use only for MVP. Can be exposed later.
- **No integrations with other tools** -- no Slack, no Jira import, no CI/CD hooks for MVP.

---

## 3. User Personas

### Team Lead / Engineering Manager

- Creates or joins an organization, sets up products, invites team members.
- Configures statuses and tags for the team's workflow.
- Relies on the probabilistic forecast to communicate progress to stakeholders and answer "what are the chances we'll be done by date X?" — using probability distributions, not single dates.
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
- On first login, a Velucid user profile is created from the provider's profile (display name, avatar).
- The identity provider handles all credential management; Velucid does not store passwords.
- The system supports multiple identity providers per user (e.g., a user can link both GitHub and Google to the same Velucid account). Auto-linking by email is performed when a new provider's email matches an existing user.

**Email Verification (Required Before Platform Access):**

Identity providers may not always return a verified email (e.g., GitHub users can hide their email). Velucid cannot rely on third-party providers for email delivery. Therefore:

- After first login (or any login where email is not yet verified), the user is redirected to an email verification page before accessing any platform features.
- The user must provide their email address on the verification page. If the identity provider supplied an email, it is pre-filled but the user can change it.
- Velucid sends a verification code (6-digit) to the provided email address. The code expires after 15 minutes.
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

Organizations in Velucid follow the same pattern as GitHub organizations:

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
- Deletion is a soft delete: a `TaskDeleted` event is recorded, and the task is excluded from the backlog, kanban board, and forecast going forward.
- Deleted tasks are NOT purged from the event store -- their history is preserved for auditability.
- Deleted tasks no longer count toward the forecast's probability distribution.
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
- Every status change is an event, which feeds the probabilistic forecast.

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
- Tags are used for filtering in the backlog and kanban views, and for filtering what's included in the forecast report.

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

### 4.8 Probabilistic Forecasting

**The Core Report**

This is the primary -- and only -- report in Velucid. It replaces traditional cumulative flow diagrams and single-date projections with an honest, probability-based forecasting system powered by Monte Carlo simulation. The core principle: **never display a single completion date anywhere in the UI. Every forecast must be expressed as a probability or a confidence range.**

> See `docs/velucid_forecasting_spec.md` for the full technical specification, including the Monte Carlo algorithm, data model, and detailed UI specification.

**Why Not a Traditional Cumulative Flow Diagram:**

A traditional CFD shows bands for each kanban column (backlog → in progress → review → done). While useful for visualizing flow, it does not honestly communicate forecast uncertainty. A single regression line implies false precision. In reality, two sources of variance make any single date misleading:

- **Throughput variance** — the rate at which the team completes tasks changes day to day due to interruptions, complexity, team changes, and morale.
- **Scope variance** — the total number of tasks is not fixed. Scope grows as discovery happens, requirements are clarified, and stakeholders add work.

Velucid addresses both by running thousands of Monte Carlo simulations, each sampling a plausible future, and building a distribution of outcomes.

**Data Model — Two Lines, Not Status Bands:**

The forecast requires only two time series. No story points, no hour estimates, no complexity scores:

- **Completed count** — a cumulative count of tasks marked done, sampled daily. This is the only throughput signal.
- **Scope count** — a cumulative count of all tasks that exist in the project at each sample point (done + not done). This is not a fixed number — it grows.

The project completes when `completed[t] >= scope[t]`. This intersection is what all forecasting targets — not a date you type in.

**Monte Carlo Simulation:**

- Run 10,000 simulations, each re-sampling daily throughput and scope growth from historical distributions.
- Per-day re-sampling (not static) produces realistic path simulations where day-to-day variance naturally averages out while still capturing tail risk.
- Build a cumulative distribution function (CDF) from the results. Each percentile point answers: "by day X, what fraction of simulations had finished?"
- Minimum viable history: 7 days. Below 7 days, show a "gathering data" state. Use truncated normal distribution when history is between 7–10 days; empirical sampling when ≥10 days.
- Use a 14-day rolling window for computing throughput and scope growth samples (user-adjustable: 7, 14, 30 days).
- Mark non-working days (weekends, holidays) via a project-level "working days" setting (default: Mon–Fri) and exclude them from sampling distributions.

**Stat Cards:**

Above the chart, show three stat cards — the headline numbers users read first:

- **50% date** — labeled "50/50 estimate" in muted text.
- **70% date** — labeled "Planning date" and highlighted. This is the recommended date for stakeholder communication.
- **95% date** — labeled "95% likely by" in muted text.

Never label any date as "the completion date" without a confidence qualifier. "Done by Jun 28" is forbidden. "70% confidence: Jun 28" is correct.

**Threshold Slider:**

A slider (default 70%, range 50–99%) always visible alongside the stat cards. Adjusting it updates all stat card dates, the S-curve crosshair, and the confidence date line on the progress chart.

**Never-Finish Indicator:**

If any simulations resulted in never-finish (scope grows faster than throughput), show a fourth stat card:

- **< 20%**: Show the percentage in muted text.
- **20–50%**: Amber warning. *"X% of forecasts never finish — consider reducing scope or increasing team capacity."*
- **> 50%**: Red alert. *"X% of forecasts never finish — scope is growing faster than work is completing. Review and reduce active scope."*

**Primary View — Forecast Chart (S-Curve):**

The primary view is the probability S-curve. It directly answers: "what date has X% confidence?"

- X-axis: calendar dates (today to p95 date).
- Left Y-axis: cumulative probability (0–100%).
- Right Y-axis: probability density (histogram bars behind the curve).
- S-curve line in blue (#378ADD).
- Threshold line in amber (#EF9F27) with crosshair showing the exact date.
- Region to the left of the threshold filled in pale amber.

**Secondary View — Progress & Forecast Chart (Dual-Cone):**

Shows historical progress and projected cones — the detailed "why" behind the forecast:

- **Scope line** (solid gray): historical scope growth up to today.
- **Completed line** (solid blue): historical completed count with light fill below.
- **Scope cone** (dashed gray bands): p10–p90 of scope growth projections.
- **Completed cone** (dashed blue bands): p10–p90 of throughput projections.
- **Intersection region** (amber, ~35% opacity): the overlap zone where the cones meet — the visual answer to "when will this finish?"
- **Confidence date line**: vertical solid amber at the date corresponding to the slider setting.
- **Today marker**: vertical dashed line where history ends and forecast begins.
- X-axis: calendar dates. Y-axis: task count (cumulative). No story points, no hours.

> **This is NOT a traditional Cumulative Flow Diagram.** A traditional CFD shows bands for each kanban column. This chart shows only two lines (completed + scope), so calling it a "CFD" would create incorrect expectations. It is a "Progress & Forecast chart."

**Forecast Spread as Health Signal:**

The spread between p50 and p95 dates is a direct proxy for project health:

- **< 3 weeks span**: "High predictability" label. Team is consistent, scope is stable.
- **3–8 weeks span**: Normal state. No label.
- **8+ weeks span**: "High uncertainty" label. Throughput is erratic, scope is growing fast, or both.
- **No convergence** (cones never intersect): Red warning. "At current rates, this project has no projected completion."

**Hover Tooltips:**

On the progress & forecast chart: completed tasks, scope, WIP gap, and probability of completion (future dates). On the forecast chart: probability of completion and corresponding confidence date.

**No Deadline Field:**

Velucid does not have a "deadline" or "due date" field on projects. Users can add a vertical "target marker" reference line on either chart, which shows the probability of completion by that date as a label. This makes the question honest: not "will we hit the deadline" but "what are our chances of hitting this target."

**Tag-Based Filtering:**

- Users can select which tags to include in the report (e.g., show only tasks tagged `team:backend`).
- When tags are filtered, the forecast recalculates based on the filtered subset.

**Accessibility:**

- All members of the product can view the forecast.
- The forecast is read-only.
- Color contrast between chart elements meets WCAG 2.1 AA guidelines.

**Acceptance Criteria:**

- The forecast view is accessible via a "Forecast" tab (or navigation item) within each product, alongside Backlog and Kanban Board.
- Three stat cards display 50%, 70%, and 95% confidence dates, all with explicit confidence labels.
- The threshold slider (50–99%, default 70%) updates all stat cards and chart elements instantly without re-running simulations.
- A never-finish indicator appears as a fourth stat card when any simulations fail to converge, with severity-appropriate styling.
- The primary view (S-curve) renders the CDF with probability on the Y-axis and dates on the X-axis.
- The secondary view (progress & forecast) renders historical completed and scope lines with projected cones and an intersection region.
- The forecast is based on Monte Carlo simulation (10,000 runs) using observed throughput and scope growth — no estimates, no story points.
- A "gathering data" state is shown when fewer than 7 days of history exist. No forecast is displayed.
- Tag-based filtering recalculates the forecast for the filtered subset in real time (under 2 seconds).
- Forecast spread health signal is displayed alongside stat cards.
- All product members can view the forecast; it is read-only.
- No single date is ever displayed without a confidence qualifier.

---

### 4.9 Architectural Constraints

The following architectural decisions are **product-level constraints** that shape how Velucid behaves. Detailed technical design (event schemas, infrastructure, projections) is documented separately in `architecture.md`.

- **Event-sourced by design:** All state changes (tasks, products, organizations, users) are recorded as immutable events. This is not an implementation detail -- it is a product feature. It enables full auditability, the forecast's historical accuracy, and the ability to rebuild any view from the event history.
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

1. User clicks "Sign in with GitHub" on the Velucid landing page.
2. Auth0 handles the GitHub OAuth flow; user is redirected back to Velucid.
3. Velucid creates a user profile from GitHub data (`UserCreated` + `IdentityLinked` events).
4. User is redirected to the email verification page.
5. User enters their email address (pre-filled if GitHub provided one) and clicks "Send Code."
6. Velucid sends a 6-digit verification code to the email (`EmailVerificationRequested` event).
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

### 6.5 Viewing the Probabilistic Forecast

1. User navigates to a product's Forecast tab.
2. Three stat cards display: "50/50 estimate," "Planning date" (70% confidence, highlighted), and "95% likely by."
3. The primary view shows the S-curve (probability CDF) — the user can immediately see what date has their desired confidence level.
4. The user adjusts the threshold slider from 70% to 85%. All stat card dates update instantly; the S-curve crosshair moves to the new date.
5. The user switches to the secondary view (Progress & Forecast) to see historical completed and scope lines with projected cones fanning into the future.
6. The intersection region (amber) shows the probable completion window.
7. User applies a tag filter (e.g., `team:backend`) to see the forecast for a subset of work.
8. The forecast recalculates: stat cards, S-curve, and cones all update to reflect the filtered data.
9. If a never-finish indicator is present (e.g., "12% of forecasts never finish"), the user sees it as a fourth stat card.

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
- **Opinionated but flexible:** Velucid has strong opinions about workflow (no time tracking, status-driven flow), but is flexible about categorization (free-form tags).
- **Data over ceremony:** The probabilistic forecast is the hero element. Every other UI element exists to make the data in that forecast accurate.

### 7.2 Platform

- **Web-only** for MVP. Desktop-first responsive design.
- SPA with client-side routing for snappy navigation.

### 7.3 Key UI Patterns

- **Sidebar navigation:** Organization selector at the top, product list below.
- **Backlog as default landing page** for a product, since it shows everything.
- **Kanban board** accessible via tab or navigation change within the product.
- **Forecast** accessible via tab or navigation -- always one click away.
- **Toast notifications** for async operations (e.g., "Task created", "Member invited").
- **Drag-and-drop** for kanban card movement.
- **Inline editing** for task title, description, and tags (no modal for quick edits).

### 7.4 Accessibility

- Keyboard-navigable kanban board (arrow keys to move between cards, Enter to open).
- Semantic HTML and ARIA labels for screen readers.
- Sufficient color contrast for forecast chart elements (S-curve, cones, stat cards, intersection region).

---

## 8. Milestones & Phasing

### Phase 1: Foundation (MVP)

**Goal:** A single team can manage a product with a backlog, kanban board, and probabilistic forecast.

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
- Probabilistic forecast with Monte Carlo simulation (S-curve and progress & forecast views)
- Forecast stat cards, threshold slider, and never-finish detection
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

1. ~~**Projection confidence model:**~~ **RESOLVED.** Monte Carlo simulation (10,000 runs) has been selected as the forecasting method. It models both throughput variance and scope variance simultaneously, producing a full probability distribution. See `docs/velucid_forecasting_spec.md` for the complete specification.
2. **Tag namespace registry:** Should tags be strictly validated against known namespaces, or is any `namespace:value` string accepted? The PRD assumes free-form, but a registry would enable better autocomplete and consistency.
3. ~~**Minimum data threshold for projections:**~~ **RESOLVED.** Minimum 7 days of status transition history required before displaying a forecast. Below 7 days, show a "gathering data" state. Use truncated normal distribution for 7–10 days of history; empirical sampling for ≥10 days. See `docs/velucid_forecasting_spec.md`.
4. **Email change flow:** Should users be able to change their verified email after the initial verification? If so, what's the flow (re-verify, cool-down period, notification to org owners)?

> **Technical open questions** (component library selection, projection recalculation strategy, organization deletion semantics) have been moved to `architecture.md`.

### Resolved Decisions

- **Email verification strategy:** Velucid collects and verifies email internally rather than relying on identity providers. Rationale: providers like GitHub may not return email (user privacy settings), and provider APIs/policies can change without notice. Internal verification ensures Velucid always has a verified communication channel for invitations and notifications.
- **Backlog-only status designation:** The first status in a product's status list is automatically the backlog-only status (excluded from the kanban board). This is a structural rule, not a naming convention. The status can be renamed but not removed or reordered.

---

## 10. Appendix

### Inspiration & References

- **#noestimate movement:** The product philosophy is rooted in the #noestimate movement, which challenges the value of time-based estimation in software development.
- **GitHub organization model:** Multi-tenancy, roles, and org membership follow GitHub's established pattern.
- **Kanban method & probabilistic forecasting:** Velucid's forecast system is inspired by the Kanban method's flow metrics and advances beyond the traditional Cumulative Flow Diagram. By modeling both throughput and scope uncertainty via Monte Carlo simulation, it produces honest probability distributions instead of misleading single dates. See `docs/velucid_forecasting_spec.md`.
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
| Probabilistic Forecast | The core report: a Monte Carlo-based forecasting system that shows probability distributions of completion dates, not single dates. Includes an S-curve (CDF), stat cards, and a progress & forecast chart with dual cones. |
| Monte Carlo Simulation | A simulation technique that runs 10,000 random samples to build a probability distribution, modeling both throughput and scope growth uncertainty. |
| S-Curve (CDF) | Cumulative Distribution Function. The primary forecast view mapping each date to the probability that the project finishes by that date. |
| Never-Finish % | The fraction of Monte Carlo simulations where scope grew faster than throughput and the project never completed. A critical health metric. |
| Scope Cone | The projected fan of scope growth into the future, based on historical scope growth variance. |
| Completed Cone | The projected fan of completed tasks into the future, based on historical throughput variance. |
| Intersection Region | The zone where the completed cone and scope cone overlap. Represents the probable window of project completion. |
| Event Stream | An append-only log of events that represents the history of an entity. |
