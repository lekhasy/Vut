# Task 05: User Actor Implementation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 05 |
| **Priority** | P1 |
| **Estimated Effort** | 1.5 days |

## Description

Implement the User Actor that handles user creation and profile updates. This actor manages the `user-{userId}` event stream in KurrentDB, processes commands from the BFF, and emits events that feed into the read model via Redpanda.

## Architecture Reference

- Architecture doc Section 5.3 (User Actor)
- Architecture doc Section 8.1 (First-Time User Sign-In sequence)
- Architecture doc Section 8.2 (Returning User Sign-In sequence)

## Technical Requirements

### User Actor State
```csharp
public class UserState
{
    public Guid UserId { get; set; }
    public string ProviderId { get; set; }  // Auth0 subject, e.g. "github|12345678"
    public string DisplayName { get; set; }
    public string AvatarUrl { get; set; }
    public bool Exists { get; set; }
}
```

### Commands (handled by the actor)
```csharp
public record CreateUserCommand(string ProviderId, string DisplayName, string AvatarUrl);
public record UpdateProfileCommand(string DisplayName, string AvatarUrl);
```

### Events (emitted by the actor)
```csharp
public record UserCreatedEvent(
    Guid UserId,
    string ProviderId,
    string DisplayName,
    string AvatarUrl,
    string ActorId,
    DateTime Timestamp
) : IEvent;

public record UserProfileUpdatedEvent(
    Guid UserId,
    string DisplayName,
    string AvatarUrl,
    string ActorId,
    DateTime Timestamp
) : IEvent;
```

### Command Handlers

#### `CreateUserCommand`
1. Check if user already exists (state `Exists` is true after rehydration).
2. If exists: return existing `UserId` without emitting any event (idempotent).
3. If not exists:
   - Generate new `UserId` (UUID v4).
   - Emit `UserCreatedEvent` with all fields.
   - Set state: `Exists = true`, populate all fields.
4. Return `{ userId: "..." }`.

#### `UpdateProfileCommand`
1. Check if user exists. If not, return error.
2. Compare incoming `DisplayName` and `AvatarUrl` with current state.
3. If neither changed, return success without emitting event.
4. If either changed, emit `UserProfileUpdatedEvent`.
5. Update state.
6. Return success.

### Stream ID Convention
- Stream: `user-{userId}` (e.g., `user-a1b2c3d4-e5f6-7890-abcd-ef1234567890`).
- The actor manager routes commands to the correct actor based on the stream ID.

### Event Routing to Redpanda
- All events from User actors go to topic `vut.user-events`.
- Message key: `userId` (string).
- This routing is handled by the base class from Task 04.

### File Structure
```
src/
  Vut.ActorService/
    Actors/
      UserActor.cs
      UserActorManager.cs
    Events/
      UserCreatedEvent.cs
      UserProfileUpdatedEvent.cs
    Commands/
      CreateUserCommand.cs
      UpdateProfileCommand.cs
```

### Unit Tests
```
tests/
  Vut.ActorService.Tests/
    Actors/
      UserActorTests.cs
```

Test cases:
- `CreateUser` with new provider ID -> emits `UserCreated`, returns `userId`.
- `CreateUser` with existing provider ID -> idempotent, no new event, returns existing `userId`.
- `UpdateProfile` with changed displayName -> emits `UserProfileUpdated`.
- `UpdateProfile` with no changes -> no event emitted.
- `UpdateProfile` for non-existent user -> returns error.

## Acceptance Criteria

- [ ] User Actor handles `CreateUser` and `UpdateProfile` commands correctly.
- [ ] `CreateUser` is idempotent -- duplicate calls return the same `userId` without extra events.
- [ ] `UpdateProfile` only emits an event when data actually changes.
- [ ] Events are appended to `user-{userId}` stream in KurrentDB.
- [ ] Events are published to `vut.user-events` Redpanda topic.
- [ ] Actor correctly rehydrates from KurrentDB on activation.
- [ ] All unit tests pass.
- [ ] The actor returns structured responses (JSON) that the BFF can parse.

## Dependencies

- Task 04 (.NET Actor Service Foundation) -- must be complete.

## Notes

- The `ProviderId` (Auth0 `sub` claim) is the natural key for user lookup. The BFF uses this to check if a user exists before calling `CreateUser`.
- The actor manager needs a `GetOrCreateByProviderId` method that first checks the read model, then falls back to creating a new actor. This is an orchestration concern handled by the BFF/gRPC layer.
- Consider adding a `UserState.Applied(IEvent @event)` method for clean event replay during rehydration.
