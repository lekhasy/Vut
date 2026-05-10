# Task 04: .NET Orleans Silo Foundation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 04 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 3 days |

## Description

Build the foundational .NET Orleans silo that hosts all virtual actor (grain) types and a co-hosted ASP.NET Core API. This includes the Orleans silo configuration with PostgreSQL as the cluster membership provider via `Orleans.Clustering.AdoNet`, the `EventSourcedGrain<TState>` base class that integrates directly with KurrentDB using the `EventStore.Client` .NET SDK, and the co-hosted ASP.NET Core API controllers that call grains via `IGrainFactory`.

There are **no actor managers** and **no external message broker**. The Orleans runtime handles grain identity resolution, auto-activation, location transparency, and failover automatically. PostgreSQL (already in the stack) provides cluster membership.

## Architecture Reference

- Architecture doc Section 2 (Why Virtual Actors — Orleans)
- Architecture doc Section 4.1 (Silo Configuration)
- Architecture doc Section 4.2 (Grain Types)
- Architecture doc Section 4.4 (Grain Activation Flow)
- Architecture doc Section 5.1 (Base Grain Abstraction — EventSourcedGrain)
- Architecture doc Section 5.2 (Grain Lifecycle State Machine)
- Architecture doc Section 6.1 (KurrentDB .NET Client Integration)
- Architecture doc Section 6.2 (Event Serialization)
- Architecture doc Section 6.4 (Event Envelope)

## Technical Requirements

### Solution Structure
```
src/
  Vut.Silo/
    Program.cs
    Vut.Silo.csproj
    Configuration/
      KurrentDbOptions.cs
    Grains/
      EventSourcedGrain.cs
    Events/
      IEvent.cs
      EventTypeMapping.cs
    Controllers/
      (placeholder — concrete controllers added in Tasks 05/06)
```

### NuGet Packages

| Package | Purpose |
|---------|---------|
| `Microsoft.Orleans.Server` | Orleans silo host, grain runtime |
| `Microsoft.Orleans.Clustering.AdoNet` | PostgreSQL-based cluster membership |
| `Npgsql` | ADO.NET provider for PostgreSQL |
| `EventStore.Client.Grpc.Streams` | KurrentDB .NET client for event sourcing |
| `Resend` | Resend .NET SDK for sending verification and invitation emails |
| `Microsoft.AspNetCore.OpenApi` | (optional) Swagger for co-hosted API |

### Silo Configuration (`Program.cs`)

On startup, the service must:
1. Create a `WebApplication.CreateBuilder()` host.
2. Configure Orleans via `builder.UseOrleans(siloBuilder => ...)`:
   - **Clustering provider**: PostgreSQL via `UseAdoNetClustering` with `Invariant = "Npgsql"`. **Do NOT use `UseLocalhostClustering()` even for local development** — ADO.NET clustering is used from day one so scaling to multiple silos requires zero code changes.
   - **ClusterId**: `"vut-cluster"`
   - **ServiceId**: `"vut"`
   - **Silo port**: `11111` (silo-to-silo communication)
   - **Gateway port**: `30000` (Orleans client connections)
   - **Default grain storage**: `AddMemoryGrainStorageAsDefault()` (grain state lives in KurrentDB, not Orleans storage)
   - **Grain collection**: `GrainCollectionOptions.CollectionAge = TimeSpan.FromMinutes(30)`
3. Register `EventStoreClient` as a singleton for KurrentDB access.
4. Register `ResendClient` as a singleton for sending emails (verification codes, invitation emails). API key from configuration (`Resend:ApiKey`).
5. Register ASP.NET Core controllers via `builder.Services.AddControllers()`.
5. Map controllers via `app.MapControllers()`.
6. Run on port 5000 for HTTP API.

