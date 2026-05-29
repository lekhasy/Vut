namespace Velucid.Silo.Events;

/// <summary>
/// Raised when an invitation is sent to join an organization.
/// </summary>
/// <param name="OrgId">The unique identifier of the organization.</param>
/// <param name="Email">The email address being invited.</param>
/// <param name="Role">The role being assigned when accepted.</param>
/// <param name="InviterUserId">The user ID of the person sending the invitation.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public sealed record InvitationSentEvent(
    Guid OrgId,
    string Email,
    string Role,
    Guid InviterUserId,
    Guid ActorId,
    DateTimeOffset Timestamp) : IEvent;