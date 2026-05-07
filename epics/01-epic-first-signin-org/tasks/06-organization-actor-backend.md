# Task 06: Organization Actor Implementation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 06 |
| **Priority** | P1 |
| **Estimated Effort** | 3 days |

## Description

Implement the Organization Actor that handles organization creation, renaming, member invitation, acceptance, removal, and role changes. This actor manages the `organization-{orgId}` event stream in KurrentDB and enforces all business rules for organization membership.

## Architecture Reference

- Architecture doc Section 5.4 (Organization Actor)
- Architecture doc Section 8.3 (Create Organization sequence)
- Architecture doc Section 8.4 (Invite and Accept Member sequence)

## Technical Requirements

### Organization Actor State
```csharp
public class OrganizationState
{
    public Guid OrgId { get; set; }
    public string Name { get; set; }
    public Dictionary<Guid, MemberEntry> Members { get; set; } = new();
    public Dictionary<string, InvitationEntry> Invitations { get; set; } = new();
    public bool IsDeleted { get; set; }
}

public record MemberEntry(Guid UserId, string Role, DateTime JoinedAt);
public record InvitationEntry(string Email, string Role, DateTime InvitedAt, string Status);
```

### Commands
```csharp
public record CreateOrganizationCommand(string Name, Guid OwnerId);
public record RenameOrganizationCommand(Guid OrgId, string NewName, string ActorId);
public record InviteMemberCommand(Guid OrgId, string InviteeEmail, string Role, string ActorId);
public record AcceptInvitationCommand(Guid OrgId, Guid UserId, string Email);
public record DeclineInvitationCommand(Guid OrgId, Guid UserId, string Email);
public record RemoveMemberCommand(Guid OrgId, Guid UserId, string ActorId);
public record ChangeMemberRoleCommand(Guid OrgId, Guid UserId, string NewRole, string ActorId);
```

### Events
```csharp
public record OrganizationCreatedEvent(Guid OrgId, string Name, string ActorId, DateTime Timestamp) : IEvent;
public record OrganizationRenamedEvent(Guid OrgId, string NewName, string ActorId, DateTime Timestamp) : IEvent;
public record MemberInvitedEvent(Guid OrgId, string InviteeEmail, string Role, string ActorId, DateTime Timestamp) : IEvent;
public record MemberJoinedEvent(Guid OrgId, Guid UserId, string ActorId, DateTime Timestamp) : IEvent;
public record MemberRemovedEvent(Guid OrgId, Guid UserId, string ActorId, DateTime Timestamp) : IEvent;
public record MemberRoleChangedEvent(Guid OrgId, Guid UserId, string OldRole, string NewRole, string ActorId, DateTime Timestamp) : IEvent;
public record OrganizationDeletedEvent(Guid OrgId, string ActorId, DateTime Timestamp) : IEvent;
```

### Command Handlers & Validation Rules

#### `CreateOrganizationCommand`
1. Validate: `Name` is non-empty (trimmed length > 0).
2. Generate new `OrgId` (UUID v4).
3. Emit `OrganizationCreatedEvent`.
4. Automatically add creator as first Owner: emit `MemberJoinedEvent` with `Role = "Owner"`.
5. Return `{ orgId, name }`.

#### `RenameOrganizationCommand`
1. Validate: caller (`ActorId`) is an Owner in `Members`.
2. Validate: `NewName` is non-empty.
3. Emit `OrganizationRenamedEvent`.
4. Update state.
5. Return success.

#### `InviteMemberCommand`
1. Validate: caller is an Owner.
2. Validate: `InviteeEmail` is a valid email format.
3. Validate: `Role` is "Owner" or "Member".
4. Validate: no existing invitation for this email with status "Pending".
5. Validate: email does not already belong to an existing member.
6. Emit `MemberInvitedEvent`.
7. Add to `Invitations` with status "Pending".
8. Return success.