```csharp
// Program.cs — Orleans silo with co-hosted ASP.NET Core API
var builder = WebApplication.CreateBuilder(args);

builder.UseOrleans(siloBuilder =>
{
    siloBuilder
        .UseAdoNetClustering(options =>
        {
            options.ConnectionString = builder.Configuration
                .GetConnectionString("PostgreSQL");
            options.Invariant = "Npgsql";
        })
        .ConfigureEndpoints(
            siloPort: 11111,
            gatewayPort: 30000)
        .AddMemoryGrainStorageAsDefault()
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "vut-cluster";
            options.ServiceId = "vut";
        })
        .Configure<GrainCollectionOptions>(options =>
        {
            options.CollectionAge = TimeSpan.FromMinutes(30);
        });
});

// KurrentDB client (singleton, injected into grains via DI)
builder.Services.AddSingleton(new EventStoreClient(
    EventStoreClientSettings.Create(
        builder.Configuration["KurrentDb:ConnectionString"]
        ?? "esdb://vut-kurrentdb:2113?tls=false")));

// Co-hosted ASP.NET Core API
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

### KurrentDB .NET Client Integration

Each grain interacts with KurrentDB directly using the official `EventStore.Client.Grpc.Streams` NuGet package — there is no intermediary persistence provider layer.

```csharp
// KurrentDB client registered in DI, injected into grains
builder.Services.AddSingleton(new EventStoreClient(
    EventStoreClientSettings.Create(
        "esdb://vut-kurrentdb:2113?tls=false")));
```

The `EventStoreClient` is a singleton shared by all grains. Each grain scopes its reads and writes to its own stream:
- User grains: stream ID = `user-{userId}`
- Organization grains: stream ID = `organization-{orgId}`

### Base Grain Abstraction (`EventSourcedGrain<TState>`)

All aggregate grains inherit from this base class, which handles the event sourcing lifecycle with direct KurrentDB integration:

```csharp
public abstract class EventSourcedGrain<TState> : Grain
    where TState : class, new()
{
    private readonly EventStoreClient _client;
    private readonly string _streamId;
    private TState _state = new();
    private StreamRevision _currentRevision = StreamRevision.None;

    protected TState State => _state;

    protected EventSourcedGrain(EventStoreClient client, string streamId)
    {
        _client = client;
        _streamId = streamId;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateFromStream(ct);
        DelayDeactivation(TimeSpan.FromMinutes(30));
        await base.OnActivateAsync(ct);
    }

    private async Task HydrateFromStream(CancellationToken ct)
    {
        var result = _client.ReadStreamAsync(
            Direction.Forwards,
            _streamId,
            StreamPosition.Start,
            cancellationToken: ct);

        if (await result.ReadState == ReadState.StreamNotFound)
            return; // New aggregate, empty state

        await foreach (var resolved in result)
        {
            var @event = DeserializeEvent(resolved);
            Apply(_state, @event);
            _currentRevision = resolved.Event.EventNumber;
        }
    }

    protected async Task<TResult> EmitEvent<TResult>(
        object @event,
        Func<TState, TResult> resultSelector)
    {
        var eventData = SerializeEvent(@event);
        var writeResult = await _client.AppendToStreamAsync(
            _streamId,
            _currentRevision,
            new[] { eventData });

        _currentRevision = writeResult.NextExpectedStreamRevision;
        Apply(_state, @event);
        return resultSelector(_state);
    }

    protected async Task EmitEvent(object @event)
    {
        var eventData = SerializeEvent(@event);
        var writeResult = await _client.AppendToStreamAsync(
            _streamId,
            _currentRevision,
            new[] { eventData });

        _currentRevision = writeResult.NextExpectedStreamRevision;
        Apply(_state, @event);
    }

    protected abstract void Apply(TState state, object @event);

    private EventData SerializeEvent(object @event)
    {
        var typeName = EventTypeMapping.GetTypeName(@event.GetType());
        var json = JsonSerializer.SerializeToUtf8Bytes(
            @event, @event.GetType());
        return new EventData(Uuid.NewUuid(), typeName, json);
    }

    private object DeserializeEvent(ResolvedEvent resolved)
    {
        var type = EventTypeMapping.GetClrType(
            resolved.Event.EventType);
        return JsonSerializer.Deserialize(
            resolved.Event.Data.Span, type)!;
    }
}
```

**Key points:**
- No external persistence provider — grains talk to KurrentDB directly via `EventStoreClient`.
- Optimistic concurrency via `_currentRevision` — KurrentDB rejects writes if another process has appended events since the grain last read.
- `OnActivateAsync()` hydrates state from the event stream on activation.
- `DelayDeactivation(TimeSpan.FromMinutes(30))` keeps the grain in memory after its last call; `GrainCollectionOptions.CollectionAge` provides the global idle timeout.
- Each grain method is a separate strongly-typed C# interface method — no `HandleMessage` switch or gRPC proto files.

### Co-hosted ASP.NET Core API

The API is hosted inside the Orleans silo process. Controllers use `IGrainFactory` (injected via DI) to obtain grain references:

```csharp
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;

    public UserController(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request)
    {
        var userId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        var result = await grain.CreateUser(
            request.ProviderId,
            request.ProviderName,
            request.DisplayName,
            request.AvatarUrl,
            request.Email);

        return Ok(result);
    }
}
```

No gRPC serialization or cluster routing is involved. `IGrainFactory` returns a grain reference (proxy). If the grain is on this silo, the call is local. If on another silo, Orleans handles transport transparently.

### Event Serialization
- Use `System.Text.Json` with camelCase naming convention.
- The `EventSourcedGrain<TState>` base class handles serialization/deserialization via `SerializeEvent` and `DeserializeEvent` methods.
- Map event type strings to CLR types via `EventTypeMapping`:
  - `"UserCreated"` -> `UserCreatedEvent`
  - `"UserProfileUpdated"` -> `UserProfileUpdatedEvent`
  - `"OrganizationCreated"` -> `OrganizationCreatedEvent`
  - etc. (all events from architecture doc Sections 5.3 and 5.4).
- Each event type implements `IEvent` which requires `ActorId` and `Timestamp`.

### Dockerfile
- Multi-stage build: `mcr.microsoft.com/dotnet/sdk:8.0` for build, `mcr.microsoft.com/dotnet/aspnet:8.0` for runtime.
- Expose port 5000 (HTTP API), port 11111 (silo-to-silo), port 30000 (Orleans gateway).
- Output image: `vut/silo`.
- Replicas: **1** on single-machine K3s deployment (increase when scaling via Tailscale).

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Vut.Silo/Vut.Silo.csproj", "Vut.Silo/"]
RUN dotnet restore "Vut.Silo/Vut.Silo.csproj"
COPY src/ .
RUN dotnet publish "Vut.Silo/Vut.Silo.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000 11111 30000
ENTRYPOINT ["dotnet", "Vut.Silo.dll"]
```

