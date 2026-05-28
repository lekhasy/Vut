using Velucid.Silo.Events;
using Velucid.Silo.Models;
using Velucid.Silo.Services;

namespace Velucid.Silo.Grains;

/// <summary>
/// Event-sourced grain that manages the User aggregate. Persists events to the
/// <c>user-{userId}</c> KurrentDB stream and rebuilds state on activation.
/// </summary>
public class UserGrain : EventSourcedGrain<UserState>, IUserGrain
{
    private readonly IEmailVerificationStore _emailVerificationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserGrain"/> class.
    /// </summary>
    /// <param name="eventStreamClient">The event stream client for event persistence.</param>
    /// <param name="timeProvider">The time provider for UTC timestamps.</param>
    /// <param name="emailVerificationStore">The Redis-backed store for email verification tokens.</param>
    public UserGrain(
        IEventStreamClient eventStreamClient,
        TimeProvider timeProvider,
        IEmailVerificationStore emailVerificationStore)
        : base(eventStreamClient, timeProvider)
    {
        _emailVerificationStore = emailVerificationStore;
    }

    /// <inheritdoc/>
    protected override string BuildStreamId() => $"user-{this.GetPrimaryKey()}";

    /// <inheritdoc/>
    protected override void Apply(UserState state, IEvent @event)
    {
        switch (@event)
        {
            case UserRegisteredEvent e:
                state.UserId = e.UserId;
                state.DisplayName = e.DisplayName;
                state.AvatarUrl = e.AvatarUrl;
                state.Email = e.Email;
                state.IsEmailVerified = false;
                state.Identities[e.Sub] = new IdentityEntry
                {
                    Sub = e.Sub,
                    ProviderName = e.ProviderName,
                    Email = e.Email,
                    LinkedAt = e.Timestamp
                };
                break;

            case IdentityLinkedEvent e:
                state.Identities[e.Sub] = new IdentityEntry
                {
                    Sub = e.Sub,
                    ProviderName = e.ProviderName,
                    Email = e.Email,
                    LinkedAt = e.Timestamp
                };
                break;

            case UserProfileUpdatedEvent e:
                state.DisplayName = e.DisplayName;
                state.AvatarUrl = e.AvatarUrl;
                break;

            case EmailVerifiedEvent e:
                state.IsEmailVerified = true;
                state.Email = e.Email;
                break;
        }
    }

    /// <inheritdoc/>
    public async Task<CreateUserResult> CreateUser(
        string sub, string providerName,
        string displayName, string avatarUrl, string? email)
    {
        if (Exists)
            return new CreateUserResult(State.UserId);

        var userId = this.GetPrimaryKey();
        var now = UtcNow;

        await EmitEvent(new UserRegisteredEvent(
            userId, displayName, avatarUrl, email,
            sub, providerName,
            userId, now));

        return new CreateUserResult(userId);
    }

    /// <inheritdoc/>
    public async Task<CreateUserResult> LinkIdentity(
        string sub, string providerName,
        string displayName, string avatarUrl, string? email)
    {
        // Check if this specific identity is already linked
        if (State.Identities.ContainsKey(sub))
            return new CreateUserResult(State.UserId);

        var userId = this.GetPrimaryKey();
        var now = UtcNow;

        // Emit IdentityLinkedEvent — the user may already exist, but this is a new identity link
        await EmitEvent(new IdentityLinkedEvent(
            userId, sub, providerName, email,
            userId, now));

        return new CreateUserResult(userId);
    }

    /// <inheritdoc/>
    public async Task UpdateProfile(string displayName, string avatarUrl)
    {
        if (!Exists)
            throw new InvalidOperationException("User does not exist.");

        if (State.DisplayName == displayName && State.AvatarUrl == avatarUrl)
            return;

        await EmitEvent(new UserProfileUpdatedEvent(
            State.UserId, displayName, avatarUrl,
            State.UserId, UtcNow));
    }

    /// <inheritdoc/>
    public async Task<string> RequestEmailVerification(string email)
    {
        if (!Exists)
            throw new InvalidOperationException("User does not exist.");

        var token = Random.Shared.Next(100000, 999999).ToString();

        // Store in Redis — overwrites any previous token
        await _emailVerificationStore.SetAsync(State.UserId, token, email);

        // Emit event for audit trail and projector (if needed)
        await EmitEvent(new EmailVerificationRequestedEvent(
            State.UserId, email, token,
            State.UserId, UtcNow));

        return token;
    }

    /// <inheritdoc/>
    public async Task VerifyEmail(string token)
    {
        if (!Exists)
            throw new InvalidOperationException("User does not exist.");

        // Verify token against Redis store
        var stored = await _emailVerificationStore.GetAsync(State.UserId);
        if (stored is null)
            throw new InvalidOperationException("No pending verification. Request a new code.");

        if (stored.Token != token)
            throw new InvalidOperationException("Invalid verification token.");

        if (stored.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Verification token expired.");

        // Clear from Redis
        await _emailVerificationStore.DeleteAsync(State.UserId);

        // Emit event to mark email as verified in KurrentDB
        await EmitEvent(new EmailVerifiedEvent(
            State.UserId, State.Email!,
            State.UserId, UtcNow));
    }

    /// <inheritdoc/>
    public Task<UserInfo> GetUserInfo()
    {
        if (!Exists)
            throw new InvalidOperationException("User does not exist.");

        return Task.FromResult(new UserInfo(
            State.UserId,
            State.DisplayName,
            State.AvatarUrl,
            State.Email,
            State.IsEmailVerified));
    }

}