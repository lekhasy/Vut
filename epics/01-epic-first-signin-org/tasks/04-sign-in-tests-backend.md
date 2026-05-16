# Add tests for sign-in orchestration logic

**Developer Type:** Backend
**Priority:** High
**Phase:** Backend API

## Description
Add tests in `UserGrainTests.cs` covering the new sign-in orchestration logic. Also add a new `UserGrainSignInTests.cs` test file for the complete integration scenarios involving `SignInCoordinator` and `UserLookupGrain`.

## Architecture Reference
Section "Sign-In Flow" — the endpoint must be idempotent and return `{ userId, isEmailVerified }` regardless of path (existing user / link identity / create new).

## Technical Requirements

### A. Add to `UserGrainTests.cs`
Add a new test section for the `SignIn` method (once implemented as part of Task 02, you may need to add it to `IUserGrain` separately):

> Note: Currently `SignIn` is not on `IUserGrain` — it is orchestrated by `SignInCoordinator`. Tests for `SignInCoordinator` go in a new file. Tests in `UserGrainTests.cs` cover the individual grain methods (`CreateUser`, `LinkIdentity`).

Add these tests to `UserGrainTests.cs`:

1. **`SignIn_NewUser_CreatesUserAndReturnsResult`** — simulates the coordinator calling `CreateUser` on a new grain
2. **`SignIn_ExistingUserByProviderId_ReturnsExistingUser`** — simulate a user grain that already exists and `SignIn` returns its state
3. **`SignIn_LinkIdentity_UpdatesExistingUser`** — simulate the email-match path where `LinkIdentity` is called on an existing user

Actually, since `SignIn` is on `SignInCoordinator` not `UserGrain`, add these to `UserGrainTests.cs` to verify the component methods work correctly:
- `CreateUser_SetsIsEmailVerifiedFalse` — verify the created user has `IsEmailVerified = false`
- `LinkIdentity_WithEmail_UpdatesExistingUser` — verify linking a new identity works

### B. New file: `UserGrainSignInTests.cs`
Create a new test class that tests the `SignInCoordinator` grain end-to-end:

```csharp
[Collection("Orleans TestCluster")]
public sealed class UserGrainSignInTests : IAsyncLifetime
{
    // Tests:
    // 1. SignIn_NewUser_ReturnsNewUserIdAndIsEmailVerifiedFalse
    // 2. SignIn_SameProviderIdTwice_ReturnsSameUserId (idempotency)
    // 3. SignIn_UnknownProvider_EmailMatch_LinksIdentityAndReturns
    // 4. SignIn_UnknownProvider_NoEmailMatch_CreatesNewUser
    // 5. SignIn_AfterLinkIdentity_CanBeCalledAgainWithSameResult
}
```

Each test should:
- Use the existing `InMemoryEventStreamClient` and `TestSiloConfigurator`
- Register `IUserLookupGrain` and `ISignInCoordinator` in the test cluster
- Verify the returned `SignInResult` has the correct `userId` and `isEmailVerified`
- Verify the correct events were emitted in the event store

### C. Verify existing tests still pass
- Ensure all existing `UserGrainTests` tests still pass (no regression)
- Ensure `UserGrainRehydrationTests` still pass
- Ensure `UserGrainTimeDependentTests` still pass

## Acceptance Criteria
- [ ] New tests cover: new user creation, idempotency by providerId, email-match identity link, non-existent user create
- [ ] All existing tests continue to pass
- [ ] Tests use `InMemoryEventStreamClient` and `FakeTimeProvider` correctly
- [ ] Test names clearly describe the scenario being tested

## Dependencies
- Task 02 (SignIn logic must be implemented first)
- Task 03 (SignIn endpoint must be implemented first, though tests can be written in parallel once the interface is known)

## Estimated Effort
M (4-8h)

## Notes
- Use `cluster.GrainFactory.GetGrain<ISignInCoordinator>("default")` to get the coordinator
- Use `cluster.GrainFactory.GetGrain<IUserLookupGrain>("user-lookup")` to inspect index state
- For tests needing time control, use `FakeTimeProvider` via `TestSiloConfigurator.SharedTimeProvider`
- Each test should reset `EventTypeMapping` and `SharedEventStreamClient` / `SharedTimeProvider` in `InitializeAsync`