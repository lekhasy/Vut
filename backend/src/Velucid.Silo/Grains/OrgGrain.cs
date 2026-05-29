using Velucid.Silo.Events;
using Velucid.Silo.Models;

namespace Velucid.Silo.Grains;

/// <summary>
/// Event-sourced grain that manages the Organization aggregate. Persists events
/// to the <c>org-{orgId}</c> KurrentDB stream and rebuilds state on activation.
/// </summary>
public class OrgGrain : EventSourcedGrain<OrgState>, IOrgGrain
{
    public OrgGrain(
        IEventStreamClient eventStreamClient,
        TimeProvider timeProvider)
        : base(eventStreamClient, timeProvider)
    {
    }

    protected override string BuildStreamId() => $"org-{this.GetPrimaryKey()}";

    protected override void Apply(OrgState state, IEvent @event)
    {
        switch (@event)
        {
            case OrgCreatedEvent e:
                state.OrgId = e.OrgId;
                state.Name = e.Name;
                state.CreatedAt = e.Timestamp.DateTime;
                state.UpdatedAt = e.Timestamp.DateTime;
                state.Members[e.OwnerUserId] = "Owner";
                state.IsDeleted = false;
                break;

            case OrgRenamedEvent e:
                state.Name = e.Name;
                state.UpdatedAt = e.Timestamp.DateTime;
                break;

            case OrgDeletedEvent e:
                state.IsDeleted = true;
                state.UpdatedAt = e.Timestamp.DateTime;
                break;

            case MemberAddedEvent e:
                state.Members[e.UserId] = e.Role;
                state.UpdatedAt = e.Timestamp.DateTime;
                break;

            case MemberRemovedEvent e:
                state.Members.Remove(e.UserId);
                state.UpdatedAt = e.Timestamp.DateTime;
                break;

            case InvitationSentEvent e:
                state.Invitations.Add(new InvitationRecord
                {
                    Email = e.Email,
                    Role = e.Role,
                    InviterUserId = e.InviterUserId,
                    InvitedAt = e.Timestamp.DateTime,
                    Status = "Pending"
                });
                state.UpdatedAt = e.Timestamp.DateTime;
                break;
        }
    }

    public async Task<OrgInfo> CreateOrg(string name, Guid ownerUserId)
    {
        if (Exists)
            return new OrgInfo(State.OrgId, State.Name, State.IsDeleted);

        var orgId = this.GetPrimaryKey();
        var now = UtcNow;

        await EmitEvent(new OrgCreatedEvent(orgId, name, ownerUserId, ownerUserId, now));

        return new OrgInfo(orgId, name, false);
    }

    public async Task RenameOrg(string name, Guid requesterUserId)
    {
        if (!Exists)
            throw new InvalidOperationException("Organization does not exist.");
        if (State.IsDeleted)
            throw new InvalidOperationException("Organization is deleted.");
        if (!State.Members.ContainsKey(requesterUserId))
            throw new InvalidOperationException("Only org members can rename the organization.");

        if (State.Name == name)
            return;

        await EmitEvent(new OrgRenamedEvent(State.OrgId, name, requesterUserId, UtcNow));
    }

    public async Task DeleteOrg(Guid requesterUserId)
    {
        if (!Exists)
            throw new InvalidOperationException("Organization does not exist.");
        if (State.IsDeleted)
            return;

        if (!State.Members.TryGetValue(requesterUserId, out var role) || role != "Owner")
            throw new InvalidOperationException("Only the owner can delete the organization.");

        await EmitEvent(new OrgDeletedEvent(State.OrgId, requesterUserId, UtcNow));
    }

    public async Task AddMember(Guid userId, string role, Guid requesterUserId)
    {
        if (!Exists)
            throw new InvalidOperationException("Organization does not exist.");
        if (State.IsDeleted)
            throw new InvalidOperationException("Organization is deleted.");
        if (!State.Members.ContainsKey(requesterUserId))
            throw new InvalidOperationException("Only org members can add members.");
        if (role != "Owner" && role != "Member")
            throw new ArgumentException("Role must be 'Owner' or 'Member'.", nameof(role));

        if (State.Members.ContainsKey(userId))
            return;

        await EmitEvent(new MemberAddedEvent(State.OrgId, userId, role, requesterUserId, UtcNow));
    }

    public async Task RemoveMember(Guid userId, Guid requesterUserId)
    {
        if (!Exists)
            throw new InvalidOperationException("Organization does not exist.");
        if (State.IsDeleted)
            throw new InvalidOperationException("Organization is deleted.");

        if (!State.Members.TryGetValue(requesterUserId, out var requesterRole) || requesterRole != "Owner")
            throw new InvalidOperationException("Only the owner can remove members.");

        if (!State.Members.ContainsKey(userId))
            return; // not a member

        if (State.Members[userId] == "Owner")
            throw new InvalidOperationException("Cannot remove the owner.");

        await EmitEvent(new MemberRemovedEvent(State.OrgId, userId, requesterUserId, UtcNow));
    }

    public async Task SendInvitation(string email, string role, Guid inviterUserId)
    {
        if (!Exists)
            throw new InvalidOperationException("Organization does not exist.");
        if (State.IsDeleted)
            throw new InvalidOperationException("Organization is deleted.");

        // Check if inviter is a member
        if (!State.Members.ContainsKey(inviterUserId))
            throw new InvalidOperationException("Only org members can send invitations.");

        // Check if email already has a pending invitation
        var existing = State.Invitations.FirstOrDefault(i => i.Email == email && i.Status == "Pending");
        if (existing != null)
            return; // already invited

        await EmitEvent(new InvitationSentEvent(State.OrgId, email, role, inviterUserId, State.OrgId, UtcNow));
    }

    public Task<OrgInfo> GetOrgInfo()
    {
        if (!Exists)
            throw new InvalidOperationException("Organization does not exist.");

        return Task.FromResult(new OrgInfo(State.OrgId, State.Name, State.IsDeleted));
    }

    public Task<IReadOnlyList<OrgMemberInfo>> GetMembers()
    {
        if (!Exists)
            throw new InvalidOperationException("Organization does not exist.");

        var members = State.Members.Select(kv => new OrgMemberInfo(kv.Key, kv.Value)).ToList();
        return Task.FromResult<IReadOnlyList<OrgMemberInfo>>(members);
    }

    public Task<bool> IsMember(Guid userId)
    {
        if (!Exists)
            return Task.FromResult(false);

        return Task.FromResult(State.Members.ContainsKey(userId));
    }
}