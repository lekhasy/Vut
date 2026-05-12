# Task 06: Organization Grain Implementation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 06 |
| **Priority** | P1 |
| **Estimated Effort** | 3 days |

## Description

Implement the Organization Grain (virtual actor) that handles organization creation, renaming, member invitation, acceptance, removal, and role changes. This grain manages the `organization-{orgId}` event stream in KurrentDB via direct `EventStoreClient` integration (inherited from `EventSourcedGrain<OrganizationState>`), exposes strongly-typed methods on `IOrganizationGrain`, and enforces all business rules for organization membership.

There is **no OrganizationActorManager**. The grain is auto-activated by the Orleans runtime on first call to `IGrainFactory.GetGrain<IOrganizationGrain>(orgId)`. API controllers call grain methods directly — no gRPC or cluster messages.

## Architecture Reference

- Architecture doc Section 5.1 (Base Grain Abstraction — EventSourcedGrain)
- Architecture doc Section 5.4 (Organization Grain)
- Architecture doc Section 5.5 (Calling Grains from API Controllers)
- Architecture doc Section 8.4 (Create Organization sequence)
- Architecture doc Section 8.5 (Invite and Accept Member sequence)

## Technical Requirements

### Organization Grain Interface

```csharp
public interface IOrganizationGrain : IGrainWithGuidKey
{
    Task<CreateOrgResult> CreateOrganization(string name, Guid ownerId);
    Task RenameOrganization(string newName);
    Task InviteMember(string inviteeEmail, string role);
    Task AcceptInvitation(Guid userId, string email);
    Task DeclineInvitation(Guid userId, string email);
    Task RemoveMember(Guid userId);
    Task ChangeMemberRole(Guid userId, string newRole);
}
```

Each method is a native C# async method on the grain interface — no gRPC proto definitions, no cluster command messages, no `HandleMessage` switch statement.

### Organization Grain State
```csharp
public class OrganizationState
{
    public Guid OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<Guid, MemberEntry> Members { get; set; } = new();
    public Dictionary<string, InvitationEntry> Invitations { get; set; } = new();
    public bool IsDeleted { get; set; }
}

public record MemberEntry(Guid UserId, string Role, DateTime JoinedAt);
public record InvitationEntry(string Email, string Role, DateTime InvitedAt, string Status);
```

### Events (persisted to KurrentDB via EventStoreClient)
```csharp
public record OrganizationCreatedEvent(Guid OrgId, string Name, string ActorId, DateTime Timestamp) : IEvent;
public record OrganizationRenamedEvent(Guid OrgId, string NewName, string ActorId, DateTime Timestamp) : IEvent;
public record MemberInvitedEvent(Guid OrgId, string InviteeEmail, string Role, string ActorId, DateTime Timestamp) : IEvent;
public record MemberJoinedEvent(Guid OrgId, Guid UserId, string ActorId, DateTime Timestamp) : IEvent;
public record MemberRemovedEvent(Guid OrgId, Guid UserId, string ActorId, DateTime Timestamp) : IEvent;
public record MemberRoleChangedEvent(Guid OrgId, Guid UserId, string OldRole, string NewRole, string ActorId, DateTime Timestamp) : IEvent;
public record OrganizationDeletedEvent(Guid OrgId, string ActorId, DateTime Timestamp) : IEvent;
```

### Grain Implementation

The `OrganizationGrain` inherits from `EventSourcedGrain<OrganizationState>` (built in Task 04) and implements `IOrganizationGrain`:

