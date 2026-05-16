using FluentAssertions;
using Orleans.TestingHost;
using Velucid.Silo.Events;
using Velucid.Silo.Grains;
using Velucid.Silo.Tests.Infrastructure;

namespace Velucid.Silo.Tests.Grains;

/// <summary>
/// Tests that verify grain state is correctly rebuilt from persisted events
/// after silo restart (full deactivation and rehydration).
/// </summary>
[Collection("Orleans TestCluster")]
public sealed class UserGrainRehydrationTests
{
    private static void RegisterEventTypes()
    {
        EventTypeMapping.Reset();
        EventTypeMapping.Register<UserCreatedEvent>("UserCreated");
        EventTypeMapping.Register<IdentityLinkedEvent>("IdentityLinked");
        EventTypeMapping.Register<UserProfileUpdatedEvent>("UserProfileUpdated");
        EventTypeMapping.Register<EmailVerificationRequestedEvent>("EmailVerificationRequested");
        EventTypeMapping.Register<EmailVerifiedEvent>("EmailVerified");
        EventTypeMapping.Freeze();
    }

    [Fact]
    public async Task StateRehydration_AfterSiloRestart_ReplaysAllEvents()
    {
        // Arrange — phase 1: create a user and perform several operations
        RegisterEventTypes();
        var eventStore = new InMemoryEventStreamClient();
        var userId = Guid.NewGuid();

        TestSiloConfigurator.SharedEventStreamClient = eventStore;
        TestSiloConfigurator.SharedTimeProvider = null;
        TestSiloConfigurator.SharedEmailVerificationStore = new InMemoryEmailVerificationStore();

        var builder1 = new TestClusterBuilder();
        builder1.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        var cluster1 = builder1.Build();
        await cluster1.DeployAsync();

        try
        {
            var grain = cluster1.GrainFactory.GetGrain<IUserGrain>(userId);
            await grain.CreateUser("github|12345", "github", "Original", "https://avatar.url", "test@example.com");
            await grain.UpdateProfile("Updated", "https://new-avatar.url");
            await grain.LinkIdentity("google|67890", "google", "test@gmail.com");
            var token = await grain.RequestEmailVerification("test@example.com");
            await grain.VerifyEmail(token);

            // 6 events: UserCreated, IdentityLinked(github), ProfileUpdated, IdentityLinked(google), EmailVerificationRequested, EmailVerified
            eventStore.GetEvents($"user-{userId}").Should().HaveCount(6);

            // Tear down the silo — all grain activations are destroyed
            await cluster1.StopAllSilosAsync();
            await cluster1.DisposeAsync();
        }
        catch
        {
            await cluster1.StopAllSilosAsync();
            await cluster1.DisposeAsync();
            throw;
        }

        // Act — phase 2: restart with the SAME event store to force rehydration
        RegisterEventTypes();
        TestSiloConfigurator.SharedEventStreamClient = eventStore;
        TestSiloConfigurator.SharedTimeProvider = null;
        TestSiloConfigurator.SharedEmailVerificationStore = new InMemoryEmailVerificationStore();

        var builder2 = new TestClusterBuilder();
        builder2.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        var cluster2 = builder2.Build();
        await cluster2.DeployAsync();

        try
        {
            var rehydratedGrain = cluster2.GrainFactory.GetGrain<IUserGrain>(userId);

            // Assert — idempotent CreateUser proves Exists flag was restored
            var result = await rehydratedGrain.CreateUser(
                "github|12345", "github", "Original", "https://avatar.url", "test@example.com");
            result.UserId.Should().Be(userId);

            // No new events — the grain correctly saw the user already exists
            eventStore.GetEvents($"user-{userId}").Should().HaveCount(6,
                "rehydrated grain should not emit new events for duplicate creation");

            // Already-linked identity is still known — no-op
            await rehydratedGrain.LinkIdentity("google|67890", "google", "test@gmail.com");
            eventStore.GetEvents($"user-{userId}").Should().HaveCount(6,
                "already-linked identity should remain a no-op after rehydration");

            // New identity can be linked — proves state was fully rebuilt
            await rehydratedGrain.LinkIdentity("microsoft|11111", "microsoft", null);
            eventStore.GetEvents($"user-{userId}").Should().HaveCount(7,
                "new identity should be linkable after rehydration");

            // Profile update with same values is a no-op — proves DisplayName and AvatarUrl were restored
            await rehydratedGrain.UpdateProfile("Updated", "https://new-avatar.url");
            eventStore.GetEvents($"user-{userId}").Should().HaveCount(7,
                "no event for unchanged profile after rehydration");
        }
        finally
        {
            await cluster2.StopAllSilosAsync();
            await cluster2.DisposeAsync();
            EventTypeMapping.Reset();
        }
    }
}
