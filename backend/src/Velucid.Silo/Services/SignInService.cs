using Velucid.Silo.Grains;
using Velucid.Silo.Models;
using SignInResult = Velucid.Silo.Models.SignInResult;

namespace Velucid.Silo.Services;

/// <summary>
/// Orchestrates the Auth0 sign-in flow:
/// 1. Check whether the Auth0 sub is already linked via <see cref="IIdentityGrain"/>
/// 2. If not linked, create the user via <see cref="IUserGrain"/> then link it
/// 3. Return the resolved <see cref="SignInResult"/>
/// </summary>
public sealed class SignInService : ISignInService
{
    private readonly IGrainFactory _grainFactory;

    public SignInService(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    /// <inheritdoc/>
    public async Task<SignInResult> SignIn(
        string sub, string providerName,
        string displayName, string avatarUrl, string? email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var identityGrain = _grainFactory.GetGrain<IIdentityGrain>(sub);

        // Step 1: check existing link
        Guid userId;
        bool isNewUser;
        try
        {
            userId = await identityGrain.GetLinkedUserId();
            isNewUser = false;
        }
        catch (IdentityOrphanedException)
        {
            // Step 2a: not linked — record the link first so a retry after failure is safe
            var newUserId = Guid.NewGuid();
            await identityGrain.SetLinkedUserId(newUserId, providerName, email);

            // Step 2b: create the user with that link recorded
            var userGrain = _grainFactory.GetGrain<IUserGrain>(newUserId);
            await userGrain.CreateUser(sub, providerName, displayName, avatarUrl, email);

            userId = newUserId;
            isNewUser = true;
        }

        // Step 3: fetch user info and return result
        var userGrainForInfo = _grainFactory.GetGrain<IUserGrain>(userId);
        UserInfo? userInfoFromGrain = null;
        try
        {
            userInfoFromGrain = await userGrainForInfo.GetUserInfo();
        }
        catch (InvalidOperationException)
        {
            // Grain not yet hydrated from stream — fall through to use locally-constructed info
        }

        var userInfo = userInfoFromGrain
            ?? new UserInfo(userId, displayName, avatarUrl, email, IsEmailVerified: false);

        return new SignInResult(
            userInfo.UserId,
            userInfo.DisplayName,
            userInfo.AvatarUrl,
            userInfo.Email,
            userInfo.IsEmailVerified,
            isNewUser);
    }
}
