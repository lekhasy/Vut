# Addendum — Velucid PRD (2026-06-07 redesign)

This addendum preserves content that does not fit (yet) in the main PRD narrative. It is captured during the drafting conversation per the bmad-prd skill convention.

---

## A. §4.2–§4.12 Drafting Notes (deferred per user instruction)

Per user instruction on 2026-06-07, the §4 Features section of the PRD is being drafted in **two passes**. **Pass 1** (this turn) covers §4.1 (Authentication & Identity) and §4.10 (Local-First & Realtime Sync) only. **Pass 2** (deferred until the user signals readiness) covers §4.2–§4.9, §4.11, and §4.12.

This addendum preserves the **scope, intent, known decisions, and FR placeholders** for every deferred feature so pass 2 can resume without re-discovery. Where the placeholder FRs need to be promoted to full FRs, the drafting pass will use the addendum as the source of truth.

### Build order within MVP (recap from Decision Log 2026-06-07)

1. **§4.1 Authentication & Identity** — pass 1
2. **§4.10 Local-First & Realtime Sync** — pass 1
3. **§4.2 Workspaces, §4.3 Workspace Invitations, §4.4 Accounts & Roles, §4.5 Teams & TeamMembers, §4.6 Initiatives** — pass 2
4. **§4.8 WorkItems** — pass 2 (built before §4.7 by explicit user call)
5. **§4.7 Labels** — pass 2 (deprioritized per user call)
6. **§4.9 Forecasts** — pass 2 (last in MVP; the math is in `docs/velucid_forecasting_spec.md` and unchanged in this pass)
7. **§4.11 Notifications (email)** — pass 2 (rides on the OTP transactional email provider)

### §4.2 Workspaces (deferred)

**Scope.** A User with no Workspace (or who chose to create a new one) can create a Workspace by giving it a name. Workspace names are **globally unique** across all of Velucid. The user who creates the Workspace becomes its **Owner**. In MVP, the only configurable field is the name.

**User flow (realizes UJ-1 climax).** Owner types a name → uniqueness check → if available, Workspace is created → user is jumped into it.

**Known decisions.**
- Globally unique names (Decision Log 2026-06-07 "Onboarding").
- One Owner per Workspace at creation; ownership transfer is out of scope for MVP.

**FR placeholders to draft in pass 2.**
- FR-W1: Owner can create a Workspace by giving it a name.
- FR-W2: Workspace names are globally unique; the UI shows an inline error on collision.
- FR-W3: The creator of a Workspace becomes its Owner.
- FR-W4: A User can be an Account in multiple Workspaces.
- FR-W5: An Owner can rename a Workspace.

**Out of scope.** Logo, description, deletion, ownership transfer.

### §4.3 Workspace Invitations (deferred)

**Scope.** A Workspace Owner can invite people by email. The invitation is a first-class aggregate (**WorkspaceInvitation**) with its own event stream (`workspace-invitation-{uniqueInvitationId}`). Invitations do not appear in the Workspace's event stream. Lifecycle: `Pending` → `Accepted` / `Revoked` / `Expired`.

**User flow (invite side, UJ-2 covers accept side).** Owner clicks "Invite people" → enters email → chooses one or more Teams (optional) → clicks Invite → system sends an invite email with a one-time code/link → invitee signs in → on enter-code, Account is created in the Workspace and (if Teams were specified) TeamMember records are created.

**Known decisions.**
- One stream per invitation (not per Workspace) — Decision Log 2026-06-07 "New aggregate".
- Team assignment during invite is optional.
- Acceptance creates the Account and (optionally) TeamMember records.

**FR placeholders to draft in pass 2.**
- FR-I1: Owner can invite a person to a Workspace by entering their email and optionally selecting Teams.
- FR-I2: An invite email with a one-time code is sent to the entered address.
- FR-I3: An invitee with a pending invitation, after sign-in, sees the "Enter invite code" prompt.
- FR-I4: Entering the correct code creates the Account in the Workspace and (if Teams were specified) TeamMember records for those Teams.
- FR-I5: Owner can revoke a pending invitation.
- FR-I6: Invitations expire after a configurable TTL (operational NFR — default TBD; see Open Questions).

**Out of scope.** Bulk invite, custom invitation message, resending an invitation, decline path (invitees either accept or let it expire).

### §4.4 Accounts & Roles (deferred)

**Scope.** An Account is a User's identity within a Workspace. Fields: display name, avatar, role. Two roles: **Owner** and **Member**. Owner-only actions: workspace configuration, team configuration, initiative creation. Members can do everything else.

**Known decisions.**
- Roles are flat (no admin / moderator tiers) — Decision Log 2026-06-07 "Roles".
- Account fields are minimal in MVP (Decision Log 2026-06-07 "Account fields: intentionally minimal in MVP").
- One Owner per Workspace at creation; ownership transfer is out of scope.

