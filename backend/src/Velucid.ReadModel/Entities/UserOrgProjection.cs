namespace Velucid.ReadModel.Entities;

public sealed class UserOrgProjection
{
    public Guid UserId { get; set; }
    public Guid OrgId { get; set; }
    public string Role { get; set; } = string.Empty;
}
