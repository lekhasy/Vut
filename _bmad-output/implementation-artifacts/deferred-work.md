# Deferred Work

## Deferred from: code review of 1-1-organizations (2026-05-28)

- **Projector at-least-once delivery causes duplicate event reprocessing on Nack/Retry** — `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs:245-252` — deferred, inherent to persistent subscription pattern
- **CreateOrg TOCTOU race during Orleans grain activation** — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:914-925` — deferred, inherent to Orleans grain activation lifecycle
- **Stream ID check may drop events with resolveLinkTos enabled** — `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs:260-261` — deferred, pre-existing design issue
- **OrgInvitationProjection missing unique constraint on (OrgId, Email)** — `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs` — deferred, pre-existing schema gap
- **InviterUserId never persisted to OrgInvitationProjection** — `backend/src/Velucid.ReadModel/Entities/OrgInvitationProjection.cs` — deferred, pre-existing entity design
- **TOCTOU race: RenameOrg and DeleteOrg concurrent execution order** — `backend/src/Velucid.Silo/Grains/OrgGrain.cs:927-948` — deferred, grain concurrency model issue

## Deferred from: code review of 3-2-migrate-org-grain-auth (2026-06-02)

- **InitializeAsync not thread-safe** — `OpenFgaTupleSync.cs:186-204` — deferred, single caller today
- **OpenFgaOptions defaults to HTTP** — `OpenFgaOptions.cs:15` — deferred, dev default; override in prod config
- **OpenFgaOptions no empty/whitespace validation** — `OpenFgaOptions.cs:15-17` — deferred, defensive
- **ResolveStoreIdAsync no retry on transient failure** — `OpenFgaTupleSync.cs:258-270` — deferred, crash-fast acceptable
- **MemberRemovedEvent lacks role field** — `OrgProjector.cs:270-271` — deferred, would require event schema change
- **Null role in MemberAddedPayload would NRE** — `OrgProjector.cs:262` — deferred, grain validates before emitting
- **DeleteOrg doesn't clean up OpenFGA tuples** — deferred, org IDs are GUIDs
- **SendInvitation doesn't validate role** — deferred, pre-existing behavior
- **AddMember idempotency makes projector role-update branch dead code** — deferred, not a functional bug
- **Race condition: grain auth check before projector writes OpenFGA tuple** — deferred, architectural trade-off of projector-based tuple writes (Task 7 decision). Projector processes quickly in practice but immediate follow-up commands after CreateOrg may fail until projector catches up.