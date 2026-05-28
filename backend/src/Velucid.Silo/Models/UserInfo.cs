using Orleans;

namespace Velucid.Silo.Models;

/// <summary>
/// Public user information returned by grain methods.
/// Not persisted — only used as a DTO across grain boundaries.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="AvatarUrl">The URL of the user's avatar.</param>
/// <param name="Email">The user's email address, if known.</param>
/// <param name="IsEmailVerified">Whether the user's email has been verified.</param>
[GenerateSerializer]
[Immutable]
public record UserInfo(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string AvatarUrl,
    [property: Id(3)] string? Email,
    [property: Id(4)] bool IsEmailVerified);
