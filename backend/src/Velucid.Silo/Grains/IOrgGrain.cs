using Velucid.Silo.Models;
using Velucid.Silo.Services;

namespace Velucid.Silo.Grains;

/// <summary>
/// Grain interface for the Organization aggregate. Manages org creation,
/// member management, and invitations via event sourcing.
/// </summary>
public interface IOrgGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Creates a new organization with the current user as owner.
    /// Idempotent: if the org already exists, returns without emitting events.
    /// </summary>
    /// <param name="name">The organization name.</param>
    /// <param name="ownerUserId">The user ID of the organization owner.</param>
    Task<OrgInfo> CreateOrg(string name, Guid ownerUserId);

    /// <summary>
    /// Renames the organization. Only callable by org members.
    /// </summary>
    /// <param name="name">The new organization name.</param>
    /// <param name="requesterUserId">The user ID making the request (must be an org member).</param>
    Task RenameOrg(string name, Guid requesterUserId);

    /// <summary>
    /// Soft-deletes the organization. Only callable by the owner.
    /// </summary>
    /// <param name="requesterUserId">The user ID of the requester (must be the org owner).</param>
    Task DeleteOrg(Guid requesterUserId);

    /// <summary>
    /// Adds a member to the organization. Only callable by org members.
    /// </param>
    Task AddMember(Guid userId, string role, Guid requesterUserId);

    /// <summary>
    /// Removes a member from the organization. Only the owner can remove non-owner members.
    /// </summary>
    /// <param name="userId">The user ID to remove.</param>
    /// <param name="requesterUserId">The user ID making the request (must be owner).</param>
    Task RemoveMember(Guid userId, Guid requesterUserId);

    /// <summary>
    /// Sends an invitation to join the organization.
    /// </summary>
    /// <param name="email">The email address to invite.</param>
    /// <param name="role">The role to assign when accepted.</param>
    /// <param name="inviterUserId">The user ID of the person sending the invitation.</param>
    Task SendInvitation(string email, string role, Guid inviterUserId);

    /// <summary>
    /// Gets the organization's public info. Throws if the org does not exist.
    /// </summary>
    Task<OrgInfo> GetOrgInfo();

    /// <summary>
    /// Gets the members of the organization.
    /// </summary>
    Task<IReadOnlyList<OrgMemberInfo>> GetMembers();

    /// <summary>
    /// Checks if a user is a member of the organization.
    /// </summary>
    Task<bool> IsMember(Guid userId);
}