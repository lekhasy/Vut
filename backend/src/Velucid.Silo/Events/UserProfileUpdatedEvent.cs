namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a user's profile information is updated.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="DisplayName">The updated display name.</param>
/// <param name="AvatarUrl">The updated avatar URL.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public record UserProfileUpdatedEvent(
    Guid UserId,
    string DisplayName,
    string AvatarUrl,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;
