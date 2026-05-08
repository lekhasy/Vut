# Task 04: .NET Actor Service Foundation (Virtual Actor Cluster)

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 04 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 3 days |

## Description

Build the foundational .NET actor service using Proto.Actor's virtual actor (grain) cluster model. This includes the Proto.Actor cluster host setup with Redpanda as the cluster provider, the `Proto.Persistence.EventStore` bridge for KurrentDB integration, the `AggregateGrain<TState>` base class that all grain types inherit from, and gRPC service definitions for cluster communication.

There are **no actor managers** in this design. The Proto.Actor runtime handles grain identity resolution, auto-activation, location transparency, and failover automatically.

## Architecture Reference

- Architecture doc Section 2 (Why Virtual Actors)
- Architecture doc Section 4 (Cluster Topology & Placement)
- Architecture doc Section 5.1 (Base Grain Abstraction)
- Architecture doc Section 5.2 (Grain Lifecycle State Machine)
- Architecture doc Section 6.1 (Proto.Persistence.EventStore Bridge)
- Architecture doc Section 6.4 (Event Envelope)

## Technical Requirements

### Solution Structure
```
src/
  Vut.ActorService/
    Program.cs
    Vut.ActorService.csproj
    Configuration/
      KurrentDbOptions.cs
      ClusterOptions.cs
    Grains/
      AggregateGrain.cs
    Events/
      IEvent.cs
      EventTypeMapping.cs
    Infrastructure/
      EventSerializer.cs
    Proto/
      grain.proto (gRPC service definitions for cluster messages)
```

### Cluster Configuration (`Program.cs`)

On startup, the service must:
1. Build the `ActorSystem`.
2. Configure the Proto.Actor cluster with:
   - **Cluster name**: `vut-cluster`
   - **Cluster provider**: Redpanda (Kafka-compatible membership gossip)
   - **Identity lookup**: `PartitionIdentityLookup` (hash-based partitioning)
   - **Bootstrap servers**: `vut-redpanda:9092` (from `ClusterOptions`)
3. Register cluster kinds for each aggregate type:
   - `"user"` -> `UserGrain` (added in Task 05)
   - `"organization"` -> `OrganizationGrain` (added in Task 06)
   - Register placeholders or stubs for Task 05/06 initially.
4. Start the cluster and block until shutdown.

```csharp
// Pseudocode -- Program.cs cluster setup
var clusterConfig = ClusterConfig.Setup(
    clusterName: "vut-cluster",
    clusterProvider: new RedpandaProvider(new RedpandaConfig(clusterOptions.RedpandaBootstrapServers)),
    identityLookup: new PartitionIdentityLookup(),
    kinds: new[]
    {
        ClusterKind.Get("user", GetUserGrainProps(persistenceProvider)),
        ClusterKind.Get("organization", GetOrganizationGrainProps(persistenceProvider)),
    }
);

var system = new ActorSystem();
var cluster = new Cluster(system, clusterConfig);
await cluster.StartAsync();
```

### Proto.Persistence.EventStore Bridge

Use the `Proto.Persistence.EventStore` NuGet package to bridge Proto.Actor's persistence API with KurrentDB streams.

```csharp
// Pseudocode -- provider initialization
var eventStoreSettings = EventStoreClientSettings.Create(
    kurrentDbOptions.ConnectionString);
var persistenceProvider = new EventStoreProvider(eventStoreSettings);
```

Each grain gets its own persistence instance scoped to its stream:
- User grains: stream ID = `user-{userId}`
- Organization grains: stream ID = `organization-{orgId}`

The provider handles event loading, snapshotting, and state recovery transparently.

### Base Grain Abstraction (`AggregateGrain<TState>`)

All aggregate grains inherit from this base class, which handles the event sourcing lifecycle:

