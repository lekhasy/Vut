namespace Velucid.Silo.Services;

/// <summary>
/// Stores email verification tokens in Redis with a 15-minute TTL.
/// Tokens are keyed by user ID — requesting a new code overwrites the previous one.
/// </summary>
public sealed class EmailVerificationToken
{
    public required string Token { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public interface IEmailVerificationStore
{
    /// <summary>
    /// Stores a verification token for the given user, overwriting any existing token.
    /// </summary>
    Task SetAsync(Guid userId, string token, string email, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the stored verification token for the given user, or null if not found or expired.
    /// </summary>
    Task<EmailVerificationToken?> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Removes the verification token for the given user (e.g., after successful verification).
    /// </summary>
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
}