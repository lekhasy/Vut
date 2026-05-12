# Task 05: User Grain Implementation

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 05 |
| **Priority** | P1 |
| **Estimated Effort** | 2 days |

## Description

Implement the User Grain (virtual actor) that handles user creation, multi-provider identity linking, email verification, and profile updates. This grain manages the `user-{userId}` event stream in KurrentDB via direct `EventStoreClient` integration (inherited from `EventSourcedGrain<TState>`), exposes strongly-typed methods on `IUserGrain`, and emits events that feed into the read model via KurrentDB persistent subscriptions.

There is **no UserActorManager**. The grain is auto-activated by the Orleans runtime on first call to `IGrainFactory.GetGrain<IUserGrain>(userId)`. API controllers call grain methods directly — no gRPC or cluster messages.

## Architecture Reference

- Architecture doc Section 5.1 (Base Grain Abstraction — EventSourcedGrain)
- Architecture doc Section 5.3 (User Grain)
- Architecture doc Section 5.5 (Calling Grains from API Controllers)
- Architecture doc Section 8.1 (First-Time User Sign-In sequence)
- Architecture doc Section 8.2 (Email Verification sequence)
- Architecture doc Section 8.3 (Returning User Sign-In sequence)

## Technical Requirements

### User Grain Interface

```csharp
public interface IUserGrain : IGrainWithGuidKey
{
    Task<CreateUserResult> CreateUser(
        string providerId, string providerName,
        string displayName, string avatarUrl, string? email);

    Task LinkIdentity(
        string providerId, string providerName, string? email);

    Task UpdateProfile(string displayName, string avatarUrl);

    Task<string> RequestEmailVerification(string email);

    Task VerifyEmail(string token);
}
```

Each method is a native C# async method on the grain interface — no gRPC proto definitions, no cluster command messages, no `HandleMessage` switch statement.

### User Grain State
```csharp
public class UserState
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string? Email { get; set; }
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
    public string? Email { get; set; }
    public DateTime LinkedAt { get; set; }
}
```

### Events (persisted to KurrentDB via EventStoreClient)
```csharp
public record UserCreatedEvent(
    Guid UserId,
    string DisplayName,
    string AvatarUrl,
    string? Email,
    string ActorId,
    DateTime Timestamp
) : IEvent;

public record IdentityLinkedEvent(
    Guid UserId,
    string ProviderId,
    string ProviderName,
    string? Email,
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

### Grain Implementation

The `UserGrain` inherits from `EventSourcedGrain<UserState>` (built in Task 04) and implements `IUserGrain`:

```csharp
public class UserGrain : EventSourcedGrain<UserState>, IUserGrain
{
    public UserGrain(EventStoreClient client)
        : base(client, $"user-{/* GrainKey from Orleans */}") { }

    // Construct stream ID from the grain's Guid key
    // Stream ID: user-{userId}

    protected override void Apply(UserState state, object @event)
    {
        switch (@event)
        {
            case UserCreatedEvent e:
                state.UserId = e.UserId;
                state.DisplayName = e.DisplayName;
                state.AvatarUrl = e.AvatarUrl;
                state.Email = e.Email;
                state.IsEmailVerified = false;
                state.Exists = true;
                break;
            case IdentityLinkedEvent e:
                state.Identities[e.ProviderId] = new IdentityEntry
                {
                    ProviderId = e.ProviderId,
                    ProviderName = e.ProviderName,
                    Email = e.Email,
                    LinkedAt = e.Timestamp
                };
                break;
            case UserProfileUpdatedEvent e:
                state.DisplayName = e.DisplayName;
                state.AvatarUrl = e.AvatarUrl;
                break;
            case EmailVerificationRequestedEvent e:
                state.EmailVerificationToken = e.Token;
                state.EmailVerificationTokenExpiresAt = e.Timestamp.AddMinutes(15);
                break;
            case EmailVerifiedEvent e:
                state.IsEmailVerified = true;
                state.Email = e.Email;
                state.EmailVerificationToken = string.Empty;
                break;
        }
    }

