# Task 05: User Actor Implementation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 05 |
| **Priority** | P1 |
| **Estimated Effort** | 2 days |

## Description

Implement the User Actor that handles user creation, multi-provider identity linking, email verification, and profile updates. This actor manages the `user-{userId}` event stream in KurrentDB, processes commands from the BFF, and emits events that feed into the read model via Redpanda.

## Architecture Reference

- Architecture doc Section 5.3 (User Actor)
- Architecture doc Section 8.1 (First-Time User Sign-In sequence)
- Architecture doc Section 8.2 (Email Verification sequence)
- Architecture doc Section 8.3 (Returning User Sign-In sequence)

## Technical Requirements

### User Actor State
```csharp
public class UserState
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public string EmailVerificationToken { get; set; } = string.Empty;
    public DateTime EmailVerificationTokenExpiresAt { get; set; }
    public Dictionary<string, IdentityEntry> Identities { get; set; } = new();
    public bool Exists { get; set; }
}

public class IdentityEntry
{
    public string ProviderId { get; set; } = string.Empty;   // Auth0 subject, e.g., "github|12345678"
    public string ProviderName { get; set; } = string.Empty; // "github", "google", "microsoft"
    public string Email { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
}
```

### Commands (handled by the actor)
```csharp
public record CreateUserCommand(
    string ProviderId,
    string ProviderName,
    string DisplayName,
    string AvatarUrl,
    string Email
);

public record LinkIdentityCommand(
    Guid UserId,
    string ProviderId,
    string ProviderName,
    string Email
);

public record UpdateProfileCommand(string DisplayName, string AvatarUrl);

public record RequestEmailVerificationCommand(Guid UserId, string Email);

public record VerifyEmailCommand(Guid UserId, string Token);
```

### Events (emitted by the actor)
```csharp
public record UserCreatedEvent(
    Guid UserId,
    string DisplayName,
    string AvatarUrl,
    string Email,
    string ActorId,
    DateTime Timestamp
) : IEvent;

public record IdentityLinkedEvent(
    Guid UserId,
    string ProviderId,
    string ProviderName,
    string Email,
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

public record EmailVerificationRequestedEvent(
    Guid UserId,
    string Email,
    string Token,
    string ActorId,
    DateTime Timestamp
) : IEvent;

public record EmailVerifiedEvent(
    Guid UserId,
    string Email,
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
   - Emit `UserCreatedEvent` with profile fields (no `providerId`).
   - Emit `IdentityLinkedEvent` for the initial provider.
   - Set state: `Exists = true`, populate all fields including `Identities` map.
4. Return `{ userId: "..." }`.

#### `LinkIdentityCommand`
1. Check if `ProviderId` is already in `Identities` map for this user — if so, no-op.
2. Emit `IdentityLinkedEvent`.
3. Add to `Identities` map in state.

#### `UpdateProfileCommand`
1. Check if user exists. If not, return error.
2. Compare incoming `DisplayName` and `AvatarUrl` with current state.
3. If neither changed, return success without emitting event.
4. If either changed, emit `UserProfileUpdatedEvent`.
5. Update state.

#### `RequestEmailVerificationCommand`
1. Generate a 6-digit random code as the verification token.
2. Set token expiry to 15 minutes from now.
3. Emit `EmailVerificationRequestedEvent`.
4. Return the token (the actor service sends the email via SMTP after receiving this event).

#### `VerifyEmailCommand`
1. Validate token matches `EmailVerificationToken` in state and hasn't expired.
2. If valid: emit `EmailVerifiedEvent`, set `IsEmailVerified = true`, update `Email`, clear token.
3. If invalid or expired: return error.

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
      IdentityLinkedEvent.cs
      UserProfileUpdatedEvent.cs
      EmailVerificationRequestedEvent.cs
      EmailVerifiedEvent.cs
    Commands/
      CreateUserCommand.cs
      LinkIdentityCommand.cs
      UpdateProfileCommand.cs
      RequestEmailVerificationCommand.cs
      VerifyEmailCommand.cs
```

### Unit Tests
```
tests/
  Vut.ActorService.Tests/
    Actors/
      UserActorTests.cs
```

Test cases:
- `CreateUser` with new provider ID -> emits `UserCreated` + `IdentityLinked`, returns `userId`.
- `CreateUser` with existing user -> idempotent, no new events, returns existing `userId`.
- `LinkIdentity` with new provider -> emits `IdentityLinked`.
- `LinkIdentity` with already-linked provider -> no-op.
- `UpdateProfile` with changed displayName -> emits `UserProfileUpdated`.
- `UpdateProfile` with no changes -> no event emitted.
- `UpdateProfile` for non-existent user -> returns error.
- `RequestEmailVerification` -> emits `EmailVerificationRequested`, token stored in state.
- `VerifyEmail` with valid token -> emits `EmailVerified`, `IsEmailVerified` becomes true.
- `VerifyEmail` with expired token -> returns error.
- `VerifyEmail` with wrong token -> returns error.

## Acceptance Criteria

- [ ] User Actor handles `CreateUser`, `LinkIdentity`, `UpdateProfile`, `RequestEmailVerification`, and `VerifyEmail` commands correctly.
- [ ] `CreateUser` is idempotent -- duplicate calls return the same `userId` without extra events.
- [ ] `CreateUser` emits both `UserCreated` and `IdentityLinked` events for the initial provider.
- [ ] `LinkIdentity` adds a new provider to the user's identity map.
- [ ] `UpdateProfile` only emits an event when data actually changes.
- [ ] `RequestEmailVerification` generates a 6-digit code with 15-minute expiry.
- [ ] `VerifyEmail` validates the token and sets `IsEmailVerified = true`.
- [ ] Events are appended to `user-{userId}` stream in KurrentDB.
- [ ] Events are published to `vut.user-events` Redpanda topic.
- [ ] Actor correctly rehydrates from KurrentDB on activation.
- [ ] All unit tests pass.

## Dependencies

- Task 04 (.NET Actor Service Foundation) -- must be complete.

## Notes

- The BFF handles the orchestration of looking up users by `providerId` (via the read model's `user_identity` table) before deciding whether to call `CreateUser` or `LinkIdentity`. The actor itself only manages the aggregate state.
- The email verification token is stored in actor state. If the actor is passivated and rehydrated, the token must survive (it's replayed from the `EmailVerificationRequested` event).
- Consider adding a `UserState.Apply(IEvent @event)` method for clean event replay during rehydration.
