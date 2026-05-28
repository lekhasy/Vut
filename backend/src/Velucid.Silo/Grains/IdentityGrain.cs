using Velucid.Silo.Events;
using Velucid.Silo.Models;

namespace Velucid.Silo.Grains;

/// <summary>
/// Event-sourced grain that manages identity-to-user linkages.
/// Stores only the linked UserId — does not create users.
/// </summary>
public class IdentityGrain : EventSourcedGrain<IdentityState>, IIdentityGrain
{
    public IdentityGrain(
        IEventStreamClient eventStreamClient,
        TimeProvider timeProvider)
        : base(eventStreamClient, timeProvider)
    {
    }

    protected override string BuildStreamId() => $"identity-{this.GetPrimaryKeyString()}";

    protected override void Apply(IdentityState state, IEvent @event)
    {
        switch (@event)
        {
            case IdentityLinkedToUserEvent e:
                state.UserId = e.UserId;
                break;
        }
    }

    /// <inheritdoc/>
    public async Task SetLinkedUserId(Guid userId, string providerName, string? email)
    {
        if (Exists)
            return;

        var sub = this.GetPrimaryKeyString();

        await EmitEvent(new IdentityLinkedToUserEvent(
            userId,
            sub,
            providerName,
            email,
            userId,
            UtcNow));
    }

    /// <inheritdoc/>
    public Task<Guid> GetLinkedUserId()
    {
        if (!Exists)
            throw new IdentityOrphanedException(this.GetPrimaryKeyString());

        return Task.FromResult(State.UserId);
    }
}
