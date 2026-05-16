namespace Velucid.Silo.Models;

/// <summary>
/// The result returned by the sign-in flow, containing the resolved user information.
/// Whether the user was found by existing provider, linked via email, or newly created,
/// this result carries everything the caller needs to establish a session.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="AvatarUrl">The URL of the user's avatar.</param>
/// <param name="Email">The user's email address, if known.</param>
/// <param name="IsEmailVerified">Whether the user's email has been verified.</param>
/// <param name="IsNewUser">Whether this sign-in resulted in a new user being created.</param>
[GenerateSerializer]
[Immutable]
public record SignInResult(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string AvatarUrl,
    [property: Id(3)] string? Email,
    [property: Id(4)] bool IsEmailVerified,
    [property: Id(5)] bool IsNewUser);
