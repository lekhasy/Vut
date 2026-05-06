# Task 03: PostgreSQL Read Model Schema & Migrations

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 03 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 1 day |

## Description

Create the PostgreSQL read model schema for Epic 1 projections. This includes all projection tables, the checkpoint table for idempotent projector consumers, and a migration framework that can be run against the `vut_readmodel` database in the Kubernetes cluster or locally.

## Architecture Reference

- Architecture doc Section 7 (Read Model - PostgreSQL Projections)
- Architecture doc Section 7.1 (Projection Views - full SQL DDL)
- Architecture doc Section 7.2 (Projector Service Design - checkpoint table)

## Technical Requirements

### Migration Framework
- Use a .NET-compatible migration tool (recommend FluentMigrator or DbUp).
- Migrations must be idempotent and versioned.
- Migrations run as part of the projector-service startup or as a standalone console app.
- Connection string sourced from environment variable or config.

### Tables to Create

#### `user_projection`
```sql
CREATE TABLE user_projection (
    user_id       UUID PRIMARY KEY,
    provider_id   TEXT NOT NULL UNIQUE,
    display_name  TEXT NOT NULL,
    avatar_url    TEXT,
    created_at    TIMESTAMPTZ NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL
);
CREATE INDEX idx_user_projection_provider ON user_projection(provider_id);
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

#### `projection_checkpoint`
```sql
CREATE TABLE projection_checkpoint (
    projector_name   TEXT NOT NULL,
    topic            TEXT NOT NULL,
    partition_id     INT NOT NULL,
    last_offset      BIGINT NOT NULL,
    updated_at       TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (projector_name, topic, partition_id)
);
```

### File Structure
```
src/
  Vut.ReadModel.Migrations/
    Program.cs
    Scripts/
      001_create_user_projection.sql
      002_create_org_projection.sql
      003_create_org_member_projection.sql
      004_create_org_invitation_projection.sql
      005_create_user_org_projection.sql
      006_create_projection_checkpoint.sql
    Vut.ReadModel.Migrations.csproj
```

## Acceptance Criteria

- [ ] All 6 tables are created by running migrations against a fresh PostgreSQL database.
- [ ] Migrations are idempotent (running twice does not fail).
- [ ] CHECK constraints on `role` and `status` columns are enforced.
- [ ] All foreign key relationships are correct.
- [ ] All required indexes exist.
- [ ] Migration tool can be run from command line: `dotnet run --connection "Host=...;Database=vut_readmodel;..."`.
- [ ] `projection_checkpoint` table has the correct composite primary key.

## Dependencies

- Task 01 (Kubernetes Infrastructure) -- for the PostgreSQL StatefulSet to be running.
- Can develop and test locally with Docker PostgreSQL in parallel.

## Notes

- The migration scripts should be SQL-first (raw .sql files) so they can be reviewed and audited easily.
- Future epics will add `product_projection`, `task_projection`, `task_tag_projection`, and `cumulative_flow_snapshot` tables. Design the migration numbering to accommodate this.
- The `user_org_projection` is a denormalized reverse index. It is maintained by the projector alongside `org_member_projection` to enable fast "my organizations" queries without JOINs.
