# Add integration tests for sign-in endpoint

**Developer Type:** Backend
**Priority:** High
**Phase:** Integration

## Description
Add integration tests that verify the complete sign-in flow end-to-end: from the HTTP endpoint (`POST /api/users/sign-in`) through the grain orchestration to the event store. Tests should cover all three paths: existing user by providerId, email-match and link, and new user creation.

## Architecture Reference
Section "Sign-In Flow" — the endpoint must be idempotent and return `{ userId, isEmailVerified }` regardless of path.

## Technical Requirements
Create a new test file `UserControllerSignInTests.cs` (or add to an existing integration test suite) that:

1. **Setup**: Uses `WebApplicationFactory` or similar to spin up the Silo with test harness, using `InMemoryEventStreamClient` and `FakeTimeProvider`.

2. **Tests**:
   - `SignIn_NewUser_Returns201WithUserIdAndIsEmailVerifiedFalse` — POST with new providerId/email, verify response
   - `SignIn_SameProviderId_IsIdempotent_ReturnsSameUserId` — call twice, verify same userId
   - `SignIn_EmailMatch_LinksIdentity_ReturnsExistingUserId` — create user with email, then call with same email but different providerId, verify existing userId
   - `SignIn_UnknownProvider_NoEmail_CreatesNewUser` — call with new providerId and no email, verify new userId
   - `SignIn_ExistingProviderId_ReturnsExistingUser_IsEmailVerifiedCorrect` — verify the `isEmailVerified` field is correctly returned from existing user state

3. **Test approach**:
   - Each test creates a fresh `TestCluster` or uses a shared one with isolated event stores per test
   - Uses `HttpClient` to call `http://localhost:{port}/api/users/sign-in`
   - Verifies HTTP 200 and the JSON body shape `{ userId: string, isEmailVerified: boolean }`
   - Verifies the event store has the correct events for each path

4. **Test isolation**:
   - Each test should use a unique `userId` (generated via `Guid.NewGuid()`) to avoid cross-test contamination
   - Or use unique providerId/email combinations per test

## Acceptance Criteria
- [ ] Tests cover all three sign-in paths (providerId match, email match, new user)
- [ ] Idempotency verified (same providerId → same userId)
- [ ] `isEmailVerified` correctly reflects existing user state
- [ ] HTTP response codes are correct (200 for success, 500 for unexpected errors)
- [ ] Tests use the Orleans test infrastructure correctly (no manual silo management)

## Dependencies
- Task 03 (endpoint must be implemented)
- Task 04 (grain tests should pass first)

## Estimated Effort
S (2-4h)

## Notes
- If the project already has integration tests for `UserController`, add the sign-in tests there.
- The `WebApplicationFactory` approach requires the Silo to be hostable as an ASP.NET Core app — verify the Silo project has the proper `Program.cs` host setup.
- Ensure `EventTypeMapping` is registered before the Silo starts (as done in other test files).