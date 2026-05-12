namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a new user is created during the first sign-in flow.
/// </summary>
/// <param name="UserId">The unique identifier assigned to the new user.</param>
/// <param name="DisplayName">The user's display name from the identity provider.</param>
/// <param name="AvatarUrl">The URL of the user's avatar from the identity provider.</param>
/// <param name="Email">The user's email address, if provided by the identity provider.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public record UserCreatedEvent(
    Guid UserId,
    string DisplayName,
    string AvatarUrl,
    string? Email,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;
