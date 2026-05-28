namespace Velucid.Silo.Events;

public record IdentityLinkedToUserEvent(
    Guid UserId,
    string Sub,
    string ProviderName,
    string? Email,
    Guid ActorId,
    DateTimeOffset Timestamp
) : IEvent;
