using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Velucid.ReadModel;
using Velucid.Silo.Authorization;
using Velucid.Silo.Events;
using Velucid.Silo.Services;

namespace Velucid.Silo.Tests.Infrastructure;

/// <summary>
/// Configures the Orleans test silo with in-memory event stream client,
/// time provider, read model, and registers event type mappings.
/// </summary>
public sealed class TestSiloConfigurator : ISiloConfigurator
{
    /// <summary>
    /// Shared in-memory event stream client used across the test silo.
    /// Set before the test cluster is created.
    /// </summary>
    internal static InMemoryEventStreamClient? SharedEventStreamClient;

    /// <summary>
    /// Shared time provider for controlling time in tests.
    /// Set before the test cluster is created. Defaults to <see cref="TimeProvider.System"/>.
    /// </summary>
    internal static TimeProvider? SharedTimeProvider;

    /// <summary>
    /// Shared email verification store for tests.
    /// </summary>
    internal static IEmailVerificationStore? SharedEmailVerificationStore;

    internal static InMemoryOpenFgaAuthorizationService? SharedAuthService;

    /// <inheritdoc/>
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IEventStreamClient>(
                _ => SharedEventStreamClient
                     ?? throw new InvalidOperationException(
                         "SharedEventStreamClient must be set before starting the test cluster."));

            services.AddSingleton(_ => SharedTimeProvider ?? TimeProvider.System);

            services.AddSingleton<IEmailVerificationStore>(
                _ => SharedEmailVerificationStore
                     ?? throw new InvalidOperationException(
                         "SharedEmailVerificationStore must be set before starting the test cluster."));

            var authService = SharedAuthService ?? new InMemoryOpenFgaAuthorizationService();
            services.AddSingleton<IOpenFgaAuthorizationService>(authService);

            services.AddDbContext<ReadModelDbContext>(options =>
                options.UseInMemoryDatabase("VelucidTestReadModel"));
        });
    }
}