```csharp
public abstract class AggregateGrain<TState> : IActor
    where TState : class, new()
{
    private readonly IProvider _persistenceProvider;
    private Persistence _persistence = null!;
    private TState _state = new();

    protected AggregateGrain(IProvider persistenceProvider)
    {
        _persistenceProvider = persistenceProvider;
    }

    // Called on grain activation -- loads events from KurrentDB
    protected abstract string GetStreamId(string identity);

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started _:
                var streamId = GetStreamId(context.Self!.Id);
                _persistence = Persistence.WithEventSourcingAndSnapshotting(
                    _persistenceProvider,
                    streamId,
                    ApplyEvent,
                    ApplySnapshot,
                    () => _state);
                await _persistence.RecoverStateAsync();
                context.SetReceiveTimeout(TimeSpan.FromMinutes(30));
                break;
            case ReceiveTimeout _:
                // Grain will be passivated by the runtime
                break;
            default:
                await HandleMessage(context);
                break;
        }
    }

    protected async Task EmitEvent(object @event)
    {
        await _persistence.PersistEventAsync(@event);
    }

    private void ApplyEvent(object @event) => Apply(_state, @event);
    private void ApplySnapshot(Snapshot snapshot) => _state = (TState)snapshot.State;

    protected TState State => _state;
    protected abstract void Apply(TState state, object @event);
    protected abstract Task HandleMessage(IContext context);
}
```

**Key points:**
- No `ActorManagerBase` -- the cluster runtime handles identity resolution and activation.
- No manual PID dictionary -- `Cluster.GetGrain(kind, identity)` returns the PID.
- No spawn/hydrate/passivate state machine -- the grain lifecycle is driven by the runtime.
- Passivation is handled via `ReceiveTimeout` (30 minutes). When the timeout fires, the grain is deactivated. Next message triggers fresh activation and re-hydration from KurrentDB.

### Event Serialization
- Use `System.Text.Json` with camelCase naming convention.
- `Proto.Persistence.EventStore` handles serialization/deserialization transparently.
- Map event type strings to CLR types via `EventTypeMapping`:
  - `"UserCreated"` -> `UserCreatedEvent`
  - `"UserProfileUpdated"` -> `UserProfileUpdatedEvent`
  - `"OrganizationCreated"` -> `OrganizationCreatedEvent`
  - etc. (all events from architecture doc Sections 5.3 and 5.4).
- Each event type implements `IEvent` which requires `ActorId` and `Timestamp`.

### gRPC Service Definition (`grain.proto`)

Define gRPC messages for cluster communication. These are used by the BFF to send commands to grains:

```protobuf
syntax = "proto3";

package vut;

// Generic command request -- routed to grain by cluster kind + identity
message ClusterCommandRequest {
  string kind = 1;       // "user" or "organization"
  string identity = 2;   // userId or orgId (UUID)
  string command_type = 3;
  string payload = 4;    // JSON
}

message ClusterCommandResponse {
  bool success = 1;
  string payload = 2;    // JSON result
  string error = 3;
}

service GrainService {
  rpc SendCommand(ClusterCommandRequest) returns (ClusterCommandResponse);
}
```

**Note:** The BFF sends commands via `Cluster.GetGrain(kind, identity)` -- this gRPC definition is for the external API surface. Internal cluster communication uses Proto.Actor's built-in transport.

### Dockerfile
- Multi-stage build: `mcr.microsoft.com/dotnet/sdk:8.0` for build, `mcr.microsoft.com/dotnet/aspnet:8.0` for runtime.
- Expose port 5000 (gRPC).
- Output image: `vut/actor-service`.
- Replicas: 3 in production (for grain distribution across cluster nodes).

## Acceptance Criteria

- [ ] `Vut.ActorService` compiles and starts without errors.
- [ ] Proto.Actor cluster connects to Redpanda and joins `vut-cluster`.
- [ ] Cluster kinds are registered for `"user"` and `"organization"` (stubs for Task 05/06).
- [ ] `AggregateGrain<TState>` correctly integrates with `Proto.Persistence.EventStore`.
- [ ] Grain activation loads events from KurrentDB via the persistence provider.
- [ ] Grain passivation works via `ReceiveTimeout` (30 minutes).
- [ ] Events are serialized as JSON with camelCase.
- [ ] gRPC service is defined and the server listens on port 5000.
- [ ] Dockerfile builds successfully and produces a working container image.

## Dependencies

- Task 01 (Kubernetes Infrastructure) -- needs KurrentDB and Redpanda running.
- Can develop and test locally with Docker containers for KurrentDB and Redpanda.

## Notes

- The actor service is the heart of the write path. It must be reliable and well-tested.
- Every pod runs the same code and can host any grain kind -- there are no special roles or manager pods.
- The `Proto.Persistence.EventStore` NuGet package handles event loading, snapshotting, and state recovery. The grain only works with strongly-typed event objects.
- Snapshotting should be configured for every 50 events to optimize activation time for grains with long event histories (see Architecture doc Section 15.1).
- This task creates the framework; Tasks 05 and 06 add the concrete User and Organization grains.
