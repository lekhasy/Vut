using Microsoft.AspNetCore.Mvc;
using Velucid.Silo.Grains;

namespace Velucid.Silo.Controllers;

/// <summary>
/// API controller for user operations. Delegates to the <see cref="IUserGrain"/>
/// via <see cref="IGrainFactory"/>.
/// </summary>
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserController"/> class.
    /// </summary>
    /// <param name="grainFactory">The Orleans grain factory for activating user grains.</param>
    public UserController(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    /// <summary>
    /// Creates a new user with the specified identity and profile information.
    /// </summary>
    /// <param name="request">The user creation request.</param>
    /// <returns>The created user result with the assigned user ID.</returns>
    [HttpPost("create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var userId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        var result = await grain.CreateUser(
            request.ProviderId, request.ProviderName,
            request.DisplayName, request.AvatarUrl, request.Email);

        return CreatedAtAction(nameof(CreateUser), result);
    }

    /// <summary>
    /// Links an additional identity provider to an existing user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="request">The identity linking request.</param>
    [HttpPost("{userId:guid}/link-identity")]
    public async Task<IActionResult> LinkIdentity(
        Guid userId, [FromBody] LinkIdentityRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        await grain.LinkIdentity(
            request.ProviderId, request.ProviderName, request.Email);

        return Ok();
    }

    /// <summary>
    /// Updates a user's profile information.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="request">The profile update request.</param>
    [HttpPut("{userId:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(
        Guid userId, [FromBody] UpdateProfileRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        await grain.UpdateProfile(request.DisplayName, request.AvatarUrl);

        return Ok();
    }

    /// <summary>
    /// Requests email verification for a user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="request">The email verification request.</param>
    /// <returns>The generated verification token.</returns>
    [HttpPost("{userId:guid}/request-email-verification")]
    public async Task<IActionResult> RequestEmailVerification(
        Guid userId, [FromBody] RequestEmailVerificationRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        var token = await grain.RequestEmailVerification(request.Email);

        return Ok(new { Token = token });
    }

    /// <summary>
    /// Verifies a user's email address using the provided token.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="request">The email verification request containing the token.</param>
    [HttpPost("{userId:guid}/verify-email")]
    public async Task<IActionResult> VerifyEmail(
        Guid userId, [FromBody] VerifyEmailRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        await grain.VerifyEmail(request.Token);

        return Ok();
    }
}

/// <summary>
/// Request body for creating a new user.
/// </summary>
/// <param name="ProviderId">The identity provider subject (e.g., "github|12345678").</param>
/// <param name="ProviderName">The identity provider name (e.g., "github").</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="AvatarUrl">The URL of the user's avatar.</param>
/// <param name="Email">The user's email address, if provided.</param>
public record CreateUserRequest(
    string ProviderId,
    string ProviderName,
    string DisplayName,
    string AvatarUrl,
    string? Email);

/// <summary>
/// Request body for linking an identity provider to an existing user.
/// </summary>
/// <param name="ProviderId">The identity provider subject.</param>
/// <param name="ProviderName">The identity provider name.</param>
/// <param name="Email">The email associated with this identity, if available.</param>
public record LinkIdentityRequest(
    string ProviderId,
    string ProviderName,
    string? Email);

/// <summary>
/// Request body for updating a user's profile.
/// </summary>
/// <param name="DisplayName">The new display name.</param>
/// <param name="AvatarUrl">The new avatar URL.</param>
public record UpdateProfileRequest(
    string DisplayName,
    string AvatarUrl);

/// <summary>
/// Request body for requesting email verification.
/// </summary>
/// <param name="Email">The email address to verify.</param>
public record RequestEmailVerificationRequest(string Email);

/// <summary>
/// Request body for verifying an email address.
/// </summary>
/// <param name="Token">The 6-digit verification code.</param>
public record VerifyEmailRequest(string Token);
