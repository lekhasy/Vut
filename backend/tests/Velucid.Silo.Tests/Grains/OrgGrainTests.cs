using FluentAssertions;
using Orleans.TestingHost;
using Velucid.Silo.Authorization;
using Velucid.Silo.Events;
using Velucid.Silo.Grains;
using Velucid.Silo.Models;
using Velucid.Silo.Tests.Infrastructure;

namespace Velucid.Silo.Tests.Grains;

/// <summary>
/// Unit tests for the <see cref="OrgGrain"/> using Orleans TestCluster
/// with an in-memory event stream client.
/// </summary>
[Collection("Orleans TestCluster")]
public sealed class OrgGrainTests : IAsyncLifetime
{
    private TestCluster _cluster = null!;
    private InMemoryEventStreamClient _eventStore = null!;
    private InMemoryOpenFgaAuthorizationService _authService = null!;

    public async Task InitializeAsync()
    {
        EventTypeMapping.Reset();
        EventTypeMapping.Register<OrgCreatedEvent>("OrgCreated");
        EventTypeMapping.Register<OrgRenamedEvent>("OrgRenamed");
        EventTypeMapping.Register<OrgDeletedEvent>("OrgDeleted");
        EventTypeMapping.Register<MemberAddedEvent>("MemberAdded");
        EventTypeMapping.Register<MemberRemovedEvent>("MemberRemoved");
        EventTypeMapping.Register<InvitationSentEvent>("InvitationSent");
        EventTypeMapping.Freeze();

        _eventStore = new InMemoryEventStreamClient();
        TestSiloConfigurator.SharedEventStreamClient = _eventStore;

        _authService = new InMemoryOpenFgaAuthorizationService();
        TestSiloConfigurator.SharedAuthService = _authService;

        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        _cluster = builder.Build();

        await _cluster.DeployAsync();
    }

    public async Task DisposeAsync()
    {
        await _cluster.StopAllSilosAsync();
        await _cluster.DisposeAsync();
        EventTypeMapping.Reset();
    }

    // Simulates projector tuple sync (in production, OrgProjector handles this)
    private async Task SyncOwnerTuple(Guid orgId, Guid ownerUserId) =>
        await _authService.WriteTuples([new AuthorizationTuple($"user:{ownerUserId}", "owner", $"organization:{orgId}")]);

    private async Task SyncMemberTuple(Guid orgId, Guid userId, string role) =>
        await _authService.WriteTuples([new AuthorizationTuple($"user:{userId}", role.ToLowerInvariant(), $"organization:{orgId}")]);

    // ==================== CreateOrg Tests ====================

