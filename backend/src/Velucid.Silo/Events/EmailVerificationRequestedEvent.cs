namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a user requests email verification. The token expires after 15 minutes.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="Email">The email address to verify.</param>
/// <param name="Token">The 6-digit verification code.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public record EmailVerificationRequestedEvent(
    Guid UserId,
    string Email,
    string Token,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;
