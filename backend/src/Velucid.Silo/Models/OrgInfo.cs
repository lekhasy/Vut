using Orleans;

namespace Velucid.Silo.Models;

/// <summary>
/// Public organization information returned by grain methods.
/// Not persisted — only used as a DTO across grain boundaries.
/// </summary>
[GenerateSerializer]
[Immutable]
public record OrgInfo(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] string Name,
    [property: Id(2)] bool IsDeleted);

/// <summary>
/// Organization member information returned by grain methods.
/// </summary>
[GenerateSerializer]
[Immutable]
public record OrgMemberInfo(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string Role);