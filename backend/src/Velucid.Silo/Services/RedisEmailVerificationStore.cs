using System.Text.Json;
using StackExchange.Redis;

namespace Velucid.Silo.Services;

public sealed class RedisEmailVerificationStore : IEmailVerificationStore
{
    private const string KeyPrefix = "email-verification:";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    private readonly IConnectionMultiplexer _redis;

    public RedisEmailVerificationStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task SetAsync(Guid userId, string token, string email, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{userId}";
        var payload = new EmailVerificationToken
        {
            Token = token,
            Email = email,
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime)
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        await db.StringSetAsync(key, json, TokenLifetime);
    }

    public async Task<EmailVerificationToken?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{userId}";
        var json = (byte[]?)await db.StringGetAsync(key);

        if (json is null)
            return null;

        var payload = JsonSerializer.Deserialize<EmailVerificationToken>((ReadOnlySpan<byte>)json);
        if (payload is null || payload.ExpiresAt < DateTimeOffset.UtcNow)
            return null;

        return payload;
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{userId}";
        await db.KeyDeleteAsync(key);
    }
}