# Add SignInResult DTO and IUserGrain.SignIn method

**Developer Type:** Backend
**Priority:** Critical
**Phase:** Foundation

## Description
Add the result DTO for the sign-in endpoint and extend the `IUserGrain` interface with a `SignIn` method that encapsulates the provider-lookup / email-lookup / link-or-create logic. This task does NOT implement the logic itself; it only adds the interface contract. Task 02 implements the logic.

## Architecture Reference
Section "Sign-In Flow" (frontend callback.astro → POST /api/users/sign-in):
> The endpoint should be idempotent — return `{ userId, isEmailVerified }` regardless of create/link/existing path

## Technical Requirements
- Add `SignInResult` record to `UserController.cs` (near the other request/result records):
  ```csharp
  public record SignInResult(Guid UserId, bool IsEmailVerified);
  ```
- Add `SignIn` method to `IUserGrain.cs`:
  ```csharp
  Task<SignInResult> SignIn(string providerId, string providerName, string displayName, string avatarUrl, string? email);
  ```
- The method signature should match what the controller will pass: providerId, providerName, displayName, avatarUrl, email.

## Acceptance Criteria
- [ ] `SignInResult` record is added to `UserController.cs`
- [ ] `SignIn` method signature is added to `IUserGrain.cs`
- [ ] No implementation logic is added in this task — only the interface contract

## Dependencies
None — can start immediately

## Estimated Effort
XS (1-2h)

## Notes
- The `SignIn` method will eventually need to look up users by provider ID and by email. Orleans supports composite grain keys via `GetGrain(IUserGrain, primaryKey, keyExtension)` — the key extension can be the providerId, enabling a "user-by-provider" grain variant. However, the simplest path given the existing grain design is to have `SignIn` use a well-known grain address pattern. Defer the key strategy decision to task 02.