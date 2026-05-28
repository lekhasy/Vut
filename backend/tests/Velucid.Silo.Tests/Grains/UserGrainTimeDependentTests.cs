using FluentAssertions;
using Orleans.TestingHost;
using Velucid.Silo.Events;
using Velucid.Silo.Grains;
using Velucid.Silo.Tests.Infrastructure;

namespace Velucid.Silo.Tests.Grains;

/// <summary>
/// Tests for time-dependent behavior in <see cref="UserGrain"/>,
/// using a <see cref="FakeTimeProvider"/> to control the clock.
/// </summary>
[Collection("Orleans TestCluster")]
public sealed class UserGrainTimeDependentTests : IAsyncLifetime
{
    private TestCluster _cluster = null!;
    private InMemoryEventStreamClient _eventStore = null!;
    private FakeTimeProvider _timeProvider = null!;

    public async Task InitializeAsync()
    {
        EventTypeMapping.Reset();
        EventTypeMapping.Register<UserRegisteredEvent>("UserCreated");
        EventTypeMapping.Register<UserProfileUpdatedEvent>("UserProfileUpdated");
        EventTypeMapping.Register<EmailVerificationRequestedEvent>("EmailVerificationRequested");
        EventTypeMapping.Register<EmailVerifiedEvent>("EmailVerified");
        EventTypeMapping.Freeze();

        _eventStore = new InMemoryEventStreamClient();
        _timeProvider = new FakeTimeProvider();

        TestSiloConfigurator.SharedEventStreamClient = _eventStore;
        TestSiloConfigurator.SharedTimeProvider = _timeProvider;
        TestSiloConfigurator.SharedEmailVerificationStore = new InMemoryEmailVerificationStore(_timeProvider);

        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        _cluster = builder.Build();

        await _cluster.DeployAsync();
    }

    public async Task DisposeAsync()
    {
        await _cluster.StopAllSilosAsync();
        await _cluster.DisposeAsync();
        TestSiloConfigurator.SharedTimeProvider = null;
        EventTypeMapping.Reset();
    }

    [Fact]
    public async Task VerifyEmail_ExpiredToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", "test@example.com");
        var token = await grain.RequestEmailVerification("test@example.com");

        // Advance time past the 15-minute expiry window
        _timeProvider.Advance(TimeSpan.FromMinutes(16));

        // Act
        var act = () => grain.VerifyEmail(token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No pending verification*");
    }
}
