using Velucid.Silo.Models;

namespace Velucid.Silo.Grains;

/// <summary>
/// Grain interface for the User aggregate. Manages identity linking, email verification, and profile updates.
/// </summary>
/// <remarks>
/// <para>
/// Each grain instance maps to a single user and is activated by calling
/// <c>grainFactory.GetGrain&lt;IUserGrain&gt;(userId)</c>.
/// The grain persists events to the <c>user-{userId}</c> KurrentDB stream.
/// </para>
/// </remarks>
public interface IUserGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Creates a new user and links an Auth0 identity in one atomic operation.
    /// Idempotent: if the user already exists, returns without emitting events.
    /// </summary>
    Task<CreateUserResult> CreateUser(
        string sub, string providerName,
        string displayName, string avatarUrl, string? email);

    /// <summary>
    /// Links an Auth0 identity to this user grain.
    /// Idempotent: if the identity is already linked, returns without emitting events.
    /// </summary>
    /// <param name="sub">The Auth0 subject identifier (e.g., "github|12345678").</param>
    /// <param name="providerName">The Auth0 provider name (e.g., "github").</param>
    /// <param name="displayName">The user's display name.</param>
    /// <param name="avatarUrl">The URL of the user's avatar.</param>
    /// <param name="email">The user's email address, if provided.</param>
    /// <returns>A <see cref="CreateUserResult"/> containing the user's ID.</returns>
    Task<CreateUserResult> LinkIdentity(
        string sub, string providerName,
        string displayName, string avatarUrl, string? email);

    /// <summary>
    /// Updates the user's profile display name and avatar URL.
    /// No-op if neither value has changed. Throws if the user does not exist.
    /// </summary>
    /// <param name="displayName">The new display name.</param>
    /// <param name="avatarUrl">The new avatar URL.</param>
    Task UpdateProfile(string displayName, string avatarUrl);

    /// <summary>
    /// Generates a 6-digit email verification code with a 15-minute expiry.
    /// The API controller is responsible for sending the email after receiving the token.
    /// </summary>
    /// <param name="email">The email address to verify.</param>
    /// <returns>The 6-digit verification code.</returns>
    Task<string> RequestEmailVerification(string email);

    /// <summary>
    /// Verifies the user's email using the provided token. Throws if the token
    /// is invalid or expired.
    /// </summary>
    /// <param name="token">The 6-digit verification code.</param>
    Task VerifyEmail(string token);

    /// <summary>
    /// Returns the user's public info. Throws if the user does not exist.
    /// </summary>
    Task<UserInfo> GetUserInfo();
}