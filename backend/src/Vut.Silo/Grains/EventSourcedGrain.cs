using System.Text.Json;
using KurrentDB.Client;
using Vut.Silo.Events;

namespace Vut.Silo.Grains;

/// <summary>
/// Base class for all event-sourced grains. Integrates with KurrentDB
/// via <see cref="IEventStreamClient"/> for event persistence and hydration.
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
/// Optimistic concurrency is enforced via <c>_currentStreamState</c>. KurrentDB rejects writes
/// if another process has appended events since the grain last read.
/// </para>
/// <para>
/// Derived classes must implement <see cref="BuildStreamId"/> to construct the KurrentDB
/// stream identifier from the grain's key. This is called during activation, when the
/// grain context (and thus the grain key) is available.
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

    private readonly IEventStreamClient _eventStreamClient;
    private string _streamId = string.Empty;
    private TState _state = new();
    private StreamState _currentStreamState = StreamState.NoStream;

    /// <summary>
    /// Gets the current aggregate state, hydrated from the event stream.
    /// </summary>
    protected TState State => _state;

    /// <summary>
    /// Gets the KurrentDB stream identifier for this grain,
    /// resolved during activation via <see cref="BuildStreamId"/>.
    /// </summary>
    protected string StreamId => _streamId;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcedGrain{TState}"/> class.
    /// </summary>
    /// <param name="eventStreamClient">The event stream client for persistence operations.</param>
    protected EventSourcedGrain(IEventStreamClient eventStreamClient)
    {
        _eventStreamClient = eventStreamClient ?? throw new ArgumentNullException(nameof(eventStreamClient));
    }

    /// <summary>
    /// Constructs the KurrentDB stream identifier for this grain.
    /// Called once during grain activation when the grain context is available.
    /// </summary>
    /// <returns>
    /// The stream identifier (e.g., "user-{userId}" or "organization-{orgId}").
    /// </returns>
    protected abstract string BuildStreamId();

    /// <summary>
    /// Called when the grain is activated. Hydrates state from the KurrentDB event stream
    /// and configures deactivation delay.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _streamId = BuildStreamId();
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
        _currentStreamState = await _eventStreamClient.AppendToStreamAsync(
            _streamId, _currentStreamState, eventData, cancellationToken);

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
        _currentStreamState = await _eventStreamClient.AppendToStreamAsync(
            _streamId, _currentStreamState, eventData, cancellationToken);

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
        await foreach (var streamEvent in _eventStreamClient.ReadStreamForwardAsync(
            _streamId, cancellationToken))
        {
            var @event = DeserializeEvent(streamEvent);
            Apply(_state, @event);
            _currentStreamState = streamEvent.Position;
        }
    }

    private static EventData SerializeEvent(IEvent @event)
    {
        var typeName = EventTypeMapping.GetTypeName(@event.GetType());
        var json = JsonSerializer.SerializeToUtf8Bytes(
            @event, @event.GetType(), JsonOptions);
        return new EventData(Uuid.NewUuid(), typeName, json);
    }

    private static IEvent DeserializeEvent(StreamEvent streamEvent)
    {
        var type = EventTypeMapping.GetClrType(streamEvent.EventType);
        return (IEvent)(JsonSerializer.Deserialize(
            streamEvent.Data.Span, type, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize event '{streamEvent.EventType}' " +
                $"from stream '{streamEvent.StreamId}'."));
    }
}
