namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a member is added to an organization.
/// </summary>
/// <param name="OrgId">The unique identifier of the organization.</param>
/// <param name="UserId">The user ID being added.</param>
/// <param name="Role">The role being assigned.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public sealed record MemberAddedEvent(
    Guid OrgId,
    Guid UserId,
    string Role,
    Guid ActorId,
    DateTimeOffset Timestamp) : IEvent;