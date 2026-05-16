using System.Collections.Concurrent;
using System.Text.Json;
using KurrentDB.Client;
using Velucid.Silo.Events;

namespace Velucid.Silo.Tests.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="IEventStreamClient"/> for testing.
/// Stores events per stream and supports hydration via read.
/// </summary>
public sealed class InMemoryEventStreamClient : IEventStreamClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, List<StoredEvent>> _streams = new();
    private readonly Lock _appendLock = new();

    /// <summary>
    /// Gets all domain events stored for the specified stream.
    /// </summary>
    public IReadOnlyList<IEvent> GetEvents(string streamId)
    {
        return _streams.TryGetValue(streamId, out var events)
            ? events.Select(e => e.DomainEvent).ToList()
            : [];
    }

    /// <inheritdoc/>
    public Task<StreamState> AppendToStreamAsync(
        string streamId,
        StreamState expectedState,
        EventData eventData,
        CancellationToken cancellationToken = default)
    {
        var events = _streams.GetOrAdd(streamId, _ => []);

        var typeName = eventData.Type;
        var clrType = EventTypeMapping.GetClrType(typeName);
        var domainEvent = (IEvent)(JsonSerializer.Deserialize(
            eventData.Data.Span, clrType, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize event '{typeName}'."));

        ulong position;
        lock (_appendLock)
        {
            position = (ulong)events.Count;
            events.Add(new StoredEvent(domainEvent, eventData.Data, typeName, position));
        }

        return Task.FromResult<StreamState>(position);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamEvent> ReadStreamForwardAsync(
        string streamId,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        if (!_streams.TryGetValue(streamId, out var events) || events.Count == 0)
            yield break;

        foreach (var stored in events)
        {
            yield return new StreamEvent(
                stored.EventType,
                stored.Data,
                stored.Position,
                streamId);
        }

        await Task.CompletedTask;
    }

    private sealed record StoredEvent(
        IEvent DomainEvent,
        ReadOnlyMemory<byte> Data,
        string EventType,
        ulong Position);
}
