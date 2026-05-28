using Velucid.Silo.Models;

namespace Velucid.Silo.Grains;

/// <summary>
/// Grain interface for managing Auth0 identity-to-user linkages.
/// Each grain instance is keyed by the Auth0 subject identifier (sub).
/// </summary>
/// <remarks>
/// <para>
/// The grain stores a single piece of state: the linked <see cref="Guid">UserId</see>.
/// It does NOT create users — that responsibility belongs to <see cref="IUserGrain"/>.
/// The <see cref="SignInService"/> coordinates the full sign-in flow across both grains.
/// </para>
/// </remarks>
public interface IIdentityGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records that the given Auth0 sub has been linked to a user account.
    /// Idempotent — subsequent calls with the same UserId are no-ops.
    /// </summary>
    /// <param name="userId">The UserId to link this identity to.</param>
    /// <param name="providerName">The Auth0 provider name (e.g., "github").</param>
    /// <param name="email">The email associated with this identity, if available.</param>
    Task SetLinkedUserId(Guid userId, string providerName, string? email);

    /// <summary>
    /// Returns the linked UserId for this Auth0 sub.
    /// Throws <see cref="IdentityOrphanedException"/> if the identity has not been linked.
    /// </summary>
    /// <returns>The linked UserId.</returns>
    /// <exception cref="IdentityOrphanedException">Thrown when the identity has not been linked to a user.</exception>
    Task<Guid> GetLinkedUserId();
}
