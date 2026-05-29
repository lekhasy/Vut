namespace Velucid.Silo.Events;

/// <summary>
/// Raised when an organization is soft-deleted.
/// </summary>
/// <param name="OrgId">The unique identifier of the organization.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public sealed record OrgDeletedEvent(
    Guid OrgId,
    Guid ActorId,
    DateTimeOffset Timestamp) : IEvent;