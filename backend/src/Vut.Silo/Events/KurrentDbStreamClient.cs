using KurrentDB.Client;

namespace Vut.Silo.Events;

/// <summary>
/// Production implementation of <see cref="IEventStreamClient"/> that delegates
/// to <see cref="KurrentDBClient"/> for event persistence and retrieval.
/// </summary>
public sealed class KurrentDbStreamClient : IEventStreamClient
{
    private readonly KurrentDBClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="KurrentDbStreamClient"/> class.
    /// </summary>
    /// <param name="client">The KurrentDB client to delegate operations to.</param>
    public KurrentDbStreamClient(KurrentDBClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc/>
    public async Task<StreamState> AppendToStreamAsync(
        string streamId,
        StreamState expectedState,
        EventData eventData,
        CancellationToken cancellationToken = default)
    {
        var writeResult = await _client.AppendToStreamAsync(
            streamId,
            expectedState,
            [eventData],
            cancellationToken: cancellationToken);

        return writeResult.NextExpectedStreamState;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamEvent> ReadStreamForwardAsync(
        string streamId,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var result = _client.ReadStreamAsync(
            Direction.Forwards,
            streamId,
            StreamPosition.Start,
            cancellationToken: cancellationToken);

        if (await result.ReadState == ReadState.StreamNotFound)
            yield break;

        await foreach (var resolved in result)
        {
            yield return new StreamEvent(
                resolved.Event.EventType,
                resolved.Event.Data,
                resolved.Event.EventNumber.ToUInt64(),
                resolved.Event.EventStreamId);
        }
    }
}
