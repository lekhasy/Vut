# Task 07: Projector Service Implementation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 07 |
| **Priority** | P1 |
| **Estimated Effort** | 2.5 days |

## Description

Implement the .NET Projector Service that subscribes to KurrentDB persistent subscriptions and updates PostgreSQL read model projections. This service is the bridge between the event store (source of truth) and the read model (query-optimized views). Projectors subscribe directly to KurrentDB persistent subscriptions — no Redpanda/Kafka message broker in the projection path. The service must be idempotent and resilient to restarts.

## Architecture Reference

- Architecture doc Section 7.3 (Projector Service Design)
- Architecture doc Section 7.1 (Entity Relationship Diagram)
- Architecture doc Section 7.2 (SQL Schema)
- Architecture doc Section 6.5 (KurrentDB Persistent Subscriptions)

## Technical Requirements

### Solution Structure
```
src/
  Vut.ProjectorService/
    Program.cs
    Vut.ProjectorService.csproj
    Configuration/
      KurrentDbOptions.cs
      PostgresOptions.cs
    Handlers/
      UserProjector.cs
      OrgProjector.cs
    Database/
      DbConnectionFactory.cs
```

### Service Lifecycle
- Runs as a .NET Worker Service (`BackgroundService`).
- On startup: subscribe to KurrentDB persistent subscriptions for `user-*` and `organization-*` streams.
- Subscription groups:
  - `vut-projector-user` — stream filter: `user-*` (all User stream events)
  - `vut-projector-org` — stream filter: `organization-*` (all Organization stream events)
- On each consumed event: deserialize the event envelope, route to the correct handler, update PostgreSQL, ack the event.
- KurrentDB handles checkpointing internally via persistent subscriptions — no separate checkpoint table needed.
- On shutdown: acknowledge final position gracefully.

### User Projector (`UserProjector`)

| Event | Action |
|-------|--------|
| `UserCreated` | `INSERT INTO user_projection (user_id, display_name, avatar_url, email, is_email_verified, created_at, updated_at)` — `email` may be null |
| `IdentityLinked` | `INSERT INTO user_identity (user_id, provider_id, provider_name, email, linked_at)` — `email` may be null |
| `UserProfileUpdated` | `UPDATE user_projection SET display_name = @displayName, avatar_url = @avatarUrl, updated_at = @timestamp WHERE user_id = @userId` |
| `EmailVerified` | `UPDATE user_projection SET is_email_verified = TRUE, email = @email, updated_at = @timestamp WHERE user_id = @userId` |

All operations must be idempotent:
- `INSERT INTO user_projection` uses `ON CONFLICT (user_id) DO UPDATE SET ...` pattern.
- `INSERT INTO user_identity` uses `ON CONFLICT (user_id, provider_id) DO UPDATE SET ...` pattern.
- `UPDATE` uses `WHERE user_id = @userId` and is naturally idempotent.

### Org Projector (`OrgProjector`)

| Event | Action |
|-------|--------|
| `OrganizationCreated` | `INSERT INTO org_projection (org_id, name, created_at, updated_at)` |
| `OrganizationRenamed` | `UPDATE org_projection SET name = @newName, updated_at = @timestamp WHERE org_id = @orgId` |
| `MemberInvited` | `INSERT INTO org_invitation_projection (org_id, email, role, status, invited_at)` with `ON CONFLICT DO UPDATE` |
| `MemberJoined` | 1) `INSERT INTO org_member_projection (org_id, user_id, role, joined_at)` with `ON CONFLICT DO UPDATE` 2) `INSERT INTO user_org_projection (user_id, org_id, role)` with `ON CONFLICT DO UPDATE` 3) `UPDATE org_invitation_projection SET status = 'Accepted', user_id = @userId WHERE org_id = @orgId AND email = @email` |
| `MemberRemoved` | 1) `DELETE FROM org_member_projection WHERE org_id = @orgId AND user_id = @userId` 2) `DELETE FROM user_org_projection WHERE user_id = @userId AND org_id = @orgId` |
| `MemberRoleChanged` | 1) `UPDATE org_member_projection SET role = @newRole WHERE org_id = @orgId AND user_id = @userId` 2) `UPDATE user_org_projection SET role = @newRole WHERE user_id = @userId AND org_id = @orgId` |

Idempotency rules:
- All INSERTs use `ON CONFLICT ... DO UPDATE`.
- DELETEs are naturally idempotent (deleting a non-existent row is a no-op).
- All operations within a single event should be wrapped in a PostgreSQL transaction.

### Database Connection
- Use `Npgsql` for PostgreSQL connectivity.
- Connection string from configuration or environment variable.
- Use connection pooling (configure min/max pool size).

### KurrentDB Persistent Subscription Configuration
- Use EventStoreDB GRPC client NuGet package.
- Consumer group `vut-projector-user` subscribing to stream `user-*`.
- Consumer group `vut-projector-org` subscribing to stream `organization-*`.
- Subscription uses `StreamRevision.Current` (start from live, replay existing on first run).
- Manual ack after successful projection — KurrentDB tracks positions internally.
- Subscribe in a loop with cancellation token support for graceful shutdown.

### Dockerfile
- Multi-stage build similar to actor-service.
- Expose no ports (this is a background worker, not a service).
- Output image: `vut/projector-service`.

## Acceptance Criteria

- [ ] Projector service starts and subscribes to both KurrentDB persistent subscription groups.
- [ ] Consumed events are correctly projected into PostgreSQL tables.
- [ ] `UserCreated` creates a row in `user_projection`.
- [ ] `IdentityLinked` creates a row in `user_identity`.
- [ ] `EmailVerified` updates `is_email_verified` and `email` in `user_projection`.
- [ ] `OrganizationCreated` creates a row in `org_projection`.
- [ ] `MemberJoined` creates rows in `org_member_projection`, `user_org_projection`, and updates `org_invitation_projection`.
- [ ] `MemberRemoved` deletes from both `org_member_projection` and `user_org_projection`.
- [ ] `MemberRoleChanged` updates both member tables.
- [ ] Restarting the projector resumes from the last acknowledged position without reprocessing.
- [ ] All operations are idempotent (reprocessing the same event produces the same result).
- [ ] Dockerfile builds successfully.

## Dependencies

- Task 03 (PostgreSQL Schema) -- tables must exist.
- Task 04 (Actor Service Foundation) -- event serialization via Proto.Persistence.EventStore must match.
- Tasks 05 and 06 (User and Organization Grains) -- to produce events for testing.

## Notes

- The projector is eventually consistent. There will be a small delay (typically <100ms) between an event being appended to KurrentDB and the projection being updated. The frontend should handle this gracefully (refetch after mutations).
- **Projectors are separate from the actor cluster.** They are independent .NET workers that subscribe to KurrentDB persistent subscriptions. They do not participate in the Proto.Actor cluster. This ensures projectors can be scaled independently of the grain service.
- KurrentDB persistent subscriptions handle checkpointing internally — no `projection_checkpoint` table is needed.
- For local development, use Testcontainers for KurrentDB and PostgreSQL.
- The projector should log event processing at DEBUG level and errors at ERROR level. Include event type, stream ID, and event number in log messages.
