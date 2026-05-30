using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;
using Velucid.Silo.Configuration;

namespace Velucid.Silo.Authorization;

public sealed class OpenFgaInitializer : IOpenFgaInitializer
{
    private readonly OpenFgaOptions _options;
    private readonly ILogger<OpenFgaInitializer> _logger;
    private readonly OpenFgaClient _fgaClient;

    public OpenFgaInitializer(
        IOptions<OpenFgaOptions> options,
        ILogger<OpenFgaInitializer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var configuration = new ClientConfiguration
        {
            ApiUrl = _options.ApiUrl,
            StoreId = null // Will be set per-request
        };

        _fgaClient = new OpenFgaClient(configuration);
    }

    public async Task InitializeAsync()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("OpenFGA is disabled, skipping initialization");
            return;
        }

        _logger.LogInformation("Initializing OpenFGA authorization model...");

        try
        {
            // Get or create store by name
            var storeId = await GetOrCreateStoreAsync(_options.StoreName);

            // Share resolved store ID with OpenFgaAuthorizationService via static fields
            OpenFgaAuthorizationService.ResolvedStoreId = storeId;

            // Get or create authorization model for this store
            var modelId = await GetOrCreateModelAsync(storeId);

            // Share resolved model ID with OpenFgaAuthorizationService via static fields
            OpenFgaAuthorizationService.ResolvedModelId = modelId;

            _logger.LogInformation(
                "OpenFGA initialization complete. StoreId={StoreId}, ModelId={ModelId}",
                storeId, modelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OpenFGA. Authorization may not work correctly.");
            throw;
        }
    }

    private async Task<string> GetOrCreateStoreAsync(string storeName)
    {
        var stores = await _fgaClient.ListStores(new ClientListStoresRequest());

        var existingStore = stores.Stores?.FirstOrDefault(s => s.Name == storeName);
        if (existingStore != null)
        {
            _logger.LogInformation("Found existing OpenFGA store '{StoreName}' with ID: {StoreId}", storeName, existingStore.Id);
            return existingStore.Id;
        }

        var store = await _fgaClient.CreateStore(new ClientCreateStoreRequest { Name = storeName });
        _logger.LogInformation("Created new OpenFGA store '{StoreName}' with ID: {StoreId}", storeName, store.Id);
        return store.Id;
    }

    private async Task<string> GetOrCreateModelAsync(string storeId)
    {
        // List existing models for this store
        var models = await _fgaClient.ReadAuthorizationModels(
            new ClientReadAuthorizationModelsOptions { StoreId = storeId });

        // If a model exists, use the latest one (first in list is typically latest)
        if (models.AuthorizationModels != null && models.AuthorizationModels.Count > 0)
        {
            var existingModel = models.AuthorizationModels.First();
            _logger.LogInformation("Found existing authorization model with ID: {ModelId}", existingModel.Id);
            return existingModel.Id;
        }

        // Create new model
        var model = await _fgaClient.WriteAuthorizationModel(
            new ClientWriteAuthorizationModelRequest
            {
                SchemaVersion = "1.1",
                TypeDefinitions = new List<TypeDefinition>
                {
                new()
                {
                    Type = "organization",
                    Relations = new Dictionary<string, Userset>
                    {
                        ["owner"] = new Userset { This = new object() },
                        ["member"] = new Userset { This = new object() },
                        ["viewer"] = new Userset { This = new object() },
                        ["view_org"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { This = new object() },
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } },
                                    new() { ComputedUserset = new ObjectRelation { Relation = "member" } }
                                }
                            }
                        },
                        ["view_members"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } },
                                    new() { ComputedUserset = new ObjectRelation { Relation = "member" } }
                                }
                            }
                        },
                        ["create_task"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } },
                                    new() { ComputedUserset = new ObjectRelation { Relation = "member" } }
                                }
                            }
                        },
                        ["create_product"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } }
                                }
                            }
                        },
                        ["delete_product"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } }
                                }
                            }
                        },
                        ["invite_member"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } }
                                }
                            }
                        },
                        ["change_member_role"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } }
                                }
                            }
                        },
                        ["remove_member"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } }
                                }
                            }
                        },
                        ["delete_org"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } }
                                }
                            }
                        },
                        ["manage_org_settings"] = new Userset
                        {
                            Union = new Usersets
                            {
                                Child = new List<Userset>
                                {
                                    new() { ComputedUserset = new ObjectRelation { Relation = "owner" } }
                                }
                            }
                        }
                    }
                }
            }
        });

        _logger.LogInformation("Created new authorization model with ID: {ModelId}", model.AuthorizationModelId);
        return model.AuthorizationModelId;
    }
}
