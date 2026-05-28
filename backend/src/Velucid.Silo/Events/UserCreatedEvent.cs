namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a new user is created and their first Auth0 identity is linked.
/// This is emitted from <see cref="Grains.UserGrain.CreateUser"/> in one atomic event.
/// </summary>
/// <param name="UserId">The unique identifier assigned to the new user.</param>
/// <param name="DisplayName">The user's display name from the identity provider.</param>
/// <param name="AvatarUrl">The URL of the user's avatar from the identity provider.</param>
/// <param name="Email">The user's email address, if provided by the identity provider.</param>
/// <param name="Sub">The Auth0 subject identifier of the first identity (e.g., "github|12345678").</param>
/// <param name="ProviderName">The Auth0 provider name (e.g., "github").</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public record UserRegisteredEvent(
    Guid UserId,
    string DisplayName,
    string AvatarUrl,
    string? Email,
    string Sub,
    string ProviderName,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;
