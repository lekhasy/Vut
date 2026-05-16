using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Velucid.ReadModel;
using Velucid.Silo.Grains;
using Velucid.Silo.Models;

namespace Velucid.Silo.Services;

public sealed class SignInService : ISignInService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ReadModelDbContext _db;
    private readonly ILogger<SignInService> _logger;

    public SignInService(
        IGrainFactory grainFactory,
        ReadModelDbContext db,
        ILogger<SignInService> logger)
    {
        _grainFactory = grainFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<SignInResult> SignIn(
        string sub, string providerName,
        string displayName, string avatarUrl, string? email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        // Look up by sub (Auth0 subject identifier) in the read model.
        var identity = await _db.UserIdentities
            .AsNoTracking()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Sub == sub);

        if (identity is not null)
        {
            _logger.LogDebug(
                "Sign-in: found existing user {UserId} by sub {Sub}",
                identity.UserId, sub);

            return new SignInResult(
                identity.UserId,
                identity.User.DisplayName,
                identity.User.AvatarUrl ?? string.Empty,
                identity.User.Email,
                identity.User.IsEmailVerified,
                IsNewUser: false);
        }

        // No match — create a new user.
        var newUserId = Guid.NewGuid();
        _logger.LogDebug(
            "Sign-in: creating new user {UserId} with sub {Sub}",
            newUserId, sub);

        var newGrain = _grainFactory.GetGrain<IUserGrain>(newUserId);
        await newGrain.CreateUser(sub, providerName, displayName, avatarUrl, email);

        return new SignInResult(
            newUserId,
            displayName,
            avatarUrl,
            email,
            IsEmailVerified: false,
            IsNewUser: true);
    }
}