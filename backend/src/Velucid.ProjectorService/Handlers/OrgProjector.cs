using System.Text.Json;
using Grpc.Core;
using KurrentDB.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Velucid.ProjectorService.Services;
using Velucid.ReadModel;
using Velucid.ReadModel.Entities;

namespace Velucid.ProjectorService.Handlers;

public sealed class OrgProjector : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private const string SubscriptionGroup = "velucid-projector-org";
    private const string OrgStreamPrefix = "org-";

    private readonly KurrentDBPersistentSubscriptionsClient _psClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OpenFgaTupleSync _tupleSync;
    private readonly ILogger<OrgProjector> _logger;

    public OrgProjector(
        KurrentDBPersistentSubscriptionsClient psClient,
        IServiceScopeFactory scopeFactory,
        OpenFgaTupleSync tupleSync,
        ILogger<OrgProjector> logger)
    {
        _psClient = psClient;
        _scopeFactory = scopeFactory;
        _tupleSync = tupleSync;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrgProjector starting, subscribing to persistent subscription {Group}", SubscriptionGroup);

        await _tupleSync.InitializeAsync();

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
                _logger.LogError(ex, "OrgProjector subscription dropped, reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("OrgProjector stopped");
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        var settings = new PersistentSubscriptionSettings(resolveLinkTos: true);
        try
        {
            await _psClient.CreateToAllAsync(
                SubscriptionGroup, StreamFilter.Prefix("org"), settings);
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

        if (resolved.Event.EventType.StartsWith("$") || !streamId.StartsWith(OrgStreamPrefix))
            return;

        _logger.LogDebug("Projecting {EventType} from {StreamId}", eventType, streamId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        switch (eventType)
        {
            case "OrgCreated":
                await ProjectOrgCreatedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "OrgRenamed":
                await ProjectOrgRenamedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "OrgDeleted":
                await ProjectOrgDeletedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "MemberAdded":
                await ProjectMemberAddedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "MemberRemoved":
                await ProjectMemberRemovedAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            case "InvitationSent":
                await ProjectInvitationSentAsync(db, resolved.Event.Data.ToArray(), ct);
                break;
            default:
                _logger.LogDebug("Unknown event type {EventType}, skipping", eventType);
                return;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ProjectOrgCreatedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<OrgCreatedPayload>(data);

        var org = await db.OrgProjections.FindAsync([e.OrgId], ct);
        if (org is null)
        {
            org = new OrgProjection
            {
                OrgId = e.OrgId,
                Name = e.Name,
                IsDeleted = false,
                CreatedAt = e.Timestamp.DateTime,
                UpdatedAt = e.Timestamp.DateTime,
            };
            db.OrgProjections.Add(org);
        }
        else
        {
            org.Name = e.Name;
            org.IsDeleted = false;
            org.UpdatedAt = e.Timestamp.DateTime;
        }

        // Add owner as member
        var existingMember = await db.OrgMemberProjections.FindAsync([e.OrgId, e.OwnerUserId], ct);
        if (existingMember is null)
        {
            var member = new OrgMemberProjection
            {
                OrgId = e.OrgId,
                UserId = e.OwnerUserId,
                Role = "Owner",
                JoinedAt = e.Timestamp.DateTime,
            };
            db.OrgMemberProjections.Add(member);
        }

        // Also add to user_org_projection for fast lookup
        var existingUserOrg = await db.UserOrgProjections.FindAsync([e.OwnerUserId, e.OrgId], ct);
        if (existingUserOrg is null)
        {
            var userOrg = new UserOrgProjection
            {
                UserId = e.OwnerUserId,
                OrgId = e.OrgId,
                Role = "Owner",
            };
            db.UserOrgProjections.Add(userOrg);
        }

        // Sync owner tuple to OpenFGA
        await _tupleSync.WriteTuplesAsync([
            ($"user:{e.OwnerUserId}", "owner", $"organization:{e.OrgId}")
        ]);
    }

    private static async Task ProjectOrgRenamedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<OrgRenamedPayload>(data);

        var org = await db.OrgProjections.FindAsync([e.OrgId], ct);
        if (org is null) return;

        org.Name = e.Name;
        org.UpdatedAt = e.Timestamp.DateTime;
    }

    private static async Task ProjectOrgDeletedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<OrgDeletedPayload>(data);

        var org = await db.OrgProjections.FindAsync([e.OrgId], ct);
        if (org is null) return;

        org.IsDeleted = true;
        org.UpdatedAt = e.Timestamp.DateTime;
    }

    private async Task ProjectMemberAddedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<MemberAddedPayload>(data);

        var existing = await db.OrgMemberProjections.FindAsync([e.OrgId, e.UserId], ct);
        if (existing is null)
        {
            var member = new OrgMemberProjection
            {
                OrgId = e.OrgId,
                UserId = e.UserId,
                Role = e.Role,
                JoinedAt = e.Timestamp.DateTime,
            };
            db.OrgMemberProjections.Add(member);
        }
        else
        {
            existing.Role = e.Role;
        }

        // Update user_org_projection
        var existingUserOrg = await db.UserOrgProjections.FindAsync([e.UserId, e.OrgId], ct);
        if (existingUserOrg is null)
        {
            var userOrg = new UserOrgProjection
            {
                UserId = e.UserId,
                OrgId = e.OrgId,
                Role = e.Role,
            };
            db.UserOrgProjections.Add(userOrg);
        }
        else
        {
            existingUserOrg.Role = e.Role;
        }

        // Sync member tuple to OpenFGA
        await _tupleSync.WriteTuplesAsync([
            ($"user:{e.UserId}", e.Role.ToLowerInvariant(), $"organization:{e.OrgId}")
        ]);
    }

    private async Task ProjectMemberRemovedAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<MemberRemovedPayload>(data);

        var member = await db.OrgMemberProjections.FindAsync([e.OrgId, e.UserId], ct);
        var removedRole = member?.Role;

        if (member != null)
        {
            db.OrgMemberProjections.Remove(member);
        }

        var userOrg = await db.UserOrgProjections.FindAsync([e.UserId, e.OrgId], ct);
        if (userOrg != null)
        {
            db.UserOrgProjections.Remove(userOrg);
        }

        // Delete member tuple from OpenFGA
        if (removedRole != null)
        {
            await _tupleSync.DeleteTuplesAsync([
                ($"user:{e.UserId}", removedRole.ToLowerInvariant(), $"organization:{e.OrgId}")
            ]);
        }
    }

    private static async Task ProjectInvitationSentAsync(ReadModelDbContext db, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var e = Deserialize<InvitationSentPayload>(data);

        var existing = await db.OrgInvitationProjections.FindAsync([e.OrgId, e.Email], ct);
        if (existing is null)
        {
            var invitation = new OrgInvitationProjection
            {
                OrgId = e.OrgId,
                Email = e.Email,
                Role = e.Role,
                Status = "Pending",
                InvitedAt = e.Timestamp.DateTime,
            };
            db.OrgInvitationProjections.Add(invitation);
        }
        else
        {
            existing.Role = e.Role;
            if (existing.Status != "Accepted")
                existing.Status = "Pending";
            existing.InvitedAt = e.Timestamp.DateTime;
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> data)
        => JsonSerializer.Deserialize<T>(data.Span, JsonOptions)!;

    // Lightweight payloads matching the JSON shape of Silo events (camelCase).
    private record OrgCreatedPayload(Guid OrgId, string Name, Guid OwnerUserId, DateTimeOffset Timestamp);
    private record OrgRenamedPayload(Guid OrgId, string Name, Guid RenamedBy, DateTimeOffset Timestamp);
    private record OrgDeletedPayload(Guid OrgId, Guid DeletedBy, DateTimeOffset Timestamp);
    private record MemberAddedPayload(Guid OrgId, Guid UserId, string Role, DateTimeOffset Timestamp);
    private record MemberRemovedPayload(Guid OrgId, Guid UserId, Guid RemovedBy, DateTimeOffset Timestamp);
    private record InvitationSentPayload(Guid OrgId, string Email, string Role, Guid InviterUserId, DateTimeOffset Timestamp);
}