    [Fact]
    public async Task CreateOrg_NewUser_EmitsOrgCreatedEventWithCorrectFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);

        // Act
        var result = await grain.CreateOrg(orgName, ownerUserId);

        // Assert
        result.Should().BeEquivalentTo(new OrgInfo(orgId, orgName, false));

        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<OrgCreatedEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                OrgId = orgId,
                Name = orgName,
                OwnerUserId = ownerUserId
            });
    }

    [Fact]
    public async Task CreateOrg_DuplicateCall_IsIdempotentNoNewEvents()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);

        // Act
        var result = await grain.CreateOrg(orgName, ownerUserId);

        // Assert
        result.OrgId.Should().Be(orgId);
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(1, "no new events should be emitted for duplicate creation");
    }

    [Fact]
    public async Task CreateOrg_OwnerAutoAddedAsMember()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);

        // Act
        var members = await grain.GetMembers();

        // Assert
        members.Should().HaveCount(1);
        members[0].Should().BeEquivalentTo(new OrgMemberInfo(ownerUserId, "Owner"));
    }

    // ==================== DeleteOrg Tests ====================

    [Fact]
    public async Task DeleteOrg_OwnerCalling_EmitsOrgDeletedEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        await grain.DeleteOrg(ownerUserId);

        // Assert
        var info = await grain.GetOrgInfo();
        info.IsDeleted.Should().BeTrue();

        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<OrgDeletedEvent>();
        var deletedEvent = (OrgDeletedEvent)events[1];
        deletedEvent.OrgId.Should().Be(orgId);
        deletedEvent.ActorId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task DeleteOrg_NonOwnerCalling_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var nonOwnerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        var act = () => grain.DeleteOrg(nonOwnerUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the owner can delete*");
    }

    [Fact]
    public async Task DeleteOrg_AlreadyDeleted_IsNoOp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);
        await grain.DeleteOrg(ownerUserId);

        // Act
        await grain.DeleteOrg(ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2, "no new events for already deleted org");
    }

    // ==================== RemoveMember Tests ====================

    [Fact]
    public async Task RemoveMember_OwnerRemovingMember_EmitsMemberRemovedEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);
        await grain.AddMember(memberUserId, "Member", ownerUserId);
        await SyncMemberTuple(orgId, memberUserId, "Member");

        // Act
        await grain.RemoveMember(memberUserId, ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(3);
        events[2].Should().BeOfType<MemberRemovedEvent>();
        var removedEvent = (MemberRemovedEvent)events[2];
        removedEvent.OrgId.Should().Be(orgId);
        removedEvent.UserId.Should().Be(memberUserId);
        removedEvent.ActorId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task RemoveMember_OwnerRemovingSelf_ThrowsInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        var act = () => grain.RemoveMember(ownerUserId, ownerUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot remove the owner*");
    }

    [Fact]
    public async Task RemoveMember_NonOwnerCalling_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var nonOwnerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);
        await grain.AddMember(nonOwnerUserId, "Member", ownerUserId);
        await SyncMemberTuple(orgId, nonOwnerUserId, "Member");

        // Act
        var act = () => grain.RemoveMember(memberUserId, nonOwnerUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the owner can remove members*");
    }

    [Fact]
    public async Task RemoveMember_NonExistentMember_IsNoOp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var nonMemberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        await grain.RemoveMember(nonMemberUserId, ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(1, "no event should be emitted for non-existent member");
    }

    // ==================== SendInvitation Tests ====================

    [Fact]
    public async Task SendInvitation_OwnerInviting_EmitsInvitationSentEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var inviteEmail = $"invitee-{Guid.NewGuid():N}@example.com";
        var inviteRole = "Member";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        await grain.SendInvitation(inviteEmail, inviteRole, ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<InvitationSentEvent>();
        var inviteEvent = (InvitationSentEvent)events[1];
        inviteEvent.OrgId.Should().Be(orgId);
        inviteEvent.Email.Should().Be(inviteEmail);
        inviteEvent.Role.Should().Be(inviteRole);
        inviteEvent.InviterUserId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task SendInvitation_NonMemberInviting_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var nonMemberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        var act = () => grain.SendInvitation("test@example.com", "Member", nonMemberUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the owner can send invitations*");
    }

    [Fact]
    public async Task SendInvitation_DuplicateEmail_IsNoOpIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var inviteEmail = $"dup-{Guid.NewGuid():N}@example.com";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);
        await grain.SendInvitation(inviteEmail, "Member", ownerUserId);

        // Act
        await grain.SendInvitation(inviteEmail, "Member", ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2, "no new event for duplicate invitation");
    }

    [Fact]
    public async Task SendInvitation_InvalidRoleString_AcceptsAnyRole()
    {
        // Arrange — SendInvitation does NOT validate role; it stores whatever is passed.
        // Role validation is AddMember's responsibility, not SendInvitation's.
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act — any role string is accepted by SendInvitation
        await grain.SendInvitation("test@example.com", "Admin", ownerUserId);

        // Assert — invitation was stored (event emitted)
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<InvitationSentEvent>();
        var inviteEvent = (InvitationSentEvent)events[1];
        inviteEvent.Email.Should().Be("test@example.com");
    }

    // ==================== RenameOrg Tests ====================

    [Fact]
    public async Task RenameOrg_MemberRenaming_EmitsOrgRenamedEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var newName = $"RenamedOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        await grain.RenameOrg(newName, ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<OrgRenamedEvent>();
        var renamedEvent = (OrgRenamedEvent)events[1];
        renamedEvent.OrgId.Should().Be(orgId);
        renamedEvent.Name.Should().Be(newName);
        renamedEvent.ActorId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task RenameOrg_NonMemberRenaming_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var nonMemberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        var act = () => grain.RenameOrg("New Name", nonMemberUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the owner can rename*");
    }

    [Fact]
    public async Task RenameOrg_SameName_IsNoOpIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        await grain.RenameOrg(orgName, ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(1, "no event for same name rename");
    }

    // ==================== AddMember Tests ====================

    [Fact]
    public async Task AddMember_MemberAddingNewMember_EmitsMemberAddedEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var newMemberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        await grain.AddMember(newMemberUserId, "Member", ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<MemberAddedEvent>();
        var addedEvent = (MemberAddedEvent)events[1];
        addedEvent.OrgId.Should().Be(orgId);
        addedEvent.UserId.Should().Be(newMemberUserId);
        addedEvent.Role.Should().Be("Member");
        addedEvent.ActorId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task AddMember_InvalidRole_ThrowsArgumentException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var newMemberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        var act = () => grain.AddMember(newMemberUserId, "Admin", ownerUserId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Owner*Member*");
    }

    [Fact]
    public async Task AddMember_AlreadyMember_IsNoOpIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);
        await grain.AddMember(memberUserId, "Member", ownerUserId);
        await SyncMemberTuple(orgId, memberUserId, "Member");

        // Act
        await grain.AddMember(memberUserId, "Member", ownerUserId);

        // Assert
        var events = _eventStore.GetEvents($"org-{orgId}");
        events.Should().HaveCount(2, "no new event for already member");
    }

    // ==================== GetOrgInfo / GetMembers / IsMember Tests ====================

    [Fact]
    public async Task GetOrgInfo_NonExistentOrg_ThrowsInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);

        // Act
        var act = () => grain.GetOrgInfo();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task GetMembers_NonExistentOrg_ThrowsInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);

        // Act
        var act = () => grain.GetMembers();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task IsMember_ExistingMember_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);

        // Act
        var isMember = await grain.IsMember(ownerUserId);

        // Assert
        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task IsMember_NonExistentOrg_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);

        // Act
        var isMember = await grain.IsMember(userId);

        // Assert
        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsMember_ExistingOrgNonMemberUser_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var nonMemberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);

        // Act
        var isMember = await grain.IsMember(nonMemberUserId);

        // Assert
        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsMember_NonMemberInviting_VerifiesMembershipCheck()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var nonMemberUserId = Guid.NewGuid();
        var orgName = $"TestOrg-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IOrgGrain>(orgId);
        await grain.CreateOrg(orgName, ownerUserId);
        await SyncOwnerTuple(orgId, ownerUserId);

        // Act
        var act = () => grain.SendInvitation("test@example.com", "Member", nonMemberUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the owner can send invitations*");
    }
}