    public async Task<CreateUserResult> CreateUser(
        string providerId, string providerName,
        string displayName, string avatarUrl, string? email)
    {
        // Idempotent: if already created, return existing userId
        if (State.Exists)
            return new CreateUserResult(State.UserId);

        var userId = this.GetPrimaryKey();
        await EmitEvent(new UserCreatedEvent(
            userId, displayName, avatarUrl, email,
            userId.ToString(), DateTime.UtcNow));
        await EmitEvent(new IdentityLinkedEvent(
            userId, providerId, providerName, email,
            userId.ToString(), DateTime.UtcNow));

        return new CreateUserResult(userId);
    }

    public async Task LinkIdentity(
        string providerId, string providerName, string? email)
    {
        if (State.Identities.ContainsKey(providerId))
            return; // Already linked, no-op

        await EmitEvent(new IdentityLinkedEvent(
            State.UserId, providerId, providerName, email,
            State.UserId.ToString(), DateTime.UtcNow));
    }

    public async Task UpdateProfile(string displayName, string avatarUrl)
    {
        if (!State.Exists)
            throw new InvalidOperationException("User does not exist");

        if (State.DisplayName == displayName && State.AvatarUrl == avatarUrl)
            return; // No changes

        await EmitEvent(new UserProfileUpdatedEvent(
            State.UserId, displayName, avatarUrl,
            State.UserId.ToString(), DateTime.UtcNow));
    }

    public async Task<string> RequestEmailVerification(string email)
    {
        var token = Random.Shared.Next(100000, 999999).ToString();
        await EmitEvent(new EmailVerificationRequestedEvent(
            State.UserId, email, token,
            State.UserId.ToString(), DateTime.UtcNow));
        return token;
    }

    public async Task VerifyEmail(string token)
    {
        if (State.EmailVerificationToken != token)
            throw new InvalidOperationException("Invalid verification token");
        if (DateTime.UtcNow > State.EmailVerificationTokenExpiresAt)
            throw new InvalidOperationException("Verification token expired");

        await EmitEvent(new EmailVerifiedEvent(
            State.UserId, State.Email!,
            State.UserId.ToString(), DateTime.UtcNow));
    }
}
```

### Grain Method Details

#### `CreateUser`
1. Check if user already exists (state `Exists` is true after rehydration from KurrentDB events).
2. If exists: return existing `UserId` without emitting any event (idempotent).
3. If not exists:
   - Use `this.GetPrimaryKey()` to get the grain's Guid key as the `UserId`.
   - Emit `UserCreatedEvent` with profile fields.
   - Emit `IdentityLinkedEvent` for the initial provider.
   - State is updated by `Apply` after each `EmitEvent`.
4. Return `CreateUserResult { UserId }`.

#### `LinkIdentity`
1. Check if `ProviderId` is already in `Identities` map for this user — if so, no-op.
2. Emit `IdentityLinkedEvent`.
3. State updated automatically via `Apply`.

#### `UpdateProfile`
1. Check if user exists. If not, throw error.
2. Compare incoming `DisplayName` and `AvatarUrl` with current state.
3. If neither changed, return without emitting event.
4. If either changed, emit `UserProfileUpdatedEvent`.

#### `RequestEmailVerification`
1. Generate a 6-digit random code as the verification token.
2. Set token expiry to 15 minutes from now (set in `Apply`).
3. Emit `EmailVerificationRequestedEvent`.
4. Return the token (the API controller sends the email via Resend after receiving this).

#### `VerifyEmail`
1. Validate token matches `EmailVerificationToken` in state and hasn't expired.
2. If valid: emit `EmailVerifiedEvent`, state updated via `Apply` — sets `IsEmailVerified = true`, updates `Email`, clears token.
3. If invalid or expired: throw error.

### Stream ID Convention
- Stream: `user-{userId}` (e.g., `user-a1b2c3d4-e5f6-7890-abcd-ef1234567890`).
- The stream ID is constructed in the grain constructor from the grain's Guid key.
- The grain is activated by: `grainFactory.GetGrain<IUserGrain>(userId)`.
- Projectors subscribe to KurrentDB persistent subscriptions directly.

### API Controller (co-hosted in Orleans silo)

```csharp
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;

    public UserController(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request)
    {
        var userId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        var result = await grain.CreateUser(
            request.ProviderId, request.ProviderName,
            request.DisplayName, request.AvatarUrl, request.Email);
        return CreatedAtAction(nameof(CreateUser), result);
    }

    [HttpPost("{userId}/link-identity")]
    public async Task<IActionResult> LinkIdentity(
        Guid userId, [FromBody] LinkIdentityRequest request)
    {
        var grain = _grainFactory.GetGrain<IUserGrain>(userId);
        await grain.LinkIdentity(
            request.ProviderId, request.ProviderName, request.Email);
        return Ok();
    }
}
```

### File Structure
```
src/
  Velucid.Silo/
    Grains/
      IUserGrain.cs
      UserGrain.cs
    Events/
      UserCreatedEvent.cs
      IdentityLinkedEvent.cs
      UserProfileUpdatedEvent.cs
      EmailVerificationRequestedEvent.cs
      EmailVerifiedEvent.cs
    Models/
      UserState.cs
      IdentityEntry.cs
      CreateUserResult.cs
    Controllers/
      UserController.cs
