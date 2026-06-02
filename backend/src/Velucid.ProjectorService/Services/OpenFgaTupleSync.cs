using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using Velucid.ProjectorService.Configuration;

namespace Velucid.ProjectorService.Services;

public sealed class OpenFgaTupleSync
{
    private readonly OpenFgaOptions _options;
    private readonly ILogger<OpenFgaTupleSync> _logger;
    private OpenFgaClient? _client;

    public OpenFgaTupleSync(
        IOptions<OpenFgaOptions> options,
        ILogger<OpenFgaTupleSync> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("OpenFGA tuple sync is disabled");
            return;
        }

        var configuration = new ClientConfiguration { ApiUrl = _options.ApiUrl };
        var initClient = new OpenFgaClient(configuration);

        var storeId = await ResolveStoreIdAsync(initClient);

        _client = new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = _options.ApiUrl,
            StoreId = storeId
        });

        _logger.LogInformation("OpenFGA tuple sync initialized with store {StoreId}", storeId);
    }

    public async Task WriteTuplesAsync(IEnumerable<(string User, string Relation, string Object)> tuples)
    {
        if (!_options.Enabled || _client is null) return;

        var clientTuples = tuples.Select(t => new ClientTupleKey
        {
            User = t.User,
            Relation = t.Relation,
            Object = t.Object
        }).ToList();

        if (clientTuples.Count == 0) return;

        try
        {
            await _client.Write(new ClientWriteRequest { Writes = clientTuples });
            _logger.LogInformation("Wrote {Count} tuples to OpenFGA", clientTuples.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write {Count} tuples to OpenFGA", clientTuples.Count);
            throw;
        }
    }

    public async Task DeleteTuplesAsync(IEnumerable<(string User, string Relation, string Object)> tuples)
    {
        if (!_options.Enabled || _client is null) return;

        var clientTuples = tuples.Select(t => new ClientTupleKeyWithoutCondition
        {
            User = t.User,
            Relation = t.Relation,
            Object = t.Object
        }).ToList();

        if (clientTuples.Count == 0) return;

        try
        {
            await _client.Write(new ClientWriteRequest { Deletes = clientTuples });
            _logger.LogInformation("Deleted {Count} tuples from OpenFGA", clientTuples.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Count} tuples from OpenFGA", clientTuples.Count);
            throw;
        }
    }

    private async Task<string> ResolveStoreIdAsync(OpenFgaClient client)
    {
        var stores = await client.ListStores(new ClientListStoresRequest());
        var store = stores.Stores?.FirstOrDefault(s => s.Name == _options.StoreName);

        if (store != null)
        {
            _logger.LogInformation("Found OpenFGA store '{StoreName}' with ID: {StoreId}", _options.StoreName, store.Id);
            return store.Id;
        }

        var created = await client.CreateStore(new ClientCreateStoreRequest { Name = _options.StoreName });
        _logger.LogInformation("Created OpenFGA store '{StoreName}' with ID: {StoreId}", _options.StoreName, created.Id);
        return created.Id;
    }
}
