namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a new organization is created.
/// </summary>
/// <param name="OrgId">The unique identifier of the organization.</param>
/// <param name="Name">The organization name.</param>
/// <param name="OwnerUserId">The user ID of the organization owner.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public sealed record OrgCreatedEvent(
    Guid OrgId,
    string Name,
    Guid OwnerUserId,
    Guid ActorId,
    DateTimeOffset Timestamp) : IEvent;