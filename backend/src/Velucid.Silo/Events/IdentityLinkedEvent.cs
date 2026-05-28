namespace Velucid.Silo.Events;

/// <summary>
/// Raised when an additional identity is linked to an existing user account.
/// Emitted from <see cref="Grains.UserGrain.LinkIdentity"/> when a user already
/// exists and a new identity provider is being attached.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="Sub">The Auth0 subject identifier (e.g., "github|12345678").</param>
/// <param name="ProviderName">The Auth0 provider name (e.g., "github").</param>
/// <param name="Email">The email associated with this identity, if available.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public record IdentityLinkedEvent(
    Guid UserId,
    string Sub,
    string ProviderName,
    string? Email,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;