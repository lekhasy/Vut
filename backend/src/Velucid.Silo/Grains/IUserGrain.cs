using Velucid.Silo.Models;

namespace Velucid.Silo.Grains;

/// <summary>
/// Grain interface for the User aggregate. Manages user creation, multi-provider
/// identity linking, email verification, and profile updates.
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
    /// Creates a new user with the specified identity provider and profile information.
    /// Idempotent: if the user already exists, returns the existing user ID without emitting events.
    /// </summary>
    /// <param name="providerId">The identity provider subject (e.g., "github|12345678").</param>
    /// <param name="providerName">The identity provider name (e.g., "github", "google").</param>
    /// <param name="displayName">The user's display name from the identity provider.</param>
    /// <param name="avatarUrl">The URL of the user's avatar.</param>
    /// <param name="email">The user's email address, if provided.</param>
    /// <returns>A <see cref="CreateUserResult"/> containing the user's ID.</returns>
    Task<CreateUserResult> CreateUser(
        string providerId, string providerName,
        string displayName, string avatarUrl, string? email);

    /// <summary>
    /// Links an additional identity provider to the user's account.
    /// No-op if the provider is already linked.
    /// </summary>
    /// <param name="providerId">The identity provider subject.</param>
    /// <param name="providerName">The identity provider name.</param>
    /// <param name="email">The email associated with this identity, if available.</param>
    Task LinkIdentity(
        string providerId, string providerName, string? email);

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
}
