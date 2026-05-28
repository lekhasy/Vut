using FluentAssertions;
using Orleans.TestingHost;
using Velucid.Silo.Events;
using Velucid.Silo.Grains;
using Velucid.Silo.Tests.Infrastructure;

namespace Velucid.Silo.Tests.Grains;

/// <summary>
/// Unit tests for the <see cref="UserGrain"/> using Orleans TestCluster
/// with an in-memory event stream client.
/// </summary>
[Collection("Orleans TestCluster")]
public sealed class UserGrainTests : IAsyncLifetime
{
    private TestCluster _cluster = null!;
    private InMemoryEventStreamClient _eventStore = null!;

    public async Task InitializeAsync()
    {
        EventTypeMapping.Reset();
        EventTypeMapping.Register<UserRegisteredEvent>("UserCreated");
        EventTypeMapping.Register<IdentityLinkedEvent>("IdentityLinked");
        EventTypeMapping.Register<UserProfileUpdatedEvent>("UserProfileUpdated");
        EventTypeMapping.Register<EmailVerificationRequestedEvent>("EmailVerificationRequested");
        EventTypeMapping.Register<EmailVerifiedEvent>("EmailVerified");
        EventTypeMapping.Freeze();

        _eventStore = new InMemoryEventStreamClient();
        TestSiloConfigurator.SharedEventStreamClient = _eventStore;
        TestSiloConfigurator.SharedEmailVerificationStore = new InMemoryEmailVerificationStore();

        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        _cluster = builder.Build();

        await _cluster.DeployAsync();
    }

    public async Task DisposeAsync()
    {
        await _cluster.StopAllSilosAsync();
        await _cluster.DisposeAsync();
        TestSiloConfigurator.SharedEmailVerificationStore = null;
        EventTypeMapping.Reset();
    }

    [Fact]
    public async Task CreateUser_NewUser_EmitsSingleUserRegisteredEventWithIdentity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);

        // Act
        var result = await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", "test@example.com");

        // Assert
        result.UserId.Should().Be(userId);

        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<UserRegisteredEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                UserId = userId,
                DisplayName = "Test User",
                AvatarUrl = "https://avatar.url",
                Email = "test@example.com",
                Sub = "github|12345",
                ProviderName = "github"
            });
    }

    [Fact]
    public async Task CreateUser_ExistingUser_IsIdempotentNoNewEvents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", "test@example.com");

        // Act
        var result = await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", "test@example.com");

        // Assert
        result.UserId.Should().Be(userId);
        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(1, "no new events should be emitted for duplicate creation");
    }

    [Fact]
    public async Task LinkIdentity_NewProvider_EmitsIdentityLinkedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", null);

        // Act
        await grain.LinkIdentity("google|67890", "google", "Test User", "https://avatar.url", "test@gmail.com");

        // Assert
        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<IdentityLinkedEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                Sub = "google|67890",
                ProviderName = "google",
                Email = "test@gmail.com"
            });
    }

    [Fact]
    public async Task LinkIdentity_AlreadyLinkedProvider_IsNoOp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", null);

        // Act
        await grain.LinkIdentity("github|12345", "github", "https://avatar.url", "test@example.com", null);

        // Assert
        // 1 event: UserRegistered (with linked identity). LinkIdentity emits nothing
        // for already-linked identity. UpdateProfile emits nothing (same values).
        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(1, "no new events for already-linked identity or unchanged profile");
    }

    [Fact]
    public async Task UpdateProfile_ChangedDisplayName_EmitsProfileUpdatedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Original Name", "https://avatar.url", null);

        // Act
        await grain.UpdateProfile("Updated Name", "https://avatar.url");

        // Assert
        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<UserProfileUpdatedEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                DisplayName = "Updated Name",
                AvatarUrl = "https://avatar.url"
            });
    }

    [Fact]
    public async Task UpdateProfile_NoChanges_DoesNotEmitEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", null);

        // Act
        await grain.UpdateProfile("Test User", "https://avatar.url");

        // Assert
        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(1, "no event should be emitted when nothing changed");
    }

    [Fact]
    public async Task UpdateProfile_NonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);

        // Act
        var act = () => grain.UpdateProfile("Name", "https://avatar.url");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task RequestEmailVerification_EmitsEventAndReturns6DigitToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", "test@example.com");

        // Act
        var token = await grain.RequestEmailVerification("test@example.com");

        // Assert
        token.Should().MatchRegex(@"^\d{6}$", "token should be a 6-digit code");

        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<EmailVerificationRequestedEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                Email = "test@example.com",
                Token = token
            });
    }

    [Fact]
    public async Task VerifyEmail_ValidToken_EmitsEmailVerifiedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", "test@example.com");
        var token = await grain.RequestEmailVerification("test@example.com");

        // Act
        await grain.VerifyEmail(token);

        // Assert
        var events = _eventStore.GetEvents($"user-{userId}");
        events.Should().HaveCount(3);
        events[2].Should().BeOfType<EmailVerifiedEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                Email = "test@example.com"
            });
    }

    [Fact]
    public async Task VerifyEmail_WrongToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);
        await grain.CreateUser(
            "github|12345", "github", "Test User", "https://avatar.url", "test@example.com");
        await grain.RequestEmailVerification("test@example.com");

        // Act
        var act = () => grain.VerifyEmail("000000");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid verification token*");
    }

    [Fact]
    public async Task CreateUser_NullEmail_EmitsEventsWithNullEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);

        // Act
        var result = await grain.CreateUser(
            "github|12345", "github", "No Email User", "https://avatar.url", null);

        // Assert
        result.UserId.Should().Be(userId);
        var events = _eventStore.GetEvents($"user-{userId}");
        events[0].Should().BeOfType<UserRegisteredEvent>()
            .Which.Email.Should().BeNull();
    }

    [Fact]
    public async Task RequestEmailVerification_NonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);

        // Act
        var act = () => grain.RequestEmailVerification("test@example.com");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task VerifyEmail_NonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IUserGrain>(userId);

        // Act
        var act = () => grain.VerifyEmail("123456");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

}
