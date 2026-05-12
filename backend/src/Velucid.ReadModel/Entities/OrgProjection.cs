namespace Velucid.ReadModel.Entities;

public sealed class OrgProjection
{
    public Guid OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
