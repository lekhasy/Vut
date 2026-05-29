namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a member is removed from an organization.
/// </summary>
/// <param name="OrgId">The unique identifier of the organization.</param>
/// <param name="UserId">The user ID being removed.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public sealed record MemberRemovedEvent(
    Guid OrgId,
    Guid UserId,
    Guid ActorId,
    DateTimeOffset Timestamp) : IEvent;