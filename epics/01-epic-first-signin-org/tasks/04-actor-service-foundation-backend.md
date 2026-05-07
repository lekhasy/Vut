# Task 04: .NET Actor Service Foundation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 04 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 3 days |

## Description

Build the foundational .NET actor service that all subsequent backend tasks build upon. This includes the Proto.Actor host setup, the shared event envelope, KurrentDB client integration, actor lifecycle management (spawn, hydrate, passivate), and the base classes that User and Organization actors will inherit from. Projectors subscribe to KurrentDB persistent subscriptions directly — no Redpanda message production needed from the actor service.

## Architecture Reference

- Architecture doc Section 5 (Actor Model Design)
- Architecture doc Section 5.1 (Proto.Actor Hierarchy)
- Architecture doc Section 5.2 (Actor Lifecycle)
- Architecture doc Section 6 (Event Stream Design - envelope, serialization, topics)

## Technical Requirements

### Solution Structure
```
src/
  Vut.ActorService/
    Program.cs
    Vut.ActorService.csproj
    Configuration/
      KurrentDbOptions.cs
    Actors/
      ActorManagerBase.cs
      AggregateActorBase.cs
    Events/
      EventEnvelope.cs
      IEvent.cs
      EventTypeMapping.cs
    Infrastructure/
      KurrentDbClient.cs
      EventSerializer.cs
    Proto/
      actor.proto (gRPC service definitions)
```

### Event Envelope
```csharp
public class EventEnvelope
{
    public Guid EventId { get; set; }
    public string EventType { get; set; }
    public string StreamId { get; set; }
    public long EventNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActorId { get; set; }
    public JsonElement Payload { get; set; }
}
```

### Event Serialization
- Use `System.Text.Json` with camelCase naming convention.
- Map event type strings to CLR types via `EventTypeMapping`:
  - `"UserCreated"` -> `UserCreatedEvent`
  - `"UserProfileUpdated"` -> `UserProfileUpdatedEvent`
  - `"OrganizationCreated"` -> `OrganizationCreatedEvent`
  - etc. (all events from architecture doc Sections 5.3 and 5.4).
- Each event type implements `IEvent` which requires `ActorId` and `Timestamp`.

### KurrentDB Client (`KurrentDbClient`)
- Append events to a stream: `AppendAsync(string streamId, IEvent @event)`.
- Read events for rehydration: `ReadStreamAsync(string streamId)` -> returns `IAsyncEnumerable<EventEnvelope>`.
- Use the EventStoreDB GRPC client NuGet package.
- Connection string from configuration (`KurrentDbOptions.ConnectionString`).

### Actor Lifecycle (`AggregateActorBase`)
- Abstract base class for all aggregate actors.
- On receive of first command: load events from KurrentDB, replay to build state.
- Track time since last message; after N minutes idle, passivate (stop actor to free memory).
- On next command after passivation: re-spawn and rehydrate.
- Provide `EmmitEvent(IEvent @event)` method that:
  1. Appends to KurrentDB stream.
  2. Updates local actor state.

### Actor Manager (`ActorManagerBase<TActor>`)
- Manages PID lookup and spawning of aggregate actors.
- `GetOrCreateActor(string aggregateId)` -> returns `PID`.
- Uses a `ConcurrentDictionary<string, PID>` for fast lookup.
- On spawn: creates actor and registers with the Proto.Actor system.

### gRPC Service Definition (`actor.proto`)
Define a gRPC service that the Astro.js BFF will call:
```protobuf
service ActorService {
  rpc SendCommand(CommandRequest) returns (CommandResponse);
}

message CommandRequest {
  string command_type = 1;
  string payload = 2; // JSON
  string actor_id = 3;
}

message CommandResponse {
  bool success = 1;
  string payload = 2; // JSON result
  string error = 3;
}
```

### Dockerfile
- Multi-stage build: `mcr.microsoft.com/dotnet/sdk:8.0` for build, `mcr.microsoft.com/dotnet/aspnet:8.0` for runtime.
- Expose port 5000.
- Output image: `vut/actor-service`.

## Acceptance Criteria

- [ ] `Vut.ActorService` compiles and starts without errors.
- [ ] Actor service connects to KurrentDB and can append/read events.
- [ ] `AggregateActorBase` correctly rehydrates state from KurrentDB event stream.
- [ ] `ActorManagerBase` correctly spawns and looks up actors.
- [ ] Event envelope is serialized as JSON with camelCase and includes all required fields.
- [ ] gRPC service is defined and the server listens on port 5000.
- [ ] Dockerfile builds successfully and produces a working container image.

## Dependencies

- Task 01 (Kubernetes Infrastructure) -- needs KurrentDB and Redpanda running.
- Can develop and test locally with Docker containers for KurrentDB and Redpanda.

## Notes

- The actor service is the heart of the write path. It must be reliable and well-tested.
- Proto.Actor virtual actors are the recommended pattern -- they provide location transparency and automatic activation.
- The `EmmitEvent` method must handle KurrentDB append failures gracefully (retry with backoff).
- This task creates the framework; Tasks 05 and 06 add the concrete User and Organization actors.