## Acceptance Criteria

- [ ] `Vut.Silo` compiles and starts without errors.
- [ ] Orleans silo joins `vut-cluster` using PostgreSQL ADO.NET clustering.
- [ ] `ClusterOptions` configured with `ClusterId = "vut-cluster"` and `ServiceId = "vut"`.
- [ ] `GrainCollectionOptions.CollectionAge` set to 30 minutes.
- [ ] `EventSourcedGrain<TState>` correctly hydrates state from KurrentDB via `EventStoreClient` on `OnActivateAsync()`.
- [ ] `EventSourcedGrain<TState>` persists events to KurrentDB with optimistic concurrency.
- [ ] `EventStoreClient` registered as singleton in DI container.
- [ ] Events are serialized as JSON with camelCase via `System.Text.Json`.
- [ ] ASP.NET Core controllers are co-hosted and serve HTTP on port 5000.
- [ ] `IGrainFactory` is injectable in API controllers.
- [ ] Dockerfile builds successfully and produces a working container image exposing ports 5000, 11111, and 30000.

## Dependencies

- Task 01 (Kubernetes Infrastructure) -- needs KurrentDB and PostgreSQL running.
- Can develop and test locally with Docker containers for KurrentDB and PostgreSQL.

## Notes

- The silo is the heart of the write path. It must be reliable and well-tested.
- Every pod runs the same code and can host any grain type -- there are no special roles or manager pods.
- **Do NOT use `UseLocalhostClustering()`** even during local development. Always use `UseAdoNetClustering()` with PostgreSQL. This ensures scaling to multiple silos (same machine or across machines via Tailscale) requires zero code changes — just increase replicas.
- Orleans clustering tables (`OrleansMembershipTable`, `OrleansMembershipVersionTable`) are created automatically by the ADO.NET clustering provider on first silo startup (see Architecture doc Section 9.4).
- Snapshotting should be considered for grains with long event histories (every 50 events) to optimize activation time (see Architecture doc Section 15.1).
- This task creates the framework; Tasks 05 and 06 add the concrete User and Organization grain interfaces, implementations, and API controllers.
