namespace Velucid.Silo.Models;

/// <summary>
/// Represents a linked identity provider entry for a user.
/// </summary>
public class IdentityEntry
{
    /// <summary>
    /// The Auth0 subject identifier (e.g., "github|12345678").
    /// </summary>
    public string Sub { get; internal set; } = string.Empty;

    /// <summary>
    /// The identity provider name (e.g., "github", "google", "microsoft").
    /// </summary>
    public string ProviderName { get; internal set; } = string.Empty;

    /// <summary>
    /// The email associated with this identity, if available.
    /// </summary>
    public string? Email { get; internal set; }

    /// <summary>
    /// The UTC timestamp when this identity was linked.
    /// </summary>
    public DateTimeOffset LinkedAt { get; internal set; }
}