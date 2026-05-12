# Task 03: PostgreSQL Read Model Schema & Migrations

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 03 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 1 day |

## Description

Create the PostgreSQL read model schema for Epic 1 projections. This includes all projection tables and a migration framework that can be run against the `velucid_readmodel` database in the Kubernetes cluster or locally. Note: KurrentDB persistent subscriptions handle checkpointing internally, so no `projection_checkpoint` table is needed.

## Architecture Reference

- Architecture doc Section 7 (Read Model - PostgreSQL Projections)
- Architecture doc Section 7.1 (Entity Relationship Diagram)
- Architecture doc Section 7.2 (SQL Schema)
- Architecture doc Section 6.5 (KurrentDB Persistent Subscriptions) — projectors subscribe directly to KurrentDB persistent subscriptions. KurrentDB handles checkpointing internally; no `projection_checkpoint` table is needed.
- Architecture doc Section 4.1 (Silo Configuration) — Orleans clustering uses the same PostgreSQL instance via `Orleans.Clustering.AdoNet`.

## Technical Requirements

### Migration Framework
- Use Entity Framework Core with Npgsql provider for migrations.
- Migrations are code-first (C# migration classes), versioned, and idempotent.
- Migrations run as part of the projector-service startup or as a standalone console app.
- Connection string sourced from environment variable or config.

### Tables to Create

#### `user_projection`
```sql
CREATE TABLE user_projection (
    user_id           UUID PRIMARY KEY,
    display_name      TEXT NOT NULL,
    avatar_url        TEXT,
    email             TEXT,
    is_email_verified BOOLEAN NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMPTZ NOT NULL,
    updated_at        TIMESTAMPTZ NOT NULL
);
```

#### `user_identity`
```sql
CREATE TABLE user_identity (
    user_id       UUID NOT NULL REFERENCES user_projection(user_id),
    provider_id   TEXT NOT NULL,       -- Auth0 subject (e.g., "github|12345678")
    provider_name TEXT NOT NULL,       -- e.g., "github", "google", "microsoft"
    email         TEXT,                -- Email from this provider (nullable — providers may not return email)
    linked_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, provider_id)
);
CREATE UNIQUE INDEX idx_user_identity_provider ON user_identity(provider_id);
CREATE INDEX idx_user_identity_email ON user_identity(email) WHERE email IS NOT NULL;
```

#### `org_projection`
```sql
CREATE TABLE org_projection (
    org_id        UUID PRIMARY KEY,
    name          TEXT NOT NULL,
    is_deleted    BOOLEAN NOT NULL DEFAULT FALSE,
    created_at    TIMESTAMPTZ NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL
);
```

#### `org_member_projection`
```sql
CREATE TABLE org_member_projection (
    org_id        UUID NOT NULL REFERENCES org_projection(org_id),
    user_id       UUID NOT NULL REFERENCES user_projection(user_id),
    role          TEXT NOT NULL CHECK (role IN ('Owner', 'Member')),
    joined_at     TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (org_id, user_id)
);
CREATE INDEX idx_org_member_projection_user ON org_member_projection(user_id);
```

#### `org_invitation_projection`
```sql
CREATE TABLE org_invitation_projection (
    org_id            UUID NOT NULL REFERENCES org_projection(org_id),
    email             TEXT NOT NULL,
    role              TEXT NOT NULL CHECK (role IN ('Owner', 'Member')),
    status            TEXT NOT NULL CHECK (status IN ('Pending', 'Accepted', 'Declined')),
    invited_at        TIMESTAMPTZ NOT NULL,
    user_id           UUID,
    PRIMARY KEY (org_id, email)
);
CREATE INDEX idx_org_invitation_projection_email ON org_invitation_projection(email, status);
```

#### `user_org_projection`
```sql
CREATE TABLE user_org_projection (
    user_id       UUID NOT NULL REFERENCES user_projection(user_id),
    org_id        UUID NOT NULL REFERENCES org_projection(org_id),
    role          TEXT NOT NULL,
    PRIMARY KEY (user_id, org_id)
);
```

### File Structure
```
src/
  Velucid.ReadModel/
    Entities/
      UserProjection.cs
      UserIdentity.cs
      OrgProjection.cs
      OrgMemberProjection.cs
      OrgInvitationProjection.cs
      UserOrgProjection.cs
    ReadModelDbContext.cs
  Velucid.ReadModel.Migrations/
    Program.cs
    DesignTimeDbContextFactory.cs
    Migrations/
      InitialCreate.cs
      RemoveProjectionCheckpoint.cs
    Velucid.ReadModel.Migrations.csproj
```

## Acceptance Criteria

- [x] All 6 tables are created via EF Core migrations against a fresh PostgreSQL database.
- [x] Migrations are idempotent (running twice does not fail).
- [x] CHECK constraints on `role` and `status` columns are enforced.
- [x] All foreign key relationships are correct.
- [x] All required indexes exist, including unique index on `user_identity(provider_id)` and email index for auto-linking.
- [x] Migration tool can be run from command line: `dotnet run -- "Host=...;Database=velucid_readmodel;..."`.
- [x] `projection_checkpoint` table is NOT created — KurrentDB handles checkpointing internally.
- [ ] Future epics can add `product_projection`, `task_projection`, `task_tag_projection`, and `cumulative_flow_snapshot` tables.

## Dependencies

- Task 01 (Kubernetes Infrastructure) -- for the PostgreSQL StatefulSet to be running.
- Can develop and test locally with Docker PostgreSQL in parallel.

## Notes

- Migrations are code-first (EF Core) with Npgsql provider — no raw SQL migration files needed.
- Future epics will add `product_projection`, `task_projection`, `task_tag_projection`, and `cumulative_flow_snapshot` tables. New migrations can be added following the same pattern.
- The `user_org_projection` is a denormalized reverse index. It is maintained by the projector alongside `org_member_projection` to enable fast "my organizations" queries without JOINs.
- **Orleans clustering tables** (`OrleansMembershipTable`, `OrleansMembershipVersionTable`) are auto-created by the `Orleans.Clustering.AdoNet` provider on first silo startup. No manual migration is needed for these tables — they are managed entirely by Orleans in the same PostgreSQL database.