```

### Unit Tests
```
tests/
  Velucid.Silo.Tests/
    Grains/
      UserGrainTests.cs
```

Test cases (using `Microsoft.Orleans.TestingHost` or grain test utilities):
- `CreateUser` with new provider ID → emits `UserCreated` + `IdentityLinked`, returns `userId`.
- `CreateUser` with existing user → idempotent, no new events, returns existing `userId`.
- `LinkIdentity` with new provider → emits `IdentityLinked`.
- `LinkIdentity` with already-linked provider → no-op.
- `UpdateProfile` with changed displayName → emits `UserProfileUpdated`.
- `UpdateProfile` with no changes → no event emitted.
- `UpdateProfile` for non-existent user → throws error.
- `RequestEmailVerification` → emits `EmailVerificationRequested`, token stored in state.
- `VerifyEmail` with valid token → emits `EmailVerified`, `IsEmailVerified` becomes true.
- `VerifyEmail` with expired token → throws error.
- `VerifyEmail` with wrong token → throws error.
- State rehydration from events matches original state after activation (`OnActivateAsync`).

## Acceptance Criteria

- [ ] `IUserGrain` interface defined with `CreateUser`, `LinkIdentity`, `UpdateProfile`, `RequestEmailVerification`, and `VerifyEmail` methods.
- [ ] `UserGrain` inherits from `EventSourcedGrain<UserState>` and implements `IUserGrain`.
- [ ] `CreateUser` is idempotent — duplicate calls return the same `userId` without extra events.
- [ ] `CreateUser` emits both `UserCreated` and `IdentityLinked` events for the initial provider.
- [ ] `LinkIdentity` adds a new provider to the user's identity map.
- [ ] `UpdateProfile` only emits an event when data actually changes.
- [ ] `RequestEmailVerification` generates a 6-digit code with 15-minute expiry.
- [ ] `VerifyEmail` validates the token and sets `IsEmailVerified = true`.
- [ ] Events are persisted to `user-{userId}` stream in KurrentDB via `EventStoreClient`.
- [ ] Grain correctly rehydrates from KurrentDB on activation via `OnActivateAsync`.
- [ ] Grain deactivation managed by `GrainCollectionOptions.CollectionAge` (30 minutes) and `DelayDeactivation`.
- [ ] API controller calls grain via `IGrainFactory.GetGrain<IUserGrain>(userId)`.
- [ ] All unit tests pass.

## Dependencies

- Task 04 (.NET Orleans Silo Foundation) — must be complete (provides `EventSourcedGrain<TState>` base class, Orleans silo setup, co-hosted ASP.NET Core API, and `EventStoreClient` singleton).

## Notes

- The BFF handles the orchestration of looking up users by `providerId` (via the read model's `user_identity` table) before deciding whether to call `CreateUser` or `LinkIdentity`. The grain itself only manages the aggregate state.
- The email verification token is stored in grain state. If the grain is deactivated and re-activated, the token survives because it's replayed from the `EmailVerificationRequested` event in the KurrentDB stream during `OnActivateAsync`.
- No cluster kind registration needed — Orleans discovers grain types automatically via the `IUserGrain` interface and DI.
