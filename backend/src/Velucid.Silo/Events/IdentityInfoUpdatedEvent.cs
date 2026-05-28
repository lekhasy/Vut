namespace Velucid.Silo.Events;

/// <summary>
/// Raised when an identity's information (display name, avatar, email) is updated.
/// This event is NOT for linking — linking is handled by <see cref="IdentityLinkedToUserEvent"/>.
/// Emitted from the User grain when a user updates their profile, allowing projectors
/// to sync the updated info to all linked identity records.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="Sub">The Auth0 subject identifier (e.g., "github|12345678").</param>
/// <param name="ProviderName">The Auth0 provider name (e.g., "github").</param>
/// <param name="DisplayName">The updated display name.</param>
/// <param name="AvatarUrl">The updated avatar URL.</param>
/// <param name="Email">The updated email address.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public record IdentityInfoUpdatedEvent(
    Guid UserId,
    string Sub,
    string ProviderName,
    string DisplayName,
    string AvatarUrl,
    string? Email,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;