**FR placeholders to draft in pass 2.**
- FR-A1: An Account is created when a User first has a presence in a Workspace (create-first-workspace or invitation accept).
- FR-A2: The creator of a Workspace becomes its Owner.
- FR-A3: An Account has a display name, avatar, and role.
- FR-A4: An Owner can configure the Workspace, configure Teams, and create Initiatives.
- FR-A5: A Member can do everything Owners can except workspace configuration, team configuration, and Initiative creation.
- FR-A6: A User can view all Accounts in a Workspace they are a Member of.

**Out of scope.** Account deletion, role change, profile fields beyond display name + avatar, ownership transfer.

### §4.5 Teams & TeamMembers (deferred)

**Scope.** A Team is a group of TeamMembers, owned by a Workspace. A Team has a name and an ordered WorkItem list (priority order). A TeamMember is the join of an Account with a Team. An Account can be a TeamMember of many Teams; a Team has many Accounts.

**Known decisions.**
- TeamMember is the join record (renamed from "Member" in the intake to disambiguate from the role) — Decision Log 2026-06-07 "Roles".
- Teams have a priority-ordered WorkItem list, not a kanban board — Decision Log 2026-06-07 "UX: priority list with drag-to-reorder".

**FR placeholders to draft in pass 2.**
- FR-T1: Owner can create a Team in a Workspace.
- FR-T2: Owner can rename a Team.
- FR-T3: Owner can add a TeamMember to a Team.
- FR-T4: Owner can remove a TeamMember from a Team.
- FR-T5: A Member can view all Teams in a Workspace (read access is workspace-wide).
- FR-T6: An Account can view the Teams they are a TeamMember of.

**Out of scope.** Team deletion, team-level roles (Team Lead, etc.), per-team configuration beyond name.

### §4.6 Initiatives (deferred)

**Scope.** An Initiative is a goal a Workspace wants to achieve. Fields: name, optional description. An Initiative is created by an Owner. WorkItems belong to exactly one Initiative.

**Known decisions.**
- Only Owner can create Initiatives — Decision Log 2026-06-07 "Roles".

**FR placeholders to draft in pass 2.**
- FR-N1: Owner can create an Initiative by giving it a name and optional description.
- FR-N2: A Member can view all Initiatives in a Workspace.
- FR-N3: An Owner can rename an Initiative.
- FR-N4: An Owner can delete an Initiative (cascade semantics TBD — see Open Questions).

**Out of scope.** Initiative status (active / completed), initiative owner (separate from Workspace Owner), target date / deadline.

### §4.7 Labels (deferred — built after §4.8 WorkItems, before §4.9 Forecasts)

**Scope.** A Label is a workspace-scoped categorization tag. Fields: name, optional color. Any Member of the Workspace can create a Label. A WorkItem can have zero or more Labels.

**Known decisions.**
- Deprioritized — built after WorkItems (Decision Log 2026-06-07 "Build order").
- Forecast is NOT parameterized by Labels (it was in the old model) — Decision Log 2026-06-07 "Forecast".

**FR placeholders to draft in pass 2.**
- FR-L1: Any Member can create a Label in the Workspace.
- FR-L2: A Label has a name and an optional color.
- FR-L3: A WorkItem can have one or more Labels applied.
- FR-L4: A Member can remove a Label from a WorkItem.
- FR-L5: Labels are visible on the WorkItem's display.

**Out of scope.** Label namespaces (no `namespace:value` format), label-based filtering on the team list, label renaming/deletion (TBD).

### §4.8 WorkItems (deferred — built after §4.2–§4.6, before §4.7 Labels)

**Scope.** A WorkItem is the unit of work. Fields: title, optional description, status, priority (sort position), assigned Team, optional assigned TeamMember, zero-or-more Labels. Belongs to one Initiative and one Team. Status is one of six default statuses (one per category): `Backlog`, `Todo`, `InProgress`, `Done`, `Canceled`, `Duplicate`. Priority is the sort position in the Team's list, set by drag-to-reorder; highest priority on top.

**User flows.** UJ-3 (reorder), UJ-5 (create). Status changes by any Member, any-to-any status. No archive — to "stop work", change status to `Canceled`.

**Known decisions.**
- Statuses defined per Team, six default Status Categories with one default status each — Decision Log 2026-06-07 "WorkItem status".
- Adding custom statuses to a category is post-MVP.
- No archive, no hard delete in MVP — Decision Log 2026-06-07 "No WorkItem archive".
- Priority is drag-to-reorder, not a numeric field.

