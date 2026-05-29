# Deferred Work

## Deferred from: code review of 1-1-organizations (2026-05-28)

- **Projector at-least-once delivery causes duplicate event reprocessing on Nack/Retry** — `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs:245-252` — deferred, inherent to persistent subscription pattern
- **CreateOrg TOCTOU race during Orleans grain activation** — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:914-925` — deferred, inherent to Orleans grain activation lifecycle
- **Stream ID check may drop events with resolveLinkTos enabled** — `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs:260-261` — deferred, pre-existing design issue
- **OrgInvitationProjection missing unique constraint on (OrgId, Email)** — `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs` — deferred, pre-existing schema gap
- **InviterUserId never persisted to OrgInvitationProjection** — `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs` — deferred, pre-existing entity design
- **TOCTOU race: RenameOrg and DeleteOrg concurrent execution order** — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:927-948` — deferred, grain concurrency model issue