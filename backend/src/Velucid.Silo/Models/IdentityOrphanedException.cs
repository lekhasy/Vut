namespace Velucid.Silo.Models;

/// <summary>
/// Thrown when an identity grain is accessed but has not yet been linked to a user.
/// </summary>
public class IdentityOrphanedException : Exception
{
    /// <summary>
    /// The Auth0 subject identifier that is orphaned.
    /// </summary>
    public string Sub { get; }

    public IdentityOrphanedException(string sub)
        : base($"Identity '{sub}' has not been linked to a user. Complete the sign-in flow first.")
    {
        Sub = sub;
    }
}