#### `AcceptInvitationCommand`
1. Validate: a pending invitation exists for the given `Email`.
2. Emit `MemberJoinedEvent`.
3. Add to `Members` with the role from the invitation.
4. Update invitation status to "Accepted".
5. Return success.

#### `DeclineInvitationCommand`
1. Validate: a pending invitation exists for the given `Email`.
2. Update invitation status to "Declined".
3. Return success.

#### `RemoveMemberCommand`
1. Validate: caller is an Owner.
2. Validate: target `UserId` is a member.
3. Validate: removing this member does not leave the org with zero Owners (if the target is an Owner, there must be at least one other Owner).
4. Emit `MemberRemovedEvent`.
5. Remove from `Members`.
6. Return success.

#### `ChangeMemberRoleCommand`
1. Validate: caller is an Owner.
2. Validate: target `UserId` is a member.
3. Validate: `NewRole` is "Owner" or "Member".
4. Validate: if demoting from Owner to Member, there must be at least one other Owner remaining.
5. Emit `MemberRoleChangedEvent` with `OldRole` and `NewRole`.
6. Update `Members` entry.
7. Return success.

### Stream ID Convention
- Stream: `organization-{orgId}`.
- Projectors subscribe to KurrentDB persistent subscriptions directly — no topic routing needed.

### File Structure
```
src/
  Vut.ActorService/
    Actors/
      OrganizationActor.cs
      OrganizationActorManager.cs
    Events/
      OrganizationCreatedEvent.cs
      OrganizationRenamedEvent.cs
      MemberInvitedEvent.cs
      MemberJoinedEvent.cs
      MemberRemovedEvent.cs
      MemberRoleChangedEvent.cs
      OrganizationDeletedEvent.cs
    Commands/
      CreateOrganizationCommand.cs
      RenameOrganizationCommand.cs
      InviteMemberCommand.cs
      AcceptInvitationCommand.cs
      DeclineInvitationCommand.cs
      RemoveMemberCommand.cs
      ChangeMemberRoleCommand.cs
```

### Unit Tests
```
tests/
  Vut.ActorService.Tests/
    Actors/
      OrganizationActorTests.cs
```

Test cases:
- Create org -> emits `OrganizationCreated` + `MemberJoined` (creator as Owner).
- Create org with empty name -> error.
- Rename org by Owner -> success.
- Rename org by Member -> error (403).
- Invite member by Owner -> success, invitation added as Pending.
- Invite member by Member -> error (403).
- Accept valid invitation -> emits `MemberJoined`, member added.
- Accept with wrong email -> error.
- Remove member by Owner -> success.
- Remove last Owner -> error.
- Change role Owner->Member by Owner -> success (if other Owners exist).
- Demote last Owner -> error.
- Rehydrate from events -> state matches original.

## Acceptance Criteria

- [ ] All command handlers implement the validation rules specified above.
- [ ] `CreateOrganization` emits both `OrganizationCreated` and `MemberJoined` events.
- [ ] Only Owners can invite, remove members, change roles, and rename the org.
- [ ] Cannot remove or demote the last Owner.
- [ ] `AcceptInvitation` validates the email matches a pending invitation.
- [ ] All events are appended to `organization-{orgId}` stream in KurrentDB.
- [ ] Actor correctly rehydrates from KurrentDB on activation.
- [ ] All unit tests pass.

## Dependencies

- Task 04 (.NET Actor Service Foundation) -- must be complete.

## Notes

- The `OrganizationDeleted` event is defined but no delete command is required in Epic 1. The event type should exist in the codebase for future use.
- The actor should return structured error responses (not exceptions) so the BFF can map them to HTTP status codes. Use a pattern like `{ success: false, error: "NOT_OWNER", message: "Only owners can invite members" }`.
- `MemberJoinedEvent` during org creation does NOT include `oldRole`/`newRole` -- it simply adds the member. The role is derived from the invitation or is "Owner" for the creator.
- The `ActorId` in commands is the `userId` of the person performing the action, extracted from the session by the BFF.