**FR placeholders to draft in pass 2.**
- FR-WI1: A Member can create a WorkItem by giving it a title and picking an Initiative and a Team.
- FR-WI2: A WorkItem has a title, optional description, status, priority, and belongs to one Initiative and one Team.
- FR-WI3: A WorkItem can be assigned to one TeamMember (optional).
- FR-WI4: A WorkItem's status can be changed to any of the six default statuses.
- FR-WI5: A WorkItem's priority (position in the Team's list) can be changed by drag-to-reorder.
- FR-WI6: A Member can edit a WorkItem's title and description.
- FR-WI7: A WorkItem can have zero or more Labels applied.
- FR-WI8: A WorkItem's status changes are recorded as events (feed the forecast).

**Out of scope.** Deletion, archiving, comments, activity feed, attachments, due dates, subtasks/checklists, custom Status definitions.

### §4.9 Forecasts (deferred — built LAST in MVP)

**Scope.** Two independent forecast flavors, both Monte Carlo on observed WorkItem flow:
- **Per-Initiative Forecast** — overall completion curve for an Initiative's WorkItems.
- **Per-Initiative × Per-Team Forecast** — forecasts each Team's contribution to an Initiative, answering "which team is the bottleneck / who came last."

Both are read-only. Both are parameterized by **Initiative** (not by tags — tags are gone from the forecast model).

**User flow (realizes UJ-4 climax).** Marcus opens the Mobile Refresh Initiative, clicks Forecast tab, sees stat cards with 50/70/95% confidence dates, adjusts threshold slider, switches to per-Team view, sees Growth is the bottleneck.

**Known decisions.**
- Forecast is in MVP — Decision Log 2026-06-07 "Forecast".
- Per-Initiative and Per-Initiative×Per-Team, computed independently.
- Algorithm unchanged from `docs/velucid_forecasting_spec.md` (Tag-based filter removed, math carries over).
- Forecast is built LAST in MVP.

**FR placeholders to draft in pass 2.**
- FR-F1: A Member can view the Per-Initiative Forecast for any Initiative they have access to.
- FR-F2: The forecast shows 50% / 70% / 95% confidence dates as stat cards.
- FR-F3: A threshold slider (50–99%, default 70%) updates the displayed dates.
- FR-F4: A Member can view the Per-Initiative × Per-Team Forecast for any Initiative.
- FR-F5: The per-Team view shows each Team's forecast completion and contribution to the Initiative.
- FR-F6: Initiatives with fewer than 7 days of history show a "gathering data" state.
- FR-F7: The forecast is read-only; no actions are taken from it.

**Out of scope.** Tag-based filtering on the forecast (the old model had this; the new model does not), forecast export / sharing, forecast notifications, per-WorkItem forecast contribution.

**Notes.** The math, algorithm, and chart rendering are owned by `docs/velucid_forecasting_spec.md` and are NOT in the PRD. The PRD describes the user-visible behavior; the spec describes the mechanism.

### §4.11 Notifications — Email (deferred)

**Scope.** Email-only in MVP. No in-app notification center, no notification bell, no unread counts. Uses the same transactional email provider as the OTP login code.

**Triggers in MVP.**
- WorkspaceInvitation email (covered in §4.3).
- WorkItem assignment to a TeamMember (the assignee gets an email).

**Known decisions.**
- Email-only (Decision Log 2026-06-07 "Notifications: email-only in MVP").

**FR placeholders to draft in pass 2.**
- FR-N1: When a WorkItem is assigned to a TeamMember, an email is sent to the assignee.
- FR-N2: Notification emails are sent via the same transactional email provider as OTP codes.

**Out of scope.** In-app notification center, mentions, daily digests, notification preferences, SMS / push.

### §4.12 Cross-Cutting NFRs (deferred)

**Likely NFR clusters.**
- **Security & tenancy isolation.** All reads scoped to the user's Workspaces; cross-tenant access impossible; auth enforced at the BFF.
- **Eventual consistency.** Read model lags writes; optimistic UI masks the lag (covered in §4.10).
- **Performance budgets.** Team list load, create WorkItem latency, forecast recompute time — specific numbers TBD with the user in pass 2.
- **Accessibility.** WCAG 2.1 AA color contrast; keyboard-navigable drag-to-reorder; ARIA labels.
- **Auditability.** Every state change is an event; the event log is the source of truth.
- **Self-hostable.** Entire stack runs on the team's own machines; no cloud provider dependency in MVP.
- **Transport.** HTTPS only.
- **Event hygiene.** No authentication tokens or sensitive credentials in events.

**FR placeholders to draft in pass 2.** (Specific numbers filled in with the user.)
- NFR-S1: All Workspace data access is tenant-scoped.
- NFR-S2: All transport is over TLS.
- NFR-S3: Events do not contain credentials.
- NFR-P1, NFR-P2, …: performance budgets (TBD).
- NFR-A1, NFR-A2, …: accessibility (TBD).

**Out of scope.** SOC 2 / HIPAA / regulated-industry compliance; multi-region deployment.
