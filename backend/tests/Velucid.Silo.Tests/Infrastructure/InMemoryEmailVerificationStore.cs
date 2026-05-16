using Velucid.Silo.Services;

namespace Velucid.Silo.Tests.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="IEmailVerificationStore"/> for testing.
/// Tokens expire based on the provided <see cref="TimeProvider"/> — when tests
/// advance the fake clock, the store's expiry checks follow accordingly.
/// </summary>
public sealed class InMemoryEmailVerificationStore : IEmailVerificationStore
{
    private readonly Dictionary<Guid, StoredToken> _tokens = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryEmailVerificationStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task SetAsync(Guid userId, string token, string email, CancellationToken ct = default)
    {
        _tokens[userId] = new StoredToken
        {
            Token = token,
            Email = email,
            ExpiresAt = _timeProvider.GetUtcNow().Add(TimeSpan.FromMinutes(15))
        };
        return Task.CompletedTask;
    }

    public Task<EmailVerificationToken?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        _tokens.TryGetValue(userId, out var stored);
        if (stored is null || stored.ExpiresAt < _timeProvider.GetUtcNow())
            return Task.FromResult<EmailVerificationToken?>(null);
        return Task.FromResult<EmailVerificationToken?>(new EmailVerificationToken
        {
            Token = stored.Token,
            Email = stored.Email,
            ExpiresAt = stored.ExpiresAt
        });
    }

    public Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        _tokens.Remove(userId);
        return Task.CompletedTask;
    }

    private sealed class StoredToken
    {
        public required string Token { get; init; }
        public required string Email { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }
}