using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Velucid.Silo.Events;

namespace Velucid.Silo.Tests.Infrastructure;

/// <summary>
/// Configures the Orleans test silo with in-memory event stream client
/// and registers event type mappings.
/// </summary>
public sealed class TestSiloConfigurator : ISiloConfigurator
{
    /// <summary>
    /// Shared in-memory event stream client used across the test silo.
    /// Set before the test cluster is created.
    /// </summary>
    internal static InMemoryEventStreamClient? SharedEventStreamClient;

    /// <inheritdoc/>
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IEventStreamClient>(
                _ => SharedEventStreamClient
                     ?? throw new InvalidOperationException(
                         "SharedEventStreamClient must be set before starting the test cluster."));
        });
    }
}
