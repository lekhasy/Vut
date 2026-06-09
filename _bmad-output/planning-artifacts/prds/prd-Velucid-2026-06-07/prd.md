---
title: Velucid
created: 2026-06-07
updated: 2026-06-07
status: draft
---

# PRD: Velucid

*Working title — confirm.*

## 0. Document Purpose

This PRD describes what Velucid does and the behavior its users see. It is the source of truth for product decisions; technical implementation (stack, transport, event schemas, infra) lives in `architecture.md`. The PRD is intended for the Product Manager, Engineering leads, and the architects/editors who consume it downstream (UX, architecture, epics). It uses a glossary-anchored vocabulary (see §3) — FRs, UJs, and SMs reference those terms verbatim.

The PRD is built on a clean-slate redesign dated 2026-06-07 that replaced an earlier model based on tags, kanban, Auth0, and a single hero forecast. The redesign rationale is captured in `.decision-log.md`. The forecasting technical spec at `docs/velucid_forecasting_spec.md` carries forward unchanged for now; it is the last thing to be re-scoped to the new aggregate model.

## 1. Vision

Velucid is a project management tool for teams who have stopped pretending that time-based estimation works. It is built on two convictions.

**First, the way a work item gets assigned to a team should be an event that says what it means.** Adding a "team" tag to a card in a kanban tool is just a string flip — the event log can no longer tell you whether the item was assigned, categorized, or both. Velucid uses **explicit aggregates**: WorkItems are assigned to Teams, TeamMembers, and Initiatives as first-class relationships, so the events are legible, the model is queryable, and the audit history means what it says. There are no tags pretending to be relationships. There is an *Assignment to a Team* — an event, recorded forever.

**Second, leadership deserves an honest answer to "when will this be done?"** Not a single date, not a velocity chart, not a sprint burndown. Velucid runs Monte Carlo simulation on observed work-item flow to produce a **probabilistic forecast**: a probability distribution, with explicit confidence bands (50%, 70%, 95%). It offers two independent views — **per Initiative** (the overall forecast) and **per Initiative × Team** (the bottleneck analysis) — so leaders can commit to a stakeholder date *and* see which team is slowing things down. Neither forecast depends on the other.

Velucid is local-first in feel: the React app reads from a local store, so the UI is instant. There is no kanban, no sprints, no story points, no velocity. Each Team has one priority-ordered list of WorkItems, drag-to-reorder, with nothing else getting in the way.

## 2. Target User

### 2.1 Jobs To Be Done

- When I'm running an Initiative, I want to know **when** it will finish, with an honest confidence band, so I can communicate a real date to stakeholders — not a guess.
- When I'm leading a team, I want to know **which team is the bottleneck** on an Initiative, so I can unblock them.
- When I'm organizing work, I want **explicit structure** (Initiatives, Teams, TeamMembers, WorkItems) so the model reflects reality, the event log is auditable, and I never have to ask "what does this tag mean?".
- When I'm working in the app, I want it to feel **instant** — no spinners, no waiting, no jank.
- When I'm onboarding a new team or member, I want to **get them up and running in minutes**, not days.

### 2.2 Non-Users (v1)

- Teams who *want* time-based estimation (story points, sprint planning, velocity). Velucid is intentionally not for them.
- Teams who need a kanban board with columns and WIP limits. Velucid deliberately has neither.
- Solo hobbyists. Velucid's value is in multi-team coordination, not personal task tracking.

### 2.3 Key User Journeys

- **UJ-1. Maya signs up and creates her first workspace.**
  - **Persona + context:** Maya, engineering manager, new to Velucid. She's heard it's the tool that does forecasting without the estimation theater.
  - **Entry state:** unauthenticated. Lands on the marketing page.
  - **Path:** clicks "Sign in" → enters her email on the sign-in page → receives a 6-digit login code in her inbox → enters the code → arrives at the empty-state screen ("Create your first workspace") → names her workspace "Acme Corp" (globally-unique check passes) → workspace is created.
  - **Climax:** she is the Owner of Acme Corp and sees the empty state of a brand-new workspace.
  - **Resolution:** she can now create Initiatives, create Teams, and invite people.
  - **Edge case:** if "Acme Corp" is already taken globally, the field shows an inline error and she picks a different name.

