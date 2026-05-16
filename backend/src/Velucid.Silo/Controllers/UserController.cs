using Microsoft.AspNetCore.Mvc;
using Velucid.Silo.Grains;
using Velucid.Silo.Models;
using Velucid.Silo.Services;
using SignInResult = Velucid.Silo.Models.SignInResult;

namespace Velucid.Silo.Controllers;

[ApiController]
public class UserController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ISignInService _signInService;

    public UserController(IGrainFactory grainFactory, ISignInService signInService)
    {
        _grainFactory = grainFactory;
        _signInService = signInService;
    }

    /// <summary>
    /// Signs in a user by resolving their identity provider claims to an existing user
    /// or creating a new one. Queries the read model for lookups, delegates to grains
    /// for aggregate operations.
    /// </summary>
    [HttpPost("api/auth/sign-in")]
    public async Task<ActionResult<SignInResult>> SignIn([FromBody] SignInRequest request)
    {
        var result = await _signInService.SignIn(
            request.Sub,
            request.ProviderName,
            request.DisplayName,
            request.AvatarUrl,
            request.Email);

        return Ok(result);
    }

    [HttpPut("api/users/{userId:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(
        Guid userId, [FromBody] UpdateProfileRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        await grain.UpdateProfile(request.DisplayName, request.AvatarUrl);
        return Ok();
    }

    [HttpPost("api/users/{userId:guid}/request-email-verification")]
    public async Task<ActionResult<EmailVerificationResponse>> RequestEmailVerification(
        Guid userId, [FromBody] RequestEmailVerificationRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        var token = await grain.RequestEmailVerification(request.Email);
        return Ok(new EmailVerificationResponse(token));
    }

    [HttpPost("api/users/{userId:guid}/verify-email")]
    public async Task<IActionResult> VerifyEmail(
        Guid userId, [FromBody] VerifyEmailRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        await grain.VerifyEmail(request.Token);
        return Ok();
    }
}

public record SignInRequest(
    string Sub,
    string ProviderName,
    string DisplayName,
    string AvatarUrl,
    string? Email);

public record UpdateProfileRequest(
    string DisplayName,
    string AvatarUrl);

public record RequestEmailVerificationRequest(string Email);
public record VerifyEmailRequest(string Token);
public record EmailVerificationResponse(string Token);