```csharp
public class OrganizationGrain : EventSourcedGrain<OrganizationState>, IOrganizationGrain
{
    public OrganizationGrain(EventStoreClient client)
        : base(client, $"organization-{/* GrainKey from Orleans */}") { }

    // Construct stream ID from the grain's Guid key
    // Stream ID: organization-{orgId}

    protected override void Apply(OrganizationState state, object @event)
    {
        switch (@event)
        {
            case OrganizationCreatedEvent e:
                state.OrgId = e.OrgId;
                state.Name = e.Name;
                break;
            case OrganizationRenamedEvent e:
                state.Name = e.NewName;
                break;
            case MemberInvitedEvent e:
                state.Invitations[e.InviteeEmail] = new InvitationEntry(
                    e.InviteeEmail, e.Role, e.Timestamp, "Pending");
                break;
            case MemberJoinedEvent e:
                var role = state.Invitations.TryGetValue(/* email */, out var inv)
                    ? inv.Role : "Owner"; // Creator gets Owner role
                state.Members[e.UserId] = new MemberEntry(
                    e.UserId, role, e.Timestamp);
                break;
            case MemberRemovedEvent e:
                state.Members.Remove(e.UserId);
                break;
            case MemberRoleChangedEvent e:
                if (state.Members.TryGetValue(e.UserId, out var member))
                    state.Members[e.UserId] = member with { Role = e.NewRole };
                break;
            case OrganizationDeletedEvent e:
                state.IsDeleted = true;
                break;
        }
    }

    public async Task<CreateOrgResult> CreateOrganization(string name, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name cannot be empty");

        var orgId = this.GetPrimaryKey();
        await EmitEvent(new OrganizationCreatedEvent(
            orgId, name.Trim(), ownerId.ToString(), DateTime.UtcNow));
        await EmitEvent(new MemberJoinedEvent(
            orgId, ownerId, ownerId.ToString(), DateTime.UtcNow));

        return new CreateOrgResult(orgId, name.Trim());
    }

    public async Task RenameOrganization(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Organization name cannot be empty");

        await EmitEvent(new OrganizationRenamedEvent(
            State.OrgId, newName.Trim(),
            State.OrgId.ToString(), DateTime.UtcNow));
    }

    public async Task InviteMember(string inviteeEmail, string role)
    {
        if (role is not ("Owner" or "Member"))
            throw new ArgumentException("Role must be Owner or Member");
        if (State.Invitations.TryGetValue(inviteeEmail, out var existing)
            && existing.Status == "Pending")
            throw new InvalidOperationException("Pending invitation already exists");

        await EmitEvent(new MemberInvitedEvent(
            State.OrgId, inviteeEmail, role,
            State.OrgId.ToString(), DateTime.UtcNow));
    }

    public async Task AcceptInvitation(Guid userId, string email)
    {
        if (!State.Invitations.TryGetValue(email, out var invitation)
            || invitation.Status != "Pending")
            throw new InvalidOperationException("No pending invitation for this email");

        await EmitEvent(new MemberJoinedEvent(
            State.OrgId, userId,
            userId.ToString(), DateTime.UtcNow));
    }

    public async Task DeclineInvitation(Guid userId, string email)
    {
        if (!State.Invitations.TryGetValue(email, out var invitation)
            || invitation.Status != "Pending")
            throw new InvalidOperationException("No pending invitation for this email");

        // Invitation status updated in Apply
        // (emit a decline event or handle in state directly)
    }

    public async Task RemoveMember(Guid userId)
    {
        if (!State.Members.ContainsKey(userId))
            throw new InvalidOperationException("User is not a member");

        if (State.Members[userId].Role == "Owner"
            && State.Members.Count(m => m.Value.Role == "Owner") <= 1)
            throw new InvalidOperationException("Cannot remove the last Owner");

        await EmitEvent(new MemberRemovedEvent(
            State.OrgId, userId,
            State.OrgId.ToString(), DateTime.UtcNow));
    }

    public async Task ChangeMemberRole(Guid userId, string newRole)
    {
        if (newRole is not ("Owner" or "Member"))
            throw new ArgumentException("Role must be Owner or Member");
        if (!State.Members.TryGetValue(userId, out var member))
            throw new InvalidOperationException("User is not a member");

        if (member.Role == "Owner" && newRole == "Member"
            && State.Members.Count(m => m.Value.Role == "Owner") <= 1)
            throw new InvalidOperationException("Cannot demote the last Owner");

        await EmitEvent(new MemberRoleChangedEvent(
            State.OrgId, userId, member.Role, newRole,
            State.OrgId.ToString(), DateTime.UtcNow));
    }
}
```

### Grain Method Details & Validation Rules

#### `CreateOrganization`
1. Validate: `Name` is non-empty (trimmed length > 0).
2. Use `this.GetPrimaryKey()` to get the grain's Guid key as `OrgId`.
3. Emit `OrganizationCreatedEvent`.
4. Automatically add creator as first Owner: emit `MemberJoinedEvent`.
5. Return `CreateOrgResult { OrgId, Name }`.

#### `RenameOrganization`
1. Validate: `NewName` is non-empty.
2. Emit `OrganizationRenamedEvent`.
3. State updated automatically via `Apply`.

**Note:** Authorization (caller is Owner) is enforced at the API controller level before calling the grain method.

#### `InviteMember`
1. Validate: `Role` is "Owner" or "Member".
2. Validate: no existing invitation for this email with status "Pending".
3. Emit `MemberInvitedEvent`.
4. State updated automatically via `Apply`.

#### `AcceptInvitation`
1. Validate: a pending invitation exists for the given `Email`.
2. Emit `MemberJoinedEvent`.
3. State updated automatically via `Apply` — adds to `Members` with the role from the invitation, updates invitation status to "Accepted".

#### `DeclineInvitation`
1. Validate: a pending invitation exists for the given `Email`.
2. Update invitation status to "Declined" (via event and `Apply`).

#### `RemoveMember`
1. Validate: target `UserId` is a member.
2. Validate: removing this member does not leave the org with zero Owners.
3. Emit `MemberRemovedEvent`.

