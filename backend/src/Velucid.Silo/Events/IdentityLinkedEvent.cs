namespace Velucid.Silo.Events;

/// <summary>
/// Raised when an identity provider is linked to a user account.
/// This occurs during initial creation and when additional providers are added.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="Sub">The Auth0 subject identifier (e.g., "github|12345678").</param>
/// <param name="ProviderName">The identity provider name (e.g., "github", "google").</param>
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