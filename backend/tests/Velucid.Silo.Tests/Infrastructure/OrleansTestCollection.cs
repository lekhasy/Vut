namespace Velucid.Silo.Tests.Grains;

/// <summary>
/// xUnit test collection that serializes all Orleans TestCluster tests.
/// Required because <see cref="Infrastructure.TestSiloConfigurator"/> uses static
/// fields to share the event stream client and time provider across test classes.
/// </summary>
[CollectionDefinition("Orleans TestCluster", DisableParallelization = true)]
public sealed class OrleansTestCollection;
