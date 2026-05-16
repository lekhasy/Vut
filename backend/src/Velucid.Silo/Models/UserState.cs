namespace Velucid.Silo.Models;

/// <summary>
/// The aggregate state for the User grain, rebuilt by replaying events from KurrentDB.
/// </summary>
public class UserState
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public Guid UserId { get; internal set; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string DisplayName { get; internal set; } = string.Empty;

    /// <summary>
    /// The URL of the user's avatar.
    /// </summary>
    public string AvatarUrl { get; internal set; } = string.Empty;

    /// <summary>
    /// The user's verified or pending email address.
    /// </summary>
    public string? Email { get; internal set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    public bool IsEmailVerified { get; internal set; }

    /// <summary>
    /// The map of linked identity providers, keyed by Auth0 sub.
    /// </summary>
    public Dictionary<string, IdentityEntry> Identities { get; internal set; } = new();
}