# Add POST /api/users/sign-in endpoint to UserController

**Developer Type:** Backend
**Priority:** Critical
**Phase:** Backend API

## Description
Add a new `POST /api/users/sign-in` endpoint to `UserController.cs` that accepts `{ providerId, providerName, displayName, avatarUrl, email }` and delegates to `UserGrain.SignIn`. This is the single entry point that replaces the 3 separate BFF calls.

## Architecture Reference
Section "Sign-In Flow" from the desired architecture:
```
Frontend → POST /api/users/sign-in → { providerId, providerName, displayName, avatarUrl, email }
Backend returns: { userId, isEmailVerified }
```

## Technical Requirements
- Add `SignInRequest` record to `UserController.cs`:
  ```csharp
  public record SignInRequest(
      string ProviderId,
      string ProviderName,
      string DisplayName,
      string AvatarUrl,
      string? Email);
  ```
- Add the `POST /api/users/sign-in` endpoint:
  ```csharp
  [HttpPost("sign-in")]
  public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
  {
      // Use a well-known approach to invoke the sign-in logic.
      // Since SignIn is static/orchestration logic, we activate a "sign-in coordinator" grain
      // or invoke directly on the user grain with the given providerId as key.
      //
      // The approach: use IUserLookupGrain to resolve, then activate IUserGrain.
      // But the cleanest API is: the controller just calls IUserGrain.SignIn(...)
      // which internally uses the lookup grain. However, IUserGrain is keyed by Guid, not string.
      //
      // Best approach: create a ISignInGrain : IGrainWithStringKey (key = providerId or "default")
      // that orchestrates the full sign-in flow and returns SignInResult.
      //
      // OR: the controller calls a static UserGrain.SignIn(...) helper that does the same.
      // But Orleans grains can't have static methods that use DI.
      //
      // Simplest: add an IUserGrainFactory or use IGrainFactory to call the lookup grain,
      // then activate the user grain. Wrap this in a helper service registered in DI.
      //
      // Actually the cleanest pattern: the endpoint uses IGrainFactory directly:
      //   1. Get the UserLookupGrain ("user-lookup")
      //   2. Try GetUserIdByProviderId(request.ProviderId)
      //   3. If found, get IUserGrain(userId), return SignInResult(state.UserId, state.IsEmailVerified)
      //   4. If not, Try GetUserIdByEmail(request.Email)
      //   5. If found, link identity, return SignInResult(existingUserId, existingIsEmailVerified)
      //   6. If not, create new user grain and return SignInResult(newUserId, false)
      //
      // But the orchestrator could be a dedicated ISignInCoordinator grain to keep controller thin.
  }
  ```

**Recommended implementation approach:**
Add a `SignInCoordinator` grain (`ISignInCoordinator : IGrainWithStringKey`) registered as a singleton that encapsulates the full sign-in orchestration. The controller simply calls:
```csharp
var result = await _grainFactory.GetGrain<ISignInCoordinator>("default").SignIn(request);
return Ok(result);
```

`SignInCoordinator.SignIn` internally:
1. Calls `UserLookupGrain.GetUserIdByProviderId(providerId)` → if found, return existing
2. Calls `UserLookupGrain.GetUserIdByEmail(email)` → if found, activate user grain, link identity, return
3. Otherwise: `var newUserId = Guid.NewGuid(); var userGrain = GetGrain<IUserGrain>(newUserId); await userGrain.CreateUser(...); return new SignInResult(newUserId, false)`

The controller endpoint should be thin — delegate everything to the coordinator grain.

## Acceptance Criteria
- [ ] `POST /api/users/sign-in` endpoint added to `UserController`
- [ ] `SignInRequest` record defined
- [ ] Endpoint delegates to `ISignInCoordinator` grain
- [ ] Returns `Ok(new SignInResult(userId, isEmailVerified))` on success
- [ ] Returns appropriate error response on failure (500 with message)
- [ ] Endpoint is idempotent — same providerId returns same userId

## Dependencies
- Task 02 (SignIn logic in UserGrain must be implemented first)

## Estimated Effort
S (2-4h)

## Notes
- The controller should have no branching logic — just call the coordinator grain and return the result.
- Consider adding logging at the controller level for observability.
- The `SignInCoordinator` should be a stateless orchestrator; it activates other grains but maintains no state itself.