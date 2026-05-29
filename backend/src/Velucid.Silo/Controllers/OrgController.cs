using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Velucid.ReadModel;
using Velucid.Silo.Grains;

namespace Velucid.Silo.Controllers;

[ApiController]
public class OrgController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ReadModelDbContext _db;

    public OrgController(IGrainFactory grainFactory, ReadModelDbContext db)
    {
        _grainFactory = grainFactory;
        _db = db;
    }

    /// <summary>
    /// Lists all organizations the current user belongs to.
    /// </summary>
    [HttpGet("api/orgs")]
    public async Task<ActionResult<List<OrgDto>>> ListOrgs([FromQuery] Guid userId)
    {
        var memberships = _db.OrgMemberProjections
            .Where(m => m.UserId == userId)
            .ToList();

        if (memberships.Count == 0)
            return Ok(new List<OrgDto>());

        var orgIds = memberships.Select(m => m.OrgId).ToList();
        var orgs = _db.OrgProjections
            .Where(o => orgIds.Contains(o.OrgId) && !o.IsDeleted)
            .ToList();

        var result = orgs.Select(o =>
        {
            var membership = memberships.First(m => m.OrgId == o.OrgId);
            return new OrgDto(o.OrgId, o.Name, membership.Role, o.IsDeleted);
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Creates a new organization with the current user as owner.
    /// </summary>
    [HttpPost("api/orgs")]
    public async Task<ActionResult<OrgDto>> CreateOrg([FromBody] CreateOrgRequest request, [FromQuery] Guid userId)
    {
        var grain = _grainFactory.GetGrain<IOrgGrain>(request.OrgId);
        var orgInfo = await grain.CreateOrg(request.Name, userId);

        return CreatedAtAction(
            nameof(GetOrg),
            new { orgId = orgInfo.OrgId },
            new OrgDto(orgInfo.OrgId, orgInfo.Name, "Owner", orgInfo.IsDeleted));
    }

    /// <summary>
    /// Gets organization details.
    /// </summary>
    [HttpGet("api/orgs/{orgId:guid}")]
    public ActionResult<OrgDto> GetOrg(Guid orgId, [FromQuery] Guid userId)
    {
        var org = _db.OrgProjections.Find(orgId);
        if (org == null || org.IsDeleted)
            return NotFound();

        var membership = _db.OrgMemberProjections
            .Where(m => m.OrgId == orgId && m.UserId == userId)
            .FirstOrDefault();

        if (membership is null)
            return NotFound();

        return Ok(new OrgDto(org.OrgId, org.Name, membership.Role, org.IsDeleted));
    }

    /// <summary>
    /// Renames the organization.
    /// </summary>
    [HttpPut("api/orgs/{orgId:guid}")]
    public async Task<IActionResult> UpdateOrg(Guid orgId, [FromBody] UpdateOrgRequest request, [FromQuery] Guid userId)
    {
        var grain = _grainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.RenameOrg(request.Name, userId);
        return Ok();
    }

    /// <summary>
    /// Soft-deletes the organization.
    /// </summary>
    [HttpDelete("api/orgs/{orgId:guid}")]
    public async Task<IActionResult> DeleteOrg(Guid orgId, [FromQuery] Guid userId)
    {
        var grain = _grainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.DeleteOrg(userId);
        return NoContent();
    }

    /// <summary>
    /// Sends an invitation to join the organization.
    /// </summary>
    [HttpPost("api/orgs/{orgId:guid}/invitations")]
    public async Task<IActionResult> SendInvitation(Guid orgId, [FromBody] SendInvitationRequest request, [FromQuery] Guid inviterUserId)
    {
        var grain = _grainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.SendInvitation(request.Email, request.Role, inviterUserId);
        return Ok();
    }

    /// <summary>
    /// Lists all members of the organization.
    /// </summary>
    [HttpGet("api/orgs/{orgId:guid}/members")]
    public ActionResult<List<OrgMemberDto>> ListMembers(Guid orgId, [FromQuery] Guid userId)
    {
        var isMember = _db.OrgMemberProjections
            .Any(m => m.OrgId == orgId && m.UserId == userId);
        if (!isMember)
            return NotFound();

        var members = _db.OrgMemberProjections
            .Where(m => m.OrgId == orgId)
            .Join(
                _db.UserProjections,
                m => m.UserId,
                u => u.UserId,
                (m, u) => new { Member = m, User = u })
            .Select(x => new OrgMemberDto(
                x.User.UserId,
                x.User.DisplayName,
                x.User.AvatarUrl ?? string.Empty,
                x.Member.Role,
                x.Member.JoinedAt))
            .ToList();

        return Ok(members);
    }

    /// <summary>
    /// Removes a member from the organization. Only the owner can do this.
    /// </summary>
    [HttpDelete("api/orgs/{orgId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid orgId, Guid userId, [FromQuery] Guid requesterUserId)
    {
        var grain = _grainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.RemoveMember(userId, requesterUserId);
        return NoContent();
    }
}

public record CreateOrgRequest(Guid OrgId, string Name);
public record UpdateOrgRequest(string Name);
public record SendInvitationRequest(string Email, string Role);
public record OrgDto(Guid OrgId, string Name, string Role, bool IsDeleted);
public record OrgMemberDto(Guid UserId, string DisplayName, string AvatarUrl, string Role, DateTime JoinedAt);