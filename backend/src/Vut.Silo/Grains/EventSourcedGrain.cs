using System.Text.Json;
using EventStore.Client;
using Vut.Silo.Events;

namespace Vut.Silo.Grains;

/// <summary>
/// Base class for all event-sourced grains. Integrates directly with KurrentDB
/// using <see cref="EventStoreClient"/> for event persistence and hydration.
/// </summary>
/// <typeparam name="TState">
/// The aggregate state type. Must be a reference type with a parameterless constructor.
/// </typeparam>
/// <remarks>
/// <para>
/// On activation, the grain replays all events from its KurrentDB stream to rebuild state.
/// Commands emit new events via <see cref="EmitEvent{TResult}"/> or <see cref="EmitEvent"/>,
/// which persist to KurrentDB with optimistic concurrency before applying to in-memory state.
/// </para>
/// <para>
/// Optimistic concurrency is enforced via <c>_currentRevision</c>. KurrentDB rejects writes
/// if another process has appended events since the grain last read.
/// </para>
/// </remarks>
public abstract class EventSourcedGrain<TState> : Grain
    where TState : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly EventStoreClient _client;
    private readonly string _streamId;
    private TState _state = new();
    private StreamRevision _currentRevision = StreamRevision.None;

    /// <summary>
    /// Gets the current aggregate state, hydrated from the event stream.
    /// </summary>
    protected TState State => _state;

    /// <summary>
    /// Gets the KurrentDB stream identifier for this grain.
    /// </summary>
    protected string StreamId => _streamId;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcedGrain{TState}"/> class.
    /// </summary>
    /// <param name="client">The KurrentDB client for event stream operations.</param>
    /// <param name="streamId">
    /// The KurrentDB stream identifier (e.g., "user-{userId}" or "organization-{orgId}").
    /// </param>
    protected EventSourcedGrain(EventStoreClient client, string streamId)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _streamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
    }

    /// <summary>
    /// Called when the grain is activated. Hydrates state from the KurrentDB event stream
    /// and configures deactivation delay.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await HydrateFromStream(cancellationToken);
        DelayDeactivation(TimeSpan.FromMinutes(30));
        await base.OnActivateAsync(cancellationToken);
    }

    /// <summary>
    /// Emits an event to KurrentDB and applies it to the in-memory state,
    /// returning a result projected from the updated state.
    /// </summary>
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="event">The domain event to persist.</param>
    /// <param name="resultSelector">
    /// A function that projects the updated state into a result value.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The result projected from the updated state.</returns>
    protected async Task<TResult> EmitEvent<TResult>(
        IEvent @event,
        Func<TState, TResult> resultSelector,
        CancellationToken cancellationToken = default)
    {
        var eventData = SerializeEvent(@event);
        var writeResult = await _client.AppendToStreamAsync(
            _streamId,
            _currentRevision,
            [eventData],
            cancellationToken: cancellationToken);

        _currentRevision = writeResult.NextExpectedStreamRevision;
        Apply(_state, @event);
        return resultSelector(_state);
    }

    /// <summary>
    /// Emits an event to KurrentDB and applies it to the in-memory state.
    /// </summary>
    /// <param name="event">The domain event to persist.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    protected async Task EmitEvent(
        IEvent @event,
        CancellationToken cancellationToken = default)
    {
        var eventData = SerializeEvent(@event);
        var writeResult = await _client.AppendToStreamAsync(
            _streamId,
            _currentRevision,
            [eventData],
            cancellationToken: cancellationToken);

        _currentRevision = writeResult.NextExpectedStreamRevision;
        Apply(_state, @event);
    }

    /// <summary>
    /// Applies a domain event to the aggregate state. Implementations must handle
    /// all event types that the grain can produce. This method is called both
    /// during hydration (event replay) and after emitting new events.
    /// </summary>
    /// <param name="state">The current aggregate state to mutate.</param>
    /// <param name="event">The domain event to apply.</param>
    protected abstract void Apply(TState state, IEvent @event);

    private async Task HydrateFromStream(CancellationToken cancellationToken)
    {
        var result = _client.ReadStreamAsync(
            Direction.Forwards,
            _streamId,
            StreamPosition.Start,
            cancellationToken: cancellationToken);

        if (await result.ReadState == ReadState.StreamNotFound)
            return;

        await foreach (var resolved in result)
        {
            var @event = DeserializeEvent(resolved);
            Apply(_state, @event);
            _currentRevision = new StreamRevision(resolved.Event.EventNumber.ToUInt64());
        }
    }

    private static EventData SerializeEvent(IEvent @event)
    {
        var typeName = EventTypeMapping.GetTypeName(@event.GetType());
        var json = JsonSerializer.SerializeToUtf8Bytes(
            @event, @event.GetType(), JsonOptions);
        return new EventData(Uuid.NewUuid(), typeName, json);
    }

    private static IEvent DeserializeEvent(ResolvedEvent resolved)
    {
        var type = EventTypeMapping.GetClrType(resolved.Event.EventType);
        return (IEvent)(JsonSerializer.Deserialize(
            resolved.Event.Data.Span, type, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize event '{resolved.Event.EventType}' " +
                $"from stream '{resolved.Event.EventStreamId}'."));
    }
}
