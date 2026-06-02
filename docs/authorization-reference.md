# Domain Command Reference

This document maps every write operation (command) in Velucid to its events, authorization rules, and implementation status. It follows CQRS — commands mutate state by emitting events; queries read state without events.

**How to read this document:**
- **Command** — what the user wants to do (maps to a grain method)
- **Event** — what gets recorded (maps to an event class persisted to KurrentDB)
- **Who** — which roles are authorized (determined by OpenFGA permissions)
- **Status** — implemented, planned, or not started

## Concepts

| Concept | Description |
|---------|-------------|
| **Command** | A write operation requested by a user. Produces one or more events. |
| **Event** | An immutable fact recorded in the event store. Events are the source of truth. |
| **Query** | A read operation. No events produced. No authorization at grain level (controller handles access control). |
| **Role** | A user's relationship to a resource: `owner` or `member` (per-organization). |
| **Permission** | An OpenFGA-defined right derived from role. e.g. `delete_org` requires `owner` relation. |

## Roles

| Role | How Assigned | Scope |
|------|-------------|-------|
| **Owner** | Created the org, or promoted by another owner | Per-organization |
| **Member** | Added by an owner | Per-organization |

## Authorization Flow

```
User sends command
       |
       v
  1. Domain validation       Does the resource exist? Is it deleted? Duplicate check?
       |
       v
  2. Authorization check     Can this user perform this action?
       |                     (OpenFGA Check — throws UnauthorizedAccessException if denied)
       v
  3. Business logic          Emit event → persist to KurrentDB → apply to in-memory state
       |
       v
  4. Projector sync          OrgProjector writes/deletes OpenFGA tuples on membership events
```

---

## User Commands

User commands have no organization-level authorization. Any authenticated user can manage their own profile.

| Command | Event | Permission | Who | Status |
|---------|-------|------------|-----|--------|
| Create User | `UserCreated` | — | Any authenticated user | Done |
| Link Identity | `IdentityLinked` | — | Self | Done |
| Update Profile | `UserProfileUpdated` | — | Self | Done |
| Request Email Verification | `EmailVerificationRequested` | — | Self | Done |
| Verify Email | `EmailVerified` | — | Self (token-based) | Done |

## Organization Commands

All org commands require the user to be a member of the organization. Authorization is enforced via OpenFGA.

| Command | Event | OpenFGA Permission | Owner | Member | Non-Member | Status |
|---------|-------|--------------------|:-----:|:------:|:----------:|--------|
| Create Org | `OrgCreated` | — (anyone can create) | Yes | Yes | Yes | Done |
| Rename Org | `OrgRenamed` | `manage_org_settings` | Yes | No | No | Done |
| Delete Org | `OrgDeleted` | `delete_org` | Yes | No | No | Done |
| Add Member | `MemberAdded` | `invite_member` | Yes | No | No | Done |
| Remove Member | `MemberRemoved` | `remove_member` | Yes | No | No | Done |
| Send Invitation | `InvitationSent` | `invite_member` | Yes | No | No | Done |

### Key Behaviors

- **Create Org** — no auth check; any user can create. Creator becomes owner automatically.
- **Owner removal protection** — an owner cannot be removed from their own org.
- **Idempotent membership** — adding an existing member or removing a non-member is silently ignored.
- **Role validation** — only `Owner` and `Member` are valid roles.
- **Soft delete** — deleting an org marks it deleted; further commands are blocked.
- **Access tightening note** — `Add Member` and `Send Invitation` were tightened from any-member to owner-only during the OpenFGA migration (story 3.2).

### OpenFGA Tuple Sync

The OrgProjector syncs membership tuples on these events:

| Event | OpenFGA Action | Tuple |
|-------|---------------|-------|
| `OrgCreated` | Write | `user:{ownerId}` — owner → `organization:{orgId}` |
| `MemberAdded` | Write | `user:{userId}` — role → `organization:{orgId}` |
| `MemberRemoved` | Delete | `user:{userId}` — role → `organization:{orgId}` |

### Organization Queries (no events, no grain-level auth)

| Query | Returns | Notes |
|-------|---------|-------|
| Get Org Info | `OrgInfo(orgId, name, isDeleted)` | Throws if org doesn't exist |
| Get Members | `IReadOnlyList<OrgMemberInfo>` | All members with roles |
| Is Member | `bool` | Returns false for non-existent orgs |

---

## Product Commands (Planned)

| Command | Event | OpenFGA Permission | Owner | Member | Status |
|---------|-------|--------------------|:-----:|:------:|--------|
| Create Product | `ProductCreated` | `create_product` | Yes | No | Not started |
| Rename Product | `ProductRenamed` | `manage_org_settings` | Yes | No | Not started |
| Delete Product | `ProductDeleted` | `delete_product` | Yes | No | Not started |

## Task Commands (Planned)

| Command | Event | OpenFGA Permission | Owner | Member | Status |
|---------|-------|--------------------|:-----:|:------:|--------|
| Create Task | `TaskCreated` | `create_task` | Yes | Yes | Not started |
| Update Task | `TaskUpdated` | `create_task` | Yes | Yes | Not started |
| Delete Task | `TaskDeleted` | `create_task` | Yes | Yes | Not started |
| Move Task (status change) | `TaskMoved` | `create_task` | Yes | Yes | Not started |

## Forecast Commands (Planned)

| Command | Event | OpenFGA Permission | Owner | Member | Status |
|---------|-------|--------------------|:-----:|:------:|--------|
| Run Forecast | `ForecastStarted` | `view_org` | Yes | Yes | Not started |
| Configure Forecast | `ForecastConfigured` | `manage_org_settings` | Yes | No | Not started |

---

## OpenFGA Authorization Model

The full permission model defined in `velucid-auth-model.fga`:

```
type organization
  relations
    define owner as self
    define member as self

    define view_org          as owner or member
    define view_members      as owner or member
    define create_task       as owner or member
    define create_product    as owner
    define delete_product    as owner
    define invite_member     as owner
    define change_member_role as owner
    define remove_member     as owner
    define delete_org        as owner
    define manage_org_settings as owner
```

**Planned extensions:** Product and Task resources will be added as new types with their own relations and permissions, scoped to the parent organization.
