# Task 07: Projector Service Implementation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 07 |
| **Priority** | P1 |
| **Estimated Effort** | 2.5 days |

## Description

Implement the .NET Projector Service that subscribes to Redpanda consumer groups and updates PostgreSQL read model projections. This service is the bridge between the event store (source of truth) and the read model (query-optimized views). It must be idempotent, checkpointed, and resilient to restarts.

## Architecture Reference

- Architecture doc Section 7.2 (Projector Service Design)
- Architecture doc Section 7.1 (Projection Views - table schema)
- Architecture doc Section 6.4 (Redpanda Topic Design)

## Technical Requirements

### Solution Structure
```
src/
  Vut.ProjectorService/
    Program.cs
    Vut.ProjectorService.csproj
    Configuration/
      RedpandaOptions.cs
      PostgresOptions.cs
    Checkpoints/
      CheckpointStore.cs
    Handlers/
      UserProjector.cs
      OrgProjector.cs
    Database/
      DbConnectionFactory.cs
```

### Service Lifecycle
- Runs as a .NET Worker Service (`IHostedService` or `BackgroundService`).
- On startup: load last checkpoint offsets from `projection_checkpoint` table.
- Subscribe to Redpanda consumer groups:
  - Consumer group `vut-projector-user` for topic `vut.user-events`.
  - Consumer group `vut-projector-org` for topic `vut.org-events`.
- On each consumed message: deserialize the event envelope, route to the correct handler, update PostgreSQL, save checkpoint.
- On shutdown: commit final offsets.

### Checkpoint Store (`CheckpointStore`)
```csharp
public interface ICheckpointStore
{
    Task<long> GetLastOffsetAsync(string projectorName, string topic, int partitionId);
    Task SaveOffsetAsync(string projectorName, string topic, int partitionId, long offset);
}
```
- Reads/writes to the `projection_checkpoint` table.
- Checkpoints are saved after each successful batch of event projections (or every N events).

### User Projector (`UserProjector`)
Handles events from `vut.user-events` topic:

| Event | Action |
|-------|--------|
| `UserCreated` | `INSERT INTO user_projection (user_id, provider_id, display_name, avatar_url, created_at, updated_at)` |
| `UserProfileUpdated` | `UPDATE user_projection SET display_name = @displayName, avatar_url = @avatarUrl, updated_at = @timestamp WHERE user_id = @userId` |

All operations must be idempotent:
- `INSERT` uses `ON CONFLICT (user_id) DO UPDATE SET ...` pattern.
- `UPDATE` uses `WHERE user_id = @userId` and is naturally idempotent.

### Org Projector (`OrgProjector`)
Handles events from `vut.org-events` topic:

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

### Redpanda Consumer Configuration
- Use Confluent.Kafka NuGet package.
- Consumer group IDs: `vut-projector-user` and `vut-projector-org`.
- `enable.auto.commit = false` -- manual offset commit after successful projection.
- `auto.offset.reset = earliest` -- start from beginning on first run.
- Consume in a loop with cancellation token support for graceful shutdown.

### Dockerfile
- Multi-stage build similar to actor-service.
- Expose no ports (this is a background worker, not a service).
- Output image: `vut/projector-service`.

## Acceptance Criteria

- [ ] Projector service starts and subscribes to both Redpanda consumer groups.
- [ ] Consumed events are correctly projected into PostgreSQL tables.
- [ ] `UserCreated` creates a row in `user_projection`.
- [ ] `OrganizationCreated` creates a row in `org_projection`.
- [ ] `MemberJoined` creates rows in `org_member_projection`, `user_org_projection`, and updates `org_invitation_projection`.
- [ ] `MemberRemoved` deletes from both `org_member_projection` and `user_org_projection`.
- [ ] `MemberRoleChanged` updates both member tables.
- [ ] Checkpoints are saved after each processed event.
- [ ] Restarting the projector resumes from the last checkpoint without reprocessing.
- [ ] All operations are idempotent (reprocessing the same event produces the same result).
- [ ] Dockerfile builds successfully.

## Dependencies

- Task 03 (PostgreSQL Schema) -- tables must exist.
- Task 04 (Actor Service Foundation) -- event envelope and serialization must match.
- Tasks 05 and 06 (User and Organization Actors) -- to produce events for testing.

## Notes

- The projector is eventually consistent. There will be a small delay (typically <100ms) between an event being appended to KurrentDB and the projection being updated. The frontend should handle this gracefully (refetch after mutations).
- For testing, use Testcontainers for Redpanda and PostgreSQL, or use embedded Kafka mock.
- The projector should log event processing at DEBUG level and errors at ERROR level. Include event type, stream ID, and offset in log messages.
- Batch checkpointing (every 10 events or every 1 second, whichever comes first) is a performance optimization that can be added later.