- **UJ-2. David accepts an invitation and joins an existing workspace.**
  - **Persona + context:** David, backend engineer. Maya invited him to Acme Corp's Backend Team.
  - **Entry state:** unauthenticated. He has an email containing a one-time invite code from Maya.
  - **Path:** clicks "Sign in" → enters his email → receives a 6-digit login code → enters it → the post-auth screen is "Enter your invite code" (because he has a pending invitation) → enters the code from Maya's email → he is jumped directly into Acme Corp, and the Backend Team is now in his list.
  - **Climax:** he is a Member of Acme Corp with a TeamMember record on Backend Team.
  - **Resolution:** he can see the Backend Team's priority list and start picking up WorkItems.
  - **Edge case:** if the invite code is expired or already used, the field shows an error and prompts him to ask Maya for a new invite.

- **UJ-3. Priya reorders the priority list for the week.**
  - **Persona + context:** Priya, lead of the Platform Team. Monday morning, planning the week.
  - **Entry state:** already signed in. On the Platform Team page.
  - **Path:** sees the team's WorkItem list, currently ordered by last activity → drags the most urgent item to the top → drags a low-priority item down two spots → releases.
  - **Climax:** the list reflects the new priority order instantly. The change syncs to the server in the background.
  - **Resolution:** her teammates see the new order in their UI in real time, without a refresh.
  - **Edge case:** if two teammates reorder the list simultaneously, both clients converge to a consistent order on the next sync (no data loss).

- **UJ-4. Marcus checks the forecast before a stakeholder meeting.**
  - **Persona + context:** Marcus, product lead. He has a stakeholder meeting in 20 minutes about the "Mobile Refresh" Initiative.
  - **Entry state:** already signed in. On the Mobile Refresh Initiative page.
  - **Path:** clicks the "Forecast" tab → sees three stat cards: 50% / 70% / 95% confidence dates → moves the threshold slider to 80% → the dates update instantly → switches to the "per Team" view → sees a chart showing each Team's forecast completion for this Initiative.
  - **Climax:** he has an honest date ("80% confidence: 2026-09-12") and a clear view that "Growth" is the bottleneck team.
  - **Resolution:** he walks into the meeting with a real number and a concrete unblock target.
  - **Edge case:** if the Initiative has fewer than 7 days of history, the forecast shows a "gathering data" state instead of dates.

- **UJ-5. Sarah creates a work item and assigns it to a team.**
  - **Persona + context:** Sarah, PM at Acme Corp. Needs to log a new piece of work.
  - **Entry state:** already signed in.
  - **Path:** clicks "New WorkItem" → enters title "Tighten password reset flow" and an optional description → picks the "Mobile Refresh" Initiative from a dropdown → picks "Growth" Team from a dropdown → saves.
  - **Climax:** the WorkItem exists with status `Backlog` (the default Backlog-category status) and is visible in Growth's team list at the bottom (lowest priority).
  - **Resolution:** Growth's team members see the new item in their list in real time and can pick it up.

## 3. Glossary

*Downstream workflows and readers must use these terms exactly. FRs, UJs, and SMs use Glossary terms verbatim.*

### Aggregates (the core domain entities)

