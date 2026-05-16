using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Velucid.ReadModel;
using Velucid.ReadModel.Entities;
using Velucid.Silo.Grains;
using Velucid.Silo.Models;
using Velucid.Silo.Services;

namespace Velucid.Silo.Tests.Services;

public sealed class SignInServiceTests : IAsyncLifetime
{
    private readonly IGrainFactory _grainFactory = Substitute.For<IGrainFactory>();
    private ReadModelDbContext _db = null!;
    private SignInService _sut = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseInMemoryDatabase($"SignInTest-{Guid.NewGuid()}")
            .Options;

        _db = new ReadModelDbContext(options);
        _sut = new SignInService(_grainFactory, _db, Substitute.For<ILogger<SignInService>>());

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task SignIn_ExistingProvider_ReturnsExistingUser()
    {
        // Arrange — seed a user with a linked identity
        var userId = Guid.NewGuid();
        _db.UserProjections.Add(new UserProjection
        {
            UserId = userId,
            DisplayName = "Existing",
            AvatarUrl = "https://avatar.url",
            Email = "existing@test.com",
            IsEmailVerified = true,
            Identities =
            [
                new UserIdentity
                {
                    UserId = userId, Sub = "github|12345",
                    ProviderName = "github", Email = "existing@test.com"
                }
            ]
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.SignIn("github|12345", "github", "Existing", "https://avatar.url", "existing@test.com");

        // Assert
        result.Should().BeEquivalentTo(new
        {
            UserId = userId,
            DisplayName = "Existing",
            IsNewUser = false,
            IsEmailVerified = true
        });

        // No grain should have been called — resolved entirely from read model
        _grainFactory.DidNotReceive().GetGrain<IUserGrain>(Arg.Any<Guid>());
    }

    [Fact]
    public async Task SignIn_NoMatch_CreatesNewUser()
    {
        // Arrange
        var grain = Substitute.For<IUserGrain>();
        grain.CreateUser(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(new CreateUserResult(Guid.Empty));
        _grainFactory.GetGrain<IUserGrain>(Arg.Any<Guid>()).Returns(grain);

        // Act
        var result = await _sut.SignIn("github|99999", "github", "New User", "https://avatar.url", "new@test.com");

        // Assert
        result.Should().BeEquivalentTo(new
        {
            DisplayName = "New User",
            AvatarUrl = "https://avatar.url",
            Email = "new@test.com",
            IsEmailVerified = false,
            IsNewUser = true
        });

        await grain.Received(1).CreateUser(
            "github|99999", "github", "New User", "https://avatar.url", "new@test.com");
    }

    [Fact]
    public async Task SignIn_NoMatchNoEmail_CreatesNewUser()
    {
        // Arrange
        var grain = Substitute.For<IUserGrain>();
        _grainFactory.GetGrain<IUserGrain>(Arg.Any<Guid>()).Returns(grain);

        // Act
        var result = await _sut.SignIn("github|11111", "github", "No Email", "https://avatar.url", null);

        // Assert
        result.Should().BeEquivalentTo(new
        {
            DisplayName = "No Email",
            Email = (string?)null,
            IsNewUser = true
        });
    }

    [Fact]
    public async Task SignIn_NewProviderWithSameEmail_CreatesNewUser()
    {
        // Arrange — seed a user with a github identity and same email
        var existingUserId = Guid.NewGuid();
        _db.UserProjections.Add(new UserProjection
        {
            UserId = existingUserId,
            DisplayName = "Existing",
            AvatarUrl = "https://avatar.url",
            Email = "shared@test.com",
            IsEmailVerified = false,
            Identities =
            [
                new UserIdentity
                {
                    UserId = existingUserId, Sub = "github|12345",
                    ProviderName = "github", Email = "shared@test.com"
                }
            ]
        });
        await _db.SaveChangesAsync();

        var grain = Substitute.For<IUserGrain>();
        _grainFactory.GetGrain<IUserGrain>(Arg.Any<Guid>()).Returns(grain);

        // Act — different provider but same email
        // Since we don't auto-link, this creates a brand new user
        var result = await _sut.SignIn("google|67890", "google", "New User", "https://avatar.url", "shared@test.com");

        // Assert — new user created, no auto-linking
        result.IsNewUser.Should().BeTrue();
        await grain.Received(1).CreateUser(
            "google|67890", "google", "New User", "https://avatar.url", "shared@test.com");
    }
}