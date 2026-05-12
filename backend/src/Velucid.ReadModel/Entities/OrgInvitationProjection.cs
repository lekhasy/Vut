namespace Velucid.ReadModel.Entities;

public sealed class OrgInvitationProjection
{
    public Guid OrgId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
    public Guid? UserId { get; set; }
}