- **User** — A human who can sign in to Velucid. One User record, identified by email. May have many Accounts across many Workspaces. (1:N to Account.)
- **Account** — A User's identity *within a specific Workspace*. Has its own display name, avatar, and workspace role. (N:1 to User, N:1 to Workspace.)
- **Workspace** — The top-level tenant. Contains Accounts, Teams, Initiatives, Labels, and (via the WorkItems owned by its Teams) the work. Workspace names are **globally unique** across Velucid. (1:N to Account, Team, Initiative, Label.)
- **Initiative** — A goal the Workspace wants to achieve. WorkItems belong to exactly one Initiative. (N:1 to Workspace, 1:N to WorkItem.)
- **Team** — A group of TeamMembers. Owns a priority-ordered WorkItem list. (N:1 to Workspace, N:M to Account via TeamMember.)
- **TeamMember** — The join of an Account with a Team. Records that this Account is on this Team. (N:1 to Account, N:1 to Team.) *Renamed from the intake term "Member" to disambiguate from the workspace role `Member` — see Decision Log 2026-06-07 "Roles".*
- **WorkItem** — The unit of work. Has a title, optional description, status, priority (sort position), assigned Team, optional assigned TeamMember, and zero-or-more Labels. Belongs to exactly one Initiative and exactly one Team. (N:1 to Initiative, N:1 to Team, optional N:1 to TeamMember, N:M to Label.)
- **Label** — A workspace-scoped categorization tag. Any Member of the Workspace can create one. (N:1 to Workspace, N:M to WorkItem.)
- **WorkspaceInvitation** — A pending invitation to join a Workspace. Has its own event stream (`workspace-invitation-{uniqueInvitationId}`). Lifecycle: `Pending` → `Accepted` / `Revoked` / `Expired`. On accept, an Account and (optionally) TeamMember records are created. (N:1 to Workspace, N:1 to inviting Account.)

### Status model

- **Status Category** — A grouping of statuses. Six categories exist: `Backlog`, `Unstarted`, `Started`, `Completed`, `Canceled`, `Duplicate`. Each has exactly one default Status in MVP.
- **Status** — The position of a WorkItem in its Team's workflow. Belongs to exactly one Status Category. In MVP, only the six default Statuses exist (one per Category). Adding custom Statuses to a Category (e.g., a `Blocked` status under `Backlog`) is **deferred to post-MVP**.
- **Default statuses (one per category):** `Backlog` (category Backlog), `Todo` (Unstarted), `InProgress` (Started), `Done` (Completed), `Canceled` (Canceled), `Duplicate` (Duplicate).

### Roles

- **Owner** — A workspace-level role on an Account. Can configure the Workspace, configure Teams, and create Initiatives. There is exactly one Owner per Workspace at creation (the user who created it); ownership transfer is **out of scope for MVP**.
- **Member** — A workspace-level role on an Account. Can do everything Owners can do *except* workspace configuration, team configuration, and Initiative creation.

### Other

- **Priority** — The manual ordering of WorkItems within a Team's list. Set by drag-to-reorder on the Team's list view. The highest-priority WorkItem appears at the top. Persisted as a sort position; not derived from a numeric field.
- **Forecast** — A probabilistic projection of when an Initiative's WorkItems will complete. Two flavors, computed independently:
  - **Per-Initiative Forecast** — overall completion curve for an Initiative's WorkItems.
  - **Per-Initiative × Per-Team Forecast** — forecasts each Team's contribution to an Initiative, answering "which team is the bottleneck / who came last."
- **BFF (Back-end For Front-end)** — The Astro 5 server-side component. Sole auth entry point; serves the landing page; proxies BFF-shaped requests from the React SPA to the silo. *Implementation detail; referenced for product-visible behavior only.*
- **Local store** — The React SPA's in-browser cache of the read model. Makes reads instant. Writes are optimistic (reflected locally first) and synced in the background. No offline support in MVP. *Implementation detail.*

---

## 4. Features

*Drafting of §4 happens in two passes (see Decision Log 2026-06-07 "§4 Features drafted in two passes"). Pass 1 covers §4.1 and §4.10 below. Pass 2 covers §4.2–§4.9, §4.11, §4.12 — scope, intent, and FR placeholders are preserved in `addendum.md` so pass 2 can resume without re-discovery.*

### 4.1 Authentication & Identity

**Description.** Velucid owns authentication end-to-end. There is no third-party identity provider. The Astro BFF is the sole auth entry point — the silo and projector never see the login flow. A user signs in by entering their email, receiving a one-time 6-digit code via email, and entering that code. On successful sign-in, a User record is created (if first time), a session is established, and the user lands in either the create-first-workspace flow or the enter-invite-code flow, depending on whether they have a pending invitation. This feature realizes UJ-1 (first sign-in) and UJ-2 (invited sign-in).

**Functional Requirements:**

#### FR-1: Sign-in is email-only; no third-party identity provider

A user can sign in by entering their email address on the sign-in page. Velucid does not integrate with any third-party identity provider (no Auth0, no Google, no GitHub, no SSO). The system supports exactly one auth method: email + one-time code.

