namespace Velucid.ReadModel.Entities;

public sealed class UserIdentity
{
    public Guid UserId { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime LinkedAt { get; set; }

    public UserProjection User { get; set; } = null!;
}
