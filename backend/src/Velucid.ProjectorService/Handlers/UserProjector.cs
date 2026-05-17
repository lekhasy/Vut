using System.Text.Json;
using Grpc.Core;
using KurrentDB.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Velucid.ProjectorService.Handlers;
using Velucid.ReadModel;
using Velucid.ReadModel.Entities;

namespace Velucid.ProjectorService.Handlers;

public sealed class UserProjector : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private const string SubscriptionGroup = "velucid-projector-user";
    private const string UserStreamPrefix = "user-";

    private readonly KurrentDBPersistentSubscriptionsClient _psClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserProjector> _logger;

    public UserProjector(
        KurrentDBPersistentSubscriptionsClient psClient,
        IServiceScopeFactory scopeFactory,
        ILogger<UserProjector> logger)
    {
        _psClient = psClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UserProjector starting, subscribing to persistent subscription {Group}", SubscriptionGroup);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SubscribeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserProjector subscription dropped, reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("UserProjector stopped");
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        var settings = new PersistentSubscriptionSettings(resolveLinkTos: true);
        try
        {
            await _psClient.CreateToAllAsync(
                SubscriptionGroup, StreamFilter.Prefix("user"), settings);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            _logger.LogInformation("Subscription group {Group} already exists, using existing", SubscriptionGroup);
        }

        await using var subscription = _psClient.SubscribeToAll(
            SubscriptionGroup, cancellationToken: ct);

        await foreach (var message in subscription.Messages.WithCancellation(ct))
        {
            if (message is not PersistentSubscriptionMessage.Event(var resolved, _))
                continue;

            try
            {
                await HandleEventAsync(resolved, ct);
                await subscription.Ack(resolved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to project event {EventType} from stream {StreamId} at position {Position}",
                    resolved.Event.EventType, resolved.Event.EventStreamId, resolved.Event.EventNumber);
                await subscription.Nack(PersistentSubscriptionNakEventAction.Retry, ex.Message, resolved);
            }
        }
    }

    private async Task HandleEventAsync(ResolvedEvent resolved, CancellationToken ct)
    {
        var eventType = resolved.Event.EventType;
        var streamId = resolved.Event.EventStreamId;

        // Skip system events and non-user streams
        if (resolved.Event.EventType.StartsWith("$") || !streamId.StartsWith(UserStreamPrefix))
            return;

        _logger.LogDebug("Projecting {EventType} from {StreamId}", eventType, streamId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        switch (eventType)
        {
            case "UserCreated":
                await ProjectUserCreatedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "IdentityLinked":
                await ProjectIdentityLinkedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "UserProfileUpdated":
                await ProjectProfileUpdatedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "EmailVerified":
                await ProjectEmailVerifiedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            default:
                _logger.LogDebug("Unknown event type {EventType}, skipping", eventType);
                return;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task ProjectUserCreatedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<UserCreatedPayload>(data);

        var projection = await db.UserProjections.FindAsync([e.UserId], ct);
        if (projection is null)
        {
            projection = new UserProjection
            {
                UserId = e.UserId,
                DisplayName = e.DisplayName,
                AvatarUrl = e.AvatarUrl,
                Email = e.Email,
                IsEmailVerified = false,
                CreatedAt = e.Timestamp.DateTime,
                UpdatedAt = e.Timestamp.DateTime,
            };
            db.UserProjections.Add(projection);
        }
        else
        {
            projection.DisplayName = e.DisplayName;
            projection.AvatarUrl = e.AvatarUrl;
            projection.Email = e.Email;
            projection.UpdatedAt = e.Timestamp.DateTime;
        }
    }

    private static async Task ProjectIdentityLinkedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<IdentityLinkedPayload>(data);

        var identity = await db.UserIdentities.FindAsync([e.UserId, e.Sub], ct);
        if (identity is null)
        {
            identity = new UserIdentity
            {
                UserId = e.UserId,
                Sub = e.Sub,
                ProviderName = e.ProviderName,
                Email = e.Email,
                LinkedAt = e.Timestamp.DateTime,
            };
            db.UserIdentities.Add(identity);
        }
        else
        {
            identity.ProviderName = e.ProviderName;
            identity.Email = e.Email;
            identity.LinkedAt = e.Timestamp.DateTime;
        }
    }

    private static async Task ProjectProfileUpdatedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<ProfileUpdatedPayload>(data);

        var projection = await db.UserProjections.FindAsync([e.UserId], ct);
        if (projection is null) return;

        projection.DisplayName = e.DisplayName;
        projection.AvatarUrl = e.AvatarUrl;
        projection.UpdatedAt = e.Timestamp.DateTime;
    }

    private static async Task ProjectEmailVerifiedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<EmailVerifiedPayload>(data);

        var projection = await db.UserProjections.FindAsync([e.UserId], ct);
        if (projection is null) return;

        projection.IsEmailVerified = true;
        projection.Email = e.Email;
        projection.UpdatedAt = e.Timestamp.DateTime;
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> data)
        => JsonSerializer.Deserialize<T>(data.Span, JsonOptions)!;

    // Lightweight payloads matching the JSON shape of Silo events (camelCase).
    private record UserCreatedPayload(Guid UserId, string DisplayName, string? AvatarUrl, string? Email, DateTimeOffset Timestamp);
    private record IdentityLinkedPayload(Guid UserId, string Sub, string ProviderName, string? Email, DateTimeOffset Timestamp);
    private record ProfileUpdatedPayload(Guid UserId, string DisplayName, string? AvatarUrl, DateTimeOffset Timestamp);
    private record EmailVerifiedPayload(Guid UserId, string? Email, DateTimeOffset Timestamp);
}