namespace Vut.ReadModel.Entities;

public sealed class OrgMemberProjection
{
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}
