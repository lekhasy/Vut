using Velucid.Silo.Events;
using Velucid.Silo.Models;

namespace Velucid.Silo.Grains;

/// <summary>
/// Event-sourced grain that manages the User aggregate. Persists events to the
/// <c>user-{userId}</c> KurrentDB stream and rebuilds state on activation.
/// </summary>
public class UserGrain : EventSourcedGrain<UserState>, IUserGrain
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserGrain"/> class.
    /// </summary>
    /// <param name="eventStreamClient">The event stream client for event persistence.</param>
    public UserGrain(IEventStreamClient eventStreamClient) : base(eventStreamClient) { }

    /// <inheritdoc/>
    protected override string BuildStreamId() => $"user-{this.GetPrimaryKey()}";

    /// <inheritdoc/>
    protected override void Apply(UserState state, IEvent @event)
    {
        switch (@event)
        {
            case UserCreatedEvent e:
                state.UserId = e.UserId;
                state.DisplayName = e.DisplayName;
                state.AvatarUrl = e.AvatarUrl;
                state.Email = e.Email;
                state.IsEmailVerified = false;
                state.Exists = true;
                break;

            case IdentityLinkedEvent e:
                state.Identities[e.ProviderId] = new IdentityEntry
                {
                    ProviderId = e.ProviderId,
                    ProviderName = e.ProviderName,
                    Email = e.Email,
                    LinkedAt = e.Timestamp
                };
                break;

            case UserProfileUpdatedEvent e:
                state.DisplayName = e.DisplayName;
                state.AvatarUrl = e.AvatarUrl;
                break;

            case EmailVerificationRequestedEvent e:
                state.EmailVerificationToken = e.Token;
                state.EmailVerificationTokenExpiresAt = e.Timestamp.AddMinutes(15);
                break;

            case EmailVerifiedEvent e:
                state.IsEmailVerified = true;
                state.Email = e.Email;
                state.EmailVerificationToken = string.Empty;
                break;
        }
    }

    /// <inheritdoc/>
    public async Task<CreateUserResult> CreateUser(
        string providerId, string providerName,
        string displayName, string avatarUrl, string? email)
    {
        if (State.Exists)
            return new CreateUserResult(State.UserId);

        var userId = this.GetPrimaryKey();
        var now = DateTimeOffset.UtcNow;

        await EmitEvent(new UserCreatedEvent(
            userId, displayName, avatarUrl, email,
            userId, now));

        await EmitEvent(new IdentityLinkedEvent(
            userId, providerId, providerName, email,
            userId, now));

        return new CreateUserResult(userId);
    }

    /// <inheritdoc/>
    public async Task LinkIdentity(
        string providerId, string providerName, string? email)
    {
        if (State.Identities.ContainsKey(providerId))
            return;

        await EmitEvent(new IdentityLinkedEvent(
            State.UserId, providerId, providerName, email,
            State.UserId, DateTimeOffset.UtcNow));
    }

    /// <inheritdoc/>
    public async Task UpdateProfile(string displayName, string avatarUrl)
    {
        if (!State.Exists)
            throw new InvalidOperationException("User does not exist.");

        if (State.DisplayName == displayName && State.AvatarUrl == avatarUrl)
            return;

        await EmitEvent(new UserProfileUpdatedEvent(
            State.UserId, displayName, avatarUrl,
            State.UserId, DateTimeOffset.UtcNow));
    }

    /// <inheritdoc/>
    public async Task<string> RequestEmailVerification(string email)
    {
        var token = Random.Shared.Next(100000, 999999).ToString();

        await EmitEvent(new EmailVerificationRequestedEvent(
            State.UserId, email, token,
            State.UserId, DateTimeOffset.UtcNow));

        return token;
    }

    /// <inheritdoc/>
    public async Task VerifyEmail(string token)
    {
        if (State.EmailVerificationToken != token)
            throw new InvalidOperationException("Invalid verification token.");

        if (DateTimeOffset.UtcNow > State.EmailVerificationTokenExpiresAt)
            throw new InvalidOperationException("Verification token expired.");

        await EmitEvent(new EmailVerifiedEvent(
            State.UserId, State.Email!,
            State.UserId, DateTimeOffset.UtcNow));
    }
}