**Consequences (testable):**
- The sign-in page exposes exactly one input: an email field.
- No "Sign in with X" buttons exist anywhere in the product.
- The PRD's tech stack explicitly excludes any third-party identity provider.

**Out of Scope:**
- Password-based auth.
- Two-factor / multi-factor.
- Social login.
- SAML / OIDC / enterprise SSO.

#### FR-2: A one-time login code is emailed after email submission

After the user submits their email on the sign-in page, Velucid generates a one-time 6-digit numeric login code, stores it with a short time-to-live (TTL), and emails it to the submitted address via the configured transactional email provider.

**Consequences (testable):**
- The code is exactly 6 digits, numeric.
- The code is valid for at most 15 minutes from issuance (TTL value see §7 Success Metrics).
- Submitting the same email multiple times within a short window does not generate a new code each time (rate-limited).
- The code is stored hashed (or otherwise not in plaintext) at rest.

**Out of Scope:**
- Recovery codes.
- Backup channels (SMS, etc.).

#### FR-3: User completes sign-in by entering the code

The user enters the 6-digit code on a confirmation page. On a correct match within the TTL, the user is signed in: a User record exists (created on first sign-in), a session is established, and the user is redirected to the post-auth landing screen.

**Consequences (testable):**
- Entering the correct code within the TTL establishes a session.
- Entering an incorrect code shows an inline error and allows retry up to a rate limit.
- An expired or already-used code shows a clear "code expired" / "code already used" message and offers to send a new one.
- The session is server-side (or signed cookie) — implementation choice in architecture, not visible in FRs.

**Out of Scope:**
- Multi-device session management UI (a single user can have multiple sessions, but the management UI is out of scope).

#### FR-4: Sign-in is owned by the Astro BFF; the silo and projector never see the login flow

The login flow (email submission, code generation, code verification, session establishment) is implemented entirely in the Astro 5 BFF. The silo and the projector never see login code storage or verification. The React SPA may render login UI but every state-changing call goes through the BFF.

**Consequences (testable):**
- The silo and projector codebases contain no login flow logic.
- All login code storage and verification happens in the BFF (or its backing store).
- The React SPA cannot generate or verify codes directly.

#### FR-5: First-time sign-in creates a User record identified by email

On the first successful sign-in for an email address, Velucid creates a User record. The User is identified by their email address. The email is the User's primary key for sign-in purposes.

**Consequences (testable):**
- A User record exists for every email that has successfully signed in.
- The email is unique across Users (case-insensitive comparison).
- Subsequent sign-ins from the same email do not create duplicate User records.

#### FR-6: Post-auth landing screen has two paths based on pending invitations

After successful sign-in, the user lands on one of two screens:
- **No pending invitation:** "Create your first workspace" prompt. The user must name a globally-unique workspace to proceed.
- **Has pending invitation:** "Enter your invite code" prompt. The user enters the one-time invite code from their email, and is jumped directly to the Workspace they were invited to.

The system never presents both prompts to the same user at the same time — invitations take precedence over the create-first-workspace prompt.

**Consequences (testable):**
- A user with no pending invitations sees the create-first-workspace screen.
- A user with one or more pending invitations sees the enter-invite-code screen.
- After accepting an invitation, the user lands in the Workspace (UJ-2).
- After creating their first workspace, the user lands in that Workspace (UJ-1).

**Out of Scope:**
- Letting a user with a pending invitation skip the invitation and create a new workspace instead (the invitation must be accepted or expired first; explicit "decline" path is post-MVP).

**Feature-specific NFRs:**
- Login code generation, storage, and verification must complete in under 500ms p95.
- Email delivery time is provider-dependent; the UI must show a clear "code sent" state without blocking on delivery confirmation.

**Notes:**
- Code TTL, rate-limit thresholds, and the email provider are operational decisions tracked in §7 / §8 Open Questions.
- The PRD does not commit to a specific transactional email provider (architecture picks; see `architecture.md`).

### 4.2–4.9 Workspaces, Invitations, Accounts, Teams, Initiatives, Labels, WorkItems, Forecasts

