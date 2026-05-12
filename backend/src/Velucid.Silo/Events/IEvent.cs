namespace Velucid.Silo.Events;

/// <summary>
/// Marker interface for all domain events persisted to KurrentDB.
/// Every event must carry an actor identifier and a timestamp.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// The identifier of the actor (user or system) that caused the event.
    /// </summary>
    Guid ActorId { get; }

    /// <summary>
    /// The UTC timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }
}
