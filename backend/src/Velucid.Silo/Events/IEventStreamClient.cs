using KurrentDB.Client;

namespace Velucid.Silo.Events;

/// <summary>
/// Represents a persisted event read back from the event stream,
/// carrying the data needed for deserialization and position tracking.
/// </summary>
/// <param name="EventType">The event type name (e.g., "UserCreated").</param>
/// <param name="Data">The serialized event data as UTF-8 bytes.</param>
/// <param name="Position">The position of this event within the stream.</param>
/// <param name="StreamId">The stream identifier this event belongs to.</param>
public record StreamEvent(
    string EventType,
    ReadOnlyMemory<byte> Data,
    ulong Position,
    string StreamId);

/// <summary>
/// Abstraction over the event stream persistence layer.
/// Enables testing grains without a running KurrentDB instance.
/// </summary>
public interface IEventStreamClient
{
    /// <summary>
    /// Appends an event to the specified stream with optimistic concurrency control.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="expectedState">The expected stream state for concurrency check.</param>
    /// <param name="eventData">The serialized event data to persist.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The next expected stream state after the write.</returns>
    Task<StreamState> AppendToStreamAsync(
        string streamId,
        StreamState expectedState,
        EventData eventData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all events from the specified stream in forward order.
    /// Returns an empty sequence if the stream does not exist.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>An async enumerable of stream events, or empty if the stream doesn't exist.</returns>
    IAsyncEnumerable<StreamEvent> ReadStreamForwardAsync(
        string streamId,
        CancellationToken cancellationToken = default);
}
