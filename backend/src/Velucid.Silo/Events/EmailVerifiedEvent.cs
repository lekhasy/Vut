namespace Velucid.Silo.Events;

/// <summary>
/// Raised when a user's email address is successfully verified.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="Email">The verified email address.</param>
/// <param name="ActorId">The identifier of the actor who caused this event.</param>
/// <param name="Timestamp">The UTC timestamp when the event occurred.</param>
public record EmailVerifiedEvent(
    Guid UserId,
    string Email,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;
