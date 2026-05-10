using Orleans;

namespace Vut.Silo.Models;

/// <summary>
/// The result returned when a user is created or when a duplicate creation is attempted.
/// </summary>
/// <param name="UserId">The unique identifier of the created (or existing) user.</param>
[GenerateSerializer]
[Immutable]
public record CreateUserResult([property: Id(0)] Guid UserId);