*Deferred to drafting pass 2. Scope, intent, known decisions, and FR placeholders are preserved in `addendum.md` §A. Build order within this group is captured in the Decision Log 2026-06-07 "Build order within MVP" entry.*

### 4.10 Local-First & Realtime Sync

**Description.** The React SPA holds a local store of the read model. Reads from the local store are instant — no network round-trip. Writes are optimistic: the local store is updated immediately, and the change is synced to the server in the background. Other team members' changes appear in the local store in real time (transport TBD — the user has indicated the transport is still in development, so the PRD does not name it). There is no offline support in MVP — if the network is unavailable, writes fail with a clear error; reads continue to work from the local store.

This feature is **load-bearing**: per the build order, it is sequenced as the second build step (right after auth) and must be in place before any structural feature (§4.2–§4.9) is built on top of it.

**Functional Requirements:**

#### FR-7: Reads from the local store are instant

Any view in the React SPA that displays data read from the read model (workspaces, teams, work items, etc.) renders from the local store, with no perceptible network wait. Initial load from a cold local store may involve a single network fetch, but after the local store is populated, no subsequent read blocks on the network.

**Consequences (testable):**
- Switching between any two views within a Workspace does not trigger a loading spinner from network reads.
- All UI elements that show list data (team lists, work item lists, etc.) re-render from the local store on user interaction, with no perceptible delay.

#### FR-8: Writes are optimistic — UI reflects the change before the server confirms

When a user makes a change (create work item, reorder list, change status, etc.), the local store is updated immediately and the UI re-renders. The change is then sent to the server in the background. If the server rejects the change, the local store is reverted and the user is shown an error.

**Consequences (testable):**
- All write actions reflect in the UI within one frame of the user action.
- Successful server confirmation is silent (no toast, no confirmation modal).
- Server rejection reverts the local change and shows a clear error.

#### FR-9: Teammate changes appear in the UI in real time

When another team member makes a change to a shared entity (work item, team list, initiative, etc.), the change appears in the local store of the current user without a refresh, and the UI re-renders to reflect it.

**Consequences (testable):**
- A change made by Account A on a work item appears in the UI of Account B viewing the same team list within seconds (or as fast as the real-time transport allows).
- The change is reflected in the same UI element the other user is interacting with (e.g., the work item moves in both users' lists in real time).

**Out of Scope:**
- Presence indicators (showing which users are currently viewing what).
- Optimistic locking UX (the system resolves conflicts server-side; users don't see conflict-resolution prompts).

#### FR-10: No offline write support — writes fail clearly when the network is unavailable

If the user is offline (or the network is otherwise unavailable), the app does not attempt to queue writes for later. Writes that cannot be sent to the server fail immediately with a clear error message, and the local change is reverted.

**Consequences (testable):**
- With no network, the user can still read from the local store.
- With no network, any write action shows a "no connection" error and the local change is reverted.
- No "queued for later" state exists.

**Out of Scope:**
- Offline write support.
- Conflict resolution at write time (writes that succeed server-side are the only ones that persist).

**Feature-specific NFRs:**
- Time from user action to UI update (optimistic): < 16ms (one frame).
- Time from teammate action to local UI update (realtime): < 2s p95.
- Local store size: must accommodate a Workspace with 1,000 WorkItems across 10 teams with no perceptible slowdown.

**Notes:**
- The real-time transport (WebSocket, SSE, or other) is an architecture decision — the user has indicated the chosen transport is still in development. The PRD specifies the *behavior*; architecture specifies the *mechanism*.
- The local store is read-only from the user's perspective; the user cannot export, import, or directly manipulate it.
- Conflict resolution at write time is server-side; the PRD does not describe a user-facing conflict resolution UX because there is none in MVP.

### 4.11 Notifications (email), 4.12 Cross-Cutting NFRs

*Deferred to drafting pass 2. Scope, intent, known decisions, and FR placeholders are preserved in `addendum.md` §A.*

---

*Drafting continues in the next pass: §5 Non-Goals, §6 MVP Scope (with Build Order subsection), §7 Success Metrics, §8 Open Questions, §9 Assumptions Index. §4 pass 2 (Features §4.2–§4.9, §4.11, §4.12) is paused until the user signals readiness.*
