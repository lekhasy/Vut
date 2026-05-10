namespace Vut.Silo.Models;

/// <summary>
/// The aggregate state for the User grain, rebuilt by replaying events from KurrentDB.
/// </summary>
public class UserState
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The URL of the user's avatar.
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    /// The user's verified or pending email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// The current email verification token, if a verification is pending.
    /// </summary>
    public string EmailVerificationToken { get; set; } = string.Empty;

    /// <summary>
    /// The UTC expiry time for the current email verification token.
    /// </summary>
    public DateTimeOffset EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>
    /// The map of linked identity providers, keyed by provider ID.
    /// </summary>
    public Dictionary<string, IdentityEntry> Identities { get; set; } = new();

    /// <summary>
    /// Whether this user aggregate has been created (has a <see cref="UserCreatedEvent"/>).
    /// </summary>
    public bool Exists { get; set; }
}