#### `ChangeMemberRole`
1. Validate: target `UserId` is a member.
2. Validate: `NewRole` is "Owner" or "Member".
3. Validate: if demoting from Owner to Member, there must be at least one other Owner remaining.
4. Emit `MemberRoleChangedEvent` with `OldRole` and `NewRole`.

### Stream ID Convention
- Stream: `organization-{orgId}`.
- The stream ID is constructed in the grain constructor from the grain's Guid key.
- The grain is activated by: `grainFactory.GetGrain<IOrganizationGrain>(orgId)`.
- Projectors subscribe to KurrentDB persistent subscriptions directly.

### API Controller (co-hosted in Orleans silo)

```csharp
[ApiController]
[Route("api/organizations")]
public class OrganizationController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;

    public OrganizationController(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization(
        [FromBody] CreateOrgRequest request)
    {
        var orgId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IOrganizationGrain>(orgId);
        var result = await grain.CreateOrganization(
            request.Name, request.OwnerId);
        return CreatedAtAction(nameof(CreateOrganization), result);
    }

    [HttpPost("{orgId}/invite")]
    public async Task<IActionResult> InviteMember(
        Guid orgId, [FromBody] InviteMemberRequest request)
    {
        var grain = _grainFactory.GetGrain<IOrganizationGrain>(orgId);
        await grain.InviteMember(request.Email, request.Role);
        return Ok();
    }
}
```

### File Structure
```
src/
  Velucid.Silo/
    Grains/
      IOrganizationGrain.cs
      OrganizationGrain.cs
    Events/
      OrganizationCreatedEvent.cs
      OrganizationRenamedEvent.cs
      MemberInvitedEvent.cs
      MemberJoinedEvent.cs
      MemberRemovedEvent.cs
      MemberRoleChangedEvent.cs
      OrganizationDeletedEvent.cs
    Models/
      OrganizationState.cs
      MemberEntry.cs
      InvitationEntry.cs
      CreateOrgResult.cs
    Controllers/
      OrganizationController.cs
```

### Unit Tests
```
tests/
  Velucid.Silo.Tests/
    Grains/
      OrganizationGrainTests.cs
```

Test cases (using `Microsoft.Orleans.TestingHost` or grain test utilities):
- Create org → emits `OrganizationCreated` + `MemberJoined` (creator as Owner).
- Create org with empty name → throws error.
- Rename org → emits `OrganizationRenamed`.
- Invite member → success, invitation added as Pending.
- Invite with duplicate pending email → throws error.
- Accept valid invitation → emits `MemberJoined`, member added.
- Accept with wrong email → throws error.
- Remove member → emits `MemberRemoved`.
- Remove last Owner → throws error.
- Change role Owner→Member → emits `MemberRoleChanged` (if other Owners exist).
- Demote last Owner → throws error.
- State rehydration from events matches original state after activation (`OnActivateAsync`).

## Acceptance Criteria

- [ ] `IOrganizationGrain` interface defined with `CreateOrganization`, `RenameOrganization`, `InviteMember`, `AcceptInvitation`, `DeclineInvitation`, `RemoveMember`, and `ChangeMemberRole` methods.
- [ ] `OrganizationGrain` inherits from `EventSourcedGrain<OrganizationState>` and implements `IOrganizationGrain`.
- [ ] `CreateOrganization` emits both `OrganizationCreated` and `MemberJoined` events.
- [ ] Cannot remove or demote the last Owner.
- [ ] `AcceptInvitation` validates the email matches a pending invitation.
- [ ] All events are persisted to `organization-{orgId}` stream in KurrentDB via `EventStoreClient`.
- [ ] Grain correctly rehydrates from KurrentDB on activation via `OnActivateAsync`.
- [ ] Grain deactivation managed by `GrainCollectionOptions.CollectionAge` (30 minutes) and `DelayDeactivation`.
- [ ] API controller calls grain via `IGrainFactory.GetGrain<IOrganizationGrain>(orgId)`.
- [ ] All unit tests pass.

## Dependencies

- Task 04 (.NET Orleans Silo Foundation) — must be complete (provides `EventSourcedGrain<TState>` base class, Orleans silo setup, co-hosted ASP.NET Core API, and `EventStoreClient` singleton).

## Notes

- The `OrganizationDeleted` event is defined but no delete command is required in Epic 1. The event type should exist in the codebase for future use.
- The grain should return structured error responses (not exceptions) so the API controller can map them to HTTP status codes. Use a pattern like `{ success: false, error: "NOT_OWNER", message: "Only owners can invite members" }`.
- `MemberJoinedEvent` during org creation does NOT include `oldRole`/`newRole` — it simply adds the member. The role is derived from the invitation or is "Owner" for the creator.
- Authorization (caller is Owner) should be enforced at the API controller level using the `actorId` from the session, before calling the grain method. The grain validates structural business rules (e.g., "last Owner" checks).
- No cluster kind registration needed — Orleans discovers grain types automatically via the `IOrganizationGrain` interface and DI.
