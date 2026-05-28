using FluentAssertions;
using NSubstitute;
using Velucid.Silo.Grains;
using Velucid.Silo.Models;
using Velucid.Silo.Services;
using SignInResult = Velucid.Silo.Models.SignInResult;

namespace Velucid.Silo.Tests.Services;

public sealed class SignInServiceTests
{
    private readonly IGrainFactory _grainFactory = Substitute.For<IGrainFactory>();
    private readonly SignInService _sut;

    public SignInServiceTests()
    {
        _sut = new SignInService(_grainFactory);
    }

    [Fact]
    public async Task SignIn_AlreadyLinked_ReturnsExistingUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sub = "github|12345";

        var identityGrain = Substitute.For<IIdentityGrain>();
        identityGrain.GetLinkedUserId().Returns(userId);
        _grainFactory.GetGrain<IIdentityGrain>(sub).Returns(identityGrain);

        var userGrain = Substitute.For<IUserGrain>();
        userGrain.GetUserInfo().Returns(new UserInfo(
            userId, "Existing", "https://avatar.url", "existing@test.com", IsEmailVerified: true));
        _grainFactory.GetGrain<IUserGrain>(userId).Returns(userGrain);

        // Act
        var result = await _sut.SignIn(sub, "github", "New Name", "https://new-avatar.url", "new@test.com");

        // Assert
        result.Should().BeEquivalentTo(new
        {
            UserId = userId,
            DisplayName = "Existing",
            IsNewUser = false,
            IsEmailVerified = true
        });

        // IdentityGrain.SetLinkedUserId must NOT be called for existing users
        identityGrain.DidNotReceiveWithAnyArgs().SetLinkedUserId(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task SignIn_NotLinked_CreatesUserAndLinks()
    {
        // Arrange
        var sub = "github|99999";

        var identityGrain = Substitute.For<IIdentityGrain>();
        identityGrain.GetLinkedUserId()
            .Returns(Task.FromException<Guid>(new IdentityOrphanedException(sub)));
        _grainFactory.GetGrain<IIdentityGrain>(sub).Returns(identityGrain);

        var userGrain = Substitute.For<IUserGrain>();
        userGrain.CreateUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo => new CreateUserResult(Guid.Empty));
        userGrain.GetUserInfo()
            .Returns(callInfo => new UserInfo(Guid.Empty, "New User", "https://avatar.url", "new@test.com", IsEmailVerified: false));
        _grainFactory.GetGrain<IUserGrain>(Arg.Any<Guid>()).Returns(userGrain);

        // Act
        var result = await _sut.SignIn(sub, "github", "New User", "https://avatar.url", "new@test.com");

        // Assert
        result.IsNewUser.Should().BeTrue();
        result.DisplayName.Should().Be("New User");
        result.Email.Should().Be("new@test.com");

        // Verify SetLinkedUserId was called before CreateUser to prevent orphaned users on retry
        await identityGrain.Received(1).SetLinkedUserId(
            Arg.Any<Guid>(), "github", "new@test.com");
        await userGrain.Received(1).CreateUser(
            sub, "github", "New User", "https://avatar.url", "new@test.com");
    }

    [Fact]
    public async Task SignIn_NotLinkedNoEmail_CreatesUserAndLinks()
    {
        // Arrange
        var sub = "github|11111";

        var identityGrain = Substitute.For<IIdentityGrain>();
        identityGrain.GetLinkedUserId()
            .Returns(Task.FromException<Guid>(new IdentityOrphanedException(sub)));
        _grainFactory.GetGrain<IIdentityGrain>(sub).Returns(identityGrain);

        var userGrain = Substitute.For<IUserGrain>();
        userGrain.CreateUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo => new CreateUserResult(Guid.Empty));
        userGrain.GetUserInfo()
            .Returns(callInfo => new UserInfo(Guid.Empty, "No Email", "https://avatar.url", null, IsEmailVerified: false));
        _grainFactory.GetGrain<IUserGrain>(Arg.Any<Guid>()).Returns(userGrain);

        // Act
        var result = await _sut.SignIn(sub, "github", "No Email", "https://avatar.url", null);

        // Assert
        result.IsNewUser.Should().BeTrue();
        result.Email.Should().BeNull();

        await identityGrain.Received(1).SetLinkedUserId(
            Arg.Any<Guid>(), "github", null);
    }

    [Fact]
    public async Task SignIn_DifferentProviderSameEmail_CreatesNewUser()
    {
        // Arrange — different provider with same email creates new user (no auto-linking)
        var sub = "google|67890";

        var identityGrain = Substitute.For<IIdentityGrain>();
        identityGrain.GetLinkedUserId()
            .Returns(Task.FromException<Guid>(new IdentityOrphanedException(sub)));
        _grainFactory.GetGrain<IIdentityGrain>(sub).Returns(identityGrain);

        var userGrain = Substitute.For<IUserGrain>();
        userGrain.CreateUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo => new CreateUserResult(Guid.Empty));
        userGrain.GetUserInfo()
            .Returns(callInfo => new UserInfo(Guid.Empty, "New User", "https://avatar.url", "shared@test.com", IsEmailVerified: false));
        _grainFactory.GetGrain<IUserGrain>(Arg.Any<Guid>()).Returns(userGrain);

        // Act
        var result = await _sut.SignIn(sub, "google", "New User", "https://avatar.url", "shared@test.com");

        // Assert
        result.IsNewUser.Should().BeTrue();
        await userGrain.Received(1).CreateUser(
            sub, "google", "New User", "https://avatar.url", "shared@test.com");
    }

    [Fact]
    public async Task SignIn_GetUserInfoFails_ReturnsPartialResult()
    {
        // Arrange — user was created via UserGrain but GetUserInfo fails
        // SignInService should still return a partial result
        var sub = "github|55555";

        var identityGrain = Substitute.For<IIdentityGrain>();
        identityGrain.GetLinkedUserId()
            .Returns(Task.FromException<Guid>(new IdentityOrphanedException(sub)));
        _grainFactory.GetGrain<IIdentityGrain>(sub).Returns(identityGrain);

        var userGrain = Substitute.For<IUserGrain>();
        userGrain.CreateUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo => new CreateUserResult(Guid.Empty));
        userGrain.GetUserInfo()
            .Returns(Task.FromException<UserInfo>(new InvalidOperationException("User does not exist.")));
        _grainFactory.GetGrain<IUserGrain>(Arg.Any<Guid>()).Returns(userGrain);

        // Act
        var result = await _sut.SignIn(sub, "github", "Partial User", "https://avatar.url", "partial@test.com");

        // Assert — should still return a result despite GetUserInfo failure
        result.IsNewUser.Should().BeTrue();
        result.DisplayName.Should().Be("Partial User");
    }
}
