---
name: project-architecture
description: Key architectural patterns and conventions in the Velucid backend
type: project
---

## Orleans Event-Sourced Architecture

- **Silo project**: `src/Velucid.Silo` - co-hosted Orleans silo + ASP.NET Core API
- **Grain base class**: `EventSourcedGrain<TState>` - persists events to KurrentDB, replays on activation
- **Grain keying**: User grains keyed by `Guid`, lookup grain keyed by string `"user-lookup"`, coordinator keyed by `"default"`
- **Event serialization**: JSON with camelCase naming, mapped via `EventTypeMapping` static class (register then freeze)
- **Events**: All implement `IEvent` interface with `ActorId` (Guid) and `Timestamp` (DateTimeOffset)
- **Stream convention**: `user-{userId}` for user event streams

## Read Model
- **Project**: `src/Velucid.ReadModel` - separate EF Core project with PostgreSQL
- **Tables**: `user_projection`, `user_identity` (with indexes on ProviderId and Email), `org_projection`, etc.
- **DbContext**: `ReadModelDbContext` with snake_case naming convention
- The read model has indexes but the Silo uses its own `IUserLookupGrain` for in-memory lookups

## Test Infrastructure
- **Test framework**: xUnit + FluentAssertions + NSubstitute + Orleans.TestingHost
- **Collection**: `[Collection("Orleans TestCluster")]` with `DisableParallelization = true`
- **Static shared state**: `TestSiloConfigurator.SharedEventStreamClient` and `SharedTimeProvider` (statics because ISiloConfigurator can't receive constructor params)
- **Event store**: `InMemoryEventStreamClient` - stores events per stream, supports hydration
- **Time**: `FakeTimeProvider` - starts at 2025-01-01, advances explicitly

## Key Design Decisions
- `SignInCoordinator` grain is a stateless orchestrator; controller just delegates to it
- `IUserLookupGrain` is an in-memory index; not persistent (event stream is source of truth)
- `UserGrain.RegisterInLookupIndex` is best-effort (catches exceptions silently)
- No CancellationToken propagation in grain interfaces (Orleans grain calls don't accept CTs directly)
- **Why:** The lookup index is ephemeral; if lost on silo restart, it rebuilds as sign-in requests flow through
- **How to apply:** When adding new grains, follow the same pattern: EventSourcedGrain base, IEvent events, EventTypeMapping registration in Program.cs
