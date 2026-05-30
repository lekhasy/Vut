using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFga.Sdk.Api;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using Velucid.Silo.Configuration;

namespace Velucid.Silo.Authorization;

public sealed class OpenFgaAuthorizationService : IOpenFgaAuthorizationService
{
    private readonly OpenFgaClient _fgaClient;
    private readonly OpenFgaOptions _options;
    private readonly ILogger<OpenFgaAuthorizationService> _logger;

    public OpenFgaAuthorizationService(
        IOptions<OpenFgaOptions> options,
        ILogger<OpenFgaAuthorizationService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var configuration = new ClientConfiguration
        {
            ApiUrl = _options.ApiUrl,
            StoreId = string.IsNullOrEmpty(ResolvedStoreId) ? null : ResolvedStoreId,
            AuthorizationModelId = string.IsNullOrEmpty(ResolvedModelId) ? null : ResolvedModelId
        };

        _fgaClient = new OpenFgaClient(configuration);
    }

    // Shared state set by OpenFgaInitializer after resolving IDs
    public static string? ResolvedStoreId { get; set; }
    public static string? ResolvedModelId { get; set; }

    public async Task<bool> Check(Guid userId, string permission, Guid resourceId)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("OpenFGA is disabled, denying permission {Permission} on {ResourceId}", permission, resourceId);
            return false;
        }

        try
        {
            var user = $"user:{userId}";
            var relation = permission;
            var obj = $"organization:{resourceId}";

            var response = await _fgaClient.Check(new ClientCheckRequest
            {
                User = user,
                Relation = relation,
                Object = obj
            });

            return response.Allowed ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA check failed for user {User}, permission {Permission}, resource {ResourceId}",
                userId, permission, resourceId);

            return _options.FailureMode == OpenFgaFailureMode.LogAndAllow;
        }
    }

    public async Task WriteTuples(IEnumerable<AuthorizationTuple> tuples)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("OpenFGA is disabled, skipping WriteTuples");
            return;
        }

        var clientTuples = tuples.Select(t => new ClientTupleKey
        {
            User = t.User,
            Relation = t.Relation,
            Object = t.Object
        }).ToList();

        if (clientTuples.Count == 0)
        {
            return;
        }

        try
        {
            await _fgaClient.Write(new ClientWriteRequest
            {
                Writes = clientTuples
            });

            _logger.LogInformation("Successfully wrote {Count} tuples to OpenFGA", clientTuples.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write {Count} tuples to OpenFGA", clientTuples.Count);
            throw;
        }
    }

    public async Task DeleteTuples(IEnumerable<AuthorizationTuple> tuples)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("OpenFGA is disabled, skipping DeleteTuples");
            return;
        }

        var clientTuples = tuples.Select(t => new ClientTupleKeyWithoutCondition
        {
            User = t.User,
            Relation = t.Relation,
            Object = t.Object
        }).ToList();

        if (clientTuples.Count == 0)
        {
            return;
        }

        try
        {
            await _fgaClient.Write(new ClientWriteRequest
            {
                Deletes = clientTuples
            });

            _logger.LogInformation("Successfully deleted {Count} tuples from OpenFGA", clientTuples.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Count} tuples from OpenFGA", clientTuples.Count);
            throw;
        }
    }
}
