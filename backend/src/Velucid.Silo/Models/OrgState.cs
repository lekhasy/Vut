namespace Velucid.Silo.Models;

/// <summary>
/// Represents the state of an Organization aggregate, hydrated from the event stream.
/// </summary>
public sealed class OrgState
{
    public Guid OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<Guid, string> Members { get; set; } = new(); // UserId -> Role
    public List<InvitationRecord> Invitations { get; set; } = new();
}

public sealed class InvitationRecord
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid InviterUserId { get; set; }
    public DateTime InvitedAt { get; set; }
    public string Status { get; set; } = "Pending";
}